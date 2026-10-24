using System.Reflection;
using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Phase 3C lifecycle, authority and boundary matrix (Simulation only). The composition derives
// Effective WJ, paired IV (WJn to IVn), WSB cabinet and controller axes through the existing
// canonical derivations only, keeps the Sensor path immutable, preserves the first primary failure
// separately from motion and Valve cleanup, and exposes no Pump command, no hardware path, no
// Queue / AutoSequence operation and no Kiosk or Runtime API control.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class CleaningPathCompositionLifecycleTests
{
    private static CleaningPathCompositionHarness RunAcceptedOpenHarness(
        string assignedWaterJetId = "WJ4",
        string assignedIsolationValveId = "IV4",
        string? overrideWaterJetId = null)
    {
        var harness = new CleaningPathCompositionHarness(
            assignedWaterJetId: assignedWaterJetId,
            assignedIsolationValveId: assignedIsolationValveId,
            overrideWaterJetId: overrideWaterJetId);
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        return harness;
    }

    private static void RunAcceptedClose(CleaningPathCompositionHarness harness)
    {
        harness.DriveUntil(() => harness.Composition.Current!.Status == CleaningPathCompositionStatus.SupervisingValveClose);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.DriveUntilTerminal();
    }

    [Fact]
    public void Effective_Wj_Override_Derives_Paired_Iv_Wsb_And_Axes()
    {
        var harness = RunAcceptedOpenHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        RunAcceptedClose(harness);

        var identity = harness.Composition.Result!.Handoff.Identity;
        Assert.Equal("WJ2", identity.CanonicalAssignedWaterJetId);
        Assert.Equal("WJ4", identity.EffectiveWaterJetId);
        Assert.Equal(EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE, identity.EffectiveWjProvenance);

        // The paired valve, cabinet, controller and axes come from the canonical derivations of the
        // EFFECTIVE Water Jet: WJ4 pairs IV4 and runs C / D on WSB2.
        Assert.Equal("IV4", identity.DerivedIsolationValveId);
        Assert.Equal("WSB2", identity.CabinetId);
        Assert.Equal("MC-WSB2", identity.ControllerId);
        Assert.Equal("C", identity.HorizontalAxisId);
        Assert.Equal("D", identity.VerticalAxisId);
        Assert.All(harness.Equipment.ValveCommands, request => Assert.Equal("IV4", request.ValveId));
    }

    [Fact]
    public void Effective_Wj_Override_Does_Not_Mutate_P1_Through_P6()
    {
        var harness = RunAcceptedOpenHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        var pathBefore = harness.StoreSnapshot.PathFor(CleaningPathCompositionHarness.Sensor)!;
        RunAcceptedClose(harness);

        var points = harness.Composition.Result!.PathResult!.Points;
        Assert.Same(pathBefore, harness.StoreSnapshot.PathFor(CleaningPathCompositionHarness.Sensor));
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal(pathBefore.Points[index].HorizontalNative, points[index].HorizontalNative);
            Assert.Equal(pathBefore.Points[index].VerticalNative, points[index].VerticalNative);
        }
    }

    [Fact]
    public void Canonical_And_Override_Provenance_Remain_Distinct()
    {
        var canonical = RunAcceptedOpenHarness();
        RunAcceptedClose(canonical);
        Assert.Equal(EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT, canonical.Composition.Result!.Handoff.Identity.EffectiveWjProvenance);
        Assert.Null(canonical.Composition.Result.Handoff.Identity.RequestedOverrideWaterJetId);

        var overridden = RunAcceptedOpenHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        RunAcceptedClose(overridden);
        Assert.Equal(EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE, overridden.Composition.Result!.Handoff.Identity.EffectiveWjProvenance);
        Assert.Equal("WJ4", overridden.Composition.Result.Handoff.Identity.RequestedOverrideWaterJetId);
    }

    [Fact]
    public void Active_Composition_Cannot_Mutate_Assignment_Or_Path()
    {
        var harness = RunAcceptedOpenHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        var pathBefore = harness.StoreSnapshot.PathFor(CleaningPathCompositionHarness.Sensor)!;
        var resolutionBefore = harness.Resolution.Resolution!;
        RunAcceptedClose(harness);

        Assert.Same(pathBefore, harness.StoreSnapshot.PathFor(CleaningPathCompositionHarness.Sensor));
        Assert.Same(resolutionBefore, harness.Resolution.Resolution);
        Assert.Equal("WJ2", harness.Resolution.Resolution!.CanonicalAssignedWaterJetId);
        Assert.Equal("WJ4", harness.Resolution.Resolution!.EffectiveWaterJetId);
    }

    [Fact]
    public void Primary_Failure_Separate_From_Motion_And_Valve_Cleanup()
    {
        var harness = new CleaningPathCompositionHarness();
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.DriveUntil(() => harness.Equipment.ValveCommands.Count == 2);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.DriveUntilTerminal();

        var handoff = harness.Composition.Result!.Handoff;
        Assert.Equal(ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION.ToString(), handoff.PrimaryFailureCode);
        Assert.Equal(CleaningPathCleanupState.NotNeeded, handoff.MotionCleanupState);
        Assert.Equal(CleaningPathCleanupState.Succeeded, handoff.ValveCleanupState);
        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, harness.Composition.Result.Status);
    }

    [Fact]
    public void Abort_Does_Not_Begin_The_Next_Point()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.Composition.Abort();
        harness.DriveUntilTerminal();

        var pathResult = harness.Composition.Result!.PathResult!;
        var moveRequests = pathResult.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested);
        Assert.Equal(4, moveRequests);
        Assert.Equal(CleaningPathExecutionCodes.PathAborted, pathResult.Code);
        Assert.Equal(CleaningPathCompositionStatus.AbortedAwaitingSafeReturn, harness.Composition.Result.Status);
    }

    [Fact]
    public void Close_Does_Not_Begin_The_Next_Point()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.Composition.RequestClose();
        harness.DriveUntilTerminal();

        var pathResult = harness.Composition.Result!.PathResult!;
        var moveRequests = pathResult.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested);
        Assert.Equal(4, moveRequests);
        Assert.Equal(CleaningPathExecutionCodes.PathClosed, pathResult.Code);
        Assert.Equal(CleaningPathCompositionStatus.ClosedAwaitingSafeReturn, harness.Composition.Result.Status);
    }

    [Fact]
    public void Abort_Requests_Iv_Close_When_Needed()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.Composition.Abort();
        harness.DriveUntilTerminal();

        var commands = harness.Equipment.ValveCommands;
        Assert.Equal(2, commands.Count);
        Assert.Equal(CleaningPathValveCommandKind.Open, commands[0].Kind);
        Assert.Equal(CleaningPathValveCommandKind.Close, commands[1].Kind);
        Assert.Equal(CleaningPathCleanupState.Succeeded, harness.Composition.Result!.Handoff.ValveCleanupState);
    }

    [Fact]
    public void Close_Requests_Iv_Close_When_Needed()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.Composition.RequestClose();
        harness.DriveUntilTerminal();

        var commands = harness.Equipment.ValveCommands;
        Assert.Equal(2, commands.Count);
        Assert.Equal(CleaningPathValveCommandKind.Close, commands[1].Kind);
        Assert.Equal(CleaningPathCleanupState.Succeeded, harness.Composition.Result!.Handoff.ValveCleanupState);
    }

    [Fact]
    public void Equipment_Boundary_Exposes_No_Pump_Command()
    {
        var names = typeof(ICleaningPathEquipmentBoundary)
            .GetMembers()
            .Select(member => member.Name)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "ReadLowerLimitDetected",
                "ReadPumpOutletPressure",
                "ReadUpperLimitDetected",
                "ReadValveOutletPressure",
                "RequestValve",
            },
            names);

        // The only command member is the typed paired-IV request: no Pump start, stop or output
        // command exists anywhere in the typed equipment contract.
        Assert.DoesNotContain(names, name => name.Contains("Start", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Stop", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Vfd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Composition_Surface_Has_No_Queue_Api_Kiosk_Or_Native_Member()
    {
        var surfaceTypes = new[]
        {
            typeof(CleaningPathCompositionCoordinator),
            typeof(CleaningPathCompositionRequest),
            typeof(CleaningPathCompositionIdentity),
            typeof(CleaningPathPumpReadinessResult),
            typeof(CleaningPathValveCommandRequest),
            typeof(CleaningPathValveSupervisionResult),
            typeof(CleaningPathCompositionEvent),
            typeof(CleaningPathCompositionHandoff),
            typeof(CleaningPathCompositionResult),
            typeof(CleaningPathCompositionSnapshot),
            typeof(CleaningPathCompositionStartOutcome),
        };

        foreach (var type in surfaceTypes)
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var typeName = member switch
                {
                    PropertyInfo property => property.PropertyType.Name,
                    MethodInfo method => method.ReturnType.Name,
                    _ => string.Empty,
                };

                Assert.DoesNotContain("Queue", typeName, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("AutoSequence", typeName, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Kiosk", typeName, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Adapter", typeName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Success_Never_Means_Safe_Return_Or_Cleaning_Job_Complete()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        var result = harness.Composition.Result!;
        Assert.Equal(CleaningPathCompositionStatus.CompleteAwaitingSafeReturn, result.Status);
        Assert.Equal(CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn, result.Code);

        // The evidence stream claims neither a completed Safe Return nor a completed Cleaning Job.
        Assert.All(result.Events, e => Assert.DoesNotContain("SAFE_RETURN_COMPLETE", e.Code, StringComparison.Ordinal));
        Assert.All(result.Events, e => Assert.DoesNotContain("JOB_COMPLETE", e.Code, StringComparison.Ordinal));
    }

    [Fact]
    public void Pump_Readiness_Evidence_Preserved_Fail_Closed()
    {
        var harness = new CleaningPathCompositionHarness();
        var sample = CleaningPathCompositionHarness.PumpSample(20, PressureQuality.BAD);
        var outcome = harness.OpenHomeStart(sample);

        Assert.False(outcome.Started);
        Assert.Same(sample, outcome.PumpReadiness!.Sample);
        Assert.Equal(PressureQuality.BAD, outcome.PumpReadiness!.Sample!.Quality);
        Assert.Equal(PressureSample.PumpOutletSource, outcome.PumpReadiness!.Sample!.SourceId);
    }

    [Fact]
    public void Composition_Identity_Immutable_During_Active_Run()
    {
        var harness = RunAcceptedOpenHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        var identityBefore = harness.Composition.Current!.Identity;
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);

        Assert.Same(identityBefore, harness.Composition.Current!.Identity);
        Assert.Equal("IV4", harness.Composition.Current!.Identity.DerivedIsolationValveId);
    }
}
