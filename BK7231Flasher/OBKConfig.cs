using System;
using System.IO;
using System.Text;

namespace BK7231Flasher
{
    public class OBKConfig : ConfigBase
    {
        public byte[] efdata = null;
        static byte DEFAULT_BOOT_SUCCESS_TIME = 5;
        static int OBK_CONFIG_VERSION = 4;
        static int MAX_GPIO = 32;
        static int MAX_CHANNELS = 64;

        // unit is 0.1s
        static byte CFG_DEFAULT_BTN_SHORT = 3;
        static byte CFG_DEFAULT_BTN_LONG = 10;
        static byte CFG_DEFAULT_BTN_REPEAT = 5;


        public void zeroMemory()
        {
            for(int i = 0; i < raw.Length; i++)
            {
                raw[i] = 0;
            }
        }
        public int version
        {
            get
            {
                return readInt(0x04);
            }
            set
            {
                writeInt(0x04,value);
            }
        }
        public int genericFlags
        {
            get
            {
                return readInt(0x08);
            }
            set
            {
                writeInt(0x08, value);
            }
        }
        public byte timeRequiredToMarkBootSuccessfull
        {
            get
            {
                return readByte(0x00000597);
            }
            set
            {
                writeByte(0x00000597, value);
            }
        }
        public byte buttonShortPress
        {
            get
            {
                return readByte(0x000004B8);
            }
            set
            {
                writeByte(0x000004B8, value);
            }
        }
        public byte buttonLongPress
        {
            get
            {
                return readByte(0x000004B9);
            }
            set
            {
                writeByte(0x000004B9, value);
            }
        }
        internal bool loadFrom(byte [] dat, BKType type, bool bApplyOffset)
        {
            byte[] subArray = null;
            if(type == BKType.Detect)
            {
                if(dat.Length < 8192)
                {
                    bApplyOffset = false;
                }
                else
                {
                    type = OBKFlashLayout.detectChipTypeForCFG(dat);
                }
            }
            if(bApplyOffset)
            {
                int offset = OBKFlashLayout.getConfigLocation(type, out var sectors);
                subArray = MiscUtils.subArray(dat, offset, sectors * BK7231Flasher.SECTOR_SIZE);
            }
            else
            {
                subArray = dat;
            }
            switch(type)
            {
                case BKType.RTL8710B:
                case BKType.RTL87X0C:
                case BKType.RTL8720D:
                case BKType.BL602:
                case BKType.ECR6600:
                    _ = OBKFlashLayout.getConfigLocation(type, out var sectors);
                    var sname = type == BKType.BL602 ? "mY0bcFg" : "ObkCfg";
                    dat = EasyFlash.LoadValueFromData(subArray, sname, sectors * BK7231Flasher.SECTOR_SIZE, type, out efdata);
                    subArray = dat;
                    break;
                default: break;
            }
            if (isValid(subArray, type: type))
            {
                this.raw = subArray;
                return false;
            }
            return true;
        }
        internal bool loadFrom(string fname, BKType type)
        {
            byte[] fileBytes = File.ReadAllBytes(fname);
            return loadFrom(fileBytes, type, true);
        }
        public byte buttonHoldRepeat
        {
            get
            {
                return readByte(0x000004BA);
            }
            set
            {
                writeByte(0x000004BA, value);
            }
        }

        public bool hasFlag(int flag)
        {
            if (flag >= 32)
            {
                flag -= 32;
                return BIT.CHECK(this.genericFlags2, flag);
            }
            return BIT.CHECK(this.genericFlags, flag);
        }
        public void setFlag(int flag, bool b)
        {
            if (flag >= 32)
            {
                flag -= 32;
                this.genericFlags2 = BIT.SET_TO2(this.genericFlags2, flag, b);
                return;
            }
            this.genericFlags = BIT.SET_TO2(this.genericFlags, flag, b);
        }
        public int genericFlags2
        {
            get
            {
                return readInt(0x0C);
            }
            set
            {
                writeInt(0x0C, value);
            }
        }
        public string wifi_ssid
        {
            get
            {
                return readStr(0x14, 64);
            }
            set
            {
                writeStr(0x14, value, 64);
            }
        }
        public string wifi_pass
        {
            get
            {
                return readStr(0x54, 64);
            }
            set
            {
                writeStr(0x54, value, 64);
            }
        }
        public string mqtt_host
        {
            get
            {
                return readStr(0x94, 256);
            }
            set
            {
                writeStr(0x94, value, 256);
            }
        }
        public string mqtt_clientId
        {
            get
            {
                return readStr(0x194, 64);
            }
            set
            {
                writeStr(0x194, value, 64);
            }
        }
        public string mqtt_userName
        {
            get
            {
                return readStr(0x1D4, 64);
            }
            set
            {
                writeStr(0x1D4, value, 64);
            }
        }
        public string mqtt_pass
        {
            get
            {
                return readStr(0x214, 128);
            }
            set
            {
                writeStr(0x214, value, 128);
            }
        }
        public int mqtt_port
        {
            get
            {
                return readInt(0x294);
            }
            set
            {
                writeInt(0x294, value);
            }
        }
        public string webappRoot
        {
            get
            {
                return readStr(0x298, 64);
            }
            set
            {
                writeStr(0x298, value, 64);
            }
        }
        public string shortDeviceName
        {
            get
            {
                return readStr(0x2DE, 32);
            }
            set
            {
                writeStr(0x2DE, value, 32);
            }
        }
        public string longDeviceName
        {
            get
            {
                return readStr(0x2FE, 64);
            }
            set
            {
                writeStr(0x2FE, value, 64);
            }
        }
        public string mqtt_group
        {
            get
            {
                return readStr(0x00000554, 64);
            }
            set
            {
                writeStr(0x00000554, value, 64);
            }
        }
        public int dgr_sendFlags
        {
            get
            {
                return readInt(0x460);
            }
            set
            {
                writeInt(0x460, value);
            }
        }
        public int dgr_recvFlags
        {
            get
            {
                return readInt(0x464);
            }
            set
            {
                writeInt(0x464, value);
            }
        }
        public string dgr_name
        {
            get
            {
                return readStr(0x468, 16);
            }
            set
            {
                writeStr(0x468, value, 16);
            }
        }
        public byte getPinRole(int index)
        {
            if (index > MAX_GPIO)
                return 0;
            return readByte(0x033E + index);
        }
        public void setPinChannel(string index, int nc)
        {
            int idx;
            if (!int.TryParse(index, out idx))
            {
                return;
            }
            setPinChannel(idx, (byte)nc);
        }
        public void setPinRole(string index, PinRole nr)
        {
            int idx;
            if(!int.TryParse(index, out idx))
            {
                return;
            }
            setPinRole(idx, nr);
        }
        public void setPinRole(int index, PinRole nr)
        {
            setPinRole(index, (byte)nr);
        }
        public void setPinRole(int index, byte nr)
        {
            if (index > MAX_GPIO)
                return;
           writeByte(0x033E + index,nr);
        }
        public byte getPinChannel(int index)
        {
            if (index > MAX_GPIO)
                return 0;
            return readByte(0x35E + index);
        }
        public void setPinChannel(int index, byte nc)
        {
            if (index > MAX_GPIO)
                return;
            writeByte(0x35E + index, nc);
        }
        public byte getPinChannel2(int index)
        {
            if (index > MAX_GPIO)
                return 0;
            return readByte(0x37E + index);
        }
        public void setPinChannel2(int index, byte nc)
        {
            if (index > MAX_GPIO)
                return;
            writeByte(0x37E + index, nc);
        }
        public byte getChannelType(int index)
        {
            if (index > MAX_CHANNELS)
                return 0;
            return readByte(0x39E + index);
        }
        public void setChannelType(int index, byte nc)
        {
            if (index > MAX_CHANNELS)
                return;
            writeByte(0x39E + index, nc);
        }
        public string ntpServer
        {
            get
            {
                return readStr(0x478, 32);
            }
            set
            {
                writeStr(0x478, value, 32);
            }
        }
        public string ping_host
        {
            get
            {
                return readStr(0x000005A0, 64);
            }
            set
            {
                writeStr(0x000005A0, value, 64);
            }
        }
        public string initCommandLine
        {
            get
            {
                return readStr(0x000005E0, 1568);
            }
            set
            {
                writeStr(0x000005E0, value, 1568);
            }
        }
        public string wifi_ssid2
        {
            get
            {
                return readStr(0x00000C00, 64);
            }
            set
            {
                writeStr(0x00000C00, value, 64);
            }
        }
        public string wifi_pass2
        {
            get
            {
                return readStr(0x00000C84, 64);
            }
            set
            {
                writeStr(0x00000C84, value, 64);
            }
        }
        string readIP(int ofs)
        {
            byte a = getData()[ofs];
            byte b = getData()[ofs+1];
            byte c = getData()[ofs+2];
            byte d = getData()[ofs+3];
            // return string like 192.168.0.13
            string ipAddress = $"{a}.{b}.{c}.{d}";
            return ipAddress;
        }
        void writeIP(int ofs, string s)
        {  
            string[] octets = s.Split('.');

            if (octets.Length == 4)
            {
                if (byte.TryParse(octets[0], out byte a) &&
                    byte.TryParse(octets[1], out byte b) &&
                    byte.TryParse(octets[2], out byte c) &&
                    byte.TryParse(octets[3], out byte d))
                {
                    byte[] data = getData();
                    data[ofs] = a;
                    data[ofs + 1] = b;
                    data[ofs + 2] = c;
                    data[ofs + 3] = d;
                }
                else
                {
                }
            }
            else
            {
            }
        }
        public string localIPAddr
        {
            get
            {
                return readIP(0x00000527);
            }
            set
            {
                writeIP(0x00000527, value);
            }
        }
        public string netMask
        {
            get
            {
                return readIP(0x00000527+4);
            }
            set
            {
                writeIP(0x00000527+4, value);
            }
        }
        public string gatewayIPAddr
        {
            get
            {
                return readIP(0x00000527 + 8);
            }
            set
            {
                writeIP(0x00000527 + 8, value);
            }
        }
        public string dnsServerIpAddr
        {
            get
            {
                return readIP(0x00000527 + 12);
            }
            set
            {
                writeIP(0x00000527 + 12, value);
            }
        }

        public void setDefaults()
        {
            this.version = OBK_CONFIG_VERSION;
            this.mqtt_port = 1883;
            this.timeRequiredToMarkBootSuccessfull = DEFAULT_BOOT_SUCCESS_TIME;
            this.ping_host = "192.168.0.1";
            this.mqtt_userName = "homeassistant";
            this.webappRoot = "https://openbekeniot.github.io/webapp/";
            this.mqtt_group = "bekens";
            this.ntpServer = "217.147.223.78";
            // default value is 5, which means 500ms
            this.buttonHoldRepeat = CFG_DEFAULT_BTN_REPEAT;
            // default value is 3, which means 300ms
            this.buttonShortPress = CFG_DEFAULT_BTN_SHORT;
            // default value is 10, which means 1000ms
            this.buttonLongPress = CFG_DEFAULT_BTN_LONG;
            string randomSuffix = Rand.getRandomByte().ToString("X2") + Rand.getRandomByte().ToString("X2") + Rand.getRandomByte().ToString("X2");
            this.shortDeviceName = "obk" + randomSuffix;
            this.longDeviceName = "OpenBekenX_" + randomSuffix;
        }

        public static bool isValid(byte [] raw, int extraOfs = 0, BKType type = BKType.Invalid)
        {
            if (raw == null)
                return false;
            if(raw.Length < extraOfs + 5)
            {
                return false;
            }
            byte crc = 0;
            if (raw[extraOfs+0] != (byte)'C' || raw[extraOfs + 1] != (byte)'F' || raw[extraOfs + 2] != (byte)'G')
            {
                FormMain.Singleton.addLog("It's not an OBK config, header is bad"+Environment.NewLine, System.Drawing.Color.Purple);
                return false;
            }
            int version = BitConverter.ToInt32(raw, extraOfs + 4);
            if(version > 100)
            {
                FormMain.Singleton.addLog("OBK config has wrong version? Skipping" + Environment.NewLine, System.Drawing.Color.Purple);
                return false;
            }
            int useLen = getLenForVersion(version);
            switch(type)
            {
                case BKType.RTL8720D:
                case BKType.LN882H:
                case BKType.BL602:
                    crc = CRC.Tiny_CRC8_unsigned(raw, extraOfs + 4, useLen - 4);
                    break;
                case BKType.W800:
                    useLen = getLenForVersion(3);
                    crc = CRC.Tiny_CRC8_unsigned(raw, extraOfs + 4, useLen - 4);
                    break;
                default:
                    crc = CRC.Tiny_CRC8(raw, extraOfs + 4, useLen - 4);
                    break;
            }
            if(raw[extraOfs + 3] != crc)
            {
                FormMain.Singleton.addLog("OBK config has wrong checksum? Skipping" + Environment.NewLine, System.Drawing.Color.Purple);
                return false;
            }
            return true;
        }
        static int getLenForVersion(int v)
        {
            if(v <= 3)
            {
                return 2016;
            }
            return 3584;
        }
        public void saveConfig(BKType type = BKType.Invalid)
        {
            byte crc = 0;
            raw[0] = (byte)'C';
            raw[1] = (byte)'F';
            raw[2] = (byte)'G';
            version = 4;
            int realLen = getLenForVersion(version);
            switch(type)
            {
                case BKType.RTL8720D:
                case BKType.LN882H:
                case BKType.BL602:
                    crc = CRC.Tiny_CRC8_unsigned(raw, 4, realLen - 4);
                    break;
                case BKType.W800:
                    realLen = getLenForVersion(3);
                    crc = CRC.Tiny_CRC8_unsigned(raw, 4, realLen - 4);
                    break;
                default:
                    crc = CRC.Tiny_CRC8(raw, 4, realLen - 4);
                    break;
            }
            raw[3] = (byte)crc;
            if (isValid(raw, type: type) == false)
            {
                Console.WriteLine("Error in checksum recalculation");
            }
        }

    }
}
