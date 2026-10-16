namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical vocabulary of the native load / version probe software gate.
///
/// This adapter is a minimal, vendor-neutral, fail-closed boundary that PROVES an Owner-installed
/// motion communication library can be loaded from an approved Owner-local directory and reports
/// the library's own version. This Gate authorizes library inspection and loading ONLY. It does
/// not authorize a controller endpoint, controller discovery, a controller connection, a
/// controller read or any motion command, and nothing in this adapter can perform one: the only
/// native export the boundary is allowed to resolve is
/// <see cref="MotionLibraryProbeVocabulary.AuthorizedExportName"/>, a library-local version query
/// that takes a caller-supplied output buffer, requires no controller handle, opens no controller
/// connection and contacts no endpoint.
///
/// Public-repository boundary: this file commits SHAPE ONLY. The commits here are environment
/// variable NAMES, the two approved module FILE NAMES, the single authorized export NAME and the
/// refusal vocabulary. The native directory, the expected file hashes and the expected version
/// are Owner-local external values: they never appear in this repository, in a test, fixture,
/// log line, UI state, document or probe output.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (no .NET SDK in the Arena sandbox;
/// the Owner-local build is the validation of record).
/// </summary>
public static class MotionLibraryProbeVocabulary
{
    // ---------------------------------------------------------------- Owner-local configuration

    /// <summary>
    /// Owner-local environment variable carrying the approved native directory. The value is
    /// confidential local deployment configuration: it is read from the process environment at
    /// probe time, held in memory, redacted everywhere and never committed.
    /// </summary>
    public const string NativeDirectoryVariable = "WJSS_MOTION_LIB_DIR";

    /// <summary>
    /// Owner-local environment variable carrying the expected SHA-256 (64 hexadecimal characters)
    /// of the approved primary module file. The value is never committed and never logged.
    /// </summary>
    public const string ExpectedPrimaryHashVariable = "WJSS_MOTION_LIB_GCLIB_SHA256";

    /// <summary>
    /// Owner-local environment variable carrying the expected SHA-256 (64 hexadecimal characters)
    /// of the approved companion module file. The value is never committed and never logged.
    /// </summary>
    public const string ExpectedCompanionHashVariable = "WJSS_MOTION_LIB_GCLIBO_SHA256";

    /// <summary>
    /// Owner-local environment variable carrying the expected library version string. The
    /// returned version must equal this value exactly (ordinal) for the probe to be accepted.
    /// </summary>
    public const string ExpectedVersionVariable = "WJSS_MOTION_LIB_EXPECTED_VERSION";

    // ----------------------------------------------------------------------- approved artefacts

    /// <summary>The ONLY module file name the probe may load. A file name, never a path.</summary>
    public const string PrimaryModuleFileName = "gclib.dll";

    /// <summary>
    /// The approved companion module verified and loaded first, so dependency resolution binds
    /// the hash-verified companion instead of any other same-named copy on the machine. A file
    /// name, never a path.
    /// </summary>
    public const string CompanionModuleFileName = "gclibo.dll";

    /// <summary>
    /// The ONLY native export the probe may resolve and invoke: the vendor-declared
    /// <c>gclib_result gclib_version(char* version, size_t len)</c>. It requires no controller
    /// handle, opens no controller connection and writes the library version into the caller's
    /// bounded buffer. No open, close, discovery, address enumeration, command, callback,
    /// subscription, revision, serial or status API is resolvable through this boundary.
    /// </summary>
    public const string AuthorizedExportName = "gclib_version";

    /// <summary>The bounded output buffer handed to the authorized export. Fixed and filled with zeroes first.</summary>
    public const int VersionBufferSize = 256;

    // ---------------------------------------------------------------- redacted status wording

    /// <summary>The only configuration statement the probe may publish: values are never included.</summary>
    public const string ConfigurationRedacted = "MOTION_LIBRARY_CONFIGURATION_CONFIGURED_REDACTED";

    /// <summary>Success: identity, architecture, load, export, version and module-path proofs all passed.</summary>
    public const string NativeLibraryProbeComplete = "NATIVE_LIBRARY_PROBE_COMPLETE";

    // ----------------------------------------------------------------------- refusal vocabulary

    /// <summary>A required Owner-local configuration variable is absent or blank. Nothing was probed.</summary>
    public const string NativeLibDirNotConfigured = "NATIVE_LIB_DIR_NOT_CONFIGURED";

    /// <summary>A configured value was present but rejected by shape rules (not fully qualified, UNC, wildcard, invalid character, malformed hash, or module path escaping the approved directory).</summary>
    public const string NativeLibDirRejected = "NATIVE_LIB_DIR_REJECTED";

    /// <summary>An approved module file is absent from the approved directory.</summary>
    public const string NativeFileNotFound = "NATIVE_FILE_NOT_FOUND";

    /// <summary>An approved module file exists but its SHA-256 does not equal the Owner-configured expected hash.</summary>
    public const string NativeFileIdentityMismatch = "NATIVE_FILE_IDENTITY_MISMATCH";

    /// <summary>An approved module file could not be read for the identity or architecture proof, so neither could be verified.</summary>
    public const string NativeFileReadFailed = "NATIVE_FILE_READ_FAILED";

    /// <summary>An approved module file is not an x64 PE image, or its image header cannot be proven to be one.</summary>
    public const string NativeArchitectureMismatch = "NATIVE_ARCHITECTURE_MISMATCH";

    /// <summary>The approved module could not be loaded from the approved absolute path.</summary>
    public const string NativeLoadFailed = "NATIVE_LOAD_FAILED";

    /// <summary>The authorized export is absent from the loaded approved module.</summary>
    public const string NativeExportNotFound = "NATIVE_EXPORT_NOT_FOUND";

    /// <summary>The authorized export returned no terminated, printable version string within the bounded buffer.</summary>
    public const string NativeVersionCallFailed = "NATIVE_VERSION_CALL_FAILED";

    /// <summary>The returned version does not equal the Owner-configured expected version.</summary>
    public const string NativeVersionMismatch = "NATIVE_VERSION_MISMATCH";

    /// <summary>The path of the loaded module could not be proven equal to the approved file, or was proven different.</summary>
    public const string NativeModulePathMismatch = "NATIVE_MODULE_PATH_MISMATCH";

    /// <summary>A native handle could not be released deterministically after a successful probe.</summary>
    public const string NativeUnloadFailed = "NATIVE_UNLOAD_FAILED";

    /// <summary>The probe can only run inside a 64-bit Windows process; nothing was loaded.</summary>
    public const string NativeProbeUnsupportedPlatform = "NATIVE_PROBE_UNSUPPORTED_PLATFORM";

    /// <summary>Probe detail texts NEVER carry this value: guards tests and reviews.</summary>
    public const string DetailRedactionRequirement =
        "Probe outcome Detail members must never contain a directory, a path, a hash, an expected " +
        "version value, an endpoint, or managed exception internals. Module file names are the " +
        "greatest identity detail permitted.";
}
