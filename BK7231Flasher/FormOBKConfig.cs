using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormOBKConfig : Form
    {
        FormMain main;
        OBKConfig cfg;

        public FormOBKConfig(FormMain mf)
        {
            this.main = mf;
            InitializeComponent();
        }
        public void refreshBinding()
        {
            for(int i = 0; i < this.Controls.Count; i++)
            {
                var c = this.Controls[i];
                if(c is TextBox)
                {
                    TextBox tb = c as TextBox;
                    for(int j = 0; j < tb.DataBindings.Count; j++)
                    {
                        var b = tb.DataBindings[j];
                        b.ReadValue();
                    }
                }
            }
        }

        private void FormOBKConfig_Load(object sender, EventArgs e)
        {
             cfg = new OBKConfig() ;
            this.textBoxWiFiSSID.DataBindings.Add(new Binding("Text", cfg, "wifi_ssid"));
            this.textBoxWiFiPass.DataBindings.Add(new Binding("Text", cfg, "wifi_pass"));
        }

        private void FormOBKConfig_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void FormOBKConfig_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            for(int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                BKType type = BKType.BK7231N;
                if (fname.Contains("BK7231T"))
                {
                    type = BKType.BK7231T;
                }
                cfg.loadFrom(fname, type);
                refreshBinding();
            }
        }
    }
}
