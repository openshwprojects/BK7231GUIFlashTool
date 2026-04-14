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
		bool FlashConfigured;
		uint? FlashHashOffset;
		bool IsInFallbackMode = false;
		int flashSizeMB = 2;
		byte[] flashID = { 0, 0, 0 };
		readonly Stack<int> ReadTimeoutStack = new Stack<int>();
		readonly CancellationToken _ct;
		const int ReadUnitSize = 0x1000;
		const int VerifyWindowSize = 256 * 1024;
		const int ReadChunkRetryLimit = 3;
		const int ReadWindowRetryLimit = 3;
		const int WriteWindowRetryLimit = 3;
		const int HashRetryLimit = 3;
		const int CommandRetryLimit = 3;
		const int FallbackBaudRate = 115200;
		const string InternalBuildId = "rtlz2-resiliency-r26";

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

		string FormatProgressAddress(uint address, bool mapped)
		{
			uint displayAddr = mapped ? FLASH_MMAP_BASE + address : address;
			string fmt = mapped ? "X8" : "X6";
			return $"0x{displayAddr.ToString(fmt)}... ";
		}

		void EndProgressLineIfNeeded()
		{
			if(HasOpenProgressLine)
			{
				addLogLine(string.Empty);
				HasOpenProgressLine = false;
			}
		}

		int LogProgressAddress(uint address, bool mapped, int itemsOnLine)
		{
			addLog(FormatProgressAddress(address, mapped));
			HasOpenProgressLine = true;
			itemsOnLine++;
			return itemsOnLine;
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

		void WdtDisableRaw()
		{
			// Mirror PGTool: send WDT disable before baud change to prevent chip reset
			// during the protocol transition window. Raw write (not via Command/RegisterWrite)
			// to avoid IsInFallbackMode echo-read side-effects at this point in the flow.
			try
			{
				var wdtCmd = Encoding.ASCII.GetBytes("EW 40002800 7EFFFFFF\n");
				serial.Write(wdtCmd, 0, wdtCmd.Length);
				Thread.Sleep(50);
				try { serial.ReadExisting(); } catch { }
			}
			catch { }
		}

		bool TryChangeBaudOnce(int fromBaud, int toBaud, int txIdleMs, int oldReadMs, int newReadMs)
		{
			serial.BaudRate = fromBaud;
			Flush();
			if(!Link())
			{
				return false;
			}
			// Disable WDT before baud change (matches PGTool: sent immediately before ucfg).
			WdtDisableRaw();
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

		// Flash-read variant of DumpWords: takes a byte count (must be a multiple of 4),
		// calls DumpWords with the mapped flash address, and converts the returned 32-bit words
		// to a byte array. ARM is little-endian, so BitConverter.GetBytes(uint) preserves byte
		// order correctly on any LE host (x86/x64 Windows or Linux).
		bool DumpFlashWords(uint start, int byteCount, out byte[] bytes)
		{
			int wordCount = byteCount / 4;
			if(!DumpWords(start, wordCount, out var words) || words == null || words.Length != wordCount)
			{
				bytes = null;
				return false;
			}
			bytes = new byte[wordCount * 4];
			for(int i = 0; i < wordCount; i++)
				Buffer.BlockCopy(BitConverter.GetBytes(words[i]), 0, bytes, i * 4, 4);
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

		bool WriteWindow(byte[] window, uint offset, int progressBase, int progressTotal)
		{
			int itemsOnLine = 0;
			uint nextLoggedOffset = 0;
			XMODEM.PacketSentEventHandler progressHandler =
				(sentBytes, total, seq, off) =>
				{
					logger.setProgress(progressBase + sentBytes, progressTotal);
					while(nextLoggedOffset < sentBytes && nextLoggedOffset < (uint)window.Length)
					{
						itemsOnLine = LogProgressAddress(offset + nextLoggedOffset, false, itemsOnLine);
						nextLoggedOffset += ReadUnitSize;
					}
				};
			xm.PacketSent += progressHandler;
			try
			{
				for(int attempt = 1; attempt <= WriteWindowRetryLimit; attempt++)
				{
					_ct.ThrowIfCancellationRequested();
					logger.setState("Writing...", Color.Transparent);
					itemsOnLine = 0;
					nextLoggedOffset = 0;
					EndProgressLineIfNeeded();
					if(attempt == 1)
						addLogLine($"Writing {window.Length / 1024}KiB to 0x{offset:X6}");
					else
						addLogLine($"Retrying write 0x{offset:X6} (attempt {attempt}/{WriteWindowRetryLimit})");
					using(var stream = new MemoryStream(window, false))
					{
						if(!RunWithRecovery("Flash write", CommandRetryLimit, () => FlashTransmit(stream, offset)))
						{
							EndProgressLineIfNeeded();
							itemsOnLine = 0;
							if(attempt == WriteWindowRetryLimit)
							{
								return false;
							}
							continue;
						}
					}
					while(nextLoggedOffset < (uint)window.Length)
					{
						itemsOnLine = LogProgressAddress(offset + nextLoggedOffset, false, itemsOnLine);
						nextLoggedOffset += ReadUnitSize;
					}
					EndProgressLineIfNeeded();
					itemsOnLine = 0;
					logger.setState("Verifying write...", Color.Transparent);
					addLogLine($"Verifying 0x{offset:X6} len 0x{window.Length:X}...");
					if(VerifyFlashWindow(window, offset))
					{
						if(attempt > 1)
						{
							addLogLine($"Write window recovered at 0x{offset:X6} on attempt {attempt}/{WriteWindowRetryLimit}");
						}
						addLogLine($"Write verified OK at 0x{offset:X6}");
						logger.setState("Writing...", Color.Transparent);
						return true;
					}

					addWarningLine($"Write verify failed at 0x{offset:X6}");
					if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
					{
						addWarningLine($"Lowering baud rate to {FallbackBaudRate} for write recovery");
						ChangeBaud(FallbackBaudRate);
					}
					try { Flush(); } catch { }
					try { Link(); } catch(OperationCanceledException) { throw; } catch { }
					if(attempt < WriteWindowRetryLimit)
					{
						addWarningLine($"Retrying write window 0x{offset:X6} ({attempt + 1}/{WriteWindowRetryLimit})");
					}
				}
				return false;
			}
			finally
			{
				xm.PacketSent -= progressHandler;
			}
		}


		bool WriteFlashWindows(byte[] data, uint offset)
		{
			EnsureWindowBounds(offset, data.Length);
			int done = 0;
			while(done < data.Length)
			{
				_ct.ThrowIfCancellationRequested();
				int windowLen = Math.Min(VerifyWindowSize, data.Length - done);
				var window = new byte[windowLen];
				Buffer.BlockCopy(data, done, window, 0, windowLen);
				if(!WriteWindow(window, offset + (uint)done, done, data.Length))
				{
					addErrorLine($"Write window failed at 0x{offset + (uint)done:X6}");
					return false;
				}
				done += windowLen;
				// Snap to exact position after successful verify
				logger.setProgress(done, data.Length);
			}
			return true;
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
			switch(pin)
			{
				case 0: return "0 (PIN_A7_A12 - default)";
				case 1: return "1 (PIN_B6_B12)";
				default: return $"{pin} (reserved - not reliably supported by PGTool autodetect)";
			}
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

		bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			addLog("Engine: " + InternalBuildId + Environment.NewLine);
			if(serial == null || !serial.IsOpen)
			{
				try
				{
					serial = new SerialPort(serialName, 115200);
					serial.ReadBufferSize = 1024 * 1024;
					serial.WriteBufferSize = 1024 * 256;
					serial.Open();
				}
				catch(Exception ex)
				{
					ReportSerialOpenFailure(ex);
					return false;
				}
			}
			Flush();
			serial.ReadTimeout = 5000;
			serial.WriteTimeout = 5000;
			addLog("Port ready!" + Environment.NewLine);
			xm = new XMODEM(serial, XMODEM.Variants.XModem1KChecksum, 0xFF);
			if(Link() == false)
			{
				// Close port regardless of whether we just opened it, so the GUI
				// releases the COM port and re-enables buttons on link failure.
				logger.setState("Link failed!", Color.Red);
				closePort();
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

				if(ChangeBaud(baudrate) == false)
				{
					closePort();
					return true;
				}
				if(mode != WriteMode.OnlyOBKConfig)
				{
					uint writeOffset = (uint)(address & 0x00ffffff);
					addLog(string.Format("Write Flash data 0x{0:X8} to 0x{1:X8}", writeOffset, writeOffset + size) + Environment.NewLine);

					if(!WriteFlashWindows(data, (uint)(startSector * 0x1000)))
					{
						addLog("Error: Write Flash!" + Environment.NewLine);
						try { if(serial != null) serial.BaudRate = FallbackBaudRate; } catch { } // write failed; chip state unknown, skip Link()
						closePort();
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
						closePort();
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
						closePort();
						return false;
					}
					addSuccess("OBK config write success!" + Environment.NewLine);
				}
				else
				{
					addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
				}
				logger.setProgress(size, size);
				addSuccess("Flash complete!" + Environment.NewLine);
				SetWriteCompleteState();
				ChangeBaud(115200);
				return false;
			}
			catch(OperationCanceledException)
			{
				LogCancelledOperation();
				try { if(serial != null) serial.BaudRate = FallbackBaudRate; } catch { } // chip may be gone; skip Link()
				closePort();
				return true;
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
			if(bytes == null)
			{
				throw new Exception("Failed to read flash ID from register dump");
			}
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
				try
				{
					ReadFlashId();
				}
				catch(OperationCanceledException)
				{
					LogCancelledOperation();
					closePort();
					return;
				}
				catch(Exception ex)
				{
					addErrorLine($"Flash ID read failed: {ex.Message}");
					logger.setState("Read error", Color.Red);
					closePort();
					return;
				}
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

		byte[] ReadVerifiedWindow(uint startAddr, int windowLength, int progressBase, int progressTotal)
		{
			EnsureWindowBounds(startAddr, windowLength);
			for(int attempt = 1; attempt <= ReadWindowRetryLimit; attempt++)
			{
				_ct.ThrowIfCancellationRequested();
				logger.setState("Reading...", Color.Transparent);
				var window = new byte[windowLength];
				int copied = 0;
				bool failed = false;
				int itemsOnLine = 0;
				for(int chunkOffset = 0; chunkOffset < windowLength; chunkOffset += ReadUnitSize)
				{
					_ct.ThrowIfCancellationRequested();
					int chunkLength = Math.Min(ReadUnitSize, windowLength - chunkOffset);
					uint chunkAddr = startAddr + (uint)chunkOffset;
					byte[] chunk = null;
					for(int chunkAttempt = 1; chunkAttempt <= ReadChunkRetryLimit; chunkAttempt++)
					{
						if(RunWithRecovery($"Read 0x{chunkAddr:X6}", CommandRetryLimit, () => DumpFlashWords(chunkAddr | FLASH_MMAP_BASE, chunkLength, out chunk)))
						{
							if(chunkAttempt > 1)
							{
								EndProgressLineIfNeeded();
								addLogLine($"Read retry succeeded at 0x{chunkAddr:X6} on attempt {chunkAttempt}/{ReadChunkRetryLimit}");
							}
							break;
						}
						if(chunkAttempt < ReadChunkRetryLimit)
						{
							EndProgressLineIfNeeded();
							addWarningLine($"Read retry at 0x{chunkAddr:X6} ({chunkAttempt + 1}/{ReadChunkRetryLimit})");
							Thread.Sleep(75);
						}
					}
					if(chunk == null || chunk.Length != chunkLength)
					{
						EndProgressLineIfNeeded();
						addWarningLine($"Read failed at 0x{chunkAddr:X6}");
						failed = true;
						break;
					}
					Buffer.BlockCopy(chunk, 0, window, copied, chunkLength);
					itemsOnLine = LogProgressAddress(chunkAddr, false, itemsOnLine);
					copied += chunkLength;
					logger.setProgress(progressBase + copied, progressTotal);
				}

				EndProgressLineIfNeeded();
				itemsOnLine = 0;

				if(failed)
				{
					if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
					{
						addWarningLine($"Lowering baud rate to {FallbackBaudRate} for read recovery");
						ChangeBaud(FallbackBaudRate);
					}
					if(attempt < ReadWindowRetryLimit)
					{
						addWarningLine($"Retrying read window 0x{startAddr:X6} ({attempt + 1}/{ReadWindowRetryLimit})");
					}
					continue;
				}

				logger.setState("Verifying read...", Color.Transparent);
				addLogLine($"Verifying read window 0x{startAddr:X6} len 0x{windowLength:X}");
				if(VerifyFlashWindow(window, startAddr))
				{
					if(attempt > 1)
					{
						addLogLine($"Read window recovered at 0x{startAddr:X6} on attempt {attempt}/{ReadWindowRetryLimit}");
					}
					addLogLine($"Read window verified at 0x{startAddr:X6} len 0x{windowLength:X}");
					logger.setState("Reading...", Color.Transparent);
					return window;
				}

				addWarningLine($"Read verify failed at 0x{startAddr:X6} len 0x{windowLength:X} attempt {attempt}/{ReadWindowRetryLimit}");
				try { Flush(); } catch { }
				try { Link(); } catch(OperationCanceledException) { throw; } catch { }
				if(attempt == 2 && serial.BaudRate > FallbackBaudRate)
				{
					addWarningLine($"Lowering baud rate to {FallbackBaudRate} for read recovery");
					ChangeBaud(FallbackBaudRate);
				}
				if(attempt < ReadWindowRetryLimit)
				{
					addWarningLine($"Retrying read window 0x{startAddr:X6} ({attempt + 1}/{ReadWindowRetryLimit})");
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
					closePort();
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
						_ct.ThrowIfCancellationRequested();
						int windowLength = Math.Min(VerifyWindowSize, remaining);
						var windowTimer = Stopwatch.StartNew();
						var window = ReadVerifiedWindow(currentAddr, windowLength, copied, amount);
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
						closePort();
						return null;
					}

					addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
					totalTimer.Stop();
					addLogLine($"Read time: {totalTimer.Elapsed}");
				}

				SetReadCompleteState();
				addLogLine("Read complete!");
				ChangeBaud(FallbackBaudRate);
				return ret;
			}
			catch(OperationCanceledException)
			{
				LogCancelledOperation();
				try { if(serial != null) serial.BaudRate = FallbackBaudRate; } catch { } // chip may be gone; skip Link()
				closePort();
				return null;
			}
			catch(Exception ex)
			{
				addError(ex.ToString() + Environment.NewLine);
				logger.setState("Read error", Color.Red);
				try { if(serial != null) serial.BaudRate = FallbackBaudRate; } catch { } // chip may be gone; skip Link()
			}
			closePort();
			return null;
		}

		public bool doReadInternal(int startSector, int sectors)
		{
			byte[] res = readFlash(startSector * 0x1000, sectors * 0x1000);
			ms = res != null ? new MemoryStream(res) : null;
			return ms == null;
		}

		public override byte[] getReadResult()
		{
			return ms?.ToArray() ?? null;
		}

		public override bool doErase(int startSector, int sectors, bool bAll)
		{
			try
			{
				_ct.ThrowIfCancellationRequested();
				if(doGenericSetup() == false)
				{
					return false;
				}
				// Detect flash pin and disable WDT.
				// configure:false skips FlashHashOffset pre-read — not needed for erase.
				FlashInit(configure: false);

				bool result;
				if(bAll)
				{
					addLogLine($"Chip erase: ceras 0 {FlashMode}");
					logger.setState("Erasing chip...", Color.Orange);
					result = RunWithRecovery("Chip erase", 1, () => SendEraseCommand($"ceras 0 {FlashMode}"));
				}
				else
				{
					// Sector erase is not yet wired to any GUI action.
					// The seras command is valid on the chip but leave it stubbed
					// until there is a concrete use case and test path.
					addErrorLine("Sector erase is not implemented in this build.");
					logger.setState("Erase failed!", Color.Red);
					closePort();
					return false;
				}

				if(result)
				{
					SetEraseCompleteState();
				}
				else
				{
					logger.setState("Erase failed!", Color.Red);
				}
				closePort();
				return result;
			}
			catch(OperationCanceledException)
			{
				LogCancelledOperation();
				try { if(serial != null) serial.BaudRate = FallbackBaudRate; } catch { }
				closePort();
				return false;
			}
			catch(Exception ex)
			{
				addErrorLine($"Erase failed: {ex.Message}");
				logger.setState("Erase failed!", Color.Red);
				closePort();
				return false;
			}
		}

		bool SendEraseCommand(string eraseCmd)
		{
			_ct.ThrowIfCancellationRequested();
			Command(eraseCmd);
			// Poll for 2-byte ACK ("OK", 0x4F 0x4B) with short read slices so the
			// cancellation token is checked regularly. PGTool allows up to 60 seconds
			// for the erase to complete before treating it as a failure.
			var deadline = DateTime.Now.AddSeconds(60);
			PushReadTimeout(200);
			try
			{
				var buf = new byte[2];
				int got = 0;
				while(got < 2)
				{
					_ct.ThrowIfCancellationRequested();
					if(DateTime.Now > deadline)
						throw new Exception("Erase timed out waiting for ACK after 60 seconds");
					try
					{
						int n = serial.Read(buf, got, 2 - got);
						if(n > 0) got += n;
					}
					catch(TimeoutException) { continue; }
				}
				if(buf[0] != 0x4F || buf[1] != 0x4B)
				{
					throw new Exception($"Unexpected erase ACK: {BitConverter.ToString(buf)}");
				}
				addLogLine("Erase ACK: OK");
				return true;
			}
			finally
			{
				PopReadTimeout();
			}
		}

		public override void closePort()
		{
			if(serial != null)
			{
				try { serial.Close(); } catch { }
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

