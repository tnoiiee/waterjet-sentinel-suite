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
/// The single Active Job. Stage 0.4A CP-1 carries no phase execution: the Job
/// stays in phase P1 with lifecycle RUNNING, and <see cref="PumpReady"/> is the
/// only waiting gate (WAITING_FOR_PUMP is projected as AutoSequence PUMP_NOT_READY).
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
    int QueueRevisionAfter);

/// <summary>
/// Immutable sequencing state. The kernel is the only writer; every transition
/// returns a new instance and never mutates an existing one. <see cref="Mode"/>
/// holds the non-critical AutoSequence modes (OFF, RUNNING, PAUSE_REQUESTED,
/// PAUSED); <see cref="CriticalSuspended"/> is the separate critical gate that
/// projects as CRITICAL_SUSPENDED.
/// </summary>
public sealed record SequencingState(
    IReadOnlyList<SequencingEntry> Queue,
    int QueueRevision,
    SequencingActiveJob? ActiveJob,
    AutoSequenceMode Mode,
    bool CriticalSuspended,
    int EvidenceSeq,
    int NextEntrySeq,
    int NextJobSeq);
