using System;
using System.Drawing;
using System.Windows.Forms;

namespace BK7231Flasher
{
    class RichTextUtil
    {
        public static void AppendText(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;

            box.SelectionStart = box.TextLength;
            if (text.Contains(Environment.NewLine))
            {
                box.ScrollToCaret();
            }
        }
    }
}
