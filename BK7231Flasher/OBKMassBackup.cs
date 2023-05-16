using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BK7231Flasher
{
    class OBKMassBackup
    {
        List<OBKDeviceAPI> devices = new List<OBKDeviceAPI>();
        Thread thread;

        internal void addDevice(OBKDeviceAPI dev)
        {
            devices.Add(dev);
        }
        internal void beginBackupThread()
        {
            thread = new Thread(workerThread);
        }
        void workerThread()
        {
            for(int i = 0; i < devices.Count; i++)
            {

            }
        }
    }
}
