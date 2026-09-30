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
        static readonly int[] CommonSerialBauds = new int[] { 9600, 115200, 230400, 460800, 921600, 1500000, 2000000 };
        static readonly int[] XrSerialBauds = new int[] { 9600, 115200, 921600, 1000000, 1500000, 3000000 };
        #region Beken
        const string BekenRomSpace = "ROM memory";
        const string BekenReadRegBackend = "register read";
        const string BekenRomController = "direct";
        const string BekenNonSecureRomSpace = "non-secure ROM alias";
        const string BekenEfuseSpace = "eFuse byte index";
        const string BekenEfuseBackend = "register R/W";
        const string BekenSctrlEfuseController = "SCTRL 0x00800074/0x00800078";
        const string FourByteBekenEfuseSpace = "eFuse bytes 0x00..0x03";
        const string Bk7258EfuseController = "EFUSE CTRL/OPTR 0x54880010/0x54880014";
        const string Bk7239NEfuseController = "OTP1 eFuse field 0x5B10043C";
        const int FourByteBekenEfuseSize = 0x04;
        const string BekenOtpApbAhbSpace = "combined output: OTP1 APB 0x400 + OTP2 AHB 0xC00";
        const string BekenOtpApbAhbWindows = "OTP1 APB data 0x5B100400; OTP2 AHB data 0x5B010000";
        const int BekenOtp1ApbDataSize = 0x400;
        const int BekenOtp2AhbDataSize = 0xC00;

        static readonly IReadOnlyList<RomReadOutputSlice> BekenOtpApbAhbSlices = new List<RomReadOutputSlice>()
        {
            new RomReadOutputSlice("OTP1 APB", "OTP1_APB", 0x00, BekenOtp1ApbDataSize),
            new RomReadOutputSlice("OTP2 AHB", "OTP2_AHB", BekenOtp1ApbDataSize, BekenOtp2AhbDataSize),
        };
        #endregion
        #region LN882x
        const string LnEfuseSpace = "eFuse shadow/current";
        const string LnFlashOtpSpace = "SPI flash OTP";
        const string LnHalController = "HAL functions";
        #endregion
        #region RTL87xx
        const string Rtlz2EfuseSpace = "physical eFuse bytes";
        const string Rtlz2EfuseController = "HAL functions";
        const string Rtl8710bEfuseSpace = "logical eFuse map";
        const string Rtl8710bEfuseController = "EFUSE_LogicalMap_Read";
        const string RtlKm0RomSpace = "KM0 ROM memory";
        const string RtlKm4RomSpace = "KM4 ROM memory";
        const string RtlAmebaEfuseSpace = "logical OTP map";
        const string RtlAmebaEfuseController = "OTP_LogicalMap_Read";
        #endregion
        #region ECR6600
        const string EcrEfuseSpace = "eFuse raw image";
        const string EcrEfuseController = "eFuse controller @ 0x0020F000";
        #endregion
        #region RDA5981
        const string RdaEfuseSpace = "eFuse pages 0..15";
        const string RdaEfuseController = "RF SPI @ 0x4001301C";
        const string RdaOtpController = "SPI read via 0x48 command";
        #endregion
        #region GD32
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
        const string Xr809Backend = "XRadio BROM/stub command";
        const string Xr809RomController = "BROM/stub cmd 0x08";
        const string XrEfuseSpace = "eFuse raw image";
        const string XrBromBackend = "XRadio BROM command";
        const string XrRomController = "BROM cmd 0x08";
        const string XrEfuseController = "eFuse regs 0x40043C40/0x40043C60";
        #endregion
        #region OPL1000A2
        const string OplEfuseSpace = "OTP data";
        const string OplEfuseController = "Hal_Sys_OtpRead";
        #endregion
        const string CommonStubRomController = "raw CPU memory via XMODEM";
        const string CommonRomMemory = "ROM memory";

        static string GCD(object chipType, byte cmd) => $"{chipType}_Stub cmd 0x{cmd:X2}";
        private static readonly byte CRR = ECRBaseFlasher.CMD_CUSTOM_XMODEM_READ_RAW;
        private static readonly byte CRE = ECRBaseFlasher.CMD_CUSTOM_READ_EFUSE;
        private static readonly byte CRO = ECRBaseFlasher.CMD_CUSTOM_READ_OTP;

        static readonly IReadOnlyList<RomReadTarget> Targets = new List<RomReadTarget>()
        {
            new RomReadTarget(BKType.BK7231M, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231M, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7231N, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, BekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            // BK7236 production Boot ROM maps to 0x061F0000; its non-secure alias is untested on hardware.
            new RomReadTarget(BKType.BK7236, RomReadKind.Rom, "ROM", 0x161F0000, 0x10000, 115200, CommonSerialBauds, BekenNonSecureRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7239N, RomReadKind.Rom, "ROM", 0x16000000, 0x10000, 115200, CommonSerialBauds, BekenNonSecureRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7239N, RomReadKind.Efuse, "eFuse", 0x00000000, FourByteBekenEfuseSize, 115200, CommonSerialBauds, FourByteBekenEfuseSpace, BekenReadRegBackend, Bk7239NEfuseController),
            new RomReadTarget(BKType.BK7239N, RomReadKind.Otp, "OTP", 0x00000000, BekenOtp1ApbDataSize + BekenOtp2AhbDataSize, 115200, CommonSerialBauds, BekenOtpApbAhbSpace, BekenReadRegBackend, BekenOtpApbAhbWindows, outputSlices: BekenOtpApbAhbSlices),
            new RomReadTarget(BKType.BK7238, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7238, RomReadKind.Efuse, "eFuse", 0x00000000, FourByteBekenEfuseSize, 115200, CommonSerialBauds, FourByteBekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, BekenRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7252N, RomReadKind.Efuse, "eFuse", 0x00000000, FourByteBekenEfuseSize, 115200, CommonSerialBauds, FourByteBekenEfuseSpace, BekenEfuseBackend, BekenSctrlEfuseController),
            // BK7231T/BK7231U do not expose ROM/eFuse reads over UART.
            // BK7258 download mode filters the secure ROM alias at 0x06000000.
            // The non-secure alias at 0x16000000 maps to the same physical ROM.
            new RomReadTarget(BKType.BK7258, RomReadKind.Rom, "ROM", 0x16000000, 0x10000, 115200, CommonSerialBauds, BekenNonSecureRomSpace, BekenReadRegBackend, BekenRomController),
            new RomReadTarget(BKType.BK7258, RomReadKind.Efuse, "eFuse", 0x00000000, FourByteBekenEfuseSize, 115200, CommonSerialBauds, FourByteBekenEfuseSpace, BekenEfuseBackend, Bk7258EfuseController),
            new RomReadTarget(BKType.BK7258, RomReadKind.Otp, "OTP", 0x00000000, BekenOtp1ApbDataSize + BekenOtp2AhbDataSize, 115200, CommonSerialBauds, BekenOtpApbAhbSpace, BekenReadRegBackend, BekenOtpApbAhbWindows, outputSlices: BekenOtpApbAhbSlices),
            new RomReadTarget(BKType.W800, RomReadKind.Rom, "Mask ROM", 0x00000000, 0x5000, 921600, CommonSerialBauds, CommonRomMemory, GCD(BKType.W800, CRR), CommonStubRomController),
            new RomReadTarget(BKType.W800, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.W800, CRO), RdaOtpController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Rom, "ROM", 0x00000000, 0x20000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.LN882H, CRR), CommonStubRomController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, LnFlashOtpSpace, GCD(BKType.LN882H, CRO), LnHalController),
            new RomReadTarget(BKType.LN882H, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, LnEfuseSpace, GCD(BKType.LN882H, CRE), LnHalController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.LN8825, CRR), CommonStubRomController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Otp, "Flash OTP", 0x00000000, 0x400, 115200, CommonSerialBauds, LnFlashOtpSpace, GCD(BKType.LN8825, CRO), LnHalController),
            new RomReadTarget(BKType.LN8825, RomReadKind.Efuse, "eFuse", 0x00000000, 0x40, 115200, CommonSerialBauds, LnEfuseSpace, GCD(BKType.LN8825, CRE), LnHalController),
            new RomReadTarget(BKType.OPL1000A2, RomReadKind.Rom, "ROM", 0x00000000, 0xC0000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.OPL1000A2, CRR), CommonStubRomController, outputFileNameTag: "M3_ROM"),
            new RomReadTarget(BKType.OPL1000A2, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, OplEfuseSpace, GCD(BKType.OPL1000A2, CRE), OplEfuseController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Rom, "ROM", 0x00000000, 0x60000, 115200, CommonSerialBauds, CommonRomMemory, GCD("RTL8710C", CRR), CommonStubRomController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, Rtlz2EfuseSpace, GCD("RTL8710C", CRE), Rtlz2EfuseController),
            new RomReadTarget(BKType.RTL87X0C, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RTL87X0C, CRO), RdaOtpController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Rom, "ROM", 0x00000000, 0x80000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.RTL8710B, CRR), CommonStubRomController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Efuse, "eFuse", 0x00000000, 0x200, 115200, CommonSerialBauds, Rtl8710bEfuseSpace, GCD(BKType.RTL8710B, CRE), Rtl8710bEfuseController),
            new RomReadTarget(BKType.RTL8710B, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RTL8710B, CRO), RdaOtpController),
            new RomReadTarget(BKType.RTL8720D, RomReadKind.Rom, "ROM", 0x00000000, 0x24000, 115200, CommonSerialBauds, RtlKm0RomSpace, GCD(BKType.RTL8720D, CRR), CommonStubRomController),
            new RomReadTarget(BKType.RTL8720D, RomReadKind.Efuse, "eFuse", 0x00000000, 0x400, 115200, CommonSerialBauds, Rtl8710bEfuseSpace, GCD(BKType.RTL8720D, CRE), Rtl8710bEfuseController),
            new RomReadTarget(BKType.RTL8720D, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RTL8720D, CRO), RdaOtpController),
            // These stubs run on KM4. The KM0 (RTL8721DA) and KR4 (RTL8720E)
            // ROMs are physically private to their respective secondary cores.
            new RomReadTarget(BKType.RTL8721DA, RomReadKind.Rom, "ROM", 0x00000000, 0x80000, 115200, CommonSerialBauds, RtlKm4RomSpace, GCD(BKType.RTL8721DA, CRR), CommonStubRomController, outputFileNameTag: "KM4_ROM"),
            new RomReadTarget(BKType.RTL8721DA, RomReadKind.Efuse, "eFuse", 0x00000000, 0x400, 115200, CommonSerialBauds, RtlAmebaEfuseSpace, GCD(BKType.RTL8721DA, CRE), RtlAmebaEfuseController),
            new RomReadTarget(BKType.RTL8721DA, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RTL8721DA, CRO), RdaOtpController),
            new RomReadTarget(BKType.RTL8720E, RomReadKind.Rom, "ROM", 0x00000000, 0x48000, 115200, CommonSerialBauds, RtlKm4RomSpace, GCD(BKType.RTL8720E, CRR), CommonStubRomController, outputFileNameTag: "KM4_ROM"),
            new RomReadTarget(BKType.RTL8720E, RomReadKind.Efuse, "eFuse", 0x00000000, 0x400, 115200, CommonSerialBauds, RtlAmebaEfuseSpace, GCD(BKType.RTL8720E, CRE), RtlAmebaEfuseController),
            new RomReadTarget(BKType.RTL8720E, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RTL8720E, CRO), RdaOtpController),
            new RomReadTarget(BKType.ECR6600, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.ECR6600, CRR), CommonStubRomController),
            new RomReadTarget(BKType.ECR6600, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 115200, CommonSerialBauds, EcrEfuseSpace, GCD(BKType.ECR6600, CRE), EcrEfuseController),
            new RomReadTarget(BKType.ECR6600, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace, GCD(BKType.ECR6600, CRO), RdaOtpController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Rom, "ROM", 0x00000000, 0x10000, 921600, CommonSerialBauds, CommonRomMemory, GCD(BKType.RDA5981, CRR), CommonStubRomController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 921600, CommonSerialBauds, RdaEfuseSpace, GCD(BKType.RDA5981, CRE), RdaEfuseController),
            new RomReadTarget(BKType.RDA5981, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace + " (Unused)", GCD(BKType.RDA5981, CRO), RdaOtpController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Rom, "ROM", 0x0BF40000, 0x40000, 921600, CommonSerialBauds, CommonRomMemory, GCD(BKType.GD32VW553, CRR), CommonStubRomController),
            new RomReadTarget(BKType.GD32VW553, RomReadKind.Efuse, "eFuse", 0x00000000, Gd32RfEfuseSize + Gd32McuEfuseSize, 921600, CommonSerialBauds, Gd32EfuseSpace, Gd32EfuseBackend, Gd32EfuseController, 0, null, Gd32EfuseSlices),
            new RomReadTarget(BKType.XR806, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, CommonRomMemory, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR806, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, XrEfuseSpace, XrBromBackend, XrEfuseController),
            new RomReadTarget(BKType.XR809, RomReadKind.Rom, "ROM", 0x00000000, 0x4000, 921600, XrSerialBauds, CommonRomMemory, Xr809Backend, Xr809RomController),
            new RomReadTarget(BKType.XR809, RomReadKind.Efuse, "eFuse", 0x00000000, 0x100, 921600, XrSerialBauds, XrEfuseSpace, Xr809Backend, XrEfuseController),
            new RomReadTarget(BKType.XR872, RomReadKind.Rom, "ROM", 0x00000000, 0x28000, 921600, XrSerialBauds, CommonRomMemory, XrBromBackend, XrRomController),
            new RomReadTarget(BKType.XR872, RomReadKind.Efuse, "eFuse", 0x00000000, 0x80, 921600, XrSerialBauds, XrEfuseSpace, XrBromBackend, XrEfuseController),
            new RomReadTarget(BKType.TR6260, RomReadKind.Rom, "ROM", 0x00000000, 0x8000, 115200, CommonSerialBauds, CommonRomMemory, GCD(BKType.TR6260, CRR), CommonStubRomController),
            new RomReadTarget(BKType.TR6260, RomReadKind.Efuse, "eFuse", 0x00000000, 0x20, 115200, CommonSerialBauds, EcrEfuseSpace, GCD(BKType.TR6260, CRE), "eFuse controller @ 0x0060B200"),
            new RomReadTarget(BKType.TR6260, RomReadKind.Otp, "Flash OTP", 0x00000000, -1, 115200, CommonSerialBauds, LnFlashOtpSpace, GCD(BKType.TR6260, CRO), RdaOtpController),
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
