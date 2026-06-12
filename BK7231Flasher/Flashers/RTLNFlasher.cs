using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class RTLNFlasher : ECRBaseFlasher
	{
		byte[] flashID;

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
				serial = new SerialPort(serialName, 115200);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				serial.ReadTimeout = 2000;
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

		protected override bool Sync()
		{
			//Thread.Sleep(200);
			//if(serial.BytesToRead > 0 && serial.ReadByte() == xm.C)
			//{
			//	serial.Write(new[] { xm.EOT }, 0, 1);
			//	Thread.Sleep(200);
			//}
			flashID = ReadFlashId(true);
			if(flashID != null)
			{
				addLogLine("Stub is already uploaded!");
				return true;
			}
			else
			{
				addLogLine("Sending RAM code...");
				var stub = chipType switch
				{
					BKType.RTL8721DA => FLoaders.GetBinaryFromAssembly("RTL8721DA_Stub"),
					BKType.RTL8720E => FLoaders.GetBinaryFromAssembly("RTL8720E_Stub"),
					_ => throw new Exception()
				};
				
				var offset = chipType switch
				{
					BKType.RTL8721DA => 0x3000A000,
					BKType.RTL8720E => 0x3000A000,
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
				Thread.Sleep(100);
				serial.DiscardInBuffer();
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
					InternalWrite(startSector, data);
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null && !isCancelled)
				{
					if(cfg != null)
					{
						addErrorLine($"OBK config write is not supported on {chipType}.");
					}
					else
					{
						addLog("NOTE: the OBK config writing is disabled, so not writing anything extra." + Environment.NewLine);
					}
				}
			}
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

