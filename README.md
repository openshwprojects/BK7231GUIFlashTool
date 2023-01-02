# BK7231GUIFlashTool

BK7231 GUI Flash Tool a simple Windows Application that allows you to flash new firmware to BK7231T/BK7231N devices without having computer and programming knowledge.
Futhermore, it automatically creates an original firmware backup, so you can submit the original firmware dump for futher analysis (NOTE: it may contain SSID if paired).

# Usage

1. Download and unpack executable from Releases tab on the right
2. Prepare flashing circuit for BK7231 (both T and N)
2.1 get USB to UART converter with 3.3V voltage signals
2.2 connecte RX to TXD1 of Beken, TX to RXD1 of Beken
2.3 you may also need to solder a wire to CEN signal, more about that later
2.4 of course, you also need to power device from some reliable power supply, Beken runs on 3.3V, do not try hacking devices connected to mains!
3. Open our flasher:

![image](https://user-images.githubusercontent.com/85486843/210281085-6141160b-df6d-486c-b574-ef784f5cbd56.png)
4. Select proper platform - BK7231T or BK7231N
5. Select your COM port of USB to UART converter
6. Click "Download latest from Web" to get proper binary file
7. Wait for download to finish

![image](https://user-images.githubusercontent.com/85486843/210281125-a3e25ab2-3144-4e02-a30c-6e135ecefd24.png)
8. Close download window
9. Click "Backup and flash new"
10. When the log window is waiting for "Getting bus", do a device reboot. You can do this in two ways, choose one:
  Option A: short CEN to GND for 0.25s (it is tricky to get this right, requires precise timing)
  Option B: power off and on device (of course, it should not be connected to mains, use your own safe 3.3V power supply that can supply enough current)
  
![image](https://user-images.githubusercontent.com/85486843/210281194-27decf09-723e-41f7-8b47-6fe2b6bb4857.png)
11. It will begin reading (it does first backup, then write)

![image](https://user-images.githubusercontent.com/85486843/210281251-cd69ddab-f0ab-4389-8476-0eb33045aa76.png)



# CRC Mismatch?
CRCs are calculated correctly for both N and T. If you get CRC mismatch, you are most likely selecting a wrong chip type.
![image](https://user-images.githubusercontent.com/85486843/210281290-31d037f5-61c1-403b-a9c5-891fbda75914.png)
