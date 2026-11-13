using Wjss.Adapters.MotionLibrary;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;

namespace Wjss.MotionComposition;

/// <summary>
/// The structured result of one motion shutdown.
///
/// It preserves every failure separately: Stop requests that were refused, axes that were still
/// active after the bounded Stop window, the final session state and the native close code. It is
/// never collapsed into a single boolean and it never claims a clean shutdown it did not observe.
/// </summary>
public sealed record MotionCleanupRecord
{
    /// <summary>True when a shutdown was actually attempted.</summary>
    public required bool Attempted { get; init; }

    /// <summary>The axes one typed Stop was requested on, in A/B/C/D order.</summary>
    public IReadOnlyList<string> StopRequestedAxes { get; init; } = Array.Empty<string>();

    /// <summary>The axes that still reported an active operation after the bounded Stop window.</summary>
    public IReadOnlyList<string> StillActiveAxes { get; init; } = Array.Empty<string>();

    /// <summary>The session lifecycle state observed after the shutdown.</summary>
    public required string SessionState { get; init; }

    /// <summary>The canonical port close code, or a refusal code when the close never ran.</summary>
    public required string CloseCode { get; init; }

    /// <summary>Per-stage failures, in stable stage order. Empty when every stage was observed clean.</summary>
    public IReadOnlyDictionary<string, string> Failures { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// True only when every requested Stop was issued, no axis remained active and the session
    /// reached a terminal non-driving state. A faulted or still-closing session is not complete.
    /// </summary>
    public bool Complete =>
        Attempted
        && Failures.Count == 0
        && StillActiveAxes.Count == 0
        && (string.Equals(SessionState, "CLOSED", StringComparison.Ordinal)
            || string.Equals(SessionState, "CLOSED_BEFORE_OPEN", StringComparison.Ordinal));
}

/// <summary>
/// The one shared Manual Motion composition: the existing controller owner, the existing
/// hardware-capable port (source-linked, never re-implemented) and the existing Application
/// session over them.
///
/// Construction performs NO native load, NO open, NO read, NO command and NO network contact. The
/// adapter consumes the one-use Owner consent and validates its own endpoint and Gate A values,
/// then stops. The port opens only when <see cref="ManualMotionSession"/> is driven by a caller
/// that already holds the trial authority.
/// </summary>
public sealed class MotionCompositionRoot
{
    /// <summary>Returned in the refusal slot when the composition itself could not be constructed.</summary>
    public const string CompositionRefused = "MOTION_COMPOSITION_REFUSED";

    /// <summary>Returned in the success slot when the composition was constructed with no contact.</summary>
    public const string CompositionComposed = "MOTION_COMPOSITION_COMPOSED";

    /// <summary>Stage names carried in <see cref="MotionCleanupRecord.Failures"/>.</summary>
    public const string StageStop = "STOP";
    public const string StageClose = "CLOSE";
    public const string StageNativeClose = "NATIVE_CLOSE";

    private readonly ManualMotionControllerOwner _owner;
    private readonly IManualMotionPort _port;
    private readonly ManualMotionSession _session;
    private readonly string _cabinetId;

    private MotionCompositionRoot(
        ManualMotionControllerOwner owner,
        IManualMotionPort port,
        ManualMotionSession session,
        string profileCode,
        string cabinetId)
    {
        _owner = owner;
        _port = port;
        _session = session;
        ProfileCode = profileCode;
        _cabinetId = cabinetId;
    }

    /// <summary>The Application session over the hardware-capable port.</summary>
    public ManualMotionSession Session => _session;

    /// <summary>The port identity, so a caller can never claim a hardware path it did not compose.</summary>
    public ManualMotionPortMode Mode => _port.Mode;

    /// <summary>The accepted profile code (a code only; never a value from the Owner-local file).</summary>
    public string ProfileCode { get; }

    /// <summary>
    /// The cabinet identity the accepted Owner-local profile is bound to, read from the profile itself
    /// (<see cref="ManualMotionProfile.ControllerId"/>) and never from a file name, a path or a caller's
    /// claim. The WSB1/WSB2/WSB3/WSB4 cabinets all expose the same four axis letters, so the letters prove
    /// nothing on their own: this is what lets a caller verify the profile belongs to the cabinet its
    /// Water Jet is wired to, before any session is created.
    /// </summary>
    public string CabinetId => _cabinetId;

    /// <summary>True only for the hardware-capable port. A simulated port is never reported as this.</summary>
    public bool HardwareCapable => _port.Mode == ManualMotionPortMode.HardwareCapable;

    /// <summary>
    /// Composes the owner, the port and the session from the Owner-local external source only.
    ///
    /// Every step is fail-closed and every refusal is typed. A refused profile is a refusal, never a
    /// silent substitution with the built-in simulation profile, so a dry Hardware trial can never
    /// run against simulated motion while claiming to be physical.
    /// </summary>
    public static bool TryCreate(
        Func<string, string?> readEnvironment,
        IClock clock,
        out MotionCompositionRoot? root,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(clock);

        root = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        // 1. The existing invocation seam: consent, then the existing native and endpoint
        //    configuration. Nothing is loaded and nothing is opened here. The reader and the
        //    one-use consent removal are passed in so this stays testable without a real bench.
        ManualMotionControllerOwner? owner;
        string invocationCode;
        try
        {
            bool created = ManualMotionControllerInvocation.TryCreate(
                readEnvironment,
                name => Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process),
                ManualMotionControllerOwner.CreateProduction,
                out owner,
                out invocationCode);
            if (!created || owner is null)
            {
                refusalCode = invocationCode;
                refusalDetail =
                    "The hardware-capable motion composition was refused before any native load: the "
                    + "Owner-local consent, endpoint or Gate A values are missing or were not accepted. "
                    + "No port is opened and no simulated port is substituted.";
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            refusalCode = CompositionRefused;
            refusalDetail =
                "The hardware-capable motion composition could not be constructed from the Owner-local "
                + "configuration. No port is opened and no simulated port is substituted.";
            return false;
        }

        // 2. The Owner-local profile. A missing, oversized, unparsable or refused profile refuses the
        //    composition. The built-in simulation profile is NEVER substituted here.
        ManualMotionProfileResult profile;
        try
        {
            profile = ManualMotionProfileFile.Load(readEnvironment);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            ReleaseQuietly(owner);
            refusalCode = ManualMotionCodes.ProfileFileRefused;
            refusalDetail =
                "The Owner-local motion profile could not be read. A refused profile is never replaced "
                + "by the built-in simulation profile.";
            return false;
        }

        if (!profile.Accepted || profile.Profile is null)
        {
            ReleaseQuietly(owner);
            refusalCode = profile.Code;
            refusalDetail =
                "The Owner-local motion profile was refused. A refused profile is never replaced by the "
                + "built-in simulation profile, so no dry Hardware trial can run on simulated motion.";
            return false;
        }

        // 3. The existing port over the existing owner, and the existing session over both.
        var port = new MotionLibraryManualMotionPort(owner);
        var session = new ManualMotionSession(port, profile, clock);
        root = new MotionCompositionRoot(owner, port, session, profile.Code, profile.Profile!.ControllerId);
        refusalCode = CompositionComposed;
        return true;
    }

    /// <summary>
    /// One typed Stop per axis, a bounded settle window, a session close and finally the native
    /// release. Every stage is captured separately: a refused Stop is reported as a Stop failure, an
    /// axis that stays active is reported as still active, and the native close code is preserved
    /// even when an earlier stage failed.
    ///
    /// This method reports rather than throws for every recoverable fault, and it is safe to call
    /// more than once. It does not claim a clean shutdown: <see cref="MotionCleanupRecord.Complete"/>
    /// is derived from what was actually observed.
    /// </summary>
    public MotionCleanupRecord Shutdown(int maximumSteps = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSteps);

        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        var requested = new List<string>();
        var stillActive = new List<string>();

        var state = _session.Snapshot.State;
        if (state is ManualMotionSessionState.NotStarted
            or ManualMotionSessionState.ProfileRefused
            or ManualMotionSessionState.OpenRefused)
        {
            // The port never opened: there is nothing to stop, but the native configuration is still
            // released so a later trial starts from a clean owner.
            string releaseCode = ReleaseQuietly(_owner, failures, StageNativeClose);
            return new MotionCleanupRecord
            {
                Attempted = true,
                SessionState = "CLOSED_BEFORE_OPEN",
                CloseCode = releaseCode,
                Failures = failures,
            };
        }

        foreach (ManualMotionAxis axis in new[]
                 {
                     ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D,
                 })
        {
            string label = axis.ToString();
            requested.Add(label);
            try
            {
                _session.RequestStop(axis);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                failures[StageStop + "/" + label] = ManualMotionControllerCodes.CleanupIncomplete;
            }
        }

        for (int step = 0; step < maximumSteps && _session.Snapshot.State == ManualMotionSessionState.Ready; step += 1)
        {
            try
            {
                _session.Step();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                failures[StageStop] = ManualMotionControllerCodes.CleanupIncomplete;
                break;
            }

            if (_session.Snapshot.ActiveOperations.Count == 0
                && _session.Snapshot.State == ManualMotionSessionState.Ready)
            {
                break;
            }
        }

        foreach (ManualMotionOperationSnapshot operation in _session.Snapshot.ActiveOperations)
        {
            stillActive.Add(operation.Axis.ToString());
        }

        try
        {
            _session.RequestClose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            failures[StageClose] = ManualMotionControllerCodes.CleanupIncomplete;
        }

        for (int step = 0; step < maximumSteps && _session.Snapshot.State != ManualMotionSessionState.Closed; step += 1)
        {
            try
            {
                _session.Step();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                failures[StageClose] = ManualMotionControllerCodes.CleanupIncomplete;
                break;
            }

            if (_session.Snapshot.State is ManualMotionSessionState.Faulted or ManualMotionSessionState.Closed)
            {
                break;
            }
        }

        string closeCode = ReleaseQuietly(_owner, failures, StageNativeClose);
        state = _session.Snapshot.State;

        return new MotionCleanupRecord
        {
            Attempted = true,
            StopRequestedAxes = requested,
            StillActiveAxes = stillActive,
            SessionState = state == ManualMotionSessionState.Closed ? "CLOSED" : state.ToString().ToUpperInvariant(),
            CloseCode = closeCode,
            Failures = failures,
        };
    }

    /// <summary>
    /// Releases the native configuration without ever letting a failure escape. The failure is
    /// recorded by the caller and never swallowed silently.
    /// </summary>
    private static string ReleaseQuietly(
        ManualMotionControllerOwner owner,
        Dictionary<string, string>? failures = null,
        string? stage = null)
    {
        try
        {
            return owner.Close().Code;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            if (failures is not null && stage is not null)
            {
                failures[stage] = ManualMotionControllerCodes.CleanupIncomplete;
            }

            return ManualMotionControllerCodes.CleanupIncomplete;
        }
    }
}
