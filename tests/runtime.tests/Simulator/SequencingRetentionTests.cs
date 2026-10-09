using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>Frozen outcome and read-only equipment-fault retention for either feedback order.</summary>
public sealed class SequencingRetentionTests
{
    private static IReadOnlyList<SimulatorScenarioStep> StandbyFirst(SimulatorScenarioId scenario)
    {
        var steps = SimulatorRunHarness.ScenarioSet().Get(scenario).Steps.ToList();
        var closeIndex = steps.FindIndex(s => s.Event is ValveSupervisionObserved { LowerDetected: true } or
            ValveSupervisionObserved { TimedOut: true, UpperDetected: false });
        var standbyIndex = steps.FindIndex(s => s.Event is AxisFeedbackObserved { Feedback: AxisFeedbackState.AT_STANDBY });
        var close = (ValveSupervisionObserved)steps[closeIndex].Event;
        var early = steps[closeIndex].Tick;
        var late = steps[standbyIndex].Tick;
        steps[closeIndex] = new(early, new AxisFeedbackObserved(SimulatorRunHarness.At(early), AxisFeedbackState.AT_STANDBY));
        steps[standbyIndex] = new(late, close with { At = SimulatorRunHarness.At(late),
            Pressure = close.Pressure is null ? null : close.Pressure with { At = SimulatorRunHarness.At(late) } });
        return steps;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Normal_Completion_Archives_The_Join_Without_Fabricating_A_Branch_Order(bool standbyFirst)
    {
        var run = standbyFirst ? SimulatorRunHarness.Run(StandbyFirst(SimulatorScenarioId.NORMAL_COMPLETION))
            : SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var outcome = Assert.IsType<JobOutcomeRecord>(run.FinalRetention.LastJobOutcome);
        Assert.Equal("COMPLETED", outcome.Outcome);
        Assert.True(outcome.ValveCloseCommandSeq < outcome.AxisReturnCommandSeq);
        Assert.True(outcome.AxisReturnCommandSeq < outcome.ValveClosedConfirmedSeq);
        Assert.True(outcome.AxisReturnCommandSeq < outcome.StandbyConfirmedSeq);
        Assert.Equal(standbyFirst, outcome.StandbyConfirmedSeq < outcome.ValveClosedConfirmedSeq);
        Assert.True(outcome.ValveClosedConfirmedSeq < outcome.OutcomeSeq);
        Assert.True(outcome.StandbyConfirmedSeq < outcome.OutcomeSeq);
        Assert.True(outcome.OutcomeSeq < outcome.ReleaseSeq);
        Assert.Empty(run.FinalRetention.CurrentSafeReturnTail);
        Assert.Single(run.FinalState.Queue);
        Assert.Equal(3, run.FinalState.QueueRevision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inferred_Close_Archives_A_Null_Lower_Limit_Confirmation_In_Both_Orders(bool standbyFirst)
    {
        var run = standbyFirst ? SimulatorRunHarness.Run(StandbyFirst(SimulatorScenarioId.VALVE_CLOSE_LOW))
            : SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_LOW);
        var outcome = Assert.IsType<JobOutcomeRecord>(run.FinalRetention.LastJobOutcome);
        Assert.Equal("CLOSED_BY_PRESSURE", outcome.ValveCloseResolution);
        Assert.Null(outcome.ValveClosedConfirmedSeq);
        Assert.Contains(outcome.Events, e => e.Step == SafeReturnStep.SR3 && e.Event == "CLOSED_BY_PRESSURE");
        Assert.Equal("COMPLETE_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT", outcome.QualifiedCompletion);
        Assert.Contains("COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT", outcome.QualifiedRemarks!);
        Assert.Contains(run.FinalState.EquipmentFaults, f => f.Diagnosis == "LOWER_LIMIT_SENSOR_FAULT");
        Assert.Null(run.FinalState.ActiveJob);
    }

    [Theory]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_FAILURE, "LEAK_SUSPECTED")]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_HIGH, "NOT_FULLY_CLOSED")]
    public void Unsafe_Valve_Result_Never_Archives_An_Outcome(SimulatorScenarioId scenario, string diagnosis)
    {
        var run = SimulatorRunHarness.Run(StandbyFirst(scenario));
        Assert.Null(run.FinalRetention.LastJobOutcome);
        Assert.Equal(diagnosis, run.FinalState.ActiveJob?.CloseResolution?.ToString());
        Assert.True(run.FinalState.ActiveJob?.AxisStandbyConfirmed);
        Assert.Single(run.FinalState.EquipmentFaults);
        Assert.Single(run.FinalState.Queue);
    }

    [Fact]
    public void Upper_And_Lower_Faults_Are_Distinct_In_The_One_Completed_Outcome()
    {
        var steps = SimulatorRunHarness.ScenarioSet().Get(SimulatorScenarioId.NORMAL_COMPLETION).Steps.ToList();
        var openIndex = steps.FindIndex(s => s.Event is ValveSupervisionObserved { UpperDetected: true });
        var open = (ValveSupervisionObserved)steps[openIndex].Event;
        steps[openIndex] = steps[openIndex] with { Event = open with { UpperDetected = false, TimedOut = true } };
        var closeIndex = steps.FindIndex(s => s.Event is ValveSupervisionObserved { LowerDetected: true });
        var close = (ValveSupervisionObserved)steps[closeIndex].Event;
        steps[closeIndex] = steps[closeIndex] with { Event = close with { LowerDetected = false, TimedOut = true } };
        var run = SimulatorRunHarness.Run(steps);
        var outcome = Assert.IsType<JobOutcomeRecord>(run.FinalRetention.LastJobOutcome);
        Assert.Equal("COMPLETED", outcome.Outcome);
        Assert.Equal("COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS", outcome.QualifiedCompletion);
        Assert.Equal(new[] { "COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT", "COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT" }, outcome.QualifiedRemarks);
        Assert.Equal(2, outcome.EquipmentFaults?.Count);
        Assert.All(outcome.EquipmentFaults!, f => { Assert.True(f.ModalOpen); Assert.True(f.NextDispatchBlocked); });
        Assert.Equal(2, run.FinalState.EquipmentFaults.Count);
    }
}
