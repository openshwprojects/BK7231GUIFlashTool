using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace BK7231Flasher
{
    public abstract class XRRomFlasherBase : XRBaseFlasher
    {
        const byte OP_CHANGE_BAUD = 0x10;
        const byte OP_ERASE       = 0x19;
        const byte OP_READ        = 0x1A;
        const byte OP_WRITE       = 0x1B;

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
