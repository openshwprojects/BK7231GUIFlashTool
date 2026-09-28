using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
	public class RTLZ2Flasher : ECRBaseFlasher, IRomReadFlasher
	{
		private readonly List<string> USED_COMMANDS = new List<string>() 
		{
			"ping","disc","ucfg","DW","DB","EW","EB","WDTRST","hashq","fwd","fwdram"
		};
		readonly uint FLASH_MMAP_BASE = 0x98000000;
		uint? FuncPtr = null;
		string FuncName = string.Empty;
		int? FlashMode;
		bool FlashConfigured;
		uint? FlashHashOffset;
		bool IsInFallbackMode = false;
		readonly Stack<int> ReadTimeoutStack = new Stack<int>();
		readonly CancellationToken _ct;
		const int HashRetryLimit = 3;
		const int CommandRetryLimit = 3;

		public RTLZ2Flasher(CancellationToken ct) : base(ct)
		{
			_ct = ct;
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
				catch(OperationCanceledException)
				{
					throw;
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

		bool RunWithRecovery(string label, int attempts, Func<bool> action)
		{
			for(int attempt = 1; attempt <= attempts; attempt++)
			{
				try
				{
					if(action())
					{
						if(attempt > 1)
						{
							EndProgressLineIfNeeded();
							addLogLine($"{label} recovered on attempt {attempt}/{attempts}");
						}
						return true;
					}
				}
				catch(OperationCanceledException)
				{
					throw; // never swallow cancellation
				}
				catch(Exception ex)
				{
					if(attempt >= attempts)
					{
						EndProgressLineIfNeeded();
						addErrorLine($"{label} failed after {attempts} attempts: {ex.Message}");
						return false;
					}
					EndProgressLineIfNeeded();
					addWarningLine($"{label} retrying attempt {attempt + 1}/{attempts}: {ex.Message}");
				}
				try { Flush(); } catch { }
				try { Link(); } catch(OperationCanceledException) { throw; } catch { }
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
						if(attempt > 1)
						{
							EndProgressLineIfNeeded();
							addLogLine($"{label} recovered on attempt {attempt}/{attempts}");
						}
						return result;
					}
					last = new Exception("No data returned");
				}
				catch(OperationCanceledException)
				{
					throw; // never swallow cancellation
				}
				catch(Exception ex)
				{
					last = ex;
				}
				if(attempt < attempts)
				{
					EndProgressLineIfNeeded();
					addWarningLine($"{label} retrying attempt {attempt + 1}/{attempts}: {last.Message}");
					try { Flush(); } catch { }
					try { Link(); } catch(OperationCanceledException) { throw; } catch { }
					Thread.Sleep(50);
				}
			}
			if(last != null)
			{
				EndProgressLineIfNeeded();
				addErrorLine($"{label} failed after {attempts} attempts: {last.Message}");
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

		bool HasOpenProgressLine;

		void EndProgressLineIfNeeded()
		{
			if(HasOpenProgressLine)
			{
				addLogLine(string.Empty);
				HasOpenProgressLine = false;
			}
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

		bool TryPingLink(int timeoutMs)
		{
			var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
			while(DateTime.Now < deadline)
			{
				_ct.ThrowIfCancellationRequested();
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
			return false;
		}

		bool Link()
		{
			if(TryPingLink(400))
			{
				return true;
			}
			LinkFallback();
			if(TryPingLink(10000))
			{
				return true;
			}
			addErrorLine("Ping response is incorrect");
			addErrorLine("Link failed!");
			return false;
		}

		bool LinkFallback()
		{
			Command("Rtk8710C");
			var resp = ReadWithTimeout(100);
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

		bool DumpBytes(uint start, int count, out byte[] bytes)
		{
			EnsureWindowBounds(start & 0x00FFFFFF, count);
			int readCount = 0;
			var data = new List<byte>(count);
			Command($"DB {start:X} {count}");
			// DB outputs ~3.7 ASCII chars per binary byte (hex digits + spaces + address).
			// Deadline must account for actual wire time at the current baud rate,
			// otherwise 115200 baud 4KB reads (1311ms on wire) time out at the old 1024ms floor.
			var deadline = DateTime.Now.AddMilliseconds(
				Math.Max(1500, count * 37000 / serial.BaudRate + 500));
			PushReadTimeout(500);
			try
			{
				while(readCount < count && DateTime.Now < deadline)
				{
					_ct.ThrowIfCancellationRequested();
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

		bool DumpWords(uint start, int count, out uint[] words)
		{
			int bytesRead = 0;
			int expectedBytes = count * 4;
			var data = new List<uint>(Math.Max(1, count));
			Command($"DW {start:X} {count}");
			// DW outputs ~11.75 ASCII chars per 4-byte word (address + 4 hex words per line + separators).
			// Use a baud-aware deadline matching DumpBytes to avoid false timeouts at any baud rate.
			var deadline = DateTime.Now.AddMilliseconds(
				Math.Max(1500, count * 118000 / serial.BaudRate + 500));
			PushReadTimeout(500);
			try
			{
				while(bytesRead < expectedBytes && DateTime.Now < deadline)
				{
					_ct.ThrowIfCancellationRequested();
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
					if(addr != start + bytesRead)
					{
						throw new Exception("Unexpected word dump address");
					}
					if(parts.Length < 5)
					{
						throw new Exception("Incomplete word dump line");
					}

					for(int i = 1; i < 5 && bytesRead < expectedBytes; i++)
					{
						// uint.TryParse handles the full 32-bit range (0x00000000–0xFFFFFFFF).
						// int.TryParse would silently fail for values >= 0x80000000, corrupting
						// flash data with high bits set.
						if(!uint.TryParse(parts[i].Trim('\r'), System.Globalization.NumberStyles.HexNumber, null, out var value))
						{
							throw new Exception("Invalid word dump data");
						}
						data.Add(value);
						bytesRead += 4;
					}
				}
			}
			finally
			{
				PopReadTimeout();
			}

			if(bytesRead != expectedBytes || data.Count != count)
			{
				words = null;
				return false;
			}

			words = data.ToArray();
			return true;
		}

		int RegisterRead(uint addr)
		{
			uint start = addr & ~0xFU;
			if(!DumpWords(start, 4, out var words) || words == null || words.Length != 4)
			{
				throw new Exception($"Register read failed at 0x{addr:X}");
			}
			int index = (int)((addr - start) >> 2);
			if(index < 0 || index >= words.Length)
			{
				throw new Exception($"Register read index failed at 0x{addr:X}");
			}
			return (int)words[index];
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
					if(nameBytes == null)
						continue;
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
			// Poll for the 38-byte hash response ("hashs " + 32-byte SHA256) in 200ms slices
			// so the cancellation token is checked regularly. The chip may take up to
			// several minutes to compute a hash over a large flash region.
			var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
			PushReadTimeout(200);
			try
			{
				var buf = new byte[38];
				int got = 0;
				while(got < 38)
				{
					_ct.ThrowIfCancellationRequested();
					if(DateTime.Now > deadline)
						throw new Exception("Hash response timed out");
					try
					{
						int n = serial.Read(buf, got, 38 - got);
						if(n > 0) got += n;
					}
					catch(TimeoutException) { continue; }
				}
				var prefix = Encoding.ASCII.GetString(buf, 0, 6);
				if(prefix != "hashs ")
				{
					throw new Exception($"Unexpected response to hashq: {Encoding.ASCII.GetString(buf)}");
				}
				return buf.Skip(6).Take(32).ToArray();
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

		bool RamTransmit(byte[] data, uint offset)
		{
			PushReadTimeout(3000);
			try
			{
				Command($"fwdram {offset:x}");
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
				xm.PacketSent += Xm_PacketSent;
				var res = xm.Send(data, offset);
				xm.PacketSent -= Xm_PacketSent;
				addLogLine();
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

		static string FlashPinName(int pin)
		{
			// RTL8720C register 0x40000038 bits[6:5] = FLASH_PIN_SEL.
			// Pin group names proven from decompiled mainwindow.baml (comboBoxFlashPin item order):
			//   0 = PIN_A7_A12  (default, comboBox index 0)
			//   1 = PIN_B6_B12  (comboBox index 1)
			//   2 = PIN_A15_A20 (comboBox index 2)
			//
			// Important caveat: PGTool's ParseFlashPinSel() extracts bits 6:5 as a binary
			// string ("00"/"01"/"10"/"11") but then calls int.Parse() on it as if it were
			// decimal text. This means values 2 and 3 (binary "10"/"11") would parse as 10
			// and 11 — out of range for the comboBox. Only values 0 and 1 are reliably
			// supported by PGTool's own autodetect. Treat 2+ as reserved/unverified on
			// actual silicon until confirmed by vendor.
			return pin switch
			{
				0 => "0 (PIN_A7_A12 - default)",
				1 => "1 (PIN_B6_B12)",
				_ => $"{pin} (reserved - not reliably supported by PGTool autodetect)",
			};
		}

		void FlashInit(bool configure = true)
		{
			if(FlashMode == null)
			{
				FlashMode = (RegisterRead(0x40000038) >> 5) & 0b11;
				addLogLine($"Flash pin detected: {FlashPinName(FlashMode.Value)}");
			}
			if(!FlashConfigured)
			{
				// Disable WDT (register 0x40002800 = WDT_CTRL, value 0x7EFFFFFF disables it).
				// Guarded by FlashConfigured so it only runs once per session.
				RegisterWrite(0x40002800, 0x7EFFFFFF);
				FlashConfigured = true;
			}
			if(configure && FlashHashOffset == null)
			{
				FlashReadHash(0, 0);
			}
		}

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				serial = new SerialPort(serialName, 115200)
				{
					ReadBufferSize = 65536,
					ReadTimeout = 8000
				};
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				xm = new XMODEM(serial, XMODEM.Variants.XModem1KChecksum, 0xFF)
				{
					MaxSenderRetries = 20,
					ReceiverMaxConsecutiveRetries = 20
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

		public byte[] ReadRomTarget(RomReadTarget target)
		{
			try
			{
				if(doGenericSetup() == false)
				{
					return null;
				}
				if(!Sync())
				{
					return null;
				}

				string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
				switch(target.Kind)
				{
					case RomReadKind.Rom:
						return InternalReadRawMemory(target.Address ?? 0, target.Length ?? 384 * 1024, targetKindName);
					case RomReadKind.Otp:
						return InternalReadEfusePayload(target.Length ?? -1, targetKindName, true);
					case RomReadKind.Efuse:
						return InternalReadEfusePayload(target.Length ?? 512, targetKindName);
					default:
						addError("Selected RTL87X0C ROM reader target is not implemented." + Environment.NewLine);
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
		}

		public override void closePort()
		{
			if(serial != null)
			{
				try { if(serial.IsOpen) serial.Close(); } catch { }
				try { serial.Dispose(); } catch { }
				serial = null;
			}
			FlashMode = null;
			FlashConfigured = false;
			FlashHashOffset = null;
			FuncPtr = null;
			FuncName = string.Empty;
			IsInFallbackMode = false;
			ReadTimeoutStack.Clear();
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
					sectors = flashSizeMB * 256;
					byte[] res = InternalRead(startSector, sectors);
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
					if(!InternalWrite(startSector, data)) return;
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null && !isCancelled)
				{
					if(cfg != null)
					{
						cfg.saveConfig(chipType);
						var cfgData = cfg.getData();

						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);

						var offset = OBKFlashLayout.getConfigLocation(chipType, out var efsectors);
						var areaSize = efsectors * BK7231Flasher.SECTOR_SIZE;
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
						ms?.Dispose();
						ms = new MemoryStream(efdata);
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

		protected override bool Sync()
		{
			if(isCancelled)
				return false;
			var flashID = ReadFlashId(true);
			if(flashID != null)
			{
				if(!CheckChipInfo(PrintChipInfo)) return false;
				addLogLine("Stub is already uploaded!");
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF);
				return true;
			}
			serial.Write("\r\n");
			if(!Link())
				return false;
			FlashInit();
			addLogLine("Uploading stub...");
			var stub = FLoaders.GetBinaryFromAssembly("RTL8710C_Stub");
			if(!RamTransmit(stub, 0x10001000))
				return false;
			if(!MemoryBoot(0x10001000))
				return false;
			Thread.Sleep(10);
			flashID = ReadFlashId();
			if(flashID != null)
			{
				if(!CheckChipInfo(PrintChipInfo)) return false;
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF);
				return true;
			}
			return false;
		}

		private void PrintChipInfo(byte[] data)
		{
			var efuseChip = MiscUtils.ReadU32LE(data, 4) & 0xFF;
			var chipInfo = efuseChip switch
			{
				0xFE => "RTL87x0CF",
				0xFD => "RTL87x0CM",
				_ => $"Unknown ${efuseChip}"
			};
			var syscfg0 = MiscUtils.ReadU32LE(data, 8);
			addLogLine($"Chip variant: {chipInfo}, chip VID: {syscfg0 >> 8 & 0xF}, chip version: {syscfg0 >> 4 & 0xF}");
		}
	}
}

