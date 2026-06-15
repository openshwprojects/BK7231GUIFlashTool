| Platform | Family | Read | Write | Erase | OBK Config | RF restore | RF relocation | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| BK7231M | Beken | ✅ | ✅³ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ✅² | ➖² | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅³ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7252 | Beken | ⚠️¹'¹⁰ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ❓ |
| BK7252N (T4) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ✅ | ➖² | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ✅³ | ✅³ | ❌ | ❌ | ❌ | ❌ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️⁴ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ⚠️⁴ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ | ⚠️⁴ |
| ESP32 | Espressif | ✅ | ✅ | ✅ | ❓ | ➖ | ➖ | ❌ |
| ESP32<br>-C2<br>-C3<br>-C5<br>-C6<br>-C61<br>-S2<br>-S3 | Espressif | ✅ | ✅ | ✅ | ❓ | ➖ | ➖ | ❓ |
| ESP8266<br>ESP8285 | Espressif | ✅ | ✅ | ✅ | ❓ | ➖ | ➖ | ❓ |
| ECR6600 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| TR6260 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❓ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ✅³ | ✅ | ➖ | ➖ | ➖ | ➖ |
| LN882H | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ➖ | ✅ |
| LN8825 | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ➖ | ✅ |
| RDA5981 | RDA Micro | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ❓ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ❓ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ✅⁵ | ✅ | ➖ | ➖ | ❓ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ❌ | ➖ | ➖ | ❓ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ❌ | ➖ | ➖ | ❓ |
| W600 (write only) | WinnerMicro | ❌⁶ | ✅⁶ | ❌ | ⚠️⁶ | ➖ | ➖ | ❌ |
| W80x | WinnerMicro | ✅ | ✅⁷ | ❌ | ✅⁷ | ➖ | ❌ | ⚠️⁷ |
| XR806 | XRadio | ✅ | ✅ | ✅⁸ | ❌ | ➖ | ❌ | ⚠️⁸ |
| XR809 | XRadio | ✅ | ✅ | ✅⁸ | ❌ | ➖ | ❌ | ⚠️⁸ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ✅⁸ | ❌ | ➖ | ❌ | ⚠️⁸ |

✅ - Works<br>
❓ - Not tested<br>
❌ - Not implemented<br>
❗️ - Broken<br>
⚠️ - Warning<br>
➖ - Not applicable<br>

¹ Default write and erase start at `0x11000`<br>
² No OpenBK support at present<br>
³ Always writes from `0x0`<br>
⁴ `BL602`/`BL616`/`BL618`/`BL702` custom reads work, but custom writes still follow the image/partition flow instead of arbitrary raw offsets.<br>
⁵ `RTL87X0C` erase-all is implemented as a chip erase; sector erase is not implemented in the current backend.<br>
⁶ Write-only; standalone OBK config writes are disabled and config injection only happens during a full firmware write.<br>
⁷ Writes expect `.fls` or a full-backup-style `.bin` with a firmware header at `0x2000`; config writes use the same wrapped path.<br>
⁸ `XR806`/`XR809`/`XR872` explicit erase is full-chip only, and custom writes are raw bytes only. Full-chip erase performed before write.<br>
¹⁰ 4MB `BK7252U` wrap-around broken<br>
