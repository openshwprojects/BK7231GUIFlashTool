using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BK7231Flasher.MiscUtils;

namespace BK7231Flasher
{
    public class TuyaConfig
    {
        // thanks to kmnh & Kuba bk7231 tools for figuring out this format
        static readonly string KEY_MASTER = "qwertyuiopasdfgh";
        static readonly int SECTOR_SIZE = 4096;
        static readonly uint MAGIC_FIRST_BLOCK = 0x13579753;
        static readonly uint MAGIC_NEXT_BLOCK = 0x98761234;
        static readonly uint MAGIC_FIRST_BLOCK_OS3 = 0x135726AB;
        // 8721D for RTL8720D devices, 8711AM_4M for WRG1. Not known for W800, ECR6600, RTL8720CM, BK7252...
        public static byte[] KEY_PART_1 = Encoding.ASCII.GetBytes("8710_2M");
        static readonly byte[] KEY_PART_2 = Encoding.ASCII.GetBytes("HHRRQbyemofrtytf");
        static readonly byte[] KEY_NULL = new byte[] { 0x90, 0x90, 0xA4, 0xA4, 0xA2, 0xC4, 0xF2, 0xCA, 0xDA, 0xDE, 0xCC, 0xE4, 0xE8, 0xF2, 0xE8, 0xCC };
        static readonly byte[] KEY_PART_1_D = Encoding.ASCII.GetBytes("8721D");
        static readonly byte[] KEY_PART_1_AM = Encoding.ASCII.GetBytes("8711AM_4M");
        //static byte[] MAGIC_CONFIG_START = new byte[] { 0x46, 0xDC, 0xED, 0x0E, 0x67, 0x2F, 0x3B, 0x70, 0xAE, 0x12, 0x76, 0xA3, 0xF8, 0x71, 0x2E, 0x03 };
        // TODO: check more bins with this offset
        // hex 0x1EE000
        // While flash is 0x200000, so we have at most 0x12000 bytes there...
        // So it's 0x12 sectors (18 sectors)
        const int USUAL_BK7231_MAGIC_POSITION = 2023424;
        const int USUAL_BK_NEW_XR806_MAGIC_POSITION = 2052096;
        const int USUAL_RTLB_XR809_MAGIC_POSITION = 2011136;
        const int USUAL_RTLC_ECR6600_MAGIC_POSITION = 1921024;
        const int USUAL_RTLD_MAGIC_POSITION = 3891200;
        const int USUAL_WBRG1_MAGIC_POSITION = 8220672;
        const int USUAL_T3_MAGIC_POSITION = 3997696;
        const int USUAL_T5_MAGIC_POSITION = 8245248;
        const int USUAL_LN882H_MAGIC_POSITION = 2015232;
        const int USUAL_TR6260_MAGIC_POSITION = 860160;
        const int USUAL_W800_MAGIC_POSITION = 1835008;
        const int USUAL_LN8825_MAGIC_POSITION = 1994752;
        const int USUAL_RTLCM_MAGIC_POSITION = 3633152;
        const int USUAL_BK7252_MAGIC_POSITION = 3764224;
        
        const int KVHeaderSize = 0x12;

        class VaultPage
        {
            public int FlashOffset;
            public uint Seq;
            public byte[] Data;
        }
        
        public sealed class KvEntry
        {
            public uint ValueLength;
            public ushort KeyId;
            public ushort ChecksumStored;
            public ushort ChecksumCalculated;
            public bool IsCheckSumCorrect => ChecksumStored == ChecksumCalculated;

            public string Key = "";
            public byte[] Value = Array.Empty<byte>();
        
            public override string ToString()
                => $"{Key} (len={ValueLength}, valid={IsCheckSumCorrect})";
        }

        static ushort CalcChecksum(byte[] buf, int off, int len)
        {
            ushort sum = 0;
            for(int i = off; i < off + len; i++) sum += buf[i];
            return sum;
        }

        static bool TryParseEntry(byte[] pageData, int entryOffset, out KvEntry entry)
        {
            entry = null!;

            if(entryOffset + KVHeaderSize > pageData.Length)
                return false;

            uint valueLen = ReadU32LE(pageData, entryOffset + 4);

            if(valueLen == 0 || valueLen > pageData.Length)
                return false;

            int totalLength = KVHeaderSize + (int)valueLen;
            if(entryOffset + totalLength > pageData.Length)
                return false;

            var storedChsum = ReadU16LE(pageData, entryOffset);
            var keyId = ReadU16LE(pageData, entryOffset + 8);
            var keyLen = pageData[entryOffset + 0x11];
            var keyOff = 18;
            var valOff = 128;

            int keyPos = entryOffset + keyOff;
            if(keyPos < 0 || keyPos >= pageData.Length)
                return false;

            var keyBytes = new List<byte>();
            for(int i = keyPos; i < keyPos + keyLen; i++)
            {
                byte b = pageData[i];
                if(!(b == 0 || (b >= 0x20 && b <= 0x7E)))
                	return false;
                if(b == 0)
                    break;
                keyBytes.Add(b);
            }

            if(keyBytes.Count == 0)
                return false;

            string key = Encoding.ASCII.GetString(keyBytes.ToArray());

            int valPos = entryOffset + valOff;
            if(valPos < 0 || valPos + valueLen > pageData.Length)
                return false;

            ushort calcChsum = CalcChecksum(pageData, valPos, (int)valueLen);
            //if(storedChsum != calcChsum)
            //	return false;

            byte[] value = new byte[valueLen];
            Buffer.BlockCopy(pageData, valPos, value, 0, (int)valueLen);

            entry = new KvEntry
            {
                ValueLength = valueLen,
                KeyId = keyId,
                Key = key,
                Value = value,
                ChecksumStored = storedChsum,
                ChecksumCalculated = calcChsum
            };

            return true;
        }

        List<KvEntry> ParseVault()
        {
            var entries = new List<KvEntry>();
            byte[] data = descryptedRaw;

            for(int off = 0; off + KVHeaderSize < data.Length; off += 0x80)
            {
                if(TryParseEntry(data, off, out var entry))
                {
                    entries.Add(entry);

                    int nextOffset = off + 0x80;
                    if(nextOffset > off)
                        off = nextOffset - 0x80;
                }
            }

            return entries;
        }

        // pretty useless for RTLs, since littlefs overwrites it.
        internal static int getMagicOffset(BKType type) => type switch
            {
                BKType.RTL8710B => USUAL_RTLB_XR809_MAGIC_POSITION,
                BKType.RTL87X0C => USUAL_RTLC_ECR6600_MAGIC_POSITION,
                BKType.RTL8720D => USUAL_RTLD_MAGIC_POSITION,
                BKType.LN882H   => USUAL_LN882H_MAGIC_POSITION,
                BKType.BK7236   => USUAL_T3_MAGIC_POSITION,
                BKType.BK7238   => USUAL_BK_NEW_XR806_MAGIC_POSITION,
                BKType.BK7258   => USUAL_T5_MAGIC_POSITION,
                BKType.ECR6600  => USUAL_RTLC_ECR6600_MAGIC_POSITION,
                BKType.LN8825   => USUAL_LN8825_MAGIC_POSITION,
                BKType.TR6260   => USUAL_TR6260_MAGIC_POSITION,
                _               => USUAL_BK7231_MAGIC_POSITION,
            };

        public static int getMagicSize(BKType type) => type switch
        {
            BKType.RTL8710B => 0x200000 - USUAL_RTLB_XR809_MAGIC_POSITION,
            BKType.RTL87X0C => 0x1E5000 - USUAL_RTLC_ECR6600_MAGIC_POSITION,
            BKType.RTL8720D => 0x3FC000 - USUAL_RTLD_MAGIC_POSITION,
            BKType.LN882H   => 0x200000 - USUAL_LN882H_MAGIC_POSITION,
            BKType.BK7236   => 0x3E0000 - USUAL_T3_MAGIC_POSITION,
            BKType.BK7238   => 0x200000 - USUAL_BK_NEW_XR806_MAGIC_POSITION,
            BKType.BK7258   => 0x7ED000 - USUAL_T5_MAGIC_POSITION,
            BKType.ECR6600  => 0x1F7000 - USUAL_RTLC_ECR6600_MAGIC_POSITION,
            BKType.LN8825   => 0x200000 - USUAL_LN8825_MAGIC_POSITION,
            BKType.TR6260   => 0x0DE000 - USUAL_TR6260_MAGIC_POSITION,
            _               => 0x200000 - USUAL_BK7231_MAGIC_POSITION,
        };

        int magicPosition = -1;
        byte[] descryptedRaw;
        byte[] original;
        List<KeyValue> parms = new List<KeyValue>();

        public string getMagicPositionHex() => $"0x{magicPosition:X}";

        public string getMagicPositionDecAndHex() => $"{magicPosition} ({getMagicPositionHex()})";

        public bool fromFile(string fname)
        {
            using var fs = new FileStream(fname, FileMode.Open, FileAccess.Read);
            var buffer = new byte[fs.Length];
            fs.Read(buffer, 0, buffer.Length);
            return fromBytes(buffer);
        }

        bool bLastBinaryOBKConfig;
        bool bGivenBinaryIsFullOf0xff;
        // warn users that they have erased flash sector with cfg
        public bool isLastBinaryFullOf0xff()
        {
            return bGivenBinaryIsFullOf0xff;
        }
        public bool isLastBinaryOBKConfig()
        {
            return bLastBinaryOBKConfig;
        }

        public bool fromBytes(byte[] data)
        {
            descryptedRaw = null;
            original = data;
            if (isFullOf(data, 0xff))
            {
                FormMain.Singleton.addLog("It seems that dragged binary is full of 0xff, someone must have erased the flash" + Environment.NewLine, System.Drawing.Color.Purple);
                bGivenBinaryIsFullOf0xff = true;
                return true;
            }
            if(data.Length>3 && data[0] == (byte)'C' && data[1] == (byte)'F' && data[2] == (byte)'G')
            {
                FormMain.Singleton.addLog("It seems that dragged binary is OBK config, not a Tuya one" + Environment.NewLine, System.Drawing.Color.Purple);
                bLastBinaryOBKConfig = true;
                return true;
            }

            try
            {
                if(TryVaultExtract(data)) return false;
            }
            finally
            {
                if(descryptedRaw != null)
                {
                    string debugName = "lastRawDecryptedStrings.bin";
                    FormMain.Singleton.addLog("Saving debug Tuya decryption data to " + debugName + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

                    File.WriteAllBytes(debugName, descryptedRaw);
                }
            }

            return true;
        }

        bool TryVaultExtract(byte[] flash)
        {
            descryptedRaw = null;

            var deviceKeys = FindDeviceKeys(flash);
            if(deviceKeys.Count == 0)
            {
                FormMain.Singleton.addLog("Failed to extract Tuya keys - magic constant header not found in binary" + Environment.NewLine, System.Drawing.Color.Purple);
                return false;
            }

            var baseKeyCandidates = new byte[][]
            {
                KEY_PART_1,
                KEY_NULL,
                KEY_PART_1_D,
                KEY_PART_2,
                KEY_PART_1_AM,
            };

            var pageMagics = new uint[] { MAGIC_NEXT_BLOCK, MAGIC_FIRST_BLOCK_OS3, MAGIC_FIRST_BLOCK };

            List<VaultPage> bestPages = null;
            int bestCount = 0;
            var obj = new object();
            var time = Stopwatch.StartNew();
            foreach(var devKey in deviceKeys)
            {
                Parallel.ForEach(baseKeyCandidates, baseKey =>
                {
                    using var aes = Aes.Create();
                    aes.Mode = CipherMode.ECB;
                    aes.Padding = PaddingMode.None;
                    aes.KeySize = 128;
                    aes.Key = DeriveVaultKey(devKey, baseKey);
                    using var decryptor = aes.CreateDecryptor();
                    var blockBuffer = new byte[SECTOR_SIZE];
                    var firstBlock = new byte[16];
                    foreach(var magic in pageMagics)
                    {
                        List<VaultPage> pages = new List<VaultPage>();

                        for(int ofs = 0; ofs + SECTOR_SIZE <= flash.Length; ofs += SECTOR_SIZE)
                        {
                            decryptor.TransformBlock(flash, ofs, 16, firstBlock, 0);

                            var pageMagic = ReadU32LE(firstBlock, 0);
                            if(pageMagic != magic) continue;

                            var dec = AESDecrypt(flash, ofs, decryptor, blockBuffer);

                            if(dec == null) continue;

                            var crc = ReadU32LE(dec, 4);
                            if(!checkCRC(crc, dec, 8, dec.Length - 8))
                            {
                                FormMain.Singleton.addLog($"WARNING - bad block CRC at offset {ofs}" + Environment.NewLine, System.Drawing.Color.Purple);
                                continue;
                            }

                            var seq = ReadU32LE(dec, 8);

                            pages.Add(new VaultPage
                            {
                                FlashOffset = ofs,
                                Seq = seq,
                                Data = dec
                            });
                        }
                        lock(obj)
                        {
                            if(pages.Count > bestCount)
                            {
                                bestCount = pages.Count;
                                bestPages = pages;
                            }
                        }
                    }
                });
            }
            time.Stop();
            if(bestPages == null)
            {
                FormMain.Singleton.addLog("Failed to extract Tuya keys - decryption failed" + Environment.NewLine, System.Drawing.Color.Orange);
                return false;
            }
            FormMain.Singleton.addLog($"Decryption took {time.ElapsedMilliseconds} ms" + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            var dataFlashOffset = bestPages.Min(x => x.FlashOffset);
            magicPosition = magicPosition < dataFlashOffset ? magicPosition : dataFlashOffset;
            FormMain.Singleton.addLog($"Tuya config extractor - magic is at {magicPosition} (0x{magicPosition:X}) " + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            if(bestPages.Count < 2)
            {
                FormMain.Singleton.addLog("Failed to extract Tuya keys - config not found" + Environment.NewLine, System.Drawing.Color.Orange);
                return false;
            }

            //bestPages.Sort((a, b) => a.Seq.CompareTo(b.Seq));
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            foreach(var p in bestPages) bw.Write(p.Data, 0, p.Data.Length);

            descryptedRaw = ms.ToArray();
            return true;
        }

        byte[] DeriveVaultKey(byte[] baseKey, byte[] deviceKey)
        {
            if(baseKey.Length != 16)
                throw new Exception($"baseKey.Length != 16 ({baseKey.Length}");
            var vaultKey = new byte[16];
            if(deviceKey.Length != 16)
            {
                for (int i = 0; i < 16; i++)
                {
                    int v = deviceKey[i & 3] + KEY_PART_2[i];
                    vaultKey[i] = (byte)((v + baseKey[i]) & 0xFF);
                }
                return vaultKey;
            }
            for(int i = 0; i < 16; i++) vaultKey[i] = (byte)((baseKey[i] + deviceKey[i]) & 0xFF);
            return vaultKey;
        }

        List<byte[]> FindDeviceKeys(byte[] flash)
        {
            var keys = new List<byte[]>();
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 128;
            aes.Key = Encoding.ASCII.GetBytes(KEY_MASTER);

            using var decryptor = aes.CreateDecryptor();
            var blockBuffer = new byte[SECTOR_SIZE];
            var dec = new byte[16];
            for(int ofs = 0; ofs + SECTOR_SIZE <= flash.Length; ofs += SECTOR_SIZE)
            {
                decryptor.TransformBlock(flash, ofs, 16, dec, 0);

                var pageMagic = ReadU32LE(dec, 0);
                if(pageMagic != MAGIC_FIRST_BLOCK) continue;

                dec = AESDecrypt(flash, ofs, decryptor, blockBuffer);
                if(dec == null) continue;

                var dk = new byte[16];
                Array.Copy(dec, 8, dk, 0, 16);

                var crc = ReadU32LE(dec, 4);
                if(checkCRC(crc, dk, 0, 16))
                {
                    keys.Add(dk);
                    magicPosition = ofs;
                }
                //else
                //{
                //    FormMain.Singleton.addLog("WARNING - bad firstblock crc" + Environment.NewLine, System.Drawing.Color.Purple);
                //}
            }
            return keys;
        }

        byte[] AESDecrypt(byte[] flash, int ofs, ICryptoTransform decryptor, byte[] buffer)
        {
            Array.Copy(flash, ofs, buffer, 0, SECTOR_SIZE);
            return decryptor.TransformFinalBlock(buffer, 0, SECTOR_SIZE);
        }

        public string getKeysHumanReadable(OBKConfig tg = null)
        {
            bool bHasBattery = false;
            string desc = "";
            if(tg != null && !string.IsNullOrWhiteSpace(tg.initCommandLine)) tg.initCommandLine += "\r\n";
            for(int i = 0; i < parms.Count; i++)
            {
                var p = parms[i];
                string key = p.Key;
                string value = p.Value;
                switch(key)
                {
                    case var k when Regex.IsMatch(k, "^led\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- LED (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.LED);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^netled\\d+_pin$"):
                    case "netled_pin":
                    case "wfst":
                    case "wfst_pin":
                        // some devices have netled1_pin, some have netled_pin
                        //int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- WiFi LED on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "bz_pin_pin"):
                    case "buzzer_io":
                        desc += "- Buzzer Pin (TODO) on P" + value + Environment.NewLine;
                        //tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "status_led_pin"):
                        desc += "- Status LED on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "remote_io"):
                        desc += "- RF Remote on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.RCRecv);
                        break;
                    case var k when Regex.IsMatch(k, "samp_sw_pin"):
                        desc += "- Battery Relay on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BAT_Relay);
                        break;
                    case var k when Regex.IsMatch(k, "samp_pin"):
                        desc += "- Battery ADC on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BAT_ADC);
                        break;
                    case var k when Regex.IsMatch(k, "i2c_scl_pin"):
                        desc += "- I2C SCL on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "i2c_sda_pin"):
                        desc += "- I2C SDA on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "alt_pin_pin"):
                        desc += "- ALT pin on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "one_wire_pin"):
                        desc += "- OneWire IO pin on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "backlit_io_pin"):
                        desc += "- Backlit IO pin on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.LED);
                        break;
                    case "max_V":
                        desc += "- Battery Max Voltage: " + value + Environment.NewLine;
                        bHasBattery = true;
                        break;
                    case "min_V":
                        desc += "- Battery Min Voltage: " + value + Environment.NewLine;
                        bHasBattery = true;
                        break;
                    case "rl":
                        desc += "- Relay (channel 0) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, 0);
                        break;
                    case var k when Regex.IsMatch(k, "^rl\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Relay (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^rl_on\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Bridge Relay On (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^rl_off\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Bridge Relay Off (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel_n);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case "bt_pin":
                    case "bt":
                    {
                        int number = 0;
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^k\\d+pin_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^bt\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^door\\d+_magt_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Door Sensor (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^onoff\\d+$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- TglChannelToggle (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.TglChanOnTgl);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case "gate_sensor_pin_pin":
                        desc += "- Door/Gate sensor on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        break;
                    case "basic_pin_pin":
                        // This will read 1 if there was a movement, at least on the sensor I have
                        // some devices have netled1_pin, some have netled_pin
                        desc += "- PIR sensor on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        break;
                    case "ele_pin":
                    case "epin":
                        desc += "- BL0937 ELE (CF) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937CF);
                        break;
                    case "vi_pin":
                    case "ivpin":
                        desc += "- BL0937 VI (CF1) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937CF1);
                        break;
                    case "sel_pin_pin":
                    case "ivcpin":
                        desc += "- BL0937 SEL on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937SEL);
                        break;
                    case "r_pin":
                        desc += "- LED Red (Channel 1) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 0);
                        break;
                    case "g_pin":
                        desc += "- LED Green (Channel 2) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 1);
                        break;
                    case "b_pin":
                        desc += "- LED Blue (Channel 3) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 2);
                        break;
                    case "c_pin":
                        desc += "- LED Cool (Channel 4) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 3);
                        break;
                    case "w_pin":
                        desc += "- LED Warm (Channel 5) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 4);
                        break;
                    case "mic":
                    case "micpin":
                        desc += "- Microphone (TODO) on P" + value + Environment.NewLine;
                        break;
                    case "ctrl_pin":
                        desc += "- Control Pin (TODO) on P" + value + Environment.NewLine;
                        break;
                    case "buzzer_pwm":
                        desc += "- Buzzer Frequency (TODO) is " + value + "Hz" + Environment.NewLine;
                        break;
                    case "irpin":
                    case "infrr":
                        desc += "- IR Receiver is on P" + value + "" + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.IRRecv);
                        break;
                    case "infre":
                        desc += "- IR Transmitter is on P" + value + "" + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.IRSend);
                        break;
                    case "reset_pin":
                        desc += "- Button is on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        break;
                    case "pwmhz":
                        desc += "- PWM Frequency " + value + "" + Environment.NewLine;
                        if(tg != null && int.TryParse(value, out _)) tg.initCommandLine += $"PWMFrequency {value}\r\n";
                        break;
                    case "pirsense_pin":
                        desc += "- PIR Sensitivity " + value + "" + Environment.NewLine;
                        break;
                    case "pirlduty":
                        desc += "- PIR Low Duty " + value + "" + Environment.NewLine;
                        break;
                    case "pirfreq":
                        desc += "- PIR Frequency " + value + "" + Environment.NewLine;
                        break;
                    case "pirmduty":
                        desc += "- PIR High Duty " + value + "" + Environment.NewLine;
                        break;
                    case "pirin_pin":
                        desc += "- PIR Input " + value + "" + Environment.NewLine;
                        break;
                    case "mosi":
                        desc += "- SPI MOSI " + value + "" + Environment.NewLine;
                        // assume SPI LED
                        tg?.setPinRole(value, PinRole.SM16703P_DIN);
                        break;
                    case "miso":
                        desc += "- SPI MISO " + value + "" + Environment.NewLine;
                        break;
                    case "SCL":
                        desc += "- SPI SCL " + value + "" + Environment.NewLine;
                        break;
                    case "CS":
                        desc += "- SPI CS " + value + "" + Environment.NewLine;
                        break;
                    case "total_bt_pin":
                        desc += "- Pair/Toggle All Button on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn_Tgl_All);
                        break;
                    default:
                        break;
                }
            }
            // LED
            string iicscl = getKeyValue("iicscl");
            string iicsda = getKeyValue("iicsda");
            if (iicscl.Length > 0 && iicsda.Length > 0)
            {
                string iicr = getKeyValue("iicr","-1");
                string iicg = getKeyValue("iicg", "-1");
                string iicb = getKeyValue("iicb", "-1");
                string iicc = getKeyValue("iicc", "-1");
                string iicw = getKeyValue("iicw", "-1");
                string ledType = "Unknown";
                string iicccur = getKeyValue("iicccur");
                string iicwcur = getKeyValue("iicwcur");
                string campere = getKeyValue("campere");
                string wampere = getKeyValue("wampere");
                string ehccur = getKeyValue("ehccur");
                string ehwcur = getKeyValue("ehwcur");
                string drgbcur = getKeyValue("drgbcur");
                string dwcur = getKeyValue("dwcur");
                string dccur = getKeyValue("dccur");
                string cjwcur = getKeyValue("cjwcur");
                string cjccur = getKeyValue("cjccur");
                string _2235ccur = getKeyValue("2235ccur");
                string _2235wcur = getKeyValue("2235wcur");
                string _2335ccur = getKeyValue("2335ccur");
                string kp58wcur = getKeyValue("kp58wcur");
                string kp58ccur = getKeyValue("kp58ccur");
                string currents = string.Empty;
                bool isExc = false;
                // use current (color/cw) setting
                if (ehccur.Length>0 || wampere.Length > 0 || iicccur.Length > 0)
                {
                    ledType = "SM2135";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = ehccur.Length > 0 ? Convert.ToInt32(ehccur) : iicccur.Length > 0 ? Convert.ToInt32(iicccur) : campere.Length > 0 ? Convert.ToInt32(campere) : 1;
                        cwcurrent = ehwcur.Length > 0 ? Convert.ToInt32(ehwcur) : iicwcur.Length > 0 ? Convert.ToInt32(iicwcur) : wampere.Length > 0 ? Convert.ToInt32(wampere) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"SM2135_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(ehccur.Length > 0 ? ehccur : iicccur.Length > 0 ? iicccur : campere.Length > 0 ? campere : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(ehwcur.Length > 0 ? ehwcur : iicwcur.Length > 0 ? iicwcur : wampere.Length > 0 ? wampere : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.SM2135DAT);
                    tg?.setPinRole(iicscl, PinRole.SM2135CLK);
                }
                else if (dccur.Length > 0)
                {
                    ledType = "BP5758D_";
                    var rgbcurrent = 0;
                    var wcurrent = 0;
                    var ccurrent = 0;
                    try
                    {
                        rgbcurrent = drgbcur.Length > 0 ? Convert.ToInt32(drgbcur) : 1;
                        wcurrent = dwcur.Length > 0 ? Convert.ToInt32(dwcur) : 1;
                        ccurrent = dccur.Length > 0 ? Convert.ToInt32(dccur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"BP5758D_Current {rgbcurrent} {Math.Max(wcurrent, ccurrent)}\r\n";
                    }
                    currents =  $"- RGB current is {(drgbcur.Length > 0 ? drgbcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Warm white current is {(dwcur.Length > 0 ? dwcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Cold white current is {(dccur.Length > 0 ? dccur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.BP5758D_DAT);
                    tg?.setPinRole(iicscl, PinRole.BP5758D_CLK);
                }
                else if (cjwcur.Length > 0)
                {
                    ledType = "BP1658CJ_";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = cjccur.Length > 0 ? Convert.ToInt32(cjccur) : 1;
                        cwcurrent = cjwcur.Length > 0 ? Convert.ToInt32(cjwcur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"BP1658CJ_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(cjccur.Length > 0 ? cjccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(cjwcur.Length > 0 ? cjwcur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.BP1658CJ_DAT);
                    tg?.setPinRole(iicscl, PinRole.BP1658CJ_CLK);
                }
                else if (_2235ccur.Length > 0)
                {
                    ledType = "SM2235";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = _2235ccur.Length > 0 ? Convert.ToInt32(_2235ccur) : 1;
                        cwcurrent = _2235wcur.Length > 0 ? Convert.ToInt32(_2235wcur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"SM2235_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(_2235ccur.Length > 0 ? _2235ccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(_2235wcur.Length > 0 ? _2235wcur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.SM2235DAT);
                    tg?.setPinRole(iicscl, PinRole.SM2235CLK);
                }
                else if (kp58wcur.Length > 0)
                {
                    ledType = "KP18058_";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = kp58wcur.Length > 0 ? Convert.ToInt32(kp58wcur) : 1;
                        cwcurrent = kp58ccur.Length > 0 ? Convert.ToInt32(kp58ccur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"KP18058_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(kp58wcur.Length > 0 ? kp58wcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(kp58ccur.Length > 0 ? kp58ccur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.KP18058_DAT);
                    tg?.setPinRole(iicscl, PinRole.KP18058_CLK);
                }
                else
                {

                }
                string dat_name = ledType + "DAT";
                string clk_name = ledType + "CLK";
                desc += "- " + dat_name + " on P" + iicsda + Environment.NewLine;
                desc += "- " + clk_name + " on P" + iicscl + Environment.NewLine;
                string map = "" + iicr + " " + iicg + " " + iicb + " " + iicc + " " + iicw;
                isExc = false;
                try
                {
                    map = $"{Convert.ToInt32(iicr)} {Convert.ToInt32(iicg)} {Convert.ToInt32(iicb)} {Convert.ToInt32(iicc)} {Convert.ToInt32(iicw)}";
                }
                catch
                {
                    isExc = true;
                }
                finally
                {
                    if(tg != null && !isExc) tg.initCommandLine += $"LED_Map {map}\r\n";
                }
                desc += "- LED remap is " + map + Environment.NewLine;
                desc += currents;
            }
            if (desc.Length > 0)
            {
                desc = "Device configuration, as extracted from Tuya: " + Environment.NewLine + desc;
            }
            else
            {
                desc = "Sorry, no meaningful pins data found. This device may be TuyaMCU or a custom one with no Tuya config data." + Environment.NewLine;
            }
            if (bHasBattery)
            {
                desc += "Device seems to use Battery Driver. See more details here: https://www.elektroda.com/rtvforum/topic3959103.html" + Environment.NewLine;
            }
            var baud = this.findKeyValue("baud");
            if(baud != null)
            {
                desc += "Baud keyword found, this device may be TuyaMCU or BL0942. Baud value is " + baud.Value + Environment.NewLine;
            }
            var kp = this.findKeyValue("module");
            kp ??= this.findKeyContaining("module");
            if (kp != null)
            {
                BKType type = TuyaModules.getTypeForModuleName(kp.Value);
                desc += "Device seems to be using " + kp.Value + " module";
                if(type != BKType.Invalid)
                {
                    desc += ", which is using " + type + ".";
                }
                else
                {
                    desc += ".";
                }
            }
            else
            {
                desc += "No module information found.";
            }
            kp = findKeyValue("em_sys_env");
            if(kp != null)
            {
                desc += Environment.NewLine;
                BKType type = TuyaModules.getTypeForPlatformName(kp.Value);
                desc += $"Device internal platform - {kp.Value}";
                if(type != BKType.Invalid)
                {
                    desc += ", equals " + type + ".";
                }
                else
                {
                    desc += ".";
                }
            }
            desc += Environment.NewLine;
            void printposdevice(string device)
            {
                desc += $"And the Tuya section starts at {getMagicPositionDecAndHex()}, which is a default {device} offset." + Environment.NewLine;
            }
            switch(magicPosition)
            {
                case 0:
                case 0x1000:
                    break;
                case USUAL_BK7231_MAGIC_POSITION:
                    desc += $"And the Tuya section starts, as usual, at {getMagicPositionDecAndHex()}" + Environment.NewLine;
                    break;
                case USUAL_BK_NEW_XR806_MAGIC_POSITION:
                    printposdevice("T1/XR806 and some T34");
                    break;
                case USUAL_RTLC_ECR6600_MAGIC_POSITION:
                    printposdevice("RTL8720C and ECR6600");
                    break;
                case USUAL_RTLB_XR809_MAGIC_POSITION:
                    printposdevice("RTL8710B/XR809");
                    break;
                case USUAL_T3_MAGIC_POSITION:
                    printposdevice("T3/BK7236");
                    break;
                case USUAL_T5_MAGIC_POSITION:
                    printposdevice("T5/BK7258");
                    break;
                case USUAL_RTLD_MAGIC_POSITION:
                    printposdevice("4MB RTL8720D");
                    break;
                case USUAL_WBRG1_MAGIC_POSITION:
                    printposdevice("8MB RTL8720D/WBRG1");
                    break;
                case USUAL_LN882H_MAGIC_POSITION:
                    printposdevice("LN882H");
                    break;
                case USUAL_TR6260_MAGIC_POSITION:
                    printposdevice("TR6260");
                    break;
                case USUAL_W800_MAGIC_POSITION:
                    printposdevice("W800");
                    break;
                case USUAL_LN8825_MAGIC_POSITION:
                    printposdevice("LN8825B");
                    break;
                case USUAL_RTLCM_MAGIC_POSITION:
                    printposdevice("RTL8720CM");
                    break;
                case USUAL_BK7252_MAGIC_POSITION:
                    printposdevice("BK7252");
                    break;
                default:
                    desc += "And the Tuya section starts at an UNCOMMON POSITION " + getMagicPositionDecAndHex() + Environment.NewLine;
                    break;

            }
            return desc;
        }
        public string getKeyValue(string key, string sdefault = "")
        {
            for (int i = 0; i < parms.Count; i++)
            {
                if (parms[i].Key == key)
                {
                    return parms[i].Value;
                }
            }
            return sdefault;
        }
        public string getKeysAsJSON()
        {
            string r = "{" + Environment.NewLine;
            for (int i = 0; i < parms.Count; i++)
            {
                var p = parms[i];
                r += "\t\""+p.Key + "\":\"" + p.Value + "\"";
                if (i != parms.Count-1)
                {
                    r += ",";
                }
                r += Environment.NewLine;
            }
            r += "}" + Environment.NewLine;
            return r;
        }
        public bool extractKeys()
        {
            var KVs = ParseVault();
            var KVs_Deduped = KVs
                .GroupBy(x => x.Key)
                .Select(g => g.OrderByDescending(x => x.IsCheckSumCorrect).First())
                .GroupBy(x => Convert.ToBase64String(x.Value))
                .Select(g => g.OrderByDescending(x => x.KeyId).First())
                .ToList();

            byte[] str = KVs_Deduped.FirstOrDefault(x => x.Key == "user_param_key" && x.IsCheckSumCorrect == true)?.Value ??
                KVs_Deduped.FirstOrDefault(x => x.Key == "baud_cfg" && x.IsCheckSumCorrect == true)?.Value;
            byte[] em_sys_env = KVs_Deduped.FirstOrDefault(x => x.Key == "em_sys_env" && x.IsCheckSumCorrect == true)?.Value;
            // old method. Works better when user_param_key is corrupted (bad checksum)
            if(str == null)
            {
                if(KVs.Any(x => x.Key == "user_param_key"))
                    FormMain.Singleton.addLog("Tuya user_param_key is corrupted, using old extraction method" + Environment.NewLine, System.Drawing.Color.Orange);
                int first_at = 0;
                int keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("user_param_key"));
                //var t = Encoding.ASCII.GetString(descryptedRaw.Where(x => x != 0).ToArray());
                if (keys_at == -1)
                {
                    int jsonAt = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("Jsonver"));
                    if(jsonAt != -1)
                    {
                        keys_at = MiscUtils.findFirstRev(descryptedRaw, (byte)'{', jsonAt);
                    }
                    if (keys_at == -1)
                    {
                        keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("ap_s{"));
                
                        if(keys_at == -1)
                        {
                            keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("baud_cfg"));
                            if(keys_at == -1)
                            {
                                // ln882h hack
                                int jsonInOrig = MiscUtils.indexOf(original, Encoding.ASCII.GetBytes("crc:"));
                                if(jsonInOrig != -1 && original[jsonInOrig + 6] == ',' && original[jsonInOrig + 7] == '}')
                                {
                                    keys_at = jsonInOrig;
                                    descryptedRaw = original;
                                    while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                                        keys_at--;
                                    keys_at--;
                                }
                                // extract at least something
                                if(keys_at == -1)
                                {
                                    keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("gw_bi"));
                                    if(keys_at == -1)
                                    {
                                        FormMain.Singleton.addLog("Failed to extract Tuya keys - no json start found" + Environment.NewLine, System.Drawing.Color.Orange);
                                        return true;
                                    }
                                }
                            }
                            while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                                keys_at++;
                            keys_at++;
                            first_at = keys_at;
                        }
                        else
                        {
                            first_at = keys_at + 5;
                        }
                    }
                    else
                    {
                        first_at = keys_at + 1;
                    }
                }
                else
                {
                    while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                        keys_at++;
                    keys_at++;
                    first_at = keys_at;
                }
                int stopAT = MiscUtils.findMatching(descryptedRaw, (byte)'}', (byte)'{', first_at);
                if (stopAT == -1)
                {
                    //FormMain.Singleton.addLog("Failed to extract Tuya keys - no json end found" + Environment.NewLine, System.Drawing.Color.Purple);
                    // return true;
                    stopAT = descryptedRaw.Length;
                }
                str = MiscUtils.subArray(descryptedRaw, first_at, stopAT - first_at);
            }
            // There is still some kind of Tuya paging here,
            // let's skip it in a quick and dirty way
            string asciiString = bytesToAsciiStr(str);
            string[] pairs = asciiString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < pairs.Length; i++)
            {
                string []kp = pairs[i].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if(kp.Length < 2)
                {
                    FormMain.Singleton.addLog("Malformed key? " + Environment.NewLine, System.Drawing.Color.Orange);

                    continue;
                }
                string skey = (kp.Length > 2 && kp[1].Contains('[')) ? kp[1] : kp[0];
                string svalue = kp[kp.Length - 1];
                skey = skey.Trim(new char[] { '"' }).Replace("\"", "").Replace("[", "").Replace("{", "");
                svalue = svalue.Trim(new char[] { '"' }).Replace("\"", "").Replace("}", "");
                //parms.Add(skey, svalue);
                if (findKeyValue(skey) == null)
                {
                    KeyValue kv = new KeyValue(skey, svalue);
                    parms.Add(kv);
                }
            }
            if(em_sys_env != null && !isFullOf(em_sys_env, 0x00))
            {
                KeyValue kv = new KeyValue("em_sys_env", bytesToAsciiStr(em_sys_env));
                parms.Add(kv);
            }
            FormMain.Singleton.addLog("Tuya keys extraction has found " + parms.Count + " keys" + Environment.NewLine, System.Drawing.Color.Black);

            return false;
        }
        KeyValue findKeyContaining(string key)
        {
            for (int i = 0; i < parms.Count; i++)
            {
                if (parms[i].Key.Contains(key))
                    return parms[i];
            }
            return null;
        }
        KeyValue findKeyValue(string key)
        {
            for(int i = 0; i < parms.Count; i++)
            {
                if (parms[i].Key == key)
                    return parms[i];
            }
            return null;
        }
        bool checkCRC(uint expected, byte [] dat, int ofs, int len)
        {
            uint n = 0;
            for(int i = 0; i < len; i++)
            {
                n += dat[ofs + i];
            }
            n = n & 0xFFFFFFFF;
            if (n == expected)
                return true;
            return false;
        }
        string bytesToAsciiStr(byte[] data)
        {
            var asciiString = "";
            for(int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b < 32)
                    continue;
                if (b > 127)
                    continue;
                char ch = (char)b;
                asciiString += ch;
            }
            return asciiString;
        }
    }
}
