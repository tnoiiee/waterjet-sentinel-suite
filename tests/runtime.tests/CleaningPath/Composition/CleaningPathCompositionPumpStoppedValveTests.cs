using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Scope B2: paired-IV supervision for the ONE evidence class in which Pump-ready was admitted from an
// Owner declaration under a Pump the Owner attests STOPPED (the runbook Stage D simulated variant). These
// facts drive the production composition coordinator over the REAL CleaningPathSimulatedPumpReadyGate and
// one real Manual Motion session on the deterministic simulated port, because the supervision mode must
// follow the identity that actually carried this run's readiness decision and nothing else: a boundary that
// declares nothing, and a declaring boundary whose sample carries the MEASURED Pump Outlet identity, both
// keep the physical classification byte-for-byte.
//
// Every value here is synthetic test data. Nothing in this file states or implies that a valve moved, that
// a Pump ran, that a limit switch exists in the field, or that any pressure was measured: a simulated
// result is never hardware evidence.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class CleaningPathCompositionPumpStoppedValveTests
{
    private const double DeclaredPumpBar = 16.64;
    private const double PumpReadySetpointBar = 15.0;
    private const string Valve = "IV3";

    private static readonly DateTimeOffset SampleAt = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);

    private static PressureSample ValveOutlet(double? bar, string? sourceId = null) =>
        new(bar, PressureQuality.GOOD, false, SampleAt, sourceId ?? PressureSample.ValveOutletSource(Valve));

    private static int PositionAbsoluteCount(ManualMotionSimulatedPort port) =>
        port.ExecutedCommands.Count(command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute);

    /// <summary>
    /// One composition over the real declared-source gate. The gate IS the equipment boundary, so the
    /// unchanged delegation of every valve observation and command is part of what these facts test, and
    /// the run is begun because that is what admits a paired-IV OPEN through this gate at all.
    /// </summary>
    private sealed class PumpStoppedRig
    {
        internal PumpStoppedRig(FakeEquipmentBoundary? equipment = null)
        {
            Motion = new CleaningPathExecutionHarness();
            Equipment = equipment ?? new FakeEquipmentBoundary();
            Gate = new CleaningPathSimulatedPumpReadyGate(Equipment, Motion.Clock, DeclaredPumpBar, PumpReadySetpointBar);
            Gate.Begin();
            Composition = new CleaningPathCompositionCoordinator(Motion.Boundary, Gate, Motion.Clock);
        }

        internal CleaningPathExecutionHarness Motion { get; }

        internal FakeEquipmentBoundary Equipment { get; }

        internal CleaningPathSimulatedPumpReadyGate Gate { get; }

        internal CleaningPathCompositionCoordinator Composition { get; }

        internal CleaningPathCompositionResult Result => Composition.Result!;

        internal int Moves => PositionAbsoluteCount(Motion.Port);

        internal void Start()
        {
            Motion.OpenAndObserve();
            Motion.HomeRoles();
            Motion.ActivateScenarioOptions();
            var outcome = Composition.Start(Motion.StoreSnapshot, Motion.Resolution, CleaningPathCompositionHarness.Request);

            // Declared Pump-ready is the precondition of this whole file: a finite value strictly above the
            // setpoint, carried by the gate's own declared identity, with no physical Pump verified.
            Assert.True(outcome.Started);
            Assert.Equal(CleaningPathCompositionCodes.PumpReadyAccepted, outcome.Code);
            Assert.Equal(DeclaredPumpBar, outcome.PumpReadiness!.Sample!.Bar);
            Assert.False(CleaningPathSimulatedPumpReadyGate.PhysicalPumpVerified);
            Assert.Equal(0, Moves);
        }

        internal void Step()
        {
            Motion.Clock.Advance(TimeSpan.FromMilliseconds(25));
            Composition.Step();
        }

        internal void DriveUntil(Func<bool> done, int maximumSteps = 12000)
        {
            for (var index = 0; index < maximumSteps && !done(); index++)
            {
                Step();
            }

            Assert.True(done(), "the expected composition state was not reached within the step budget");
        }

        internal void DriveUntilTerminal() => DriveUntil(() => Composition.Result is not null);

        internal void DriveUntilStatus(CleaningPathCompositionStatus status) =>
            DriveUntil(() => Composition.Current!.Status == status);
    }

    [Fact]
    public void Declared_Pump_Run_Completes_The_Sequence_On_Limit_Evidence_Alone()
    {
        // The D-sim wall B2 removes: P1 dual-verifies, the paired IV is commanded open, the Upper limit is
        // detected and the IV outlet observation stays at 0 bar because the Pump is attested stopped. The
        // open is accepted on the limit, P2 through P6 run, and the close is confirmed on the Lower limit -
        // with the raw 0 bar preserved as the evidence and never replaced by a fabricated pressure value.
        var rig = new PumpStoppedRig(new FakeEquipmentBoundary
        {
            UpperLimitDetected = true,
            ValveSample = ValveOutlet(0.0),
        });
        rig.Start();
        rig.DriveUntilStatus(CleaningPathCompositionStatus.SupervisingValveOpen);
        Assert.Equal(2, rig.Moves);

        rig.DriveUntilStatus(CleaningPathCompositionStatus.SupervisingValveClose);
        Assert.Equal(12, rig.Moves);
        rig.Equipment.LowerLimitDetected = true;
        rig.DriveUntilTerminal();

        Assert.Equal(CleaningPathCompositionStatus.CompleteAwaitingSafeReturn, rig.Result.Status);
        Assert.Equal(CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn, rig.Result.Code);
        Assert.Null(rig.Result.Handoff.PrimaryFailureCode);

        var open = rig.Result.Handoff.ValveOpenSupervision!;
        Assert.Equal(Valve, open.ValveId);
        Assert.Equal(ValveOpenResolution.OPEN_CONFIRMED, open.Assessment.Open);
        Assert.Equal(ValveDiagnosis.NONE, open.Assessment.Diagnosis);
        Assert.True(open.LimitDetected);
        Assert.False(open.TimedOut);
        Assert.Equal(0.0, open.PressureEvidence!.Bar);
        Assert.Equal(PressureSample.ValveOutletSource(Valve), open.PressureEvidence.SourceId);

        var close = rig.Result.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.LOWER_LIMIT_CONFIRMED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.NONE, close.Assessment.Diagnosis);
        Assert.True(close.LimitDetected);
        Assert.Equal(0.0, close.PressureEvidence!.Bar);

        // Exactly one open and one close reached the delegated boundary: the gate forwarded both, and it
        // refused neither, because the run had begun.
        Assert.Equal(2, rig.Equipment.ValveCommands.Count);
        Assert.Equal(CleaningPathValveCommandKind.Open, rig.Equipment.ValveCommands[0].Kind);
        Assert.Equal(CleaningPathValveCommandKind.Close, rig.Equipment.ValveCommands[1].Kind);
        Assert.All(rig.Equipment.ValveCommands, request => Assert.Equal(Valve, request.ValveId));
        Assert.Empty(rig.Gate.RefusedRequests);
        Assert.Equal(CleaningPathCleanupState.NotNeeded, rig.Result.ValveCleanupState);
    }

    [Fact]
    public void Declared_Pump_Run_Times_Out_The_Open_And_Still_Fences_The_Valve_Closed()
    {
        // 0 bar without the Upper limit is NOT an open in this mode: the window ends as a failed open, P2 is
        // never requested, and the cleanup close is still commanded because the valve state is unknown. The
        // cleanup then refuses to call the valve closed on an expected 0 bar instead of inferring closure.
        var rig = new PumpStoppedRig(new FakeEquipmentBoundary
        {
            UpperLimitDetected = false,
            ValveSample = ValveOutlet(0.0),
        });
        rig.Start();
        rig.DriveUntilStatus(CleaningPathCompositionStatus.SupervisingValveOpen);
        Assert.Equal(2, rig.Moves);
        rig.DriveUntilTerminal();

        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, rig.Result.Status);
        Assert.Equal(CleaningPathCompositionCodes.CompositionFailedAwaitingSafeReturn, rig.Result.Code);
        Assert.Equal("VALVE_FAILED_TO_OPEN", rig.Result.Handoff.PrimaryFailureCode);
        Assert.Equal(2, rig.Moves);

        var open = rig.Result.Handoff.ValveOpenSupervision!;
        Assert.Equal(ValveOpenResolution.BLOCKED, open.Assessment.Open);
        Assert.Equal(ValveDiagnosis.VALVE_FAILED_TO_OPEN, open.Assessment.Diagnosis);
        Assert.True(open.TimedOut);
        Assert.Equal(0.0, open.PressureEvidence!.Bar);

        Assert.Equal(2, rig.Equipment.ValveCommands.Count);
        Assert.Equal(CleaningPathValveCommandKind.Close, rig.Equipment.ValveCommands[1].Kind);
        var cleanup = rig.Result.Handoff.ValveCleanupSupervision!;
        Assert.Equal(ValveCloseResolution.NOT_FULLY_CLOSED, cleanup.Assessment.Close);
        Assert.Equal(ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT, cleanup.Assessment.Diagnosis);
        Assert.Equal(CleaningPathCleanupState.Failed, rig.Result.ValveCleanupState);

        // The failure was the valve claim, not the motion: the path was fenced by the typed Abort and no
        // further point was ever requested.
        Assert.Equal(2, rig.Moves);
    }

    [Fact]
    public void Unexpected_Line_Pressure_Under_A_Stopped_Pump_Blocks_The_Open_Immediately()
    {
        // Any live pressure at or above the low band contradicts the attested stop, so the open fails on the
        // first supervision step - before the window elapses and even with the Upper limit detected, where
        // the physical route would have read the same band as a mere leak. The valve is still fenced closed.
        var rig = new PumpStoppedRig(new FakeEquipmentBoundary
        {
            UpperLimitDetected = true,
            ValveSample = ValveOutlet(2.0),
        });
        rig.Start();
        rig.DriveUntilStatus(CleaningPathCompositionStatus.SupervisingValveOpen);
        rig.Step();
        rig.DriveUntilTerminal();

        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, rig.Result.Status);
        Assert.Equal("VALVE_OPEN_FEEDBACK_CONTRADICTION", rig.Result.Handoff.PrimaryFailureCode);
        var open = rig.Result.Handoff.ValveOpenSupervision!;
        Assert.Equal(ValveOpenResolution.BLOCKED, open.Assessment.Open);
        Assert.Equal(ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION, open.Assessment.Diagnosis);
        Assert.False(open.TimedOut);
        Assert.Equal(2.0, open.PressureEvidence!.Bar);
        Assert.Equal(2, rig.Moves);
        Assert.Equal(CleaningPathValveCommandKind.Close, rig.Equipment.ValveCommands[^1].Kind);
    }

    [Fact]
    public void Declared_Pump_Run_Refuses_The_Success_Terminal_Without_The_Physical_Lower_Limit()
    {
        // The widening may accept an open on a limit switch; it can never report a completed path-and-valve
        // sequence on a pressure inference. With the Lower limit missing the close window ends
        // NOT_FULLY_CLOSED, that classification becomes the first primary failure, and the valve evidence
        // still reads the raw 0 bar.
        var rig = new PumpStoppedRig(new FakeEquipmentBoundary
        {
            UpperLimitDetected = true,
            ValveSample = ValveOutlet(0.0),
        });
        rig.Start();
        rig.DriveUntilStatus(CleaningPathCompositionStatus.SupervisingValveClose);
        Assert.Equal(12, rig.Moves);
        rig.DriveUntilTerminal();

        Assert.NotEqual(
            CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn,
            rig.Result.Code);
        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, rig.Result.Status);
        Assert.Equal("LOWER_LIMIT_SENSOR_FAULT", rig.Result.Handoff.PrimaryFailureCode);

        var close = rig.Result.Handoff.ValveCloseSupervision!;
        Assert.Equal(ValveCloseResolution.NOT_FULLY_CLOSED, close.Assessment.Close);
        Assert.Equal(ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT, close.Assessment.Diagnosis);
        Assert.True(close.TimedOut);
        Assert.False(close.LimitDetected);
        Assert.Equal(0.0, close.PressureEvidence!.Bar);
        Assert.NotEqual(ValveCloseResolution.CLOSED_BY_PRESSURE, close.Assessment.Close);
    }

    [Fact]
    public void Declared_Pump_Run_Still_Refuses_A_Valve_Observation_From_The_Wrong_Source()
    {
        // The widening changes what 0 bar means and nothing about where an observation came from. A
        // ready-level pressure from another valve's outlet identity is invalid input for this pair and the
        // limit switch cannot substitute for it: the open stays blocked as invalid input, P2 is never
        // configured, and the raw sample is preserved with the identity it arrived with.
        var rig = new PumpStoppedRig(new FakeEquipmentBoundary
        {
            UpperLimitDetected = true,
            ValveSample = ValveOutlet(16.0, PressureSample.ValveOutletSource("IV2")),
        });
        rig.Start();
        rig.DriveUntilTerminal();

        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, rig.Result.Status);
        Assert.Equal("VALVE_PRESSURE_INPUT_INVALID", rig.Result.Handoff.PrimaryFailureCode);
        var open = rig.Result.Handoff.ValveOpenSupervision!;
        Assert.Equal(ValveOpenResolution.BLOCKED, open.Assessment.Open);
        Assert.Equal(ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID, open.Assessment.Diagnosis);
        Assert.Equal(PressureSample.ValveOutletSource("IV2"), open.PressureEvidence!.SourceId);
        Assert.Equal(16.0, open.PressureEvidence.Bar);
        Assert.Equal(2, rig.Moves);
    }

    [Fact]
    public void A_Boundary_That_Declares_Nothing_Keeps_The_Physical_Open_Classification()
    {
        // The regression guard for every non-declaring route: the same 0 bar with the Upper limit detected
        // under a MEASURED Pump Outlet readiness is still the contradiction it always was, so a real run can
        // never inherit the limit-only acceptance by accident.
        var harness = new CleaningPathCompositionHarness();
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16.0)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = ValveOutlet(0.0);
        harness.DriveUntilTerminal();

        var result = harness.Composition.Result!;
        var open = result.Handoff.ValveOpenSupervision!;
        Assert.Equal(ValveOpenResolution.BLOCKED, open.Assessment.Open);
        Assert.Equal(ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION, open.Assessment.Diagnosis);
        Assert.Equal("VALVE_OPEN_FEEDBACK_CONTRADICTION", result.Handoff.PrimaryFailureCode);
        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, result.Status);
        Assert.Equal(2, PositionAbsoluteCount(harness.Port));
    }

    [Fact]
    public void A_Declaring_Boundary_Served_By_A_Measured_Sample_Keeps_The_Physical_Rules()
    {
        // The declared mode is entered only when the DECLARED identity carried the readiness decision. A
        // boundary that declares an identity and then serves a sample carrying the MEASURED Pump Outlet
        // identity is admitted by the measured clause, so it stays on the physical supervision route and 0
        // bar with the Upper limit detected is still a contradiction. Selecting the mode from the presence
        // of a declaration alone is what this fact makes unreachable.
        var motion = new CleaningPathExecutionHarness();
        var equipment = new DeclaresMeasuredIdentityBoundary();
        var composition = new CleaningPathCompositionCoordinator(motion.Boundary, equipment, motion.Clock);
        motion.OpenAndObserve();
        motion.HomeRoles();
        motion.ActivateScenarioOptions();

        var outcome = composition.Start(motion.StoreSnapshot, motion.Resolution, CleaningPathCompositionHarness.Request);
        Assert.True(outcome.Started);
        Assert.Equal(PressureSample.PumpOutletSource, outcome.PumpReadiness!.Sample!.SourceId);

        for (var index = 0; index < 12000 && composition.Result is null; index++)
        {
            motion.Clock.Advance(TimeSpan.FromMilliseconds(25));
            composition.Step();
        }

        var result = composition.Result!;
        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, result.Status);
        Assert.Equal("VALVE_OPEN_FEEDBACK_CONTRADICTION", result.Handoff.PrimaryFailureCode);
        Assert.Equal(
            ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION,
            result.Handoff.ValveOpenSupervision!.Assessment.Diagnosis);
        Assert.Equal(2, PositionAbsoluteCount(motion.Port));
    }

    /// <summary>
    /// A boundary that declares the gate's declared-source identity as the one it accepts, while serving the
    /// MEASURED Pump Outlet identity as its sample. No production gate behaves this way; the fake exists so
    /// that the selection rule - the identity that decided readiness, not the mere presence of a declaration
    /// - is testable without touching any approved production source.
    /// </summary>
    private sealed class DeclaresMeasuredIdentityBoundary : ICleaningPathEquipmentBoundary, ICleaningPathDeclaredPumpSource
    {
        public string AcceptedPumpReadySourceId => CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId;

        public void RequestValve(CleaningPathValveCommandRequest request)
        {
        }

        public PressureSample? ReadPumpOutletPressure() =>
            new(DeclaredPumpBar, PressureQuality.GOOD, false, SampleAt, PressureSample.PumpOutletSource);

        public PressureSample? ReadValveOutletPressure(string valveId) => ValveOutlet(0.0);

        public bool ReadUpperLimitDetected(string valveId) => true;

        public bool ReadLowerLimitDetected(string valveId) => false;
    }
}
