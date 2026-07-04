using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace BK7231Flasher
{
	public class RDAFlasher : ECRBaseFlasher
	{
		public RDAFlasher(CancellationToken ct) : base(ct)
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
				serial.BaudRate = 921600;
				int attempts = 0;
				int addr = 0x10007d;
				while(attempts++ < 500)
				{
					addLog($"Sync attempt {attempts}/500 ");
					int tries = 50;
					serial.ReadTimeout = 50;
					serial.DiscardInBuffer();
					while(tries-- > 0)
					{
						if(isCancelled)
							return false;
						serial.Write("\r\n");
						var t = string.Empty;
						try
						{
							t = serial.ReadLine();
						}
						catch { }
						if(t.Contains("Boot >"))
						{
							logger.addLog("... OK!" + Environment.NewLine, Color.Green);
							var ramcode = FLoaders.GetRawBinaryFromAssembly("RDA5981_Stub");
							serial.Write($"loadb 00100000 {ramcode.Length:X}\r");
							Thread.Sleep(1);
							var resp = serial.ReadExisting();
							if(!resp.Contains("Download")) continue;
							serial.Write(ramcode, 0, ramcode.Length);
							Thread.Sleep(1);
							resp = serial.ReadExisting();
							if(!resp.Contains("Done")) continue;
							serial.Write($"checksum 1 00100000 {ramcode.Length}\r");
							Thread.Sleep(20);
							var calc = CRC.crc32_ver2(0xFFFFFFFF, ramcode);
							resp = serial.ReadExisting();
							resp = resp.Substring(resp.IndexOf("CRC32") + 8, 10);
							var crc = Convert.ToUInt32(resp, 16);
							if(crc != calc) continue;
							serial.Write($"\rgo 2 {addr:X}\r");
							Thread.Sleep(20);
							serial.BaudRate = 115200;
							serial.DiscardInBuffer();
							if(ReadFlashId(true) != null)
								return true;
							else
							{
								addErrorLine("Stub sync failed!");
								serial.BaudRate = 921600;
							}
						}
					}
					addWarningLine("... failed, will retry!");
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
					var startAddr = 0x1000;
					if(data[0] == 0x02 && data[4] == 'A' && data[5] == 'D' && data[6] == 'R')
					{
						startAddr = 0x0;
					}
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
						addLogLine("Writing config first");
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
			var calc = CRC.crc32_ver2(0xFFFFFFFF, data);
			if(crc != calc)
			{
				logger.setState("CRC mismatch!", Color.Red);
				addErrorLine("CRC mismatch!");
				addErrorLine($"Sent by RDA {formatHex(crc)}, our CRC {formatHex(calc)}");
				return false;
			}
			addSuccess($"CRC matches {formatHex(calc)}!" + Environment.NewLine);
			return true;
		}
	}
}
