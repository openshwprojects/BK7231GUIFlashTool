using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
	public class RTLNFlasher : ECRBaseFlasher
	{
		byte[] flashID;
		static readonly byte CMD_KV_GET = 0x93;
		static readonly byte CMD_KV_SET = 0x94;
		static readonly byte CMD_MAC_GET = 0x95;

		public RTLNFlasher(CancellationToken ct) : base(ct)
		{
		}

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				serial = new SerialPort(serialName, 115200)
				{
					ReadBufferSize = 65536,
					ReadTimeout = 8000
				};
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
				{
					MaxSenderRetries = 50,
					ReceiverMaxConsecutiveRetries = 50
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

		internal byte[] ReadMAC()
		{
			return ExecuteCommand(CMD_MAC_GET, expectedReplyLen: 6);
		}

		private bool SetBaud(int baud, bool noCheck = false)
		{
			if(serial.BaudRate != baud)
			{
				int givenBaud = baud;
				int x = 0x0D;
				int[] br = { 115200, 128000, 153600, 230400, 380400, 460800, 500000, 921600, 1000000, 1382400, 1444400, 1500000, 1843200, 2000000, 2100000, 2764800, 3000000, 3250000, 3692300, 3750000, 4000000, 6000000 };
				foreach(int el in br)
				{
					if(el >= baud)
					{
						baud = el;
						break;
					}
					x++;
				}
				addLog("Setting baud rate " + baud + " (given as " + givenBaud + ")...");
				byte[] pkt = new byte[2];
				pkt[0] = 0x05;
				pkt[1] = (byte)x;
				if(noCheck)
				{
					serial.Write(pkt, 0, pkt.Length);
				}
				else
				{
					if(!WriteCmd(pkt))
					{
						addLog("... ERROR!" + Environment.NewLine);
						return false;
					}
				}
				addLog("... OK!" + Environment.NewLine);

				return SetComBaud(baud);
			}
			return true;
		}

		private bool SetComBaud(int baud)
		{
			try
			{
				serial.Close();
				serial.BaudRate = baud;
				serial.Open();
				serial.ReadTimeout = 8000;
				serial.WriteTimeout = 8000;
				Thread.Sleep(50);
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
			}
			catch
			{
				addErrorLine("Error: ReOpen COM port at " + baud);
				return false;
			}
			return true;
		}
		protected override bool Sync()
		{
			//Thread.Sleep(200);
			//if(serial.BytesToRead > 0 && serial.ReadByte() == xm.C)
			//{
			//	serial.Write(new[] { xm.EOT }, 0, 1);
			//	Thread.Sleep(200);
			//}
			if(isCancelled) return false;
			flashID = ReadFlashId(true);
			if(flashID != null)
			{
				addLogLine("Stub is already uploaded!");
				return true;
			}
			else
			{
				SetBaud(460800);
				addLogLine("Sending RAM code...");
				var stub = chipType switch
				{
					BKType.RTL8721DA => FLoaders.GetBinaryFromAssembly("RTL8721DA_Stub"),
					BKType.RTL8720E => FLoaders.GetBinaryFromAssembly("RTL8720E_Stub"),
					_ => throw new Exception()
				};
				
				var offset = chipType switch
				{
					BKType.RTL8721DA => 0x3000A020,
					BKType.RTL8720E => 0x3000A020,
					_ => throw new Exception()
				};
				addLogLine($"Write Floader to SRAM at 0x{offset:X8} to 0x{offset + stub.Length:X8}");
				try
				{
					xm.PacketSent += Xm_RtlPacketSent;
					xm.Variant = XMODEM.Variants.XModem1KChecksum;
					if(!WriteBlockMem(stub, offset, stub.Length))
					{
						addErrorLine("Error Write!");
						return false;
					}
					xm.Abort();
					xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF);
				}
				finally
				{
					xm.PacketSent -= Xm_RtlPacketSent;
				}
				addLogLine("");
				//Thread.Sleep(100);
				//serial.DiscardInBuffer();
				//serial.BaudRate = 115200;
				SetComBaud(115200);
				flashID = ReadFlashId(false);
				if(flashID != null)
				{
					addLogLine("RAM code ready!");
					return true;
				}
				else
				{
					addLogLine("RAM code sync failed!");
					return false;
				}
			}
			return false;
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
					byte[] res = InternalRead(startSector, sectors, bUseCompressionIfPossible);
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
					InternalWrite(startSector, data);
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null && !isCancelled)
				{
					if(cfg != null)
					{
						cfg.saveConfig(chipType);
						var cfgdata = cfg.getData();
						var cfgname = Encoding.ASCII.GetBytes("ObkCfg");

						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);

						var data = new byte[cfgname.Length + 1 + 2];
						data[0] = (byte)cfgname.Length;
						data[1] = (byte)(cfgdata.Length & 0xFF);
						data[2] = (byte)((cfgdata.Length >> 8) & 0xFF);
						Array.Copy(cfgname, 0, data, 3, cfgname.Length);
						var res = ExecuteCommand(CMD_KV_SET, data, 0.5f, 0);
						if(res == null)
						{
							serial.Write(new[] { xm.EOT }, 0, 1);
							logger.setState("OBK config write failed!", Color.Red);
							return;
						}
						var ret = xm.Send(cfgdata);
						if(ret != cfgdata.Length)
						{
							addErrorLine($"Write failed ({xm.TerminationReason})! Expected sent bytes: {cfgdata.Length}, really sent: {ret}");
							addError("Writing OBK config data to chip failed." + Environment.NewLine);
							logger.setState("OBK config write failed!", Color.Red);
							Thread.Sleep(100);
							serial.Write(new[] { xm.EOT }, 0, 1);
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

		public override void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
		{
			if(startSector == int.MinValue && sectors == int.MinValue)
			{
				if(doGenericSetup() == false)
				{
					return;
				}
				if(Sync())
				{
					var res = ExecuteCommand(CMD_KV_GET, Encoding.ASCII.GetBytes("ObkCfg"), 0.5f, 3584);
					ms?.Dispose();
					ms = null;
					if(res != null)
					{
						ms = new MemoryStream(res);
						logger.setState("Read success!", Color.Green);
						return;
					}
					logger.setState("Read failed!", Color.Red);
				}
			}
			else base.doRead(startSector, sectors, fullRead);
		}

		private void Xm_RtlPacketSent(int sentBytes, int total, int sequence, uint offset)
		{
			base.Xm_PacketSent(sentBytes, total, sequence, offset);
			xm.ExtraHeaderBytes = new byte[4]
			{
				(byte)(offset & 0xFF),
				(byte)((offset >> 8) & 0xFF),
				(byte)((offset >> 16) & 0xFF),
				(byte)((offset >> 24) & 0xFF),
			};
		}

		public bool WriteBlockMem(byte[] stream, int offset, int size)
		{
			if(!WriteCmd(new byte[] { 0x07 }))
				return false;
			return xm.Send(stream, (uint)offset) == size;
		}

		private bool WriteCmd(byte[] cmd, byte ack = 0x06)
		{
			try
			{
				serial.Write(cmd, 0, cmd.Length);
				return WaitResp(ack); // ACK
			}
			catch
			{
				return false;
			}
		}
		private bool WaitResp(byte code, int retries = 1000)
		{
			while(retries-- > 0)
			{
				try
				{
					if(isCancelled) return false;
					int val = serial.ReadByte();
#if false
					if (false)
					{
						Console.WriteLine("Try " + retries + " wants " + code + " got " + val);
					}
#endif
					if(val == -1)
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
	}
}

