using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormPrompt : Form
    {
        string result;
        bool bIsCanceled = true;
        public FormPrompt()
        {
            InitializeComponent();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormPrompt_Load(object sender, EventArgs e)
        {
        }
        public string getResult()
        {
            return result;
        }

        internal bool getIsCanceled()
        {
            return bIsCanceled;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            result = textBox1.Text;
            bIsCanceled = false;
            this.Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var textboxSender = (TextBox)sender;
            var cursorPosition = textboxSender.SelectionStart;
            textboxSender.Text = Regex.Replace(textboxSender.Text, "[^0-9a-zA-Z_]", "");
            textboxSender.SelectionStart = cursorPosition;
        }
    }
}
