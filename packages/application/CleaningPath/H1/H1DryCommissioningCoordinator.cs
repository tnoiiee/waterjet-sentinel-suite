using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;

namespace Wjss.Runtime.Core.CleaningPath.H1;

/// <summary>
/// The H1 dry Cleaning Path commissioning run coordinator for WSB1.
///
/// It composes the existing typed boundaries and never duplicates them: Phase 3A resolution and
/// path data, the canonical Motion topology catalog, the fixed Centre contract and the typed Manual
/// Motion session. Commissioning is two explicitly separated actions. Commission/Home
/// (StartCommission) invokes the existing typed Home lifecycle on both resolved axes
/// (independent per axis, each carrying the typed position-zero definition and its zero
/// verification), proves the coordinate establishment, and terminates Commissioned without
/// running any path point. Run Path (StartPath) never dispatches Home: it requires the
/// session-scoped coordinate readiness (both resolved axes Homed with a verified zero), refuses
/// with a typed reason directing the Operator to Commission/Home when that readiness is absent,
/// and only then runs P1 through P6 through the existing typed path execution coordinator and
/// the dry Centre return on the fixed Centre contract. An active Home input never proves
/// readiness and is never treated as a completed Home. Each axis establishes its zero with the
/// Owner-approved Home-carried reset-encoder composition: the typed position-zero definition, then
/// the typed dual-zero definition, then fresh position reads strictly newer than the dual-zero
/// definition, both within the configured tolerance. Dispatch alone never proves a Home.
///
/// Failure behavior is fail-closed and evidence-preserving: the first genuine primary cause is
/// retained, no later point may start, every remaining active axis is stopped exactly once and
/// supervised to inactive with brake applied afterwards, cleanup evidence is recorded separately,
/// and no dry Safe Return completion is ever fabricated. A Home request that the session refuses
/// (for example an already-active Home input) is detected by per-request log-sequence
/// correlation, so the run always reaches a typed terminal failure and never remains in Homing
/// indefinitely. Owner Abort and Close preserve typed causes and block any further run until an
/// explicit new start action.
///
/// The surface of this type exposes NO Pump command, NO physical Isolation Valve command and NO
/// Queue / AutoSequence path of any kind: dry motion only.
/// </summary>
public sealed class H1DryCommissioningCoordinator
{
    private const string SupportedCabinetId = "WSB1";

    /// <summary>The explicitly separated commissioning actions this coordinator can run.</summary>
    private enum RunKind
    {
        /// <summary>No action has started.</summary>
        None = 0,

        /// <summary>Commission/Home: the typed Home lifecycle and coordinate establishment only.</summary>
        Commission = 1,

        /// <summary>Run Path: P1 through P6 and the dry Centre return on established coordinates.</summary>
        Path = 2,
    }

    private readonly ManualMotionSession _session;
    private readonly ManualMotionCleaningPathBoundary _boundary;
    private readonly IClock _clock;

    private CleaningPathExecutionCoordinator _path;
    private bool _started;
    private long _runCounter;
    private RunKind _runKind;

    private H1DryCommissioningPhase _phase = H1DryCommissioningPhase.NotStarted;
    private string _runId = string.Empty;
    private H1DryCommissioningRequest? _request;
    private H1DryCommissioningIdentity _identity = EmptyIdentity();
    private H1CabinetAdmission _cabinetAdmission = H1CabinetAdmission.NotEvaluated;
    private string? _primaryFailure;
    private H1DrySafeReturnState _drySafeReturn = H1DrySafeReturnState.NotPublished;

    private readonly bool[] _homeSeen = new bool[2];
    private readonly bool[] _homeSettled = new bool[2];
    private readonly bool[] _homeFailed = new bool[2];
    private readonly string[] _homeOutcomeCode = new string[2] { string.Empty, string.Empty };

    /// <summary>
    /// The session log sequence watermark captured when Commission/Home dispatches its Home
    /// requests. Home-kind entries with a greater sequence are the per-request outcome evidence of
    /// this run, independent of the axis outcome-code value.
    /// </summary>
    private long _homeLogWatermark;

    private readonly bool[] _axisStopRequested = new bool[2];
    private readonly H1AxisCentreRecord?[] _centreRecords = new H1AxisCentreRecord?[2];
    private readonly CleaningPathMotionOutcome[] _centreOutcome = new CleaningPathMotionOutcome[2];
    private readonly string[] _centreOutcomeSource = new string[2] { string.Empty, string.Empty };
    private H1CentreReturnRecord? _centreReturn;
    private bool _closingRequested;
    private bool _abortingRequested;

    /// <summary>A terminal cause waiting only for the stop/close supervision to settle.</summary>
    private (H1DryCommissioningPhase Phase, string Code)? _pendingTerminal;

    public H1DryCommissioningCoordinator(
        ManualMotionSession session,
        ManualMotionCleaningPathBoundary boundary,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(clock);
        _session = session;
        _boundary = boundary;
        _clock = clock;
        _path = new CleaningPathExecutionCoordinator(_boundary, _clock);
    }

    /// <summary>The immutable run snapshot republished after every step (null before Start).</summary>
    public H1DryCommissioningSnapshot? Current { get; private set; }

    /// <summary>The final typed dry-commissioning result, or null while the run is non-terminal.</summary>
    public H1DryCommissioningResult? Result { get; private set; }

    /// <summary>True while the run has started and not reached a terminal state.</summary>
    public bool Active => _started && Result is null;

    /// <summary>
    /// Starts the Commission/Home action: the existing typed Home lifecycle on both resolved axes
    /// and the coordinate establishment. Every precondition is proven fail-closed before any motion
    /// request: guards, accepted Phase 3A snapshot and resolution, selected Sensor match, WSB1-only
    /// admission, canonical route match against the Motion topology catalog, a ready and
    /// not-faulted session with no active axis operation, and current/fresh Status, TP, RP and
    /// Brake-output observations on both resolved axes. The first unmet precondition refuses the
    /// start with a typed code and the machine is left untouched. The run terminates Commissioned
    /// when both axes are Homed with a verified zero; no path point ever runs in this action.
    /// </summary>
    public H1DryCommissioningStartOutcome StartCommission(H1DryCommissioningRequest request)
    {
        var admission = AdmitStart(request);
        if (!admission.Accepted)
        {
            return new H1DryCommissioningStartOutcome(false, admission.RefusalCode!);
        }

        var resolved = admission.Resolution!;
        BeginRun(request, resolved, RunKind.Commission);
        _phase = H1DryCommissioningPhase.Homing;

        var horizontal = AxisEnumFor(resolved.HorizontalAxisId);
        var vertical = AxisEnumFor(resolved.VerticalAxisId);
        _homeLogWatermark = CurrentLogSequence();

        // Deterministic Horizontal-first request order; the two Home lifecycles remain independent
        // per axis with no synchronized Begin and no combined-axis command.
        _session.RequestHome(horizontal);
        _session.RequestHome(vertical);

        Publish();
        return new H1DryCommissioningStartOutcome(true, H1DryCommissioningCodes.KindCommissionStarted);
    }

    /// <summary>
    /// Starts the Run Path action: P1 through P6 and the dry Centre return through the existing
    /// typed path execution coordinator. This action NEVER dispatches Home. It requires the
    /// session-scoped coordinate readiness established by an explicit Commission/Home run: both
    /// resolved axes must be Homed with a verified zero in this session. When that readiness is
    /// absent the start is refused with the typed coordinate-readiness reason directing the
    /// Operator to Commission/Home, and no Home, P1 or other motion request is made. The Home
    /// switch state alone never proves readiness. All other start preconditions are identical to
    /// Commission/Home and are proven fail-closed first.
    /// </summary>
    public H1DryCommissioningStartOutcome StartPath(H1DryCommissioningRequest request)
    {
        var admission = AdmitStart(request);
        if (!admission.Accepted)
        {
            return new H1DryCommissioningStartOutcome(false, admission.RefusalCode!);
        }

        var resolved = admission.Resolution!;
        var horizontal = AxisSnapshot(AxisEnumFor(resolved.HorizontalAxisId));
        var vertical = AxisSnapshot(AxisEnumFor(resolved.VerticalAxisId));
        if (horizontal?.HomeState != ManualMotionHomeState.Homed
            || horizontal?.CoordinateState != ManualMotionCoordinateState.ZeroVerified
            || vertical?.HomeState != ManualMotionHomeState.Homed
            || vertical?.CoordinateState != ManualMotionCoordinateState.ZeroVerified)
        {
            return new H1DryCommissioningStartOutcome(
                false,
                H1DryCommissioningCodes.StartRefusedCoordinateReadinessAbsent);
        }

        BeginRun(request, resolved, RunKind.Path);
        _phase = H1DryCommissioningPhase.CoordinateEstablishment;

        Publish();
        return new H1DryCommissioningStartOutcome(true, H1DryCommissioningCodes.KindPathRunStarted);
    }

    /// <summary>
    /// Proves every common start precondition without mutating any state. The first unmet
    /// precondition refuses with its typed code; only an accepted admission may begin a run.
    /// </summary>
    private StartAdmission AdmitStart(H1DryCommissioningRequest request)
    {
        if (_started && Result is null)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedRunActive);
        }

        if (request is null
            || request.PathStore is null
            || request.Resolution is null
            || string.IsNullOrWhiteSpace(request.SelectedSensorId)
            || request.Guards is null)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedRequestIncomplete);
        }

        var guardRefusal = CheckGuards(request.Guards);
        if (guardRefusal is not null)
        {
            return StartAdmission.Refuse(guardRefusal);
        }

        if (request.PathStore.SourceKind != CleaningPathSourceKind.OwnerLocalFile
            || !string.Equals(request.PathStore.Schema, CleaningPathStoreParser.SchemaIdentifier, StringComparison.Ordinal))
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedSnapshotNotAccepted);
        }

        if (!request.Resolution.Accepted || request.Resolution.Resolution is null)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedResolutionNotAccepted);
        }

        var resolved = request.Resolution.Resolution;
        if (!string.Equals(resolved.SensorId, request.SelectedSensorId, StringComparison.Ordinal)
            || !string.Equals(resolved.Path.SensorId, request.SelectedSensorId, StringComparison.Ordinal)
            || request.PathStore.PathFor(request.SelectedSensorId) is null)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedSensorMismatch);
        }

        // H1 supports only WSB1: a selected Effective WJ outside WSB1 is refused fail-closed.
        if (!string.Equals(resolved.CabinetId, SupportedCabinetId, StringComparison.Ordinal))
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedUnsupportedWsb);
        }

        // The canonical route must match the selected Effective WJ exactly: the Motion topology
        // catalog is the sole authority and this check consumes it, never restates it.
        var binding = MotionTopologyCatalog.FindWaterJetBinding(resolved.EffectiveWaterJetId);
        if (binding is null
            || !string.Equals(binding.CabinetId, resolved.CabinetId, StringComparison.Ordinal)
            || !string.Equals(binding.ControllerId, resolved.ControllerId, StringComparison.Ordinal)
            || !string.Equals(binding.HorizontalAxisId, resolved.HorizontalAxisId, StringComparison.Ordinal)
            || !string.Equals(binding.VerticalAxisId, resolved.VerticalAxisId, StringComparison.Ordinal))
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedCanonicalRouteMismatch);
        }

        if (_boundary.SessionFaulted)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedSessionFaulted);
        }

        if (!_boundary.SessionReady)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedSessionNotReady);
        }

        if (_session.Snapshot.ActiveOperations.Count > 0
            || _boundary.Frame(CleaningPathExecutionRole.Horizontal).MoveActive
            || _boundary.Frame(CleaningPathExecutionRole.Vertical).MoveActive)
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedAxisOperationActive);
        }

        if (!ObservationsCurrent(AxisEnumFor(resolved.HorizontalAxisId))
            || !ObservationsCurrent(AxisEnumFor(resolved.VerticalAxisId)))
        {
            return StartAdmission.Refuse(H1DryCommissioningCodes.StartRefusedObservationNotFresh);
        }

        return StartAdmission.Accept(resolved);
    }

    /// <summary>Every precondition is proven: only now does the machine take the run.</summary>
    private void BeginRun(H1DryCommissioningRequest request, ResolvedCleaningPath resolved, RunKind kind)
    {
        ResetRunState();
        Result = null;
        _request = request;
        _identity = BuildIdentity(resolved);
        _cabinetAdmission = H1CabinetAdmission.Wsb1Admitted;
        _runId = "H1R" + Interlocked.Increment(ref _runCounter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _path = new CleaningPathExecutionCoordinator(_boundary, _clock);
        _started = true;
        _runKind = kind;
    }

    /// <summary>The fail-closed admission result of one start attempt; it mutates nothing.</summary>
    private sealed record StartAdmission(bool Accepted, string? RefusalCode, ResolvedCleaningPath? Resolution)
    {
        public static StartAdmission Refuse(string code) => new(false, code, null);

        public static StartAdmission Accept(ResolvedCleaningPath resolution) => new(true, null, resolution);
    }

    /// <summary>
    /// Advances the run by one observation/decision step. The driver keeps the serialized port
    /// cadence separate by calling the boundary's step; this method only advances run state.
    /// </summary>
    public void Step()
    {
        if (!_started || Result is not null)
        {
            return;
        }

        if (_pendingTerminal is { } pending)
        {
            if (BothAxesSettled() && (!_closingRequested || _session.Snapshot.IsTerminal))
            {
                Finish(pending.Phase, pending.Code);
            }

            Publish();
            return;
        }

        switch (_phase)
        {
            case H1DryCommissioningPhase.Homing:
                StepHoming();
                break;
            case H1DryCommissioningPhase.CoordinateEstablishment:
                StepCoordinateEstablishment();
                break;
            case H1DryCommissioningPhase.PathExecution:
                StepPathExecution();
                break;
            case H1DryCommissioningPhase.CentreReturn:
                StepCentreReturn();
                break;
            default:
                break;
        }

        Publish();
    }

    /// <summary>
    /// Owner Abort: the typed Owner-abort cause is preserved, no later point may start, every
    /// remaining active axis is stopped exactly once and supervised, cleanup is recorded separately
    /// and another run stays blocked until an explicit restart.
    /// </summary>
    public void Abort()
    {
        if (!_started || Result is not null || _abortingRequested || _closingRequested)
        {
            return;
        }

        _abortingRequested = true;
        if (_phase == H1DryCommissioningPhase.PathExecution && _path.Active)
        {
            // The typed path abort owns the exactly-once Stop of every active axis and its
            // supervised cleanup; its terminal result carries the typed abort cause.
            _path.Abort();
        }
        else
        {
            RequestRemainingStops();
            _pendingTerminal = (H1DryCommissioningPhase.Aborted, H1DryCommissioningCodes.RunAborted);
        }

        Publish();
    }

    /// <summary>
    /// Close: the typed close cause is preserved, no later point may start, remaining active axes
    /// are stopped exactly once and supervised, brake is applied after inactive and the session
    /// resources are closed through the existing boundary.
    /// </summary>
    public void RequestClose()
    {
        if (!_started || Result is not null || _closingRequested)
        {
            return;
        }

        _closingRequested = true;
        if (_phase == H1DryCommissioningPhase.PathExecution && _path.Active)
        {
            _path.RequestClose();
        }
        else
        {
            RequestRemainingStops();
            _boundary.RequestClose();
            _pendingTerminal = (H1DryCommissioningPhase.Closed, H1DryCommissioningCodes.RunClosed);
        }

        Publish();
    }

    // ------------------------------------------------------------------------------- phases

    private void StepHoming()
    {
        if (_session.Snapshot.State == ManualMotionSessionState.Faulted)
        {
            RequestRemainingStops();
            _primaryFailure ??= ManualMotionCodes.SessionFaulted;
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedHome);
            return;
        }

        var horizontal = AxisEnumFor(_identity.HorizontalAxisId);
        var vertical = AxisEnumFor(_identity.VerticalAxisId);
        ObserveHome(0, horizontal);
        ObserveHome(1, vertical);

        if (_homeFailed[0] || _homeFailed[1])
        {
            RequestRemainingStops();
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedHome);
            return;
        }

        if (_homeSettled[0] && _homeSettled[1])
        {
            _phase = H1DryCommissioningPhase.CoordinateEstablishment;
        }
    }

    private void ObserveHome(int index, ManualMotionAxis axis)
    {
        if (_homeSettled[index] || _homeFailed[index])
        {
            return;
        }

        var snapshot = AxisSnapshot(axis);
        if (snapshot is null)
        {
            return;
        }

        var active = false;
        foreach (var operation in _session.Snapshot.ActiveOperations)
        {
            if (operation.Axis == axis && operation.Kind == ManualMotionOperationKind.Home)
            {
                active = true;
                break;
            }
        }

        if (active)
        {
            _homeSeen[index] = true;
            return;
        }

        if (!_homeSeen[index])
        {
            // Per-request outcome correlation by the session log sequence: the session records
            // exactly one Home-kind entry for every applied Home request (OPERATION_STARTED on
            // acceptance, the typed refusal code otherwise), so the first Home-kind entry of this
            // axis after the start watermark is the outcome of THIS run's request even when the
            // axis outcome code is unchanged from before the request. The published log window is
            // far larger than the entries one session step can produce, and this coordinator
            // re-scans every step, so an applied request cannot be missed.
            var entry = FirstHomeEntrySince(axis);
            if (entry is null)
            {
                // The Home request has not been applied yet; nothing has happened and nothing is fabricated.
                return;
            }

            _homeSeen[index] = true;
            _homeOutcomeCode[index] = entry.Code;
            if (!string.Equals(entry.Code, ManualMotionCodes.OperationStarted, StringComparison.Ordinal))
            {
                // The applied request was refused before any native Home dispatch (for example an
                // already-active Home input): fail closed with the exact typed refusal code. The
                // run can never remain in Homing after an applied refusal.
                _homeFailed[index] = true;
                _primaryFailure ??= entry.Code;
            }

            return;
        }

        _homeOutcomeCode[index] = snapshot.LastOutcomeCode;
        if (snapshot.HomeState == ManualMotionHomeState.Homed
            && snapshot.CoordinateState == ManualMotionCoordinateState.ZeroVerified
            && string.Equals(snapshot.LastOutcomeCode, ManualMotionCodes.HomeCompletedZeroVerified, StringComparison.Ordinal))
        {
            _homeSettled[index] = true;
            return;
        }

        // Fail closed: any other completion (timeout, refusal, unverified zero, failed Home) blocks
        // continuation and becomes the first primary cause when none was recorded yet.
        _homeFailed[index] = true;
        _primaryFailure ??= snapshot.LastOutcomeCode;
    }

    /// <summary>The first Home-kind log entry of one axis after the Commission/Home watermark.</summary>
    private ManualMotionLogEntry? FirstHomeEntrySince(ManualMotionAxis axis)
    {
        foreach (var entry in _session.Snapshot.Log)
        {
            if (entry.Sequence > _homeLogWatermark
                && entry.Axis == axis
                && entry.Operation == ManualMotionOperationKind.Home)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>The newest session log sequence, used as the Commission/Home watermark.</summary>
    private long CurrentLogSequence()
    {
        var log = _session.Snapshot.Log;
        return log.Count == 0 ? 0 : log[^1].Sequence;
    }

    private void StepCoordinateEstablishment()
    {
        if (_abortingRequested || _closingRequested)
        {
            return;
        }

        var horizontal = AxisSnapshot(AxisEnumFor(_identity.HorizontalAxisId));
        var vertical = AxisSnapshot(AxisEnumFor(_identity.VerticalAxisId));
        if (horizontal is null || vertical is null)
        {
            _primaryFailure ??= H1DryCommissioningCodes.CoordinateEstablishmentRefused;
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedCoordinateEstablishment);
            return;
        }

        // The exact accepted contract: the typed Home lifecycle already applied the Owner-approved
        // Home-carried reset-encoder composition (the typed position-zero definition, then the typed
        // dual-zero definition) and verified zero by position reads whose evidence is strictly newer
        // than the dual-zero definition, so the required coordinate state is proven here. A Home
        // that ended before the dual-zero definition, or whose dual-zero definition was refused, or
        // whose reads did not verify, is not Homed and is refused by the check below.
        if (horizontal.HomeState != ManualMotionHomeState.Homed
            || vertical.HomeState != ManualMotionHomeState.Homed
            || horizontal.CoordinateState != ManualMotionCoordinateState.ZeroVerified
            || vertical.CoordinateState != ManualMotionCoordinateState.ZeroVerified)
        {
            _primaryFailure ??= H1DryCommissioningCodes.CoordinateEstablishmentRefused;
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedCoordinateEstablishment);
            return;
        }

        if (_runKind == RunKind.Commission)
        {
            // The Commission/Home action ends here: both axes are Homed with a verified zero in
            // this session, the coordinate readiness is established for a later explicit Run Path,
            // and no path point may start in this action.
            Finish(H1DryCommissioningPhase.Commissioned, H1DryCommissioningCodes.CommissionComplete);
            return;
        }

        _phase = H1DryCommissioningPhase.PathExecution;
        var outcome = _path.Start(_request!.PathStore, _request.Resolution);
        if (!outcome.Started)
        {
            _primaryFailure ??= outcome.Code;
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedPathExecution);
        }
    }

    private void StepPathExecution()
    {
        _path.Step();
        var result = _path.Result;
        if (result is null)
        {
            return;
        }

        if (result.Status == CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn)
        {
            _phase = H1DryCommissioningPhase.CentreReturn;
            var handoff = result.Handoff!;
            _boundary.RequestMove(CleaningPathExecutionRole.Horizontal, handoff.HorizontalCentreNative);
            _boundary.RequestMove(CleaningPathExecutionRole.Vertical, handoff.VerticalCentreNative);
            return;
        }

        if (result.Status == CleaningPathExecutionStatus.PathFailed)
        {
            if (string.Equals(result.Code, CleaningPathExecutionCodes.PathAborted, StringComparison.Ordinal))
            {
                // The typed path abort already stopped every active axis exactly once and recorded
                // its cleanup; the typed Owner-abort cause is preserved and no failure is fabricated.
                _primaryFailure = null;
                Finish(H1DryCommissioningPhase.Aborted, H1DryCommissioningCodes.RunAborted);
                return;
            }

            if (string.Equals(result.Code, CleaningPathExecutionCodes.PathClosed, StringComparison.Ordinal)
                || string.Equals(result.Code, CleaningPathExecutionCodes.PathSessionClosing, StringComparison.Ordinal))
            {
                _primaryFailure = null;
                Finish(H1DryCommissioningPhase.Closed, H1DryCommissioningCodes.RunClosed);
                return;
            }

            _primaryFailure ??= _path.Current?.FailureCode ?? result.Code;
            Finish(H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedPathExecution);
            return;
        }

        // Any non-terminal shape here is a contradiction and fails closed.
        _primaryFailure ??= result.Code;
        Finish(H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedPathExecution);
    }

    private void StepCentreReturn()
    {
        foreach (var motionEvent in _boundary.DrainEvents())
        {
            var index = motionEvent.Role == CleaningPathExecutionRole.Horizontal ? 0 : 1;
            if (motionEvent.Outcome is CleaningPathMotionOutcome.OperationStarted
                or CleaningPathMotionOutcome.MoveCompletedPositionVerified
                or CleaningPathMotionOutcome.MoveTimeout
                or CleaningPathMotionOutcome.MoveAbortedByOperator
                or CleaningPathMotionOutcome.MoveAbortedSessionClosing
                or CleaningPathMotionOutcome.MotionCommandRefused
                or CleaningPathMotionOutcome.AbortedBeforeMotion
                or CleaningPathMotionOutcome.BrakeApplyRefused
                or CleaningPathMotionOutcome.CommunicationFault
                or CleaningPathMotionOutcome.StaleResultRejected
                or CleaningPathMotionOutcome.SessionFaulted)
            {
                _centreOutcome[index] = motionEvent.Outcome;
                _centreOutcomeSource[index] = motionEvent.SourceCode;
            }
        }

        if (_abortingRequested || _closingRequested)
        {
            if (BothAxesSettled() && (!_closingRequested || _session.Snapshot.IsTerminal))
            {
                Finish(
                    _closingRequested ? H1DryCommissioningPhase.Closed : H1DryCommissioningPhase.Aborted,
                    _closingRequested ? H1DryCommissioningCodes.RunClosed : H1DryCommissioningCodes.RunAborted);
            }

            return;
        }

        var handoff = _path.Result!.Handoff!;
        var anyFailed = false;
        for (var index = 0; index < 2; index++)
        {
            if (_centreRecords[index] is not null)
            {
                anyFailed |= _centreRecords[index]!.Verified == false;
                continue;
            }

            var role = index == 0 ? CleaningPathExecutionRole.Horizontal : CleaningPathExecutionRole.Vertical;
            var frame = _boundary.Frame(role);
            var target = index == 0 ? handoff.HorizontalCentreNative : handoff.VerticalCentreNative;
            var axisId = index == 0 ? _identity.HorizontalAxisId : _identity.VerticalAxisId;

            if (_centreOutcome[index] is CleaningPathMotionOutcome.MoveTimeout
                or CleaningPathMotionOutcome.MoveAbortedByOperator
                or CleaningPathMotionOutcome.MoveAbortedSessionClosing
                or CleaningPathMotionOutcome.MotionCommandRefused
                or CleaningPathMotionOutcome.AbortedBeforeMotion
                or CleaningPathMotionOutcome.BrakeApplyRefused
                or CleaningPathMotionOutcome.CommunicationFault
                or CleaningPathMotionOutcome.StaleResultRejected
                or CleaningPathMotionOutcome.SessionFaulted)
            {
                _centreRecords[index] = new H1AxisCentreRecord(
                    axisId,
                    target,
                    _centreOutcome[index],
                    _centreOutcomeSource[index],
                    Verified: false,
                    frame.BrakeApplyCommanded);
                _primaryFailure ??= _centreOutcomeSource[index];
                anyFailed = true;
                continue;
            }

            if (_centreOutcome[index] != CleaningPathMotionOutcome.MoveCompletedPositionVerified)
            {
                // The Move has not produced its typed verified completion yet; wait without fabricating.
                continue;
            }

            // Mirror of the typed path verification: a typed success without the final inactive,
            // current and fresh observations never verifies.
            if (frame.MoveActive || !frame.IdleObserved || !frame.TpFresh || !frame.RpFresh
                || frame.PositionTp is null || frame.PositionRp is null)
            {
                _primaryFailure ??= CleaningPathExecutionCodes.VerificationRefusedObservationMismatch;
                _centreRecords[index] = new H1AxisCentreRecord(
                    axisId,
                    target,
                    CleaningPathMotionOutcome.None,
                    CleaningPathExecutionCodes.VerificationRefusedObservationMismatch,
                    Verified: false,
                    frame.BrakeApplyCommanded);
                anyFailed = true;
                continue;
            }

            _centreRecords[index] = new H1AxisCentreRecord(
                axisId,
                target,
                CleaningPathMotionOutcome.MoveCompletedPositionVerified,
                _centreOutcomeSource[index],
                Verified: true,
                frame.BrakeApplyCommanded);
        }

        if (_centreRecords[0] is not null && _centreRecords[1] is not null)
        {
            _centreReturn = new H1CentreReturnRecord(
                handoff.HorizontalCentreNative,
                handoff.VerticalCentreNative,
                _centreRecords[0]!,
                _centreRecords[1]!);
        }

        if (_centreRecords[0]?.Verified == true && _centreRecords[1]?.Verified == true)
        {
            _drySafeReturn = H1DrySafeReturnState.Completed;
            Finish(H1DryCommissioningPhase.Completed, H1DryCommissioningCodes.RunComplete);
            return;
        }

        if (anyFailed)
        {
            RequestRemainingStops();
            _pendingTerminal = (H1DryCommissioningPhase.Failed, H1DryCommissioningCodes.RunFailedCentreReturn);
        }
    }

    // ------------------------------------------------------------------------------ helpers

    private static string? CheckGuards(H1DryCommissioningGuards guards)
    {
        if (!guards.ControllerIdentityVerified)
        {
            return H1DryCommissioningCodes.StartRefusedControllerIdentityNotVerified;
        }

        if (!guards.ControllerEndpointConfigured)
        {
            return H1DryCommissioningCodes.StartRefusedEndpointNotConfigured;
        }

        if (!guards.OwnerLocalProfileConfigured)
        {
            return H1DryCommissioningCodes.StartRefusedProfileNotConfigured;
        }

        if (!guards.DryModeDeclared)
        {
            return H1DryCommissioningCodes.StartRefusedDryModeNotDeclared;
        }

        if (!guards.PumpCommandPathUnavailable)
        {
            return H1DryCommissioningCodes.StartRefusedPumpCommandPathAvailable;
        }

        if (!guards.PhysicalValveCommandPathUnavailable)
        {
            return H1DryCommissioningCodes.StartRefusedPhysicalValveCommandPathAvailable;
        }

        if (!guards.QueueAutoSequenceUnavailable)
        {
            return H1DryCommissioningCodes.StartRefusedQueueAutoSequenceAvailable;
        }

        if (!guards.PhysicalAbortAccessible)
        {
            return H1DryCommissioningCodes.StartRefusedPhysicalAbortNotAccessible;
        }

        return guards.OperatorObserverPresent
            ? null
            : H1DryCommissioningCodes.StartRefusedOperatorObserverNotPresent;
    }

    private void ResetRunState()
    {
        _phase = H1DryCommissioningPhase.NotStarted;
        _primaryFailure = null;
        _drySafeReturn = H1DrySafeReturnState.NotPublished;
        _cabinetAdmission = H1CabinetAdmission.NotEvaluated;
        _centreReturn = null;
        _closingRequested = false;
        _abortingRequested = false;
        _pendingTerminal = null;
        Array.Clear(_homeSeen);
        Array.Clear(_homeSettled);
        Array.Clear(_homeFailed);
        Array.Clear(_axisStopRequested);
        Array.Clear(_centreRecords);
        Array.Clear(_centreOutcome);
        _homeLogWatermark = 0;
        _runKind = RunKind.None;
        _homeOutcomeCode[0] = string.Empty;
        _homeOutcomeCode[1] = string.Empty;
        _centreOutcomeSource[0] = string.Empty;
        _centreOutcomeSource[1] = string.Empty;
    }

    private void RequestRemainingStops()
    {
        RequestStopOnce(CleaningPathExecutionRole.Horizontal, 0);
        RequestStopOnce(CleaningPathExecutionRole.Vertical, 1);
    }

    private void RequestStopOnce(CleaningPathExecutionRole role, int index)
    {
        if (_axisStopRequested[index])
        {
            return;
        }

        var frame = _boundary.Frame(role);
        if (frame.MoveActive)
        {
            _boundary.RequestStop(role);
            _axisStopRequested[index] = true;
            return;
        }

        var axis = AxisEnumFor(index == 0 ? _identity.HorizontalAxisId : _identity.VerticalAxisId);
        foreach (var operation in _session.Snapshot.ActiveOperations)
        {
            if (operation.Axis == axis)
            {
                _session.RequestStop(axis);
                _axisStopRequested[index] = true;
                break;
            }
        }
    }

    private bool BothAxesSettled()
    {
        foreach (var operation in _session.Snapshot.ActiveOperations)
        {
            if (operation.Axis == AxisEnumFor(_identity.HorizontalAxisId)
                || operation.Axis == AxisEnumFor(_identity.VerticalAxisId))
            {
                return false;
            }
        }

        return !_boundary.Frame(CleaningPathExecutionRole.Horizontal).MoveActive
            && !_boundary.Frame(CleaningPathExecutionRole.Vertical).MoveActive;
    }

    private void Finish(H1DryCommissioningPhase terminalPhase, string code)
    {
        _phase = terminalPhase;
        _pendingTerminal = null;
        Result = new H1DryCommissioningResult
        {
            TerminalPhase = terminalPhase,
            Code = code,
            PrimaryFailureCode = _primaryFailure,
            Identity = _identity,
            CabinetAdmission = _cabinetAdmission,
            HorizontalCoordinate = BuildCoordinateEvidence(0),
            VerticalCoordinate = BuildCoordinateEvidence(1),
            PathResult = _path.Result,
            CentreReturn = _centreReturn,
            VerifiedLedger = _path.Current?.VerifiedLedger ?? Array.Empty<JobPhase>(),
            HorizontalCleanup = BuildCleanupEvidence(0),
            VerticalCleanup = BuildCleanupEvidence(1),
            DrySafeReturn = _drySafeReturn,
        };
        Publish();
    }

    private H1AxisCoordinateEvidence BuildCoordinateEvidence(int index)
    {
        var axisId = index == 0 ? _identity.HorizontalAxisId : _identity.VerticalAxisId;
        var roleLabel = index == 0 ? "Horizontal" : "Vertical";
        var snapshot = AxisSnapshot(AxisEnumFor(axisId));
        var dualZeroState = (snapshot?.DualZeroState ?? ManualMotionDualZeroState.NotDispatched).ToString();
        var disposition = snapshot?.DualZeroState switch
        {
            // The Home-carried reset-encoder composition dispatched the dual-zero definition after
            // the position-zero definition and proved zero from strictly newer position reads.
            ManualMotionDualZeroState.DispatchedUnverified => H1DryCommissioningCodes.DualZeroDispatchedInHomeSequence,

            // The composition was refused at its dual-zero definition: the Home failed closed.
            ManualMotionDualZeroState.Refused => H1DryCommissioningCodes.DualZeroRefusedInHomeSequence,

            // No dual-zero definition was dispatched in this session (for example a Home that ended
            // before zero establishment). The composition is never optional for a Home that reaches
            // zero establishment; this disposition only records that it was not reached. Nothing is
            // claimed.
            _ => H1DryCommissioningCodes.DualZeroNotDispatched,
        };
        return new H1AxisCoordinateEvidence(
            axisId,
            roleLabel,
            _homeOutcomeCode[index] ?? string.Empty,
            _homeSettled[index],
            snapshot?.CoordinateState == ManualMotionCoordinateState.ZeroVerified,
            dualZeroState,
            disposition);
    }

    private H1AxisCleanupEvidence BuildCleanupEvidence(int index)
    {
        var axisId = index == 0 ? _identity.HorizontalAxisId : _identity.VerticalAxisId;
        if (!_axisStopRequested[index])
        {
            return new H1AxisCleanupEvidence(axisId, false, H1DryCommissioningCodes.CleanupNotNeeded);
        }

        var role = index == 0 ? CleaningPathExecutionRole.Horizontal : CleaningPathExecutionRole.Vertical;
        var frame = _boundary.Frame(role);
        return new H1AxisCleanupEvidence(
            axisId,
            true,
            frame.StopState == CleaningPathStopState.Issued && frame.IdleObserved
                ? H1DryCommissioningCodes.CleanupStopSupervised
                : H1DryCommissioningCodes.CleanupIncomplete);
    }

    private bool ObservationsCurrent(ManualMotionAxis axis)
    {
        var snapshot = AxisSnapshot(axis);
        return snapshot is not null
            && snapshot.StatusWord.Fresh
            && snapshot.PositionTp.Fresh
            && snapshot.PositionRp.Fresh
            && snapshot.BrakeOutput.Fresh;
    }

    private ManualMotionAxisSnapshot? AxisSnapshot(ManualMotionAxis axis)
    {
        foreach (var snapshot in _session.Snapshot.Axes)
        {
            if (snapshot.Axis == axis)
            {
                return snapshot;
            }
        }

        return null;
    }

    private static ManualMotionAxis AxisEnumFor(string axisId) => axisId switch
    {
        "A" => ManualMotionAxis.A,
        "B" => ManualMotionAxis.B,
        "C" => ManualMotionAxis.C,
        "D" => ManualMotionAxis.D,
        _ => throw new ArgumentException("axis identity is not one of the four typed axes", nameof(axisId)),
    };

    private static H1DryCommissioningIdentity BuildIdentity(ResolvedCleaningPath resolved) => new()
    {
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
    };

    private static H1DryCommissioningIdentity EmptyIdentity() => new()
    {
        SensorId = string.Empty,
        CanonicalWaterJetId = string.Empty,
        EffectiveWaterJetId = string.Empty,
        EffectiveWjProvenance = EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT,
        DerivedIsolationValveId = string.Empty,
        CabinetId = string.Empty,
        ControllerId = string.Empty,
        HorizontalAxisId = string.Empty,
        VerticalAxisId = string.Empty,
        HorizontalBrakeOutputId = string.Empty,
        VerticalBrakeOutputId = string.Empty,
    };

    private void Publish()
    {
        Current = new H1DryCommissioningSnapshot
        {
            RunId = _runId,
            Phase = _phase,
            Identity = _identity,
            CabinetAdmission = _cabinetAdmission,
            CurrentPointId = CurrentPointId(),
            VerifiedLedger = _path.Current?.VerifiedLedger ?? Array.Empty<JobPhase>(),
            PrimaryFailureCode = _primaryFailure,
            DrySafeReturn = _drySafeReturn,
        };
    }

    private string? CurrentPointId()
    {
        if (_phase != H1DryCommissioningPhase.PathExecution || _path.Current is null || _path.Result is not null)
        {
            return null;
        }

        return ((JobPhase)_path.Current.CurrentPointIndex).ToString();
    }
}
