using System.Globalization;
using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Pure Stage 0.4A sequencing kernel: the GlobalQueue (capacity 8, FIFO,
/// dispatch-ready entries only, head-only dispatch, no scan-forward) and the
/// AutoSequence gate with exactly one Active Job.
///
/// <para>
/// The kernel is deterministic: identical initial state and identical event
/// sequence produce identical states and evidence. It reads no clock, no random
/// source and no configuration. It executes no Job phase, no Pump action, no
/// Valve or Axis action, and no Safe Return. It has no command or actuation path.
/// </para>
///
/// <para>
/// Refusals and no-ops leave the queue, queue revision, Job and AutoSequence
/// state unchanged. The evidence sequence advances on every transition, so each
/// evidence record has a unique number. Pump readiness never refuses a dispatch;
/// it is a waiting gate on the Active Job.
/// </para>
///
/// <para>
/// State integrity (Owner correction 2026-10-08): <see cref="Apply"/> validates
/// the supplied state before any transition. An inconsistent state is refused
/// with SEQUENCING_STATE_INVALID and no other field changes. Projections throw
/// InvalidOperationException for an inconsistent state and never fall back to a
/// running state. A counter that cannot be incremented safely throws
/// InvalidOperationException rather than overflowing or fabricating evidence.
/// </para>
/// </summary>
public static class SequencingKernel
{
    /// <summary>The initial state: AutoSequence OFF, empty queue, revision 0, no Job.</summary>
    public static SequencingState Initial() =>
        new(Array.Empty<SequencingEntry>(), 0, null, AutoSequenceMode.OFF, false, 0, 1, 1);

    /// <summary>
    /// Applies one event and returns the next state with its evidence. An
    /// inconsistent input state is refused with SEQUENCING_STATE_INVALID and
    /// changes nothing except the evidence sequence.
    /// </summary>
    public static SequencingTransition Apply(SequencingState state, SequencingEvent sequencingEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sequencingEvent);

        if (SequencingStateValidator.FirstViolation(state) is not null)
        {
            var (kind, at) = Describe(sequencingEvent);
            return Commit(state, state, SequencingOutcome.REFUSED, kind, at, SequencingCodes.StateInvalid, null, null);
        }

        return sequencingEvent switch
        {
            StartAutoSequence e => Start(state, e),
            AdmitQueueEntry e => Admit(state, e),
            DispatchHead e => Dispatch(state, e),
            ObservePumpReadiness e => ObservePump(state, e),
            RaiseCriticalSuspension e => RaiseCritical(state, e),
            RequestPause e => ApplyPause(state, e),
            ReleaseActiveJob e => Release(state, e),
            _ => throw new ArgumentOutOfRangeException(nameof(sequencingEvent)),
        };
    }

    /// <summary>
    /// Projects the queue to the wire entries. Position 1 is the head; DirtyScore is never set.
    /// Throws InvalidOperationException for an inconsistent state.
    /// </summary>
    public static IReadOnlyList<QueueEntry> ProjectQueueEntries(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SequencingStateValidator.RequireValid(state);

        var entries = new List<QueueEntry>(state.Queue.Count);
        for (var index = 0; index < state.Queue.Count; index++)
        {
            var entry = state.Queue[index];
            entries.Add(new QueueEntry
            {
                Position = index + 1,
                EntryId = entry.EntryId,
                SensorId = entry.SensorId,
                SourceReason = entry.SourceReason,
                SecondsSinceLastClean = entry.SecondsSinceLastClean,
            });
        }

        return entries;
    }

    /// <summary>
    /// Projects the AutoSequence wire state. CRITICAL_SUSPENDED takes precedence,
    /// then PAUSE_REQUESTED and PAUSED. While RUNNING, the Active Job's pump gate
    /// shows PUMP_NOT_READY or JOB_ACTIVE; with no Job, the queue shows QUEUE_EMPTY
    /// or READY_TO_DISPATCH. Every mode is explicit. An inconsistent state throws
    /// InvalidOperationException and is never projected as a running state.
    /// </summary>
    public static AutoSequenceState ProjectAutoSequenceState(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SequencingStateValidator.RequireValid(state);

        if (state.CriticalSuspended)
        {
            return AutoSequenceState.CRITICAL_SUSPENDED;
        }

        return state.Mode switch
        {
            AutoSequenceMode.OFF => AutoSequenceState.OFF,
            AutoSequenceMode.RUNNING => ProjectRunningState(state),
            AutoSequenceMode.PAUSE_REQUESTED => AutoSequenceState.PAUSE_REQUESTED,
            AutoSequenceMode.PAUSED => AutoSequenceState.PAUSED,
            _ => throw new InvalidOperationException(SequencingCodes.StateInvalid + ": " + SequencingStateValidator.ModeUndefined),
        };
    }

    /// <summary>
    /// Projects the wire AutoSequence mode. The critical gate projects as CRITICAL_SUSPENDED.
    /// Throws InvalidOperationException for an inconsistent state.
    /// </summary>
    public static AutoSequenceMode ProjectAutoSequenceMode(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SequencingStateValidator.RequireValid(state);

        return state.CriticalSuspended ? AutoSequenceMode.CRITICAL_SUSPENDED : state.Mode;
    }

    private static AutoSequenceState ProjectRunningState(SequencingState state)
    {
        if (state.ActiveJob is { } job)
        {
            return job.PumpReady ? AutoSequenceState.JOB_ACTIVE : AutoSequenceState.PUMP_NOT_READY;
        }

        return state.Queue.Count == 0 ? AutoSequenceState.QUEUE_EMPTY : AutoSequenceState.READY_TO_DISPATCH;
    }

    private static SequencingTransition Start(SequencingState state, StartAutoSequence e)
    {
        if (state.CriticalSuspended)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, SequencingCodes.KindStartAutoSequence, e.At, SequencingCodes.CriticalSuspended, null, null);
        }

        if (state.Mode != AutoSequenceMode.OFF)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, SequencingCodes.KindStartAutoSequence, e.At, SequencingCodes.StartNotOff, null, null);
        }

        var next = state with { Mode = AutoSequenceMode.RUNNING };
        return Commit(state, next, SequencingOutcome.APPLIED, SequencingCodes.KindStartAutoSequence, e.At, SequencingCodes.AutoSequenceStarted, null, null);
    }

    private static SequencingTransition Admit(SequencingState state, AdmitQueueEntry e)
    {
        const string kind = SequencingCodes.KindAdmitQueueEntry;

        if (state.CriticalSuspended)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.CriticalSuspended, null, null);
        }

        if (e.Source != SequencingAdmissionSource.SCENARIO_PREPARED)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.AdmissionSourceNotScenario, null, null);
        }

        var structuralRefusal = SequencingTopology.StructuralRefusal(e.Topology, e.SensorId);
        if (structuralRefusal is not null)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, structuralRefusal, null, null);
        }

        if (string.IsNullOrWhiteSpace(e.SourceReason) || e.SecondsSinceLastClean < 0)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.EntryInvalid, null, null);
        }

        if (state.ActiveJob is { } activeJob && string.Equals(activeJob.TargetSensorId, e.SensorId, StringComparison.Ordinal))
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.TargetActive, null, activeJob.JobId);
        }

        if (ContainsSensor(state.Queue, e.SensorId))
        {
            return Commit(state, state, SequencingOutcome.NO_OP, kind, e.At, SequencingCodes.AdmissionDuplicateNoOp, null, null);
        }

        if (state.Queue.Count >= QueueSummary.MaxEntries)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.QueueFull, null, null);
        }

        var nextRevision = NextCounter(state.QueueRevision, "QueueRevision");
        var nextEntrySeq = NextCounter(state.NextEntrySeq, "NextEntrySeq");
        var entryId = SequencingCodes.EntryIdPrefix + state.NextEntrySeq.ToString(CultureInfo.InvariantCulture);
        var queue = new List<SequencingEntry>(state.Queue);
        queue.Add(new SequencingEntry(entryId, e.SensorId, e.SourceReason, e.SecondsSinceLastClean));
        var next = state with
        {
            Queue = queue,
            QueueRevision = nextRevision,
            NextEntrySeq = nextEntrySeq,
        };
        return Commit(state, next, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.Admitted, entryId, null);
    }

    private static SequencingTransition Dispatch(SequencingState state, DispatchHead e)
    {
        const string kind = SequencingCodes.KindDispatchHead;

        if (state.CriticalSuspended)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.CriticalSuspended, null, null);
        }

        if (state.Mode == AutoSequenceMode.PAUSE_REQUESTED)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.DispatchRefusedPauseRequested, null, null);
        }

        if (state.Mode != AutoSequenceMode.RUNNING)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.DispatchRefusedNotRunning, null, null);
        }

        if (state.ActiveJob is { } activeJob)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.DispatchRefusedJobActive, null, activeJob.JobId);
        }

        if (state.Queue.Count == 0)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.DispatchRefusedQueueEmpty, null, null);
        }

        // Only Position 1 is ever considered. An invalid head is removed atomically; the
        // kernel does not scan forward and does not dispatch in the same transition.
        var head = state.Queue[0];
        var remaining = new List<SequencingEntry>(state.Queue);
        remaining.RemoveAt(0);

        var headRefusal = SequencingTopology.StructuralRefusal(e.Topology, head.SensorId);
        var headInvalid = headRefusal is not null
            || string.IsNullOrWhiteSpace(head.SourceReason)
            || head.SecondsSinceLastClean < 0;
        if (headInvalid)
        {
            var removedRevision = NextCounter(state.QueueRevision, "QueueRevision");
            var removed = state with
            {
                Queue = remaining,
                QueueRevision = removedRevision,
            };
            return Commit(state, removed, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.RemovedByEligibility, head.EntryId, null);
        }

        var assignment = SequencingTopology.FindAssignment(e.Topology, head.SensorId)
            ?? throw new InvalidOperationException("A structurally eligible head must have an assignment.");

        var dispatchRevision = NextCounter(state.QueueRevision, "QueueRevision");
        var evidenceSeq = NextCounter(state.EvidenceSeq, "EvidenceSeq");
        var nextJobSeq = NextCounter(state.NextJobSeq, "NextJobSeq");
        var jobId = SequencingCodes.JobIdPrefix + state.NextJobSeq.ToString(CultureInfo.InvariantCulture);
        var dispatchId = SequencingCodes.DispatchIdPrefix + state.NextJobSeq.ToString(CultureInfo.InvariantCulture);
        var job = new SequencingActiveJob(
            JobId: jobId,
            DispatchId: dispatchId,
            TargetSensorId: head.SensorId,
            JetId: assignment.JetId,
            ValveId: assignment.ValveId,
            SourceEntryId: head.EntryId,
            DispatchEvidenceSeq: evidenceSeq,
            PumpReady: e.PumpReady,
            StartedAt: e.At,
            QueueRevisionBefore: state.QueueRevision,
            QueueRevisionAfter: dispatchRevision);

        var dispatched = state with
        {
            Queue = remaining,
            QueueRevision = dispatchRevision,
            ActiveJob = job,
            NextJobSeq = nextJobSeq,
        };
        return Commit(state, dispatched, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.Dispatched, head.EntryId, jobId);
    }

    private static SequencingTransition ObservePump(SequencingState state, ObservePumpReadiness e)
    {
        const string kind = SequencingCodes.KindObservePumpReadiness;

        if (state.ActiveJob is not { } activeJob)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (activeJob.PumpReady == e.Ready)
        {
            return Commit(state, state, SequencingOutcome.NO_OP, kind, e.At, SequencingCodes.PumpReadinessUnchanged, null, activeJob.JobId);
        }

        var next = state with { ActiveJob = activeJob with { PumpReady = e.Ready } };
        return Commit(state, next, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.PumpReadinessChanged, null, activeJob.JobId);
    }

    private static SequencingTransition RaiseCritical(SequencingState state, RaiseCriticalSuspension e)
    {
        const string kind = SequencingCodes.KindRaiseCriticalSuspension;

        if (state.CriticalSuspended)
        {
            return Commit(state, state, SequencingOutcome.NO_OP, kind, e.At, SequencingCodes.CriticalAlreadySuspended, null, null);
        }

        var next = state with { CriticalSuspended = true };
        return Commit(state, next, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.CriticalSuspensionRaised, null, null);
    }

    private static SequencingTransition ApplyPause(SequencingState state, RequestPause e)
    {
        const string kind = SequencingCodes.KindRequestPause;

        if (state.CriticalSuspended)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.CriticalSuspended, null, null);
        }

        if (state.Mode is AutoSequenceMode.OFF or AutoSequenceMode.PAUSED)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.PauseNotRunning, null, null);
        }

        if (state.Mode == AutoSequenceMode.PAUSE_REQUESTED)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.PauseAlreadyRequested, null, state.ActiveJob?.JobId);
        }

        if (state.ActiveJob is { } activeJob)
        {
            var requested = state with { Mode = AutoSequenceMode.PAUSE_REQUESTED };
            return Commit(state, requested, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.PauseRequested, null, activeJob.JobId);
        }

        var paused = state with { Mode = AutoSequenceMode.PAUSED };
        return Commit(state, paused, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.Paused, null, null);
    }

    private static SequencingTransition Release(SequencingState state, ReleaseActiveJob e)
    {
        const string kind = SequencingCodes.KindReleaseActiveJob;

        if (state.ActiveJob is not { } activeJob)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        var evidence = e.Evidence;
        if (!string.Equals(evidence.JobId, activeJob.JobId, StringComparison.Ordinal))
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.ReleaseJobMismatch, null, activeJob.JobId);
        }

        if (!evidence.OutcomeRecorded || !evidence.SafeReturnComplete)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.SafeReturnNotComplete, null, activeJob.JobId);
        }

        if (evidence.Seq <= activeJob.DispatchEvidenceSeq)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.ReleaseSeqNotAfterDispatch, null, activeJob.JobId);
        }

        // Release clears the Job. A pending pause completes now, not earlier.
        var nextMode = state.Mode == AutoSequenceMode.PAUSE_REQUESTED ? AutoSequenceMode.PAUSED : state.Mode;
        var released = state with { ActiveJob = null, Mode = nextMode };
        return Commit(state, released, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.JobReleased, null, activeJob.JobId);
    }

    private static bool ContainsSensor(IReadOnlyList<SequencingEntry> queue, string sensorId)
    {
        foreach (var entry in queue)
        {
            if (string.Equals(entry.SensorId, sensorId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static (string Kind, DateTimeOffset At) Describe(SequencingEvent sequencingEvent) => sequencingEvent switch
    {
        StartAutoSequence e => (SequencingCodes.KindStartAutoSequence, e.At),
        AdmitQueueEntry e => (SequencingCodes.KindAdmitQueueEntry, e.At),
        DispatchHead e => (SequencingCodes.KindDispatchHead, e.At),
        ObservePumpReadiness e => (SequencingCodes.KindObservePumpReadiness, e.At),
        RaiseCriticalSuspension e => (SequencingCodes.KindRaiseCriticalSuspension, e.At),
        RequestPause e => (SequencingCodes.KindRequestPause, e.At),
        ReleaseActiveJob e => (SequencingCodes.KindReleaseActiveJob, e.At),
        _ => throw new ArgumentOutOfRangeException(nameof(sequencingEvent)),
    };

    /// <summary>
    /// Returns value + 1 for a counter that is non-negative and below int.MaxValue.
    /// Any other value throws, so no counter wraps and no evidence is fabricated.
    /// </summary>
    private static int NextCounter(int value, string counter)
    {
        if (value < 0 || value == int.MaxValue)
        {
            throw new InvalidOperationException(
                SequencingCodes.StateInvalid + ": " + SequencingCodes.CounterNotIncrementable + " " + counter);
        }

        return value + 1;
    }

    private static SequencingTransition Commit(
        SequencingState state,
        SequencingState next,
        SequencingOutcome outcome,
        string eventKind,
        DateTimeOffset at,
        string code,
        string? entryId,
        string? jobId)
    {
        var seq = NextCounter(state.EvidenceSeq, "EvidenceSeq");
        var committed = next with { EvidenceSeq = seq };
        var evidence = new SequencingEvidence(
            Seq: seq,
            At: UtcTimestamps.Format(at),
            EventKind: eventKind,
            Outcome: outcome,
            Code: code,
            EntryId: entryId,
            JobId: jobId,
            QueueRevision: committed.QueueRevision);
        return new SequencingTransition(committed, outcome, evidence);
    }
}
