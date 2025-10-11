using System;

namespace BK7231Flasher
{
    class OBKFlashLayout
    {
        public static int getConfigLocation(BKType type, out int sectors)
        {
            switch(type)
            {
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7238:
                case BKType.BK7252:
                case BKType.BK7252N:
                    sectors = 1;
                    return 0x1e1000;
                case BKType.BK7231N:
                case BKType.BK7231M:
                    sectors = 1;
                    return 0x1d1000;
                case BKType.RTL8720D:
                    sectors = 0x40000 / BK7231Flasher.SECTOR_SIZE;
                    return 0x1B0000;
                case BKType.RTL87X0C:
                    sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
                    return 0x1F0000;
                case BKType.RTL8710B:
                    sectors = 0x8000 / BK7231Flasher.SECTOR_SIZE;
                    return 0x195000;
                case BKType.BL602: // this is for new toml
                    sectors = 0x13000 / BK7231Flasher.SECTOR_SIZE;
                    return 0x1E9000;
                case BKType.LN882H:
                    sectors = 1;
                    return 0x1FF000;
                case BKType.ECR6600:
                    sectors = 0x1C000 / BK7231Flasher.SECTOR_SIZE;
                    return 0x1B9000;
                //case BKType.XR809:
                //    sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
                //    return 0x1E0000;
                //case BKType.XR806:
                //    sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
                //    return 0x1F0000;
                //case BKType.XR872:
                //    sectors = 0x8000 / BK7231Flasher.SECTOR_SIZE;
                //    return 0xEF000;
                //case BKType.W800:
                //    //sectors = 0x21000 / BK7231Flasher.SECTOR_SIZE;
                //    //return 0x1DB000;
                //    sectors = 1;
                //    return 0x1F0303;
                //case BKType.W600:
                //    sectors = 1;
                //    return 0xF0000;
                //case BKType.TR6260:
                //    sectors = 0x12000 / BK7231Flasher.SECTOR_SIZE;
                //    return 0xEC000;
                default:
                    sectors = 0;
                    return 0;
            }
        }

        internal static BKType detectChipTypeForCFG(byte[] dat)
        {
            try
            {
                int ofs_BK7231T = getConfigLocation(BKType.BK7231T, out _);
                int ofs_BK7231N = getConfigLocation(BKType.BK7231N, out _);
                if (OBKConfig.isValid(dat, ofs_BK7231N))
                {
                    return BKType.BK7231N;
                }
                if (OBKConfig.isValid(dat, ofs_BK7231T))
                {
                    return BKType.BK7231T;
                }
            }
            catch(Exception ex)
            {
            }
            return BKType.Detect;
        }
    }
}
