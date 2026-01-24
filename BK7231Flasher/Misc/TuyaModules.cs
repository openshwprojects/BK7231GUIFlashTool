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
                    if(s[2] != 'R')
                        return nameof(BKType.BK7231T);
                    else if(s[2] == 'R')
                        return nameof(BKType.RTL87X0C);
                    else
                        break;

                case 'C':
                    if(s[1] == 'B')
                        return nameof(BKType.BK7231N);
                    else if(s[1] == 'R')
                        return nameof(BKType.RTL87X0C);
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
                        default: break;
                    }
                    break;
                case 'X': return "XR809";
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
            "rtl8720cf_ameba"     => nameof(BKType.RTL87X0C),
            "rtl8720dn"           => nameof(BKType.RTL8720D),
            "T1"                  => nameof(BKType.BK7238),
            "TR6260_1M"           => nameof(BKType.TR6260),
            "rtl8711am_zb_gw_ame" => "RTL8711AM",
            "bk7231"              => "BK7231Q",
            "RTL8710BN_2M"        => nameof(BKType.RTL8710B),

            // unconfirmed, from tuya names
            "T3"                  => nameof(BKType.BK7236),
            "T5"                  => nameof(BKType.BK7258),
            "t2"                  => nameof(BKType.BK7231N),
            "bk7231t"             => nameof(BKType.BK7231T),
            "bk7231nl"            => nameof(BKType.BK7231N),
            "bk7238"              => nameof(BKType.BK7238),
            "bk7258"              => nameof(BKType.BK7258),
            "bl602"               => nameof(BKType.BL602),
            "esp32"               => "ESP32",
            "rtl8711am"           => "RTL8711AM",
            "rtl8710bn"           => nameof(BKType.RTL8710B),
            "rtl8711daf"          => nameof(BKType.RTL8721DA),
            "rtl8720cm_ameba"     => nameof(BKType.RTL87X0C),
            "rtl8721cs"           => nameof(BKType.RTL8720D),
            "test_rtl8720dn"      => nameof(BKType.RTL8720D),
            "w800"                => nameof(BKType.W800),
            "w803"                => nameof(BKType.W800),
            "xr806"               => "XR806",
            "xr809"               => "XR809",

            _                     => nameof(BKType.Invalid),
        };
    }
}
