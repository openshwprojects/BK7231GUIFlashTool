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
	public class RTLZ2Flasher : BaseFlasher
	{
		MemoryStream ms;
		private readonly List<string> USED_COMMANDS = new List<string>() 
		{
			"ping","disc","ucfg","DW","DB","EW","EB","WDTRST","hashq","fwd","fwdram"
		};
		readonly uint FLASH_MMAP_BASE = 0x98000000;
		readonly int DumpAmount = 0x1000;
		uint? FuncPtr = null;
		string FuncName = string.Empty;
		int? FlashMode;
		uint? FlashHashOffset;
		bool IsInFallbackMode = false;
		int flashSizeMB = 2;
		byte[] flashID = { 0, 0, 0 };

		public RTLZ2Flasher(CancellationToken ct) : base(ct)
		{
		}

		void Flush()
		{
			serial.DiscardInBuffer();
			serial.DiscardOutBuffer();
		}

		T WithinReadWindow<T>(int timeoutMs, Func<T> action)
		{
			var previous = serial.ReadTimeout;
			serial.ReadTimeout = timeoutMs;
			try
			{
				return action();
			}
			finally
			{
				serial.ReadTimeout = previous;
			}
		}

		void WithinReadWindow(int timeoutMs, Action action)
		{
			WithinReadWindow<object>(timeoutMs, () =>
			{
				action();
				return null;
			});
		}

		byte[] ReadExactly(int count)
		{
			var buffer = new byte[count];
			int offset = 0;
			while(offset < count)
			{
				try
				{
					int read = serial.Read(buffer, offset, count - offset);
					if(read <= 0)
						return null;
					offset += read;
				}
				catch
				{
					return null;
				}
			}
			return buffer;
		}

		string ReadForWindow(int waitMs)
		{
			var sb = new StringBuilder();
			var deadline = DateTime.Now.AddMilliseconds(waitMs);
			return WithinReadWindow(20, () =>
			{
				while(DateTime.Now < deadline)
				{
					try
					{
						var chunk = serial.ReadExisting();
						if(chunk.Length > 0)
							sb.Append(chunk);
					}
					catch
					{
					}
					Thread.Sleep(10);
				}
				return sb.ToString();
			});
		}

		bool TryRepairLink()
		{
			try
			{
				Flush();
			}
			catch
			{
			}
			Thread.Sleep(25);
			try
			{
				return Link();
			}
			catch
			{
				return false;
			}
		}

		T RepeatOnFailure<T>(string label, int attempts, Func<T> action)
		{
			Exception last = null;
			for(int attempt = 1; attempt <= attempts; attempt++)
			{
				try
				{
					return action();
				}
				catch(Exception ex)
				{
					last = ex;
					addWarningLine($"{label} attempt {attempt}/{attempts} failed: {ex.Message}");
					if(attempt < attempts)
						TryRepairLink();
				}
			}
			throw new Exception($"{label} failed after {attempts} attempts", last);
		}

		void Command(string cmd)
		{
			Flush();
			var cmdascii = Encoding.ASCII.GetBytes($"{cmd}\n");
			serial.Write(cmdascii, 0, cmdascii.Length);
			if(IsInFallbackMode)
			{
				// Fallback mode echoes the command.
				ReadExactly(cmdascii.Length + 1);
			}
		}

		bool Link()
		{
			// Check fallback before ping.
			LinkFallback();
			// Allow the ROM console time to respond.
			var deadline = DateTime.Now.AddSeconds(10);
			while(DateTime.Now < deadline)
			{
				try
				{
					Command("ping");
					var resp = ReadExactly(4);
					if(resp != null && Encoding.ASCII.GetString(resp) == "ping")
					{
						var extra = string.Empty;
						try { extra = serial.ReadExisting(); } catch { }
						if(!extra.Contains("$8710c"))
							return true;
					}
				}
				catch { }
				Thread.Sleep(100);
			}
			addErrorLine("Ping response is incorrect");
			addErrorLine("Link failed!");
			return false;
		}

		bool LinkFallback()
		{
			Command("Rtk8710C");
			// Allow a short window for the fallback prompt.
			var resp = ReadForWindow(200);
			if(resp == "\r\n$8710c>\r\n$8710c>" || resp == "Rtk8710C\r\nCommand NOT found.\r\n$8710c>")
			{
				IsInFallbackMode = true;
				var chipVer = (uint)((RegisterRead(0x400001F0) >> 4) & 0xF);
				if(chipVer > 2)
					MemoryBoot(0);
				else
					MemoryBoot(0x1443C);
				IsInFallbackMode = false;
			}
			return true;
		}

		bool ChangeBaud(int baud)
		{
			if(baud == serial.BaudRate)
				return Link();
			addLogLine($"Setting baud rate to {baud}");
			if(!Link())
				return false;
			Flush();
			var cmd = Encoding.ASCII.GetBytes($"ucfg {baud} 0 0\n");
			serial.Write(cmd, 0, cmd.Length);
			serial.BaudRate = baud;
			var response = ReadForWindow(1000).Trim();
			if(response != "OK")
			{
				addErrorLine($"Baud change response is incorrect: {response}");
				return false;
			}
			return Link();
		}

		bool DumpBytes(uint start, int count, out byte[] bytes)
		{
			bytes = RepeatOnFailure("Byte read", 3, () =>
			{
				var lbytes = new List<byte>(count);
				int readCount = 0;
				int timeoutMs = Math.Max(125, (int)Math.Ceiling(Math.Max(Math.Min(count, 1024), 64) * 0.5 / 500.0 * 1000.0));
				Command($"DB {start:X} {count}");
				WithinReadWindow(timeoutMs, () =>
				{
					while(readCount < count)
					{
						var line = serial.ReadLine();
						var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
						if(parts.Length == 0)
							continue;
						if(parts[0] == "[Addr]")
							continue;
						if(parts[0] == "\r")
							continue;
						var addr = Convert.ToUInt32(parts[0].Trim(':', '\r', '\n'), 16);
						if(addr != start + readCount)
							throw new Exception("Read address is incorrect");
						if(parts.Length < 17)
							throw new Exception("Read line is incomplete");
						for(int i = 1; i < 17 && readCount < count; i++)
						{
							lbytes.Add(Convert.ToByte(parts[i].Trim('\r'), 16));
							readCount++;
						}
					}
				});
				if(readCount != count)
					throw new Exception($"Short read: got {readCount}, expected {count}");
				return lbytes.ToArray();
			});
			return true;
		}

		bool DumpWords(uint start, int count, out int[] ints)
		{
			ints = RepeatOnFailure("Word read", 3, () =>
			{
				var words = new List<int>(count);
				int readBytes = 0;
				int expectedBytes = count * 4;
				int timeoutMs = Math.Max(125, (int)Math.Ceiling(Math.Max(Math.Min(count, 256), 16) * 1.5 / 500.0 * 1000.0));
				Command($"DW {start:X} {count}");
				WithinReadWindow(timeoutMs, () =>
				{
					while(readBytes < expectedBytes)
					{
						var line = serial.ReadLine();
						var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
						if(parts.Length == 0)
							continue;
						if(parts[0] == "\r")
							continue;
						var addr = Convert.ToUInt32(parts[0].Trim(':', '\r', '\n'), 16);
						if(addr != start + readBytes)
							throw new Exception("Read address is incorrect");
						if(parts.Length < 5)
							throw new Exception("Read line is incomplete");
						for(int i = 1; i < 5 && readBytes < expectedBytes; i++)
						{
							words.Add(Convert.ToInt32(parts[i].Trim('\r'), 16));
							readBytes += 4;
						}
					}
				});
				if(words.Count != count)
					throw new Exception($"Short read: got {words.Count}, expected {count}");
				return words.ToArray();
			});
			return true;
		}

		int RegisterRead(uint addr)
		{
			DumpWords((uint)(addr & ~0x3), 1, out var words);
			return words[0];
		}

		void RegisterWrite(uint addr, uint value)
		{
			RepeatOnFailure("Register write", 3, () =>
			{
				Command($"EW {addr:X} {value:X}");
				WithinReadWindow(300, () => serial.ReadLine());
				return true;
			});
		}

		bool MemoryBoot(uint addr)
		{
			addr |= 1;
			if(FuncPtr == null)
			{
				var cmds = RegisterRead(0x1002F054);
				for(uint i = (uint)cmds; i < cmds + 8 * 12; i += 12)
				{
					var namePtr = RegisterRead(i);
					if(namePtr == 0)
						break;
					DumpBytes((uint)namePtr, 16, out var nameBytes);
					var fname = Encoding.ASCII.GetString(nameBytes).Split('\0')[0];
					if(USED_COMMANDS.Contains(fname))
						continue;
					FuncPtr = i + 4;
					FuncName = fname;
					break;
				}
			}
			if(FuncPtr == null)
				throw new Exception($"{nameof(FuncPtr)} is null!");
			RegisterWrite(FuncPtr.Value, addr);
			addLogLine($"Jump to 0x{FuncPtr.Value:X} using '{FuncName}'");
			Command($"{FuncName}");
			return true;
		}

		bool FlashTransmit(MemoryStream data, uint offset)
		{
			return RepeatOnFailure("Flash transfer", data == null ? 3 : 2, () =>
			{
				FlashInit(false);
				WithinReadWindow(3000, () =>
				{
					Command($"fwd 0 {FlashMode} {offset:x}");
					FlashHashOffset = offset;
					if(data == null)
					{
						var resp = serial.ReadByte();
						if(resp != 0x15)
							throw new Exception($"expected NAK, got {resp}");
						serial.Write(new byte[] { 0x18 }, 0, 1);
						var abort = ReadExactly(3);
						if(abort == null || abort.Length != 3 || abort[0] != 0x18 || abort[1] != (byte)'E' || abort[2] != (byte)'R')
							throw new Exception($"expected CAN, got {Encoding.ASCII.GetString(abort ?? new byte[0])}");
					}
					else
					{
						logger.setState("Writing...", Color.Transparent);
						xm.PacketSent += Xm_PacketSent;
						try
						{
							var res = xm.Send(data.ToArray(), offset | FLASH_MMAP_BASE);
							if(res != data.Length)
								throw new Exception("XMODEM transmission failed");
						}
						finally
						{
							xm.PacketSent -= Xm_PacketSent;
						}
					}
				});
				if(!Link())
					throw new Exception("Link restore failed after transfer");
				if(data == null)
					return true;
				logger.setState("Verifying write...", Color.Transparent);
				addLogLine("Verifying write via hash...");
				using(var sha256 = SHA256.Create())
				{
					var localHash = HashToStr(sha256.ComputeHash(data.ToArray()));
					FlashHashOffset = null;
					var flashHash = HashToStr(FlashReadHash(offset, (int)data.Length));
					if(localHash != flashHash)
					{
						logger.setState("Write verify failed!", Color.Red);
						throw new Exception($"Write hash mismatch! expected {localHash}, got {flashHash}");
					}
					addSuccess($"Write verified OK ({localHash})!" + Environment.NewLine);
				}
				logger.setProgress(1, 1);
				addLog("Write complete!" + Environment.NewLine);
				logger.setState("Write complete!", Color.Transparent);
				return true;
			});
		}

		byte[] FlashReadHash(uint offset, int length)
		{
			return RepeatOnFailure("Hash read", 3, () =>
			{
				if(FlashHashOffset != offset)
					FlashTransmit(null, offset);
				Command($"hashq {length} 0 {FlashMode}");
				var timeoutMs = Math.Max(600, (int)(Math.Ceiling((double)length / 1500000.0 * 10.0) / 10.0 * 1000.0));
				var response = WithinReadWindow(timeoutMs, () => ReadExactly(6 + 32));
				if(response == null || response.Length != 38)
					throw new Exception("Hash response is incomplete");
				var prefix = Encoding.ASCII.GetString(response, 0, 6);
				if(prefix != "hashs ")
					throw new Exception($"Unexpected response to 'hashq', got {Encoding.ASCII.GetString(response)}");
				return response.Skip(6).Take(32).ToArray();
			});
		}

		void FlashInit(bool configure = true)
		{
			FlashMode = (RegisterRead(0x40000038) >> 5) & 0b11;
			RegisterWrite(0x40002800, 0x7EFFFFFF);
			if(configure && FlashHashOffset == null)
			{
				FlashReadHash(0, 0);
			}
		}

		bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			if(serial == null)
			{
				serial = new SerialPort(serialName, 115200);
				serial.ReadBufferSize = 32768;
				serial.WriteBufferSize = 8192;
				serial.ReadTimeout = 5000;
				serial.WriteTimeout = 5000;
				serial.Open();
			}
			Flush();
			serial.ReadTimeout = 5000;
			addLog("Port ready!" + Environment.NewLine);
			xm = new XMODEM(serial, XMODEM.Variants.XModem1KChecksum, 0xFF);
			if(Link() == false)
			{
				return false;
			}
			return true;
		}

		public bool doWrite(int startSector, int numSectors, byte[] data, WriteMode mode)
		{
			try
			{
				OBKConfig cfg = mode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();

				int size = numSectors * BK7231Flasher.SECTOR_SIZE;
				if(data != null)
				{
					size = data.Length;
				}
				logger.setProgress(0, size);
				addLog(Environment.NewLine + "Starting write!" + Environment.NewLine);
				// startSector is a sector index.
				addLog("Write parms: start 0x" +
					(startSector * BK7231Flasher.SECTOR_SIZE).ToString("X")
					+ " (sector " + startSector + "), len 0x" +
					(size).ToString("X2")
					+ " (" + (((size + 0xFFF) & ~0xFFF) / BK7231Flasher.SECTOR_SIZE) + " sectors)"
					+ Environment.NewLine);
				Console.WriteLine("Connected");
				if(mode == WriteMode.ReadAndWrite)
				{
					doRead(startSector, numSectors, true);
					if(ms == null)
					{
						return true;
					}
					if(saveReadResult(startSector) == false)
					{
						return true;
					}
				}
				else
				{
					if(doGenericSetup() == false)
					{
						return true;
					}
				}
				int address = startSector * BK7231Flasher.SECTOR_SIZE;
				int count = (size + 4095) / 4096;


				if(ChangeBaud(baudrate) == false)
				{
					return true;
				}
				if(mode != WriteMode.OnlyOBKConfig)
				{
					uint writeOffset = (uint)(address & 0x00ffffff);
					addLog(string.Format("Write Flash data 0x{0:X8} to 0x{1:X8}", writeOffset, writeOffset + size) + Environment.NewLine);
					writeOffset |= FLASH_MMAP_BASE;

					var ms = new MemoryStream(data);
					// Pass raw flash offset to FlashTransmit - fwd command needs raw, not virtual (MMAP) address
					if(!FlashTransmit(ms, (uint)(startSector * 0x1000)))
					{
						addLog("Error: Write Flash!" + Environment.NewLine);
						ChangeBaud(115200);
						ms?.Dispose();
						return true;
					}
					addLog("Write done!" + Environment.NewLine);
					ms?.Dispose();
				}
				if(cfg != null)
				{
					var offset = (uint)OBKFlashLayout.getConfigLocation(chipType, out var sectors);
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
						return false;
					}
					ms?.Dispose();
					ms = new MemoryStream(efdata);
					addLog("Now will also write OBK config..." + Environment.NewLine);
					addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
					addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
					addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
					addLog("Writing config sector " + formatHex(offset) + "..." + Environment.NewLine);
					// Do NOT OR in FLASH_MMAP_BASE here - FlashTransmit expects raw flash offset for fwd command
					bool bOk = FlashTransmit(ms, offset);
					if(bOk == false)
					{
						logger.setState("Writing error!", Color.Red);
						addError("Writing OBK config data to chip failed." + Environment.NewLine);
						return false;
					}
					logger.setState("OBK config write success!", Color.Green);
				}
				else
				{
					addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
				}
				ChangeBaud(115200);
				return false;
			}
			catch(Exception ex)
			{
				addError(ex.ToString() + Environment.NewLine);
			}
			closePort();
			return true;
		}

		public void ReadFlashId()
		{
			FlashInit();
			// read flash id
			Command($"EB 0x40020060 0x9F");
			Command($"EW 0x40020004 3");
			Command($"EW 0x40020008 1");
			Command($"EW 0x40020008 0");
			Thread.Sleep(10);
			Flush();
			DumpBytes(0x40020060, 16, out var bytes);
			flashSizeMB = (1 << (bytes[1] - 0x11)) / 8;
			flashID[0] = bytes[0];
			flashID[1] = bytes[4];
			flashID[2] = bytes[1];
			addLogLine($"Flash ID: 0x{flashID[0]:X}{flashID[1]:X}{flashID[2]:X}");
			addLogLine($"{flashSizeMB}MB flash size detected");
		}

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			if(doGenericSetup() == false)
			{
				return;
			}
			if(fullRead)
			{
				ReadFlashId();
				sectors = flashSizeMB * 256;
			}
			byte[] res = readFlash(startSector * 0x1000, sectors * 0x1000);
			ms = res != null ? new MemoryStream(res) : null;
		}

		internal byte[] readFlash(int addr = 0, int amount = 4096)
		{
			try
			{
				int startAmount = amount;
				byte[] ret = new byte[amount];
				var sha256Hash = SHA256.Create();
				logger.setProgress(0, amount);
				logger.setState("Reading", Color.White);
				addLogLine("Starting read...");
				addLog("Read parms: start 0x" +
					(addr).ToString("X2")
					+ " (sector " + addr / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
					(amount).ToString("X2")
					+ " (" + amount / BK7231Flasher.SECTOR_SIZE + " sectors)"
					+ Environment.NewLine);
				FlashInit();
				if(ChangeBaud(baudrate) == false)
				{
					return null;
				}
				uint startAddr = (uint)addr;
				int progress = 0;
				int errCount = 0;
				while(startAmount > 0 && errCount < 10)
				{
					byte[] bytes = new byte[DumpAmount];
					addLog($"Reading at 0x{startAddr:X6}... ");
					try
					{
						DumpBytes(startAddr | FLASH_MMAP_BASE, DumpAmount, out bytes);
						// Verify each chunk against the chip hash.
						var chunkHash = HashToStr(sha256Hash.ComputeHash(bytes));
						FlashHashOffset = null;
						var expectedChunkHash = HashToStr(FlashReadHash(startAddr, DumpAmount));
						if(chunkHash != expectedChunkHash)
						{
							throw new Exception($"Hash mismatch (got {chunkHash}, expected {expectedChunkHash})");
						}
						addLogLine("OK");
						errCount = 0;
					}
					catch(InvalidOperationException ioex)
					{
						addErrorLine(ioex.Message);
						return null;
					}
					catch(Exception ex)
					{
						addWarningLine($"Reading at 0x{startAddr:X6} failed with {ex.Message}, retrying... ");
						Thread.Sleep(250);
						errCount++;
						continue;
					}
					startAddr += (uint)DumpAmount;
					startAmount -= DumpAmount;
					logger.setProgress(amount - startAmount, amount);
					bytes.CopyTo(ret, progress);
					progress += DumpAmount;
				}
				if(errCount >= 10)
				{
					throw new Exception("Error count exceeded limit!");
				}
				addLogLine(Environment.NewLine + "Getting hash...");
				var readHash = HashToStr(sha256Hash.ComputeHash(ret));
				var expectedHash = HashToStr(FlashReadHash((uint)addr, amount));
				if(readHash != expectedHash)
				{
					addErrorLine($"Hash mismatch!\r\nexpected\t{expectedHash}\r\ngot\t{readHash}");
					logger.setState("SHA mismatch!", Color.Red);
					sha256Hash.Clear();
					ChangeBaud(115200);
					return null;
				}
				else
				{
					addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
				}
				logger.setState("Read done", Color.DarkGreen);
				addLogLine("Read complete!");
				sha256Hash.Clear();
				ChangeBaud(115200);
				return ret;
			}
			catch(Exception ex)
			{
				addError(ex.ToString() + Environment.NewLine);
				logger.setState("Read error", Color.Red);
				ChangeBaud(115200);
			}
			closePort();
			return null;
		}

		public bool doReadInternal(int startSector, int sectors)
		{
			byte[] res = readFlash(startSector * 0x1000, sectors * 0x1000);
			ms = new MemoryStream(res);
			return false;
		}

		public override byte[] getReadResult()
		{
			return ms?.ToArray() ?? null;
		}

		public override bool doErase(int startSector, int sectors, bool bAll)
		{
			return false;
		}

		public override void closePort()
		{
			if(serial != null)
			{
				serial.Close();
				serial.Dispose();
				serial = null;
			}
		}

		public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
		{
			byte[] data = null;

			if(rwMode != WriteMode.OnlyOBKConfig)
			{
				if(string.IsNullOrEmpty(sourceFileName))
				{
					addErrorLine("No source file set!");
					return;
				}
				data = File.ReadAllBytes(sourceFileName);
			}
			else
			{
				startSector = OBKFlashLayout.getConfigLocation(chipType, out sectors) / BK7231Flasher.SECTOR_SIZE;
			}
			doWrite(startSector, sectors, data, rwMode);
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

