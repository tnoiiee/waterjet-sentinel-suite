using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>Regression guards for critical Pump and legacy limit observations after the parallel return correction.</summary>
public sealed class SequencingCp3aHardeningTests
{
    [Theory]
    [InlineData(SimulatorScenarioId.PUMP_TRIP)]
    [InlineData(SimulatorScenarioId.PUMP_UNEXPECTED_STOP)]
    public void Critical_Pump_Disables_Water_And_Latch_Survives_Independent_Release(SimulatorScenarioId scenario)
    {
        var run = SimulatorRunHarness.RunScenario(scenario);
        var critical = Assert.Single(run.Transitions,
            t => t.Records.Any(r => r.Code == SequencingCodes.CriticalSuspensionRaised));
        Assert.False(critical.State.ActiveJob?.WaterOutputOn ?? true);
        Assert.True(critical.State.CriticalSuspended);
        Assert.True(run.FinalState.CriticalSuspended);
        Assert.Null(run.FinalState.ActiveJob);
        Assert.Equal(CleaningJobOutcome.ABORTED, SimulatorRunHarness.ReleasedOutcome(run.Transitions));
        Assert.Equal(new[] { SafeReturnStep.SR1, SafeReturnStep.SR2, SafeReturnStep.SR4 },
            critical.Records.Where(r => r.Step is not null).Select(r => r.Step!.Value));
    }

    [Fact]
    public void Limit_Only_Feedback_During_Return_Cannot_Substitute_For_IVn_Pressure()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = set.Get(SimulatorScenarioId.EXPLICIT_ABORT).Steps.ToList();
        var close = steps.FindIndex(s => s.Event is ValveSupervisionObserved);
        var tick = steps[close].Tick;
        steps[close] = new(tick, new ValveLimitObserved(SimulatorRunHarness.At(tick),
            set.Sensors[0].AssignedIsolationValveId, false, true));
        var run = SimulatorRunHarness.Run(steps);
        Assert.NotNull(run.FinalState.ActiveJob);
        Assert.Null(run.FinalRetention.LastJobOutcome);
        Assert.DoesNotContain(run.Transitions.SelectMany(t => t.Records), r => r.Step == SafeReturnStep.SR3);
        Assert.DoesNotContain(run.Transitions.SelectMany(t => t.Records), r => r.Step == SafeReturnStep.SR7);
    }

    [Fact]
    public void Axis_Fault_Does_Not_Release_Job_Or_Reset_The_Queue()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.AXIS_STANDBY_FAILURE);
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, run.FinalState.ActiveJob?.Lifecycle);
        Assert.Null(run.FinalRetention.LastJobOutcome);
        Assert.Single(run.FinalState.Queue);
        Assert.Equal(3, run.FinalState.QueueRevision);
    }
}
