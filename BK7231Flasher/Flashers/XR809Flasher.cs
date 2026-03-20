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
    public class XR809Flasher : BaseFlasher
    {
        // =====================================================================
        // Constants
        // =====================================================================

        const int  XR_ROM_BAUD         = 115200;    // BROM default after reset
        const int  XR_SAFE_WORK_BAUD   = 921600;    // PhoenixMC caps the BROM1 stub upload at 921600
        const int  XR_SECTOR_SIZE      = 0x200;     // 512 bytes
        const int  XR_ERASE_BLOCK_SIZE = 0x10000;   // 64 KB, matches PhoenixMC's main erase path
        const int  XR_WRITE_CHUNK_SIZE = 0x4000;    // 16 KB, 32 sectors per write command
        const int  XR_READ_RETRY_COUNT  = 3;
        const int  XR_WRITE_RETRY_COUNT = 2;
        const int  XR_SYNC_RETRY_COUNT  = 10;
        const int  XR_UPGRADE_SETTLE_MS = 120;
        const int  XR_MAX_BROM_PAYLOAD  = XR_WRITE_CHUNK_SIZE;
        const int  XR_STUB_LOAD_ADDR    = 0x00010000;
        const int  XR_STUB_ENTRY_ADDR   = 0x00010101;

        // BROM packet byte constants
        const byte BROM_HOST_FLAGS     = 0x04;      // host→device flags byte
        const byte BROM_HOST_QUERY     = 0x00;      // PhoenixMC uses 0 for simple no-payload requests
        const byte BROM_HOST_PAYLOAD   = 0x01;      // PhoenixMC uses 1 when the request carries parameters/data metadata
        const byte BROM_FLAG_ERROR     = 0x01;      // bit 0 of device→host flags byte = command failed

        // Opcodes
        const byte OP_READ_4           = 0x04;
        const byte OP_WRITE_MEMORY     = 0x09;
        const byte OP_CHANGE_BAUD      = 0x10;
        const byte OP_SET_PC           = 0x13;
        const byte OP_GET_FLASH_ID     = 0x18;
        const byte OP_ERASE            = 0x19;
        const byte OP_READ             = 0x1A;
        const byte OP_WRITE            = 0x1B;
        const byte OP_GET_FLASH_ID_EXT = 0x1C;
        const byte OP_ERASE_EXT        = 0x1D;
        const byte OP_READ_EXT         = 0x1E;
        const byte OP_WRITE_EXT        = 0x1F;

        const byte XR_ERASE_MODE_4K    = 0x01;
        const byte XR_ERASE_MODE_64K   = 0x03;

        // PhoenixMC Windows exact timeout buckets are known for 115200, 921600,
        // 1000000, 1500000 and 3000000. Other GUI bauds use the nearest slower
        // known bucket here rather than being blocked.

        // XR809 .img section ID to name mapping.
        // PhoenixMC carries an internal XR section-ID table alongside a
        // reverse-ordered section-name table. The first eight IDs in that table
        // match the XR809 images we have and map cleanly to the names below.
        static readonly Dictionary<uint, string> SECTION_NAMES = new Dictionary<uint, string>
        {
            { 0xA5FF5A00, "boot"     },
            { 0xA5FE5A01, "app"      },
            { 0xA5FD5A02, "app_xip"  },
            { 0xA5FC5A03, "net"      },
            { 0xA5FB5A04, "net_ap"   },
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
        byte         bromVersion    = 0x00;
        bool         ramStubLoaded;
        bool         useExtendedFlashOpcodes;

        // =====================================================================
        // Response / header structures
        // =====================================================================
        struct BROMResponse
        {
            public byte   Flags;
            public byte   BromVersion;
            public ushort Checksum;         // present in header but never validated by the BROM
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
        public XR809Flasher(CancellationToken ct) : base(ct)
        {
        }

        static bool IsAllowedXRBaud(int baud)
        {
            switch (baud)
            {
                case 9600:
                case 115200:
                case 230400:
                case 460800:
                case 921600:
                case 1000000:
                case 1500000:
                case 2000000:
                case 3000000:
                    return true;
                default:
                    return false;
            }
        }

        int GetPhoenixReadWaitMs()
        {
            int baud = serial?.BaudRate ?? XR_ROM_BAUD;
            switch (baud)
            {
                case 9600:
                    return int.MaxValue;
                case 115200:
                case 230400:
                case 460800:
                    return 500;
                case 921600:
                case 1000000:
                    return 100;
                case 1500000:
                case 2000000:
                    return 70;
                case 3000000:
                    return 30;
                default:
                    return 500;
            }
        }

        int GetPhoenixReadTimeoutMs()
        {
            int waitMs = GetPhoenixReadWaitMs();
            if (waitMs == int.MaxValue)
                return int.MaxValue;
            int total = waitMs * 40;
            return total < 1000 ? 1000 : total;
        }

        // Easy Flasher passes XR809 startSector/sectors in the shared public
        // 4 KB framework unit used by several platform front-ends. Convert that
        // contract to byte addresses here, then later to the native 512-byte
        // XR transport sectors used on the wire.
        static int PublicSectorIndexToByteAddress(int startSector)
        {
            return startSector * BK7231Flasher.SECTOR_SIZE;
        }

        static int PublicSectorCountToByteLength(int sectors)
        {
            return sectors * BK7231Flasher.SECTOR_SIZE;
        }

        bool IsCustomRawWriteRequest(int startSector, int sectors, WriteMode rwMode)
        {
            if (rwMode != WriteMode.OnlyWrite)
                return false;

            int fullFlashFrameworkSectors = BK7231Flasher.FLASH_SIZE / BK7231Flasher.SECTOR_SIZE;
            return startSector != 0 || sectors != fullFlashFrameworkSectors;
        }

        static bool StartsWithAwihMagic(byte[] data)
        {
            return data != null && data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0x48495741;
        }

        // =====================================================================
        // Checksum
        //
        // Returns the 16-bit additive checksum used by XR packet preimages
        // and XR .img section validation. The running sum wraps naturally at
        // 16 bits and the final value is bitwise inverted.
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
        // payloadPre is used for checksum calculation.
        // payloadWire is emitted on the serial wire.
        // requestType is header byte 5; PhoenixMC uses 0 for simple query packets
        // like GetFlashId/GetChipType, and 1 for payload-bearing commands.
        // =====================================================================
        byte[] BuildPhoenixPacket(byte opcode, byte[] payloadPre, byte[] payloadWire, byte requestType = BROM_HOST_PAYLOAD)
        {
            if (payloadPre  == null) payloadPre  = new byte[0];
            if (payloadWire == null) payloadWire = new byte[0];

            int logicalLen = 1 + payloadPre.Length; // 1 for opcode

            // ── preimage: LE length, zeroed checksum, LE payload ──────────────
            byte[] pre = new byte[12 + logicalLen];
            pre[0] = (byte)'B'; pre[1] = (byte)'R'; pre[2] = (byte)'O'; pre[3] = (byte)'M';
            pre[4] = BROM_HOST_FLAGS;
            pre[5] = requestType;
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
            pkt[5] = requestType;
            pkt[6]  = (byte)((cs >> 8) & 0xFF);   // checksum big-endian
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

        byte[] BuildGetFlashIdPacket(byte opcode = OP_GET_FLASH_ID)
        {
            if (opcode == OP_GET_FLASH_ID)
            {
                // Known-good legacy XR GetFlashId request captured from the
                // working PhoenixMC path.
                return new byte[]
                {
                    0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51,
                    0x00, 0x00, 0x00, 0x01, 0x18
                };
            }
            return BuildPhoenixPacket(opcode, new byte[0], new byte[0], BROM_HOST_QUERY);
        }

        byte[] BuildEraseBlockPacket(int address, byte eraseMode, byte opcode)
        {
            var pre  = new byte[5];
            var wire = new byte[5];
            pre[0]  = eraseMode;
            wire[0] = eraseMode;

            pre[1] = (byte)( address        & 0xFF);
            pre[2] = (byte)((address >>  8) & 0xFF);
            pre[3] = (byte)((address >> 16) & 0xFF);
            pre[4] = (byte)((address >> 24) & 0xFF);

            wire[1] = (byte)((address >> 24) & 0xFF);
            wire[2] = (byte)((address >> 16) & 0xFF);
            wire[3] = (byte)((address >>  8) & 0xFF);
            wire[4] = (byte)( address        & 0xFF);
            return BuildPhoenixPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
        }

        // Read packet payload: big-endian sector index and sector count.
        byte[] BuildReadPacket(int sectorIndex, int sectorCount, byte opcode)
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
            return BuildPhoenixPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
        }

        // Write packet: 23 bytes total, 10-byte payload (sectorIndex BE, sectorCount BE, dataChecksum BE).
        byte[] BuildWritePacket(int sectorIndex, int sectorCount, ushort dataChecksum, byte opcode)
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
            return BuildPhoenixPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
        }

        byte[] BuildWriteMemoryPacket(int address, int length, ushort dataChecksum)
        {
            var pre  = new byte[10];
            var wire = new byte[10];
            pre[0] = (byte)( address        & 0xFF);
            pre[1] = (byte)((address >>  8) & 0xFF);
            pre[2] = (byte)((address >> 16) & 0xFF);
            pre[3] = (byte)((address >> 24) & 0xFF);
            pre[4] = (byte)( length         & 0xFF);
            pre[5] = (byte)((length  >>  8) & 0xFF);
            pre[6] = (byte)((length  >> 16) & 0xFF);
            pre[7] = (byte)((length  >> 24) & 0xFF);
            pre[8] = (byte)( dataChecksum        & 0xFF);
            pre[9] = (byte)((dataChecksum >>  8) & 0xFF);

            wire[0] = (byte)((address >> 24) & 0xFF);
            wire[1] = (byte)((address >> 16) & 0xFF);
            wire[2] = (byte)((address >>  8) & 0xFF);
            wire[3] = (byte)( address        & 0xFF);
            wire[4] = (byte)((length  >> 24) & 0xFF);
            wire[5] = (byte)((length  >> 16) & 0xFF);
            wire[6] = (byte)((length  >>  8) & 0xFF);
            wire[7] = (byte)( length         & 0xFF);
            wire[8] = (byte)((dataChecksum >>  8) & 0xFF);
            wire[9] = (byte)( dataChecksum        & 0xFF);
            return BuildPhoenixPacket(OP_WRITE_MEMORY, pre, wire, BROM_HOST_PAYLOAD);
        }

        byte[] BuildRead4Packet(int address)
        {
            var pre  = new byte[4];
            var wire = new byte[4];
            pre[0] = (byte)( address        & 0xFF);
            pre[1] = (byte)((address >>  8) & 0xFF);
            pre[2] = (byte)((address >> 16) & 0xFF);
            pre[3] = (byte)((address >> 24) & 0xFF);
            wire[0] = (byte)((address >> 24) & 0xFF);
            wire[1] = (byte)((address >> 16) & 0xFF);
            wire[2] = (byte)((address >>  8) & 0xFF);
            wire[3] = (byte)( address        & 0xFF);
            return BuildPhoenixPacket(OP_READ_4, pre, wire, BROM_HOST_PAYLOAD);
        }

        byte[] BuildSetPcPacket(int address)
        {
            var pre  = new byte[4];
            var wire = new byte[4];
            pre[0] = (byte)( address        & 0xFF);
            pre[1] = (byte)((address >>  8) & 0xFF);
            pre[2] = (byte)((address >> 16) & 0xFF);
            pre[3] = (byte)((address >> 24) & 0xFF);
            wire[0] = (byte)((address >> 24) & 0xFF);
            wire[1] = (byte)((address >> 16) & 0xFF);
            wire[2] = (byte)((address >>  8) & 0xFF);
            wire[3] = (byte)( address        & 0xFF);
            return BuildPhoenixPacket(OP_SET_PC, pre, wire, BROM_HOST_PAYLOAD);
        }

        // ChangeBaud packet: 4-byte payload = (baud | 0x03000000) BE.
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
            return BuildPhoenixPacket(OP_CHANGE_BAUD, pre, wire, BROM_HOST_PAYLOAD);
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

        // Sends ChangeBaud(115200) to the device before closing so it returns to the
        // default baud and doesn't need a power cycle to reconnect next session.
        // Best-effort — any failure is swallowed; base.closePort() is always called.
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

        static string FormatHexSnippet(byte[] data, int maxBytes = 16)
        {
            if (data == null || data.Length == 0)
                return "<none>";

            int shown = data.Length > maxBytes ? maxBytes : data.Length;
            var sb = new StringBuilder(shown * 3 + 8);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            if (data.Length > shown)
                sb.Append(" ...");
            return sb.ToString();
        }

        byte[] ReadExact(int count, int timeoutMs)
        {
            var buf = new byte[count];
            int got = 0;
            var sw  = Stopwatch.StartNew();
            long deadlineMs     = timeoutMs;
            long hardDeadlineMs = timeoutMs + 500;

            while (got < count && !isCancelled)
            {
                try
                {
                    int r = serial.Read(buf, got, count - got);
                    if (r > 0)
                    {
                        got += r;
                        long extendedDeadline = sw.ElapsedMilliseconds + 200;
                        if (extendedDeadline > deadlineMs)
                            deadlineMs = extendedDeadline > hardDeadlineMs ? hardDeadlineMs : extendedDeadline;
                        continue;
                    }
                }
                catch (TimeoutException) { }

                if (sw.ElapsedMilliseconds >= deadlineMs)
                    break;
                Thread.Sleep(5);
            }
            if (got == count) return buf;
            if (got == 0)     return null;
            var partial = new byte[got];
            Buffer.BlockCopy(buf, 0, partial, 0, got);
            return partial;
        }

        byte[] ReadBromHeader(int timeoutMs)
        {
            var header = new byte[12];
            int got = 0;
            int matchedMagicBytes = 0;
            var sw = Stopwatch.StartNew();
            long deadlineMs = timeoutMs;
            long hardDeadlineMs = timeoutMs + 500;

            while (!isCancelled)
            {
                try
                {
                    int b = serial.ReadByte();
                    if (b >= 0)
                    {
                        byte value = (byte)b;

                        if (got == 0)
                        {
                            switch (matchedMagicBytes)
                            {
                                case 0:
                                    matchedMagicBytes = value == (byte)'B' ? 1 : 0;
                                    break;
                                case 1:
                                    matchedMagicBytes = value == (byte)'R' ? 2 : (value == (byte)'B' ? 1 : 0);
                                    break;
                                case 2:
                                    matchedMagicBytes = value == (byte)'O' ? 3 : (value == (byte)'B' ? 1 : 0);
                                    break;
                                default:
                                    if (value == (byte)'M')
                                    {
                                        header[0] = (byte)'B';
                                        header[1] = (byte)'R';
                                        header[2] = (byte)'O';
                                        header[3] = (byte)'M';
                                        got = 4;
                                    }
                                    matchedMagicBytes = value == (byte)'M' ? 0 : (value == (byte)'B' ? 1 : 0);
                                    break;
                            }
                        }
                        else
                        {
                            header[got++] = value;
                            long extendedDeadline = sw.ElapsedMilliseconds + 200;
                            if (extendedDeadline > deadlineMs)
                                deadlineMs = extendedDeadline > hardDeadlineMs ? hardDeadlineMs : extendedDeadline;
                            if (got == header.Length)
                                return header;
                        }

                        continue;
                    }
                }
                catch (TimeoutException) { }

                if (sw.ElapsedMilliseconds >= deadlineMs)
                    break;
                Thread.Sleep(5);
            }

            if (got == 0)
                return null;

            var partial = new byte[got];
            Buffer.BlockCopy(header, 0, partial, 0, got);
            return partial;
        }

        // Best-effort software jump into download mode. In PhoenixMC captures
        // taken at 115200 before BROM sync, the observed preamble is:
        // LF, "upgrade", LF NUL. Devices without console support may ignore
        // it; the normal 0x55/"OK" sync still follows.
        void TryEnterDownloadModeByUpgradeCommand()
        {
            if (serial == null || !serial.IsOpen)
                return;

            try
            {
                if (serial.BaudRate != XR_ROM_BAUD)
                {
                    serial.BaudRate = XR_ROM_BAUD;
                    Thread.Sleep(30);
                }

                addLogLine("Attempting XR809 software upgrade command before sync...");
                DrainInput(quietMs: 40, hardLimitMs: 120);

                serial.Write(new byte[] { 0x0A }, 0, 1);
                byte[] cmd = Encoding.ASCII.GetBytes("upgrade");
                serial.Write(cmd, 0, cmd.Length);
                serial.Write(new byte[] { 0x0A, 0x00 }, 0, 2);
                serial.BaseStream.Flush();
                Thread.Sleep(XR_UPGRADE_SETTLE_MS);

                // The running app may echo console text or briefly emit its own
                // response before dropping into BROM. Clear any such bytes so the
                // normal 0x55/"OK" sync starts from a clean state.
                DrainInput(quietMs: 40, hardLimitMs: 200);
            }
            catch (Exception ex)
            {
                addLogLine($"Upgrade pre-sync command did not complete: {ex.Message}");
            }
        }

        // =====================================================================
        // Sync
        //
        // Sends 0x55, expects "OK" (2 bytes) back, up to XR_SYNC_RETRY_COUNT attempts.
        // =====================================================================
        bool Sync()
        {
            for (int attempt = 1; attempt <= XR_SYNC_RETRY_COUNT; attempt++)
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
                    // Only mention the attempt number if it wasn't the first try,
                    // so a normal first-attempt fail (port not yet settled) stays silent.
                    if (attempt > 1)
                        addLogLine($"Sync OK (attempt {attempt})");
                    else
                        addLogLine("Sync OK");
                    DrainInput();
                    return true;
                }
            }
            addErrorLine($"Sync failed after {XR_SYNC_RETRY_COUNT} attempts.");
            return false;
        }

        // =====================================================================
        // Low-level packet exchange
        // =====================================================================
        BROMResponse ReadBromResponse(int headerTimeoutMs, int payloadTimeoutMs)
        {
            byte[] header = ReadBromHeader(headerTimeoutMs);
            if (header == null || header.Length < 12)
                throw new IOException(
                    $"BROM header incomplete ({header?.Length ?? 0}/12 bytes): {FormatHexSnippet(header)}.");
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

            if (resp.PayloadLength < 0)
                throw new IOException($"Negative BROM payload length {resp.PayloadLength}.");
            if (resp.PayloadLength > XR_MAX_BROM_PAYLOAD)
                throw new IOException(
                    $"BROM payload length {resp.PayloadLength} exceeds XR809 transport ceiling {XR_MAX_BROM_PAYLOAD}.");

            // Response checksum is not validated; the BROM itself never checks it either.
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

        BROMResponse ReadFixedHeaderResponse(int headerTimeoutMs)
        {
            byte[] header = ReadBromHeader(headerTimeoutMs);
            if (header == null || header.Length < 12)
                throw new IOException(
                    $"BROM header incomplete ({header?.Length ?? 0}/12 bytes): {FormatHexSnippet(header)}.");
            if (header[0] != 'B' || header[1] != 'R' || header[2] != 'O' || header[3] != 'M')
                throw new IOException(
                    $"BROM magic mismatch: {header[0]:X2} {header[1]:X2} {header[2]:X2} {header[3]:X2}");

            return new BROMResponse
            {
                Flags         = header[4],
                BromVersion   = header[5],
                Checksum      = (ushort)((header[6] << 8) | header[7]),
                PayloadLength = (header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11],
                Payload       = new byte[0],
            };
        }

        BROMResponse ExecuteRawPacket(
            byte[] pkt,
            int    headerTimeoutMs  = 1500,
            int    payloadTimeoutMs = 3000,
            bool   sleepBeforeRead  = false)
        {
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

            // The BROM requires a 50 ms delay between sending a read command and
            // reading back the response header.
            if (sleepBeforeRead)
                Thread.Sleep(50);

            return ReadBromResponse(headerTimeoutMs, payloadTimeoutMs);
        }

        // =====================================================================
        // GetChipType  (opcode 0x17)
        //
        // Sends a 13-byte packet (same format as GetFlashId, opcode 0x17 instead
        // of 0x18) and reads a single-byte chip type value from the response
        // payload.  On XR809/BROM1 this is mainly useful after the RAM stub has
        // started; older ROMs may ignore it.
        // Failure is silently ignored — not all BROMs respond.
        // =====================================================================
        void ReadChipType()
        {
            // Packet: BROM magic | flags 0x04 0x00 | checksum 0x60 0x52 | logicalLen 0x00 0x00 0x00 0x01 | opcode 0x17
            var pkt = new byte[] { 0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x52,
                                   0x00, 0x00, 0x00, 0x01, 0x17 };
            try
            {
                BROMResponse resp = ExecuteRawPacket(pkt, headerTimeoutMs: 1500,
                                                          payloadTimeoutMs: 1500);
                if (!resp.IsError && resp.PayloadLength >= 1)
                    addLogLine($"Chip type    : {resp.Payload[0]}");
            }
            catch { }
        }

        // =====================================================================
        // GetFlashId
        //
        // PhoenixMC uses the legacy 0x18 query on older ROMs, but enables the
        // extended 0x1C form once the active BROM/stub reports version 3+ on
        // the "new BROM" path. The returned header byte 5 carries the active
        // BROM/stub version in either case.
        // =====================================================================
        bool ReadFlashId()
        {
            bool preferExtended = ramStubLoaded || useExtendedFlashOpcodes || bromVersion >= 3;

            BROMResponse resp = preferExtended
                ? ExecuteFlashCommandWithOpcodeFallback(
                    opcode => ExecuteRawPacket(
                        BuildGetFlashIdPacket(opcode), headerTimeoutMs: 1500, payloadTimeoutMs: 1500),
                    OP_GET_FLASH_ID,
                    OP_GET_FLASH_ID_EXT,
                    "GetFlashId")
                : ExecuteRawPacket(
                    BuildGetFlashIdPacket(OP_GET_FLASH_ID), headerTimeoutMs: 1500, payloadTimeoutMs: 1500);
            bromVersion = resp.BromVersion;

            if (resp.IsError)  { addErrorLine("GetFlashId returned error."); return false; }
            if (resp.PayloadLength < 3)
            {
                addErrorLine($"GetFlashId: payload too short ({resp.PayloadLength} bytes).");
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
            }

            bool newExtendedMode = bromVersion >= 3;
            if (newExtendedMode != useExtendedFlashOpcodes)
            {
                useExtendedFlashOpcodes = newExtendedMode;
                addLogLine(useExtendedFlashOpcodes
                    ? "XR809 BROM/stub reports v3+, enabling PhoenixMC extended opcodes (0x1C-0x1F)."
                    : "Using legacy XR opcodes (0x18-0x1B).");
            }
            return true;
        }

        // =====================================================================
        // Connection sequence
        //
        // 1. Best-effort software jump to download mode at 115200
        // 2. Sync at 115200
        // 3. GetFlashId  (reads ROM BROM version and JEDEC flash ID)
        // 4. If BROM version <= 1: upload PhoenixMC's RAM stub to 0x10000 and
        //    jump to 0x10101, then re-sync on the stub transport
        // 5. ChangeBaud to the requested working rate and re-Sync
        // 6. GetFlashId again so later read/write/erase logic sees the active
        //    stub/BROM capabilities.
        // =====================================================================
        bool EnsureConnectedAndIdentified()
        {
            bromVersion = 0;
            flashID = null;
            ramStubLoaded = false;
            useExtendedFlashOpcodes = false;

            TryEnterDownloadModeByUpgradeCommand();
            if (!Sync())        return false;
            if (!ReadFlashId()) return false;
            ReadChipType();

            if (bromVersion < 2)
            {
                int uploadBaud = this.baudrate;
                if (uploadBaud > XR_SAFE_WORK_BAUD)
                    uploadBaud = XR_SAFE_WORK_BAUD;
                if (uploadBaud <= XR_ROM_BAUD)
                    uploadBaud = XR_ROM_BAUD;

                addLogLine(
                    $"XR809 BROM version 0x{bromVersion:X2} requires a RAM stub; " +
                    $"uploading PhoenixMC loader at up to {uploadBaud} baud.");

                if (!ChangeBaudAndResync(uploadBaud))
                    return false;
                if (!UploadRamStub())
                    return false;
                if (!Sync())
                {
                    addErrorLine("XR809 RAM stub did not respond to the standard 0x55/\"OK\" sync.");
                    return false;
                }
            }

            if (!ChangeBaudAndResync(this.baudrate))
                return false;
            if (!ReadFlashId())
                return false;
            ReadChipType();
            return true;
        }

        // Sends the ChangeBaud command, switches the host serial port and re-syncs.
        bool ChangeBaudAndResync(int newBaud)
        {
            if (newBaud <= XR_ROM_BAUD) return true;

            if (!IsAllowedXRBaud(newBaud))
            {
                addWarningLine($"Baud {newBaud} is not supported by this XR809 transport profile; falling back to {XR_SAFE_WORK_BAUD}.");
                newBaud = XR_SAFE_WORK_BAUD;
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

        bool WriteMemoryOnce(int address, byte[] data)
        {
            ushort dataChecksum = ComputeChecksum(data, 0, data.Length);

            BROMResponse cmdAck = ExecuteRawPacket(
                BuildWriteMemoryPacket(address, data.Length, dataChecksum),
                headerTimeoutMs: 3000,
                payloadTimeoutMs: 1000);
            if (cmdAck.IsError)
            {
                addErrorLine($"XR809 RAM loader upload command failed at 0x{address:X8}.");
                return false;
            }

            serial.Write(data, 0, data.Length);
            serial.BaseStream.Flush();

            BROMResponse finalAck = ReadBromResponse(
                headerTimeoutMs: 10000, payloadTimeoutMs: 1000);
            if (finalAck.IsError)
            {
                addErrorLine($"XR809 RAM loader upload final ACK failed at 0x{address:X8}.");
                return false;
            }
            return true;
        }

        bool SetPc(int address)
        {
            BROMResponse resp = ExecuteRawPacket(
                BuildSetPcPacket(address), headerTimeoutMs: 3000, payloadTimeoutMs: 1000);
            if (resp.IsError)
            {
                addErrorLine($"XR809 SetPC(0x{address:X8}) returned an error.");
                return false;
            }
            return true;
        }

        bool Read4Byte(int address, out int value)
        {
            value = 0;
            BROMResponse resp = ExecuteRawPacket(
                BuildRead4Packet(address), headerTimeoutMs: 3000, payloadTimeoutMs: 1500);
            if (resp.IsError || resp.PayloadLength < 4)
                return false;
            value = (resp.Payload[0] << 24) | (resp.Payload[1] << 16) |
                    (resp.Payload[2] << 8)  |  resp.Payload[3];
            return true;
        }

        bool UploadRamStub()
        {
            byte[] stub = FLoaders.GetBinaryFromAssembly("XR809_Stub");
            if (stub == null || stub.Length == 0)
            {
                addErrorLine("XR809 BROM stub resource is missing.");
                return false;
            }

            addLogLine($"Uploading XR809 BROM1 stub ({stub.Length} bytes) to 0x{XR_STUB_LOAD_ADDR:X8}...");
            if (!WriteMemoryOnce(XR_STUB_LOAD_ADDR, stub))
                return false;
            if (!SetPc(XR_STUB_ENTRY_ADDR))
                return false;

            ramStubLoaded = true;
            useExtendedFlashOpcodes = false;
            Thread.Sleep(50);

            if (Read4Byte(0x40040100, out int loaderCookie))
                addLogLine($"XR809 stub handshake register 0x40040100 = 0x{loaderCookie:X8}");

            addLogLine($"XR809 BROM stub started at 0x{XR_STUB_ENTRY_ADDR:X8}.");
            return true;
        }

        bool RecoverSession(string reason)
        {
            addWarningLine($"XR809 transport recovery: {reason}");
            try { closePort(); } catch { }
            Thread.Sleep(100);
            if (!DoGenericSetup()) return false;
            return EnsureConnectedAndIdentified();
        }

        string FormatBromResponseForLog(BROMResponse resp)
        {
            return $"flags=0x{resp.Flags:X2}, brom=0x{resp.BromVersion:X2}, checksum=0x{resp.Checksum:X4}, payloadLen={resp.PayloadLength}, status={(resp.IsError ? "ERROR" : "OK")}";
        }

        BROMResponse ExecuteFlashCommandWithOpcodeFallback(
            Func<byte, BROMResponse> executor,
            byte legacyOpcode,
            byte extendedOpcode,
            string commandName)
        {
            if (useExtendedFlashOpcodes)
            {
                try
                {
                    BROMResponse resp = executor(extendedOpcode);
                    if (!resp.IsError)
                        return resp;
                    addWarningLine(
                        $"{commandName}: XR809 extended opcode 0x{extendedOpcode:X2} returned an error, " +
                        $"falling back to legacy opcode 0x{legacyOpcode:X2}.");
                }
                catch (Exception ex)
                {
                    addWarningLine(
                        $"{commandName}: XR809 extended opcode 0x{extendedOpcode:X2} failed ({ex.Message}), " +
                        $"falling back to legacy opcode 0x{legacyOpcode:X2}.");
                }
                useExtendedFlashOpcodes = false;
            }

            return executor(legacyOpcode);
        }

        // PhoenixMC erases XR809 in 64 KB blocks with erase mode 0x03. A 4 KB
        // variant (mode 0x01) also exists, but the Easy Flasher UI currently
        // keeps XR809 on the same full-chip erase semantics as XR806/XR872.
        bool EraseBlockOnce(int address, byte eraseMode)
        {
            void WriteErasePacket(byte selectedOpcode)
            {
                byte[] pkt = BuildEraseBlockPacket(address, eraseMode, selectedOpcode);
                serial.Write(pkt, 0, pkt.Length);
                serial.BaseStream.Flush();
            }

            BROMResponse ReadEraseAck(byte selectedOpcode)
            {
                WriteErasePacket(selectedOpcode);

                int headerTimeoutMs = Math.Max(
                    eraseMode == XR_ERASE_MODE_4K ? 500 : 1000,
                    GetPhoenixReadWaitMs());
                int sleepMs = eraseMode == XR_ERASE_MODE_4K ? 100 : 200;

                for (int attempt = 1; attempt <= 10 && !isCancelled; attempt++)
                {
                    try
                    {
                        BROMResponse resp = ReadFixedHeaderResponse(headerTimeoutMs);
                        if (resp.PayloadLength != 0)
                        {
                            if (resp.PayloadLength > 0)
                                ReadExact(resp.PayloadLength, 1000);
                            throw new IOException(
                                $"Erase ACK for 0x{address:X8} returned unexpected payload length {resp.PayloadLength}.");
                        }
                        return resp;
                    }
                    catch
                    {
                        if (attempt >= 10) throw;
                        Thread.Sleep(sleepMs);
                    }
                }

                throw new IOException($"Timed out waiting for erase ACK at 0x{address:X8}.");
            }

            BROMResponse ack = ExecuteFlashCommandWithOpcodeFallback(
                ReadEraseAck, OP_ERASE, OP_ERASE_EXT, "Erase");
            if (ack.IsError)
            {
                addErrorLine($"Erase failed at 0x{address:X8}. ACK: {FormatBromResponseForLog(ack)}");
                return false;
            }

            return true;
        }

        // =====================================================================
        // Read  (opcode 0x1A, or 0x1E once the active XR809 BROM/stub reports
        // the extended command set)
        //
        // The BROM requires a 50 ms delay after sending the command before the
        // response header is available. PhoenixMC Windows uses 0x1000-byte
        // read chunks in its full-read loop, i.e. 8 sectors per request.
        // =====================================================================
        byte[] ReadSectorsOnce(int sectorIndex, int sectorCount)
        {
            int expectedBytes = sectorCount * XR_SECTOR_SIZE;
            int payloadMs     = GetPhoenixReadTimeoutMs();

            BROMResponse resp = ExecuteFlashCommandWithOpcodeFallback(
                opcode => ExecuteRawPacket(
                    BuildReadPacket(sectorIndex, sectorCount, opcode),
                    headerTimeoutMs:  GetPhoenixReadTimeoutMs(),
                    payloadTimeoutMs: payloadMs,
                    sleepBeforeRead:  true),
                OP_READ,
                OP_READ_EXT,
                "Read");

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

        byte[] ReadSectors(int sectorIndex, int sectorCount)
        {
            Exception lastEx = null;
            for (int attempt = 1; attempt <= XR_READ_RETRY_COUNT && !isCancelled; attempt++)
            {
                try
                {
                    byte[] data = ReadSectorsOnce(sectorIndex, sectorCount);
                    if (data != null)
                    {
                        if (attempt > 1)
                            addWarningLine($"Read sector {sectorIndex} recovered on attempt {attempt}.");
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    addWarningLine($"Read sector {sectorIndex} attempt {attempt} failed: {ex.Message}");
                }

                try
                {
                    DrainInput();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                }
                catch { }

                if (attempt < XR_READ_RETRY_COUNT)
                {
                    if (attempt >= 2 && !RecoverSession($"read sector {sectorIndex} retry {attempt + 1}"))
                        break;
                    Thread.Sleep(50);
                }
            }

            if (lastEx != null)
                throw new IOException($"Read failed at sector {sectorIndex} after {XR_READ_RETRY_COUNT} attempts.", lastEx);
            return null;
        }

        // =====================================================================
        // Write  (opcode 0x1B, or 0x1F once the active XR809 BROM/stub reports
        // the extended command set)
        //
        // Two-stage: send 23-byte command and receive ACK, then send raw sector
        // data and receive a second ACK.  The data checksum is embedded in the
        // command packet.  Buffers are zero-padded to a sector boundary.
        // =====================================================================
        bool WriteSectorsOnce(int sectorIndex, byte[] data, int sectorCount)
        {
            int    dataLen      = sectorCount * XR_SECTOR_SIZE;
            ushort dataChecksum = ComputeChecksum(data, 0, dataLen);

            BROMResponse cmdAck = ExecuteFlashCommandWithOpcodeFallback(
                opcode => ExecuteRawPacket(
                    BuildWritePacket(sectorIndex, sectorCount, dataChecksum, opcode),
                    headerTimeoutMs: 3000,
                    payloadTimeoutMs: 1000),
                OP_WRITE,
                OP_WRITE_EXT,
                "Write");
            if (cmdAck.IsError)
            {
                addErrorLine($"Write command ACK error at sector {sectorIndex}.");
                return false;
            }

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

        bool WriteSectors(int sectorIndex, byte[] data, int sectorCount)
        {
            Exception lastEx = null;
            for (int attempt = 1; attempt <= XR_WRITE_RETRY_COUNT && !isCancelled; attempt++)
            {
                try
                {
                    bool ok = WriteSectorsOnce(sectorIndex, data, sectorCount);
                    if (ok)
                    {
                        if (attempt > 1)
                            addWarningLine($"Write sector {sectorIndex} recovered on attempt {attempt}.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    addWarningLine($"Write sector {sectorIndex} attempt {attempt} failed: {ex.Message}");
                }

                try
                {
                    DrainInput();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                }
                catch { }

                if (attempt < XR_WRITE_RETRY_COUNT)
                {
                    if (!RecoverSession($"write sector {sectorIndex} retry {attempt + 1}"))
                        break;
                    Thread.Sleep(50);
                }
            }

            if (lastEx != null)
                throw new IOException($"Write failed at sector {sectorIndex} after {XR_WRITE_RETRY_COUNT} attempts.", lastEx);
            return false;
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
            int perRead      = 8;

            logger.setProgress(0, totalSectors);
            logger.setState("Reading...", Color.Transparent);
            addLog("Reading: ");

            for (int s = 0; s < totalSectors && !isCancelled; )
            {
                int count = Math.Min(perRead, totalSectors - s);
                addLog($"0x{startAddress + s * XR_SECTOR_SIZE:X6}... ");
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

            addLog(Environment.NewLine);
            logger.setState("Read complete", Color.DarkGreen);
            return result;
        }

        // =====================================================================
        // High-level erase
        //
        // XR809 uses the same UI-facing full-chip erase semantics as XR806/XR872,
        // but the underlying transport matches PhoenixMC's 64 KB block erase
        // path used for pre-write erase. Custom raw writes intentionally skip erase.
        // Addressed erase is intentionally not used in this implementation.
        // =====================================================================
        bool InternalChipErase()
        {
            int totalBlocks = Math.Max(1, flashSizeBytes / XR_ERASE_BLOCK_SIZE);

            logger.setProgress(0, totalBlocks);
            logger.setState("Erasing...", Color.Transparent);
            addLog("Erasing: ");

            for (int block = 0; block < totalBlocks && !isCancelled; block++)
            {
                int address = block * XR_ERASE_BLOCK_SIZE;
                addLog($"0x{address:X6}... ");
                if (!EraseBlockOnce(address, XR_ERASE_MODE_64K))
                {
                    addLog(Environment.NewLine);
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                logger.setProgress(block + 1, totalBlocks);
            }

            addLog(Environment.NewLine);
            logger.setState("Erase complete", Color.DarkGreen);
            return !isCancelled;
        }

        // =====================================================================
        // High-level write loop
        //
        // Writes in 16 KB chunks (32 sectors).  The last chunk is zero-padded
        // to a full sector boundary before being sent.
        // =====================================================================
        bool InternalWrite(int startAddress, byte[] data)
        {
            int total = (data.Length + XR_WRITE_CHUNK_SIZE - 1) / XR_WRITE_CHUNK_SIZE;
            int done  = 0;

            logger.setProgress(0, total);
            logger.setState("Writing...", Color.Transparent);
            addLog("Writing: ");

            for (int offset = 0; offset < data.Length && !isCancelled; )
            {
                int chunkBytes = Math.Min(XR_WRITE_CHUNK_SIZE, data.Length - offset);

                byte[] chunk = MiscUtils.subArray(data, offset, chunkBytes);
                chunk = MiscUtils.padArray(chunk, XR_SECTOR_SIZE);

                int sectorIndex = (startAddress + offset) / XR_SECTOR_SIZE;
                int sectorCount = chunk.Length / XR_SECTOR_SIZE;

                addLog($"0x{startAddress + offset:X6}... ");
                if (!WriteSectors(sectorIndex, chunk, sectorCount))
                {
                    addLog(Environment.NewLine);
                    logger.setState("Write failed", Color.Red);
                    return false;
                }

                offset += chunkBytes;
                logger.setProgress(++done, total);
            }

            addLog(Environment.NewLine);
            logger.setState("Write complete", Color.DarkGreen);
            return !isCancelled;
        }

        // =====================================================================
        // .img parsing and validation
        //
        // Each section has a 64-byte header followed by data.  The header
        // contains the AWIH magic, header and data checksums, data size, load
        // address, entry point, and the flash offset of the next section
        // (0xFFFFFFFF = end of chain).
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

        int ValidateAndLogImgLayout(byte[] img, bool logLayout = true)
        {
            int offset  = 0;
            int count   = 0;
            int lastEnd = 0;
            var seenOffsets = new HashSet<int>();

            while (true)
            {
                if (!seenOffsets.Add(offset))
                    throw new InvalidDataException(
                        $"Section chain loops back to 0x{offset:X}.");
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

                if (dataSize < 0)
                    throw new InvalidDataException(
                        $"Negative section size at 0x{offset:X}.");
                if (dataOffset + dataSize > img.Length)
                    throw new InvalidDataException(
                        $"Section data at 0x{offset:X} overruns file.");
                if (!ValidateDataChecksum(img, dataOffset, dataSize, hdr.DataChecksum))
                    throw new InvalidDataException(
                        $"Data checksum mismatch at 0x{offset:X}.");

                if (offset < lastEnd)
                    throw new InvalidDataException(
                        $"Section at 0x{offset:X} overlaps earlier image data ending at 0x{lastEnd:X}.");

                lastEnd = dataOffset + dataSize;

                if (logLayout)
                {
                    string name = SECTION_NAMES.TryGetValue(hdr.SectionId, out string n)
                                  ? n : $"id_{hdr.SectionId:X8}";
                    addLogLine($"  [{count}] {name}");
                    addLogLine($"      offset=0x{offset:X6}  size=0x{dataSize:X6}  load=0x{hdr.LoadAddr:X8}  entry=0x{hdr.Entry:X8}");
                }

                if (++count > 100)
                    throw new InvalidDataException("More than 100 sections – likely corrupt image.");

                if (hdr.NextAddr == 0xFFFFFFFF) break;

                if (hdr.NextAddr < (uint)lastEnd)
                    throw new InvalidDataException(
                        $"Next section pointer 0x{hdr.NextAddr:X} falls inside current section ending at 0x{lastEnd:X}.");
                if (hdr.NextAddr > (uint)(img.Length - 0x40))
                    throw new InvalidDataException(
                        $"Next section pointer 0x{hdr.NextAddr:X} is out of file range.");
                if (hdr.NextAddr <= (uint)offset)
                    throw new InvalidDataException(
                        $"Next section pointer 0x{hdr.NextAddr:X} does not advance past 0x{offset:X}.");

                offset = (int)hdr.NextAddr;
            }

            if (logLayout)
                addLogLine($"  {count} section(s), effective size 0x{lastEnd:X} ({lastEnd} bytes)");
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

        // Needed by the Verify button in FormMain.
        public override byte[] getReadResult()
        {
            return readResult?.ToArray();
        }

        // Easy Flasher's generic raw doWrite() path relies on Beken-specific
        // RF/partition assumptions, so it is intentionally disabled for XR809.
        public override void doWrite(int startSector, byte[] data)
        {
            addErrorLine("XR809: raw sector write via doWrite() is not supported on this chip.");
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
                    startAddr = PublicSectorIndexToByteAddress(startSector);
                    length    = PublicSectorCountToByteLength(sectors);
                    addLogLine($"Partial read: 0x{startAddr:X6} + 0x{length:X6} bytes (framework 4 KB units)");
                }

                byte[] data = InternalRead(startAddr, length);
                readResult?.Dispose();
                readResult = new MemoryStream(data);
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

                if (bAll)
                {
                    addLogLine($"Full erase requested: {flashSizeBytes / 0x100000} MB");
                }
                else
                {
                    int requestedStartAddr = PublicSectorIndexToByteAddress(startSector);
                    int requestedLength    = PublicSectorCountToByteLength(sectors);
                    addWarningLine("XR809 erase is full-chip only; ignoring requested " +
                                   $"range 0x{requestedStartAddr:X6} + 0x{requestedLength:X6} bytes.");
                }
                bool ok = InternalChipErase();
                if (ok) addSuccess("Erase complete!" + Environment.NewLine);
                return ok;
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
                addErrorLine("XR809 does not support standalone OBK config writes.");
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
                byte[] fileData           = File.ReadAllBytes(sourceFileName);
                bool customRawWrite       = IsCustomRawWriteRequest(startSector, sectors, rwMode);
                int requestedWriteAddress = PublicSectorIndexToByteAddress(startSector);
                int writeAddress          = requestedWriteAddress;
                int effectiveLen          = fileData.Length;
                string ext                = Path.GetExtension(sourceFileName).ToLowerInvariant();
                bool treatAsXRImage       = false;

                if (customRawWrite)
                {
                    addLogLine($"Custom write: raw bytes only, no erase, destination 0x{writeAddress:X6}.");
                    if (ext != ".bin")
                        addWarningLine($"Custom write ignores extension \"{ext}\" and writes raw bytes as selected.");
                }
                else if (ext == ".img")
                {
                    // Validate now (throws on bad checksum/magic) but defer layout log
                    // to just before write so it doesn't appear before the erase step.
                    effectiveLen = ValidateAndLogImgLayout(fileData, logLayout: false);
                    treatAsXRImage = true;
                    if (requestedWriteAddress != 0)
                    {
                        addWarningLine($".img write ignores requested offset 0x{requestedWriteAddress:X6} " +
                                       "and starts at 0x000000.");
                    }
                    writeAddress = 0;
                    addLogLine($"Validated .img: 0x{effectiveLen:X} bytes, will write to offset 0x000000");
                }
                else if (ext == ".bin")
                {
                    if (StartsWithAwihMagic(fileData))
                    {
                        addLogLine(".bin starts with AWIH magic, but main .bin writes are treated as raw full-flash data.");
                    }
                    addWarningLine($"Writing raw .bin at byte offset 0x{writeAddress:X6}.");
                }
                else
                {
                    addWarningLine($"Unrecognised extension \"{ext}\" – writing raw bytes.");
                }

                if ((long)writeAddress + effectiveLen > flashSizeBytes)
                {
                    addErrorLine($"Write range 0x{writeAddress:X6} + 0x{effectiveLen:X} bytes exceeds flash size " +
                                 $"({flashSizeBytes / 0x100000} MB).  Aborting.");
                    return;
                }

                byte[] toWrite = new byte[effectiveLen];
                Buffer.BlockCopy(fileData, 0, toWrite, 0, effectiveLen);

                if (!customRawWrite)
                {
                    addLogLine("XR809 write path performs a full-chip erase before programming.");
                    addLogLine($"Post-erase write destination: 0x{writeAddress:X6}");
                    if (!InternalChipErase()) return;
                    if (isCancelled) return;

                    // Show layout now, just before writing, so it appears in context
                    if (treatAsXRImage)
                    {
                        addLogLine("Image layout:");
                        ValidateAndLogImgLayout(fileData, logLayout: true);
                    }
                }

                addLogLine(customRawWrite ? "Writing raw custom data..." : "Writing firmware...");
                if (!InternalWrite(writeAddress, toWrite)) return;

                addSuccess("XR809 flash write complete!" + Environment.NewLine);
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
                closePort();
            }
        }
    }
}
