using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Focused Phase 3B coordinator tests over a recording fake boundary: the complete start-guard
/// matrix, fail-closed verification (a typed success without the final inactive/current/fresh
/// observations never verifies), the verified-ledger invariants and the typed Safe Return handoff.
/// The fake proves the coordinator is pure with respect to any adapter; the simulation scenarios
/// prove the same states against the real session and simulated port.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathExecutionCoordinatorTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 23, 7, 0, 0, TimeSpan.Zero);
    private static readonly JobPhase[] AllPoints = { JobPhase.P1, JobPhase.P2, JobPhase.P3, JobPhase.P4, JobPhase.P5, JobPhase.P6 };
    private static readonly CleaningPathExecutionRole[] BothRoles = { CleaningPathExecutionRole.Horizontal, CleaningPathExecutionRole.Vertical };

    private sealed class FakeAxisFrameState
    {
        internal bool MoveActive;
        internal bool StopRequested = false;
        internal CleaningPathMotionOutcome LastOutcome = CleaningPathMotionOutcome.None;
        internal string? LastOutcomeSourceCode;
        internal CleaningPathMotionBitState Motion = CleaningPathMotionBitState.Inactive;
        internal decimal? PositionTp;
        internal decimal? PositionRp;
        internal bool TpFresh = true;
        internal bool RpFresh = true;
        internal bool IdleObserved = true;
        internal CleaningPathStopState StopState = CleaningPathStopState.None;

        internal CleaningPathMotionAxisFrame ToFrame(CleaningPathExecutionRole role) => new(
            role,
            MoveActive,
            StopRequested,
            LastOutcome,
            LastOutcomeSourceCode,
            Motion,
            PositionTp,
            PositionRp,
            TpFresh,
            RpFresh,
            IdleObserved,
            StopState,
            BrakeApplyCommanded: true);
    }

    private sealed class FakeBoundary : ICleaningPathMotionBoundary
    {
        internal readonly FakeAxisFrameState Horizontal = new();
        internal readonly FakeAxisFrameState Vertical = new();
        internal readonly List<(CleaningPathExecutionRole Role, int Target)> MoveRequests = new();
        internal readonly List<CleaningPathExecutionRole> StopRequests = new();
        private readonly Queue<CleaningPathMotionEvent> _pending = new();
        private long _sourceSequence;

        internal bool Allowed = true;
        internal bool MoveReady = true;
        internal bool Ready = true;
        internal bool Faulted = false;

        public bool SessionReady => Ready;
        public bool SessionFaulted => Faulted;
        public bool AxisAllowed(CleaningPathExecutionRole role) => Allowed;
        public bool AxisMoveReady(CleaningPathExecutionRole role) => MoveReady;

        public CleaningPathMotionAxisFrame Frame(CleaningPathExecutionRole role) =>
            (role == CleaningPathExecutionRole.Horizontal ? Horizontal : Vertical).ToFrame(role);

        public IReadOnlyList<CleaningPathMotionEvent> DrainEvents()
        {
            var drained = new List<CleaningPathMotionEvent>();
            while (_pending.Count > 0)
            {
                drained.Add(_pending.Dequeue());
            }

            return drained;
        }

        public void RequestMove(CleaningPathExecutionRole role, int targetNative) => MoveRequests.Add((role, targetNative));

        public void RequestStop(CleaningPathExecutionRole role) => StopRequests.Add(role);

        public void RequestClose() => Ready = false;

        public void Step()
        {
        }

        internal void Push(CleaningPathExecutionRole role, CleaningPathMotionOutcome outcome, string sourceCode)
        {
            _sourceSequence++;
            _pending.Enqueue(new CleaningPathMotionEvent(_sourceSequence, Instant, role, outcome, sourceCode));
        }
    }

    private static (CleaningPathExecutionCoordinator Coordinator, FakeBoundary Boundary, CleaningPathSnapshot Snapshot, CleaningPathResolutionResult Resolution) CreateFake()
    {
        var boundary = new FakeBoundary();
        var clock = new TestClock(Instant);
        var coordinator = new CleaningPathExecutionCoordinator(boundary, clock);
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var resolution = CleaningPathResolver.Resolve(
            snapshot,
            SyntheticCleaningPathStores.SensorConfiguration("G+201", "WJ4", "IV4"),
            new CleaningPathResolutionRequest { SensorId = "G+201" });
        Assert.True(resolution.Accepted);
        return (coordinator, boundary, snapshot, resolution);
    }

    private static void ScriptAxisStart(FakeBoundary boundary, CleaningPathExecutionRole role)
    {
        var frame = role == CleaningPathExecutionRole.Horizontal ? boundary.Horizontal : boundary.Vertical;
        frame.MoveActive = true;
        frame.IdleObserved = false;
        frame.Motion = CleaningPathMotionBitState.Active;
        boundary.Push(role, CleaningPathMotionOutcome.OperationStarted, "OPERATION_STARTED");
    }

    private static void ScriptAxisVerified(FakeBoundary boundary, CleaningPathExecutionRole role, int target)
    {
        var frame = role == CleaningPathExecutionRole.Horizontal ? boundary.Horizontal : boundary.Vertical;
        frame.MoveActive = false;
        frame.IdleObserved = true;
        frame.Motion = CleaningPathMotionBitState.Inactive;
        frame.PositionTp = target;
        frame.PositionRp = target;
        frame.TpFresh = true;
        frame.RpFresh = true;
        frame.LastOutcome = CleaningPathMotionOutcome.MoveCompletedPositionVerified;
        frame.LastOutcomeSourceCode = "MOVE_COMPLETED_POSITION_VERIFIED";
        boundary.Push(role, CleaningPathMotionOutcome.MoveCompletedPositionVerified, "MOVE_COMPLETED_POSITION_VERIFIED");
    }

    [Fact]
    public void Start_Guard_Matrix_Refuses_Every_Unmet_Precondition()
    {
        var (coordinator, boundary, snapshot, resolution) = CreateFake();

        boundary.Allowed = false;
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedAxisNotAllowed, coordinator.Start(snapshot, resolution).Code);

        boundary.Allowed = true;
        boundary.MoveReady = false;
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedAxisNotMoveReady, coordinator.Start(snapshot, resolution).Code);

        boundary.MoveReady = true;
        boundary.Horizontal.MoveActive = true;
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedUnresolvedOperation, coordinator.Start(snapshot, resolution).Code);
        boundary.Horizontal.MoveActive = false;

        Assert.Equal(CleaningPathExecutionCodes.StartRefusedSnapshotNotAccepted, coordinator.Start(null, resolution).Code);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedResolutionNotAccepted, coordinator.Start(snapshot, null).Code);

        var path = snapshot.PathFor("G+201")!;
        var shortPoints = path.Points.Take(5).ToList();
        var shortPath = CleaningPathSnapshot.Create(
            CleaningPathStoreParser.SchemaIdentifier,
            CleaningPathSourceKind.OwnerLocalFile,
            new[] { new SensorCleaningPath { SensorId = path.SensorId, Points = shortPoints } },
            null);
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedPointSetInvalid, coordinator.Start(shortPath, resolution).Code);

        var sameAxis = CleaningPathResolutionResult.FromResolution(new ResolvedCleaningPath
        {
            SensorId = "G+201",
            Path = path,
            Centre = snapshot.Centre,
            CanonicalAssignedWaterJetId = "WJ4",
            RequestedOverrideWaterJetId = null,
            EffectiveWaterJetId = "WJ4",
            EffectiveWjProvenance = EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT,
            DerivedIsolationValveId = "IV4",
            CabinetId = "WSB2",
            ControllerId = "MC-WSB2",
            HorizontalAxisId = "C",
            VerticalAxisId = "C",
            HorizontalBrakeOutputId = "OUTPUT_3",
            VerticalBrakeOutputId = "OUTPUT_3",
        });
        Assert.Equal(CleaningPathExecutionCodes.StartRefusedAxesNotDistinct, coordinator.Start(snapshot, sameAxis).Code);
    }

    [Fact]
    public void Typed_Success_Without_Final_Observations_Never_Verifies()
    {
        var (coordinator, boundary, snapshot, resolution) = CreateFake();
        var started = coordinator.Start(snapshot, resolution);
        Assert.True(started.Started);

        ScriptAxisStart(boundary, CleaningPathExecutionRole.Horizontal);
        ScriptAxisStart(boundary, CleaningPathExecutionRole.Vertical);
        coordinator.Step();

        // Typed success events arrive, but the final observations still show motion active: the
        // coordinator refuses the verification and fails closed with the typed mismatch code.
        boundary.Push(CleaningPathExecutionRole.Horizontal, CleaningPathMotionOutcome.MoveCompletedPositionVerified, "MOVE_COMPLETED_POSITION_VERIFIED");
        boundary.Push(CleaningPathExecutionRole.Vertical, CleaningPathMotionOutcome.MoveCompletedPositionVerified, "MOVE_COMPLETED_POSITION_VERIFIED");
        coordinator.Step();

        Assert.Null(coordinator.Result);
        Assert.Equal(CleaningPathPointState.CleanupPending, coordinator.Current!.CurrentPoint.State);

        boundary.Horizontal.MoveActive = false;
        boundary.Vertical.MoveActive = false;
        boundary.Horizontal.IdleObserved = true;
        boundary.Vertical.IdleObserved = true;
        coordinator.Step();

        Assert.NotNull(coordinator.Result);
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, coordinator.Result!.Status);
        Assert.Equal(CleaningPathExecutionCodes.VerificationRefusedObservationMismatch, coordinator.Result!.Code);
        Assert.DoesNotContain(
            coordinator.Result!.Points,
            point => point.State == CleaningPathPointState.Verified);
    }

    [Fact]
    public void Verified_Ledger_Appends_Each_Point_Exactly_Once_And_Hands_Off()
    {
        var (coordinator, boundary, snapshot, resolution) = CreateFake();
        Assert.True(coordinator.Start(snapshot, resolution).Started);

        for (var index = 0; index < 6; index++)
        {
            ScriptAxisStart(boundary, CleaningPathExecutionRole.Horizontal);
            ScriptAxisStart(boundary, CleaningPathExecutionRole.Vertical);
            coordinator.Step();

            ScriptAxisVerified(boundary, CleaningPathExecutionRole.Horizontal, 10000 * (index + 1));
            ScriptAxisVerified(boundary, CleaningPathExecutionRole.Vertical, 10000 * (11 + index));
            coordinator.Step();
        }

        var result = coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn, result.Status);
        Assert.Equal(6, result.Points.Count);
        Assert.Equal(AllPoints, result.Points.Select(point => point.PointId));
        Assert.Equal(6, coordinator.Current!.VerifiedLedger.Count);
        Assert.Equal(coordinator.Current!.VerifiedLedger.Distinct().Count(), coordinator.Current!.VerifiedLedger.Count);

        // Twelve Move requests (Horizontal before Vertical for every point), zero Stops, and one
        // combined point-verification event per point.
        Assert.Equal(12, boundary.MoveRequests.Count);
        Assert.Empty(boundary.StopRequests);
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal(CleaningPathExecutionRole.Horizontal, boundary.MoveRequests[index * 2].Role);
            Assert.Equal(CleaningPathExecutionRole.Vertical, boundary.MoveRequests[index * 2 + 1].Role);
        }

        Assert.Equal(6, result.Events.Count(e => e.Kind == CleaningPathExecutionCodes.KindExecutionPhaseVerified));

        // The handoff names the fixed Centre contract only and never a dispatched return.
        Assert.NotNull(result.Handoff);
        Assert.Equal(1250000, result.Handoff!.HorizontalCentreNative);
        Assert.Equal(1250000, result.Handoff!.VerticalCentreNative);
        Assert.Equal(CleaningPathCoordinateTruth.Verified, result.Handoff!.CoordinateTruth);
        Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, result.Handoff!.HorizontalFinalOutcome);
        Assert.Equal(CleaningPathMotionOutcome.MoveCompletedPositionVerified, result.Handoff!.VerticalFinalOutcome);
    }

    [Fact]
    public void Abort_Stops_Each_Active_Axis_Once_And_Keeps_The_Original_Result()
    {
        var (coordinator, boundary, snapshot, resolution) = CreateFake();
        Assert.True(coordinator.Start(snapshot, resolution).Started);

        ScriptAxisStart(boundary, CleaningPathExecutionRole.Horizontal);
        ScriptAxisStart(boundary, CleaningPathExecutionRole.Vertical);
        coordinator.Step();

        coordinator.Abort();
        coordinator.Step();
        Assert.Equal(BothRoles, boundary.StopRequests);

        // Both axes complete through their own supervised lifecycles; the abort is preserved as the
        // typed cause and the per-axis results remain visible.
        boundary.Push(CleaningPathExecutionRole.Horizontal, CleaningPathMotionOutcome.MoveAbortedByOperator, "MOVE_ABORTED_BY_OPERATOR");
        boundary.Push(CleaningPathExecutionRole.Vertical, CleaningPathMotionOutcome.MoveAbortedByOperator, "MOVE_ABORTED_BY_OPERATOR");
        boundary.Horizontal.MoveActive = false;
        boundary.Vertical.MoveActive = false;
        boundary.Horizontal.IdleObserved = true;
        boundary.Vertical.IdleObserved = true;
        boundary.Horizontal.StopState = CleaningPathStopState.Issued;
        boundary.Vertical.StopState = CleaningPathStopState.Issued;
        coordinator.Step();

        var result = coordinator.Result!;
        Assert.Equal(CleaningPathExecutionStatus.PathFailed, result.Status);
        Assert.Equal(CleaningPathExecutionCodes.PathAborted, result.Code);
        Assert.Single(result.Points);
        Assert.Equal(CleaningPathMotionOutcome.MoveAbortedByOperator, result.Points[0].Horizontal.MotionOutcome);
        Assert.Equal(CleaningPathMotionOutcome.MoveAbortedByOperator, result.Points[0].Vertical.MotionOutcome);
        Assert.Equal(CleaningPathCleanupState.Succeeded, result.CleanupState);
    }
}
