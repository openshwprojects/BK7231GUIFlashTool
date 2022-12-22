using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace BK7231Flasher
{
    public interface ILogListener
    {
        void addLog(string s, Color c);
    }
}
