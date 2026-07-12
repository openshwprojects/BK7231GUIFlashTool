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
        public string AddressSpace { get; private set; }
        public string Backend { get; private set; }
        public string Controller { get; private set; }
        public IReadOnlyList<RomReadOutputSlice> OutputSlices { get; private set; }

        public RomReadTarget(BKType platform, RomReadKind kind, string displayName,
            int? address, int? length, int defaultBaudRate, int[] allowedBaudRates, bool isImplemented,
            string addressSpace = null, string backend = null, string controller = null,
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
            AddressSpace = addressSpace ?? "";
            Backend = backend ?? "";
            Controller = controller ?? "";
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
        const int Gd32RfEfuseSize = 0x40;
        const int Gd32McuEfuseSize = 0x8C;

        static readonly IReadOnlyList<RomReadOutputSlice> Gd32EfuseSlices = new List<RomReadOutputSlice>()
        {
            new RomReadOutputSlice("RF eFuse", "RF_EFUSE", 0x00, Gd32RfEfuseSize),
            new RomReadOutputSlice("MCU eFuse", "MCU_EFUSE", Gd32RfEfuseSize, Gd32McuEfuseSize),
        };

        const string BekenRomSpace = "ROM memory";
        const string BekenRomBackend = "register read";
        const string BekenRomController = "direct";
        const string BekenEfuseSpace = "eFuse byte index";
        const string BekenEfuseBackend = "register R/W";
        const string BekenSctrlEfuseController = "SCTRL 0x00800074/0x00800078";
        const string LnRomSpace = "ROM memory";
        const string LnEfuseSpace = "eFuse shadow/current + CRC16";
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
        const string Rtl8710bRomSpace = "ROM memory";
        const string Rtl8710bEfuseSpace = "logical eFuse map";
        const string Rtl8710bRomBackend = "RTL8710B_Stub cmd 0x98";
        const string Rtl8710bEfuseBackend = "RTL8710B_Stub cmd 0x99";
        const string Rtl8710bRomController = "raw CPU memory via XMODEM";
        const string Rtl8710bEfuseController = "EFUSE_LogicalMap_Read";
        const string RdaRomSpace = "ROM memory";
        const string RdaRomBackend = "RDA5981_Stub cmd 0x98";
        const string RdaRomController = "raw CPU memory via XMODEM";
        const string RdaEfuseSpace = "eFuse pages 0..15";
        const string RdaEfuseBackend = "RDA5981_Stub cmd 0x99";
        const string RdaEfuseController = "RF SPI @ 0x4001301C";
        const string Gd32RomSpace = "ROM memory";
        const string Gd32RomBackend = "GD32VW553_Stub cmd 0x98";
        const string Gd32RomController = "raw CPU memory via XMODEM";
        const string Gd32EfuseSpace = "eFuse payload";
        const string Gd32EfuseBackend = "GD32VW553_Stub cmd 0x99";
        const string Gd32EfuseController = "custom stub";
        const string Xr809RomSpace = "ROM memory";
        const string Xr809EfuseSpace = "eFuse raw image";
        const string Xr809Backend = "XRadio BROM/stub command";
        const string Xr809RomController = "BROM/stub cmd 0x08";
        const string Xr809EfuseController = "eFuse regs 0x40043C40/0x40043C60";
        const string XrRomSpace = "ROM memory";
        const string XrEfuseSpace = "eFuse raw image";
        const string XrBromBackend = "XRadio BROM command";
        const string XrRomController = "BROM cmd 0x08";
        const string XrEfuseController = "eFuse regs 0x40043C40/0x40043C60";

        static readonly IReadOnlyList<RomReadTarget> Targets = new List<RomReadTarget>()
        {
            new RomReadTarget(BKType.BK7231N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, true, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            // BK7231T/BK7231U do not expose ROM/eFuse reads over UART.
            // BK7236/BK7258 are omitted for now: they do not use the standard BK72xx
            // SCTRL eFuse path below, and their ROM/eFuse read flow still needs proving.
            new RomReadTarget(BKType.LN882H, RomReadKind.Rom, "ROM", 0x00000000, 0x20000, 115200, CommonSerialBauds, true, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, true, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController, 2, "CRC16"),
            new RomReadTarget(BKType.LN882H, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, true, LnEfuseSpace, LnRamcodeBackend, LnEfuseController, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, true, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, true, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, true, LnEfuseSpace, LnRamcodeBackend, LnEfuseController, 2, "CRC16"),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Rom, "ROM", 0x00000000, 0x60000, 115200, CommonSerialBauds, true, Rtlz2RomSpace, Rtlz2Backend, Rtlz2RomController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, true, Rtlz2EfuseSpace, Rtlz2Backend, Rtlz2EfuseController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Rom, "ROM", 0x00000000, 0x80000, 115200, CommonSerialBauds, true, Rtl8710bRomSpace, Rtl8710bRomBackend, Rtl8710bRomController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, true, Rtl8710bEfuseSpace, Rtl8710bEfuseBackend, Rtl8710bEfuseController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 921600, CommonSerialBauds, true, RdaRomSpace, RdaRomBackend, RdaRomController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 921600, CommonSerialBauds, true, RdaEfuseSpace, RdaEfuseBackend, RdaEfuseController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Rom, "ROM", 0x0BF40000, 0x40000, 921600, CommonSerialBauds, true, Gd32RomSpace, Gd32RomBackend, Gd32RomController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Efuse, "eFuse", 0x00000000, Gd32RfEfuseSize + Gd32McuEfuseSize, 921600, CommonSerialBauds, true, Gd32EfuseSpace, Gd32EfuseBackend, Gd32EfuseController, 0, null, Gd32EfuseSlices),
            new RomReadTarget(BKType.XR806, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, true, XrRomSpace, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR806, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, true, XrEfuseSpace, XrBromBackend, XrEfuseController),
            new RomReadTarget(BKType.XR809, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 921600, XrSerialBauds, true, Xr809RomSpace, Xr809Backend, Xr809RomController),
            new RomReadTarget(BKType.XR809, RomReadKind.Efuse, "eFuse", 0x00000000, 0x100, 921600, XrSerialBauds, true, Xr809EfuseSpace, Xr809Backend, Xr809EfuseController),
            new RomReadTarget(BKType.XR872, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, true, XrRomSpace, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR872, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, true, XrEfuseSpace, XrBromBackend, XrEfuseController),
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
