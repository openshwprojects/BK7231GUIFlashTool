using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace BK7231Flasher
{
    /// <summary>Opticum HD N2 / dump_maciej.bin maincode updates only.</summary>
    public sealed class ALIM3801Flasher : BaseFlasher
    {
        private readonly Func<string, IALIM3801Transport> transportFactory;
        private IALIM3801Transport activeTransport;
        private byte[] readResult;
        private int operationActive;

        public ALIM3801Flasher(CancellationToken cancellationToken)
            : this(cancellationToken, name => new ALIM3801SerialTransport(name)) { }

        public ALIM3801Flasher(CancellationToken cancellationToken, Func<string, IALIM3801Transport> transportFactory)
            : base(cancellationToken)
        {
            this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        }

        private void Log(string message) { logger?.addLog(message + Environment.NewLine, Color.Black); }
        private void State(string message, Color color) { logger?.setState(message, color); }

        private void RejectUnsafeOptions()
        {
            if (bCustomWriteMode || bOverwriteBootloader || bSkipKeyCheck || bIgnoreCRCErr || bUseCompressionIfPossible)
                throw new NotSupportedException("ALi M3801 does not allow custom/raw writes, extra compression, bootloader overwrite, or bypassing identity/CRC checks.");
        }

        private ALIM3801Protocol Connect()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(serialName))
                throw new ArgumentException("Select an explicit UART port for the tuner.");
            activeTransport = transportFactory(serialName);
            cancellationToken.ThrowIfCancellationRequested();
            var client = new ALIM3801Protocol(activeTransport, cancellationToken)
            {
                Log = Log,
                Progress = (phase, current, maximum) =>
                {
                    State(phase, Color.Black);
                    logger?.setProgress(current, maximum);
                },
                BackupSaved = path => Log("Full backup saved to " + Path.GetFullPath(path))
            };
            Log("UART is open at 115200, 8E1. Capturing boot for 60 seconds. Running Maciej v3 or newer restarts automatically on the capture command. If the tuner is off, power it on now; stock firmware and v1/v2 require a power cycle.");
            State("Capturing boot - v3 auto restart; otherwise power on", Color.Black);
            client.CaptureBoot(TimeSpan.FromSeconds(60));
            client.Handshake();
            Log("Classic ALi bootloader handshake OK.");
            return client;
        }

        private void Run(Action<ALIM3801Protocol> operation)
        {
            if (Interlocked.CompareExchange(ref operationActive, 1, 0) != 0)
                throw new InvalidOperationException("An ALi operation is already running.");
            ALIM3801Protocol client = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                client = Connect();
                operation(client);
            }
            catch (Exception error)
            {
                bool cancelled = cancellationToken.IsCancellationRequested;
                State(cancelled ? "ALi operation cancelled" : "ALi operation failed", cancelled ? Color.Orange : Color.Red);
                Log(error.Message);
                if (client != null && client.MayHaveWrittenFlash && !client.FlashVerified)
                    Log("Programming may continue on the tuner. Verification is incomplete; leave power connected and keep the backup.");
                if (cancelled && !(error is OperationCanceledException))
                    throw new OperationCanceledException("ALi operation cancelled.", error, cancellationToken);
                throw;
            }
            finally
            {
                closePort();
                Interlocked.Exchange(ref operationActive, 0);
            }
        }

        private string NewBackupPath(string kind)
        {
            // The UI's backup name is a label, never a caller-selected directory.
            string label = backupName ?? "";
            foreach (char invalid in Path.GetInvalidFileNameChars()) label = label.Replace(invalid, '_');
            if (label.Length > 80) label = label.Substring(0, 80);
            string name = kind + "_ALIM3801_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff")
                + (label.Length == 0 ? "" : "_" + label) + "_" + Guid.NewGuid().ToString("N") + ".bin";
            string directory = Path.GetFullPath("backups");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, name);
        }

        public override void doRead(int startSector = 0, int sectors = 1024, bool fullRead = false)
        {
            if (startSector != 0 || (!fullRead && sectors != 1024) || bCustomWriteMode)
                throw new NotSupportedException("ALi M3801 supports only a complete 4 MiB read from offset zero.");
            readResult = null;
            Run(client =>
            {
                Log("Reading complete flash and CRC (about 7 minutes).");
                readResult = client.Dump();
                State("4 MiB read and CRC verified", Color.DarkGreen);
            });
        }

        public override byte[] getReadResult() { return readResult == null ? null : (byte[])readResult.Clone(); }

        public override bool saveReadResult(int startOffset)
        {
            if (startOffset != 0) throw new NotSupportedException("ALi read results start at offset zero.");
            if (readResult == null) throw new InvalidOperationException("There is no complete CRC-checked read to save.");
            cancellationToken.ThrowIfCancellationRequested();
            string path = NewBackupPath("readResult");
            ALIM3801Protocol.WriteNewFile(path, readResult);
            Log("Saved full 4 MiB read to " + path);
            logger?.onReadResultQIOSaved((byte[])readResult.Clone(), "", path);
            return true;
        }

        public override void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
            RejectUnsafeOptions();
            if (rwMode != WriteMode.ReadAndWrite && rwMode != WriteMode.OnlyWrite)
                throw new NotSupportedException("ALi M3801 supports maincode updates only; configuration-only and erase modes are unavailable.");
            if (startSector != 0x50 || sectors != 1024)
                throw new NotSupportedException("ALi updates use fixed maincode offset 0x50000 and a complete 4 MiB backup.");
            if (string.IsNullOrWhiteSpace(sourceFileName)) throw new ArgumentException("Select a .maincode.bin file.");
            byte[] payload = File.ReadAllBytes(sourceFileName);
            ALIM3801Protocol.ValidateMaincode(payload);
            readResult = null;
            string backupPath = NewBackupPath("before_maincode");
            Run(client =>
            {
                var result = client.UpdateMaincode(payload, backupPath);
                readResult = result.After;
                logger?.onReadResultQIOSaved((byte[])result.Before.Clone(), "", result.BackupPath);
                State("Maincode verified and tuner rebooted", Color.DarkGreen);
                Log("Update complete. For another bootloader capture, running Maciej v3 or newer can restart automatically; older firmware requires a power cycle.");
            });
        }

        public override void doWrite(int startSector, byte[] data)
        {
            throw new NotSupportedException("Raw writes are disabled. Use the guarded ALi maincode update with a full backup.");
        }

        public override bool doErase(int startSector = 0, int sectors = 10, bool bAll = false)
        {
            throw new NotSupportedException("Erase is disabled for ALi M3801.");
        }

        public override void doTestReadWrite(int startSector = 0, int sectors = 10)
        {
            throw new NotSupportedException("Destructive read/write tests are disabled for ALi M3801.");
        }

        public override void closePort()
        {
            IALIM3801Transport transport = Interlocked.Exchange(ref activeTransport, null);
            transport?.Dispose();
        }

        public override void Dispose() { closePort(); }
    }
}
