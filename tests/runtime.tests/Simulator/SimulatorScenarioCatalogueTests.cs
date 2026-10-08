using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b scenario catalogue tests. These tests were written in Arena and are NOT EXECUTED IN ARENA;
/// they are planned for Owner-local execution. They drive every scheduled event through the kernel and check the
/// release, latch, queue and retained-Job outcome of each scenario. No clock, no randomness, synthetic SYN-* only.
/// </summary>
public sealed class SimulatorScenarioCatalogueTests
{
    public static IEnumerable<object[]> AllScenarioIds() =>
        Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_Scenario_Applies_Only_Its_Scheduled_Refusals_With_Contiguous_Evidence(SimulatorScenarioId id)
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(id).Events);

        foreach (var transition in transitions)
        {
            if (transition.Evidence.Code == SequencingCodes.QueueFull)
            {
                Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
                continue;
            }

            Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        }

        var sequence = transitions.SelectMany(transition => transition.Records).Select(record => record.Seq).ToArray();
        for (var index = 1; index < sequence.Length; index++)
        {
            Assert.Equal(sequence[index - 1] + 1, sequence[index]);
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Identical_Scenario_Input_Produces_Byte_Identical_Evidence(SimulatorScenarioId id)
    {
        var first = Run(SimulatorScenarioCatalogue.Get(id).Events);
        var second = Run(SimulatorScenarioCatalogue.Get(id).Events);

        Assert.Equal(
            JsonSerializer.Serialize(first.SelectMany(transition => transition.Records).ToArray(), ContractJson.Options),
            JsonSerializer.Serialize(second.SelectMany(transition => transition.Records).ToArray(), ContractJson.Options));
    }

    [Fact]
    public void Idle_Scenario_Is_The_Initial_Off_State_With_No_Events()
    {
        Assert.Empty(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.IDLE).Events);
        var initial = SequencingKernel.Initial();

        Assert.Equal(AutoSequenceMode.OFF, SequencingKernel.ProjectAutoSequenceMode(initial));
        Assert.Empty(SequencingKernel.ProjectQueueEntries(initial));
        Assert.Equal(0, initial.QueueRevision);
    }

    [Fact]
    public void Normal_Completion_Releases_As_Completed_And_Keeps_The_Queue()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.NORMAL_COMPLETION).Events);
        var final = transitions[^1].State;

        Assert.Null(final.ActiveJob);
        Assert.False(final.CriticalSuspended);
        Assert.Equal(CleaningJobOutcome.COMPLETED, ReleaseOutcome(transitions));
        Assert.Equal(3, final.QueueRevision);
        Assert.Equal(new[] { "SYN-S02" }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Pump_Wait_Then_Ready_Starts_Cleaning_Only_After_Readiness()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.PUMP_WAIT_THEN_READY).Events);

        Assert.Equal(SequencingCodes.PumpReadinessChanged, transitions[4].Evidence.Code);
        Assert.Equal(SequencingCodes.CleaningStarted, transitions[7].Evidence.Code);
        Assert.Equal(CleaningJobOutcome.COMPLETED, ReleaseOutcome(transitions));
        Assert.Null(transitions[^1].State.ActiveJob);
    }

    [Fact]
    public void Explicit_Abort_Releases_As_Aborted()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.EXPLICIT_ABORT).Events);

        Assert.Equal(CleaningJobOutcome.ABORTED, ReleaseOutcome(transitions));
        Assert.Null(transitions[^1].State.ActiveJob);
        Assert.False(transitions[^1].State.CriticalSuspended);
    }

    [Fact]
    public void Execution_Failure_Releases_As_Failed()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.EXECUTION_FAILURE).Events);

        Assert.Equal(CleaningJobOutcome.FAILED, ReleaseOutcome(transitions));
        Assert.Null(transitions[^1].State.ActiveJob);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.PUMP_UNEXPECTED_STOP)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP)]
    public void Pump_Critical_Latches_Releases_As_Aborted_And_Keeps_The_Latch(SimulatorScenarioId id)
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(id).Events);
        var final = transitions[^1].State;

        Assert.Equal(CleaningJobOutcome.ABORTED, ReleaseOutcome(transitions));
        Assert.Null(final.ActiveJob);
        Assert.True(final.CriticalSuspended);
        Assert.Equal(new[] { "SYN-S02" }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Valve_Close_Failure_Retains_The_Job_As_Safe_Return_Failed()
    {
        var final = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.VALVE_CLOSE_FAILURE).Events)[^1].State;

        var job = RequireJob(final);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.ValveCloseNotConfirmed, job.FailureCode);
        Assert.False(job.WaterOutputOn);
    }

    [Fact]
    public void Axis_Standby_Failure_Retains_The_Job_As_Safe_Return_Failed()
    {
        var final = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.AXIS_STANDBY_FAILURE).Events)[^1].State;

        var job = RequireJob(final);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.AxisFault, job.FailureCode);
    }

    [Fact]
    public void Pause_After_Current_Job_Completes_Then_Pauses_With_The_Queue_Kept()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB).Events);
        var final = transitions[^1].State;

        Assert.Equal(CleaningJobOutcome.COMPLETED, ReleaseOutcome(transitions));
        Assert.Null(final.ActiveJob);
        Assert.Equal(AutoSequenceMode.PAUSED, SequencingKernel.ProjectAutoSequenceMode(final));
        Assert.Equal(new[] { "SYN-S02" }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Queue_Capacity_Refuses_The_Ninth_Admission_And_Dispatches_Only_The_Head()
    {
        var transitions = Run(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO).Events);
        var refused = transitions.Single(transition => transition.Outcome == SequencingOutcome.REFUSED);
        var final = transitions[^1].State;

        Assert.Equal(SequencingCodes.QueueFull, refused.Evidence.Code);
        var job = RequireJob(final);
        Assert.Equal("SYN-S01", job.TargetSensorId);
        Assert.Equal(9, final.QueueRevision);
        Assert.Equal(
            new[] { "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08" },
            final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Theory]
    [InlineData("NORMAL_COMPLETION", true, SimulatorScenarioId.NORMAL_COMPLETION)]
    [InlineData("QUEUE_CAPACITY_AND_FIFO", true, SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO)]
    [InlineData("normal_completion", false, SimulatorScenarioId.IDLE)]
    [InlineData("1", false, SimulatorScenarioId.IDLE)]
    [InlineData(" IDLE", false, SimulatorScenarioId.IDLE)]
    [InlineData("", false, SimulatorScenarioId.IDLE)]
    public void Selection_Matches_Exact_Identity_Names_Only(string name, bool expected, SimulatorScenarioId expectedId)
    {
        var matched = SimulatorScenarioCatalogue.TryParse(name, out var id);

        Assert.Equal(expected, matched);
        if (expected)
        {
            Assert.Equal(expectedId, id);
        }
    }

    [Fact]
    public void Selection_Refuses_Null()
    {
        Assert.False(SimulatorScenarioCatalogue.TryParse(null, out _));
    }

    private static List<SequencingTransition> Run(IEnumerable<SequencingEvent> events)
    {
        var state = SequencingKernel.Initial();
        var transitions = new List<SequencingTransition>();
        foreach (var sequencingEvent in events)
        {
            var transition = SequencingKernel.Apply(state, sequencingEvent);
            transitions.Add(transition);
            state = transition.State;
        }

        return transitions;
    }

    private static SequencingActiveJob RequireJob(SequencingState state) =>
        state.ActiveJob ?? throw new InvalidOperationException("An Active Job was expected.");

    private static CleaningJobOutcome? ReleaseOutcome(IEnumerable<SequencingTransition> transitions) =>
        transitions.SelectMany(transition => transition.Records).Single(record => record.Step == SafeReturnStep.SR6).JobOutcome;
}
