using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;
using static System.Environment;

namespace BK7231Flasher
{
	public class MT7687Flasher : ECRBaseFlasher, IRomReadFlasher
	{
		private bool isMT7697 = false;

		public MT7687Flasher(CancellationToken ct) : base(ct)
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
				var baud = (OSVersion.Platform == PlatformID.Unix || OSVersion.Platform == PlatformID.MacOSX) ? 115200 : 117000;
				serial = new SerialPort(serialName, baud);
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

		private bool ReadSyncPromptByte(ref int count, bool preserveTimeoutCount)
		{
			try
			{
				byte sync = (byte)serial.ReadByte();
				if(sync == 'C' || sync == 0x15)
				{
					count++;
					if(count > 2)
					{
						addLogLine("Sync success!");
						return true;
					}
				}
				else
				{
					count = 0;
				}
			}
			catch(TimeoutException)
			{
				if(!preserveTimeoutCount)
					count = 0;
			}
			return false;
		}

		private bool SyncInDownloadMode()
		{
			var orig = serial.ReadTimeout;
			var count = 0;
			serial.ReadTimeout = 10;
			Stopwatch sw = Stopwatch.StartNew();
			while(sw.ElapsedMilliseconds < 4000 && !isCancelled)
			{
				if(ReadSyncPromptByte(ref count, true))
				{
					serial.DiscardOutBuffer();
					serial.DiscardInBuffer();
					serial.ReadTimeout = orig;
					return true;
				}
				Thread.Sleep(10);
			}
			serial.ReadTimeout = orig;
			return false;
		}

		protected override bool Sync()
		{
			//Connect();
			addLogLine("Checking if chip is in download mode...");
			if(SyncInDownloadMode())
			{
				var stub = FLoaders.GetBinaryFromAssembly("MT7697_Stub");
				addLogLine("Sending stub...");
				if(xm.Send(stub) != stub.Length)
					return false;
				addLogLine("Stub uploaded!");
				Thread.Sleep(50);
			}
			else
			{
				addLogLine("Not in download mode, trying to sync in stub mode...");
			}
			serial.BaudRate = 115200;
			var flashID = ReadFlashId(true);
			if(flashID != null)
			{
				if(!CheckChipInfo(PrintChipInfo)) return false;
				return true;
			}
			addErrorLine("Sync failed!");
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
						uint offset = 5;
						while(true)
						{
							var header = new byte[9];
							Array.Copy(data, offset, header, 0, header.Length);
							uint flashOffset = BitConverter.ToUInt32(header, 1);
							uint length = BitConverter.ToUInt32(header, 5);
							if(header[0] != 0x02 && header[0] != 0x03 && header[0] != 0x04)
							{
								addWarningLine($"Unknown type {header[0]:X2} with offset {flashOffset:X} and length {length}, skipping...");
							}
							else
							{
								bool write = true;
								if(isMT7697 && header[0] == 0x03)
								{
									addLogLine("Skipping MT7687 bootloader since the chip is MT7697");
									write = false;
								}
								else if(!isMT7697 && header[0] == 0x04)
								{
									addLogLine("Skipping MT7697 bootloader since the chip is MT7687");
									write = false;
								}
								if(write)
								{
									var split = new byte[length];
									Array.Copy(data, offset + header.Length, split, 0, length);
									if(CheckHash((int)flashOffset, split.Length, split, false))
									{
										addSuccess($"Binary at 0x{flashOffset:X}-0x{flashOffset + length:X} is already flashed, skipping..." + NewLine);
									}
									else if(!InternalWrite((int)flashOffset, split))
									{
										logger.setState("Write error!", Color.Red);
										return;
									}
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
						return InternalReadRawMemory(target.Address ?? 0, target.Length ?? 0x10000, targetKindName);
					case RomReadKind.Otp:
						return InternalReadEfusePayload(target.Length ?? -1, targetKindName, true);
					case RomReadKind.Efuse:
						return InternalReadEfusePayload(target.Length ?? 0x200, targetKindName);
					default:
						addError("Selected MT7687 read target is not implemented." + Environment.NewLine);
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

		internal override byte[] ReadMAC()
		{
			var rf_efuse = ExecuteCommand(CMD_CUSTOM_READ_EFUSE, expectedReplyLen: 0x200);
			return new byte[] { rf_efuse[4], rf_efuse[5], rf_efuse[6], rf_efuse[7], rf_efuse[8], rf_efuse[9] };
		}

		private void PrintChipInfo(byte[] data)
		{
			var bond = MiscUtils.ReadU32LE(data, 4);
			isMT7697 = ((bond >> 9) & 1U) == 0;
			var bt = ((bond >> 10) & 1U) == 0;
			var w5g = ((bond >> 12) & 1U) == 0;
			addLogLine($"Chip is {(isMT7697 ? "MT7697" : "MT7687")}, {(bt ? "with" : "without")} BLE, {(w5g ? "with" : "without")} 5GHz WiFi.");
		}
	}
}

