using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Time;

namespace Wjss.Runtime.Core.CleaningPath.Execution;

/// <summary>
/// The pure, deterministic Phase 3B Cleaning Path execution coordinator (Simulation only). It
/// consumes an accepted immutable Phase 3A snapshot and a successful Phase 3A resolver result and
/// executes the Sensor-owned path as strict ordered points P1…P6 through the adapter-neutral typed
/// Manual Motion boundary: for every point the Horizontal and Vertical Moves are independent
/// per-axis operations whose lifecycles may overlap in time (no synchronized Begin, no
/// interpolation, no coordinated trajectory), and the point is verified only when BOTH axes report
/// the typed <c>MOVE_COMPLETED_POSITION_VERIFIED</c> outcome with final inactive, current and fresh
/// observations. Pn+1 never starts before both axes verify Pn; no point is skipped or repeated.
///
/// Failure model: the original failure is captured unchanged; every later point is blocked; the
/// remaining active axis receives exactly one typed Stop request and is supervised through the
/// existing lifecycle; the cleanup outcome is preserved separately from the original cause. After
/// verified P6 the coordinator sets <see cref="CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn"/>
/// and exposes a typed handoff only — it never dispatches the Centre return, never touches a Pump
/// or Isolation Valve and never releases a Job.
///
/// The coordinator is pure with respect to Hardware: it writes no port, no file and no topology,
/// mutates neither the snapshot nor the resolver result, and reads deterministic observations only.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathExecutionCoordinator
{
    private static long _executionCounter;

    private readonly ICleaningPathMotionBoundary _boundary;
    private readonly IClock _clock;

    private readonly List<CleaningPathExecutionEvent> _events = new();
    private readonly List<JobPhase> _ledger = new();
    private readonly List<CleaningPathPointRecord> _finishedPoints = new();

    private AxisState _horizontal = new(CleaningPathExecutionRole.Horizontal);
    private AxisState _vertical = new(CleaningPathExecutionRole.Vertical);

    private CleaningPathSnapshot? _snapshot;
    private CleaningPathResolutionResult? _resolution;
    private IReadOnlyList<CleaningPathPointTarget> _targets = Array.Empty<CleaningPathPointTarget>();
    private CleaningPathExecutionStatus _status = CleaningPathExecutionStatus.NotStarted;
    private CleaningPathCleanupState _cleanupState = CleaningPathCleanupState.NotNeeded;
    private CleaningPathPointState _pointState = CleaningPathPointState.Pending;
    private string? _failureCode;
    private string? _failureSourceCode;
    private int _currentPointIndex = -1;
    private long _evidenceSequence;
    private DateTimeOffset _startedAt;
    private string _executionId = string.Empty;
    private bool _started;
    private bool _failureHandled;
    private bool _closeRequested;

    public CleaningPathExecutionCoordinator(ICleaningPathMotionBoundary boundary, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(clock);
        _boundary = boundary;
        _clock = clock;
    }

    /// <summary>The immutable execution snapshot republished after every step (null before Start).</summary>
    public CleaningPathExecutionSnapshot? Current { get; private set; }

    /// <summary>The final typed result, or null while the execution is non-terminal.</summary>
    public CleaningPathExecutionResult? Result { get; private set; }

    /// <summary>True while the execution has started and not reached a terminal state.</summary>
    public bool Active => _started && Result is null;

    /// <summary>
    /// Starts one execution: validates every start precondition and issues the P1 Horizontal Move
    /// request before the P1 Vertical Move request through the same serialized boundary. Any
    /// unmet precondition refuses the start with a typed code and leaves the machine untouched.
    /// </summary>
    public CleaningPathExecutionStartOutcome Start(CleaningPathSnapshot? snapshot, CleaningPathResolutionResult? resolution)
    {
        if (_started)
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedExecutionActive);
        }

        if (snapshot is null
            || snapshot.SourceKind != CleaningPathSourceKind.OwnerLocalFile
            || !string.Equals(snapshot.Schema, CleaningPathStoreParser.SchemaIdentifier, StringComparison.Ordinal))
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedSnapshotNotAccepted);
        }

        if (resolution is null || !resolution.Accepted || resolution.Resolution is null)
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedResolutionNotAccepted);
        }

        var resolved = resolution.Resolution;
        if (!string.Equals(resolved.SensorId, resolved.Path.SensorId, StringComparison.Ordinal)
            || !string.Equals(snapshot.PathFor(resolved.SensorId)?.SensorId, resolved.SensorId, StringComparison.Ordinal))
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedSensorMismatch);
        }

        var path = snapshot.PathFor(resolved.SensorId);
        if (path is null)
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedPathMissing);
        }

        if (!TryBuildTargets(path, out var targets, out var targetRefusal))
        {
            return RefuseStart(targetRefusal);
        }

        if (string.Equals(resolved.HorizontalAxisId, resolved.VerticalAxisId, StringComparison.Ordinal))
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedAxesNotDistinct);
        }

        if (!_boundary.AxisAllowed(CleaningPathExecutionRole.Horizontal)
            || !_boundary.AxisAllowed(CleaningPathExecutionRole.Vertical))
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedAxisNotAllowed);
        }

        var horizontalFrame = _boundary.Frame(CleaningPathExecutionRole.Horizontal);
        var verticalFrame = _boundary.Frame(CleaningPathExecutionRole.Vertical);
        if (horizontalFrame.MoveActive || verticalFrame.MoveActive)
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedUnresolvedOperation);
        }

        if (!_boundary.AxisMoveReady(CleaningPathExecutionRole.Horizontal)
            || !_boundary.AxisMoveReady(CleaningPathExecutionRole.Vertical))
        {
            return RefuseStart(CleaningPathExecutionCodes.StartRefusedAxisNotMoveReady);
        }

        _snapshot = snapshot;
        _resolution = resolution;
        _targets = targets;
        _started = true;
        _startedAt = _clock.UtcNow;
        _executionId = "CPX" + Interlocked.Increment(ref _executionCounter).ToString(CultureInfo.InvariantCulture);
        _horizontal = new AxisState(CleaningPathExecutionRole.Horizontal, resolved.HorizontalAxisId, resolved.HorizontalBrakeOutputId);
        _vertical = new AxisState(CleaningPathExecutionRole.Vertical, resolved.VerticalAxisId, resolved.VerticalBrakeOutputId);

        Record(CleaningPathExecutionCodes.KindExecutionStarted, CleaningPathExecutionCodes.KindExecutionStarted, null, null, null);
        StartPoint(0);
        Publish();
        return new CleaningPathExecutionStartOutcome(true, CleaningPathExecutionCodes.KindExecutionStarted, Current);
    }

    /// <summary>
    /// Advances the coordinator by one observation/decision step. The boundary's driver step is
    /// separate: the test or application driver calls <see cref="ICleaningPathMotionBoundary.Step"/>
    /// for the serialized port cadence and this method for execution state.
    /// </summary>
    public void Step()
    {
        if (!_started || Result is not null)
        {
            return;
        }

        foreach (var motionEvent in _boundary.DrainEvents())
        {
            ProcessEvent(motionEvent);
        }

        Decide();
        Publish();
    }

    /// <summary>Operator abort: no new point request is ever issued; every active axis is stopped exactly once.</summary>
    public void Abort()
    {
        if (!_started || Result is not null || _status is CleaningPathExecutionStatus.Aborting or CleaningPathExecutionStatus.Closing)
        {
            return;
        }

        _status = CleaningPathExecutionStatus.Aborting;
        Record(CleaningPathExecutionCodes.KindAbortRequested, CleaningPathExecutionCodes.PathAborted, null, null, null);
        StopRemainingActiveAxes();
        Publish();
    }

    /// <summary>Application close: every active axis is stopped exactly once, then the session close is requested.</summary>
    public void RequestClose()
    {
        if (!_started || Result is not null || _closeRequested)
        {
            return;
        }

        _closeRequested = true;
        _status = CleaningPathExecutionStatus.Closing;
        Record(CleaningPathExecutionCodes.KindCloseRequested, CleaningPathExecutionCodes.PathClosed, null, null, null);
        StopRemainingActiveAxes();
        _boundary.RequestClose();
        Publish();
    }

    // ------------------------------------------------------------------------------ start

    private static bool TryBuildTargets(
        SensorCleaningPath path,
        out IReadOnlyList<CleaningPathPointTarget> targets,
        out string refusalCode)
    {
        var built = new List<CleaningPathPointTarget>(6);
        if (path.Points.Count != CleaningPathStoreParser.ExpectedPointIds.Count)
        {
            targets = Array.Empty<CleaningPathPointTarget>();
            refusalCode = CleaningPathExecutionCodes.StartRefusedPointSetInvalid;
            return false;
        }

        for (var index = 0; index < path.Points.Count; index++)
        {
            var point = path.Points[index];
            var expectedId = CleaningPathStoreParser.ExpectedPointIds[index];
            if (!string.Equals(point.PointId, expectedId, StringComparison.Ordinal)
                || !Enum.TryParse<JobPhase>(point.PointId, out var pointId)
                || pointId != (JobPhase)index)
            {
                targets = Array.Empty<CleaningPathPointTarget>();
                refusalCode = CleaningPathExecutionCodes.StartRefusedPointSetInvalid;
                return false;
            }

            if (point.HorizontalNative is < 0 or > CleaningPathUnits.MaximumNative
                || point.VerticalNative is < 0 or > CleaningPathUnits.MaximumNative)
            {
                targets = Array.Empty<CleaningPathPointTarget>();
                refusalCode = CleaningPathExecutionCodes.StartRefusedTargetOutOfRange;
                return false;
            }

            built.Add(new CleaningPathPointTarget(pointId, point.HorizontalNative, point.VerticalNative));
        }

        targets = built;
        refusalCode = string.Empty;
        return true;
    }

    private static CleaningPathExecutionStartOutcome RefuseStart(string code) => new(false, code, null);

    // ------------------------------------------------------------------------------ points

    private void StartPoint(int index)
    {
        _currentPointIndex = index;
        _pointState = CleaningPathPointState.Starting;
        _horizontal.ResetForPoint();
        _vertical.ResetForPoint();

        // Deterministic request order: Horizontal first, then Vertical, through the same serialized
        // boundary. The two Move lifecycles may overlap; there is no synchronized Begin.
        RequestMoveOf(_horizontal);
        RequestMoveOf(_vertical);
        _pointState = CleaningPathPointState.Running;
    }

    private void RequestMoveOf(AxisState axis)
    {
        var target = _targets[_currentPointIndex];
        var targetNative = axis.Role == CleaningPathExecutionRole.Horizontal ? target.HorizontalNative : target.VerticalNative;
        axis.State = CleaningPathAxisExecutionState.Requested;
        axis.RequestedTargetNative = targetNative;
        _boundary.RequestMove(axis.Role, targetNative);
        Record(CleaningPathExecutionCodes.KindMoveRequested, CleaningPathExecutionCodes.KindMoveRequested, target.PointId, axis.Role, null);
    }

    private void ProcessEvent(CleaningPathMotionEvent motionEvent)
    {
        var axis = AxisFor(motionEvent.Role);
        switch (motionEvent.Outcome)
        {
            case CleaningPathMotionOutcome.OperationStarted:
                if (axis.State == CleaningPathAxisExecutionState.Requested)
                {
                    axis.State = CleaningPathAxisExecutionState.Running;
                    axis.MoveAcceptedObserved = true;
                    Record(CleaningPathExecutionCodes.KindMoveAccepted, CleaningPathExecutionCodes.KindMoveAccepted, CurrentPointId, axis.Role, motionEvent.SourceCode);
                }

                return;

            case CleaningPathMotionOutcome.MoveCompletedPositionVerified:
                axis.MoveOutcome = motionEvent.Outcome;
                axis.MotionOutcomeSourceCode = motionEvent.SourceCode;
                return;

            case CleaningPathMotionOutcome.BrakeApplyCommanded:
                return;

            case CleaningPathMotionOutcome.StopIssued:
            case CleaningPathMotionOutcome.StopRefused:
            case CleaningPathMotionOutcome.StopNotObservedWithinTimeout:
                if (axis.StopRequestedOnce)
                {
                    axis.CleanupOutcome = motionEvent.Outcome;
                    axis.CleanupSourceCode = motionEvent.SourceCode;
                    if (motionEvent.Outcome != CleaningPathMotionOutcome.StopIssued)
                    {
                        axis.State = CleaningPathAxisExecutionState.CleanupFailed;
                    }
                }

                return;

            case CleaningPathMotionOutcome.BrakeApplyRefused:
                // A cleanup failure after a verified Move: the motion result is preserved and the
                // point can never become Verified while any cleanup code exists.
                axis.CleanupOutcome = motionEvent.Outcome;
                axis.CleanupSourceCode = motionEvent.SourceCode;
                if (axis.State is CleaningPathAxisExecutionState.Running or CleaningPathAxisExecutionState.Requested)
                {
                    axis.State = CleaningPathAxisExecutionState.CleanupFailed;
                    OnPointFailure(motionEvent.SourceCode ?? CleaningPathExecutionCodes.PointCleanupFailed, motionEvent.SourceCode, axis);
                }

                return;

            case CleaningPathMotionOutcome.MoveAbortedByOperator:
            case CleaningPathMotionOutcome.MoveAbortedSessionClosing:
                axis.MoveOutcome = motionEvent.Outcome;
                axis.MotionOutcomeSourceCode = motionEvent.SourceCode;
                if (axis.StopRequestedOnce)
                {
                    // Expected end of a supervised cleanup Stop: one per-axis result is preserved,
                    // and a cleanup that already failed is never downgraded to a plain failure.
                    if (axis.State != CleaningPathAxisExecutionState.CleanupFailed)
                    {
                        axis.State = CleaningPathAxisExecutionState.Failed;
                    }

                    return;
                }

                if (axis.State is CleaningPathAxisExecutionState.Running or CleaningPathAxisExecutionState.Requested)
                {
                    axis.State = CleaningPathAxisExecutionState.Failed;
                    OnPointFailure(motionEvent.SourceCode ?? CleaningPathExecutionCodes.PathFailedAxisFailure, motionEvent.SourceCode, axis);
                }

                return;

            default:
                // Every remaining typed outcome is a failure of this axis's own operation
                // (refusal, timeout, fault, stale result, or an unmapped code treated fail-closed).
                axis.MoveOutcome = motionEvent.Outcome;
                axis.MotionOutcomeSourceCode = motionEvent.SourceCode;
                if (axis.State is CleaningPathAxisExecutionState.Running
                    or CleaningPathAxisExecutionState.Requested
                    or CleaningPathAxisExecutionState.AwaitingStopped)
                {
                    axis.State = CleaningPathAxisExecutionState.Failed;
                    OnPointFailure(motionEvent.SourceCode ?? CleaningPathExecutionCodes.PathFailedAxisFailure, motionEvent.SourceCode, axis);
                }

                return;
        }
    }

    private void OnPointFailure(string failureCode, string? sourceCode, AxisState cause)
    {
        // The original failure is captured exactly once and is never overwritten by cleanup.
        if (!_failureHandled)
        {
            _failureHandled = true;
            _failureCode = failureCode;
            _failureSourceCode = sourceCode;
            Record(CleaningPathExecutionCodes.KindPointFailed, failureCode, CurrentPointId, cause.Role, sourceCode);
        }

        _pointState = CleaningPathPointState.CleanupPending;
        StopRemainingActiveAxes();
    }

    private void StopRemainingActiveAxes()
    {
        foreach (var axis in new[] { _horizontal, _vertical })
        {
            if (axis.StopRequestedOnce || axis.State is not (CleaningPathAxisExecutionState.Requested or CleaningPathAxisExecutionState.Running))
            {
                continue;
            }

            axis.StopRequestedOnce = true;
            axis.State = CleaningPathAxisExecutionState.AwaitingStopped;
            _boundary.RequestStop(axis.Role);
            Record(CleaningPathExecutionCodes.KindStopRequested, CleaningPathExecutionCodes.KindStopRequested, CurrentPointId, axis.Role, null);
        }
    }

    // ------------------------------------------------------------------------------ decisions

    private void Decide()
    {
        if (_status is CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn or CleaningPathExecutionStatus.PathFailed)
        {
            return;
        }

        AdoptFrameOutcomes();
        FinalizeAxisVerification(_horizontal);
        FinalizeAxisVerification(_vertical);

        if (_status is CleaningPathExecutionStatus.Aborting or CleaningPathExecutionStatus.Closing)
        {
            FinalizeFailure();
            return;
        }

        if (_failureHandled)
        {
            FinalizeFailure();
            return;
        }

        if (_horizontal.State == CleaningPathAxisExecutionState.Verified
            && _vertical.State == CleaningPathAxisExecutionState.Verified)
        {
            CompletePoint();
            return;
        }

        if (_horizontal.State == CleaningPathAxisExecutionState.CleanupFailed
            || _vertical.State == CleaningPathAxisExecutionState.CleanupFailed)
        {
            if (!_failureHandled)
            {
                OnPointFailure(CleaningPathExecutionCodes.PointCleanupFailed, _horizontal.CleanupSourceCode ?? _vertical.CleanupSourceCode, _horizontal);
            }

            FinalizeFailure();
        }
    }

    /// <summary>
    /// Adoption fallback for terminal per-axis outcomes that produced no per-axis event (a session
    /// fault clears every active operation and logs one attributed entry only). A sticky
    /// previous-point code is never adopted: adoption requires an observed operation start or a
    /// session fault, so a freshly requested operation can never inherit an earlier result.
    /// </summary>
    private void AdoptFrameOutcomes()
    {
        AdoptFrameOutcome(_horizontal);
        AdoptFrameOutcome(_vertical);
    }

    private void AdoptFrameOutcome(AxisState axis)
    {
        if (axis.State is not (CleaningPathAxisExecutionState.Requested
                or CleaningPathAxisExecutionState.Running
                or CleaningPathAxisExecutionState.AwaitingStopped)
            || axis.MoveOutcome != CleaningPathMotionOutcome.None)
        {
            return;
        }

        var frame = _boundary.Frame(axis.Role);
        if (frame.MoveActive || frame.LastOutcome == CleaningPathMotionOutcome.None)
        {
            return;
        }

        if (!axis.MoveAcceptedObserved && !_boundary.SessionFaulted)
        {
            return;
        }

        ProcessEvent(new CleaningPathMotionEvent(0, _clock.UtcNow, axis.Role, frame.LastOutcome, frame.LastOutcomeSourceCode ?? string.Empty));
    }

    private void FinalizeAxisVerification(AxisState axis)
    {
        if (axis.MoveOutcome != CleaningPathMotionOutcome.MoveCompletedPositionVerified
            || axis.State is CleaningPathAxisExecutionState.Verified
                or CleaningPathAxisExecutionState.Failed
                or CleaningPathAxisExecutionState.CleanupFailed
                or CleaningPathAxisExecutionState.AwaitingStopped)
        {
            return;
        }

        if (axis.CleanupOutcome is CleaningPathMotionOutcome.BrakeApplyRefused
            or CleaningPathMotionOutcome.StopRefused
            or CleaningPathMotionOutcome.StopNotObservedWithinTimeout)
        {
            axis.State = CleaningPathAxisExecutionState.CleanupFailed;
            return;
        }

        var frame = _boundary.Frame(axis.Role);
        if (frame.MoveActive || !frame.IdleObserved || !frame.TpFresh || !frame.RpFresh
            || frame.PositionTp is null || frame.PositionRp is null)
        {
            // Fail closed: a typed success without the final inactive, current and fresh
            // observations never verifies a point.
            axis.MoveOutcome = CleaningPathMotionOutcome.None;
            axis.MotionOutcomeSourceCode = CleaningPathExecutionCodes.VerificationRefusedObservationMismatch;
            axis.State = CleaningPathAxisExecutionState.Failed;
            OnPointFailure(CleaningPathExecutionCodes.VerificationRefusedObservationMismatch, axis.MotionOutcomeSourceCode, axis);
            return;
        }

        axis.State = CleaningPathAxisExecutionState.Verified;
    }

    private void CompletePoint()
    {
        var pointId = CurrentPointId!.Value;
        _pointState = CleaningPathPointState.Verified;
        if (!_ledger.Contains(pointId))
        {
            _ledger.Add(pointId);
        }

        _finishedPoints.Add(BuildPointRecord(CleaningPathPointState.Verified));
        Record(CleaningPathExecutionCodes.KindExecutionPhaseVerified, CleaningPathExecutionCodes.KindExecutionPhaseVerified, pointId, null, null);

        if (_currentPointIndex + 1 >= _targets.Count)
        {
            _status = CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn;
            Record(CleaningPathExecutionCodes.KindPathCompleted, CleaningPathExecutionCodes.PathCompleteAwaitingSafeReturn, pointId, null, null);
            Result = BuildResult();
            Publish();
            return;
        }

        StartPoint(_currentPointIndex + 1);
    }

    private void FinalizeFailure()
    {
        // The failure terminal is reached only when every axis's own operation lifecycle has ended.
        if (_horizontal.MoveActiveObserved(_boundary) || _vertical.MoveActiveObserved(_boundary))
        {
            if (_status is CleaningPathExecutionStatus.Aborting or CleaningPathExecutionStatus.Closing)
            {
                StopRemainingActiveAxes();
            }

            return;
        }

        if (_status is CleaningPathExecutionStatus.Aborting or CleaningPathExecutionStatus.Closing)
        {
            FinalizeCleanup();
            _pointState = CleaningPathPointState.Failed;
            _finishedPoints.Add(BuildPointRecord(CleaningPathPointState.Failed));
            _status = CleaningPathExecutionStatus.PathFailed;
            if (_failureCode is null)
            {
                _failureCode = _closeRequested
                    ? CleaningPathExecutionCodes.PathClosed
                    : CleaningPathExecutionCodes.PathAborted;
            }

            Record(CleaningPathExecutionCodes.KindCleanupRecorded, CleanupCodeForState(_cleanupState), CurrentPointId, null, null);
            Record(CleaningPathExecutionCodes.KindPathFailed, _failureCode, CurrentPointId, null, _failureSourceCode);
            Result = BuildResult();
            Publish();
            return;
        }

        if (!_failureHandled)
        {
            return;
        }

        FinalizeCleanup();
        _pointState = _horizontal.State == CleaningPathAxisExecutionState.CleanupFailed
                || _vertical.State == CleaningPathAxisExecutionState.CleanupFailed
            ? CleaningPathPointState.CleanupFailed
            : CleaningPathPointState.Failed;
        _finishedPoints.Add(BuildPointRecord(_pointState));
        _status = CleaningPathExecutionStatus.PathFailed;
        Record(CleaningPathExecutionCodes.KindCleanupRecorded, CleanupCodeForState(_cleanupState), CurrentPointId, null, null);
        Record(CleaningPathExecutionCodes.KindPathFailed, _failureCode ?? CleaningPathExecutionCodes.PathFailedAxisFailure, CurrentPointId, null, _failureSourceCode);
        Result = BuildResult();
        Publish();
    }

    private void FinalizeCleanup()
    {
        // Cleanup outcome is preserved separately from the original cause and is derived only from
        // typed per-axis observations; a successful cleanup never converts a failure to success and
        // a failed cleanup never discards the original cause.
        var successes = 0;
        var failures = 0;
        foreach (var axis in new[] { _horizontal, _vertical })
        {
            var frame = _boundary.Frame(axis.Role);
            var cleanupFailed = axis.CleanupOutcome is CleaningPathMotionOutcome.BrakeApplyRefused
                or CleaningPathMotionOutcome.StopRefused
                or CleaningPathMotionOutcome.StopNotObservedWithinTimeout;

            if (axis.StopRequestedOnce)
            {
                if (!cleanupFailed && frame.StopState == CleaningPathStopState.Issued && frame.IdleObserved)
                {
                    if (axis.CleanupOutcome == CleaningPathMotionOutcome.None)
                    {
                        axis.CleanupOutcome = CleaningPathMotionOutcome.StopIssued;
                        axis.CleanupSourceCode = frame.LastOutcomeSourceCode;
                    }

                    successes++;
                }
                else
                {
                    if (frame.StopState == CleaningPathStopState.Refused)
                    {
                        axis.CleanupOutcome = CleaningPathMotionOutcome.StopRefused;
                        axis.CleanupSourceCode = frame.LastOutcomeSourceCode;
                    }
                    else if (frame.StopState == CleaningPathStopState.NotObservedWithinTimeout)
                    {
                        axis.CleanupOutcome = CleaningPathMotionOutcome.StopNotObservedWithinTimeout;
                        axis.CleanupSourceCode = frame.LastOutcomeSourceCode;
                    }

                    failures++;
                }
            }
            else if (cleanupFailed)
            {
                failures++;
            }
        }

        _cleanupState = successes == 0 && failures == 0
            ? CleaningPathCleanupState.NotNeeded
            : failures == 0
                ? CleaningPathCleanupState.Succeeded
                : successes == 0
                    ? CleaningPathCleanupState.Failed
                    : CleaningPathCleanupState.Partial;
    }

    // ------------------------------------------------------------------------------ publication

    private JobPhase? CurrentPointId =>
        _currentPointIndex >= 0 && _currentPointIndex < _targets.Count ? _targets[_currentPointIndex].PointId : null;

    private AxisState AxisFor(CleaningPathExecutionRole role) =>
        role == CleaningPathExecutionRole.Horizontal ? _horizontal : _vertical;

    private void Record(string kind, string code, JobPhase? pointId, CleaningPathExecutionRole? role, string? sourceCode)
    {
        _evidenceSequence++;
        _events.Add(new CleaningPathExecutionEvent(_evidenceSequence, _clock.UtcNow, kind, code, pointId, role, sourceCode));
    }

    private CleaningPathPointRecord BuildPointRecord(CleaningPathPointState state)
    {
        var target = _targets[_currentPointIndex];
        return new CleaningPathPointRecord(
            target.PointId,
            target.HorizontalNative,
            target.VerticalNative,
            state,
            _horizontal.ToRecord(),
            _vertical.ToRecord(),
            _failureCode,
            _failureSourceCode,
            _startedAt,
            state == CleaningPathPointState.Verified ? _clock.UtcNow : null);
    }

    private void Publish()
    {
        if (!_started)
        {
            Current = null;
            return;
        }

        var resolved = _resolution!.Resolution!;
        Current = new CleaningPathExecutionSnapshot
        {
            ExecutionId = _executionId,
            SensorId = resolved.SensorId,
            CanonicalWaterJetId = resolved.CanonicalAssignedWaterJetId,
            RequestedOverrideWaterJetId = resolved.RequestedOverrideWaterJetId,
            EffectiveWaterJetId = resolved.EffectiveWaterJetId,
            EffectiveWjProvenance = resolved.EffectiveWjProvenance,
            DerivedIsolationValveId = resolved.DerivedIsolationValveId,
            CabinetId = resolved.CabinetId,
            ControllerId = resolved.ControllerId,
            HorizontalAxisId = resolved.HorizontalAxisId,
            VerticalAxisId = resolved.VerticalAxisId,
            HorizontalBrakeOutputId = resolved.HorizontalBrakeOutputId,
            VerticalBrakeOutputId = resolved.VerticalBrakeOutputId,
            Targets = _targets,
            CurrentPointIndex = _currentPointIndex,
            VerifiedLedger = _ledger.ToArray(),
            StartedAt = _startedAt,
            EvidenceSequence = _evidenceSequence,
            Status = _status,
            CurrentPoint = BuildCurrentPointRecord(),
            CleanupState = _cleanupState,
            FailureCode = _failureCode,
        };
    }

    private CleaningPathPointRecord BuildCurrentPointRecord()
    {
        if (_currentPointIndex < 0 || _currentPointIndex >= _targets.Count)
        {
            var last = _targets.Count == 0 ? default(JobPhase) : _targets[_targets.Count - 1].PointId;
            return new CleaningPathPointRecord(
                last,
                0,
                0,
                CleaningPathPointState.Pending,
                _horizontal.ToRecord(),
                _vertical.ToRecord(),
                _failureCode,
                _failureSourceCode,
                null,
                null);
        }

        var target = _targets[_currentPointIndex];
        return new CleaningPathPointRecord(
            target.PointId,
            target.HorizontalNative,
            target.VerticalNative,
            _pointState,
            _horizontal.ToRecord(),
            _vertical.ToRecord(),
            _failureCode,
            _failureSourceCode,
            _startedAt,
            _pointState == CleaningPathPointState.Verified ? _clock.UtcNow : null);
    }

    private CleaningPathExecutionResult BuildResult()
    {
        var handoff = _status == CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn
            ? BuildHandoff()
            : null;
        return new CleaningPathExecutionResult(
            _status,
            handoff is null ? _failureCode ?? CleaningPathExecutionCodes.PathFailedAxisFailure : CleaningPathExecutionCodes.PathCompleteAwaitingSafeReturn,
            handoff,
            _finishedPoints.ToArray(),
            _events.ToArray(),
            _cleanupState);
    }

    private CleaningPathExecutionHandoff BuildHandoff()
    {
        var centre = _snapshot!.Centre;
        var truth = _boundary.Frame(CleaningPathExecutionRole.Horizontal) is { TpFresh: true, RpFresh: true, IdleObserved: true }
                && _boundary.Frame(CleaningPathExecutionRole.Vertical) is { TpFresh: true, RpFresh: true, IdleObserved: true }
            ? CleaningPathCoordinateTruth.Verified
            : CleaningPathCoordinateTruth.Degraded;
        var resolved = _resolution!.Resolution!;
        return new CleaningPathExecutionHandoff(
            _executionId,
            resolved.SensorId,
            resolved.EffectiveWaterJetId,
            resolved.HorizontalAxisId,
            resolved.VerticalAxisId,
            _horizontal.MoveOutcome,
            _vertical.MoveOutcome,
            centre.HorizontalNative,
            centre.VerticalNative,
            truth,
            _cleanupState,
            _clock.UtcNow);
    }

    private static string CleanupCodeForState(CleaningPathCleanupState state) => state switch
    {
        CleaningPathCleanupState.Succeeded => "CLEANUP_SUCCEEDED",
        CleaningPathCleanupState.Partial => "CLEANUP_PARTIAL",
        CleaningPathCleanupState.Failed => "CLEANUP_FAILED",
        _ => "CLEANUP_NOT_NEEDED",
    };

    private sealed class AxisState
    {
        public AxisState(CleaningPathExecutionRole role, string axisId = "", string brakeOutputId = "")
        {
            Role = role;
            AxisId = axisId;
            BrakeOutputId = brakeOutputId;
        }

        public CleaningPathExecutionRole Role { get; }
        public string AxisId { get; }
        public string BrakeOutputId { get; }
        public CleaningPathAxisExecutionState State { get; set; } = CleaningPathAxisExecutionState.Pending;
        public int? RequestedTargetNative { get; set; }
        public CleaningPathMotionOutcome MoveOutcome { get; set; }
        public string? MotionOutcomeSourceCode { get; set; }
        public CleaningPathMotionOutcome CleanupOutcome { get; set; }
        public string? CleanupSourceCode { get; set; }
        public bool StopRequestedOnce { get; set; }
        public bool MoveAcceptedObserved { get; set; }

        public void ResetForPoint()
        {
            State = CleaningPathAxisExecutionState.Pending;
            RequestedTargetNative = null;
            MoveOutcome = CleaningPathMotionOutcome.None;
            MotionOutcomeSourceCode = null;
            CleanupOutcome = CleaningPathMotionOutcome.None;
            CleanupSourceCode = null;
            StopRequestedOnce = false;
            MoveAcceptedObserved = false;
        }

        public bool MoveActiveObserved(ICleaningPathMotionBoundary boundary) => boundary.Frame(Role).MoveActive;

        public CleaningPathAxisExecutionRecord ToRecord() => new(
            Role,
            AxisId,
            BrakeOutputId,
            State,
            RequestedTargetNative,
            MoveOutcome,
            MotionOutcomeSourceCode,
            CleanupOutcome,
            CleanupSourceCode,
            StopRequestedOnce,
            MoveAcceptedObserved);
    }
}
