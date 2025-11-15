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
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*",
				Title = "Select a BIN file",
				Multiselect = false,
			};

			if(openFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			lblFirmware.Text = openFileDialog.FileName;
			lblFirmware.Visible = true;
		}

		private async void OnBtnDecryptClick(object sender, EventArgs e)
		{
			await Task.Delay(1); // just so that ui thread won't freeze
			if(string.IsNullOrWhiteSpace(lblFirmware.Text))
			{
				AddDecryptionLogLine($"Firmware is not selected", Color.Red);
				return;
			}

			var bytes = File.ReadAllBytes(lblFirmware.Text);
			if(bytes.Length < 0xBA00)
				return;
			var bootloader = new byte[bytes.Length > 0x10000 ? 0x10000 : bytes.Length];
			Array.Copy(bytes, bootloader, bootloader.Length);
			var decrc = Utils.UnCRC(bootloader);
			if(decrc.Length < 27168) // 27168 - tuya bk7252 bootloader
			{
				if(decrc.Length == 0)
				{
					AddDecryptionLogLine($"Already decrc'ed or not a BK72xx binary.", Color.Red);
					return;
				}
				AddDecryptionLogLine($"Decrc'ed binary too short: {decrc.Length}", Color.Red);
				return;
			}
			var imageU32 = Utils.U8ToU32(decrc);
			var keys = new List<Tuple<uint, uint>>();
			foreach(var str in Utils.BootloaderDict)
			{
				var search = Encoding.ASCII.GetBytes(str);
				var head = search.Take(4).ToArray();
				var matcher = Utils.XorIter(search.Skip(4).ToArray(), search);
				var selectorValues = new List<byte?> { 0, 1, 2, 3 }
				   .Select(x => x)
				   .Concat(new List<byte?> { null })
				   .ToArray();

				foreach(var sel1 in selectorValues)
					foreach(var sel2 in selectorValues)
						foreach(var sel3 in selectorValues)
						{
							var selectors = new byte?[] { sel1, sel2, sel3 };

							var keystream = Beken_Crypto.Keystream(selectors, 0).Take(imageU32.Count).ToArray();
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
								var decrypted = new List<uint>();
								var crypto = new BekenCrypto(new uint[] { 0, 0, k, settings });
								uint enaddr = 0;
								for(int en = 0; en < imageU32.Count; en++)
								{
									decrypted.Add(crypto.EncryptU32(enaddr, imageU32[en]));
									enaddr += 4;
								}
								if(Utils.VerifyDecrypt(imageU32, decrypted.ToArray(), out var decrKeys, out var keysAddress))
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
			if(string.IsNullOrWhiteSpace(lblFirmware.Text))
			{
				AddDecryptionLogLine($"Firmware is not selected", Color.Red);
				return;
			}
			var data = File.ReadAllBytes(lblFirmware.Text);
			if(data.Length < 0x10000)
			{
				AddDecryptionLogLine($"Firmware is too short", Color.Red);
				return;
			}
			var bootloader = new byte[0x10000];
			Array.Copy(data, bootloader, bootloader.Length);
			var decrc = Utils.UnCRC(bootloader);
			if(decrc.Length < 50000)
			{
				if(decrc.Length == 0)
				{
					AddDecryptionLogLine($"Not a BK72xx binary.", Color.Red);
					return;
				}
				AddDecryptionLogLine($"Decrc'ed binary too short: {decrc.Length}", Color.Red);
				return;
			}
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var decr = Utils.EncryptDecryptBekenFW(keys, decrc, 0);
			File.WriteAllBytes(lblFirmware.Text + ".bootloader.decr.bin", decr);

			AddDecryptionLogLine($"Decrypted bootloader path: {lblFirmware.Text}.bootloader.decr.bin", Color.Green);
		}

		private void OnBtnDecryptFirmwareClick(object sender, EventArgs e)
		{
			if(string.IsNullOrWhiteSpace(lblFirmware.Text))
			{
				AddDecryptionLogLine($"Firmware is not selected", Color.Red	);
				return;
			}
			var data = File.ReadAllBytes(lblFirmware.Text);
			if(data.Length < 0x121000)
			{
				AddDecryptionLogLine($"Firmware is too short", Color.Red);
				return;
			}
			var fw = new byte[0x121000];
			Array.Copy(data, 0x11000, fw, 0, fw.Length);
			var decrc = Utils.UnCRC(fw);
			if(decrc.Length < 50000)
			{
				if(decrc.Length == 0)
				{
					AddDecryptionLogLine($"Not a BK72xx binary.", Color.Red);
					return;
				}
				AddDecryptionLogLine($"Decrc'ed binary too short: {decrc.Length}", Color.Red);
				return;
			}
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var decr = Utils.EncryptDecryptBekenFW(keys, decrc, 0x10000);
			File.WriteAllBytes(lblFirmware.Text + ".fw.decr.bin", decr);

			AddDecryptionLogLine($"Decrypted firmware path: {lblFirmware.Text}.fw.decr.bin", Color.Green);
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
			var partitions = Convert.FromBase64String(FLoaders.BK7231N_1_0_1_partitions);
			var combined = new List<byte>();
			combined.AddRange(encr);
			combined.AddRange(partitions);
			var data = combined.ToArray();
			var fixup = new byte[] { (byte)'B', (byte)'K', (byte)'7', (byte)'2', (byte)'3', (byte)'1', 0, 0 };
			Array.Copy(fixup, 0, data, 0x100, fixup.Length);
			var crc = Utils.CRCBeken(data);
			File.WriteAllBytes($"bootloader_1.0.1_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin", crc);

			AddDecryptionLogLine($"Encrypted bootloader path: {Directory.GetCurrentDirectory()}/bootloader_1.0.1_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin", Color.Green);
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
			var encr = Utils.EncryptDecryptBekenFW(keys, uintdata.ToArray(), 0);
			var partitions = Convert.FromBase64String(FLoaders.BK7231N_1_0_1_partitions);
			var combined = new List<byte>();
			combined.AddRange(encr);
			combined.AddRange(partitions);
			var data = combined.ToArray();
			var fixup = new byte[] { (byte)'B', (byte)'K', (byte)'7', (byte)'2', (byte)'3', (byte)'1', 0, 0 };
			Array.Copy(fixup, 0, data, 0x100, fixup.Length);
			//data[0x1F00] = 0xd7;
			//data[0x1F01] = 0x13;
			//data[0x1F02] = 0x00;
			//data[0x1F03] = 0xEA;
			var crc = Utils.CRCBeken(data);
			File.WriteAllBytes($"bootloader_1.0.13_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin", crc);

			AddDecryptionLogLine($"Encrypted bootloader path: {Directory.GetCurrentDirectory()}/bootloader_1.0.13_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.bin", Color.Green);
		}

		private void OnBtnEncryptFirmwareClick(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*",
				Title = "Select a BIN file",
				Multiselect = false,
			};

			if(openFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			var data = File.ReadAllBytes(openFileDialog.FileName);
			var uintdata = Utils.U8ToU32(data);
			var keys = new uint[] { Convert.ToUInt32(numCoeff1.Text, 16), Convert.ToUInt32(numCoeff2.Text, 16), Convert.ToUInt32(numCoeff3.Text, 16), Convert.ToUInt32(numCoeff4.Text, 16) };
			var encr = Utils.EncryptDecryptBekenFW(keys, uintdata.ToArray(), 0x10000);
			var crc = Utils.CRCBeken(encr);
			File.WriteAllBytes($"firmwares/{openFileDialog.SafeFileName}_UA_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.0x11000.bin", crc);

			AddDecryptionLogLine($"firmwares/{openFileDialog.SafeFileName}_UA_{keys[0]:X}_{keys[1]:X}_{keys[2]:X}_{keys[3]:X}.0x11000.bin", Color.Green);
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
