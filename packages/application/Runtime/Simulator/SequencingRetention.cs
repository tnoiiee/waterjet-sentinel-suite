using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// The first critical condition of a run, captured on the transition that raised the critical latch. It keeps the
/// Job context that was present at latch time. Later critical observations are never retained here.
/// </summary>
public sealed record RetainedCriticalCondition(
    SequencingEvidence Evidence,
    PumpObservation Observation,
    string? JobId,
    string? TargetSensorId,
    JobPhase? PhaseAtEvent,
    bool SafeReturnRequired);

/// <summary>
/// Bounded, deterministic evidence retention for one SIMULATOR run (Stage 0.4A CP-3b). It is a pure value: each
/// <see cref="Retain"/> call returns a new instance and changes nothing else. It has five distinct scopes.
///
/// <list type="bullet">
/// <item><description>Process-run evidence log: the newest <see cref="EvidenceLogCapacity"/> records, oldest first. The count of older records dropped is kept in <see cref="EvidenceDropped"/>.</description></item>
/// <item><description>Current Job Safe Return tail: the Safe Return records of the current Job only, oldest first, capacity <see cref="SafeReturnTailCapacity"/>. It is reset when a Job is successfully dispatched and archived into <see cref="LastJobOutcome"/> when the Job releases.</description></item>
/// <item><description>Last dispatch: kept until the next dispatch. The Job does not store its source reason or origin, so it is read from the queue head that was dispatched.</description></item>
/// <item><description>Last Job outcome: captured on the SR7 release from the release records and the archived tail, before the Active Job is lost.</description></item>
/// <item><description>First critical condition: captured once, on the transition that raises the latch.</description></item>
/// </list>
///
/// Retention never clears the critical latch and never drives a Job.
/// </summary>
public sealed record SequencingRetention
{
    /// <summary>Maximum number of evidence records kept in <see cref="EvidenceLog"/>.</summary>
    public const int EvidenceLogCapacity = 256;

    /// <summary>Maximum number of Safe Return records kept in <see cref="CurrentSafeReturnTail"/> for the current Job.</summary>
    public const int SafeReturnTailCapacity = 16;

    /// <summary>The process-run evidence log, oldest first and newest last.</summary>
    public required IReadOnlyList<SequencingEvidence> EvidenceLog { get; init; }

    /// <summary>The number of older evidence records dropped to keep <see cref="EvidenceLog"/> bounded.</summary>
    public required int EvidenceDropped { get; init; }

    /// <summary>The Safe Return records of the current Job, oldest first. Empty when no Job is active.</summary>
    public required IReadOnlyList<SequencingEvidence> CurrentSafeReturnTail { get; init; }

    /// <summary>The number of current-Job Safe Return records dropped by the tail capacity (zero in every scenario).</summary>
    public required int CurrentTailDropped { get; init; }

    /// <summary>The most recent dispatch, or null when no Job has been dispatched in this run.</summary>
    public DispatchRecord? LastDispatch { get; init; }

    /// <summary>The frozen record of the last released Job, or null when no Job has released in this run.</summary>
    public JobOutcomeRecord? LastJobOutcome { get; init; }

    /// <summary>The first critical condition of the run, or null when the latch has never been raised.</summary>
    public RetainedCriticalCondition? FirstCritical { get; init; }

    /// <summary>The start instant of the current verified phase (the matching PHASE_VERIFIED instant), or the Job start before the first verification. Null when no Job is active.</summary>
    public string? CurrentPhaseStartedAt { get; init; }

    /// <summary>The last applied valve observation of the current Job, or null before any valve observation.</summary>
    public SequencingEvidence? CurrentValveFeedback { get; init; }

    /// <summary>The last applied axis observation of the current Job, or null before any axis observation.</summary>
    public SequencingEvidence? CurrentAxisFeedback { get; init; }

    /// <summary>A retention with nothing recorded.</summary>
    public static SequencingRetention Empty { get; } = new()
    {
        EvidenceLog = new List<SequencingEvidence>().AsReadOnly(),
        EvidenceDropped = 0,
        CurrentSafeReturnTail = new List<SequencingEvidence>().AsReadOnly(),
        CurrentTailDropped = 0,
    };

    /// <summary>
    /// Returns the retention after one kernel transition. <paramref name="before"/> is the state the transition started
    /// from. The latch rule compares the state before and after the transition, not the evidence code.
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

        var tail = new List<SequencingEvidence>(previous.CurrentSafeReturnTail);
        var tailDropped = previous.CurrentTailDropped;
        var lastDispatch = previous.LastDispatch;
        var lastOutcome = previous.LastJobOutcome;
        var firstCritical = previous.FirstCritical;
        var phaseStartedAt = previous.CurrentPhaseStartedAt;
        var valveFeedback = previous.CurrentValveFeedback;
        var axisFeedback = previous.CurrentAxisFeedback;
        var latchRaised = !before.CriticalSuspended && transition.State.CriticalSuspended;

        foreach (var evidence in transition.Records)
        {
            if (evidence.Code == SequencingCodes.Dispatched && transition.State.ActiveJob is { } dispatched && before.Queue.Count > 0)
            {
                lastDispatch = DispatchOf(dispatched, before.Queue[0]);
                tail = new List<SequencingEvidence>();
                tailDropped = 0;
                phaseStartedAt = UtcTimestamps.Format(dispatched.StartedAt);
                valveFeedback = null;
                axisFeedback = null;
            }

            if (evidence.Step is not null)
            {
                tail.Add(evidence);
                if (tail.Count > SafeReturnTailCapacity)
                {
                    tail.RemoveAt(0);
                    tailDropped++;
                }
            }

            if (evidence.Code == SequencingCodes.PhaseVerified)
            {
                phaseStartedAt = evidence.At;
            }

            // Preparation limits are retained separately from SR3 close resolution.
            // SR3 may represent a pressure inference without any Lower-limit observation.
            if (evidence.Outcome == SequencingOutcome.APPLIED
                && evidence.EventKind == SequencingCodes.KindValveLimitObserved
                && evidence.ValveFeedback is not null
                && evidence.Step is null)
            {
                valveFeedback = evidence;
            }

            if (evidence.Outcome == SequencingOutcome.APPLIED && evidence.AxisFeedback is not null)
            {
                axisFeedback = evidence;
            }

            if (firstCritical is null && latchRaised && evidence.Pump is PumpObservation.UNEXPECTED_STOP or PumpObservation.TRIP)
            {
                firstCritical = new RetainedCriticalCondition(
                    evidence,
                    evidence.Pump.Value,
                    before.ActiveJob?.JobId,
                    before.ActiveJob?.TargetSensorId,
                    before.ActiveJob?.VerifiedPhase,
                    before.ActiveJob is not null);
            }

            if (evidence.Code == SequencingCodes.JobReleased && evidence.Step == SafeReturnStep.SR7)
            {
                var released = before.ActiveJob ?? throw new InvalidOperationException("A release was recorded with no Active Job.");
                lastOutcome = OutcomeOf(released, tail, evidence, transition.State);
                tail = new List<SequencingEvidence>();
                tailDropped = 0;
                phaseStartedAt = null;
                valveFeedback = null;
                axisFeedback = null;
            }
        }

        return new SequencingRetention
        {
            EvidenceLog = log.AsReadOnly(),
            EvidenceDropped = dropped,
            CurrentSafeReturnTail = tail.AsReadOnly(),
            CurrentTailDropped = tailDropped,
            LastDispatch = lastDispatch,
            LastJobOutcome = lastOutcome,
            FirstCritical = firstCritical,
            CurrentPhaseStartedAt = phaseStartedAt,
            CurrentValveFeedback = valveFeedback,
            CurrentAxisFeedback = axisFeedback,
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

    private static JobOutcomeRecord OutcomeOf(
        SequencingActiveJob job,
        IReadOnlyList<SequencingEvidence> tail,
        SequencingEvidence release,
        SequencingState releasedState) => new()
    {
        JobId = job.JobId,
        TargetSensorId = job.TargetSensorId,
        DispatchId = job.DispatchId,
        QueueRevisionBefore = job.QueueRevisionBefore,
        QueueRevisionAfter = job.QueueRevisionAfter,
        QueueEntryId = job.SourceEntryId,
        PhaseAtTrigger = job.VerifiedPhase ?? JobPhase.P1,
        Trigger = job.Trigger ?? throw new InvalidOperationException("A released Job has no trigger."),
        CleaningPhasesComplete = job.VerifiedPhase == JobPhase.P6,
        Outcome = (job.PendingOutcome ?? throw new InvalidOperationException("A released Job has no pending outcome.")).ToString(),
        ValveId = job.ValveId,
        ValveCloseCommandSeq = RequireSeq(tail, SafeReturnStep.SR2),
        ValveClosedConfirmedSeq = tail.LastOrDefault(e => e.Step == SafeReturnStep.SR3
            && e.CloseResolution == ValveCloseResolution.LOWER_LIMIT_CONFIRMED)?.Seq,
        ValveCloseResolution = tail.LastOrDefault(e => e.CloseResolution is not null)?.CloseResolution?.ToString(),
        ValveDiagnosis = tail.LastOrDefault(e => e.CloseResolution is not null)?.Diagnosis?.ToString(),
        QualifiedCompletion = job.PendingOutcome == CleaningJobOutcome.COMPLETED
            && releasedState.EquipmentFaults.Any(f => f.Diagnosis == ValveDiagnosis.UPPER_LIMIT_SENSOR_FAULT.ToString())
            && releasedState.EquipmentFaults.Any(f => f.Diagnosis == ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT.ToString())
            ? "COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS"
            : job.PendingOutcome == CleaningJobOutcome.COMPLETED
                && releasedState.EquipmentFaults.Any(f => f.Diagnosis == ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT.ToString())
                ? "COMPLETE_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT"
                : job.PendingOutcome == CleaningJobOutcome.COMPLETED
                    && releasedState.EquipmentFaults.Any(f => f.Diagnosis == ValveDiagnosis.UPPER_LIMIT_SENSOR_FAULT.ToString())
                    ? "COMPLETE_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT" : null,
        QualifiedRemarks = job.PendingOutcome == CleaningJobOutcome.COMPLETED
            ? releasedState.EquipmentFaults.Select(f => f.Diagnosis switch
        {
            "UPPER_LIMIT_SENSOR_FAULT" => "COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT",
            "LOWER_LIMIT_SENSOR_FAULT" => "COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT",
            _ => f.Diagnosis,
        }).ToArray() : Array.Empty<string>(),
        EquipmentFaults = releasedState.EquipmentFaults,
        AxisReturnCommandSeq = RequireSeq(tail, SafeReturnStep.SR4),
        StandbyConfirmedSeq = RequireSeq(tail, SafeReturnStep.SR5),
        OutcomeSeq = RequireSeq(tail, SafeReturnStep.SR6),
        ReleaseSeq = release.Seq,
        AutoSequenceAtRelease = SequencingKernel.ProjectAutoSequenceState(releasedState),
        FinalizedAt = release.At,
        Events = tail.Select(ToEvent).ToArray(),
    };

    private static int RequireSeq(IReadOnlyList<SequencingEvidence> tail, SafeReturnStep step)
    {
        for (var index = tail.Count - 1; index >= 0; index--)
        {
            if (tail[index].Step == step)
            {
                return tail[index].Seq;
            }
        }

        throw new InvalidOperationException($"The released Job has no {step} evidence record.");
    }

    private static SafeReturnEvent ToEvent(SequencingEvidence evidence) => new()
    {
        Seq = evidence.Seq,
        Step = evidence.Step,
        Event = evidence.Code,
        At = evidence.At,
    };
}
