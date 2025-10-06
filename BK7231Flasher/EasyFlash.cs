using System;
using System.Drawing;
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

		[DllImport("msvcrt.dll", SetLastError = false, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
		
		[DllImport("msvcrt.dll", SetLastError = false, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr memset(IntPtr dest, int c, int count);

		public static unsafe byte[] LoadFromData(byte[] data, int size, out byte[] efdata)
		{
			efdata = data;
			if(data == null)
				return null;
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.set_env_size((uint)size);
			else if(IntPtr.Size == 8)
				EF64.set_env_size((uint)size);
			else
				EF32.set_env_size((uint)size);
			byte* env;
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				env = EFLinux.get_env_area();
			else if(IntPtr.Size == 8)
				env = EF64.get_env_area();
			else
				env = EF32.get_env_area();

			fixed(byte* pdata = data) memcpy((IntPtr)env, (IntPtr)pdata, size);
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.easyflash_init();
			else if(IntPtr.Size == 8)
				EF64.easyflash_init();
			else
				EF32.easyflash_init();
			byte[] bname = Encoding.ASCII.GetBytes("ObkCfg");
			fixed(byte* name = bname)
			{
				var test = stackalloc byte[1];
				uint savedlen = 0;
				uint res2;
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					res2 = EFLinux.ef_get_env_blob((char*)name, &test, 1, &savedlen);
				else if(IntPtr.Size == 8)
					res2 = EF64.ef_get_env_blob((char*)name, &test, 1, &savedlen);
				else
					res2 = EF32.ef_get_env_blob((char*)name, &test, 1, &savedlen);
				if(res2 == 0)
				{
					Console.WriteLine("No config in EF!");
					return null;
				}
				var config = stackalloc byte[(int)savedlen];
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					res2 = EFLinux.ef_get_env_blob((char*)name, config, savedlen, &savedlen);
				else if(IntPtr.Size == 8)
					res2 = EF64.ef_get_env_blob((char*)name, config, savedlen, &savedlen);
				else
					res2 = EF32.ef_get_env_blob((char*)name, config, savedlen, &savedlen);
				byte[] managed_area = new byte[savedlen];
				for(int i = 0; i < savedlen; i++)
				{
					managed_area[i] = config[i];
				}
				return managed_area;
			}
		}

		public static unsafe byte[] SaveCfgToNewEasyFlash(OBKConfig cfg, int areaSize)
		{
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.set_env_size((uint)areaSize);
			else if(IntPtr.Size == 8)
				EF64.set_env_size((uint)areaSize);
			else
				EF32.set_env_size((uint)areaSize);
			byte* env;
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				env = EFLinux.get_env_area();
			else if(IntPtr.Size == 8)
				env = EF64.get_env_area();
			else
				env = EF32.get_env_area();

			memset((IntPtr)env, 0xFF, areaSize);
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.easyflash_init();
			else if(IntPtr.Size == 8)
				EF64.easyflash_init();
			else
				EF32.easyflash_init();

			byte[] bname = Encoding.ASCII.GetBytes("ObkCfg");
			fixed(byte* name = bname)
			{
				cfg.saveConfig();
				var cfgData = cfg.getData();
				fixed(byte* pdata = cfgData)
				{
					uint res;
					if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						res = EFLinux.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
					else if(IntPtr.Size == 8)
						res = EF64.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
					else
						res = EF32.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
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

		public static unsafe byte[] SaveCfgToExistingEasyFlash(OBKConfig cfg, int areaSize)
		{
			if(cfg.efdata.Length != areaSize)
			{
				throw new Exception("Saved EF data length != target EF length");
			}
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.set_env_size((uint)areaSize);
			else if(IntPtr.Size == 8)
				EF64.set_env_size((uint)areaSize);
			else
				EF32.set_env_size((uint)areaSize);
			byte* env;
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				env = EFLinux.get_env_area();
			else if(IntPtr.Size == 8)
				env = EF64.get_env_area();
			else
				env = EF32.get_env_area();
			fixed(byte* pdata = cfg.efdata) memcpy((IntPtr)env, (IntPtr)pdata, areaSize);
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				EFLinux.easyflash_init();
			else if(IntPtr.Size == 8)
				EF64.easyflash_init();
			else
				EF32.easyflash_init();

			byte[] bname = Encoding.ASCII.GetBytes("ObkCfg");
			fixed(byte* name = bname)
			{
				cfg.saveConfig();
				var cfgData = cfg.getData();
				fixed(byte* pdata = cfgData)
				{
					uint res;
					if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						res = EFLinux.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
					else if(IntPtr.Size == 8)
						res = EF64.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
					else
						res = EF32.ef_set_env_blob((char*)name, pdata, (uint)cfgData.Length);
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
