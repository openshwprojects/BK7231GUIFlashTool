using System;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        private TuyaConfig _lastTuyaConfig;

        // Used to ignore stale background renders of enhanced output.
        private int _tuyaEnhancedRenderSeq = 0;

                
private async Task SetTextBoxTextChunkedAsync(TextBox box, string text, int seq)
{
    // Avoid freezing the UI when the enhanced output is very large.
    // For typical small outputs, a direct assignment is fine.
    if (text == null) text = "";

    const int ChunkSize = 32 * 1024; // 32KB
    const int DirectSetThreshold = 256 * 1024; // 256KB

    if (text.Length <= DirectSetThreshold)
    {
        box.Text = text;
        return;
    }

    box.Clear();

    for (int i = 0; i < text.Length; i += ChunkSize)
    {
        if (seq != _tuyaEnhancedRenderSeq)
            return;

        int len = Math.Min(ChunkSize, text.Length - i);
        box.AppendText(text.Substring(i, len));

        // Yield back to the message pump between chunks.
        await Task.Yield();
    }
}


private void updateTuyaConfigOutput()
        {
            var tc = _lastTuyaConfig;
            if (tc == null)
                return;

            // Text description box: pin/module translation derived from Tuya keys.
            // In enhanced mode we prefer translation based on the enhanced KV view (even if checksums are bad),
            // but fall back to the classic extraction when the enhanced view cannot recover any meaningful pins.
            textBoxTuyaCFGText.Text = checkBoxTuyaCfgEnhanced.Checked
                ? tc.getKeysHumanReadableEnhanced()
                : tc.getKeysHumanReadable();

            if (!checkBoxTuyaCfgEnhanced.Checked)
            {
                // Cancel any in-flight enhanced render.
                _tuyaEnhancedRenderSeq++;
                textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();
                return;
            }

            // Show the base JSON quickly, then compute enhanced output off the UI thread.
            int seq = ++_tuyaEnhancedRenderSeq;
            textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();

            Task.Run(() => tc.getEnhancedExtractionText())
                .ContinueWith(t =>
                {
                    if (seq != _tuyaEnhancedRenderSeq)
                        return;

                    string result = null;
                    if (t.Status == TaskStatus.RanToCompletion)
                        result = t.Result;
                    else
                        result = tc.getKeysAsJSON();

                    
                    if (string.IsNullOrWhiteSpace(result))
                        result = tc.getKeysAsJSON();

try
                    {
                        if (IsDisposed)
                            return;
                        BeginInvoke((Action)(async () =>
                        {
                            if (seq != _tuyaEnhancedRenderSeq)
                                return;
                            await SetTextBoxTextChunkedAsync(textBoxTuyaCFGJSON, result, seq);
                        }));
                    }
                    catch
                    {
                        // ignore
                    }
                });
        }


        private void checkBoxTuyaCfgEnhanced_CheckedChanged(object sender, EventArgs e)
        {
            updateTuyaConfigOutput();
        }

        private void tabPage2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void tabPage2_DragDrop(object sender, DragEventArgs e)
        {
            textBoxTuyaCFGJSON.Text = "";
            textBoxTuyaCFGText.Text = "";
            _lastTuyaConfig = null;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
				importTuyaConfig(file);
			}
		}
        private void buttonImportConfigFileDialog_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set the file dialog properties
            //openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string selectedFile = openFileDialog1.FileName;

                    // Call the importTuyaConfig method with the selected file
                    importTuyaConfig(selectedFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
        }

        public void importTuyaConfig(string file) 
        {
            try
            {
                // Do something with the dropped file(s)
                TuyaConfig tc = new TuyaConfig();
                if (tc.fromFile(file) == false)
                {
                    if (tc.extractKeys() == false)
                    {
                        _lastTuyaConfig = tc;
                        updateTuyaConfigOutput();
                    }
                    else
                    {
                        MessageBox.Show("Failed to extract keys");
                    }
                }
                else
                {
                    if(tc.isLastBinaryOBKConfig())
                    {
                        MessageBox.Show("The file you've dragged looks like OBK config, not a Tuya one.");
                    }
                    else if (tc.isLastBinaryFullOf0xff())
                    {
                        MessageBox.Show("Failed, it seems that given binary is an erased flash sector, full of 0xFF");
                    }
                    else
                    {
                        MessageBox.Show("Failed, see log for more details");
                    }
                }
            }
            catch (Exception ex)
            {
                textBoxTuyaCFGText.Text = "Sorry, exception occurred: " + ex.ToString();
            }
        }
    }
}
