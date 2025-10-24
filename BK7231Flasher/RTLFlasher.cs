using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    public class RTLFlasher : BaseFlasher
    {
        int timeoutMs = 200;
        int flashSizeMB = 2;
        byte[] flashID;
        bool isInFloaderMode = false;
        XMODEM xm;
        MemoryStream ms;
        
        private static readonly byte CMD_USB = 0x05; // UART Set Baud
        private static readonly byte CMD_XMD = 0x07; // Go xmodem mode (write RAM/Flash mode)
        private static readonly byte CMD_EFS = 0x17; // Erase Flash Sectors
        private static readonly byte CMD_GFS = 0x21; // FLASH Read Status Register
        private static readonly byte CMD_SFS = 0x26; // FLASH Write Status Register
        private static readonly byte CMD_CRC = 0x27; // Check Flash write checksum
        private static readonly byte CMD_RWA = 0x31; // Read dword <addr, 4 byte> -> 0x31,<dword, 4 byte>,  0x15
        private static readonly byte CMD_ABRT = 0x1B; // User Break (End xmodem mode (write RAM/Flash mode))

        public bool Connect()
        {
            serial.DtrEnable = false;
            serial.RtsEnable = true;
            Thread.Sleep(50);
            serial.DtrEnable = true;
            serial.RtsEnable = false;
            Thread.Sleep(50);
            serial.DtrEnable = false;
            return false;
        }
        public bool EraseSectorsFlash(int offset, int size)
        {
            logger.setState("Erasing...", Color.Transparent);
            int count = (size + 4095) / 4096;
            offset &= 0xfff000;

            byte[] pkt = new byte[6];
            pkt[0] = CMD_EFS;
            pkt[1] = (byte)(offset & 0xFF);
            pkt[2] = (byte)((offset >> 8) & 0xFF);
            pkt[3] = (byte)((offset >> 16) & 0xFF);
            pkt[4] = (byte)(count & 0xFF);
            pkt[5] = (byte)((count >> 8) & 0xFF);

            if (!WriteCmd(pkt))
                return false;
            return true;
        }

        public bool WriteBlockFlash(MemoryStream stream, int offset, int size)
        {
            logger.setState("Writing...", Color.Transparent);
            if (!WriteCmd(new byte[] { CMD_XMD }))
                return false;
            var result = xm.Send(stream.ToArray(), (uint)offset);
            if(result == size)
            {
                addLogLine("");
                var baud = serial.BaudRate;
                Thread.Sleep(150);
                // hack
                RestoreBaud();
                SetBaud(baud);
                if(!ChecksumVerify(offset, size, stream.ToArray()))
                {
                    return false;
                }
                addLog("Write complete!" + Environment.NewLine);
                logger.setState("Write complete!", Color.Transparent);
                logger.setProgress(size, size);
                return true;
            }
            else
            {
                addErrorLine($"Write failed! Expected sent bytes: {size}, really sent: {result}");
            }
            return false;
        }

        public bool WriteBlockMem(MemoryStream stream, int offset, int size)
        {
            if(!WriteCmd(new byte[] { CMD_XMD }))
                return false;
            return xm.Send(stream.ToArray(), (uint)offset) == size;
        }

        public override void Xm_PacketSent(int sentBytes, int total, int sequence, uint offset)
        {
            base.Xm_PacketSent(sentBytes, total, sequence, offset);
            xm.ExtraHeaderBytes = new byte[4]
            {
                (byte)(offset & 0xFF),
                (byte)((offset >> 8) & 0xFF),
                (byte)((offset >> 16) & 0xFF),
                (byte)((offset >> 24) & 0xFF),
            };
        }

        public override void Dispose()
        {
            xm.PacketSent -= Xm_PacketSent;
            base.Dispose();
        }

        public uint? FlashWrChkSum(int offset, int size)
        {
            byte[] pkt = new byte[7];
            pkt[0] = CMD_CRC;
            pkt[1] = (byte)(offset & 0xFF);
            pkt[2] = (byte)((offset >> 8) & 0xFF);
            pkt[3] = (byte)((offset >> 16) & 0xFF);
            pkt[4] = (byte)(size & 0xFF);
            pkt[5] = (byte)((size >> 8) & 0xFF);
            pkt[6] = (byte)((size >> 16) & 0xFF);

            if (!WriteCmd(pkt, CMD_CRC))
                return null;

            byte[] data = ReadBytes(4);
            if (data == null || data.Length != 4)
                return null;

            return (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
        }

        public byte? GetFlashStatus(int num = 0)
        {
            byte[] blk;
            if (num == 0)
                blk = new byte[] { CMD_GFS, 0x05, 0x01 };
            else if (num == 1)
                blk = new byte[] { CMD_GFS, 0x35, 0x01 };
            else if (num == 2)
                blk = new byte[] { CMD_GFS, 0x15, 0x01 };
            else
                return null;

            if (!WriteCmd(blk, CMD_GFS))
                return null;

            try
            {
                int read = serial.ReadByte();
                if (read >= 0)
                    return (byte)read;
                else
                    return null;
            }
            catch
            {
                return null;
            }
        }

        // Equivalent to Python SetFlashStatus(status, num=0)
        public byte? SetFlashStatus(byte status, int num = 0)
        {
            byte[] blk;
            byte statusByte = (byte)(status & 0xff);
            if (num == 0)
                blk = new byte[] { CMD_SFS, 0x01, 0x01, statusByte };
            else if (num == 1)
                blk = new byte[] { CMD_SFS, 0x31, 0x01, statusByte };
            else if (num == 2)
                blk = new byte[] { CMD_SFS, 0x11, 0x01, statusByte };
            else
                return null;

            if (WriteCmd(blk))
                return GetFlashStatus(num);

            return null;
        }
        public byte[] ReadFlashID()
        {
            addLog("Sending Flash ID Read..." + Environment.NewLine);
            byte[] cmd = new byte[] { CMD_GFS, 0x9F, 0x03 }; // CMD_GFS = 0x21, 0x9F for JEDEC ID, 0x03 for 3 bytes
            if (!WriteCmd(cmd, CMD_GFS))
            {
                addError("Error sending Flash ID command!"+Environment.NewLine);
                return null;
            }

            try
            {
                byte[] flashID = ReadBytes(3); // Read 3 bytes (Manufacturer ID, Memory Type, Capacity)
                if (flashID == null || flashID.Length != 3)
                {
                    addError("Error reading Flash ID!" + Environment.NewLine);
                    return null;
                }
                return flashID;
            }
            catch
            {
                addError("Exception while reading Flash ID!" + Environment.NewLine);
                return null;
            }
        }

        public bool Floader(int baud)
        {
            //if(!SetBaud(baud))
            //{
            //    addError("Error Set Baud!" + Environment.NewLine);
            //    return true;
            //}
            byte[] dat = Convert.FromBase64String(FLoaders.AmebaDFLoader);
            int offset = 0x00082000;
            bool skipUpload = false;
            var regSkip = new byte[0];
            switch(chipType)
            {
                case BKType.RTL8710B:
                    dat = Convert.FromBase64String(FLoaders.AmebaZ_NewFloader);
                    offset = 0x10002000;
                    regSkip = new byte[4] { 25, 32, 0, 16 };
                    break;
                case BKType.RTL8720D:
                    Convert.FromBase64String(FLoaders.AmebaDFLoader);
                    offset = 0x00082000;
                    regSkip = new byte[4] { 33, 32, 8, 0 };
                    break;
                case BKType.RTL8721DA:
                    Convert.FromBase64String(FLoaders.AmebaDplusFLoader);
                    offset = 0x00082000;
                    break;
                case BKType.RTL8720E:
                    Convert.FromBase64String(FLoaders.AmebaLiteFloader);
                    offset = 0x00082000;
                    break;
            }
            byte[] regs = ReadRegs(offset, 4);
            if(regs != null && regs.Length == 4 && regs.SequenceEqual(regSkip))
            {
                isInFloaderMode = true;
                skipUpload = true;
                addLog("RAM code is already uploaded." + Environment.NewLine);
            }
            if(!skipUpload)
            {
                addLog("Sending RAM code..." + Environment.NewLine);
                var stream = new MemoryStream(dat);
                long size = stream.Length;
                addLog(string.Format("Write Floader to SRAM at 0x{0:X8} to 0x{1:X8}" + Environment.NewLine, offset, offset + (int)size));
                if(!WriteBlockMem(stream, offset, (int)size))
                {
                    stream.Close();
                    addError("Error Write!" + Environment.NewLine);
                    RestoreBaud();
                    return true;
                }
                stream.Close();
                RestoreComBaud();
                isInFloaderMode = true;
                if(!SetBaud(baud))
                {
                    addError("Error Set Baud!" + Environment.NewLine);
                    return true;
                }
                addLog("RAM code ready!" + Environment.NewLine);
            }
            else
            {
                if(!SetBaud(baud))
                {
                    addError("Error Set Baud!" + Environment.NewLine);
                    return true;
                }
            }
            // See: https://www.elektroda.com/rtvforum/viewtopic.php?p=21606205#21606205
            // NOTE: It will not work without RAM loader
            flashID = ReadFlashID();
            if(flashID == null)
            {
                addError("Flash ID read failed, cannot proceed" + Environment.NewLine);
                return true;
            }
            flashSizeMB = (1 << (flashID[2] - 0x11)) / 8;
            addLog("Flash ID: 0x" + flashID[0].ToString("X2") + flashID[1].ToString("X2") + flashID[2].ToString("X2")+Environment.NewLine);
            addLog("Flash size is " + flashSizeMB + "MB" + Environment.NewLine);
            return false;
        }

        public bool ReadBlockFlash(MemoryStream stream, int offset, int size)
        {
            int count = (size + 4095) / 4096;
            var expectedPackets = count * 4;
            int totalRead = 0;
            offset &= 0xffffff;

            logger.setProgress(0, count);
            logger.setState("Reading...", Color.Transparent);
            if (count < 1 || count > 0x10000 || offset < 0)
            {
                addError("Bad parameters!");
                return false;
            }
            byte[] header;
            header = new byte[6];
            header[0] = 0x20;
            header[1] = (byte)(offset & 0xff);
            header[2] = (byte)((offset >> 8) & 0xff);
            header[3] = (byte)((offset >> 16) & 0xff);
            header[4] = (byte)(count & 0xFF);              // ushort (2B) - low byte
            header[5] = (byte)((count >> 8) & 0xFF);       // ushort (2B) - high byte

            try
            {
                serial.Write(header, 0, header.Length);
            }
            catch
            {
                addError("Error Write to COM Port!");
                return false;
            }

            void Xm_PacketReceived(XMODEM sender, byte[] packet, bool endOfFileDetected)
            {
                expectedPackets--;
                logger.setProgress(count * 4 - expectedPackets, count * 4);
                if((expectedPackets % 4) == 3)
                {
                    addLog($"Reading at 0x{offset:X}... ");
                }
                offset += packet.Length;
            }
            xm.PacketReceived += Xm_PacketReceived;
            try
            {
                var res = xm.Receive(stream);
                if(res != XMODEM.TerminationReasonEnum.EndOfFile)
                {
                    addErrorLine($"Read failed with {res}");
                    Thread.Sleep(100);
                    return false;
                }
            }
            finally
            {
                xm.PacketReceived -= Xm_PacketReceived;
            }
            Thread.Sleep(150);
            addLogLine("");
            if(!ChecksumVerify(offset - count * 0x1000, count * 0x1000, stream.ToArray()))
            {
                return false;
            }
            logger.setProgress(count, count);
            addLogLine("All blocks read!");
            addLogLine($"Read done for {stream.Length} bytes!");
            return true;
        }

        private bool ChecksumVerify(int startSector, int total, byte [] array)
        {
            logger.setState("Doing checksum verification...", Color.Transparent);
            addLogLine($"Starting checksum check for {total / 0x1000} sectors, starting at offset 0x{startSector:X}");
            var crc = FlashWrChkSum(startSector, total);
            var calc = CalculateChecksum(array);
            if (crc != calc)
            {
                logger.setState("Checksum mismatch!", Color.Red);
                addErrorLine("Checksum mismatch!");
                addErrorLine($"Send by RTL {formatHex(crc ?? 0)}, our checksum {formatHex(calc)}");
                if (bIgnoreCRCErr)
                {
                    addWarningLine("IgnoreCRCErr checked, bin will be saved even if there is a crc mismatch");
                    return true;
                }
                return false;
            }
            addSuccess($"Checksum matches {formatHex(calc)}!" + Environment.NewLine);
            return true;
        }

        private static uint CalculateChecksum(byte[] data)
        {
            uint checksum = 0;
            uint processed = 0;

            uint remainder = (uint)(data.Length & 3);
            uint blockCount = (uint)((data.Length - remainder) / 4);

            int offset = 0;

            for(uint i = 0; i < blockCount; i++)
            {
                uint value = BitConverter.ToUInt32(data, offset);
                checksum += value;
                offset += 4;
                processed += 4;
            }
            for(uint i = 0; i < remainder; i++)
            {
                checksum += (uint)(data[offset + i] << (int)(i * 8));
                processed++;
            }

            return checksum;
        }
        public byte[] ReadRegs(int offset, int size)
        {
            MemoryStream ms = new MemoryStream();
            while (size > 0)
            {
                byte[] pkt = new byte[5];
                pkt[0] = CMD_RWA;
                pkt[1] = (byte)(offset & 0xff);
                pkt[2] = (byte)((offset >> 8) & 0xff);
                pkt[3] = (byte)((offset >> 16) & 0xff);
                pkt[4] = (byte)((offset >> 24) & 0xff);

                try
                {
                    serial.DiscardInBuffer();
                    serial.Write(pkt, 0, 5);
                }
                catch
                {
                    addError("Error Write to COM Port!");
                    return null;
                }

                if (!WaitResp(0x31, 5))
                {
                    //addError("Error read data head id!");
                    return null;
                }

                byte[] data = ReadBytes(5);
                if (data == null || data.Length != 5 || data[4] != 0x15)
                    return null;

                ms.Write(data, 0, 4);
                size -= 4;
                offset += 4;
            }

            return ms.ToArray();
        }

        private byte[] ReadBytes(int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            int retries = 1000;

            while (offset < count && retries > 0)
            {
                try
                {
                    int read = serial.Read(buffer, offset, count - offset);
                    if (read > 0)
                    {
                        offset += read;
                    }
                    else
                    {
                        retries--;
                        Thread.Sleep(1);
                    }
                }
                catch(TimeoutException)
                {
                    continue;
                }
                catch
                {
                    return null;
                }
            }

            if (offset == count)
                return buffer;

            return null;
        }

        private bool WaitResp(byte code, int retries = 1000)
        {
            while (retries-- > 0)
            {
                try
                {
                    int val = serial.ReadByte();
                    if (false)
                    {
                        Console.WriteLine("Try " + retries + " wants " + code + " got " + val);
                    }
                    if (val == -1)
                        return false;
                    if ((byte)val == code)
                        return true;
                }
                catch
                {
                    Thread.Sleep(1);
                    // return false;
                }
            }
            return false;
        }

        private bool WriteCmd(byte[] cmd, byte ack = 0x06)
        {
            try
            {
                serial.Write(cmd, 0, cmd.Length);
                return WaitResp(ack); // ACK
            }
            catch
            {
                return false;
            }
        }

        private bool SetBaud(int baud, bool noCheck = false)
        {
            if(serial.BaudRate != baud)
            {
                int givenBaud = baud;
                int x = 0x0D;
                int[] br = { 115200, 128000, 153600, 230400, 380400, 460800, 500000, 921600, 1000000, 1382400, 1444400, 1500000, 1843200, 2000000 };
                foreach(int el in br)
                {
                    if(el >= baud)
                    {
                        baud = el;
                        break;
                    }
                    x++;
                }
                addLog("Setting baud rate " + baud + " (given as " + givenBaud + ")...");
                byte[] pkt = new byte[2];
                pkt[0] = CMD_USB;
                pkt[1] = (byte)x;
                if(noCheck)
                {
                    serial.Write(pkt, 0, pkt.Length);
                }
                else
                {
                    if(!WriteCmd(pkt))
                    {
                        addLog("... ERROR!" + Environment.NewLine);
                        return false;
                    }
                }
                addLog("... OK!" + Environment.NewLine);

                if(chipType == BKType.RTL8721DA || chipType == BKType.RTL8720E)
                {
                    SetComBaud(baud);
                    addLog("Sending baud check...");
                    if(!WriteCmd(new byte[] { CMD_XMD }))
                    {
                        addLog("... ERROR!" + Environment.NewLine);
                        return false;
                    }
                    addLog("... OK!" + Environment.NewLine);
                    return true;
                }
                else return SetComBaud(baud);
            }
            return true;
        }

        private bool SetComBaud(int baud)
        {
            try
            {
                serial.Close();
                serial.BaudRate = baud;
                serial.Open();
                serial.ReadTimeout = timeoutMs;
                serial.WriteTimeout = timeoutMs;
                Thread.Sleep(50);
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
            }
            catch
            {
                addError("Error: ReOpen COM port at " + baud);
                Environment.Exit(-1);
            }
            return true;
        }

        public bool RestoreComBaud()
        {
            return SetComBaud(chipType == BKType.RTL8710B ? 1500000 : 115200);
        }

        public bool RestoreBaud()
        {
            return SetBaud(chipType == BKType.RTL8710B ? 1500000 : 115200, true);
        }

        bool openPort()
        {
            try
            {
                serial = new SerialPort(serialName, 115200);
                serial.ReadTimeout = timeoutMs;
                serial.WriteTimeout = timeoutMs;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                xm = new XMODEM(serial, XMODEM.Variants.XModem1KChecksum);
                xm.PacketSent += Xm_PacketSent;
            }
            catch (Exception)
            {
                return true;
            }
            return false;
        }

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            if (openPort())
            {
                logger.setState("Open serial failed!", Color.Red);
                addError("Failed to open serial port!" + Environment.NewLine);
                return false;
            }
            addSuccess("Serial port open!" + Environment.NewLine);
            if (Connect() == true)
            {
                addError("Failed to connect!" + Environment.NewLine);
                return false;
            }
            RestoreComBaud();
            if(Floader(baudrate) == true)
            {
                addError("Failed to setup loader!" + Environment.NewLine);
                return false;
            }
            return true;
        }

        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
            OBKConfig cfg = mode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();

            int size = numSectors * BK7231Flasher.SECTOR_SIZE;
            if (data != null)
            {
                size = data.Length;
            }
            logger.setProgress(0, size);
            addLog(Environment.NewLine + "Starting write!" + Environment.NewLine);
            addLog("Write parms: start 0x" +
                (startSector).ToString("X2")
                + " (sector " + startSector / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
                (size).ToString("X2")
                + " (" + size / BK7231Flasher.SECTOR_SIZE + " sectors)"
                + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return true;
            }
            Console.WriteLine("Connected");
            if(mode == WriteMode.ReadAndWrite)
            {
                numSectors = flashSizeMB * 256;
                ms = readChunk(startSector, numSectors);
                if (ms == null)
                {
                    return true;
                }
                if(saveReadResult(startSector) == false)
                {
                    return true;
                }
            }
            int address = startSector * BK7231Flasher.SECTOR_SIZE;
            int count = (size + 4095) / 4096;
            int eraseSize = count * 4096;
            int eraseOffset = address & 0xfff000;
            addLog(string.Format("Erase Flash {0} sectors, data from 0x{1:X8} to 0x{2:X8}", count, eraseOffset, eraseOffset + eraseSize)+Environment.NewLine);

            if(mode != WriteMode.OnlyOBKConfig)
            {
                if(!EraseSectorsFlash(eraseOffset, size))
                {
                    addError("Error: Erase Flash sectors!");
                    RestoreBaud();
                    return true;
                }
                addLog("Erase done!" + Environment.NewLine);
            }
            if (mode == WriteMode.OnlyErase)
            {
                // skip
                addLog("Erase only finished, nothing more to do!" + Environment.NewLine);
            }
            else if(mode != WriteMode.OnlyOBKConfig)
            {
                int writeOffset = address & 0x00ffffff;
                writeOffset |= 0x08000000;
                addLog(string.Format("Write Flash data 0x{0:X8} to 0x{1:X8}", writeOffset, writeOffset + size) + Environment.NewLine);
                
                logger.setState("Writing...", Color.Transparent);
                var ms = new MemoryStream(data);
                if (!WriteBlockFlash(ms, writeOffset, size))
                {
                    logger.setState("Write error!", Color.Red);
                    addErrorLine("Write failed.");
                    RestoreBaud();
                    ms?.Dispose();
                    return true;
                }
                addLog("Write done!" + Environment.NewLine);
                ms?.Dispose();
            }

            if(cfg != null)
            {
                var offset = OBKFlashLayout.getConfigLocation(chipType, out var sectors);
                var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

                if(!EraseSectorsFlash(offset, size))
                {
                    addError("Error: Erase Flash sectors!");
                    RestoreBaud();
                    return true;
                }

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
                        addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
                        addLog(ex.Message + Environment.NewLine);
                        efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                    }
                }
                else
                {
                    efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
                }
                if(efdata == null)
                {
                    addLog("Something went wrong with EasyFlash" + Environment.NewLine);
                    return false;
                }
                ms?.Dispose();
                ms = new MemoryStream(efdata);
                addLog("Now will also write OBK config..." + Environment.NewLine);
                addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                addLog("Writing config sector " + formatHex(offset) + "...");
                offset |= 0x08000000;
                bool bOk = WriteBlockFlash(ms, offset, areaSize);
                if(bOk == false)
                {
                    logger.setState("Writing error!", Color.Red);
                    addError("Writing OBK config data to chip failed." + Environment.NewLine);
                    return false;
                }
                logger.setState("OBK config write success!", Color.Green);
            }
            else
            {
                addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
            }
            RestoreBaud();
            return false;
        }

        MemoryStream readChunk(int startSector, int sectors)
        {
            MemoryStream tempResult = new MemoryStream();
            if (!ReadBlockFlash(tempResult, startSector * BK7231Flasher.SECTOR_SIZE, sectors * BK7231Flasher.SECTOR_SIZE))
            {
                logger.setState("Reading error!", Color.Red);
                RestoreBaud();
                return null;
            }
            return tempResult;
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting read!" + Environment.NewLine);
            addLog("Read parms: start 0x" +
                (startSector * BK7231Flasher.SECTOR_SIZE).ToString("X2")
                + " (sector " + startSector + "), len 0x" +
                (sectors * BK7231Flasher.SECTOR_SIZE).ToString("X2")
                + " (" + sectors + " sectors)"
                + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return;
            }
            if(fullRead)
                sectors = flashSizeMB * 256;

            logger.setProgress(0, sectors);
            ms = readChunk(startSector, sectors);
            if (ms == null)
            {
                return;
            }
            //File.WriteAllBytes("lastRead.bin", ms.ToArray());
            logger.setState("Reading success!", Color.Green);
            addSuccess("All read!" + Environment.NewLine);
            addLog("Loaded total " + formatHex(sectors * BK7231Flasher.SECTOR_SIZE) + " bytes " + Environment.NewLine);
            RestoreBaud();
        }

        public override byte[] getReadResult()
        {
            if (ms == null)
                return null;
            return ms.ToArray();
        }

        public override bool doErase(int startSector, int sectors, bool bAll)
        {
            return doWrite(startSector, sectors, null, WriteMode.OnlyErase);
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            byte[] data = null;

            if(rwMode != WriteMode.OnlyOBKConfig)
            {
                if (string.IsNullOrEmpty(sourceFileName))
                {
                    addErrorLine("No source file set!");
                    return;
                }
                data = File.ReadAllBytes(sourceFileName);
            }
            else
            {
                startSector = OBKFlashLayout.getConfigLocation(chipType, out sectors) / BK7231Flasher.SECTOR_SIZE;
            }
            doWrite(startSector, sectors, data, rwMode);
        }

        internal bool saveReadResult(string fileName)
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

