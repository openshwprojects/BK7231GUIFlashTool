using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BK7231Flasher
{
    public class TuyaKeysText
    {
        public string textDescription;
    }
    public class TuyaConfig
    {
        // thanks to kmnh & Kuba bk7231 tools for figuring out this format
        static string KEY_MASTER = "qwertyuiopasdfgh";
        static int SECTOR_SIZE = 4096;
        static uint MAGIC_FIRST_BLOCK = 0x13579753;
        static uint MAGIC_NEXT_BLOCK = 0x98761234;
        // 8721D for RTL8720D devices, 8711AM_4M for WRG1. Not known for W800, ECR6600, RTL8720CM, BK7252...
        public static byte[] KEY_PART_1 = Encoding.ASCII.GetBytes("8710_2M");
        static byte[] KEY_PART_2 = Encoding.ASCII.GetBytes("HHRRQbyemofrtytf");
        static byte[] MAGIC_CONFIG_START = new byte[] { 0x46, 0xDC, 0xED, 0x0E, 0x67, 0x2F, 0x3B, 0x70, 0xAE, 0x12, 0x76, 0xA3, 0xF8, 0x71, 0x2E, 0x03 };
        // TODO: check more bins with this offset
        // hex 0x1EE000
        // While flash is 0x200000, so we have at most 0x12000 bytes there...
        // So it's 0x12 sectors (18 sectors)
        const int USUAL_BK7231_MAGIC_POSITION = 2023424;
        const int USUAL_BK_NEW_XR806_MAGIC_POSITION = 2052096;
        const int USUAL_RTLB_XR809_MAGIC_POSITION = 2011136;
        const int USUAL_RTLC_MAGIC_POSITION = 1933312;
        const int USUAL_RTLD_MAGIC_POSITION = 3989504;
        const int USUAL_WBRG1_MAGIC_POSITION = 8220672;
        const int USUAL_T3_MAGIC_POSITION = 3997696;
        // offset is 'incorrect'. data begins at 0x7dd000, but magic is after it, at 0x7fd000. Same situation is in WBR1D dump and W800 config dump.
        const int USUAL_T5_MAGIC_POSITION = 8245248;//8376320;
        const int USUAL_LN882H_MAGIC_POSITION = 2015232;
        const int USUAL_TR6260_MAGIC_POSITION = 860160;
        const int USUAL_ECR6600_MAGIC_POSITION = 1921024;
        const int USUAL_W800_MAGIC_POSITION = 1835008;

        internal static int getMagicOffset(BKType type)
        {
            // pretty useless for RTLs, since littlefs overwrites it.
            switch(type)
            {
                case BKType.RTL8710B:
                    return USUAL_RTLB_XR809_MAGIC_POSITION;
                case BKType.RTL87X0C:
                    return USUAL_RTLC_MAGIC_POSITION;
                case BKType.RTL8720D:
                    return USUAL_RTLD_MAGIC_POSITION;
                case BKType.LN882H:
                    return USUAL_LN882H_MAGIC_POSITION;
                case BKType.BK7236:
                    return USUAL_T3_MAGIC_POSITION;
                case BKType.BK7238:
                    return USUAL_BK_NEW_XR806_MAGIC_POSITION;
                case BKType.BK7258:
                    return USUAL_T5_MAGIC_POSITION;
                case BKType.ECR6600:
                    return USUAL_ECR6600_MAGIC_POSITION;
                default:
                    return USUAL_BK7231_MAGIC_POSITION;
            }
        }
        public static int getMagicSize(BKType type)
        {
            switch(type)
            {
                case BKType.RTL8710B:
                    return 0x200000 - USUAL_RTLB_XR809_MAGIC_POSITION;
                case BKType.RTL87X0C:
                    return 0x200000 - USUAL_RTLC_MAGIC_POSITION;
                case BKType.RTL8720D:
                    return 0x400000 - USUAL_RTLD_MAGIC_POSITION;
                case BKType.LN882H:
                    return 0x200000 - USUAL_LN882H_MAGIC_POSITION;
                case BKType.BK7236:
                    return 0x400000 - USUAL_T3_MAGIC_POSITION;
                case BKType.BK7238:
                    return 0x200000 - USUAL_BK_NEW_XR806_MAGIC_POSITION;
                case BKType.BK7258:
                    return 0x800000 - USUAL_T5_MAGIC_POSITION;
                case BKType.ECR6600:
                    return 0x200000 - USUAL_ECR6600_MAGIC_POSITION;
                default:
                    return 0x200000 - USUAL_BK7231_MAGIC_POSITION;
            }
        }
        public static string getMagicConstantStartString()
        {
            string s = "";
            for(int i = 0; i < MAGIC_CONFIG_START.Length; i++)
            {
                s += MAGIC_CONFIG_START[i].ToString("X2");
            }
            return s;
        }
        int magicPosition;
        MemoryStream decrypted;
        byte[] descryptedRaw;
        // Dictionary<string, string> parms = new Dictionary<string, string>();
        List<KeyValue> parms = new List<KeyValue>();

        public int getMagicPosition()
        {
            return magicPosition;
        }
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
            bool bSaveDebug;
            bSaveDebug = true;
            string saveDir = "DebugAES/";
            if (bSaveDebug)
            {
                if (Directory.Exists(saveDir) == false)
                {
                    Directory.CreateDirectory(saveDir);
                }
            }
            byte[] decryptedData = null;
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.ECB;
                    aes.Key = key;
                    aes.Padding = PaddingMode.Zeros;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    if (bSaveDebug)
                    {
                        File.WriteAllBytes(saveDir + "used_IV.bin", aes.IV);
                        string hexString = "";
                        foreach (byte b in aes.IV)
                        {
                            hexString += b.ToString("X2");
                        }
                        File.WriteAllText(saveDir + "used_IV.txt", hexString);
                    }
                    int readOfs = baseOffset + SECTOR_SIZE * blockIndex;
                    int rem = data.Length - readOfs;
                    using (MemoryStream msDecrypt = new MemoryStream(data, readOfs, SECTOR_SIZE))
                    {
                        if (bSaveDebug)
                        {
                            byte[] msDecryptB = MiscUtils.subArray(data, readOfs, SECTOR_SIZE);
                            File.WriteAllBytes(saveDir + "tuya_" + blockIndex + ".bin", msDecryptB);
                            string hexString = "";
                            foreach (byte b in msDecryptB)
                            {
                                hexString += b.ToString("X2");
                            }
                            File.WriteAllText(saveDir + "tuya_" + blockIndex + ".txt", hexString);
                        }
                        if (bSaveDebug)
                        {
                            byte[] msDecryptB = key;
                            File.WriteAllBytes(saveDir + "key_" + blockIndex + ".bin", msDecryptB);
                            string hexString = "";
                            foreach (byte b in msDecryptB)
                            {
                                hexString += b.ToString("X2");
                            }
                            File.WriteAllText(saveDir + "key_" + blockIndex + ".txt", hexString);
                        }
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
                            if (bSaveDebug)
                            {
                                File.WriteAllBytes(saveDir + "decoded_" + blockIndex + ".bin", decryptedData);
                                string hexString = "";
                                foreach (byte b in decryptedData)
                                {
                                    hexString += b.ToString("X2");
                                }
                                File.WriteAllText(saveDir + "decoded_" + blockIndex + ".txt", hexString);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return decryptedData;
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
            this.bLastBinaryOBKConfig = false;
            this.bGivenBinaryIsFullOf0xff = false;

            int needle = MiscUtils.indexOf(data, MAGIC_CONFIG_START);
            if(needle < 0)
            {
                FormMain.Singleton.addLog("Failed to extract Tuya keys - magic constant header not found in binary" + Environment.NewLine, System.Drawing.Color.Purple);

                if (MiscUtils.isFullOf(data, 0xff))
                {
                    FormMain.Singleton.addLog("It seems that dragged binary is full of 0xff, someone must have erased the flash" + Environment.NewLine, System.Drawing.Color.Purple);
                    bGivenBinaryIsFullOf0xff = true;
                }
                if(data.Length>3 && data[0] == (byte)'C' && data[1] == (byte)'F' && data[2] == (byte)'G')
                {
                    FormMain.Singleton.addLog("It seems that dragged binary is OBK config, not a Tuya one" + Environment.NewLine, System.Drawing.Color.Purple);
                    this.bLastBinaryOBKConfig = true;
                }
                return true;
            }
            needle -= 32;
            FormMain.Singleton.addLog("Tuya config extractor - magic is at " + needle +" " + Environment.NewLine, System.Drawing.Color.DarkSlateGray);
            magicPosition = needle;

            byte[] key = null;

            decrypted = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(decrypted);
            byte[] first = myDecrypt(data, needle, 0, Encoding.ASCII.GetBytes(KEY_MASTER));
            using (BinaryReader br = new BinaryReader(new MemoryStream(first)))
            {
                UInt32 mag = br.ReadUInt32();
                if (mag != MAGIC_FIRST_BLOCK)
                {
                    FormMain.Singleton.addLog("Failed to extract Tuya keys - bad firstblock header" + Environment.NewLine, System.Drawing.Color.Purple);
                    return true;
                }
                uint crc = br.ReadUInt32();
                key = br.ReadBytes(16);
                if (checkCRC(crc, key, 0, key.Length) == false)
                {
                    FormMain.Singleton.addLog("Failed to extract Tuya keys - bad firstblock crc" + Environment.NewLine, System.Drawing.Color.Purple);
                    return true;
                }
                key = makeSecondaryKey(key);
                bw.Write(first, 8, first.Length - 8);
            }
            int blockIndex = 1;
            for (; blockIndex < 500; blockIndex++)
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
                        //FormMain.Singleton.addLog("Failed to extract Tuya keys - bad nextblock header" + Environment.NewLine, System.Drawing.Color.Purple);
                        //return true;
                        FormMain.Singleton.addLog("WARNING - strange nextblock header " + mag.ToString("X4") + Environment.NewLine, System.Drawing.Color.Purple);

                    }
                    uint crc = br.ReadUInt32();
                    if (checkCRC(crc, next, 8, next.Length - 8) == false)
                    {
                        FormMain.Singleton.addLog("WARNING - bad nextblock CRC" + Environment.NewLine, System.Drawing.Color.Purple);

                        //   FormMain.Singleton.addLog("Failed to extract Tuya keys - bad nextblock CRC" + Environment.NewLine, System.Drawing.Color.Purple);
                        //  return true;
                    }
                    //uint len = br.ReadUInt32();
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
            bool bHasBattery = false;
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
                else if (Regex.IsMatch(key, "bz_pin_pin"))
                {// https://www.elektroda.com/rtvforum/viewtopic.php?p=21070110#21070110
                    desc += "- Buzzer on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        //tg.setPinRole(value, PinRole.WifiLED_n);
                    }
                }
                else if (Regex.IsMatch(key, "status_led_pin"))
                {
                    desc += "- Status LED on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.WifiLED_n);
                    }
                }
                else if (Regex.IsMatch(key, "remote_io"))
                {
                    desc += "- RF Remote on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.RCRecv);
                    }
                }
                else if (Regex.IsMatch(key, "samp_sw_pin"))
                {
                    desc += "- Battery Relay on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BAT_Relay);
                    }
                }
                else if (Regex.IsMatch(key, "samp_pin"))
                {
                    desc += "- Battery ADC on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BAT_ADC);
                    }
                }
                else if (Regex.IsMatch(key, "i2c_scl_pin"))
                {
                    desc += "- I2C SCL on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                    }
                }
                else if (Regex.IsMatch(key, "i2c_sda_pin"))
                {
                    desc += "- I2C SDA on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                    }
                }
                else if (Regex.IsMatch(key, "alt_pin_pin"))
                {
                    desc += "- ALT pin on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                    }
                }
                else if (Regex.IsMatch(key, "one_wire_pin"))
                {
                    desc += "- OneWire IO pin on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                       
                    }
                }
                else if (Regex.IsMatch(key, "backlit_io_pin"))
                {
                    desc += "- Backlit IO pin on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.LED);
                    }
                }
                else if (key == "max_V")
                {
                    desc += "- Battery Max Voltage: " + value + Environment.NewLine;
                    bHasBattery = true;
                }
                else if (key == "min_V")
                {
                    desc += "- Battery Min Voltage: " + value + Environment.NewLine;
                    bHasBattery = true;
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
                else if (Regex.IsMatch(key, "^rl_on\\d+_pin$"))
                {
                    // match rl_on1_pin
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Bridge Relay On (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Rel);
                        tg.setPinChannel(value, number);
                    }
                }
                else if (Regex.IsMatch(key, "^rl_off\\d+_pin$"))
                {
                    // match rl_off1_pin
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Bridge Relay Off (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Rel_n);
                        tg.setPinChannel(value, number);
                    }
                }
                else if (key == "bt_pin" || key == "bt")
                {
                    int number = 0;
                    desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Btn);
                        tg.setPinChannel(value, number);
                    }
                }
                // parse k3pin_pin
                else if (Regex.IsMatch(key, "^k\\d+pin_pin$"))
                {
                    int number = int.Parse(Regex.Match(key, "\\d+").Value);
                    desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Btn);
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
                else if (key == "gate_sensor_pin_pin")
                {
                    desc += "- Door/Gate sensor on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.dInput);
                    }
                }
                else if (key == "basic_pin_pin")
                {
                    // This will read 1 if there was a movement, at least on the sensor I have
                    // some devices have netled1_pin, some have netled_pin
                    desc += "- PIR sensor on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.dInput);
                    }
                }
                else if (key == "netled_pin" || key == "wfst")
                {
                    // some devices have netled1_pin, some have netled_pin
                    desc += "- WiFi LED on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.WifiLED_n);
                    }
                }
                else if(key == "ele_pin" || key == "epin")
                {
                    desc += "- BL0937 ELE (CF) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BL0937CF);
                    }
                }
                else if (key == "vi_pin" || key == "ivpin")
                {
                    desc += "- BL0937 VI (CF1) on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.BL0937CF1);
                    }
                }
                else if (key == "sel_pin_pin" || key == "ivcpin")
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
                else if (key == "micpin")
                {
                    desc += "- Microphone (TODO) on P" + value + Environment.NewLine;
                }
                else if (key == "ctrl_pin")
                {
                    desc += "- Control Pin (TODO) on P" + value + Environment.NewLine;
                }
                else if (key == "buzzer_io")
                {
                    desc += "- Buzzer Pin (TODO) on P" + value + Environment.NewLine;
                }
                else if (key == "buzzer_pwm")
                {
                    desc += "- Buzzer Frequency (TODO) is " + value + "Hz"+ Environment.NewLine;
                }
                else if (key == "mic")
                {
                    desc += "- Microphone (TODO) is on P" + value + "" + Environment.NewLine;
                }
                else if (key == "irpin" || key == "infrr")
                {
                    desc += "- IR Receiver is on P" + value + "" + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.IRRecv);
                    }
                }
                else if (key == "infre")
                {
                    desc += "- IR Transmitter is on P" + value + "" + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.IRSend);
                    }
                }
                else if (key == "reset_pin")
                {
                    desc += "- Button is on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.Btn);
                    }
                }
                else if (key == "wfst_pin")
                {
                    desc += "- WiFi LED on P" + value + Environment.NewLine;
                    if (tg != null)
                    {
                        tg.setPinRole(value, PinRole.WifiLED);
                    }
                }
                else if (key == "pwmhz")
                {
                    desc += "- PWM Frequency " + value + "" + Environment.NewLine;
                }
                else if (key == "pirsense_pin")
                {
                    desc += "- PIR Sensitivity " + value + "" + Environment.NewLine;
                }
                else if (key == "pirlduty")
                {
                    desc += "- PIR Low Duty " + value + "" + Environment.NewLine;
                }
                else if (key == "pirfreq")
                {
                    desc += "- PIR Frequency " + value + "" + Environment.NewLine;
                }
                else if (key == "pirmduty")
                {
                    desc += "- PIR High Duty " + value + "" + Environment.NewLine;
                }
                else if (key == "pirin_pin")
                {
                    desc += "- PIR Input " + value + "" + Environment.NewLine;
                }
                else if (key == "mosi")
                {
                    desc += "- SPI MOSI " + value + "" + Environment.NewLine;
                }
                else if (key == "miso")
                {
                    desc += "- SPI MISO " + value + "" + Environment.NewLine;
                }
                else if (key == "total_bt_pin")
                {
                    desc += "- Pair/Toggle All Button on P" + value + Environment.NewLine;
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
                string currents = string.Empty;
                // use current (color/cw) setting
                if (ehccur.Length>0 || wampere.Length > 0 || iicccur.Length > 0)
                {
                    ledType = "SM2135";
                    currents =  $"- RGB current is {(ehccur.Length > 0 ? ehccur : iicccur.Length > 0 ? iicccur : campere.Length > 0 ? campere : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(ehwcur.Length > 0 ? ehwcur : iicwcur.Length > 0 ? iicwcur : wampere.Length > 0 ? wampere : "Unknown")} mA{Environment.NewLine}";
                }
                else if (dccur.Length > 0)
                {
                    ledType = "BP5758D_";
                    currents =  $"- RGB current is {(drgbcur.Length > 0 ? drgbcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Warm white current is {(dwcur.Length > 0 ? dwcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Cold white current is {(dccur.Length > 0 ? dccur : "Unknown")} mA{Environment.NewLine}";
                }
                else if (cjwcur.Length > 0)
                {
                    ledType = "BP1658CJ_";
                    currents =  $"- RGB current is {(cjccur.Length > 0 ? cjccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(cjwcur.Length > 0 ? cjwcur : "Unknown")} mA{Environment.NewLine}";
                }
                else if (_2235ccur.Length > 0)
                {
                    ledType = "SM2235";
                    currents =  $"- RGB current is {(_2235ccur.Length > 0 ? _2235ccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(_2235wcur.Length > 0 ? _2235wcur : "Unknown")} mA{Environment.NewLine}";
                }
                else if (_2335ccur.Length > 0)
                {
                    // fallback
                    ledType = "SM2235";
                }
                else if (kp58wcur.Length > 0)
                {
                    ledType = "KP18058";
                }
                else
                {

                }
                string dat_name = ledType + "DAT";
                string clk_name = ledType + "CLK";
                desc += "- " + dat_name + " on P" + iicsda + Environment.NewLine;
                desc += "- " + clk_name + " on P" + iicscl + Environment.NewLine;
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
            if(kp == null)
            {
                kp = this.findKeyContaining("module");
            }
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
            desc += Environment.NewLine;
            void printposdevice(string device)
            {
                desc += $"And the Tuya section starts at {magicPosition} (0x{magicPosition:X}), which is a default {device} offset." + Environment.NewLine;
            }
            switch(magicPosition)
            {
                case USUAL_BK7231_MAGIC_POSITION:
                    desc += $"And the Tuya section starts, as usual, at {magicPosition}" + Environment.NewLine;
                    break;
                case USUAL_BK_NEW_XR806_MAGIC_POSITION:
                    printposdevice("T1/XR806 and some T34");
                    break;
                case USUAL_RTLC_MAGIC_POSITION:
                    printposdevice("RTL8720C");
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
                case USUAL_ECR6600_MAGIC_POSITION:
                    printposdevice("ECR6600");
                    break;
                case USUAL_W800_MAGIC_POSITION:
                    printposdevice("W800");
                    break;
                default:
                    desc += "And the Tuya section starts at an UNCOMMON POSITION " + magicPosition + Environment.NewLine;
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
                            // extract at least something
                            keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("gw_bi"));
                            if(keys_at == -1)
                            {
                                FormMain.Singleton.addLog("Failed to extract Tuya keys - no json start found" + Environment.NewLine, System.Drawing.Color.Orange);
                                return true;
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
                string svalue = kp[kp.Length - 1];
                skey = skey.Trim(new char[] { '"' }).Replace("\"", "");
                svalue = svalue.Trim(new char[] { '"' }).Replace("\"", "").Replace("}", "");
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
