using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    /// <summary>One explicitly selected UART, or an in-memory transport for tests.</summary>
    public interface IALIM3801Transport : IDisposable
    {
        void Write(byte[] data, CancellationToken cancellationToken);
        int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken);
    }

    public sealed class ALIM3801SerialTransport : IALIM3801Transport
    {
        private readonly SerialPort port;
        private int disposed;
        private int receiveErrors;

        public ALIM3801SerialTransport(string portName)
        {
            port = CreatePort(portName);
            port.ErrorReceived += OnErrorReceived;
            try { port.Open(); }
            catch { port.ErrorReceived -= OnErrorReceived; port.Dispose(); throw; }
        }

        /// <summary>Build the fixed profile without opening hardware.</summary>
        public static SerialPort CreatePort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("ALi M3801 requires an explicit serial port.", nameof(portName));
            return new SerialPort(portName, 115200, Parity.Even, 8, StopBits.One)
            {
                Handshake = System.IO.Ports.Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                // The loader streams without flow control; allow headroom for host scheduling stalls.
                ReadBufferSize = 1024 * 1024,
                ReadTimeout = 250,
                WriteTimeout = 10000
            };
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs args)
        {
            int errors = (int)(args.EventType & (SerialError.RXOver | SerialError.Overrun
                | SerialError.RXParity | SerialError.Frame));
            int previous;
            do { previous = Volatile.Read(ref receiveErrors); }
            while (Interlocked.CompareExchange(ref receiveErrors, previous | errors, previous) != previous);
        }

        private void ThrowIfReceiveError()
        {
            var errors = (SerialError)Volatile.Read(ref receiveErrors);
            if (errors != 0)
                throw new IOException("ALi UART receive error (" + errors
                    + "). The received stream may be incomplete or corrupted; operation stopped.");
        }

        public void Write(byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfReceiveError();
            port.Write(data, 0, data.Length);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfReceiveError();
            port.ReadTimeout = Math.Max(1, Math.Min(timeoutMilliseconds, 250));
            try
            {
                int result = port.Read(buffer, offset, Math.Min(count, Math.Max(1, port.BytesToRead)));
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfReceiveError();
                return result;
            }
            catch (TimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfReceiveError();
                return 0;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                port.ErrorReceived -= OnErrorReceived;
                port.Dispose();
            }
        }
    }

    public sealed class ALIM3801UpdateResult
    {
        public byte[] Before { get; internal set; }
        public byte[] After { get; internal set; }
        public string BackupPath { get; internal set; }
        public IReadOnlyList<string> BurnProgress { get; internal set; }
    }

    /// <summary>
    /// Classic ALi protocol verified against dump_maciej.bin. Updates normally
    /// save a full backup. The explicit no-backup path checks short profile
    /// metadata first. Readback can be explicitly skipped; FlashVerified is
    /// true only after a successful complete readback comparison.
    /// </summary>
    public sealed class ALIM3801Protocol
    {
        public const int FlashSize = 0x400000;
        public const int MainOffset = 0x50000;
        public const int MainSpan = 0x240000;
        public const int LoaderSize = 0x4FE00; // Excludes per-unit HDCP keys.
        public const int BlockSize = 1024;
        public const string LoaderSha256 = "6e1489a037dfe702a775912ee8eaabc14030b8f3cd65397792b3f217f6f29fa4";
        private const uint MainId = 0x01FE0101;
        private const uint NoCrc = 0x4E435243;
        private static readonly uint[] CrcTable = MakeCrcTable();
        private readonly IALIM3801Transport transport;
        private readonly CancellationToken cancellationToken;
        private bool handshaken;
        private bool addressed;
        private bool staged;

        public int CommandTimeoutMilliseconds { get; set; } = 10000;
        public int DumpTimeoutMilliseconds { get; set; } = 600000;
        public int BurnTimeoutMilliseconds { get; set; } = 120000;
        public bool MayHaveWrittenFlash { get; private set; }
        public bool FlashVerified { get; private set; }
        public Action<string> Log { get; set; }
        public Action<string, int, int> Progress { get; set; }
        public Action<string> BackupSaved { get; set; }

        public ALIM3801Protocol(IALIM3801Transport transport, CancellationToken cancellationToken)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.cancellationToken = cancellationToken;
        }

        private static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < table.Length; n++)
            {
                uint crc = n << 24;
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04C11DB7u : 0u);
                table[n] = crc;
            }
            return table;
        }

        public static uint Crc32Mpeg2(byte[] data, int offset = 0, int count = -1)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (count == -1) count = data.Length - offset;
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new ArgumentOutOfRangeException(nameof(count));
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + count; i++)
                crc = (crc << 8) ^ CrcTable[((crc >> 24) ^ data[i]) & 255];
            return crc;
        }

        private static uint ReadBigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteBigEndian(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24); data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8); data[offset + 3] = (byte)value;
        }

        private static bool Equal(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
            return true;
        }

        public static void ValidateMaincode(byte[] payload, int offset = MainOffset)
        {
            if (offset != MainOffset) throw new InvalidDataException("Only maincode at 0x50000 is writable.");
            if (payload == null || payload.Length < 142 || payload.Length > MainSpan)
                throw new InvalidDataException("Select an ALi .maincode.bin chunk, not a full flash image or raw application.");
            uint length = ReadBigEndian(payload, 4);
            if (ReadBigEndian(payload, 0) != MainId || ReadBigEndian(payload, 8) != MainSpan)
                throw new InvalidDataException("Maincode chunk ID/span does not match the supported layout.");
            if (length < 126 || (long)length + 16 > payload.Length)
                throw new InvalidDataException("Incomplete maincode chunk.");
            if (Crc32Mpeg2(payload, 16, (int)length) != ReadBigEndian(payload, 12))
                throw new InvalidDataException("Maincode CRC mismatch.");
            uint load = ReadBigEndian(payload, 120), entry = ReadBigEndian(payload, 124);
            if (load == 0 || load == uint.MaxValue) load = 0x80000200;
            if (entry == 0 || entry == uint.MaxValue) entry = load;
            if (load != 0x80000200 || entry != 0x80000200)
                throw new InvalidDataException("Maincode load/entry must be 0x80000200.");
            // Match the old decoder's probability buffer and known-size stream.
            if (payload[128] != 0x6C || payload[129] != 0 || payload[130] != 0
                || payload[131] != 0x80 || payload[132] != 0)
                throw new InvalidDataException("Expected LZMA lc=0, lp=2, pb=2, 8 MiB dictionary.");
            ulong size = 0;
            for (int i = 7; i >= 0; i--) size = (size << 8) | payload[133 + i];
            if (size == 0 || size > 0x800000)
                throw new InvalidDataException("LZMA must declare an application size between 1 and 8 MiB.");
        }

        public static void ValidateTargetFlash(byte[] flash)
        {
            if (flash == null || flash.Length != FlashSize)
                throw new InvalidDataException("Target flash must contain exactly 4 MiB.");
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(flash, 0, LoaderSize)).Replace("-", "").ToLowerInvariant();
            if (hash != LoaderSha256)
                throw new InvalidDataException("Bootloader fingerprint does not match the supported dump_maciej.bin profile.");
            int[] offsets = { 0, 0x4FE00, MainOffset, 0x290000, 0x2A0000, 0x2BFF80 };
            uint[] ids = { 0x23010010, 0x09F60100, MainId, 0x02FD0100, 0x03FC0100, 0x04FB0100 };
            for (int i = 0; i < offsets.Length; i++)
            {
                int current = offsets[i];
                uint length = ReadBigEndian(flash, current + 4);
                uint span = ReadBigEndian(flash, current + 8);
                uint crc = ReadBigEndian(flash, current + 12);
                uint expectedSpan = i + 1 < offsets.Length ? (uint)(offsets[i + 1] - current) : 0;
                if (ReadBigEndian(flash, current) != ids[i] || span != expectedSpan
                    || (long)current + 16 + length > FlashSize
                    || (span != 0 && (long)length + 16 > span))
                    throw new InvalidDataException("Unexpected chunk layout at 0x" + current.ToString("X") + ".");
                if (crc != NoCrc && Crc32Mpeg2(flash, current + 16, (int)length) != crc)
                    throw new InvalidDataException("Chunk CRC mismatch at 0x" + current.ToString("X") + ".");
            }
        }

        public static void WriteNewFile(string path, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A new backup filename is required.", nameof(path));
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(data, 0, data.Length);
                output.Flush(true);
            }
        }

        private void ResetStaging() { addressed = false; staged = false; }

        private void Write(byte[] data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            transport.Write(data, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private int ReadSome(byte[] data, int offset, int count, int timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = transport.Read(data, offset, count, Math.Max(1, timeout), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (read < 0 || read > count) throw new IOException("Transport returned an invalid byte count.");
            return read;
        }

        private byte[] ReadExact(int count, int timeout, string progress = null)
        {
            if (timeout <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));
            var result = new byte[count];
            int received = 0, reported = 0;
            long lastDataMilliseconds = 0;
            var timer = Stopwatch.StartNew();
            while (received < count)
            {
                int remaining = timeout - (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue);
                if (remaining <= 0)
                    throw new TimeoutException("ALi UART timeout: expected " + count + " bytes, received " + received
                        + " after " + timer.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)
                        + " s; no data for " + ((timer.ElapsedMilliseconds - lastDataMilliseconds) / 1000.0)
                            .ToString("F1", CultureInfo.InvariantCulture) + " s.");
                int n = ReadSome(result, received, count - received, remaining);
                received += n;
                if (n > 0) lastDataMilliseconds = timer.ElapsedMilliseconds;
                if (progress != null && (received - reported >= 16384 || received == count))
                {
                    Progress?.Invoke(progress, received, count);
                    reported = received;
                }
                if (n == 0) cancellationToken.WaitHandle.WaitOne(1);
            }
            return result;
        }

        private void Expect(byte[] expected, int timeout = 0)
        {
            byte[] actual = ReadExact(expected.Length, timeout == 0 ? CommandTimeoutMilliseconds : timeout);
            if (!Equal(actual, expected))
                throw new IOException("Unexpected ALi UART reply. Expected " + BitConverter.ToString(expected)
                    + ", received " + BitConverter.ToString(actual) + ".");
        }

        private void Command(string command)
        {
            byte[] raw = Encoding.ASCII.GetBytes(command + "\r");
            Write(raw);
            Expect(raw);
        }

        public byte[] CaptureBoot(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(60))
                throw new ArgumentOutOfRangeException(nameof(duration), "Capture must be between 0 and 60 seconds.");
            handshaken = false;
            ResetStaging();
            var timer = Stopwatch.StartNew();
            var buffer = new byte[4096];
            using (var received = new MemoryStream())
            {
                while (timer.Elapsed < duration)
                {
                    long probeStart = timer.ElapsedMilliseconds;
                    Write(new byte[] { (byte)'c' });
                    int n = ReadSome(buffer, 0, buffer.Length, 50);
                    received.Write(buffer, 0, n);
                    int pause = (int)Math.Min(50 - (timer.ElapsedMilliseconds - probeStart), (duration - timer.Elapsed).TotalMilliseconds);
                    if (pause > 0 && cancellationToken.WaitHandle.WaitOne(pause)) cancellationToken.ThrowIfCancellationRequested();
                }
                Write(new byte[] { 13 });
                var drain = Stopwatch.StartNew();
                while (drain.ElapsedMilliseconds < 2000)
                {
                    int n = ReadSome(buffer, 0, buffer.Length, 200);
                    if (n == 0) return received.ToArray();
                    received.Write(buffer, 0, n);
                }
                throw new IOException("UART did not become quiet after boot capture.");
            }
        }

        public void Handshake()
        {
            handshaken = false;
            ResetStaging();
            Command("comtest 2");
            Write(Encoding.ASCII.GetBytes("XX"));
            Expect(Encoding.ASCII.GetBytes("XX"));
            handshaken = true;
        }

        private void RequireHandshake()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!handshaken) throw new InvalidOperationException("Classic bootloader handshake is required.");
        }

        public byte[] Dump()
        {
            RequireHandshake();
            Command("dump");
            uint size = ReadBigEndian(ReadExact(4, CommandTimeoutMilliseconds), 0);
            if (size != FlashSize)
            {
                Write(new byte[] { (byte)'E' });
                throw new InvalidDataException("Expected 4 MiB flash, received size 0x" + size.ToString("X") + ".");
            }
            Write(new byte[] { (byte)'O' });
            byte[] data = ReadExact(FlashSize, DumpTimeoutMilliseconds, "Reading flash");
            uint crc = ReadBigEndian(ReadExact(4, CommandTimeoutMilliseconds), 0);
            if (crc != Crc32Mpeg2(data)) throw new InvalidDataException("UART dump CRC mismatch.");
            return data;
        }

        private void Address()
        {
            ResetStaging();
            // ali_atoi is decimal-only: "0x50000" would address the loader at zero.
            Command("address " + MainOffset.ToString(CultureInfo.InvariantCulture));
            Expect(new byte[] { (byte)'O' });
            addressed = true;
        }

        private void Transfer(byte[] payload)
        {
            if (!addressed || staged) throw new InvalidOperationException("A fresh maincode address is required.");
            ValidateMaincode(payload);
            try
            {
                Command("transfer " + payload.Length.ToString(CultureInfo.InvariantCulture));
                Expect(new byte[] { (byte)'O' });
                for (int offset = 0; offset < payload.Length; offset += BlockSize)
                {
                    int length = Math.Min(BlockSize, payload.Length - offset);
                    var block = new byte[length + 4];
                    Buffer.BlockCopy(payload, offset, block, 0, length);
                    WriteBigEndian(block, length, Crc32Mpeg2(block, 0, length));
                    for (int attempt = 0; ; attempt++)
                    {
                        Write(block);
                        byte reply = ReadExact(1, CommandTimeoutMilliseconds)[0];
                        if (reply == (byte)'O') break;
                        if (reply != (byte)'E' || attempt == 2)
                            throw new IOException("Transfer block " + (offset / BlockSize) + " rejected or retry limit reached.");
                    }
                    Progress?.Invoke("Transferring maincode", offset + length, payload.Length);
                }
                staged = true;
            }
            catch { ResetStaging(); throw; }
        }

        private List<string> Burn()
        {
            if (!addressed || !staged) throw new InvalidOperationException("A complete transfer is required before burn.");
            ResetStaging();
            // A missing acknowledgement does not prove erase/program did not start.
            MayHaveWrittenFlash = true;
            Command("burn");
            Expect(new byte[] { (byte)'O' });
            var timer = Stopwatch.StartNew();
            var lines = new List<string>();
            var line = new StringBuilder();
            while (true)
            {
                int remaining = BurnTimeoutMilliseconds - (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue);
                if (remaining <= 0) throw new TimeoutException("Burn did not send final F.");
                byte value = ReadExact(1, remaining)[0];
                if (value == (byte)'F' && line.Length == 0)
                {
                    foreach (string item in lines)
                        if (item.StartsWith("E", StringComparison.Ordinal)) throw new IOException("Flash programming/verification error: " + item);
                    return lines;
                }
                line.Append((char)value);
                if (line.Length > 128) throw new IOException("Invalid burn progress.");
                if (value == 10)
                {
                    string entry = line.ToString().TrimEnd('\r', '\n');
                    if (!entry.StartsWith("B", StringComparison.Ordinal) && !entry.StartsWith("E", StringComparison.Ordinal))
                        throw new IOException("Unexpected burn progress: " + entry);
                    lines.Add(entry);
                    Log?.Invoke(entry);
                    line.Clear();
                }
            }
        }

        private void CheckVersionProfile(bool verifyAfterWrite)
        {
            RequireHandshake();
            ResetStaging();
            Command("version");
            byte[] lengthBytes = ReadExact(2, CommandTimeoutMilliseconds);
            int length = (lengthBytes[0] << 8) | lengthBytes[1];
            const int headerSize = 128;
            if (length != 2 + 6 * headerSize)
                throw new InvalidDataException("Unexpected ALi version response length; expected six chunk headers.");
            byte[] version = ReadExact(length, CommandTimeoutMilliseconds);
            uint crc = ReadBigEndian(ReadExact(4, CommandTimeoutMilliseconds), 0);
            if (crc != Crc32Mpeg2(version))
                throw new InvalidDataException("ALi version response CRC mismatch.");
            // Verified against the exact dump's native version command. This
            // is a compatibility check, not a hash of the bootloader code.
            if (version[0] != 0x11)
                throw new InvalidDataException("Unexpected flash ID in ALi version response.");
            int[] offsets = { 0, 0x4FE00, MainOffset, 0x290000, 0x2A0000, 0x2BFF80 };
            uint[] ids = { 0x23010010, 0x09F60100, MainId, 0x02FD0100, 0x03FC0100, 0x04FB0100 };
            for (int i = 0; i < offsets.Length; i++)
            {
                int header = 2 + i * headerSize;
                uint chunkLength = ReadBigEndian(version, header + 4);
                uint span = ReadBigEndian(version, header + 8);
                uint expectedSpan = i + 1 < offsets.Length ? (uint)(offsets[i + 1] - offsets[i]) : 0;
                if (ReadBigEndian(version, header) != ids[i] || span != expectedSpan
                    || (long)offsets[i] + 16 + chunkLength > FlashSize
                    || (span != 0 && (long)chunkLength + 16 > span))
                    throw new InvalidDataException("Unexpected ALi version chunk layout at 0x" + offsets[i].ToString("X") + ".");
            }
            if (ReadBigEndian(version, 2 + 4) != 0
                || Encoding.ASCII.GetString(version, 2 + 16, 16).TrimEnd('\0') != "bootloader"
                || Encoding.ASCII.GetString(version, 2 + 32, 16).TrimEnd('\0') != "1.0.0"
                || Encoding.ASCII.GetString(version, 2 + 48, 16).TrimEnd('\0') != "2009-06-18")
                throw new InvalidDataException("Unexpected ALi bootloader header in version response.");
            Log?.Invoke(verifyAfterWrite
                ? "Version profile and chunk layout match. Exact bootloader fingerprint will be checked in readback."
                : "Version profile and chunk layout match. Readback verification is disabled.");
            if (version[1] != 0)
                Log?.Invoke("Bootloader reports chunk status 0x" + version[1].ToString("X2") + ".");
        }

        /// <summary>
        /// Explicitly skip the initial dump and backup. Short version metadata
        /// checks the profile before writing. Optional readback checks the exact
        /// loader and payload. With readback disabled, After is null and
        /// FlashVerified remains false. There is no before-image for an outside-range comparison.
        /// </summary>
        public ALIM3801UpdateResult UpdateMaincodeWithoutBackup(byte[] payload, bool reboot = true, bool verifyAfterWrite = true)
        {
            ValidateMaincode(payload);
            RequireHandshake();
            var stablePayload = (byte[])payload.Clone();
            MayHaveWrittenFlash = false;
            FlashVerified = false;
            ResetStaging();
            try
            {
                Log?.Invoke("Writing without a backup. Checking the short bootloader version profile.");
                CheckVersionProfile(verifyAfterWrite);
                cancellationToken.ThrowIfCancellationRequested();
                Address();
                Transfer(stablePayload);
                Log?.Invoke("Programming maincode. Leave tuner power connected.");
                List<string> burnProgress = Burn();
                byte[] after = null;
                if (verifyAfterWrite)
                {
                    Log?.Invoke("Reading all 4 MiB back to verify the payload, loader and chunk integrity (about 7 minutes).");
                    after = Dump();
                    for (int i = 0; i < stablePayload.Length; i++)
                        if (after[MainOffset + i] != stablePayload[i])
                            throw new InvalidDataException("Maincode readback mismatch. Tuner left in bootloader; do not reboot.");
                    ValidateTargetFlash(after);
                    FlashVerified = true;
                    Log?.Invoke("Maincode readback, bootloader fingerprint and chunk integrity verified. No pre-write backup was created.");
                }
                else
                    Log?.Invoke("Readback verification skipped; bootloader reported programming complete.");
                if (reboot) Reboot();
                return new ALIM3801UpdateResult { After = after, BurnProgress = burnProgress.AsReadOnly() };
            }
            catch { handshaken = false; ResetStaging(); throw; }
        }

        public ALIM3801UpdateResult UpdateMaincode(byte[] payload, string backupPath, bool reboot = true, bool verifyAfterWrite = true)
        {
            ValidateMaincode(payload);
            if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("A backup path is required.", nameof(backupPath));
            if (File.Exists(backupPath)) throw new IOException("Backup already exists; choose a new filename.");
            RequireHandshake();
            var stablePayload = (byte[])payload.Clone();
            MayHaveWrittenFlash = false;
            FlashVerified = false;
            ResetStaging();
            try
            {
                Log?.Invoke("Reading the complete 4 MiB backup (about 7 minutes at 115200 8E1).");
                byte[] before = Dump();
                ValidateTargetFlash(before);
                cancellationToken.ThrowIfCancellationRequested();
                WriteNewFile(backupPath, before);
                BackupSaved?.Invoke(backupPath);
                Log?.Invoke("Backup saved. Transferring maincode to RAM.");
                cancellationToken.ThrowIfCancellationRequested();
                Address();
                Transfer(stablePayload);
                Log?.Invoke("Programming maincode. Leave tuner power connected.");
                List<string> burnProgress = Burn();
                byte[] after = null;
                if (verifyAfterWrite)
                {
                    Log?.Invoke("Reading all 4 MiB back for verification (about 7 minutes).");
                    after = Dump();
                    var expected = (byte[])before.Clone();
                    Buffer.BlockCopy(stablePayload, 0, expected, MainOffset, stablePayload.Length);
                    if (!Equal(after, expected))
                        throw new InvalidDataException("Full flash readback mismatch. Tuner left in bootloader; do not reboot.");
                    ValidateTargetFlash(after);
                    FlashVerified = true;
                    Log?.Invoke("Full readback verified; bootloader, keys and settings unchanged.");
                }
                else
                    Log?.Invoke("Readback verification skipped; bootloader reported programming complete.");
                if (reboot) Reboot();
                return new ALIM3801UpdateResult { Before = before, After = after, BackupPath = backupPath, BurnProgress = burnProgress.AsReadOnly() };
            }
            catch { handshaken = false; ResetStaging(); throw; }
        }

        public void Reboot()
        {
            RequireHandshake();
            handshaken = false;
            ResetStaging();
            Command("reboot");
            Expect(new byte[] { (byte)'O' });
        }
    }
}
