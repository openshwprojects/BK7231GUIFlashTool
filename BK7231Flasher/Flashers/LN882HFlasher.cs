using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace BK7231Flasher
{
    public class LN882HFlasher : BaseFlasher
    {
        int timeoutMs = 2000;
        int flashSizeMB = 2;
        byte[] flashID;

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            try
            {
                serial = new SerialPort(serialName, 115200);
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
            flash_program(data,0,data?.Length ?? 0, "", true, mode);
            return false;
        }
        public void change_baudrate(int baudrate, bool wait = true)
        {
            if(baudrate == serial.BaudRate)
                return;
            addLogLine("change_baudrate: Change baudrate " + baudrate);
            serial.Write("baudrate " + baudrate + "\r\n");
            Thread.Sleep(500);
            serial.BaudRate = baudrate;
            flush_com();
            if(!wait) return;
            addLogLine("change_baudrate: Waiting for change...");

            string msg = "";
            while (!msg.Contains("RAMCODE"))
            {
                if(isCancelled) return;
                addLogLine("change_baudrate: send version... wait for:  RAMCODE");
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
                catch (TimeoutException) { msg = ""; }
            }

            addLogLine("change_baudrate: Baudrate change done");
        }

        public void flash_program(byte [] data, int ofs, int len, string filename, bool bRestoreBaud, WriteMode mode)
        {
            OBKConfig cfg = mode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
            logger.setState("Prepare write...", Color.White);
            change_baudrate(this.baudrate);
            if(mode != WriteMode.OnlyOBKConfig)
            {
                addLogLine("flash_program: will flash " + len + " bytes " + filename);
                addLogLine("flash_program: sending startaddr");
                serial.Write($"startaddr 0x{ofs:X}\r\n");
                addLogLine(serial.ReadLine().Trim());
                addLogLine(serial.ReadLine().Trim());

                addLogLine("flash_program: sending update command");
                serial.Write("upgrade\r\n");
                serial.Read(new byte[7], 0, 7);

                addLogLine("flash_program: sending file via ymodem");
                YModem modem = new YModem(serial, logger);
                int res = modem.send(data, filename, len, true, 3);
                if(res != len)
                {
                    addLogLine("flash_program: failed to flash, flashed only " +
                        res + " out of " + len + " bytes!");
                    return;
                }
                addLogLine("flash_program: sending file done");

                addLogLine("flash_program: flashed " + len + " bytes!");
                addLogLine("If you want your program to run now, disconnect boot pin and do power off and on cycle");
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
                serial.Write($"startaddr 0x{offset:X}\r\n");
                addLogLine(serial.ReadLine().Trim());
                addLogLine(serial.ReadLine().Trim());
                serial.Write("upgrade\r\n");
                serial.Read(new byte[7], 0, 7);
                YModem modem = new YModem(serial, logger);
                var res = modem.send(wd, "ObkCfg", wd.Length, true, 3);
                if(res != wd.Length)
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
            serial.Write("filecount\r\n");
            addLogLine(serial.ReadLine().Trim());
            addLogLine(serial.ReadLine().Trim());
            if(bRestoreBaud)
            {
                change_baudrate(115200);
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
            addLogLine("Sync with LN882H...");
            serial.DiscardInBuffer();

            string msg = "";
            int loops = 0;
            int attempts = 0;
            while (msg != "Mar 14 2021/00:23:32\r" && attempts++ < 500)
            {
                //Thread.Sleep(1000);
                flush_com();
                //addLogLine("sending version... waiting for:  Mar 14 2021/00:23:32");
                loops++;
                if (loops % 10 == 0 && loops>9)
                {
                    addLogLine("Still no reply - maybe you need to pull BOOT pin down or do full power off/on before next attempt");
                }
                addLog($"Sync attempt {attempts}/500 ");
                try
                {
                    if(isCancelled) return true;
                    serial.Write("version\r\n");
                    msg = serial.ReadLine();
                    if(msg.Equals("\r"))
                        msg = serial.ReadLine();
                    if(msg.Equals("RAMCODE\r"))
                    {
                        isRamcode = true;
                        break;
                    }
                    //addLogLine(msg);
                }
                catch (TimeoutException)
                {
                    addWarningLine("... failed, will retry!");
                    msg = "";
                }
            }

            //addLogLine("Connect to bootloader...");
            logger.addLog("... OK!" + Environment.NewLine, Color.Green);
            return false;
        }
        public bool upload_ram_loader()
        {
            //addLogLine("upload_ram_loader will upload " + fname + "!");
            //if (File.Exists(fname) == false)
            //{
            //    addLogLine("Can't open " + fname + "!");
            //    return true;
            //}
            if(prepareForLoaderSend(out var isRamcode))
            {
                return true;
            }
            string msg = "";
            if(!isRamcode)
            {
                byte[] dat = FLoaders.GetBinaryFromAssembly("LN882H_RamCode");
                serial.Write($"download [rambin] [0x20000000] [{dat.Length}]\r\n");
                addLogLine("Will send file via YModem");

                YModem modem = new YModem(serial, logger);
                modem.send(dat, "RAMCODE", dat.Length, false);

                addLogLine("Starting program. Wait....");
                while (msg != "RAMCODE\r")
                {
                    if(isCancelled) return true;
                    Thread.Sleep(1000);
                    serial.DiscardInBuffer();
                    addLogLine("send version... wait for:  RAMCODE");
                    serial.Write("version\r\n");
                    try
                    {
                        msg = serial.ReadLine();
                        addLogLine(msg);
                        msg = serial.ReadLine();
                        addLogLine(msg);
                    }
                    catch (TimeoutException)
                    {
                        msg = "";
                    }
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
        public bool doReadInternal(int startSector = 0x000, int size = 0x1000, bool fullRead = false, bool restoreBaud = true)
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
            flashSizeMB = (1 << (flashID[0] - 0x11)) / 8;
            addLog("Flash ID: 0x" + flashID[2].ToString("X2") + flashID[1].ToString("X2") + flashID[0].ToString("X2")+Environment.NewLine);
            addLog("Flash size is " + flashSizeMB + "MB" + Environment.NewLine);
            if(fullRead)
            {
                size = flashSizeMB * 0x100000;
            }
            change_baudrate(baudrate);

            var result = true;
            var t = Stopwatch.StartNew();
            logger.setState("Reading flash", Color.Green);
            serial.Write($"fdump 0x{startSector:X} 0x{size:X}\r\n");
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
                if(!isCancelled) logger.setProgress(size - toRead, size);
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
                if(ms.Length != size)
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
            if(result && !ChecksumVerify(startSector, size, ms.ToArray()))
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
                serial.Write($"{0x18}");
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
            return false;
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

                startSector = 0;
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
    }
}

