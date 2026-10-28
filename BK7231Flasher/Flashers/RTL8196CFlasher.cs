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
	// Realtek RTL8196C routers (e.g. LT-6408n). The factory boot loader on UART0
	// (38400 8N1) drops to its "<RealTek>" console when it sees ESC during the first
	// seconds after power-on. The stub (OpenRTL8196C examples/31_uart_flasher) is
	// written to RAM with the console's EW command, started with J and then speaks
	// the common ECR stub protocol. Flash below 0x10000 (loader, MAC and RF
	// settings) is never erased or written, the stub refuses it as well.
	public class RTL8196CFlasher : ECRBaseFlasher
	{
		internal static readonly byte CMD_CUSTOM_REBOOT = 0x9F;

		const int LoaderBaud = 38400;
		const int StubBaud = 115200;
		// the stub is linked for cached KSEG0; EW goes through the uncached alias so the
		// words land in RAM instead of the D-cache
		const uint StubUploadAddr = 0xA0500000;
		const uint StubEntry = 0x80500000;
		const int ProtectedEnd = 0x10000;
		const int EraseChunk = 0x40000;
		const int PowerCycleTimeoutSec = 120;
		const int StubBaudFallbackMs = 2500;
		const string LoaderPrompt = "<RealTek>";

		// Which rates work depends on the adapter's and the router's clock errors together
		// (with a CH340: 1000000, 768000, 614400, 400000 do, 921600, 500000, 460800 do not),
		// so they are tried from the fastest down and kept only if commands get through.
		static readonly int[] TransferBauds = { 2000000, 1500000, 1000000, 921600, 768000, 614400, 500000, 460800, 400000, 250000, 230400 };

		public RTL8196CFlasher(CancellationToken ct) : base(ct)
		{
			flashSizeMB = 8;
		}

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				serial = new SerialPort(serialName, StubBaud);
				serial.ReadTimeout = 2000;
				serial.WriteTimeout = 2000;
				serial.Encoding = Encoding.GetEncoding(28591);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
				{
					ReceiverTimeoutMillisec = 1000,
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

		protected override bool Sync()
		{
			if(isCancelled)
				return false;
			if(ReadFlashId(true) != null)
			{
				addLogLine("Stub is already running!");
			}
			else
			{
				serial.BaudRate = LoaderBaud;
				if(!EnterLoader() || !UploadStub())
					return false;
				if(!SetBaud(StubBaud) || ReadFlashId() == null)
				{
					addErrorLine("Stub started, but does not answer at " + StubBaud + " baud!");
					return false;
				}
			}
			if(!CheckChipInfo(data => addLogLine($"UART clock is {MiscUtils.ReadU32LE(data, 4) * 16 / 1e6:F3} MHz")))
				return false;
			return NegotiateBaud();
		}

		bool StubAnswers(int tries)
		{
			for(int i = 0; i < tries; i++)
			{
				if(ExecuteCommand(CMD_CUSTOM_FLASH_ID, null, 0.2f, 4, isErrorExpected: true) == null)
					return false;
			}
			return true;
		}

		// Leaves the stub at StubBaud and sets baudrate to the fastest rate (up to the
		// requested one) at which 20 commands in a row get through.
		bool NegotiateBaud()
		{
			int wanted = baudrate;
			baudrate = StubBaud;
			foreach(int rate in TransferBauds.Where(b => b <= wanted))
			{
				if(isCancelled)
					return false;
				var msg = new byte[4];
				MiscUtils.WriteU32LE(msg, 0, (uint)rate);
				bool ok = ExecuteCommand(CMD_BAUD, msg, 2, 0, rate, isErrorExpected: true) != null && StubAnswers(20);
				if(ok)
				{
					ok = SetBaud(StubBaud);
					if(ok)
						baudrate = rate;
				}
				else
				{
					// ask for StubBaud at the failing rate; if that is lost too, the stub drops
					// back to StubBaud by itself after 2 s without a good command
					MiscUtils.WriteU32LE(msg, 0, StubBaud);
					if(ExecuteCommand(CMD_BAUD, msg, 0.5f, 0, StubBaud, isErrorExpected: true) == null)
						Thread.Sleep(StubBaudFallbackMs);
				}
				if(!StubAnswers(1))
				{
					addErrorLine("Lost the stub while testing baud rates!");
					return false;
				}
				if(ok)
					break;
				addLogLine($"{rate} baud does not work with this adapter.");
			}
			if(baudrate != wanted)
				addWarningLine($"Using {baudrate} baud for transfers instead of {wanted}.");
			else
				addLogLine($"Using {baudrate} baud for transfers.");
			return true;
		}

		bool EnterLoader()
		{
			logger.setState("Waiting for router power-cycle...", Color.Yellow);
			var seen = new StringBuilder();
			serial.DiscardInBuffer();
			serial.Write("\r");
			Thread.Sleep(300);
			seen.Append(serial.ReadExisting());
			if(seen.ToString().Contains(LoaderPrompt))
			{
				addLogLine("Router is already at the loader prompt.");
				return true;
			}
			addWarningLine("Power-cycle the router now: unplug its power supply and plug it back in.");
			addLogLine($"Sending ESC to catch the boot loader, waiting up to {PowerCycleTimeoutSec} s...");
			var sw = Stopwatch.StartNew();
			long received = 0;
			while(sw.Elapsed.TotalSeconds < PowerCycleTimeoutSec)
			{
				if(isCancelled)
					return false;
				serial.Write("\x1b");
				Thread.Sleep(10);
				string chunk = serial.ReadExisting();
				if(received == 0 && chunk.Length > 0)
					addLogLine($"Router is talking ({sw.Elapsed.TotalSeconds:F1} s), waiting for the loader prompt...");
				received += chunk.Length;
				seen.Append(chunk);
				string text = seen.ToString();
				if(text.Contains(LoaderPrompt))
				{
					Thread.Sleep(200);
					serial.DiscardInBuffer();
					logger.addLog("Boot loader caught!" + Environment.NewLine, Color.Green);
					return true;
				}
				if(text.Contains("Jump to image") || text.Contains("decompressing"))
				{
					addWarningLine("The router booted its firmware before the ESC got through, power-cycle it again.");
					seen.Clear();
				}
				if(seen.Length > 4096)
					seen.Remove(0, seen.Length - 64);
			}
			if(received == 0)
				addErrorLine("Nothing was received from the router. Was the power really off for a few seconds? Check that adapter RX goes to router TX and GND to GND.");
			else
				addErrorLine($"No <RealTek> prompt in {received} received bytes, last: " +
					System.Text.RegularExpressions.Regex.Replace(seen.ToString(Math.Max(0, seen.Length - 120), Math.Min(120, seen.Length)), @"[^\x20-\x7e]", "."));
			addErrorLine("Check that adapter TX goes to router RX (ESC must reach the loader), at 38400 baud.");
			return false;
		}

		string LoaderCommand(string line, int timeoutMs = 3000)
		{
			serial.DiscardInBuffer();
			serial.Write(line + "\r");
			var seen = new StringBuilder();
			var sw = Stopwatch.StartNew();
			while(sw.ElapsedMilliseconds < timeoutMs && !isCancelled)
			{
				seen.Append(serial.ReadExisting());
				// the loader echoes the command, the prompt after it ends the reply
				int at = seen.ToString().IndexOf(LoaderPrompt, Math.Min(line.Length, seen.Length));
				if(at >= 0)
					return seen.ToString(0, at);
				Thread.Sleep(1);
			}
			throw new TimeoutException($"No loader prompt after '{line}'");
		}

		Dictionary<uint, uint> DumpWords(uint addr, int words)
		{
			var result = new Dictionary<uint, uint>();
			string text = LoaderCommand($"DW {addr:X8} {words}", 5000 + words * 20);
			foreach(var line in text.Split('\r', '\n'))
			{
				var parts = line.Replace('\t', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if(parts.Length < 2 || !parts[0].EndsWith(":"))
					continue;
				if(!uint.TryParse(parts[0].TrimEnd(':'), System.Globalization.NumberStyles.HexNumber, null, out uint a))
					continue;
				for(int i = 1; i < parts.Length; i++)
				{
					if(uint.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out uint w))
						result[a + (uint)(4 * (i - 1))] = w;
				}
			}
			return result;
		}

		bool UploadStub()
		{
			byte[] dat = FLoaders.GetBinaryFromAssembly($"{chipType}_Stub");
			Array.Resize(ref dat, (dat.Length + 3) & ~3);
			var words = new uint[dat.Length / 4];
			for(int i = 0; i < words.Length; i++)
				words[i] = (uint)(dat[4 * i] << 24 | dat[4 * i + 1] << 16 | dat[4 * i + 2] << 8 | dat[4 * i + 3]);
			try
			{
				LoaderCommand("");
				addLogLine($"Uploading {dat.Length} byte stub to 0x{StubUploadAddr:X8} with EW...");
				logger.setState("Uploading stub...", Color.White);
				const int perLine = 8;
				for(int i = 0; i < words.Length; i += perLine)
				{
					if(isCancelled)
						return false;
					var chunk = words.Skip(i).Take(perLine).Select(w => w.ToString("X8"));
					LoaderCommand($"EW {StubUploadAddr + 4 * (uint)i:X8} " + string.Join(" ", chunk));
					logger.setProgress(i, words.Length);
				}
				addLogLine("Verifying stub...");
				for(int i = 0; i < words.Length; i += 64)
				{
					if(isCancelled)
						return false;
					int n = Math.Min(64, words.Length - i);
					uint baseAddr = StubUploadAddr + 4 * (uint)i;
					var got = DumpWords(baseAddr, n);
					for(int k = 0; k < n; k++)
					{
						if(!got.TryGetValue(baseAddr + 4 * (uint)k, out uint w) || w != words[i + k])
						{
							addErrorLine($"Stub verify failed at 0x{baseAddr + 4 * (uint)k:X8}!");
							return false;
						}
					}
					logger.setProgress(i + n, words.Length);
				}
			}
			catch(TimeoutException ex)
			{
				addErrorLine(ex.Message);
				return false;
			}
			serial.DiscardInBuffer();
			serial.Write($"J {StubEntry:X8}\r");
			var seen = new StringBuilder();
			var sw = Stopwatch.StartNew();
			while(sw.ElapsedMilliseconds < 3000)
			{
				seen.Append(serial.ReadExisting());
				if(seen.ToString().Contains("stub ready"))
				{
					logger.addLog("Stub is running!" + Environment.NewLine, Color.Green);
					return true;
				}
				Thread.Sleep(10);
			}
			addErrorLine("Stub did not start!");
			return false;
		}

		bool EraseRange(int addr, int len)
		{
			logger.setState("Erasing...", Color.White);
			for(int done = 0; done < len && !isCancelled; done += EraseChunk)
			{
				int n = Math.Min(EraseChunk, len - done);
				addLog($"Erasing 0x{addr + done:X}... ");
				var msg = new byte[8];
				MiscUtils.WriteU32LE(msg, 0, (uint)(addr + done));
				MiscUtils.WriteU32LE(msg, 4, (uint)n);
				if(ExecuteCommand(CMD_FLASH_ERASE, msg, 10) == null)
				{
					addErrorLine($"Erase at 0x{addr + done:X} failed!");
					return false;
				}
				logger.setProgress(done + n, len);
			}
			addLogLine();
			return !isCancelled;
		}

		// Sectors that are all 0xFF in the image are only erased, the rest goes over XMODEM.
		bool WriteImage(int addr, byte[] data)
		{
			if(bOverwriteBootloader)
				addWarningLine("Overwrite bootloader is ignored on RTL8196C: the factory loader is the only way back in.");
			if(addr < ProtectedEnd)
			{
				int skip = ProtectedEnd - addr;
				if(data.Length <= skip)
				{
					addErrorLine("Nothing to write above 0x10000.");
					return false;
				}
				addWarningLine($"Skipping the first 0x{skip:X} bytes: flash below 0x10000 (loader, MAC and RF settings) is kept.");
				data = data.Skip(skip).ToArray();
				addr = ProtectedEnd;
			}
			if(addr % BK7231Flasher.SECTOR_SIZE != 0)
			{
				addErrorLine($"Write offset 0x{addr:X} is not sector aligned!");
				return false;
			}
			int sectorCount = (data.Length + BK7231Flasher.SECTOR_SIZE - 1) / BK7231Flasher.SECTOR_SIZE;
			int dataLen = data.Length;
			Array.Resize(ref data, sectorCount * BK7231Flasher.SECTOR_SIZE);
			for(int i = dataLen; i < data.Length; i++)
				data[i] = 0xFF;
			if(addr + data.Length > flashSizeMB * 0x100000)
			{
				addErrorLine("Image does not fit in flash!");
				return false;
			}
			bool IsBlank(int sector)
			{
				for(int i = sector * BK7231Flasher.SECTOR_SIZE; i < (sector + 1) * BK7231Flasher.SECTOR_SIZE; i++)
					if(data[i] != 0xFF)
						return false;
				return true;
			}
			int start = 0;
			while(start < sectorCount && !isCancelled)
			{
				bool blank = IsBlank(start);
				int end = start + 1;
				while(end < sectorCount && IsBlank(end) == blank)
					end++;
				int ofs = start * BK7231Flasher.SECTOR_SIZE;
				int len = (end - start) * BK7231Flasher.SECTOR_SIZE;
				if(blank)
				{
					if(!EraseRange(addr + ofs, len))
						return false;
				}
				else
				{
					var chunk = new byte[len];
					Array.Copy(data, ofs, chunk, 0, len);
					if(!InternalWrite(addr + ofs, chunk))
						return false;
				}
				start = end;
			}
			return !isCancelled;
		}

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
			if(doGenericSetup() == false)
			{
				return false;
			}
			if(!Sync())
			{
				return false;
			}
			int start = bAll ? ProtectedEnd : startSector;
			int end = bAll ? flashSizeMB * 0x100000 : startSector + sectors * BK7231Flasher.SECTOR_SIZE;
			if(start < ProtectedEnd)
			{
				addWarningLine("Flash below 0x10000 (loader, MAC and RF settings) is never erased.");
				start = ProtectedEnd;
			}
			addLogLine($"Erasing 0x{start:X}..0x{end:X}...");
			bool ok = EraseRange(start, end - start);
			if(ok)
				logger.setState("Erase done", Color.DarkGreen);
			return ok;
		}

		public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
		{
			if(doGenericSetup() == false)
			{
				return;
			}
			if(!Sync())
			{
				return;
			}
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
			if(rwMode == WriteMode.OnlyOBKConfig)
			{
				addErrorLine("Writing the OBK config is not supported on RTL8196C yet.");
				return;
			}
			if(string.IsNullOrEmpty(sourceFileName))
			{
				addLogLine("No filename given!");
				return;
			}
			addLogLine("Reading " + sourceFileName + "...");
			byte[] data = File.ReadAllBytes(sourceFileName);
			if(!WriteImage(startSector, data))
			{
				logger.setState("Writing error!", Color.Red);
				return;
			}
			logger.setState("Writing done", Color.DarkGreen);
			addLogLine("Rebooting the router...");
			ExecuteCommand(CMD_CUSTOM_REBOOT, null, 1, isErrorExpected: true);
		}

		protected override bool CheckHash(int addr, int len, byte[] data) => base.CheckCRC(addr, len, data);
	}
}
