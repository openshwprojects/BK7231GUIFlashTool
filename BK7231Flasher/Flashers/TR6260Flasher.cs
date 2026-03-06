using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace BK7231Flasher
{
    public class TR6260Flasher : BaseFlasher
    {
        const int DEFAULT_BAUD = 57600;
        const int DEFAULT_FLASH_SIZE = 0x100000;
        const int FLASH_BLOCK = 1024;
        const int PARTITION_ADDR = 0x6000;
        const int APP_ADDR = 0x7000;
        const int APP2_ADDR = 0x80000;
        const int RAM_ADDR = 0x10000;
        const uint TRS_SYNC = 0x73796E63;

        const byte TRS_ROM_SYNC_ACK = 1;
        const byte TRS_UBOOT_SYNC_ACK = 2;
        const byte TRS_ROM_BAUD_ACK = 3;
        const byte TRS_ROM_FILE_ACK = 0;
        const byte TRS_PARTITION_MISSING = 234;
        const byte TRS_PARTITION_ERROR = 235;

        const uint TRS_FRM_TYPE_UBOOT = 1;
        const uint TRS_FRM_TYPE_NV = 2;
        const uint TRS_FRM_TYPE_APP = 3;
        const uint TRS_FRM_TYPE_APP_2 = 4;
        const uint TRS_FRM_TYPE_RAM = 5;
        const uint TRS_FRM_TYPE_ERASE = 6;
        const uint TRS_FRM_TYPE_UPLOAD = 8;

        static readonly int[] UTPMAIN_GUI_SUPPORTED_BAUDS = new[] { 57600, 115200, 460800, 576000, 691200, 806400, 921600, 2000000 };

        MemoryStream ms;
        bool abortLogged;
        bool portUnavailable;

        sealed class TransferSegment
        {
            public uint FileType { get; set; }
            public int Address { get; set; }
            public byte[] Data { get; set; }
            public string Description { get; set; }
        }

        public TR6260Flasher(CancellationToken ct) : base(ct)
        {
        }

        bool IsPortUnavailableException(Exception ex)
        {
            return ex is InvalidOperationException || ex is ObjectDisposedException || ex is IOException || ex is NullReferenceException;
        }

        bool IsAbortState()
        {
            return isCancelled || cancellationToken.IsCancellationRequested || portUnavailable;
        }

        bool WaitCancelable(int delayMs)
        {
            if(ShouldAbort())
                return false;

            if(delayMs <= 0)
                return true;

            return !cancellationToken.WaitHandle.WaitOne(delayMs) && !ShouldAbort();
        }

        bool ShouldAbort(bool requireOpenPort = false, string message = null)
        {
            if(isCancelled || cancellationToken.IsCancellationRequested)
            {
                if(!abortLogged)
                {
                    addWarningLine(message ?? "TR6260: operation cancelled");
                    abortLogged = true;
                }
                return true;
            }

            if(requireOpenPort)
            {
                bool unavailable;
                var port = serial;
                try
                {
                    unavailable = portUnavailable || port == null || !port.IsOpen;
                }
                catch
                {
                    unavailable = true;
                }

                if(unavailable)
                {
                    portUnavailable = true;
                    if(!abortLogged)
                    {
                        addWarningLine(message ?? "TR6260: serial port is not available");
                        abortLogged = true;
                    }
                    return true;
                }
            }

            return false;
        }

        bool SetupPort(int initialBaud = DEFAULT_BAUD)
        {
            try
            {
                closePort();
                abortLogged = false;
                portUnavailable = false;
                serial = new SerialPort(serialName, initialBaud)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    DtrEnable = false,
                    RtsEnable = false
                };
                serial.Open();
                FlushPort();
                return true;
            }
            catch(Exception ex)
            {
                addErrorLine("TR6260: failed to open port: " + ex.Message);
                return false;
            }
        }

        void FlushPort()
        {
            try
            {
                var port = serial;
                if(port == null || !port.IsOpen)
                    return;
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }
            catch
            {
            }
        }

        byte? ReadResponseByte(int attempts = 1, int delayMs = 50)
        {
            for(int i = 0; i < attempts; i++)
            {
                if(ShouldAbort(true))
                    return null;

                try
                {
                    var port = serial;
                    if(port == null || !port.IsOpen)
                    {
                        portUnavailable = true;
                        return null;
                    }

                    int v = port.ReadByte();
                    if(v >= 0)
                        return (byte)v;
                }
                catch(TimeoutException)
                {
                }
                catch(Exception ex)
                {
                    if(IsPortUnavailableException(ex))
                    {
                        portUnavailable = true;
                        if(!abortLogged)
                        {
                            addWarningLine("TR6260: serial port became unavailable while waiting for a response");
                            abortLogged = true;
                        }
                        return null;
                    }
                    return null;
                }

                if(delayMs > 0 && !WaitCancelable(delayMs))
                    return null;
            }
            return null;
        }

        bool WriteRaw(byte[] data)
        {
            if(ShouldAbort(true))
                return false;

            try
            {
                var port = serial;
                if(port == null || !port.IsOpen)
                {
                    portUnavailable = true;
                    return false;
                }

                port.Write(data, 0, data.Length);
                return true;
            }
            catch(Exception ex)
            {
                if(IsPortUnavailableException(ex))
                {
                    portUnavailable = true;
                    if(!abortLogged)
                    {
                        addWarningLine("TR6260: serial port became unavailable during transfer");
                        abortLogged = true;
                    }
                    return false;
                }

                addErrorLine("TR6260: serial write failed: " + ex.Message);
                return false;
            }
        }

        byte SyncOnce()
        {
            if(!WriteRaw(BitConverter.GetBytes(TRS_SYNC)))
                return 0;
            if(!WaitCancelable(50))
                return 0;
            return ReadResponseByte(1, 0) ?? (byte)0;
        }

        bool WaitForUbootSync(int attempts = 10)
        {
            for(int loop = 1; loop <= attempts; loop++)
            {
                if(ShouldAbort(true, "TR6260: uboot sync aborted because the serial port is unavailable"))
                    return false;

                byte resp = SyncOnce();
                if(resp == TRS_UBOOT_SYNC_ACK)
                    return true;
                if(ShouldAbort(true, "TR6260: uboot sync aborted because the serial port is unavailable"))
                    return false;

                addLogLine($"TR6260: response {resp} after RAM boot upload, retry {loop}/{attempts}");
                if(!WaitCancelable(150))
                    return false;
            }
            return false;
        }

        bool PrepareSession(bool needUbootProtocol)
        {
            if(!SetupPort())
                return false;

            addLogLine("TR6260: syncing bootrom...");
            byte syncResp = 0;
            for(int loop = 1; loop <= 10; loop++)
            {
                if(ShouldAbort(true, "TR6260: sync aborted because the serial port is unavailable"))
                    return false;

                syncResp = SyncOnce();
                if(syncResp == TRS_ROM_SYNC_ACK || syncResp == TRS_UBOOT_SYNC_ACK)
                    break;
                if(ShouldAbort(true, "TR6260: sync aborted because the serial port is unavailable"))
                    return false;

                if(!WaitCancelable(1000))
                    return false;
            }

            if(syncResp != TRS_ROM_SYNC_ACK && syncResp != TRS_UBOOT_SYNC_ACK)
            {
                addErrorLine("TR6260: sync failed");
                return false;
            }

            if(syncResp == TRS_ROM_SYNC_ACK)
            {
                addLogLine("TR6260: sync OK (UART/bootrom mode)");
                if(needUbootProtocol)
                {
                    if(!LoadBootloaderToRam())
                        return false;
                    if(!WaitForUbootSync())
                    {
                        addErrorLine("TR6260: RAM boot upload completed, but sync to uboot failed");
                        return false;
                    }
                    addLogLine("TR6260: sync OK (uboot mode)");
                }
            }
            else
            {
                addLogLine("TR6260: sync OK (uboot/flash mode)");
            }

            if(needUbootProtocol && !ConfigureBaudrateIfNeeded())
                return false;

            if(needUbootProtocol)
            {
                byte verify = SyncOnce();
                if(verify != TRS_UBOOT_SYNC_ACK)
                {
                    if(!IsAbortState())
                        addErrorLine("TR6260: download/read protocol sync failed after baud change");
                    return false;
                }
            }

            return true;
        }

        bool IsUtpMainUserBaudSupported(int requested)
        {
            return UTPMAIN_GUI_SUPPORTED_BAUDS.Contains(requested);
        }

        string DescribeSupportedBauds()
        {
            return string.Join(", ", UTPMAIN_GUI_SUPPORTED_BAUDS);
        }

        bool ConfigureBaudrateIfNeeded()
        {
            int requested = baudrate > 0 ? baudrate : DEFAULT_BAUD;
            if(!IsUtpMainUserBaudSupported(requested))
            {
                addErrorLine($"TR6260: baud {requested} is not supported by the UTPmain GUI flow. Supported values: {DescribeSupportedBauds()}");
                return false;
            }

            byte baudCode;
            switch(requested)
            {
                case 57600:
                    return true;
                case 115200:
                    baudCode = 1;
                    break;
                case 460800:
                    baudCode = 5;
                    break;
                case 576000:
                    baudCode = 6;
                    break;
                case 691200:
                    baudCode = 7;
                    break;
                case 806400:
                    baudCode = 8;
                    break;
                case 921600:
                    baudCode = 9;
                    break;
                case 2000000:
                    baudCode = 13;
                    break;
                default:
                    addErrorLine($"TR6260: baud {requested} is listed as supported but no download-mode baud code is defined for it in the current flasher implementation");
                    return false;
            }

            if(!WriteRaw(new[] { baudCode }))
                return false;

            try
            {
                serial.BaudRate = requested;
            }
            catch(Exception ex)
            {
                addErrorLine("TR6260: failed to switch serial port baud rate: " + ex.Message);
                return false;
            }

            if(!WaitCancelable(50))
                return false;
            byte? ack = ReadResponseByte(1, 0);
            if(ack != TRS_ROM_BAUD_ACK)
            {
                if(!IsAbortState())
                    addErrorLine($"TR6260: set baud failed, response {(ack.HasValue ? ack.Value.ToString() : "<timeout>")}");
                return false;
            }

            return true;
        }

        byte[] TryLoadEmbeddedAsset(string assetName, string fileName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string wantedSuffix = ".Floaders." + fileName;
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(str => str.EndsWith(wantedSuffix, StringComparison.OrdinalIgnoreCase)
                        || str.IndexOf(assetName + ".bin", StringComparison.OrdinalIgnoreCase) >= 0);
                if(resourceName == null)
                    return null;

                using(var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if(stream == null)
                        return null;
                    using(var msLocal = new MemoryStream())
                    {
                        stream.CopyTo(msLocal);
                        byte[] raw = msLocal.ToArray();
                        if(raw.Length >= 2 && raw[0] == 0x1F && raw[1] == 0x8B)
                            return FLoaders.GZToBytes(raw);
                        return raw;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        byte[] LoadTr6260Asset(string assetName, string fileName)
        {
            byte[] embedded = TryLoadEmbeddedAsset(assetName, fileName);
            if(embedded != null && embedded.Length > 0)
                return embedded;

            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Floaders", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                Path.Combine("BK7231Flasher", "Floaders", fileName),
                Path.Combine("Floaders", fileName)
            };

            foreach(var path in candidates.Distinct())
            {
                if(File.Exists(path))
                    return File.ReadAllBytes(path);
            }

            return null;
        }

        bool LoadBootloaderToRam()
        {
            byte[] boot = LoadTr6260Asset("TR6260_Boot", "TR6260_Boot.bin");
            if(boot == null || boot.Length == 0)
            {
                addErrorLine("TR6260: TR6260_Boot.bin not found (embedded or disk)");
                return false;
            }

            addLogLine("TR6260: uploading bootloader to RAM...");
            if(!BeginTransfer(TRS_FRM_TYPE_UBOOT, 0, boot.Length, 30))
            {
                addErrorLine("TR6260: RAM bootloader header failed");
                return false;
            }

            if(!WriteFileBlocks(boot, "RAM bootloader"))
                return false;

            addLogLine("TR6260: RAM bootloader upload completed");
            return true;
        }

        bool BeginTransfer(uint fileType, int address, int length, int responsePolls = 30)
        {
            byte[] header = new byte[12];
            Array.Copy(BitConverter.GetBytes(fileType), 0, header, 0, 4);
            Array.Copy(BitConverter.GetBytes(address), 0, header, 4, 4);
            Array.Copy(BitConverter.GetBytes(length), 0, header, 8, 4);

            if(!WriteRaw(header))
                return false;

            return ReadResponseByte(responsePolls, 50).HasValue;
        }

        bool WriteFileBlocks(byte[] data, string description)
        {
            int done = 0;
            int totalBlocks = (data.Length + FLASH_BLOCK - 1) / FLASH_BLOCK;
            int blockIndex = 0;
            while(done < data.Length)
            {
                int take = Math.Min(FLASH_BLOCK, data.Length - done);
                byte[] block = new byte[take];
                Buffer.BlockCopy(data, done, block, 0, take);
                if(!WriteRaw(block))
                    return false;

                byte? ack = ReadResponseByte(30, 50);
                if(ack != TRS_ROM_FILE_ACK)
                {
                    if(!IsAbortState())
                        addErrorLine($"TR6260: {description} failed at block {blockIndex + 1}/{Math.Max(totalBlocks, 1)}, response {(ack.HasValue ? ack.Value.ToString() : "<timeout>")}");
                    return false;
                }

                done += take;
                blockIndex++;
                logger.setProgress(done, data.Length);
            }

            return true;
        }

        bool RunUploadedProgram()
        {
            byte[] runCfg = new byte[12];
            if(!WriteRaw(runCfg))
                return false;

            byte? resp = ReadResponseByte(1, 50);
            if(resp == TRS_PARTITION_MISSING)
                addWarningLine("TR6260: target reported missing partition table");
            else if(resp == TRS_PARTITION_ERROR)
                addWarningLine("TR6260: target reported partition error");

            return true;
        }

        uint GetFileTypeForAddress(int address)
        {
            if(address == 0)
                return TRS_FRM_TYPE_UBOOT;
            if(address == APP2_ADDR)
                return TRS_FRM_TYPE_APP_2;
            if(address == RAM_ADDR)
                return TRS_FRM_TYPE_RAM;
            if(address == 0x4000 || address == 0xA000)
                return TRS_FRM_TYPE_APP;
            return TRS_FRM_TYPE_NV;
        }

        bool WriteSingleSegment(int address, byte[] data, uint? fileTypeOverride = null, string description = null)
        {
            uint fileType = fileTypeOverride ?? GetFileTypeForAddress(address);
            string desc = description ?? $"segment at 0x{address:X}";
            addLogLine($"TR6260: writing {desc}, {data.Length} bytes at 0x{address:X}");

            if(!BeginTransfer(fileType, address, data.Length, 30))
            {
                addErrorLine($"TR6260: {desc} header failed");
                return false;
            }

            if(!WriteFileBlocks(data, desc))
                return false;

            return true;
        }

        List<TransferSegment> TryParseOneB(byte[] data)
        {
            if(data == null || data.Length < 8)
                return null;

            bool isOneB = (data[0] == (byte)'o' && data[1] == (byte)'n' && data[2] == (byte)'e' && data[3] == (byte)'b')
                || (data[0] == (byte)'b' && data[1] == (byte)'e' && data[2] == (byte)'n' && data[3] == (byte)'o');
            if(!isOneB)
                return null;

            try
            {
                uint count = BitConverter.ToUInt32(data, 4);
                int offset = 8;
                var segments = new List<TransferSegment>();
                for(uint i = 0; i < count; i++)
                {
                    if(offset + 12 > data.Length)
                        return null;

                    uint fileType = BitConverter.ToUInt32(data, offset + 0);
                    int address = BitConverter.ToInt32(data, offset + 4);
                    int length = BitConverter.ToInt32(data, offset + 8);
                    offset += 12;

                    if(length < 0 || offset + length > data.Length)
                        return null;

                    byte[] segmentData = new byte[length];
                    Buffer.BlockCopy(data, offset, segmentData, 0, length);
                    offset += length;

                    segments.Add(new TransferSegment
                    {
                        FileType = fileType,
                        Address = address,
                        Data = segmentData,
                        Description = $"oneb type {fileType} at 0x{address:X}"
                    });
                }
                return segments;
            }
            catch
            {
                return null;
            }
        }

        bool LooksLikeWholeFlashImage(int address, byte[] data)
        {
            if(address != 0 || data == null)
                return false;

            if(data.Length == DEFAULT_FLASH_SIZE)
                return true;

            return data.Length >= (DEFAULT_FLASH_SIZE - APP_ADDR);
        }

        bool WriteFirmwarePayload(int startSector, byte[] data)
        {
            int address = startSector * BK7231Flasher.SECTOR_SIZE;
            List<TransferSegment> oneb = TryParseOneB(data);
            if(oneb != null)
            {
                addLogLine($"TR6260: detected all-in-one package with {oneb.Count} segment(s)");
                foreach(var segment in oneb)
                {
                    if(!WriteSingleSegment(segment.Address, segment.Data, segment.FileType, segment.Description))
                        return false;
                }
                return true;
            }

            if(LooksLikeWholeFlashImage(address, data))
            {
                addLogLine("TR6260: treating input as whole-flash image at 0x000000");
                return WriteSingleSegment(0, data, TRS_FRM_TYPE_UBOOT, "whole flash image");
            }

            if(address == 0)
            {
                byte[] boot = LoadTr6260Asset("TR6260_Boot", "TR6260_Boot.bin");
                byte[] partition = LoadTr6260Asset("TR6260_Partition", "TR6260_Partition.bin");

                if(boot == null || boot.Length == 0)
                {
                    addErrorLine("TR6260: TR6260_Boot.bin not found (embedded or disk)");
                    return false;
                }
                if(partition == null || partition.Length == 0)
                {
                    addErrorLine("TR6260: TR6260_Partition.bin not found (embedded or disk)");
                    return false;
                }

                if(!WriteSingleSegment(0, boot, TRS_FRM_TYPE_UBOOT, "bootloader"))
                    return false;
                if(!WriteSingleSegment(PARTITION_ADDR, partition, TRS_FRM_TYPE_NV, "partition table"))
                    return false;
                if(!WriteSingleSegment(APP_ADDR, data, TRS_FRM_TYPE_NV, "application"))
                    return false;
                return true;
            }

            return WriteSingleSegment(address, data, null, null);
        }

        byte[] ReadExact(int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while(offset < count)
            {
                if(ShouldAbort(true))
                    return null;

                try
                {
                    var port = serial;
                    if(port == null || !port.IsOpen)
                    {
                        portUnavailable = true;
                        return null;
                    }

                    int got = port.Read(buffer, offset, count - offset);
                    if(got <= 0)
                        return null;
                    offset += got;
                }
                catch(TimeoutException)
                {
                    return null;
                }
                catch(Exception ex)
                {
                    if(IsPortUnavailableException(ex))
                    {
                        portUnavailable = true;
                        if(!abortLogged)
                        {
                            addWarningLine("TR6260: serial port became unavailable during read");
                            abortLogged = true;
                        }
                    }
                    return null;
                }
            }
            return buffer;
        }

        bool ReadFlashViaUboot(int offset, int length)
        {
            ms = new MemoryStream();
            addLogLine($"TR6260: reading {length} bytes at 0x{offset:X}");
            if(!BeginTransfer(TRS_FRM_TYPE_UPLOAD, offset, length, 30))
            {
                if(!IsAbortState())
                    addErrorLine("TR6260: read begin failed");
                return false;
            }

            int remaining = length;
            while(remaining > 0)
            {
                int take = Math.Min(FLASH_BLOCK, remaining);
                byte[] chunk = ReadExact(take);
                if(chunk == null || chunk.Length != take)
                {
                    if(!IsAbortState())
                        addErrorLine("TR6260: read data timed out");
                    ms = null;
                    return false;
                }

                ms.Write(chunk, 0, chunk.Length);
                remaining -= chunk.Length;
                logger.setProgress(length - remaining, length);

                if(!WriteRaw(new[] { TRS_ROM_FILE_ACK }))
                {
                    ms = null;
                    return false;
                }
            }

            addSuccess("TR6260 read completed.\n");
            return true;
        }

        bool EraseViaUboot(int offset, int length)
        {
            addLogLine($"TR6260: erasing 0x{length:X} bytes at 0x{offset:X}");
            if(!BeginTransfer(TRS_FRM_TYPE_ERASE, offset, length, 600))
            {
                if(!IsAbortState())
                    addErrorLine("TR6260: erase failed");
                return false;
            }

            addSuccess("TR6260 erase completed.\n");
            return true;
        }

        public override void doWrite(int startSector, byte[] data)
        {
            if(data == null || data.Length == 0)
            {
                addErrorLine("TR6260: no data supplied for write");
                return;
            }

            try
            {
                if(!PrepareSession(true))
                    return;

                if(!WriteFirmwarePayload(startSector, data))
                    return;

                RunUploadedProgram();
                addSuccess("TR6260 write completed.\n");
            }
            finally
            {
                closePort();
            }
        }

        public override void doRead(int startSector = 0, int sectors = 10, bool fullRead = false)
        {
            int offset = startSector * BK7231Flasher.SECTOR_SIZE;
            int length = sectors * BK7231Flasher.SECTOR_SIZE;
            if(fullRead)
            {
                offset = 0;
                length = DEFAULT_FLASH_SIZE;
            }

            try
            {
                if(!PrepareSession(true))
                    return;

                ReadFlashViaUboot(offset, length);
            }
            finally
            {
                closePort();
            }
        }

        public override byte[] getReadResult()
        {
            return ms?.ToArray();
        }

        public override bool doErase(int startSector = 0, int sectors = 10, bool bAll = false)
        {
            int offset = bAll ? 0 : (startSector * BK7231Flasher.SECTOR_SIZE);
            int length = bAll ? DEFAULT_FLASH_SIZE : (sectors * BK7231Flasher.SECTOR_SIZE);

            try
            {
                if(!PrepareSession(true))
                    return false;

                return EraseViaUboot(offset, length);
            }
            finally
            {
                closePort();
            }
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if(rwMode == WriteMode.ReadAndWrite)
            {
                doRead(startSector, sectors, false);
                if(ms == null)
                    return;
                if(!saveReadResult(startSector * BK7231Flasher.SECTOR_SIZE))
                    return;
            }

            if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
            {
                if(string.IsNullOrEmpty(sourceFileName))
                {
                    addErrorLine("TR6260: no filename given for write");
                    return;
                }

                addLogLine("Reading " + sourceFileName + "...");
                byte[] data = File.ReadAllBytes(sourceFileName);
                doWrite(startSector, data);
                return;
            }

            if(rwMode == WriteMode.OnlyOBKConfig)
            {
                addWarningLine("TR6260: OBK config write is not implemented");
            }
        }

        bool saveReadResult(string fileName)
        {
            if(ms == null)
            {
                addError("There was no result to save.\n");
                return false;
            }

            byte[] dat = ms.ToArray();
            string fullPath = Path.Combine("backups", fileName);
            Directory.CreateDirectory("backups");
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

        public override void closePort()
        {
            var port = serial;
            serial = null;
            portUnavailable = true;
            if(port != null)
            {
                try { port.Close(); } catch { }
                try { port.Dispose(); } catch { }
            }
        }
    }
}
