using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        OBKScanner scan;
        List<OBKDeviceAPI> founds = new List<OBKDeviceAPI>();

        private void buttonStartScan_Click(object sender, EventArgs e)
        {
            if (scan != null)
            {
                buttonStartScan.Text = "Stopping...";
                scan.requestStop();
                return;
            }
            IPAddress tmp;
            if(IPAddress.TryParse(textBoxStartIP.Text, out tmp) == false)
            {
                MessageBox.Show("Invalid start IP");
                return;
            }
            if (IPAddress.TryParse(textBoxEndIP.Text, out tmp) == false)
            {
                MessageBox.Show("Invalid end IP");
                return;
            }

            scan = new OBKScanner(textBoxStartIP.Text, textBoxEndIP.Text);
            scan.setOnDeviceFound(onScannerFound);
            scan.setOnFinished(onScannerFinished);
            scan.startScan();
            buttonStartScan.Text = "Stop";
        }
        
        private void onScannerFinished(bool bInterrupted)
        {
            if (this.InvokeRequired)
            {
                Singleton.textBoxLog.Invoke((MethodInvoker)delegate
                {
                    onScannerFinished(bInterrupted);
                });
                return;
            }
            scan = null;
            buttonStartScan.Text = "Start";
        }

        private void onScannerFound(OBKDeviceAPI api)
        {
            if(this.InvokeRequired)
            {
                Singleton.textBoxLog.Invoke((MethodInvoker)delegate
                {
                    onScannerFound(api);
                });
                return;
            }
            OBKDeviceAPI exi = findDeviceForIP(api.getAdr());

            if (exi != null)
            {
                updateItem(exi);
            }
            else
            {
                api.setUserIndex(founds.Count);
                founds.Add(api);
                updateItem(api);
            }
        }

        private void updateItem(OBKDeviceAPI exi)
        {
            while(listView1.Items.Count <= exi.getUserIndex())
            {
                listView1.Items.Add(new ListViewItem());
            }
            updateItem(exi, listView1.Items[exi.getUserIndex()]);
        }

        private OBKDeviceAPI findDeviceForIP(string s)
        {
            for(int i = 0; i < founds.Count; i++)
            {
                if (founds[i].hasAdr(s))
                {
                    return founds[i];
                }
            }
            return null;
        }

        private void textBoxScannerThreads_TextChanged(object sender, EventArgs e)
        {
            if (scan != null)
            {
                int cnt;
                if(int.TryParse(textBoxScannerThreads.Text,out cnt))
                {
                    scan.setMaxWorkers(cnt);
                }
            }
        }
        void updateItem(OBKDeviceAPI dev, ListViewItem it)
        {
            setSubItem(it, 0, dev.getUserIndex().ToString());
            setSubItem(it, 1, dev.getAdr());
            setSubItem(it, 2, dev.getShortName());
            setSubItem(it, 3, dev.getChipSet());
            setSubItem(it, 4, dev.getMAC());
            setSubItem(it, 5, dev.getBuild());
        }
        void setSubItem(ListViewItem it, int index, string s)
        {
            while(it.SubItems.Count <= index)
            {
                it.SubItems.Add(new ListViewItem.ListViewSubItem());
            }
            it.SubItems[index].Text = s;
        }
    }
}
