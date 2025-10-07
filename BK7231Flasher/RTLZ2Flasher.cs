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

		void Flush()
		{
			serial.DiscardInBuffer();
			serial.DiscardOutBuffer();
		}

		void Command(string cmd)
		{
			Flush();
			//addLogLine($">>> {cmd}");
			var cmdascii = Encoding.ASCII.GetBytes($"{cmd}\n");
			serial.Write(cmdascii, 0, cmdascii.Length);
			if(IsInFallbackMode)
				serial.ReadLine();
		}

		bool Link()
		{
			LinkFallback();
			Command("ping");
			byte[] bytes = { 0, 0, 0, 0 };
			Thread.Sleep(25);
			try
			{
				serial.Read(bytes, 0, 4);
			}
			catch { }

			if(Encoding.ASCII.GetString(bytes) != "ping")
			{
				addErrorLine("Ping response is incorrect");
				addErrorLine("Link failed!");
				return false;
			}
			var extra = string.Empty;
			try
			{
				extra = serial.ReadExisting();
			}
			catch { }
			if(extra.Length > 0)
			{
				//addLogLine($"<<< {extra}");
				if(extra.Contains("$8710c"))
				{
					addErrorLine("Ping fallback");
					addErrorLine("Link failed!");
					return false;
				}
			}
			return true;
		}

		bool LinkFallback()
		{
			Command("Rtk8710C");
			Thread.Sleep(100);
			string resp = string.Empty;
			try
			{
				resp = serial.ReadExisting();
			}
			catch { }
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
			if(baud != baudrate)
				return Link();
			addLogLine($"Setting baud rate to {baud}");
			Command($"ucfg {baud} 0 0");
			Thread.Sleep(500);
			serial.BaudRate = baud;
			Flush();
			return Link();
		}

		bool DumpBytes(uint start, int count, out byte[] bytes)
		{
			int readCount = 0;
			Command($"DB {start:X} {count}");
			//Thread.Sleep(20);
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
				//addLogLine($"<<< {line}");
				var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
				if(parts.Length == 0)
					continue;
				if(parts[0] == "[Addr]")
					continue;
				uint addr;
				try
				{
					if(parts[0] == "\r")
						continue;
					addr = Convert.ToUInt32(parts[0].Trim(':', '\r', '\n'), 16);
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
			return true;
		}

		bool DumpWords(uint start, int count, out int[] ints)
		{
			int readCount = 0;
			Command($"DW {start:X} {count}");
			Thread.Sleep(25);
			var lbytes = new List<int>();
			while(readCount < count)
			{
				var line = serial.ReadLine();
				//addLogLine($"<<< {line}");
				var parts = line.Split(' ').Where(x => x != string.Empty).ToArray();
				if(parts.Length == 0)
					continue;
				uint addr;
				try
				{
					if(parts[0] == "\r")
						continue;
					addr = Convert.ToUInt32(parts[0].Trim(':', '\r', '\n'), 16);
				}
				catch { continue; }
				if(addr != start + readCount)
					throw new Exception();
				if(parts.Length < 5)
					throw new Exception();
				for(int i = 1; i < 5; i++)
				{
					var val = Convert.ToInt32(parts[i].Trim('\r'), 16);
					lbytes.Add(val);
					readCount+=4;
					if(readCount >= count)
						break;
				}
				if(readCount >= count)
					break;
			}
			ints = lbytes.ToArray();
			return true;
		}

		int RegisterRead(uint addr)
		{
			DumpWords((uint)(addr & ~0x3), 4, out var shorts);
			return shorts[addr - addr & ~0x3];
		}

		void RegisterWrite(uint addr, uint value)
		{
			Command($"EW {addr:X} {value:X}");
			Thread.Sleep(5);
			serial.ReadLine();
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

		bool FlashTransmit(Stream data, uint offset)
		{
			FlashInit(false);
			//Thread.Sleep(1000);
			Command($"fwd 0 {FlashMode} {offset:x}");
			FlashHashOffset = offset;
			if(data == null)
			{
				var resp = serial.ReadByte();
				if(resp != '\x15')
					throw new Exception($"expected NAK, got {resp}");
				serial.Write("\x18");
				Flush();
				Thread.Sleep(10);
				var bytes = new char[3];
				serial.Read(bytes, 0, 3);
				if(bytes[0] != 24 && bytes[1] != 'E' && bytes[2] != 'R')
					throw new Exception($"expected CAN, got {new string(bytes)}");
				return true;
			}
			else return SendXmodem(data, offset, (int)data.Length, 3);
		}

		byte[] FlashReadHash(uint offset, int length)
		{
			if(FlashHashOffset != offset)
			{
				FlashTransmit(null, offset);
			}
			Command($"hashq {length} 0 {FlashMode}");
			var timeout = Math.Ceiling((double)length / 1500 * 10) / 10;
			Thread.Sleep((int)timeout + 1);
			var bytes = new char[6 + 32];
			for(int i = 0; i < 6 + 32; i++)
			{
				bytes[i] = (char)serial.ReadByte();
			}
			if(!new string(bytes).StartsWith("hashs "))
				throw new Exception($"Unexpected response to 'hashq', got {new string(bytes)}");
			return bytes.Skip(6).Select(c => (byte)c).ToArray();
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
				serial.Open();
			}
			Flush();
			serial.ReadTimeout = 5000;
			addLog("Port ready!" + Environment.NewLine);
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
				OBKConfig cfg;
				if(mode == WriteMode.OnlyOBKConfig)
				{
					cfg = logger.getConfig();
				}
				else
				{
					cfg = logger.getConfigToWrite();
				}

				int size = numSectors * BK7231Flasher.SECTOR_SIZE;
				if(data != null)
				{
					size = data.Length;
				}
				logger.setProgress(0, size);
				addLog(Environment.NewLine + "Starting write!" + Environment.NewLine);
				addLog("Write parms: start 0x" +
					(startSector).ToString("X2")
					+ " (sector " + startSector / BK7231Flasher.SECTOR_SIZE + "), len 0x" +
					(size).ToString("X2")
					+ " (" + startSector + " sectors)"
					+ Environment.NewLine);
				Console.WriteLine("Connected");
				if(mode == WriteMode.ReadAndWrite)
				{
					doRead(startSector, numSectors, true);
					if(ms == null)
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
					writeOffset |= FLASH_MMAP_BASE;
					addLog(string.Format("Write Flash data 0x{0:X8} to 0x{1:X8}", writeOffset, writeOffset + size) + Environment.NewLine);

					var ms = new MemoryStream(data);
					if(!FlashTransmit(ms, (uint)(startSector * 0x1000) | FLASH_MMAP_BASE))
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

					byte[] efdata;
					if(cfg.efdata != null)
					{
						try
						{
							efdata = EasyFlash.SaveCfgToExistingEasyFlash(cfg, areaSize, chipType);
						}
						catch(Exception ex)
						{
							addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
							addLog(ex.Message + Environment.NewLine);
							efdata = EasyFlash.SaveCfgToNewEasyFlash(cfg, areaSize, chipType);
						}
					}
					else
					{
						efdata = EasyFlash.SaveCfgToNewEasyFlash(cfg, areaSize, chipType);
					}
					ms?.Dispose();
					ms = new MemoryStream(efdata);
					addLog("Now will also write OBK config..." + Environment.NewLine);
					addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
					addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
					addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
					offset |= FLASH_MMAP_BASE;
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
			Command($"EW 0x40020004 3");
			Command($"EB 0x40020060 0x9F");
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

		internal byte[] readFlash(int addr = 0, int amount = 4096)
		{
			try
			{
				int startAmount = amount;
				byte[] ret = new byte[amount];
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
				while(startAmount > 0)
				{
					DumpBytes(startAddr | FLASH_MMAP_BASE, DumpAmount, out var bytes);
					startAddr += (uint)DumpAmount;
					startAmount -= DumpAmount;
					logger.setProgress(amount - startAmount, amount);
					bytes.CopyTo(ret, progress);
					progress += DumpAmount;
					addLog($"Reading at 0x{startAddr:X6}... ");
				}
				addLogLine(Environment.NewLine + "Getting hash...");
				var sha256Hash = SHA256.Create();
				var readHash = HashToStr(sha256Hash.ComputeHash(ret));
				var expectedHash = HashToStr(FlashReadHash((uint)addr, amount));
				if(readHash != expectedHash)
				{
					addErrorLine($"Hash mismatch!\r\nexpected\t{expectedHash}\r\ngot\t{readHash}");
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
			}
		}

		public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
		{

		}

		public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
		{
			byte[] data = null;

			if(rwMode != WriteMode.OnlyOBKConfig)
			{
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

		public static string HashToStr(byte[] data)
		{
			var sb = new StringBuilder();
			foreach(byte b in data)
				sb.Append(b.ToString("X2"));

			return sb.ToString();
		}

		private bool WriteCmd(byte[] cmd, byte ack = 0x06)
		{
			try
			{
				Flush();
				serial.Write(cmd, 0, cmd.Length);
				return WaitResp(ack); // ACK
			}
			catch
			{
				return false;
			}
		}

		private bool WaitResp(byte code)
		{
			int retries = 200;
			while(retries-- > 0)
			{
				try
				{
					int val = serial.ReadByte();
					if(val == -1 || val == 0x15) // nack
						return false;
					if((byte)val == code)
						return true;
				}
				catch
				{
					Thread.Sleep(1);
					// return false;
				}
			}
			return false;
		}
		private byte CalcChecksum(byte[] data, int start, int length)
		{
			int sum = 0;
			for(int i = start; i < length; i++)
			{
				sum += data[i];
			}
			return (byte)(sum & 0xff);
		}

		private bool SendXmodem(Stream stream, uint offset, int size, int retry)
		{
			logger.setProgress(0, size);
			logger.setState("Writing...", Color.Transparent);
			serial.ReadTimeout = 100;
			//this.chk32 = 0;
			int sequence = 1;
			int initialSize = size;
			int localOfs = 0;
			while(size > 0)
			{
				if((sequence % 4) == 1 || sequence == 1)
				{
					addLog(string.Format("Writing at 0x{0:X6}...", offset - FLASH_MMAP_BASE));
				}
				int packetSize;
				byte cmd;
				if(size <= 128)
				{
					packetSize = 128;
					cmd = 0x01; // SOH
				}
				else
				{
					packetSize = 1024;
					cmd = 0x02; // STX
				}
				localOfs += packetSize;
				int rdsize = (size < packetSize) ? size : packetSize;
				byte[] data = new byte[rdsize];
				int read = stream.Read(data, 0, rdsize);
				if(read <= 0)
				{
					addError("send: at EOF");
					return false;
				}

				// Pad data to packetSize with 0xFF
				byte[] paddedData = new byte[packetSize];
				for(int i = 0; i < packetSize; i++)
				{
					if(i < read)
						paddedData[i] = data[i];
					else
						paddedData[i] = 0xFF;
				}

				// Construct packet
				byte[] pkt = new byte[3 + packetSize + 1];
				pkt[0] = cmd;
				pkt[1] = (byte)sequence;
				pkt[2] = (byte)(0xFF - sequence);
				//pkt[3] = (byte)(offset & 0xFF);
				//pkt[4] = (byte)((offset >> 8) & 0xFF);
				//pkt[5] = (byte)((offset >> 16) & 0xFF);
				//pkt[6] = (byte)((offset >> 24) & 0xFF);
				for(int i = 0; i < packetSize; i++)
					pkt[3 + i] = paddedData[i];

				pkt[3 + packetSize] = CalcChecksum(pkt, 3, pkt.Length);

				// Retry logic
				int errorCount = 0;
				while(true)
				{
					if(WriteCmd(pkt))
					{
						Thread.Sleep(1);
						sequence = (sequence + 1) % 256;
						offset += (uint)packetSize;
						size -= rdsize;
						break;
					}
					else
					{
						errorCount++;
						if(errorCount > retry)
						{
							logger.setState("Write error!", Color.Transparent);
							return false;
						}
					}
				}
				logger.setProgress(localOfs, initialSize);
			}
			addLog("Write complete!" + Environment.NewLine);
			logger.setState("Write complete!", Color.Transparent);

			return WriteCmd(new byte[] { 0x04 }); // EOT
		}
	}
}

