using System.Globalization;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b projection tests: the wire presentation of the kernel and retention, the canonical identity proof,
/// Safe Return and critical projection, and the carried sections. NOT EXECUTED IN ARENA: Owner-local validation required.
/// </summary>
public sealed class SequencingRuntimeProjectionTests
{
    private static readonly string[] ExpectedSr1Sr2Events =
        ["SR1", "SR2"];

    private static readonly string[] ExpectedSr1ThroughSr4Events =
        ["SR1", "SR2", "SR3", "SR4"];

    public static IEnumerable<object[]> AllScenarioIds() =>
        Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_Applied_Step_Projects_A_Valid_Revision_At_The_Scheduled_Instant(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var revision = run.Initial.Revision;

        for (var index = 0; index < run.Candidates.Count; index++)
        {
            revision++;
            var candidate = run.Candidates[index];

            Assert.Equal(revision, candidate.Revision);
            Assert.Equal(SimulatorScenarioCatalogue.AtTick(run.CandidateTicks[index]), candidate.GeneratedAtUtc);
            Assert.True(RuntimeStateInvariants.TryValidate(candidate, out var code, out var detail), $"[{code}] {detail}");
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Candidate_Count_Equals_The_Applied_Transition_Count_So_Refusals_Project_Nothing(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var applied = run.Transitions.Count(transition => transition.Outcome == SequencingOutcome.APPLIED);

        Assert.Equal(applied, run.Candidates.Count);
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Projection_Is_Byte_Identical_For_Identical_Input(SimulatorScenarioId id)
    {
        var first = SimulatorRunHarness.RunScenario(id);
        var second = SimulatorRunHarness.RunScenario(id);

        Assert.Equal(first.Candidates.Count, second.Candidates.Count);
        for (var index = 0; index < first.Candidates.Count; index++)
        {
            Assert.Equal(SimulatorRunHarness.Sections(first.Candidates[index]), SimulatorRunHarness.Sections(second.Candidates[index]));
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Queue_Summary_Is_The_Kernel_Queue_In_FIFO_Order_With_Capacity_Eight(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        if (id == SimulatorScenarioId.IDLE)
        {
            Assert.Empty(run.Candidates);
            return;
        }

        var tick = run.CandidateTicks[^1];
        var queue = SimulatorRunHarness.Required(run.CandidateAt(tick).Queue);
        var kernel = run.StateAt(tick).Queue;

        Assert.Equal("GlobalQueue", queue.Label);
        Assert.Equal(QueueSummary.MaxEntries, queue.Capacity);
        Assert.Equal(kernel.Count, queue.TotalQueued);
        Assert.Equal(kernel.Count, queue.Entries.Count);
        Assert.Equal(run.StateAt(tick).QueueRevision, queue.Revision);
        for (var index = 0; index < kernel.Count; index++)
        {
            Assert.Equal(index + 1, queue.Entries[index].Position);
            Assert.Equal(kernel[index].EntryId, queue.Entries[index].EntryId);
            Assert.Equal(kernel[index].SensorId, queue.Entries[index].SensorId);
            Assert.Equal(kernel[index].SourceReason, queue.Entries[index].SourceReason);
            Assert.Equal(kernel[index].SecondsSinceLastClean, queue.Entries[index].SecondsSinceLastClean);
        }
    }

    [Fact]
    public void Queue_Carries_The_Retained_Last_Dispatch_Until_The_Next_Dispatch()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var expected = SimulatorRunHarness.Json(run.RetentionAt(3).LastDispatch);

        Assert.Equal(expected, SimulatorRunHarness.Json(run.CandidateAt(3).Queue.LastDispatch));
        Assert.Equal(expected, SimulatorRunHarness.Json(run.CandidateAt(16).Queue.LastDispatch));
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Sensor_Queue_State_Matches_The_Kernel_And_Keeps_The_Canonical_Identity(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        if (id == SimulatorScenarioId.IDLE)
        {
            Assert.Empty(run.Candidates);
            return;
        }

        var tick = run.CandidateTicks[^1];
        var state = run.StateAt(tick);
        var candidate = run.CandidateAt(tick);
        var canonical = SimulatorRunHarness.CanonicalSensors();

        Assert.Equal(canonical.Count, candidate.Sensors.Count);
        foreach (var record in canonical)
        {
            var projected = candidate.Sensors.Single(sensor => string.Equals(sensor.SensorId, record.SensorId, StringComparison.Ordinal));
            var expected = ExpectedQueueState(state, record.SensorId);

            Assert.Equal(record.ScanOrder, projected.ScanOrder);
            Assert.Equal(record.AssignedWaterJetId, projected.AssignedWaterJetId);
            Assert.Equal(record.AssignedIsolationValveId, projected.AssignedIsolationValveId);
            Assert.Equal(expected, projected.QueueState);
            Assert.Equal(expected == QueueState.ACTIVE, projected.IsActiveJobTarget);
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Active_Job_Projects_Exactly_One_Runtime_Sensor_As_Its_Target(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);

        foreach (var candidate in run.Candidates)
        {
            var targets = candidate.Sensors.Where(sensor => sensor.IsActiveJobTarget).ToArray();
            var job = candidate.ActiveJob;
            if (job is null)
            {
                Assert.Empty(targets);
                continue;
            }

            var target = Assert.Single(targets);
            Assert.Equal(job.TargetSensorId, target.SensorId);
            Assert.Equal(QueueState.ACTIVE, target.QueueState);
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void No_Projected_Identity_Is_A_SYN_S_Placeholder_And_Every_Identity_Is_Canonical(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var canonicalIds = SimulatorRunHarness.CanonicalSensors()
            .Select(sensor => sensor.SensorId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in run.Candidates)
        {
            foreach (var sensor in candidate.Sensors)
            {
                Assert.Contains(sensor.SensorId, canonicalIds);
                Assert.False(sensor.SensorId.StartsWith("SYN-S", StringComparison.Ordinal));
            }

            foreach (var entry in candidate.Queue.Entries)
            {
                Assert.Contains(entry.SensorId, canonicalIds);
            }

            var job = candidate.ActiveJob;
            if (job is not null)
            {
                Assert.Contains(job.TargetSensorId, canonicalIds);
                Assert.Contains(job.Dispatch.SensorId, canonicalIds);
            }

            var dispatch = candidate.Queue.LastDispatch;
            if (dispatch is not null)
            {
                Assert.Contains(dispatch.SensorId, canonicalIds);
            }

            if (candidate.Sequence.Critical?.TargetSensorId is { } target)
            {
                Assert.Contains(target, canonicalIds);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Pump_And_Sequence_Controls_Are_Carried_From_The_Previous_Revision(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var pump = SimulatorRunHarness.Json(run.Initial.Pump);
        var controls = SimulatorRunHarness.Json(run.Initial.Sequence.Controls);

        foreach (var candidate in run.Candidates)
        {
            Assert.Equal(pump, SimulatorRunHarness.Json(candidate.Pump));
            Assert.Equal(controls, SimulatorRunHarness.Json(candidate.Sequence.Controls));
        }
    }

    [Fact]
    public void Preparing_Job_Presents_P1_Pending_Preparing_With_Zero_Progress_And_Deterministic_Instants()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var candidate = run.CandidateAt(3);
        var job = SimulatorRunHarness.Required(candidate.ActiveJob);
        var started = UtcTimestamps.Format(SimulatorRunHarness.At(3));

        Assert.Equal(JobPhase.P1, job.Phase);
        Assert.Equal(0, job.PhaseIndex);
        Assert.Equal(0.0, job.PhaseProgress);
        Assert.Equal("P1 PENDING - PREPARING", job.PhaseLabel);
        Assert.Equal(SequencingRuntimeProjection.CleaningPhaseInProgress, job.CleaningPhase);
        Assert.Equal(JobLifecycle.RUNNING, job.Lifecycle);
        Assert.Null(job.SafeReturn);
        Assert.Equal(started, job.StartedAt);
        Assert.Equal(started, job.PhaseStartedAt);
        Assert.Equal(started, job.Dispatch.DispatchedAt);
        Assert.Equal(AutoSequenceState.JOB_ACTIVE, candidate.Sequence.AutoSequence);
    }

    [Fact]
    public void Pump_Wait_Presents_Pump_Not_Ready_Until_The_Readiness_Is_Observed()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_WAIT_THEN_READY);
        var waiting = run.CandidateAt(3);
        var job = SimulatorRunHarness.Required(waiting.ActiveJob);

        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, waiting.Sequence.AutoSequence);
        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, waiting.Queue.AutoSequence);
        Assert.Equal("P1 PENDING - PREPARING", job.PhaseLabel);
        Assert.Equal(AutoSequenceState.JOB_ACTIVE, run.CandidateAt(4).Sequence.AutoSequence);
    }

    [Fact]
    public void Ready_To_Clean_Job_Presents_P1_Pending_Ready_To_Clean()
    {
        var job = SimulatorRunHarness.Required(
            SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION).CandidateAt(5).ActiveJob);

        Assert.Equal(JobPhase.P1, job.Phase);
        Assert.Equal(0, job.PhaseIndex);
        Assert.Equal(0.0, job.PhaseProgress);
        Assert.Equal("P1 PENDING - READY_TO_CLEAN", job.PhaseLabel);
    }

    [Fact]
    public void Cleaning_Before_The_First_Verified_Phase_Presents_P1_Pending_Cleaning()
    {
        var job = SimulatorRunHarness.Required(
            SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION).CandidateAt(6).ActiveJob);

        Assert.Equal(JobPhase.P1, job.Phase);
        Assert.Equal(0, job.PhaseIndex);
        Assert.Equal(0.0, job.PhaseProgress);
        Assert.Equal("P1 PENDING - CLEANING", job.PhaseLabel);
    }

    [Theory]
    [InlineData(7, JobPhase.P1, 1)]
    [InlineData(9, JobPhase.P2, 2)]
    [InlineData(10, JobPhase.P3, 3)]
    [InlineData(11, JobPhase.P4, 4)]
    [InlineData(12, JobPhase.P5, 5)]
    [InlineData(13, JobPhase.P6, 6)]
    public void Verified_Phase_Presents_Its_Index_Progress_And_Verification_Instant(int tick, JobPhase phase, int index)
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var job = SimulatorRunHarness.Required(run.CandidateAt(tick).ActiveJob);

        Assert.Equal(phase, job.Phase);
        Assert.Equal(index, job.PhaseIndex);
        Assert.Equal(index / 6.0, job.PhaseProgress);
        Assert.Equal($"{phase} - CLEANING", job.PhaseLabel);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(tick)), job.PhaseStartedAt);
    }

    [Fact]
    public void Abort_Safe_Return_Presents_The_Valve_And_Axis_Legs_From_The_Retained_Records()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.EXPLICIT_ABORT);
        var closedSeq = run.TransitionAt(4).Evidence.Seq;
        var job = SimulatorRunHarness.Required(run.CandidateAt(7).ActiveJob);
        var sr = SimulatorRunHarness.Required(job.SafeReturn);

        Assert.Equal(SequencingCodes.TriggerAbort, sr.Trigger);
        Assert.Equal(CleaningJobOutcome.ABORTED.ToString(), sr.PendingOutcome);
        Assert.Equal(JobPhase.P1, sr.PhaseAtTrigger);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(7)), sr.StartedAt);
        Assert.Equal(SafeReturnStep.SR2, sr.Step);
        Assert.Equal(job.ValveId, sr.Valve.ValveId);
        Assert.Equal("SR2", sr.Valve.Command);
        Assert.Equal(SimulatorRunHarness.RecordWithCode(run.TransitionAt(7), "SR2").Seq, sr.Valve.CommandSeq);
        Assert.Equal("CLOSED", sr.Valve.Feedback);
        Assert.Equal(closedSeq, sr.Valve.FeedbackSeq);
        Assert.Equal("SR4", sr.Axis.Command);
        Assert.Null(sr.Axis.CommandSeq);
        Assert.Equal("UNKNOWN", sr.Axis.Standby);
        Assert.Null(sr.Axis.StandbySeq);
        Assert.Null(sr.Failure);
        Assert.Equal(
            ExpectedSr1Sr2Events,
            sr.Events.Select(record => record.Event).ToArray());

        var later = SimulatorRunHarness.Required(
            SimulatorRunHarness.Required(run.CandidateAt(8).ActiveJob).SafeReturn);
        var valveObserved = SimulatorRunHarness.RecordWithCode(run.TransitionAt(8), SequencingCodes.ValveFeedbackObserved);

        Assert.Equal(SafeReturnStep.SR4, later.Step);
        Assert.Equal("CLOSED", later.Valve.Feedback);
        Assert.Equal(valveObserved.Seq, later.Valve.FeedbackSeq);
        Assert.Equal(SimulatorRunHarness.RecordWithCode(run.TransitionAt(8), "SR4").Seq, later.Axis.CommandSeq);
        Assert.Equal(
            ExpectedSr1ThroughSr4Events,
            later.Events.Select(record => record.Event).ToArray());

        Assert.Null(run.CandidateAt(9).ActiveJob);
        Assert.NotNull(run.CandidateAt(9).Sequence.LastJobOutcome);
    }

    [Fact]
    public void Valve_Close_Failure_Presents_The_Failure_Record_And_Keeps_The_Job()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_FAILURE);
        var candidate = run.CandidateAt(8);
        var job = SimulatorRunHarness.Required(candidate.ActiveJob);
        var sr = SimulatorRunHarness.Required(job.SafeReturn);
        var failure = SimulatorRunHarness.Required(sr.Failure);

        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SafeReturnStep.SR_FAILED, sr.Step);
        Assert.Equal(SequencingCodes.ValveCloseNotConfirmed, failure.Reason);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, failure.AtLifecycle);
        Assert.Equal(run.TransitionAt(8).Evidence.Seq, failure.Seq);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(8)), failure.At);
        Assert.Null(candidate.Sequence.Critical);
        Assert.Null(candidate.Sequence.LastJobOutcome);
    }

    [Fact]
    public void Axis_Standby_Failure_Presents_The_Axis_Fault_And_Keeps_The_Job()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.AXIS_STANDBY_FAILURE);
        var candidate = run.CandidateAt(9);
        var job = SimulatorRunHarness.Required(candidate.ActiveJob);
        var sr = SimulatorRunHarness.Required(job.SafeReturn);
        var failure = SimulatorRunHarness.Required(sr.Failure);

        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SafeReturnStep.SR_FAILED, sr.Step);
        Assert.Equal(SequencingCodes.AxisFault, failure.Reason);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, failure.AtLifecycle);
        Assert.Equal(run.TransitionAt(9).Evidence.Seq, failure.Seq);
        Assert.Equal("FAULT", sr.Axis.Standby);
        Assert.Null(sr.Axis.StandbySeq);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.PUMP_UNEXPECTED_STOP, CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP, CriticalPumpKind.MAIN_PUMP_TRIP)]
    public void Critical_Pump_Event_Projects_The_Latched_Condition_Without_Acknowledgement(
        SimulatorScenarioId id,
        CriticalPumpKind kind)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var latch = run.TransitionAt(7).Records.Single(record => record.Pump is not null);
        var candidate = run.CandidateAt(7);
        var critical = SimulatorRunHarness.Required(candidate.Sequence.Critical);
        var sequence = latch.Seq.ToString(CultureInfo.InvariantCulture);
        var job = SimulatorRunHarness.Required(run.StateAt(6).ActiveJob);

        Assert.Null(run.CandidateAt(6).Sequence.Critical);
        Assert.Equal(SequencingRuntimeProjection.CriticalEventIdPrefix + sequence, critical.EventId);
        Assert.Equal(SequencingRuntimeProjection.CriticalAlarmIdPrefix + sequence, critical.AlarmId);
        Assert.Equal(kind, critical.Kind);
        Assert.Equal(AlarmSeverity.HIGH, critical.Severity);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(7)), critical.RaisedAt);
        Assert.Equal(latch.Seq, critical.EvidenceSeq);
        Assert.True(critical.ConditionActive);
        Assert.Null(critical.ClearedAt);
        Assert.False(critical.Acknowledged);
        Assert.Null(critical.AcknowledgedAt);
        Assert.True(critical.ModalOpen);
        Assert.Null(critical.ModalClosedAt);
        Assert.Equal(job.JobId, critical.JobId);
        Assert.Equal(job.TargetSensorId, critical.TargetSensorId);
        Assert.Null(critical.PhaseAtEvent);
        Assert.True(critical.SafeReturnRequired);
        Assert.False(critical.SafeReturnComplete);
        Assert.False(critical.SafeReturnFailed);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, candidate.Sequence.AutoSequence);
        Assert.Equal(AutoSequenceMode.CRITICAL_SUSPENDED, candidate.Sequence.Mode);
    }

    [Fact]
    public void Latch_Persists_After_Release_And_The_Queue_Is_Kept()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_TRIP);
        var candidate = run.CandidateAt(9);
        var critical = SimulatorRunHarness.Required(candidate.Sequence.Critical);
        var released = SimulatorRunHarness.Required(candidate.Sequence.LastJobOutcome);

        Assert.Null(candidate.ActiveJob);
        Assert.True(critical.ConditionActive);
        Assert.False(critical.Acknowledged);
        Assert.True(critical.SafeReturnComplete);
        Assert.Equal(released.JobId, critical.JobId);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, candidate.Sequence.AutoSequence);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, candidate.Queue.AutoSequence);
        Assert.Equal(new[] { set.Sensors[1].SensorId }, candidate.Queue.Entries.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Critical_During_Safe_Return_Latches_With_The_Job_Context_Retained()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new RequestAbort(SimulatorRunHarness.At(7))));
        steps.Add(SimulatorRunHarness.Step(8, new ObservePumpState(SimulatorRunHarness.At(8), PumpObservation.TRIP)));
        var run = SimulatorRunHarness.Run(steps);

        var candidate = run.CandidateAt(8);
        var critical = SimulatorRunHarness.Required(candidate.Sequence.Critical);
        var job = SimulatorRunHarness.Required(run.StateAt(6).ActiveJob);
        var pump = run.TransitionAt(8).Records.Single(record => record.Pump is not null);

        Assert.Equal(CriticalPumpKind.MAIN_PUMP_TRIP, critical.Kind);
        Assert.Equal(pump.Seq, critical.EvidenceSeq);
        Assert.Equal(SequencingCodes.CriticalEventDuringSafeReturn, pump.Code);
        Assert.Equal(job.JobId, critical.JobId);
        Assert.Equal(job.TargetSensorId, critical.TargetSensorId);
        Assert.True(critical.SafeReturnRequired);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, candidate.Sequence.AutoSequence);
    }

    [Fact]
    public void Critical_Without_A_Job_Projects_No_Job_Context()
    {
        var steps = new List<SimulatorScenarioStep>
        {
            SimulatorRunHarness.Step(0, new StartAutoSequence(SimulatorRunHarness.At(0))),
            SimulatorRunHarness.Step(1, new ObservePumpState(SimulatorRunHarness.At(1), PumpObservation.TRIP)),
        };
        var run = SimulatorRunHarness.Run(steps);
        var candidate = run.CandidateAt(1);
        var critical = SimulatorRunHarness.Required(candidate.Sequence.Critical);

        Assert.Null(candidate.ActiveJob);
        Assert.Equal(CriticalPumpKind.MAIN_PUMP_TRIP, critical.Kind);
        Assert.Null(critical.JobId);
        Assert.Null(critical.TargetSensorId);
        Assert.Null(critical.PhaseAtEvent);
        Assert.False(critical.SafeReturnRequired);
        Assert.False(critical.SafeReturnComplete);
        Assert.False(critical.SafeReturnFailed);
    }

    [Fact]
    public void First_Critical_Is_Kept_Against_A_Later_Observation_In_The_Same_Safe_Return()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new ObservePumpState(SimulatorRunHarness.At(7), PumpObservation.UNEXPECTED_STOP)));
        steps.Add(SimulatorRunHarness.Step(8, new ObservePumpState(SimulatorRunHarness.At(8), PumpObservation.TRIP)));
        steps.Add(SimulatorRunHarness.ValveClosed(set, 9));
        steps.Add(SimulatorRunHarness.Step(10, new AxisFeedbackObserved(SimulatorRunHarness.At(10), AxisFeedbackState.AT_STANDBY)));
        var run = SimulatorRunHarness.Run(steps);

        var first = run.TransitionAt(7).Records.Single(record => record.Pump is not null);
        var critical = SimulatorRunHarness.Required(run.CandidateAt(9).Sequence.Critical);

        Assert.Equal(SequencingOutcome.NO_OP, run.TransitionAt(8).Outcome);
        Assert.Equal(CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP, critical.Kind);
        Assert.Equal(first.Seq, critical.EvidenceSeq);
        Assert.Contains(
            run.FinalRetention.EvidenceLog,
            record => record.Pump == PumpObservation.TRIP);
        Assert.Equal(PumpObservation.UNEXPECTED_STOP, SimulatorRunHarness.Required(run.FinalRetention.FirstCritical).Observation);
    }

    [Fact]
    public void Last_Job_Outcome_Is_Projected_Into_The_Sequence_Section_After_Release()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var candidate = run.CandidateAt(16);
        var outcome = SimulatorRunHarness.Required(candidate.Sequence.LastJobOutcome);

        Assert.Null(candidate.ActiveJob);
        Assert.Equal(SimulatorRunHarness.Json(run.FinalRetention.LastJobOutcome), SimulatorRunHarness.Json(outcome));
        Assert.Equal(CleaningJobOutcome.COMPLETED.ToString(), outcome.Outcome);
    }

    [Fact]
    public void Projection_Refuses_An_Instant_That_Is_Not_Later_Than_The_Previous_Revision()
    {
        var previous = SimulatorRunHarness.InitialState();
        var state = SequencingKernel.Initial();
        var transition = SequencingKernel.Apply(state, new StartAutoSequence(SimulatorRunHarness.At(0)));
        var retention = SequencingRetention.Retain(SequencingRetention.Empty, state, transition);

        Assert.Throws<InvalidOperationException>(() =>
            SequencingRuntimeProjection.ProjectRuntimeState(previous, transition.State, retention, previous.GeneratedAtUtc));
    }

    [Fact]
    public void Active_Job_Without_A_Retained_Dispatch_Is_Refused()
    {
        var state = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION).StateAt(3);

        Assert.NotNull(state.ActiveJob);
        Assert.Throws<InvalidOperationException>(() =>
            SequencingRuntimeProjection.ProjectActiveJob(state, SequencingRetention.Empty));
    }

    private static QueueState ExpectedQueueState(SequencingState state, string sensorId)
    {
        if (state.ActiveJob is { } job && string.Equals(job.TargetSensorId, sensorId, StringComparison.Ordinal))
        {
            return QueueState.ACTIVE;
        }

        foreach (var entry in state.Queue)
        {
            if (string.Equals(entry.SensorId, sensorId, StringComparison.Ordinal))
            {
                return QueueState.QUEUED;
            }
        }

        return QueueState.NONE;
    }
}
