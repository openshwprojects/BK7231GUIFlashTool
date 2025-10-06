namespace BK7231Flasher
{
    public class CRC
    {
        public static uint[] crc32_table;

        public static void initCRC()
        {
            crc32_table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0)
                    {
                        c = (0xEDB88320 ^ (c >> 1));
                    }
                    else
                    {
                        c = c >> 1;
                    }
                }
                crc32_table[i] = c;
            }
        }
        public static byte Tiny_CRC8(byte[] data, int start, int length)
        {
            sbyte crc = 0x00;
            sbyte extract;
            sbyte sum;
            int i;
            sbyte tempI;

            unchecked
            {
                for (i = 0; i < length; i++)
                {
                    extract = (sbyte)data[start + i];
                    for (tempI = 8; tempI != 0; tempI--)
                    {
                        sum = (sbyte)((crc ^ extract) & 0x01);
                        crc >>= 1;
                        if (sum != 0)
                            crc ^= (sbyte)0x8C;
                        extract >>= 1;
                    }
                }
            }
            return (byte)crc;
        }

        public static uint crc32_ver2(uint crc, byte[] buffer)
        {
            return crc32_ver2(crc,buffer,buffer.Length);
        }
        public static uint crc32_ver2(uint crc, byte[] buffer, int useLen)
        {
            if (crc32_table == null)
            {
                initCRC();
            }
            for (uint i = 0; i < useLen; i++)
            {
                uint c = buffer[i];
                crc = (crc >> 8) ^ crc32_table[(crc ^ c) & 0xff];
            }
            return crc;
        }
    }
}
