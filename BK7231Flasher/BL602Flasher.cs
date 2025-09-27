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
    public class BL602Flasher : BaseFlasher
    {
        private SerialPort _port;
        int timeoutMs = 10000;



        public bool Sync()
        {
            for (int i = 0; i < 1000; i++)
            {
                addLogLine("Sync attempt " + i + "... please ground BOOT and reset...");
                if (internalSync())
                {
                    addLogLine("Sync OK!");
                    return true;
                }
                addWarningLine("Sync failed, will retry!");
                Thread.Sleep(500);
            }
            return false;
        }


        bool internalSync()
        {
            // Flush input buffer
            while (_port.BytesToRead > 0)
            {
                _port.ReadByte();
            }

            // Write initialization sequence
            byte[] syncRequest = new byte[70];
            for (int i = 0; i < syncRequest.Length; i++) syncRequest[i] = (byte)'U';
            _port.Write(syncRequest, 0, syncRequest.Length);

            for (int i = 0; i < 500; i++)
            {
                Thread.Sleep(1);

                // Check for 2-byte response
                if (_port.BytesToRead >= 2)
                {
                    byte[] response = new byte[2];
                    _port.Read(response, 0, 2);
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
            public int bootromVersion;
            public byte[] remaining;

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format("BootROM version: {0}", bootromVersion));
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
            BLInfo v = new BLInfo();
            v.bootromVersion = res[2] + (res[3] << 8) + (res[4] << 16) + (res[5] << 24);
            v.remaining = new byte[res.Length - 6];
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
                addLogLine("Flash ID: {0:X2}{1:X2}{2:X2}{3:X2}", res[2], res[3], res[4], res[5]);
            }
            else
            {
                addLogLine("Invalid response length: " + res.Length);
            }
            return res;
        }
        byte[] readFully()
        {
            byte[] r = new byte[_port.BytesToRead];
            _port.Read(r, 0, r.Length);
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
            byte[] raw = new byte[] { (byte)type, chksum, (byte)(len & 0xFF), (byte)(len >> 8) };
            _port.Write(raw, 0, raw.Length);
            if (parms != null)
            {
                _port.Write(parms, start, len);
            }
            byte[] ret = null;
            int timeoutMS = (int)(timeout * 1000);
#if false
        Thread.Sleep(100);
        while (timeoutMS > 0)
        {
            if (_port.BytesToRead >= expectedReplyLen)
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
                if (_port.BytesToRead >= expectedReplyLen)
                    break;
            }
#endif
            if (_port.BytesToRead >= 2)
            {
                byte[] rep = new byte[2];
                _port.Read(rep, 0, 2);
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
            }
            addLogLine("Command timed out!");
            return null;
        }
        internal BLInfo getAndPrintInfo()
        {
            BLInfo inf = this.getInfo();
            if (inf == null)
            {
                return null;
            }
            addLogLine(inf.ToString());
            return inf;
        }

        internal void writeFlash(byte[] data, int adr, int len = -1)
        {
            if (len < 0)
                len = data.Length;
            int ofs = 0;
            addLogLine("Starting flash write " + len);
            byte[] buffer = new byte[4096];
            while (ofs < len)
            {
                Console.Write("." + ofs + ".");
                int chunk = len - ofs;
                if (chunk > 4092)
                    chunk = 4092;
                buffer[0] = (byte)(adr & 0xFF);
                buffer[1] = (byte)((adr >> 8) & 0xFF);
                buffer[2] = (byte)((adr >> 16) & 0xFF);
                buffer[3] = (byte)((adr >> 24) & 0xFF);
                Array.Copy(data, ofs, buffer, 4, chunk);
                int bufferLen = chunk + 4;
                this.executeCommand(0x31, buffer, 0, bufferLen, true, 10);
                ofs += chunk;
                adr += chunk;
            }
            addLogLine("Done flash write " + len);
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
            _port = new SerialPort(serialName, 115200);
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            addLog("Port ready!" + Environment.NewLine);

            this.Sync();
            if (this.getAndPrintInfo() == null)
            {
                addErrorLine("Initial get info failed.");
                addErrorLine("This may happen if you don't reset between flash operations");
                return false;
            }
            this.loadAndRunPreprocessedImage("loaders/eflash_loader_rc32m.bin");
            //resync in eflash
            this.Sync();
            this.readFlashID();

            return true;
        }
        public bool loadAndRunPreprocessedImage(string fname)
        {
            if (File.Exists(fname) == false)
            {
                addErrorLine("File " + fname + " bnot found!");
                return false;
            }
            byte[] loaderBinary = File.ReadAllBytes(fname);

            return loadAndRunPreprocessedImage(loaderBinary); ;
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

        public void flash_program(byte [] data, int ofs, int len, string filename, bool bRestoreBaud)
        {
           // logger.setState("Prepare write...", Color.White);
           //addLogLine("flash_program: will flash " + len + " bytes " + filename);
           // change_baudrate(this.baudrate);
           //addLogLine("flash_program: sending startaddr");
           // _port.Write("startaddr 0x0\r\n");
           //addLogLine(_port.ReadLine().Trim());
           //addLogLine(_port.ReadLine().Trim());

           //addLogLine("flash_program: sending update command");
           // _port.Write("upgrade\r\n");
           // _port.Read(new byte[7], 0, 7);

           //addLogLine("flash_program: sending file via ymodem");
           // YModem modem = new YModem(_port, logger);
           //int res = modem.send(data, filename, len, true, 3);
           // if(res != len)
           // {
           //     addLogLine("flash_program: failed to flash, flashed only " +
           //         res +  " out of " + len + " bytes!");
           //     return;
           // }
           //addLogLine("flash_program: sending file done");

           // _port.Write("filecount\r\n");
           //addLogLine(_port.ReadLine().Trim());
           //addLogLine(_port.ReadLine().Trim());

           // if(bRestoreBaud)
           // {
           //     change_baudrate(115200);
           // }
           // addLogLine("flash_program: flashed " + len + " bytes!");
           // addLogLine("If you want your program to run now, disconnect boot pin and do power off and on cycle");

        }


        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if (doGenericSetup() == false)
            {
                return ;
            }
            doReadInternal();
        }
        public void flush_com()
        {
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
        }

        public bool upload_ram_loader_for_read(byte[] RAMCODE, int baudToSet)
        {
            if (prepareForLoaderSend())
            {
                return true;
            }
           // addLogLine("Connect to bootloader...");
           // _port.Write($"download [rambin] [0x20000000] [{RAMCODE.Length}]\r\n");
           // addLogLine("Will send RAMCODE");
           // YModem modem = new YModem(_port, logger);

           // var stream = new MemoryStream(RAMCODE);
           // int ret = modem.send(stream, "RAMCODE", stream.Length, false,20, baudToSet);
           // if (ret != stream.Length)
           // {
           //    addLogLine("Ramcode upload failed, expected " + stream.Length + ", got " + ret + "!");
           //     return false;
           // }
           //addLogLine("RAMCODE send ok, starting immediately");
            return true;
        }
        bool openPort()
        {
            try
            {
                _port = new SerialPort(serialName, 115200, Parity.None, 8, StopBits.One);
            }
            catch (Exception ex)
            {
                addError("Serial port create exception: " + ex.ToString() + Environment.NewLine);
                return true;
            }
            try
            {
              //  _port.ReadBufferSize = 4096 * 2;
        //     _port.ReadBufferSize = 3000000;
            }
            catch (Exception ex)
            {
                addWarning("Setting _port port buffer size exception: " + ex.ToString() + Environment.NewLine);
            }
            try
            {
                _port.Open();
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
            //logger.setState("Connecting...", Color.White);
            //addLogLine("Sync with LN882H...");
            //_port.DiscardInBuffer();

            //string msg = "";
            //int loops = 0;
            //while (msg != "Mar 14 2021/00:23:32\r")
            //{
            //    Thread.Sleep(1000);
            //    flush_com();
            //    addLogLine("sending version... waiting for:  Mar 14 2021/00:23:32");
            //    loops++;
            //    if (loops % 10 == 0 && loops>9)
            //    {
            //        addLogLine("Still no reply - maybe you need to pull BOOT pin down or do full power off/on before next attempt");
            //    }
            //    try
            //    {
            //        _port.Write("version\r\n");
            //        msg = _port.ReadLine();
            //        addLogLine(msg);
            //    }
            //    catch (TimeoutException)
            //    {
            //        msg = "";
            //    }
            //}

            //addLogLine("Connect to bootloader...");
            return false;
        }
        public bool upload_ram_loader(string fname)
        {
            //addLogLine("upload_ram_loader will upload " + fname + "!");
            //if (File.Exists(fname) == false)
            //{
            //    addLogLine("Can't open " + fname + "!");
            //    return true;
            //}
            //if(prepareForLoaderSend())
            //{
            //    return true;
            //}
            //_port.Write("download [rambin] [0x20000000] [37872]\r\n");
            //addLogLine("Will send file via YModem");

            //YModem modem = new YModem(_port, logger);
            //modem.send_file(fname, false, 3);

            //addLogLine("Starting program. Wait....");
            //string msg = "";
            //while (msg != "RAMCODE\r")
            //{
            //    Thread.Sleep(1000);
            //    _port.DiscardInBuffer();
            //    addLogLine("send version... wait for:  RAMCODE");
            //    _port.Write("version\r\n");
            //    try
            //    {
            //        msg = _port.ReadLine();
            //        addLogLine(msg);
            //        msg = _port.ReadLine();
            //        addLogLine(msg);
            //    }
            //    catch (TimeoutException)
            //    {
            //        msg = "";
            //    }
            //}

            //_port.Write("flash_uid\r\n");
            //try
            //{
            //    msg = _port.ReadLine();
            //    msg = _port.ReadLine();
            //    addLogLine(msg.Trim());
            //}
            //catch (TimeoutException)
            //{
            //    addLogLine("Timeout on flash_uid");
            //    return true;
            //}
            //addLogLine("upload_ram_loader complete for " + fname + "!");

            return false;
        }
        internal byte[] readFlash(int addr = 0, int amount = 4096)
        {
            byte[] ret = new byte[amount];
            Console.Write("Starting read...");
            while (amount > 0)
            {
                int length = 512;
                if (amount < length)
                    length = amount;

                Console.Write(".");

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
                byte[] result = this.executeCommand(0x32, cmdBuffer, 0, cmdBuffer.Length, true, 100, rawReplyLen);

                if (result == null)
                {
                    addLog("Read fail - no reply");
                    return null;
                }

                int dataLen = result.Length - 2;
                if (dataLen != length)
                {
                    addLog("Read fail - size mismatch");
                    return null;
                }
                Array.Copy(result, 2, ret, addr, dataLen);

                addr += dataLen;
                amount -= dataLen;
            }
            addLog("Read complete!");
            return ret;
        }

        public bool doReadInternal() { 
           
            return false;
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
            if (_port != null)
            {
                _port.Close();
                _port.Dispose();
            }
        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            //byte[] data = null;
            //if (rwMode != WriteMode.OnlyOBKConfig)
            //{
            //    addLog("Reading file " + sourceFileName + "..." + Environment.NewLine);
            //    data = File.ReadAllBytes(sourceFileName);
            //    if (data == null)
            //    {
            //        addError("Failed to open " + sourceFileName + "..." + Environment.NewLine);
            //        return;
            //    }
            //    addSuccess("Loaded " + data.Length + " bytes from " + sourceFileName + "..." + Environment.NewLine);

            //    startSector = 0;
            //    doWrite(startSector, 0, data, rwMode);

            //}
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

