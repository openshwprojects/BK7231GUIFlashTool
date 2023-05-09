using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace BK7231Flasher
{
    public interface ILogListener
    {
        void addLog(string s, Color c);
        void setProgress(int cur, int max);
        void setState(string s, Color col);
        void onReadResultQIOSaved(byte[] dat, string fullPath);
        OBKConfig getConfigToWrite();
        OBKConfig getConfig();
    }
}
