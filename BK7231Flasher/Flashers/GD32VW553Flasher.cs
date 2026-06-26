using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
	public class GD32VW553Flasher : ECRBaseFlasher
	{
		static readonly byte GD32_GET = 0x00;
		static readonly byte GD32_PID = 0x06;
		//static readonly byte GD32_READ = 0x11;
		static readonly byte GD32_JUMP = 0x21;
		static readonly byte GD32_PROGRAM = 0x31;
		//static readonly byte GD32_ERASE = 0x44;
		static readonly byte ACK = 0x79;
		static readonly byte NACK = 0x1F;

		private static byte[] AllowedCommands;

		public GD32VW553Flasher(CancellationToken ct) : base(ct)
		{
		}

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				serial = new SerialPort(serialName, 115200, Parity.Even);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				serial.ReadTimeout = 2000;
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF);
			}
			catch(Exception ex)
			{
				addLog("Port setup failed with " + ex.Message + "!" + Environment.NewLine);
				return false;
			}
			addLog("Port ready!" + Environment.NewLine);
			return true;
		}

		private bool SendCommand(byte cmd)
		{
			serial.DiscardInBuffer();

			serial.Write(new[] { cmd, (byte)~cmd }, 0, 2);

			return CheckAck(cmd, "first ACK");
		}

		private bool CheckAck(byte cmd, string stage)
		{
			int ack = serial.ReadByte();

			if(ack == ACK) return true;

			if(ack == NACK) addErrorLine("NACK received!");

			addErrorLine($"Command 0x{cmd:X} failed on {stage}!");

			return false;
		}

		private static byte ChecksumXor(Span<byte> data)
		{
			byte checksum = 0;

			foreach(byte b in data) checksum ^= b;

			return checksum;
		}

		private static byte[] CreateAddressPacket(int addr)
		{
			byte[] packet =
			{
				(byte)(addr >> 24),
				(byte)(addr >> 16),
				(byte)(addr >> 8),
				(byte)addr
			};

			return packet.Append(ChecksumXor(packet)).ToArray();
		}

		private static byte[] CreateDataPacket(byte[] data)
		{
			byte[] packet = new byte[data.Length + 2];

			packet[0] = (byte)(data.Length - 1);

			Array.Copy(data, 0, packet, 1, data.Length);

			packet[packet.Length - 1] = ChecksumXor(packet.AsSpan(0, packet.Length - 1));

			return packet;
		}

		private byte[] SendGETCommand(out byte bootVersion)
		{
			bootVersion = 0;

			if(!SendCommand(GD32_GET)) return null;

			var len = serial.ReadByte();
			bootVersion = (byte)serial.ReadByte();
			var data = new byte[len];
			serial.Read(data, 0, len);

			if(!CheckAck(GD32_GET, "final")) return null;

			return data;
		}

		private byte[] SendPIDCommand()
		{
			if(!SendCommand(GD32_PID)) return null;

			var len = serial.ReadByte();
			var data = new byte[len];
			Thread.Sleep(1);
			serial.Read(data, 0, len);

			if(!CheckAck(GD32_PID, "final")) return null;

			return data;
		}

		private bool SendUploadCommand(int addr, byte[] data)
		{
			if(!AllowedCommands.Contains(GD32_PROGRAM))
			{
				addErrorLine($"Command 0x{GD32_PROGRAM:X} is not allowed!");
				return false;
			}
			if(data.Length > 256)
			{
				addErrorLine($"Data is too long!");
				return false;
			}

			if(!SendCommand(GD32_PROGRAM)) return false;

			var packet = CreateAddressPacket(addr);

			serial.Write(packet, 0, packet.Length);

			if(!CheckAck(GD32_PROGRAM, "address")) return false;

			var bdata = CreateDataPacket(data);

			serial.Write(bdata, 0, bdata.Length);

			if(!CheckAck(GD32_PROGRAM, "data")) return false;

			return true;
		}

		private bool SendJumpCommand(int addr)
		{
			if(!AllowedCommands.Contains(GD32_JUMP))
			{
				addErrorLine($"Command 0x{GD32_JUMP:X} is not allowed!");
				return false;
			}

			if(!SendCommand(GD32_JUMP)) return false;

			var packet = CreateAddressPacket(addr);

			serial.Write(packet, 0, packet.Length);

			return CheckAck(GD32_JUMP, "address");
		}

		//private byte[] SendReadCommand(int addr, byte len)
		//{
		//	if(!AllowedCommands.Contains(GD32_READ))
		//	{
		//		addErrorLine($"Command 0x{GD32_READ:X} is not allowed!");
		//		return null;
		//	}
		//
		//	if(!SendCommand(GD32_READ)) return null;
		//
		//	var address = CreateAddressPacket(addr);
		//
		//	serial.Write(address, 0, address.Length);
		//
		//	if(!CheckAck(GD32_READ, "address")) return null;
		//
		//	serial.Write(new[] { len, (byte)~len }, 0, 2);
		//
		//	if(!CheckAck(GD32_READ, "length")) return null;
		//
		//	byte[] data = new byte[len];
		//
		//	int tries = 1000;
		//	while(serial.BytesToRead < len && tries-- > 0)
		//		Thread.Sleep(1);
		//
		//	serial.Read(data, 0, len);
		//
		//	return data;
		//}

		protected override bool Sync()
		{
			var timeout = serial.ReadTimeout;
			serial.ReadTimeout = 100;
			serial.Parity = Parity.Even;
			var sync = new byte[] { 0x7F };
			serial.Write(sync, 0, 1);
			var resp = 0;
			try
			{
				resp = serial.ReadByte();
			}
			catch { }
			serial.ReadTimeout = timeout;
			if(resp == 0x79 || resp == 0x1F)
			{
				AllowedCommands = SendGETCommand(out var bootVersion);
				addLogLine($"Bootloader version: 0x{bootVersion:X}");
				var pid = Encoding.ASCII.GetString(SendPIDCommand().Take(4).ToArray());
				addLogLine($"Product ID: {pid}");
				flashSizeMB = pid[2] switch
				{
					'I' => 2,
					'M' => 4,
					_ => throw new Exception("Unknown chip rev")
				};
				addLogLine($"Flash size is {flashSizeMB}MB");
				return UploadStub();
			}
			serial.Parity = Parity.None;
			var stubsync = ExecuteCommand(CMD_SYN, Encoding.ASCII.GetBytes("cnys"), 0.2f, 0, isErrorExpected: false);
			if(stubsync != null)
			{
				addLogLine("Stub is already uploaded!");
				return true;
			}
			return false;
		}

		private bool UploadStub()
		{
			var stub = FLoaders.GetBinaryFromAssembly("GD32VW553_Stub");
			var startupAddress = 0x20002000;
			var uploadAddress = startupAddress;
			var amount = stub.Length;
			addLogLine($"Uploading stub to 0x{startupAddress:X}...");
			logger.setState("Uploading stub", Color.White);
			logger.setProgress(0, amount);
			while(amount > 0)
			{
				if(isCancelled) return false;
				var packet = new byte[amount > 256 ? 256 : amount];
				addLog($"0x{uploadAddress - startupAddress:X}..");
				Array.Copy(stub, stub.Length - amount, packet, 0, packet.Length);
				if(!SendUploadCommand(uploadAddress, packet))
				{
					logger.setState("Stub failure", Color.White);
					addErrorLine($"Stub upload failed!");
					return false;
				}
				uploadAddress += packet.Length;
				amount -= packet.Length;
				logger.setProgress(stub.Length - amount, stub.Length);
			}
			addLogLine($"\r\nStub uploaded! Jumping...");

			if(!SendJumpCommand(startupAddress))
			{
				logger.setState("Stub failure", Color.White);
				addErrorLine($"Jump to stub failed!");
				return false;
			}

			serial.Parity = Parity.None;
			Thread.Sleep(10);
			serial.Write(stub, 0, 1); // dummy write to let stub select uart
			serial.DiscardInBuffer();
			serial.DiscardOutBuffer();
			var stubsync = ExecuteCommand(CMD_SYN, Encoding.ASCII.GetBytes("cnys"), 0.2f, 0, isErrorExpected: false);
			if(stubsync == null)
				return false;
			return true;
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
						var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
						var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

						cfg.saveConfig(chipType);
						var cfgData = cfg.getData();
						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
						bool bOk = InternalWrite(offset, cfgData, areaSize);
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
	}
}

