namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Completion evidence that releases the Active Job. CP-1 accepts it as input;
/// it does not produce it. <see cref="Seq"/> is the Safe Return sequence that
/// the release must follow, and it must be strictly greater than the Job's
/// dispatch evidence sequence. Both flags must be true for a release.
/// </summary>
public sealed record SafeReturnReleaseEvidence(
    string JobId,
    int Seq,
    bool OutcomeRecorded,
    bool SafeReturnComplete);
