using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

namespace BK7231Flasher
{
    public class XR809Flasher : XRBaseFlasher
    {
        const int  XR_STUB_LOAD_ADDR    = 0x00010000;
        const int  XR_STUB_ENTRY_ADDR   = 0x00010101;

        // Opcodes
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
        // XR809 transport bauds accepted by the ROM/stub path.
        public static readonly int[] SupportedBaudRates =
        {
            9600,
            115200,
            921600,
            1000000,
            1500000,
            3000000,
        };

        public static string SupportedBaudRatesText => string.Join(", ", SupportedBaudRates);

        // XR809 .img section ID to name mapping.
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

        bool         ramStubLoaded;
        bool         useExtendedFlashOpcodes;
        public XR809Flasher(CancellationToken ct) : base(ct)
        {
        }

        protected override Dictionary<uint, string> SectionNames
        {
            get { return SECTION_NAMES; }
        }

        protected override string FormatHeaderIncompleteMessage(byte[] header)
        {
            return $"BROM header incomplete ({header?.Length ?? 0}/12 bytes): {FormatHexSnippet(header)}.";
        }

        public static bool IsAllowedXRBaud(int baud)
        {
            switch (baud)
            {
                case 9600:
                case 115200:
                case 921600:
                case 1000000:
                case 1500000:
                case 3000000:
                    return true;
                default:
                    return false;
            }
        }

        static int GetNearestSupportedXRBaud(int baud)
        {
            int bestBaud = SupportedBaudRates[0];
            int bestDiff = Math.Abs(bestBaud - baud);
            for (int i = 1; i < SupportedBaudRates.Length; i++)
            {
                int candidate = SupportedBaudRates[i];
                int diff = Math.Abs(candidate - baud);
                if (diff < bestDiff || (diff == bestDiff && candidate < bestBaud))
                {
                    bestBaud = candidate;
                    bestDiff = diff;
                }
            }
            return bestBaud;
        }

        int NormalizeRequestedXRBaud(int requestedBaud, string context)
        {
            if (IsAllowedXRBaud(requestedBaud))
                return requestedBaud;

            int fallbackBaud = GetNearestSupportedXRBaud(requestedBaud);
            addWarningLine(
                $"{context}: XR809 does not support {requestedBaud} baud; using nearest supported baud {fallbackBaud}. " +
                $"Supported bauds: {SupportedBaudRatesText}.");
            return fallbackBaud;
        }

        // =====================================================================
        // Packet building
        //
        // payloadPre is used for checksum calculation.
        // payloadWire is emitted on the serial wire.
        // requestType is header byte 5: 0 for simple query packets and 1 for
        // payload-bearing commands.
        // =====================================================================
        new byte[] BuildBromPacket(byte opcode, byte[] payloadPre, byte[] payloadWire, byte requestType = BROM_HOST_PAYLOAD)
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
                // Legacy XR GetFlashId request format.
                return new byte[]
                {
                    0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51,
                    0x00, 0x00, 0x00, 0x01, 0x18
                };
            }
            return BuildBromPacket(opcode, new byte[0], new byte[0], BROM_HOST_QUERY);
        }

        byte[] BuildEraseChipPacket(byte opcode)
        {
            var payload = new byte[] { 0x00 };
            return BuildBromPacket(opcode, payload, payload, BROM_HOST_PAYLOAD);
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
            return BuildBromPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
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
            return BuildBromPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
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
            return BuildBromPacket(opcode, pre, wire, BROM_HOST_PAYLOAD);
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
            return BuildBromPacket(OP_WRITE_MEMORY, pre, wire, BROM_HOST_PAYLOAD);
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
            return BuildBromPacket(OP_SET_PC, pre, wire, BROM_HOST_PAYLOAD);
        }

        // ChangeBaud packet: 4-byte payload = (baud | 0x03000000) BE.
        protected override byte[] BuildChangeBaudPacket(int newBaud)
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
            return BuildBromPacket(OP_CHANGE_BAUD, pre, wire, BROM_HOST_PAYLOAD);
        }

        // =====================================================================
        // GetFlashId
        //
        // Older ROM/stub revisions use the legacy 0x18 query. Version 3+
        // transports may expose the extended 0x1C form. The returned header
        // byte 5 carries the active BROM/stub version in either case.
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
                    ? "XR809 BROM/stub reports v3+, enabling extended XR opcodes (0x1C-0x1F)."
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
        // 4. If BROM version <= 1: upload the RAM stub to 0x10000 and
        //    jump to 0x10101, then re-sync on the stub transport
        // 5. ChangeBaud to the requested working rate and re-Sync
        // 6. GetFlashId again so later read/write/erase logic sees the active
        //    stub/BROM capabilities.
        // =====================================================================
        protected override bool EnsureConnectedAndIdentified()
        {
            bromVersion = 0;
            flashID = null;
            ramStubLoaded = false;
            useExtendedFlashOpcodes = false;

            TryEnterDownloadModeByUpgradeCommand();
            if (!Sync())        return false;
            if (!ReadFlashId()) return false;

            int requestedBaud = NormalizeRequestedXRBaud(this.baudrate, "GUI baud selection");

            if (bromVersion < 2)
            {
                ReadChipType();

                addLogLine(
                    $"XR809 BROM version 0x{bromVersion:X2} requires a RAM stub; " +
                    $"uploading RAM stub loader at {XR_ROM_BAUD} baud.");

                if (!UploadRamStub())
                    return false;
                if (!Sync())
                {
                    addErrorLine("XR809 RAM stub did not respond to the standard 0x55/\"OK\" sync.");
                    return false;
                }
            }

            int workBaud = requestedBaud;
            if (!ChangeBaudAndResync(workBaud))
                return false;
            if (!ReadFlashId())
                return false;
            ReadChipType();
            return true;
        }

        // Sends the ChangeBaud command, switches the host serial port and re-syncs.
        bool ChangeBaudAndResync(int newBaud)
        {
            if (!IsAllowedXRBaud(newBaud))
            {
                int fallbackBaud = GetNearestSupportedXRBaud(newBaud);
                addWarningLine(
                    $"XR809 does not support {newBaud} baud; switching transport to nearest supported baud {fallbackBaud}. " +
                    $"Supported bauds: {SupportedBaudRatesText}.");
                newBaud = fallbackBaud;
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
                addErrorLine($"XR809 RAM stub upload command failed at 0x{address:X8}.");
                return false;
            }

            serial.Write(data, 0, data.Length);
            serial.BaseStream.Flush();

            BROMResponse finalAck = ReadBromResponse(
                headerTimeoutMs: 10000, payloadTimeoutMs: 1000);
            if (finalAck.IsError)
            {
                addErrorLine($"XR809 RAM stub upload final ACK failed at 0x{address:X8}.");
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

            // Go straight from SetPC to the normal 0x55/"OK" sync sequence.
            // Do not probe RAM/register state here; on XR809/BROM1 that extra
            // read can fail before the stub transport is ready.
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

        // XR809 supports 4 KB and 64 KB addressed erase modes. Range erase uses
        // 64 KB only for fully covered aligned blocks, preserving 4 KB precision.
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
                    GetReadWaitMs());
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
        // response header is available. This transport uses 0x1000-byte read
        // chunks in its full-read loop, i.e. 8 sectors per request.
        // =====================================================================
        byte[] ReadSectorsOnce(int sectorIndex, int sectorCount)
        {
            int expectedBytes = sectorCount * XR_SECTOR_SIZE;
            int payloadMs     = GetReadTimeoutMs();

            BROMResponse resp = ExecuteFlashCommandWithOpcodeFallback(
                opcode => ExecuteRawPacket(
                    BuildReadPacket(sectorIndex, sectorCount, opcode),
                    headerTimeoutMs:  GetReadTimeoutMs(),
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

        protected override byte[] ReadSectors(int sectorIndex, int sectorCount)
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

        bool EraseChipOnce()
        {
            BROMResponse ack = ExecuteFlashCommandWithOpcodeFallback(
                selectedOpcode => ExecuteRawPacket(
                    BuildEraseChipPacket(selectedOpcode),
                    headerTimeoutMs: 5000,
                    payloadTimeoutMs: 1000),
                OP_ERASE,
                OP_ERASE_EXT,
                "Erase");
            if (ack.IsError)
            {
                addErrorLine("XR809 full-chip erase command returned an error.");
                return false;
            }
            return true;
        }

        protected override bool WriteSectors(int sectorIndex, byte[] data, int sectorCount)
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
        // High-level erase
        //
        // The explicit erase action uses single-command 0x19 00 full-chip
        // erase, while the pre-write erase path erases only the target range.
        // Custom raw writes intentionally skip erase.
        // =====================================================================
        bool InternalRangeErase(int startAddress, int length)
        {
            if (length <= 0)
            {
                addWarningLine("Erase range is empty; skipping erase.");
                return true;
            }

            int eraseStart = startAddress & ~(XR_ERASE_SECTOR_SIZE - 1);
            long requestedEnd = (long)startAddress + length;
            int eraseEnd = (int)((requestedEnd + XR_ERASE_SECTOR_SIZE - 1) & ~(long)(XR_ERASE_SECTOR_SIZE - 1));
            int headEnd = Math.Min(eraseEnd, (eraseStart + XR_ERASE_BLOCK_SIZE - 1) & ~(XR_ERASE_BLOCK_SIZE - 1));
            if (headEnd + XR_ERASE_BLOCK_SIZE > eraseEnd)
                headEnd = eraseStart;

            int bulkStart = headEnd;
            int bulkEnd = bulkStart + ((eraseEnd - bulkStart) / XR_ERASE_BLOCK_SIZE) * XR_ERASE_BLOCK_SIZE;
            int headCommands = (headEnd - eraseStart) / XR_ERASE_SECTOR_SIZE;
            int bulkCommands = (bulkEnd - bulkStart) / XR_ERASE_BLOCK_SIZE;
            int tailCommands = (eraseEnd - bulkEnd) / XR_ERASE_SECTOR_SIZE;
            int totalCommands = headCommands + bulkCommands + tailCommands;

            logger.setProgress(0, totalCommands);
            logger.setState("Erasing...", Color.Transparent);
            addLogLine($"Range erase: 0x{eraseStart:X6} + 0x{eraseEnd - eraseStart:X6} " +
                       $"({bulkCommands} x 64 KB, {headCommands + tailCommands} x 4 KB)");

            int progress = 0;
            for (int address = eraseStart; address < headEnd && !isCancelled; address += XR_ERASE_SECTOR_SIZE)
            {
                addLog($"4K@0x{address:X6}... ");
                if (!EraseBlockOnce(address, XR_ERASE_MODE_4K))
                {
                    addLog(Environment.NewLine);
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                logger.setProgress(++progress, totalCommands);
            }

            for (int address = bulkStart; address < bulkEnd && !isCancelled; address += XR_ERASE_BLOCK_SIZE)
            {
                addLog($"64K@0x{address:X6}... ");
                if (!EraseBlockOnce(address, XR_ERASE_MODE_64K))
                {
                    addLog(Environment.NewLine);
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                logger.setProgress(++progress, totalCommands);
            }

            for (int address = bulkEnd; address < eraseEnd && !isCancelled; address += XR_ERASE_SECTOR_SIZE)
            {
                addLog($"4K@0x{address:X6}... ");
                if (!EraseBlockOnce(address, XR_ERASE_MODE_4K))
                {
                    addLog(Environment.NewLine);
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                logger.setProgress(++progress, totalCommands);
            }

            addLog(Environment.NewLine);
            logger.setState("Erase complete", Color.DarkGreen);
            return !isCancelled;
        }

        bool InternalExplicitChipErase()
        {
            logger.setProgress(0, 1);
            logger.setState("Erasing...", Color.Transparent);
            addLog("Erasing: chip... ");

            bool ok = EraseChipOnce();
            if (!ok)
            {
                addLog(Environment.NewLine);
                logger.setState("Erase failed", Color.Red);
                return false;
            }

            logger.setProgress(1, 1);
            addLog(Environment.NewLine);
            logger.setState("Erase complete", Color.DarkGreen);
            return true;
        }

        protected override bool PerformRangeErase(int startAddress, int length)
        {
            return InternalRangeErase(startAddress, length);
        }

        protected override bool PerformExplicitErase()
        {
            return InternalExplicitChipErase();
        }
    }
}
