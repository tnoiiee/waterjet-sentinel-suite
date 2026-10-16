namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// Runs the native load / version probe: a minimal, fail-closed proof that the Owner-installed
/// motion communication library can be loaded from the approved Owner-local directory and that
/// its reported version equals the Owner-configured expected version.
///
/// Order of proof (every step fails closed — a refusal never skips a rule and never retries):
///   0. platform gate: a 64-bit Windows process only;
///   1. both approved files exist in the approved directory;
///   2. both file identities equal the Owner-configured expected SHA-256 values;
///   3. both images are x64 PE images;
///   4. the companion loads from its approved absolute path, so dependency resolution binds the
///      hash-verified companion and never a same-named copy elsewhere on the machine;
///   5. the loaded companion's module path is proven equal to its approved file;
///   6. the primary module loads from its approved absolute path with the restricted flag set;
///   7. the loaded primary's module path is proven equal to its approved file;
///   8. exactly one export — <see cref="MotionLibraryProbeVocabulary.AuthorizedExportName"/> —
///      is resolved; nothing else is ever resolved;
///   9. the export is invoked once with the bounded, zero-filled buffer;
///  10. the returned version is a terminated, printable string that equals the expected version;
///  11. both module handles are released deterministically (primary first, companion last).
///
/// The runner holds NO session, owns NOTHING beyond the call, contacts NO endpoint, performs NO
/// discovery, and resolves NO open/close/command/callback/status export. It never invents a
/// value: a failure is reported as the refusal that happened, in the canonical vocabulary, with
/// redacted detail.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed class MotionLibraryProbeRunner
{
    private readonly MotionLibraryProbeConfiguration _configuration;
    private readonly INativeKernel _kernel;

    /// <summary>The production boundary: the real operating-system loading primitives.</summary>
    public MotionLibraryProbeRunner(MotionLibraryProbeConfiguration configuration)
        : this(configuration, new WindowsNativeKernel())
    {
    }

    /// <summary>Test seam (InternalsVisibleTo): exercises the complete defect class without a real native module.</summary>
    internal MotionLibraryProbeRunner(MotionLibraryProbeConfiguration configuration, INativeKernel kernel)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    /// <summary>Executes the probe once against the resolved configuration.</summary>
    public MotionLibraryProbeOutcome Probe()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
                "The native load / version probe runs only in a 64-bit Windows process. Nothing " +
                "was loaded; the refusal is stated before any file outside the identity-proof " +
                "scope of this gate was touched.",
                nativeHandlesReleased: true);
        }

        IntPtr companionModule = IntPtr.Zero;
        IntPtr primaryModule = IntPtr.Zero;
        MotionLibraryProbeOutcome? outcome = null;
        try
        {
            outcome = ProbeCore(ref companionModule, ref primaryModule);
        }
        finally
        {
            // Deterministic cleanup on every exit path: release whatever was loaded, in reverse
            // load order. An accepted core already released and zeroed both handles, so this is a
            // no-op there; on refusal paths it is the unload proof, and an unload that cannot be
            // confirmed degrades the outcome honestly instead of masking the primary refusal.
            var cleaned = ReleaseModules(ref primaryModule, ref companionModule);
            if (outcome is not null && !cleaned)
            {
                outcome = outcome.Accepted
                    ? MotionLibraryProbeOutcome.Refused(
                        MotionLibraryProbeVocabulary.NativeUnloadFailed,
                        "The probe proofs passed, but a native module handle could not be released " +
                        "deterministically. The probe does not report success with an unreleased handle.",
                        nativeHandlesReleased: false)
                    : outcome with
                    {
                        NativeHandlesReleased = false,
                        Detail = outcome.Detail + " Deterministic cleanup of the loaded native " +
                                 "handles could not be fully confirmed.",
                    };
            }
        }

        return outcome!;
    }

    /// <summary>The ordered proof. Any refusal is final: it is never re-attempted and never masked.</summary>
    private MotionLibraryProbeOutcome ProbeCore(ref IntPtr companionModule, ref IntPtr primaryModule)
    {
        var companionPath = _configuration.CompanionModulePath;
        var primaryPath = _configuration.PrimaryModulePath;

        // 1. Existence. The module FILE NAME is the greatest identity detail ever reported.
        if (!File.Exists(companionPath))
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeFileNotFound,
                $"The approved companion module {MotionLibraryProbeVocabulary.CompanionModuleFileName} " +
                "is absent from the approved directory.",
                nativeHandlesReleased: true);
        }

        if (!File.Exists(primaryPath))
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeFileNotFound,
                $"The approved primary module {MotionLibraryProbeVocabulary.PrimaryModuleFileName} " +
                "is absent from the approved directory.",
                nativeHandlesReleased: true);
        }

        if (!VerifyFileIdentity(companionPath, _configuration.ExpectedCompanionHash, MotionLibraryProbeVocabulary.CompanionModuleFileName, out var refusal))
        {
            return refusal;
        }

        if (!VerifyFileIdentity(primaryPath, _configuration.ExpectedPrimaryHash, MotionLibraryProbeVocabulary.PrimaryModuleFileName, out refusal))
        {
            return refusal;
        }

        // 3. Architecture: both images must be provably x64 PE images before any load.
        if (!VerifyX64(companionPath, MotionLibraryProbeVocabulary.CompanionModuleFileName, out refusal))
        {
            return refusal;
        }

        if (!VerifyX64(primaryPath, MotionLibraryProbeVocabulary.PrimaryModuleFileName, out refusal))
        {
            return refusal;
        }

        // 4./5. Companion first: if a module of the companion's name is ever required, the
        // in-process module is the hash-verified one from the approved directory.
        companionModule = _kernel.LoadApprovedModule(companionPath);
        if (companionModule == IntPtr.Zero)
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeLoadFailed,
                $"The approved companion module {MotionLibraryProbeVocabulary.CompanionModuleFileName} " +
                "could not be loaded from its approved location with the restricted search set.",
                nativeHandlesReleased: true);
        }

        if (!ProveLoadedModulePath(companionModule, companionPath, MotionLibraryProbeVocabulary.CompanionModuleFileName, out refusal))
        {
            return refusal;
        }

        // 6./7. Primary module, same proof shape.
        primaryModule = _kernel.LoadApprovedModule(primaryPath);
        if (primaryModule == IntPtr.Zero)
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeLoadFailed,
                $"The approved primary module {MotionLibraryProbeVocabulary.PrimaryModuleFileName} " +
                "could not be loaded from its approved location with the restricted search set.",
                nativeHandlesReleased: true);
        }

        if (!ProveLoadedModulePath(primaryModule, primaryPath, MotionLibraryProbeVocabulary.PrimaryModuleFileName, out refusal))
        {
            return refusal;
        }

        // 8. Exactly one export is ever resolved: the authorized version query.
        var export = _kernel.ResolveExport(primaryModule, MotionLibraryProbeVocabulary.AuthorizedExportName);
        if (export == IntPtr.Zero)
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeExportNotFound,
                $"The authorized export {MotionLibraryProbeVocabulary.AuthorizedExportName} is absent " +
                "from the loaded approved module. No other export is ever resolved by this boundary.",
                nativeHandlesReleased: false);
        }

        // 9./10. One invocation, bounded buffer, version proof.
        if (!InvokeVersion(export, out var version, out refusal))
        {
            return refusal;
        }

        // 11. Deterministic unload: primary first, companion last. An unload failure after every
        // other proof passed is itself a refusal: success is never claimed with a live handle.
        if (!ReleaseModules(ref primaryModule, ref companionModule))
        {
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeUnloadFailed,
                "Every proof passed, but a native module handle could not be released " +
                "deterministically. The probe does not report success with an unreleased handle.",
                nativeHandlesReleased: false);
        }

        return MotionLibraryProbeOutcome.Complete(
            version!,
            "The approved modules loaded from the approved directory; identities, x64 architecture, " +
            "authorized export and module paths were proven; the reported version equals the " +
            "Owner-configured expected version; every native handle was released. No controller " +
            "was opened, discovered, read or commanded.");
    }

    /// <summary>File existence was proven; now prove the SHA-256 identity against the expected value.</summary>
    private bool VerifyFileIdentity(
        string path, byte[] expectedHash, string moduleFileName, out MotionLibraryProbeOutcome refusal)
    {
        refusal = null!;
        if (!NativeFileInspection.TryComputeSha256(path, out var actualHash))
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeFileReadFailed,
                $"The approved module {moduleFileName} could not be read, so its identity could not " +
                "be verified. An unverifiable identity is a refusal, never an acceptance.",
                nativeHandlesReleased: true);
            return false;
        }

        if (!NativeFileInspection.HashesEqual(expectedHash, actualHash))
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeFileIdentityMismatch,
                $"The identity of {moduleFileName} does not equal the Owner-configured expected " +
                "identity. Hash values are never rendered by this boundary; compare the " +
                "Owner-local evidence values outside the repository.",
                nativeHandlesReleased: true);
            return false;
        }

        return true;
    }

    /// <summary>Prove the image's PE machine is the approved x64 architecture before any load.</summary>
    private bool VerifyX64(string path, string moduleFileName, out MotionLibraryProbeOutcome refusal)
    {
        refusal = null!;
        byte[] prefix;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[4096];
            var read = stream.Read(buffer, 0, buffer.Length);
            prefix = buffer[..read];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeFileReadFailed,
                $"The approved module {moduleFileName} could not be read, so its architecture could " +
                "not be verified. An unverifiable architecture is a refusal, never an acceptance.",
                nativeHandlesReleased: true);
            return false;
        }

        if (!NativeFileInspection.TryReadPeMachine(prefix, out var machine))
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeArchitectureMismatch,
                $"The approved module {moduleFileName} is not a provable PE image: the architecture " +
                "proof requires a machine value and none could be read.",
                nativeHandlesReleased: true);
            return false;
        }

        if (machine != NativeFileInspection.PeMachineX64)
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeArchitectureMismatch,
                $"The approved module {moduleFileName} reports PE machine 0x{machine:X4}, which is " +
                "not the approved x64 (0x8664). Only the approved x64 native directory is approved.",
                nativeHandlesReleased: true);
            return false;
        }

        return true;
    }

    /// <summary>Prove the loaded module came from the approved file — never from a same-named copy elsewhere.</summary>
    private bool ProveLoadedModulePath(
        IntPtr module, string approvedPath, string moduleFileName, out MotionLibraryProbeOutcome refusal)
    {
        refusal = null!;
        var loadedPath = _kernel.GetLoadedModulePath(module);
        if (loadedPath is null)
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeModulePathMismatch,
                $"The loaded path of {moduleFileName} could not be determined, so it cannot be " +
                "proven equal to the approved file. An unproven module path is a refusal.",
                nativeHandlesReleased: false);
            return false;
        }

        var normalizedLoaded = Path.GetFullPath(loadedPath);
        if (!string.Equals(normalizedLoaded, approvedPath, StringComparison.OrdinalIgnoreCase))
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeModulePathMismatch,
                $"The loaded {moduleFileName} does not come from the approved file. The boundary " +
                "never reports the substituted location; the module was refused.",
                nativeHandlesReleased: false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// One invocation of the authorized version export with the bounded, zero-filled buffer.
    /// Success is defined ONLY by the buffer: a terminated, printable version string within the
    /// bound that equals the Owner-configured expected version. The native result integer is
    /// never interpreted: no vendor error constant is inferred by this boundary.
    /// </summary>
    private bool InvokeVersion(IntPtr export, out string? version, out MotionLibraryProbeOutcome refusal)
    {
        version = null;
        refusal = null!;

        var buffer = new byte[MotionLibraryProbeVocabulary.VersionBufferSize];
        Array.Clear(buffer, 0, buffer.Length);

        _ = _kernel.InvokeAuthorizedVersionExport(export, buffer);

        var terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator <= 0)
        {
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeVersionCallFailed,
                "The authorized version export produced no terminated version string inside the " +
                "bounded buffer. An absent or unterminated value is a refusal, never a guess.",
                nativeHandlesReleased: false);
            return false;
        }

        for (var index = 0; index < terminator; index++)
        {
            if (buffer[index] is < 0x20 or > 0x7E)
            {
                refusal = MotionLibraryProbeOutcome.Refused(
                    MotionLibraryProbeVocabulary.NativeVersionCallFailed,
                    "The authorized version export produced a value that is not a printable version " +
                    "string inside the bounded buffer. An unreadable value is a refusal, never a guess.",
                    nativeHandlesReleased: false);
                return false;
            }
        }

        version = System.Text.Encoding.ASCII.GetString(buffer, 0, terminator);
        if (!string.Equals(version, _configuration.ExpectedVersion, StringComparison.Ordinal))
        {
            version = null;
            refusal = MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeVersionMismatch,
                "The version reported by the authorized export does not equal the Owner-configured " +
                "expected version. The values are never rendered by this boundary; compare the " +
                "Owner-local evidence outside the repository.",
                nativeHandlesReleased: false);
            return false;
        }

        return true;
    }

    /// <summary>Release both module handles in reverse load order. False when any release failed.</summary>
    private bool ReleaseModules(ref IntPtr primaryModule, ref IntPtr companionModule)
    {
        var released = true;
        if (primaryModule != IntPtr.Zero)
        {
            released = _kernel.ReleaseModule(primaryModule) && released;
            primaryModule = IntPtr.Zero;
        }

        if (companionModule != IntPtr.Zero)
        {
            released = _kernel.ReleaseModule(companionModule) && released;
            companionModule = IntPtr.Zero;
        }

        return released;
    }
}
