using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public class RTLFlasher : BaseFlasher
    {
        private SerialPort serial;
        int timeoutMs = 200;


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
        public bool Floader(int baud)
        {
            if (!SetBaud(baud))
            {
                addError("Error Set Baud!");
                return true;
            }

            byte[] regs = ReadRegs(0x00082000, 4);
            if (regs == null || regs.Length != 4 || !(regs[0] == 33 && regs[1] == 32 && regs[2] == 8 && regs[3] == 0))
            {
                string fname = "loaders/imgtool_flashloader_amebad.bin";
                if(File.Exists(fname) == false)
                {
                    addError("Can't find " + fname);
                    return true;
                }
                FileStream stream = new FileStream(fname, FileMode.Open, FileAccess.Read);
                long size = stream.Length;
                if (size < 1)
                {
                    stream.Close();
                    addError("Error: File size = 0!");
                    RestoreBaud();
                    return true;
                }
                int offset = 0x00082000;
                addLog(string.Format("Write SRAM at 0x{0:X8} to 0x{1:X8} from file: imgtool_flashloader_amebad.bin"+Environment.NewLine, offset, offset + (int)size));
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

            return false;
        }

        public bool ReadBlockFlash(MemoryStream stream, int offset, int size)
        {
            int count = (size + 4095) / 4096;
            offset &= 0xffffff;

            logger.setProgress(0, count);
            logger.setState("Reading...", Color.Transparent);
            if (count < 1 || count > 0x10000 || offset < 0)
            {
                addError("Bad parameters!");
                return false;
            }

            byte[] header = new byte[6];
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

            count *= 4;
            for (int i = 0; i < count; i++)
            {
                logger.setProgress(i, count);
                if ((i & 63) == 0)
                {
                   addLog(string.Format("Read block at 0x{0:X6}...", offset));
                }

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

                byte[] data = ReadBytes(1025);
                if (data == null || data.Length != 1025)
                {
                    return false;
                }

                if (data[1024] != CalcChecksum(data, 0, 1024))
                {
                    WriteCmd(new byte[] { 0x18 }); // CAN
                    addError("Bad Checksum!");
                    return false;
                }

                if (size > 1024)
                {
                    serial.Write(new byte[] { 0x06 }, 0, 1); // ACK
                    stream.Write(data, 0, 1024);
                }
                else
                {
                    stream.Write(data, 0, size);
                    WriteCmd(new byte[] { 0x18 }); // CAN
                    if ((i & 63) == 0)
                        addLog("ok");
                    return true;
                }

                size -= 1024;
                offset += 1024;
                if ((i & 63) == 0)
                    addLog("ok");
            }

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
                Console.WriteLine("Set baudrate " + baud);
                int x = 0x0D;
                int[] br = { 115200, 128000, 153600, 230400, 380400, 460800, 500000, 921600, 1000000, 1382400, 1444400, 1500000 };
                foreach (int el in br)
                {
                    if (el >= baud)
                    {
                        baud = el;
                        break;
                    }
                    x++;
                }

                byte[] pkt = new byte[2];
                pkt[0] = 0x05;
                pkt[1] = (byte)x;
                if (!WriteCmd(pkt))
                    return false;

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
            return SetBaud(115200);
        }

        public bool WriteBlockMem(FileStream stream, int offset, int size)
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

            int localOfs = 0;
            while (size > 0)
            {
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
                logger.setProgress(localOfs, size);
            }
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
            addLog("Sending RAM code..." + Environment.NewLine);
            if (Floader(baudrate) == true)
            {
                addError("Failed to setup loader!" + Environment.NewLine);
                return false;
            }
            addLog("RAM code ready!" + Environment.NewLine);
            return true;
        }
        public override void doWrite(int startSector, byte[] data)
        {
            logger.setProgress(0, data.Length);
            addLog(Environment.NewLine + "Starting write!" + Environment.NewLine);
            addLog("Write parms: start 0x" +
                (startSector).ToString("X2")
                + " (sector " + startSector / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
                (data.Length).ToString("X2")
                + " (" + startSector + " sectors)"
                + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return;
            }
            Console.WriteLine("Connected");
            int address = startSector * BK7231Flasher.SECTOR_SIZE;
            int size = data.Length;
            int count = (size + 4095) / 4096;
            int eraseSize = count * 4096;
            int eraseOffset = address & 0xfff000;
            addLog(string.Format("Erase Flash {0} sectors, data from 0x{1:X8} to 0x{2:X8}", count, eraseOffset, eraseOffset + eraseSize)+Environment.NewLine);

            if (!this.EraseSectorsFlash(eraseOffset, size))
            {
                addError("Error: Erase Flash sectors!");
                this.RestoreBaud();
                return;
            }

            int writeOffset = address & 0x00ffffff;
            writeOffset |= 0x08000000;
            addLog(string.Format("Write Flash data 0x{0:X8} to 0x{1:X8}", writeOffset, writeOffset + size) + Environment.NewLine);

            MemoryStream ms = new MemoryStream(data);
            if (!this.WriteBlockFlash(ms, writeOffset, size))
            {
                addLog("Error: Write Flash!" + Environment.NewLine);
                this.RestoreBaud();
                return;
            }
            addLog("Write done!" + Environment.NewLine);

            /*
            uint? checksum = rtl.FlashWrChkSum(writeOffset, size);
            if (checksum == null)
            {
                Console.WriteLine("Flash block checksum retrieval error!");
                rtl.RestoreBaud();
                return;
            }

            Console.WriteLine("Checksum of the written block in Flash: 0x{0:X8}", checksum.Value);*/


            this.RestoreBaud();
        }
        MemoryStream readChunk(int startSector, int sectors)
        {
            MemoryStream tempResult = new MemoryStream();
            if (!this.ReadBlockFlash(tempResult, startSector * BK7231Flasher.SECTOR_SIZE, sectors * BK7231Flasher.SECTOR_SIZE))
            {
                this.RestoreBaud();
                return null;
            }
            return tempResult;
        }
        MemoryStream ms;
        public override void doRead(int startSector = 0x000, int sectors = 10)
        {
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting read!" + Environment.NewLine);
            addLog("Read parms: start 0x" +
                (startSector).ToString("X2")
                + " (sector " + startSector / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
                (sectors * BK7231Flasher.SECTOR_SIZE).ToString("X2")
                + " (" + startSector + " sectors)"
                + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return;
            }
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
        public override bool doErase(int startSector = 0x000, int sectors = 10)
        {
            return false;
        }
        public override void closePort()
        {

        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            byte []data = File.ReadAllBytes(sourceFileName);
            doWrite(startSector, data);
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

