using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Phase 3C paired-IV close supervision matrix (Simulation only). The close decision is the existing
// typed Sequencing pressure / Valve classifier over the paired-IV outlet pressure and that valve's
// Lower limit; every classification is recorded for the Mandatory Safe Return phase and the
// composition terminal always awaits Safe Return: it never means a completed Safe Return, a
// completed Cleaning Job or Production completion, and no Centre movement is ever commanded.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class CleaningPathCompositionValveCloseTests
{
    private static int PositionAbsoluteCount(CleaningPathCompositionHarness harness) =>
        harness.Port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute);

    private static CleaningPathCompositionHarness RunToCloseSupervision()
    {
        var harness = new CleaningPathCompositionHarness();
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        harness.DriveUntil(() => harness.Composition.Current!.Status == CleaningPathCompositionStatus.SupervisingValveClose);
        return harness;
    }

    private static void FinishClose(CleaningPathCompositionHarness harness, bool lower, PressureSample sample)
    {
        harness.Equipment.LowerLimitDetected = lower;
        harness.Equipment.ValveSample = sample;
        harness.DriveUntilTerminal();
    }

    [Fact]
    public void P6_Must_Verify_Before_Iv_Close()
    {
        var harness = new CleaningPathCompositionHarness();
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        harness.DriveUntil(() => harness.Equipment.ValveCommands.Count == 2);

        // The close command exists exactly when all six points were planned and P6 is verified.
        Assert.Equal(12, PositionAbsoluteCount(harness));
        Assert.Equal(Wjss.Contracts.JobPhase.P6, harness.Composition.Current!.CurrentPointId);
        Assert.Equal(CleaningPathValveCommandKind.Close, harness.Equipment.ValveCommands[1].Kind);
    }

    [Fact]
    public void Lower_Detected_Pressure_Below_Low_Confirms_Closed()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 0.5));

        var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.LOWER_LIMIT_CONFIRMED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.NONE, close.Assessment.Diagnosis);
    }

    [Fact]
    public void Missing_Lower_Plus_Pressure_Below_Low_Records_Lower_Fault()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: false, CleaningPathCompositionHarness.ValveSample("IV4", 0.5));

        var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.CLOSED_BY_PRESSURE, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT, close.Assessment.Diagnosis);
        Assert.True(close.TimedOut);
    }

    [Fact]
    public void Middle_Band_Is_Leak_Suspected()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 8));

        var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.LEAK_SUSPECTED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.VALVE_LEAK_SUSPECTED, close.Assessment.Diagnosis);
    }

    [Fact]
    public void Pressure_At_Or_Above_High_Is_Not_Fully_Closed()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 15));

        var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.NOT_FULLY_CLOSED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.VALVE_NOT_FULLY_CLOSED, close.Assessment.Diagnosis);
    }

    [Fact]
    public void Lower_Detected_Cannot_Override_Pressure_Contradiction()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 16));

        // The Lower limit never overrides pressure at or above High into a clean closed result.
        var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.NOT_FULLY_CLOSED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.VALVE_NOT_FULLY_CLOSED, close.Assessment.Diagnosis);
    }

    [Fact]
    public void Invalid_Bad_Stale_Wrong_Source_Pressure_Remains_Invalid()
    {
        var invalidSamples = new PressureSample?[]
        {
            null,
            CleaningPathCompositionHarness.ValveSample("IV4", null),
            CleaningPathCompositionHarness.ValveSample("IV4", double.NaN),
            CleaningPathCompositionHarness.ValveSample("IV4", double.NegativeInfinity),
            CleaningPathCompositionHarness.ValveSample("IV4", 0.5, PressureQuality.BAD),
            CleaningPathCompositionHarness.ValveSample("IV4", 0.5, stale: true),
            CleaningPathCompositionHarness.ValveSample("IV4", 0.5, sourceId: "IV2_OUTLET"),
        };

        foreach (var sample in invalidSamples)
        {
            var harness = RunToCloseSupervision();
            FinishClose(harness, lower: true, sample!);

            var close = harness.Composition.Result!.Handoff.ValveCloseSupervision!;
            Assert.Equal(ValveCloseResolution.PRESSURE_INPUT_INVALID, close.Assessment.Close);
            Assert.Equal(ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID, close.Assessment.Diagnosis);
        }
    }

    [Fact]
    public void Terminal_Awaits_Safe_Return_Even_When_Close_Is_Not_Confirmed()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 16));

        // The close classification is recorded evidence for the Mandatory Safe Return phase; the
        // composition terminal itself means only "awaiting Mandatory Safe Return".
        Assert.Equal(CleaningPathCompositionStatus.CompleteAwaitingSafeReturn, harness.Composition.Result!.Status);
        Assert.Equal(
            CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn,
            harness.Composition.Result.Code);
        Assert.Equal(CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn, harness.Composition.Result.Handoff.TerminalCode);
    }

    [Fact]
    public void No_Centre_Move_Occurs()
    {
        var harness = RunToCloseSupervision();
        FinishClose(harness, lower: true, CleaningPathCompositionHarness.ValveSample("IV4", 0.5));

        // Every dispatched absolute Move belongs to the Sensor path targets; the named Centre
        // contract (1250000 / 1250000 native) is never dispatched as a Move.
        var pathTargets = harness.StoreSnapshot.PathFor(CleaningPathCompositionHarness.Sensor)!
            .Points
            .SelectMany(point => new[] { point.HorizontalNative, point.VerticalNative })
            .ToHashSet();
        foreach (var command in harness.Port.ExecutedCommands.Where(c => c.Kind == ManualMotionPortCommandKind.PositionAbsolute))
        {
            Assert.Contains(command.Operand, pathTargets);
        }

        Assert.Equal(12, PositionAbsoluteCount(harness));
    }
}
