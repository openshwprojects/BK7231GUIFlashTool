using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class Form1 : Form, ILogListener
    {
        public static Form1 Singleton;
        SerialPort serial;
        public Form1()
        {
            Singleton = this;
            InitializeComponent();

        }

        string label_startRead = "Start Read";
        string label_stopRead = "Stop Read";
        private void Form1_Load(object sender, EventArgs e)
        {
            serial = new SerialPort("COM14", 115200, Parity.None, 8, StopBits.One);
            serial.Open();
        }
        public static void AppendText(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }
        public void addLog(string s, Color col)
        {
            Singleton.textBoxLog.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                AppendText(Singleton.textBoxLog, s, col);
            });
        }
        public void setButtonReadLabel(string s)
        {
            Singleton.buttonRead.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                Singleton.buttonRead.Text = s;
            });
        }
        Thread worker;
        private void buttonRead_Click(object sender, EventArgs e)
        {
            if(worker != null)
            {
                var res = MessageBox.Show("Do you want to interrupt flashing?", "Stop?", MessageBoxButtons.YesNo);
                if(res == DialogResult.Yes)
                {
                    worker.Abort();
                    worker = null;
                    setButtonReadLabel(label_startRead);
                }
                return;
            }
            setButtonReadLabel(label_stopRead);
            worker = new Thread(new ThreadStart(readThread));
            worker.Start();
        }
        void readThread()
        {
            BK7231Flasher f = new BK7231Flasher(this,serial);
            f.doRead();
            worker = null;
            setButtonReadLabel(label_startRead);
        }
    }
}
