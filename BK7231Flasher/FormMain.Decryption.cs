using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BK7231Flasher
{
	public partial class FormMain
	{
		private void AddDecryptionLog(string s, Color col)
		{
			Singleton.textBoxDecryptLog.Invoke((MethodInvoker)delegate {
				// Running on the UI thread
				RichTextUtil.AppendText(Singleton.textBoxDecryptLog, s, col);
			});
		}

		private void AddDecryptionLogLine(string s, Color col) => AddDecryptionLog(s + Environment.NewLine, col);

		private void OnSelectFirmwareClick(object sender, EventArgs e)
		{
			lblFirmware.Text = OpenFile(out _) ?? lblFirmware.Text;
			lblFirmware.Visible = true;
		}

		private static string OpenFile(out string safeFileName)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*",
				Title = "Select a BIN file",
				Multiselect = false,
			};

			if(openFileDialog.ShowDialog() != DialogResult.OK)
			{
				safeFileName = null;
				return null;
			}
			safeFileName = openFileDialog.SafeFileName;
			return openFileDialog.FileName;
		}

		private bool CheckDecrcLength(byte[] data, int len)
		{
			if(data == null) return false;
			if(data.Length < len)
			{
				if(data.Length == 0)
				{
					AddDecryptionLogLine($"Already uncrc'ed or not a BK72xx binary.", Color.Red);
					return false;
				}
				AddDecryptionLogLine($"Uncrc'ed binary too short: {data.Length}", Color.Red);
				return false;
			}
			return true;
		}

		private byte[] LoadFirmware(int startAddr = 0, int maxLength = 0, bool skipCrc = false)
		{
			if(string.IsNullOrWhiteSpace(lblFirmware.Text))
			{
				AddDecryptionLogLine($"Firmware is not selected", Color.Red);
				return null;
			}
			var bytes = File.ReadAllBytes(lblFirmware.Text);
			maxLength = (maxLength == 0 ? bytes.Length : maxLength) - startAddr;
			var data = new byte[bytes.Length - startAddr > maxLength ? maxLength : bytes.Length - startAddr];
			Array.Copy(bytes, startAddr, data, 0, data.Length);
			return skipCrc ? data : Utils.UnCRC(data);
		}

		private async void OnBtnDecryptClick(object sender, EventArgs e)
		{
			await Task.Delay(1); // dummy
			var decrc = LoadFirmware(0, 0x10000, chkSkipDecrc.Checked);
			if(!CheckDecrcLength(decrc, 27168)) // 27168 - tuya bk7252 bootloader
			{
				return;
			}
			if(decrc.AsSpan().IndexOf(Encoding.ASCII.GetBytes(Utils.BootloaderDict[0])) > 0)
			{
				AddDecryptionLogLine($"Firmware built with zero keys.", Color.Green);
				numCoeff1.Text = "0";
				numCoeff2.Text = "0";
				numCoeff3.Text = "0";
				numCoeff4.Text = "0";
				return;
			}
			var imageU32 = Utils.U8ToU32(decrc);
			var keys = new List<Tuple<uint, uint>>();
			foreach(var str in Utils.BootloaderDict)
			{
				var search = Encoding.ASCII.GetBytes(str);
				var head = search.Take(4).ToArray();
				var matcher = Utils.XorIter(search.Skip(4).ToArray(), search);
				var selectorValues = new List<byte?> { 0, 1, 2, 3, null };

				foreach(var sel1 in selectorValues)
				foreach(var sel2 in selectorValues)
				foreach(var sel3 in selectorValues)
				{
					var selectors = new byte?[] { sel1, sel2, sel3 };

					var keystream = Beken_Crypto.Keystream(selectors, 0).Take(imageU32.Length).ToArray();
					var xoredImage = Utils.XorIter(imageU32, keystream);

					var preprocImage = Utils.U32ToU8(Utils.XorIter(xoredImage, xoredImage.Skip(1)));
					var imageBytes = Utils.U32ToU8(xoredImage);

					uint settings = Beken_Crypto.FormatSettingsWord(selectors);
					foreach(int hit in Utils.FindAll(preprocImage, matcher.ToArray()))
					{
						var keyPart = imageBytes.Skip(hit).Take(4).ToArray();
						var key = Utils.XorIter(keyPart, head).ToArray();
						uint k = BitConverter.ToUInt32(key, 0);
						k = Utils.RotateLeft(k, (hit % 4) * 8);
						keys.Add(new Tuple<uint, uint>(k, settings));
						AddDecryptionLogLine($"Found match at 0x{hit:X} with key: 0 0 {k:X} {settings:X}", Color.Black);
						var decrypted = new uint[imageU32.Length];
						var crypto = new BekenCrypto(new uint[] { 0, 0, k, settings });
						uint enaddr = 0;
						for(int en = 0; en < imageU32.Length; en++)
						{
							decrypted[en] = crypto.EncryptU32(enaddr, imageU32[en]);
							enaddr += 4;
						}
						if(Utils.VerifyDecrypt(imageU32, decrypted, out var decrKeys, out var keysAddress))
						{
							AddDecryptionLogLine($"Decrypt combination: 0 0 {k:X} {settings:X}", Color.Black);
							AddDecryptionLogLine($"Bootloader key: {decrKeys[0]:X} {decrKeys[1]:X} {decrKeys[2]:X} {decrKeys[3]:X} at 0x{keysAddress:X}", Color.Green);
							if(k == 0)
								AddDecryptionLogLine($"Coeff3 in decrypt combination is zero, firmware is most likely unencrypted, in which case bootloader key is incorrect.", Color.Orange);
							numCoeff1.Text = $"{decrKeys[0]:X}";
							numCoeff2.Text = $"{decrKeys[1]:X}";
							numCoeff3.Text = $"{decrKeys[2]:X}";
							numCoeff4.Text = $"{decrKeys[3]:X}";
							return;
						}
					}
				}
			}
			if(keys.Count > 0)
			{
				var mostCommonCoeff = keys
					.GroupBy(kv => kv)
					.OrderByDescending(g => g.Count())
					.First().Key;
				AddDecryptionLogLine($"Most common decrypt combination: 0 0 {mostCommonCoeff.Item1:X} {mostCommonCoeff.Item2:X}", Color.Orange);
				if(mostCommonCoeff.Item1 == 0)
					AddDecryptionLogLine("Coeff3 in most common decrypt combination is zero, already unencrypted?", Color.Orange);
				numCoeff1.Text = $"0";
				numCoeff2.Text = $"0";
				numCoeff3.Text = $"{mostCommonCoeff.Item1:X}";
				numCoeff4.Text = $"{mostCommonCoeff.Item2:X}";
				AddDecryptionLogLine($"Failed to get keys from bootloader", Color.Red);
			}
			else
			{
				AddDecryptionLogLine($"Failed to get decrypt combination", Color.Orange);
			}
		}

		private void OnBtnDecryptBootloaderClick(object sender, EventArgs e)
		{
			var decrc = LoadFirmware(0, 0x10000, chkSkipDecrc.Checked);
			if(!CheckDecrcLength(decrc, 27168))
			{
				return;
			}
			_ = ParsePartitions(decrc, true, out var toRemove);
			var final = new byte[decrc.Length - toRemove];
			Array.Copy(decrc, final, final.Length);
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var decr = Utils.EncryptDecryptBekenFW(keys, final, 0);
			var name = lblFirmware.Text + ".bootloader.decr.bin";
			File.WriteAllBytes(name, decr);

			AddDecryptionLogLine($"Decrypted bootloader path: {name}", Color.Green);
		}

		private void OnBtnDecryptFirmwareClick(object sender, EventArgs e)
		{
			var decrc = LoadFirmware(0x11000, 0, chkSkipDecrc.Checked);
			if(!CheckDecrcLength(decrc, 27168))
			{
				return;
			}
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var decr = Utils.EncryptDecryptBekenFW(keys, decrc, 0x10000);
			var name = lblFirmware.Text + ".fw.decr.bin";
			File.WriteAllBytes(name, decr);

			AddDecryptionLogLine($"Decrypted firmware path: {name}", Color.Green);
		}

		private void OnBtnEncryptBootloaderClick(object sender, EventArgs e)
		{
			var gzbl = new MemoryStream(Convert.FromBase64String(FLoaders.BK7231N_1_0_1_gz));
			var gzip = new GZipStream(gzbl, CompressionMode.Decompress);
			var stre = new MemoryStream();
			gzip.CopyTo(stre);
			var bootloader = stre.ToArray();
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			for(int i = 0x48, k = 0; i < 0x57; i += 4, k++)
			{
				Array.Copy(Utils.U32ToU8(new List<uint> { keys[k] }), 0, bootloader, i, 4);
			}
			var uintdata = Utils.U8ToU32(bootloader);
			var encr = Utils.EncryptDecryptBekenFW(keys, uintdata.ToArray(), 0);
			var partitions = GetPartitions(true);
			if(partitions == null)
				return;
			var combined = new List<byte>();
			combined.AddRange(encr);
			combined.AddRange(partitions);
			var data = combined.ToArray();
			var fixup = new byte[] { (byte)'B', (byte)'K', (byte)'7', (byte)'2', (byte)'3', (byte)'1', 0, 0 };
			Array.Copy(fixup, 0, data, 0x100, fixup.Length);
			var crc = Utils.CRCBeken(data);
			var name = $"bootloader_1.0.1_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin";
			File.WriteAllBytes(name, crc);

			AddDecryptionLogLine($"Encrypted bootloader path: {Directory.GetCurrentDirectory()}/{name}", Color.Green);
		}

		private void OnBtnEncryptBKNBootloaderClick(object sender, EventArgs e)
		{
			// uascent bootloader, common versions don't encrypt OTA
			var gzbl = new MemoryStream(Convert.FromBase64String(FLoaders.BK7231N_1_0_13_gz));
			var gzip = new GZipStream(gzbl, CompressionMode.Decompress);
			var stre = new MemoryStream();
			gzip.CopyTo(stre);
			var bootloader = stre.ToArray();
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			for(int i = 0x1F08, k = 0; i < 0x1F17; i += 4, k++)
			{
				Array.Copy(Utils.U32ToU8(new List<uint> { keys[k] }), 0, bootloader, i, 4);
			}
			var uintdata = Utils.U8ToU32(bootloader);
			var encr = Utils.EncryptDecryptBekenFW(keys, uintdata, 0);
			var partitions = GetPartitions();
			if(partitions == null)
				return;
			var combined = new List<byte>();
			combined.AddRange(encr);
			combined.AddRange(partitions);
			var data = combined.ToArray();
			var fixup = new byte[] { (byte)'B', (byte)'K', (byte)'7', (byte)'2', (byte)'3', (byte)'1', 0, 0 };
			Array.Copy(fixup, 0, data, 0x100, fixup.Length);
			var crc = Utils.CRCBeken(data);
			var name = $"bootloader_1.0.13_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin";
			File.WriteAllBytes(name, crc);

			AddDecryptionLogLine($"Encrypted bootloader path: {Directory.GetCurrentDirectory()}/{name}", Color.Green);
		}

		private void OnBtnEncryptFirmwareClick(object sender, EventArgs e)
		{
			var fileName = OpenFile(out var safeFileName);
			if(fileName == null) return;
			var data = File.ReadAllBytes(fileName);
			var uintdata = Utils.U8ToU32(data);
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var encr = Utils.EncryptDecryptBekenFW(keys, uintdata, 0x10000);
			var crc = Utils.CRCBeken(encr);
			var name = $"firmwares/{safeFileName}_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.0x11000.bin";
			File.WriteAllBytes(name, crc);

			AddDecryptionLogLine(name, Color.Green);
		}

		private void OnBtnLoadPartitionsClick(object sender, EventArgs e)
		{
			var decrc = LoadFirmware(0, 0x10000, chkSkipDecrc.Checked);
			if(!CheckDecrcLength(decrc, 512))
			{
				return;
			}
			var falPartitions = ParsePartitions(decrc, false, out _);
			if(falPartitions.Count == 0)
			{
				AddDecryptionLogLine("Loading partitions failed!", Color.Red);
				return;
			}
			AddDecryptionLogLine("Partitions loaded!", Color.Green);
			falPartitions.Reverse();
			dgPartitions.Rows.Clear();
			foreach(var partition in falPartitions)
			{
				dgPartitions.Rows.Add(new string[] { partition.name, partition.flash_name, $"0x{partition.offset:X}", $"0x{partition.len:X}" });
			}
		}

		private List<FALPartition64> ParsePartitions(byte[] bootloader, bool noLog, out int partitionsLength)
		{
			partitionsLength = 0;
			var imageU32 = Utils.U8ToU32(bootloader);
			var falPartitions = new List<FALPartition64>();
			var limit = imageU32.Length - 1 - 16 * 10;
			limit = limit < 0 ? 0 : limit;
			for(int i = imageU32.Length - 1; i > limit; i--)
			{
				if(imageU32[i] == Utils.FAL_PART_MAGIC_WORD)
				{
					if(i > imageU32.Length - 16)
					{
						partitionsLength += (imageU32.Length - i) * 4;
						if(!noLog) AddDecryptionLogLine($"FAL magic found, but length is {(imageU32.Length - i) * 4} instead of 64, skipping...", Color.Orange);
						continue;
					}
					var data = new uint[16];
					Array.Copy(imageU32, i, data, 0, data.Length);
					var bytedata = Utils.U32ToU8(data);
					var partition = Utils.BytesToFAL64(bytedata);
					if(partition.crc32 == 0 || partition.crc32 == (CRC.crc32_ver2(0xFFFFFFFF, bytedata, 60) ^ 0xFFFFFFFF))
					{
						if(partition.crc32 == 0)
							if(!noLog) AddDecryptionLogLine($"Partition {partition.name} has zeroed CRC32! Ignore if Tuya/1.0.1 bootloader", Color.Orange);
						falPartitions.Add(partition);
					}
					else
					{
						if(!noLog) AddDecryptionLogLine($"Partition {partition.name} has incorrect CRC32! Skipping...", Color.Red);
					}
					partitionsLength += 64;
				}
			}
			return falPartitions;
		}

		private void OnBtnLoadDefaultNPartitionsClick(object sender, EventArgs e)
		{
			dgPartitions.Rows.Clear();
			dgPartitions.Rows.Add(new string[] { "bootloader", "beken_onchip_crc", $"0x{0:X}",       $"0x{65536:X}" });
			dgPartitions.Rows.Add(new string[] { "app",        "beken_onchip_crc", $"0x{65536:X}",   $"0x{1083136:X}" });
			dgPartitions.Rows.Add(new string[] { "download",   "beken_onchip",     $"0x{1220608:X}", $"0x{679936:X}" });
		}

		private byte[] GetPartitions(bool nocrc = false)
		{
			var falPartitions = new List<byte>();
			try
			{
				foreach(DataGridViewRow row in dgPartitions.Rows)
				{
					if(row.IsNewRow)
						continue;
					var part = new FALPartition64
					{
						magic_word = Utils.FAL_PART_MAGIC_WORD,
						name = (string)row.Cells[0].Value,
						flash_name = (string)row.Cells[1].Value,
						crc32 = 0,
					};
					if(((string)row.Cells[2].Value).StartsWith("0x"))
					{
						part.offset = Convert.ToUInt32((string)row.Cells[2].Value, 16);
					}
					else
					{
						part.offset = Convert.ToUInt32((string)row.Cells[2].Value);
					}
					if(((string)row.Cells[3].Value).StartsWith("0x"))
					{
						part.len = Convert.ToUInt32((string)row.Cells[3].Value, 16);
					}
					else
					{
						part.len = Convert.ToUInt32((string)row.Cells[3].Value);
					}
					if(!nocrc)
					{
						var array = Utils.FAL64ToBytes(part);
						var skipCrc = new byte[array.Length - 4];
						Array.Copy(array, skipCrc, skipCrc.Length);
						part.crc32 = CRC.crc32_ver2(0xFFFFFFFF, skipCrc) ^ 0xFFFFFFFF;
					}
					var partition = Utils.FAL64ToBytes(part);
					falPartitions.AddRange(partition);
				}
				if(falPartitions.Count == 0)
					throw new Exception("Configure partitions first!");
				return falPartitions.ToArray();
			}
			catch(Exception ex)
			{
				AddDecryptionLogLine("Failed to parse partitions!", Color.Red);
				AddDecryptionLogLine(ex.Message, Color.Red);
			}
			return null;
		}

		private void OnNumCoeffKeyPress(object sender, KeyEventArgs e)
		{
			var control = (TextBox)sender;
			if(((e.KeyValue < 48) || (e.KeyValue > 57)) &&
				((e.KeyValue < 65) || (e.KeyValue > 70)) &&
				((e.KeyValue < 97) || (e.KeyValue > 102)) &&
				(e.KeyValue != (char)Keys.Back) &&
				((e.KeyValue != (char)Keys.Delete) || (e.KeyValue == '.')))
			{
				e.Handled = true;
			}
			e.SuppressKeyPress = control.Text.Length >= 8 ? e.KeyCode != Keys.Back : e.Handled;
			if(e.Modifiers == Keys.Control && e.KeyCode == Keys.V)
			{
				try
				{
					var t = Clipboard.GetText().Trim(' ');
					control.Text = Convert.ToUInt32(t, 16).ToString("X");
				}
				catch { }
			}
		}
	}
}
