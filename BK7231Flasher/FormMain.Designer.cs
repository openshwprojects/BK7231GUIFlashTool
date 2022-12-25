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
            this.buttonTestReadWrite = new System.Windows.Forms.Button();
            this.buttonDownloadLatest = new System.Windows.Forms.Button();
            this.labelMatchingFirmwares = new System.Windows.Forms.Label();
            this.comboBoxBaudRate = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button3 = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.buttonTestWrite = new System.Windows.Forms.Button();
            this.buttonDoBackupAndFlashNew = new System.Windows.Forms.Button();
            this.checkBoxShowAdvanced = new System.Windows.Forms.CheckBox();
            this.timer100ms = new System.Windows.Forms.Timer(this.components);
            this.buttonStop = new System.Windows.Forms.Button();
            this.buttonClearLog = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.labelState = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
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
            this.textBoxLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textBoxLog.Location = new System.Drawing.Point(3, 185);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.Size = new System.Drawing.Size(777, 296);
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
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(791, 510);
            this.tabControl1.TabIndex = 9;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.labelState);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.button4);
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
            this.tabPage1.Size = new System.Drawing.Size(783, 484);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "tabPage1";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // buttonTestReadWrite
            // 
            this.buttonTestReadWrite.Location = new System.Drawing.Point(642, 97);
            this.buttonTestReadWrite.Name = "buttonTestReadWrite";
            this.buttonTestReadWrite.Size = new System.Drawing.Size(133, 23);
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
            this.progressBar1.Size = new System.Drawing.Size(767, 23);
            this.progressBar1.TabIndex = 9;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.textBox2);
            this.tabPage2.Controls.Add(this.button3);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.textBox1);
            this.tabPage2.Controls.Add(this.button2);
            this.tabPage2.Controls.Add(this.button1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(609, 424);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
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
            // buttonTestWrite
            // 
            this.buttonTestWrite.Location = new System.Drawing.Point(642, 68);
            this.buttonTestWrite.Name = "buttonTestWrite";
            this.buttonTestWrite.Size = new System.Drawing.Size(133, 23);
            this.buttonTestWrite.TabIndex = 15;
            this.buttonTestWrite.Text = "Test write pattern";
            this.buttonTestWrite.UseVisualStyleBackColor = true;
            this.buttonTestWrite.Click += new System.EventHandler(this.buttonTestWrite_Click);
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
            // checkBoxShowAdvanced
            // 
            this.checkBoxShowAdvanced.AutoSize = true;
            this.checkBoxShowAdvanced.Location = new System.Drawing.Point(634, 8);
            this.checkBoxShowAdvanced.Name = "checkBoxShowAdvanced";
            this.checkBoxShowAdvanced.Size = new System.Drawing.Size(141, 17);
            this.checkBoxShowAdvanced.TabIndex = 17;
            this.checkBoxShowAdvanced.Text = "Show advanced options";
            this.checkBoxShowAdvanced.UseVisualStyleBackColor = true;
            // 
            // timer100ms
            // 
            this.timer100ms.Enabled = true;
            this.timer100ms.Tick += new System.EventHandler(this.timer100ms_Tick);
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
            // buttonClearLog
            // 
            this.buttonClearLog.Location = new System.Drawing.Point(669, 127);
            this.buttonClearLog.Name = "buttonClearLog";
            this.buttonClearLog.Size = new System.Drawing.Size(106, 23);
            this.buttonClearLog.TabIndex = 19;
            this.buttonClearLog.Text = "Clear log";
            this.buttonClearLog.UseVisualStyleBackColor = true;
            this.buttonClearLog.Click += new System.EventHandler(this.buttonClearLog_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(379, 126);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(180, 23);
            this.button4.TabIndex = 20;
            this.button4.Text = "Do firmware write (no backup!)";
            this.button4.UseVisualStyleBackColor = true;
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
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(791, 510);
            this.Controls.Add(this.tabControl1);
            this.Name = "FormMain";
            this.Text = "BK7231 Easy UART Flasher - Automatically download firmware and flash BK7231T/BK72" +
    "31N ";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
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
        private System.Windows.Forms.TabPage tabPage2;
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
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label labelState;
    }
}

