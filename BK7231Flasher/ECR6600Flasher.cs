using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
	public class ECR6600Flasher : BaseFlasher
	{
		MemoryStream ms;
		int flashSizeMB = 2;
		//byte[] flashID;

		bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				serial = new SerialPort(serialName, 115200);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				serial.ReadTimeout = 10000;
			}
			catch(Exception ex)
			{
				addLog("Port setup failed with " + ex.Message + "!" + Environment.NewLine);
				return false;
			}
			addLog("Port ready!" + Environment.NewLine);
			return true;
		}

		public bool Connect()
		{
			serial.DtrEnable = false;
			serial.RtsEnable = true;
			Thread.Sleep(50);
			serial.DtrEnable = true;
			serial.RtsEnable = false;
			Thread.Sleep(50);
			serial.DtrEnable = false;
			return false;
		}

		// recompiled stub command, won't work on normal stubs
		//public byte[] ReadFlashId()
		//{
		//	var fid = ExecuteCommand(0x9F, null, 0.1f, 4);
		//	if(fid == null)
		//		return null;
		//	addLogLine($"Flash ID: 0x{fid[0]:X2}{fid[1]:X2}{fid[2]:X2}");
		//	flashSizeMB = (1 << (fid[2] - 0x11)) / 8;
		//	addLogLine($"Flash size is {flashSizeMB}MB");
		//	return fid;
		//}

		public bool Sync()
		{
			//var fid = ReadFlashId();
			//if(fid != null)
			//{
			//	flashID = fid;
			//	addLogLine("Stub is already uploaded!");
			//	return true;
			//}
			if(ExecuteCommand(0x00, Encoding.ASCII.GetBytes("cnys"), isErrorExpected: true) != null)
			{
				addLogLine("Stub is already uploaded!");
				return true;
			}
			Connect();
			int tries = 50;
			serial.ReadTimeout = 50;
			serial.DiscardInBuffer();
			while(tries-- > 0)
			{
				serial.Write("cnys");
				var t = 0;
				try
				{
					t = serial.ReadByte();
				}
				catch { }
				if(t == 0x01 || t == 0xFF)
				{
					addLogLine("Sync success");
					return UploadStub();
				}
			}
			return false;
		}

		private bool UploadStub()
		{
			// write is broken with custom/V128 stub. RDTool continues to work ok with it though.
			var stub = Convert.FromBase64String(FLoaders.ECR6600_Stub_V1311);
			var startupAddress = 0x10000; // works even if 0
			var empty = new byte[8];
			var dat = new List<byte>()
			{ 
				0x01, 0x00, 0x00, 0x00,
				(byte)(startupAddress & 0xFF),
				(byte)((startupAddress >> 8) & 0xFF),
				(byte)((startupAddress >> 16) & 0xFF),
				(byte)((startupAddress >> 24) & 0xFF),
				(byte)(stub.Length & 0xFF),
				(byte)((stub.Length >> 8) & 0xFF),
				(byte)((stub.Length >> 16) & 0xFF),
				(byte)((stub.Length >> 24) & 0xFF),
			};
			serial.DiscardInBuffer();
			int tries = 10;
			while(tries-- > 0)
			{
				serial.Write(dat.ToArray(), 0, dat.Count);
				addLogLine("Uploading stub...");
				var t = -1;
				try
				{
					t = serial.ReadByte();
				}
				catch
				{
					serial.Write(empty, 0, empty.Length);
					addWarningLine("... failed, will retry!");
					continue;
				}
				if(t == 0)
				{
					serial.Write(stub, 0, stub.Length);
					addLogLine("Stub uploaded!");
					break;
				}
			}
			Thread.Sleep(10);
			serial.DiscardInBuffer();
			//flashID = ReadFlashId();
			//if(tries == 0 || flashID == null)
			//	return false;
			return true;
		}

		public override void doWrite(int startSector, byte[] data)
		{

		}

		byte StubCRC8(byte[] buf, int length)
		{
			uint crc = 0;

			for(int i = 0; i < length; i++)
			{
				crc += buf[i];
			}
			return (byte)(crc % 256);
		}

		byte[] ExecuteCommand(int type, byte[] parms = null,
			float timeout = 0.1f, int expectedReplyLen = 0, int br = 115200, bool isErrorExpected = false)
		{
			parms = parms ?? (new byte[0]);
			// MAGIC, TYPE, MSG LENGTH (2 bytes)
			List<byte> raw = new List<byte>() { 0xA5, (byte)type, (byte)(parms.Length & 0xFF), (byte)((parms.Length >> 8) & 0xFF) };
			// MSG
			raw.AddRange(parms);
			// CRC8
			raw.Add(StubCRC8(raw.ToArray(), raw.Count));
			serial.Write(raw.ToArray(), 0, raw.Count);
			Thread.Sleep(10);
			if(type == 0x07) serial.BaudRate = br;
			int timeoutMS = (int)(timeout * 1000);
			Stopwatch sw = Stopwatch.StartNew();
			var expect = 1 + 1 + 2 + expectedReplyLen + 1 + 1; // magic + type + data_len + data + status + crc8
			while(sw.ElapsedMilliseconds < timeoutMS)
			{
				if(serial.BytesToRead >= expect)
					break;
			}
			if(serial.BytesToRead == 0)
			{
				if(!isErrorExpected) addErrorLine("Command response is empty!");
				return null;
			}
			var bytes = new byte[serial.BytesToRead];
			serial.Read(bytes, 0, bytes.Length);
			if(bytes[0] != 0x5A)
			{
				if(!isErrorExpected) addErrorLine("Command header is incorrect!");
				return null;
			}
			byte crcret = StubCRC8(bytes, bytes.Length - 1);
			//for(int k = 0; k < bytes.Length - 1; k++)
			//{
			//	crcret += bytes[k];
			//}
			if(crcret != bytes[bytes.Length - 1])
			{
				addErrorLine("Command CRC is incorrect!");
				logger.setState("CRC mismatch!", Color.Red);
				return null;
			}
			var status = string.Empty;
			switch(bytes[bytes.Length - 2])
			{
				case 0x00: break;
				case 0x01:
					status = "ERROR";
					break;
				case 0x02:
					status = "ADDR_ERROR";
					break;
				case 0x03:
					status = "TYPE_ERROR";
					break;
				case 0x04:
					status = "LEN_ERROR";
					break;
				case 0x05:
					status = "CRC_ERROR";
					break;
				default:
					status = $"Unknown error {bytes[bytes.Length - 2]}";
					break;
			}
			if(status != string.Empty)
			{
				if(!isErrorExpected) addErrorLine($"Command status is {status}");
				return null;
			}
			if(bytes.Length != expect)
			{
				addErrorLine($"Command reply length {bytes.Length} != expected {expect}");
				return null;
			}
			var ret = new byte[expectedReplyLen];
			Array.Copy(bytes, 4, ret, 0, expectedReplyLen);
			return ret;
		}

		private bool SetBaud(int baud)
		{
			if(serial.BaudRate != baud)
			{
				var msg = new byte[4];
				msg[0] = (byte)(baud & 0xFF);
				msg[1] = (byte)((baud >> 8) & 0xFF);
				msg[2] = (byte)((baud >> 16) & 0xFF);
				msg[3] = (byte)((baud >> 24) & 0xFF);
				return ExecuteCommand(0x07, msg, 2, 0, baud) != null;
			}
			return true;
		}

		private int DetectFlashSize()
		{
			var addr = 0x1000000;
			while(addr > 0x100000)
			{
				var length = 8;
				var msg = new byte[8];
				msg[0] = (byte)(addr & 0xFF);
				msg[1] = (byte)((addr >> 8) & 0xFF);
				msg[2] = (byte)((addr >> 16) & 0xFF);
				msg[3] = (byte)((addr >> 24) & 0xFF);
				msg[4] = (byte)(length & 0xFF);
				msg[5] = (byte)((length >> 8) & 0xFF);
				msg[6] = (byte)((length >> 16) & 0xFF);
				msg[7] = (byte)((length >> 24) & 0xFF);
				var res = ExecuteCommand(0x03, msg, 0.1f, length, isErrorExpected: true);
				if(res != null)
					break;
				addr /= 2;
			}
			addr *= 2;
			return addr;
		}

		private byte[] InternalRead(int addr, int sectors)
		{
			try
			{
				if(!SetBaud(baudrate))
					return null;
				var retaddr = 0;
				int startAmount = sectors * BK7231Flasher.SECTOR_SIZE;
				byte[] ret = new byte[startAmount];
				logger.setProgress(0, sectors);
				logger.setState("Reading", Color.White);
				for(int i = 0; i < sectors; i++)
				{
					var length = BK7231Flasher.SECTOR_SIZE;
					var msg = new byte[8];
					msg[0] = (byte)(addr & 0xFF);
					msg[1] = (byte)((addr >> 8) & 0xFF);
					msg[2] = (byte)((addr >> 16) & 0xFF);
					msg[3] = (byte)((addr >> 24) & 0xFF);
					msg[4] = (byte)(length & 0xFF);
					msg[5] = (byte)((length >> 8) & 0xFF);
					msg[6] = (byte)((length >> 16) & 0xFF);
					msg[7] = (byte)((length >> 24) & 0xFF);
					byte[] res = null;
					int retries = 5;
					while(retries-- > 0)
					{
						addLog($"0x{addr:X}... ");
						res = ExecuteCommand(0x03, msg, 2, length);
						if(res != null)
						{
							logger.setState("Reading", Color.White);
							break;
						}
						addWarning($"0x{addr:X} failed, retrying...");
					}
					if(res == null)
					{
						addLogLine("");
						addErrorLine("Failed after maximum number of attempts!");
						return null;
					}
					Array.Copy(res, 0, ret, retaddr, length);
					addr += length;
					retaddr += length;
					logger.setProgress(i, sectors);
				}
				logger.setProgress(sectors, sectors);
				logger.setState("Read done", Color.DarkGreen);
				addLogLine("");
				addLogLine("Read complete!");
				return ret;
			}
			finally
			{
				SetBaud(115200);
			}
		}

		private bool InternalWrite(int addr, byte[] data, int len = -1)
		{
			try
			{
				if(!SetBaud(baudrate))
					return false;
				if(len < 0)
					len = data.Length;
				int ofs = 0;
				var bufLen = 0x1000;
				logger.setProgress(0, len);
				//doErase(addr, (len + 4095) / 4096);
				addLogLine("Starting flash write " + len);
				logger.setState("Writing", Color.White);
				var cmd = new byte[8];
				cmd[0] = (byte)(addr & 0xFF);
				cmd[1] = (byte)((addr >> 8) & 0xFF);
				cmd[2] = (byte)((addr >> 16) & 0xFF);
				cmd[3] = (byte)((addr >> 24) & 0xFF);
				cmd[4] = (byte)(len & 0xFF);
				cmd[5] = (byte)((len >> 8) & 0xFF);
				cmd[6] = (byte)((len >> 16) & 0xFF);
				cmd[7] = (byte)((len >> 24) & 0xFF);
				var res = ExecuteCommand(0x02, cmd, 5, 0);
				if(res == null)
					return false;
				while(ofs < len)
				{
					int chunk = len - ofs;
					if(chunk > bufLen)
						chunk = bufLen;
					var buffer = new byte[chunk + 1];
					Array.Copy(data, ofs, buffer, 1, chunk);
					buffer[0] = (byte)(chunk >> 8);
					res = ExecuteCommand(0x02, buffer, 5, 0);
					if(res == null)
						return false;
					ofs += chunk;
					addr += chunk;
					addLog($"0x{addr:X}...");
					logger.setProgress(ofs, len);
				}
				addLogLine("");
				logger.setState("Writing done", Color.DarkGreen);
				addLogLine("Done flash write " + len);
				return true;
			}
			finally
			{
				SetBaud(115200);
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
					sectors = DetectFlashSize() / BK7231Flasher.SECTOR_SIZE;
					addLogLine($"Detected flash size: {sectors / 256}MB");
				}
				byte[] res = InternalRead(startSector, sectors);
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
			if(bAll)
			{
				if(doGenericSetup() == false)
				{
					return false;
				}
				addLogLine("Doing chip erase...");
				return ExecuteCommand(0x05, null, 10) != null;
			}
			else
			{
				var length = sectors * BK7231Flasher.SECTOR_SIZE;
				var msg = new byte[8];
				msg[0] = (byte)(startSector & 0xFF);
				msg[1] = (byte)((startSector >> 8) & 0xFF);
				msg[2] = (byte)((startSector >> 16) & 0xFF);
				msg[3] = (byte)((startSector >> 24) & 0xFF);
				msg[4] = (byte)(length & 0xFF);
				msg[5] = (byte)((length >> 8) & 0xFF);
				msg[6] = (byte)((length >> 16) & 0xFF);
				msg[7] = (byte)((length >> 24) & 0xFF);
				var t = ExecuteCommand(0x04, msg, 10);
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
		
		public override void doTestReadWrite(int startSector = 0x000, int sectors = 10)
		{
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
					sectors = DetectFlashSize() / BK7231Flasher.SECTOR_SIZE;
					addLogLine($"Flash size detected: {sectors / 256}MB");
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
					var oneb = new byte[4];
					Array.Copy(data, 0, oneb, 0, 4);
					if(oneb[0] == 'o' && oneb[1] == 'n' && oneb[2] == 'e' && oneb[3] == 'b')
					{
						// this is obk (or allinone or stub_cpu_inone), not backup
						uint offset = 5;
						while(true)
						{
							var header = new byte[9];
							Array.Copy(data, offset, header, 0, header.Length);
							uint flashOffset = BitConverter.ToUInt32(header, 1);
							uint length = BitConverter.ToUInt32(header, 5);
							if(header[0] == 0x00)
							{
								addLogLine("Skipping stub...");
							}
							else if(header[0] != 0x02)
							{
								addWarningLine($"Unknown type {header[0]:X2} with offset {flashOffset:X} and length {length}, skipping");
							}
							else
							{
								var split = new byte[length];
								Array.Copy(data, offset + header.Length, split, 0, length);
								if(!InternalWrite((int)flashOffset, split))
								{
									logger.setState("Write error!", Color.Red);
									return;
								}
							}

							offset += (uint)(length + header.Length);
							if(offset == data.Length)
								break;
						}
					}
					else
					{
						InternalWrite(startSector, data);
					}
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
				{
					if(cfg != null)
					{
						var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
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

