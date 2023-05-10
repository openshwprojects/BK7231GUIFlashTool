using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    public class OBKConfig : ConfigBase
    {
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
            if (bApplyOffset)
            {
                int offset = OBKFlashLayout.getConfigLocation(type);
                subArray = MiscUtils.subArray(dat, offset, 4096);
            }
            else
            {
                subArray = dat;
            }
            if (isValid(subArray))
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

        public static bool isValid(byte [] raw, int extraOfs = 0)
        {
            byte crc = 0;
            if (raw[extraOfs+0] != (byte)'C' || raw[extraOfs + 1] != (byte)'F' || raw[extraOfs + 2] != (byte)'G')
            {
                FormMain.Singleton.addLog("It's not an OBK config, header is bad"+Environment.NewLine, System.Drawing.Color.Yellow);
                return false;
            }
            int version = BitConverter.ToInt32(raw, extraOfs + 4);
            if(version > 100)
            {
                FormMain.Singleton.addLog("OBK config has wrong version? Skipping" + Environment.NewLine, System.Drawing.Color.Yellow);
                return false;
            }
            int useLen = getLenForVersion(version);
            crc = CRC.Tiny_CRC8(raw, extraOfs + 4, useLen - 4);
            if(raw[extraOfs + 3] != crc)
            {
                FormMain.Singleton.addLog("OBK config has wrong checksum? Skipping" + Environment.NewLine, System.Drawing.Color.Yellow);
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
        public void saveConfig()
        {
            byte crc = 0;
            raw[0] = (byte)'C';
            raw[1] = (byte)'F';
            raw[2] = (byte)'G';
            version = 4;
            int realLen = getLenForVersion(version);
            crc = CRC.Tiny_CRC8(raw, 4, realLen - 4);
            raw[3] = (byte)crc;
            if (isValid(raw) == false)
            {
                Console.WriteLine("Error in checksum recalculation");
            }
        }

    }
}
