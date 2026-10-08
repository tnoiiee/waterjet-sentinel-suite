using System.Reflection;
using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-2 behaviour tests for the Cleaning Job lifecycle, abstract Pump
/// and feedback inputs, and Mandatory Safe Return. They drive only the public
/// kernel (Apply and the projections), except for the state-validator tests, which
/// build invalid states through the internal constructor by reflection, as the
/// CP-1 integrity tests do. No timer, clock, random value, device, route or
/// adapter is used. Every identifier is synthetic (SYN-*).
/// </summary>
public sealed class SequencingJobSafeReturnTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] SensorIds =
        ["SYN-S01", "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08"];

    private static readonly string[] NonSensorIds = ["SYN-G01"];

    // The single internal constructor takes eight parameters (CP-1 seam, unchanged in CP-2).
    private static readonly ConstructorInfo RawConstructor = typeof(SequencingState)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(constructor => constructor.GetParameters().Length == 8);

    // ---- Job lifecycle and single Active Job ----

    [Fact]
    public void T01_Second_Job_Cannot_Start_While_One_Is_Active_Or_In_Safe_Return()
    {
        var running = Running();
        var second = SequencingKernel.Apply(running, new DispatchHead(At(10), Topology(), PumpReady: true));

        Assert.Equal(SequencingOutcome.REFUSED, second.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedJobActive, second.Evidence.Code);
        Assert.Equal(running.QueueRevision, second.State.QueueRevision);

        var inSafeReturn = Expect(SequencingKernel.Apply(running, new RequestAbort(At(10))));
        var refused = SequencingKernel.Apply(inSafeReturn, new DispatchHead(At(11), Topology(), PumpReady: true));
        Assert.Equal(SequencingOutcome.REFUSED, refused.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedJobActive, refused.Evidence.Code);
    }

    [Fact]
    public void T02_Normal_Completion_Enters_Safe_Return_With_Completed_Pending()
    {
        var state = RunToP6(Cleaning(Running(), 4), 9);
        var transition = SequencingKernel.Apply(state, new RequestNormalCompletion(At(30)));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(CleaningJobOutcome.COMPLETED, job.PendingOutcome);
        Assert.Equal(SequencingCodes.TriggerNormalCompletion, job.Trigger);
        Assert.Equal(Of("SR1", "SR2"), Codes(transition));
    }

    [Fact]
    public void T03_Abort_Enters_Safe_Return_With_Aborted_Pending()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(CleaningJobOutcome.ABORTED, job.PendingOutcome);
        Assert.Equal(Of("ABORT_REQUESTED", "SR1", "SR2"), Codes(transition));
    }

    [Fact]
    public void T04_Execution_Failure_Enters_Safe_Return_With_Failed_Pending_And_Reason()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ReportExecutionFailure(At(30), "SYN-FAULT-1"));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(CleaningJobOutcome.FAILED, job.PendingOutcome);
        Assert.Equal("SYN-FAULT-1", job.TriggerReason);
        Assert.Equal(Of("SR1", "SR2"), Codes(transition));

        var blank = SequencingKernel.Apply(Cleaning(Running(), 4), new ReportExecutionFailure(At(31), " "));
        Assert.Equal(SequencingOutcome.REFUSED, blank.Outcome);
        Assert.Equal(SequencingCodes.FailureReasonInvalid, blank.Evidence.Code);
    }

    // ---- Pump classification and critical latch ----

    [Fact]
    public void T05_Pump_Unexpected_Stop_Disables_Synthetic_Water_In_The_Same_Transition()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.UNEXPECTED_STOP));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.False(job.WaterOutputOn);
        Assert.False(job.CleaningActive);
        Assert.Equal(SequencingCodes.TriggerPumpUnexpectedStop, job.Trigger);
        Assert.Equal(CleaningJobOutcome.ABORTED, job.PendingOutcome);
        var water = Find(transition, SafeReturnStep.SR1);
        Assert.Equal(SequencingCodes.IntentWaterOutputOff, water.Intent);
    }

    [Fact]
    public void T06_Pump_Trip_Disables_Synthetic_Water_In_The_Same_Transition()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.TRIP));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.False(job.WaterOutputOn);
        Assert.False(job.CleaningActive);
        Assert.Equal(SequencingCodes.TriggerPumpTrip, job.Trigger);
        Assert.Equal(CleaningJobOutcome.ABORTED, job.PendingOutcome);
    }

    [Fact]
    public void T07_Pump_Critical_Sets_Critical_Suspended()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.TRIP));

        Assert.True(transition.State.CriticalSuspended);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(transition.State));
        Assert.Equal(AutoSequenceMode.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceMode(transition.State));
    }

    [Fact]
    public void T08_Critical_Event_Requests_The_Paired_Valve_Close_In_The_Same_Transition()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.UNEXPECTED_STOP));

        Assert.Equal(Of("CRITICAL_SUSPENSION_RAISED", "SR1", "SR2"), Codes(transition));
        var close = Find(transition, SafeReturnStep.SR2);
        Assert.Equal(SequencingCodes.IntentValveClose, close.Intent);
        Assert.Equal("IV1", close.ValveId);
        Assert.Equal("WJ1", close.JetId);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, RequireJob(transition.State).Lifecycle);
    }

    [Fact]
    public void T28_Expected_Pump_Stop_Is_Not_Critical()
    {
        var state = Expect(SequencingKernel.Apply(Running(), new ObservePumpState(At(30), PumpObservation.EXPECTED_STOP)));

        var job = RequireJob(state);
        Assert.False(state.CriticalSuspended);
        Assert.False(job.PumpReady);
        Assert.Equal(JobLifecycle.RUNNING, job.Lifecycle);
        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, SequencingKernel.ProjectAutoSequenceState(state));

        var idle = SequencingKernel.Apply(SequencingKernel.Initial(), new ObservePumpState(At(1), PumpObservation.EXPECTED_STOP));
        Assert.Equal(SequencingOutcome.NO_OP, idle.Outcome);
        Assert.False(idle.State.CriticalSuspended);
    }

    [Fact]
    public void Expected_Pump_Stop_While_Cleaning_Is_Refused_And_Not_Critical()
    {
        var state = Cleaning(Running(), 4);
        var transition = SequencingKernel.Apply(state, new ObservePumpState(At(30), PumpObservation.EXPECTED_STOP));

        Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
        Assert.Equal(SequencingCodes.PumpExpectedStopNotModelled, transition.Evidence.Code);
        Assert.False(transition.State.CriticalSuspended);
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(transition.State).Lifecycle);
    }

    [Fact]
    public void Pump_Not_Ready_During_Cleaning_Must_Be_Classified_Not_Gated()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpReadiness(At(30), Ready: false));

        Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
        Assert.Equal(SequencingCodes.PumpStopRequiresClassification, transition.Evidence.Code);
        Assert.True(RequireJob(transition.State).PumpReady);
    }

    // ---- Safe Return ordering ----

    [Fact]
    public void T09_Valve_Close_Request_Precedes_Axis_Standby_Return()
    {
        var trigger = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.TRIP));
        var closed = SequencingKernel.Apply(trigger.State, Valve(31, upper: false, lower: true));

        Assert.Equal(SequencingOutcome.APPLIED, closed.Outcome);
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED", "SR3", "SR4"), Codes(closed));
        var request = Find(trigger, SafeReturnStep.SR2).Seq;
        var confirmed = Find(closed, SafeReturnStep.SR3).Seq;
        var axis = Find(closed, SafeReturnStep.SR4);
        Assert.True(request < confirmed);
        Assert.True(confirmed < axis.Seq);
        Assert.Equal(SequencingCodes.IntentAxisToStandby, axis.Intent);
    }

    [Fact]
    public void T10_Axis_Return_Cannot_Be_Requested_Before_Valve_Closed_Confirmation()
    {
        var trigger = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));

        var axisFirst = SequencingKernel.Apply(trigger.State, new AxisFeedbackObserved(At(31), AxisFeedbackState.AT_STANDBY));
        Assert.Equal(SequencingOutcome.NO_OP, axisFirst.Outcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, RequireJob(axisFirst.State).Lifecycle);
        Assert.NotNull(axisFirst.State.ActiveJob);
        Assert.DoesNotContain(axisFirst.Records, record => record.Step == SafeReturnStep.SR4);
        Assert.DoesNotContain(axisFirst.Records, record => record.Intent == SequencingCodes.IntentAxisToStandby);

        var valveOpen = SequencingKernel.Apply(trigger.State, Valve(31, upper: true, lower: false));
        Assert.Equal(SequencingOutcome.APPLIED, valveOpen.Outcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, RequireJob(valveOpen.State).Lifecycle);
        Assert.Equal(Of(SequencingCodes.ValveNotConfirmed), Codes(valveOpen));
    }

    [Fact]
    public void T11_Safe_Return_Evidence_Sequence_Strictly_Increases_Across_The_Whole_Run()
    {
        var transitions = Run(NormalRunEvents());

        var sequence = transitions.SelectMany(transition => transition.Records).Select(record => record.Seq).ToArray();
        for (var index = 1; index < sequence.Length; index++)
        {
            Assert.True(sequence[index] > sequence[index - 1], "Evidence sequence must strictly increase.");
            Assert.Equal(sequence[index - 1] + 1, sequence[index]);
        }

        foreach (var transition in transitions)
        {
            Assert.Equal(transition.Evidence.Seq, transition.State.EvidenceSeq);
        }
    }

    [Fact]
    public void T12_Final_Outcome_Is_Absent_Before_Valve_And_Axis_Confirmations()
    {
        var transitions = Run(NormalRunEvents());
        var releaseIndex = transitions.Count - 1;

        for (var index = 0; index < releaseIndex; index++)
        {
            Assert.DoesNotContain(transitions[index].Records, record => record.JobOutcome is not null && record.Step is SafeReturnStep.SR6);
            Assert.DoesNotContain(transitions[index].Records, record => record.Step is SafeReturnStep.SR5 or SafeReturnStep.SR6 or SafeReturnStep.SR7);
        }

        Assert.Equal(Of("SR5", "SR6", "JOB_RELEASED"), Codes(transitions[releaseIndex]));
    }

    [Fact]
    public void T13_Active_Job_Is_Retained_Until_Verified_Safe_Return()
    {
        var trigger = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));
        Assert.NotNull(trigger.State.ActiveJob);

        var closed = SequencingKernel.Apply(trigger.State, Valve(31, upper: false, lower: true));
        Assert.NotNull(closed.State.ActiveJob);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, RequireJob(closed.State).Lifecycle);

        var released = SequencingKernel.Apply(closed.State, new AxisFeedbackObserved(At(32), AxisFeedbackState.AT_STANDBY));
        Assert.Null(released.State.ActiveJob);
    }

    [Fact]
    public void T14_Next_Dispatch_Is_Impossible_Before_Release()
    {
        var verifyStandby = Expect(SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30))));
        verifyStandby = Expect(SequencingKernel.Apply(verifyStandby, Valve(31, upper: false, lower: true)));

        var refused = SequencingKernel.Apply(verifyStandby, new DispatchHead(At(32), Topology(), PumpReady: true));
        Assert.Equal(SequencingOutcome.REFUSED, refused.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedJobActive, refused.Evidence.Code);
        Assert.Equal(verifyStandby.QueueRevision, refused.State.QueueRevision);
    }

    // ---- Outcomes ----

    [Fact]
    public void T15_Normal_Safe_Return_Produces_Completed()
    {
        var release = Run(NormalRunEvents())[^1];

        var standby = Find(release, SafeReturnStep.SR6);
        Assert.Equal(CleaningJobOutcome.COMPLETED, standby.JobOutcome);
        Assert.Null(release.State.ActiveJob);
    }

    [Fact]
    public void T16_Abort_Safe_Return_Produces_Aborted()
    {
        var release = Run(AbortRunEvents())[^1];

        Assert.Equal(CleaningJobOutcome.ABORTED, Find(release, SafeReturnStep.SR6).JobOutcome);
        Assert.Null(release.State.ActiveJob);
    }

    [Fact]
    public void T17_Execution_Failure_Safe_Return_Produces_Failed()
    {
        var events = Prelude().Concat(new SequencingEvent[]
        {
            Valve(4, upper: false, lower: true),
            new AdvanceJobPreparation(At(5)),
            new BeginCleaning(At(6)),
            new ReportExecutionFailure(At(7), "SYN-FAULT-1"),
            Valve(8, upper: false, lower: true),
            new AxisFeedbackObserved(At(9), AxisFeedbackState.AT_STANDBY),
        }).ToList();

        var release = Run(events)[^1];

        Assert.Equal(CleaningJobOutcome.FAILED, Find(release, SafeReturnStep.SR6).JobOutcome);
        Assert.Null(release.State.ActiveJob);
    }

    [Fact]
    public void T18_Pump_Critical_Safe_Return_Produces_Aborted()
    {
        var events = Prelude().Concat(new SequencingEvent[]
        {
            Valve(4, upper: false, lower: true),
            new AdvanceJobPreparation(At(5)),
            new BeginCleaning(At(6)),
            new ObservePumpState(At(7), PumpObservation.UNEXPECTED_STOP),
            Valve(8, upper: false, lower: true),
            new AxisFeedbackObserved(At(9), AxisFeedbackState.AT_STANDBY),
        }).ToList();

        var release = Run(events)[^1];

        Assert.Equal(CleaningJobOutcome.ABORTED, Find(release, SafeReturnStep.SR6).JobOutcome);
        Assert.Null(release.State.ActiveJob);
        Assert.True(release.State.CriticalSuspended);
    }

    // ---- Safe Return failure ----

    [Fact]
    public void T19_Valve_Close_Failure_Produces_Safe_Return_Failed_With_Recovery_Evidence()
    {
        var trigger = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));
        var before = trigger.State;
        var queueBefore = QueueJson(before);
        var revisionBefore = before.QueueRevision;

        var failed = SequencingKernel.Apply(before, new FeedbackTimeoutExpired(At(31), FeedbackTarget.VALVE_CLOSED));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        var job = RequireJob(failed.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.ValveCloseNotConfirmed, job.FailureCode);

        // Retained evidence: RECOVERY_REQUIRED, and no final outcome.
        var failure = Find(failed, SafeReturnStep.SR_FAILED);
        Assert.Equal(CleaningJobOutcome.RECOVERY_REQUIRED, failure.JobOutcome);
        Assert.DoesNotContain(failed.Records, record => record.JobOutcome is CleaningJobOutcome.COMPLETED or CleaningJobOutcome.ABORTED or CleaningJobOutcome.FAILED);

        // Queue preserved.
        Assert.Equal(queueBefore, QueueJson(failed.State));
        Assert.Equal(revisionBefore, failed.State.QueueRevision);
    }

    [Fact]
    public void T19b_Invalid_Valve_Limit_State_Fails_Immediately_During_Safe_Return()
    {
        var trigger = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));

        var failed = SequencingKernel.Apply(trigger.State, Valve(31, upper: true, lower: true));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, RequireJob(failed.State).Lifecycle);
        Assert.Equal(SequencingCodes.ValveInvalidLimitState, RequireJob(failed.State).FailureCode);
        Assert.Equal(CleaningJobOutcome.RECOVERY_REQUIRED, Find(failed, SafeReturnStep.SR_FAILED).JobOutcome);
    }

    [Fact]
    public void T19c_Invalid_Valve_Limit_During_Cleaning_Enters_Safe_Return_As_Execution_Failure()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), Valve(30, upper: true, lower: true));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        var job = RequireJob(transition.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(CleaningJobOutcome.FAILED, job.PendingOutcome);
        Assert.Equal(SequencingCodes.ValveInvalidLimitState, job.TriggerReason);
    }

    [Fact]
    public void T20_Axis_Standby_Fault_Produces_Safe_Return_Failed()
    {
        var verifyStandby = Run(AbortRunEvents(includeAxis: false))[^1];

        var failed = SequencingKernel.Apply(verifyStandby.State, new AxisFeedbackObserved(At(20), AxisFeedbackState.FAULT));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        var job = RequireJob(failed.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.AxisFault, job.FailureCode);
        Assert.Equal(CleaningJobOutcome.RECOVERY_REQUIRED, Find(failed, SafeReturnStep.SR_FAILED).JobOutcome);
    }

    [Fact]
    public void T20b_Axis_Standby_Timeout_Produces_Safe_Return_Failed()
    {
        var verifyStandby = Run(AbortRunEvents(includeAxis: false))[^1];

        var failed = SequencingKernel.Apply(verifyStandby.State, new FeedbackTimeoutExpired(At(20), FeedbackTarget.AXIS_STANDBY));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, RequireJob(failed.State).Lifecycle);
        Assert.Equal(SequencingCodes.AxisStandbyNotConfirmedFailure, RequireJob(failed.State).FailureCode);
    }

    [Fact]
    public void T22_Safe_Return_Failure_Retains_The_Active_Job()
    {
        var failed = SequencingKernel.Apply(
            SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30))).State,
            new FeedbackTimeoutExpired(At(31), FeedbackTarget.VALVE_CLOSED));

        var job = RequireJob(failed.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal("SYN-S01", job.TargetSensorId);
        Assert.False(job.WaterOutputOn);
        Assert.Null(RequireJob(failed.State).Ledger.AxisStandbySeq);
        Assert.Null(RequireJob(failed.State).Ledger.ValveClosedSeq);
    }

    [Fact]
    public void T24_No_Automatic_Retry_After_Safe_Return_Failure()
    {
        var failed = SequencingKernel.Apply(
            SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30))).State,
            new FeedbackTimeoutExpired(At(31), FeedbackTarget.VALVE_CLOSED));

        var closedLater = SequencingKernel.Apply(failed.State, Valve(32, upper: false, lower: true));
        Assert.Equal(SequencingOutcome.NO_OP, closedLater.Outcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, RequireJob(closedLater.State).Lifecycle);
        Assert.DoesNotContain(closedLater.Records, record => record.Step is SafeReturnStep.SR3 or SafeReturnStep.SR4);

        var axisLater = SequencingKernel.Apply(closedLater.State, new AxisFeedbackObserved(At(33), AxisFeedbackState.AT_STANDBY));
        Assert.Equal(SequencingOutcome.NO_OP, axisLater.Outcome);
        Assert.NotNull(axisLater.State.ActiveJob);

        var timeoutAgain = SequencingKernel.Apply(axisLater.State, new FeedbackTimeoutExpired(At(34), FeedbackTarget.VALVE_CLOSED));
        Assert.Equal(SequencingOutcome.REFUSED, timeoutAgain.Outcome);
        Assert.Equal(SequencingCodes.FeedbackTimeoutNotPending, timeoutAgain.Evidence.Code);
    }

    [Fact]
    public void T25_No_Automatic_Reset_The_Critical_Latch_Persists_After_Safe_Return_Failure()
    {
        var failed = SequencingKernel.Apply(
            SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.UNEXPECTED_STOP)).State,
            new FeedbackTimeoutExpired(At(31), FeedbackTarget.VALVE_CLOSED));

        // Later readings after the failure are recorded as evidence only (NO_OP); none of them clears the latch.
        var state = failed.State;
        state = SequencingKernel.Apply(state, new ObservePumpState(At(32), PumpObservation.READY)).State;
        state = SequencingKernel.Apply(state, Valve(33, upper: false, lower: true)).State;
        state = SequencingKernel.Apply(state, new AxisFeedbackObserved(At(34), AxisFeedbackState.AT_STANDBY)).State;

        Assert.True(state.CriticalSuspended);
        Assert.NotNull(state.ActiveJob);
    }

    [Fact]
    public void T26_No_Automatic_Resume_After_Safe_Return_Failure()
    {
        var failed = SequencingKernel.Apply(
            SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30))).State,
            new FeedbackTimeoutExpired(At(31), FeedbackTarget.VALVE_CLOSED));

        Assert.Equal(AutoSequenceState.JOB_ACTIVE, SequencingKernel.ProjectAutoSequenceState(failed.State));
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, SequencingKernel.ProjectJobLifecycle(failed.State));

        var dispatch = SequencingKernel.Apply(failed.State, new DispatchHead(At(32), Topology(), PumpReady: true));
        Assert.Equal(SequencingOutcome.REFUSED, dispatch.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedJobActive, dispatch.Evidence.Code);
    }

    [Fact]
    public void Timeouts_Are_Only_Honoured_For_A_Pending_Wait()
    {
        var running = Running();
        var refused = SequencingKernel.Apply(running, new FeedbackTimeoutExpired(At(10), FeedbackTarget.VALVE_CLOSED));

        Assert.Equal(SequencingOutcome.REFUSED, refused.Outcome);
        Assert.Equal(SequencingCodes.FeedbackTimeoutNotPending, refused.Evidence.Code);
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(refused.State).Lifecycle);
    }

    // ---- Critical latch and release ----

    [Fact]
    public void T27_Critical_Job_Release_Does_Not_Clear_The_Critical_Latch()
    {
        var release = Run(CriticalRunEvents(PumpObservation.UNEXPECTED_STOP))[^1];

        Assert.Null(release.State.ActiveJob);
        Assert.True(release.State.CriticalSuspended);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(release.State));
        Assert.Equal(CleaningJobOutcome.ABORTED, Find(release, SafeReturnStep.SR6).JobOutcome);

        var dispatch = SequencingKernel.Apply(release.State, new DispatchHead(At(60), Topology(), PumpReady: true));
        Assert.Equal(SequencingOutcome.REFUSED, dispatch.Outcome);
        Assert.Equal(SequencingCodes.CriticalSuspended, dispatch.Evidence.Code);
    }

    [Fact]
    public void T29b_Critical_During_Safe_Return_Sets_The_Latch_Without_Restarting_The_Steps()
    {
        var aborting = SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(30)));

        var critical = SequencingKernel.Apply(aborting.State, new ObservePumpState(At(31), PumpObservation.TRIP));

        Assert.Equal(SequencingOutcome.APPLIED, critical.Outcome);
        Assert.True(critical.State.CriticalSuspended);
        Assert.Equal(Of(SequencingCodes.CriticalEventDuringSafeReturn), Codes(critical));
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, RequireJob(critical.State).Lifecycle);
        Assert.Equal(CleaningJobOutcome.ABORTED, RequireJob(critical.State).PendingOutcome);
    }

    [Fact]
    public void Second_Critical_Event_With_The_Latch_Already_Set_Is_A_No_Op_In_Safe_Return()
    {
        var critical = SequencingKernel.Apply(Cleaning(Running(), 4), new ObservePumpState(At(30), PumpObservation.TRIP));

        var second = SequencingKernel.Apply(critical.State, new ObservePumpState(At(31), PumpObservation.UNEXPECTED_STOP));

        Assert.Equal(SequencingOutcome.NO_OP, second.Outcome);
        Assert.True(second.State.CriticalSuspended);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, RequireJob(second.State).Lifecycle);
    }

    // ---- WJ/IV pairing ----

    [Fact]
    public void T29_WJn_Remains_Paired_With_IVn_Through_Safe_Return()
    {
        var state = Expect(SequencingKernel.Apply(SequencingKernel.Initial(), new StartAutoSequence(At(0))));
        state = Expect(SequencingKernel.Apply(state, Admit(1, "SYN-S03")));
        state = Expect(SequencingKernel.Apply(state, new DispatchHead(At(2), Topology(), PumpReady: true)));

        var job = RequireJob(state);
        Assert.Equal("WJ3", job.JetId);
        Assert.Equal("IV3", job.ValveId);

        var mismatched = SequencingKernel.Apply(state, Valve(3, upper: false, lower: true, valveId: "IV1"));
        Assert.Equal(SequencingOutcome.REFUSED, mismatched.Outcome);
        Assert.Equal(SequencingCodes.ValveIdMismatch, mismatched.Evidence.Code);

        var aborted = SequencingKernel.Apply(state, new RequestAbort(At(4)));
        var closed = SequencingKernel.Apply(aborted.State, Valve(5, upper: false, lower: true, valveId: "IV3"));
        Assert.Equal(SequencingOutcome.APPLIED, closed.Outcome);
        Assert.Equal("IV3", Find(closed, SafeReturnStep.SR3).ValveId);
        Assert.Equal("WJ3", Find(closed, SafeReturnStep.SR3).JetId);
    }

    [Fact]
    public void T30_Invalid_WJ_IV_Pairing_Is_Refused_At_Admission()
    {
        var mismatched = new SequencingTopology(
            new[] { new SequencingSensorAssignment("SYN-S01", "WJ1", "IV2") },
            NonSensorIds);
        var state = Expect(SequencingKernel.Apply(SequencingKernel.Initial(), new StartAutoSequence(At(0))));

        var admission = SequencingKernel.Apply(
            state,
            new AdmitQueueEntry(At(1), SequencingAdmissionSource.SCENARIO_PREPARED, "SYN-S01", "SCENARIO_PREPARED", 0, mismatched));

        Assert.Equal(SequencingOutcome.REFUSED, admission.Outcome);
        Assert.Equal(SequencingCodes.EntryInvalid, admission.Evidence.Code);
        Assert.Empty(SequencingKernel.ProjectQueueEntries(admission.State));
    }

    // ---- No external completion, determinism and phase gates ----

    [Fact]
    public void T31_External_Completion_Evidence_Cannot_Skip_Safe_Return()
    {
        var cleaning = Cleaning(Running(), 4);

        var axisDuringRun = SequencingKernel.Apply(cleaning, new AxisFeedbackObserved(At(20), AxisFeedbackState.AT_STANDBY));
        Assert.Equal(SequencingOutcome.NO_OP, axisDuringRun.Outcome);
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(axisDuringRun.State).Lifecycle);
        Assert.NotNull(axisDuringRun.State.ActiveJob);
        Assert.DoesNotContain(axisDuringRun.Records, record => record.Step is not null);

        var completion = SequencingKernel.Apply(cleaning, new RequestNormalCompletion(At(21)));
        Assert.Equal(SequencingOutcome.REFUSED, completion.Outcome);
        Assert.Equal(SequencingCodes.PhaseIncomplete, completion.Evidence.Code);
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(completion.State).Lifecycle);
    }

    [Fact]
    public void T32_Identical_Inputs_Produce_Byte_Identical_State_And_Evidence()
    {
        var first = Run(CriticalRunEvents(PumpObservation.TRIP));
        var second = Run(CriticalRunEvents(PumpObservation.TRIP));

        Assert.Equal(
            JsonSerializer.Serialize(first.SelectMany(transition => transition.Records).ToArray(), ContractJson.Options),
            JsonSerializer.Serialize(second.SelectMany(transition => transition.Records).ToArray(), ContractJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(first[^1].State, ContractJson.Options),
            JsonSerializer.Serialize(second[^1].State, ContractJson.Options));
    }

    [Fact]
    public void Phases_Are_Strictly_Ordered_And_Gated_By_The_Valve()
    {
        var state = Cleaning(Running(), 4);

        var outOfOrder = SequencingKernel.Apply(state, new ExecutionPhaseVerified(At(20), JobPhase.P2));
        Assert.Equal(SequencingOutcome.REFUSED, outOfOrder.Outcome);
        Assert.Equal(SequencingCodes.PhaseOutOfOrder, outOfOrder.Evidence.Code);

        var p1 = Expect(SequencingKernel.Apply(state, new ExecutionPhaseVerified(At(21), JobPhase.P1)));
        var noOpen = SequencingKernel.Apply(p1, new ExecutionPhaseVerified(At(22), JobPhase.P2));
        Assert.Equal(SequencingOutcome.REFUSED, noOpen.Outcome);
        Assert.Equal(SequencingCodes.ValveNotOpen, noOpen.Evidence.Code);
    }

    [Fact]
    public void Cleaning_Requires_A_Ready_Pump_And_A_Closed_Valve()
    {
        var notReady = Expect(SequencingKernel.Apply(Running(), new ObservePumpReadiness(At(10), Ready: false)));
        var prepared = Expect(SequencingKernel.Apply(notReady, Valve(11, upper: false, lower: true)));
        prepared = Expect(SequencingKernel.Apply(prepared, new AdvanceJobPreparation(At(12))));

        var refused = SequencingKernel.Apply(prepared, new BeginCleaning(At(13)));
        Assert.Equal(SequencingOutcome.REFUSED, refused.Outcome);
        Assert.Equal(SequencingCodes.PumpNotReady, refused.Evidence.Code);
        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, SequencingKernel.ProjectAutoSequenceState(prepared));
    }

    // ---- Validator: CP-2 state combinations ----

    [Fact]
    public void Validator_Rejects_A_Running_Job_That_Carries_Safe_Return_Data()
    {
        var state = Raw(RawJob(trigger: SequencingCodes.TriggerAbort, pending: CleaningJobOutcome.ABORTED, step: SafeReturnStep.SR2, ledger: new SafeReturnLedger(2, 3, null, null, null, null)));

        AssertInvalid(state, "JOB_RUNNING_HAS_SAFE_RETURN");
    }

    [Fact]
    public void Validator_Rejects_A_Pump_Trigger_Without_The_Critical_Latch()
    {
        var state = Raw(
            RawJob(
                lifecycle: JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED,
                trigger: SequencingCodes.TriggerPumpTrip,
                pending: CleaningJobOutcome.ABORTED,
                step: SafeReturnStep.SR2,
                ledger: new SafeReturnLedger(2, 3, null, null, null, null)),
            critical: false);

        AssertInvalid(state, "JOB_CRITICAL_LATCH_MISSING");
    }

    [Fact]
    public void Validator_Rejects_A_Trigger_Whose_Pending_Outcome_Does_Not_Match()
    {
        var state = Raw(RawJob(
            lifecycle: JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED,
            trigger: SequencingCodes.TriggerNormalCompletion,
            pending: CleaningJobOutcome.ABORTED,
            step: SafeReturnStep.SR2,
            ledger: new SafeReturnLedger(2, 3, null, null, null, null)));

        AssertInvalid(state, "JOB_TRIGGER_OUTCOME_MISMATCH");
    }

    [Fact]
    public void Validator_Rejects_Water_On_During_Safe_Return()
    {
        var state = Raw(RawJob(
            lifecycle: JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED,
            cleaning: true,
            water: true,
            trigger: SequencingCodes.TriggerAbort,
            pending: CleaningJobOutcome.ABORTED,
            step: SafeReturnStep.SR2,
            ledger: new SafeReturnLedger(2, 3, null, null, null, null)));

        AssertInvalid(state, "JOB_SAFE_RETURN_WATER_ON");
    }

    [Fact]
    public void Validator_Rejects_A_Transient_Lifecycle_As_A_Stored_State()
    {
        var state = Raw(RawJob(
            lifecycle: JobLifecycle.SAFE_RETURN_CLOSE_VALVE,
            trigger: SequencingCodes.TriggerAbort,
            pending: CleaningJobOutcome.ABORTED,
            step: SafeReturnStep.SR2,
            ledger: new SafeReturnLedger(2, 3, null, null, null, null)));

        AssertInvalid(state, "JOB_LIFECYCLE_TRANSIENT");
    }

    [Fact]
    public void Validator_Rejects_Cleaning_Without_A_Valve_Observation()
    {
        var state = Raw(RawJob(stage: CleaningStage.CLEANING, cleaning: true, water: true, valve: null));

        AssertInvalid(state, "JOB_VALVE_GATE_INVALID");
    }

    [Fact]
    public void Validator_Rejects_Ready_To_Clean_Without_A_Closed_Valve()
    {
        var state = Raw(RawJob(stage: CleaningStage.READY_TO_CLEAN, valve: ValveFeedbackState.OPEN));

        AssertInvalid(state, "JOB_VALVE_GATE_INVALID");
    }

    // ---- helpers (synthetic only) ----

    private static string[] Of(params string[] values) => values;

    private static DateTimeOffset At(int second) => Instant.AddSeconds(second);

    private static SequencingTopology Topology()
    {
        var assignments = new List<SequencingSensorAssignment>();
        for (var index = 0; index < SensorIds.Length; index++)
        {
            var ordinal = (index % 8 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            assignments.Add(new SequencingSensorAssignment(SensorIds[index], "WJ" + ordinal, "IV" + ordinal));
        }

        return new SequencingTopology(assignments, NonSensorIds);
    }

    private static AdmitQueueEntry Admit(int second, string sensorId) =>
        new(At(second), SequencingAdmissionSource.SCENARIO_PREPARED, sensorId, "SCENARIO_PREPARED", 0, Topology());

    private static ValveLimitObserved Valve(int second, bool upper, bool lower, string valveId = "IV1") =>
        new(At(second), valveId, upper, lower);

    private static SequencingState Expect(SequencingTransition transition)
    {
        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        return transition.State;
    }

    private static List<SequencingTransition> Run(IEnumerable<SequencingEvent> events)
    {
        var state = SequencingKernel.Initial();
        var transitions = new List<SequencingTransition>();
        foreach (var sequencingEvent in events)
        {
            var transition = SequencingKernel.Apply(state, sequencingEvent);
            Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
            transitions.Add(transition);
            state = transition.State;
        }

        return transitions;
    }

    private static List<SequencingEvent> Prelude() => new()
    {
        new StartAutoSequence(At(0)),
        Admit(1, "SYN-S01"),
        Admit(2, "SYN-S02"),
        new DispatchHead(At(3), Topology(), PumpReady: true),
    };

    private static List<SequencingEvent> Phases(int first)
    {
        var phases = new List<SequencingEvent>();
        var tick = first;
        foreach (var phase in new[] { JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 })
        {
            phases.Add(new ExecutionPhaseVerified(At(tick), phase));
            tick++;
        }

        return phases;
    }

    /// <summary>Start through release on the normal path: completion, valve close, then axis Standby.</summary>
    private static List<SequencingEvent> NormalRunEvents()
    {
        var events = Prelude();
        events.Add(Valve(4, upper: false, lower: true));
        events.Add(new AdvanceJobPreparation(At(5)));
        events.Add(new BeginCleaning(At(6)));
        events.Add(new ExecutionPhaseVerified(At(7), JobPhase.P1));
        events.Add(Valve(8, upper: true, lower: false));
        events.AddRange(Phases(9));
        events.Add(new RequestNormalCompletion(At(14)));
        events.Add(Valve(15, upper: false, lower: true));
        events.Add(new AxisFeedbackObserved(At(16), AxisFeedbackState.AT_STANDBY));
        return events;
    }

    /// <summary>Start through release on the abort path.</summary>
    private static List<SequencingEvent> AbortRunEvents(bool includeAxis = true)
    {
        var events = Prelude();
        events.Add(Valve(4, upper: false, lower: true));
        events.Add(new AdvanceJobPreparation(At(5)));
        events.Add(new BeginCleaning(At(6)));
        events.Add(new RequestAbort(At(7)));
        events.Add(Valve(8, upper: false, lower: true));
        if (includeAxis)
        {
            events.Add(new AxisFeedbackObserved(At(9), AxisFeedbackState.AT_STANDBY));
        }

        return events;
    }

    /// <summary>Start through release with a Pump critical event during cleaning.</summary>
    private static List<SequencingEvent> CriticalRunEvents(PumpObservation observation)
    {
        var events = Prelude();
        events.Add(Valve(4, upper: false, lower: true));
        events.Add(new AdvanceJobPreparation(At(5)));
        events.Add(new BeginCleaning(At(6)));
        events.Add(new ExecutionPhaseVerified(At(7), JobPhase.P1));
        events.Add(new ObservePumpState(At(8), observation));
        events.Add(Valve(9, upper: false, lower: true));
        events.Add(new AxisFeedbackObserved(At(10), AxisFeedbackState.AT_STANDBY));
        return events;
    }

    private static SequencingState Running() => Expect(Run(Prelude())[^1]);

    private static SequencingState Cleaning(SequencingState state, int second)
    {
        state = Expect(SequencingKernel.Apply(state, Valve(second, upper: false, lower: true)));
        state = Expect(SequencingKernel.Apply(state, new AdvanceJobPreparation(At(second + 1))));
        return Expect(SequencingKernel.Apply(state, new BeginCleaning(At(second + 2))));
    }

    private static SequencingState RunToP6(SequencingState state, int second)
    {
        state = Expect(SequencingKernel.Apply(state, new ExecutionPhaseVerified(At(second), JobPhase.P1)));
        state = Expect(SequencingKernel.Apply(state, Valve(second + 1, upper: true, lower: false)));
        var tick = second + 2;
        foreach (var phase in new[] { JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 })
        {
            state = Expect(SequencingKernel.Apply(state, new ExecutionPhaseVerified(At(tick), phase)));
            tick++;
        }

        return state;
    }

    private static SequencingActiveJob RequireJob(SequencingState state) =>
        state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");

    private static string[] Codes(SequencingTransition transition) =>
        transition.Records.Select(record => record.Code).ToArray();

    private static SequencingEvidence Find(SequencingTransition transition, SafeReturnStep step) =>
        transition.Records.Single(record => record.Step == step);

    private static string QueueJson(SequencingState state) =>
        JsonSerializer.Serialize(SequencingKernel.ProjectQueueEntries(state), ContractJson.Options);

    private static SequencingState Raw(SequencingActiveJob? job, bool critical = true, int evidenceSeq = 10)
    {
        var built = RawConstructor.Invoke(new object?[]
        {
            Array.Empty<SequencingEntry>(),
            1,
            job,
            AutoSequenceMode.RUNNING,
            critical,
            evidenceSeq,
            1,
            1,
        });
        return (SequencingState)built!;
    }

    private static SequencingActiveJob RawJob(
        JobLifecycle lifecycle = JobLifecycle.RUNNING,
        CleaningStage stage = CleaningStage.PREPARING,
        bool cleaning = false,
        bool water = false,
        ValveFeedbackState? valve = null,
        CleaningJobOutcome? pending = null,
        string? trigger = null,
        SafeReturnStep? step = null,
        SafeReturnLedger? ledger = null) =>
        new(
            "SYN-JOB-1",
            "SYN-DSP-1",
            "SYN-S01",
            "WJ1",
            "IV1",
            "SYN-QE-0",
            1,
            true,
            Instant,
            0,
            1,
            lifecycle,
            stage,
            null,
            cleaning,
            water,
            valve,
            pending,
            trigger,
            null,
            step,
            null,
            ledger ?? SafeReturnLedger.Empty);

    private static void AssertInvalid(SequencingState state, string violation)
    {
        var error = Assert.Throws<InvalidOperationException>(() => { _ = SequencingKernel.ProjectJobLifecycle(state); });
        Assert.Equal(SequencingCodes.StateInvalid + ": " + violation, error.Message);
        Assert.Equal(SequencingOutcome.REFUSED, SequencingKernel.Apply(state, new StartAutoSequence(At(0))).Outcome);
    }
}
