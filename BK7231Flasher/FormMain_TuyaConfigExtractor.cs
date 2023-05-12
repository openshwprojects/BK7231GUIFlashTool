using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        private void tabPage2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void tabPage2_DragDrop(object sender, DragEventArgs e)
        {
            textBoxTuyaCFGJSON.Text = "";
            textBoxTuyaCFGText.Text = "";
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                try
                {
                    // Do something with the dropped file(s)
                    TuyaConfig tc = new TuyaConfig();
                    if (tc.fromFile(file) == false)
                    {
                        if (tc.extractKeys() == false)
                        {
                            textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();
                            textBoxTuyaCFGText.Text = tc.getKeysHumanReadable();
                        }
                    }
                }
                catch (Exception ex)
                {
                    textBoxTuyaCFGText.Text = "Sorry, exception occured: " + ex.ToString();
                }
            }
        }

    }
}
