using System.Collections.Generic;
using System.Linq;

namespace BK7231Flasher
{
    public enum RomReadKind
    {
        Rom,
        Otp,
        Efuse,
    }

    public sealed class RomReadTarget
    {
        public BKType Platform { get; private set; }
        public RomReadKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public int? Address { get; private set; }
        public int? Length { get; private set; }
        public int ReadTrailerLength { get; private set; }
        public string ReadTrailerName { get; private set; }
        public int DefaultBaudRate { get; private set; }
        public int[] AllowedBaudRates { get; private set; }
        public bool IsImplemented { get; private set; }
        public IReadOnlyList<RomReadOutputSlice> OutputSlices { get; private set; }

        public RomReadTarget(BKType platform, RomReadKind kind, string displayName,
            int? address, int? length, int defaultBaudRate, int[] allowedBaudRates, bool isImplemented,
            int readTrailerLength = 0, string readTrailerName = null, IReadOnlyList<RomReadOutputSlice> outputSlices = null)
        {
            Platform = platform;
            Kind = kind;
            DisplayName = displayName;
            Address = address;
            Length = length;
            ReadTrailerLength = readTrailerLength;
            ReadTrailerName = readTrailerName ?? "";
            DefaultBaudRate = defaultBaudRate;
            AllowedBaudRates = allowedBaudRates ?? new int[0];
            IsImplemented = isImplemented;
            OutputSlices = outputSlices ?? new RomReadOutputSlice[0];
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public int? WireReadLength
        {
            get
            {
                return Length.HasValue ? Length.Value + ReadTrailerLength : (int?)null;
            }
        }
    }

    public sealed class RomReadOutputSlice
    {
        public string DisplayName { get; private set; }
        public string FileNameTag { get; private set; }
        public int Offset { get; private set; }
        public int Length { get; private set; }

        public RomReadOutputSlice(string displayName, string fileNameTag, int offset, int length)
        {
            DisplayName = displayName;
            FileNameTag = fileNameTag;
            Offset = offset;
            Length = length;
        }
    }

    public static class RomReadCatalog
    {
        static readonly int[] CommonSerialBauds = new int[] { 9600, 115200, 230400, 460800, 921600, 1500000, 2000000, 3000000 };
        static readonly int[] XrSerialBauds = new int[] { 9600, 115200, 921600, 1000000, 1500000, 3000000 };
        const int Gd32RfEfuseSize = 0x3F;
        const int Gd32McuEfuseSize = 0x8C;

        static readonly IReadOnlyList<RomReadOutputSlice> Gd32EfuseSlices = new List<RomReadOutputSlice>()
        {
            new RomReadOutputSlice("RF eFuse", "RF_EFUSE", 0x00, Gd32RfEfuseSize),
            new RomReadOutputSlice("MCU eFuse", "MCU_EFUSE", Gd32RfEfuseSize, Gd32McuEfuseSize),
        };

        static readonly IReadOnlyList<RomReadTarget> Targets = new List<RomReadTarget>()
        {
            new RomReadTarget(BKType.BK7231N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.BK7238, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.BK7238, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true),
            // BK7231T/BK7231U do not expose ROM/eFuse reads over UART.
            // BK7236/BK7258 are omitted for now: they do not use the standard BK72xx
            // SCTRL eFuse path below, and their ROM/eFuse read flow still needs proving.
            new RomReadTarget(BKType.LN882H, RomReadKind.Rom, "ROM", 0x00000000, 0x20000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.LN882H, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, true, 2, "CRC16"),
            new RomReadTarget(BKType.LN882H, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, true, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.LN8825, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, true, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, true, 2, "CRC16"),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Rom, "ROM", 0x00000000, 0x60000, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, true),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 921600, CommonSerialBauds, true),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 921600, CommonSerialBauds, true),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Rom, "ROM", 0x0BF40000, 0x40000, 921600, CommonSerialBauds, true),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Efuse, "eFuse", 0x00000000, Gd32RfEfuseSize + Gd32McuEfuseSize, 921600, CommonSerialBauds, true, 0, null, Gd32EfuseSlices),
            new RomReadTarget(BKType.XR806, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, true),
            new RomReadTarget(BKType.XR806, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, true),
            new RomReadTarget(BKType.XR809, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 921600, XrSerialBauds, true),
            new RomReadTarget(BKType.XR809, RomReadKind.Efuse, "eFuse", 0x00000000, 0x100, 921600, XrSerialBauds, true),
            new RomReadTarget(BKType.XR872, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, true),
            new RomReadTarget(BKType.XR872, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, true),
        };

        public static IEnumerable<BKType> GetSupportedPlatforms()
        {
            return Targets.Where(target => target.IsImplemented).Select(target => target.Platform).Distinct();
        }

        public static IEnumerable<RomReadTarget> GetTargets(BKType platform)
        {
            return Targets.Where(target => target.Platform == platform);
        }

        public static RomReadTarget GetTarget(BKType platform, RomReadKind kind)
        {
            return Targets.FirstOrDefault(target => target.Platform == platform && target.Kind == kind);
        }

        public static string GetKindDisplayName(RomReadKind kind)
        {
            switch (kind)
            {
                case RomReadKind.Efuse:
                    return "eFuse";
                case RomReadKind.Otp:
                    return "OTP";
                case RomReadKind.Rom:
                    return "ROM";
                default:
                    return "Selected target";
            }
        }
    }
}
