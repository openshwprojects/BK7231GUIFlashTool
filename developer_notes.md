

	bkWriter 1.60 notes/dumps

Baud 115200, bkWriter 1.60, BK7231T, read flash at 0x11000, read lenght 0x1000 (one sector)


72 65 62 6F 6F 74 0D 0A <------ this is "reboot" ASCII command
01 E0 FC 01 00 <---------------- BuildCmd_LinkCheck, it repeats about 10, 20 times
01 E0 FC FF F4 05 00 09 00 10 01 00 <------- BuildCmd_FlashRead4K
01 E0 FC FF F4 05 00 09 00 10 01 00 <------- BuildCmd_FlashRead4K
01 E0 FC 02 0E A5 <------ BuildCmd_Reboot (02 is data lenght, 0E is CMD_Reboot, argument is A5), it repeats 3 - 6 times

As above, but baud 921600.

72 65 62 6F 6F 74 0D 0A <------ this is "reboot" ASCII command
01 E0 FC 01 00 <---------------- BuildCmd_LinkCheck, it repeats about 10, 20 times
01 E0 FC 06 0F 00 10 0E 00 0F D4 9E 9E 9E   <------- BuildCmd_SetBaudRate
Then baud rate changes. I had to do capture separately.
01 E0 FC FF F4 05 00 09 00 10 01 00 <------ BuildCmd_FlashRead4K
01 E0 FC 02 0E A5 <------ BuildCmd_Reboot (02 is data lenght, 0E is CMD_Reboot, argument is A5), it repeats 3 - 6 times


Trying to write it back.
At 115200, it fails at erasing flash.
At 921600 it works...

Then baud rate changes. I had to do capture separately.
01 E0 FC FF F4 06 00 0F 20 00 10 01 00 <----- BuildCmd_FlashErase, erase szCmd = 20, address 00 10 01 00
01 E0 FC FF F4 05 10 07 00 10 01 00 [4096bytesOfWrittenData] <----- BuildCmd_FlashWrite4K
01 E0 FC 09 10 00 10 01 00 FF 1F 01 00 <---- BuildCmd_CheckCRC (09 is data lenght, 10 is CMD_CheckCRC, then start address 00 10 01 00, end address)
01 E0 FC 02 0E A5 <------ BuildCmd_Reboot (02 is data lenght, 0E is CMD_Reboot, argument is A5), it repeats 3 - 6 times



