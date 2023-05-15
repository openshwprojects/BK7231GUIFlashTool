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
                        else
                        {
                            MessageBox.Show("Failed to extract keys");
                        }
                    }
                    else
                    {
                        if(tc.isLastBinaryOBKConfig())
                        {
                            MessageBox.Show("The file you've dragged looks like OBK config, not a Tuya one.");
                        }
                        else if (tc.isLastBinaryFullOf0xff())
                        {
                            MessageBox.Show("Failed, it seems that given binary is an erased flash sector, full of 0xFF");
                        }
                        else
                        {
                            MessageBox.Show("Failed, see log for more details");
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
