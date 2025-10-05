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
    public class SPIFlasher : BaseFlasher
    {
        protected CH341DEV hd;


        public bool BK_EnterSPIMode(byte data)
        {
            for (int x = 0; x < 10; x++)
            {
                byte[] sendBuf = new byte[25];
                for (int i = 0; i < 25; i++) sendBuf[i] = data;
                hd.Ch341SPI4Stream(sendBuf);
            }

            byte[] buf1 = new byte[4] { 0x9F, 0x00, 0x00, 0x00 };
            byte[] resp = hd.Ch341SPI4Stream(buf1);
            if (resp == null)
                return false;

            int zeroCount = 0;
            for (int i = 1; i < 4; i++)
                if (resp[i] == 0x00) zeroCount++;

            addLogLine("SPI Response: " + BitConverter.ToString(resp));
            bool bOk = resp[0] != 0x00 && zeroCount == 3;
            if (bOk)
            {
                // flush
                for (int x = 0; x < 1000; x++)
                {
                    byte[] sendBuf = new byte[25];
                    for (int i = 0; i < 25; i++)
                        sendBuf[i] = 0;
                    hd.Ch341SPI4Stream(sendBuf);
                }
            }
            return bOk;
        }

        public byte[] ReadJEDECID()
        {
            byte[] cmd = new byte[4] { 0x9F, 0x00, 0x00, 0x00 };
            byte[] resp = hd.Ch341SPI4Stream(cmd);
            if (resp != null)
            {
                addLogLine($"JEDEC ID: {BitConverter.ToString(resp)}");
            }
            return resp;
        }

        public int GetFlashSize(byte[] jedec)
        {
            if (jedec == null || jedec.Length < 4)
                return 0;
            int size = 1 << jedec[3]; // common formula: 2^N bytes
            addLogLine($"Detected flash size: {size / 1024} KB");
            return size;
        }
        
        public bool CheckFlashEmpty(uint address, int size)
        {
            byte[] d = ReadFlash(address, size , true);
            if (d == null)
                return false;
            for (int i = 0; i < size; i++)
            {
                if (d[i] != 0xff)
                    return false;
            }
            return true;
        }
        public bool CheckFlash(uint address, int size, byte[] data) {
            byte[] d = ReadFlash(address, size);
            for(int i = 0; i < data.Length; i++)
            {
                if (d[i] != data[i])
                    return false;
            }
            return true;
        }
        public byte[] ReadFlash(uint address, int size, bool bSilent = false)
        {
            const int pageSize = 256;
            byte[] result = new byte[size];
            byte[] cmd = new byte[4 + pageSize];
            if (bSilent == false)
            {
                addLogLine("Starting flash read, ofs 0x" + address.ToString("X") + ", len 0x" + size.ToString("X"));

                logger.setProgress(0, size);
                logger.setState("Reading", Color.White);
            }
            for (uint addr = address; addr < address + size; addr += pageSize)
            {;
                int offset = (int)(addr - address);
                if (bSilent == false)
                {
                    logger.setProgress(offset, size);
                }
                int readSize = Math.Min(pageSize, size - offset);

                cmd[0] = 0x03; // Read Data
                cmd[1] = (byte)((addr >> 16) & 0xFF);
                cmd[2] = (byte)((addr >> 8) & 0xFF);
                cmd[3] = (byte)(addr & 0xFF);

                byte[] resp = hd.Ch341SPI4Stream(cmd);
                if (resp != null)
                {
                    //if(resp[0] != 0x03)
                    //{
                    //    addLogLine($" Failed to read 0x{addr:X6}");
                    //    return null;
                    //}
                    //Console.Write($"0x{addr:X6}...");
                    Array.Copy(resp, 4, result, offset, readSize);
                }
                else
                {
                    if (bSilent == false)
                    {
                        addLogLine($" Failed to read 0x{addr:X6}");
                    }
                    return null;
                }
            }

            if (bSilent == false)
            {
                logger.setState("Reading done", Color.DarkGreen);
                addLogLine($"Done!");
            }
            return result;
        }
        public byte ReadStatus()
        {
            byte[] cmd = new byte[2] { 0x05, 0x00 }; // Read Status Register
            byte[] resp = hd.Ch341SPI4Stream(cmd);
            return resp != null ? resp[1] : (byte)0xFF;
        }

        public bool WaitWriteComplete(int timeoutMs = 5000)
        {
            int elapsed = 0;
            const int pollInterval = 1; // ms
            while (elapsed < timeoutMs)
            {
                if ((ReadStatus() & 0x01) == 0)
                    return true; // WIP=0
                Thread.Sleep(pollInterval);
                elapsed += pollInterval;
            }
            addLogLine("Error: Flash write timed out.");
            return false;
        }

        public bool WriteEnable()
        {
            byte[] cmd = new byte[1] { 0x06 }; // Write Enable
            hd.Ch341SPI4Stream(cmd);
            return (ReadStatus() & 0x02) != 0; // WEL=1
        }

        public bool ChipErase()
        {
            addLogLine("Full chip erase - enabling chip...");
            if (!WriteEnable())
                return false;

            addLogLine("Full chip erase - sending erase...");
            byte[] cmd = new byte[1] { 0xC7 };
            hd.Ch341SPI4Stream(cmd);

            addLogLine("Full chip erase - awaiting erase complete...");
            // Poll WIP bit until erase completes
            if (!WaitWriteComplete(120_000)) // typical full chip erase can take up to 120s
            {
                addLogLine("Error: Full chip erase timed out.");
                return false;
            }
            addLogLine("Full chip erase done - checking is first sector empty (just to be sure)");

            if (CheckFlashEmpty(0, 4096) == false)
            {
                addLogLine("Full chip erase failed");
                logger.setState("Erase fail", Color.Red);
                return false;
            }
            addLogLine("Full chip erase completed successfully.");
            return true;
        }
        bool eraseSector(uint addr, bool bVerify)
        {
            const int sectorSize = 4096;
            if (!WriteEnable())
                return false;

            byte[] cmd = new byte[4];
            cmd[0] = 0x20; // Sector Erase
            cmd[1] = (byte)((addr >> 16) & 0xFF);
            cmd[2] = (byte)((addr >> 8) & 0xFF);
            cmd[3] = (byte)(addr & 0xFF);

            hd.Ch341SPI4Stream(cmd);
            if (!WaitWriteComplete(5000))
                return false;
            if (CheckFlashEmpty(addr, sectorSize) == false)
            {
                return false;
            }
            return true;
        }
        public bool EraseFlash(uint ofs, int len)
        {
            const int sectorSize = 4096;
            byte[] cmd = new byte[4];

            addLogLine("Starting flash erase, ofs 0x" + ofs.ToString("X") + ", len 0x" + len.ToString("X"));

            logger.setProgress(0, len);
            logger.setState("Erasing", Color.White);
            for (uint addr = ofs; addr < ofs + len; addr += sectorSize)
            {
                logger.setProgress((int)addr - (int)ofs, len);
                int errors = 0;
                while (true)
                {
                    bool bOk = eraseSector(addr, true);
                    if(bOk)
                    {
                        break;
                    }
                    errors++;
                    if (errors > 10)
                    {
                        addLogLine("Erase verfication failed at " + addr);
                        logger.setState("Erase fail", Color.Red);
                        return true;
                    }
                    addLogLine("Error at " + addr + ", retry "+ errors);
                }
            }
            logger.setState("Erase done", Color.DarkGreen);
            return true;
        }

        public bool WriteFlash(uint ofs, int len, byte[] buffer)
        {
            const int pageSize = 256;
            byte[] cmd = new byte[4 + pageSize];
            addLogLine("Starting flash write, ofs 0x" + ofs.ToString("X") + ", len 0x" + len.ToString("X"));

            logger.setProgress(0, len);
            logger.setState("Writing", Color.White);
            int loops = 0;
            for (int offset = 0; offset < len; offset += pageSize)
            {
                int writeSize = Math.Min(pageSize, len - offset);

                logger.setProgress(offset, len);
                if (!WriteEnable())
                {
                    return false;
                }

                uint addr = ofs + (uint)offset;
                cmd[0] = 0x02; // Page Program
                cmd[1] = (byte)((addr >> 16) & 0xFF);
                cmd[2] = (byte)((addr >> 8) & 0xFF);
                cmd[3] = (byte)(addr & 0xFF);
                Array.Copy(buffer, offset, cmd, 4, writeSize);

                hd.Ch341SPI4Stream(cmd);
                if (!WaitWriteComplete(5000))
                {
                    return false;
                }
                if (loops % 50 == 0)
                {
                    byte[] readBack = ReadFlash(addr, writeSize, true);
                    if (readBack == null)
                    {
                        logger.setState("Writing error", Color.Red);
                        return false;
                    }
                    for (int i = 0; i < writeSize; i++)
                    {
                        if (readBack[i] != buffer[offset + i])
                        {
                            logger.setState("Writing error", Color.Red);
                            return false;
                        }
                    }
                }
                loops++;

            }
            logger.setState("Writing done", Color.DarkGreen);
            return true;
        }

        public bool WriteFlashAndVerify(uint ofs, int len, byte[] buffer)
        {
            if (!WriteFlash(ofs, len, buffer)) return false;

            byte[] readBack = ReadFlash(ofs, len, true);
            if (readBack == null) return false;

            for (int i = 0; i < len; i++)
                if (readBack[i] != buffer[i])
                    return false;

            return true;
        }

        public byte[] RandomPattern(int len)
        {
            byte[] pattern = new byte[len];
            Random rnd = new Random((int)DateTime.Now.Ticks);
            rnd.NextBytes(pattern);
            return pattern;
        }

        public bool TestReadWrite(uint ofs, int len)
        {
            byte[] pattern = RandomPattern(len);

            //ChipErase();
            addLogLine("Erasing flash...");
            if (!EraseFlash(ofs, len))
                return false;
            byte[] cleared = ReadFlash(ofs, len);
            for (int i = 0; i < len; i++)
            {
                if (cleared[i] != 0xff)
                {
                    addLogLine("Clear error");
                    return false;
                }
            }

            addLogLine("Writing pattern...");
            if (!WriteFlashAndVerify(ofs, len, pattern))
            {
                addLogLine("Test Failed!");
                return false;
            }

            addLogLine("Test passed!");
            return true;
        }
        public virtual bool Sync()
        {
             return true;
        }
        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);

            hd = new CH341DEV(0);
            if (hd.Ch341Open() == -1)
            {
                addError("CH341 error " + hd.getLastError() + Environment.NewLine);
                return true;
            }
            hd.Ch341SetI2CSpeed(3);
            addLog("CH341 ready!" + Environment.NewLine);

            if (this.Sync() == false)
            {
                // failed
                return true;
            }
            if (this.getAndPrintInfo())
            {
                addErrorLine("Initial get info failed.");
                addErrorLine("This may happen if you don't reset between flash operations");
                addErrorLine("So, make sure that BOOT is connected, do reset (or power off/on) and try again");
                return false;
            }
            return true;
        }
        int flashSize;
        bool getAndPrintInfo()
        {
            byte[] jedec = this.ReadJEDECID();
            if(jedec == null)
            {
                addErrorLine("Failed to read JEDEC ID!");
                return true;
            }
            flashSize = this.GetFlashSize(jedec);
            if(flashSize <= 0 || flashSize >= 64 * 1024 * 1024)
            {
                addErrorLine("Failed to extract flash size!");
                return true;
            }
          
            return false;
        }
        public bool loadAndRunPreprocessedImage(string fname)
        {
            if (File.Exists(fname) == false)
            {
                addErrorLine("File " + fname + " not found!");
                return false;
            }
            byte[] loaderBinary = File.ReadAllBytes(fname);

            return false;
        }
        public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
        {
            if (doGenericSetup() == false)
            {
                return true;
            }
            //if(upload_ram_loader("loaders/LN882H_RAM_BIN.bin"))
            //{
            //    return true;
            //}
            //flash_program(data,0,data.Length, "", false);
            return false;
        }



        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if (doGenericSetup() == false)
            {
                return;
            }
            if(fullRead)
            {
                doReadInternal(0, flashSize);
            }
            else
            {
                doReadInternal((uint)startSector * 4096, sectors * 4096);
            }
            ChipReset();
        }
        public virtual bool ChipReset()
        {
            return false;
        }

        bool openPort()
        {
            return false;
        }

        public bool doReadInternal(uint addr, int len)
        {
            byte[] res = ReadFlash(addr, len);
            if(res != null)
            {
                ms = new MemoryStream(res);
            }
            else
            {
                ms = null;
            }
            return false;
        }
        MemoryStream ms;
        public override byte[] getReadResult()
        {
            if (ms == null)
                return null;
            return ms.ToArray();
        }
        public override bool doErase(int startSector, int sectors, bool bAll)
        {
            if (doGenericSetup() == false)
            {
                return false;
            }
            if (UnprotectFlash() == false)
            {
                return false;
            }
            if (bAll)
            {
                if (!ChipErase())
                {
                    return false;
                }
                return true;
            }
            return false;
        }
        public bool UnprotectFlash()
        {
            // Enable writes
            if (!WriteEnable())
                return false;

            // Write 0x00 to Status Register (clear BP bits)
            byte[] cmd = new byte[2] { 0x01, 0x00 };
            hd.Ch341SPI4Stream(cmd);

            if (!WaitWriteComplete(5000))
                return false;

            // Verify unprotected: check status register has BP=0
            byte status = ReadStatus();
            addLogLine("Unprotect done, SR1=0x" + status.ToString("X2"));

            return (status & 0x1C) == 0; // BP0..BP3 = 0
        }
        public override void closePort()
        {

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
            if (rwMode == WriteMode.ReadAndWrite)
            {
                doReadInternal(0, flashSize);
            }
            if (UnprotectFlash() == false)
            {
                return;
            }
            if(string.IsNullOrEmpty(sourceFileName))
            {
                addLogLine("No filename given!");
                return;
            }
            addLogLine("Reading " + sourceFileName + "...");
            byte[] x = File.ReadAllBytes(sourceFileName);
            if(EraseFlash(0, x.Length)==false)
            {

                return;
            }
            WriteFlash(0, x.Length,x);
            ChipReset();
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

