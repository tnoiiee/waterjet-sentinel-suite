using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Wjss.Time;

namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// The Application-owned Manual Motion Commissioning session for WSB1 Axis A (Horizontal) and Axis B
/// (Vertical). It owns the operation lifecycle; the UI only posts typed requests and reads snapshots.
///
/// Threading contract:
///   - every Request* member and <see cref="Snapshot"/> are safe from any thread: a request is only
///     enqueued and is applied by the next <see cref="Step"/>;
///   - <see cref="Step"/> is called by exactly one driver thread; a concurrent call is a programming
///     error and throws;
///   - one step makes at most one port call (the protective Stop on a fault is the single
///     exception), so the controller sees one native operation at a time.
///
/// Behaviour that is fixed by construction:
///   - one active operation at a time, so at most one axis is in motion within this session;
///   - Stop pre-empts a multi-step Jog, Home or Move between command steps;
///   - an observation never issues a command; completion is decided only by the operation lifecycle;
///   - a refused read leaves no value behind it: nothing is fabricated, and an old value goes stale;
///   - a result from an earlier port generation is rejected;
///   - a communication fault makes the session FAULTED: nothing is retried and nothing reconnects;
///   - every motion-producing value comes from the Owner-local profile; a missing value refuses.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class ManualMotionSession
{
    private const int MaximumLogEntries = 200;
    private const int PublishedLogEntries = 60;
    private const int MaximumTransitionsPerStep = 16;

    private static readonly ManualMotionAxisProfile EmptyAxisProfile = new();
    private static readonly ReadOnlyCollection<string> NotAllowedItems = Array.AsReadOnly(new[] { "axis is not listed in the profile" });

    private readonly IManualMotionPort _port;
    private readonly IClock _clock;
    private readonly ManualMotionProfile? _profile;
    private readonly string _profileCode;
    private readonly ConcurrentQueue<Request> _requests = new();
    private readonly AxisRuntime _axisA = new(ManualMotionAxis.A);
    private readonly AxisRuntime _axisB = new(ManualMotionAxis.B);
    private readonly List<ManualMotionLogEntry> _log = new();
    private readonly Queue<ManualMotionAxis> _pendingStops = new();

    private ManualMotionSnapshot _snapshot;
    private int _stepping;
    private ManualMotionSessionState _state;
    private long _generation;
    private long _portGeneration;
    private long _callSequence;
    private long _logSequence;
    private long _operationSequence;
    private OperationRuntime? _active;
    private DateTimeOffset _nextObservationCycleDue;
    private List<ManualMotionPortRead>? _observationCycle;
    private int _observationCycleIndex;
    private DateTimeOffset _observationCycleStartedAt;
    private long _observationCycleOperationId;
    private bool _observationCycleStopPriority;
    private bool _observationCycleMotionBegun;
    private bool _closeRequested;
    private bool _portClosed;
    private string _openCode = string.Empty;
    private string _closeCode = string.Empty;

    /// <summary>
    /// Creates a session over one port. A refused profile, or the built-in simulation profile offered
    /// to a hardware-capable port, refuses the session before the port is ever opened.
    /// </summary>
    public ManualMotionSession(IManualMotionPort port, ManualMotionProfileResult profileResult, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(profileResult);
        ArgumentNullException.ThrowIfNull(clock);

        _port = port;
        _clock = clock;

        if (!profileResult.Accepted || profileResult.Profile is null)
        {
            _profile = null;
            _profileCode = profileResult.Code;
            _state = ManualMotionSessionState.ProfileRefused;
        }
        else if (port.Mode == ManualMotionPortMode.HardwareCapable
                 && profileResult.Profile.Source == ManualMotionProfileSource.SimulationBuiltIn)
        {
            _profile = null;
            _profileCode = ManualMotionCodes.ProfileSimulationSourceRefusedForHardware;
            _state = ManualMotionSessionState.ProfileRefused;
        }
        else
        {
            _profile = profileResult.Profile;
            _profileCode = profileResult.Code;
            _state = ManualMotionSessionState.NotStarted;
        }

        var now = clock.UtcNow;
        _nextObservationCycleDue = now;
        if (_state == ManualMotionSessionState.ProfileRefused)
        {
            AppendLog(now, null, ManualMotionOperationKind.None, _profileCode);
        }

        _snapshot = BuildSnapshot(now);
    }

    /// <summary>The port identity of this session.</summary>
    public ManualMotionPortMode Mode => _port.Mode;

    /// <summary>The latest published state. Safe to read from any thread.</summary>
    public ManualMotionSnapshot Snapshot => Volatile.Read(ref _snapshot);

    // ---------------------------------------------------------------------------- requests

    /// <summary>Requests one brake release on one axis.</summary>
    public void RequestBrakeRelease(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.BrakeRelease, axis));

    /// <summary>Requests one brake apply on one axis.</summary>
    public void RequestBrakeApply(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.BrakeApply, axis));

    /// <summary>Starts one hold-to-run Jog press. <paramref name="pressId"/> identifies the press for heartbeats and release.</summary>
    public void RequestJogStart(ManualMotionAxis axis, ManualMotionJogDirection direction, long pressId) =>
        Enqueue(new Request(RequestKind.JogStart, axis) { Direction = direction, PressId = pressId });

    /// <summary>Reports that the hold-to-run press is still held.</summary>
    public void RequestJogHeartbeat(ManualMotionAxis axis, long pressId) =>
        Enqueue(new Request(RequestKind.JogHeartbeat, axis) { PressId = pressId });

    /// <summary>Ends one hold-to-run press: release, lost capture or application deactivation.</summary>
    public void RequestJogRelease(ManualMotionAxis axis, long pressId, ManualMotionJogEndReason reason) =>
        Enqueue(new Request(RequestKind.JogRelease, axis) { PressId = pressId, Reason = reason });

    /// <summary>Requests the typed Stop of one axis. An active motion operation on that axis is stopped through its lifecycle.</summary>
    public void RequestStop(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.Stop, axis));

    /// <summary>Requests Home on one axis.</summary>
    public void RequestHome(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.Home, axis));

    /// <summary>Requests define-position-zero on one axis (zero is the only operand).</summary>
    public void RequestDefinePositionZero(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.DefinePositionZero, axis));

    /// <summary>Requests define-dual-zero on one axis (zero is the only operand; the effect stays UNVERIFIED).</summary>
    public void RequestDefineDualZero(ManualMotionAxis axis) => Enqueue(new Request(RequestKind.DefineDualZero, axis));

    /// <summary>Requests one absolute bounded Move of one axis to a native-unit target.</summary>
    public void RequestMove(ManualMotionAxis axis, int target) => Enqueue(new Request(RequestKind.Move, axis) { Target = target });

    /// <summary>Requests the session close: an active motion operation is stopped first, then the port is closed.</summary>
    public void RequestClose() => Enqueue(new Request(RequestKind.Close, ManualMotionAxis.A));

    // ------------------------------------------------------------------------------ driver

    /// <summary>
    /// Applies queued requests, then advances the session by at most one port call, then publishes
    /// a new snapshot. Called by exactly one driver thread.
    /// </summary>
    public void Step()
    {
        if (Interlocked.CompareExchange(ref _stepping, 1, 0) != 0)
        {
            throw new InvalidOperationException("ManualMotionSession.Step is driven by exactly one thread.");
        }

        try
        {
            var now = _clock.UtcNow;
            try
            {
                DrainRequests(now);
                StepCore(now);
            }
            catch (Exception)
            {
                // A software defect must never leave motion unsupervised: the session faults, makes
                // one protective Stop attempt for an active motion operation and issues nothing else.
                FaultSession(now, _active?.Axis, _active?.Kind ?? ManualMotionOperationKind.None, ManualMotionCodes.SessionFaulted);
            }

            Volatile.Write(ref _snapshot, BuildSnapshot(now));
        }
        finally
        {
            Volatile.Write(ref _stepping, 0);
        }
    }

    private void Enqueue(Request request) => _requests.Enqueue(request);

    private void DrainRequests(DateTimeOffset now)
    {
        while (_requests.TryDequeue(out var request))
        {
            ApplyRequest(request, now);
        }
    }

    private void StepCore(DateTimeOffset now)
    {
        switch (_state)
        {
            case ManualMotionSessionState.NotStarted:
                if (_closeRequested)
                {
                    _state = ManualMotionSessionState.Closed;
                    _closeCode = ManualMotionCodes.SessionClosed;
                    AppendLog(now, null, ManualMotionOperationKind.None, _closeCode);
                    return;
                }

                OpenPort(now);
                return;

            case ManualMotionSessionState.OpenRefused:
                if (!_portClosed)
                {
                    ClosePort(now, keepState: true);
                }

                return;

            case ManualMotionSessionState.Ready:
            case ManualMotionSessionState.Closing:
                StepOperational(now);
                return;

            case ManualMotionSessionState.Faulted:
                if (_pendingStops.Count > 0)
                {
                    ExecuteStandaloneStop(now);
                    return;
                }

                if (_closeRequested && !_portClosed)
                {
                    ClosePort(now, keepState: false);
                }

                return;

            default:
                return;
        }
    }

    private void StepOperational(DateTimeOffset now)
    {
        if (_pendingStops.Count > 0)
        {
            ExecuteStandaloneStop(now);
            return;
        }

        if (_active is not null && AdvanceActive(now))
        {
            return;
        }

        if (_state != ManualMotionSessionState.Ready && _state != ManualMotionSessionState.Closing)
        {
            return;
        }

        if (_active is null && _state == ManualMotionSessionState.Closing)
        {
            ClosePort(now, keepState: false);
            return;
        }

        Observe(now);
    }

    // ---------------------------------------------------------------------- request handling

    private void ApplyRequest(Request request, DateTimeOffset now)
    {
        switch (request.Kind)
        {
            case RequestKind.Close:
                ApplyClose(now);
                return;
            case RequestKind.JogHeartbeat:
                ApplyJogHeartbeat(request, now);
                return;
            case RequestKind.JogRelease:
                ApplyJogRelease(request);
                return;
            case RequestKind.Stop:
                ApplyStop(request, now);
                return;
            default:
                ApplyOperationRequest(request, now);
                return;
        }
    }

    private void ApplyClose(DateTimeOffset now)
    {
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        if (_state == ManualMotionSessionState.Ready)
        {
            _state = ManualMotionSessionState.Closing;
            if (_active is not null && IsMotionOperation(_active.Kind))
            {
                RequestOperationStop(_active, StopReason.SessionClosing);
            }
        }

        AppendLog(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionClosing);
    }

    private void ApplyJogHeartbeat(Request request, DateTimeOffset now)
    {
        if (_active is { Kind: ManualMotionOperationKind.Jog } jog
            && jog.Axis == request.Axis
            && jog.PressId == request.PressId
            && !jog.StopRequested)
        {
            jog.LastHeartbeatAt = now;
        }
    }

    private void ApplyJogRelease(Request request)
    {
        if (_active is { Kind: ManualMotionOperationKind.Jog } jog
            && jog.Axis == request.Axis
            && jog.PressId == request.PressId)
        {
            var reason = request.Reason switch
            {
                ManualMotionJogEndReason.CaptureLost => StopReason.CaptureLost,
                ManualMotionJogEndReason.Deactivated => StopReason.Deactivated,
                _ => StopReason.Released,
            };

            RequestOperationStop(jog, reason);
        }
    }

    private void ApplyStop(Request request, DateTimeOffset now)
    {
        if (!Enum.IsDefined(request.Axis))
        {
            AppendLog(now, null, ManualMotionOperationKind.Stop, ManualMotionCodes.AxisOutOfDomain);
            return;
        }

        if (_active is not null && _active.Axis == request.Axis && IsMotionOperation(_active.Kind))
        {
            RequestOperationStop(_active, StopReason.Operator);
            return;
        }

        if (_state is ManualMotionSessionState.Ready or ManualMotionSessionState.Closing or ManualMotionSessionState.Faulted)
        {
            if (!_pendingStops.Contains(request.Axis))
            {
                _pendingStops.Enqueue(request.Axis);
            }

            return;
        }

        Refuse(now, request.Axis, ManualMotionOperationKind.Stop, ManualMotionCodes.SessionNotReady);
    }

    private void ApplyOperationRequest(Request request, DateTimeOffset now)
    {
        var kind = OperationKindFor(request.Kind);
        if (!Enum.IsDefined(request.Axis))
        {
            AppendLog(now, null, kind, ManualMotionCodes.AxisOutOfDomain);
            return;
        }

        var refusal = CheckCommonPreconditions(request.Axis) ?? CheckOperationPreconditions(request, kind, now);
        if (refusal is not null)
        {
            Refuse(now, request.Axis, kind, refusal);
            return;
        }

        StartOperation(request, kind, now);
    }

    private string? CheckCommonPreconditions(ManualMotionAxis axis)
    {
        if (_closeRequested || _state == ManualMotionSessionState.Closing)
        {
            return ManualMotionCodes.SessionClosing;
        }

        if (_state != ManualMotionSessionState.Ready)
        {
            return ManualMotionCodes.SessionNotReady;
        }

        if (_profile?.For(axis) is null)
        {
            return ManualMotionCodes.AxisNotAllowedByProfile;
        }

        return _active is not null ? ManualMotionCodes.OperationInProgress : null;
    }

    private string? CheckOperationPreconditions(Request request, ManualMotionOperationKind kind, DateTimeOffset now)
    {
        var axis = request.Axis;
        var p = AxisProfile(axis);
        switch (kind)
        {
            case ManualMotionOperationKind.BrakeRelease:
            case ManualMotionOperationKind.BrakeApply:
                return CheckBrakeConfigured(p, requireSettle: false);

            case ManualMotionOperationKind.Jog:
                return CheckBrakeConfigured(p, requireSettle: true)
                    ?? (p.JogSpeed is null ? ManualMotionCodes.JogSpeedNotConfigured : null)
                    ?? (_profile?.JogHeartbeatTimeoutMilliseconds is null || _profile?.JogMaximumHoldMilliseconds is null
                        ? ManualMotionCodes.JogTimingNotConfigured
                        : null)
                    ?? (p.StopTimeoutMilliseconds is null ? ManualMotionCodes.StopTimeoutNotConfigured : null)
                    ?? CheckStationary(axis, p, now)
                    ?? CheckOtherAxisNotMoving(axis, now);

            case ManualMotionOperationKind.Home:
                return CheckBrakeConfigured(p, requireSettle: true)
                    ?? (p.HomeSpeed is null || p.HomeTimeoutMilliseconds is null ? ManualMotionCodes.HomeParametersNotConfigured : null)
                    ?? (p.StopTimeoutMilliseconds is null ? ManualMotionCodes.StopTimeoutNotConfigured : null)
                    ?? (p.HomeStatusBit is null || p.HomeActiveValue is null ? ManualMotionCodes.HomeFeedbackNotConfigured : null)
                    ?? (p.PositionTolerance is null ? ManualMotionCodes.PositionToleranceNotConfigured : null)
                    ?? CheckStationary(axis, p, now)
                    ?? CheckOtherAxisNotMoving(axis, now)
                    ?? (InterpretHome(axis, now) == ManualMotionInterpretation.Active ? ManualMotionCodes.HomeInputAlreadyActive : null);

            case ManualMotionOperationKind.DefinePositionZero:
                return CheckStationary(axis, p, now);

            case ManualMotionOperationKind.DefineDualZero:
                return (p.DualZeroPermitted ? null : ManualMotionCodes.DualZeroNotPermittedByProfile)
                    ?? CheckStationary(axis, p, now);

            case ManualMotionOperationKind.Move:
                return (Runtime(axis).HomeState != ManualMotionHomeState.Homed ? ManualMotionCodes.MoveRequiresHomed : null)
                    ?? (p.MoveAcceleration is null || p.MoveSpeed is null || p.MoveDeceleration is null || p.MotionTimeoutMilliseconds is null
                        ? ManualMotionCodes.MoveParametersNotConfigured
                        : null)
                    ?? (p.MoveMinimumTarget is null || p.MoveMaximumTarget is null ? ManualMotionCodes.MoveBoundsNotConfigured : null)
                    ?? (request.Target < (p.MoveMinimumTarget ?? 0) || request.Target > (p.MoveMaximumTarget ?? 0)
                        ? ManualMotionCodes.MoveTargetOutOfBounds
                        : null)
                    ?? (p.PositionTolerance is null ? ManualMotionCodes.PositionToleranceNotConfigured : null)
                    ?? (p.StopTimeoutMilliseconds is null ? ManualMotionCodes.StopTimeoutNotConfigured : null)
                    ?? CheckBrakeConfigured(p, requireSettle: true)
                    ?? CheckStationary(axis, p, now)
                    ?? CheckOtherAxisNotMoving(axis, now);

            default:
                return ManualMotionCodes.SessionNotReady;
        }
    }

    private static string? CheckBrakeConfigured(ManualMotionAxisProfile axisProfile, bool requireSettle)
    {
        if (axisProfile.BrakeOutput is null)
        {
            return ManualMotionCodes.BrakeOutputNotConfigured;
        }

        if (axisProfile.BrakeReleaseAction is null)
        {
            return ManualMotionCodes.BrakePolarityNotConfigured;
        }

        return requireSettle && axisProfile.BrakeSettleMilliseconds is null
            ? ManualMotionCodes.BrakeSettleNotConfigured
            : null;
    }

    private string? CheckStationary(ManualMotionAxis axis, ManualMotionAxisProfile axisProfile, DateTimeOffset now)
    {
        if (!ObservationTimingConfigured)
        {
            return ManualMotionCodes.ObservationTimingNotConfigured;
        }

        if (axisProfile.MotionStatusBit is null || axisProfile.MotionActiveValue is null)
        {
            return ManualMotionCodes.MotionFeedbackNotConfigured;
        }

        return InterpretMotion(axis, now) switch
        {
            ManualMotionInterpretation.Inactive => null,
            ManualMotionInterpretation.Active => ManualMotionCodes.AxisMovingObserved,
            _ => ManualMotionCodes.ObservationNotFresh,
        };
    }

    private string? CheckOtherAxisNotMoving(ManualMotionAxis axis, DateTimeOffset now)
    {
        var other = axis == ManualMotionAxis.A ? ManualMotionAxis.B : ManualMotionAxis.A;
        return InterpretMotion(other, now) == ManualMotionInterpretation.Active
            ? ManualMotionCodes.OtherAxisMovingObserved
            : null;
    }

    private bool ObservationTimingConfigured =>
        _profile?.ObservationIntervalMilliseconds is not null && _profile?.ObservationFreshnessMilliseconds is not null;

    private void StartOperation(Request request, ManualMotionOperationKind kind, DateTimeOffset now)
    {
        var axis = request.Axis;
        var p = AxisProfile(axis);
        _operationSequence++;
        var op = new OperationRuntime(_operationSequence, kind, axis, now);

        switch (kind)
        {
            case ManualMotionOperationKind.BrakeRelease:
            case ManualMotionOperationKind.BrakeApply:
            case ManualMotionOperationKind.DefineDualZero:
                op.Phase = ManualMotionOperationPhase.Dispatching;
                break;

            case ManualMotionOperationKind.DefinePositionZero:
                CaptureZeroVerification(op, p);
                op.Phase = ManualMotionOperationPhase.DefiningZero;
                break;

            case ManualMotionOperationKind.Jog:
            {
                CaptureMotion(op, p);
                op.HeartbeatTimeout = Milliseconds(_profile?.JogHeartbeatTimeoutMilliseconds ?? 0);
                op.RunTimeout = Milliseconds(_profile?.JogMaximumHoldMilliseconds ?? 0);
                op.PressId = request.PressId;
                op.LastHeartbeatAt = now;
                var speed = p.JogSpeed ?? 0;
                var signedSpeed = request.Direction == ManualMotionJogDirection.Negative ? -speed : speed;
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.JogSpeed, axis, signedSpeed, 0));
                op.Phase = ManualMotionOperationPhase.ReleasingBrake;
                break;
            }

            case ManualMotionOperationKind.Home:
                CaptureMotion(op, p);
                CaptureZeroVerification(op, p);
                op.HomeBit = p.HomeStatusBit ?? 0;
                op.HomeActive = p.HomeActiveValue ?? 0;
                op.RunTimeout = Milliseconds(p.HomeTimeoutMilliseconds ?? 0);
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.HomeSpeed, axis, p.HomeSpeed ?? 0, 0));
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.Home, axis, 0, 0));
                op.Phase = ManualMotionOperationPhase.ReleasingBrake;
                break;

            case ManualMotionOperationKind.Move:
                CaptureMotion(op, p);
                op.Tolerance = p.PositionTolerance ?? 0;
                op.RunTimeout = Milliseconds(p.MotionTimeoutMilliseconds ?? 0);
                op.Target = request.Target;
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.Acceleration, axis, p.MoveAcceleration ?? 0, 0));
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.Speed, axis, p.MoveSpeed ?? 0, 0));
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.Deceleration, axis, p.MoveDeceleration ?? 0, 0));
                op.PendingCommands.Enqueue(new ManualMotionPortCommand(ManualMotionPortCommandKind.PositionAbsolute, axis, request.Target, 0));
                op.Phase = ManualMotionOperationPhase.ReleasingBrake;
                break;

            default:
                return;
        }

        op.PhaseStartedAt = now;
        _active = op;
        AppendLog(now, axis, kind, ManualMotionCodes.OperationStarted);
    }

    private static void CaptureMotion(OperationRuntime op, ManualMotionAxisProfile p)
    {
        op.BrakeSettle = Milliseconds(p.BrakeSettleMilliseconds ?? 0);
        op.StopTimeout = Milliseconds(p.StopTimeoutMilliseconds ?? 0);
        op.MotionBit = p.MotionStatusBit ?? 0;
        op.MotionActive = p.MotionActiveValue ?? 0;
    }

    private static void CaptureZeroVerification(OperationRuntime op, ManualMotionAxisProfile p)
    {
        op.HasZeroVerification = p.PositionTolerance is not null && p.StopTimeoutMilliseconds is not null;
        op.Tolerance = p.PositionTolerance ?? 0;
        op.StopTimeout = Milliseconds(p.StopTimeoutMilliseconds ?? 0);
    }

    // ------------------------------------------------------------------- operation lifecycle

    private bool AdvanceActive(DateTimeOffset now)
    {
        for (var transitions = 0; transitions < MaximumTransitionsPerStep; transitions++)
        {
            var op = _active;
            if (op is null)
            {
                return false;
            }

            var progress = AdvanceOnce(op, now);
            if (progress == Progress.CalledPort)
            {
                return true;
            }

            if (progress == Progress.Waiting)
            {
                return false;
            }
        }

        return false;
    }

    private Progress AdvanceOnce(OperationRuntime op, DateTimeOffset now) => op.Phase switch
    {
        ManualMotionOperationPhase.Dispatching => DoDispatching(op, now),
        ManualMotionOperationPhase.ReleasingBrake => DoReleasingBrake(op, now),
        ManualMotionOperationPhase.BrakeSettling => DoBrakeSettling(op, now),
        ManualMotionOperationPhase.Configuring => DoConfiguring(op, now),
        ManualMotionOperationPhase.Beginning => DoBeginning(op, now),
        ManualMotionOperationPhase.Running => DoRunning(op, now),
        ManualMotionOperationPhase.Stopping => DoStopping(op, now),
        ManualMotionOperationPhase.AwaitingStopped => DoAwaitingStopped(op, now),
        ManualMotionOperationPhase.DefiningZero => DoDefiningZero(op, now),
        ManualMotionOperationPhase.VerifyingZero => DoVerifyingZero(op, now),
        ManualMotionOperationPhase.ApplyingBrake => DoApplyingBrake(op, now),
        _ => CompleteWithoutCall(op, now, ManualMotionCodes.AbortedBeforeMotion),
    };

    private Progress DoDispatching(OperationRuntime op, DateTimeOffset now)
    {
        var runtime = Runtime(op.Axis);
        switch (op.Kind)
        {
            case ManualMotionOperationKind.BrakeRelease:
            {
                var outcome = ExecuteCommand(BrakeCommand(op.Axis, AxisProfile(op.Axis), release: true));
                if (Interrupted(outcome, now, op))
                {
                    return Progress.CalledPort;
                }

                runtime.BrakeCommandState = outcome.Accepted
                    ? ManualMotionBrakeCommandState.ReleaseCommanded
                    : ManualMotionBrakeCommandState.Uncertain;
                CompleteNow(op, now, outcome.Accepted ? ManualMotionCodes.BrakeReleaseCommanded : ManualMotionCodes.BrakeReleaseRefused);
                return Progress.CalledPort;
            }

            case ManualMotionOperationKind.BrakeApply:
            {
                var outcome = ExecuteCommand(BrakeCommand(op.Axis, AxisProfile(op.Axis), release: false));
                if (Interrupted(outcome, now, op))
                {
                    return Progress.CalledPort;
                }

                runtime.BrakeCommandState = outcome.Accepted
                    ? ManualMotionBrakeCommandState.ApplyCommanded
                    : ManualMotionBrakeCommandState.Uncertain;
                CompleteNow(op, now, outcome.Accepted ? ManualMotionCodes.BrakeApplyCommanded : ManualMotionCodes.BrakeApplyRefused);
                return Progress.CalledPort;
            }

            default:
            {
                var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.DefineDualZero, op.Axis, 0, 0));
                if (Interrupted(outcome, now, op))
                {
                    return Progress.CalledPort;
                }

                if (outcome.Accepted)
                {
                    runtime.DualZeroState = ManualMotionDualZeroState.DispatchedUnverified;
                    ClearHome(runtime, now, op);
                    CompleteNow(op, now, ManualMotionCodes.DualZeroDispatchedEffectUnverified);
                }
                else
                {
                    runtime.DualZeroState = ManualMotionDualZeroState.Refused;
                    CompleteNow(op, now, ManualMotionCodes.DualZeroRefused);
                }

                return Progress.CalledPort;
            }
        }
    }

    private Progress DoReleasingBrake(OperationRuntime op, DateTimeOffset now)
    {
        if (op.StopRequested)
        {
            return CompleteWithoutCall(op, now, ManualMotionCodes.AbortedBeforeMotion);
        }

        var runtime = Runtime(op.Axis);
        var outcome = ExecuteCommand(BrakeCommand(op.Axis, AxisProfile(op.Axis), release: true));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        if (!outcome.Accepted)
        {
            runtime.BrakeCommandState = ManualMotionBrakeCommandState.Uncertain;
            CompleteNow(op, now, ManualMotionCodes.BrakeReleaseRefused);
            return Progress.CalledPort;
        }

        runtime.BrakeCommandState = ManualMotionBrakeCommandState.ReleaseCommanded;
        op.BrakeReleasedByOperation = true;
        SetPhase(op, ManualMotionOperationPhase.BrakeSettling, now);
        return Progress.CalledPort;
    }

    private static Progress DoBrakeSettling(OperationRuntime op, DateTimeOffset now)
    {
        if (op.StopRequested)
        {
            op.EndCode ??= ManualMotionCodes.AbortedBeforeMotion;
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.Transitioned;
        }

        if (now - op.PhaseStartedAt >= op.BrakeSettle)
        {
            SetPhase(op, ManualMotionOperationPhase.Configuring, now);
            return Progress.Transitioned;
        }

        return Progress.Waiting;
    }

    private Progress DoConfiguring(OperationRuntime op, DateTimeOffset now)
    {
        if (op.StopRequested)
        {
            op.EndCode ??= ManualMotionCodes.AbortedBeforeMotion;
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.Transitioned;
        }

        if (op.PendingCommands.Count == 0)
        {
            SetPhase(op, ManualMotionOperationPhase.Beginning, now);
            return Progress.Transitioned;
        }

        var command = op.PendingCommands.Dequeue();
        var outcome = ExecuteCommand(command);
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        if (!outcome.Accepted)
        {
            op.EndCode = ManualMotionCodes.MotionCommandRefused;
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
        }

        return Progress.CalledPort;
    }

    private Progress DoBeginning(OperationRuntime op, DateTimeOffset now)
    {
        if (op.StopRequested)
        {
            op.EndCode ??= ManualMotionCodes.AbortedBeforeMotion;
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.Transitioned;
        }

        var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.Begin, op.Axis, 0, 0));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        // A refused or unclassified begin may still have started motion, so it is stopped through the
        // same supervised Stop path before the brake is applied.
        op.MotionBegun = true;
        op.BeginSequence = outcome.Sequence;
        op.BeginAt = now;
        if (!outcome.Accepted)
        {
            op.EndCode = ManualMotionCodes.MotionCommandRefused;
            SetPhase(op, ManualMotionOperationPhase.Stopping, now);
            return Progress.CalledPort;
        }

        if (op.Kind == ManualMotionOperationKind.Home)
        {
            Runtime(op.Axis).HomeState = ManualMotionHomeState.Homing;
        }

        SetPhase(op, ManualMotionOperationPhase.Running, now);
        return Progress.CalledPort;
    }

    private Progress DoRunning(OperationRuntime op, DateTimeOffset now)
    {
        if (op.StopRequested)
        {
            op.EndCode ??= EndCodeForStop(op.Kind, op.StopReason);
            SetPhase(op, ManualMotionOperationPhase.Stopping, now);
            return Progress.Transitioned;
        }

        var elapsed = now - op.BeginAt;
        switch (op.Kind)
        {
            case ManualMotionOperationKind.Jog:
                if (now - op.LastHeartbeatAt > op.HeartbeatTimeout)
                {
                    return StopFromRunning(op, now, StopReason.HeartbeatExpired);
                }

                return elapsed > op.RunTimeout ? StopFromRunning(op, now, StopReason.MaximumHold) : Progress.Waiting;

            case ManualMotionOperationKind.Home:
                if (elapsed > op.RunTimeout)
                {
                    return StopFromRunning(op, now, StopReason.Timeout);
                }

                if (StatusBitObservedAfter(op.Axis, op.BeginSequence, now, op.HomeBit, op.HomeActive) == true)
                {
                    op.HomeDetected = true;
                    SetPhase(op, ManualMotionOperationPhase.Stopping, now);
                    return Progress.Transitioned;
                }

                return Progress.Waiting;

            case ManualMotionOperationKind.Move:
                if (elapsed > op.RunTimeout)
                {
                    return StopFromRunning(op, now, StopReason.Timeout);
                }

                if (MoveReached(op, now))
                {
                    op.EndCode = ManualMotionCodes.MoveCompletedPositionVerified;
                    SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
                    return Progress.Transitioned;
                }

                return Progress.Waiting;

            default:
                return Progress.Waiting;
        }
    }

    private static Progress StopFromRunning(OperationRuntime op, DateTimeOffset now, StopReason reason)
    {
        RequestOperationStop(op, reason);
        op.EndCode ??= EndCodeForStop(op.Kind, op.StopReason);
        SetPhase(op, ManualMotionOperationPhase.Stopping, now);
        return Progress.Transitioned;
    }

    private Progress DoStopping(OperationRuntime op, DateTimeOffset now)
    {
        if (!op.MotionBegun)
        {
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.Transitioned;
        }

        var runtime = Runtime(op.Axis);
        var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.Stop, op.Axis, 0, 0));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        runtime.StopState = outcome.Accepted ? ManualAxisStopState.Issued : ManualAxisStopState.Refused;
        op.StopSequence = outcome.Sequence;
        op.StopIssuedAt = now;
        SetPhase(op, ManualMotionOperationPhase.AwaitingStopped, now);
        return Progress.CalledPort;
    }

    private Progress DoAwaitingStopped(OperationRuntime op, DateTimeOffset now)
    {
        var moving = StatusBitObservedAfter(op.Axis, op.StopSequence, now, op.MotionBit, op.MotionActive);
        if (moving == false)
        {
            SetPhase(
                op,
                op.Kind == ManualMotionOperationKind.Home && op.HomeDetected
                    ? ManualMotionOperationPhase.DefiningZero
                    : ManualMotionOperationPhase.ApplyingBrake,
                now);
            return Progress.Transitioned;
        }

        if (now - op.StopIssuedAt > op.StopTimeout)
        {
            var runtime = Runtime(op.Axis);
            runtime.StopState = ManualAxisStopState.NotObservedWithinTimeout;
            if (op.Kind == ManualMotionOperationKind.Home)
            {
                runtime.HomeState = ManualMotionHomeState.HomeFailed;
            }

            // The brake is NOT applied automatically: the axis may still be moving. The Operator keeps
            // the explicit Stop and Brake Apply controls and the independent protection.
            return CompleteWithoutCall(op, now, ManualMotionCodes.StopNotObservedWithinTimeout);
        }

        return Progress.Waiting;
    }

    private Progress DoDefiningZero(OperationRuntime op, DateTimeOffset now)
    {
        var runtime = Runtime(op.Axis);
        var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.DefinePositionZero, op.Axis, 0, 0));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        if (!outcome.Accepted)
        {
            if (op.Kind == ManualMotionOperationKind.Home)
            {
                runtime.HomeState = ManualMotionHomeState.HomeFailed;
                op.EndCode = ManualMotionCodes.PositionZeroRefused;
                SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
                return Progress.CalledPort;
            }

            CompleteNow(op, now, ManualMotionCodes.PositionZeroRefused);
            return Progress.CalledPort;
        }

        runtime.CoordinateState = ManualMotionCoordinateState.ZeroDispatched;
        if (op.Kind != ManualMotionOperationKind.Home)
        {
            ClearHome(runtime, now, op);
        }

        op.ZeroSequence = outcome.Sequence;
        op.ZeroAt = now;
        SetPhase(op, ManualMotionOperationPhase.VerifyingZero, now);
        return Progress.CalledPort;
    }

    private Progress DoVerifyingZero(OperationRuntime op, DateTimeOffset now)
    {
        if (!op.HasZeroVerification)
        {
            return FinishZero(op, now, verified: null);
        }

        var runtime = Runtime(op.Axis);
        var tp = ZeroReading(runtime.PositionTp, op.ZeroSequence, now, op.Tolerance);
        var rp = ZeroReading(runtime.PositionRp, op.ZeroSequence, now, op.Tolerance);
        if (tp is bool tpWithin && rp is bool rpWithin)
        {
            return FinishZero(op, now, tpWithin && rpWithin);
        }

        return now - op.ZeroAt > op.StopTimeout ? FinishZero(op, now, verified: null) : Progress.Waiting;
    }

    private Progress FinishZero(OperationRuntime op, DateTimeOffset now, bool? verified)
    {
        var runtime = Runtime(op.Axis);
        if (verified == true)
        {
            runtime.CoordinateState = ManualMotionCoordinateState.ZeroVerified;
        }
        else if (verified == false)
        {
            runtime.CoordinateState = ManualMotionCoordinateState.ZeroVerificationFailed;
        }

        if (op.Kind == ManualMotionOperationKind.Home)
        {
            runtime.HomeState = verified switch
            {
                true => ManualMotionHomeState.Homed,
                false => ManualMotionHomeState.HomeFailed,
                _ => ManualMotionHomeState.HomedUnverified,
            };
            op.EndCode = verified switch
            {
                true => ManualMotionCodes.HomeCompletedZeroVerified,
                false => ManualMotionCodes.HomeZeroVerificationFailed,
                _ => ManualMotionCodes.HomeCompletedZeroNotVerified,
            };
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.Transitioned;
        }

        var code = verified switch
        {
            true => ManualMotionCodes.PositionZeroVerified,
            false => ManualMotionCodes.PositionZeroVerificationFailed,
            _ => ManualMotionCodes.PositionZeroNotVerified,
        };
        return CompleteWithoutCall(op, now, code);
    }

    private Progress DoApplyingBrake(OperationRuntime op, DateTimeOffset now)
    {
        var runtime = Runtime(op.Axis);
        var endCode = op.EndCode ?? (op.MotionBegun ? ManualMotionCodes.StopIssued : ManualMotionCodes.AbortedBeforeMotion);
        if (op.Kind == ManualMotionOperationKind.Home && runtime.HomeState == ManualMotionHomeState.Homing)
        {
            runtime.HomeState = ManualMotionHomeState.HomeFailed;
        }

        if (!op.BrakeReleasedByOperation)
        {
            return CompleteWithoutCall(op, now, endCode);
        }

        var outcome = ExecuteCommand(BrakeCommand(op.Axis, AxisProfile(op.Axis), release: false));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        if (outcome.Accepted)
        {
            runtime.BrakeCommandState = ManualMotionBrakeCommandState.ApplyCommanded;
            CompleteNow(op, now, endCode);
        }
        else
        {
            runtime.BrakeCommandState = ManualMotionBrakeCommandState.Uncertain;
            AppendLog(now, op.Axis, op.Kind, endCode);
            CompleteNow(op, now, ManualMotionCodes.BrakeApplyRefused);
        }

        return Progress.CalledPort;
    }

    private Progress CompleteWithoutCall(OperationRuntime op, DateTimeOffset now, string code)
    {
        CompleteNow(op, now, code);
        return Progress.Transitioned;
    }

    private void CompleteNow(OperationRuntime op, DateTimeOffset now, string code)
    {
        Runtime(op.Axis).LastOutcomeCode = code;
        AppendLog(now, op.Axis, op.Kind, code);
        if (ReferenceEquals(_active, op))
        {
            _active = null;
        }
    }

    private void ClearHome(AxisRuntime runtime, DateTimeOffset now, OperationRuntime op)
    {
        if (runtime.HomeState == ManualMotionHomeState.NotHomed)
        {
            return;
        }

        runtime.HomeState = ManualMotionHomeState.NotHomed;
        AppendLog(now, op.Axis, op.Kind, ManualMotionCodes.CoordinateRedefinedHomeCleared);
    }

    private static void RequestOperationStop(OperationRuntime op, StopReason reason)
    {
        if (op.StopRequested)
        {
            return;
        }

        op.StopRequested = true;
        op.StopReason = reason;
    }

    private static void SetPhase(OperationRuntime op, ManualMotionOperationPhase phase, DateTimeOffset now)
    {
        op.Phase = phase;
        op.PhaseStartedAt = now;
    }

    private static bool IsMotionOperation(ManualMotionOperationKind kind) =>
        kind is ManualMotionOperationKind.Jog or ManualMotionOperationKind.Home or ManualMotionOperationKind.Move;

    private static string EndCodeForStop(ManualMotionOperationKind kind, StopReason reason) => kind switch
    {
        ManualMotionOperationKind.Jog => reason switch
        {
            StopReason.Released => ManualMotionCodes.JogStoppedOnRelease,
            StopReason.CaptureLost => ManualMotionCodes.JogStoppedCaptureLost,
            StopReason.Deactivated => ManualMotionCodes.JogStoppedDeactivated,
            StopReason.HeartbeatExpired => ManualMotionCodes.JogStoppedHeartbeatExpired,
            StopReason.MaximumHold => ManualMotionCodes.JogStoppedMaximumHold,
            StopReason.SessionClosing => ManualMotionCodes.JogStoppedSessionClosing,
            _ => ManualMotionCodes.JogStoppedByOperator,
        },
        ManualMotionOperationKind.Home => reason switch
        {
            StopReason.Timeout => ManualMotionCodes.HomeTimeout,
            StopReason.SessionClosing => ManualMotionCodes.HomeAbortedSessionClosing,
            _ => ManualMotionCodes.HomeAbortedByOperator,
        },
        ManualMotionOperationKind.Move => reason switch
        {
            StopReason.Timeout => ManualMotionCodes.MoveTimeout,
            StopReason.SessionClosing => ManualMotionCodes.MoveAbortedSessionClosing,
            _ => ManualMotionCodes.MoveAbortedByOperator,
        },
        _ => ManualMotionCodes.StopIssued,
    };

    private static ManualMotionOperationKind OperationKindFor(RequestKind kind) => kind switch
    {
        RequestKind.BrakeRelease => ManualMotionOperationKind.BrakeRelease,
        RequestKind.BrakeApply => ManualMotionOperationKind.BrakeApply,
        RequestKind.JogStart => ManualMotionOperationKind.Jog,
        RequestKind.Home => ManualMotionOperationKind.Home,
        RequestKind.DefinePositionZero => ManualMotionOperationKind.DefinePositionZero,
        RequestKind.DefineDualZero => ManualMotionOperationKind.DefineDualZero,
        RequestKind.Move => ManualMotionOperationKind.Move,
        _ => ManualMotionOperationKind.None,
    };

    private static ManualMotionPortCommand BrakeCommand(ManualMotionAxis axis, ManualMotionAxisProfile p, bool release)
    {
        var releaseIsSet = p.BrakeReleaseAction == ManualMotionBrakeReleaseAction.SetOutput;
        var set = release ? releaseIsSet : !releaseIsSet;
        return new ManualMotionPortCommand(
            set ? ManualMotionPortCommandKind.BrakeOutputSet : ManualMotionPortCommandKind.BrakeOutputClear,
            axis,
            0,
            p.BrakeOutput ?? 0);
    }

    // ---------------------------------------------------------------- standalone Stop, open, close

    private void ExecuteStandaloneStop(DateTimeOffset now)
    {
        var axis = _pendingStops.Dequeue();
        var runtime = Runtime(axis);
        var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.Stop, axis, 0, 0));
        if (outcome.Stale)
        {
            AppendLog(now, axis, ManualMotionOperationKind.Stop, ManualMotionCodes.StaleResultRejected);
            return;
        }

        if (outcome.Fault)
        {
            if (_state == ManualMotionSessionState.Faulted)
            {
                AppendLog(now, axis, ManualMotionOperationKind.Stop, ManualMotionCodes.CommunicationFault);
                return;
            }

            FaultSession(now, axis, ManualMotionOperationKind.Stop, ManualMotionCodes.CommunicationFault);
            return;
        }

        runtime.StopState = outcome.Accepted ? ManualAxisStopState.Issued : ManualAxisStopState.Refused;
        var code = outcome.Accepted ? ManualMotionCodes.StopIssued : ManualMotionCodes.StopRefused;
        runtime.LastOutcomeCode = code;
        AppendLog(now, axis, ManualMotionOperationKind.Stop, code);
    }

    private void OpenPort(DateTimeOffset now)
    {
        ManualMotionPortOpenResult result;
        try
        {
            result = _port.Open();
        }
        catch (Exception)
        {
            result = new ManualMotionPortOpenResult(false, ManualMotionCodes.CommunicationFault, 0);
        }

        _openCode = result.Code;
        if (!result.Opened)
        {
            _state = ManualMotionSessionState.OpenRefused;
            AppendLog(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionOpenRefused);
            return;
        }

        _portGeneration = result.PortGeneration;
        _generation++;
        _state = ManualMotionSessionState.Ready;
        _nextObservationCycleDue = now;
        AppendLog(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionOpened);
    }

    private void ClosePort(DateTimeOffset now, bool keepState)
    {
        ManualMotionPortCloseResult result;
        try
        {
            result = _port.Close();
        }
        catch (Exception)
        {
            result = new ManualMotionPortCloseResult(false, false, false, ManualMotionCodes.SessionCloseIncomplete);
        }

        _portClosed = true;
        _generation++;
        _pendingStops.Clear();
        _closeCode = result.BoundariesInvalidated && result.HandleClosed && result.ModulesReleased
            ? ManualMotionCodes.SessionClosed
            : ManualMotionCodes.SessionCloseIncomplete;
        if (!keepState)
        {
            _state = ManualMotionSessionState.Closed;
        }

        AppendLog(now, null, ManualMotionOperationKind.None, _closeCode);
    }

    private bool Interrupted(CallOutcome outcome, DateTimeOffset now, OperationRuntime op)
    {
        if (outcome.Stale)
        {
            FaultSession(now, op.Axis, op.Kind, ManualMotionCodes.StaleResultRejected);
            return true;
        }

        if (outcome.Fault)
        {
            FaultSession(now, op.Axis, op.Kind, ManualMotionCodes.CommunicationFault);
            return true;
        }

        return false;
    }

    private void FaultSession(DateTimeOffset now, ManualMotionAxis? axis, ManualMotionOperationKind operation, string code)
    {
        _observationCycle = null;
        var active = _active;
        _active = null;
        if (active is not null)
        {
            var runtime = Runtime(active.Axis);
            runtime.LastOutcomeCode = code;
            if (runtime.HomeState == ManualMotionHomeState.Homing)
            {
                runtime.HomeState = ManualMotionHomeState.HomeFailed;
            }

            if (active.MotionBegun && _state != ManualMotionSessionState.Faulted && !_portClosed)
            {
                ProtectiveStop(active.Axis, now);
            }
        }

        _state = ManualMotionSessionState.Faulted;
        _generation++;
        AppendLog(now, axis, operation, code);
        AppendLog(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionFaulted);
    }

    private void ProtectiveStop(ManualMotionAxis axis, DateTimeOffset now)
    {
        // One best-effort typed Stop for motion that is already running. It is not a retry of the
        // failed call, it is never repeated and its failure is only recorded.
        _callSequence++;
        var runtime = Runtime(axis);
        try
        {
            var result = _port.Execute(new ManualMotionPortCommand(ManualMotionPortCommandKind.Stop, axis, 0, 0));
            var accepted = result.Accepted && !result.Fault && result.PortGeneration == _portGeneration;
            runtime.StopState = accepted ? ManualAxisStopState.Issued : ManualAxisStopState.Refused;
            AppendLog(now, axis, ManualMotionOperationKind.Stop, accepted ? ManualMotionCodes.StopIssued : ManualMotionCodes.StopRefused);
        }
        catch (Exception)
        {
            runtime.StopState = ManualAxisStopState.Refused;
            AppendLog(now, axis, ManualMotionOperationKind.Stop, ManualMotionCodes.StopRefused);
        }
    }

    private CallOutcome ExecuteCommand(ManualMotionPortCommand command)
    {
        _callSequence++;
        var sequence = _callSequence;
        ManualMotionPortCommandResult result;
        try
        {
            result = _port.Execute(command);
        }
        catch (Exception)
        {
            return new CallOutcome(false, true, false, ManualMotionCodes.CommunicationFault, sequence);
        }

        if (result.PortGeneration != _portGeneration)
        {
            return new CallOutcome(false, false, true, ManualMotionCodes.StaleResultRejected, sequence);
        }

        return new CallOutcome(result.Accepted && !result.Fault, result.Fault, false, result.Code, sequence);
    }

    // ------------------------------------------------------------------------ observation

    /// <summary>
    /// Advances the priority-tiered complete observation cycle by at most one read.
    ///
    /// The profile observation interval is the cadence between the STARTS of complete cycles,
    /// never a delay between the reads of one cycle: once a cycle starts, its remaining reads are
    /// eligible on the following steps, one read per step, so every kind of an allowed axis is
    /// refreshed once per cycle. Status is always first. This method is reached only after the
    /// step has served pending Stops and the active operation, so an urgent lifecycle is never
    /// queued behind a cycle remainder; a context change (an operation starting or ending,
    /// motion beginning after an accepted Begin, or post-Stop supervision beginning) discards
    /// the remainder and the next observation starts a
    /// fresh Status-first cycle. An overrun cycle starts no catch-up burst: the next cycle
    /// becomes due one interval after this cycle started and still advances one read per step.
    /// </summary>
    private void Observe(DateTimeOffset now)
    {
        var interval = _profile?.ObservationIntervalMilliseconds;
        if (interval is null || !ObservationTimingConfigured)
        {
            _observationCycle = null;
            _nextObservationCycleDue = DateTimeOffset.MaxValue;
            return;
        }

        var operation = _active;
        var operationId = operation?.Id ?? 0;
        var motionBegun = operation?.MotionBegun ?? false;
        var stopPriority = operation is { Phase: ManualMotionOperationPhase.AwaitingStopped };

        if (_observationCycle is not null
            && (operationId != _observationCycleOperationId
                || motionBegun != _observationCycleMotionBegun
                || stopPriority != _observationCycleStopPriority))
        {
            _observationCycle = null;
            _nextObservationCycleDue = now;
        }

        var cycle = _observationCycle;
        if (cycle is null)
        {
            if (now < _nextObservationCycleDue)
            {
                return;
            }

            cycle = ObservationPlan();
            if (cycle.Count == 0)
            {
                _nextObservationCycleDue = now + Milliseconds(interval.Value);
                return;
            }

            _observationCycle = cycle;
            _observationCycleIndex = 0;
            _observationCycleStartedAt = now;
            _observationCycleOperationId = operationId;
            _observationCycleMotionBegun = motionBegun;
            _observationCycleStopPriority = stopPriority;
        }

        var read = cycle[_observationCycleIndex];
        ExecuteObservationRead(read, now);

        if (_observationCycle is null)
        {
            // The read faulted the session and the cycle was discarded with it.
            return;
        }

        _observationCycleIndex++;
        if (_observationCycleIndex >= cycle.Count)
        {
            _observationCycle = null;
            _nextObservationCycleDue = _observationCycleStartedAt + Milliseconds(interval.Value);
        }
    }

    private void ExecuteObservationRead(ManualMotionPortRead read, DateTimeOffset now)
    {
        _callSequence++;
        var sequence = _callSequence;
        ManualMotionPortReadResult result;
        try
        {
            result = _port.Read(read);
        }
        catch (Exception)
        {
            FaultSession(now, read.Axis, ManualMotionOperationKind.None, ManualMotionCodes.CommunicationFault);
            return;
        }

        var observation = Runtime(read.Axis).For(read.Kind);
        if (result.PortGeneration != _portGeneration)
        {
            observation.LastRefusalCode = ManualMotionCodes.StaleResultRejected;
            return;
        }

        if (result.Fault)
        {
            FaultSession(now, read.Axis, ManualMotionOperationKind.None, ManualMotionCodes.CommunicationFault);
            return;
        }

        if (!result.Succeeded || !ValueFitsKind(read.Kind, result.Value))
        {
            // Nothing is fabricated: the previous value keeps its old timestamp and goes stale.
            observation.LastRefusalCode = result.Succeeded || result.Code.Length == 0
                ? ManualMotionCodes.ObservationRefused
                : result.Code;
            return;
        }

        observation.Value = result.Value;
        observation.At = now;
        observation.Sequence = sequence;
        observation.Generation = _generation;
        observation.LastRefusalCode = string.Empty;
    }

    /// <summary>
    /// The complete read set of one observation cycle, in deterministic order: Status first, then
    /// TP, RP and the configured brake-output state. While an operation runs the cycle covers its
    /// axis only; while the stopped state after a Stop is awaited, Status evidence has sole
    /// priority and the cycle is the Status read alone. The plan derives from the profile-allowed
    /// axes and their configured outputs — no axis is hard-coded.
    /// </summary>
    private List<ManualMotionPortRead> ObservationPlan()
    {
        var plan = new List<ManualMotionPortRead>(8);
        if (_active is not null)
        {
            var axis = _active.Axis;
            if (_profile?.For(axis) is { } p)
            {
                plan.Add(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, axis, 0));
                if (_active.Phase != ManualMotionOperationPhase.AwaitingStopped)
                {
                    plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
                    plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionRp, axis, 0));
                    if (p.BrakeOutput is int output)
                    {
                        plan.Add(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, axis, output));
                    }
                }
            }

            return plan;
        }

        AddIdleReads(plan, ManualMotionAxis.A);
        AddIdleReads(plan, ManualMotionAxis.B);
        return plan;
    }

    private void AddIdleReads(List<ManualMotionPortRead> plan, ManualMotionAxis axis)
    {
        var p = _profile?.For(axis);
        if (p is null)
        {
            return;
        }

        plan.Add(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, axis, 0));
        plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
        plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionRp, axis, 0));
        if (p.BrakeOutput is int output)
        {
            plan.Add(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, axis, output));
        }
    }

    private static bool ValueFitsKind(ManualMotionReadKind kind, decimal value) => kind switch
    {
        ManualMotionReadKind.StatusWord => value >= 0m && value <= 255m && value == decimal.Truncate(value),
        ManualMotionReadKind.BrakeOutputState => value == 0m || value == 1m,
        _ => true,
    };

    private bool IsFresh(Observation observation, DateTimeOffset now)
    {
        if (observation.Value is null || observation.At is null || observation.Generation != _generation)
        {
            return false;
        }

        var freshness = _profile?.ObservationFreshnessMilliseconds;
        return freshness is not null && now - observation.At.Value <= Milliseconds(freshness.Value);
    }

    private ManualMotionInterpretation InterpretMotion(ManualMotionAxis axis, DateTimeOffset now)
    {
        var p = _profile?.For(axis);
        if (p is null || p.MotionStatusBit is not int bit || p.MotionActiveValue is not int active)
        {
            return ManualMotionInterpretation.Unknown;
        }

        return InterpretBit(Runtime(axis).Status, now, bit, active);
    }

    private ManualMotionInterpretation InterpretHome(ManualMotionAxis axis, DateTimeOffset now)
    {
        var p = _profile?.For(axis);
        if (p is null || p.HomeStatusBit is not int bit || p.HomeActiveValue is not int active)
        {
            return ManualMotionInterpretation.Unknown;
        }

        return InterpretBit(Runtime(axis).Status, now, bit, active);
    }

    private ManualMotionInterpretation InterpretBit(Observation status, DateTimeOffset now, int bit, int active)
    {
        if (!IsFresh(status, now) || !TryGetStatusWord(status, out var word))
        {
            return ManualMotionInterpretation.Unknown;
        }

        return ((word >> bit) & 1) == active ? ManualMotionInterpretation.Active : ManualMotionInterpretation.Inactive;
    }

    private bool? StatusBitObservedAfter(ManualMotionAxis axis, long afterSequence, DateTimeOffset now, int bit, int active)
    {
        var status = Runtime(axis).Status;
        if (status.Sequence <= afterSequence || !IsFresh(status, now) || !TryGetStatusWord(status, out var word))
        {
            return null;
        }

        return ((word >> bit) & 1) == active;
    }

    private static bool TryGetStatusWord(Observation status, out int word)
    {
        word = 0;
        if (status.Value is not decimal value || value < 0m || value > 255m || value != decimal.Truncate(value))
        {
            return false;
        }

        word = (int)value;
        return true;
    }

    private bool MoveReached(OperationRuntime op, DateTimeOffset now)
    {
        if (StatusBitObservedAfter(op.Axis, op.BeginSequence, now, op.MotionBit, op.MotionActive) != false)
        {
            return false;
        }

        var runtime = Runtime(op.Axis);
        return WithinTolerance(runtime.PositionTp, op.BeginSequence, now, op.Target, op.Tolerance)
            && WithinTolerance(runtime.PositionRp, op.BeginSequence, now, op.Target, op.Tolerance);
    }

    private bool WithinTolerance(Observation observation, long afterSequence, DateTimeOffset now, int target, int tolerance)
    {
        if (observation.Sequence <= afterSequence || !IsFresh(observation, now) || observation.Value is not decimal value)
        {
            return false;
        }

        return Math.Abs(value - target) <= tolerance;
    }

    private bool? ZeroReading(Observation observation, long afterSequence, DateTimeOffset now, int tolerance)
    {
        if (observation.Sequence <= afterSequence || !IsFresh(observation, now) || observation.Value is not decimal value)
        {
            return null;
        }

        return Math.Abs(value) <= tolerance;
    }

    // ---------------------------------------------------------------------------- publication

    private void Refuse(DateTimeOffset now, ManualMotionAxis axis, ManualMotionOperationKind kind, string code)
    {
        Runtime(axis).LastOutcomeCode = code;
        AppendLog(now, axis, kind, code);
    }

    private void AppendLog(DateTimeOffset now, ManualMotionAxis? axis, ManualMotionOperationKind operation, string code)
    {
        _logSequence++;
        _log.Add(new ManualMotionLogEntry
        {
            Sequence = _logSequence,
            At = now,
            Axis = axis,
            Operation = operation,
            Code = code,
            Detail = ManualMotionCodes.DetailFor(code),
        });

        if (_log.Count > MaximumLogEntries)
        {
            _log.RemoveRange(0, _log.Count - MaximumLogEntries);
        }
    }

    private ManualMotionSnapshot BuildSnapshot(DateTimeOffset now)
    {
        var axes = new List<ManualMotionAxisSnapshot>(2) { AxisSnapshot(_axisA, now), AxisSnapshot(_axisB, now) };
        var start = Math.Max(0, _log.Count - PublishedLogEntries);
        var log = _log.GetRange(start, _log.Count - start);
        var active = _active;

        return new ManualMotionSnapshot
        {
            Mode = _port.Mode,
            State = _state,
            Generation = _generation,
            ProfileSource = _profile?.Source,
            ProfileCode = _profileCode,
            OpenCode = _openCode,
            CloseCode = _closeCode,
            ActiveOperation = active is null
                ? null
                : new ManualMotionOperationSnapshot
                {
                    Id = active.Id,
                    Kind = active.Kind,
                    Axis = active.Axis,
                    Phase = active.Phase,
                    StartedAt = active.StartedAt,
                    StopRequested = active.StopRequested,
                },
            Axes = axes.AsReadOnly(),
            Log = log.AsReadOnly(),
        };
    }

    private ManualMotionAxisSnapshot AxisSnapshot(AxisRuntime runtime, DateTimeOffset now)
    {
        var p = _profile?.For(runtime.Axis);
        return new ManualMotionAxisSnapshot
        {
            Axis = runtime.Axis,
            AxisLetter = ManualMotionTopology.AxisLetter(runtime.Axis),
            RoleLabel = ManualMotionTopology.RoleLabel(runtime.Axis),
            AllowedByProfile = p is not null,
            BrakeCommandState = runtime.BrakeCommandState,
            BrakeOutput = ObservationSnapshot(runtime.BrakeOutput, now),
            StatusWord = ObservationSnapshot(runtime.Status, now),
            Motion = InterpretMotion(runtime.Axis, now),
            HomeInput = InterpretHome(runtime.Axis, now),
            MotionInterpretationConfigured = p is not null
                && p.MotionStatusBit is not null
                && p.MotionActiveValue is not null,
            HomeInterpretationConfigured = p is not null
                && p.HomeStatusBit is not null
                && p.HomeActiveValue is not null,
            PositionTp = ObservationSnapshot(runtime.PositionTp, now),
            PositionRp = ObservationSnapshot(runtime.PositionRp, now),
            HomeState = runtime.HomeState,
            CoordinateState = runtime.CoordinateState,
            DualZeroState = runtime.DualZeroState,
            StopState = runtime.StopState,
            LastOutcomeCode = runtime.LastOutcomeCode,
            NotConfigured = p is null ? NotAllowedItems : p.NotConfiguredItems(),
        };
    }

    private ManualMotionReadingSnapshot ObservationSnapshot(Observation observation, DateTimeOffset now) => new()
    {
        Value = observation.Value,
        ObservedAt = observation.At,
        Fresh = IsFresh(observation, now),
        LastRefusalCode = observation.LastRefusalCode,
    };

    private ManualMotionAxisProfile AxisProfile(ManualMotionAxis axis) => _profile?.For(axis) ?? EmptyAxisProfile;

    private AxisRuntime Runtime(ManualMotionAxis axis) => axis == ManualMotionAxis.B ? _axisB : _axisA;

    private static TimeSpan Milliseconds(int value) => TimeSpan.FromTicks(value * TimeSpan.TicksPerMillisecond);

    // ------------------------------------------------------------------------------ private types

    private enum RequestKind
    {
        BrakeRelease = 1,
        BrakeApply = 2,
        JogStart = 3,
        JogHeartbeat = 4,
        JogRelease = 5,
        Stop = 6,
        Home = 7,
        DefinePositionZero = 8,
        DefineDualZero = 9,
        Move = 10,
        Close = 11,
    }

    private enum StopReason
    {
        None = 0,
        Released = 1,
        CaptureLost = 2,
        Deactivated = 3,
        HeartbeatExpired = 4,
        MaximumHold = 5,
        Operator = 6,
        SessionClosing = 7,
        Timeout = 8,
    }

    private enum Progress
    {
        Waiting = 0,
        Transitioned = 1,
        CalledPort = 2,
    }

    private readonly record struct CallOutcome(bool Accepted, bool Fault, bool Stale, string Code, long Sequence);

    private sealed class Request
    {
        public Request(RequestKind kind, ManualMotionAxis axis)
        {
            Kind = kind;
            Axis = axis;
        }

        public RequestKind Kind { get; }

        public ManualMotionAxis Axis { get; }

        public ManualMotionJogDirection Direction { get; init; }

        public long PressId { get; init; }

        public ManualMotionJogEndReason Reason { get; init; }

        public int Target { get; init; }
    }

    private sealed class Observation
    {
        public decimal? Value { get; set; }

        public DateTimeOffset? At { get; set; }

        public long Sequence { get; set; }

        public long Generation { get; set; }

        public string LastRefusalCode { get; set; } = string.Empty;
    }

    private sealed class AxisRuntime
    {
        public AxisRuntime(ManualMotionAxis axis)
        {
            Axis = axis;
        }

        public ManualMotionAxis Axis { get; }

        public ManualMotionBrakeCommandState BrakeCommandState { get; set; }

        public ManualMotionHomeState HomeState { get; set; }

        public ManualMotionCoordinateState CoordinateState { get; set; }

        public ManualMotionDualZeroState DualZeroState { get; set; }

        public ManualAxisStopState StopState { get; set; }

        public string LastOutcomeCode { get; set; } = string.Empty;

        public Observation Status { get; } = new();

        public Observation PositionTp { get; } = new();

        public Observation PositionRp { get; } = new();

        public Observation BrakeOutput { get; } = new();

        public Observation For(ManualMotionReadKind kind) => kind switch
        {
            ManualMotionReadKind.StatusWord => Status,
            ManualMotionReadKind.PositionTp => PositionTp,
            ManualMotionReadKind.PositionRp => PositionRp,
            _ => BrakeOutput,
        };
    }

    private sealed class OperationRuntime
    {
        public OperationRuntime(long id, ManualMotionOperationKind kind, ManualMotionAxis axis, DateTimeOffset startedAt)
        {
            Id = id;
            Kind = kind;
            Axis = axis;
            StartedAt = startedAt;
            PhaseStartedAt = startedAt;
        }

        public long Id { get; }

        public ManualMotionOperationKind Kind { get; }

        public ManualMotionAxis Axis { get; }

        public DateTimeOffset StartedAt { get; }

        public ManualMotionOperationPhase Phase { get; set; }

        public DateTimeOffset PhaseStartedAt { get; set; }

        public Queue<ManualMotionPortCommand> PendingCommands { get; } = new();

        public bool StopRequested { get; set; }

        public StopReason StopReason { get; set; }

        public string? EndCode { get; set; }

        public bool BrakeReleasedByOperation { get; set; }

        public bool MotionBegun { get; set; }

        public long BeginSequence { get; set; }

        public DateTimeOffset BeginAt { get; set; }

        public long StopSequence { get; set; }

        public DateTimeOffset StopIssuedAt { get; set; }

        public long ZeroSequence { get; set; }

        public DateTimeOffset ZeroAt { get; set; }

        public bool HomeDetected { get; set; }

        public long PressId { get; set; }

        public DateTimeOffset LastHeartbeatAt { get; set; }

        public int Target { get; set; }

        public TimeSpan BrakeSettle { get; set; }

        public TimeSpan StopTimeout { get; set; }

        public TimeSpan RunTimeout { get; set; }

        public TimeSpan HeartbeatTimeout { get; set; }

        public int Tolerance { get; set; }

        public bool HasZeroVerification { get; set; }

        public int MotionBit { get; set; }

        public int MotionActive { get; set; }

        public int HomeBit { get; set; }

        public int HomeActive { get; set; }
    }
}
