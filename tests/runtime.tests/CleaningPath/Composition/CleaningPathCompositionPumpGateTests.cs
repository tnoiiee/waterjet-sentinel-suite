using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Phase 3C Pump-ready gate matrix (Simulation only). The Pump-ready rule is the existing strict
// greater-than rule over the exact AI-003 Pump Outlet source identity; the Pump Inlet instrument
// (AI-002, diagnostic and trend only) can never satisfy it, and no P1 Move and no paired-IV
// command may occur before the gate is accepted.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class CleaningPathCompositionPumpGateTests
{
    private static int PositionAbsoluteCount(CleaningPathCompositionHarness harness) =>
        harness.Port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute);

    private static int BeginCount(CleaningPathCompositionHarness harness) =>
        harness.Port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.Begin);

    private static void AssertMachineUntouched(CleaningPathCompositionHarness harness)
    {
        // Home dispatches exactly one Begin per resolved axis and never a PositionAbsolute plan.
        Assert.Equal(2, BeginCount(harness));
        Assert.Equal(0, PositionAbsoluteCount(harness));
        Assert.Empty(harness.Equipment.ValveCommands);
        Assert.Null(harness.Composition.Current);
        Assert.Null(harness.Composition.Result);
    }

    [Fact]
    public void Valid_Pressure_Above_Setpoint_Allows_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(15.01));

        Assert.True(outcome.Started);
        Assert.Equal(CleaningPathCompositionCodes.PumpReadyAccepted, outcome.Code);
        Assert.True(outcome.PumpReadiness!.Ready);

        // Both P1 Move plans exist and the paired-IV open is requested only after both axes verify.
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        harness.DriveUntil(() => harness.Composition.Current!.Status == CleaningPathCompositionStatus.SupervisingValveOpen);
        Assert.Equal(2, PositionAbsoluteCount(harness));
        Assert.Single(harness.Equipment.ValveCommands);
    }

    [Fact]
    public void Pressure_Equal_To_Setpoint_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(15.00));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpNotReady, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Pressure_Below_Setpoint_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(14.99));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpNotReady, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Bad_Pressure_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(20, PressureQuality.BAD));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Stale_Pressure_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(20, stale: true));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Null_Pressure_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(null);

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Null(outcome.PumpReadiness!.Sample);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void NaN_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(double.NaN));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Positive_Infinity_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(double.PositiveInfinity));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Negative_Infinity_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(double.NegativeInfinity));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Wrong_Source_Blocks_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(20, sourceId: "IV4_OUTLET"));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Equal("IV4_OUTLET", outcome.PumpReadiness!.Sample!.SourceId);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Invalid_Thresholds_Block_P1()
    {
        var harness = new CleaningPathCompositionHarness();
        var request = new CleaningPathCompositionRequest
        {
            PressureThresholds = new SequencingPressureThresholds(double.NaN, 15),
            ValveSupervisionTimeout = CleaningPathCompositionHarness.SupervisionTimeout,
        };
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(20), request);

        Assert.False(outcome.Started);
        Assert.Equal(SequencingPressureThresholds.InvalidCode, outcome.Code);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void Ai002_Pump_Inlet_Cannot_Satisfy_Pump_Ready()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(
            40, sourceId: CleaningPathCompositionHarness.Ai002PumpInletSource));

        // AI-002 is diagnostic and trend only: even a ready-level pressure from the Pump Inlet
        // identity is wrong-source input and never satisfies the Pump-ready rule.
        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Equal(CleaningPathCompositionHarness.Ai002PumpInletSource, outcome.PumpReadiness!.Sample!.SourceId);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void No_P1_Move_Occurs_Before_Pump_Ready_Acceptance()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(15.00));

        Assert.False(outcome.Started);
        Assert.Equal(2, BeginCount(harness));
        Assert.Equal(0, PositionAbsoluteCount(harness));
    }

    [Fact]
    public void No_Iv_Command_Occurs_Before_P1_Verification()
    {
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16));
        Assert.True(outcome.Started);

        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        for (var index = 0; index < 2000 && harness.Composition.Current!.Status != CleaningPathCompositionStatus.SupervisingValveOpen; index++)
        {
            Assert.Empty(harness.Equipment.ValveCommands);
            harness.Step(1);
        }

        // The open request exists exactly when P1 is dual-verified and held at P1.
        Assert.Equal(CleaningPathCompositionStatus.SupervisingValveOpen, harness.Composition.Current!.Status);
        Assert.Equal(Wjss.Contracts.JobPhase.P1, harness.Composition.Current.CurrentPointId);
        var open = Assert.Single(harness.Equipment.ValveCommands);
        Assert.Equal(CleaningPathValveCommandKind.Open, open.Kind);
    }
}
