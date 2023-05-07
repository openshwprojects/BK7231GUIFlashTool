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
            if(type == BKType.BK7231T)
            {
                return 0x1e1000;
            }
            if (type == BKType.BK7231N)
            {
                return 0x1d1000;
            }
            return 0;
        }
    }
}
