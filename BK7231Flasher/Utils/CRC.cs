using System;
using System.Collections.Concurrent;
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
    }

    public enum CRC16Type
    {
        CCITT,
        CCITT_FALSE,
        CCITT_TRUE,
        CMS,
        XMODEM
    }

    public static class CRC16
    {
        private class CRCParams
        {
            public ushort Poly;
            public ushort Init;
            public bool Ref;
            public ushort Out;
            public ushort[] Table;
        }

        private static readonly ConcurrentDictionary<CRC16Type, CRCParams> Configs = new ConcurrentDictionary<CRC16Type, CRCParams>();

        static CRC16()
        {
            Add(CRC16Type.CCITT, 0x1021, 0x0000, true, 0x0000);
            Add(CRC16Type.CCITT_FALSE, 0x1021, 0xFFFF, false, 0x0000);
            Add(CRC16Type.CCITT_TRUE, 0x1021, 0x0000, true, 0x0000);
            Add(CRC16Type.CMS, 0x8005, 0xFFFF, false, 0x0000);
            Add(CRC16Type.XMODEM, 0x1021, 0x0000, false, 0x0000);
        }

        private static void Add(CRC16Type type, ushort poly, ushort init, bool reflect, ushort xorOut)
        {
            var cfg = new CRCParams
            {
                Poly = reflect ? Reverse16(poly) : poly,
                Init = reflect ? Reverse16(init) : init,
                Ref = reflect,
                Out = xorOut,
                Table = BuildTable(reflect ? Reverse16(poly) : poly, reflect)
            };
            Configs[type] = cfg;
        }

        private static ushort Reverse16(ushort num)
        {
            ushort result = 0;
            for(int i = 0; i < 16; i++)
                result |= (ushort)(((num >> i) & 1) << (15 - i));
            return result;
        }

        private static ushort[] BuildTable(ushort poly, bool reflect)
        {
            var table = new ushort[256];
            if(reflect)
            {
                for(int b = 0; b < 256; b++)
                {
                    ushort crc = (ushort)b;
                    for(int i = 0; i < 8; i++)
                    {
                        if((crc & 0x0001) != 0)
                            crc = (ushort)((crc >> 1) ^ poly);
                        else
                            crc >>= 1;
                    }
                    table[b] = crc;
                }
            }
            else
            {
                for(int b = 0; b < 256; b++)
                {
                    ushort crc = (ushort)(b << 8);
                    for(int i = 0; i < 8; i++)
                    {
                        if((crc & 0x8000) != 0)
                            crc = (ushort)((crc << 1) ^ poly);
                        else
                            crc <<= 1;
                    }
                    table[b] = (ushort)(crc & 0xFFFF);
                }
            }
            return table;
        }

        public static ushort Compute(CRC16Type type, byte[] data)
            => Compute(type, data, 0, data.Length);

        public static ushort Compute(CRC16Type type, byte[] data, int start = 0, int? length = null)
        {
            if(!Configs.TryGetValue(type, out var cfg))
                throw new ArgumentNullException(nameof(type));

            if(data == null)
                throw new ArgumentNullException(nameof(data));

            ushort crc = cfg.Init;

            return cfg.Ref
                ? CalcRef(cfg, data, start, length ?? data.Length, crc)
                : CalcStd(cfg, data, start, length ?? data.Length, crc);
        }

        private static ushort CalcStd(CRCParams cfg, byte[] data, int start, int length, ushort crc)
        {
            for(int i = start; i < start + length; i++)
            {
                byte idx = (byte)(data[i] ^ (crc >> 8));
                crc = (ushort)(cfg.Table[idx] ^ ((crc << 8) & 0xFFFF));
            }
            return (ushort)(crc ^ cfg.Out);
        }

        private static ushort CalcRef(CRCParams cfg, byte[] data, int start, int length, ushort crc)
        {
            for(int i = start; i < start + length; i++)
            {
                byte idx = (byte)(data[i] ^ (crc & 0xFF));
                crc = (ushort)(cfg.Table[idx] ^ (crc >> 8));
            }
            return (ushort)(crc ^ cfg.Out);
        }
    }

}
