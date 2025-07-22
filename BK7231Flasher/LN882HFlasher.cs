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
        int timeoutMs = 200;


        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            addLog("Going to open port: " + serialName + "." + Environment.NewLine);
            if (openPort())
            {
                addLog("Failed to open port" + Environment.NewLine);
                return false;
            }
            addLog("Port ready!" + Environment.NewLine);
            return true;
        }
        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
         
            return false;
        }

        public override void doRead(int startSector = 0x000, int sectors = 10)
        {
            if (doGenericSetup() == false)
            {
                return ;
            }
            doReadInternal();
        }
        public void flush_com()
        {
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
        }

        public bool upload_ram_loader_for_read(byte[] RAMCODE)
        {
           addLogLine("Sync with LN882H");
            serial.DiscardInBuffer();

            string msg = "";
            while (msg != "Mar 14 2021/00:23:32\r")
            {
                Thread.Sleep(1000);
                flush_com();
                addLogLine("send version... wait for:  Mar 14 2021/00:23:32");
                serial.Write("version\r\n");
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
            serial.Write($"download [rambin] [0x20000000] [{RAMCODE.Length}]\r\n");
            addLogLine("Will send RAMCODE");
            YModem modem = new YModem(serial);

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
                serial.ReadBufferSize = 4096 * 2;
                serial.ReadBufferSize = 3000000;
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
            var isUploaded = upload_ram_loader_for_read(RAMCODE);
            if (!isUploaded)
            {
               addLogLine("read_flash_to_file: failed to upload RAMCODE");
                return false;
            }
            serial.BaudRate = baudrate;
            byte[] flashsize = new byte[4];
            var addr = 0;
            var read = 0;
            var result = true;
            while (read < 4)
            {
                read += serial.Read(flashsize, read, 4 - read);
            }
            int total_flash_size = flashsize[3] << 24 | flashsize[2] << 16 | flashsize[1] << 8 | flashsize[0];
           addLogLine($"Reading flash, flash size is {total_flash_size / 0x100000} MB");
            int packetsize = 512 + 2;
            var t = Stopwatch.StartNew();
            logger.setState("Reading flash", Color.Green);
            ms = new MemoryStream();
            {
                while (addr < total_flash_size)
                {
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

            addLogLine("Saving read result...");
            if (saveReadResult(0) == false)
            {
                return false;
            }
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

