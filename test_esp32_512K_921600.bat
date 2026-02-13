@echo off
BK7231Flasher\bin\Release\BK7231Flasher.exe -test -chip ESP32 -ofs 0x10000 -len 0x80000 -baud 921600
pause
