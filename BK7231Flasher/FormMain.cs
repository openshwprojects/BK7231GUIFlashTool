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
       // string label_startRead = "Start Read Flash (Full backup)";
///string label_stopRead = "Stop Read Flash";
        private void Form1_Load(object sender, EventArgs e)
        {
            tabControl1.TabPages.Remove(tabPagePageTool);
            if (Directory.Exists("backups") == false)
            {
                Directory.CreateDirectory("backups");
            }

            settings = MySettings.CreateAndLoad("settings.cfg");
            // do not overwrite user settings with default
            bWithinSettingSet = true;

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
            // allow overwrite old settings with new
            bWithinSettingSet = false;
            applySettings();
            refreshAdvancedOptions();

            //foreach(var c in this.Controls)
            //{
            //    if(c is LinkLabel)
            //    {
            //        LinkLabel ll = c as LinkLabel;
            //        ll.LinkClicked += genericLinkClicked;
            //    }
            //    if(c is TabControl)
            //    {
            //        TabControl tc = c as TabControl;
            //        foreach(TabPage tp in tc.TabPages)
            //        {
            //            tp.
            //        }
            //    }
            //}
        }
        MySettings settings;
        void applySettings()
        {
            if (settings.HasKey("BaudRate"))
            {
                setComboBoxValueByContent(comboBoxBaudRate, settings.FindKeyValue("BaudRate"));
            }
            if (settings.HasKey("Platform"))
            {
                setComboBoxValueByContent(comboBoxChipType, settings.FindKeyValue("Platform"));
            }
            if (settings.HasKey("Port"))
            {
                setComboBoxValueByContent(comboBoxUART, settings.FindKeyValue("Port"));
            }
            if (settings.HasKey("bAdvanced"))
            {
               checkBoxShowAdvanced.Checked = settings.FindKeyValueBool("bAdvanced");
            }
        }
        public void setComboBoxValueByContent(ComboBox comboBox, string itemToSet)
        {
            int index = -1;
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i].ToString() == itemToSet)
                {
                    index = i;
                    break;
                }
            }
            if(index != -1)
            {
                comboBox.SelectedIndex = index;
            }
        }
        void setSettingsKeyAndSave(string key, object value)
        {
            if (value == null)
                return;
            setSettingsKeyAndSave(key, value.ToString());
        }
        bool bWithinSettingSet;
        void setSettingsKeyAndSave(string key, string value)
        {
            if (bWithinSettingSet)
            {
                return;
            }
            bWithinSettingSet = true;
            settings.SetKeyValue(key, value);
            settings.Save("settings.cfg");
            bWithinSettingSet = false;
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
        public void setState(string s, Color col)
        {
            Singleton.textBoxLog.Invoke((MethodInvoker)delegate {
                Singleton.labelState.Text = s;
                Singleton.labelState.BackColor = col;
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
                    setState("Interrupted by user.", Color.Yellow);
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
            startSector = BK7231Flasher.BOOTLOADER_SIZE;
            byte[] dat = new byte[sectors * BK7231Flasher.SECTOR_SIZE];
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
            startSector = BK7231Flasher.BOOTLOADER_SIZE;
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
            flasher.doReadAndWrite(startSector, sectors, chosenSourceFile,false);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doOnlyFlashNew()
        {
            clearUp();
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            int startSector = getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doReadAndWrite(startSector, sectors, chosenSourceFile,true);
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
                startSector = BK7231Flasher.BOOTLOADER_SIZE;
            }
            return startSector;
        }
        int getBackupSectorCountForCurrentPlatform()
        {
#if false
            int sectors;
            sectors = (BK7231Flasher.FLASH_SIZE - getBackupStartSectorForCurrentPlatform()) / BK7231Flasher.SECTOR_SIZE;
            return sectors;
#else
            int sectors;
            sectors = (BK7231Flasher.FLASH_SIZE) / BK7231Flasher.SECTOR_SIZE;
            return sectors;
#endif
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
            comboBoxFirmware.Items.Clear();
            if (Directory.Exists(firmwaresPath) == false)
            {
                return;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(firmwaresPath);
            }
            catch(Exception ex)
            {
                return;
            }
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
        public void clearFirmwaresList()
        {
            if (Directory.Exists(firmwaresPath) == false)
            {
                return;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(firmwaresPath);
            }
            catch (Exception ex)
            {
                return;
            }
            for (int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                File.Delete(fname);
            }
            refreshFirmwaresList();
        }
        private void comboBoxChipType_SelectedIndexChanged(object sender, EventArgs e)
        {
            refreshType();
            refreshFirmwaresList();
            setSettingsKeyAndSave("Platform", comboBoxChipType.SelectedItem);
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
                buttonWriteOnly.Enabled = b;
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

        private void comboBoxUART_SelectedIndexChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("Port", comboBoxUART.SelectedItem);
        }

        private void comboBoxBaudRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("BaudRate", comboBoxBaudRate.SelectedItem);
        }

        private void checkBoxShowAdvanced_CheckedChanged(object sender, EventArgs e)
        {
            refreshAdvancedOptions();
            setSettingsKeyAndSave("bAdvanced", checkBoxShowAdvanced.Checked); 
        }
        void refreshAdvancedOptions()
        {
            showAdvancedOptions(checkBoxShowAdvanced.Checked);
        }

        private void showAdvancedOptions(bool b)
        {
            buttonTestReadWrite.Visible = b;
            buttonTestWrite.Visible = b;
        }

        private void buttonOpenBackupsDir_Click(object sender, EventArgs e)
        {
            try
            {
                // opens the folder in explorer
                string path = Path.Combine(Directory.GetCurrentDirectory(), "backups");
                Process.Start("explorer.exe", path);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Failed, no firmwares loaded yet!");
            }
        }

        private void buttonClearOldFirmware_Click(object sender, EventArgs e)
        {
            clearFirmwaresList();
        }

        private void buttonWriteOnly_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doOnlyFlashNew);
        }

        private void genericLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel ll = sender as LinkLabel;
            System.Diagnostics.Process.Start(ll.Text);
        }
    }
}
