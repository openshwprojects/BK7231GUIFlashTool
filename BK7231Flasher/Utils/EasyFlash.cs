using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BK7231Flasher
{
	static class EasyFlash
	{
		static class EF32
		{
			const string Dll = "easyflash/WinEF_x86.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF64
		{
			const string Dll = "easyflash/WinEF_x64.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF32_GRAN8
		{
			const string Dll = "easyflash/WinEF_GRAN8_x86.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF64_GRAN8
		{
			const string Dll = "easyflash/WinEF_GRAN8_x64.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF32_GRAN32
		{
			const string Dll = "easyflash/WinEF_GRAN32_x86.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF64_GRAN32
		{
			const string Dll = "easyflash/WinEF_GRAN32_x64.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF32_ECR
		{
			const string Dll = "easyflash/WinEF_ECR_x86.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EF64_ECR
		{
			const string Dll = "easyflash/WinEF_ECR_x64.dll";

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint easyflash_init();

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
			public static extern unsafe byte* get_env_area();
		}

		static class EFLinux
		{
			const string Dll = "easyflash/libef.so";

			[DllImport(Dll)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll)]
			public static extern uint easyflash_init();

			[DllImport(Dll)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll)]
			public static extern unsafe byte* get_env_area();
		}

		static class EFLinux_GRAN8
		{
			const string Dll = "easyflash/libef_GRAN8.so";

			[DllImport(Dll)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll)]
			public static extern uint easyflash_init();

			[DllImport(Dll)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll)]
			public static extern unsafe byte* get_env_area();
		}

		static class EFLinux_GRAN32
		{
			const string Dll = "easyflash/libef_GRAN32.so";

			[DllImport(Dll)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll)]
			public static extern uint easyflash_init();

			[DllImport(Dll)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll)]
			public static extern unsafe byte* get_env_area();
		}

		static class EFLinux_ECR
		{
			const string Dll = "easyflash/libef_ECR.so";

			[DllImport(Dll)]
			public static extern unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len);

			[DllImport(Dll)]
			public static extern unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len);

			[DllImport(Dll)]
			public static extern uint easyflash_init();

			[DllImport(Dll)]
			public static extern uint set_env_size(uint size);

			[DllImport(Dll)]
			public static extern unsafe byte* get_env_area();
		}

		[DllImport("msvcrt.dll", SetLastError = false, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);

		[DllImport("msvcrt.dll", SetLastError = false, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr memset(IntPtr dest, int c, int count);

		static bool HasLinuxGran32()
		{
			try
			{
				return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "easyflash", "libef_GRAN32.so"));
			}
			catch
			{
				return false;
			}
		}

		private static unsafe bool SetupBase(byte[] data, int size, BKType type, out byte* env)
		{
			try
			{
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					switch(type)
					{
						case BKType.BL602:
							EFLinux_GRAN8.set_env_size((uint)size);
							break;
						case BKType.TR6260:
							if(HasLinuxGran32())
							{
								EFLinux_GRAN32.set_env_size((uint)size);
							}
							else
							{
								EFLinux.set_env_size((uint)size);
							}
							break;
						case BKType.ECR6600:
							EFLinux_ECR.set_env_size((uint)size);
							break;
						default:
							EFLinux.set_env_size((uint)size);
							break;
					}
				}
				else if(IntPtr.Size == 8)
				{
					switch(type)
					{
						case BKType.BL602:
							EF64_GRAN8.set_env_size((uint)size);
							break;
						case BKType.TR6260:
							EF64_GRAN32.set_env_size((uint)size);
							break;
						case BKType.ECR6600:
							EF64_ECR.set_env_size((uint)size);
							break;
						default:
							EF64.set_env_size((uint)size);
							break;
					}
				}
				else
				{
					switch(type)
					{
						case BKType.BL602:
							EF32_GRAN8.set_env_size((uint)size);
							break;
						case BKType.TR6260:
							EF32_GRAN32.set_env_size((uint)size);
							break;
						case BKType.ECR6600:
							EF32_ECR.set_env_size((uint)size);
							break;
						default:
							EF32.set_env_size((uint)size);
							break;
					}
				}

				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					switch(type)
					{
						case BKType.BL602:
							env = EFLinux_GRAN8.get_env_area();
							break;
						case BKType.TR6260:
							env = HasLinuxGran32() ? EFLinux_GRAN32.get_env_area() : EFLinux.get_env_area();
							break;
						case BKType.ECR6600:
							env = EFLinux_ECR.get_env_area();
							break;
						default:
							env = EFLinux.get_env_area();
							break;
					}
				}
				else if(IntPtr.Size == 8)
				{
					switch(type)
					{
						case BKType.BL602:
							env = EF64_GRAN8.get_env_area();
							break;
						case BKType.TR6260:
							env = EF64_GRAN32.get_env_area();
							break;
						case BKType.ECR6600:
							env = EF64_ECR.get_env_area();
							break;
						default:
							env = EF64.get_env_area();
							break;
					}
				}
				else
				{
					switch(type)
					{
						case BKType.BL602:
							env = EF32_GRAN8.get_env_area();
							break;
						case BKType.TR6260:
							env = EF32_GRAN32.get_env_area();
							break;
						case BKType.ECR6600:
							env = EF32_ECR.get_env_area();
							break;
						default:
							env = EF32.get_env_area();
							break;
					}
				}

				fixed(byte* pdata = data)
					memcpy((IntPtr)env, (IntPtr)pdata, size);

				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					switch(type)
					{
						case BKType.BL602:
							EFLinux_GRAN8.easyflash_init();
							break;
						case BKType.TR6260:
							if(HasLinuxGran32())
							{
								EFLinux_GRAN32.easyflash_init();
							}
							else
							{
								EFLinux.easyflash_init();
							}
							break;
						case BKType.ECR6600:
							EFLinux_ECR.easyflash_init();
							break;
						default:
							EFLinux.easyflash_init();
							break;
					}
				}
				else if(IntPtr.Size == 8)
				{
					switch(type)
					{
						case BKType.BL602:
							EF64_GRAN8.easyflash_init();
							break;
						case BKType.TR6260:
							EF64_GRAN32.easyflash_init();
							break;
						case BKType.ECR6600:
							EF64_ECR.easyflash_init();
							break;
						default:
							EF64.easyflash_init();
							break;
					}
				}
				else
				{
					switch(type)
					{
						case BKType.BL602:
							EF32_GRAN8.easyflash_init();
							break;
						case BKType.TR6260:
							EF32_GRAN32.easyflash_init();
							break;
						case BKType.ECR6600:
							EF32_ECR.easyflash_init();
							break;
						default:
							EF32.easyflash_init();
							break;
					}
				}
			}
			catch
			{
				env = null;
				return false;
			}
			return true;
		}

		public static unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len, BKType type)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				switch(type)
				{
					case BKType.BL602:
						return EFLinux_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.TR6260:
						return HasLinuxGran32() ? EFLinux_GRAN32.ef_get_env_blob(key, value_buf, buf_len, saved_value_len) : EFLinux.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.ECR6600:
						return EFLinux_ECR.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					default:
						return EFLinux.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				}
			}
			else if(IntPtr.Size == 8)
			{
				switch(type)
				{
					case BKType.BL602:
						return EF64_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.TR6260:
						return EF64_GRAN32.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.ECR6600:
						return EF64_ECR.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					default:
						return EF64.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				}
			}
			else
			{
				switch(type)
				{
					case BKType.BL602:
						return EF32_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.TR6260:
						return EF32_GRAN32.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					case BKType.ECR6600:
						return EF32_ECR.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
					default:
						return EF32.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				}
			}
		}

		public static unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len, BKType type)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				switch(type)
				{
					case BKType.BL602:
						return EFLinux_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.TR6260:
						return HasLinuxGran32() ? EFLinux_GRAN32.ef_set_env_blob(key, value_buf, buf_len) : EFLinux.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.ECR6600:
						return EFLinux_ECR.ef_set_env_blob(key, value_buf, buf_len);
					default:
						return EFLinux.ef_set_env_blob(key, value_buf, buf_len);
				}
			}
			else if(IntPtr.Size == 8)
			{
				switch(type)
				{
					case BKType.BL602:
						return EF64_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.TR6260:
						return EF64_GRAN32.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.ECR6600:
						return EF64_ECR.ef_set_env_blob(key, value_buf, buf_len);
					default:
						return EF64.ef_set_env_blob(key, value_buf, buf_len);
				}
			}
			else
			{
				switch(type)
				{
					case BKType.BL602:
						return EF32_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.TR6260:
						return EF32_GRAN32.ef_set_env_blob(key, value_buf, buf_len);
					case BKType.ECR6600:
						return EF32_ECR.ef_set_env_blob(key, value_buf, buf_len);
					default:
						return EF32.ef_set_env_blob(key, value_buf, buf_len);
				}
			}
		}

		public static unsafe byte[] LoadValueFromData(byte[] data, string sname, int size, BKType type, out byte[] efdata)
		{
			efdata = data;
			if(data == null)
				return null;
			if(type == BKType.TR6260)
				return NativeGran32_Load(data, sname);
			if(!SetupBase(data, size, type, out var env)) return null;
			byte[] bname = Encoding.ASCII.GetBytes(sname);
			fixed(byte* name = bname)
			{
				var test = stackalloc byte[1];
				uint savedlen = 0;
				uint res2 = ef_get_env_blob((char*)name, &test, 1, &savedlen, type);
				if(res2 == 0)
				{
					Console.WriteLine("No config in EF!");
					return null;
				}
				var config = stackalloc byte[(int)savedlen];
				res2 = ef_get_env_blob((char*)name, config, savedlen, &savedlen, type);
				byte[] managed_area = new byte[savedlen];
				for(int i = 0; i < savedlen; i++)
				{
					managed_area[i] = config[i];
				}
				return managed_area;
			}
		}

		public static unsafe byte[] SaveValueToNewEasyFlash(string sname, byte[] cfgData, int areaSize, BKType type)
		{
			if(type == BKType.TR6260)
				return NativeGran32_Save(sname, cfgData, areaSize);
			var data = new byte[areaSize];
			fixed (byte* ptr = data) memset((IntPtr)ptr, 0xFF, areaSize);
			if(!SetupBase(data, areaSize, type, out var env)) return null;

			byte[] bname = Encoding.ASCII.GetBytes(sname);
			fixed(byte* name = bname)
			{
				fixed(byte* pdata = cfgData)
				{
					uint res = ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length, type);
					Console.WriteLine($"ef_set_env_blob returned {res}");
					if(res != 0) return null;
				}
				var efdata = new byte[areaSize];
				for(int i = 0; i < areaSize; i++)
				{
					efdata[i] = env[i];
				}
				return efdata;
			}
		}

		public static unsafe byte[] SaveValueToExistingEasyFlash(string sname, byte[] efData, byte[] cfgData, int areaSize, BKType type)
		{
			if(type == BKType.TR6260)
				return NativeGran32_Save(sname, cfgData, areaSize);
			if(efData.Length != areaSize)
			{
				throw new Exception("Saved EF data length != target EF length");
			}
			var data = new byte[areaSize];
			fixed(byte* pdata = efData) fixed (byte* ptr = data) memcpy((IntPtr)ptr, (IntPtr)pdata, areaSize);
			if(!SetupBase(data, areaSize, type, out var env)) return null;

			byte[] bname = Encoding.ASCII.GetBytes(sname);
			fixed(byte* name = bname)
			{
				fixed(byte* pdata = cfgData)
				{
					uint res = ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length, type);
					Console.WriteLine($"ef_set_env_blob returned {res}");
					if(res != 0)
						return null;
				}
				var efdata = new byte[areaSize];
				for(int i = 0; i < areaSize; i++)
				{
					efdata[i] = env[i];
				}
				return efdata;
			}
		}

		// ---------------------------------------------------------------------------
		// Native GRAN=32 EasyFlash implementation for TR6260.
		// The pre-built WinEF_GRAN32 DLLs write GRAN=1 format (has "KV40" magic in
		// entries) which the TR6260 firmware rejects.  These methods implement the
		// correct GRAN=32 binary layout derived from the OpenTR6260 ef_env.c source
		// and verified byte-for-byte against a live TR6260 flash dump.
		//
		// Sector layout (GRAN=32, EF_WRITE_GRAN=32, sector_size=4096):
		//   [+0x00..+0x0B]  store status  (3 × uint32 dirty words)
		//   [+0x0C..+0x13]  dirty status  (2 × uint32 dirty words)
		//   [+0x14..+0x17]  0xFFFFFFFF
		//   [+0x18..+0x1B]  "EF40" magic  (0x30344645 LE)
		//   [+0x1C..+0x23]  0xFFFFFFFF
		//   [+0x24..]       entries
		//
		// Entry layout:
		//   [+0x00..+0x13]  status_table (5 × uint32; word[0]=0x00 PRE_WRITE, word[4]=0x00 WRITE)
		//   [+0x14..+0x17]  total_len (uint32) = 36 + WgAlign(name_len) + WgAlign(value_len)
		//   [+0x18..+0x1B]  crc32 = CRC32(name_len_byte, 0xFF×3, value_len_le32, name_padded, value_padded)
		//   [+0x1C]         name_len (uint8)
		//   [+0x1D..+0x1F]  0xFF×3 padding
		//   [+0x20..+0x23]  value_len (uint32)
		//   [+0x24..]       name (WgAlign(name_len) bytes, 0xFF-padded)
		//   [+0x24+WgAlign(name_len)..] value (WgAlign(value_len) bytes, 0xFF-padded)
		// ---------------------------------------------------------------------------

		private static int WgAlign32(int x) => (x + 3) & ~3;

		private static uint Crc32Ieee(byte[] data, int offset, int length)
		{
			// Standard IEEE 802.3 CRC-32 (same polynomial as zlib crc32).
			// Matches ef_calc_crc32() in OpenTR6260 which uses crc^~0U init/final XOR.
			CRC.initCRC();
			uint crc = CRC.crc32_ver2(0xFFFFFFFF, data, length, (uint)offset);
			return crc ^ 0xFFFFFFFF;
		}

		/// <summary>
		/// Build a fresh GRAN=32 EasyFlash area image containing a single entry.
		/// The returned buffer is areaSize bytes: one active sector followed by
		/// blank (magic-only) sectors to fill the rest of the area.
		/// </summary>
		private static byte[] NativeGran32_Save(string key, byte[] value, int areaSize)
		{
			const int SECTOR_SIZE = 4096;
			const int SECTOR_HDR  = 0x24;   // 36 bytes

			byte[] keyBytes  = Encoding.ASCII.GetBytes(key);
			int    nameLen   = keyBytes.Length;
			int    valueLen  = value.Length;
			int    nameSz    = WgAlign32(nameLen);
			int    valueSz   = WgAlign32(valueLen);
			int    totalLen  = 20 + 4 + 4 + 4 + 4 + nameSz + valueSz; // status+len+crc+name_len_field+value_len_field+name+value

			// Build CRC input: name_len(1) + pad(3) + value_len(4) + name_padded + value_padded
			byte[] crcBuf = new byte[4 + 4 + nameSz + valueSz];
			crcBuf[0] = (byte)nameLen;
			crcBuf[1] = 0xFF; crcBuf[2] = 0xFF; crcBuf[3] = 0xFF;
			crcBuf[4] = (byte)(valueLen      ); crcBuf[5] = (byte)(valueLen >>  8);
			crcBuf[6] = (byte)(valueLen >> 16); crcBuf[7] = (byte)(valueLen >> 24);
			// Fill padding with 0xFF, then copy actual bytes
			for(int i = 8; i < crcBuf.Length; i++) crcBuf[i] = 0xFF;
			Array.Copy(keyBytes, 0, crcBuf, 8,           nameLen);
			Array.Copy(value,    0, crcBuf, 8 + nameSz,  valueLen);
			uint crc32 = Crc32Ieee(crcBuf, 0, crcBuf.Length);

			// Build entry
			byte[] entry = new byte[totalLen];
			for(int i = 0; i < totalLen; i++) entry[i] = 0xFF;
			entry[0]  = 0x00;  // PRE_WRITE status word[0]
			entry[4]  = 0x00;  // WRITE     status word[1]
			WriteUint32LE(entry, 0x14, (uint)totalLen);
			WriteUint32LE(entry, 0x18, crc32);
			entry[0x1C] = (byte)nameLen;
			WriteUint32LE(entry, 0x20, (uint)valueLen);
			Array.Copy(keyBytes, 0, entry, 0x24,          nameLen);
			Array.Copy(value,    0, entry, 0x24 + nameSz, valueLen);

			// Build active sector header (STORE_USING, DIRTY_FALSE)
			byte[] secHdr = new byte[SECTOR_HDR];
			for(int i = 0; i < SECTOR_HDR; i++) secHdr[i] = 0xFF;
			secHdr[0]  = 0x00;  // store word[0] = EMPTY
			secHdr[4]  = 0x00;  // store word[1] = USING
			secHdr[12] = 0x00;  // dirty word[0] = DIRTY_FALSE
			secHdr[0x18] = 0x45; secHdr[0x19] = 0x46;   // "EF40"
			secHdr[0x1A] = 0x34; secHdr[0x1B] = 0x30;

			// Build active sector
			byte[] activeSector = new byte[SECTOR_SIZE];
			for(int i = 0; i < SECTOR_SIZE; i++) activeSector[i] = 0xFF;
			Array.Copy(secHdr, 0, activeSector, 0,          SECTOR_HDR);
			Array.Copy(entry,  0, activeSector, SECTOR_HDR, totalLen);

			// Build blank (formatted but empty) sector
			byte[] blankSector = new byte[SECTOR_SIZE];
			for(int i = 0; i < SECTOR_SIZE; i++) blankSector[i] = 0xFF;
			blankSector[0]  = 0x00;  // store EMPTY
			blankSector[12] = 0x00;  // dirty FALSE
			blankSector[0x18] = 0x45; blankSector[0x19] = 0x46;
			blankSector[0x1A] = 0x34; blankSector[0x1B] = 0x30;

			int nSectors = areaSize / SECTOR_SIZE;
			byte[] result = new byte[areaSize];
			for(int i = 0; i < areaSize; i++) result[i] = 0xFF;
			Array.Copy(activeSector, 0, result, 0, SECTOR_SIZE);
			for(int s = 1; s < nSectors; s++)
				Array.Copy(blankSector, 0, result, s * SECTOR_SIZE, SECTOR_SIZE);

			return result;
		}

		/// <summary>
		/// Scan a GRAN=32 EasyFlash area image and return the most recent value
		/// stored under 'key', or null if not found.
		/// </summary>
		private static byte[] NativeGran32_Load(byte[] data, string key)
		{
			const int SECTOR_SIZE = 4096;
			const int SECTOR_HDR  = 0x24;
			byte[] magic    = new byte[] { 0x45, 0x46, 0x34, 0x30 }; // "EF40"
			byte[] keyBytes = Encoding.ASCII.GetBytes(key);
			int    keyLen   = keyBytes.Length;

			byte[] latest = null;

			for(int sectorOff = 0; sectorOff + SECTOR_SIZE <= data.Length; sectorOff += SECTOR_SIZE)
			{
				// Check sector magic
				if(data[sectorOff + 0x18] != magic[0] || data[sectorOff + 0x19] != magic[1] ||
				   data[sectorOff + 0x1A] != magic[2] || data[sectorOff + 0x1B] != magic[3])
					continue;

				int pos = sectorOff + SECTOR_HDR;
				while(pos + 36 <= sectorOff + SECTOR_SIZE)
				{
					byte s0 = data[pos];
					if(s0 == 0xFF) break;  // ENV_UNUSED – no more entries in sector

					int totalLen = (int)ReadUint32LE(data, pos + 0x14);
					if(totalLen == unchecked((int)0xFFFFFFFF) || totalLen <= 0 ||
					   pos + totalLen > sectorOff + SECTOR_SIZE)
						break;

					// s0==0x00 means at least PRE_WRITE; s1==0x00 means WRITE committed
					byte s1 = data[pos + 4];
					if(s0 == 0x00 && s1 == 0x00)
					{
						int entryNameLen = data[pos + 0x1C];
						if(entryNameLen == keyLen)
						{
							bool match = true;
							for(int i = 0; i < keyLen; i++)
							{
								if(data[pos + 0x24 + i] != keyBytes[i]) { match = false; break; }
							}
							if(match)
							{
								int valueLen  = (int)ReadUint32LE(data, pos + 0x20);
								int nameSz    = WgAlign32(entryNameLen);
								int valStart  = pos + 0x24 + nameSz;
								if(valStart + valueLen <= sectorOff + SECTOR_SIZE)
								{
									latest = new byte[valueLen];
									Array.Copy(data, valStart, latest, 0, valueLen);
								}
							}
						}
					}

					pos += totalLen;
				}
			}

			return latest;
		}

		private static void WriteUint32LE(byte[] buf, int offset, uint value)
		{
			buf[offset]     = (byte) value;
			buf[offset + 1] = (byte)(value >>  8);
			buf[offset + 2] = (byte)(value >> 16);
			buf[offset + 3] = (byte)(value >> 24);
		}

		private static uint ReadUint32LE(byte[] buf, int offset)
		{
			return (uint)(buf[offset] | (buf[offset+1] << 8) | (buf[offset+2] << 16) | (buf[offset+3] << 24));
		}
	}
}
