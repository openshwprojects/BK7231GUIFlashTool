using System;
using System.IO;
using System.Text;

namespace BK7231Flasher
{
	static class EasyFlash
	{
		internal static int EF_WRITE_GRAN = 1;

		internal static bool RequireKVHeader = false;

		internal static bool AlternateSectorHeader = false;

		private static int WriteGran => EF_WRITE_GRAN < 8 ? 1 : EF_WRITE_GRAN / 8;

		private static int SECTOR_HDR => EF_WRITE_GRAN == 32 ? 0x24 : EF_WRITE_GRAN == 8 ? 0x14 : 0x10;

		private static int SectorMagicOffset => EF_WRITE_GRAN == 32 ? 0x18 : EF_WRITE_GRAN == 8 ? 0x08 : 0x04;

		//private static int DataOff => EF_WRITE_GRAN == 32 ? 0x14 : EF_WRITE_GRAN == 8 ? 12 : 8;
		private static int DataOff => EF_WRITE_GRAN == 32 ? 28 - (RequireKVHeader ? 0 : 8) : EF_WRITE_GRAN == 8 ? 12 - (RequireKVHeader ? 0 : 4) : (RequireKVHeader ? 8 : 4);

		public static byte[] LoadValueFromData(byte[] data, string sname, int size, BKType type, out byte[] efdata)
		{
			efdata = data;
			if(data == null)
				return null;
			RequireKVHeader = true;
			AlternateSectorHeader = false;
			switch(type)
			{
				case BKType.ECR6600:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					AlternateSectorHeader = true;
					break;
				case BKType.TR6260:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					break;
				case BKType.BL602:
				case BKType.BL616:
				case BKType.BL702:
					EF_WRITE_GRAN = 8;
					break;
				default:
					EF_WRITE_GRAN = 1;
					break;
			}
			return NativeEF_Load(data, sname);
		}

		public static byte[] SaveValueToNewEasyFlash(string sname, byte[] cfgData, int areaSize, BKType type)
		{
			RequireKVHeader = true;
			AlternateSectorHeader = false;
			switch(type)
			{
				case BKType.ECR6600:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					AlternateSectorHeader = true;
					break;
				case BKType.TR6260:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					break;
				case BKType.BL602:
				case BKType.BL616:
				case BKType.BL702:
					EF_WRITE_GRAN = 8;
					break;
				default:
					EF_WRITE_GRAN = 1;
					break;
			}
			return NativeEF_Save(sname, cfgData, areaSize);
		}

		public static byte[] SaveValueToExistingEasyFlash(string sname, byte[] efData, byte[] cfgData, int areaSize, BKType type)
		{
			RequireKVHeader = true;
			AlternateSectorHeader = false;
			switch(type)
			{
				case BKType.ECR6600:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					AlternateSectorHeader = true;
					break;
				case BKType.TR6260:
					EF_WRITE_GRAN = 32;
					RequireKVHeader = false;
					break;
				case BKType.BL602:
				case BKType.BL616:
				case BKType.BL702:
					EF_WRITE_GRAN = 8;
					break;
				default:
					EF_WRITE_GRAN = 1;
					break;
			}
			// Read all existing entries, replace/insert the target key, reserialize.
			// This preserves other keys that the firmware wrote (nv_version, OBK_FV,
			// StaSSID, StaPW, PMK, etc.) rather than wiping them.
			var existing = NativeEF_ReadAllEntries(efData);
			existing[sname] = cfgData;
			return NativeEF_SaveEntries(existing, areaSize);
		}

		// ---------------------------------------------------------------------------
		// Native GRAN=32 EasyFlash implementation for TR6260.
		// The pre-built WinEF_GRAN32 DLLs write GRAN=1 format (has "KV40" magic in
		// entries) which the TR6260 firmware rejects.  These methods implement the
		// correct GRAN=32 binary layout derived from the OpenTR6260 ef_env.c source
		// and verified byte-for-byte against a live TR6260 flash dump.
		//
		// Sector layout (GRAN=32, EF_WRITE_GRAN=32, BK7231Flasher.SECTOR_SIZE=4096):
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

		private static int WgAlign(int x) => (x + (WriteGran - 1)) & ~(WriteGran - 1);

		private static uint Crc32Ieee(byte[] data, int offset, int length) => CRC.crc32_ver2(0xFFFFFFFF, data, length, (uint)offset) ^ 0xFFFFFFFF;

		/// <summary>
		/// Build a single EasyFlash entry (status_table + header + name + value).
		/// </summary>
		internal static byte[] NativeEF_MakeEntry(string key, byte[] value)
		{
			byte[] keyBytes = Encoding.ASCII.GetBytes(key);
			int nameLen = keyBytes.Length;
			int valueLen = value.Length;

			int nameSz = WgAlign(nameLen);
			int valSz = WgAlign(valueLen);

			int totalLen = DataOff + 16 + nameSz + valSz;

			byte[] entry = new byte[totalLen];
			entry.AsSpan().Fill(0xFF);

			if(EF_WRITE_GRAN == 1)
			{
				entry[0] = 0x3F; // ENV_PRE_WRITE && ENV_WRITE
			}
			else
			{
				entry[0] = 0x00; // ENV_PRE_WRITE
				entry[WriteGran] = 0x00; // ENV_WRITE
			}

			if(RequireKVHeader)
			{
				entry[SectorMagicOffset + 0] = (byte)'K';
				entry[SectorMagicOffset + 1] = (byte)'V';
				entry[SectorMagicOffset + 2] = (byte)'4';
				entry[SectorMagicOffset + 3] = (byte)'0';
			}

			MiscUtils.WriteU32LE(entry, DataOff, (uint)totalLen);

			int crcBufSize = 8 + nameSz + valSz;
			byte[] crcBuf = new byte[crcBufSize];
			crcBuf.AsSpan().Fill(0xFF);
			crcBuf[0] = (byte)nameLen;

			crcBuf[4] = (byte)(valueLen);
			crcBuf[5] = (byte)(valueLen >> 8);
			crcBuf[6] = (byte)(valueLen >> 16);
			crcBuf[7] = (byte)(valueLen >> 24);

			Array.Copy(keyBytes, 0, crcBuf, 8, nameLen);
			Array.Copy(value, 0, crcBuf, 8 + nameSz, valueLen);

			uint crc32 = Crc32Ieee(crcBuf, 0, crcBuf.Length);
			MiscUtils.WriteU32LE(entry, DataOff + 4, crc32);
			entry[DataOff + 8] = (byte)nameLen;
			MiscUtils.WriteU32LE(entry, DataOff + 12, (uint)valueLen);

			int dataStart = DataOff + 16;
			Array.Copy(keyBytes, 0, entry, dataStart, nameLen);
			Array.Copy(value, 0, entry, dataStart + nameSz, valueLen);

			return entry;
		}

		/// <summary>
		/// Build an EF sector header.
		/// isActive=true  → STORE_USING (has entries); false → STORE_EMPTY (blank formatted).
		/// </summary>
		internal static byte[] NativeEF_MakeSectorHdr(bool isActive)
		{
			byte[] h = new byte[SECTOR_HDR];
			h.AsSpan().Fill(0xFF);
			if(EF_WRITE_GRAN == 1)
			{
				if(isActive)
				{
					h[0] = 0x3F; // EMPTY && USING
					h[1] = 0x7F; // DIRTY_FALSE /*&& DIRTY_TRUE*/
				}
				else
				{
					h[0] = 0x7F; // EMPTY
					h[1] = 0x7F; // DIRTY_FALSE
				}
			}
			else
			{
				var table_size = ((4 - (EF_WRITE_GRAN == 1 ? 0 : 1)) * EF_WRITE_GRAN + 7) / 8;
				h[0] = 0x00;  // store word[0] = EMPTY written
				if(isActive)
					h[WriteGran] = 0x00;  // store word[1] = USING written
				h[table_size + 0] = 0x00;  // dirty word[0] = DIRTY_FALSE written
				//h[table_size + 1] = 0x00;  // dirty word[1] = DIRTY_TRUE written
			}
			h[SectorMagicOffset + 0] = (byte)'E';
			h[SectorMagicOffset + 1] = (byte)'F';
			h[SectorMagicOffset + 2] = (byte)'4';
			h[SectorMagicOffset + 3] = AlternateSectorHeader == false ? (byte)'0' : (byte)'1';
			return h;
		}

		/// <summary>
		/// Serialize key/value pairs into a complete EasyFlash area image.
		/// Entries are packed across as many sectors as needed; remaining sectors
		/// are written as blank-but-formatted (magic present, no entries).
		/// </summary>
		internal static byte[] NativeEF_SaveEntries(System.Collections.Generic.Dictionary<string, byte[]> kvPairs, int areaSize)
		{
			int SECTOR_AVAIL = BK7231Flasher.SECTOR_SIZE - SECTOR_HDR;

			// Pack entries into sectors, splitting when a sector would overflow.
			var sectors = new System.Collections.Generic.List<System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, byte[]>>>();
			var cur = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, byte[]>>();
			int curUsed = 0;

			foreach(var kv in kvPairs)
			{
				byte[] entry = NativeEF_MakeEntry(kv.Key, kv.Value);
				if(curUsed + entry.Length > SECTOR_AVAIL && cur.Count > 0)
				{
					sectors.Add(cur);
					cur = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, byte[]>>();
					curUsed = 0;
				}
				cur.Add(kv);
				curUsed += entry.Length;
			}
			if(cur.Count > 0) sectors.Add(cur);

			byte[] result = new byte[areaSize];
			result.AsSpan().Fill(0xFF);

			byte[] blankHdr = NativeEF_MakeSectorHdr(false);
			int nSectors = areaSize / BK7231Flasher.SECTOR_SIZE;

			for(int s = 0; s < nSectors; s++)
			{
				int base_ = s * BK7231Flasher.SECTOR_SIZE;
				if(s < sectors.Count)
				{
					byte[] hdr = NativeEF_MakeSectorHdr(true);
					Array.Copy(hdr, 0, result, base_, SECTOR_HDR);
					int pos = base_ + SECTOR_HDR;
					foreach(var kv in sectors[s])
					{
						byte[] entry = NativeEF_MakeEntry(kv.Key, kv.Value);
						Array.Copy(entry, 0, result, pos, entry.Length);
						pos += entry.Length;
					}
				}
				else
				{
					Array.Copy(blankHdr, 0, result, base_, SECTOR_HDR);
				}
			}

			return result;
		}

		/// <summary>
		/// Build a fresh EasyFlash area image containing a single key/value entry.
		/// Used when no existing EF data is available (SaveValueToNewEasyFlash path).
		/// </summary>
		internal static byte[] NativeEF_Save(string key, byte[] value, int areaSize)
		{
			var kv = new System.Collections.Generic.Dictionary<string, byte[]> { { key, value }, { "nv_version", new byte[] { 0x30, 0x2e, 0x30, 0x31 } } };
			return NativeEF_SaveEntries(kv, areaSize);
		}

		/// <summary>
		/// Read all WRITE-committed entries from an EasyFlash area.
		/// Returns a dictionary of key -> value (latest value wins for duplicate keys).
		/// </summary>
		internal static System.Collections.Generic.Dictionary<string, byte[]> NativeEF_ReadAllEntries(byte[] data)
		{
			var result = new System.Collections.Generic.Dictionary<string, byte[]>();

			for(int sectorOff = 0; sectorOff + BK7231Flasher.SECTOR_SIZE <= data.Length; sectorOff += BK7231Flasher.SECTOR_SIZE)
			{
				if(data[sectorOff + SectorMagicOffset + 0] != (byte)'E' ||
					data[sectorOff + SectorMagicOffset + 1] != (byte)'F' ||
					data[sectorOff + SectorMagicOffset + 2] != (byte)'4' ||
					(data[sectorOff + SectorMagicOffset + 3] != (byte)'0' &&
					data[sectorOff + SectorMagicOffset + 3] != (byte)'1'))
					continue;

				int pos = sectorOff + SECTOR_HDR;
				while(pos + EF_WRITE_GRAN + 4 <= sectorOff + BK7231Flasher.SECTOR_SIZE)
				{
					byte s0 = data[pos];
					if(s0 == 0xFF) break;
					var off = DataOff;
					int totalLen = (int)MiscUtils.ReadU32LE(data, pos + off);
					if(totalLen == unchecked((int)0xFFFFFFFF) || totalLen <= 0 ||
					   pos + totalLen > sectorOff + BK7231Flasher.SECTOR_SIZE)
						break;
					if(RequireKVHeader &&
						(data[pos + SectorMagicOffset + 0] != (byte)'K' ||
						data[pos + SectorMagicOffset + 1] != (byte)'V' ||
						data[pos + SectorMagicOffset + 2] != (byte)'4' ||
						data[pos + SectorMagicOffset + 3] != (byte)'0'))
					{
						pos += totalLen;
						continue;
					}
					byte s1 = data[pos + WriteGran];
					if((EF_WRITE_GRAN >= 8 && s0 == 0x00 && s1 == 0x00) || (EF_WRITE_GRAN < 8 && (s0 == 0x3F || s0 == 0xBF)))
					{
						int nameLen  = data[pos + off + 8];
						int valueLen = (int)MiscUtils.ReadU32LE(data, pos + off + 12);
						int nameSz   = WgAlign(nameLen);
						var crcRead = MiscUtils.ReadU32LE(data, pos + off + 4);
						var crcEf = Crc32Ieee(data, pos + off + 8, nameSz + valueLen + 8);
						if(crcRead != crcEf)
						{
							Console.WriteLine($"Bad CRC32 for EF entry! Skipping...");
							pos += totalLen;
							continue;
						}
						int valStart = pos + off + 16 + nameSz;
						if(nameLen > 0 && valStart + valueLen <= sectorOff + BK7231Flasher.SECTOR_SIZE)
						{
							string name = Encoding.ASCII.GetString(data, pos + off + 16, nameLen);
							byte[] val  = new byte[valueLen];
							Array.Copy(data, valStart, val, 0, valueLen);
							result[name] = val;  // latest write wins
						}
					}

					pos += totalLen;
				}
			}

			return result;
		}

		/// <summary>
		/// Scan an EasyFlash area image and return the most recent value
		/// stored under 'key', or null if not found.
		/// </summary>
		internal static byte[] NativeEF_Load(byte[] data, string key)
		{
			var all = NativeEF_ReadAllEntries(data);
			return all.TryGetValue(key, out var val) ? val : null;
		}
	}
}
