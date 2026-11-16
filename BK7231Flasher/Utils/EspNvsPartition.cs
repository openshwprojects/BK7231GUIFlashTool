// EspNvsPartition.cs
// ESP-IDF NVS (Non-Volatile Storage) partition parser and generator.
// Format reference: ESP-IDF nvs_flash "Internals" documentation and Espressif's
// nvs_partition_gen.py (package esp-idf-nvs-partition-gen 0.3.0). Generated images were
// verified byte-identical to nvs_partition_gen.py output (NVS version 2, multi-page
// blob support) for a single namespace containing a single blob (3584 B and 9000 B cases).
// Parser verified against a real OpenBeken ESP32 (ESP32-WROOM-32) flash dump.
// Self-contained: does not depend on or modify any existing flasher code.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace BK7231Flasher
{
    public enum EspNvsItemType : byte
    {
        U8 = 0x01,
        I8 = 0x11,
        U16 = 0x02,
        I16 = 0x12,
        U32 = 0x04,
        I32 = 0x14,
        U64 = 0x08,
        I64 = 0x18,
        Str = 0x21,
        BlobV1 = 0x41,      // legacy single-page blob (NVS version 1)
        BlobData = 0x42,    // blob chunk (NVS version 2)
        BlobIndex = 0x48,   // blob index (NVS version 2)
        Any = 0xFF
    }

    public enum EspNvsPageState : uint
    {
        Empty = 0xFFFFFFFF,
        Active = 0xFFFFFFFE,
        Full = 0xFFFFFFFC,
        Freeing = 0xFFFFFFF8,
        Corrupt = 0xFFFFFFF0,
        Invalid = 0x00000000
    }

    /// <summary>One parsed NVS entry. Immutable.</summary>
    public sealed class EspNvsEntry
    {
        public int PageIndex { get; }
        public int EntryIndex { get; }
        public byte NamespaceIndex { get; }
        public EspNvsItemType Type { get; }
        public int Span { get; }
        public byte ChunkIndex { get; }
        public string Key { get; }
        public bool IsErased { get; }
        public bool EntryCrcValid { get; }
        /// <summary>True for fixed-size types; for variable-length types, true only if the payload CRC matches.</summary>
        public bool DataCrcValid { get; }
        private readonly byte[] rawData;       // 8-byte data field
        private readonly byte[] payload;       // variable-length payload (empty for fixed types)

        internal EspNvsEntry(int pageIndex, int entryIndex, byte namespaceIndex, EspNvsItemType type, int span,
            byte chunkIndex, string key, bool isErased, bool entryCrcValid, bool dataCrcValid, byte[] rawData, byte[] payload)
        {
            PageIndex = pageIndex;
            EntryIndex = entryIndex;
            NamespaceIndex = namespaceIndex;
            Type = type;
            Span = span;
            ChunkIndex = chunkIndex;
            Key = key;
            IsErased = isErased;
            EntryCrcValid = entryCrcValid;
            DataCrcValid = dataCrcValid;
            this.rawData = rawData;
            this.payload = payload;
        }

        public bool IsUsable => !IsErased && EntryCrcValid && DataCrcValid;

        public byte[] GetRawData() => (byte[])rawData.Clone();

        public byte[] GetPayload() => (byte[])payload.Clone();

        /// <summary>Little-endian integer value of a fixed-size entry (U8..I64).</summary>
        public ulong GetUnsignedValue()
        {
            int size = (byte)Type & 0x0F;
            ulong value = 0;
            for (int i = 0; i < size && i < 8; i++)
                value |= (ulong)rawData[i] << (8 * i);
            return value;
        }

        /// <summary>Blob index fields (only meaningful when Type == BlobIndex).</summary>
        internal uint BlobIndexTotalSize => BitConverter.ToUInt32(rawData, 0);
        internal byte BlobIndexChunkCount => rawData[4];
        internal byte BlobIndexChunkStart => rawData[5];
    }

    public sealed class EspNvsPageInfo
    {
        public int PageIndex { get; }
        public EspNvsPageState State { get; }
        public uint SequenceNumber { get; }
        public byte Version { get; }
        public bool HeaderCrcValid { get; }

        internal EspNvsPageInfo(int pageIndex, EspNvsPageState state, uint sequenceNumber, byte version, bool headerCrcValid)
        {
            PageIndex = pageIndex;
            State = state;
            SequenceNumber = sequenceNumber;
            Version = version;
            HeaderCrcValid = headerCrcValid;
        }
    }

    public sealed class EspNvsParseResult
    {
        public bool Success { get; }
        public string Error { get; }
        public IReadOnlyList<EspNvsPageInfo> Pages { get; }
        public IReadOnlyList<EspNvsEntry> Entries { get; }
        /// <summary>Namespace index -> namespace name (live entries only).</summary>
        public IReadOnlyDictionary<byte, string> Namespaces { get; }

        private EspNvsParseResult(bool success, string error, IReadOnlyList<EspNvsPageInfo> pages,
            IReadOnlyList<EspNvsEntry> entries, IReadOnlyDictionary<byte, string> namespaces)
        {
            Success = success;
            Error = error;
            Pages = pages;
            Entries = entries;
            Namespaces = namespaces;
        }

        internal static EspNvsParseResult Ok(List<EspNvsPageInfo> pages, List<EspNvsEntry> entries, Dictionary<byte, string> namespaces)
            => new EspNvsParseResult(true, string.Empty, pages, entries, namespaces);

        internal static EspNvsParseResult Fail(string error)
            => new EspNvsParseResult(false, error, new List<EspNvsPageInfo>(), new List<EspNvsEntry>(), new Dictionary<byte, string>());

        /// <summary>Returns the namespace index for a name, or -1 if not present.</summary>
        public int FindNamespaceIndex(string namespaceName)
        {
            foreach (var kv in Namespaces)
            {
                if (kv.Value == namespaceName)
                    return kv.Key;
            }
            return -1;
        }
    }

    public sealed class EspNvsBlobResult
    {
        public bool Success { get; }
        public string Error { get; }
        private readonly byte[] data;

        private EspNvsBlobResult(bool success, string error, byte[] data)
        {
            Success = success;
            Error = error;
            this.data = data;
        }

        public byte[] GetData() => (byte[])data.Clone();

        internal static EspNvsBlobResult Ok(byte[] data) => new EspNvsBlobResult(true, string.Empty, data);
        internal static EspNvsBlobResult Fail(string error) => new EspNvsBlobResult(false, error, Array.Empty<byte>());
    }

    public sealed class EspNvsBuildResult
    {
        public bool Success { get; }
        public string Error { get; }
        private readonly byte[] image;

        private EspNvsBuildResult(bool success, string error, byte[] image)
        {
            Success = success;
            Error = error;
            this.image = image;
        }

        public byte[] GetImage() => (byte[])image.Clone();

        internal static EspNvsBuildResult Ok(byte[] image) => new EspNvsBuildResult(true, string.Empty, image);
        internal static EspNvsBuildResult Fail(string error) => new EspNvsBuildResult(false, error, Array.Empty<byte>());
    }

    public static class EspNvsPartition
    {
        public const int PageSize = 4096;
        public const int EntrySize = 32;
        public const int EntriesPerPage = 126;
        public const int PageHeaderSize = 32;
        public const int BitmapSize = 32;
        public const int FirstEntryOffset = PageHeaderSize + BitmapSize;   // 64
        public const byte PageVersion2 = 0xFE;   // multi-page blob support
        public const byte PageVersion1 = 0xFF;
        public const int MaxKeyLength = 15;      // 16-byte field, null-terminated
        public const byte ChunkIndexAny = 0xFF;

        // Entry state bitmap values (2 bits per entry)
        private const int BitmapEmpty = 3;
        private const int BitmapWritten = 2;
        private const int BitmapErased = 0;

        #region Parsing

        /// <summary>Parses an NVS partition image (whole partition, multiple of 4096 bytes).</summary>
        public static EspNvsParseResult Parse(byte[] image, int offset, int length)
        {
            if (image == null)
                return EspNvsParseResult.Fail("Image is null.");
            if (offset < 0 || length <= 0 || offset + length > image.Length)
                return EspNvsParseResult.Fail("Offset/length out of range.");
            if (length % PageSize != 0)
                return EspNvsParseResult.Fail("NVS partition length must be a multiple of 4096 bytes.");

            var pages = new List<EspNvsPageInfo>();
            var entries = new List<EspNvsEntry>();
            var namespaces = new Dictionary<byte, string>();
            int pageCount = length / PageSize;

            for (int p = 0; p < pageCount; p++)
            {
                int pageBase = offset + p * PageSize;
                uint stateRaw = BitConverter.ToUInt32(image, pageBase);
                uint seq = BitConverter.ToUInt32(image, pageBase + 4);
                byte version = image[pageBase + 8];
                uint storedHeaderCrc = BitConverter.ToUInt32(image, pageBase + 28);
                bool headerCrcValid = storedHeaderCrc == Crc32(image, pageBase + 4, 24);
                EspNvsPageState state = ToPageState(stateRaw);
                pages.Add(new EspNvsPageInfo(p, state, seq, version, headerCrcValid));

                // Only ACTIVE / FULL / FREEING pages carry readable entries
                if (state != EspNvsPageState.Active && state != EspNvsPageState.Full && state != EspNvsPageState.Freeing)
                    continue;

                int i = 0;
                while (i < EntriesPerPage)
                {
                    int bm = GetBitmapState(image, pageBase, i);
                    if (bm == BitmapEmpty)
                    {
                        i++;
                        continue;
                    }

                    int e = pageBase + FirstEntryOffset + i * EntrySize;
                    byte ns = image[e];
                    var type = (EspNvsItemType)image[e + 1];
                    int span = image[e + 2];
                    byte chunk = image[e + 3];
                    uint storedEntryCrc = BitConverter.ToUInt32(image, e + 4);
                    bool entryCrcValid = storedEntryCrc == EntryCrc(image, e);
                    string key = ReadKey(image, e + 8);
                    var raw = new byte[8];
                    Buffer.BlockCopy(image, e + 24, raw, 0, 8);

                    if (span < 1 || i + span > EntriesPerPage)
                    {
                        // Corrupt span: record as single invalid entry and move on
                        entries.Add(new EspNvsEntry(p, i, ns, type, 1, chunk, key, bm == BitmapErased, false, false, raw, Array.Empty<byte>()));
                        i++;
                        continue;
                    }

                    byte[] payload = Array.Empty<byte>();
                    bool dataCrcValid = true;
                    if (IsVariableLength(type))
                    {
                        int size = BitConverter.ToUInt16(raw, 0);
                        uint storedDataCrc = BitConverter.ToUInt32(raw, 4);
                        int maxPayload = (span - 1) * EntrySize;
                        if (size <= maxPayload)
                        {
                            payload = new byte[size];
                            Buffer.BlockCopy(image, e + EntrySize, payload, 0, size);
                            dataCrcValid = storedDataCrc == Crc32(payload, 0, size);
                        }
                        else
                        {
                            dataCrcValid = false;
                        }
                    }

                    bool erased = bm == BitmapErased;
                    var entry = new EspNvsEntry(p, i, ns, type, span, chunk, key, erased, entryCrcValid, dataCrcValid, raw, payload);
                    entries.Add(entry);

                    // Namespace definitions live in namespace 0 as U8 entries
                    if (ns == 0 && type == EspNvsItemType.U8 && entry.IsUsable)
                        namespaces[raw[0]] = key;

                    i += span;
                }
            }

            return EspNvsParseResult.Ok(pages, entries, namespaces);
        }

        /// <summary>
        /// Reads a blob by namespace/key. Supports NVS v2 multi-page blobs (BlobIndex + BlobData)
        /// and legacy v1 blobs (BlobV1). Only live entries with valid CRCs are used.
        /// </summary>
        public static EspNvsBlobResult ReadBlob(EspNvsParseResult parsed, string namespaceName, string key)
        {
            if (parsed == null || !parsed.Success)
                return EspNvsBlobResult.Fail("Parse result is not valid.");

            int nsIndex = parsed.FindNamespaceIndex(namespaceName);
            if (nsIndex < 0)
                return EspNvsBlobResult.Fail("Namespace '" + namespaceName + "' not found.");

            // v2: locate the live blob index
            EspNvsEntry? index = null;
            foreach (var en in parsed.Entries)
            {
                if (en.NamespaceIndex == nsIndex && en.Type == EspNvsItemType.BlobIndex && en.Key == key && en.IsUsable)
                {
                    if (index != null)
                        return EspNvsBlobResult.Fail("More than one live blob index for '" + key + "'.");
                    index = en;
                }
            }

            if (index != null)
            {
                uint total = index.BlobIndexTotalSize;
                int count = index.BlobIndexChunkCount;
                int start = index.BlobIndexChunkStart;
                var result = new byte[total];
                int written = 0;
                for (int c = 0; c < count; c++)
                {
                    int chunkIndex = start + c;
                    EspNvsEntry? chunk = null;
                    foreach (var en in parsed.Entries)
                    {
                        if (en.NamespaceIndex == nsIndex && en.Type == EspNvsItemType.BlobData && en.Key == key
                            && en.ChunkIndex == chunkIndex && en.IsUsable)
                        {
                            chunk = en;
                            break;
                        }
                    }
                    if (chunk == null)
                        return EspNvsBlobResult.Fail("Blob chunk " + chunkIndex + " for '" + key + "' missing or invalid.");

                    byte[] part = chunk.GetPayload();
                    if (written + part.Length > total)
                        return EspNvsBlobResult.Fail("Blob chunks exceed declared size for '" + key + "'.");
                    Buffer.BlockCopy(part, 0, result, written, part.Length);
                    written += part.Length;
                }
                if (written != total)
                    return EspNvsBlobResult.Fail("Blob size mismatch for '" + key + "': got " + written + ", expected " + total + ".");
                return EspNvsBlobResult.Ok(result);
            }

            // v1 fallback
            foreach (var en in parsed.Entries)
            {
                if (en.NamespaceIndex == nsIndex && en.Type == EspNvsItemType.BlobV1 && en.Key == key && en.IsUsable)
                    return EspNvsBlobResult.Ok(en.GetPayload());
            }

            return EspNvsBlobResult.Fail("Blob '" + key + "' not found in namespace '" + namespaceName + "'.");
        }

        #endregion

        #region Generation

        /// <summary>
        /// Builds a complete NVS v2 partition image containing one namespace with one blob.
        /// Remaining space is left erased (0xFF). Layout matches nvs_partition_gen.py.
        /// </summary>
        public static EspNvsBuildResult BuildSingleBlobImage(string namespaceName, string key, byte[] data, int partitionSize)
        {
            if (data == null)
                return EspNvsBuildResult.Fail("Data is null.");
            if (!IsValidKey(namespaceName))
                return EspNvsBuildResult.Fail("Invalid namespace name (1-15 ASCII chars).");
            if (!IsValidKey(key))
                return EspNvsBuildResult.Fail("Invalid key (1-15 ASCII chars).");
            if (partitionSize < 3 * PageSize || partitionSize % PageSize != 0)
                return EspNvsBuildResult.Fail("Partition size must be a multiple of 4096 and at least 3 pages.");

            var image = new byte[partitionSize];
            for (int i = 0; i < image.Length; i++)
                image[i] = 0xFF;

            var writer = new PageWriter(image);
            const byte nsIndex = 1;

            // Namespace entry: ns 0, U8, value = namespace index
            var nsRaw = FilledBytes(8, 0xFF);
            nsRaw[0] = nsIndex;
            if (!writer.WriteEntry(0, EspNvsItemType.U8, 1, ChunkIndexAny, namespaceName, nsRaw, null, out string err))
                return EspNvsBuildResult.Fail(err);

            // Blob data chunks
            int remaining = data.Length;
            int dataOffset = 0;
            byte chunkIndex = 0;
            int chunkCount = 0;
            do
            {
                int freeEntries = writer.FreeEntriesInPage;
                // Need room for header + at least one data entry; keep one entry free for the index if this is the last chunk
                if (freeEntries < 2)
                {
                    if (!writer.NextPage(out err))
                        return EspNvsBuildResult.Fail(err);
                    freeEntries = writer.FreeEntriesInPage;
                }

                int maxChunkBytes = (freeEntries - 1) * EntrySize;
                int chunkBytes = Math.Min(remaining, maxChunkBytes);
                if (chunkBytes == 0 && remaining > 0)
                    return EspNvsBuildResult.Fail("Unable to place blob chunk.");

                var chunk = new byte[chunkBytes];
                Buffer.BlockCopy(data, dataOffset, chunk, 0, chunkBytes);
                var raw = VariableLengthRaw(chunk);
                int span = 1 + (chunkBytes + EntrySize - 1) / EntrySize;
                if (!writer.WriteEntry(nsIndex, EspNvsItemType.BlobData, span, chunkIndex, key, raw, chunk, out err))
                    return EspNvsBuildResult.Fail(err);

                dataOffset += chunkBytes;
                remaining -= chunkBytes;
                chunkIndex++;
                chunkCount++;
                if (chunkCount > 127)
                    return EspNvsBuildResult.Fail("Blob too large.");
            } while (remaining > 0);

            // Blob index
            if (writer.FreeEntriesInPage < 1 && !writer.NextPage(out err))
                return EspNvsBuildResult.Fail(err);
            var idxRaw = FilledBytes(8, 0xFF);
            WriteUInt32(idxRaw, 0, (uint)data.Length);
            idxRaw[4] = (byte)chunkCount;
            idxRaw[5] = 0; // chunk start (VER_0)
            if (!writer.WriteEntry(nsIndex, EspNvsItemType.BlobIndex, 1, ChunkIndexAny, key, idxRaw, null, out err))
                return EspNvsBuildResult.Fail(err);

            writer.Finish();
            return EspNvsBuildResult.Ok(image);
        }

        /// <summary>Writes entries sequentially into pages, maintaining headers and bitmaps.</summary>
        private sealed class PageWriter
        {
            private readonly byte[] image;
            private readonly int pageCount;
            private int pageIndex;
            private int entryIndex;

            public PageWriter(byte[] image)
            {
                this.image = image;
                pageCount = image.Length / PageSize;
                pageIndex = 0;
                entryIndex = 0;
                WritePageHeader(pageIndex, EspNvsPageState.Active, 0);
            }

            public int FreeEntriesInPage => EntriesPerPage - entryIndex;

            public bool NextPage(out string error)
            {
                // ESP-IDF requires one page to stay empty for garbage collection
                if (pageIndex + 1 >= pageCount - 1)
                {
                    error = "Data does not fit in NVS partition.";
                    return false;
                }
                WritePageHeader(pageIndex, EspNvsPageState.Full, (uint)pageIndex);
                pageIndex++;
                entryIndex = 0;
                WritePageHeader(pageIndex, EspNvsPageState.Active, (uint)pageIndex);
                error = string.Empty;
                return true;
            }

            public bool WriteEntry(byte ns, EspNvsItemType type, int span, byte chunk, string key, byte[] raw8, byte[]? payload, out string error)
            {
                if (span > FreeEntriesInPage)
                {
                    error = "Entry does not fit in page.";
                    return false;
                }
                int pageBase = pageIndex * PageSize;
                int e = pageBase + FirstEntryOffset + entryIndex * EntrySize;

                image[e] = ns;
                image[e + 1] = (byte)type;
                image[e + 2] = (byte)span;
                image[e + 3] = chunk;
                // key: 16 bytes, zero-padded
                for (int k = 0; k < 16; k++)
                    image[e + 8 + k] = 0;
                byte[] keyBytes = Encoding.ASCII.GetBytes(key);
                Buffer.BlockCopy(keyBytes, 0, image, e + 8, keyBytes.Length);
                Buffer.BlockCopy(raw8, 0, image, e + 24, 8);
                WriteUInt32(image, e + 4, EntryCrc(image, e));

                if (payload != null && payload.Length > 0)
                    Buffer.BlockCopy(payload, 0, image, e + EntrySize, payload.Length);   // tail stays 0xFF

                for (int s = 0; s < span; s++)
                    SetBitmapState(image, pageBase, entryIndex + s, BitmapWritten);

                entryIndex += span;
                error = string.Empty;
                return true;
            }

            public void Finish()
            {
                // Current page stays ACTIVE (as nvs_partition_gen.py leaves it)
            }

            private void WritePageHeader(int page, EspNvsPageState state, uint seq)
            {
                int b = page * PageSize;
                WriteUInt32(image, b, (uint)state);
                WriteUInt32(image, b + 4, seq);
                image[b + 8] = PageVersion2;
                for (int i = 9; i < 28; i++)
                    image[b + i] = 0xFF;
                WriteUInt32(image, b + 28, Crc32(image, b + 4, 24));
            }
        }

        #endregion

        #region Helpers

        private static bool IsVariableLength(EspNvsItemType type)
            => type == EspNvsItemType.Str || type == EspNvsItemType.BlobV1 || type == EspNvsItemType.BlobData;

        private static EspNvsPageState ToPageState(uint raw)
        {
            switch (raw)
            {
                case (uint)EspNvsPageState.Empty: return EspNvsPageState.Empty;
                case (uint)EspNvsPageState.Active: return EspNvsPageState.Active;
                case (uint)EspNvsPageState.Full: return EspNvsPageState.Full;
                case (uint)EspNvsPageState.Freeing: return EspNvsPageState.Freeing;
                case (uint)EspNvsPageState.Corrupt: return EspNvsPageState.Corrupt;
                default: return EspNvsPageState.Invalid;
            }
        }

        private static int GetBitmapState(byte[] image, int pageBase, int entry)
            => (image[pageBase + PageHeaderSize + entry / 4] >> ((entry % 4) * 2)) & 0x3;

        private static void SetBitmapState(byte[] image, int pageBase, int entry, int state)
        {
            int idx = pageBase + PageHeaderSize + entry / 4;
            int shift = (entry % 4) * 2;
            image[idx] = (byte)((image[idx] & ~(0x3 << shift)) | ((state & 0x3) << shift));
        }

        private static string ReadKey(byte[] image, int offset)
        {
            int len = 0;
            while (len < 16 && image[offset + len] != 0)
                len++;
            return Encoding.ASCII.GetString(image, offset, len);
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
                return false;
            foreach (char c in key)
            {
                if (c < 0x20 || c > 0x7E)
                    return false;
            }
            return true;
        }

        private static byte[] VariableLengthRaw(byte[] payload)
        {
            var raw = new byte[8];
            raw[0] = (byte)(payload.Length & 0xFF);
            raw[1] = (byte)(payload.Length >> 8);
            raw[2] = 0xFF;  // reserved
            raw[3] = 0xFF;
            WriteUInt32(raw, 4, Crc32(payload, 0, payload.Length));
            return raw;
        }

        private static byte[] FilledBytes(int len, byte value)
        {
            var b = new byte[len];
            for (int i = 0; i < len; i++)
                b[i] = value;
            return b;
        }

        private static void WriteUInt32(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16);
            buf[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>Entry CRC: over bytes 0..3 and 8..31 of the 32-byte entry.</summary>
        private static uint EntryCrc(byte[] image, int entryOffset)
        {
            var tmp = new byte[28];
            Buffer.BlockCopy(image, entryOffset, tmp, 0, 4);
            Buffer.BlockCopy(image, entryOffset + 8, tmp, 4, 24);
            return Crc32(tmp, 0, 28);
        }

        private static readonly uint[] crcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>
        /// CRC32 as used by ESP-IDF NVS: esp_rom_crc32_le(0xFFFFFFFF, ...),
        /// equivalent to Python zlib.crc32(data, 0xFFFFFFFF).
        /// (Reflected 0xEDB88320, initial register 0, final XOR 0xFFFFFFFF.)
        /// </summary>
        internal static uint Crc32(byte[] data, int offset, int length)
        {
            uint c = 0;
            for (int i = 0; i < length; i++)
                c = crcTable[(c ^ data[offset + i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        #endregion
    }
}
