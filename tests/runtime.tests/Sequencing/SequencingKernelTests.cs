using System.Globalization;
using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-1 tests for the pure sequencing kernel: GlobalQueue capacity,
/// explicit admission, dispatch-ready FIFO with head-only dispatch, the single
/// Active Job, pump readiness as a waiting gate (not a Queue state), head
/// revalidation removal, critical freeze, pause as AutoSequence state, and
/// deterministic evidence. Every identifier is synthetic (SYN-*); no Production
/// value, threshold, timeout, tag or coordinate appears.
/// </summary>
public sealed class SequencingKernelTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] SensorIds =
        ["SYN-S01", "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08", "SYN-S09", "SYN-S10"];

    private static readonly string[] NonSensorIds = ["SYN-G01"];

    private static readonly string[] ForbiddenQueueVocabulary =
        ["BLOCKED", "HELD", "WAITING_FOR_PUMP", "WAITING_FOR_EQUIPMENT", "EXCLUDED"];

    [Fact]
    public void Q1_Capacity_Is_Eight_And_The_Ninth_Admission_Is_Refused_Atomically()
    {
        var state = SequencingKernel.Initial();
        for (var index = 0; index < 8; index++)
        {
            var admitted = Admit(state, SensorIds[index], index + 1);
            Assert.Equal(SequencingOutcome.APPLIED, admitted.Outcome);
            state = admitted.State;
        }

        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var ninth = Admit(state, SensorIds[8], 9);

        Assert.Equal(SequencingOutcome.REFUSED, ninth.Outcome);
        Assert.Equal(SequencingCodes.QueueFull, ninth.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(ninth.State));
        Assert.Equal(revisionBefore, ninth.State.QueueRevision);
        Assert.Equal(ninth.Evidence.Seq, ninth.State.EvidenceSeq);

        var queueCount = ninth.State.Queue.Count;
        Assert.Equal(QueueSummary.MaxEntries, queueCount);
    }

    [Fact]
    public void Q2_Serialized_Queue_Entries_Carry_No_Forbidden_Waiting_Or_Exclusion_Vocabulary()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Dispatch(state, 3, pumpReady: false).State;
        state = Admit(state, "SYN-S03", 4).State;

        var json = QueueJson(state);

        Assert.True(json.Contains("SYN-S03", StringComparison.Ordinal));
        foreach (var token in ForbiddenQueueVocabulary)
        {
            Assert.False(json.Contains(token, StringComparison.Ordinal), token);
        }
    }

    [Fact]
    public void Q3_Only_Position_One_Dispatches()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Admit(state, "SYN-S03", 3).State;

        var dispatched = Dispatch(state, 4);

        Assert.Equal(SequencingOutcome.APPLIED, dispatched.Outcome);
        Assert.Equal(SequencingCodes.Dispatched, dispatched.Evidence.Code);
        Assert.Equal("SYN-S01", RequireJob(dispatched.State).TargetSensorId);

        var entries = SequencingKernel.ProjectQueueEntries(dispatched.State);
        var remainingCount = entries.Count;
        Assert.Equal(2, remainingCount);
        Assert.Equal(1, entries[0].Position);
        Assert.Equal("SYN-S02", entries[0].SensorId);
        Assert.Equal(2, entries[1].Position);
        Assert.Equal("SYN-S03", entries[1].SensorId);
    }

    [Fact]
    public void Q4_Atomic_Dispatch_Creates_Exactly_One_Job_And_Removes_The_Head()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Admit(state, "SYN-S03", 3).State;

        var dispatched = Dispatch(state, 4);
        var job = RequireJob(dispatched.State);

        Assert.Equal("SYN-JOB-1", job.JobId);
        Assert.Equal("SYN-QE-1", job.SourceEntryId);
        Assert.Equal(2, dispatched.State.NextJobSeq);
        Assert.Equal(4, dispatched.State.QueueRevision);
        Assert.Equal(3, job.QueueRevisionBefore);
        Assert.Equal(4, job.QueueRevisionAfter);
        Assert.Equal(dispatched.Evidence.Seq, job.DispatchEvidenceSeq);
        Assert.False(job.PumpReady); // dispatch boolean is not a measured Pump outlet
        Assert.Equal("WJ1", job.JetId);
        Assert.Equal("IV1", job.ValveId);

        var queueCount = dispatched.State.Queue.Count;
        Assert.Equal(2, queueCount);
    }

    [Fact]
    public void Q5_Duplicate_Admission_Is_A_No_Op()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var duplicate = Admit(state, "SYN-S01", 2);

        Assert.Equal(SequencingOutcome.NO_OP, duplicate.Outcome);
        Assert.Equal(SequencingCodes.AdmissionDuplicateNoOp, duplicate.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(duplicate.State));
        Assert.Equal(revisionBefore, duplicate.State.QueueRevision);
    }

    [Fact]
    public void Q6_Admission_Of_The_Active_Target_Is_Refused()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Dispatch(state, 2).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var refused = Admit(state, "SYN-S01", 3);

        Assert.Equal(SequencingOutcome.REFUSED, refused.Outcome);
        Assert.Equal(SequencingCodes.TargetActive, refused.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(refused.State));
        Assert.Equal(revisionBefore, refused.State.QueueRevision);
    }

    [Fact]
    public void Q7_Critical_Suspension_Freezes_The_Queue_And_Revision()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Critical(state, 3).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var admission = Admit(state, "SYN-S03", 4);
        var dispatch = Dispatch(state, 5);

        Assert.Equal(SequencingOutcome.REFUSED, admission.Outcome);
        Assert.Equal(SequencingCodes.CriticalSuspended, admission.Evidence.Code);
        Assert.Equal(SequencingOutcome.REFUSED, dispatch.Outcome);
        Assert.Equal(SequencingCodes.CriticalSuspended, dispatch.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(dispatch.State));
        Assert.Equal(revisionBefore, dispatch.State.QueueRevision);
        Assert.Null(dispatch.State.ActiveJob);
        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(dispatch.State));
        Assert.Equal(AutoSequenceMode.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceMode(dispatch.State));
    }

    [Fact]
    public void Q8_Structurally_Invalid_Head_Is_Removed_With_No_Job_And_No_Next_Dispatch()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;

        // Equipment eligibility at dispatch no longer assigns SYN-S01; SYN-S02 is still eligible.
        var eligibility = Topology(excludedSensorId: "SYN-S01");
        var transition = Dispatch(state, 3, eligibility);

        Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        Assert.Equal(SequencingCodes.RemovedByEligibility, transition.Evidence.Code);
        Assert.Equal("SYN-QE-1", transition.Evidence.EntryId);
        Assert.Null(transition.Evidence.JobId);
        Assert.Null(transition.State.ActiveJob);
        Assert.Equal(1, transition.State.NextJobSeq);
        Assert.Equal(3, transition.State.QueueRevision);

        var entries = SequencingKernel.ProjectQueueEntries(transition.State);
        var remainingCount = entries.Count;
        Assert.Equal(1, remainingCount);
        Assert.Equal("SYN-S02", entries[0].SensorId);
        Assert.Equal(1, entries[0].Position);
        Assert.Equal(AutoSequenceState.READY_TO_DISPATCH, SequencingKernel.ProjectAutoSequenceState(transition.State));
    }

    [Fact]
    public void Q9_Pump_Not_Ready_Keeps_The_Job_In_The_Waiting_Gate_Without_A_Queue_Waiting_State()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;

        var dispatched = Dispatch(state, 3, pumpReady: false);

        Assert.Equal(SequencingOutcome.APPLIED, dispatched.Outcome);
        Assert.False(RequireJob(dispatched.State).PumpReady);
        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, SequencingKernel.ProjectAutoSequenceState(dispatched.State));
        Assert.Equal(AutoSequenceMode.RUNNING, SequencingKernel.ProjectAutoSequenceMode(dispatched.State));

        var queueJson = QueueJson(dispatched.State);
        foreach (var token in ForbiddenQueueVocabulary)
        {
            Assert.False(queueJson.Contains(token, StringComparison.Ordinal), token);
        }

        var stillWaiting = Pump(dispatched.State, 4, ready: false);
        Assert.Equal(SequencingOutcome.NO_OP, stillWaiting.Outcome);
        Assert.Equal(SequencingCodes.PumpReadinessUnchanged, stillWaiting.Evidence.Code);
        Assert.Equal(AutoSequenceState.PUMP_NOT_READY, SequencingKernel.ProjectAutoSequenceState(stillWaiting.State));

        var booleanOnly = Pump(stillWaiting.State, 5, ready: true);
        Assert.Equal(SequencingOutcome.REFUSED, booleanOnly.Outcome);
        var ready = SequencingKernel.Apply(booleanOnly.State, PumpSample(6));
        Assert.Equal(SequencingOutcome.APPLIED, ready.Outcome);
        Assert.Equal(AutoSequenceState.JOB_ACTIVE, SequencingKernel.ProjectAutoSequenceState(ready.State));
    }

    [Fact]
    public void Q10_A_Second_Dispatch_Is_Refused_While_A_Job_Is_Active()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Dispatch(state, 3).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var second = Dispatch(state, 4);

        Assert.Equal(SequencingOutcome.REFUSED, second.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedJobActive, second.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(second.State));
        Assert.Equal(revisionBefore, second.State.QueueRevision);
        Assert.Equal("SYN-JOB-1", RequireJob(second.State).JobId);
    }

    [Fact]
    public void Q11_Pause_Requested_Cannot_Dispatch()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Dispatch(state, 3).State;
        state = Pause(state, 4).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var dispatch = Dispatch(state, 5);

        Assert.Equal(AutoSequenceMode.PAUSE_REQUESTED, SequencingKernel.ProjectAutoSequenceMode(state));
        Assert.Equal(AutoSequenceState.PAUSE_REQUESTED, SequencingKernel.ProjectAutoSequenceState(state));
        Assert.Equal(SequencingOutcome.REFUSED, dispatch.Outcome);
        Assert.Equal(SequencingCodes.DispatchRefusedPauseRequested, dispatch.Evidence.Code);
        Assert.Equal(queueBefore, QueueJson(dispatch.State));
        Assert.Equal(revisionBefore, dispatch.State.QueueRevision);
    }

    [Fact]
    public void Q12_Pause_With_No_Active_Job_Goes_Directly_To_Paused()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        var queueBefore = QueueJson(state);
        var revisionBefore = state.QueueRevision;

        var paused = Pause(state, 2);

        Assert.Equal(SequencingOutcome.APPLIED, paused.Outcome);
        Assert.Equal(SequencingCodes.Paused, paused.Evidence.Code);
        Assert.Null(paused.State.ActiveJob);
        Assert.Equal(AutoSequenceMode.PAUSED, SequencingKernel.ProjectAutoSequenceMode(paused.State));
        Assert.Equal(AutoSequenceState.PAUSED, SequencingKernel.ProjectAutoSequenceState(paused.State));
        Assert.Equal(queueBefore, QueueJson(paused.State));
        Assert.Equal(revisionBefore, paused.State.QueueRevision);
    }

    [Fact]
    public void Q13_Identical_Input_Produces_Byte_Identical_Evidence_And_Final_State()
    {
        var first = RunDeterministicScript();
        var second = RunDeterministicScript();

        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(first.State, second.State);
        Assert.True(first.Evidence.Contains("DISPATCHED", StringComparison.Ordinal));
        Assert.True(first.Evidence.Contains("JOB_RELEASED", StringComparison.Ordinal));
    }

    [Fact]
    public void Start_Is_Accepted_Only_From_Off()
    {
        var started = Start(SequencingKernel.Initial(), 0);
        Assert.Equal(SequencingOutcome.APPLIED, started.Outcome);
        Assert.Equal(SequencingCodes.AutoSequenceStarted, started.Evidence.Code);

        var again = Start(started.State, 1);
        Assert.Equal(SequencingOutcome.REFUSED, again.Outcome);
        Assert.Equal(SequencingCodes.StartNotOff, again.Evidence.Code);
        Assert.Equal(AutoSequenceMode.RUNNING, SequencingKernel.ProjectAutoSequenceMode(again.State));
    }

    [Fact]
    public void Admission_Refuses_Non_Sensor_Positions_And_Unknown_Sensors()
    {
        var state = SequencingKernel.Initial();

        var nonSensor = Admit(state, "SYN-G01", 1);
        Assert.Equal(SequencingOutcome.REFUSED, nonSensor.Outcome);
        Assert.Equal(SequencingCodes.NotASensor, nonSensor.Evidence.Code);

        var unknown = Admit(state, "SYN-X99", 2);
        Assert.Equal(SequencingOutcome.REFUSED, unknown.Outcome);
        Assert.Equal(SequencingCodes.SensorUnknown, unknown.Evidence.Code);

        var queueCount = unknown.State.Queue.Count;
        Assert.Empty(SequencingKernel.ProjectQueueEntries(unknown.State));
        Assert.Equal(0, queueCount);
    }

    [Fact]
    public void Release_Happens_Only_Through_Kernel_Safe_Return_And_Then_Completes_A_Pending_Pause()
    {
        var state = Start(SequencingKernel.Initial(), 0).State;
        state = Admit(state, "SYN-S01", 1).State;
        state = Admit(state, "SYN-S02", 2).State;
        state = Dispatch(state, 3).State;
        state = Pause(state, 4).State;
        Assert.NotNull(state.ActiveJob);
        Assert.Equal(AutoSequenceMode.PAUSE_REQUESTED, SequencingKernel.ProjectAutoSequenceMode(state));

        var driven = DriveNormalCompletion(state, 5);
        var released = driven[^1];

        Assert.Equal(SequencingOutcome.APPLIED, released.Outcome);
        Assert.Equal(SequencingCodes.JobReleased, released.Evidence.Code);
        Assert.Null(released.State.ActiveJob);
        Assert.Equal(AutoSequenceMode.PAUSED, SequencingKernel.ProjectAutoSequenceMode(released.State));
        Assert.Equal(AutoSequenceState.PAUSED, SequencingKernel.ProjectAutoSequenceState(released.State));

        var remainingCount = released.State.Queue.Count;
        Assert.Equal(1, remainingCount);
        Assert.Equal(3, released.State.QueueRevision);
    }

    // ---- fixtures and helpers (synthetic only) ----

    private static SequencingTopology Topology(string? excludedSensorId = null)
    {
        var assignments = new List<SequencingSensorAssignment>();
        for (var index = 0; index < SensorIds.Length; index++)
        {
            if (string.Equals(SensorIds[index], excludedSensorId, StringComparison.Ordinal))
            {
                continue;
            }

            var ordinal = (index % 8 + 1).ToString(CultureInfo.InvariantCulture);
            assignments.Add(new SequencingSensorAssignment(SensorIds[index], "WJ" + ordinal, "IV" + ordinal));
        }

        return new SequencingTopology(assignments, NonSensorIds);
    }

    private static DateTimeOffset At(int second) => Instant.AddSeconds(second);

    private static SequencingTransition Start(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new StartAutoSequence(At(second)));

    private static SequencingTransition Admit(SequencingState state, string sensorId, int second) =>
        SequencingKernel.Apply(
            state,
            new AdmitQueueEntry(At(second), SequencingAdmissionSource.SCENARIO_PREPARED, sensorId, "SCENARIO_PREPARED", 0, Topology()));

    private static SequencingTransition Dispatch(SequencingState state, int second, bool pumpReady = true) =>
        SequencingKernel.Apply(state, new DispatchHead(At(second), Topology(), pumpReady));

    private static SequencingTransition Dispatch(SequencingState state, int second, SequencingTopology topology) =>
        SequencingKernel.Apply(state, new DispatchHead(At(second), topology, PumpReady: true));

    private static SequencingTransition Pump(SequencingState state, int second, bool ready) =>
        SequencingKernel.Apply(state, new ObservePumpReadiness(At(second), ready));

    private static PumpPressureObserved PumpSample(int second) =>
        new(At(second), new PressureSample(16, PressureQuality.GOOD, false, At(second), PressureSample.PumpOutletSource), new());

    private static SequencingTransition Critical(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new ObservePumpState(At(second), PumpObservation.UNEXPECTED_STOP));

    private static SequencingTransition Pause(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new RequestPause(At(second)));

    /// <summary>
    /// Drives the Active Job through the full normal path (valve, preparation, cleaning,
    /// P1 to P6, completion, Safe Return) to release. Returns every transition, in order.
    /// </summary>
    private static List<SequencingTransition> DriveNormalCompletion(SequencingState state, int second)
    {
        var events = new List<SequencingEvent>
        {
            PumpSample(second),
            new ValveLimitObserved(At(second), "IV1", UpperLimit: false, LowerLimit: true),
            new AdvanceJobPreparation(At(second + 1)),
            new BeginCleaning(At(second + 2)),
            new ExecutionPhaseVerified(At(second + 3), JobPhase.P1),
            new ValveSupervisionObserved(At(second + 4), "IV1", true, false, false,
                new PressureSample(15, PressureQuality.GOOD, false, At(second + 4), PressureSample.ValveOutletSource("IV1")), new()),
        };

        var tick = second + 5;
        foreach (var phase in new[] { JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 })
        {
            events.Add(new ExecutionPhaseVerified(At(tick), phase));
            tick++;
        }

        events.Add(new RequestNormalCompletion(At(tick)));
        tick++;
        events.Add(new ValveSupervisionObserved(At(tick), "IV1", false, true, false,
            new PressureSample(0.5, PressureQuality.GOOD, false, At(tick), PressureSample.ValveOutletSource("IV1")), new()));
        tick++;
        events.Add(new AxisFeedbackObserved(At(tick), AxisFeedbackState.AT_STANDBY));

        var transitions = new List<SequencingTransition>(events.Count);
        foreach (var sequencingEvent in events)
        {
            var transition = SequencingKernel.Apply(state, sequencingEvent);
            Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
            transitions.Add(transition);
            state = transition.State;
        }

        return transitions;
    }

    private static SequencingActiveJob RequireJob(SequencingState state) =>
        state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");

    private static string QueueJson(SequencingState state) =>
        JsonSerializer.Serialize(SequencingKernel.ProjectQueueEntries(state), ContractJson.Options);

    private static (string Evidence, string State) RunDeterministicScript()
    {
        var log = new List<SequencingEvidence>();
        var state = SequencingKernel.Initial();

        state = Step(log, Start(state, 1));
        state = Step(log, Admit(state, "SYN-S01", 2));
        state = Step(log, Admit(state, "SYN-S02", 3));
        state = Step(log, Admit(state, "SYN-S01", 4));
        state = Step(log, Admit(state, "SYN-G01", 5));
        state = Step(log, Dispatch(state, 6, pumpReady: false));
        state = Step(log, Pump(state, 7, ready: false));
        state = Step(log, Pump(state, 8, ready: true)); // refused: boolean cannot replace a sample
        state = Step(log, SequencingKernel.Apply(state, PumpSample(8)));
        state = Step(log, Dispatch(state, 9));
        state = Step(log, Pause(state, 10));
        state = Step(log, Dispatch(state, 11));

        foreach (var transition in DriveNormalCompletion(state, 12))
        {
            state = Step(log, transition);
        }

        state = Step(log, Dispatch(state, 30));
        state = Step(log, Critical(state, 31));
        state = Step(log, Admit(state, "SYN-S03", 32));

        return (JsonSerializer.Serialize(log, ContractJson.Options), JsonSerializer.Serialize(state, ContractJson.Options));
    }

    private static SequencingState Step(List<SequencingEvidence> log, SequencingTransition transition)
    {
        log.AddRange(transition.Records);
        return transition.State;
    }
}
