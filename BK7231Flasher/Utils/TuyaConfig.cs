using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BK7231Flasher.MiscUtils;

namespace BK7231Flasher
{
    public class TuyaConfig
    {
        // thanks to kmnh & Kuba bk7231 tools for figuring out this format
        static readonly string KEY_MASTER = "qwertyuiopasdfgh";
        static readonly int SECTOR_SIZE = 4096;
        static readonly uint MAGIC_FIRST_BLOCK = 0x13579753;
        static readonly uint MAGIC_NEXT_BLOCK = 0x98761234;
        static readonly uint MAGIC_FIRST_BLOCK_OS3 = 0x135726AB;
        // 8721D for RTL8720D devices, 8711AM_4M for WRG1. Not known for W800, ECR6600, RTL8720CM, BK7252...
        static readonly byte[] KEY_PART_1 = Encoding.ASCII.GetBytes("8710_2M");
        static readonly byte[] KEY_PART_2 = Encoding.ASCII.GetBytes("HHRRQbyemofrtytf");
        static readonly byte[] KEY_NULL = DeriveVaultKey(KEY_PART_2, KEY_PART_2);
        static readonly byte[] KEY_PART_1_D = Encoding.ASCII.GetBytes("8721D");
        static readonly byte[] KEY_PART_1_AM = Encoding.ASCII.GetBytes("8711AM_4M");
        //static byte[] MAGIC_CONFIG_START = new byte[] { 0x46, 0xDC, 0xED, 0x0E, 0x67, 0x2F, 0x3B, 0x70, 0xAE, 0x12, 0x76, 0xA3, 0xF8, 0x71, 0x2E, 0x03 };
        // TODO: check more bins with this offset
        // hex 0x1EE000
        // While flash is 0x200000, so we have at most 0x12000 bytes there...
        // So it's 0x12 sectors (18 sectors)
        const int USUAL_BK7231_MAGIC_POSITION = 2023424;
        const int USUAL_BK_NEW_XR806_MAGIC_POSITION = 2052096;
        const int USUAL_RTLB_XR809_MAGIC_POSITION = 2011136;
        const int USUAL_RTLC_ECR6600_MAGIC_POSITION = 1921024;
        const int USUAL_RTLD_MAGIC_POSITION = 3891200;
        const int USUAL_WBRG1_MAGIC_POSITION = 8220672;
        const int USUAL_T3_MAGIC_POSITION = 3997696;
        const int USUAL_T5_MAGIC_POSITION = 8245248;
        const int USUAL_LN882H_MAGIC_POSITION = 2015232;
        const int USUAL_TR6260_MAGIC_POSITION = 860160;
        const int USUAL_W800_MAGIC_POSITION = 1835008;
        const int USUAL_LN8825_MAGIC_POSITION = 1994752;
        const int USUAL_RTLCM_MAGIC_POSITION = 3633152;
        const int USUAL_BK7252_MAGIC_POSITION = 3764224;
        const int USUAL_ESP8266_MAGIC_POSITION = 503808;

        const int KVHeaderSize = 0x12;

        int magicPosition = -1;
        byte[] descryptedRaw;
        // Always holds the decrypted config blob produced by TryVaultExtract() (vault) or alternate extractors (e.g. ESP8266 PSM).
        // Enhanced extraction must only operate on this buffer (never on the full original flash dump).
        byte[] vaultDecryptedRaw;
        byte[] original;
        Dictionary<string, string> parms = new Dictionary<string, string>();

        // Enhanced pin translation should be derived from the enhanced KV view (which can include
        // checksum-bad but still useful pages). This cache is presentation-only and does not
        // modify the original 'parms' dictionary used by the classic extraction.
        Dictionary<string, string> _cachedEnhancedParms;
        byte[] _cachedEnhancedParmsSource;

        // Caches for presentation-only enhanced extraction (no effect on extraction logic).
        List<KvEntry> _cachedVaultEntries;
        byte[] _cachedVaultSource;
        string _cachedEnhancedText;
        byte[] _cachedEnhancedTextSource;

        List<KvEntry> _cachedDedupedEntries;
        byte[] _cachedDedupedSource;

        // Enhanced-extraction-only cache: per-key best entry without cross-key value dedupe.
        // This preserves legitimate distinct keys that may share identical values.
        List<KvEntry> _cachedEnhancedEntries;
        byte[] _cachedEnhancedEntriesSource;


        class VaultPage
        {
            public int FlashOffset;
            public uint Seq;
            public byte[] Data;
        }

        public sealed class KvEntry
        {
            public uint ValueLength;
            public ushort KeyId;
            public ushort ChecksumStored;
            public ushort ChecksumCalculated;
            public bool IsCheckSumCorrect => ChecksumStored == ChecksumCalculated;

            public string Key = "";
            public byte[] Value = Array.Empty<byte>();
        
            public override string ToString()
                => $"{Key} (len={ValueLength}, valid={IsCheckSumCorrect})";
        }

        List<KvEntry> GetVaultEntriesCached()
        {
            // Only parse the decrypted KV-pages buffer; never parse the full original dump.
            if (vaultDecryptedRaw == null)
                return new List<KvEntry>();

            if (_cachedVaultEntries != null && object.ReferenceEquals(_cachedVaultSource, vaultDecryptedRaw))
                return _cachedVaultEntries;

            _cachedVaultEntries = ParseVaultPreferred();
            _cachedVaultSource = vaultDecryptedRaw;

            // Any change in parsed KV invalidates the rendered enhanced view cache.
            _cachedDedupedEntries = null;
            _cachedDedupedSource = null;

            _cachedEnhancedEntries = null;
            _cachedEnhancedEntriesSource = null;

            _cachedEnhancedText = null;
            _cachedEnhancedTextSource = null;

            return _cachedVaultEntries;
        }


List<KvEntry> GetVaultEntriesDedupedCached()
{
    if (vaultDecryptedRaw == null)
        return new List<KvEntry>();

    if (_cachedDedupedEntries != null && object.ReferenceEquals(_cachedDedupedSource, vaultDecryptedRaw))
        return _cachedDedupedEntries;

    var KVs = GetVaultEntriesCached();

    // Keep logic identical to the original extraction dedupe:
    // 1) Prefer checksum-correct entries per key
    // 2) Then dedupe identical values (by bytes) and pick highest KeyId
    var KVs_Deduped = KVs
        .GroupBy(x => x.Key)
        .Select(g => g.OrderByDescending(x => x.IsCheckSumCorrect).First())
        .GroupBy(x => Convert.ToBase64String(x.Value ?? Array.Empty<byte>()))
        .Select(g => g.OrderByDescending(x => x.KeyId).First())
        .ToList();

    _cachedDedupedEntries = KVs_Deduped;
    _cachedDedupedSource = vaultDecryptedRaw;

    // Derived caches: invalidate the rendered enhanced view cache.
    _cachedEnhancedText = null;
    _cachedEnhancedTextSource = null;

    return _cachedDedupedEntries;
}


        // Enhanced extraction should not drop legitimate distinct keys that share identical values.
        // Unlike GetVaultEntriesDedupedCached(), this applies only per-key selection (prefer checksum-correct)
        // and preserves the first-seen key order from the parsed vault.
        List<KvEntry> GetVaultEntriesEnhancedCached()
        {
            if (vaultDecryptedRaw == null)
                return new List<KvEntry>();

            if (_cachedEnhancedEntries != null && object.ReferenceEquals(_cachedEnhancedEntriesSource, vaultDecryptedRaw))
                return _cachedEnhancedEntries;

            var kvs = GetVaultEntriesCached();

            var bestByKey = new Dictionary<string, KvEntry>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (var kv in kvs)
            {
                if (kv == null || string.IsNullOrEmpty(kv.Key))
                    continue;

                if (!bestByKey.TryGetValue(kv.Key, out var cur))
                {
                    bestByKey[kv.Key] = kv;
                    order.Add(kv.Key);
                }
                else if (IsBetterEnhancedEntry(kv, cur))
                {
                    bestByKey[kv.Key] = kv;
                }
            }

            var list = new List<KvEntry>(order.Count);
            foreach (var k in order)
            {
                if (bestByKey.TryGetValue(k, out var e))
                    list.Add(e);
            }

            _cachedEnhancedEntries = list;
            _cachedEnhancedEntriesSource = vaultDecryptedRaw;

            _cachedEnhancedText = null;
            _cachedEnhancedTextSource = null;

            return _cachedEnhancedEntries;
        }

        static bool IsBetterEnhancedEntry(KvEntry candidate, KvEntry current)
        {
            if (candidate == null)
                return false;
            if (current == null)
                return true;

            if (candidate.IsCheckSumCorrect != current.IsCheckSumCorrect)
                return candidate.IsCheckSumCorrect;

            if (candidate.KeyId != current.KeyId)
                return candidate.KeyId > current.KeyId;

            if (candidate.ValueLength != current.ValueLength)
                return candidate.ValueLength > current.ValueLength;

            return false;
        }



        static ushort CalcChecksum(byte[] buf, int off, int len)
        {
            ushort sum = 0;
            for(int i = off; i < off + len; i++) sum += buf[i];
            return sum;
        }

        static bool TryParseEntry(byte[] pageData, int entryOffset, out KvEntry entry)
        {
            entry = null!;

            if(entryOffset + KVHeaderSize > pageData.Length)
                return false;

            uint valueLen = ReadU32LE(pageData, entryOffset + 4);

            if(valueLen == 0 || valueLen > pageData.Length)
                return false;

            int totalLength = KVHeaderSize + (int)valueLen;
            if(entryOffset + totalLength > pageData.Length)
                return false;

            var storedChsum = ReadU16LE(pageData, entryOffset);
            var keyId = ReadU16LE(pageData, entryOffset + 8);
            var keyLen = pageData[entryOffset + 0x11];
            var keyOff = 18;
            var valOff = 128;

            int keyPos = entryOffset + keyOff;
            if(keyPos < 0 || keyPos >= pageData.Length)
                return false;

            var keyBytes = new List<byte>();
            for(int i = keyPos; i < keyPos + keyLen; i++)
            {
                byte b = pageData[i];
                if(!(b == 0 || (b >= 0x20 && b <= 0x7E)))
                    return false;
                if(b == 0)
                    break;
                keyBytes.Add(b);
            }

            if(keyBytes.Count == 0)
                return false;

            string key = Encoding.ASCII.GetString(keyBytes.ToArray());

            int valPos = entryOffset + valOff;
            if(valPos < 0 || valPos + valueLen > pageData.Length)
                return false;

            ushort calcChsum = CalcChecksum(pageData, valPos, (int)valueLen);
            //if(storedChsum != calcChsum)
            //	return false;

            byte[] value = new byte[valueLen];
            Buffer.BlockCopy(pageData, valPos, value, 0, (int)valueLen);

            entry = new KvEntry
            {
                ValueLength = valueLen,
                KeyId = keyId,
                Key = key,
                Value = value,
                ChecksumStored = storedChsum,
                ChecksumCalculated = calcChsum
            };

            return true;
        }

        List<KvEntry> ParseVault()
        {
            var entries = new List<KvEntry>();
            byte[] data = vaultDecryptedRaw;
            if (data == null || data.Length == 0)
                return entries;

            for(int off = 0; off + KVHeaderSize < data.Length; off += 0x80)
            {
                if(TryParseEntry(data, off, out var entry))
                {
                    entries.Add(entry);

                    int nextOffset = off + 0x80;
                    if(nextOffset > off)
                        off = nextOffset - 0x80;
                }
            }
            return entries;
        }

            
        static bool LooksLikePsmBlob(byte[] buf)
        {
            if (buf == null || buf.Length < 4)
                return false;

            // Some images may have padding before the header.
            int i = 0;
            while (i < buf.Length && (buf[i] == 0x00 || buf[i] == 0xFF))
                i++;

            if (i + 3 >= buf.Length)
                return false;

            return buf[i] == (byte)'P' && buf[i + 1] == (byte)'S' && buf[i + 2] == (byte)'M' && buf[i + 3] == (byte)'1';
        }

        static bool LooksLikePsmKeyValueTable(byte[] buf)
        {
            if (buf == null || buf.Length < 128)
                return false;

            // Heuristic: scan a capped prefix and look for multiple printable "key=value" segments
            // separated by NUL/0xFF padding. This reduces false positives when we fail the normal vault extractor.
            int max = Math.Min(buf.Length, SECTOR_SIZE * 32); // cap scan to 128KB
            int i = 0;
            int pairs = 0;
            bool hasKnown = false;

            while (i < max)
            {
                while (i < max && (buf[i] == 0x00 || buf[i] == 0xFF))
                    i++;

                int start = i;

                while (i < max && buf[i] != 0x00 && buf[i] != 0xFF)
                    i++;

                int end = i;
                int len = end - start;
                if (len < 6)
                    continue;

                // Must be mostly printable ASCII (plus whitespace).
                int printable = 0;
                for (int k = start; k < end; k++)
                {
                    byte b = buf[k];
                    if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b <= 0x7E))
                        printable++;
                }

                if (printable < (len * 9) / 10)
                    continue;

                int eq = -1;
                for (int k = start; k < end; k++)
                {
                    if (buf[k] == (byte)'=')
                    {
                        eq = k;
                        break;
                    }
                }

                if (eq <= start || eq >= end - 1)
                    continue;

                int keyLen = eq - start;
                if (keyLen <= 0 || keyLen > 96)
                    continue;

                pairs++;

                if (!hasKnown)
                {
                    int klen = Math.Min(keyLen, 96);
                    string key = Encoding.ASCII.GetString(buf, start, klen);

                    if (key.IndexOf("local_key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("ssid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("passwd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("ty_ws_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("gw_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("device_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("ESP.", StringComparison.Ordinal) >= 0)
                    {
                        hasKnown = true;
                    }
                }

                // Quick accept thresholds.
                if ((pairs >= 3 && hasKnown) || pairs >= 10)
                    return true;
            }

            return false;
        }


        static string NormalizePsmKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            key = key.Trim();

            // PSM partitions often contain stray leading bytes before the key name.
            int esp = key.IndexOf("ESP.", StringComparison.Ordinal);
            if (esp > 0)
                key = key.Substring(esp);

            // Strip leading junk (keep letters, digits, underscore).
            int j = 0;
            while (j < key.Length && !char.IsLetterOrDigit(key[j]) && key[j] != '_')
                j++;

            if (j > 0 && j < key.Length)
                key = key.Substring(j);

            return key.Trim();
        }

        List<KvEntry> ParseVaultPsm()
        {
            var result = new List<KvEntry>();
            if (vaultDecryptedRaw == null || vaultDecryptedRaw.Length == 0)
                return result;

            // PSM is a NUL-separated key=value string table.
            // We surface each key as a KV entry so enhanced extraction can render all JSON blocks.
            int i = 0;
            while (i < vaultDecryptedRaw.Length)
            {
                // Skip padding and NULs.
                while (i < vaultDecryptedRaw.Length && (vaultDecryptedRaw[i] == 0x00 || vaultDecryptedRaw[i] == 0xFF))
                    i++;

                int start = i;

                while (i < vaultDecryptedRaw.Length && vaultDecryptedRaw[i] != 0x00 && vaultDecryptedRaw[i] != 0xFF)
                    i++;

                int end = i;
                int len = end - start;
                if (len < 4)
                    continue;

                // Ensure the segment is mostly printable ASCII (plus common whitespace).
                int printable = 0;
                for (int k = start; k < end; k++)
                {
                    byte b = vaultDecryptedRaw[k];
                    if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b <= 0x7E))
                        printable++;
                }

                if (printable < (len * 9) / 10)
                    continue;

                string s = Encoding.ASCII.GetString(vaultDecryptedRaw, start, len).Trim();
                if (s.Length < 4)
                    continue;

                int eq = s.IndexOf('=');
                if (eq <= 0 || eq >= s.Length - 1)
                    continue;

                string key = NormalizePsmKey(s.Substring(0, eq));
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                // Ignore the PSM header token itself if it appears in the table.
                if (string.Equals(key, "PSM1", StringComparison.Ordinal))
                    continue;

                string val = s.Substring(eq + 1).Trim();
                byte[] valBytes = Encoding.UTF8.GetBytes(val);

                result.Add(new KvEntry
                {
                    Key = key,
                    Value = valBytes,
                    ValueLength = (uint)valBytes.Length,
                    KeyId = 0,
                    ChecksumStored = 0,
                    ChecksumCalculated = 0
                });
            }

            return result;
        }

        List<KvEntry> ParseVaultPreferred()
        {
            if (LooksLikePsmBlob(vaultDecryptedRaw))
                return ParseVaultPsm();

            // Prefer KVStorage-style indexed parsing when it clearly applies.
            // Fall back to the classic fixed-stride parser otherwise.
            try
            {
                var kvsIndexed = ParseVaultKvStorage();
                if (LooksLikeKvStorageResult(kvsIndexed))
                    return kvsIndexed;
            }
            catch
            {
                // Best-effort only.
            }
            return ParseVault();
        }

        static bool LooksLikeKvStorageResult(List<KvEntry> entries)
        {
            if (entries == null || entries.Count < 3)
                return false;

            // Require at least one canonical gw_* key (or user_param_key) to avoid false positives.
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Key))
                    continue;

                if (e.Key.StartsWith("gw_", StringComparison.Ordinal) ||
                    string.Equals(e.Key, "user_param_key", StringComparison.Ordinal) ||
                    string.Equals(e.Key, "tuya_seed", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        sealed class KvStorageIndex
        {
            public string Name;
            public int Length;
            public ushort BlockId;
            public byte PageId;
            public uint Element;
            public List<KvStoragePart> Parts = new List<KvStoragePart>();
        }

        sealed class KvStoragePart
        {
            public ushort BlockId;
            public byte PageStart;
            public byte PageEnd;
        }

        List<KvEntry> ParseVaultKvStorage()
        {
            var result = new List<KvEntry>();
            byte[] data = vaultDecryptedRaw;
            if (data == null || data.Length < SECTOR_SIZE)
                return result;

            // Build an index of blocks by block_id.
            var blocksById = new Dictionary<ushort, int>();
            int blockCount = data.Length / SECTOR_SIZE;

            for (int b = 0; b < blockCount; b++)
            {
                int blockStart = b * SECTOR_SIZE;
                uint magic = ReadU32LE(data, blockStart);
                if (magic != MAGIC_NEXT_BLOCK && magic != MAGIC_FIRST_BLOCK_OS3 && magic != MAGIC_FIRST_BLOCK)
                    continue;

                ushort blockId = ReadU16LE(data, blockStart + 8);
                if (!blocksById.ContainsKey(blockId))
                    blocksById[blockId] = blockStart;
            }

            if (blocksById.Count == 0)
                return result;

            // Collect all index pages.
            var indexes = new List<KvStorageIndex>();

            foreach (var kv in blocksById)
            {
                int blockStart = kv.Value;

                byte mapSize = data[blockStart + 14];
                int mapOff = blockStart + 15;
                if (mapSize == 0 || mapOff + mapSize > blockStart + 128)
                    continue;

                // map_data bytes
                for (int pi = 0; pi < 31; pi++)
                {
                    int pageId = pi + 1;
                    if (!IsIndexPage(data, mapOff, mapSize, pageId))
                        continue;

                    int pageStart = blockStart + (pageId * 128);
                    if (pageStart + 128 > data.Length)
                        continue;

                    if (TryParseKvStorageIndexPage(data, blockStart, ReadU16LE(data, blockStart + 8), (byte)pageId, pageStart, out var idx))
                    {
                        indexes.Add(idx);
                    }
                }
            }

            if (indexes.Count == 0)
                return result;

            // Deduplicate by key name: prefer highest 'element' (newest), then longest length.
            var chosen = indexes
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name) && x.Length >= 0)
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .Select(g => g
                    .OrderByDescending(x => x.Element)
                    .ThenByDescending(x => x.Length)
                    .First())
                .ToList();

            foreach (var idx in chosen)
            {
                var value = ReassembleKvStorageValue(data, blocksById, idx);
                if (value == null)
                    continue;
                if (value.Length == 0 && idx.Length > 0)
                    continue;

                result.Add(new KvEntry
                {
                    Key = idx.Name,
                    ValueLength = (uint)value.Length,
                    KeyId = (ushort)(idx.Element & 0xFFFF),
                    Value = value,
                    ChecksumStored = 0,
                    ChecksumCalculated = 0
                });
            }

            return result;
        }

        static bool IsIndexPage(byte[] data, int mapOff, int mapSize, int pageId)
        {
            // pageId is 1..31. The map uses bits indexed by pageId.
            int byteIndex = pageId / 8;
            int bitIndex = pageId % 8;

            if (byteIndex < 0 || byteIndex >= mapSize)
                return false;

            byte m = data[mapOff + byteIndex];
            return (m & (1 << bitIndex)) != 0;
        }

        static bool TryParseKvStorageIndexPage(byte[] data, int blockStart, ushort blockId, byte pageId, int pageStart, out KvStorageIndex idx)
        {
            idx = null;

            try
            {
                // Layout mirrors the Tuya KVStorage DataBlock.IndexPage layout, but some firmwares
                // encode parts_size as either count-of-parts or byte-size. We accept both.
                uint crc = ReadU32LE(data, pageStart + 0);
                int length = (int)ReadU32LE(data, pageStart + 4);
                ushort idxBlockId = ReadU16LE(data, pageStart + 8);
                byte idxPageId = data[pageStart + 10];
                ushort partsField = ReadU16LE(data, pageStart + 11);
                uint element = ReadU32LE(data, pageStart + 13);
                byte nameLen = data[pageStart + 17];

                if (idxBlockId != blockId)
                    return false;
                if (idxPageId != pageId)
                    return false;
                if (nameLen == 0 || nameLen > 110) // defensive
                    return false;

                int nameOff = pageStart + 18;
                int nameEnd = nameOff + nameLen;
                if (nameEnd > pageStart + 128)
                    return false;

                // nameLen appears to include a NUL terminator in many firmwares
                int realNameLen = nameLen;
                if (realNameLen > 0 && data[nameOff + realNameLen - 1] == 0)
                    realNameLen -= 1;

                if (realNameLen <= 0)
                    return false;

                // ASCII-ish key names only; avoid fragment garbage as keys in KVStorage mode.
                for (int i = 0; i < realNameLen; i++)
                {
                    byte b = data[nameOff + i];
                    if (!(b == 0 || (b >= 0x20 && b <= 0x7E)))
                        return false;
                }

                string name = Encoding.ASCII.GetString(data, nameOff, realNameLen);

                int partsOff = nameOff + nameLen;
                int remaining = (pageStart + 128) - partsOff;
                // partsField==0 is valid for empty values on some firmwares (e.g. em_sys_env = "").
                // In that case there may be no parts array at all.
                if (partsField == 0)
                {
                    idx = new KvStorageIndex
                    {
                        Name = name,
                        Length = length,
                        BlockId = idxBlockId,
                        PageId = idxPageId,
                        Element = element,
                        Parts = new List<KvStoragePart>(0)
                    };
                    return true;
                }

                if (remaining < 4)
                    return false;

                int partsCount = 0;
                // Interpret partsField as count if it fits, else as bytes (multiple of 4).
                if (partsField > 0 && (partsField * 4) <= remaining)
                    partsCount = partsField;
                else if (partsField > 0 && (partsField % 4) == 0 && partsField <= remaining)
                    partsCount = partsField / 4;
                else
                    return false;

                if (partsCount <= 0 || partsCount > 64)
                    return false;

                var parts = new List<KvStoragePart>(partsCount);
                for (int i = 0; i < partsCount; i++)
                {
                    int po = partsOff + (i * 4);
                    ushort pBlock = ReadU16LE(data, po);
                    byte pStart = data[po + 2];
                    byte pEnd = data[po + 3];
                    if (pStart == 0 || pEnd == 0 || pStart > pEnd || pEnd > 31)
                        return false;

                    parts.Add(new KvStoragePart { BlockId = pBlock, PageStart = pStart, PageEnd = pEnd });
                }

                idx = new KvStorageIndex
                {
                    Name = name,
                    Length = length,
                    BlockId = idxBlockId,
                    PageId = idxPageId,
                    Element = element,
                    Parts = parts
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        static byte[] ReassembleKvStorageValue(byte[] data, Dictionary<ushort, int> blocksById, KvStorageIndex idx)
        {
            if (idx == null || idx.Parts == null || idx.Parts.Count == 0 || idx.Length <= 0)
                return Array.Empty<byte>();

            try
            {
                using var ms = new MemoryStream();

                foreach (var part in idx.Parts)
                {
                    if (!blocksById.TryGetValue(part.BlockId, out int blockStart))
                        return Array.Empty<byte>();

                    for (int pid = part.PageStart; pid <= part.PageEnd; pid++)
                    {
                        int pageStart = blockStart + (pid * 128);
                        if (pageStart + 128 > data.Length)
                            return Array.Empty<byte>();

                        ms.Write(data, pageStart, 128);
                    }
                }

                var buf = ms.ToArray();
                if (buf.Length > idx.Length)
                {
                    Array.Resize(ref buf, idx.Length);
                }
                else if (buf.Length < idx.Length)
                {
                    // Some firmwares may declare a longer length; be defensive and keep what we have.
                }
                return buf;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }


        // pretty useless for RTLs, since littlefs overwrites it.
        internal static int getMagicOffset(BKType type) => type switch
            {
                BKType.RTL8710B => USUAL_RTLB_XR809_MAGIC_POSITION,
                BKType.RTL87X0C => USUAL_RTLC_ECR6600_MAGIC_POSITION,
                BKType.RTL8720D => USUAL_RTLD_MAGIC_POSITION,
                BKType.LN882H   => USUAL_LN882H_MAGIC_POSITION,
                BKType.BK7236   => USUAL_T3_MAGIC_POSITION,
                BKType.BK7238   => USUAL_BK_NEW_XR806_MAGIC_POSITION,
                BKType.BK7258   => USUAL_T5_MAGIC_POSITION,
                BKType.ECR6600  => USUAL_RTLC_ECR6600_MAGIC_POSITION,
                BKType.LN8825   => USUAL_LN8825_MAGIC_POSITION,
                BKType.TR6260   => USUAL_TR6260_MAGIC_POSITION,
                _               => USUAL_BK7231_MAGIC_POSITION,
            };

        public static int getMagicSize(BKType type) => type switch
        {
            BKType.RTL8710B => 0x200000 - USUAL_RTLB_XR809_MAGIC_POSITION,
            BKType.RTL87X0C => 0x1E5000 - USUAL_RTLC_ECR6600_MAGIC_POSITION,
            BKType.RTL8720D => 0x3FC000 - USUAL_RTLD_MAGIC_POSITION,
            BKType.LN882H   => 0x200000 - USUAL_LN882H_MAGIC_POSITION,
            BKType.BK7236   => 0x3E0000 - USUAL_T3_MAGIC_POSITION,
            BKType.BK7238   => 0x200000 - USUAL_BK_NEW_XR806_MAGIC_POSITION,
            BKType.BK7258   => 0x7ED000 - USUAL_T5_MAGIC_POSITION,
            BKType.ECR6600  => 0x1F7000 - USUAL_RTLC_ECR6600_MAGIC_POSITION,
            BKType.LN8825   => 0x200000 - USUAL_LN8825_MAGIC_POSITION,
            BKType.TR6260   => 0x0DE000 - USUAL_TR6260_MAGIC_POSITION,
            _               => 0x200000 - USUAL_BK7231_MAGIC_POSITION,
        };

        public string getMagicPositionHex() => $"0x{magicPosition:X}";

        public string getMagicPositionDecAndHex() => $"{magicPosition} ({getMagicPositionHex()})";

        public bool fromFile(string fname)
        {
            using var fs = new FileStream(fname, FileMode.Open, FileAccess.Read);
            var buffer = new byte[fs.Length];
            fs.Read(buffer, 0, buffer.Length);
            return fromBytes(buffer);
        }

        bool bLastBinaryOBKConfig;
        bool bGivenBinaryIsFullOf0xff;
        bool bVaultMagicHeaderNotFound;
        // warn users that they have erased flash sector with cfg
        public bool isLastBinaryFullOf0xff()
        {
            return bGivenBinaryIsFullOf0xff;
        }
        public bool isLastBinaryOBKConfig()
        {
            return bLastBinaryOBKConfig;
        }

        public bool fromBytes(byte[] data)
        {
            descryptedRaw = null;
            vaultDecryptedRaw = null;
            bVaultMagicHeaderNotFound = false;
            original = data;
            if (isFullOf(data, 0xff))
            {
                FormMain.Singleton?.addLog("It seems that dragged binary is full of 0xff, someone must have erased the flash" + Environment.NewLine, System.Drawing.Color.Purple);
                bGivenBinaryIsFullOf0xff = true;
                return true;
            }
            if(data.Length>3 && data[0] == (byte)'C' && data[1] == (byte)'F' && data[2] == (byte)'G')
            {
                FormMain.Singleton?.addLog("It seems that dragged binary is OBK config, not a Tuya one" + Environment.NewLine, System.Drawing.Color.Purple);
                bLastBinaryOBKConfig = true;
                return true;
            }

            try
            {
                if(TryVaultExtract(data)) return false;
                if(TryExtractPSM(data)) return false;
                if(bVaultMagicHeaderNotFound)
                    FormMain.Singleton?.addLog("Failed to extract Tuya keys - magic constant header not found in binary" + Environment.NewLine, System.Drawing.Color.Purple);
            }
            finally
            {
                if(descryptedRaw != null)
                {
                    string debugName = "lastRawDecryptedStrings.bin";
                    FormMain.Singleton?.addLog("Saving debug Tuya decryption data to " + debugName + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

                    try
                    {
                        File.WriteAllBytes(debugName, descryptedRaw);
                    }
                    catch(Exception ex)
                    {
                        try
                        {
                            FormMain.Singleton?.addLog("WARNING - failed to write " + debugName + ": " + ex.Message + Environment.NewLine, System.Drawing.Color.Purple);
                        }
                        catch { }
                    }
                }
            }

            return true;
        }

        bool TryVaultExtract(byte[] flash)
        {
            descryptedRaw = null;
            vaultDecryptedRaw = null;

            var deviceKeys = FindDeviceKeys(flash);
            if(deviceKeys.Count == 0)
            {
                bVaultMagicHeaderNotFound = true;
                return false;
            }

            var baseKeyCandidates = new byte[][]
            {
                KEY_PART_1,
                KEY_NULL,
                KEY_PART_1_D,
                KEY_PART_2,
                KEY_PART_1_AM,
            };

            var pageMagics = new uint[] { MAGIC_NEXT_BLOCK, MAGIC_FIRST_BLOCK_OS3, MAGIC_FIRST_BLOCK };

            List<VaultPage> bestPages = null;
            int bestCount = 0;
            var obj = new object();
            var time = Stopwatch.StartNew();
            var crcBadOffsets = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
            int crcBadCount = 0;
            foreach(var devKey in deviceKeys)
            {
                Parallel.ForEach(baseKeyCandidates, baseKey =>
                {
                    using var aes = Aes.Create();
                    aes.Mode = CipherMode.ECB;
                    aes.Padding = PaddingMode.None;
                    aes.KeySize = 128;
                    aes.Key = DeriveVaultKey(devKey, baseKey);
                    using var decryptor = aes.CreateDecryptor();
                    var blockBuffer = new byte[SECTOR_SIZE];
                    var firstBlock = new byte[16];
                    foreach(var magic in pageMagics)
                    {
                        List<VaultPage> pages = new List<VaultPage>();

                        for(int ofs = 0; ofs + SECTOR_SIZE <= flash.Length; ofs += SECTOR_SIZE)
                        {
                            decryptor.TransformBlock(flash, ofs, 16, firstBlock, 0);

                            var pageMagic = ReadU32LE(firstBlock, 0);
                            if(pageMagic != magic) continue;

                            var dec = AESDecrypt(flash, ofs, decryptor, blockBuffer);

                            if(dec == null) continue;

                            var crc = ReadU32LE(dec, 4);
                            if(!checkCRC(crc, dec, 8, dec.Length - 8))
                            {
                                System.Threading.Interlocked.Increment(ref crcBadCount);
                                // Don't log from worker threads. Buffer a bounded set of unique offsets and emit after the parallel scan.
                                if(crcBadOffsets.Count < 200) crcBadOffsets.TryAdd(ofs, 0);
                                continue;
                            }

                            var seq = ReadU32LE(dec, 8);

                            pages.Add(new VaultPage
                            {
                                FlashOffset = ofs,
                                Seq = seq,
                                Data = dec
                            });
                        }
                        lock(obj)
                        {
                            if(pages.Count > bestCount)
                            {
                                bestCount = pages.Count;
                                bestPages = pages;
                            }
                        }
                    }
                });
            }
            time.Stop();

            // Emit buffered CRC warnings (avoid UI/threading issues from logging inside Parallel.ForEach)
            if(crcBadCount > 0)
            {
                try
                {
                    var sample = crcBadOffsets.Keys.OrderBy(x => x).Take(50).ToArray();
                    var msg = $"WARNING - {crcBadCount} bad block CRC(s) encountered during Tuya vault scan.";
                    if(sample.Length > 0)
                        msg += " Sample offsets: " + string.Join(", ", sample.Select(o => "0x" + o.ToString("X")));
                    if(crcBadOffsets.Count > sample.Length)
                        msg += $" (+{crcBadOffsets.Count - sample.Length} more unique offset(s) recorded)";
                    FormMain.Singleton?.addLog(msg + Environment.NewLine, System.Drawing.Color.Purple);
                }
                catch { }
            }

            if(bestPages == null)
            {
                FormMain.Singleton?.addLog("Failed to extract Tuya keys - decryption failed" + Environment.NewLine, System.Drawing.Color.Orange);
                return false;
            }
            FormMain.Singleton?.addLog($"Decryption took {time.ElapsedMilliseconds} ms" + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            var dataFlashOffset = bestPages.Min(x => x.FlashOffset);
            magicPosition = magicPosition < dataFlashOffset ? magicPosition : dataFlashOffset;
            FormMain.Singleton?.addLog($"Tuya config extractor - magic is at {magicPosition} (0x{magicPosition:X}) " + Environment.NewLine, System.Drawing.Color.DarkSlateGray);

            if(bestPages.Count < 2)
            {
                FormMain.Singleton?.addLog("Failed to extract Tuya keys - config not found" + Environment.NewLine, System.Drawing.Color.Orange);
                return false;
            }

            //bestPages.Sort((a, b) => a.Seq.CompareTo(b.Seq));
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            foreach(var p in bestPages) bw.Write(p.Data, 0, p.Data.Length);

            descryptedRaw = ms.ToArray();
            vaultDecryptedRaw = descryptedRaw;
            return true;
        }

        static byte[] DeriveVaultKey(byte[] baseKey, byte[] deviceKey)
        {
            if(baseKey.Length != 16)
                throw new Exception($"baseKey.Length != 16 ({baseKey.Length})");
            var vaultKey = new byte[16];
            if(deviceKey.Length != 16)
            {
                for (int i = 0; i < 16; i++)
                {
                    int v = deviceKey[i & 3] + KEY_PART_2[i];
                    vaultKey[i] = (byte)((v + baseKey[i]) & 0xFF);
                }
                return vaultKey;
            }
            for(int i = 0; i < 16; i++) vaultKey[i] = (byte)((baseKey[i] + deviceKey[i]) & 0xFF);
            return vaultKey;
        }

        List<byte[]> FindDeviceKeys(byte[] flash)
        {
            var keys = new List<byte[]>();
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 128;
            aes.Key = Encoding.ASCII.GetBytes(KEY_MASTER);

            using var decryptor = aes.CreateDecryptor();
            var blockBuffer = new byte[SECTOR_SIZE];
            var dec = new byte[16];
            for(int ofs = 0; ofs + SECTOR_SIZE <= flash.Length; ofs += SECTOR_SIZE)
            {
                decryptor.TransformBlock(flash, ofs, 16, dec, 0);

                var pageMagic = ReadU32LE(dec, 0);
                if(pageMagic != MAGIC_FIRST_BLOCK) continue;

                dec = AESDecrypt(flash, ofs, decryptor, blockBuffer);
                if(dec == null) continue;

                var dk = new byte[16];
                Array.Copy(dec, 8, dk, 0, 16);

                var crc = ReadU32LE(dec, 4);
                if(checkCRC(crc, dk, 0, 16))
                {
                    keys.Add(dk);
                    magicPosition = ofs;
                }
                //else
                //{
                //    FormMain.Singleton.addLog("WARNING - bad firstblock crc" + Environment.NewLine, System.Drawing.Color.Purple);
                //}
            }
            return keys;
        }

        byte[] AESDecrypt(byte[] flash, int ofs, ICryptoTransform decryptor, byte[] buffer)
        {
            Array.Copy(flash, ofs, buffer, 0, SECTOR_SIZE);
            return decryptor.TransformFinalBlock(buffer, 0, SECTOR_SIZE);
        }

        bool TryExtractPSM(byte[] vaultData)
        {
            if(TryLocatePsm(vaultData, null, out magicPosition, out descryptedRaw))
            {
                vaultDecryptedRaw = descryptedRaw;
                FormMain.Singleton?.addLog(
                    $"Found plaintext PSM at 0x{magicPosition:X}" + Environment.NewLine,
                    System.Drawing.Color.DarkSlateGray);
                return true;
            }

            if(!TryFindPsmKeyByBruteforce(vaultData, out var psmAesKey))
                return false;

            if(!TryLocatePsm(vaultData, psmAesKey, out magicPosition, out descryptedRaw))
                return false;

            vaultDecryptedRaw = descryptedRaw;

            FormMain.Singleton?.addLog(
                $"Found AES PSM at 0x{magicPosition:X}" + Environment.NewLine,
                System.Drawing.Color.DarkSlateGray);

            return true;
        }

        bool TryLocatePsm(byte[] data, byte[] aesKey, out int foundOffset, out byte[] psmData)
        {
            foundOffset = -1;
            psmData = null;

            using var aes = aesKey != null ? Aes.Create() : null;

            if (aes != null)
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.KeySize = 128;
                aes.Key = aesKey;
            }

            using var dec = aes != null ? aes.CreateDecryptor() : null;

            for (int ofs = 0; ofs + 16 <= data.Length; ofs += SECTOR_SIZE)
            {
                var hdr = new byte[16];
                Array.Copy(data, ofs, hdr, 0, 16);

                if (dec != null)
                    hdr = dec.TransformFinalBlock(hdr, 0, 16);

                // Require strict PSM1 header to avoid false positives.
                if (!(hdr[0] == (byte)'P' && hdr[1] == (byte)'S' && hdr[2] == (byte)'M' && hdr[3] == (byte)'1'))
                    continue;

                var region = ExtractPsmRegion(data, ofs, dec);

                // Validate the extracted region looks like a PSM key=value table.
                if (region == null || region.Length < 128)
                    continue;

                if (!LooksLikePsmBlob(region))
                    continue;

                if (!LooksLikePsmKeyValueTable(region))
                    continue;

                foundOffset = ofs;
                psmData = region;
                return true;
            }

            return false;
        }

        byte[] ExtractPsmRegion(byte[] data, int start, ICryptoTransform dec)
        {
            using var ms = new MemoryStream();
            int misses = 0;

            for(int ofs = start; ofs + SECTOR_SIZE <= data.Length; ofs += SECTOR_SIZE)
            {
                var buf = new byte[SECTOR_SIZE];
                Array.Copy(data, ofs, buf, 0, SECTOR_SIZE);

                if(dec != null)
                    buf = dec.TransformFinalBlock(buf, 0, SECTOR_SIZE);

                if(LooksLikePsm(buf))
                {
                    ms.Write(buf, 0, buf.Length);
                    misses = 0;
                }
                else
                {
                    misses++;
                    if(misses >= 2)
                        break;
                }
            }

            return ms.ToArray();
        }

        bool LooksLikePsm(byte[] buf)
        {
            var needles = new[]
            {
                "ty_ws_", "device_", "gw_", "local_key",
                "ssid", "passwd", "ESP."
            };

            var s = Encoding.ASCII.GetString(buf);
            foreach(var n in needles)
                if (s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return buf[0] == 'P' && buf[1] == 'S' && buf[2] == 'M' && buf[3] == '1';
        }

        bool TryFindPsmKeyByBruteforce(byte[] data, out byte[] key)
        {
            key = null;

            for(int mac4 = 0; mac4 <= 0xFF; mac4++)
            {
                var k = new byte[16];
                Encoding.ASCII.GetBytes("Qby6").CopyTo(k, 0);
                k[4] = (byte)mac4;
                Encoding.ASCII.GetBytes("TH9bj8GiexU").CopyTo(k, 5);

                if(TryLocatePsm(data, k, out _, out _))
                {
                    key = k;
                    return true;
                }
            }

            return false;
        }

        public string getKeysHumanReadable(OBKConfig tg = null)
        {
            return GetKeysHumanReadableInternal(parms, tg);
        }

        // When enhanced extraction is enabled, prefer key/value pairs reconstructed from the enhanced
        // vault entries. This allows the text description to still show pins/modules even when the
        // classic extraction picked a fallback (e.g. baud_cfg) due to a checksum-bad user_param_key.
        public string getKeysHumanReadableEnhanced(OBKConfig tg = null)
        {
            var enhanced = GetEnhancedParmsCached();
            if (enhanced == null || enhanced.Count == 0)
                return GetKeysHumanReadableInternal(parms, tg);

            // If enhanced produced no pin/module-ish keys, keep classic behavior.
            bool enhancedLooksUseful = enhanced.Keys.Any(k => k.EndsWith("_pin", StringComparison.Ordinal) || k.IndexOf("pin", StringComparison.OrdinalIgnoreCase) >= 0 || k.Equals("module", StringComparison.Ordinal));
            if (!enhancedLooksUseful)
                return GetKeysHumanReadableInternal(parms, tg);

            var enhancedDesc = GetKeysHumanReadableInternal(enhanced, tg);

            // Safety: if enhanced still couldn't recover any pins but classic did, fall back.
            if (enhancedDesc != null && enhancedDesc.StartsWith("Sorry, no meaningful pins", StringComparison.Ordinal) &&
                !GetKeysHumanReadableInternal(parms, null).StartsWith("Sorry, no meaningful pins", StringComparison.Ordinal))
            {
                return GetKeysHumanReadableInternal(parms, tg);
            }
            return enhancedDesc;
        }

        string GetKeysHumanReadableInternal(Dictionary<string, string> source, OBKConfig tg = null)
        {
            if (source == null)
                source = new Dictionary<string, string>();

            bool bHasBattery = false;
            string desc = "";
            if(tg != null && !string.IsNullOrWhiteSpace(tg.initCommandLine)) tg.initCommandLine += "\r\n";
            foreach(var kv in source)
            {
                string key = kv.Key;
                string value = kv.Value;
                switch(key)
                {
                    case var k when Regex.IsMatch(k, "^led\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- LED (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.LED);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^netled\\d+_pin$"):
                    case "netled_pin":
                    case "wfst":
                    case "wfst_pin":
                        // some devices have netled1_pin, some have netled_pin
                        //int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- WiFi LED on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "bz_pin_pin"):
                    case "buzzer_io":
                        desc += "- Buzzer Pin (TODO) on P" + value + Environment.NewLine;
                        //tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "status_led_pin"):
                        desc += "- Status LED on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.WifiLED_n);
                        break;
                    case var k when Regex.IsMatch(k, "remote_io"):
                        desc += "- RF Remote on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.RCRecv);
                        break;
                    case var k when Regex.IsMatch(k, "samp_sw_pin"):
                        desc += "- Battery Relay on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BAT_Relay);
                        break;
                    case var k when Regex.IsMatch(k, "samp_pin"):
                        desc += "- Battery ADC on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BAT_ADC);
                        break;
                    case var k when Regex.IsMatch(k, "i2c_scl_pin"):
                        desc += "- I2C SCL on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "i2c_sda_pin"):
                        desc += "- I2C SDA on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "alt_pin_pin"):
                        desc += "- ALT pin on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "one_wire_pin"):
                        desc += "- OneWire IO pin on P" + value + Environment.NewLine;
                        break;
                    case var k when Regex.IsMatch(k, "backlit_io_pin"):
                        desc += "- Backlit IO pin on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.LED);
                        break;
                    case "max_V":
                        desc += "- Battery Max Voltage: " + value + Environment.NewLine;
                        bHasBattery = true;
                        break;
                    case "min_V":
                        desc += "- Battery Min Voltage: " + value + Environment.NewLine;
                        bHasBattery = true;
                        break;
                    case "rl":
                        desc += "- Relay (channel 0) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, 0);
                        break;
                    case var k when Regex.IsMatch(k, "^rl\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Relay (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^rl_on\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Bridge Relay On (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^rl_off\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Bridge Relay Off (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Rel_n);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case "bt_pin":
                    case "bt":
                    {
                        int number = 0;
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^k\\d+pin_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^bt\\d+_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Button (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^door\\d+_magt_pin$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- Door Sensor (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case var k when Regex.IsMatch(k, "^onoff\\d+$"):
                    {
                        int number = int.Parse(Regex.Match(key, "\\d+").Value);
                        desc += "- TglChannelToggle (channel " + number + ") on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.TglChanOnTgl);
                        tg?.setPinChannel(value, number);
                        break;
                    }
                    case "gate_sensor_pin_pin":
                        desc += "- Door/Gate sensor on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        break;
                    case "basic_pin_pin":
                        // This will read 1 if there was a movement, at least on the sensor I have
                        // some devices have netled1_pin, some have netled_pin
                        desc += "- PIR sensor on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.dInput);
                        break;
                    case "ele_pin":
                    case "epin":
                        desc += "- BL0937 ELE (CF) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937CF);
                        break;
                    case "vi_pin":
                    case "ivpin":
                        desc += "- BL0937 VI (CF1) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937CF1);
                        break;
                    case "sel_pin_pin":
                    case "ivcpin":
                        desc += "- BL0937 SEL on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.BL0937SEL);
                        break;
                    case "r_pin":
                        desc += "- LED Red (Channel 1) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 0);
                        break;
                    case "g_pin":
                        desc += "- LED Green (Channel 2) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 1);
                        break;
                    case "b_pin":
                        desc += "- LED Blue (Channel 3) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 2);
                        break;
                    case "c_pin":
                        desc += "- LED Cool (Channel 4) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 3);
                        break;
                    case "w_pin":
                        desc += "- LED Warm (Channel 5) on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.PWM);
                        tg?.setPinChannel(value, 4);
                        break;
                    case "mic":
                    case "micpin":
                        desc += "- Microphone (TODO) on P" + value + Environment.NewLine;
                        break;
                    case "ctrl_pin":
                        desc += "- Control Pin (TODO) on P" + value + Environment.NewLine;
                        break;
                    case "buzzer_pwm":
                        desc += "- Buzzer Frequency (TODO) is " + value + "Hz" + Environment.NewLine;
                        break;
                    case "irpin":
                    case "infrr":
                        desc += "- IR Receiver is on P" + value + "" + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.IRRecv);
                        break;
                    case "infre":
                        desc += "- IR Transmitter is on P" + value + "" + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.IRSend);
                        break;
                    case "reset_pin":
                        desc += "- Button is on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn);
                        break;
                    case "pwmhz":
                        desc += "- PWM Frequency " + value + "" + Environment.NewLine;
                        if(tg != null && int.TryParse(value, out _)) tg.initCommandLine += $"PWMFrequency {value}\r\n";
                        break;
                    case "pirsense_pin":
                        desc += "- PIR Sensitivity " + value + "" + Environment.NewLine;
                        break;
                    case "pirlduty":
                        desc += "- PIR Low Duty " + value + "" + Environment.NewLine;
                        break;
                    case "pirfreq":
                        desc += "- PIR Frequency " + value + "" + Environment.NewLine;
                        break;
                    case "pirmduty":
                        desc += "- PIR High Duty " + value + "" + Environment.NewLine;
                        break;
                    case "pirin_pin":
                        desc += "- PIR Input " + value + "" + Environment.NewLine;
                        break;
                    case "mosi":
                        desc += "- SPI MOSI " + value + "" + Environment.NewLine;
                        // assume SPI LED
                        tg?.setPinRole(value, PinRole.SM16703P_DIN);
                        break;
                    case "miso":
                        desc += "- SPI MISO " + value + "" + Environment.NewLine;
                        break;
                    case "SCL":
                        desc += "- SPI SCL " + value + "" + Environment.NewLine;
                        break;
                    case "CS":
                        desc += "- SPI CS " + value + "" + Environment.NewLine;
                        break;
                    case "total_bt_pin":
                        desc += "- Pair/Toggle All Button on P" + value + Environment.NewLine;
                        tg?.setPinRole(value, PinRole.Btn_Tgl_All);
                        break;
                    default:
                        break;
                }
            }
            // LED
            string iicscl = getKeyValue("iicscl");
            string iicsda = getKeyValue("iicsda");
            if (iicscl.Length > 0 && iicsda.Length > 0)
            {
                string iicr = getKeyValue("iicr","-1");
                string iicg = getKeyValue("iicg", "-1");
                string iicb = getKeyValue("iicb", "-1");
                string iicc = getKeyValue("iicc", "-1");
                string iicw = getKeyValue("iicw", "-1");
                string ledType = "Unknown";
                string iicccur = getKeyValue("iicccur");
                string iicwcur = getKeyValue("iicwcur");
                string campere = getKeyValue("campere");
                string wampere = getKeyValue("wampere");
                string ehccur = getKeyValue("ehccur");
                string ehwcur = getKeyValue("ehwcur");
                string drgbcur = getKeyValue("drgbcur");
                string dwcur = getKeyValue("dwcur");
                string dccur = getKeyValue("dccur");
                string cjwcur = getKeyValue("cjwcur");
                string cjccur = getKeyValue("cjccur");
                string _2235ccur = getKeyValue("2235ccur");
                string _2235wcur = getKeyValue("2235wcur");
                string _2335ccur = getKeyValue("2335ccur");
                string kp58wcur = getKeyValue("kp58wcur");
                string kp58ccur = getKeyValue("kp58ccur");
                string currents = string.Empty;
                bool isExc = false;
                // use current (color/cw) setting
                if (ehccur.Length>0 || wampere.Length > 0 || iicccur.Length > 0)
                {
                    ledType = "SM2135";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = ehccur.Length > 0 ? Convert.ToInt32(ehccur) : iicccur.Length > 0 ? Convert.ToInt32(iicccur) : campere.Length > 0 ? Convert.ToInt32(campere) : 1;
                        cwcurrent = ehwcur.Length > 0 ? Convert.ToInt32(ehwcur) : iicwcur.Length > 0 ? Convert.ToInt32(iicwcur) : wampere.Length > 0 ? Convert.ToInt32(wampere) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"SM2135_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(ehccur.Length > 0 ? ehccur : iicccur.Length > 0 ? iicccur : campere.Length > 0 ? campere : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(ehwcur.Length > 0 ? ehwcur : iicwcur.Length > 0 ? iicwcur : wampere.Length > 0 ? wampere : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.SM2135DAT);
                    tg?.setPinRole(iicscl, PinRole.SM2135CLK);
                }
                else if (dccur.Length > 0)
                {
                    ledType = "BP5758D_";
                    var rgbcurrent = 0;
                    var wcurrent = 0;
                    var ccurrent = 0;
                    try
                    {
                        rgbcurrent = drgbcur.Length > 0 ? Convert.ToInt32(drgbcur) : 1;
                        wcurrent = dwcur.Length > 0 ? Convert.ToInt32(dwcur) : 1;
                        ccurrent = dccur.Length > 0 ? Convert.ToInt32(dccur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"BP5758D_Current {rgbcurrent} {Math.Max(wcurrent, ccurrent)}\r\n";
                    }
                    currents =  $"- RGB current is {(drgbcur.Length > 0 ? drgbcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Warm white current is {(dwcur.Length > 0 ? dwcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- Cold white current is {(dccur.Length > 0 ? dccur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.BP5758D_DAT);
                    tg?.setPinRole(iicscl, PinRole.BP5758D_CLK);
                }
                else if (cjwcur.Length > 0)
                {
                    ledType = "BP1658CJ_";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = cjccur.Length > 0 ? Convert.ToInt32(cjccur) : 1;
                        cwcurrent = cjwcur.Length > 0 ? Convert.ToInt32(cjwcur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"BP1658CJ_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(cjccur.Length > 0 ? cjccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(cjwcur.Length > 0 ? cjwcur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.BP1658CJ_DAT);
                    tg?.setPinRole(iicscl, PinRole.BP1658CJ_CLK);
                }
                else if (_2235ccur.Length > 0)
                {
                    ledType = "SM2235";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = _2235ccur.Length > 0 ? Convert.ToInt32(_2235ccur) : 1;
                        cwcurrent = _2235wcur.Length > 0 ? Convert.ToInt32(_2235wcur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"SM2235_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(_2235ccur.Length > 0 ? _2235ccur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(_2235wcur.Length > 0 ? _2235wcur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.SM2235DAT);
                    tg?.setPinRole(iicscl, PinRole.SM2235CLK);
                }
                else if (kp58wcur.Length > 0)
                {
                    ledType = "KP18058_";
                    var rgbcurrent = 0;
                    var cwcurrent = 0;
                    try
                    {
                        rgbcurrent = kp58wcur.Length > 0 ? Convert.ToInt32(kp58wcur) : 1;
                        cwcurrent = kp58ccur.Length > 0 ? Convert.ToInt32(kp58ccur) : 1;
                    }
                    catch
                    {
                        isExc = true;
                    }
                    finally
                    {
                        if(tg != null && !isExc) tg.initCommandLine += $"KP18058_Current {rgbcurrent} {cwcurrent}\r\n";
                    }
                    currents =  $"- RGB current is {(kp58wcur.Length > 0 ? kp58wcur : "Unknown")} mA{Environment.NewLine}";
                    currents += $"- White current is {(kp58ccur.Length > 0 ? kp58ccur : "Unknown")} mA{Environment.NewLine}";
                    tg?.setPinRole(iicsda, PinRole.KP18058_DAT);
                    tg?.setPinRole(iicscl, PinRole.KP18058_CLK);
                }
                else
                {

                }
                string dat_name = ledType + "DAT";
                string clk_name = ledType + "CLK";
                desc += "- " + dat_name + " on P" + iicsda + Environment.NewLine;
                desc += "- " + clk_name + " on P" + iicscl + Environment.NewLine;
                string map = "" + iicr + " " + iicg + " " + iicb + " " + iicc + " " + iicw;
                isExc = false;
                try
                {
                    map = $"{Convert.ToInt32(iicr)} {Convert.ToInt32(iicg)} {Convert.ToInt32(iicb)} {Convert.ToInt32(iicc)} {Convert.ToInt32(iicw)}";
                }
                catch
                {
                    isExc = true;
                }
                finally
                {
                    if(tg != null && !isExc) tg.initCommandLine += $"LED_Map {map}\r\n";
                }
                desc += "- LED remap is " + map + Environment.NewLine;
                desc += currents;
            }
            if (desc.Length > 0)
            {
                desc = "Device configuration, as extracted from Tuya: " + Environment.NewLine + desc;
            }
            else
            {
                desc = "Sorry, no meaningful pins data found. This device may be TuyaMCU or a custom one with no Tuya config data." + Environment.NewLine;
            }
            if (bHasBattery)
            {
                desc += "Device seems to use Battery Driver. See more details here: https://www.elektroda.com/rtvforum/topic3959103.html" + Environment.NewLine;
            }
            var baud = FindKeyValueIn(source, "baud");
            if(baud != null)
            {
                desc += "Baud keyword found, this device may be TuyaMCU or BL0942. Baud value is " + baud + Environment.NewLine;
            }
            var kp = FindKeyValueIn(source, "module");
            kp ??= FindKeyContainingIn(source, "module");
            if (kp != null)
            {
                var type = TuyaModules.getTypeForModuleName(kp);
                desc += "Device seems to be using " + kp + " module";
                if(type != nameof(BKType.Invalid))
                {
                    desc += ", which is using " + type + ".";
                }
                else
                {
                    desc += ".";
                }
            }
            else
            {
                desc += "No module information found.";
            }
            kp = FindKeyValueIn(source, "em_sys_env");
            if(kp != null)
            {
                desc += Environment.NewLine;
                var type = TuyaModules.getTypeForPlatformName(kp);
                desc += $"Device internal platform - {kp}";
                if(type != nameof(BKType.Invalid))
                {
                    desc += ", equals " + type + ".";
                }
                else
                {
                    desc += ".";
                }
            }
            desc += Environment.NewLine;
            void printposdevice(string device)
            {
                desc += $"And the Tuya section starts at {getMagicPositionDecAndHex()}, which is a default {device} offset." + Environment.NewLine;
            }
            switch(magicPosition)
            {
                case 0:
                case 0x1000:
                    break;
                case USUAL_BK7231_MAGIC_POSITION:
                    desc += $"And the Tuya section starts, as usual, at {getMagicPositionDecAndHex()}" + Environment.NewLine;
                    break;
                case USUAL_BK_NEW_XR806_MAGIC_POSITION:
                    printposdevice("T1/XR806 and some T34/BK7231N");
                    break;
                case USUAL_RTLC_ECR6600_MAGIC_POSITION:
                    printposdevice("RTL8720C and ECR6600");
                    break;
                case USUAL_RTLB_XR809_MAGIC_POSITION:
                    printposdevice("RTL8710B/XR809/BK7231Q");
                    break;
                case USUAL_T3_MAGIC_POSITION:
                    printposdevice("T3/BK7236");
                    break;
                case USUAL_T5_MAGIC_POSITION:
                    printposdevice("T5/BK7258");
                    break;
                case USUAL_RTLD_MAGIC_POSITION:
                    printposdevice("4MB RTL8720D");
                    break;
                case USUAL_WBRG1_MAGIC_POSITION:
                    printposdevice("8MB RTL8720D/WBRG1");
                    break;
                case USUAL_LN882H_MAGIC_POSITION:
                    printposdevice("LN882H");
                    break;
                case USUAL_TR6260_MAGIC_POSITION:
                    printposdevice("TR6260");
                    break;
                case USUAL_W800_MAGIC_POSITION:
                    printposdevice("W800");
                    break;
                case USUAL_LN8825_MAGIC_POSITION:
                    printposdevice("LN8825B");
                    break;
                case USUAL_RTLCM_MAGIC_POSITION:
                    printposdevice("RTL8720CM");
                    break;
                case USUAL_BK7252_MAGIC_POSITION:
                    printposdevice("BK7252");
                    break;
                case USUAL_ESP8266_MAGIC_POSITION:
                    printposdevice("ESP8266");
                    break;
                default:
                    desc += "And the Tuya section starts at an UNCOMMON POSITION " + getMagicPositionDecAndHex() + Environment.NewLine;
                    break;

            }
            return desc;
        }

        static string FindKeyContainingIn(Dictionary<string, string> source, string keyPart)
        {
            if (source == null || string.IsNullOrEmpty(keyPart))
                return null;
            foreach (var kv in source)
            {
                if (kv.Key != null && kv.Key.Contains(keyPart))
                    return kv.Value;
            }
            return null;
        }

        static string FindKeyValueIn(Dictionary<string, string> source, string key)
        {
            if (source == null || key == null)
                return null;
            if (source.TryGetValue(key, out var value))
                return value;
            return null;
        }

        Dictionary<string, string> GetEnhancedParmsCached()
        {
            var src = vaultDecryptedRaw;
            if (src == null)
                return null;

            if (_cachedEnhancedParms != null && object.ReferenceEquals(_cachedEnhancedParmsSource, src))
                return _cachedEnhancedParms;

            try
            {
                _cachedEnhancedParms = BuildEnhancedParmsFromVault();
                _cachedEnhancedParmsSource = src;
                return _cachedEnhancedParms;
            }
            catch
            {
                _cachedEnhancedParms = null;
                _cachedEnhancedParmsSource = src;
                return null;
            }
        }

        Dictionary<string, string> BuildEnhancedParmsFromVault()
        {
            var result = new Dictionary<string, string>();

            // Start from enhanced entries (per-key best selection, regardless of overall dedupe).
            var kvs = GetVaultEntriesEnhancedCached();
            if (kvs == null || kvs.Count == 0)
                return result;

            byte[] upk = kvs.FirstOrDefault(x => x.Key == "user_param_key")?.Value;
            byte[] baudCfg = kvs.FirstOrDefault(x => x.Key == "baud_cfg")?.Value;
            byte[] em = kvs.FirstOrDefault(x => x.Key == "em_sys_env")?.Value;

            // em_sys_env (platform string)
            if (em != null && !IsAllPadding(em))
            {
                string s = bytesToAsciiStr(em);
                if (!string.IsNullOrWhiteSpace(s))
                    result["em_sys_env"] = s;
            }

            // Prefer user_param_key for pins/module.
            if (upk != null && upk.Length > 0 && !IsAllPadding(upk))
            {
                if (TryParseTuyaObjectPairsFromBytes(upk, out var dictFromUpk) && dictFromUpk.Count > 0)
                {
                    foreach (var kv in dictFromUpk)
                    {
                        if (!result.ContainsKey(kv.Key))
                            result[kv.Key] = kv.Value;
                    }
                }
            }

            // baud_cfg can provide baud value.
            if (baudCfg != null && baudCfg.Length > 0 && !IsAllPadding(baudCfg))
            {
                if (TryParseTuyaObjectPairsFromBytes(baudCfg, out var dictFromBaud) && dictFromBaud.Count > 0)
                {
                    foreach (var kv in dictFromBaud)
                    {
                        if (!result.ContainsKey(kv.Key))
                            result[kv.Key] = kv.Value;
                    }
                }
            }

            // If classic extraction already has some useful fields that enhanced didn't parse, merge them.
            // This keeps behavior consistent for fields like magic position diagnostics.
            foreach (var kv in parms)
            {
                if (!result.ContainsKey(kv.Key))
                    result[kv.Key] = kv.Value;
            }

            return result;
        }

        static bool TryParseTuyaObjectPairsFromBytes(byte[] data, out Dictionary<string, string> dict)
        {
            dict = new Dictionary<string, string>();
            if (data == null || data.Length == 0)
                return false;

            string ascii = ExtractAsciiForJsonSearch(data, maxChars: 65536);
            if (string.IsNullOrWhiteSpace(ascii))
                return false;

            // First attempt: extract a balanced JSON object/array and parse strictly.
            int start = FindNextJsonStart(ascii, 0);
            if (start >= 0 && TryExtractBalancedJson(ascii, start, out int endExclusive, out string jsonCandidate))
            {
                if (TryParseJsonToFlatPairs(jsonCandidate, dict))
                    return dict.Count > 0;

                if (TryRepairTuyaJsonish(jsonCandidate, out var repaired) && TryParseJsonToFlatPairs(repaired, dict))
                    return dict.Count > 0;
            }

            // Fallback: loose key:value pairs (as used by the classic extraction).
            string[] pairs = ascii.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in pairs)
            {
                string[] kp = p.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (kp.Length < 2)
                    continue;

                string skey = (kp.Length > 2 && kp[1].Contains('[')) ? kp[1] : kp[0];
                string svalue = kp[kp.Length - 1];

                skey = skey.Trim(new char[] { '"' }).Replace("\"", "").Replace("[", "").Replace("{", "");
                svalue = svalue.Trim(new char[] { '"' }).Replace("\"", "").Replace("}", "");

                if (string.IsNullOrWhiteSpace(skey))
                    continue;

                if (!dict.ContainsKey(skey))
                    dict[skey] = svalue;
            }

            return dict.Count > 0;
        }

        static bool TryParseJsonToFlatPairs(string json, Dictionary<string, string> dict)
        {
            if (string.IsNullOrWhiteSpace(json) || dict == null)
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                            continue;

                        string val = prop.Value.ToString();
                        dict[prop.Name] = val;
                    }
                    return dict.Count > 0;
                }

                // If it's an array, we can't flatten to Tuya pin pairs reliably.
                return false;
            }
            catch
            {
                return false;
            }
        }
        public string getKeyValue(string key, string sdefault = "")
        {
            if(parms.TryGetValue(key, out var value))
                return value;
            return sdefault;
        }
        public string getKeysAsJSON()
        {
            string r = "{";
            foreach(var kv in parms)
            {
                r += Environment.NewLine + $"\t\"{kv.Key}\":\"{kv.Value}\",";
            }
            if(parms.Count > 0)
            {
                r = r.Substring(0, r.Length - 1); // remove last ','
                r += Environment.NewLine;
            }
            r += "}" + Environment.NewLine;
            return r;
        }
                        public string getEnhancedExtractionText()
        {
            // Enhanced mode output for the JSON text area must be a single valid JSON document.
            // Merge KV entries into one JSON object: { "<kv_key>": <parsed-json-or-string>, ... }
            // Non-enhanced (classic) output remains unchanged.

            var src = vaultDecryptedRaw;
            if (src == null || src.Length == 0)
                return "";

            if (_cachedEnhancedText != null && object.ReferenceEquals(_cachedEnhancedTextSource, src))
                return _cachedEnhancedText;

            JsonElement ToJsonElement(string rendered)
            {
                rendered = NormalizeEnhancedNewlines(rendered ?? "").Trim();

                // Try to treat it as JSON first (object/array/scalar). If that fails, store as a JSON string.
                try
                {
                    using var doc = JsonDocument.Parse(rendered, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                    return doc.RootElement.Clone();
                }
                catch
                {
                    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(rendered));
                    return doc.RootElement.Clone();
                }
            }


            string FixDisplayEscapes(string json)
            {
                if (string.IsNullOrEmpty(json))
                    return json;

                // System.Text.Json escapes some characters by default (e.g. + as \\u002B, and some HTML-sensitive chars).
                // For UI readability, unescape those sequences in the emitted JSON text.
                return json
                    .Replace("\\u002B", "+").Replace("\\u002b", "+")
                    .Replace("\\u003C", "<").Replace("\\u003c", "<")
                    .Replace("\\u003E", ">").Replace("\\u003e", ">")
                    .Replace("\\u0026", "&")
                    .Replace("\\u003D", "=").Replace("\\u003d", "=")
                    .Replace("\\u0027", "'");
            }

            // Preserve the original KV enumeration order for readability in the text area.
            var valuesByKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var orderedKeys = new List<string>();
            bool hasAny = false;

            try
            {
                var kvs = GetVaultEntriesEnhancedCached();
                foreach (var kv in kvs)
                {
                    if (kv == null || string.IsNullOrEmpty(kv.Key))
                        continue;

                    string rendered = RenderEnhancedValue(kv.Key, kv.Value);
                    var el = ToJsonElement(rendered);

                    if (!valuesByKey.ContainsKey(kv.Key))
                        orderedKeys.Add(kv.Key);

                    valuesByKey[kv.Key] = el;
                    hasAny = true;
                }
            }
            catch
            {
                // Best-effort only.
            }

            // If enhanced yielded nothing, keep prior best-effort fallback behavior, but remain JSON-correct.
            if (!hasAny)
            {
                try
                {
                    // Try to recover a single well-formed JSON object/array from the decrypted vault blob.
                    if (TryPrettyPrintJsonFromMixedBytes(src, "vault_blob", out var prettyBlob))
                    {
                        string p = NormalizeEnhancedNewlines(prettyBlob).Trim();
                        if (p.Length > 0)
                        {
                            string fixedP = FixDisplayEscapes(p) + Environment.NewLine;
                            _cachedEnhancedText = fixedP;
                            _cachedEnhancedTextSource = src;
                            return fixedP;
                        }
                    }

                    // If classic/raw extraction produced parameters, return the classic JSON object (already valid JSON).
                    if (parms != null && parms.Count > 0)
                    {
                        string fallback = FixDisplayEscapes(getKeysAsJSON());
                        _cachedEnhancedText = fallback;
                        _cachedEnhancedTextSource = src;
                        return fallback;
                    }

                    // Last resort: expose ASCII as a JSON string field so the output remains valid JSON.
                    if (IsAsciiOnlyOrPadded(src))
                    {
                        string ascii = ExtractAsciiForJsonSearch(src, maxChars: 65536);
                        if (!string.IsNullOrWhiteSpace(ascii))
                        {
                            var asciiObj = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                            {
                                ["_ascii"] = ToJsonElement(ascii)
                            };
                            string s = JsonSerializer.Serialize(asciiObj, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
                            s = FixDisplayEscapes(s);
                            _cachedEnhancedText = s;
                            _cachedEnhancedTextSource = src;
                            return s;
}
                    }
                }
                catch
                {
                    // Best-effort only.
                }

                return "";
            }

            // Credential safety net (preserve prior behavior) without breaking single-JSON:
            // if raw/classic extraction has credentials but the enhanced view doesn't surface them, include
            // the classic/raw JSON under a reserved property.
            try
            {
                bool rawHasCreds = parms != null && (parms.ContainsKey("uuid") || parms.ContainsKey("auth_key") || parms.ContainsKey("psk_key"));
                if (rawHasCreds)
                {
                    bool enhancedHasCreds = false;

                    // Fast path: check gw_bi object if present.
                    if (valuesByKey.TryGetValue("gw_bi", out var gwbi) && gwbi.ValueKind == JsonValueKind.Object)
                    {
                        if (gwbi.TryGetProperty("uuid", out _) || gwbi.TryGetProperty("auth_key", out _) || gwbi.TryGetProperty("psk_key", out _))
                            enhancedHasCreds = true;
                    }

                    // Also consider direct top-level presence, just in case.
                    if (!enhancedHasCreds && (valuesByKey.ContainsKey("uuid") || valuesByKey.ContainsKey("auth_key") || valuesByKey.ContainsKey("psk_key")))
                        enhancedHasCreds = true;

                    if (!enhancedHasCreds)
                    {
                        string rawJson = getKeysAsJSON();
                        if (!string.IsNullOrWhiteSpace(rawJson))
                        {
                            const string RawKey = "_raw";
                            if (!valuesByKey.ContainsKey(RawKey))
                                orderedKeys.Add(RawKey);
                            valuesByKey[RawKey] = ToJsonElement(rawJson);
                        }
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }

            // Emit one JSON object in a stable, readable order.
            string result;
            try
            {
                // Manual formatting avoids Utf8JsonWriter (which may pull in IAsyncDisposable/Microsoft.Bcl.AsyncInterfaces on .NET Framework).
                var keysToWrite = new List<string>();
                foreach (var k in orderedKeys)
                {
                    if (valuesByKey.ContainsKey(k))
                        keysToWrite.Add(k);
                }

                var sb = new StringBuilder();
                sb.Append("{").Append(Environment.NewLine);

                var opts = new JsonSerializerOptions { WriteIndented = true };

                for (int i = 0; i < keysToWrite.Count; i++)
                {
                    string k = keysToWrite[i];
                    var v = valuesByKey[k];

                    sb.Append("  ");
                    sb.Append(JsonSerializer.Serialize(k));
                    sb.Append(": ");

                    string vJson = JsonSerializer.Serialize(v, opts);

                    // Normalize to LF for processing then re-emit with Environment.NewLine.
                    vJson = vJson.Replace("\r\n", "\n").Replace("\r", "\n");
                    var lines = vJson.Split('\n');
                    if (lines.Length > 0)
                    {
                        sb.Append(lines[0]);
                        for (int li = 1; li < lines.Length; li++)
                        {
                            sb.Append(Environment.NewLine);
                            sb.Append("  ");
                            sb.Append(lines[li]);
                        }
                    }

                    if (i != keysToWrite.Count - 1)
                        sb.Append(",");

                    sb.Append(Environment.NewLine);
                }

                sb.Append("}").Append(Environment.NewLine);
                result = sb.ToString();
            }
            catch
            {
                // Fallback to serializer if manual formatting fails for any reason.
                var tmp = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var k in orderedKeys)
                {
                    if (valuesByKey.TryGetValue(k, out var v))
                        tmp[k] = v;
                }
                result = JsonSerializer.Serialize(tmp, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
            }

            result = FixDisplayEscapes(result);

            // Safety: ensure the emitted text is valid JSON. If not, fall back to serializer output.
            try
            {
                using var _ = JsonDocument.Parse(result);
            }
            catch
            {
                var tmp = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var k in orderedKeys)
                {
                    if (valuesByKey.TryGetValue(k, out var v))
                        tmp[k] = v;
                }
                result = JsonSerializer.Serialize(tmp, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
                result = FixDisplayEscapes(result);
            }

            _cachedEnhancedText = result;
            _cachedEnhancedTextSource = src;
            return result;
        }



                static string RenderEnhancedValue(string kvKey, byte[] value)
        {
            // Some keys legitimately exist with an "empty" value.
            // They can be stored as zero-length, or as a blob that is entirely padding (0x00/0xFF).
            // Preserve them as an explicit empty string so the key still shows up in enhanced output.
            if (value != null && (value.Length == 0 || IsAllPadding(value)))
                return "\"\"";

            // tuya_seed is a binary blob (typically 32 bytes). Rendering it as mixed ASCII + \xNN
            // is confusing; present it as compact hex/base64 derived directly from the bytes.
            if (string.Equals(kvKey, "tuya_seed", StringComparison.Ordinal))
            {
                string seed = RenderBinaryCompact(value);
                if (!string.IsNullOrWhiteSpace(seed))
                    return seed;
            }

            // Fast path: if the value (or a sub-range of it) contains a valid JSON object/array,
            // prefer the pretty-printed JSON as the enhanced presentation.
            // This is bounded and best-effort; it does not synthesize data, it only renders bytes already present.
            if (TryPrettyPrintJsonFromMixedBytes(value, kvKey, out var prettyAny))
                return NormalizeEnhancedNewlines(prettyAny).Trim();


            // Special-case: user_param_key is frequently stored as JSON-ish text but can be wrapped
            // with binary/padding bytes. In enhanced extraction we recover and pretty-print the first
            // valid JSON object/array found within the ASCII-cleaned bytes.
            if (string.Equals(kvKey, "user_param_key", StringComparison.Ordinal))
            {
                if (TryPrettyPrintJsonFromMixedBytes(value, kvKey, out var prettyUpk))
                    return NormalizeEnhancedNewlines(prettyUpk).Trim();

                // Fallback: if we could not isolate a full JSON object/array, still prefer a clean
                // ASCII-only view over \\xNN noise (presentation-only).
                if (IsAsciiOnlyOrPadded(value))
                {
                    string upkAscii = ExtractAsciiForJsonSearch(value, maxChars: 65536);
                    if (!string.IsNullOrWhiteSpace(upkAscii))
                    {
                        string asciiNorm = NormalizeEnhancedNewlines(upkAscii).Trim();

                        if (TryPrettyPrintLooseJsonPairs(asciiNorm, kvKey, out var prettyPairs))
                            return NormalizeEnhancedNewlines(prettyPairs).Trim();

                        return asciiNorm;
                    }
                }
            }

            // Some images contain a gw_bi value wrapped with binary bytes and/or padding.
            // In those cases, prefer a clean ASCII-only view over \\xNN sequences (presentation-only),
            // but only when the underlying bytes are actually ASCII (plus typical padding).
            if (string.Equals(kvKey, "gw_bi", StringComparison.Ordinal))
            {
                if (IsAsciiOnlyOrPadded(value))
                {
                    string gwbiAscii = ExtractAsciiForJsonSearch(value, maxChars: 65536);
                    if (!string.IsNullOrWhiteSpace(gwbiAscii))
                    {
                        string asciiNorm = NormalizeEnhancedNewlines(gwbiAscii).Trim();
                        string asciiTrim = asciiNorm.TrimStart();
                        if (asciiTrim.Length > 0 && (asciiTrim[0] == '{' || asciiTrim[0] == '['))
                        {
                            if (TryExtractBalancedJson(asciiTrim, 0, out int endExclusive, out string jsonCandidate) &&
                                IsOnlyWhitespaceOrEscapedPadding(asciiTrim, endExclusive) &&
                                TryPrettyPrintJsonCandidate(jsonCandidate, kvKey, out var pretty))
                            {
                                return NormalizeEnhancedNewlines(pretty).Trim();
                            }
                        }

                        return asciiNorm;
                    }
                }
            }

            // If the key is known to be mostly textual, prefer a clean ASCII-only view over \xNN noise,
            // but only when the underlying bytes are actually ASCII (plus typical padding). This matches
            // Strict ASCII-gating avoids presenting partial garbage as meaningful text.
            if (!string.IsNullOrEmpty(kvKey) &&
                (kvKey.StartsWith("gw_", StringComparison.Ordinal) ||
                 kvKey.EndsWith("_v2", StringComparison.Ordinal)))
            {
                if (IsAsciiOnlyOrPadded(value))
                {
                    string ascii = ExtractAsciiForJsonSearch(value, maxChars: 65536);
                    if (!string.IsNullOrWhiteSpace(ascii))
                        return NormalizeEnhancedNewlines(ascii).Trim();
                }
                else
                {
                    // If it is not clean ASCII (plus padding), do not try to
                    // present partial garbage as text. Use a compact binary representation instead.
                    string bin = RenderBinaryCompact(value);
                    if (!string.IsNullOrWhiteSpace(bin))
                        return bin;
                }
            }
            // Final fallback: avoid emitting mixed ASCII + \xNN noise.
            if (IsAsciiOnlyOrPadded(value))
            {
                string ascii = ExtractAsciiForJsonSearch(value, maxChars: 65536);
                if (!string.IsNullOrWhiteSpace(ascii))
                {
                    string asciiNorm = NormalizeEnhancedNewlines(ascii).Trim();

                    if (TryPrettyPrintLooseJsonPairs(asciiNorm, kvKey, out var prettyPairs))
                        return NormalizeEnhancedNewlines(prettyPairs).Trim();

                    return asciiNorm;
                }
            }

            string compact = RenderBinaryCompact(value);
            return string.IsNullOrWhiteSpace(compact) ? "" : compact;
}


        // Attempt to locate and pretty-print a valid JSON object/array embedded inside a mixed
        // binary/text value. This is deliberately bounded and best-effort.
        static bool TryPrettyPrintJsonFromMixedBytes(byte[] data, string kvKey, out string pretty)
        {
            pretty = null;
            if (data == null || data.Length == 0)
                return false;

            // Extract ASCII only (drops binary noise) to avoid \xNN expansion.
            // Bounded to keep worst-case time predictable.
            string ascii = ExtractAsciiForJsonSearch(data, maxChars: 65536);
            if (string.IsNullOrWhiteSpace(ascii))
                return false;

            int startAt = 0;
            int bestLen = 0;
            string bestPretty = null;

            // Scan a handful of candidate JSON starts and keep the *largest* valid JSON found.
            // This helps when the value contains multiple fragments or when there is a short false-positive first.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int start = FindNextJsonStart(ascii, startAt);
                if (start < 0)
                    break;

                if (TryExtractBalancedJson(ascii, start, out int endExclusive, out string jsonCandidate))
                {
                    // Only accept if it parses as JSON.
                    if (TryPrettyPrintJsonCandidate(jsonCandidate, kvKey, out var prettyCandidate))
                    {
                        if (jsonCandidate.Length > bestLen)
                        {
                            bestLen = jsonCandidate.Length;
                            bestPretty = prettyCandidate;
                        }
                    }
                }

                startAt = start + 1;
            }

            if (bestPretty != null)
            {
                pretty = bestPretty;
                return true;
            }

            return false;
        }


        static bool IsAsciiOnlyOrPadded(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            bool hasContent = false;

            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];

                // Allow typical erased/padded bytes.
                if (b == 0x00 || b == 0xFF)
                    continue;

                // Allow common whitespace.
                if (b == 0x09 || b == 0x0A || b == 0x0D)
                {
                    hasContent = true;
                    continue;
                }

                // Visible ASCII.
                if (b >= 0x20 && b <= 0x7E)
                {
                    hasContent = true;
                    continue;
                }

                return false;
            }

            return hasContent;
        }


        static string ExtractAsciiForJsonSearch(byte[] data, int maxChars)
        {
            if (data == null || data.Length == 0)
                return "";

            if (maxChars < 128)
                maxChars = 128;

            var sb = new StringBuilder(Math.Min(maxChars, data.Length));
            int written = 0;

            for (int i = 0; i < data.Length && written < maxChars; i++)
            {
                byte b = data[i];

                // Skip typical flash padding and NULs.
                if (b == 0x00 || b == 0xFF)
                    continue;

                // Preserve common whitespace.
                if (b == 0x09 || b == 0x0A || b == 0x0D)
                {
                    sb.Append((char)b);
                    written++;
                    continue;
                }

                // Visible ASCII.
                if (b >= 0x20 && b <= 0x7E)
                {
                    sb.Append((char)b);
                    written++;
                    continue;
                }
            }

            return sb.ToString();
        }


		static bool IsAllPadding(byte[] data)
		{
			if (data == null || data.Length == 0)
				return true;

			for (int i = 0; i < data.Length; i++)
			{
				byte b = data[i];
				if (b != 0x00 && b != 0xFF)
					return false;
			}

			return true;
		}

		static string RenderBinaryCompact(byte[] data)
		{
			if (data == null || data.Length == 0)
				return "";

			int end = data.Length;
			while (end > 0 && (data[end - 1] == 0x00 || data[end - 1] == 0xFF))
				end--;

			if (end <= 0)
				return "";

			// For larger blobs, base64 keeps the enhanced output size under control.
			if (end > 512)
				return Convert.ToBase64String(data, 0, end);

			var sb = new StringBuilder(end * 2);
			for (int i = 0; i < end; i++)
				sb.Append(data[i].ToString("x2"));

			return sb.ToString();
		}


        // Render a binary KV value into a stable, readable string without dropping content.
        // - Visible ASCII and common whitespace are preserved.
        // - Non-ASCII/control bytes are shown as \\xNN (presentation-only).
        // - Trailing NUL padding is ignored (presentation-only).
        static string RenderBytesForEnhancedDisplay(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";

            int end = data.Length;
            while (end > 0 && (data[end - 1] == 0x00 || data[end - 1] == 0xFF))
                end--;

            if (end <= 0)
                return "";

            var sb = new StringBuilder(end);

            for (int i = 0; i < end; i++)
            {
                byte b = data[i];

                // Preserve common whitespace.
                if (b == 0x09) { sb.Append('\t'); continue; } // TAB
                if (b == 0x0A) { sb.Append('\n'); continue; } // LF
                if (b == 0x0D) { sb.Append('\n'); continue; } // CR -> normalize to LF

                // Visible ASCII.
                if (b >= 0x20 && b <= 0x7E)
                {
                    sb.Append((char)b);
                    continue;
                }

                // Ignore NUL padding (presentation only).
                if (b == 0x00)
                    continue;

                // Preserve non-ASCII/control bytes in a compact, non-garbled form.
                sb.Append("\\x");
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

                
        
static bool IsSimpleEnhancedKey(string key)
{
    if (string.IsNullOrWhiteSpace(key) || key.Length > 64)
        return false;

    for (int i = 0; i < key.Length; i++)
    {
        char c = key[i];
        bool ok =
            (c >= 'a' && c <= 'z') ||
            (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9') ||
            c == '_' || c == '-' || c == '.' || c == ':';
        if (!ok)
            return false;
    }
    return true;
}

static string FormatEnhancedKeyLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";

            // Keep simple identifiers as-is.
            if (key.Length <= 64)
            {
                bool simple = true;
                for (int i = 0; i < key.Length; i++)
                {
                    char c = key[i];
                    if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-'))
                    {
                        simple = false;
                        break;
                    }
                }
                if (simple)
                    return key;
            }

            // Otherwise, quote+escape so fragments like :null,"passwd":null,... remain visible but unambiguous.
            return QuoteAndEscapeForDisplay(key);
        }

        static string QuoteAndEscapeForDisplay(string s)
        {
            if (s == null)
                return "\"\"";

            var sb = new StringBuilder(s.Length + 2);
            sb.Append('\"');

            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            sb.Append("\\u" + ((int)ch).ToString("X4"));
                        else
                            sb.Append(ch);
                        break;
                }
            }

            sb.Append('\"');
            return sb.ToString();
        }

        static bool TryPrettyPrintLooseJsonPairs(string ascii, string kvKey, out string pretty)
        {
            pretty = null;
            if (string.IsNullOrWhiteSpace(ascii))
                return false;

            string s = ascii.Trim();
            if (s.Length < 8)
                return false;

            if (s[0] == '{' || s[0] == '[')
                return false;

            // Heuristic: only attempt when the text looks like it contains credential-ish pairs.
            bool looksRelevant =
                s.IndexOf("\"uuid\"", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("\"auth_key\"", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("\"psk_key\"", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("\"ap_ssid\"", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("\"ap_passwd\"", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("\"nc_tp\"", StringComparison.Ordinal) >= 0;

            if (!looksRelevant)
                return false;

            s = s.TrimStart(',', ' ', '\t');

            // Remove a trailing comma if present.
            s = s.TrimEnd();
            if (s.EndsWith(",", StringComparison.Ordinal))
                s = s.TrimEnd(',');

            string candidate = "{" + s + "}";

            if (TryPrettyPrintJsonCandidate(candidate, kvKey, out var prettyInner))
            {
                pretty = prettyInner;
                return true;
            }

            if (TryRepairTuyaJsonish(candidate, out var repaired) &&
                TryPrettyPrintJsonCandidate(repaired, kvKey, out var prettyRepaired))
            {
                pretty = prettyRepaired;
                return true;
            }

            return false;
        }

static string NormalizeEnhancedNewlines(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            // Normalize newline forms.
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");

            // Collapse overly long blank-line runs (presentation only) in a single pass.
            s = Regex.Replace(s, "\n{3,}", "\n\n");

            return s.Replace("\n", Environment.NewLine);
        }

        static bool IsAllWhitespace(string s, int startIndex)
        {
            if (string.IsNullOrEmpty(s))
                return true;
            if (startIndex < 0)
                startIndex = 0;
            for (int i = startIndex; i < s.Length; i++)
            {
                if (!char.IsWhiteSpace(s[i]))
                    return false;
            }
            return true;
        }

        static bool IsOnlyWhitespaceOrEscapedPadding(string s, int startIndex)
        {
            if (string.IsNullOrEmpty(s))
                return true;

            if (startIndex < 0)
                startIndex = 0;

            for (int i = startIndex; i < s.Length;)
            {
                char c = s[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Accept common escaped padding sequences produced by RenderBytesForEnhancedDisplay (presentation-only):
                // \xFF (erased flash) and \x00 (NUL padding).
                if (c == '\\' && i + 3 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
                {
                    char h1 = s[i + 2];
                    char h2 = s[i + 3];

                    bool isFF = (h1 == 'F' || h1 == 'f') && (h2 == 'F' || h2 == 'f');
                    bool is00 = (h1 == '0') && (h2 == '0');

                    if (isFF || is00)
                    {
                        i += 4;
                        continue;
                    }
                }

                return false;
            }

            return true;
        }



		// Pretty-print any valid JSON objects/arrays found within a text run.
        // If parsing fails, the original text is preserved.
        static string PrettyPrintJsonInsideText(string text, string kvKey)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder(text.Length);
            int i = 0;

            while (i < text.Length)
            {
                int start = FindNextJsonStart(text, i);
                if (start < 0)
                {
                    sb.Append(text.Substring(i));
                    break;
                }

                sb.Append(text.Substring(i, start - i));

                if (TryExtractBalancedJson(text, start, out int endExclusive, out string jsonCandidate))
                {
                    if (TryPrettyPrintJsonCandidate(jsonCandidate, kvKey, out var pretty))
                    {
                        // Separate adjacent text from multi-line JSON for readability.
                        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                            sb.Append('\n');

                        sb.Append(pretty);

                        if (endExclusive < text.Length && text[endExclusive] != '\n')
                            sb.Append('\n');

                        i = endExclusive;
                        continue;
                    }
                }

                // Not JSON (or couldn't be parsed) - keep original character and continue.
                sb.Append(text[start]);
                i = start + 1;
            }

            return sb.ToString();
        }

        static int FindNextJsonStart(string text, int startAt)
{
    // Find '{' or '[' that is NOT inside a quoted JSON string.
    // This avoids false positives like the "[[...]]" embedded in gw_ai.s_time_z (string).
    bool inString = false;
    bool escape = false;

    for (int i = 0; i < text.Length; i++)
    {
        char ch = text[i];

        if (i >= startAt)
        {
            if (!inString && (ch == '{' || ch == '['))
                return i;
        }

        if (escape)
        {
            escape = false;
            continue;
        }

		if (ch == '\\')
        {
            if (inString)
                escape = true;
            continue;
        }

        if (ch == '"')
        {
            inString = !inString;
            continue;
        }
    }
    return -1;
}

        static bool TryExtractBalancedJson(string text, int start, out int endExclusive, out string json)
        {
            endExclusive = 0;
            json = null;

            var stack = new Stack<char>();
            bool inString = false;
            bool escape = false;

            char open = text[start];
            if (open != '{' && open != '[')
                return false;

            stack.Push(open);

            for (int i = start + 1; i < text.Length; i++)
            {
                char ch = text[i];

                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }
		if (ch == '\\')
                    {
                        escape = true;
                        continue;
                    }
                    if (ch == '"')
                    {
                        inString = false;
                        continue;
                    }
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{' || ch == '[')
                {
                    stack.Push(ch);
                    continue;
                }

                if (ch == '}' || ch == ']')
                {
                    if (stack.Count == 0)
                        return false;

                    char top = stack.Pop();
                    if ((top == '{' && ch != '}') || (top == '[' && ch != ']'))
                        return false;

                    if (stack.Count == 0)
                    {
                        endExclusive = i + 1;
                        json = text.Substring(start, endExclusive - start).Trim();
                        return json.Length > 0;
                    }
                }
            }

            return false;
        }

        static bool TryPrettyPrintJsonCandidate(string json, string kvKey, out string pretty)
        {
            pretty = null;

            // First try strict JSON.
            if (TryPrettyPrintJson(json, out pretty))
                return true;

            // Best-effort "repair" pass for Tuya's non-standard JSON-ish text (unquoted keys/values),
            // Token-quoting repair for Tuya 'JSON-ish' blobs. Applies to any key in enhanced extraction.
            if (TryRepairTuyaJsonish(json, out var repaired) &&
                TryPrettyPrintJson(repaired, out pretty))
            {
                return true;
            }

            return false;
        }

        static bool TryRepairTuyaJsonish(string input, out string repaired)
        {
            repaired = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Don't attempt if it already looks like standard JSON (has any quotes),
            // or if it's missing obvious structure.
            if (input.IndexOf(':') < 0)
                return false;

            if (input.IndexOf('"') >= 0)
                return false;

            if (!input.Contains("{") && !input.Contains("["))
                return false;

            try
            {
                // Token-quoting approach for Tuya 'JSON-ish' blobs:
                // - quote tokens between JSON punctuation
                // - restore JSON literals (true/false/null)
                // - unquote numeric literals (incl. negative/float)
                // - remove trailing commas before } or ]
                string s = input;

                s = Regex.Replace(s, @"([^{}\[\]:,\s]+)", m => "\"" + m.Value + "\"");
                s = Regex.Replace(s, "\"(true|false|null)\"", m => m.Groups[1].Value.ToLowerInvariant(), RegexOptions.IgnoreCase);
                s = Regex.Replace(s, "\"(-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?)\"", "$1");
                s = Regex.Replace(s, @",\s*([}\]])", "$1");

                repaired = s;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryPrettyPrintJson(string json, out string pretty)
        {
            pretty = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

public bool extractKeys()
        {
            var KVs = ParseVault();
            var KVs_Deduped = KVs
                .GroupBy(x => x.Key)
                .Select(g => g.OrderByDescending(x => x.IsCheckSumCorrect).First())
                .GroupBy(x => Convert.ToBase64String(x.Value))
                .Select(g => g.OrderByDescending(x => x.KeyId).First())
                .ToList();


            
                
            byte[] str = KVs_Deduped.FirstOrDefault(x => x.Key == "user_param_key" && x.IsCheckSumCorrect == true)?.Value ??
                KVs_Deduped.FirstOrDefault(x => x.Key == "baud_cfg" && x.IsCheckSumCorrect == true)?.Value;
            byte[] em_sys_env = KVs_Deduped.FirstOrDefault(x => x.Key == "em_sys_env" && x.IsCheckSumCorrect == true)?.Value;
            // old method. Works better when user_param_key is corrupted (bad checksum)
            if(str == null)
            {
                if(KVs.Any(x => x.Key == "user_param_key"))
                    FormMain.Singleton?.addLog("Tuya user_param_key is corrupted, using old extraction method" + Environment.NewLine, System.Drawing.Color.Orange);
                int first_at = 0;
                int keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("user_param_key"));
                //var t = Encoding.ASCII.GetString(descryptedRaw.Where(x => x != 0).ToArray());
                if (keys_at == -1)
                {
                    int jsonAt = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("Jsonver"));
                    if(jsonAt != -1)
                    {
                        keys_at = MiscUtils.findFirstRev(descryptedRaw, (byte)'{', jsonAt);
                    }
                    if (keys_at == -1)
                    {
                        keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("ap_s{"));
                
                        if(keys_at == -1)
                        {
                            keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("baud_cfg"));
                            if(keys_at == -1)
                            {
                                // ln882h hack
                                int jsonInOrig = MiscUtils.indexOf(original, Encoding.ASCII.GetBytes("crc:"));
                                if(jsonInOrig != -1 && original[jsonInOrig + 6] == ',' && original[jsonInOrig + 7] == '}')
                                {
                                    keys_at = jsonInOrig;
                                    descryptedRaw = original;
                                    while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                                        keys_at--;
                                    keys_at--;
                                }
                                // extract at least something
                                if(keys_at == -1)
                                {
                                    keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("gw_bi"));
                                    if(keys_at == -1)
                                    {
                                        // 8266 addition
                                        keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("dev_if_rec_key"));
                                        if(keys_at == -1)
                                        {
                                            FormMain.Singleton?.addLog("Failed to extract Tuya keys - no json start found" + Environment.NewLine, System.Drawing.Color.Orange);
                                            return true;
                                        }
                                    }
                                }
                            }
                            while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                                keys_at++;
                            keys_at++;
                            first_at = keys_at;
                        }
                        else
                        {
                            first_at = keys_at + 5;
                        }
                    }
                    else
                    {
                        first_at = keys_at + 1;
                    }
                }
                else
                {
                    while(descryptedRaw[keys_at] != '{' && keys_at <= descryptedRaw.Length)
                        keys_at++;
                    keys_at++;
                    first_at = keys_at;
                }
                int stopAT = MiscUtils.findMatching(descryptedRaw, (byte)'}', (byte)'{', first_at);
                if (stopAT == -1)
                {
                    //FormMain.Singleton.addLog("Failed to extract Tuya keys - no json end found" + Environment.NewLine, System.Drawing.Color.Purple);
                    // return true;
                    stopAT = descryptedRaw.Length;
                }
                str = MiscUtils.subArray(descryptedRaw, first_at, stopAT - first_at);
            }
            // There is still some kind of Tuya paging here,
            // let's skip it in a quick and dirty way
            string asciiString = bytesToAsciiStr(str);
            string[] pairs = asciiString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < pairs.Length; i++)
            {
                string []kp = pairs[i].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if(kp.Length < 2)
                {
                    FormMain.Singleton?.addLog("Malformed key? " + Environment.NewLine, System.Drawing.Color.Orange);

                    continue;
                }
                string skey = (kp.Length > 2 && kp[1].Contains('[')) ? kp[1] : kp[0];
                string svalue = kp[kp.Length - 1];
                skey = skey.Trim(new char[] { '"' }).Replace("\"", "").Replace("[", "").Replace("{", "");
                svalue = svalue.Trim(new char[] { '"' }).Replace("\"", "").Replace("}", "");
                //parms.Add(skey, svalue);
                if (findKeyValue(skey) == null)
                {
                    parms.Add(skey, svalue);
                }
            }
            if(em_sys_env != null && !isFullOf(em_sys_env, 0x00))
            {
                parms.Add("em_sys_env", bytesToAsciiStr(em_sys_env));
            }
            FormMain.Singleton?.addLog("Tuya keys extraction has found " + parms.Count + " keys" + Environment.NewLine, System.Drawing.Color.Black);

            return false;
        }
        string findKeyContaining(string key)
        {
            foreach(var kv in parms)
            {
                if (kv.Key.Contains(key))
                    return kv.Value;
            }
            return null;
        }
        string findKeyValue(string key)
        {
            if(parms.TryGetValue(key, out var value))
                return value;
            return null;
        }
        bool checkCRC(uint expected, byte [] dat, int ofs, int len)
        {
            uint n = 0;
            for(int i = 0; i < len; i++)
            {
                n += dat[ofs + i];
            }
            n &= 0xFFFFFFFF;
            if (n == expected)
                return true;
            return false;
        }
        string bytesToAsciiStr(byte[] data)
        {
            var asciiString = "";
            for(int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b < 32)
                    continue;
                if (b > 127)
                    continue;
                char ch = (char)b;
                asciiString += ch;
            }
            return asciiString;
        }
    }
}