using BK7231Flasher.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using static BK7231Flasher.BL602Utils;

namespace BK7231Flasher
{
    public class BL616Flasher : BaseFlasher
    {
        //int timeoutMs = 10000;
        float flashSizeMB = 2;
        byte[] flashID;
        BLInfo blinfo = null;

        // BLDC/DevCube eflash-loader config uses tx_size=2056. In bflb_mcu_tool,
        // flash read/write payload slicing uses tx_size - 8, i.e. 2048 bytes.
        const int BL_FLASH_TX_SIZE = 2056;
        const int BL_FLASH_CMD_OVERHEAD = 8;
        const int BL_FLASH_READ_CHUNK = BL_FLASH_TX_SIZE - BL_FLASH_CMD_OVERHEAD;
        const int BL_FLASH_WRITE_DATA_CHUNK = BL_FLASH_TX_SIZE - BL_FLASH_CMD_OVERHEAD;
        const int BL_SYNC_WAIT_MS = 1000;
        const double BL616_SYNC_BURST_SECONDS = 0.006;
        const int BL_ROM_BOOT_BAUD = 500000;
        const int BL616_CLK_SET_TIMEOUT_MS = 2000;
        const int BL616_COMMAND_RETRY_COUNT = 3;
        const int BL616_READ_RETRY_COUNT = 3;
        const int BL616_WRITE_RETRY_COUNT = 10;
        const int BL616_BOOTROM_UART_TIMEOUT_MS = 10000;
        const uint BL616_BOOTROM_TIMEOUT_REGISTER = 0x6102DF04;
        const uint BL616_BOOTROM_TIMEOUT_VALUE_A0 = 0x27101200;
        const byte BL616_BOOT_INFO_A0_MARKER = 0x01;
        static readonly byte[] BL616_FLASH_SET_PARA = new byte[] { 0x80, 0x41, 0x01, 0x00 };

        public bool Sync()
        {
            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    addLog($"Sync attempt {i}/1000 ");
                    if(internalSync())
                    {
                        logger.addLog("... OK!" + Environment.NewLine, Color.Green);
                        return true;
                    }
                    addWarningLine("... failed, will retry!");
                    if(i % 10 == 1)
                    {
                        addLogLine($"If doing something immediately after another operation, it might not sync for about half a minute");
                        addLogLine("Otherwise, please pull high BOOT pin and reset.");
                    }
                    Thread.Sleep(50);
                }
                catch(Exception ex)
                {
                    addLogLine("");
                    addErrorLine(ex.ToString());
                    return false;
                }
            }
            return false;
        }


        bool internalSync()
        {
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();

            // BLDC uses a baud-scaled sync burst. For BL616 this is a 6ms 0x55 train.
            // A fixed-length burst can be too
            // long at low baud rates, which can destabilize the first post-sync command.
            int syncPatternLen = getSyncPatternLength();
            byte[] syncRequest = Enumerable.Repeat((byte)'U', syncPatternLen).ToArray();
            serial.Write(syncRequest, 0, syncRequest.Length);

            byte[] response;
            if (TryReadExact(2, BL_SYNC_WAIT_MS, out response) && IsAck(response, "OK"))
            {
                Thread.Sleep(30);
                return true;
            }

            return false;
        }

        int getSyncPatternLength()
        {
            int activeBaudrate = baudrate;
            if(serial != null && serial.BaudRate > 0)
            {
                activeBaudrate = serial.BaudRate;
            }

            double burstSeconds = BL616_SYNC_BURST_SECONDS;
            int syncLen = (int)(burstSeconds * activeBaudrate / 10.0);
            if(syncLen < 16)
            {
                syncLen = 16;
            }
            return syncLen;
        }

        internal class BLInfo
        {
            public int BootromVersion;
            public byte[] remaining;
            public BKType Variant;

            public BLInfo(int bootromVersion)
            {
                BootromVersion = bootromVersion;
                var bootrom = $"{BootromVersion:X}";
                // BL616 ROM version is 0x6160001. BL618 appears to use a compatible bootrom flow.
                if(bootrom.StartsWith("616") || bootrom.StartsWith("618"))
                {
                    Variant = BKType.BL616;
                }
                else
                {
                    Variant = BKType.BL602;
                }
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"BootROM version: {BootromVersion} ({BootromVersion:X})");
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
                sb.AppendLine($"Chip type: {Variant}");
                return sb.ToString();
            }
        }

        internal BLInfo getInfo()
        {
            byte[] res = executeCommand(0x10, null, 0, 0, false, 2, 2, true);
            if (res == null)
            {
                return null;
            }
            int len = res[0] + (res[1] << 8);
            if (len + 2 != res.Length)
            {
                return null;
            }
            var v = new BLInfo(res[2] + (res[3] << 8) + (res[4] << 16) + (res[5] << 24))
            {
                remaining = new byte[res.Length - 6]
            };
            Array.Copy(res, 6, v.remaining, 0, v.remaining.Length);
            return v;
        }
        internal byte[] readFlashID()
        {
            byte[] res = executeCommand(0x36, null, 0, 0, true, 2, 2, true);
            if (res == null)
            {
                return null;
            }

            if (res.Length < 6)
            {
                addLogLine("Invalid response length: " + res.Length);
                return null;
            }

            flashSizeMB = (float)(1 << (res[4] - 0x11)) / 8;
            if(flashSizeMB > 32)
            {
                return null;
            }
            addLogLine("Flash ID: {0:X2}{1:X2}{2:X2}{3:X2}", res[2], res[3], res[4], res[5]);
            addLogLine($"Flash size is {flashSizeMB}MB");
            return res;
        }
        bool IsAck(byte[] rep, string ack)
        {
            if(rep == null || rep.Length != 2 || string.IsNullOrEmpty(ack))
            {
                return false;
            }

            // BLDC accepts slightly degraded ACK pairs when one byte survives.
            // Keep this behaviour for high-baud resilience on BL616 transport.
            if(ack == "OK")
            {
                return rep[0] == 0x4F || rep[1] == 0x4F || rep[0] == 0x4B || rep[1] == 0x4B;
            }
            if(ack == "PD")
            {
                return rep[0] == 0x50 || rep[1] == 0x50 || rep[0] == 0x44 || rep[1] == 0x44;
            }

            return rep[0] == (byte)ack[0] && rep[1] == (byte)ack[1];
        }

        bool TryReadExact(int count, int timeoutMs, out byte[] data)
        {
            data = new byte[count];
            int offset = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while (offset < count && sw.ElapsedMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int available = serial.BytesToRead;
                if (available <= 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                int toRead = Math.Min(available, count - offset);
                int got = serial.Read(data, offset, toRead);
                if (got > 0)
                {
                    offset += got;
                }
            }

            if (offset == count)
            {
                return true;
            }

            if (offset > 0)
            {
                Array.Resize(ref data, offset);
            }
            return false;
        }

        int getInitialBootBaudrate()
        {
            // BL616 bootrom flow starts at 500000, then switches to selected transfer baud
            // through clk_set (0x22).
            return BL_ROM_BOOT_BAUD;
        }

        int getEflashBaudrate(BKType variant)
        {
            // Honour the selected GUI/CLI baud for the eflash-loader phase after the
            // conservative ROM sync/upload phase has completed. The initial boot phase
            // may still be lowered for reliability, but read/write traffic runs at the
            // requested transfer baud.
            return baudrate;
        }

        void setSerialBaudrate(int targetBaudrate, string stage)
        {
            if(serial == null)
            {
                return;
            }

            if(serial.BaudRate == targetBaudrate)
            {
                return;
            }

            addLogLine($"Switching {stage} baud to {targetBaudrate}...");
            serial.BaudRate = targetBaudrate;
            Thread.Sleep(100);
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
        }

        void configureSerialPortHostBuffers(SerialPort port)
        {
            if(port == null)
            {
                return;
            }

            try
            {
                // BLDC uses larger UART queues; mirror that on host side to reduce
                // short-read events when running at multi-megabaud rates.
                int queueSize = Math.Max(8192, BL_FLASH_TX_SIZE * 16);
                port.ReadBufferSize = queueSize;
                port.WriteBufferSize = queueSize;
            }
            catch(InvalidOperationException)
            {
                // Buffer sizing is best-effort and must be done before opening.
            }
            catch(Exception ex)
            {
                addWarningLine($"Unable to configure serial buffers: {ex.Message}");
            }
        }

        float getSerialTransferTimeoutSeconds(int wireBytes, float minSeconds)
        {
            int activeBaudrate = baudrate;
            if(serial != null && serial.BaudRate > 0)
            {
                activeBaudrate = serial.BaudRate;
            }

            double wireSeconds = (wireBytes * 10.0) / Math.Max(9600, activeBaudrate);
            double timeout = Math.Max(minSeconds, (wireSeconds * 4.0) + 1.0);
            return (float)Math.Min(60.0, timeout);
        }

        byte[] executeCommand(int type, byte[] parms = null,
            int start = 0, int len = 0, bool bChecksum = false,
            float timeout = 0.1f, int expectedReplyLen = 2,
            bool lengthPrefixedPayload = false)
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

            var raw = new byte[] { (byte)type, chksum, (byte)(len & 0xFF), (byte)(len >> 8) };
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            serial.Write(raw, 0, raw.Length);
            if (parms != null && len > 0)
            {
                serial.Write(parms, start, len);
            }

            if (expectedReplyLen == 0)
            {
                return Array.Empty<byte>();
            }

            int timeoutMS = Math.Max(1, (int)(timeout * 1000));
            Stopwatch sw = Stopwatch.StartNew();
            bool pendingLogged = false;

            while (sw.ElapsedMilliseconds < timeoutMS)
            {
                int remainingMs = Math.Max(1, timeoutMS - (int)sw.ElapsedMilliseconds);
                byte[] rep;
                if (!TryReadExact(2, remainingMs, out rep))
                {
                    break;
                }

                if (IsAck(rep, "OK"))
                {
                    if (lengthPrefixedPayload)
                    {
                        byte[] lenBytes;
                        int repeatedOkCount = 0;
                        while (true)
                        {
                            if (!TryReadExact(2, timeoutMS, out lenBytes))
                            {
                                addLogLine($"Command 0x{type:X2} timed out while reading length prefix; got {lenBytes.Length}/2 bytes");
                                return null;
                            }

                            // Bouffalo's if_deal_response() consumes any extra OK pair before
                            // the 2-byte little-endian response length. Keep that behaviour so
                            // response parsing stays compatible with BLDC/bflb_iot_tool.
                            if (!IsAck(lenBytes, "OK"))
                            {
                                break;
                            }

                            repeatedOkCount++;
                            if (repeatedOkCount > 4)
                            {
                                addLogLine($"Command 0x{type:X2} received repeated OK while waiting for length prefix");
                                return null;
                            }
                        }

                        int payloadLen = lenBytes[0] | (lenBytes[1] << 8);
                        byte[] payload;
                        if (!TryReadExact(payloadLen, timeoutMS, out payload))
                        {
                            addLogLine($"Command 0x{type:X2} timed out while reading payload; got {payload.Length}/{payloadLen} bytes");
                            return null;
                        }

                        byte[] ret = new byte[2 + payload.Length];
                        ret[0] = lenBytes[0];
                        ret[1] = lenBytes[1];
                        Array.Copy(payload, 0, ret, 2, payload.Length);
                        return ret;
                    }

                    int payloadBytes = Math.Max(0, expectedReplyLen - 2);
                    if (payloadBytes == 0)
                    {
                        return Array.Empty<byte>();
                    }

                    byte[] payloadFixed;
                    if (!TryReadExact(payloadBytes, timeoutMS, out payloadFixed))
                    {
                        addLogLine($"Command 0x{type:X2} timed out while reading fixed payload; got {payloadFixed.Length}/{payloadBytes} bytes");
                        return null;
                    }
                    return payloadFixed;
                }

                if (IsAck(rep, "FL"))
                {
                    addLogLine($"Command 0x{type:X2} failed!");
                    return null;
                }

                if (IsAck(rep, "PD"))
                {
                    if (!pendingLogged)
                    {
                        addLogLine($"Command 0x{type:X2} pending...");
                        pendingLogged = true;
                    }
                    Thread.Sleep(20);
                    continue;
                }

                addLogLine($"Command 0x{type:X2} unexpected acknowledgement: {rep[0]:X2}{rep[1]:X2}");
                return null;
            }

            if (expectedReplyLen != 0) addLogLine($"Command 0x{type:X2} timed out!");
            return null;
        }
        internal BLInfo getAndPrintInfo()
        {
            blinfo = this.getInfo();
            if (blinfo == null)
            {
                return null;
            }
            addLogLine(blinfo.ToString());
            return blinfo;
        }

        internal bool writeFlash(byte[] data, int adr, int len = -1)
        {
            try
            {
                var bufLen = BL_FLASH_WRITE_DATA_CHUNK;
                if(len < 0)
                    len = data.Length;
                int ofs = 0;
                int startAddr = adr;
                logger.setProgress(0, len);
                int nextLogOffset = 0;
                doErase(adr, (len + 4095) / 4096);
                addLogLine("Starting flash write " + len);
                byte[] buffer = new byte[bufLen + 4];
                logger.setState("Writing", Color.White);
                Thread.Sleep(1000);
                while(ofs < len)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    while(ofs >= nextLogOffset)
                    {
                        addLog("." + formatHex(nextLogOffset) + ".");
                        nextLogOffset += 0x1000;
                    }
                    int chunk = len - ofs;
                    if(chunk > bufLen)
                        chunk = bufLen;
                    buffer[0] = (byte)(adr & 0xFF);
                    buffer[1] = (byte)((adr >> 8) & 0xFF);
                    buffer[2] = (byte)((adr >> 16) & 0xFF);
                    buffer[3] = (byte)((adr >> 24) & 0xFF);
                    Array.Copy(data, ofs, buffer, 4, chunk);
                    int bufferLen = chunk + 4;
                    float writeTimeout = getSerialTransferTimeoutSeconds(bufferLen + 4, 5.0f);

                    bool chunkWritten = false;
                    for(int retry = 0; retry < BL616_WRITE_RETRY_COUNT; retry++)
                    {
                        if(executeCommand(0x31, buffer, 0, bufferLen, true, writeTimeout) != null)
                        {
                            chunkWritten = true;
                            break;
                        }

                        addWarningLine($"Write retry {retry + 1}/{BL616_WRITE_RETRY_COUNT} at 0x{adr:X6}: no reply");
                        serial.DiscardInBuffer();
                        Thread.Sleep(50);
                        if(retry + 1 < BL616_WRITE_RETRY_COUNT && !tryRecoverTransferAfterTimeout("Write", adr))
                        {
                            break;
                        }
                    }

                    if(!chunkWritten)
                    {
                        throw new Exception("Write failed!");
                    }
                    ofs += chunk;
                    adr += chunk;
                    logger.setProgress(ofs, len);
                }
                addLogLine("");
                if(!CheckSHA256(startAddr, len, data))
                {
                    logger.setState("SHA mismatch!", Color.Red);
                    return false;
                }
                logger.setState("Writing done", Color.DarkGreen);
                addLogLine("Done flash write " + len);
                return true;
            }
            catch(Exception exc)
            {
                addErrorLine(exc.Message);
                return false;
            }
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
                float chunkTimeout = getSerialTransferTimeoutSeconds(chunk + 4, 1.0f);
                this.executeCommand(type, parms, start + ofs, chunk, false, chunkTimeout);
                ofs += chunk;
            }
        }

        bool setBL616ClockAndBaud(int targetBaudrate)
        {
            // BLDC load_function=2 sends clk_set (0x22) in bootrom context, then reopens host UART
            // at speed_uart_load without an additional sync.
            if(targetBaudrate <= 0)
            {
                addErrorLine("BL616 target baudrate is invalid.");
                return false;
            }

            byte[] clkSetPayload = new byte[8];
            clkSetPayload[0] = 0x01; // irq_enable = true
            clkSetPayload[4] = (byte)(targetBaudrate & 0xFF);
            clkSetPayload[5] = (byte)((targetBaudrate >> 8) & 0xFF);
            clkSetPayload[6] = (byte)((targetBaudrate >> 16) & 0xFF);
            clkSetPayload[7] = (byte)((targetBaudrate >> 24) & 0xFF);

            float clkSetTimeout = Math.Max(1.0f, BL616_CLK_SET_TIMEOUT_MS / 1000.0f);
            for(int attempt = 1; attempt <= BL616_COMMAND_RETRY_COUNT; attempt++)
            {
                if(executeCommand(0x22, clkSetPayload, 0, clkSetPayload.Length, true, clkSetTimeout) != null)
                {
                    setSerialBaudrate(targetBaudrate, "BL616 operation");
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(30);
                    return true;
                }

                if(attempt < BL616_COMMAND_RETRY_COUNT)
                {
                    addWarningLine($"BL616 clk_set retry {attempt}/{BL616_COMMAND_RETRY_COUNT - 1} for baud {targetBaudrate}...");
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(30);
                }
            }

            addErrorLine($"BL616 clk_set (0x22) failed for baud {targetBaudrate}.");
            return false;
        }

        bool setBL616FlashParameters()
        {
            for(int attempt = 1; attempt <= BL616_COMMAND_RETRY_COUNT; attempt++)
            {
                if(executeCommand(0x3B, BL616_FLASH_SET_PARA, 0, BL616_FLASH_SET_PARA.Length, true, 2.0f) != null)
                {
                    return true;
                }

                if(attempt < BL616_COMMAND_RETRY_COUNT)
                {
                    addWarningLine($"BL616 flash_set_para retry {attempt}/{BL616_COMMAND_RETRY_COUNT - 1}...");
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(30);
                }
            }

            addErrorLine("BL616 flash_set_para (0x3B) failed.");
            return false;
        }

        bool setBL616BootromUartTimeout()
        {
            bool isA0StyleBootInfo = blinfo?.remaining != null
                && blinfo.remaining.Length > 0
                && blinfo.remaining[0] == BL616_BOOT_INFO_A0_MARKER;

            byte[] payload;
            int command;
            string commandLabel;
            if(isA0StyleBootInfo)
            {
                payload = new byte[8];
                payload[0] = (byte)(BL616_BOOTROM_TIMEOUT_REGISTER & 0xFF);
                payload[1] = (byte)((BL616_BOOTROM_TIMEOUT_REGISTER >> 8) & 0xFF);
                payload[2] = (byte)((BL616_BOOTROM_TIMEOUT_REGISTER >> 16) & 0xFF);
                payload[3] = (byte)((BL616_BOOTROM_TIMEOUT_REGISTER >> 24) & 0xFF);
                payload[4] = (byte)(BL616_BOOTROM_TIMEOUT_VALUE_A0 & 0xFF);
                payload[5] = (byte)((BL616_BOOTROM_TIMEOUT_VALUE_A0 >> 8) & 0xFF);
                payload[6] = (byte)((BL616_BOOTROM_TIMEOUT_VALUE_A0 >> 16) & 0xFF);
                payload[7] = (byte)((BL616_BOOTROM_TIMEOUT_VALUE_A0 >> 24) & 0xFF);
                command = 0x50; // memory_write
                commandLabel = "memory_write";
            }
            else
            {
                payload = BitConverter.GetBytes(BL616_BOOTROM_UART_TIMEOUT_MS);
                command = 0x23; // set_timeout
                commandLabel = "set_timeout";
            }

            for(int attempt = 1; attempt <= BL616_COMMAND_RETRY_COUNT; attempt++)
            {
                if(executeCommand(command, payload, 0, payload.Length, true, 2.0f) != null)
                {
                    return true;
                }

                if(attempt < BL616_COMMAND_RETRY_COUNT)
                {
                    addWarningLine($"BL616 {commandLabel} retry {attempt}/{BL616_COMMAND_RETRY_COUNT - 1}...");
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(30);
                }
            }

            addWarningLine($"BL616 {commandLabel} timeout setup failed; continuing with ROM default timeout.");
            return false;
        }

        bool tryRecoverTransferAfterTimeout(string operation, int addr)
        {
            addWarningLine($"{operation} recovery at 0x{addr:X6}: re-syncing serial stream...");
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            Thread.Sleep(30);

            if(!internalSync())
            {
                addWarningLine($"{operation} recovery sync failed.");
                return false;
            }

            if(!setBL616FlashParameters())
            {
                addWarningLine($"{operation} recovery flash_set_para failed.");
                return false;
            }

            return true;
        }

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);

            int initialBootBaudrate = getInitialBootBaudrate();
            addLog("Going to open port: " + serialName + $" at boot baud {initialBootBaudrate} (requested transfer baud {baudrate})." + Environment.NewLine);
            if(serial == null)
            {
                serial = new SerialPort(serialName, initialBootBaudrate);
                configureSerialPortHostBuffers(serial);
                serial.Open();
            }
            else
            {
                setSerialBaudrate(initialBootBaudrate, "ROM/boot");
            }

            serial.ReadTimeout = 5000;
            serial.WriteTimeout = 5000;
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            addLog("Port ready!" + Environment.NewLine);

            if(this.Sync() == false)
            {
                return false;
            }
            if(this.getAndPrintInfo() == null)
            {
                addErrorLine("Initial get info failed.");
                addErrorLine("This may happen if you don't reset between flash operations.");
                addErrorLine("Please make sure BOOT is held high, reset (or power-cycle), and try again.");
                return false;
            }

            if(blinfo?.Variant != BKType.BL616)
            {
                addErrorLine($"Selected chip type is {chipType}, but current chip type is {blinfo?.Variant}. BL616 flasher will stop.");
                return false;
            }

            // BLDC sets BL616 bootrom UART timeout to 10s before clk_set.
            // This improves stability at higher post-handshake baudrates.
            setBL616BootromUartTimeout();

            int operationBaudrate = getEflashBaudrate(blinfo.Variant);
            if(!setBL616ClockAndBaud(operationBaudrate))
            {
                return false;
            }
            if(!setBL616FlashParameters())
            {
                return false;
            }

            flashID = readFlashID();
            if(flashID == null)
            {
                addWarningLine("Flash ID read failed, retrying BL616 flash parameter setup once...");
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                if(setBL616FlashParameters())
                {
                    flashID = readFlashID();
                }
            }

            if(flashID == null)
            {
                addErrorLine("Failed to get BL616 flash ID.");
                return false;
            }

            return true;
        }
        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            try
            {
                if(doGenericSetup() == false)
                {
                    return;
                }
                if(fullRead) sectors = (int)(flashSizeMB * 256);
                doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
            }
        }

        internal byte[] readFlash(int addr = 0, int amount = 4096)
        {
            try
            {
                var startAddr = addr;
                int startAmount = amount;
                byte[] ret = new byte[amount];
                logger.setProgress(0, startAmount);
                logger.setState("Reading", Color.White);
                addLogLine("Starting read...");
                int destAddr = 0;
                while(amount > 0)
                {
                    int length = BL_FLASH_READ_CHUNK;
                    if(amount < length)
                        length = amount;

                    addLog($"Reading at 0x{addr:X6}... ");
                    byte[] cmdBuffer = new byte[8];
                    cmdBuffer[0] = (byte)(addr & 0xFF);
                    cmdBuffer[1] = (byte)((addr >> 8) & 0xFF);
                    cmdBuffer[2] = (byte)((addr >> 16) & 0xFF);
                    cmdBuffer[3] = (byte)((addr >> 24) & 0xFF);
                    cmdBuffer[4] = (byte)(length & 0xFF);
                    cmdBuffer[5] = (byte)((length >> 8) & 0xFF);
                    cmdBuffer[6] = (byte)((length >> 16) & 0xFF);
                    cmdBuffer[7] = (byte)((length >> 24) & 0xFF);

                    byte[] result = null;
                    int dataLen = -1;
                    bool chunkOk = false;
                    for(int retry = 0; retry < BL616_READ_RETRY_COUNT; retry++)
                    {
                        float readTimeout = getSerialTransferTimeoutSeconds(length + 8, 5.0f);
                        result = this.executeCommand(0x32, cmdBuffer, 0, cmdBuffer.Length, true, readTimeout, 2, true);

                        if(result == null)
                        {
                            addWarningLine($"Read retry {retry + 1}/{BL616_READ_RETRY_COUNT} at 0x{addr:X6}: no reply");
                            serial.DiscardInBuffer();
                            Thread.Sleep(50);

                            if(retry + 1 < BL616_READ_RETRY_COUNT && !tryRecoverTransferAfterTimeout("Read", addr))
                            {
                                break;
                            }
                            continue;
                        }

                        dataLen = result.Length - 2;
                        if(dataLen == length)
                        {
                            chunkOk = true;
                            break;
                        }

                        addWarningLine($"Read retry {retry + 1}/{BL616_READ_RETRY_COUNT} at 0x{addr:X6}: size mismatch, expected {length}, got {dataLen}");
                        serial.DiscardInBuffer();
                        Thread.Sleep(50);
                        if(retry + 1 < BL616_READ_RETRY_COUNT && !tryRecoverTransferAfterTimeout("Read", addr))
                        {
                            break;
                        }
                    }

                    if(!chunkOk)
                    {
                        logger.setState("Read error", Color.Red);
                        addErrorLine($"Read fail at 0x{addr:X6} - size mismatch after retries");
                        return null;
                    }
                    Array.Copy(result, 2, ret, destAddr, dataLen);
                    cancellationToken.ThrowIfCancellationRequested();
                    addr += dataLen;
                    amount -= dataLen;
                    destAddr += dataLen;
                    logger.setProgress(destAddr, startAmount);
                }
                addLogLine("");
                if(!CheckSHA256(startAddr, startAmount, ret))
                {
                    logger.setState("SHA mismatch!", Color.Red);
                    return null;
                }
                logger.setState("Read done", Color.DarkGreen);
                addLogLine("Read complete!");
                return ret;
            }
            catch(Exception ex)
            { 
                addLogLine("");
                addErrorLine(ex.ToString());
                return null;
            }
        }

        public bool CheckSHA256(int addr, int length, byte[] data)
        {
            byte[] sha256cmd = new byte[8];
            sha256cmd[0] = (byte)(addr & 0xFF);
            sha256cmd[1] = (byte)((addr >> 8) & 0xFF);
            sha256cmd[2] = (byte)((addr >> 16) & 0xFF);
            sha256cmd[3] = (byte)((addr >> 24) & 0xFF);
            sha256cmd[4] = (byte)(length & 0xFF);
            sha256cmd[5] = (byte)((length >> 8) & 0xFF);
            sha256cmd[6] = (byte)((length >> 16) & 0xFF);
            sha256cmd[7] = (byte)((length >> 24) & 0xFF);
            float shaTimeout = Math.Max(30.0f, getSerialTransferTimeoutSeconds(64, 10.0f));
            byte[] sha256result = executeCommand(0x3D, sha256cmd, 0, sha256cmd.Length, true, shaTimeout, 2, true);
            if(sha256result == null)
            {
                addErrorLine($"Failed to get hash");
                return false;
            }
            string sha256read;
            using(var hasher = SHA256.Create())
            {
                var sha = hasher.ComputeHash(data);
                sha256read = HashToStr(sha);
            }
            var sha256flash = HashToStr(sha256result.Skip(2).ToArray());
            if(sha256flash != sha256read)
            {
                addErrorLine($"Hash mismatch!\r\nexpected\t{sha256read}\r\ngot\t{sha256flash}");
                return false;
            }
            else
            {
                addSuccess($"Hash matches {sha256read}!" + Environment.NewLine);
                return true;
            }
        }

        public bool doReadInternal(int addr = 0, int amount = 0x200000) {
            byte[] res = readFlash(addr, amount);
            if(res != null)
            {
                ms?.Dispose();
                ms = new MemoryStream(res);
            }
            return false;
        }
        MemoryStream ms;

        public BL616Flasher(CancellationToken ct) : base(ct)
        {
        }

        public override byte[] getReadResult()
        {
            return ms?.ToArray();
        }
        public override bool doErase(int startSector, int sectors, bool bAll = false)
        {
            logger.setState("Erasing...", Color.White);
            if(bAll)
            {
                if (doGenericSetup() == false)
                {
                    return false;
                }
                addLogLine("Erasing...");
                var res = executeCommand(0x3C, null, 0, 0, true, 30);
                if(res != null) logger.setState("Erase done", Color.DarkGreen);
                else
                {
                    logger.setState("Erase failed!", Color.Red);
                    return false;
                }
            }
            else
            {
                if(sectors < 1)
                    return false;
                var end = sectors * BK7231Flasher.SECTOR_SIZE;
                end += startSector - 1; //end addr
                addLogLine($"Erasing from 0x{startSector:X} to 0x{(end + 1):X}");
                byte[] cmdBuffer = new byte[8];
                cmdBuffer[0] = (byte)(startSector & 0xFF);
                cmdBuffer[1] = (byte)((startSector >> 8) & 0xFF);
                cmdBuffer[2] = (byte)((startSector >> 16) & 0xFF);
                cmdBuffer[3] = (byte)((startSector >> 24) & 0xFF);
                cmdBuffer[4] = (byte)(end & 0xFF);
                cmdBuffer[5] = (byte)((end >> 8) & 0xFF);
                cmdBuffer[6] = (byte)((end >> 16) & 0xFF);
                cmdBuffer[7] = (byte)((end >> 24) & 0xFF);
                var res = executeCommand(0x30, cmdBuffer, 0, cmdBuffer.Length, true, 30);
                if(res != null) logger.setState("Erase done", Color.DarkGreen);
                else
                {
                    logger.setState("Erase failed!", Color.Red);
                    return false;
                }
            }
            serial.DiscardInBuffer();
            return true;
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
            try
            {
                if(doGenericSetup() == false)
                {
                    return;
                }

                if(rwMode == WriteMode.OnlyOBKConfig)
                {
                    addErrorLine("OBK config write is not supported on BL616 yet.");
                    return;
                }

                if(rwMode == WriteMode.ReadAndWrite)
                {
                    if(sectors <= 0)
                    {
                        sectors = (int)(flashSizeMB * 256);
                    }
                    doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
                    if(ms == null)
                    {
                        return;
                    }
                    if(saveReadResult(startSector) == false)
                    {
                        return;
                    }
                }
                if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
                {
                    if(string.IsNullOrEmpty(sourceFileName))
                    {
                        addLogLine("No filename given!");
                        return;
                    }
                    addLogLine("Reading " + sourceFileName + "...");
                    byte[] data = File.ReadAllBytes(sourceFileName);
                    if(data == null || data.Length == 0)
                    {
                        addErrorLine("Selected file is empty.");
                        return;
                    }

                    int writeOffset = Math.Max(0, startSector);
                    if(sectors > 0)
                    {
                        int requestedRange = sectors * BK7231Flasher.SECTOR_SIZE;
                        if(data.Length > requestedRange)
                        {
                            addErrorLine($"Selected file size ({data.Length} bytes) exceeds write range ({requestedRange} bytes).");
                            return;
                        }
                    }

                    addLogLine($"Writing {data.Length} bytes at 0x{writeOffset:X}...");
                    if(!writeFlash(data, writeOffset, data.Length))
                    {
                        return;
                    }
                }
            }
            catch(Exception ex)
            {
                addErrorLine(ex.Message);
            }
            return;
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

