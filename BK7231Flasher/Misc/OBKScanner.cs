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
        internal const int MAX_ATTEMPTS = 20;
        internal const ulong MAX_ADDRESSES = 65536;

        int maxWorkers = 8;


        int attemptsCount = 1;
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

        internal void setAttemptsCount(int count)
        {
            if (count < 1 || count > MAX_ATTEMPTS)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            this.attemptsCount = count;
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

            List<string> addressesToCheck = new List<string>();
            uint current = start;
            while (true)
            {
                addressesToCheck.Add(uint32ToIPv4(current));
                if (current == end)
                {
                    break;
                }
                current++;
            }

            int total = addressesToCheck.Count;
            int done = 0;
            callOnProgress(done, total,"Starting scan...");
            for (int attempt = 0;
                attempt < attemptsCount && addressesToCheck.Count > 0 && bWantStop == false;
                attempt++)
            {
                List<string> retryAddresses =
                    attempt + 1 < attemptsCount ? new List<string>() : null;
                foreach (string nextIPstr in addressesToCheck)
                {
                    if (bWantStop)
                    {
                        break;
                    }
                    OBKDeviceAPI worker;
                    while ((worker = getWorker(retryAddresses)) == null && bWantStop == false)
                    {
                        Thread.Sleep(100);
                    }
                    if (bWantStop)
                    {
                        break;
                    }
                    Thread.Sleep(50);
                    int scannerTimeOutMS =
                        attempt == 0 ? 2000 : 5000 + 500 * (attempt - 1);
                    Console.WriteLine("Will try to check " + nextIPstr);
                    worker.clear();
                    worker.setPassword(password);
                    worker.setUser(userName);
                    worker.setAdr(nextIPstr);
                    worker.setWebRequestTimeOut(scannerTimeOutMS);
                    worker.sendGetInfo(null);
                    done++;
                    callOnProgress(done, total, "Checked " + nextIPstr + "...");
                }
                if (bWantStop == false)
                {
                    drainWorkers(retryAddresses);
                }
                else
                {
                    processCompletedWorkers(null);
                }
                if (retryAddresses != null)
                {
                    addressesToCheck = retryAddresses;
                    if (retryAddresses.Count > 0 && bWantStop == false)
                    {
                        total += retryAddresses.Count;
                        callOnProgress(done, total,
                            "Retrying " + retryAddresses.Count + " address(es)...");
                    }
                }
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

        private static string uint32ToIPv4(uint address)
        {
            return ((address >> 24) & 0xFF) + "."
                + ((address >> 16) & 0xFF) + "."
                + ((address >> 8) & 0xFF) + "."
                + (address & 0xFF);
        }

        private void drainWorkers(List<string> retryAddresses)
        {
            while (bWantStop == false && processCompletedWorkers(retryAddresses))
            {
                Thread.Sleep(50);
            }
        }

        private bool processCompletedWorkers(List<string> retryAddresses)
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
                    retryAddresses?.Add(worker.getAdr());
                    workers.RemoveAt(i);
                }
                else
                {
                    hasPendingWorkers = true;
                }
            }
            return hasPendingWorkers;
        }

        private OBKDeviceAPI getWorker(List<string> retryAddresses)
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
                    retryAddresses?.Add(d.getAdr());
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
