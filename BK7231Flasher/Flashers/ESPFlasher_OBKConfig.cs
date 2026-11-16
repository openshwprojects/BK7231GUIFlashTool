// ESPFlasher_OBKConfig.cs
// OpenBeken (OBK) config read/write for ESP-IDF targets.
// OBK on ESP-IDF stores mainConfig_t in NVS (namespace "config", key "cfg"), not at a fixed
// flash offset, so the generic "raw config sector" path used by other platforms does not apply.
// Everything runs inside a single bootloader session (one Connect/reset, one stub upload).

using System;
using System.Drawing;
using System.IO;

namespace BK7231Flasher
{
    public partial class ESPFlasher
    {
        /// <summary>
        /// Reads the NVS partition (located via the partition table at 0x8000) into the read
        /// result buffer, so FormMain can pass getReadResult() to OBKConfig.loadFrom.
        /// </summary>
        public void doReadOBKConfigPartition()
        {
            ms = null;
            if (!OBKEspNvsConfig.IsSupportedChip(chipType))
            {
                addErrorLine($"OBK config read is not supported for {chipType} yet.");
                return;
            }
            if (!Connect())
                return;

            // Same flash setup as doRead()
            ReadMac();
            uint? fid = ReadFlashId();
            if (fid.HasValue)
            {
                uint sizeCode = (fid.Value >> 16) & 0xFF;
                if (sizeCode > 0 && sizeCode <= 31)
                    SpiSetParams(1u << (int)sizeCode);
            }
            StartStubForSession();

            logger.setState("Reading OBK config...", Color.LightBlue);
            EspPartitionEntry nvs = FindNvsPartitionInSession(null);
            if (nvs == null)
            {
                logger.setState("Read error", Color.Red);
                return;
            }

            addLogLine($"Reading NVS partition '{nvs.Label}' at 0x{nvs.Offset:X} (0x{nvs.Size:X} bytes)...");
            byte[] data = ReadFlashInSession((uint)nvs.Offset, (uint)nvs.Size);
            if (data == null || data.Length != nvs.Size)
            {
                addErrorLine("Failed to read NVS partition.");
                logger.setState("Read error", Color.Red);
                return;
            }
            ms = new MemoryStream(data);
            addLogLine("NVS partition read complete.");
            logger.setState("Read complete", Color.LightGreen);
        }

        /// <summary>
        /// Writes optional firmware, then the OBK config as a fresh NVS partition image,
        /// in one session. firmware may be null (config-only write).
        /// </summary>
        void doWriteWithOBKConfig(uint firmwareOffset, byte[] firmware, OBKConfig cfg)
        {
            if (cfg == null)
            {
                addErrorLine("No OBK config to write.");
                return;
            }
            if (!Connect())
                return;
            StartStubForSession();

            if (firmware != null)
            {
                if (!WriteFlashInSession(firmwareOffset, firmware, out _))
                {
                    addErrorLine("Firmware write failed, OBK config was not written.");
                    return;
                }
            }

            // If the firmware image starts at 0x0 it contains the partition table: use it
            // instead of reading it back from the chip.
            byte[] firmwareAt0 = (firmware != null && firmwareOffset == 0) ? firmware : null;
            bool cfgOk = WriteOBKConfigInSession(cfg, firmwareAt0);

            SendFlashEnd();

            if (cfgOk)
            {
                addSuccess("OBK config written." + Environment.NewLine);
                logger.setState("OBK config written", Color.LightGreen);
            }
            else
            {
                addErrorLine("OBK config write failed.");
                logger.setState("OBK config write failed", Color.Red);
            }
        }

        bool WriteOBKConfigInSession(OBKConfig cfg, byte[] firmwareAt0)
        {
            EspPartitionEntry nvs = FindNvsPartitionInSession(firmwareAt0);
            if (nvs == null)
                return false;

            cfg.saveConfig(chipType);
            byte[] cfgData = cfg.getData();
            if (cfgData == null || cfgData.Length < OBKEspNvsConfig.ConfigBlobSize)
            {
                addErrorLine($"OBK config buffer has unexpected size ({(cfgData == null ? 0 : cfgData.Length)} bytes).");
                return false;
            }
            byte[] blob = new byte[OBKEspNvsConfig.ConfigBlobSize];
            Array.Copy(cfgData, blob, blob.Length);

            EspNvsBuildResult image = OBKEspNvsConfig.BuildConfigPartition(blob, nvs.Size);
            if (!image.Success)
            {
                addErrorLine("Failed to build NVS image: " + image.Error);
                return false;
            }

            addLog("Now will also write OBK config..." + Environment.NewLine);
            addLog("Long name from CFG: " + cfg.longDeviceName + Environment.NewLine);
            addLog("Short name from CFG: " + cfg.shortDeviceName + Environment.NewLine);
            addLog("Web Root from CFG: " + cfg.webappRoot + Environment.NewLine);
            addWarningLine($"Replacing whole NVS partition '{nvs.Label}' at 0x{nvs.Offset:X} (Wi-Fi driver data, PHY calibration and OBK flash vars are reset).");

            if (!WriteFlashInSession((uint)nvs.Offset, image.GetImage(), out bool verified))
                return false;
            if (!verified)
            {
                if (isStub)
                    return false;   // MD5 was checked and failed (error already logged)
                addWarningLine("ROM mode: OBK config write was not MD5-verified.");
            }
            return true;
        }

        /// <summary>
        /// Locates the NVS partition from the partition table (in firmwareAt0 when it covers
        /// 0x8000, otherwise read from the chip). Returns null on failure (error logged).
        /// </summary>
        EspPartitionEntry FindNvsPartitionInSession(byte[] firmwareAt0)
        {
            int tableOffset = OBKEspNvsConfig.DefaultPartitionTableOffset;
            if (firmwareAt0 != null && firmwareAt0.Length >= tableOffset + OBKEspNvsConfig.PartitionTableSize)
            {
                EspPartitionLookupResult fromImage = OBKEspNvsConfig.FindNvsPartition(firmwareAt0, tableOffset);
                if (fromImage.Success)
                {
                    addLogLine($"NVS partition from firmware image: '{fromImage.Partition.Label}' at 0x{fromImage.Partition.Offset:X}, size 0x{fromImage.Partition.Size:X}.");
                    return fromImage.Partition;
                }
                addWarningLine("Firmware image has no usable partition table (" + fromImage.Error + "), reading it from chip.");
            }

            addLogLine($"Reading partition table at 0x{tableOffset:X}...");
            byte[] table = ReadFlashInSession((uint)tableOffset, (uint)OBKEspNvsConfig.PartitionTableSize);
            if (table == null)
            {
                addErrorLine("Failed to read partition table.");
                return null;
            }
            EspPartitionLookupResult fromChip = OBKEspNvsConfig.FindNvsPartition(table, 0);
            if (!fromChip.Success)
            {
                addErrorLine("Partition table: " + fromChip.Error + " Is OpenBeken (ESP-IDF build) flashed?");
                return null;
            }
            addLogLine($"NVS partition: '{fromChip.Partition.Label}' at 0x{fromChip.Partition.Offset:X}, size 0x{fromChip.Partition.Size:X}.");
            return fromChip.Partition;
        }

        /// <summary>Same stub/SPI/baud sequence as doRead()/doWrite().</summary>
        void StartStubForSession()
        {
            if (LegacyMode)
                return;
            if (UploadStub())
            {
                addLogLine("Re-attaching SPI after stub...");
                SpiAttach();
                if (baudrate > 115200)
                {
                    addLogLine($"Changing baud rate to {baudrate}...");
                    ChangeBaudrate(baudrate);
                    addLogLine($"Baud rate changed to {baudrate}.");
                }
            }
        }

        /// <summary>Reads flash in the current session: stub fast read, or ROM slow read. Null on failure.</summary>
        byte[] ReadFlashInSession(uint addr, uint size)
        {
            if (isStub)
            {
                try
                {
                    return ReadFlashFast(addr, size);
                }
                catch (Exception ex)
                {
                    addErrorLine($"Flash read at 0x{addr:X} failed: " + ex.Message);
                    return null;
                }
            }
            return ReadFlashBlockSlow(addr, size);
        }
    }
}
