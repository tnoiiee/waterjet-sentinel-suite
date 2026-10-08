using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// Bounded, deterministic evidence retention for one SIMULATOR run (Stage 0.4A CP-3b). It is a pure value: each
/// <see cref="Retain"/> call returns a new instance and changes nothing else. The evidence log keeps the newest
/// <see cref="EvidenceLogCapacity"/> records and counts the records it dropped. The Safe Return tail keeps the
/// newest <see cref="SafeReturnTailCapacity"/> Safe Return records. The last dispatch is taken from the queue head
/// that was dispatched, because the kernel Job does not store its source reason or origin. The first critical
/// record is kept once, and later critical records are not retained. Retention never clears the critical latch
/// and never drives a Job.
/// </summary>
public sealed record SequencingRetention
{
    /// <summary>Maximum number of evidence records kept in <see cref="EvidenceLog"/>.</summary>
    public const int EvidenceLogCapacity = 256;

    /// <summary>Maximum number of Safe Return records kept in <see cref="SafeReturnTail"/>.</summary>
    public const int SafeReturnTailCapacity = 16;

    /// <summary>The newest evidence records, oldest first.</summary>
    public required IReadOnlyList<SequencingEvidence> EvidenceLog { get; init; }

    /// <summary>How many older evidence records were dropped to keep the log bounded.</summary>
    public required int EvidenceDropped { get; init; }

    /// <summary>The newest Safe Return records (steps SR1 to SR7 and SR_FAILED), oldest first.</summary>
    public required IReadOnlyList<SequencingEvidence> SafeReturnTail { get; init; }

    /// <summary>The most recent dispatch, or null when no Job has been dispatched in this run.</summary>
    public DispatchRecord? LastDispatch { get; init; }

    /// <summary>The first critical record of the run, or null when no critical event has occurred.</summary>
    public SequencingEvidence? FirstCriticalEvidence { get; init; }

    /// <summary>A retention with nothing recorded.</summary>
    public static SequencingRetention Empty { get; } = new()
    {
        EvidenceLog = new List<SequencingEvidence>().AsReadOnly(),
        EvidenceDropped = 0,
        SafeReturnTail = new List<SequencingEvidence>().AsReadOnly(),
    };

    /// <summary>
    /// Returns the retention after one kernel transition. <paramref name="before"/> is the state the transition
    /// started from, which is where the dispatched queue head is read.
    /// </summary>
    public static SequencingRetention Retain(SequencingRetention previous, SequencingState before, SequencingTransition transition)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(transition);

        var log = new List<SequencingEvidence>(previous.EvidenceLog);
        log.AddRange(transition.Records);
        var dropped = previous.EvidenceDropped;
        if (log.Count > EvidenceLogCapacity)
        {
            var overflow = log.Count - EvidenceLogCapacity;
            log.RemoveRange(0, overflow);
            dropped += overflow;
        }

        var tail = new List<SequencingEvidence>(previous.SafeReturnTail);
        foreach (var evidence in transition.Records)
        {
            if (evidence.Step is not null)
            {
                tail.Add(evidence);
            }
        }

        if (tail.Count > SafeReturnTailCapacity)
        {
            tail.RemoveRange(0, tail.Count - SafeReturnTailCapacity);
        }

        var lastDispatch = previous.LastDispatch;
        var firstCritical = previous.FirstCriticalEvidence;
        foreach (var evidence in transition.Records)
        {
            if (evidence.Code == SequencingCodes.Dispatched
                && transition.State.ActiveJob is { } dispatchedJob
                && before.Queue.Count > 0)
            {
                lastDispatch = DispatchOf(dispatchedJob, before.Queue[0]);
            }

            if (firstCritical is null && evidence.Code == SequencingCodes.CriticalSuspensionRaised)
            {
                firstCritical = evidence;
            }
        }

        return new SequencingRetention
        {
            EvidenceLog = log.AsReadOnly(),
            EvidenceDropped = dropped,
            SafeReturnTail = tail.AsReadOnly(),
            LastDispatch = lastDispatch,
            FirstCriticalEvidence = firstCritical,
        };
    }

    private static DispatchRecord DispatchOf(SequencingActiveJob job, SequencingEntry head) => new()
    {
        DispatchId = job.DispatchId,
        QueueRevisionBefore = job.QueueRevisionBefore,
        QueueRevisionAfter = job.QueueRevisionAfter,
        QueueEntryId = head.EntryId,
        PositionBefore = 1,
        SensorId = head.SensorId,
        SourceReason = head.SourceReason,
        JobId = job.JobId,
        Origin = SequencingAdmissionSource.SCENARIO_PREPARED.ToString(),
        DispatchedAt = UtcTimestamps.Format(job.StartedAt),
    };
}
