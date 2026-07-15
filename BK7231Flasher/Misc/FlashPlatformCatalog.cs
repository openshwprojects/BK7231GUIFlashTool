using System;
using System.Collections.Generic;
using System.Linq;

namespace BK7231Flasher
{
    public static class FlashPlatformCatalog
    {
        public static readonly IReadOnlyDictionary<BKType, string> Chips = new Dictionary<BKType, string>()
        {
            { BKType.BK7231M,    "BK7231M" },
            { BKType.BK7231N,    "BK7231N (T2, T34)" },
            { BKType.BK7231T,    "BK7231T" },
            { BKType.BK7231U,    "BK7231U" },
            { BKType.BK7236,     "BK7236 (T3)" },
            { BKType.BK7238,     "BK7238 (T1)" },
            { BKType.BK7252,     "BK7252" },
            { BKType.BK7252N,    "BK7252N (T4)" },
            { BKType.BK7258,     "BK7258 (T5)" },
            { BKType.BekenSPI,   "Beken SPI CH341" },
            { BKType.BL602,      "BL602" },
            { BKType.BL616,      "BL616" },
            { BKType.BL702,      "BL702" },
            { BKType.ECR6600,    "ECR6600" },
            { BKType.ESP32,      "ESP32" },
            { BKType.ESP32S2,    "ESP32-S2" },
            { BKType.ESP32C2,    "ESP32-C2" },
            { BKType.ESP32C3,    "ESP32-C3" },
            { BKType.ESP32C5,    "ESP32-C5" },
            { BKType.ESP32C6,    "ESP32-C6" },
            { BKType.ESP32C61,   "ESP32-C61" },
            { BKType.ESP32S3,    "ESP32-S3" },
            { BKType.ESP8266,    "ESP8266" },
            { BKType.GD32VW553,  "GD32VW553" },
            { BKType.GenericSPI, "Generic SPI CH341" },
            { BKType.LN882H,     "LN882H" },
            { BKType.LN8825,     "LN8825" },
            { BKType.RDA5981,    "RDA5981" },
            { BKType.OPL1000A2,  "OPL1000A2" },
            { BKType.RTL8710B,   "RTL8710B (AmebaZ)" },
            { BKType.RTL8720D,   "RTL8720DN (AmebaD)" },
            { BKType.RTL8721DA,  "RTL8721DA (AmebaDplus)" },
            { BKType.RTL8720E,   "RTL8720E (AmebaLite)" },
            { BKType.RTL87X0C,   "RTL87X0C (AmebaZ2)" },
            { BKType.TR6260,     "TR6260" },
            { BKType.W600,       "W600 (write)" },
            { BKType.W800,       "W800" },
            { BKType.XR806,      "XR806" },
            { BKType.XR809,      "XR809" },
            { BKType.XR872,      "XR872 (XF16)" },
        };

        public static IEnumerable<ChipType> GetOrderedChipTypes()
        {
            return Chips
                .OrderBy(chip => chip.Value, StringComparer.OrdinalIgnoreCase)
                .Select(chip => new ChipType(chip.Key, chip.Value));
        }

        public static bool UsesSerialPort(BKType type)
        {
            return type != BKType.BekenSPI && type != BKType.GenericSPI;
        }

        public static string GetDisplayName(BKType type)
        {
            string displayName;
            if (Chips.TryGetValue(type, out displayName))
            {
                return displayName;
            }
            return type.ToString();
        }
    }
}
