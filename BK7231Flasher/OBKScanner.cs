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
    public delegate void OBKScannerFoundDevice(OBKDeviceAPI api);
    public delegate void OBKScannerFinished(bool bInterrupted);
    public delegate void OBKScannerProgress(int done, int total, string comment);

    public class OBKScanner
    {
        int maxWorkers = 8;


        int loopsCount = 2;
        string startIP, endIP;
        Thread thread;
        bool bWantStop;
        List<OBKDeviceAPI> workers = new List<OBKDeviceAPI>();
        OBKScannerFoundDevice onDeviceFound;
        OBKScannerFinished onScanFinished;
        OBKScannerProgress onProgress;
        string userName, password;

        internal void requestStop()
        {
            bWantStop = true;
        }

        public OBKScanner(string start, string end)
        {
            this.startIP = start;
            this.endIP = end;
        }
        public void setOnDeviceFound(OBKScannerFoundDevice d)
        {
            this.onDeviceFound = d;
        }
        public void setOnFinished(OBKScannerFinished d)
        {
            this.onScanFinished = d;
        }
        public void setOnProgress(OBKScannerProgress d)
        {
            this.onProgress = d;
        }
        public void setMaxWorkers(int max)
        {
            this.maxWorkers = max;
        }
        public void startScan()
        {
            thread = new Thread(scanThread);
            thread.Start();
        }

        internal void setLoopsCount(int nct)
        {
            this.loopsCount = nct;
        }

        internal void setUser(string text)
        {
            this.userName = text;
        }
        internal void setPassword(string text)
        {
            this.password = text;
        }

        void callOnProgress(int done, int total, string comment = "")
        {
            if (onProgress != null)
            {
                onProgress(done, total, comment);
            }
        }
        void scanThread()
        {
            IPAddress startAddress = IPAddress.Parse(startIP);
            IPAddress endAddress = IPAddress.Parse(endIP);

            byte[] startBytes = startAddress.GetAddressBytes();
            byte[] endBytes = endAddress.GetAddressBytes();

            Array.Reverse(startBytes);
            Array.Reverse(endBytes);
            uint start = BitConverter.ToUInt32(startBytes, 0);
            uint end = BitConverter.ToUInt32(endBytes, 0);
            int total = (((int)end - (int)start)+1) * loopsCount;
            int done = 0;
            callOnProgress(done, total,"Starting scan...");
            for(int loop = 0; loop < loopsCount; loop++)
            {
                uint current = start;
                while (current <= end)
                {
                    if (bWantStop)
                    {
                        break;
                    }
                    OBKDeviceAPI worker = getWorker();
                    if (worker == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    Thread.Sleep(50);
                    int scannerTimeOutMS;
                    if(loopsCount <= 1)
                    {
                        scannerTimeOutMS = 5000;
                    }
                    else
                    {
                        if(loop == 0)
                        {
                            scannerTimeOutMS = 2000;
                        }
                        else
                        {
                            scannerTimeOutMS = 5000 + 500 * loop;
                        }
                    }
                    byte[] bytes = BitConverter.GetBytes(current);
                    Array.Reverse(bytes);
                    IPAddress ip = new IPAddress(bytes);
                    string nextIPstr = ip.ToString();
                    Console.WriteLine("Will try to check " + nextIPstr);
                    worker.clear();
                    worker.setPassword(password);
                    worker.setUser(userName);
                    worker.setAdr(ip.ToString());
                    worker.setWebRequestTimeOut(scannerTimeOutMS);
                    worker.sendGetInfo(null);
                    current++;
                    done++;
                    callOnProgress(done, total, "Checked "+nextIPstr+"...");
                }
            }
            callOnProgress(total, total, "All done.");
            onScanFinished(bWantStop);
        }

        private OBKDeviceAPI getWorker()
        {
            for(int i = 0; i < workers.Count; i++)
            {
                OBKDeviceAPI d = workers[i];
                if(d.hasBasicInfoReceived())
                {
                    processFoundDevice(d);
                    workers.RemoveAt(i);
                    break;
                }
                if (d.getInfoFailed())
                {
                    return d;
                }
            }
            if(workers.Count < maxWorkers)
            {
                OBKDeviceAPI w = new OBKDeviceAPI();
                workers.Add(w);
                return w;
            }
            return null;
        }

        private void processFoundDevice(OBKDeviceAPI d)
        {
            onDeviceFound(d);
        }
    }
}
