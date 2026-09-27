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

        public string SecondaryId { get; }

        public bool FriendlyNameFromSecondaryId { get; }

        public bool HasChipId => string.IsNullOrEmpty(NormalizedId) == false;

        public bool HasSecondaryId => string.IsNullOrEmpty(SecondaryId) == false;

        public bool IsKnown => string.Equals(FriendlyName, "unknown", StringComparison.OrdinalIgnoreCase) == false;

        public BKChipIdentityResult(int? registerAddress, byte[] rawBytes, string normalizedId, string friendlyName, BKType[] matchingTypes,
            string secondaryId = null, bool friendlyNameFromSecondaryId = false)
        {
            RegisterAddress = registerAddress;
            RawBytes = rawBytes ?? Array.Empty<byte>();
            NormalizedId = normalizedId;
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? "unknown" : friendlyName;
            MatchingTypes = matchingTypes ?? Array.Empty<BKType>();
            SecondaryId = secondaryId;
            FriendlyNameFromSecondaryId = friendlyNameFromSecondaryId;
        }

        public bool MatchesSelected(BKType selectedType)
        {
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
        private const int SysVersionIdRegister = 0x44010004;
        private const int AonRevisionIdRegister = 0x440001F0;

        // Only keep IDs here that we have evidence can come back from CMD_ReadReg chip-ID probes.
        // BK7231T/BK7231U/BK7252 modes are intentionally left out because this tool does not
        // have a proven chip-ID register probe for their command flow.
        // BK7231M is intentionally mapped to the same chip ID as BK7231N for chip identity
        // checks only. Its separate relaxed encryption-key behavior remains in BK7231Flasher.cs.
        // Entries without matching BKType values are identification-only: they can be logged
        // and warned about, but they are not selectable chip modes in this tool.
        private static readonly Dictionary<string, BKChipIdentityDefinition> KnownChipIds =
            new Dictionary<string, BKChipIdentityDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "7231c", new BKChipIdentityDefinition("BK7231N family", BKType.BK7231N, BKType.BK7231M) },
                { "7236", new BKChipIdentityDefinition("BK7236 family", BKType.BK7236, BKType.BK7239N, BKType.BK7258) },
                { "7238", new BKChipIdentityDefinition("BK7238", BKType.BK7238) },
                { "7256", new BKChipIdentityDefinition("BK7256") },
                { "7252a", new BKChipIdentityDefinition("BK7252N", BKType.BK7252N) },
                { "7259", new BKChipIdentityDefinition("BK7259") },
            };

        // BK7236-family SYS_VERSION values are shared by several derivatives.
        // Known AON revision IDs refine the platform match where possible.
        private static readonly Dictionary<string, BKChipIdentityDefinition> Known7236SecondaryIds =
            new Dictionary<string, BKChipIdentityDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "25750920", new BKChipIdentityDefinition("BK7239N", BKType.BK7239N) },
                { "25750820", new BKChipIdentityDefinition("BK7239N", BKType.BK7239N) },
                { "25730020", new BKChipIdentityDefinition("BK7239N", BKType.BK7239N) },
                { "25750b20", new BKChipIdentityDefinition("BK7239N", BKType.BK7239N) },
                { "24400030", new BKChipIdentityDefinition("BK7236N", BKType.BK7236) },
                { "24c00020", new BKChipIdentityDefinition("BK7236N", BKType.BK7236) },
                { "24c00030", new BKChipIdentityDefinition("BK7236N", BKType.BK7236) },
                { "26140020", new BKChipIdentityDefinition("BK7236N", BKType.BK7236) },
                { "25300020", new BKChipIdentityDefinition("BK7236Q", BKType.BK7236) },
                { "20340b10", new BKChipIdentityDefinition("BK7236 / BK7258 family", BKType.BK7236, BKType.BK7258) },
            };

        public static bool ShouldAttemptChipIdRead(BKType selectedType)
        {
            switch (selectedType)
            {
                case BKType.BK7231M:
                case BKType.BK7231N:
                case BKType.BK7236:
                case BKType.BK7238:
                case BKType.BK7239N:
                case BKType.BK7252N:
                case BKType.BK7258:
                    return true;
                default:
                    return false;
            }
        }

        public static BKChipIdentityResult Detect(BKType selectedType, Func<int, byte[]> readRegister)
        {
            if (ShouldAttemptChipIdRead(selectedType) == false)
            {
                return new BKChipIdentityResult(null, null, null, null, null);
            }

            BKChipIdentityResult result = DetectForAddresses(GetCandidateRegisterAddresses(selectedType), readRegister);
            if (string.Equals(result.NormalizedId, "7236", StringComparison.OrdinalIgnoreCase))
            {
                result = Refine7236Family(result, readRegister);
            }
            return result;
        }

        private static BKChipIdentityResult Refine7236Family(BKChipIdentityResult primary, Func<int, byte[]> readRegister)
        {
            byte[] rawBytes = readRegister(AonRevisionIdRegister);
            string secondaryId = NormalizeUInt32(rawBytes);
            if (string.IsNullOrEmpty(secondaryId))
            {
                return primary;
            }

            BKChipIdentityDefinition definition;
            bool friendlyNameFromSecondaryId = Known7236SecondaryIds.TryGetValue(secondaryId, out definition);
            if (friendlyNameFromSecondaryId == false)
            {
                definition = new BKChipIdentityDefinition(primary.FriendlyName, primary.MatchingTypes);
            }
            return new BKChipIdentityResult(primary.RegisterAddress, primary.RawBytes, primary.NormalizedId,
                definition.FriendlyName, definition.MatchingTypes, secondaryId, friendlyNameFromSecondaryId);
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

        public static string BuildChipIdReadFailureWarning(BKType selectedType)
        {
            if (ShouldAttemptChipIdRead(selectedType) == false)
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
                case BKType.BK7239N:
                case BKType.BK7258:
                    // These modes prefer SYS_VERSION; every supported chip-ID probe tries both locations.
                    yield return SysVersionIdRegister;
                    yield return SctrlChipIdRegister;
                    break;
                default:
                    yield return SctrlChipIdRegister;
                    yield return SysVersionIdRegister;
                    break;
            }
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
            // 0x7238 -> BK7238, 0x7231c -> BK7231N, 0x7236 -> BK7236 family.
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
