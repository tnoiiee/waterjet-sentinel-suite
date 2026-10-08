namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Result of one transition. <see cref="State"/> is always a new instance whose
/// EvidenceSeq equals <see cref="SequencingEvidence.Seq"/>. On REFUSED and NO_OP
/// the queue, queue revision, Job and AutoSequence state are unchanged; only the
/// evidence sequence advances, so every evidence record has a unique number.
/// </summary>
public sealed record SequencingTransition(
    SequencingState State,
    SequencingOutcome Outcome,
    SequencingEvidence Evidence);

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
/// shared UTC millisecond encoding.
/// </summary>
public sealed record SequencingEvidence(
    int Seq,
    string At,
    string EventKind,
    SequencingOutcome Outcome,
    string Code,
    string? EntryId,
    string? JobId,
    int QueueRevision);
