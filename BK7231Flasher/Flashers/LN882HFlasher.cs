using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class LN882HFlasher : ECRBaseFlasher, IRomReadFlasher
	{
		string LN882H_RomVersion = "Mar 14 2021/00:23:32\r";
		string LN8825_RomVersion = "Jun 19 2019/21:01:04\r";

		public LN882HFlasher(CancellationToken ct) : base(ct)
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
				serial = new SerialPort(serialName, 117000);
				serial.ReadTimeout = 2000;
				serial.WriteTimeout = 2000;
				serial.Open();
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF)
				{
					ReceiverTimeoutMillisec = 1000,
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

		bool prepareForLoaderSend()
		{
			logger.setState("Connecting...", Color.White);
			addLogLine($"Sync with {chipType}...");
			serial.DiscardInBuffer();

			string msg = "";
			int loops = 0;
			int attempts = 0;
			int maxAttempts = 100;
			string ver = chipType == BKType.LN882H ? LN882H_RomVersion : LN8825_RomVersion;
			while(msg != ver && attempts++ < maxAttempts)
			{
				if(attempts > 1)
					addWarningLine("... failed, will retry!");
				//Thread.Sleep(1000);
				serial.DiscardInBuffer();
				serial.DiscardOutBuffer();
				loops++;
				if(loops % 10 == 0 && loops > 9)
				{
					addLogLine("Still no reply - maybe you need to pull BOOT pin down or do full power off/on before next attempt");
				}
				addLog($"Sync attempt {attempts}/{maxAttempts} ");
				try
				{
					if(isCancelled)
						return true;
					serial.Write("version\r\n");
					msg = serial.ReadLine();
					if(msg.Equals("\r"))
						msg = serial.ReadLine();
					if(msg.Equals(LN882H_RomVersion) && chipType != BKType.LN882H)
					{
						addLogLine($"... fail!");
						throw new Exception($"Selected chip type is {chipType}, but connected chip is {BKType.LN882H}");
					}
					else if(msg.Equals(LN8825_RomVersion) && chipType != BKType.LN8825)
					{
						addLogLine($"... fail!");
						throw new Exception($"Selected chip type is {chipType}, but connected chip is {BKType.LN8825}");
					}
				}
				catch(TimeoutException)
				{
					msg = "";
				}
				catch(Exception ex)
				{
					addErrorLine(ex.Message);
					return true;
				}
			}
			if(attempts >= maxAttempts)
			{
				addErrorLine($"... failed!");
				return true;
			}
			else
			{
				logger.addLog("... OK!" + Environment.NewLine, Color.Green);
			}
			return false;
		}

		protected override bool Sync()
		{
			if(isCancelled)
				return false;
			if(ReadFlashId(true) != null)
			{
				if(!CheckChipInfo()) return false;
				addLogLine("Stub is already uploaded!");
				return true;
			}
			if(prepareForLoaderSend())
			{
				return false;
			}
			byte[] dat = FLoaders.GetBinaryFromAssembly($"{chipType}_Stub");
			serial.Write($"download [rambin] [0x20000000] [{dat.Length}]\r\n");
			addLogLine("Uploading stub...");
			xm = new XMODEM(serial, XMODEM.Variants.YModem, 0xFF);
			xm.PacketSent += Xm_PacketSent;
			var sent = xm.Send(dat, 0);
			xm.PacketSent -= Xm_PacketSent;
			xm = new XMODEM(serial, XMODEM.Variants.XModem1K, 0xFF);
			addLogLine();
			if(sent < dat.Length)
			{
				addErrorLine(Environment.NewLine + "Failed to upload stub!");
				return false;
			}
			serial.BaudRate = 115200;
			Thread.Sleep(10);
			if(ReadFlashId() != null)
			{
				if(!CheckChipInfo()) return false;
				return true;
			}

			return false;
		}

		public byte[] ReadRomTarget(RomReadTarget target)
		{
			try
			{
				if(doGenericSetup() == false)
				{
					return null;
				}
				if(!Sync())
				{
					return null;
				}
				string targetKindName = RomReadCatalog.GetKindDisplayName(target.Kind);
				switch(target.Kind)
				{
					case RomReadKind.Rom:
						return InternalReadRawMemory(target.Address ?? 0, target.Length ?? 0x20000, targetKindName);
					case RomReadKind.Otp:
						return InternalReadEfusePayload(target.Length ?? 0x400, targetKindName, true);
					case RomReadKind.Efuse:
						return InternalReadEfusePayload(target.Length ?? 0x40, targetKindName);
					default:
						addError("Selected LN882x ROM reader target is not implemented." + Environment.NewLine);
						return null;
				}
			}
			catch(Exception ex)
			{
				string targetKindName = target == null ? "Selected target" : RomReadCatalog.GetKindDisplayName(target.Kind);
				addError(targetKindName + " read failed: " + ex.Message + Environment.NewLine);
				logger.setState(targetKindName + " read failed.", Color.Red);
				return null;
			}
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
					if(!InternalWrite(startSector, data)) return;
				}
				if((rwMode == WriteMode.OnlyWrite || rwMode == WriteMode.ReadAndWrite || rwMode == WriteMode.OnlyOBKConfig) && cfg != null && !isCancelled)
				{
					if(cfg != null)
					{
						var offset = OBKFlashLayout.getConfigLocation(chipType, out sectors);
						var areaSize = sectors * BK7231Flasher.SECTOR_SIZE;

						cfg.saveConfig(chipType);
						var cfgData = MiscUtils.padArray(cfg.getData(), BK7231Flasher.SECTOR_SIZE);
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

		protected override bool CheckHash(int addr, int len, byte[] data) => base.CheckCRC(addr, len, data);
	}
}

