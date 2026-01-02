using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    public class BL602Flasher : BaseFlasher
    {
        //int timeoutMs = 10000;
        int flashSizeMB = 2;
        byte[] flashID;
        BLInfo blinfo = null;

        public bool Sync()
        {
            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    addLog($"Sync attempt {i}/1000 ");
                    if(internalSync())
                    {
                        logger.addLog("... OK!" + Environment.NewLine, Color.Green);
                        return true;
                    }
                    addWarningLine("... failed, will retry!");
                    if(i % 10 == 1)
                    {
                        addLogLine($"If doing something immediately after another operation, it might not sync for about half a minute");
                        addLogLine($"Otherwise, please pull high BOOT/IO8 and reset.");
                    }
                    Thread.Sleep(50);
                }
                catch(Exception ex)
                {
                    addLogLine("");
                    addErrorLine(ex.ToString());
                    return false;
                }
            }
            return false;
        }


        bool internalSync()
        {
            serial.DiscardInBuffer();

            // Write initialization sequence
            byte[] syncRequest = new byte[70];
            for (int i = 0; i < syncRequest.Length; i++) syncRequest[i] = (byte)'U';
            serial.Write(syncRequest, 0, syncRequest.Length);

            for (int i = 0; i < 75; i++)
            {
                Thread.Sleep(1);

                // Check for 2-byte response
                if (serial.BytesToRead >= 2)
                {
                    byte[] response = new byte[2];
                    serial.Read(response, 0, 2);
                    if (response[0] == 'O' && response[1] == 'K')
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal class BLInfo
        {
            public int BootromVersion;
            public byte[] remaining;
            public BKType Variant;

            public BLInfo(int bootromVersion)
            {
                BootromVersion = bootromVersion;
                var bootrom = $"{BootromVersion:X}";
                // BL702 ROM version is 0x7020001. BL616 - 0x6160001
                // unknown if BL704/706 and BL618 have different rom version.
                if(bootrom.StartsWith("702") || bootrom.StartsWith("704") || bootrom.StartsWith("706"))
                {
                    Variant = BKType.BL702;
                }
                else if(bootrom.StartsWith("616") || bootrom.StartsWith("618"))
                {
                    Variant = BKType.BL616;
                }
                else
                {
                    Variant = BKType.BL602;
                }
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"BootROM version: {BootromVersion} ({BootromVersion:X})");
                sb.AppendLine("OTP flags:");
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int index = x + y * 4;
                        sb.Append(Convert.ToString(remaining[index], 2).PadLeft(8, '0')).Append(" ");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine($"Chip type: {Variant}");
                return sb.ToString();
            }
        }

        internal BLInfo getInfo()
        {
            byte[] res = executeCommand(0x10, null);
            if (res == null)
            {
                return null;
            }
            int len = res[0] + (res[1] << 8);
            if (len + 2 != res.Length)
            {
                return null;
            }
            var v = new BLInfo(res[2] + (res[3] << 8) + (res[4] << 16) + (res[5] << 24))
            {
                remaining = new byte[res.Length - 6]
            };
            Array.Copy(res, 6, v.remaining, 0, v.remaining.Length);
            return v;
        }
        internal byte[] readFlashID()
        {
            byte[] res = executeCommand(0x36, null, 0, 0, true, 0.1f, 6);
            if (res == null)
            {
                return null;
            }

            if (res.Length >= 6)
            {
                flashSizeMB = (1 << (res[4] - 0x11)) / 8;
                if(flashSizeMB > 32)
                {
                    return null;
                }
                addLogLine("Flash ID: {0:X2}{1:X2}{2:X2}{3:X2}", res[2], res[3], res[4], res[5]);
                addLogLine($"Flash size is {flashSizeMB}MB");
            }
            else
            {
                addLogLine("Invalid response length: " + res.Length);
            }
            return res;
        }
        byte[] readFully()
        {
            byte[] r = new byte[serial.BytesToRead];
            for(int i = 0; i < r.Length; i++)
            {
                serial.Read(r, i, 1);
            }
            return r;
        }
        byte[] executeCommand(int type, byte[] parms = null,
            int start = 0, int len = 0, bool bChecksum = false,
            float timeout = 0.1f, int expectedReplyLen = 2)
        {
            if (len < 0)
            {
                len = parms.Length;
            }
            byte chksum = 1;
            if (bChecksum)
            {
                chksum = 0;
                chksum += (byte)(len & 0xFF);
                chksum += (byte)(len >> 8);
                for (int i = 0; i < len; i++)
                {
                    chksum += parms[start + i];
                }
                chksum = (byte)(chksum & 0xFF);
            }
            var raw = new byte[] { (byte)type, chksum, (byte)(len & 0xFF), (byte)(len >> 8) };
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            serial.Write(raw, 0, raw.Length);
            if (parms != null)
            {
                serial.Write(parms, start, len);
            }
            byte[] ret = null;
            int timeoutMS = (int)(timeout * 1000);
#if false
        Thread.Sleep(100);
        while (timeoutMS > 0)
        {
            if (serial.BytesToRead >= expectedReplyLen)
            {
                break;
            }
            int step = 5;
            Thread.Sleep(step);
            timeoutMS -= step;
        }
#else
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMS)
            {
                if (serial.BytesToRead >= expectedReplyLen)
                    break;
            }
#endif
            if (serial.BytesToRead >= 2)
            {
                byte[] rep = new byte[2];
                for(int i = 0; i < rep.Length; i++)
                {
                    serial.Read(rep, i, 1);
                }
                if (rep[0] == 'O' && rep[1] == 'K')
                {
                    // Console.Write(".ok.");
                    ret = readFully();
                    return ret;
                }
                else if (rep[0] == 'F' && rep[1] == 'L')
                {
                    addLogLine("Command fail!");
                    ret = readFully();
                    return null;
                }
                else if (rep[0] == 'P' && rep[1] == 'D')
                {
                    int errcount = 500;
                    while(errcount-- > 0)
                    {
                        try
                        {
                            for(int i = 0; i < rep.Length; i++)
                            {
                                serial.Read(rep, i, 1);
                            }
                        }
                        catch { }
                        if(rep[0] == 'O' && rep[1] == 'K')
                            return readFully();
                        else if(rep[0] == 'P' && rep[1] == 'D')
                            addLogLine("Command pending...");
                        rep[0] = 0;
                        Thread.Sleep(20);
                    }
                    return readFully();
                }
            }
            if(expectedReplyLen != 0) addLogLine("Command timed out!");
            return null;
        }
        internal BLInfo getAndPrintInfo()
        {
            blinfo = this.getInfo();
            if (blinfo == null)
            {
                return null;
            }
            addLogLine(blinfo.ToString());
            return blinfo;
        }

        internal bool writeFlash(byte[] data, int adr, int len = -1)
        {
            try
            {
                var bufLen = 4096;
                if(len < 0)
                    len = data.Length;
                int ofs = 0;
                int startAddr = adr;
                logger.setProgress(0, len);
                doErase(adr, (len + 4095) / 4096);
                addLogLine("Starting flash write " + len);
                byte[] buffer = new byte[bufLen + 4];
                logger.setState("Writing", Color.White);
                Thread.Sleep(1000);
                while(ofs < len)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(ofs % 0x1000 == 0) addLog("." + formatHex(ofs) + ".");
                    int chunk = len - ofs;
                    if(chunk > bufLen)
                        chunk = bufLen;
                    buffer[0] = (byte)(adr & 0xFF);
                    buffer[1] = (byte)((adr >> 8) & 0xFF);
                    buffer[2] = (byte)((adr >> 16) & 0xFF);
                    buffer[3] = (byte)((adr >> 24) & 0xFF);
                    Array.Copy(data, ofs, buffer, 4, chunk);
                    int bufferLen = chunk + 4;
                    int errCnt = 0;
                    while(executeCommand(0x31, buffer, 0, bufferLen, true, 5) == null && errCnt < 20)
                        errCnt++;
                    if(errCnt >= 20)
                        throw new Exception("Read failed!");
                    ofs += chunk;
                    adr += chunk;
                    logger.setProgress(ofs, len);
                }
                addLogLine("");
                if(!CheckSHA256(startAddr, len, data))
                {
                    logger.setState("SHA mismatch!", Color.Red);
                    return false;
                }
                logger.setState("Writing done", Color.DarkGreen);
                addLogLine("Done flash write " + len);
                return true;
            }
            catch(Exception exc)
            {
                addErrorLine(exc.Message);
                return false;
            }
        }
        void executeCommandChunked(int type, byte[] parms = null, int start = 0, int len = 0)
        {
            if (len == -1)
            {
                len = parms.Length - start;
            }
            int ofs = 0;
            while (ofs < len)
            {
                int chunk = len - ofs;
                if (chunk > 4092)
                    chunk = 4092;
                this.executeCommand(type, parms, start + ofs, chunk);
                ofs += chunk;
            }
        }

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            if(serial == null)
            {
                serial = new SerialPort(serialName, baudrate);
                serial.Open();
            }
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            addLog("Port ready!" + Environment.NewLine);

            if(this.Sync() == false)
            {
                // failed
                return false;
            }
            if (this.getAndPrintInfo() == null)
            {
                flashID = readFlashID();
                if(flashID != null)
                {
                    addLogLine("Eflash loader is already uploaded!");
                    return true;
                }
                addErrorLine("Initial get info failed.");
                addErrorLine("This may happen if you don't reset between flash operations");
                addErrorLine("So, make sure that BOOT is connected, do reset (or power off/on) and try again");
                return false;
            }
            if(blinfo.Variant == BKType.BL616)
            {
                executeCommand(0x3B, new byte[] { 0x80, 0x41, 0x01, 0x00 }, 0, 4, true, 2, 0);
                // do it twice for bl616
                flashID = readFlashID();
                flashID = readFlashID();
                if(flashID == null)
                {
                    addErrorLine("Failed to get BL616 flash id!");
                    return false;
                }
                return true;
            }
            else if(blinfo.Variant != chipType)
            {
                addErrorLine($"Selected chip type is {chipType}, but current chip type is {blinfo.Variant}! Will continue anyway...");
            }
            this.loadAndRunPreprocessedImage();
            Thread.Sleep(100);
            //resync in eflash
            if(this.Sync() == false)
            {
                return false;
            }
            flashID = readFlashID();

            return true;
        }
        public bool loadAndRunPreprocessedImage()
        {
            // bl616 doesn't require flash loader, and already rom implements full protocol
            if(blinfo.Variant == BKType.BL616) return true;
            byte[] loaderBinary = chipType switch
            {
                BKType.BL702 => FLoaders.GetBinaryFromAssembly("BL702Floader"),
                _ => FLoaders.GetBinaryFromAssembly("BL602Floader"),
            };
            return loadAndRunPreprocessedImage(loaderBinary);
        }
        public bool loadAndRunPreprocessedImage(byte[] file)
        {
            addLogLine("Sending boot header...");
            // loadBootHeader
            this.executeCommand(0x11, file, 0, 176);
            addLogLine("Sending segment header...");
            // loadSegmentHeader
            this.executeCommand(0x17, file, 176, 16);
            addLogLine("Writing application to RAM...");
            this.executeCommandChunked(0x18, file, 176 + 16, -1);
            addLogLine("Checking...");
            this.executeCommand(0x19);
            addLogLine("Jumping...");
            this.executeCommand(0x1a);
            return false;
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if (doGenericSetup() == false)
            {
                return ;
            }
            if(fullRead) sectors = flashSizeMB * 256;
            doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
        }

        internal byte[] readFlash(int addr = 0, int amount = 4096)
        {
            try
            {
                var startAddr = addr;
                int startAmount = amount;
                byte[] ret = new byte[amount];
                logger.setProgress(0, startAmount);
                logger.setState("Reading", Color.White);
                addLogLine("Starting read...");
                int destAddr = 0;
                while(amount > 0)
                {
                    int length = 4096;
                    if(amount < length)
                        length = amount;

                    addLog($"Reading at 0x{destAddr:X6}... ");
                    byte[] cmdBuffer = new byte[8];
                    cmdBuffer[0] = (byte)(addr & 0xFF);
                    cmdBuffer[1] = (byte)((addr >> 8) & 0xFF);
                    cmdBuffer[2] = (byte)((addr >> 16) & 0xFF);
                    cmdBuffer[3] = (byte)((addr >> 24) & 0xFF);
                    cmdBuffer[4] = (byte)(length & 0xFF);
                    cmdBuffer[5] = (byte)((length >> 8) & 0xFF);
                    cmdBuffer[6] = (byte)((length >> 16) & 0xFF);
                    cmdBuffer[7] = (byte)((length >> 24) & 0xFF);

                    // executeCommand returns byte[]: response including at least 2 bytes header + length data
                    int rawReplyLen = 2 + 2 + length; // OK + lenght 2 bytes + bytes
                    byte[] result = this.executeCommand(0x32, cmdBuffer, 0, cmdBuffer.Length, true, 5, rawReplyLen);

                    if(result == null)
                    {
                        logger.setState("Read error", Color.Red);
                        addErrorLine("Read fail - no reply");
                        return null;
                    }

                    int dataLen = result.Length - 2;
                    if(dataLen != length)
                    {
                        //logger.setState("Read error", Color.Red);
                        addErrorLine(Environment.NewLine + "Read fail - size mismatch, will resync and continue");
                        //return null;
                        if(Sync() == false) return null;
                        continue;
                    }
                    Array.Copy(result, 2, ret, destAddr, dataLen);
                    cancellationToken.ThrowIfCancellationRequested();
                    addr += dataLen;
                    amount -= dataLen;
                    destAddr += dataLen;
                    logger.setProgress(addr, startAmount);
                }
                addLogLine("");
                if(!CheckSHA256(startAddr, startAmount, ret))
                {
                    logger.setState("SHA mismatch!", Color.Red);
                    return null;
                }
                logger.setState("Read done", Color.DarkGreen);
                addLogLine("Read complete!");
                return ret;
            }
            catch(Exception ex)
            { 
                addLogLine("");
                addErrorLine(ex.ToString());
                return null;
            }
        }

        public bool CheckSHA256(int addr, int length, byte[] data)
        {
            byte[] sha256cmd = new byte[8];
            sha256cmd[0] = (byte)(addr & 0xFF);
            sha256cmd[1] = (byte)((addr >> 8) & 0xFF);
            sha256cmd[2] = (byte)((addr >> 16) & 0xFF);
            sha256cmd[3] = (byte)((addr >> 24) & 0xFF);
            sha256cmd[4] = (byte)(length & 0xFF);
            sha256cmd[5] = (byte)((length >> 8) & 0xFF);
            sha256cmd[6] = (byte)((length >> 16) & 0xFF);
            sha256cmd[7] = (byte)((length >> 24) & 0xFF);
            byte[] sha256result = executeCommand(0x3D, sha256cmd, 0, sha256cmd.Length, true, 10, 2 + 32);
            if(sha256result == null)
            {
                addErrorLine($"Failed to get hash");
                return false;
            }
            string sha256read;
            using(var hasher = SHA256.Create())
            {
                var sha = hasher.ComputeHash(data);
                sha256read = HashToStr(sha);
            }
            var sha256flash = HashToStr(sha256result.Skip(2).ToArray());
            if(sha256flash != sha256read)
            {
                addErrorLine($"Hash mismatch!\r\nexpected\t{sha256read}\r\ngot\t{sha256flash}");
                return false;
            }
            else
            {
                addSuccess($"Hash matches {sha256read}!" + Environment.NewLine);
                return true;
            }
        }

        public bool doReadInternal(int addr = 0, int amount = 0x200000) {
            byte[] res = readFlash(addr, amount);
            if(res != null) ms = new MemoryStream(res);
            return false;
        }
        MemoryStream ms;

        public BL602Flasher(CancellationToken ct) : base(ct)
        {
        }

        public override byte[] getReadResult()
        {
            return ms?.ToArray();
        }
        public override bool doErase(int startSector, int sectors, bool bAll = false)
        {
            logger.setState("Erasing...", Color.White);
            int errcount = 100;
            if(bAll)
            {
                if (doGenericSetup() == false)
                {
                    return false;
                }
                addLogLine("Erasing...");
                executeCommand(0x3C, null, 0, 0, true, 0, 0);
                errcount = 30000;
            }
            else
            {
                if(sectors < 1)
                    return false;
                var end = sectors * BK7231Flasher.SECTOR_SIZE;
                end += startSector - 1; //end addr
                addLogLine($"Erasing from 0x{startSector:X} to 0x{(end + 1):X}");
                byte[] cmdBuffer = new byte[8];
                cmdBuffer[0] = (byte)(startSector & 0xFF);
                cmdBuffer[1] = (byte)((startSector >> 8) & 0xFF);
                cmdBuffer[2] = (byte)((startSector >> 16) & 0xFF);
                cmdBuffer[3] = (byte)((startSector >> 24) & 0xFF);
                cmdBuffer[4] = (byte)(end & 0xFF);
                cmdBuffer[5] = (byte)((end >> 8) & 0xFF);
                cmdBuffer[6] = (byte)((end >> 16) & 0xFF);
                cmdBuffer[7] = (byte)((end >> 24) & 0xFF);
                var res = executeCommand(0x30, cmdBuffer, 0, cmdBuffer.Length, true, 30);
                if(res != null) logger.setState("Erase done", Color.DarkGreen);
                else
                {
                    logger.setState("Erase failed!", Color.Red);
                    return false;
                }
            }
            serial.DiscardInBuffer();
            return true;
        }
        public override void closePort()
        {
            if (serial != null)
            {
                serial.Close();
                serial.Dispose();
            }
        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if (doGenericSetup() == false)
            {
                return;
            }
            OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
            if(rwMode == WriteMode.ReadAndWrite)
            {
                sectors = flashSizeMB * 256;
                doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
                if (ms == null)
                {
                    return;
                }
                if(saveReadResult(startSector) == false)
                {
                    return;
                }
            }
            if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
            {
                if(string.IsNullOrEmpty(sourceFileName))
                {
                    addLogLine("No filename given!");
                    return;
                }
                if(!sourceFileName.Contains("readResult"))
                {
                    if(chipType != BKType.BL602)
                    {
                        addErrorLine($"Firmware write is not supported on {chipType}. If you intend to restore a backup, make sure that file name starts with 'readResult'");
                        return;
                    }
                    //if(bOverwriteBootloader)
                    {
                        addLogLine("Writing boot...");
                        byte[] boot = FLoaders.GetBinaryFromAssembly("BL602_Boot");
                        byte[] partitions = FLoaders.GetBinaryFromAssembly(flashSizeMB > 1 ? "BL602_Partitions" : "BL602_1MBPartitions");
                        boot = MiscUtils.padArray(boot, 0xE000);
                        boot = boot.Concat(partitions).ToArray();
                        if(!writeFlash(boot, 0))
                            return;
                    }
                    addLogLine("Reading " + sourceFileName + "...");
                    byte[] data = File.ReadAllBytes(sourceFileName);
                    data = data.Concat(new byte[] { 0, 0, 0, 0 }).ToArray();
                    byte[] apphdr = FLoaders.GetBinaryFromAssembly("BL602_AppHdr");
                    apphdr[120] = (byte)(data.Length & 0xFF);
                    apphdr[121] = (byte)((data.Length >> 8) & 0xFF);
                    apphdr[122] = (byte)((data.Length >> 16) & 0xFF);
                    apphdr[123] = (byte)((data.Length >> 24) & 0xFF);
                    using(var hasher = SHA256.Create())
                    {
                        var sha = hasher.ComputeHash(data);
                        Array.Copy(sha, 0, apphdr, 132, 32);
                    }
                    var apphdrnocrc = new byte[apphdr.Length - 4];
                    Array.Copy(apphdr, 0, apphdrnocrc, 0, apphdrnocrc.Length);
                    var crc32 = CRC.crc32_ver2(0xFFFFFFFF, apphdrnocrc) ^ 0xFFFFFFFF;
                    apphdr[apphdr.Length - 1 - 3] = (byte)crc32;
                    apphdr[apphdr.Length - 1 - 2] = (byte)(crc32 >> 8);
                    apphdr[apphdr.Length - 1 - 1] = (byte)(crc32 >> 16);
                    apphdr[apphdr.Length - 1] = (byte)(crc32 >> 24);
                    byte[] wd = MiscUtils.padArray(apphdr, BK7231Flasher.SECTOR_SIZE);
                    data = wd.Concat(data).ToArray();
                    if(!writeFlash(data, 0x10000))
                        return;

                    //if(bOverwriteBootloader)
                    {
                        addLogLine("Writing dts...");
                        byte[] dts = FLoaders.GetBinaryFromAssembly("BL602_Dts");
                        if(!writeFlash(dts, flashSizeMB > 1 ? 0x1FC000 : 0xFC000))
                            return;
                    }
                }
                else
                {
                    addLogLine("Reading " + sourceFileName + "...");
                    byte[] data = File.ReadAllBytes(sourceFileName);
                    this.writeFlash(data, 0);
                }
            }
            if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
            {
                if(chipType != BKType.BL602)
                {
                    addErrorLine($"Config write is not supported on {chipType}");
                    return;
                }
                if(cfg != null)
                {
                    var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
                    var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

                    cfg.saveConfig(chipType);
                    var cfgData = cfg.getData();
                    byte[] efdata;
                    if(cfg.efdata != null)
                    {
                        try
                        {
                            efdata = EasyFlash.SaveValueToExistingEasyFlash("mY0bcFg", cfg.efdata, cfgData, areaSize, chipType);
                        }
                        catch(Exception ex)
                        {
                            addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
                            addLog(ex.Message + Environment.NewLine);
                            efdata = EasyFlash.SaveValueToNewEasyFlash("mY0bcFg", cfgData, areaSize, chipType);
                        }
                    }
                    else
                    {
                        efdata = EasyFlash.SaveValueToNewEasyFlash("mY0bcFg", cfgData, areaSize, chipType);
                    }
                    addLog("Now will also write OBK config..." + Environment.NewLine);
                    addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                    addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                    addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                    bool bOk = writeFlash(efdata, offset, areaSize);
                    if(bOk == false)
                    {
                        logger.setState("Writing error!", Color.Red);
                        addError("Writing OBK config data to chip failed." + Environment.NewLine);
                        return;
                    }
                    logger.setState("OBK config write success!", Color.Green);
                }
                else
                {
                    addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
                }
            }
            return;
        }
        bool saveReadResult(string fileName)
        {
            if (ms == null)
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

