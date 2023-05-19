using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
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
        string backupsPath = "backups";
       // string label_startRead = "Start Read Flash (Full backup)";
///string label_stopRead = "Stop Read Flash";
        private void Form1_Load(object sender, EventArgs e)
        {

            buttonIPSaveResultToFile.Enabled = false;
            setIPDeviceButtonsState(false);
#if false
            {
                byte[] data1 = { 0x01, 0x02, 0x03 };
                byte crc1 = CRC.Tiny_CRC8(data1, 0, 3);
                byte[] data2 = { (byte)0xF1, (byte)0xF2, (byte)0xF3 };
                byte crc2 = CRC.Tiny_CRC8(data2, 0, 3);

                Console.Write("test");

            }
#endif
            

            //  var t = new TuyaConfig();
            //  t.fromFile("W:/GIT/BK7231GUIFlashTool/BK7231Flasher/bin/Release/backups/CBU_2Gang_8046/readResult_BK7231N_QIO_2023-05-5--13-40-02.bin");
            //// t.extractKeys();

            tabControl1.TabPages.Remove(tabPagePageTool);
            if (Directory.Exists(backupsPath) == false)
            {
                Directory.CreateDirectory(backupsPath);
            }

            if (formObkCfg == null)
            {
                formObkCfg = new FormOBKConfig(this);
            }

            settings = MySettings.CreateAndLoad("settings.cfg");
            List<string> recentIPs = settings.getRecentIPs();
            for(int i = recentIPs.Count-1; i >= 0; i--)
            {
                string s = recentIPs[i];
                comboBoxIP.Items.Add(s);
            }
            if(comboBoxIP.Items.Count == 0)
            {
                comboBoxIP.Items.Add(("127.0.0.1"));
            }
            comboBoxIP.SelectedIndex = 0;

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
            if (settings.HasKey("bAllowBackupRestore"))
            {
                checkBoxAllowBackup.Checked = settings.FindKeyValueBool("bAllowBackupRestore");
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
            saveSettings();
            bWithinSettingSet = false;
        }
        void saveSettings()
        {
            settings.Save("settings.cfg");
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
                    if(worker != null)
                    {
                        worker.Abort();
                    }
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
            float.TryParse(textBox_cfg_readTimeOutMultForLoop.Text.Replace(',','.'),  NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out cfg_readTimeOutMultForLoop);
            float.TryParse(textBox_cfg_readTimeOutMultForSerialClass.Text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out cfg_readTimeOutMultForSerialClass);
            int.TryParse(textBox_cfg_readReplyStyle.Text.Replace(',', '.'), NumberStyles.Integer, CultureInfo.InvariantCulture, out cfg_readReplyStyle);

            return true;
        }
        int cfg_readReplyStyle;
        float cfg_readTimeOutMultForLoop;
        float cfg_readTimeOutMultForSerialClass;
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
        
        void createFlasher()
        {
            flasher = new BK7231Flasher(this, serialName, curType, chosenBaudRate);
            flasher.setReadReplyStyle(cfg_readReplyStyle);
            flasher.setReadTimeOutMultForLoop(cfg_readTimeOutMultForLoop);
            flasher.setReadTimeOutMultForSerialClass(cfg_readTimeOutMultForSerialClass);
        }
        void testWrite()
        {
            clearUp();
            createFlasher();
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
            createFlasher();
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
            createFlasher();
            flasher.setBackupName(lastBackupNameEnteredByUser);
            int startSector = getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doReadAndWrite(startSector, sectors, chosenSourceFile, WriteMode.ReadAndWrite);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doOnlyFlashNew()
        {
            clearUp();
            createFlasher();
            int startSector = getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doReadAndWrite(startSector, sectors, chosenSourceFile, WriteMode.OnlyWrite);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doOnlyFlashOBKConfig()
        {
            clearUp();
            createFlasher();
            flasher.doReadAndWrite(0, 0, "", WriteMode.OnlyOBKConfig);
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
        void restoreRF()
        {
            clearUp();
            createFlasher();
            int startOfs = RFPartitionUtil.getRFOffset(curType);
            byte[] data = RFPartitionUtil.constructRFDataFor(curType, BK7231Flasher.SECTOR_SIZE);
            flasher.doWrite(startOfs, data);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void eraseAll()
        {
            clearUp();
            createFlasher();
            int startOfs = BK7231Flasher.BOOTLOADER_SIZE;
            int sectors = (BK7231Flasher.FLASH_SIZE - startOfs) / BK7231Flasher.SECTOR_SIZE;
            flasher.doErase(startOfs, sectors);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void readThread()
        {
            clearUp();
            createFlasher();
            flasher.setBackupName(lastBackupNameEnteredByUser);
            // thanks to wrap around hack, we can read from start correctly
            int startSector = 0x0;// getBackupStartSectorForCurrentPlatform();
            int sectors = getBackupSectorCountForCurrentPlatform();
            flasher.doRead(startSector, sectors);
            flasher.saveReadResult();
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doOnlyReadOBKConfig()
        {
            clearUp();
            createFlasher();
            // thanks to wrap around hack, we can read from start correctly
            int startSector = OBKFlashLayout.getConfigLocation(curType);
            int sectors = 1;
            flasher.doRead(startSector, sectors);
            byte [] res = flasher.getReadResult();
            bool bError = formObkCfg.tryToLoadOBKConfig(res, curType, false);
            if(bError)
            {
                addLog("OBK config load failed.", Color.DarkOrange);
            }
            else
            {
                addLog("OBK config loaded. You can now view it by clicking 'Change OBK settings' button."+Environment.NewLine, Color.Black);
                addLog("You can also edit it whatever you want." + Environment.NewLine, Color.Black);
                addLog("You can also use 'Write OBK config' button to write it back with your changes." + Environment.NewLine, Color.Black);
            }
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
            if (s.StartsWith("readResult_"))
            {
                s = s.Substring("readResult_".Length);
                s = "Open" + s;
            }
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
                if (s.StartsWith("OpenBK7231T_QIO_"))
                {
                    return true;
                }
            }
            /*string prefix = getFirmwarePrefix(curType);
            if (s.StartsWith(prefix))
            {
                return true;
            }*/
            return false;
        }
        public OBKConfig getConfig()
        {
              return formObkCfg.getCFG();
        }
        public OBKConfig getConfigToWrite()
        {
            if (checkBoxAutoOBKConfig.Checked)
            {
                return formObkCfg.getCFG();
            }
            return null;
        }
        public void onReadResultQIOSaved(byte[] dat, string fullPath)
        {
            if (checkBoxReadOBKConfig.Checked)
            {
                addLog("Backup 2MB created, now will attempt to extract OBK config." + Environment.NewLine, Color.Gray);
                if(formObkCfg.tryToLoadOBKConfig(dat, curType))
                {
                    addLog("OBK config not found." + Environment.NewLine, Color.DarkRed);
                }
                else
                {
                    addLog("OBK config extracted." + Environment.NewLine, Color.Green);
                }
            }
            else
            {
                addLog("Backup 2MB created, but OBK config reading is disabled on GUI, skipping extraction." + Environment.NewLine, Color.Gray);
            }

            if (checkBoxAutoReadTuya.Checked == false)
            {
                addLog("Backup 2MB created, but Tuya config reading is disabled on GUI, skipping extraction."+Environment.NewLine, Color.Gray);
            }
            else
            {
                addLog("Backup 2MB created, now will attempt to extract Tuya config." + Environment.NewLine, Color.Gray);
                try
                {
                    TuyaConfig tc = new TuyaConfig();
                    if (tc.fromBytes(dat) == false)
                    {
                        if (tc.extractKeys() == false)
                        {
                            byte[] mac = RFPartitionUtil.getMACFromQio(dat, curType);
                            Singleton.buttonRead.Invoke((MethodInvoker)delegate {
                                // Running on the UI thread
                                FormExtractedConfig fo = new FormExtractedConfig();
                                fo.showConfig(tc);
                                fo.showMAC(mac);
                                fo.Show();
                            });
                            // also pass to new config window
                            formObkCfg.loadFromTuyaConfig(tc);
                            formObkCfg.onMACLoaded(mac,curType);

                            addLog("Tuya config extracted and shown." + Environment.NewLine, Color.Green);
                            string macStr = "";
                            for(int k = 0; k < 6; k++)
                            {
                                if(k!= 0)
                                {
                                    macStr += ":";
                                }
                                macStr += mac[k].ToString("X2");
                            }
                            addLog("MAC seems to be " + macStr + Environment.NewLine, Color.Green);
                        }
                        else
                        {
                            addLog("Sorry, failed to extract keys from Tuya Config in backup binary." + Environment.NewLine, Color.DarkOrange);
                        }
                    }
                    else
                    {
                        addLog("Sorry, failed to find Tuya Config in backup binary." + Environment.NewLine, Color.DarkOrange);
                    }
                }
                catch (Exception ex)
                {
                    addLog("Sorry, failed to find Tuya Config in backup binary due to an unknown exception " + ex.ToString() + "" + Environment.NewLine, Color.DarkRed);
                }
            }
        }
        public int addToFirmaresList(string dir)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (Exception ex)
            {
                return 0;
            }
            for (int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                fname = Path.GetFileName(fname);
                if (checkFirmwareForCurType(fname) == false)
                {
                    continue;
                }
                comboBoxFirmware.Items.Add(fname);
            }
            return files.Length;
        }
        public void refreshFirmwaresList()
        {
            comboBoxFirmware.Items.Clear();
            if (Directory.Exists(firmwaresPath) == false)
            {
                return;
            }
            int found = 0;
            found += addToFirmaresList(firmwaresPath);
            if (checkBoxAllowBackup.Checked && checkBoxShowAdvanced.Checked)
            {
                found += addToFirmaresList(backupsPath);
            }
            labelMatchingFirmwares.Text = "" + found + " total bins, " + comboBoxFirmware.Items.Count + " matching.";
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
                // kinda hacky, but we are 100% sure that there are no backups in firmwares, so should be ok
                if(File.Exists(chosenSourceFile) == false)
                {
                    chosenSourceFile = Path.Combine(backupsPath, comboBoxFirmware.SelectedItem.ToString());
                }
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
            if (promptForBackupName() == false)
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
        string lastBackupNameEnteredByUser;
        bool promptForBackupName()
        {
            FormPrompt p = new FormPrompt();
            p.ShowDialog();
            if(p.getIsCanceled())
            {
                lastBackupNameEnteredByUser = "";
                return false;
            }
            lastBackupNameEnteredByUser = p.getResult();
            return true;
        }
        private void button4_Click(object sender, EventArgs e)
        {
            showObkConfigFormIfPossible();
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            if (promptForBackupName() == false)
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
            buttonEraseAll.Visible = b;
            buttonRestoreRF.Visible = b;
            checkBoxAllowBackup.Visible = b;
        }
        
        private void buttonOpenBackupsDir_Click(object sender, EventArgs e)
        {
            try
            {
                // opens the folder in explorer
                string path = Path.Combine(Directory.GetCurrentDirectory(), backupsPath);
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
            showObkConfigFormIfPossible();
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

        private void buttonEraseAll_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("This will remove everything, including configuration of OBK and MAC address and RF partition. "+
                "You will need to do 'Restore RF partition' in OBK Web Application/Flash tab to get correct MAC. "+
                "Do it if you have RF issues. Flash OBK after doing erase. This option might require lower bauds. ", "WARNING! NUKE CHIP?", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                if (doGenericOperationPreparations() == false)
                {
                    return;
                }
                //setButtonReadLabel(label_stopRead);
                startWorkerThread(eraseAll);
            }
        }

        private void buttonRestoreRF_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("This will overwrite RF partition with new one with random MAC. " +
                "This can be used to fix strange issue where your device works in AP but don't want to connect to your WiFi. " +
                "It can also fix issue where you have 00 00 00 MAC. You must have CORRECT PLATFORM selected first (T or N), because offsets are different.", "WARNING! NUKE RF?", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                if (doGenericOperationPreparations() == false)
                {
                    return;
                }
                //setButtonReadLabel(label_stopRead);
                startWorkerThread(restoreRF);
            }
        }

        private void checkBoxAllowBackup_CheckedChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("bAllowBackupRestore", checkBoxAllowBackup.Checked);
            refreshFirmwaresList();
        }

        private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://openbekeniot.github.io/webapp/templateImporter.html");
        }
        FormOBKConfig formObkCfg;
        private void buttonChangeOBKSettings_Click(object sender, EventArgs e)
        {
            formObkCfg.Show();
        }
        void showObkConfigFormIfPossible()
        {
            if (formObkCfg.Visible)
            {
                formObkCfg.saveBinding();
            }
        }
        private void buttonWriteOBKConfig_Click(object sender, EventArgs e)
        {
            showObkConfigFormIfPossible();
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doOnlyFlashOBKConfig);
        }

        private void buttonReadOBKConfig_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doOnlyReadOBKConfig);
        }

        private void buttonStartScan_Click(object sender, EventArgs e)
        {
            startOrStopScannerThread();
        }

        private void textBoxScannerThreads_TextChanged(object sender, EventArgs e)
        {
            setMaxWorkersCountFromGUI();
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewItem selectedItem = listView1.FocusedItem;

                ContextMenuStrip contextMenu = new ContextMenuStrip();

                ToolStripMenuItem openPageMenuItem = new ToolStripMenuItem("Open page");
                openPageMenuItem.Click += (s, args) =>
                {
                    string url = selectedItem.SubItems[1].Text; 
                    System.Diagnostics.Process.Start("http://"+url);
                };
                contextMenu.Items.Add(openPageMenuItem);

                ToolStripMenuItem copyUrlMenuItem = new ToolStripMenuItem("Copy URL");
                copyUrlMenuItem.Click += (s, args) =>
                {
                    string url = selectedItem.SubItems[1].Text; 
                    Clipboard.SetText(url);
                };
                contextMenu.Items.Add(copyUrlMenuItem);


                ToolStripMenuItem rebootMenuItem = new ToolStripMenuItem("Reboot");
                rebootMenuItem.Click += (s, args) =>
                {
                    OBKDeviceAPI devo = selectedItem.Tag as OBKDeviceAPI;
                    devo.sendCmnd("reboot",null);
                };
                contextMenu.Items.Add(rebootMenuItem);

                OBKDeviceAPI dev = selectedItem.Tag as OBKDeviceAPI;
                for (int i = 0; i < dev.getPowerSlotsCount(); i++)
                {
                    int slotIndex = i+1; 
                    ToolStripMenuItem toggleMenuItem = new ToolStripMenuItem("Toggle POWER"+ slotIndex);
                    toggleMenuItem.Click += (s, args) =>
                    {
                        OBKDeviceAPI devo = selectedItem.Tag as OBKDeviceAPI;
                        devo.sendCmnd("POWER"+ slotIndex + " TOGGLE", null);
                    };
                    contextMenu.Items.Add(toggleMenuItem);
                }
                
                if(dev.hasDimmerSupport())
                {
                    ToolStripMenuItem dimmerToolsMenuItem = new ToolStripMenuItem("Dimmer");
                    contextMenu.Items.Add(dimmerToolsMenuItem);

                    for(int i = 0; i <= 100; i+=25)
                    {
                        int savedI = i;
                        ToolStripMenuItem dimmerMenuItem = new ToolStripMenuItem("Set Dimmer "+i+"%");
                        dimmerMenuItem.Click += (s, args) =>
                        {
                            OBKDeviceAPI devo = selectedItem.Tag as OBKDeviceAPI;
                            devo.sendCmnd("Dimmer " + savedI + "", null);
                        };
                        dimmerToolsMenuItem.DropDownItems.Add(dimmerMenuItem);
                    }
                }
                if (dev.hasCTSupport())
                {
                    ToolStripMenuItem ctToolsMenuItem = new ToolStripMenuItem("CT");
                    contextMenu.Items.Add(ctToolsMenuItem);

                    for (int i = 154; i <= 500; i += 173)
                    {
                        int savedI = i;
                        ToolStripMenuItem ctMenuItem = new ToolStripMenuItem("Set CT " + i + "");
                        ctMenuItem.Click += (s, args) =>
                        {
                            OBKDeviceAPI devo = selectedItem.Tag as OBKDeviceAPI;
                            devo.sendCmnd("CT " + savedI + "", null);
                        };
                        ctToolsMenuItem.DropDownItems.Add(ctMenuItem);
                    }
                }
                if (dev.hasColorSupport())
                {
                    ToolStripMenuItem colorToolsMenuItem = new ToolStripMenuItem("Color");
                    contextMenu.Items.Add(colorToolsMenuItem);

                    for (int i = 0; i < Colors.list.GetLength(0); i++)
                    {
                        int savedI = i;
                        string name = Colors.list[i, 0];
                        string code = Colors.list[i, 1];
                        ToolStripMenuItem colorMenuItem = new ToolStripMenuItem("Set " + name + " ("+code+")");
                        colorMenuItem.Click += (s, args) =>
                        {
                            OBKDeviceAPI devo = selectedItem.Tag as OBKDeviceAPI;
                            devo.sendCmnd("Color " + code + "", null);
                        };
                        colorToolsMenuItem.DropDownItems.Add(colorMenuItem);
                    }
                }
                contextMenu.Show(listView1, e.Location);
            }
        }

        private void buttonIPCFGDump_Click(object sender, EventArgs e)
        {
            doSingleDeviceOBKCFGDump();
        }

        private void buttonIPDownloadTuyaConfig_Click(object sender, EventArgs e)
        {
            doSingleDeviceTuyaCFGDump();
        }

        private void buttonIPDump2MB_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented yet, as it's not needed");
        }

        private void buttonIPSaveResultToFile_Click(object sender, EventArgs e)
        {
            onIPSaveResultToFileClicked();
        }

        private void buttonStartMassBackup_Click(object sender, EventArgs e)
        {
            buttonStartMassBackup.Enabled = false;
            OBKMassBackup backup = new OBKMassBackup();
            for(int i = 0; i < listView1.Items.Count; i++)
            {
                ListViewItem it = listView1.Items[i];
                OBKDeviceAPI dev = it.Tag as OBKDeviceAPI;
                backup.addDevice(dev);
            }
            backup.setOnProgress(onMassBackupProgress);
            backup.setOnFinished(onMassBackupFinish);
            backup.beginBackupThread();
        }
        private void onMassBackupFinish()
        {
            onMassBackupProgress("Ready!");
            Singleton.labelMassBackupProgress.Invoke((MethodInvoker)delegate {
                buttonStartMassBackup.Enabled = false;
            });
        }
        private void onMassBackupProgress(string txt)
        {
            Singleton.labelMassBackupProgress.Invoke((MethodInvoker)delegate {
                labelMassBackupProgress.Text = txt;
            });
        }
    }
}
