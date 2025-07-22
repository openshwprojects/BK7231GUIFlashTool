using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public class LN882HFlasher : BaseFlasher
    {
        private SerialPort serial;
        int timeoutMs = 10000;


        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            serial = new SerialPort(serialName, 115200);
            serial.ReadTimeout = timeoutMs;
            serial.WriteTimeout = timeoutMs;
            serial.Open();
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            addLog("Port ready!" + Environment.NewLine);
            return true;
        }
        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
            if (doGenericSetup() == false)
            {
                return true;
            }
            if(upload_ram_loader("loaders/LN882H_RAM_BIN.bin"))
            {
                return true;
            }
            flash_program(data,0,data.Length, "");
            return false;
        }
        public void change_baudrate(int baudrate)
        {
           addLogLine("change_baudrate: Change baudrate " + baudrate);
            serial.Write("baudrate " + baudrate + "\r\n");
            serial.ReadExisting();
            serial.BaudRate = baudrate;
           addLogLine("change_baudrate: Waiting for change...");
            flush_com();

            string msg = "";
            while (!msg.Contains("RAMCODE"))
            {
               addLogLine("change_baudrate: send version... wait for:  RAMCODE");
                Thread.Sleep(1000);
                flush_com();
                serial.Write("version\r\n");
                try
                {
                    msg = serial.ReadLine();
                   addLogLine(msg);
                    msg = serial.ReadLine();
                   addLogLine(msg);
                }
                catch (TimeoutException) { msg = ""; }
            }

           addLogLine("change_baudrate: Baudrate change done");
        }

        public void flash_program(byte [] data, int ofs, int len, string filename)
        {
           addLogLine("flash_program: will flash " + len + " bytes " + filename);
            change_baudrate(this.baudrate);
           addLogLine("flash_program: sending startaddr");
            serial.Write("startaddr 0x0\r\n");
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
                    res +  " out of " + len + " bytes!");
                return;
            }
           addLogLine("flash_program: sending file done");

            serial.Write("filecount\r\n");
           addLogLine(serial.ReadLine().Trim());
           addLogLine(serial.ReadLine().Trim());

            change_baudrate(115200);
            addLogLine("flash_program: flashed " + len + " bytes!");
            addLogLine("If you want your program to run now, disconnect boot pin and do power off and on cycle");

        }


        public override void doRead(int startSector = 0x000, int sectors = 10)
        {
            if (doGenericSetup() == false)
            {
                return ;
            }
            // doReadInternal();
            read_flash_to_file("qqq", baudrate);
        }
        public void flush_com()
        {
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
        }

        public bool upload_ram_loader_for_read(byte[] RAMCODE)
        {
            if (prepareForLoaderSend())
            {
                return true;
            }
            addLogLine("Connect to bootloader...");
            serial.Write($"download [rambin] [0x20000000] [{RAMCODE.Length}]\r\n");
            addLogLine("Will send RAMCODE");
            YModem modem = new YModem(serial, logger);

            var stream = new MemoryStream(RAMCODE);
            int ret = modem.send(stream, "RAMCODE", stream.Length, false);
            if (ret != stream.Length)
            {
               addLogLine("Ramcode upload failed, expected " + stream.Length + ", got " + ret + "!");
                return false;
            }
           addLogLine("RAMCODE send ok, starting immediately");
            return true;
        }
        bool openPort()
        {
            try
            {
                serial = new SerialPort(serialName, 115200, Parity.None, 8, StopBits.One);
            }
            catch (Exception ex)
            {
                addError("Serial port create exception: " + ex.ToString() + Environment.NewLine);
                return true;
            }
            try
            {
              //  serial.ReadBufferSize = 4096 * 2;
        //     serial.ReadBufferSize = 3000000;
            }
            catch (Exception ex)
            {
                addWarning("Setting serial port buffer size exception: " + ex.ToString() + Environment.NewLine);
            }
            try
            {
                serial.Open();
            }
            catch (Exception ex)
            {
                addError("Serial port open exception: " + ex.ToString() + Environment.NewLine);
                return true;
            }
            return false;
        }
        bool prepareForLoaderSend()
        {
            logger.setState("Connecting...", Color.White);
            addLogLine("Sync with LN882H...");
            serial.DiscardInBuffer();

            string msg = "";
            int loops = 0;
            while (msg != "Mar 14 2021/00:23:32\r")
            {
                Thread.Sleep(1000);
                flush_com();
                addLogLine("sending version... waiting for:  Mar 14 2021/00:23:32");
                serial.Write("version\r\n");
                loops++;
                if (loops % 10 == 0 && loops>9)
                {
                    addLogLine("Still no reply - maybe you need to pull BOOT pin down or do full power off/on before next attempt");
                    
                }
                try
                {
                    msg = serial.ReadLine();
                    addLogLine(msg);
                }
                catch (TimeoutException)
                {
                    msg = "";
                }
            }

            addLogLine("Connect to bootloader...");
            return false;
        }
        public bool upload_ram_loader(string fname)
        {
            addLogLine("upload_ram_loader will upload " + fname + "!");
            if (File.Exists(fname) == false)
            {
                addLogLine("Can't open " + fname + "!");
                return true;
            }
            if(prepareForLoaderSend())
            {
                return true;
            }
            serial.Write("download [rambin] [0x20000000] [37872]\r\n");
            addLogLine("Will send file via YModem");

            YModem modem = new YModem(serial, logger);
            modem.send_file(fname, false, 3);

            addLogLine("Starting program. Wait....");
            string msg = "";
            while (msg != "RAMCODE\r")
            {
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
            addLogLine("upload_ram_loader complete for " + fname + "!");

            return false;
        }
        public bool upload_ram_loader_for_read_OLD(byte[] RAMCODE)
        {
            Console.WriteLine("Sync with LN882H");
            serial.DiscardInBuffer();

            string msg = "";
            while (msg != "Mar 14 2021/00:23:32\r")
            {
                Thread.Sleep(1000);
                flush_com();
                Console.WriteLine("send version... wait for:  Mar 14 2021/00:23:32");
                serial.Write("version\r\n");
                try
                {
                    msg = serial.ReadLine();
                    Console.WriteLine(msg);
                }
                catch (TimeoutException)
                {
                    msg = "";
                }
            }

            Console.WriteLine("Connect to bootloader...");
            serial.Write($"download [rambin] [0x20000000] [{RAMCODE.Length}]\r\n");
            Console.WriteLine("Will send RAMCODE");
            YModem modem = new YModem(serial,logger);

            var stream = new MemoryStream(RAMCODE);
            int ret = modem.send(stream, "RAMCODE", stream.Length, false);
            if (ret != stream.Length)
            {
                Console.WriteLine("Ramcode upload failed, expected " + stream.Length + ", got " + ret + "!");
                return false;
            }
            Console.WriteLine("Starting immediately");
            return true;
        }
        public bool read_flash_to_file(string filename, int baudRate)
        {
            byte[] RAMCODE = LN882H_RamDumper.RAMCODE;
            switch (baudRate)
            {
                default:
                    Console.WriteLine("Not supported baud - defaulting to 115200");
                    baudRate = 115200; goto case 115200;
                case 115200:
                    Console.WriteLine("Setting up baud 115200");
                    RAMCODE[8] = 0x6F;
                    RAMCODE[9] = 0xD8;
                    RAMCODE[10] = 0xCB;
                    RAMCODE[11] = 0x1A;
                    RAMCODE[20] = 0xC5;
                    RAMCODE[21] = 0xDA;
                    RAMCODE[22] = 0x90;
                    RAMCODE[23] = 0x00;
                    RAMCODE[8226] = 0xE1;
                    RAMCODE[8227] = 0x30;
                    break;
                case 230400:
                    Console.WriteLine("Setting up baud 230400");
                    RAMCODE[8] = 0xCF;
                    RAMCODE[9] = 0x53;
                    RAMCODE[10] = 0x27;
                    RAMCODE[11] = 0xAF;
                    RAMCODE[20] = 0x2B;
                    RAMCODE[21] = 0xB4;
                    RAMCODE[22] = 0x8B;
                    RAMCODE[23] = 0x03;
                    RAMCODE[8226] = 0x61;
                    RAMCODE[8227] = 0x30;
                    break;
                case 460800:
                    Console.WriteLine("Setting up baud 460800");
                    RAMCODE[8] = 0xB0;
                    RAMCODE[9] = 0xE9;
                    RAMCODE[10] = 0x95;
                    RAMCODE[11] = 0xCB;
                    RAMCODE[20] = 0x05;
                    RAMCODE[21] = 0x07;
                    RAMCODE[22] = 0xFD;
                    RAMCODE[23] = 0x63;
                    RAMCODE[8226] = 0xE1;
                    RAMCODE[8227] = 0x20;
                    break;
                case 921600:
                    Console.WriteLine("Setting up baud 921600");
                    RAMCODE[8] = 0x10;
                    RAMCODE[9] = 0x62;
                    RAMCODE[10] = 0x79;
                    RAMCODE[11] = 0x7E;
                    RAMCODE[20] = 0xEB;
                    RAMCODE[21] = 0x69;
                    RAMCODE[22] = 0xE6;
                    RAMCODE[23] = 0x60;
                    RAMCODE[8226] = 0x61;
                    RAMCODE[8227] = 0x20;
                    break;
            }
            var isUploaded = upload_ram_loader_for_read(RAMCODE);
            if (!isUploaded)
            {
                Console.WriteLine("read_flash_to_file: failed to upload RAMCODE");
                return false;
            }
            serial.BaudRate = baudRate;
            byte[] flashsize = new byte[4];
            var addr = 0;
            var read = 0;
            var result = true;
            while (read < 4)
            {
                read += serial.Read(flashsize, read, 4 - read);
            }
            int total_flash_size = flashsize[3] << 24 | flashsize[2] << 16 | flashsize[1] << 8 | flashsize[0];
            Console.WriteLine($"Reading flash to file {filename}, flash size is {total_flash_size / 0x100000} MB");
            int packetsize = 512 + 2;
            var t = Stopwatch.StartNew();
            using (FileStream fs = new FileStream(filename, FileMode.Create))
            {
                while (addr < total_flash_size)
                {
                    byte[] buf = new byte[packetsize];
                    byte[] nocrc = new byte[packetsize - 2];
                    byte[] crc = new byte[2];
                    read = 0;
                    while (read < packetsize)
                    {
                        read += serial.Read(buf, read, packetsize - read);
                    }
                    Array.Copy(buf, nocrc, packetsize - 2);
                    Array.ConstrainedCopy(buf, packetsize - 2, crc, 0, 2);
                    ushort calc_crc = YModem.calc_crc(nocrc);
                    ushort sentcrc = (ushort)(crc[1] << 8 | crc[0]);
                    if (sentcrc != calc_crc)
                    {
                        Console.WriteLine($"CRC FAIL at {addr}");
                        result = false;
                        break;
                    }
                    addr += packetsize - 2;
                    fs.Write(buf, 0, packetsize - 2);

                }
            }
            t.Stop();
            Console.WriteLine($"\ndone in {t.Elapsed.TotalSeconds}s");
            return result;
        }

        public bool doReadInternal() { 
            byte[] RAMCODE = LN882H_RamDumper.RAMCODE;
            switch (baudrate)
            {
                default:
                   addLogLine("Not supported baud - defaulting to 115200");
                    baudrate = 115200; goto case 115200;
                case 115200:
                   addLogLine("Setting up baud 115200");
                    RAMCODE[8] = 0x6F;
                    RAMCODE[9] = 0xD8;
                    RAMCODE[10] = 0xCB;
                    RAMCODE[11] = 0x1A;
                    RAMCODE[20] = 0xC5;
                    RAMCODE[21] = 0xDA;
                    RAMCODE[22] = 0x90;
                    RAMCODE[23] = 0x00;
                    RAMCODE[8226] = 0xE1;
                    RAMCODE[8227] = 0x30;
                    break;
                case 230400:
                   addLogLine("Setting up baud 230400");
                    RAMCODE[8] = 0xCF;
                    RAMCODE[9] = 0x53;
                    RAMCODE[10] = 0x27;
                    RAMCODE[11] = 0xAF;
                    RAMCODE[20] = 0x2B;
                    RAMCODE[21] = 0xB4;
                    RAMCODE[22] = 0x8B;
                    RAMCODE[23] = 0x03;
                    RAMCODE[8226] = 0x61;
                    RAMCODE[8227] = 0x30;
                    break;
                case 460800:
                   addLogLine("Setting up baud 460800");
                    RAMCODE[8] = 0xB0;
                    RAMCODE[9] = 0xE9;
                    RAMCODE[10] = 0x95;
                    RAMCODE[11] = 0xCB;
                    RAMCODE[20] = 0x05;
                    RAMCODE[21] = 0x07;
                    RAMCODE[22] = 0xFD;
                    RAMCODE[23] = 0x63;
                    RAMCODE[8226] = 0xE1;
                    RAMCODE[8227] = 0x20;
                    break;
                case 921600:
                   addLogLine("Setting up baud 921600");
                    RAMCODE[8] = 0x10;
                    RAMCODE[9] = 0x62;
                    RAMCODE[10] = 0x79;
                    RAMCODE[11] = 0x7E;
                    RAMCODE[20] = 0xEB;
                    RAMCODE[21] = 0x69;
                    RAMCODE[22] = 0xE6;
                    RAMCODE[23] = 0x60;
                    RAMCODE[8226] = 0x61;
                    RAMCODE[8227] = 0x20;
                    break;
            }
            var isUploaded = upload_ram_loader_for_read_OLD(RAMCODE);
            if (!isUploaded)
            {
               addLogLine("read_flash_to_file: failed to upload RAMCODE");
                return false;
            }
            //flush_com();
            serial.BaudRate = baudrate;
            byte[] flashsize = new byte[4];
            var addr = 0;
            var read = 0;
            var result = true;
            int toRead = serial.BytesToRead;
            while (read < 4)
            {
                read += serial.Read(flashsize, read, 4 - read);
            }
            int total_flash_size = flashsize[3] << 24 | flashsize[2] << 16 | flashsize[1] << 8 | flashsize[0];
            int mbs = total_flash_size / 0x100000;
            int ava = serial.BytesToRead;
            addLogLine($"Reading flash at baud " + serial.BaudRate + $", flash size is {mbs} MB, available data {ava}");
            int packetsize = 512 + 2;
            var t = Stopwatch.StartNew();
            logger.setState("Reading flash", Color.Green);
            addLog("Reading..");
            int loops = 0;
            ms = new MemoryStream();
            {
                while (addr < total_flash_size)
                {
                    loops++;
                    if (loops % 50 == 0)
                    {
                        addLog(addr + "... ");
                    }
                    logger.setProgress(addr, total_flash_size);
                    byte[] buf = new byte[packetsize];
                    byte[] nocrc = new byte[packetsize - 2];
                    byte[] crc = new byte[2];
                    read = 0;
                    while (read < packetsize)
                    {
                        read += serial.Read(buf, read, packetsize - read);
                    }
                    Array.Copy(buf, nocrc, packetsize - 2);
                    Array.ConstrainedCopy(buf, packetsize - 2, crc, 0, 2);
                    ushort calc_crc = YModem.calc_crc(nocrc);
                    ushort sentcrc = (ushort)(crc[1] << 8 | crc[0]);
                    if (sentcrc != calc_crc)
                    {
                        logger.setState("CRC ERROR", Color.Red);
                        addLogLine($"CRC FAIL at {addr}, try lower baud?");
                        result = false;
                        break;
                    }
                    addr += packetsize - 2;
                    ms.Write(buf, 0, packetsize - 2);
                }
            }
            t.Stop();
            logger.setState("Reading complete!", Color.Green);
            addLogLine($"\ndone in {t.Elapsed.TotalSeconds}s");

            return result;
        }
        MemoryStream ms;
        public override byte[] getReadResult()
        {
                return ms.GetBuffer();
        }
        public override bool doErase(int startSector = 0x000, int sectors = 10)
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

