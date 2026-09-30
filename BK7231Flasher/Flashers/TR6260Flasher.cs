using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace BK7231Flasher
{
	public class TR6260Flasher : ECRBaseFlasher, IRomReadFlasher
	{
		const int FLASH_BLOCK = 1024;
		const int DEFAULT_FLASH_SIZE = 0x100000;
		const uint TRS_SYNC = 0x73796E63;

		const byte TRS_ROM_SYNC_ACK = 1;
		const byte TRS_ROM_FILE_ACK = 0;

		const uint TRS_FRM_TYPE_UBOOT = 1;
		const int PARTITION_ADDR = 0x3000;
		const int APP_ADDR = 0x4000;

		bool sessionPortUnavailable;
		bool sessionClosedPortWriteLogged;

		public TR6260Flasher(CancellationToken ct) : base(ct)
		{
		}

		void SetState(string text, Color color)
		{
			logger?.setState(text, color);
		}

		void SetBusyState(string text)
		{
			SetState(text, Color.Transparent);
		}

		void SetErrorState(string text)
		{
			SetState(text, Color.Red);
		}

		bool IsPortUnavailable()
		{
			try
			{
				return serial == null || !serial.IsOpen;
			}
			catch
			{
				return true;
			}
		}

		bool IsPortUnavailableException(Exception ex)
		{
			if(ex is ObjectDisposedException || ex is InvalidOperationException || ex is NullReferenceException)
				return true;

			string msg = ex?.Message ?? string.Empty;
			return msg.IndexOf("port is closed", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("instance of an object", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		void MarkPortUnavailable()
		{
			sessionPortUnavailable = true;
		}

		void LogPortClosedWriteFailureOnce()
		{
			if(sessionClosedPortWriteLogged)
				return;

			addErrorLine("Serial write failed: The port is closed.");
			sessionClosedPortWriteLogged = true;
		}

		byte? ReadResponseByte(int attempts = 1, int delayMs = 50)
		{
			for(int i = 0; i < attempts; i++)
			{
				try
				{
					int v = serial.ReadByte();
					if(v >= 0)
						return (byte)v;
				}
				catch(TimeoutException)
				{
				}
				catch(Exception ex)
				{
					if(IsPortUnavailableException(ex) || IsPortUnavailable())
					{
						MarkPortUnavailable();
						return null;
					}
				}

				if(sessionPortUnavailable || cancellationToken.IsCancellationRequested)
					return null;

				if(delayMs > 0)
					Thread.Sleep(delayMs);
			}
			return null;
		}

		bool WriteRaw(byte[] data)
		{
			if(data == null || data.Length == 0)
				return true;

			if(IsPortUnavailable())
			{
				MarkPortUnavailable();
				LogPortClosedWriteFailureOnce();
				return false;
			}

			try
			{
				serial.Write(data, 0, data.Length);
				return true;
			}
			catch(Exception ex)
			{
				if(IsPortUnavailableException(ex) || IsPortUnavailable())
				{
					MarkPortUnavailable();
					LogPortClosedWriteFailureOnce();
					return false;
				}

				addErrorLine("Serial write failed: " + ex.Message);
				return false;
			}
		}

		byte SyncOnce()
		{
			if(!WriteRaw(BitConverter.GetBytes(TRS_SYNC)))
				return 0;
			Thread.Sleep(50);
			return ReadResponseByte(1, 0) ?? (byte)0;
		}

		bool LoadBootloaderToRam()
		{
			byte[] boot = FLoaders.GetBinaryFromAssembly("TR6260_Stub");
			SetBusyState("Uploading stub...");
			addLogLine("Uploading stub...");
			if(!BeginTransfer(TRS_FRM_TYPE_UBOOT, 0, boot.Length, 30))
			{
				addErrorLine("Stub header failed");
				SetErrorState("Stub upload failed");
				return false;
			}

			if(!WriteFileBlocks(boot, "stub"))
				return false;

			addLogLine("Stub upload completed.");
			return true;
		}

		bool BeginTransfer(uint fileType, int address, int length, int responsePolls = 30)
		{
			byte[] header = new byte[12];
			Array.Copy(BitConverter.GetBytes(fileType), 0, header, 0, 4);
			Array.Copy(BitConverter.GetBytes(address), 0, header, 4, 4);
			Array.Copy(BitConverter.GetBytes(length), 0, header, 8, 4);

			if(!WriteRaw(header))
				return false;

			return ReadResponseByte(responsePolls, 50).HasValue;
		}

		bool WriteFileBlocks(byte[] data, string description, int baseAddress = -1)
		{
			int done = 0;
			int totalBlocks = (data.Length + FLASH_BLOCK - 1) / FLASH_BLOCK;
			int blockIndex = 0;
			int nextPrintOffset = baseAddress >= 0 ? baseAddress : -1;
			while(done < data.Length)
			{
				if(cancellationToken.IsCancellationRequested)
					return false;
				if(baseAddress >= 0)
				{
					int currentOffset = baseAddress + done;
					if(currentOffset >= nextPrintOffset)
					{
						addLog(formatHex(currentOffset) + "... ");
						nextPrintOffset = currentOffset + BK7231Flasher.SECTOR_SIZE;
					}
				}

				int take = Math.Min(FLASH_BLOCK, data.Length - done);
				byte[] block = new byte[take];
				Buffer.BlockCopy(data, done, block, 0, take);
				if(!WriteRaw(block))
					return false;

				byte? ack = ReadResponseByte(30, 50);
				if(ack != TRS_ROM_FILE_ACK)
				{
					addLog(Environment.NewLine);
					addErrorLine($"{description} failed at block {blockIndex + 1}/{Math.Max(totalBlocks, 1)}, response {(ack.HasValue ? ack.Value.ToString() : "<timeout>")}");
					SetErrorState("Transfer failed");
					return false;
				}

				done += take;
				blockIndex++;
				logger.setProgress(done, data.Length);
			}

			if(baseAddress >= 0)
				addLog(Environment.NewLine);

			return true;
		}

		public override void closePort()
		{
			try
			{
				if(serial != null)
				{
					try
					{
						if(serial.IsOpen)
							serial.Close();
					}
					catch
					{
					}

					try
					{
						serial.Dispose();
					}
					catch
					{
					}
				}
			}
			finally
			{
				serial = null;
			}
		}

		bool LooksLikeWholeFlashImage(int address, byte[] data)
		{
			if(address != 0 || data == null)
				return false;

			if(data.Length == DEFAULT_FLASH_SIZE)
				return true;

			return data.Length >= (DEFAULT_FLASH_SIZE - APP_ADDR);
		}
		
		static uint ParseAsciiDecimal(byte[] ascii, int len, int offset = 0)
		{
			uint val = 0;
			for(int i = offset; i < len + offset; i++)
			{
				byte c = ascii[i];
				if(c >= '0' && c <= '9') val = (uint)(val * 10 + (c - '0'));
				else return 0;
			}
			return val;
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
					if(LooksLikeWholeFlashImage(startSector, data))
					{
						if(!InternalWrite(0, data)) return;
					}
					else if(startSector == 0)
					{
						if((ParseAsciiDecimal(data, 8) + ParseAsciiDecimal(data, 8, 8)) == data.Length - 16)
						{
							byte[] boot = FLoaders.GetRawBinaryFromAssembly("TR6260_Boot");
							byte[] partition = FLoaders.GetRawBinaryFromAssembly("TR6260_Partition");
							if(!InternalWrite(0, boot)) return;
							if(!InternalWrite(PARTITION_ADDR, partition)) return;
							if(!InternalWrite(APP_ADDR, data)) return;
						}
						else
						{
							if(!InternalWrite(startSector, data)) return;
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
						cfg.saveConfig(chipType);
						var cfgData = cfg.getData();

						addLog("Now will also write OBK config..." + Environment.NewLine);
						addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
						addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
						addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);

						var offset = OBKFlashLayout.getConfigLocation(chipType, out var efsectors);
						var areaSize = efsectors * BK7231Flasher.SECTOR_SIZE;
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
						ms?.Dispose();
						ms = new MemoryStream(efdata);
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

		protected override bool doGenericSetup()
		{
			addLog("Now is: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "." + Environment.NewLine);
			addLog("Flasher mode: " + chipType + Environment.NewLine);
			addLog("Going to open port: " + serialName + "." + Environment.NewLine);
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				serial = new SerialPort(serialName, 115200)
				{
					ReadBufferSize = 65536,
					ReadTimeout = 1000
				};
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
				{
					MaxSenderRetries = 10,
					ReceiverMaxConsecutiveRetries = 10
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
			if(isCancelled)
				return false;
			SetBusyState("Syncing bootrom...");
			addLogLine("Trying to sync bootrom...");
			serial.BaudRate = 57600;
			byte syncResp = SyncOnce();
			if(syncResp == TRS_ROM_SYNC_ACK)
			{
				addLogLine("Sync OK (UART/bootrom mode)");
				if(!LoadBootloaderToRam())
					return false;
				serial.BaudRate = 115200;
				Thread.Sleep(50);
				var flashID = ReadFlashId();
				if(flashID != null)
				{
					if(!CheckChipInfo())
						return false;
					return true;
				}
			}
			else
			{
				addLogLine("Failed, checking if stub is already uploaded...");
				serial.BaudRate = 115200;
				var flashID = ReadFlashId(true);
				if(flashID != null)
				{
					if(!CheckChipInfo())
						return false;
					addLogLine("Stub is already uploaded!");
					return true;
				}
			}
			return false;
		}
		public byte[] ReadRomTarget(RomReadTarget target)
		{
			try
			{
				if(target == null)
				{
					addError("No ROM reader target selected." + Environment.NewLine);
					return null;
				}
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
						return InternalReadRawMemory(target.Address ?? 0, target.Length ?? 0x8000, targetKindName);
					case RomReadKind.Otp:
						return InternalReadEfusePayload(target.Length ?? -1, targetKindName, true);
					case RomReadKind.Efuse:
						return InternalReadEfusePayload(target.Length ?? 0x20, targetKindName);
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
				try
				{ closePort(); }
				catch { }
			}
		}

		protected override bool CheckHash(int addr, int len, byte[] data) => base.CheckCRC(addr, len, data);

		internal override byte[] ReadMAC() => null;
	}
}
