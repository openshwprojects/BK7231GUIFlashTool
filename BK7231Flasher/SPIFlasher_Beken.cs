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
        public void GPIO_CEN_SET()
        {
            CH341.CH341SetOutput(hd.usb_id, 0xFF, 0x04, 0x04);
        }
        public void GPIO_CEN_CLR()
        {
            CH341.CH341SetOutput(hd.usb_id, 0xFF, 0x04, 0x00);
        }
        public override void ChipReset()
        {
            addLogLine("Performing a Beken CEN reset on D2 of CH341...");
            GPIO_CEN_CLR();
            Thread.Sleep(100);
            GPIO_CEN_SET();
        }
        public override bool Sync()
        {
            int loop = 0;
            while (true)
            {
                addLogLine("CH341 Beken sync attempt " + loop);
                loop++;
                ChipReset();
                bool bOk = BK_EnterSPIMode(0xD2);
                if (bOk)
                {
                    addLogLine("CH341 Beken sync OK!");
                    return true;
                }
                Thread.Sleep(100);
            }
            return false;
        }
    }
}
