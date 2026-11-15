// OBKEspNvsConfig.cs
// OpenBeken (OBK) config access for ESP-IDF builds (ESP32 family, ESP8266 RTOS SDK).
// OBK stores its mainConfig_t as an NVS blob: namespace "config", key "cfg"
// (see OpenBK7231T_App src/hal/espidf/hal_flashConfig_espidf.c).
// Self-contained: does not modify existing flasher code.

#nullable enable

using System;
using System.Text;

namespace BK7231Flasher
{
    /// <summary>One entry of an ESP-IDF partition table. Immutable.</summary>
    public sealed class EspPartitionEntry
    {
        public string Label { get; }
        public byte Type { get; }
        public byte SubType { get; }
        public int Offset { get; }
        public int Size { get; }

        internal EspPartitionEntry(string label, byte type, byte subType, int offset, int size)
        {
            Label = label;
            Type = type;
            SubType = subType;
            Offset = offset;
            Size = size;
        }
    }

    public sealed class EspPartitionLookupResult
    {
        public bool Success { get; }
        public string Error { get; }
        public EspPartitionEntry? Partition { get; }

        private EspPartitionLookupResult(bool success, string error, EspPartitionEntry? partition)
        {
            Success = success;
            Error = error;
            Partition = partition;
        }

        internal static EspPartitionLookupResult Ok(EspPartitionEntry p) => new EspPartitionLookupResult(true, string.Empty, p);
        internal static EspPartitionLookupResult Fail(string error) => new EspPartitionLookupResult(false, error, null);
    }

    public static class OBKEspNvsConfig
    {
        public const string NvsNamespace = "config";
        public const string NvsKey = "cfg";

        /// <summary>sizeof(mainConfig_t) as stored by OBK (MAGIC_CONFIG_SIZE_V4). Verified on ESP32.</summary>
        public const int ConfigBlobSize = 3584;

        /// <summary>
        /// Chips for which NVS-based OBK config read/write is enabled.
        /// Only ESP32 (Xtensa LX6) has been verified on hardware: NVS v2 layout, partition table,
        /// and OBK's CRC8 variant (unsigned) were all checked against a real dump.
        /// Other ESP-IDF targets most likely behave the same, but are left disabled until tested.
        /// ESP8266 (RTOS SDK) is excluded: its NVS version support was not verified.
        /// </summary>
        public static bool IsSupportedChip(BKType type)
        {
            switch (type)
            {
                case BKType.ESP32:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extracts OBK config bytes from data handed to OBKConfig.loadFrom:
        ///  - a raw config (starts with "CFG"), e.g. an exported settings file
        ///  - a full flash dump starting at 0x0 (isFullDump = true), e.g. a backup
        ///  - an NVS partition image (isFullDump = false), e.g. "Read only OBK config"
        /// Returns exactly ConfigBlobSize bytes on success.
        /// </summary>
        public static EspNvsBlobResult ExtractConfigBytes(byte[] data, bool isFullDump)
        {
            if (data == null || data.Length == 0)
                return EspNvsBlobResult.Fail("No data.");

            EspNvsBlobResult blob;
            if (data.Length >= 3 && data[0] == (byte)'C' && data[1] == (byte)'F' && data[2] == (byte)'G')
                blob = EspNvsBlobResult.Ok(data);
            else if (isFullDump)
                blob = ReadConfigFromFullDump(data);
            else
                blob = ReadConfig(data, 0, data.Length);

            if (!blob.Success)
                return blob;

            byte[] cfg = blob.GetData();
            if (cfg.Length < ConfigBlobSize)
                return EspNvsBlobResult.Fail("OBK config blob too small: " + cfg.Length + " bytes, expected " + ConfigBlobSize + ".");
            if (cfg.Length == ConfigBlobSize)
                return EspNvsBlobResult.Ok(cfg);

            var trimmed = new byte[ConfigBlobSize];
            Buffer.BlockCopy(cfg, 0, trimmed, 0, ConfigBlobSize);
            return EspNvsBlobResult.Ok(trimmed);
        }

        /// <summary>Default ESP-IDF partition table offset (CONFIG_PARTITION_TABLE_OFFSET).</summary>
        public const int DefaultPartitionTableOffset = 0x8000;
        /// <summary>Partition table area size (one sector).</summary>
        public const int PartitionTableSize = 0x1000;

        private const byte PartitionTypeData = 0x01;
        private const byte PartitionSubTypeNvs = 0x02;

        /// <summary>
        /// Finds the first data/nvs partition in an ESP-IDF partition table.
        /// tableData must contain the partition table starting at tableOffset.
        /// </summary>
        public static EspPartitionLookupResult FindNvsPartition(byte[] tableData, int tableOffset)
        {
            if (tableData == null)
                return EspPartitionLookupResult.Fail("Partition table data is null.");
            if (tableOffset < 0 || tableOffset >= tableData.Length)
                return EspPartitionLookupResult.Fail("Partition table offset out of range.");

            int end = Math.Min(tableData.Length, tableOffset + PartitionTableSize);
            for (int o = tableOffset; o + 32 <= end; o += 32)
            {
                byte m0 = tableData[o];
                byte m1 = tableData[o + 1];
                if (m0 == 0xAA && m1 == 0x50)
                {
                    byte type = tableData[o + 2];
                    byte subType = tableData[o + 3];
                    int offset = BitConverter.ToInt32(tableData, o + 4);
                    int size = BitConverter.ToInt32(tableData, o + 8);
                    int labelLen = 0;
                    while (labelLen < 16 && tableData[o + 12 + labelLen] != 0)
                        labelLen++;
                    string label = Encoding.ASCII.GetString(tableData, o + 12, labelLen);
                    if (type == PartitionTypeData && subType == PartitionSubTypeNvs)
                    {
                        if (offset <= 0 || size <= 0 || size % EspNvsPartition.PageSize != 0)
                            return EspPartitionLookupResult.Fail("NVS partition entry has invalid offset/size.");
                        return EspPartitionLookupResult.Ok(new EspPartitionEntry(label, type, subType, offset, size));
                    }
                }
                else if (m0 == 0xEB && m1 == 0xEB)
                {
                    continue;   // MD5 checksum entry
                }
                else if (m0 == 0xFF && m1 == 0xFF)
                {
                    break;      // end of table
                }
                else
                {
                    return EspPartitionLookupResult.Fail("No valid partition table at 0x" + tableOffset.ToString("X") + ".");
                }
            }
            return EspPartitionLookupResult.Fail("No NVS partition found in partition table.");
        }

        /// <summary>Extracts raw OBK config bytes (mainConfig_t) from an NVS partition image.</summary>
        public static EspNvsBlobResult ReadConfig(byte[] nvsImage, int offset, int length)
        {
            EspNvsParseResult parsed = EspNvsPartition.Parse(nvsImage, offset, length);
            if (!parsed.Success)
                return EspNvsBlobResult.Fail("NVS parse failed: " + parsed.Error);
            return EspNvsPartition.ReadBlob(parsed, NvsNamespace, NvsKey);
        }

        /// <summary>
        /// Convenience for a full-flash dump: locates NVS via the partition table at
        /// DefaultPartitionTableOffset, then extracts the OBK config.
        /// </summary>
        public static EspNvsBlobResult ReadConfigFromFullDump(byte[] flashDump)
        {
            EspPartitionLookupResult nvs = FindNvsPartition(flashDump, DefaultPartitionTableOffset);
            if (!nvs.Success || nvs.Partition == null)
                return EspNvsBlobResult.Fail(nvs.Error);
            if (nvs.Partition.Offset + nvs.Partition.Size > flashDump.Length)
                return EspNvsBlobResult.Fail("Dump too small for NVS partition.");
            return ReadConfig(flashDump, nvs.Partition.Offset, nvs.Partition.Size);
        }

        /// <summary>
        /// Builds a fresh NVS partition image containing only the OBK config blob.
        /// Writing it replaces ALL other NVS data (Wi-Fi driver data, PHY calibration, OBK flash vars).
        /// </summary>
        public static EspNvsBuildResult BuildConfigPartition(byte[] obkConfig, int partitionSize)
            => EspNvsPartition.BuildSingleBlobImage(NvsNamespace, NvsKey, obkConfig, partitionSize);
    }
}
