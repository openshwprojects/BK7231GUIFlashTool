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
using System.Windows.Forms;

// No serial ports are opened by this executable. The adapter tests inject a
// factory that throws if any unsupported operation gets as far as opening one.
internal static class ALIM3801SelfTests
{
    const int FlashSize = 0x400000, MainOffset = 0x50000, MainSpan = 0x240000;
    static string temporaryDirectory;
    static int passed;

    [STAThread]
    static int Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "ali-selftests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            Run("CRC-32/MPEG-2 published vector and subrange", CrcVector);
            Run("fragmented handshake and exact 4 MiB dump", FragmentedDump);
            Run("GUI stall does not block bounded continuous flash reception", GuiStallDuringDump);
            Run("dump rejects CRC corruption and wrong flash size", BadDump);
            Run("truncated dump times out without any write command", TruncatedDump);
            Run("three updates v1 -> v2 -> v2 preserve loader and settings", RepeatedUpdates);
            Run("no-backup repeated updates skip initial dump and keep post-write verification", NoBackupRepeatedUpdates);
            Run("three fast updates skip both backup and post-write dump", NoVerificationRepeatedUpdates);
            Run("fast writes retain payload and version/profile validation", NoVerificationPreflight);
            Run("fast writes stop on transfer/burn errors and require final F", NoVerificationWriteFailures);
            Run("backup without verification saves one durable dump and honest result", BackupWithoutVerification);
            Run("no-backup version CRC and incompatible profiles reject before staging", NoBackupInvalidProfile);
            Run("no-backup corrupt payload or loader readback never reboots", NoBackupReadbackFailure);
            Run("no-backup transfer and burn failures stop without reboot", NoBackupWriteFailure);
            Run("ALi CLI keeps backup default and explicitly selects no-backup mode", CliWriteModes);
            Run("ALi GUI offers distinct backup and no-backup buttons", GuiWriteModes);
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

    static void GuiStallDuringDump()
    {
        Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, "GUI regression must run on an STA thread");
        byte[] expected = NewFlash(), actual = null;
        Exception workerError = null;
        int reports = 0;
        bool completedWithoutPumping = false;
        using (var cancellation = new CancellationTokenSource())
        using (var transport = new BoundedDumpTransport(expected))
        using (var form = new FormMain())
        {
            // InitializeComponent enables a port-enumeration timer even without
            // Load. Stop every designer timer before creating handles/pumping;
            // this fixture tests logging only, without device enumeration or
            // application settings initialized by the normal Load handler.
            foreach (var field in typeof(FormMain).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                if (typeof(System.Windows.Forms.Timer).IsAssignableFrom(field.FieldType))
                    ((System.Windows.Forms.Timer)field.GetValue(form))?.Stop();
            // Never Show this form: its Load handler enumerates serial ports and
            // loads application settings. Only create the controls used by the
            // real ILogListener implementation, with no application operation.
            IntPtr formHandle = form.Handle;
            Control state = form.Controls.Find("labelState", true).Single();
            Control progress = form.Controls.Find("progressBar1", true).Single();
            IntPtr stateHandle = state.Handle, progressHandle = progress.Handle;
            var protocol = new ALIM3801Protocol(transport, cancellation.Token)
            {
                DumpTimeoutMilliseconds = 10000,
                Progress = (phase, current, maximum) =>
                {
                    Interlocked.Increment(ref reports);
                    form.setState(phase, Color.Transparent);
                    form.setProgress(current, maximum);
                }
            };
            var worker = new Thread(() =>
            {
                try { protocol.Handshake(); actual = protocol.Dump(); }
                catch (Exception error) { workerError = error; }
            }) { IsBackground = true, Name = "ALi bounded dump GUI regression" };
            worker.Start();
            try
            {
                // Deliberately do not pump UI messages. A synchronous Invoke
                // blocks the receiver while the independent producer keeps
                // sending, overflowing its finite receive queue.
                var deadline = Stopwatch.StartNew();
                while (worker.IsAlive && deadline.ElapsedMilliseconds < 12000)
                    Thread.Sleep(10);
                completedWithoutPumping = !worker.IsAlive;
            }
            finally
            {
                // Also release a failing, old synchronous-Invoke implementation
                // so the negative-control run exits instead of hanging.
                cancellation.Cancel();
                var cleanup = Stopwatch.StartNew();
                while (worker.IsAlive && cleanup.ElapsedMilliseconds < 3000)
                {
                    Application.DoEvents();
                    Thread.Sleep(1);
                }
                Application.DoEvents();
            }
            Check(!worker.IsAlive, "GUI regression worker did not stop during cleanup");
            Check(completedWithoutPumping,
                "Serial receipt waited for the UI thread; bounded producer dropped " + transport.DroppedBytes + " bytes");
            if (workerError != null) throw new Exception("Continuous dump failed while UI messages were paused", workerError);
            Check(reports > 1, "GUI progress callback was not exercised repeatedly");
            Check(transport.DroppedBytes == 0, "Continuous producer overflowed while UI was paused");
            Equal(expected, actual, "Bounded continuous dump was not received intact and CRC checked");
            Check(state.Text == "Reading flash", "Queued state update did not render after UI resumed");
            Check(((ProgressBar)progress).Value == FlashSize, "Queued final progress was lost");
        }
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

    static void NoBackupRepeatedUpdates()
    {
        var fake = new Bootloader(NewFlash());
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.BackupSaved = path => { throw new Exception("No-backup mode created a backup"); };
        var images = new[] { Payload("v1"), Payload("v2"), Payload("v2") };
        int fileCount = Directory.GetFiles(temporaryDirectory).Length;
        for (int cycle = 0; cycle < images.Length; cycle++)
        {
            protocol.Handshake();
            var expected = Clone(fake.Flash);
            Array.Copy(images[cycle], 0, expected, MainOffset, images[cycle].Length);
            fake.BeforeAddress = () => Check(fake.DumpStreams == cycle, "No-backup mode read flash before staging");
            var result = protocol.UpdateMaincodeWithoutBackup(images[cycle]);
            Check(result.Before == null && result.BackupPath == null, "No-backup result claimed a pre-write image");
            Equal(expected, result.After, "No-backup readback differed from programmed result");
            Check(protocol.FlashVerified && protocol.MayHaveWrittenFlash, "No-backup success flags missing");
        }
        Check(fake.VersionCommands == 3 && fake.Burns == 3 && fake.DumpStreams == 3 && fake.Reboots == 3,
            "Each no-backup update requires one short preflight, one burn and one full post-read");
        Check(Directory.GetFiles(temporaryDirectory).Length == fileCount, "No-backup mode wrote a file");
        string[] writes = fake.Commands.ToArray();
        Check(Array.IndexOf(writes, "version\r") < Array.IndexOf(writes, "address 327680\r")
            && Array.IndexOf(writes, "burn\r") < Array.IndexOf(writes, "dump\r"), "No-backup command order incorrect");
    }

    static void NoVerificationRepeatedUpdates()
    {
        var initial = NewFlash();
        var fake = new Bootloader(initial);
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.BackupSaved = path => { throw new Exception("Fast mode created a backup"); };
        int fileCount = Directory.GetFiles(temporaryDirectory).Length;
        var images = new[] { Payload("v1"), Payload("v2"), Payload("v1") };
        for (int cycle = 0; cycle < images.Length; cycle++)
        {
            int commandsBefore = fake.Commands.Count;
            protocol.Handshake();
            var expected = Clone(fake.Flash);
            Array.Copy(images[cycle], 0, expected, MainOffset, images[cycle].Length);
            fake.BeforeAddress = () => Check(fake.DumpStreams == 0, "Fast mode read flash before staging");
            var result = protocol.UpdateMaincodeWithoutBackup(images[cycle], verifyAfterWrite: false);
            Check(result.Before == null && result.After == null && result.BackupPath == null,
                "Unverified fast write claimed a backup or readback");
            Check(protocol.MayHaveWrittenFlash && !protocol.FlashVerified, "Skipped readback was reported verified");
            Equal(expected, fake.Flash, "Fast update modified unexpected flash bytes");
            string[] expectedCommands = { "comtest 2\r", "XX", "version\r", "address 327680\r",
                "transfer 65536\r", "burn\r", "reboot\r" };
            Check(fake.Commands.Skip(commandsBefore).SequenceEqual(expectedCommands),
                "Fast update skipped preflight/burn completion or inserted a dump");
        }
        Check(fake.VersionCommands == 3 && fake.Burns == 3 && fake.Reboots == 3 && fake.DumpStreams == 0,
            "Three fast updates must perform three preflights/burns/reboots and no dumps");
        // Bootloader.Write independently verifies every transmitted packet CRC.
        Check(fake.PacketWrites == 3 * 64, "Fast updates skipped or changed the 1024-byte packet transfers");
        Check(Directory.GetFiles(temporaryDirectory).Length == fileCount, "Fast mode wrote an unexpected backup file");
    }

    static void NoVerificationPreflight()
    {
        foreach (int corruptOffset in new[] { 0, 128 })
        {
            var fake = new Bootloader(NewFlash());
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            var payload = Payload("v1");
            payload[corruptOffset] ^= 1; // Invalid chunk header, then invalid app data CRC.
            MustReject(() => protocol.UpdateMaincodeWithoutBackup(payload, verifyAfterWrite: false));
            Check(fake.VersionCommands == 0 && fake.PacketWrites == 0 && fake.Burns == 0 && fake.Reboots == 0,
                "Disabling readback bypassed payload/header validation");
        }
        foreach (Action<Bootloader> mutation in new Action<Bootloader>[] {
            f => f.CorruptVersionCrc = true,
            f => f.MutateVersion = v => v[2 + 16] = (byte)'x' })
        {
            var fake = new Bootloader(NewFlash());
            mutation(fake);
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1"), verifyAfterWrite: false));
            Check(fake.VersionCommands == 1 && fake.PacketWrites == 0 && fake.DumpStreams == 0 && fake.Burns == 0 && fake.Reboots == 0,
                "Disabling readback bypassed version CRC/profile validation");
            Check(!protocol.MayHaveWrittenFlash && !protocol.FlashVerified, "Rejected preflight changed write flags");
        }
    }

    static void NoVerificationWriteFailures()
    {
        var fake = new Bootloader(NewFlash()) { RejectPacketsRemaining = 20 };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1"), verifyAfterWrite: false));
        Check(fake.PacketWrites == 3 && fake.Burns == 0 && fake.DumpStreams == 0 && fake.Reboots == 0,
            "Fast mode ignored rejected transfer packets");
        fake = new Bootloader(NewFlash()) { ReportBurnError = true };
        protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1"), verifyAfterWrite: false));
        Check(fake.Burns == 1 && fake.DumpStreams == 0 && fake.Reboots == 0 && !protocol.FlashVerified,
            "Fast mode rebooted after a burn error");
        fake = new Bootloader(NewFlash()) { SuppressBurnFinish = true };
        protocol = new ALIM3801Protocol(fake, CancellationToken.None) { BurnTimeoutMilliseconds = 50 };
        protocol.Handshake();
        Throws<TimeoutException>(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1"), verifyAfterWrite: false));
        Check(fake.Burns == 1 && fake.DumpStreams == 0 && fake.Reboots == 0 && !protocol.FlashVerified,
            "Fast mode must wait for the burn F marker before reboot");
    }

    static void BackupWithoutVerification()
    {
        foreach (bool reboot in new[] { true, false })
        {
            var initial = NewFlash();
            var fake = new Bootloader(initial);
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            string backup = Backup("backup-no-verify-" + reboot);
            int saved = 0;
            protocol.BackupSaved = path => { Check(path == backup, "Backup callback path changed"); saved++; };
            fake.BeforeAddress = () => Equal(initial, File.ReadAllBytes(backup), "Backup must be durable before fast staging");
            var result = protocol.UpdateMaincode(Payload("v2"), backup, reboot: reboot, verifyAfterWrite: false);
            Equal(initial, File.ReadAllBytes(backup), "Backup-before-fast-write contents changed");
            Equal(initial, result.Before, "Backup result did not retain the actual pre-write data");
            Check(result.After == null && result.BackupPath == backup, "Skipped verification claimed an after-image");
            Check(saved == 1 && fake.DumpStreams == 1 && fake.Burns == 1 && fake.Reboots == (reboot ? 1 : 0),
                "Backup without verification needs exactly one dump, one burn, and the requested reboot behavior");
            Check(protocol.MayHaveWrittenFlash && !protocol.FlashVerified, "Backup-only update incorrectly claimed readback verification");
            Check(fake.Commands.IndexOf("dump\r") < fake.Commands.IndexOf("address 327680\r"), "Only dump was not the initial backup");
        }
    }


    static void NoBackupInvalidProfile()
    {
        Action<Bootloader>[] mutations = {
            f => f.CorruptVersionCrc = true,
            f => f.VersionLength = 642,
            f => f.MutateVersion = v => v[0] = 0x12,
            f => f.MutateVersion = v => Put32(v, 2 + 2 * 128, 0x01fe0102),
            f => f.MutateVersion = v => Put32(v, 2 + 128 + 8, 0x100),
            f => f.MutateVersion = v => Put32(v, 2 + 2 * 128 + 4, MainSpan),
            f => f.MutateVersion = v => v[2 + 16] = (byte)'x',
            f => f.MutateVersion = v => v[2 + 32] = (byte)'2',
            f => f.MutateVersion = v => v[2 + 48] = (byte)'3'
        };
        foreach (var mutation in mutations)
        {
            var fake = new Bootloader(NewFlash());
            mutation(fake);
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1")));
            Check(fake.VersionCommands == 1 && fake.PacketWrites == 0 && fake.DumpStreams == 0 && fake.Burns == 0 && fake.Reboots == 0,
                "Invalid version preflight reached staging or flash");
            Check(!protocol.MayHaveWrittenFlash && !protocol.FlashVerified, "Invalid preflight set write flags");
        }
        var valid = new Bootloader(NewFlash());
        var invalidPayloadProtocol = new ALIM3801Protocol(valid, CancellationToken.None);
        invalidPayloadProtocol.Handshake();
        MustReject(() => invalidPayloadProtocol.UpdateMaincodeWithoutBackup(new byte[16]));
        Check(valid.VersionCommands == 0 && valid.PacketWrites == 0 && valid.Burns == 0, "Invalid payload reached the device");
    }

    static void NoBackupReadbackFailure()
    {
        Action<Bootloader>[] mutations = {
            f => f.AfterBurn = flash => flash[MainOffset + 0xffff] ^= 1,
            f => f.AfterBurn = flash => flash[0x100] ^= 1,
            f => f.CorruptDumpNumber = 1
        };
        foreach (var mutation in mutations)
        {
            var fake = new Bootloader(NewFlash());
            mutation(fake);
            var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
            protocol.Handshake();
            MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1")));
            Check(fake.Burns == 1 && fake.DumpStreams == 1 && fake.Reboots == 0, "Bad no-backup readback caused a reboot");
            Check(protocol.MayHaveWrittenFlash && !protocol.FlashVerified, "Incomplete verification was reported as successful");
        }
    }

    static void NoBackupWriteFailure()
    {
        var fake = new Bootloader(NewFlash()) { RejectPacketsRemaining = 20 };
        var protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1")));
        Check(fake.PacketWrites == 3 && fake.Burns == 0 && fake.DumpStreams == 0 && fake.Reboots == 0,
            "Incomplete no-backup transfer was burned");
        fake = new Bootloader(NewFlash()) { ReportBurnError = true };
        protocol = new ALIM3801Protocol(fake, CancellationToken.None);
        protocol.Handshake();
        MustReject(() => protocol.UpdateMaincodeWithoutBackup(Payload("v1")));
        Check(fake.Burns == 1 && fake.DumpStreams == 0 && fake.Reboots == 0, "No-backup burn error was retried or ignored");
    }

    sealed class WriteModeRecorder : BaseFlasher
    {
        public WriteMode LastMode;
        public int Start, Sectors;
        public WriteModeRecorder() : base(CancellationToken.None) { }
        public override void doReadAndWrite(int startSector, int sectors, string file, WriteMode mode)
        { Start = startSector; Sectors = sectors; LastMode = mode; }
    }

    static void CliWriteModes()
    {
        string path = Path.Combine(temporaryDirectory, "route.maincode.bin");
        File.WriteAllBytes(path, Payload("v1"));
        var method = typeof(CommandLineRunner).GetMethod("DoWrite", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        foreach (bool noBackup in new[] { false, true })
        {
            var recorder = new WriteModeRecorder();
            Check((int)method.Invoke(null, new object[] { recorder, BKType.ALIM3801, path, noBackup, false }) == 0, "CLI mode routing failed");
            Check(recorder.Start == 0x50 && recorder.Sectors == 1024, "CLI changed fixed ALi range");
            Check(recorder.LastMode == (noBackup ? WriteMode.OnlyWrite : WriteMode.ReadAndWrite), "CLI changed ALi default backup semantics");
        }
        string invalidPayload = Path.Combine(temporaryDirectory, "invalid-routing.maincode.bin");
        File.WriteAllBytes(invalidPayload, new byte[16]);
        foreach (bool noVerify in new[] { false, true })
        {
            int opened = 0;
            var adapter = new ALIM3801Flasher(CancellationToken.None, port => { opened++; throw new Exception("Unexpected serial factory call"); });
            Check(adapter.VerifyAfterWrite, "Adapter/CLI default must retain verification");
            // Invalid payload stops before connecting, after DoWrite has routed
            // the option onto the real adapter. No serial port can be opened.
            var failure = Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, new object[] { adapter, BKType.ALIM3801, invalidPayload, true, noVerify }));
            Check(failure.InnerException is InvalidDataException, "CLI verification routing failed before payload validation");
            Check(adapter.VerifyAfterWrite == !noVerify, "CLI --no-verify was not passed to the ALi adapter");
            Check(opened == 0, "CLI option-routing test attempted a serial connection");
        }
    }

    static void GuiWriteModes()
    {
        using (var form = new FormMain())
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var field in typeof(FormMain).GetFields(flags))
                if (typeof(System.Windows.Forms.Timer).IsAssignableFrom(field.FieldType))
                    ((System.Windows.Forms.Timer)field.GetValue(form))?.Stop();
            typeof(FormMain).GetField("aliM3801Selected", flags).SetValue(form, true);
            typeof(FormMain).GetMethod("updatePlatformCapabilityButtons", flags).Invoke(form, new object[] { true });
            Control write = form.Controls.Find("buttonWriteOnly", true).Single();
            Check(write.Text == "Flash (no backup)", "ALi no-backup button text is misleading");
            // GetState reflects this control's own Visible bit while the form
            // remains hidden, avoiding Show/Load and all device enumeration.
            var getState = typeof(Control).GetMethod("GetState", flags);
            Check((bool)getState.Invoke(write, new object[] { 2 }), "ALi no-backup button remains hidden");
            var verify = (CheckBox)form.Controls.Find("checkBoxAliVerifyAfterWrite", true).Single();
            Check(!verify.Checked, "ALi GUI post-write verification must start unchecked");
            Check((bool)getState.Invoke(verify, new object[] { 2 }), "ALi verification option is hidden for ALi");
            typeof(FormMain).GetMethod("updatePlatformCapabilityButtons", flags).Invoke(form, new object[] { false });
            Check(!verify.Enabled, "Verification choice remains editable during an operation");
            typeof(FormMain).GetMethod("updatePlatformCapabilityButtons", flags).Invoke(form, new object[] { true });
            Check(verify.Enabled, "Verification choice remains disabled while idle");
            using (var cancellation = new CancellationTokenSource())
            {
                typeof(FormMain).GetField("cts", flags).SetValue(form, cancellation);
                typeof(FormMain).GetField("curType", flags).SetValue(form, BKType.ALIM3801);
                foreach (bool checkedChoice in new[] { false, true })
                {
                    typeof(FormMain).GetField("aliVerifyAfterWrite", flags).SetValue(form, checkedChoice);
                    // Factory construction configures the adapter; it does not
                    // connect to hardware or start an operation.
                    typeof(FormMain).GetMethod("createFlasher", flags).Invoke(form, new object[0]);
                    var adapter = (ALIM3801Flasher)typeof(FormMain).GetField("flasher", flags).GetValue(form);
                    Check(adapter.VerifyAfterWrite == checkedChoice, "GUI's captured verification choice did not reach the adapter");
                    adapter.Dispose();
                }
                typeof(FormMain).GetField("flasher", flags).SetValue(form, null);
            }
            typeof(FormMain).GetField("aliM3801Selected", flags).SetValue(form, false);
            typeof(FormMain).GetMethod("updatePlatformCapabilityButtons", flags).Invoke(form, new object[] { true });
            Check(!(bool)getState.Invoke(verify, new object[] { 2 }), "ALi verification option is visible on other platforms");
        }
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
            Check(port.ReadBufferSize >= 1024 * 1024, "Continuous ALi dump requires the requested 1 MiB receive queue");
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
        CliReject("--chip ALIM3801 --port NOT_A_PORT fread --no-backup", "--no-backup is available only for ALIM3801 fwrite");
        CliReject("--chip BK7231N fwrite app.bin --no-backup", "--no-backup is available only for ALIM3801 fwrite");
        CliReject("--chip ALIM3801 fwrite app.bin --no-backup", "requires an explicit --port");
        CliReject("--chip ALIM3801 --port NOT_A_PORT fread --no-verify", "--no-verify is available only for ALIM3801 fwrite");
        CliReject("--chip BK7231N fwrite app.bin --no-verify", "--no-verify is available only for ALIM3801 fwrite");
        CliReject("--chip ALIM3801 fwrite app.bin --no-verify", "requires an explicit --port");
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

    // An independent sender with finite buffering, unlike Bootloader's unbounded
    // reply queue. It deliberately does not slow down when the receiver stalls.
    sealed class BoundedDumpTransport : IALIM3801Transport
    {
        readonly byte[] stream;
        readonly byte[] ring = new byte[1024 * 1024];
        readonly object gate = new object();
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        Thread producer;
        int head, buffered, step, droppedBytes;
        bool disposed;
        public int DroppedBytes { get { lock (gate) return droppedBytes; } }

        public BoundedDumpTransport(byte[] flash)
        {
            stream = new byte[flash.Length + 4];
            Array.Copy(flash, stream, flash.Length);
            Put32(stream, flash.Length, ReferenceCrc(flash, 0, flash.Length));
        }

        void Enqueue(byte[] source, int offset, int count)
        {
            lock (gate)
            {
                int accepted = Math.Min(count, ring.Length - buffered);
                int tail = (head + buffered) % ring.Length;
                int first = Math.Min(accepted, ring.Length - tail);
                Array.Copy(source, offset, ring, tail, first);
                Array.Copy(source, offset + first, ring, 0, accepted - first);
                buffered += accepted;
                droppedBytes += count - accepted;
                Monitor.PulseAll(gate);
            }
        }

        public void Write(byte[] bytes, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string[] commands = { "comtest 2\r", "XX", "dump\r", "O" };
            Check(step < commands.Length && Encoding.ASCII.GetString(bytes) == commands[step],
                "Unexpected command in bounded dump test");
            if (step < 3) Enqueue(bytes, 0, bytes.Length);
            if (step == 2)
            {
                var size = new byte[4]; Put32(size, 0, FlashSize);
                Enqueue(size, 0, size.Length);
            }
            if (step++ == 3)
            {
                producer = new Thread(() =>
                {
                    for (int offset = 0; offset < stream.Length; offset += 16384)
                    {
                        if (stop.IsCancellationRequested) return;
                        Enqueue(stream, offset, Math.Min(16384, stream.Length - offset));
                        if (stop.Token.WaitHandle.WaitOne(5)) return;
                    }
                }) { IsBackground = true, Name = "ALi finite-buffer UART sender" };
                producer.Start();
            }
        }

        public int Read(byte[] bytes, int offset, int count, int timeoutMilliseconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (buffered == 0) Monitor.Wait(gate, Math.Min(timeoutMilliseconds, 20));
                ct.ThrowIfCancellationRequested();
                int take = Math.Min(count, buffered);
                int first = Math.Min(take, ring.Length - head);
                Array.Copy(ring, head, bytes, offset, first);
                Array.Copy(ring, 0, bytes, offset + first, take - first);
                head = (head + take) % ring.Length;
                buffered -= take;
                return take;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            stop.Cancel();
            if (producer != null) Check(producer.Join(3000), "Bounded dump producer did not stop");
            stop.Dispose();
        }
    }

    sealed class Bootloader : IALIM3801Transport
    {
        public byte[] Flash;
        public List<string> Commands = new List<string>();
        public List<byte[]> PacketHistory = new List<byte[]>();
        public int Burns, Reboots, DumpStreams, PacketWrites, WriteCalls, ReadCalls, VersionCommands;
        public int VersionLength = 770;
        public bool CorruptVersionCrc;
        public Action<byte[]> MutateVersion, AfterBurn;
        public int RejectPacketsRemaining, CorruptDumpNumber;
        public uint AdvertisedSize = FlashSize;
        public bool CorruptAfterBurn, ReportBurnError, TruncateDump, SuppressBurnFinish;
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
            if (command == "version\r")
            {
                VersionCommands++;
                var version = new byte[770];
                version[0] = 0x11;
                int[] offsets = { 0, 0x4fe00, MainOffset, 0x290000, 0x2a0000, 0x2bff80 };
                for (int i = 0; i < offsets.Length; i++)
                {
                    Array.Copy(Flash, offsets[i], version, 2 + 128 * i, 128);
                    // The native version command substitutes a recalculated
                    // CRC even for headers whose stored CRC says NCRC.
                    Put32(version, 2 + 128 * i + 12, ReferenceCrc(Flash, offsets[i] + 16, (int)Get32(Flash, offsets[i] + 4)));
                }
                MutateVersion?.Invoke(version);
                Reply(bytes);
                Reply(new byte[] { (byte)(VersionLength >> 8), (byte)VersionLength });
                Reply(version);
                uint crc = ReferenceCrc(version, 0, version.Length);
                Reply32(CorruptVersionCrc ? crc ^ 1U : crc);
                addressSet = stageComplete = false;
                return;
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
                AfterBurn?.Invoke(Flash);
                Reply(ReportBurnError ? "B0064K\nE0000001\nF" : (SuppressBurnFinish ? "B0064K\n" : "B0064K\nF"));
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
