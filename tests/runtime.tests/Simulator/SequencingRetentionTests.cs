using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b retention tests: the five bounded scopes (evidence log 256, current-Job Safe Return tail 16, last
/// dispatch, last Job outcome, first critical), their reset and archive rules, and the latch. NOT EXECUTED IN ARENA:
/// Owner-local validation required.
/// </summary>
public sealed class SequencingRetentionTests
{
    [Fact]
    public void Evidence_Log_Keeps_The_Newest_256_Records_Oldest_First_And_Counts_The_Dropped()
    {
        var records = Records(1, SequencingRetention.EvidenceLogCapacity + 44);
        var retention = Retain(SequencingRetention.Empty, records);

        Assert.Equal(SequencingRetention.EvidenceLogCapacity, retention.EvidenceLog.Count);
        Assert.Equal(44, retention.EvidenceDropped);
        Assert.Equal(45, retention.EvidenceLog[0].Seq);
        Assert.Equal(300, retention.EvidenceLog[^1].Seq);
    }

    [Fact]
    public void Evidence_Log_Accumulates_Across_Calls_And_Keeps_The_Same_Bound()
    {
        var first = Retain(SequencingRetention.Empty, Records(1, 200));
        var second = Retain(first, Records(201, 100));

        Assert.Equal(SequencingRetention.EvidenceLogCapacity, second.EvidenceLog.Count);
        Assert.Equal(44, second.EvidenceDropped);
        Assert.Equal(45, second.EvidenceLog[0].Seq);
        Assert.Equal(300, second.EvidenceLog[^1].Seq);
    }

    [Fact]
    public void Evidence_Log_Below_Capacity_Drops_Nothing()
    {
        var retention = Retain(SequencingRetention.Empty, Records(1, 10));

        Assert.Equal(10, retention.EvidenceLog.Count);
        Assert.Equal(0, retention.EvidenceDropped);
    }

    [Fact]
    public void Current_Job_Safe_Return_Tail_Keeps_The_Newest_16_Records_And_Counts_The_Dropped()
    {
        var records = Enumerable.Range(1, 20)
            .Select(seq => Record(seq, "SR_TEST", SafeReturnStep.SR2))
            .ToArray();
        var retention = Retain(SequencingRetention.Empty, records);

        Assert.Equal(SequencingRetention.SafeReturnTailCapacity, retention.CurrentSafeReturnTail.Count);
        Assert.Equal(4, retention.CurrentTailDropped);
        Assert.Equal(5, retention.CurrentSafeReturnTail[0].Seq);
        Assert.Equal(20, retention.CurrentSafeReturnTail[^1].Seq);
    }

    [Fact]
    public void Last_Dispatch_Carries_Its_Identity_Revisions_Position_Origin_And_Instant()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var dispatch = SimulatorRunHarness.Required(run.RetentionAt(3).LastDispatch);
        var head = run.StateAt(2).Queue[0];
        var job = SimulatorRunHarness.Required(run.StateAt(3).ActiveJob);

        Assert.StartsWith(SequencingCodes.DispatchIdPrefix, dispatch.DispatchId, StringComparison.Ordinal);
        Assert.Equal(job.DispatchId, dispatch.DispatchId);
        Assert.Equal(2, dispatch.QueueRevisionBefore);
        Assert.Equal(3, dispatch.QueueRevisionAfter);
        Assert.Equal(head.EntryId, dispatch.QueueEntryId);
        Assert.Equal(1, dispatch.PositionBefore);
        Assert.Equal(set.Sensors[0].SensorId, dispatch.SensorId);
        Assert.Equal(SimulatorScenarioCatalogue.AdmissionReason, dispatch.SourceReason);
        Assert.Equal(job.JobId, dispatch.JobId);
        Assert.Equal(SequencingAdmissionSource.SCENARIO_PREPARED.ToString(), dispatch.Origin);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(3)), dispatch.DispatchedAt);
    }

    [Fact]
    public void Last_Dispatch_Is_Kept_Until_The_Next_Dispatch_And_Then_Replaced()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new RequestAbort(SimulatorRunHarness.At(7))));
        steps.Add(SimulatorRunHarness.ValveClosed(set, 8));
        steps.Add(SimulatorRunHarness.Step(9, new AxisFeedbackObserved(SimulatorRunHarness.At(9), AxisFeedbackState.AT_STANDBY)));
        steps.Add(SimulatorRunHarness.Dispatch(set, 10));
        var run = SimulatorRunHarness.Run(steps);

        var firstDispatch = SimulatorRunHarness.Required(run.RetentionAt(6).LastDispatch);
        var afterRelease = SimulatorRunHarness.Required(run.RetentionAt(9).LastDispatch);
        var secondDispatch = SimulatorRunHarness.Required(run.RetentionAt(10).LastDispatch);

        Assert.Equal(set.Sensors[0].SensorId, firstDispatch.SensorId);
        Assert.Equal(firstDispatch.DispatchId, afterRelease.DispatchId);
        Assert.Equal(set.Sensors[1].SensorId, secondDispatch.SensorId);
        Assert.NotEqual(firstDispatch.DispatchId, secondDispatch.DispatchId);
    }

    [Fact]
    public void Current_Job_Tail_Resets_At_The_Next_Dispatch_And_The_Last_Outcome_Is_Kept()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new RequestAbort(SimulatorRunHarness.At(7))));
        steps.Add(SimulatorRunHarness.ValveClosed(set, 8));
        steps.Add(SimulatorRunHarness.Step(9, new AxisFeedbackObserved(SimulatorRunHarness.At(9), AxisFeedbackState.AT_STANDBY)));
        steps.Add(SimulatorRunHarness.Dispatch(set, 10));
        var run = SimulatorRunHarness.Run(steps);

        var duringJob = run.RetentionAt(7);
        var afterRelease = run.RetentionAt(9);
        var afterDispatch = run.RetentionAt(10);

        Assert.NotEmpty(duringJob.CurrentSafeReturnTail);
        Assert.Empty(afterRelease.CurrentSafeReturnTail);
        Assert.Equal(0, afterRelease.CurrentTailDropped);
        Assert.Empty(afterDispatch.CurrentSafeReturnTail);
        Assert.Equal(0, afterDispatch.CurrentTailDropped);
        Assert.Equal(
            SimulatorRunHarness.Json(afterRelease.LastJobOutcome),
            SimulatorRunHarness.Json(afterDispatch.LastJobOutcome));
        Assert.NotNull(afterDispatch.LastJobOutcome);
    }

    [Fact]
    public void Last_Job_Outcome_Is_Captured_At_The_Release_With_The_Safe_Return_Sequences_And_Event_Tail()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var outcome = SimulatorRunHarness.Required(run.FinalRetention.LastJobOutcome);
        var release = run.TransitionAt(16).Evidence;

        Assert.Equal(SequencingCodes.TriggerNormalCompletion, outcome.Trigger);
        Assert.Equal(CleaningJobOutcome.COMPLETED.ToString(), outcome.Outcome);
        Assert.Equal(JobPhase.P6, outcome.PhaseAtTrigger);
        Assert.True(outcome.CleaningPhasesComplete);
        Assert.Equal(release.Seq, outcome.ReleaseSeq);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(16)), outcome.FinalizedAt);
        Assert.Equal(AutoSequenceState.READY_TO_DISPATCH, outcome.AutoSequenceAtRelease);
        Assert.True(outcome.ValveCloseCommandSeq < outcome.ValveClosedConfirmedSeq);
        Assert.True(outcome.ValveClosedConfirmedSeq < outcome.AxisReturnCommandSeq);
        Assert.True(outcome.AxisReturnCommandSeq < outcome.StandbyConfirmedSeq);
        Assert.True(outcome.StandbyConfirmedSeq < outcome.OutcomeSeq);
        Assert.True(outcome.OutcomeSeq < outcome.ReleaseSeq);
        Assert.Equal(
            new[] { "SR1", "SR2", "SR3", "SR4", "SR5", "SR6", SequencingCodes.JobReleased },
            outcome.Events.Select(record => record.Event).ToArray());
    }

    [Fact]
    public void Last_Job_Outcome_Of_An_Abort_Records_The_Abort_Trigger_And_The_Default_Phase()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.EXPLICIT_ABORT);
        var outcome = SimulatorRunHarness.Required(run.FinalRetention.LastJobOutcome);

        Assert.Equal(SequencingCodes.TriggerAbort, outcome.Trigger);
        Assert.Equal(CleaningJobOutcome.ABORTED.ToString(), outcome.Outcome);
        Assert.Equal(JobPhase.P1, outcome.PhaseAtTrigger);
        Assert.False(outcome.CleaningPhasesComplete);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(9)), outcome.FinalizedAt);
    }

    [Fact]
    public void Last_Job_Outcome_Stays_Absent_While_A_Failed_Job_Is_Retained()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_FAILURE);

        Assert.Null(run.FinalRetention.LastJobOutcome);
        Assert.NotEmpty(run.FinalRetention.CurrentSafeReturnTail);
    }

    [Fact]
    public void Current_Phase_Start_Follows_The_Dispatch_Then_Each_Verification()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);

        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(3)), run.RetentionAt(3).CurrentPhaseStartedAt);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(7)), run.RetentionAt(7).CurrentPhaseStartedAt);
        Assert.Equal(UtcTimestamps.Format(SimulatorRunHarness.At(9)), run.RetentionAt(9).CurrentPhaseStartedAt);
        Assert.Null(run.RetentionAt(16).CurrentPhaseStartedAt);
    }

    [Fact]
    public void Valve_Feedback_Retention_Tracks_The_Last_Applied_Valve_Observation()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var closed = SimulatorRunHarness.Required(run.RetentionAt(4).CurrentValveFeedback);
        var open = SimulatorRunHarness.Required(run.RetentionAt(8).CurrentValveFeedback);

        Assert.Equal(run.TransitionAt(4).Evidence.Seq, closed.Seq);
        Assert.Equal(ValveFeedbackState.CLOSED, closed.ValveFeedback);
        Assert.Equal(run.TransitionAt(8).Evidence.Seq, open.Seq);
        Assert.Equal(ValveFeedbackState.OPEN, open.ValveFeedback);
    }

    [Fact]
    public void First_Critical_Is_Captured_On_The_Latch_Transition_With_The_Job_Context()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_TRIP);
        var first = SimulatorRunHarness.Required(run.FinalRetention.FirstCritical);
        var latch = run.TransitionAt(7).Records.Single(record => record.Pump is not null);
        var job = SimulatorRunHarness.Required(run.StateAt(6).ActiveJob);

        Assert.Equal(PumpObservation.TRIP, first.Observation);
        Assert.Equal(latch.Seq, first.Evidence.Seq);
        Assert.Equal(SequencingCodes.CriticalSuspensionRaised, first.Evidence.Code);
        Assert.Equal(job.JobId, first.JobId);
        Assert.Equal(set.Sensors[0].SensorId, first.TargetSensorId);
        Assert.Null(first.PhaseAtEvent);
        Assert.True(first.SafeReturnRequired);
    }

    [Fact]
    public void First_Critical_Is_First_Wins_And_Later_Observations_Are_Evidence_Only()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new ObservePumpState(SimulatorRunHarness.At(7), PumpObservation.UNEXPECTED_STOP)));
        steps.Add(SimulatorRunHarness.Step(8, new ObservePumpState(SimulatorRunHarness.At(8), PumpObservation.TRIP)));
        steps.Add(SimulatorRunHarness.ValveClosed(set, 9));
        steps.Add(SimulatorRunHarness.Step(10, new AxisFeedbackObserved(SimulatorRunHarness.At(10), AxisFeedbackState.AT_STANDBY)));
        var run = SimulatorRunHarness.Run(steps);

        var firstRecord = run.TransitionAt(7).Records.Single(record => record.Pump is not null);
        var laterRecord = run.TransitionAt(8).Records.Single(record => record.Pump is not null);
        var first = SimulatorRunHarness.Required(run.FinalRetention.FirstCritical);

        Assert.Equal(SequencingOutcome.NO_OP, run.TransitionAt(8).Outcome);
        Assert.Equal(PumpObservation.UNEXPECTED_STOP, first.Observation);
        Assert.Equal(firstRecord.Seq, first.Evidence.Seq);
        Assert.Contains(run.FinalRetention.EvidenceLog, record => record.Seq == laterRecord.Seq && record.Pump == PumpObservation.TRIP);
    }

    [Fact]
    public void First_Critical_Captured_During_Safe_Return_Carries_The_Job_Context()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.Step(7, new RequestAbort(SimulatorRunHarness.At(7))));
        steps.Add(SimulatorRunHarness.Step(8, new ObservePumpState(SimulatorRunHarness.At(8), PumpObservation.TRIP)));
        var run = SimulatorRunHarness.Run(steps);

        var first = SimulatorRunHarness.Required(run.FinalRetention.FirstCritical);
        var job = SimulatorRunHarness.Required(run.StateAt(6).ActiveJob);

        Assert.Equal(job.JobId, first.JobId);
        Assert.Equal(job.TargetSensorId, first.TargetSensorId);
        Assert.True(first.SafeReturnRequired);
        Assert.Equal(PumpObservation.TRIP, first.Observation);
    }

    [Fact]
    public void First_Critical_Without_A_Job_Carries_No_Job_Context()
    {
        var steps = new List<SimulatorScenarioStep>
        {
            SimulatorRunHarness.Step(0, new StartAutoSequence(SimulatorRunHarness.At(0))),
            SimulatorRunHarness.Step(1, new ObservePumpState(SimulatorRunHarness.At(1), PumpObservation.UNEXPECTED_STOP)),
        };
        var run = SimulatorRunHarness.Run(steps);
        var first = SimulatorRunHarness.Required(run.FinalRetention.FirstCritical);

        Assert.Equal(PumpObservation.UNEXPECTED_STOP, first.Observation);
        Assert.Null(first.JobId);
        Assert.Null(first.TargetSensorId);
        Assert.Null(first.PhaseAtEvent);
        Assert.False(first.SafeReturnRequired);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.PUMP_UNEXPECTED_STOP)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP)]
    public void Latch_Is_Never_Cleared_By_Retention(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);

        Assert.True(run.FinalState.CriticalSuspended);
        Assert.NotNull(run.FinalRetention.FirstCritical);
    }

    [Fact]
    public void Retention_Is_A_Pure_Value_That_Does_Not_Change_Its_Input()
    {
        var before = Retain(SequencingRetention.Empty, Records(1, 5));
        var next = Retain(before, Records(6, 5));

        Assert.Equal(5, before.EvidenceLog.Count);
        Assert.Equal(10, next.EvidenceLog.Count);
    }

    private static SequencingEvidence[] Records(int firstSeq, int count) =>
        Enumerable.Range(firstSeq, count).Select(seq => Record(seq, "TEST_RECORD")).ToArray();

    private static SequencingEvidence Record(int seq, string code, SafeReturnStep? step = null) =>
        new(
            seq,
            UtcTimestamps.Format(SimulatorRunHarness.At(0)),
            "TEST",
            SequencingOutcome.APPLIED,
            code,
            null,
            null,
            0,
            Step: step);

    private static SequencingRetention Retain(SequencingRetention previous, IReadOnlyList<SequencingEvidence> records)
    {
        var before = SequencingKernel.Initial();
        var transition = new SequencingTransition(before, SequencingOutcome.APPLIED, records[^1], records);
        return SequencingRetention.Retain(previous, before, transition);
    }
}
