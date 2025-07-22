using BK7231Flasher;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace LN882HTool
{
	public class LN882HFlasher
	{
		private SerialPort _port;

		public LN882HFlasher(string portName, int baudRate, int timeoutMs = 10000)
		{
			try
			{
				Console.WriteLine("Opening port " + portName + "...");
				_port = new SerialPort(portName, baudRate);
				_port.ReadTimeout = timeoutMs;
				_port.WriteTimeout = timeoutMs;
				_port.Open();
				_port.DiscardInBuffer();
				_port.DiscardOutBuffer();
				Console.WriteLine("Port " + portName + " open!");
			}
			catch (Exception)
			{
				Console.WriteLine("Error: Open {0}, {1} baud!", portName, baudRate);
				Environment.Exit(-1);
			}
		}

		public bool upload_ram_loader_for_read(byte[] RAMCODE)
		{
			Console.WriteLine("Sync with LN882H");
			_port.DiscardInBuffer();

			string msg = "";
			while(msg != "Mar 14 2021/00:23:32\r")
			{
				Thread.Sleep(1000);
				flush_com();
				Console.WriteLine("send version... wait for:  Mar 14 2021/00:23:32");
				_port.Write("version\r\n");
				try
				{
					msg = _port.ReadLine();
					Console.WriteLine(msg);
				}
				catch(TimeoutException)
				{
					msg = "";
				}
			}

			Console.WriteLine("Connect to bootloader...");
			_port.Write($"download [rambin] [0x20000000] [{RAMCODE.Length}]\r\n");
			Console.WriteLine("Will send RAMCODE");
			YModem modem = new YModem(_port, FormMain.Singleton);

			var stream = new MemoryStream(RAMCODE);
			int ret = modem.send(stream, "RAMCODE", stream.Length, false);
            if(ret != stream.Length)
            {
                Console.WriteLine("Ramcode upload failed, expected " + stream.Length +", got " + ret+"!");
                return false;
            }
			Console.WriteLine("Starting immediately");
			return true;
		}

		public bool upload_ram_loader(string fname)
		{
			Console.WriteLine("upload_ram_loader will upload " + fname + "!");
			if (File.Exists(fname) == false)
			{
				Console.WriteLine("Can't open " + fname + "!");
				return true;
			}

			Console.WriteLine("Sync with LN882H... wait 5 seconds");
			_port.DiscardInBuffer();
			Thread.Sleep(5000);

			string msg = "";
			while (msg != "Mar 14 2021/00:23:32\r")
			{
				Thread.Sleep(2000);
				flush_com();
				Console.WriteLine("send version... wait for:  Mar 14 2021/00:23:32");
				_port.Write("version\r\n");
				try
				{
					msg = _port.ReadLine();
					Console.WriteLine(msg);
				}
				catch (TimeoutException)
				{
					msg = "";
				}
			}

			Console.WriteLine("Connect to bootloader...");
			_port.Write("download [rambin] [0x20000000] [37872]\r\n");
			Console.WriteLine("Will send file via YModem2");

			YModem2 modem = new YModem2(_port);
			modem.send_file(fname, false, 3);

			Console.WriteLine("Start program. Wait 5 seconds");
			Thread.Sleep(5000);

			msg = "";
			while (msg != "RAMCODE\r")
			{
			    Thread.Sleep(5000);
			    _port.DiscardInBuffer();
			    Console.WriteLine("send version... wait for:  RAMCODE");
			    _port.Write("version\r\n");
			    try
			    {
			        msg = _port.ReadLine();
			        Console.WriteLine(msg);
			        msg = _port.ReadLine();
			        Console.WriteLine(msg);
			    }
			    catch (TimeoutException)
			    {
			        msg = "";
			    }
			}
			
			_port.Write("flash_uid\r\n");
			try
			{
			    msg = _port.ReadLine();
			    msg = _port.ReadLine();
			    Console.WriteLine(msg.Trim());
			}
			catch (TimeoutException)
			{
			    Console.WriteLine("Timeout on flash_uid");
			}

			return true;
		}

		internal void runTerminal()
		{
			while (true)
			{
				Console.Write("Give command: ");
				string cmd = Console.ReadLine();
				if (string.IsNullOrEmpty(cmd)) continue;

				_port.Write(cmd + "\r\n");

				//   ReadUartWithTimeoutAndLineLimit(_port, maxLines: 2, timeoutMs: 1000);
				// fdump does not respect it?
				 ReadUartWithTimeoutAndLineLimit(_port, maxLines: 9999, timeoutMs: 1000);
			}
		}

		void ReadUartWithTimeoutAndLineLimit(SerialPort port, int maxLines, int timeoutMs)
		{
			int linesRead = 0;
			StringBuilder currentLine = new StringBuilder();
			DateTime lastReceived = DateTime.Now;

			while (true)
			{
				if (port.BytesToRead > 0)
				{
					char ch = (char)port.ReadByte();
					Console.Write(ch);
					currentLine.Append(ch);
					lastReceived = DateTime.Now;

					if (currentLine.ToString().EndsWith("\r\n"))
					{
						linesRead++;
						//currentLine.Clear();
						if (linesRead >= maxLines)
							break;
					}
				}
				else
				{
					if ((DateTime.Now - lastReceived).TotalMilliseconds > timeoutMs)
					{
						Console.WriteLine("\n[Timeout waiting for device reply]");
						break;
					}

					Thread.Sleep(10);
				}
			}
		}


		public void flush_com()
		{
			_port.DiscardInBuffer();
			_port.DiscardOutBuffer();
		}

		public void close()
		{
			_port.Close();
		}

		public void change_baudrate(int baudrate)
		{
			Console.WriteLine("change_baudrate: Change baudrate " + baudrate);
			_port.Write("baudrate " + baudrate + "\r\n");
			_port.ReadExisting();
			_port.BaudRate = baudrate;
			Console.WriteLine("change_baudrate: Wait 5 seconds for change");
			Thread.Sleep(5000);
			flush_com();

			string msg = "";
			while (!msg.Contains("RAMCODE"))
			{
				Console.WriteLine("change_baudrate: send version... wait for:  RAMCODE");
				Thread.Sleep(1000);
				flush_com();
				_port.Write("version\r\n");
				try
				{
					msg = _port.ReadLine();
					Console.WriteLine(msg);
					msg = _port.ReadLine();
					Console.WriteLine(msg);
				}
				catch (TimeoutException) { msg = ""; }
			}

			Console.WriteLine("change_baudrate: Baudrate change done");
		}

		public void flash_program(string filename)
		{
			Console.WriteLine("flash_program: will flash " + filename);
			change_baudrate(230400);
			Console.WriteLine("flash_program: sending startaddr");
			_port.Write("startaddr 0x0\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());

			Console.WriteLine("flash_program: sending update command");
			_port.Write("upgrade\r\n");
			_port.Read(new byte[7], 0, 7);

			Console.WriteLine("flash_program: sending file via ymodem");
			YModem2 modem = new YModem2(_port);
			modem.send_file(filename, true, 3);
			Console.WriteLine("flash_program: sending file done");

			_port.Write("filecount\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());

			change_baudrate(115200);
		}

		public void flash_erase_all()
		{
			_port.Write("ferase_all\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void flash_info()
		{
			_port.Write("flash_info\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void get_mac_in_otp()
		{
			_port.Write("get_mac_in_flash_otp\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void get_mac_local()
		{
			_port.Write("get_m_local_mac\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void read_gpio(string pin)
		{
			_port.Write("gpio_read " + pin + "\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void write_gpio(string pin, string val)
		{
			_port.Write("gpio_write " + pin + " " + val + "\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}
		string readBytesSafe(int targetLen)
		{
			string s = "";
			for (int tr = 0; tr < 100; tr++)
			{
				while (_port.BytesToRead > 0)
				{
					char c = (char)_port.ReadChar();
					if (c != ' ')
					{
						s += c;
						if (s.Length == targetLen)
						{
							return s.Trim();
						}
					}
				}
				Thread.Sleep(1);
			}
			Console.WriteLine("readBytesSafe Failed");
			return s.Trim();
		}

		public bool read_flash(int flash_addr, bool is_otp, out byte[] flash_data)
		{
			//Console.WriteLine("read_flash[" + flash_addr + "] entered");
			string cmd = is_otp ? $"flash_otp_read 0x{flash_addr:X} 0x100\r\n" : $"flash_read 0x{flash_addr:X} 0x100\r\n";
			_port.Write(cmd);

			string rep = _port.ReadLine(); // echo
			// string dataLine = _port.ReadLine().Trim();
			string dataLine = readBytesSafe(256 * 2 + 4);
			// Console.WriteLine("read_flash[" + flash_addr + "] got " + dataLine);
			string hexData = dataLine.Replace(" ", "");
			flash_data = new byte[0];

			if (hexData.Length != 256 * 2 + 4)
				return false;

			string hexPayload = hexData.Substring(0, hexData.Length - 4);
			string checksum = hexData.Substring(hexData.Length - 4);
			flash_data = HexStringToBytes(hexPayload);

			ushort calc_crc = YModem2.calc_crc(flash_data);
			return calc_crc == Convert.ToUInt16(checksum, 16);
		}

		public bool read_flash_to_file(string filename, int baudRate)
		{
            byte[] RAMCODE = LN882H_RamDumper.RAMCODE;
			switch(baudRate)
			{
				default:
                    Console.WriteLine("Not supported baud - defaulting to 115200");
					baudRate = 115200; goto case 115200;
				case 115200:
                    Console.WriteLine("Setting up baud 115200");
                    RAMCODE[8] = 0x6F;
					RAMCODE[9] = 0xD8;
					RAMCODE[10] = 0xCB;
					RAMCODE[11] = 0x1A;
					RAMCODE[20] = 0xC5;
					RAMCODE[21] = 0xDA;
					RAMCODE[22] = 0x90;
					RAMCODE[23] = 0x00;
					RAMCODE[8226] = 0xE1;
					RAMCODE[8227] = 0x30;
					break;
				case 230400:
                    Console.WriteLine("Setting up baud 230400");
                    RAMCODE[8] = 0xCF;
					RAMCODE[9] = 0x53;
					RAMCODE[10] = 0x27;
					RAMCODE[11] = 0xAF;
					RAMCODE[20] = 0x2B;
					RAMCODE[21] = 0xB4;
					RAMCODE[22] = 0x8B;
					RAMCODE[23] = 0x03;
					RAMCODE[8226] = 0x61;
					RAMCODE[8227] = 0x30;
					break;
				case 460800:
                    Console.WriteLine("Setting up baud 460800");
                    RAMCODE[8] = 0xB0;
					RAMCODE[9] = 0xE9;
					RAMCODE[10] = 0x95;
					RAMCODE[11] = 0xCB;
					RAMCODE[20] = 0x05;
					RAMCODE[21] = 0x07;
					RAMCODE[22] = 0xFD;
					RAMCODE[23] = 0x63;
					RAMCODE[8226] = 0xE1;
					RAMCODE[8227] = 0x20;
					break;
				case 921600:
                    Console.WriteLine("Setting up baud 921600");
                    RAMCODE[8] = 0x10;
					RAMCODE[9] = 0x62;
					RAMCODE[10] = 0x79;
					RAMCODE[11] = 0x7E;
					RAMCODE[20] = 0xEB;
					RAMCODE[21] = 0x69;
					RAMCODE[22] = 0xE6;
					RAMCODE[23] = 0x60;
					RAMCODE[8226] = 0x61;
					RAMCODE[8227] = 0x20;
					break;
			}
			var isUploaded = upload_ram_loader_for_read(RAMCODE);
            if (!isUploaded)
            {
                Console.WriteLine("read_flash_to_file: failed to upload RAMCODE");
                return false;
            }
			_port.BaudRate = baudRate;
			byte[] flashsize = new byte[4];
			var addr = 0;
			var read = 0;
			var result = true;
			while(read < 4)
			{
				read += _port.Read(flashsize, read, 4 - read);
			}
			int total_flash_size = flashsize[3] << 24 | flashsize[2] << 16 | flashsize[1] << 8 | flashsize[0];
            int mbs = total_flash_size / 0x100000;
            Console.WriteLine($"Reading flash to file {filename}, flash size is {mbs} MB");
			int packetsize = 512 + 2;
			var t = Stopwatch.StartNew();
			using(FileStream fs = new FileStream(filename, FileMode.Create))
			{
				while(addr < total_flash_size)
				{
					byte[] buf = new byte[packetsize];
					byte[] nocrc = new byte[packetsize - 2];
					byte[] crc = new byte[2];
					read = 0;
					while(read < packetsize)
					{
						read += _port.Read(buf, read, packetsize - read);
					}
					Array.Copy(buf, nocrc, packetsize - 2);
					Array.ConstrainedCopy(buf, packetsize - 2, crc, 0, 2);
					ushort calc_crc = YModem2.calc_crc(nocrc);
					ushort sentcrc = (ushort)(crc[1] << 8 | crc[0]);
					if(sentcrc != calc_crc)
					{
						Console.WriteLine($"CRC FAIL at {addr}");
						result = false;
						break;
					}
					addr += packetsize - 2;
					fs.Write(buf, 0, packetsize - 2);

				}
			}
			t.Stop();
			Console.WriteLine($"\ndone in {t.Elapsed.TotalSeconds}s");
			return result;
		}

		public void dump_flash()
		{
			_port.Write("fdump 0x0 0x2000\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			while (true)
			{
				string msg = _port.ReadLine().Trim();
				if (msg == "pppp") break;
				Console.WriteLine(msg);
			}
		}

		public void get_gpio_all()
		{
			_port.Write("gpio_read_al\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		public void get_otp_lock()
		{
			_port.Write("flash_otp_get_lock_state\r\n");
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
			Console.WriteLine(_port.ReadLine().Trim());
		}

		private byte[] HexStringToBytes(string hex)
		{
			int len = hex.Length;
			byte[] bytes = new byte[len / 2];
			for (int i = 0; i < len; i += 2)
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			return bytes;
		}
	}
}
