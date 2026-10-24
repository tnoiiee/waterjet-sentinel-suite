using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

/// <summary>
/// The Application-owned, deterministic, Simulation-only Phase 3C composition coordinator. It
/// composes the accepted immutable Phase 3A resolution, the existing canonical Equipment / Motion
/// derivations, one AI-003 Pump Outlet Pressure readiness gate and the paired Isolation Valve (the
/// WJn to IVn derivation) around the existing typed Phase 3B execution coordinator, which it never
/// duplicates or replaces:
///
/// - before P1: a valid AI-003 Pump Outlet observation must satisfy the strict Pump-ready rule
///   (pressure strictly greater than the configured setpoint) or the start is refused fail-closed
///   with the exact pressure evidence preserved; no Pump command exists in this scope;
/// - P1 runs through the Phase 3B dual-axis execution; only when BOTH axes verify P1 is the paired
///   IV open commanded and supervised through the existing Sequencing pressure / Valve classifier;
/// - P2 through P6 start only after the open supervision accepts the open state (the typed point
///   advance gate), preserving strict point order, parallel-independent per-axis lifecycles and
///   every Phase 3B failure and cleanup rule;
/// - after both axes verify P6 the paired IV close is commanded and its supervision classification
///   is recorded; the composition then publishes a typed Mandatory Safe Return handoff only.
///
/// The successful terminal means exactly "path and paired-IV sequence completed, awaiting Mandatory
/// Safe Return". It never means a completed Cleaning Job, a completed Safe Return or Production
/// completion, and no Centre movement is ever commanded here. On any failure the first primary
/// cause is preserved unchanged, every later path point stays blocked, active motion is supervised
/// through the Phase 3B boundary (exactly-once Stop of the remaining active axis), and the paired
/// IV is fenced closed when it may be open or its state is unknown, with motion cleanup and Valve
/// cleanup recorded separately.
///
/// The coordinator is pure with respect to hardware: it issues typed paired-IV command requests and
/// reads typed observations only; no hardware or native adapter type is referenced and no raw
/// device command string exists here.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathCompositionCoordinator : ICleaningPathPointAdvanceGate
{
    private static long _compositionCounter;

    private readonly ICleaningPathMotionBoundary _motion;
    private readonly ICleaningPathEquipmentBoundary _equipment;
    private readonly IClock _clock;
    private readonly CleaningPathExecutionCoordinator _path;
    private readonly List<CleaningPathCompositionEvent> _events = new();

    private CleaningPathCompositionRequest? _request;
    private CleaningPathCompositionIdentity? _identity;
    private CleaningPathPumpReadinessResult? _pumpReadiness;
    private CleaningPathValveSupervisionResult? _valveOpenSupervision;
    private CleaningPathValveSupervisionResult? _valveCloseSupervision;
    private CleaningPathValveSupervisionResult? _valveCleanupSupervision;
    private CleaningPathCompositionStatus _status = CleaningPathCompositionStatus.NotStarted;
    private string _compositionId = string.Empty;
    private long _evidenceSequence;
    private bool _started;
    private bool _valveOpenRequested;
    private bool _valveOpenAccepted;
    private bool _valveCloseRequested;
    private bool _valveCleanupCloseRequested;
    private DateTimeOffset _valveSupervisionStartedAt;
    private string? _primaryFailureCode;
    private bool _abortRequested;
    private bool _closeRequested;

    public CleaningPathCompositionCoordinator(
        ICleaningPathMotionBoundary motion,
        ICleaningPathEquipmentBoundary equipment,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(clock);
        _motion = motion;
        _equipment = equipment;
        _clock = clock;

        // The Phase 3B coordinator is composed, never duplicated: this composition is its optional
        // point-advance gate, so P2 through P6 start only after the paired-IV open is accepted.
        _path = new CleaningPathExecutionCoordinator(motion, clock, this);
    }

    /// <summary>The composition lifecycle snapshot republished after every step (null before Start).</summary>
    public CleaningPathCompositionSnapshot? Current { get; private set; }

    /// <summary>The final typed composition result (null until a terminal state).</summary>
    public CleaningPathCompositionResult? Result { get; private set; }

    /// <summary>True between a successful Start and a terminal result.</summary>
    public bool Active => _started && Result is null;

    /// <summary>
    /// Starts one composition: validates the request, requires the Pump-ready gate BEFORE any P1
    /// Move request, then delegates the P1 through P6 motion execution to the existing Phase 3B
    /// coordinator. Every refusal is fail-closed with the exact Pump evidence preserved.
    /// </summary>
    public CleaningPathCompositionStartOutcome Start(
        CleaningPathSnapshot? snapshot,
        CleaningPathResolutionResult? resolution,
        CleaningPathCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_started)
        {
            return new CleaningPathCompositionStartOutcome(false, CleaningPathCompositionCodes.StartRefusedCompositionActive, null, null);
        }

        if (!request.PressureThresholds.Valid)
        {
            return new CleaningPathCompositionStartOutcome(false, SequencingPressureThresholds.InvalidCode, null, null);
        }

        if (request.ValveSupervisionTimeout <= TimeSpan.Zero)
        {
            return new CleaningPathCompositionStartOutcome(
                false, CleaningPathCompositionCodes.StartRefusedValveSupervisionTimeoutInvalid, null, null);
        }

        _compositionId = "CPC" + Interlocked.Increment(ref _compositionCounter).ToString(CultureInfo.InvariantCulture);

        // Pump-ready is evaluated once, freshly, from the exact Pump Outlet source identity: a
        // stale, BAD, null, non-finite or wrong-source observation never starts P1 and is never
        // fabricated or retained as ready.
        var pump = EvaluatePumpReadiness(request.PressureThresholds);
        if (!pump.Ready)
        {
            return new CleaningPathCompositionStartOutcome(false, pump.Code, pump, null);
        }

        var pathStart = _path.Start(snapshot, resolution);
        if (!pathStart.Started)
        {
            return new CleaningPathCompositionStartOutcome(false, pathStart.Code, pump, null);
        }

        _request = request;
        _identity = IdentityFrom(resolution!.Resolution!);
        _pumpReadiness = pump;
        _started = true;
        _status = CleaningPathCompositionStatus.ExecutingPath;
        Record(CleaningPathCompositionCodes.KindPumpReadinessEvaluated, pump.Code, null);
        Publish();
        return new CleaningPathCompositionStartOutcome(true, CleaningPathCompositionCodes.PumpReadyAccepted, pump, Current);
    }

    /// <summary>
    /// Advances the composition by one deterministic driver step: one motion boundary step, one
    /// Phase 3B execution step and one composition supervision step.
    /// </summary>
    public void Step()
    {
        if (!_started || Result is not null)
        {
            return;
        }

        _motion.Step();
        _path.Step();
        Supervise();
        Publish();
    }

    /// <summary>
    /// Operator abort: no next point is ever started, active axes are supervised through the Phase
    /// 3B exactly-once Stop lifecycle, the paired IV is fenced closed when needed and the terminal
    /// preserves the requested cause while awaiting Mandatory Safe Return.
    /// </summary>
    public void Abort()
    {
        if (!_started || Result is not null || _abortRequested || _closeRequested)
        {
            return;
        }

        _abortRequested = true;
        Record(CleaningPathCompositionCodes.KindCompositionAbortRequested, CleaningPathCompositionCodes.AbortRequested, CurrentPointId());
        _path.Abort();
        Publish();
    }

    /// <summary>
    /// Application close: active axes are supervised through the Phase 3B lifecycle and the motion
    /// session close is requested; the paired IV is fenced closed when needed and the terminal
    /// preserves the requested cause while awaiting Mandatory Safe Return.
    /// </summary>
    public void RequestClose()
    {
        if (!_started || Result is not null || _closeRequested)
        {
            return;
        }

        _closeRequested = true;
        Record(CleaningPathCompositionCodes.KindCompositionCloseRequested, CleaningPathCompositionCodes.CloseRequested, CurrentPointId());
        _path.RequestClose();
        Publish();
    }

    /// <summary>The composed Phase 3B advance gate: P2 through P6 require the accepted paired-IV open.</summary>
    bool ICleaningPathPointAdvanceGate.PointStartAllowed(JobPhase pointId) => _valveOpenAccepted;

    // ------------------------------------------------------------------------------- pump gate

    private CleaningPathPumpReadinessResult EvaluatePumpReadiness(SequencingPressureThresholds thresholds)
    {
        var sample = _equipment.ReadPumpOutletPressure();
        if (!SequencingPressure.Valid(sample)
            || !string.Equals(sample!.SourceId, PressureSample.PumpOutletSource, StringComparison.Ordinal))
        {
            // Invalid input and wrong-source input are the same fail-closed class: no readiness
            // rule may be evaluated and the exact sample evidence is preserved.
            return new CleaningPathPumpReadinessResult(false, SequencingCodes.PumpPressureInputInvalid, sample);
        }

        return SequencingPressure.PumpReady(sample, thresholds)
            ? new CleaningPathPumpReadinessResult(true, CleaningPathCompositionCodes.PumpReadyAccepted, sample)
            : new CleaningPathPumpReadinessResult(false, SequencingCodes.PumpNotReady, sample);
    }

    // ------------------------------------------------------------------------------ supervision

    private void Supervise()
    {
        switch (_status)
        {
            case CleaningPathCompositionStatus.SupervisingValveOpen:
                SuperviseValveOpen();
                return;

            case CleaningPathCompositionStatus.SupervisingValveClose:
                SuperviseValveClose();
                return;

            case CleaningPathCompositionStatus.SupervisingValveCleanup:
                SuperviseValveCleanup();
                return;

            case CleaningPathCompositionStatus.ExecutingPath:
                break;

            default:
                return;
        }

        if (_path.Result is not null)
        {
            HandlePathTerminal();
            return;
        }

        // P1 is dual-verified and held between points by the advance gate: only now may the paired
        // IV open be requested. A single verified axis never reaches this state.
        if (!_valveOpenRequested
            && _path.Current is
            {
                Status: CleaningPathExecutionStatus.Running,
                CurrentPoint.PointId: JobPhase.P1,
                CurrentPoint.State: CleaningPathPointState.Verified,
            })
        {
            BeginValveOpenSupervision();
        }
    }

    private void BeginValveOpenSupervision()
    {
        _valveOpenRequested = true;
        _valveSupervisionStartedAt = _clock.UtcNow;
        _equipment.RequestValve(new CleaningPathValveCommandRequest(_identity!.DerivedIsolationValveId, CleaningPathValveCommandKind.Open));
        Record(CleaningPathCompositionCodes.KindValveOpenRequested, SequencingCodes.IntentValveOpen, JobPhase.P1);
        _status = CleaningPathCompositionStatus.SupervisingValveOpen;
    }

    private void SuperviseValveOpen()
    {
        if (_abortRequested || _closeRequested)
        {
            // The requested cause preserves the terminal; an unaccepted open leaves the paired IV
            // state unknown, so the close fence runs before the terminal.
            EnterValveCleanupOrTerminal();
            return;
        }

        var timedOut = _clock.UtcNow - _valveSupervisionStartedAt >= _request!.ValveSupervisionTimeout;
        var valveId = _identity!.DerivedIsolationValveId;
        var sample = _equipment.ReadValveOutletPressure(valveId);
        var upperDetected = _equipment.ReadUpperLimitDetected(valveId);
        var assessment = SequencingPressure.Open(SourceChecked(sample, valveId), upperDetected, timedOut, _request.PressureThresholds);
        if (assessment.Open is null && assessment.Diagnosis == ValveDiagnosis.NONE)
        {
            // Still pending inside the supervision window: no pressure band is inferred yet.
            return;
        }

        _valveOpenSupervision = new CleaningPathValveSupervisionResult(valveId, assessment, sample, upperDetected, timedOut);
        Record(CleaningPathCompositionCodes.KindValveOpenSupervision, OpenCodeFor(assessment), JobPhase.P1);

        if (assessment.Open is ValveOpenResolution.OPEN_CONFIRMED or ValveOpenResolution.OPEN_BY_PRESSURE)
        {
            _valveOpenAccepted = true;
            _status = CleaningPathCompositionStatus.ExecutingPath;
            return;
        }

        // Blocked: the Valve diagnosis is the first primary failure and every later path point stays
        // blocked (the advance gate stays closed and the path execution ends without starting P2).
        _primaryFailureCode ??= assessment.Diagnosis.ToString();
        EndPathWithoutFurtherPoints();
        EnterValveCleanupOrTerminal();
    }

    private void BeginValveCloseSupervision()
    {
        _valveCloseRequested = true;
        _valveSupervisionStartedAt = _clock.UtcNow;
        _equipment.RequestValve(new CleaningPathValveCommandRequest(_identity!.DerivedIsolationValveId, CleaningPathValveCommandKind.Close));
        Record(CleaningPathCompositionCodes.KindValveCloseRequested, SequencingCodes.IntentValveClose, JobPhase.P6);
        _status = CleaningPathCompositionStatus.SupervisingValveClose;
    }

    private void SuperviseValveClose()
    {
        var result = EvaluateCloseSupervision();
        if (result is null)
        {
            return;
        }

        // The close classification is recorded for the Mandatory Safe Return phase to verify; the
        // sequence itself is complete and the terminal awaits Safe Return.
        _valveCloseSupervision = result;
        Record(CleaningPathCompositionCodes.KindValveCloseSupervision, CloseCodeFor(result.Assessment), JobPhase.P6);
        Terminalize();
    }

    private void BeginValveCleanupSupervision()
    {
        if (_valveCleanupCloseRequested)
        {
            return;
        }

        _valveCleanupCloseRequested = true;
        _valveSupervisionStartedAt = _clock.UtcNow;
        _equipment.RequestValve(new CleaningPathValveCommandRequest(_identity!.DerivedIsolationValveId, CleaningPathValveCommandKind.Close));
        Record(CleaningPathCompositionCodes.KindValveCleanupRequested, SequencingCodes.IntentValveClose, CurrentPointId());
        _status = CleaningPathCompositionStatus.SupervisingValveCleanup;
    }

    private void SuperviseValveCleanup()
    {
        var result = EvaluateCloseSupervision();
        if (result is null)
        {
            return;
        }

        // Valve cleanup is recorded separately from the primary failure and from motion cleanup.
        _valveCleanupSupervision = result;
        Record(CleaningPathCompositionCodes.KindValveCleanupSupervision, CloseCodeFor(result.Assessment), CurrentPointId());
        Terminalize();
    }

    private CleaningPathValveSupervisionResult? EvaluateCloseSupervision()
    {
        var timedOut = _clock.UtcNow - _valveSupervisionStartedAt >= _request!.ValveSupervisionTimeout;
        var valveId = _identity!.DerivedIsolationValveId;
        var sample = _equipment.ReadValveOutletPressure(valveId);
        var lowerDetected = _equipment.ReadLowerLimitDetected(valveId);
        var assessment = SequencingPressure.Close(SourceChecked(sample, valveId), lowerDetected, timedOut, _request.PressureThresholds);
        if (assessment.Close is null)
        {
            return null;
        }

        return new CleaningPathValveSupervisionResult(valveId, assessment, sample, lowerDetected, timedOut);
    }

    // ------------------------------------------------------------------------------ path bridge

    private void HandlePathTerminal()
    {
        var pathResult = _path.Result!;

        if (_abortRequested || _closeRequested)
        {
            EnterValveCleanupOrTerminal();
            return;
        }

        if (pathResult.Status == CleaningPathExecutionStatus.PathCompleteAwaitingSafeReturn)
        {
            BeginValveCloseSupervision();
            return;
        }

        // The motion failure code is the first primary failure when no earlier cause exists.
        _primaryFailureCode ??= pathResult.Code;
        EnterValveCleanupOrTerminal();
    }

    private void EndPathWithoutFurtherPoints()
    {
        // The typed path Abort is the fence that guarantees no further point request is ever
        // issued; it never overwrites the already-captured primary failure.
        _path.Abort();
    }

    private void EnterValveCleanupOrTerminal()
    {
        if (_valveOpenRequested && !_valveCloseRequested && !_valveCleanupCloseRequested)
        {
            // The paired IV may be open or its state is unknown: fence it closed before terminal.
            BeginValveCleanupSupervision();
            return;
        }

        Terminalize();
    }

    // ------------------------------------------------------------------------------ publication

    private void Terminalize()
    {
        var (status, code) = _abortRequested
            ? (CleaningPathCompositionStatus.AbortedAwaitingSafeReturn, CleaningPathCompositionCodes.CompositionAbortedAwaitingSafeReturn)
            : _closeRequested
                ? (CleaningPathCompositionStatus.ClosedAwaitingSafeReturn, CleaningPathCompositionCodes.CompositionClosedAwaitingSafeReturn)
                : _primaryFailureCode is not null
                    ? (CleaningPathCompositionStatus.FailedAwaitingSafeReturn, CleaningPathCompositionCodes.CompositionFailedAwaitingSafeReturn)
                    : (CleaningPathCompositionStatus.CompleteAwaitingSafeReturn, CleaningPathCompositionCodes.PathAndValveSequenceCompleteAwaitingSafeReturn);

        _status = status;
        var pathResult = _path.Result;
        var handoff = new CleaningPathCompositionHandoff
        {
            CompositionId = _compositionId,
            Identity = _identity!,
            TerminalStatus = status,
            TerminalCode = code,
            PumpReadiness = _pumpReadiness!,
            ValveOpenSupervision = _valveOpenSupervision,
            ValveCloseSupervision = _valveCloseSupervision,
            ValveCleanupSupervision = _valveCleanupSupervision,
            HorizontalFinalOutcome = FinalOutcomeFor(pathResult, horizontal: true),
            VerticalFinalOutcome = FinalOutcomeFor(pathResult, horizontal: false),
            PrimaryFailureCode = _primaryFailureCode,
            MotionCleanupState = pathResult?.CleanupState ?? CleaningPathCleanupState.NotNeeded,
            ValveCleanupState = ValveCleanupStateFor(_valveCleanupSupervision),
            MotionHandoff = pathResult?.Handoff,
            CompletedAt = _clock.UtcNow,
        };

        Record(CleaningPathCompositionCodes.KindCompositionTerminal, code, CurrentPointId());
        Result = new CleaningPathCompositionResult(
            status,
            code,
            handoff,
            pathResult,
            _events.ToArray(),
            handoff.MotionCleanupState,
            handoff.ValveCleanupState);
        Publish();
    }

    private JobPhase? CurrentPointId() => _path.Current?.CurrentPoint.PointId;

    private void Record(string kind, string code, JobPhase? pointId)
    {
        _evidenceSequence++;
        _events.Add(new CleaningPathCompositionEvent(_evidenceSequence, _clock.UtcNow, kind, code, pointId));
    }

    private void Publish()
    {
        Current = new CleaningPathCompositionSnapshot(
            _compositionId,
            _identity ?? PlaceholderIdentity(),
            _status,
            CurrentPointId(),
            _pumpReadiness ?? new CleaningPathPumpReadinessResult(false, string.Empty, null),
            _evidenceSequence);
    }

    private static CleaningPathCompositionIdentity IdentityFrom(ResolvedCleaningPath resolved) => new(
        resolved.SensorId,
        resolved.CanonicalAssignedWaterJetId,
        resolved.RequestedOverrideWaterJetId,
        resolved.EffectiveWaterJetId,
        resolved.EffectiveWjProvenance,
        resolved.DerivedIsolationValveId,
        resolved.CabinetId,
        resolved.ControllerId,
        resolved.HorizontalAxisId,
        resolved.VerticalAxisId);

    private static CleaningPathCompositionIdentity PlaceholderIdentity() => new(
        string.Empty, string.Empty, null, string.Empty, EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// A pressure observation enters the classifier only from the exact paired-IV outlet source
    /// identity; a wrong-source observation is invalid input and never yields a pressure band.
    /// </summary>
    private static PressureSample? SourceChecked(PressureSample? sample, string valveId) =>
        sample is not null
            && string.Equals(sample.SourceId, PressureSample.ValveOutletSource(valveId), StringComparison.Ordinal)
            ? sample
            : null;

    private static string OpenCodeFor(ValveAssessment assessment) =>
        assessment.Open is ValveOpenResolution.OPEN_CONFIRMED or ValveOpenResolution.OPEN_BY_PRESSURE
            ? CleaningPathCompositionCodes.ValveOpenAccepted
            : assessment.Diagnosis.ToString();

    private static string CloseCodeFor(ValveAssessment assessment) =>
        assessment.Diagnosis != ValveDiagnosis.NONE
            ? assessment.Diagnosis.ToString()
            : (assessment.Close ?? ValveCloseResolution.PRESSURE_INPUT_INVALID).ToString();

    private static CleaningPathCleanupState ValveCleanupStateFor(CleaningPathValveSupervisionResult? cleanup) =>
        cleanup?.Assessment.Close switch
        {
            ValveCloseResolution.LOWER_LIMIT_CONFIRMED or ValveCloseResolution.CLOSED_BY_PRESSURE => CleaningPathCleanupState.Succeeded,
            ValveCloseResolution.PRESSURE_INPUT_INVALID => CleaningPathCleanupState.Partial,
            ValveCloseResolution.LEAK_SUSPECTED or ValveCloseResolution.NOT_FULLY_CLOSED => CleaningPathCleanupState.Failed,
            _ => CleaningPathCleanupState.NotNeeded,
        };

    private static CleaningPathMotionOutcome FinalOutcomeFor(CleaningPathExecutionResult? pathResult, bool horizontal)
    {
        if (pathResult?.Handoff is { } motionHandoff)
        {
            return horizontal ? motionHandoff.HorizontalFinalOutcome : motionHandoff.VerticalFinalOutcome;
        }

        if (pathResult is { Points.Count: > 0 })
        {
            var lastPoint = pathResult.Points[^1];
            return horizontal ? lastPoint.Horizontal.MotionOutcome : lastPoint.Vertical.MotionOutcome;
        }

        return CleaningPathMotionOutcome.None;
    }
}
