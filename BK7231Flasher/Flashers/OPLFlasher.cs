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
	public class OPLFlasher : ECRBaseFlasher, IRomReadFlasher
	{
		public OPLFlasher(CancellationToken ct) : base(ct)
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
				serial.ReadTimeout = 1000;
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

		private bool UploadAndJump(int addr, byte[] data)
		{
			serial.DiscardInBuffer();
			var offset = 0;
			while(offset < data.Length)
			{
				var len = Math.Min(16, data.Length - offset);
				var record = BuildS3Record(addr + offset, data, offset, len);
				serial.Write(record, 0, record.Length);
				if(!WaitForString("x", 100))
				{
					addErrorLine("First stub upload failed!");
					return false;
				}
				offset += len;
				logger.setProgress(offset, data.Length);
			}

			var s7 = BuildS7Record(addr);
			serial.Write(s7, 0, s7.Length);
			serial.Write("\r");

			if(!WaitForString("Jump(j)", 1000))
			{
				addErrorLine("First stub final check failed!");
				return false;
			}

			addLogLine("Jumping...");

			serial.Write(new byte[] { (byte)'j' }, 0, 1);
			Thread.Sleep(2);
			var rest = serial.ReadExisting();
			if(rest.Contains("Continue"))
			{
				addErrorLine("Jump failed!");
				return false;
			}
			return true;
		}

		private bool UploadStub()
		{
			serial.BaudRate = 115200;
			var stub = FLoaders.GetBinaryFromAssembly("OPL1000A2_Stub");
			var stage1 = FLoaders.GetRawBinaryFromAssembly("OPL1000A2_Stub_stage1");
			var baudRate = baudrate > 1419000 ? 921600 : baudrate;
			MiscUtils.WriteU32LE(stage1, 8, (uint)baudRate);

			serial.DiscardInBuffer();
			logger.setState("Syncing", Color.Transparent);

			if(!WaitForString("<CHECK>", 5000))
				return false;

			serial.Write(new byte[] { 0xFE, (byte)'u' }, 0, 2);

			if(!WaitForString("Wait for SREC", 1000))
				return false;

			addLogLine();
			addLogLine("Base ROM synced, uploading stage 1 stub.");
			logger.setState("Uploading", Color.Transparent);
			if(!UploadAndJump(0x00440000, stage1))
			{
				return false;
			}
			addLogLine("Uploading stage 2 stub.");
			serial.BaudRate = baudRate;
			var sent = xm.Send(stub, instant: true);
			if(sent != stub.Length)
			{
				addErrorLine("Second stub upload failed!");
			}
			addLogLine("Done.");
			if(isCancelled)
				return false;

			Thread.Sleep(10);
			serial.BaudRate = 115200;
			serial.DiscardInBuffer();
			var flashID = ReadFlashId(false);
			if(flashID != null)
			{
				addLogLine("Stub ready!");
				return true;
			}
			else
			{
				addErrorLine("Stub sync failed!");
				logger.setState("Stub sync failed!", Color.Red);
			}
			return false;
		}

		private bool WaitForString(string target, int timeoutMs)
		{
			var sb = new StringBuilder();
			var sw = Stopwatch.StartNew();

			while(sw.ElapsedMilliseconds < timeoutMs)
			{
				try
				{
					if(isCancelled)
						return false;
					if(serial.BytesToRead > 0)
					{
						int b = serial.ReadByte();
						sb.Append((char)b);
						if(sb.ToString().Contains("nvalid"))
							return false;
						if(sb.ToString().EndsWith(target))
							return true;
					}
					else
						Thread.Sleep(1);
				}
				catch { }
			}
			return false;
		}

		private static byte[] BuildS3Record(int addr, byte[] data, int offset, int length)
		{
			var payload = new byte[length];
			Array.Copy(data, offset, payload, 0, length);

			var count = 4 + length + 1;

			var sum = count;
			sum += (addr >> 24) & 0xFF;
			sum += (addr >> 16) & 0xFF;
			sum += (addr >> 8) & 0xFF;
			sum += addr & 0xFF;

			foreach(byte b in payload)
				sum += b;

			var checksum = (byte)((~sum) & 0xFF);

			var recordStr = $"S3{count:X2}{addr:X8}{BitConverter.ToString(payload).Replace("-", "").ToUpperInvariant()}{checksum:X2}\r";

			return Encoding.ASCII.GetBytes(recordStr);
		}

		private static byte[] BuildS7Record(int addr)
		{
			var count = 5;
			var sum = count;
			sum += (addr >> 24) & 0xFF;
			sum += (addr >> 16) & 0xFF;
			sum += (addr >> 8) & 0xFF;
			sum += addr & 0xFF;

			var checksum = (byte)((~sum) & 0xFF);
			var recordStr = $"S7{count:X2}{addr:X8}{checksum:X2}";
			return Encoding.ASCII.GetBytes(recordStr);
		}

		protected override bool Sync()
		{
			if(isCancelled)
				return false;
			if(ReadFlashId(true) != null)
			{
				addLogLine("Stub is already uploaded!");
				return true;
			}
			else
			{
				int attempts = 0;
				while(attempts++ < 500)
				{
					addLog($"Sync attempt {attempts}/500 ");
					if(!UploadStub())
					{
						if(isCancelled)
							return false;
						addWarningLine("... failed, will retry!");
					}
					else
					{
						return true;
					}
					if(isCancelled)
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
					var startAddr = 0;
					if(!InternalWrite(startAddr, data))
					{
						logger.setState("Write error!", Color.Red);
						return;
					}
				}
				if(rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig)
				{
					if(cfg != null)
					{
						var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
						var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

						cfg.saveConfig(chipType);
						var cfgData = cfg.getData();
						byte[] efdata;
						if(cfg.efdata != null)
						{
							try
							{
								efdata = EasyFlash.SaveValueToExistingEasyFlash("ObkCfg", cfg.efdata, cfgData, areaSize, chipType);
							}
							catch(Exception ex)
							{
								addLog("Saving config to existing EasyFlash failed" + Environment.NewLine);
								addLog(ex.Message + Environment.NewLine);
								efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
							}
						}
						else
						{
							efdata = EasyFlash.SaveValueToNewEasyFlash("ObkCfg", cfgData, areaSize, chipType);
						}
						if(efdata == null)
						{
							addLog("Something went wrong with EasyFlash" + Environment.NewLine);
							return;
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

		protected override bool CheckHash(int addr, int len, byte[] data)
		{
			//return base.CheckHash(addr, len, data);
			var cmd = new byte[8];
			cmd[0] = (byte)(addr & 0xFF);
			cmd[1] = (byte)((addr >> 8) & 0xFF);
			cmd[2] = (byte)((addr >> 16) & 0xFF);
			cmd[3] = (byte)((addr >> 24) & 0xFF);
			cmd[4] = (byte)(len & 0xFF);
			cmd[5] = (byte)((len >> 8) & 0xFF);
			cmd[6] = (byte)((len >> 16) & 0xFF);
			cmd[7] = (byte)((len >> 24) & 0xFF);
			var res = ExecuteCommand(0x8F, cmd, 30f, 4);
			uint crc;
			if(res == null)
			{
				return false;
			}
			else
			{
				crc = BitConverter.ToUInt32(res, 0);
			}
			uint calc = 0;
			unchecked
			{
				for(int pos = 0; pos < data.Length; pos += 0x1000)
				{
					int blen = Math.Min(0x1000, data.Length - pos);
					calc += CRC.crc32_ver2(0xFFFFFFFF, data, blen, (uint)pos) ^ 0xFFFFFFFF;
				}
			}
			if(crc != calc)
			{
				logger.setState("CRC mismatch!", Color.Red);
				addErrorLine("CRC mismatch!");
				addErrorLine($"Sent by OPL {formatHex(crc)}, our CRC {formatHex(calc)}");
				return false;
			}
			addSuccess($"CRC matches {formatHex(calc)}!" + Environment.NewLine);
			return true;
		}

		internal override byte[] ReadMAC()
		{
			var rf_efuse = ExecuteCommand(CMD_CUSTOM_READ_EFUSE, expectedReplyLen: 512);
			if(rf_efuse == null)
				return null;
			var mac = new byte[6];
			Array.Copy(rf_efuse, 0x100, mac, 0, 6);
			return mac;
		}


		public byte[] ReadRomTarget(RomReadTarget target)
		{
			try
			{
				if(doGenericSetup() == false)
				{
					return null;
				}
				if(Sync() == false)
				{
					logger.setState("Sync failed!", Color.Red);
					return null;
				}
				string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
				switch(target.Kind)
				{
					case RomReadKind.Rom:
						return InternalReadRawMemory(target.Address.Value, target.Length.Value, targetKindName);
					case RomReadKind.Efuse:
						addLogLine("Reading " + chipType + " eFuse via custom stub command 0x99.");
						return InternalReadEfusePayload(target.Length.Value, targetKindName);
					default:
						addError("Selected OPL1000A2 read target is not implemented." + Environment.NewLine);
						return null;
				}
			}
			catch(OperationCanceledException)
			{
				string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
				addLogLine(targetKindName + " read cancelled by user.");
				logger.setState("Cancelled", Color.DarkGray);
				return null;
			}
			catch(Exception ex)
			{
				string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
				addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
				logger.setState(targetKindName + " read failed.", Color.Red);
				return null;
			}
			finally
			{
				try
				{ closePort(); }
				catch { }
			}
		}
	}
}
