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
        string chosenSourceFile;

        public FormMain()
        {
            Singleton = this;
            InitializeComponent();

        }
        public string getFirmwareDir()
        {
            return firmwaresPath;
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
            //setButtonReadLabel(label_startRead);
            setButtonStates(true);

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

            if (false)
            {
                downloadLatestFor(BKType.BK7231N);
            }
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
                RichTextUtil.AppendText(Singleton.textBoxLog, s, col);
            });
        }
        //public void setButtonReadLabel(string s)
        //{
        //    Singleton.buttonRead.Invoke((MethodInvoker)delegate {
        //        // Running on the UI thread
        //        Singleton.buttonRead.Text = s;
        //    });
        //}
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
        bool interruptIfRequired()
        {
            if (worker != null)
            {
                var res = MessageBox.Show("Do you want to interrupt flashing?", "Stop?", MessageBoxButtons.YesNo);
                if (res == DialogResult.Yes)
                {
                    worker.Abort();
                    worker = null;
                    //setButtonReadLabel(label_startRead);
                    setButtonStates(true);
                }
                return false;
            }
            return true;
        }
        bool doGenericOperationPreparations()
        {
            if (interruptIfRequired() == false)
            {
                return false;
            }
            refreshType();
            chosenBaudRate = getBaudRateFromGUI();
            if (chosenBaudRate <= 0)
            {
                MessageBox.Show("Please enter a correct number for a baud rate.");
                return false;
            }
            serialName = getSelectedSerialName();
            if (serialName.Length <= 0)
            {
                MessageBox.Show("Please choose a correct serial port or connect one if not present.");
                return false;
            }
            return true;
        }
        void downloadLatestFor(BKType type)
        {
            FormDownloader fd = new FormDownloader(this, type);
            fd.ShowDialog();
            refreshType();
            refreshFirmwaresList();
        }
        void clearUp()
        {
            if (flasher != null)
            {
                flasher.closePort();
                flasher = null;
            }
        }
        
        void testWrite()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector;
            int sectors;
            sectors = 5;
            startSector = 0x11000;
            byte[] dat = new byte[sectors * 0x1000];
            int baseVal = BK7231Flasher.rand.Next();
            for(int i = 0; i < dat.Length; i++)
            {
                int useVal = baseVal + i;
                dat[i] = (byte)(useVal % 256);
            }
            flasher.doWrite(startSector, dat);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void testReadWrite()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector;
            int sectors;
            sectors = 2;
            startSector = 0x11000;
            flasher.doTestReadWrite(startSector, sectors);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doBackupAndFlashNew()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector = getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doReadAndWrite(startSector, sectors, chosenSourceFile);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        int getBackupStartSectorForCurrentPlatform()
        {
            int startSector;
            if (curType == BKType.BK7231N)
            {
                startSector = 0;
            }
            else
            {
                startSector = 0x11000;
            }
            return startSector;
        }
        int getBackupSectorCountForCurrentPlatform()
        {
            int sectors;
            sectors = (0x200000 - getBackupStartSectorForCurrentPlatform()) / 0x1000;
            return sectors;
        }
        void readThread()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector = getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doRead(startSector, sectors);
            flasher.saveReadResult();
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        public static string getFirmwarePrefix(BKType t)
        {
            if (t == BKType.BK7231N)
            {
                return ("OpenBK7231N_QIO_");
            }
            if (t == BKType.BK7231T)
            {
                return ("OpenBK7231T_UA_");
            }
            return "Error_Firmware";
        }
        public bool checkFirmwareForCurType(string s)
        {
            string prefix = getFirmwarePrefix(curType);
            if (s.StartsWith(prefix))
            {
                return true;
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
            refreshType();
            var res = MessageBox.Show("Do you want to automatically download latest release from WWW?", 
                "Download?", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                downloadLatestFor(curType);
            }
        }

        private void comboBoxFirmware_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(comboBoxFirmware.SelectedItem != null)
            {
                chosenSourceFile = Path.Combine(firmwaresPath,comboBoxFirmware.SelectedItem.ToString());
            }
            else
            {
                chosenSourceFile = "";
            }
        }

        private void comboBoxFirmware_Click(object sender, EventArgs e)
        {
            refreshType();
            if (comboBoxFirmware.Items.Count == 0)
            {
                var res = MessageBox.Show("No firmwares yet. Do you want to download one automatically?",
                    "Download?", MessageBoxButtons.YesNo);
                if (res == DialogResult.Yes)
                {
                    downloadLatestFor(curType);
                }
            }
        }

        void startWorkerThread(ThreadStart ts)
        {
            setButtonStates(false);
            worker = new Thread(ts);
            worker.Start();
        }
        private void buttonRead_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
           // setButtonReadLabel(label_stopRead);
            startWorkerThread(readThread);
        }
        private void buttonTestReadWrite_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(testReadWrite);
        }

        private void buttonTestWrite_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(testWrite);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doBackupAndFlashNew);
        }

        private void timer100ms_Tick(object sender, EventArgs e)
        {
            scanForCOMPorts();
        }
        void setButtonStates(bool b)
        {
            Singleton.buttonRead.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                buttonRead.Enabled = b;
                buttonTestReadWrite.Enabled = b;
                buttonTestWrite.Enabled = b;
                buttonDoBackupAndFlashNew.Enabled = b;
                buttonStop.Enabled = !b;
            });
        }
        private void buttonStop_Click(object sender, EventArgs e)
        {
            interruptIfRequired();
        }

        private void buttonClearLog_Click(object sender, EventArgs e)
        {
            textBoxLog.Text = "";
        }
    }
}
