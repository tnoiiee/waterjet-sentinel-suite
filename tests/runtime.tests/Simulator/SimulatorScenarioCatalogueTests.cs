using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>SIMULATOR catalogue paths for measured readiness, P1–P6, close and independent Axis return.</summary>
public sealed class SimulatorScenarioCatalogueTests
{
    public static IEnumerable<object[]> AllIds() => Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllIds))]
    public void All_Schedules_Are_Deterministic_And_Strictly_Ordered(SimulatorScenarioId id)
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = set.Get(id).Steps;
        for (var i = 1; i < steps.Count; i++) Assert.True(steps[i - 1].Tick < steps[i].Tick);
        var first = SimulatorRunHarness.RunScenario(id);
        var again = SimulatorRunHarness.RunScenario(id);
        Assert.Equal(SimulatorRunHarness.Json(first.Transitions), SimulatorRunHarness.Json(again.Transitions));
        Assert.Equal(SimulatorRunHarness.Json(first.FinalRetention.LastJobOutcome),
            SimulatorRunHarness.Json(again.FinalRetention.LastJobOutcome));
    }

    [Fact]
    public void Catalogue_Uses_Canonical_Sensor_To_WJn_IVn_Assignment_And_Separate_Transmitter()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        Assert.Equal(Enum.GetValues<SimulatorScenarioId>(), set.Scenarios.Select(s => s.Id));
        foreach (var sensor in set.Sensors)
        {
            var assigned = SequencingTopology.FindAssignment(set.Topology, sensor.SensorId);
            Assert.Equal(sensor.AssignedWaterJetId, assigned?.JetId);
            Assert.Equal(sensor.AssignedIsolationValveId, assigned?.ValveId);
        }
        foreach (var scenario in set.Scenarios)
        {
            foreach (var step in scenario.Steps)
            {
                if (step.Event is PumpPressureObserved pump)
                    Assert.Equal(PressureSample.PumpOutletSource, pump.Pressure?.SourceId);
                if (step.Event is ValveSupervisionObserved valve)
                    Assert.Equal(PressureSample.ValveOutletSource(valve.ValveId), valve.Pressure?.SourceId);
                Assert.DoesNotContain("Reset", step.Event.GetType().Name);
                Assert.DoesNotContain("Acknowledge", step.Event.GetType().Name);
            }
        }
    }

    [Fact]
    public void Normal_Completion_Traverses_Measured_Pump_P1_Through_P6_And_Both_Command_Legs()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var events = run.Transitions.SelectMany(t => t.Records).ToArray();
        Assert.Contains(events, e => e.Code == SequencingCodes.KindPumpPressureObserved);
        Assert.Contains(events, e => e.Code == SequencingCodes.IntentValveOpen);
        Assert.Equal(new[] { JobPhase.P1, JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 },
            events.Where(e => e.Code == SequencingCodes.PhaseVerified).Select(e => e.Phase!.Value));
        Assert.Equal(new[] { SafeReturnStep.SR1, SafeReturnStep.SR2, SafeReturnStep.SR4, SafeReturnStep.SR3,
            SafeReturnStep.SR5, SafeReturnStep.SR6, SafeReturnStep.SR7 },
            events.Where(e => e.Step is not null).Select(e => e.Step!.Value));
        Assert.Equal(CleaningJobOutcome.COMPLETED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(run.FinalState.ActiveJob);
        Assert.Single(run.FinalState.Queue);
    }

    [Fact]
    public void Pump_Wait_Requires_Its_Measured_Outlet_Before_Begin_And_Leaves_Queue_Alone()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_WAIT_THEN_READY);
        var dispatch = run.Steps.ToList().FindIndex(s => s.Event is DispatchHead);
        var pump = run.Steps.ToList().FindIndex(s => s.Event is PumpPressureObserved);
        var begin = run.Steps.ToList().FindIndex(s => s.Event is BeginCleaning);
        Assert.True(dispatch < pump && pump < begin);
        Assert.False(run.Transitions[dispatch].State.ActiveJob?.PumpReady ?? true);
        Assert.True(run.Transitions[pump].State.ActiveJob?.PumpReady ?? false);
        Assert.Null(run.FinalState.ActiveJob);
        Assert.Equal(3, run.FinalState.QueueRevision);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.EXPLICIT_ABORT, CleaningJobOutcome.ABORTED)]
    [InlineData(SimulatorScenarioId.EXECUTION_FAILURE, CleaningJobOutcome.FAILED)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP, CleaningJobOutcome.ABORTED)]
    public void Other_Release_Paths_Still_Join_The_Two_Legs(SimulatorScenarioId id, CleaningJobOutcome expected)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        Assert.Equal(expected, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Null(run.FinalState.ActiveJob);
        Assert.Single(run.FinalState.Queue);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_FAILURE, ValveCloseResolution.LEAK_SUSPECTED)]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_HIGH, ValveCloseResolution.NOT_FULLY_CLOSED)]
    public void Unsafe_Close_Does_Not_Finalize_Or_Release(SimulatorScenarioId id, ValveCloseResolution expected)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        Assert.Equal(expected, run.FinalState.ActiveJob?.CloseResolution);
        Assert.True(run.FinalState.ActiveJob?.AxisStandbyConfirmed ?? false);
        Assert.Null(run.FinalRetention.LastJobOutcome);
        Assert.Single(run.FinalState.EquipmentFaults);
    }

    [Fact]
    public void Capacity_Still_Refuses_Ninth_And_Only_Dispatches_Head()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO);
        Assert.Single(run.Transitions, t => t.Evidence.Code == SequencingCodes.QueueFull);
        Assert.Equal(set.Sensors[0].SensorId, run.FinalState.ActiveJob?.TargetSensorId);
        Assert.Equal(9, run.FinalState.QueueRevision);
        Assert.Equal(7, run.FinalState.Queue.Count);
    }
}
