using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Deterministic test harness for the Phase 3C Simulation-only composition scenarios: one real
// Manual Motion session over the deterministic simulated port (reused through the Phase 3B
// harness), the typed Phase 3B execution coordinator (composed inside the composition) and one
// typed fake equipment boundary that serves Pump and paired-valve observations and records the
// typed paired-valve command requests. Every value is synthetic test data; no Owner-local
// pressure, no real coordinate and no Hardware value appears here. A simulated result is never
// hardware evidence.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

internal sealed class CleaningPathCompositionHarness
{
    internal const string Sensor = CleaningPathExecutionHarness.Sensor;
    internal const string Ai002PumpInletSource = "PUMP_INLET";

    internal static readonly SequencingPressureThresholds Thresholds = new();
    internal static readonly TimeSpan SupervisionTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly DateTimeOffset SampleAt = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);

    private readonly CleaningPathExecutionHarness _motion;

    internal CleaningPathCompositionHarness(
        ManualMotionSimulationOptions? options = null,
        int verticalSpeed = 200000,
        int verticalMoveTimeoutMilliseconds = 20000,
        string assignedWaterJetId = "WJ4",
        string assignedIsolationValveId = "IV4",
        string? overrideWaterJetId = null)
    {
        _motion = new CleaningPathExecutionHarness(
            options: options,
            verticalSpeed: verticalSpeed,
            verticalMoveTimeoutMilliseconds: verticalMoveTimeoutMilliseconds,
            assignedWaterJetId: assignedWaterJetId,
            assignedIsolationValveId: assignedIsolationValveId,
            overrideWaterJetId: overrideWaterJetId);
        Equipment = new FakeEquipmentBoundary();
        Composition = new CleaningPathCompositionCoordinator(_motion.Boundary, Equipment, _motion.Clock);
    }

    internal FakeEquipmentBoundary Equipment { get; }

    internal CleaningPathCompositionCoordinator Composition { get; }

    internal CleaningPathExecutionHarness Motion => _motion;

    internal TestClock Clock => _motion.Clock;

    internal ManualMotionSession Session => _motion.Session;

    internal ManualMotionSimulatedPort Port => _motion.Port;

    internal CleaningPathSnapshot StoreSnapshot => _motion.StoreSnapshot;

    internal CleaningPathResolutionResult Resolution => _motion.Resolution;

    internal static CleaningPathCompositionRequest Request { get; } = new()
    {
        PressureThresholds = Thresholds,
        ValveSupervisionTimeout = SupervisionTimeout,
    };

    /// <summary>Opens the session, homes both roles, activates scenario options and starts the composition.</summary>
    internal CleaningPathCompositionStartOutcome OpenHomeStart(
        PressureSample? pumpSample,
        CleaningPathCompositionRequest? request = null)
    {
        _motion.OpenAndObserve();
        _motion.HomeRoles();
        _motion.ActivateScenarioOptions();
        Equipment.PumpSample = pumpSample;
        return Composition.Start(_motion.StoreSnapshot, _motion.Resolution, request ?? Request);
    }

    internal void Step(int steps = 1)
    {
        for (var index = 0; index < steps; index++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(25));
            Composition.Step();
        }
    }

    internal void DriveUntil(Func<bool> done, int maximumSteps = 12000)
    {
        for (var index = 0; index < maximumSteps && !done(); index++)
        {
            Step(1);
        }

        Assert.True(done(), "the expected state was not reached within the step budget");
    }

    internal void DriveUntilTerminal(int maximumSteps = 12000)
    {
        DriveUntil(() => Composition.Result is not null, maximumSteps);
    }

    internal static PressureSample PumpSample(
        double? bar,
        PressureQuality quality = PressureQuality.GOOD,
        bool stale = false,
        string sourceId = PressureSample.PumpOutletSource) =>
        new(bar, quality, stale, SampleAt, sourceId);

    internal static PressureSample ValveSample(
        string valveId,
        double? bar,
        PressureQuality quality = PressureQuality.GOOD,
        bool stale = false,
        string? sourceId = null) =>
        new(bar, quality, stale, SampleAt, sourceId ?? PressureSample.ValveOutletSource(valveId));
}

/// <summary>
/// The typed fake equipment boundary: records every paired-IV command request and serves the
/// configured Pump and paired-valve observations. It implements exactly the typed contract and can
/// never issue a Pump command because the contract has none.
/// </summary>
internal sealed class FakeEquipmentBoundary : ICleaningPathEquipmentBoundary
{
    internal List<CleaningPathValveCommandRequest> ValveCommands { get; } = new();

    internal PressureSample? PumpSample { get; set; }

    internal PressureSample? ValveSample { get; set; }

    internal bool UpperLimitDetected { get; set; }

    internal bool LowerLimitDetected { get; set; }

    public void RequestValve(CleaningPathValveCommandRequest request) => ValveCommands.Add(request);

    public PressureSample? ReadPumpOutletPressure() => PumpSample;

    public PressureSample? ReadValveOutletPressure(string valveId) => ValveSample;

    public bool ReadUpperLimitDetected(string valveId) => UpperLimitDetected;

    public bool ReadLowerLimitDetected(string valveId) => LowerLimitDetected;
}
