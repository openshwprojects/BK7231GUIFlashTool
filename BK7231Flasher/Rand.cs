using System;

namespace BK7231Flasher
{
    public class Rand
    {
        public static Random r = new Random(DateTime.Now.Millisecond);
        public static byte getRandomByte()
        {
            return (byte)r.Next(0, 255);
        }
    }
}
