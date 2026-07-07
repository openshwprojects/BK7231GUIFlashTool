using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
    public class LN882HFlasher : BaseFlasher, IRomReadFlasher
    {
        int timeoutMs = 2000;
        const int eraseTimeoutMs = 300000;
        int flashSizeMB = 2;
        byte[] flashID;
        string LN882H_RomVersion = "Mar 14 2021/00:23:32\r";
        string LN8825_RomVersion = "Jun 19 2019/21:01:04\r";
        const string CommandPass = "pppp";
        const string CommandFail = "ffff";
        const int LN882H_ROM_SIZE = 0x20000;
        const int LN882H_EFUSE_PAYLOAD_SIZE = 0x40;
        const int LN882H_FLASH_OTP_PAYLOAD_SIZE = 0x400;
        const int LN882H_FIXED_DUMP_CRC_SIZE = 2;
        const int LN882H_SPECIAL_READ_TIMEOUT_MS = 5000;

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            try
            {
                serial = new SerialPort(serialName, 117000);
                serial.ReadTimeout = timeoutMs;
                serial.WriteTimeout = timeoutMs;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
                {
                    ReceiverTimeoutMillisec = 1000,
                };
            }
            catch(Exception ex)
            {
                addLog("Port setup failed with "+ex.Message+"!" + Environment.NewLine);
                return false;
            }
            addLog("Port ready!" + Environment.NewLine);
            return true;
        }
        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
            if (doGenericSetup() == false)
            {
                return true;
            }
            if(upload_ram_loader())
            {
                return true;
            }
            if(mode == WriteMode.ReadAndWrite)
            {
                if(!doReadInternal(startSector, 0, true, false))
                {
                    return true;
                }
                if(saveReadResult(startSector) == false)
                {
                    return true;
                }
            }
            flash_program(data,startSector,data?.Length ?? 0, "", true, mode);
            return false;
        }
        public void change_baudrate(int baudrate, bool wait = true)
        {
            if(baudrate == serial.BaudRate || isCancelled)
                return;
            addLogLine($"Setting baud rate {baudrate}...");
            serial.Write("baudrate " + baudrate + "\r\n");
            Thread.Sleep(500);
            serial.BaudRate = baudrate;
            flush_com();
            if(!wait) return;
            addLogLine("Resyncing...");

            string msg = "";
            int attempts = 0;
            while (!msg.Contains("RAMCODE") && attempts++ < 500)
            {
                if(isCancelled) return;
                if(attempts > 1) addWarningLine("... failed, will retry!");
                addLog($"Sync attempt {attempts}/500 ");
                //Thread.Sleep(1000);
                flush_com();
                serial.Write("version\r\n");
                try
                {
                    msg = serial.ReadLine();
                    //addLogLine(msg);
                    msg = serial.ReadLine();
                    //addLogLine(msg);
                }
                catch(TimeoutException)
                {
                    msg = "";
                }
                catch(Exception ex)
                {
                    addErrorLine(ex.Message);
                    return;
                }
            }
            logger.addLog("... OK!" + Environment.NewLine, Color.Green);
        }

        public void flash_program(byte [] data, int ofs, int len, string filename, bool bRestoreBaud, WriteMode mode)
        {
            try
            {
                xm.PacketSent += Xm_PacketSent;
                OBKConfig cfg = mode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
                logger.setState("Prepare write...", Color.White);
                change_baudrate(this.baudrate);
                if(mode != WriteMode.OnlyOBKConfig)
                {
                    addLogLine("flash_program: will flash " + len + " bytes " + filename);
                    var sw = new Stopwatch();
                    sw.Start();
                    if(bUseCompressionIfPossible)
                    {
                        var compData = Compress(data);
                        addLogLine($"Using compression, writing {compData.Length} bytes, compression rate - {((double)data.Length - compData.Length) / data.Length * 100.0:F2}%");
                        PreWrite(ofs, len, true);
                        int res = xm.Send(compData, (uint)ofs);
                        addLogLine();
                        if(res != compData.Length)
                        {
                            addLogLine($"flash_program: failed to flash ({xm.TerminationReason}), flashed only " +
                                res + " out of " + compData.Length + " bytes!");
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    else if(chipType == BKType.LN882H)
                    {
                        PreWrite(ofs);
                        YModem modem = new YModem(serial, logger);
                        int res = modem.send(data, filename, len, true);
                        if(res != len)
                        {
                            addLogLine("flash_program: failed to flash, flashed only " +
                                res + " out of " + len + " bytes!");
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    else
                    {
                        PreWrite(ofs, data.Length, true);
                        int res = xm.Send(data, (uint)ofs);
                        addLogLine();
                        if(res != data.Length)
                        {
                            addLogLine("flash_program: failed to flash, flashed only " +
                                res + " out of " + len + " bytes!");
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    sw.Stop();
                    logger.setProgress(1, 1);
                    addLogLine($"flash_program: sending file done in {sw.ElapsedMilliseconds}ms");

                    addLogLine("flash_program: flashed " + len + " bytes!");
                    if(!ChecksumVerify(ofs, len, data))
                    {
                        change_baudrate(115200, false);
                        return;
                    }
                    addLogLine("If you want your program to run now, disconnect boot pin and do power off and on cycle");
                    logger.setState("Write done", Color.DarkGreen);
                }
                if(cfg != null && !isCancelled)
                {
                    var offset = (uint)OBKFlashLayout.getConfigLocation(chipType, out var sectors);
                    var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;
                    cfg.saveConfig(chipType);
                    byte[] wd = MiscUtils.padArray(cfg.getData(), BK7231Flasher.SECTOR_SIZE);
                    ms?.Dispose();
                    ms = new MemoryStream(wd);
                    addLog("Now will also write OBK config..." + Environment.NewLine);
                    addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                    addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                    addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                    if(bUseCompressionIfPossible)
                    {
                        var compData = Compress(wd);
                        addLogLine($"Using compression, writing {compData.Length} bytes, compression rate - {((double)wd.Length - compData.Length) / wd.Length * 100.0:F2}%");
                        PreWrite((int)offset, wd.Length, true);
                        int res = xm.Send(compData, offset);
                        addLogLine("");
                        if(res != compData.Length)
                        {
                            logger.setState("Writing error!", Color.Red);
                            addError("Writing OBK config data to chip failed." + Environment.NewLine);
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    else if(chipType == BKType.LN882H)
                    {
                        PreWrite((int)offset);
                        YModem modem = new YModem(serial, logger);
                        var res = modem.send(wd, "ObkCfg", wd.Length, true, 3);
                        if(res != wd.Length)
                        {
                            logger.setState("Writing error!", Color.Red);
                            addError("Writing OBK config data to chip failed." + Environment.NewLine);
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    else
                    {
                        PreWrite((int)offset, wd.Length, true);
                        int res = xm.Send(wd, offset);
                        addLogLine("");
                        if(res != wd.Length)
                        {
                            logger.setState("Writing error!", Color.Red);
                            addError("Writing OBK config data to chip failed." + Environment.NewLine);
                            change_baudrate(115200, false);
                            return;
                        }
                    }
                    if(!ChecksumVerify((int)offset, wd.Length, wd))
                    {
                        change_baudrate(115200, false);
                        return;
                    }
                    logger.setState("OBK config write success!", Color.Green);
                }
                else
                {
                    addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
                }
                //serial.Write("filecount\r\n");
                //addLogLine(serial.ReadLine().Trim());
                //addLogLine(serial.ReadLine().Trim());
                if(bRestoreBaud)
                {
                    change_baudrate(115200, false);
                }
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
            }
            finally
            {
                xm.PacketSent -= Xm_PacketSent;
            }
        }


        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if (doGenericSetup() == false)
            {
                return ;
            }
            if(upload_ram_loader())
            {
                return;
            }
            doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE, fullRead);
        }
        public void flush_com()
        {
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
        }
        bool prepareForLoaderSend(out bool isRamcode)
        {
            isRamcode = false;
            logger.setState("Connecting...", Color.White);
            addLogLine($"Sync with {chipType}...");
            serial.DiscardInBuffer();

            string msg = "";
            int loops = 0;
            int attempts = 0;
            int maxAttempts = 100;
            string ver = chipType == BKType.LN882H ? LN882H_RomVersion : LN8825_RomVersion;
            while (msg != ver && attempts++ < maxAttempts)
            {
                if(attempts > 1) addWarningLine("... failed, will retry!");
                //Thread.Sleep(1000);
                flush_com();
                //addLogLine($"sending version... waiting for: {ver}");
                loops++;
                if (loops % 10 == 0 && loops>9)
                {
                    addLogLine("Still no reply - maybe you need to pull BOOT pin down or do full power off/on before next attempt");
                }
                addLog($"Sync attempt {attempts}/{maxAttempts} ");
                try
                {
                    if(isCancelled) return true;
                    serial.Write("version\r\n");
                    msg = serial.ReadLine();
                    if(msg.Equals("\r"))
                        msg = serial.ReadLine();
                    if(msg.Equals("RAMCODE\r"))
                    {
                        serial.BaudRate = 115200;
                        isRamcode = true;
                        break;
                    }
                    if(msg.Equals(LN882H_RomVersion) && chipType != BKType.LN882H)
                    {
                        addLogLine($"... fail!");
                        throw new Exception($"Selected chip type is {chipType}, but current chip is {BKType.LN882H}");
                    }
                    else if(msg.Equals(LN8825_RomVersion) && chipType != BKType.LN8825)
                    {
                        addLogLine($"... fail!");
                        throw new Exception($"Selected chip type is {chipType}, but current chip is {BKType.LN8825}");
                    }
                    //addLogLine(msg);
                }
                catch(TimeoutException)
                {
                    msg = "";
                }
                catch(Exception ex)
                {
                    addErrorLine(ex.Message);
                    return true;
                }
            }
            if(attempts >= maxAttempts)
            {
                addErrorLine($"... failed!");
                return true;
            }
            else
            {
                logger.addLog("... OK!" + Environment.NewLine, Color.Green);
            }
            return false;
        }
        public bool upload_ram_loader()
        {
            if(prepareForLoaderSend(out var isRamcode))
            {
                return true;
            }
            string msg = "";
            if(!isRamcode)
            {
                string name = chipType == BKType.LN882H ? "LN882H_RamCode" : "LN88xx_RamCode";
                byte[] dat = FLoaders.GetBinaryFromAssembly(name);
                serial.Write($"download [rambin] [0x20000000] [{dat.Length}]\r\n");
                addLogLine("Uploading RAM code");

                YModem modem = new YModem(serial, logger);
                var sent = modem.send(dat, "RAMCODE", dat.Length, false);
                if(sent < 0)
                {
                    addErrorLine(Environment.NewLine + "Failed to upload ramcode!");
                    return true;
                }
                serial.BaudRate = 115200;
                int attempts = 0;
                int maxAttempts = 100;
                while (msg != "RAMCODE\r" && attempts++ < maxAttempts)
                {
                    if(attempts > 1) addWarningLine("... failed, will retry!");
                    if(isCancelled) return true;
                    Thread.Sleep(1000);
                    serial.DiscardInBuffer();
                    //addLogLine("send version... wait for:  RAMCODE");
                    serial.Write("version\r\n");
                    addLog($"Sync attempt {attempts}/{maxAttempts} ");
                    try
                    {
                        msg = serial.ReadLine();
                        //addLogLine(msg);
                        msg = serial.ReadLine();
                        //addLogLine(msg);
                    }
                    catch (TimeoutException)
                    {
                        msg = "";
                    }
                }
                if(attempts >= maxAttempts)
                {
                    addErrorLine($"... failed!");
                    return true;
                }
                else
                {
                    logger.addLog("... OK!" + Environment.NewLine, Color.Green);
                }
            }
            else
            {
                addLogLine("RAMCODE already uploaded");
                return false;
            }
            serial.Write("flash_uid\r\n");
            try
            {
                msg = serial.ReadLine();
                msg = serial.ReadLine();
                addLogLine(msg.Trim());
            }
            catch (TimeoutException)
            {
                addLogLine("Timeout on flash_uid");
                return true;
            }
            addLogLine("upload_ram_loader complete!");

            return false;
        }

        public byte[] ReadRomTarget(RomReadTarget target)
        {
            try
            {
                if(target == null)
                {
                    addError("No ROM reader target selected." + Environment.NewLine);
                    return null;
                }
                if((chipType != BKType.LN882H && chipType != BKType.LN8825) || target.Platform != chipType)
                {
                    addError("LN882x ROM reader target is not supported by this flasher." + Environment.NewLine);
                    return null;
                }
                if(doGenericSetup() == false)
                {
                    return null;
                }
                if(upload_ram_loader())
                {
                    return null;
                }
                change_baudrate(baudrate);
                string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
                switch(target.Kind)
                {
                    case RomReadKind.Rom:
                        return ReadLn882hRom(target.Address ?? 0, target.Length ?? LN882H_ROM_SIZE, targetKindName);
                    case RomReadKind.Otp:
                        return ReadLn882hFixedDump("otp_dump", target.Length ?? LN882H_FLASH_OTP_PAYLOAD_SIZE,
                            target.ReadTrailerLength, target.ReadTrailerName, "flash OTP", targetKindName);
                    case RomReadKind.Efuse:
                        return ReadLn882hFixedDump("efuse_dump", target.Length ?? LN882H_EFUSE_PAYLOAD_SIZE,
                            target.ReadTrailerLength, target.ReadTrailerName, "eFuse", targetKindName);
                    default:
                        addError("Selected LN882x ROM reader target is not implemented." + Environment.NewLine);
                        return null;
                }
            }
            catch(Exception ex)
            {
                string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
                addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
                logger.setState(targetKindName + " read failed.", Color.Red);
                return null;
            }
        }

        byte[] ReadLn882hRom(int offset, int length, string targetKindName)
        {
            if(offset < 0 || length <= 0)
            {
                throw new ArgumentOutOfRangeException("length", chipType + " ROM read range is outside the supported range.");
            }
            return ReadLn882hXmodemDump($"fdump 0x{offset:X} 0x{length:X} 1", offset, length, "Reading " + targetKindName + "...", targetKindName);
        }

        byte[] ReadLn882hFixedDump(string command, int payloadLength, int trailerLength, string trailerName, string label, string targetKindName)
        {
            if(payloadLength <= 0 || trailerLength < 0)
            {
                throw new ArgumentOutOfRangeException("payloadLength", chipType + " dump length is invalid.");
            }
            int wireLength = payloadLength + trailerLength;
            logger.setState("Reading " + targetKindName + "...", Color.Transparent);
            logger.setProgress(0, wireLength);
            addLogLine("Reading " + chipType + " " + label + " via " + command + ".");
            flush_com();
            serial.Write(command + "\r\n");
            byte[] result = ReadLn882hSerialBytes(wireLength, LN882H_SPECIAL_READ_TIMEOUT_MS);
            logger.setProgress(wireLength, wireLength);
            if(trailerLength == LN882H_FIXED_DUMP_CRC_SIZE)
            {
                addLogLine("Returned " + (string.IsNullOrEmpty(trailerName) ? "trailer" : trailerName) + ": " +
                    formatHex((ushort)(result[payloadLength] | (result[payloadLength + 1] << 8))));
            }
            else if(trailerLength > 0)
            {
                addLogLine("Returned " + trailerLength + " " +
                    (string.IsNullOrEmpty(trailerName) ? "trailer" : trailerName) + " byte(s).");
            }
            logger.setState(targetKindName + " read success!", Color.Green);
            if(trailerLength == 0)
            {
                return result;
            }
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(result, 0, payload, 0, payloadLength);
            return payload;
        }

        byte[] ReadLn882hXmodemDump(string command, int offset, int size, string stateText, string targetKindName)
        {
            logger.setState(stateText, Color.Green);
            logger.setProgress(0, size);
            addLogLine("Sending command: " + command);
            flush_com();
            serial.Write(command + "\r\n");
            using(MemoryStream dump = new MemoryStream())
            {
                int toRead = size;
                int currentOffset = offset;
                void Xm_PacketReceived(XMODEM sender, byte[] packet, bool endOfFileDetected)
                {
                    if(((size - toRead) % 0x1000) == 0)
                    {
                        addLog($"0x{currentOffset:X}... ");
                    }
                    currentOffset += packet.Length;
                    toRead -= packet.Length;
                    if(!isCancelled) logger.setProgress(size - toRead, size);
                }
                xm.PacketReceived += Xm_PacketReceived;
                try
                {
                    var res = xm.Receive(dump);
                    if(res != XMODEM.TerminationReasonEnum.EndOfFile)
                    {
                        throw new IOException(chipType + " dump failed with " + res);
                    }
                }
                finally
                {
                    addLog(Environment.NewLine);
                    xm.PacketReceived -= Xm_PacketReceived;
                }
                if(dump.Length != size)
                {
                    throw new IOException("Read " + dump.Length + " bytes, but expected " + size + ".");
                }
                logger.setState(targetKindName + " read success!", Color.Green);
                return dump.ToArray();
            }
        }

        byte[] ReadLn882hSerialBytes(int expectedLength, int readTimeout)
        {
            byte[] result = new byte[expectedLength];
            int offset = 0;
            int oldReadTimeout = serial.ReadTimeout;
            serial.ReadTimeout = 500;
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                while(!isCancelled && offset < expectedLength && sw.ElapsedMilliseconds < readTimeout)
                {
                    try
                    {
                        int read = serial.Read(result, offset, expectedLength - offset);
                        if(read > 0)
                        {
                            offset += read;
                            logger.setProgress(offset, expectedLength);
                        }
                    }
                    catch(TimeoutException)
                    {
                        continue;
                    }
                }
                if(isCancelled)
                {
                    throw new OperationCanceledException("Read cancelled by user.");
                }
                if(offset == expectedLength)
                {
                    return result;
                }
                throw new TimeoutException("Timed out waiting for " + chipType + " dump response.");
            }
            finally
            {
                serial.ReadTimeout = oldReadTimeout;
            }
        }

        public bool doReadInternal(int startSector = 0x000, int size = 0x1000, bool fullRead = false, bool restoreBaud = true)
        {
            if(fullRead)
            {
                serial.Write("flash_id\r\n");
                try
                {
                    flashID = new byte[4];
                    for(int i = 0; i < flashID.Length; i++)
                    {
                        serial.Read(flashID, i, 1);
                    }
                }
                catch
                {
                    addLogLine("Error on flash_id");
                    return false;
                }
                if(flashID[0] < 0x11 || flashID[0] > 0x22) throw new Exception("Flash ID incorrect!");
                flashSizeMB = (1 << (flashID[0] - 0x11)) / 8;
                addLog("Flash ID: 0x" + flashID[2].ToString("X2") + flashID[1].ToString("X2") + flashID[0].ToString("X2")+Environment.NewLine);
                addLog("Flash size is " + flashSizeMB + "MB" + Environment.NewLine);
                size = flashSizeMB * 0x100000;
            }
            change_baudrate(baudrate);

            var result = true;
            var t = Stopwatch.StartNew();
            logger.setState("Reading flash", Color.Green);
            logger.setProgress(0, 1);
            if(bUseCompressionIfPossible)
            {
                serial.Write($"fdumpz 0x{startSector:X} 0x{size:X}\r\n");
            }
            else
            {
                serial.Write($"fdump 0x{startSector:X} 0x{size:X} 0\r\n");
            }
            ms?.Dispose();
            ms = new MemoryStream();
            var offset = startSector;
            int count = (size + 4095) / 4096;
            var toRead = size;
            addLog($"Reading ");
            void Xm_PacketReceived(XMODEM sender, byte[] packet, bool endOfFileDetected)
            {
                if(((size - toRead) % 0x1000) == 0)
                {
                    addLog($"0x{offset:X}... ");
                }
                offset += packet.Length;
                toRead -= packet.Length;
                if(!isCancelled && !bUseCompressionIfPossible) logger.setProgress(size - toRead, size);
            }
            xm.PacketReceived += Xm_PacketReceived;
            try
            {
                var res = xm.Receive(ms);
                if(res != XMODEM.TerminationReasonEnum.EndOfFile)
                {
                    addErrorLine($"{Environment.NewLine}Read failed with {res}");
                    Thread.Sleep(100);
                    result = false;
                }
                if(ms.Length != size && !bUseCompressionIfPossible)
                {
                    addError($"Read {ms.Length} bytes, but expected {size}! Try with lower baud rate?");
                    result = false;
                }
            }
            finally
            {
                addLog(Environment.NewLine);
                xm.PacketReceived -= Xm_PacketReceived;
            }
            var ret = ms.ToArray();
            if(bUseCompressionIfPossible)
            {
                ret = Decompress(ret);
                addLogLine($"Uncompressed, compression ratio {((double)ret.Length - ms.Length) / ret.Length * 100.0:F2}%");
                ms?.Dispose();
                ms = new MemoryStream(ret);
            }
            if(result && !ChecksumVerify(startSector, size, ret))
            {
                result = false;
            }
            t.Stop();
            if(result)
            {
                logger.setState("Read complete!", Color.Green);
            }
            else
            {
                logger.setState("Read error!", Color.Red);
                Thread.Sleep(100);
                ms?.Dispose();
                ms = null;
            }
            addLogLine($"done in {t.Elapsed.TotalSeconds}s");
            if(restoreBaud || !result) change_baudrate(115200, false);
            return result;
        }
        MemoryStream ms;

        public LN882HFlasher(CancellationToken ct) : base(ct)
        {
        }

        public override byte[] getReadResult()
        {
            return ms?.ToArray();
        }
        public override bool doErase(int startSector, int sectors, bool bAll)
        {
            if(doGenericSetup() == false)
            {
                return false;
            }
            if(upload_ram_loader())
            {
                return false;
            }

            try
            {
                logger.setState("Erasing flash...", Color.White);
                change_baudrate(this.baudrate);

                bool result;
                if(bAll)
                {
                    addLogLine("Full chip erase requested for " + chipType + ".");
                    result = SendCommandExpectResult("ferase_all", eraseTimeoutMs);
                }
                else
                {
                    int offset = startSector;
                    int length = sectors * BK7231Flasher.SECTOR_SIZE;
                    addLogLine($"Flash erase requested for offset 0x{offset:X}, length 0x{length:X}.");
                    result = SendCommandExpectResult($"ferase 0x{offset:X} 0x{length:X}", eraseTimeoutMs);
                }

                logger.setState(result ? "Erase complete!" : "Erase failed!", result ? Color.DarkGreen : Color.Red);
                if(result)
                {
                    addSuccess("Erase complete!" + Environment.NewLine);
                }
                else
                {
                    addError("Erase failed!" + Environment.NewLine);
                }
                return result;
            }
            catch(Exception ex)
            {
                addErrorLine("Erase failed: " + ex.Message);
                logger.setState("Erase failed!", Color.Red);
                return false;
            }
            finally
            {
                if(serial != null && serial.IsOpen)
                {
                    try { change_baudrate(115200, false); }
                    catch(Exception ex) { addWarningLine("Could not restore baud rate after erase: " + ex.Message); }
                }
            }
        }
        public override void closePort()
        {
            if (serial != null)
            {
                serial.Close();
                serial.Dispose();
                serial = null;
            }
        }
        public override void Dispose()
        {
            if(xm != null)
            {
                xm.PacketSent -= Xm_PacketSent;
            }
            ms?.Dispose();
            ms = null;
            closePort();
            base.Dispose();
        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            byte[] data = null;
            if (rwMode != WriteMode.OnlyOBKConfig)
            {
                if (string.IsNullOrEmpty(sourceFileName))
                {
                    addLogLine("No filename given!");
                    return;
                }
                addLog("Reading file " + sourceFileName + "..." + Environment.NewLine);
                data = File.ReadAllBytes(sourceFileName);
                if (data == null)
                {
                    addError("Failed to open " + sourceFileName + "..." + Environment.NewLine);
                    return;
                }
                addSuccess("Loaded " + data.Length + " bytes from " + sourceFileName + "..." + Environment.NewLine);

                doWrite(startSector, 0, data, rwMode);

            }
            else
            {
                startSector = OBKFlashLayout.getConfigLocation(chipType, out sectors);
                doWrite(startSector, sectors, data, rwMode);
            }
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

        public uint GetCRC32(int offset, int size)
        {
            serial.Write($"flash_crc32 0x{offset:X} 0x{size:X}\r\n");
            var data = new byte[4];
            for(int i = 0; i < data.Length; i++)
            {
                serial.Read(data, i, 1);
            }
            return (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
        }

        private bool ChecksumVerify(int startSector, int total, byte[] array)
        {
            logger.setState("Doing CRC verification...", Color.Transparent);
            addLogLine($"Starting CRC check for {total / 0x1000} sectors, starting at offset 0x{startSector:X}");
            var crc = GetCRC32(startSector, total);
            var calc = CRC.crc32_ver2(0xFFFFFFFF, array) ^ 0xFFFFFFFF;
            if(crc != calc)
            {
                logger.setState("CRC mismatch!", Color.Red);
                addErrorLine("CRC mismatch!");
                addErrorLine($"Sent by LN {formatHex(crc)}, our CRC {formatHex(calc)}");
                if(bIgnoreCRCErr)
                {
                    addWarningLine("IgnoreCRCErr checked, bin will be saved even if there is a crc mismatch");
                    return true;
                }
                return false;
            }
            addSuccess($"CRC matches {formatHex(calc)}!" + Environment.NewLine);
            return true;
        }

        private void PreWrite(int startAddr, int size = 0, bool xm_mode = false)
        {
            if(xm_mode)
            {
                serial.Write($"fwrite{(bUseCompressionIfPossible ? "z" : "")} 0x{startAddr:X} 0x{size:X}\r\n");
                return;
            }
            serial.Write($"startaddr 0x{startAddr:X}\r\n");
            addLogLine(serial.ReadLine().Trim());
            addLogLine(serial.ReadLine().Trim());
            serial.Write("upgrade\r\n");
            serial.Read(new byte[7], 0, 7);
        }

        private bool SendCommandExpectResult(string command, int readTimeout)
        {
            addLogLine("Sending command: " + command);
            flush_com();
            int oldReadTimeout = serial.ReadTimeout;
            serial.ReadTimeout = 1000;
            try
            {
                serial.Write(command + "\r\n");
                string response = "";
                Stopwatch sw = Stopwatch.StartNew();
                while(!isCancelled && sw.ElapsedMilliseconds < readTimeout)
                {
                    try
                    {
                        int ch = serial.ReadByte();
                        if(ch < 0)
                        {
                            continue;
                        }
                        response += (char)ch;
                        if(response.Length > 64)
                        {
                            response = response.Substring(response.Length - 64);
                        }
                        if(response.Contains(CommandPass))
                        {
                            addLogLine("Command result: " + CommandPass);
                            return true;
                        }
                        if(response.Contains(CommandFail))
                        {
                            addLogLine("Command result: " + CommandFail);
                            return false;
                        }
                    }
                    catch(TimeoutException)
                    {
                        continue;
                    }
                }
                if(isCancelled)
                {
                    addWarningLine("Erase cancelled by user.");
                    return false;
                }
                addErrorLine("Timed out waiting for erase result.");
                return false;
            }
            finally
            {
                serial.ReadTimeout = oldReadTimeout;
            }
        }
    }
}

