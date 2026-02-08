
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        string firmwaresPath = "firmwares/";
        public static FormMain Singleton;
        int chosenBaudRate;
        string chosenSourceFile;
        int firmwareComboWidthNormal = -1;
        int firmwareComboWidthAdvanced = -1;
        FormCustom formCustom;
        CancellationTokenSource cts;

        public Dictionary<BKType, string> Chips = new Dictionary<BKType, string>()
        {
            { BKType.BK7231T,    "BK7231T" },
            { BKType.BK7231U,    "BK7231U" },
            { BKType.BK7231N,    "BK7231N (T2, T34)" },
            { BKType.BK7231M,    "BK7231M" },
            { BKType.BK7236,     "BK7236 (T3)" },
            { BKType.BK7238,     "BK7238 (T1)" },
            { BKType.BK7252,     "BK7252" },
            { BKType.BK7252N,    "BK7252N (T4)" },
            { BKType.BK7258,     "BK7258 (T5)" },
            { BKType.RTL8710B,   "RTL8710B (AmebaZ)" },
            { BKType.RTL87X0C,   "RTL87X0C (AmebaZ2)" },
            { BKType.RTL8720D,   "RTL8720DN (AmebaD)" },
            { BKType.LN882H,     "LN882H" },
            { BKType.LN8825,     "LN8825" },
            { BKType.BL602,      "BL602" },
            { BKType.BL702,      "BL702" },
            { BKType.ECR6600,    "ECR6600" },
            { BKType.W800,       "W800" },
            { BKType.W600,       "W600 (write)" },
            { BKType.RDA5981,    "RDA5981" },
            { BKType.BekenSPI,   "Beken SPI CH341" },
            { BKType.GenericSPI, "Generic SPI CH341" },
            { BKType.ESP32,      "ESP32" },
        };

        public readonly int[] BaudRates = new int[] { 115200, 230400, 460800, 921600, 1500000, 2000000, 3000000, /*4000000, 6000000*/ };

        public FormMain()
        {
            Singleton = this;
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildVersion>().Value;
            Text += $" (build {(string.IsNullOrWhiteSpace(version) ? "local" : version)})";
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
        private void Control_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Control_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                ////MessageBox.Show("Dropped file: " + file);
                //CustomParms cp = new CustomParms();
                //cp.sourceFileName = file;
                //cp.ofs = 0;
                //cp.len = File.ReadAllBytes(file).Length;
                //doCustomWrite(cp);
                comboBoxFirmware.DropDownStyle = ComboBoxStyle.DropDown;
                comboBoxFirmware.Text = file;
                chosenSourceFile = file;
                addLog("You dragged and dropped file " + file + "...",Color.Black);
            }
        }
        // string label_startRead = "Start Read Flash (Full backup)";
        ///string label_stopRead = "Stop Read Flash";
        private void Form1_Load(object sender, EventArgs e)
        {
            this.DragEnter += new DragEventHandler(Control_DragEnter);
            this.DragDrop += new DragEventHandler(Control_DragDrop);

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var snsettings = (SettingsSection)config.GetSection("system.net/settings");
            snsettings.HttpWebRequest.UseUnsafeHeaderParsing = true;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("system.net/settings");

            buttonIPSaveResultToFile.Enabled = false;
            setIPDeviceButtonsState(false);

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

            foreach(var chip in Chips)
            {
                comboBoxChipType.Items.Add(new ChipType(chip.Key, chip.Value));
            }

            comboBoxChipType.SelectedIndex = 0;
            foreach(var baud in BaudRates)
            {
                comboBoxBaudRate.Items.Add(baud);
            }

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
            refreshType();
            refreshFirmwaresList();

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
            if (settings.HasKey("ScannerPass"))
            {
                textBoxIPScannerPass.Text = settings.FindKeyValue("ScannerPass","admin");
            }
            if (settings.HasKey("ScannerUser"))
            {
                textBoxIPScannerUser.Text = settings.FindKeyValue("ScannerUser");
            }
            if (settings.HasKey("ScannerFirst"))
            {
                textBoxStartIP.Text = settings.FindKeyValue("ScannerFirst");
            }
            if (settings.HasKey("ScannerLast"))
            {
                textBoxEndIP.Text = settings.FindKeyValue("ScannerLast");
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
            if (cur > max)
                cur = max;
            Singleton.textBoxLog.BeginInvoke((MethodInvoker)delegate {
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
        Task worker;
        string getSelectedSerialName()
        {
            if (comboBoxUART.SelectedIndex != -1)
            {
                return comboBoxUART.SelectedItem.ToString();
            }
            return "";
        }
        BaseFlasher flasher;
        string serialName;
        BKType curType;
        void refreshType()
        {
            curType = ((ChipType)comboBoxChipType.SelectedItem).Type;
            if(curType == BKType.BekenSPI || curType == BKType.GenericSPI)
            {
                comboBoxUART.Enabled = false;
                comboBoxBaudRate.Enabled = false;
            }
            else
            {
                comboBoxUART.Enabled = true;
                comboBoxBaudRate.Enabled = true;
            }
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
                        cts?.Cancel();
                        //worker.Abort();
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
            if(comboBoxUART.Enabled)
            {
                serialName = getSelectedSerialName();
                if (serialName.Length <= 0)
                {
                    MessageBox.Show("Please choose a correct serial port or connect one if not present.");
                    return false;
                }
            }
            else
            {
                serialName = "";
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
                cts.Cancel();
                flasher.Dispose();
                //flasher.closePort();
                flasher = null;
            }
        }
        
        void createFlasher()
        {
            switch(curType)
            {
                case BKType.RTL8710B:
                case BKType.RTL8720D:
                case BKType.RTL8721DA:
                case BKType.RTL8720E:
                    flasher = new RTLFlasher(cts.Token);
                    break;
                case BKType.RTL87X0C:
                    flasher = new RTLZ2Flasher(cts.Token);
                    break;
                case BKType.LN882H:
                case BKType.LN8825:
                    flasher = new LN882HFlasher(cts.Token);
                    break;
                case BKType.BL602:
                case BKType.BL702:
                    flasher = new BL602Flasher(cts.Token);
                    break;
                case BKType.BekenSPI:
                    flasher = new SPIFlasher_Beken(cts.Token);
                    break;
                case BKType.GenericSPI:
                    flasher = new SPIFlasher(cts.Token);
                    break;
                case BKType.ECR6600:
                    flasher = new ECR6600Flasher(cts.Token);
                    break;
                case BKType.W600:
                case BKType.W800:
                    flasher = new WMFlasher(cts.Token);
                    break;
                case BKType.RDA5981:
                    flasher = new RDAFlasher(cts.Token);
                    break;
                case BKType.ESP32:
                    flasher = new ESPFlasher(cts.Token);
                    break;
                default:
                    flasher = new BK7231Flasher(cts.Token);
                    break;
            }
            flasher.setBasic(this, serialName, curType, chosenBaudRate);
            flasher.setReadReplyStyle(cfg_readReplyStyle);
            flasher.setReadTimeOutMultForLoop(cfg_readTimeOutMultForLoop);
            flasher.setReadTimeOutMultForSerialClass(cfg_readTimeOutMultForSerialClass);
            flasher.setOverwriteBootloader(checkBoxOverwriteBootloader.Checked);
            flasher.setSkipKeyCheck(checkBoxSkipKeyCheck.Checked);
            flasher.setIgnoreCRCErr(chkIgnoreCRCErr.Checked);
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
        internal void doCustomWrite(CustomParms cp)
        {
            showObkConfigFormIfPossible();
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doOnlyFlashNew, cp);
        }
        void doOnlyFlashNew(object oParm)
        {
            clearUp();
            createFlasher();
            int startSector;
            int sectors;
            CustomParms parms = null;
            if (oParm != null)
            {
                parms = oParm as CustomParms;
            }
            if(parms!=null)
            {
                startSector = parms.ofs;
                sectors = parms.len / BK7231Flasher.SECTOR_SIZE;
                chosenSourceFile = parms.sourceFileName;
            }
            else
            {
                startSector = getBackupStartSectorForCurrentPlatform();
                sectors = getBackupSectorCountForCurrentPlatform();
            }
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
            switch(curType)
            {
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7252:
                    return BK7231Flasher.BOOTLOADER_SIZE;
                default:
                    return 0;

            }
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
            if(startOfs < 0 || data.Length == 0)
            {
                worker = null;
                clearUp();
                setButtonStates(true);
                if(startOfs < 0) addLog($"RF restore is not supported on {curType}" + Environment.NewLine, Color.Red);
                else addLog("Generated RF partition is empty, not supported?" + Environment.NewLine, Color.DarkOrange);
                return;
            }
            flasher.doWrite(startOfs, data);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        internal void restoreRFfromBackup(byte[] fileData)
        {
            clearUp();
            if(doGenericOperationPreparations() == false)
            {
                return;
            }
            startWorkerThread(restoreRFbkp, fileData);
        }
        void restoreRFbkp(object fileData)
        {
            createFlasher();
            int startOfs = RFPartitionUtil.getRFOffset(curType);
            byte[] data = RFPartitionUtil.getRFFromBackup((byte[])fileData, curType, out int addr);
            if(startOfs < 0 || data.Length == 0)
            {
                worker = null;
                clearUp();
                setButtonStates(true);
                if(startOfs < 0) addLog($"RF restore is not supported on {curType}" + Environment.NewLine, Color.Red);
                else addLog("RF partition not found in backup" + Environment.NewLine, Color.DarkOrange);
                return;
            }
            addLog($"RF partition found at 0x{addr:X2}" + Environment.NewLine, Color.Green);
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
            flasher.doErase(startOfs, sectors, true);
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void verifyThread(object oParm)
        {
            if (!string.IsNullOrEmpty(chosenSourceFile))
            {
                byte[] verifyWith = File.ReadAllBytes(chosenSourceFile);
                int sectors = (verifyWith.Length / 4096) + 1;
                clearUp();
                createFlasher();
                int startSector = 0;
                flasher.doRead(startSector, sectors, false);

                byte[] data = flasher.getReadResult();
                int errors = 0;
                for (int i = 0; i < verifyWith.Length; i++)
                {
                    if (data[i] != verifyWith[i])
                    {
                        errors++;
                    }
                }
                if (errors == 0)
                {
                    setState("Verify OK", Color.Green);
                }
                else
                {
                    setState("Verify bad", Color.Red);
                }
            }
            else
            {
                setState("No file", Color.Red);
            }
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void readThread(object oParm)
        {
            bool isFullRead = true;
            CustomParms parms = null;
            if(oParm != null)
            {
                parms = oParm as CustomParms;
            }
            clearUp();
            createFlasher();
            flasher.setBackupName(lastBackupNameEnteredByUser);
            // thanks to wrap-around hack, we can read from start correctly
            int startSector;
            int sectors;
            if (parms!= null)
            {
                startSector = parms.ofs;
                if(curType == BKType.RTL8720D || curType == BKType.RTL87X0C || curType == BKType.RTL8710B)
                    startSector /= BK7231Flasher.SECTOR_SIZE;
                sectors = parms.len / BK7231Flasher.SECTOR_SIZE;
                isFullRead = false;
            }
            else if(curType == BKType.BK7252)
            {
                startSector = 0x11000;
                sectors = getBackupSectorCountForCurrentPlatform() - (startSector/ BK7231Flasher.SECTOR_SIZE);
                addLog("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*" + Environment.NewLine, Color.DarkOrange);
                addLog("BK7252 mode - read offset is 0x11000, we can't access bootloader." + Environment.NewLine, Color.DarkOrange);
                addLog("^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*^*" + Environment.NewLine, Color.DarkOrange);
            }
            else
            {
                startSector = 0x0;// getBackupStartSectorForCurrentPlatform();
                sectors = getBackupSectorCountForCurrentPlatform();
            }
            flasher.doRead(startSector, sectors, isFullRead);
            
            flasher.saveReadResult(startSector);

            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void blankThread(object oParm)
        {
            clearUp();
            createFlasher();
            flasher.doRead(0, 0, true);

            bool bBlank = true;
            int nonBlank = 0;
            byte[] data = flasher.getReadResult();
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0xff)
                {
                    bBlank = false;
                    nonBlank++;
                }
            }
            if (bBlank)
            {
                setState("Blank", Color.Green);
            }
            else
            {
                setState("Not blank", Color.Yellow);
            }
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        void doOnlyReadOBKConfig()
        {
            clearUp();
            createFlasher();
            // thanks to wrap-around hack, we can read from start correctly
            int startSector = OBKFlashLayout.getConfigLocation(curType, out var sectors);
            if(curType == BKType.BL602 || curType == BKType.BL702)
            {
                addLog("Reading partitions..." + Environment.NewLine, Color.Black);
                if(curType == BKType.BL702)
                {
                    flasher.doRead(0x1000, 1);
                }
                else
                {
                    flasher.doRead(0xE000, 1);
                }
                
                var ptdata = flasher.getReadResult();
                try
                {
                    var partition = BL602Utils.PT_Parse(ptdata).First(x => x.Name == "PSM");
                    startSector = (int)partition.Address0;
                    sectors = (int)partition.Length0 / BK7231Flasher.SECTOR_SIZE;
                }
                catch(InvalidOperationException)
                {
                    addLog("No PSM partition! Can't read config." + Environment.NewLine, Color.Red);
                    throw;
                }
                catch(InvalidDataException ex)
                {
                    addLog($"Partition error: {ex.Message}" + Environment.NewLine, Color.Red);
                    addLog($"Can't read config." + Environment.NewLine, Color.Red);
                    throw;
                }
                catch(Exception ex)
                {
                    worker = null;
                    clearUp();
                    setButtonStates(true);
                }
            }

            if(curType == BKType.RTL8720D || curType == BKType.RTL87X0C || curType == BKType.RTL8710B)
            {
                flasher.doRead(startSector / BK7231Flasher.SECTOR_SIZE, sectors);
            }
            else if(curType == BKType.BL602 || curType == BKType.BL702)
            {
                // do it like that so that there would be no need for re-sync
                ((BL602Flasher)flasher).doReadInternal(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
            }
            else
            {
                flasher.doRead(startSector, sectors);
            }
            byte[] res = flasher.getReadResult();
            bool bError = formObkCfg.tryToLoadOBKConfig(res, curType, false);
            if(bError)
            {
                addLog("OBK config load failed." + Environment.NewLine, Color.DarkOrange);
            }
            else
            {
                addLog("OBK config loaded. You can now view it by clicking 'Change OBK settings' button."+Environment.NewLine, Color.Black);
                addLog("You can also edit it however you want." + Environment.NewLine, Color.Black);
                addLog("You can also use 'Write OBK config' button to write it back with your changes." + Environment.NewLine, Color.Black);
            }
            worker = null;
            //setButtonReadLabel(label_startRead);
            clearUp();
            setButtonStates(true);
        }
        public static string getFirmwarePrefix(BKType t)
        {
            switch(t)
            {
                case BKType.BK7231N:
                case BKType.BK7231M:
                case BKType.BK7236:
                case BKType.BK7238:
                case BKType.BK7252N:
                case BKType.BK7258:
                    return $"Open{t}_QIO_";
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7252:
                    return $"Open{t}_UA_";
                default:
                    return $"Open{t}_";
            }
        }
        public bool checkFirmwareForCurType(string s)
        {
            if (s.StartsWith("readResult_"))
            {
                s = s.Substring("readResult_".Length);
                s = "Open" + s;
            }
            switch(curType)
            {
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7231N:
                case BKType.BK7231M:
                case BKType.BK7236:
                case BKType.BK7238:
                case BKType.BK7252:
                case BKType.BK7252N:
                case BKType.BK7258:
                    if(s.StartsWith($"Open{curType}_QIO_") || s.StartsWith($"Open{curType}_UA_"))
                    {
                        return true;
                    }
                    break;
                case BKType.W600:
                case BKType.W800:
                    if(s.StartsWith($"Open{curType}_") && (s.EndsWith(".fls") || s.EndsWith(".bin")))
                    {
                        return true;
                    }
                    break;
                default:
                    if(s.StartsWith($"Open{curType}_"))
                    {
                        return true;
                    }
                    break;
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
        public void onReadResultQIOSaved(byte[] dat, string lastEncryptionKey, string fullPath)
        {
            if (checkBoxReadOBKConfig.Checked)
            {
                addLog("Backup created, now will attempt to extract OBK config." + Environment.NewLine, Color.Gray);
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
                addLog("Backup created, but OBK config reading is disabled on GUI, skipping extraction." + Environment.NewLine, Color.Gray);
            }

            if (checkBoxAutoReadTuya.Checked == false)
            {
                addLog("Backup created, but Tuya config reading is disabled on GUI, skipping extraction."+Environment.NewLine, Color.Gray);
            }
            else
            {
                addLog("Backup created, will now attempt to extract Tuya config." + Environment.NewLine, Color.Gray);
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
                                fo.showEncryption(lastEncryptionKey);
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

            if (comboBoxFirmware.Items.Count > 0)
            {
                comboBoxFirmware.SelectedIndex = 0;
            }
            else
            {
                // If we previously had a manual (drag/drop or browse) path in the textbox, clear it.
                comboBoxFirmware.SelectedIndex = -1;
                comboBoxFirmware.Text = "";
                chosenSourceFile = "";
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
            // Changing platform should reset any manually typed / drag-dropped firmware path
            comboBoxFirmware.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxFirmware.SelectedIndex = -1;
            comboBoxFirmware.Text = "";
            chosenSourceFile = "";

            refreshType();
            refreshFirmwaresList();
            setSettingsKeyAndSave("Platform", comboBoxChipType.SelectedItem);
        }

        private void buttonDownloadLatest_Click(object sender, EventArgs e)
        {
            refreshType();
            var res = MessageBox.Show("Do you want to automatically download latest release?", 
                "Download?", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                downloadLatestFor(curType);
            }
        }
        private void comboBoxFirmware_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxFirmware.SelectedItem != null)
            {
                // If user previously drag/dropped or used browse (free-text), switch back to list mode once an item is selected.
                if (comboBoxFirmware.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    comboBoxFirmware.DropDownStyle = ComboBoxStyle.DropDownList;
                }

                chosenSourceFile = Path.Combine(firmwaresPath, comboBoxFirmware.SelectedItem.ToString());
                // kinda hacky, but we are 100% sure that there are no backups in firmwares, so should be ok
                if (File.Exists(chosenSourceFile) == false)
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

        private void buttonBrowseFirmware_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select firmware to flash";
                ofd.Filter = "Firmware files (*.bin;*.rbl;*.fls;*.img)|*.bin;*.rbl;*.fls;*.img|All files (*.*)|*.*";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                // Match existing drag/drop behaviour
                comboBoxFirmware.DropDownStyle = ComboBoxStyle.DropDown;
                comboBoxFirmware.SelectedIndex = -1;
                comboBoxFirmware.Text = ofd.FileName;
                chosenSourceFile = ofd.FileName;

                addLog("Selected firmware file " + ofd.FileName + "." + Environment.NewLine, Color.Black);
            }
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
            startWorkerThread(readThread,null);
        }
        private void buttonBlankCheck_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            startWorkerThread(blankThread, null);
        }
        private void buttonTestReadWrite_Click(object sender, EventArgs e)
        {
            if (true)
            {
                MessageBox.Show("This option is developer-only, it's disabled in release");
                return;
            }
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(testReadWrite);
        }

        private void buttonTestWrite_Click(object sender, EventArgs e)
        {
            if (true)
            {
                MessageBox.Show("This option is developer-only, it's disabled in release");
                return;
            }
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(testWrite);
        }
        string lastBackupNameEnteredByUser = "unnamedDump.bin";
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
                buttonWriteOBKConfig.Enabled = b;
                buttonReadOBKConfig.Enabled = b;
                buttonRestoreRF.Enabled = b;
                buttonCustomOperation.Enabled = b;
                buttonEraseAll.Enabled = b;
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
            buttonBlankCheck.Visible = b;
            buttonDetect.Visible = b;
            checkBoxAllowBackup.Visible = b;
            checkBoxOverwriteBootloader.Visible = b;
            checkBoxSkipKeyCheck.Visible = b;
            buttonCustomOperation.Visible = b;
            chkIgnoreCRCErr.Visible = b;

            // Advanced-only firmware browse button + combobox width management
            if (firmwareComboWidthNormal < 0)
            {
                firmwareComboWidthNormal = comboBoxFirmware.Width;
                firmwareComboWidthAdvanced = firmwareComboWidthNormal - 26;
            }

            if (buttonBrowseFirmware != null)
            {
                buttonBrowseFirmware.Visible = b;
            }

            comboBoxFirmware.Width = b ? firmwareComboWidthAdvanced : firmwareComboWidthNormal;
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
            var res = MessageBox.Show("Do you want to clear all downloaded firmware files? ", "Remove old firmware files", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                clearFirmwaresList();
            }
        }

        private void buttonWriteOnly_Click(object sender, EventArgs e)
        {
            showObkConfigFormIfPossible();
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            //setButtonReadLabel(label_stopRead);
            startWorkerThread(doOnlyFlashNew, null);
        }

        private void genericLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel ll = sender as LinkLabel;
            System.Diagnostics.Process.Start(ll.Text);
        }

        private void buttonEraseAll_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("This will remove everything from 0x11000, including configuration of OBK and MAC address and RF partition. "+
                "You will need to do 'Restore RF partition' in OBK Web Application/Flash tab to get correct MAC. "+
                "Do it if you have RF issues. Flash OBK after doing erase. This option might require a lower baud rate. ", "WARNING! NUKE CHIP?", MessageBoxButtons.YesNo);
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

        public class CustomParms
        {
            public int ofs, len;
            public string sourceFileName;
            internal bool bBlankCheck;
        }
        public void doCustomRead(CustomParms customRead)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            if (promptForBackupName() == false)
            {
                return;
            }
            startWorkerThread(readThread, customRead);
        }
        
        void startWorkerThread(Action<object> ts, object customArg)
        {
            cts?.Dispose();
            cts = new CancellationTokenSource();
            setButtonStates(false);
            worker = new Task(ts, customArg, cts.Token);
            worker.Start();
        }
        void startWorkerThread(Action ts)
        {
            cts?.Dispose();
            cts = new CancellationTokenSource();
            setButtonStates(false);
            worker = new Task(ts, cts.Token);
            worker.Start();
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
        private void onMassBackupFinish(int totalErrors, int totalRetries)
        {
            onMassBackupProgress("Ready! "+totalErrors+" errors, " + totalRetries + " retries.");
            Singleton.labelMassBackupProgress.Invoke((MethodInvoker)delegate {
                buttonStartMassBackup.Enabled = true;
            });
        }
        private void onMassBackupProgress(string txt)
        {
            Singleton.labelMassBackupProgress.Invoke((MethodInvoker)delegate {
                labelMassBackupProgress.Text = txt;
            });
        }

        private void textBoxIPScannerUser_TextChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("ScannerUser", textBoxIPScannerUser.Text);
        }

        private void textBoxIPScannerPass_TextChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("ScannerPass", textBoxIPScannerPass.Text);
        }

        private void textBoxStartIP_TextChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("ScannerFirst", textBoxStartIP.Text);
        }

        private void textBoxEndIP_TextChanged(object sender, EventArgs e)
        {
            setSettingsKeyAndSave("ScannerLast", textBoxEndIP.Text);
        }

        private void buttonIPScannerOpenDir_Click(object sender, EventArgs e)
        {
            try
            {
                // opens the folder in explorer
                string path = Path.Combine(Directory.GetCurrentDirectory(), backupsPath);
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed, no backups done yet!");
            }
        }

        private void buttonTuyaConfig_CopyJSONToClipBoard_Click(object sender, EventArgs e)
        {
            string text = null;

            // Prefer copying the full underlying JSON/enhanced extraction result rather than what's currently in the textbox
            // (Textbox content can be truncated due to WinForms TextBox limits).
            try
            {
                if (_lastTuyaConfig != null)
                {
                    if (checkBoxTuyaCfgEnhanced != null && checkBoxTuyaCfgEnhanced.Checked)
                    {
                        text = _lastTuyaConfig.getEnhancedExtractionText();
                    }
                    else
                    {
                        text = _lastTuyaConfig.getKeysAsJSON();
                    }
                }
            }
            catch
            {
                // fall back to textbox text
            }

            if (string.IsNullOrEmpty(text))
            {
                text = textBoxTuyaCFGJSON?.Text;
            }

            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("No JSON text available to copy.", "Copy to clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy JSON text to clipboard: " + ex.Message, "Copy to clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void buttonTuyaConfig_CopyTextToClipBoard_Click(object sender, EventArgs e)
        {
            string text = textBoxTuyaCFGText?.Text;
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("No text description available to copy.", "Copy to clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy to clipboard: " + ex.Message, "Clipboard error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        private void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=WunlqIMAdgw");
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            killScanner();
        }

        private void checkBoxOverwriteBootloader_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBoxOverwriteBootloader.Checked == true)
            {
                DialogResult res = MessageBox.Show("This will break bootloader if used incorrectly. Do you have a backup? Are you sure?",
                    "Are you sure?", MessageBoxButtons.YesNo);
                if(res == DialogResult.No)
                {
                    checkBoxOverwriteBootloader.Checked = false;
                }
            }
        }

        private void checkBoxSkipKeyCheck_CheckedChanged(object sender, EventArgs e)
        {

        }
        private void buttonCustomOperation_Click(object sender, EventArgs e)
        {
            if (formCustom == null)
            {
                formCustom = new FormCustom();
                formCustom.fm = this;
            }
            formCustom.ShowDialog();
        }

        class OTAData
        {
            public string ip;
            public byte[] data;
        }
        private void buttonOTAFlash_Click(object sender, EventArgs e)
        {
            string fname = ("D:/OpenBK7231T_1.17.799.rbl");
            buttonOTAFlash.Enabled = false;
            string ip = textBoxOTATarget.Text;
            OTAData data = new OTAData();
            data.ip = ip;
            data.data = System.IO.File.ReadAllBytes(fname);
            System.Threading.Thread thread = new System.Threading.Thread(ThreadOTA);
            thread.Start(data);
        }
        public void ThreadOTA(object ocb)
        {
            OTAData d = (OTAData)ocb;
            try
            {
                var request = (System.Net.WebRequest)System.Net.WebRequest.Create($"http://{d.ip}/api/ota");
                request.Method = "POST";
                request.ContentType = "application/octet-stream";
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(d.data, 0, d.data.Length);
                }
                try
                {
                    using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            //        throw new Exception($"Failed to send OTA file: {response.StatusCode} {response.StatusDescription}");
                        }
                    }
                }
                catch (Exception ex2)
                {
                }
                request = (System.Net.WebRequest)System.Net.WebRequest.Create($"http://{d.ip}/api/reboot");
                request.Method = "POST";
                try
                {
                    using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            //      throw new Exception($"Failed to send reboot command: {response.StatusCode} {response.StatusDescription}");
                        }
                    }
                }
                catch (Exception ex2)
                {
                }

                System.Windows.Forms.MessageBox.Show("OTA update completed successfully.");
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                this.Invoke((MethodInvoker)delegate {
                    buttonOTAFlash.Enabled = true;
                });
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void tabPage8_Enter(object sender, EventArgs e)
        {
            refreshOTAs();
        }
        void refreshOTAs()
        {

        }

        private void chkIgnoreCRCErr_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void textBox_cfg_readReplyStyle_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonVerify_Click(object sender, EventArgs e)
        {
            if (doGenericOperationPreparations() == false)
            {
                return;
            }
            if(curType != BKType.BekenSPI
                && curType != BKType.GenericSPI)
            {
                MessageBox.Show("Not implemented yet.");
                return;
            }
            startWorkerThread(verifyThread, null);
        }

        private void buttonDetect_Click(object sender, EventArgs e)
        {
            CustomParms cp = new CustomParms();
            cp.len = 0;
            cp.ofs = 0;
            cp.sourceFileName = "";
            doCustomWrite(cp);
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildVersion : Attribute
    {
        public string Value { get; set; }

        public BuildVersion(string value)
        {
            Value = value;
        }
    }
}
