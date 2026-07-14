using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace BK7231Flasher
{
    public abstract class XRRomFlasherBase : XRBaseFlasher, IRomReadFlasher
    {
        const byte OP_READ32     = 0x04;
        const byte OP_WRITE32    = 0x05;
        const byte OP_READ_MEMORY = 0x08;
        const byte OP_CHANGE_BAUD = 0x10;
        const byte OP_ERASE       = 0x19;
        const byte OP_READ        = 0x1A;
        const byte OP_WRITE       = 0x1B;

        const int  XR_ROM_BASE            = 0x00000000;
        const int  XR806_ROM_SIZE         = 0x00028000;
        const int  XR872_ROM_SIZE         = 0x00028000;
        const int  XR_ROM_READ_CHUNK_SIZE = 0x1000;
        const int  XR_EFUSE_BYTES         = 0x80;
        const int  XR_EFUSE_WORDS         = XR_EFUSE_BYTES / 4;
        const int  XR_EFUSE_POLL_TIMEOUT_MS = 250;
        const int  XR_EFUSE_CTRL          = 0x40043C40;
        const int  XR_EFUSE_READ_VALUE    = 0x40043C60;
        const int  XR_EFUSE_TIMING_CTRL   = 0x40043C90;
        const uint XR_EFUSE_CLK_GATE_EN   = 0x10000000;
        const uint XR_EFUSE_INDEX_MASK    = 0x00FF0000;
        const uint XR_EFUSE_LOCK_MASK     = 0x0000FF00;
        const uint XR_EFUSE_UNLOCK        = 0x0000AC00;
        const uint XR_EFUSE_HW_BUSY       = 0x00000004;
        const uint XR_EFUSE_READ_START    = 0x00000002;
        const uint XR_EFUSE_PROG_START    = 0x00000001;
        const uint XR_EFUSE_TIMING_24_26M = 0x63321190;

        const byte XR_ERASE_MODE_CHIP = 0x00;
        const byte XR_ERASE_MODE_4K   = 0x01;
        const byte XR_ERASE_MODE_64K  = 0x03;

        protected XRRomFlasherBase(CancellationToken ct) : base(ct)
        {
        }

        protected abstract string LegacyLoaderUnsupportedMessage { get; }

        byte[] BuildGetFlashIdPacket()
        {
            return BuildLegacyGetFlashIdPacket();
        }

        byte[] BuildChipErasePacket()
        {
            var payload = new byte[] { XR_ERASE_MODE_CHIP };
            return BuildBromPacket(OP_ERASE, payload, payload);
        }

        byte[] BuildEraseBlockPacket(int address, byte eraseMode)
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
            return BuildBromPacket(OP_ERASE, pre, wire);
        }

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
            return BuildBromPacket(OP_READ, pre, wire);
        }

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
            return BuildBromPacket(OP_WRITE, pre, wire);
        }

        byte[] BuildRead32Packet(int address)
        {
            var pre  = new byte[4];
            var wire = new byte[4];
            WriteLe32(pre, 0, (uint)address);
            WriteBe32(wire, 0, (uint)address);
            return BuildBromPacket(OP_READ32, pre, wire, BROM_HOST_PAYLOAD);
        }

        byte[] BuildWrite32Packet(int address, uint value)
        {
            var pre  = new byte[8];
            var wire = new byte[8];
            WriteLe32(pre, 0, (uint)address);
            WriteBe32(wire, 0, (uint)address);
            WriteLe32(pre, 4, value);
            WriteLe32(wire, 4, value);
            return BuildBromPacket(OP_WRITE32, pre, wire, BROM_HOST_PAYLOAD);
        }

        byte[] BuildReadMemoryPacket(int address, int length)
        {
            var pre  = new byte[8];
            var wire = new byte[8];
            WriteLe32(pre, 0, (uint)address);
            WriteBe32(wire, 0, (uint)address);
            WriteLe32(pre, 4, (uint)length);
            WriteBe32(wire, 4, (uint)length);
            return BuildBromPacket(OP_READ_MEMORY, pre, wire, BROM_HOST_PAYLOAD);
        }

        static void WriteLe32(byte[] data, int offset, uint value)
        {
            data[offset + 0] = (byte)( value        & 0xFF);
            data[offset + 1] = (byte)((value >>  8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        static void WriteBe32(byte[] data, int offset, uint value)
        {
            data[offset + 0] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >>  8) & 0xFF);
            data[offset + 3] = (byte)( value        & 0xFF);
        }

        static uint ReadLe32(byte[] data, int offset)
        {
            return (uint)(
                data[offset + 0] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

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
            return BuildBromPacket(OP_CHANGE_BAUD, pre, wire);
        }

        bool ReadFlashId()
        {
            BROMResponse resp = ExecuteRawPacket(
                BuildGetFlashIdPacket(), headerTimeoutMs: 1500, payloadTimeoutMs: 1500);
            return TrySetFlashIdFromResponse(resp);
        }

        protected override bool EnsureConnectedAndIdentified()
        {
            TryEnterDownloadModeByUpgradeCommand();
            if (!Sync())        return false;
            if (!ReadFlashId()) return false;
            ReadChipType();

            if (bromVersion < 2)
            {
                addErrorLine(
                    $"BROM version 0x{bromVersion:X2} requires a RAM loader upload " +
                    "(used by older XR chips).  " + LegacyLoaderUnsupportedMessage);
                return false;
            }
            return ChangeBaudAndResync(this.baudrate);
        }

        bool ChangeBaudAndResync(int newBaud)
        {
            if (newBaud <= XR_ROM_BAUD) return true;

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

        bool RecoverSession(string reason)
        {
            addWarningLine($"{XRChipName} transport recovery: {reason}");
            try { closePort(); } catch { }
            Thread.Sleep(100);
            if (!DoGenericSetup()) return false;
            return EnsureConnectedAndIdentified();
        }

        string FormatBromResponseForLog(BROMResponse resp)
        {
            return $"flags=0x{resp.Flags:X2}, brom=0x{resp.BromVersion:X2}, checksum=0x{resp.Checksum:X4}, payloadLen={resp.PayloadLength}, status={(resp.IsError ? "ERROR" : "OK")}";
        }

        int GetRomReadSize()
        {
            switch (chipType)
            {
                case BKType.XR806:
                    return XR806_ROM_SIZE;
                case BKType.XR872:
                    return XR872_ROM_SIZE;
                default:
                    throw new NotSupportedException(chipType + " ROM read size is not defined for this flasher.");
            }
        }

        public byte[] ReadRomTarget(RomReadTarget target)
        {
            try
            {
                if (target == null)
                {
                    addError("No ROM reader target selected." + Environment.NewLine);
                    return null;
                }
                if (!DoGenericSetup()) return null;
                if (!EnsureConnectedAndIdentified()) return null;

                string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
                switch (target.Kind)
                {
                    case RomReadKind.Rom:
                        return ReadXrRom(target.Address ?? XR_ROM_BASE, target.Length ?? GetRomReadSize(), targetKindName);
                    case RomReadKind.Efuse:
                        return ReadXrEfuse(target.Length ?? XR_EFUSE_BYTES, targetKindName);
                    default:
                        addError("Selected " + chipType + " read target is not implemented." + Environment.NewLine);
                        return null;
                }
            }
            catch (OperationCanceledException)
            {
                string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
                addLogLine(targetKindName + " read cancelled by user.");
                logger.setState("Cancelled", Color.DarkGray);
                return null;
            }
            catch (Exception ex)
            {
                string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
                addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
                logger.setState(targetKindName + " read failed.", Color.Red);
                return null;
            }
            finally
            {
                try { closePort(); } catch { }
            }
        }

        byte[] ReadXrRom(int offset, int length, string targetKindName)
        {
            int romSize = GetRomReadSize();
            if (offset < XR_ROM_BASE || length <= 0 || offset > XR_ROM_BASE + romSize - length)
            {
                throw new ArgumentOutOfRangeException("length", chipType + " ROM read range is outside the supported BootROM area.");
            }

            logger.setState("Reading " + targetKindName + "...", Color.Transparent);
            logger.setProgress(0, length);
            addLogLine("Reading " + chipType + " " + targetKindName + " via PhoenixMC BROM memory read from " +
                formatHex(offset) + ", length " + formatHex(length) + ".");

            ProbeXrRomVectors();

            byte[] result = new byte[length];
            int copied = 0;
            while (copied < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunkAddress = offset + copied;
                int chunkLength = Math.Min(XR_ROM_READ_CHUNK_SIZE, length - copied);
                addLog("0x" + chunkAddress.ToString("X6") + "... ");
                byte[] chunk = ReadXrMemoryBlock(chunkAddress, chunkLength);
                Buffer.BlockCopy(chunk, 0, result, copied, chunkLength);
                copied += chunkLength;
                logger.setProgress(copied, length);
            }

            addLog(Environment.NewLine);
            logger.setState(targetKindName + " read success!", Color.Green);
            return result;
        }

        void ProbeXrRomVectors()
        {
            uint sp = ReadXrMemory32(0x00000000);
            uint reset = ReadXrMemory32(0x00000004);
            uint nmi = ReadXrMemory32(0x00000008);
            addLogLine(chipType + " ROM vector probe: SP=0x" + sp.ToString("X8") +
                ", Reset=0x" + reset.ToString("X8") + ", NMI=0x" + nmi.ToString("X8"));

            bool blank = sp == 0 && reset == 0 && nmi == 0;
            bool erased = sp == 0xFFFFFFFF && reset == 0xFFFFFFFF && nmi == 0xFFFFFFFF;
            if (blank || erased)
            {
                throw new IOException(chipType + " low ROM was not readable through the BROM memory command (vector probe returned blank data).");
            }
            if ((reset & 1) == 0)
            {
                addWarningLine(chipType + " ROM vector reset address does not have the Thumb bit set; continuing with the dump attempt.");
            }
        }

        byte[] ReadXrEfuse(int expectedLength, string targetKindName)
        {
            if (expectedLength != XR_EFUSE_BYTES)
            {
                throw new ArgumentOutOfRangeException("expectedLength", chipType + " eFuse dump length must be " + XR_EFUSE_BYTES + " bytes.");
            }

            logger.setState("Reading " + targetKindName + "...", Color.Transparent);
            logger.setProgress(0, expectedLength);
            addLogLine("Reading " + chipType + " eFuse via PhoenixMC BROM read32/write32 commands.");
            WriteXrMemory32(XR_EFUSE_TIMING_CTRL, XR_EFUSE_TIMING_24_26M);

            byte[] result = new byte[expectedLength];
            for (int word = 0; word < XR_EFUSE_WORDS; word++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint value = ReadXrEfuseWord(word);
                int offset = word * 4;
                WriteLe32(result, offset, value);
                logger.setProgress(offset + 4, expectedLength);
            }

            logger.setState(targetKindName + " read success!", Color.Green);
            return result;
        }

        byte[] ReadXrMemoryBlock(int address, int length)
        {
            if (length <= 0 || length > XR_ROM_READ_CHUNK_SIZE)
            {
                throw new ArgumentOutOfRangeException("length", chipType + " memory read chunk length is invalid.");
            }

            BROMResponse resp = ExecuteRawPacket(
                BuildReadMemoryPacket(address, length),
                headerTimeoutMs:  GetReadTimeoutMs(),
                payloadTimeoutMs: GetReadTimeoutMs(),
                sleepBeforeRead:  true);
            if (resp.IsError)
            {
                throw new IOException(chipType + " memory read command failed at " + formatHex(address) + ".");
            }
            if (resp.Payload == null || resp.Payload.Length != length)
            {
                throw new IOException(chipType + " memory read returned " + (resp.Payload == null ? 0 : resp.Payload.Length) +
                    " bytes, expected " + length + ".");
            }
            return resp.Payload;
        }

        uint ReadXrMemory32(int address)
        {
            BROMResponse resp = ExecuteRawPacket(
                BuildRead32Packet(address),
                headerTimeoutMs: 1500,
                payloadTimeoutMs: 1000);
            if (resp.IsError)
            {
                throw new IOException(chipType + " read32 command failed at " + formatHex(address) + ".");
            }
            if (resp.Payload == null || resp.Payload.Length < 4)
            {
                throw new IOException(chipType + " read32 returned " + (resp.Payload == null ? 0 : resp.Payload.Length) + " byte(s).");
            }
            return ReadLe32(resp.Payload, 0);
        }

        void WriteXrMemory32(int address, uint value)
        {
            BROMResponse resp = ExecuteRawPacket(
                BuildWrite32Packet(address, value),
                headerTimeoutMs: 1500,
                payloadTimeoutMs: 1000);
            if (resp.IsError)
            {
                throw new IOException(chipType + " write32 command failed at " + formatHex(address) + ".");
            }
        }

        uint ReadXrEfuseWord(int word)
        {
            int byteIndex = word * 4;
            uint controlBase = ReadXrMemory32(XR_EFUSE_CTRL);
            controlBase |= XR_EFUSE_CLK_GATE_EN;
            controlBase &= ~(XR_EFUSE_INDEX_MASK | XR_EFUSE_LOCK_MASK | XR_EFUSE_READ_START | XR_EFUSE_PROG_START);

            uint readControl = controlBase |
                (((uint)byteIndex << 16) & XR_EFUSE_INDEX_MASK) |
                XR_EFUSE_UNLOCK |
                XR_EFUSE_READ_START;

            try
            {
                WriteXrMemory32(XR_EFUSE_CTRL, controlBase);
                // Keep PROG_START clear; this path only asks the controller to latch one eFuse word.
                WriteXrMemory32(XR_EFUSE_CTRL, readControl);

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(XR_EFUSE_POLL_TIMEOUT_MS);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    uint status = ReadXrMemory32(XR_EFUSE_CTRL);
                    if ((status & (XR_EFUSE_READ_START | XR_EFUSE_HW_BUSY)) == 0)
                    {
                        return ReadXrMemory32(XR_EFUSE_READ_VALUE);
                    }
                    Thread.Sleep(1);
                }
                throw new TimeoutException("Timed out waiting for " + chipType + " eFuse word " + word + ".");
            }
            finally
            {
                try
                {
                    uint idleControl = controlBase & ~(XR_EFUSE_CLK_GATE_EN | XR_EFUSE_INDEX_MASK |
                        XR_EFUSE_LOCK_MASK | XR_EFUSE_READ_START | XR_EFUSE_PROG_START);
                    WriteXrMemory32(XR_EFUSE_CTRL, idleControl);
                }
                catch
                {
                }
            }
        }

        bool EraseChip()
        {
            byte[] pkt = BuildChipErasePacket();
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

            int headerTimeoutMs = Math.Max(500, GetReadWaitMs());

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
                            $"Erase ACK returned unexpected payload length {resp.PayloadLength}.");
                    }

                    if (resp.IsError)
                    {
                        addErrorLine($"Full-chip erase command returned an error. ACK: {FormatBromResponseForLog(resp)}");
                        return false;
                    }
                    return true;
                }
                catch
                {
                    if (attempt >= 10) throw;
                    Thread.Sleep(200);
                }
            }

            throw new IOException("Timed out waiting for full-chip erase ACK.");
        }

        bool EraseBlockOnce(int address, byte eraseMode)
        {
            byte[] pkt = BuildEraseBlockPacket(address, eraseMode);
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

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

                    if (resp.IsError)
                    {
                        addErrorLine($"Erase failed at 0x{address:X8}. ACK: {FormatBromResponseForLog(resp)}");
                        return false;
                    }
                    return true;
                }
                catch
                {
                    if (attempt >= 10) throw;
                    Thread.Sleep(sleepMs);
                }
            }

            throw new IOException($"Timed out waiting for erase ACK at 0x{address:X8}.");
        }

        byte[] ReadSectorsOnce(int sectorIndex, int sectorCount)
        {
            int expectedBytes = sectorCount * XR_SECTOR_SIZE;
            int payloadMs     = GetReadTimeoutMs();

            BROMResponse resp = ExecuteRawPacket(
                BuildReadPacket(sectorIndex, sectorCount),
                headerTimeoutMs:  GetReadTimeoutMs(),
                payloadTimeoutMs: payloadMs,
                sleepBeforeRead:  true);

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

        bool WriteSectorsOnce(int sectorIndex, byte[] data, int sectorCount)
        {
            int    dataLen      = sectorCount * XR_SECTOR_SIZE;
            ushort dataChecksum = ComputeChecksum(data, 0, dataLen);

            BROMResponse cmdAck = ExecuteRawPacket(
                BuildWritePacket(sectorIndex, sectorCount, dataChecksum),
                headerTimeoutMs:  3000,
                payloadTimeoutMs: 1000);
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

        bool InternalChipErase()
        {
            logger.setProgress(0, 1);
            logger.setState("Erasing...", Color.Transparent);
            addLogLine("Full-chip erase...");

            bool ok = EraseChip();
            if (!ok)
            {
                logger.setState("Erase failed", Color.Red);
                return false;
            }

            logger.setProgress(1, 1);
            logger.setState("Erase complete", Color.DarkGreen);
            return !isCancelled;
        }

        protected override bool PerformRangeErase(int startAddress, int length)
        {
            return InternalRangeErase(startAddress, length);
        }

        protected override bool PerformExplicitErase()
        {
            return InternalChipErase();
        }
    }
}
