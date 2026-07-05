using System.Collections.Generic;

namespace BK7231Flasher
{
    public static class PlatformBootModeGuide
    {
        static readonly IReadOnlyDictionary<BKType, string> Instructions = new Dictionary<BKType, string>()
        {
            { BKType.BK7231M, "Use the same UART download-mode wiring/reset sequence used for normal BK7231M flash read/write." },
            { BKType.BK7231N, GetBekenUart1Instructions("BK7231N") },
            { BKType.BK7231T, GetBekenUart1Instructions("BK7231T") },
            { BKType.BK7231U, GetBekenUart1Instructions("BK7231U") },
            { BKType.BK7236, GetBekenUart1Instructions("BK7236") },
            { BKType.BK7238, GetBekenUart1Instructions("BK7238") },
            { BKType.BK7252, "Use the same UART download-mode wiring/reset sequence used for normal BK7252 flash read/write." },
            { BKType.BK7252N, GetBekenUart1Instructions("BK7252N") },
            { BKType.BK7258, GetBekenUart1Instructions("BK7258") },
            { BKType.BL602, "Hold BOOT/IO8 in boot mode, reset or power-cycle the device, then start the operation." },
            { BKType.BL702, "Hold BOOT/IO28 in boot mode, reset or power-cycle the device, then start the operation." },
            { BKType.BL616, "Hold BOOT/IO28 in boot mode, reset or power-cycle the device, then start the operation." },
            { BKType.LN882H, GetLn882xInstructions("LN882H", "A2", "A3", "BOOT/GPIOA9") },
            { BKType.LN8825, GetLn882xInstructions("LN8825B", "B9", "B8", "BOOT/GPIOA10") },
            { BKType.ESP32, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32S2, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32C2, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32C3, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32C5, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32C6, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32C61, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP32S3, "Hold BOOT/GPIO0 low, reset or power-cycle the device, then release BOOT after sync starts." },
            { BKType.ESP8266, "Hold GPIO0 low, reset or power-cycle the device, then release GPIO0 after sync starts." },
            { BKType.RTL87X0C, GetRtl87x0cInstructions() },
            { BKType.RDA5981, GetRda5981Instructions() },
            { BKType.TR6260, "Use the same serial boot/download-mode wiring used for normal TR6260 flash read/write." },
            { BKType.W800, "Use the same UART boot/download-mode wiring used for normal W800 flash read/write." },
            { BKType.XR806, "Use the same UART boot/download-mode wiring used for normal XR806 flash read/write." },
            { BKType.XR809, "Use the same UART boot/download-mode wiring used for normal XR809 flash read/write." },
            { BKType.XR872, "Use the same UART boot/download-mode wiring used for normal XR872 flash read/write." },
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
