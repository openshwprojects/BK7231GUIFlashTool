using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    class BIT
    {
        public static void SET(ref int PIN, int N)
        {
            PIN |= (1 << N);
        }

        public static void CLEAR(ref int PIN, int N)
        {
            PIN &= ~(1 << N);
        }

        public static void TGL(ref int PIN, int N)
        {
            PIN ^= (1 << N);
        }

        public static bool CHECK(int PIN, int N)
        {
            return ((PIN & (1 << N)) != 0);
        }

        public static void SET_TO(ref int PIN, int N, bool TG)
        {
            if (TG)
            {
                SET(ref PIN, N);
            }
            else
            {
                CLEAR(ref PIN, N);
            }
        }
        public static int SET_TO2(int PIN, int N, bool TG)
        {
            SET_TO(ref PIN, N, TG);
            return PIN;
        }
    }
}
