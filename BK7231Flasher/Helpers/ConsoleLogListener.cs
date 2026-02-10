using System;
using System.Drawing;

namespace BK7231Flasher
{
    /// <summary>
    /// A console-based ILogListener for command-line operation (no GUI).
    /// </summary>
    public class ConsoleLogListener : ILogListener
    {
        public void addLog(string s, Color c)
        {
            if (c == Color.Red)
                Console.Error.Write(s);
            else
                Console.Write(s);
        }

        public void setProgress(int cur, int max)
        {
            if (max > 0)
            {
                int pct = (int)((long)cur * 100 / max);
                Console.Write($"\r[{pct}%] {cur}/{max}    ");
            }
        }

        public void setState(string s, Color col)
        {
            Console.WriteLine($"[STATE] {s}");
        }

        public void onReadResultQIOSaved(byte[] dat, string lastEncryptionKey, string fullPath)
        {
            Console.WriteLine($"Read result saved to: {fullPath}");
            if (!string.IsNullOrEmpty(lastEncryptionKey))
            {
                Console.WriteLine($"Encryption key: {lastEncryptionKey}");
            }
        }

        public OBKConfig getConfigToWrite()
        {
            return null;
        }

        public OBKConfig getConfig()
        {
            return null;
        }
    }
}
