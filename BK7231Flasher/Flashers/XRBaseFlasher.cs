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
    public abstract class XRBaseFlasher : BaseFlasher
    {
        protected const int XR_ROM_BAUD         = 115200;
        protected const int XR_SECTOR_SIZE      = 0x200;
        protected const int XR_ERASE_SECTOR_SIZE = 0x1000;
        protected const int XR_ERASE_BLOCK_SIZE  = 0x10000;
        protected const int XR_WRITE_CHUNK_SIZE = 0x4000;
        protected const int XR_READ_RETRY_COUNT  = 3;
        protected const int XR_WRITE_RETRY_COUNT = 2;
        protected const int XR_SYNC_RETRY_COUNT  = 10;
        protected const int XR_UPGRADE_SETTLE_MS = 120;

        protected const byte BROM_HOST_FLAGS   = 0x04;
        protected const byte BROM_HOST_QUERY   = 0x00;
        protected const byte BROM_HOST_PAYLOAD = 0x01;
        protected const byte BROM_FLAG_ERROR   = 0x01;

        protected MemoryStream readResult;
        protected byte[]       flashID;
        protected int          flashSizeBytes = 2 * 0x100000;
        protected byte         bromVersion    = 0x00;

        protected struct BROMResponse
        {
            public byte   Flags;
            public byte   BromVersion;
            public ushort Checksum;
            public int    PayloadLength;
            public byte[] Payload;
            public bool   IsError => (Flags & BROM_FLAG_ERROR) != 0;
        }

        protected struct XRSectionHeader
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

        protected XRBaseFlasher(CancellationToken ct) : base(ct)
        {
        }

        protected virtual string XRChipName
        {
            get { return chipType.ToString(); }
        }

        protected virtual int XRMaxBromPayload
        {
            get { return XR_WRITE_CHUNK_SIZE; }
        }

        protected abstract Dictionary<uint, string> SectionNames { get; }

        protected abstract bool EnsureConnectedAndIdentified();
        protected abstract byte[] ReadSectors(int sectorIndex, int sectorCount);
        protected abstract bool WriteSectors(int sectorIndex, byte[] data, int sectorCount);
        protected abstract bool PerformRangeErase(int startAddress, int length);
        protected abstract bool PerformExplicitErase();
        protected abstract byte[] BuildChangeBaudPacket(int newBaud);

        protected virtual string FormatHeaderIncompleteMessage(byte[] header)
        {
            return $"BROM header incomplete ({header?.Length ?? 0}/12 bytes).";
        }

        protected int GetReadWaitMs()
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

        protected int GetReadTimeoutMs()
        {
            int waitMs = GetReadWaitMs();
            if (waitMs == int.MaxValue)
                return int.MaxValue;
            int total = waitMs * 40;
            return total < 1000 ? 1000 : total;
        }

        protected static int PublicSectorIndexToByteAddress(int startSector)
        {
            return startSector * BK7231Flasher.SECTOR_SIZE;
        }

        protected static int PublicSectorCountToByteLength(int sectors)
        {
            return sectors * BK7231Flasher.SECTOR_SIZE;
        }

        protected bool IsCustomRawWriteRequest(int startSector, int sectors, WriteMode rwMode)
        {
            if (rwMode != WriteMode.OnlyWrite)
                return false;

            int fullFlashFrameworkSectors = BK7231Flasher.FLASH_SIZE / BK7231Flasher.SECTOR_SIZE;
            return startSector != 0 || sectors != fullFlashFrameworkSectors;
        }

        protected static bool StartsWithAwihMagic(byte[] data)
        {
            return data != null && data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0x48495741;
        }

        protected static ushort ComputeChecksum(byte[] data, int offset, int count)
        {
            ushort sum = 0;
            int i = 0;
            for (; i + 1 < count; i += 2)
                sum += (ushort)(data[offset + i] | (data[offset + i + 1] << 8));
            if ((count & 1) != 0)
                sum += data[offset + count - 1];
            return (ushort)(~sum);
        }

        protected byte[] BuildBromPacket(byte opcode, byte[] payloadPre, byte[] payloadWire, byte requestType = BROM_HOST_QUERY)
        {
            if (payloadPre == null) payloadPre = new byte[0];
            if (payloadWire == null) payloadWire = new byte[0];

            int logicalLen = 1 + payloadPre.Length;

            byte[] pre = new byte[12 + logicalLen];
            pre[0] = (byte)'B'; pre[1] = (byte)'R'; pre[2] = (byte)'O'; pre[3] = (byte)'M';
            pre[4] = BROM_HOST_FLAGS;
            pre[5] = requestType;
            pre[8]  = (byte)( logicalLen        & 0xFF);
            pre[9]  = (byte)((logicalLen >>  8) & 0xFF);
            pre[10] = (byte)((logicalLen >> 16) & 0xFF);
            pre[11] = (byte)((logicalLen >> 24) & 0xFF);
            pre[12] = opcode;
            if (payloadPre.Length > 0)
                Buffer.BlockCopy(payloadPre, 0, pre, 13, payloadPre.Length);

            ushort cs = ComputeChecksum(pre, 0, pre.Length);

            byte[] pkt = new byte[12 + logicalLen];
            pkt[0] = (byte)'B'; pkt[1] = (byte)'R'; pkt[2] = (byte)'O'; pkt[3] = (byte)'M';
            pkt[4] = BROM_HOST_FLAGS;
            pkt[5] = requestType;
            pkt[6]  = (byte)((cs >> 8) & 0xFF);
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

        protected static byte[] BuildLegacyGetFlashIdPacket()
        {
            return new byte[]
            {
                0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51,
                0x00, 0x00, 0x00, 0x01, 0x18
            };
        }

        protected static byte[] BuildChipTypePacket()
        {
            return new byte[]
            {
                0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x17
            };
        }

        protected bool DoGenericSetup()
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
                catch
                {
                }
            }
            base.closePort();
        }

        protected void DrainInput(int quietMs = 150, int hardLimitMs = 750)
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

        protected static string FormatHexSnippet(byte[] data, int maxBytes = 16)
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

        protected byte[] ReadExact(int count, int timeoutMs)
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

        protected void TryEnterDownloadModeByUpgradeCommand()
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

                addLogLine($"Attempting {XRChipName} software upgrade command before sync...");
                DrainInput(quietMs: 40, hardLimitMs: 120);

                serial.Write(new byte[] { 0x0A }, 0, 1);
                byte[] cmd = Encoding.ASCII.GetBytes("upgrade");
                serial.Write(cmd, 0, cmd.Length);
                serial.Write(new byte[] { 0x0A, 0x00 }, 0, 2);
                serial.BaseStream.Flush();
                Thread.Sleep(XR_UPGRADE_SETTLE_MS);

                DrainInput(quietMs: 40, hardLimitMs: 200);
            }
            catch (Exception ex)
            {
                addLogLine("Upgrade pre-sync command did not complete: " + ex.Message);
            }
        }

        protected bool Sync()
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

        protected BROMResponse ReadBromResponse(int headerTimeoutMs, int payloadTimeoutMs)
        {
            byte[] header = ReadExact(12, headerTimeoutMs);
            if (header == null || header.Length < 12)
                throw new IOException(FormatHeaderIncompleteMessage(header));
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
            if (resp.PayloadLength > XRMaxBromPayload)
                throw new IOException(
                    $"BROM payload length {resp.PayloadLength} exceeds {XRChipName} transport ceiling {XRMaxBromPayload}.");

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

        protected BROMResponse ReadFixedHeaderResponse(int headerTimeoutMs)
        {
            byte[] header = ReadExact(12, headerTimeoutMs);
            if (header == null || header.Length < 12)
                throw new IOException(FormatHeaderIncompleteMessage(header));
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

        protected BROMResponse ExecuteRawPacket(
            byte[] pkt,
            int    headerTimeoutMs  = 1500,
            int    payloadTimeoutMs = 3000,
            bool   sleepBeforeRead  = false)
        {
            serial.Write(pkt, 0, pkt.Length);
            serial.BaseStream.Flush();

            if (sleepBeforeRead)
                Thread.Sleep(50);

            return ReadBromResponse(headerTimeoutMs, payloadTimeoutMs);
        }

        protected void ReadChipType()
        {
            try
            {
                BROMResponse resp = ExecuteRawPacket(
                    BuildChipTypePacket(),
                    headerTimeoutMs: 1500,
                    payloadTimeoutMs: 1500);
                if (!resp.IsError && resp.PayloadLength >= 1)
                    addLogLine($"Chip type    : {resp.Payload[0]}");
            }
            catch
            {
            }
        }

        protected bool TrySetFlashIdFromResponse(BROMResponse resp)
        {
            bromVersion = resp.BromVersion;

            if (resp.IsError)
            {
                addErrorLine("GetFlashId returned error.");
                return false;
            }
            if (resp.PayloadLength < 3)
            {
                addErrorLine($"GetFlashId: payload too short ({resp.PayloadLength} bytes).");
                return false;
            }

            flashID = resp.Payload;
            addLogLine($"BROM version : {bromVersion}");
            addLogLine($"Flash ID     : {flashID[0]:X2} {flashID[1]:X2} {flashID[2]:X2}");

            if (flashID.Length >= 3 && flashID[2] >= 0x11 && flashID[2] <= 0x20)
            {
                flashSizeBytes = (1 << (flashID[2] - 0x11)) * 0x20000;
                addLogLine($"Flash size   : {flashSizeBytes / 0x100000} MB");
            }
            else
            {
                addWarningLine("Could not decode flash size from JEDEC ID; defaulting to 2 MB.");
            }
            return true;
        }

        protected byte[] InternalRead(int startAddress, int length)
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

        protected bool InternalWrite(int startAddress, byte[] data)
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

        protected XRSectionHeader ParseSectionHeader(byte[] img, int offset)
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

        protected static bool ValidateHeaderChecksum(byte[] img, int offset)
        {
            ushort sum = 0;
            for (int i = 0; i < 0x40; i += 2)
                sum += (ushort)(img[offset + i] | (img[offset + i + 1] << 8));
            return sum == 0xFFFF;
        }

        protected static bool ValidateDataChecksum(byte[] img, int dataOffset, int size, ushort stored)
        {
            ushort sum = stored;
            int i = 0;
            for (; i + 1 < size; i += 2)
                sum += (ushort)(img[dataOffset + i] | (img[dataOffset + i + 1] << 8));
            if ((size & 1) != 0)
                sum += img[dataOffset + size - 1];
            return sum == 0xFFFF;
        }

        protected int ValidateAndLogImgLayout(byte[] img, bool logLayout = true)
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
                    string name = SectionNames.TryGetValue(hdr.SectionId, out string n)
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

        public override byte[] getReadResult()
        {
            return readResult?.ToArray();
        }

        public override void doWrite(int startSector, byte[] data)
        {
            addErrorLine($"{XRChipName}: raw sector write via doWrite() is not supported on this chip.");
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

                bool ok;
                if (bAll)
                {
                    addLogLine($"Full erase requested: {flashSizeBytes / 0x100000} MB");
                    ok = PerformExplicitErase();
                }
                else
                {
                    int requestedStartAddr = PublicSectorIndexToByteAddress(startSector);
                    int requestedLength    = PublicSectorCountToByteLength(sectors);
                    addLogLine($"Range erase requested: 0x{requestedStartAddr:X6} + 0x{requestedLength:X6} bytes.");
                    ok = PerformRangeErase(requestedStartAddr, requestedLength);
                }
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
            try
            {
                if (!DoGenericSetup())               return;
                if (!EnsureConnectedAndIdentified()) return;

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
                OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();

                if (rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
                {
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
                        addLogLine($"Custom write: raw bytes at selected destination 0x{writeAddress:X6}.");
                        if (ext != ".bin")
                            addWarningLine($"Custom write ignores extension \"{ext}\" and writes raw bytes as selected.");
                    }
                    else if (ext == ".img")
                    {
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

                    if (customRawWrite)
                    {
                        addLogLine($"{XRChipName} custom write erases only the 4 KB sectors covered by the raw data.");
                        addLogLine($"Post-erase custom write destination: 0x{writeAddress:X6}");
                        if (!PerformRangeErase(writeAddress, effectiveLen)) return;
                        if (isCancelled) return;
                    }
                    else
                    {
                        addLogLine($"{XRChipName} write path erases only the 4 KB sectors covered by the write.");
                        addLogLine($"Post-erase write destination: 0x{writeAddress:X6}");
                        if (!PerformRangeErase(writeAddress, effectiveLen)) return;
                        if (isCancelled) return;

                        if (treatAsXRImage)
                        {
                            addLogLine("Image layout:");
                            ValidateAndLogImgLayout(fileData, logLayout: true);
                        }
                    }

                    addLogLine(customRawWrite ? "Writing raw custom data..." : "Writing firmware...");
                    if (!InternalWrite(writeAddress, toWrite)) return;

                    addSuccess($"{XRChipName} flash write complete!" + Environment.NewLine);
                }
                
                if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && !isCancelled)
                {
                    if(cfg != null)
                    {
                        var offset = OBKFlashLayout.getConfigLocation(chipType, out var cfgSectors);
                        var areaSize = cfgSectors * BK7231Flasher.SECTOR_SIZE;

                        cfg.saveConfig(chipType);
                        var cfgData = cfg.getData();
                        byte[] efdata;
                        if(cfg.efdata != null)
                        {
                            try
                            {
                                efdata = EasyFlash.SaveValueToExistingEasyFlash("ObkCfg", cfg.efdata, cfgData, areaSize, chipType);
                            }
                            catch(Exception ex)
                            {
                                addLogLine("Saving config to existing EasyFlash failed");
                                addLogLine(ex.Message);
                                efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                            }
                        }
                        else
                        {
                            efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                        }
                        if(efdata == null)
                        {
                            throw new Exception("Something went wrong with EasyFlash");
                        }
                        addLogLine("Now will also write OBK config...");
                        addLogLine($"Long name from CFG: {cfg.longDeviceName}");
                        addLogLine($"Short name from CFG: {cfg.shortDeviceName}");
                        addLogLine($"Web Root from CFG: {cfg.webappRoot}");
                        addLogLine($"Erasing OBK config area 0x{offset:X6} + 0x{efdata.Length:X} bytes...");
                        if (!PerformRangeErase(offset, efdata.Length))
                        {
                            logger.setState("Erase error!", Color.Red);
                            addErrorLine("Erase failed! OBK config was not written.");
                            return;
                        }
                        if (isCancelled) return;

                        addLogLine($"Writing OBK config area 0x{offset:X6}...");
                        bool bOk = InternalWrite(offset, efdata);
                        if(bOk == false)
                        {
                            logger.setState("Write error!", Color.Red);
                            throw new Exception("Writing OBK config data to chip failed!");
                        }
                        logger.setState("OBK config write success!", Color.Green);
                    }
                    else
                    {
                        addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
                    }
                }
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
