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
        public static string DEFAULT_BASE_DIR = Path.Combine("backups", "massNetworkBackups");
        private const int COMMAND_WAIT_TIMEOUT_MS = 10000;
        private const int FLASH_WAIT_TIMEOUT_MS = 60000;

        List<OBKDeviceAPI> devices = new List<OBKDeviceAPI>();
        Thread thread;
        string deviceDirectory;
        string baseDir;
        string deviceDirName = "";
        MassBackupProgressUpdate onProgress;
        MassBackupFinished onFinished;
        readonly ManualResetEventSlim downloadCompleted = new ManualResetEventSlim(false);
        int activeDownloadGeneration;

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
        int beginDownload()
        {
            int generation = Interlocked.Increment(ref activeDownloadGeneration);
            downloadCompleted.Reset();
            downloadState = DownloadState.Pending;
            return generation;
        }
        bool waitForDownload(int generation, int timeoutMS)
        {
            if (downloadCompleted.Wait(timeoutMS))
            {
                return downloadState == DownloadState.Ok;
            }
            if (Interlocked.CompareExchange(
                ref activeDownloadGeneration, generation + 1, generation) == generation)
            {
                downloadState = DownloadState.Error;
                onProgress?.Invoke("Timed out downloading " + downloadTarget
                    + " for " + deviceDirName + ".");
            }
            return false;
        }
        void processDeviceTASCommand(int index, string cmd, DownloadTarget dt)
        {
            OBKDeviceAPI dev = devices[index];
            downloadTarget = dt;
            for (int at = 0; at < 5; at++)
            {
                int generation = beginDownload();
                dev.sendCmnd(cmd, (self, reply, replyText) =>
                    onTasReplyTemplate(generation, self, reply, replyText));
                waitForDownload(generation, COMMAND_WAIT_TIMEOUT_MS);
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
        private void onTasReplyTemplate(
            int generation,
            OBKDeviceAPI self,
            JsonObject reply,
            string replyText)
        {
            if (generation != Volatile.Read(ref activeDownloadGeneration))
            {
                return;
            }
            try
            {
                if (reply != null)
                {
                    string fileName = deviceDirName + "_" + downloadTarget.ToString() + ".txt";
                    File.WriteAllText(Path.Combine(deviceDirectory, fileName), replyText);
                    downloadState = DownloadState.Ok;
                }
                else
                {
                    downloadState = DownloadState.Error;
                }
            }
            catch (Exception ex)
            {
                downloadState = DownloadState.Error;
                Console.WriteLine("Failed to save " + downloadTarget + ": " + ex.Message);
            }
            finally
            {
                if (generation == Volatile.Read(ref activeDownloadGeneration))
                {
                    downloadCompleted.Set();
                }
            }
        }
        int stat_totalErrors;
        int stat_totalRetriesDone;
        DownloadState downloadState;
        DownloadTarget downloadTarget;
        private void onGenericDownloadReady(
            int generation,
            int expectedLength,
            byte[] data,
            int dataLen)
        {
            if (generation != Volatile.Read(ref activeDownloadGeneration))
            {
                return;
            }
            try
            {
                if (data == null
                    || dataLen != expectedLength
                    || data.Length != expectedLength)
                {
                    downloadState = DownloadState.Error;
                    Console.WriteLine("Device: " + deviceDirName + ", mode "
                        + downloadTarget + ", incomplete download!");
                }
                else
                {
                    string fileName = deviceDirName + "_" + downloadTarget.ToString() + ".bin";
                    Console.WriteLine("Device: " + deviceDirName + ", mode "
                        + downloadTarget + ", saving result to file...");
                    File.WriteAllBytes(Path.Combine(deviceDirectory,fileName), data);
                    downloadState = DownloadState.Ok;
                }
            }
            catch (Exception ex)
            {
                downloadState = DownloadState.Error;
                Console.WriteLine("Failed to save " + downloadTarget + ": " + ex.Message);
            }
            finally
            {
                if (generation == Volatile.Read(ref activeDownloadGeneration))
                {
                    downloadCompleted.Set();
                }
            }
        }
        private void onGenericProgress(int generation, int done, int total)
        {
            if (generation != Volatile.Read(ref activeDownloadGeneration))
            {
                return;
            }
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
                int expectedLength;
                if (mode == 0)
                {
                    OBKFlashLayout.getConfigLocation(dev.getBKType(), out int sectors);
                    expectedLength = sectors * BK7231Flasher.SECTOR_SIZE;
                }
                else
                {
                    expectedLength = TuyaConfig.getMagicSize(dev.getBKType());
                }
                if (expectedLength <= 0)
                {
                    downloadTarget = mode == 0
                        ? DownloadTarget.OBKConfig
                        : DownloadTarget.TuyaConfig;
                    onProgress?.Invoke("Skipping " + downloadTarget + " for " + deviceDirName
                        + ": this backup is not supported for " + dev.getChipSet() + ".");
                    continue;
                }
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    downloadTarget = mode == 0
                        ? DownloadTarget.OBKConfig
                        : DownloadTarget.TuyaConfig;
                    int generation = beginDownload();
                    if(mode == 0)
                    {
                        dev.sendGetFlashChunk_OBKConfig(
                            (data, dataLen) => onGenericDownloadReady(
                                generation, expectedLength, data, dataLen),
                            (done, total) => onGenericProgress(generation, done, total));
                    }
                    else
                    {
                        dev.sendGetFlashChunk_TuyaCFGFromOBKDevice(
                            (data, dataLen) => onGenericDownloadReady(
                                generation, expectedLength, data, dataLen),
                            (done, total) => onGenericProgress(generation, done, total));
                    }
                    waitForDownload(generation, FLASH_WAIT_TIMEOUT_MS);
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
