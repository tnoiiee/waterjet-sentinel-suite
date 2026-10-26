using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath.Execution;

namespace Wjss.Runtime.Core.CleaningPath.H1;

/// <summary>The phases of one H1 dry Cleaning Path commissioning run.</summary>
public enum H1DryCommissioningPhase
{
    /// <summary>No run has been started.</summary>
    NotStarted = 0,

    /// <summary>Start preconditions are being proven fail-closed.</summary>
    Preconditions = 1,

    /// <summary>Both resolved axes are running the typed Home lifecycle, independent per axis.</summary>
    Homing = 2,

    /// <summary>The coordinate state is being established and proven before any Move.</summary>
    CoordinateEstablishment = 3,

    /// <summary>P1 through P6 run through the existing typed path execution coordinator.</summary>
    PathExecution = 4,

    /// <summary>The dry Centre return Move is running on both resolved axes, independent per axis.</summary>
    CentreReturn = 5,

    /// <summary>The run completed; the dry Safe Return completion is published.</summary>
    Completed = 6,

    /// <summary>The run failed; the first primary cause is preserved and no later point may start.</summary>
    Failed = 7,

    /// <summary>The Owner aborted the run; the typed Owner-abort cause is preserved.</summary>
    Aborted = 8,

    /// <summary>The session close was requested; the typed close cause is preserved.</summary>
    Closed = 9,

    /// <summary>
    /// The Commission/Home run completed: both resolved axes proved Homed with a verified zero in
    /// this session and no path point ran. A later explicit Run Path consumes that readiness.
    /// </summary>
    Commissioned = 10,
}

/// <summary>
/// The dry Safe Return publication state. The ONLY published member is the dry Centre completion:
/// no Pump completion, no Valve completion, no physical Cleaning Job completion and no Production
/// completion can ever be represented by this type.
/// </summary>
public enum H1DrySafeReturnState
{
    /// <summary>No dry Safe Return completion has been published.</summary>
    NotPublished = 0,

    /// <summary>Both resolved axes verified the fixed Centre target and the dry result is published.</summary>
    Completed = 1,
}

/// <summary>The typed evidence that the selected Effective WJ is supported by this run (WSB1 only).</summary>
public enum H1CabinetAdmission
{
    /// <summary>The resolved cabinet has not been evaluated.</summary>
    NotEvaluated = 0,

    /// <summary>The resolved cabinet is exactly WSB1 and the run is admitted.</summary>
    Wsb1Admitted = 1,

    /// <summary>The resolved cabinet is not WSB1; the run is refused fail-closed.</summary>
    UnsupportedCabinetRefused = 2,
}

/// <summary>The canonical codes of the H1 dry commissioning run. Codes only: no values, no locations.</summary>
public static class H1DryCommissioningCodes
{
    // ---------------------------------------------------------------------------- start refusals
    public const string StartRefusedRunActive = "H1_START_REFUSED_RUN_ACTIVE";
    public const string StartRefusedRequestIncomplete = "H1_START_REFUSED_REQUEST_INCOMPLETE";
    public const string StartRefusedSnapshotNotAccepted = "H1_START_REFUSED_SNAPSHOT_NOT_ACCEPTED";
    public const string StartRefusedResolutionNotAccepted = "H1_START_REFUSED_RESOLUTION_NOT_ACCEPTED";
    public const string StartRefusedSensorMismatch = "H1_START_REFUSED_SENSOR_MISMATCH";
    public const string StartRefusedUnsupportedWsb = "H1_START_REFUSED_UNSUPPORTED_WSB";
    public const string StartRefusedCanonicalRouteMismatch = "H1_START_REFUSED_CANONICAL_ROUTE_MISMATCH";
    public const string StartRefusedControllerIdentityNotVerified = "H1_START_REFUSED_CONTROLLER_IDENTITY_NOT_VERIFIED";
    public const string StartRefusedEndpointNotConfigured = "H1_START_REFUSED_ENDPOINT_NOT_CONFIGURED";
    public const string StartRefusedProfileNotConfigured = "H1_START_REFUSED_PROFILE_NOT_CONFIGURED";
    public const string StartRefusedDryModeNotDeclared = "H1_START_REFUSED_DRY_MODE_NOT_DECLARED";
    public const string StartRefusedPumpCommandPathAvailable = "H1_START_REFUSED_PUMP_COMMAND_PATH_AVAILABLE";
    public const string StartRefusedPhysicalValveCommandPathAvailable = "H1_START_REFUSED_PHYSICAL_VALVE_COMMAND_PATH_AVAILABLE";
    public const string StartRefusedQueueAutoSequenceAvailable = "H1_START_REFUSED_QUEUE_AUTO_SEQUENCE_AVAILABLE";
    public const string StartRefusedPhysicalAbortNotAccessible = "H1_START_REFUSED_PHYSICAL_ABORT_NOT_ACCESSIBLE";
    public const string StartRefusedOperatorObserverNotPresent = "H1_START_REFUSED_OPERATOR_OBSERVER_NOT_PRESENT";
    public const string StartRefusedSessionNotReady = "H1_START_REFUSED_SESSION_NOT_READY";
    public const string StartRefusedSessionFaulted = "H1_START_REFUSED_SESSION_FAULTED";
    public const string StartRefusedAxisOperationActive = "H1_START_REFUSED_AXIS_OPERATION_ACTIVE";
    public const string StartRefusedObservationNotFresh = "H1_START_REFUSED_OBSERVATION_NOT_FRESH";
    public const string StartRefusedCoordinateReadinessAbsent = "H1_START_REFUSED_COORDINATE_READINESS_ABSENT";

    // ------------------------------------------------------------------------------ event kinds
    public const string KindCommissionStarted = "H1_COMMISSION_STARTED";
    public const string KindPathRunStarted = "H1_PATH_RUN_STARTED";
    public const string KindHomeRequested = "H1_HOME_REQUESTED";
    public const string KindCoordinateEstablished = "H1_COORDINATE_ESTABLISHED";
    public const string KindPathExecutionStarted = "H1_PATH_EXECUTION_STARTED";
    public const string KindCentreMoveRequested = "H1_CENTRE_MOVE_REQUESTED";
    public const string KindCentreVerified = "H1_CENTRE_VERIFIED";
    public const string KindDrySafeReturnCompleted = "H1_DRY_SAFE_RETURN_COMPLETED";
    public const string KindRunTerminal = "H1_RUN_TERMINAL";

    // ------------------------------------------------------------------------------- phase codes
    public const string HomePhaseStarted = "H1_HOME_PHASE_STARTED";
    public const string CoordinateEstablishmentVerified = "H1_COORDINATE_ESTABLISHMENT_VERIFIED";
    public const string CoordinateEstablishmentRefused = "H1_COORDINATE_ESTABLISHMENT_REFUSED";
    /// <summary>
    /// No dual-zero definition was dispatched for this axis in this session, because the Home ended
    /// before zero establishment. The composition itself is never optional for an H1 Home.
    /// </summary>
    public const string DualZeroNotDispatched = "H1_DUAL_ZERO_NOT_DISPATCHED";
    public const string DualZeroDispatchedEffectUnverified = "H1_DUAL_ZERO_DISPATCHED_EFFECT_UNVERIFIED";

    /// <summary>
    /// The Owner-approved Home-carried reset-encoder composition ran: the position-zero definition
    /// and then the dual-zero definition were dispatched for this axis, in that order, as one
    /// contiguous native pair, and the zero proof was taken from position reads strictly newer than
    /// the dual-zero definition.
    /// </summary>
    public const string DualZeroDispatchedInHomeSequence = "H1_DUAL_ZERO_DISPATCHED_IN_HOME_SEQUENCE";

    /// <summary>
    /// The Owner-approved Home-carried reset-encoder composition was refused at its dual-zero
    /// definition. The Home failed closed and no zero was verified.
    /// </summary>
    public const string DualZeroRefusedInHomeSequence = "H1_DUAL_ZERO_REFUSED_IN_HOME_SEQUENCE";
    public const string CentreReturnStarted = "H1_CENTRE_RETURN_STARTED";

    // ------------------------------------------------------------------------------- terminals
    public const string RunComplete = "H1_DRY_CLEANING_PATH_COMMISSIONING_COMPLETE";
    public const string CommissionComplete = "H1_COMMISSION_COMPLETE_COORDINATES_VERIFIED";
    public const string RunFailed = "H1_FAILED";
    public const string RunFailedHome = "H1_FAILED_HOME";
    public const string RunFailedCoordinateEstablishment = "H1_FAILED_COORDINATE_ESTABLISHMENT";
    public const string RunFailedPathExecution = "H1_FAILED_PATH_EXECUTION";
    public const string RunFailedCentreReturn = "H1_FAILED_CENTRE_RETURN";
    public const string RunAborted = "H1_ABORT_OWNER_ABORT_REQUESTED";
    public const string RunClosed = "H1_CLOSE_REQUESTED";

    // -------------------------------------------------------------------------- cleanup evidence
    public const string CleanupNotNeeded = "H1_CLEANUP_NOT_NEEDED";
    public const string CleanupStopSupervised = "H1_CLEANUP_STOP_SUPERVISED";
    public const string CleanupIncomplete = "H1_CLEANUP_INCOMPLETE";
}

/// <summary>
/// The host-attested start guards. Every member must be supplied explicitly: there is no default,
/// no fallback and no fabrication. The dry command-path members attest that no Pump command path,
/// no physical Isolation Valve command path and no Queue / AutoSequence path is connected to this
/// run; the Product surface itself exposes none of those commands, so a false attestation is a
/// contradiction and refuses the start fail-closed.
/// </summary>
public sealed record H1DryCommissioningGuards
{
    /// <summary>True only when the resolved controller identity was proven for this run.</summary>
    public required bool ControllerIdentityVerified { get; init; }

    /// <summary>True only when the Owner-local controller endpoint is configured outside the repository.</summary>
    public required bool ControllerEndpointConfigured { get; init; }

    /// <summary>True only when the Owner-local motion profile is configured outside the repository.</summary>
    public required bool OwnerLocalProfileConfigured { get; init; }

    /// <summary>True only when the run is declared dry (no water, no physical output command).</summary>
    public required bool DryModeDeclared { get; init; }

    /// <summary>True only when no Pump command path is connected to this run.</summary>
    public required bool PumpCommandPathUnavailable { get; init; }

    /// <summary>True only when no physical Isolation Valve command path is connected to this run.</summary>
    public required bool PhysicalValveCommandPathUnavailable { get; init; }

    /// <summary>True only when no Queue / AutoSequence / next-job path is connected to this run.</summary>
    public required bool QueueAutoSequenceUnavailable { get; init; }

    /// <summary>True only when the physical abort means is accessible to the Operator for this run.</summary>
    public required bool PhysicalAbortAccessible { get; init; }

    /// <summary>True only when the Owner is present as Operator/Observer for this run.</summary>
    public required bool OperatorObserverPresent { get; init; }
}

/// <summary>The immutable identity echo of one H1 run, for evidence only.</summary>
public sealed record H1DryCommissioningIdentity
{
    public required string SensorId { get; init; }
    public required string CanonicalWaterJetId { get; init; }
    public string? RequestedOverrideWaterJetId { get; init; }
    public required string EffectiveWaterJetId { get; init; }
    public required EffectiveWaterJetProvenance EffectiveWjProvenance { get; init; }

    /// <summary>The paired Isolation Valve identity, derived for informational evidence only.</summary>
    public required string DerivedIsolationValveId { get; init; }

    public required string CabinetId { get; init; }
    public required string ControllerId { get; init; }
    public required string HorizontalAxisId { get; init; }
    public required string VerticalAxisId { get; init; }
    public required string HorizontalBrakeOutputId { get; init; }
    public required string VerticalBrakeOutputId { get; init; }
}

/// <summary>The start request of one H1 run. Nothing here mutates Sensor assignment or the Cleaning Path.</summary>
public sealed record H1DryCommissioningRequest
{
    /// <summary>The accepted Phase 3A Cleaning Path store snapshot.</summary>
    public required CleaningPathSnapshot? PathStore { get; init; }

    /// <summary>The accepted Phase 3A resolution for the selected Sensor.</summary>
    public required CleaningPathResolutionResult? Resolution { get; init; }

    /// <summary>The one Owner-selected Sensor identity.</summary>
    public required string SelectedSensorId { get; init; }

    /// <summary>The explicit host-attested guards.</summary>
    public required H1DryCommissioningGuards Guards { get; init; }
}

/// <summary>The outcome of one run start attempt.</summary>
public sealed record H1DryCommissioningStartOutcome(bool Started, string Code);

/// <summary>
/// The per-axis coordinate evidence of the run. The Home lifecycle carries the Owner-approved
/// reset-encoder composition: the typed position-zero definition, then the typed dual-zero
/// definition, then fresh position reads whose evidence is strictly newer than the dual-zero
/// definition. Only when both reads are within the configured tolerance is the axis published
/// Homed with ZeroVerified. The dual-zero definition inside a Home composes the Home lifecycle; the
/// standalone dual-zero operation keeps its separate, profile-permitted, dispatch-only semantics
/// and its effect stays UNVERIFIED. The dual-zero disposition is recorded per axis.
/// </summary>
public sealed record H1AxisCoordinateEvidence(
    string AxisId,
    string RoleLabel,
    string HomeOutcomeCode,
    bool HomeVerified,
    bool CoordinateZeroVerified,
    string DualZeroState,
    string DualZeroDispositionCode);

/// <summary>The per-axis record of the dry Centre return.</summary>
public sealed record H1AxisCentreRecord(
    string AxisId,
    int TargetNative,
    CleaningPathMotionOutcome Outcome,
    string OutcomeSourceCode,
    bool Verified,
    bool BrakeApplyCommanded);

/// <summary>The dry Centre return record: both axes must verify before any completion is published.</summary>
public sealed record H1CentreReturnRecord(
    int HorizontalTargetNative,
    int VerticalTargetNative,
    H1AxisCentreRecord Horizontal,
    H1AxisCentreRecord Vertical);

/// <summary>The cleanup evidence of one axis after a failure, abort or close.</summary>
public sealed record H1AxisCleanupEvidence(
    string AxisId,
    bool StopRequested,
    string CleanupCode);

/// <summary>
/// The typed H1 dry-commissioning result. It carries dry motion evidence only: there is no Pump
/// completion, no Valve completion, no physical Cleaning Job completion and no Production
/// completion member anywhere in this type.
/// </summary>
public sealed record H1DryCommissioningResult
{
    public required H1DryCommissioningPhase TerminalPhase { get; init; }
    public required string Code { get; init; }

    /// <summary>The first genuine primary cause, or null when no failure occurred.</summary>
    public string? PrimaryFailureCode { get; init; }

    public required H1DryCommissioningIdentity Identity { get; init; }
    public required H1CabinetAdmission CabinetAdmission { get; init; }

    /// <summary>The coordinate evidence per axis, preserving the DP / DE distinction.</summary>
    public required H1AxisCoordinateEvidence HorizontalCoordinate { get; init; }
    public required H1AxisCoordinateEvidence VerticalCoordinate { get; init; }

    /// <summary>The Phase 3B path result (P1–P6 ledger and motion cleanup), or null when the path never started.</summary>
    public CleaningPathExecutionResult? PathResult { get; init; }

    /// <summary>The dry Centre return record, or null when the Centre return never started.</summary>
    public H1CentreReturnRecord? CentreReturn { get; init; }

    public required IReadOnlyList<JobPhase> VerifiedLedger { get; init; }
    public required H1AxisCleanupEvidence HorizontalCleanup { get; init; }
    public required H1AxisCleanupEvidence VerticalCleanup { get; init; }
    public required H1DrySafeReturnState DrySafeReturn { get; init; }
}

/// <summary>
/// The immutable run snapshot republished after every step. The live observation detail (Status,
/// TP, RP, Brake state and freshness) is read from the existing session snapshot by the operator
/// surface; this record carries the run-level identity, phase and evidence.
/// </summary>
public sealed record H1DryCommissioningSnapshot
{
    public required string RunId { get; init; }
    public required H1DryCommissioningPhase Phase { get; init; }
    public required H1DryCommissioningIdentity Identity { get; init; }
    public required H1CabinetAdmission CabinetAdmission { get; init; }

    /// <summary>The current point identity during the path phase, or null outside it.</summary>
    public string? CurrentPointId { get; init; }

    public required IReadOnlyList<JobPhase> VerifiedLedger { get; init; }

    /// <summary>The first genuine primary cause, or null while no failure occurred.</summary>
    public string? PrimaryFailureCode { get; init; }

    public required H1DrySafeReturnState DrySafeReturn { get; init; }
}
