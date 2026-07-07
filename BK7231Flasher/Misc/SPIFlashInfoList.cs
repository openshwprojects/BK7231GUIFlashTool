using System;
using System.Collections.Generic;

namespace BK7231Flasher
{
    public class SPIFlashInfo
    {
        public int mid { get; private set; }
        public string icName { get; private set; }
        public string manufacturer { get; private set; }
        public int szMem { get; private set; }

        public SPIFlashInfo(int mid, string icName, string manufacturer, int szMem)
        {
            this.mid = mid;
            this.icName = icName;
            this.manufacturer = manufacturer;
            this.szMem = szMem;
        }

        public override string ToString()
        {
            return string.Format("mid: {0:X6}, icName: {1}, manufacturer: {2}, szMem: {3:X} ({4} KB)",
                mid, icName, manufacturer, szMem, szMem / 1024);
        }
    }

    public class SPIFlashInfoList
    {
        private const int KB = 1024;
        private const int MB = 1024 * KB;

        private static SPIFlashInfoList _sing;
        public static SPIFlashInfoList Singleton
        {
            get
            {
                if(_sing == null)
                {
                    _sing = new SPIFlashInfoList();
                }
                return _sing;
            }
        }

        private readonly Dictionary<int, SPIFlashInfo> flashes = new Dictionary<int, SPIFlashInfo>();

        private void addFlash(SPIFlashInfo flash)
        {
            if(flashes.ContainsKey(flash.mid))
            {
                throw new ArgumentException("Duplicate SPI flash JEDEC ID " + flash.mid.ToString("X6"));
            }
            flashes.Add(flash.mid, flash);
        }

        public SPIFlashInfo findFlashForMID(int mid)
        {
            SPIFlashInfo flash;
            if(flashes.TryGetValue(mid, out flash))
            {
                return flash;
            }
            return null;
        }

        private SPIFlashInfoList()
        {
            // Info-only SPI NOR identifiers sourced from imported chip database entries.
            // Keep this separate from BKFlashList: it does not define erase, write, or unprotect behaviour.
            addFlash(new SPIFlashInfo(0x00202037, "A25L05PT", "AMIC", 64 * KB));
            addFlash(new SPIFlashInfo(0x00212037, "A25L10PT", "AMIC", 128 * KB));
            addFlash(new SPIFlashInfo(0x00112037, "A25L10PU", "AMIC", 128 * KB));
            addFlash(new SPIFlashInfo(0x00252037, "A25L16PT", "AMIC", 2 * MB));
            addFlash(new SPIFlashInfo(0x00222037, "A25L20PT", "AMIC", 256 * KB));
            addFlash(new SPIFlashInfo(0x00122037, "A25L20PU", "AMIC", 256 * KB));
            addFlash(new SPIFlashInfo(0x00232037, "A25L40PT", "AMIC", 512 * KB));
            addFlash(new SPIFlashInfo(0x00132037, "A25L40PU", "AMIC", 512 * KB));
            addFlash(new SPIFlashInfo(0x00103037, "A25L512", "AMIC", 64 * KB));
            addFlash(new SPIFlashInfo(0x00242037, "A25L80PT", "AMIC", 1 * MB));
            addFlash(new SPIFlashInfo(0x00142037, "A25L80PU", "AMIC", 1 * MB));
            addFlash(new SPIFlashInfo(0x00154037, "A25LQ16", "AMIC", 2 * MB));
            addFlash(new SPIFlashInfo(0x00164037, "A25LQ32A", "AMIC", 4 * MB));
            addFlash(new SPIFlashInfo(0x001860e0, "ACE25A128G_1.8V", "ACE", 16 * MB));
            addFlash(new SPIFlashInfo(0x001031a1, "ACE25C512", "ACE", 64 * KB));
            addFlash(new SPIFlashInfo(0x0000431f, "AT25DF021", "Atmel", 256 * KB));
            addFlash(new SPIFlashInfo(0x0000481f, "AT25DF641", "Atmel", 8 * MB));
            addFlash(new SPIFlashInfo(0x0000401f, "AT25DN256", "Adesto", 32 * KB));
            addFlash(new SPIFlashInfo(0x0000651f, "AT25F512B", "Atmel", 64 * KB));
            addFlash(new SPIFlashInfo(0x0000841f, "AT25SF041", "Atmel", 512 * KB));
            addFlash(new SPIFlashInfo(0x0000041f, "AT26F004", "Atmel", 512 * KB));
            addFlash(new SPIFlashInfo(0x0016329b, "ATO25Q32", "ATO", 4 * MB));
            addFlash(new SPIFlashInfo(0x001540e0, "BG25Q16A", "Berg Micro", 2 * MB));
            addFlash(new SPIFlashInfo(0x001640e0, "BG25Q32A", "Berg Micro", 4 * MB));
            addFlash(new SPIFlashInfo(0x00164068, "BH25Q32", "Bohong", 4 * MB));
            addFlash(new SPIFlashInfo(0x00144068, "BY25D80", "Boya", 1 * MB));
            addFlash(new SPIFlashInfo(0x001560e0, "BY25Q16AL", "Boya Micro", 2 * MB));
            addFlash(new SPIFlashInfo(0x00174068, "BY25Q64AS", "Boya Micro", 8 * MB));
            addFlash(new SPIFlashInfo(0x00174054, "DQ25Q64A", "Douqi", 8 * MB));
            addFlash(new SPIFlashInfo(0x0010201c, "EN25B05", "Eon", 64 * KB));
            addFlash(new SPIFlashInfo(0x0015311c, "EN25F16", "Eon", 2 * MB));
            addFlash(new SPIFlashInfo(0x0016311c, "EN25F32", "Eon", 4 * MB));
            addFlash(new SPIFlashInfo(0x0017311c, "EN25F64", "Eon", 8 * MB));
            addFlash(new SPIFlashInfo(0x0014311c, "EN25F80", "Eon", 1 * MB));
            addFlash(new SPIFlashInfo(0x0018301c, "EN25Q128", "Eon", 16 * MB));
            addFlash(new SPIFlashInfo(0x0015301c, "EN25Q16A", "Eon", 2 * MB));
            addFlash(new SPIFlashInfo(0x0013301c, "EN25Q40", "Eon", 512 * KB));
            addFlash(new SPIFlashInfo(0x0017301c, "EN25Q64", "Eon", 8 * MB));
            addFlash(new SPIFlashInfo(0x0014301c, "EN25Q80A", "Eon", 1 * MB));
            addFlash(new SPIFlashInfo(0x0018701c, "EN25QH128", "Eon", 16 * MB));
            addFlash(new SPIFlashInfo(0x0015701c, "EN25QH16B", "ESMT", 2 * MB));
            addFlash(new SPIFlashInfo(0x0019701c, "EN25QH256", "Eon", 32 * MB));
            addFlash(new SPIFlashInfo(0x0016411c, "EN25QH32A", "ESMT", 4 * MB));
            addFlash(new SPIFlashInfo(0x0016611c, "EN25QH32A", "ESMT", 4 * MB));
            addFlash(new SPIFlashInfo(0x0017701c, "EN25QH64", "Eon", 8 * MB));
            addFlash(new SPIFlashInfo(0x0018711c, "EN25QX128A", "Eon", 16 * MB));
            addFlash(new SPIFlashInfo(0x0013381c, "EN25S40_1.8V", "Eon", 512 * KB));
            addFlash(new SPIFlashInfo(0x0015511c, "EN25T16", "Eon", 2 * MB));
            addFlash(new SPIFlashInfo(0x0014511c, "EN25T80", "Eon", 1 * MB));
            addFlash(new SPIFlashInfo(0x0015324a, "ES25M16A", "ExcelSemi", 2 * MB));
            addFlash(new SPIFlashInfo(0x0013324a, "ES25M40A", "ExcelSemi", 512 * KB));
            addFlash(new SPIFlashInfo(0x0014324a, "ES25M80A", "ExcelSemi", 1 * MB));
            addFlash(new SPIFlashInfo(0x0011204a, "ES25P10", "ExcelSemi", 128 * KB));
            addFlash(new SPIFlashInfo(0x0015204a, "ES25P16", "ExcelSemi", 2 * MB));
            addFlash(new SPIFlashInfo(0x0012204a, "ES25P20", "ExcelSemi", 256 * KB));
            addFlash(new SPIFlashInfo(0x0016204a, "ES25P32", "ExcelSemi", 4 * MB));
            addFlash(new SPIFlashInfo(0x0013204a, "ES25P40", "ExcelSemi", 512 * KB));
            addFlash(new SPIFlashInfo(0x0014204a, "ES25P80", "ExcelSemi", 1 * MB));
            addFlash(new SPIFlashInfo(0x0013208c, "F25L004A", "ESMT", 512 * KB));
            addFlash(new SPIFlashInfo(0x008c8c8c, "F25L04UA", "EFST", 512 * KB));
            addFlash(new SPIFlashInfo(0x0016208c, "F25L32P", "EFST", 4 * MB));
            addFlash(new SPIFlashInfo(0x0016408c, "F25L32Q", "EFST", 4 * MB));
            addFlash(new SPIFlashInfo(0x0017418c, "F25L64QA", "ESMT", 8 * MB));
            addFlash(new SPIFlashInfo(0x0013308c, "F25S04P", "EFST", 512 * KB));
            addFlash(new SPIFlashInfo(0x001340a1, "FM25Q04A", "Fudan Microelectronics", 512 * KB));
            addFlash(new SPIFlashInfo(0x001432f8, "FM25Q08A", "Fidelix", 1 * MB));
            addFlash(new SPIFlashInfo(0x001840a1, "FM25Q128", "Fudan Microelectronics", 16 * MB));
            addFlash(new SPIFlashInfo(0x001640a1, "FM25Q32", "Fudan Microelectronics", 4 * MB));
            addFlash(new SPIFlashInfo(0x001632f8, "FM25Q32A", "Fidelix", 4 * MB));
            addFlash(new SPIFlashInfo(0x001732f8, "FM25Q64A", "Fidelix", 8 * MB));
            addFlash(new SPIFlashInfo(0x0012400e, "FT25H02", "FMD", 256 * KB));
            addFlash(new SPIFlashInfo(0x0015400e, "FT25H16", "Fremont", 2 * MB));
            addFlash(new SPIFlashInfo(0x001330c8, "GD25D40", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x00134051, "GD25D40", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x001430c8, "GD25D80", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x00144051, "GD25D80", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x001440c8, "GD25D80", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x001320c8, "GD25F40", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x001420c8, "GD25F80", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x001260c8, "GD25LQ20C_1.8V", "GigaDevice", 256 * KB));
            addFlash(new SPIFlashInfo(0x001140c8, "GD25Q10", "GigaDevice", 128 * KB));
            addFlash(new SPIFlashInfo(0x001540c8, "GD25Q16B", "GigaDevice", 2 * MB));
            addFlash(new SPIFlashInfo(0x001240c8, "GD25Q20", "GigaDevice", 256 * KB));
            addFlash(new SPIFlashInfo(0x001640c8, "GD25Q32E", "GigaDevice", 4 * MB));
            addFlash(new SPIFlashInfo(0x001340c8, "GD25Q41B", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x001040c8, "GD25Q512", "GigaDevice", 64 * KB));
            addFlash(new SPIFlashInfo(0x001565c8, "GD25WQ16E", "GigaDevice", 2 * MB));
            addFlash(new SPIFlashInfo(0x001665c8, "GD25WQ32E", "GigaDevice", 4 * MB));
            addFlash(new SPIFlashInfo(0x001765c8, "GD25WQ64E", "GigaDevice", 8 * MB));
            addFlash(new SPIFlashInfo(0x0014609d, "IS25LP080D", "ISSI", 1 * MB));
            addFlash(new SPIFlashInfo(0x0009409d, "IS25LQ025", "ISSI", 32 * KB));
            addFlash(new SPIFlashInfo(0x0012709d, "IS25WP020D_1.8V", "ISSI", 256 * KB));
            addFlash(new SPIFlashInfo(0x0013709d, "IS25WP040D_1.8V", "ISSI", 512 * KB));
            addFlash(new SPIFlashInfo(0x0014709d, "IS25WP080D_1.8V", "ISSI", 1 * MB));
            addFlash(new SPIFlashInfo(0x001526c2, "KH25L8036D", "KHIC", 1 * MB));
            addFlash(new SPIFlashInfo(0x00182020, "M25P128_ST25P28V6G", "Numonyx", 16 * MB));
            addFlash(new SPIFlashInfo(0x00118020, "M25PE10", "Numonyx", 128 * KB));
            addFlash(new SPIFlashInfo(0x00128020, "M25PE20", "Numonyx", 256 * KB));
            addFlash(new SPIFlashInfo(0x00138020, "M25PE40", "Numonyx", 512 * KB));
            addFlash(new SPIFlashInfo(0x00157120, "M25PX16", "ST", 2 * MB));
            addFlash(new SPIFlashInfo(0x00167120, "M25PX32", "ST", 4 * MB));
            addFlash(new SPIFlashInfo(0x00177120, "M25PX64", "ST", 8 * MB));
            addFlash(new SPIFlashInfo(0x00147120, "M25PX80", "ST", 1 * MB));
            addFlash(new SPIFlashInfo(0x00104020, "M45PE05", "ST", 64 * KB));
            addFlash(new SPIFlashInfo(0x00154051, "MD25D16", "GigaDevice", 2 * MB));
            addFlash(new SPIFlashInfo(0x00124051, "MD25D20", "GigaDevice", 256 * KB));
            addFlash(new SPIFlashInfo(0x001431c8, "MD25T80", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x0018ba20, "MT25QL128AB", "Micron", 16 * MB));
            addFlash(new SPIFlashInfo(0x0019bb20, "MT25QU256", "Micron", 32 * MB));
            addFlash(new SPIFlashInfo(0x001a20c2, "MX25L51245G", "Macronix", 64 * MB));
            addFlash(new SPIFlashInfo(0x001128c2, "MX25R1035F", "Macronix", 128 * KB));
            addFlash(new SPIFlashInfo(0x001528c2, "MX25R1635F", "Macronix", 2 * MB));
            addFlash(new SPIFlashInfo(0x001228c2, "MX25R2035F", "Macronix", 256 * KB));
            addFlash(new SPIFlashInfo(0x001628c2, "MX25R3235F", "Macronix", 4 * MB));
            addFlash(new SPIFlashInfo(0x001328c2, "MX25R4035F", "Macronix", 512 * KB));
            addFlash(new SPIFlashInfo(0x001028c2, "MX25R512F", "Macronix", 64 * KB));
            addFlash(new SPIFlashInfo(0x001728c2, "MX25R6435F", "Macronix", 8 * MB));
            addFlash(new SPIFlashInfo(0x001428c2, "MX25R8035F", "Macronix", 1 * MB));
            addFlash(new SPIFlashInfo(0x003125c2, "MX25U1001E_1.8V", "Macronix", 128 * KB));
            addFlash(new SPIFlashInfo(0x001825c2, "MX25U12835F_1.8V", "Macronix", 16 * MB));
            addFlash(new SPIFlashInfo(0x003825c2, "MX25U12873F_1.8V", "Macronix", 16 * MB));
            addFlash(new SPIFlashInfo(0x003025c2, "MX25U5121E_1.8V", "Macronix", 64 * KB));
            addFlash(new SPIFlashInfo(0x003a80c2, "MX25UM51245G", "Macronix", 64 * MB));
            addFlash(new SPIFlashInfo(0x001123c2, "MX25V1035F", "Macronix", 128 * KB));
            addFlash(new SPIFlashInfo(0x001523c2, "MX25V1635F", "Macronix", 2 * MB));
            addFlash(new SPIFlashInfo(0x001223c2, "MX25V2035F", "Macronix", 256 * KB));
            addFlash(new SPIFlashInfo(0x005325c2, "MX25V4035", "Macronix", 512 * KB));
            addFlash(new SPIFlashInfo(0x001023c2, "MX25V512F", "Macronix", 64 * KB));
            addFlash(new SPIFlashInfo(0x005425c2, "MX25V8035", "Macronix", 1 * MB));
            addFlash(new SPIFlashInfo(0x001423c2, "MX25V8035F", "Macronix", 1 * MB));
            addFlash(new SPIFlashInfo(0x003a25c2, "MX66U51235F_1.8V", "Macronix", 64 * MB));
            addFlash(new SPIFlashInfo(0x001875c2, "MX77L12850F", "Macronix", 16 * MB));
            addFlash(new SPIFlashInfo(0x0015ba20, "N25Q016", "Micron", 2 * MB));
            addFlash(new SPIFlashInfo(0x0016ba20, "N25Q032A", "Micron", 4 * MB));
            addFlash(new SPIFlashInfo(0x0017ba20, "N25Q064A", "Micron", 8 * MB));
            addFlash(new SPIFlashInfo(0x001130d5, "N25S10", "Nantronics", 128 * KB));
            addFlash(new SPIFlashInfo(0x001530d5, "N25S16", "Nantronics", 2 * MB));
            addFlash(new SPIFlashInfo(0x001230d5, "N25S20", "Nantronics", 256 * KB));
            addFlash(new SPIFlashInfo(0x001630d5, "N25S32", "Nantronics", 4 * MB));
            addFlash(new SPIFlashInfo(0x001330d5, "N25S40", "Nantronics", 512 * KB));
            addFlash(new SPIFlashInfo(0x001430d5, "N25S80", "Nantronics", 1 * MB));
            addFlash(new SPIFlashInfo(0x0019cb2c, "N25W256A11", "Micron", 32 * MB));
            addFlash(new SPIFlashInfo(0x007c7f9d, "NX25P10", "NexFlash", 128 * KB));
            addFlash(new SPIFlashInfo(0x007d7f9d, "NX25P20", "NexFlash", 256 * KB));
            addFlash(new SPIFlashInfo(0x007e7f9d, "NX25P40", "NexFlash", 512 * KB));
            addFlash(new SPIFlashInfo(0x00137f9d, "NX25P80", "NexFlash", 1 * MB));
            addFlash(new SPIFlashInfo(0x00152085, "P25Q16HB-K", "Puya", 2 * MB));
            addFlash(new SPIFlashInfo(0x00166085, "P25Q32H", "Puya", 4 * MB));
            addFlash(new SPIFlashInfo(0x00136085, "P25Q40", "Puya", 512 * KB));
            addFlash(new SPIFlashInfo(0x00176085, "P25Q64H", "Puya", 8 * MB));
            addFlash(new SPIFlashInfo(0x00146085, "P25Q80", "Puya", 1 * MB));
            addFlash(new SPIFlashInfo(0x00154285, "P25Q80", "Puya", 2 * MB));
            addFlash(new SPIFlashInfo(0x000049bf, "PCT25VF010A", "PCT", 128 * KB));
            addFlash(new SPIFlashInfo(0x000044bf, "PCT25VF040A", "PCT", 512 * KB));
            addFlash(new SPIFlashInfo(0x00219d7f, "Pm25LD010", "PFlash", 128 * KB));
            addFlash(new SPIFlashInfo(0x002f9d7f, "Pm25LD256C", "PFlash", 32 * KB));
            addFlash(new SPIFlashInfo(0x007c9d7f, "Pm25LV010", "PFlash", 128 * KB));
            addFlash(new SPIFlashInfo(0x00229d7f, "Pm25LV020", "PFlash", 256 * KB));
            addFlash(new SPIFlashInfo(0x007e9d7f, "Pm25LV040", "PFlash", 512 * KB));
            addFlash(new SPIFlashInfo(0x007d9d7f, "Pm25W020", "PFlash", 256 * KB));
            addFlash(new SPIFlashInfo(0x001340e0, "PN25Q40A", "Boya/Bohong Microelectronics", 512 * KB));
            addFlash(new SPIFlashInfo(0x001440e0, "PN25Q80A", "Boya/Bohong Microelectronics", 1 * MB));
            addFlash(new SPIFlashInfo(0x00118989, "QB25F016S33B", "Intel", 2 * MB));
            addFlash(new SPIFlashInfo(0x00138989, "QB25F640S33B", "Intel", 8 * MB));
            addFlash(new SPIFlashInfo(0x00100201, "S25FL001D", "Spansion", 128 * KB));
            addFlash(new SPIFlashInfo(0x00110201, "S25FL002D", "Spansion", 256 * KB));
            addFlash(new SPIFlashInfo(0x00140201, "S25FL016A", "Spansion", 2 * MB));
            addFlash(new SPIFlashInfo(0x00260201, "S25FL040A_BOT", "Spansion", 512 * KB));
            addFlash(new SPIFlashInfo(0x00250201, "S25FL040A_TOP", "Spansion", 512 * KB));
            addFlash(new SPIFlashInfo(0x00154001, "S25FL116K", "Spansion", 2 * MB));
            addFlash(new SPIFlashInfo(0x00164001, "S25FL132K", "Spansion", 4 * MB));
            addFlash(new SPIFlashInfo(0x00174001, "S25FL164K", "Spansion", 8 * MB));
            addFlash(new SPIFlashInfo(0x00190201, "S25FL256S", "Spansion", 32 * MB));
            addFlash(new SPIFlashInfo(0x008c25bf, "SST25VF020B", "SST", 256 * KB));
            addFlash(new SPIFlashInfo(0x004b25bf, "SST25VF064C", "SST", 8 * MB));
            addFlash(new SPIFlashInfo(0x005426bf, "SST26WF040B", "SST", 512 * KB));
            addFlash(new SPIFlashInfo(0x001560eb, "TH25Q-16HB", "Tsingteng Microsystem", 2 * MB));
            addFlash(new SPIFlashInfo(0x001460cd, "TH25Q-80HB", "Tsingteng Microsystem", 1 * MB));
            addFlash(new SPIFlashInfo(0x001971ef, "W25M512JV", "Winbond", 64 * MB));
            addFlash(new SPIFlashInfo(0x000010ef, "W25P10", "Winbond", 128 * KB));
            addFlash(new SPIFlashInfo(0x000011ef, "W25P20", "Winbond", 256 * KB));
            addFlash(new SPIFlashInfo(0x000012ef, "W25P40", "Winbond", 512 * KB));
            addFlash(new SPIFlashInfo(0x001720ef, "W25P64", "Winbond", 8 * MB));
            addFlash(new SPIFlashInfo(0x001420ef, "W25P80", "Winbond", 1 * MB));
            addFlash(new SPIFlashInfo(0x001160ef, "W25Q10EW_1.8V", "Winbond", 128 * KB));
            addFlash(new SPIFlashInfo(0x001840ef, "W25Q128JV", "Winbond", 16 * MB));
            addFlash(new SPIFlashInfo(0x001870ef, "W25Q128JV", "Winbond", 16 * MB));
            addFlash(new SPIFlashInfo(0x001560ef, "W25Q16FW_1.8V", "Winbond", 2 * MB));
            addFlash(new SPIFlashInfo(0x001240ef, "W25Q20CL", "Winbond", 256 * KB));
            addFlash(new SPIFlashInfo(0x001260ef, "W25Q20EW_1.8V", "Winbond", 256 * KB));
            addFlash(new SPIFlashInfo(0x001970ef, "W25Q256JV-IM-JM", "Winbond", 32 * MB));
            addFlash(new SPIFlashInfo(0x001660ef, "W25Q32FW_1.8V", "Winbond", 4 * MB));
            addFlash(new SPIFlashInfo(0x001360ef, "W25Q40EW_1.8V", "Winbond", 512 * KB));
            addFlash(new SPIFlashInfo(0x001760ef, "W25Q64FW", "Winbond", 8 * MB));
            addFlash(new SPIFlashInfo(0x001770ef, "W25Q64JV-xM", "Winbond", 8 * MB));
            addFlash(new SPIFlashInfo(0x001780ef, "W25Q64JW-IM_1.8V", "Winbond", 8 * MB));
            addFlash(new SPIFlashInfo(0x001740ef, "W25Q64xV", "Winbond", 8 * MB));
            addFlash(new SPIFlashInfo(0x001450ef, "W25Q80BW_1.8V", "Winbond", 1 * MB));
            addFlash(new SPIFlashInfo(0x001460ef, "W25Q80EW_1.8V", "Winbond", 1 * MB));
            addFlash(new SPIFlashInfo(0x00114020, "XM25QH10B", "XMC", 128 * KB));
            addFlash(new SPIFlashInfo(0x00124020, "XM25QH20B", "XMC", 256 * KB));
            addFlash(new SPIFlashInfo(0x00164020, "XM25QH32B", "XMC", 4 * MB));
            addFlash(new SPIFlashInfo(0x00134020, "XM25QH40B", "XMC", 512 * KB));
            addFlash(new SPIFlashInfo(0x00144020, "XM25QH80B", "XMC", 1 * MB));
            addFlash(new SPIFlashInfo(0x00135020, "XM25QU41B_1.8V", "XMC", 512 * KB));
            addFlash(new SPIFlashInfo(0x00145020, "XM25QU80B_1.8V", "XMC", 1 * MB));
            addFlash(new SPIFlashInfo(0x00184220, "XM25QW128C", "XMC", 16 * MB));
            addFlash(new SPIFlashInfo(0x00154220, "XM25QW16C", "XMC", 2 * MB));
            addFlash(new SPIFlashInfo(0x00194220, "XM25QW256C", "XMC", 32 * MB));
            addFlash(new SPIFlashInfo(0x00164220, "XM25QW32C", "XMC", 4 * MB));
            addFlash(new SPIFlashInfo(0x00204220, "XM25QW512C", "XMC", 64 * MB));
            addFlash(new SPIFlashInfo(0x00174220, "XM25QW64C", "XMC", 8 * MB));
            addFlash(new SPIFlashInfo(0x0011400b, "XT25F01B", "XTX Technology Limited", 128 * KB));
            addFlash(new SPIFlashInfo(0x0013311c, "XT25F04B", "XTX Technology Limited", 512 * KB));
            addFlash(new SPIFlashInfo(0x0014405e, "XT25F08B", "XTX Technology Limited", 1 * MB));
            addFlash(new SPIFlashInfo(0x0015400b, "XT25F16B", "XTX Technology Limited", 2 * MB));
            addFlash(new SPIFlashInfo(0x0016400b, "XT25F32B", "XTX Technology Limited", 4 * MB));
            addFlash(new SPIFlashInfo(0x0017400b, "XT25F64B", "XTX Technology Limited", 8 * MB));
            addFlash(new SPIFlashInfo(0x0017600b, "XT25Q64B", "XTX Technology Limited", 8 * MB));
            addFlash(new SPIFlashInfo(0x0015405e, "ZB25D16", "Zbit", 2 * MB));
            addFlash(new SPIFlashInfo(0x0012405e, "ZB25D20", "Zbit", 256 * KB));
            addFlash(new SPIFlashInfo(0x001220ba, "ZD25D20", "Zetta", 256 * KB));

            // Additional info-only coverage for every operational flash ID known by BKFlashList.
            addFlash(new SPIFlashInfo(0x00424b01, "BK", "BK", 512 * KB));
            addFlash(new SPIFlashInfo(0x00186068, "BY25Q128EL", "Boya Micro", 16 * MB));
            addFlash(new SPIFlashInfo(0x00184068, "BY25Q128ES", "Boya Micro", 16 * MB));
            addFlash(new SPIFlashInfo(0x001860a1, "FM25LQ128I", "Fudan Microelectronics", 16 * MB));
            addFlash(new SPIFlashInfo(0x001860c8, "GD25LQ128E", "GigaDevice", 16 * MB));
            addFlash(new SPIFlashInfo(0x001960c8, "GD25LQ256E", "GigaDevice", 32 * MB));
            addFlash(new SPIFlashInfo(0x001968c8, "GD25LX256E", "GigaDevice", 32 * MB));
            addFlash(new SPIFlashInfo(0x001840c8, "GD25Q128C", "GigaDevice", 16 * MB));
            addFlash(new SPIFlashInfo(0x001940c8, "GD25Q256C", "GigaDevice", 32 * MB));
            addFlash(new SPIFlashInfo(0x001365c8, "GD25Q41B", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x001364c8, "GD25Q41BT", "GigaDevice", 512 * KB));
            addFlash(new SPIFlashInfo(0x001a47c8, "GD25Q512C", "GigaDevice", 64 * MB));
            addFlash(new SPIFlashInfo(0x001740c8, "GD25Q64C", "GigaDevice", 8 * MB));
            addFlash(new SPIFlashInfo(0x001464c8, "GD25WD80E", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x001865c8, "GD25WQ128E", "GigaDevice", 16 * MB));
            addFlash(new SPIFlashInfo(0x001465c8, "GD25WQ80E", "GigaDevice", 1 * MB));
            addFlash(new SPIFlashInfo(0x001040c4, "GT25Q05D", "Giantec Semiconductor", 64 * KB));
            addFlash(new SPIFlashInfo(0x001140c4, "GT25Q10D", "Giantec Semiconductor", 128 * KB));
            addFlash(new SPIFlashInfo(0x001840c4, "GT25Q128EZ", "Giantec Semiconductor", 16 * MB));
            addFlash(new SPIFlashInfo(0x001560c4, "GT25Q16B", "Giantec Semiconductor", 2 * MB));
            addFlash(new SPIFlashInfo(0x001240c4, "GT25Q20D", "Giantec Semiconductor", 256 * KB));
            addFlash(new SPIFlashInfo(0x001340c4, "GT25Q40D", "Giantec Semiconductor", 512 * KB));
            addFlash(new SPIFlashInfo(0x001323c2, "MX25V4035F", "Macronix", 512 * KB));
            addFlash(new SPIFlashInfo(0x001420c2, "MX25V80066", "Macronix", 1 * MB));
            addFlash(new SPIFlashInfo(0x00182085, "P25Q128HA", "Puya", 16 * MB));
            addFlash(new SPIFlashInfo(0x00156085, "P25Q16SU", "Puya", 2 * MB));
            addFlash(new SPIFlashInfo(0x00162085, "P25Q32HB", "Puya", 4 * MB));
            addFlash(new SPIFlashInfo(0x00124485, "PY25D22U", "Puya", 256 * KB));
            addFlash(new SPIFlashInfo(0x00124585, "PY25D24U", "Puya", 256 * KB));
            addFlash(new SPIFlashInfo(0x00186585, "PY25Q128LA", "Puya", 16 * MB));
            addFlash(new SPIFlashInfo(0x00166585, "PY25Q32LB", "Puya", 4 * MB));
            addFlash(new SPIFlashInfo(0x00172085, "PY25Q64HA", "Puya", 8 * MB));
            addFlash(new SPIFlashInfo(0x001260eb, "TH25D20HA", "Tsingteng Microsystem", 256 * KB));
            addFlash(new SPIFlashInfo(0x001360cd, "TH25D40HB", "Tsingteng Microsystem", 512 * KB));
            addFlash(new SPIFlashInfo(0x001371cd, "TH25D40UC", "Tsingteng Microsystem", 512 * KB));
            addFlash(new SPIFlashInfo(0x001571cd, "TH25Q16UC", "Tsingteng Microsystem", 2 * MB));
            addFlash(new SPIFlashInfo(0x001660cd, "TH25Q32UB", "Tsingteng Microsystem", 4 * MB));
            addFlash(new SPIFlashInfo(0x001760eb, "TH25Q64HA", "Tsingteng Microsystem", 8 * MB));
            addFlash(new SPIFlashInfo(0x001760cd, "TH25Q64HB", "Tsingteng Microsystem", 8 * MB));
            addFlash(new SPIFlashInfo(0x001260b3, "UC25HQ20", "UCUN", 256 * KB));
            addFlash(new SPIFlashInfo(0x001360b3, "UC25HQ40", "UCUN", 512 * KB));
            addFlash(new SPIFlashInfo(0x001880ef, "WB25Q128JW_IM", "Winbond", 16 * MB));
            addFlash(new SPIFlashInfo(0x001860ef, "WB25Q128JW_IQ", "Winbond", 16 * MB));
            addFlash(new SPIFlashInfo(0x00184020, "XM25QH128D", "XMC", 16 * MB));
            addFlash(new SPIFlashInfo(0x00194020, "XM25QH256D", "XMC", 32 * MB));
            addFlash(new SPIFlashInfo(0x00174020, "XM25QH64D", "XMC", 8 * MB));
            addFlash(new SPIFlashInfo(0x00184120, "XM25QU128C", "XMC", 16 * MB));
            addFlash(new SPIFlashInfo(0x0014400b, "XT25F08F", "XTX Technology Limited", 1 * MB));
            addFlash(new SPIFlashInfo(0x0018400b, "XT25F128F", "XTX Technology Limited", 16 * MB));
            addFlash(new SPIFlashInfo(0x0015404b, "XT25F16B_1", "XTX Technology Limited", 2 * MB));
            addFlash(new SPIFlashInfo(0x0018600b, "XT25Q128B", "XTX Technology Limited", 16 * MB));
            addFlash(new SPIFlashInfo(0x0018505e, "ZB25LQ128C", "Zbit", 16 * MB));
        }
    }
}
