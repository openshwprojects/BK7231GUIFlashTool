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
    public partial class FormExtractedConfig : Form
    {
        public FormExtractedConfig()
        {
            InitializeComponent();
        }

        internal void showConfig(TuyaConfig tc)
        {
              textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();
              textBoxTuyaCFGText.Text = tc.getKeysHumanReadable();
        }
        private void FormExtractedConfig_Load(object sender, EventArgs e)
        {

        }
        internal void showMAC(byte[] mac)
        {
            string macString = BitConverter.ToString(mac).Replace("-", ":");
            labelMac.Text = "The MAC address of this device seems to be " + macString;
        }
    }
}
