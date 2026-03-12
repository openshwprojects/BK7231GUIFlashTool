using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    public class XR806Flasher : BaseFlasher
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------
        const int XR_ROM_BAUD            = 115200;
        const int XR_SECTOR_SIZE         = 0x200;       // 512 bytes
        const int XR_ERASE_BLOCK_SIZE    = 0x10000;     // 64 KB
        const int XR_WRITE_CHUNK_SIZE    = 0x4000;      // 16 KB  (32 sectors)
        const int XR_WRITE_SECTORS       = XR_WRITE_CHUNK_SIZE / XR_SECTOR_SIZE;
        const int XR_MAX_WORK_BAUD       = 921600;      // BROM v3 hard cap (OpenUart clamps here)
        const uint AWIH_MAGIC            = 0x48495741;  // "AWIH" – XR section header magic

        // Host->device flag byte (confirmed from ELF: always 0x04, 0x00)
        const byte BROM_HOST_FLAGS       = 0x04;

        // Response flag bits (from CheckAck analysis)
        const byte BROM_FLAG_ERROR       = 0x01;   // bit 0 – command failed
        const byte BROM_FLAG_HAS_PAYLOAD = 0x02;   // bit 1 – response carries data

        // Opcodes
        const byte OP_GET_FLASH_ID       = 0x18;
        const byte OP_CHANGE_BAUD        = 0x10;
        const byte OP_ERASE              = 0x19;
        const byte OP_READ               = 0x1A;
        const byte OP_WRITE              = 0x1B;

        // Baud rates supported by PhoenixMC (name_arr from ELF data section 0x6182c0).
        // XR806 BROM v3 additionally caps the working baud at XR_MAX_WORK_BAUD (921600).
        // The ROM always starts at 115200; valid working bauds are entries > 115200.
        static readonly int[] SUPPORTED_BAUDS = { 115200, 230400, 460800, 921600 };

        // Known-good hardcoded request (checksum pre-verified against PhoenixMC)
        static readonly byte[] GET_FLASH_ID_REQUEST = new byte[]
            { 0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51, 0x00, 0x00, 0x00, 0x01, 0x18 };

        // Known XR .img section IDs
        static readonly Dictionary<uint, string> SECTION_NAMES = new Dictionary<uint, string>
        {
            { 0xA5FF5A00, "boot"     },
            { 0xA5FE5A01, "app"      },
            { 0xA5FD5A02, "app_xip"  },
            { 0xA5FA5A05, "wlan_bl"  },
            { 0xA5F95A06, "wlan_fw"  },
            { 0xA5F85A07, "wlan_sdd" },
        };

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------
        MemoryStream readResult;
        byte[]       flashID;
        int          flashSizeBytes = 2 * 0x100000;
        byte         bromVersion  = 0xFF;

        // -----------------------------------------------------------------------
        // Packet structures
        // -----------------------------------------------------------------------
        struct BROMResponse
        {
            public byte   Flags;
            public byte   BromVersion;
            public ushort Checksum;
            public int    PayloadLength;
            public byte[] Payload;
            public bool   IsError => (Flags & BROM_FLAG_ERROR) != 0;
        }

        struct XRImageSectionHeader
        {
            public uint   Magic;
            public uint   Version;
            public ushort HeaderChecksum;
            public ushort DataChecksum;
            public uint   DataSize;
            public uint   LoadAddr;
            public uint   Entry;
            public uint   BodyLen;
            public uint   Attribute;
            public uint   NextAddr;
            public uint   SectionId;
            // 6 private dwords (0x28..0x3F) – not individually named
        }

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------
        public XR806Flasher(CancellationToken ct) : base(ct)
        {
        }

        // -----------------------------------------------------------------------
        // Port setup
        // -----------------------------------------------------------------------
        bool DoGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString()
                              + " " + DateTime.Now.ToLongTimeString()
                              + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            try
            {
                serial = new SerialPort(serialName, XR_ROM_BAUD);
                serial.ReadTimeout = 250;
                serial.WriteTimeout = 5000;
                serial.ReadBufferSize = 1024 * 1024;
                serial.WriteBufferSize = 1024 * 1024;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
            }
            catch (Exception ex)
            {
                addErrorLine("Port setup failed: " + ex.Message);
                return false;
            }
            addLogLine("Port ready!");
            return true;
        }

        // -----------------------------------------------------------------------
        // Serial helpers
        // -----------------------------------------------------------------------
        void DrainInput(int quietMillis = 150, int hardLimitMillis = 750)
        {
            var total = Stopwatch.StartNew();
            var quiet = Stopwatch.StartNew();
            while (total.ElapsedMilliseconds < hardLimitMillis)
            {
                int waiting = serial.BytesToRead;
                if (waiting > 0)
                {
                    var dump = new byte[waiting];
                    serial.Read(dump, 0, dump.Length);
                    quiet.Restart();
                    continue;
                }
                if (quiet.ElapsedMilliseconds >= quietMillis)
                    break;
                Thread.Sleep(10);
            }
        }

        byte[] ReadExact(int count, int timeoutMillis)
        {
            var buf = new byte[count];
            int got = 0;
            var sw  = Stopwatch.StartNew();
            while (got < count && sw.ElapsedMilliseconds < timeoutMillis)
            {
                try
                {
                    int r = serial.Read(buf, got, count - got);
                    if (r > 0) { got += r; continue; }
                }
                catch (TimeoutException) { }
                Thread.Sleep(5);
            }
            if (got == count) return buf;
            if (got == 0)     return null;
            var partial = new byte[got];
            Array.Copy(buf, 0, partial, 0, got);
            return partial;
        }

        // -----------------------------------------------------------------------
        // Sync
        // -----------------------------------------------------------------------
        bool Sync()
        {
            // PhoenixMC Synchron() retries up to 5 times before giving up.
            // Keep the same retry count here.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                DrainInput();
                serial.Write(new byte[] { 0x55 }, 0, 1);
                serial.BaseStream.Flush();

                var reply = new byte[2];
                int got = 0;
                var sw  = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 1500 && got < 2)
                {
                    try { got += serial.Read(reply, got, 2 - got); }
                    catch (TimeoutException) { }
                }

                if (got == 2 && reply[0] == 'O' && reply[1] == 'K')
                {
                    addLogLine("Sync OK!");
                    DrainInput();
                    return true;
                }
            }

            addErrorLine("Sync failed (0 byte(s) received).");
            DrainInput();
            return false;
        }

        // -----------------------------------------------------------------------
        // Checksum – TWO separate algorithms
        //
        // (A) BROM HEADER checksum  – RFC 1071 Internet Checksum (carry-folded)
        //     Used in the 12-byte packet header and verified by CheckAck.
        //
        // (B) DATA checksum         – plain 16-bit word accumulation + invert
        //     Used only in the Write command payload.
        //     Confirmed from WriteSector SIMD (paddw) loop in the ELF.
        // -----------------------------------------------------------------------

        // BROM packet header checksum — and write data checksum (same algorithm for both).
        //
        // Confirmed from PhoenixMC ELF (GetFlashId, ReadSector, WriteSector, EraseFlash,
        // ChangeBaud): plain 16-bit word accumulation with natural uint16 overflow truncation.
        // NO carry fold. The assembly is simply:
        //   ADD AX, WORD PTR [buf+n]  (repeated over preimage with LE payload, LE length)
        //   NOT AX
        //   ROL AX, 8 / MOV WORD PTR [buf+6], AX   ← stored LE, giving big-endian wire bytes
        //
        // RFC1071 (carry-folded) produces the same result only when the sum stays <= 0xFFFF.
        // It diverges by 1 for sector indices >= ~96 and write commands with large checksum
        // fields — causing the device to reject the packet.
        static ushort ComputeChecksum(byte[] data, int offset, int count)
        {
            ushort sum = 0;
            int i = 0;
            for (; i + 1 < count; i += 2)
                sum += (ushort)(data[offset + i] | (data[offset + i + 1] << 8));
            if ((count & 1) != 0)
                sum += data[offset + count - 1];
            return (ushort)(~sum);
        }

        // BuildPhoenixPacket uses ComputeChecksum (see above) for the header.
        // WriteSectors also uses ComputeChecksum for the data payload checksum.

        byte[] BuildPhoenixPacket(byte opcode, byte[] payloadPre, byte[] payloadWire)
        {
            if (payloadPre == null)  payloadPre  = new byte[0];
            if (payloadWire == null) payloadWire = new byte[0];
            if (payloadPre.Length != payloadWire.Length)
                throw new InvalidOperationException("XR806 packet preimage/wire payload length mismatch.");

            int logicalLength = 1 + payloadPre.Length;

            byte[] pre = new byte[12 + logicalLength];
            pre[0] = (byte)'B'; pre[1] = (byte)'R'; pre[2] = (byte)'O'; pre[3] = (byte)'M';
            pre[4] = BROM_HOST_FLAGS; pre[5] = 0x00;
            pre[8] = (byte)( logicalLength        & 0xFF);
            pre[9] = (byte)((logicalLength >>  8) & 0xFF);
            pre[10]= (byte)((logicalLength >> 16) & 0xFF);
            pre[11]= (byte)((logicalLength >> 24) & 0xFF);
            pre[12]= opcode;
            if (payloadPre.Length > 0)
                Array.Copy(payloadPre, 0, pre, 13, payloadPre.Length);

            ushort cs = ComputeChecksum(pre, 0, pre.Length);

            byte[] pkt = new byte[12 + logicalLength];
            pkt[0] = (byte)'B'; pkt[1] = (byte)'R'; pkt[2] = (byte)'O'; pkt[3] = (byte)'M';
            pkt[4] = BROM_HOST_FLAGS; pkt[5] = 0x00;
            pkt[6] = (byte)((cs >> 8) & 0xFF);
            pkt[7] = (byte)( cs       & 0xFF);
            pkt[8] = (byte)((logicalLength >> 24) & 0xFF);
            pkt[9] = (byte)((logicalLength >> 16) & 0xFF);
            pkt[10]= (byte)((logicalLength >>  8) & 0xFF);
            pkt[11]= (byte)( logicalLength        & 0xFF);
            pkt[12]= opcode;
            if (payloadWire.Length > 0)
                Array.Copy(payloadWire, 0, pkt, 13, payloadWire.Length);
            return pkt;
        }

        byte[] BuildErasePacket(int address)
        {
            byte[] payloadPre  = new byte[5];
            byte[] payloadWire = new byte[5];

            payloadPre[0] = 0x03;
            payloadPre[1] = (byte)( address        & 0xFF);
            payloadPre[2] = (byte)((address >>  8) & 0xFF);
            payloadPre[3] = (byte)((address >> 16) & 0xFF);
            payloadPre[4] = (byte)((address >> 24) & 0xFF);

            payloadWire[0] = 0x03;
            payloadWire[1] = (byte)((address >> 24) & 0xFF);
            payloadWire[2] = (byte)((address >> 16) & 0xFF);
            payloadWire[3] = (byte)((address >>  8) & 0xFF);
            payloadWire[4] = (byte)( address        & 0xFF);

            return BuildPhoenixPacket(OP_ERASE, payloadPre, payloadWire);
        }

        byte[] BuildReadPacket(int sectorIndex, int sectorCount)
        {
            byte[] payloadPre  = new byte[8];
            byte[] payloadWire = new byte[8];

            payloadPre[0] = (byte)( sectorIndex        & 0xFF);
            payloadPre[1] = (byte)((sectorIndex >>  8) & 0xFF);
            payloadPre[2] = (byte)((sectorIndex >> 16) & 0xFF);
            payloadPre[3] = (byte)((sectorIndex >> 24) & 0xFF);
            payloadPre[4] = (byte)( sectorCount        & 0xFF);
            payloadPre[5] = (byte)((sectorCount >>  8) & 0xFF);
            payloadPre[6] = (byte)((sectorCount >> 16) & 0xFF);
            payloadPre[7] = (byte)((sectorCount >> 24) & 0xFF);

            payloadWire[0] = (byte)((sectorIndex >> 24) & 0xFF);
            payloadWire[1] = (byte)((sectorIndex >> 16) & 0xFF);
            payloadWire[2] = (byte)((sectorIndex >>  8) & 0xFF);
            payloadWire[3] = (byte)( sectorIndex        & 0xFF);
            payloadWire[4] = (byte)((sectorCount >> 24) & 0xFF);
            payloadWire[5] = (byte)((sectorCount >> 16) & 0xFF);
            payloadWire[6] = (byte)((sectorCount >>  8) & 0xFF);
            payloadWire[7] = (byte)( sectorCount        & 0xFF);

            return BuildPhoenixPacket(OP_READ, payloadPre, payloadWire);
        }

        byte[] BuildWritePacket(int sectorIndex, int sectorCount, ushort dataChecksum)
        {
            byte[] payloadPre  = new byte[10];
            byte[] payloadWire = new byte[10];

            payloadPre[0] = (byte)( sectorIndex        & 0xFF);
            payloadPre[1] = (byte)((sectorIndex >>  8) & 0xFF);
            payloadPre[2] = (byte)((sectorIndex >> 16) & 0xFF);
            payloadPre[3] = (byte)((sectorIndex >> 24) & 0xFF);
            payloadPre[4] = (byte)( sectorCount        & 0xFF);
            payloadPre[5] = (byte)((sectorCount >>  8) & 0xFF);
            payloadPre[6] = (byte)((sectorCount >> 16) & 0xFF);
            payloadPre[7] = (byte)((sectorCount >> 24) & 0xFF);
            payloadPre[8] = (byte)( dataChecksum        & 0xFF);
            payloadPre[9] = (byte)((dataChecksum >>  8) & 0xFF);

            payloadWire[0] = (byte)((sectorIndex >> 24) & 0xFF);
            payloadWire[1] = (byte)((sectorIndex >> 16) & 0xFF);
            payloadWire[2] = (byte)((sectorIndex >>  8) & 0xFF);
            payloadWire[3] = (byte)( sectorIndex        & 0xFF);
            payloadWire[4] = (byte)((sectorCount >> 24) & 0xFF);
            payloadWire[5] = (byte)((sectorCount >> 16) & 0xFF);
            payloadWire[6] = (byte)((sectorCount >>  8) & 0xFF);
            payloadWire[7] = (byte)( sectorCount        & 0xFF);
            payloadWire[8] = (byte)((dataChecksum >>  8) & 0xFF);
            payloadWire[9] = (byte)( dataChecksum        & 0xFF);

            return BuildPhoenixPacket(OP_WRITE, payloadPre, payloadWire);
        }

        byte[] BuildChangeBaudPacket(int newBaud)
        {
            int bromBaudValue = newBaud | unchecked((int)0x03000000);

            byte[] payloadPre  = new byte[4];
            byte[] payloadWire = new byte[4];

            payloadPre[0] = (byte)( bromBaudValue        & 0xFF);
            payloadPre[1] = (byte)((bromBaudValue >>  8) & 0xFF);
            payloadPre[2] = (byte)((bromBaudValue >> 16) & 0xFF);
            payloadPre[3] = (byte)((bromBaudValue >> 24) & 0xFF);

            payloadWire[0] = (byte)((bromBaudValue >> 24) & 0xFF);
            payloadWire[1] = (byte)((bromBaudValue >> 16) & 0xFF);
            payloadWire[2] = (byte)((bromBaudValue >>  8) & 0xFF);
            payloadWire[3] = (byte)( bromBaudValue        & 0xFF);

            return BuildPhoenixPacket(OP_CHANGE_BAUD, payloadPre, payloadWire);
        }

        bool ChangeBaudAndResync(int newBaud)
        {
            if (newBaud <= XR_ROM_BAUD)
                return true;

            byte[] pkt = BuildChangeBaudPacket(newBaud);
            BROMResponse resp = ExecuteRawPacket(pkt, 2000, 1000, false);

            if (resp.IsError)
            {
                addErrorLine($"ChangeBaud returned error flag for {newBaud} baud.");
                return false;
            }

            try
            {
                DrainInput();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                serial.BaudRate = newBaud;
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                addErrorLine("Host UART baud switch failed: " + ex.Message);
                return false;
            }

            if (!Sync())
            {
                addErrorLine($"Re-sync failed after switching to {newBaud} baud.");
                return false;
            }

            addLogLine($"Switched XR806 transport baud to {newBaud}.");
            return true;
        }

        BROMResponse ReadBromResponse(int headerTimeoutMs, int payloadTimeoutMs)
        {
            byte[] header = ReadExact(12, headerTimeoutMs);
            if (header == null || header.Length != 12)
                throw new IOException("BROM response header missing or incomplete.");

            if (header[0] != 'B' || header[1] != 'R' || header[2] != 'O' || header[3] != 'M')
                throw new IOException(
                    $"BROM magic mismatch: {header[0]:X2} {header[1]:X2} {header[2]:X2} {header[3]:X2}");

            var resp = new BROMResponse
            {
                Flags         = header[4],
                BromVersion   = header[5],
                Checksum      = (ushort)((header[6] << 8) | header[7]),
                PayloadLength = (header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11],
                Payload       = new byte[0],
            };

            if (resp.PayloadLength > 0)
            {
                byte[] payload = ReadExact(resp.PayloadLength, payloadTimeoutMs);
                if (payload == null || payload.Length != resp.PayloadLength)
                    throw new IOException(
                        $"BROM payload incomplete: expected {resp.PayloadLength}, got {payload?.Length ?? 0}.");
                resp.Payload = payload;
            }

            return resp;
        }

        BROMResponse ExecuteRawPacket(byte[] pkt, int headerTimeoutMs = 1500, int payloadTimeoutMs = 3000, bool isReadCommand = false)
        {
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

            if (isReadCommand)
                Thread.Sleep(50);

            return ReadBromResponse(headerTimeoutMs, payloadTimeoutMs);
        }

        BROMResponse ExecuteGetFlashId()
        {
            return ExecuteRawPacket(GET_FLASH_ID_REQUEST, 1500, 1500, false);
        }
        // -----------------------------------------------------------------------
        // GetFlashId / connect
        // -----------------------------------------------------------------------
        bool ReadFlashId()
        {
            BROMResponse resp = ExecuteGetFlashId();
            bromVersion = resp.BromVersion;

            if (resp.IsError)
            {
                addErrorLine("GetFlashId returned error flag.");
                return false;
            }
            if (resp.PayloadLength < 3)
            {
                addErrorLine($"GetFlashId payload too short ({resp.PayloadLength} bytes).");
                return false;
            }

            flashID = resp.Payload;
            addLogLine($"BROM version : {bromVersion}");
            addLogLine($"Flash ID     : {flashID[0]:X2} {flashID[1]:X2} {flashID[2]:X2}");

            // JEDEC size byte: e.g. 0x15 → 1 << (0x15 - 0x11) = 16 Mbit = 2 MB
            if (flashID.Length >= 3 && flashID[2] >= 0x11 && flashID[2] <= 0x20)
            {
                flashSizeBytes = (1 << (flashID[2] - 0x11)) * 0x20000;
                addLogLine($"Flash size   : {flashSizeBytes / 0x100000} MB");
            }
            else
            {
                addWarningLine("Could not decode flash size from JEDEC ID; defaulting to 2 MB.");
                flashSizeBytes = 2 * 0x100000;
            }
            return true;
        }

        // Validate the requested working baud against PhoenixMC's name_arr list and the
        // XR806 BROM v3 hard cap. Returns the clamped/validated baud, or -1 on failure.
        int ValidateWorkingBaud(int requestedBaud)
        {
            // Clamp to BROM v3 maximum (OpenUart does this silently; we do it explicitly)
            if (requestedBaud > XR_MAX_WORK_BAUD)
            {
                addWarningLine($"Requested baud {requestedBaud} exceeds XR806 BROM v3 maximum " +
                               $"({XR_MAX_WORK_BAUD}). Clamping to {XR_MAX_WORK_BAUD}.");
                requestedBaud = XR_MAX_WORK_BAUD;
            }

            // Must be one of PhoenixMC's supported bauds (name_arr) and > ROM baud
            bool found = false;
            foreach (int b in SUPPORTED_BAUDS)
                if (b == requestedBaud) { found = true; break; }

            if (!found || requestedBaud <= XR_ROM_BAUD)
            {
                addErrorLine($"Baud {requestedBaud} is not supported by the XR806 BROM.");
                addErrorLine("Supported working bauds: " +
                             string.Join(", ", System.Array.FindAll(SUPPORTED_BAUDS, b => b > XR_ROM_BAUD)));
                return -1;
            }
            return requestedBaud;
        }

        bool EnsureConnectedAndIdentified()
        {
            // Determine working baud from UI setting (this.baudrate), validated against
            // PhoenixMC's supported list and the BROM v3 cap.
            int workBaud = ValidateWorkingBaud(this.baudrate);
            if (workBaud < 0) return false;

            if (!Sync())         return false;
            if (!ReadFlashId())  return false;
            if (!ChangeBaudAndResync(workBaud)) return false;

            if (bromVersion != 0x03)
                addWarningLine($"Warning: unexpected BROM version 0x{bromVersion:X2} for XR806 " +
                               "(expected 0x03). Proceeding anyway.");
            return true;
        }

        // -----------------------------------------------------------------------
        // Erase  (opcode 0x19)
        //
        // BUG FIX: payload must be [ 0x03, addr_be[3], addr_be[2], addr_be[1], addr_be[0] ]
        // Confirmed from EraseFlash in the unstripped ELF:
        //   mov BYTE PTR [rbx+0xd], 0x3      ← first payload byte = 0x03
        //   bswap ebp                         ← address big-endian
        //   mov DWORD PTR [rbx+0xe], ebp      ← address follows 0x03
        // -----------------------------------------------------------------------
        bool Erase64KBlock(int address)
        {
            byte[] pkt = BuildErasePacket(address);
            BROMResponse resp = ExecuteRawPacket(pkt, 8000, 1000, false);

            if (resp.IsError)
            {
                addErrorLine($"Erase returned error flag at 0x{address:X6}.");
                return false;
            }
            return true;
        }

        // -----------------------------------------------------------------------
        // Read  (opcode 0x1A)
        //
        // BUG FIX: BootROM only handles ONE sector per request.
        // PhoenixMC ReadFlashLength loops one ReadSector call at a time.
        // Requesting 32 sectors caused the device to fail silently.
        // -----------------------------------------------------------------------

        // Read one or more 512-byte sectors. PhoenixMC uses 1 sector on the
        // initial 115200 ROM path and 4 sectors after ChangeBaud to 921600.
        byte[] ReadSectors(int sectorIndex, int sectorCount)
        {
            byte[] pkt = BuildReadPacket(sectorIndex, sectorCount);

            int expectedBytes = sectorCount * XR_SECTOR_SIZE;
            int payloadTimeout = (serial.BaudRate > XR_ROM_BAUD) ? 2000 : 4000;
            BROMResponse resp = ExecuteRawPacket(pkt, 4000, payloadTimeout, true);

            if (resp.IsError)
            {
                addErrorLine($"Read returned error flag for sector {sectorIndex}.");
                return null;
            }
            if (resp.Payload == null || resp.Payload.Length < expectedBytes)
            {
                addErrorLine($"Read sector {sectorIndex}: short payload ({resp.Payload?.Length ?? 0} bytes, expected {expectedBytes}).");
                return null;
            }
            return resp.Payload;
        }

        // -----------------------------------------------------------------------
        // Write  (opcode 0x1B)
        //
        // BUG FIX: data checksum must use plain 16-bit sum + invert,
        // NOT the RFC-1071 carry-folding used for the BROM header checksum.
        // Confirmed from WriteSector SIMD (paddw) loop in the unstripped ELF.
        //
        // Two-stage transaction:
        //   1. Send write command (with data checksum embedded in payload)
        //   2. Receive command ACK
        //   3. Send raw data bytes
        //   4. Receive final ACK
        // -----------------------------------------------------------------------
        bool WriteSectors(int sectorIndex, byte[] data, int sectorCount)
        {
            int dataLen = sectorCount * XR_SECTOR_SIZE;

            // Compute data checksum with the CORRECT algorithm
            ushort dataChecksum = ComputeChecksum(data, 0, dataLen);

            byte[] pkt = BuildWritePacket(sectorIndex, sectorCount, dataChecksum);

            // Stage 1: send write command, get command ACK
            BROMResponse cmdAck = ExecuteRawPacket(pkt, 3000, 1000, false);
            if (cmdAck.IsError)
            {
                addErrorLine($"Write command ACK returned error for sector {sectorIndex}.");
                return false;
            }

            // Stage 2: send raw data
            serial.Write(data, 0, dataLen);
            serial.BaseStream.Flush();

            // Stage 3: get final write result ACK
            BROMResponse finalAck = ReadBromResponse(10000, 1000);
            if (finalAck.IsError)
            {
                addErrorLine($"Write final ACK returned error for sector {sectorIndex}.");
                return false;
            }
            return true;
        }

        // -----------------------------------------------------------------------
        // High-level read loop  (1 sector per request, per PhoenixMC)
        // -----------------------------------------------------------------------
        byte[] InternalRead(int startAddress, int length)
        {
            var result = new byte[length];
            int done = 0;
            int totalSectors = (length + XR_SECTOR_SIZE - 1) / XR_SECTOR_SIZE;
            int sectorIndex  = startAddress / XR_SECTOR_SIZE;

            logger.setProgress(0, totalSectors);
            logger.setState("Reading...", Color.Transparent);

            // Match PhoenixMC ReadFlashLength: 4 sectors per command at 921600 baud,
            // 1 sector at lower bauds. The previous 1-sector workaround was masking the
            // checksum bug; with the correct plain-16 checksum this is safe.
            int sectorsPerRead = (serial.BaudRate >= XR_MAX_WORK_BAUD) ? 4 : 1;
            for (int s = 0; s < totalSectors && !isCancelled; )
            {
                int thisCount = Math.Min(sectorsPerRead, totalSectors - s);
                byte[] chunk = ReadSectors(sectorIndex + s, thisCount);
                if (chunk == null)
                {
                    logger.setState("Read failed", Color.Red);
                    throw new IOException($"Read failed at sector {sectorIndex + s} (address 0x{(startAddress + s * XR_SECTOR_SIZE):X6}).");
                }

                int chunkBytes = Math.Min(thisCount * XR_SECTOR_SIZE, length - done);
                Array.Copy(chunk, 0, result, done, chunkBytes);
                done += chunkBytes;
                s += thisCount;
                logger.setProgress(s, totalSectors);
            }

            logger.setState("Read complete", Color.DarkGreen);
            return result;
        }

        // -----------------------------------------------------------------------
        // High-level erase loop
        // -----------------------------------------------------------------------
        bool InternalEraseRange(int startAddress, int length)
        {
            int eraseStart = startAddress &  ~(XR_ERASE_BLOCK_SIZE - 1);
            int eraseEnd   = (startAddress + length + XR_ERASE_BLOCK_SIZE - 1) & ~(XR_ERASE_BLOCK_SIZE - 1);
            int total      = (eraseEnd - eraseStart) / XR_ERASE_BLOCK_SIZE;
            int done       = 0;

            logger.setProgress(0, total);
            logger.setState("Erasing...", Color.Transparent);

            for (int address = eraseStart; address < eraseEnd; address += XR_ERASE_BLOCK_SIZE)
            {
                if (isCancelled) return false;
                addLogLine($"Erasing 64 KB block at 0x{address:X6}");
                if (!Erase64KBlock(address))
                {
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                done++;
                logger.setProgress(done, total);
            }

            logger.setState("Erase complete", Color.DarkGreen);
            return true;
        }

        // -----------------------------------------------------------------------
        // High-level write loop  (16 KB chunks = 32 sectors per write command)
        // -----------------------------------------------------------------------
        bool InternalWrite(int startAddress, byte[] data)
        {
            int totalChunks = (data.Length + XR_WRITE_CHUNK_SIZE - 1) / XR_WRITE_CHUNK_SIZE;
            logger.setProgress(0, totalChunks);
            logger.setState("Writing...", Color.Transparent);

            int offset    = 0;
            int chunksDone = 0;
            while (offset < data.Length && !isCancelled)
            {
                int chunkBytes = Math.Min(XR_WRITE_CHUNK_SIZE, data.Length - offset);

                // Pad chunk up to a whole sector boundary
                byte[] chunk = MiscUtils.subArray(data, offset, chunkBytes);
                chunk = MiscUtils.padArray(chunk, XR_SECTOR_SIZE);

                int sectorIndex = (startAddress + offset) / XR_SECTOR_SIZE;
                int sectorCount = chunk.Length / XR_SECTOR_SIZE;

                addLogLine($"Writing 0x{chunkBytes:X} bytes at 0x{startAddress + offset:X6} " +
                           $"(sector {sectorIndex}, {sectorCount} sectors)");

                if (!WriteSectors(sectorIndex, chunk, sectorCount))
                {
                    logger.setState("Write failed", Color.Red);
                    return false;
                }

                offset     += chunkBytes;
                chunksDone++;
                logger.setProgress(chunksDone, totalChunks);
            }

            logger.setState("Write complete", Color.DarkGreen);
            return !isCancelled;
        }

        // -----------------------------------------------------------------------
        // .img file parsing and validation
        // -----------------------------------------------------------------------
        static XRImageSectionHeader ParseSectionHeader(byte[] image, int offset)
        {
            return new XRImageSectionHeader
            {
                Magic          = BitConverter.ToUInt32(image, offset + 0x00),
                Version        = BitConverter.ToUInt32(image, offset + 0x04),
                HeaderChecksum = BitConverter.ToUInt16(image, offset + 0x08),
                DataChecksum   = BitConverter.ToUInt16(image, offset + 0x0A),
                DataSize       = BitConverter.ToUInt32(image, offset + 0x0C),
                LoadAddr       = BitConverter.ToUInt32(image, offset + 0x10),
                Entry          = BitConverter.ToUInt32(image, offset + 0x14),
                BodyLen        = BitConverter.ToUInt32(image, offset + 0x18),
                Attribute      = BitConverter.ToUInt32(image, offset + 0x1C),
                NextAddr       = BitConverter.ToUInt32(image, offset + 0x20),
                SectionId      = BitConverter.ToUInt32(image, offset + 0x24),
            };
        }

        // XR image header checksum: sum all 16-bit words in the 64-byte header,
        // result must equal 0xFFFF (i.e. inverted sum of all words = 0).
        static bool ValidateHeaderChecksum(byte[] image, int offset)
        {
            ushort sum = 0;
            for (int i = 0; i < 0x40; i += 2)
                sum += (ushort)(image[offset + i] | (image[offset + i + 1] << 8));
            return sum == 0xFFFF;
        }

        // XR image data checksum: plain 16-bit word sum of data + checksum word = 0xFFFF
        static bool ValidateDataChecksum(byte[] image, int dataOffset, int size, ushort storedChecksum)
        {
            ushort sum = storedChecksum;
            int i = 0;
            for (; i + 1 < size; i += 2)
                sum += (ushort)(image[dataOffset + i] | (image[dataOffset + i + 1] << 8));
            if ((size & 1) != 0)
                sum += image[dataOffset + size - 1];
            return sum == 0xFFFF;
        }

        // Parse the .img, validate all sections, log the layout, return effective length.
        int GetXRImageEffectiveLength(byte[] image, bool logLayout)
        {
            if (logLayout)
            {
                addLogLine("");
                addLogLine("┌─────────────────────────────────────────────────────────────────────┐");
                addLogLine("│                   XR806 Firmware Section Layout                     │");
                addLogLine("├────────┬───────────┬──────────┬──────────┬──────────┬───────────────┤");
                addLogLine("│  Idx   │  Section  │  Offset  │   Size   │ LoadAddr │  Entry Point  │");
                addLogLine("├────────┼───────────┼──────────┼──────────┼──────────┼───────────────┤");
            }

            int  offset    = 0;
            int  loopGuard = 0;
            int  lastEnd   = 0;

            while (true)
            {
                if (offset < 0 || offset + 0x40 > image.Length)
                    throw new InvalidDataException(
                        $"XR .img section header at 0x{offset:X} is out of range.");

                XRImageSectionHeader hdr = ParseSectionHeader(image, offset);

                if (hdr.Magic != 0x48495741)
                    throw new InvalidDataException(
                        $"Bad AWIH magic at offset 0x{offset:X}: 0x{hdr.Magic:X8}");

                if (!ValidateHeaderChecksum(image, offset))
                    throw new InvalidDataException(
                        $"Header checksum mismatch at 0x{offset:X}.");

                int dataOffset = offset + 0x40;
                int dataSize   = (int)hdr.DataSize;

                if (dataOffset + dataSize > image.Length)
                    throw new InvalidDataException(
                        $"Section data at 0x{offset:X} exceeds file size.");

                if (!ValidateDataChecksum(image, dataOffset, dataSize, hdr.DataChecksum))
                    throw new InvalidDataException(
                        $"Data checksum mismatch at 0x{offset:X}.");

                lastEnd = dataOffset + dataSize;

                if (logLayout)
                {
                    string name = SECTION_NAMES.TryGetValue(hdr.SectionId, out string n) ? n : $"0x{hdr.SectionId:X8}";
                    addLogLine(
                        $"│ {loopGuard,5}  │ {name,-9} │ 0x{offset:X6}  │ 0x{dataSize:X6} │ 0x{hdr.LoadAddr:X6} │ 0x{hdr.Entry:X10}  │");
                }

                if (++loopGuard > 100)
                    throw new InvalidDataException("Too many sections (> 100); likely corrupt image.");

                if (hdr.NextAddr == 0xFFFFFFFF)
                    break;

                offset = (int)hdr.NextAddr;
            }

            if (logLayout)
            {
                addLogLine("└────────┴───────────┴──────────┴──────────┴──────────┴───────────────┘");
                addLogLine($"  Total sections : {loopGuard}");
                addLogLine($"  Effective size : 0x{lastEnd:X6}  ({lastEnd} bytes)");
                addLogLine("");
            }

            return lastEnd;
        }

        // -----------------------------------------------------------------------
        // Save read result
        // -----------------------------------------------------------------------
        bool SaveReadResult(string fileName)
        {
            if (readResult == null)
            {
                addErrorLine("No read result to save.");
                return false;
            }
            byte[] dat = readResult.ToArray();
            Directory.CreateDirectory("backups");
            string fullPath = Path.Combine("backups", fileName);
            File.WriteAllBytes(fullPath, dat);
            addSuccess($"Saved {dat.Length} bytes to {fullPath}" + Environment.NewLine);
            logger.onReadResultQIOSaved(dat, "", fullPath);
            return true;
        }

        public override bool saveReadResult(int startOffset)
        {
            string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType, backupName, "bin");
            return SaveReadResult(fileName);
        }

        // -----------------------------------------------------------------------
        // Public operations  (override BaseFlasher)
        // -----------------------------------------------------------------------

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            try
            {
                if (!DoGenericSetup())              return;
                if (!EnsureConnectedAndIdentified()) return;

                int startAddress, length;
                if (fullRead)
                {
                    startAddress = 0;
                    length       = flashSizeBytes;
                    addLogLine($"Full read: {flashSizeBytes / 0x100000} MB starting at 0x000000");
                }
                else
                {
                    // startSector / sectors come from the UI in the same 4 KB units
                    // used by the rest of the app; translate to byte addresses.
                    startAddress = startSector * BK7231Flasher.SECTOR_SIZE;
                    length       = sectors     * BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Partial read: 0x{startAddress:X6} + 0x{length:X6} bytes");
                }

                byte[] data = InternalRead(startAddress, length);
                readResult  = new MemoryStream(data);
                saveReadResult(startAddress);
            }
            catch (Exception ex)
            {
                addErrorLine(ex.Message);
            }
        }

        public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
        {
            try
            {
                if (!DoGenericSetup())              return false;
                if (!EnsureConnectedAndIdentified()) return false;

                int startAddress, length;
                if (bAll)
                {
                    startAddress = 0;
                    length       = flashSizeBytes;
                    addLogLine($"Full erase: {flashSizeBytes / 0x100000} MB");
                }
                else
                {
                    startAddress = startSector * BK7231Flasher.SECTOR_SIZE;
                    length       = sectors     * BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Partial erase: 0x{startAddress:X6} + 0x{length:X6} bytes");
                }

                return InternalEraseRange(startAddress, length);
            }
            catch (Exception ex)
            {
                addErrorLine(ex.Message);
                return false;
            }
        }

        public override void doReadAndWrite(
            int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if (rwMode == WriteMode.OnlyOBKConfig)
            {
                addErrorLine("XR806 does not support standalone OBK config writes.");
                return;
            }

            try
            {
                if (!DoGenericSetup())              return;
                if (!EnsureConnectedAndIdentified()) return;

                // ── Backup read (ReadAndWrite mode) ─────────────────────────
                if (rwMode == WriteMode.ReadAndWrite)
                {
                    addLogLine($"Reading full flash ({flashSizeBytes / 0x100000} MB) before write...");
                    byte[] backup = InternalRead(0, flashSizeBytes);
                    readResult    = new MemoryStream(backup);
                    if (!saveReadResult(0)) return;
                }

                if (isCancelled) return;

                // ── Write ────────────────────────────────────────────────────
                if (rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
                {
                    if (string.IsNullOrEmpty(sourceFileName))
                    {
                        addErrorLine("No source file specified.");
                        return;
                    }
                    if (!File.Exists(sourceFileName))
                    {
                        addErrorLine($"Source file not found: {sourceFileName}");
                        return;
                    }

                    addLogLine($"Loading {sourceFileName} ...");
                    byte[] fileData      = File.ReadAllBytes(sourceFileName);
                    int    writeAddress  = startSector * BK7231Flasher.SECTOR_SIZE;
                    int    effectiveLen  = fileData.Length;
                    string ext           = Path.GetExtension(sourceFileName).ToLowerInvariant();

                    bool isImg = ext == ".img";
                    bool isBin = ext == ".bin";

                    // Both .img and .bin must begin with AWIH magic (0x48495741).
                    // PhoenixMC's FwParser::Parse checks this at byte 0 before anything else.
                    if (fileData.Length < 4 ||
                        BitConverter.ToUInt32(fileData, 0) != AWIH_MAGIC)
                    {
                        addErrorLine($"File does not start with AWIH magic (0x{AWIH_MAGIC:X8} / \"AWIH\").");
                        addErrorLine("Only XR806 firmware images with a valid AWIH section chain are supported.");
                        return;
                    }

                    if (isImg || isBin)
                    {
                        // Parse, validate checksums, and print the section layout table.
                        effectiveLen = GetXRImageEffectiveLength(fileData, logLayout: true);
                        writeAddress = 0;   // AWIH images always flash from offset 0
                        addLogLine($"Validated XR firmware ({ext}): flashing 0x{effectiveLen:X} bytes to flash offset 0x000000");
                    }
                    else
                    {
                        addWarningLine($"Unrecognised extension '{ext}'; writing raw AWIH image at offset 0x{writeAddress:X6}.");
                    }

                    if (effectiveLen > flashSizeBytes)
                    {
                        addErrorLine($"Image (0x{effectiveLen:X} bytes) exceeds detected flash size ({flashSizeBytes / 0x100000} MB)." );
                        return;
                    }

                    byte[] toWrite = new byte[effectiveLen];
                    Array.Copy(fileData, 0, toWrite, 0, effectiveLen);

                    addLogLine("Erasing target region...");
                    if (!InternalEraseRange(writeAddress, effectiveLen)) return;
                    if (isCancelled) return;

                    addLogLine("Writing firmware...");
                    if (!InternalWrite(writeAddress, toWrite)) return;

                    addSuccess("XR806 flash complete!" + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                addErrorLine(ex.Message);
            }
        }
    }
}
