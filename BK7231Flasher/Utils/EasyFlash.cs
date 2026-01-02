using System;
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
	}
}
