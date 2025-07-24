﻿using Newtonsoft.Json.Linq;
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
                importTuyaConfig(file);
            }
        }

        private void buttonImportConfigFileDialog_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set the file dialog properties
            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string selectedFile = openFileDialog1.FileName;
                    importTuyaConfig(selectedFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
        }

        public void importTuyaConfig(string file)
        {
            try
            {
                // Do something with the dropped file(s)
                TuyaConfig tc = new TuyaConfig();
                if (tc.fromFile(file) == false)
                {
                    // Note: extractKeys() now always returns false (never fails), so this "else" should not trigger
                    if (tc.extractKeys() == false)
                    {
                        textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();
                        textBoxTuyaCFGText.Text = tc.getKeysHumanReadable();
                    }
                    else
                    {
                        // Kept for legacy: Should never hit this unless you re-add a failure state
                        MessageBox.Show("Failed to extract keys");
                    }
                }
                else
                {
                    if (tc.isLastBinaryOBKConfig())
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
