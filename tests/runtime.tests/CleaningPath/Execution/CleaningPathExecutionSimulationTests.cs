using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The required Phase 3B Simulation scenario matrix (1-30), executed against the real Manual
/// Motion session and the deterministic simulated port through the typed boundary and the
/// execution coordinator. Every value is synthetic test data; a simulated result is never
/// hardware evidence. No Centre return, Pump or Isolation Valve behavior exists in this scope.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathExecutionSimulationTests
{
    private static readonly int[] HorizontalTargets = { 10000, 20000, 30000, 40000, 50000, 60000 };
    private static readonly int[] VerticalTargets = { 110000, 120000, 130000, 140000, 150000, 160000 };
    private static readonly JobPhase[] AllPoints = { JobPhase.P1, JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 };

    private static void RunToCompletion(CleaningPathExecutionHarness harness)
    {
        harness.OpenAndObserve();
        harness.HomeRoles();
        var started = harness.Start();
        Assert.True(started.Started);
        harness.DriveUntilTerminal();
        Assert.NotNull(harness.Coordinator.Result);
    }

    private static CleaningPathExecutionResult Completed(CleaningPathExecutionHarness harness)
    {
        RunToCompletion(harness);
        return harness.Coordinator.Result!;
    }

    private static void AssertPathCompleted(CleaningPathExecutionResult result)
    {
        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
        Assert.Equal(CleaningPathExecutionCodes.PathCompleteAwaitingSafeReturn, result.Code);
        Assert.Equal(AllPoints, result.Points.Select(point => point.PointId));
    }

    // 1
    [Fact]
    public void P1_Through_P6_Success_Both_Axes_Verified_At_Every_Point()
    {
        var result = Completed(new CleaningPathExecutionHarness());

        AssertPathCompleted(result);
        foreach (var point in result.Points)
        {
            Assert.Equal(CleaningPathPointState.Verified, point.State);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Horizontal.MotionOutcome);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Vertical.MotionOutcome);
        }
    }

    // 2
    [Fact]
    public void Horizontal_Finishes_Before_Vertical()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        harness.DriveUntil(
            () => harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).LastOutcome
                    == CleaningPathMotionOutcome.MoveCompletedPositionVerified
                && harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).MoveActive,
            4000);

        harness.DriveUntilTerminal();
        AssertPathCompleted(harness.Coordinator.Result!);
    }

    // 3
    [Fact]
    public void Vertical_Finishes_Before_Horizontal()
    {
        var harness = new CleaningPathExecutionHarness(horizontalSpeed: 100000, verticalSpeed: 400000);
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        harness.DriveUntil(
            () => harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).LastOutcome
                    == CleaningPathMotionOutcome.MoveCompletedPositionVerified
                && harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).MoveActive,
            4000);

        harness.DriveUntilTerminal();
        AssertPathCompleted(harness.Coordinator.Result!);
    }

    // 4
    [Fact]
    public void Deterministic_Bounded_Start_Skew()
    {
        var harness = new CleaningPathExecutionHarness();
        RunToCompletion(harness);

        var horizontalBegin = harness.BeginIndexOf(CleaningPathExecutionRole.Horizontal);
        var verticalBegin = harness.BeginIndexOf(CleaningPathExecutionRole.Vertical);
        Assert.True(horizontalBegin >= 0);
        Assert.True(verticalBegin >= 0);

        // Deterministic request order (Horizontal then Vertical) through one serialized owner: the
        // Horizontal Begin always precedes the Vertical Begin by at most two serialized call slots
        // (one fair-rotation gap plus the settle-gate offset). No synchronized Begin exists.
        Assert.True(horizontalBegin < verticalBegin);
        Assert.True(verticalBegin - horizontalBegin <= 2, $"start skew {verticalBegin - horizontalBegin} call slots exceeded the bounded contract");

        var requestEvents = harness.Coordinator.Result!.Events
            .Where(e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested && e.PointId == JobPhase.P1)
            .ToList();
        Assert.Equal(CleaningPathExecutionRole.Horizontal, requestEvents[0].Role);
        Assert.Equal(CleaningPathExecutionRole.Vertical, requestEvents[1].Role);
    }

    // 5
    [Fact]
    public void Pn_Plus_One_Is_Blocked_Until_Both_Axes_Verify_Pn()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        // P1: Horizontal verified while Vertical is still moving; P2 must not be requested.
        harness.DriveUntil(
            () => harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).LastOutcome
                    == CleaningPathMotionOutcome.MoveCompletedPositionVerified
                && harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).MoveActive,
            4000);

        Assert.Equal(0, harness.Coordinator.Current!.CurrentPointIndex);
        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute))
        {
            Assert.NotEqual(HorizontalTargets[1], command.Operand);
            Assert.NotEqual(VerticalTargets[1], command.Operand);
        }

        harness.DriveUntilTerminal();
        AssertPathCompleted(harness.Coordinator.Result!);
    }

    // 6
    [Fact]
    public void No_Point_Is_Skipped()
    {
        var result = Completed(new CleaningPathExecutionHarness());

        AssertPathCompleted(result);
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal((JobPhase)index, result.Points[index].PointId);
            Assert.Equal(HorizontalTargets[index], result.Points[index].HorizontalNative);
            Assert.Equal(VerticalTargets[index], result.Points[index].VerticalNative);
        }
    }

    // 7
    [Fact]
    public void No_Verified_Point_Is_Repeated()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = Completed(harness);

        AssertPathCompleted(result);
        Assert.Equal(6, result.Points.Count);
        Assert.Equal(result.Points.Select(point => point.PointId).Distinct().Count(), result.Points.Count);
        Assert.Equal(result.Points.Count, harness.Coordinator.Current!.VerifiedLedger.Count);
    }

    // 8
    [Fact]
    public void Horizontal_Timeout_While_Vertical_Remains_Active()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { MotionStalled = true }),
            verticalSpeed: 4000,
            horizontalMoveTimeoutMilliseconds: 3000);
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        // The stalled Horizontal times out while the slow Vertical is still moving.
        harness.DriveUntil(
            () => harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).LastOutcome == CleaningPathMotionOutcome.MoveTimeout
                && harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).MoveActive,
            4000);

        harness.DriveUntilTerminal();
        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathPointState.Failed, result.Points[0].State);
    }

    // 9
    [Fact]
    public void Vertical_Timeout_While_Horizontal_Remains_Active()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("D", new ManualMotionAxisSimulationOptions { MotionStalled = true }),
            horizontalSpeed: 2000,
            verticalMoveTimeoutMilliseconds: 3000);
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        harness.DriveUntil(
            () => harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).LastOutcome == CleaningPathMotionOutcome.MoveTimeout
                && harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).MoveActive,
            4000);

        harness.DriveUntilTerminal();
        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
    }

    // 10
    [Fact]
    public void Fault_Before_Begin_Is_Preserved_And_Fail_Closed()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                FaultOnCommand = ManualMotionPortCommandKind.Acceleration,
            }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("COMMUNICATION_FAULT", result.Points[0].FailureCode);
        Assert.DoesNotContain(
            result.Points,
            point => point.State == CleaningPathPointState.Verified);
    }

    // 11
    [Fact]
    public void Fault_After_Begin_Is_Preserved_And_Fail_Closed()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                FaultOnReadWhileMoving = true,
            }));
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);

        // Motion has begun on Horizontal before the read fault faults the session.
        harness.DriveUntil(() => harness.Boundary.SessionFaulted, 4000);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("COMMUNICATION_FAULT", result.Points[0].FailureCode);
    }

    // 12
    [Fact]
    public void TP_RP_Contradiction_On_Horizontal_Is_Refused()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { RpBias = 5000m }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathMotionOutcome.MoveTimeout, result.Points[0].Horizontal.MotionOutcome);
    }

    // 13
    [Fact]
    public void TP_RP_Contradiction_On_Vertical_Is_Refused()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("D", new ManualMotionAxisSimulationOptions { RpBias = 5000m }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathMotionOutcome.MoveTimeout, result.Points[0].Vertical.MotionOutcome);
    }

    // 14
    [Fact]
    public void Stale_Observation_On_One_Axis_Is_Refused()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                RefuseReadKindWhileMoving = ManualMotionReadKind.PositionTp,
            }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.DoesNotContain(
            result.Points,
            point => point.State == CleaningPathPointState.Verified);
    }

    // 15
    [Fact]
    public void Successful_Supervised_Stop_Of_Remaining_Axis()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { MotionStalled = true }),
            verticalSpeed: 4000,
            horizontalMoveTimeoutMilliseconds: 3000);
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathCleanupState.Succeeded, result.CleanupState);
        Assert.Equal(CleaningPathMotionOutcome.MoveAbortedByOperator, result.Points[0].Vertical.MotionOutcome);
        Assert.Equal(CleaningPathMotionOutcome.StopIssued, result.Points[0].Vertical.CleanupOutcome);
        Assert.True(result.Points[0].Vertical.StopRequestedOnce);
    }

    // 16
    [Fact]
    public void Stop_Not_Observed_For_Remaining_Axis_Is_Preserved()
    {
        var harness = new CleaningPathExecutionHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisC = new ManualMotionAxisSimulationOptions { MotionStalled = true },
                AxisD = new ManualMotionAxisSimulationOptions { StopIgnored = true },
            },
            verticalSpeed: 4000,
            horizontalMoveTimeoutMilliseconds: 3000);
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathMotionOutcome.StopNotObservedWithinTimeout, result.Points[0].Vertical.CleanupOutcome);
        Assert.Equal(CleaningPathCleanupState.Failed, result.CleanupState);
    }

    // 17
    [Fact]
    public void Brake_Apply_Only_After_Inactive_Observation()
    {
        var harness = new CleaningPathExecutionHarness();
        RunToCompletion(harness);

        // For each axis the per-axis command stream is six cycles: brake release, motion commands,
        // Begin, and exactly one brake apply after that Begin (the session applies the brake only
        // after the inactive observation) and before the next cycle's Begin.
        foreach (var letter in new[] { "C", "D" })
        {
            var begins = new List<int>();
            var applies = new List<int>();
            for (var index = 0; index < harness.Port.ExecutedCommands.Count; index++)
            {
                var command = harness.Port.ExecutedCommands[index];
                if (CleaningPathExecutionHarness.LetterOf(command.Axis) != letter)
                {
                    continue;
                }

                if (command.Kind == ManualMotionPortCommandKind.Begin)
                {
                    begins.Add(index);
                }

                if (command.Kind == ManualMotionPortCommandKind.BrakeOutputClear)
                {
                    applies.Add(index);
                }
            }

            Assert.Equal(6, begins.Count);

            // The setup Home of this axis also applies its brake; only the six applies after the
            // first Move Begin belong to path execution.
            var pathApplies = applies.Where(index => index > begins[0]).ToList();
            Assert.Equal(6, pathApplies.Count);
            for (var cycle = 0; cycle < 6; cycle++)
            {
                Assert.True(begins[cycle] < pathApplies[cycle]);
                if (cycle < 5)
                {
                    Assert.True(pathApplies[cycle] < begins[cycle + 1]);
                }
            }
        }
    }

    // 18
    [Fact]
    public void Brake_Apply_Refusal_Is_Fail_Closed_With_Motion_Result_Preserved()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                RefuseCommand = ManualMotionPortCommandKind.BrakeOutputClear,
            }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("BRAKE_APPLY_REFUSED", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathPointState.CleanupFailed, result.Points[0].State);

        // The motion result is preserved unchanged; the cleanup failure never converts it to a
        // verified point and never erases the typed per-axis motion outcome.
        Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, result.Points[0].Horizontal.MotionOutcome);
        Assert.Equal(CleaningPathMotionOutcome.BrakeApplyRefused, result.Points[0].Horizontal.CleanupOutcome);
    }

    // 19
    [Fact]
    public void Abort_During_One_Point_Stops_Each_Active_Axis_Once()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);
        harness.Drive(40);

        harness.Coordinator.Abort();
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal(CleaningPathExecutionCodes.PathAborted, result.Code);
        Assert.Single(result.Points);
        Assert.Equal(JobPhase.P1, result.Points[0].PointId);
        Assert.True(result.Points[0].Horizontal.StopRequestedOnce || result.Points[0].Vertical.StopRequestedOnce);
        Assert.DoesNotContain(result.Events, e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested && e.PointId == JobPhase.P2);
    }

    // 20
    [Fact]
    public void Application_Close_During_One_Point_Stops_Each_Active_Axis_Once()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);
        harness.Drive(40);

        harness.Coordinator.RequestClose();
        harness.DriveUntilTerminal();
        harness.Drive(20);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal(CleaningPathExecutionCodes.PathClosed, result.Code);
        Assert.DoesNotContain(result.Events, e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested && e.PointId == JobPhase.P2);
        Assert.Equal(ManualMotionSessionState.Closed, harness.Session.Snapshot.State);
    }

    // 21
    [Fact]
    public void Invalid_Or_Refused_Phase3A_Snapshot_Cannot_Start()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();

        var refusedStore = CleaningPathStoreParser.Parse("{ not json");
        Assert.Null(refusedStore.Snapshot);
        var byRefusedStore = harness.Coordinator.Start(refusedStore.Snapshot, harness.Resolution);
        Assert.False(byRefusedStore.Started);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedSnapshotNotAccepted, byRefusedStore.Code);

        var wrongSource = CleaningPathSnapshot.Create(
            CleaningPathStoreParser.SchemaIdentifier,
            CleaningPathSourceKind.LegacyMigrationInspection,
            new[] { harness.StoreSnapshot.PathFor(CleaningPathExecutionHarness.Sensor)! },
            null);
        var byWrongSource = harness.Coordinator.Start(wrongSource, harness.Resolution);
        Assert.False(byWrongSource.Started);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedSnapshotNotAccepted, byWrongSource.Code);

        var mismatchedConfiguration = SyntheticCleaningPathStores.SensorConfiguration("G1", "WJ4", "IV4");
        var refusedResolution = CleaningPathResolver.Resolve(
            harness.StoreSnapshot,
            mismatchedConfiguration,
            new CleaningPathResolutionRequest { SensorId = CleaningPathExecutionHarness.Sensor });
        Assert.False(refusedResolution.Accepted);
        var byRefusedResolution = harness.Coordinator.Start(harness.StoreSnapshot, refusedResolution);
        Assert.False(byRefusedResolution.Started);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedResolutionNotAccepted, byRefusedResolution.Code);
    }

    // 22
    [Fact]
    public void Out_Of_Range_Target_Cannot_Enter_Execution()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();

        var source = harness.StoreSnapshot.PathFor(CleaningPathExecutionHarness.Sensor)!;
        var points = new List<CleaningPathPoint>();
        for (var index = 0; index < 6; index++)
        {
            var point = source.Points[index];
            points.Add(index == 0
                ? new CleaningPathPoint
                {
                    PointId = point.PointId,
                    HorizontalMillimetres = point.HorizontalMillimetres,
                    VerticalMillimetres = point.VerticalMillimetres,
                    HorizontalNative = CleaningPathUnits.MaximumNative + 1,
                    VerticalNative = point.VerticalNative,
                }
                : point);
        }

        var outOfRange = CleaningPathSnapshot.Create(
            CleaningPathStoreParser.SchemaIdentifier,
            CleaningPathSourceKind.OwnerLocalFile,
            new[] { new SensorCleaningPath { SensorId = source.SensorId, Points = points } },
            null);
        var outcome = harness.Coordinator.Start(outOfRange, harness.Resolution);
        Assert.False(outcome.Started);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedTargetOutOfRange, outcome.Code);
    }

    // 23
    [Fact]
    public void Effective_WJ_Override_Does_Not_Mutate_P1_Through_P6()
    {
        var harness = new CleaningPathExecutionHarness(assignedWaterJetId: "WJ2", assignedIsolationValveId: "IV2", overrideWaterJetId: "WJ4");
        var pathBefore = harness.StoreSnapshot.PathFor(CleaningPathExecutionHarness.Sensor)!;
        var result = Completed(harness);

        AssertPathCompleted(result);
        Assert.Equal("WJ2", harness.Resolution.Resolution!.CanonicalAssignedWaterJetId);
        Assert.Equal("WJ4", harness.Resolution.Resolution!.EffectiveWaterJetId);
        Assert.Same(pathBefore, harness.StoreSnapshot.PathFor(CleaningPathExecutionHarness.Sensor));
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal(pathBefore.Points[index].HorizontalNative, result.Points[index].HorizontalNative);
            Assert.Equal(pathBefore.Points[index].VerticalNative, result.Points[index].VerticalNative);
        }
    }

    // 24
    [Fact]
    public void Resolver_And_Topology_Result_Remain_Immutable()
    {
        var harness = new CleaningPathExecutionHarness();
        var resolutionBefore = harness.Resolution;
        var resolvedBefore = harness.Resolution.Resolution!;
        RunToCompletion(harness);

        Assert.Same(resolutionBefore, harness.Resolution);
        Assert.Same(resolvedBefore, harness.Resolution.Resolution);
        Assert.Equal(resolvedBefore, harness.Resolution.Resolution);
        Assert.Equal("IV4", harness.Resolution.Resolution!.DerivedIsolationValveId);
    }

    // 25
    [Fact]
    public void No_Unrelated_Axis_Operation_Occurs()
    {
        var harness = new CleaningPathExecutionHarness();
        RunToCompletion(harness);

        Assert.Empty(harness.UnrelatedAxisCommands());
    }

    // 26
    [Fact]
    public void Combined_Point_Evidence_Preserves_Both_Per_Axis_Results()
    {
        var result = Completed(new CleaningPathExecutionHarness());

        foreach (var point in result.Points)
        {
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Horizontal.MotionOutcome);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Vertical.MotionOutcome);
            Assert.False(string.IsNullOrEmpty(point.Horizontal.MotionOutcomeSourceCode));
            Assert.False(string.IsNullOrEmpty(point.Vertical.MotionOutcomeSourceCode));
        }
    }

    // 27
    [Fact]
    public void Final_Successful_State_Is_PathCompleteAwaitingSafeReturn()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = Completed(harness);

        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
        Assert.NotNull(result.Handoff);
        Assert.Equal(CleaningPathExecutionHarness.Sensor, result.Handoff!.SensorId);
        Assert.Equal("WJ4", result.Handoff!.EffectiveWaterJetId);
        Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, result.Handoff!.HorizontalFinalOutcome);
        Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, result.Handoff!.VerticalFinalOutcome);
    }

    // 28
    [Fact]
    public void No_Centre_Move_Occurs()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = Completed(harness);

        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute))
        {
            Assert.NotEqual(1250000, command.Operand);
        }

        Assert.Equal(1250000, result.Handoff!.HorizontalCentreNative);
        Assert.Equal(1250000, result.Handoff!.VerticalCentreNative);
    }

    // 29
    [Fact]
    public void No_Pump_Or_Isolation_Valve_Operation_Occurs()
    {
        var harness = new CleaningPathExecutionHarness();
        RunToCompletion(harness);

        // The typed boundary exposes motion operations only: every dispatched command is one of the
        // closed Manual Motion kinds, and nothing else is reachable from this scope.
        var motionKinds = new HashSet<ManualMotionPortCommandKind>
        {
            ManualMotionPortCommandKind.BrakeOutputSet,
            ManualMotionPortCommandKind.BrakeOutputClear,
            ManualMotionPortCommandKind.Acceleration,
            ManualMotionPortCommandKind.Speed,
            ManualMotionPortCommandKind.Deceleration,
            ManualMotionPortCommandKind.PositionAbsolute,
            ManualMotionPortCommandKind.Begin,
            ManualMotionPortCommandKind.Stop,
            ManualMotionPortCommandKind.HomeSpeed,
            ManualMotionPortCommandKind.Home,
            ManualMotionPortCommandKind.DefinePositionZero,
        };
        foreach (var command in harness.Port.ExecutedCommands)
        {
            Assert.Contains(command.Kind, motionKinds);
        }
    }

    // 30
    [Fact]
    public void TP_RP_Divergence_Remains_Fail_Closed()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { RpBias = 5000m }));
        RunToCompletion(harness);

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.DoesNotContain(
            result.Points,
            point => point.State == CleaningPathPointState.Verified);
        Assert.Empty(harness.Coordinator.Current!.VerifiedLedger);
        Assert.Null(result.Handoff);
    }
}

/// <summary>
/// Focused regression coverage for the three proven Phase 3B integration defect classes: the
/// missing Cleaning Path execution evidence window, sticky prior-axis outcomes not fenced from the
/// current Move, and per-axis simulation options activated before Home. The original 30 simulation
/// scenarios and 4 coordinator tests remain unchanged; these cases pin the repaired contracts.
/// Simulation only; every value is synthetic; a simulated result is never hardware evidence.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathExecutionRegressionTests
{
    private const string HomeCompletionCode = "HOME_COMPLETED_ZERO_VERIFIED";

    private static ManualMotionAxisSnapshot AxisOf(CleaningPathExecutionHarness harness, ManualMotionAxis axis)
    {
        foreach (var candidate in harness.Session.Snapshot.Axes)
        {
            if (candidate.Axis == axis)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("the expected axis is not present in the session snapshot");
    }

    private static CleaningPathExecutionResult RunToCompletion(CleaningPathExecutionHarness harness)
    {
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.True(harness.Start().Started);
        harness.DriveUntilTerminal();
        return harness.Coordinator.Result!;
    }

    // 1 — HOME EVENT WINDOW
    [Fact]
    public void Home_Evidence_Is_Never_Published_As_Execution_Move_Evidence()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = RunToCompletion(harness);

        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
        foreach (var executionEvent in result.Events)
        {
            Assert.DoesNotContain(HomeCompletionCode, executionEvent.SourceCode ?? string.Empty);
        }

        Assert.Equal(12, result.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindMoveAccepted));
        Assert.All(
            result.Events.Where(e => e.Kind == CleaningPathExecutionCodes.KindMoveAccepted),
            e => Assert.Equal("OPERATION_STARTED", e.SourceCode));
    }

    // 2 — STICKY HOME OUTCOME
    [Fact]
    public void Sticky_Home_Outcome_Is_Fenced_From_P1_Move()
    {
        var harness = new CleaningPathExecutionHarness();
        harness.OpenAndObserve();
        harness.HomeRoles();

        // The session intentionally keeps the historical Home result on both axes.
        Assert.Equal(HomeCompletionCode, AxisOf(harness, ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(HomeCompletionCode, AxisOf(harness, ManualMotionAxis.D).LastOutcomeCode);

        Assert.True(harness.Start().Started);
        harness.Drive(8);

        // The execution-scoped outcome knows only the current Move; the sticky Home result can
        // never be adopted as the P1 Move outcome, and the session history is left unchanged.
        Assert.True(
            harness.Boundary.Frame(CleaningPathExecutionRole.Horizontal).LastOutcome
                is CleaningPathMotionOutcome.None
                or CleaningPathMotionOutcome.OperationStarted,
            "a historical Home outcome must never appear as the current Move outcome");
        Assert.True(
            harness.Boundary.Frame(CleaningPathExecutionRole.Vertical).LastOutcome
                is CleaningPathMotionOutcome.None
                or CleaningPathMotionOutcome.OperationStarted,
            "a historical Home outcome must never appear as the current Move outcome");
        Assert.Equal(HomeCompletionCode, AxisOf(harness, ManualMotionAxis.C).LastOutcomeCode);
    }

    // 3 — MOVE EVIDENCE AFTER WINDOW
    [Fact]
    public void Move_Evidence_After_Window_Is_Consumed_Normally()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = RunToCompletion(harness);

        Assert.Equal(12, result.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindMoveRequested));
        Assert.Equal(12, result.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindMoveAccepted));
        Assert.Equal(6, result.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindExecutionPhaseVerified));
        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
    }

    // 4 — SESSION FAULT AFTER WINDOW
    [Fact]
    public void Session_Fault_After_Window_Preserves_The_Current_Failure()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                FaultOnCommand = ManualMotionPortCommandKind.Acceleration,
            }));
        var result = RunToCompletion(harness);

        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("COMMUNICATION_FAULT", result.Points[0].FailureCode);
    }

    // 5 — MOTION STALL DELAYED ACTIVATION
    [Fact]
    public void Motion_Stall_Activates_After_Home_Only()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { MotionStalled = true }),
            horizontalMoveTimeoutMilliseconds: 3000);
        harness.OpenAndObserve();
        harness.HomeRoles();

        // Home completed normally before activation.
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.C).HomeState);
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.D).HomeState);

        Assert.True(harness.Start().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.C).HomeState);
    }

    // 6 — RP BIAS DELAYED ACTIVATION
    [Fact]
    public void RP_Bias_Activates_After_Home_Only()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions { RpBias = 5000m }));
        harness.OpenAndObserve();
        harness.HomeRoles();

        // Home and its TP/RP zero verification completed normally before activation.
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.C).HomeState);
        Assert.Equal(ManualMotionCoordinateState.ZeroVerified, AxisOf(harness, ManualMotionAxis.C).CoordinateState);

        Assert.True(harness.Start().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Empty(result.Points.Where(point => point.State == CleaningPathPointState.Verified));
    }

    // 7 — READ FAULT DELAYED ACTIVATION
    [Fact]
    public void Read_Fault_Activates_After_Home_Only()
    {
        var harness = new CleaningPathExecutionHarness(
            options: CleaningPathExecutionHarness.OptionsWith("C", new ManualMotionAxisSimulationOptions
            {
                FaultOnReadWhileMoving = true,
            }));
        harness.OpenAndObserve();
        harness.HomeRoles();
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.C).HomeState);

        Assert.True(harness.Start().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("COMMUNICATION_FAULT", result.Points[0].FailureCode);
    }

    // 8 — STOP-IGNORED DELAYED ACTIVATION
    [Fact]
    public void Stop_Ignored_Activates_After_Home_Only()
    {
        var harness = new CleaningPathExecutionHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisC = new ManualMotionAxisSimulationOptions { MotionStalled = true },
                AxisD = new ManualMotionAxisSimulationOptions { StopIgnored = true },
            },
            verticalSpeed: 4000,
            horizontalMoveTimeoutMilliseconds: 3000);
        harness.OpenAndObserve();
        harness.HomeRoles();

        // Home's internal completion Stop was effective before activation (Homed proves it).
        Assert.Equal(ManualMotionHomeState.Homed, AxisOf(harness, ManualMotionAxis.D).HomeState);

        Assert.True(harness.Start().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal("MOVE_TIMEOUT", result.Points[0].FailureCode);
        Assert.Equal(CleaningPathMotionOutcome.StopNotObservedWithinTimeout, result.Points[0].Vertical.CleanupOutcome);
        Assert.Equal(CleaningPathCleanupState.Failed, result.CleanupState);
    }

    // 9 — NO HISTORY DELETION
    [Fact]
    public void Manual_Motion_History_Is_Never_Deleted()
    {
        var harness = new CleaningPathExecutionHarness();
        RunToCompletion(harness);

        Assert.Contains(
            harness.Session.Snapshot.Log,
            entry => entry.Code == HomeCompletionCode);
    }

    // 10 — CURRENT P6 HANDOFF
    [Fact]
    public void P6_Handoff_Is_Reached_Without_Historical_Contamination()
    {
        var harness = new CleaningPathExecutionHarness();
        var result = RunToCompletion(harness);

        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
        Assert.NotNull(result.Handoff);
        Assert.Equal(6, result.Points.Count);
        Assert.All(result.Points, point => Assert.Equal(CleaningPathPointState.Verified, point.State));
        Assert.Equal(1250000, result.Handoff!.HorizontalCentreNative);
        Assert.Equal(1250000, result.Handoff!.VerticalCentreNative);
        foreach (var executionEvent in result.Events)
        {
            Assert.DoesNotContain(HomeCompletionCode, executionEvent.SourceCode ?? string.Empty);
        }
    }
}
