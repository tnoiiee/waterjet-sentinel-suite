using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.CleaningPath.H1;
using Wjss.Runtime.Core.Motion.Manual;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Focused deterministic tests of the H1 dry Cleaning Path commissioning run on WSB1, through the
/// typed Manual Motion and Cleaning Path boundaries over the simulated port. Commissioning is two
/// explicitly separated actions: Commission/Home (the typed Home lifecycle and coordinate
/// establishment) and Run Path (P1 through P6 plus the dry Centre return, which never dispatches
/// Home and requires the session-scoped coordinate readiness). Every value is synthetic test
/// data; a simulated result is never hardware evidence. The deeper motion mechanics
/// (MoveReached, TP/RP acceptance, freshness, brake lifecycle, stop supervision) are protected by
/// the existing Manual Motion and Phase 3B suites, which these tests consume unchanged.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class H1DryCommissioningCoordinatorTests
{
    // ------------------------------------------------------------------ identity and authority

    [Fact]
    public void Start_Admits_Wsb1_Selected_Sensor()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        var outcome = harness.CommissionThenStartPath();
        Assert.True(outcome.Started);
        Assert.Equal(H1DryCommissioningCodes.KindPathRunStarted, outcome.Code);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Completed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunComplete, result.Code);
        Assert.Equal(H1CabinetAdmission.Wsb1Admitted, result.CabinetAdmission);
        Assert.Equal(H1DryCommissioningHarness.Sensor, result.Identity.SensorId);
        Assert.Equal(H1DryCommissioningHarness.Wsb1WaterJetId, result.Identity.EffectiveWaterJetId);
        Assert.Equal(H1DryCommissioningHarness.Wsb1ValveId, result.Identity.DerivedIsolationValveId);
    }

    [Fact]
    public void Start_Refuses_Selected_Water_Jet_Outside_Wsb1()
    {
        var harness = new H1DryCommissioningHarness(
            sensor: H1DryCommissioningHarness.UnsupportedCabinetSensor,
            assignedWaterJetId: H1DryCommissioningHarness.OtherCabinetWaterJetId,
            assignedIsolationValveId: H1DryCommissioningHarness.OtherCabinetValveId);
        harness.OpenAndObserve();

        var outcome = harness.StartPath();
        Assert.False(outcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedUnsupportedWsb, outcome.Code);
        var commissionOutcome = harness.StartCommission();
        Assert.False(commissionOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedUnsupportedWsb, commissionOutcome.Code);
        Assert.Null(harness.Coordinator.Result);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void Identity_Consumes_Canonical_Topology_Route()
    {
        var harness = new H1DryCommissioningHarness();
        var resolved = harness.Resolution.Resolution!;

        // The canonical Motion topology catalog is the sole authority; the resolution consumes it.
        Assert.Equal("WSB1", resolved.CabinetId);
        Assert.Equal("MC-WSB1", resolved.ControllerId);
        Assert.Equal("A", resolved.HorizontalAxisId);
        Assert.Equal("B", resolved.VerticalAxisId);
        Assert.Equal(EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT, resolved.EffectiveWjProvenance);
    }

    [Fact]
    public void Validated_Override_Construction_Admits_Run_With_Provenance()
    {
        var harness = new H1DryCommissioningHarness(overrideWaterJetId: H1DryCommissioningHarness.Wsb1WaterJetId);
        harness.OpenAndObserve();

        var outcome = harness.CommissionThenStartPath();
        Assert.True(outcome.Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Completed, result.TerminalPhase);
        Assert.Equal(EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE, result.Identity.EffectiveWjProvenance);
        Assert.Equal(H1DryCommissioningHarness.Wsb1WaterJetId, result.Identity.EffectiveWaterJetId);
    }

    [Fact]
    public void Override_Does_Not_Mutate_P1_To_P6_Targets()
    {
        var harness = new H1DryCommissioningHarness(overrideWaterJetId: H1DryCommissioningHarness.Wsb1WaterJetId);
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();

        var expected = harness.StoreSnapshot.PathFor(H1DryCommissioningHarness.Sensor)!.Points;
        var points = harness.Coordinator.Result!.PathResult!.Points;
        Assert.Equal(6, points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            Assert.Equal(expected[index].HorizontalNative, points[index].HorizontalNative);
            Assert.Equal(expected[index].VerticalNative, points[index].VerticalNative);
        }
    }

    [Fact]
    public void Identity_Remains_Immutable_During_Run()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);

        var midRun = harness.Coordinator.Current!.Identity;
        harness.DriveUntilTerminal();
        var final = harness.Coordinator.Result!.Identity;

        Assert.Equal(final.SensorId, midRun.SensorId);
        Assert.Equal(final.CanonicalWaterJetId, midRun.CanonicalWaterJetId);
        Assert.Equal(final.EffectiveWaterJetId, midRun.EffectiveWaterJetId);
        Assert.Equal(final.CabinetId, midRun.CabinetId);
        Assert.Equal(final.HorizontalAxisId, midRun.HorizontalAxisId);
        Assert.Equal(final.VerticalAxisId, midRun.VerticalAxisId);
    }

    [Fact]
    public void Run_Uses_No_Unrelated_Axis_Operation()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();

        Assert.Equal(H1DryCommissioningPhase.Completed, harness.Coordinator.Result!.TerminalPhase);
        Assert.Empty(harness.UnrelatedAxisCommands());
    }

    // -------------------------------------------------------------------------- preconditions

    [Fact]
    public void Start_Refuses_When_Controller_Identity_Not_Verified()
    {
        AssertStartRefusedWith(
            guards => guards with { ControllerIdentityVerified = false },
            H1DryCommissioningCodes.StartRefusedControllerIdentityNotVerified);
    }

    [Fact]
    public void Start_Refuses_When_Endpoint_Not_Configured()
    {
        AssertStartRefusedWith(
            guards => guards with { ControllerEndpointConfigured = false },
            H1DryCommissioningCodes.StartRefusedEndpointNotConfigured);
    }

    [Fact]
    public void Start_Refuses_When_Profile_Not_Configured()
    {
        AssertStartRefusedWith(
            guards => guards with { OwnerLocalProfileConfigured = false },
            H1DryCommissioningCodes.StartRefusedProfileNotConfigured);
    }

    [Fact]
    public void Start_Refuses_When_Dry_Mode_Not_Declared()
    {
        AssertStartRefusedWith(
            guards => guards with { DryModeDeclared = false },
            H1DryCommissioningCodes.StartRefusedDryModeNotDeclared);
    }

    [Fact]
    public void Start_Refuses_When_Pump_Command_Path_Available()
    {
        AssertStartRefusedWith(
            guards => guards with { PumpCommandPathUnavailable = false },
            H1DryCommissioningCodes.StartRefusedPumpCommandPathAvailable);
    }

    [Fact]
    public void Start_Refuses_When_Physical_Valve_Command_Path_Available()
    {
        AssertStartRefusedWith(
            guards => guards with { PhysicalValveCommandPathUnavailable = false },
            H1DryCommissioningCodes.StartRefusedPhysicalValveCommandPathAvailable);
    }

    [Fact]
    public void Start_Refuses_When_Queue_AutoSequence_Available()
    {
        AssertStartRefusedWith(
            guards => guards with { QueueAutoSequenceUnavailable = false },
            H1DryCommissioningCodes.StartRefusedQueueAutoSequenceAvailable);
    }

    [Fact]
    public void Start_Refuses_When_Physical_Abort_Not_Accessible()
    {
        AssertStartRefusedWith(
            guards => guards with { PhysicalAbortAccessible = false },
            H1DryCommissioningCodes.StartRefusedPhysicalAbortNotAccessible);
    }

    [Fact]
    public void Start_Refuses_When_Operator_Observer_Not_Present()
    {
        AssertStartRefusedWith(
            guards => guards with { OperatorObserverPresent = false },
            H1DryCommissioningCodes.StartRefusedOperatorObserverNotPresent);
    }

    [Fact]
    public void Start_Refuses_When_Observation_Not_Current()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenWithoutObservationCadence();

        var pathOutcome = harness.StartPath();
        Assert.False(pathOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedObservationNotFresh, pathOutcome.Code);
        var commissionOutcome = harness.StartCommission();
        Assert.False(commissionOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedObservationNotFresh, commissionOutcome.Code);
    }

    [Fact]
    public void Start_Refuses_When_Axis_Operation_Active()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.Drive(2);

        var pathOutcome = harness.StartPath();
        Assert.False(pathOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedAxisOperationActive, pathOutcome.Code);
        var commissionOutcome = harness.StartCommission();
        Assert.False(commissionOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedAxisOperationActive, commissionOutcome.Code);
    }

    [Fact]
    public void Start_Refuses_While_Run_Active()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);

        var pathOutcome = harness.StartPath();
        Assert.False(pathOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedRunActive, pathOutcome.Code);
        var commissionOutcome = harness.StartCommission();
        Assert.False(commissionOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedRunActive, commissionOutcome.Code);
    }

    [Fact]
    public void Start_Refuses_Incomplete_Request()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        var request = new H1DryCommissioningRequest
        {
            PathStore = null,
            Resolution = null,
            SelectedSensorId = string.Empty,
            Guards = H1DryCommissioningHarness.AllGuardsTrue,
        };

        var pathOutcome = harness.Coordinator.StartPath(request);
        Assert.False(pathOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedRequestIncomplete, pathOutcome.Code);
        var commissionOutcome = harness.Coordinator.StartCommission(request);
        Assert.False(commissionOutcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedRequestIncomplete, commissionOutcome.Code);
    }

    // --------------------------------------------------------------------- Home and DP / DE

    [Fact]
    public void Both_Axes_Home_With_Typed_Position_Zero_Verified()
    {
        var harness = CommissionedHarness();

        var result = harness.Coordinator.Result!;
        Assert.True(result.HorizontalCoordinate.HomeVerified);
        Assert.True(result.VerticalCoordinate.HomeVerified);
        Assert.True(result.HorizontalCoordinate.CoordinateZeroVerified);
        Assert.True(result.VerticalCoordinate.CoordinateZeroVerified);
        Assert.Equal(ManualMotionCodes.HomeCompletedZeroVerified, result.HorizontalCoordinate.HomeOutcomeCode);
        Assert.Equal(ManualMotionCodes.HomeCompletedZeroVerified, result.VerticalCoordinate.HomeOutcomeCode);

        // The accepted Home contract carries the Owner-approved reset-encoder composition per axis:
        // the typed position-zero definition exactly once, then the typed dual-zero definition.
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefinePositionZero).Count);
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero).Count);

        // Commission/Home is separate from Run Path: no path Move and no path result exists.
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
        Assert.Null(result.PathResult);
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
    }

    [Fact]
    public void Home_Carried_Sequence_Does_Not_Depend_On_The_Standalone_Dual_Zero_Permission()
    {
        // The Owner's Hardware profile declares dualZeroPermitted=false on every axis. Gating the
        // Home-carried reset-encoder composition on that permission would leave the repaired Hardware
        // run emitting the position definition alone and reproduce the proven defect, so this test
        // asserts the complete pair under exactly that condition.
        var harness = new H1DryCommissioningHarness(dualZeroPermitted: false);
        Assert.False(((ManualMotionAxisProfile)harness.Profile.AxisA!).DualZeroPermitted);
        Assert.False(((ManualMotionAxisProfile)harness.Profile.AxisB!).DualZeroPermitted);

        harness.OpenAndObserve();
        harness.CommissionOnly();

        // H1 still commissions and still verifies zero from both reads.
        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Commissioned, result.TerminalPhase);
        Assert.True(result.HorizontalCoordinate.CoordinateZeroVerified);
        Assert.True(result.VerticalCoordinate.CoordinateZeroVerified);
        Assert.Equal(H1DryCommissioningCodes.DualZeroDispatchedInHomeSequence, result.HorizontalCoordinate.DualZeroDispositionCode);
        Assert.Equal(H1DryCommissioningCodes.DualZeroDispatchedInHomeSequence, result.VerticalCoordinate.DualZeroDispositionCode);

        // The canonical WSB1 route resolves Horizontal to axis A and Vertical to axis B; only those
        // two roles are homed by this run.
        Assert.Equal("A", result.HorizontalCoordinate.AxisId);
        Assert.Equal("B", result.VerticalCoordinate.AxisId);

        // Exactly one DP and one DE per resolved axis, DP immediately followed by DE, in the Legacy
        // order, with no other command between them.
        foreach (var letter in new[] { "A", "B" })
        {
            var kinds = new List<ManualMotionPortCommandKind>();
            var indices = new List<int>();
            for (var index = 0; index < harness.Port.ExecutedCommands.Count; index++)
            {
                var command = harness.Port.ExecutedCommands[index];
                if (!string.Equals(CleaningPathExecutionHarness.LetterOf(command.Axis), letter, StringComparison.Ordinal))
                {
                    continue;
                }

                if (command.Kind == ManualMotionPortCommandKind.DefinePositionZero
                    || command.Kind == ManualMotionPortCommandKind.DefineDualZero)
                {
                    indices.Add(index);
                    kinds.Add(command.Kind);
                }
            }

            Assert.Equal(
                new[] { ManualMotionPortCommandKind.DefinePositionZero, ManualMotionPortCommandKind.DefineDualZero },
                kinds);
            Assert.Equal(indices[0] + 1, indices[1]);
            Assert.Equal(0, harness.Port.ExecutedCommands[indices[0]].Operand);
            Assert.Equal(0, harness.Port.ExecutedCommands[indices[1]].Operand);
        }

        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero).Count);

        // The permission still governs the STANDALONE dual-zero action on this same session: the
        // Home-carried composition is the only thing that stopped reading it.
        var before = harness.Port.ExecutedCommands.Count;
        harness.Session.RequestDefineDualZero(ManualMotionAxis.A);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(25));
        harness.Boundary.Step();
        Assert.Equal(
            ManualMotionCodes.DualZeroNotPermittedByProfile,
            harness.Session.Snapshot.Axes[0].LastOutcomeCode);
        Assert.Equal(before, harness.Port.ExecutedCommands.Count);
    }

    [Fact]
    public void Dual_Zero_Completes_The_Home_Carried_Reset_Encoder_Sequence()
    {
        var harness = CommissionedHarness();

        // Every successfully homed axis carries the Owner-approved composition, and the run records
        // it as dispatched-in-Home-sequence rather than as an optional, unrelated manual define.
        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningCodes.DualZeroDispatchedInHomeSequence, result.HorizontalCoordinate.DualZeroDispositionCode);
        Assert.Equal(H1DryCommissioningCodes.DualZeroDispatchedInHomeSequence, result.VerticalCoordinate.DualZeroDispositionCode);
        Assert.Equal(nameof(ManualMotionDualZeroState.DispatchedUnverified), result.HorizontalCoordinate.DualZeroState);
        Assert.Equal(nameof(ManualMotionDualZeroState.DispatchedUnverified), result.VerticalCoordinate.DualZeroState);
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero).Count);
    }

    [Fact]
    public void Home_Carried_Sequence_Emits_Position_Zero_Then_Dual_Zero_For_Every_Axis()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        // Assert the native rendering and the dispatch order of the pair for every WSB1 axis, not
        // only the interpreter state: the sequence is DP then DE on the same axis, with no other
        // command of that axis between them.
        foreach (var letter in new[] { "A", "B", "C", "D" })
        {
            var indices = new List<int>();
            var kinds = new List<ManualMotionPortCommandKind>();
            for (var index = 0; index < harness.Port.ExecutedCommands.Count; index++)
            {
                var command = harness.Port.ExecutedCommands[index];
                if (!string.Equals(CleaningPathExecutionHarness.LetterOf(command.Axis), letter, StringComparison.Ordinal))
                {
                    continue;
                }

                if (command.Kind == ManualMotionPortCommandKind.DefinePositionZero
                    || command.Kind == ManualMotionPortCommandKind.DefineDualZero)
                {
                    indices.Add(index);
                    kinds.Add(command.Kind);
                }
            }

            if (letter is "C" or "D")
            {
                // Only the two resolved WSB1 roles of this run are homed; C and D are not dispatched.
                Assert.Empty(kinds);
                continue;
            }

            Assert.Equal(
                new[] { ManualMotionPortCommandKind.DefinePositionZero, ManualMotionPortCommandKind.DefineDualZero },
                kinds);
            Assert.Equal(indices[0] + 1, indices[1]);
        }
    }

    [Fact]
    public void Dual_Zero_Refusal_Fails_The_Home_Closed()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisA = new ManualMotionAxisSimulationOptions { RefuseCommand = ManualMotionPortCommandKind.DefineDualZero },
            });
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedHome, result.Code);
        Assert.Equal(ManualMotionCodes.HomeDualZeroRefused, result.PrimaryFailureCode);
        Assert.False(result.HorizontalCoordinate.HomeVerified);
        Assert.False(result.HorizontalCoordinate.CoordinateZeroVerified);
        Assert.Equal(H1DryCommissioningCodes.DualZeroRefusedInHomeSequence, result.HorizontalCoordinate.DualZeroDispositionCode);

        // The refusal is terminal for this axis: no Move, no path point and no second Home sequence.
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
        Assert.Null(result.PathResult);
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
    }

    [Fact]
    public void Position_Zero_Refusal_Prevents_The_Dual_Zero_Definition()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisA = new ManualMotionAxisSimulationOptions { RefuseCommand = ManualMotionPortCommandKind.DefinePositionZero },
            });
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(ManualMotionCodes.PositionZeroRefused, result.PrimaryFailureCode);
        Assert.False(result.HorizontalCoordinate.CoordinateZeroVerified);

        // A refused position definition never reaches the dual-zero definition on that axis: the
        // only dual-zero definition dispatched is the one carried by the other (healthy) axis.
        var horizontalDualZero = 0;
        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero))
        {
            if (string.Equals(CleaningPathExecutionHarness.LetterOf(command.Axis), "A", StringComparison.Ordinal))
            {
                horizontalDualZero++;
            }
        }

        Assert.Equal(0, horizontalDualZero);
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
    }

    [Fact]
    public void Position_Zero_Definition_Precedes_Every_Move()
    {
        var harness = RunFullSuccess();

        var firstMove = -1;
        var lastZero = -1;
        for (var index = 0; index < harness.Port.ExecutedCommands.Count; index++)
        {
            var kind = harness.Port.ExecutedCommands[index].Kind;
            if (kind == ManualMotionPortCommandKind.PositionAbsolute && firstMove < 0)
            {
                firstMove = index;
            }

            if (kind == ManualMotionPortCommandKind.DefinePositionZero)
            {
                lastZero = index;
            }
        }

        Assert.True(firstMove >= 0);
        Assert.True(lastZero >= 0);
        Assert.True(lastZero < firstMove, "the typed position-zero definition must precede every Move");
    }

    [Fact]
    public void One_Axis_Home_Failure_Blocks_Continuation()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions { HomeReachable = false });
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedHome, result.Code);
        Assert.NotNull(result.PrimaryFailureCode);
        Assert.False(result.HorizontalCoordinate.HomeVerified);
        Assert.False(result.VerticalCoordinate.HomeVerified);
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
    }

    [Fact]
    public void Home_Failure_Requests_Nothing_Beyond_The_Stop_Cleanup()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions { HomeReachable = false });
        harness.OpenAndObserve();
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        // No Move, no zero redefinition and no second Home cycle ever runs after the failed Home.
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero));
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.Home).Count);
        Assert.Equal(H1DrySafeReturnState.NotPublished, harness.Coordinator.Result!.DrySafeReturn);
    }

    // ------------------------------------------------------- separated actions and readiness

    [Fact]
    public void Run_Path_After_Commission_Dispatches_No_Home()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        harness.CommissionOnly();
        var commandsAtPathStart = harness.Port.ExecutedCommands.Count;
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.Home).Count);

        var outcome = harness.StartPath();
        Assert.True(outcome.Started);
        Assert.Equal(H1DryCommissioningCodes.KindPathRunStarted, outcome.Code);
        harness.DriveUntilTerminal();
        Assert.Equal(H1DryCommissioningPhase.Completed, harness.Coordinator.Result!.TerminalPhase);

        // Run Path never dispatches Home and never dispatches a zero-establishing command: no Home,
        // position-zero or dual-zero command exists after the path start, and the path itself moved
        // through P1 to P6 and the Centre return.
        Assert.True(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute).Count > 0);
        for (var index = commandsAtPathStart; index < harness.Port.ExecutedCommands.Count; index++)
        {
            var kind = harness.Port.ExecutedCommands[index].Kind;
            Assert.NotEqual(ManualMotionPortCommandKind.Home, kind);
            Assert.NotEqual(ManualMotionPortCommandKind.DefinePositionZero, kind);
            Assert.NotEqual(ManualMotionPortCommandKind.DefineDualZero, kind);
        }

        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.Home).Count);
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefinePositionZero).Count);
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero).Count);
    }

    [Fact]
    public void Run_Path_Refuses_Without_Coordinate_Readiness()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        var outcome = harness.StartPath();
        Assert.False(outcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedCoordinateReadinessAbsent, outcome.Code);
        Assert.Null(harness.Coordinator.Result);
        Assert.False(harness.Coordinator.Active);

        // Neither Home nor P1 nor any other motion request is made: refusal happens first.
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void Run_Path_Refuses_When_Only_One_Axis_Is_Commissioned()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        // One session-scoped Home on the horizontal axis only: the vertical axis stays NotHomed.
        // The wait includes the full end of the Home operation (brake applied after inactive), so
        // the refusal reason proven here is the coordinate readiness, not an active operation.
        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.DriveUntil(
            () => harness.Session.Snapshot.Axes[0].HomeState == ManualMotionHomeState.Homed
                && harness.Session.Snapshot.Axes[0].CoordinateState == ManualMotionCoordinateState.ZeroVerified
                && harness.Session.Snapshot.ActiveOperations.Count == 0);
        var commandsAtRefusal = harness.Port.ExecutedCommands.Count;

        var outcome = harness.StartPath();
        Assert.False(outcome.Started);
        Assert.Equal(H1DryCommissioningCodes.StartRefusedCoordinateReadinessAbsent, outcome.Code);
        Assert.Null(harness.Coordinator.Result);
        Assert.Equal(commandsAtRefusal, harness.Port.ExecutedCommands.Count);
    }

    [Fact]
    public void Home_Input_Already_Active_Reaches_Terminal_Failure()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions { StartPositionA = 0m });
        harness.OpenAndObserve();
        Assert.Equal(ManualMotionInterpretation.Active, harness.Session.Snapshot.Axes[0].HomeInput);

        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedHome, result.Code);
        Assert.Equal(ManualMotionCodes.HomeInputAlreadyActive, result.PrimaryFailureCode);
        Assert.False(result.HorizontalCoordinate.HomeVerified);

        // No native Home command is dispatched to the axis whose Home input is already active, and
        // therefore no zero-establishing command of that axis either: the composed reset-encoder
        // pair is reachable only through a Home that actually ran.
        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.Home))
        {
            Assert.NotEqual("A", CleaningPathExecutionHarness.LetterOf(command.Axis));
        }

        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.DefinePositionZero))
        {
            Assert.NotEqual("A", CleaningPathExecutionHarness.LetterOf(command.Axis));
        }

        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.DefineDualZero))
        {
            Assert.NotEqual("A", CleaningPathExecutionHarness.LetterOf(command.Axis));
        }

        Assert.Empty(harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute));
    }

    [Fact]
    public void Home_Input_Already_Active_At_Baseline_Reaches_Terminal_Failure()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions { StartPositionA = 0m });
        harness.OpenAndObserve();

        // A prior refused Home attempt leaves the typed refusal code as the axis outcome baseline:
        // the new refusal cannot be distinguished by the outcome code alone.
        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.Drive(2);
        Assert.Equal(ManualMotionCodes.HomeInputAlreadyActive, harness.Session.Snapshot.Axes[0].LastOutcomeCode);

        Assert.True(harness.StartCommission().Started);

        // Bounded liveness: the applied refusal is detected by per-request correlation and the
        // run reaches its typed terminal failure; it never remains in Homing indefinitely.
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedHome, result.Code);
        Assert.Equal(ManualMotionCodes.HomeInputAlreadyActive, result.PrimaryFailureCode);
        foreach (var command in harness.CommandsOf(ManualMotionPortCommandKind.Home))
        {
            Assert.NotEqual("A", CleaningPathExecutionHarness.LetterOf(command.Axis));
        }
    }

    [Fact]
    public void Second_Commission_While_Axes_Rest_On_The_Home_Input_Fails_Closed()
    {
        var harness = CommissionedHarness();
        var homeCount = harness.CommandsOf(ManualMotionPortCommandKind.Home).Count;

        // After a verified Home both axes rest on their Home switches: a second Commission/Home
        // is refused fail-closed and the active input is never treated as a completed Home.
        Assert.True(harness.StartCommission().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedHome, result.Code);
        Assert.Equal(ManualMotionCodes.HomeInputAlreadyActive, result.PrimaryFailureCode);
        Assert.Equal(homeCount, harness.CommandsOf(ManualMotionPortCommandKind.Home).Count);
    }

    [Fact]
    public void Run_B_Canonical_Route_Commissions_And_Runs_The_Path()
    {
        var harness = new H1DryCommissioningHarness(
            sensor: "G+214",
            assignedWaterJetId: "WJ2",
            assignedIsolationValveId: "IV2");
        harness.OpenAndObserve();

        var resolved = harness.Resolution.Resolution!;
        Assert.Equal("WSB1", resolved.CabinetId);
        Assert.Equal("C", resolved.HorizontalAxisId);
        Assert.Equal("D", resolved.VerticalAxisId);
        Assert.Equal("WJ2", resolved.EffectiveWaterJetId);

        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();
        Assert.Equal(H1DryCommissioningPhase.Completed, harness.Coordinator.Result!.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunComplete, harness.Coordinator.Result.Code);
        Assert.Empty(harness.UnrelatedAxisCommands());
    }

    // ------------------------------------------------------------------------------ P1 to P6

    [Fact]
    public void Path_Executes_P1_To_P6_In_Order_Without_Skip_Or_Repeat()
    {
        var harness = RunFullSuccess();

        var ledger = harness.Coordinator.Result!.VerifiedLedger;
        Assert.Equal(6, ledger.Count);
        Assert.Equal(JobPhase.P1, ledger[0]);
        Assert.Equal(JobPhase.P2, ledger[1]);
        Assert.Equal(JobPhase.P3, ledger[2]);
        Assert.Equal(JobPhase.P4, ledger[3]);
        Assert.Equal(JobPhase.P5, ledger[4]);
        Assert.Equal(JobPhase.P6, ledger[5]);
    }

    [Fact]
    public void Both_Axes_Verify_Every_Point_With_Typed_Completion_Evidence()
    {
        var harness = RunFullSuccess();

        var points = harness.Coordinator.Result!.PathResult!.Points;
        Assert.Equal(6, points.Count);
        foreach (var point in points)
        {
            Assert.Equal(CleaningPathPointState.Verified, point.State);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Horizontal.MotionOutcome);
            Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, point.Vertical.MotionOutcome);
        }
    }

    [Fact]
    public void Move_Begins_Are_Never_Synchronized()
    {
        var harness = RunFullSuccess();

        var horizontalBegins = harness.MoveBeginIndicesOf(CleaningPathExecutionRole.Horizontal);
        var verticalBegins = harness.MoveBeginIndicesOf(CleaningPathExecutionRole.Vertical);
        Assert.Equal(7, horizontalBegins.Count); // six points + Centre return
        Assert.Equal(7, verticalBegins.Count);
        for (var index = 0; index < horizontalBegins.Count; index++)
        {
            Assert.NotEqual(horizontalBegins[index], verticalBegins[index]);
        }
    }

    [Fact]
    public void Next_Point_Does_Not_Start_While_One_Axis_Is_Unverified()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntil(() => harness.Coordinator.Current!.CurrentPointId == "P2");
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.B,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal("MOVE_TIMEOUT", result.PrimaryFailureCode);
        Assert.Equal(JobPhase.P1, Assert.Single(result.VerifiedLedger));

        // P1 and P2 requested their Moves; P3 never started.
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute)
            .Count(command => command.Axis == ManualMotionAxis.A));
        Assert.Equal(2, harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute)
            .Count(command => command.Axis == ManualMotionAxis.B));
    }

    [Fact]
    public void Brake_Applies_After_Each_Axis_Operation()
    {
        var harness = RunFullSuccess();

        // Each completed motion operation (2 Home + 12 point Moves + 2 Centre Moves) releases the
        // brake before its motion and applies it exactly once after the axis is inactive.
        Assert.Equal(16, harness.CommandsOf(ManualMotionPortCommandKind.BrakeOutputClear).Count);
        Assert.Equal(16, harness.CommandsOf(ManualMotionPortCommandKind.BrakeOutputSet).Count);
    }

    // ------------------------------------------------------------------------ Centre return

    [Fact]
    public void Centre_Return_Runs_Only_After_P6_Verifies()
    {
        var harness = RunFullSuccess();

        var commands = harness.Port.ExecutedCommands;
        var firstCentreTarget = 0;
        var lastPointTarget = 0;
        for (var index = 0; index < commands.Count; index++)
        {
            if (commands[index].Kind != ManualMotionPortCommandKind.PositionAbsolute)
            {
                continue;
            }

            if (commands[index].Operand == CleaningPathUnits.SafeReturnCentre.HorizontalNative
                || commands[index].Operand == CleaningPathUnits.SafeReturnCentre.VerticalNative)
            {
                if (firstCentreTarget == 0)
                {
                    firstCentreTarget = index;
                }
            }
            else
            {
                lastPointTarget = index;
            }
        }

        Assert.True(firstCentreTarget > 0);
        Assert.True(lastPointTarget > 0);
        Assert.True(lastPointTarget < firstCentreTarget, "the Centre Move must follow every P1–P6 Move");
    }

    [Fact]
    public void Centre_Targets_Exactly_The_Fixed_Centre_Contract()
    {
        var harness = RunFullSuccess();

        var centre = harness.Coordinator.Result!.CentreReturn!;
        Assert.Equal(1250000, centre.HorizontalTargetNative);
        Assert.Equal(1250000, centre.VerticalTargetNative);
        Assert.Equal(CleaningPathUnits.SafeReturnCentre.HorizontalNative, centre.HorizontalTargetNative);
        Assert.Equal(CleaningPathUnits.SafeReturnCentre.VerticalNative, centre.VerticalTargetNative);

        var centreCommands = harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute)
            .Where(command => command.Operand == 1250000)
            .ToList();
        Assert.Equal(2, centreCommands.Count);
    }

    [Fact]
    public void Both_Axes_Verify_Centre_Before_Dry_Completion()
    {
        var harness = RunFullSuccess();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Completed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunComplete, result.Code);
        Assert.Equal(H1DrySafeReturnState.Completed, result.DrySafeReturn);
        Assert.True(result.CentreReturn!.Horizontal.Verified);
        Assert.True(result.CentreReturn.Vertical.Verified);
        Assert.True(result.CentreReturn.Horizontal.BrakeApplyCommanded);
        Assert.True(result.CentreReturn.Vertical.BrakeApplyCommanded);
    }

    [Fact]
    public void Centre_Failure_Does_Not_Publish_Dry_Safe_Return()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntil(() => harness.Coordinator.Current!.Phase == H1DryCommissioningPhase.CentreReturn);
        harness.Port.ConfigureAxisSimulationOptions(
            ManualMotionAxis.B,
            new ManualMotionAxisSimulationOptions { MotionStalled = true });
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunFailedCentreReturn, result.Code);
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
        Assert.Equal("MOVE_TIMEOUT", result.PrimaryFailureCode);
    }

    [Fact]
    public void Failure_Before_P6_Never_Starts_The_Centre_Return()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisB = new ManualMotionAxisSimulationOptions { MotionStalled = true },
            });
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();

        Assert.Equal(H1DryCommissioningPhase.Failed, harness.Coordinator.Result!.TerminalPhase);
        Assert.Equal(H1DrySafeReturnState.NotPublished, harness.Coordinator.Result!.DrySafeReturn);
        Assert.DoesNotContain(
            harness.Port.ExecutedCommands,
            command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute && command.Operand == 1250000);
    }

    // --------------------------------------------------------------------- failure and cleanup

    [Fact]
    public void Primary_Failure_Retained_While_Remaining_Axis_Is_Stopped_Once()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisA = new ManualMotionAxisSimulationOptions { MotionStalled = true },
            },
            verticalSpeed: 4000);
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Failed, result.TerminalPhase);
        Assert.Equal("MOVE_TIMEOUT", result.PrimaryFailureCode);

        // The stalled Horizontal axis takes the first MOVE_TIMEOUT; the still-active Vertical axis
        // is stopped exactly once, supervised to inactive with brake applied, and its cleanup is
        // recorded separately from the primary failure.
        var pathResult = result.PathResult!;
        Assert.Equal(CleaningPathCleanupState.Succeeded, pathResult.CleanupState);
        var failedPoint = Assert.Single(pathResult.Points);
        Assert.True(failedPoint.Vertical.StopRequestedOnce);
        Assert.Equal(CleaningPathMotionOutcome.StopIssued, failedPoint.Vertical.CleanupOutcome);
        Assert.Equal("MOVE_TIMEOUT", failedPoint.FailureCode);
    }

    [Fact]
    public void Failed_Run_Publishes_No_Dry_Safe_Return_And_No_Fabricated_Success()
    {
        var harness = new H1DryCommissioningHarness(
            options: new ManualMotionSimulationOptions
            {
                AxisA = new ManualMotionAxisSimulationOptions { MotionStalled = true },
            },
            verticalSpeed: 4000);
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
        Assert.NotEqual(H1DryCommissioningPhase.Completed, result.TerminalPhase);
        Assert.NotEqual(H1DryCommissioningCodes.RunComplete, result.Code);
        Assert.Empty(result.VerifiedLedger);
    }

    // ------------------------------------------------------------------------ Abort and Close

    [Fact]
    public void Abort_Preserves_Owner_Abort_Cause_And_Blocks_Later_Points()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntil(() => harness.Coordinator.Current!.CurrentPointId == "P2");
        harness.Coordinator.Abort();
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Aborted, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunAborted, result.Code);
        Assert.Null(result.PrimaryFailureCode);
        Assert.Equal(JobPhase.P1, Assert.Single(result.VerifiedLedger));
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
    }

    [Fact]
    public void Close_Preserves_Close_Cause_And_Blocks_Later_Points()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntil(() => harness.Coordinator.Current!.CurrentPointId == "P3");
        harness.Coordinator.RequestClose();
        harness.DriveUntilTerminal();

        var result = harness.Coordinator.Result!;
        Assert.Equal(H1DryCommissioningPhase.Closed, result.TerminalPhase);
        Assert.Equal(H1DryCommissioningCodes.RunClosed, result.Code);
        Assert.Null(result.PrimaryFailureCode);
        Assert.Equal(H1DrySafeReturnState.NotPublished, result.DrySafeReturn);
    }

    [Fact]
    public void Abort_Before_Any_Start_Is_A_No_Op()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        harness.Coordinator.Abort();
        harness.Coordinator.RequestClose();
        Assert.Null(harness.Coordinator.Result);
        Assert.False(harness.Coordinator.Active);
    }

    [Fact]
    public void No_Automatic_Restart_After_Terminal()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntil(() => harness.Coordinator.Current!.CurrentPointId == "P2");
        harness.Coordinator.Abort();
        harness.DriveUntilTerminal();

        var terminal = harness.Coordinator.Result!;
        var homeAndMoveCount = harness.CommandsOf(ManualMotionPortCommandKind.Home).Count
            + harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute).Count;
        harness.Drive(50);

        Assert.False(harness.Coordinator.Active);
        Assert.Same(terminal, harness.Coordinator.Result);
        Assert.Equal(
            homeAndMoveCount,
            harness.CommandsOf(ManualMotionPortCommandKind.Home).Count
                + harness.CommandsOf(ManualMotionPortCommandKind.PositionAbsolute).Count);
    }

    // ------------------------------------------------------------------------------ exclusions

    [Fact]
    public void Run_Surface_Carries_Dry_Motion_Commands_Only()
    {
        var harness = RunFullSuccess();

        // Every dispatched command is one member of the closed typed manual motion plan: brake
        // output set/clear, home speed, Home, Begin, position-zero definition and Move setup.
        foreach (var command in harness.Port.ExecutedCommands)
        {
            Assert.True(
                command.Kind is ManualMotionPortCommandKind.BrakeOutputSet
                    or ManualMotionPortCommandKind.BrakeOutputClear
                    or ManualMotionPortCommandKind.HomeSpeed
                    or ManualMotionPortCommandKind.Home
                    or ManualMotionPortCommandKind.Begin
                    or ManualMotionPortCommandKind.DefinePositionZero
                    or ManualMotionPortCommandKind.Acceleration
                    or ManualMotionPortCommandKind.Speed
                    or ManualMotionPortCommandKind.Deceleration
                    or ManualMotionPortCommandKind.PositionAbsolute
                    or ManualMotionPortCommandKind.Stop,
                "an unexpected command kind reached the port");
        }
    }

    [Fact]
    public void Tests_Run_Only_On_The_Simulated_Port()
    {
        var harness = new H1DryCommissioningHarness();
        Assert.Equal(ManualMotionPortMode.Simulated, harness.Session.Mode);
    }

    // ------------------------------------------------------------------------------- helpers

    private static void AssertStartRefusedWith(
        Func<H1DryCommissioningGuards, H1DryCommissioningGuards> mutate,
        string expectedCode)
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();

        // Both explicitly separated actions prove the same common fail-closed preconditions.
        var pathOutcome = harness.StartPath(mutate(H1DryCommissioningHarness.AllGuardsTrue));
        Assert.False(pathOutcome.Started);
        Assert.Equal(expectedCode, pathOutcome.Code);
        var commissionOutcome = harness.StartCommission(mutate(H1DryCommissioningHarness.AllGuardsTrue));
        Assert.False(commissionOutcome.Started);
        Assert.Equal(expectedCode, commissionOutcome.Code);
        Assert.Null(harness.Coordinator.Result);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    /// <summary>One harness whose Commission/Home action completed with verified coordinates.</summary>
    private static H1DryCommissioningHarness CommissionedHarness()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        harness.CommissionOnly();
        return harness;
    }

    private static H1DryCommissioningHarness RunFullSuccess()
    {
        var harness = new H1DryCommissioningHarness();
        harness.OpenAndObserve();
        Assert.True(harness.CommissionThenStartPath().Started);
        harness.DriveUntilTerminal();
        Assert.Equal(H1DryCommissioningPhase.Completed, harness.Coordinator.Result!.TerminalPhase);
        return harness;
    }
}
