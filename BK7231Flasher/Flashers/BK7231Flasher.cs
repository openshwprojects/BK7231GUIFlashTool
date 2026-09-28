using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace BK7231Flasher
{
    public class BK7231Flasher : BaseFlasher, IRomReadFlasher
    {
        enum BekenLinkStage
        {
            Unknown,
            BootRom,
            Bl2,
        }

        enum CRCVerificationResult
        {
            Match,
            Mismatch,
            TransportError,
        }

        public static Random rand = new Random(Guid.NewGuid().GetHashCode());

        bool bDebugUART;
        MemoryStream ms;
        string lastEncryptionKey;
        BKChipIdentityResult chipIdentity;
        const int DEFAULT_FLASH_SIZE = 0x200000;
        const int BK7252_MAX_FLASH_SIZE = 0x400000;
        const int READ_RESPONSE_HEADER_SIZE = 15;
        const int BK7252_READ_ATTEMPTS = 20;
        const int FLASH_READ4K_ATTEMPTS = 20;
        const int READ_RANGE_ATTEMPTS = 2;
        const int READ_RANGE_CRC_ATTEMPTS = 2;
        const int VERIFIED_PAGE_WRITE_ATTEMPTS = 5;
        const int FLASH_MID_ATTEMPTS = 5;
        const int FLASH_MID_RETRY_DELAY_MS = 200;
        const int ERASE_ATTEMPTS = 5;
        const float FLASH_COMMAND_MIN_TIMEOUT = 0.5f;
        const int SET_BAUD_DRAIN_TIMEOUT_MS = 1000;
        const int BEKEN_EFUSE_SIZE = 0x20;
        const int FOUR_BYTE_BEKEN_EFUSE_SIZE = 0x04;
        const int SCTRL_EFUSE_CTRL = 0x00800074;
        const int SCTRL_EFUSE_OPTR = 0x00800078;
        const int BK7258_SYS_DEVICE_CLK_ENABLE = 0x54010030;
        const int BK7258_SYS_POWER_SLEEP_WAKEUP = 0x54010040;
        const int BK7258_EFUSE_CLOCK_ENABLE = 1 << 7;
        const int BK7258_OTP_CLOCK_ENABLE = 1 << 15;
        const int BK7258_OTP_POWER_DOWN = 1 << 3;
        const int BK7258_EFUSE_CTRL = 0x54880010;
        const int BK7258_EFUSE_OPTR = 0x54880014;
        const int BK7239N_EFUSE_DATA_BASE = 0x5B10043C;
        const int BEKEN_OTP1_APB_DATA_BASE = 0x5B100400;
        const int BEKEN_OTP1_APB_DATA_SIZE = 0x400;
        const int BEKEN_OTP2_AHB_DATA_BASE = 0x5B010000;
        const int BEKEN_OTP2_AHB_DATA_SIZE = 0xC00;
        public static int SECTOR_SIZE = 0x1000;
        public static int BLOCK_SIZE = 0x10000;
        public static int SECTORS_PER_BLOCK = BLOCK_SIZE / SECTOR_SIZE;
        public static int FLASH_SIZE = DEFAULT_FLASH_SIZE;
        public static int BOOTLOADER_SIZE = 0x11000;
        public static int TOTAL_SECTORS = FLASH_SIZE / SECTOR_SIZE;
        public static string TUYA_ENCRYPTION_KEY = "510fb093 a3cbeadc 5993a17e c7adeb03";
        public static string EMPTY_ENCRYPTION_KEY = "00000000 00000000 00000000 00000000";
        int bk7252ReadAddressBase = DEFAULT_FLASH_SIZE;
        BekenLinkStage observedLinkStage = BekenLinkStage.Unknown;
        readonly object modificationSessionLock = new object();
        bool flashProtectionSessionActive;
        bool closePortDeferredForProtectionRestore;
        int? originalFlashProtectionBits;
        public bool LastOperationSucceeded { get; private set; }
        bool openPort()
        {
            // Close any previously open port before re-opening
            if (serial != null)
            {
                try
                {
                    if (serial.IsOpen)
                        serial.Close();
                    serial.Dispose();
                }
                catch { }
                serial = null;
            }
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
            lock (modificationSessionLock)
            {
                if (flashProtectionSessionActive)
                {
                    closePortDeferredForProtectionRestore = true;
                    return;
                }
            }
            if (serial != null)
            {
                serial.Close();
                serial.Dispose();
                serial = null;
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
            return BuildCmd_LinkCheck((byte)CommandCode.LinkCheck);
        }

        byte[] BuildCmd_LinkCheck(byte linkCommand)
        {
            byte[] ret = new byte[5];
            ret[0] = 0x01;
            ret[1] = 0xe0;
            ret[2] = 0xfc;
            ret[3] = 0x01;
            ret[4] = linkCommand;
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
            int pending = serial.BytesToRead;
            while (pending > 0)
            {
                int readLength = Math.Min(tmp.Length, pending);
                int readNow = serial.Read(tmp, 0, readLength);
                if (readNow <= 0)
                {
                    break;
                }
                pending -= readNow;
            }
        }

        bool tryGetResponseHeader(byte[] txbuf, byte? expectedResponseCommand, out bool f4Framed, out byte command)
        {
            f4Framed = txbuf != null && txbuf.Length > 7 && txbuf[3] == 0xff && txbuf[4] == 0xf4;
            if (expectedResponseCommand.HasValue)
            {
                command = expectedResponseCommand.Value;
                return true;
            }
            if (txbuf == null)
            {
                command = 0;
                return false;
            }
            command = f4Framed ? txbuf[7] : txbuf[4];
            if (f4Framed == false && (command == 0x00 || command == 0x02))
            {
                command++;
            }
            return true;
        }

        bool responseHeaderMatches(List<byte> received, int offset, int rxLen, bool f4Framed, byte command)
        {
            if (received[offset] != 0x04 || received[offset + 1] != 0x0e)
            {
                return false;
            }
            if (f4Framed)
            {
                int declaredLength = received[offset + 7] | (received[offset + 8] << 8);
                int expectedLength = rxLen - 9;
                bool lengthMatches = declaredLength == expectedLength
                    || (command == (byte)CommandCode.FlashGetMID && declaredLength == expectedLength - 1);
                return lengthMatches && received[offset + 2] == 0xff && received[offset + 3] == 0x01
                    && received[offset + 4] == 0xe0 && received[offset + 5] == 0xfc
                    && received[offset + 6] == 0xf4 && received[offset + 9] == command;
            }
            return received[offset + 2] + 3 == rxLen
                && received[offset + 3] == 0x01 && received[offset + 4] == 0xe0
                && received[offset + 5] == 0xfc && received[offset + 6] == command;
        }

        bool responseDetailsMatch(byte[] response, byte[] txbuf, bool f4Framed, byte command)
        {
            if (txbuf == null)
            {
                return true;
            }
            if (f4Framed == false && command == (byte)CommandCode.ReadReg && response.Length >= 11)
            {
                return readInt32LE(response, 7) == readInt32LE(txbuf, 5);
            }
            if (f4Framed == false)
            {
                return true;
            }
            if (command == (byte)CommandCode.FlashWrite && response.Length >= 15)
            {
                return readInt32LE(response, 11) == readInt32LE(txbuf, 8);
            }
            if ((command == (byte)CommandCode.FlashRead4K || command == (byte)CommandCode.FlashWrite4K)
                && usesDirectFlashAccessProfile() && response.Length >= 15)
            {
                return readInt32LE(response, 11) == readInt32LE(txbuf, 8);
            }
            if (command == (byte)CommandCode.FlashErase && usesDirectFlashAccessProfile() && response.Length >= 16)
            {
                return response[11] == txbuf[8] && readInt32LE(response, 12) == readInt32LE(txbuf, 9);
            }
            if (command == (byte)CommandCode.FlashReadSR && response.Length >= 13)
            {
                return response[11] == txbuf[8];
            }
            if (command == (byte)CommandCode.FlashWriteSR && response.Length >= 13)
            {
                int valueLength = response.Length - 12;
                for (int i = 0; i < valueLength; i++)
                {
                    if (response[12 + i] != txbuf[9 + i])
                    {
                        return false;
                    }
                }
                return response[11] == txbuf[8];
            }
            return true;
        }

        bool tryExtractResponse(List<byte> received, int rxLen, byte[] txbuf, byte? expectedResponseCommand, out byte[] response)
        {
            response = null;
            if (tryGetResponseHeader(txbuf, expectedResponseCommand, out bool f4Framed, out byte command) == false)
            {
                return false;
            }
            int headerLength = f4Framed ? 10 : 7;
            while (received.Count >= headerLength)
            {
                int headerOffset = -1;
                for (int offset = 0; offset <= received.Count - headerLength; offset++)
                {
                    if (responseHeaderMatches(received, offset, rxLen, f4Framed, command))
                    {
                        headerOffset = offset;
                        break;
                    }
                }
                if (headerOffset < 0)
                {
                    received.RemoveRange(0, received.Count - headerLength + 1);
                    return false;
                }
                if (headerOffset > 0)
                {
                    received.RemoveRange(0, headerOffset);
                }
                if (received.Count < rxLen)
                {
                    return false;
                }
                byte[] candidate = received.GetRange(0, rxLen).ToArray();
                if (responseDetailsMatch(candidate, txbuf, f4Framed, command))
                {
                    response = candidate;
                    return true;
                }
                received.RemoveRange(0, rxLen);
            }
            return false;
        }

        byte[] Start_Cmd(byte[] txbuf, int rxLen = 0, float timeout = 0.05f, byte? expectedResponseCommand = null)
        {
            if (txbuf != null)
            {
                consumePending();
            }
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
                List<byte> received = new List<byte>(rxLen);
                byte[] readBuffer = new byte[Math.Min(Math.Max(rxLen, 256), 4096)];
                while (timer.Elapsed.TotalSeconds < timeout * cfg_readTimeOutMultForLoop)
                {
                    try
                    {
                        if (cfg_readReplyStyle == 0)
                        {
                            //addLog("serial.BytesToRead " + serial.BytesToRead+"");
                            int available = serial.BytesToRead;
                            if (available > 0 && (received.Count > 0 || available >= rxLen))
                            {
                                //  addLog("Tries to read!");
                                int readNow = serial.Read(readBuffer, 0, Math.Min(readBuffer.Length, available));
                                for (int i = 0; i < readNow; i++)
                                {
                                    received.Add(readBuffer[i]);
                                }
                                if (bDebugUART)
                                {
                                    addLog("Read len: " + received.Count + Environment.NewLine);
                                }
                                if (tryExtractResponse(received, rxLen, txbuf, expectedResponseCommand, out byte[] response))
                                {
                                    if (bDebugUART)
                                    {
                                        addLog("Got UART reply!" + Environment.NewLine);
                                    }
                                    return response;
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
                                int readNow = serial.Read(readBuffer, 0, Math.Min(readBuffer.Length, ava));
                                for (int i = 0; i < readNow; i++)
                                {
                                    received.Add(readBuffer[i]);
                                }
                                if (bDebugUART)
                                {
                                    addLog("Read len: " + received.Count + Environment.NewLine);
                                }
                                if (tryExtractResponse(received, rxLen, txbuf, expectedResponseCommand, out byte[] response))
                                {
                                    if (bDebugUART)
                                    {
                                        addLog("Got UART reply!" + Environment.NewLine);
                                    }
                                    return response;
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
            if (buf.Length >= 13 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length)
                && buf[10] == 0 && regAddr == buf[11] && (byte)val == buf[12])
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
            if (buf.Length >= 14 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length)
                && buf[10] == 0 && regAddr == buf[11]
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
            if (buf.Length >= 13 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length)
                && buf[10] == 0 && addr == buf[11])
            {
                byte[] ret = new byte[2] { buf[11], buf[12] };
                return ret;
            }
            addError("CheckRespond_FlashReadSR: bad value returned?" + Environment.NewLine);
            return null;
        }
        int CheckRespond_FlashGetMID(byte[] buf)
        {
            byte[] cBuf = new byte[] { 0x04,0x0e,0xff,0x01,0xe0,0xfc,0xf4,(1+4)&0xff,
                    ((1+4)>>8)&0xff,(byte)CommandCode.FlashGetMID};
            if (buf.Length >= 15 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                if (usesDirectFlashAccessProfile() && buf[10] != 0)
                {
                    addError("FlashGetMID returned status " + buf[10] + "." + Environment.NewLine);
                    return 0;
                }
                return BitConverter.ToInt32(buf, 11) >> 8;
            }
            // Some BootROM revisions report one extra response byte in the length field.
            cBuf[7] += 1;
            if (buf.Length >= 15 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                if (usesDirectFlashAccessProfile() && buf[10] != 0)
                {
                    addError("FlashGetMID returned status " + buf[10] + "." + Environment.NewLine);
                    return 0;
                }
                return BitConverter.ToInt32(buf, 11) >> 8;
            }
            addError("CheckRespond_FlashGetMID: bad value returned?" + Environment.NewLine);
            return 0;
        }
        bool CheckRespond_FlashWrite4K(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, (1 + 1 + (4)) & 0xff,
               0, (byte)CommandCode.FlashWrite4K};
            if (buf.Length >= 15 && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                if (usesDirectFlashAccessProfile() && buf[10] != 0)
                {
                    addError("FlashWrite4K returned status " + buf[10] + " at " + formatHex(addr) + "." + Environment.NewLine);
                    return false;
                }
                int returnedAddress = readInt32LE(buf, 11);
                if(returnedAddress != addr)
                {
                    addError("FlashWrite4K returned address " + formatHex(returnedAddress)
                        + " instead of " + formatHex(addr) + "." + Environment.NewLine);
                    return false;
                }
                return true;
            }
            addError("CheckRespond_FlashWrite4K: bad value returned?" + Environment.NewLine);
            return false;
        }
        bool CheckRespond_FlashRead4K(byte[] buf, int addr)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, (1 + 1 + (4 + 4 * 1024)) & 0xff,
                ((1 + 1 + (4 + 4 * 1024)) >> 8) & 0xff, (byte)CommandCode.FlashRead4K};
            if (buf.Length >= READ_RESPONSE_HEADER_SIZE + SECTOR_SIZE
                && cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf, cBuf.Length))
            {
                if (usesDirectFlashAccessProfile())
                {
                    if (buf[10] != 0)
                    {
                        addWarning("FlashRead4K returned status " + buf[10] + " at " + formatHex(addr) + "." + Environment.NewLine);
                        return false;
                    }
                    int returnedAddress = readInt32LE(buf, 11);
                    if (returnedAddress != addr)
                    {
                        addWarning("FlashRead4K returned address " + formatHex(returnedAddress)
                            + " instead of " + formatHex(addr) + "." + Environment.NewLine);
                        return false;
                    }
                }
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
        bool CheckRespond_FlashErase(byte[] buf, int addr, int szcmd)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0xff, 0x01, 0xe0, 0xfc, 0xf4, 1 + 1 + (1 + 4), 0x00, (byte)(CommandCode.FlashErase) };
            if (cBuf.Length > buf.Length || ByteArrayCompare(cBuf, buf, cBuf.Length) == false
                || buf.Length < 12 || szcmd != buf[11])
            {
                return false;
            }
            if (usesDirectFlashAccessProfile())
            {
                if (buf.Length < 16)
                {
                    addWarning("FlashErase returned a short response." + Environment.NewLine);
                    return false;
                }
                if (buf[10] != 0)
                {
                    addWarning("FlashErase returned status " + buf[10] + " at " + formatHex(addr) + "." + Environment.NewLine);
                    return false;
                }
                int returnedAddress = readInt32LE(buf, 12);
                if (returnedAddress != addr)
                {
                    addWarning("FlashErase returned address " + formatHex(returnedAddress)
                        + " instead of " + formatHex(addr) + "." + Environment.NewLine);
                    return false;
                }
            }
            return true;
        }
        bool CheckRespond_LinkCheck(byte[] buf)
        {
            byte[] cBuf = new byte[] { 0x04, 0x0e, 0x05, 0x01, 0xe0, 0xfc, (byte)(CommandCode.LinkCheck) + 1, 0x00 };
            return cBuf.Length <= buf.Length && ByteArrayCompare(cBuf, buf);
        }
        static int readInt32LE(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        bool usesDirectFlashAccessProfile()
        {
            switch (chipType)
            {
                case BKType.BK7231M:
                case BKType.BK7231N:
                case BKType.BK7236:
                case BKType.BK7238:
                case BKType.BK7239N:
                case BKType.BK7252N:
                case BKType.BK7258:
                    return true;
                default:
                    return false;
            }
        }

        bool tryDecodeLinkStage(byte requestCommand, byte[] response, out BekenLinkStage stage)
        {
            stage = BekenLinkStage.Unknown;
            if (response == null || response.Length < CalcRxLength_LinkCheck())
            {
                return false;
            }
            if (response[0] != 0x04 || response[1] != 0x0e || response[2] != 0x05
                || response[3] != 0x01 || response[4] != 0xe0 || response[5] != 0xfc
                || response[6] != requestCommand + 1 || response[7] != 0x00)
            {
                return false;
            }
            if (response[6] == 0x01)
            {
                stage = BekenLinkStage.BootRom;
                return true;
            }
            if (response[6] == 0x03)
            {
                stage = BekenLinkStage.Bl2;
                return true;
            }
            return false;
        }

        void observeLinkStage(byte requestCommand, byte[] response)
        {
            if (usesDirectFlashAccessProfile() == false)
            {
                return;
            }
            if (tryDecodeLinkStage(requestCommand, response, out BekenLinkStage stage) == false)
            {
                return;
            }
            if (observedLinkStage == stage)
            {
                return;
            }
            observedLinkStage = stage;
            string stageName = stage == BekenLinkStage.BootRom ? "BootROM" : "BL2";
            addLog("Link-stage probe command 0x" + requestCommand.ToString("X2")
                + " returned 0x" + response[6].ToString("X2") + "." + Environment.NewLine);
            addSuccess("Detected link stage: " + stageName + Environment.NewLine);
            if (stage == BekenLinkStage.Bl2)
            {
                addWarning("The target is currently answering as BL2; this flasher operation requires the BootROM command endpoint."
                    + Environment.NewLine);
            }
        }

        void probeBl2LinkStage()
        {
            if (usesDirectFlashAccessProfile() == false || observedLinkStage != BekenLinkStage.Unknown)
            {
                return;
            }
            byte[] response = Start_Cmd(BuildCmd_LinkCheck(0x02), CalcRxLength_LinkCheck(), 0.0015f);
            observeLinkStage(0x02, response);
        }

        void logReadRegProbeResult()
        {
            if (BKChipIdentity.ShouldAttemptChipIdRead(chipType) == false)
            {
                return;
            }
            if (chipIdentity != null && chipIdentity.HasChipId)
            {
                addSuccess("Chip ID read via CMD_ReadReg succeeded."
                    + Environment.NewLine);
            }
            else
            {
                addWarning("CMD_ReadReg chip-ID probe did not return a usable ID; continuing with selected "
                    + chipType + " mode." + Environment.NewLine);
            }
        }

        bool getBus()
        {
            int maxTries = 100;
            int loops = 100;
            bool bOk = false;
            observedLinkStage = BekenLinkStage.Unknown;
            addLog("Getting bus... (now, please do reboot by CEN or by power off/on)" + Environment.NewLine);
            serial.BaudRate = 115200;
            for (int tr = 0; tr < maxTries && !bOk; tr++)
            {
                serial.DtrEnable = true;
                serial.RtsEnable = true;
                Thread.Sleep(50);
                serial.DtrEnable = false;
                serial.RtsEnable = false;
                if(tr % 5 == 0)
                {
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
                    if ((l % 8) == 7)
                    {
                        probeBl2LinkStage();
                    }
                }
                addWarning("Getting bus failed, will try again - " + tr + "/" + maxTries + "!" + Environment.NewLine);
                if(tr % 10 == 9)
                {
                    addWarning("Reminder: you should do a device reboot now (do power off/on of the device, but don't disconnect UART or do a CEN short to ground for 0.25sec)" + Environment.NewLine);
                }
            }
            if (usesDirectFlashAccessProfile() && observedLinkStage == BekenLinkStage.Unknown)
            {
                addWarning("No valid BootROM or BL2 link-stage response was observed." + Environment.NewLine);
            }
            return false;
        }
        bool runModificationOperation(Func<bool> operation)
        {
            LastOperationSucceeded = false;
            try
            {
                LastOperationSucceeded = operation();
            }
            catch (Exception ex)
            {
                addError("Exception caught: " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
                try
                {
                    if (restoreFlashProtection() == false)
                    {
                        LastOperationSucceeded = false;
                    }
                }
                finally
                {
                    bool closeDeferredPort;
                    lock (modificationSessionLock)
                    {
                        flashProtectionSessionActive = false;
                        closeDeferredPort = closePortDeferredForProtectionRestore;
                        closePortDeferredForProtectionRestore = false;
                    }
                    if (closeDeferredPort)
                    {
                        closePort();
                    }
                }
            }
            return LastOperationSucceeded;
        }

        public override void doWrite(int startSector, byte [] data)
        {
            runModificationOperation(() => doWriteInternal(startSector, data));
        }
        public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
            runModificationOperation(() => doTestReadWriteInternal(startSector, sectors));
        }
        
        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            runModificationOperation(() => doReadAndWriteInternal(startSector, sectors, sourceFileName, rwMode));
        }
        
        public override bool doErase(int startSector, int sectors, bool bAll)
        {
            return runModificationOperation(() =>
            {
                logger.setProgress(0, sectors);
                addLog("Erase started with ofs " + formatHex(startSector) + " and requested len in sectors " + sectors + Environment.NewLine);
                if (doGenericSetup(true) == false)
                {
                    return false;
                }
                if (chipType == BKType.BK7252)
                {
                    detectBK7252UFlashSize();
                }
                if (bAll)
                {
                    if (startSector < 0 || startSector >= FLASH_SIZE)
                    {
                        addError("Erase-all start address is outside flash." + Environment.NewLine);
                        return false;
                    }
                    sectors = (FLASH_SIZE - startSector) / SECTOR_SIZE;
                    logger.setProgress(0, sectors);
                    addLog("Erase-all using flash size " + formatFlashSize(FLASH_SIZE)
                        + ", start " + formatHex(startSector)
                        + ", sectors " + sectors
                        + ", end " + formatHex(startSector + sectors * SECTOR_SIZE) + Environment.NewLine);
                }
                return doEraseInternal(startSector, sectors);
            });
        }
        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            ms = null;
            try
            {
                doReadInternal(startSector, sectors, fullRead);
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
            int maxAttempts = 10;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                addSuccess("Going to set baud rate setting (" + baudrate + ")!" + Environment.NewLine);
                logger.setState("Setting baud rate...", Color.Transparent);
                if (setBaudRateIfNeeded())
                {
                    Thread.Sleep(50);
                    return true;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                addError("Failed to set baud rate!" + Environment.NewLine);
                logger.setState("Failed to set baud rate!", Color.Red);
                if (attempt < maxAttempts)
                {
                    Thread.Sleep(50);
                }
            }
            return false;
        }
        
        bool doGenericSetup(bool prepareForModification = true, bool loadExternalFlashInfo = true)
        {
            resetLegacyFlashSize();
            deviceMID = 0;
            flashInfo = null;
            lock (modificationSessionLock)
            {
                flashProtectionSessionActive = false;
                closePortDeferredForProtectionRestore = false;
            }
            originalFlashProtectionBits = null;
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
            lastEncryptionKey = "";
            chipIdentity = BKChipIdentity.Detect(chipType, ReadFlashReg);
            if (chipIdentity.HasChipId == false)
            {
                if (BKChipIdentity.ShouldAttemptChipIdRead(chipType))
                {
                    addWarning("Failed to get chip ID!" + Environment.NewLine);
                    string chipIdFailureWarning = BKChipIdentity.BuildChipIdReadFailureWarning(chipType);
                    if (string.IsNullOrEmpty(chipIdFailureWarning) == false)
                    {
                        addErrorLine(chipIdFailureWarning);
                    }
                }
            }
            else
            {
                string primaryIdentitySuffix = chipIdentity.FriendlyNameFromSecondaryId ? "" : $" ({chipIdentity.FriendlyName})";
                addLog($"Chip ID: 0x{chipIdentity.NormalizedId}{primaryIdentitySuffix}" + Environment.NewLine);
                if (chipIdentity.HasSecondaryId)
                {
                    string secondaryIdentitySuffix = chipIdentity.FriendlyNameFromSecondaryId ? $" ({chipIdentity.FriendlyName})" : "";
                    addLog($"Secondary chip ID: 0x{chipIdentity.SecondaryId.ToUpperInvariant()}{secondaryIdentitySuffix}" + Environment.NewLine);
                }
                string chipMismatchWarning = chipIdentity.BuildMismatchWarning(chipType);
                if (string.IsNullOrEmpty(chipMismatchWarning) == false)
                {
                    addErrorLine(chipMismatchWarning);
                    if (bSkipKeyCheck == false)
                    {
                        return false;
                    }
                }
            }
            logReadRegProbeResult();
            if (usesDirectFlashAccessProfile())
            {
                if ((loadExternalFlashInfo || prepareForModification) && loadFlashInfo() == false)
                {
                    return false;
                }
                if (prepareForModification && prepareFlashForModification() == false)
                {
                    return false;
                }
                if (chipType != BKType.BK7236 && chipType != BKType.BK7238 && chipType != BKType.BK7239N
                    && chipType != BKType.BK7252N && chipType != BKType.BK7258)
                {
                    addLog("Going to read encryption key..." + Environment.NewLine);
                    string key = readEncryptionKey(out var coeffs);
                    addLog("Encryption key read done!" + Environment.NewLine);
                    addLog("Encryption key: " + key + Environment.NewLine);
                    bool enforceKeyCheck = chipType != BKType.BK7231M;
                    string otherMode;
                    string expectedKey;
                    if (chipType == BKType.BK7231N)
                    {
                        otherMode = "BK7231M";
                        expectedKey = TUYA_ENCRYPTION_KEY;
                    }
                    else if(chipType == BKType.BK7231M)
                    {
                        otherMode = "BK7231N";
                        expectedKey = EMPTY_ENCRYPTION_KEY;
                    }
                    else
                    {
                        otherMode = "";
                        expectedKey = EMPTY_ENCRYPTION_KEY;
                    }
                    if(key != expectedKey)
                    {
                        if(key != EMPTY_ENCRYPTION_KEY && coeffs.Distinct().Count() == 1)
                        {
                            string chipMismatchWarning = chipIdentity?.BuildMismatchWarning(chipType);
                            if (string.IsNullOrEmpty(chipMismatchWarning))
                            {
                                addErrorLine($"WARNING! Selected chip is a {chipType}, but according to encryption key this may be {chipIdentity?.DescribeDetectedChip() ?? "an unknown chip"}!");
                            }
                            if(enforceKeyCheck && !bSkipKeyCheck) return false;
                        }
                        addError("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^" + Environment.NewLine);
                        addError("WARNING! Non-standard encryption key!" + Environment.NewLine);
                        addError("If it's all zero, it may also mean that read is disabled." + Environment.NewLine);
                        addError("Please report to forum https://www.elektroda.com/rtvforum/forum51.html " + Environment.NewLine);
                        if(chipType == BKType.BK7231N || chipType == BKType.BK7231M)
                        {
                            addError($"Or just try using {otherMode} mode " + Environment.NewLine);
                        }
                        addError("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^" + Environment.NewLine);
                        if(enforceKeyCheck && bSkipKeyCheck == false)
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
            addLog("Going to do erase, start " + formatHex(startSector) +", sec count " + sectors +"!" + Environment.NewLine);
            if(!eraseRange(startSector, sectors))
            {
                return false;
            }
            addLog(Environment.NewLine);
            addLog("All selected sectors erased!" + Environment.NewLine);
            logger.setState("Erase complete.", Color.Green);
            return true;
        }
        int deviceMID;
        BKFlash flashInfo;

        public BK7231Flasher(CancellationToken ct) : base(ct)
        {
        }

        bool loadFlashInfo()
        {
            if (usesDirectFlashAccessProfile() == false)
            {
                return true;
            }
            if (flashInfo != null && deviceMID != 0)
            {
                return true;
            }
            addLog("Reading device flash MID..." + Environment.NewLine);
            deviceMID = GetFlashMID();
            if (deviceMID == 0)
            {
                addError("Failed to read device MID!" + Environment.NewLine);
                return false;
            }
            addSuccess("Flash MID loaded: " + deviceMID.ToString("X6") + Environment.NewLine);
            addLog("Searching for the flash definition..." + Environment.NewLine);
            flashInfo = BKFlashList.Singleton.findFlashForMID(deviceMID);
            if(flashInfo == null)
            {
                addError("Failed to find flash definition for device MID " + deviceMID.ToString("X6") + "." + Environment.NewLine);
                return false;
            }
            addSuccess("Flash definition found for " + deviceMID.ToString("X6") + "." + Environment.NewLine);
            addLog("Flash information: " + flashInfo.ToString() + Environment.NewLine);
            setFlashSize(flashInfo.szMem);
            addLog("Flash size is " + formatFlashSize(FLASH_SIZE) + "." + Environment.NewLine);
            return true;
        }

        bool prepareFlashForModification()
        {
            if (usesDirectFlashAccessProfile() == false)
            {
                return true;
            }
            if (loadFlashInfo() == false)
            {
                return false;
            }
            bool captureOriginalProtection;
            lock (modificationSessionLock)
            {
                captureOriginalProtection = flashProtectionSessionActive == false;
            }
            if (captureOriginalProtection)
            {
                if (tryReadFlashStatus(out int originalStatus) == false)
                {
                    addError("Unable to capture flash protection before modification." + Environment.NewLine);
                    return false;
                }
                lock (modificationSessionLock)
                {
                    originalFlashProtectionBits = originalStatus & flashInfo.cwMsk;
                    flashProtectionSessionActive = true;
                }
                addLog("Original flash protection bits: "
                    + formatHex(originalFlashProtectionBits.Value) + Environment.NewLine);
            }
            addLog("Clearing flash protection before modification..." + Environment.NewLine);
            int unprotectedBits = BKFlashList.BFD(flashInfo.cwUnp, flashInfo.sb, flashInfo.lb);
            if (setProtectionBits(unprotectedBits) == false)
            {
                addError("Unable to clear flash protection; erase/write has been aborted." + Environment.NewLine);
                return false;
            }
            addSuccess("Flash protection cleared." + Environment.NewLine);
            return true;
        }

        bool restoreFlashProtection()
        {
            if (flashProtectionSessionActive == false)
            {
                return true;
            }
            try
            {
                if (flashInfo == null || originalFlashProtectionBits.HasValue == false
                    || serial == null || serial.IsOpen == false)
                {
                    addError("Could not restore flash protection because the active flash session is unavailable." + Environment.NewLine);
                    return false;
                }
                int protectionBits = originalFlashProtectionBits.Value;
                addLog("Restoring original flash protection bits " + formatHex(protectionBits) + "..." + Environment.NewLine);
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    if (setProtectionBits(protectionBits))
                    {
                        originalFlashProtectionBits = null;
                        addSuccess("Original flash protection restored." + Environment.NewLine);
                        return true;
                    }
                    if (attempt < 2)
                    {
                        addWarning("Flash protection restore failed; retrying." + Environment.NewLine);
                        Thread.Sleep(20);
                    }
                }
            }
            catch (Exception ex)
            {
                addError("Flash protection restore failed: " + ex.Message + Environment.NewLine);
            }
            logger.setState("Flash protection restore failed.", Color.Red);
            addError("Flash modification completed, but protection could not be restored." + Environment.NewLine);
            return false;
        }

        bool tryReadFlashStatus(out int status)
        {
            status = 0;
            for (int i = 0; i < flashInfo.szSR; i++)
            {
                byte[] srBytes = ReadFlashSR(flashInfo.cwdRd[i]);
                if (srBytes == null || srBytes.Length < 2)
                {
                    return false;
                }
                status |= srBytes[1] << (8 * i);
            }
            return true;
        }

        bool setProtectionBits(int protectionBits)
        {
            int targetBits = protectionBits & flashInfo.cwMsk;
            int maxTries = 10;
            int tryNum = 0;
            while (true)
            {
                tryNum++;
                if (tryReadFlashStatus(out int status) == false)
                {
                    if (tryNum >= maxTries)
                    {
                        addError("Flash protection update failed because the status register could not be read after "
                            + maxTries + " retries." + Environment.NewLine);
                        return false;
                    }
                    addWarning("Flash status read failed; retrying protection update." + Environment.NewLine);
                    Thread.Sleep(10);
                    continue;
                }
                addLog("Flash status: " + formatHex(status) + ", target protection bits: "
                    + formatHex(targetBits) + Environment.NewLine);
                if ((status & flashInfo.cwMsk) == targetBits)
                {
                    return true;
                }
                if(tryNum >= maxTries)
                {
                    addError("Flash protection update failed after " + maxTries + " retries." + Environment.NewLine);
                    return false;
                }
                int updatedStatus = (status & ~flashInfo.cwMsk) | targetBits;
                if (WriteFlashSR(flashInfo.szSR, flashInfo.cwdWr[0], updatedStatus & 0xffff) == false)
                {
                    addWarning("Flash protection write failed; retrying." + Environment.NewLine);
                }
                Thread.Sleep(10);
            }
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
            if (cfg != null && (sectors <= 0 || ofs < 0))
            {
                if (rwMode == WriteMode.OnlyOBKConfig)
                {
                    logger.setState("OBK config unsupported.", Color.Red);
                    addError("OBK config location is not defined for " + chipType + "." + Environment.NewLine);
                    return false;
                }
                addWarning("Automatic OBK config injection is not supported on " + chipType
                    + "; continuing without it." + Environment.NewLine);
                cfg = null;
            }
            logger.setState("Writing...", Color.Transparent);
            if (data != null)
            {
                data = MiscUtils.padArray(data, SECTOR_SIZE);
                sectors = data.Length / SECTOR_SIZE;
                if(chipType == BKType.BK7252 && startSector + data.Length > FLASH_SIZE)
                {
                    addError("BK7252U: write range " + formatHex(startSector) + ".."
                        + formatHex(startSector + data.Length)
                        + " exceeds detected flash size " + formatFlashSize(FLASH_SIZE)
                        + ". Aborting before erase." + Environment.NewLine);
                    return false;
                }
            }
            logger.setProgress(0, sectors);
            if (data != null && doEraseInternal(startSector, sectors) == false)
            {
                return false;
            }
            if (cfg != null && doEraseInternal(ofs, 1) == false)
            {
                return false;
            }
            logger.setState("Writing...", Color.Transparent);
            if (data != null)
            {
                for (int sec = 0; sec < sectors; sec++)
                {
                    int secAddr = startSector + SECTOR_SIZE * sec;
                    bool bOk = usesDirectFlashAccessProfile()
                        ? writePageWithCRCVerification(secAddr, data, SECTOR_SIZE * sec)
                        : writeSector4K(secAddr, data, SECTOR_SIZE * sec);
                    addLog(formatHex(secAddr) + "...");
                    if (bOk == false)
                    {
                        logger.setState("Writing error!", Color.Red);
                        addError(" Writing sector " + formatHex(secAddr) + " failed!" + Environment.NewLine);
                        return false;
                    }
                    logger.setProgress(sec + 1, sectors);
                }
                if (usesDirectFlashAccessProfile())
                {
                    addSuccess("All written pages passed independent CRC verification." + Environment.NewLine);
                }
                else if (checkCRC(startSector, sectors, data) == false)
                {
                    logger.setState("Bad CRC!", Color.Red);
                    return false;
                }
            }
            addLog(Environment.NewLine);
            if (cfg != null)
            {
                addLog("Now will also write OBK config..." + Environment.NewLine);
                cfg.saveConfig(chipType);
                addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                addLog("Writing config sector " + formatHex(ofs) + "...");
                byte[] wd = MiscUtils.padArray(cfg.getData(), SECTOR_SIZE);
                bool bOk = usesDirectFlashAccessProfile()
                    ? writePageWithCRCVerification(ofs, wd, 0)
                    : writeSector4K(ofs, wd, 0);
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
            logger.setState("Write success!" + Environment.NewLine, Color.Green);
            return true;
        }
        bool doTestReadWriteInternal(int startSector = 0x11000, int sectors = 10)
        {
            addLog(Environment.NewLine + "Starting read-write test!" + Environment.NewLine);
            if (doGenericSetup() == false)
            {
                return false;
            }
            if (chipType == BKType.BK7252)
            {
                detectBK7252UFlashSize();
            }
            if (doEraseInternal(startSector, sectors) == false)
            {
                return false;
            }
            MemoryStream toCheck = readChunk(startSector, sectors, true);
            if (toCheck == null)
            {
                addError("Read failed?" + Environment.NewLine);
                return false;
            }
            if (MiscUtils.isFullOf(toCheck.ToArray(), 0xff)==false)
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
            if (chipType == BKType.BK7252)
            {
                detectBK7252UFlashSize();
            }
            if (usesDirectFlashAccessProfile() && prepareFlashForModification() == false)
            {
                return false;
            }
            if (writeChunk(startSector, data,WriteMode.OnlyWrite) == false)
            {
                return false;
            }
            MemoryStream toCheck2 = readChunk(startSector, sectors);
            if (toCheck2 == null)
            {
                addError("Read-back verification failed." + Environment.NewLine);
                return false;
            }
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
            data = MiscUtils.padArray(data, SECTOR_SIZE);
            int sectors = data.Length / SECTOR_SIZE;
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting write test!" + Environment.NewLine);
            if (doGenericSetup(true) == false)
            {
                return false;
            }
            if (chipType == BKType.BK7252)
            {
                detectBK7252UFlashSize();
            }
            if(!eraseRange(startSector, sectors))
            {
                return false;
            }
            addLog(Environment.NewLine);
            addLog("All selected sectors erased!" + Environment.NewLine);
            for (int sec = 0; sec < sectors; sec++)
            {
                int secAddr = startSector + SECTOR_SIZE * sec;
                bool bOk = usesDirectFlashAccessProfile()
                    ? writePageWithCRCVerification(secAddr, data, SECTOR_SIZE * sec)
                    : writeSector4K(secAddr, data, SECTOR_SIZE * sec);
                addLog(formatHex(secAddr) + "...");
                if (bOk == false)
                {
                    logger.setState("Write sector failed!", Color.Red);
                    addError(" Writing sector " + formatHex(secAddr) + " failed!" + Environment.NewLine);
                    return false;
                }
                logger.setProgress(sec + 1, sectors);
            }
            if (usesDirectFlashAccessProfile() == false && checkCRC(startSector, sectors, data) == false)
            {
                return false;
            }
            addSuccess("Write success!");
            return true;
        }
        public byte[] ReadRomTarget(RomReadTarget target)
        {
            try
            {
                return ReadRomTargetInternal(target);
            }
            catch (Exception ex)
            {
                string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
                addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
                logger.setState(targetKindName + " read failed.", Color.Red);
                return null;
            }
        }

        byte[] ReadRomTargetInternal(RomReadTarget target)
        {
            if (target == null)
            {
                addError("No ROM reader target selected." + Environment.NewLine);
                return null;
            }
            if (doGenericSetup(false, false) == false)
            {
                return null;
            }
            int offset = target.Address ?? 0;
            int length = target.Length ?? 0;
            switch (target.Kind)
            {
                case RomReadKind.Rom:
                    return ReadBekenRom(offset, length);
                case RomReadKind.Efuse:
                    return ReadBekenEfuse(offset, length);
                case RomReadKind.Otp:
                    return ReadBekenOtpData(offset, length);
                default:
                    addError("Selected read target is not implemented." + Environment.NewLine);
                    return null;
            }
        }

        byte[] ReadBekenRom(int offset, int length)
        {
            if ((offset % 4) != 0 || (length % 4) != 0)
            {
                throw new InvalidOperationException(chipType + " ROM reads must be 4-byte aligned.");
            }
            if (offset < 0 || length <= 0 || offset > int.MaxValue - length)
            {
                throw new InvalidOperationException(chipType + " ROM read range is out of bounds.");
            }
            logger.setState("Reading ROM...", Color.Transparent);
            logger.setProgress(0, length);
            addLog("Reading " + chipType + " ROM from " + formatHex(offset) + ", length " + formatHex(length) + Environment.NewLine);
            byte[] result = new byte[length];
            for (int ofs = 0; ofs < length; ofs += 4)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.setState("ROM read cancelled.", Color.Yellow);
                    return null;
                }
                byte[] word = ReadFlashReg(offset + ofs);
                if (word == null || word.Length < 4)
                {
                    throw new IOException(chipType + " ROM read failed at " + formatHex(offset + ofs));
                }
                Buffer.BlockCopy(word, 0, result, ofs, 4);
                logger.setProgress(ofs + 4, length);
            }
            if ((chipType == BKType.BK7236 || chipType == BKType.BK7239N || chipType == BKType.BK7258)
                && (result.All(value => value == 0) || result.All(value => value == 0xFF)))
            {
                throw new IOException(chipType + " ROM read from " + formatHex(offset) + " returned only blank bytes.");
            }
            logger.setState("ROM read success!", Color.Green);
            return result;
        }

        byte[] ReadBekenEfuse(int offset, int length)
        {
            if (chipType == BKType.BK7239N)
            {
                return ReadBekenOtpData(offset, length, true);
            }
            int efuseSize = chipType == BKType.BK7238 || chipType == BKType.BK7252N || chipType == BKType.BK7258
                ? FOUR_BYTE_BEKEN_EFUSE_SIZE : BEKEN_EFUSE_SIZE;
            if (offset < 0 || length <= 0 || offset + length > efuseSize)
            {
                throw new InvalidOperationException(chipType + " eFuse read range is out of bounds.");
            }
            logger.setState("Reading eFuse...", Color.Transparent);
            logger.setProgress(0, length);
            addLog("Reading " + chipType + " eFuse from " + formatHex(offset) + ", length " + formatHex(length) + Environment.NewLine);
            if (chipType == BKType.BK7258)
            {
                return ReadBK7258Efuse(offset, length);
            }
            byte[] result = new byte[length];
            int efuseCtrl = SCTRL_EFUSE_CTRL;
            int efuseOptr = SCTRL_EFUSE_OPTR;
            for (int ofs = 0; ofs < length; ofs++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.setState("eFuse read cancelled.", Color.Yellow);
                    return null;
                }
                result[ofs] = ReadBekenEfuseByte(efuseCtrl, efuseOptr, offset + ofs);
                logger.setProgress(ofs + 1, length);
            }
            logger.setState("eFuse read success!", Color.Green);
            return result;
        }

        byte[] ReadBK7258Efuse(int offset, int length)
        {
            int originalClock = ReadFlashRegRequiredInt(BK7258_SYS_DEVICE_CLK_ENABLE, "BK7258 SYS clock enable");
            bool bClockChanged = (originalClock & BK7258_EFUSE_CLOCK_ENABLE) == 0;
            if (bClockChanged && WriteFlashReg(BK7258_SYS_DEVICE_CLK_ENABLE, originalClock | BK7258_EFUSE_CLOCK_ENABLE) == false)
            {
                throw new IOException("BK7258 eFuse clock enable failed.");
            }

            try
            {
                byte[] result = new byte[length];
                for (int ofs = 0; ofs < length; ofs++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.setState("eFuse read cancelled.", Color.Yellow);
                        return null;
                    }
                    result[ofs] = ReadBK7258EfuseByte(offset + ofs);
                    logger.setProgress(ofs + 1, length);
                }
                logger.setState("eFuse read success!", Color.Green);
                return result;
            }
            finally
            {
                WriteFlashReg(BK7258_EFUSE_CTRL, 0);
                if (bClockChanged)
                {
                    WriteFlashReg(BK7258_SYS_DEVICE_CLK_ENABLE, originalClock);
                }
            }
        }

        byte ReadBK7258EfuseByte(int addr)
        {
            // Read direction only: DIR, write data and VDD2.5 remain clear.
            int command = ((addr & 0x1F) << 8) | 1;
            if (WriteFlashReg(BK7258_EFUSE_CTRL, command) == false)
            {
                throw new IOException("BK7258 eFuse control write failed at byte " + addr);
            }

            Stopwatch waitTimer = Stopwatch.StartNew();
            int control;
            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("eFuse read cancelled by user.");
                }
                if (waitTimer.ElapsedMilliseconds > 1000)
                {
                    throw new TimeoutException("BK7258 eFuse read timed out at byte " + addr);
                }
                control = ReadFlashRegRequiredInt(BK7258_EFUSE_CTRL, "BK7258 eFuse control");
            } while ((control & 1) != 0);

            int operationResult = ReadFlashRegRequiredInt(BK7258_EFUSE_OPTR, "BK7258 eFuse result");
            if ((operationResult & 0x100) == 0)
            {
                throw new IOException("BK7258 eFuse byte " + addr + " was not marked valid: " + formatHex(operationResult));
            }
            return (byte)(operationResult & 0xFF);
        }

        byte[] ReadBekenOtpData(int offset, int length, bool readBk7239NEfuseField = false)
        {
            int expectedLength = BEKEN_OTP1_APB_DATA_SIZE + BEKEN_OTP2_AHB_DATA_SIZE;
            if (readBk7239NEfuseField)
            {
                if (chipType != BKType.BK7239N || offset != 0 || length != FOUR_BYTE_BEKEN_EFUSE_SIZE)
                {
                    throw new InvalidOperationException(chipType + " eFuse read range is out of bounds.");
                }
            }
            else if ((chipType != BKType.BK7239N && chipType != BKType.BK7258) || offset != 0 || length != expectedLength)
            {
                throw new InvalidOperationException(chipType + " OTP read must include the complete OTP1 and OTP2 windows.");
            }

            string targetName = readBk7239NEfuseField ? "eFuse" : "OTP";
            logger.setState("Reading " + targetName + "...", Color.Transparent);
            logger.setProgress(0, length);
            if (readBk7239NEfuseField)
            {
                addLog("Reading " + chipType + " OTP1 eFuse field from " + formatHex(BK7239N_EFUSE_DATA_BASE + offset)
                    + ", length " + formatHex(length) + Environment.NewLine);
            }
            else
            {
                addLog("Reading " + chipType + " OTP1 APB and OTP2 AHB windows, combined length " + formatHex(length) + Environment.NewLine);
            }

            int originalClock = 0;
            bool bClockChanged = false;
            int originalPower = 0;
            bool bPowerChanged = false;
            if (chipType == BKType.BK7258)
            {
                originalClock = ReadFlashRegRequiredInt(BK7258_SYS_DEVICE_CLK_ENABLE, "BK7258 SYS clock enable");
                originalPower = ReadFlashRegRequiredInt(BK7258_SYS_POWER_SLEEP_WAKEUP, "BK7258 SYS power control");
                bClockChanged = (originalClock & BK7258_OTP_CLOCK_ENABLE) == 0;
                bPowerChanged = (originalPower & BK7258_OTP_POWER_DOWN) != 0;
                if (bClockChanged && WriteFlashReg(BK7258_SYS_DEVICE_CLK_ENABLE, originalClock | BK7258_OTP_CLOCK_ENABLE) == false)
                {
                    throw new IOException("BK7258 OTP clock enable failed.");
                }
                if (bPowerChanged && WriteFlashReg(BK7258_SYS_POWER_SLEEP_WAKEUP, originalPower & ~BK7258_OTP_POWER_DOWN) == false)
                {
                    if (bClockChanged)
                    {
                        WriteFlashReg(BK7258_SYS_DEVICE_CLK_ENABLE, originalClock);
                    }
                    throw new IOException("BK7258 OTP power enable failed.");
                }
            }

            try
            {
                Thread.Sleep(2);
                byte[] result = new byte[length];
                for (int ofs = 0; ofs < length; ofs += 4)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.setState(targetName + " read cancelled.", Color.Yellow);
                        return null;
                    }
                    int address = readBk7239NEfuseField
                        ? BK7239N_EFUSE_DATA_BASE + offset + ofs
                        : ofs < BEKEN_OTP1_APB_DATA_SIZE
                            ? BEKEN_OTP1_APB_DATA_BASE + ofs
                            : BEKEN_OTP2_AHB_DATA_BASE + ofs - BEKEN_OTP1_APB_DATA_SIZE;
                    byte[] word = ReadFlashReg(address);
                    if (word == null || word.Length < 4)
                    {
                        throw new IOException(chipType + " " + targetName + " read failed at " + formatHex(address));
                    }
                    Buffer.BlockCopy(word, 0, result, ofs, 4);
                    logger.setProgress(ofs + 4, length);
                }
                logger.setState(targetName + " read success!", Color.Green);
                return result;
            }
            finally
            {
                if (bPowerChanged)
                {
                    WriteFlashReg(BK7258_SYS_POWER_SLEEP_WAKEUP, originalPower);
                }
                if (bClockChanged)
                {
                    WriteFlashReg(BK7258_SYS_DEVICE_CLK_ENABLE, originalClock);
                }
            }
        }

        int ReadFlashRegRequiredInt(int address, string registerName)
        {
            byte[] value = ReadFlashReg(address);
            if (value == null || value.Length < 4)
            {
                throw new IOException(registerName + " read failed at " + formatHex(address));
            }
            return (value[3] << 24) | (value[2] << 16) | (value[1] << 8) | value[0];
        }

        byte ReadBekenEfuseByte(int efuseCtrl, int efuseOptr, int addr)
        {
            int reg = ReadFlashRegInt(efuseCtrl);
            reg = (reg & ~0x1F02) | (addr << 8) | 1;
            if (WriteFlashReg(efuseCtrl, reg) == false)
            {
                throw new IOException(chipType + " eFuse control write failed at " + addr);
            }
            Stopwatch waitTimer = Stopwatch.StartNew();
            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("eFuse read cancelled by user.");
                }
                if (waitTimer.ElapsedMilliseconds > 1000)
                {
                    throw new TimeoutException(chipType + " eFuse read timed out at " + addr);
                }
                reg = ReadFlashRegInt(efuseCtrl);
            } while ((reg & 1) != 0);
            reg = ReadFlashRegInt(efuseOptr);
            if ((reg & 0x100) == 0)
            {
                throw new IOException(chipType + " eFuse data " + addr + " invalid: " + formatHex(reg));
            }
            return (byte)(reg & 0xff);
        }

        string readEncryptionKey(out uint[] coeffs)
        {
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
            coeffs = new uint[4];
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
        static void setFlashSize(int size)
        {
            FLASH_SIZE = size;
            TOTAL_SECTORS = FLASH_SIZE / SECTOR_SIZE;
        }
        void resetLegacyFlashSize()
        {
            if (chipType == BKType.BK7231T || chipType == BKType.BK7231U || chipType == BKType.BK7252)
            {
                setFlashSize(DEFAULT_FLASH_SIZE);
                bk7252ReadAddressBase = DEFAULT_FLASH_SIZE;
            }
        }
        int translateReadAddressForChip(int logicalAddr)
        {
            // Original Easy Flasher wrap-around hack:
            // BK7231T/U do not allow direct bootloader reads, but the protected window can be read
            // by wrapping logical addresses into the mirrored upper flash range.
            if(chipType == BKType.BK7231T || chipType == BKType.BK7231U)
            {
                return logicalAddr + FLASH_SIZE;
            }
            // BK7252U uses the same style of mapped read, but the usable base depends on detected flash size.
            if(chipType == BKType.BK7252)
            {
                return logicalAddr + bk7252ReadAddressBase;
            }
            return logicalAddr;
        }
        string formatFlashSize(int size)
        {
            int mb = size / (1024 * 1024);
            if(size == mb * 1024 * 1024)
            {
                return formatHex(size) + " (" + mb + "MB)";
            }
            return formatHex(size);
        }
        static bool IsFilledWith(byte[] data, byte value)
        {
            return data != null && data.All(b => b == value);
        }
        byte[] readSectorPayload(int wireAddr, int retries = 2, float timeout = 15)
        {
            for(int attempt = 0; attempt <= retries; attempt++)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                byte[] res = readSector(wireAddr, timeout);
                if(res == null)
                {
                    addWarning("Read " + formatHex(wireAddr) + " returned no data"
                        + (attempt < retries ? ", retrying." : ".") + Environment.NewLine);
                    continue;
                }
                if(res.Length < READ_RESPONSE_HEADER_SIZE + SECTOR_SIZE)
                {
                    addWarning("Read response for " + formatHex(wireAddr) + " was too short (" + res.Length + " bytes)"
                        + (attempt < retries ? ", retrying." : ".") + Environment.NewLine);
                    continue;
                }
                byte[] payload = new byte[SECTOR_SIZE];
                Array.Copy(res, READ_RESPONSE_HEADER_SIZE, payload, 0, SECTOR_SIZE);
                return payload;
            }
            return null;
        }
        byte[] readBK7252UProbePage(int logicalAddr, int readAddressBase, string readMode)
        {
            int wireAddr = logicalAddr + readAddressBase;
            addLog("BK7252U: probe " + readMode + " logical " + formatHex(logicalAddr) + " -> wire " + formatHex(wireAddr) + "... ");
            byte[] payload = readSectorPayload(wireAddr, 0, 2);
            if(payload == null)
            {
                addLog("FAIL" + Environment.NewLine);
                return null;
            }
            addLog("OK, first 16 bytes " + string.Join(" ", payload.Take(16).Select(b => b.ToString("X2"))) + Environment.NewLine);
            return payload;
        }
        int detectBK7252UFlashSizeWithReadBase(int readAddressBase, string readMode)
        {
            byte[] basePage = readBK7252UProbePage(0x11000, readAddressBase, readMode);
            if(basePage == null)
            {
                addWarning("BK7252U: " + readMode + " flash size detection failed (could not read base page)." + Environment.NewLine);
                return 0;
            }
            if(IsFilledWith(basePage, 0xFF) || IsFilledWith(basePage, 0x00))
            {
                addWarning("BK7252U: " + readMode + " flash size detection is ambiguous because the base page is blank." + Environment.NewLine);
                return 0;
            }

            int[] candidateSizes = new int[] { DEFAULT_FLASH_SIZE, BK7252_MAX_FLASH_SIZE };
            foreach(int candidateSize in candidateSizes)
            {
                int logicalAddr = 0x11000 + candidateSize;
                addLog("BK7252U: checking " + readMode + " wraparound at " + formatHex(logicalAddr) + Environment.NewLine);
                byte[] probePage = readBK7252UProbePage(logicalAddr, readAddressBase, readMode);
                if(probePage != null && basePage.SequenceEqual(probePage))
                {
                    return candidateSize;
                }
            }

            return 0;
        }
        int detectBK7252UFlashSizeFromBootloaderMirror()
        {
            byte[] lowerMirror = readBK7252UProbePage(0, DEFAULT_FLASH_SIZE, "bootloader mirror 2MB");
            byte[] upperMirror = readBK7252UProbePage(0, BK7252_MAX_FLASH_SIZE, "bootloader mirror 4MB");
            if(upperMirror == null || IsFilledWith(upperMirror, 0xFF) || IsFilledWith(upperMirror, 0x00))
            {
                addWarning("BK7252U: bootloader mirror detection is ambiguous because the upper mirror is blank or unreadable." + Environment.NewLine);
                return 0;
            }
            if(lowerMirror != null && lowerMirror.SequenceEqual(upperMirror))
            {
                return DEFAULT_FLASH_SIZE;
            }
            return BK7252_MAX_FLASH_SIZE;
        }
        void detectBK7252UFlashSize(int fallbackSize = 0)
        {
            if(chipType != BKType.BK7252)
            {
                return;
            }
            setFlashSize(DEFAULT_FLASH_SIZE);
            bk7252ReadAddressBase = DEFAULT_FLASH_SIZE;
            addLog("BK7252U: detecting flash size by wrap-around" + Environment.NewLine);

            int detectedSize = detectBK7252UFlashSizeWithReadBase(0, "raw");
            if(detectedSize == 0)
            {
                addWarning("BK7252U: raw wrap detection failed; trying shifted read window." + Environment.NewLine);
                detectedSize = detectBK7252UFlashSizeWithReadBase(DEFAULT_FLASH_SIZE, "shifted");
            }
            if(detectedSize == 0)
            {
                addWarning("BK7252U: shifted wrap detection failed; trying bootloader mirror probe." + Environment.NewLine);
                detectedSize = detectBK7252UFlashSizeFromBootloaderMirror();
            }
            if(detectedSize != 0)
            {
                setFlashSize(detectedSize);
                bk7252ReadAddressBase = detectedSize;
                addSuccess("BK7252U: detected flash size " + formatFlashSize(detectedSize)
                    + ", full reads will use wire base " + formatHex(bk7252ReadAddressBase) + Environment.NewLine);
                return;
            }

            if(fallbackSize > DEFAULT_FLASH_SIZE && fallbackSize <= BK7252_MAX_FLASH_SIZE)
            {
                setFlashSize(fallbackSize);
                bk7252ReadAddressBase = fallbackSize;
                addWarning("BK7252U: flash size wrap-around not detected, using requested size "
                    + formatFlashSize(fallbackSize) + " and wire base " + formatHex(bk7252ReadAddressBase) + "." + Environment.NewLine);
                return;
            }

            addWarning("BK7252U: flash size wrap-around not detected, keeping default 2MB and wire base 0x200000." + Environment.NewLine);
        }
        float getCRCCommandTimeout(int rangeLength)
        {
            if (usesDirectFlashAccessProfile() == false)
            {
                return 5.0f;
            }
            if (rangeLength <= SECTOR_SIZE)
            {
                return FLASH_COMMAND_MIN_TIMEOUT;
            }
            return Math.Max(5.0f, rangeLength / (float)0x40000);
        }

        bool tryGetDeviceCRC(int start, int endExclusive, out uint crc)
        {
            crc = 0;
            int commandEnd = endExclusive;
            if (usesDirectFlashAccessProfile())
            {
                commandEnd--;
            }
            float timeout = getCRCCommandTimeout(endExclusive - start);
            byte[] response = Start_Cmd(BuildCmd_CheckCRC(start, commandEnd), CalcRxLength_CheckCRC(), timeout);
            if (response == null)
            {
                return false;
            }
            byte[] expected = new byte[] { 0x04, 0x0e, 0x08, 0x01, 0xe0, 0xfc, (byte)CommandCode.CheckCRC };
            if (response.Length < 11 || expected.Length > response.Length
                || ByteArrayCompare(expected, response, expected.Length) == false)
            {
                return false;
            }
            crc = (uint)readInt32LE(response, 7);
            return true;
        }

        CRCVerificationResult verifyCRC(int start, int endExclusive, byte[] data, out uint deviceCRC, out uint localCRC)
        {
            localCRC = CRC.crc32_ver2(0xffffffff, data);
            if (tryGetDeviceCRC(start, endExclusive, out deviceCRC) == false)
            {
                return CRCVerificationResult.TransportError;
            }
            return deviceCRC == localCRC ? CRCVerificationResult.Match : CRCVerificationResult.Mismatch;
        }

        float getFlashRead4KTimeout()
        {
            int currentBaud = serial != null ? serial.BaudRate : baudrate;
            float transferSeconds = (READ_RESPONSE_HEADER_SIZE + SECTOR_SIZE) * 10.0f / Math.Max(currentBaud, 1);
            return Math.Max(FLASH_COMMAND_MIN_TIMEOUT, transferSeconds * 5.0f);
        }

        static byte[] copyPage(byte[] data, int offset)
        {
            byte[] page = new byte[SECTOR_SIZE];
            Array.Copy(data, offset, page, 0, SECTOR_SIZE);
            return page;
        }

        static bool pageIsErased(byte[] page)
        {
            return page.All(value => value == 0xFF);
        }

        bool writePageWithCRCVerification(int address, byte[] data, int offset)
        {
            byte[] page = copyPage(data, offset);
            bool erasedPage = pageIsErased(page);
            bool eraseBeforeRetry = false;
            bool writeBeforeVerification = erasedPage == false;
            for (int attempt = 1; attempt <= VERIFIED_PAGE_WRITE_ATTEMPTS; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                if (eraseBeforeRetry)
                {
                    addWarning("Retrying page " + formatHex(address) + ": erasing its 4K sector again." + Environment.NewLine);
                    if (eraseSector(address, 0x20) == false)
                    {
                        continue;
                    }
                    eraseBeforeRetry = false;
                    writeBeforeVerification = erasedPage == false;
                }
                if (writeBeforeVerification && writeSector4K(address, page, 0) == false)
                {
                    addWarning("Write page " + formatHex(address) + " failed on attempt " + attempt + "." + Environment.NewLine);
                    eraseBeforeRetry = true;
                    continue;
                }
                writeBeforeVerification = false;
                CRCVerificationResult crcResult = verifyCRC(address, address + SECTOR_SIZE, page, out uint deviceCRC, out uint localCRC);
                if (crcResult == CRCVerificationResult.Match)
                {
                    return true;
                }
                if (crcResult == CRCVerificationResult.TransportError)
                {
                    addWarning("Write page " + formatHex(address) + " CRC command failed on attempt " + attempt
                        + "; retrying verification without erasing." + Environment.NewLine);
                    continue;
                }
                addWarning("Write page " + formatHex(address) + " CRC mismatch on attempt " + attempt
                    + ": device " + formatHex(deviceCRC) + ", source " + formatHex(localCRC) + "."
                    + Environment.NewLine);
                eraseBeforeRetry = true;
            }
            return false;
        }

        MemoryStream readChunk(int startSector, int sectors, bool skipBlankHeuristic = false, int readRangeAttempt = 1)
        {
            logger.setState("Reading...", Color.Transparent);
            logger.setProgress(0, sectors);
            MemoryStream tempResult = new MemoryStream();
            if (startSector < 0 || sectors <= 0 || (long)startSector + (long)sectors * SECTOR_SIZE > FLASH_SIZE)
            {
                addError("Read range is outside the detected flash size." + Environment.NewLine);
                return null;
            }
            if ((startSector % SECTOR_SIZE) != 0)
            {
                addError("Read range must start on a 4K boundary." + Environment.NewLine);
                return null;
            }
            int step = SECTOR_SIZE;
            bool directFlashAccessProfile = usesDirectFlashAccessProfile();
            addLog("Going to start reading at offset " + formatHex(startSector) + "..." + Environment.NewLine);
            for (int i = 0; i < sectors; i++)
            {
                int logicalAddr = startSector + step * i;
                int wireAddr = translateReadAddressForChip(logicalAddr);
                addLog(wireAddr != logicalAddr
                    ? formatHex(logicalAddr) + " -> " + formatHex(wireAddr) + "... "
                    : formatHex(logicalAddr) + "... ");
                bool bOk;
                if (directFlashAccessProfile)
                {
                    byte[] payload = readSectorPayload(wireAddr, FLASH_READ4K_ATTEMPTS - 1, getFlashRead4KTimeout());
                    bOk = payload != null;
                    if(bOk)
                    {
                        tempResult.Write(payload, 0, payload.Length);
                    }
                }
                else if(chipType == BKType.BK7252)
                {
                    byte[] payload = readSectorPayload(wireAddr, BK7252_READ_ATTEMPTS - 1);
                    bOk = payload != null;
                    if(bOk)
                    {
                        tempResult.Write(payload, 0, payload.Length);
                    }
                }
                else
                {
                    bOk = readSectorTo(wireAddr, tempResult);
                }
                if (bOk == false)
                {
                    logger.setState("Reading failed.", Color.Red);
                    addError("Failed reading page " + formatHex(logicalAddr) + "." + Environment.NewLine);
                    return null;
                }
                logger.setProgress(i + 1, sectors);
            }
            addLog(Environment.NewLine + "Read operation finished; verifying result..." + Environment.NewLine);
            byte[] result = tempResult.ToArray();
            if (directFlashAccessProfile == false && skipBlankHeuristic == false
                && checkAbnormal(startSector, sectors, result) == false)
            {
                return null;
            }
            bool crcVerified;
            if (chipType == BKType.BK7252)
            {
                if (checkBK7252ReadCRC(startSector, sectors, result, out crcVerified) == false)
                {
                    return null;
                }
            }
            else
            {
                bool finalReadRangeAttempt = readRangeAttempt >= READ_RANGE_ATTEMPTS;
                CRCVerificationResult crcResult = checkCRCResult(startSector, sectors, result, directFlashAccessProfile);
                if (directFlashAccessProfile && crcResult == CRCVerificationResult.TransportError)
                {
                    for (int crcAttempt = 2; crcAttempt <= READ_RANGE_CRC_ATTEMPTS; crcAttempt++)
                    {
                        addWarning("CRC command failed; retrying verification without rereading flash ("
                            + crcAttempt + "/" + READ_RANGE_CRC_ATTEMPTS + ")." + Environment.NewLine);
                        crcResult = checkCRCResult(startSector, sectors, result, true);
                        if (crcResult != CRCVerificationResult.TransportError)
                        {
                            break;
                        }
                    }
                }
                if (directFlashAccessProfile && crcResult == CRCVerificationResult.Mismatch && finalReadRangeAttempt == false)
                {
                    addWarning("CRC mismatch; retrying the read range ("
                        + (readRangeAttempt + 1) + "/" + READ_RANGE_ATTEMPTS + ")." + Environment.NewLine);
                    return readChunk(startSector, sectors, skipBlankHeuristic, readRangeAttempt + 1);
                }
                crcVerified = crcResult == CRCVerificationResult.Match;
                bool ignoreCRCError = directFlashAccessProfile
                    ? bIgnoreCRCErr && (finalReadRangeAttempt || crcResult == CRCVerificationResult.TransportError)
                    : bIgnoreCRCErr;
                if (acceptCRCResult(crcResult, ignoreCRCError) == false)
                {
                    logger.setState("CRC verification failed!", Color.Red);
                    addError(crcResult == CRCVerificationResult.TransportError
                        ? "CRC command failed after retrying verification." + Environment.NewLine
                        : "CRC still mismatched after rereading the flash range." + Environment.NewLine);
                    return null;
                }
            }
            if (crcVerified == false)
            {
                logger.setState("Read contains unverified data.", Color.Yellow);
                addWarning("Read completed, but IgnoreCRCErr accepted an unverified range." + Environment.NewLine);
            }
            else
            {
                logger.setState("Reading success!", Color.Green);
                addSuccess("All read!" + Environment.NewLine);
            }
            addLog("Loaded total " + formatHex(sectors * step) + " bytes " + Environment.NewLine);
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
            return acceptCRCResult(checkCRCResult(startSector, total, array), bIgnoreCRCErr);
        }

        CRCVerificationResult checkCRCResult(int startSector, int total, byte [] array, bool failureIsRecoverable = false)
        {
            logger.setState("Doing CRC verification...", Color.Transparent);
            addLog("Starting CRC check for " + total + " sectors, starting at offset 0x" + startSector.ToString("X2") + Environment.NewLine);
            int last = startSector + total * SECTOR_SIZE;
            CRCVerificationResult result = verifyCRC(startSector, last, array, out uint bk_crc, out uint our_crc);
            if (result == CRCVerificationResult.TransportError)
            {
                if (failureIsRecoverable)
                {
                    addWarning("Failed to read CRC from the chip." + Environment.NewLine);
                }
                else
                {
                    logger.setState("CRC command failed!", Color.Red);
                    addError("Failed to read CRC from the chip." + Environment.NewLine);
                }
                return result;
            }
            if (result == CRCVerificationResult.Mismatch)
            {
                if (failureIsRecoverable)
                {
                    addWarning("CRC mismatch: device " + formatHex(bk_crc) + ", local " + formatHex(our_crc) + "." + Environment.NewLine);
                }
                else
                {
                    logger.setState("CRC mismatch!", Color.Red);
                    addError("CRC mismatch!" + Environment.NewLine);
                    addError("Send by BK " + formatHex(bk_crc) + ", our CRC " + formatHex(our_crc) + Environment.NewLine);
                    if (usesDirectFlashAccessProfile() == false)
                    {
                        addError("Maybe you have wrong chip type set?" + Environment.NewLine);
                        addError("Did you set BK7231T but have in reality BK7231N or BK7231M?" + Environment.NewLine);
                    }
                }
                return result;
            }
            addSuccess("CRC matches " + formatHex(bk_crc) + "!" + Environment.NewLine);
            return result;
        }

        bool acceptCRCResult(CRCVerificationResult result, bool ignoreCRCError)
        {
            if (result == CRCVerificationResult.Match)
            {
                return true;
            }
            if (ignoreCRCError == false)
            {
                return false;
            }
            if (result == CRCVerificationResult.TransportError)
            {
                addWarning("IgnoreCRCErr checked, bin will be accepted without a device CRC." + Environment.NewLine);
            }
            else
            {
                addWarning("IgnoreCRCErr checked, bin will be saved even if there is a crc mismatch" + Environment.NewLine);
            }
            return true;
        }
        bool checkBK7252ReadCRC(int startSector, int total, byte[] array, out bool crcVerified)
        {
            crcVerified = false;
            logger.setState("Doing CRC verification...", Color.Transparent);
            int logicalEnd = startSector + total * SECTOR_SIZE;
            int mappedStart = translateReadAddressForChip(startSector);
            int mappedEnd = translateReadAddressForChip(logicalEnd);
            uint our_crc = CRC.crc32_ver2(0xffffffff, array);

            addLog("BK7252U: starting mapped CRC check for " + total + " sectors, logical "
                + formatHex(startSector) + ".." + formatHex(logicalEnd)
                + " -> wire " + formatHex(mappedStart) + ".." + formatHex(mappedEnd) + Environment.NewLine);

            bool mappedCRCAvailable = tryGetDeviceCRC(mappedStart, mappedEnd, out uint mapped_crc);
            if (mappedCRCAvailable && mapped_crc == our_crc)
            {
                crcVerified = true;
                addSuccess("BK7252U: mapped CRC matches " + formatHex(mapped_crc) + "!" + Environment.NewLine);
                return true;
            }
            if (mappedCRCAvailable)
            {
                addWarning("BK7252U: mapped CRC " + formatHex(mapped_crc) + " did not match our CRC "
                    + formatHex(our_crc) + "; trying logical CRC range." + Environment.NewLine);
            }
            else
            {
                addWarning("BK7252U: mapped CRC command failed; trying logical CRC range." + Environment.NewLine);
            }

            bool logicalCRCAvailable = tryGetDeviceCRC(startSector, logicalEnd, out uint logical_crc);
            if (logicalCRCAvailable && logical_crc == our_crc)
            {
                crcVerified = true;
                addSuccess("BK7252U: logical CRC matches " + formatHex(logical_crc) + "!" + Environment.NewLine);
                return true;
            }

            logger.setState("CRC verification failed!", Color.Red);
            if (mappedCRCAvailable || logicalCRCAvailable)
            {
                addError("BK7252U CRC mismatch!" + Environment.NewLine);
                addError("Mapped CRC " + (mappedCRCAvailable ? formatHex(mapped_crc) : "unavailable")
                    + ", logical CRC " + (logicalCRCAvailable ? formatHex(logical_crc) : "unavailable")
                    + ", our CRC " + formatHex(our_crc) + Environment.NewLine);
            }
            else
            {
                addError("BK7252U CRC commands failed for both mapped and logical ranges." + Environment.NewLine);
            }
            if (bIgnoreCRCErr)
            {
                addWarning("IgnoreCRCErr checked, bin will be accepted despite the CRC failure." + Environment.NewLine);
                return true;
            }
            return false;
        }
        bool checkExistingSessionAliveBeforeWrite()
        {
            int logicalAddr = chipType == BKType.BK7252 ? BOOTLOADER_SIZE : 0;
            int wireAddr = translateReadAddressForChip(logicalAddr);
            addLog("Checking existing flasher session before write at logical "
                + formatHex(logicalAddr) + " -> wire " + formatHex(wireAddr) + "... ");
            byte[] payload = readSectorPayload(wireAddr, 1, 2);
            if(payload == null)
            {
                addWarning("failed." + Environment.NewLine);
                return false;
            }
            addSuccess("OK." + Environment.NewLine);
            return true;
        }

        static bool FileNameHasQioMarker(string sourceFileName)
        {
            string fileName = Path.GetFileName(sourceFileName);
            return !string.IsNullOrEmpty(fileName) &&
                fileName.IndexOf("_QIO_", StringComparison.Ordinal) >= 0;
        }

        static bool IsBK7252UFullBackupLength(int length)
        {
            return length == DEFAULT_FLASH_SIZE || length == BK7252_MAX_FLASH_SIZE;
        }

        bool doReadAndWriteInternal(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if (rwMode == WriteMode.ReadAndWrite)
            {
                ms = null;
            }
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
            if (doGenericSetup(false) == false)
            {
                return false;
            }
            if(chipType == BKType.BK7252)
            {
                detectBK7252UFlashSize();
            }
            int realStart = 0;
            int realLen = TOTAL_SECTORS;
            if (rwMode == WriteMode.ReadAndWrite)
            {
                if(chipType == BKType.BK7252)
                {
                    addLog("BK7252U: full QIO backup before write logical 0x000000.." + formatHex(FLASH_SIZE - 1) + Environment.NewLine);
                }
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
                if (string.IsNullOrEmpty(sourceFileName))
                {
                    addLogLine("No filename given!");
                    return false;
                }
                addLog("Reading file " + sourceFileName + "..." + Environment.NewLine);
                data = File.ReadAllBytes(sourceFileName);
                if (data == null)
                {
                    addError("Failed to open " + sourceFileName + "..." + Environment.NewLine);
                    return false;
                }
                addSuccess("Loaded " + data.Length + " bytes from " + sourceFileName + "..." + Environment.NewLine);
                bool bSkipBootloader = false;
                bool bHasQioMarker = FileNameHasQioMarker(sourceFileName);
                if (!bCustomWriteMode && (bHasQioMarker
                    || (chipType == BKType.BK7252 && IsBK7252UFullBackupLength(data.Length))))
                {
                    if(bOverwriteBootloader == false && (chipType == BKType.BK7231N || chipType == BKType.BK7231M))
                    {
                        startSector = BK7231Flasher.BOOTLOADER_SIZE;
                        bSkipBootloader = true;
                    }
                    if(this.chipType == BKType.BK7231T || chipType == BKType.BK7231U)
                    {
                        startSector = BK7231Flasher.BOOTLOADER_SIZE;
                        bSkipBootloader = true;
                    }
                    if (this.chipType == BKType.BK7252)
                    {
                        startSector = BK7231Flasher.BOOTLOADER_SIZE;
                        bSkipBootloader = true;
                    }
                }
                if (bSkipBootloader && startSector == BK7231Flasher.BOOTLOADER_SIZE)
                {
                    // Skip the bootloader in full-image/QIO writes for bootloader-protected chips.
                    if (data.Length <= startSector)
                    {
                        addError("Cannot skip QIO bootloader area because the file is only "
                            + data.Length + " bytes, but the bootloader skip size is "
                            + startSector + " bytes." + Environment.NewLine);
                        return false;
                    }

                    int length = data.Length - startSector;
                    byte[] newData = new byte[length];
                    Array.Copy(data, startSector, newData, 0, length);
                    data = newData;
                    addWarning("Writing full image from " + formatHex(startSector) + " and skipping protected bootloader..." + Environment.NewLine);
                    addWarning("... so bootloader will not be overwritten!" + Environment.NewLine);
                }
            }
            bool backupRequiresBusReattach = rwMode == WriteMode.ReadAndWrite
                && usesDirectFlashAccessProfile() == false && chipType != BKType.BK7252;
            if (backupRequiresBusReattach)
            {
                addLog("Preparing to write data file to chip - reattaching before write..." + Environment.NewLine);
                if (doGetBusAndSetBaudRate() == false)
                {
                    return false;
                }
            }
            else if (rwMode == WriteMode.ReadAndWrite)
            {
                addLog("Backup complete, keeping current flasher session for write phase." + Environment.NewLine);
                if(checkExistingSessionAliveBeforeWrite() == false)
                {
                    if(chipType == BKType.BK7252)
                    {
                        addError("BK7252U: flasher session is not responding after backup; aborting before erase/write." + Environment.NewLine);
                        return false;
                    }
                    addError("Flasher session is not responding after backup; aborting before erase/write." + Environment.NewLine);
                    return false;
                }
            }
            if (usesDirectFlashAccessProfile() && prepareFlashForModification() == false)
            {
                return false;
            }
            if (writeChunk(startSector, data, rwMode) == false)
            {
                addError("Writing file data to chip failed." + Environment.NewLine);
                return false;
            }
            if(rwMode == WriteMode.ReadAndWrite && chipType == BKType.BK7238 && ms != null)
            {
                var rData = ms.ToArray();
                RFPartitionUtil.getMACFromQio(rData, chipType, out var isNeedFix);
                if(isNeedFix)
                {
                    var rfAddr = RFPartitionUtil.getRFOffset(chipType);
                    var rfData = RFPartitionUtil.getRFFromBackup(rData, chipType, out var origAddr);
                    if(rfData.Length != 0)
                    {
                        addLog($"Moving RF partition from {formatHex(origAddr)} to {formatHex(rfAddr)}..." + Environment.NewLine);
                        if(writeChunk(rfAddr, rfData, rwMode) == false)
                        {
                            addErrorLine("RF move failed!.");
                            return false;
                        }
                    }
                    else
                    {
                        addWarningLine("No RF partition found! You must manually restore it.");
                    }
                }
            }
            addSuccess("Writing file data to chip successs." + Environment.NewLine);
            //File.WriteAllBytes("lastRead.bin", ms.ToArray());
            return true;
        }
        void doReadInternal(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            logger.setProgress(0, sectors);
            addLog(Environment.NewLine + "Starting read!" + Environment.NewLine);
            addLog("Read parms: start 0x"+
                (startSector ).ToString("X2")
                + " (sector " + startSector / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
                (sectors * BK7231Flasher.SECTOR_SIZE).ToString("X2")
                + " (" + sectors + " sectors)"
                + Environment.NewLine);
            if (doGenericSetup(false) == false)
            {
                return;
            }
            if(chipType == BKType.BK7252)
            {
                int requestedFullReadSize = fullRead ? BK7252_MAX_FLASH_SIZE : 0;
                detectBK7252UFlashSize(requestedFullReadSize);
                if(fullRead)
                {
                    startSector = 0;
                    addLog("BK7252U: full QIO read logical 0x000000.." + formatHex(FLASH_SIZE - 1) + Environment.NewLine);
                }
            }
            if(fullRead)
                sectors = TOTAL_SECTORS;
            ms = readChunk(startSector, sectors);
            resetLegacyFlashSize();
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
            byte[] txbuf = BuildCmd_FlashGetMID(0x9f);
            int attempts = usesDirectFlashAccessProfile() ? FLASH_MID_ATTEMPTS : 1;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
                byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashGetID(), 0.1f);
                if (rxbuf != null)
                {
                    int mid = CheckRespond_FlashGetMID(rxbuf);
                    if (mid != 0)
                    {
                        return mid;
                    }
                }
                if (attempt < attempts)
                {
                    addWarning("Flash MID read failed on attempt " + attempt + "; retrying." + Environment.NewLine);
                    Thread.Sleep(FLASH_MID_RETRY_DELAY_MS);
                }
            }
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
        byte[] readSector(int addr, float timeout = 15)
        {
            //addLog("Starting read sector for " + addr + Environment.NewLine);
            byte[] txbuf = BuildCmd_FlashRead4K(addr);
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashRead4K(), timeout);
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
        bool linkCheck(float timeout = 0.001f)
        {
            byte[] txbuf = BuildCmd_LinkCheck();
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_LinkCheck(), timeout);
            observeLinkStage(0x00, rxbuf);
            return rxbuf != null && CheckRespond_LinkCheck(rxbuf);
        }
        bool isSectorModificationAllowed(int addr)
        {
            if (addr < 0 || addr >= FLASH_SIZE)
            {
                addError("ERROR: Out of range write/erase attempt detected, this could break bootloader");
                return false;
            }
            addr %= FLASH_SIZE;
            if (chipType != BKType.BK7231T && chipType != BKType.BK7231U && chipType != BKType.BK7252) 
                return true;
            if (addr >= 0 && addr < BOOTLOADER_SIZE)
            {
                addError("ERROR: protected bootloader overwriting attempt detected, interrupting.");
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
            float timeout = szcmd == 0xD8 ? 5.0f : 2.0f;
            byte[] rxbuf = Start_Cmd(txbuf, CalcRxLength_FlashErase(), timeout);
            return rxbuf != null && CheckRespond_FlashErase(rxbuf, addr, szcmd);
        }
        bool setBaudrate(int baudrate, int delay_ms)
        {
            byte[] txbuf = BuildCmd_SetBaudRate(baudrate, delay_ms);
            Start_Cmd(txbuf,0, 0.5f);
            Stopwatch drainTimer = Stopwatch.StartNew();
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    addWarning("Baud rate change cancelled while waiting for serial output." + Environment.NewLine);
                    return false;
                }
                if (serial == null || serial.IsOpen == false)
                {
                    addError("Serial port closed while changing baud rate." + Environment.NewLine);
                    return false;
                }
                if (serial.BytesToWrite <= 0)
                {
                    break;
                }
                if (drainTimer.ElapsedMilliseconds >= SET_BAUD_DRAIN_TIMEOUT_MS)
                {
                    addError("Timed out waiting for serial output before changing baud rate." + Environment.NewLine);
                    return false;
                }
                Thread.Sleep(1);
            }
            Thread.Sleep(delay_ms/2);
            int prev = serial.BaudRate;
            serial.BaudRate = baudrate;
            byte[] rxbuf = Start_Cmd(null, CalcRxLength_SetBaudRate(), 0.5f, (byte)CommandCode.SetBaudRate);
            if (rxbuf != null)
            {
                if (CheckRespond_SetBaudRate(rxbuf, baudrate, delay_ms))
                {
                    return true;
                }
            }
            addWarning("Set-baud acknowledgement was missing or invalid; checking target at " + baudrate + " baud." + Environment.NewLine);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (cancellationToken.IsCancellationRequested || serial == null || serial.IsOpen == false)
                {
                    return false;
                }
                if (linkCheck(0.05f))
                {
                    addSuccess("Link check confirmed communication at " + baudrate + " baud; continuing." + Environment.NewLine);
                    return true;
                }
            }
            if (serial != null && serial.IsOpen)
            {
                serial.BaudRate = prev;
            }
            return false;
        }

        bool eraseRange(int startSector, int sectors)
        {
            if (startSector < 0 || (startSector % SECTOR_SIZE) != 0 || sectors <= 0)
            {
                addError("Erase range must start on a 4K boundary and contain at least one sector." + Environment.NewLine);
                return false;
            }
            long endAddress = (long)startSector + (long)sectors * SECTOR_SIZE;
            if (endAddress > FLASH_SIZE)
            {
                addError("Erase range " + formatHex(startSector) + ".." + formatHex((int)endAddress)
                    + " exceeds flash size " + formatFlashSize(FLASH_SIZE) + "." + Environment.NewLine);
                return false;
            }
            int current = startSector / SECTOR_SIZE;
            int end = current + sectors;
            int completed = 0;

            bool eraseUnit(int addr, int command, int pages, string unitName)
            {
                for (int attempt = 1; attempt <= ERASE_ATTEMPTS; attempt++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.setState("Erase cancelled.", Color.Yellow);
                        return false;
                    }
                    addLog("Erasing " + unitName + " " + formatHex(addr) + "...");
                    if (eraseSector(addr, command))
                    {
                        addLog(" ok! ");
                        completed += pages;
                        logger.setProgress(Math.Min(completed, sectors), sectors);
                        return true;
                    }
                    addWarning(" failed (attempt " + attempt + "/" + ERASE_ATTEMPTS + "). ");
                }
                logger.setState("Erase failed.", Color.Red);
                addError(" Erasing " + unitName + " " + formatHex(addr) + " failed." + Environment.NewLine);
                return false;
            }

            while(current < end && (current % SECTORS_PER_BLOCK) != 0)
            {
                if (eraseUnit(current * SECTOR_SIZE, 0x20, 1, "sector") == false)
                {
                    return false;
                }
                current++;
            }
            while((end - current) >= SECTORS_PER_BLOCK)
            {
                if (eraseUnit(current * SECTOR_SIZE, 0xD8, SECTORS_PER_BLOCK, "block") == false)
                {
                    return false;
                }
                current += SECTORS_PER_BLOCK;
            }
            while(current < end)
            {
                if (eraseUnit(current * SECTOR_SIZE, 0x20, 1, "sector") == false)
                {
                    return false;
                }
                current++;
            }
            return true;
        }
    }
}

