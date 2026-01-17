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
    }
}
