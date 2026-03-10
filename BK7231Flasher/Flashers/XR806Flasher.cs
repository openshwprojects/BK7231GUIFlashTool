using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
    public class XR806Flasher : BaseFlasher
    {
        const int XR_ROM_BAUD = 115200;
        const int XR_SECTOR_SIZE = 0x200;
        const int XR_ERASE_BLOCK_SIZE = 0x10000;
        const int XR_WRITE_CHUNK_SIZE = 0x4000;
        const int XR_WRITE_CHUNK_SECTORS = XR_WRITE_CHUNK_SIZE / XR_SECTOR_SIZE;
        static readonly byte[] BROM_MAGIC = new byte[] { (byte)'B', (byte)'R', (byte)'O', (byte)'M' };
        static readonly byte[] SYNC_OK = new byte[] { (byte)'O', (byte)'K' };
        static readonly byte[] GET_FLASH_ID_REQUEST = new byte[] { 0x42, 0x52, 0x4F, 0x4D, 0x04, 0x00, 0x60, 0x51, 0x00, 0x00, 0x00, 0x01, 0x18 };

        MemoryStream ms;
        byte[] flashID;
        int flashSizeMB = 2;
        byte bromVersion = 0xFF;

        struct BROMResponse
        {
            public byte Flags;
            public byte BromVersion;
            public ushort Checksum;
            public int PayloadLength;
            public byte[] Payload;
        }

        struct XRImageSectionHeader
        {
            public uint Magic;
            public uint Version;
            public ushort HeaderChecksum;
            public ushort DataChecksum;
            public uint DataSize;
            public uint LoadAddr;
            public uint Entry;
            public uint BodyLen;
            public uint Attribute;
            public uint NextAddr;
            public uint SectionId;
            public uint[] Priv;
        }

        public XR806Flasher(CancellationToken ct) : base(ct)
        {
        }

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            try
            {
                serial = new SerialPort(serialName, XR_ROM_BAUD);
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                serial.ReadTimeout = 2000;
                serial.WriteTimeout = 5000;
            }
            catch(Exception ex)
            {
                addLog("Port setup failed with " + ex.Message + "!" + Environment.NewLine);
                return false;
            }
            addLog("Port ready!" + Environment.NewLine);
            return true;
        }

        void DrainInput(int quietMillis = 150, int hardLimitMillis = 750)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch quiet = Stopwatch.StartNew();
            while(total.ElapsedMilliseconds < hardLimitMillis)
            {
                int waiting = serial.BytesToRead;
                if(waiting > 0)
                {
                    var dump = new byte[waiting];
                    serial.Read(dump, 0, dump.Length);
                    quiet.Restart();
                    continue;
                }
                if(quiet.ElapsedMilliseconds >= quietMillis)
                    break;
                Thread.Sleep(10);
            }
        }

        bool Sync()
        {
            DrainInput();
            serial.Write(new byte[] { 0x55 }, 0, 1);
            Thread.Sleep(20);
            var reply = new byte[2];
            int got = 0;
            Stopwatch sw = Stopwatch.StartNew();
            while(sw.ElapsedMilliseconds < 1500 && got < 2)
            {
                try
                {
                    got += serial.Read(reply, got, 2 - got);
                }
                catch(TimeoutException)
                {
                }
            }
            bool ok = got == 2 && reply[0] == SYNC_OK[0] && reply[1] == SYNC_OK[1];
            if(ok)
            {
                addLogLine("Sync success!");
            }
            else
            {
                addErrorLine($"Sync failed, got {got} byte(s)");
            }
            DrainInput();
            return ok;
        }

        static ushort ComputeBromChecksum(byte[] data, int offset, int count)
        {
            uint sum = 0;
            int i = 0;
            while(i < count)
            {
                byte lo = data[offset + i];
                byte hi = 0xFF;
                if(i + 1 < count)
                    hi = data[offset + i + 1];
                sum += (uint)(lo | (hi << 8));
                sum = (sum & 0xFFFF) + (sum >> 16);
                i += 2;
            }
            sum = (sum & 0xFFFF) + (sum >> 16);
            return (ushort)(~sum & 0xFFFF);
        }

        static ushort ComputeDataChecksum(byte[] data, int offset, int count)
        {
            return ComputeBromChecksum(data, offset, count);
        }

        byte[] BuildBromPacket(byte opcode, byte[] payload)
        {
            payload = payload ?? new byte[0];
            int logicalLength = 1 + payload.Length;
            byte[] packet = new byte[12 + logicalLength];
            Array.Copy(BROM_MAGIC, 0, packet, 0, 4);
            packet[4] = 0x04;
            packet[5] = 0x00;
            packet[8] = (byte)((logicalLength >> 24) & 0xFF);
            packet[9] = (byte)((logicalLength >> 16) & 0xFF);
            packet[10] = (byte)((logicalLength >> 8) & 0xFF);
            packet[11] = (byte)(logicalLength & 0xFF);
            packet[12] = opcode;
            if(payload.Length > 0)
                Array.Copy(payload, 0, packet, 13, payload.Length);
            ushort checksum = ComputeBromChecksum(packet, 0, packet.Length);
            packet[6] = (byte)((checksum >> 8) & 0xFF);
            packet[7] = (byte)(checksum & 0xFF);
            return packet;
        }

        BROMResponse ReadBromResponse(int headerTimeoutMillis, int payloadTimeoutMillis)
        {
            byte[] header = ReadExact(12, headerTimeoutMillis);
            if(header == null || header.Length != 12)
                throw new IOException("BROM response header is missing or incomplete");
            if(header[0] != 'B' || header[1] != 'R' || header[2] != 'O' || header[3] != 'M')
                throw new IOException("BROM response magic is invalid");
            int payloadLength = (header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11];
            byte[] payload = new byte[0];
            if(payloadLength > 0)
            {
                payload = ReadExact(payloadLength, payloadTimeoutMillis);
                if(payload == null || payload.Length != payloadLength)
                    throw new IOException("BROM response payload is incomplete");
            }
            BROMResponse resp = new BROMResponse();
            resp.Flags = header[4];
            resp.BromVersion = header[5];
            resp.Checksum = (ushort)((header[6] << 8) | header[7]);
            resp.PayloadLength = payloadLength;
            resp.Payload = payload;
            return resp;
        }

        byte[] ReadExact(int count, int timeoutMillis)
        {
            byte[] ret = new byte[count];
            int got = 0;
            Stopwatch sw = Stopwatch.StartNew();
            while(got < count && sw.ElapsedMilliseconds < timeoutMillis)
            {
                try
                {
                    int r = serial.Read(ret, got, count - got);
                    if(r > 0)
                    {
                        got += r;
                        continue;
                    }
                }
                catch(TimeoutException)
                {
                }
                Thread.Sleep(10);
            }
            if(got == count)
                return ret;
            if(got <= 0)
                return null;
            byte[] partial = new byte[got];
            Array.Copy(ret, 0, partial, 0, got);
            return partial;
        }

        BROMResponse ExecuteBromCommand(byte opcode, byte[] payload, int headerTimeoutMillis = 1500, int payloadTimeoutMillis = 3000)
        {
            byte[] packet = opcode == 0x18 && (payload == null || payload.Length == 0) ? GET_FLASH_ID_REQUEST : BuildBromPacket(opcode, payload);
            serial.Write(packet, 0, packet.Length);
            serial.BaseStream.Flush();
            if(opcode == 0x1A)
                Thread.Sleep(50);
            return ReadBromResponse(headerTimeoutMillis, payloadTimeoutMillis);
        }

        public byte[] ReadFlashId()
        {
            BROMResponse resp = ExecuteBromCommand(0x18, null, 1500, 1500);
            bromVersion = resp.BromVersion;
            if(resp.PayloadLength < 3)
            {
                addErrorLine("Flash ID response payload is too short.");
                return null;
            }
            flashID = resp.Payload;
            addLogLine($"BROM version: {bromVersion}");
            addLogLine($"Flash ID: 0x{flashID[0]:X2}{flashID[1]:X2}{flashID[2]:X2}");
            if(flashID.Length >= 3 && flashID[2] >= 0x11 && flashID[2] <= 0x20)
            {
                flashSizeMB = (1 << (flashID[2] - 0x11)) / 8;
                addLogLine($"Flash size is {flashSizeMB}MB");
            }
            return flashID;
        }

        bool EnsureConnectedAndIdentified()
        {
            if(!Sync())
                return false;
            if(ReadFlashId() == null)
                return false;
            if(bromVersion != 0x03)
            {
                addWarningLine($"Unexpected BROM version 0x{bromVersion:X2} for XR806 path.");
            }
            if(baudrate != XR_ROM_BAUD)
            {
                addWarningLine($"XR806 support currently stays at {XR_ROM_BAUD} baud; requested {baudrate} is ignored for now.");
            }
            return true;
        }

        bool Erase64KBlock(int address)
        {
            byte[] payload = new byte[5];
            payload[0] = (byte)((address >> 24) & 0xFF);
            payload[1] = (byte)((address >> 16) & 0xFF);
            payload[2] = (byte)((address >> 8) & 0xFF);
            payload[3] = (byte)(address & 0xFF);
            payload[4] = 0x01;
            BROMResponse resp = ExecuteBromCommand(0x19, payload, 4000, 1000);
            return resp.PayloadLength == 0 || resp.Payload != null;
        }

        byte[] ReadSectors(int sectorIndex, int sectorCount)
        {
            byte[] payload = new byte[8];
            payload[0] = (byte)((sectorIndex >> 24) & 0xFF);
            payload[1] = (byte)((sectorIndex >> 16) & 0xFF);
            payload[2] = (byte)((sectorIndex >> 8) & 0xFF);
            payload[3] = (byte)(sectorIndex & 0xFF);
            payload[4] = (byte)((sectorCount >> 24) & 0xFF);
            payload[5] = (byte)((sectorCount >> 16) & 0xFF);
            payload[6] = (byte)((sectorCount >> 8) & 0xFF);
            payload[7] = (byte)(sectorCount & 0xFF);
            BROMResponse resp = ExecuteBromCommand(0x1A, payload, 4000, Math.Max(4000, sectorCount * 50));
            return resp.Payload;
        }

        bool WriteSectors(int sectorIndex, byte[] data, int sectorCount)
        {
            ushort dataChecksum = ComputeDataChecksum(data, 0, sectorCount * XR_SECTOR_SIZE);
            byte[] payload = new byte[10];
            payload[0] = (byte)((sectorIndex >> 24) & 0xFF);
            payload[1] = (byte)((sectorIndex >> 16) & 0xFF);
            payload[2] = (byte)((sectorIndex >> 8) & 0xFF);
            payload[3] = (byte)(sectorIndex & 0xFF);
            payload[4] = (byte)((sectorCount >> 24) & 0xFF);
            payload[5] = (byte)((sectorCount >> 16) & 0xFF);
            payload[6] = (byte)((sectorCount >> 8) & 0xFF);
            payload[7] = (byte)(sectorCount & 0xFF);
            payload[8] = (byte)((dataChecksum >> 8) & 0xFF);
            payload[9] = (byte)(dataChecksum & 0xFF);
            ExecuteBromCommand(0x1B, payload, 3000, 1000);
            serial.Write(data, 0, sectorCount * XR_SECTOR_SIZE);
            serial.BaseStream.Flush();
            BROMResponse finalResp = ReadBromResponse(5000, 1000);
            return finalResp.PayloadLength == 0 || finalResp.Payload != null;
        }

        byte[] InternalRead(int startAddress, int length)
        {
            byte[] result = new byte[length];
            int done = 0;
            int totalSectors = (length + XR_SECTOR_SIZE - 1) / XR_SECTOR_SIZE;
            int sectorIndex = startAddress / XR_SECTOR_SIZE;
            logger.setProgress(0, totalSectors);
            logger.setState("Reading...", Color.Transparent);
            while(done < length && !isCancelled)
            {
                int remaining = length - done;
                int readBytes = Math.Min(XR_WRITE_CHUNK_SIZE, remaining);
                int sectorCount = (readBytes + XR_SECTOR_SIZE - 1) / XR_SECTOR_SIZE;
                addLogLine($"Read at 0x{startAddress + done:X6}, sectors {sectorCount}");
                byte[] chunk = ReadSectors(sectorIndex, sectorCount);
                if(chunk == null || chunk.Length < sectorCount * XR_SECTOR_SIZE)
                    throw new IOException("XR806 read command returned incomplete data");
                Array.Copy(chunk, 0, result, done, Math.Min(readBytes, chunk.Length));
                done += readBytes;
                sectorIndex += sectorCount;
                logger.setProgress(Math.Min(totalSectors, sectorIndex - startAddress / XR_SECTOR_SIZE), totalSectors);
            }
            logger.setState("Reading done", Color.DarkGreen);
            return result;
        }

        bool InternalEraseRange(int startAddress, int length)
        {
            int eraseStart = startAddress & ~(XR_ERASE_BLOCK_SIZE - 1);
            int eraseEnd = (startAddress + length + XR_ERASE_BLOCK_SIZE - 1) & ~(XR_ERASE_BLOCK_SIZE - 1);
            int total = (eraseEnd - eraseStart) / XR_ERASE_BLOCK_SIZE;
            int done = 0;
            logger.setProgress(0, total);
            logger.setState("Erasing...", Color.Transparent);
            for(int address = eraseStart; address < eraseEnd; address += XR_ERASE_BLOCK_SIZE)
            {
                if(isCancelled)
                    return false;
                addLogLine($"Erasing 64K block at 0x{address:X6}");
                if(!Erase64KBlock(address))
                {
                    addErrorLine($"Erase failed at 0x{address:X6}");
                    logger.setState("Erase failed", Color.Red);
                    return false;
                }
                done++;
                logger.setProgress(done, total);
            }
            logger.setState("Erase done", Color.DarkGreen);
            return true;
        }

        bool InternalWrite(int startAddress, byte[] data)
        {
            int totalChunks = (data.Length + XR_WRITE_CHUNK_SIZE - 1) / XR_WRITE_CHUNK_SIZE;
            logger.setProgress(0, totalChunks);
            logger.setState("Writing...", Color.Transparent);
            int offset = 0;
            while(offset < data.Length && !isCancelled)
            {
                int chunkBytes = Math.Min(XR_WRITE_CHUNK_SIZE, data.Length - offset);
                byte[] chunk = MiscUtils.subArray(data, offset, chunkBytes);
                chunk = MiscUtils.padArray(chunk, XR_SECTOR_SIZE);
                int sectorIndex = (startAddress + offset) / XR_SECTOR_SIZE;
                int sectorCount = chunk.Length / XR_SECTOR_SIZE;
                addLogLine($"Write at 0x{startAddress + offset:X6}, bytes 0x{chunkBytes:X}");
                if(!WriteSectors(sectorIndex, chunk, sectorCount))
                {
                    addErrorLine($"Write failed at 0x{startAddress + offset:X6}");
                    logger.setState("Write failed", Color.Red);
                    return false;
                }
                offset += chunkBytes;
                logger.setProgress((offset + XR_WRITE_CHUNK_SIZE - 1) / XR_WRITE_CHUNK_SIZE, totalChunks);
            }
            logger.setState("Writing done", Color.DarkGreen);
            return !isCancelled;
        }

        static ushort ComputeXRImageChecksum16(byte[] data, int offset, int count)
        {
            uint sum = 0;
            int i = 0;
            while(i < count)
            {
                byte lo = data[offset + i];
                byte hi = 0;
                if(i + 1 < count)
                    hi = data[offset + i + 1];
                sum += (uint)(lo | (hi << 8));
                sum &= 0xFFFF;
                i += 2;
            }
            return (ushort)sum;
        }

        static XRImageSectionHeader ParseImageSectionHeader(byte[] image, int offset)
        {
            XRImageSectionHeader h = new XRImageSectionHeader();
            h.Magic = BitConverter.ToUInt32(image, offset + 0x00);
            h.Version = BitConverter.ToUInt32(image, offset + 0x04);
            h.HeaderChecksum = BitConverter.ToUInt16(image, offset + 0x08);
            h.DataChecksum = BitConverter.ToUInt16(image, offset + 0x0A);
            h.DataSize = BitConverter.ToUInt32(image, offset + 0x0C);
            h.LoadAddr = BitConverter.ToUInt32(image, offset + 0x10);
            h.Entry = BitConverter.ToUInt32(image, offset + 0x14);
            h.BodyLen = BitConverter.ToUInt32(image, offset + 0x18);
            h.Attribute = BitConverter.ToUInt32(image, offset + 0x1C);
            h.NextAddr = BitConverter.ToUInt32(image, offset + 0x20);
            h.SectionId = BitConverter.ToUInt32(image, offset + 0x24);
            h.Priv = new uint[6];
            for(int i = 0; i < 6; i++)
                h.Priv[i] = BitConverter.ToUInt32(image, offset + 0x28 + i * 4);
            return h;
        }

        static bool ValidateXRImageHeader(byte[] image, int offset)
        {
            return ComputeXRImageChecksum16(image, offset, 0x40) == 0xFFFF;
        }

        static bool ValidateXRImageData(byte[] image, int dataOffset, int size, ushort initialChecksum)
        {
            byte[] tmp = new byte[size + 2];
            Array.Copy(image, dataOffset, tmp, 0, size);
            tmp[size + 0] = (byte)(initialChecksum & 0xFF);
            tmp[size + 1] = (byte)((initialChecksum >> 8) & 0xFF);
            return ComputeXRImageChecksum16(tmp, 0, tmp.Length) == 0xFFFF;
        }

        int GetXRImageEffectiveLength(byte[] image)
        {
            int offset = 0;
            int loops = 0;
            while(true)
            {
                if(offset < 0 || offset + 0x40 > image.Length)
                    throw new InvalidDataException("XR image section header is out of range");
                XRImageSectionHeader hdr = ParseImageSectionHeader(image, offset);
                if(hdr.Magic != 0x48495741)
                    throw new InvalidDataException($"XR image header magic mismatch at 0x{offset:X}");
                if(!ValidateXRImageHeader(image, offset))
                    throw new InvalidDataException($"XR image header checksum mismatch at 0x{offset:X}");
                int dataOffset = offset + 0x40;
                if(dataOffset + hdr.DataSize > image.Length)
                    throw new InvalidDataException($"XR image data exceeds file size at 0x{offset:X}");
                if(!ValidateXRImageData(image, dataOffset, (int)hdr.DataSize, hdr.DataChecksum))
                    throw new InvalidDataException($"XR image data checksum mismatch at 0x{offset:X}");
                loops++;
                if(loops > 100)
                    throw new InvalidDataException("XR image contains too many sections");
                if(hdr.NextAddr == 0xFFFFFFFF)
                    return dataOffset + (int)hdr.DataSize;
                offset = (int)hdr.NextAddr;
            }
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            try
            {
                if(doGenericSetup() == false)
                    return;
                if(!EnsureConnectedAndIdentified())
                    return;
                int startAddress = startSector;
                int length = sectors * BK7231Flasher.SECTOR_SIZE;
                if(fullRead)
                {
                    length = flashSizeMB * 0x100000;
                    startAddress = 0;
                    addLogLine($"Reading full XR806 flash ({flashSizeMB}MB)...");
                }
                byte[] res = InternalRead(startAddress, length);
                ms = new MemoryStream(res);
                saveReadResult(startAddress);
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
            }
        }

        public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
        {
            try
            {
                if(doGenericSetup() == false)
                    return false;
                if(!EnsureConnectedAndIdentified())
                    return false;
                int startAddress = 0;
                int length = flashSizeMB * 0x100000;
                if(!bAll)
                {
                    startAddress = startSector;
                    length = sectors * BK7231Flasher.SECTOR_SIZE;
                }
                return InternalEraseRange(startAddress, length);
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
                return false;
            }
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if(rwMode == WriteMode.OnlyOBKConfig)
            {
                addErrorLine("XR806 support does not yet implement standalone OBK config writes.");
                return;
            }
            try
            {
                if(doGenericSetup() == false)
                    return;
                if(!EnsureConnectedAndIdentified())
                    return;
                if(rwMode == WriteMode.ReadAndWrite)
                {
                    sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Flash size detected: {flashSizeMB}MB");
                    byte[] res = InternalRead(0, flashSizeMB * 0x100000);
                    ms = new MemoryStream(res);
                    if(!saveReadResult(0))
                        return;
                }
                if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite) && !isCancelled)
                {
                    if(string.IsNullOrEmpty(sourceFileName))
                    {
                        addErrorLine("No filename given!");
                        return;
                    }
                    if(!File.Exists(sourceFileName))
                    {
                        addErrorLine("Source file does not exist!");
                        return;
                    }
                    addLogLine("Reading " + sourceFileName + "...");
                    byte[] data = File.ReadAllBytes(sourceFileName);
                    int effectiveLength = data.Length;
                    int writeAddress = startSector;
                    if(Path.GetExtension(sourceFileName).Equals(".img", StringComparison.OrdinalIgnoreCase))
                    {
                        effectiveLength = GetXRImageEffectiveLength(data);
                        addLogLine($"Validated XR image, effective length 0x{effectiveLength:X}");
                        writeAddress = 0;
                    }
                    else
                    {
                        addWarningLine("Non-.img write requested; using the raw file length and caller-provided start address.");
                    }
                    if(effectiveLength > flashSizeMB * 0x100000)
                    {
                        addErrorLine("Image is larger than detected flash size.");
                        return;
                    }
                    byte[] toWrite = new byte[effectiveLength];
                    Array.Copy(data, 0, toWrite, 0, effectiveLength);
                    if(!InternalEraseRange(writeAddress, effectiveLength))
                        return;
                    if(!InternalWrite(writeAddress, toWrite))
                        return;
                }
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
            }
        }

        bool saveReadResult(string fileName)
        {
            if(ms == null)
            {
                addError("There was no result to save." + Environment.NewLine);
                return false;
            }
            byte[] dat = ms.ToArray();
            string fullPath = "backups/" + fileName;
            File.WriteAllBytes(fullPath, dat);
            addSuccess("Wrote " + dat.Length + " to " + fileName + Environment.NewLine);
            logger.onReadResultQIOSaved(dat, "", fullPath);
            return true;
        }

        public override bool saveReadResult(int startOffset)
        {
            string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType, backupName, "bin");
            return saveReadResult(fileName);
        }
    }
}
