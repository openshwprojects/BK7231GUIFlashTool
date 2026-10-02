using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BK7231Flasher
{
    public static class TuyaConfigCorpusInventory
    {
        sealed class InventoryEntry
        {
            public string RelativePath { get; set; }
            public string Sha256 { get; set; }
            public long FileLength { get; set; }
            public bool ExtractionSucceeded { get; set; }
            public bool ClassicExtractionSucceeded { get; set; }
            public string Error { get; set; }
            public string ExtractionKind { get; set; }
            public string VaultKeyVariant { get; set; }
            public string ParserKind { get; set; }
            public string PageMagic { get; set; }
            public int DeviceKeyOffset { get; set; }
            public int ArenaOffset { get; set; }
            public int DecryptedLength { get; set; }
            public int SectorCount { get; set; }
            public int EntryCount { get; set; }
            public int LargestValueLength { get; set; }
            public bool HasDuplicateBlockIds { get; set; }
            public int BadCrcCount { get; set; }
            public int EnhancedJsonLength { get; set; }
            public int HumanReadableLength { get; set; }
            public string[] CredentialIndicators { get; set; }
        }

        public static int Run(string inputDirectory, string outputFile)
        {
            if(!Directory.Exists(inputDirectory))
            {
                Console.Error.WriteLine($"Error: Inventory directory not found: {inputDirectory}");
                return 1;
            }

            string root = Path.GetFullPath(inputDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(IsCandidateDump)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"Scanning {files.Count} binary dump(s) under {root}");
            var results = new List<InventoryEntry>(files.Count);

            for(int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                var entry = new InventoryEntry
                {
                    RelativePath = GetRelativePath(root, file),
                    FileLength = new FileInfo(file).Length,
                    CredentialIndicators = Array.Empty<string>()
                };

                try
                {
                    entry.Sha256 = GetSha256(file);

                    var tc = new TuyaConfig
                    {
                        WriteDebugArtifacts = false
                    };

                    entry.ExtractionSucceeded = tc.fromFile(file) == false;
                    if(entry.ExtractionSucceeded)
                    {
                        entry.ClassicExtractionSucceeded = tc.extractKeys() == false;
                        string enhanced = tc.getEnhancedExtractionText() ?? "";
                        string human = tc.getKeysHumanReadableEnhanced() ?? "";
                        entry.EnhancedJsonLength = enhanced.Length;
                        entry.HumanReadableLength = human.Length;
                        entry.CredentialIndicators = GetCredentialIndicators(enhanced);

                        var diagnostics = tc.AnalyzeExtractionDiagnostics();
                        entry.ExtractionKind = diagnostics.ExtractionKind;
                        entry.VaultKeyVariant = diagnostics.VaultKeyVariant;
                        entry.ParserKind = diagnostics.ParserKind;
                        entry.PageMagic = diagnostics.PageMagic == 0 ? "" : $"0x{diagnostics.PageMagic:X8}";
                        entry.DeviceKeyOffset = diagnostics.DeviceKeyOffset;
                        entry.ArenaOffset = diagnostics.ArenaOffset;
                        entry.DecryptedLength = diagnostics.DecryptedLength;
                        entry.SectorCount = diagnostics.SectorCount;
                        entry.EntryCount = diagnostics.EntryCount;
                        entry.LargestValueLength = diagnostics.LargestValueLength;
                        entry.HasDuplicateBlockIds = diagnostics.HasDuplicateBlockIds;
                        entry.BadCrcCount = diagnostics.BadCrcCount;
                    }
                }
                catch(Exception ex)
                {
                    entry.Error = ex.Message;
                }

                results.Add(entry);
                string status = entry.ExtractionSucceeded
                    ? $"{entry.ExtractionKind}/{entry.VaultKeyVariant}/{entry.ParserKind}"
                    : "not extracted";
                Console.WriteLine($"[{i + 1}/{files.Count}] {entry.RelativePath}: {status}");
            }

            string outputPath = Path.GetFullPath(outputFile);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if(!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string json = JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(outputPath, json + Environment.NewLine, new UTF8Encoding(false));
            Console.WriteLine($"Inventory written to {outputPath}");
            return 0;
        }

        static bool IsCandidateDump(string path)
        {
            string extension = Path.GetExtension(path);
            if(!string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".dump", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".img", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".fls", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        static string GetRelativePath(string root, string path)
        {
            if(path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        static string GetSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static string[] GetCredentialIndicators(string json)
        {
            var indicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var document = JsonDocument.Parse(json);
                InspectJson(document.RootElement, "$", indicators);
            }
            catch
            {
                indicators.Add("json-not-parseable");
                foreach(Match match in Regex.Matches(json ?? "", "\\\"(?<name>ssid|passwd|password|pwd|ap_info|ap_info_v2)\\\"\\s*:\\s*(?<value>null|\\\"(?:\\\\.|[^\\\"])*\\\")", RegexOptions.IgnoreCase))
                {
                    string value = match.Groups["value"].Value;
                    if(string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "\"\"")
                        continue;
                    indicators.Add("$." + match.Groups["name"].Value + " (unparsed JSON)");
                }
            }
            return indicators.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        static void InspectJson(JsonElement element, string path, HashSet<string> indicators)
        {
            if(element.ValueKind == JsonValueKind.Object)
            {
                foreach(var property in element.EnumerateObject())
                {
                    string propertyPath = path + "." + property.Name;
                    if(IsCredentialProperty(property.Name) && HasValue(property.Value))
                        indicators.Add(propertyPath);
                    if(IsEncodedNetworkRecord(property.Name) && HasValue(property.Value))
                        indicators.Add(propertyPath + " (encoded network record)");
                    InspectJson(property.Value, propertyPath, indicators);
                }
            }
            else if(element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach(var item in element.EnumerateArray())
                {
                    InspectJson(item, path + "[" + index + "]", indicators);
                    index++;
                }
            }
            else if(element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString();
                if(TryDecodeBase64(value, out string decoded) && ContainsCredentialMarker(decoded))
                    indicators.Add(path + " (base64 credential marker)");
            }
        }

        static bool IsCredentialProperty(string name)
        {
            return string.Equals(name, "ssid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "passwd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "password", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "pwd", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsEncodedNetworkRecord(string name)
        {
            return string.Equals(name, "ap_info", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "ap_info_v2", StringComparison.OrdinalIgnoreCase);
        }

        static bool HasValue(JsonElement element)
        {
            if(element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return false;
            if(element.ValueKind == JsonValueKind.String)
                return !string.IsNullOrWhiteSpace(element.GetString());
            if(element.ValueKind == JsonValueKind.Array)
                return element.GetArrayLength() > 0;
            if(element.ValueKind == JsonValueKind.Object)
                return element.EnumerateObject().Any();
            return true;
        }

        static bool TryDecodeBase64(string value, out string decoded)
        {
            decoded = null;
            if(string.IsNullOrWhiteSpace(value) || value.Length < 8 || (value.Length % 4) != 0)
                return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                decoded = Encoding.UTF8.GetString(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool ContainsCredentialMarker(string value)
        {
            if(string.IsNullOrEmpty(value))
                return false;
            return value.IndexOf("ssid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("passwd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
