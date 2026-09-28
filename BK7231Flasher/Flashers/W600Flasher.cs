using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class W600Flasher : BaseFlasher
	{
		byte[] flashID;

		public W600Flasher(CancellationToken ct) : base(ct)
		{
		}

		private bool doGenericSetup()
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
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
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

		private byte[] ReadFlashId()
		{
			var res = ExecuteCommand(0x3C, null, 1, 10, isErrorExpected: true);
			if(res != null && res[0] == 'F' && res[1] == 'I' && res[2] == 'D')
			{
				flashID = new byte[] { Convert.ToByte($"{(char)res[4]}{(char)res[5]}", 16) };
				addLogLine($"Flash ID: 0x{flashID[0]:X}");
				return flashID;
			}
			addLogLine("Getting flash id failed, assuming device is in secboot mode.");
			addLogLine("Erasing secboot, will resync...");
			ExecuteCommand(0x3F, null, 1, 2);
			if(!Sync())
				return null;
			return ReadFlashId();
		}

		private bool Sync()
		{
			serial.DiscardInBuffer();
			var count = 0;
			try
			{
				int attempts = 0;
				while(attempts++ < 1000)
				{
					byte sync = 0;
					try { sync = (byte)serial.ReadByte(); } catch { }
					if(sync == 'C')
					{
						count++;
					}
					else
					{
						if(sync == 'P')
							continue;
						for(int i = 0; i < 250; i++)
						{
							serial.Write(new byte[] { 0x1B }, 0, 1);
							Thread.Sleep(1);
						}
						addLogLine($"Sync attempt {attempts}/1000 failed...");
						serial.DiscardInBuffer();
						count = 0;
					}
					if(count > 3)
					{
						addLogLine("Sync success!");
						return true;
					}
				}
			}
			catch(Exception ex)
			{
				addErrorLine(ex.Message);
			}
			return false;
		}

		private bool InitialiseTarget()
		{
			return Sync() && ReadFlashId() != null;
		}

		private byte[] ExecuteCommand(int type, byte[] parms = null,
			float timeout = 0.1f, int expectedReplyLen = 0, int br = 115200, bool isErrorExpected = false)
		{
			parms = parms ?? new byte[0];
			var cmd = new List<byte>()
			{
				(byte)(type & 0xFF),
				(byte)((type >> 8) & 0xFF),
				(byte)((type >> 16) & 0xFF),
				(byte)((type >> 24) & 0xFF)
			};
			cmd.AddRange(parms);
			var raw = new List<byte>()
			{
				0x21,
				(byte)(cmd.Count + 2 & 0xFF),
				(byte)((cmd.Count + 2 >> 8) & 0xFF)
			};
			var crc = CRC16.Compute(CRC16Type.CCITT_FALSE, cmd.ToArray());
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
			int timeoutMs = (int)(timeout * 1000);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			while(sw.ElapsedMilliseconds < timeoutMs)
			{
				if(serial.BytesToRead >= expectedReplyLen)
					break;
			}
			if(serial.BytesToRead == 0)
			{
				if(!isErrorExpected) addErrorLine("Command response is empty!");
				return null;
			}
			var bytes = new byte[serial.BytesToRead];
			serial.Read(bytes, 0, bytes.Length);
			if(bytes.Length < expectedReplyLen)
			{
				if(!isErrorExpected) addErrorLine($"Command reply length {bytes.Length} < expected {expectedReplyLen}");
				return null;
			}
			var ret = new byte[expectedReplyLen];
			Array.Copy(bytes, ret, expectedReplyLen);
			return ret;
		}

		private bool SetBaud(int baud, bool noResync = false)
		{
			if(serial.BaudRate == baud)
				return true;
			addLogLine($"Changing baud to {baud}{(!noResync ? ", will resync..." : string.Empty)}");
			byte[] msg = BitConverter.GetBytes(baud);
			ExecuteCommand(0x31, msg, 1, 1, baud, noResync);
			return noResync || Sync();
		}

		private bool EraseAndWait(string label, byte[] parms, int timeoutSeconds)
		{
			addLogLine(label);
			var response = ExecuteCommand(0x32, parms, timeoutSeconds, 4);
			return response != null && response.Length >= 4 &&
				response[0] == 'C' && response[1] == 'C' && response[2] == 'C' && response[3] == 'C';
		}

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			addErrorLine("W600 doesn't support read. Use JLink for firmware backup.");
		}

		public override void doWrite(int startSector, byte[] data)
		{
			return;
		}

		public override bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
		{
			if(!bAll)
			{
				addErrorLine("W600 range erase is not implemented.");
				return false;
			}
			if(!doGenericSetup() || !InitialiseTarget())
				return false;
			bool ok = EraseAndWait("Erasing W600 flash...", null, 24);
			if(ok)
			{
				logger.setState("Erase done", Color.DarkGreen);
				logger.setProgress(1, 1);
				addLogLine("Erase flash ok.");
			}
			else
			{
				logger.setState("Erase error!", Color.Red);
			}
			return ok;
		}

		public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
		{
			if(rwMode == WriteMode.ReadAndWrite)
			{
				addErrorLine("W600 doesn't support read. Use JLink for firmware backup.");
				return;
			}
			if(rwMode == WriteMode.OnlyOBKConfig)
			{
				addErrorLine("Writing only OBK config is disabled for W600, use \"Automatically configure OBK on flash write\".");
				return;
			}
			if(!doGenericSetup() || !InitialiseTarget())
				return;
			try
			{
				xm.PacketSent += Xm_PacketSent;
				SetBaud(baudrate);
				OBKConfig cfg = logger.getConfigToWrite();
				if(rwMode == WriteMode.OnlyWrite)
				{
					if(string.IsNullOrEmpty(sourceFileName))
					{
						addLogLine("No filename given!");
						return;
					}
					addLogLine("Reading " + sourceFileName + "...");
					byte[] data = File.ReadAllBytes(sourceFileName);
					if(sourceFileName.EndsWith(".fls", StringComparison.OrdinalIgnoreCase))
					{
						if(xm.Send(data) != data.Length)
						{
							logger.setState("Write error!", Color.Red);
							addErrorLine("Write error!");
							return;
						}
					}
					else if(data.Length >= 0x100000)
					{
						startSector = 0x2000;
						var secBootHeader = new byte[64];
						Array.Copy(data, startSector, secBootHeader, 0, secBootHeader.Length);
						if(secBootHeader[0] != 0x9F || secBootHeader[1] != 0xFF || secBootHeader[2] != 0xFF || secBootHeader[3] != 0xA0)
						{
							addErrorLine("Unknown file type, no firmware header at 0x2000!");
							return;
						}
						if(secBootHeader[60] != 0xFF || secBootHeader[61] != 0xFF || secBootHeader[62] != 0xFF || secBootHeader[63] != 0xFF)
						{
							addErrorLine("Not W600 backup!");
							return;
						}
						var cutData = new byte[data.Length - startSector];
						Array.Copy(data, startSector, cutData, 0, cutData.Length);
						var fls = GenerateW600PseudoFLSFromData(cutData, startSector | 0x08000000);
						if(xm.Send(fls, (uint)startSector) != fls.Length)
						{
							logger.setState("Write error!", Color.Red);
							return;
						}
					}
					else
					{
						addErrorLine("Unknown file type, skipping.");
						return;
					}
					logger.setState("Writing done", Color.DarkGreen);
					addLogLine("Done flash write " + data.Length);
					logger.setProgress(1, 1);
				}

				if(cfg != null && !isCancelled)
				{
					int offset = OBKFlashLayout.getConfigLocation(chipType, out _) | 0x08000000;
					cfg.saveConfig(chipType);
					var data = MiscUtils.padArray(cfg.getData(), BK7231Flasher.SECTOR_SIZE);
					addLog("Now will also write OBK config..." + Environment.NewLine);
					addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
					addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
					addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
					var fls = GenerateW600PseudoFLSFromData(data, offset);
					if(xm.Send(fls, (uint)(offset ^ 0x08000000)) == fls.Length)
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
				if(!isCancelled) SetBaud(115200, true);
			}
		}

		private byte[] GenerateW600PseudoFLSFromData(byte[] data, int startAddr)
		{
			var crc = CRC.crc32_ver2(0xFFFFFFFF, data);
			var fls = new List<byte>()
			{
				0x9F, 0xFF, 0xFF, 0xA0,
				0x00, 0x02, 0x00, 0x00,
				(byte)(startAddr & 0xFF), (byte)((startAddr >> 8) & 0xFF),
				(byte)((startAddr >> 16) & 0xFF), (byte)((startAddr >> 24) & 0xFF),
				(byte)(data.Length & 0xFF), (byte)((data.Length >> 8) & 0xFF),
				(byte)((data.Length >> 16) & 0xFF), (byte)((data.Length >> 24) & 0xFF),
				(byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF),
				(byte)((crc >> 16) & 0xFF), (byte)((crc >> 24) & 0xFF),
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
			};
			var headerCrc = CRC.crc32_ver2(0xFFFFFFFF, fls.ToArray());
			fls.Add((byte)(headerCrc & 0xFF));
			fls.Add((byte)((headerCrc >> 8) & 0xFF));
			fls.Add((byte)((headerCrc >> 16) & 0xFF));
			fls.Add((byte)((headerCrc >> 24) & 0xFF));
			fls.AddRange(data);
			return fls.ToArray();
		}
	}
}
