namespace BK7231Flasher
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.buttonRead = new System.Windows.Forms.Button();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboBoxUART = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxChipType = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBoxFirmware = new System.Windows.Forms.ComboBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.main = new System.Windows.Forms.TabPage();
            this.chkIgnoreCRCErr = new System.Windows.Forms.CheckBox();
            this.buttonCustomOperation = new System.Windows.Forms.Button();
            this.checkBoxSkipKeyCheck = new System.Windows.Forms.CheckBox();
            this.checkBoxOverwriteBootloader = new System.Windows.Forms.CheckBox();
            this.buttonReadOBKConfig = new System.Windows.Forms.Button();
            this.buttonWriteOBKConfig = new System.Windows.Forms.Button();
            this.checkBoxReadOBKConfig = new System.Windows.Forms.CheckBox();
            this.checkBoxAutoReadTuya = new System.Windows.Forms.CheckBox();
            this.buttonChangeOBKSettings = new System.Windows.Forms.Button();
            this.checkBoxAutoOBKConfig = new System.Windows.Forms.CheckBox();
            this.checkBoxAllowBackup = new System.Windows.Forms.CheckBox();
            this.buttonRestoreRF = new System.Windows.Forms.Button();
            this.buttonEraseAll = new System.Windows.Forms.Button();
            this.buttonClearOldFirmware = new System.Windows.Forms.Button();
            this.buttonOpenBackupsDir = new System.Windows.Forms.Button();
            this.labelState = new System.Windows.Forms.Label();
            this.buttonWriteOnly = new System.Windows.Forms.Button();
            this.buttonClearLog = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.checkBoxShowAdvanced = new System.Windows.Forms.CheckBox();
            this.buttonDoBackupAndFlashNew = new System.Windows.Forms.Button();
            this.buttonTestWrite = new System.Windows.Forms.Button();
            this.buttonTestReadWrite = new System.Windows.Forms.Button();
            this.buttonDownloadLatest = new System.Windows.Forms.Button();
            this.labelMatchingFirmwares = new System.Windows.Forms.Label();
            this.comboBoxBaudRate = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.tabPagePageTool = new System.Windows.Forms.TabPage();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button3 = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.extract = new System.Windows.Forms.TabPage();
            this.lblKeyInfo = new System.Windows.Forms.Label();
            this.txtKey = new System.Windows.Forms.TextBox();
            this.chkChangeKey = new System.Windows.Forms.CheckBox();
            this.buttonImportConfigFileDialog = new System.Windows.Forms.Button();
            this.linkLabel5 = new System.Windows.Forms.LinkLabel();
            this.label32 = new System.Windows.Forms.Label();
            this.buttonTuyaConfig_CopyTextToClipBoard = new System.Windows.Forms.Button();
            this.buttonTuyaConfig_CopyJSONToClipBoard = new System.Windows.Forms.Button();
            this.label18 = new System.Windows.Forms.Label();
            this.textBoxTuyaCFGText = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.linkLabel4 = new System.Windows.Forms.LinkLabel();
            this.textBoxTuyaCFGJSON = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.getCFG = new System.Windows.Forms.TabPage();
            this.buttonIPSaveResultToFile = new System.Windows.Forms.Button();
            this.progressBarIPOperation = new System.Windows.Forms.ProgressBar();
            this.labelIPOperationStatus = new System.Windows.Forms.Label();
            this.comboBoxIP = new System.Windows.Forms.ComboBox();
            this.buttonIPDownloadTuyaConfig = new System.Windows.Forms.Button();
            this.label20 = new System.Windows.Forms.Label();
            this.buttonIPCFGDump = new System.Windows.Forms.Button();
            this.buttonIPDump2MB = new System.Windows.Forms.Button();
            this.labelCheckCommunicationStatus = new System.Windows.Forms.Label();
            this.buttonCheckCommunication = new System.Windows.Forms.Button();
            this.label19 = new System.Windows.Forms.Label();
            this.LANscanner = new System.Windows.Forms.TabPage();
            this.button4 = new System.Windows.Forms.Button();
            this.buttonIPScannerOpenDir = new System.Windows.Forms.Button();
            this.textBoxIPScannerPass = new System.Windows.Forms.TextBox();
            this.label31 = new System.Windows.Forms.Label();
            this.textBoxIPScannerUser = new System.Windows.Forms.TextBox();
            this.label30 = new System.Windows.Forms.Label();
            this.textBoxBoxScannerRetries = new System.Windows.Forms.TextBox();
            this.label28 = new System.Windows.Forms.Label();
            this.label29 = new System.Windows.Forms.Label();
            this.labelMassBackupProgress = new System.Windows.Forms.Label();
            this.buttonStartMassBackup = new System.Windows.Forms.Button();
            this.labelScanState = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnID = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.buttonStartScan = new System.Windows.Forms.Button();
            this.textBoxScannerThreads = new System.Windows.Forms.TextBox();
            this.label23 = new System.Windows.Forms.Label();
            this.textBoxEndIP = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.textBoxStartIP = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.UARTsettings = new System.Windows.Forms.TabPage();
            this.label27 = new System.Windows.Forms.Label();
            this.textBox_cfg_readReplyStyle = new System.Windows.Forms.TextBox();
            this.textBox_cfg_readTimeOutMultForLoop = new System.Windows.Forms.TextBox();
            this.label26 = new System.Windows.Forms.Label();
            this.textBox_cfg_readTimeOutMultForSerialClass = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.OTAtools = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label34 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.listBoxOTA = new System.Windows.Forms.ListBox();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.label33 = new System.Windows.Forms.Label();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.buttonOTAFlash = new System.Windows.Forms.Button();
            this.textBoxOTATarget = new System.Windows.Forms.TextBox();
            this.Docs = new System.Windows.Forms.TabPage();
            this.label35 = new System.Windows.Forms.Label();
            this.linkLabel6 = new System.Windows.Forms.LinkLabel();
            this.label36 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label12 = new System.Windows.Forms.Label();
            this.linkLabelSPIFlasher = new System.Windows.Forms.LinkLabel();
            this.label11 = new System.Windows.Forms.Label();
            this.linkLabelForumDevicesSectio = new System.Windows.Forms.LinkLabel();
            this.linkLabelDevicesDB = new System.Windows.Forms.LinkLabel();
            this.linkLabelForum = new System.Windows.Forms.LinkLabel();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.linkLabelOpenBeken = new System.Windows.Forms.LinkLabel();
            this.timer100ms = new System.Windows.Forms.Timer(this.components);
            this.tabControl1.SuspendLayout();
            this.main.SuspendLayout();
            this.tabPagePageTool.SuspendLayout();
            this.extract.SuspendLayout();
            this.getCFG.SuspendLayout();
            this.LANscanner.SuspendLayout();
            this.UARTsettings.SuspendLayout();
            this.OTAtools.SuspendLayout();
            this.panel1.SuspendLayout();
            this.Docs.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonRead
            // 
            this.buttonRead.Location = new System.Drawing.Point(185, 121);
            this.buttonRead.Name = "buttonRead";
            this.buttonRead.Size = new System.Drawing.Size(168, 49);
            this.buttonRead.TabIndex = 1;
            this.buttonRead.Text = "Firmware backup (read) only";
            this.buttonRead.UseVisualStyleBackColor = true;
            this.buttonRead.Click += new System.EventHandler(this.buttonRead_Click);
            // 
            // textBoxLog
            // 
            this.textBoxLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxLog.Location = new System.Drawing.Point(3, 206);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.Size = new System.Drawing.Size(955, 379);
            this.textBoxLog.TabIndex = 2;
            this.textBoxLog.Text = "";
            // 
            // comboBoxUART
            // 
            this.comboBoxUART.FormattingEnabled = true;
            this.comboBoxUART.Location = new System.Drawing.Point(108, 6);
            this.comboBoxUART.Name = "comboBoxUART";
            this.comboBoxUART.Size = new System.Drawing.Size(133, 21);
            this.comboBoxUART.TabIndex = 3;
            this.comboBoxUART.SelectedIndexChanged += new System.EventHandler(this.comboBoxUART_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Select UART port:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 36);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(86, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Select chip type:";
            // 
            // comboBoxChipType
            // 
            this.comboBoxChipType.FormattingEnabled = true;
            this.comboBoxChipType.Location = new System.Drawing.Point(108, 33);
            this.comboBoxChipType.Name = "comboBoxChipType";
            this.comboBoxChipType.Size = new System.Drawing.Size(133, 21);
            this.comboBoxChipType.TabIndex = 6;
            this.comboBoxChipType.SelectedIndexChanged += new System.EventHandler(this.comboBoxChipType_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 63);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(82, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Select firmware:";
            // 
            // comboBoxFirmware
            // 
            this.comboBoxFirmware.FormattingEnabled = true;
            this.comboBoxFirmware.Location = new System.Drawing.Point(108, 60);
            this.comboBoxFirmware.Name = "comboBoxFirmware";
            this.comboBoxFirmware.Size = new System.Drawing.Size(261, 21);
            this.comboBoxFirmware.TabIndex = 8;
            this.comboBoxFirmware.SelectedIndexChanged += new System.EventHandler(this.comboBoxFirmware_SelectedIndexChanged);
            this.comboBoxFirmware.Click += new System.EventHandler(this.comboBoxFirmware_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.main);
            this.tabControl1.Controls.Add(this.tabPagePageTool);
            this.tabControl1.Controls.Add(this.extract);
            this.tabControl1.Controls.Add(this.getCFG);
            this.tabControl1.Controls.Add(this.LANscanner);
            this.tabControl1.Controls.Add(this.UARTsettings);
            this.tabControl1.Controls.Add(this.OTAtools);
            this.tabControl1.Controls.Add(this.Docs);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(969, 643);
            this.tabControl1.TabIndex = 9;
            // 
            // main
            // 
            this.main.Controls.Add(this.chkIgnoreCRCErr);
            this.main.Controls.Add(this.buttonCustomOperation);
            this.main.Controls.Add(this.checkBoxSkipKeyCheck);
            this.main.Controls.Add(this.checkBoxOverwriteBootloader);
            this.main.Controls.Add(this.buttonReadOBKConfig);
            this.main.Controls.Add(this.buttonWriteOBKConfig);
            this.main.Controls.Add(this.checkBoxReadOBKConfig);
            this.main.Controls.Add(this.checkBoxAutoReadTuya);
            this.main.Controls.Add(this.buttonChangeOBKSettings);
            this.main.Controls.Add(this.checkBoxAutoOBKConfig);
            this.main.Controls.Add(this.checkBoxAllowBackup);
            this.main.Controls.Add(this.buttonRestoreRF);
            this.main.Controls.Add(this.buttonEraseAll);
            this.main.Controls.Add(this.buttonClearOldFirmware);
            this.main.Controls.Add(this.buttonOpenBackupsDir);
            this.main.Controls.Add(this.labelState);
            this.main.Controls.Add(this.buttonWriteOnly);
            this.main.Controls.Add(this.buttonClearLog);
            this.main.Controls.Add(this.buttonStop);
            this.main.Controls.Add(this.checkBoxShowAdvanced);
            this.main.Controls.Add(this.buttonDoBackupAndFlashNew);
            this.main.Controls.Add(this.buttonTestWrite);
            this.main.Controls.Add(this.buttonTestReadWrite);
            this.main.Controls.Add(this.buttonDownloadLatest);
            this.main.Controls.Add(this.labelMatchingFirmwares);
            this.main.Controls.Add(this.comboBoxBaudRate);
            this.main.Controls.Add(this.label5);
            this.main.Controls.Add(this.progressBar1);
            this.main.Controls.Add(this.buttonRead);
            this.main.Controls.Add(this.comboBoxFirmware);
            this.main.Controls.Add(this.textBoxLog);
            this.main.Controls.Add(this.label3);
            this.main.Controls.Add(this.comboBoxUART);
            this.main.Controls.Add(this.comboBoxChipType);
            this.main.Controls.Add(this.label1);
            this.main.Controls.Add(this.label2);
            this.main.Location = new System.Drawing.Point(4, 22);
            this.main.Name = "main";
            this.main.Padding = new System.Windows.Forms.Padding(3);
            this.main.Size = new System.Drawing.Size(961, 617);
            this.main.TabIndex = 0;
            this.main.Text = "Flasher";
            this.main.UseVisualStyleBackColor = true;
            // 
            // chkIgnoreCRCErr
            // 
            this.chkIgnoreCRCErr.AutoSize = true;
            this.chkIgnoreCRCErr.Location = new System.Drawing.Point(710, 43);
            this.chkIgnoreCRCErr.Name = "chkIgnoreCRCErr";
            this.chkIgnoreCRCErr.Size = new System.Drawing.Size(106, 17);
            this.chkIgnoreCRCErr.TabIndex = 37;
            this.chkIgnoreCRCErr.Text = "Ignore CRC Error";
            this.chkIgnoreCRCErr.UseVisualStyleBackColor = true;
            this.chkIgnoreCRCErr.CheckedChanged += new System.EventHandler(this.chkIgnoreCRCErr_CheckedChanged);
            // 
            // buttonCustomOperation
            // 
            this.buttonCustomOperation.Location = new System.Drawing.Point(710, 66);
            this.buttonCustomOperation.Name = "buttonCustomOperation";
            this.buttonCustomOperation.Size = new System.Drawing.Size(127, 23);
            this.buttonCustomOperation.TabIndex = 36;
            this.buttonCustomOperation.Text = "Custom operation";
            this.buttonCustomOperation.UseVisualStyleBackColor = true;
            this.buttonCustomOperation.Click += new System.EventHandler(this.buttonCustomOperation_Click);
            // 
            // checkBoxSkipKeyCheck
            // 
            this.checkBoxSkipKeyCheck.AutoSize = true;
            this.checkBoxSkipKeyCheck.Location = new System.Drawing.Point(710, 23);
            this.checkBoxSkipKeyCheck.Name = "checkBoxSkipKeyCheck";
            this.checkBoxSkipKeyCheck.Size = new System.Drawing.Size(100, 17);
            this.checkBoxSkipKeyCheck.TabIndex = 35;
            this.checkBoxSkipKeyCheck.Text = "Skip key check";
            this.checkBoxSkipKeyCheck.UseVisualStyleBackColor = true;
            this.checkBoxSkipKeyCheck.CheckedChanged += new System.EventHandler(this.checkBoxSkipKeyCheck_CheckedChanged);
            // 
            // checkBoxOverwriteBootloader
            // 
            this.checkBoxOverwriteBootloader.AutoSize = true;
            this.checkBoxOverwriteBootloader.Location = new System.Drawing.Point(710, 3);
            this.checkBoxOverwriteBootloader.Name = "checkBoxOverwriteBootloader";
            this.checkBoxOverwriteBootloader.Size = new System.Drawing.Size(227, 17);
            this.checkBoxOverwriteBootloader.TabIndex = 34;
            this.checkBoxOverwriteBootloader.Text = "Overwrite bootloader (for N/M, don\'t use it)";
            this.checkBoxOverwriteBootloader.UseVisualStyleBackColor = true;
            this.checkBoxOverwriteBootloader.CheckedChanged += new System.EventHandler(this.checkBoxOverwriteBootloader_CheckedChanged);
            // 
            // buttonReadOBKConfig
            // 
            this.buttonReadOBKConfig.Location = new System.Drawing.Point(708, 120);
            this.buttonReadOBKConfig.Name = "buttonReadOBKConfig";
            this.buttonReadOBKConfig.Size = new System.Drawing.Size(244, 23);
            this.buttonReadOBKConfig.TabIndex = 33;
            this.buttonReadOBKConfig.Text = "Read only OBK config";
            this.buttonReadOBKConfig.UseVisualStyleBackColor = true;
            this.buttonReadOBKConfig.Click += new System.EventHandler(this.buttonReadOBKConfig_Click);
            // 
            // buttonWriteOBKConfig
            // 
            this.buttonWriteOBKConfig.Location = new System.Drawing.Point(708, 149);
            this.buttonWriteOBKConfig.Name = "buttonWriteOBKConfig";
            this.buttonWriteOBKConfig.Size = new System.Drawing.Size(244, 23);
            this.buttonWriteOBKConfig.TabIndex = 32;
            this.buttonWriteOBKConfig.Text = "Write only OBK config";
            this.buttonWriteOBKConfig.UseVisualStyleBackColor = true;
            this.buttonWriteOBKConfig.Click += new System.EventHandler(this.buttonWriteOBKConfig_Click);
            // 
            // checkBoxReadOBKConfig
            // 
            this.checkBoxReadOBKConfig.AutoSize = true;
            this.checkBoxReadOBKConfig.Checked = true;
            this.checkBoxReadOBKConfig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxReadOBKConfig.Location = new System.Drawing.Point(239, 183);
            this.checkBoxReadOBKConfig.Name = "checkBoxReadOBKConfig";
            this.checkBoxReadOBKConfig.Size = new System.Drawing.Size(159, 17);
            this.checkBoxReadOBKConfig.TabIndex = 31;
            this.checkBoxReadOBKConfig.Text = "Read OBK Config if possible";
            this.checkBoxReadOBKConfig.UseVisualStyleBackColor = true;
            // 
            // checkBoxAutoReadTuya
            // 
            this.checkBoxAutoReadTuya.AutoSize = true;
            this.checkBoxAutoReadTuya.Checked = true;
            this.checkBoxAutoReadTuya.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxAutoReadTuya.Location = new System.Drawing.Point(11, 182);
            this.checkBoxAutoReadTuya.Name = "checkBoxAutoReadTuya";
            this.checkBoxAutoReadTuya.Size = new System.Drawing.Size(222, 17);
            this.checkBoxAutoReadTuya.TabIndex = 30;
            this.checkBoxAutoReadTuya.Text = "Automatically extract Tuya Config on read";
            this.checkBoxAutoReadTuya.UseVisualStyleBackColor = true;
            // 
            // buttonChangeOBKSettings
            // 
            this.buttonChangeOBKSettings.Location = new System.Drawing.Point(709, 178);
            this.buttonChangeOBKSettings.Name = "buttonChangeOBKSettings";
            this.buttonChangeOBKSettings.Size = new System.Drawing.Size(244, 23);
            this.buttonChangeOBKSettings.TabIndex = 29;
            this.buttonChangeOBKSettings.Text = "Change OBK settings for flash write";
            this.buttonChangeOBKSettings.UseVisualStyleBackColor = true;
            this.buttonChangeOBKSettings.Click += new System.EventHandler(this.buttonChangeOBKSettings_Click);
            // 
            // checkBoxAutoOBKConfig
            // 
            this.checkBoxAutoOBKConfig.AutoSize = true;
            this.checkBoxAutoOBKConfig.Location = new System.Drawing.Point(404, 182);
            this.checkBoxAutoOBKConfig.Name = "checkBoxAutoOBKConfig";
            this.checkBoxAutoOBKConfig.Size = new System.Drawing.Size(225, 17);
            this.checkBoxAutoOBKConfig.TabIndex = 28;
            this.checkBoxAutoOBKConfig.Text = "Automatically configure OBK on flash write";
            this.checkBoxAutoOBKConfig.UseVisualStyleBackColor = true;
            // 
            // checkBoxAllowBackup
            // 
            this.checkBoxAllowBackup.AutoSize = true;
            this.checkBoxAllowBackup.Location = new System.Drawing.Point(545, 23);
            this.checkBoxAllowBackup.Name = "checkBoxAllowBackup";
            this.checkBoxAllowBackup.Size = new System.Drawing.Size(125, 17);
            this.checkBoxAllowBackup.TabIndex = 27;
            this.checkBoxAllowBackup.Text = "Allow backup restore";
            this.checkBoxAllowBackup.UseVisualStyleBackColor = true;
            this.checkBoxAllowBackup.CheckedChanged += new System.EventHandler(this.checkBoxAllowBackup_CheckedChanged);
            // 
            // buttonRestoreRF
            // 
            this.buttonRestoreRF.Location = new System.Drawing.Point(843, 37);
            this.buttonRestoreRF.Name = "buttonRestoreRF";
            this.buttonRestoreRF.Size = new System.Drawing.Size(110, 23);
            this.buttonRestoreRF.TabIndex = 26;
            this.buttonRestoreRF.Text = "Restore RF part";
            this.buttonRestoreRF.UseVisualStyleBackColor = true;
            this.buttonRestoreRF.Click += new System.EventHandler(this.buttonRestoreRF_Click);
            // 
            // buttonEraseAll
            // 
            this.buttonEraseAll.Location = new System.Drawing.Point(709, 95);
            this.buttonEraseAll.Name = "buttonEraseAll";
            this.buttonEraseAll.Size = new System.Drawing.Size(127, 23);
            this.buttonEraseAll.TabIndex = 25;
            this.buttonEraseAll.Text = "Erase all";
            this.buttonEraseAll.UseVisualStyleBackColor = true;
            this.buttonEraseAll.Click += new System.EventHandler(this.buttonEraseAll_Click);
            // 
            // buttonClearOldFirmware
            // 
            this.buttonClearOldFirmware.Location = new System.Drawing.Point(533, 39);
            this.buttonClearOldFirmware.Name = "buttonClearOldFirmware";
            this.buttonClearOldFirmware.Size = new System.Drawing.Size(168, 23);
            this.buttonClearOldFirmware.TabIndex = 24;
            this.buttonClearOldFirmware.Text = "Clear old firmwares";
            this.buttonClearOldFirmware.UseVisualStyleBackColor = true;
            this.buttonClearOldFirmware.Click += new System.EventHandler(this.buttonClearOldFirmware_Click);
            // 
            // buttonOpenBackupsDir
            // 
            this.buttonOpenBackupsDir.Location = new System.Drawing.Point(533, 94);
            this.buttonOpenBackupsDir.Name = "buttonOpenBackupsDir";
            this.buttonOpenBackupsDir.Size = new System.Drawing.Size(168, 23);
            this.buttonOpenBackupsDir.TabIndex = 23;
            this.buttonOpenBackupsDir.Text = "Open backups dir";
            this.buttonOpenBackupsDir.UseVisualStyleBackColor = true;
            this.buttonOpenBackupsDir.Click += new System.EventHandler(this.buttonOpenBackupsDir_Click);
            // 
            // labelState
            // 
            this.labelState.AutoSize = true;
            this.labelState.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F);
            this.labelState.Location = new System.Drawing.Point(247, 88);
            this.labelState.Name = "labelState";
            this.labelState.Size = new System.Drawing.Size(174, 29);
            this.labelState.TabIndex = 22;
            this.labelState.Text = "Doing nothing..";
            // 
            // buttonWriteOnly
            // 
            this.buttonWriteOnly.Location = new System.Drawing.Point(359, 122);
            this.buttonWriteOnly.Name = "buttonWriteOnly";
            this.buttonWriteOnly.Size = new System.Drawing.Size(168, 50);
            this.buttonWriteOnly.TabIndex = 20;
            this.buttonWriteOnly.Text = "Firmware write (no backup!)";
            this.buttonWriteOnly.UseVisualStyleBackColor = true;
            this.buttonWriteOnly.Click += new System.EventHandler(this.buttonWriteOnly_Click);
            // 
            // buttonClearLog
            // 
            this.buttonClearLog.Location = new System.Drawing.Point(835, 591);
            this.buttonClearLog.Name = "buttonClearLog";
            this.buttonClearLog.Size = new System.Drawing.Size(118, 23);
            this.buttonClearLog.TabIndex = 19;
            this.buttonClearLog.Text = "Clear log";
            this.buttonClearLog.UseVisualStyleBackColor = true;
            this.buttonClearLog.Click += new System.EventHandler(this.buttonClearLog_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonStop.ForeColor = System.Drawing.Color.Red;
            this.buttonStop.Location = new System.Drawing.Point(533, 123);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(168, 49);
            this.buttonStop.TabIndex = 18;
            this.buttonStop.Text = "Stop current operation";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // checkBoxShowAdvanced
            // 
            this.checkBoxShowAdvanced.AutoSize = true;
            this.checkBoxShowAdvanced.Location = new System.Drawing.Point(545, 3);
            this.checkBoxShowAdvanced.Name = "checkBoxShowAdvanced";
            this.checkBoxShowAdvanced.Size = new System.Drawing.Size(141, 17);
            this.checkBoxShowAdvanced.TabIndex = 17;
            this.checkBoxShowAdvanced.Text = "Show advanced options";
            this.checkBoxShowAdvanced.UseVisualStyleBackColor = true;
            this.checkBoxShowAdvanced.CheckedChanged += new System.EventHandler(this.checkBoxShowAdvanced_CheckedChanged);
            // 
            // buttonDoBackupAndFlashNew
            // 
            this.buttonDoBackupAndFlashNew.Location = new System.Drawing.Point(11, 120);
            this.buttonDoBackupAndFlashNew.Name = "buttonDoBackupAndFlashNew";
            this.buttonDoBackupAndFlashNew.Size = new System.Drawing.Size(168, 50);
            this.buttonDoBackupAndFlashNew.TabIndex = 16;
            this.buttonDoBackupAndFlashNew.Text = "Backup and flash new firmware";
            this.buttonDoBackupAndFlashNew.UseVisualStyleBackColor = true;
            this.buttonDoBackupAndFlashNew.Click += new System.EventHandler(this.button4_Click);
            // 
            // buttonTestWrite
            // 
            this.buttonTestWrite.Location = new System.Drawing.Point(842, 66);
            this.buttonTestWrite.Name = "buttonTestWrite";
            this.buttonTestWrite.Size = new System.Drawing.Size(110, 23);
            this.buttonTestWrite.TabIndex = 15;
            this.buttonTestWrite.Text = "Test write pattern";
            this.buttonTestWrite.UseVisualStyleBackColor = true;
            this.buttonTestWrite.Click += new System.EventHandler(this.buttonTestWrite_Click);
            // 
            // buttonTestReadWrite
            // 
            this.buttonTestReadWrite.Location = new System.Drawing.Point(843, 95);
            this.buttonTestReadWrite.Name = "buttonTestReadWrite";
            this.buttonTestReadWrite.Size = new System.Drawing.Size(109, 23);
            this.buttonTestReadWrite.TabIndex = 14;
            this.buttonTestReadWrite.Text = "Test read/write pattern";
            this.buttonTestReadWrite.UseVisualStyleBackColor = true;
            this.buttonTestReadWrite.Click += new System.EventHandler(this.buttonTestReadWrite_Click);
            // 
            // buttonDownloadLatest
            // 
            this.buttonDownloadLatest.Location = new System.Drawing.Point(533, 66);
            this.buttonDownloadLatest.Name = "buttonDownloadLatest";
            this.buttonDownloadLatest.Size = new System.Drawing.Size(168, 23);
            this.buttonDownloadLatest.TabIndex = 13;
            this.buttonDownloadLatest.Text = "Download latest firmware";
            this.buttonDownloadLatest.UseVisualStyleBackColor = true;
            this.buttonDownloadLatest.Click += new System.EventHandler(this.buttonDownloadLatest_Click);
            // 
            // labelMatchingFirmwares
            // 
            this.labelMatchingFirmwares.AutoSize = true;
            this.labelMatchingFirmwares.Location = new System.Drawing.Point(375, 63);
            this.labelMatchingFirmwares.Name = "labelMatchingFirmwares";
            this.labelMatchingFirmwares.Size = new System.Drawing.Size(121, 13);
            this.labelMatchingFirmwares.TabIndex = 12;
            this.labelMatchingFirmwares.Text = "X matching bins, Y total.";
            // 
            // comboBoxBaudRate
            // 
            this.comboBoxBaudRate.FormattingEnabled = true;
            this.comboBoxBaudRate.Location = new System.Drawing.Point(108, 88);
            this.comboBoxBaudRate.Name = "comboBoxBaudRate";
            this.comboBoxBaudRate.Size = new System.Drawing.Size(133, 21);
            this.comboBoxBaudRate.TabIndex = 11;
            this.comboBoxBaudRate.SelectedIndexChanged += new System.EventHandler(this.comboBoxBaudRate_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(8, 91);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(74, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Set baud rate:";
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(8, 591);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(821, 23);
            this.progressBar1.TabIndex = 9;
            // 
            // tabPagePageTool
            // 
            this.tabPagePageTool.Controls.Add(this.textBox2);
            this.tabPagePageTool.Controls.Add(this.button3);
            this.tabPagePageTool.Controls.Add(this.label4);
            this.tabPagePageTool.Controls.Add(this.textBox1);
            this.tabPagePageTool.Controls.Add(this.button2);
            this.tabPagePageTool.Controls.Add(this.button1);
            this.tabPagePageTool.Location = new System.Drawing.Point(4, 22);
            this.tabPagePageTool.Name = "tabPagePageTool";
            this.tabPagePageTool.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePageTool.Size = new System.Drawing.Size(961, 617);
            this.tabPagePageTool.TabIndex = 1;
            this.tabPagePageTool.Text = "Page Tool";
            this.tabPagePageTool.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(9, 99);
            this.textBox2.Multiline = true;
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(592, 319);
            this.textBox2.TabIndex = 5;
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(277, 23);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 4;
            this.button3.Text = "Read";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(90, 28);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(75, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Page address:";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(171, 25);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 20);
            this.textBox1.TabIndex = 2;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(394, 22);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 1;
            this.button2.Text = "Next Page";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(9, 23);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Prev Page";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // extract
            // 
            this.extract.AllowDrop = true;
            this.extract.Controls.Add(this.lblKeyInfo);
            this.extract.Controls.Add(this.txtKey);
            this.extract.Controls.Add(this.chkChangeKey);
            this.extract.Controls.Add(this.buttonImportConfigFileDialog);
            this.extract.Controls.Add(this.linkLabel5);
            this.extract.Controls.Add(this.label32);
            this.extract.Controls.Add(this.buttonTuyaConfig_CopyTextToClipBoard);
            this.extract.Controls.Add(this.buttonTuyaConfig_CopyJSONToClipBoard);
            this.extract.Controls.Add(this.label18);
            this.extract.Controls.Add(this.textBoxTuyaCFGText);
            this.extract.Controls.Add(this.label17);
            this.extract.Controls.Add(this.linkLabel4);
            this.extract.Controls.Add(this.textBoxTuyaCFGJSON);
            this.extract.Controls.Add(this.label16);
            this.extract.Location = new System.Drawing.Point(4, 22);
            this.extract.Name = "extract";
            this.extract.Size = new System.Drawing.Size(961, 617);
            this.extract.TabIndex = 4;
            this.extract.Text = "Extract Config from Tuya binary";
            this.extract.UseVisualStyleBackColor = true;
            this.extract.DragDrop += new System.Windows.Forms.DragEventHandler(this.tabPage2_DragDrop);
            this.extract.DragEnter += new System.Windows.Forms.DragEventHandler(this.tabPage2_DragEnter);
            // 
            // lblKeyInfo
            // 
            this.lblKeyInfo.AutoSize = true;
            this.lblKeyInfo.Location = new System.Drawing.Point(419, 48);
            this.lblKeyInfo.Name = "lblKeyInfo";
            this.lblKeyInfo.Size = new System.Drawing.Size(328, 13);
            this.lblKeyInfo.TabIndex = 20;
            this.lblKeyInfo.Text = "Default Tuya key is 8710_2M, but on RTL8720D devices it\'s 8721D";
            this.lblKeyInfo.Visible = false;
            // 
            // txtKey
            // 
            this.txtKey.Location = new System.Drawing.Point(323, 45);
            this.txtKey.Name = "txtKey";
            this.txtKey.Size = new System.Drawing.Size(90, 20);
            this.txtKey.TabIndex = 19;
            this.txtKey.Text = "8710_2M";
            this.txtKey.Visible = false;
            this.txtKey.TextChanged += new System.EventHandler(this.txtKey_TextChanged);
            // 
            // chkChangeKey
            // 
            this.chkChangeKey.AutoSize = true;
            this.chkChangeKey.Location = new System.Drawing.Point(234, 47);
            this.chkChangeKey.Name = "chkChangeKey";
            this.chkChangeKey.Size = new System.Drawing.Size(83, 17);
            this.chkChangeKey.TabIndex = 18;
            this.chkChangeKey.Text = "Change key";
            this.chkChangeKey.UseVisualStyleBackColor = true;
            this.chkChangeKey.CheckedChanged += new System.EventHandler(this.chkChangeKey_CheckedChanged);
            // 
            // buttonImportConfigFileDialog
            // 
            this.buttonImportConfigFileDialog.Location = new System.Drawing.Point(18, 43);
            this.buttonImportConfigFileDialog.Name = "buttonImportConfigFileDialog";
            this.buttonImportConfigFileDialog.Size = new System.Drawing.Size(185, 23);
            this.buttonImportConfigFileDialog.TabIndex = 15;
            this.buttonImportConfigFileDialog.Text = "Open config with open file dialog instead...";
            this.buttonImportConfigFileDialog.UseVisualStyleBackColor = true;
            this.buttonImportConfigFileDialog.Click += new System.EventHandler(this.buttonImportConfigFileDialog_Click);
            // 
            // linkLabel5
            // 
            this.linkLabel5.AutoSize = true;
            this.linkLabel5.Location = new System.Drawing.Point(415, 73);
            this.linkLabel5.Name = "linkLabel5";
            this.linkLabel5.Size = new System.Drawing.Size(160, 13);
            this.linkLabel5.TabIndex = 14;
            this.linkLabel5.TabStop = true;
            this.linkLabel5.Text = "TuyaConfig from OBK YT tutorial";
            this.linkLabel5.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel5_LinkClicked);
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(8, 73);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(414, 13);
            this.label32.TabIndex = 13;
            this.label32.Text = "You can also get binary from OBK device, only Tuya-config section, 72KB, see tuto" +
    "rial:";
            // 
            // buttonTuyaConfig_CopyTextToClipBoard
            // 
            this.buttonTuyaConfig_CopyTextToClipBoard.Location = new System.Drawing.Point(570, 89);
            this.buttonTuyaConfig_CopyTextToClipBoard.Name = "buttonTuyaConfig_CopyTextToClipBoard";
            this.buttonTuyaConfig_CopyTextToClipBoard.Size = new System.Drawing.Size(114, 21);
            this.buttonTuyaConfig_CopyTextToClipBoard.TabIndex = 12;
            this.buttonTuyaConfig_CopyTextToClipBoard.Text = "Copy to Clipboard";
            this.buttonTuyaConfig_CopyTextToClipBoard.UseVisualStyleBackColor = true;
            this.buttonTuyaConfig_CopyTextToClipBoard.Click += new System.EventHandler(this.buttonTuyaConfig_CopyTextToClipBoard_Click);
            // 
            // buttonTuyaConfig_CopyJSONToClipBoard
            // 
            this.buttonTuyaConfig_CopyJSONToClipBoard.Location = new System.Drawing.Point(234, 89);
            this.buttonTuyaConfig_CopyJSONToClipBoard.Name = "buttonTuyaConfig_CopyJSONToClipBoard";
            this.buttonTuyaConfig_CopyJSONToClipBoard.Size = new System.Drawing.Size(114, 21);
            this.buttonTuyaConfig_CopyJSONToClipBoard.TabIndex = 11;
            this.buttonTuyaConfig_CopyJSONToClipBoard.Text = "Copy to Clipboard";
            this.buttonTuyaConfig_CopyJSONToClipBoard.UseVisualStyleBackColor = true;
            this.buttonTuyaConfig_CopyJSONToClipBoard.Click += new System.EventHandler(this.buttonTuyaConfig_CopyJSONToClipBoard_Click);
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(354, 93);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(82, 13);
            this.label18.TabIndex = 10;
            this.label18.Text = "Text description";
            // 
            // textBoxTuyaCFGText
            // 
            this.textBoxTuyaCFGText.Location = new System.Drawing.Point(354, 113);
            this.textBoxTuyaCFGText.Multiline = true;
            this.textBoxTuyaCFGText.Name = "textBoxTuyaCFGText";
            this.textBoxTuyaCFGText.Size = new System.Drawing.Size(330, 363);
            this.textBoxTuyaCFGText.TabIndex = 9;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(15, 97);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(67, 13);
            this.label17.TabIndex = 8;
            this.label17.Text = "JSON format";
            // 
            // linkLabel4
            // 
            this.linkLabel4.AutoSize = true;
            this.linkLabel4.Location = new System.Drawing.Point(351, 17);
            this.linkLabel4.Name = "linkLabel4";
            this.linkLabel4.Size = new System.Drawing.Size(125, 13);
            this.linkLabel4.TabIndex = 7;
            this.linkLabel4.TabStop = true;
            this.linkLabel4.Text = "OBK Template Converter";
            this.linkLabel4.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel4_LinkClicked);
            // 
            // textBoxTuyaCFGJSON
            // 
            this.textBoxTuyaCFGJSON.Location = new System.Drawing.Point(18, 113);
            this.textBoxTuyaCFGJSON.Multiline = true;
            this.textBoxTuyaCFGJSON.Name = "textBoxTuyaCFGJSON";
            this.textBoxTuyaCFGJSON.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxTuyaCFGJSON.Size = new System.Drawing.Size(330, 363);
            this.textBoxTuyaCFGJSON.TabIndex = 1;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(9, 4);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(761, 26);
            this.label16.TabIndex = 0;
            this.label16.Text = resources.GetString("label16.Text");
            // 
            // getCFG
            // 
            this.getCFG.Controls.Add(this.buttonIPSaveResultToFile);
            this.getCFG.Controls.Add(this.progressBarIPOperation);
            this.getCFG.Controls.Add(this.labelIPOperationStatus);
            this.getCFG.Controls.Add(this.comboBoxIP);
            this.getCFG.Controls.Add(this.buttonIPDownloadTuyaConfig);
            this.getCFG.Controls.Add(this.label20);
            this.getCFG.Controls.Add(this.buttonIPCFGDump);
            this.getCFG.Controls.Add(this.buttonIPDump2MB);
            this.getCFG.Controls.Add(this.labelCheckCommunicationStatus);
            this.getCFG.Controls.Add(this.buttonCheckCommunication);
            this.getCFG.Controls.Add(this.label19);
            this.getCFG.Location = new System.Drawing.Point(4, 22);
            this.getCFG.Name = "getCFG";
            this.getCFG.Padding = new System.Windows.Forms.Padding(3);
            this.getCFG.Size = new System.Drawing.Size(961, 617);
            this.getCFG.TabIndex = 5;
            this.getCFG.Text = "Get CFG from OBK";
            this.getCFG.UseVisualStyleBackColor = true;
            // 
            // buttonIPSaveResultToFile
            // 
            this.buttonIPSaveResultToFile.Location = new System.Drawing.Point(722, 176);
            this.buttonIPSaveResultToFile.Name = "buttonIPSaveResultToFile";
            this.buttonIPSaveResultToFile.Size = new System.Drawing.Size(162, 23);
            this.buttonIPSaveResultToFile.TabIndex = 11;
            this.buttonIPSaveResultToFile.Text = "Save result to file...";
            this.buttonIPSaveResultToFile.UseVisualStyleBackColor = true;
            this.buttonIPSaveResultToFile.Click += new System.EventHandler(this.buttonIPSaveResultToFile_Click);
            // 
            // progressBarIPOperation
            // 
            this.progressBarIPOperation.Location = new System.Drawing.Point(404, 146);
            this.progressBarIPOperation.Name = "progressBarIPOperation";
            this.progressBarIPOperation.Size = new System.Drawing.Size(480, 23);
            this.progressBarIPOperation.TabIndex = 10;
            // 
            // labelIPOperationStatus
            // 
            this.labelIPOperationStatus.AutoSize = true;
            this.labelIPOperationStatus.Location = new System.Drawing.Point(401, 130);
            this.labelIPOperationStatus.Name = "labelIPOperationStatus";
            this.labelIPOperationStatus.Size = new System.Drawing.Size(154, 13);
            this.labelIPOperationStatus.TabIndex = 9;
            this.labelIPOperationStatus.Text = "Current operation progress: 0/0";
            // 
            // comboBoxIP
            // 
            this.comboBoxIP.FormattingEnabled = true;
            this.comboBoxIP.Location = new System.Drawing.Point(35, 48);
            this.comboBoxIP.Name = "comboBoxIP";
            this.comboBoxIP.Size = new System.Drawing.Size(177, 21);
            this.comboBoxIP.TabIndex = 8;
            // 
            // buttonIPDownloadTuyaConfig
            // 
            this.buttonIPDownloadTuyaConfig.Location = new System.Drawing.Point(401, 77);
            this.buttonIPDownloadTuyaConfig.Name = "buttonIPDownloadTuyaConfig";
            this.buttonIPDownloadTuyaConfig.Size = new System.Drawing.Size(483, 23);
            this.buttonIPDownloadTuyaConfig.TabIndex = 7;
            this.buttonIPDownloadTuyaConfig.Text = "Download Tuya config (Tuya GPIO) from 0x1EE000 offset, 72kilobytes";
            this.buttonIPDownloadTuyaConfig.UseVisualStyleBackColor = true;
            this.buttonIPDownloadTuyaConfig.Click += new System.EventHandler(this.buttonIPDownloadTuyaConfig_Click);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(9, 7);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(748, 26);
            this.label20.TabIndex = 6;
            this.label20.Text = resources.GetString("label20.Text");
            // 
            // buttonIPCFGDump
            // 
            this.buttonIPCFGDump.Location = new System.Drawing.Point(599, 48);
            this.buttonIPCFGDump.Name = "buttonIPCFGDump";
            this.buttonIPCFGDump.Size = new System.Drawing.Size(285, 23);
            this.buttonIPCFGDump.TabIndex = 5;
            this.buttonIPCFGDump.Text = "Download OBK config partition (4096 bytes) from target";
            this.buttonIPCFGDump.UseVisualStyleBackColor = true;
            this.buttonIPCFGDump.Click += new System.EventHandler(this.buttonIPCFGDump_Click);
            // 
            // buttonIPDump2MB
            // 
            this.buttonIPDump2MB.Location = new System.Drawing.Point(401, 48);
            this.buttonIPDump2MB.Name = "buttonIPDump2MB";
            this.buttonIPDump2MB.Size = new System.Drawing.Size(192, 23);
            this.buttonIPDump2MB.TabIndex = 4;
            this.buttonIPDump2MB.Text = "Download 2MB dump from target";
            this.buttonIPDump2MB.UseVisualStyleBackColor = true;
            this.buttonIPDump2MB.Click += new System.EventHandler(this.buttonIPDump2MB_Click);
            // 
            // labelCheckCommunicationStatus
            // 
            this.labelCheckCommunicationStatus.AutoSize = true;
            this.labelCheckCommunicationStatus.Location = new System.Drawing.Point(22, 78);
            this.labelCheckCommunicationStatus.Name = "labelCheckCommunicationStatus";
            this.labelCheckCommunicationStatus.Size = new System.Drawing.Size(41, 13);
            this.labelCheckCommunicationStatus.TabIndex = 3;
            this.labelCheckCommunicationStatus.Text = "label20";
            this.labelCheckCommunicationStatus.Visible = false;
            // 
            // buttonCheckCommunication
            // 
            this.buttonCheckCommunication.Location = new System.Drawing.Point(218, 48);
            this.buttonCheckCommunication.Name = "buttonCheckCommunication";
            this.buttonCheckCommunication.Size = new System.Drawing.Size(145, 23);
            this.buttonCheckCommunication.TabIndex = 2;
            this.buttonCheckCommunication.Text = "Check communication";
            this.buttonCheckCommunication.UseVisualStyleBackColor = true;
            this.buttonCheckCommunication.Click += new System.EventHandler(this.buttonCheckCommunication_Click);
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(9, 51);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(20, 13);
            this.label19.TabIndex = 1;
            this.label19.Text = "IP:";
            // 
            // LANscanner
            // 
            this.LANscanner.Controls.Add(this.button4);
            this.LANscanner.Controls.Add(this.buttonIPScannerOpenDir);
            this.LANscanner.Controls.Add(this.textBoxIPScannerPass);
            this.LANscanner.Controls.Add(this.label31);
            this.LANscanner.Controls.Add(this.textBoxIPScannerUser);
            this.LANscanner.Controls.Add(this.label30);
            this.LANscanner.Controls.Add(this.textBoxBoxScannerRetries);
            this.LANscanner.Controls.Add(this.label28);
            this.LANscanner.Controls.Add(this.label29);
            this.LANscanner.Controls.Add(this.labelMassBackupProgress);
            this.LANscanner.Controls.Add(this.buttonStartMassBackup);
            this.LANscanner.Controls.Add(this.labelScanState);
            this.LANscanner.Controls.Add(this.label24);
            this.LANscanner.Controls.Add(this.listView1);
            this.LANscanner.Controls.Add(this.buttonStartScan);
            this.LANscanner.Controls.Add(this.textBoxScannerThreads);
            this.LANscanner.Controls.Add(this.label23);
            this.LANscanner.Controls.Add(this.textBoxEndIP);
            this.LANscanner.Controls.Add(this.label22);
            this.LANscanner.Controls.Add(this.textBoxStartIP);
            this.LANscanner.Controls.Add(this.label21);
            this.LANscanner.Location = new System.Drawing.Point(4, 22);
            this.LANscanner.Name = "LANscanner";
            this.LANscanner.Padding = new System.Windows.Forms.Padding(3);
            this.LANscanner.Size = new System.Drawing.Size(961, 617);
            this.LANscanner.TabIndex = 6;
            this.LANscanner.Text = "LAN Scanner";
            this.LANscanner.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(827, 554);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(118, 23);
            this.button4.TabIndex = 21;
            this.button4.Text = "Clear log";
            this.button4.UseVisualStyleBackColor = true;
            // 
            // buttonIPScannerOpenDir
            // 
            this.buttonIPScannerOpenDir.Location = new System.Drawing.Point(13, 584);
            this.buttonIPScannerOpenDir.Name = "buttonIPScannerOpenDir";
            this.buttonIPScannerOpenDir.Size = new System.Drawing.Size(156, 23);
            this.buttonIPScannerOpenDir.TabIndex = 20;
            this.buttonIPScannerOpenDir.Text = "Open backups dir";
            this.buttonIPScannerOpenDir.UseVisualStyleBackColor = true;
            this.buttonIPScannerOpenDir.Click += new System.EventHandler(this.buttonIPScannerOpenDir_Click);
            // 
            // textBoxIPScannerPass
            // 
            this.textBoxIPScannerPass.Location = new System.Drawing.Point(296, 70);
            this.textBoxIPScannerPass.Name = "textBoxIPScannerPass";
            this.textBoxIPScannerPass.Size = new System.Drawing.Size(151, 20);
            this.textBoxIPScannerPass.TabIndex = 19;
            this.textBoxIPScannerPass.TextChanged += new System.EventHandler(this.textBoxIPScannerPass_TextChanged);
            // 
            // label31
            // 
            this.label31.AutoSize = true;
            this.label31.Location = new System.Drawing.Point(234, 73);
            this.label31.Name = "label31";
            this.label31.Size = new System.Drawing.Size(56, 13);
            this.label31.TabIndex = 18;
            this.label31.Text = "Password:";
            // 
            // textBoxIPScannerUser
            // 
            this.textBoxIPScannerUser.Location = new System.Drawing.Point(77, 70);
            this.textBoxIPScannerUser.Name = "textBoxIPScannerUser";
            this.textBoxIPScannerUser.Size = new System.Drawing.Size(151, 20);
            this.textBoxIPScannerUser.TabIndex = 17;
            this.textBoxIPScannerUser.TextChanged += new System.EventHandler(this.textBoxIPScannerUser_TextChanged);
            // 
            // label30
            // 
            this.label30.AutoSize = true;
            this.label30.Location = new System.Drawing.Point(11, 73);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(60, 13);
            this.label30.TabIndex = 16;
            this.label30.Text = "UserName:";
            // 
            // textBoxBoxScannerRetries
            // 
            this.textBoxBoxScannerRetries.Location = new System.Drawing.Point(513, 77);
            this.textBoxBoxScannerRetries.Name = "textBoxBoxScannerRetries";
            this.textBoxBoxScannerRetries.Size = new System.Drawing.Size(44, 20);
            this.textBoxBoxScannerRetries.TabIndex = 15;
            this.textBoxBoxScannerRetries.Text = "5";
            // 
            // label28
            // 
            this.label28.AutoSize = true;
            this.label28.Location = new System.Drawing.Point(458, 77);
            this.label28.Name = "label28";
            this.label28.Size = new System.Drawing.Size(39, 13);
            this.label28.TabIndex = 14;
            this.label28.Text = "Loops:";
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(11, 538);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(353, 13);
            this.label29.TabIndex = 13;
            this.label29.Text = "Here you can automatically download CFG backup for all devices from list";
            // 
            // labelMassBackupProgress
            // 
            this.labelMassBackupProgress.AutoSize = true;
            this.labelMassBackupProgress.Location = new System.Drawing.Point(175, 559);
            this.labelMassBackupProgress.Name = "labelMassBackupProgress";
            this.labelMassBackupProgress.Size = new System.Drawing.Size(76, 13);
            this.labelMassBackupProgress.TabIndex = 11;
            this.labelMassBackupProgress.Text = "Doing nothing.";
            // 
            // buttonStartMassBackup
            // 
            this.buttonStartMassBackup.Location = new System.Drawing.Point(13, 554);
            this.buttonStartMassBackup.Name = "buttonStartMassBackup";
            this.buttonStartMassBackup.Size = new System.Drawing.Size(156, 23);
            this.buttonStartMassBackup.TabIndex = 10;
            this.buttonStartMassBackup.Text = "Start mass CFG backup";
            this.buttonStartMassBackup.UseVisualStyleBackColor = true;
            this.buttonStartMassBackup.Click += new System.EventHandler(this.buttonStartMassBackup_Click);
            // 
            // labelScanState
            // 
            this.labelScanState.AutoSize = true;
            this.labelScanState.Location = new System.Drawing.Point(12, 101);
            this.labelScanState.Name = "labelScanState";
            this.labelScanState.Size = new System.Drawing.Size(61, 13);
            this.labelScanState.TabIndex = 9;
            this.labelScanState.Text = "Scan state:";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(8, 12);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(747, 13);
            this.label24.TabIndex = 8;
            this.label24.Text = "Here you can scan your LAN for OpenBeken and Tasmota devices. Scanner will first " +
    "do a quick, unprecise loop and then some slower, more precise checks.\r\n";
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnID,
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5});
            this.listView1.FullRowSelect = true;
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(11, 117);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(942, 418);
            this.listView1.TabIndex = 7;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // columnID
            // 
            this.columnID.Text = "Idx";
            this.columnID.Width = 36;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "IP";
            this.columnHeader1.Width = 100;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Short Name";
            this.columnHeader2.Width = 120;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Chipset";
            this.columnHeader3.Width = 78;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "MAC";
            this.columnHeader4.Width = 111;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Build";
            this.columnHeader5.Width = 122;
            // 
            // buttonStartScan
            // 
            this.buttonStartScan.Location = new System.Drawing.Point(577, 45);
            this.buttonStartScan.Name = "buttonStartScan";
            this.buttonStartScan.Size = new System.Drawing.Size(139, 52);
            this.buttonStartScan.TabIndex = 6;
            this.buttonStartScan.Text = "Start";
            this.buttonStartScan.UseVisualStyleBackColor = true;
            this.buttonStartScan.Click += new System.EventHandler(this.buttonStartScan_Click);
            // 
            // textBoxScannerThreads
            // 
            this.textBoxScannerThreads.Location = new System.Drawing.Point(513, 42);
            this.textBoxScannerThreads.Name = "textBoxScannerThreads";
            this.textBoxScannerThreads.Size = new System.Drawing.Size(44, 20);
            this.textBoxScannerThreads.TabIndex = 5;
            this.textBoxScannerThreads.Text = "16";
            this.textBoxScannerThreads.TextChanged += new System.EventHandler(this.textBoxScannerThreads_TextChanged);
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(458, 45);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(49, 13);
            this.label23.TabIndex = 4;
            this.label23.Text = "Threads:";
            // 
            // textBoxEndIP
            // 
            this.textBoxEndIP.Location = new System.Drawing.Point(296, 42);
            this.textBoxEndIP.Name = "textBoxEndIP";
            this.textBoxEndIP.Size = new System.Drawing.Size(151, 20);
            this.textBoxEndIP.TabIndex = 3;
            this.textBoxEndIP.Text = "192.168.0.255";
            this.textBoxEndIP.TextChanged += new System.EventHandler(this.textBoxEndIP_TextChanged);
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(234, 45);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(42, 13);
            this.label22.TabIndex = 2;
            this.label22.Text = "End IP:";
            // 
            // textBoxStartIP
            // 
            this.textBoxStartIP.Location = new System.Drawing.Point(77, 42);
            this.textBoxStartIP.Name = "textBoxStartIP";
            this.textBoxStartIP.Size = new System.Drawing.Size(151, 20);
            this.textBoxStartIP.TabIndex = 1;
            this.textBoxStartIP.Text = "192.168.0.1";
            this.textBoxStartIP.TextChanged += new System.EventHandler(this.textBoxStartIP_TextChanged);
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(12, 45);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(45, 13);
            this.label21.TabIndex = 0;
            this.label21.Text = "Start IP:";
            // 
            // UARTsettings
            // 
            this.UARTsettings.Controls.Add(this.label27);
            this.UARTsettings.Controls.Add(this.textBox_cfg_readReplyStyle);
            this.UARTsettings.Controls.Add(this.textBox_cfg_readTimeOutMultForLoop);
            this.UARTsettings.Controls.Add(this.label26);
            this.UARTsettings.Controls.Add(this.textBox_cfg_readTimeOutMultForSerialClass);
            this.UARTsettings.Controls.Add(this.label25);
            this.UARTsettings.Location = new System.Drawing.Point(4, 22);
            this.UARTsettings.Name = "UARTsettings";
            this.UARTsettings.Padding = new System.Windows.Forms.Padding(3);
            this.UARTsettings.Size = new System.Drawing.Size(961, 617);
            this.UARTsettings.TabIndex = 7;
            this.UARTsettings.Text = "UART timeouts";
            this.UARTsettings.UseVisualStyleBackColor = true;
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(11, 123);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(99, 13);
            this.label27.TabIndex = 5;
            this.label27.Text = "cfg_readReplyStyle";
            // 
            // textBox_cfg_readReplyStyle
            // 
            this.textBox_cfg_readReplyStyle.Location = new System.Drawing.Point(187, 117);
            this.textBox_cfg_readReplyStyle.Name = "textBox_cfg_readReplyStyle";
            this.textBox_cfg_readReplyStyle.Size = new System.Drawing.Size(243, 20);
            this.textBox_cfg_readReplyStyle.TabIndex = 4;
            this.textBox_cfg_readReplyStyle.Text = "5";
            this.textBox_cfg_readReplyStyle.TextChanged += new System.EventHandler(this.textBox_cfg_readReplyStyle_TextChanged);
            // 
            // textBox_cfg_readTimeOutMultForLoop
            // 
            this.textBox_cfg_readTimeOutMultForLoop.Location = new System.Drawing.Point(187, 90);
            this.textBox_cfg_readTimeOutMultForLoop.Name = "textBox_cfg_readTimeOutMultForLoop";
            this.textBox_cfg_readTimeOutMultForLoop.Size = new System.Drawing.Size(243, 20);
            this.textBox_cfg_readTimeOutMultForLoop.TabIndex = 3;
            this.textBox_cfg_readTimeOutMultForLoop.Text = "5";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(8, 93);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(148, 13);
            this.label26.TabIndex = 2;
            this.label26.Text = "cfg_readTimeOutMultForLoop";
            // 
            // textBox_cfg_readTimeOutMultForSerialClass
            // 
            this.textBox_cfg_readTimeOutMultForSerialClass.Location = new System.Drawing.Point(187, 66);
            this.textBox_cfg_readTimeOutMultForSerialClass.Name = "textBox_cfg_readTimeOutMultForSerialClass";
            this.textBox_cfg_readTimeOutMultForSerialClass.Size = new System.Drawing.Size(243, 20);
            this.textBox_cfg_readTimeOutMultForSerialClass.TabIndex = 1;
            this.textBox_cfg_readTimeOutMultForSerialClass.Text = "5";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(6, 69);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(175, 13);
            this.label25.TabIndex = 0;
            this.label25.Text = "cfg_readTimeOutMultForSerialClass";
            // 
            // OTAtools
            // 
            this.OTAtools.Controls.Add(this.panel1);
            this.OTAtools.Controls.Add(this.buttonOTAFlash);
            this.OTAtools.Controls.Add(this.textBoxOTATarget);
            this.OTAtools.Location = new System.Drawing.Point(4, 22);
            this.OTAtools.Name = "OTAtools";
            this.OTAtools.Padding = new System.Windows.Forms.Padding(3);
            this.OTAtools.Size = new System.Drawing.Size(961, 617);
            this.OTAtools.TabIndex = 8;
            this.OTAtools.Text = "OTA Tool";
            this.OTAtools.UseVisualStyleBackColor = true;
            this.OTAtools.Enter += new System.EventHandler(this.tabPage8_Enter);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label34);
            this.panel1.Controls.Add(this.comboBox1);
            this.panel1.Controls.Add(this.listBoxOTA);
            this.panel1.Controls.Add(this.radioButton3);
            this.panel1.Controls.Add(this.radioButton2);
            this.panel1.Controls.Add(this.label33);
            this.panel1.Controls.Add(this.radioButton1);
            this.panel1.Location = new System.Drawing.Point(8, 31);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(235, 445);
            this.panel1.TabIndex = 3;
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(4, 35);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(48, 13);
            this.label34.TabIndex = 8;
            this.label34.Text = "Platform:";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(58, 32);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(151, 21);
            this.comboBox1.TabIndex = 7;
            // 
            // listBoxOTA
            // 
            this.listBoxOTA.FormattingEnabled = true;
            this.listBoxOTA.Location = new System.Drawing.Point(7, 78);
            this.listBoxOTA.Name = "listBoxOTA";
            this.listBoxOTA.Size = new System.Drawing.Size(202, 355);
            this.listBoxOTA.TabIndex = 6;
            // 
            // radioButton3
            // 
            this.radioButton3.AutoSize = true;
            this.radioButton3.Location = new System.Drawing.Point(124, 54);
            this.radioButton3.Name = "radioButton3";
            this.radioButton3.Size = new System.Drawing.Size(85, 17);
            this.radioButton3.TabIndex = 5;
            this.radioButton3.TabStop = true;
            this.radioButton3.Text = "Pull Request";
            this.radioButton3.UseVisualStyleBackColor = true;
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(77, 54);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(41, 17);
            this.radioButton2.TabIndex = 4;
            this.radioButton2.TabStop = true;
            this.radioButton2.Text = "File";
            this.radioButton2.UseVisualStyleBackColor = true;
            this.radioButton2.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // label33
            // 
            this.label33.AutoSize = true;
            this.label33.Location = new System.Drawing.Point(4, 4);
            this.label33.Name = "label33";
            this.label33.Size = new System.Drawing.Size(103, 13);
            this.label33.TabIndex = 3;
            this.label33.Text = "Choose OTA source";
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Checked = true;
            this.radioButton1.Location = new System.Drawing.Point(7, 54);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(64, 17);
            this.radioButton1.TabIndex = 2;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "Release";
            this.radioButton1.UseVisualStyleBackColor = true;
            // 
            // buttonOTAFlash
            // 
            this.buttonOTAFlash.Location = new System.Drawing.Point(433, 51);
            this.buttonOTAFlash.Name = "buttonOTAFlash";
            this.buttonOTAFlash.Size = new System.Drawing.Size(97, 23);
            this.buttonOTAFlash.TabIndex = 1;
            this.buttonOTAFlash.Text = "Flash!";
            this.buttonOTAFlash.UseVisualStyleBackColor = true;
            this.buttonOTAFlash.Click += new System.EventHandler(this.buttonOTAFlash_Click);
            // 
            // textBoxOTATarget
            // 
            this.textBoxOTATarget.Location = new System.Drawing.Point(275, 51);
            this.textBoxOTATarget.Name = "textBoxOTATarget";
            this.textBoxOTATarget.Size = new System.Drawing.Size(152, 20);
            this.textBoxOTATarget.TabIndex = 0;
            this.textBoxOTATarget.Text = "192.168.0.165";
            // 
            // Docs
            // 
            this.Docs.Controls.Add(this.label35);
            this.Docs.Controls.Add(this.linkLabel6);
            this.Docs.Controls.Add(this.label36);
            this.Docs.Controls.Add(this.linkLabel1);
            this.Docs.Controls.Add(this.label12);
            this.Docs.Controls.Add(this.linkLabelSPIFlasher);
            this.Docs.Controls.Add(this.label11);
            this.Docs.Controls.Add(this.linkLabelForumDevicesSectio);
            this.Docs.Controls.Add(this.linkLabelDevicesDB);
            this.Docs.Controls.Add(this.linkLabelForum);
            this.Docs.Controls.Add(this.label10);
            this.Docs.Controls.Add(this.label9);
            this.Docs.Controls.Add(this.label8);
            this.Docs.Controls.Add(this.label7);
            this.Docs.Controls.Add(this.linkLabelOpenBeken);
            this.Docs.Location = new System.Drawing.Point(4, 22);
            this.Docs.Name = "Docs";
            this.Docs.Size = new System.Drawing.Size(961, 617);
            this.Docs.TabIndex = 2;
            this.Docs.Text = "Documentation/Tutorials";
            this.Docs.UseVisualStyleBackColor = true;
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(9, 180);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(274, 13);
            this.label35.TabIndex = 16;
            this.label35.Text = "We will guide you step by step with device configuration!";
            // 
            // linkLabel6
            // 
            this.linkLabel6.AutoSize = true;
            this.linkLabel6.Location = new System.Drawing.Point(230, 154);
            this.linkLabel6.Name = "linkLabel6";
            this.linkLabel6.Size = new System.Drawing.Size(318, 13);
            this.linkLabel6.TabIndex = 15;
            this.linkLabel6.TabStop = true;
            this.linkLabel6.Text = "https://www.elektroda.com/rtvforum/forum390.html?tylko_dzial=1";
            this.linkLabel6.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label36
            // 
            this.label36.AutoSize = true;
            this.label36.Location = new System.Drawing.Point(9, 154);
            this.label36.Name = "label36";
            this.label36.Size = new System.Drawing.Size(187, 13);
            this.label36.TabIndex = 14;
            this.label36.Text = "Forum section for quick IoT questions:";
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(231, 130);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(246, 13);
            this.linkLabel1.TabIndex = 11;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "https://github.com/openshwprojects/obkSimulator";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(9, 130);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(143, 13);
            this.label12.TabIndex = 10;
            this.label12.Text = "OpenBK Windows Simulator:";
            // 
            // linkLabelSPIFlasher
            // 
            this.linkLabelSPIFlasher.AutoSize = true;
            this.linkLabelSPIFlasher.Location = new System.Drawing.Point(231, 106);
            this.linkLabelSPIFlasher.Name = "linkLabelSPIFlasher";
            this.linkLabelSPIFlasher.Size = new System.Drawing.Size(286, 13);
            this.linkLabelSPIFlasher.TabIndex = 9;
            this.linkLabelSPIFlasher.TabStop = true;
            this.linkLabelSPIFlasher.Text = "https://github.com/openshwprojects/BK7231_SPI_Flasher";
            this.linkLabelSPIFlasher.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(9, 106);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(201, 13);
            this.label11.TabIndex = 8;
            this.label11.Text = "Our SPI BK7231 flasher (recovery mode):";
            // 
            // linkLabelForumDevicesSectio
            // 
            this.linkLabelForumDevicesSectio.AutoSize = true;
            this.linkLabelForumDevicesSectio.Location = new System.Drawing.Point(231, 82);
            this.linkLabelForumDevicesSectio.Name = "linkLabelForumDevicesSectio";
            this.linkLabelForumDevicesSectio.Size = new System.Drawing.Size(251, 13);
            this.linkLabelForumDevicesSectio.TabIndex = 7;
            this.linkLabelForumDevicesSectio.TabStop = true;
            this.linkLabelForumDevicesSectio.Text = "https://www.elektroda.com/rtvforum/forum507.html";
            this.linkLabelForumDevicesSectio.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // linkLabelDevicesDB
            // 
            this.linkLabelDevicesDB.AutoSize = true;
            this.linkLabelDevicesDB.Location = new System.Drawing.Point(230, 57);
            this.linkLabelDevicesDB.Name = "linkLabelDevicesDB";
            this.linkLabelDevicesDB.Size = new System.Drawing.Size(274, 13);
            this.linkLabelDevicesDB.TabIndex = 6;
            this.linkLabelDevicesDB.TabStop = true;
            this.linkLabelDevicesDB.Text = "https://openbekeniot.github.io/webapp/devicesList.html";
            this.linkLabelDevicesDB.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // linkLabelForum
            // 
            this.linkLabelForum.AutoSize = true;
            this.linkLabelForum.Location = new System.Drawing.Point(230, 34);
            this.linkLabelForum.Name = "linkLabelForum";
            this.linkLabelForum.Size = new System.Drawing.Size(142, 13);
            this.linkLabelForum.TabIndex = 5;
            this.linkLabelForum.TabStop = true;
            this.linkLabelForum.Text = "https://www.elektroda.com/";
            this.linkLabelForum.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(9, 82);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(216, 13);
            this.label10.TabIndex = 4;
            this.label10.Text = "Devices forum (submit new teardowns here):";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(9, 57);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(150, 13);
            this.label9.TabIndex = 3;
            this.label9.Text = "Devices Templates/database:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(9, 34);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(144, 13);
            this.label8.TabIndex = 2;
            this.label8.Text = "Our forum (ask here for help):";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(9, 12);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(151, 13);
            this.label7.TabIndex = 1;
            this.label7.Text = "Our repository (for developers):";
            // 
            // linkLabelOpenBeken
            // 
            this.linkLabelOpenBeken.AutoSize = true;
            this.linkLabelOpenBeken.Location = new System.Drawing.Point(230, 12);
            this.linkLabelOpenBeken.Name = "linkLabelOpenBeken";
            this.linkLabelOpenBeken.Size = new System.Drawing.Size(281, 13);
            this.linkLabelOpenBeken.TabIndex = 0;
            this.linkLabelOpenBeken.TabStop = true;
            this.linkLabelOpenBeken.Text = "https://github.com/openshwprojects/OpenBK7231T_App";
            this.linkLabelOpenBeken.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // timer100ms
            // 
            this.timer100ms.Enabled = true;
            this.timer100ms.Tick += new System.EventHandler(this.timer100ms_Tick);
            // 
            // FormMain
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(969, 643);
            this.Controls.Add(this.tabControl1);
            this.Name = "FormMain";
            this.Text = "BK7231 Easy UART Flasher - Automatically download firmware and flash BK7231T/BK72" +
    "31N  - Elektroda.com";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.main.ResumeLayout(false);
            this.main.PerformLayout();
            this.tabPagePageTool.ResumeLayout(false);
            this.tabPagePageTool.PerformLayout();
            this.extract.ResumeLayout(false);
            this.extract.PerformLayout();
            this.getCFG.ResumeLayout(false);
            this.getCFG.PerformLayout();
            this.LANscanner.ResumeLayout(false);
            this.LANscanner.PerformLayout();
            this.UARTsettings.ResumeLayout(false);
            this.UARTsettings.PerformLayout();
            this.OTAtools.ResumeLayout(false);
            this.OTAtools.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.Docs.ResumeLayout(false);
            this.Docs.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button buttonRead;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.ComboBox comboBoxUART;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBoxChipType;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboBoxFirmware;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage main;
        private System.Windows.Forms.TabPage tabPagePageTool;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.ComboBox comboBoxBaudRate;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button buttonDownloadLatest;
        private System.Windows.Forms.Label labelMatchingFirmwares;
        private System.Windows.Forms.Button buttonTestReadWrite;
        private System.Windows.Forms.Button buttonTestWrite;
        private System.Windows.Forms.Button buttonDoBackupAndFlashNew;
        private System.Windows.Forms.CheckBox checkBoxShowAdvanced;
        private System.Windows.Forms.Timer timer100ms;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Button buttonClearLog;
        private System.Windows.Forms.Button buttonWriteOnly;
        private System.Windows.Forms.Label labelState;
        private System.Windows.Forms.TabPage Docs;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.LinkLabel linkLabelOpenBeken;
        private System.Windows.Forms.LinkLabel linkLabelForumDevicesSectio;
        private System.Windows.Forms.LinkLabel linkLabelDevicesDB;
        private System.Windows.Forms.LinkLabel linkLabelForum;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.LinkLabel linkLabelSPIFlasher;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button buttonOpenBackupsDir;
        private System.Windows.Forms.Button buttonClearOldFirmware;
        private System.Windows.Forms.Button buttonEraseAll;
        private System.Windows.Forms.Button buttonRestoreRF;
        private System.Windows.Forms.CheckBox checkBoxAllowBackup;
        private System.Windows.Forms.TabPage extract;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox textBoxTuyaCFGJSON;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.LinkLabel linkLabel4;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox textBoxTuyaCFGText;
        private System.Windows.Forms.Button buttonChangeOBKSettings;
        private System.Windows.Forms.CheckBox checkBoxAutoOBKConfig;
        private System.Windows.Forms.CheckBox checkBoxAutoReadTuya;
        private System.Windows.Forms.CheckBox checkBoxReadOBKConfig;
        private System.Windows.Forms.Button buttonWriteOBKConfig;
        private System.Windows.Forms.Button buttonReadOBKConfig;
        private System.Windows.Forms.TabPage getCFG;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Button buttonCheckCommunication;
        private System.Windows.Forms.Label labelCheckCommunicationStatus;
        private System.Windows.Forms.Button buttonIPDump2MB;
        private System.Windows.Forms.Button buttonIPCFGDump;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.TabPage LANscanner;
        private System.Windows.Forms.TextBox textBoxStartIP;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TextBox textBoxEndIP;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Button buttonStartScan;
        private System.Windows.Forms.TextBox textBoxScannerThreads;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnID;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label labelScanState;
        private System.Windows.Forms.Button buttonIPDownloadTuyaConfig;
        private System.Windows.Forms.ComboBox comboBoxIP;
        private System.Windows.Forms.ProgressBar progressBarIPOperation;
        private System.Windows.Forms.Label labelIPOperationStatus;
        private System.Windows.Forms.Button buttonIPSaveResultToFile;
        private System.Windows.Forms.TabPage UARTsettings;
        private System.Windows.Forms.TextBox textBox_cfg_readTimeOutMultForSerialClass;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.TextBox textBox_cfg_readTimeOutMultForLoop;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox textBox_cfg_readReplyStyle;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.Label labelMassBackupProgress;
        private System.Windows.Forms.Button buttonStartMassBackup;
        private System.Windows.Forms.TextBox textBoxBoxScannerRetries;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TextBox textBoxIPScannerPass;
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.TextBox textBoxIPScannerUser;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.Button buttonIPScannerOpenDir;
        private System.Windows.Forms.Button buttonTuyaConfig_CopyJSONToClipBoard;
        private System.Windows.Forms.Button buttonTuyaConfig_CopyTextToClipBoard;
        private System.Windows.Forms.LinkLabel linkLabel5;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.Button buttonImportConfigFileDialog;
        private System.Windows.Forms.CheckBox checkBoxOverwriteBootloader;
        private System.Windows.Forms.CheckBox checkBoxSkipKeyCheck;
        private System.Windows.Forms.Button buttonCustomOperation;
        private System.Windows.Forms.TabPage OTAtools;
        private System.Windows.Forms.Button buttonOTAFlash;
        private System.Windows.Forms.TextBox textBoxOTATarget;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.Label label33;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.RadioButton radioButton3;
        private System.Windows.Forms.ListBox listBoxOTA;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.CheckBox chkIgnoreCRCErr;
        private System.Windows.Forms.CheckBox chkChangeKey;
        private System.Windows.Forms.TextBox txtKey;
        private System.Windows.Forms.Label lblKeyInfo;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.LinkLabel linkLabel6;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.Button button4;
    }
}

