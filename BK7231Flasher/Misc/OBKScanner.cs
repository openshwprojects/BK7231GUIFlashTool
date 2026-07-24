using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BK7231Flasher
{
    public delegate void OBKScannerFoundDevice(OBKDeviceAPI api);
    public delegate void OBKScannerFinished(bool bInterrupted);
    public delegate void OBKScannerProgress(int done, int total, string comment);

    public class OBKScanner
    {
        internal const int MAX_WORKERS = 128;
        internal const int MAX_LOOPS = 20;
        internal const ulong MAX_ADDRESSES = 65536;

        int maxWorkers = 8;


        int loopsCount = 2;
        string startIP, endIP;
        Thread thread;
        volatile bool bWantStop;
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
            if (max < 1 || max > MAX_WORKERS)
            {
                throw new ArgumentOutOfRangeException(nameof(max));
            }
            this.maxWorkers = max;
        }
        public void startScan()
        {
            thread = new Thread(scanThread);
            thread.Start();
        }

        internal void setLoopsCount(int nct)
        {
            if (nct < 1 || nct > MAX_LOOPS)
            {
                throw new ArgumentOutOfRangeException(nameof(nct));
            }
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
            uint start;
            uint end;
            string rangeError;
            if (tryParseRange(startIP, endIP, out start, out end, out rangeError) == false)
            {
                callOnProgress(0, 0, rangeError);
                onScanFinished?.Invoke(true);
                return;
            }

            int total = (int)(((ulong)end - start + 1) * (ulong)loopsCount);
            int done = 0;
            callOnProgress(done, total,"Starting scan...");
            for(int loop = 0; loop < loopsCount && bWantStop == false; loop++)
            {
                uint current = start;
                while (true)
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
                    done++;
                    callOnProgress(done, total, "Checked "+nextIPstr+"...");
                    if (current == end)
                    {
                        break;
                    }
                    current++;
                }
            }
            if (bWantStop == false)
            {
                drainWorkers();
            }
            else
            {
                processCompletedWorkers();
            }
            callOnProgress(done, total, bWantStop ? "Stopped." : "All done.");
            onScanFinished?.Invoke(bWantStop);
        }

        internal static bool tryParseRange(string startText, string endText,
            out uint start, out uint end, out string error)
        {
            start = 0;
            end = 0;
            error = null;
            IPAddress address;
            if (IPAddress.TryParse(startText?.Trim(), out address) == false
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "Invalid start IPv4 address.";
                return false;
            }
            start = ipv4ToUInt32(address);
            if (IPAddress.TryParse(endText?.Trim(), out address) == false
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "Invalid end IPv4 address.";
                return false;
            }
            end = ipv4ToUInt32(address);
            if (end < start)
            {
                error = "End IP must not be before start IP.";
                return false;
            }
            ulong addressCount = (ulong)end - start + 1;
            if (addressCount > MAX_ADDRESSES)
            {
                error = "IP range is too large. The maximum is " + MAX_ADDRESSES + " addresses.";
                return false;
            }
            return true;
        }

        private static uint ipv4ToUInt32(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private void drainWorkers()
        {
            while (bWantStop == false && processCompletedWorkers())
            {
                Thread.Sleep(50);
            }
        }

        private bool processCompletedWorkers()
        {
            bool hasPendingWorkers = false;
            for (int i = workers.Count - 1; i >= 0; i--)
            {
                OBKDeviceAPI worker = workers[i];
                if (worker.hasBasicInfoReceived())
                {
                    processFoundDevice(worker);
                    workers.RemoveAt(i);
                }
                else if (worker.getInfoFailed())
                {
                    workers.RemoveAt(i);
                }
                else
                {
                    hasPendingWorkers = true;
                }
            }
            return hasPendingWorkers;
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
