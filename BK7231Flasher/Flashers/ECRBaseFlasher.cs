using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace BK7231Flasher
{
	public abstract class ECRBaseFlasher : BaseFlasher
	{
		protected static readonly byte CMD_SYN = 0x00;
		protected static readonly byte CMD_FLASH_ERASE = 0x04;
		protected static readonly byte CMD_FLASH_CHIPERASE = 0x05;
		protected static readonly byte CMD_BAUD = 0x07;
		protected static readonly byte CMD_SHA256 = 0x09;
		protected static readonly byte CMD_CUSTOM_FLASH_ID = 0x90;
		protected static readonly byte CMD_CUSTOM_XMODEM_WRITE = 0x91;
		protected static readonly byte CMD_CUSTOM_XMODEM_READ = 0x92;
		protected static readonly byte CMD_CUSTOM_KV_GET = 0x93;
		protected static readonly byte CMD_CUSTOM_KV_SET = 0x94;
		protected static readonly byte CMD_CUSTOM_GET_MAC = 0x95;
		protected static readonly byte CMD_CUSTOM_XMODEM_READ_ZLIB = 0x96;

		protected int flashSizeMB = 4;

		protected MemoryStream ms;

		public ECRBaseFlasher(CancellationToken ct) : base(ct)
		{
		}

		protected abstract bool doGenericSetup();

		protected abstract bool Sync();

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
			if(bAll)
			{
				if(doGenericSetup() == false)
				{
					return false;
				}
				if(Sync())
				{
					addLogLine("Doing chip erase...");
					return ExecuteCommand(CMD_FLASH_CHIPERASE, null, 10) != null;
				}
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
				return ExecuteCommand(CMD_FLASH_ERASE, msg, 10) != null;
			}
			return false;
		}

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			if(sectors == 0 && !fullRead)
			{
				addErrorLine($"Read length cannot be zero!");
				return;
			}
			if(doGenericSetup() == false)
			{
				return;
			}
			if(Sync())
			{
				if(fullRead)
				{
					sectors = flashSizeMB * 256;
				}
				var useCompression = false;
				if(bUseCompressionIfPossible)
				{
					useCompression = true;
				}
				byte[] res = InternalRead(startSector, sectors, useCompression);
				if(res != null)
					ms = new MemoryStream(res);
			}
			return;
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
		
		public override byte[] getReadResult()
		{
			return ms?.ToArray();
		}
		public override bool saveReadResult(int startOffset)
		{
			string fileName = MiscUtils.formatDateNowFileName("readResult_" + chipType, backupName, "bin");
			return saveReadResult(fileName);
		}

		protected byte[] ExecuteCommand(int type, byte[] parms = null,
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
			if(type == CMD_BAUD) serial.BaudRate = br;
			int timeoutMS = (int)(timeout * 1000);
			Stopwatch sw = Stopwatch.StartNew();
			var expect = 1 + 1 + 2 + expectedReplyLen + 1 + 1; // magic + type + data_len + data + status + crc8
			while(sw.ElapsedMilliseconds < timeoutMS)
			{
				if(isCancelled) return null;
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

		protected byte[] InternalRead(int addr, int sectors, bool isCompressed = false)
		{
			if(sectors == 0)
			{
				addErrorLine($"Read length cannot be zero!");
				return null;
			}
			var sha256Hash = SHA256.Create();
			var offset = addr;
			var toRead = sectors * 0x1000;
			int startAmount = sectors * BK7231Flasher.SECTOR_SIZE;
			void Xm_PacketReceived(XMODEM sender, byte[] packet, bool endOfFileDetected)
			{
				if(((startAmount - toRead) % 0x1000) == 0)
				{
					addLog($"0x{offset:X}... ");
				}
				offset += packet.Length;
				toRead -= packet.Length;
				if(!isCancelled && !isCompressed)
					logger.setProgress(startAmount - toRead, startAmount);
			}
			try
			{
				if(!SetBaud(baudrate))
					return null;
				byte[] ret = new byte[startAmount];
				logger.setProgress(0, sectors);
				logger.setState("Reading", Color.White);
				var msg = new byte[8];
				msg[0] = (byte)(addr & 0xFF);
				msg[1] = (byte)((addr >> 8) & 0xFF);
				msg[2] = (byte)((addr >> 16) & 0xFF);
				msg[3] = (byte)((addr >> 24) & 0xFF);
				msg[4] = (byte)(startAmount & 0xFF);
				msg[5] = (byte)((startAmount >> 8) & 0xFF);
				msg[6] = (byte)((startAmount >> 16) & 0xFF);
				msg[7] = (byte)((startAmount >> 24) & 0xFF);
				var res = ExecuteCommand(isCompressed == false ? CMD_CUSTOM_XMODEM_READ : CMD_CUSTOM_XMODEM_READ_ZLIB, msg, 2, 0);
				var stream = new MemoryStream();
				xm.PacketReceived += Xm_PacketReceived;
				var sw = new Stopwatch();

				try
				{
					sw.Start();
					var tries = 3;
					while(tries-- >= 0)
					{
						var recv = xm.Receive(stream);
						//if(recv == XMODEM.TerminationReasonEnum.CancelNotificationReceived) continue;
						//else 
						if(recv != XMODEM.TerminationReasonEnum.EndOfFile)
						{
							addErrorLine($"Read failed with {recv}");
							return null;
						}
						else
						{
							break;
						}

						if(isCancelled)
							return null;
					}
					ret = stream.ToArray();
				}
				finally
				{
					sw.Stop();
					xm.PacketReceived -= Xm_PacketReceived;
					stream.Dispose();
				}
				logger.addLog(Environment.NewLine + $"Flash read took {sw.ElapsedMilliseconds} ms" + Environment.NewLine, Color.Gray);
				if(isCancelled) return null;
				if(isCompressed) ret = Ionic.Zlib.ZlibStream.UncompressBuffer(ret);
				addLogLine("Getting hash...");
				res = ExecuteCommand(CMD_SHA256, msg, 30, 32);
				if(res == null)
				{
					addErrorLine($"Failed to get hash!");
					logger.setState("SHA failed!", Color.Red);
					if(!bIgnoreCRCErr) return null;
				}
				else
				{
					var readHash = HashToStr(sha256Hash.ComputeHash(ret));
					var expectedHash = HashToStr(res);
					if(readHash != expectedHash)
					{
						addErrorLine($"Hash mismatch!\r\ndevice:\t{expectedHash}\r\nflasher:\t{readHash}");
						logger.setState("SHA mismatch!", Color.Red);
						if(!bIgnoreCRCErr) return null;
					}
					else
					{
						addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
					}
				}
				logger.setProgress(sectors, sectors);
				logger.setState("Read done", Color.DarkGreen);
				addLogLine("Read complete!");
				return ret;
			}
			finally
			{
				SetBaud(115200);
				sha256Hash.Clear();
			}
		}

		protected bool InternalWrite(int addr, byte[] data, int len = -1)
		{
			var sha256Hash = SHA256.Create();
			try
			{
				xm.PacketSent += Xm_PacketSent;
				if(!SetBaud(baudrate))
					return false;
				if(len < 0)
					len = data.Length;
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
				var res = ExecuteCommand(CMD_CUSTOM_XMODEM_WRITE, cmd, 0.1f, 0);
				if(res == null)
				{
					serial.Write(new[] { xm.EOT }, 0, 1);
					Thread.Sleep(100);
					return false;
				}
				var ret = xm.Send(data, (uint)addr);
				if(ret != len)
				{
					addErrorLine($"Write failed ({xm.TerminationReason})! Expected sent bytes: {len}, really sent: {ret}");
					Thread.Sleep(100);
					serial.Write(new[] { xm.EOT }, 0, 1);
					Thread.Sleep(100);
					return false;
				}
				addLogLine(Environment.NewLine + "Getting hash...");
				res = ExecuteCommand(CMD_SHA256, cmd, 30f, 32);
				var readHash = HashToStr(sha256Hash.ComputeHash(data));
				var expectedHash = HashToStr(res);
				if(readHash != expectedHash)
				{
					addErrorLine($"Hash mismatch!\r\ndevice:\t{expectedHash}\r\nflasher:\t{readHash}");
					logger.setState("SHA mismatch!", Color.Red);
					return false;
				}
				else
				{
					addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
				}
				logger.setState("Writing done", Color.DarkGreen);
				addLogLine("Done flash write " + len);
				return true;
			}
			finally
			{
				xm.PacketSent -= Xm_PacketSent;
				sha256Hash.Clear();
				SetBaud(115200);
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

		protected bool SetBaud(int baud)
		{
			if(serial.BaudRate != baud)
			{
				var msg = new byte[4];
				msg[0] = (byte)(baud & 0xFF);
				msg[1] = (byte)((baud >> 8) & 0xFF);
				msg[2] = (byte)((baud >> 16) & 0xFF);
				msg[3] = (byte)((baud >> 24) & 0xFF);
				return ExecuteCommand(CMD_BAUD, msg, 2, 0, baud) != null;
			}
			return true;
		}

		protected byte StubCRC8(byte[] buf, int length)
		{
			byte crc = 0;

			unchecked
			{
				for(int i = 0; i < length; i++)
				{
					crc += buf[i];
				}
			}
			return (byte)(crc % 256);
		}

		protected byte[] ReadFlashId(bool isErrorExpected = false)
		{
			var flashID = ExecuteCommand(CMD_CUSTOM_FLASH_ID, null, 0.2f, 4, isErrorExpected: isErrorExpected);
			if(flashID == null)
				return null;
			addLogLine($"Flash ID: 0x{flashID[0]:X2}{flashID[1]:X2}{flashID[2]:X2}");
			if(flashID[2] < 0x11 || flashID[2] > 0x22)
				throw new Exception("Flash ID incorrect!");
			flashSizeMB = (1 << (flashID[2] - 0x11)) / 8;
			addLogLine($"Flash size is {flashSizeMB}MB");
			return flashID;
		}
	}
}
