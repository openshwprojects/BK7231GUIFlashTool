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

        // Match Bouffalo DevCube eflash-loader transfer sizing more closely.
        // DevCube v1.9.0 uses tx_size=2056 in the BL602/BL702 eflash_loader config;
        // the flash write command payload includes a 4-byte address, leaving 2052 bytes of data.
        const int BL_FLASH_READ_CHUNK = 4096;
        const int BL_FLASH_WRITE_PAYLOAD_LIMIT = 2056;
        const int BL_FLASH_WRITE_DATA_CHUNK = BL_FLASH_WRITE_PAYLOAD_LIMIT - 4;
        const int BL_SYNC_PATTERN_LEN = 300;
        const int BL_SYNC_WAIT_MS = 1000;
        const int BL_ROM_BOOT_BAUD = 500000;
        const int BL_EFLASH_STARTUP_DELAY_MS = 300;
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

            // Bouffalo ROM ISP synchronisation is an auto-baud pattern of repeated 0x55 bytes.
            // The previous 16-byte pattern was marginal on BL60x/BL70x targets; DevCube/blflash
            // use a much longer train before waiting for the 0x4F 0x4B acknowledgement.
            byte[] syncRequest = Enumerable.Repeat((byte)'U', BL_SYNC_PATTERN_LEN).ToArray();
            serial.Write(syncRequest, 0, syncRequest.Length);

            byte[] response;
            if (TryReadExact(2, BL_SYNC_WAIT_MS, out response) && IsAck(response, "OK"))
            {
                return true;
            }

            return false;
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
            return rep != null && rep.Length == 2 && rep[0] == (byte)ack[0] && rep[1] == (byte)ack[1];
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
            // BLDC/DevCube keeps the ROM sync and RAM-loader upload phase conservative
            // for high transfer speeds, but low selected bauds must not be raised.
            // Upstream/old Easy Flasher could complete BL602 at 115200 by running the
            // ROM, loader upload and eflash phase at the selected low rate. Treat
            // 500000 as a ceiling for the ROM phase, not as a forced minimum.
            if(chipType == BKType.BL602 || chipType == BKType.BL702)
            {
                return Math.Min(baudrate, BL_ROM_BOOT_BAUD);
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
                        if (!TryReadExact(2, timeoutMS, out lenBytes))
                        {
                            addLogLine($"Command 0x{type:X2} timed out while reading length prefix; got {lenBytes.Length}/2 bytes");
                            return null;
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
                    int errCnt = 0;
                    float writeTimeout = getSerialTransferTimeoutSeconds(bufferLen + 4, 5.0f);
                    while(executeCommand(0x31, buffer, 0, bufferLen, true, writeTimeout) == null && errCnt < 10)
                        errCnt++;
                    if(errCnt >= 10)
                        throw new Exception("Write failed!");
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

        bool doGenericSetup()
        {
            addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
            addLog("Flasher mode: " + chipType + Environment.NewLine);
            int initialBootBaudrate = getInitialBootBaudrate();
            addLog("Going to open port: " + serialName + $" at boot baud {initialBootBaudrate} (requested transfer baud {baudrate})." + Environment.NewLine);
            if(serial == null)
            {
                serial = new SerialPort(serialName, initialBootBaudrate);
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
            if(blinfo.Variant == BKType.BL616)
            {
                executeCommand(0x3B, new byte[] { 0x80, 0x41, 0x01, 0x00 }, 0, 4, true, 2, 0);
                // do it twice for bl616
                flashID = readFlashID();
                flashID = readFlashID();
                if(flashID == null)
                {
                    addErrorLine("Failed to get BL616 flash id!");
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

            //resync in eflash
            if(this.Sync() == false)
            {
                return false;
            }
            flashID = readFlashID();
            if(flashID == null)
            {
                addErrorLine("Failed to get flash ID from eflash loader.");
                return false;
            }

            return true;
        }
        public bool loadAndRunPreprocessedImage()
        {
            // bl616 doesn't require flash loader, and already rom implements full protocol
            if(blinfo.Variant == BKType.BL616) return true;
            BKType loaderVariant = blinfo?.Variant ?? chipType;
            string loaderResource = loaderVariant switch
            {
                BKType.BL702 => BL702_EFLASH_LOADER_RESOURCE,
                _ => BL602_EFLASH_LOADER_RESOURCE,
            };
            addLogLine($"Using {loaderVariant} eflash loader resource: {loaderResource}");
            byte[] loaderBinary = FLoaders.GetBinaryFromAssembly(loaderResource);
            return loadAndRunPreprocessedImage(loaderBinary);
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
                    for(int retry = 0; retry < 3; retry++)
                    {
                        float readTimeout = getSerialTransferTimeoutSeconds(length + 8, 5.0f);
                        result = this.executeCommand(0x32, cmdBuffer, 0, cmdBuffer.Length, true, readTimeout, 2, true);

                        if(result == null)
                        {
                            addWarningLine($"Read retry {retry + 1}/3 at 0x{addr:X6}: no reply");
                            serial.DiscardInBuffer();
                            Thread.Sleep(50);
                            continue;
                        }

                        dataLen = result.Length - 2;
                        if(dataLen == length)
                        {
                            chunkOk = true;
                            break;
                        }

                        addWarningLine($"Read retry {retry + 1}/3 at 0x{addr:X6}: size mismatch, expected {length}, got {dataLen}");
                        serial.DiscardInBuffer();
                        Thread.Sleep(50);
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

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            try
            {
                if(doGenericSetup() == false)
                {
                    return;
                }
                OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
                if(rwMode == WriteMode.ReadAndWrite)
                {
                    sectors = (int)(flashSizeMB * 256);
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
                if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
                {
                    if(chipType != BKType.BL602)
                    {
                        addErrorLine($"Config write is not supported on {chipType}");
                        return;
                    }
                    if(cfg != null)
                    {
                        var ptdata = chipType == BKType.BL702 ? readFlash(0x1000, 0x1000) : readFlash(0xE000, 0x1000);
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
                    else
                    {
                        addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
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
