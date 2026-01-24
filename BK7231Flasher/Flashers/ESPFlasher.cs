using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

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
            GET_SECURITY_INFO = 0x14,
        }

        const byte SLIP_END = 0xC0;
        const byte SLIP_ESC = 0xDB;
        const byte SLIP_ESC_END = 0xDC;
        const byte SLIP_ESC_ESC = 0xDD;

        public ESPFlasher(CancellationToken ct) : base(ct)
        {
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

        public bool Connect()
        {
            addLogLine("Attempting to connect to ESP32...");
            if(!openPort())
            {
                return false;
            }

            // Reset strategy could be complex, for now assume user put it in boot mode or standard DTR/RTS
            // esptool does: DTR=0, RTS=1 -> DTR=1, RTS=0
            serial.DtrEnable = false;
            serial.RtsEnable = true;
            Thread.Sleep(100);
            serial.DtrEnable = true;
            serial.RtsEnable = false;
            Thread.Sleep(500);

            if (Sync())
            {
                addSuccess(Environment.NewLine + "Synced with ESP32!" + Environment.NewLine);
                SpiAttach();
                return true;
            }

            addError(Environment.NewLine + "Failed to sync with ESP32. Please ensure device is in bootloader mode." + Environment.NewLine);
            return false;
        }

        void SpiAttach()
        {
             try
             {
                 // ESP32 ROM needs 8 bytes of 0 for SPI_ATTACH
                 byte[] payload = new byte[8]; 
                 sendCommand(ESPCommand.SPI_ATTACH, payload);
                 var resp = readPacket(1000, ESPCommand.SPI_ATTACH);
                 if(resp == null) 
                     addErrorLine("SPI_ATTACH failed (no response)");
                 else
                     addLogLine("SPI_ATTACH success");
             }
             catch(Exception ex)
             {
                 addErrorLine("SPI_ATTACH exception: " + ex.Message);
             }
        }

        bool openPort()
        {
            try
            {
                if (serial == null)
                {
                    serial = new SerialPort(serialName, baudrate);
                    serial.Open();
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
                    if (resp != null && resp.Length > 0)
                    {
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
            var resp = readPacket(3000, ESPCommand.READ_REG); // Wait longer for ReadReg
            
            // Check if resp is just value (4 bytes)
            if (resp != null && resp.Length == 4)
            {
                return BitConverter.ToUInt32(resp, 0);
            }
            // Or maybe it has header? No, readPacket/parseResponse strips header.
            
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

        void sendCommand(ESPCommand op, byte[] data, uint checksum = 0)
        {
            // Packet: C0 [00 op len(2) checksum(4)] [data] C0
            // Inside brackets is SLIP escaped.
            
            // Construct inner packet
            List<byte> inner = new List<byte>();
            inner.Add(0x00);
            inner.Add((byte)op);
            inner.AddRange(BitConverter.GetBytes((ushort)data.Length));
            inner.AddRange(BitConverter.GetBytes(checksum));
            inner.AddRange(data);

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


        byte[] readPacket(int timeoutMs = 1000, ESPCommand expectedCmd = (ESPCommand)0)
        {
             // Simple SLIP reader
             List<byte> payload = new List<byte>();
             serial.ReadTimeout = timeoutMs;
             
             long startTicks = DateTime.Now.Ticks;
             
             while(true)
             {
                 if(timeoutMs > 0 && (DateTime.Now.Ticks - startTicks) / 10000 > timeoutMs)
                 {
                     break;
                 }
                 
                 payload.Clear();
                 bool inPacket = false;
                 bool escape = false;
                 
                 try 
                 {
                     while (true)
                     {
                         if(timeoutMs > 0 && (DateTime.Now.Ticks - startTicks) / 10000 > timeoutMs)
                         {
                             // Total timeout
                             return null;
                         }

                         int bInt = serial.ReadByte();
                         if (bInt == -1) break;
                         byte b = (byte)bInt;
    
                         if (b == SLIP_END)
                         {
                             if (inPacket)
                             {
                                 // End of packet
                                 byte[] res = parseResponse(payload.ToArray());
                                 if(res != null) 
                                 {
                                      // Check OpCode if required
                                      byte cmd = payload[1];
                                      if(expectedCmd != 0 && cmd != (byte)expectedCmd)
                                      {
                                          // addLogLine($"Skipping unmatched response CMD {cmd:X} (wanted {(byte)expectedCmd:X})");
                                          break; // Break inner loop to continue draining
                                      }
                                      return res;
                                 }
                                 else 
                                 {
                                     break; // Invalid parse, continue draining
                                 }
                             }
                             else
                             {
                                 // Start of packet
                                 inPacket = true;
                                 payload.Clear();
                             }
                         }
                         else if (inPacket)
                         {
                             if (b == SLIP_ESC)
                             {
                                 escape = true;
                             }
                             else
                             {
                                 if (escape)
                                 {
                                     if (b == SLIP_ESC_END) payload.Add(SLIP_END);
                                     else if (b == SLIP_ESC_ESC) payload.Add(SLIP_ESC);
                                     escape = false;
                                 }
                                 else
                                 {
                                     payload.Add(b);
                                 }
                             }
                         }
                     }
                 }
                 catch(TimeoutException) { 
                     // Inner read byte timeout, treated as loop break to check total timeout
                 }
             }
             
             return null;
        }

        byte[] parseResponse(byte[] raw)
        {
            // Response format: direction(1) command(1) size(2) value(4) data(...)
            // direction should be 0x01 (response)
            if (raw.Length < 8) return null;
            if (raw[0] != 0x01) return null; // Not a response?
            
            byte cmd = raw[1];
            ushort size = BitConverter.ToUInt16(raw, 2);
            uint value = BitConverter.ToUInt32(raw, 4);
            
            
            // addLogLine($"RX Packet: CMD={cmd:X} SIZE={size} VAL={value:X} RAW={BitConverter.ToString(raw)}");

            if (raw.Length - 8 < size) 
            {
                addLogLine("Incomplete packet!");
                return null;
            }

            byte[] data = new byte[size];
            Array.Copy(raw, 8, data, 0, size);
            
            if (size == 0 || cmd == (byte)ESPCommand.READ_REG)
            {
                 return BitConverter.GetBytes(value);
            }

            return data;
        }

        public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {
            if(Connect())
            {
                GetChipId();
                ReadMac();
                ReadFlashId();
                addLogLine($"THIS IS JUST A TEST, THERE IS NOTHING MORE YET...");
                addLogLine($"THIS IS JUST A TEST, THERE IS NOTHING MORE YET...");
                addLogLine($"THIS IS JUST A TEST, THERE IS NOTHING MORE YET...");
            }
        }
    }
}
