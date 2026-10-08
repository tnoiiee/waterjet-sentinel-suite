using Wjss.Contracts;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// The single Stage 0.4A CP-1 state-integrity guard. It is pure and deterministic:
/// it returns the first violation in a fixed order, so identical malformed input
/// always yields the same violation identity. It never normalizes a state.
///
/// <para>
/// Used by <see cref="SequencingKernel.Apply"/> (refusal with
/// SEQUENCING_STATE_INVALID) and by every projection (InvalidOperationException).
/// Equipment topology is an event input, not state, so it is not checked here.
/// </para>
/// </summary>
internal static class SequencingStateValidator
{
    internal const string QueueNull = "QUEUE_NULL";
    internal const string QueueOverCapacity = "QUEUE_OVER_CAPACITY";
    internal const string QueueItemNull = "QUEUE_ITEM_NULL";
    internal const string QueueEntryFieldBlank = "QUEUE_ENTRY_FIELD_BLANK";
    internal const string QueueEntrySecondsNegative = "QUEUE_ENTRY_SECONDS_NEGATIVE";
    internal const string QueueEntryIdDuplicate = "QUEUE_ENTRY_ID_DUPLICATE";
    internal const string QueueSensorDuplicate = "QUEUE_SENSOR_DUPLICATE";
    internal const string QueueRevisionNegative = "QUEUE_REVISION_NEGATIVE";
    internal const string EvidenceSeqNegative = "EVIDENCE_SEQ_NEGATIVE";
    internal const string NextEntrySeqNotUsable = "NEXT_ENTRY_SEQ_NOT_USABLE";
    internal const string NextJobSeqNotUsable = "NEXT_JOB_SEQ_NOT_USABLE";
    internal const string ModeUndefined = "MODE_UNDEFINED";
    internal const string ModeCriticalNotCanonical = "MODE_CRITICAL_NOT_CANONICAL";
    internal const string ModeOffWithJob = "MODE_OFF_WITH_JOB";
    internal const string ModePausedWithJob = "MODE_PAUSED_WITH_JOB";
    internal const string ModePauseRequestedWithoutJob = "MODE_PAUSE_REQUESTED_WITHOUT_JOB";
    internal const string JobFieldBlank = "JOB_FIELD_BLANK";
    internal const string JobPairingInvalid = "JOB_PAIRING_INVALID";
    internal const string JobRevisionInvalid = "JOB_REVISION_INVALID";
    internal const string JobDispatchSeqInvalid = "JOB_DISPATCH_SEQ_INVALID";
    internal const string JobTargetStillQueued = "JOB_TARGET_STILL_QUEUED";

    /// <summary>Returns the first violation identity, or null when the state is consistent.</summary>
    internal static string? FirstViolation(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var queue = state.RawQueue;
        if (queue is null)
        {
            return QueueNull;
        }

        if (queue.Count > QueueSummary.MaxEntries)
        {
            return QueueOverCapacity;
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var sensorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in queue)
        {
            if (entry is null)
            {
                return QueueItemNull;
            }

            if (IsBlank(entry.EntryId) || IsBlank(entry.SensorId) || IsBlank(entry.SourceReason))
            {
                return QueueEntryFieldBlank;
            }

            if (entry.SecondsSinceLastClean < 0)
            {
                return QueueEntrySecondsNegative;
            }

            if (!entryIds.Add(entry.EntryId))
            {
                return QueueEntryIdDuplicate;
            }

            if (!sensorIds.Add(entry.SensorId))
            {
                return QueueSensorDuplicate;
            }
        }

        if (state.QueueRevision < 0)
        {
            return QueueRevisionNegative;
        }

        if (state.EvidenceSeq < 0)
        {
            return EvidenceSeqNegative;
        }

        if (state.NextEntrySeq < 1)
        {
            return NextEntrySeqNotUsable;
        }

        if (state.NextJobSeq < 1)
        {
            return NextJobSeqNotUsable;
        }

        if (!Enum.IsDefined(state.Mode))
        {
            return ModeUndefined;
        }

        if (state.Mode == AutoSequenceMode.CRITICAL_SUSPENDED)
        {
            return ModeCriticalNotCanonical;
        }

        if (state.ActiveJob is { } job)
        {
            return JobViolation(state, job, queue);
        }

        return state.Mode == AutoSequenceMode.PAUSE_REQUESTED ? ModePauseRequestedWithoutJob : null;
    }

    /// <summary>Throws InvalidOperationException carrying SEQUENCING_STATE_INVALID when the state is inconsistent.</summary>
    internal static void RequireValid(SequencingState state)
    {
        var violation = FirstViolation(state);
        if (violation is not null)
        {
            throw new InvalidOperationException(SequencingCodes.StateInvalid + ": " + violation);
        }
    }

    private static string? JobViolation(SequencingState state, SequencingActiveJob job, IReadOnlyList<SequencingEntry> queue)
    {
        if (state.Mode == AutoSequenceMode.OFF)
        {
            return ModeOffWithJob;
        }

        if (state.Mode == AutoSequenceMode.PAUSED)
        {
            return ModePausedWithJob;
        }

        if (IsBlank(job.JobId)
            || IsBlank(job.DispatchId)
            || IsBlank(job.TargetSensorId)
            || IsBlank(job.JetId)
            || IsBlank(job.ValveId)
            || IsBlank(job.SourceEntryId))
        {
            return JobFieldBlank;
        }

        if (!SequencingTopology.IsPairedOrdinal(job.JetId, job.ValveId))
        {
            return JobPairingInvalid;
        }

        // Computed in 64-bit so that the consecutive-revision check cannot itself overflow.
        var consecutive = (long)job.QueueRevisionBefore + 1 == job.QueueRevisionAfter;
        if (job.QueueRevisionBefore < 0 || !consecutive || job.QueueRevisionAfter > state.QueueRevision)
        {
            return JobRevisionInvalid;
        }

        if (job.DispatchEvidenceSeq < 1 || job.DispatchEvidenceSeq > state.EvidenceSeq)
        {
            return JobDispatchSeqInvalid;
        }

        foreach (var entry in queue)
        {
            if (string.Equals(entry.SensorId, job.TargetSensorId, StringComparison.Ordinal)
                || string.Equals(entry.EntryId, job.SourceEntryId, StringComparison.Ordinal))
            {
                return JobTargetStillQueued;
            }
        }

        return null;
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
