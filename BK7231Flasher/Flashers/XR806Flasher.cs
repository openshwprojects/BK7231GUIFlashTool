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
        // =====================================================================
        // Constants  (all confirmed against PhoenixMC ELF)
        // =====================================================================

        const int  XR_ROM_BAUD         = 115200;    // BROM default after reset
        const int  XR_WORK_BAUD        = 921600;    // 0xe1000 – BROM v3 hard cap (0x40e9c7: cmp r14d,0xe1000; ja → clamp)
        const int  XR_SECTOR_SIZE      = 0x200;     // 512 bytes
        const int  XR_ERASE_BLOCK_SIZE = 0x10000;   // 64 KB granularity
        const int  XR_WRITE_CHUNK_SIZE = 0x4000;    // 16 KB (WriteFlashLength: 0x410005 mov edx,0x4000)
        const int  XR_WRITE_SECTORS    = XR_WRITE_CHUNK_SIZE / XR_SECTOR_SIZE;  // 32

        // BROM packet byte constants
        const byte BROM_HOST_FLAGS     = 0x04;      // always; flags[1]=0x00 (confirmed from every packet builder)
        const byte BROM_FLAG_ERROR     = 0x01;      // CheckAckEv (0x409e2c): test al,0x1

        // Opcodes  (confirmed from ELF function bodies)
        const byte OP_CHANGE_BAUD      = 0x10;
        const byte OP_GET_FLASH_ID     = 0x18;
        const byte OP_ERASE            = 0x19;
        const byte OP_READ             = 0x1A;
        const byte OP_WRITE            = 0x1B;

        // Known-good 13-byte GetFlashId request (checksum pre-verified against ELF)
        static readonly byte[] GET_FLASH_ID_REQUEST =
            { 0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51, 0x00, 0x00, 0x00, 0x01, 0x18 };

        // .img section ID → human name table  (from FwParser::Parse string table)
        static readonly Dictionary<uint, string> SECTION_NAMES = new Dictionary<uint, string>
        {
            { 0xA5FF5A00, "boot"     },
            { 0xA5FE5A01, "app"      },
            { 0xA5FD5A02, "app_xip"  },
            { 0xA5FA5A05, "wlan_bl"  },
            { 0xA5F95A06, "wlan_fw"  },
            { 0xA5F85A07, "wlan_sdd" },
        };

        // =====================================================================
        // Fields
        // =====================================================================
        MemoryStream readResult;
        byte[]       flashID;
        int          flashSizeBytes = 2 * 0x100000;
        byte         bromVersion    = 0xFF;

        // =====================================================================
        // Response / header structures
        // =====================================================================
        struct BROMResponse
        {
            public byte   Flags;
            public byte   BromVersion;
            public ushort Checksum;         // parsed but intentionally not validated –
                                            // PhoenixMC CheckAckEv never validates it either
            public int    PayloadLength;
            public byte[] Payload;
            public bool   IsError => (Flags & BROM_FLAG_ERROR) != 0;
        }

        struct XRSectionHeader
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
        }

        // =====================================================================
        // Constructor
        // =====================================================================
        public XR806Flasher(CancellationToken ct) : base(ct)
        {
        }

        // =====================================================================
        // Checksum
        //
        // ONE algorithm used for everything – plain 16-bit word accumulation, natural
        // uint16 overflow, then bitwise NOT.  Confirmed from:
        //   CFlashHost::CheckSum16 (0x4086d0) – used for all BROM packet headers
        //   FwParser::CheckSum16   (0x411ba0) – used for .img section header + data
        // Both compile to identical code (same SIMD paddw loop, same scalar tail).
        //
        // This is NOT RFC 1071.  RFC 1071 folds the 32-bit carry back into 16 bits;
        // this code does not.  The two algorithms diverge whenever the running sum
        // exceeds 0xFFFF, which happens at sector ~96 on a 2 MB flash read.
        //
        // Preimage rules for BROM packet headers (from OpenUartEv / WriteSectorEjPhj):
        //   – logical-length field is little-endian in the preimage
        //   – multi-byte payload fields are little-endian in the preimage
        //   – checksum field is zeroed in the preimage
        //   – odd trailing byte is added as-is (matches the scalar tail at 0x4088d4)
        // The wire packet re-encodes: checksum big-endian (ROL ax,8 / store),
        // logical-length big-endian, payload fields big-endian where noted.
        // =====================================================================
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

        // =====================================================================
        // Packet building
        //
        // payloadPre  – payload bytes in checksum-preimage order (little-endian)
        // payloadWire – same bytes in wire order (big-endian where applicable)
        // =====================================================================
        byte[] BuildPhoenixPacket(byte opcode, byte[] payloadPre, byte[] payloadWire)
        {
            if (payloadPre  == null) payloadPre  = new byte[0];
            if (payloadWire == null) payloadWire = new byte[0];

            int logicalLen = 1 + payloadPre.Length; // 1 for opcode

            // ── preimage: LE length, zeroed checksum, LE payload ──────────────
            byte[] pre = new byte[12 + logicalLen];
            pre[0] = (byte)'B'; pre[1] = (byte)'R'; pre[2] = (byte)'O'; pre[3] = (byte)'M';
            pre[4] = BROM_HOST_FLAGS;
            // [5] = 0x00  (flags high byte – always 0)
            // [6..7] = 0x0000  (checksum placeholder)
            pre[8]  = (byte)( logicalLen        & 0xFF);
            pre[9]  = (byte)((logicalLen >>  8) & 0xFF);
            pre[10] = (byte)((logicalLen >> 16) & 0xFF);
            pre[11] = (byte)((logicalLen >> 24) & 0xFF);
            pre[12] = opcode;
            if (payloadPre.Length > 0)
                Buffer.BlockCopy(payloadPre, 0, pre, 13, payloadPre.Length);

            ushort cs = ComputeChecksum(pre, 0, pre.Length);

            // ── wire packet: BE checksum, BE length, BE payload ───────────────
            byte[] pkt = new byte[12 + logicalLen];
            pkt[0] = (byte)'B'; pkt[1] = (byte)'R'; pkt[2] = (byte)'O'; pkt[3] = (byte)'M';
            pkt[4] = BROM_HOST_FLAGS;
            pkt[6]  = (byte)((cs >> 8) & 0xFF);          // ROL ax,8 then store (from ELF)
            pkt[7]  = (byte)( cs       & 0xFF);
            pkt[8]  = (byte)((logicalLen >> 24) & 0xFF);
            pkt[9]  = (byte)((logicalLen >> 16) & 0xFF);
            pkt[10] = (byte)((logicalLen >>  8) & 0xFF);
            pkt[11] = (byte)( logicalLen        & 0xFF);
            pkt[12] = opcode;
            if (payloadWire.Length > 0)
                Buffer.BlockCopy(payloadWire, 0, pkt, 13, payloadWire.Length);
            return pkt;
        }

        // EraseFlashEj (0x40ced0):
        //   mov [rbx+0xd], 0x3     ← payload[0] = 0x03
        //   bswap ebp              ← address big-endian
        //   mov [rbx+0xe], ebp     ← payload[1..4] = address BE
        byte[] BuildErasePacket(int address)
        {
            var pre  = new byte[5];
            var wire = new byte[5];
            pre[0] = wire[0] = 0x03;
            pre[1]  = (byte)( address        & 0xFF);
            pre[2]  = (byte)((address >>  8) & 0xFF);
            pre[3]  = (byte)((address >> 16) & 0xFF);
            pre[4]  = (byte)((address >> 24) & 0xFF);
            wire[1] = (byte)((address >> 24) & 0xFF);
            wire[2] = (byte)((address >> 16) & 0xFF);
            wire[3] = (byte)((address >>  8) & 0xFF);
            wire[4] = (byte)( address        & 0xFF);
            return BuildPhoenixPacket(OP_ERASE, pre, wire);
        }

        // ReadSectorEjPhj (0x40c620): 21-byte wire packet, 8-byte payload.
        byte[] BuildReadPacket(int sectorIndex, int sectorCount)
        {
            var pre  = new byte[8];
            var wire = new byte[8];
            pre[0] = (byte)( sectorIndex        & 0xFF);
            pre[1] = (byte)((sectorIndex >>  8) & 0xFF);
            pre[2] = (byte)((sectorIndex >> 16) & 0xFF);
            pre[3] = (byte)((sectorIndex >> 24) & 0xFF);
            pre[4] = (byte)( sectorCount        & 0xFF);
            pre[5] = (byte)((sectorCount >>  8) & 0xFF);
            pre[6] = (byte)((sectorCount >> 16) & 0xFF);
            pre[7] = (byte)((sectorCount >> 24) & 0xFF);
            wire[0] = (byte)((sectorIndex >> 24) & 0xFF);
            wire[1] = (byte)((sectorIndex >> 16) & 0xFF);
            wire[2] = (byte)((sectorIndex >>  8) & 0xFF);
            wire[3] = (byte)( sectorIndex        & 0xFF);
            wire[4] = (byte)((sectorCount >> 24) & 0xFF);
            wire[5] = (byte)((sectorCount >> 16) & 0xFF);
            wire[6] = (byte)((sectorCount >>  8) & 0xFF);
            wire[7] = (byte)( sectorCount        & 0xFF);
            return BuildPhoenixPacket(OP_READ, pre, wire);
        }

        // WriteSectorEjPhj (0x40f1e0): 23-byte wire packet, 10-byte payload.
        byte[] BuildWritePacket(int sectorIndex, int sectorCount, ushort dataChecksum)
        {
            var pre  = new byte[10];
            var wire = new byte[10];
            pre[0] = (byte)( sectorIndex        & 0xFF);
            pre[1] = (byte)((sectorIndex >>  8) & 0xFF);
            pre[2] = (byte)((sectorIndex >> 16) & 0xFF);
            pre[3] = (byte)((sectorIndex >> 24) & 0xFF);
            pre[4] = (byte)( sectorCount        & 0xFF);
            pre[5] = (byte)((sectorCount >>  8) & 0xFF);
            pre[6] = (byte)((sectorCount >> 16) & 0xFF);
            pre[7] = (byte)((sectorCount >> 24) & 0xFF);
            pre[8] = (byte)( dataChecksum        & 0xFF);
            pre[9] = (byte)((dataChecksum >>  8) & 0xFF);
            wire[0] = (byte)((sectorIndex >> 24) & 0xFF);
            wire[1] = (byte)((sectorIndex >> 16) & 0xFF);
            wire[2] = (byte)((sectorIndex >>  8) & 0xFF);
            wire[3] = (byte)( sectorIndex        & 0xFF);
            wire[4] = (byte)((sectorCount >> 24) & 0xFF);
            wire[5] = (byte)((sectorCount >> 16) & 0xFF);
            wire[6] = (byte)((sectorCount >>  8) & 0xFF);
            wire[7] = (byte)( sectorCount        & 0xFF);
            wire[8] = (byte)((dataChecksum >>  8) & 0xFF);
            wire[9] = (byte)( dataChecksum        & 0xFF);
            return BuildPhoenixPacket(OP_WRITE, pre, wire);
        }

        // OpenUartEv (0x40e7a6): esi = baud | 0x03000000 before calling ChangeBaudEj
        byte[] BuildChangeBaudPacket(int newBaud)
        {
            int val = newBaud | unchecked((int)0x03000000);
            var pre  = new byte[4];
            var wire = new byte[4];
            pre[0]  = (byte)( val        & 0xFF);
            pre[1]  = (byte)((val >>  8) & 0xFF);
            pre[2]  = (byte)((val >> 16) & 0xFF);
            pre[3]  = (byte)((val >> 24) & 0xFF);
            wire[0] = (byte)((val >> 24) & 0xFF);
            wire[1] = (byte)((val >> 16) & 0xFF);
            wire[2] = (byte)((val >>  8) & 0xFF);
            wire[3] = (byte)( val        & 0xFF);
            return BuildPhoenixPacket(OP_CHANGE_BAUD, pre, wire);
        }

        // =====================================================================
        // Port setup / teardown
        // =====================================================================
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
                serial.ReadTimeout     = 250;
                serial.WriteTimeout    = 5000;
                serial.ReadBufferSize  = 1024 * 1024;
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

        // Matches PhoenixMC CloseEv (0x40ace0):
        //   ChangeBaud(115200|0x03000000)  → ChangeUartBaud(115200) → Synchron → UART_Close
        // Leaves the device at 115200 so it doesn't need a power cycle to reconnect.
        // Best-effort: failures are silently swallowed before calling base.closePort().
        public override void closePort()
        {
            if (serial != null && serial.IsOpen && serial.BaudRate != XR_ROM_BAUD)
            {
                try
                {
                    byte[] pkt = BuildChangeBaudPacket(XR_ROM_BAUD);
                    serial.Write(pkt, 0, pkt.Length);
                    serial.BaseStream.Flush();
                    Thread.Sleep(60);
                    serial.BaudRate = XR_ROM_BAUD;
                    Thread.Sleep(40);
                    // We skip re-Sync here; device will be reset before next session anyway.
                }
                catch { /* best-effort */ }
            }
            base.closePort();
        }

        // =====================================================================
        // Serial helpers
        // =====================================================================
        void DrainInput(int quietMs = 150, int hardLimitMs = 750)
        {
            var total = Stopwatch.StartNew();
            var quiet = Stopwatch.StartNew();
            while (total.ElapsedMilliseconds < hardLimitMs)
            {
                int waiting = serial.BytesToRead;
                if (waiting > 0)
                {
                    var buf = new byte[waiting];
                    serial.Read(buf, 0, buf.Length);
                    quiet.Restart();
                    continue;
                }
                if (quiet.ElapsedMilliseconds >= quietMs) break;
                Thread.Sleep(10);
            }
        }

        byte[] ReadExact(int count, int timeoutMs)
        {
            var buf = new byte[count];
            int got = 0;
            var sw  = Stopwatch.StartNew();
            while (got < count && sw.ElapsedMilliseconds < timeoutMs && !isCancelled)
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
            Buffer.BlockCopy(buf, 0, partial, 0, got);
            return partial;
        }

        // =====================================================================
        // Sync  (PhoenixMC Synchron at 0x409320)
        //
        // ebp = 5  → 5 attempts (0x40932a)
        // Each attempt: write 0x55 (1 byte), read 2 bytes, compare with 'OK' (0x4b4f)
        // =====================================================================
        bool Sync()
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                if (isCancelled) return false;
                DrainInput();
                serial.Write(new byte[] { 0x55 }, 0, 1);
                serial.BaseStream.Flush();

                var reply = new byte[2];
                int got   = 0;
                var sw    = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 1500 && got < 2 && !isCancelled)
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
                if (attempt < 5)
                    addLogLine($"Sync attempt {attempt}/5 failed, retrying...");
            }
            addErrorLine("Sync failed after 5 attempts.");
            return false;
        }

        // =====================================================================
        // Low-level packet exchange
        //
        // ReadBromResponse always reads exactly 12 header bytes then exactly
        // PayloadLength bytes.  Throws IOException on any framing failure so
        // callers can catch cleanly without partial-state issues.
        // =====================================================================
        BROMResponse ReadBromResponse(int headerTimeoutMs, int payloadTimeoutMs)
        {
            byte[] header = ReadExact(12, headerTimeoutMs);
            if (header == null || header.Length < 12)
                throw new IOException(
                    $"BROM header incomplete ({header?.Length ?? 0}/12 bytes).");
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

            // CheckAckEv (0x409e2c) checks bit 1 (has-payload) then reads payload.
            // It does NOT verify the checksum field.  We do the same.
            if (resp.PayloadLength > 0)
            {
                byte[] payload = ReadExact(resp.PayloadLength, payloadTimeoutMs);
                if (payload == null || payload.Length < resp.PayloadLength)
                    throw new IOException(
                        $"BROM payload incomplete ({payload?.Length ?? 0}/{resp.PayloadLength} bytes).");
                resp.Payload = payload;
            }
            return resp;
        }

        BROMResponse ExecuteRawPacket(
            byte[] pkt,
            int    headerTimeoutMs  = 1500,
            int    payloadTimeoutMs = 3000,
            bool   sleepBeforeRead  = false)
        {
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

            // ReadSectorEjPhj (0x40c72e): usleep(0xC350) = 50 ms unconditionally
            // between WriteMsgEPhj and ReadMsgEPhj on read commands.
            if (sleepBeforeRead)
                Thread.Sleep(50);

            return ReadBromResponse(headerTimeoutMs, payloadTimeoutMs);
        }

        // =====================================================================
        // GetFlashId  (opcode 0x18)
        //
        // ELF GetFlashIdEv (0x40aee0):
        //   Sends 13-byte hardcoded packet; receives 12-byte header.
        //   On success, CheckAckEv reads payload into [rbx+0x6084].
        //   [rbx+0x6080] stores logicalLength (usually 3 for 3-byte JEDEC ID).
        //   [rbx+0x402b] = BromVersion (flags[1] of response header).
        // =====================================================================
        bool ReadFlashId()
        {
            BROMResponse resp = ExecuteRawPacket(
                GET_FLASH_ID_REQUEST, headerTimeoutMs: 1500, payloadTimeoutMs: 1500);
            bromVersion = resp.BromVersion;

            if (resp.IsError)  { addErrorLine("GetFlashId returned error."); return false; }
            if (resp.PayloadLength < 3)
            {
                addErrorLine($"GetFlashId: payload too short ({resp.PayloadLength} bytes).");
                return false;
            }

            flashID = resp.Payload;
            addLogLine($"BROM version : 0x{bromVersion:X2}");
            addLogLine($"Flash ID     : {flashID[0]:X2} {flashID[1]:X2} {flashID[2]:X2}");

            if (flashID[2] >= 0x11 && flashID[2] <= 0x20)
            {
                flashSizeBytes = (1 << (flashID[2] - 0x11)) * 0x20000;
                addLogLine($"Flash size   : {flashSizeBytes / 0x100000} MB  " +
                           $"(JEDEC capacity byte 0x{flashID[2]:X2})");
            }
            else
            {
                addWarningLine("JEDEC capacity byte out of expected range – defaulting to 2 MB.");
            }
            return true;
        }

        // =====================================================================
        // Connection sequence  (mirrors OpenUartEv at 0x40e6b0)
        //
        // Confirmed flow (BROM v3 / XR806 path):
        //   1. Uart_Init at 115200                              (0x40e713)
        //   2. Synchron                                         (0x40e762)
        //   3. GetFlashId                                       (0x40e77c)
        //   4. Check BromVersion at [rbx+0x6080]:
        //      if version <= 1 → WriteMemoryLen + SetPC (RAM loader), then re-Sync (0x40e793)
        //      if version >  1 → skip RAM loader, go straight to ChangeBaud  (0x40e7a0)
        //   5. ChangeBaud(baud | 0x03000000)  [capped at 0xe1000 before call] (0x40e7a6/0x40e9c7)
        //   6. ChangeUartBaud (host side)                       (0x40e7c5)
        //   7. Synchron again at new baud                       (0x40e7d5)
        //
        // What PhoenixMC does on BROM v1 that we do NOT implement:
        //   WriteMemoryLen(address=0x10000, data=<embedded blob>, len=0x16ec)
        //   SetPC(0x10101)
        //   Then switch baud + re-Sync + second pass through the same flow.
        //   This is the RAM-loader stub upload path for older mask ROM chips.
        //   XR806 is always BROM v3; this path is documented but not needed.
        // =====================================================================
        bool EnsureConnectedAndIdentified()
        {
            if (!Sync())        return false;
            if (!ReadFlashId()) return false;

            if (bromVersion <= 1)
            {
                addErrorLine(
                    $"BROM version 0x{bromVersion:X2} requires a RAM loader upload " +
                    "(used by older XR chips).  This flasher supports XR806 (BROM v3) only.");
                return false;
            }
            if (bromVersion != 0x03)
                addWarningLine($"Unexpected BROM version 0x{bromVersion:X2} – expected 0x03.  Proceeding.");

            return ChangeBaudAndResync(XR_WORK_BAUD);
        }

        // ChangeBaudEj (0x40a9f0) + ChangeUartBaudEv (0x40abe0) + Synchron
        bool ChangeBaudAndResync(int newBaud)
        {
            if (newBaud <= XR_ROM_BAUD) return true;

            // OpenUartEv (0x40e9c7): cmp r14d,0xe1000; ja → force g_baud=0xe1000
            if (newBaud > XR_WORK_BAUD)
            {
                addWarningLine($"Baud {newBaud} exceeds BROM v3 cap; clamping to {XR_WORK_BAUD}.");
                newBaud = XR_WORK_BAUD;
            }

            BROMResponse resp = ExecuteRawPacket(
                BuildChangeBaudPacket(newBaud), headerTimeoutMs: 2000, payloadTimeoutMs: 1000);
            if (resp.IsError)
            {
                addErrorLine($"ChangeBaud command failed for {newBaud} baud.");
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
                addErrorLine("Host baud switch failed: " + ex.Message);
                return false;
            }

            if (!Sync())
            {
                addErrorLine($"Re-sync failed after switching to {newBaud} baud.");
                return false;
            }
            addLogLine($"Transport baud set to {newBaud}.");
            return true;
        }

        // =====================================================================
        // Erase  (opcode 0x19)
        //
        // EraseFlashEj (0x40ced0): sends 18-byte command, receives 12-byte ACK.
        // No sleep in device-side function; we allow 8 s for worst-case 64 KB erase.
        // =====================================================================
        bool Erase64KBlock(int address)
        {
            BROMResponse resp = ExecuteRawPacket(
                BuildErasePacket(address), headerTimeoutMs: 8000, payloadTimeoutMs: 1000);
            if (resp.IsError)
            {
                addErrorLine($"Erase failed at 0x{address:X6}.");
                return false;
            }
            return true;
        }

        // =====================================================================
        // Read  (opcode 0x1A)
        //
        // ReadSectorEjPhj (0x40c620):
        //   WriteMsgEPhj(21 bytes)   ← send command
        //   usleep(0xC350)           ← 50 ms unconditional
        //   ReadMsgEPhj(12 bytes)    ← header
        //   CheckAckEv               ← reads payload (sectorCount × 512 bytes)
        //
        // ReadFlashLengthEjPhj (0x40cdb0):
        //   chunkSize = (baud == 0xe1000) ? 0x800 : 0x200
        //   → 4 sectors per call at 921600, 1 sector per call otherwise
        // =====================================================================
        byte[] ReadSectors(int sectorIndex, int sectorCount)
        {
            int expectedBytes = sectorCount * XR_SECTOR_SIZE;
            int payloadMs     = serial.BaudRate >= XR_WORK_BAUD ? 2000 : 4000;

            BROMResponse resp = ExecuteRawPacket(
                BuildReadPacket(sectorIndex, sectorCount),
                headerTimeoutMs:  4000,
                payloadTimeoutMs: payloadMs,
                sleepBeforeRead:  true);  // ← the unconditional 50 ms usleep

            if (resp.IsError)
            {
                addErrorLine($"Read failed at sector {sectorIndex}.");
                return null;
            }
            if (resp.Payload == null || resp.Payload.Length < expectedBytes)
            {
                addErrorLine($"Read sector {sectorIndex}: got {resp.Payload?.Length ?? 0} bytes, " +
                             $"expected {expectedBytes}.");
                return null;
            }
            return resp.Payload;
        }

        // =====================================================================
        // Write  (opcode 0x1B)
        //
        // WriteSectorEjPhj (0x40f1e0) – two-stage protocol:
        //   Stage 1: WriteMsgEPhj(23)   → ReadMsgEPhj(12)   command ACK
        //   Stage 2: WriteMsgEPhj(data) → ReadMsgEPhj(12)   final write ACK
        // No sleep between stages.
        //
        // Data checksum: ComputeChecksum over the sector data (same paddw algorithm,
        // confirmed from the SIMD loop at 0x40f260–0x40f380).
        //
        // Sector buffer is zero-padded to a whole sector boundary.  In PhoenixMC this
        // happens via memset(0x4000) at the top of WriteFlashLengthEjPhj; we replicate
        // it with MiscUtils.padArray.
        // =====================================================================
        bool WriteSectors(int sectorIndex, byte[] data, int sectorCount)
        {
            int    dataLen      = sectorCount * XR_SECTOR_SIZE;
            ushort dataChecksum = ComputeChecksum(data, 0, dataLen);

            // Stage 1: write command → command ACK
            BROMResponse cmdAck = ExecuteRawPacket(
                BuildWritePacket(sectorIndex, sectorCount, dataChecksum),
                headerTimeoutMs:  3000,
                payloadTimeoutMs: 1000);
            if (cmdAck.IsError)
            {
                addErrorLine($"Write command ACK error at sector {sectorIndex}.");
                return false;
            }

            // Stage 2: send raw sector data → final ACK
            serial.Write(data, 0, dataLen);
            serial.BaseStream.Flush();

            BROMResponse finalAck = ReadBromResponse(
                headerTimeoutMs: 10000, payloadTimeoutMs: 1000);
            if (finalAck.IsError)
            {
                addErrorLine($"Write final ACK error at sector {sectorIndex}.");
                return false;
            }
            return true;
        }

        // =====================================================================
        // High-level read loop
        // =====================================================================
        byte[] InternalRead(int startAddress, int length)
        {
            var result       = new byte[length];
            int done         = 0;
            int totalSectors = (length + XR_SECTOR_SIZE - 1) / XR_SECTOR_SIZE;
            int startSector  = startAddress / XR_SECTOR_SIZE;
            int perRead      = serial.BaudRate >= XR_WORK_BAUD ? 4 : 1;

            logger.setProgress(0, totalSectors);
            logger.setState("Reading...", Color.Transparent);

            for (int s = 0; s < totalSectors && !isCancelled; )
            {
                int count = Math.Min(perRead, totalSectors - s);
                byte[] chunk = ReadSectors(startSector + s, count);
                if (chunk == null)
                    throw new IOException(
                        $"Read failed at sector {startSector + s} " +
                        $"(0x{startAddress + s * XR_SECTOR_SIZE:X6}).");

                int take = Math.Min(count * XR_SECTOR_SIZE, length - done);
                Buffer.BlockCopy(chunk, 0, result, done, take);
                done += take;
                s    += count;
                logger.setProgress(s, totalSectors);
            }

            logger.setState("Read complete", Color.DarkGreen);
            return result;
        }

        // =====================================================================
        // High-level erase loop  (64 KB blocks, matches EraseFlashLengthEjj)
        //
        // EraseFlashLengthEjj (0x40d0c0):
        //   eraseStart = startAddr & ~0xFFFF  (rounded down to 64 KB boundary)
        //   loops in +0x10000 steps until address >= end
        //   breaks immediately on error
        // =====================================================================
        bool InternalEraseRange(int startAddress, int length)
        {
            int eraseStart = startAddress &  ~(XR_ERASE_BLOCK_SIZE - 1);
            int eraseEnd   = (startAddress + length + XR_ERASE_BLOCK_SIZE - 1)
                             & ~(XR_ERASE_BLOCK_SIZE - 1);
            int total = (eraseEnd - eraseStart) / XR_ERASE_BLOCK_SIZE;
            int done  = 0;

            logger.setProgress(0, total);
            logger.setState("Erasing...", Color.Transparent);

            for (int addr = eraseStart; addr < eraseEnd && !isCancelled; addr += XR_ERASE_BLOCK_SIZE)
            {
                addLogLine($"  Erasing 64 KB block at 0x{addr:X6}");
                if (!Erase64KBlock(addr))
                {
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                logger.setProgress(++done, total);
            }

            logger.setState("Erase complete", Color.DarkGreen);
            return !isCancelled;
        }

        // =====================================================================
        // High-level write loop  (matches WriteFlashLengthEjPhj at 0x40ffa0)
        //
        // PhoenixMC loop: chunk = 0x4000, sectors per write = 0x20 = 32.
        // Last partial chunk: ⌈remainingBytes / 512⌉ sectors; zero-padded.
        // =====================================================================
        bool InternalWrite(int startAddress, byte[] data)
        {
            int total = (data.Length + XR_WRITE_CHUNK_SIZE - 1) / XR_WRITE_CHUNK_SIZE;
            int done  = 0;

            logger.setProgress(0, total);
            logger.setState("Writing...", Color.Transparent);

            for (int offset = 0; offset < data.Length && !isCancelled; )
            {
                int chunkBytes = Math.Min(XR_WRITE_CHUNK_SIZE, data.Length - offset);

                // Zero-pad to next sector boundary (replicates memset in WriteFlashLength)
                byte[] chunk = MiscUtils.subArray(data, offset, chunkBytes);
                chunk = MiscUtils.padArray(chunk, XR_SECTOR_SIZE);

                int sectorIndex = (startAddress + offset) / XR_SECTOR_SIZE;
                int sectorCount = chunk.Length / XR_SECTOR_SIZE;

                addLog($"  0x{startAddress + offset:X6}  ");
                if (!WriteSectors(sectorIndex, chunk, sectorCount))
                {
                    logger.setState("Write failed", Color.Red);
                    return false;
                }
                addLogLine("OK");

                offset += chunkBytes;
                logger.setProgress(++done, total);
            }

            logger.setState("Write complete", Color.DarkGreen);
            return !isCancelled;
        }

        // =====================================================================
        // .img parsing + validation  (mirrors FwParser::Parse at 0x4111b0)
        //
        // FwParser::Parse checks, in order:
        //   1. AWIH magic (0x48495741) at each section header byte 0
        //      (0x4111f5: cmp DWORD PTR [rbp+0x18], 0x48495741)
        //   2. Header checksum: sum of all 16-bit words in the 64-byte header == 0xFFFF
        //      (using FwParser::CheckSum16, identical algorithm to CFlashHost::CheckSum16)
        //   3. Data checksum: stored_checksum + sum(data words) == 0xFFFF
        //   4. NextAddr == 0xFFFFFFFF → end of chain
        //
        // FwParser::CheckSum16 (0x411ba0) and CFlashHost::CheckSum16 (0x4086d0) are
        // byte-for-byte identical.  Both use the same paddw SIMD loop.
        // =====================================================================
        XRSectionHeader ParseSectionHeader(byte[] img, int offset)
        {
            return new XRSectionHeader
            {
                Magic          = BitConverter.ToUInt32(img, offset + 0x00),
                Version        = BitConverter.ToUInt32(img, offset + 0x04),
                HeaderChecksum = BitConverter.ToUInt16(img, offset + 0x08),
                DataChecksum   = BitConverter.ToUInt16(img, offset + 0x0A),
                DataSize       = BitConverter.ToUInt32(img, offset + 0x0C),
                LoadAddr       = BitConverter.ToUInt32(img, offset + 0x10),
                Entry          = BitConverter.ToUInt32(img, offset + 0x14),
                BodyLen        = BitConverter.ToUInt32(img, offset + 0x18),
                Attribute      = BitConverter.ToUInt32(img, offset + 0x1C),
                NextAddr       = BitConverter.ToUInt32(img, offset + 0x20),
                SectionId      = BitConverter.ToUInt32(img, offset + 0x24),
            };
        }

        // Sum of all 16-bit words across the 64-byte header including the stored
        // checksum itself must equal 0xFFFF.
        static bool ValidateHeaderChecksum(byte[] img, int offset)
        {
            ushort sum = 0;
            for (int i = 0; i < 0x40; i += 2)
                sum += (ushort)(img[offset + i] | (img[offset + i + 1] << 8));
            return sum == 0xFFFF;
        }

        // stored_checksum + sum(data words) must equal 0xFFFF.
        static bool ValidateDataChecksum(byte[] img, int dataOffset, int size, ushort stored)
        {
            ushort sum = stored;
            int i = 0;
            for (; i + 1 < size; i += 2)
                sum += (ushort)(img[dataOffset + i] | (img[dataOffset + i + 1] << 8));
            if ((size & 1) != 0)
                sum += img[dataOffset + size - 1];
            return sum == 0xFFFF;
        }

        int ValidateAndLogImgLayout(byte[] img)
        {
            addLogLine("");
            addLogLine("┌─────────────────────────────────────────────────────────────────────┐");
            addLogLine("│                    XR806 .img Section Layout                        │");
            addLogLine("├────────┬───────────┬──────────┬──────────┬──────────┬───────────────┤");
            addLogLine("│  Idx   │  Section  │  Offset  │   Size   │ LoadAddr │  Entry Point  │");
            addLogLine("├────────┼───────────┼──────────┼──────────┼──────────┼───────────────┤");

            int offset  = 0;
            int count   = 0;
            int lastEnd = 0;

            while (true)
            {
                if (offset < 0 || offset + 0x40 > img.Length)
                    throw new InvalidDataException(
                        $"Section header at 0x{offset:X} is out of file range.");

                XRSectionHeader hdr = ParseSectionHeader(img, offset);

                if (hdr.Magic != 0x48495741)
                    throw new InvalidDataException(
                        $"Bad AWIH magic at 0x{offset:X}: 0x{hdr.Magic:X8}");
                if (!ValidateHeaderChecksum(img, offset))
                    throw new InvalidDataException(
                        $"Header checksum mismatch at 0x{offset:X}.");

                int dataOffset = offset + 0x40;
                int dataSize   = (int)hdr.DataSize;

                if (dataOffset + dataSize > img.Length)
                    throw new InvalidDataException(
                        $"Section data at 0x{offset:X} overruns file.");
                if (!ValidateDataChecksum(img, dataOffset, dataSize, hdr.DataChecksum))
                    throw new InvalidDataException(
                        $"Data checksum mismatch at 0x{offset:X}.");

                lastEnd = dataOffset + dataSize;

                string name = SECTION_NAMES.TryGetValue(hdr.SectionId, out string n)
                              ? n : $"id_{hdr.SectionId:X8}";
                addLogLine(
                    $"│ [{count,2}]   │ {name,-9} │ 0x{offset:X6}  │ 0x{dataSize:X6} │" +
                    $" 0x{hdr.LoadAddr:X6} │ 0x{hdr.Entry:X10}  │");

                if (++count > 100)
                    throw new InvalidDataException("More than 100 sections – likely corrupt image.");

                if (hdr.NextAddr == 0xFFFFFFFF) break;
                offset = (int)hdr.NextAddr;
            }

            addLogLine("└────────┴───────────┴──────────┴──────────┴──────────┴───────────────┘");
            addLogLine($"  Sections: {count}   Effective size: 0x{lastEnd:X}  ({lastEnd} bytes)");
            addLogLine("");
            return lastEnd;
        }

        // =====================================================================
        // Save read result
        // =====================================================================
        public override bool saveReadResult(int startOffset)
        {
            if (readResult == null) { addErrorLine("No read result to save."); return false; }
            try
            {
                byte[] dat      = readResult.ToArray();
                string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType, backupName, "bin");
                Directory.CreateDirectory("backups");
                string fullPath = Path.Combine("backups", fileName);
                File.WriteAllBytes(fullPath, dat);
                addSuccess($"Saved {dat.Length} bytes → {fullPath}" + Environment.NewLine);
                logger.onReadResultQIOSaved(dat, "", fullPath);
                return true;
            }
            catch (Exception ex)
            {
                addErrorLine("Save failed: " + ex.Message);
                return false;
            }
        }

        // =====================================================================
        // BaseFlasher virtual overrides
        // =====================================================================

        // Returns the last read data so FormMain's Verify button (line 749) works.
        public override byte[] getReadResult()
        {
            return readResult?.ToArray();
        }

        // FormMain calls doWrite() for RF partition restore (lines 678, 708) and
        // test-pattern writes (line 557).  XR806 does not share Beken's RF layout;
        // log clearly instead of silently no-op'ing.
        public override void doWrite(int startSector, byte[] data)
        {
            addErrorLine("XR806: raw sector write via doWrite() is not supported on this chip.");
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            try
            {
                if (!DoGenericSetup())               return;
                if (!EnsureConnectedAndIdentified()) return;

                int startAddr, length;
                if (fullRead)
                {
                    startAddr = 0;
                    length    = flashSizeBytes;
                    addLogLine($"Full read: {flashSizeBytes / 0x100000} MB from 0x000000");
                }
                else
                {
                    startAddr = startSector * BK7231Flasher.SECTOR_SIZE;
                    length    = sectors     * BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Partial read: 0x{startAddr:X6} + 0x{length:X6} bytes");
                }

                byte[] data = InternalRead(startAddr, length);
                readResult?.Dispose();
                readResult = new MemoryStream(data);
                saveReadResult(startAddr);
            }
            catch (OperationCanceledException)
            {
                addLogLine("Read cancelled.");
            }
            catch (Exception ex)
            {
                addErrorLine("Read error: " + ex.Message);
            }
            finally
            {
                closePort();
            }
        }

        public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
        {
            try
            {
                if (!DoGenericSetup())               return false;
                if (!EnsureConnectedAndIdentified()) return false;

                int startAddr, length;
                if (bAll)
                {
                    startAddr = 0;
                    length    = flashSizeBytes;
                    addLogLine($"Full erase: {flashSizeBytes / 0x100000} MB");
                }
                else
                {
                    startAddr = startSector * BK7231Flasher.SECTOR_SIZE;
                    length    = sectors     * BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Partial erase: 0x{startAddr:X6} + 0x{length:X6} bytes");
                }
                return InternalEraseRange(startAddr, length);
            }
            catch (OperationCanceledException)
            {
                addLogLine("Erase cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                addErrorLine("Erase error: " + ex.Message);
                return false;
            }
            finally
            {
                closePort();
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
                if (!DoGenericSetup())               return;
                if (!EnsureConnectedAndIdentified()) return;

                // ── Backup (ReadAndWrite mode) ────────────────────────────────
                if (rwMode == WriteMode.ReadAndWrite)
                {
                    addLogLine($"Backing up full flash ({flashSizeBytes / 0x100000} MB)...");
                    byte[] backup = InternalRead(0, flashSizeBytes);
                    if (isCancelled) return;
                    readResult?.Dispose();
                    readResult = new MemoryStream(backup);
                    if (!saveReadResult(0)) return;
                }

                if (isCancelled) return;

                // ── Write ─────────────────────────────────────────────────────
                if (rwMode != WriteMode.OnlyWrite && rwMode != WriteMode.ReadAndWrite) return;

                if (string.IsNullOrEmpty(sourceFileName) || !File.Exists(sourceFileName))
                {
                    addErrorLine($"Source file not found: {sourceFileName}");
                    return;
                }

                addLogLine($"Loading: {sourceFileName}");
                byte[] fileData  = File.ReadAllBytes(sourceFileName);
                int writeAddress = startSector * BK7231Flasher.SECTOR_SIZE;
                int effectiveLen = fileData.Length;
                string ext       = Path.GetExtension(sourceFileName).ToLowerInvariant();

                if (ext == ".img")
                {
                    // Full FwParser-equivalent validation with layout log
                    effectiveLen = ValidateAndLogImgLayout(fileData);
                    writeAddress = 0;
                    addLogLine($"Validated .img: writing 0x{effectiveLen:X} bytes to offset 0x000000");
                }
                else if (ext == ".bin")
                {
                    // Raw write path – matches PhoenixMC -B flag (no FwParser, no AWIH check).
                    // Opportunistically show layout if AWIH is present.
                    if (fileData.Length >= 4 &&
                        BitConverter.ToUInt32(fileData, 0) == 0x48495741)
                    {
                        addLogLine(".bin starts with AWIH – showing layout:");
                        try { ValidateAndLogImgLayout(fileData); }
                        catch (Exception ex) { addWarningLine("Layout parse error: " + ex.Message); }
                    }
                    addWarningLine($"Writing raw .bin at byte offset 0x{writeAddress:X6}.");
                }
                else
                {
                    addWarningLine($"Unrecognised extension \"{ext}\" – writing raw bytes.");
                }

                if (effectiveLen > flashSizeBytes)
                {
                    addErrorLine($"Image (0x{effectiveLen:X} bytes) exceeds flash size " +
                                 $"({flashSizeBytes / 0x100000} MB).  Aborting.");
                    return;
                }

                byte[] toWrite = new byte[effectiveLen];
                Buffer.BlockCopy(fileData, 0, toWrite, 0, effectiveLen);

                addLogLine("Erasing target region...");
                if (!InternalEraseRange(writeAddress, effectiveLen)) return;
                if (isCancelled) return;

                addLogLine("Writing firmware...");
                if (!InternalWrite(writeAddress, toWrite)) return;

                addSuccess("XR806 flash write complete!" + Environment.NewLine);
            }
            catch (OperationCanceledException)
            {
                addLogLine("Operation cancelled.");
            }
            catch (Exception ex)
            {
                addErrorLine("Fatal error: " + ex.Message);
            }
            finally
            {
                // closePort() is always called regardless of how we exit –
                // including via exception, cancellation, or early return.
                // This is the only port-close path; no double-close is possible
                // because base.closePort() checks serial != null.
                closePort();
            }
        }
    }
}
