using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormDownloader : Form
    {
        static FormDownloader Singleton;
        Thread worker;
        FormMain fm;
        string list_url = "https://github.com/openshwprojects/OpenBK7231T_App/releases";
        // string list_url = "http://example.com/";
        BKType bkType;

        public FormDownloader(FormMain formMain, BKType bkType)
        {
            Singleton = this;
            this.fm = formMain;
            this.bkType = bkType;
            InitializeComponent();
        }

        private void FormDownloader_Load(object sender, EventArgs e)
        {
            startDownloaderThread();
        }
        void startDownloaderThread()
        {
            worker = new Thread(new ThreadStart(downloadThread));
            worker.Start();
        }
        public void setState(string ss)
        {
            Singleton.progressBar1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                labelState.Text = ss;
            });
        }
        public void setProgress(int cur, int max)
        {
            Singleton.progressBar1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                progressBar1.Maximum = max;
                progressBar1.Value = cur;
            });
        }
        void downloadThread()
        {
            setState("Downloading main Releases page...");
            Thread.Sleep(200);
            addLog("Target platform: " + bkType);
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolTypeExtensions.Tls11 | SecurityProtocolTypeExtensions.Tls12 | SecurityProtocolType.Ssl3;
            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (s, e) =>
            {
                setProgress((int)e.BytesReceived, (int)e.TotalBytesToReceive);
            };
            webClient.DownloadFileCompleted += (s, e) =>
            {
            };
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolTypeExtensions.Tls11 | SecurityProtocolTypeExtensions.Tls12 | SecurityProtocolType.Ssl3;
            webClient.Headers.Add("user-agent", "request");
            addLog("Will request page: " + list_url);
            string contents = webClient.DownloadString(list_url);
            if (contents.Length <= 1)
            {
                setState("Failed to download HTML page, receiver empty buffer?!");
                addError("Failed to download HTML page, receiver empty buffer?!");
                return;
            }
            addLog("Got reply length " + contents.Length);
            addLog("Now will search page for binary link...");
            string pfx = FormMain.getFirmwarePrefix(bkType);
            addLog("Searching for: " +pfx+"!");
            int ofs = contents.IndexOf(pfx);
            if(ofs == -1)
            {
                setState("Failed to find binary link!");
                addError("Failed to find binary link in "+list_url+"!");
                return;
            }
            setState("Searching downloaded page...");
            Thread.Sleep(200);
            string firmware_binary_url = pickQuotedString(contents,ofs);
            addLog("Found link: " + firmware_binary_url + "!");
            string fileName = Path.GetFileName(firmware_binary_url);
            string dir = fm.getFirmwareDir();
            string tg = Path.Combine(dir, fileName);
            addLog("Now will try to download it to " + tg+"!");
            webClient.DownloadFile(firmware_binary_url, tg);
            if (File.Exists(tg))
            {
                addSuccess("Downloaded and saved "+tg+"!");
                setState("Download ready! You can close this dialog now.");
            }
            else
            {
                setState("Failed to download!");
                addError("Failed to download!");
            }
        }
        void addLog(string s)
        {
            this.addLog(s, Color.Black);
        }
        void addError(string s)
        {
            this.addLog(s, Color.Red);
        }
        void addSuccess(string s)
        {
            this.addLog(s, Color.Green);
        }
        void addWarning(string s)
        {
            this.addLog(s, Color.Orange);
        }
        public void addLog(string s, Color col)
        {
            Singleton.richTextBoxLog.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                RichTextUtil.AppendText(Singleton.richTextBoxLog, s + Environment.NewLine, col);
            });
        }
        string pickQuotedString(string buffer, int at)
        {
            int start = at;
            while(start>0&&buffer[start] != '"')
            {
                start--;
            }
            start++;
            int end = at;
            while (end + 1 < buffer.Length && buffer[end] != '"')
            {
                end++;
            }
            return buffer.Substring(start, end - start);
        }
        private static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (error == System.Net.Security.SslPolicyErrors.None)
            {
                return true;
            }

            Console.WriteLine("X509Certificate [{0}] Policy Error: '{1}'",
                cert.Subject,
                error.ToString());

            return false;
        }
    }
}
