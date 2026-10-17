namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The ONE scoped exception to the repository-wide prohibition on naming a raw command API.
///
/// The prohibition itself is unchanged and still applies to every file, every project and every
/// documentation page of the gate set. Exactly one (path, token) pair is exempt: the internal B2
/// native boundary file and the one native export constant it resolves. The exemption is matched
/// by exact ordinal string equality on the normalized repository-relative path AND on the token,
/// so it cannot be widened by a wildcard, a sibling file, a nested path, a case variant, a
/// generated build-output path or any other token.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class B2CommandTokenException
{
    /// <summary>The only file whose text may name the token, relative to the repository root.</summary>
    internal const string ApprovedRelativePath =
        "adapters/motion-library/MotionControllerCommandKernel.cs";

    /// <summary>The only token the approved file may name.</summary>
    internal const string ApprovedToken = "gclib_command";

    /// <summary>
    /// True only for the exact (path, token) pair. Path comparison is ordinal after normalizing
    /// the separator, so a case variant or a differently rooted path is not the approved file.
    /// </summary>
    internal static bool IsApprovedPair(string? relativePath, string? token) =>
        string.Equals(Normalize(relativePath), ApprovedRelativePath, StringComparison.Ordinal)
        && string.Equals(token, ApprovedToken, StringComparison.Ordinal);

    /// <summary>True only when the file resolves to the approved path and the token is the approved token.</summary>
    internal static bool Covers(string repositoryRoot, string file, string token) =>
        IsApprovedPair(Path.GetRelativePath(repositoryRoot, file), token);

    private static string Normalize(string? path) =>
        (path ?? string.Empty).Replace('\\', '/');
}
