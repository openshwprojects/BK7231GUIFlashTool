namespace BK7231Flasher
{
    partial class FormExtractedConfig
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
            this.textBoxTuyaCFGJSON = new System.Windows.Forms.TextBox();
            this.textBoxTuyaCFGText = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelMac = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(796, 24);
            this.label1.TabIndex = 0;
            this.label1.Text = "We\'ve tried to extract device Template/GPIO config from the device flash for you," +
    " here is result:";
            // 
            // textBoxTuyaCFGJSON
            // 
            this.textBoxTuyaCFGJSON.Location = new System.Drawing.Point(17, 66);
            this.textBoxTuyaCFGJSON.Multiline = true;
            this.textBoxTuyaCFGJSON.Name = "textBoxTuyaCFGJSON";
            this.textBoxTuyaCFGJSON.Size = new System.Drawing.Size(317, 356);
            this.textBoxTuyaCFGJSON.TabIndex = 1;
            // 
            // textBoxTuyaCFGText
            // 
            this.textBoxTuyaCFGText.Location = new System.Drawing.Point(340, 66);
            this.textBoxTuyaCFGText.Multiline = true;
            this.textBoxTuyaCFGText.Name = "textBoxTuyaCFGText";
            this.textBoxTuyaCFGText.Size = new System.Drawing.Size(317, 356);
            this.textBoxTuyaCFGText.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(17, 47);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(70, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "JSON format:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(337, 50);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(85, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Text description:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(17, 445);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(498, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "You can just copy-paste the JSON format into OBK Web App->Import field to setup G" +
    "PIO of your device.";
            // 
            // labelMac
            // 
            this.labelMac.AutoSize = true;
            this.labelMac.Location = new System.Drawing.Point(17, 429);
            this.labelMac.Name = "labelMac";
            this.labelMac.Size = new System.Drawing.Size(200, 13);
            this.labelMac.TabIndex = 6;
            this.labelMac.Text = "It also seems that device MAC is [TODO]";
            // 
            // FormExtractedConfig
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(815, 480);
            this.Controls.Add(this.labelMac);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxTuyaCFGText);
            this.Controls.Add(this.textBoxTuyaCFGJSON);
            this.Controls.Add(this.label1);
            this.Name = "FormExtractedConfig";
            this.Text = "Tuya Config Quick Viewer - see original Tuya user_param_key device configuration";
            this.Load += new System.EventHandler(this.FormExtractedConfig_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxTuyaCFGJSON;
        private System.Windows.Forms.TextBox textBoxTuyaCFGText;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelMac;
    }
}