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
    }
}
