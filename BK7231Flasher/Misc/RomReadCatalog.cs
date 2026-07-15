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
        public string AddressSpace { get; private set; }
        public string Backend { get; private set; }
        public string Controller { get; private set; }
        public string OutputFileNameTag { get; private set; }
        public IReadOnlyList<RomReadOutputSlice> OutputSlices { get; private set; }

        public RomReadTarget(BKType platform, RomReadKind kind, string displayName,
            int? address, int? length, int defaultBaudRate, int[] allowedBaudRates,
            string addressSpace = null, string backend = null, string controller = null,
            int readTrailerLength = 0, string readTrailerName = null, IReadOnlyList<RomReadOutputSlice> outputSlices = null,
            string outputFileNameTag = null)
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
            AddressSpace = addressSpace ?? "";
            Backend = backend ?? "";
            Controller = controller ?? "";
            OutputFileNameTag = outputFileNameTag ?? "";
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
        #region Beken
        const string BekenRomSpace = "ROM memory";
        const string BekenRomBackend = "register read";
        const string BekenRomController = "direct";
        const string BekenEfuseSpace = "eFuse byte index";
        const string BekenEfuseBackend = "register R/W";
        const string BekenSctrlEfuseController = "SCTRL 0x00800074/0x00800078";
        #endregion
        #region LN882x
        const string LnRomSpace = "ROM memory";
        const string LnEfuseSpace = "eFuse shadow/current (CRC16 trailer)";
        const string LnFlashOtpSpace = "SPI flash OTP (CRC16 trailer)";
        const string LnRamcodeBackend = "custom RAMCODE command";
        const string LnRomController = "fdump with ROM flag";
        const string LnEfuseController = "efuse_dump";
        const string LnFlashOtpController = "otp_dump";
        #endregion
        #region RTL87x0C
        const string Rtlz2RomSpace = "ROM memory";
        const string Rtlz2EfuseSpace = "physical eFuse bytes";
        const string Rtlz2RomBackend = "ROM console DB";
        const string Rtlz2EfuseBackend = "ROM console EW/DB";
        const string Rtlz2RomController = "DB direct memory";
        const string Rtlz2EfuseController = "SRAM helper @ 0x10037000";
        #endregion
        #region RTL87xx
        const string RtlStubRomSpace = "ROM memory";
        const string Rtl8710bEfuseSpace = "logical eFuse map";
        const string Rtl8710bRomBackend = "RTL8710B_Stub cmd 0x98";
        const string Rtl8710bEfuseBackend = "RTL8710B_Stub cmd 0x99";
        const string RtlStubRomController = "raw CPU memory via XMODEM";
        const string Rtl8710bEfuseController = "EFUSE_LogicalMap_Read";
        const string Rtl8721daRomBackend = "RTL8721DA_Stub cmd 0x98";
        const string Rtl8721daEfuseBackend = "RTL8721DA_Stub cmd 0x99";
        const string Rtl8720eRomBackend = "RTL8720E_Stub cmd 0x98";
        const string Rtl8720eEfuseBackend = "RTL8720E_Stub cmd 0x99";
        const string RtlKm4RomSpace = "KM4 ROM memory";
        const string RtlAmebaEfuseSpace = "logical OTP map";
        const string RtlAmebaEfuseController = "OTP_LogicalMap_Read";
        #endregion
        #region ECR6600
        const string EcrRomSpace = "ROM memory";
        const string EcrRomBackend = "ECR6600_Stub_Custom cmd 0x98";
        const string EcrRomController = "raw CPU memory via XMODEM";
        const string EcrEfuseSpace = "eFuse raw image";
        const string EcrEfuseBackend = "ECR6600_Stub_Custom cmd 0x99";
        const string EcrEfuseController = "eFuse controller @ 0x0020F000";
        #endregion
        #region RDA5981
        const string RdaRomSpace = "ROM memory";
        const string RdaRomBackend = "RDA5981_Stub cmd 0x98";
        const string RdaRomController = "raw CPU memory via XMODEM";
        const string RdaEfuseSpace = "eFuse pages 0..15";
        const string RdaEfuseBackend = "RDA5981_Stub cmd 0x99";
        const string RdaEfuseController = "RF SPI @ 0x4001301C";
        #endregion
        #region GD32
        const string Gd32RomSpace = "ROM memory";
        const string Gd32RomBackend = "GD32VW553_Stub cmd 0x98";
        const string Gd32RomController = "raw CPU memory via XMODEM";
        const string Gd32EfuseSpace = "combined output: RF 0x40 + MCU 0x8C";
        const string Gd32EfuseBackend = "ROM READ, then GD32VW553_Stub cmd 0x99";
        const string Gd32EfuseController = "MCU @ 0x40022808; RF via custom stub";
        const int Gd32RfEfuseSize = 0x40;
        const int Gd32McuEfuseSize = 0x8C;

        static readonly IReadOnlyList<RomReadOutputSlice> Gd32EfuseSlices = new List<RomReadOutputSlice>()
        {
            new RomReadOutputSlice("RF eFuse", "RF_EFUSE", 0x00, Gd32RfEfuseSize),
            new RomReadOutputSlice("MCU eFuse", "MCU_EFUSE", Gd32RfEfuseSize, Gd32McuEfuseSize),
        };
        #endregion
        #region XR8xx
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
        #endregion
        #region OPL1000A2
        const string OplRomSpace = "ROM memory";
        const string OplRomBackend = "OPL1000A2_Stub cmd 0x98";
        const string OplRomController = "raw CPU memory via XMODEM";
        const string OplEfuseSpace = "OTP data";
        const string OplEfuseBackend = "OPL1000A2_Stub cmd 0x99";
        const string OplEfuseController = "Hal_Sys_OtpRead";
        #endregion

        static readonly IReadOnlyList<RomReadTarget> Targets = new List<RomReadTarget>()
        {
            new RomReadTarget(BKType.BK7231M, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231M, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenRomBackend, BekenRomController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            // BK7231T/BK7231U do not expose ROM/eFuse reads over UART.
            // BK7236/BK7258 are omitted for now: they do not use the standard BK72xx
            // SCTRL eFuse path below, and their ROM/eFuse read flow still needs proving.
            new RomReadTarget(BKType.LN882H, RomReadKind.Rom, "ROM", 0x00000000, 0x20000, 115200, CommonSerialBauds, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController, 2, "CRC16"),
            new RomReadTarget(BKType.LN882H, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, LnEfuseSpace, LnRamcodeBackend, LnEfuseController, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, LnRomSpace, LnRamcodeBackend, LnRomController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, LnFlashOtpSpace, LnRamcodeBackend, LnFlashOtpController, 2, "CRC16"),
            new RomReadTarget(BKType.LN8825, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, LnEfuseSpace, LnRamcodeBackend, LnEfuseController, 2, "CRC16"),
            new RomReadTarget(BKType.OPL1000A2, RomReadKind.Rom, "ROM", 0x00000000, 0xC0000, 115200, CommonSerialBauds, OplRomSpace, OplRomBackend, OplRomController, outputFileNameTag: "M3_ROM"),
            new RomReadTarget(BKType.OPL1000A2, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, OplEfuseSpace, OplEfuseBackend, OplEfuseController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Rom, "ROM", 0x00000000, 0x60000, 115200, CommonSerialBauds, Rtlz2RomSpace, Rtlz2RomBackend, Rtlz2RomController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, Rtlz2EfuseSpace, Rtlz2EfuseBackend, Rtlz2EfuseController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Rom, "ROM", 0x00000000, 0x80000, 115200, CommonSerialBauds, RtlStubRomSpace, Rtl8710bRomBackend, RtlStubRomController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, Rtl8710bEfuseSpace, Rtl8710bEfuseBackend, Rtl8710bEfuseController),
            // These stubs run on KM4. The KM0 (RTL8721DA) and KR4 (RTL8720E)
            // ROMs are physically private to their respective secondary cores.
            new RomReadTarget(BKType.RTL8721DA, RomReadKind.Rom, "ROM", 0x00000000, 0x80000, 115200, CommonSerialBauds, RtlKm4RomSpace, Rtl8721daRomBackend, RtlStubRomController, outputFileNameTag: "KM4_ROM"),
            new RomReadTarget(BKType.RTL8721DA, RomReadKind.Efuse, "eFuse", 0x00000000, 0x400, 115200, CommonSerialBauds, RtlAmebaEfuseSpace, Rtl8721daEfuseBackend, RtlAmebaEfuseController),
            new RomReadTarget(BKType.RTL8720E, RomReadKind.Rom, "ROM", 0x00000000, 0x48000, 115200, CommonSerialBauds, RtlKm4RomSpace, Rtl8720eRomBackend, RtlStubRomController, outputFileNameTag: "KM4_ROM"),
            new RomReadTarget(BKType.RTL8720E, RomReadKind.Efuse, "eFuse", 0x00000000, 0x400, 115200, CommonSerialBauds, RtlAmebaEfuseSpace, Rtl8720eEfuseBackend, RtlAmebaEfuseController),
            new RomReadTarget(BKType.ECR6600, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 115200, CommonSerialBauds, EcrRomSpace, EcrRomBackend, EcrRomController),
            new RomReadTarget(BKType.ECR6600, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 115200, CommonSerialBauds, EcrEfuseSpace, EcrEfuseBackend, EcrEfuseController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 921600, CommonSerialBauds, RdaRomSpace, RdaRomBackend, RdaRomController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 921600, CommonSerialBauds, RdaEfuseSpace, RdaEfuseBackend, RdaEfuseController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Rom, "ROM", 0x0BF40000, 0x40000, 921600, CommonSerialBauds, Gd32RomSpace, Gd32RomBackend, Gd32RomController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Efuse, "eFuse", 0x00000000, Gd32RfEfuseSize + Gd32McuEfuseSize, 921600, CommonSerialBauds, Gd32EfuseSpace, Gd32EfuseBackend, Gd32EfuseController, 0, null, Gd32EfuseSlices),
            new RomReadTarget(BKType.XR806, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, XrRomSpace, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR806, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, XrEfuseSpace, XrBromBackend, XrEfuseController),
            new RomReadTarget(BKType.XR809, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 921600, XrSerialBauds, Xr809RomSpace, Xr809Backend, Xr809RomController),
            new RomReadTarget(BKType.XR809, RomReadKind.Efuse, "eFuse", 0x00000000, 0x100, 921600, XrSerialBauds, Xr809EfuseSpace, Xr809Backend, Xr809EfuseController),
            new RomReadTarget(BKType.XR872, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, XrRomSpace, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR872, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, XrEfuseSpace, XrBromBackend, XrEfuseController),
        };

        public static IEnumerable<BKType> GetSupportedPlatforms()
        {
            return Targets.Select(target => target.Platform).Distinct();
        }

        public static IEnumerable<RomReadTarget> GetTargets(BKType platform)
        {
            return Targets.Where(target => target.Platform == platform);
        }

        public static RomReadTarget GetTarget(BKType platform, RomReadKind kind)
        {
            return Targets.FirstOrDefault(target => target.Platform == platform && target.Kind == kind);
        }

        public static string GetKindDisplayName(RomReadKind kind) => kind switch
        {
            RomReadKind.Efuse => "eFuse",
            RomReadKind.Otp => "OTP",
            RomReadKind.Rom => "ROM",
            _ => "Selected target",
        };
    }
}
