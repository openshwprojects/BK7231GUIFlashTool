using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class WMFlasher : BaseFlasher
	{
		// it uses CRC16 CCITT 0xFFFF
		// WM command - 0x21 {length high} {length low} {crc16 high} {crc16 low} {command 4 bytes} {payload}
		MemoryStream ms;
		int flashSizeMB = 2;
		byte[] flashID;
		XMODEM xm;

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
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K)
				{
					SendInactivityTimeoutMillisec = 5000,
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

		public byte[] ReadFlashId()
		{
			var res = ExecuteCommand(0x3c, null, 1, 10);
			if(res != null && res[0] == 'F' && res[1] == 'I' && res[2] == 'D')
			{
				flashID = new byte[]
				{ 
					Convert.ToByte($"{(char)res[4]}{(char)res[5]}", 16),
					Convert.ToByte($"{(char)res[7]}{(char)res[8]}", 16),
				};
				addLogLine($"Flash ID: 0x{flashID[0]:X}xx{flashID[1]:X}");
				flashSizeMB = (1 << (flashID[1] - 0x11)) / 8;
				addLogLine($"Flash size is {flashSizeMB}MB");
			}
			var romv = ExecuteCommand(0x3e, null, 1, 3);
			addLogLine($"ROM version: {(char)romv[2]}");
			return flashID;
		}

		public bool Sync()
		{
			serial.DiscardInBuffer();
			var count = 0;
			try
			{
				int attempts = 0;
				while(attempts++ < 1000)
				{
					byte sync = 0;
					try
					{
						sync = (byte)serial.ReadByte();
					}
					catch { }
					if(sync == 'C')
						count++;
					else
					{
						addLogLine($"Sync attempt {attempts}/1000 failed...");
						Thread.Sleep(250);
						serial.DiscardInBuffer();
						count = 0;
					}
					if(count > 3)
					{
						addLogLine($"Sync success!");
						return true;
					}
				}
			}
			catch { }
			return false;
		}

		private bool UploadStub()
		{
			var stub = Convert.FromBase64String(FLoaders.W800_Stub);
			addLogLine($"Sending stub...");
			if(xm.Send(stub) == stub.Length)
			{
				addLogLine($"Stub uploaded!");
				return true;
			}
			return false;
		}

		public override void doWrite(int startSector, byte[] data)
		{
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync())
			{
				InternalWrite(startSector, data);
			}
		}

		byte[] ExecuteCommand(int type, byte[] parms = null,
			float timeout = 0.1f, int expectedReplyLen = 0, int br = 115200, bool isErrorExpected = false)
		{
			parms = parms ?? (new byte[0]);
			var cmd = new List<byte>()
			{
				(byte)(type & 0xFF),
				(byte)((type >> 8) & 0xFF),
				(byte)((type >> 16) & 0xFF),
				(byte)((type >> 24) & 0xFF),
			};
			cmd.AddRange(parms);
			var raw = new List<byte>()
			{ 
				0x21, 
				(byte)(cmd.Count + 2 & 0xFF), 
				(byte)((cmd.Count + 2 >> 8) & 0xFF)
			};
			var crc = CRC.crc_ccitt(cmd.ToArray(), 0, cmd.Count, 0xFFFF);
			raw.Add((byte)(crc & 0xFF));
			raw.Add((byte)((crc >> 8) & 0xFF));
			raw.AddRange(cmd);

			serial.DiscardInBuffer();
			serial.Write(raw.ToArray(), 0, raw.Count);
			if(type == 0x31)
			{
				Thread.Sleep(10);
				serial.BaudRate = br;
			}
			int timeoutMS = (int)(timeout * 1000);
			Stopwatch sw = Stopwatch.StartNew();
			while(sw.ElapsedMilliseconds < timeoutMS)
			{
				if(serial.BytesToRead >= expectedReplyLen)
					break;
			}
			if(serial.BytesToRead == 0)
			{
				if(!isErrorExpected)
					addErrorLine("Command response is empty!");
				return null;
			}
			var bytes = new byte[serial.BytesToRead];
			serial.Read(bytes, 0, bytes.Length);
			if(bytes.Length < expectedReplyLen)
			{
				if(!isErrorExpected)
					addErrorLine($"Command reply length {bytes.Length} < expected {expectedReplyLen}");
				return null;
			}
			var ret = new byte[expectedReplyLen];
			Array.Copy(bytes, 0, ret, 0, expectedReplyLen);
			return ret;
		}

		private bool SetBaud(int baud, bool noResync = false)
		{
			addLogLine($"Changing baud to {baud}{(!noResync ? ", will resync..." : string.Empty)}");
			var msg = new byte[4];
			msg[0] = (byte)(baud & 0xFF);
			msg[1] = (byte)((baud >> 8) & 0xFF);
			msg[2] = (byte)((baud >> 16) & 0xFF);
			msg[3] = (byte)((baud >> 24) & 0xFF);
			ExecuteCommand(0x31, msg, 1, 1, baud);
			return noResync || Sync();
		}

		public bool ReadFlash(MemoryStream stream, int offset, int size)
		{
			var readLength = 4096;
			int count = (size + readLength - 1) / readLength;
			int crcErrCount = 0;
			int respErrCount = 0;

			logger.setProgress(0, count);
			logger.setState("Reading...", Color.Transparent);
			for(int i = 0; i < count; i++)
			{
				addLog(string.Format($"Read block at 0x{offset ^ 0x08000000:X6}..."));
				var header = new byte[8];
				header[0] = (byte)(offset & 0xFF);
				header[1] = (byte)((offset >> 8) & 0xFF);
				header[2] = (byte)((offset >> 16) & 0xFF);
				header[3] = (byte)((offset >> 24) & 0xFF);
				header[4] = (byte)(readLength & 0xFF);
				header[5] = (byte)((readLength >> 8) & 0xFF);
				header[6] = (byte)((readLength >> 16) & 0xFF);
				header[7] = (byte)((readLength >> 24) & 0xFF);
				var response = ExecuteCommand(0x4a, header, 2, readLength + 4, isErrorExpected: true);
				if(response == null)
				{
					addWarningLine("Failed to get response! Retrying...");
					respErrCount++;
					if(crcErrCount > 10)
					{
						addErrorLine("Response error count exceeded limit, stopping!");
						return false;
					}
					i--;
					continue;
				}
				else
				{
					respErrCount = 0;
				}

				var crc32 = CRC.crc32_ver2(0xFFFFFFFF, response, response.Length - 4);
				var recvcrc32 = BitConverter.ToUInt32(response, response.Length - 4);
				if(crc32 != recvcrc32)
				{
					addWarningLine("CRC Error! Retrying...");
					crcErrCount++;
					if(crcErrCount > 10)
					{
						addErrorLine("CRC error count exceeded limit, stopping!");
						return false;
					}
					i--;
					continue;
				}
				else
				{
					crcErrCount = 0;
				}
				stream.Write(response, 0, response.Length - 4);
				offset += readLength;
				logger.setProgress(i, count);
			}

			logger.setProgress(count, count);
			addLog("All blocks read!" + Environment.NewLine);
			addLog("Read done for " + stream.Length + " bytes!" + Environment.NewLine);
			return true;
		}

		MemoryStream ReadInternal(int startSector, int sectors)
		{
			MemoryStream tempResult = new MemoryStream();
			if(!ReadFlash(tempResult, startSector, sectors * BK7231Flasher.SECTOR_SIZE))
			{
				logger.setState("Reading error!", Color.Red);
				SetBaud(115200);
				return null;
			}
			return tempResult;
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
				byte[] res = null;//ExecuteCommand(0x02, cmd, 5, 0);
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
					//res = ExecuteCommand(0x02, buffer, 5, 0);
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
			if(Sync() && ReadFlashId() != null && UploadStub() && Sync())
			{
				try
				{
					SetBaud(baudrate);
					if(fullRead)
					{
						sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
					}
					ms = ReadInternal(startSector | 0x08000000, sectors);
					if(ms == null)
					{
						return;
					}
				}
				catch(Exception ex)
				{
					addErrorLine(ex.Message);
				}
				finally
				{
					SetBaud(115200);
				}
			}
			return;
		}
		
		public override byte[] getReadResult()
		{
			return ms?.ToArray();
		}

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
			//if(bAll)
			//{
			//	if(doGenericSetup() == false)
			//	{
			//		return false;
			//	}
			//	if(Sync())
			//	{
			//		//addLogLine("Doing chip erase...");
			//		//var msg = new byte[4];
			//		//msg[0] = (byte)(2 & 0xFF);
			//		//msg[1] = (byte)((2 >> 8) & 0xFF);
			//		//msg[2] = (byte)(0x1FE00 & 0xFF);
			//		//msg[3] = (byte)((0x1FE00 >> 8) & 0xFF);
			//		//return ExecuteCommand(0x32, msg, 10) != null;
			//	}
			//}
			//else
			//{
			//	//var msg = new byte[4];
			//	//var length = sectors * BK7231Flasher.SECTOR_SIZE / 10;
			//	//msg[0] = (byte)(startSector & 0xFF);
			//	//msg[1] = (byte)((startSector >> 8) & 0xFF);
			//	//msg[2] = (byte)(length & 0xFF);
			//	//msg[3] = (byte)((length >> 8) & 0xFF);
			//	//return ExecuteCommand(0x32, msg, 10) != null;
			//}
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
			if(Sync() && ReadFlashId() != null && UploadStub() && Sync())
			{
				try
				{
					SetBaud(baudrate);
					OBKConfig cfg = rwMode == WriteMode.OnlyOBKConfig ? logger.getConfig() : logger.getConfigToWrite();
					if(rwMode == WriteMode.ReadAndWrite)
					{
						sectors = flashSizeMB * 0x100000 / BK7231Flasher.SECTOR_SIZE;
						addLogLine($"Flash size detected: {sectors / 256}MB");
						ms = ReadInternal(startSector | 0x08000000, sectors);
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
						addLogLine("Starting flash write " + data.Length);
						logger.setState("Writing", Color.White);
						if(sourceFileName.EndsWith(".fls"))
						{
							xm.PacketSent += Xm_PacketSent;
							var res = xm.Send(data);
							if(res == data.Length)
							{
								logger.setState("Writing done", Color.DarkGreen);
								addLogLine("Done flash write " + data.Length);
							}
							else
							{
								logger.setState("Write error!", Color.Red);
							}
							xm.PacketSent -= Xm_PacketSent;
						}
						else if(sourceFileName.Contains("readResult") && data.Length >= 0x100000)
						{
							try
							{
								xm.PacketSent += Xm_PacketSent;
								startSector = 0x2400;
								var secBootHeader = new byte[64];
								Array.Copy(data, 0x2000, secBootHeader, 0, secBootHeader.Length);
								var secBootLength = BitConverter.ToUInt32(secBootHeader, 12);
								var secBoot = new byte[secBootLength];
								var secBootAddr = BitConverter.ToUInt32(secBootHeader, 8);
								var secBootFls = new List<byte>();
								secBootFls.AddRange(secBootHeader);
								Array.Copy(data, secBootAddr ^ 0x08000000, secBoot, 0, secBootLength);
								secBootFls.AddRange(secBoot);
								var cutData = new byte[data.Length - startSector - 1];
								Array.Copy(data, startSector, cutData, 0, cutData.Length);
								startSector |= 0x08000000;
								var fls = GenerateW800PseudoFLSFromData(cutData, startSector);
								var res = xm.Send(fls);
								if(res == fls.Length)
								{
									addLogLine("Restoring secboot");
									var sfb = xm.Send(secBootFls.ToArray());
									if(sfb != secBootFls.Count)
									{
										logger.setState("Write secboot error!", Color.Red);
										return;
									}
									logger.setState("Writing done", Color.DarkGreen);
									addLogLine("Done flash write " + data.Length);
									logger.setProgress(1, 1);
								}
								else
								{
									logger.setState("Write error!", Color.Red);
								}
							}
							catch(Exception ex)
							{
								addErrorLine(ex.Message);
							}
							finally
							{
								xm.PacketSent -= Xm_PacketSent;
							}
						}
						else
						{
							addErrorLine("Unknown file type, skipping.");
						}
					}
					if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
					{
						addWarningLine("Writing config is not supported!");
					}
				}
				catch(Exception ex)
				{
					addErrorLine(ex.Message);
				}
				finally
				{
					SetBaud(115200, true);
				}
			}
		}

		private void Xm_PacketSent(int sentBytes, int total)
		{
			logger.setProgress(sentBytes, total);
		}

		byte[] GenerateW800PseudoFLSFromData(byte[] data, int startAddr)
		{
			var crc = CRC.crc32_ver2(0xFFFFFFFF, data);
			var fls = new List<byte>()
			{
				0x9F, 0xFF, 0xFF, 0xA0,
				0x00, 0x02, 0x00, 0x00,
				(byte)(startAddr & 0xFF),
				(byte)((startAddr >> 8) & 0xFF),
				(byte)((startAddr >> 16) & 0xFF),
				(byte)((startAddr >> 24) & 0xFF),
				(byte)(data.Length & 0xFF),
				(byte)((data.Length >> 8) & 0xFF),
				(byte)((data.Length >> 16) & 0xFF),
				(byte)((data.Length >> 24) & 0xFF),
				//0x00, 0x00, 0x00, 0x00,
				(byte)(startAddr - 0x400 & 0xFF),
				(byte)(((startAddr - 0x400) >> 8) & 0xFF),
				(byte)(((startAddr - 0x400) >> 16) & 0xFF),
				(byte)(((startAddr - 0x400) >> 24) & 0xFF),
				0x00, 0x00, 0x01, 0x08,
				(byte)(crc & 0xFF),
				(byte)((crc >> 8) & 0xFF),
				(byte)((crc >> 16) & 0xFF),
				(byte)((crc >> 24) & 0xFF),
				0x00, 0x00, 0x00, 0x00,
				0x31, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
			};
			var crcHdr = CRC.crc32_ver2(0xFFFFFFFF, fls.ToArray());
			var t = Convert.ToString(crcHdr, 16);
			fls.Add((byte)(crcHdr & 0xFF));
			fls.Add((byte)((crcHdr >> 8) & 0xFF));
			fls.Add((byte)((crcHdr >> 16) & 0xFF));
			fls.Add((byte)((crcHdr >> 24) & 0xFF));
			fls.AddRange(data);
			return fls.ToArray();
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

