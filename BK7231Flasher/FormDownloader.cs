using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        string release_api_url = "https://api.github.com/repos/openshwprojects/OpenBK7231T_App/releases/latest";
        // string list_url = "http://example.com/";
        BKType bkType;
        const string enabledFeaturesUrl = "https://github.com/openshwprojects/OpenBK7231T_App/tree/main/docs/enabledFeatures.md";

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
        public void setState(string ss, Color c)
        {
            Singleton.progressBar1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                labelState.Text = ss;
                labelState.BackColor = c;
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
            try
            {
                doDownloadInternal();
            }
            catch (Exception ex)
            {
                setState("Exception!", Color.Red);
                setState(ex.ToString(), Color.Red);
                addLog("It's possible that your system does not support Secure Protocol needed by github.", Color.Red);
                addLog("Sorry, exception occurred.", Color.Red);
                addLog("Please manually download firmware from here:", Color.Red);
                addLog(list_url, Color.Red);
                string pfx = FormMain.getFirmwarePrefix(bkType);
                addLog("Please choose the file with prefix "+pfx, Color.Red);
                addLog("Please put this in 'firmwares' dir in dir where the flasher exe is and restart flasher",Color.Red);
            }
        }
        void doDownloadInternal() {
            setState("Checking latest OpenBeken release...", Color.Transparent);
            Thread.Sleep(200);
            addLog("Target platform: " + bkType);


            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (s, e) =>
            {
                if (e.TotalBytesToReceive > 0)
                    setProgress((int)e.BytesReceived, (int)e.TotalBytesToReceive);
            };
            webClient.DownloadFileCompleted += (s, e) =>
            {
            };
            webClient.Headers.Add("user-agent", "BK7231GUIFlashTool");
            webClient.Headers.Add("Accept", "application/vnd.github+json");

            addLog("Will request GitHub API: " + release_api_url);
            string tagName;
            HashSet<string> uartFlashFileNames;
            List<FirmwareAsset> releaseAssets = getLatestReleaseAssets(webClient, out tagName, out uartFlashFileNames);
            if (releaseAssets.Count <= 0)
            {
                setState("Failed to find release assets!", Color.Red);
                addError("GitHub API reply did not contain any release assets.");
                return;
            }

            addLog("Latest release tag: " + (string.IsNullOrEmpty(tagName) ? "unknown" : tagName));
            addLog("Release asset count: " + releaseAssets.Count);
            addLog("Now will search release assets for firmware binary...");

            List<FirmwareAsset> candidates = getFirmwareCandidatesForPlatform(releaseAssets, uartFlashFileNames);
            if (candidates.Count <= 0)
            {
                string pfx = FormMain.getFirmwarePrefix(bkType);
                setState("Failed to find binary link!", Color.Red);
                addError("Failed to find a firmware file for " + bkType + ".");
                addError("Expected prefix: " + pfx);
                return;
            }

            FirmwareAsset defaultAsset = pickDefaultFirmwareAsset(candidates, uartFlashFileNames);
            FirmwareAsset selectedAsset = chooseFirmwareAssetIfRequired(candidates, defaultAsset);
            if (selectedAsset == null)
            {
                setState("Download cancelled.", Color.Yellow);
                addWarning("Download cancelled by user.");
                return;
            }

            addLog("Selected firmware: " + selectedAsset.Name);
            addLog("Found link: " + selectedAsset.Url + "!");
            string fileName = selectedAsset.Name;
            string dir = fm.getFirmwareDir();
            string tg = Path.Combine(dir, fileName);
            addLog("Now will try to download it to " + tg+"!");
            webClient.DownloadFile(selectedAsset.Url, tg);
            if (File.Exists(tg))
            {
                addSuccess("Downloaded and saved "+tg+"!");
                setState("Download ready! You can close this dialog now.", Color.Green);
            }
            else
            {
                setState("Failed to download!", Color.Red);
                addError("Failed to download!");
            }
        }
        List<FirmwareAsset> getLatestReleaseAssets(WebClient webClient, out string tagName, out HashSet<string> uartFlashFileNames)
        {
            tagName = "";
            uartFlashFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string contents = webClient.DownloadString(release_api_url);
            if (contents.Length <= 1)
            {
                setState("Failed to download GitHub API reply, received empty buffer?!", Color.Red);
                addError("Failed to download GitHub API reply, received empty buffer?!");
                return new List<FirmwareAsset>();
            }

            List<FirmwareAsset> assets = new List<FirmwareAsset>();
            using (JsonDocument doc = JsonDocument.Parse(contents))
            {
                JsonElement root = doc.RootElement;
                JsonElement tag;
                if (root.TryGetProperty("tag_name", out tag))
                    tagName = tag.GetString() ?? "";

                JsonElement body;
                if (root.TryGetProperty("body", out body) && body.ValueKind == JsonValueKind.String)
                    uartFlashFileNames = getUartFlashFileNamesFromReleaseBody(body.GetString() ?? "");

                JsonElement jsonAssets;
                if (root.TryGetProperty("assets", out jsonAssets) == false || jsonAssets.ValueKind != JsonValueKind.Array)
                    return assets;

                int index = 0;
                foreach (JsonElement jsonAsset in jsonAssets.EnumerateArray())
                {
                    JsonElement nameElement;
                    JsonElement urlElement;
                    if (jsonAsset.TryGetProperty("name", out nameElement) == false)
                        continue;
                    if (jsonAsset.TryGetProperty("browser_download_url", out urlElement) == false)
                        continue;

                    string name = nameElement.GetString();
                    string url = urlElement.GetString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                        continue;

                    assets.Add(new FirmwareAsset(name, url, index));
                    index++;
                }
            }
            return assets;
        }
        HashSet<string> getUartFlashFileNamesFromReleaseBody(string body)
        {
            HashSet<string> fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(body))
                return fileNames;

            string[] lines = body.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if (line.IndexOf("UART Flash", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                foreach (Match match in Regex.Matches(line, @"Open[^\s\]\)\|]+"))
                    fileNames.Add(match.Value.Trim());
            }
            return fileNames;
        }
        List<FirmwareAsset> getFirmwareCandidatesForPlatform(List<FirmwareAsset> releaseAssets, HashSet<string> uartFlashFileNames)
        {
            List<FirmwareAsset> result = new List<FirmwareAsset>();

            if (bkType == BKType.BekenSPI)
            {
                addLog("Searching for Beken QIO firmware binaries for SPI flashing.");
                foreach (FirmwareAsset asset in releaseAssets)
                {
                    if (bIsBekenSpiFlashableFirmware(asset.Name))
                        result.Add(asset);
                }
                result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return result;
            }

            List<string> prefixes = getFirmwareCandidatePrefixes();
            addLog("Searching for firmware prefix: " + string.Join(", ", prefixes.ToArray()) + "!");

            bool bHaveReleaseBodyUsage = uartFlashFileNames != null && uartFlashFileNames.Count > 0;
            if (bHaveReleaseBodyUsage)
                addLog("Using release asset list, with release table data where available.");
            else
                addWarning("Release body usage table was not available; falling back to filename filtering.");

            foreach (FirmwareAsset asset in releaseAssets)
            {
                if (bIsMainFlashableFirmware(asset.Name, prefixes))
                    result.Add(asset);
            }

            result.Sort((a, b) => compareFirmwareAssets(a, b, uartFlashFileNames));
            return result;
        }
        List<string> getFirmwareCandidatePrefixes()
        {
            List<string> prefixes = new List<string>();
            string pfx = FormMain.getFirmwarePrefix(bkType);
            prefixes.Add(pfx);

            // BK7231M has normal and ALT QIO UART-flashable builds in current OpenBeken releases.
            if (bkType == BKType.BK7231M)
                prefixes.Add("OpenBK7231M_ALT_QIO_");

            return prefixes;
        }
        bool bIsBekenSpiFlashableFirmware(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string lower = fileName.ToLowerInvariant();
            if (lower.StartsWith("openbk") == false)
                return false;
            if (lower.Contains("_qio_") == false)
                return false;
            if (lower.EndsWith(".bin") == false)
                return false;
            if (lower.Contains("_ota"))
                return false;
            if (lower.Contains("_ug_"))
                return false;
            if (lower.Contains("_gz"))
                return false;
            if (lower.Contains("cloudcutter"))
                return false;

            return true;
        }
        bool bIsMainFlashableFirmware(string fileName, List<string> prefixes)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            if (bStartsWithAny(fileName, prefixes) == false)
                return false;

            string lower = fileName.ToLowerInvariant();
            if (lower.EndsWith(".rbl"))
                return false;
            if (lower.EndsWith(".ota"))
                return false;
            if (lower.Contains("_ota"))
                return false;
            if (lower.Contains("_ug_"))
                return false;
            if (lower.Contains("_gz"))
                return false;
            if (lower.Contains("cloudcutter"))
                return false;
            if (lower.EndsWith(".zip"))
                return false;

            if (bIsEspPlatform())
                return lower.EndsWith(".factory.bin");

            if (lower.EndsWith(".bin"))
                return true;
            if (lower.EndsWith(".img"))
                return true;
            if (lower.EndsWith(".fls"))
                return true;

            return false;
        }
        bool bStartsWithAny(string fileName, List<string> prefixes)
        {
            foreach (string pfx in prefixes)
            {
                if (fileName.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        bool bIsEspPlatform()
        {
            switch (bkType)
            {
                case BKType.ESP32:
                case BKType.ESP32S2:
                case BKType.ESP32S3:
                case BKType.ESP32C2:
                case BKType.ESP32C3:
                case BKType.ESP32C5:
                case BKType.ESP32C6:
                case BKType.ESP32C61:
                case BKType.ESP8266:
                    return true;
                default:
                    return false;
            }
        }
        int compareFirmwareAssets(FirmwareAsset a, FirmwareAsset b, HashSet<string> uartFlashFileNames)
        {
            int ar = getFirmwareSortRank(a, uartFlashFileNames);
            int br = getFirmwareSortRank(b, uartFlashFileNames);
            if (ar != br)
                return ar.CompareTo(br);

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }
        int getFirmwareSortRank(FirmwareAsset asset, HashSet<string> uartFlashFileNames)
        {
            if (bIsCurrentDefaultFirmwareName(asset.Name))
                return 0;
            if (uartFlashFileNames != null && uartFlashFileNames.Contains(asset.Name))
                return 1;
            return 2;
        }
        bool bIsCurrentDefaultFirmwareName(string fileName)
        {
            string pfx = FormMain.getFirmwarePrefix(bkType);
            if (fileName.StartsWith(pfx, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            string rest = fileName.Substring(pfx.Length);
            int dot = rest.LastIndexOf('.');
            if (dot < 0)
                return false;

            string withoutExtension = rest.Substring(0, dot);
            return withoutExtension.IndexOf('_') < 0;
        }
        FirmwareAsset pickDefaultFirmwareAsset(List<FirmwareAsset> candidates, HashSet<string> uartFlashFileNames)
        {
            if (candidates == null || candidates.Count <= 0)
                return null;

            FirmwareAsset defaultAsset = null;
            foreach (FirmwareAsset candidate in candidates)
            {
                if (bIsCurrentDefaultFirmwareName(candidate.Name))
                {
                    defaultAsset = candidate;
                    break;
                }
            }
            if (defaultAsset == null && uartFlashFileNames != null)
            {
                foreach (FirmwareAsset candidate in candidates)
                {
                    if (uartFlashFileNames.Contains(candidate.Name))
                    {
                        defaultAsset = candidate;
                        break;
                    }
                }
            }
            if (defaultAsset == null)
                defaultAsset = candidates[0];

            if (bkType != BKType.BekenSPI)
                defaultAsset.bIsDefault = true;
            return defaultAsset;
        }
        FirmwareAsset chooseFirmwareAssetIfRequired(List<FirmwareAsset> candidates, FirmwareAsset defaultAsset)
        {
            if (candidates == null || candidates.Count <= 0)
                return null;
            if (candidates.Count == 1)
                return defaultAsset;

            FirmwareAsset selected = null;
            Singleton.Invoke((MethodInvoker)delegate {
                selected = chooseFirmwareAssetOnUiThread(candidates, defaultAsset);
            });
            return selected;
        }
        FirmwareAsset chooseFirmwareAssetOnUiThread(List<FirmwareAsset> candidates, FirmwareAsset defaultAsset)
        {
            if (bkType == BKType.BekenSPI)
                return showFirmwareVariantDialog(candidates, defaultAsset);

            DialogResult res = showFirmwareVariantOfferDialog(defaultAsset);
            if (res == DialogResult.Cancel)
                return null;
            if (res == DialogResult.No)
                return defaultAsset;

            return showFirmwareVariantDialog(candidates, defaultAsset);
        }
        DialogResult showFirmwareVariantOfferDialog(FirmwareAsset defaultAsset)
        {
            using (Form f = new Form())
            using (Label label = new Label())
            using (Button buttonYes = new Button())
            using (Button buttonNo = new Button())
            using (Button buttonCancel = new Button())
            {
                f.Text = "Firmware variants available";
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.ClientSize = new Size(460, 165);
                f.ShowIcon = false;

                label.Left = 12;
                label.Top = 12;
                label.Width = 436;
                label.Height = 105;
                label.Text = "Firmware variants are available for " + bkType + "." + Environment.NewLine + Environment.NewLine +
                    "Default:" + Environment.NewLine +
                    defaultAsset.Name + Environment.NewLine + Environment.NewLine +
                    "Do you want to choose a different variant?";

                buttonYes.Text = "Choose variant";
                buttonYes.Left = 48;
                buttonYes.Top = 122;
                buttonYes.Width = 120;
                buttonYes.DialogResult = DialogResult.Yes;

                buttonNo.Text = "Download default";
                buttonNo.Left = 178;
                buttonNo.Top = 122;
                buttonNo.Width = 122;
                buttonNo.DialogResult = DialogResult.No;

                buttonCancel.Text = "Cancel";
                buttonCancel.Left = 312;
                buttonCancel.Top = 122;
                buttonCancel.Width = 100;
                buttonCancel.DialogResult = DialogResult.Cancel;

                f.Controls.Add(label);
                f.Controls.Add(buttonYes);
                f.Controls.Add(buttonNo);
                f.Controls.Add(buttonCancel);
                f.AcceptButton = buttonNo;
                f.CancelButton = buttonCancel;

                return f.ShowDialog(this);
            }
        }
        FirmwareAsset showFirmwareVariantDialog(List<FirmwareAsset> candidates, FirmwareAsset defaultAsset)
        {
            using (Form f = new Form())
            using (Label label = new Label())
            using (LinkLabel linkLabel = new LinkLabel())
            using (ListBox listBox = new ListBox())
            using (Button buttonDownload = new Button())
            using (Button buttonCancel = new Button())
            {
                f.Text = bkType == BKType.BekenSPI ? "Choose Beken SPI firmware" : "Choose firmware variant";
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.ClientSize = new Size(560, 300);

                label.Left = 12;
                label.Top = 12;
                label.Width = 536;
                label.Height = 40;
                if (bkType == BKType.BekenSPI)
                    label.Text = "Choose a Beken SPI firmware file.";
                else
                    label.Text = "Choose a firmware variant for " + bkType + ". If unsure, keep the default.";

                listBox.Left = 12;
                listBox.Top = 60;
                listBox.Width = 536;
                listBox.Height = 185;
                listBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;

                linkLabel.Left = 12;
                linkLabel.Top = 264;
                linkLabel.Width = 260;
                linkLabel.Height = 20;
                linkLabel.Text = "Review enabled features for each platform";
                linkLabel.LinkClicked += (s, e) =>
                {
                    System.Diagnostics.Process.Start(enabledFeaturesUrl);
                };

                FirmwareChoiceListItem defaultItem = null;
                foreach (FirmwareAsset candidate in candidates)
                {
                    FirmwareChoiceListItem item = new FirmwareChoiceListItem(candidate);
                    listBox.Items.Add(item);
                    if (candidate == defaultAsset)
                        defaultItem = item;
                }
                if (defaultItem != null)
                    listBox.SelectedItem = defaultItem;
                else if (listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;

                buttonDownload.Text = "Download selected";
                buttonDownload.Left = 318;
                buttonDownload.Top = 260;
                buttonDownload.Width = 110;
                buttonDownload.DialogResult = DialogResult.OK;

                buttonCancel.Text = "Cancel";
                buttonCancel.Left = 438;
                buttonCancel.Top = 260;
                buttonCancel.Width = 110;
                buttonCancel.DialogResult = DialogResult.Cancel;

                f.Controls.Add(label);
                f.Controls.Add(linkLabel);
                f.Controls.Add(listBox);
                f.Controls.Add(buttonDownload);
                f.Controls.Add(buttonCancel);
                f.AcceptButton = buttonDownload;
                f.CancelButton = buttonCancel;

                listBox.DoubleClick += (s, e) =>
                {
                    if (listBox.SelectedItem != null)
                        f.DialogResult = DialogResult.OK;
                };

                if (f.ShowDialog(this) != DialogResult.OK)
                    return null;

                FirmwareChoiceListItem selectedItem = listBox.SelectedItem as FirmwareChoiceListItem;
                if (selectedItem == null)
                    return null;
                return selectedItem.Asset;
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

        class FirmwareAsset
        {
            public readonly string Name;
            public readonly string Url;
            public readonly int ReleaseAssetIndex;
            public bool bIsDefault;

            public FirmwareAsset(string name, string url, int releaseAssetIndex)
            {
                Name = name;
                Url = url;
                ReleaseAssetIndex = releaseAssetIndex;
            }
        }
        class FirmwareChoiceListItem
        {
            public readonly FirmwareAsset Asset;

            public FirmwareChoiceListItem(FirmwareAsset asset)
            {
                Asset = asset;
            }

            public override string ToString()
            {
                if (Asset.bIsDefault)
                    return Asset.Name + "  (default)";
                return Asset.Name;
            }
        }
    }
}
