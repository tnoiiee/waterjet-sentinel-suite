using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Wjss.Time;

namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// The Application-owned Manual Motion Commissioning session for the WSB1 axes A/B/C/D (A and C
/// Horizontal, B and D Vertical). It owns the operation lifecycle; the UI only posts typed requests
/// and reads snapshots.
///
/// Threading contract:
///   - every Request* member and <see cref="Snapshot"/> are safe from any thread: a request is only
///     enqueued and is applied by the next <see cref="Step"/>;
///   - <see cref="Step"/> is called by exactly one driver thread; a concurrent call is a programming
///     error and throws;
///   - one step makes at most one port call (the protective Stops on a fault are the only
///     exception), so the controller sees one native operation at a time.
///
/// Behaviour that is fixed by construction:
///   - at most one active operation per axis; different axes may hold active operations during the
///     same interval, and their typed calls are interleaved deterministically through this one
///     session (this is independent per-axis motion, never coordinated trajectory control);
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

    private static readonly ManualMotionAxis[] AllAxes =
    {
        ManualMotionAxis.A,
        ManualMotionAxis.B,
        ManualMotionAxis.C,
        ManualMotionAxis.D,
    };

    private static readonly ReadOnlyCollection<string> NotAllowedItems = Array.AsReadOnly(new[] { "axis is not listed in the profile" });

    private readonly IManualMotionPort _port;
    private readonly IClock _clock;
    private readonly ManualMotionProfile? _profile;
    private readonly string _profileCode;
    private readonly ConcurrentQueue<Request> _requests = new();
    private readonly AxisRuntime _axisA = new(ManualMotionAxis.A);
    private readonly AxisRuntime _axisB = new(ManualMotionAxis.B);
    private readonly AxisRuntime _axisC = new(ManualMotionAxis.C);
    private readonly AxisRuntime _axisD = new(ManualMotionAxis.D);
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
    private int _advanceCursor;
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
        foreach (var axis in AllAxes)
        {
            Runtime(axis).CycleDueAt = now;
        }
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

    /// <summary>Requests the session close: every active motion operation is stopped first, then the port is closed.</summary>
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
                // one protective Stop attempt per active axis that began motion, and issues nothing
                // else. A defect is attributed to no single axis: the fault handler terminates every
                // active operation truthfully.
                FaultSession(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionFaulted);
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

        if (AdvanceActives(now))
        {
            return;
        }

        if (_state != ManualMotionSessionState.Ready && _state != ManualMotionSessionState.Closing)
        {
            return;
        }

        if (!HasAnyActiveOperation && _state == ManualMotionSessionState.Closing)
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

            // Every active motion operation is stopped through its own lifecycle; the per-axis Stops
            // are dispatched one at a time by the ordinary fair advancement, and each axis brake is
            // applied only after that same axis is observed inactive.
            foreach (var op in ActiveOperationsInAxisOrder())
            {
                if (IsMotionOperation(op.Kind))
                {
                    RequestOperationStop(op, StopReason.SessionClosing);
                }
            }
        }

        AppendLog(now, null, ManualMotionOperationKind.None, ManualMotionCodes.SessionClosing);
    }

    private void ApplyJogHeartbeat(Request request, DateTimeOffset now)
    {
        // The press must match this axis's own Jog operation and press ID exactly: a stale or wrong
        // press ID never refreshes any axis, and a heartbeat for one axis never keeps another alive.
        if (ActiveFor(request.Axis) is { Kind: ManualMotionOperationKind.Jog } jog
            && jog.PressId == request.PressId
            && !jog.StopRequested)
        {
            jog.LastHeartbeatAt = now;
        }
    }

    private void ApplyJogRelease(Request request)
    {
        // A release ends only the matching press on the requested axis; it never releases or stops
        // a Jog on any other axis.
        if (ActiveFor(request.Axis) is { Kind: ManualMotionOperationKind.Jog } jog
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

        // Stop is per-axis: an active motion operation of the requested axis is stopped through its
        // own lifecycle, and no operation on any other axis is touched.
        var active = ActiveFor(request.Axis);
        if (active is not null && IsMotionOperation(active.Kind))
        {
            RequestOperationStop(active, StopReason.Operator);
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

        // Same-axis exclusion only: a request is refused while THIS axis already has an active
        // operation. An active operation on another axis never refuses this request.
        return ActiveFor(axis) is not null ? ManualMotionCodes.OperationInProgress : null;
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
                    ?? CheckStationary(axis, p, now);

            case ManualMotionOperationKind.Home:
                return CheckBrakeConfigured(p, requireSettle: true)
                    ?? (p.HomeSpeed is null || p.HomeTimeoutMilliseconds is null ? ManualMotionCodes.HomeParametersNotConfigured : null)
                    ?? (p.StopTimeoutMilliseconds is null ? ManualMotionCodes.StopTimeoutNotConfigured : null)
                    ?? (p.HomeStatusBit is null || p.HomeActiveValue is null ? ManualMotionCodes.HomeFeedbackNotConfigured : null)
                    ?? (p.PositionTolerance is null ? ManualMotionCodes.PositionToleranceNotConfigured : null)
                    ?? CheckStationary(axis, p, now)
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
                    ?? CheckStationary(axis, p, now);

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
                CaptureDualZeroIntent(op, p);
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
                CaptureDualZeroIntent(op, p);
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
        SetActive(axis, op);
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

    /// <summary>
    /// Captures the Home-carried dual-zero definition intent from the profile. The intent is only
    /// honoured by a Home operation; the standalone define-position-zero operation never carries it.
    /// </summary>
    private static void CaptureDualZeroIntent(OperationRuntime op, ManualMotionAxisProfile p)
    {
        op.DualZeroDefinitionRequired = p.DualZeroPermitted;
    }

    // ------------------------------------------------------------------- operation lifecycle

    private bool AdvanceActives(DateTimeOffset now)
    {
        // Deterministic fair advancement in A/B/C/D round-robin order: every active axis advances
        // its own lifecycle in turn, and the cursor rotates past the axis that consumed the step's
        // single port call, so no axis permanently monopolizes the driver and a long operation on
        // one axis never blocks another axis's commands or supervision. Advancement always precedes
        // observation work, so an urgent Stop, timeout or capture-loss transition is never queued
        // behind a read.
        var transitions = 0;
        for (var scanned = 0; scanned < AllAxes.Length; scanned++)
        {
            var axis = AllAxes[(_advanceCursor + scanned) % AllAxes.Length];
            var op = ActiveFor(axis);
            if (op is null)
            {
                continue;
            }

            while (transitions < MaximumTransitionsPerStep)
            {
                transitions++;
                var progress = AdvanceOnce(op, now);
                if (progress == Progress.CalledPort)
                {
                    // The Owner-approved Home-carried reset-encoder composition is one contiguous
                    // native pair on the axis: when the position-zero definition was just accepted
                    // and the dual-zero definition is now due, the advance cursor stays on this axis
                    // so the very next port call of the session is that axis's dual-zero definition.
                    // No other axis's command can therefore occur between DP and DE. The only
                    // commands that still pre-empt the pair are the existing pending-Stop, capture
                    // loss, close and fault-cleanup paths, which are served before active
                    // advancement and are never deferred behind zero establishment.
                    _advanceCursor = op.Phase == ManualMotionOperationPhase.DefiningDualZero
                        ? IndexFor(axis)
                        : (IndexFor(axis) + 1) % AllAxes.Length;
                    return true;
                }

                if (progress == Progress.Waiting)
                {
                    break;
                }
            }
        }

        return false;
    }

    private static int IndexFor(ManualMotionAxis axis) => (int)axis - 1;

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
        ManualMotionOperationPhase.DefiningDualZero => DoDefiningDualZero(op, now),
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

        // A refused, faulted or unclassified begin may still have started motion: the call reached
        // (or may have reached) the controller, so the may-have-begun state is recorded BEFORE the
        // result classification. A faulted begin must therefore receive the protective Stop of the
        // fault cleanup exactly like a refused one, and the supervised Stop path runs before the
        // brake is applied.
        op.MotionBegun = true;
        op.BeginSequence = outcome.Sequence;
        op.BeginAt = now;
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

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

        // The dispatch is recorded as attempted before the call: if this very call faults, fault
        // cleanup must never protective-repeat it (no retry of an already-attempted Stop).
        op.StopCommandAttempted = true;
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

        // The Owner-approved Home-carried composition: a Home operation continues into the second
        // half of the exact Legacy reset-encoder sequence (define position zero, then define dual
        // zero) when the profile permits the dual-zero definition. The standalone define-position-zero
        // operation does not continue: its semantics are unchanged.
        if (op.Kind == ManualMotionOperationKind.Home && op.DualZeroDefinitionRequired)
        {
            SetPhase(op, ManualMotionOperationPhase.DefiningDualZero, now);
            return Progress.CalledPort;
        }

        SetPhase(op, ManualMotionOperationPhase.VerifyingZero, now);
        return Progress.CalledPort;
    }

    /// <summary>
    /// The second half of the Owner-approved Home-carried reset-encoder composition: the typed
    /// define-dual-zero command for the same axis, dispatched as the single next command after the
    /// accepted define-position-zero. A refusal fails the Home closed with a typed code and never
    /// becomes a zero proof; the verification watermark is then taken from THIS command, so no
    /// position reading taken before the dual-zero definition can satisfy the zero verification.
    /// </summary>
    private Progress DoDefiningDualZero(OperationRuntime op, DateTimeOffset now)
    {
        var runtime = Runtime(op.Axis);
        var outcome = ExecuteCommand(new ManualMotionPortCommand(ManualMotionPortCommandKind.DefineDualZero, op.Axis, 0, 0));
        if (Interrupted(outcome, now, op))
        {
            return Progress.CalledPort;
        }

        if (!outcome.Accepted)
        {
            runtime.DualZeroState = ManualMotionDualZeroState.Refused;
            runtime.CoordinateState = ManualMotionCoordinateState.Undefined;
            runtime.HomeState = ManualMotionHomeState.HomeFailed;
            op.EndCode = ManualMotionCodes.HomeDualZeroRefused;
            SetPhase(op, ManualMotionOperationPhase.ApplyingBrake, now);
            return Progress.CalledPort;
        }

        runtime.DualZeroState = ManualMotionDualZeroState.DispatchedUnverified;
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

        // Completing one axis clears only that axis slot; no other axis operation is touched.
        ClearActive(op.Axis, op);
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
        foreach (var axis in AllAxes)
        {
            Runtime(axis).CycleDueAt = now;
        }
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
        // A communication or native fault is session-global because all axes share one controller
        // connection: every active operation is terminally represented (never left pending), and one
        // serialized best-effort typed Stop is attempted for every axis whose motion may already have
        // begun while the boundary is still usable. No stopped state is fabricated and no brake is
        // applied without an observed inactive state.
        foreach (var a in AllAxes)
        {
            ResetObservationCycle(Runtime(a));
        }

        var actives = ActiveOperationsInAxisOrder();
        foreach (var op in actives)
        {
            ClearActive(op.Axis, op);
            var runtime = Runtime(op.Axis);
            runtime.LastOutcomeCode = code;
            if (runtime.HomeState == ManualMotionHomeState.Homing)
            {
                runtime.HomeState = ManualMotionHomeState.HomeFailed;
            }
        }

        foreach (var op in actives)
        {
            // The fault cleanup decides per operation: never began (no Stop), may have begun but
            // never attempted its Stop (exactly one protective Stop), or already attempted its Stop
            // (never protective-repeated). Every attempt stays serialized and its failure is only
            // recorded.
            if (op.MotionBegun && !op.StopCommandAttempted && _state != ManualMotionSessionState.Faulted && !_portClosed)
            {
                ProtectiveStop(op.Axis, now);
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
    /// Advances the deterministic multi-axis observation scheduler by at most one read.
    ///
    /// Each profile-allowed axis owns its own complete Status-first cycle (Status, then TP, RP and
    /// the configured brake-output state), and the profile observation interval is the cadence
    /// between the STARTS of one axis's cycles, never a delay between the reads of one cycle. An
    /// axis awaiting its stopped state after a Stop supervises with a Status-only cycle that has the
    /// highest observation priority and alternates fairly with any other axis in the same state; the
    /// remaining reads go to the least-recently-served eligible axis (ties break on the A/B/C/D
    /// order), so complete cycles interleave across axes, no axis monopolizes consecutive reads, and
    /// a just-rebased axis is served before any older rotation. This method is reached only after
    /// the step has served pending Stops and
    /// the active operations, so an urgent lifecycle is never queued behind a cycle remainder; a
    /// context change (an operation starting or ending on that axis, motion beginning after an
    /// accepted Begin, or post-Stop supervision beginning) rebases ONLY that axis's cycle, and the
    /// other axes keep their observation progress. An overrun cycle starts no catch-up burst: the
    /// next cycle becomes due one interval after this cycle started and still advances one read per
    /// step.
    /// </summary>
    private void Observe(DateTimeOffset now)
    {
        var interval = _profile?.ObservationIntervalMilliseconds;
        if (interval is null || !ObservationTimingConfigured)
        {
            foreach (var a in AllAxes)
            {
                ResetObservationCycle(Runtime(a));
            }

            return;
        }

        foreach (var a in AllAxes)
        {
            var runtime = Runtime(a);
            var op = runtime.Active;
            var operationId = op?.Id ?? 0;
            var motionBegun = op?.MotionBegun ?? false;
            var stopPriority = op is { Phase: ManualMotionOperationPhase.AwaitingStopped };
            if (runtime.CycleReads is not null
                && (operationId != runtime.CycleOperationId
                    || motionBegun != runtime.CycleMotionBegun
                    || stopPriority != runtime.CycleStopPriority))
            {
                // Post-Begin and Stop-supervision rebases apply to this axis only: the discarded
                // remainder belongs to this axis, and its next cycle starts Status-first at once.
                // The axis becomes the most urgent observation candidate, so a post-Begin Status
                // and position refresh is never queued behind an idle axis's rotation.
                runtime.CycleReads = null;
                runtime.CycleDueAt = now;
                runtime.LastServedAt = null;
            }
        }

        var serve = NextObservationAxis(now);
        if (serve is null)
        {
            return;
        }

        var axis = serve.Value;
        var served = Runtime(axis);
        if (served.CycleReads is null)
        {
            var plan = ObservationPlan(axis, served.Active is { Phase: ManualMotionOperationPhase.AwaitingStopped });
            if (plan.Count == 0)
            {
                served.CycleDueAt = now + Milliseconds(interval.Value);
                return;
            }

            served.CycleReads = plan;

            served.CycleIndex = 0;
            served.CycleStartedAt = now;
            served.CycleOperationId = served.Active?.Id ?? 0;
            served.CycleMotionBegun = served.Active?.MotionBegun ?? false;
            served.CycleStopPriority = served.Active is { Phase: ManualMotionOperationPhase.AwaitingStopped };
        }

        var read = served.CycleReads[served.CycleIndex];
        served.LastServedAt = now;
        ExecuteObservationRead(read, now);

        if (served.CycleReads is null)
        {
            // The read faulted the session and every cycle was discarded with it.
            return;
        }

        served.CycleIndex++;
        if (served.CycleIndex >= served.CycleReads.Count)
        {
            served.CycleReads = null;
            served.CycleDueAt = served.CycleStartedAt + Milliseconds(interval.Value);
        }
    }

    private static void ResetObservationCycle(AxisRuntime runtime)
    {
        runtime.CycleReads = null;
        runtime.CycleDueAt = DateTimeOffset.MaxValue;
    }

    private ManualMotionAxis? NextObservationAxis(DateTimeOffset now)
    {
        // Levels: a Status read for an axis awaiting stopped state comes first (fair alternation),
        // then the remaining axes. Within each level the least-recently-served axis wins and ties
        // break on the stable A/B/C/D order, so reads interleave across axes instead of letting one
        // axis monopolize a run of reads, a just-rebased axis is served at once, and idle axes keep
        // rotating beside active axes instead of being starved behind them.
        return PickObservationAxis(now, awaitingStoppedOnly: true)
            ?? PickObservationAxis(now, awaitingStoppedOnly: false);
    }

    private ManualMotionAxis? PickObservationAxis(DateTimeOffset now, bool awaitingStoppedOnly)
    {
        ManualMotionAxis? chosen = null;
        DateTimeOffset? chosenServedAt = null;
        foreach (var axis in AllAxes)
        {
            var runtime = Runtime(axis);
            if (_profile?.For(axis) is null)
            {
                continue;
            }

            var awaitingStopped = runtime.Active is { Phase: ManualMotionOperationPhase.AwaitingStopped };
            if (awaitingStoppedOnly ? !awaitingStopped : awaitingStopped)
            {
                continue;
            }

            // An in-flight cycle stays eligible so each axis keeps its Status-first per-cycle order;
            // a due cycle is eligible at its cadence. Eligibility never jumps a cycle ahead of one.
            if (runtime.CycleReads is null && now < runtime.CycleDueAt)
            {
                continue;
            }

            // null means "never served since the last rebase": the most urgent candidate.
            var servedAt = runtime.LastServedAt;
            if (chosen is null
                || (servedAt is null && chosenServedAt is not null)
                || (servedAt is not null && chosenServedAt is not null && servedAt < chosenServedAt))
            {
                chosen = axis;
                chosenServedAt = servedAt;
            }
        }

        return chosen;
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
    /// The complete read set of one observation cycle of one axis, in deterministic order: Status
    /// first, then TP, RP and the configured brake-output state. While that axis awaits its stopped
    /// state after a Stop, Status evidence has sole priority for that axis and its cycle is the
    /// Status read alone. The plan derives from the profile-allowed axis and its configured outputs
    /// — no axis is hard-coded.
    /// </summary>
    private List<ManualMotionPortRead> ObservationPlan(ManualMotionAxis axis, bool stopPriority)
    {
        var plan = new List<ManualMotionPortRead>(4);
        var p = _profile?.For(axis);
        if (p is null)
        {
            return plan;
        }

        plan.Add(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, axis, 0));
        if (!stopPriority)
        {
            plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
            plan.Add(new ManualMotionPortRead(ManualMotionReadKind.PositionRp, axis, 0));
            if (p.BrakeOutput is int output)
            {
                plan.Add(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, axis, output));
            }
        }

        return plan;
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
        var axes = new List<ManualMotionAxisSnapshot>(4)
        {
            AxisSnapshot(_axisA, now),
            AxisSnapshot(_axisB, now),
            AxisSnapshot(_axisC, now),
            AxisSnapshot(_axisD, now),
        };
        var start = Math.Max(0, _log.Count - PublishedLogEntries);
        var log = _log.GetRange(start, _log.Count - start);
        var actives = ActiveOperationsInAxisOrder();
        var activeSnapshots = new List<ManualMotionOperationSnapshot>(actives.Count);
        foreach (var active in actives)
        {
            activeSnapshots.Add(new ManualMotionOperationSnapshot
            {
                Id = active.Id,
                Kind = active.Kind,
                Axis = active.Axis,
                Phase = active.Phase,
                StartedAt = active.StartedAt,
                StopRequested = active.StopRequested,
            });
        }

        return new ManualMotionSnapshot
        {
            Mode = _port.Mode,
            State = _state,
            Generation = _generation,
            ProfileSource = _profile?.Source,
            ProfileCode = _profileCode,
            OpenCode = _openCode,
            CloseCode = _closeCode,
            ActiveOperations = activeSnapshots.AsReadOnly(),
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
            CabinetId = ManualMotionTopology.CabinetId(runtime.Axis),
            WaterJetId = ManualMotionTopology.WaterJetId(runtime.Axis),
            InstalledPosition = ManualMotionTopology.InstalledPosition(runtime.Axis),
            TargetCoverage = ManualMotionTopology.TargetCoverage(runtime.Axis),
            BrakeOutputNumber = ManualMotionTopology.ConfirmedBrakeOutput(runtime.Axis),
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

    private AxisRuntime Runtime(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => _axisA,
        ManualMotionAxis.B => _axisB,
        ManualMotionAxis.C => _axisC,
        ManualMotionAxis.D => _axisD,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "axis is outside the closed A/B/C/D domain"),
    };

    // ------------------------------------------------------------------ per-axis operation slots

    /// <summary>The active operation of one axis, or null when that axis is idle. Fail-closed lookup.</summary>
    private OperationRuntime? ActiveFor(ManualMotionAxis axis) => Runtime(axis).Active;

    /// <summary>Binds one new operation to its axis slot. The slot must be empty (same-axis exclusion).</summary>
    private void SetActive(ManualMotionAxis axis, OperationRuntime operation)
    {
        var runtime = Runtime(axis);
        if (runtime.Active is not null)
        {
            throw new InvalidOperationException("one operation per axis: the axis slot was still occupied");
        }

        runtime.Active = operation;
    }

    /// <summary>Clears the axis slot only when it still holds the expected operation.</summary>
    private void ClearActive(ManualMotionAxis axis, OperationRuntime expectedOperation)
    {
        var runtime = Runtime(axis);
        if (ReferenceEquals(runtime.Active, expectedOperation))
        {
            runtime.Active = null;
        }
    }

    /// <summary>Every active operation, in stable A/B/C/D order.</summary>
    private List<OperationRuntime> ActiveOperationsInAxisOrder()
    {
        var actives = new List<OperationRuntime>(4);
        foreach (var axis in AllAxes)
        {
            if (Runtime(axis).Active is { } op)
            {
                actives.Add(op);
            }
        }

        return actives;
    }

    private bool HasAnyActiveOperation
    {
        get
        {
            foreach (var axis in AllAxes)
            {
                if (Runtime(axis).Active is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

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

        /// <summary>The one operation slot of this axis; null when the axis is idle.</summary>
        public OperationRuntime? Active { get; set; }

        /// <summary>The in-flight observation cycle reads of this axis; null when no cycle is running.</summary>
        public List<ManualMotionPortRead>? CycleReads { get; set; }

        /// <summary>The read index inside the in-flight cycle of this axis.</summary>
        public int CycleIndex { get; set; }

        /// <summary>When the in-flight cycle of this axis started.</summary>
        public DateTimeOffset CycleStartedAt { get; set; }

        /// <summary>When the next cycle of this axis becomes due.</summary>
        public DateTimeOffset CycleDueAt { get; set; }

        /// <summary>The operation ID context of the in-flight cycle of this axis.</summary>
        public long CycleOperationId { get; set; }

        /// <summary>The post-Begin context of the in-flight cycle of this axis.</summary>
        public bool CycleMotionBegun { get; set; }

        /// <summary>The post-Stop supervision context of the in-flight cycle of this axis.</summary>
        public bool CycleStopPriority { get; set; }

        /// <summary>When this axis was last served an observation read; null when never (most urgent).</summary>
        public DateTimeOffset? LastServedAt { get; set; }

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

        /// <summary>True once this operation attempted its ordinary Stop dispatch (never repeated).</summary>
        public bool StopCommandAttempted { get; set; }

        public string? EndCode { get; set; }

        public bool BrakeReleasedByOperation { get; set; }

        public bool MotionBegun { get; set; }

        public long BeginSequence { get; set; }

        public DateTimeOffset BeginAt { get; set; }

        public long StopSequence { get; set; }

        public DateTimeOffset StopIssuedAt { get; set; }

        /// <summary>
        /// The dispatch sequence of the LAST zero-establishing command of this operation: the
        /// position-zero definition for a standalone define, and the dual-zero definition for the
        /// Home-carried composition. Every zero verification reading must be strictly newer.
        /// </summary>
        public long ZeroSequence { get; set; }

        public DateTimeOffset ZeroAt { get; set; }

        /// <summary>
        /// True when this operation carries the Owner-approved Home-carried dual-zero definition,
        /// captured from the profile permission at start. False for every standalone define.
        /// </summary>
        public bool DualZeroDefinitionRequired { get; set; }

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
