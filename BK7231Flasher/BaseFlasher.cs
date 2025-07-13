using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public enum BKType
    {
        BK7231T,
        BK7231N,
        BK7231M,
        BK7238,
        BK7252,
        RTL8720DN,
        Detect,
        Invalid,
    }
    public enum WriteMode
    {
        ReadAndWrite,
        OnlyWrite,
        OnlyOBKConfig
    }

    public class BaseFlasher
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

        public void setBasic(ILogListener logger, string serialName, BKType bkType, int baudrate = 921600)
        {
            this.logger = logger;
            this.serialName = serialName;
            this.chipType = bkType;
            this.baudrate = baudrate;
        }
        public void addLog(string s)
        {
            logger.addLog(s, Color.Black);
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
        public virtual void doRead(int startSector = 0x000, int sectors = 10)
        {

        }
        public virtual byte[] getReadResult()
        {
            return null;
        }
        public virtual bool doErase(int startSector = 0x000, int sectors = 10)
        {
            return false;
        }
        public virtual void closePort()
        {

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
    }
}

