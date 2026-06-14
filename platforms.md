| Platform | Family | Read | Write | Erase | OBK config write | RF restore | RF relocation | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| BK7231M | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34, BL2028N) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ⚠️² | ❌² | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7252 | Beken | ⚠️¹ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ⚠️¹ |
| BK7252N (T4) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ⚠️² | ❌² | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ⚠️³ | ⚠️³ | ❌ | ❌ | ❌ | ❌ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️⁴ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️⁴ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ✅ | ❌⁵ | ➖ | ❌ | ⚠️⁴ |
| ESP32 | Espressif | ✅ | ✅ | ❌⁶ | ❌⁶ | ➖ | ❌ | ❌⁶ |
| ESP32<br>-C2<br>-C3<br>-C5<br>-C6<br>-C61<br>-S2<br>-S3 | Espressif | ✅ | ✅ | ❌⁶ | ❌⁶ | ➖ | ❌ | ❌⁶ |
| ESP8266<br>ESP8285 | Espressif | ✅ | ✅ | ❌⁶ | ❌⁶ | ➖ | ❌ | ❌⁶ |
| ECR6600 | ESWIN / Transa Semi | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| TR6260 | ESWIN / Transa Semi | ✅ | ✅⁷ | ✅ | ✅ | ➖ | ❌ | ⚠️⁷ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ⚠️³ | ⚠️³ | ❌ | ❌ | ❌ | ❌ |
| LN882H | Lightning Semi | ✅ | ✅ | ❌⁸ | ✅ | ➖ | ❌ | ✅ |
| LN8825 | Lightning Semi | ✅ | ✅ | ❌⁸ | ✅ | ➖ | ❌ | ✅ |
| RDA5981 | RDA Micro | ✅ | ✅⁹ | ✅ | ✅ | ➖ | ❌ | ⚠️⁹ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️¹⁰ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️¹⁰ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ❌¹¹ | ✅ | ➖ | ❌ | ⚠️¹⁰ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ❌¹² | ➖ | ❌ | ✅ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ❌¹² | ➖ | ❌ | ✅ |
| W600 (write only) | WinnerMicro | ❌¹³ | ✅¹³ | ❌ | ⚠️¹³ | ➖ | ❌ | ❌ |
| W80x | WinnerMicro | ✅ | ✅¹⁴ | ❌ | ✅¹⁴ | ➖ | ❌ | ⚠️¹⁴ |
| XR806 | XRadio | ✅ | ✅ | ✅¹⁵ | ❌ | ➖ | ❌ | ⚠️¹⁵ |
| XR809 | XRadio | ✅ | ✅ | ✅¹⁵ | ❌ | ➖ | ❌ | ⚠️¹⁵ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ✅¹⁵ | ❌ | ➖ | ❌ | ⚠️¹⁵ |

✅ - Works<br>
❓ - Not tested<br>
❌ - Not implemented<br>
❗️ - Broken<br>
⚠️ - Warning<br>
➖ - Not applicable<br>

¹ `BK7231T`/`BK7231U` default write and erase start at `0x11000`; `BK7252` normal reads also start there, so the bootloader area is not part of a standard dump and custom work against it is limited.<br>
² `BK7236`/`BK7258` standalone OBK config write is not implemented, and the GUI erase-all path only covers the first `2MB`.<br>
³ `Beken SPI CH341` and `Generic SPI CH341` always write from `0x0`, and erase is chip-erase only.<br>
⁴ `BL602`/`BL616`/`BL618`/`BL702` custom reads work, but custom writes still follow the image/partition flow instead of arbitrary raw offsets.<br>
⁵ `BL702` standalone OBK config write is blocked.<br>
⁶ ESP read/write support is there, but the GUI erase action, standalone OBK config write, and custom offset read/write paths are not wired up.<br>
⁷ `TR6260` writes erase the whole `1MB` flash before programming, including custom writes.<br>
⁸ `LN882H`/`LN8825` do not implement a separate erase action; regular writes still work.<br>
⁹ `RDA5981` firmware writes use fixed image start addresses, so custom write offsets are not supported.<br>
¹⁰ `RTL8710B`/`RTL8720DN`/`RTL87X0C` custom write offsets are unreliable; custom reads still work.<br>
¹¹ `RTL87X0C` explicit erase is not implemented.<br>
¹² `RTL8721DA` and `RTL8720E` do not support standalone OBK config writes.<br>
¹³ `W600` is write-only; standalone OBK config writes are disabled and config injection only happens during a full firmware write.<br>
¹⁴ `W80x` writes expect `.fls` or a full-backup-style `.bin` with a firmware header at `0x2000`; config writes use the same wrapped path.<br>
¹⁵ `XR806`/`XR809`/`XR872` explicit erase is full-chip only, and custom writes are raw bytes only.<br>
