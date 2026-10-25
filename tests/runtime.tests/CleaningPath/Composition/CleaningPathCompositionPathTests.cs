using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// Phase 3C P2 through P6 gating matrix (Simulation only). After the accepted paired-IV open the
// Phase 3B coordinator resumes with the unchanged strict P1 to P6 order, no skip, no repeat,
// parallel-independent per-axis lifecycles and the unchanged failure model: the first cause is
// preserved, later points stay blocked and the paired IV is fenced closed when needed.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class CleaningPathCompositionPathTests
{
    private static CleaningPathCompositionHarness RunAcceptedOpenHarness()
    {
        var harness = new CleaningPathCompositionHarness();
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
    public void Accepted_Valve_Open_Result_Permits_P2()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);

        // P2 exists only because the accepted open result opened the typed advance gate.
        Assert.Equal(CleaningPathCompositionStatus.ExecutingPath, harness.Composition.Current!.Status);
        Assert.Single(harness.Equipment.ValveCommands);
    }

    [Fact]
    public void P2_Through_P6_Strict_Order_With_No_Skip_And_No_Repeat()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        var points = harness.Composition.Result!.PathResult!.Points;
        Assert.Equal(6, points.Count);
        Assert.Equal(JobPhase.P1, points[0].PointId);
        Assert.Equal(JobPhase.P2, points[1].PointId);
        Assert.Equal(JobPhase.P3, points[2].PointId);
        Assert.Equal(JobPhase.P4, points[3].PointId);
        Assert.Equal(JobPhase.P5, points[4].PointId);
        Assert.Equal(JobPhase.P6, points[5].PointId);
        Assert.All(points, point => Assert.Equal(CleaningPathPointState.Verified, point.State));
    }

    [Fact]
    public void Both_Axes_Verify_Every_Point()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        var points = harness.Composition.Result!.PathResult!.Points;
        Assert.All(points, point =>
        {
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Horizontal.MotionOutcome);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Vertical.MotionOutcome);
        });
    }

    [Fact]
    public void Phase3B_Parallel_Independent_Semantics_Remain()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        // Per point the two axis records stay independent and the deterministic request order is
        // Horizontal then Vertical through the one serialized owner.
        var pathResult = harness.Composition.Result!.PathResult!;
        Assert.All(pathResult.Points, point =>
        {
            Assert.Equal(CleaningPathExecutionRole.Horizontal, point.Horizontal.Role);
            Assert.Equal(CleaningPathExecutionRole.Vertical, point.Vertical.Role);
            Assert.NotEqual(point.Horizontal.AxisId, point.Vertical.AxisId);
            Assert.False(point.Horizontal.StopRequestedOnce);
            Assert.False(point.Vertical.StopRequestedOnce);
        });

        var requestEvents = pathResult.Events
            .Where(e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested)
            .ToList();
        Assert.Equal(12, requestEvents.Count);
        for (var index = 0; index < 12; index += 2)
        {
            Assert.Equal(CleaningPathExecutionRole.Horizontal, requestEvents[index].Role);
            Assert.Equal(CleaningPathExecutionRole.Vertical, requestEvents[index + 1].Role);
        }
    }

    [Fact]
    public void Motion_Failure_Preserves_The_First_Cause()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);

        // Stall the Vertical axis only after the accepted open so the failure occurs on P2.
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.D,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.DriveUntilTerminal();

        Assert.Equal(CleaningPathCompositionStatus.FailedAwaitingSafeReturn, harness.Composition.Result!.Status);
        Assert.Equal("MOVE_TIMEOUT", harness.Composition.Result.Handoff.PrimaryFailureCode);
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, harness.Composition.Result.PathResult!.Status);
    }

    [Fact]
    public void Cleanup_Fences_The_Paired_Iv_When_Necessary()
    {
        var harness = RunAcceptedOpenHarness();
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.D,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.DriveUntil(() => harness.Equipment.ValveCommands.Count == 2);

        // The paired IV was commanded open, so its state may be open: a cleanup close is fenced.
        var cleanupClose = harness.Equipment.ValveCommands[1];
        Assert.Equal(CleaningPathValveCommandKind.Close, cleanupClose.Kind);
        Assert.Equal("IV4", cleanupClose.ValveId);

        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.DriveUntilTerminal();

        Assert.Equal(CleaningPathCleanupState.Succeeded, harness.Composition.Result!.Handoff.ValveCleanupState);
        Assert.NotNull(harness.Composition.Result.Handoff.ValveCleanupSupervision);
    }

    [Fact]
    public void Primary_Failure_Separate_From_Motion_And_Valve_Cleanup()
    {
        // Normal validated path profile on both P2 axes (equal 20000 ms Move timeouts). The
        // deterministic Horizontal-first request and begin order gives the stalled Horizontal
        // operation the first MOVE_TIMEOUT while the Vertical operation remains active and
        // requires exactly-once Stop; that Stop is supervised to inactive with brake apply. The
        // test proves the separation of primary failure, motion cleanup and Valve cleanup, and
        // does not depend on which role owns the MOVE_TIMEOUT.
        var harness = new CleaningPathCompositionHarness();
        Assert.True(harness.OpenHomeStart(CleaningPathCompositionHarness.PumpSample(16)).Started);
        harness.Equipment.UpperLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 16);
        harness.DriveUntil(() => harness.Composition.Current!.CurrentPointId == JobPhase.P2);

        // Both P2 operations are already requested but not yet processed by the next boundary
        // step: stalling both resolved axes (C = Horizontal, D = Vertical) keeps the Horizontal
        // operation active until its MOVE_TIMEOUT fires first; the Vertical operation is then
        // stopped exactly once and supervised to inactive with brake apply.
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.C,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.D,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.Equipment.LowerLimitDetected = true;
        harness.Equipment.ValveSample = CleaningPathCompositionHarness.ValveSample("IV4", 0.5);
        harness.DriveUntilTerminal();

        var handoff = harness.Composition.Result!.Handoff;
        Assert.Equal("MOVE_TIMEOUT", handoff.PrimaryFailureCode);
        Assert.Equal(CleaningPathCleanupState.Succeeded, handoff.MotionCleanupState);
        Assert.Equal(CleaningPathCleanupState.Succeeded, handoff.ValveCleanupState);
    }

    [Fact]
    public void No_Unrelated_Wj_Iv_Or_Axis_Operation()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        Assert.Empty(harness.Motion.UnrelatedAxisCommands());
        Assert.All(harness.Equipment.ValveCommands, request => Assert.Equal("IV4", request.ValveId));
    }

    [Fact]
    public void Path_Execution_Is_Composed_Never_Duplicated()
    {
        var harness = RunAcceptedOpenHarness();
        RunAcceptedClose(harness);

        // The Phase 3B execution result is carried through the composition result: one execution,
        // one verified ledger and one typed path handoff naming the fixed Centre contract.
        var pathResult = harness.Composition.Result!.PathResult!;
        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, pathResult.Status);
        Assert.NotNull(pathResult.Handoff);
        Assert.Equal(1250000, pathResult.Handoff!.HorizontalCentreNative);
        Assert.Equal(1250000, pathResult.Handoff!.VerticalCentreNative);
    }
}
