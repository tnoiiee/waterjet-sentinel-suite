using Wjss.Contracts;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Result of one transition. <see cref="State"/> is always a new instance whose
/// EvidenceSeq equals the sequence of the last record. <see cref="Evidence"/> is
/// that last record. <see cref="Records"/> lists every evidence record the
/// transition produced, in strictly increasing sequence order. A Safe Return
/// transition may produce several records (one per Safe Return step), all from
/// the one Kernel evidence sequence. On REFUSED and NO_OP the queue, queue
/// revision, Job and AutoSequence state are unchanged; only the evidence
/// sequence advances.
/// </summary>
public sealed record SequencingTransition(
    SequencingState State,
    SequencingOutcome Outcome,
    SequencingEvidence Evidence,
    IReadOnlyList<SequencingEvidence> Records);

/// <summary>Outcome of one transition.</summary>
public enum SequencingOutcome
{
    APPLIED,
    NO_OP,
    REFUSED,
}

/// <summary>
/// One evidence record. Fields are plain values so that a serialized evidence
/// stream is byte-identical for identical input. <see cref="At"/> uses the
/// shared UTC millisecond encoding. The optional Job fields are populated only
/// for Job and Safe Return records. <see cref="LifecycleAfter"/> is null when the
/// record releases the Job.
/// </summary>
public sealed record SequencingEvidence(
    int Seq,
    string At,
    string EventKind,
    SequencingOutcome Outcome,
    string Code,
    string? EntryId,
    string? JobId,
    int QueueRevision,
    string? JetId = null,
    string? ValveId = null,
    JobLifecycle? LifecycleBefore = null,
    JobLifecycle? LifecycleAfter = null,
    JobPhase? Phase = null,
    SafeReturnStep? Step = null,
    ValveFeedbackState? ValveFeedback = null,
    AxisFeedbackState? AxisFeedback = null,
    PumpObservation? Pump = null,
    CleaningJobOutcome? JobOutcome = null,
    string? Intent = null,
    ValveCloseResolution? CloseResolution = null,
    ValveDiagnosis? Diagnosis = null);
