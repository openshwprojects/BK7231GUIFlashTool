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
        public int DefaultBaudRate { get; private set; }
        public int[] AllowedBaudRates { get; private set; }
        public bool IsImplemented { get; private set; }
        public string AddressSpace { get; private set; }
        public string Backend { get; private set; }
        public string Controller { get; private set; }

        public RomReadTarget(BKType platform, RomReadKind kind, string displayName,
            int? address, int? length, int defaultBaudRate, int[] allowedBaudRates, bool isImplemented,
            string addressSpace = null, string backend = null, string controller = null)
        {
            Platform = platform;
            Kind = kind;
            DisplayName = displayName;
            Address = address;
            Length = length;
            DefaultBaudRate = defaultBaudRate;
            AllowedBaudRates = allowedBaudRates ?? new int[0];
            IsImplemented = isImplemented;
            AddressSpace = addressSpace ?? "";
            Backend = backend ?? "";
            Controller = controller ?? "";
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class RomReadCatalog
    {
        static readonly int[] CommonSerialBauds = new int[] { 9600, 115200, 230400, 460800, 921600, 1500000, 2000000, 3000000 };
        const string BekenRomSpace = "ROM memory";
        const string BekenRomBackend = "register read";
        const string BekenRomController = "direct";
        const string BekenEfuseSpace = "eFuse byte index";
        const string BekenEfuseBackend = "register R/W";
        const string BekenSctrlEfuseController = "SCTRL 0x00800074/0x00800078";
        const string BekenArminoEfuseController = "EFUSE 0x44880010/0x44880014";
        const string LnRomSpace = "ROM memory";
        const string LnEfuseSpace = "eFuse dump + CRC16";
        const string LnFlashOtpSpace = "SPI flash OTP + CRC16";
        const string LnRamcodeBackend = "custom RAMCODE command";
        const string LnRomController = "fdump with ROM flag";
        const string LnEfuseController = "efuse_dump";
        const string LnFlashOtpController = "otp_dump";
        const string Rtlz2RomSpace = "ROM memory";
        const string Rtlz2EfuseSpace = "physical eFuse bytes";
        const string Rtlz2Backend = "ROM console DB/EW";
        const string Rtlz2RomController = "DB direct memory";
        const string Rtlz2EfuseController = "SRAM helper @ 0x10037000";

        static readonly IReadOnlyList<RomReadTarget> Targets = new List<RomReadTarget>()
        {
            new RomReadTarget(BKType.BK7231N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7231T, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231T, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7231U, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231U, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7236, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenArminoEfuseController),
            // BK7258 ROM probe returned a blank dump; keep disabled until the address/protocol is proven.
            new RomReadTarget(BKType.BK7258, RomReadKind.Rom, "ROM probe", 0x06000000, 0x1000, 115200, CommonSerialBauds, false, BekenRomSpace, BekenRomBackend, "needs proof"),
            new RomReadTarget(BKType.BK7258, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenArminoEfuseController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Rom, "ROM", 0x00000000, 0x20000, 115200, CommonSerialBauds, true, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x402, 115200, CommonSerialBauds, true, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Efuse, "eFuse", 0x00000000, 0x12, 115200, CommonSerialBauds, true, LnEfuseSpace, LnRamcodeBackend, LnEfuseController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x402, 115200, CommonSerialBauds, true, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Efuse, "eFuse", 0x00000000, 0x12, 115200, CommonSerialBauds, true, LnEfuseSpace, LnRamcodeBackend, LnEfuseController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Rom, "ROM", 0x00000000, 0x60000, 115200, CommonSerialBauds, true, Rtlz2RomSpace, Rtlz2Backend, Rtlz2RomController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, true, Rtlz2EfuseSpace, Rtlz2Backend, Rtlz2EfuseController),
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
