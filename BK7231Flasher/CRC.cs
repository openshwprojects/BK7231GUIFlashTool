using System;
using System.Collections.Generic;

namespace BK7231Flasher
{
    public class CRC
    {
        public static uint[] crc32_table;
        private static List<ushort> crc_ccitt_table = new List<ushort>();

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
        public static byte Tiny_CRC8_unsigned(byte[] data, int start, int length)
        {
            byte crc = 0x00;
            byte extract;
            byte sum;
            int i;
            byte tempI;

            unchecked
            {
                for(i = 0; i < length; i++)
                {
                    extract = (byte)data[start + i];
                    for(tempI = 8; tempI != 0; tempI--)
                    {
                        sum = (byte)((crc ^ extract) & 0x01);
                        crc >>= 1;
                        if(sum != 0)
                            crc ^= (byte)0x8C;
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

        public static ushort crc_ccitt(byte[] input, int start, int length, ushort startingValue = 0)
        {
            try
            {
                if(crc_ccitt_table.Count == 0)
                {
                    InitCrcCcitt();
                }
                ushort crcValue = startingValue;
                for(int i = start; i < length; i++)
                {
                    byte tmp = (byte)((crcValue >> 8) ^ input[i]);
                    crcValue = (ushort)((crcValue << 8) ^ crc_ccitt_table[tmp]);
                }

                return crcValue;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }

        private static void InitCrcCcitt()
        {
            for(int i = 0; i < 256; i++)
            {
                ushort crc = 0;
                ushort c = (ushort)(i << 8);

                for(int j = 0; j < 8; j++)
                {
                    if(((crc ^ c) & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc <<= 1;
                    }

                    c <<= 1;
                }

                crc_ccitt_table.Add(crc);
            }
        }
    }
}
