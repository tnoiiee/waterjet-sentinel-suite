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
    private bool _windowActive;
    private CleaningPathMotionOutcome _horizontalOutcome = CleaningPathMotionOutcome.None;
    private string? _horizontalOutcomeSource;
    private CleaningPathMotionOutcome _verticalOutcome = CleaningPathMotionOutcome.None;
    private string? _verticalOutcomeSource;
    private bool _horizontalMoveStartedInWindow;
    private bool _verticalMoveStartedInWindow;

    public ManualMotionCleaningPathBoundary(ManualMotionSession session, string horizontalAxisId, string verticalAxisId)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _horizontal = AxisFor(horizontalAxisId);
        _vertical = AxisFor(verticalAxisId);
    }

    /// <inheritdoc/>
    public bool BeginExecutionEvidenceWindow()
    {
        var snapshot = _session.Snapshot;
        if (snapshot.State is ManualMotionSessionState.Faulted or ManualMotionSessionState.Closed or ManualMotionSessionState.Closing)
        {
            return false;
        }

        // The current log sequence becomes the historical baseline: every earlier entry remains in
        // the Manual Motion history unchanged, but no earlier entry can reach this execution and no
        // sticky per-axis outcome that existed before this point can be adopted as a Move result.
        var baseline = _lastDrainedLogSequence;
        foreach (var entry in snapshot.Log)
        {
            if (entry.Sequence > baseline)
            {
                baseline = entry.Sequence;
            }
        }

        _lastDrainedLogSequence = baseline;
        _horizontalOutcome = CleaningPathMotionOutcome.None;
        _horizontalOutcomeSource = null;
        _verticalOutcome = CleaningPathMotionOutcome.None;
        _verticalOutcomeSource = null;
        _horizontalMoveStartedInWindow = false;
        _verticalMoveStartedInWindow = false;
        _windowActive = true;
        return true;
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
            ScopedOutcomeFor(role),
            ScopedOutcomeSourceFor(role),
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
        var drained = new List<CleaningPathMotionEvent>();
        if (!_windowActive)
        {
            return drained;
        }

        var snapshot = _session.Snapshot;
        foreach (var entry in snapshot.Log)
        {
            if (entry.Sequence <= _lastDrainedLogSequence)
            {
                continue;
            }

            // Sequence is consumed for every later entry (the Manual Motion history is never
            // deleted or rewritten); only execution-relevant evidence is published.
            _lastDrainedLogSequence = entry.Sequence;
            if (RoleFor(entry.Axis) is not CleaningPathExecutionRole role)
            {
                continue;
            }

            var outcome = OutcomeFor(entry.Code);
            if (!IsExecutionEvidence(entry.Operation, outcome))
            {
                continue;
            }

            UpdateScopedOutcome(role, outcome, entry.Code, entry.Operation);
            drained.Add(new CleaningPathMotionEvent(
                entry.Sequence,
                entry.At,
                role,
                outcome,
                entry.Code));
        }

        ApplySessionFaultFallback();
        return drained;
    }

    /// <summary>
    /// Execution-relevant evidence is Move lifecycle evidence, Stop lifecycle evidence used to
    /// supervise or clean up a Move, and session-fault evidence attributable to the active
    /// execution. Historical or unrelated operation kinds (Home, Jog, zero definitions, standalone
    /// brake or pre-execution Stops) are never published to the Cleaning Path execution.
    /// </summary>
    private static bool IsExecutionEvidence(ManualMotionOperationKind operation, CleaningPathMotionOutcome outcome) =>
        operation is ManualMotionOperationKind.Move
            or ManualMotionOperationKind.Stop
            || (operation == ManualMotionOperationKind.None
                && outcome is CleaningPathMotionOutcome.CommunicationFault
                    or CleaningPathMotionOutcome.StaleResultRejected
                    or CleaningPathMotionOutcome.SessionFaulted);

    private void UpdateScopedOutcome(CleaningPathExecutionRole role, CleaningPathMotionOutcome outcome, string sourceCode, ManualMotionOperationKind operation)
    {
        if (operation == ManualMotionOperationKind.Move)
        {
            if (role == CleaningPathExecutionRole.Horizontal)
            {
                _horizontalMoveStartedInWindow = true;
            }
            else
            {
                _verticalMoveStartedInWindow = true;
            }
        }

        if (role == CleaningPathExecutionRole.Horizontal)
        {
            _horizontalOutcome = outcome;
            _horizontalOutcomeSource = sourceCode;
        }
        else
        {
            _verticalOutcome = outcome;
            _verticalOutcomeSource = sourceCode;
        }
    }

    /// <summary>
    /// A session-wide fault terminates every active operation and attributes one logged entry only;
    /// the other active axis's sticky code was overwritten by the fault but produced no entry. That
    /// axis's current-Move outcome may adopt the fault result only while the window is active, its
    /// Move was started inside the window, the session is faulted and the sticky code maps to a
    /// fault outcome — never a historical Home or pre-window result.
    /// </summary>
    private void ApplySessionFaultFallback()
    {
        if (!_windowActive || !SessionFaulted)
        {
            return;
        }

        if (_horizontalMoveStartedInWindow
            && _horizontalOutcome is CleaningPathMotionOutcome.OperationStarted or CleaningPathMotionOutcome.None)
        {
            var sticky = AxisSnapshot(CleaningPathExecutionRole.Horizontal)?.LastOutcomeCode ?? string.Empty;
            var stickyOutcome = OutcomeFor(sticky);
            if (stickyOutcome is CleaningPathMotionOutcome.CommunicationFault
                or CleaningPathMotionOutcome.StaleResultRejected
                or CleaningPathMotionOutcome.SessionFaulted)
            {
                _horizontalOutcome = stickyOutcome;
                _horizontalOutcomeSource = sticky;
            }
        }

        if (_verticalMoveStartedInWindow
            && _verticalOutcome is CleaningPathMotionOutcome.OperationStarted or CleaningPathMotionOutcome.None)
        {
            var sticky = AxisSnapshot(CleaningPathExecutionRole.Vertical)?.LastOutcomeCode ?? string.Empty;
            var stickyOutcome = OutcomeFor(sticky);
            if (stickyOutcome is CleaningPathMotionOutcome.CommunicationFault
                or CleaningPathMotionOutcome.StaleResultRejected
                or CleaningPathMotionOutcome.SessionFaulted)
            {
                _verticalOutcome = stickyOutcome;
                _verticalOutcomeSource = sticky;
            }
        }
    }

    private CleaningPathMotionOutcome ScopedOutcomeFor(CleaningPathExecutionRole role) =>
        role == CleaningPathExecutionRole.Horizontal ? _horizontalOutcome : _verticalOutcome;

    private string? ScopedOutcomeSourceFor(CleaningPathExecutionRole role) =>
        role == CleaningPathExecutionRole.Horizontal ? _horizontalOutcomeSource : _verticalOutcomeSource;

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
