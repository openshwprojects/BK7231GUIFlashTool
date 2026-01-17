using System;

namespace BK7231Flasher
{
    public class MiscUtils
    {
        public static string formatDateNowFileNameBase()
        {
            string r = DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss"); ;
            return r;
        }
        public static string formatDateNowFileName(string start, string backupName, string ext)
        {
            string r = formatDateNowFileNameFor(start, backupName,  formatDateNowFileNameBase(), ext);
            return r;
        }
        public static string formatDateNowFileNameFor(string start, string backupName, string dateStr, string ext)
        {
            string r = start + "_";
            if (backupName.Length > 0)
            {
                r += backupName + "_";
            }
            r += dateStr + "." + ext;
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
        public static byte [] subArray(byte [] originalArray, int start, int length)
        {
            if(start + length > originalArray.Length)
            {
                return null;
            }
            byte[] subArray = new byte[length];
            Array.Copy(originalArray, start, subArray, 0, length);
            return subArray;
        }
        public static int findFirst(byte[] dat, byte needle, int start)
        {
            for (int i = start; i < dat.Length; i++)
            {
                if (dat[i] == needle)
                {
                    return i;
                }
            }
            return -1;
        }
        public static int findFirstRev(byte[] dat, byte needle, int start)
        {
            for (int i = start; i >= 0; i--)
            {
                if (dat[i] == needle)
                {
                    return i;
                }
            }
            return -1;
        }
        public static int findMatching(byte[] dat, byte needle, byte opener, int start)
        {
            int level = 1;
            for (int i = start; i < dat.Length; i++)
            {
                if(dat[i] == opener)
                {
                    level++;
                }
                if (dat[i] == needle)
                {
                    level--;
                    if(level == 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
        public static int indexOf(byte[] src, byte[] needle, int start = 0)
        {
            for (int i = start; i < src.Length - needle.Length; i++)
            {
                bool bOk = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (src[i + j] != needle[j])
                    {
                        bOk = false;
                        break;
                    }
                }
                if (bOk)
                    return i;
            }
            return -1;
        }

        internal static byte[] stripBinary(byte[] str)
        {
            throw new NotImplementedException();
        }

        internal static bool isFullOf(byte[] data, byte ch)
        {
            for(int i = 0; i < data.Length; i++)
            {
                if(data[i] != ch)
                {
                    return false;
                }
            }
            return true;
        }

        internal static uint ReadU32LE(byte[] b, int o = 0) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
