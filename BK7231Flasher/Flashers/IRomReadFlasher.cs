namespace BK7231Flasher
{
    public interface IRomReadFlasher
    {
        byte[] ReadRomTarget(RomReadTarget target);
    }
}
