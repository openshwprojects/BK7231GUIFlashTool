namespace BK7231Flasher
{
    class TuyaModules
    {
        //public string[][] modules = new
        //{
        //    new string[] { "WB3S", "BK7231T" },
        //    new string[] { "WB2S", "BK7231T" },
        //};
        public static BKType getTypeForModuleName(string s)
        {
            if (s.Length == 0)
                return BKType.Invalid;
            s = s.ToUpper();
            switch(s[0])
            {
                case 'W':
                    if(s[2] != 'R')
                        return BKType.BK7231T;
                    else if(s[2] == 'R')
                        return BKType.RTL87X0C;
                    else
                        break;

                case 'C':
                    if(s[1] == 'B')
                        return BKType.BK7231N;
                    else if(s[1] == 'R')
                        return BKType.RTL87X0C;
                    else
                        break;

                case 'T':
                    if(s == "T34")
                        return BKType.BK7231N;
                    switch(s[1])
                    {
                        case '1': return BKType.BK7238;
                        case '2': return BKType.BK7231N;
                        case '3': return BKType.BK7236;
                        case '4': return BKType.BK7252N;
                        case '5': return BKType.BK7258;
                        default: break;
                    }
                    break;
                default: break;
            }
            return BKType.Invalid;
        }
        public static BKType getTypeForPlatformName(string s) => s switch
        {
            "eswin_ecr6600" => BKType.ECR6600,
            "LN882X_2M" => BKType.LN8825,
            "ln882h" => BKType.LN882H,
            "bk7231n" => BKType.BK7231N,
            "BK7231NL" => BKType.BK7231N,
            "rtl8720cf_ameba" => BKType.RTL87X0C,
            "rtl8720dn" => BKType.RTL8720D,
            "T1" => BKType.BK7238,
            "TR6260_1M" => BKType.TR6260,
            //"rtl8711am_zb_gw_ame" => BKType.RTL8711AM,

            // unconfirmed, from tuya names
            "T3" => BKType.BK7236,
            "T5" => BKType.BK7258,
            "t2" => BKType.BK7231N,
            "bk7231t" => BKType.BK7231T,
            "bk7231nl" => BKType.BK7231N,
            "bk7238" => BKType.BK7238,
            "bk7258" => BKType.BK7258,
            "bl602" => BKType.BL602,
            //"esp32" => BKType.ESP32,
            //"rtl8711am" => BKType.RTL8711AM,
            "rtl8710bn" => BKType.RTL8710B,
            "rtl8711daf" => BKType.RTL8721DA,
            "rtl8720cm_ameba" => BKType.RTL87X0C,
            "rtl8721cs" => BKType.RTL8720D,
            "test_rtl8720dn" => BKType.RTL8720D,
            "w800" => BKType.W800,
            "w803" => BKType.W800,
            //"xr806" => BKType.XR806,
            //"xr809" => BKType.XR809,
            _ => BKType.Invalid,
        };
    }
}
