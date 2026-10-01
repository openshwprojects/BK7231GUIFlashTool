using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BK7231Flasher
{
	public abstract class ECRBaseFlasher : BaseFlasher
	{
		internal static readonly byte CMD_SYN = 0x00;
		internal static readonly byte CMD_FLASH_ERASE = 0x04;
		internal static readonly byte CMD_FLASH_CHIPERASE = 0x05;
		internal static readonly byte CMD_BAUD = 0x07;
		internal static readonly byte CMD_SHA256 = 0x09;
		internal static readonly byte CMD_CUSTOM_CHIP_INFO = 0x20;
		internal static readonly byte CMD_CUSTOM_CRC32 = 0x8F;
		internal static readonly byte CMD_CUSTOM_FLASH_ID = 0x90;
		internal static readonly byte CMD_CUSTOM_XMODEM_WRITE = 0x91;
		internal static readonly byte CMD_CUSTOM_XMODEM_READ = 0x92;
		internal static readonly byte CMD_CUSTOM_KV_GET = 0x93;
		internal static readonly byte CMD_CUSTOM_KV_SET = 0x94;
		internal static readonly byte CMD_CUSTOM_GET_MAC = 0x95;
		internal static readonly byte CMD_CUSTOM_XMODEM_READ_COMPRESSED = 0x96;
		internal static readonly byte CMD_CUSTOM_XMODEM_WRITE_COMPRESSED = 0x97;
		internal static readonly byte CMD_CUSTOM_XMODEM_READ_RAW = 0x98;
		internal static readonly byte CMD_CUSTOM_READ_EFUSE = 0x99;
		internal static readonly byte CMD_CUSTOM_READ_OTP = 0x9A;

		internal static readonly Dictionary<BKType, uint> PlatformIDs = new Dictionary<BKType, uint>()
		{
			{ BKType.ECR6600, 0x4C7959C9 },
			{ BKType.GD32VW553, 0xFFDC26B5 },
			{ BKType.OPL1000A2, 0xAA5D6AC8 },
			{ BKType.LN8825, 0x8ABF79A8 },
			{ BKType.LN882H, 0xA40B7429 },
			{ BKType.RDA5981, 0x7272742E },
			{ BKType.RTL8710B, 0x43B186D6 },
			{ BKType.RTL87X0C, 0x34B6B640 },
			{ BKType.RTL8720D, 0xA8949DBA },
			{ BKType.RTL8721DA, 0xF9073AB3 },
			{ BKType.RTL8720E, 0xDF93AD2C },
			{ BKType.TR6260, 0x396440B3 },
			{ BKType.W800, 0xDC7E93D2 },
		};

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
				byte[] res = InternalRead(startSector, sectors);
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

		private bool ReadStubByte(Stopwatch sw, int timeoutMs, out byte value)
		{
			while(sw.ElapsedMilliseconds < timeoutMs && !isCancelled)
			{
				if(serial.BytesToRead > 0)
				{
					value = (byte)serial.ReadByte();
					return true;
				}
				Thread.Sleep(1);
			}
			value = 0;
			return false;
		}

		protected virtual byte[] ExecuteCommand(int type, byte[] parms = null,
			float timeout = 0.1f, int expectedReplyLen = 0, int br = 115200, bool isErrorExpected = false)
		{
			parms ??= new byte[0];
			var raw = new List<byte>()
			{
				0xA5,
				(byte)type,
				(byte)(parms.Length & 0xFF),
				(byte)((parms.Length >> 8) & 0xFF)
			};
			raw.AddRange(parms);
			raw.Add(StubCRC8(raw.ToArray(), raw.Count));

			serial.DiscardInBuffer();
			serial.Write(raw.ToArray(), 0, raw.Count);

			if(type == CMD_BAUD)
			{
				serial.BaseStream.Flush();
				var ms = (int)Math.Ceiling((raw.Count * 10.0 / serial.BaudRate) * 1000);
				Thread.Sleep(ms);
				serial.BaudRate = br;
			}

			int timeoutMs = Math.Max(1, (int)(timeout * 1000));
			Stopwatch sw = Stopwatch.StartNew();
			byte value;
			do
			{
				if(!ReadStubByte(sw, timeoutMs, out value))
				{
					if(!isErrorExpected) addErrorLine("Command response is empty!");
					return null;
				}
			} while(value != 0x5A);

			var response = new List<byte>() { value };
			for(int i = 0; i < 3; i++)
			{
				if(!ReadStubByte(sw, timeoutMs, out value))
				{
					if(!isErrorExpected) addErrorLine("Command response header is incomplete!");
					return null;
				}
				response.Add(value);
			}
			int dataLength = response[2] | response[3] << 8;
			for(int i = 0; i < dataLength + 2; i++)
			{
				if(!ReadStubByte(sw, timeoutMs, out value))
				{
					if(!isErrorExpected) addErrorLine("Command response is incomplete!");
					return null;
				}
				response.Add(value);
			}

			byte[] bytes = response.ToArray();
			if(bytes[1] != (byte)type)
			{
				if(!isErrorExpected) addErrorLine($"Command response type 0x{bytes[1]:X2} does not match 0x{type:X2}!");
				return null;
			}
			if(StubCRC8(bytes, bytes.Length - 1) != bytes[bytes.Length - 1])
			{
				addErrorLine("Command checksum is incorrect!");
				logger.setState("Checksum mismatch!", Color.Red);
				return null;
			}
			byte status = bytes[bytes.Length - 2];
			if(status != 0)
			{
				if(!isErrorExpected)
				{
					string statusName = status switch
					{
						0x01 => "ERROR",
						0x02 => "ADDR_ERROR",
						0x03 => "TYPE_ERROR",
						0x04 => "LEN_ERROR",
						0x05 => "CRC_ERROR",
						0x10 => "NOT_SUPPORTED",
						0x11 => "OTP_CANT_READ",
						_ => $"UNKNOWN_ERROR_{status:X2}"
					};
					addErrorLine($"Command failed with error code 0x{status:X2} - {statusName}!");
				}
				return null;
			}
			if(expectedReplyLen > 0 && dataLength != expectedReplyLen)
			{
				if(!isErrorExpected) addErrorLine($"Command reply length {dataLength} != expected {expectedReplyLen}");
				return null;
			}
			var ret = new byte[dataLength];
			Array.Copy(bytes, 4, ret, 0, dataLength);
			return ret;
		}

		protected byte[] InternalRead(int addr, int sectors)
		{
			if(sectors == 0)
			{
				addErrorLine($"Read length cannot be zero!");
				return null;
			}
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
				if(!isCancelled && !bUseCompressionIfPossible)
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
				if(bUseCompressionIfPossible)
				{
					byte comprLevel = chipType switch
					{
						BKType.ECR6600 => 2,
						BKType.W800 => 2,
						BKType.GD32VW553 => 2,
						BKType.OPL1000A2 => 1,
						BKType.RDA5981 => 5,
						BKType.RTL8710B => 2,
						BKType.RTL8721DA => 4,
						BKType.RTL8720E => 5,
						_ => 5,
					};
					msg = msg.Append(comprLevel).ToArray();
				}
				var res = ExecuteCommand(bUseCompressionIfPossible ? CMD_CUSTOM_XMODEM_READ_COMPRESSED : CMD_CUSTOM_XMODEM_READ, msg, 0.1f, 0);
				if(bUseCompressionIfPossible && res == null)
				{
					addErrorLine("Compressed read failed! Will try normal read...");
					bUseCompressionIfPossible = false;
					res = ExecuteCommand(CMD_CUSTOM_XMODEM_READ, msg, 0.1f, 0);
				}
				if(res == null)
					return null;
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
				if(bUseCompressionIfPossible)
				{
					int compressedLength = ret.Length;
					ret = Decompress(ret);
					addLogLine($"Uncompressed {compressedLength} bytes to {ret.Length} bytes, compression rate - {((double)ret.Length - compressedLength) / ret.Length * 100.0:F2}%");
				}
				if(ret.Length != startAmount)
				{
					addErrorLine($"Flash read returned {ret.Length} bytes; expected {startAmount} bytes.");
					return null;
				}
				addLogLine("Getting hash...");
				if(!CheckHash(addr, startAmount, ret))
				{
					if(!bIgnoreCRCErr)
						return null;
				}
				logger.setProgress(sectors, sectors);
				logger.setState("Read done", Color.DarkGreen);
				addLogLine("Read complete!");
				return ret;
			}
			finally
			{
				Thread.Sleep(1);
				SetBaud(115200);
			}
		}

		protected byte[] InternalReadRawMemory(int addr, int length, string targetKindName)
		{
			if(length <= 0)
			{
				addErrorLine($"Read length cannot be zero!");
				return null;
			}
			int received = 0;
			int currentOffset = addr;
			void Xm_PacketReceived(XMODEM sender, byte[] packet, bool endOfFileDetected)
			{
				if((received % 0x1000) == 0)
				{
					addLog($"0x{currentOffset:X}... ");
				}
				currentOffset += packet.Length;
				received += packet.Length;
				logger.setProgress(Math.Min(received, length), length);
			}
			try
			{
				if(!SetBaud(baudrate))
					return null;
				logger.setProgress(0, length);
				logger.setState("Reading " + targetKindName + "...", Color.Transparent);
				addLogLine("Requesting " + chipType + " " + targetKindName + " raw memory dump: address " + formatHex(addr) + ", length " + formatHex(length) + ".");
				var msg = new byte[8];
				msg[0] = (byte)(addr & 0xFF);
				msg[1] = (byte)((addr >> 8) & 0xFF);
				msg[2] = (byte)((addr >> 16) & 0xFF);
				msg[3] = (byte)((addr >> 24) & 0xFF);
				msg[4] = (byte)(length & 0xFF);
				msg[5] = (byte)((length >> 8) & 0xFF);
				msg[6] = (byte)((length >> 16) & 0xFF);
				msg[7] = (byte)((length >> 24) & 0xFF);
				var res = ExecuteCommand(CMD_CUSTOM_XMODEM_READ_RAW, msg, 2, 0);
				if(res == null)
				{
					throw new IOException(chipType + " " + targetKindName + " raw read command was not accepted.");
				}
				using(MemoryStream stream = new MemoryStream())
				{
					xm.PacketReceived += Xm_PacketReceived;
					try
					{
						var recv = xm.Receive(stream);
						if(recv != XMODEM.TerminationReasonEnum.EndOfFile)
						{
							throw new IOException(chipType + " " + targetKindName + " dump failed with " + recv + ".");
						}
					}
					finally
					{
						addLog(Environment.NewLine);
						xm.PacketReceived -= Xm_PacketReceived;
					}
					if(stream.Length < length)
					{
						throw new IOException("Read " + stream.Length + " bytes, but expected " + length + ".");
					}
					byte[] ret = stream.ToArray();
					if(ret.Length != length)
					{
						Array.Resize(ref ret, length);
					}
					logger.setProgress(length, length);
					logger.setState(targetKindName + " read success!", Color.Green);
					addLogLine("Read complete!");
					return ret;
				}
			}
			finally
			{
				SetBaud(115200);
			}
		}

		protected byte[] InternalReadEfusePayload(int expectedLength, string targetKindName, bool isOtp = false)
		{
			try
			{
				if(!SetBaud(baudrate))
					return null;
				if(expectedLength > 0)
					logger.setProgress(0, expectedLength);
				logger.setState("Reading " + targetKindName + "...", Color.Transparent);
				byte[] result = ExecuteCommand(isOtp ? CMD_CUSTOM_READ_OTP : CMD_CUSTOM_READ_EFUSE, null, 2, expectedLength) ?? throw new IOException($"{chipType} {targetKindName} command returned no data.");
				if(expectedLength > 0)
					logger.setProgress(expectedLength, expectedLength);
				logger.setState(targetKindName + " read success!", Color.Green);
				return result;
			}
			finally
			{
				SetBaud(115200);
			}
		}

		protected bool InternalWrite(int addr, byte[] data, int len = -1)
		{
			try
			{
				xm.PacketSent += Xm_PacketSent;
				if(!SetBaud(baudrate))
					return false;
				if(len < 0)
					len = data.Length;
				var hlen = len;
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
				var res = ExecuteCommand(bUseCompressionIfPossible ? CMD_CUSTOM_XMODEM_WRITE_COMPRESSED : CMD_CUSTOM_XMODEM_WRITE, cmd, 0.1f, 0);
				if(bUseCompressionIfPossible && res == null)
				{
					addErrorLine("Compressed write failed! Will try normal write...");
					bUseCompressionIfPossible = false;
					res = ExecuteCommand(CMD_CUSTOM_XMODEM_WRITE, cmd, 0.1f, 0);
				}
				if(res == null)
				{
					serial.Write(new[] { xm.EOT }, 0, 1);
					Thread.Sleep(100);
					return false;
				}
				int ret;
				var sw = new Stopwatch();
				if(bUseCompressionIfPossible)
				{
					var compData = Compress(data);
					addLogLine($"Using compression, writing {compData.Length} bytes, compression rate - {((double)data.Length - compData.Length) / data.Length * 100.0:F2}%");
					len = compData.Length;
					sw.Start();
					ret = xm.Send(compData, (uint)addr);
					sw.Stop();
				}
				else
				{
					sw.Start();
					ret = xm.Send(data, (uint)addr);
					sw.Stop();
				}
				logger.addLog(Environment.NewLine + $"Flash write took {sw.ElapsedMilliseconds} ms" + Environment.NewLine, Color.Gray);
				if(ret != len)
				{
					addErrorLine($"Write failed ({xm.TerminationReason})! Expected sent bytes: {len}, really sent: {ret}");
					Thread.Sleep(100);
					serial.Write(new[] { xm.EOT }, 0, 1);
					Thread.Sleep(100);
					return false;
				}
				addLogLine("Getting hash...");
				if(!CheckHash(addr, hlen, data))
				{
					return false;
				}
				logger.setState("Writing done", Color.DarkGreen);
				addLogLine("Done flash write " + len);
				return true;
			}
			finally
			{
				xm.PacketSent -= Xm_PacketSent;
				Thread.Sleep(1);
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
			if(flashID[2] > 0x31 && flashID[2] < 0x3C)
				flashID[2] -= 0x20;
			if(flashID[2] < 0x11 || flashID[2] > 0x1C)
				throw new Exception("Flash ID incorrect!");
			flashSizeMB = (1 << (flashID[2] - 0x11)) / 8;
			addLogLine($"Flash size is {flashSizeMB}MB");
			return flashID;
		}

		protected virtual bool CheckHash(int addr, int len, byte[] data)
		{
			var cmd = new byte[8];
			cmd[0] = (byte)(addr & 0xFF);
			cmd[1] = (byte)((addr >> 8) & 0xFF);
			cmd[2] = (byte)((addr >> 16) & 0xFF);
			cmd[3] = (byte)((addr >> 24) & 0xFF);
			cmd[4] = (byte)(len & 0xFF);
			cmd[5] = (byte)((len >> 8) & 0xFF);
			cmd[6] = (byte)((len >> 16) & 0xFF);
			cmd[7] = (byte)((len >> 24) & 0xFF);
			var sw = Stopwatch.StartNew();
			var res = ExecuteCommand(CMD_SHA256, cmd, 20f, 32);
			sw.Stop();
			if(res == null)
			{
				return false;
			}
			using var sha256Hash = SHA256.Create();
			var readHash = HashToStr(sha256Hash.ComputeHash(data));
			var expectedHash = HashToStr(res);
			if(readHash != expectedHash)
			{
				addErrorLine($"Hash mismatch!\r\ndevice:\t{expectedHash}\r\nflasher:\t{readHash}");
				logger.setState("SHA mismatch!", Color.Red);
				return false;
			}
			addSuccess($"Hash matches {expectedHash}!" + Environment.NewLine);
			logger.addLog($"Hash took {sw.ElapsedMilliseconds} ms" + Environment.NewLine, Color.Gray);
			return true;
		}

		protected virtual bool CheckCRC(int addr, int len, byte[] data)
		{
			var cmd = new byte[8];
			cmd[0] = (byte)(addr & 0xFF);
			cmd[1] = (byte)((addr >> 8) & 0xFF);
			cmd[2] = (byte)((addr >> 16) & 0xFF);
			cmd[3] = (byte)((addr >> 24) & 0xFF);
			cmd[4] = (byte)(len & 0xFF);
			cmd[5] = (byte)((len >> 8) & 0xFF);
			cmd[6] = (byte)((len >> 16) & 0xFF);
			cmd[7] = (byte)((len >> 24) & 0xFF);
			var sw = Stopwatch.StartNew();
			var res = ExecuteCommand(CMD_CUSTOM_CRC32, cmd, 20f, 4);
			sw.Stop();
			uint crc;
			if(res == null)
			{
				return false;
			}
			else
			{
				crc = BitConverter.ToUInt32(res, 0);
			}
			var calc = CRC.crc32_ver2(0xFFFFFFFF, data) ^ 0xFFFFFFFF;
			if(crc != calc)
			{
				logger.setState("CRC mismatch!", Color.Red);
				addErrorLine($"CRC mismatch!\r\ndevice:\t{formatHex(crc)}\r\nflasher:\t{formatHex(calc)}");
				return false;
			}
			addSuccess($"CRC matches {formatHex(calc)}!" + Environment.NewLine);
			logger.addLog($"CRC took {sw.ElapsedMilliseconds} ms" + Environment.NewLine, Color.Gray);
			return true;
		}

		internal override byte[] ReadMAC()
		{
			return ExecuteCommand(CMD_CUSTOM_GET_MAC, expectedReplyLen: 6);
		}

		protected virtual byte[] GetChipInfo()
		{
			var data = ExecuteCommand(CMD_CUSTOM_CHIP_INFO, expectedReplyLen: -1) ?? throw new Exception("Failed to get chip data from stub!");
			var stubPlatform = MiscUtils.ReadU32LE(data);
			if(!PlatformIDs.TryGetValue(chipType, out var chipID))
			{
				throw new Exception($"Platform {chipType} is not supported by this flasher!");
			}

			if(chipID != stubPlatform)
			{
				var runningPlatform = PlatformIDs.Where(x => x.Value == stubPlatform);
				if(runningPlatform.Count() == 0)
				{
					throw new Exception($"Got data from stub, but running platform is not known and flasher is configured for {chipType}!");
				}
				throw new Exception($"Running platform is {runningPlatform.First().Key}, but flasher is configured for {chipType}!");
			}

			return data;
		}

		protected virtual bool CheckChipInfo(Action<byte[]> action = null)
		{
			try
			{
				var data = GetChipInfo();
				action?.Invoke(data);
				return true;
			}
			catch(Exception e)
			{
				addErrorLine(e.Message);
				return false;
			}
		}

		public override void Dispose()
		{
			ms?.Dispose();
			ms = null;
			closePort();
			base.Dispose();
		}
	}
}
