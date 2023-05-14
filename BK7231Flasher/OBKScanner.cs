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
    public delegate void OBKScannerProgress(int done, int total);

    public class OBKScanner
    {
        int maxWorkers = 8;
        string startIP, endIP;
        Thread thread;
        bool bWantStop;
        List<OBKDeviceAPI> workers = new List<OBKDeviceAPI>();
        OBKScannerFoundDevice onDeviceFound;
        OBKScannerFinished onScanFinished;
        OBKScannerProgress onProgress;

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
        void callOnProgress(int done, int total)
        {
            if (onProgress != null)
            {
                onProgress(done, total);
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
            int total = (int)end - (int)start;
            int done = 0;
            callOnProgress(done, total);
            uint current = start;
            int scannerTimeOutMS = 1000;
            while (current <= end)
            {
                if (bWantStop)
                {
                    break;
                }
                OBKDeviceAPI worker = getWorker();
                if(worker == null)
                {
                    Thread.Sleep(100);
                    continue;
                }
                byte[] bytes = BitConverter.GetBytes(current);
                Array.Reverse(bytes);
                IPAddress ip = new IPAddress(bytes);
                Console.WriteLine("Will try to check " +ip.ToString());
                worker.clear();
                worker.setAdr(ip.ToString());
                worker.setWebRequestTimeOut(scannerTimeOutMS);
                worker.sendGetInfo(null);
                current++;
                done++;
                callOnProgress(done, total);
            }
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
