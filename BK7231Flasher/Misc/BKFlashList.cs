using System;
using System.Collections.Generic;

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

        public static int FLASH_ID_XTX25F08B = 0x14405e; // 芯天下flash,w+v:6.5s,e+w+v:11.5s		
        public static int FLASH_ID_MXIC25V8035F = 0x1423c2; // 旺宏flash,w+v:8.2s,e+w+v:17.2s		
        public static int FLASH_ID_XTX25F04B = 0x13311c; // 芯天下flash-4M		
        public static int FLASH_ID_GD25D40 = 0x134051; // GD flash-4M,w+v:3.1s,e+w+v:5.1s		
        public static int FLASH_ID_GD25D80 = 0x144051; // GD flash-8M，e+w+v=9.8s		
        public static int FLASH_ID_GD125D80 = 0x1440c8; // GD flash-8M，		
        public static int FLASH_ID_Puya25Q80 = 0x146085; // puya 8M,w+v:10.4s,e+w+v:11.3s,新版e+w+v：8.3s		
        public static int FLASH_ID_Puya25Q40 = 0x136085; // puya 4M,e+w+v:6s，新版w+v=4s，e+w+v=4.3s		
        public static int FLASH_ID_Puya25Q32H = 0x166085; // puya 32M******暂时只用于脱机烧录器上7231的外挂flash	
        public static int FLASH_ID_GD25Q16B = 0x001540c8; // GD 16M******暂时只用于脱机烧录器上7231的外挂flash		
        public static int FLASH_ID_XTX25F16B = 0x15400b; // XTX 16M		
        public static int FLASH_ID_XTX25F32B = 0x0016400b; // xtx 32M******暂时只用于脱机烧录器上7231的外挂flash		
        public static int FLASH_ID_XTX25Q64B = 0x0017600b; // xtx 64M******暂时只用于脱机烧录器上7231的外挂flash		
        public static int FLASH_ID_XTX25F64B = 0x0017400b; // xtx 64M******暂时只用于脱机烧录器上7231的外挂flash		
        public static int FLASH_ID_XTX25Q128B = 0x0018600b; // xtx 128M		
        public static int FLASH_ID_XTX25F128F = 0x0018400b; // xtx 128M		
        public static int FLASH_ID_GT25Q16B = 0x1560c4; // 聚辰16M-Bits		
        public static int FLASH_ID_GD25WD80E = 0x1464c8; // GD flash-8M，		
        public static int FLASH_ID_GD25Q41BT = 0x1364c8; // GD flash-4M,w+v:3.1s,e+w+v:5.1s		
        public static int FLASH_ID_GD25LQ128E = 0x1860c8; // media		
        public static int FLASH_ID_GD25WQ128E = 0x1865c8; // media		
        public static int FLASH_ID_GD25LX256E = 0x1968c8;		
        public static int FLASH_ID_Puya25Q16HB = 0x154285; // puya 16M, previously FLASH_ID_Puya25Q80_38	
        public static int FLASH_ID_Puya25Q16SU = 0x156085; // puya 16M		
        public static int FLASH_ID_Puya25Q128HA = 0x182085; // puya 128M		
        public static int FLASH_ID_DSZB25LQ128C = 0x0018505e;		
        public static int FLASH_ID_ESMT25QE32A = 0x0016411c;		
        public static int FLASH_ID_ESMT25QW32A = 0x0016611c;		
        public static int FLASH_ID_TH25Q80HB = 0x001460cd;		
        public static int FLASH_ID_TH25D40HB = 0x001360cd;		
        public static int FLASH_ID_TH25Q16HB = 0x001560eb;		
        public static int FLASH_ID_XM25QU128C = 0x00184120;
        public static int FLASH_ID_Puya25Q64H=0x176085;
        public static int FLASH_ID_MXIC25V1635F=0x1523c2; //旺宏flash,w+v:8.2s,e+w+v:17.2s
        public static int FLASH_ID_GD25Q41B=0x1340c8;
        public static int FLASH_ID_BY_PN25Q40A=0x1340e0;
        public static int FLASH_ID_BY_PN25Q80A=0x1440e0;
        public static int FLASH_ID_WB25Q128JV=0x001840ef;
        public static int FLASH_ID_ESMT25QH16B=0x0015701c;
        public static int FLASH_ID_GD25WQ16E=0x001565c8;
        public static int FLASH_ID_GD25WQ32E=0x001665c8;
        public static int FLASH_ID_GD25WQ64E=0x001765c8;
        public static int FLASH_ID_Puya25Q16HBK = 0x152085;
        public static int FLASH_ID_GD_25Q32E = 0x001640c8; //  # GD flash-4M, renamed from FLASH_ID_NA
        public static int FLASH_ID_P25Q32HB = 0x162085; // py_p25q32HB
        public static int FLASH_ID_GT25Q05D = 0x001040c4;
        public static int FLASH_ID_GT25Q10D = 0x001140c4;
        public static int FLASH_ID_GT25Q20D = 0x001240c4;
        public static int FLASH_ID_PY25D22U = 0x00124485;
        public static int FLASH_ID_PY25D24U = 0x00124585;
        public static int FLASH_ID_UC25HQ20 = 0x001260b3;
        public static int FLASH_ID_TH25D20HA = 0x001260eb;
        public static int FLASH_ID_MX25V4035F = 0x001323c2;
        public static int FLASH_ID_GT25Q40D = 0x001340c4;
        public static int FLASH_ID_UC25HQ40 = 0x001360b3;
        public static int FLASH_ID_GD25Q41B_65H = 0x001365c8;
        public static int FLASH_ID_TH25D40UC = 0x001371cd;
        public static int FLASH_ID_BK = 0x00424b01;
        public static int FLASH_ID_MX25V80066 = 0x001420c2;
        public static int FLASH_ID_XT25F08F = 0x0014400b;
        public static int FLASH_ID_GD25WQ80E = 0x001465c8;
        public static int FLASH_ID_XT25F16B_1 = 0x0015404b;
        public static int FLASH_ID_TH25Q16UC = 0x001571cd;
        public static int FLASH_ID_XM25QH32D = 0x00164020;
        public static int FLASH_ID_TH25Q32UB = 0x001660cd;
        public static int FLASH_ID_PY25Q32LB = 0x00166585;
        public static int FLASH_ID_PY25Q64HA = 0x00172085;
        public static int FLASH_ID_XM25QH64D = 0x00174020;
        public static int FLASH_ID_GD25Q64C = 0x001740c8;
        public static int FLASH_ID_TH25Q64HB = 0x001760cd;
        public static int FLASH_ID_TH25Q64HA = 0x001760eb;
        public static int FLASH_ID_XM25QH128D = 0x00184020;
        public static int FLASH_ID_BY25Q128ES = 0x00184068;
        public static int FLASH_ID_GT25Q128EZ = 0x001840c4;
        public static int FLASH_ID_GD25Q128C = 0x001840c8;
        public static int FLASH_ID_BY25Q128EL = 0x00186068;
        public static int FLASH_ID_FM25LQ128I = 0x001860a1;
        public static int FLASH_ID_WB25Q128JW_IQ = 0x001860ef;
        public static int FLASH_ID_PY25Q128LA = 0x00186585;
        public static int FLASH_ID_WB25Q128JW_IM = 0x001880ef;
        public static int FLASH_ID_XM25QH256D = 0x00194020;
        public static int FLASH_ID_GD25Q256C = 0x001940c8;
        public static int FLASH_ID_GD25LQ256E = 0x001960c8;

        public static int FLASH_ID_GD25Q512C = 0x001a47c8;

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
            addFlash(new BKFlash(FLASH_ID_GT25Q05D,     "GT25Q05D",   "GT",   64 * 1024,        2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GT25Q10D,     "GT25Q10D",   "GT",   128 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GT25Q20D,     "GT25Q20D",   "GT",   256 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_PY25D22U,     "PY25D22U",   "PY",   256 * 1024,       1, 0x00, 0x07, BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_PY25D24U,     "PY25D24U",   "PY",   256 * 1024,       1, 0x00, 0x07, BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_UC25HQ20,     "UC25HQ20",   "UC",   256 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25D20HA,    "TH25D20HA",  "TH",   256 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_BY_PN25Q40A,  "PN25Q40A",   "BY",   512 * 1024,       1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25D40,      "GD25D40",    "GD",   512 * 1024,       1, 0x00, 0x07, BFD(0x0f, 2, 3),           2, 3, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q41BT,    "GD25Q41BT",  "GD",   512 * 1024,       1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q41B,     "GD25Q41B",   "GD",   512 * 1024,       1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q40,    "P25Q40",     "Puya", 512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25D40HB,    "TH25D40HB",  "TH",   512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F04B,    "PN25F04B",   "xtx",  512 * 1024,       1, 0x00, 0x07, BFD(0x0f, 2, 4),           2, 4, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_MX25V4035F,   "MX25V4035F", "MXIC", 512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GT25Q40D,     "GT25Q40D",   "GT",   512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_UC25HQ40,     "UC25HQ40",   "UC",   512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q41B_65H, "GD25Q41B",   "GD",   512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25D40UC,    "TH25D40UC",  "TH",   512 * 1024,       2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_BK,           "BK",         "BK",   512 * 1024,       0, 0x00, 0x00, 0x00, 0, 0, new byte[] { 0xff, 0xff, 0xff, 0xff }, new byte[] { 0xff, 0xff, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_BY_PN25Q80A,  "PN25Q80A",   "BY",   1 * 1024 * 1024,  1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 3), 2, 3, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25WD80E,    "GD25WD80E",  "GD",   1 * 1024 * 1024,  1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25D80,      "GD25D80",    "GD",   1 * 1024 * 1024,  1, 0x00, 0x07, BFD(0x0f, 2, 3),           2, 3, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD125D80,     "GD25D80",    "GD",   1 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q80,    "P25Q80",     "Puya", 1 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q80HB,    "TH25Q80HB",  "TH",   1 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_MXIC25V8035F, "MX25V8035F", "WH",   1 * 1024 * 1024,  2, 0x00, 0x07, BIT(12) | BFD(0x1f, 2, 4), 2, 5, new byte[] { 0x05, 0x15, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F08B,    "PN25F08B",   "xtx",  1 * 1024 * 1024,  1, 0x00, 0x07, BFD(0x0f, 2, 4),           2, 4, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_MX25V80066,   "MX25V80066", "MXIC", 1 * 1024 * 1024,  1, 0x00, 0x07, BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XT25F08F,     "XT25F08F",   "XTX",  1 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25WQ80E,    "GD25WQ80E",  "GD",   1 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_ESMT25QH16B,  "EN25QH16B",  "ESMT", 2 * 1024 * 1024,  1, 0x00, 0x07, BFD(0xf, 2, 5),            2, 4, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q16B,     "GD25Q16B",   "GD",   2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25WQ16E,    "GD25WQ16E",  "GD",   2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GT25Q16B,     "GT25Q16B",   "GT",   2 * 1024 * 1024,  3, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0x15, 0xff }, new byte[] { 0x01, 0x31, 0x11, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q16HB,  "P25Q16HB",   "Puya", 2 * 1024 * 1024,  1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q16HBK, "P25Q16HBK",  "Puya", 2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q16SU,  "P25Q16SU",   "Puya", 2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q16HB,    "TH25Q16HB",  "TH",   2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_MXIC25V1635F, "MX25V1635F", "WH",   2 * 1024 * 1024,  2, 0x00, 0x07, BIT(12) | BFD(0x1f, 2, 4), 2, 5, new byte[] { 0x05, 0x15, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F16B,    "XT25F16B",   "xtx",  2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XT25F16B_1,   "XT25F16B_1", "XTX",  2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q16UC,    "TH25Q16UC",  "TH",   2 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_ESMT25QE32A,  "EN25QE32A",  "ESMT", 4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_ESMT25QW32A,  "EN25QW32A",  "ESMT", 4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25WQ32E,    "GD25WQ32E",  "GD",   4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD_25Q32E,    "GD25Q32E",   "GD",   4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q32H,   "P25Q32H",    "Puya", 4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_P25Q32HB,     "P25Q32HB",   "Puya", 4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F32B,    "XT25F32B",   "xtx",  4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XM25QH32D,    "XM25QH32D",  "XM",   4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q32UB,    "TH25Q32UB",  "TH",   4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_PY25Q32LB,    "PY25Q32LB",  "PY",   4 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GD25WQ64E,    "GD25WQ64E",  "GD",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q64H,   "P25Q64H",    "Puya", 8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25Q64B,    "XT25Q64B",   "xtx",  8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F64B,    "XT25F64B",   "xtx",  8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_PY25Q64HA,    "PY25Q64HA",  "PY",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XM25QH64D,    "XM25QH64D",  "XM",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q64C,     "GD25Q64C",   "GD",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q64HB,    "TH25Q64HB",  "TH",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_TH25Q64HA,    "TH25Q64HA",  "TH",   8 * 1024 * 1024,  2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GD25LQ128E,   "GD25LQ128E", "GD",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25WQ128E,   "GD25WQ128E", "GD",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_Puya25Q128HA, "P25Q128HA",  "Puya", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff })); 
            addFlash(new BKFlash(FLASH_ID_WB25Q128JV,   "WB25Q128JV", "WB",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XM25QU128C,   "XM25QU128C", "XMC",  16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25F128F,   "XT25F128F",  "xtx",  16 * 1024 * 1024, 3, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0x15, 0xff }, new byte[] { 0x01, 0x31, 0x11, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XTX25Q128B,   "XT25Q128B",  "xtx",  16 * 1024 * 1024, 3, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0x15, 0xff }, new byte[] { 0x01, 0x31, 0x11, 0xff }));
            addFlash(new BKFlash(FLASH_ID_DSZB25LQ128C, "ZB25LQ128C", "Zbit", 16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0x15, 0xff }, new byte[] { 0x01, 0x31, 0x11, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XM25QH128D,   "XM25QH128D", "XM",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_BY25Q128ES,   "BY25Q128ES", "BY",   16 * 1024 * 1024, 2, 0x00, 0x07, BFD(0x1f, 2, 5) | BFD(0x1f, 10, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GT25Q128EZ,   "GT25Q128EZ", "GT",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q128C,    "GD25Q128C",  "GD",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_BY25Q128EL,   "BY25Q128EL", "BY",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_FM25LQ128I,   "FM25LQ128I", "FM",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_WB25Q128JW_IQ, "WB25Q128JW_IQ", "WB",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_PY25Q128LA,   "PY25Q128LA", "PY",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_WB25Q128JW_IM, "WB25Q128JW_IM", "WB",   16 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GD25LX256E,   "GD25LX256E", "GD",   32 * 1024 * 1024, 1, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0xff, 0xff, 0xff }, new byte[] { 0x01, 0xff, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_XM25QH256D,   "XM25QH256D", "XM",   32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25Q256C,    "GD25Q256C",  "GD",   32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
            addFlash(new BKFlash(FLASH_ID_GD25LQ256E,   "GD25LQ256E", "GD",   32 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));

            addFlash(new BKFlash(FLASH_ID_GD25Q512C,    "GD25Q512C",  "GD",   64 * 1024 * 1024, 2, 0x00, 0x07, BIT(14) | BFD(0x1f, 2, 5), 2, 5, new byte[] { 0x05, 0x35, 0xff, 0xff }, new byte[] { 0x01, 0x31, 0xff, 0xff }));
        }
	}
}
