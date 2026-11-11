using BK7231Flasher;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

// No serial ports are opened by this executable. The adapter tests inject a
// factory that throws if any unsupported operation gets as far as opening one.
internal static class ALIM3801SelfTests
{
    const int FlashSize = 0x400000, MainOffset = 0x50000, MainSpan = 0x240000;
    static string temporaryDirectory;
    static int passed;

    static int Main()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "ali-selftests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            Run("CRC-32/MPEG-2 published vector and subrange", CrcVector);
            Run("fragmented handshake and exact 4 MiB dump", FragmentedDump);
            Run("dump rejects CRC corruption and wrong flash size", BadDump);
            Run("truncated dump times out without any write command", TruncatedDump);
            Run("three updates v1 -> v2 -> v2 preserve loader and settings", RepeatedUpdates);
            Run("1024-byte packets retry a rejected block unchanged", PacketRetry);
            Run("exhausted packet retry never burns or reboots", PacketRetryExhausted);
            Run("backup CreateNew preserves existing file", ExclusiveBackup);
            Run("unrecognized loader and corrupt target chunk refuse burn", InvalidTarget);
            Run("invalid payload and offset refuse writes", InvalidPayload);
            Run("readback mismatch outside maincode prevents reboot", ReadbackMismatch);
            Run("burn error prevents verification and reboot", BurnError);
            Run("cancellation prevents subsequent protocol writes", Cancellation);
            Run("unsupported adapter operations reject before port open", AdapterGuards);
            Run("fixed 115200 8E1 serial profile stays unopened", SerialProfile);
            Run("CLI aliases and unsafe requests reject before port access", CliGuards);
            Console.WriteLine("PASS: " + passed + " ALi M3801 selftests; no hardware was opened.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
        finally
        {
            // Resolve and check the final target before recursively deleting
            // only this run's unique directory directly beneath Windows TEMP.
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string cleanupTarget = Path.GetFullPath(temporaryDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string cleanupName = Path.GetFileName(cleanupTarget);
            string runId = cleanupName.StartsWith("ali-selftests-", StringComparison.Ordinal)
                ? cleanupName.Substring("ali-selftests-".Length) : "";
            Guid parsedRunId;
            if (string.Equals(cleanupTarget, tempRoot, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(cleanupTarget), tempRoot, StringComparison.OrdinalIgnoreCase)
                || runId.Length != 32 || !Guid.TryParseExact(runId, "N", out parsedRunId)
                || !string.Equals(cleanupTarget, Path.Combine(tempRoot, "ali-selftests-" + runId), StringComparison.OrdinalIgnoreCase)
                || (File.GetAttributes(cleanupTarget) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Refusing cleanup outside this run's unique temporary directory.");
            Directory.Delete(cleanupTarget, true);
        }
    }

    static void Run(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static T Throws<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    static void MustReject(Action operation)
    {
        // Production uses several explicit validation exception types. Do not
        // accidentally count a broken test (null/array/assert failure) as success.
        try { operation(); }
        catch (ArgumentException) { return; }
        catch (InvalidDataException) { return; }
        catch (InvalidOperationException) { return; }
        catch (NotSupportedException) { return; }
        catch (IOException) { return; }
        throw new Exception("Unsafe operation was accepted");
    }

    static string Backup(string name) { return Path.Combine(temporaryDirectory, name + ".bin"); }
    static byte[] Clone(byte[] bytes) { return (byte[])bytes.Clone(); }
    static void Equal(byte[] expected, byte[] actual, string message)
    {
        Check(expected.SequenceEqual(actual), message);
    }

    static void CrcVector()
    {
        var bytes = Encoding.ASCII.GetBytes("123456789");
        Check(ReferenceCrc(bytes, 0, bytes.Length) == 0x0376e6e7, "Independent reference CRC is incorrect");
        Check(ALIM3801Protocol.Crc32Mpeg2(bytes) == 0x0376e6e7, "Published CRC check vector failed");
        Check(ALIM3801Protocol.Crc32Mpeg2(new byte[0]) == 0xffffffff, "Empty CRC has wrong initial value");
        Check(ALIM3801Protocol.Crc32Mpeg2(Encoding.ASCII.GetBytes("x123456789y"), 1, 9) == 0x0376e6e7,
            "CRC offset/count is ignored");
    }

    static void FragmentedDump()
    {
        var fake = new Bootloader(NewFlash());
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        Equal(fake.Flash, protocol.Dump(), "Dump bytes changed or were truncated");
        Check(fake.ReadCalls > 1000, "Replies were not fragmented");
        Check(fake.Commands.SequenceEqual(new[] { "comtest 2\r", "XX", "dump\r", "O" }), "Wrong dump command sequence");
        Check(fake.Burns == 0 && fake.Reboots == 0, "Read operation modified device");
    }

    static void BadDump()
    {
        var fake = new Bootloader(NewFlash()) { CorruptDumpNumber = 1 };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.Dump());
        Check(fake.Burns == 0, "CRC failure caused a burn");
        fake = new Bootloader(NewFlash()) { AdvertisedSize = 0x200000 };
        protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.Dump());
        Check(fake.Commands.Last() == "E", "Unsupported dump size was not declined before streaming");
        Check(fake.DumpStreams == 0, "Unexpected-size stream was accepted");
    }

    static void TruncatedDump()
    {
        var fake = new Bootloader(NewFlash()) { TruncateDump = true };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None) { DumpTimeoutMilliseconds = 50 };
        protocol.Handshake();
        Throws<TimeoutException>(() => protocol.Dump());
        Check(fake.Burns == 0 && fake.PacketWrites == 0 && fake.Reboots == 0, "Truncated read started a write");
    }

    static void RepeatedUpdates()
    {
        var initial = NewFlash();
        var fake = new Bootloader(initial);
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        var images = new[] { Payload("v1"), Payload("v2"), Payload("v2") };
        var before = Clone(initial);
        for (int cycle = 0; cycle < images.Length; cycle++)
        {
            protocol.Handshake();
            string backup = Backup("repeat-" + cycle);
            fake.BeforeAddress = () => Equal(before, File.ReadAllBytes(backup), "Full backup must exist before address/staging command");
            var result = protocol.UpdateMaincode(images[cycle], backup);
            Equal(before, File.ReadAllBytes(backup), "Backup did not match device before cycle " + cycle);
            Equal(before, result.Before, "Reported before bytes mismatch");
            var expected = Clone(before);
            Array.Copy(images[cycle], 0, expected, MainOffset, images[cycle].Length);
            Equal(expected, fake.Flash, "Unexpected write range in update " + cycle);
            Equal(expected, result.After, "Readback differs from expected update " + cycle);
            Equal(initial.Take(MainOffset).ToArray(), fake.Flash.Take(MainOffset).ToArray(), "Loader/HDCP changed");
            Equal(initial.Skip(MainOffset + MainSpan).ToArray(), fake.Flash.Skip(MainOffset + MainSpan).ToArray(), "Database changed");
            before = expected;
        }
        Check(fake.Burns == 3 && fake.Reboots == 3, "Not all repeated update cycles completed");
        Check(fake.DumpStreams == 6, "Each update needs a full backup and full readback");
        Check(fake.Commands.Count(c => c == "address 327680\r") == 3, "Address must be decimal 327680");
        Check(fake.PacketWrites == 3 * 64, "64 KiB image must use 64 packets of 1024 bytes");
    }

    static void PacketRetry()
    {
        var fake = new Bootloader(NewFlash()) { RejectPacketsRemaining = 1 };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        protocol.UpdateMaincode(Payload("v2"), Backup("retry"), false);
        Check(fake.PacketWrites == 65 && fake.Burns == 1 && fake.Reboots == 0, "Single retry or stay-in-loader failed");
        Equal(fake.PacketHistory[0], fake.PacketHistory[1], "Retry changed the rejected packet");
    }

    static void PacketRetryExhausted()
    {
        var fake = new Bootloader(NewFlash()) { RejectPacketsRemaining = 20 };
        var before = Clone(fake.Flash);
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincode(Payload("v2"), Backup("retry-exhausted")));
        Check(fake.PacketWrites == 3, "Retry count must be bounded to first attempt plus two retries");
        Check(fake.Burns == 0 && fake.Reboots == 0, "Incomplete transfer was burned/rebooted");
        Equal(before, fake.Flash, "Failed transfer changed flash");
        Equal(before, File.ReadAllBytes(Backup("retry-exhausted")), "Backup was lost on transfer failure");
    }

    static void ExclusiveBackup()
    {
        var path = Backup("existing");
        var sentinel = Encoding.ASCII.GetBytes("Keep this previous backup unchanged");
        File.WriteAllBytes(path, sentinel);
        var fake = new Bootloader(NewFlash());
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincode(Payload("v2"), path));
        Equal(sentinel, File.ReadAllBytes(path), "Existing backup overwritten");
        Check(fake.PacketWrites == 0 && fake.Burns == 0 && fake.Reboots == 0, "Duplicate backup allowed writes");
        Throws<IOException>(() => ALIM3801Protocol.WriteNewFile(path, new byte[] { 1 }));
        Equal(sentinel, File.ReadAllBytes(path), "CreateNew helper overwrote existing data");
    }

    static void InvalidTarget()
    {
        var wrongLoader = NewFlash();
        wrongLoader[0x100] ^= 1;
        // Recompute the loader chunk CRC so its fingerprint is the reason it
        // fails, independently of the generic chunk CRC check.
        FixChunkCrc(wrongLoader, 0);
        var corruptConfig = NewFlash();
        corruptConfig[0x2a0080] ^= 1;
        var wrongChain = NewFlash();
        Put32(wrongChain, 0x2a0008, 0x10000);
        foreach (var pair in new[] { Tuple.Create("loader", wrongLoader), Tuple.Create("config-crc", corruptConfig), Tuple.Create("chain", wrongChain) })
        {
            var fake = new Bootloader(pair.Item2);
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            MustReject(() => protocol.UpdateMaincode(Payload("v2"), Backup("bad-" + pair.Item1)));
            Check(fake.PacketWrites == 0 && fake.Burns == 0 && fake.Reboots == 0, "Unsafe target allowed writes: " + pair.Item1);
        }
    }

    static void InvalidPayload()
    {
        var good = Payload("v2");
        ALIM3801Protocol.ValidateMaincode(good);
        MustReject(() => ALIM3801Protocol.ValidateMaincode(good, 0));
        MustReject(() => ALIM3801Protocol.ValidateMaincode(good, 0x290000));
        var badPayloads = new List<byte[]> { new byte[0], new byte[FlashSize], good.Take(140).ToArray() };
        Action<int, uint> badWord = (offset, value) => { var b = Clone(good); Put32(b, offset, value); if (offset >= 16) FixChunkCrc(b, 0); badPayloads.Add(b); };
        badWord(0, 0x23010010); // Loader chunk disguised as a maincode payload.
        badWord(8, MainSpan + 0x10000); // Header span controls effective erase range.
        badWord(120, 0x80010000);
        badWord(124, 0x80010000);
        var badCrc = Clone(good); badCrc[140] ^= 1; badPayloads.Add(badCrc);
        var badProps = Clone(good); badProps[128] = 0x5d; FixChunkCrc(badProps, 0); badPayloads.Add(badProps);
        var badDictionary = Clone(good); badDictionary[132] = 1; FixChunkCrc(badDictionary, 0); badPayloads.Add(badDictionary);
        var unknownSize = Clone(good); for (int i = 133; i < 141; i++) unknownSize[i] = 0xff; FixChunkCrc(unknownSize, 0); badPayloads.Add(unknownSize);
        var zeroSize = Clone(good); for (int i = 133; i < 141; i++) zeroSize[i] = 0; FixChunkCrc(zeroSize, 0); badPayloads.Add(zeroSize);
        for (int i = 0; i < badPayloads.Count; i++)
        {
            var fake = new Bootloader(NewFlash());
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            MustReject(() => ALIM3801Protocol.ValidateMaincode(badPayloads[i]));
            protocol.Handshake();
            int beforeWrites = fake.WriteCalls;
            MustReject(() => protocol.UpdateMaincode(badPayloads[i], Backup("bad-payload-" + i)));
            Check(fake.WriteCalls == beforeWrites, "Invalid payload touched the device: " + i);
        }
    }

    static void ReadbackMismatch()
    {
        var fake = new Bootloader(NewFlash()) { CorruptAfterBurn = true };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincode(Payload("v2"), Backup("mismatch")));
        Check(fake.Burns == 1 && fake.DumpStreams == 2 && fake.Reboots == 0, "Full readback failure must leave loader active");
        Check(File.ReadAllBytes(Backup("mismatch")).Length == FlashSize, "Pre-write backup was not retained");
    }

    static void BurnError()
    {
        var fake = new Bootloader(NewFlash()) { ReportBurnError = true };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincode(Payload("v2"), Backup("burn-error")));
        Check(fake.Burns == 1 && fake.DumpStreams == 1 && fake.Reboots == 0, "Burn error was ignored or retried");
    }

    static void Cancellation()
    {
        using (var cancel = new CancellationTokenSource())
        {
            var fake = new Bootloader(NewFlash());
            var before = Clone(fake.Flash);
            var protocol = new ALIM3801Protocol(fake, cancel.Token);
            protocol.Handshake();
            int atCancel = -1;
            fake.AfterPacket = () => { atCancel = fake.WriteCalls; cancel.Cancel(); };
            Throws<OperationCanceledException>(() => protocol.UpdateMaincode(Payload("v2"), Backup("cancelled")));
            Check(fake.WriteCalls == atCancel && fake.Burns == 0 && fake.Reboots == 0, "Cancellation allowed another command");
            Equal(before, fake.Flash, "Cancellation before burn modified flash");
            Throws<OperationCanceledException>(() => protocol.Handshake());
            Check(fake.WriteCalls == atCancel, "Cancelled client attempted a new handshake");
        }
    }

    sealed class TestLogger : ILogListener
    {
        public void addLog(string s, Color c) { }
        public void setProgress(int cur, int max) { }
        public void setState(string s, Color c) { }
        public void onReadResultQIOSaved(byte[] bytes, string key, string path) { }
        public OBKConfig getConfigToWrite() { return null; }
        public OBKConfig getConfig() { return null; }
    }

    static void AdapterGuards()
    {
        int opens = 0;
        Func<ALIM3801Flasher> adapter = () =>
        {
            var result = new ALIM3801Flasher(CancellationToken.None, port => { opens++; throw new Exception("Unexpected port open: " + port); });
            result.setBasic(new TestLogger(), "NEVER_OPEN_A_PORT", BKType.ALIM3801, 115200);
            return result;
        };
        var payloadPath = Path.Combine(temporaryDirectory, "app.maincode.bin");
        File.WriteAllBytes(payloadPath, Payload("v2"));
        var flasher = adapter();
        MustReject(() => flasher.doWrite(0x50, Payload("v2")));
        MustReject(() => flasher.doErase(0, 1024, true));
        MustReject(() => flasher.doTestReadWrite(0x50, 16));
        MustReject(() => flasher.doRead(0x50, 16, false));
        MustReject(() => flasher.saveReadResult(0));
        MustReject(() => flasher.doReadAndWrite(0, 1024, payloadPath, WriteMode.ReadAndWrite));
        MustReject(() => flasher.doReadAndWrite(0x50, 16, payloadPath, WriteMode.ReadAndWrite));
        MustReject(() => flasher.doReadAndWrite(0x50, 1024, payloadPath, WriteMode.OnlyErase));
        MustReject(() => flasher.doReadAndWrite(0x50, 1024, payloadPath, WriteMode.OnlyOBKConfig));
        foreach (Action<ALIM3801Flasher> configure in new Action<ALIM3801Flasher>[] {
            f => f.setOverwriteBootloader(true), f => f.setIgnoreCRCErr(true),
            f => f.setCustomWriteMode(true), f => f.setSkipKeyCheck(true), f => f.setUseCompression(true) })
        {
            flasher = adapter();
            configure(flasher);
            MustReject(() => flasher.doReadAndWrite(0x50, 1024, payloadPath, WriteMode.OnlyWrite));
        }
        Check(opens == 0, "Unsupported adapter route reached serial port factory");
    }

    static void SerialProfile()
    {
        using (var port = ALIM3801SerialTransport.CreatePort("NEVER_OPEN_A_PORT"))
        {
            Check(!port.IsOpen, "Profile construction opened hardware");
            Check(port.PortName == "NEVER_OPEN_A_PORT", "Requested port changed");
            Check(port.BaudRate == 115200 && port.DataBits == 8 && port.Parity == Parity.Even && port.StopBits == StopBits.One,
                "Wrong ALi UART profile");
            Check(port.Handshake == Handshake.None && !port.DtrEnable && !port.RtsEnable, "UART reset/flow-control lines enabled");
        }
        MustReject(() => ALIM3801SerialTransport.CreatePort(" "));
    }

    static void CliGuards()
    {
        // These malformed requests must fail before the legacy automatic port
        // selection path, including when no port was supplied at all.
        CliReject("--chip ALIM380I fread", "Unknown chip type 'ALIM380I'");
        CliReject("fread", "--chip is required");
        CliReject("--chip 999 fread", "Unknown chip type '999'");
        CliReject("--chip BK7231N fwrite", "File path is required for write operations");
        CliReject("--chip ALIM3801 --port NOT_A_PORT fwrite", "File path is required for write operations");
        foreach (string chip in new[] { "ALIM3801", "ALI", "M3801", "ALI_M3801", "ALI-M3801" })
            CliReject("--chip " + chip + " fread", "requires an explicit --port");
        CliReject("--chip ALIM3801 --port NOT_A_PORT --baud 921600 fread", "fixed 115200 baud, 8E1");
        CliReject("--chip ALIM3801 --port NOT_A_PORT test --addr 0x50000 --size 0x10000", "supports fread and guarded maincode fwrite only");
        CliReject("--chip ALIM3801 --port NOT_A_PORT fread --addr 0 --size 0x400000", "supports fread and guarded maincode fwrite only");
        CliReject("--chip ALIM3801 --port NOT_A_PORT fread --no-stub", "supports fread and guarded maincode fwrite only");
        string invalidFile = Path.Combine(temporaryDirectory, "invalid.maincode.bin");
        File.WriteAllBytes(invalidFile, new byte[16]);
        CliReject("--chip ALIM3801 --port NOT_A_PORT fwrite \"" + invalidFile + "\"", "Select an ALi .maincode.bin chunk");
    }

    static void CliReject(string arguments, string expectedMessage)
    {
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo(typeof(ALIM3801Protocol).Assembly.Location, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = temporaryDirectory
            };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                throw new Exception("CLI validation did not finish promptly: " + arguments);
            }
            string output = outputTask.Result + errorTask.Result;
            Check(process.ExitCode == 1, "CLI must reject request with exit 1: " + arguments + "\n" + output);
            Check(output.Contains(expectedMessage), "CLI rejected for the wrong reason: " + arguments + "\n" + output);
            Check(!output.Contains("[AutoSelect]") && !output.Contains("UART is open"), "CLI reached hardware path: " + output);
        }
    }

    static byte[] Payload(string version)
    {
        var chunk = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "maciej-" + version + ".chunk.bin"));
        var payload = Enumerable.Repeat((byte)0xff, 0x10000).ToArray();
        Array.Copy(chunk, payload, chunk.Length);
        return payload;
    }

    static byte[] NewFlash()
    {
        var flash = Enumerable.Repeat((byte)0xff, FlashSize).ToArray();
        using (var input = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "maciej-loader.bin.gz")))
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gzip.CopyTo(output);
            Check(output.Length == 0x4fe00, "Loader fixture has wrong size");
            Array.Copy(output.ToArray(), flash, (int)output.Length);
        }
        MakeChunk(flash, 0x4fe00, 0x09f60100, 0x200, "synthetic-hdcp");
        Array.Copy(Payload("v2"), 0, flash, MainOffset, 0x10000);
        MakeChunk(flash, 0x290000, 0x02fd0100, 0x10000, "synthetic-radio");
        MakeChunk(flash, 0x2a0000, 0x03fc0100, 0x1ff80, "synthetic-db");
        MakeChunk(flash, 0x2bff80, 0x04fb0100, 0, "synthetic-user");
        ALIM3801Protocol.ValidateTargetFlash(flash);
        return flash;
    }

    static void MakeChunk(byte[] flash, int offset, uint id, uint span, string name)
    {
        Put32(flash, offset, id);
        Put32(flash, offset + 4, 128); // 112 header bytes + 16 bytes of synthetic data.
        Put32(flash, offset + 8, span);
        var nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, flash, offset + 16, nameBytes.Length);
        for (int i = 0; i < 16; i++) flash[offset + 128 + i] = (byte)(i + (offset >> 16));
        FixChunkCrc(flash, offset);
    }

    static void FixChunkCrc(byte[] bytes, int offset)
    {
        Put32(bytes, offset + 12, ReferenceCrc(bytes, offset + 16, checked((int)Get32(bytes, offset + 4))));
    }

    static uint Get32(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    static void Put32(byte[] bytes, int offset, uint value)
    {
        for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (24 - i * 8));
    }

    // Deliberately bitwise, not the production lookup-table implementation.
    static uint ReferenceCrc(byte[] bytes, int offset, int count)
    {
        uint crc = 0xffffffff;
        for (int i = offset; i < offset + count; i++)
        {
            crc ^= (uint)bytes[i] << 24;
            for (int bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04c11db7U : 0U);
        }
        return crc;
    }

    sealed class Bootloader : IALIM3801Transport
    {
        public byte[] Flash;
        public List<string> Commands = new List<string>();
        public List<byte[]> PacketHistory = new List<byte[]>();
        public int Burns, Reboots, DumpStreams, PacketWrites, WriteCalls, ReadCalls;
        public int RejectPacketsRemaining, CorruptDumpNumber;
        public uint AdvertisedSize = FlashSize;
        public bool CorruptAfterBurn, ReportBurnError, TruncateDump;
        public Action AfterPacket, BeforeAddress;
        readonly Queue<byte[]> replies = new Queue<byte[]>();
        int replyOffset, dumpCommands, received;
        string state = "handshake";
        byte[] staged;
        bool addressSet, stageComplete;
        public Bootloader(byte[] flash) { Flash = Clone(flash); }

        void Reply(byte[] bytes) { replies.Enqueue(bytes); }
        void Reply(string value) { Reply(Encoding.ASCII.GetBytes(value)); }
        void Reply32(uint value) { var b = new byte[4]; Put32(b, 0, value); Reply(b); }

        public void Write(byte[] bytes, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            WriteCalls++;
            Check(replies.Count == 0, "Host wrote before consuming previous reply");
            if (state == "packet")
            {
                int count = Math.Min(1024, staged.Length - received);
                Check(bytes.Length == count + 4, "Wrong packet data/CRC length");
                Check(Get32(bytes, count) == ReferenceCrc(bytes, 0, count), "Packet CRC is not big-endian CRC-32/MPEG-2");
                PacketWrites++;
                PacketHistory.Add(Clone(bytes));
                if (RejectPacketsRemaining > 0) { RejectPacketsRemaining--; Reply("E"); }
                else
                {
                    Array.Copy(bytes, 0, staged, received, count);
                    received += count;
                    Reply("O");
                    if (received == staged.Length) { state = "ready"; stageComplete = true; }
                }
                AfterPacket?.Invoke();
                return;
            }
            string command = Encoding.ASCII.GetString(bytes);
            Commands.Add(command);
            if (command == "comtest 2\r")
            {
                Check(state == "handshake" || state == "ready", "Handshake in wrong state");
                Reply(bytes); state = "select"; addressSet = stageComplete = false; return;
            }
            if (state == "select")
            {
                Check(command == "XX", "Classic selector must be XX");
                Reply(bytes); state = "ready"; return;
            }
            if (state == "dump-ack")
            {
                if (command == "E") { state = "ready"; return; }
                Check(command == "O" && AdvertisedSize == FlashSize, "Incorrect dump acknowledgement");
                DumpStreams++;
                if (TruncateDump) { Reply(Flash.Take(100).ToArray()); state = "ready"; return; }
                Reply(Clone(Flash));
                uint crc = ReferenceCrc(Flash, 0, Flash.Length);
                Reply32(dumpCommands == CorruptDumpNumber ? crc ^ 1U : crc);
                state = "ready"; return;
            }
            Check(state == "ready", "Command before successful handshake: " + command);
            if (command == "dump\r")
            {
                dumpCommands++; Reply(bytes); Reply32(AdvertisedSize); state = "dump-ack"; return;
            }
            if (command.StartsWith("address ", StringComparison.Ordinal))
            {
                Check(command == "address 327680\r", "Unsafe/nondecimal address: " + command);
                BeforeAddress?.Invoke();
                Reply(bytes); Reply("O"); addressSet = true; stageComplete = false; return;
            }
            if (command.StartsWith("transfer ", StringComparison.Ordinal))
            {
                Check(addressSet && !stageComplete, "Transfer without fresh address");
                string number = command.Substring(9).TrimEnd('\r');
                int length;
                Check(int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out length), "Transfer length must be decimal");
                Check(length > 0 && length <= MainSpan, "Transfer length out of bounds");
                staged = new byte[length]; received = 0; Reply(bytes); Reply("O"); state = "packet"; return;
            }
            if (command == "burn\r")
            {
                Check(addressSet && stageComplete && received == staged.Length, "Burn before complete transfer");
                Burns++; Reply(bytes); Reply("O");
                Array.Copy(staged, 0, Flash, MainOffset, staged.Length);
                if (CorruptAfterBurn) Flash[0x3f0000] ^= 1;
                Reply(ReportBurnError ? "B0064K\nE0000001\nF" : "B0064K\nF");
                addressSet = stageComplete = false; return;
            }
            if (command == "reboot\r")
            {
                Reboots++; Reply(bytes); Reply("O"); state = "handshake"; return;
            }
            throw new Exception("Unknown host command: " + command);
        }

        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Check(timeoutMilliseconds > 0, "Read timeout must be positive");
            ReadCalls++;
            if (replies.Count == 0) return 0;
            var next = replies.Peek();
            // Uneven fragmentation crosses echoes, size/CRC words and packets.
            int[] fragments = { 1, 2, 7, 31, 257, 4093 };
            int take = Math.Min(count, Math.Min(next.Length - replyOffset, fragments[ReadCalls % fragments.Length]));
            Array.Copy(next, replyOffset, buffer, offset, take);
            replyOffset += take;
            if (replyOffset == next.Length) { replies.Dequeue(); replyOffset = 0; }
            return take;
        }

        public void Dispose() { }
    }
}
