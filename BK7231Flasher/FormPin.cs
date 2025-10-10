using System;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormPin : Form
    {
        int index;
        OBKConfig cfg;

        public FormPin()
        {
            InitializeComponent();
            comboBoxRole.Items.AddRange(OBKRoles.names);
        }

        private void FormPin_Load(object sender, EventArgs e)
        {

        }

        internal void setupEditingPin(OBKConfig cfg, int index)
        {
            this.index = index;
            this.cfg = cfg;
            comboBoxRole.SelectedIndex = cfg.getPinRole(index);
            textBoxCh0.Text = ""+cfg.getPinChannel(index);
            textBoxCh1.Text = "" + cfg.getPinChannel2(index);
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            int ch0 = 0;
            int ch1 = 0;
            try
            {
                ch0 = int.Parse(textBoxCh0.Text);
                ch1 = int.Parse(textBoxCh1.Text);
            }
            catch(Exception ex)
            {

            }
            cfg.setPinRole(this.index, (byte)comboBoxRole.SelectedIndex);
            cfg.setPinChannel(this.index, (byte)ch0);
            cfg.setPinChannel2(this.index, (byte)ch1);
            this.Close();
        }
    }
}
