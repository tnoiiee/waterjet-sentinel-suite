using System.Security.Cryptography;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The resolved Owner-local configuration of one native load / version probe.
///
/// It exists so the probe can hold the approved directory, the derived approved module paths and
/// the expected hashes without ever rendering them: <see cref="ToString"/> returns only the
/// redacted identity, so console diagnostics, structured log templates, exception text, debugger
/// output and any accidental interpolation can never reveal the confidential values. The values
/// themselves are reachable only through the typed members, which the probe reads solely to
/// perform the identity, architecture, load and version proofs; they are never part of a
/// contract, an API payload, a fixture, evidence or UI state.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionLibraryProbeConfiguration
{
    /// <summary>The approved native directory, normalized. Never serialized, logged or displayed.</summary>
    public required string NativeDirectory { get; init; }

    /// <summary>The approved primary module absolute path (approved directory + gclib.dll).</summary>
    public required string PrimaryModulePath { get; init; }

    /// <summary>The approved companion module absolute path (approved directory + gclibo.dll).</summary>
    public required string CompanionModulePath { get; init; }

    /// <summary>The expected SHA-256 (32 bytes) of the primary module file. Never rendered.</summary>
    public required byte[] ExpectedPrimaryHash { get; init; }

    /// <summary>The expected SHA-256 (32 bytes) of the companion module file. Never rendered.</summary>
    public required byte[] ExpectedCompanionHash { get; init; }

    /// <summary>The expected library version string. The probe accepts only an exact ordinal equality.</summary>
    public required string ExpectedVersion { get; init; }

    /// <summary>The redacted logical identity only; the configured values are never included.</summary>
    public override string ToString() => MotionLibraryProbeVocabulary.ConfigurationRedacted;
}

/// <summary>
/// Resolves the Owner-local probe configuration from an external source.
///
/// Public-repository boundary: the values are confidential local deployment configuration. They
/// are read from the process environment at probe time, held only in memory, and never committed,
/// serialized, returned by an API route, written to a log, rendered by a UI, or recorded in
/// evidence. The only configuration statement any output may carry is the redacted identity
/// <see cref="MotionLibraryProbeVocabulary.ConfigurationRedacted"/>.
///
/// There is deliberately NO committed configuration path for any value: the resolver accepts only
/// an environment reader, so no repository file can supply, override or default the directory,
/// the hashes or the expected version. The resolver is deliberately fail-closed and performs NO
/// I/O: it validates shape only (a present variable is not a valid variable). Nothing here
/// probes, loads, opens, enumerates or contacts anything.
/// </summary>
public static class MotionLibraryConfigurationResolver
{
    /// <summary>
    /// Reads the four required Owner-local variables and produces the resolved probe
    /// configuration. Fails closed on absence, blankness, a directory that is not a fully
    /// qualified local path (UNC, device, wildcard, quote, control character, stray colon or
    /// over-length values are all rejected), a hash that is not exactly 64 hexadecimal
    /// characters, a blank expected version, or a module path that would escape the approved
    /// directory.
    /// </summary>
    public static bool TryResolve(
        Func<string, string?> readEnvironment,
        out MotionLibraryProbeConfiguration configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        configuration = null!;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        var rawDirectory = RequiredValue(
            readEnvironment, MotionLibraryProbeVocabulary.NativeDirectoryVariable, ref refusalCode, ref refusalDetail);
        if (rawDirectory is null) return false;

        var rawPrimaryHash = RequiredValue(
            readEnvironment, MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable, ref refusalCode, ref refusalDetail);
        if (rawPrimaryHash is null) return false;

        var rawCompanionHash = RequiredValue(
            readEnvironment, MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable, ref refusalCode, ref refusalDetail);
        if (rawCompanionHash is null) return false;

        var expectedVersion = RequiredValue(
            readEnvironment, MotionLibraryProbeVocabulary.ExpectedVersionVariable, ref refusalCode, ref refusalDetail);
        if (expectedVersion is null) return false;

        if (!NativeFileInspection.TryNormalizeApprovedDirectory(rawDirectory, out var nativeDirectory, out var directoryReason))
        {
            return Reject(directoryReason, out refusalCode, out refusalDetail);
        }

        if (!NativeFileInspection.TryParseExpectedHash(rawPrimaryHash, out var expectedPrimaryHash))
        {
            return Reject(
                $"{MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable} must be exactly 64 hexadecimal characters " +
                "(the SHA-256 of the approved primary module file). A present value is not a valid value.",
                out refusalCode, out refusalDetail);
        }

        if (!NativeFileInspection.TryParseExpectedHash(rawCompanionHash, out var expectedCompanionHash))
        {
            return Reject(
                $"{MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable} must be exactly 64 hexadecimal characters " +
                "(the SHA-256 of the approved companion module file). A present value is not a valid value.",
                out refusalCode, out refusalDetail);
        }

        if (!NativeFileInspection.TryCombineContained(
                nativeDirectory, MotionLibraryProbeVocabulary.PrimaryModuleFileName, out var primaryModulePath)
            || !NativeFileInspection.TryCombineContained(
                nativeDirectory, MotionLibraryProbeVocabulary.CompanionModuleFileName, out var companionModulePath))
        {
            return Reject(
                "An approved module path would escape the approved directory. The approved modules live " +
                "directly in the approved native directory, never in a subdirectory or a sibling.",
                out refusalCode, out refusalDetail);
        }

        configuration = new MotionLibraryProbeConfiguration
        {
            NativeDirectory = nativeDirectory,
            PrimaryModulePath = primaryModulePath,
            CompanionModulePath = companionModulePath,
            ExpectedPrimaryHash = expectedPrimaryHash,
            ExpectedCompanionHash = expectedCompanionHash,
            ExpectedVersion = expectedVersion,
        };
        return true;
    }

    /// <summary>
    /// The ONLY configuration statement a caller may publish, log or display. It never contains
    /// the directory, a path, a hash or the expected version.
    /// </summary>
    public static string DescribeConfiguration(MotionLibraryProbeConfiguration? configuration) =>
        configuration is null
            ? "MOTION_LIBRARY_CONFIGURATION_NOT_CONFIGURED"
            : MotionLibraryProbeVocabulary.ConfigurationRedacted;

    /// <summary>
    /// One required variable. Absent or blank after trimming is a configuration-missing refusal;
    /// the variable NAME is the greatest detail ever reported (names are committed shape; values
    /// never are).
    /// </summary>
    private static string? RequiredValue(
        Func<string, string?> readEnvironment, string variable, ref string refusalCode, ref string refusalDetail)
    {
        var raw = readEnvironment(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            refusalCode = MotionLibraryProbeVocabulary.NativeLibDirNotConfigured;
            refusalDetail =
                $"Probe configuration is missing: {variable} has no Owner-local value. The value is " +
                "deliberately not committed: probe configuration is confidential local deployment " +
                "material and no repository value is read, defaulted or substituted.";
            return null;
        }

        return raw.Trim();
    }

    private static bool Reject(string why, out string refusalCode, out string refusalDetail)
    {
        refusalCode = MotionLibraryProbeVocabulary.NativeLibDirRejected;
        refusalDetail = why;
        return false;
    }
}

/// <summary>
/// File-shape inspection helpers of the native probe boundary. Pure shape logic and bounded
/// reads only: no load, no open, no enumeration, no network, no controller concept exists here.
/// </summary>
internal static class NativeFileInspection
{
    /// <summary>The PE machine value of the approved architecture: x64 (AMD64).</summary>
    internal const ushort PeMachineX64 = 0x8664;

    /// <summary>Length of a SHA-256 hash in bytes and in hexadecimal characters.</summary>
    internal const int Sha256ByteLength = 32;

    internal const int Sha256HexLength = Sha256ByteLength * 2;

    /// <summary>Upper bound on the approved directory value length; rejects pathological values before any I/O.</summary>
    private const int MaxDirectoryLength = 240;

    /// <summary>
    /// Normalizes the approved native directory with strict shape rules, performing NO I/O.
    /// Only a fully qualified local drive path is accepted: no UNC, no device path, no relative
    /// or rooted-but-driveless path, no wildcard, no quote, no control or invalid character, no
    /// stray colon, no percent expansion and no over-length value. Trailing separators are
    /// collapsed except the root itself.
    /// </summary>
    internal static bool TryNormalizeApprovedDirectory(string raw, out string normalized, out string reason)
    {
        normalized = string.Empty;
        reason = string.Empty;

        if (raw.Length < 3 || raw.Length > MaxDirectoryLength)
        {
            reason = "The approved native directory must be a fully qualified local directory value of reasonable length.";
            return false;
        }

        if (raw.Contains('%'))
        {
            reason = "The approved native directory must be a literal path: expansion tokens are never expanded.";
            return false;
        }

        if (raw.Contains('*') || raw.Contains('?'))
        {
            reason = "The approved native directory must name exactly one directory: wildcards are rejected.";
            return false;
        }

        if (raw.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            reason = "The approved native directory contains a character that is invalid in a directory path.";
            return false;
        }

        if (raw.Contains('"'))
        {
            // Explicit and deterministic: the platform invalid-character set varies by release
            // and is never allowed to define this gate's quote rule. A quote can make a
            // configured value pass shape checks silently, so the guard is stated here once,
            // in canonical order, before any drive/path normalization touches the value.
            reason = "The approved native directory must be a literal path: quote characters are never accepted.";
            return false;
        }

        var hasDrive = char.IsAsciiLetter(raw[0])
                       && raw[1] == ':'
                       && (raw[2] == '\\' || raw[2] == '/');
        if (!hasDrive)
        {
            reason =
                "The approved native directory must be a fully qualified local drive path (for example " +
                "the approved directory recorded in the Owner-local evidence). UNC, device, rooted-but-" +
                "driveless and relative values are all rejected: directory value shape only, never I/O.";
            return false;
        }

        if (raw.AsSpan(2).Contains(':'))
        {
            reason = "The approved native directory may not contain a colon beyond the drive volume separator.";
            return false;
        }

        normalized = Path.GetFullPath(raw);
        var volumeRoot = Path.GetPathRoot(normalized) ?? string.Empty;
        while (normalized.Length > volumeRoot.Length
               && (normalized.EndsWith('\\') || normalized.EndsWith('/')))
        {
            normalized = normalized[..^1];
        }

        return true;
    }

    /// <summary>
    /// Parses an expected SHA-256 from exactly 64 hexadecimal characters (either case). No
    /// separator, prefix or whitespace form is accepted: the value is one hash and nothing else.
    /// </summary>
    internal static bool TryParseExpectedHash(string raw, out byte[] hash)
    {
        hash = Array.Empty<byte>();
        if (raw.Length != Sha256HexLength)
        {
            return false;
        }

        foreach (var c in raw)
        {
            var isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        hash = Convert.FromHexString(raw);
        return hash.Length == Sha256ByteLength;
    }

    /// <summary>
    /// Combines the approved directory with one approved module FILE NAME and proves containment:
    /// the resulting full path must live directly in the approved directory — never in a
    /// subdirectory, a sibling or a different volume.
    /// </summary>
    internal static bool TryCombineContained(string approvedDirectory, string moduleFileName, out string fullPath)
    {
        fullPath = string.Empty;
        if (moduleFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || moduleFileName.Contains('\\')
            || moduleFileName.Contains('/')
            || moduleFileName.Contains(':'))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(approvedDirectory, moduleFileName));
        var parent = Path.GetDirectoryName(combined) ?? string.Empty;
        if (!string.Equals(parent, approvedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = combined;
        return true;
    }

    /// <summary>Constant-time equality of an expected and a computed hash.</summary>
    internal static bool HashesEqual(byte[] expected, byte[] actual) =>
        expected.Length == Sha256ByteLength
        && actual.Length == Sha256ByteLength
        && CryptographicOperations.FixedTimeEquals(expected, actual);

    /// <summary>
    /// Computes the SHA-256 of one approved file through a bounded read. Returns false when the
    /// file cannot be read; the caller refuses closed in that case because the identity can no
    /// longer be proven.
    /// </summary>
    internal static bool TryComputeSha256(string absolutePath, out byte[] hash)
    {
        hash = Array.Empty<byte>();
        try
        {
            using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            hash = SHA256.HashData(stream);
            return hash.Length == Sha256ByteLength;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the PE machine value from a bounded prefix of a module image. Recognizes the DOS MZ
    /// header, validates the e_lfanew pointer against the buffer bound and the PE signature, and
    /// reports the 16-bit machine field. Anything structurally invalid returns false: an
    /// unparseable image is not a provable x64 image.
    /// </summary>
    internal static bool TryReadPeMachine(ReadOnlySpan<byte> image, out ushort machine)
    {
        machine = 0;
        const int dosHeaderLength = 0x40;
        const int lfanewOffset = 0x3C;
        const int peHeaderMinimum = 24;

        if (image.Length < dosHeaderLength + peHeaderMinimum)
        {
            return false;
        }

        if (image[0] != (byte)'M' || image[1] != (byte)'Z')
        {
            return false;
        }

        var lfanew = BitConverter.ToInt32(image[lfanewOffset..(lfanewOffset + 4)]);
        if (lfanew < dosHeaderLength || lfanew > image.Length - peHeaderMinimum)
        {
            return false;
        }

        if (image[lfanew] != (byte)'P'
            || image[lfanew + 1] != (byte)'E'
            || image[lfanew + 2] != 0
            || image[lfanew + 3] != 0)
        {
            return false;
        }

        machine = BitConverter.ToUInt16(image[(lfanew + 4)..(lfanew + 6)]);
        return true;
    }
}
