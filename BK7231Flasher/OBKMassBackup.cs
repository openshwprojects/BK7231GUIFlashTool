using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    enum DownloadState
    {
        Pending,
        Ok,
        Error
    }
    enum DownloadTarget
    {
        None,
        OBKConfig,
        TuyaConfig
    }
    public delegate void MassBackupProgressUpdate(string txt);
    public delegate void MassBackupFinished();
    class OBKMassBackup
    {
        List<OBKDeviceAPI> devices = new List<OBKDeviceAPI>();
        Thread thread;
        string deviceDirectory;
        string baseDir;
        string deviceDirName = "";
        MassBackupProgressUpdate onProgress;
        MassBackupFinished onFinished;

        public void setOnProgress(MassBackupProgressUpdate cb)
        {
            onProgress = cb;
        }
        public void setOnFinished(MassBackupFinished cb)
        {
            onFinished = cb;
        }
        internal void addDevice(OBKDeviceAPI dev)
        {
            devices.Add(dev);
        }
        internal void beginBackupThread()
        {
            thread = new Thread(workerThread);
            thread.Start();
        }
        void processDeviceTAS(int index)
        {
            OBKDeviceAPI dev = devices[index];
        }
        int stat_totalRetriesDone;
        DownloadState downloadState;
        DownloadTarget downloadTarget;
        private void onGenericDownloadReady(byte[] data, int dataLen)
        {
            if(data == null || dataLen == 0)
            {
                downloadState = DownloadState.Error;
                Console.WriteLine("Device: " + deviceDirName + ", mode " + downloadTarget + ", error!");
            }
            else
            {
                downloadState = DownloadState.Ok;
                string fileName = deviceDirName + "_" + downloadTarget.ToString() + ".bin";
                Console.WriteLine("Device: " + deviceDirName + ", mode " + downloadTarget + ", saving result to file...");
                File.WriteAllBytes(Path.Combine(deviceDirectory,fileName), data);
            }
        }
        private void onGenericProgress(int done, int total)
        {
            string stat = "Downloading " + downloadTarget.ToString() + " for " + deviceDirName
                + " progress " + done + "/" + total;
            if (onProgress != null)
            {
                onProgress(stat);
            }
        }
        void processDeviceOBK(int index)
        {
            int retriesDone = 0;
            OBKDeviceAPI dev = devices[index];
            for (int mode = 0; mode < 2; mode++)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    downloadState = DownloadState.Pending;
                    if(mode == 0)
                    {
                        downloadTarget = DownloadTarget.OBKConfig;
                        dev.sendGetFlashChunk_OBKConfig(onGenericDownloadReady, onGenericProgress);
                    }
                    else
                    {
                        downloadTarget = DownloadTarget.TuyaConfig;
                        dev.sendGetFlashChunk_TuyaCFGFromOBKDevice(onGenericDownloadReady, onGenericProgress);
                    }
                    while (downloadState == DownloadState.Pending)
                    {
                        Thread.Sleep(100);
                    }
                    if (downloadState == DownloadState.Ok)
                    {
                        break;
                    }
                    retriesDone++;
                    stat_totalRetriesDone++;
                }
            }
            Console.WriteLine("Device: " + dev.getShortName() + " processed with " +retriesDone + " extra retries.");
        }
        void processDevice(int index)
        {
            OBKDeviceAPI dev = devices[index];
            if(dev.hasShortName())
            {
                deviceDirName = dev.getShortName();
            }
            else
            {
                deviceDirName = dev.getMQTTTopic();
            }
            deviceDirName += "_" + dev.getMACLast3BytesText();
            // remove ws
            deviceDirName = deviceDirName.Replace(" ", "");
            deviceDirectory = Path.Combine(baseDir, deviceDirName);
            Directory.CreateDirectory(deviceDirectory);
            File.WriteAllText(Path.Combine(deviceDirectory, deviceDirName + ".txt"), dev.getInfoText());
            if(dev.isTasmota())
            {
                processDeviceTAS(index);
            }
            else
            {
                processDeviceOBK(index);
            }
        }
        void workerThread()
        {
            stat_totalRetriesDone = 0;
            baseDir = "massNetworkBackups";
            Directory.CreateDirectory(baseDir);
            baseDir = Path.Combine(baseDir, "backup_" + MiscUtils.formatDateNowFileNameBase());
            Directory.CreateDirectory(baseDir);

            for (int i = 0; i < devices.Count; i++)
            {
                processDevice(i);
            }
            Console.WriteLine("Total backup finished with " + stat_totalRetriesDone + " extra retries.");
            if (onFinished != null)
            {
                onFinished();
            }
        }
    }
}
