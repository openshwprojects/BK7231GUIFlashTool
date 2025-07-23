
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace LN882HTool
{

    class Program2
    {
        public static void Main3(string[] args)
        {
            string port = "COM3";
            string toWrite = "";
            string toRead = "";
            bool bErase = false;
            bool bInfo = false;
            bool bTerminal = false;
            int baud = 460800;
            // YModem.test();

            // Erase: SharpLN882HTool.exe -p COM3 -ef
            // Read: SharpLN882HTool.exe -p COM3 -rf 460800 dump.bin
            // Write: SharpLN882HTool.exe -p COM3 -wf obk.bin
            
            baud = 460800;
            port = "COM3";
            toRead = "dump.bin";
            //toWrite = "OpenLN882H_1.18.135.bin";
            if (bInfo)
            {
                LN882HFlasher f = new LN882HFlasher(port, 115200);
                f.upload_ram_loader("LN882H_RAM_BIN.bin");
                f.flash_info();
                f.get_mac_in_otp();
                f.get_mac_local();
                Console.WriteLine("Info done!");
            }
            if (toRead.Length>0)
            {
                LN882HFlasher f = new LN882HFlasher(port, 115200);
                Console.WriteLine("Will do dump everything");
                if(f.read_flash_to_file(toRead, baud)) Console.WriteLine("Dump done!");
                else Console.WriteLine("Dump failed!");
            }
            if(bErase)
            {
                LN882HFlasher f = new LN882HFlasher(port, 115200);
                f.upload_ram_loader("LN882H_RAM_BIN.bin");
              //  f.flash_info();
                Console.WriteLine("Will do flash erase all...");
                f.flash_erase_all();
                Console.WriteLine("Erase done!");
            }
            if (toWrite.Length > 0)
            {
                LN882HFlasher f = new LN882HFlasher(port, 115200);
                f.upload_ram_loader("LN882H_RAM_BIN.bin");
                //f.flash_info();
                Console.WriteLine("Will do flash " + toWrite + "...");
                f.flash_program(toWrite);
                Console.WriteLine("Flash done!");
            }
            if(bTerminal)
            {
                LN882HFlasher f = new LN882HFlasher(port, 115200);
                f.upload_ram_loader("LN882H_RAM_BIN.bin");
                f.runTerminal();
            }
        }
    }
}