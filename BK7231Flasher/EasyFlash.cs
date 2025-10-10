using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BK7231Flasher
{
	static class EasyFlash
	{
		static class EF32
		{
			const string Dll = "WinEF_x86.dll";

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
			const string Dll = "WinEF_x64.dll";

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
			const string Dll = "WinEF_GRAN8_x86.dll";

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
			const string Dll = "WinEF_GRAN8_x64.dll";

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
			const string Dll = "libef.so";

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
			const string Dll = "libef.so";

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

		private static unsafe void SetupBase(byte[] data, int size, BKType type, out byte* env)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if(type == BKType.BL602)
					EFLinux_GRAN8.set_env_size((uint)size);
				else
					EFLinux.set_env_size((uint)size);
			}
			else if(IntPtr.Size == 8)
			{
				if(type == BKType.BL602)
					EF64_GRAN8.set_env_size((uint)size);
				else
					EF64.set_env_size((uint)size);
			}
			else
			{
				if(type == BKType.BL602)
					EF32_GRAN8.set_env_size((uint)size);
				else
					EF32.set_env_size((uint)size);
			}

			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if(type == BKType.BL602)
					env = EFLinux_GRAN8.get_env_area();
				else
					env = EFLinux.get_env_area();
			}
			else if(IntPtr.Size == 8)
			{
				if(type == BKType.BL602)
					env = EF64_GRAN8.get_env_area();
				else
					env = EF64.get_env_area();
			}
			else
			{
				if(type == BKType.BL602)
					env = EF32_GRAN8.get_env_area();
				else
					env = EF32.get_env_area();
			}

			fixed(byte* pdata = data)
				memcpy((IntPtr)env, (IntPtr)pdata, size);
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if(type == BKType.BL602)
					EFLinux_GRAN8.easyflash_init();
				else
					EFLinux.easyflash_init();
			}
			else if(IntPtr.Size == 8)
			{
				if(type == BKType.BL602)
					EF64_GRAN8.easyflash_init();
				else
					EF64.easyflash_init();
			}
			else
			{
				if(type == BKType.BL602)
					EF32_GRAN8.easyflash_init();
				else
					EF32.easyflash_init();
			}
		}

		public static unsafe uint ef_get_env_blob(char* key, void* value_buf, uint buf_len, uint* saved_value_len, BKType type)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if(type == BKType.BL602)
					return EFLinux_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				else
					return EFLinux.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
			}
			else if(IntPtr.Size == 8)
			{
				if(type == BKType.BL602)
					return EF64_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				else
					return EF64.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
			}
			else
			{
				if(type == BKType.BL602)
					return EF32_GRAN8.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
				else
					return EF32.ef_get_env_blob(key, value_buf, buf_len, saved_value_len);
			}
		}

		public static unsafe uint ef_set_env_blob(char* key, void* value_buf, uint buf_len, BKType type)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if(type == BKType.BL602)
					return EFLinux_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
				else
					return EFLinux.ef_set_env_blob(key, value_buf, buf_len);
			}
			else if(IntPtr.Size == 8)
			{
				if(type == BKType.BL602)
					return EF64_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
				else
					return EF64.ef_set_env_blob(key, value_buf, buf_len);
			}
			else
			{
				if(type == BKType.BL602)
					return EF32_GRAN8.ef_set_env_blob(key, value_buf, buf_len);
				else
					return EF32.ef_set_env_blob(key, value_buf, buf_len);
			}
		}

		public static unsafe byte[] LoadFromData(byte[] data, int size, BKType type, out byte[] efdata)
		{
			efdata = data;
			if(data == null)
				return null;
			SetupBase(data, size, type, out var env);
			byte[] bname = type == BKType.BL602 ? Encoding.ASCII.GetBytes("mY0bcFg") : Encoding.ASCII.GetBytes("ObkCfg");
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

		public static unsafe byte[] SaveCfgToNewEasyFlash(OBKConfig cfg, int areaSize, BKType type)
		{
			var data = new byte[areaSize];
			fixed (byte* ptr = data) memset((IntPtr)ptr, 0xFF, areaSize);
			SetupBase(data, areaSize, type, out var env);

			byte[] bname = type == BKType.BL602 ? Encoding.ASCII.GetBytes("mY0bcFg") : Encoding.ASCII.GetBytes("ObkCfg");
			fixed(byte* name = bname)
			{
				cfg.saveConfig(type);
				var cfgData = cfg.getData();
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

		public static unsafe byte[] SaveCfgToExistingEasyFlash(OBKConfig cfg, int areaSize, BKType type)
		{
			if(cfg.efdata.Length != areaSize)
			{
				throw new Exception("Saved EF data length != target EF length");
			}
			var data = new byte[areaSize];
			fixed(byte* pdata = cfg.efdata) fixed (byte* ptr = data) memcpy((IntPtr)ptr, (IntPtr)pdata, areaSize);
			SetupBase(data, areaSize, type, out var env);

			byte[] bname = type == BKType.BL602 ? Encoding.ASCII.GetBytes("mY0bcFg") : Encoding.ASCII.GetBytes("ObkCfg");
			fixed(byte* name = bname)
			{
				cfg.saveConfig(type);
				var cfgData = cfg.getData();
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
