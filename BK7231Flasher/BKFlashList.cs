using System;
using System.Collections.Generic;
using System.Text;

namespace BK7231Flasher
{
    public class BKFlash
    {
        public int mid;
        public string icName;
        public string manufacturer;
        public int szMem;
        public int szSR;
        public int cwUnp;
        public int cwEnp;
        public int cwMsk;
        public int sb;
        public int lb;
        public byte[] cwdRd;
        public byte[] cwdWr;

        public BKFlash(int mid, string icName, string manufacturer, int szMem, int szSR,
            int cwUnp, int cwEnp, int cwMsk, int sb, int lb, byte[] cwdRd, byte[] cwdWr)
        {
            this.mid = mid;
            this.icName = icName;
            this.manufacturer = manufacturer;
            this.szMem = szMem;
            this.szSR = szSR;
            this.cwUnp = cwUnp;
            this.cwEnp = cwEnp;
            this.cwMsk = cwMsk;
            this.sb = sb;
            this.lb = lb;
            this.cwdRd = cwdRd;
            this.cwdWr = cwdWr;
        }
        public override string ToString()
        {
            return string.Format("mid: {0:X}, icName: {1}, manufacturer: {2}, szMem: {3:X}, szSR: {4:X}, cwUnp: {5:X}, cwEnp: {6:X}, cwMsk: {7:X}, sb: {8:X}, lb: {9:X}, cwdRd: {10}, cwdWr: {11}",
                mid, icName, manufacturer, szMem, szSR, cwUnp, cwEnp, cwMsk, sb, lb, BitConverter.ToString(cwdRd), BitConverter.ToString(cwdWr));
        }



    }

    public class BKFlashList
    {
        private static BKFlashList _sing;
        public static BKFlashList Singleton
        {
            get
            {
                if(_sing == null)
                {
                    _sing = new BKFlashList();
                }
                return _sing;
            }
        }

        List<BKFlash> flashes = new List<BKFlash>();

        public static int FLASH_ID_XTX_25F08B=0x14405e ;//       # 芯天下flash,w+v:6.5s,e+w+v:11.5s
        public static int FLASH_ID_MXIC_25V8035F=0x1423c2   ;//  # 旺宏flash,w+v:8.2s,e+w+v:17.2s
        public static int FLASH_ID_XTX_25F04B=0x13311c    ;//    # 芯天下flash-4M
        public static int FLASH_ID_GD_25D40=0x134051     ;//     # GD flash-4M,w+v:3.1s,e+w+v:5.1s
        public static int FLASH_ID_GD_25D80=0x144051     ;//     # GD flash-8M，e+w+v=9.8s
        public static int FLASH_ID_GD_1_25D80=0x1440C8   ;//     # GD flash-8M，
        public static int FLASH_ID_Puya_25Q80=0x146085   ;//     # puya 8M,w+v:10.4s,e+w+v:11.3s,新版e+w+v：8.3s
        public static int FLASH_ID_Puya_25Q40=0x136085   ;//     # puya 4M,e+w+v:6s，新版w+v=4s，e+w+v=4.3s
        public static int FLASH_ID_Puya_25Q32H=0x166085 ;//      # puya 32M******暂时只用于脱机烧录器上7231的外挂flash
        public static int FLASH_ID_GD_25Q16=0x001540c8   ;//     # GD 16M******暂时只用于脱机烧录器上7231的外挂flash
        public static int FLASH_ID_GD_25Q16B=0x001540c8  ;//     # GD 16M******暂时只用于脱机烧录器上7231的外挂flash
        public static int FLASH_ID_XTX_25F16B=0x15400b  ;//      # XTX 16M
        public static int FLASH_ID_XTX_25F32B	=0x0016400b ;//    # xtx 32M******暂时只用于脱机烧录器上7231的外挂flash

        public static int FLASH_ID_MXIC_25V4035F=0x1323c2 ;//    # 旺宏flash,w+v:8.2s,e+w+v:17.2s
        public static int FLASH_ID_MXIC_25V1635F=0x1523c2 ;//    # 旺宏flash,w+v:8.2s,e+w+v:17.2s
        public static int FLASH_ID_GD_25Q41B=0x1340c8   ;//      # GD flash-4M,w+v:3.1s,e+w+v:5.1s
        public static int FLASH_ID_BY_PN25Q80A=0x1440e0 ;//      # GD flash-4M,w+v:3.1s,e+w+v:5.1s
        public static int FLASH_ID_BY_PN25Q40A=0x1340e0;//       # GD flash-4M,w+v:3.1s,e+w+v:5.1s

        public static int FLASH_ID_XTX_25Q64B	=0x0017600b ;//    # xtx 64M******暂时只用于脱机烧录器上7231的外挂flash
        public static int FLASH_ID_XTX_25F64B	=0x0017400b;//'     # xtx 64M******暂时只用于脱机烧录器上7231的外挂flash
        public static int FLASH_ID_Puya_25Q64H=0x00176085;
        public static int FLASH_ID_GD_25Q64=0x001740c8;
        public static int FLASH_ID_WB_25Q128JV=0x001840ef;
        public static int FLASH_ID_ESMT_25QH16B=0x0015701c;
        public static int FLASH_ID_GD_25WQ64E=0x001765c8;
        public static int FLASH_ID_GD_25WQ32E=0x001665c8;
        public static int FLASH_ID_GD_25WQ16E=0x001565c8;
        public static int FLASH_ID_TH25Q_16HB = 0x001560eb;
        public static int FLASH_ID_NA = 0x001640c8;

        public static int FLASH_ID_Puya_25Q16HB_K = 0x152085;

        public static int BFD(int v, int bs, int bl)
        {
            return (v & ((1 << (bl)) - 1)) << (bs);
        }
        public static int BIT(int v)
        {
            return 0x1 << v;
        }
        void addFlash(BKFlash flash)
        {
            flashes.Add(flash);
        }
        public BKFlash findFlashForMID(int mid)
        {
            foreach(var f in flashes)
            {
                if(f.mid == mid)
                {
                    return f;
                }
            }
            return null;
        }
        public BKFlashList()
        {
            addFlash(new BKFlash(FLASH_ID_XTX_25F08B, "PN25F08B", "xtx", 8 * 1024 * 1024, 1, 0x00, 0x07, BFD(0x0f, 2, 4), 2, 4, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX_25F04B, "PN25F04B", "xtx", 4 * 1024 * 1024, 1, 0x00, 0x07, BFD(0x0f, 2, 4), 2, 4, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD_25D40, "GD25D40", "GD", 4 * 1024 * 1024, 1, 0x00, 0x07, BFD(0x0f, 2, 3), 2, 3, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD_25D80, "GD25D80", "GD", 8 * 1024 * 1024, 1, 0x00, 0x07, BFD(0x0f, 2, 3), 2, 3, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD_1_25D80, "GD25D80", "GD", 8 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya_25Q80, "P25Q80", "Puya", 8 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya_25Q40, "P25Q40", "Puya", 4 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya_25Q32H, "P25Q32H", "Puya", 32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya_25Q64H, "P25Q64H", "Puya", 64 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff}, new byte[] { 0x01, 0xff, 0xff, 0xff}));


            addFlash(new BKFlash(FLASH_ID_XTX_25F16B, "XT25F16B", "xtx", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_GD_25Q16B, "GD25Q16B", "GD", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_MXIC_25V8035F, "MX25V8035F", "WH", 8 * 1024 * 1024, 2, 0x00, 0x07, BIT(12) | BFD(0x1f, 2, 4), 2, 5,    new byte[] {0x05, 0x15, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_MXIC_25V1635F, "MX25V1635F", "WH", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(12) | BFD(0x1f, 2, 4), 2, 5,    new byte[] {0x05, 0x15, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_XTX_25F32B, "XT25F32B", "xtx", 32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_GD_25Q41B, "GD25Q41B", "GD", 4 * 1024 * 1024, 1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_BY_PN25Q40A, "PN25Q40A", "BY", 4 * 1024 * 1024, 1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_BY_PN25Q80A, "PN25Q80A", "BY", 8 * 1024 * 1024, 1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_XTX_25F64B, "XT25F64B", "xtx", 64 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_XTX_25Q64B, "XT25Q64B", "xtx", 64 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_WB_25Q128JV, "WB25Q128JV", "WB", 128 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_ESMT_25QH16B, "EN25QH16B", "ESMT", 16 * 1024 * 1024, 1, 0x00, 0x07, BFD(0xf, 2, 5), 2, 4,    new byte[] {0x05, 0xff, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_TH25Q_16HB, "TH25Q_16HB", "TH", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_NA, "NA_NA", "NA", 32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_GD_25WQ16E, "GD25WQ16E", "GD", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_GD_25WQ32E, "GD25WQ32E", "GD", 32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_GD_25WQ64E, "GD25WQ64E", "GD", 64 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5,    new byte[] {0x05, 0x35, 0xff, 0xff},   new byte[] {0x01, 0xff, 0xff, 0xff}));
            addFlash(new BKFlash(FLASH_ID_Puya_25Q16HB_K, "P25Q16HB_K", "Puya", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

        }
    }
}
