using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    public class ConfigBase
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
        internal byte[] getData()
        {
            return raw;
        }
        protected void writeStr(int ofs, string value, int maxLen)
        {
            byte[] strBytes = Encoding.ASCII.GetBytes(value);
            int len = strBytes.Length;
            if (len > maxLen-1)
                len = maxLen-1;
            for(int i = 0; i < len; i++)
            {
                writeByte(ofs + i, strBytes[i]);
            }
            writeByte(ofs + len, 0);
        }
        protected string readStr(int ofs, int maxLen)
        {
            string r = "";
            int realLen;
            for(realLen = 0; realLen < maxLen; realLen++)
            {
                if (readByte(ofs + realLen) == 0)
                {
                    break;
                }
            }
            r = Encoding.ASCII.GetString(raw, ofs, realLen);
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
