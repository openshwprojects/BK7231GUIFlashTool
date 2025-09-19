using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            if (s[0] == 'W' && s[2] != 'R')
                return BKType.BK7231T;
            else if(s[2] == 'R') return BKType.RTL8720C;
            if(s[0] == 'C')
                return BKType.BK7231N;
            if (s[0] == 'T')
            {
                switch(s[1])
                {
                    case '1': return BKType.BK7238;
                    case '2': return BKType.BK7231N;
                    case '3': return BKType.BK7236;
                    case '4': return BKType.BK7252N;
                    case '5': return BKType.BK7258;
                }
            }
            return BKType.Invalid;
        }
    }
}
