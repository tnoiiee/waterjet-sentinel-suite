using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b scenario catalogue tests. NOT EXECUTED IN ARENA: Owner-local validation required.
/// The catalogue is bound to the canonical Runtime Sensor set. These tests drive every schedule through the kernel and
/// check canonical identity, refusals, release outcomes and evidence. No clock is read and nothing is random.
/// </summary>
public sealed class SimulatorScenarioCatalogueTests
{
    private const string PlaceholderPrefix = "SYN-S";

    public static IEnumerable<object[]> AllScenarioIds() =>
        Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Fact]
    public void Catalogue_Holds_Exactly_Eleven_Scenarios_In_Catalogue_Order()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var ids = set.Scenarios.Select(scenario => scenario.Id).ToArray();

        Assert.Equal(Enum.GetValues<SimulatorScenarioId>(), ids);
    }

    [Fact]
    public void Scenario_Sensors_Are_The_Canonical_Runtime_Sensors_In_ScanOrder()
    {
        var canonical = SimulatorRunHarness.CanonicalSensors();
        var set = SimulatorScenarioCatalogue.Create(canonical);
        var expected = canonical.OrderBy(sensor => sensor.ScanOrder).Select(sensor => sensor.SensorId).ToArray();

        Assert.Equal(expected, set.Sensors.Select(sensor => sensor.SensorId).ToArray());
    }

    [Fact]
    public void Create_Orders_The_Set_By_ScanOrder_Whatever_The_Input_Order()
    {
        var reversed = SimulatorRunHarness.CanonicalSensors().OrderByDescending(sensor => sensor.ScanOrder).ToArray();
        var set = SimulatorScenarioCatalogue.Create(reversed);
        var lowest = SimulatorRunHarness.CanonicalSensors().OrderBy(sensor => sensor.ScanOrder).First();

        Assert.Equal(lowest.SensorId, set.Sensors[0].SensorId);
        Assert.Equal(lowest.SensorId, set.Topology.SensorAssignments[0].SensorId);
    }

    [Fact]
    public void Create_Refuses_A_Canonical_Set_Smaller_Than_The_Capacity_Scenario_Needs()
    {
        var tooFew = SimulatorRunHarness.CanonicalSensors()
            .Take(SimulatorScenarioCatalogue.RequiredSensorCount - 1)
            .ToArray();

        Assert.Throws<ArgumentException>(() => SimulatorScenarioCatalogue.Create(tooFew));
    }

    [Fact]
    public void Topology_Pairs_Every_Canonical_Sensor_With_Its_Canonical_WaterJet_And_Valve()
    {
        var canonical = SimulatorRunHarness.CanonicalSensors();
        var set = SimulatorRunHarness.ScenarioSet();

        Assert.Equal(canonical.Count, set.Topology.SensorAssignments.Count);
        foreach (var record in canonical)
        {
            var assignment = SimulatorRunHarness.Required(SequencingTopology.FindAssignment(set.Topology, record.SensorId));
            Assert.Equal(record.AssignedWaterJetId, assignment.JetId);
            Assert.Equal(record.AssignedIsolationValveId, assignment.ValveId);
        }
    }

    [Fact]
    public void Every_Admitted_Sensor_Is_A_Canonical_Runtime_Sensor_With_Its_Canonical_Pairing()
    {
        var canonicalIds = SimulatorRunHarness.CanonicalSensors().Select(sensor => sensor.SensorId).ToHashSet(StringComparer.Ordinal);
        var set = SimulatorRunHarness.ScenarioSet();

        foreach (var scenario in set.Scenarios)
        {
            foreach (var step in scenario.Steps)
            {
                if (step.Event is AdmitQueueEntry admit)
                {
                    Assert.Contains(admit.SensorId, canonicalIds);
                    Assert.NotNull(SequencingTopology.FindAssignment(admit.Topology, admit.SensorId));
                }
            }
        }
    }

    [Fact]
    public void Primary_And_Second_Targets_Are_The_First_Two_Canonical_Sensors_In_ScanOrder()
    {
        var ordered = SimulatorRunHarness.CanonicalSensors().OrderBy(sensor => sensor.ScanOrder).ToArray();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var admissions = run.Steps.Where(step => step.Event is AdmitQueueEntry).ToArray();

        Assert.Equal(ordered[0].SensorId, ((AdmitQueueEntry)admissions[0].Event).SensorId);
        Assert.Equal(ordered[1].SensorId, ((AdmitQueueEntry)admissions[1].Event).SensorId);
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void No_Scenario_Sensor_Is_A_SYN_S_Placeholder(SimulatorScenarioId id)
    {
        var scenario = SimulatorRunHarness.ScenarioSet().Get(id);

        foreach (var step in scenario.Steps)
        {
            if (step.Event is AdmitQueueEntry admit)
            {
                Assert.False(admit.SensorId.StartsWith(PlaceholderPrefix, StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Idle_Scenario_Has_No_Steps_And_The_Initial_State_Is_Off_With_An_Empty_Queue()
    {
        var idle = SimulatorRunHarness.ScenarioSet().Get(SimulatorScenarioId.IDLE);
        var initial = SequencingKernel.Initial();

        Assert.Empty(idle.Steps);
        Assert.Equal(AutoSequenceMode.OFF, SequencingKernel.ProjectAutoSequenceMode(initial));
        Assert.Empty(SequencingKernel.ProjectQueueEntries(initial));
        Assert.Equal(0, initial.QueueRevision);
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_Scenario_Schedule_Has_Strictly_Increasing_Ticks(SimulatorScenarioId id)
    {
        var steps = SimulatorRunHarness.ScenarioSet().Get(id).Steps;

        for (var index = 1; index < steps.Count; index++)
        {
            Assert.True(steps[index - 1].Tick < steps[index].Tick);
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_Scenario_Applies_Only_Its_Scheduled_Refusals_With_Contiguous_Evidence(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);

        foreach (var transition in run.Transitions)
        {
            if (transition.Evidence.Code == SequencingCodes.QueueFull)
            {
                Assert.Equal(SequencingOutcome.REFUSED, transition.Outcome);
                continue;
            }

            Assert.Equal(SequencingOutcome.APPLIED, transition.Outcome);
        }

        var sequence = run.Transitions
            .SelectMany(transition => transition.Records)
            .Select(record => record.Seq)
            .ToArray();
        for (var index = 1; index < sequence.Length; index++)
        {
            Assert.Equal(sequence[index - 1] + 1, sequence[index]);
        }
    }

    [Fact]
    public void Queue_Capacity_Is_The_Only_Scenario_That_Refuses_An_Admission()
    {
        var refusals = Enum.GetValues<SimulatorScenarioId>()
            .Where(id => SimulatorRunHarness.RunScenario(id).Transitions
                .Any(transition => transition.Outcome == SequencingOutcome.REFUSED))
            .ToArray();

        Assert.Equal(new[] { SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO }, refusals);
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Identical_Scenario_Input_Produces_Byte_Identical_Evidence(SimulatorScenarioId id)
    {
        var first = SimulatorRunHarness.RunScenario(id);
        var second = SimulatorRunHarness.RunScenario(id);

        Assert.Equal(
            SimulatorRunHarness.Json(first.Transitions.SelectMany(transition => transition.Records).ToArray()),
            SimulatorRunHarness.Json(second.Transitions.SelectMany(transition => transition.Records).ToArray()));
    }

    [Fact]
    public void Normal_Completion_Releases_As_Completed_And_Keeps_The_Second_Sensor_Queued()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var final = run.FinalState;

        Assert.Null(final.ActiveJob);
        Assert.False(final.CriticalSuspended);
        Assert.Equal(CleaningJobOutcome.COMPLETED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Equal(3, final.QueueRevision);
        Assert.Equal(new[] { set.Sensors[1].SensorId }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Pump_Wait_Then_Ready_Starts_Cleaning_Only_After_Readiness()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_WAIT_THEN_READY);

        Assert.Equal(SequencingCodes.PumpReadinessChanged, run.TransitionAt(4).Evidence.Code);
        Assert.Equal(SequencingCodes.CleaningStarted, run.TransitionAt(7).Evidence.Code);
        Assert.Equal(CleaningJobOutcome.COMPLETED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(run.FinalState.ActiveJob);
    }

    [Fact]
    public void Explicit_Abort_Releases_As_Aborted_Without_A_Latch()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.EXPLICIT_ABORT);

        Assert.Equal(CleaningJobOutcome.ABORTED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(run.FinalState.ActiveJob);
        Assert.False(run.FinalState.CriticalSuspended);
    }

    [Fact]
    public void Execution_Failure_Releases_As_Failed()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.EXECUTION_FAILURE);

        Assert.Equal(CleaningJobOutcome.FAILED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(run.FinalState.ActiveJob);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.PUMP_UNEXPECTED_STOP)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP)]
    public void Pump_Critical_Latches_Releases_As_Aborted_And_Keeps_The_Latch(SimulatorScenarioId id)
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(id);
        var final = run.FinalState;

        Assert.Equal(CleaningJobOutcome.ABORTED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(final.ActiveJob);
        Assert.True(final.CriticalSuspended);
        Assert.Equal(new[] { set.Sensors[1].SensorId }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Valve_Close_Failure_Retains_The_Job_As_Safe_Return_Failed()
    {
        var final = SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_FAILURE).FinalState;
        var job = SimulatorRunHarness.Required(final.ActiveJob);

        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.ValveCloseNotConfirmed, job.FailureCode);
        Assert.False(job.WaterOutputOn);
    }

    [Fact]
    public void Axis_Standby_Failure_Retains_The_Job_As_Safe_Return_Failed()
    {
        var final = SimulatorRunHarness.RunScenario(SimulatorScenarioId.AXIS_STANDBY_FAILURE).FinalState;
        var job = SimulatorRunHarness.Required(final.ActiveJob);

        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);
        Assert.Equal(SequencingCodes.AxisFault, job.FailureCode);
    }

    [Fact]
    public void Pause_After_Current_Job_Completes_Then_Pauses_With_The_Queue_Kept()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB);
        var final = run.FinalState;

        Assert.Equal(CleaningJobOutcome.COMPLETED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(final.ActiveJob);
        Assert.Equal(AutoSequenceMode.PAUSED, SequencingKernel.ProjectAutoSequenceMode(final));
        Assert.Equal(new[] { set.Sensors[1].SensorId }, final.Queue.Select(entry => entry.SensorId).ToArray());
    }

    [Fact]
    public void Queue_Capacity_Refuses_The_Ninth_Admission_And_Dispatches_Only_The_Head()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO);
        var refused = run.Transitions.Single(transition => transition.Outcome == SequencingOutcome.REFUSED);
        var job = SimulatorRunHarness.Required(run.FinalState.ActiveJob);
        var expectedQueue = set.Sensors
            .Skip(1)
            .Take(QueueSummary.MaxEntries - 1)
            .Select(sensor => sensor.SensorId)
            .ToArray();

        Assert.Equal(SequencingCodes.QueueFull, refused.Evidence.Code);
        Assert.Equal(set.Sensors[0].SensorId, job.TargetSensorId);
        Assert.Equal(9, run.FinalState.QueueRevision);
        Assert.Equal(expectedQueue, run.FinalState.Queue.Select(entry => entry.SensorId).ToArray());
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
        var matched = SimulatorScenarioCatalogue.TryParse(null, out _);

        Assert.False(matched);
    }

    [Fact]
    public void No_Scenario_Step_Acknowledges_Clears_Or_Resets_A_Condition()
    {
        var set = SimulatorRunHarness.ScenarioSet();

        foreach (var scenario in set.Scenarios)
        {
            foreach (var step in scenario.Steps)
            {
                var kind = step.Event.GetType().Name;
                Assert.False(kind.Contains("Acknowledge", StringComparison.Ordinal));
                Assert.False(kind.Contains("Clear", StringComparison.Ordinal));
                Assert.False(kind.Contains("Reset", StringComparison.Ordinal));
            }
        }
    }
}
