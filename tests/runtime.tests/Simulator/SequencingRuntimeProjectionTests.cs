using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>Read-only wire projection of measured Pump/paired Valve and parallel Safe Return evidence.</summary>
public sealed class SequencingRuntimeProjectionTests
{
    public static IEnumerable<object[]> Scenarios() => Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_Applied_Step_Projects_A_Deterministic_Valid_Revision(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        Assert.Equal(run.Transitions.Count(t => t.Outcome == SequencingOutcome.APPLIED), run.Candidates.Count);
        var previous = run.Initial;
        foreach (var candidate in run.Candidates)
        {
            Assert.Equal(previous.Revision + 1, candidate.Revision);
            Assert.True(RuntimeStateInvariants.TryValidate(candidate, out var code, out var detail), $"{code}: {detail}");
            var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
            var replay = RuntimeDeltaApply.Apply(previous, delta);
            Assert.True(replay.Applied, replay.Reason);
            Assert.Equal(SimulatorRunHarness.Sections(candidate), SimulatorRunHarness.Sections(replay.State!));
            previous = candidate;
        }
    }

    [Fact]
    public void Pump_And_Paired_Valve_Are_Separate_Measured_Sources_In_The_Active_Job_And_Delta()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var pumpStep = run.Steps.Single(s => s.Event is PumpPressureObserved);
        var pump = SimulatorRunHarness.Required(run.CandidateAt(pumpStep.Tick).ActiveJob);
        Assert.Equal("PUMP_OUTLET", pump.PumpPressureSourceId);
        Assert.Equal(16.0, pump.PumpOutletPressureBar);
        Assert.Equal(15.0, pump.PumpReadySetpointBar);
        Assert.True(pump.PumpPressureInputValid);
        Assert.Null(pump.ValveOutletPressureBar);
        Assert.Equal(16.0, run.CandidateAt(pumpStep.Tick).Pump.Pressure);
        var openStep = run.Steps.First(s => s.Event is ValveSupervisionObserved { UpperDetected: true });
        var opened = SimulatorRunHarness.Required(run.CandidateAt(openStep.Tick).ActiveJob);
        Assert.Equal(opened.ValveId + "_OUTLET", opened.ValvePressureSourceId);
        Assert.Equal(15.0, opened.ValveOutletPressureBar);
        Assert.Equal(16.0, opened.PumpOutletPressureBar);
        Assert.Equal("OPEN_CONFIRMED", opened.ValveOpenResolution);
        var delta = RuntimeDeltaProjector.ProjectCandidate(run.PreviousTo(openStep.Tick), run.CandidateAt(openStep.Tick));
        Assert.Equal(opened.ValvePressureSourceId, delta.ActiveJob.Present.ValvePressureSourceId);
    }

    [Fact]
    public void Safe_Return_Commands_Axis_Before_Either_Branch_Reports_Feedback()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var commandStep = run.Steps.Single(s => s.Event is RequestNormalCompletion);
        var active = SimulatorRunHarness.Required(run.CandidateAt(commandStep.Tick).ActiveJob);
        var sr = SimulatorRunHarness.Required(active.SafeReturn);
        Assert.Equal(SafeReturnStep.SR4, sr.Step);
        Assert.True(sr.Valve.CommandSeq < sr.Axis.CommandSeq);
        Assert.Null(sr.Valve.FeedbackSeq);
        Assert.Null(sr.Axis.StandbySeq);
        Assert.Equal("CLOSE_COMMANDED", sr.Valve.Command);
        Assert.Equal("RETURN_COMMANDED", sr.Axis.Command);
    }

    [Fact]
    public void Pressure_Inference_Projects_No_Lower_Limit_Confirmation_But_Retains_The_Fault()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_LOW);
        var closeStep = run.Steps.Single(s => s.Event is ValveSupervisionObserved { TimedOut: true });
        var active = SimulatorRunHarness.Required(run.CandidateAt(closeStep.Tick).ActiveJob);
        var sr = SimulatorRunHarness.Required(active.SafeReturn);
        Assert.Equal("CLOSED_BY_PRESSURE", sr.Valve.Resolution);
        Assert.Equal("NOT_CONFIRMED", sr.Valve.Feedback);
        Assert.Null(sr.Valve.FeedbackSeq);
        Assert.True(sr.Axis.CommandSeq < sr.Events.Single(e => e.Step == SafeReturnStep.SR3).Seq);
        var final = run.Candidates[^1];
        Assert.Null(final.ActiveJob);
        Assert.Equal("COMPLETE_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT", final.Sequence.LastJobOutcome?.QualifiedCompletion);
        Assert.Single(final.Sequence.EquipmentFaults!);
    }

    [Fact]
    public void Unsafe_Close_And_Axis_Standby_Keep_The_Job_And_No_Last_Outcome()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.VALVE_CLOSE_FAILURE);
        var final = run.Candidates[^1];
        var active = SimulatorRunHarness.Required(final.ActiveJob);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, active.Lifecycle);
        Assert.Equal("LEAK_SUSPECTED", active.SafeReturn?.Valve.Resolution);
        Assert.Equal("STANDBY_CONFIRMED", active.SafeReturn?.Axis.Standby);
        Assert.Null(final.Sequence.LastJobOutcome);
        Assert.Single(final.Sequence.EquipmentFaults!);
        Assert.Single(final.Queue.Entries);
    }
}
