using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		readonly Stack<int> ReadTimeoutStack = new Stack<int>();
		const int ReadUnitSize = 0x1000;
		const int VerifyWindowSize = 128 * 1024;
		const int ReadChunkRetryLimit = 3;
		const int ReadWindowRetryLimit = 3;
		const int WriteWindowRetryLimit = 3;
		const int HashRetryLimit = 3;
		const int CommandRetryLimit = 2;
		const int FallbackBaudRate = 115200;
		const string InternalBuildId = "rtlz2-resiliency-r2";

		public RTLZ2Flasher(CancellationToken ct) : base(ct)
		{
		}

		void Flush()
		{
			serial.DiscardInBuffer();
			serial.DiscardOutBuffer();
		}

		void PushReadTimeout(int timeoutMs)
		{
			ReadTimeoutStack.Push(serial.ReadTimeout);
			serial.ReadTimeout = timeoutMs;
		}

		void PopReadTimeout()
		{
			if(ReadTimeoutStack.Count > 0)
			{
				serial.ReadTimeout = ReadTimeoutStack.Pop();
			}
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
					{
						return null;
					}
					offset += read;
				}
				catch
				{
					return null;
				}
			}
			return buffer;
		}

		string ReadWithTimeout(int waitMs)
		{
			var sb = new StringBuilder();
			var deadline = DateTime.Now.AddMilliseconds(waitMs);
			PushReadTimeout(20);
			try
			{
				while(DateTime.Now < deadline)
				{
					try
					{
						var chunk = serial.ReadExisting();
						if(chunk.Length > 0)
						{
							sb.Append(chunk);
						}
					}
					catch { }
					Thread.Sleep(10);
				}
			}
			finally
			{
				PopReadTimeout();
			}
			return sb.ToString();
		}

		bool WaitForTxIdle(int timeoutMs)
		{
			var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
			while(DateTime.Now < deadline)
			{
				try
				{
					if(serial.BytesToWrite == 0)
					{
						try { serial.BaseStream.Flush(); } catch { }
						return true;
					}
				}
				catch { }
				Thread.Sleep(5);
			}
			try { serial.BaseStream.Flush(); } catch { }
			return false;
		}

		bool HasAck(string text)
		{
			return !string.IsNullOrEmpty(text) && text.Contains("OK");
		}

		bool RunWithRecovery(string label, int attempts, Func<bool> action)
		{
			for(int attempt = 1; attempt <= attempts; attempt++)
			{
				try
				{
					if(action())
					{
						return true;
					}
				}
				catch(Exception ex)
				{
					if(attempt >= attempts)
					{
						addErrorLine($"{label} failed: {ex.Message}");
						return false;
					}
					addWarningLine($"{label} failed on attempt {attempt}/{attempts}: {ex.Message}");
				}
				try { Flush(); } catch { }
				try { Link(); } catch { }
				Thread.Sleep(50);
			}
			return false;
		}

		byte[] RunWithRecoveryBytes(string label, int attempts, Func<byte[]> action)
		{
			Exception last = null;
			for(int attempt = 1; attempt <= attempts; attempt++)
			{
				try
				{
					var result = action();
					if(result != null)
					{
						return result;
					}
					last = new Exception("No data returned");
				}
				catch(Exception ex)
				{
					last = ex;
				}
				if(attempt < attempts)
				{
					addWarningLine($"{label} failed on attempt {attempt}/{attempts}: {last.Message}");
					try { Flush(); } catch { }
					try { Link(); } catch { }
					Thread.Sleep(50);
				}
			}
			if(last != null)
			{
				addErrorLine($"{label} failed: {last.Message}");
			}
			return null;
		}

		void EnsureWindowBounds(uint start, int count)
		{
			if(count <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			ulong end = (ulong)start + (uint)count;
			if(end > 0x02000000UL)
			{
				throw new ArgumentOutOfRangeException(nameof(count), "Requested range exceeds supported flash window");
			}
		}

		void LogAddressSpan(uint start, int length, bool mapped)
		{
			uint baseAddr = mapped ? FLASH_MMAP_BASE + start : start;
			for(int offset = 0; offset < length; offset += ReadUnitSize)
			{
				string fmt = mapped ? "X8" : "X6";
				uint addr = baseAddr + (uint)offset;
				addLog($"0x{addr.ToString(fmt)}... ");
			}
			addLogLine(string.Empty);
		}

		void Command(string cmd)
		{
			Flush();
			//addLogLine($">>> {cmd}");
			var cmdascii = Encoding.ASCII.GetBytes($"{cmd}\n");
			serial.Write(cmdascii, 0, cmdascii.Length);
			if(IsInFallbackMode)
			{
				ReadExactly(cmdascii.Length + 1);
			}
		}

		bool Link()
		{
			LinkFallback();
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
						{
							return true;
						}
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
			var resp = ReadWithTimeout(200);
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

		bool TryChangeBaudOnce(int fromBaud, int toBaud, int txIdleMs, int oldReadMs, int newReadMs)
		{
			serial.BaudRate = fromBaud;
			Flush();
			if(!Link())
			{
				return false;
			}
			var cmd = Encoding.ASCII.GetBytes($"ucfg {toBaud} 0 0\n");
			serial.Write(cmd, 0, cmd.Length);
			WaitForTxIdle(txIdleMs);
			var oldSide = ReadWithTimeout(oldReadMs);
			serial.BaudRate = toBaud;
			Thread.Sleep(20);
			var newSide = ReadWithTimeout(newReadMs);
			if(HasAck(oldSide) || HasAck(newSide))
			{
				Flush();
				return Link();
			}
			Flush();
			if(Link())
			{
				addLogLine("Baud change completed without explicit OK response");
				return true;
			}
			serial.BaudRate = fromBaud;
			Flush();
			return false;
		}

		bool ChangeBaud(int baud)
		{
			if(baud == serial.BaudRate)
			{
				return Link();
			}
			addLogLine($"Setting baud rate to {baud}");
			var originalBaud = serial.BaudRate;
			if(TryChangeBaudOnce(originalBaud, baud, 150, 40, 250))
			{
				return true;
			}
			if(TryChangeBaudOnce(originalBaud, baud, 300, 75, 450))
			{
				return true;
			}
			addErrorLine("Baud change response is incorrect: ");
			return false;
		}

		bool DumpBytes(uint start, int count, out byte[] bytes)
		{
			EnsureWindowBounds(start & 0x00FFFFFF, count);
			int readCount = 0;
			var data = new List<byte>(count);
			Command($"DB {start:X} {count}");
			var deadline = DateTime.Now.AddMilliseconds(Math.Max(2000, count / 8));
			PushReadTimeout(500);
			try
			{
				while(readCount < count && DateTime.Now < deadline)
				{
					string line;
					try
					{
						line = serial.ReadLine();
					}
					catch(TimeoutException)
					{
						continue;
					}

					var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
					if(parts.Length == 0 || parts[0] == "[Addr]" || parts[0] == "\r")
					{
						continue;
					}

					uint addr;
					if(!uint.TryParse(parts[0].Trim(':', '\r', '\n'), System.Globalization.NumberStyles.HexNumber, null, out addr))
					{
						continue;
					}
					if(addr != start + readCount)
					{
						throw new Exception("Unexpected byte dump address");
					}
					if(parts.Length < 17)
					{
						throw new Exception("Incomplete byte dump line");
					}

					for(int i = 1; i < 17 && readCount < count; i++)
					{
						if(!byte.TryParse(parts[i].Trim('\r'), System.Globalization.NumberStyles.HexNumber, null, out var value))
						{
							throw new Exception("Invalid byte dump data");
						}
						data.Add(value);
						readCount++;
					}
				}
			}
			finally
			{
				PopReadTimeout();
			}

			if(readCount != count)
			{
				bytes = null;
				return false;
			}

			bytes = data.ToArray();
			return true;
		}

		bool DumpWords(uint start, int count, out int[] ints)
		{
			int readCount = 0;
			var data = new List<int>(Math.Max(1, count / 4));
			Command($"DW {start:X} {count}");
			var deadline = DateTime.Now.AddMilliseconds(Math.Max(1500, count * 50));
			PushReadTimeout(500);
			try
			{
				while(readCount < count && DateTime.Now < deadline)
				{
					string line;
					try
					{
						line = serial.ReadLine();
					}
					catch(TimeoutException)
					{
						continue;
					}

					var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
					if(parts.Length == 0 || parts[0] == "\r")
					{
						continue;
					}

					uint addr;
					if(!uint.TryParse(parts[0].Trim(':', '\r', '\n'), System.Globalization.NumberStyles.HexNumber, null, out addr))
					{
						continue;
					}
					if(addr != start + readCount)
					{
						throw new Exception("Unexpected word dump address");
					}
					if(parts.Length < 5)
					{
						throw new Exception("Incomplete word dump line");
					}

					for(int i = 1; i < 5 && readCount < count; i++)
					{
						if(!int.TryParse(parts[i].Trim('\r'), System.Globalization.NumberStyles.HexNumber, null, out var value))
						{
							throw new Exception("Invalid word dump data");
						}
						data.Add(value);
						readCount += 4;
					}
				}
			}
			finally
			{
				PopReadTimeout();
			}

			if(readCount != count)
			{
				ints = null;
				return false;
			}

			ints = data.ToArray();
			return true;
		}

		int RegisterRead(uint addr)
		{
			if(!DumpWords((uint)(addr & ~0x3), 4, out var words) || words == null || words.Length == 0)
			{
				throw new Exception($"Register read failed at 0x{addr:X}");
			}
			return words[0];
		}

		void RegisterWrite(uint addr, uint value)
		{
			Command($"EW {addr:X} {value:X}");
			var response = ReadWithTimeout(120);
			if(response.Contains("ERR"))
			{
				throw new Exception($"Register write failed at 0x{addr:X}");
			}
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

		bool SetHashBase(uint offset)
		{
			return RunWithRecovery("Hash offset set", CommandRetryLimit, () => FlashTransmit(null, offset));
		}

		byte[] FlashReadHashCore(uint offset, int length)
		{
			if(FlashHashOffset != offset)
			{
				if(!SetHashBase(offset))
				{
					return null;
				}
			}
			var timeoutSeconds = Math.Ceiling((double)Math.Max(length, 1) / 1500.0 * 10.0) / 10.0;
			var timeoutMs = Math.Max(serial.ReadTimeout, (int)(timeoutSeconds * 1000.0) + 250);
			Command($"hashq {length} 0 {FlashMode}");
			PushReadTimeout(timeoutMs);
			try
			{
				var response = ReadExactly(6 + 32);
				if(response == null || response.Length != 38)
				{
					throw new Exception("Incomplete hash response");
				}
				var prefix = Encoding.ASCII.GetString(response, 0, 6);
				if(prefix != "hashs ")
				{
					throw new Exception($"Unexpected response to hashq: {Encoding.ASCII.GetString(response)}");
				}
				return response.Skip(6).Take(32).ToArray();
			}
			finally
			{
				PopReadTimeout();
			}
		}

		byte[] FlashReadHash(uint offset, int length)
		{
			return RunWithRecoveryBytes("Hash read", HashRetryLimit, () => FlashReadHashCore(offset, length));
		}

		bool VerifyFlashWindow(byte[] expected, uint offset)
		{
			using(var sha256 = SHA256.Create())
			{
				var expectedHash = sha256.ComputeHash(expected);
				var actualHash = FlashReadHash(offset, expected.Length);
				if(actualHash == null)
				{
					return false;
				}
				return expectedHash.SequenceEqual(actualHash);
			}
		}

		bool FlashTransmit(MemoryStream data, uint offset)
		{
			FlashInit(false);
			PushReadTimeout(3000);
			try
			{
				Command($"fwd 0 {FlashMode} {offset:x}");
				FlashHashOffset = offset;
				if(data == null)
				{
					var resp = serial.ReadByte();
					if(resp != '\x15')
					{
						throw new Exception($"expected NAK, got {resp}");
					}
					serial.Write("\x18");
					Flush();
					var response = ReadExactly(3);
					if(response == null || response.Length != 3 || response[0] != 24 || response[1] != (byte)'E' || response[2] != (byte)'R')
					{
						throw new Exception($"expected CAN, got {(response == null ? "<null>" : Encoding.ASCII.GetString(response))}");
					}
					return Link();
				}

				logger.setState("Writing...", Color.Transparent);
				var res = xm.Send(data.ToArray(), offset | FLASH_MMAP_BASE);
				if(res != data.Length)
				{
					logger.setState("Write error!", Color.Transparent);
					return false;
				}
			}
			finally
			{
				PopReadTimeout();
			}
			Thread.Sleep(50);
			return Link();
		}

		bool WriteWindow(byte[] window, uint offset)
		{
			for(int attempt = 1; attempt <= WriteWindowRetryLimit; attempt++)
			{
				addLogLine($"Write window 0x{offset:X6} len 0x{window.Length:X} attempt {attempt}/{WriteWindowRetryLimit}");
				LogAddressSpan(offset, window.Length, true);
				using(var stream = new MemoryStream(window, false))
				{
					if(!RunWithRecovery("Flash write", CommandRetryLimit, () => FlashTransmit(stream, offset)))
					{
						if(attempt == WriteWindowRetryLimit)
						{
							return false;
						}
						continue;
					}
				}
				logger.setState("Verifying write...", Color.Transparent);
				addLogLine($"Verifying write window 0x{offset:X6} len 0x{window.Length:X}");
				if(VerifyFlashWindow(window, offset))
				{
					return true;
				}
				addWarningLine($"Write verify failed at 0x{offset:X6}");
				if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
				{
					addWarningLine($"Lowering baud rate to {FallbackBaudRate} for write recovery");
					ChangeBaud(FallbackBaudRate);
				}
				try { Flush(); } catch { }
				try { Link(); } catch { }
			}
			return false;
		}

		bool WriteFlashWindows(byte[] data, uint offset)
		{
			EnsureWindowBounds(offset, data.Length);
			int done = 0;
			while(done < data.Length)
			{
				int windowLen = Math.Min(VerifyWindowSize, data.Length - done);
				var window = new byte[windowLen];
				Buffer.BlockCopy(data, done, window, 0, windowLen);
				if(!WriteWindow(window, offset + (uint)done))
				{
					addErrorLine($"Write window failed at 0x{offset + (uint)done:X6}");
					return false;
				}
				done += windowLen;
				logger.setProgress(done, data.Length);
			}
			return true;
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
			addLog("Engine: " + InternalBuildId + Environment.NewLine);
			if(serial == null)
			{
				serial = new SerialPort(serialName, 115200);
				serial.ReadBufferSize = 1024 * 1024;
				serial.WriteBufferSize = 1024 * 256;
				serial.Open();
			}
			Flush();
			serial.ReadTimeout = 5000;
			serial.WriteTimeout = 5000;
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
				// startSector is a sector index, not a byte address.
				// Byte address = startSector * SECTOR_SIZE. Sector number = startSector as-is.
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

					if(!WriteFlashWindows(data, (uint)(startSector * 0x1000)))
					{
						addLog("Error: Write Flash!" + Environment.NewLine);
						ChangeBaud(FallbackBaudRate);
						return true;
					}
					addLog("Write done!" + Environment.NewLine);
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
					bool bOk = WriteFlashWindows(efdata, offset);
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
				logger.setProgress(size, size);
				addSuccess("Flash complete!" + Environment.NewLine);
				logger.setState("Flash complete!", Color.DarkGreen);
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
				//while(sectors < 4096)
				//{
				//	DumpBytes((uint)sectors * 0x1000 | FLASH_MMAP_BASE, 16, out var bytes);
				//	if(bytes[0] != 0x99 && bytes[1] != 0x99 && bytes[2] != 0x96 && bytes[3] != 0x96)
				//	{
				//		sectors *= 2;
				//	}
				//	else
				//		break;
				//	if(sectors == 8192)
				//	{
				//		sectors = 512;
				//		break;
				//	}
				//}
			}
			byte[] res = readFlash(startSector * 0x1000, sectors * 0x1000);
			ms = res != null ? new MemoryStream(res) : null;
		}

		byte[] ReadVerifiedWindow(uint startAddr, int windowLength)
		{
			EnsureWindowBounds(startAddr, windowLength);
			for(int attempt = 1; attempt <= ReadWindowRetryLimit; attempt++)
			{
				var window = new byte[windowLength];
				int copied = 0;
				bool failed = false;
				LogAddressSpan(startAddr, windowLength, false);
				for(int chunkOffset = 0; chunkOffset < windowLength; chunkOffset += ReadUnitSize)
				{
					int chunkLength = Math.Min(ReadUnitSize, windowLength - chunkOffset);
					uint chunkAddr = startAddr + (uint)chunkOffset;
					byte[] chunk = null;
					for(int chunkAttempt = 1; chunkAttempt <= ReadChunkRetryLimit; chunkAttempt++)
					{
						if(RunWithRecovery($"Read 0x{chunkAddr:X6}", CommandRetryLimit, () => DumpBytes(chunkAddr | FLASH_MMAP_BASE, chunkLength, out chunk)))
						{
							break;
						}
						if(chunkAttempt < ReadChunkRetryLimit)
						{
							addWarningLine($"Read retry at 0x{chunkAddr:X6} ({chunkAttempt}/{ReadChunkRetryLimit})");
							Thread.Sleep(75);
						}
					}
					if(chunk == null || chunk.Length != chunkLength)
					{
						addWarningLine($"Read failed at 0x{chunkAddr:X6}");
						failed = true;
						break;
					}
					Buffer.BlockCopy(chunk, 0, window, copied, chunkLength);
					copied += chunkLength;
				}

				if(failed)
				{
					if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
					{
						addWarningLine($"Lowering baud rate to {FallbackBaudRate} for read recovery");
						ChangeBaud(FallbackBaudRate);
					}
					continue;
				}

				logger.setState("Verifying read...", Color.Transparent);
				addLogLine($"Verifying read window 0x{startAddr:X6} len 0x{windowLength:X}");
				if(VerifyFlashWindow(window, startAddr))
				{
					return window;
				}

				addWarningLine($"Read verify failed at 0x{startAddr:X6} len 0x{windowLength:X} attempt {attempt}/{ReadWindowRetryLimit}");
				try { Flush(); } catch { }
				try { Link(); } catch { }
				if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
				{
					addWarningLine($"Lowering baud rate to {FallbackBaudRate} for read recovery");
					ChangeBaud(FallbackBaudRate);
				}
			}
			return null;
		}

		internal byte[] readFlash(int addr = 0, int amount = 4096)
		{
			try
			{
				EnsureWindowBounds((uint)addr, amount);
				var ret = new byte[amount];
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

				var totalTimer = Stopwatch.StartNew();
				using(var overallSha = SHA256.Create())
				{
					uint currentAddr = (uint)addr;
					int remaining = amount;
					int copied = 0;
					bool loggedRate = false;

					while(remaining > 0)
					{
						int windowLength = Math.Min(VerifyWindowSize, remaining);
						var windowTimer = Stopwatch.StartNew();
						var window = ReadVerifiedWindow(currentAddr, windowLength);
						windowTimer.Stop();
						if(window == null)
						{
							throw new Exception($"Verified read failed at 0x{currentAddr:X6}");
						}

						Buffer.BlockCopy(window, 0, ret, copied, windowLength);
						overallSha.TransformBlock(window, 0, windowLength, null, 0);

						copied += windowLength;
						currentAddr += (uint)windowLength;
						remaining -= windowLength;
						logger.setProgress(copied, amount);

						if(!loggedRate)
						{
							loggedRate = true;
							var seconds = Math.Max(windowTimer.Elapsed.TotalSeconds, 0.001);
							var rate = windowLength / seconds;
							addLogLine($"Observed read rate: {rate / 1024.0:F1} KiB/s");
						}
					}

					overallSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					addLogLine(Environment.NewLine + "Getting hash...");
					var readHash = HashToStr(overallSha.Hash);
					var expectedHashBytes = FlashReadHash((uint)addr, amount);
					if(expectedHashBytes == null)
					{
						throw new Exception("Final hash read failed");
					}
					var expectedHash = HashToStr(expectedHashBytes);
					if(readHash != expectedHash)
					{
						addErrorLine($"Hash mismatch!\r\nexpected\t{expectedHash}\r\ngot\t{readHash}");
						logger.setState("SHA mismatch!", Color.Red);
						ChangeBaud(FallbackBaudRate);
						return null;
					}

					addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
					totalTimer.Stop();
					addLogLine($"Read time: {totalTimer.Elapsed}");
				}

				logger.setState("Read done", Color.DarkGreen);
				addLogLine("Read complete!");
				ChangeBaud(FallbackBaudRate);
				return ret;
			}
			catch(Exception ex)
			{
				addError(ex.ToString() + Environment.NewLine);
				logger.setState("Read error", Color.Red);
				ChangeBaud(FallbackBaudRate);
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

