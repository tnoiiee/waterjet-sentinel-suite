namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical, redacted vocabulary of the WSB1 Manual Motion Commissioning controller path. Codes
/// only: no command text, reply text, native result number, vendor error text, endpoint, path, hash
/// or consent value ever appears in a code or leaves this boundary.
///
/// There is deliberately no code that claims a physical effect: a dispatched command is reported as
/// dispatched, never as moved, stopped, braked or homed.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class ManualMotionControllerCodes
{
    /// <summary>The single controller open of this owner succeeded and every boundary was bound.</summary>
    internal const string Opened = "MANUAL_MOTION_CONTROLLER_OPENED";

    /// <summary>A second open was refused: one open per owner, nothing reconnects.</summary>
    internal const string OpenAlreadyPerformed = "MANUAL_MOTION_CONTROLLER_OPEN_ALREADY_PERFORMED";

    /// <summary>Gate A or the native-module proof refused; nothing was opened.</summary>
    internal const string NativeProofRefused = "MANUAL_MOTION_NATIVE_PROOF_REFUSED";

    /// <summary>One native dispatch completed with success and no refusal marker. Not a physical effect.</summary>
    internal const string Dispatched = "MANUAL_MOTION_COMMAND_DISPATCHED";

    /// <summary>The controller reply carried its refusal marker.</summary>
    internal const string RejectedByController = "MANUAL_MOTION_COMMAND_REJECTED_BY_CONTROLLER";

    /// <summary>A reply arrived that is not an established acknowledgement; no success is claimed.</summary>
    internal const string ReplyUnclassified = "MANUAL_MOTION_COMMAND_REPLY_UNCLASSIFIED";

    /// <summary>The native dispatch returned a non-success result or did not return cleanly.</summary>
    internal const string DispatchFailed = "MANUAL_MOTION_COMMAND_DISPATCH_FAILED";

    /// <summary>The owner is not open (before the open, after a refused open, or after the close).</summary>
    internal const string NotOpen = "MANUAL_MOTION_CONTROLLER_NOT_OPEN";

    /// <summary>A native result returned after the owner generation advanced; it is not accepted.</summary>
    internal const string ResultStaleRejected = "MANUAL_MOTION_RESULT_STALE_REJECTED";

    /// <summary>The axis is outside the closed A/B/C/D domain.</summary>
    internal const string AxisOutOfDomain = "MANUAL_MOTION_AXIS_OUT_OF_DOMAIN";

    /// <summary>The command kind is outside the closed plan.</summary>
    internal const string CommandOutOfDomain = "MANUAL_MOTION_COMMAND_OUT_OF_DOMAIN";

    /// <summary>The typed operand is outside the structural bounds of its kind.</summary>
    internal const string OperandOutOfRange = "MANUAL_MOTION_OPERAND_OUT_OF_RANGE";

    /// <summary>The brake output is outside the closed domain or does not match the axis allocation.</summary>
    internal const string OutputOutOfDomain = "MANUAL_MOTION_OUTPUT_OUT_OF_DOMAIN";

    /// <summary>The rendered command exceeds the fixed length bound.</summary>
    internal const string CommandTooLong = "MANUAL_MOTION_COMMAND_TOO_LONG";

    /// <summary>The dispatch boundary was invalidated before the call.</summary>
    internal const string BoundaryClosed = "MANUAL_MOTION_BOUNDARY_CLOSED";

    /// <summary>The dispatch boundary has no bound handle or export.</summary>
    internal const string BoundaryUnavailable = "MANUAL_MOTION_BOUNDARY_UNAVAILABLE";

    /// <summary>One bounded read produced one validated value.</summary>
    internal const string ReadAccepted = "MANUAL_MOTION_READ_ACCEPTED";

    /// <summary>The read value is outside the accepted shape of its kind; nothing was repaired.</summary>
    internal const string ReadValueOutOfRange = "MANUAL_MOTION_READ_VALUE_OUT_OF_RANGE";

    /// <summary>The read target is outside the closed WSB1 A/B/C/D read set.</summary>
    internal const string ReadTargetOutOfDomain = "MANUAL_MOTION_READ_TARGET_OUT_OF_DOMAIN";

    /// <summary>Invalidation, handle close and module release were all proven.</summary>
    internal const string CleanupComplete = "MANUAL_MOTION_CLEANUP_COMPLETE";

    /// <summary>At least one cleanup step was not proven.</summary>
    internal const string CleanupIncomplete = "MANUAL_MOTION_CLEANUP_INCOMPLETE";

    /// <summary>The Owner-local consent variable of the hardware-capable commissioning session.</summary>
    internal const string ConsentVariable = "WJSS_MANUAL_MOTION_CONSENT";

    /// <summary>The exact consent value. It is an explicit local confirmation, not a credential.</summary>
    internal const string ExactConsentValue = "AUTHORIZE_WSB1_MANUAL_MOTION_SESSION";

    /// <summary>The consent value matched exactly and was removed from the process.</summary>
    internal const string ConsentAccepted = "MANUAL_MOTION_CONSENT_ACCEPTED";

    /// <summary>No consent value was present.</summary>
    internal const string ConsentNotConfigured = "MANUAL_MOTION_CONSENT_NOT_CONFIGURED";

    /// <summary>A consent value was present but did not match exactly.</summary>
    internal const string ConsentRejected = "MANUAL_MOTION_CONSENT_REJECTED";

    /// <summary>The consent value could not be read or removed.</summary>
    internal const string ConsentConsumptionFailed = "MANUAL_MOTION_CONSENT_CONSUMPTION_FAILED";

    /// <summary>The Owner-local configuration could not be evaluated.</summary>
    internal const string PreconditionEvaluationFailed = "MANUAL_MOTION_PRECONDITION_EVALUATION_FAILED";
}

/// <summary>The closed WSB1 A/B/C/D read set of the commissioning path, mapped onto the existing typed B2 reads.</summary>
internal enum ManualMotionReadTarget
{
    /// <summary>The raw status word of Axis A.</summary>
    StatusA = 1,

    /// <summary>The raw status word of Axis B.</summary>
    StatusB = 2,

    /// <summary>The first raw position read (TP) of Axis A; label unresolved.</summary>
    TpA = 3,

    /// <summary>The first raw position read (TP) of Axis B; label unresolved.</summary>
    TpB = 4,

    /// <summary>The second raw position read (RP) of Axis A; label unresolved.</summary>
    RpA = 5,

    /// <summary>The second raw position read (RP) of Axis B; label unresolved.</summary>
    RpB = 6,

    /// <summary>The logical state of output 1 (the Axis A brake output), UNCONFIRMED.</summary>
    Output1 = 7,

    /// <summary>The logical state of output 2 (the Axis B brake output), UNCONFIRMED.</summary>
    Output2 = 8,

    /// <summary>The raw status word of Axis C.</summary>
    StatusC = 9,

    /// <summary>The raw status word of Axis D.</summary>
    StatusD = 10,

    /// <summary>The first raw position read (TP) of Axis C; label unresolved.</summary>
    TpC = 11,

    /// <summary>The first raw position read (TP) of Axis D; label unresolved.</summary>
    TpD = 12,

    /// <summary>The second raw position read (RP) of Axis C; label unresolved.</summary>
    RpC = 13,

    /// <summary>The second raw position read (RP) of Axis D; label unresolved.</summary>
    RpD = 14,

    /// <summary>The logical state of output 3 (the Axis C brake output), UNCONFIRMED.</summary>
    Output3 = 15,

    /// <summary>The logical state of output 4 (the Axis D brake output), UNCONFIRMED.</summary>
    Output4 = 16,
}

/// <summary>The redacted outcome of the single controller open.</summary>
internal sealed class ManualMotionOpenOutcome
{
    private ManualMotionOpenOutcome(bool opened, string code, string detailCode, long generation)
    {
        Opened = opened;
        Code = code;
        DetailCode = detailCode;
        Generation = generation;
    }

    /// <summary>True when the controller is open and every boundary is bound.</summary>
    internal bool Opened { get; }

    /// <summary>The canonical outcome code.</summary>
    internal string Code { get; }

    /// <summary>The canonical code of the refusing step (Gate A, load, path proof, export, direct mode or open), or empty.</summary>
    internal string DetailCode { get; }

    /// <summary>The owner generation after the open.</summary>
    internal long Generation { get; }

    internal static ManualMotionOpenOutcome Accepted(long generation) =>
        new(true, ManualMotionControllerCodes.Opened, string.Empty, generation);

    internal static ManualMotionOpenOutcome Refused(string code, string detailCode, long generation) =>
        new(false, code, detailCode, generation);
}

/// <summary>The redacted result of one dispatch (a plan command or the typed Stop).</summary>
/// <param name="Dispatched">True when the single dispatch completed with success and no refusal marker.</param>
/// <param name="Fault">True when the native dispatch failed (communication or native fault).</param>
/// <param name="NativeAttempted">True when a native call was made.</param>
/// <param name="Code">The canonical code.</param>
/// <param name="Generation">The owner generation the result belongs to.</param>
internal readonly record struct ManualMotionDispatchResult(
    bool Dispatched,
    bool Fault,
    bool NativeAttempted,
    string Code,
    long Generation);

/// <summary>The redacted result of one bounded read. A refused read carries no value.</summary>
/// <param name="Succeeded">True when one validated value was read.</param>
/// <param name="Fault">True when the native read failed.</param>
/// <param name="Value">The native value; meaningful only when <paramref name="Succeeded"/> is true.</param>
/// <param name="Code">The canonical code.</param>
/// <param name="Generation">The owner generation the result belongs to.</param>
internal readonly record struct ManualMotionReadResult(
    bool Succeeded,
    bool Fault,
    decimal Value,
    string Code,
    long Generation);

/// <summary>The measured cleanup truth of the owner close. Idempotent: the first outcome is kept.</summary>
internal sealed class ManualMotionCloseOutcome
{
    internal ManualMotionCloseOutcome(bool boundariesInvalidated, bool handleClosed, bool modulesReleased)
    {
        BoundariesInvalidated = boundariesInvalidated;
        HandleClosed = handleClosed;
        ModulesReleased = modulesReleased;
        Code = boundariesInvalidated && handleClosed && modulesReleased
            ? ManualMotionControllerCodes.CleanupComplete
            : ManualMotionControllerCodes.CleanupIncomplete;
    }

    /// <summary>True when every read, Stop and command boundary was invalidated before the close.</summary>
    internal bool BoundariesInvalidated { get; }

    /// <summary>True when the controller handle was closed and zeroed (or was never opened).</summary>
    internal bool HandleClosed { get; }

    /// <summary>True when every approved native module was released (or none was loaded).</summary>
    internal bool ModulesReleased { get; }

    /// <summary>The canonical cleanup code.</summary>
    internal string Code { get; }
}
