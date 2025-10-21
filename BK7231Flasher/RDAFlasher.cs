using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BK7231Flasher
{
	public class RDAFlasher : BaseFlasher
	{
		MemoryStream ms;
		int flashSizeMB = 1;
		XMODEM xm;
		readonly int FLASH_MMAP_BASE = 0x18000000;
		readonly int DumpAmount = 0x1000;

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
				xm = new XMODEM(serial, XMODEM.Variants.XModemCRC)
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
					var z = parts[0].Remove(parts[0].IndexOf('-'), 1).Trim(':');
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
			var oldTimeout = serial.ReadTimeout;
			serial.ReadTimeout = 5000;
			serial.WriteLine("");
			Thread.Sleep(25);
			serial.DiscardInBuffer();
			serial.Write($"checksum 2 {offset:X} {length}\r\n");
			Thread.Sleep(5);
			var z = serial.ReadLine();
			var x = serial.ReadLine();
			serial.ReadTimeout = oldTimeout;
			return x.Substring(7, 20 * 2);
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
						var treadHash = RTLZ2Flasher.HashToStr(sha1Hash.ComputeHash(bytes));
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
				var readHash = RTLZ2Flasher.HashToStr(sha1Hash.ComputeHash(ret));
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
				serial.DiscardInBuffer();
				serial.Write($"flash 1\r\n");
				Thread.Sleep(15);
				if(isFirmware)
				{
					serial.Write($"flash 5 2 4096 0 1\r\n");
					Thread.Sleep(15);
					serial.Write($"flash 3\r\n");
					Thread.Sleep(25);
					serial.Write($"flash 6 0 1 rda5991h 0x1000 0x1000 {len + (len % 4096):X}\r\n");
					Thread.Sleep(15);
					serial.Write($"mw 40109020 0x23524441\r\n");
					Thread.Sleep(15);
					serial.Write($"flash 3\r\n");
					Thread.Sleep(25);
				}
				else
				{
					serial.Write($"flash 7 {addr | FLASH_MMAP_BASE:X} {len + (len % 4096):X}\r\n");
					Thread.Sleep(len / 300);
				}
				serial.Write($"mw 40011004 a8e8\r\n");
				Thread.Sleep(15);
				serial.Write($"mw 40011024 5b44\r\n");
				Thread.Sleep(15);
				serial.Write($"mw 40011000 1\r\n");
				Thread.Sleep(15);
				serial.Write($"loadx {addr | FLASH_MMAP_BASE:X}\r\n");
				var result = xm.Send(data);
				if(result != len)
				{
					addErrorLine($"Sent {result} bytes, expected {len}");
					logger.setState("Send error!", Color.Red);
					xm.CancelFileTransfer();
					return false;
				}
				Thread.Sleep(500);
				if(isFirmware)
				{
					serial.Write($"flash 6 0 1 rda5991h 0x1000 0x1000 0x1000\r\n");
					Thread.Sleep(15);
					serial.Write($"mw 40109020 0x23524441\r\n");
					Thread.Sleep(15);
					serial.Write($"flash 3\r\n");
					Thread.Sleep(225);
				}
				addLogLine(Environment.NewLine + "Getting hash...");
				if(addr < 0x1000)
				{
					addr += 0x1000;
					var skipped = new byte[data.Length - 0x1000];
					Array.Copy(data, 0x1000, skipped, 0, skipped.Length);
					data = skipped;
					len -= 0x1000;
				}
				var readHash = RTLZ2Flasher.HashToStr(sha1Hash.ComputeHash(data));
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
					addLogLine($"Detecting flash size...");
					var start = InternalRead(0x1000, 16, false);
					flashSizeMB = 1;
					for(int i = 0x800000; i > 0x100000; i /= 2)
					{
						if(!InternalRead(i + 0x1000, 16, false).SequenceEqual(start))
						{
							flashSizeMB = i / 0x100000;
							break;
						}
					}
					sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
					addLogLine($"Detected flash size: {sectors / 256}MB");
				}
				byte[] res = InternalRead(startSector, sectors * BK7231Flasher.SECTOR_SIZE);
				if(res != null)
					ms = new MemoryStream(res);
			}
			return;
		}
		
		public override byte[] getReadResult()
		{
			return ms?.ToArray();
		}

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
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
			if(rwMode == WriteMode.OnlyOBKConfig)
			{
				addErrorLine("Writing only OBK config is disabled for RDA5981, use \"Automatically configure OBK on flash write\".");
				return;
			}
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync())
			{
				OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
				if(rwMode == WriteMode.ReadAndWrite)
				{
					addLogLine($"Detecting flash size...");
					var start = InternalRead(0x1000, 16, false);
					flashSizeMB = 1;
					for(int i = 0x800000; i > 0x100000; i /= 2)
					{
						if(!InternalRead(i + 0x1000, 16, false).SequenceEqual(start))
						{
							flashSizeMB = i / 0x100000;
							break;
						}
					}
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
					addLogLine("Writing config first");
					if(cfg != null)
					{
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
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
				{
				}
			}
		}

		private void Xm_PacketSent(int sentBytes, int total)
		{
			logger.setProgress(sentBytes, total);
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
