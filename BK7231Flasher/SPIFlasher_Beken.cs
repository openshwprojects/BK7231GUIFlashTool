using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
