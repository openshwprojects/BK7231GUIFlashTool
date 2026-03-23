using System;
using System.Collections.Generic;
using System.Text;

namespace BK7231Flasher
{
    internal sealed class BKChipIdentityResult
    {
        public int? RegisterAddress { get; }

        public byte[] RawBytes { get; }

        public string NormalizedId { get; }

        public string FriendlyName { get; }

        public BKType[] MatchingTypes { get; }

        public bool HasChipId => string.IsNullOrEmpty(NormalizedId) == false;

        public bool IsKnown => string.Equals(FriendlyName, "unknown", StringComparison.OrdinalIgnoreCase) == false;

        public BKChipIdentityResult(int? registerAddress, byte[] rawBytes, string normalizedId, string friendlyName, BKType[] matchingTypes)
        {
            RegisterAddress = registerAddress;
            RawBytes = rawBytes ?? Array.Empty<byte>();
            NormalizedId = normalizedId;
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? "unknown" : friendlyName;
            MatchingTypes = matchingTypes ?? Array.Empty<BKType>();
        }

        public bool MatchesSelected(BKType selectedType)
        {
            if (selectedType == BKType.BK7231M)
            {
                return true;
            }
            for (int i = 0; i < MatchingTypes.Length; i++)
            {
                if (MatchingTypes[i] == selectedType)
                {
                    return true;
                }
            }
            return false;
        }

        public bool ShouldWarnSelected(BKType selectedType)
        {
            if (selectedType == BKType.BK7231M)
            {
                return false;
            }
            if (HasChipId == false)
            {
                return false;
            }
            if (IsKnown == false)
            {
                return true;
            }
            return MatchesSelected(selectedType) == false;
        }

        public string BuildMismatchWarning(BKType selectedType)
        {
            if (ShouldWarnSelected(selectedType) == false)
            {
                return null;
            }
            if (IsKnown)
            {
                return $"WARNING! Selected chip is a {selectedType}, but according to chip ID this is a {FriendlyName}!";
            }
            return $"WARNING! Selected chip is a {selectedType}, but according to chip ID this is an unknown chip (0x{NormalizedId})!";
        }

        public string DescribeDetectedChip()
        {
            if (HasChipId == false)
            {
                return "an unknown chip";
            }
            if (IsKnown)
            {
                return $"{FriendlyName} (chip ID 0x{NormalizedId})";
            }
            return $"an unknown chip (chip ID 0x{NormalizedId})";
        }
    }

    internal sealed class BKChipIdentityDefinition
    {
        public string FriendlyName { get; }

        public BKType[] MatchingTypes { get; }

        public BKChipIdentityDefinition(string friendlyName, params BKType[] matchingTypes)
        {
            FriendlyName = friendlyName;
            MatchingTypes = matchingTypes ?? Array.Empty<BKType>();
        }
    }

    internal static class BKChipIdentity
    {
        private const int SctrlChipIdRegister = 0x800000;
        private const int DeviceIdRegister = 0x44010004;

        // Only keep IDs here that we have evidence can come back from the newer ReadReg path.
        // Legacy BK7231T/BK7231U/BK7252 modes are handled by probe policy instead of assuming
        // fixed chip-ID strings for devices that normally do not support this register read.
        // BK7231M is intentionally not mapped here; in practice it is treated as a user-facing
        // mode for BK7231N-family chips without the expected Tuya encryption key.
        // Entries without matching BKType values are identification-only: they can be logged
        // and warned about, but they are not selectable chip modes in this tool.
        private static readonly Dictionary<string, BKChipIdentityDefinition> KnownChipIds =
            new Dictionary<string, BKChipIdentityDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "7231c", new BKChipIdentityDefinition("BK7231N", BKType.BK7231N) },
                { "7236", new BKChipIdentityDefinition("BK7236 / BK7258 family", BKType.BK7236, BKType.BK7258) },
                { "7238", new BKChipIdentityDefinition("BK7238", BKType.BK7238) },
                { "7256", new BKChipIdentityDefinition("BK7256") },
                { "7252a", new BKChipIdentityDefinition("BK7252N", BKType.BK7252N) },
                { "7259", new BKChipIdentityDefinition("BK7259") },
            };

        private static readonly Dictionary<string, BKChipIdentityDefinition> KnownBootVersions =
            new Dictionary<string, BKChipIdentityDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "BK7231S_1.0.5", new BKChipIdentityDefinition("legacy BK7231T / BK7231U family", BKType.BK7231T, BKType.BK7231U) },
            };

        public static bool ShouldAttemptRead(BKType selectedType)
        {
            switch (selectedType)
            {
                case BKType.BK7231T:
                case BKType.BK7231U:
                case BKType.BK7252:
                    return false;
                default:
                    return true;
            }
        }

        public static bool ShouldProbeUnexpectedReadReply(BKType selectedType)
        {
            return ShouldAttemptRead(selectedType) == false;
        }

        public static BKChipIdentityResult Detect(BKType selectedType, Func<int, byte[]> readRegister)
        {
            if (ShouldAttemptRead(selectedType) == false)
            {
                return new BKChipIdentityResult(null, null, null, null, null);
            }

            return DetectForAddresses(GetCandidateRegisterAddresses(selectedType), readRegister);
        }

        public static BKChipIdentityResult ProbeUnexpectedReadReply(BKType selectedType, Func<int, byte[]> readRegister)
        {
            if (ShouldProbeUnexpectedReadReply(selectedType) == false)
            {
                return new BKChipIdentityResult(null, null, null, null, null);
            }

            return DetectForAddresses(GetUnexpectedProbeAddresses(), readRegister);
        }

        public static string BuildUnexpectedReadReplyWarning(BKType selectedType, BKChipIdentityResult detectedChip)
        {
            if (detectedChip == null || detectedChip.HasChipId == false)
            {
                return null;
            }
            return $"WARNING! Selected chip is a {selectedType}, but chip ID read unexpectedly replied with 0x{detectedChip.NormalizedId} ({detectedChip.FriendlyName}). This chip mode normally does not support chip ID read, so the selected chip may be wrong.";
        }

        public static string FormatBootVersionForLog(string bootVersion)
        {
            if (string.IsNullOrWhiteSpace(bootVersion))
            {
                return null;
            }

            BKChipIdentityDefinition definition;
            if (KnownBootVersions.TryGetValue(bootVersion, out definition))
            {
                return $"{bootVersion} ({definition.FriendlyName})";
            }
            return bootVersion;
        }

        public static string BuildBootVersionWarning(BKType selectedType, string bootVersion)
        {
            if (string.IsNullOrWhiteSpace(bootVersion))
            {
                return null;
            }

            BKChipIdentityDefinition definition;
            if (KnownBootVersions.TryGetValue(bootVersion, out definition) == false)
            {
                return null;
            }

            for (int i = 0; i < definition.MatchingTypes.Length; i++)
            {
                if (definition.MatchingTypes[i] == selectedType)
                {
                    return null;
                }
            }

            return $"WARNING! Selected chip is a {selectedType}, but according to boot version this looks like a {definition.FriendlyName}!";
        }

        private static BKChipIdentityResult DetectForAddresses(IEnumerable<int> registerAddresses, Func<int, byte[]> readRegister)
        {
            BKChipIdentityResult bestResult = new BKChipIdentityResult(null, null, null, null, null);
            foreach (int registerAddress in registerAddresses)
            {
                byte[] rawBytes = readRegister(registerAddress);
                if (rawBytes == null)
                {
                    continue;
                }

                BKChipIdentityResult current = FromRaw(registerAddress, rawBytes);
                if (current.HasChipId == false)
                {
                    if (bestResult.RegisterAddress.HasValue == false)
                    {
                        bestResult = current;
                    }
                    continue;
                }

                if (current.IsKnown)
                {
                    return current;
                }

                if (bestResult.HasChipId == false)
                {
                    bestResult = current;
                }
            }

            return bestResult;
        }

        public static string BuildReadRegFailureWarning(BKType selectedType)
        {
            if (ShouldAttemptRead(selectedType) == false || selectedType == BKType.BK7231M)
            {
                return null;
            }
            return $"WARNING! Failed to read chip ID for selected chip mode {selectedType}. This chip mode normally supports chip ID read, so the selected chip may be wrong.";
        }

        private static IEnumerable<int> GetCandidateRegisterAddresses(BKType selectedType)
        {
            switch (selectedType)
            {
                case BKType.BK7236:
                case BKType.BK7258:
                    // Current BK7236/BK7258 code reads 0x44010004 first, while the
                    // other BK chips in this path use the SCTRL_CHIP_ID register at 0x800000.
                    yield return DeviceIdRegister;
                    yield return SctrlChipIdRegister;
                    break;
                default:
                    yield return SctrlChipIdRegister;
                    break;
            }
        }

        private static IEnumerable<int> GetUnexpectedProbeAddresses()
        {
            yield return SctrlChipIdRegister;
            yield return DeviceIdRegister;
        }

        private static BKChipIdentityResult FromRaw(int registerAddress, byte[] rawBytes)
        {
            List<string> candidateIds = EnumerateCandidateIds(rawBytes);
            BKChipIdentityDefinition definition = null;
            string chosenId = null;

            for (int i = 0; i < candidateIds.Count; i++)
            {
                string candidateId = candidateIds[i];
                if (KnownChipIds.TryGetValue(candidateId, out definition))
                {
                    chosenId = candidateId;
                    break;
                }
                if (chosenId == null)
                {
                    chosenId = candidateId;
                }
            }

            if (definition != null)
            {
                return new BKChipIdentityResult(registerAddress, rawBytes, chosenId, definition.FriendlyName, definition.MatchingTypes);
            }

            return new BKChipIdentityResult(registerAddress, rawBytes, chosenId, null, null);
        }

        private static List<string> EnumerateCandidateIds(byte[] rawBytes)
        {
            List<string> results = new List<string>();
            // Expected examples from the existing flasher logic:
            // 0x7238 -> BK7238, 0x7231c -> BK7231N, 0x7236 -> BK7236/BK7258 family.
            AddCandidate(results, NormalizePreferred(rawBytes));
            AddCandidate(results, NormalizeLegacy(rawBytes));
            AddCandidate(results, NormalizeUInt32(rawBytes));
            return results;
        }

        private static void AddCandidate(List<string> results, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }
            for (int i = 0; i < results.Count; i++)
            {
                if (string.Equals(results[i], candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            results.Add(candidate);
        }

        private static string NormalizePreferred(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
            {
                return null;
            }
            bool hasNonZeroByte = false;
            for (int i = 0; i < rawBytes.Length; i++)
            {
                if (rawBytes[i] != 0x00)
                {
                    hasNonZeroByte = true;
                    break;
                }
            }
            if (hasNonZeroByte == false)
            {
                return null;
            }

            byte[] bigEndian = new byte[rawBytes.Length];
            for (int i = 0; i < rawBytes.Length; i++)
            {
                bigEndian[i] = rawBytes[rawBytes.Length - 1 - i];
            }

            int start = 0;
            while (start < bigEndian.Length - 1 && bigEndian[start] == 0x00)
            {
                start++;
            }
            if (start < bigEndian.Length - 1 && bigEndian[start] == 0x01)
            {
                start++;
                while (start < bigEndian.Length - 1 && bigEndian[start] == 0x00)
                {
                    start++;
                }
            }

            return FormatBytesAsHex(bigEndian, start);
        }

        private static string NormalizeLegacy(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = rawBytes.Length - 1; i >= 0; i--)
            {
                byte current = rawBytes[i];
                if (current == 0x00 || current == 0x01)
                {
                    continue;
                }
                builder.Append(current.ToString("x"));
            }
            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string NormalizeUInt32(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length != 4)
            {
                return null;
            }

            uint value = (uint)(rawBytes[0]
                | (rawBytes[1] << 8)
                | (rawBytes[2] << 16)
                | (rawBytes[3] << 24));
            return value == 0 ? null : value.ToString("x");
        }

        private static string FormatBytesAsHex(byte[] bytes, int startIndex)
        {
            if (bytes == null || bytes.Length == 0 || startIndex >= bytes.Length)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = startIndex; i < bytes.Length; i++)
            {
                builder.Append(i == startIndex ? bytes[i].ToString("x") : bytes[i].ToString("x2"));
            }
            return builder.Length == 0 ? null : builder.ToString();
        }
    }
}
