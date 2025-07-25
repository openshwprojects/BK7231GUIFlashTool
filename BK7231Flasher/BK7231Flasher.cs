﻿using System;
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
    public class BK7231Flasher : BaseFlasher
    {
        public static Random rand = new Random(Guid.NewGuid().GetHashCode());

        bool bDebugUART;
        MemoryStream ms;
        string lastEncryptionKey;
        public static int SECTOR_SIZE = 0x1000;
        public static int FLASH_SIZE = 0x200000;
        public static int BOOTLOADER_SIZE = 0x11000;
        public static int TOTAL_SECTORS = FLASH_SIZE / SECTOR_SIZE;
        public static string TUYA_ENCRYPTION_KEY = "510fb093 a3cbeadc 5993a17e c7adeb03";
        public static string EMPTY_ENCRYPTION_KEY = "00000000 00000000 00000000 00000000";
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
            catch(Exception ex)
            {
                addError("Serial port open exception: " + ex.ToString() + Environment.NewLine);
                return true;
            }
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
       enum CommandCode
        {
            LinkCheck = 0,
            WriteReg = 1,
            ReadReg = 0x03,
            FlashRead4K = 0x09,
            CheckCRC = 0x10,
            SetBaudRate = 0x0f,
            FlashErase4K = 0x0b,
            FlashErase = 0x0f,
            FlashWrite4K = 0x07,
            FlashWrite = 0x06,
            FlashGetMID = 0x0e,
            FlashReadSR = 0x0c,
            FlashWriteSR = 0x0d,
        }
        byte[] BuildCmd_LinkCheck()
        {
            byte[] ret = new byte[5];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0x01; // len
            ret[4] = (byte)CommandCode.LinkCheck;
            return ret;
        }

        byte[] BuildCmd_ReadRegn(int addr)
        {
            int length = 1 + (4);
            byte[] buf = new byte[9];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = (byte)length;
            buf[4] = (byte)CommandCode.ReadReg;
            buf[5] = (byte)(addr & 0xff);
            buf[6] = (byte)((addr >> 8) & 0xff);
            buf[7] = (byte)((addr >> 16) & 0xff);
            buf[8] = (byte)((addr >> 24) & 0xff);
            return buf;
        }
        byte[] BuildCmd_EraseSector4K(int addr, int szcmd)
        {
            int length = 1 + (4 );
            byte[] buf = new byte[12];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = 0xff;
            buf[4] = 0xf4;
            buf[5] = (byte)length;
            buf[6] = 0;
            buf[7] = (byte)CommandCode.FlashErase4K;
            buf[8] = (byte)(addr & 0xff);
            buf[9] = (byte)((addr >> 8) & 0xff);
            buf[10] = (byte)((addr >> 16) & 0xff);
            buf[11] = (byte)((addr >> 24) & 0xff);
            return buf;
        }
        // szcmd can have two options: SECTOR_4K = 0x20 and BLOCK_64K = 0xD8
        byte[] BuildCmd_FlashErase(int addr, int szcmd)
        {
            int length = 1 + (4+1);
            byte[] buf = new byte[13];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = 0xff;
            buf[4] = 0xf4;
            buf[5] = (byte)length;
            buf[6] = 0;
            buf[7] = (byte)CommandCode.FlashErase;
            buf[8] = (byte)(szcmd);
            buf[9] = (byte)(addr & 0xff);
            buf[10] = (byte)((addr >> 8) & 0xff);
            buf[11] = (byte)((addr >> 16) & 0xff);
            buf[12] = (byte)((addr >> 24) & 0xff);
            return buf;
        }
        byte[] BuildCmd_SetBaudRate(int baudrate, int delay_ms)
        {
            int length = 1 + (4 + 1);
            byte[] buf = new byte[10];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = (byte)length;
            buf[4] = (byte)CommandCode.SetBaudRate;
            buf[5] = (byte)(baudrate & 0xff);
            buf[6] = (byte)((baudrate >> 8) & 0xff);
            buf[7] = (byte)((baudrate >> 16) & 0xff);
            buf[8] = (byte)((baudrate >> 24) & 0xff);
            buf[9] = (byte)(delay_ms & 0xff);
            return buf;
        }
        byte[] BuildCmd_CheckCRC(int startAddr, int endAddr)
        {
            int length = 1 + (4 + 4);
            byte[] buf = new byte[13];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = (byte)length;
            buf[4] = (byte)CommandCode.CheckCRC;
            buf[5] = (byte)(startAddr & 0xff);
            buf[6] = (byte)((startAddr >> 8) & 0xff);
            buf[7] = (byte)((startAddr >> 16) & 0xff);
            buf[8] = (byte)((startAddr >> 24) & 0xff);
            buf[9] = (byte)(endAddr & 0xff);
            buf[10] = (byte)((endAddr >> 8) & 0xff);
            buf[11] = (byte)((endAddr >> 16) & 0xff);
            buf[12] = (byte)((endAddr >> 24) & 0xff);
            return buf;
        }
        
        byte[] BuildCmd_FlashGetMID(int addr)
        {
            int length = 1 + (4);
            byte[] ret = new byte[12];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0xff;
            ret[4] = 0xf4;
            ret[5] = (byte)(length & 0xff);
            ret[6] = (byte)((length >> 8) & 0xff);
            ret[7] = (byte)CommandCode.FlashGetMID;
            ret[8] = (byte)(addr & 0xff);
            ret[9] = (byte)((addr >> 8) & 0xff);
            ret[10] = (byte)((addr >> 16) & 0xff);
            ret[11] = (byte)((addr >> 24) & 0xff);
            return ret;
        }
        byte[] BuildCmd_FlashWriteSR(int regAddr, int val)
        {
            int length = 1 + (1 + 1);
            byte[] buf = new byte[10];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = 0xff;
            buf[4] = 0xf4;
            buf[5] = (byte)(length & 0xff);
            buf[6] = (byte)((length >> 8) & 0xff);
            buf[7] = (byte)CommandCode.FlashWriteSR;
            buf[8] = (byte)(regAddr & 0xff);
            buf[9] = (byte)((val) & 0xff);
            return buf;
        }
        byte[] BuildCmd_FlashWriteSR2(int regAddr, int val)
        {
            int length = 1 + (1 + 2);
            byte[] buf = new byte[11];
            buf[0] = 0x01;
            buf[1] = 0xe0;
            buf[2] = 0xfc;
            buf[3] = 0xff;
            buf[4] = 0xf4;
            buf[5] = (byte)(length & 0xff);
            buf[6] = (byte)((length >> 8) & 0xff);
            buf[7] = (byte)CommandCode.FlashWriteSR;
            buf[8] = (byte)(regAddr & 0xff);
            buf[9] = (byte)((val) & 0xff);
            buf[10] = (byte)((val >> 8) & 0xff);
            return buf;
        }
        byte[] BuildCmd_FlashReadSR(int addr)
        {
            int length = 1 + (1);
            byte[] ret = new byte[9];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0xff;
            ret[4] = 0xf4;
            ret[5] = (byte)(length & 0xff);
            ret[6] = (byte)((length >> 8) & 0xff);
            ret[7] = (byte)CommandCode.FlashReadSR;
            ret[8] = (byte)(addr & 0xff);
            return ret;
        }
        byte[] BuildCmd_FlashWrite4K(int addr, byte [] data, int startOfs)
        {
            int length = 1 + (4 + 4 * 1024);
            byte[] ret = new byte[12+4*1024];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0xff;
            ret[4] = 0xf4;
            ret[5] = (byte)(length & 0xff);
            ret[6] = (byte)((length >> 8) & 0xff);
            ret[7] = (byte)CommandCode.FlashWrite4K;
            ret[8] = (byte)(addr & 0xff);
            ret[9] = (byte)((addr >> 8) & 0xff);
            ret[10] = (byte)((addr >> 16) & 0xff);
            ret[11] = (byte)((addr >> 24) & 0xff);
            int lenToCopy = 4096;
            if (lenToCopy > data.Length)
                lenToCopy = data.Length;
            Array.Copy(data, startOfs, ret, 12, lenToCopy);
            return ret;
        }
        byte[] BuildCmd_FlashWrite(int addr, byte[] data, int startOfs, int writeLen)
        {
            int length = 1 + (4 + writeLen);
            byte[] ret = new byte[12 + writeLen];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0xff;
            ret[4] = 0xf4;
            ret[5] = (byte)(length & 0xff);
            ret[6] = (byte)((length >> 8) & 0xff);
            ret[7] = (byte)CommandCode.FlashWrite;
            ret[8] = (byte)(addr & 0xff);
            ret[9] = (byte)((addr >> 8) & 0xff);
            ret[10] = (byte)((addr >> 16) & 0xff);
            ret[11] = (byte)((addr >> 24) & 0xff);
            Array.Copy(data, startOfs, ret, 12, writeLen);
            return ret;
        }
        byte[] BuildCmd_WriteReg(int regAddr, int val)
        {
            int length = 1 + (4 + 4);
            byte[] ret = new byte[12 + 4];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = (byte)length;
            ret[4] = (byte)CommandCode.WriteReg;
            ret[5] = (byte)(regAddr & 0xff);
            ret[6] = (byte)((regAddr >> 8) & 0xff);
            ret[7] = (byte)((regAddr >> 16) & 0xff);
            ret[8] = (byte)((regAddr >> 24) & 0xff);
            ret[9] = (byte)(val & 0xff);
            ret[10] = (byte)((val >> 8) & 0xff);
            ret[11] = (byte)((val >> 16) & 0xff);
            ret[12] = (byte)((val >> 24) & 0xff);
            return ret;
        }
        byte[] BuildCmd_FlashRead4K(int addr)
        {
            int length = 1 + (4 + 0);
            byte[] ret = new byte[12];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0xff;
            ret[4] = 0xf4;
            ret[5] = (byte)(length & 0xff);
            ret[6] = (byte)((length >> 8) & 0xff);
            ret[7] = (byte)CommandCode.FlashRead4K;
            ret[8] = (byte)(addr & 0xff);
            ret[9] = (byte)((addr >> 8) & 0xff);
            ret[10] = (byte)((addr >> 16) & 0xff);
            ret[11] = (byte)((addr >> 24) & 0xff);
            return ret;
        }
        int CalcRxLength_CheckCRC()
        {
            return (3 + 3 + 1 + 4);
        }
        int CalcRxLength_SetBaudRate()
        {
            return (3 + 3 + 1 + 4 + 1);
        }
        int CalcRxLength_LinkCheck()
        {
            return (3 + 3 + 1 + 1 + 0);
        }
        int CalcRxLength_EraseSector4K()
        {
            return (3 + 3 + 3 + (1 + 1 + (4 + 0)));
        }
        int CalcRxLength_FlashErase()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 4)));
        }
        void consumeSerial(float timeout)
        {
            int realRead;
            serial.ReadTimeout = (int)(1000 * timeout);
            byte[] tmp = new byte[4096];
            try
            {
                realRead = serial.Read(tmp, 0, tmp.Length);
            }
            catch (Exception ex)
            {

            }
        }
        byte[] tmp = new byte[4096];
        void consumePending()
        {
            if (serial.BytesToRead > 0)
            {
                serial.Read(tmp, 0, serial.BytesToRead);
            }
        }

        byte[] Start_Cmd(byte[] txbuf, int rxLen = 0, float timeout = 0.05f)
        {
            consumePending();
            int realRead = 0;
            serial.ReadTimeout = (int)(10*cfg_readTimeOutMultForSerialClass);
            if(txbuf != null)
            {
                serial.Write(txbuf, 0, txbuf.Length);
            }
            if (rxLen == 0)
                return null;
            var timer = new Stopwatch();
            timer.Start();
            if (rxLen > 0)
            {
                byte[] ret = new byte[rxLen];
                while (timer.Elapsed.TotalSeconds < timeout * cfg_readTimeOutMultForLoop)
                {
                    try
                    {
                        if (cfg_readReplyStyle == 0)
                        {
                            //addLog("serial.BytesToRead " + serial.BytesToRead+"");
                            if (serial.BytesToRead >= rxLen)
                            {
                                //  addLog("Tries to read!");
                                int readNow = serial.Read(ret, realRead, rxLen - realRead);
                                realRead += readNow;
                                if (bDebugUART)
                                {
                                    addLog("Read len: " + realRead + Environment.NewLine);
                                }
                                if (realRead == rxLen)
                                {
                                    if (bDebugUART)
                                    {
                                        addLog("Got UART reply!" + Environment.NewLine);
                                    }
                                    return ret;
                                }
                            }
                        }
                        else
                        {
                            //addLog("serial.BytesToRead " + serial.BytesToRead+"");
                            int ava = serial.BytesToRead;
                            if (ava > 0)
                            {
                                //  addLog("Tries to read!");
                                int wantsToRead = rxLen - realRead;
                                if (wantsToRead > ava)
                                    wantsToRead = ava;
                                int readNow = serial.Read(ret, realRead, wantsToRead);
                                realRead += readNow;
                                if (bDebugUART)
                                {
                                    addLog("Read len: " + realRead + Environment.NewLine);
                                }
                                if (realRead >= rxLen)
                                {
                                    if (bDebugUART)
                                    {
                                        addLog("Got UART reply!" + Environment.NewLine);
                                    }
                                    return ret;
                                }
                            }
                        }
                    }
                    catch (TimeoutException)
                    {

                    }
                    catch (Exception ex)
                    {
                        addLog("Got exception: " + ex.ToString() + "!" + Environment.NewLine);
                        return null;
                    }
                }
                if (rxLen > 10)
                {
                    addLog("failed with serial.BytesToRead " + serial.BytesToRead + " (expected " + rxLen+")" + Environment.NewLine);
                    try
                    {
                        string s = "";
                        int loaded = serial.BytesToRead;
                        for(int k = 0; k < loaded && k < 16; k++)
                        {
                            byte dataByte = (byte) serial.ReadByte();
                            s += dataByte.ToString("X2");
                        }
                        addLog("The beginning of buffer in UART contains " + s + " data." + Environment.NewLine);
                    }
                    catch(Exception ex)
                    {

                    }
                }
                return null;
            }
            return null;
        }
        static bool ByteArrayCompare(byte[] a1, byte[] a2, int len)
        {
            if (a1.Length < len)
                return false;
            if (a2.Length < len)
                return false;
            for (int i = 0; i < len; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }
        static bool ByteArrayCompare(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }
        uint CheckRespond_CheckCRC(byte[] buf, int a0, int a1)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)CommandCode.CheckCRC };
            cBuf[2] = 3 + 1 + 4;
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                //addLog("CheckRespond_CheckCRC: OK");
                uint r = buf[10];
                r = (r << 8) + buf[9];
                r = (r << 8) + buf[8];
                r = (r << 8) + buf[7];
                return r;
            }
            addLog("CheckRespond_CheckCRC: ERROR" + Environment.NewLine);
            return 0;
        }
        bool CheckRespond_WriteReg(byte[] buf, int regAddr, int val)
        {
            byte[] cBuf = new byte[15] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)CommandCode.WriteReg,
            0, 0, 0,0,0,0,0,0};
            cBuf[2] = 3 + 1 + 4 + 4;

            cBuf[7] = (byte)(regAddr & 0xff);
            cBuf[8] = (byte)((regAddr >> 8) & 0xff);
            cBuf[9] = (byte)((regAddr >> 16) & 0xff);
            cBuf[10] = (byte)((regAddr >> 24) & 0xff);
            cBuf[11] = (byte)(val & 0xff);
            cBuf[12] = (byte)((val >> 8) & 0xff);
            cBuf[13] = (byte)((val >> 16) & 0xff);
            cBuf[14] = (byte)((val >> 24) & 0xff);

            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                return true;
            }
            addLog("CheckRespond_WriteReg: ERROR" + Environment.NewLine);
            return false;
        }


        bool CheckRespond_FlashWrite(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] {
                0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, (1 + 1 + (4 + 1)) & 0xff,
                ((1 + 1 + (4 + 1)) >> 8) & 0xff, (byte)CommandCode.FlashWrite};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                int r = buf[14];
                r = (r << 8) + buf[13];
                r = (r << 8) + buf[12];
                r = (r << 8) + buf[11];
                if (r != addr)
                {
                    addError("CheckRespond_FlashWrite: returned address didnt match?" + Environment.NewLine);
                    return false;
                }
                return true;
            }
            addError("CheckRespond_FlashWrite: bad value returned?" + Environment.NewLine);
            return false;
        }

        byte[] CheckRespond_FlashWriteSR(byte[] buf, int regAddr, int val)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4,
                (byte)(1 + 1 + (1 + 1)) & 0xff, ((1 + 1 + (1 + 1)) >> 8) & 0xff,
                (byte)CommandCode.FlashWriteSR};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length) 
                && val == buf[12] && regAddr == buf[11])
            {
                byte[] ret = new byte[] { buf[11] };
                return ret;
            }
            addError("CheckRespond_FlashWriteSR: bad value returned?" + Environment.NewLine);
            return null;
        }
        byte[] CheckRespond_FlashWriteSR2(byte[] buf, int regAddr, int val)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4,
                (byte)(1 + 1 + (1 + 2)) & 0xff, ((1 + 1 + (1 + 2)) >> 8) & 0xff,
                (byte)CommandCode.FlashWriteSR};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length)
                && ((byte)(val & 0xFF) == buf[12]) && ((byte)((val >> 8) & 0xFF) == buf[13]))
            {
                byte[] ret = new byte[] { buf[11] };
                return ret;
            }
            addError("CheckRespond_FlashWriteSR: bad value returned?" + Environment.NewLine);
            return null;
        }

        byte[] CheckRespond_ReadFlashReg(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)CommandCode.ReadReg, 0, 0, 0, 0};
            cBuf[2] = 3 + 1 + 4 + 4;
            cBuf[7] = (byte)(addr & 0xff);
            cBuf[8] = (byte)((addr >> 8) & 0xff);
            cBuf[9] = (byte)((addr >> 16) & 0xff);
            cBuf[10] = (byte)((addr >> 24) & 0xff);
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                byte[] ret = new byte[4] { buf[11], buf[12], buf[13], buf[14] };
                return ret;
            }
            addError("CheckRespond_FlashReadSR: bad value returned?" + Environment.NewLine);
            return null;
        }
        byte[] CheckRespond_FlashReadSR(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04,0x0e,0xff,0x01,0xe0,0xfc,0xf4,(1+1+(1+1))&0xff,
                   ((1+1+(1+1))>>8)&0xff,(byte)CommandCode.FlashReadSR};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                byte[] ret = new byte[2] { buf[10], buf[12] };
                return ret;
            }
            addError("CheckRespond_FlashReadSR: bad value returned?" + Environment.NewLine);
            return null;
        }
        int CheckRespond_FlashGetMID(byte[] buf)
        {
            byte[] cBuf = new byte[] { 0x04,0x0e,0xff,0x01,0xe0,0xfc,0xf4,(1+4)&0xff,
                    ((1+4)>>8)&0xff,(byte)CommandCode.FlashGetMID};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                return BitConverter.ToInt32(buf, 11) >> 8;
            }
            // FIX BootROM Bug
            cBuf[7] += 1;
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                return BitConverter.ToInt32(buf, 11) >> 8;
            }
            addError("CheckRespond_FlashGetMID: bad value returned?" + Environment.NewLine);
            return 0;
        }
        bool CheckRespond_FlashWrite4K(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, (1 + 1 + (4)) & 0xff,
               0, (byte)CommandCode.FlashWrite4K};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                int r = buf[14];
                r = (r << 8) + buf[13];
                r = (r << 8) + buf[12];
                r = (r << 8) + buf[11];
                if(r != addr)
                {
                    addError("CheckRespond_FlashWrite4K: returned address didnt match?" + Environment.NewLine);
                    return false;
                }
                byte val = buf[10];
                // addLog("CheckRespond_FlashRead4K: OK");
                return true;
            }
            addError("CheckRespond_FlashWrite4K: bad value returned?" + Environment.NewLine);
            return false;
        }
        bool CheckRespond_FlashRead4K(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, (1 + 1 + (4 + 4 * 1024)) & 0xff,
                ((1 + 1 + (4 + 4 * 1024)) >> 8) & 0xff, (byte)CommandCode.FlashRead4K};
            if (cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                // addLog("CheckRespond_FlashRead4K: OK");
                return true;
            }
            addLog("CheckRespond_FlashRead4K: ERROR" + Environment.NewLine);
            return false;
        }

        bool CheckRespond_SetBaudRate(byte[] buf, int baudrate, int delay_ms)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)(CommandCode.SetBaudRate), 0, 0, 0, 0, 0 };
            cBuf[2] = 3 + 1 + 4 + 1;
            cBuf[7] = (byte)(baudrate & 0xff);
            cBuf[8] = (byte)((baudrate >> 8) & 0xff);
            cBuf[9] = (byte)((baudrate >> 16) & 0xff);
            cBuf[10] = (byte)((baudrate >> 24) & 0xff);
            cBuf[11] = (byte)(delay_ms & 0xff);

            return cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf);
        }
        bool CheckRespond_EraseSector4K(byte[] buf)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, 0x06, 0x00, (byte)(CommandCode.FlashErase4K)  };
            return cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length);
        }
        bool CheckRespond_FlashErase(byte[] buf, int szcmd)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, 1 + 1 + (1 + 4), 0x00, (byte)(CommandCode.FlashErase)  };
            return cBuf.Length <= buf.Length && szcmd == buf[11] && ByteArrayCompare(cBuf, buf, cBuf.Length);
        }
        bool CheckRespond_LinkCheck(byte[] buf)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)(CommandCode.LinkCheck) + 1, 0x00 };
            return cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf);
        }
        bool getBus()
        {
            int maxTries = 100;
            int loops = 1000;
            bool bOk = false;
            addLog("Getting bus... (now, please do reboot by CEN or by power off/on)" + Environment.NewLine);
            for (int tr = 0; tr < maxTries && !bOk; tr++)
            {
                if(tr % 10 == 0)
                {
                    //serial.RtsEnable = true;
                   // Thread.Sleep(10);
                   // serial.RtsEnable = false;
                    // OBK commandline reboot
                    serial.WriteLine("reboot");
                }
                for (int l = 0; l < loops && !bOk; l++)
                {
                    bOk = linkCheck();
                    if (bOk)
                    {
                        addSuccess("Getting bus success!" + Environment.NewLine);
                        return true;
                    }
                }
                addWarning("Getting bus failed, will try again - " + tr + "/" + maxTries + "!" + Environment.NewLine);
                if(tr % 10 == 9)
                {
                    addWarning("Reminder: you should do a device reboot now (do power off/on of the device, but don't disconnect UART or do a CEN short to ground for 0.25sec)" + Environment.NewLine);
                }
            }
            return false;
        }
        public override void doWrite(int startSector, byte [] data)
        {
            try
            {
                doWriteInternal(startSector, data);
            }
            catch (Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
            try
            {
                doTestReadWriteInternal(startSector, sectors);
            }
            catch (Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
        }
        
        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            try
            {
                doReadAndWriteInternal(startSector, sectors, sourceFileName, rwMode);
            }
            catch (Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
        }
        
        public override bool doErase(int startSector = 0x000, int sectors = 10)
        {
            try
            {
                logger.setProgress(0, sectors);
                addLog("Erase started with ofs " + startSector + " and len in sectors " + sectors);
                if (doGenericSetup() == false)
                {
                    return false;
                }
                if (doEraseInternal(startSector, sectors) == false)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
            return true;
        }
        public override void doRead(int startSector = 0x000, int sectors = 10)
        {
            try
            {
                doReadInternal(startSector, sectors);
            }
            catch(Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
        }
        public override byte[]getReadResult()
        {
            if (ms == null)
                return null;
            return ms.ToArray();
        }
        bool saveReadResult(string fileName)
        {
            if(ms == null)
            {
                addError("There was no result to save."+Environment.NewLine);
                return false;
            }
            byte[] dat = ms.ToArray();
            string fullPath = "backups/" + fileName;
            File.WriteAllBytes(fullPath, dat);
            addSuccess("Wrote " + dat.Length + " to " + fileName + Environment.NewLine);
            logger.onReadResultQIOSaved(dat, lastEncryptionKey, fullPath);
            return true;
        }
        public override bool saveReadResult(int startOffset)
        {
            string typeStr = "";
            if(startOffset == 0x11000)
            {
                typeStr = "UA";
            }
            else if(startOffset == 0x0)
            {
                typeStr = "QIO";
            }
            else
            {
                typeStr = startOffset.ToString();
            }
            string fileName = MiscUtils.formatDateNowFileName("readResult_"+chipType+ "_"+typeStr, backupName, "bin");
            return saveReadResult(fileName);
        }
        bool setBaudRateIfNeeded()
        {
            bool bOk = setBaudrate(baudrate, 200);
            return bOk;
        }

        bool doGetBusAndSetBaudRate()
        {
            logger.setState("Getting bus...", Color.Transparent);
            if (getBus() == false)
            {
                addError("Failed to get bus!" + Environment.NewLine);
                logger.setState("Failed to get bus!", Color.Red);
                return false;
            }
            Thread.Sleep(50);
            int attempt = 0;
            int maxAttempts = 0;
            while (true)
            {
                addSuccess("Going to set baud rate setting (" + baudrate + ")!" + Environment.NewLine);
                logger.setState("Setting baud rate...", Color.Transparent);
                if (setBaudRateIfNeeded() == false)
                {
                    addError("Failed to set baud rate!" + Environment.NewLine);
                    logger.setState("Failed to set baud rate!", Color.Red);
                    if (attempt >= maxAttempts)
                    {
                        return false;
                    }
                    Thread.Sleep(50);
                }
                else
                {
                    break;
                }

                attempt++;
            }
            Thread.Sleep(50);
            return true;
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
            if (doGetBusAndSetBaudRate() == false)
            {
                return false;
            }
            // make sure it's clear
            lastEncryptionKey = "";
            if (chipType == BKType.BK7231N || chipType == BKType.BK7231M || chipType == BKType.BK7238)
            {
                if (doUnprotect())
                {
                    return false;
                }
                if (chipType != BKType.BK7238)
                {
                    addLog("Going to read encryption key..." + Environment.NewLine);
                    string key = readEncryptionKey();
                    addLog("Encryption key read done!" + Environment.NewLine);
                    addLog("Encryption key: " + key + Environment.NewLine);
                    string otherMode;
                    string expectedKey;
                    if (chipType == BKType.BK7231N)
                    {
                        otherMode = "BK7231M";
                        expectedKey = TUYA_ENCRYPTION_KEY;
                    }
                    else
                    {
                        otherMode = "BK7231N";
                        expectedKey = EMPTY_ENCRYPTION_KEY;
                    }
                    if (key != expectedKey)
                    {
                        addError("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^" + Environment.NewLine);
                        addError("WARNING! Non-standard encryption key!" + Environment.NewLine);
                        addError("If it's all zero, it may also mean that read is disabled." + Environment.NewLine);
                        addError("Please report to forum https://www.elektroda.com/rtvforum/forum51.html " + Environment.NewLine);

                        if (chipType == BKType.BK7231N)
                        {
                            addError("Or just try using BK7231M mode " + Environment.NewLine);
                        }
                        else
                        {

                        }
                        addError("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^" + Environment.NewLine);
                        if (bSkipKeyCheck == false)
                        {
                            return false;
                        }
                    }
                    lastEncryptionKey = key;
                }
            }
            return true;
        }
        bool doEraseInternal(int startSector, int sectors)
        {
            logger.setProgress(0, sectors);
            logger.setState("Erasing...", Color.Transparent);
            addLog("Going to do erase, start " + startSector +", sec count " + sectors +"!" + Environment.NewLine);
            for (int sec = 0; sec < sectors; sec++)
            {
                int secAddr = startSector + SECTOR_SIZE * sec;
                // 4K erase
                bool bOk = eraseSector(secAddr, 0x20);
                addLog("Erasing sector " + secAddr + "...");
                if (bOk == false)
                {
                    logger.setState("Erase failed.", Color.Red);
                    addError(" Erasing sector " + secAddr + " failed!" + Environment.NewLine);
                    return false;
                }
                addLog(" ok! ");
                logger.setProgress(sec+1, sectors);
            }
            addLog(Environment.NewLine);
            addLog("All selected sectors erased!" + Environment.NewLine);
            return true;
        }
        int deviceMID;
        BKFlash flashInfo;


        bool doUnprotect()
        {
            addLog("Will try to read device flash MID (for unprotect N):" + Environment.NewLine);
            deviceMID = GetFlashMID();
            if (deviceMID == 0)
            {
                addError("Failed to read device MID!" + Environment.NewLine);
                return false;
            }
            addSuccess("Flash MID loaded: " + deviceMID.ToString("X2") + Environment.NewLine);
            addLog("Will now search for Flash def in out database..." + Environment.NewLine);
            flashInfo = BKFlashList.Singleton.findFlashForMID(deviceMID);
            if(flashInfo == null)
            {
                addError("Failed to find flash def for device MID!" + Environment.NewLine);
                return false;
            }
            addSuccess("Flash def found! For: " + deviceMID.ToString("X2") + Environment.NewLine);
            addLog("Flash information: " + flashInfo.ToString() + Environment.NewLine);
            if (setProtectState(true))
            {
                return false;
            }
            return true;
        }
        bool setProtectState(bool unprotect)
        {
            addSuccess("Entering SetProtectState(" + unprotect + ")..." + Environment.NewLine);
            int cw = unprotect ? flashInfo.cwUnp : flashInfo.cwEnp;
            int maxTries = 10;
            int tryNum = 0;
            while (true)
            {
                tryNum++;
                int sr = 0;

                // read sr register
                for (int i = 0; i < flashInfo.szSR; i++)
                {
                    // value is second, sr size will be [2]
                    byte [] srBytes = ReadFlashSR(flashInfo.cwdRd[i]);
                    if (srBytes != null)
                    {
                        sr |= srBytes[1] << (8 * i);
                        if (true)
                        {
                            addLog("sr: " + sr.ToString("x") + Environment.NewLine);
                        }
                    }
                    else
                    {
                        addError("SetProtectState(" + unprotect + ") failed because ReadFlashSR failed!" + Environment.NewLine);
                        return false;
                    }
                }

                if (true)
                {
                    addLog("final sr: " + sr.ToString("x") + Environment.NewLine);
                    addLog("msk: " + flashInfo.cwMsk.ToString("x") + Environment.NewLine);
                    addLog("cw: " + cw.ToString("x") + ", sb: " + flashInfo.sb + ", lb: " + flashInfo.lb + Environment.NewLine);
                    addLog("bfd: " + BKFlashList.BFD(cw, flashInfo.sb, flashInfo.lb).ToString("x") + Environment.NewLine);
                }

                // if (un)protect word is set
                if ((sr & flashInfo.cwMsk) == BKFlashList.BFD(cw, flashInfo.sb, flashInfo.lb))
                {
                    break;
                }
                if(tryNum >= maxTries)
                {
                    addError("SetProtectState(" + unprotect + ") failed after " + maxTries+ " retries!" + Environment.NewLine);
                    return false;
                }
                // set (un)protect word
                int srt = (int)(sr & (flashInfo.cwMsk ^ 0xffffffff));
                srt |= BKFlashList.BFD(cw, flashInfo.sb, flashInfo.lb);
                WriteFlashSR(flashInfo.szSR, flashInfo.cwdWr[0], srt & 0xffff);

                System.Threading.Thread.Sleep(10);
            }
            addSuccess("SetProtectState(" + unprotect + ") success!" + Environment.NewLine);
            return true;
        }
        bool writeChunk(int startSector, byte [] data, WriteMode rwMode)
        {
            OBKConfig cfg;
            if(rwMode == WriteMode.OnlyOBKConfig)
            {
                cfg = logger.getConfig();
            }
            else
            {
                cfg = logger.getConfigToWrite();
            }
            int ofs = OBKFlashLayout.getConfigLocation(chipType, out var sectors);
            logger.setState("Writing...", Color.Transparent);
            if (data != null)
            {
                data = MiscUtils.padArray(data, SECTOR_SIZE);
                sectors = data.Length / SECTOR_SIZE;
            }
            logger.setProgress(0, sectors);
            if (data != null)
            {
                if (doEraseInternal(startSector, sectors) == false)
                {
                    return false;
                }
            }
            if (cfg != null)
            {
                if (doEraseInternal(ofs, 1) == false)
                {
                    return false;
                }
            }
            logger.setState("Writing...", Color.Transparent);
            if (data != null)
            {
                for (int sec = 0; sec < sectors; sec++)
                {
                    int secAddr = startSector + SECTOR_SIZE * sec;
                    // 4K write
                    bool bOk = writeSector4K(secAddr, data, SECTOR_SIZE * sec);
                    //bool bOk = writeSector(secAddr, data, sectorSize * sec, SECTOR_SIZE);
                    addLog("Writing sector " + formatHex(secAddr) + "...");
                    if (bOk == false)
                    {
                        logger.setState("Writing error!", Color.Red);
                        addError(" Writing sector " + secAddr + " failed!" + Environment.NewLine);
                        return false;
                    }
                    logger.setProgress(sec + 1, sectors);
                    addLog(" ok! ");
                }
                if (false == checkCRC(startSector, sectors, data))
                {
                    logger.setState("Bad CRC!", Color.Red);
                    return false;
                }
            }

            addLog(Environment.NewLine);
            if (cfg != null)
            {
                addLog("Now will also write OBK config..." + Environment.NewLine);
                cfg.saveConfig();
                addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                addLog("Writing config sector " + formatHex(ofs) + "...");
                byte[] wd = MiscUtils.padArray(cfg.getData(), SECTOR_SIZE);
                bool bOk = writeSector4K(ofs, wd, 0);
                if (bOk == false)
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
            logger.setState("Write success!", Color.Green);
            return true;
        }
        bool doTestReadWriteInternal(int startSector = 0x11000, int sectors = 10)
        {
            addLog(Environment.NewLine + "Starting read-write test!" + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return false;
            }
            if (doEraseInternal(startSector, sectors) == false)
            {
                return false;
            }
            MemoryStream toCheck = readChunk(startSector, sectors);
            if (toCheck == null)
            {
                addError("Read failed?" + Environment.NewLine);
                return false;
            }
            if (isFullOf(toCheck.ToArray(), 0xff)==false)
            {
                addError("Erase verify error? Flash was not full of 0xFF!" + Environment.NewLine);
                return false;
            }
            addSuccess("After erase, flash was full of 0xff" + Environment.NewLine);
            byte[] data = new byte[sectors * SECTOR_SIZE];
            rand.NextBytes(data);
            for(int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            // NOTE: it must be done again, i checked many times,
            // if i do an erase, and then read, then next write fails.
            // it must be write dirrectly after erase
            if (doGetBusAndSetBaudRate() == false)
            {
                return false;
            }
            if (chipType == BKType.BK7231N || chipType == BKType.BK7231M || chipType == BKType.BK7238)
            {
                if (doUnprotect())
                {
                    return false;
                }
            }
            if (writeChunk(startSector, data,WriteMode.OnlyWrite) == false)
            {
                return false;
            }
            MemoryStream toCheck2 = readChunk(startSector, sectors);
            byte[] toCheck2Array = toCheck2.ToArray();
            if (ByteArrayCompare(toCheck2Array, data) == false)
            {
                addError("Failed! Loaded data was different than the written one?!" + Environment.NewLine);
                return false;
            }
            addSuccess("Check passed! Loaded data was the same as written!");
            return true;
        }
        bool doWriteInternal(int startSector, byte []data)
        {
            int sectors = data.Length/ SECTOR_SIZE;
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting write test!" + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return false;
            }
            for (int sec = 0; sec < sectors; sec++)
            {
                int secAddr = startSector + SECTOR_SIZE * sec;
                // 4K erase
                bool bOk = eraseSector(secAddr, 0x20);
                addLog("Erasing sector " + formatHex(secAddr) + "...");
                if (bOk == false)
                {
                    logger.setState("Erase sector failed!", Color.Red);
                    addError(" Erasing sector " + secAddr + " failed!" + Environment.NewLine);
                    return false;
                }
                addLog(" ok! ");

            }
            addLog(Environment.NewLine);
            addLog("All selected sectors erased!" + Environment.NewLine);
            for (int sec = 0; sec < sectors; sec++)
            {
                int secAddr = startSector + SECTOR_SIZE * sec;
                // 4K write
                bool bOk = writeSector4K(secAddr, data, SECTOR_SIZE * sec);
                //bool bOk = writeSector(secAddr, data, SECTOR_SIZE * sec, SECTOR_SIZE);
                addLog("Writing sector " + secAddr + "...");
                if (bOk == false)
                {
                    logger.setState("Write sector failed!", Color.Red);
                    addError(" Writing sector " + secAddr + " failed!" + Environment.NewLine);
                    return false;
                }
                addLog(" ok! ");
            }
            if (false == checkCRC(startSector, sectors, data))
            {
                return false;
            }
            addSuccess("Write success!");
            return true;
        }
        bool isFullOf(byte [] dat, byte c)
        {
            for(int i = 0; i < dat.Length; i++)
            {
                if (dat[i] != c)
                    return false;
            }
            return true;
        }
        string readEncryptionKey()
        {
            int SCTRL_EFUSE_CTRL = 0x00800074;
            int SCTRL_EFUSE_OPTR = 0x00800078;
            byte[] efuse = new byte[16];
            for (int addr = 0; addr < 16; addr++)
            {
                int reg = ReadFlashRegInt(SCTRL_EFUSE_CTRL);
                reg = (reg & ~0x1F02) | (addr << 8) | 1;
                WriteFlashReg(SCTRL_EFUSE_CTRL, reg);
                while ((reg & 1) != 0)
                {
                    reg = ReadFlashRegInt(SCTRL_EFUSE_CTRL);
                }
                reg = ReadFlashRegInt(SCTRL_EFUSE_OPTR);
                if ((reg & 0x100) != 0)
                {
                    int regb = (reg & 0xff);
                    efuse[addr] = (byte)regb;
                    Console.WriteLine($"Efuse ok at {addr} (0x{addr:X}) is {regb} (0x{regb:X})");
                }
                else
                {
                    Console.WriteLine("Efuse error at " + addr);
                }
            }
            uint[] coeffs = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                coeffs[i] = ((uint)efuse[i * 4]) |
                            ((uint)efuse[i * 4 + 1] << 8) |
                            ((uint)efuse[i * 4 + 2] << 16) |
                            ((uint)efuse[i * 4 + 3] << 24);
            }

            string encryptionKey = string.Join(" ", Array.ConvertAll(coeffs, c => c.ToString("x8")));
            Console.WriteLine("Encryption Key: " + encryptionKey);
            return encryptionKey;
        }
        MemoryStream readChunk(int startSector, int sectors)
        {
            logger.setState("Reading...", Color.Transparent);
            logger.setProgress(0, sectors);
            MemoryStream tempResult = new MemoryStream();

            int step = 4096;
           // startSector = 0x11000;
            // 4K page align
            startSector = (int)(startSector & 0xfffff000);
            addLog("Going to start reading at offset " + formatHex(startSector) + "..." + Environment.NewLine);
            for (int i = 0; i < sectors; i++)
            {
                int addr = startSector + step * i;
                addLog("Reading " + formatHex(addr) + "... ");
                // BK7231T does not allow bootloader read, but we can use a wrap-around hack
                if(chipType == BKType.BK7252)
                {
                    // wrap around breaks stuff here, maybe it has 4MB flash?
                    //addr += 0x400000;// not working for BK7252 as well
                }
                else
                {
                    addr += FLASH_SIZE;
                }
                bool bOk = readSectorTo(addr, tempResult);
                if (bOk == false)
                {
                    logger.setState("Reading failed.", Color.Red);
                    addError("Failed! ");
                    return null;
                }
                // dev only
                //if(checkAbnormal(0,(int)tempResult.Length,tempResult))
                //{
                //    logger.setState("Reading failed.", Color.Red);
                //    addError("Failed! ");
                //    return null;
                //}
                logger.setProgress(i + 1, sectors);
                addLog("Ok! ");
            }
            addLog(Environment.NewLine + "Basic read operation finished, but now it's time to verify..." + Environment.NewLine);

            if (false == checkAbnormal(startSector, sectors, tempResult.ToArray()))
            {
                return null;
            }
            if (false==checkCRC(startSector, sectors, tempResult.ToArray()))
            {
                return null;
            }
            logger.setState("Reading success!", Color.Green);
            addSuccess("All read!" + Environment.NewLine);
            addLog("Loaded total " + formatHex(sectors* step) + " bytes " + Environment.NewLine);
            return tempResult;
        }
        bool checkAbnormal(int startSector, int total, byte[] array)
        {
            bool isAllZero = true;
            for (int i = 0; i < array.Length; i++) {
                if (array[i] != 0x00) {
                    isAllZero = false;
                    break;
                }
            }
            if (isAllZero) {
                logger.setState("Only 0x00 bytes read!", Color.Red);
                addError("Data is entirely filled with 0x00, something must went wrong!" + Environment.NewLine);
                return false;
            }
            
            bool isAllFF = true;
            for (int i = 0; i < array.Length; i++) {
                if (array[i] != 0xFF)   {
                    isAllFF = false;
                    break;
                }
            }
            if (isAllFF)  {
                logger.setState("Only 0xff bytes read!", Color.Red);
                addError("Data is entirely filled with 0xff, something must went wrong!" + Environment.NewLine);
                return false;
            }
            return true;
        }
        bool checkCRC(int startSector, int total, byte [] array)
        {
            logger.setState("Doing CRC verification...", Color.Transparent);
            addLog("Starting CRC check for " + total + " sectors, starting at offset 0x" + startSector.ToString("X2") + Environment.NewLine);
            int last = startSector + total * SECTOR_SIZE;
            uint bk_crc = calcCRC(startSector, last);
            uint our_crc = CRC.crc32_ver2(0xffffffff, array);
            if (bk_crc != our_crc)
            {
                logger.setState("CRC mismatch!", Color.Red);
                addError("CRC mismatch!" + Environment.NewLine);
                addError("Send by BK " + formatHex(bk_crc) + ", our CRC " + formatHex(our_crc) + Environment.NewLine);
                addError("Maybe you have wrong chip type set?" + Environment.NewLine);
                addError("Did you set BK7231T but have in reality BK7231N or BK7231M?" + Environment.NewLine);
                if (bIgnoreCRCErr) {
                    addWarning("IgnoreCRCErr checked, bin will be saved even if there is a crc mismatch" + Environment.NewLine);
                    return true;
                }
                return false;
            }
            addSuccess("CRC matches " + formatHex(bk_crc) + "!" + Environment.NewLine);
            return true;
        }

        bool doReadAndWriteInternal(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            logger.setProgress(0, sectors);
            if (rwMode == WriteMode.OnlyWrite)
            {
                addLog(Environment.NewLine + "Starting flash new (no backup)!" + Environment.NewLine);
            }
            else if (rwMode == WriteMode.OnlyOBKConfig)
            {
                addLog(Environment.NewLine + "Starting write only OBK config!" + Environment.NewLine);
            }
            else
            {
                addLog(Environment.NewLine + "Starting read backup and flash new!" + Environment.NewLine);
            }
            if (doGenericSetup() == false)
            {
                return false;
            }
            // hack
            int realStart = 0;
            int realLen = TOTAL_SECTORS;
            if(chipType == BKType.BK7252)
            {
                realStart = 0x11000;
                realLen = TOTAL_SECTORS - (0x11000 / 0x1000);
            }
            if (rwMode == WriteMode.ReadAndWrite)
            {
                //ms = readChunk(startSector, sectors);
                ms = readChunk(realStart, realLen);
                if (ms == null)
                {
                    return false;
                }
                if (saveReadResult(realStart) == false)
                {
                    return false;
                }
            }

            byte[] data = null;
            if (rwMode != WriteMode.OnlyOBKConfig)
            { 
                addLog("Reading file " + sourceFileName + "..." + Environment.NewLine);
                data = File.ReadAllBytes(sourceFileName);
                if (data == null)
                {
                    addError("Failed to open " + sourceFileName + "..." + Environment.NewLine);
                    return false;
                }
                addSuccess("Loaded " + data.Length + " bytes from " + sourceFileName + "..." + Environment.NewLine);
                bool bSkipBootloader = false;
                if (sourceFileName.Contains("_QIO_"))
                {
                    if(bOverwriteBootloader == false)
                    {
                        startSector = BK7231Flasher.BOOTLOADER_SIZE;
                        bSkipBootloader = true;
                    }
                    if(this.chipType == BKType.BK7231T)
                    {
                        startSector = BK7231Flasher.BOOTLOADER_SIZE;
                        bSkipBootloader = true;
                    }
                }
                if (bSkipBootloader && startSector == BK7231Flasher.BOOTLOADER_SIZE)
                {
                    // very hacky, but skip bootloader
                    int length = data.Length - startSector;
                    byte[] newData = new byte[length];
                    Array.Copy(data, startSector, newData, 0, length);
                    data = newData;
                    addWarning("Using hack to write QIO - just skip bootloader..." + Environment.NewLine);
                    addWarning("... so bootloader will not be overwritten!" + Environment.NewLine);
                }
            }
            addLog("Preparing to write data file to chip - resetting bus and baud..." + Environment.NewLine);
            // it must be redone
            if (doGetBusAndSetBaudRate() == false)
            {
                return false;
            }
            if (chipType == BKType.BK7231N || chipType == BKType.BK7231M || chipType == BKType.BK7238)
            {
                if (doUnprotect())
                {
                    return false;
                }
            }
            if (writeChunk(startSector, data, rwMode) == false)
            {
                addError("Writing file data to chip failed." + Environment.NewLine);
                return false;
            }
            addSuccess("Writing file data to chip successs." + Environment.NewLine);
            //File.WriteAllBytes("lastRead.bin", ms.ToArray());
            return true;
        }
        void doReadInternal(int startSector = 0x000, int sectors = 10)
        {
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting read!" + Environment.NewLine);
            addLog("Read parms: start 0x"+
                (startSector ).ToString("X2")
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
        }
        bool readSectorTo(int addr, MemoryStream tg)
        {
            byte[] res = readSector(addr);
            if (res != null)
            {
                int start_ofs = 15;
                tg.Write(res, start_ofs, res.Length - start_ofs);
                return true;
            }
            return false;
        }
        int CalcRxLength_FlashWrite4K()
        {
            return (3 + 3 + 3 + (1 + 1 + (4 + 0)));
        }
        int CalcRxLength_FlashWrite()
        {
            return (3 + 3 + 3 + (1 + 1 + (4 + 1)));
        }
        int CalcRxLength_FlashRead4K()
        {
            return (3 + 3 + 3 + (1 + 1 + (4 + 4 * 1024)));
        }
        int CalcRxLength_ReadFlashReg()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 3)));
        }
        int CalcRxLength_WriteFlashReg()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 3)));
        }
        int CalcRxLength_ReadFlashSR()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 1)));
        }
        int CalcRxLength_FlashWriteSR()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 1)));
        }
        int CalcRxLength_FlashWriteSR2()
        {
            return (3 + 3 + 3 + (1 + 1 + (1 + 2)));
        }
        int CalcRxLength_FlashGetID()
        {
            return (3 + 3 + 3 + (1 + 1 + (4)));
        }
        int GetFlashMID()
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashGetMID(0x9f);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashGetID());
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                return CheckRespond_FlashGetMID(rxbuf);
            }
            //addLog("Failed!" + Environment.NewLine);
            return 0;
        }
        bool WriteFlashReg(int addr, int val)
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_WriteReg(addr, val);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_WriteFlashReg());
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                return CheckRespond_WriteReg(rxbuf, addr, val);
            }
            //addLog("Failed!" + Environment.NewLine);
            return false;
        }
        byte[] ReadFlashReg(int addr)
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_ReadRegn(addr);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_ReadFlashReg());
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                return CheckRespond_ReadFlashReg(rxbuf, addr);
            }
            //addLog("Failed!" + Environment.NewLine);
            return null;
        }

        int ReadFlashRegInt(int addr)
        {
            byte[] r = ReadFlashReg(addr);
            if (r != null)
            {
                //return BitConverter.ToInt32(r, 0); ;
              int value = (r[3] << 24) | (r[2] << 16) | (r[1] << 8) | r[0];
                //   int value = (r[0] << 24) | (r[1] << 16) | (r[2] << 8) | r[3];
                return value;
            }
            return 0;
        }
        byte[] ReadFlashSR(int addr)
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashReadSR(addr);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_ReadFlashSR());
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                return CheckRespond_FlashReadSR(rxbuf, addr);
            }
            //addLog("Failed!" + Environment.NewLine);
            return null;
        }
        bool WriteFlashSR(int size, int addr, int val)
        {
            byte[] txbuf;
            int rxlen;
            if(size == 1)
            {
                txbuf = BuildCmd_FlashWriteSR(addr, val);
                rxlen = CalcRxLength_FlashWriteSR();
            }
            else
            {
                txbuf = BuildCmd_FlashWriteSR2(addr, val);
                rxlen = CalcRxLength_FlashWriteSR2();
            }
            byte[] rxbuf = Start_Cmd(txbuf, rxlen);
            if (rxbuf != null)
            {
                if(size == 1)
                {
                    //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                    if (CheckRespond_FlashWriteSR(rxbuf, addr, val)!=null)
                    {
                        return true;
                    }
                }
                else
                {
                    //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                    if (CheckRespond_FlashWriteSR2(rxbuf, addr, val) != null)
                    {
                        return true;
                    }
                }
            }
            //addLog("Failed!" + Environment.NewLine);
            return false;
        }
        bool writeSector4K(int addr, byte [] data, int first)
        {
            if (isSectorModificationAllowed(addr) == false)
            {
                return false;
            }
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashWrite4K(addr, data, first);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashWrite4K());
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                if (CheckRespond_FlashWrite4K(rxbuf, addr))
                {
                    return true;
                }
            }
            //addLog("Failed!" + Environment.NewLine);
            return false;
        }
        bool writeSector(int addr, byte[] data, int first, int dataSize)
        {
            if (isSectorModificationAllowed(addr) == false)
            {
                return false;
            }
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashWrite(addr, data, first, dataSize);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashWrite(), 5);
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                if (CheckRespond_FlashWrite(rxbuf, addr))
                {
                    return true;
                }
            }
            //addLog("Failed!" + Environment.NewLine);
            return false;
        }
        byte[] readSector(int addr)
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashRead4K(addr);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashRead4K(), 15);
            if (rxbuf != null)
            {
                //addLog("Loaded " + rxbuf.Length + " bytes!" + Environment.NewLine);
                if (CheckRespond_FlashRead4K(rxbuf, addr))
                {
                    return rxbuf;
                }
            }
            //addLog("Failed!" + Environment.NewLine);
            return null;
        }
        uint calcCRC(int start, int end)
        {
            if (chipType == BKType.BK7231N || chipType == BKType.BK7231M || chipType == BKType.BK7238)
            {
                end = end - 1;
            }
            byte[] txbuf = BuildCmd_CheckCRC(start, end);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_CheckCRC(), 5.0f);
            if (rxbuf != null)
            {
                uint r = CheckRespond_CheckCRC(rxbuf, start, end);
                return r;
            }
            return 0;
        }
        bool linkCheck()
        {
            byte[] txbuf = BuildCmd_LinkCheck();
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_LinkCheck(), 0.001f);
            if (rxbuf != null)
            {
                if (CheckRespond_LinkCheck(rxbuf))
                {
                    return true;
                }
            }
            return false;
        }
        bool isSectorModificationAllowed(int addr)
        {
            if (addr >= FLASH_SIZE)
            {
                addError("ERROR: Out of range write/erase attempt detected, this could break bootloader");
                return false;
            }
            addr %= FLASH_SIZE;
            if (chipType == BKType.BK7231N || chipType == BKType.BK7231M || chipType == BKType.BK7238) 
                return true;
            if (addr >= 0 && addr < BOOTLOADER_SIZE)
            {
                addError("ERROR: T bootloader overwriting attempt detected, interrupting.");
                return false;
            }
            return true;
        }
        bool eraseSector4K(int addr)
        {
            if (isSectorModificationAllowed(addr) == false)
            {
                return false;
            }
            byte[] txbuf = BuildCmd_EraseSector4K(addr, 0);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_EraseSector4K(), 1.0f);
            if (rxbuf != null)
            {
                if (CheckRespond_EraseSector4K(rxbuf))
                {
                    return true;
                }
            }
            return false;
        }
        bool eraseSector(int addr, int szcmd)
        {
            if (isSectorModificationAllowed(addr) == false)
            {
                return false;
            }
            byte[] txbuf = BuildCmd_FlashErase(addr, szcmd);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashErase(), 1.0f);
            if (rxbuf != null)
            {
                if (CheckRespond_FlashErase(rxbuf, szcmd))
                {
                    return true;
                }
            }
            return false;
        }
        bool setBaudrate(int baudrate, int delay_ms)
        {
            byte[] txbuf = BuildCmd_SetBaudRate(baudrate, delay_ms);
            Start_Cmd(txbuf,0, 0.5f);
            while (serial.BytesToWrite > 0)
            {

            }
            Thread.Sleep(delay_ms/2);
            serial.BaudRate = baudrate;
            byte[] rxbuf = Start_Cmd(null, CalcRxLength_SetBaudRate(), 0.5f);
            if (rxbuf != null)
            {
                if (CheckRespond_SetBaudRate(rxbuf, baudrate, delay_ms))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
