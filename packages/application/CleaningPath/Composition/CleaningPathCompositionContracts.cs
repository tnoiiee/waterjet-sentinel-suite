using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// Phase 3C composition contracts (Simulation only): the typed Pump-ready gate, the paired
// Isolation Valve command/supervision vocabulary, the combined evidence sequence, the Safe Return
// handoff and the final composition result. Every value is a typed identity or an existing
// Sequencing / Cleaning Path contract; no raw device command string exists here and no hardware or
// native adapter type is referenced.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>Composition lifecycle states. Every terminal awaits Mandatory Safe Return.</summary>
public enum CleaningPathCompositionStatus
{
    NotStarted = 0,
    ExecutingPath = 1,
    SupervisingValveOpen = 2,
    SupervisingValveClose = 3,
    SupervisingValveCleanup = 4,
    CompleteAwaitingSafeReturn = 5,
    FailedAwaitingSafeReturn = 6,
    AbortedAwaitingSafeReturn = 7,
    ClosedAwaitingSafeReturn = 8,
}

/// <summary>The typed paired-IV command vocabulary (the only commands the composition issues).</summary>
public enum CleaningPathValveCommandKind
{
    Open = 1,
    Close = 2,
}

/// <summary>
/// Stable machine codes for the Phase 3C composition. Pump-ready, threshold and Valve diagnosis
/// identities are reused from the existing Sequencing vocabulary and are never duplicated here.
/// </summary>
public static class CleaningPathCompositionCodes
{
    // Composition terminals (every one awaits Mandatory Safe Return; none means a completed
    // Cleaning Job and none means a completed Safe Return).
    public const string PathAndValveSequenceCompleteAwaitingSafeReturn = "PATH_AND_VALVE_SEQUENCE_COMPLETE_AWAITING_SAFE_RETURN";
    public const string CompositionFailedAwaitingSafeReturn = "COMPOSITION_FAILED_AWAITING_SAFE_RETURN";
    public const string CompositionAbortedAwaitingSafeReturn = "COMPOSITION_ABORTED_AWAITING_SAFE_RETURN";
    public const string CompositionClosedAwaitingSafeReturn = "COMPOSITION_CLOSED_AWAITING_SAFE_RETURN";

    // Pump-ready gate and start refusals. PUMP_NOT_READY, PUMP_PRESSURE_INPUT_INVALID and
    // INVALID_PRESSURE_THRESHOLDS are reused from the existing Sequencing vocabulary.
    public const string PumpReadyAccepted = "PUMP_READY_ACCEPTED";
    public const string StartRefusedCompositionActive = "START_REFUSED_COMPOSITION_ACTIVE";
    public const string StartRefusedValveSupervisionTimeoutInvalid = "START_REFUSED_VALVE_SUPERVISION_TIMEOUT_INVALID";

    // Paired-IV supervision identities.
    public const string ValveOpenAccepted = "VALVE_OPEN_ACCEPTED";

    // Requested terminal causes.
    public const string AbortRequested = "COMPOSITION_ABORT_REQUESTED";
    public const string CloseRequested = "COMPOSITION_CLOSE_REQUESTED";

    // Evidence kinds of the combined composition evidence sequence.
    public const string KindPumpReadinessEvaluated = "PUMP_READINESS_EVALUATED";
    public const string KindValveOpenRequested = "VALVE_OPEN_REQUESTED";
    public const string KindValveOpenSupervision = "VALVE_OPEN_SUPERVISION";
    public const string KindValveCloseRequested = "VALVE_CLOSE_REQUESTED";
    public const string KindValveCloseSupervision = "VALVE_CLOSE_SUPERVISION";
    public const string KindValveCleanupRequested = "VALVE_CLEANUP_REQUESTED";
    public const string KindValveCleanupSupervision = "VALVE_CLEANUP_SUPERVISION";
    public const string KindCompositionAbortRequested = "COMPOSITION_ABORT";
    public const string KindCompositionCloseRequested = "COMPOSITION_CLOSE";
    public const string KindCompositionTerminal = "COMPOSITION_TERMINAL";
}

/// <summary>
/// The immutable Phase 3C composition request: typed development thresholds and one typed
/// supervision policy. No device parameter and no Production configuration exists here.
/// </summary>
public sealed record CleaningPathCompositionRequest
{
    /// <summary>The existing typed pressure thresholds (Low, High and Pump-ready setpoint).</summary>
    public required SequencingPressureThresholds PressureThresholds { get; init; }

    /// <summary>The typed supervision window for one paired-IV open or close supervision.</summary>
    public required TimeSpan ValveSupervisionTimeout { get; init; }
}

/// <summary>
/// The resolved composition identity. Every field is derived by the existing Phase 3A resolver and
/// the canonical Equipment / Motion catalogs at resolution time: this record copies those accepted
/// derivations for evidence and is never a second topology authority. The paired Isolation Valve is
/// the WJn to IVn derivation already proven by the resolver.
/// </summary>
public sealed record CleaningPathCompositionIdentity(
    string SensorId,
    string CanonicalAssignedWaterJetId,
    string? RequestedOverrideWaterJetId,
    string EffectiveWaterJetId,
    EffectiveWaterJetProvenance EffectiveWjProvenance,
    string DerivedIsolationValveId,
    string CabinetId,
    string ControllerId,
    string HorizontalAxisId,
    string VerticalAxisId);

/// <summary>
/// The typed Pump-ready evidence: the exact observation (quality, source and value preserved) and
/// the fail-closed rule outcome. The Pump Inlet instrument is diagnostic and trend only and can
/// never appear here as a ready source.
/// </summary>
public sealed record CleaningPathPumpReadinessResult(bool Ready, string Code, PressureSample? Sample);

/// <summary>One typed paired-IV command request. The composition never carries a raw command string.</summary>
public sealed record CleaningPathValveCommandRequest(string ValveId, CleaningPathValveCommandKind Kind);

/// <summary>
/// One typed paired-IV supervision result: the existing classifier assessment plus the exact
/// evidence it consumed (pressure sample, limit observation and supervision-window state).
/// </summary>
public sealed record CleaningPathValveSupervisionResult(
    string ValveId,
    ValveAssessment Assessment,
    PressureSample? PressureEvidence,
    bool LimitDetected,
    bool TimedOut);

/// <summary>
/// Adapter-neutral typed equipment boundary for one composition. It exposes observations and the
/// paired-IV open/close commands only: there is no Pump command member, no Main Valve member and no
/// hardware or native adapter type anywhere in this contract.
/// </summary>
public interface ICleaningPathEquipmentBoundary
{
    /// <summary>Issues one typed paired-IV command request (open or close of exactly one valve).</summary>
    void RequestValve(CleaningPathValveCommandRequest request);

    /// <summary>Reads the Pump Outlet Pressure observation (AI-003 identity), or null when absent.</summary>
    PressureSample? ReadPumpOutletPressure();

    /// <summary>Reads one valve's outlet pressure observation, or null when absent.</summary>
    PressureSample? ReadValveOutletPressure(string valveId);

    /// <summary>True when the paired valve's Upper limit is detected.</summary>
    bool ReadUpperLimitDetected(string valveId);

    /// <summary>True when the paired valve's Lower limit is detected.</summary>
    bool ReadLowerLimitDetected(string valveId);
}

/// <summary>One entry of the combined composition evidence sequence.</summary>
public sealed record CleaningPathCompositionEvent(
    long Sequence,
    DateTimeOffset At,
    string Kind,
    string Code,
    JobPhase? PointId);

/// <summary>
/// The typed Mandatory Safe Return handoff published at every composition terminal. It preserves
/// the Pump, Valve and motion evidence separately and names the Centre contract only through the
/// Phase 3B motion handoff: no Centre movement and no Safe Return step is ever commanded here.
/// </summary>
public sealed record CleaningPathCompositionHandoff
{
    public required string CompositionId { get; init; }

    public required CleaningPathCompositionIdentity Identity { get; init; }

    public required CleaningPathCompositionStatus TerminalStatus { get; init; }

    public required string TerminalCode { get; init; }

    public required CleaningPathPumpReadinessResult PumpReadiness { get; init; }

    public CleaningPathValveSupervisionResult? ValveOpenSupervision { get; init; }

    public CleaningPathValveSupervisionResult? ValveCloseSupervision { get; init; }

    public CleaningPathValveSupervisionResult? ValveCleanupSupervision { get; init; }

    public required CleaningPathMotionOutcome HorizontalFinalOutcome { get; init; }

    public required CleaningPathMotionOutcome VerticalFinalOutcome { get; init; }

    /// <summary>The first primary failure cause, preserved unchanged (null on the success terminal).</summary>
    public string? PrimaryFailureCode { get; init; }

    public required CleaningPathCleanupState MotionCleanupState { get; init; }

    public required CleaningPathCleanupState ValveCleanupState { get; init; }

    /// <summary>The Phase 3B motion handoff (present on the success terminal; null on failure).</summary>
    public required CleaningPathExecutionHandoff? MotionHandoff { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>The final typed composition result (null until the composition reaches a terminal state).</summary>
public sealed record CleaningPathCompositionResult(
    CleaningPathCompositionStatus Status,
    string Code,
    CleaningPathCompositionHandoff Handoff,
    CleaningPathExecutionResult? PathResult,
    IReadOnlyList<CleaningPathCompositionEvent> Events,
    CleaningPathCleanupState MotionCleanupState,
    CleaningPathCleanupState ValveCleanupState);

/// <summary>The composition lifecycle snapshot republished after every step (null before Start).</summary>
public sealed record CleaningPathCompositionSnapshot(
    string CompositionId,
    CleaningPathCompositionIdentity Identity,
    CleaningPathCompositionStatus Status,
    JobPhase? CurrentPointId,
    CleaningPathPumpReadinessResult PumpReadiness,
    long EvidenceSequence);

/// <summary>
/// The typed start outcome. A refused start is fail-closed and preserves the exact Pump
/// pressure-quality and source evidence; the machine is left untouched and no path point, valve
/// command or Pump command is ever issued.
/// </summary>
public sealed record CleaningPathCompositionStartOutcome(
    bool Started,
    string Code,
    CleaningPathPumpReadinessResult? PumpReadiness,
    CleaningPathCompositionSnapshot? Composition);
