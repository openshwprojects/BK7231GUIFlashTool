using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
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
        FormMain.CustomParms cp;
        bool doSharedPrepare()
        {
            if (checkRound())
            {
                return true;
            }
            cp = new FormMain.CustomParms();
            cp.ofs = ofs;
            cp.len = len;
            return false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if(doSharedPrepare())
            {
                return;
            }
            this.Close();
            fm.doCustomRead(cp);
        }

        private void buttonCustomWrite_Click(object sender, EventArgs e)
        {
            if (doSharedPrepare())
            {
                return;
            }
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Select a BIN file"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            cp.sourceFileName = openFileDialog.FileName;
            this.Close();
            fm.doCustomWrite(cp);
        }

        private void btnRestoreRF_Click(object sender, EventArgs e)
        {
            if(doSharedPrepare())
            {
                return;
            }
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Select a backup file"
            };

            if(openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            this.Close();
            var file = new FileStream(openFileDialog.FileName, FileMode.Open);
            var data = new byte[file.Length];
            file.Read(data, 0, data.Length);
            file.Dispose();
            fm.restoreRFfromBackup(data);
        }
    }
}
