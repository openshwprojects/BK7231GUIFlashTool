using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
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
        TuyaConfig,
        TasmotaTemplate,
        TasmotaStatus0,
        TasmotaStatus1
    }
    public delegate void MassBackupProgressUpdate(string txt);
    public delegate void MassBackupFinished(int totalErrors, int totalRetries);
    class OBKMassBackup
    {
        public static string DEFAULT_BASE_DIR = "massNetworkBackups";

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
        void processDeviceTASCommand(int index, string cmd, DownloadTarget dt)
        {
            OBKDeviceAPI dev = devices[index];
            downloadTarget = dt;
            for (int at = 0; at < 5; at++)
            {
                downloadState = DownloadState.Pending;
                dev.sendCmnd(cmd, onTasReplyTemplate);
                while (downloadState == DownloadState.Pending)
                {
                    Thread.Sleep(100);
                }
                if (downloadState == DownloadState.Ok)
                {
                    break;
                }
                stat_totalRetriesDone++;
                Thread.Sleep(250 + 250 * at);
            }
            if(downloadState == DownloadState.Error)
            {
                stat_totalErrors++;
            }
        }
        void processDeviceTAS(int index)
        {
            processDeviceTASCommand(index, "Template", DownloadTarget.TasmotaTemplate);
            processDeviceTASCommand(index, "Status 0", DownloadTarget.TasmotaStatus0);
            processDeviceTASCommand(index, "Status 1", DownloadTarget.TasmotaStatus1);
        }
        private void onTasReplyTemplate(OBKDeviceAPI self, JsonObject reply, string replyText)
        {
            if (reply != null)
            {
                downloadState = DownloadState.Ok;
                string fileName = deviceDirName + "_" + downloadTarget.ToString() + ".txt";
                File.WriteAllText(Path.Combine(deviceDirectory, fileName), replyText);
            }
            else
            {
                downloadState = DownloadState.Error;
            }
        }
        int stat_totalErrors;
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
            stat += ", total fatal download errors so far: " + stat_totalErrors + ", retries " + stat_totalRetriesDone;
            if (onProgress != null)
            {
                onProgress(stat);
            }
        }
        void processDeviceOBK(int index)
        {
            int retriesDone = 0;
            int faileds = 0;
            OBKDeviceAPI dev = devices[index];
            for (int mode = 0; mode < 2; mode++)
            {
                for (int attempt = 0; attempt < 8; attempt++)
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
                    Thread.Sleep(250+attempt*250);
                    retriesDone++;
                    stat_totalRetriesDone++;
                }
                if(downloadState == DownloadState.Error)
                {
                    stat_totalErrors++;
                    faileds++;
                }
            }
            Console.WriteLine("Device: " + dev.getShortName() + " processed with " +retriesDone + " extra retries and " + faileds + " failures.");
        }
        void processDevice(int index)
        {
            Thread.Sleep(50);
            OBKDeviceAPI dev = devices[index];
            dev.setWebRequestTimeOut(5000);
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
            stat_totalErrors = 0;
            stat_totalRetriesDone = 0;
            baseDir = DEFAULT_BASE_DIR;
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
                onFinished(stat_totalErrors,stat_totalRetriesDone);
            }
        }
    }
}
