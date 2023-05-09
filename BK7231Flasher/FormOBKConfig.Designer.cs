namespace BK7231Flasher
{
    partial class FormOBKConfig
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormOBKConfig));
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxWiFiSSID = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxWiFiPass = new System.Windows.Forms.TextBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxMQTTHost = new System.Windows.Forms.TextBox();
            this.textBoxMQTTPort = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textBoxMQTTTopic = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxMQTTGroup = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textBoxMQTTUser = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.textBoxMQTTPass = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.listViewGPIO = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label11 = new System.Windows.Forms.Label();
            this.textBoxLongName = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.textBoxShortName = new System.Windows.Forms.TextBox();
            this.checkBoxAutoGenerateNames = new System.Windows.Forms.CheckBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.appendSettingsFromOBKConfigToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonRandomizeNames = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.checkBoxFlag = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.label13 = new System.Windows.Forms.Label();
            this.textBoxShortStartupCommand = new System.Windows.Forms.TextBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.label14 = new System.Windows.Forms.Label();
            this.textBoxWebAppRoot = new System.Windows.Forms.TextBox();
            this.menuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(500, 143);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // textBoxWiFiSSID
            // 
            this.textBoxWiFiSSID.Location = new System.Drawing.Point(87, 166);
            this.textBoxWiFiSSID.Name = "textBoxWiFiSSID";
            this.textBoxWiFiSSID.Size = new System.Drawing.Size(387, 20);
            this.textBoxWiFiSSID.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(22, 169);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "WiFi SSID:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(22, 195);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(56, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "WiFi pass:";
            // 
            // textBoxWiFiPass
            // 
            this.textBoxWiFiPass.Location = new System.Drawing.Point(87, 192);
            this.textBoxWiFiPass.Name = "textBoxWiFiPass";
            this.textBoxWiFiPass.Size = new System.Drawing.Size(387, 20);
            this.textBoxWiFiPass.TabIndex = 4;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(507, 15);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(283, 17);
            this.checkBox1.TabIndex = 5;
            this.checkBox1.Text = "Try to import Tuya GPIO settings from firmware backup";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(22, 288);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(64, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "MQTT host:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(314, 288);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(29, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Port:";
            // 
            // textBoxMQTTHost
            // 
            this.textBoxMQTTHost.Location = new System.Drawing.Point(87, 285);
            this.textBoxMQTTHost.Name = "textBoxMQTTHost";
            this.textBoxMQTTHost.Size = new System.Drawing.Size(221, 20);
            this.textBoxMQTTHost.TabIndex = 8;
            // 
            // textBoxMQTTPort
            // 
            this.textBoxMQTTPort.Location = new System.Drawing.Point(349, 285);
            this.textBoxMQTTPort.Name = "textBoxMQTTPort";
            this.textBoxMQTTPort.Size = new System.Drawing.Size(42, 20);
            this.textBoxMQTTPort.TabIndex = 9;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(21, 319);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(62, 13);
            this.label6.TabIndex = 10;
            this.label6.Text = "Client topic:";
            // 
            // textBoxMQTTTopic
            // 
            this.textBoxMQTTTopic.Location = new System.Drawing.Point(87, 316);
            this.textBoxMQTTTopic.Name = "textBoxMQTTTopic";
            this.textBoxMQTTTopic.Size = new System.Drawing.Size(109, 20);
            this.textBoxMQTTTopic.TabIndex = 11;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(202, 319);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(65, 13);
            this.label7.TabIndex = 12;
            this.label7.Text = "Group topic:";
            // 
            // textBoxMQTTGroup
            // 
            this.textBoxMQTTGroup.Location = new System.Drawing.Point(273, 316);
            this.textBoxMQTTGroup.Name = "textBoxMQTTGroup";
            this.textBoxMQTTGroup.Size = new System.Drawing.Size(118, 20);
            this.textBoxMQTTGroup.TabIndex = 13;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(24, 351);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(32, 13);
            this.label8.TabIndex = 14;
            this.label8.Text = "User:";
            // 
            // textBoxMQTTUser
            // 
            this.textBoxMQTTUser.Location = new System.Drawing.Point(87, 348);
            this.textBoxMQTTUser.Name = "textBoxMQTTUser";
            this.textBoxMQTTUser.Size = new System.Drawing.Size(304, 20);
            this.textBoxMQTTUser.TabIndex = 15;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(21, 376);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(56, 13);
            this.label9.TabIndex = 16;
            this.label9.Text = "Password:";
            // 
            // textBoxMQTTPass
            // 
            this.textBoxMQTTPass.Location = new System.Drawing.Point(87, 374);
            this.textBoxMQTTPass.Name = "textBoxMQTTPass";
            this.textBoxMQTTPass.Size = new System.Drawing.Size(304, 20);
            this.textBoxMQTTPass.TabIndex = 17;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(508, 68);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(61, 13);
            this.label10.TabIndex = 23;
            this.label10.Text = "GPIO roles:";
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 50;
            // 
            // listViewGPIO
            // 
            this.listViewGPIO.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4});
            this.listViewGPIO.FullRowSelect = true;
            this.listViewGPIO.HideSelection = false;
            this.listViewGPIO.Location = new System.Drawing.Point(507, 38);
            this.listViewGPIO.Name = "listViewGPIO";
            this.listViewGPIO.Size = new System.Drawing.Size(306, 382);
            this.listViewGPIO.TabIndex = 24;
            this.listViewGPIO.UseCompatibleStateImageBehavior = false;
            this.listViewGPIO.View = System.Windows.Forms.View.Details;
            this.listViewGPIO.MouseClick += new System.Windows.Forms.MouseEventHandler(this.listView1_MouseClick);
            this.listViewGPIO.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewGPIO_MouseDoubleClick);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Pin";
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Role";
            this.columnHeader2.Width = 115;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Channel";
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Channel2";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(24, 225);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(55, 13);
            this.label11.TabIndex = 25;
            this.label11.Text = "Full name:";
            // 
            // textBoxLongName
            // 
            this.textBoxLongName.Location = new System.Drawing.Point(85, 222);
            this.textBoxLongName.Name = "textBoxLongName";
            this.textBoxLongName.Size = new System.Drawing.Size(198, 20);
            this.textBoxLongName.TabIndex = 26;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(289, 225);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(64, 13);
            this.label12.TabIndex = 27;
            this.label12.Text = "Short name:";
            // 
            // textBoxShortName
            // 
            this.textBoxShortName.Location = new System.Drawing.Point(359, 222);
            this.textBoxShortName.Name = "textBoxShortName";
            this.textBoxShortName.Size = new System.Drawing.Size(115, 20);
            this.textBoxShortName.TabIndex = 28;
            // 
            // checkBoxAutoGenerateNames
            // 
            this.checkBoxAutoGenerateNames.AutoSize = true;
            this.checkBoxAutoGenerateNames.Checked = true;
            this.checkBoxAutoGenerateNames.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxAutoGenerateNames.Location = new System.Drawing.Point(27, 257);
            this.checkBoxAutoGenerateNames.Name = "checkBoxAutoGenerateNames";
            this.checkBoxAutoGenerateNames.Size = new System.Drawing.Size(298, 17);
            this.checkBoxAutoGenerateNames.TabIndex = 29;
            this.checkBoxAutoGenerateNames.Text = "On firmware read, regenerate random OBK device names ";
            this.checkBoxAutoGenerateNames.UseVisualStyleBackColor = true;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(829, 24);
            this.menuStrip1.TabIndex = 30;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem,
            this.appendSettingsFromOBKConfigToolStripMenuItem,
            this.clearSettingsToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // appendSettingsFromTuya2MBBackupToolStripMenuItem
            // 
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem.Name = "appendSettingsFromTuya2MBBackupToolStripMenuItem";
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem.Size = new System.Drawing.Size(295, 22);
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem.Text = "Append settings from Tuya 2MB backup...";
            this.appendSettingsFromTuya2MBBackupToolStripMenuItem.Click += new System.EventHandler(this.appendSettingsFromTuya2MBBackupToolStripMenuItem_Click);
            // 
            // appendSettingsFromOBKConfigToolStripMenuItem
            // 
            this.appendSettingsFromOBKConfigToolStripMenuItem.Name = "appendSettingsFromOBKConfigToolStripMenuItem";
            this.appendSettingsFromOBKConfigToolStripMenuItem.Size = new System.Drawing.Size(295, 22);
            this.appendSettingsFromOBKConfigToolStripMenuItem.Text = "Append settings from OBK config...";
            this.appendSettingsFromOBKConfigToolStripMenuItem.Click += new System.EventHandler(this.appendSettingsFromOBKConfigToolStripMenuItem_Click);
            // 
            // clearSettingsToolStripMenuItem
            // 
            this.clearSettingsToolStripMenuItem.Name = "clearSettingsToolStripMenuItem";
            this.clearSettingsToolStripMenuItem.Size = new System.Drawing.Size(295, 22);
            this.clearSettingsToolStripMenuItem.Text = "Clear settings";
            this.clearSettingsToolStripMenuItem.Click += new System.EventHandler(this.clearSettingsToolStripMenuItem_Click);
            // 
            // buttonRandomizeNames
            // 
            this.buttonRandomizeNames.Location = new System.Drawing.Point(331, 253);
            this.buttonRandomizeNames.Name = "buttonRandomizeNames";
            this.buttonRandomizeNames.Size = new System.Drawing.Size(130, 23);
            this.buttonRandomizeNames.TabIndex = 31;
            this.buttonRandomizeNames.Text = "Randomize names now";
            this.buttonRandomizeNames.UseVisualStyleBackColor = true;
            this.buttonRandomizeNames.Click += new System.EventHandler(this.buttonRandomizeNames_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 24);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(829, 463);
            this.tabControl1.TabIndex = 32;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.listViewGPIO);
            this.tabPage1.Controls.Add(this.checkBox1);
            this.tabPage1.Controls.Add(this.buttonRandomizeNames);
            this.tabPage1.Controls.Add(this.textBoxWiFiSSID);
            this.tabPage1.Controls.Add(this.label8);
            this.tabPage1.Controls.Add(this.checkBoxAutoGenerateNames);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.textBoxShortName);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.label12);
            this.tabPage1.Controls.Add(this.textBoxWiFiPass);
            this.tabPage1.Controls.Add(this.textBoxLongName);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.label11);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.textBoxMQTTHost);
            this.tabPage1.Controls.Add(this.textBoxMQTTPort);
            this.tabPage1.Controls.Add(this.textBoxMQTTPass);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.textBoxMQTTTopic);
            this.tabPage1.Controls.Add(this.textBoxMQTTUser);
            this.tabPage1.Controls.Add(this.label7);
            this.tabPage1.Controls.Add(this.textBoxMQTTGroup);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(821, 437);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Generic";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.panel1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(821, 437);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Flags";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // checkBoxFlag
            // 
            this.checkBoxFlag.AutoSize = true;
            this.checkBoxFlag.Location = new System.Drawing.Point(5, 3);
            this.checkBoxFlag.Name = "checkBoxFlag";
            this.checkBoxFlag.Size = new System.Drawing.Size(80, 17);
            this.checkBoxFlag.TabIndex = 0;
            this.checkBoxFlag.Text = "[1] First flag";
            this.checkBoxFlag.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.checkBoxFlag);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(815, 431);
            this.panel1.TabIndex = 1;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.textBoxShortStartupCommand);
            this.tabPage3.Controls.Add(this.label13);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(821, 437);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Short startup command";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(9, 7);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(219, 13);
            this.label13.TabIndex = 0;
            this.label13.Text = "Short startup command is run at OBK startup:";
            // 
            // textBoxShortStartupCommand
            // 
            this.textBoxShortStartupCommand.Location = new System.Drawing.Point(12, 24);
            this.textBoxShortStartupCommand.Multiline = true;
            this.textBoxShortStartupCommand.Name = "textBoxShortStartupCommand";
            this.textBoxShortStartupCommand.Size = new System.Drawing.Size(801, 261);
            this.textBoxShortStartupCommand.TabIndex = 1;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.textBoxWebAppRoot);
            this.tabPage4.Controls.Add(this.label14);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(821, 437);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Misc";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(9, 4);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(76, 13);
            this.label14.TabIndex = 0;
            this.label14.Text = "Web App root:";
            // 
            // textBoxWebAppRoot
            // 
            this.textBoxWebAppRoot.Location = new System.Drawing.Point(12, 22);
            this.textBoxWebAppRoot.Name = "textBoxWebAppRoot";
            this.textBoxWebAppRoot.Size = new System.Drawing.Size(315, 20);
            this.textBoxWebAppRoot.TabIndex = 1;
            // 
            // FormOBKConfig
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(829, 487);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "FormOBKConfig";
            this.Text = "FormOBKConfig";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormOBKConfig_FormClosing);
            this.Load += new System.EventHandler(this.FormOBKConfig_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.FormOBKConfig_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.FormOBKConfig_DragEnter);
            this.Leave += new System.EventHandler(this.FormOBKConfig_Leave);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxWiFiSSID;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxWiFiPass;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxMQTTHost;
        private System.Windows.Forms.TextBox textBoxMQTTPort;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBoxMQTTTopic;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textBoxMQTTGroup;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textBoxMQTTUser;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBoxMQTTPass;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ListView listViewGPIO;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox textBoxLongName;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox textBoxShortName;
        private System.Windows.Forms.CheckBox checkBoxAutoGenerateNames;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem appendSettingsFromTuya2MBBackupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem appendSettingsFromOBKConfigToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearSettingsToolStripMenuItem;
        private System.Windows.Forms.Button buttonRandomizeNames;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.CheckBox checkBoxFlag;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox textBoxShortStartupCommand;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TextBox textBoxWebAppRoot;
        private System.Windows.Forms.Label label14;
    }
}