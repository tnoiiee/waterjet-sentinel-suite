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

    private static int PositionAbsoluteCount(CleaningPathExecutionHarness motion) =>
        motion.Port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute);

    private static int BeginCount(CleaningPathExecutionHarness motion) =>
        motion.Port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.Begin);

    private static void AssertMachineUntouched(CleaningPathCompositionHarness harness)
    {
        // Home dispatches exactly one Begin per resolved axis and never a PositionAbsolute plan.
        Assert.Equal(2, BeginCount(harness));
        Assert.Equal(0, PositionAbsoluteCount(harness));
        Assert.Empty(harness.Equipment.ValveCommands);
        Assert.Null(harness.Composition.Current);
        Assert.Null(harness.Composition.Result);
    }

    /// <summary>
    /// Opens, homes and activates one real Motion session, exactly as OpenHomeStart does, and starts
    /// one real composition against one equipment boundary that DECLARES the identity of the Pump
    /// sample it serves. This is the only way to exercise the declared-source seam with the production
    /// coordinator: the harness boundary cannot declare anything, which is what keeps a forged identity
    /// refused.
    /// </summary>
    private static CleaningPathCompositionStartOutcome StartDeclaring(
        CleaningPathExecutionHarness motion,
        PressureSample? sample,
        string acceptedSourceId)
    {
        motion.OpenAndObserve();
        motion.HomeRoles();
        motion.ActivateScenarioOptions();
        var composition = new CleaningPathCompositionCoordinator(
            motion.Boundary,
            new DeclaringPumpSourceBoundary(sample, acceptedSourceId),
            motion.Clock);
        return composition.Start(motion.StoreSnapshot, motion.Resolution, CleaningPathCompositionHarness.Request);
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
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV3", 16);
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
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV3", 16);
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

    // ------------------------------------------------- declared-source Pump identity (D-sim seam)

    [Fact]
    public void Declared_Source_Identity_Satisfies_Pump_Ready_In_The_Real_Composition()
    {
        // The identity the simulated Pump-ready gate stamps on its one declared value is accepted by
        // the production coordinator itself, over the real session boundary, so the D-sim run reaches
        // P1 instead of dying at the source-identity clause. The value rule is unchanged: 16.64 bar
        // against the same 15 bar strict setpoint.
        var sample = CleaningPathCompositionHarness.PumpSample(
            16.64, sourceId: CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId);
        var outcome = StartDeclaring(
            new CleaningPathExecutionHarness(),
            sample,
            CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId);

        Assert.True(outcome.Started);
        Assert.Equal(CleaningPathCompositionCodes.PumpReadyAccepted, outcome.Code);
        Assert.True(outcome.PumpReadiness!.Ready);

        // The evidence keeps the identity it arrived with: acceptance never renames a declared sample
        // into a measured AI-003 observation.
        Assert.Same(sample, outcome.PumpReadiness.Sample);
        Assert.Equal(CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId, outcome.PumpReadiness.Sample!.SourceId);
        Assert.Equal(16.64, outcome.PumpReadiness.Sample.Bar);
        Assert.Equal(PressureQuality.GOOD, outcome.PumpReadiness.Sample.Quality);
    }

    [Fact]
    public void Declared_Identity_Forged_By_A_Non_Declaring_Source_Is_Still_Refused()
    {
        // The exact identity text of the declared-source gate, served by a boundary that declares
        // nothing, is still wrong-source input. Widening the accepted identity from the source of the
        // sample rather than from the sample itself is what makes this refusal survive the fix, so the
        // evidence class stays unfabricable.
        var harness = new CleaningPathCompositionHarness();
        var outcome = harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(
            20, sourceId: CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId));

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Equal(
            CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId,
            outcome.PumpReadiness!.Sample!.SourceId);
        AssertMachineUntouched(harness);
    }

    [Fact]
    public void A_Declaration_Admits_Its_Own_Identity_And_No_Other()
    {
        // A boundary may widen the accepted identity only to the single identity it declares. The Pump
        // Inlet instrument stays refused even for a boundary that declares a source, because its own
        // sample does not carry the declared identity.
        var motion = new CleaningPathExecutionHarness();
        var outcome = StartDeclaring(
            motion,
            CleaningPathCompositionHarness.PumpSample(40, sourceId: CleaningPathCompositionHarness.Ai002PumpInletSource),
            CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId);

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Equal(CleaningPathCompositionHarness.Ai002PumpInletSource, outcome.PumpReadiness!.Sample!.SourceId);

        // Home only; no P1 Move and no paired-IV command was requested by the refused start.
        Assert.Equal(0, PositionAbsoluteCount(motion));
        Assert.Equal(2, BeginCount(motion));
    }

    [Fact]
    public void An_Empty_Declaration_Admits_Nothing()
    {
        // The opt-out is total: a source that declares an empty identity relaxes no clause at all, and
        // the sample that looks exactly like the declared gate's is refused as wrong-source input.
        var motion = new CleaningPathExecutionHarness();
        var outcome = StartDeclaring(
            motion,
            CleaningPathCompositionHarness.PumpSample(
                20, sourceId: CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId),
            string.Empty);

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpPressureInputInvalid, outcome.Code);
        Assert.Equal(0, PositionAbsoluteCount(motion));
    }

    [Fact]
    public void A_Declared_Value_At_The_Setpoint_Is_Not_Ready()
    {
        // Only the source-identity clause is informed by the declaration; the value rule is the same
        // strict greater-than over the same setpoint, so a declared value equal to the setpoint is
        // PUMP_NOT_READY rather than an acceptance.
        var motion = new CleaningPathExecutionHarness();
        var outcome = StartDeclaring(
            motion,
            CleaningPathCompositionHarness.PumpSample(
                15.00, sourceId: CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId),
            CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId);

        Assert.False(outcome.Started);
        Assert.Equal(SequencingCodes.PumpNotReady, outcome.Code);
        Assert.Equal(0, PositionAbsoluteCount(motion));
    }
}

/// <summary>
/// An equipment boundary that declares the one Pump identity its own sample carries, the same
/// capability the simulated Pump-ready gate publishes. The declared identity and the served sample are
/// supplied separately on purpose, so a test can put them in disagreement and prove that the
/// composition judges the pair rather than trusting either half alone. It commands nothing.
/// </summary>
internal sealed class DeclaringPumpSourceBoundary : ICleaningPathEquipmentBoundary, ICleaningPathDeclaredPumpSource
{
    internal DeclaringPumpSourceBoundary(PressureSample? pumpSample, string acceptedPumpReadySourceId)
    {
        PumpSample = pumpSample;
        AcceptedPumpReadySourceId = acceptedPumpReadySourceId;
    }

    internal PressureSample? PumpSample { get; }

    public string AcceptedPumpReadySourceId { get; }

    public List<CleaningPathValveCommandRequest> ValveCommands { get; } = new();

    public void RequestValve(CleaningPathValveCommandRequest request) => ValveCommands.Add(request);

    public PressureSample? ReadPumpOutletPressure() => PumpSample;

    public PressureSample? ReadValveOutletPressure(string valveId) => null;

    public bool ReadUpperLimitDetected(string valveId) => false;

    public bool ReadLowerLimitDetected(string valveId) => false;
}
