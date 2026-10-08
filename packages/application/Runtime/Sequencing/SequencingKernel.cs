using System.Globalization;
using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Pure Stage 0.4A sequencing kernel: the GlobalQueue (capacity 8, FIFO,
/// dispatch-ready entries only, head-only dispatch, no scan-forward), the
/// AutoSequence gate with exactly one Active Job, and (Stage 0.4A CP-2) the
/// Cleaning Job lifecycle with Mandatory Safe Return.
///
/// <para>
/// The kernel is deterministic: identical initial state and identical event
/// sequence produce identical states and evidence. It reads no clock, no random
/// source and no configuration. It executes no Job phase, no Pump action, no
/// Valve or Axis action, and no Safe Return step against equipment. Safe Return
/// steps are recorded as ordered evidence, with intents as pure data. There is no
/// command or actuation path.
/// </para>
///
/// <para>
/// Mandatory Safe Return (CP-2): every terminal intent (normal completion, abort,
/// execution failure, Pump UNEXPECTED_STOP or TRIP) enters SR1 to SR2 in one
/// transition. SR1 turns cleaning and synthetic water off, and SR2 requests the
/// paired valve close. SR3 needs a valve observation CLOSED that arrives after
/// SR2. Only then is the axis return requested (SR4). SR5 needs AT_STANDBY. SR6
/// records the pending outcome and SR7 releases the Job, both in the same
/// transition as SR5. No outcome is recorded before SR5. Release is never an
/// external input.
/// </para>
///
/// <para>
/// Refusals and no-ops leave the queue, queue revision, Job and AutoSequence state
/// unchanged. The evidence sequence advances on every record, so each evidence
/// record has a unique number. A transition may emit several records, all from the
/// one evidence sequence.
/// </para>
///
/// <para>
/// State integrity: <see cref="Apply"/> validates the supplied state before any
/// transition. An inconsistent state is refused with SEQUENCING_STATE_INVALID and
/// no other field changes. Projections throw InvalidOperationException for an
/// inconsistent state and never fall back to a running state.
/// </para>
/// </summary>
public static class SequencingKernel
{
    /// <summary>The initial state: AutoSequence OFF, empty queue, revision 0, no Job.</summary>
    public static SequencingState Initial() =>
        new(Array.Empty<SequencingEntry>(), 0, null, AutoSequenceMode.OFF, false, 0, 1, 1);

    /// <summary>
    /// Applies one event and returns the next state with its evidence records. An
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
            ObservePumpReadiness e => ObservePumpReadinessGate(state, e),
            ObservePumpState e => ObservePump(state, e),
            RequestPause e => ApplyPause(state, e),
            AdvanceJobPreparation e => AdvancePreparation(state, e),
            BeginCleaning e => BeginCleaningStep(state, e),
            ExecutionPhaseVerified e => VerifyPhase(state, e),
            RequestNormalCompletion e => RequestCompletion(state, e),
            RequestAbort e => RequestAbortStep(state, e),
            ReportExecutionFailure e => ReportFailure(state, e),
            ValveLimitObserved e => ObserveValve(state, e),
            AxisFeedbackObserved e => ObserveAxis(state, e),
            FeedbackTimeoutExpired e => ExpireFeedback(state, e),
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
    /// then PAUSE_REQUESTED and PAUSED. While RUNNING, a Job in any Safe Return
    /// lifecycle (and any Job not yet ready) projects as JOB_ACTIVE or PUMP_NOT_READY.
    /// With no Job, the queue shows QUEUE_EMPTY or READY_TO_DISPATCH. Throws
    /// InvalidOperationException for an inconsistent state.
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

    /// <summary>
    /// Projects the lifecycle of the Active Job, or null when no Job is active.
    /// Throws InvalidOperationException for an inconsistent state.
    /// </summary>
    public static JobLifecycle? ProjectJobLifecycle(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SequencingStateValidator.RequireValid(state);

        return state.ActiveJob?.Lifecycle;
    }

    private static AutoSequenceState ProjectRunningState(SequencingState state)
    {
        if (state.ActiveJob is { } job)
        {
            return job.Lifecycle != JobLifecycle.RUNNING || job.PumpReady
                ? AutoSequenceState.JOB_ACTIVE
                : AutoSequenceState.PUMP_NOT_READY;
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
            QueueRevisionAfter: dispatchRevision,
            Lifecycle: JobLifecycle.RUNNING,
            Stage: CleaningStage.PREPARING,
            VerifiedPhase: null,
            CleaningActive: false,
            WaterOutputOn: false,
            LastValveFeedback: null,
            PendingOutcome: null,
            Trigger: null,
            TriggerReason: null,
            Step: null,
            FailureCode: null,
            Ledger: SafeReturnLedger.Empty);

        var dispatched = state with
        {
            Queue = remaining,
            QueueRevision = dispatchRevision,
            ActiveJob = job,
            NextJobSeq = nextJobSeq,
        };
        return Commit(state, dispatched, SequencingOutcome.APPLIED, kind, e.At, SequencingCodes.Dispatched, head.EntryId, jobId);
    }

    /// <summary>
    /// The pre-cleaning readiness gate. During Cleaning a not-ready observation is
    /// refused: it must be classified through <see cref="ObservePumpState"/>.
    /// </summary>
    private static SequencingTransition ObservePumpReadinessGate(SequencingState state, ObservePumpReadiness e)
    {
        const string kind = SequencingCodes.KindObservePumpReadiness;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.SafeReturnInProgress, job, job.Lifecycle, job.Lifecycle));
        }

        if (job.CleaningActive && !e.Ready)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PumpStopRequiresClassification, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (job.PumpReady == e.Ready)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PumpReadinessUnchanged, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        var next = state with { ActiveJob = job with { PumpReady = e.Ready } };
        return Single(state, next, SequencingOutcome.APPLIED, JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.PumpReadinessChanged, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
    }

    private static SequencingTransition ObservePump(SequencingState state, ObservePumpState e)
    {
        const string kind = SequencingCodes.KindObservePumpState;

        if (e.Observation is PumpObservation.UNEXPECTED_STOP or PumpObservation.TRIP)
        {
            return CriticalPump(state, e);
        }

        var observation = e.Observation;
        if (state.ActiveJob is not { } job)
        {
            return Single(state, state, SequencingOutcome.NO_OP, Record(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PumpObservationNoted) with { Pump = observation });
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PumpObservationNotedDuringSafeReturn, job, job.Lifecycle, job.Lifecycle) with { Pump = observation });
        }

        if (observation == PumpObservation.EXPECTED_STOP)
        {
            // An expected stop is non-critical. It is refused while cleaning, because no
            // expected-stop cleaning path is modelled in CP-2 (O-3).
            if (job.CleaningActive)
            {
                return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PumpExpectedStopNotModelled, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });
            }

            if (!job.PumpReady)
            {
                return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PumpReadinessUnchanged, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });
            }

            var stopped = state with { ActiveJob = job with { PumpReady = false } };
            return Single(state, stopped, SequencingOutcome.APPLIED, JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.PumpExpectedStopNoted, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });
        }

        // READY.
        if (job.PumpReady)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PumpReadinessUnchanged, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });
        }

        var ready = state with { ActiveJob = job with { PumpReady = true } };
        return Single(state, ready, SequencingOutcome.APPLIED, JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.PumpReadinessChanged, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });
    }

    /// <summary>
    /// A Pump UNEXPECTED_STOP or TRIP. It sets the critical latch in the same
    /// transition. With a running Job it also disables water, requests the paired
    /// valve close, and enters Safe Return with pending ABORTED. It never delays,
    /// dispatches, or clears the latch. During a Safe Return it sets the latch only.
    /// </summary>
    private static SequencingTransition CriticalPump(SequencingState state, ObservePumpState e)
    {
        const string kind = SequencingCodes.KindObservePumpState;
        var observation = e.Observation;
        var latchRaised = !state.CriticalSuspended;

        if (state.ActiveJob is not { } job)
        {
            return latchRaised
                ? Single(state, state with { CriticalSuspended = true }, SequencingOutcome.APPLIED, Record(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.CriticalSuspensionRaised) with { Pump = observation })
                : Single(state, state, SequencingOutcome.NO_OP, Record(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.CriticalAlreadySuspended) with { Pump = observation });
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            // Safe Return is already in progress: the latch is set, the steps are not restarted.
            var outcome = latchRaised ? SequencingOutcome.APPLIED : SequencingOutcome.NO_OP;
            var raised = state with { CriticalSuspended = true };
            return Single(state, raised, outcome, JobRecord(kind, e.At, outcome, SequencingCodes.CriticalEventDuringSafeReturn, job, job.Lifecycle, job.Lifecycle) with { Pump = observation });
        }

        var trail = new EvidenceTrail(state.EvidenceSeq);
        var latchOutcome = latchRaised ? SequencingOutcome.APPLIED : SequencingOutcome.NO_OP;
        var latchCode = latchRaised ? SequencingCodes.CriticalSuspensionRaised : SequencingCodes.CriticalAlreadySuspended;
        trail.Add(JobRecord(kind, e.At, latchOutcome, latchCode, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Pump = observation });

        var trigger = observation == PumpObservation.TRIP
            ? SequencingCodes.TriggerPumpTrip
            : SequencingCodes.TriggerPumpUnexpectedStop;
        var stopped = StartSafeReturn(trail, job, kind, e.At, trigger, CleaningJobOutcome.ABORTED, null, JobLifecycle.RUNNING);
        return trail.Finish(state with { ActiveJob = stopped, CriticalSuspended = true }, SequencingOutcome.APPLIED);
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

    private static SequencingTransition AdvancePreparation(SequencingState state, AdvanceJobPreparation e)
    {
        const string kind = SequencingCodes.KindAdvanceJobPreparation;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.SafeReturnInProgress, job, job.Lifecycle, job.Lifecycle));
        }

        if (job.Stage != CleaningStage.PREPARING)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.PreparationAlreadyAdvanced, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (job.LastValveFeedback != ValveFeedbackState.CLOSED)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.ValveNotClosed, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, valve: job.LastValveFeedback));
        }

        var next = state with { ActiveJob = job with { Stage = CleaningStage.READY_TO_CLEAN } };
        return Single(state, next, SequencingOutcome.APPLIED, JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.PreparationAdvanced, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, valve: ValveFeedbackState.CLOSED));
    }

    private static SequencingTransition BeginCleaningStep(SequencingState state, BeginCleaning e)
    {
        const string kind = SequencingCodes.KindBeginCleaning;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.SafeReturnInProgress, job, job.Lifecycle, job.Lifecycle));
        }

        if (job.Stage == CleaningStage.CLEANING)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.CleaningAlreadyActive, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (job.Stage == CleaningStage.PREPARING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PreparationNotComplete, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (!job.PumpReady)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PumpNotReady, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (job.LastValveFeedback != ValveFeedbackState.CLOSED)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.ValveNotClosed, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, valve: job.LastValveFeedback));
        }

        var cleaning = job with { Stage = CleaningStage.CLEANING, CleaningActive = true, WaterOutputOn = true };
        var started = JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.CleaningStarted, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING) with { Intent = SequencingCodes.IntentWaterOutputOn };
        return Single(state, state with { ActiveJob = cleaning }, SequencingOutcome.APPLIED, started);
    }

    private static SequencingTransition VerifyPhase(SequencingState state, ExecutionPhaseVerified e)
    {
        const string kind = SequencingCodes.KindExecutionPhaseVerified;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.SafeReturnInProgress, job, job.Lifecycle, job.Lifecycle, phase: e.Phase));
        }

        if (job.Stage != CleaningStage.CLEANING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.CleaningNotActive, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, phase: e.Phase));
        }

        var expected = NextPhase(job.VerifiedPhase);
        if (expected is null || e.Phase != expected)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PhaseOutOfOrder, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, phase: e.Phase));
        }

        if (e.Phase == JobPhase.P1 && job.LastValveFeedback != ValveFeedbackState.CLOSED)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.ValveNotClosed, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, phase: e.Phase, valve: job.LastValveFeedback));
        }

        if (e.Phase == JobPhase.P2 && job.LastValveFeedback != ValveFeedbackState.OPEN)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.ValveNotOpen, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, phase: e.Phase, valve: job.LastValveFeedback));
        }

        var advanced = state with { ActiveJob = job with { VerifiedPhase = e.Phase } };
        return Single(state, advanced, SequencingOutcome.APPLIED, JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.PhaseVerified, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, phase: e.Phase));
    }

    private static SequencingTransition RequestCompletion(SequencingState state, RequestNormalCompletion e)
    {
        const string kind = SequencingCodes.KindRequestNormalCompletion;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.SafeReturnInProgress, job, job.Lifecycle, job.Lifecycle));
        }

        if (job.Stage != CleaningStage.CLEANING)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.CleaningNotActive, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        if (job.VerifiedPhase != JobPhase.P6)
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.PhaseIncomplete, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        var trail = new EvidenceTrail(state.EvidenceSeq);
        var stopped = StartSafeReturn(trail, job, kind, e.At, SequencingCodes.TriggerNormalCompletion, CleaningJobOutcome.COMPLETED, null, JobLifecycle.RUNNING);
        return trail.Finish(state with { ActiveJob = stopped }, SequencingOutcome.APPLIED);
    }

    private static SequencingTransition RequestAbortStep(SequencingState state, RequestAbort e)
    {
        const string kind = SequencingCodes.KindRequestAbort;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            // Noted and not restarted: the Safe Return already in progress continues.
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.AbortNotedDuringSafeReturn, job, job.Lifecycle, job.Lifecycle));
        }

        var trail = new EvidenceTrail(state.EvidenceSeq);
        trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.AbortRequested, job, JobLifecycle.RUNNING, JobLifecycle.ABORTING));
        var stopped = StartSafeReturn(trail, job, kind, e.At, SequencingCodes.TriggerAbort, CleaningJobOutcome.ABORTED, null, JobLifecycle.ABORTING);
        return trail.Finish(state with { ActiveJob = stopped }, SequencingOutcome.APPLIED);
    }

    private static SequencingTransition ReportFailure(SequencingState state, ReportExecutionFailure e)
    {
        const string kind = SequencingCodes.KindReportExecutionFailure;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        if (job.Lifecycle != JobLifecycle.RUNNING)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.ExecutionFailureNotedDuringSafeReturn, job, job.Lifecycle, job.Lifecycle));
        }

        if (string.IsNullOrWhiteSpace(e.ReasonCode))
        {
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.FailureReasonInvalid, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING));
        }

        var trail = new EvidenceTrail(state.EvidenceSeq);
        var stopped = StartSafeReturn(trail, job, kind, e.At, SequencingCodes.TriggerExecutionFailure, CleaningJobOutcome.FAILED, e.ReasonCode, JobLifecycle.RUNNING);
        return trail.Finish(state with { ActiveJob = stopped }, SequencingOutcome.APPLIED);
    }

    private static SequencingTransition ObserveValve(SequencingState state, ValveLimitObserved e)
    {
        const string kind = SequencingCodes.KindValveLimitObserved;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        var observed = ValveFeedbackDerivation.Derive(e.UpperLimit, e.LowerLimit);
        if (!string.Equals(e.ValveId, job.ValveId, StringComparison.Ordinal))
        {
            // Feedback for another valve is refused (O-6). It is recorded as evidence only.
            return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.ValveIdMismatch, job, job.Lifecycle, job.Lifecycle, valve: observed));
        }

        return job.Lifecycle switch
        {
            JobLifecycle.RUNNING => ObserveValveWhileRunning(state, job, e, observed),
            JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED => ObserveValveWhileAwaitingClose(state, job, e, observed),
            _ => Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.ValveFeedbackNoEffect, job, job.Lifecycle, job.Lifecycle, valve: observed)),
        };
    }

    private static SequencingTransition ObserveValveWhileRunning(SequencingState state, SequencingActiveJob job, ValveLimitObserved e, ValveFeedbackState observed)
    {
        const string kind = SequencingCodes.KindValveLimitObserved;

        if (observed == ValveFeedbackState.INVALID_LIMIT_STATE)
        {
            // Physically contradictory feedback is an immediate execution failure (O-6).
            var trail = new EvidenceTrail(state.EvidenceSeq);
            trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.ValveFeedbackObserved, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, valve: observed));
            var invalid = job with { LastValveFeedback = observed };
            var stopped = StartSafeReturn(trail, invalid, kind, e.At, SequencingCodes.TriggerExecutionFailure, CleaningJobOutcome.FAILED, SequencingCodes.ValveInvalidLimitState, JobLifecycle.RUNNING);
            return trail.Finish(state with { ActiveJob = stopped }, SequencingOutcome.APPLIED);
        }

        var unchanged = job.LastValveFeedback == observed;
        var outcome = unchanged ? SequencingOutcome.NO_OP : SequencingOutcome.APPLIED;
        var code = unchanged ? SequencingCodes.ValveFeedbackUnchanged : SequencingCodes.ValveFeedbackObserved;
        var updated = state with { ActiveJob = job with { LastValveFeedback = observed } };
        return Single(state, updated, outcome, JobRecord(kind, e.At, outcome, code, job, JobLifecycle.RUNNING, JobLifecycle.RUNNING, valve: observed));
    }

    private static SequencingTransition ObserveValveWhileAwaitingClose(SequencingState state, SequencingActiveJob job, ValveLimitObserved e, ValveFeedbackState observed)
    {
        const string kind = SequencingCodes.KindValveLimitObserved;
        const JobLifecycle waiting = JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED;
        var trail = new EvidenceTrail(state.EvidenceSeq);

        // Only this observation (made after SR2) can confirm SR3. An earlier reading is never reused.
        if (observed == ValveFeedbackState.CLOSED)
        {
            trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.ValveFeedbackObserved, job, waiting, waiting, valve: observed));
            var closed = trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR3), job, waiting, JobLifecycle.SAFE_RETURN_TO_STANDBY, step: SafeReturnStep.SR3, valve: observed));
            // SR4 is requested only after SR3 is confirmed, in the same transition.
            var request = trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR4), job, JobLifecycle.SAFE_RETURN_TO_STANDBY, JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, step: SafeReturnStep.SR4, intent: SequencingCodes.IntentAxisToStandby));
            var advanced = job with
            {
                Lifecycle = JobLifecycle.SAFE_RETURN_VERIFY_STANDBY,
                Step = SafeReturnStep.SR4,
                LastValveFeedback = ValveFeedbackState.CLOSED,
                Ledger = job.Ledger with { ValveClosedSeq = closed, AxisReturnRequestSeq = request },
            };
            return trail.Finish(state with { ActiveJob = advanced }, SequencingOutcome.APPLIED);
        }

        if (observed == ValveFeedbackState.INVALID_LIMIT_STATE)
        {
            trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.ValveFeedbackObserved, job, waiting, waiting, valve: observed));
            var failed = FailSafeReturn(trail, job with { LastValveFeedback = observed }, kind, e.At, SequencingCodes.ValveInvalidLimitState, observed, null);
            return trail.Finish(state with { ActiveJob = failed }, SequencingOutcome.APPLIED);
        }

        // OPEN or TRANSIT_OR_FAULT: the close is not confirmed. The wait continues until a timeout input (O-6).
        trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.ValveNotConfirmed, job, waiting, waiting, valve: observed));
        return trail.Finish(state with { ActiveJob = job with { LastValveFeedback = observed } }, SequencingOutcome.APPLIED);
    }

    private static SequencingTransition ObserveAxis(SequencingState state, AxisFeedbackObserved e)
    {
        const string kind = SequencingCodes.KindAxisFeedbackObserved;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        // Axis feedback has an effect only while awaiting Standby. Anywhere else it is recorded only.
        // This is what prevents any axis confirmation from releasing a Job that has not been through SR3.
        if (job.Lifecycle != JobLifecycle.SAFE_RETURN_VERIFY_STANDBY)
        {
            return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.AxisFeedbackNoEffect, job, job.Lifecycle, job.Lifecycle, axis: e.Feedback));
        }

        switch (e.Feedback)
        {
            case AxisFeedbackState.AT_STANDBY:
                return ConfirmStandbyAndRelease(state, job, e);

            case AxisFeedbackState.FAULT:
            {
                var trail = new EvidenceTrail(state.EvidenceSeq);
                var failed = FailSafeReturn(trail, job, kind, e.At, SequencingCodes.AxisFault, null, AxisFeedbackState.FAULT);
                return trail.Finish(state with { ActiveJob = failed }, SequencingOutcome.APPLIED);
            }

            default:
                return Single(state, state, SequencingOutcome.NO_OP, JobRecord(kind, e.At, SequencingOutcome.NO_OP, SequencingCodes.StandbyNotConfirmed, job, JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, axis: e.Feedback));
        }
    }

    /// <summary>
    /// SR5 (Standby confirmed), SR6 (pending outcome recorded) and SR7 (Job
    /// released) in one transition. The outcome is recorded only here, after
    /// SR3 and SR5. Release is the kernel's own step and needs no external input.
    /// </summary>
    private static SequencingTransition ConfirmStandbyAndRelease(SequencingState state, SequencingActiveJob job, AxisFeedbackObserved e)
    {
        const string kind = SequencingCodes.KindAxisFeedbackObserved;
        const JobLifecycle awaiting = JobLifecycle.SAFE_RETURN_VERIFY_STANDBY;
        var trail = new EvidenceTrail(state.EvidenceSeq);

        trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR5), job, awaiting, awaiting, step: SafeReturnStep.SR5, axis: AxisFeedbackState.AT_STANDBY));
        trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR6), job, awaiting, awaiting, step: SafeReturnStep.SR6, jobOutcome: job.PendingOutcome));
        trail.Add(JobRecord(kind, e.At, SequencingOutcome.APPLIED, SequencingCodes.JobReleased, job, awaiting, null, step: SafeReturnStep.SR7));

        var released = state with
        {
            ActiveJob = null,
            Mode = state.Mode == AutoSequenceMode.PAUSE_REQUESTED ? AutoSequenceMode.PAUSED : state.Mode,
        };
        return trail.Finish(released, SequencingOutcome.APPLIED);
    }

    private static SequencingTransition ExpireFeedback(SequencingState state, FeedbackTimeoutExpired e)
    {
        const string kind = SequencingCodes.KindFeedbackTimeoutExpired;

        if (state.ActiveJob is not { } job)
        {
            return Commit(state, state, SequencingOutcome.REFUSED, kind, e.At, SequencingCodes.NoActiveJob, null, null);
        }

        var trail = new EvidenceTrail(state.EvidenceSeq);
        if (e.Target == FeedbackTarget.VALVE_CLOSED && job.Lifecycle == JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED)
        {
            var failedValve = FailSafeReturn(trail, job, kind, e.At, SequencingCodes.ValveCloseNotConfirmed, null, null);
            return trail.Finish(state with { ActiveJob = failedValve }, SequencingOutcome.APPLIED);
        }

        if (e.Target == FeedbackTarget.AXIS_STANDBY && job.Lifecycle == JobLifecycle.SAFE_RETURN_VERIFY_STANDBY)
        {
            var failedAxis = FailSafeReturn(trail, job, kind, e.At, SequencingCodes.AxisStandbyNotConfirmedFailure, null, null);
            return trail.Finish(state with { ActiveJob = failedAxis }, SequencingOutcome.APPLIED);
        }

        return Single(state, state, SequencingOutcome.REFUSED, JobRecord(kind, e.At, SequencingOutcome.REFUSED, SequencingCodes.FeedbackTimeoutNotPending, job, job.Lifecycle, job.Lifecycle));
    }

    /// <summary>
    /// Records SR1 (cleaning and synthetic water off) and SR2 (paired valve close
    /// requested) as two ordered evidence records, with no delay. Returns the Job
    /// now waiting for the valve close.
    /// </summary>
    private static SequencingActiveJob StartSafeReturn(
        EvidenceTrail trail,
        SequencingActiveJob job,
        string kind,
        DateTimeOffset at,
        string trigger,
        CleaningJobOutcome pendingOutcome,
        string? triggerReason,
        JobLifecycle lifecycleBefore)
    {
        var water = trail.Add(JobRecord(kind, at, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR1), job, lifecycleBefore, JobLifecycle.SAFE_RETURN_CLOSE_VALVE, step: SafeReturnStep.SR1, intent: SequencingCodes.IntentWaterOutputOff));
        var close = trail.Add(JobRecord(kind, at, SequencingOutcome.APPLIED, StepCode(SafeReturnStep.SR2), job, JobLifecycle.SAFE_RETURN_CLOSE_VALVE, JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, step: SafeReturnStep.SR2, intent: SequencingCodes.IntentValveClose));

        return job with
        {
            Lifecycle = JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED,
            CleaningActive = false,
            WaterOutputOn = false,
            PendingOutcome = pendingOutcome,
            Trigger = trigger,
            TriggerReason = triggerReason,
            Step = SafeReturnStep.SR2,
            Ledger = new SafeReturnLedger(water, close, null, null, null, null),
        };
    }

    /// <summary>
    /// Safe Return cannot be verified: records SR_FAILED with RECOVERY_REQUIRED
    /// evidence. The Job is retained, not released, and no outcome is finalised.
    /// </summary>
    private static SequencingActiveJob FailSafeReturn(
        EvidenceTrail trail,
        SequencingActiveJob job,
        string kind,
        DateTimeOffset at,
        string failureCode,
        ValveFeedbackState? valve,
        AxisFeedbackState? axis)
    {
        var failed = trail.Add(JobRecord(kind, at, SequencingOutcome.APPLIED, failureCode, job, job.Lifecycle, JobLifecycle.SAFE_RETURN_FAILED, step: SafeReturnStep.SR_FAILED, valve: valve, axis: axis, jobOutcome: CleaningJobOutcome.RECOVERY_REQUIRED));
        return job with
        {
            Lifecycle = JobLifecycle.SAFE_RETURN_FAILED,
            Step = SafeReturnStep.SR_FAILED,
            FailureCode = failureCode,
            Ledger = job.Ledger with { FailureSeq = failed },
        };
    }

    private static JobPhase? NextPhase(JobPhase? current) => current switch
    {
        null => JobPhase.P1,
        JobPhase.P1 => JobPhase.P2,
        JobPhase.P2 => JobPhase.P3,
        JobPhase.P3 => JobPhase.P4,
        JobPhase.P4 => JobPhase.P5,
        JobPhase.P5 => JobPhase.P6,
        _ => (JobPhase?)null,
    };

    private static string StepCode(SafeReturnStep step) => step.ToString();

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
        ObservePumpState e => (SequencingCodes.KindObservePumpState, e.At),
        RequestPause e => (SequencingCodes.KindRequestPause, e.At),
        AdvanceJobPreparation e => (SequencingCodes.KindAdvanceJobPreparation, e.At),
        BeginCleaning e => (SequencingCodes.KindBeginCleaning, e.At),
        ExecutionPhaseVerified e => (SequencingCodes.KindExecutionPhaseVerified, e.At),
        RequestNormalCompletion e => (SequencingCodes.KindRequestNormalCompletion, e.At),
        RequestAbort e => (SequencingCodes.KindRequestAbort, e.At),
        ReportExecutionFailure e => (SequencingCodes.KindReportExecutionFailure, e.At),
        ValveLimitObserved e => (SequencingCodes.KindValveLimitObserved, e.At),
        AxisFeedbackObserved e => (SequencingCodes.KindAxisFeedbackObserved, e.At),
        FeedbackTimeoutExpired e => (SequencingCodes.KindFeedbackTimeoutExpired, e.At),
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
        string? jobId) =>
        Single(state, next, outcome, Record(eventKind, at, outcome, code, entryId, jobId));

    private static SequencingTransition Single(SequencingState state, SequencingState next, SequencingOutcome outcome, SequencingEvidence draft)
    {
        var trail = new EvidenceTrail(state.EvidenceSeq);
        trail.Add(draft);
        return trail.Finish(next, outcome);
    }

    private static SequencingEvidence Record(
        string eventKind,
        DateTimeOffset at,
        SequencingOutcome outcome,
        string code,
        string? entryId = null,
        string? jobId = null) =>
        new(
            Seq: 0,
            At: UtcTimestamps.Format(at),
            EventKind: eventKind,
            Outcome: outcome,
            Code: code,
            EntryId: entryId,
            JobId: jobId,
            QueueRevision: 0);

    private static SequencingEvidence JobRecord(
        string eventKind,
        DateTimeOffset at,
        SequencingOutcome outcome,
        string code,
        SequencingActiveJob job,
        JobLifecycle? before,
        JobLifecycle? after,
        SafeReturnStep? step = null,
        string? intent = null,
        ValveFeedbackState? valve = null,
        AxisFeedbackState? axis = null,
        CleaningJobOutcome? jobOutcome = null,
        JobPhase? phase = null) =>
        Record(eventKind, at, outcome, code, null, job.JobId) with
        {
            JetId = job.JetId,
            ValveId = job.ValveId,
            LifecycleBefore = before,
            LifecycleAfter = after,
            Phase = phase,
            Step = step,
            ValveFeedback = valve,
            AxisFeedback = axis,
            JobOutcome = jobOutcome,
            Intent = intent,
        };

    /// <summary>
    /// Allocates evidence sequence numbers for one transition and assembles the
    /// result. Every record of one transition uses this single trail, so the
    /// sequence stays strictly increasing across Safe Return steps.
    /// </summary>
    private sealed class EvidenceTrail
    {
        private readonly List<SequencingEvidence> _records = new();
        private int _last;

        public EvidenceTrail(int evidenceSeq) => _last = evidenceSeq;

        /// <summary>Appends one record and returns its evidence sequence number.</summary>
        public int Add(SequencingEvidence draft)
        {
            var seq = NextCounter(_last, "EvidenceSeq");
            _last = seq;
            _records.Add(draft with { Seq = seq });
            return seq;
        }

        /// <summary>Stamps the state with the last sequence and the queue revision, and returns the transition.</summary>
        public SequencingTransition Finish(SequencingState next, SequencingOutcome outcome)
        {
            var committed = next with { EvidenceSeq = _last };
            var records = new SequencingEvidence[_records.Count];
            for (var index = 0; index < records.Length; index++)
            {
                records[index] = _records[index] with { QueueRevision = committed.QueueRevision };
            }

            return new SequencingTransition(committed, outcome, records[^1], records);
        }
    }
}
