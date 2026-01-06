using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static BK7231Flasher.Misc.BL602FlashList;

namespace BK7231Flasher
{
	internal class BL602Utils
	{
		#region Partitions

		public static List<PartitionEntry> Partitions_2MB = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x10000,
				Length0 = 0x102000,
				Address1 = 0x112000,
				Length1 = 0x90000,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0x1A2000,
				Length0 = 0x47000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0x1E9000,
				Length0 = 0x13000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 7,
				TypeFlag = 0,
				Name = "factory",
				Address0 = 0x1FC000,
				Length0 = 0x4000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		public static List<PartitionEntry> Partitions_1MB = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x10000,
				Length0 = 0xDC000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0xEC000,
				Length0 = 0xB000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0xF7000,
				Length0 = 0x5000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 7,
				TypeFlag = 0,
				Name = "factory",
				Address0 = 0xFC000,
				Length0 = 0x4000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		public static List<PartitionEntry> Partitions_4MB = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x10000,
				Length0 = 0x120000,
				Address1 = 0x130000,
				Length1 = 0xB9000,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0x200000,
				Length0 = 0x200000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0x1E9000,
				Length0 = 0x13000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 7,
				TypeFlag = 0,
				Name = "factory",
				Address0 = 0x1FC000,
				Length0 = 0x4000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		public static List<PartitionEntry> Partitions_512K_BL702 = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x3000,
				Length0 = 0x70000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0x73000,
				Length0 = 0x9000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0x7A000,
				Length0 = 0x5000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		public static List<PartitionEntry> Partitions_1MB_BL702 = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x3000,
				Length0 = 0xDC000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0xDF000,
				Length0 = 0x18000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0xF7000,
				Length0 = 0x9000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		public static List<PartitionEntry> Partitions_2MB_BL702 = new List<PartitionEntry>()
		{
			new PartitionEntry()
			{
				PartitionType = 0,
				TypeFlag = 0,
				Name = "FW",
				Address0 = 0x3000,
				Length0 = 0x10A000,
				Address1 = 0x10D000,
				Length1 = 0x95000,
			},
			new PartitionEntry()
			{
				PartitionType = 3,
				TypeFlag = 0,
				Name = "media",
				Address0 = 0x1A2000,
				Length0 = 0x47000,
				Address1 = 0,
				Length1 = 0,
			},
			new PartitionEntry()
			{
				PartitionType = 4,
				TypeFlag = 0,
				Name = "PSM",
				Address0 = 0x1E9000,
				Length0 = 0x13000,
				Address1 = 0,
				Length1 = 0,
			},
		};

		#endregion

		public const uint PartitionMagicCode = 0x54504642; // "BFPT" little-endian
		public const int HeaderSize = 16;
		public const int EntrySize = 36;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct PartitionTableHeader
		{
			public uint Magic;
			public ushort Reserved;
			public ushort Count;
			public uint Age;
			public uint HeaderCRC;
		}

		public class PartitionEntry
		{
			public byte PartitionType { get; set; }
			public byte TypeFlag { get; set; } // active slot, 0 or 1
			public string Name { get; set; }

			public uint Address0 { get; set; }
			public uint Address1 { get; set; }

			public uint Length0 { get; set; }
			public uint Length1 { get; set; }

			//public uint ActiveAddress =>
			//	TypeFlag == 0 ? Address0 : Address1;

			//public uint ActiveMaxLength =>
			//	TypeFlag == 0 ? Length0 : Length1;
		}

		public static byte[] PT_Build(List<PartitionEntry> entries)
		{
			if(entries.Count > 16)
				throw new ArgumentException("Maximum 16 entries allowed");

			var header = new PartitionTableHeader
			{
				Magic = PartitionMagicCode,
				Reserved = 0,
				Count = (ushort)entries.Count,
				Age = 0,
				HeaderCRC = 0
			};

			using var ms = new MemoryStream();

			ms.Write(Utils.ToBytes(header), 0, HeaderSize);

			foreach(var e in entries)
			{
				var entry = new byte[EntrySize];

				entry[0] = e.PartitionType;
				entry[2] = e.TypeFlag;

				var nameBytes = Encoding.ASCII.GetBytes(e.Name);
				// max 8 bytes for a name
				Array.Copy(nameBytes, 0, entry, 3, Math.Min(8, nameBytes.Length));

				Array.Copy(BitConverter.GetBytes(e.Address0), 0, entry, 12, 4);
				Array.Copy(BitConverter.GetBytes(e.Address1), 0, entry, 16, 4);
				Array.Copy(BitConverter.GetBytes(e.Length0), 0, entry, 20, 4);
				Array.Copy(BitConverter.GetBytes(e.Length1), 0, entry, 24, 4);

				ms.Write(entry, 0, EntrySize);
			}

			// entries crc32
			ms.Write(BitConverter.GetBytes(CRC.crc32_ver2(0xFFFFFFFF, ms.GetBuffer(), entries.Count * EntrySize, HeaderSize) ^ 0xFFFFFFFF), 0, 4);

			// header crc32
			var result = ms.GetBuffer();
			var headerCrc = BitConverter.GetBytes(CRC.crc32_ver2(0xFFFFFFFF, result, 12, 0) ^ 0xFFFFFFFF);
			Array.Copy(headerCrc, 0, result, 12, 4);

			var pTable = MiscUtils.padArray(new byte[1], 0x2000);
			Array.Copy(result, 0, pTable, 0, result.Length);
			Array.Copy(result, 0, pTable, 0x1000, result.Length);

			return pTable;
		}

		public static List<PartitionEntry> PT_Parse(byte[] data)
		{
			var header = Utils.FromBytes<PartitionTableHeader>(data);

			if(header.Magic != PartitionMagicCode)
				throw new InvalidDataException("Bad partition magic");

			if(!BitConverter.GetBytes(CRC.crc32_ver2(0xFFFFFFFF, data, 12, 0) ^ 0xFFFFFFFF)
				.SequenceEqual(data.Skip(12).Take(4).ToArray()))
				throw new InvalidDataException("Header CRC mismatch");

			var entriesLength = header.Count * EntrySize;

			if(!BitConverter.GetBytes(CRC.crc32_ver2(0xFFFFFFFF, data, entriesLength, HeaderSize) ^ 0xFFFFFFFF)
				.SequenceEqual(data.Skip(HeaderSize + entriesLength).Take(4)))
				throw new InvalidDataException("Entries CRC mismatch");

			var list = new List<PartitionEntry>();

			for(var i = 0; i < header.Count; i++)
			{
				var baseOffset = HeaderSize + i * EntrySize;
				var entry = new byte[EntrySize];
				Array.Copy(data, baseOffset, entry, 0, EntrySize);

				var typeFlag = entry[2];

				var pe = new PartitionEntry
				{
					PartitionType = entry[0],
					TypeFlag = typeFlag,
					Name = Encoding.ASCII.GetString(entry, 3, 8).TrimEnd('\0'),
					Address0 = BitConverter.ToUInt32(entry, 12),
					Address1 = BitConverter.ToUInt32(entry, 16),
					Length0 = BitConverter.ToUInt32(entry, 20),
					Length1 = BitConverter.ToUInt32(entry, 24)
				};

				list.Add(pe);
			}

			return list;
		}

		public static byte[] CreateBootHeader(FlashConfig flashConfig, byte[] firmware, BKType chipType)
		{
			byte[] header = new byte[176];

			void WriteBits(uint value, int offset, int pos, int bitlen)
			{
				if(bitlen == 32)
				{
					byte[] bytes = BitConverter.GetBytes(value);
					Array.Copy(bytes, 0, header, offset, 4);
					return;
				}

				uint oldVal = BitConverter.ToUInt32(header, offset);

				uint mask = ((1u << bitlen) - 1u) << pos;
				uint newVal = (oldVal & ~mask) | ((value << pos) & mask);

				byte[] outBytes = BitConverter.GetBytes(newVal);
				Array.Copy(outBytes, 0, header, offset, 4);
			}

			WriteBits(0x504E4642, 0, 0, 32);
			WriteBits(1, 4, 0, 32);

			// flash cfg
			WriteBits(0x47464346, 8, 0, 32);
			WriteBits(flashConfig.io_mode, 12, 0, 8);
			WriteBits(flashConfig.cont_read_support, 12, 8, 8);
			if(chipType == BKType.BL602)
			{
				WriteBits(1, 12, 16, 8); // sfctrl_clk_delay 1 for 80M flash
				WriteBits(1, 12, 24, 8); // sfctrl_clk_invert
			}
			else if(chipType == BKType.BL702)
			{
				WriteBits(0, 12, 16, 8); // sfctrl_clk_delay
				WriteBits(3, 12, 24, 8); // sfctrl_clk_invert
			}
			WriteBits(flashConfig.reset_en_cmd, 16, 0, 8);
			WriteBits(flashConfig.reset_cmd, 16, 8, 8);
			WriteBits(flashConfig.exit_contread_cmd, 16, 16, 8);
			WriteBits(flashConfig.exit_contread_cmd_size, 16, 24, 8);
			WriteBits(flashConfig.jedecid_cmd, 20, 0, 8);
			WriteBits(flashConfig.jedecid_cmd_dmy_clk, 20, 8, 8);
			WriteBits(flashConfig.qpi_jedecid_cmd, 20, 16, 8);
			WriteBits(flashConfig.qpi_jedecid_dmy_clk, 20, 24, 8);
			WriteBits(flashConfig.sector_size, 24, 0, 8);
			WriteBits(flashConfig.mfg_id, 24, 8, 8);
			WriteBits(flashConfig.page_size, 24, 16, 16);
			WriteBits(flashConfig.chip_erase_cmd, 28, 0, 8);
			WriteBits(flashConfig.sector_erase_cmd, 28, 8, 8);
			WriteBits(flashConfig.blk32k_erase_cmd, 28, 16, 8);
			WriteBits(flashConfig.blk64k_erase_cmd, 28, 24, 8);
			WriteBits(flashConfig.write_enable_cmd, 32, 0, 8);
			WriteBits(flashConfig.page_prog_cmd, 32, 8, 8);
			WriteBits(flashConfig.qpage_prog_cmd, 32, 16, 8);
			WriteBits(flashConfig.qual_page_prog_addr_mode, 32, 24, 8);
			WriteBits(flashConfig.fast_read_cmd, 36, 0, 8);
			WriteBits(flashConfig.fast_read_dmy_clk, 36, 8, 8);
			WriteBits(flashConfig.qpi_fast_read_cmd, 36, 16, 8);
			WriteBits(flashConfig.qpi_fast_read_dmy_clk, 36, 24, 8);
			WriteBits(flashConfig.fast_read_do_cmd, 40, 0, 8);
			WriteBits(flashConfig.fast_read_do_dmy_clk, 40, 8, 8);
			WriteBits(flashConfig.fast_read_dio_cmd, 40, 16, 8);
			WriteBits(flashConfig.fast_read_dio_dmy_clk, 40, 24, 8);
			WriteBits(flashConfig.fast_read_qo_cmd, 44, 0, 8);
			WriteBits(flashConfig.fast_read_qo_dmy_clk, 44, 8, 8);
			WriteBits(flashConfig.fast_read_qio_cmd, 44, 16, 8);
			WriteBits(flashConfig.fast_read_qio_dmy_clk, 44, 24, 8);
			WriteBits(flashConfig.qpi_fast_read_qio_cmd, 48, 0, 8);
			WriteBits(flashConfig.qpi_fast_read_qio_dmy_clk, 48, 8, 8);
			WriteBits(flashConfig.qpi_page_prog_cmd, 48, 16, 8);
			WriteBits(flashConfig.write_vreg_enable_cmd, 48, 24, 8);
			WriteBits(flashConfig.wel_reg_index, 52, 0, 8);
			WriteBits(flashConfig.qe_reg_index, 52, 8, 8);
			WriteBits(flashConfig.busy_reg_index, 52, 16, 8);
			WriteBits(flashConfig.wel_bit_pos, 52, 24, 8);
			WriteBits(flashConfig.qe_bit_pos, 56, 0, 8);
			WriteBits(flashConfig.busy_bit_pos, 56, 8, 8);
			WriteBits(flashConfig.wel_reg_write_len, 56, 16, 8);
			WriteBits(flashConfig.wel_reg_read_len, 56, 24, 8);
			WriteBits(flashConfig.qe_reg_write_len, 60, 0, 8);
			WriteBits(flashConfig.qe_reg_read_len, 60, 8, 8);
			WriteBits(flashConfig.release_power_down, 60, 16, 8);
			WriteBits(flashConfig.busy_reg_read_len, 60, 24, 8);
			WriteBits(flashConfig.reg_read_cmd0, 64, 0, 8);
			WriteBits(flashConfig.reg_read_cmd1, 64, 8, 8);
			WriteBits(flashConfig.reg_write_cmd0, 68, 0, 8);
			WriteBits(flashConfig.reg_write_cmd1, 68, 8, 8);
			WriteBits(flashConfig.enter_qpi_cmd, 72, 0, 8);
			WriteBits(flashConfig.exit_qpi_cmd, 72, 8, 8);
			WriteBits(flashConfig.cont_read_code, 72, 16, 8);
			WriteBits(flashConfig.cont_read_exit_code, 72, 24, 8);
			WriteBits(flashConfig.burst_wrap_cmd, 76, 0, 8);
			WriteBits(flashConfig.burst_wrap_dmy_clk, 76, 8, 8);
			WriteBits(flashConfig.burst_wrap_data_mode, 76, 16, 8);
			WriteBits(flashConfig.burst_wrap_code, 76, 24, 8);
			WriteBits(flashConfig.de_burst_wrap_cmd, 80, 0, 8);
			WriteBits(flashConfig.de_burst_wrap_cmd_dmy_clk, 80, 8, 8);
			WriteBits(flashConfig.de_burst_wrap_code_mode, 80, 16, 8);
			WriteBits(flashConfig.de_burst_wrap_code, 80, 24, 8);
			WriteBits(flashConfig.sector_erase_time, 84, 0, 16);
			WriteBits(flashConfig.blk32k_erase_time, 84, 16, 16);
			WriteBits(flashConfig.blk64k_erase_time, 88, 0, 16);
			WriteBits(flashConfig.page_prog_time, 88, 16, 16);
			WriteBits(flashConfig.chip_erase_time & 0xFFFF, 92, 0, 16);
			WriteBits(flashConfig.power_down_delay, 92, 16, 8);
			WriteBits(flashConfig.qe_data, 92, 24, 8);

			WriteBits(CRC.crc32_ver2(0xFFFFFFFF, header, 84, 12) ^ 0xFFFFFFFF, 96, 0, 32); // flashcfg_crc32

			// clk cfg
			WriteBits(0x47464350, 100, 0, 32); // clkcfg_magic_code
			if(chipType == BKType.BL602)
			{
				WriteBits(4, 104, 0, 8); // xtal_type ["None", "24M", "32M", "38.4M", "40M", "26M", "RC32M"]
				WriteBits(4, 104, 8, 8); // pll_clk ["RC32M", "XTAL", "48M", "120M", "160M", "192M"]
				WriteBits(0, 104, 16, 8); // hclk_div
				WriteBits(1, 104, 24, 8); // bclk_div
				WriteBits(3, 108, 0, 8); // flash_clk_type ["120M", "XTAL", "48M", "80M", "BCLK", "96M", "Manual"]
				WriteBits(1, 108, 8, 8); // flash_clk_div
			}
			else if(chipType == BKType.BL702)
			{
				WriteBits(1, 104, 0, 8); // xtal_type ["None", "32M", "RC32M"]
				WriteBits(1, 104, 8, 8); // pll_clk ["RC32M", "XTAL", "57P6M", "96M", "144M"]
				WriteBits(0, 104, 16, 8); // hclk_div
				WriteBits(0, 104, 24, 8); // bclk_div
				WriteBits(1, 108, 0, 8); // flash_clk_type ["144M", "XCLK", "57P6M", "72M", "BCLK", "96M", "Manual"]
				WriteBits(0, 108, 8, 8); // flash_clk_div
				//WriteBits(1, 104, 0, 8); // xtal_type ["None", "32M", "RC32M"]
				//WriteBits(4, 104, 8, 8); // pll_clk ["RC32M", "XTAL", "57P6M", "96M", "144M"]
				//WriteBits(0, 104, 16, 8); // hclk_div
				//WriteBits(1, 104, 24, 8); // bclk_div
				//WriteBits(3, 108, 0, 8); // flash_clk_type ["144M", "XCLK", "57P6M", "72M", "BCLK", "96M", "Manual"]
				//WriteBits(0, 108, 8, 8); // flash_clk_div
			}
			WriteBits(CRC.crc32_ver2(0xFFFFFFFF, header, 8, 104) ^ 0xFFFFFFFF, 112, 0, 32); // clkcfg_crc32

			// bootcfg
			WriteBits(0, 116, 0, 2); // sign
			WriteBits(0, 116, 2, 2); // encrypt_type
			WriteBits(0, 116, 4, 2); // key_sel
			WriteBits(1, 116, 8, 1); // no_segment
			WriteBits(1, 116, 9, 1); // cache_enable
			WriteBits(0, 116, 10, 1); // notload_in_bootrom
			WriteBits(0, 116, 11, 1); // aes_region_lock
			WriteBits(3, 116, 12, 4); // cache_way_disable
			WriteBits(0, 116, 16, 1); // crc_ignore
			WriteBits(0, 116, 17, 1); // hash_ignore
			if(chipType == BKType.BL702)
			{
				WriteBits(1, 116, 19, 1); // boot2_enable
				WriteBits(0, 116, 20, 1); // boot2_rollback
			}

			WriteBits((uint)firmware.Length, 120, 0, 32); // img_len
			WriteBits(0, 124, 0, 32); // bootentry
			WriteBits(0x1000, 128, 0, 32); // img_start
			if(chipType == BKType.BL702)
			{
				WriteBits(0x1000, 164, 0, 32); // boot2_pt_table_0
				WriteBits(0x2000, 168, 0, 32); // boot2_pt_table_1
			}

			using SHA256 sha256 = SHA256.Create();
			var sha = sha256.ComputeHash(firmware);
			Array.Copy(sha, 0, header, 132, 32);
			
			WriteBits(CRC.crc32_ver2(0xFFFFFFFF, header, 172) ^ 0xFFFFFFFF, 172, 0, 32); // crc32

			return header;
		}
	}
}
