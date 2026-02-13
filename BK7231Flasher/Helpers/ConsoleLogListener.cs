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

        private System.Diagnostics.Stopwatch progressStopwatch;
        private int lastProgressMax = -1;

        public void setProgress(int cur, int max)
        {
            if (max > 0)
            {
                // Reset stopwatch when a new operation starts
                if (max != lastProgressMax || cur == 0)
                {
                    progressStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    lastProgressMax = max;
                }

                int pct = (int)((long)cur * 100 / max);
                string speedStr = "";
                if (progressStopwatch != null && progressStopwatch.ElapsedMilliseconds > 100 && cur > 0)
                {
                    double seconds = progressStopwatch.ElapsedMilliseconds / 1000.0;
                    double bytesPerSec = cur / seconds;
                    if (bytesPerSec >= 1024 * 1024)
                        speedStr = $" {bytesPerSec / 1024 / 1024:F1} MB/s";
                    else if (bytesPerSec >= 1024)
                        speedStr = $" {bytesPerSec / 1024:F1} KB/s";
                    else
                        speedStr = $" {bytesPerSec:F0} B/s";
                }
                Console.Write($"\r[{pct}%] {cur}/{max}{speedStr}    ");
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
