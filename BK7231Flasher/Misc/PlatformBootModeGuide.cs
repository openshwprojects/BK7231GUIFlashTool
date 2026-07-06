using System.Collections.Generic;

namespace BK7231Flasher
{
    public static class PlatformBootModeGuide
    {
        static readonly IReadOnlyDictionary<BKType, string> Instructions = new Dictionary<BKType, string>()
        {
            { BKType.BK7231M, "" },
            { BKType.BK7231N, GetBekenUart1Instructions("BK7231N") },
            { BKType.BK7231T, GetBekenUart1Instructions("BK7231T") },
            { BKType.BK7231U, GetBekenUart1Instructions("BK7231U") },
            { BKType.BK7236, "" },
            { BKType.BK7238, GetBekenUart1Instructions("BK7238") },
            { BKType.BK7252, "" },
            { BKType.BK7252N, GetBekenUart1Instructions("BK7252N") },
            { BKType.BK7258, "" },
            { BKType.BL602, "" },
            { BKType.BL702, "" },
            { BKType.BL616, "" },
            { BKType.LN882H, GetLn882xInstructions("LN882H", "A2", "A3", "BOOT/GPIOA9") },
            { BKType.LN8825, GetLn882xInstructions("LN8825B", "B9", "B8", "BOOT/GPIOA10") },
            { BKType.ESP32, "" },
            { BKType.ESP32S2, "" },
            { BKType.ESP32C2, "" },
            { BKType.ESP32C3, "" },
            { BKType.ESP32C5, "" },
            { BKType.ESP32C6, "" },
            { BKType.ESP32C61, "" },
            { BKType.ESP32S3, "" },
            { BKType.ESP8266, "" },
            { BKType.RTL87X0C, GetRtl87x0cInstructions() },
            { BKType.RDA5981, GetRda5981Instructions() },
            { BKType.TR6260, "" },
            { BKType.W800, "" },
            { BKType.XR806, GetXr806Instructions() },
            { BKType.XR809, GetXrUart0TwoBootPinsInstructions("XR809") },
            { BKType.XR872, GetXrUart0TwoBootPinsInstructions("XR872") },
        };

        static string GetBekenUart1Instructions(string platformName)
        {
            return "Connect the " + platformName + " UART1 flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " TX1 (GPIO11 / P11)" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " RX1 (GPIO10 / P10)" + System.Environment.NewLine +
                "- Adapter GND -> board GND" + System.Environment.NewLine +
                "Power the board from a stable 3.3 V supply. If that supply is separate from the USB-to-TTL adapter, connect the supply GND to the adapter GND as a common ground." + System.Environment.NewLine +
                "Start the read first. While the tool is trying to connect, reset the chip by briefly pulling CEN to GND, or by power-cycling the 3.3 V supply. If linking does not start, try the reset/power-cycle again.";
        }

        static string GetLn882xInstructions(string platformName, string txPin, string rxPin, string bootPin)
        {
            return "Connect the " + platformName + " UART flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " " + txPin + " / TX0" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " " + rxPin + " / RX0" + System.Environment.NewLine +
                "- Adapter GND -> board GND" + System.Environment.NewLine +
                "- " + bootPin + " -> GND" + System.Environment.NewLine +
                "Use a stable 3.3 V supply and a common ground between the board and adapter." + System.Environment.NewLine +
                "With " + bootPin + " held low, unplug the USB-to-TTL adapter from the computer, plug it back in, then power on the 3.3 V supply.";
        }

        static string GetRtl87x0cInstructions()
        {
            return "Connect the RTL87X0C UART2 flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> RTL87X0C TX2 (Log_TX / PA16)" + System.Environment.NewLine +
                "- Adapter TX -> RTL87X0C RX2 (Log_RX / PA15)" + System.Environment.NewLine +
                "- Adapter GND -> board GND" + System.Environment.NewLine +
                "- PA00 / GPIO0 -> 3.3 V" + System.Environment.NewLine +
                "- PA13 / GPIO13 (RXD) -> 3.3 V" + System.Environment.NewLine +
                "Use a stable 3.3 V supply and a common ground between the board and adapter." + System.Environment.NewLine +
                "With PA00 and PA13 pulledd high, start the read operation first, then reset the chip by briefly pulling CEN to GND, or by power-cycling the 3.3 V supply.";
        }

        static string GetRda5981Instructions()
        {
            return "Connect the RDA5981 UART flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> RDA5981 TX0 (GPIO27)" + System.Environment.NewLine +
                "- Adapter TX -> RDA5981 RX (GPIO26)" + System.Environment.NewLine +
                "- Adapter GND -> board GND" + System.Environment.NewLine +
                "- IO21 -> 3.3 V if the board does not enter UART download mode" + System.Environment.NewLine +
                "Use a stable 3.3 V supply and a common ground between the board and adapter." + System.Environment.NewLine +
                "Start the read first. While the tool is trying to connect, reset or power-cycle the device; if linking does not start, try again with IO21 pulled high.";
        }

        static string GetXr806Instructions()
        {
            return GetXrUart0Instructions(
                "XR806",
                "- PB02 -> GND",
                "With PB02 held low, start the read operation first, reset the chip by pulling CHIP_PWD low and releasing it, then release PB02.");
        }

        static string GetXrUart0TwoBootPinsInstructions(string platformName)
        {
            return GetXrUart0Instructions(
                platformName,
                "- PB02 -> GND" + System.Environment.NewLine +
                "- PB03 -> GND",
                "With PB02 and PB03 held low, start the read operation first, then reset the chip by pulling CHIP_PWD low and releasing it, or power-cycle the device.");
        }

        static string GetXrUart0Instructions(string platformName, string bootPinLines, string bootModeAction)
        {
            return "Connect the " + platformName + " UART0 flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " TX0 (PB00)" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " RX0 (PB01)" + System.Environment.NewLine +
                "- Adapter GND -> board GND" + System.Environment.NewLine +
                bootPinLines + System.Environment.NewLine +
                "Use a stable 3.3 V supply and a common ground between the board and adapter." + System.Environment.NewLine +
                bootModeAction;
        }

        public static string GetInstructions(BKType platform)
        {
            string instructions;
            if (Instructions.TryGetValue(platform, out instructions))
            {
                return instructions;
            }
            return "No ROM-reader bootstrapping notes are catalogued yet for this platform.";
        }
    }
}
