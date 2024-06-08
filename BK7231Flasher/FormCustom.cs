using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormCustom : Form
    {
        public FormMain fm;

        public FormCustom()
        {
            InitializeComponent();

        }

        private void FormCustom_Load(object sender, EventArgs e)
        {

        }

        static int ParseStringToInt(string input)
        {
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
                return int.Parse(input, NumberStyles.HexNumber);
            }
            else
            {
                return int.Parse(input);
            }
        }
        int ofs = 0;
        int len = 0;
        bool checkRound()
        {
            try
            {
                ofs = ParseStringToInt(textBoxCh0.Text);
                len = ParseStringToInt(textBoxCh1.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please enter correct len/ofs");
                return true;
            }
            bool bChanged = false;
            if((ofs % BK7231Flasher.SECTOR_SIZE) != 0)
            {
                textBoxCh0.Text =
                   ((ofs / BK7231Flasher.SECTOR_SIZE) * BK7231Flasher.SECTOR_SIZE).ToString("X2");
                bChanged = true;
            }
            if ((len % BK7231Flasher.SECTOR_SIZE) != 0)
            {
                textBoxCh1.Text =
                   ((len / BK7231Flasher.SECTOR_SIZE) * BK7231Flasher.SECTOR_SIZE).ToString("X2");
                bChanged = true;
            }
            if (bChanged)
            {
                MessageBox.Show("Adjusted offsets to be multiply of SECTOR_SIZE ("
                    +BK7231Flasher.SECTOR_SIZE+"), adjusted, please recheck");
                return true;
            }

            return false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (checkRound())
            {
                return;
            }
            FormMain.CustomParms cp;
            cp = new FormMain.CustomParms();
            cp.ofs = ofs;
            cp.len = len;
            this.Close();
            fm.doCustomRead(cp);
        }
    }
}
