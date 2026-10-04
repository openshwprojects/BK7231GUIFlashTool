namespace BK7231Flasher
{
    class TuyaModules
    {
        //public string[][] modules = new
        //{
        //    new string[] { "WB3S", "BK7231T" },
        //    new string[] { "WB2S", "BK7231T" },
        //};
        public static string getTypeForModuleName(string s)
        {
            if (s.Length == 0)
                return nameof(BKType.Invalid);
            s = s.ToUpper();
            switch(s[0])
            {
                case 'W':
                    if(s[1] == 'X')
                        return nameof(BKType.XR806);
                    if(s[1] == 'R')
                        if(s.Length > 3 && s[2] == '1' && s[3] == '1')
                            return nameof(BKType.RTL8721DA);
                        else
                            return nameof(BKType.RTL8710B);
                    else if(s[1] == 'L')
                        return nameof(BKType.LN882H);
                    else if(s[2] != 'R' && s[2] != 'D')
                        return nameof(BKType.BK7231T);
                    else if(s[2] == 'R')
                        if(s.Length > 4 && (s[4] == 'D' || s[4] == 'S' || s[4] == 'N' || s[4] == 'T' || s[3] == 'G'))
                            return nameof(BKType.RTL8720D);
                        else
                            return nameof(BKType.RTL87X0C);
                    else if(s[2] == 'D')
                        return nameof(BKType.RDA5981);
                    else
                        break;

                case 'C':
                    if(s[1] == 'B')
                        return nameof(BKType.BK7231N);
                    else if(s[1] == 'R')
                        return nameof(BKType.RTL87X0C);
                    else if(s == "CHIP-Z2")
                        return "TLSR825x";
                    else
                        break;

                case 'T':
                    if(s == "T34")
                        return nameof(BKType.BK7231N);
                    switch(s[1])
                    {
                        case '1': return nameof(BKType.BK7238);
                        case '2': return nameof(BKType.BK7231N);
                        case '3': return nameof(BKType.BK7236);
                        case '4': return nameof(BKType.BK7252N);
                        case '5': return nameof(BKType.BK7258);
                        case '6': return "BK7236N";
                        case '7': return nameof(BKType.BK7239N);
                        case '9': return "BK7239";
                        default: break;
                    }
                    break;
                case 'X': return nameof(BKType.XR809);
                case 'Z':
                    if(s[1] == 'T')
                        return "TLSR825x";
                    else if(s[1] == 'S')
                        return "EFR32MG2x";
                    else
                        break;
                case 'A':
                    return nameof(BKType.ECR6600);
                default: break;
            }
            return nameof(BKType.Invalid);
        }
        public static string getTypeForPlatformName(string s) => s switch
        {
            "eswin_ecr6600"       => nameof(BKType.ECR6600),
            "LN882X_2M"           => nameof(BKType.LN8825),
            "ln882h"              => nameof(BKType.LN882H),
            "bk7231n"             => nameof(BKType.BK7231N),
            "BK7231NL"            => nameof(BKType.BK7231N),
            "bk7231t"             => nameof(BKType.BK7231T),
            "BK7231S_2M"          => nameof(BKType.BK7231T),
            "rtl8720cf_ameba"     => nameof(BKType.RTL87X0C),
            "rtl8720dn"           => nameof(BKType.RTL8720D),
            "T1"                  => nameof(BKType.BK7238),
            "t2"                  => nameof(BKType.BK7231N),
            "TR6260_1M"           => nameof(BKType.TR6260),
            "rtl8711am_zb_gw_ame" => "RTL8711AM",
            "bk7231"              => "BK7231Q",
            "RTL8710BN_2M"        => nameof(BKType.RTL8710B),
            //"8710_2M"             => nameof(BKType.RTL8710B), // can be RTL8710B, BK7231Q or BK7231T

            // unconfirmed, from tuya names
            "T3"                  => nameof(BKType.BK7236),
            "T5"                  => nameof(BKType.BK7258),
            "T6"                  => "BK7236N",
            "T7"                  => nameof(BKType.BK7239N),
            "T9"                  => "BK7239",
            "bk7231nl"            => nameof(BKType.BK7231N),
            "bk7238"              => nameof(BKType.BK7238),
            "bk7258"              => nameof(BKType.BK7258),
            "bl602"               => nameof(BKType.BL602),
            "bl616"               => nameof(BKType.BL616),
            "bl618"               => nameof(BKType.BL616),
            "esp32"               => nameof(BKType.ESP32),
            "esp32s2"             => nameof(BKType.ESP32S2),
            "esp32-s2"            => nameof(BKType.ESP32S2),
            "esp32c2"             => nameof(BKType.ESP32C2),
            "esp32-c2"            => nameof(BKType.ESP32C2),
            "esp32c3"             => nameof(BKType.ESP32C3),
            "esp32-c3"            => nameof(BKType.ESP32C3),
            "esp32c5"             => nameof(BKType.ESP32C5),
            "esp32-c5"            => nameof(BKType.ESP32C5),
            "esp32c6"             => nameof(BKType.ESP32C6),
            "esp32-c6"            => nameof(BKType.ESP32C6),
            "esp32c61"            => nameof(BKType.ESP32C61),
            "esp32-c61"           => nameof(BKType.ESP32C61),
            "esp32s3"             => nameof(BKType.ESP32S3),
            "esp32-s3"            => nameof(BKType.ESP32S3),
            "esp8266"             => nameof(BKType.ESP8266),
            "esp8285"             => nameof(BKType.ESP8266),
            "gd32vw553"           => nameof(BKType.GD32VW553),
            "rtl8711am"           => "RTL8711AM",
            "rtl8711am_ameba"     => "RTL8711AM",
            "rtl8710bn"           => nameof(BKType.RTL8710B),
            "rtl8711daf"          => nameof(BKType.RTL8721DA),
            "rtl8720cm_ameba"     => nameof(BKType.RTL87X0C),
            "rtl8721cs"           => nameof(BKType.RTL8720D),
            "test_rtl8720dn"      => nameof(BKType.RTL8720D),
            "w800"                => nameof(BKType.W800),
            "w803"                => nameof(BKType.W800),
            "xr806"               => nameof(BKType.XR806),
            "xr809"               => nameof(BKType.XR809),
            "xr872"               => nameof(BKType.XR872),
            "sv32wb0x"            => "SV32WB0X",

            _                     => nameof(BKType.Invalid),
        };
    }
}
