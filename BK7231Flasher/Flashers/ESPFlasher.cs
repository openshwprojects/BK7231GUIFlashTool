using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace BK7231Flasher
{
    public class ESPFlasher : BaseFlasher
    {
        public enum ESPCommand : byte
        {
            FLASH_BEGIN = 0x02,
            FLASH_DATA = 0x03,
            FLASH_END = 0x04,
            MEM_BEGIN = 0x05,
            MEM_END = 0x06,
            MEM_DATA = 0x07,
            SYNC = 0x08,
            WRITE_REG = 0x09,
            READ_REG = 0x0A,
            SPI_SET_PARAMS = 0x0B,
            SPI_ATTACH = 0x0D,
            READ_FLASH_SLOW = 0x0E,
            CHANGE_BAUDRATE = 0x0F,
            SPI_FLASH_MD5 = 0x13,
            GET_SECURITY_INFO = 0x14,
            ERASE_FLASH = 0xD0,
            READ_FLASH = 0xD2,
        }

        const byte SLIP_END = 0xC0;
        const byte SLIP_ESC = 0xDB;
        const byte SLIP_ESC_END = 0xDC;
        const byte SLIP_ESC_ESC = 0xDD;

        // ESP32 stub flasher binary (from esptool v1 JSON)
        const int ESP_RAM_BLOCK = 0x1800; // Must match esptool default

        bool isStub = false;
        byte[] _slipBuf = new byte[4096];
        public bool LegacyMode { get; set; } = false;

        public ESPFlasher(CancellationToken ct) : base(ct)
        {
        }

        MemoryStream ms;

        public override byte[] getReadResult()
        {
            if (ms == null)
                return null;
            return ms.ToArray();
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
            logger.onReadResultQIOSaved(dat, null, fullPath);
            return true;
        }

        public override bool saveReadResult(int startOffset)
        {
            string typeStr = startOffset.ToString("X");
            string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType + "_" + typeStr, backupName, "bin");
            return saveReadResult(fileName);
        }

        static Dictionary<uint, string> ChipMagicValues = new Dictionary<uint, string>()
        {
            { 0x00F01D83, "ESP32" },
            { 0x000007C6, "ESP32-S2" },
            { 0xFFF0C101, "ESP8266" },
        };

        static Dictionary<uint, string> ChipIDs = new Dictionary<uint, string>()
        {
            { 0, "ESP32" },
            { 2, "ESP32-S2" },
            { 5, "ESP32-C3" },
            { 9, "ESP32-S3" },
            { 12, "ESP32-C2" },
            { 13, "ESP32-C6" },
            { 16, "ESP32-H2" },
            { 18, "ESP32-P4" },
        };


        bool openPort()
        {
            try
            {
                if (serial == null)
                {
                    // ESP32 ROM bootloader always starts at 115200.
                    // We sync at 115200, then change baud after stub upload.
                    serial = new SerialPort(serialName, 115200);
                    serial.ReadBufferSize = 32768;
                    serial.Open();
                }
                else
                {
                    // Reset baud to 115200 for ROM bootloader sync
                    // (port may still be at a higher baud from a previous operation)
                    serial.BaudRate = 115200;
                }
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                return true;
            }
            catch (Exception ex)
            {
                addError("Failed to open serial port: " + ex.Message);
                return false;
            }
        }

        public bool Sync()
        {
            byte[] syncData = new byte[36];
            syncData[0] = 0x07;
            syncData[1] = 0x07;
            syncData[2] = 0x12;
            syncData[3] = 0x20;
            for (int i = 4; i < 36; i++) syncData[i] = 0x55;

            for (int i = 0; i < 20; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // send sync
                    sendCommand(ESPCommand.SYNC, syncData);
                    
                    // read response
                    var resp = readPacket(300, ESPCommand.SYNC);
                    if (resp != null)
                    {
                        // esptool drains 7 additional SYNC responses after the first
                        for (int j = 0; j < 7; j++)
                        {
                            try
                            {
                                readPacket(100, ESPCommand.SYNC);
                            }
                            catch { }
                        }
                        return true;
                    }
                }
                catch (Exception)
                {
                    // ignore timeout
                }
                Thread.Sleep(50);
                addLog(".");
            }
            return false;
        }

        public uint? GetChipId()
        {
            // Try GET_SECURITY_INFO first (ESP32-C3 and later)
            try
            {
                addLogLine("Trying GET_SECURITY_INFO...");
                sendCommand(ESPCommand.GET_SECURITY_INFO, new byte[0]);
                var resp = readPacket(1000, ESPCommand.GET_SECURITY_INFO);
                if(resp != null && resp.Length >= 20) 
                {
                     // Response format: flags(4), flash_crypt_cnt(1), key_purposes(7), chip_id(4), ...
                     // Chip ID is at offset 4+1+7 = 12
                     uint chipId = BitConverter.ToUInt32(resp, 12);
                     string chipName = "Unknown";
                     if(ChipIDs.ContainsKey(chipId)) chipName = ChipIDs[chipId];
                     
                     addLogLine($"Got Chip ID from Security Info: {chipId} ({chipName})");
                     return chipId;
                }
                else if (resp != null) {
                    addLogLine($"GET_SECURITY_INFO returned {resp.Length} bytes.");
                    if(resp.Length == 4)
                         addLogLine($"Value: {BitConverter.ToUInt32(resp, 0):X}");
                }
            }
            catch(Exception ex)
            {
                addLogLine("GET_SECURITY_INFO failed: " + ex.Message);
            }

            // Fallback: Read Magic Register 0x40001000
            try 
            {
                addLogLine("Trying ReadReg 0x40001000...");
                uint val = ReadReg(0x40001000);
                string chipName = "Unknown";
                if(ChipMagicValues.ContainsKey(val)) chipName = ChipMagicValues[val];
                
                addLogLine($"Read Magic Reg 0x40001000: {val:X} ({chipName})");
                if (val == 0) addLogLine("Warning: Read 0, this might indicate read failure or unsupported register.");
                return val;
            }
            catch(Exception ex)
            {
                addErrorLine("Failed to read chip magic: " + ex.Message);
            }
            return null;
        }

        public uint ReadReg(uint addr)
        {
            byte[] payload = BitConverter.GetBytes(addr); // Little Endian
            addLogLine($"Reading Reg {addr:X}...");
            sendCommand(ESPCommand.READ_REG, payload);
            var resp = readPacket(3000, ESPCommand.READ_REG);
            
            // readPacket returns the 'value' field from the header
            // for commands with resp_data_len=0 (like READ_REG)
            if (resp != null && resp.Length >= 4)
            {
                return BitConverter.ToUInt32(resp, 0);
            }
            
            throw new Exception($"Failed to read register {addr:X}, resp len {(resp==null?-1:resp.Length)}");
        }


        public void WriteReg(uint addr, uint value, uint mask = 0xFFFFFFFF, uint delay = 0)
        {
            // Packet: addr(4) + value(4) + mask(4) + delay(4)
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(addr));
            payload.AddRange(BitConverter.GetBytes(value));
            payload.AddRange(BitConverter.GetBytes(mask));
            payload.AddRange(BitConverter.GetBytes(delay));
            
            sendCommand(ESPCommand.WRITE_REG, payload.ToArray());
            readPacket(1000, ESPCommand.WRITE_REG);
        }

        const uint ESP32_SPI_REG_BASE = 0x3FF42000;
        const uint SPI_USR_OFFS = 0x1C;
        const uint SPI_USR2_OFFS = 0x24;
        const uint SPI_MOSI_DLEN_OFFS = 0x28;
        const uint SPI_MISO_DLEN_OFFS = 0x2C;
        const uint SPI_W0_OFFS = 0x80;

        uint RunSpiFlashCmd(byte cmd, int readBits = 0)
        {
            // Assuming ESP32 Registers for now
            uint baseAddr = ESP32_SPI_REG_BASE;
            
            // Set lengths (0 MOSI, readBits MISO)
            if(readBits > 0)
            {
                 WriteReg(baseAddr + SPI_MISO_DLEN_OFFS, (uint)(readBits - 1));
            }
            else
            {
                 WriteReg(baseAddr + SPI_MISO_DLEN_OFFS, 0);
            }
            WriteReg(baseAddr + SPI_MOSI_DLEN_OFFS, 0); // 0 bits MOSI

            // SPI_USR_REG flags
            // COMMAND(31) | MISO(28) if read | MOSI(27) if write
            uint usrFlags = (1u << 31); // COMMAND
            if(readBits > 0) usrFlags |= (1u << 28); // MISO
            
            WriteReg(baseAddr + SPI_USR_OFFS, usrFlags);

            // SPI_USR2_REG: (7 << 28) | cmd
            // 7 bits command length? esptool uses 7 << 28
            uint usr2 = (7u << 28) | cmd;
            WriteReg(baseAddr + SPI_USR2_OFFS, usr2);

            // Execute: SPI_CMD_REG (offset 0) bit 18 (USR)
            WriteReg(baseAddr, (1u << 18));

            // Wait for completion (poll bit 18 of SPI_CMD_REG)
            int timeout = 50;
            for(int i = 0; i < timeout; i++)
            {
                uint val = ReadReg(baseAddr);
                if ((val & (1u << 18)) == 0) break;
                Thread.Sleep(1);
            }

            // Read result from W0
            if(readBits > 0)
            {
                uint w0 = ReadReg(baseAddr + SPI_W0_OFFS);
                // Mask? esptool just reads.
                return w0;
            }
            return 0;
        }

        public uint? ReadFlashId()
        {
            try
            {
                addLogLine("Reading Flash ID...");
                // CMD 0x9F (RDID), read 24 bits
                uint fid = RunSpiFlashCmd(0x9F, 24);
                addLogLine($"Flash ID: {fid:X}");
                
                // Decode size roughly
                // 0x16 = 22 -> 4MB
                uint sizeCode = (fid >> 16) & 0xFF;
                if(sizeCode > 0 && sizeCode <= 31)
                {
                    long size = 1L << (int)sizeCode;
                    addLogLine($"Detected Flash Size: {size} bytes ({size/1024/1024}MB)");
                }
                else
                {
                    addLogLine($"Unknown Flash Size Code: {sizeCode:X}");
                }
                
                return fid;
            }
            catch(Exception ex)
            {
                addErrorLine("Failed to read Flash ID: " + ex.Message);
            }
            return null;
        }

        public byte[] ReadFlashBlockSlow(uint addr, uint size)
        {
            // esptool: read_flash_slow reads in 64-byte blocks (ROM limit)
            // Each command returns resp_data_len=64 bytes
            const int BLOCK_LEN = 64;
            
            List<byte> result = new List<byte>();
            while (result.Count < (int)size)
            {
                int blockLen = Math.Min(BLOCK_LEN, (int)size - result.Count);
                byte[] payload = new byte[8];
                Array.Copy(BitConverter.GetBytes(addr + (uint)result.Count), 0, payload, 0, 4);
                Array.Copy(BitConverter.GetBytes((uint)blockLen), 0, payload, 4, 4);

                sendCommand(ESPCommand.READ_FLASH_SLOW, payload);
                // resp_data_len = BLOCK_LEN (ROM always returns 64 bytes)
                var resp = readPacket(3000, ESPCommand.READ_FLASH_SLOW, BLOCK_LEN);
                if (resp == null || resp.Length < blockLen)
                {
                    addErrorLine($"ReadFlashBlockSlow: failed to read block at 0x{addr + (uint)result.Count:X}");
                    return null;
                }
                // ROM returns full 64-byte buffer, take only blockLen bytes
                byte[] block = new byte[blockLen];
                Array.Copy(resp, 0, block, 0, blockLen);
                result.AddRange(block);
            }
            return result.ToArray();
        }

        bool MemBegin(uint size, uint blocks, uint blockSize, uint offset)
        {
            // esptool: struct.pack("<IIII", size, blocks, blocksize, offset)
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(size));
            payload.AddRange(BitConverter.GetBytes(blocks));
            payload.AddRange(BitConverter.GetBytes(blockSize));
            payload.AddRange(BitConverter.GetBytes(offset));

            sendCommand(ESPCommand.MEM_BEGIN, payload.ToArray());
            var resp = readPacket(5000, ESPCommand.MEM_BEGIN);
            return resp != null;
        }

        bool MemData(byte[] data, uint seq)
        {
            // esptool: struct.pack("<IIII", len(data), seq, 0, 0) + data
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes((uint)data.Length));
            payload.AddRange(BitConverter.GetBytes(seq));
            payload.AddRange(BitConverter.GetBytes((uint)0)); // reserved
            payload.AddRange(BitConverter.GetBytes((uint)0)); // reserved
            payload.AddRange(data);

            // esptool: self.checksum(data) - XOR all data bytes starting from 0xEF
            byte checksum = 0xEF;
            foreach (byte b in data) checksum ^= b;

            sendCommand(ESPCommand.MEM_DATA, payload.ToArray(), checksum);
            var resp = readPacket(5000, ESPCommand.MEM_DATA);
            return resp != null;
        }

        bool MemEnd(uint entryPoint)
        {
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes((uint)(entryPoint == 0 ? 1 : 0))); // no_entry
            payload.AddRange(BitConverter.GetBytes(entryPoint));

            sendCommand(ESPCommand.MEM_END, payload.ToArray());
            var resp = readPacket(3000, ESPCommand.MEM_END);
            return resp != null;
        }

        public bool UploadStub()
        {
            if (LegacyMode)
            {
                addLogLine("Legacy mode enabled, skipping stub upload.");
                return false;
            }

            addLogLine("Uploading stub flasher...");
            try
            {
                // Load stub from esptool JSON
                string stubPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "references", "esptool", "targets", "stub_flasher", "1", "esp32.json");
                if (!File.Exists(stubPath))
                {
                    // Try relative to working directory
                    stubPath = Path.Combine("references", "esptool", "targets", "stub_flasher", "1", "esp32.json");
                }
                if (!File.Exists(stubPath))
                {
                    addErrorLine("Stub JSON not found: " + stubPath);
                    return false;
                }

                string jsonContent = File.ReadAllText(stubPath);
                
                // Simple JSON parsing for our known fields
                string textB64 = ExtractJsonString(jsonContent, "text");
                string dataB64 = ExtractJsonString(jsonContent, "data");
                uint textStart = ExtractJsonUint(jsonContent, "text_start");
                uint dataStart = ExtractJsonUint(jsonContent, "data_start");
                uint entry = ExtractJsonUint(jsonContent, "entry");
                
                byte[] text = Convert.FromBase64String(textB64);
                byte[] data = Convert.FromBase64String(dataB64);

                // Console.WriteLine($"[DEBUG] Stub loaded: text={text.Length}B @0x{textStart:X}, data={data.Length}B @0x{dataStart:X}, entry=0x{entry:X}");

                // Upload text (IRAM)
                uint blocks = (uint)((text.Length + ESP_RAM_BLOCK - 1) / ESP_RAM_BLOCK);
                if (!MemBegin((uint)text.Length, blocks, (uint)ESP_RAM_BLOCK, textStart))
                {
                    addErrorLine("Failed to MEM_BEGIN for text");
                    return false;
                }
                for (uint i = 0; i < blocks; i++)
                {
                    int len = Math.Min(ESP_RAM_BLOCK, text.Length - (int)(i * ESP_RAM_BLOCK));
                    byte[] block = new byte[len];
                    Array.Copy(text, i * ESP_RAM_BLOCK, block, 0, len);
                    // Console.WriteLine($"[DEBUG] Sending text block {i}, Len {len}");
                    if (!MemData(block, i))
                    {
                        addErrorLine($"Failed to MEM_DATA text block {i}");
                        return false;
                    }
                }

                // Upload data (DRAM)
                blocks = (uint)((data.Length + ESP_RAM_BLOCK - 1) / ESP_RAM_BLOCK);
                if (!MemBegin((uint)data.Length, blocks, (uint)ESP_RAM_BLOCK, dataStart))
                {
                    addErrorLine("Failed to MEM_BEGIN for data");
                    return false;
                }
                for (uint i = 0; i < blocks; i++)
                {
                    int len = Math.Min(ESP_RAM_BLOCK, data.Length - (int)(i * ESP_RAM_BLOCK));
                    byte[] block = new byte[len];
                    Array.Copy(data, i * ESP_RAM_BLOCK, block, 0, len);
                    if (!MemData(block, i))
                    {
                        addErrorLine($"Failed to MEM_DATA data block {i}");
                        return false;
                    }
                }

                // Execute stub
                addLogLine("Running stub flasher...");
                MemEnd(entry);

                // Wait for OHAI
                if (!CheckForOHAI(5000))
                {
                    addErrorLine("Stub did not respond with OHAI.");
                    return false;
                }

                isStub = true;
                addLogLine("Stub flasher running.");
                return true;
            }
            catch (Exception ex)
            {
                addErrorLine("Stub upload failed: " + ex.Message);
                return false;
            }
        }

        static string ExtractJsonString(string json, string key)
        {
            // Find "key": "value" pattern
            string search = "\"" + key + "\": \"";
            int idx = json.IndexOf(search);
            if (idx < 0) { search = "\"" + key + "\":\""; idx = json.IndexOf(search); }
            if (idx < 0) throw new Exception($"JSON key '{key}' not found");
            int start = idx + search.Length;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }

        static uint ExtractJsonUint(string json, string key)
        {
            // Find "key": number pattern
            string search = "\"" + key + "\": ";
            int idx = json.IndexOf(search);
            if (idx < 0) { search = "\"" + key + "\":"; idx = json.IndexOf(search); }
            if (idx < 0) throw new Exception($"JSON key '{key}' not found");
            int start = idx + search.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return uint.Parse(json.Substring(start, end - start));
        }

        bool CheckForOHAI(int timeout)
        {
             // After mem_finish, stub sends a raw SLIP packet containing "OHAI"
             // This is NOT a command response, just raw bytes
             serial.ReadTimeout = timeout;
             List<byte> buffer = new List<byte>();
             long start = DateTime.Now.Ticks;
             // Console.WriteLine($"[DEBUG] CheckForOHAI: waiting {timeout}ms...");
             
             while((DateTime.Now.Ticks - start) / 10000 < timeout)
             {
                 try {
                     int b = serial.ReadByte();
                     if(b != -1) {
                         buffer.Add((byte)b);
                         // Console.Write($"{(byte)b:X2} ");
                     }
                     
                     // Check for OHAI
                     if(buffer.Count >= 6)
                     {
                         // Check from end
                         int n = buffer.Count;
                         if(buffer[n-1] == 0xC0 && buffer[n-2] == 0x49 && buffer[n-3] == 0x41 && buffer[n-4] == 0x48 && buffer[n-5] == 0x4F && buffer[n-6] == 0xC0)
                         {
                             // Console.WriteLine();
                             // Console.WriteLine("[DEBUG] CheckForOHAI: Got OHAI!");
                             return true;
                         }
                     }
                 } catch { }
             }
             // Console.WriteLine();
             // Console.WriteLine($"[DEBUG] CheckForOHAI: timeout. Received {buffer.Count} bytes: {BitConverter.ToString(buffer.ToArray())}");
             return false;
        }

        public byte[] ReadFlashFast(uint addr, uint size, Action<int, int> progress = null)
        {
            // READ_FLASH (0xD2) payload: offset(4) + length(4) + sector_size(4) + block_size(4)
            // esptool code: struct.pack("<IIII", offset, length, self.FLASH_SECTOR_SIZE, 64)
            
            uint sectorSize = 0x1000;
            uint packetSize = 64;
            
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(addr));
            payload.AddRange(BitConverter.GetBytes(size));
            payload.AddRange(BitConverter.GetBytes(sectorSize));
            payload.AddRange(BitConverter.GetBytes(packetSize));
            
            sendCommand(ESPCommand.READ_FLASH, payload.ToArray());
            
            // esptool: check_command waits for ACK response first
            var cmdResp = readPacket(3000, ESPCommand.READ_FLASH);
            if (cmdResp == null) throw new Exception("READ_FLASH command failed (no ACK)");
            
            // Pre-allocate the full result buffer - zero copy path
            byte[] dataBytes = new byte[size];
            byte[] ackBuf = new byte[4];
            uint received = 0;
            uint lastProgress = 0;
            
            while(received < size)
            {
                 int got = readRawPacketInto(dataBytes, (int)received, 3000);
                 if(got <= 0) throw new Exception("Timeout reading fast flash data");
                 
                 received += (uint)got;
                 
                 // Stub expects ACK: 4 bytes (bytes_received) as SLIP packet
                 ackBuf[0] = (byte)(received);
                 ackBuf[1] = (byte)(received >> 8);
                 ackBuf[2] = (byte)(received >> 16);
                 ackBuf[3] = (byte)(received >> 24);
                 sendRawSlip(ackBuf);
                 
                 // Batch progress to every 4KB to reduce callback overhead
                 if(progress != null && (received - lastProgress >= 4096 || received >= size))
                 {
                     progress((int)received, (int)size);
                     lastProgress = received;
                 }
            }
            
            // After data, read MD5 (16 bytes)
            byte[] digest = readRawPacket(3000);
            if(digest == null || digest.Length != 16) throw new Exception("Failed to read MD5 digest");
            
            using(var md5 = MD5.Create())
            {
                byte[] calc = md5.ComputeHash(dataBytes);
                for(int i=0; i<16; i++)
                {
                    if(calc[i] != digest[i])
                    {
                         throw new Exception($"MD5 mismatch! Expected {BitConverter.ToString(digest)}, Calc {BitConverter.ToString(calc)}");
                    }
                }
            }
            
            addLogLine("Flash Read MD5 verified.");
            return dataBytes;
        }

        void sendRawSlip(byte[] data)
        {
             int pos = 0;
             _slipBuf[pos++] = SLIP_END;
             foreach (byte b in data)
             {
                 if (b == SLIP_END)
                 {
                     _slipBuf[pos++] = SLIP_ESC;
                     _slipBuf[pos++] = SLIP_ESC_END;
                 }
                 else if (b == SLIP_ESC)
                 {
                     _slipBuf[pos++] = SLIP_ESC;
                     _slipBuf[pos++] = SLIP_ESC_ESC;
                 }
                 else
                 {
                     _slipBuf[pos++] = b;
                 }
             }
             _slipBuf[pos++] = SLIP_END;
             serial.Write(_slipBuf, 0, pos);
        }

        // Zero-copy overload: writes SLIP-decoded data directly into dest at destOffset.
        // Returns number of bytes written, or -1 on timeout.
        int readRawPacketInto(byte[] dest, int destOffset, int timeoutMs)
        {
             serial.ReadTimeout = timeoutMs;
             bool inPacket = false;
             bool escape = false;
             int written = 0;

             while(true)
             {
                 try {
                     int waiting = serial.BytesToRead;
                     int toRead = waiting > 0 ? Math.Min(waiting, _slipBuf.Length) : 1;
                     int count = serial.Read(_slipBuf, 0, toRead);
                     if (count <= 0) break;

                     for (int i = 0; i < count; i++)
                     {
                         byte b = _slipBuf[i];
                         if (b == SLIP_END) {
                             if (inPacket && written > 0) return written;
                             inPacket = true;
                             written = 0;
                         } else if (inPacket) {
                             if (b == SLIP_ESC) escape = true;
                             else {
                                 byte decoded = b;
                                 if (escape) {
                                     if (b == SLIP_ESC_END) decoded = SLIP_END;
                                     else if (b == SLIP_ESC_ESC) decoded = SLIP_ESC;
                                     escape = false;
                                 }
                                 dest[destOffset + written] = decoded;
                                 written++;
                             }
                         }
                     }
                 } catch(TimeoutException) { return -1; }
             }
             return -1;
        }

        byte[] readRawPacket(int timeoutMs)
        {
             List<byte> payload = new List<byte>();
             serial.ReadTimeout = timeoutMs;
             bool inPacket = false;
             bool escape = false;

             while(true)
             {
                 try {
                     int waiting = serial.BytesToRead;
                     int toRead = waiting > 0 ? Math.Min(waiting, _slipBuf.Length) : 1;
                     int count = serial.Read(_slipBuf, 0, toRead);
                     if (count <= 0) break;

                     for (int i = 0; i < count; i++)
                     {
                         byte b = _slipBuf[i];
                         if (b == SLIP_END) {
                             if (inPacket && payload.Count > 0) return payload.ToArray();
                             inPacket = true;
                             payload.Clear();
                         } else if (inPacket) {
                             if (b == SLIP_ESC) escape = true;
                             else {
                                 if (escape) {
                                     if (b == SLIP_ESC_END) payload.Add(SLIP_END);
                                     else if (b == SLIP_ESC_ESC) payload.Add(SLIP_ESC);
                                     escape = false;
                                 } else payload.Add(b);
                             }
                         }
                     }
                 } catch(TimeoutException) { break; }
             }
             return null;
        }

        public string FlashMd5Sum(uint addr, uint size)
        {
             // esptool: struct.pack("<IIII", addr, size, 0, 0)
             List<byte> payload = new List<byte>();
             payload.AddRange(BitConverter.GetBytes(addr));
             payload.AddRange(BitConverter.GetBytes(size));
             payload.AddRange(BitConverter.GetBytes((uint)0));
             payload.AddRange(BitConverter.GetBytes((uint)0));
             
             sendCommand(ESPCommand.SPI_FLASH_MD5, payload.ToArray());
             // esptool: timeout = timeout_per_mb(MD5_TIMEOUT_PER_MB, size)
             // MD5_TIMEOUT_PER_MB = 8 seconds per MB, minimum 3s
             int timeout = Math.Max(3000, (int)(8.0 * size / 1000000.0 * 1000));
             
             // esptool: resp_data_len = 16 for stub, 32 for ROM
             // Stub returns 16 raw MD5 bytes, ROM returns 32 hex chars
             int rdl = isStub ? 16 : 32;
             var resp = readPacket(timeout, ESPCommand.SPI_FLASH_MD5, rdl);
             if(resp != null)
             {
                  if (isStub)
                  {
                      // Stub: resp is 16 raw bytes, convert to hex string
                      return BitConverter.ToString(resp).Replace("-","");
                  }
                  else
                  {
                      // ROM: resp is 32 hex chars as ASCII
                      return System.Text.Encoding.ASCII.GetString(resp).ToUpper();
                  }
             }
             return null;
        }

        public bool EraseFlash()
        {
            addLogLine("Erasing entire flash...");
            sendCommand(ESPCommand.ERASE_FLASH, new byte[0]);
            // This takes a LONG time. 30s+?
            var resp = readPacket(60000, ESPCommand.ERASE_FLASH); 
            if(resp != null) {
                addLogLine("Erase complete.");
                return true;
            }
            addErrorLine("Erase failed.");
            return false;
        }

        public string ReadMac()
        {
             try
             {
                 uint baseAddr = 0x3FF5A000;
                 uint w1 = ReadReg(baseAddr + 0x04);
                 uint w2 = ReadReg(baseAddr + 0x08);
                 
                 byte[] mac = new byte[6];
                 mac[0] = (byte)((w2 >> 8) & 0xFF);
                 mac[1] = (byte)((w2 >> 0) & 0xFF);
                 mac[2] = (byte)((w1 >> 24) & 0xFF);
                 mac[3] = (byte)((w1 >> 16) & 0xFF);
                 mac[4] = (byte)((w1 >> 8) & 0xFF);
                 mac[5] = (byte)((w1 >> 0) & 0xFF);
                 
                 string s = BitConverter.ToString(mac);
                 addLogLine($"MAC Address: {s}");
                 return s;
             }
             catch(Exception ex)
             {
                 addErrorLine("Failed to read MAC: " + ex.Message);
                 return null;
             }
        }

        public bool Connect()
        {
            addLogLine("Attempting to connect to ESP32...");
            if(!openPort())
            {
                return false;
            }

            // Reset strategy: DTR=0, RTS=1 -> DTR=1, RTS=0
            serial.DtrEnable = false;
            serial.RtsEnable = true;
            Thread.Sleep(100);
            serial.DtrEnable = true;
            serial.RtsEnable = false;
            Thread.Sleep(500);

            if (Sync())
            {
                addSuccess(Environment.NewLine + "Synced with ESP32!" + Environment.NewLine);
                if (!SpiAttach())
                {
                    addErrorLine("Failed to configure SPI pins.");
                    return false;
                }
                return true;
            }
            return false; // Placeholder return
        }


        bool SpiAttach()
        {
             try
             {
                 addLogLine("Configuring SPI flash pins (SpiAttach)...");
                 // esptool: stub needs 4 bytes, ROM needs 8 bytes
                 int payloadSize = isStub ? 4 : 8;
                 byte[] payload = new byte[payloadSize];
                 sendCommand(ESPCommand.SPI_ATTACH, payload);
                 var resp = readPacket(3000, ESPCommand.SPI_ATTACH);
                 if(resp == null) 
                 {
                     addErrorLine("SPI_ATTACH failed (no response or error status).");
                     return false;
                 }
                 addLogLine("SPI_ATTACH success.");
                 return true;
             }
             catch(Exception ex)
             {
                 addErrorLine("SPI_ATTACH exception: " + ex.Message);
                 return false;
             }
        }

        public bool SpiSetParams(uint size)
        {
             try
             {
                 addLogLine($"Setting SPI flash parameters for {size / 1024 / 1024}MB...");
                 List<byte> payload = new List<byte>();
                 payload.AddRange(BitConverter.GetBytes((uint)0)); // id
                 payload.AddRange(BitConverter.GetBytes(size));    // total size
                 payload.AddRange(BitConverter.GetBytes((uint)64 * 1024)); // block size
                 payload.AddRange(BitConverter.GetBytes((uint)4 * 1024));  // sector size
                 payload.AddRange(BitConverter.GetBytes((uint)256));       // page size
                 payload.AddRange(BitConverter.GetBytes((uint)0xFFFF));    // status mask

                 sendCommand(ESPCommand.SPI_SET_PARAMS, payload.ToArray());
                 var resp = readPacket(1000, ESPCommand.SPI_SET_PARAMS);
                 if (resp == null)
                 {
                     addErrorLine("SPI_SET_PARAMS failed (no response)");
                     return false;
                 }
                 addLogLine("SPI_SET_PARAMS success");
                 return true;
             }
             catch (Exception ex)
             {
                 addErrorLine("SPI_SET_PARAMS exception: " + ex.Message);
                 return false;
             }
        }

        public bool ChangeBaudrate(int newBaud)
        {
            try
            {
                addLogLine($"Requesting baud rate change to {newBaud}...");
                byte[] payload = new byte[8];
                // new baud
                Array.Copy(BitConverter.GetBytes(newBaud), 0, payload, 0, 4);
                // esptool: second arg is old baud when running stub, 0 for ROM
                int oldBaud = isStub ? serial.BaudRate : 0;
                Array.Copy(BitConverter.GetBytes(oldBaud), 0, payload, 4, 4);

                sendCommand(ESPCommand.CHANGE_BAUDRATE, payload);
                
                // Wait for the ACK at the current (old) baud rate
                var resp = readPacket(3000, ESPCommand.CHANGE_BAUDRATE);
                if (resp == null)
                {
                    addErrorLine("Baudrate change request failed (no response)");
                    return false;
                }

                // Give the chip a tiny bit of time to actually flip its internal switch
                Thread.Sleep(50);
                serial.BaudRate = newBaud;
                Thread.Sleep(50); // Give the local OS/Driver time to settle
                serial.DiscardInBuffer();
                addLogLine($"Baud rate changed to {newBaud} successfully.");
                return true;
            }
            catch (Exception ex)
            {
                addErrorLine("ChangeBaudrate exception: " + ex.Message);
                return false;
            }
        }

        void sendCommand(ESPCommand op, byte[] data, uint checksum = 0)
        {
            // Packet: C0 [00 op len(2) checksum(4)] [data] C0
            // Inside brackets is SLIP escaped.
            
            // Construct inner packet (matches esptool struct.pack("<BBHI", 0x00, op, len(data), chk) + data)
            List<byte> inner = new List<byte>();
            inner.Add(0x00);
            inner.Add((byte)op);
            inner.AddRange(BitConverter.GetBytes((ushort)data.Length));
            inner.AddRange(BitConverter.GetBytes(checksum));
            inner.AddRange(data);

            // Console.WriteLine($"[DEBUG] TX cmd=0x{(byte)op:X2} data_len={data.Length} chk=0x{checksum:X8}");

            // SLIP encode
            List<byte> packet = new List<byte>();
            packet.Add(SLIP_END);
            foreach (byte b in inner)
            {
                if (b == SLIP_END)
                {
                    packet.Add(SLIP_ESC);
                    packet.Add(SLIP_ESC_END);
                }
                else if (b == SLIP_ESC)
                {
                    packet.Add(SLIP_ESC);
                    packet.Add(SLIP_ESC_ESC);
                }
                else
                {
                    packet.Add(b);
                }
            }
            packet.Add(SLIP_END);

            serial.Write(packet.ToArray(), 0, packet.Count);
        }

        // resp_data_len: how many bytes of actual response data to expect before the status bytes
        // For most commands this is 0 (status bytes are the first 2 bytes of data payload)
        // For SPI_FLASH_MD5 with stub: 16 (16 bytes MD5 + 2 status bytes)
        // For READ_FLASH_SLOW: 64 (64 bytes data + 2 status bytes)
        byte[] readPacket(int timeoutMs = 1000, ESPCommand expectedCmd = (ESPCommand)0, int resp_data_len = 0)
        {
              // Unified SLIP reader + response parser - matches esptool's command() + check_command()
              List<byte> payload = new List<byte>();
              serial.ReadTimeout = timeoutMs;
              
              long startTicks = DateTime.Now.Ticks;
              
              // esptool tries up to 100 responses to find the matching one
              for (int retry = 0; retry < 100; retry++)
              {
                  if(timeoutMs > 0 && (DateTime.Now.Ticks - startTicks) / 10000 > timeoutMs)
                      break;
                  
                  payload.Clear();
                  bool inPacket = false;
                  bool escape = false;
                  bool gotPacket = false;
                  
                  try 
                  {
                      while (true)
                      {
                          if(timeoutMs > 0 && (DateTime.Now.Ticks - startTicks) / 10000 > timeoutMs)
                              return null;

                          int waiting = serial.BytesToRead;
                          int toRead = waiting > 0 ? Math.Min(waiting, _slipBuf.Length) : 1;
                          int count = serial.Read(_slipBuf, 0, toRead);
                          if (count <= 0) break;

                          for (int i = 0; i < count; i++)
                          {
                              byte b = _slipBuf[i];
                              if (b == SLIP_END)
                              {
                                  if (inPacket && payload.Count > 0)
                                  {
                                      gotPacket = true;
                                      goto packetDone;
                                  }
                                  else
                                  {
                                      inPacket = true;
                                      payload.Clear();
                                  }
                              }
                              else if (inPacket)
                              {
                                  if (b == SLIP_ESC)
                                      escape = true;
                                  else if (escape)
                                  {
                                      if (b == SLIP_ESC_END) payload.Add(SLIP_END);
                                      else if (b == SLIP_ESC_ESC) payload.Add(SLIP_ESC);
                                      escape = false;
                                  }
                                  else
                                      payload.Add(b);
                              }
                          }
                      }
                  }
                  catch(TimeoutException) { }

                  packetDone:
                  if (!gotPacket) continue;

                  byte[] raw = payload.ToArray();
                  // Console.WriteLine($"[DEBUG] RX Packet ({raw.Length}b): {BitConverter.ToString(raw)}");

                  // Parse: resp(1) op_ret(1) len_ret(2) val(4) [data...]
                  if (raw.Length < 8) { /* Console.WriteLine($"[DEBUG] Packet too short: {raw.Length}"); */ continue; }
                  byte resp = raw[0];
                  byte op_ret = raw[1];
                  uint val = BitConverter.ToUInt32(raw, 4);
                  byte[] data = new byte[raw.Length - 8];
                  if (data.Length > 0) Array.Copy(raw, 8, data, 0, data.Length);

                  if (resp != 0x01) { /* Console.WriteLine($"[DEBUG] Not a response (0x{resp:X2})"); */ continue; }
                  if (expectedCmd != 0 && op_ret != (byte)expectedCmd)
                  {
                      // Console.WriteLine($"[DEBUG] CMD mismatch: got 0x{op_ret:X2}, want 0x{(byte)expectedCmd:X2}");
                      continue;
                  }

                  // check_command: status_bytes = data[resp_data_len : resp_data_len + 2]
                  if (data.Length < resp_data_len + 2)
                  {
                      if (data.Length >= 2 && data[0] != 0)
                      {
                          addErrorLine($"CMD 0x{op_ret:X2} error: status=0x{data[0]:X2} err=0x{data[1]:X2}");
                          return null;
                      }
                      return BitConverter.GetBytes(val);
                  }

                  if (data[resp_data_len] != 0)
                  {
                      addErrorLine($"CMD 0x{op_ret:X2} error: status=0x{data[resp_data_len]:X2} err=0x{data[resp_data_len+1]:X2}");
                      return null;
                  }

                  // if resp_data_len > 0: return data[:resp_data_len], else: return val
                  if (resp_data_len > 0)
                  {
                      byte[] result = new byte[resp_data_len];
                      Array.Copy(data, 0, result, 0, resp_data_len);
                      return result;
                  }
                  return BitConverter.GetBytes(val);
              }
              
              // Console.WriteLine($"[DEBUG] readPacket exhausted for cmd 0x{(byte)expectedCmd:X2}");
              return null;
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if (Connect())
            {
                GetChipId();
                ReadMac();
                uint? fid = ReadFlashId();
                uint flashSize = 0;
                if (fid.HasValue)
                {
                    uint sizeCode = (fid.Value >> 16) & 0xFF;
                    if (sizeCode > 0 && sizeCode <= 31)
                    {
                        flashSize = 1u << (int)sizeCode;
                        SpiSetParams(flashSize);
                    }
                }

                if (fullRead && flashSize > 0)
                {
                    sectors = (int)(flashSize / 0x1000);
                }

                addLogLine($"Starting Flash Read: {sectors} sectors from 0x{startSector:X}...");
                ms = new MemoryStream();
                var swRead = Stopwatch.StartNew();
                uint startAddr = (uint)startSector * 0x1000;
                uint totalSize = (uint)sectors * 0x1000;

                // Try stub for faster reads
                if (!LegacyMode && UploadStub())
                {
                    // Re-attach SPI after stub starts
                    SpiAttach();
                    if (baudrate > 115200)
                    {
                        ChangeBaudrate(baudrate);
                    }

                    try
                    {
                        byte[] flashData = ReadFlashFast(startAddr, totalSize, (received, total) =>
                        {
                            logger.setProgress(received, total);
                        });
                        ms.Write(flashData, 0, flashData.Length);
                        swRead.Stop();
                        double secsFast = swRead.Elapsed.TotalSeconds;
                        double kbitsFast = (totalSize * 8.0 / 1000.0) / secsFast;
                        double kbytesFast = (totalSize / 1024.0) / secsFast;
                        addLogLine($"Read {totalSize} bytes at 0x{startAddr:X8} in {secsFast:F1}s ({kbitsFast:F1} kbit/s, {kbytesFast:F1} KB/s)");
                        addLogLine("Flash Read Complete (stub mode).");
                        return;
                    }
                    catch (Exception ex)
                    {
                        addErrorLine("Fast read failed: " + ex.Message);
                        addLogLine("Falling back to slow read...");
                    }
                }

                // Slow read fallback (ROM only, 64 bytes at a time)
                uint blockSize = 64; // Max safe size for ROM READ_FLASH_SLOW

                uint currentAddr = startAddr;
                while (currentAddr < startAddr + totalSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    uint toRead = Math.Min(blockSize, startAddr + totalSize - currentAddr);
                    byte[] block = ReadFlashBlockSlow(currentAddr, toRead);
                    
                    if (block == null)
                    {
                        addErrorLine($"Failed to read block at 0x{currentAddr:X}");
                        return;
                    }

                    if (block.Length != toRead)
                    {
                         addWarningLine($"Read mismatch at 0x{currentAddr:X}: Request {toRead}, Got {block.Length}");
                    }
                    // If we got 0, we can't progress. Avoid infinite loop.
                    if (block.Length == 0)
                    {
                        addErrorLine("Read 0 bytes, aborting.");
                        return;
                    }

                    ms.Write(block, 0, block.Length);
                    currentAddr += (uint)block.Length;
                    logger.setProgress((int)(currentAddr - startAddr), (int)totalSize);
                }
                swRead.Stop();
                double secsSlow = swRead.Elapsed.TotalSeconds;
                double kbitsSlow = (totalSize * 8.0 / 1000.0) / secsSlow;
                double kbytesSlow = (totalSize / 1024.0) / secsSlow;
                addLogLine($"Read {totalSize} bytes at 0x{startAddr:X8} in {secsSlow:F1}s ({kbitsSlow:F1} kbit/s, {kbytesSlow:F1} KB/s)");
                addLogLine("Flash Read Complete.");
            }
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            if (rwMode == WriteMode.OnlyWrite)
            {
                byte[] data = File.ReadAllBytes(sourceFileName);
                doWrite((uint)startSector * 0x1000, data);
            }
            else
            {
                addErrorLine("ReadAndWrite mode not yet implemented for ESPFlasher.");
            }
        }

        public void doWrite(uint offset, byte[] data)
        {
            if (Connect())
            {

                // Try stub for faster writes
                if (!LegacyMode)
                {
                    UploadStub();
                    if (isStub)
                    {
                        SpiAttach();
                        if (baudrate > 115200)
                        {
                            ChangeBaudrate(baudrate);
                        }
                    }
                }

                uint blockSize = 0x400; // 1024 bytes
                uint numBlocks = (uint)((data.Length + blockSize - 1) / blockSize);

                addLogLine($"Starting Flash Write: {data.Length} bytes ({numBlocks} blocks) at 0x{offset:X}...");
                var swWrite = Stopwatch.StartNew();

                // FLASH_BEGIN: size, numBlocks, blockSize, offset
                List<byte> beginPayload = new List<byte>();
                beginPayload.AddRange(BitConverter.GetBytes((uint)data.Length));
                beginPayload.AddRange(BitConverter.GetBytes(numBlocks));
                beginPayload.AddRange(BitConverter.GetBytes(blockSize));
                beginPayload.AddRange(BitConverter.GetBytes(offset));

                sendCommand(ESPCommand.FLASH_BEGIN, beginPayload.ToArray());
                // FLASH_BEGIN can take a long time if it triggers an erase
                if (readPacket(10000, ESPCommand.FLASH_BEGIN) == null)
                {
                    addErrorLine("FLASH_BEGIN failed.");
                    return;
                }

                for (uint i = 0; i < numBlocks; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    uint start = i * blockSize;
                    uint len = Math.Min(blockSize, (uint)data.Length - start);

                    // Block must be padded to blockSize with 0xFF
                    byte[] block = new byte[blockSize];
                    for (int j = 0; j < block.Length; j++) block[j] = 0xFF;
                    Array.Copy(data, (int)start, block, 0, (int)len);

                    // FLASH_DATA: size(4), seq(4), reserved(4), reserved(4), data(blockSize)
                    List<byte> dataPayload = new List<byte>();
                    dataPayload.AddRange(BitConverter.GetBytes(blockSize));
                    dataPayload.AddRange(BitConverter.GetBytes(i));
                    dataPayload.AddRange(BitConverter.GetBytes((uint)0)); // reserved
                    dataPayload.AddRange(BitConverter.GetBytes((uint)0)); // reserved
                    dataPayload.AddRange(block);

                    // Calculate checksum over the data block
                    byte checksum = 0xEF;
                    foreach (byte b in block) checksum ^= b;

                    bool success = false;
                    for (int retry = 0; retry < 3; retry++)
                    {
                        sendCommand(ESPCommand.FLASH_DATA, dataPayload.ToArray(), checksum);
                        if (readPacket(3000, ESPCommand.FLASH_DATA) != null)
                        {
                            success = true;
                            break;
                        }
                        addWarningLine($"FLASH_DATA failed at block {i}, retry {retry+1}...");
                    }

                    if (!success)
                    {
                        addErrorLine($"FLASH_DATA failed at block {i} after 3 retries.");
                        return;
                    }

                    logger.setProgress((int)((i + 1) * blockSize), data.Length);
                }

                // FLASH_END: execute (1 = run, 0 = stay)
                sendCommand(ESPCommand.FLASH_END, BitConverter.GetBytes((uint)0));
                readPacket(1000, ESPCommand.FLASH_END);

                swWrite.Stop();
                double secsWrite = swWrite.Elapsed.TotalSeconds;
                double kbitsWrite = (data.Length * 8.0 / 1000.0) / secsWrite;
                double kbytesWrite = (data.Length / 1024.0) / secsWrite;
                addLogLine($"Wrote {data.Length} bytes at 0x{offset:X8} in {secsWrite:F1}s ({kbitsWrite:F1} kbit/s, {kbytesWrite:F1} KB/s)");
                addLogLine("Flash Write Complete.");
                
                if (isStub)
                {
                     addLogLine("Verifying write with MD5...");
                     try
                     {
                         using(var md5 = MD5.Create())
                         {
                             byte[] calc = md5.ComputeHash(data);
                             string expected = BitConverter.ToString(calc).Replace("-","");
                             
                             string actual = FlashMd5Sum(offset, (uint)data.Length);
                             if(actual == null) addErrorLine("Failed to get Flash MD5");
                             else if(actual != expected) addErrorLine($"MD5 Mismatch! Expected {expected}, Got {actual}");
                             else addLogLine("Write Verified Successfully!");
                         }
                     }
                     catch(Exception ex)
                     {
                         addErrorLine("Verification exception: " + ex.Message);
                     }
                }
            }
        }
    }
}
