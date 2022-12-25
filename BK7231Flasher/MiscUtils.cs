using System;
using System.Collections.Generic;
using System.Text;

namespace BK7231Flasher
{
    public class MiscUtils
    {
        public static string formatDateNowFileNameBase()
        {
            string r = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"); ;
            return r;
        }
        public static string formatDateNowFileName(string start, string ext)
        {
            string r = start + "_" + formatDateNowFileNameBase() + "." + ext;
            return r;
        }

        internal static byte[] padArray(byte[] data, int sector)
        {
            int rem = data.Length % sector;
            if (rem == 0)
                return data;
            int toAdd = sector - rem;
            byte[] ret = new byte[data.Length + toAdd];
            Array.Copy(data, 0, ret, 0, data.Length);
            for(int i = data.Length; i < ret.Length; i++)
            {
                ret[i] = 0xff;
            }
            return ret;
        }
    }
}
