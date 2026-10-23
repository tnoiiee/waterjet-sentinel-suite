using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.Runtime.Core.CleaningPath.Execution;

/// <summary>
/// The typed Manual Motion adapter behind <see cref="ICleaningPathMotionBoundary"/>. It maps the
/// resolved Horizontal and Vertical axis identities onto exactly two axes of one existing
/// <see cref="ManualMotionSession"/>, forwards typed Move and Stop requests, and translates the
/// session's typed observations and log events into the adapter-neutral execution vocabulary.
/// The adapter never writes raw commands, never touches an axis outside the two resolved roles,
/// and never fabricates an observation.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class ManualMotionCleaningPathBoundary : ICleaningPathMotionBoundary
{
    private readonly ManualMotionSession _session;
    private readonly ManualMotionAxis _horizontal;
    private readonly ManualMotionAxis _vertical;
    private long _lastDrainedLogSequence;

    public ManualMotionCleaningPathBoundary(ManualMotionSession session, string horizontalAxisId, string verticalAxisId)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _horizontal = AxisFor(horizontalAxisId);
        _vertical = AxisFor(verticalAxisId);
    }

    /// <inheritdoc/>
    public bool SessionReady => _session.Snapshot.State == ManualMotionSessionState.Ready;

    /// <inheritdoc/>
    public bool SessionFaulted => _session.Snapshot.State == ManualMotionSessionState.Faulted;

    /// <inheritdoc/>
    public bool AxisAllowed(CleaningPathExecutionRole role) => AxisSnapshot(role) is { AllowedByProfile: true };

    /// <inheritdoc/>
    public bool AxisMoveReady(CleaningPathExecutionRole role)
    {
        var snapshot = _session.Snapshot;
        var axis = AxisSnapshot(role);
        if (axis is null)
        {
            return false;
        }

        foreach (var operation in snapshot.ActiveOperations)
        {
            if (operation.Axis == axis.Axis)
            {
                return false;
            }
        }

        return axis.HomeState == ManualMotionHomeState.Homed
            && axis.Motion == ManualMotionInterpretation.Inactive
            && axis.PositionTp.Fresh
            && axis.PositionRp.Fresh;
    }

    /// <inheritdoc/>
    public CleaningPathMotionAxisFrame Frame(CleaningPathExecutionRole role)
    {
        var snapshot = _session.Snapshot;
        var axis = AxisSnapshot(role);
        if (axis is null)
        {
            return new CleaningPathMotionAxisFrame(
                role,
                MoveActive: false,
                StopRequested: false,
                CleaningPathMotionOutcome.None,
                null,
                CleaningPathMotionBitState.Unknown,
                null,
                null,
                TpFresh: false,
                RpFresh: false,
                IdleObserved: false,
                CleaningPathStopState.None,
                BrakeApplyCommanded: false);
        }

        var moveActive = false;
        var stopRequested = false;
        foreach (var operation in snapshot.ActiveOperations)
        {
            if (operation.Axis == axis.Axis && operation.Kind == ManualMotionOperationKind.Move)
            {
                moveActive = true;
                stopRequested = operation.StopRequested;
            }
        }

        return new CleaningPathMotionAxisFrame(
            role,
            moveActive,
            stopRequested,
            OutcomeFor(axis.LastOutcomeCode),
            axis.LastOutcomeCode.Length == 0 ? null : axis.LastOutcomeCode,
            MotionFor(axis.Motion),
            axis.PositionTp.Value,
            axis.PositionRp.Value,
            axis.PositionTp.Fresh,
            axis.PositionRp.Fresh,
            axis.Motion == ManualMotionInterpretation.Inactive,
            StopStateFor(axis.StopState),
            axis.BrakeCommandState == ManualMotionBrakeCommandState.ApplyCommanded);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CleaningPathMotionEvent> DrainEvents()
    {
        var snapshot = _session.Snapshot;
        var drained = new List<CleaningPathMotionEvent>();
        foreach (var entry in snapshot.Log)
        {
            if (entry.Sequence <= _lastDrainedLogSequence)
            {
                continue;
            }

            _lastDrainedLogSequence = entry.Sequence;
            if (RoleFor(entry.Axis) is not CleaningPathExecutionRole role)
            {
                continue;
            }

            drained.Add(new CleaningPathMotionEvent(
                entry.Sequence,
                entry.At,
                role,
                OutcomeFor(entry.Code),
                entry.Code));
        }

        return drained;
    }

    /// <inheritdoc/>
    public void RequestMove(CleaningPathExecutionRole role, int targetNative) =>
        _session.RequestMove(AxisFor(role), targetNative);

    /// <inheritdoc/>
    public void RequestStop(CleaningPathExecutionRole role) =>
        _session.RequestStop(AxisFor(role));

    /// <inheritdoc/>
    public void RequestClose() => _session.RequestClose();

    /// <inheritdoc/>
    public void Step() => _session.Step();

    private static ManualMotionAxis AxisFor(string axisId) => axisId switch
    {
        "A" => ManualMotionAxis.A,
        "B" => ManualMotionAxis.B,
        "C" => ManualMotionAxis.C,
        "D" => ManualMotionAxis.D,
        _ => throw new ArgumentException("axis identity is not one of the four typed axes", nameof(axisId)),
    };

    private ManualMotionAxis AxisFor(CleaningPathExecutionRole role) =>
        role == CleaningPathExecutionRole.Horizontal ? _horizontal : _vertical;

    private CleaningPathExecutionRole? RoleFor(ManualMotionAxis? axis)
    {
        if (axis is null)
        {
            return null;
        }

        if (axis == _horizontal)
        {
            return CleaningPathExecutionRole.Horizontal;
        }

        return axis == _vertical ? CleaningPathExecutionRole.Vertical : null;
    }

    private ManualMotionAxisSnapshot? AxisSnapshot(CleaningPathExecutionRole role)
    {
        var wanted = AxisFor(role);
        foreach (var axis in _session.Snapshot.Axes)
        {
            if (axis.Axis == wanted)
            {
                return axis;
            }
        }

        return null;
    }

    private static CleaningPathMotionBitState MotionFor(ManualMotionInterpretation interpretation) => interpretation switch
    {
        ManualMotionInterpretation.Active => CleaningPathMotionBitState.Active,
        ManualMotionInterpretation.Inactive => CleaningPathMotionBitState.Inactive,
        _ => CleaningPathMotionBitState.Unknown,
    };

    private static CleaningPathStopState StopStateFor(ManualAxisStopState state) => state switch
    {
        ManualAxisStopState.Issued => CleaningPathStopState.Issued,
        ManualAxisStopState.Refused => CleaningPathStopState.Refused,
        ManualAxisStopState.NotObservedWithinTimeout => CleaningPathStopState.NotObservedWithinTimeout,
        _ => CleaningPathStopState.None,
    };

    private static CleaningPathMotionOutcome OutcomeFor(string code) => code switch
    {
        ManualMotionCodes.OperationStarted => CleaningPathMotionOutcome.OperationStarted,
        ManualMotionCodes.MoveCompletedPositionVerified => CleaningPathMotionOutcome.MoveCompletedPositionVerified,
        ManualMotionCodes.MoveTimeout => CleaningPathMotionOutcome.MoveTimeout,
        ManualMotionCodes.MoveAbortedByOperator => CleaningPathMotionOutcome.MoveAbortedByOperator,
        ManualMotionCodes.MoveAbortedSessionClosing => CleaningPathMotionOutcome.MoveAbortedSessionClosing,
        ManualMotionCodes.MotionCommandRefused => CleaningPathMotionOutcome.MotionCommandRefused,
        ManualMotionCodes.AbortedBeforeMotion => CleaningPathMotionOutcome.AbortedBeforeMotion,
        ManualMotionCodes.BrakeApplyRefused => CleaningPathMotionOutcome.BrakeApplyRefused,
        ManualMotionCodes.BrakeApplyCommanded => CleaningPathMotionOutcome.BrakeApplyCommanded,
        ManualMotionCodes.StopIssued => CleaningPathMotionOutcome.StopIssued,
        ManualMotionCodes.StopRefused => CleaningPathMotionOutcome.StopRefused,
        ManualMotionCodes.StopNotObservedWithinTimeout => CleaningPathMotionOutcome.StopNotObservedWithinTimeout,
        ManualMotionCodes.CommunicationFault => CleaningPathMotionOutcome.CommunicationFault,
        ManualMotionCodes.StaleResultRejected => CleaningPathMotionOutcome.StaleResultRejected,
        ManualMotionCodes.SessionFaulted => CleaningPathMotionOutcome.SessionFaulted,
        _ => CleaningPathMotionOutcome.Other,
    };
}
