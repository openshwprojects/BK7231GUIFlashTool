using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
    public class RTLFlasher : BaseFlasher
    {
        private SerialPort serial;
        int timeoutMs = 200;

        enum AmbZMode
        {
            MODE_RTL = 0,
            MODE_XMD,
            MODE_UNK1 = 3,
            MODE_UNK2
        }
        AmbZMode CurrentMode = AmbZMode.MODE_UNK1;
        int flashSizeMB = 2;
        byte[] flashID;

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

            if (count > 0 && count < 0x10000 && offset >= 0)
            {
                for (int i = 0; i < count; i++)
                {
                    logger.setProgress(i, count);
                    byte[] pkt = new byte[6];
                    pkt[0] = 0x17; // CMD_EFS
                    pkt[1] = (byte)(offset & 0xFF);
                    pkt[2] = (byte)((offset >> 8) & 0xFF);
                    pkt[3] = (byte)((offset >> 16) & 0xFF);
                    pkt[4] = 0x01;
                    pkt[5] = 0x00;

                    if (!WriteCmd(pkt))
                        return false;

                    offset += 4096;
                }
                return true;
            }

            addError("Bad parameters!");
            return false;
        }
        public bool WriteBlockFlash(MemoryStream stream, int offset, int size)
        {
            return SendXmodem(stream, offset, size, 3);
        }

        public uint? FlashWrChkSum(int offset, int size)
        {
            //  size = 0x4000;
            byte[] pkt = new byte[7];
            pkt[0] = 0x27; // CMD_CRC
            pkt[1] = (byte)(offset & 0xFF);
            pkt[2] = (byte)((offset >> 8) & 0xFF);
            pkt[3] = (byte)(offset >> 16);
            pkt[4] = (byte)(size & 0xFF);
            pkt[5] = (byte)((size >> 8) & 0xFF);
            pkt[6] = (byte)((size >> 16) & 0xFF);

            if (!WriteCmd(pkt, 0x27))
                return null;

            byte[] data = ReadBytes(4);
            if (data == null || data.Length != 4)
                return null;

            return (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
        }

        private static readonly byte CMD_GFS = 0x21; // FLASH Read Status Register
        private static readonly byte CMD_SFS = 0x26; // FLASH Write Status Register
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
            if (!SetBaud(baud))
            {
                addError("Error Set Baud!"+Environment.NewLine);
                return true;
            }

            byte[] regs = ReadRegs(0x00082000, 4);
            if (regs == null || regs.Length != 4 || !(regs[0] == 33 && regs[1] == 32 && regs[2] == 8 && regs[3] == 0))
            {
                //string fname = "loaders/imgtool_flashloader_amebad.bin";
                //if(File.Exists(fname) == false)
                //{
                //    addError("Can't find " + fname);
                //    return true;
                //}
                //FileStream stream = new FileStream(fname, FileMode.Open, FileAccess.Read);
                byte[] dat = Convert.FromBase64String(FLoaders.AmebaDFLoader);
                var stream = new MemoryStream(dat);
                long size = stream.Length;
                if (size < 1)
                {
                    stream.Close();
                    addError("Error: File size = 0!");
                    RestoreBaud();
                    return true;
                }
                int offset = 0x00082000;
                addLog(string.Format("Write Floader to SRAM at 0x{0:X8} to 0x{1:X8}"+Environment.NewLine, offset, offset + (int)size));
                if (!WriteBlockMem(stream, offset, (int)size))
                {
                    stream.Close();
                    addError("Error Write!" + Environment.NewLine);
                    RestoreBaud();
                    return true;
                }
                stream.Close();
                SetComBaud(115200);
                if (!SetBaud(baud))
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
            if(chipType == BKType.RTL8710B)
            {
                header = new byte[11];
                header[0] = 60; // <
                header[1] = 66; // B
                header[2] = 72; // H
                header[3] = 66; // B
                header[4] = 72; // H
                header[5] = 0x19; // CMD_RBF 
                header[6] = (byte)(offset & 0xff);
                header[7] = (byte)((offset >> 8) & 0xff);
                header[8] = (byte)(offset / 0x10000 & 0xff);
                header[9] = (byte)(count & 0xff);
                header[10] = (byte)((count >> 8) & 0xff);
                // ensure there is no handshake bytes
                var quiet = new byte[1];
                quiet[0] = 0x06;
                serial.Write(quiet, 0, 1);
                Thread.Sleep(5);
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
            }
            else
            {
                header = new byte[6];
                header[0] = 0x20;
                header[1] = (byte)(offset & 0xff);
                header[2] = (byte)((offset >> 8) & 0xff);
                header[3] = (byte)((offset >> 16) & 0xff);
                header[4] = (byte)(count & 0xFF);              // ushort (2B) - low byte
                header[5] = (byte)((count >> 8) & 0xFF);       // ushort (2B) - high byte
            }

            try
            {
                serial.Write(header, 0, header.Length);
            }
            catch
            {
                addError("Error Write to COM Port!");
                return false;
            }

            count *= 4;
            for (int i = 0; i < count; i++)
            {
                logger.setProgress(i, count);
                if ((i & 63) == 0)
                {
                   addLog(string.Format("Read block at 0x{0:X6}...", offset));
                }
                var readSize = 1024;
                if(chipType == BKType.RTL8720D)
                {
                    if (!WaitResp(0x02)) // STX
                    {
                        addError("Error read block head id!");
                        return false;
                    }

                    byte[] hdr = ReadBytes(2);
                    if (hdr == null || hdr.Length != 2 || hdr[0] != ((i + 1) & 0xff) || ((hdr[0] ^ 0xff) != hdr[1]))
                    {
                        addError("Error read block head!");
                        return false;
                    }
                    readSize = 1025;
                }

                byte[] data = ReadBytes(readSize);
                if (data == null || data.Length != readSize)
                {
                    return false;
                }

                // no checksum for AmebaZ
                if (chipType == BKType.RTL8720D && data[readSize - 1] != CalcChecksum(data, 0, readSize - 1))
                {
                    WriteCmd(new byte[] { 0x18 }); // CAN
                    addError("Bad Checksum!");
                    return false;
                }
                if (size > readSize - 1)
                {
                    serial.Write(new byte[] { 0x06 }, 0, 1); // ACK
                    stream.Write(data, 0, chipType == BKType.RTL8710B ? readSize : readSize - 1);
                }
                else
                {
                    stream.Write(data, 0, size);
                    if(chipType != BKType.RTL8710B) WriteCmd(new byte[] { 0x18 }); // CAN
                    if ((i & 63) == 0)
                        addLog("ok. ");
                    return true;
                }

                totalRead += 1024;
                size -= 1024;
                offset += 1024;
                if ((i & 63) == 0)
                    addLog("ok. ");
            }
            
            logger.setProgress(count, count);
            addLog("All blocks read!" + Environment.NewLine);
            addLog("Read done for " + totalRead + " bytes!" + Environment.NewLine);
            return true;
        }
        public byte[] ReadRegs(int offset, int size)
        {
            MemoryStream ms = new MemoryStream();
            while (size > 0)
            {
                byte[] pkt = new byte[5];
                pkt[0] = 0x31;
                pkt[1] = (byte)(offset & 0xff);
                pkt[2] = (byte)((offset >> 8) & 0xff);
                pkt[3] = (byte)((offset >> 16) & 0xff);
                pkt[4] = (byte)((offset >> 24) & 0xff);

                try
                {
                    serial.Write(pkt, 0, 5);
                }
                catch
                {
                    addError("Error Write to COM Port!");
                    return null;
                }

                if (!WaitResp(0x31))
                {
                    addError("Error read data head id!");
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

        private byte CalcChecksum(byte[] data, int start, int length)
        {
            int sum = 0;
            for (int i = start; i < length; i++)
            {
                sum += data[i];
            }
            return (byte)(sum & 0xff);
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
                catch
                {
                    return null;
                }
            }

            if (offset == count)
                return buffer;

            return null;
        }

        private bool WaitResp(byte code)
        {
            int retries = 1000;
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

        private bool SetBaud(int baud)
        {
            if (serial.BaudRate != baud)
            {
                int givenBaud = baud;
                int x = 0x0D;
                int[] br = { 115200, 128000, 153600, 230400, 380400, 460800, 500000, 921600, 1000000, 1382400, 1444400, 1500000, 1843200, 2000000 };
                foreach (int el in br)
                {
                    if (el >= baud)
                    {
                        baud = el;
                        break;
                    }
                    x++;
                }
                addLog("Setting baud rate " + baud + " (given as "+givenBaud+")...");
                if(chipType == BKType.RTL8720D)
                {
                    byte[] pkt = new byte[2];
                    pkt[0] = 0x05;
                    pkt[1] = (byte)x;
                    if(!WriteCmd(pkt))
                    {
                        addLog("... ERROR!" + Environment.NewLine);
                        return false;
                    }
                }
                else
                {
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    byte[] pkt = new byte[5];
                    pkt[0] = 60;
                    pkt[1] = 66;
                    pkt[2] = 66;
                    pkt[3] = 0x05;
                    pkt[4] = (byte)x;
                    serial.Write(pkt, 0, pkt.Length);
                    if(!WriteCmd(pkt))
                    {
                        addLog("... ERROR!" + Environment.NewLine);
                        return false;
                    }
                }
                addLog("... OK!" + Environment.NewLine);

                return SetComBaud(baud);
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

        public bool RestoreBaud()
        {
            return SetBaud(chipType == BKType.RTL8710B ? 1500000 : 115200);
        }

        public bool WriteBlockMem(Stream stream, int offset, int size)
        {
            return SendXmodem(stream, offset, size, 3);
        }
        private bool SendXmodem(Stream stream, int offset, int size, int retry)
        {
            logger.setProgress(0, size);
            logger.setState("Writing...", Color.Transparent);
            if (!WriteCmd(new byte[] { 0x07 })) // CMD_XMD
                return false;

            //this.chk32 = 0;
            int sequence = 1;
            int initialSize = size;
            int localOfs = 0;
            while (size > 0)
            {
                if ((sequence & 63) == 1)
                {
                    addLog(string.Format("Write at 0x{0:X6}...", (int)stream.Position));
                }
                int packetSize;
                byte cmd;
                if (size <= 128)
                {
                    packetSize = 128;
                    cmd = 0x01; // SOH
                }
                else
                {
                    packetSize = 1024;
                    cmd = 0x02; // STX
                }
                localOfs += packetSize;
                int rdsize = (size < packetSize) ? size : packetSize;
                byte[] data = new byte[rdsize];
                int read = stream.Read(data, 0, rdsize);
                if (read <= 0)
                {
                    addError("send: at EOF");
                    return false;
                }

                // Pad data to packetSize with 0xFF
                byte[] paddedData = new byte[packetSize];
                for (int i = 0; i < packetSize; i++)
                {
                    if (i < read)
                        paddedData[i] = data[i];
                    else
                        paddedData[i] = 0xFF;
                }

                // Construct packet
                byte[] pkt = new byte[3 + 4 + packetSize + 1];
                pkt[0] = cmd;
                pkt[1] = (byte)sequence;
                pkt[2] = (byte)(0xFF - sequence);
                pkt[3] = (byte)(offset & 0xFF);
                pkt[4] = (byte)((offset >> 8) & 0xFF);
                pkt[5] = (byte)((offset >> 16) & 0xFF);
                pkt[6] = (byte)((offset >> 24) & 0xFF);
                for (int i = 0; i < packetSize; i++)
                    pkt[7 + i] = paddedData[i];

                pkt[7 + packetSize] = CalcChecksum(pkt, 3, pkt.Length);

                if (false)
                {
                    Console.Write("Sending packet: ");
                    for (int i = 0; i < pkt.Length; i++)
                        Console.Write(pkt[i].ToString("X2") + " ");
                    Console.WriteLine();
                }

                // Retry logic
                int errorCount = 0;
                while (true)
                {
                    if (WriteCmd(pkt))
                    {
                        sequence = (sequence + 1) % 256;
                        offset += packetSize;
                        size -= rdsize;
                        break;
                    }
                    else
                    {
                        errorCount++;
                        if (errorCount > retry)
                        {
                            logger.setState("Write error!", Color.Transparent);
                            return false;
                        }
                    }
                }
                logger.setProgress(localOfs, initialSize);
            }
            addLog("Write complete!" + Environment.NewLine);
            logger.setState("Write complete!", Color.Transparent);

            return WriteCmd(new byte[] { 0x04 }); // EOT
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
            if(chipType == BKType.RTL8720D)
            {
                addLog("Sending RAM code..." + Environment.NewLine);
                if(Floader(baudrate) == true)
                {
                    addError("Failed to setup loader!" + Environment.NewLine);
                    return false;
                }
                addLog("RAM code ready!" + Environment.NewLine);
            }
            else
            {
                serial.BaudRate = 1500000;
                if(AmbZSync() == false)
                {
                    addError("Failed to sync!" + Environment.NewLine);
                    return false;
                }
                if (!SetBaud(baudrate))
                {
                    addError("Error Set Baud!"+Environment.NewLine);
                    return true;
                }

                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
            }
            return true;
        }
        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
            OBKConfig cfg;
            if(mode == WriteMode.OnlyOBKConfig)
            {
                cfg = logger.getConfig();
            }
            else
            {
                cfg = logger.getConfigToWrite();
            }

            int size = numSectors * BK7231Flasher.SECTOR_SIZE;
            if (data != null)
            {
                size = data.Length;
            }
            logger.setProgress(0, size);
            addLog(Environment.NewLine + "Starting write!" + Environment.NewLine);
            addLog("Write parms: start 0x" +
                (startSector * BK7231Flasher.SECTOR_SIZE).ToString("X2")
                + " (sector " + startSector + "), len 0x" +
                (size).ToString("X2")
                + " (" + numSectors + " sectors)"
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

                var ms = new MemoryStream(data);
                if (!WriteBlockFlash(ms, writeOffset, size))
                {
                    addLog("Error: Write Flash!" + Environment.NewLine);
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
                byte[] efdata;
                if(cfg.efdata != null)
                {
                    try
                    {
                        efdata = EasyFlash.SaveCfgToExistingEasyFlash(cfg, areaSize, chipType);
                    }
                    catch(Exception ex)
                    {
                        addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
                        addLog(ex.Message + Environment.NewLine);
                        efdata = EasyFlash.SaveCfgToNewEasyFlash(cfg, areaSize, chipType);
                    }
                }
                else
                {
                    efdata = EasyFlash.SaveCfgToNewEasyFlash(cfg, areaSize, chipType);
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

            /*
            uint? checksum = rtl.FlashWrChkSum(writeOffset, size);
            if (checksum == null)
            {
                Console.WriteLine("Flash block checksum retrieval error!");
                rtl.RestoreBaud();
                return;
            }

            Console.WriteLine("Checksum of the written block in Flash: 0x{0:X8}", checksum.Value);*/


            // this.RestoreBaud();
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
        MemoryStream ms;
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
        bool AmbZSync(AmbZMode mode = AmbZMode.MODE_RTL)
        {
            var cancel = 0;
            int retries = 15;
            while(retries-- > 0)
            {
                byte retchar;
                try
                {
                    retchar = (byte)serial.ReadByte();
                }
                catch { continue; }
                if(retchar == 0)
                    continue;
                else if(retchar == 0x15) // NAK
                {
                    if(CurrentMode != mode)
                    {
                        if(CurrentMode < AmbZMode.MODE_UNK1)
                        {
                            switch(mode)
                            {
                                case AmbZMode.MODE_RTL:
                                    if(WriteCmd(new byte[] { 0x1b }, 0x18))
                                    {
                                        CurrentMode = AmbZMode.MODE_RTL;
                                        return true;
                                    }
                                    break;
                                case AmbZMode.MODE_XMD:
                                    if(WriteCmd(new byte[] { 0x07 }))
                                    {
                                        CurrentMode = AmbZMode.MODE_XMD;
                                        return true;
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            if(mode == AmbZMode.MODE_XMD)
                                return WriteCmd(new byte[] { 0x07 });
                        }
                        CurrentMode = AmbZMode.MODE_RTL;
                    }
                    return true;
                }
                else if(retchar == 0x18) // CAN
                {
                    if(cancel > 0)
                        continue;
                    else
                        cancel = 1;
                }
                else
                {
                    if(CurrentMode == AmbZMode.MODE_UNK1)
                    {
                        if(WriteCmd(new byte[] { 0x07 }))
                        {
                            CurrentMode = AmbZMode.MODE_XMD;
                            if(mode == AmbZMode.MODE_XMD)
                                return true;
                            if(WriteCmd(new byte[] { 0x1b }, 0x18))
                            {
                                CurrentMode = AmbZMode.MODE_RTL;
                                return true;
                            }
                        }
                        CurrentMode = AmbZMode.MODE_UNK2;
                    }
                    else if(CurrentMode == AmbZMode.MODE_UNK2)
                    {
                        if(WriteCmd(new byte[] { 0x1b }, 0x18))
                        {
                            CurrentMode = AmbZMode.MODE_RTL;
                            if(mode == AmbZMode.MODE_RTL)
                                return true;
                            if(WriteCmd(new byte[] { 0x07 }))
                            {
                                CurrentMode = AmbZMode.MODE_XMD;
                                return true;
                            }
                        }
                    }
                }
                if(CurrentMode == AmbZMode.MODE_XMD)
                {
                    var can = new byte[1];
                    can[0] = 0x06;
                    serial.Write(can, 0, 1);
                    serial.Write(can, 0, 1);
                }
            }
            return false;
        }
    }
}

