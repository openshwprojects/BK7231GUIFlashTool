using System.Threading;

namespace BK7231Flasher
{
    public static class FlasherFactory
    {
        public static BaseFlasher Create(BKType chipType, CancellationToken cancellationToken)
        {
            switch (chipType)
            {
                case BKType.RTL8710B:
                case BKType.RTL8720D:
                    return new RTLFlasher(cancellationToken);
                case BKType.RTL87X0C:
                    return new RTLZ2Flasher(cancellationToken);
                case BKType.LN882H:
                case BKType.LN8825:
                    return new LN882HFlasher(cancellationToken);
                case BKType.BL602:
                case BKType.BL702:
                case BKType.BL616:
                    return new BL602Flasher(cancellationToken);
                case BKType.BekenSPI:
                    return new SPIFlasher_Beken(cancellationToken);
                case BKType.GenericSPI:
                    return new SPIFlasher(cancellationToken);
                case BKType.ECR6600:
                    return new ECR6600Flasher(cancellationToken);
                case BKType.W600:
                case BKType.W800:
                    return new WMFlasher(cancellationToken);
                case BKType.RDA5981:
                    return new RDAFlasher(cancellationToken);
                case BKType.XR806:
                    return new XR806Flasher(cancellationToken);
                case BKType.XR809:
                    return new XR809Flasher(cancellationToken);
                case BKType.XR872:
                    return new XR872Flasher(cancellationToken);
                case BKType.TR6260:
                    return new TR6260Flasher(cancellationToken);
                case BKType.ESP32:
                case BKType.ESP32S2:
                case BKType.ESP32C2:
                case BKType.ESP32C5:
                case BKType.ESP32C6:
                case BKType.ESP32C61:
                case BKType.ESP32S3:
                case BKType.ESP32C3:
                case BKType.ESP8266:
                    return new ESPFlasher(cancellationToken);
                case BKType.GD32VW553:
                    return new GD32VW553Flasher(cancellationToken);
                case BKType.RTL8721DA:
                case BKType.RTL8720E:
                    return new RTLNFlasher(cancellationToken);
                default:
                    return new BK7231Flasher(cancellationToken);
            }
        }
    }
}
