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
            if (s == "T34")
                return BKType.BK7231N;
            if (s[0] == 'W')
                return BKType.BK7231T;
            if (s[0] == 'C')
                return BKType.BK7231N;
            return BKType.Invalid;
        }
    }
}
