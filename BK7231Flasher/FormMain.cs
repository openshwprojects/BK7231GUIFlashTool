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
    public partial class FormMain : Form, ILogListener
    {
        string firmwaresPath = "firmwares/";
        public static FormMain Singleton;
        int chosenBaudRate;

        public FormMain()
        {
            Singleton = this;
            InitializeComponent();

        }
        string[] allPorts;

        void setPorts(string [] newPorts)
        {
            if(allPorts != null)
            {
                if(allPorts.Length == newPorts.Length)
                {
                    bool bChange = false;
                    for(int i = 0; i < allPorts.Length; i++)
                    {
                        if(allPorts[i] != newPorts[i])
                        {
                            bChange = true;
                            break;
                        }
                    }
                    if(bChange == false)
                    {
                        return;
                    }
                }
            }

            string prevPort = "";
            if(comboBoxUART.SelectedIndex != -1)
            {
                prevPort = comboBoxUART.SelectedItem.ToString();
            }
            allPorts = newPorts;
            comboBoxUART.Items.Clear();
            int newIndex = allPorts.Length - 1;
            for(int i = 0; i <  allPorts.Length; i++)
            {
                if (prevPort == allPorts[i])
                    newIndex = i;
                comboBoxUART.Items.Add(allPorts[i]);
            }
            if(newIndex != -1)
            {
                comboBoxUART.SelectedIndex = newIndex;
            }
        }
        void scanForCOMPorts()
        {
            string[] newPorts = SerialPort.GetPortNames();
            setPorts(newPorts);
        }
        string label_startRead = "Start Read Flash (Full backup)";
        string label_stopRead = "Stop Read Flash";
        private void Form1_Load(object sender, EventArgs e)
        {
            scanForCOMPorts();
            setButtonReadLabel(label_startRead);

            comboBoxChipType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxUART.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxFirmware.DropDownStyle = ComboBoxStyle.DropDownList;

            comboBoxChipType.Items.Add(BKType.BK7231T);
            comboBoxChipType.Items.Add(BKType.BK7231N);

            comboBoxChipType.SelectedIndex = 0;

            comboBoxBaudRate.Items.Add(115200);
            comboBoxBaudRate.Items.Add(921600);
            comboBoxBaudRate.Items.Add(1500000);

            comboBoxBaudRate.SelectedIndex = 1;

            try
            {
                if (false==Directory.Exists(firmwaresPath))
                {
                    Directory.CreateDirectory(firmwaresPath);
                }
            }
            catch(Exception ex)
            {

            }
            refreshType();
            refreshFirmwaresList();
        }
        int getBaudRateFromGUI()
        {
            int r = 0;
            try
            {
                r = int.Parse(comboBoxBaudRate.Text);
            }
            catch(Exception ex)
            {
                return 0;
            }
            return r;
        }
        public static void AppendText(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }
        public void setProgress(int cur, int max)
        {
            Singleton.textBoxLog.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                progressBar1.Maximum = max;
                progressBar1.Value = cur;
            });
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
        string getSelectedSerialName()
        {
            if (comboBoxUART.SelectedIndex != -1)
            {
                return comboBoxUART.SelectedItem.ToString();
            }
            return "";
        }
        BK7231Flasher flasher;
        string serialName;
        BKType curType;
        void refreshType()
        {
            curType = (BKType)Enum.Parse(typeof(BKType), comboBoxChipType.SelectedItem.ToString(), true);
        }
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
            refreshType();
            chosenBaudRate = getBaudRateFromGUI();
            if(chosenBaudRate <= 0)
            {
                MessageBox.Show("Please enter a correct number for a baud rate.");
                return;
            }
            serialName = getSelectedSerialName();
            if (serialName.Length <= 0)
            {
                MessageBox.Show("Please choose a correct serial port or connect one if not present.");
                return;
            }
            setButtonReadLabel(label_stopRead);
            worker = new Thread(new ThreadStart(readThread));
            worker.Start();
        }
        void clearUp()
        {
            if (flasher != null)
            {
                flasher.closePort();
                flasher = null;
            }
        }
        void readThread()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector;
            int sectors;
            if(curType == BKType.BK7231N)
            {
                startSector = 0;
                sectors = 0x200000 / 0x1000;
            }
            else
            {
                startSector = 0x11000;
                sectors = (0x200000-startSector) / 0x1000;
            }
            flasher.doRead(startSector, sectors);
            flasher.saveReadResult();
            worker = null;
            setButtonReadLabel(label_startRead);
            clearUp();
        }
        public bool checkFirmwareForCurType(string s)
        {
            if (curType == BKType.BK7231N)
            {
                if (s.StartsWith("OpenBK7231N_QIO_"))
                {
                    return true;
                }
            }
            if (curType == BKType.BK7231T)
            {
                if (s.StartsWith("OpenBK7231T_UA_"))
                {
                    return true;
                }
            }
            return false;
        }
        public void refreshFirmwaresList()
        {
            string[] files = Directory.GetFiles(firmwaresPath);
            comboBoxFirmware.Items.Clear();
            for(int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                fname = Path.GetFileName(fname);
                if (checkFirmwareForCurType(fname) == false)
                {
                    continue;
                }
                comboBoxFirmware.Items.Add(fname);
            }
            labelMatchingFirmwares.Text = "" + files.Length + " total bins, " + comboBoxFirmware.Items.Count + " matching.";
            if (comboBoxFirmware.Items.Count>0)
            {
                comboBoxFirmware.SelectedIndex = 0;
            }
        }
        private void comboBoxChipType_SelectedIndexChanged(object sender, EventArgs e)
        {
            refreshType();
            refreshFirmwaresList();
        }

        private void buttonDownloadLatest_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("Do you want to automatically download latest release from WWW?", 
                "Stop?", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {

            }
        }
    }
}
