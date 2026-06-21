using System;
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
              string json = tc.getEnhancedExtractionText();
              textBoxTuyaCFGJSON.Text = string.IsNullOrWhiteSpace(json) ? tc.getKeysAsJSON() : json;
              textBoxTuyaCFGText.Text = tc.getKeysHumanReadableEnhanced();
        }
        private void FormExtractedConfig_Load(object sender, EventArgs e)
        {

        }
        internal void showMAC(byte[] mac)
        {
            string macString = BitConverter.ToString(mac).Replace("-", ":");
            labelMac.Text = "The MAC address of this device seems to be " + macString;
        }
        internal void showEncryption(string enc)
        {
            if (enc.Length == 0)
            {
                labelKey.Text = "";
                return;
            }
            labelKey.Text = "The encryption key of this device seems to be " + enc;
        }
    }
}
