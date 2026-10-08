using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-1 state-integrity tests (Owner correction 2026-10-08). They check
/// the public construction boundary, the read-only queue storage, the invariant
/// guard (Apply refusal and projection exceptions), the counter boundaries and the
/// canonical Mode / critical-latch representation. Invalid states are built only
/// through the internal constructor by reflection, so no public constructor and no
/// InternalsVisibleTo seam is opened. Every identifier is synthetic (SYN-*).
/// </summary>
public sealed class SequencingStateIntegrityTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] SensorIds =
        ["SYN-S01", "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08", "SYN-S09", "SYN-S10"];

    private static readonly string[] NonSensorIds = ["SYN-G01"];

    // The single internal constructor takes eight parameters; the record copy constructor takes one.
    private static readonly ConstructorInfo RawConstructor = typeof(SequencingState)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(constructor => constructor.GetParameters().Length == 8);

    // ---- public-boundary tests ----

    [Fact]
    public void State_Has_No_Public_Constructor()
    {
        Assert.Empty(typeof(SequencingState).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void State_Properties_Cannot_Be_Assigned_Or_Initialized_From_Outside_The_Assembly()
    {
        var properties = typeof(SequencingState).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        Assert.NotEmpty(properties);
        Assert.Empty(properties.Where(property => property.SetMethod is { IsPublic: true }));
    }

    [Fact]
    public void Kernel_State_Queue_Cannot_Be_Mutated_Through_A_Caller_Alias()
    {
        var state = Admit(Start(SequencingKernel.Initial(), 1).State, "SYN-S01", 2).State;
        var queue = state.Queue;

        Assert.Throws<NotSupportedException>(() => { ((IList<SequencingEntry>)queue).Add(Entry(1)); });

        Assert.Single(state.Queue);
        Assert.Equal("SYN-S01", state.Queue[0].SensorId);
    }

    [Fact]
    public void Caller_List_Mutation_After_Construction_Does_Not_Change_The_State()
    {
        var source = new List<SequencingEntry> { Entry(0) };
        var state = Raw(source, queueRevision: 1, evidenceSeq: 1);

        source.Add(Entry(1));
        source.Clear();

        Assert.Single(state.Queue);
        Assert.Single(SequencingKernel.ProjectQueueEntries(state));
    }

    // ---- positive control ----

    [Fact]
    public void Baseline_Valid_Raw_State_Is_Accepted_And_Projects_Job_Active()
    {
        var state = ValidRunningWithJob();

        Assert.Equal(AutoSequenceState.JOB_ACTIVE, SequencingKernel.ProjectAutoSequenceState(state));
        Assert.Equal(AutoSequenceMode.RUNNING, SequencingKernel.ProjectAutoSequenceMode(state));
    }

    // ---- invariant guard: queue ----

    [Fact]
    public void Over_Capacity_State_Is_Refused_And_Not_Projected()
    {
        var entries = Enumerable.Range(0, QueueSummary.MaxEntries + 1).Select(Entry).ToArray();
        var state = Raw(entries, queueRevision: 9, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "QUEUE_OVER_CAPACITY");
    }

    [Fact]
    public void Duplicate_EntryId_State_Is_Rejected()
    {
        var state = Raw(
            new[] { Entry(0), new SequencingEntry("SYN-QE-0", SensorIds[1], "SCENARIO_PREPARED", 0) },
            queueRevision: 2,
            evidenceSeq: 0,
            nextEntrySeq: 2);

        AssertRefusedAndUnprojectable(state, "QUEUE_ENTRY_ID_DUPLICATE");
    }

    [Fact]
    public void Duplicate_SensorId_State_Is_Rejected()
    {
        var state = Raw(
            new[] { Entry(0), new SequencingEntry("SYN-QE-9", SensorIds[0], "SCENARIO_PREPARED", 0) },
            queueRevision: 2,
            evidenceSeq: 0,
            nextEntrySeq: 10);

        AssertRefusedAndUnprojectable(state, "QUEUE_SENSOR_DUPLICATE");
    }

    [Fact]
    public void Null_Queue_Item_Is_Rejected()
    {
        var state = Raw(new SequencingEntry[] { null! }, queueRevision: 1, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "QUEUE_ITEM_NULL");
    }

    [Fact]
    public void Null_Queue_Storage_Is_Rejected_Without_Throwing_From_Apply()
    {
        var state = Raw(null, queueRevision: 0, evidenceSeq: 0);

        var transition = SequencingKernel.Apply(state, new StartAutoSequence(Instant));

        Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
        Assert.Equal(SequencingCodes.StateInvalid, transition.Evidence.Code);
        Assert.Throws<InvalidOperationException>(() => { _ = transition.State.Queue; });
        AssertProjectionThrows(state, "QUEUE_NULL");
    }

    [Fact]
    public void Blank_Entry_Identity_Is_Rejected()
    {
        var state = Raw(new[] { new SequencingEntry(" ", SensorIds[0], "SCENARIO_PREPARED", 0) }, queueRevision: 1, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "QUEUE_ENTRY_FIELD_BLANK");
    }

    [Fact]
    public void Negative_Seconds_Since_Last_Clean_Is_Rejected()
    {
        var state = Raw(new[] { new SequencingEntry("SYN-QE-0", SensorIds[0], "SCENARIO_PREPARED", -1) }, queueRevision: 1, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "QUEUE_ENTRY_SECONDS_NEGATIVE");
    }

    [Fact]
    public void Negative_QueueRevision_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), queueRevision: -1, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "QUEUE_REVISION_NEGATIVE");
    }

    // ---- invariant guard: counters ----

    [Fact]
    public void Exhausted_Evidence_Counter_Fails_Explicitly_Without_Overflow()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), evidenceSeq: int.MaxValue);

        // A state at the boundary is consistent, so projections still work.
        Assert.Equal(AutoSequenceState.OFF, SequencingKernel.ProjectAutoSequenceState(state));

        // No evidence number exists beyond int.MaxValue, so Apply fails explicitly.
        var error = Assert.Throws<InvalidOperationException>(() => SequencingKernel.Apply(state, new StartAutoSequence(Instant)));
        Assert.Contains(SequencingCodes.CounterNotIncrementable, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_Evidence_Counter_Is_Rejected_By_Projection_And_Apply_Fails_Explicitly()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), evidenceSeq: -1);

        AssertProjectionThrows(state, "EVIDENCE_SEQ_NEGATIVE");
        var error = Assert.Throws<InvalidOperationException>(() => SequencingKernel.Apply(state, new StartAutoSequence(Instant)));
        Assert.Contains(SequencingCodes.CounterNotIncrementable, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Zero_Next_Entry_Sequence_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), nextEntrySeq: 0);

        AssertRefusedAndUnprojectable(state, "NEXT_ENTRY_SEQ_NOT_USABLE");
    }

    [Fact]
    public void Zero_Next_Job_Sequence_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), nextJobSeq: 0);

        AssertRefusedAndUnprojectable(state, "NEXT_JOB_SEQ_NOT_USABLE");
    }

    [Fact]
    public void Exhausted_Next_Entry_Sequence_Fails_Admission_Without_Overflow()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.RUNNING, nextEntrySeq: int.MaxValue);

        var error = Assert.Throws<InvalidOperationException>(() => Admit(state, "SYN-S01", 1));
        Assert.Contains(SequencingCodes.CounterNotIncrementable, error.Message, StringComparison.Ordinal);
    }

    // ---- invariant guard: canonical Mode and critical latch ----

    [Fact]
    public void Mode_CRITICAL_SUSPENDED_With_Latch_False_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.CRITICAL_SUSPENDED, critical: false);

        AssertRefusedAndUnprojectable(state, "MODE_CRITICAL_NOT_CANONICAL");
    }

    [Fact]
    public void Mode_CRITICAL_SUSPENDED_With_Latch_True_Is_Rejected_Because_The_Mode_Is_Not_Canonical()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.CRITICAL_SUSPENDED, critical: true);

        AssertRefusedAndUnprojectable(state, "MODE_CRITICAL_NOT_CANONICAL");
    }

    [Fact]
    public void Critical_Latch_With_A_Non_Critical_Mode_Is_Canonical_And_Projects_Critical()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.PAUSED, critical: true, evidenceSeq: 0);

        Assert.Equal(AutoSequenceState.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceState(state));
        Assert.Equal(AutoSequenceMode.CRITICAL_SUSPENDED, SequencingKernel.ProjectAutoSequenceMode(state));
    }

    [Fact]
    public void Off_With_An_Active_Job_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), queueRevision: 1, job: Job(), mode: AutoSequenceMode.OFF, evidenceSeq: 1);

        AssertRefusedAndUnprojectable(state, "MODE_OFF_WITH_JOB");
    }

    [Fact]
    public void Paused_With_An_Active_Job_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), queueRevision: 1, job: Job(), mode: AutoSequenceMode.PAUSED, evidenceSeq: 1);

        AssertRefusedAndUnprojectable(state, "MODE_PAUSED_WITH_JOB");
    }

    [Fact]
    public void Pause_Requested_Without_An_Active_Job_Is_Rejected()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.PAUSE_REQUESTED, evidenceSeq: 0);

        AssertRefusedAndUnprojectable(state, "MODE_PAUSE_REQUESTED_WITHOUT_JOB");
    }

    // ---- invariant guard: Active Job against Queue and pairing ----

    [Fact]
    public void Active_Job_Target_Still_Present_In_The_Queue_Is_Rejected()
    {
        var state = Raw(
            new[] { Entry(8) },
            queueRevision: 2,
            job: Job(target: "SYN-S09", revisionBefore: 0, revisionAfter: 1),
            mode: AutoSequenceMode.RUNNING,
            evidenceSeq: 1,
            nextEntrySeq: 9);

        AssertRefusedAndUnprojectable(state, "JOB_TARGET_STILL_QUEUED");
    }

    [Theory]
    [InlineData("WJ1", "IV2")]
    [InlineData("WJ9", "IV9")]
    [InlineData("WJ2", "WJ2")]
    [InlineData("", "IV1")]
    public void Invalid_Water_Jet_And_Valve_Pairing_Is_Rejected(string jetId, string valveId)
    {
        var state = Raw(
            Array.Empty<SequencingEntry>(),
            queueRevision: 1,
            job: Job(jetId: jetId, valveId: valveId),
            mode: AutoSequenceMode.RUNNING,
            evidenceSeq: 1);

        var violation = jetId.Length == 0 ? "JOB_FIELD_BLANK" : "JOB_PAIRING_INVALID";
        AssertRefusedAndUnprojectable(state, violation);
    }

    [Fact]
    public void Job_Revision_Must_Follow_The_Revision_Before_Dispatch()
    {
        var state = Raw(
            Array.Empty<SequencingEntry>(),
            queueRevision: 5,
            job: Job(revisionBefore: 0, revisionAfter: 3),
            mode: AutoSequenceMode.RUNNING,
            evidenceSeq: 1);

        AssertRefusedAndUnprojectable(state, "JOB_REVISION_INVALID");
    }

    [Fact]
    public void Job_Dispatch_Sequence_Must_Not_Exceed_The_Evidence_Sequence()
    {
        var state = Raw(
            Array.Empty<SequencingEntry>(),
            queueRevision: 1,
            job: Job(dispatchSeq: 4),
            mode: AutoSequenceMode.RUNNING,
            evidenceSeq: 1);

        AssertRefusedAndUnprojectable(state, "JOB_DISPATCH_SEQ_INVALID");
    }

    // ---- projection boundary ----

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void Undefined_Mode_Never_Projects_As_A_Running_State(int rawMode)
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: (AutoSequenceMode)rawMode, evidenceSeq: 0);

        AssertProjectionThrows(state, "MODE_UNDEFINED");
        AssertProjectionModeThrows(state, "MODE_UNDEFINED");
        Assert.Equal(SequencingOutcome.REFUSED, SequencingKernel.Apply(state, new StartAutoSequence(Instant)).Outcome);
    }

    [Fact]
    public void Invalid_Critical_State_Never_Projects_As_Ready_Or_Running()
    {
        var state = Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.CRITICAL_SUSPENDED, critical: false, evidenceSeq: 0);

        AssertProjectionThrows(state, "MODE_CRITICAL_NOT_CANONICAL");
    }

    [Fact]
    public void Mode_And_State_Projections_Agree_For_Every_Accepted_State()
    {
        var states = new List<SequencingState>();
        var state = SequencingKernel.Initial();
        states.Add(state);

        state = Step(states, Start(state, 1));
        state = Step(states, Admit(state, "SYN-S01", 2));
        state = Step(states, Admit(state, "SYN-S02", 3));
        state = Step(states, Dispatch(state, 4, pumpReady: false));
        state = Step(states, Pump(state, 5, ready: true));
        state = Step(states, Pause(state, 6));
        var job = state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");
        state = Step(states, Release(state, 7, job.DispatchEvidenceSeq + 1));
        state = Step(states, Critical(state, 8));
        state = Step(states, Start(state, 9));
        states.Add(Raw(Array.Empty<SequencingEntry>(), mode: AutoSequenceMode.PAUSED, critical: true, evidenceSeq: 0));

        foreach (var accepted in states)
        {
            AssertProjectionsAgree(accepted);
        }
    }

    // ---- determinism of the refusal path ----

    [Fact]
    public void Identical_Invalid_Input_Produces_Byte_Identical_Refusal_Evidence_And_State()
    {
        var entries = Enumerable.Range(0, QueueSummary.MaxEntries + 1).Select(Entry).ToArray();

        var first = SequencingKernel.Apply(Raw(entries, queueRevision: 9, evidenceSeq: 0), new DispatchHead(Instant, Topology(), PumpReady: true));
        var second = SequencingKernel.Apply(Raw(entries, queueRevision: 9, evidenceSeq: 0), new DispatchHead(Instant, Topology(), PumpReady: true));

        Assert.Equal(
            JsonSerializer.Serialize(first.Evidence, ContractJson.Options),
            JsonSerializer.Serialize(second.Evidence, ContractJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(first.State, ContractJson.Options),
            JsonSerializer.Serialize(second.State, ContractJson.Options));
    }

    // ---- helpers (synthetic only) ----

    private static void AssertRefusedAndUnprojectable(SequencingState state, string violation)
    {
        var transition = SequencingKernel.Apply(state, new StartAutoSequence(Instant));

        Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
        Assert.Equal(SequencingCodes.StateInvalid, transition.Evidence.Code);
        Assert.Equal(state.EvidenceSeq + 1, transition.Evidence.Seq);
        Assert.Equal(transition.Evidence.Seq, transition.State.EvidenceSeq);
        Assert.Equal(state.QueueRevision, transition.State.QueueRevision);
        Assert.Equal(state.Mode, transition.State.Mode);
        Assert.Equal(state.CriticalSuspended, transition.State.CriticalSuspended);
        Assert.Equal(state.ActiveJob, transition.State.ActiveJob);

        AssertProjectionThrows(state, violation);
        AssertProjectionModeThrows(state, violation);
    }

    private static void AssertProjectionThrows(SequencingState state, string violation)
    {
        var queueError = Assert.Throws<InvalidOperationException>(() => { SequencingKernel.ProjectQueueEntries(state); });
        Assert.Equal(SequencingCodes.StateInvalid + ": " + violation, queueError.Message);

        var stateError = Assert.Throws<InvalidOperationException>(() => { SequencingKernel.ProjectAutoSequenceState(state); });
        Assert.Equal(SequencingCodes.StateInvalid + ": " + violation, stateError.Message);
    }

    private static void AssertProjectionModeThrows(SequencingState state, string violation)
    {
        var modeError = Assert.Throws<InvalidOperationException>(() => { SequencingKernel.ProjectAutoSequenceMode(state); });
        Assert.Equal(SequencingCodes.StateInvalid + ": " + violation, modeError.Message);
    }

    private static void AssertProjectionsAgree(SequencingState state)
    {
        var mode = SequencingKernel.ProjectAutoSequenceMode(state);
        var projected = SequencingKernel.ProjectAutoSequenceState(state);

        if (mode == AutoSequenceMode.RUNNING)
        {
            var runningStates = new[]
            {
                AutoSequenceState.JOB_ACTIVE,
                AutoSequenceState.PUMP_NOT_READY,
                AutoSequenceState.QUEUE_EMPTY,
                AutoSequenceState.READY_TO_DISPATCH,
            };
            Assert.True(runningStates.Contains(projected));
            return;
        }

        var expected = mode switch
        {
            AutoSequenceMode.CRITICAL_SUSPENDED => AutoSequenceState.CRITICAL_SUSPENDED,
            AutoSequenceMode.OFF => AutoSequenceState.OFF,
            AutoSequenceMode.PAUSE_REQUESTED => AutoSequenceState.PAUSE_REQUESTED,
            AutoSequenceMode.PAUSED => AutoSequenceState.PAUSED,
            _ => throw new InvalidOperationException("Unexpected projected mode in an accepted state."),
        };
        Assert.Equal(expected, projected);
    }

    private static SequencingState Raw(
        IReadOnlyList<SequencingEntry>? queue,
        int queueRevision = 0,
        SequencingActiveJob? job = null,
        AutoSequenceMode mode = AutoSequenceMode.OFF,
        bool critical = false,
        int evidenceSeq = 0,
        int nextEntrySeq = 1,
        int nextJobSeq = 1)
    {
        var built = RawConstructor.Invoke(new object?[]
        {
            queue,
            queueRevision,
            job,
            mode,
            critical,
            evidenceSeq,
            nextEntrySeq,
            nextJobSeq,
        });
        return (SequencingState)built!;
    }

    private static SequencingState ValidRunningWithJob() =>
        Raw(Array.Empty<SequencingEntry>(), queueRevision: 1, job: Job(), mode: AutoSequenceMode.RUNNING, evidenceSeq: 1);

    private static SequencingActiveJob Job(
        string target = "SYN-S09",
        string jetId = "WJ1",
        string valveId = "IV1",
        int dispatchSeq = 1,
        int revisionBefore = 0,
        int revisionAfter = 1) =>
        new(
            "SYN-JOB-1",
            "SYN-DSP-1",
            target,
            jetId,
            valveId,
            "SYN-QE-0",
            dispatchSeq,
            true,
            Instant,
            revisionBefore,
            revisionAfter);

    private static SequencingEntry Entry(int index) =>
        new(
            "SYN-QE-" + index.ToString(CultureInfo.InvariantCulture),
            SensorIds[index],
            "SCENARIO_PREPARED",
            0);

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

    private static DateTimeOffset At(int second) => Instant.AddSeconds(second);

    private static SequencingTransition Start(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new StartAutoSequence(At(second)));

    private static SequencingTransition Admit(SequencingState state, string sensorId, int second) =>
        SequencingKernel.Apply(
            state,
            new AdmitQueueEntry(At(second), SequencingAdmissionSource.SCENARIO_PREPARED, sensorId, "SCENARIO_PREPARED", 0, Topology()));

    private static SequencingTransition Dispatch(SequencingState state, int second, bool pumpReady) =>
        SequencingKernel.Apply(state, new DispatchHead(At(second), Topology(), pumpReady));

    private static SequencingTransition Pump(SequencingState state, int second, bool ready) =>
        SequencingKernel.Apply(state, new ObservePumpReadiness(At(second), ready));

    private static SequencingTransition Critical(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new RaiseCriticalSuspension(At(second)));

    private static SequencingTransition Pause(SequencingState state, int second) =>
        SequencingKernel.Apply(state, new RequestPause(At(second)));

    private static SequencingTransition Release(SequencingState state, int second, int releaseSeq)
    {
        var job = state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");
        var evidence = new SafeReturnReleaseEvidence(job.JobId, releaseSeq, OutcomeRecorded: true, SafeReturnComplete: true);
        return SequencingKernel.Apply(state, new ReleaseActiveJob(At(second), evidence));
    }

    private static SequencingState Step(List<SequencingState> states, SequencingTransition transition)
    {
        states.Add(transition.State);
        return transition.State;
    }
}
