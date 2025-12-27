using System.Drawing;

namespace BK7231Flasher
{
    public interface ILogListener
    {
        void addLog(string s, Color c);
        void setProgress(int cur, int max);
        void setState(string s, Color col);
        void onReadResultQIOSaved(byte[] dat, string lastEncryptionKey, string fullPath);
        OBKConfig getConfigToWrite();
        OBKConfig getConfig();
    }
}
