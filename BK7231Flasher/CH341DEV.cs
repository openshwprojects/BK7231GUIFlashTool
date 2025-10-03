using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public class CH341DEV
{
    public int usb_id;
    public int open_status;
    public int i2c_speed;

    public CH341DEV(int dev_index = 0)
    {
        usb_id = dev_index;
        open_status = 0;
        i2c_speed = 3;
    }

    public int CheckStatus()
    {
        if (open_status == 0)
        {
            Console.WriteLine("Error: CH341 device not ready.");
            return -1;
        }
        return 1;
    }
    string lastError = "";
    void doError(string s)
    {
        lastError = s;
        Console.WriteLine(s);
    }
    public string getLastError()
    {
        return lastError;
    }
    public int Ch341Open()
    {
        try
        {
            if (CH341.CH341OpenDevice(usb_id) > 0)
            {
                Console.WriteLine($"CH341 device {usb_id} opened.");
                open_status = 1;
                return usb_id;
            }
            else
            {
                doError("Failed to open CH341 device.");
                open_status = 0;
                return -1;
            }
        }
        catch (DllNotFoundException)
        {
            doError("Error: CH341DLL.DLL not found.");
        }
        catch (EntryPointNotFoundException)
        {
            doError("Error: CH341DLL.DLL found, but function not exported.");
        }
        return -1;
    }

    public int Ch341Close()
    {
        if (CH341.CH341CloseDevice(usb_id))
        {
            Console.WriteLine($"CH341 device {usb_id} closed.");
            open_status = 0;
            return 1;
        }
        else
        {
            Console.WriteLine("Failed to close CH341 device.");
            return -1;
        }
    }

    public int Ch341SetI2CSpeed(int speed = 3)
    {
        i2c_speed = speed;
        if (CheckStatus() < 1) return -1;
        if (CH341.CH341SetStream(usb_id, 0x80 + speed))
        {
            //Console.WriteLine($"I2C speed set to {speed}");
            return 1;
        }
        else
        {
            Console.WriteLine($"Failed to set I2C speed to {speed}");
            open_status = 0;
            return -1;
        }
    }

    public byte[] Ch341SPI4Stream(byte[] din)
    {
        if (CheckStatus() < 1) return null;
        int len = din.Length;
        if (len > 4000) throw new Exception("Data length > 4000 not supported.");

        IntPtr pIn = Marshal.AllocHGlobal(len);
        Marshal.Copy(din, 0, pIn, len);

        byte[] outBuf = new byte[len];
        int ret = CH341.CH341StreamSPI4(usb_id, 0x80, len, pIn);

        if (ret > 0)
            Marshal.Copy(pIn, outBuf, 0, len);

        Marshal.FreeHGlobal(pIn);

        return ret > 0 ? outBuf : null;
    }
}

class CH341
{
    [DllImport("CH341DLL.DLL", CallingConvention = CallingConvention.StdCall)]
    public static extern int CH341OpenDevice(int iIndex);

    [DllImport("CH341DLL.DLL", CallingConvention = CallingConvention.StdCall)]
    public static extern bool CH341CloseDevice(int iIndex);

    [DllImport("CH341DLL.DLL", CallingConvention = CallingConvention.StdCall)]
    public static extern bool CH341SetStream(int iIndex, int iMode);

    [DllImport("CH341DLL.DLL", CallingConvention = CallingConvention.StdCall)]
    public static extern int CH341StreamSPI4(int iIndex, int iChipSelect, int iLength, IntPtr oBuffer);

    [DllImport("CH341DLL.DLL", CallingConvention = CallingConvention.StdCall)]
    public static extern bool CH341SetOutput(int iIndex, int iEnable, int iSetDirOut, int iSetDataOut);
}
