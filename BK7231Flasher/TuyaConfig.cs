using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BK7231Flasher
{
    public class TuyaKeysText
    {
        public string textDescription;
    }
    public class TuyaConfig
    {
        // thanks to kmnh/bk7231 tools for figuring out this format
        static string KEY_MASTER = "qwertyuiopasdfgh";
        static int SECTOR_SIZE = 4096;
        static uint MAGIC_FIRST_BLOCK = 0x13579753;
        static uint MAGIC_NEXT_BLOCK = 0x98761234;
        static byte[] KEY_PART_1 = Encoding.ASCII.GetBytes("8710_2M");
        static byte[] KEY_PART_2 = Encoding.ASCII.GetBytes("HHRRQbyemofrtytf");
        static byte[] MAGIC_CONFIG_START = new byte[] { 0x46, 0xDC, 0xED, 0x0E, 0x67, 0x2F, 0x3B, 0x70, 0xAE, 0x12, 0x76, 0xA3, 0xF8, 0x71, 0x2E, 0x03 };

        public static string getMagicConstantStartString()
        {
            string s = "";
            for(int i = 0; i < MAGIC_CONFIG_START.Length; i++)
            {
                s += MAGIC_CONFIG_START[i].ToString("X2");
            }
            return s;
        }
        MemoryStream decrypted;
        byte[] descryptedRaw;
        // Dictionary<string, string> parms = new Dictionary<string, string>();
        List<KeyValue> parms = new List<KeyValue>();

        public bool fromFile(string fname)
        {
            using (var fs = new FileStream(fname, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[fs.Length]; 
                fs.Read(buffer, 0, buffer.Length);
                return fromBytes(buffer);
            }
        }
        public byte []myDecrypt(byte [] data, int baseOffset, int blockIndex, byte [] key)
        {
            byte[] decryptedData = null;
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.ECB;
                    aes.Key = key;
                    aes.Padding = PaddingMode.Zeros;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    int readOfs = baseOffset + SECTOR_SIZE * blockIndex;
                    int rem = data.Length - readOfs;
                    using (MemoryStream msDecrypt = new MemoryStream(data, readOfs, SECTOR_SIZE))
                    {
                        using (MemoryStream msPlain = new MemoryStream())
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] buffer = new byte[1024];
                                int bytesRead;
                                while ((bytesRead = csDecrypt.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    msPlain.Write(buffer, 0, bytesRead);
                                }
                            }
                            decryptedData = msPlain.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return decryptedData;
        }
        public bool fromBytes(byte[] data)
        {
            int needle = MiscUtils.indexOf(data, MAGIC_CONFIG_START);
            if(needle < 0)
            {
                FormMain.Singleton.addLog("Failed to extract Tuya keys - magic constant header not found in binary" + Environment.NewLine, System.Drawing.Color.Yellow);
                return true;
            }
            needle -= 32;
            FormMain.Singleton.addLog("Tuya config extractor - magic is at " + needle +" " + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            byte[] key = null;

            decrypted = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(decrypted);
            byte[] first = myDecrypt(data, needle, 0, Encoding.ASCII.GetBytes(KEY_MASTER));
            using (BinaryReader br = new BinaryReader(new MemoryStream(first)))
            {
                UInt32 mag = br.ReadUInt32();
                if (mag != MAGIC_FIRST_BLOCK)
                {
                    FormMain.Singleton.addLog("Failed to extract Tuya keys - bad firstblock header" + Environment.NewLine, System.Drawing.Color.Yellow);
                    return true;
                }
                uint crc = br.ReadUInt32();
                key = br.ReadBytes(16);
                if (checkCRC(crc, key, 0, key.Length) == false)
                {
                    FormMain.Singleton.addLog("Failed to extract Tuya keys - bad firstblock crc" + Environment.NewLine, System.Drawing.Color.Yellow);
                    return true;
                }
                key = makeSecondaryKey(key);
                bw.Write(first, 8, first.Length - 8);
            }
            for (int blockIndex = 1; blockIndex < 500; blockIndex++)
            {
                byte[] next = myDecrypt(data, needle, blockIndex, key);
                if (next == null)
                {
                    break;
                }
                using (BinaryReader br = new BinaryReader(new MemoryStream(next)))
                {
                    UInt32 mag = br.ReadUInt32();
                    if (mag != MAGIC_NEXT_BLOCK && mag != 324478635)
                    {
                        // it seems that TuyaOS3 binaries have here 324478635?
                        //FormMain.Singleton.addLog("Failed to extract Tuya keys - bad nextblock header" + Environment.NewLine, System.Drawing.Color.Yellow);
                        //return true;
                        FormMain.Singleton.addLog("WARNING - strange nextblock header " + mag.ToString("X4") + Environment.NewLine, System.Drawing.Color.Yellow);

                    }
                    uint crc = br.ReadUInt32();
                    if (checkCRC(crc, next, 8, next.Length - 8) == false)
                    {
                        FormMain.Singleton.addLog("WARNING - bad nextblock CRC" + Environment.NewLine, System.Drawing.Color.Yellow);

                        //   FormMain.Singleton.addLog("Failed to extract Tuya keys - bad nextblock CRC" + Environment.NewLine, System.Drawing.Color.Yellow);
                        //  return true;
                    }
                    bw.Write(next, 8, next.Length - 8);
                }
            }
            descryptedRaw = decrypted.ToArray();
            string debugName = "lastRawDecryptedStrings.bin";
            FormMain.Singleton.addLog("Saving debug Tuya decryption data to " + debugName+"" + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            File.WriteAllBytes(debugName, descryptedRaw);
            return false;
        }
        public string getKeysHumanReadable(OBKConfig tg = null)
        {
            string desc = "";
            for (int i = 0; i < parms.Count; i++)
            {
                var p = parms[i];
                string key = p.Key;
                string value = p.Value;
                if (Regex.IsMatch(key, "^led\\d+_pin$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- LED (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.LED);
                        tg.setPinChannel(value, number);
                    }
                }
                else if (Regex.IsMatch(key, "^netled\\d+_pin$"))
                {
                    // some devices have netled1_pin, some have netled_pin
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- WiFi LED on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.WifiLED_n);
                    }
                }
                else if (Regex.IsMatch(key, "^rl\\d+_pin$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Relay (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Rel);
                        tg.setPinChannel(value, number);
                    }
                }
                else if (Regex.IsMatch(key, "^bt\\d+_pin$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Btn);
                        tg.setPinChannel(value, number);
                    }
                }
                else if(Regex.IsMatch(key, "^door\\d+_magt_pin$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Door Sensor (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.dInput);
                        tg.setPinChannel(value, number);
                    }
                }
                else if(Regex.IsMatch(key, "^onoff\\d+$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- TglChannelToggle (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.TglChanOnTgl);
                        tg.setPinChannel(value, number);
                    }
                }
                else if (key == "netled_pin")
                {
                    // some devices have netled1_pin, some have netled_pin
                    desc += "- WiFi LED on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.WifiLED_n);
                    }
                }
                else if(key == "ele_pin")
                {
                    desc += "- BL0937 ELE on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BL0937CF1);
                    }
                }
                else if (key == "vi_pin")
                {
                    desc += "- BL0937 VI on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BL0937CF);
                    }
                }
                else if (key == "sel_pin_pin")
                {
                    desc += "- BL0937 SEL on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BL0937SEL);
                    }
                }
                else if (key == "r_pin")
                {
                    desc += "- LED Red (Channel 1) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.PWM);
                        tg.setPinChannel(value, 0);
                    }
                }
                else if (key == "g_pin")
                {
                    desc += "- LED Green (Channel 2) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.PWM);
                        tg.setPinChannel(value, 1);
                    }
                }
                else if (key == "b_pin")
                {
                    desc += "- LED Blue (Channel 3) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.PWM);
                        tg.setPinChannel(value, 2);
                    }
                }
                else if (key == "c_pin")
                {
                    desc += "- LED Cool (Channel 4) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.PWM);
                        tg.setPinChannel(value, 3);
                    }
                }
                else if (key == "w_pin")
                {
                    desc += "- LED Warm (Channel 5) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.PWM);
                        tg.setPinChannel(value, 4);
                    }
                }
                else if (key == "ctrl_pin")
                {
                    desc += "- Control Pin (TODO) on P" + value + Environment.NewLine;
                }
                else if (key == "total_bt_pin")
                {
                    desc += "- Pair/Toggle All Pin on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Btn_Tgl_All);
                    }
                }
                else
                {

                }
            }
            // LED
            string iicscl = getKeyValue("iicscl");
            string iicsda = getKeyValue("iicsda");
            if (iicscl.Length > 0 && iicsda.Length > 0)
            {
                string iicr = getKeyValue("iicr","?");
                string iicg = getKeyValue("iicg", "?");
                string iicb = getKeyValue("iicb", "?");
                string iicc = getKeyValue("iicc", "?");
                string iicw = getKeyValue("iicw", "?");
                string map = "" + iicr + " " + iicg + " " + iicb + " " + iicc + " " + iicw;
                string ledType = "Unknown";
                string iicccur = getKeyValue("iicccur");
                string wampere = getKeyValue("wampere");
                string ehccur = getKeyValue("ehccur");
                string dccur = getKeyValue("dccur");
                string cjwcur = getKeyValue("cjwcur");
                string _2235ccur = getKeyValue("2235ccur");
                string _2335ccur = getKeyValue("2335ccur");
                // use current (color/cw) setting
                if (ehccur.Length>0 || wampere.Length > 0 || iicccur.Length > 0)
                {
                    ledType = "SM2135";
                }
                else if (dccur.Length > 0)
                {
                    ledType = "BP5758D_";
                }
                else if (cjwcur.Length > 0)
                {
                    ledType = "BP1658CJ_";
                }
                else if (_2235ccur.Length > 0)
                {
                    ledType = "SM2235";
                }
                else if (_2335ccur.Length > 0)
                {
                    // fallback
                    ledType = "SM2235";
                }
                else
                {

                }
                string dat_name = ledType + "DAT";
                string clk_name = ledType + "CLK";
                desc += "- " + dat_name + " on P" + iicsda + Environment.NewLine;
                desc += "- " + clk_name + " on P" + iicscl + Environment.NewLine;
                desc += "- LED remap is " + map + Environment.NewLine;
            }
            if (desc.Length > 0)
            {
                desc = "Device configuration, as extracted from Tuya: " + Environment.NewLine + desc;
            }
            else
            {
                desc = "Sorry, no meaningful pins data found. This device may be TuyaMCU or a custom one with no Tuya config data." + Environment.NewLine;
            }
            var kp = this.findKeyValue("module");
            if(kp == null)
            {
                kp = this.findKeyContaining("module");
            }
            if (kp != null)
            {
                BKType type = TuyaModules.getTypeForModuleName(kp.Value);
                desc += "Device seems to be using " + kp.Value + " module";
                if(type == BKType.BK7231N || type == BKType.BK7231T)
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
            desc += Environment.NewLine;
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
            int first_at = 0;
            int keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("ap_s{"));
            if (keys_at == -1)
            {
                int jsonAt = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("Jsonver"));
                if(jsonAt != -1)
                {
                    keys_at = MiscUtils.findFirstRev(descryptedRaw, (byte)'{', jsonAt);
                }
                if (keys_at == -1)
                {
                    FormMain.Singleton.addLog("Failed to extract Tuya keys - no json start found" + Environment.NewLine, System.Drawing.Color.Orange);
                    return true;
                }
                else
                {
                    first_at = keys_at + 1;
                }
            }
            else
            {
                first_at = keys_at + 5;
            }
            int stopAT = MiscUtils.findMatching(descryptedRaw, (byte)'}', (byte)'{', keys_at);
            if (stopAT == -1)
            {
                //FormMain.Singleton.addLog("Failed to extract Tuya keys - no json end found" + Environment.NewLine, System.Drawing.Color.Yellow);
                // return true;
                stopAT = descryptedRaw.Length;
            }
            byte[] str = MiscUtils.subArray(descryptedRaw, first_at, stopAT - first_at);
            string asciiString;
            // There is still some kind of Tuya paging here,
            // let's skip it in a quick and dirty way
#if false
            str = MiscUtils.stripBinary(str);
            asciiString = Encoding.ASCII.GetString(str);
#else
            asciiString = "";
            for(int i = 0; i < str.Length; i++)
            {
                byte b = str[i];
                if (b < 32)
                    continue;
                if (b > 127)
                    continue;
                char ch = (char)b;
                asciiString += ch;
            }
#endif
            string[] pairs = asciiString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < pairs.Length; i++)
            {
                string []kp = pairs[i].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if(kp.Length < 2)
                {
                    FormMain.Singleton.addLog("Malformed key? " + Environment.NewLine, System.Drawing.Color.Orange);

                    continue;
                }
                string skey = kp[0];
                string svalue = kp[1];
                skey = skey.Trim(new char[] { '"' }).Replace("\"", "");
                svalue = svalue.Trim(new char[] { '"' }).Replace("\"", "");
                //parms.Add(skey, svalue);
                if (findKeyValue(skey) == null)
                {
                    KeyValue kv = new KeyValue(skey, svalue);
                    parms.Add(kv);
                }
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
        byte[] makeSecondaryKey(byte[] innerKey)
        {
            byte[] key = new byte[0x10];
            for (int i = 0; i < 16; i++)
            {
                key[i] = (byte)(KEY_PART_1[i & 3] + KEY_PART_2[i]);
            }
            for (int i = 0; i < 16; i++)
            {
                key[i] = (byte)((key[i] + innerKey[i]) % 256);
            }
            return key;
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
    }
}
