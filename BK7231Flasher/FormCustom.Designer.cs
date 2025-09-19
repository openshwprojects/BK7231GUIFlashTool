namespace BK7231Flasher
{
    partial class FormCustom
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
            this.textBoxCh0 = new System.Windows.Forms.TextBox();
            this.textBoxCh1 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.buttonCustomRead = new System.Windows.Forms.Button();
            this.buttonCustomWrite = new System.Windows.Forms.Button();
            this.btnRestoreRF = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBoxCh0
            // 
            this.textBoxCh0.Location = new System.Drawing.Point(81, 6);
            this.textBoxCh0.Name = "textBoxCh0";
            this.textBoxCh0.Size = new System.Drawing.Size(94, 20);
            this.textBoxCh0.TabIndex = 1;
            this.textBoxCh0.Text = "0x11000";
            // 
            // textBoxCh1
            // 
            this.textBoxCh1.Location = new System.Drawing.Point(230, 6);
            this.textBoxCh1.Name = "textBoxCh1";
            this.textBoxCh1.Size = new System.Drawing.Size(121, 20);
            this.textBoxCh1.TabIndex = 2;
            this.textBoxCh1.Text = "0x1000";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(63, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Start Offset:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(181, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(43, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Length:";
            // 
            // buttonCustomRead
            // 
            this.buttonCustomRead.Location = new System.Drawing.Point(15, 136);
            this.buttonCustomRead.Name = "buttonCustomRead";
            this.buttonCustomRead.Size = new System.Drawing.Size(160, 23);
            this.buttonCustomRead.TabIndex = 7;
            this.buttonCustomRead.Text = "Do Custom Read";
            this.buttonCustomRead.UseVisualStyleBackColor = true;
            this.buttonCustomRead.Click += new System.EventHandler(this.button1_Click);
            // 
            // buttonCustomWrite
            // 
            this.buttonCustomWrite.Location = new System.Drawing.Point(181, 136);
            this.buttonCustomWrite.Name = "buttonCustomWrite";
            this.buttonCustomWrite.Size = new System.Drawing.Size(170, 23);
            this.buttonCustomWrite.TabIndex = 8;
            this.buttonCustomWrite.Text = "Do Custom Write";
            this.buttonCustomWrite.UseVisualStyleBackColor = true;
            this.buttonCustomWrite.Click += new System.EventHandler(this.buttonCustomWrite_Click);
            // 
            // btnRestoreRF
            // 
            this.btnRestoreRF.Location = new System.Drawing.Point(181, 107);
            this.btnRestoreRF.Name = "btnRestoreRF";
            this.btnRestoreRF.Size = new System.Drawing.Size(170, 23);
            this.btnRestoreRF.TabIndex = 9;
            this.btnRestoreRF.Text = "Restore RF from backup";
            this.btnRestoreRF.UseVisualStyleBackColor = true;
            this.btnRestoreRF.Click += new System.EventHandler(this.btnRestoreRF_Click);
            // 
            // FormCustom
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(361, 171);
            this.Controls.Add(this.btnRestoreRF);
            this.Controls.Add(this.buttonCustomWrite);
            this.Controls.Add(this.buttonCustomRead);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxCh1);
            this.Controls.Add(this.textBoxCh0);
            this.Name = "FormCustom";
            this.Text = "FormCustom";
            this.Load += new System.EventHandler(this.FormCustom_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox textBoxCh0;
        private System.Windows.Forms.TextBox textBoxCh1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button buttonCustomRead;
        private System.Windows.Forms.Button buttonCustomWrite;
        private System.Windows.Forms.Button btnRestoreRF;
    }
}