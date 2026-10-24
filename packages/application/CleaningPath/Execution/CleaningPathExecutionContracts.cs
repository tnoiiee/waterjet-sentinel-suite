using Wjss.Contracts;

namespace Wjss.Runtime.Core.CleaningPath.Execution;

/// <summary>
/// Phase 3B Simulation-only Cleaning Path execution contracts: the typed states, evidence and
/// boundary of the parallel-independent P1–P6 execution coordinator. The coordinator consumes an
/// accepted immutable Phase 3A snapshot and a successful Phase 3A resolver result and drives the
/// existing typed Manual Motion operations through one adapter-neutral boundary. It executes no
/// Centre return, no Pump or Isolation Valve behavior, no Queue dispatch and no Hardware action:
/// after verified P6 it ends in <see cref="CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn"/>
/// and exposes a typed handoff only.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public enum CleaningPathExecutionRole
{
    Horizontal = 1,
    Vertical = 2,
}

/// <summary>Per-axis execution states of one point (architecture-equivalent typed states).</summary>
public enum CleaningPathAxisExecutionState
{
    Pending = 0,
    Requested = 1,
    Running = 2,
    AwaitingStopped = 3,
    Verified = 4,
    Failed = 5,
    CleanupFailed = 6,
}

/// <summary>Combined point states (architecture-equivalent typed states).</summary>
public enum CleaningPathPointState
{
    Pending = 0,
    Starting = 1,
    Running = 2,
    Verified = 3,
    Failed = 4,
    CleanupPending = 5,
    CleanupFailed = 6,
}

/// <summary>Path-level execution states. The only success terminal is the Safe Return handoff.</summary>
public enum CleaningPathExecutionStatus
{
    NotStarted = 0,
    Running = 1,
    Aborting = 2,
    Closing = 3,
    PathCompleteAwaitingSafeReturn = 4,
    PathFailed = 5,
}

/// <summary>Coordinate-truth status carried by the Safe Return handoff.</summary>
public enum CleaningPathCoordinateTruth
{
    Unknown = 0,
    Verified = 1,
    Degraded = 2,
}

/// <summary>Combined cleanup outcome, preserved separately from any original failure cause.</summary>
public enum CleaningPathCleanupState
{
    NotNeeded = 0,
    Succeeded = 1,
    Partial = 2,
    Failed = 3,
}

/// <summary>The interpreted Motion bit state of one axis (never a fabricated observation).</summary>
public enum CleaningPathMotionBitState
{
    Unknown = 0,
    Active = 1,
    Inactive = 2,
}

/// <summary>The Stop supervision state of one axis as observed through the typed boundary.</summary>
public enum CleaningPathStopState
{
    None = 0,
    Issued = 1,
    Refused = 2,
    NotObservedWithinTimeout = 3,
}

/// <summary>
/// The adapter-neutral per-axis operation outcome vocabulary. The Manual Motion boundary maps the
/// existing typed session codes onto exactly these values and keeps the original code as source
/// provenance; the coordinator never compares raw code strings.
/// </summary>
public enum CleaningPathMotionOutcome
{
    None = 0,
    OperationStarted = 1,
    MoveCompletedPositionVerified = 2,
    MoveTimeout = 3,
    MoveAbortedByOperator = 4,
    MoveAbortedSessionClosing = 5,
    MotionCommandRefused = 6,
    AbortedBeforeMotion = 7,
    BrakeApplyRefused = 8,
    BrakeApplyCommanded = 9,
    StopIssued = 10,
    StopRefused = 11,
    StopNotObservedWithinTimeout = 12,
    CommunicationFault = 13,
    StaleResultRejected = 14,
    SessionFaulted = 15,
    Other = 99,
}

/// <summary>The fixed, redacted Phase 3B execution code vocabulary.</summary>
public static class CleaningPathExecutionCodes
{
    // ------------------------------------------------------------------ start refusals
    public const string StartRefusedSnapshotNotAccepted = "START_REFUSED_SNAPSHOT_NOT_ACCEPTED";
    public const string StartRefusedResolutionNotAccepted = "START_REFUSED_RESOLUTION_NOT_ACCEPTED";
    public const string StartRefusedSensorMismatch = "START_REFUSED_SENSOR_MISMATCH";
    public const string StartRefusedPathMissing = "START_REFUSED_PATH_MISSING";
    public const string StartRefusedPointSetInvalid = "START_REFUSED_POINT_SET_INVALID";
    public const string StartRefusedTargetOutOfRange = "START_REFUSED_TARGET_OUT_OF_RANGE";
    public const string StartRefusedAxesNotDistinct = "START_REFUSED_AXES_NOT_DISTINCT";
    public const string StartRefusedAxisNotAllowed = "START_REFUSED_AXIS_NOT_ALLOWED";
    public const string StartRefusedAxisNotMoveReady = "START_REFUSED_AXIS_NOT_MOVE_READY";
    public const string StartRefusedUnresolvedOperation = "START_REFUSED_UNRESOLVED_OPERATION";
    public const string StartRefusedExecutionActive = "START_REFUSED_EXECUTION_ACTIVE";

    /// <summary>START_REFUSED when the execution evidence window cannot be established safely.</summary>
    public const string StartRefusedEvidenceWindowUnavailable = "START_REFUSED_EVIDENCE_WINDOW_UNAVAILABLE";

    // ------------------------------------------------------------------ execution evidence kinds
    public const string KindExecutionStarted = "EXECUTION_STARTED";
    public const string KindMoveRequested = "MOVE_REQUESTED";
    public const string KindMoveAccepted = "MOVE_ACCEPTED";
    public const string KindExecutionPhaseVerified = "EXECUTION_PHASE_VERIFIED";
    public const string KindPointFailed = "POINT_FAILED";
    public const string KindStopRequested = "STOP_REQUESTED";
    public const string KindCleanupRecorded = "CLEANUP_RECORDED";
    public const string KindAbortRequested = "ABORT_REQUESTED";
    public const string KindCloseRequested = "CLOSE_REQUESTED";
    public const string KindPathCompleted = "PATH_COMPLETED";
    public const string KindPathFailed = "PATH_FAILED";

    // ------------------------------------------------------------------ refusal and failure codes
    public const string VerificationRefusedObservationMismatch = "VERIFICATION_REFUSED_OBSERVATION_MISMATCH";
    public const string PointCleanupFailed = "POINT_CLEANUP_FAILED";
    public const string PathCompleteAwaitingSafeReturn = "PATH_COMPLETE_AWAITING_SAFE_RETURN";
    public const string PathFailedAxisFailure = "PATH_FAILED_AXIS_FAILURE";
    public const string PathFailedPointCleanup = "PATH_FAILED_POINT_CLEANUP";
    public const string PathAborted = "PATH_ABORTED";
    public const string PathClosed = "PATH_CLOSED";
    public const string PathSessionClosing = "PATH_SESSION_CLOSING";
}

/// <summary>The typed start outcome: refused starts carry a stable code and leave the machine untouched.</summary>
public sealed record CleaningPathExecutionStartOutcome(bool Started, string Code, CleaningPathExecutionSnapshot? Execution);

/// <summary>One immutable path point target (native units) of the executed Sensor-owned path.</summary>
public sealed record CleaningPathPointTarget(JobPhase PointId, int HorizontalNative, int VerticalNative);

/// <summary>
/// One typed execution evidence entry. <see cref="SourceCode"/> preserves the originating boundary
/// code for provenance; the coordinator's own kinds and codes are the authority for execution state.
/// </summary>
public sealed record CleaningPathExecutionEvent(
    long Sequence,
    DateTimeOffset At,
    string Kind,
    string Code,
    JobPhase? PointId,
    CleaningPathExecutionRole? Role,
    string? SourceCode);

/// <summary>The per-axis execution record of the current or a finished point.</summary>
public sealed record CleaningPathAxisExecutionRecord(
    CleaningPathExecutionRole Role,
    string AxisId,
    string BrakeOutputId,
    CleaningPathAxisExecutionState State,
    int? RequestedTargetNative,
    CleaningPathMotionOutcome MotionOutcome,
    string? MotionOutcomeSourceCode,
    CleaningPathMotionOutcome CleanupOutcome,
    string? CleanupSourceCode,
    bool StopRequestedOnce,
    bool MoveAcceptedObserved);

/// <summary>The combined record of one path point: both per-axis results are preserved side by side.</summary>
public sealed record CleaningPathPointRecord(
    JobPhase PointId,
    int HorizontalNative,
    int VerticalNative,
    CleaningPathPointState State,
    CleaningPathAxisExecutionRecord Horizontal,
    CleaningPathAxisExecutionRecord Vertical,
    string? FailureCode,
    string? FailureSourceCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? VerifiedAt);

/// <summary>
/// The immutable execution snapshot captured at Start and republished after every coordinator step.
/// Paired IV / WSB / controller / axis / brake identities are carried for evidence only: the
/// canonical topology catalogs remain the sole authority and nothing here is a second topology.
/// </summary>
public sealed record CleaningPathExecutionSnapshot
{
    public required string ExecutionId { get; init; }
    public required string SensorId { get; init; }
    public required string CanonicalWaterJetId { get; init; }
    public string? RequestedOverrideWaterJetId { get; init; }
    public required string EffectiveWaterJetId { get; init; }
    public required EffectiveWaterJetProvenance EffectiveWjProvenance { get; init; }
    public required string DerivedIsolationValveId { get; init; }
    public required string CabinetId { get; init; }
    public required string ControllerId { get; init; }
    public required string HorizontalAxisId { get; init; }
    public required string VerticalAxisId { get; init; }
    public required string HorizontalBrakeOutputId { get; init; }
    public required string VerticalBrakeOutputId { get; init; }
    public required IReadOnlyList<CleaningPathPointTarget> Targets { get; init; }
    public required int CurrentPointIndex { get; init; }
    public required IReadOnlyList<JobPhase> VerifiedLedger { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required long EvidenceSequence { get; init; }
    public required CleaningPathExecutionStatus Status { get; init; }
    public required CleaningPathPointRecord CurrentPoint { get; init; }
    public required CleaningPathCleanupState CleanupState { get; init; }
    public string? FailureCode { get; init; }
}

/// <summary>
/// The typed post-P6 handoff. It names the fixed Centre contract only; the return to Centre is a
/// later mandatory motion lifecycle and is never dispatched in Phase 3B.
/// </summary>
public sealed record CleaningPathExecutionHandoff(
    string ExecutionId,
    string SensorId,
    string EffectiveWaterJetId,
    string HorizontalAxisId,
    string VerticalAxisId,
    CleaningPathMotionOutcome HorizontalFinalOutcome,
    CleaningPathMotionOutcome VerticalFinalOutcome,
    int HorizontalCentreNative,
    int VerticalCentreNative,
    CleaningPathCoordinateTruth CoordinateTruth,
    CleaningPathCleanupState CleanupState,
    DateTimeOffset CompletedAt);

/// <summary>The final typed execution result (null until the execution reaches a terminal state).</summary>
public sealed record CleaningPathExecutionResult(
    CleaningPathExecutionStatus Status,
    string Code,
    CleaningPathExecutionHandoff? Handoff,
    IReadOnlyList<CleaningPathPointRecord> Points,
    IReadOnlyList<CleaningPathExecutionEvent> Events,
    CleaningPathCleanupState CleanupState);

/// <summary>The typed observation frame of one axis as seen through the motion boundary.</summary>
public sealed record CleaningPathMotionAxisFrame(
    CleaningPathExecutionRole Role,
    bool MoveActive,
    bool StopRequested,
    CleaningPathMotionOutcome LastOutcome,
    string? LastOutcomeSourceCode,
    CleaningPathMotionBitState Motion,
    decimal? PositionTp,
    decimal? PositionRp,
    bool TpFresh,
    bool RpFresh,
    bool IdleObserved,
    CleaningPathStopState StopState,
    bool BrakeApplyCommanded);

/// <summary>One boundary event translated from the typed Manual Motion log (source sequence preserved).</summary>
public sealed record CleaningPathMotionEvent(
    long SourceSequence,
    DateTimeOffset At,
    CleaningPathExecutionRole Role,
    CleaningPathMotionOutcome Outcome,
    string SourceCode);

/// <summary>
/// The adapter-neutral operation boundary over the existing typed Manual Motion service. The
/// coordinator requests Move and Stop lifecycles and observes typed per-axis results only; the
/// boundary never exposes raw commands and the coordinator never touches a controller, a Pump, an
/// Isolation Valve or a Queue.
/// </summary>
public interface ICleaningPathMotionBoundary
{
    /// <summary>True when the underlying session is ready for typed operations.</summary>
    bool SessionReady { get; }

    /// <summary>True when the underlying session is faulted; no new operation can succeed.</summary>
    bool SessionFaulted { get; }

    /// <summary>True when both roles are permitted for Move by the existing acceptance rules.</summary>
    bool AxisAllowed(CleaningPathExecutionRole role);

    /// <summary>True when the role's axis is Move-ready: Homed, idle and with fresh current observations.</summary>
    bool AxisMoveReady(CleaningPathExecutionRole role);

    /// <summary>
    /// Begins the typed execution evidence window of one Cleaning Path execution. The current
    /// Manual Motion log sequence becomes the historical baseline: every earlier entry stays in the
    /// Manual Motion history unchanged but is unavailable to this execution, sticky per-axis
    /// outcomes that existed before the window can never be adopted as a current Move result, and
    /// only Move / Move-cleanup-Stop / session-fault evidence generated after the window flows to
    /// the coordinator. Returns false when the window cannot be established safely; the caller must
    /// then refuse the start fail-closed and request no axis Move.
    /// </summary>
    bool BeginExecutionEvidenceWindow();

    /// <summary>The current typed observation frame of one role.</summary>
    CleaningPathMotionAxisFrame Frame(CleaningPathExecutionRole role);

    /// <summary>Drains the typed events observed since the previous call (source order preserved).</summary>
    IReadOnlyList<CleaningPathMotionEvent> DrainEvents();

    /// <summary>Requests one absolute Move of one role's axis; the request is typed and per-axis.</summary>
    void RequestMove(CleaningPathExecutionRole role, int targetNative);

    /// <summary>Requests the typed Stop of one role's axis; exactly once semantics live in the session.</summary>
    void RequestStop(CleaningPathExecutionRole role);

    /// <summary>Requests the session close: every active motion operation is stopped first.</summary>
    void RequestClose();

    /// <summary>Advances the underlying session by one driver step (one serialized port call at most).</summary>
    void Step();
}

/// <summary>
/// Optional typed point-advance gate for composed executions (Phase 3C composition). The execution
/// coordinator consults it before starting any point after the first: while it withholds allowance
/// the coordinator holds between verified points and requests no Move. A null gate preserves the
/// standalone Phase 3B behavior exactly (the next point starts immediately after both axes verify
/// the current one). The gate never observes or mutates motion state and never extends the
/// P1–P6 point set.
/// </summary>
public interface ICleaningPathPointAdvanceGate
{
    /// <summary>True when the coordinator may request the two Moves of the next point.</summary>
    bool PointStartAllowed(JobPhase pointId);
}
