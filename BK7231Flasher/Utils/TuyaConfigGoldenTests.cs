using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BK7231Flasher
{
    public static class TuyaConfigGoldenTests
    {
        const int SectorSize = 4096;
        const string ManifestName = "case.json";
        const string InputName = "input.bin.gz";
        const string ExpectedJsonName = "expected.json";
        const string ExpectedHumanName = "expected-human.txt";

        sealed class GoldenCase
        {
            public string Name { get; set; }
            public string SourceFileName { get; set; }
            public string SourceSha256 { get; set; }
            public string InputSha256 { get; set; }
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
            public string[] CredentialIndicators { get; set; }
        }

        sealed class HarnessMutation
        {
            public string Name { get; set; }
            public Action<string> Apply { get; set; }
        }

        public static int Run(string fixturesDirectory)
        {
            int fixtureResult = RunFixtures(fixturesDirectory, true);
            if(fixtureResult != 0)
                return fixtureResult;

            int lineEndingResult = VerifyLineEndingInvariance(fixturesDirectory);
            if(lineEndingResult != 0)
                return lineEndingResult;
            return VerifyHarnessMutations(fixturesDirectory);
        }

        static int RunFixtures(string fixturesDirectory, bool report)
        {
            if(!Directory.Exists(fixturesDirectory))
            {
                if(report)
                    Console.Error.WriteLine($"Error: Golden fixture directory not found: {fixturesDirectory}");
                return 1;
            }

            var manifests = Directory.EnumerateFiles(fixturesDirectory, ManifestName, SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if(manifests.Count == 0)
            {
                if(report)
                    Console.Error.WriteLine($"Error: No {ManifestName} files found under {fixturesDirectory}");
                return 1;
            }

            int failures = 0;
            foreach(string manifestPath in manifests)
            {
                string caseDirectory = Path.GetDirectoryName(manifestPath);
                try
                {
                    var testCase = JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(manifestPath));
                    var errors = RunCase(caseDirectory, testCase);
                    if(errors.Count == 0)
                    {
                        if(report)
                            Console.WriteLine($"PASS {testCase.Name}");
                    }
                    else
                    {
                        failures++;
                        if(report)
                        {
                            Console.Error.WriteLine($"FAIL {testCase?.Name ?? caseDirectory}");
                            foreach(string error in errors)
                                Console.Error.WriteLine("  " + error);
                        }
                    }
                }
                catch(Exception ex)
                {
                    failures++;
                    if(report)
                        Console.Error.WriteLine($"FAIL {caseDirectory}: {ex.Message}");
                }
            }

            if(report)
                Console.WriteLine($"Tuya config golden tests: {manifests.Count - failures} passed, {failures} failed.");
            return failures == 0 ? 0 : 1;
        }

        static int VerifyHarnessMutations(string fixturesDirectory)
        {
            string sourceDirectory = GetRepresentativeFixtureDirectory(fixturesDirectory);

            var mutations = new List<HarnessMutation>
            {
                TextMutation("truncated LF enhanced JSON", ExpectedJsonName,
                    value => RemoveLastContentCharacter(ConvertLineEndings(value, "\n"))),
                TextMutation("truncated CRLF enhanced JSON", ExpectedJsonName,
                    value => RemoveLastContentCharacter(ConvertLineEndings(value, "\r\n"))),
                TextMutation("appended enhanced JSON", ExpectedJsonName, value => value + "intentional mutation"),
                TextMutation("truncated LF human-readable output", ExpectedHumanName,
                    value => RemoveLastContentCharacter(ConvertLineEndings(value, "\n"))),
                TextMutation("truncated CRLF human-readable output", ExpectedHumanName,
                    value => RemoveLastContentCharacter(ConvertLineEndings(value, "\r\n"))),
                TextMutation("appended human-readable output", ExpectedHumanName, value => value + "intentional mutation"),
                ManifestMutation("wrong input hash", item => item.InputSha256 = new string('0', 64)),
                ManifestMutation("wrong extraction kind", item => item.ExtractionKind += "-mutation"),
                ManifestMutation("wrong vault key variant", item => item.VaultKeyVariant += "-mutation"),
                ManifestMutation("wrong parser kind", item => item.ParserKind += "-mutation"),
                ManifestMutation("wrong page magic", item => item.PageMagic = "0xDEADBEEF"),
                ManifestMutation("wrong device-key offset", item => item.DeviceKeyOffset++),
                ManifestMutation("wrong arena offset", item => item.ArenaOffset++),
                ManifestMutation("wrong decrypted length", item => item.DecryptedLength++),
                ManifestMutation("wrong sector count", item => item.SectorCount++),
                ManifestMutation("wrong entry count", item => item.EntryCount++),
                ManifestMutation("wrong largest value length", item => item.LargestValueLength++),
                ManifestMutation("wrong duplicate-block state", item => item.HasDuplicateBlockIds = !item.HasDuplicateBlockIds),
                ManifestMutation("wrong bad-CRC count", item => item.BadCrcCount++),
                ManifestMutation("wrong credential indicators", item => item.CredentialIndicators = new[] { "intentional mutation" }),
                new HarnessMutation { Name = "changed decompressed input", Apply = MutateInputPayload },
                new HarnessMutation { Name = "invalid gzip input", Apply = CorruptGzipHeader },
                new HarnessMutation { Name = "missing input", Apply = path => File.Delete(Path.Combine(path, InputName)) },
                new HarnessMutation { Name = "missing enhanced JSON", Apply = path => File.Delete(Path.Combine(path, ExpectedJsonName)) },
                new HarnessMutation { Name = "missing human-readable output", Apply = path => File.Delete(Path.Combine(path, ExpectedHumanName)) },
                new HarnessMutation { Name = "malformed manifest", Apply = path => File.WriteAllText(Path.Combine(path, ManifestName), "{") },
                new HarnessMutation { Name = "missing manifest", Apply = path => File.Delete(Path.Combine(path, ManifestName)) }
            };

            int missed = 0;
            foreach(var mutation in mutations)
            {
                string temporaryRoot = Path.Combine(Path.GetTempPath(), "tuya-golden-mutation-" + Guid.NewGuid().ToString("N"));
                string temporaryCase = Path.Combine(temporaryRoot, "case");
                try
                {
                    CopyFixtureFiles(sourceDirectory, temporaryCase);

                    mutation.Apply(temporaryCase);
                    if(RunFixtures(temporaryRoot, false) == 0)
                    {
                        missed++;
                        Console.Error.WriteLine($"MUTATION MISSED: {mutation.Name}");
                    }
                }
                catch(Exception ex)
                {
                    missed++;
                    Console.Error.WriteLine($"MUTATION ERROR: {mutation.Name}: {ex.Message}");
                }
                finally
                {
                    if(Directory.Exists(temporaryRoot))
                        Directory.Delete(temporaryRoot, true);
                }
            }

            Console.WriteLine($"Golden harness mutation tests: {mutations.Count - missed} detected, {missed} missed.");
            return missed == 0 ? 0 : 1;
        }

        static int VerifyLineEndingInvariance(string fixturesDirectory)
        {
            string sourceDirectory = GetRepresentativeFixtureDirectory(fixturesDirectory);
            var representations = new[]
            {
                new { Name = "LF", Newline = "\n" },
                new { Name = "CRLF", Newline = "\r\n" }
            };
            int failed = 0;

            foreach(var representation in representations)
            {
                string temporaryRoot = Path.Combine(Path.GetTempPath(), "tuya-golden-eol-" + Guid.NewGuid().ToString("N"));
                string temporaryCase = Path.Combine(temporaryRoot, "case");
                try
                {
                    CopyFixtureFiles(sourceDirectory, temporaryCase);
                    foreach(string fileName in new[] { ExpectedJsonName, ExpectedHumanName })
                    {
                        string path = Path.Combine(temporaryCase, fileName);
                        File.WriteAllText(path, ConvertLineEndings(File.ReadAllText(path), representation.Newline),
                            new UTF8Encoding(false));
                    }

                    if(RunFixtures(temporaryRoot, false) != 0)
                    {
                        failed++;
                        Console.Error.WriteLine($"LINE ENDING FAILURE: valid {representation.Name} fixture was rejected.");
                    }
                }
                finally
                {
                    if(Directory.Exists(temporaryRoot))
                        Directory.Delete(temporaryRoot, true);
                }
            }

            Console.WriteLine($"Golden line-ending tests: {representations.Length - failed} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }

        static string GetRepresentativeFixtureDirectory(string fixturesDirectory)
        {
            string sourceManifest = Directory.EnumerateFiles(fixturesDirectory, ManifestName, SearchOption.AllDirectories)
                .FirstOrDefault(path =>
                {
                    var item = JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(path));
                    return item.ExtractionKind == "Vault" && (item.CredentialIndicators?.Length ?? 0) > 0;
                }) ?? Directory.EnumerateFiles(fixturesDirectory, ManifestName, SearchOption.AllDirectories).First();
            return Path.GetDirectoryName(sourceManifest);
        }

        static void CopyFixtureFiles(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);
            foreach(string sourceFile in Directory.EnumerateFiles(sourceDirectory))
                File.Copy(sourceFile, Path.Combine(targetDirectory, Path.GetFileName(sourceFile)));
        }

        static HarnessMutation TextMutation(string name, string fileName, Func<string, string> mutate)
        {
            return new HarnessMutation
            {
                Name = name,
                Apply = path =>
                {
                    string file = Path.Combine(path, fileName);
                    File.WriteAllText(file, mutate(File.ReadAllText(file)), new UTF8Encoding(false));
                }
            };
        }

        static HarnessMutation ManifestMutation(string name, Action<GoldenCase> mutate)
        {
            return new HarnessMutation
            {
                Name = name,
                Apply = path =>
                {
                    string file = Path.Combine(path, ManifestName);
                    var item = JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(file));
                    mutate(item);
                    File.WriteAllText(file, JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                }
            };
        }

        static string RemoveLastContentCharacter(string value)
        {
            if(string.IsNullOrEmpty(value))
                return "intentional mutation";

            int index = value.Length - 1;
            while(index >= 0 && (value[index] == '\r' || value[index] == '\n'))
                index--;

            return index < 0 ? value + "intentional mutation" : value.Remove(index, 1);
        }

        static string ConvertLineEndings(string value, string newline)
        {
            return NormalizeNewlines(value).Replace("\n", newline);
        }

        static void MutateInputPayload(string path)
        {
            string inputPath = Path.Combine(path, InputName);
            byte[] input = ReadGzip(inputPath);
            int index = Array.FindIndex(input, value => value != 0xFF);
            if(index < 0)
                index = 0;
            input[index] ^= 0x01;
            WriteGzip(inputPath, input);
        }

        static void CorruptGzipHeader(string path)
        {
            string inputPath = Path.Combine(path, InputName);
            byte[] compressed = File.ReadAllBytes(inputPath);
            compressed[0] ^= 0xFF;
            File.WriteAllBytes(inputPath, compressed);
        }

        public static int CreateFixture(string sourceFile, string caseDirectory)
        {
            if(!File.Exists(sourceFile))
            {
                Console.Error.WriteLine($"Error: Source dump not found: {sourceFile}");
                return 1;
            }
            if(Directory.Exists(caseDirectory) && Directory.EnumerateFileSystemEntries(caseDirectory).Any())
            {
                Console.Error.WriteLine($"Error: Fixture directory is not empty: {caseDirectory}");
                return 1;
            }

            try
            {
                var source = Extract(sourceFile);
                byte[] original = File.ReadAllBytes(sourceFile);
                byte[] reduced = CreateReducedInput(original, source.Diagnostics);

                var replay = Extract(reduced);
                AssertEquivalentSource(source, replay);

                Directory.CreateDirectory(caseDirectory);
                string inputPath = Path.Combine(caseDirectory, InputName);
                WriteGzip(inputPath, reduced);
                File.WriteAllText(Path.Combine(caseDirectory, ExpectedJsonName), NormalizeNewlines(source.EnhancedJson), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(caseDirectory, ExpectedHumanName), NormalizeNewlines(source.HumanReadable), new UTF8Encoding(false));

                var diagnostics = replay.Diagnostics;
                var manifest = new GoldenCase
                {
                    Name = new DirectoryInfo(caseDirectory).Name,
                    SourceFileName = Path.GetFileName(sourceFile),
                    SourceSha256 = GetSha256(sourceFile),
                    InputSha256 = GetSha256(reduced),
                    ExtractionKind = diagnostics.ExtractionKind,
                    VaultKeyVariant = diagnostics.VaultKeyVariant,
                    ParserKind = diagnostics.ParserKind,
                    PageMagic = diagnostics.PageMagic == 0 ? "" : $"0x{diagnostics.PageMagic:X8}",
                    DeviceKeyOffset = diagnostics.DeviceKeyOffset,
                    ArenaOffset = diagnostics.ArenaOffset,
                    DecryptedLength = diagnostics.DecryptedLength,
                    SectorCount = diagnostics.SectorCount,
                    EntryCount = diagnostics.EntryCount,
                    LargestValueLength = diagnostics.LargestValueLength,
                    HasDuplicateBlockIds = diagnostics.HasDuplicateBlockIds,
                    BadCrcCount = diagnostics.BadCrcCount,
                    CredentialIndicators = TuyaConfigCorpusInventory.GetCredentialIndicators(source.EnhancedJson)
                };
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(caseDirectory, ManifestName), manifestJson + Environment.NewLine, new UTF8Encoding(false));
                Console.WriteLine($"Created fixture {manifest.Name} from {sourceFile}");
                return 0;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        sealed class ExtractionResult
        {
            public string EnhancedJson;
            public string HumanReadable;
            public TuyaConfig.ExtractionDiagnostics Diagnostics;
        }

        static ExtractionResult Extract(string file)
        {
            return Extract(File.ReadAllBytes(file));
        }

        static ExtractionResult Extract(byte[] data)
        {
            var tc = new TuyaConfig
            {
                WriteDebugArtifacts = false
            };
            if(tc.fromBytes(data) != false)
                throw new InvalidOperationException("Tuya config decryption failed.");

            bool classicFailed = tc.extractKeys();
            if(classicFailed && !tc.hasEnhancedExtractionData())
                throw new InvalidOperationException("Tuya key extraction failed.");

            return new ExtractionResult
            {
                EnhancedJson = tc.getEnhancedExtractionText() ?? "",
                HumanReadable = tc.getKeysHumanReadableEnhanced() ?? "",
                Diagnostics = tc.AnalyzeExtractionDiagnostics()
            };
        }

        static List<string> RunCase(string caseDirectory, GoldenCase testCase)
        {
            var errors = new List<string>();
            if(testCase == null)
            {
                errors.Add("Manifest could not be parsed.");
                return errors;
            }

            string inputPath = Path.Combine(caseDirectory, InputName);
            if(!File.Exists(inputPath))
            {
                errors.Add($"Missing {InputName}.");
                return errors;
            }

            byte[] input = ReadGzip(inputPath);
            string actualHash = GetSha256(input);
            if(!string.Equals(actualHash, testCase.InputSha256, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Input SHA-256 differs: expected {testCase.InputSha256}, actual {actualHash}.");

            var actual = Extract(input);
            CompareText("enhanced JSON", File.ReadAllText(Path.Combine(caseDirectory, ExpectedJsonName)), actual.EnhancedJson, errors);
            CompareText("human-readable output", File.ReadAllText(Path.Combine(caseDirectory, ExpectedHumanName)), actual.HumanReadable, errors);

            var diagnostics = actual.Diagnostics;
            CompareValue("extraction kind", testCase.ExtractionKind, diagnostics.ExtractionKind, errors);
            CompareValue("vault key variant", testCase.VaultKeyVariant, diagnostics.VaultKeyVariant, errors);
            CompareValue("parser kind", testCase.ParserKind, diagnostics.ParserKind, errors);
            CompareValue("page magic", testCase.PageMagic, diagnostics.PageMagic == 0 ? "" : $"0x{diagnostics.PageMagic:X8}", errors);
            CompareValue("device key offset", testCase.DeviceKeyOffset, diagnostics.DeviceKeyOffset, errors);
            CompareValue("arena offset", testCase.ArenaOffset, diagnostics.ArenaOffset, errors);
            CompareValue("decrypted length", testCase.DecryptedLength, diagnostics.DecryptedLength, errors);
            CompareValue("sector count", testCase.SectorCount, diagnostics.SectorCount, errors);
            CompareValue("entry count", testCase.EntryCount, diagnostics.EntryCount, errors);
            CompareValue("largest value length", testCase.LargestValueLength, diagnostics.LargestValueLength, errors);
            CompareValue("duplicate block IDs", testCase.HasDuplicateBlockIds, diagnostics.HasDuplicateBlockIds, errors);
            CompareValue("bad CRC count", testCase.BadCrcCount, diagnostics.BadCrcCount, errors);
            CompareValue("credential indicators",
                string.Join(";", testCase.CredentialIndicators ?? Array.Empty<string>()),
                string.Join(";", TuyaConfigCorpusInventory.GetCredentialIndicators(actual.EnhancedJson)), errors);
            return errors;
        }

        static byte[] CreateReducedInput(byte[] original, TuyaConfig.ExtractionDiagnostics diagnostics)
        {
            var reduced = new byte[original.Length];
            for(int i = 0; i < reduced.Length; i++)
                reduced[i] = 0xFF;

            if(string.Equals(diagnostics.ExtractionKind, "Vault", StringComparison.Ordinal))
            {
                CopyRange(original, reduced, diagnostics.DeviceKeyOffset, SectorSize);
                CopyRange(original, reduced, diagnostics.ArenaOffset, diagnostics.ArenaFlashLength);
            }
            else if(string.Equals(diagnostics.ExtractionKind, "PlaintextPSM", StringComparison.Ordinal) ||
                string.Equals(diagnostics.ExtractionKind, "AesPSM", StringComparison.Ordinal))
            {
                CopyRange(original, reduced, diagnostics.ArenaOffset, diagnostics.DecryptedLength);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported extraction kind: {diagnostics.ExtractionKind}");
            }
            return reduced;
        }

        static void CopyRange(byte[] source, byte[] destination, int offset, int length)
        {
            if(offset < 0 || length <= 0 || offset + length > source.Length)
                throw new InvalidOperationException($"Invalid fixture range offset=0x{offset:X}, length=0x{length:X}.");
            Buffer.BlockCopy(source, offset, destination, offset, length);
        }

        static void AssertEquivalentSource(ExtractionResult source, ExtractionResult replay)
        {
            if(!string.Equals(NormalizeNewlines(source.EnhancedJson), NormalizeNewlines(replay.EnhancedJson), StringComparison.Ordinal))
                throw new InvalidOperationException("Reduced fixture changed enhanced JSON output.");
            if(!string.Equals(NormalizeNewlines(source.HumanReadable), NormalizeNewlines(replay.HumanReadable), StringComparison.Ordinal))
                throw new InvalidOperationException("Reduced fixture changed human-readable output.");
            if(!string.Equals(source.Diagnostics.ExtractionKind, replay.Diagnostics.ExtractionKind, StringComparison.Ordinal) ||
                !string.Equals(source.Diagnostics.VaultKeyVariant, replay.Diagnostics.VaultKeyVariant, StringComparison.Ordinal) ||
                !string.Equals(source.Diagnostics.ParserKind, replay.Diagnostics.ParserKind, StringComparison.Ordinal))
                throw new InvalidOperationException("Reduced fixture changed the extraction path.");
        }

        static void CompareText(string name, string expected, string actual, List<string> errors)
        {
            string normalizedExpected = NormalizeNewlines(expected);
            string normalizedActual = NormalizeNewlines(actual);
            if(string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
                return;

            string[] expectedLines = normalizedExpected.Split('\n');
            string[] actualLines = normalizedActual.Split('\n');
            int common = Math.Min(expectedLines.Length, actualLines.Length);
            int line = 0;
            while(line < common && string.Equals(expectedLines[line], actualLines[line], StringComparison.Ordinal))
                line++;
            errors.Add($"{name} differs at line {line + 1}; expected length {normalizedExpected.Length}, actual length {normalizedActual.Length}.");
        }

        static void CompareValue<T>(string name, T expected, T actual, List<string> errors)
        {
            if(!EqualityComparer<T>.Default.Equals(expected, actual))
                errors.Add($"{name} differs: expected {expected}, actual {actual}.");
        }

        static string NormalizeNewlines(string value)
        {
            return (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        }

        static string GetSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static string GetSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        static byte[] ReadGzip(string path)
        {
            using var input = File.OpenRead(path);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        static void WriteGzip(string path, byte[] data)
        {
            using var output = File.Create(path);
            using var gzip = new GZipStream(output, CompressionLevel.Optimal);
            gzip.Write(data, 0, data.Length);
        }
    }
}
