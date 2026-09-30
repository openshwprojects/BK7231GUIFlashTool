using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class ECR6600Flasher : ECRBaseFlasher, IRomReadFlasher
	{
		const int EcrRomBase = 0x00000000;
		const int EcrRomSize = 0x00010000;
		const int EcrEfuseSize = 0x80;

		static readonly byte CMD_RAM_DOWNLOAD = 0x01;
		//static readonly byte CMD_FLASH_DOWNLOAD = 0x02;
		//static readonly byte CMD_FLASH_UPLOAD = 0x03;
		//static readonly byte CMD_RUN = 0x06;
		//static readonly byte CMD_EFUSE = 0x08;

		public ECR6600Flasher(CancellationToken ct) : base(ct)
		{
		}

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				serial = new SerialPort(serialName, 115200);
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				serial.ReadTimeout = 10000;
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

		private bool Connect()
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

		protected override bool Sync()
		{
			var flashID = ReadFlashId(true);
			if(flashID != null)
			{
				if(!CheckChipInfo()) return false;
				addLogLine("Stub is already uploaded!");
				return true;
			}
			Connect();
			int attempts = 0;
			while(attempts++ < 500)
			{
				addLog($"Sync attempt {attempts}/500 ");
				int tries = 50;
				serial.ReadTimeout = 50;
				serial.DiscardInBuffer();
				while(tries-- > 0)
				{
					if(isCancelled) return false;
					serial.Write("cnys");
					var t = 0;
					try
					{
						t = serial.ReadByte();
					}
					catch { }
					if(t == 0x01 || t == 0xFF)
					{
						logger.addLog("... OK!" + Environment.NewLine, Color.Green);
						return UploadStub();
					}
				}
				addWarningLine("... failed, will retry!");
			}
			return false;
		}

		private bool UploadStub()
		{
			var stub = FLoaders.GetBinaryFromAssembly("ECR6600_Stub");
			var startupAddress = 0x10000; // works even if 0
			var empty = new byte[8];
			var dat = new List<byte>()
			{
				CMD_RAM_DOWNLOAD, 0x00, 0x00, 0x00,
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
			var flashID = ReadFlashId();
			if(tries == 0 || flashID == null) return false;
			if(!CheckChipInfo()) return false;
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
					byte[] res = InternalRead(startSector, sectors);
					if(res != null)
						ms = new MemoryStream(res);
					else
						return;
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
								addWarningLine($"Unknown type {header[0]:X2} with offset {flashOffset:X} and length {length}, skipping...");
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
						if(!InternalWrite(startSector, data)) return;
					}
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null && !isCancelled)
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
						return InternalReadRawMemory(target.Address ?? EcrRomBase, target.Length ?? EcrRomSize, targetKindName);
					case RomReadKind.Otp:
						return InternalReadEfusePayload(target.Length ?? -1, targetKindName, true);
					case RomReadKind.Efuse:
						return InternalReadEfusePayload(target.Length ?? EcrEfuseSize, targetKindName);
					default:
						addError("Selected ECR6600 read target is not implemented." + Environment.NewLine);
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
				try { closePort(); } catch { }
			}
		}

		internal override byte[] ReadMAC() => null;
	}
}

