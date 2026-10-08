using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3a hardening tests: FU-1 (critical latch with a RUNNING Job), FU-4 (valve movement during
/// CLEANING) and the AxisStandbySeq write with its pure preview. These tests were written in Arena and are
/// NOT EXECUTED IN ARENA; they are planned for Owner-local execution. Every identifier is synthetic (SYN-*).
/// Invalid states are built by reflection on the internal constructor, as in the CP-1 integrity tests.
/// </summary>
public sealed class SequencingCp3aHardeningTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] SensorIds =
        ["SYN-S01", "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08", "SYN-S09", "SYN-S10"];

    private static readonly string[] NonSensorIds = ["SYN-G01"];

    // The single internal constructor takes eight parameters; the record copy constructor takes one.
    private static readonly ConstructorInfo RawConstructor = typeof(SequencingState)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(constructor => constructor.GetParameters().Length == 8);

    // ---- FU-1: critical latch with a RUNNING Job ----

    [Fact]
    public void Running_Job_Under_The_Critical_Latch_Is_Rejected_With_One_Stable_Code()
    {
        var state = Raw(RawJob(), critical: true);

        AssertInvalid(state, "JOB_RUNNING_UNDER_LATCH");
    }

    [Fact]
    public void Earlier_Running_Violations_Keep_Their_Own_Codes_Under_The_Latch()
    {
        var state = Raw(
            RawJob(trigger: SequencingCodes.TriggerAbort, pending: CleaningJobOutcome.ABORTED, step: SafeReturnStep.SR2, ledger: new SafeReturnLedger(2, 3, null, null, null, null)),
            critical: true);

        AssertInvalid(state, "JOB_RUNNING_HAS_SAFE_RETURN");
    }

    [Fact]
    public void Latch_Without_A_Job_Stays_Valid_And_Projects_Critical()
    {
        var state = Raw(null, critical: true);

        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(state));
    }

    [Fact]
    public void A_Critical_Event_On_A_Running_Job_Never_Leaves_A_Running_Job_Under_The_Latch()
    {
        var running = Running();

        var critical = SequencingKernel.Apply(running, new ObservePumpState(At(4), PumpObservation.UNEXPECTED_STOP));

        Assert.Equal(SequencingOutcome.APPLIED, critical.Outcome);
        Assert.True(critical.State.CriticalSuspended);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, SequencingKernel.ProjectJobLifecycle(critical.State));
    }

    // ---- FU-4: valve movement during CLEANING ----

    [Theory]
    [InlineData(false, true)]  // CLOSED
    [InlineData(false, false)] // TRANSIT_OR_FAULT
    public void Valve_Movement_Away_From_Open_During_Cleaning_Enters_Safe_Return_In_The_Same_Transition(bool upper, bool lower)
    {
        var opened = Expect(SequencingKernel.Apply(Cleaning(Running(), 4), Valve(7, upper: true, lower: false)));
        var queueBefore = QueueJson(opened);
        var revisionBefore = opened.QueueRevision;

        var lost = SequencingKernel.Apply(opened, Valve(8, upper, lower));

        Assert.Equal(SequencingOutcome.APPLIED, lost.Outcome);
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED", "SR1", "SR2"), Codes(lost));
        var job = RequireJob(lost.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(SafeReturnStep.SR2, job.Step);
        Assert.Equal(CleaningJobOutcome.FAILED, job.PendingOutcome);
        Assert.Equal(SequencingCodes.TriggerExecutionFailure, job.Trigger);
        Assert.Equal("VALVE_LOST_DURING_CLEANING", job.TriggerReason);
        Assert.Null(job.FailureCode);
        Assert.False(job.CleaningActive);
        Assert.False(job.WaterOutputOn);
        Assert.NotNull(job.Ledger.WaterOffSeq);
        Assert.NotNull(job.Ledger.ValveCloseRequestSeq);
        Assert.Null(job.Ledger.ValveClosedSeq);
        Assert.False(lost.State.CriticalSuspended);
        Assert.Equal(queueBefore, QueueJson(lost.State));
        Assert.Equal(revisionBefore, lost.State.QueueRevision);
    }

    [Fact]
    public void Valve_Loss_Confirms_The_Close_Only_On_A_Later_Closed_Reading_And_Releases_As_Failed()
    {
        var opened = Expect(SequencingKernel.Apply(Cleaning(Running(), 4), Valve(7, upper: true, lower: false)));
        var loss = Expect(SequencingKernel.Apply(opened, Valve(8, upper: false, lower: false)));

        var confirmed = SequencingKernel.Apply(loss, Valve(9, upper: false, lower: true));

        Assert.Equal(SequencingOutcome.APPLIED, confirmed.Outcome);
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED", "SR3", "SR4"), Codes(confirmed));
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, RequireJob(confirmed.State).Lifecycle);

        var released = SequencingKernel.Apply(confirmed.State, new AxisFeedbackObserved(At(10), AxisFeedbackState.AT_STANDBY));

        Assert.Equal(Of("SR5", "SR6", "JOB_RELEASED"), Codes(released));
        Assert.Equal(CleaningJobOutcome.FAILED, Find(released, SafeReturnStep.SR6).JobOutcome);
        Assert.Null(released.State.ActiveJob);
        Assert.False(released.State.CriticalSuspended);
    }

    [Fact]
    public void Repeated_Open_Reading_During_Cleaning_Is_Not_A_Valve_Loss()
    {
        var opened = Expect(SequencingKernel.Apply(Cleaning(Running(), 4), Valve(7, upper: true, lower: false)));

        var repeated = SequencingKernel.Apply(opened, Valve(8, upper: true, lower: false));

        Assert.Equal(SequencingOutcome.NO_OP, repeated.Outcome);
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(repeated.State).Lifecycle);
        Assert.True(RequireJob(repeated.State).CleaningActive);
    }

    [Fact]
    public void Valve_Movement_Before_Any_Open_Reading_In_Cleaning_Stays_Recorded_Only()
    {
        // The residual is recorded as [OPEN] for the Owner: no OPEN reading has been confirmed yet.
        var transit = SequencingKernel.Apply(Cleaning(Running(), 4), Valve(7, upper: false, lower: false));

        Assert.Equal(SequencingOutcome.APPLIED, transit.Outcome);
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED"), Codes(transit));
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(transit.State).Lifecycle);
    }

    [Fact]
    public void Valve_Movement_Outside_Cleaning_Is_Recorded_Only()
    {
        var prepared = Expect(SequencingKernel.Apply(Running(), Valve(4, upper: false, lower: true)));

        var opened = SequencingKernel.Apply(prepared, Valve(5, upper: true, lower: false));
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED"), Codes(opened));
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(opened.State).Lifecycle);

        var transit = SequencingKernel.Apply(opened.State, Valve(6, upper: false, lower: false));
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED"), Codes(transit));
        Assert.Equal(JobLifecycle.RUNNING, RequireJob(transit.State).Lifecycle);
    }

    // ---- Latch with Safe Return jobs (accepted states) ----

    [Fact]
    public void Latch_With_A_Safe_Return_Job_Is_Accepted_And_Projects_Critical()
    {
        var critical = SequencingKernel.Apply(Running(), new ObservePumpState(At(4), PumpObservation.UNEXPECTED_STOP));

        Assert.Equal(SequencingOutcome.APPLIED, critical.Outcome);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(critical.State));
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, SequencingKernel.ProjectJobLifecycle(critical.State));
    }

    [Fact]
    public void Latch_With_A_Failed_Safe_Return_Job_Is_Accepted_And_Projects_Critical()
    {
        var latched = Expect(SequencingKernel.Apply(Running(), new ObservePumpState(At(4), PumpObservation.TRIP)));

        var failed = SequencingKernel.Apply(latched, new FeedbackTimeoutExpired(At(5), FeedbackTarget.VALVE_CLOSED));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(failed.State));
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, SequencingKernel.ProjectJobLifecycle(failed.State));
    }

    // ---- INVALID_LIMIT_STATE during CLEANING and failed Safe Return evidence ----

    [Fact]
    public void Invalid_Limit_State_During_Cleaning_Enters_Failure_Safe_Return_In_The_Same_Transition()
    {
        var transition = SequencingKernel.Apply(Cleaning(Running(), 4), Valve(7, upper: true, lower: true));

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        Assert.Equal(Of("VALVE_FEEDBACK_OBSERVED", "SR1", "SR2"), Codes(transition));
        var job = RequireJob(transition.State);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED, job.Lifecycle);
        Assert.Equal(CleaningJobOutcome.FAILED, job.PendingOutcome);
        Assert.Equal("VALVE_INVALID_LIMIT_STATE", job.TriggerReason);
        Assert.False(job.WaterOutputOn);
        Assert.False(transition.State.CriticalSuspended);
    }

    [Fact]
    public void Failed_Safe_Return_Carries_Recovery_Required_And_Retains_The_Job()
    {
        var aborting = Expect(SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(7))));

        var failed = SequencingKernel.Apply(aborting, new FeedbackTimeoutExpired(At(8), FeedbackTarget.VALVE_CLOSED));

        Assert.Equal(SequencingOutcome.APPLIED, failed.Outcome);
        Assert.Equal(CleaningJobOutcome.RECOVERY_REQUIRED, Find(failed, SafeReturnStep.SR_FAILED).JobOutcome);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, RequireJob(failed.State).Lifecycle);
        Assert.DoesNotContain(failed.Records, record => record.JobOutcome is CleaningJobOutcome.COMPLETED or CleaningJobOutcome.ABORTED or CleaningJobOutcome.FAILED);
    }

    // ---- Axis Standby confirmation is SR5, between the valve confirmation and the outcome ----

    [Fact]
    public void Axis_Standby_Confirmation_Is_Recorded_As_SR5_Before_The_Outcome_And_Release()
    {
        var closed = SequencingKernel.Apply(Expect(SequencingKernel.Apply(Cleaning(Running(), 4), new RequestAbort(At(7)))), Valve(8, upper: false, lower: true));
        var released = SequencingKernel.Apply(Expect(closed), new AxisFeedbackObserved(At(9), AxisFeedbackState.AT_STANDBY));

        Assert.Equal(Of("SR5", "SR6", "JOB_RELEASED"), Codes(released));
        var sr4 = Find(closed, SafeReturnStep.SR4).Seq;
        var sr5 = Find(released, SafeReturnStep.SR5).Seq;
        var sr6 = Find(released, SafeReturnStep.SR6).Seq;
        var sr7 = released.Records.Single(record => record.Code == SequencingCodes.JobReleased).Seq;
        Assert.True(sr4 < sr5);
        Assert.True(sr5 < sr6);
        Assert.True(sr6 < sr7);
        Assert.Null(released.State.ActiveJob);
    }

    // ---- helpers (synthetic only) ----

    private static DateTimeOffset At(int second) => Instant.AddSeconds(second);

    private static SequencingTopology Topology()
    {
        var assignments = new List<SequencingSensorAssignment>();
        for (var index = 0; index < SensorIds.Length; index++)
        {
            var ordinal = (index % 8 + 1).ToString(CultureInfo.InvariantCulture);
            assignments.Add(new SequencingSensorAssignment(SensorIds[index], "WJ" + ordinal, "IV" + ordinal));
        }

        return new SequencingTopology(assignments, NonSensorIds);
    }

    private static AdmitQueueEntry Admit(
        int second,
        string sensorId) =>
        new(
            At(second),
            SequencingAdmissionSource.SCENARIO_PREPARED,
            sensorId,
            "SCENARIO_PREPARED",
            0,
            Topology());

    private static ValveLimitObserved Valve(int second, bool upper, bool lower) => new(At(second), "IV1", upper, lower);

    private static List<SequencingEvent> Prelude() => new()
    {
        new StartAutoSequence(At(0)),
        Admit(1, "SYN-S01"),
        Admit(2, "SYN-S02"),
        new DispatchHead(At(3), Topology(), PumpReady: true),
    };

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

    private static SequencingState Expect(SequencingTransition transition)
    {
        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        return transition.State;
    }

    private static SequencingState Running() => Expect(Run(Prelude())[^1]);

    // Valve CLOSED, then AdvanceJobPreparation, then BeginCleaning: the Job is CLEANING with water on.
    private static SequencingState Cleaning(SequencingState state, int second)
    {
        state = Expect(SequencingKernel.Apply(state, Valve(second, upper: false, lower: true)));
        state = Expect(SequencingKernel.Apply(state, new AdvanceJobPreparation(At(second + 1))));
        return Expect(SequencingKernel.Apply(state, new BeginCleaning(At(second + 2))));
    }

    private static SequencingActiveJob RequireJob(SequencingState state) =>
        state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");

    private static string[] Codes(SequencingTransition transition) =>
        transition.Records.Select(record => record.Code).ToArray();

    private static SequencingEvidence Find(SequencingTransition transition, SafeReturnStep step) =>
        transition.Records.Single(record => record.Step == step);

    private static string[] Of(params string[] values) => values;

    private static string QueueJson(SequencingState state) =>
        JsonSerializer.Serialize(SequencingKernel.ProjectQueueEntries(state), ContractJson.Options);

    private static SequencingState Raw(SequencingActiveJob? job, bool critical = false, int evidenceSeq = 10)
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
