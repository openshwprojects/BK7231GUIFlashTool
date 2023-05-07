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
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxWiFiSSID = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxWiFiPass = new System.Windows.Forms.TextBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(423, 26);
            this.label1.TabIndex = 0;
            this.label1.Text = "This tool can automatically configure OBK on the first flashing operation of the " +
    "device.\r\nPlease first change options here, and then use \"Do firmware backup and " +
    "write\" to apply.";
            // 
            // textBoxWiFiSSID
            // 
            this.textBoxWiFiSSID.Location = new System.Drawing.Point(78, 54);
            this.textBoxWiFiSSID.Name = "textBoxWiFiSSID";
            this.textBoxWiFiSSID.Size = new System.Drawing.Size(304, 20);
            this.textBoxWiFiSSID.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 57);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "WiFi SSID:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 83);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(56, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "WiFi pass:";
            // 
            // textBoxWiFiPass
            // 
            this.textBoxWiFiPass.Location = new System.Drawing.Point(78, 80);
            this.textBoxWiFiPass.Name = "textBoxWiFiPass";
            this.textBoxWiFiPass.Size = new System.Drawing.Size(304, 20);
            this.textBoxWiFiPass.TabIndex = 4;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(16, 110);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(283, 17);
            this.checkBox1.TabIndex = 5;
            this.checkBox1.Text = "Try to import Tuya GPIO settings from firmware backup";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // FormOBKConfig
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.textBoxWiFiPass);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxWiFiSSID);
            this.Controls.Add(this.label1);
            this.Name = "FormOBKConfig";
            this.Text = "FormOBKConfig";
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
    }
}