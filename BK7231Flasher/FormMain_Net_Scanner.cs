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
            public int SortOrder { get; set; }
        }

        OBKScanner scan;
        List<OBKDeviceAPI> founds = new List<OBKDeviceAPI>();
        ContextMenuStrip scannerDeviceMenu;
        ContextMenuStrip scannerSubnetMenu;

        private void killScanner()
        {
            if (scan != null)
            {
                scan.requestStop();
            }
        }
        private void clearScannerResults()
        {
            founds.Clear();
            listView1.Items.Clear();
        }
        private void startOrStopScannerThread()
        {
            if (scan != null)
            {
                buttonStartScan.Text = "Stopping...";
                scan.requestStop();
                return;
            }
            uint startAddress;
            uint endAddress;
            string rangeError;
            if (OBKScanner.tryParseRange(textBoxStartIP.Text, textBoxEndIP.Text,
                out startAddress, out endAddress, out rangeError) == false)
            {
                MessageBox.Show(rangeError);
                return;
            }
            int attemptsCount;
            if (int.TryParse(textBoxBoxScannerRetries.Text, out attemptsCount) == false
                || attemptsCount < 1 || attemptsCount > OBKScanner.MAX_ATTEMPTS)
            {
                MessageBox.Show("Attempts must be between 1 and " + OBKScanner.MAX_ATTEMPTS + ".");
                return;
            }
            int workersCount;
            if (int.TryParse(textBoxScannerThreads.Text, out workersCount) == false
                || workersCount < 1 || workersCount > OBKScanner.MAX_WORKERS)
            {
                MessageBox.Show("Threads must be between 1 and " + OBKScanner.MAX_WORKERS + ".");
                return;
            }

            clearScannerResults();
            scan = new OBKScanner(textBoxStartIP.Text, textBoxEndIP.Text);
            scan.setUser(textBoxIPScannerUser.Text);
            scan.setPassword(textBoxIPScannerPass.Text);
            scan.setOnDeviceFound(onScannerFound);
            scan.setOnFinished(onScannerFinished);
            scan.setOnProgress(onScannerProgress);
            scan.setAttemptsCount(attemptsCount);
            scan.setMaxWorkers(workersCount);
            scan.startScan();
            buttonStartScan.Text = "Stop scan";
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
            labelScanState.Text = "Scan status: " + done + "/" + total + " requests sent. " + comment;
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
            buttonStartScan.Text = "Start scan";
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
            resizeScannerBuildColumn();
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
                if (int.TryParse(textBoxScannerThreads.Text, out cnt)
                    && cnt >= 1 && cnt <= OBKScanner.MAX_WORKERS)
                {
                    scan.setMaxWorkers(cnt);
                }
            }
        }
        private void listView1_Resize(object sender, EventArgs e)
        {
            resizeScannerBuildColumn();
        }
        private void resizeScannerBuildColumn()
        {
            int fixedWidth = columnID.Width + columnHeader1.Width + columnHeader2.Width
                + columnHeader3.Width + columnHeader4.Width;
            int availableWidth = listView1.ClientSize.Width - fixedWidth - 2;
            if (listView1.Items.Count > 0
                && listView1.Items[listView1.Items.Count - 1].Bounds.Bottom
                    > listView1.ClientSize.Height)
            {
                availableWidth -= SystemInformation.VerticalScrollBarWidth;
            }
            columnHeader5.Width = Math.Max(100, availableWidth);
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
                MessageBox.Show("No supported Ethernet or Wi-Fi IPv4 subnets were detected.");
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

        private static List<ScannerSubnetChoice> getLocalScannerSubnets()
        {
            List<ScannerSubnetChoice> result = new List<ScannerSubnetChoice>();
            HashSet<string> seen = new HashSet<string>();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (isScannerInterfaceType(nic.NetworkInterfaceType) == false)
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

                    IPAddress mask = uni.IPv4Mask;
                    if (mask == null)
                    {
                        continue;
                    }

                    uint addressValue = scannerIPv4ToUInt32(address);
                    uint maskValue = scannerIPv4ToUInt32(mask);
                    int prefixLength = getPrefixLength(maskValue);
                    if (prefixLength < 0)
                    {
                        continue;
                    }

                    uint network = addressValue & maskValue;
                    uint broadcast = network | ~maskValue;
                    if (broadcast - network <= 1)
                    {
                        continue;
                    }

                    uint firstHost = network + 1;
                    uint lastHost = broadcast - 1;
                    ulong addressCount = (ulong)lastHost - firstHost + 1;
                    if (addressCount > OBKScanner.MAX_ADDRESSES)
                    {
                        continue;
                    }

                    string key = nic.Id + "|" + network + "|" + maskValue;
                    if (seen.Add(key) == false)
                    {
                        continue;
                    }

                    string startIp = scannerUInt32ToIPv4(firstHost);
                    string endIp = scannerUInt32ToIPv4(lastHost);
                    bool wired = nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211;
                    result.Add(new ScannerSubnetChoice
                    {
                        StartIp = startIp,
                        EndIp = endIp,
                        SortOrder = wired ? 0 : 1,
                        DisplayText = nic.Name + " — " + address + "/" + prefixLength
                            + " (scan " + startIp + "–" + endIp + ")",
                    });
                }
            }

            return result
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.DisplayText, StringComparer.Ordinal)
                .ToList();
        }

        private static bool isScannerInterfaceType(NetworkInterfaceType type)
        {
            return type == NetworkInterfaceType.Ethernet
                || type == NetworkInterfaceType.FastEthernetFx
                || type == NetworkInterfaceType.FastEthernetT
                || type == NetworkInterfaceType.GigabitEthernet
                || type == NetworkInterfaceType.Wireless80211;
        }

        private static uint scannerIPv4ToUInt32(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private static string scannerUInt32ToIPv4(uint address)
        {
            return ((address >> 24) & 0xFF) + "."
                + ((address >> 16) & 0xFF) + "."
                + ((address >> 8) & 0xFF) + "."
                + (address & 0xFF);
        }

        private static int getPrefixLength(uint mask)
        {
            int prefixLength = 0;
            bool foundZero = false;
            for (int bit = 31; bit >= 0; bit--)
            {
                bool isSet = (mask & (1u << bit)) != 0;
                if (isSet)
                {
                    if (foundZero)
                    {
                        return -1;
                    }
                    prefixLength++;
                }
                else
                {
                    foundZero = true;
                }
            }
            return prefixLength;
        }
    }
}
