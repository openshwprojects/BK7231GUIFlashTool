using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        sealed class ScannerSubnetChoice
        {
            public string DisplayText { get; set; }
            public string StartIp { get; set; }
            public string EndIp { get; set; }
        }

        OBKScanner scan;
        List<OBKDeviceAPI> founds = new List<OBKDeviceAPI>();
        ContextMenuStrip scannerSubnetMenu;

        private void killScanner()
        {
            if (scan != null)
            {
                scan.requestStop();
            }
        }
        private void startOrStopScannerThread()
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
            int retriesCount;
            if (int.TryParse(textBoxBoxScannerRetries.Text, out retriesCount) == false)
            {
                MessageBox.Show("Invalid retries count");
                return;
            }
            if (int.TryParse(textBoxBoxScannerRetries.Text, out retriesCount) == false)
            {
                MessageBox.Show("Invalid retries count");
                return;
            }
            if(retriesCount < 1)
            {
                MessageBox.Show("It makes no sense to have less than 1 loops.");
                return;
            }

            scan = new OBKScanner(textBoxStartIP.Text, textBoxEndIP.Text);
            scan.setUser(textBoxIPScannerUser.Text);
            scan.setPassword(textBoxIPScannerPass.Text);
            scan.setOnDeviceFound(onScannerFound);
            scan.setOnFinished(onScannerFinished);
            scan.setOnProgress(onScannerProgress);
            scan.setLoopsCount(retriesCount);
            setMaxWorkersCountFromGUI();
            scan.startScan();
            buttonStartScan.Text = "Stop";
        }

        private void onScannerProgress(int done, int total, string comment)
        {
            if (this.InvokeRequired)
            {
                Singleton.textBoxLog.Invoke((MethodInvoker)delegate
                {
                    onScannerProgress(done,total, comment);
                });
                return;
            }
            labelScanState.Text = "Scan progress: " + done + "/" + total + " requests sent. "+comment;
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
                Singleton.buttonStartScan.Invoke((MethodInvoker)delegate
                {
                    Singleton.onScannerFound(api);
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

        private void setMaxWorkersCountFromGUI()
        {
            if (scan != null)
            {
                int cnt;
                if (int.TryParse(textBoxScannerThreads.Text, out cnt))
                {
                    scan.setMaxWorkers(cnt);
                }
            }
        }
        void updateItem(OBKDeviceAPI dev, ListViewItem it)
        {
            it.Tag = dev;
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

        private void buttonPickSubnet_Click(object sender, EventArgs e)
        {
            List<ScannerSubnetChoice> subnets = getLocalScannerSubnets();

            if (subnets.Count == 0)
            {
                MessageBox.Show("No local IPv4 subnets were detected on active network interfaces.");
                return;
            }

            if (scannerSubnetMenu != null)
            {
                scannerSubnetMenu.Dispose();
            }

            scannerSubnetMenu = new ContextMenuStrip();
            scannerSubnetMenu.ItemClicked += scannerSubnetMenu_ItemClicked;

            foreach (ScannerSubnetChoice subnet in subnets)
            {
                ToolStripItem item = scannerSubnetMenu.Items.Add(subnet.DisplayText);
                item.Tag = subnet;
            }

            scannerSubnetMenu.Show(buttonPickSubnet, 0, buttonPickSubnet.Height);
        }

        private void scannerSubnetMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ScannerSubnetChoice subnet = e.ClickedItem?.Tag as ScannerSubnetChoice;
            if (subnet == null)
            {
                return;
            }

            textBoxStartIP.Text = subnet.StartIp;
            textBoxEndIP.Text = subnet.EndIp;
        }

        private List<ScannerSubnetChoice> getLocalScannerSubnets()
        {
            Dictionary<string, ScannerSubnetChoice> result = new Dictionary<string, ScannerSubnetChoice>();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                IPInterfaceProperties props;
                try
                {
                    props = nic.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation uni in props.UnicastAddresses)
                {
                    IPAddress address = uni.Address;
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    byte[] bytes = address.GetAddressBytes();
                    if (bytes[0] == 127)
                    {
                        continue;
                    }
                    if (bytes[0] == 169 && bytes[1] == 254)
                    {
                        continue;
                    }

                    string subnetBase = bytes[0] + "." + bytes[1] + "." + bytes[2];
                    if (result.ContainsKey(subnetBase))
                    {
                        continue;
                    }

                    result[subnetBase] = new ScannerSubnetChoice
                    {
                        StartIp = subnetBase + ".0",
                        EndIp = subnetBase + ".255",
                        DisplayText = subnetBase + ".0/24 (" + nic.Name + ", local " + address + ")",
                    };
                }
            }

            return result.Values
                .OrderBy(s => s.StartIp, StringComparer.Ordinal)
                .ToList();
        }
    }
}
