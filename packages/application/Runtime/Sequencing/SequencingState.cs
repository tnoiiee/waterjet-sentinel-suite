using Wjss.Contracts;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// One dispatch-ready GlobalQueue entry. Position is not stored: it is the
/// index in <see cref="SequencingState.Queue"/> plus one. The entry carries no
/// state of its own (no waiting, hold, block or exclusion), by Owner ruling.
/// </summary>
public sealed record SequencingEntry(
    string EntryId,
    string SensorId,
    string SourceReason,
    int SecondsSinceLastClean);

/// <summary>
/// The single Active Job, with its Stage 0.4A CP-2 execution and Safe Return
/// state. The accepted <see cref="JobLifecycle"/> names are reused. The kernel
/// sub-stage <see cref="CleaningStage"/> refines RUNNING only. The Job keeps its
/// WJ/IV assignment from dispatch. <see cref="PumpReady"/> is the waiting gate
/// before Cleaning and is never a Queue state.
/// </summary>
public sealed record SequencingActiveJob(
    string JobId,
    string DispatchId,
    string TargetSensorId,
    string JetId,
    string ValveId,
    string SourceEntryId,
    int DispatchEvidenceSeq,
    bool PumpReady,
    DateTimeOffset StartedAt,
    int QueueRevisionBefore,
    int QueueRevisionAfter,
    JobLifecycle Lifecycle,
    CleaningStage Stage,
    JobPhase? VerifiedPhase,
    bool CleaningActive,
    bool WaterOutputOn,
    ValveFeedbackState? LastValveFeedback,
    CleaningJobOutcome? PendingOutcome,
    string? Trigger,
    string? TriggerReason,
    SafeReturnStep? Step,
    string? FailureCode,
    SafeReturnLedger Ledger)
{
    public bool ValveOpenCommanded { get; init; }
    public ValveOpenResolution? OpenResolution { get; init; }
    public ValveCloseResolution? CloseResolution { get; init; }
    public ValveDiagnosis ValveDiagnosis { get; init; } = ValveDiagnosis.NONE;
    public PressureSample? ValvePressure { get; init; }
    public bool? UpperLimitDetected { get; init; }
    public bool? LowerLimitDetected { get; init; }
    public bool AxisStandbyConfirmed { get; init; }
    public bool UpperLimitFault { get; init; }
    public PressureSample? PumpPressure { get; init; }
    public bool PumpPressureVerified { get; init; }
    public SequencingPressureThresholds PressureThresholds { get; init; } = new();
}

/// <summary>
/// Immutable sequencing state with a single writer: the kernel. Every transition
/// returns a new instance and never mutates an existing one.
///
/// <para>
/// Stage 0.4A CP-1 state-integrity boundary (Owner correction, 2026-10-08): the
/// constructor is internal and every setter is internal init, so external code
/// can read a state but cannot construct or mutate one. The queue is copied on
/// construction and exposed only as a read-only view, so no caller collection
/// can alias the stored queue. A state that violates a kernel invariant is
/// rejected by <see cref="SequencingKernel.Apply"/> with SEQUENCING_STATE_INVALID
/// and by every projection with an <see cref="InvalidOperationException"/>.
/// </para>
///
/// <para>
/// Canonical representation: <see cref="Mode"/> holds only the non-critical
/// AutoSequence modes (OFF, RUNNING, PAUSE_REQUESTED, PAUSED). The critical gate
/// is the separate <see cref="CriticalSuspended"/> latch, which projects as
/// CRITICAL_SUSPENDED. Mode CRITICAL_SUSPENDED is never a valid stored value.
/// </para>
/// </summary>
public sealed record SequencingState
{
    private SequencingEntry[]? _queue;

    internal SequencingState(
        IReadOnlyList<SequencingEntry>? queue,
        int queueRevision,
        SequencingActiveJob? activeJob,
        AutoSequenceMode mode,
        bool criticalSuspended,
        int evidenceSeq,
        int nextEntrySeq,
        int nextJobSeq)
    {
        _queue = queue?.ToArray();
        QueueRevision = queueRevision;
        ActiveJob = activeJob;
        Mode = mode;
        CriticalSuspended = criticalSuspended;
        EvidenceSeq = evidenceSeq;
        NextEntrySeq = nextEntrySeq;
        NextJobSeq = nextJobSeq;
    }

    /// <summary>
    /// The dispatch-ready queue as a read-only view over a private copy. A
    /// malformed state with no queue storage throws, and projections never read it.
    /// </summary>
    public IReadOnlyList<SequencingEntry> Queue
    {
        get => RawQueue ?? throw new InvalidOperationException(SequencingCodes.StateInvalid + ": QUEUE_NULL");
        internal init => _queue = value.ToArray();
    }

    /// <summary>Queue revision. Advances on every admission and every head removal or dispatch.</summary>
    public int QueueRevision { get; internal init; }

    /// <summary>The single Active Job, or null when no Job is active.</summary>
    public SequencingActiveJob? ActiveJob { get; internal init; }

    /// <summary>The non-critical AutoSequence mode (never CRITICAL_SUSPENDED).</summary>
    public AutoSequenceMode Mode { get; internal init; }

    /// <summary>The critical suspension latch. While true, the queue and queue revision are frozen.</summary>
    public bool CriticalSuspended { get; internal init; }

    /// <summary>Latched read-only equipment fault. No clear/reset input is present.</summary>
    public IReadOnlyList<EquipmentFaultState> EquipmentFaults { get; internal init; } = Array.Empty<EquipmentFaultState>();

    /// <summary>The evidence sequence of the most recent evidence record (0 before any transition).</summary>
    public int EvidenceSeq { get; internal init; }

    /// <summary>The next synthetic queue entry number to issue.</summary>
    public int NextEntrySeq { get; internal init; }

    /// <summary>The next synthetic Job number to issue.</summary>
    public int NextJobSeq { get; internal init; }

    /// <summary>Read-only view of the stored queue, or null for a malformed state with no queue storage.</summary>
    internal IReadOnlyList<SequencingEntry>? RawQueue => _queue is null ? null : Array.AsReadOnly(_queue);
}
