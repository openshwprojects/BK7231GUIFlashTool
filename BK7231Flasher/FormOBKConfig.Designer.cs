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
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(500, 143);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // textBoxWiFiSSID
            // 
            this.textBoxWiFiSSID.Location = new System.Drawing.Point(79, 175);
            this.textBoxWiFiSSID.Name = "textBoxWiFiSSID";
            this.textBoxWiFiSSID.Size = new System.Drawing.Size(304, 20);
            this.textBoxWiFiSSID.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 178);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "WiFi SSID:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 204);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(56, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "WiFi pass:";
            // 
            // textBoxWiFiPass
            // 
            this.textBoxWiFiPass.Location = new System.Drawing.Point(79, 201);
            this.textBoxWiFiPass.Name = "textBoxWiFiPass";
            this.textBoxWiFiPass.Size = new System.Drawing.Size(304, 20);
            this.textBoxWiFiPass.TabIndex = 4;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(511, 12);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(283, 17);
            this.checkBox1.TabIndex = 5;
            this.checkBox1.Text = "Try to import Tuya GPIO settings from firmware backup";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 254);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(64, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "MQTT host:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(306, 254);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(29, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Port:";
            // 
            // textBoxMQTTHost
            // 
            this.textBoxMQTTHost.Location = new System.Drawing.Point(79, 251);
            this.textBoxMQTTHost.Name = "textBoxMQTTHost";
            this.textBoxMQTTHost.Size = new System.Drawing.Size(221, 20);
            this.textBoxMQTTHost.TabIndex = 8;
            // 
            // textBoxMQTTPort
            // 
            this.textBoxMQTTPort.Location = new System.Drawing.Point(341, 251);
            this.textBoxMQTTPort.Name = "textBoxMQTTPort";
            this.textBoxMQTTPort.Size = new System.Drawing.Size(42, 20);
            this.textBoxMQTTPort.TabIndex = 9;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 285);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(62, 13);
            this.label6.TabIndex = 10;
            this.label6.Text = "Client topic:";
            // 
            // textBoxMQTTTopic
            // 
            this.textBoxMQTTTopic.Location = new System.Drawing.Point(79, 282);
            this.textBoxMQTTTopic.Name = "textBoxMQTTTopic";
            this.textBoxMQTTTopic.Size = new System.Drawing.Size(109, 20);
            this.textBoxMQTTTopic.TabIndex = 11;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(194, 285);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(65, 13);
            this.label7.TabIndex = 12;
            this.label7.Text = "Group topic:";
            // 
            // textBoxMQTTGroup
            // 
            this.textBoxMQTTGroup.Location = new System.Drawing.Point(265, 282);
            this.textBoxMQTTGroup.Name = "textBoxMQTTGroup";
            this.textBoxMQTTGroup.Size = new System.Drawing.Size(118, 20);
            this.textBoxMQTTGroup.TabIndex = 13;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(14, 317);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(32, 13);
            this.label8.TabIndex = 14;
            this.label8.Text = "User:";
            // 
            // textBoxMQTTUser
            // 
            this.textBoxMQTTUser.Location = new System.Drawing.Point(79, 314);
            this.textBoxMQTTUser.Name = "textBoxMQTTUser";
            this.textBoxMQTTUser.Size = new System.Drawing.Size(304, 20);
            this.textBoxMQTTUser.TabIndex = 15;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(13, 342);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(56, 13);
            this.label9.TabIndex = 16;
            this.label9.Text = "Password:";
            // 
            // textBoxMQTTPass
            // 
            this.textBoxMQTTPass.Location = new System.Drawing.Point(79, 340);
            this.textBoxMQTTPass.Name = "textBoxMQTTPass";
            this.textBoxMQTTPass.Size = new System.Drawing.Size(304, 20);
            this.textBoxMQTTPass.TabIndex = 17;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(508, 47);
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
            this.listViewGPIO.Location = new System.Drawing.Point(511, 63);
            this.listViewGPIO.Name = "listViewGPIO";
            this.listViewGPIO.Size = new System.Drawing.Size(306, 412);
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
            // FormOBKConfig
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(829, 487);
            this.Controls.Add(this.listViewGPIO);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.textBoxMQTTPass);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.textBoxMQTTUser);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.textBoxMQTTGroup);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.textBoxMQTTTopic);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textBoxMQTTPort);
            this.Controls.Add(this.textBoxMQTTHost);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.textBoxWiFiPass);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxWiFiSSID);
            this.Controls.Add(this.label1);
            this.Name = "FormOBKConfig";
            this.Text = "FormOBKConfig";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormOBKConfig_FormClosing);
            this.Load += new System.EventHandler(this.FormOBKConfig_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.FormOBKConfig_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.FormOBKConfig_DragEnter);
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
    }
}