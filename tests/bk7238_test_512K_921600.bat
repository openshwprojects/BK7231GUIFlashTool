@echo off
..\BK7231Flasher\bin\Release\BK7231Flasher.exe -test -chip BK7238 -ofs 0x11000 -len 0x80000 -baud 921600
pause
