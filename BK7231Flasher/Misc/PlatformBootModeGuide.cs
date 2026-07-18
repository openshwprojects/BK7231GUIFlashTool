using System.Collections.Generic;

namespace BK7231Flasher
{
    public static class PlatformBootModeGuide
    {
        static readonly IReadOnlyDictionary<BKType, string> Instructions = new Dictionary<BKType, string>()
        {
            { BKType.BK7231M, GetBekenUart1Instructions("BK7231M") },
            { BKType.BK7231N, GetBekenUart1Instructions("BK7231N") },
            { BKType.BK7231T, GetBekenUart1Instructions("BK7231T") },
            { BKType.BK7231U, GetBekenUart1Instructions("BK7231U") },
            { BKType.BK7236, GetBekenUartInstructions("BK7236", "download UART", "DL_UART_TX", "DL_UART_RX") },
            { BKType.BK7238, GetBekenUart1Instructions("BK7238") },
            { BKType.BK7252, "" },
            { BKType.BK7252N, GetBekenUart1Instructions("BK7252N") },
            { BKType.BK7258, GetBekenUartInstructions("BK7258", "download UART", "DL_UART_TX", "DL_UART_RX") },
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
            { BKType.ECR6600, GetEcr6600Instructions() },
            { BKType.GD32VW553, GetGd32vw553Instructions() },
            { BKType.OPL1000A2, GetOplInstructions() },
            { BKType.RTL8710B, GetRtl8710bInstructions() },
            { BKType.RTL8721DA, GetRtlUartDownloadInstructions("RTL8721DA", "PB4", "PB5") },
            { BKType.RTL8720E, GetRtlUartDownloadInstructions("RTL8720E", "PA19", "PA20") },
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
            return GetBekenUartInstructions(platformName, "UART1", "TX1", "RX1");
        }

        static string GetBekenUartInstructions(string platformName, string portName, string txName, string rxName)
        {
            return "Connect the " + platformName + " " + portName + " flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " " + txName + " (GPIO11 / P11)" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " " + rxName + " (GPIO10 / P10)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "Start the read first. While the tool is trying to connect, reset the chip by briefly pulling CEN to GND, or by power-cycling the 3.3 V supply. If linking does not start, try the reset/power-cycle again.";
        }

        static string GetLn882xInstructions(string platformName, string txPin, string rxPin, string bootPin)
        {
            return "Connect the " + platformName + " UART flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " " + txPin + " / TX0" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " " + rxPin + " / RX0" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                "- " + bootPin + " -> GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "With " + bootPin + " held low, unplug the USB-to-TTL adapter from the computer, plug it back in, then power on the 3.3v supply.";
        }

        static string GetRtl87x0cInstructions()
        {
            return "Connect the RTL87X0C UART2 flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> RTL87X0C TX2 (Log_TX / PA16)" + System.Environment.NewLine +
                "- Adapter TX -> RTL87X0C RX2 (Log_RX / PA15)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                "- PA00 / GPIO0 -> 3.3 V" + System.Environment.NewLine +
                "- PA13 / GPIO13 (RXD) -> 3.3 V" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "With PA00 and PA13 pulled high, start the read operation first, then reset the chip by briefly pulling CEN to GND, or by power-cycling the 3.3 V supply.";
        }

        static string GetRtl8710bInstructions()
        {
            return "Connect the RTL8710B log UART to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> RTL8710B Log_TX (PA30)" + System.Environment.NewLine +
                "- Adapter TX -> RTL8710B Log_RX (PA29)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "Temporarily disconnect the adapter RX from PA30 and hold PA30 low while resetting the chip or power-cycling the 3.3 V supply. Then release PA30, reconnect it to the adapter RX, and start the read.";
        }

        static string GetRtlUartDownloadInstructions(string platformName, string logRxPin, string logTxPin)
        {
            return "Connect the " + platformName + " log UART to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> " + platformName + " Log_TX (" + logTxPin + " / UD_DIS)" + System.Environment.NewLine +
                "- Adapter TX -> " + platformName + " Log_RX (" + logRxPin + ")" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "Temporarily disconnect the adapter RX from " + logTxPin + " and hold " + logTxPin + " / UD_DIS low while resetting the chip or power-cycling the 3.3 V supply. Then release " + logTxPin + ", reconnect it to the adapter RX, and start the read.";
        }

        static string GetEcr6600Instructions()
        {
            return "Connect the ECR6600 UART0 flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> ECR6600 TX0 (GPIO6)" + System.Environment.NewLine +
                "- Adapter TX -> ECR6600 RX0 (GPIO5)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "Start the read first. While the tool is trying to connect, reset the target by briefly pulling RST low, or power-cycle the 3.3 V supply. If linking does not start, try the reset or power-cycle again.";
        }

        static string GetGd32vw553Instructions()
        {
            return "Connect the GD32VW553 UART flash download port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> GD32VW553 TX (PB15)" + System.Environment.NewLine +
                "- Adapter TX -> GD32VW553 RX (PA8)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                "- PC8 / BOOT0 -> 3.3 V" + System.Environment.NewLine +
                "- PB1 / BOOT1 -> GND if the target does not already hold BOOT1 low" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "With BOOT0 held high, reset the chip with RST/NRST or power-cycle the 3.3 V supply. Once the target is in UART download mode, start the read.";
        }

        static string GetRda5981Instructions()
        {
            return "Connect the RDA5981 UART flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> RDA5981 TX0 (GPIO27)" + System.Environment.NewLine +
                "- Adapter TX -> RDA5981 RX (GPIO26)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                "- IO21 -> 3.3 V if the target does not enter UART download mode" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
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
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "First try starting the read with the target running normally. The flasher will send the software 'upgrade' command at 115200 baud before attempting sync. This only works when the existing firmware has its UART console enabled and supports that command." + System.Environment.NewLine +
                "If software entry does not work, use the hardware boot straps:" + System.Environment.NewLine +
                bootPinLines + System.Environment.NewLine +
                bootModeAction;
        }

        static string GetOplInstructions()
        {
            return "Connect the OPL1000 UART flashing port to a USB-to-TTL serial adapter:" + System.Environment.NewLine +
                "- Adapter RX -> OPL1000 DBG TX (GPIO0)" + System.Environment.NewLine +
                "- Adapter TX -> OPL1000 DBG RX (GPIO1)" + System.Environment.NewLine +
                "- Adapter GND -> target GND" + System.Environment.NewLine +
                GetPowerAndGroundInstructions() + System.Environment.NewLine +
                "Start the read first. While the tool is trying to connect, reset or power-cycle the device.";
        }

        static string GetPowerAndGroundInstructions()
        {
            return "Use a stable 3.3 V supply and a common ground between the target and USB-to-TTL adapter. The 3.3 V pin on many USB-to-TTL adapters may not provide enough current for reliable operation.";
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
