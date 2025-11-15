using System;
using System.Collections.Generic;
using System.Linq;

namespace BK7231Flasher
{
	// original code is at https://github.com/mildsunrise/bk7231_key_calculator
	public static class Beken_Crypto
	{
		static T Bit<T>(T x, int b) where T : unmanaged
		{
			dynamic dx = x;
			return (T)(dx >> b & 1);
		}

		static ushort Stage1(uint addr, byte param)
		{
			ushort Swap(ushort x, bool s) => s ? (ushort)((x >> 8) | (x << 8)) : x;
			ushort Part(int b) => Swap((ushort)(addr >> (16 * b)), Bit(param, b) != 0);
			ushort a = Enumerable.Range(0, 2).Aggregate((ushort)0, (y, b) => (ushort)(y ^ Part(b)));
			ushort z = (ushort)(((a >> 5) & 0xF) * 0x1111);
			return (ushort)(RotateRight(a, 7) ^ (0x6371 & z));
		}

		static ushort Stage2(uint addr, byte param)
		{
			addr = (addr >> param) & 0x1FFFF;
			ushort x = (ushort)(Enumerable.Range(0, 4).Aggregate(0, (acc, b) => (acc << 1) | (int)Bit(addr, 1 + b * 4)) * 0x1111);
			return (ushort)(((addr >> 10) ^ (addr << 7) ^ (0x3659 & x)) & 0xFFFF);
		}

		static uint Stage3(uint addr, byte param)
		{
			addr = RotateRight(addr, 8 * param);
			uint x = ((addr >> 2) & 0xF) * 0x11111111;
			return RotateRight(addr, 15) ^ (0xE519A4F1u & x);
		}

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

		public static IEnumerable<uint> Keystream(byte?[] selectors, uint addr)
		{
			for(uint i = addr; ; i += 4)
				yield return Encrypt(i, selectors);
		}

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

		static ushort RotateRight(ushort value, int bits)
		   => (ushort)((value >> bits) | (value << (16 - bits)));

		static uint RotateRight(uint value, int bits)
		   => (value >> bits) | (value << (32 - bits));
	}

	static class Utils
	{
		public static List<uint> U8ToU32(byte[] xs)
		{
			int len = xs.Length / 4;
			var res = new List<uint>(len);
			unsafe
			{
				fixed(byte* p = xs)
				{
					uint* u = (uint*)p;
					for(int i = 0; i < len; i++)
						res.Add(u[i]);
				}
			}
			return res;
		}

		public static byte[] U32ToU8(IEnumerable<uint> xs)
		{
			var list = new List<byte>();
			foreach(var x in xs)
				list.AddRange(BitConverter.GetBytes(x));
			return list.ToArray();
		}

		public static List<T> XorIter<T>(IEnumerable<T> xs, IEnumerable<T> ys) where T : unmanaged
		{
			var res = new List<T>();
			using(var e1 = xs.GetEnumerator())
			using(var e2 = ys.GetEnumerator())
				while(e1.MoveNext() && e2.MoveNext())
				{
					dynamic a = e1.Current, b = e2.Current;
					res.Add((T)(a ^ b));
				}
			return res;
		}

		public static IEnumerable<int> FindAll(byte[] haystack, byte[] needle)
		{
			for(int i = 0; i <= haystack.Length - needle.Length; i++)
			{
				bool match = true;
				for(int j = 0; j < needle.Length; j++)
				{
					if(haystack[i + j] != needle[j])
					{
						match = false;
						break;
					}
				}
				if(match)
					yield return i;
			}
		}

		public static uint RotateLeft(uint value, int bits)
			=> (value << bits) | (value >> (32 - bits));

		public static readonly List<string> BootloaderDict = new List<string>()
		{
			"[ARM ANOMALY]",
			"0123456789ABCDEF",
			"pwm_iconfig_fail\r\n",
			"incorrect header check",
			"invalid window size",
			"unknown compression method",
			"Custom verify RAW firmware failed!",
			"[E/OTA] (%s:%d) ",
			"unknown header flags set",
			"header crc mismatch",
			"invalid block type",
			"invalid stored block lengths",
			"too many length or distance symbols",
			"invalid code lengths set",
			"The partition (%s) has no space (unless %d) for upgrade.",
		};

		public static readonly List<uint> KnownKeysAddresses = new List<uint>()
		{
			0x1F08, // Common Beken bootloaders, 1.0.8, 1.0.13 etc.
			0x48,   // Tuya 1.0.1 N or 1.0.5 T
		};

		public static byte[] UnCRC(byte[] bytes)
		{
			var decrc = new List<byte>();
			int i = 0;
			for(i = 0; i < bytes.Length - 34; i += 34)
			{
				var packet = new byte[32];
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
			var crc = new List<byte>();
			int i = 0;
			var packet = new byte[34];
			for(i = 0; i < bytes.Length - 32; i += 32)
			{
				Array.Copy(bytes, i, packet, 0, 32);
				var computedcrc = CRC16.Compute(CRC16Type.CMS, packet, 0, 32);
				packet[32] = (byte)(computedcrc >> 8);
				packet[33] = (byte)computedcrc;
				crc.AddRange(packet);
			}
			for(int j = 0; j < packet.Length; j++)
				packet[j] = 0xFF;
			Array.Copy(bytes, i, packet, 0, bytes.Length - i);
			var finalcrc = CRC16.Compute(CRC16Type.CMS, packet);
			packet[32] = (byte)(finalcrc >> 8);
			packet[33] = (byte)finalcrc;
			crc.AddRange(packet);
			return crc.ToArray();
		}

		public static bool VerifyDecrypt(List<uint> origWords, uint[] decryptedWords, out uint[] decrKeys, out uint address)
		{
			decrKeys = new uint[4];
			address = 0;
			foreach(var addr in KnownKeysAddresses)
			{
				address = addr;
				var keys = new uint[4];
				Array.Copy(decryptedWords, addr / 4, keys, 0, 4);
				var crypto = new BekenCrypto(keys);
				var decrypted = new List<uint>();
				uint enaddr = 0;
				for(int en = 0; en < origWords.Count; en++)
				{
					decrypted.Add(crypto.EncryptU32(enaddr, origWords[en]));
					enaddr += 4;
				}
				Array.Copy(decrypted.ToArray(), addr / 4, decrKeys, 0, 4);
				if(decrKeys.SequenceEqual(keys))
				{
					return true;
				}
				else
				{
					Array.Copy(decrypted.ToArray(), addr / 4, keys, 0, 4);
					crypto = new BekenCrypto(decrKeys);
					decrypted = new List<uint>();
					for(int en = 0; en < origWords.Count; en++)
					{
						decrypted.Add(crypto.EncryptU32(enaddr, origWords[en]));
						enaddr += 4;
					}
					Array.Copy(decrypted.ToArray(), addr / 4, decrKeys, 0, 4);
					if(decrKeys.SequenceEqual(keys))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static byte[] EncryptDecryptBekenFW(uint[] keys, byte[] data, uint startAddr = 0)
		{
			var imageU32 = U8ToU32(data);
			return EncryptDecryptBekenFW(keys, imageU32.ToArray(), startAddr);
		}

		public static byte[] EncryptDecryptBekenFW(uint[] keys, uint[] data, uint startAddr = 0)
		{
			var crypted = new List<uint>();
			var crypto = new BekenCrypto(keys);
			uint enaddr = 0;
			for(int en = 0; en < data.Length; en++)
			{
				crypted.Add(crypto.EncryptU32(startAddr + enaddr, data[en]));
				enaddr += 4;
			}
			return U32ToU8(crypted.ToArray());
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

		private static uint Pn15(uint addr)
		{
			uint a = ((addr & 0x7F) << 9) | ((addr >> 7) & 0x1FF);
			uint b = (addr >> 5) & 0xF;
			uint c = PN15_CONST & (b * 0x1111u);
			return a ^ c;
		}

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

		private static uint pn32(uint addr)
		{
			uint a = ((addr & 0x7FFF) << 17) | ((addr >> 15) & 0x1FFFF);
			uint b = (addr >> 2) & 0xF;
			uint c = PN32_CONST & (b * 0x11111111u);
			return a ^ c;
		}

		// ===============================
		// Encrypt
		// ===============================
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
	}
}
