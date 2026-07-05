using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
	public class RDAFlasher : BaseFlasher, IRomReadFlasher
	{
		MemoryStream ms;
		int flashSizeMB = 1;
		readonly int FLASH_MMAP_BASE = 0x18000000;
		readonly int DumpAmount = 0x1000;
		const int RdaRomBase = 0x00000000;
		const int RdaRomSize = 0x00010000;
		const int RdaEfuseStubLoadAddress = 0x00100000;
		const int RdaEfuseStubEntryAddress = 0x00100001;
		const int RdaEfuseRawSize = 0x20;
		const int RdaEfuseStubReadyTimeoutMs = 5000;
		const int RdaEfuseCommandTimeoutMs = 5000;
		const int RdaEfusePostGoProbeTimeoutMs = 1200;
		const int RdaEfuseStubVerifyBytes = 0x00;
		const string RdaEfuseStubResource = "RDA5981_EFuse_Stub";
		const string RdaEfuseReadyText = "RDAEFUSE ready";
		const string RdaEfuseReadyToken = "RDAEFUSE";
		const string RdaEfusePromptText = "> ";

		public RDAFlasher(CancellationToken ct) : base(ct)
		{
		}

		bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				serial = new SerialPort(serialName, 921600);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				serial.ReadTimeout = 1000;
				xm = new XMODEM(serial, XMODEM.Variants.XModemCRC, 0xFF)
				{
					SendInactivityTimeoutMillisec = 15000,
					MaxSenderRetries = 3
				};
			}
			catch(Exception ex)
			{
				addLog("Port setup failed with " + ex.Message + "!" + Environment.NewLine);
				return false;
			}
			addLog("Port ready!" + Environment.NewLine);
			return true;
		}

		public bool Sync()
		{
			int attempts = 0;
			while(attempts++ < 500)
			{
				addLog($"Sync attempt {attempts}/500 ");
				int tries = 50;
				serial.ReadTimeout = 50;
				serial.DiscardInBuffer();
				while(tries-- > 0)
				{
					serial.Write("\r\n");
					var t = string.Empty;
					try
					{
						t = serial.ReadLine();
					}
					catch { }
					if(t.Contains("Boot >"))
					{
						logger.addLog("... OK!" + Environment.NewLine, Color.Green);
						return true;
					}
				}
				addWarningLine("... failed, will retry!");
			}
			return false;
		}

		private string ExecuteCommand(string command, int timeout = 100)
		{
			timeout = timeout < 50 ? 50 : timeout;
			var response = string.Empty;
			while(response.Contains("Unknown command") || response.Contains("Usage") || (response.Length < command.Length) || !response.Contains(command))
			{
				//addLogLine($"Sending command {command}");
				serial.DiscardInBuffer();
				serial.Write($"{command}\r");
				Thread.Sleep(timeout);
				response = serial.ReadExisting();
			}
			return response;
		}

		bool DumpBytes(int start, int count, out byte[] bytes)
		{
			int readCount = 0;
			serial.DiscardInBuffer();
			serial.Write($"md.b {start:X} {count}\r\n");
			serial.ReadLine();
			var lbytes = new List<byte>();
			serial.ReadTimeout = 125;
			while(readCount < count)
			{
				string line;
				try
				{
					line = serial.ReadLine();
				}
				catch { break; }
				//addLogLine($"<<< {line}\r\n");
				var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
				if(parts.Length == 0)
					continue;
				if(parts[0] == "Boot")
					continue;
				uint addr;
				try
				{
					if(parts[0] == "\r")
						continue;
					addr = Convert.ToUInt32(parts[0].Remove(parts[0].IndexOf('-'), 1).Trim(':'), 16);
				}
				catch { continue; }
				if(addr != start + readCount)
					throw new Exception("addr != start + readCount");
				if(parts.Length < 17)
					throw new Exception("parts.Length < 17");
				for(int i = 1; i < 17; i++)
				{
					var val = Convert.ToByte(parts[i].Trim('\r'), 16);
					lbytes.Add(val);
					readCount++;
					if(readCount >= count)
						break;
				}
				if(readCount >= count)
					break;
			}
			bytes = lbytes.ToArray();
			serial.ReadTimeout = 3000;
			Thread.Sleep(5);
			return true;
		}

		string FlashReadHash(int offset, int length)
		{
			var resp = ExecuteCommand($"checksum 2 {offset:X} {length}", length / 500);
			if(!resp.Contains("SHA1"))
			{
				Thread.Sleep(length / 500);
				resp = serial.ReadExisting();
			}
			else
			{
				resp = resp.Substring(resp.IndexOf("SHA1"), 20 * 2 + 7);
			}
			return resp.Substring(7, 20 * 2);
		}

		internal byte[] InternalRead(int addr = 0, int amount = 4096, bool withLog = true)
		{
			try
			{
				int startAmount = amount;
				byte[] ret = new byte[amount];
				var sha1Hash = SHA1.Create();
				if(withLog)
				{
					logger.setProgress(0, amount);
					logger.setState("Reading", Color.White);
					addLogLine("Starting read...");
					addLog("Read parms: start 0x" +
						(addr).ToString("X2")
						+ " (sector " + addr / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
						(amount).ToString("X2")
						+ " (" + amount / BK7231Flasher.SECTOR_SIZE + " sectors)"
						+ Environment.NewLine);
				}
				int startAddr = addr;
				int progress = 0;
				int errCount = 0;
				var dumpAmount = DumpAmount > amount ? amount : DumpAmount;
				while(startAmount > 0 && errCount < 10)
				{
					byte[] bytes = new byte[dumpAmount];
					if(withLog) addLog($"Reading at 0x{startAddr:X6}... ");
					try
					{
						DumpBytes(startAddr | FLASH_MMAP_BASE, dumpAmount, out bytes);
						var treadHash = HashToStr(sha1Hash.ComputeHash(bytes));
						var texpectedHash = FlashReadHash(startAddr | FLASH_MMAP_BASE, dumpAmount);
						if(treadHash != texpectedHash)
						{
							throw new Exception("Hash mismatch");
						}
						errCount = 0;
					}
					catch(Exception ex)
					{
						if(withLog) addWarningLine($"Reading at 0x{startAddr:X6} failed with {ex.Message}, retrying... ");
						Thread.Sleep(250);
						errCount++;
						continue;
					}
					startAddr += dumpAmount;
					startAmount -= dumpAmount;
					if(withLog) logger.setProgress(amount - startAmount, amount);
					bytes.CopyTo(ret, progress);
					progress += dumpAmount;
				}
				if(errCount >= 10)
				{
					throw new Exception("Error count exceeded limit!");
				}
				if(withLog) addLogLine(Environment.NewLine + "Getting hash...");
				var readHash = HashToStr(sha1Hash.ComputeHash(ret));
				var expectedHash = FlashReadHash(addr | FLASH_MMAP_BASE, amount);
				if(readHash != expectedHash)
				{
					if(withLog)
					{
						addErrorLine($"Hash mismatch!\r\nexpected\t{expectedHash}\r\ngot\t{readHash}");
						logger.setState("SHA mismatch!", Color.Red);
					}
					sha1Hash.Clear();
					return null;
				}
				else
				{
					if(withLog) addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
				}
				if(withLog)
				{
					logger.setState("Read done", Color.DarkGreen);
					addLogLine("Read complete!");
				}
				sha1Hash.Clear();
				return ret;
			}
			catch(Exception ex)
			{
				addError(ex.ToString() + Environment.NewLine);
				logger.setState("Read error", Color.Red);
			}
			closePort();
			return null;
		}

		private bool InternalWrite(int addr, byte[] data, int len = -1, bool isFirmware = false)
		{
			var sha1Hash = SHA1.Create();
			try
			{
				xm.PacketSent += Xm_PacketSent;
				if(len < 0)
					len = data.Length;
				logger.setProgress(0, len);
				addLogLine("Starting flash write " + len);
				logger.setState("Writing", Color.White);
				ExecuteCommand($"flash 1");
				ExecuteCommand($"flash 5 2 4096 0 1");
				ExecuteCommand($"flash 3");
				ExecuteCommand($"flash 6 0 1 rda5991h {(addr + 0xFFF) & ~0xFFF:X} {(addr + 0xFFF) & ~0xFFF:X} {(len + 0xFFF) & ~0xFFF:X}");
				ExecuteCommand($"mw 40109020 0x23524441");
				ExecuteCommand($"flash 3");
				var md = ExecuteCommand("md 4");
				if(md.Contains("0x0000B0B1"))
				{
					ExecuteCommand($"mw 40011004 a8b4");
					ExecuteCommand($"mw 40011024 5b10");
				}
				else if(md.Contains("0x0000B12D"))
				{
					ExecuteCommand($"mw 40011004 a8e8");
					ExecuteCommand($"mw 40011024 5b44");
				}
				else
				{
					addErrorLine($"Error on md 4 command");
					return false;
				}
				ExecuteCommand($"mw 40011000 1");
				addLogLine("Sending...");
				ExecuteCommand($"loadx {addr | FLASH_MMAP_BASE:X}");
				var result = xm.Send(data, (uint)addr);
				if(result != len)
				{
					addErrorLine($"Sent {result} bytes, expected {len}");
					logger.setState("Send error!", Color.Red);
					xm.CancelFileTransfer();
					return false;
				}
				Thread.Sleep(500);
				if(addr != 0x1000)
				{
					ExecuteCommand($"flash 6 0 1 rda5991h 0x1000 0x1000 0x1000");
					ExecuteCommand($"mw 40109020 0x23524441");
					ExecuteCommand($"flash 3");
				}
				addLogLine("Getting hash...");
				if(addr < 0x1000)
				{
					addr += 0x1000;
					var skipped = new byte[data.Length - 0x1000];
					Array.Copy(data, 0x1000, skipped, 0, skipped.Length);
					data = skipped;
					len -= 0x1000;
				}
				var readHash = HashToStr(sha1Hash.ComputeHash(data));
				var expectedHash = FlashReadHash(addr | FLASH_MMAP_BASE, len);
				if(readHash != expectedHash)
				{
					addErrorLine($"Hash mismatch!\r\nexpected\t{expectedHash}\r\ngot\t{readHash}");
					logger.setState("SHA mismatch!", Color.Red);
					return false;
				}
				else
				{
					addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
				}
				logger.setState("Writing done", Color.DarkGreen);
				addLogLine("Done flash write " + result);
				return true;
			}
			finally
			{
				xm.PacketSent -= Xm_PacketSent;
				sha1Hash.Clear();
			}
		}

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync())
			{
				if(fullRead)
				{
					GetFlashSize();
					sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
				}
				byte[] res = InternalRead(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
				if(res != null)
					ms = new MemoryStream(res);
			}
			return;
		}

		public byte[] ReadRomTarget(RomReadTarget target)
		{
			try
			{
				if(target == null)
				{
					addError("No ROM reader target selected." + Environment.NewLine);
					return null;
				}
				if(chipType != BKType.RDA5981 || target.Platform != BKType.RDA5981)
				{
					addError("RDA5981 read target is not supported by this flasher." + Environment.NewLine);
					return null;
				}
				if(doGenericSetup() == false)
				{
					return null;
				}
				if(Sync() == false)
				{
					logger.setState("Sync failed!", Color.Red);
					return null;
				}

				string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
				switch(target.Kind)
				{
					case RomReadKind.Rom:
						return ReadRdaRom(target.Address ?? RdaRomBase, target.Length ?? RdaRomSize, targetKindName);
					case RomReadKind.Efuse:
						return ReadRdaEfuse(target.Length ?? RdaEfuseRawSize, targetKindName);
					default:
						addError("Selected RDA5981 read target is not implemented." + Environment.NewLine);
						return null;
				}
			}
			catch(OperationCanceledException)
			{
				string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
				addLogLine(targetKindName + " read cancelled by user.");
				logger.setState("Cancelled", Color.DarkGray);
				return null;
			}
			catch(Exception ex)
			{
				string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
				addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
				logger.setState(targetKindName + " read failed.", Color.Red);
				return null;
			}
			finally
			{
				try { closePort(); } catch { }
			}
		}

		byte[] ReadRdaRom(int offset, int length, string targetKindName)
		{
			if(offset < RdaRomBase || length <= 0 || offset > RdaRomBase + RdaRomSize - length)
			{
				throw new ArgumentOutOfRangeException("length", chipType + " ROM read range is outside the supported BootROM area.");
			}

			byte[] result = new byte[length];
			int copied = 0;
			int errCount = 0;
			logger.setState("Reading " + targetKindName + "...", Color.Transparent);
			logger.setProgress(0, length);
			addLogLine("Reading " + chipType + " " + targetKindName + " from " + formatHex(offset) + ", length " + formatHex(length) + ".");

			while(copied < length && errCount < 10)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int chunkAddress = offset + copied;
				int chunkLength = Math.Min(DumpAmount, length - copied);
				addLog("Reading at 0x" + chunkAddress.ToString("X6") + "... ");
				try
				{
					byte[] bytes;
					DumpBytes(chunkAddress, chunkLength, out bytes);
					if(bytes == null || bytes.Length != chunkLength)
					{
						throw new IOException("Read " + (bytes == null ? 0 : bytes.Length) + " bytes, expected " + chunkLength + ".");
					}
					bytes.CopyTo(result, copied);
					copied += chunkLength;
					errCount = 0;
					logger.setProgress(copied, length);
					addLogLine("OK");
				}
				catch(Exception ex)
				{
					errCount++;
					addWarningLine("failed with " + ex.Message + ", retrying...");
					Thread.Sleep(250);
				}
			}
			if(errCount >= 10)
			{
				throw new IOException("Error count exceeded limit.");
			}

			logger.setState(targetKindName + " read success!", Color.Green);
			return result;
		}

		byte[] ReadRdaEfuse(int expectedLength, string targetKindName)
		{
			if(expectedLength != RdaEfuseRawSize)
			{
				throw new ArgumentOutOfRangeException("expectedLength", chipType + " eFuse dump length must be " + RdaEfuseRawSize + " bytes.");
			}

			UploadRdaEfuseStub();
			logger.setState("Reading " + targetKindName + "...", Color.Transparent);
			logger.setProgress(0, expectedLength);
			addLogLine("Requesting " + chipType + " EFUSE32 dump: pages 0..15, 32 bytes.");
			serial.DiscardInBuffer();
			serial.Write("F\r");

			string response = ReadUntilSerialText(RdaEfusePromptText, RdaEfuseCommandTimeoutMs);
			addLogLine("eFuse helper response: " + FormatSerialSnippet(response));
			byte[] result = ParseRdaEfuseDump(response, "EFUSE32", expectedLength);
			logger.setProgress(expectedLength, expectedLength);
			logger.setState(targetKindName + " read success!", Color.Green);
			return result;
		}

		void UploadRdaEfuseStub()
		{
			byte[] stub = FLoaders.GetRawBinaryFromAssembly(RdaEfuseStubResource);
			addLogLine("Uploading RDA5981 eFuse helper (" + stub.Length + " bytes) to " + formatHex(RdaEfuseStubLoadAddress) + " using ROM loadb.");
			logger.setState("Uploading eFuse helper", Color.Transparent);
			logger.setProgress(0, stub.Length);
			UploadRdaEfuseStubWithLoadb(stub);
			addLogLine("eFuse helper uploaded.");
			ValidateRdaEfuseStubUpload(stub);
			// The helper is a hand-written flat Thumb function; use ROM monitor custom mode with the Thumb bit set.
			addLogLine("Starting RDA5981 eFuse helper at " + formatHex(RdaEfuseStubEntryAddress) + ".");
			serial.DiscardInBuffer();
			serial.Write("go 2 " + RdaEfuseStubEntryAddress.ToString("X8") + "\r");
			string response = ReadUntilSerialText(RdaEfusePromptText, RdaEfuseStubReadyTimeoutMs);
			if(ResponseContainsRdaEfuseBanner(response) == false)
			{
				addWarningLine("No eFuse helper banner after go. Post-go serial text: " + FormatSerialSnippet(response));
				if(ProbeRdaEfuseStubAfterMissingBanner())
				{
					addWarningLine("eFuse helper answered help probe after banner timeout; continuing.");
					return;
				}
				throw new IOException("Timed out waiting for RDA5981 eFuse helper banner. See post-go/probe serial diagnostics above.");
			}
			addLogLine(RdaEfuseReadyText);
		}
		void UploadRdaEfuseStubWithLoadb(byte[] stub)
		{
			serial.DiscardInBuffer();
			string command = "loadb " + RdaEfuseStubLoadAddress.ToString("X8") + " " + stub.Length.ToString("X") + "\r";
			serial.Write(command);
			string preData = ReadUntilSerialText("Download to", 3000);
			Thread.Sleep(50);
			preData += serial.ReadExisting();
			if(preData.Contains("Download to") == false)
			{
				throw new IOException("ROM did not enter loadb raw input mode. Response: " + FormatSerialSnippet(preData));
			}
			addLogLine("loadb pre-data response: " + FormatSerialSnippet(preData));
			int sent = 0;
			const int chunkSize = 64;
			while(sent < stub.Length)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int chunk = Math.Min(chunkSize, stub.Length - sent);
				serial.Write(stub, sent, chunk);
				sent += chunk;
				logger.setProgress(sent, stub.Length);
				Thread.Sleep(2);
			}
			string postData = ReadUntilSerialText("Boot >", 5000);
			addLogLine("loadb post-data response: " + FormatSerialSnippet(postData));
			if(postData.Contains("Boot >") == false)
			{
				throw new IOException("Timed out waiting for Boot prompt after loadb helper upload. Response: " + FormatSerialSnippet(postData));
			}
			if(postData.Contains("Done") == false)
			{
				addWarningLine("loadb returned to Boot prompt without visible Done text.");
			}
		}
		void ValidateRdaEfuseStubUpload(byte[] stub)
		{
			int verifyLength = Math.Min(RdaEfuseStubVerifyBytes, stub.Length);
			if(verifyLength <= 0)
			{
				return;
			}

			addLogLine("Verifying first " + verifyLength + " bytes of eFuse helper at " + formatHex(RdaEfuseStubLoadAddress) + ".");
			try
			{
				byte[] readBack;
				DumpBytes(RdaEfuseStubLoadAddress, verifyLength, out readBack);
				if(readBack == null || readBack.Length != verifyLength)
				{
					throw new IOException("eFuse helper upload verify read " + (readBack == null ? 0 : readBack.Length) + " bytes, expected " + verifyLength + ".");
				}
				for(int i = 0; i < verifyLength; i++)
				{
					if(readBack[i] != stub[i])
					{
						throw new IOException("eFuse helper upload verify mismatch at +0x" + i.ToString("X") + ": expected 0x" + stub[i].ToString("X2") + ", got 0x" + readBack[i].ToString("X2") + ".");
					}
				}
				addLogLine("eFuse helper RAM verify OK (" + verifyLength + " bytes).");
			}
			catch(Exception ex)
			{
				serial.ReadTimeout = 3000;
				addWarningLine("eFuse helper RAM verify failed with " + ex.Message + "; continuing to launch helper.");
			}
		}

		bool ResponseContainsRdaEfuseBanner(string response)
		{
			return string.IsNullOrEmpty(response) == false && response.Contains(RdaEfuseReadyToken);
		}
		bool ProbeRdaEfuseStubAfterMissingBanner()
		{
			try
			{
				serial.Write("?\r");
				string probe = ReadUntilSerialText(RdaEfusePromptText, RdaEfusePostGoProbeTimeoutMs);
				addWarningLine("eFuse helper '?' probe response: " + FormatSerialSnippet(probe));
				return ResponseContainsRdaEfuseBanner(probe);
			}
			catch(Exception ex)
			{
				addWarningLine("eFuse helper '?' probe failed: " + ex.Message);
				return false;
			}
		}

		string ReadUntilSerialText(string expectedText, int timeoutMs)
		{
			DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
			StringBuilder response = new StringBuilder();
			while(DateTime.UtcNow < deadline)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string chunk = serial.ReadExisting();
				if(string.IsNullOrEmpty(chunk) == false)
				{
					response.Append(chunk);
					if(response.ToString().Contains(expectedText))
					{
						break;
					}
				}
				Thread.Sleep(20);
			}
			return response.ToString();
		}

		byte[] ParseRdaEfuseDump(string response, string tag, int expectedLength)
		{
			string marker = tag + "=";
			int start = response.IndexOf(marker, StringComparison.Ordinal);
			if(start < 0)
			{
				throw new IOException("RDA5981 eFuse helper did not return " + marker + ".");
			}
			start += marker.Length;
			int hexLength = expectedLength * 2;
			if(response.Length < start + hexLength)
			{
				throw new IOException("RDA5981 eFuse helper returned a short " + tag + " dump.");
			}

			string hex = response.Substring(start, hexLength);
			if(IsHexString(hex) == false)
			{
				throw new IOException("RDA5981 eFuse helper returned non-hex " + tag + " data.");
			}
			byte[] result = new byte[expectedLength];
			uint actualSum = 0;
			for(int i = 0; i < result.Length; i++)
			{
				byte value = Convert.ToByte(hex.Substring(i * 2, 2), 16);
				result[i] = value;
				actualSum += value;
			}

			string sumMarker = " SUM=";
			int sumStart = response.IndexOf(sumMarker, start + hexLength, StringComparison.Ordinal);
			if(sumStart < 0 || response.Length < sumStart + sumMarker.Length + 8)
			{
				throw new IOException("RDA5981 eFuse helper did not return a checksum.");
			}
			string sumText = response.Substring(sumStart + sumMarker.Length, 8);
			if(IsHexString(sumText) == false)
			{
				throw new IOException("RDA5981 eFuse helper returned a non-hex checksum.");
			}
			uint expectedSum = Convert.ToUInt32(sumText, 16);
			if(actualSum != expectedSum)
			{
				throw new IOException("RDA5981 eFuse checksum mismatch: expected " + expectedSum.ToString("X8") + ", got " + actualSum.ToString("X8") + ".");
			}

			addLogLine("Returned SUM: 0x" + expectedSum.ToString("X8"));
			return result;
		}

		string FormatSerialSnippet(string value)
		{
			if(string.IsNullOrEmpty(value))
			{
				return "<no serial text>";
			}
			string escaped = value.Replace("\r", "\\r").Replace("\n", "\\n");
			if(escaped.Length > 800)
			{
				return escaped.Substring(0, 800) + "...";
			}
			return escaped;
		}

		bool IsHexString(string value)
		{
			for(int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
				{
					continue;
				}
				return false;
			}
			return true;
		}

		private void GetFlashSize()
		{
			addLogLine($"Detecting flash size...");
			var start = InternalRead(0x1000, 16, false);
			for(int i = 0x800000; i >= 0x100000; i /= 2)
			{
				if(InternalRead(i + 0x1000, 16, false).SequenceEqual(start))
				{
					flashSizeMB = i / 0x100000;
				}
			}
			addLogLine($"Detected flash size: {flashSizeMB}MB");
		}
		
		public override byte[] getReadResult()
		{
			return ms?.ToArray();
		}

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
			if(bAll)
			{
				if(doGenericSetup() && Sync())
				{
					GetFlashSize();
					int len = flashSizeMB * 0x100000;
					if(ExecuteCommand($"flash 7 {FLASH_MMAP_BASE:X} {len:X}", len / 300).Contains("Erase Flash done!"))
					{
						addLogLine($"Erase done!");
						return true;
					}
					addErrorLine($"Erase failed!");
				}
			}
			else
			{
				int len = sectors * 0x1000;
				if(ExecuteCommand($"flash 7 {startSector | FLASH_MMAP_BASE:X} {len:X}", len / 300).Contains("Erase Flash done!"))
				{
					addLogLine($"Erase done!");
					return true;
				}
				addErrorLine($"Erase failed!");
			}
			return false;
		}
		
		public override void closePort()
		{
			if(serial != null)
			{
				serial.Close();
				serial.Dispose();
			}
		}

		public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
		{
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync())
			{
				OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
				if(rwMode == WriteMode.ReadAndWrite)
				{
					GetFlashSize();
					sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
					addLogLine($"Flash size detected: {sectors / 256}MB");
					byte[] res = InternalRead(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
					if(res != null)
						ms = new MemoryStream(res);
					if(ms == null)
					{
						return;
					}
					if(saveReadResult(startSector) == false)
					{
						return;
					}
				}
				if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite)
				{
					if(string.IsNullOrEmpty(sourceFileName))
					{
						addLogLine("No filename given!");
						return;
					}
					addLogLine("Reading " + sourceFileName + "...");
					byte[] data = File.ReadAllBytes(sourceFileName);
					var startAddr = 0x1000;
					if(data[0] == 0x02 && data[4] == 'A' && data[5] == 'D' && data[6] == 'R')
					{
						startAddr = 0x0;
					}
					if(!InternalWrite(startAddr, data, -1, true))
					{
						logger.setState("Write error!", Color.Red);
						return;
					}
				}
				if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig)
				{
					if(cfg != null)
					{
						addLogLine("Writing config first");
						var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
						var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

						cfg.saveConfig(chipType);
						var cfgData = cfg.getData();
						byte[] efdata;
						if(cfg.efdata != null)
						{
							try
							{
								efdata = EasyFlash.SaveValueToExistingEasyFlash("ObkCfg", cfg.efdata, cfgData, areaSize, chipType);
							}
							catch(Exception ex)
							{
								addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
								addLog(ex.Message + Environment.NewLine);
								efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
							}
						}
						else
						{
							efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
						}
						if(efdata == null)
						{
							addLog("Something went wrong with EasyFlash" + Environment.NewLine);
							return;
						}
						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
						bool bOk = InternalWrite(offset, efdata, areaSize);
						if(bOk == false)
						{
							logger.setState("Writing error!", Color.Red);
							addError("Writing OBK config data to chip failed." + Environment.NewLine);
							return;
						}
						logger.setState("OBK config write success!", Color.Green);
					}
					else
					{
						addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
					}
				}
			}
		}

		bool saveReadResult(string fileName)
		{
			if(ms == null)
			{
				addError("There was no result to save." + Environment.NewLine);
				return false;
			}
			byte[] dat = ms.ToArray();
			string fullPath = "backups/" + fileName;
			File.WriteAllBytes(fullPath, dat);
			addSuccess("Wrote " + dat.Length + " to " + fileName + Environment.NewLine);
			logger.onReadResultQIOSaved(dat, "", fullPath);
			return true;
		}
		public override bool saveReadResult(int startOffset)
		{
			string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType, backupName, "bin");
			return saveReadResult(fileName);
		}
	}
}
