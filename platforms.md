| Platform | Family | Read | Write | Erase | OBK config write | RF restore | RF relocation | Custom R/W |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| BK7231M | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231N (T2, T34, BL2028N) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231T | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7231U | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7236 (T3) | Beken | ✅ | ✅ | ⚠️ | ❌ | ✅ | ✅ | ✅ |
| BK7238 (T1) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7252 | Beken | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| BK7252N (T4) | Beken | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BK7258 (T5) | Beken | ✅ | ✅ | ⚠️ | ❌ | ✅ | ✅ | ✅ |
| Beken SPI CH341 | Beken | ✅ | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ❌ |
| BL602 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| BL616/BL618 | Bouffalo Lab | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| BL702 | Bouffalo Lab | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ⚠️ |
| ESP32 | Espressif | ✅ | ✅ | ❌ | ❌ | ➖ | ❌ | ❌ |
| ESP32-C2/C3/C5/C6/C61/S2/S3 | Espressif | ✅ | ✅ | ❌ | ❌ | ➖ | ❌ | ❌ |
| ESP8285/ESP8266 | Espressif | ✅ | ✅ | ❌ | ❌ | ➖ | ❌ | ❌ |
| ECR6600 | ESWIN / Transa Semi | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| TR6260 | ESWIN / Transa Semi | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| GD32VW553 | GigaDevice | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ✅ |
| Generic SPI CH341 | CH341 SPI | ✅ | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ❌ |
| LN882H | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ❌ | ✅ |
| LN8825 | Lightning Semi | ✅ | ✅ | ❌ | ✅ | ➖ | ❌ | ✅ |
| RDA5981 | RDA Micro | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| RTL8710B (AmebaZ) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| RTL8720DN (AmebaD) | Realtek | ✅ | ✅ | ✅ | ✅ | ➖ | ❌ | ⚠️ |
| RTL87X0C (AmebaZ2) | Realtek | ✅ | ✅ | ❌ | ✅ | ➖ | ❌ | ⚠️ |
| RTL8721DA (AmebaDp) | Realtek | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ✅ |
| RTL8720E (AmebaLite) | Realtek | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ✅ |
| W600 (write only) | WinnerMicro | ❌ | ✅ | ❌ | ⚠️ | ➖ | ❌ | ❌ |
| W800/W803 | WinnerMicro | ✅ | ✅ | ❌ | ✅ | ➖ | ❌ | ⚠️ |
| XR806 | XRadio | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ⚠️ |
| XR809 | XRadio | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ⚠️ |
| XR872 (XF16) | XRadio | ✅ | ✅ | ✅ | ❌ | ➖ | ❌ | ⚠️ |

✅ Works  
⚠️ Works with notable limitations  
❌ Not implemented  
➖ Not applicable  

Notes:

- `BK7231N`/`BK7231M` normal QIO writes preserve the bootloader unless overwrite is enabled.
- `BK7231T`/`BK7231U`/`BK7252` default write and erase start at `0x11000`; `BK7252` backups cannot read the bootloader area.
- `BK7236`/`BK7258` do not support standalone OBK config writes, and the GUI erase-all path only covers the first `2MB`.
- `BL60x` custom reads work, but custom writes still follow the image/partition flow instead of raw arbitrary offsets.
- `ESP32` family read/write support is there, but the GUI erase action, standalone OBK config write, and Custom dialog paths are not.
- `RTL8710B`/`RTL8720DN`/`RTL87X0C` custom write offsets are unreliable; `RTL8721DA`/`RTL8720E` do not support standalone OBK config writes.
- `TR6260` writes erase the whole `1MB` flash before programming; `XR806`/`XR809`/`XR872` erase is full-chip only and standalone OBK config writes are not implemented.
- `W800`/`W803` writes expect `.fls` or full-backup-style `.bin`; `W600` is write-only and only injects OBK config during full writes.
- `Beken SPI CH341` and `Generic SPI CH341` always write from `0x0`, and erase is chip-erase only.
