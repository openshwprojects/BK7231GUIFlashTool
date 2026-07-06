| Platform | Family | Read | Write | Erase | OBK Config | RF restore | RF relocation | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| BK7231M | Beken | ✅ | ✅ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34) | Beken | ✅ | ✅ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅¹ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅¹ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ⚠️¹ | ❌² | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7252 | Beken | ✅ | ✅¹ | ⚠️¹ | ✅ | ✅ | ✅ | ⚠️ |
| BK7252N (T4) | Beken | ✅ | ✅ | ⚠️¹ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ⚠️¹ | ❌² | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ✅³ | ⚠️ | ❌ | ➖ | ➖ | ℹ️ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ⚠️⁴ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ⚠️⁴ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ✅ | ⚠️⁶ | ➖ | ➖ | ⚠️⁴ |
| ESP32 | Espressif | ✅ | ✅ | ✅ | ❌ | ➖ | ➖ | ✅ |
| ESP32<br>-C2<br>-C3<br>-C5<br>-C6<br>-C61<br>-S2<br>-S3 | Espressif | ✅ | ✅ | ✅ | ❌ | ➖ | ➖ | ✅ |
| ESP8266<br>ESP8285 | Espressif | ✅ | ✅ | ✅ | ❌ | ➖ | ➖ | ✅ |
| ECR6600 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| TR6260 | ESWIN / Transa | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ⚠️ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ✅³ | ⚠️ | ❌ | ➖ | ➖ | ℹ️ |
| LN882H | Lightning Semi | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| LN8825 | Lightning Semi | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RDA5981 | RDA Micro | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ℹ️ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| W600 (write only) | WinnerMicro | ❌⁵ | ✅⁵ | ❌ | ⚠️⁵ | ➖ | ➖ | ❌ |
| W80x | WinnerMicro | ✅ | ✅ | ❌ | ✅ | ➖ | ➖ | ℹ️ |
| XR806 | XRadio | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| XR809 | XRadio | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ | ✅ |

✅ - Works<br>
❓ - Not tested<br>
❌ - Not implemented<br>
❗️ - Broken<br>
⚠️ - Warning<br>
ℹ️ - Needs checking<br>
➖ - Not applicable<br>

¹ Erase and default writes start at `0x11000`<br>
² OBK config location is not defined for this platform<br>
³ Always writes from `0x0`<br>
⁴ Custom reads work, but custom writes still follow the image/partition flow instead of arbitrary raw offsets.<br>
⁵ Write-only; standalone OBK config writes are disabled and config injection only happens during a full firmware write.<br>
⁶ OBK config reads work, but standalone OBK config writes are not implemented.<br>
