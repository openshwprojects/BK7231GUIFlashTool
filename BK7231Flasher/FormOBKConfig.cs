using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormOBKConfig : Form
    {
        FormMain main;
        OBKConfig cfg;

        public FormOBKConfig(FormMain mf)
        {
            cfg = new OBKConfig();
            cfg.setDefaults();
            this.main = mf;
            InitializeComponent();
        }
        public void saveBinding()
        {
            try
            {
                    for (int i = 0; i < this.Controls.Count; i++)
                    {
                        var c = this.Controls[i];
                        if (c is TextBox)
                        {
                            TextBox tb = c as TextBox;
                            for (int j = 0; j < tb.DataBindings.Count; j++)
                            {
                                Binding b = tb.DataBindings[j];
                                //b.WriteValue();
                            }
                    //    tb.Text = tb.Text;
                        }
                    }
            }
            catch (Exception ex)
            {

            }
        }
        void refreshBinding_r(Control.ControlCollection list)
        {
            // Running on the UI thread
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c is TextBox)
                {
                    TextBox tb = c as TextBox;
                    for (int j = 0; j < tb.DataBindings.Count; j++)
                    {
                        var b = tb.DataBindings[j];
                        b.ReadValue();
                    }
                    tb.Invalidate();
                    tb.Refresh();
                }
                refreshBinding_r(c.Controls);
            }
        }
        void refreshBinding()
        {
            refreshBinding_r(this.Controls);
        }
        FormPin fp;
        List<ComboBox> pinRoles = new List<ComboBox>();
        void refreshPins()
        {
            for(int i = 0; i < listViewGPIO.Items.Count; i++)
            {
                ListViewItem it = listViewGPIO.Items[i];
                it.SubItems[0].Text = "P" + i;
                int role = cfg.getPinRole(i);
                it.SubItems[1].Text = OBKRoles.names[role];
                it.SubItems[2].Text = "" + cfg.getPinChannel(i);
                it.SubItems[3].Text = "" + cfg.getPinChannel2(i);
                if(role == 0)
                {
                    it.ForeColor = Color.Gray;
                }
                else
                {
                    it.ForeColor = Color.Black;
                }
            }
        }
        List<CheckBox> flagCheckBoxes = new List<CheckBox>();
        public void refreshCheckBoxes()
        {
            for(int i = 0; i < flagCheckBoxes.Count; i++)
            {
                if (cfg.hasFlag(i))
                {
                    flagCheckBoxes[i].Checked = true;
                }
                else
                {
                    flagCheckBoxes[i].Checked = false;
                }
            }
        }
        private void comboBox_flag_onToggle(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            int flag = (int)cb.Tag;
            if (cfg != null)
            {
                if (cfg.hasFlag(flag) != cb.Checked)
                {
                    cfg.setFlag(flag, cb.Checked);
                }
            }
        }
        private void FormOBKConfig_Load(object sender, EventArgs e)
        {
            fp = new FormPin();
            this.textBoxWiFiSSID.DataBindings.Add(new Binding("Text", cfg, "wifi_ssid",false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxWiFiPass.DataBindings.Add(new Binding("Text", cfg, "wifi_pass", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTPort.DataBindings.Add(new Binding("Text", cfg, "mqtt_port", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTHost.DataBindings.Add(new Binding("Text", cfg, "mqtt_host", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTTopic.DataBindings.Add(new Binding("Text", cfg, "mqtt_clientId", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTUser.DataBindings.Add(new Binding("Text", cfg, "mqtt_userName", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTPass.DataBindings.Add(new Binding("Text", cfg, "mqtt_pass", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxMQTTGroup.DataBindings.Add(new Binding("Text", cfg, "mqtt_group", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxShortName.DataBindings.Add(new Binding("Text", cfg, "shortDeviceName", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxLongName.DataBindings.Add(new Binding("Text", cfg, "longDeviceName", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxShortStartupCommand.DataBindings.Add(new Binding("Text", cfg, "initCommandLine", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxWebAppRoot.DataBindings.Add(new Binding("Text", cfg, "webappRoot", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxOBKIP.DataBindings.Add(new Binding("Text", cfg, "localIPAddr", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxOBKMask.DataBindings.Add(new Binding("Text", cfg, "netMask", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxOBKGate.DataBindings.Add(new Binding("Text", cfg, "gatewayIPAddr", false, DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxOBKDNS.DataBindings.Add(new Binding("Text", cfg, "dnsServerIpAddr", false, DataSourceUpdateMode.OnPropertyChanged));

            for (int i = 0; i < 30; i++)
            {
                ListViewItem it = new ListViewItem("P" + i);
                it.Tag = i;
                it.SubItems.Add("Relay");
                it.SubItems.Add("0");
                it.SubItems.Add("0");
                listViewGPIO.Items.Add(it);
            }
            flagCheckBoxes = new List<CheckBox>();
            for (int i = 0; i < 64; i++)
            {
                CheckBox cb;
                if(i == 0)
                {
                    cb = checkBoxFlag;
                }
                else
                {
                    cb = new CheckBox();
                }
                cb.Tag = i;
                cb.Text = "[" + i + "] - "+OBKFlags.getSafe(i);
                int x = checkBoxFlag.Location.X;
                int y = checkBoxFlag.Location.Y;
                int column;
                int row;
                if (false)
                {
                    column = i / 16;
                    row = i % 16;
                }
                else
                {
                    column = 0;
                    row = i;
                }
                x += column * 200;
                y += row * 24;
                cb.Location = new Point(x, y);
                cb.Width = 550;
                cb.Height = checkBoxFlag.Height;
                if (i != 0)
                {
                    checkBoxFlag.Parent.Controls.Add(cb);
                }
                cb.Click += new EventHandler(comboBox_flag_onToggle);
                flagCheckBoxes.Add(cb);
            }
            refreshPins();
/*
            pinRoles.Add(comboBoxRoleP0);

            for (int i = 1; i <= 30; i++)
            {
                //ComboBox comboBoxClone = (ComboBox)comboBoxRoleP0.Clone();
                ComboBox comboBoxClone = new ComboBox();
                comboBoxClone.Size = comboBoxRoleP0.Size;
                comboBoxClone.Font = comboBoxRoleP0.Font;
                comboBoxClone.DropDownStyle = comboBoxRoleP0.DropDownStyle;
                comboBoxClone.Location = new Point(comboBoxRoleP0.Location.X, comboBoxRoleP0.Location.Y + i * 24);
                comboBoxRoleP0.Parent.Controls.Add(comboBoxClone);

                Label labelClone = new Label();
                labelClone.Text = "P" + i;
                labelClone.Location = new Point(labelPinP0.Location.X, labelPinP0.Location.Y + i * 24);
                comboBoxRoleP0.Parent.Controls.Add(labelClone);


                TextBox boxChannelFirst = new TextBox();
                boxChannelFirst.Width = textBoxPin0Ch0.Width;
                boxChannelFirst.Location = new Point(textBoxPin0Ch0.Location.X, textBoxPin0Ch0.Location.Y + i * 24);
                comboBoxRoleP0.Parent.Controls.Add(boxChannelFirst);

                TextBox boxChannelSecond = new TextBox();
                boxChannelSecond.Width = textBoxPin0Ch1.Width;
                boxChannelSecond.Location = new Point(textBoxPin0Ch1.Location.X, textBoxPin0Ch1.Location.Y + i * 24);
                comboBoxRoleP0.Parent.Controls.Add(boxChannelSecond);

                pinRoles.Add(comboBoxClone);
            }
            for (int i = 0; i < pinRoles.Count; i++)
            {
                ComboBox cb = pinRoles[i];
                cb.BeginUpdate(); 
                cb.Items.AddRange(OBKRoles.names);
                cb.EndUpdate();
                cb.SelectedIndex = 0;
            }*/


        }

        private void FormOBKConfig_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        internal bool tryToLoadOBKConfig(byte[] dat, BKType curType, bool bApplyOffset = true)
        {
           bool bError= cfg.loadFrom(dat, curType, bApplyOffset);
            if(bError == false)
            {
                string t = cfg.wifi_pass;
                refreshAll();
                return false;
            }
            return true;
        }
        internal void loadFromTuyaConfig(TuyaConfig tc)
        {
            tc.getKeysHumanReadable(cfg);
            refreshAll();
        }
        bool tryToImportTuyaSettings(string fname)
        {
            TuyaConfig tc = new TuyaConfig();
            if (!tc.fromFile(fname))
            {
                if (!tc.extractKeys())
                {
                    tc.getKeysHumanReadable(cfg);
                    refreshAll();
                    return false;
                }
            }
            return true;//error
        }
        bool tryToExportOBKSettings(string fname)
        {
            cfg.saveConfig();
            File.WriteAllBytes(fname, cfg.getData());
            return false;
        }
        bool tryToImportOBKSettings(string fname, BKType type)
        {
            if(type == BKType.Detect)
            {
                if (fname.Contains("BK7231T"))
                {
                    type = BKType.BK7231T;
                }
                if (fname.Contains("BK7231U"))
                {
                    type = BKType.BK7231U;
                }
                if (fname.Contains("BK7231N"))
                {
                    type = BKType.BK7231N;
                }
                if (fname.Contains("BK7231M"))
                {
                    type = BKType.BK7231M;
                }
                if (fname.Contains("BK7236"))
                {
                    type = BKType.BK7236;
                }
                if (fname.Contains("BK7238"))
                {
                    type = BKType.BK7238;
                }
                if (fname.Contains("BK7252"))
                {
                    type = BKType.BK7252;
                }
                if (fname.Contains("BK7252N"))
                {
                    type = BKType.BK7252N;
                }
                if (fname.Contains("BK7258"))
                {
                    type = BKType.BK7258;
                }
                if (fname.Contains("RTL8720D"))
                {
                    type = BKType.RTL8720D;
                }
                if (fname.Contains("RTL87X0C"))
                {
                    type = BKType.RTL87X0C;
                }
                if (fname.Contains("RTL8710B"))
                {
                    type = BKType.RTL8710B;
                }
                if (fname.Contains("LN882H"))
                {
                    type = BKType.LN882H;
                }
            }
            bool bError = cfg.loadFrom(fname, type);
            if (bError == true)
            {
                return true;//error
            }
            refreshAll();
            return false;
        }
        private void FormOBKConfig_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            for(int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                bool bError = tryToImportOBKSettings(fname, BKType.Detect);
                if(bError == true)
                {
                    bError = tryToImportTuyaSettings(fname);
                }
                refreshAll();
            }
        }
        void refreshUIThread()
        {
            refreshBinding();
            refreshPins();
            refreshCheckBoxes();
        }

        void refreshAll()
        {
            try
            {
                if (this.InvokeRequired == false)
                {
                    refreshUIThread();
                    return;
                }
                this.textBoxLongName.Invoke((MethodInvoker)delegate {
                    // Running on the UI thread
                    refreshUIThread();
                }
                    );
            }
            catch(Exception ex)
            {

            }
        }
        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
        }

        private void listViewGPIO_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListView listView = (ListView)sender;
            ListViewItem clickedItem = listView.HitTest(e.Location).Item;

            if (clickedItem != null)
            {
                // Do something with the clicked item
                int index = (int)clickedItem.Tag;
                fp.setupEditingPin(cfg, index);
                fp.ShowDialog();
                refreshPins();
            }
        }

        private void FormOBKConfig_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true; // this cancels the close event.
        }
        string getTypeLetter(BKType t)
        {
            switch(t)
            {
                case BKType.BK7231T:
                    return "T";
                case BKType.BK7231U:
                    return "U";
                case BKType.BK7231M:
                    return "M";
                case BKType.BK7231N:
                case BKType.BK7252N:
                    return "N";
                default:
                    return "";
            }
        }
        void generateNamesForMAC(byte [] mac, BKType curType)
        {
            cfg.mqtt_clientId = "" + curType + "_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");
            cfg.shortDeviceName = "obk" + getTypeLetter(curType) + "_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");
            cfg.longDeviceName = "Open" + curType + "_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");

        }
        void generateRandomNames()
        {
            byte[] mac = new byte[6];
            Rand.r.NextBytes(mac);
            cfg.mqtt_clientId = "obk_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");
            cfg.shortDeviceName = "obk" + "_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");
            cfg.longDeviceName = "OpenBK_" + mac[3].ToString("X2") + mac[4].ToString("X2") + mac[5].ToString("X5");

        }
        internal void onMACLoaded(byte[] mac, BKType curType)
        {
            if (checkBoxAutoGenerateNames.Checked)
            {
                generateNamesForMAC(mac, curType);
            }
        }

        internal OBKConfig getCFG()
        {
            return cfg;
        }

        private void appendSettingsFromTuya2MBBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Tuya Binary files (*.bin)|*.bin|All files (*.*)|*.*";
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK && openFileDialog.FileName.Length>0)
            {
                if (tryToImportTuyaSettings(openFileDialog.FileName))
                {
                    MessageBox.Show("Failed.");
                }
                else
                {
                    refreshAll();
                    MessageBox.Show("Settings imported.");
                }
            }
        }

        private void appendSettingsFromOBKConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "OBK Binary files (*.bin)|*.bin|All files (*.*)|*.*";
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK && openFileDialog.FileName.Length > 0)
            {
                if (tryToImportOBKSettings(openFileDialog.FileName, BKType.Detect))
                {
                    MessageBox.Show("Failed.");
                }
                else
                {
                    refreshAll();
                    MessageBox.Show("Settings imported.");
                }
            }
        }

        private void buttonRandomizeNames_Click(object sender, EventArgs e)
        {
            generateRandomNames();
            refreshAll();
        }

        private void clearSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cfg.zeroMemory();
            cfg.setDefaults();
            cfg.saveConfig();
            refreshAll();
        }

        private void FormOBKConfig_Leave(object sender, EventArgs e)
        {
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*";
            DialogResult result = saveFileDialog.ShowDialog();
            if (result == DialogResult.OK && saveFileDialog.FileName.Length > 0)
            {
                if (tryToExportOBKSettings(saveFileDialog.FileName))
                {
                    MessageBox.Show("Failed to save file.");
                }
                else
                {
                    MessageBox.Show("OBK settings exported as single OBK sector. Remember that you can just drag and drop them over the form to open them later.");
                }
            }
        }
    }
}
