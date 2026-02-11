using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace BK7231Flasher
{
    /// <summary>
    /// Handles command-line flash operations without the GUI.
    /// </summary>
    public static class CommandLineRunner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        const int ATTACH_PARENT_PROCESS = -1;

        enum CliOperation
        {
            None,
            Read,
            Write,
            CustomRead,
            CustomWrite,
            Test,
            Help
        }

        public static bool ShouldRunCli(string[] args)
        {
            return args.Length > 0;
        }

        static string GetFirstAvailablePort()
        {
            var ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            foreach (var port in ports)
            {
                try
                {
                    using (var sp = new SerialPort(port))
                    {
                        sp.Open();
                        Thread.Sleep(200); // Wait inside critical section to ensure port can be closed and reopened cleanly
                        sp.Close();
                        return port;
                    }
                }
                catch
                {
                    // Ignore ports that can't be opened
                }
            }
            return null;
        }

        public static void Run(string[] args)
        {
            // Attach to parent console or allocate a new one for output
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            // Re-open stdout/stderr after attaching console
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            if (!Directory.Exists("backups"))
            {
                Directory.CreateDirectory("backups");
            }

            var operation = CliOperation.None;
            string port = null;
            int baud = 921600;
            string chipName = null;
            int ofs = -1;
            int len = -1;
            string outputName = "cliBackup";
            string writeFile = null;

            // Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                switch (arg)
                {
                    case "-read":
                        operation = CliOperation.Read;
                        break;
                    case "-write":
                        operation = CliOperation.Write;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            writeFile = args[++i];
                        break;
                    case "-cread":
                        operation = CliOperation.CustomRead;
                        break;
                    case "-test":
                        operation = CliOperation.Test;
                        break;
                    case "-cwrite":
                        operation = CliOperation.CustomWrite;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            writeFile = args[++i];
                        break;
                    case "-port":
                        if (i + 1 < args.Length) port = args[++i];
                        break;
                    case "-baud":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out baud);
                        break;
                    case "-chip":
                        if (i + 1 < args.Length) chipName = args[++i];
                        break;
                    case "-ofs":
                        if (i + 1 < args.Length) ofs = ParseInt(args[++i]);
                        break;
                    case "-len":
                        if (i + 1 < args.Length) len = ParseInt(args[++i]);
                        break;
                    case "-out":
                        if (i + 1 < args.Length) outputName = args[++i];
                        break;
                    case "-help":
                    case "--help":
                    case "-h":
                    case "/?":
                        operation = CliOperation.Help;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        break;
                }
            }

            if (operation == CliOperation.Help || operation == CliOperation.None)
            {
                PrintHelp();
                Environment.Exit(operation == CliOperation.Help ? 0 : 1);
                return;
            }

            // Validate required arguments
            if (string.IsNullOrEmpty(port))
            {
                // Port is required for serial chips, but not for SPI chips
                BKType testType = BKType.Invalid;
                if (!string.IsNullOrEmpty(chipName))
                    Enum.TryParse(chipName, true, out testType);
                if (testType != BKType.BekenSPI && testType != BKType.GenericSPI)
                {
                    port = GetFirstAvailablePort();
                    if (port != null)
                    {
                        Console.WriteLine($"No port specified, auto-selected: {port}");
                        Thread.Sleep(500); // Give time for port to close properly
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: -port is required. Use -help for usage.");
                        Environment.Exit(1);
                        return;
                    }
                }
            }

            if (string.IsNullOrEmpty(chipName))
            {
                Console.Error.WriteLine("Error: -chip is required. Use -help for usage.");
                Environment.Exit(1);
                return;
            }

            if ((operation == CliOperation.Write || operation == CliOperation.CustomWrite) && string.IsNullOrEmpty(writeFile))
            {
                Console.Error.WriteLine("Error: File path is required for write operations.");
                Environment.Exit(1);
                return;
            }

            if ((operation == CliOperation.CustomRead || operation == CliOperation.CustomWrite || operation == CliOperation.Test) && (ofs < 0 || len <= 0))
            {
                Console.Error.WriteLine("Error: -ofs and -len are required for custom read/write/test operations. Use -help for usage.");
                Environment.Exit(1);
                return;
            }

            // Resolve chip type
            BKType chipType;
            if (!Enum.TryParse(chipName, true, out chipType) || chipType == BKType.Invalid || chipType == BKType.Detect)
            {
                Console.Error.WriteLine($"Error: Unknown chip type '{chipName}'. Available types:");
                foreach (var t in Enum.GetValues(typeof(BKType)))
                {
                    if ((BKType)t != BKType.Invalid && (BKType)t != BKType.Detect)
                        Console.Error.Write($"  {t}");
                }
                Console.Error.WriteLine();
                Environment.Exit(1);
                return;
            }

            int exitCode = ExecuteOperation(operation, port ?? "", baud, chipType, ofs, len, outputName, writeFile);
            Environment.Exit(exitCode);
        }

        static int ExecuteOperation(CliOperation operation, string port, int baud, BKType chipType,
            int ofs, int len, string outputName, string writeFile)
        {
            var logger = new ConsoleLogListener();
            var cts = new CancellationTokenSource();

            // Handle Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nCancelling operation...");
                cts.Cancel();
            };

            BaseFlasher flasher = CreateFlasher(chipType, cts.Token);
            flasher.setBasic(logger, port, chipType, baud);

            try
            {
                switch (operation)
                {
                    case CliOperation.Read:
                        return DoRead(flasher, chipType, outputName);

                    case CliOperation.Write:
                        return DoWrite(flasher, chipType, writeFile);

                    case CliOperation.CustomRead:
                        return DoCustomRead(flasher, chipType, ofs, len, outputName);

                    case CliOperation.CustomWrite:
                        return DoCustomWrite(flasher, chipType, ofs, len, writeFile);

                    case CliOperation.Test:
                        return DoTest(flasher, chipType, ofs, len);

                    default:
                        Console.Error.WriteLine("Error: Unknown operation.");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nError: {ex.Message}");
                return 1;
            }
            finally
            {
                flasher.closePort();
                flasher.Dispose();
            }
        }

        static int DoRead(BaseFlasher flasher, BKType chipType, string outputName)
        {
            Console.WriteLine("Starting full flash read...");

            flasher.setBackupName(outputName);

            int startSector;
            int sectors;

            if (chipType == BKType.BK7252)
            {
                startSector = 0x11000;
                sectors = (BK7231Flasher.FLASH_SIZE / BK7231Flasher.SECTOR_SIZE) - (startSector / BK7231Flasher.SECTOR_SIZE);
                Console.WriteLine("BK7252 mode - read offset is 0x11000, bootloader not accessible.");
            }
            else
            {
                startSector = 0x0;
                sectors = BK7231Flasher.FLASH_SIZE / BK7231Flasher.SECTOR_SIZE;
            }

            flasher.doRead(startSector, sectors, true);

            if (flasher.saveReadResult(startSector))
            {
                Console.WriteLine("\nRead completed successfully.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("\nRead completed but saving failed.");
                return 1;
            }
        }

        static int DoWrite(BaseFlasher flasher, BKType chipType, string writeFile)
        {
            if (!File.Exists(writeFile))
            {
                Console.Error.WriteLine($"Error: File not found: {writeFile}");
                return 1;
            }

            Console.WriteLine($"Starting write from {writeFile}...");

            int startSector;
            int sectors;

            switch (chipType)
            {
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7252:
                    startSector = BK7231Flasher.BOOTLOADER_SIZE;
                    break;
                default:
                    startSector = 0;
                    break;
            }
            sectors = BK7231Flasher.FLASH_SIZE / BK7231Flasher.SECTOR_SIZE;

            flasher.doReadAndWrite(startSector, sectors, writeFile, WriteMode.OnlyWrite);

            Console.WriteLine("\nWrite completed successfully.");
            return 0;
        }

        /// <summary>
        /// Returns true for chips whose flasher expects a sector INDEX (multiplied internally by SECTOR_SIZE)
        /// rather than a raw byte offset.
        /// </summary>
        static bool UsesSectorIndex(BKType chipType)
        {
            switch (chipType)
            {
                case BKType.RTL8720D:
                case BKType.RTL87X0C:
                case BKType.RTL8710B:
                case BKType.RTL8721DA:
                case BKType.RTL8720E:
                case BKType.ESP32:
                case BKType.LN882H:
                case BKType.LN8825:
                case BKType.BL602:
                case BKType.BL702:
                    return true;
                default:
                    return false;
            }
        }

        static int ToStartSector(BKType chipType, int ofs)
        {
            return UsesSectorIndex(chipType) ? ofs / BK7231Flasher.SECTOR_SIZE : ofs;
        }

        static int DoCustomRead(BaseFlasher flasher, BKType chipType, int ofs, int len, string outputName)
        {
            Console.WriteLine($"Starting custom read: offset=0x{ofs:X}, length=0x{len:X}...");

            flasher.setBackupName(outputName);

            int startSector = ToStartSector(chipType, ofs);
            int sectors = len / BK7231Flasher.SECTOR_SIZE;

            flasher.doRead(startSector, sectors, false);

            if (flasher.saveReadResult(startSector))
            {
                Console.WriteLine("\nCustom read completed successfully.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("\nCustom read completed but saving failed.");
                return 1;
            }
        }

        static int DoCustomWrite(BaseFlasher flasher, BKType chipType, int ofs, int len, string writeFile)
        {
            if (!File.Exists(writeFile))
            {
                Console.Error.WriteLine($"Error: File not found: {writeFile}");
                return 1;
            }

            Console.WriteLine($"Starting custom write: offset=0x{ofs:X}, length=0x{len:X}, file={writeFile}...");

            int startSector = ToStartSector(chipType, ofs);
            int sectors = len / BK7231Flasher.SECTOR_SIZE;

            flasher.doReadAndWrite(startSector, sectors, writeFile, WriteMode.OnlyWrite);

            Console.WriteLine("\nCustom write completed successfully.");
            return 0;
        }

        static int DoTest(BaseFlasher flasher, BKType chipType, int ofs, int len)
        {
            Console.WriteLine($"Starting Read/Write/Verify test at offset 0x{ofs:X}, length 0x{len:X}...");

            byte[] testPattern = new byte[len];
            for (int i = 0; i < len; i++)
            {
                testPattern[i] = (byte)(i % 256);
            }

            string tempFile = Path.Combine(Path.GetTempPath(), "bt_test_pattern.bin");
            File.WriteAllBytes(tempFile, testPattern);

            try
            {
                int startSector = ToStartSector(chipType, ofs);
                int sectors = len / BK7231Flasher.SECTOR_SIZE;

                Console.WriteLine("Step 1/3: Writing pattern...");
                flasher.doReadAndWrite(startSector, sectors, tempFile, WriteMode.OnlyWrite);

                Console.WriteLine("\nStep 2/3: Reading back...");
                flasher.doRead(startSector, sectors, false);

                Console.WriteLine("\nStep 3/3: Verifying...");
                byte[] readData = flasher.getReadResult();

                if (readData == null || readData.Length == 0)
                {
                    Console.Error.WriteLine("Error: No read data available for verification.");
                    return 1;
                }

                int compareLen = Math.Min(testPattern.Length, readData.Length);
                bool match = true;
                int mismatchCount = 0;
                for (int i = 0; i < compareLen; i++)
                {
                    if (testPattern[i] != readData[i])
                    {
                        if (mismatchCount < 5)
                            Console.Error.WriteLine($"  Mismatch at 0x{i:X}: expected 0x{testPattern[i]:X2}, got 0x{readData[i]:X2}");
                        match = false;
                        mismatchCount++;
                    }
                }

                if (!match)
                {
                    Console.Error.WriteLine($"FAIL: Verification failed with {mismatchCount} mismatched byte(s).");
                    return 1;
                }

                if (testPattern.Length != readData.Length)
                {
                    Console.WriteLine($"WARNING: Data matches but lengths differ (pattern={testPattern.Length}, read={readData.Length}).");
                }

                Console.WriteLine("SUCCESS: Verification passed! Read data matches written pattern.");
                return 0;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        static BaseFlasher CreateFlasher(BKType chipType, CancellationToken ct)
        {
            switch (chipType)
            {
                case BKType.RTL8710B:
                case BKType.RTL8720D:
                case BKType.RTL8721DA:
                case BKType.RTL8720E:
                    return new RTLFlasher(ct);
                case BKType.RTL87X0C:
                    return new RTLZ2Flasher(ct);
                case BKType.LN882H:
                case BKType.LN8825:
                    return new LN882HFlasher(ct);
                case BKType.BL602:
                case BKType.BL702:
                    return new BL602Flasher(ct);
                case BKType.BekenSPI:
                    return new SPIFlasher_Beken(ct);
                case BKType.GenericSPI:
                    return new SPIFlasher(ct);
                case BKType.ECR6600:
                    return new ECR6600Flasher(ct);
                case BKType.W600:
                case BKType.W800:
                    return new WMFlasher(ct);
                case BKType.RDA5981:
                    return new RDAFlasher(ct);
                case BKType.ESP32:
                    return new ESPFlasher(ct);
                default:
                    return new BK7231Flasher(ct);
            }
        }

        static int ParseInt(string input)
        {
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
                return int.Parse(input, NumberStyles.HexNumber);
            }
            return int.Parse(input);
        }

        static void PrintHelp()
        {
            Console.WriteLine("BK7231 GUI Flash Tool - Command Line Mode");
            Console.WriteLine();
            Console.WriteLine("Usage: BK7231Flasher.exe <operation> [options]");
            Console.WriteLine();
            Console.WriteLine("Operations:");
            Console.WriteLine("  -read                Full flash read (backup)");
            Console.WriteLine("  -write <file.bin>    Write firmware file to flash");
            Console.WriteLine("  -cread               Custom read (requires -ofs and -len)");
            Console.WriteLine("  -cwrite <file.bin>   Custom write (requires -ofs and -len)");
            Console.WriteLine("  -test                Run write/read/verify test (requires -ofs and -len)");
            Console.WriteLine();
            Console.WriteLine("Required Options:");
            Console.WriteLine("  -port <COM3>         Serial port (not needed for SPI chips)");
            Console.WriteLine("  -chip <BK7231N>      Chip type");
            Console.WriteLine();
            Console.WriteLine("Optional:");
            Console.WriteLine("  -baud <921600>       Baud rate (default: 921600)");
            Console.WriteLine("  -ofs <0x11000>       Start offset (hex or decimal, for -cread/-cwrite)");
            Console.WriteLine("  -len <0x1000>        Length in bytes (hex or decimal, for -cread/-cwrite)");
            Console.WriteLine("  -out <name>          Output name for backup (default: cliBackup)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  BK7231Flasher.exe -read -port COM3 -chip BK7231N -out mybackup");
            Console.WriteLine("  BK7231Flasher.exe -write firmware.bin -port COM3 -chip BK7231N");
            Console.WriteLine("  BK7231Flasher.exe -cread -port COM3 -chip BK7231N -ofs 0x11000 -len 0x1000");
            Console.WriteLine("  BK7231Flasher.exe -cwrite data.bin -port COM3 -chip BK7231N -ofs 0x0 -len 0x1000");
            Console.WriteLine("  BK7231Flasher.exe -test -port COM3 -chip BK7231N -ofs 0x11000 -len 0x1000");
            Console.WriteLine();
            Console.WriteLine("Available chip types:");
            foreach (var t in Enum.GetValues(typeof(BKType)))
            {
                if ((BKType)t != BKType.Invalid && (BKType)t != BKType.Detect)
                    Console.Write($"  {t}");
            }
            Console.WriteLine();
        }
    }
}
