using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    class OBKFlags
    {
       public static string[] names = {
            "[MQTT] Broadcast led params together (send dimmer and color when dimmer or color changes, topic name: YourDevName/led_basecolor_rgb/get, YourDevName/led_dimmer/get)",
            "[MQTT] Broadcast led final color (topic name: YourDevName/led_finalcolor_rgb/get)",
            "[MQTT] Broadcast self state every N (def: 60) seconds (delay configurable by 'mqtt_broadcastInterval' and 'mqtt_broadcastItemsPerSec' commands)",
            "[LED][Debug] Show raw PWM controller on WWW index instead of new LED RGB/CW/etc picker",
            "[LED] Force show RGBCW controller (for example, for SM2135 LEDs, or for DGR sender)",
            "[CMD] Enable TCP console command server (for Putty, etc)",
            "[BTN] Instant touch reaction instead of waiting for release (aka SetOption 13)",
            "[MQTT] [Debug] Always set Retain flag to all published values",
            "[LED] Alternate CW light mode (first PWM for warm/cold slider, second for brightness)",
            "[SM2135] Use separate RGB/CW modes instead of writing all 5 values as RGB",
            "[MQTT] Broadcast self state on MQTT connect",
            "[PWM] BK7231 use 600hz instead of 1khz default",
            "[LED] Remember LED driver state (RGBCW, enable, brightness, temperature) after reboot",
            "[HTTP] Show actual PIN logic level for unconfigured pins",
            "[IR] Do MQTT publish (RAW STRING) for incoming IR data",
            "[IR] Allow 'unknown' protocol",
            "[MQTT] Broadcast led final color RGBCW (topic name: YourDevName/led_finalcolor_rgbcw/get)",
            "[LED] Automatically enable Light when changing brightness, color or temperature on WWW panel",
            "[LED] Smooth transitions for LED (EXPERIMENTAL)",
            "[MQTT] Always publish channels used by TuyaMCU",
            "[LED] Force RGB mode (3 PWMs for LEDs) and ignore futher PWMs if they are set",
            "[MQTT] Retain power channels (Relay channels, etc)",
            "[IR] Do MQTT publish (Tasmota JSON format) for incoming IR data",
            "[LED] Automatically enable Light on any change of brightness, color or temperature",
            "[LED] Emulate Cool White with RGB in device with four PWMS - Red is 0, Green 1, Blue 2, and Warm is 4",
            "[POWER] Allow negative current/power for power measurement (all chips, BL0937, BL0942, etc)",
	        // On BL602, if marked, uses /dev/ttyS1, otherwise S0
	        // On Beken, if marked, uses UART2, otherwise UART1
	        "[UART] Use alternate UART for BL0942, CSE, TuyaMCU, etc",
            "[HASS] Invoke HomeAssistant discovery on change to ip address, configuration",
            "[LED] Setting RGB white (FFFFFF) enables temperature mode",
            "[NETIF] Use short device name as a hostname instead of a long name",
            "[MQTT] Enable Tasmota TELE etc publishes (for ioBroker etc)",
            "[UART] Enable UART command line",
            "[LED] Use old linear brightness mode, ignore gamma ramp",
            "[MQTT] Apply channel type multiplier on (if any) on channel value before publishing it",
            "[MQTT] In HA discovery, add relays as lights",
            "[HASS] Deactivate avty_t flag for sensor when publishing to HASS (permit to keep value). You must restart HASS discovery for change to take effect.",
            "[DRV] Deactivate Autostart of all drivers",
            "[WiFi] Quick connect to WiFi on reboot (TODO: check if it works for you and report on github)",
            "[Power] Set power and current to zero if all relays are open",
            "[MQTT] [Debug] Publish all channels (don't enable it, it will be publish all 64 possible channels on connect)",
            "[MQTT] Use kWh unit for energy consumption (total, last hour, today) instead of Wh",
            "[BTN] Ignore all button events (aka child lock)",
            "[DoorSensor] Invert state",
            "error",
            "error",
            "error",
            "error",
            "error",
            "error",
            "error",
            "error",
        };

        internal static string getSafe(int i)
        {
            if (i >= names.Length)
            {
                return "";
            }
            return names[i];
        }
    }
}
