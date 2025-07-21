using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BK7231Flasher
{
    class OBKFlashLayout
    {
        public static int getConfigLocation(BKType type, out int sectors)
        {
            if (type == BKType.BK7231T || type == BKType.BK7238 || type == BKType.BK7252)
            {
                sectors = 1;
                return 0x1e1000;
            }
            if (type == BKType.BK7231N || type == BKType.BK7231M)
            {
                sectors = 1;
                return 0x1d1000;
            }
            if(type == BKType.RTL8720DN)
            {
                sectors = 0x40000 / BK7231Flasher.SECTOR_SIZE;
                return 0x1B0000;
            }
            //if(type == BKType.RTL8710B)
            //{
            //	sectors = 0x8000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x195000;
            //}
            //if(type == BKType.RTL87X0C)
            //{
            //	sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x1F0000;
            //}
            //if(type == BKType.XR809)
            //{
            //	sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x1E0000;
            //}
            //if(type == BKType.XR806)
            //{
            //	sectors = 0x10000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x1F0000;
            //}
            //if(type == BKType.XR872)
            //{
            //	sectors = 0x8000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0xEF000;
            //}
            //if(type == BKType.W800)
            //{
            //	sectors = 0x21000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x1DB000;
            //}
            //if(type == BKType.TR6260)
            //{
            //	sectors = 0x12000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0xEC000;
            //}
            //if(type == BKType.ECR6600)
            //{
            //	sectors = 0x1C000 / BK7231Flasher.SECTOR_SIZE;
            //	return 0x1B9000;
            //}
            sectors = 0;
            return 0;
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
