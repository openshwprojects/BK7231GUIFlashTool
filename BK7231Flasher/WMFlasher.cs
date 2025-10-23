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
				serial.ReadTimeout = 2000;
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K)
				{
					SendInactivityTimeoutMillisec = 5000,
					MaxSenderRetries = 5
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
			var res = ExecuteCommand(0x3c, null, 1, 10, 0, true);
			if(res != null && res[0] == 'F' && res[1] == 'I' && res[2] == 'D')
			{
				if(chipType == BKType.W600)
				{
					flashID = new byte[]
					{
						Convert.ToByte($"{(char)res[4]}{(char)res[5]}", 16),
					};
					addLogLine($"Flash ID: 0x{flashID[0]:X}");
				}
				else
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
			}
			else if(chipType == BKType.W600)
			{
				addLogLine($"Getting flash id failed, assuming device is in secboot mode.");
				addLogLine($"Erasing secboot, will resync...");
				ExecuteCommand(0x3f, null, 1, 2);
				Sync();
				return ReadFlashId();
			}
			if(chipType != BKType.W600)
			{
				var romv = ExecuteCommand(0x3e, null, 1, 3);
				addLogLine($"ROM version: {(char)romv[2]}");
			}
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
						if(chipType == BKType.W600)
						{
							if(sync == 'P')
								continue;
							for(int i = 0; i < 250; i++)
							{
								serial.Write(new byte[] { 0x1B }, 0, 1);
								Thread.Sleep(1);
							}
						}
						else
							Thread.Sleep(250);
						addLogLine($"Sync attempt {attempts}/1000 failed...");
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
			if(chipType == BKType.W600) return true;
			var stub = Convert.FromBase64String(FLoaders.W800_Stub);
			addLogLine($"Sending stub...");
			if(xm.Send(stub) == stub.Length)
			{
				addLogLine($"Stub uploaded!");
				return Sync();
			}
			return false;
		}

		public override void doWrite(int startSector, byte[] data)
		{
			return;
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
			ExecuteCommand(0x31, msg, 1, 1, baud, noResync);
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
					if(respErrCount++ > 10)
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
					if(crcErrCount++ > 10)
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

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			if(chipType == BKType.W600)
			{
				addErrorLine("W600 doesn't support read. Use JLink for firmware backup.");
				return;
			}
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync() && ReadFlashId() != null && UploadStub())
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
			if(chipType == BKType.W600)
			{
				if(rwMode == WriteMode.ReadAndWrite)
				{
					addErrorLine("W600 doesn't support read. Use JLink for firmware backup.");
					return;
				}
				else if(rwMode == WriteMode.OnlyOBKConfig)
				{
					addErrorLine("Writing only OBK config is disabled for W600, use \"Automatically configure OBK on flash write\".");
					return;
				}
			}
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync() && ReadFlashId() != null && UploadStub())
			{
				try
				{
					xm.PacketSent += Xm_PacketSent;
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
						}
						else if(data.Length >= 0x100000)
						{
							try
							{
								startSector = 0x2000;
								var secBootHeader = new byte[64];
								Array.Copy(data, 0x2000, secBootHeader, 0, secBootHeader.Length);
								if(secBootHeader[0] != 0x9f || secBootHeader[1] != 0xff || secBootHeader[2] != 0xff || secBootHeader[3] != 0xa0)
								{
									addErrorLine("Unknown file type, no firmware header at 0x2000!");
									return;
								}
								var cutData = new byte[data.Length - startSector - 1];
								Array.Copy(data, startSector, cutData, 0, cutData.Length);
								startSector |= 0x08000000;
								var fls = GenerateW800PseudoFLSFromData(cutData, startSector);
								if(chipType == BKType.W600)
								{
									if(secBootHeader[60] != 0xFF || secBootHeader[61] != 0xFF || secBootHeader[62] != 0xFF || secBootHeader[63] != 0xFF)
									{
										addErrorLine("Not W600 backup!");
										return;
									}
									fls = GenerateW600PseudoFLSFromData(cutData, startSector);
								}
								var res = xm.Send(fls, (uint)(startSector ^ 0x08000000));
								if(res == fls.Length)
								{
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
								return;
							}
						}
						else
						{
							addErrorLine("Unknown file type, skipping.");
							return;
						}
					}
					if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null)
					{
						var offset = (OBKFlashLayout.getConfigLocation(chipType, out _) | 0x08000000);
						cfg.saveConfig(chipType);
						var data = new byte[2016 + 0x303];
						if(chipType == BKType.W800)
						{
							MiscUtils.padArray(data, 1);
							Array.Copy(cfg.getData(), 0, data, 0x303, 2016);
						}
						else
						{
							data = new byte[2016];
							Array.Copy(cfg.getData(), data, data.Length);
						}
						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
						var fls = chipType != BKType.W600 ? GenerateW800PseudoFLSFromData(data, offset - 0x303) : GenerateW600PseudoFLSFromData(data, offset);
						var res = xm.Send(fls, (uint)(offset ^ 0x08000000));
						if(res == fls.Length)
						{
							logger.setState("OBK config write success!", Color.Green);
							logger.setProgress(1, 1);
						}
						else
						{
							logger.setState("OBK config write error!", Color.Red);
						}
					}
					else
					{
						addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
					}
				}
				catch(Exception ex)
				{
					addErrorLine(ex.Message);
				}
				finally
				{
					xm.PacketSent -= Xm_PacketSent;
					SetBaud(115200, true);
				}
			}
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
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
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

		byte[] GenerateW600PseudoFLSFromData(byte[] data, int startAddr)
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
				(byte)(crc & 0xFF),
				(byte)((crc >> 8) & 0xFF),
				(byte)((crc >> 16) & 0xFF),
				(byte)((crc >> 24) & 0xFF),
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x31, 0x00, 0x00, 0x00,
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

