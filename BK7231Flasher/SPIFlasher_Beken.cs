using System;
using System.Threading;

namespace BK7231Flasher
{
    class SPIFlasher_Beken : SPIFlasher
    {
        // Beken-specific
        public bool GPIO_CEN_SET()
        {
            bool b = CH341.CH341SetOutput(hd.usb_id, 0xFF, 0x04, 0x04);
            return b;
        }
        public bool GPIO_CEN_CLR()
        {
            bool b = CH341.CH341SetOutput(hd.usb_id, 0xFF, 0x04, 0x00);
            return b;
        }
        public override bool ChipReset()
        {
            addLogLine("Performing a Beken CEN reset on D2 of CH341...");
            bool bOk = GPIO_CEN_CLR();
            if(bOk == false)
            {
                addLogLine("Failed to toggle CEN - is CH341 connected and ok?");
                return false;
            }
            Thread.Sleep(100);
            GPIO_CEN_SET();
            return true;
        }

        public bool BK_EnterSPIMode(byte data)
        {
            for (int x = 0; x < 10; x++)
            {
                byte[] sendBuf = new byte[25];
                for (int i = 0; i < 25; i++) sendBuf[i] = data;
                hd.Ch341SPI4Stream(sendBuf);
            }

            byte[] buf1 = new byte[4] { 0x9F, 0x00, 0x00, 0x00 };
            byte[] resp = hd.Ch341SPI4Stream(buf1);
            if (resp == null)
                return false;

            int zeroCount = 0;
            for (int i = 1; i < 4; i++)
                if (resp[i] == 0x00) zeroCount++;

            addLogLine("SPI Response: " + BitConverter.ToString(resp));
            bool bOk = resp[0] != 0x00 && zeroCount == 3;
            if (bOk)
            {
                // flush
                for (int x = 0; x < 1000; x++)
                {
                    byte[] sendBuf = new byte[25];
                    for (int i = 0; i < 25; i++)
                        sendBuf[i] = 0;
                    hd.Ch341SPI4Stream(sendBuf);
                }
            }
            return bOk;
        }
        public override bool Sync()
        {
            int loop = 0;
            while (true)
            {
                addLogLine("CH341 Beken sync attempt " + loop);
                loop++;
                bool bResetOk = ChipReset();
                if(bResetOk)
                {
                    bool bOk = BK_EnterSPIMode(0xD2);
                    if (bOk)
                    {
                        addLogLine("CH341 Beken sync OK!");
                        return true;
                    }
                }
                else
                {
                    // already printed errior in ChipReset
                }
                Thread.Sleep(1000);
            }
            return false;
        }
    }
}
