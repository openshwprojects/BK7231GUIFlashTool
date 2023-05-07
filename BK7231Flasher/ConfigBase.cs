using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    class ConfigBase
    {
        protected byte[] raw = new byte[3584];

        protected void writeByte(int ofs, byte b)
        {
            raw[ofs] = b;
        }
        protected byte readByte(int ofs)
        {
            return raw[ofs];
        }
        protected void writeStr(int ofs, string value, int maxLen)
        {
            byte[] strBytes = Encoding.ASCII.GetBytes(value);
            int len = strBytes.Length;
            if (len > maxLen)
                len = maxLen;
            for(int i = 0; i < len; i++)
            {
                writeByte(ofs + i, strBytes[i]);
            }
        }
        protected string readStr(int ofs, int maxLen)
        {
            string r = "";
            r = Encoding.ASCII.GetString(raw, ofs, maxLen);
            return r;
        }
        protected void writeInt(int ofs, int value)
        {
            raw[ofs + 3] = (byte)(value >> 24);
            raw[ofs + 2] = (byte)(value >> 16);
            raw[ofs + 1] = (byte)(value >> 8);
            raw[ofs] = (byte)value;
        }
        protected int readInt(int ofs)
        {
            int value = 0;
            value |= raw[ofs + 3] << 24;
            value |= raw[ofs + 2] << 16;
            value |= raw[ofs + 1] << 8;
            value |= raw[ofs];
            return value;
        }
    }
}
