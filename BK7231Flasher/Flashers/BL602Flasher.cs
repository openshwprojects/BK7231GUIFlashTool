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
    public class BL602Flasher : BaseFlasher
    {
        //int timeoutMs = 10000;
        float flashSizeMB = 2;
        byte[] flashID;
        BLInfo blinfo = null;
        int bl616ActiveFlashPin = BL616_FLASH_PIN_FROM_BOOTINFO;

        // BLDC/DevCube eflash-loader config uses tx_size=2056. In bflb_mcu_tool,
        // flash read/write payload slicing uses tx_size - 8, i.e. 2048 bytes.
        const int BL_FLASH_TX_SIZE = 2056;
        const int BL_FLASH_CMD_OVERHEAD = 8;
        const int BL_FLASH_READ_CHUNK = BL_FLASH_TX_SIZE - BL_FLASH_CMD_OVERHEAD;
        const int BL_FLASH_WRITE_DATA_CHUNK = BL_FLASH_TX_SIZE - BL_FLASH_CMD_OVERHEAD;
        const int BL_SYNC_WAIT_MS = 1000;
        const double BL602_SYNC_BURST_SECONDS = 0.006;
        const double BL702_SYNC_BURST_SECONDS = 0.003;
        const int BL_ROM_BOOT_BAUD = 500000;
        const int BL_EFLASH_STARTUP_DELAY_MS = 300;
        const int BL616_CLK_SET_TIMEOUT_MS = 2000;
        const int BL616_COMMAND_RETRY_COUNT = 3;
        const int BL616_READ_RETRY_COUNT = 3;
        const int BL616_WRITE_RETRY_COUNT = 10;
        const int BL616_BOOTROM_UART_TIMEOUT_MS = 10000;
        const uint BL616_BOOTROM_TIMEOUT_REGISTER = 0x6102DF04;
        const uint BL616_BOOTROM_TIMEOUT_VALUE_A0 = 0x27101200;
        const byte BL616_BOOT_INFO_A0_MARKER = 0x01;
        const int BL616_FLASH_CLOCK_CFG = 0x41;
        const int BL616_FLASH_IO_MODE = 0x01;
        const int BL616_FLASH_CLOCK_DELAY = 0x00;
        const byte BL616_FLASH_PIN_FROM_BOOTINFO = 0x80;
        const string BL602_EFLASH_LOADER_RESOURCE = "BL602Floader_40m";
        const string BL702_EFLASH_LOADER_RESOURCE = "BL702Floader_32m";

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
                        addLogLine($"Otherwise, please pull high BOOT/{(chipType == BKType.BL602 ? "IO8" : "IO28")} and reset.");
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

            // BLDC uses a baud-scaled sync burst: roughly 6ms worth of 0x55 for BL602
            // and 3ms for BL702. A fixed 300-byte burst works near 500k but can be too
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

            BKType syncVariant = blinfo?.Variant ?? chipType;
            double burstSeconds = syncVariant == BKType.BL702 ? BL702_SYNC_BURST_SECONDS : BL602_SYNC_BURST_SECONDS;
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
                // BL702 ROM version is 0x7020001. BL616 - 0x6160001
                // unknown if BL704/706 and BL618 have different rom version.
                if(bootrom.StartsWith("702") || bootrom.StartsWith("704") || bootrom.StartsWith("706"))
                {
                    Variant = BKType.BL702;
                }
                else if(bootrom.StartsWith("616") || bootrom.StartsWith("618"))
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

            bool lenientAckAllowed = (blinfo?.Variant == BKType.BL616) || chipType == BKType.BL616;

            // Bouffalo UART handler accepts partial OK/PD pairs when one byte survives.
            // Keep this behaviour for BL616/BL618 high-baud robustness without changing
            // BL602/BL702 strict ACK matching.
            if(ack == "OK")
            {
                if(lenientAckAllowed)
                {
                    return rep[0] == 0x4F || rep[1] == 0x4F || rep[0] == 0x4B || rep[1] == 0x4B;
                }
                return rep[0] == (byte)'O' && rep[1] == (byte)'K';
            }
            if(ack == "PD")
            {
                if(lenientAckAllowed)
                {
                    return rep[0] == 0x50 || rep[1] == 0x50 || rep[0] == 0x44 || rep[1] == 0x44;
                }
                return rep[0] == (byte)'P' && rep[1] == (byte)'D';
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
            // BLDC/DevCube v1.9.0 uses speed_uart_boot=500000 for BL602/BL702 loader
            // flow (load_function=1) and BL616/BL618 bootrom flow (load_function=2).
            if(chipType == BKType.BL602 || chipType == BKType.BL702 || chipType == BKType.BL616)
            {
                return BL_ROM_BOOT_BAUD;
            }
            return baudrate;
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
                // Match BLDC's larger UART queue behaviour to reduce short-read
                // events at higher transfer baud rates.
                int queueSize = Math.Max(8192, BL_FLASH_TX_SIZE * 16);
                port.ReadBufferSize = queueSize;
                port.WriteBufferSize = queueSize;
            }
            catch(InvalidOperationException)
            {
                // Buffer sizing is best-effort and must happen before opening.
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

        bool tryAttachToExistingEflashLoader(int initialBootBaudrate)
        {
            if(chipType == BKType.BL616)
            {
                // BL616/BL618 uses bootrom load_function=2 flow, not the RAM eflash-loader probe path.
                return false;
            }

            // If a previous operation has already jumped into the RAM eflash loader,
            // the ROM sync/get-info path can fail. Try the current baud first, then
            // the expected post-loader baud so same-session reuse still works after
            // the ROM and eflash-loader phases have been split by baud rate.
            flashID = readFlashID();
            if(flashID != null)
            {
                addLogLine("Eflash loader is already uploaded!");
                return true;
            }

            int expectedEflashBaudrate = getEflashBaudrate(chipType);
            if(expectedEflashBaudrate == serial.BaudRate)
            {
                return false;
            }

            addLogLine($"BootROM get-info failed. Probing for existing {chipType} eflash-loader at {expectedEflashBaudrate}...");
            setSerialBaudrate(expectedEflashBaudrate, $"{chipType} existing eflash-loader probe");
            if(this.Sync())
            {
                flashID = readFlashID();
                if(flashID != null)
                {
                    addLogLine("Eflash loader is already uploaded!");
                    return true;
                }
            }

            setSerialBaudrate(initialBootBaudrate, "ROM/boot");
            return false;
        }

        byte[] executeCommand(int type, byte[] parms = null,
            int start = 0, int len = 0, bool bChecksum = false,
            float timeout = 0.1f, int expectedReplyLen = 2,
            bool lengthPrefixedPayload = false)
        {
            if(len < 0)
            {
                len = parms.Length;
            }

            byte checksumOrDummy = 1;
            if(bChecksum)
            {
                checksumOrDummy = 0;
                checksumOrDummy += (byte)(len & 0xFF);
                checksumOrDummy += (byte)(len >> 8);
                for(int i = 0; i < len; i++)
                {
                    checksumOrDummy += parms[start + i];
                }
                checksumOrDummy = (byte)(checksumOrDummy & 0xFF);
            }

            return executeCommandWithHeader(type, checksumOrDummy, parms, start, len, timeout, expectedReplyLen, lengthPrefixedPayload);
        }

        byte[] executeBootromCommand(int type, byte[] parms = null,
            int start = 0, int len = 0,
            float timeout = 0.1f, int expectedReplyLen = 2,
            bool lengthPrefixedPayload = false)
        {
            if(len < 0)
            {
                len = parms.Length;
            }

            // BootROM command frame in BLDC img_loader:
            // [cmd][dummy=0x00][len_l][len_h][payload...]
            return executeCommandWithHeader(type, 0x00, parms, start, len, timeout, expectedReplyLen, lengthPrefixedPayload);
        }

        byte[] executeCommandWithHeader(int type, byte headerByte, byte[] parms,
            int start, int len, float timeout, int expectedReplyLen, bool lengthPrefixedPayload)
        {
            var raw = new byte[] { (byte)type, headerByte, (byte)(len & 0xFF), (byte)(len >> 8) };
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            serial.Write(raw, 0, raw.Length);
            if(parms != null && len > 0)
            {
                serial.Write(parms, start, len);
            }

            if(expectedReplyLen == 0)
            {
                return Array.Empty<byte>();
            }

            int timeoutMS = Math.Max(1, (int)(timeout * 1000));
            Stopwatch sw = Stopwatch.StartNew();
            bool pendingLogged = false;

            while(sw.ElapsedMilliseconds < timeoutMS)
            {
                int remainingMs = Math.Max(1, timeoutMS - (int)sw.ElapsedMilliseconds);
                byte[] rep;
                if(!TryReadExact(2, remainingMs, out rep))
                {
                    break;
                }

                if(IsAck(rep, "OK"))
                {
                    if(lengthPrefixedPayload)
                    {
                        byte[] lenBytes;
                        int repeatedOkCount = 0;
                        while(true)
                        {
                            if(!TryReadExact(2, timeoutMS, out lenBytes))
                            {
                                addLogLine($"Command 0x{type:X2} timed out while reading length prefix; got {lenBytes.Length}/2 bytes");
                                return null;
                            }

                            // Bouffalo's if_deal_response() consumes any extra OK pair before
                            // the 2-byte little-endian response length. Keep that behaviour so
                            // response parsing stays compatible with BLDC/bflb_iot_tool.
                            if(!IsAck(lenBytes, "OK"))
                            {
                                break;
                            }

                            repeatedOkCount++;
                            if(repeatedOkCount > 4)
                            {
                                addLogLine($"Command 0x{type:X2} received repeated OK while waiting for length prefix");
                                return null;
                            }
                        }

                        int payloadLen = lenBytes[0] | (lenBytes[1] << 8);
                        byte[] payload;
                        if(!TryReadExact(payloadLen, timeoutMS, out payload))
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
                    if(payloadBytes == 0)
                    {
                        return Array.Empty<byte>();
                    }

                    byte[] payloadFixed;
                    if(!TryReadExact(payloadBytes, timeoutMS, out payloadFixed))
                    {
                        addLogLine($"Command 0x{type:X2} timed out while reading fixed payload; got {payloadFixed.Length}/{payloadBytes} bytes");
                        return null;
                    }
                    return payloadFixed;
                }

                if(IsAck(rep, "FL"))
                {
                    addLogLine($"Command 0x{type:X2} failed!");
                    return null;
                }

                if(IsAck(rep, "PD"))
                {
                    if(!pendingLogged)
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

            if(expectedReplyLen != 0)
            {
                addLogLine($"Command 0x{type:X2} timed out!");
            }
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

                    bool isBL616Flow = blinfo?.Variant == BKType.BL616;
                    int maxRetries = isBL616Flow ? BL616_WRITE_RETRY_COUNT : 10;
                    bool chunkWritten = false;
                    for(int retry = 0; retry < maxRetries; retry++)
                    {
                        if(executeCommand(0x31, buffer, 0, bufferLen, true, writeTimeout) != null)
                        {
                            chunkWritten = true;
                            break;
                        }

                        addWarningLine($"Write retry {retry + 1}/{maxRetries} at 0x{adr:X6}: no reply");
                        serial.DiscardInBuffer();
                        Thread.Sleep(50);

                        if(isBL616Flow && retry + 1 < maxRetries && !tryRecoverTransferAfterTimeout("Write", adr))
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

        int getBL616FlashPinFromBootInfo()
        {
            // Factory tool extracts flash pin config from bootinfo sw_usage_data0
            // (bootinfo bytes 4..7, little-endian), then takes bits [19:14].
            if(blinfo?.remaining == null || blinfo.remaining.Length < 8)
            {
                return BL616_FLASH_PIN_FROM_BOOTINFO;
            }

            uint swUsageData = (uint)(blinfo.remaining[4]
                | (blinfo.remaining[5] << 8)
                | (blinfo.remaining[6] << 16)
                | (blinfo.remaining[7] << 24));
            int flashPin = (int)((swUsageData >> 14) & 0x3F);
            return flashPin;
        }

        byte[] buildBL616FlashSetParaPayload(int flashPin)
        {
            uint flashSet = (uint)(flashPin
                | (BL616_FLASH_CLOCK_CFG << 8)
                | (BL616_FLASH_IO_MODE << 16)
                | (BL616_FLASH_CLOCK_DELAY << 24));

            byte[] payload = BitConverter.GetBytes(flashSet);
            addLogLine($"BL616 flash_set_para: pin=0x{flashPin:X2}, set=0x{flashSet:X8}");
            return payload;
        }

        bool setBL616ClockAndBaud(int targetBaudrate)
        {
            // BLDC load_function=2 sends clk_set (0x22) in bootrom context, then
            // reopens host UART at speed_uart_load without another sync.
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

        bool setBL616FlashParameters(int flashPin)
        {
            byte[] flashSetPayload = buildBL616FlashSetParaPayload(flashPin);
            for(int attempt = 1; attempt <= BL616_COMMAND_RETRY_COUNT; attempt++)
            {
                if(executeCommand(0x3B, flashSetPayload, 0, flashSetPayload.Length, true, 2.0f) != null)
                {
                    bl616ActiveFlashPin = flashPin;
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
                if(executeBootromCommand(command, payload, 0, payload.Length, 2.0f) != null)
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

            if(!setBL616FlashParameters(bl616ActiveFlashPin))
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
                if(tryAttachToExistingEflashLoader(initialBootBaudrate))
                {
                    return true;
                }

                return false;
            }
            if (this.getAndPrintInfo() == null)
            {
                if(tryAttachToExistingEflashLoader(initialBootBaudrate))
                {
                    return true;
                }
                addErrorLine("Initial get info failed.");
                addErrorLine("This may happen if you don't reset between flash operations");
                addErrorLine("So, make sure that BOOT is connected, do reset (or power off/on) and try again");
                return false;
            }
            if(chipType == BKType.BL616 && blinfo?.Variant != BKType.BL616)
            {
                addErrorLine($"Selected chip type is {chipType}, but current chip type is {blinfo?.Variant}. BL616 flasher path will stop.");
                return false;
            }
            if(blinfo.Variant == BKType.BL616)
            {
                // BLDC load_function=2 path for BL616/BL618:
                // set timeout -> clk_set -> flash_set_para -> read jedec id.
                setBL616BootromUartTimeout();

                int operationBaudrate = getEflashBaudrate(blinfo.Variant);
                if(!setBL616ClockAndBaud(operationBaudrate))
                {
                    return false;
                }

                int bootInfoFlashPin = getBL616FlashPinFromBootInfo();
                bl616ActiveFlashPin = bootInfoFlashPin;
                if(!setBL616FlashParameters(bootInfoFlashPin))
                {
                    return false;
                }

                flashID = readFlashID();
                if(flashID == null)
                {
                    addWarningLine("Flash ID read failed after bootinfo-derived flash_set_para.");
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();

                    // Some BL616/BL618 variants appear to expose bootinfo bits that do not
                    // map to a working flash pin value for flash_set_para. Preserve the
                    // legacy-safe 0x80 path as fallback when JEDEC read fails.
                    if(bootInfoFlashPin != BL616_FLASH_PIN_FROM_BOOTINFO)
                    {
                        addWarningLine($"Retrying BL616 flash parameter setup with fallback pin 0x{BL616_FLASH_PIN_FROM_BOOTINFO:X2}...");
                        if(setBL616FlashParameters(BL616_FLASH_PIN_FROM_BOOTINFO))
                        {
                            flashID = readFlashID();
                        }
                    }

                    if(flashID == null)
                    {
                        addWarningLine("Flash ID still unavailable, retrying with current flash_set_para once...");
                        serial.DiscardInBuffer();
                        serial.DiscardOutBuffer();
                        if(setBL616FlashParameters(bl616ActiveFlashPin))
                        {
                            flashID = readFlashID();
                        }
                    }
                }

                if(flashID == null)
                {
                    addErrorLine("Failed to get BL616 flash ID.");
                    return false;
                }

                return true;
            }
            else if(blinfo.Variant != chipType)
            {
                addErrorLine($"Selected chip type is {chipType}, but current chip type is {blinfo.Variant}! Will continue anyway...");
            }
            this.loadAndRunPreprocessedImage();
            Thread.Sleep(BL_EFLASH_STARTUP_DELAY_MS);

            int eflashBaudrate = getEflashBaudrate(blinfo.Variant);
            setSerialBaudrate(eflashBaudrate, $"{blinfo.Variant} eflash-loader");

            // DevCube load_function=1 does not send clk_set here. It opens the host
            // side at speed_uart_load and performs a fresh 0x55 auto-baud handshake
            // with the RAM eflash loader before issuing flash commands.
            if(this.Sync() == false)
            {
                return false;
            }

            flashID = readFlashID();
            if(flashID == null)
            {
                addWarningLine("Flash ID read failed, re-syncing eflash loader and retrying once...");
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                if(this.Sync())
                {
                    flashID = readFlashID();
                }
                if(flashID == null)
                {
                    addErrorLine("Failed to get flash ID from eflash loader.");
                    return false;
                }
            }

            return true;
        }
        public bool loadAndRunPreprocessedImage()
        {
            // bl616 doesn't require flash loader, and already rom implements full protocol
            if(blinfo.Variant == BKType.BL616) return true;
            BKType loaderVariant = blinfo?.Variant ?? chipType;
            string loaderResource = getLoaderResourceForVariant(loaderVariant);
            addLogLine($"Using {loaderVariant} eflash loader resource: {loaderResource}");
            byte[] loaderBinary = FLoaders.GetBinaryFromAssembly(loaderResource);
            return loadAndRunPreprocessedImage(loaderBinary);
        }

        string getLoaderResourceForVariant(BKType loaderVariant)
        {
            if(loaderVariant == BKType.BL702)
            {
                return BL702_EFLASH_LOADER_RESOURCE;
            }

            return BL602_EFLASH_LOADER_RESOURCE;
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
                    bool isBL616Flow = blinfo?.Variant == BKType.BL616;
                    int maxRetries = isBL616Flow ? BL616_READ_RETRY_COUNT : 3;
                    for(int retry = 0; retry < maxRetries; retry++)
                    {
                        float readTimeout = getSerialTransferTimeoutSeconds(length + 8, 5.0f);
                        result = this.executeCommand(0x32, cmdBuffer, 0, cmdBuffer.Length, true, readTimeout, 2, true);

                        if(result == null)
                        {
                            addWarningLine($"Read retry {retry + 1}/{maxRetries} at 0x{addr:X6}: no reply");
                            serial.DiscardInBuffer();
                            Thread.Sleep(50);

                            if(isBL616Flow && retry + 1 < maxRetries && !tryRecoverTransferAfterTimeout("Read", addr))
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

                        addWarningLine($"Read retry {retry + 1}/{maxRetries} at 0x{addr:X6}: size mismatch, expected {length}, got {dataLen}");
                        serial.DiscardInBuffer();
                        Thread.Sleep(50);
                        if(isBL616Flow && retry + 1 < maxRetries && !tryRecoverTransferAfterTimeout("Read", addr))
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

        public BL602Flasher(CancellationToken ct) : base(ct)
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

        bool writeBL616ImageWithRangeValidation(int startSector, int sectors, string sourceFileName)
        {
            if(string.IsNullOrEmpty(sourceFileName))
            {
                addLogLine("No filename given!");
                return false;
            }
            addLogLine("Reading " + sourceFileName + "...");
            byte[] data = File.ReadAllBytes(sourceFileName);
            if(data == null || data.Length == 0)
            {
                addErrorLine("Selected file is empty.");
                return false;
            }

            int writeOffset = Math.Max(0, startSector);
            int detectedFlashBytes = (int)(flashSizeMB * 1024 * 1024);
            if(detectedFlashBytes <= 0)
            {
                detectedFlashBytes = BK7231Flasher.FLASH_SIZE;
            }

            long maxWritableFromOffset = (long)detectedFlashBytes - writeOffset;
            if(maxWritableFromOffset <= 0)
            {
                addErrorLine($"Write offset 0x{writeOffset:X} is outside detected flash size ({detectedFlashBytes} bytes).");
                return false;
            }
            if(data.LongLength > maxWritableFromOffset)
            {
                addErrorLine(
                    $"Selected file size ({data.Length} bytes) does not fit at 0x{writeOffset:X}. " +
                    $"Available from offset: {maxWritableFromOffset} bytes (detected flash size {detectedFlashBytes} bytes).");
                return false;
            }

            if(sectors > 0)
            {
                long requestedRange = (long)sectors * BK7231Flasher.SECTOR_SIZE;
                bool looksLikeLegacyFullFlashRange =
                    writeOffset == 0 &&
                    requestedRange == BK7231Flasher.FLASH_SIZE &&
                    detectedFlashBytes > BK7231Flasher.FLASH_SIZE &&
                    data.LongLength <= detectedFlashBytes;

                if(data.LongLength > requestedRange)
                {
                    if(looksLikeLegacyFullFlashRange)
                    {
                        addWarningLine(
                            $"Requested write range ({requestedRange} bytes) matches legacy default size. " +
                            $"Using detected BL616 flash size ({detectedFlashBytes} bytes).");
                    }
                    else
                    {
                        addErrorLine($"Selected file size ({data.Length} bytes) exceeds write range ({requestedRange} bytes).");
                        return false;
                    }
                }
            }

            addLogLine($"Writing {data.Length} bytes at 0x{writeOffset:X}...");
            return writeFlash(data, writeOffset, data.Length);
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            try
            {
                if(doGenericSetup() == false)
                {
                    return;
                }
                bool isBL616Flow = blinfo?.Variant == BKType.BL616;
                OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
                if(rwMode == WriteMode.ReadAndWrite)
                {
                    if(isBL616Flow)
                    {
                        if(sectors <= 0)
                        {
                            sectors = (int)(flashSizeMB * 256);
                        }
                    }
                    else
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
                    if(isBL616Flow)
                    {
                        if(!writeBL616ImageWithRangeValidation(startSector, sectors, sourceFileName))
                            return;
                    }
                    else
                    {
                        if(string.IsNullOrEmpty(sourceFileName))
                        {
                            addLogLine("No filename given!");
                            return;
                        }
                        addLogLine("Reading " + sourceFileName + "...");
                        byte[] data = File.ReadAllBytes(sourceFileName);
                        List<PartitionEntry> partitions;
                        switch(flashSizeMB)
                        {
                            case 0.5f:
                                if(chipType == BKType.BL602)
                                    throw new Exception("Flash is too small!");
                                partitions = Partitions_512K_BL702;
                                break;
                            case 1f:
                                if(chipType == BKType.BL702)
                                    partitions = Partitions_1MB_BL702;
                                else
                                    partitions = Partitions_1MB;
                                break;
                            case 2f:
                                if(chipType == BKType.BL702)
                                    partitions = Partitions_2MB_BL702;
                                else
                                    partitions = Partitions_2MB;
                                break;
                            default:
                                partitions = Partitions_4MB;
                                if(chipType == BKType.BL702)
                                    partitions.First(x => x.Name == "FW").Address0 = 0x3000;
                                break;
                        }
                        if(data[0] == 0x42 && data[1] == 0x46 && data[2] == 0x4e && data[3] == 0x50)
                        {
                            writeFlash(data, 0);
                        }
                        else if(data.Length > partitions.First(x => x.Name == "FW").Length0)
                        {
                            throw new Exception("The size of the selected file exceeds the length of the partition!");
                        }
                        else
                        {
                            uint flash = (uint)(flashID[2] << 16 | flashID[3] << 8 | flashID[4]);
                            BL602FlashList.FlashConfig flashConfig;
                            try
                            {
                                flashConfig = BL602FlashList.FlashDict.First(x => x.Key == flash).Value;
                            }
                            catch
                            {
                                addErrorLine($"There is no flash config for flash with id: 0x{flash:X}. Will use for 0xEF4015. This might result in unknown behvaiour.");
                                flashConfig = BL602FlashList.FlashDict.First(x => x.Key == 0xEF4015).Value;
                            }
                            if(chipType != BKType.BL602 && chipType != BKType.BL702)
                            {
                                addErrorLine($"Firmware write is not supported on {chipType}.");
                                return;
                            }
                            var apphdr = CreateBootHeader(flashConfig, data, chipType);
                            if(chipType == BKType.BL602) data = data.Concat(new byte[] { 0, 0, 0, 0 }).ToArray();
                            byte[] wd = MiscUtils.padArray(apphdr, BK7231Flasher.SECTOR_SIZE);
                            data = wd.Concat(data).ToArray();
                            addLogLine("Writing...");
                            if(chipType == BKType.BL702)
                            {
                                var bpartitions = PT_Build(partitions);
                                var fdata = wd.Concat(bpartitions).Concat(data).ToArray();
                                if(!writeFlash(fdata, 0x0))
                                    return;
                            }
                            else
                            {
                                var boot = FLoaders.GetBinaryFromAssembly("BL602_Boot");
                                var boothdr = CreateBootHeader(flashConfig, boot, chipType);
                                boothdr = MiscUtils.padArray(boothdr, BK7231Flasher.SECTOR_SIZE);
                                boot = boothdr.Concat(boot).ToArray();
                                var bpartitions = PT_Build(partitions);
                                boot = MiscUtils.padArray(boot, 0xE000);
                                boot = boot.Concat(bpartitions).ToArray();
                                var fdata = boot.Concat(data).ToArray();
                                if(!writeFlash(fdata, 0x0))
                                    return;
                            }

                            if(chipType == BKType.BL602/*bOverwriteBootloader*/)
                            {
                                addLogLine("Writing dts...");
                                byte[] dts = FLoaders.GetBinaryFromAssembly("BL602_Dts");
                                if(!writeFlash(dts, (int)partitions.First(x => x.Name == "factory").Address0))
                                    return;
                            }
                        }
                    }
                }
                if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig)
                {
                    if(cfg == null)
                    {
                        addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
                    }
                    else if(chipType != BKType.BL602 && chipType != BKType.BL616)
                    {
                        addErrorLine($"OBK config write is not supported on {chipType}. Regular firmware write is still supported.");
                        return;
                    }
                    else
                    {
                        var ptdata = readFlash(0xE000, 0x1000);
                        var partition = PT_Parse(ptdata).First(x => x.Name == "PSM");
                        var offset = (int)partition.Address0;
                        var areaSize = (int)partition.Length0;
                        cfg.saveConfig(chipType);
                        var cfgData = cfg.getData();
                        byte[] efdata;
                        if(cfg.efdata != null)
                        {
                            try
                            {
                                efdata = EasyFlash.SaveValueToExistingEasyFlash("mY0bcFg", cfg.efdata, cfgData, areaSize, chipType);
                            }
                            catch(Exception ex)
                            {
                                addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
                                addLog(ex.Message + Environment.NewLine);
                                efdata = EasyFlash.SaveValueToNewEasyFlash("mY0bcFg", cfgData, areaSize, chipType);
                            }
                        }
                        else
                        {
                            efdata = EasyFlash.SaveValueToNewEasyFlash("mY0bcFg", cfgData, areaSize, chipType);
                        }
                        addLog("Now will also write OBK config..." + Environment.NewLine);
                        addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
                        addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
                        addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
                        bool bOk = writeFlash(efdata, offset, areaSize);
                        if(bOk == false)
                        {
                            logger.setState("Writing error!", Color.Red);
                            addError("Writing OBK config data to chip failed." + Environment.NewLine);
                            return;
                        }
                        logger.setState("OBK config write success!", Color.Green);
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
