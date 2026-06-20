| Platform | Family | Read | Write | Erase | OBK Config | RF restore | RF relocation | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| BK7231M | Beken | ✅ | ✅³ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ✅ | ➖² | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅³ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7252 | Beken | ⚠️¹'⁷ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ | ℹ️ |
| BK7252N (T4) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ✅ | ➖² | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ✅³ | ✅ | ℹ️ | ℹ️ | ℹ️ | ℹ️ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️⁴ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️⁴ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ | ℹ️⁴ |
| ESP32 | Espressif | ✅ | ✅ | ✅ | ℹ️ | ➖ | ➖ | ❌ |
| ESP32<br>-C2<br>-C3<br>-C5<br>-C6<br>-C61<br>-S2<br>-S3 | Espressif | ✅ | ✅ | ✅ | ℹ️ | ➖ | ➖ | ℹ️ |
| ESP8266<br>ESP8285 | Espressif | ✅ | ✅ | ✅ | ℹ️ | ➖ | ➖ | ℹ️ |
| ECR6600 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| TR6260 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ℹ️ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ✅³ | ✅ | ➖ | ➖ | ➖ | ➖ |
| LN882H | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ➖ | ℹ️ |
| LN8825 | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ➖ | ℹ️ |
| RDA5981 | RDA Micro | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| W600 (write only) | WinnerMicro | ❌⁵ | ✅⁵ | ❌ | ⚠️⁵ | ➖ | ➖ | ❌ |
| W80x | WinnerMicro | ✅ | ✅ | ❌ | ✅ | ➖ | ❌ | ℹ️ |
| XR806 | XRadio | ✅ | ✅ | ✅⁶ | ❌ | ➖ | ❌ | ℹ️ |
| XR809 | XRadio | ✅ | ✅ | ✅⁶ | ❌ | ➖ | ❌ | ℹ️ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ✅⁶ | ❌ | ➖ | ❌ | ℹ️ |

✅ - Works<br>
❓ - Not tested<br>
❌ - Not implemented<br>
❗️ - Broken<br>
⚠️ - Warning<br>
ℹ️ - Needs checking<br>
➖ - Not applicable<br>

¹ Default write and erase start at `0x11000`<br>
² No OpenBK firmware at present for this platform<br>
³ Always writes from `0x0`<br>
⁴ Custom reads work, but custom writes still follow the image/partition flow instead of arbitrary raw offsets.<br>
⁵ Write-only; standalone OBK config writes are disabled and config injection only happens during a full firmware write.<br>
⁶ Full-chip erase performed before write.<br>
⁷ 4MB `BK7252U` wrap-around broken<br>
