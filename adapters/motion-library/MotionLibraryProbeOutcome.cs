namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The redacted result of one native load / version probe run.
///
/// One bounded outcome. Failures carry one refusal code from
/// <see cref="MotionLibraryProbeVocabulary"/> and a redacted <see cref="Detail"/>; the detail
/// names at most a module FILE NAME or a configuration variable NAME — never a directory, a
/// path, a hash, an expected version, an endpoint or managed exception internals. Only an
/// accepted outcome carries <see cref="LibraryVersion"/>, and it always equals the
/// Owner-configured expected version (the probe reports the version only after proving it).
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionLibraryProbeOutcome
{
    /// <summary>True only when every proof passed: identity, architecture, load, export, version, module path and deterministic unload.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The success code or the single refusal code, in the canonical vocabulary.</summary>
    public required string Code { get; init; }

    /// <summary>Redacted narrative of the outcome. Never a path, a hash, an endpoint or exception internals.</summary>
    public required string Detail { get; init; }

    /// <summary>The library version reported by the authorized export. Null unless <see cref="Accepted"/>.</summary>
    public string? LibraryVersion { get; init; }

    /// <summary>
    /// True when every native handle the probe opened was released deterministically. Included in
    /// refused outcomes that reached the load stage so the cleanup state is reported plainly.
    /// </summary>
    public required bool NativeHandlesReleased { get; init; }

    /// <summary>Acceptance: all proofs passed, both module handles released, version proven equal to the expected value.</summary>
    public static MotionLibraryProbeOutcome Complete(string libraryVersion, string detail) => new()
    {
        Accepted = true,
        Code = MotionLibraryProbeVocabulary.NativeLibraryProbeComplete,
        Detail = detail,
        LibraryVersion = libraryVersion,
        NativeHandlesReleased = true,
    };

    /// <summary>Refusal: fail closed with the refusal code and a redacted detail.</summary>
    public static MotionLibraryProbeOutcome Refused(string code, string detail, bool nativeHandlesReleased) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        LibraryVersion = null,
        NativeHandlesReleased = nativeHandlesReleased,
    };
}
