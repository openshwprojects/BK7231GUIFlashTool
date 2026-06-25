using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BK7231Flasher
{
	// original code is at https://github.com/mildsunrise/bk7231_key_calculator
	public static class Beken_Crypto
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort Stage1(uint addr, byte param)
		{
			ushort part0 = (param & 1) != 0 ? (ushort)((addr >> 8) | (addr << 8)) : (ushort)addr;
			ushort part1 = (ushort)(addr >> 16);
			if(((param >> 1) & 1) != 0)
				part1 = (ushort)((part1 >> 8) | (part1 << 8));

			ushort a = (ushort)(part0 ^ part1);
			ushort z = (ushort)(((a >> 5) & 0xF) * 0x1111);

			ushort rot = (ushort)((a >> 7) | (a << 9));
			return (ushort)(rot ^ (0x6371 & z));
		}


		private static readonly ushort[] Stage2Table =
		{
			0x0000, 0x1011, 0x2200, 0x3211,
			0x0440, 0x1451, 0x2640, 0x3651,
			0x0008, 0x1019, 0x2208, 0x3219,
			0x0448, 0x1459, 0x2648, 0x3659
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort Stage2(uint addr, byte param)
		{
			uint a = (addr >> param) & 0x1FFFF;

			uint x = ((a >> 13) & 1) |
					 (((a >> 9) & 1) << 1) |
					 (((a >> 5) & 1) << 2) |
					 (((a >> 1) & 1) << 3);

			return (ushort)((a >> 10) ^ (a << 7) ^ Stage2Table[x]);
		}
		private static readonly uint[] Stage3Table = new uint[16]
		{
			0x00000000, 0x11111111, 0x22222222, 0x33333333,
			0x44444444, 0x55555555, 0x66666666, 0x77777777,
			0x88888888, 0x99999999, 0xAAAAAAAA, 0xBBBBBBBB,
			0xCCCCCCCC, 0xDDDDDDDD, 0xEEEEEEEE, 0xFFFFFFFF
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Stage3(uint addr, byte param)
		{
			int bits = (param & 3) << 3;
			addr = Utils.RotateRight(addr, bits);
			return Utils.RotateLeft(addr, 17) ^ (0xE519A4F1u & Stage3Table[(addr >> 2) & 0xF]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Encrypt(uint addr, byte?[] selectors)
		{
			uint outv = 0;
			if(selectors[0].HasValue)
				outv ^= (uint)Stage1(addr, selectors[0].Value) << 16;
			if(selectors[1].HasValue)
				outv ^= Stage2(addr, selectors[1].Value);
			if(selectors[2].HasValue)
				outv ^= Stage3(addr, selectors[2].Value);
			return outv;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void Keystream(byte?[] selectors, uint addr, Span<uint> mem)
		{
			fixed(uint* rp = &mem[0])
			{
				for(int i = 0; i < mem.Length; ++i)
					rp[i] = Encrypt(addr + (uint)(i << 2), selectors);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FormatSettingsWord(byte?[] selectors)
		{
			uint outv = 0b01010101u << 24;
			for(int i = 0; i < selectors.Length; i++)
			{
				if(selectors[i] == null)
					outv |= (uint)(1 << i);
				else
					outv |= (uint)(selectors[i].Value << (5 + i * 3));
			}
			return outv;
		}
	}

	static class Utils
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint[] U8ToU32(Span<byte> xs) => MemoryMarshal.Cast<byte, uint>(xs).ToArray();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<uint> U8ToU32Span(Span<byte> xs) => MemoryMarshal.Cast<byte, uint>(xs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] U32ToU8(Span<uint> xs) => MemoryMarshal.Cast<uint, byte>(xs).ToArray();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<byte> U32ToU8Span(Span<uint> xs) => MemoryMarshal.Cast<uint, byte>(xs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint[] XorIter(Span<uint> xs, Span<uint> ys)
		{
			var len = Math.Min(xs.Length, ys.Length);
			var r = new uint[len];
			fixed(uint* rp = &r[0])
			fixed(uint* xsPtr = &xs[0])
			fixed(uint* ysPtr = &ys[0])
			{
				for(int i = 0; i < len; ++i)
					rp[i] = xsPtr[i] ^ ysPtr[i];
			}
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint[] XorIter(uint[] r, Span<uint> xs, Span<uint> ys)
		{
			fixed(uint* rp = &r[0])
			fixed(uint* xsPtr = &xs[0])
			fixed(uint* ysPtr = &ys[0])
			{
				for(int i = 0; i < r.Length; ++i)
					rp[i] = xsPtr[i] ^ ysPtr[i];
			}
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe byte[] XorIter(Span<byte> xs, Span<byte> ys)
		{
			var len = Math.Min(xs.Length, ys.Length);
			var r = new byte[len];
			fixed(byte* rp = &r[0])
			fixed(byte* xsPtr = &xs[0])
			fixed(byte* ysPtr = &ys[0])
			{
				for(int i = 0; i < len; ++i)
					rp[i] = (byte)(xsPtr[i] ^ ysPtr[i]);
			}
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe byte[] XorIter(byte[] r, Span<byte> xs, Span<byte> ys)
		{
			fixed(byte* rp = &r[0])
			fixed(byte* xsPtr = &xs[0])
			fixed(byte* ysPtr = &ys[0])
			{
				for(int i = 0; i < r.Length; ++i)
					rp[i] = (byte)(xsPtr[i] ^ ysPtr[i]);
			}
			return r;
		}

		private static readonly int[] FindAllTable = new int[256];
		private static readonly List<int> FindAllMatches = new List<int>(256);
		public static unsafe int[] FindAll(Span<byte> haystack, byte[] needle)
		{
			if(needle.Length == 0)
				return Array.Empty<int>();
			if(FindAllMatches.Count > 0)
				FindAllMatches.Clear();
			fixed(int* skip = &FindAllTable[0])
			fixed(byte* hay = &haystack[0])
			fixed(byte* ned = &needle[0])
			{
				for(int i = 0; i < 256; ++i)
					skip[i] = needle.Length;
				for(int i = 0; i < needle.Length - 1; ++i)
					skip[ned[i]] = needle.Length - 1 - i;

				int n = haystack.Length;
				int m = needle.Length;

				for(int i = 0; i <= n - m;)
				{
					int j = m - 1;
					while(j >= 0 && hay[i + j] == ned[j])
						--j;

					if(j < 0)
					{
						FindAllMatches.Add(i);
						++i;
					}
					else
					{
						i += skip[hay[i + m - 1]];
					}
				}
			}
			return FindAllMatches.ToArray();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotateLeft(uint value, int bits)
			=> (value << bits) | (value >> (32 - bits));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotateRight(uint value, int bits) =>
			(value >> bits) | (value << (32 - bits));

		public static readonly IReadOnlyList<byte[]> BootloaderDict = new List<byte[]>()
		{
			Encoding.ASCII.GetBytes("[ARM ANOMALY]"),
			Encoding.ASCII.GetBytes("0123456789ABCDEF"),
			Encoding.ASCII.GetBytes("pwm_iconfig_fail\r\n"),
			Encoding.ASCII.GetBytes("incorrect header check"),
			Encoding.ASCII.GetBytes("invalid window size"),
			Encoding.ASCII.GetBytes("unknown compression method"),
			Encoding.ASCII.GetBytes("Custom verify RAW firmware failed!"),
			Encoding.ASCII.GetBytes("[E/OTA] (%s:%d) "),
			Encoding.ASCII.GetBytes("unknown header flags set"),
			Encoding.ASCII.GetBytes("header crc mismatch"),
			Encoding.ASCII.GetBytes("invalid block type"),
			Encoding.ASCII.GetBytes("invalid stored block lengths"),
			Encoding.ASCII.GetBytes("too many length or distance symbols"),
			Encoding.ASCII.GetBytes("invalid code lengths set"),
		}.AsReadOnly();

		public static readonly IReadOnlyList<uint> KnownKeysAddresses = new List<uint>()
		{
			0x1F08, // Common Beken bootloaders, 1.0.8, 1.0.13 etc.
			0x48,   // Tuya 1.0.1 N or 1.0.5 T
			//0x20F8, // Tuya_A9_Cam_SC-10024_V1.0.0_(VGA卡片机-schemaID-000003uanz)_keyph8vwhadwsx3a_u0kmecmr7j8roazz_NEO_SPI_1.0.4.bin
		};

		internal static readonly uint FAL_PART_MAGIC_WORD = 0x45503130;

		public static byte[] UnCRC(byte[] bytes)
		{
			bytes = MiscUtils.padArray(bytes, 34);
			var decrc = new List<byte>();
			var packet = new byte[32];
			for(int i = 0; i < bytes.Length; i += 34)
			{
				Array.Copy(bytes, i, packet, 0, 32);
				var origcrc = bytes[i + 32] << 8 | bytes[i + 33];
				var computedcrc = CRC16.Compute(CRC16Type.CMS, packet);
				if(origcrc != computedcrc)
				{
					break;
				}
				decrc.AddRange(packet);
			}
			return decrc.ToArray();
		}

		public static byte[] CRCBeken(byte[] bytes)
		{
			bytes = MiscUtils.padArray(bytes, 32);
			var crc = new List<byte>(bytes.Length + bytes.Length / 16);
			var packet = new byte[34];
			for(int i = 0; i < bytes.Length; i += 32)
			{
				Array.Copy(bytes, i, packet, 0, 32);
				var computedcrc = CRC16.Compute(CRC16Type.CMS, packet, 0, 32);
				packet[32] = (byte)(computedcrc >> 8);
				packet[33] = (byte)computedcrc;
				crc.AddRange(packet);
			}
			return crc.ToArray();
		}

		private static readonly uint[] VerifyDecryptBuffer = new uint[4];
		private static readonly uint[] VerifyDecryptKeys = new uint[4];

		public static bool VerifyDecrypt(Span<uint> origWords, uint[] decryptedWords, out uint[] decrKeys, out uint address)
		{
			decrKeys = null;
			address = 0;

			foreach(var addr in KnownKeysAddresses)
			{
				address = addr;
				var keyIndex = addr >> 2;

				VerifyDecryptKeys[0] = decryptedWords[keyIndex + 0];
				VerifyDecryptKeys[1] = decryptedWords[keyIndex + 1];
				VerifyDecryptKeys[2] = decryptedWords[keyIndex + 2];
				VerifyDecryptKeys[3] = decryptedWords[keyIndex + 3];

				if(TryDecryptAndValidate(origWords, VerifyDecryptKeys, keyIndex, VerifyDecryptBuffer, out decrKeys))
					return true;

				if(TryDecryptAndValidate(origWords, decrKeys, keyIndex, VerifyDecryptBuffer, out decrKeys))
					return true;
			}

			return false;
		}

		private static unsafe bool TryDecryptAndValidate(Span<uint> origWords, uint[] keyCandidate, uint keyIndex, uint[] buffer, out uint[] extractedKeys)
		{
			var crypto = new BekenCrypto(keyCandidate);

			extractedKeys = new uint[4];

			fixed(uint* eKeys = &extractedKeys[0])
			fixed(uint* oWords = &origWords[0])
			fixed(uint* keyC = &keyCandidate[0])
			{
				uint offset = keyIndex << 2;
				for(int i = 0; i < 4; i++)
				{
					buffer[i] = crypto.EncryptU32(offset, oWords[i + (int)keyIndex]);
					offset += 4;
				}

				eKeys[0] = buffer[0];
				eKeys[1] = buffer[1];
				eKeys[2] = buffer[2];
				eKeys[3] = buffer[3];

				for(int i = 0; i < 4; i++)
					if(eKeys[i] != keyC[i])
						return false;
			}
			return true;
		}

		public static byte[] EncryptDecryptBekenFW(uint[] keys, byte[] data, uint startAddr = 0)
		{
			var imageU32 = U8ToU32(data);
			return EncryptDecryptBekenFW(keys, imageU32, startAddr);
		}

		public static byte[] EncryptDecryptBekenFW(uint[] keys, uint[] data, uint startAddr = 0)
		{
			var crypted = new uint[data.Length];
			var crypto = new BekenCrypto(keys);
			uint addr = startAddr;
			for(int en = 0; en < data.Length; en++)
			{
				crypted[en] = crypto.EncryptU32(addr, data[en]);
				addr += 4;
			}
			return U32ToU8(crypted);
		}

		public static T FromBytes<T>(byte[] buffer, int offset = 0) where T : struct
		{
			var size = Marshal.SizeOf<T>();
			var ptr = Marshal.AllocHGlobal(size);
			Marshal.Copy(buffer, offset, ptr, size);
			var value = Marshal.PtrToStructure<T>(ptr);
			Marshal.FreeHGlobal(ptr);
			return value;
		}

		public static byte[] ToBytes<T>(T data) where T : struct
		{
			var size = Marshal.SizeOf(data);
			var bytes = new byte[size];

			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			try
			{
				Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
			}
			finally
			{
				handle.Free();
			}

			return bytes;
		}
	}

	// vibe-coded
	public class BekenCrypto
	{
		private const uint PN15_CONST = 0x6371u;
		private const uint PN32_CONST = 0xE519A4F1u;
		private const uint PN16_CONST = 0x13659u;

		private readonly uint coef0;
		private readonly uint coef1_mix;
		private readonly uint coef1_hi16;
		private readonly uint random;
		private readonly bool bypass;

		private readonly bool usePn15;
		private readonly int a_hi_start, a_hi_stop, a_lo_start, a_lo_stop;
		private readonly int b_hi_start, b_hi_stop, b_lo_start, b_lo_stop;
		private readonly uint mask_a_hi, mask_a_lo, mask_b_hi, mask_b_lo;

		private readonly bool usePn16;
		private readonly int pn16_start;
		private readonly int pn16_stop;
		private readonly uint pn16_mask;

		private readonly bool usePn32;
		private readonly Func<uint, uint> pn32_calc;

		public BekenCrypto(uint[] coeffs)
		{
			coef0 = coeffs[0];
			uint coef1 = coeffs[1];
			uint coef2 = coeffs[2];
			uint coef3 = coeffs[3];

			uint top = coef3 >> 24;
			if(top == 0x00 || top == 0xFF || (coef3 & 0xF) == 0xF)
			{
				bypass = true;
				return;
			}

			coef1_mix = ((coef1 >> 8) & 0xFFu) << 9 | ((coef3 >> 4) & 1u) << 8 | (coef1 & 0xFFu);
			coef1_hi16 = coef1 >> 16;

			bool pn15_bps = ((coef3 >> 0) & 1) != 0;
			bool pn16_bps = ((coef3 >> 1) & 1) != 0;
			bool pn32_bps = ((coef3 >> 2) & 1) != 0;
			bool rand_bps = ((coef3 >> 3) & 1) != 0;

			if(!pn15_bps)
			{
				usePn15 = true;
				int sel = (int)((coef3 >> 5) & 0x3);

				switch(sel)
				{
					case 0:
						a_hi_start = 31;
						a_hi_stop = 24;
						a_lo_start = 23;
						a_lo_stop = 16;
						b_hi_start = 15;
						b_hi_stop = 8;
						b_lo_start = 7;
						b_lo_stop = 0;
						break;
					case 1:
						a_hi_start = 31;
						a_hi_stop = 24;
						a_lo_start = 23;
						a_lo_stop = 16;
						b_hi_start = 7;
						b_hi_stop = 0;
						b_lo_start = 15;
						b_lo_stop = 8;
						break;
					case 2:
						a_hi_start = 23;
						a_hi_stop = 16;
						a_lo_start = 31;
						a_lo_stop = 24;
						b_hi_start = 15;
						b_hi_stop = 8;
						b_lo_start = 7;
						b_lo_stop = 0;
						break;
					default:
						a_hi_start = 23;
						a_hi_stop = 16;
						a_lo_start = 31;
						a_lo_stop = 24;
						b_hi_start = 7;
						b_hi_stop = 0;
						b_lo_start = 15;
						b_lo_stop = 8;
						break;
				}

				mask_a_hi = (uint)((1 << (a_hi_start - a_hi_stop + 1)) - 1);
				mask_a_lo = (uint)((1 << (a_lo_start - a_lo_stop + 1)) - 1);
				mask_b_hi = (uint)((1 << (b_hi_start - b_hi_stop + 1)) - 1);
				mask_b_lo = (uint)((1 << (b_lo_start - b_lo_stop + 1)) - 1);
			}

			if(!pn16_bps)
			{
				usePn16 = true;
				int sel = (int)((coef3 >> 8) & 0x3);
				pn16_start = 16 + sel;
				pn16_stop = sel;
				pn16_mask = (uint)((1 << (pn16_start - pn16_stop + 1)) - 1);
			}

			if(!pn32_bps)
			{
				usePn32 = true;
				int sel = (int)((coef3 >> 11) & 0x3);
				switch(sel)
				{
					case 0:
						pn32_calc = addr => addr;
						break;
					case 1:
						pn32_calc = addr => (addr >> 8) + (addr << 24);
						break;
					case 2:
						pn32_calc = addr => (addr >> 16) + (addr << 16);
						break;
					default:
						pn32_calc = addr => (addr >> 24) + (addr << 8);
						break;
				}
			}

			random = rand_bps ? 0u : coef2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint Pn15(uint addr)
		{
			uint a = ((addr & 0x7F) << 9) | ((addr >> 7) & 0x1FF);
			uint b = (addr >> 5) & 0xF;
			uint c = PN15_CONST & (b * 0x1111u);
			return a ^ c;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint Pn16(uint addr)
		{
			uint a = ((addr & 0x3FF) << 7) | ((addr >> 10) & 0x7F);

			uint b =
				(((addr >> 13) & 1) << 0) |
				(((addr >> 9) & 1) << 1) |
				(((addr >> 5) & 1) << 2) |
				(((addr >> 1) & 1) << 3);

			uint c = (addr >> 4) & 1;

			uint d = PN16_CONST & ((c << 16) | (b * 0x1111u));
			return a ^ d;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint pn32(uint addr)
		{
			uint a = ((addr & 0x7FFF) << 17) | ((addr >> 15) & 0x1FFFF);
			uint b = (addr >> 2) & 0xF;
			uint c = PN32_CONST & (b * 0x11111111u);
			return a ^ c;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint EncryptU32(uint addr, uint data)
		{
			if(bypass)
				return data;

			uint pn15_v = 0, pn16_v = 0, pn32_v = 0;

			if(usePn15)
			{
				uint a_part = ((addr >> a_hi_stop) & mask_a_hi) << 8;
				a_part |= (addr >> a_lo_stop) & mask_a_lo;

				uint b_part = ((addr >> b_hi_stop) & mask_b_hi) << 8;
				b_part |= (addr >> b_lo_stop) & mask_b_lo;

				uint pn15_addr = (a_part ^ b_part) ^ coef1_hi16;
				pn15_v = Pn15(pn15_addr);
			}

			if(usePn16)
			{
				uint pn16_A = (addr >> pn16_stop) & pn16_mask;
				uint pn16_addr = pn16_A ^ coef1_mix;
				pn16_v = Pn16(pn16_addr);
			}

			if(usePn32)
			{
				uint pn32_addr = pn32_calc(addr) ^ coef0;
				pn32_v = pn32(pn32_addr);
			}

			uint pnout = pn32_v ^ ((pn15_v << 16) | (pn16_v & 0xFFFF)) ^ random;
			return data ^ pnout;
		}
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct FALPartition64
	{
		public uint magic_word;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
		public string name;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
		public string flash_name;
		public uint offset;
		public uint len;
		public uint crc32;
	};
}
