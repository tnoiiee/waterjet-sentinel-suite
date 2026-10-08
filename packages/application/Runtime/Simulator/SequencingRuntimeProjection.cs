using System.Globalization;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// Pure Stage 0.4A CP-3b projection of the sequencing kernel and its retention onto the accepted wire records. It
/// creates no contract and changes no contract. It reads no clock and calls no device.
///
/// <para>
/// Presentation convention (synthetic, not physical progress): <c>PhaseProgress</c> is the 0..1 unit of the existing
/// contract field, so verified phase Pn is presented as n/6. Before the first verified phase, the Job is presented as
/// the next phase, P1, with <c>PhaseIndex</c> 0 and <c>PhaseProgress</c> 0. The P1 pending labels for PREPARING and
/// READY_TO_CLEAN come from the brief. The label for CLEANING before any verified phase is an [OPEN] convention.
/// </para>
///
/// <para>
/// Not projected here: the Pump section (carried unchanged from the previous revision), the Sequence controls
/// (carried unchanged, still disabled), and every Alarm or Communication field (carried unchanged).
/// </para>
/// </summary>
public static class SequencingRuntimeProjection
{
    /// <summary>Prefix of the synthetic critical event identity. The full identity is this prefix plus the critical evidence sequence.</summary>
    public const string CriticalEventIdPrefix = "SYN-CRITICAL-";

    /// <summary>Prefix of the synthetic critical alarm identity. The full identity is this prefix plus the critical evidence sequence.</summary>
    public const string CriticalAlarmIdPrefix = "SYN-PUMP-CRITICAL-";

    /// <summary>The cleaning sub-stage name while running and frozen at the Safe Return trigger (contract comment).</summary>
    public const string CleaningPhaseInProgress = "IN_PROGRESS";

    /// <summary>The fallback feedback identity for a valve that has not been observed. No valve enum identity exists for it ([OPEN]).</summary>
    public const string UnobservedValveFeedback = "UNKNOWN";

    /// <summary>
    /// Projects the GlobalQueue summary. Entries are in FIFO order with position 1 at the head. DirtyScore is never set.
    /// The last dispatch is taken from <paramref name="retention"/>.
    /// </summary>
    public static QueueSummary ProjectQueue(SequencingState state, SequencingRetention retention)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retention);

        var entries = SequencingKernel.ProjectQueueEntries(state);
        return new QueueSummary
        {
            Label = RuntimeStateComposer.QueueLabel,
            Capacity = QueueSummary.MaxEntries,
            TotalQueued = entries.Count,
            Revision = state.QueueRevision,
            Entries = entries,
            AutoSequence = SequencingKernel.ProjectAutoSequenceState(state),
            LastDispatch = retention.LastDispatch,
        };
    }

    /// <summary>
    /// Returns the Sensors with <c>QueueState</c> and <c>IsActiveJobTarget</c> set from the kernel state: ACTIVE and true
    /// for the Active Job target, QUEUED for a queued Sensor, and NONE otherwise. Order and every other field are kept.
    /// </summary>
    public static IReadOnlyList<SensorPresentationState> ProjectSensorQueueStates(
        SequencingState state,
        IReadOnlyList<SensorPresentationState> sensors)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sensors);
        SequencingStateValidator.RequireValid(state);

        var queued = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in state.Queue)
        {
            queued.Add(entry.SensorId);
        }

        var target = state.ActiveJob?.TargetSensorId;
        var projected = new List<SensorPresentationState>(sensors.Count);
        foreach (var sensor in sensors)
        {
            var isTarget = target is not null && string.Equals(sensor.SensorId, target, StringComparison.Ordinal);
            var queueState = isTarget
                ? QueueState.ACTIVE
                : queued.Contains(sensor.SensorId) ? QueueState.QUEUED : QueueState.NONE;
            projected.Add(sensor with { QueueState = queueState, IsActiveJobTarget = isTarget });
        }

        return projected.AsReadOnly();
    }

    /// <summary>
    /// Projects the single Active Cleaning Job, or null when no Job is active. The phase presentation follows the
    /// brief: PREPARING and READY_TO_CLEAN are P1 pending, CLEANING presents its verified phase, and a Safe Return keeps
    /// the presentation frozen at the trigger, because the stage and the verified phase do not change during Safe Return.
    /// </summary>
    public static ActiveCleaningJobState? ProjectActiveJob(SequencingState state, SequencingRetention retention)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retention);
        SequencingStateValidator.RequireValid(state);

        if (state.ActiveJob is not { } job)
        {
            return null;
        }

        var dispatch = retention.LastDispatch ?? throw new InvalidOperationException("An Active Job has no retained dispatch.");
        if (!string.Equals(dispatch.JobId, job.JobId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The retained dispatch does not belong to the Active Job.");
        }

        var (phase, index, progress, label) = PresentationOf(job);
        return new ActiveCleaningJobState
        {
            JobId = job.JobId,
            TargetSensorId = job.TargetSensorId,
            JetId = job.JetId,
            ValveId = job.ValveId,
            Phase = phase,
            PhaseLabel = label,
            PhaseIndex = index,
            StartedAt = UtcTimestamps.Format(job.StartedAt),
            PhaseStartedAt = retention.CurrentPhaseStartedAt ?? throw new InvalidOperationException("The Active Job has no retained phase start."),
            PhaseProgress = progress,
            Lifecycle = job.Lifecycle,
            CleaningPhase = CleaningPhaseInProgress,
            Dispatch = dispatch,
            SafeReturn = job.Lifecycle == JobLifecycle.RUNNING ? null : ProjectSafeReturn(job, retention),
        };
    }

    /// <summary>
    /// Projects the Sequence section. The controls are the supplied ones (carried unchanged, still disabled). The
    /// critical event is projected only while the latch is set, and the last outcome is the retained release record.
    /// </summary>
    public static SequenceState ProjectSequence(SequencingState state, SequencingRetention retention, SequenceControls controls)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(controls);
        SequencingStateValidator.RequireValid(state);

        return new SequenceState
        {
            AutoSequence = SequencingKernel.ProjectAutoSequenceState(state),
            Mode = SequencingKernel.ProjectAutoSequenceMode(state),
            Controls = controls,
            Critical = ProjectCritical(state, retention),
            LastJobOutcome = retention.LastJobOutcome,
        };
    }

    /// <summary>
    /// Projects one candidate Runtime revision from the previous revision and the kernel state. The revision is the
    /// previous revision plus one, and the instant is supplied by the caller. The Pump, walls, alarms, communication,
    /// trend and topology are carried from <paramref name="previous"/>. The result is checked by
    /// <see cref="RuntimeStateInvariants.RequireValid"/> before it is returned. Nothing is committed.
    /// </summary>
    public static RuntimeState ProjectRuntimeState(
        RuntimeState previous,
        SequencingState state,
        SequencingRetention retention,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retention);
        if (generatedAtUtc <= previous.GeneratedAtUtc)
        {
            throw new InvalidOperationException("A candidate revision must be stamped strictly later than the previous revision.");
        }

        var candidate = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = generatedAtUtc,
            Sensors = ProjectSensorQueueStates(state, previous.Sensors),
            ActiveJob = ProjectActiveJob(state, retention),
            Queue = ProjectQueue(state, retention),
            Sequence = ProjectSequence(state, retention, previous.Sequence.Controls),
        };

        RuntimeStateInvariants.RequireValid(candidate);
        return candidate;
    }

    private static (JobPhase Phase, int Index, double Progress, string Label) PresentationOf(SequencingActiveJob job) => job.Stage switch
    {
        CleaningStage.PREPARING => (JobPhase.P1, 0, 0.0, "P1 PENDING - PREPARING"),
        CleaningStage.READY_TO_CLEAN => (JobPhase.P1, 0, 0.0, "P1 PENDING - READY_TO_CLEAN"),
        _ => job.VerifiedPhase is { } verified
            ? (verified, PhaseIndexOf(verified), PhaseIndexOf(verified) / 6.0, verified.ToString() + " - CLEANING")
            : (JobPhase.P1, 0, 0.0, "P1 PENDING - CLEANING"),
    };

    private static int PhaseIndexOf(JobPhase phase) => phase switch
    {
        JobPhase.P1 => 1,
        JobPhase.P2 => 2,
        JobPhase.P3 => 3,
        JobPhase.P4 => 4,
        JobPhase.P5 => 5,
        JobPhase.P6 => 6,
        _ => throw new InvalidOperationException("Undefined verified phase."),
    };

    private static SafeReturnState ProjectSafeReturn(SequencingActiveJob job, SequencingRetention retention)
    {
        var tail = retention.CurrentSafeReturnTail;
        var trigger = job.Trigger ?? throw new InvalidOperationException("A Safe Return Job has no trigger.");
        var pending = job.PendingOutcome ?? throw new InvalidOperationException("A Safe Return Job has no pending outcome.");
        var startedAt = tail.FirstOrDefault(evidence => evidence.Step == SafeReturnStep.SR1)?.At
            ?? throw new InvalidOperationException("A Safe Return Job has no SR1 record.");
        var failure = tail.LastOrDefault(evidence => evidence.Step == SafeReturnStep.SR_FAILED);

        return new SafeReturnState
        {
            Step = job.Step,
            Trigger = trigger,
            PendingOutcome = pending.ToString(),
            PhaseAtTrigger = job.VerifiedPhase ?? JobPhase.P1,
            StartedAt = startedAt,
            Valve = new SafeReturnValveLeg
            {
                ValveId = job.ValveId,
                Command = SafeReturnStep.SR2.ToString(),
                CommandSeq = SequenceOf(tail, SafeReturnStep.SR2),
                Feedback = job.LastValveFeedback?.ToString() ?? UnobservedValveFeedback,
                FeedbackSeq = retention.CurrentValveFeedback?.Seq,
            },
            Axis = new SafeReturnAxisLeg
            {
                Command = SafeReturnStep.SR4.ToString(),
                CommandSeq = SequenceOf(tail, SafeReturnStep.SR4),
                Standby = retention.CurrentAxisFeedback?.AxisFeedback?.ToString() ?? AxisFeedbackState.UNKNOWN.ToString(),
                StandbySeq = SequenceOf(tail, SafeReturnStep.SR5),
            },
            Failure = failure is null
                ? null
                : new SafeReturnFailure
                {
                    Reason = failure.Code,
                    AtLifecycle = failure.LifecycleBefore ?? job.Lifecycle,
                    At = failure.At,
                    Seq = failure.Seq,
                },
            Events = tail.Select(ToEvent).ToArray(),
        };
    }

    private static CriticalPumpEvent? ProjectCritical(SequencingState state, SequencingRetention retention)
    {
        if (!state.CriticalSuspended)
        {
            return null;
        }

        var condition = retention.FirstCritical
            ?? throw new InvalidOperationException("The critical latch is set but no critical condition was retained.");
        var evidence = condition.Evidence;
        var seq = evidence.Seq.ToString(CultureInfo.InvariantCulture);
        var released = condition.JobId is not null
            && string.Equals(retention.LastJobOutcome?.JobId, condition.JobId, StringComparison.Ordinal);
        var failed = condition.JobId is not null
            && state.ActiveJob is { } active
            && active.Lifecycle == JobLifecycle.SAFE_RETURN_FAILED
            && string.Equals(active.JobId, condition.JobId, StringComparison.Ordinal);

        return new CriticalPumpEvent
        {
            EventId = CriticalEventIdPrefix + seq,
            Kind = condition.Observation == PumpObservation.TRIP
                ? CriticalPumpKind.MAIN_PUMP_TRIP
                : CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP,
            Severity = AlarmSeverity.HIGH,
            RaisedAt = evidence.At,
            EvidenceSeq = evidence.Seq,
            ConditionActive = true,
            ClearedAt = null,
            Acknowledged = false,
            AcknowledgedAt = null,
            AlarmId = CriticalAlarmIdPrefix + seq,
            JobId = condition.JobId,
            TargetSensorId = condition.TargetSensorId,
            PhaseAtEvent = condition.PhaseAtEvent,
            SafeReturnRequired = condition.SafeReturnRequired,
            SafeReturnComplete = released,
            SafeReturnFailed = failed,
            ModalOpen = true,
            ModalClosedAt = null,
        };
    }

    private static int? SequenceOf(IReadOnlyList<SequencingEvidence> tail, SafeReturnStep step) =>
        tail.LastOrDefault(evidence => evidence.Step == step)?.Seq;

    private static SafeReturnEvent ToEvent(SequencingEvidence evidence) => new()
    {
        Seq = evidence.Seq,
        Step = evidence.Step,
        Event = evidence.Code,
        At = evidence.At,
    };
}
