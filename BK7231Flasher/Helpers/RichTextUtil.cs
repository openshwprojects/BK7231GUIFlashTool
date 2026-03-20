using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BK7231Flasher
{
    class RichTextUtil
    {
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        private const int WM_VSCROLL = 0x115;
        private const int SB_BOTTOM  = 7;

        public static void AppendText(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;

            SendMessage(box.Handle, WM_VSCROLL, SB_BOTTOM, 0);
        }
    }
}
