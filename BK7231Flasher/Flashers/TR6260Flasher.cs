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
        const int ERASE_OPERATION_BAUD = 115200;
        const int FLASH_BLOCK = 1024;
        const int PARTITION_ADDR = 0x6000;
        const int APP_ADDR = 0x7000;
        const int APP2_ADDR = 0x80000;
        const int RAM_ADDR = 0x10000;
        const uint TRS_SYNC = 0x73796E63;

        static readonly int[] SUPPORTED_BAUDS = { 57600, 115200, 460800, 576000, 691200, 806400, 921600, 2000000 };
        const string SUPPORTED_BAUDS_TEXT = "57600, 115200, 460800, 576000, 691200, 806400, 921600, 2000000";

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

        MemoryStream ms;
        bool sessionPortUnavailable;
        bool sessionClosedPortWriteLogged;

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

        void SetState(string text, Color color)
        {
            logger?.setState(text, color);
        }

        void SetBusyState(string text)
        {
            SetState(text, Color.Transparent);
        }

        void SetErrorState(string text)
        {
            SetState(text, Color.Red);
        }

        void SetDoneState(string text)
        {
            SetState(text, Color.DarkGreen);
        }

        void ResetSessionFlags()
        {
            sessionPortUnavailable = false;
            sessionClosedPortWriteLogged = false;
        }

        int GetRequestedBaud(int? requestedBaudOverride = null)
        {
            return requestedBaudOverride ?? (baudrate > 0 ? baudrate : DEFAULT_BAUD);
        }

        bool IsPortUnavailable()
        {
            try
            {
                return serial == null || !serial.IsOpen;
            }
            catch
            {
                return true;
            }
        }

        bool IsPortUnavailableException(Exception ex)
        {
            if(ex is ObjectDisposedException || ex is InvalidOperationException || ex is NullReferenceException)
                return true;

            string msg = ex?.Message ?? string.Empty;
            return msg.IndexOf("port is closed", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("instance of an object", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void MarkPortUnavailable()
        {
            sessionPortUnavailable = true;
        }

        void LogPortClosedWriteFailureOnce()
        {
            if(sessionClosedPortWriteLogged)
                return;

            addErrorLine("Serial write failed: The port is closed.");
            sessionClosedPortWriteLogged = true;
        }

        bool PrepareReadOrWriteSession()
        {
            return PrepareSession(true);
        }

        bool PrepareEraseSession()
        {
            return PrepareSession(true, ERASE_OPERATION_BAUD);
        }

        bool SetupPort(int initialBaud = DEFAULT_BAUD)
        {
            try
            {
                closePort();
                ResetSessionFlags();
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
                addErrorLine("Failed to open port: " + ex.Message);
                SetErrorState("Open port failed");
                return false;
            }
        }

        void FlushPort()
        {
            try
            {
                serial?.DiscardInBuffer();
                serial?.DiscardOutBuffer();
            }
            catch
            {
            }
        }

        byte? ReadResponseByte(int attempts = 1, int delayMs = 50)
        {
            for(int i = 0; i < attempts; i++)
            {
                try
                {
                    int v = serial.ReadByte();
                    if(v >= 0)
                        return (byte)v;
                }
                catch(TimeoutException)
                {
                }
                catch(Exception ex)
                {
                    if(IsPortUnavailableException(ex) || IsPortUnavailable())
                    {
                        MarkPortUnavailable();
                        return null;
                    }
                }

                if(sessionPortUnavailable || cancellationToken.IsCancellationRequested)
                    return null;

                if(delayMs > 0)
                    Thread.Sleep(delayMs);
            }
            return null;
        }

        bool WriteRaw(byte[] data)
        {
            if(data == null || data.Length == 0)
                return true;

            if(IsPortUnavailable())
            {
                MarkPortUnavailable();
                LogPortClosedWriteFailureOnce();
                return false;
            }

            try
            {
                serial.Write(data, 0, data.Length);
                return true;
            }
            catch(Exception ex)
            {
                if(IsPortUnavailableException(ex) || IsPortUnavailable())
                {
                    MarkPortUnavailable();
                    LogPortClosedWriteFailureOnce();
                    return false;
                }

                addErrorLine("Serial write failed: " + ex.Message);
                return false;
            }
        }

        byte SyncOnce()
        {
            if(!WriteRaw(BitConverter.GetBytes(TRS_SYNC)))
                return 0;
            Thread.Sleep(50);
            return ReadResponseByte(1, 0) ?? (byte)0;
        }

        bool WaitForUbootSync(int attempts = 10)
        {
            for(int loop = 1; loop <= attempts; loop++)
            {
                byte resp = SyncOnce();
                if(resp == TRS_UBOOT_SYNC_ACK)
                    return true;

                if(sessionPortUnavailable || cancellationToken.IsCancellationRequested)
                    break;

                addLogLine($"Response {resp} after RAM boot upload, retry {loop}/{attempts}");
                Thread.Sleep(150);
            }
            return false;
        }

        bool IsSupportedBaud(int requested)
        {
            return SUPPORTED_BAUDS.Contains(requested);
        }

        bool ValidateRequestedBaud(int? requestedBaudOverride = null)
        {
            int requested = GetRequestedBaud(requestedBaudOverride);
            if(IsSupportedBaud(requested))
                return true;

            addErrorLine($"Baud {requested} is not supported. Supported values: {SUPPORTED_BAUDS_TEXT}");
            SetErrorState("Unsupported baud");
            return false;
        }

        bool PrepareSession(bool needUbootProtocol, int? requestedBaudOverride = null)
        {
            if(!ValidateRequestedBaud(requestedBaudOverride))
                return false;

            SetBusyState("Opening port...");
            if(!SetupPort())
                return false;

            SetBusyState("Syncing bootrom...");
            addLogLine("Syncing bootrom...");
            byte syncResp = 0;
            for(int loop = 1; loop <= 10; loop++)
            {
                syncResp = SyncOnce();
                if(syncResp == TRS_ROM_SYNC_ACK || syncResp == TRS_UBOOT_SYNC_ACK)
                    break;

                if(sessionPortUnavailable || cancellationToken.IsCancellationRequested)
                    break;

                Thread.Sleep(1000);
            }

            // If sync failed at the default baud, the device may still be in uboot at
            // ERASE_OPERATION_BAUD from a previous erase session. Try that baud before giving up.
            if(syncResp != TRS_ROM_SYNC_ACK && syncResp != TRS_UBOOT_SYNC_ACK
               && serial != null && ERASE_OPERATION_BAUD != serial.BaudRate)
            {
                addLogLine($"Sync failed at {serial.BaudRate}, retrying at {ERASE_OPERATION_BAUD}...");
                serial.BaudRate = ERASE_OPERATION_BAUD;
                for(int loop = 1; loop <= 5; loop++)
                {
                    syncResp = SyncOnce();
                    if(syncResp == TRS_ROM_SYNC_ACK || syncResp == TRS_UBOOT_SYNC_ACK)
                        break;

                    if(sessionPortUnavailable || cancellationToken.IsCancellationRequested)
                        break;

                    Thread.Sleep(500);
                }
            }

            if(syncResp != TRS_ROM_SYNC_ACK && syncResp != TRS_UBOOT_SYNC_ACK)
            {
                addErrorLine("Sync failed");
                SetErrorState("Sync failed");
                return false;
            }

            if(syncResp == TRS_ROM_SYNC_ACK)
            {
                addLogLine("Sync OK (UART/bootrom mode)");
                if(needUbootProtocol)
                {
                    if(!LoadBootloaderToRam())
                        return false;
                    if(!WaitForUbootSync())
                    {
                        addErrorLine("RAM boot upload completed, but sync to uboot failed");
                        SetErrorState("Sync failed");
                        return false;
                    }
                    addLogLine("Sync OK (uboot mode)");
                }
            }
            else
            {
                addLogLine("Sync OK (uboot/flash mode)");
            }

            if(needUbootProtocol)
            {
                SetBusyState("Changing baud...");
                if(!ConfigureBaudrateIfNeeded(requestedBaudOverride))
                    return false;
            }

            if(needUbootProtocol)
            {
                SetBusyState("Verifying protocol...");
                byte verify = SyncOnce();
                if(verify != TRS_UBOOT_SYNC_ACK)
                {
                    addErrorLine("Download/read protocol sync failed after baud change");
                    SetErrorState("Protocol sync failed");
                    return false;
                }
            }

            return true;
        }

        bool ConfigureBaudrateIfNeeded(int? requestedBaudOverride = null)
        {
            int requested = GetRequestedBaud(requestedBaudOverride);
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
                case 1400000:
                    baudCode = 10;
                    break;
                case 1660000:
                    baudCode = 11;
                    break;
                case 1840000:
                    baudCode = 12;
                    break;
                case 2000000:
                    baudCode = 13;
                    break;
                default:
                    addErrorLine($"Baud {requested} is not supported. Supported values: {SUPPORTED_BAUDS_TEXT}");
                    SetErrorState("Unsupported baud");
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
                addErrorLine("Failed to switch serial port baud rate: " + ex.Message);
                SetErrorState("Baud change failed");
                return false;
            }

            Thread.Sleep(50);
            byte? ack = ReadResponseByte(1, 0);
            if(ack != TRS_ROM_BAUD_ACK)
            {
                addErrorLine($"Set baud failed, response {(ack.HasValue ? ack.Value.ToString() : "<timeout>")}");
                SetErrorState("Baud change failed");
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
                addErrorLine("TR6260_Boot.bin not found (embedded or disk)");
                SetErrorState("Bootloader missing");
                return false;
            }

            SetBusyState("Uploading RAM loader...");
            addLogLine("Uploading RAM loader...");
            if(!BeginTransfer(TRS_FRM_TYPE_UBOOT, 0, boot.Length, 30))
            {
                addErrorLine("RAM loader header failed");
                SetErrorState("RAM loader upload failed");
                return false;
            }

            if(!WriteFileBlocks(boot, "RAM loader"))
                return false;

            addLogLine("RAM loader upload completed.");
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

        bool WriteFileBlocks(byte[] data, string description, int baseAddress = -1)
        {
            int done = 0;
            int totalBlocks = (data.Length + FLASH_BLOCK - 1) / FLASH_BLOCK;
            int blockIndex = 0;
            int nextPrintOffset = baseAddress >= 0 ? baseAddress : -1;
            while(done < data.Length)
            {
                if(cancellationToken.IsCancellationRequested)
                    return false;
                if(baseAddress >= 0)
                {
                    int currentOffset = baseAddress + done;
                    if(currentOffset >= nextPrintOffset)
                    {
                        addLog(formatHex(currentOffset) + "... ");
                        nextPrintOffset = currentOffset + BK7231Flasher.SECTOR_SIZE;
                    }
                }

                int take = Math.Min(FLASH_BLOCK, data.Length - done);
                byte[] block = new byte[take];
                Buffer.BlockCopy(data, done, block, 0, take);
                if(!WriteRaw(block))
                    return false;

                byte? ack = ReadResponseByte(30, 50);
                if(ack != TRS_ROM_FILE_ACK)
                {
                    addLog(Environment.NewLine);
                    addErrorLine($"{description} failed at block {blockIndex + 1}/{Math.Max(totalBlocks, 1)}, response {(ack.HasValue ? ack.Value.ToString() : "<timeout>")}");
                    SetErrorState("Transfer failed");
                    return false;
                }

                done += take;
                blockIndex++;
                logger.setProgress(done, data.Length);
            }

            if(baseAddress >= 0)
                addLog(Environment.NewLine);

            return true;
        }

        bool RunUploadedProgram()
        {
            byte[] runCfg = new byte[12];
            if(!WriteRaw(runCfg))
                return false;

            byte? resp = ReadResponseByte(1, 50);
            if(resp == TRS_PARTITION_MISSING)
                addWarningLine("Target reported missing partition table");
            else if(resp == TRS_PARTITION_ERROR)
                addWarningLine("Target reported partition error");

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
            addLogLine($"Writing {desc}, {data.Length} bytes at 0x{address:X}");

            if(!BeginTransfer(fileType, address, data.Length, 30))
            {
                addErrorLine($"{desc} header failed");
                return false;
            }

            if(!WriteFileBlocks(data, desc, address))
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

        bool WriteFirmwarePayload(int startOffset, byte[] data)
        {
            int address = startOffset;
            List<TransferSegment> oneb = TryParseOneB(data);
            if(oneb != null)
            {
                addLogLine($"Detected all-in-one package with {oneb.Count} segment(s)");
                foreach(var segment in oneb)
                {
                    if(!WriteSingleSegment(segment.Address, segment.Data, segment.FileType, segment.Description))
                        return false;
                }
                return true;
            }

            if(LooksLikeWholeFlashImage(address, data))
            {
                addLogLine("Treating input as whole-flash image at 0x000000");
                return WriteSingleSegment(0, data, TRS_FRM_TYPE_UBOOT, "whole flash image");
            }

            if(address == 0)
            {
                byte[] boot = LoadTr6260Asset("TR6260_Boot", "TR6260_Boot.bin");
                byte[] partition = LoadTr6260Asset("TR6260_Partition", "TR6260_Partition.bin");

                if(boot == null || boot.Length == 0)
                {
                    addErrorLine("TR6260_Boot.bin not found (embedded or disk)");
                    return false;
                }
                if(partition == null || partition.Length == 0)
                {
                    addErrorLine("TR6260_Partition.bin not found (embedded or disk)");
                    return false;
                }

                if(!WriteSingleSegment(0, boot, TRS_FRM_TYPE_UBOOT, "RAM loader"))
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
                try
                {
                    int got = serial.Read(buffer, offset, count - offset);
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
                    if(IsPortUnavailableException(ex) || IsPortUnavailable())
                        MarkPortUnavailable();
                    return null;
                }
            }
            return buffer;
        }

        bool ReadFlashViaUboot(int offset, int length)
        {
            ms = new MemoryStream();
            SetBusyState("Reading...");
            addLogLine($"Starting flash read, ofs 0x{offset:X}, len 0x{length:X}");
            if(!BeginTransfer(TRS_FRM_TYPE_UPLOAD, offset, length, 30))
            {
                addErrorLine("Read begin failed");
                SetErrorState("Read error");
                return false;
            }

            addLog("Going to start reading at offset " + formatHex(offset) + "..." + Environment.NewLine);

            int remaining = length;
            int nextPrintOffset = offset;
            while(remaining > 0)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    addLog(Environment.NewLine);
                    SetErrorState("Cancelled");
                    ms = null;
                    return false;
                }

                int done = length - remaining;
                int currentOffset = offset + done;
                if(currentOffset >= nextPrintOffset)
                {
                    addLog(formatHex(currentOffset) + "... ");
                    nextPrintOffset = currentOffset + BK7231Flasher.SECTOR_SIZE;
                }

                int take = Math.Min(FLASH_BLOCK, remaining);
                byte[] chunk = ReadExact(take);
                if(chunk == null || chunk.Length != take)
                {
                    addLog(Environment.NewLine);
                    addErrorLine("Read data timed out");
                    SetErrorState("Read error");
                    ms = null;
                    return false;
                }

                ms.Write(chunk, 0, chunk.Length);
                remaining -= chunk.Length;
                logger.setProgress(length - remaining, length);

                if(!WriteRaw(new[] { TRS_ROM_FILE_ACK }))
                {
                    addLog(Environment.NewLine);
                    SetErrorState("Read error");
                    ms = null;
                    return false;
                }
            }

            addLog(Environment.NewLine);
            addSuccess("Read completed.\n");
            SetDoneState("Read done");
            return true;
        }

        bool EraseViaUboot(int offset, int length)
        {
            SetBusyState("Erasing...");
            addLogLine($"Starting flash erase, ofs 0x{offset:X}, len 0x{length:X}");
            if(!BeginTransfer(TRS_FRM_TYPE_ERASE, offset, length, 100))
            {
                addErrorLine("Erase failed");
                SetErrorState("Erase failed!");
                return false;
            }

            addSuccess("Erase completed.\n");
            SetDoneState("Erase complete!");
            return true;
        }

        public override void doWrite(int startSector, byte[] data)
        {
            if(data == null || data.Length == 0)
            {
                addErrorLine("No data supplied for write");
                SetErrorState("Write error");
                return;
            }

            try
            {
                if(!PrepareReadOrWriteSession())
                    return;

                int writeOfs = startSector;
                addLogLine($"Starting flash write, ofs 0x{writeOfs:X}, len 0x{data.Length:X}");
                SetBusyState("Erasing...");
                if(!EraseViaUboot(0, DEFAULT_FLASH_SIZE))
                    return;
                SetBusyState("Writing...");
                if(!WriteFirmwarePayload(startSector, data))
                {
                    SetErrorState("Write error");
                    return;
                }

                addSuccess("Write completed.\n");
                SetDoneState("Write done");
            }
            finally
            {
                closePort();
            }
        }

        public override void doRead(int startSector = 0, int sectors = 10, bool fullRead = false)
        {
            int offset = startSector;
            int length = sectors * BK7231Flasher.SECTOR_SIZE;
            if(fullRead)
            {
                offset = 0;
                length = DEFAULT_FLASH_SIZE;
            }

            try
            {
                if(!PrepareReadOrWriteSession())
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
            int offset = bAll ? 0 : startSector;
            int length = bAll ? DEFAULT_FLASH_SIZE : (sectors * BK7231Flasher.SECTOR_SIZE);

            try
            {
                if(!PrepareEraseSession())
                    return false;

                return EraseViaUboot(offset, length);
            }
            finally
            {
                closePort();
            }
        }

        public override void closePort()
        {
            try
            {
                if(serial != null)
                {
                    try
                    {
                        if(serial.IsOpen)
                            serial.Close();
                    }
                    catch
                    {
                    }

                    try
                    {
                        serial.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                serial = null;
            }
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if(rwMode == WriteMode.ReadAndWrite)
            {
                // Single shared uboot session for the whole read+write sequence.
                // PrepareReadOrWriteSession is called once; the port stays open so the
                // uboot state machine is still live when the write phase begins.
                try
                {
                    if(!PrepareReadOrWriteSession())
                        return;

                    ReadFlashViaUboot(startSector, sectors * BK7231Flasher.SECTOR_SIZE);

                    if(ms == null)
                        return;

                    if(!saveReadResult(startSector))
                        return;

                    if(string.IsNullOrEmpty(sourceFileName))
                    {
                        addErrorLine("No filename given for write");
                        return;
                    }

                    addLogLine("Reading " + sourceFileName + "...");
                    byte[] data = File.ReadAllBytes(sourceFileName);

                    addLogLine($"Starting flash write, ofs 0x{startSector:X}, len 0x{data.Length:X}");
                    SetBusyState("Erasing...");
                    if(!EraseViaUboot(0, DEFAULT_FLASH_SIZE))
                        return;
                    SetBusyState("Writing...");
                    if(!WriteFirmwarePayload(startSector, data))
                    {
                        SetErrorState("Write error");
                        return;
                    }

                    addSuccess("Backup and write completed.\n");
                    SetDoneState("Write done");
                }
                finally
                {
                    closePort();
                }
                return;
            }

            if(rwMode == WriteMode.OnlyWrite)
            {
                if(string.IsNullOrEmpty(sourceFileName))
                {
                    addErrorLine("No filename given for write");
                    return;
                }

                addLogLine("Reading " + sourceFileName + "...");
                byte[] data = File.ReadAllBytes(sourceFileName);
                doWrite(startSector, data);
                return;
            }

            if(rwMode == WriteMode.OnlyOBKConfig)
            {
                OBKConfig cfg = logger.getConfig();
                if(cfg == null)
                {
                    addWarningLine("No OBK config to write.");
                    return;
                }

                try
                {
                    if(!PrepareReadOrWriteSession())
                        return;

                    var offset = OBKFlashLayout.getConfigLocation(chipType, out var cfgSectors);
                    if(offset == 0 || cfgSectors == 0)
                    {
                        addErrorLine("OBK config location not defined for TR6260.");
                        SetErrorState("OBK config error");
                        return;
                    }

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
                            addLog("Saving to existing EasyFlash failed, creating new: " + ex.Message + Environment.NewLine);
                            efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                        }
                    }
                    else
                    {
                        efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                    }

                    if(efdata == null)
                    {
                        addErrorLine("EasyFlash serialisation failed.");
                        SetErrorState("OBK config error");
                        return;
                    }

                    addLog("Now will also write OBK config..." + Environment.NewLine);
                    addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                    addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                    addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);

                    bool bOk = WriteSingleSegment(offset, efdata, TRS_FRM_TYPE_NV, "OBK config");
                    if(!bOk)
                    {
                        addErrorLine("Writing OBK config to chip failed.");
                        SetErrorState("OBK config write failed");
                        return;
                    }

                    logger.setState("OBK config write success!", Color.Green);
                }
                finally
                {
                    closePort();
                }
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
    }
}
