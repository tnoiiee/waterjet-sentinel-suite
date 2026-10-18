namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>One raw observation of one axis: the native value, when and in which generation it was read.</summary>
public sealed class ManualMotionReadingSnapshot
{
    /// <summary>The native value, or null when no value was ever read in this session.</summary>
    public decimal? Value { get; init; }

    /// <summary>When the value was read, or null when no value was ever read.</summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>True only when the value was read in the current generation and is within the freshness window.</summary>
    public bool Fresh { get; init; }

    /// <summary>The code of the last refused read of this kind, or empty when the last read succeeded.</summary>
    public string LastRefusalCode { get; init; } = string.Empty;
}

/// <summary>
/// The published state of one axis. Raw observations are kept separate from interpretation, and no
/// member claims a physical fact: there is no safe, stopped, brake-applied, drive-ready or
/// in-position member.
/// </summary>
public sealed class ManualMotionAxisSnapshot
{
    /// <summary>The closed axis.</summary>
    public required ManualMotionAxis Axis { get; init; }

    /// <summary>The diagnostic controller axis letter.</summary>
    public required string AxisLetter { get; init; }

    /// <summary>The Operator role wording: Horizontal or Vertical.</summary>
    public required string RoleLabel { get; init; }

    /// <summary>True when the Owner-local profile lists this axis.</summary>
    public required bool AllowedByProfile { get; init; }

    /// <summary>The brake COMMAND state (never the physical brake).</summary>
    public required ManualMotionBrakeCommandState BrakeCommandState { get; init; }

    /// <summary>The raw logical state of the configured brake output, UNCONFIRMED.</summary>
    public required ManualMotionReadingSnapshot BrakeOutput { get; init; }

    /// <summary>The raw status word.</summary>
    public required ManualMotionReadingSnapshot StatusWord { get; init; }

    /// <summary>The motion interpretation of the status word under the Owner-local profile.</summary>
    public required ManualMotionInterpretation Motion { get; init; }

    /// <summary>The home-input interpretation of the status word under the Owner-local profile.</summary>
    public required ManualMotionInterpretation HomeInput { get; init; }

    /// <summary>True when both status bits are configured; the interpretation basis is then OWNER-ATTESTED, never vendor-verified.</summary>
    public required bool InterpretationOwnerAttested { get; init; }

    /// <summary>The first raw position read (TP), label unresolved.</summary>
    public required ManualMotionReadingSnapshot PositionTp { get; init; }

    /// <summary>The second raw position read (RP), label unresolved.</summary>
    public required ManualMotionReadingSnapshot PositionRp { get; init; }

    /// <summary>The session Home state.</summary>
    public required ManualMotionHomeState HomeState { get; init; }

    /// <summary>The define-position-zero coordinate state.</summary>
    public required ManualMotionCoordinateState CoordinateState { get; init; }

    /// <summary>The define-dual-zero state; its effect is never verified.</summary>
    public required ManualMotionDualZeroState DualZeroState { get; init; }

    /// <summary>The Stop dispatch state.</summary>
    public required ManualAxisStopState StopState { get; init; }

    /// <summary>
    /// Always false: a dispatched Stop is never a confirmed stop in this scope. The property has no
    /// setter and no initializer path, so no producer can publish true.
    /// </summary>
    public bool StopConfirmed { get; }

    /// <summary>The outcome code of the last operation on this axis.</summary>
    public required string LastOutcomeCode { get; init; }

    /// <summary>The profile items that are NOT_CONFIGURED for this axis.</summary>
    public required IReadOnlyList<string> NotConfigured { get; init; }
}

/// <summary>The published state of the single active operation.</summary>
public sealed class ManualMotionOperationSnapshot
{
    /// <summary>The session-local operation number.</summary>
    public required long Id { get; init; }

    /// <summary>The operation kind.</summary>
    public required ManualMotionOperationKind Kind { get; init; }

    /// <summary>The closed axis of the operation.</summary>
    public required ManualMotionAxis Axis { get; init; }

    /// <summary>The current phase.</summary>
    public required ManualMotionOperationPhase Phase { get; init; }

    /// <summary>When the operation started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>True once a typed Stop was requested for this operation.</summary>
    public required bool StopRequested { get; init; }
}

/// <summary>One redacted session log entry: codes and fixed texts only.</summary>
public sealed class ManualMotionLogEntry
{
    /// <summary>The session-local entry number.</summary>
    public required long Sequence { get; init; }

    /// <summary>When the entry was recorded.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The axis the entry refers to, or null for a session-level entry.</summary>
    public ManualMotionAxis? Axis { get; init; }

    /// <summary>The operation the entry refers to.</summary>
    public required ManualMotionOperationKind Operation { get; init; }

    /// <summary>The canonical code.</summary>
    public required string Code { get; init; }

    /// <summary>The fixed redacted detail text of the code.</summary>
    public required string Detail { get; init; }
}

/// <summary>
/// The immutable published state of a session, safe to read from any thread. The mode identity is
/// part of every snapshot so a simulated session can never be presented as a hardware session.
/// </summary>
public sealed class ManualMotionSnapshot
{
    /// <summary>The port identity.</summary>
    public required ManualMotionPortMode Mode { get; init; }

    /// <summary>The session lifecycle state.</summary>
    public required ManualMotionSessionState State { get; init; }

    /// <summary>The session generation. It advances on open, on fault and on close.</summary>
    public required long Generation { get; init; }

    /// <summary>The source of the accepted profile, or null when no profile was accepted.</summary>
    public ManualMotionProfileSource? ProfileSource { get; init; }

    /// <summary>The profile result code.</summary>
    public required string ProfileCode { get; init; }

    /// <summary>The code of the port open, or empty before the open.</summary>
    public required string OpenCode { get; init; }

    /// <summary>The code of the port close, or empty before the close.</summary>
    public required string CloseCode { get; init; }

    /// <summary>The active operation, or null.</summary>
    public ManualMotionOperationSnapshot? ActiveOperation { get; init; }

    /// <summary>Axis A then Axis B.</summary>
    public required IReadOnlyList<ManualMotionAxisSnapshot> Axes { get; init; }

    /// <summary>The most recent redacted log entries, oldest first.</summary>
    public required IReadOnlyList<ManualMotionLogEntry> Log { get; init; }

    /// <summary>
    /// True once the session can do nothing further: closed, refused before opening, or refused at
    /// the open with its cleanup close already performed.
    /// </summary>
    public bool IsTerminal =>
        State == ManualMotionSessionState.Closed
        || State == ManualMotionSessionState.ProfileRefused
        || (State == ManualMotionSessionState.OpenRefused && CloseCode.Length > 0);
}
