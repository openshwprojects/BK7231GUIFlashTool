| Platform | Family | Read | Write | R cmp | W cmp | Erase | OBK Config | RF restore | RF reloc | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ALi M3801 | ALi | Yes (4 MiB) | Maincode only [8] | - | - | No | No | No | No | No |
| BK7231M | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34) | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅¹ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅¹ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ❌² | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7239N | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ❌² | ❌⁷ | ❌⁷ | ✅ |
| BK7252 | Beken | ✅ | ✅¹ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ℹ️ |
| BK7252N (T4) | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ➖ | ➖ | ⚠️¹ | ❌² | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ✅³ | ➖ | ➖ | ✅ | ❌ | ➖ | ➖ | ℹ️ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ⚠️⁴ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ⚠️⁴ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ➖ | ➖ | ✅ | ⚠️⁶ | ➖ | ➖ | ⚠️⁴ |
| ESP32<br>-C2<br>-C3<br>-C5<br>-C6<br>-C61<br>-S2<br>-S3 | Espressif | ✅ | ✅ | ➖ | ❌ | ✅ | ❌ | ➖ | ➖ | ✅ |
| ESP8266<br>ESP8285 | Espressif | ✅ | ✅ | ➖ | ➖ | ✅ | ❌ | ➖ | ➖ | ✅ |
| ECR6600 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| TR6260 | ESWIN / Transa | ✅ | ✅ | 🟰 | 🟰 | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ✅³ | ➖ | ➖ | ✅ | ❌ | ➖ | ➖ | ℹ️ |
| LN882H | Lightning Semi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| LN8825 | Lightning Semi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RDA5981 | RDA Micro | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| OPL1000A2 | Opulinks | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ | ✅ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ➖ | 🟰 | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| W600 (write only) | WinnerMicro | ❌⁵ | ✅⁵ | ➖ | ➖ | ✅ | ⚠️⁵ | ➖ | ➖ | ❌ |
| W80x | WinnerMicro | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| XR806 | XRadio | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ✅ |
| XR809 | XRadio | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ✅ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ➖ | ➖ | ✅ | ✅ | ➖ | ➖ | ✅ |

✅ - Works<br>
❓ - Not tested<br>
❌ - Not implemented<br>
❗️ - Broken<br>
⚠️ - Warning<br>
ℹ️ - Needs checking<br>
➖ - Not applicable<br>
🟰 - Intentionally disabled<br>
[8] Only the supported `dump_maciej.bin` classic bootloader profile and flash layout. Fixed 115200 8E1. Writes require a valid maincode chunk at `0x50000`, transfer CRCs and successful burn completion. Backup mode checks the exact loader before writing; no-backup mode checks short version/layout metadata. Optional post-write verification checks the complete CRC-protected readback, payload and loader; backup mode also compares all outside bytes. Without verification the tool reboots after burn and reports an unverified write. The GUI verification checkbox defaults off; CLI verification defaults on and can be disabled with `--no-verify`. Full images and arbitrary raw writes are rejected.

R cmp / W cmp - Read/write compression via custom flasher stub; ➖ means not applicable to that platform's ROM/vendor/original transfer path.<br>

¹ Erase and default writes start at `0x11000`<br>
² OBK config location is not defined for this platform<br>
³ Always writes from `0x0`<br>
⁴ Custom reads work, but custom writes still follow the image/partition flow instead of arbitrary raw offsets.<br>
⁵ Write-only; standalone OBK config writes are disabled and config injection only happens during a full firmware write.<br>
⁶ OBK config reads work, but standalone OBK config writes are not implemented.<br>
⁷ RF partition restore and relocation are not defined for this platform.<br>
