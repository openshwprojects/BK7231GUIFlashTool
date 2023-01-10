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
            this.buttonRead = new System.Windows.Forms.Button();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboBoxUART = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxChipType = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBoxFirmware = new System.Windows.Forms.ComboBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.buttonClearOldFirmware = new System.Windows.Forms.Button();
            this.buttonOpenBackupsDir = new System.Windows.Forms.Button();
            this.labelState = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
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
            this.tabPage3 = new System.Windows.Forms.TabPage();
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
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.label15 = new System.Windows.Forms.Label();
            this.linkLabel3 = new System.Windows.Forms.LinkLabel();
            this.label14 = new System.Windows.Forms.Label();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.label13 = new System.Windows.Forms.Label();
            this.timer100ms = new System.Windows.Forms.Timer(this.components);
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPagePageTool.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonRead
            // 
            this.buttonRead.Location = new System.Drawing.Point(193, 126);
            this.buttonRead.Name = "buttonRead";
            this.buttonRead.Size = new System.Drawing.Size(180, 23);
            this.buttonRead.TabIndex = 1;
            this.buttonRead.Text = "Do firmware backup (read) only";
            this.buttonRead.UseVisualStyleBackColor = true;
            this.buttonRead.Click += new System.EventHandler(this.buttonRead_Click);
            // 
            // textBoxLog
            // 
            this.textBoxLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxLog.Location = new System.Drawing.Point(3, 185);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.Size = new System.Drawing.Size(792, 296);
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
            this.comboBoxFirmware.Size = new System.Drawing.Size(215, 21);
            this.comboBoxFirmware.TabIndex = 8;
            this.comboBoxFirmware.SelectedIndexChanged += new System.EventHandler(this.comboBoxFirmware_SelectedIndexChanged);
            this.comboBoxFirmware.Click += new System.EventHandler(this.comboBoxFirmware_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPagePageTool);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(806, 510);
            this.tabControl1.TabIndex = 9;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.buttonClearOldFirmware);
            this.tabPage1.Controls.Add(this.buttonOpenBackupsDir);
            this.tabPage1.Controls.Add(this.labelState);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.buttonWriteOnly);
            this.tabPage1.Controls.Add(this.buttonClearLog);
            this.tabPage1.Controls.Add(this.buttonStop);
            this.tabPage1.Controls.Add(this.checkBoxShowAdvanced);
            this.tabPage1.Controls.Add(this.buttonDoBackupAndFlashNew);
            this.tabPage1.Controls.Add(this.buttonTestWrite);
            this.tabPage1.Controls.Add(this.buttonTestReadWrite);
            this.tabPage1.Controls.Add(this.buttonDownloadLatest);
            this.tabPage1.Controls.Add(this.labelMatchingFirmwares);
            this.tabPage1.Controls.Add(this.comboBoxBaudRate);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.progressBar1);
            this.tabPage1.Controls.Add(this.buttonRead);
            this.tabPage1.Controls.Add(this.comboBoxFirmware);
            this.tabPage1.Controls.Add(this.textBoxLog);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.comboBoxUART);
            this.tabPage1.Controls.Add(this.comboBoxChipType);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(798, 484);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Flasher";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // buttonClearOldFirmware
            // 
            this.buttonClearOldFirmware.Location = new System.Drawing.Point(607, 58);
            this.buttonClearOldFirmware.Name = "buttonClearOldFirmware";
            this.buttonClearOldFirmware.Size = new System.Drawing.Size(67, 23);
            this.buttonClearOldFirmware.TabIndex = 24;
            this.buttonClearOldFirmware.Text = "Clear old";
            this.buttonClearOldFirmware.UseVisualStyleBackColor = true;
            this.buttonClearOldFirmware.Click += new System.EventHandler(this.buttonClearOldFirmware_Click);
            // 
            // buttonOpenBackupsDir
            // 
            this.buttonOpenBackupsDir.Location = new System.Drawing.Point(508, 97);
            this.buttonOpenBackupsDir.Name = "buttonOpenBackupsDir";
            this.buttonOpenBackupsDir.Size = new System.Drawing.Size(132, 23);
            this.buttonOpenBackupsDir.TabIndex = 23;
            this.buttonOpenBackupsDir.Text = "Open backups dir";
            this.buttonOpenBackupsDir.UseVisualStyleBackColor = true;
            this.buttonOpenBackupsDir.Click += new System.EventHandler(this.buttonOpenBackupsDir_Click);
            // 
            // labelState
            // 
            this.labelState.AutoSize = true;
            this.labelState.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F);
            this.labelState.Location = new System.Drawing.Point(246, 88);
            this.labelState.Name = "labelState";
            this.labelState.Size = new System.Drawing.Size(174, 29);
            this.labelState.TabIndex = 22;
            this.labelState.Text = "Doing nothing..";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(248, 36);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(336, 13);
            this.label6.TabIndex = 21;
            this.label6.Text = "BK7231T is WB3S, WB2S, WB2L, etc. BK7231N is CB2S, CB3S, etc";
            // 
            // buttonWriteOnly
            // 
            this.buttonWriteOnly.Location = new System.Drawing.Point(379, 126);
            this.buttonWriteOnly.Name = "buttonWriteOnly";
            this.buttonWriteOnly.Size = new System.Drawing.Size(180, 23);
            this.buttonWriteOnly.TabIndex = 20;
            this.buttonWriteOnly.Text = "Do firmware write (no backup!)";
            this.buttonWriteOnly.UseVisualStyleBackColor = true;
            this.buttonWriteOnly.Click += new System.EventHandler(this.buttonWriteOnly_Click);
            // 
            // buttonClearLog
            // 
            this.buttonClearLog.Location = new System.Drawing.Point(669, 127);
            this.buttonClearLog.Name = "buttonClearLog";
            this.buttonClearLog.Size = new System.Drawing.Size(121, 23);
            this.buttonClearLog.TabIndex = 19;
            this.buttonClearLog.Text = "Clear log";
            this.buttonClearLog.UseVisualStyleBackColor = true;
            this.buttonClearLog.Click += new System.EventHandler(this.buttonClearLog_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Location = new System.Drawing.Point(565, 127);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(98, 23);
            this.buttonStop.TabIndex = 18;
            this.buttonStop.Text = "Stop current operation";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // checkBoxShowAdvanced
            // 
            this.checkBoxShowAdvanced.AutoSize = true;
            this.checkBoxShowAdvanced.Location = new System.Drawing.Point(634, 8);
            this.checkBoxShowAdvanced.Name = "checkBoxShowAdvanced";
            this.checkBoxShowAdvanced.Size = new System.Drawing.Size(141, 17);
            this.checkBoxShowAdvanced.TabIndex = 17;
            this.checkBoxShowAdvanced.Text = "Show advanced options";
            this.checkBoxShowAdvanced.UseVisualStyleBackColor = true;
            this.checkBoxShowAdvanced.CheckedChanged += new System.EventHandler(this.checkBoxShowAdvanced_CheckedChanged);
            // 
            // buttonDoBackupAndFlashNew
            // 
            this.buttonDoBackupAndFlashNew.Location = new System.Drawing.Point(11, 126);
            this.buttonDoBackupAndFlashNew.Name = "buttonDoBackupAndFlashNew";
            this.buttonDoBackupAndFlashNew.Size = new System.Drawing.Size(176, 23);
            this.buttonDoBackupAndFlashNew.TabIndex = 16;
            this.buttonDoBackupAndFlashNew.Text = "Do backup and flash new firmware";
            this.buttonDoBackupAndFlashNew.UseVisualStyleBackColor = true;
            this.buttonDoBackupAndFlashNew.Click += new System.EventHandler(this.button4_Click);
            // 
            // buttonTestWrite
            // 
            this.buttonTestWrite.Location = new System.Drawing.Point(690, 68);
            this.buttonTestWrite.Name = "buttonTestWrite";
            this.buttonTestWrite.Size = new System.Drawing.Size(100, 23);
            this.buttonTestWrite.TabIndex = 15;
            this.buttonTestWrite.Text = "Test write pattern";
            this.buttonTestWrite.UseVisualStyleBackColor = true;
            this.buttonTestWrite.Click += new System.EventHandler(this.buttonTestWrite_Click);
            // 
            // buttonTestReadWrite
            // 
            this.buttonTestReadWrite.Location = new System.Drawing.Point(669, 98);
            this.buttonTestReadWrite.Name = "buttonTestReadWrite";
            this.buttonTestReadWrite.Size = new System.Drawing.Size(121, 23);
            this.buttonTestReadWrite.TabIndex = 14;
            this.buttonTestReadWrite.Text = "Test read/write pattern";
            this.buttonTestReadWrite.UseVisualStyleBackColor = true;
            this.buttonTestReadWrite.Click += new System.EventHandler(this.buttonTestReadWrite_Click);
            // 
            // buttonDownloadLatest
            // 
            this.buttonDownloadLatest.Location = new System.Drawing.Point(456, 58);
            this.buttonDownloadLatest.Name = "buttonDownloadLatest";
            this.buttonDownloadLatest.Size = new System.Drawing.Size(145, 23);
            this.buttonDownloadLatest.TabIndex = 13;
            this.buttonDownloadLatest.Text = "Download latest from Web";
            this.buttonDownloadLatest.UseVisualStyleBackColor = true;
            this.buttonDownloadLatest.Click += new System.EventHandler(this.buttonDownloadLatest_Click);
            // 
            // labelMatchingFirmwares
            // 
            this.labelMatchingFirmwares.AutoSize = true;
            this.labelMatchingFirmwares.Location = new System.Drawing.Point(329, 63);
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
            this.progressBar1.Location = new System.Drawing.Point(8, 156);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(782, 23);
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
            this.tabPagePageTool.Size = new System.Drawing.Size(798, 484);
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
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.linkLabel1);
            this.tabPage3.Controls.Add(this.label12);
            this.tabPage3.Controls.Add(this.linkLabelSPIFlasher);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Controls.Add(this.linkLabelForumDevicesSectio);
            this.tabPage3.Controls.Add(this.linkLabelDevicesDB);
            this.tabPage3.Controls.Add(this.linkLabelForum);
            this.tabPage3.Controls.Add(this.label10);
            this.tabPage3.Controls.Add(this.label9);
            this.tabPage3.Controls.Add(this.label8);
            this.tabPage3.Controls.Add(this.label7);
            this.tabPage3.Controls.Add(this.linkLabelOpenBeken);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(798, 484);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Documentation/Tutorials";
            this.tabPage3.UseVisualStyleBackColor = true;
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
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.label15);
            this.tabPage4.Controls.Add(this.linkLabel3);
            this.tabPage4.Controls.Add(this.label14);
            this.tabPage4.Controls.Add(this.linkLabel2);
            this.tabPage4.Controls.Add(this.label13);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(798, 484);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Help contact";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(20, 67);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(274, 13);
            this.label15.TabIndex = 9;
            this.label15.Text = "We will guide you step by step with device configuration!";
            // 
            // linkLabel3
            // 
            this.linkLabel3.AutoSize = true;
            this.linkLabel3.Location = new System.Drawing.Point(282, 40);
            this.linkLabel3.Name = "linkLabel3";
            this.linkLabel3.Size = new System.Drawing.Size(318, 13);
            this.linkLabel3.TabIndex = 8;
            this.linkLabel3.TabStop = true;
            this.linkLabel3.Text = "https://www.elektroda.com/rtvforum/forum390.html?tylko_dzial=1";
            this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(20, 40);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(256, 13);
            this.label14.TabIndex = 7;
            this.label14.Text = "The preferred section for quick IoT questions is here:";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Location = new System.Drawing.Point(198, 14);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(142, 13);
            this.linkLabel2.TabIndex = 6;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "https://www.elektroda.com/";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.genericLinkClicked);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(20, 14);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(172, 13);
            this.label13.TabIndex = 0;
            this.label13.Text = "Please ask questions on our forum:";
            // 
            // timer100ms
            // 
            this.timer100ms.Enabled = true;
            this.timer100ms.Tick += new System.EventHandler(this.timer100ms_Tick);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(806, 510);
            this.Controls.Add(this.tabControl1);
            this.Name = "FormMain";
            this.Text = "BK7231 Easy UART Flasher - Automatically download firmware and flash BK7231T/BK72" +
    "31N ";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPagePageTool.ResumeLayout(false);
            this.tabPagePageTool.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
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
        private System.Windows.Forms.TabPage tabPage1;
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
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label labelState;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
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
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.LinkLabel linkLabel3;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Button buttonOpenBackupsDir;
        private System.Windows.Forms.Button buttonClearOldFirmware;
    }
}

