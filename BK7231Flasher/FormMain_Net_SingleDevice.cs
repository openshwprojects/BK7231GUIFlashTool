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
        OBKDeviceAPI dev;
        public void onGetInfoReply(OBKDeviceAPI self)
        {
            Singleton.textBoxLog.Invoke((MethodInvoker)delegate {
                if(self.getInfoFailed())
                {
                    labelCheckCommunicationStatus.Text = "Failed to get reply." + Environment.NewLine;
                    setIPDeviceButtonsState(false);
                }
                else
                {
                    labelCheckCommunicationStatus.Text = " success!" + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "Chipset = " + self.getChipSet() + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "ShortName = " + self.getShortName() + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "Build = " + self.getBuild() + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "MQTTHost = " + self.getMQTTHost() + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "IP = " + self.getAdr() + Environment.NewLine;
                    labelCheckCommunicationStatus.Text += "MQTTTopic = " + self.getMQTTTopic() + Environment.NewLine;
                    JObject json = self.getInfo();
                    if (json != null)
                    {
                        labelCheckCommunicationStatus.Text += "MAC = " + json["mac"] + Environment.NewLine;
                        labelCheckCommunicationStatus.Text += "WebApp = " + json["webapp"] + Environment.NewLine;
                        labelCheckCommunicationStatus.Text += "Uptime = " + json["uptime_s"] + " seconds" + Environment.NewLine;
                    }
                    setIPDeviceButtonsState(true);
                }

            });
        }
        private void buttonCheckCommunication_Click(object sender, EventArgs e)
        {
            IPAddress tg;
            bool bOk = IPAddress.TryParse(textBoxIP.Text, out tg);
            if (bOk)
            {
                labelCheckCommunicationStatus.Text = "Sending GET...";
                dev = new OBKDeviceAPI(textBoxIP.Text);
                dev.sendGetInfo(onGetInfoReply);
            }
            else
            {
                labelCheckCommunicationStatus.Text = "Invalid IP.";
                MessageBox.Show("Please enter valid IP string");
            }
        }
        void doSingleDeviceOBKCFGDump()
        {
            dev.sendGetFlashChunk(null, null, 0x0, 4096);
        }
        void doSingleDeviceTuyaCFGDump()
        {
            dev.sendGetFlashChunk(null, null, TuyaConfig.getMagicOffset(), TuyaConfig.getMagicSize());
        }
        void setIPDeviceButtonsState(bool b)
        {
            buttonIPCFGDump.Enabled = b;
            buttonIPDump2MB.Enabled = b;
            buttonIPDownloadTuyaConfig.Enabled = b;
        }
    }
}
