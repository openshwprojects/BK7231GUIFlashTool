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
        public void onGetInfoReply(JObject json)
        {
            Singleton.textBoxLog.Invoke((MethodInvoker)delegate {

                labelCheckCommunicationStatus.Text += " success!" + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "Chipset = " + json["chipset"] + Environment.NewLine;   
                labelCheckCommunicationStatus.Text += "ShortName = " + json["shortName"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "Build = " + json["build"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "MQTTHost = " + json["mqtthost"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "MAC = " + json["mac"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "IP = " + json["ip"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "MQTTTopic = " + json["mqtttopic"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "WebApp = " + json["webapp"] + Environment.NewLine;
                labelCheckCommunicationStatus.Text += "Uptime = " + json["uptime_s"] + " seconds" + Environment.NewLine;

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

        private void buttonIPCFGDump_Click(object sender, EventArgs e)
        {
            dev.sendGetFlashChunk(null, 0x0, 4096);
        }
    }
}
