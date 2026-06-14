@echo off
..\BK7231Flasher\bin\Release\BK7231Flasher.exe --chip ESP32 -b 921600 test --addr 0x10000 --size 0x10000
pause
