using System;
using System.Drawing;
using System.IO.Ports;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    public enum BKType
    {
        BK7231M,
        BK7231N,
        BK7231T,
        BK7231U,
        BK7236,
        BK7238,
        BK7252,
        BK7252N,
        BK7258,
        BekenSPI,
        BL602,
        BL616,
        BL702,
        ECR6600,
        ESP32,
        ESP32C3,
        ESP32S3,
        ESP8266,
        GenericSPI,
        LN882H,
        LN8825,
        RDA5981,
        RTL8710B,
        RTL8720D,
        RTL8720E,
        RTL8721DA,
        RTL87X0C,
        TR6260,
        W600,
        W800,
        XR806,
        XR809,
        XR872,

        Detect,
        Invalid,
    }
    public enum WriteMode
    {
        ReadAndWrite,
        OnlyWrite,
        OnlyOBKConfig,
        OnlyErase
    }

    public class ChipType
    {
        public BKType Type { get; }

        public string Name { get; }

        public ChipType(BKType type, string name)
        {
            Type = type;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class BaseFlasher : IDisposable
    {
        protected ILogListener logger;
        protected string backupName;
        protected float cfg_readTimeOutMultForSerialClass = 1.0f;
        protected float cfg_readTimeOutMultForLoop = 1.0f;
        protected int cfg_readReplyStyle = 0;
        protected bool bOverwriteBootloader = false;
        protected bool bSkipKeyCheck;
        protected bool bIgnoreCRCErr = false;
        protected SerialPort serial;
        protected string serialName;
        protected BKType chipType = BKType.BK7231N;
        protected int baudrate = 921600;
        protected CancellationToken cancellationToken;
        protected XMODEM xm;
        protected bool isCancelled = false;

        public BaseFlasher(CancellationToken ct)
        {
            cancellationToken = ct;
            ct.Register(() =>
            {
                isCancelled = true;
                try
                {
                    if(xm != null)
                    {
                        try { xm.CancelFileTransfer(); } catch { }
                        try { xm.InProgress.Wait(500); } catch { }
                    }
                }
                finally
                {
                    try { closePort(); } catch { }
                }
            });
        }

        public void setBasic(ILogListener logger, string serialName, BKType bkType, int baudrate = 921600)
        {
            this.logger = logger;
            this.serialName = serialName;
            this.chipType = bkType;
            this.baudrate = baudrate;
        }
        public void addLog(string format, params object[] args)
        {
            string s = string.Format(format, args);
            logger.addLog(s, Color.Black);
        }
        public void addLog(string s)
        {
            logger.addLog(s, Color.Black);
        }
        public void addLogLine(string format, params object[] args)
        {
            string s = string.Format(format, args);
            logger.addLog(s + Environment.NewLine, Color.Black);
        }
        public void addLogLine(string s)
        {
            logger.addLog(s+Environment.NewLine, Color.Black);
        }
        public void addErrorLine(string s)
        {
            logger.addLog(s + Environment.NewLine, Color.Red);
        }
        public void addError(string s)
        {
            logger.addLog(s, Color.Red);
        }
        public void addSuccess(string s)
        {
            logger.addLog(s, Color.Green);
        }
        public void addWarning(string s)
        {
            logger.addLog(s, Color.Orange);
        }
        public void addWarningLine(string s)
        {
            logger.addLog(s + Environment.NewLine, Color.Orange);
        }
        public void setBackupName(string newName)
        {
            this.backupName = newName;
            if (this.backupName.Length == 0)
            {
                addLog("Backup name has not been set, so output file will only contain flash type/date." + Environment.NewLine);
            }
            else
            {
                addLog("Backup name is set to " + this.backupName + "." + Environment.NewLine);
            }
        }
        public static string formatHex(int i)
        {
            return "0x" + i.ToString("X2");
        }
        public static string formatHex(uint i)
        {
            return "0x" + i.ToString("X2");
        }
        public static string formatHex(long i)
        {
            return "0x" + i.ToString("X2");
        }
        public void setSkipKeyCheck(bool b)
        {
            bSkipKeyCheck = b;
        }
        public void setIgnoreCRCErr(bool b)
        {
            bIgnoreCRCErr = b;
        }

        public void setOverwriteBootloader(bool b)
        {
            bOverwriteBootloader = b;
        }
        public void setReadTimeOutMultForSerialClass(float f)
        {
            this.cfg_readTimeOutMultForSerialClass = f;
        }
        public void setReadTimeOutMultForLoop(float f)
        {
            this.cfg_readTimeOutMultForLoop = f;
        }
        public void setReadReplyStyle(int i)
        {
            this.cfg_readReplyStyle = i;
        }


        public virtual void doWrite(int startSector, byte[] data)
        {

        }
        public virtual void doRead(int startSector = 0x000, int sectors = 10, bool fullRead = false)
        {

        }
        public virtual byte[] getReadResult()
        {
            return null;
        }
        public virtual bool doErase(int startSector = 0x000, int sectors = 10, bool bAll = false)
        {
            return false;
        }
        public virtual void closePort()
        {
            if (serial != null)
            {
                try { serial.Close(); } catch { }
                try { serial.Dispose(); } catch { }
                serial = null;
            }
        }
        public virtual void doTestReadWrite(int startSector = 0x000, int sectors = 10)
        {
        }

        public virtual void doReadAndWrite(int startSector, int sectors, string sourceFileName, WriteMode rwMode)
        {
        }
        public virtual bool saveReadResult(int startOffset)
        {
            return false;
        }

        public virtual void Xm_PacketSent(int sentBytes, int total, int sequence, uint offset)
        {
            if((sequence % 4) == 1)
            {
                addLog($"0x{offset:X}... ");
            }

            logger.setProgress(sentBytes, total);
        }

        protected bool WasCancelled(Exception ex = null)
        {
            return ex is OperationCanceledException || isCancelled || cancellationToken.IsCancellationRequested;
        }

        protected void RethrowIfCancelled(Exception ex)
        {
            if(ex is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
            if(isCancelled || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Operation cancelled.", ex, cancellationToken);
            }
        }

        protected void LogCancelledOperation()
        {
            logger?.setState("Interrupted by user.", Color.Yellow);
            addWarningLine("Operation cancelled.");
        }

        protected void ReportSerialOpenFailure(Exception ex)
        {
            try { closePort(); } catch { }
            logger?.setState("Open serial failed!", Color.Red);

            string portDisplay = string.IsNullOrWhiteSpace(serialName) ? "selected serial port" : serialName;
            if(ex is UnauthorizedAccessException)
            {
                addErrorLine($"Cannot open {portDisplay}: it is already in use by another program or access is denied.");
                return;
            }

            string message = ex?.Message;
            if(string.IsNullOrWhiteSpace(message))
            {
                addErrorLine($"Cannot open {portDisplay}.");
                return;
            }

            addErrorLine($"Cannot open {portDisplay}: {message}");
        }

        protected void SetReadCompleteState()
        {
            logger?.setState("Read complete", Color.DarkGreen);
        }

        protected void SetWriteCompleteState()
        {
            logger?.setState("Write complete", Color.DarkGreen);
        }

        protected void SetEraseCompleteState()
        {
            logger?.setState("Erase complete", Color.DarkGreen);
        }

        public virtual void Dispose()
        {
            try { closePort(); } catch { }
        }

        public static string HashToStr(byte[] data)
        {
            var sb = new StringBuilder();
            foreach(byte b in data)
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }
    }
}

