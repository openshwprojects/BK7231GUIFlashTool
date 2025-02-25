using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    class OBKFlashLayout
    {
        public static int getConfigLocation(BKType type)
        {
            if (type == BKType.BK7231T || type == BKType.BK7238)
            {
                return 0x1e1000;
            }
            if (type == BKType.BK7231N || type == BKType.BK7231M)
            {
                return 0x1d1000;
            }
            return 0;
        }

        internal static BKType detectChipTypeForCFG(byte[] dat)
        {
            try
            {
                int ofs_BK7231T = getConfigLocation(BKType.BK7231T);
                int ofs_BK7231N = getConfigLocation(BKType.BK7231N);
                if (OBKConfig.isValid(dat, ofs_BK7231N))
                {
                    return BKType.BK7231N;
                }
                if (OBKConfig.isValid(dat, ofs_BK7231T))
                {
                    return BKType.BK7231T;
                }
            }
            catch(Exception ex)
            {
            }
            return BKType.Detect;
        }
    }
}
