using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormOBKConfig : Form
    {
        FormMain main;
        OBKConfig cfg;

        public FormOBKConfig(FormMain mf)
        {
            this.main = mf;
            InitializeComponent();
        }
        public void refreshBinding()
        {
            for(int i = 0; i < this.Controls.Count; i++)
            {
                var c = this.Controls[i];
                if(c is TextBox)
                {
                    TextBox tb = c as TextBox;
                    for(int j = 0; j < tb.DataBindings.Count; j++)
                    {
                        var b = tb.DataBindings[j];
                        b.ReadValue();
                    }
                }
            }
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
        private void FormOBKConfig_Load(object sender, EventArgs e)
        {
            fp = new FormPin();
            cfg = new OBKConfig() ;
            this.textBoxWiFiSSID.DataBindings.Add(new Binding("Text", cfg, "wifi_ssid"));
            this.textBoxWiFiPass.DataBindings.Add(new Binding("Text", cfg, "wifi_pass"));
            this.textBoxMQTTPort.DataBindings.Add(new Binding("Text", cfg, "mqtt_port"));
            this.textBoxMQTTHost.DataBindings.Add(new Binding("Text", cfg, "mqtt_host"));
            this.textBoxMQTTTopic.DataBindings.Add(new Binding("Text", cfg, "mqtt_clientId"));
            this.textBoxMQTTUser.DataBindings.Add(new Binding("Text", cfg, "mqtt_userName"));
            this.textBoxMQTTPass.DataBindings.Add(new Binding("Text", cfg, "mqtt_pass"));
            this.textBoxMQTTGroup.DataBindings.Add(new Binding("Text", cfg, "mqtt_group"));

            for (int i = 0; i < 30; i++)
            {
                ListViewItem it = new ListViewItem("P" + i);
                it.Tag = i;
                it.SubItems.Add("Relay");
                it.SubItems.Add("0");
                it.SubItems.Add("0");
                listViewGPIO.Items.Add(it);
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

        internal void loadFromTuyaConfig(TuyaConfig tc)
        {
            tc.getKeysHumanReadable(cfg);
        }
        private void FormOBKConfig_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            for(int i = 0; i < files.Length; i++)
            {
                string fname = files[i];
                BKType type = BKType.BK7231N;
                if (fname.Contains("BK7231T"))
                {
                    type = BKType.BK7231T;
                }
                bool bOk = cfg.loadFrom(fname, type);
                if(bOk == false)
                {
                    TuyaConfig tc = new TuyaConfig();
                    if (!tc.fromFile(fname))
                    {
                        if (!tc.extractKeys())
                        {
                            tc.getKeysHumanReadable(cfg);
                        }
                    }
                }
                refreshBinding();
                refreshPins();
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

    }
}
