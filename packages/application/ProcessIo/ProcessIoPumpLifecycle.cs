using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.ProcessIo;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the one reusable Pump lifecycle.
//
// Every consumer (manual commissioning, the integrated dry cycle, Cleaning Job preconditions and a
// future automatic sequence) uses THIS implementation. There is no second state machine, no second
// command order and no second cleanup path.
//
// SAFETY POSTURE
//   1. Admission is fail closed. Production, a conflicting owner, a concurrent lifecycle, an invalid
//      selection, an active configured Fault, an invalid configuration and an uncommissioned feedback
//      polarity all refuse BEFORE a single write.
//   2. Start never opens an Isolation Valve and never infers Running from pressure.
//   3. Pump-ready is AI-003 only, strictly above the configured setpoint, from a current PHYSICAL_READ
//      in the live generation. AI-002 is never read here.
//   4. Every terminal path that began a command runs the SAME corrected cleanup: ramp toward the
//      configured safe zero, de-energise Run Pump, then de-energise Enable Inverter, then wait for
//      confirmed not-running feedback.
//   5. An unknown stopped state is never reported as safe completion.
//   6. Nothing retries. One lifecycle, one attempt.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>Typed cleanup outcome vocabulary.</summary>
public static class PumpCleanupStates
{
    /// <summary>Every commanded de-energisation was dispatched and not-running was confirmed.</summary>
    public const string Complete = "PUMP_CLEANUP_COMPLETE";

    /// <summary>At least one cleanup obligation could not be completed.</summary>
    public const string Incomplete = "PUMP_CLEANUP_INCOMPLETE";

    /// <summary>The stopped state remains unknown, so a physical abort is required.</summary>
    public const string PhysicalAbortRequired = "PUMP_CLEANUP_PHYSICAL_ABORT_REQUIRED";
}

/// <summary>One Pump lifecycle configuration and the ownership record it runs under.</summary>
public sealed record PumpLifecycleStartRequest
{
    /// <summary>Who is asking. A Production owner is always refused.</summary>
    public required PumpCommandOwnerKind Owner { get; init; }

    /// <summary>A stable identity for the owner, used to refuse a conflicting second owner.</summary>
    public required string OwnerId { get; init; }

    /// <summary>The Pump selection. Only 1 and 2 are commissioned.</summary>
    public int Selection { get; init; } = PumpCommandContract.CurrentCriticalPathSelection;
}

/// <summary>The typed Start verdict. Nothing here is a hardware confirmation.</summary>
public sealed record PumpLifecycleStartOutcome(bool Started, string Code, PumpLifecycleState State);

/// <summary>The typed Stop verdict.</summary>
public sealed record PumpLifecycleStopOutcome(
    bool StopConfirmed,
    string Code,
    PumpLifecycleState State,
    string CleanupState);

/// <summary>
/// The terminal record. It keeps the primary failure SEPARATE from the cleanup faults, so a cleanup
/// that failed after a trip can never overwrite the trip that caused it.
/// </summary>
public sealed record PumpLifecycleResult
{
    public required PumpLifecycleState TerminalState { get; init; }

    /// <summary>The first failure state that caused termination, retained verbatim. Null on a normal Stop.</summary>
    public PumpLifecycleState? PrimaryFailure { get; init; }

    /// <summary>Cleanup faults in the order they occurred. Never merged into <see cref="PrimaryFailure"/>.</summary>
    public required IReadOnlyList<string> CleanupFaultCodes { get; init; }

    /// <summary>One of the <see cref="PumpCleanupStates"/> values.</summary>
    public required string CleanupState { get; init; }

    /// <summary>The Stop mechanism was commanded (not inferred) at least once.</summary>
    public required bool StopCommanded { get; init; }

    /// <summary>True only when the stopped state is confirmed. Completion must never be reported otherwise.</summary>
    public required bool StoppedConfirmed { get; init; }

    /// <summary>Why the Stop path was entered. Retained for evidence only.</summary>
    public required PumpStopReason StopReason { get; init; }

    /// <summary>
    /// The High Critical Pump event this lifecycle raises, if any. The alarm latch, modal presentation
    /// and Queue blocking are the existing sequencing rules and are not reimplemented here.
    /// </summary>
    public CriticalPumpKind? CriticalEvent { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// The single Pump command-ownership boundary. Exactly one owner may hold the channel, and a
/// Production owner can never acquire it in this scope.
/// </summary>
public sealed class PumpCommandOwnership
{
    private PumpCommandOwnerKind _kind = PumpCommandOwnerKind.NONE;
    private string? _ownerId;
    private int _generation;

    public PumpCommandOwnerKind Kind => _kind;

    public string? OwnerId => _ownerId;

    public bool IsHeld => _kind != PumpCommandOwnerKind.NONE;

    /// <summary>Advances on every successful acquisition, so a stale holder can be identified.</summary>
    public int Generation => _generation;

    /// <summary>
    /// Acquires the channel for one owner, or refuses with a typed code. A second acquisition by the
    /// same owner is a concurrent-lifecycle refusal, not a re-entrant grant.
    /// </summary>
    public bool TryAcquire(PumpCommandOwnerKind kind, string ownerId, out string code)
    {
        code = PumpCommandRefusals.Granted;

        if (kind == PumpCommandOwnerKind.NONE)
        {
            code = PumpCommandRefusals.RefusedInvalidConfiguration;
            return false;
        }

        if (kind == PumpCommandOwnerKind.PRODUCTION)
        {
            // Production write authority is not granted in this scope. This is a refusal, not a gate.
            code = PumpCommandRefusals.RefusedProduction;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            code = PumpCommandRefusals.RefusedInvalidConfiguration;
            return false;
        }

        if (IsHeld)
        {
            code = string.Equals(_ownerId, ownerId, StringComparison.Ordinal) && _kind == kind
                ? PumpCommandRefusals.RefusedConcurrentLifecycle
                : PumpCommandRefusals.RefusedConflictingOwner;
            return false;
        }

        _kind = kind;
        _ownerId = ownerId;
        _generation += 1;
        return true;
    }

    /// <summary>Releases the channel. Called only after confirmed Stop or after a refused admission.</summary>
    public void Release()
    {
        _kind = PumpCommandOwnerKind.NONE;
        _ownerId = null;
    }
}

/// <summary>
/// The one Pump lifecycle. It is driven explicitly by <see cref="Step"/> so that every transition is
/// deterministic and every wait is bounded by the injected clock.
/// </summary>
public sealed class ProcessIoPumpLifecycle
{
    private readonly IPumpCommandBoundary _boundary;
    private readonly PumpCommandOwnership _ownership;
    private readonly TimeProvider _time;
    private readonly List<string> _cleanupFaultCodes = new();

    private DateTimeOffset _waitStartedAt;
    private DateTimeOffset _nextRampAt;
    private double _speedCommand;
    private bool _stopRequested;
    private bool _runCleared;
    private bool _enableCleared;
    private bool _stopCommanded;
    private bool _stopEntered;
    private PumpLifecycleState? _primaryFailure;
    private CriticalPumpKind? _criticalEvent;
    private PumpStopReason _stopReason = PumpStopReason.NORMAL_COMPLETION;
    private readonly SequencingPressureThresholds _thresholds;

    public ProcessIoPumpLifecycle(
        IPumpCommandBoundary boundary,
        PumpLifecycleConfiguration configuration,
        PumpCommandOwnership ownership,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(ownership);

        _boundary = boundary;
        Configuration = configuration;
        _ownership = ownership;
        _time = time ?? TimeProvider.System;
        // Only PumpReadySetpointBar is consumed by SequencingPressure.PumpReady. The Low and High band
        // members belong to the Isolation Valve classifier and are deliberately not restated here, so
        // this lifecycle cannot invent a Valve threshold.
        _thresholds = new SequencingPressureThresholds(
            PumpReadySetpointBar: configuration.PumpReadySetpointBar);
    }

    public PumpLifecycleConfiguration Configuration { get; }

    public PumpLifecycleState State { get; private set; } = PumpLifecycleState.IDLE;

    /// <summary>True while this lifecycle holds the Pump command channel.</summary>
    public bool OwnsPumpCommand => _ownership.IsHeld && _ownership.Kind != PumpCommandOwnerKind.NONE;

    /// <summary>The current analog engineering command in milliamps.</summary>
    public double SpeedCommand => _speedCommand;

    /// <summary>The terminal record, populated once the lifecycle terminates.</summary>
    public PumpLifecycleResult? Result { get; private set; }

    /// <summary>The first failure state, retained separately from the cleanup faults.</summary>
    public PumpLifecycleState? PrimaryFailure => _primaryFailure;

    /// <summary>Cleanup faults in occurrence order. Never merged into <see cref="PrimaryFailure"/>.</summary>
    public IReadOnlyList<string> CleanupFaultCodes => _cleanupFaultCodes;

    /// <summary>
    /// True only while the Pump is at confirmed readiness and no Stop has been requested. This is the
    /// single precondition an Isolation Valve OPEN must consult: a Stop request closes it immediately.
    /// </summary>
    public bool AllowsValveOpen =>
        State == PumpLifecycleState.PUMP_READY && !_stopRequested && Result is null;

    /// <summary>
    /// True only for a confirmed, complete, normal Stop. An unknown stopped state, an incomplete
    /// cleanup or any failure terminal is never a safe completion.
    /// </summary>
    public bool SafeToReportComplete
    {
        get
        {
            PumpLifecycleResult? result = Result;
            return result is not null
                && result.StoppedConfirmed
                && string.Equals(result.CleanupState, PumpCleanupStates.Complete, StringComparison.Ordinal)
                && result.TerminalState == PumpLifecycleState.STOPPED;
        }
    }

    // ---------------------------------------------------------------- start

    /// <summary>
    /// Admits and begins the Pump. Every refusal happens before the first write.
    /// </summary>
    public PumpLifecycleStartOutcome Start(PumpLifecycleStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Result is not null || PumpLifecycleStates.IsTerminal(State))
        {
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedAlreadyTerminal, State);
        }

        if (State != PumpLifecycleState.IDLE)
        {
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedConcurrentLifecycle, State);
        }

        if (!Configuration.Valid)
        {
            return Refuse(PumpCommandRefusals.RefusedInvalidConfiguration);
        }

        // The commissioning gate. A physical Pump command fails closed while either required polarity
        // is uncommissioned: without both, Running and Fault cannot be interpreted, and a Start that
        // cannot be observed is not a Start the Product may issue.
        if (!Configuration.Feedback.IsCommissioned)
        {
            return Refuse(PumpCommandRefusals.RefusedPolarityNotCommissioned);
        }

        if (!PumpCommandContract.IsValidSelection(request.Selection))
        {
            return Refuse(PumpCommandRefusals.RefusedInvalidSelection);
        }

        if (!_ownership.TryAcquire(request.Owner, request.OwnerId, out string ownershipCode))
        {
            return Refuse(ownershipCode);
        }

        // An active configured Fault refuses the Start outright. The Fault polarity is commissioned at
        // this point, so this is a real reading rather than an assumption.
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_TRIP;
            SettlePreStartRefusal(PumpLifecycleState.FAULT_ACTIVE);
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedFaultActive, State);
        }

        // 1. Selection: energise the selected contactor, then de-energise the other.
        State = PumpLifecycleState.SELECTING_PUMP;
        IReadOnlyList<PumpCommandOutcome> selection = _boundary.SelectPump(request.Selection);
        if (selection.Count != 2 || !AllSucceeded(selection))
        {
            _primaryFailure = PumpLifecycleState.SELECTION_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedCommandAdmission, State);
        }

        State = PumpLifecycleState.PUMP_SELECTED;

        // 2. Enable Inverter, first.
        PumpCommandOutcome enable = _boundary.SetEnableInverter(true);
        if (!enable.Succeeded)
        {
            _primaryFailure = PumpLifecycleState.ENABLE_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedCommandAdmission, State);
        }

        State = PumpLifecycleState.ENABLE_DISPATCHED;

        // 3. Run Pump, second. Both are maintained and neither is a pulse.
        PumpCommandOutcome run = _boundary.SetRunPump(true);
        if (!run.Succeeded)
        {
            _primaryFailure = PumpLifecycleState.RUN_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedCommandAdmission, State);
        }

        State = PumpLifecycleState.RUN_DISPATCHED;

        State = PumpLifecycleState.WAITING_FOR_RUNNING;
        _waitStartedAt = _time.GetUtcNow();
        return new PumpLifecycleStartOutcome(true, PumpCommandRefusals.Granted, State);
    }

    private PumpLifecycleStartOutcome Refuse(string code) =>
        new(false, code, State);

    /// <summary>
    /// Settles a refusal that occurred before any command was written. Nothing was energised, so there
    /// is no commanded state to return and no cleanup write is issued; the channel is released at once.
    /// This is NOT a safe completion: <see cref="SafeToReportComplete"/> requires a confirmed Stop.
    /// </summary>
    private void SettlePreStartRefusal(PumpLifecycleState failureState)
    {
        State = failureState;
        _ownership.Release();
        Result = BuildResult(failureState, PumpCleanupStates.Complete, stopCommanded: false, stoppedConfirmed: true);
    }

    // ---------------------------------------------------------------- stop

    /// <summary>
    /// Requests the Stop path for a normal completion, a cancellation, a downstream Isolation Valve
    /// failure or any other external reason. The mechanism is always the same corrected order.
    /// </summary>
    public PumpLifecycleStopOutcome Stop(PumpStopReason reason)
    {
        if (Result is not null)
        {
            return new PumpLifecycleStopOutcome(
                Result.StoppedConfirmed,
                PumpCommandRefusals.RefusedAlreadyTerminal,
                State,
                Result.CleanupState);
        }

        if (State == PumpLifecycleState.IDLE)
        {
            return new PumpLifecycleStopOutcome(
                false,
                PumpCommandRefusals.RefusedOwnershipNotHeld,
                State,
                PumpCleanupStates.Incomplete);
        }

        // Stop is always permitted. No application permissive may block it, and the same corrected
        // mechanism runs whatever the reason is.
        _stopRequested = true;
        _stopReason = reason;
        EnterStopPath(reason);
        return new PumpLifecycleStopOutcome(false, PumpCommandRefusals.Granted, State, PumpCleanupStates.Incomplete);
    }

    /// <summary>Enters the one Stop path. Idempotent: a second entry does not restart or duplicate it.</summary>
    private void EnterStopPath(PumpStopReason reason)
    {
        _stopRequested = true;
        _stopReason = reason;
        if (_stopEntered)
        {
            return;
        }

        _stopEntered = true;
        State = PumpLifecycleState.RAMPING_DOWN;
        _nextRampAt = _time.GetUtcNow();
    }

    // ---------------------------------------------------------------- step

    /// <summary>
    /// Advances the lifecycle by exactly one deterministic unit. It performs at most one command write,
    /// so the corrected Stop order is observable and each de-energisation happens exactly once.
    /// </summary>
    public void Step()
    {
        if (Result is not null || State == PumpLifecycleState.IDLE)
        {
            return;
        }

        DateTimeOffset now = _time.GetUtcNow();

        switch (State)
        {
            case PumpLifecycleState.WAITING_FOR_RUNNING:
                StepWaitingForRunning(now);
                return;

            case PumpLifecycleState.RAMPING_OUTPUT:
                StepRampingOutput(now);
                return;

            case PumpLifecycleState.WAITING_FOR_AI003_READY:
                StepWaitingForReady(now);
                return;

            case PumpLifecycleState.PUMP_READY:
                StepReady();
                return;

            case PumpLifecycleState.RAMPING_DOWN:
                StepRampingDown(now);
                return;

            case PumpLifecycleState.CLEARING_RUN:
                StepClearingRun();
                return;

            case PumpLifecycleState.DISABLING_INVERTER:
                StepDisablingInverter();
                return;

            case PumpLifecycleState.WAITING_FOR_STOPPED:
                StepWaitingForStopped(now);
                return;

            default:
                return;
        }
    }

    private void StepWaitingForRunning(DateTimeOffset now)
    {
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_TRIP;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Current && running.Active)
        {
            // Running is taken from the commissioned feedback input only. It is never inferred from
            // the outlet pressure and never from the command that was just dispatched.
            State = PumpLifecycleState.RAMPING_OUTPUT;
            _speedCommand = Configuration.Ramp.SafeZeroEngineeringCommand;
            _nextRampAt = now;
            return;
        }

        if (now - _waitStartedAt > Configuration.RunningFeedbackTimeout)
        {
            _primaryFailure = PumpLifecycleState.RUNNING_FEEDBACK_TIMEOUT;
            EnterStopPath(PumpStopReason.RUNNING_FEEDBACK_TIMEOUT);
        }
    }

    private void StepRampingOutput(DateTimeOffset now)
    {
        if (now < _nextRampAt)
        {
            return;
        }

        double target = Configuration.Ramp.TargetEngineeringCommand;
        if (PumpSpeedRamp.AtTarget(_speedCommand, target))
        {
            State = PumpLifecycleState.WAITING_FOR_AI003_READY;
            _waitStartedAt = now;
            return;
        }

        double next = PumpSpeedRamp.Next(_speedCommand, target, Configuration.Ramp.Step);
        if (!double.IsFinite(next))
        {
            _primaryFailure = PumpLifecycleState.COMMAND_ADMISSION_REFUSED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return;
        }

        PumpAnalogCommandOutcome outcome = _boundary.SetSpeedCommand(next);
        if (!outcome.Succeeded)
        {
            // The upward analog reference could not be dispatched. The cleanup still runs: a ramp
            // failure never prevents the two de-energisations.
            _primaryFailure = PumpLifecycleState.COMMAND_ADMISSION_REFUSED;
            _cleanupFaultCodes.Add(PumpCommandRefusals.RefusedDispatchFailed);
            EnterStopPath(PumpStopReason.START_FAILURE);
            return;
        }

        _speedCommand = next;
        _nextRampAt = now + Configuration.Ramp.Interval;
    }

    private void StepWaitingForReady(DateTimeOffset now)
    {
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_TRIP;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Current && !running.Active)
        {
            // The Pump stopped while it was expected to be running. This is the critical event and the
            // primary record is retained separately from any cleanup failure that follows.
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpPressureRead pressure = _boundary.ReadPumpOutletPressure();

        if (pressure.Quality == ProcessIoObservationQuality.STALE)
        {
            // A stale outlet reading is refused outright rather than reused as readiness.
            _primaryFailure = PumpLifecycleState.AI003_STALE;
            EnterStopPath(PumpStopReason.AI003_STALE);
            return;
        }

        if (!pressure.Accepted)
        {
            // BAD, DISABLED, UNCERTAIN or never observed: the sole Pump-ready authority is unusable, and
            // the Pump Inlet row is never substituted for it.
            _primaryFailure = PumpLifecycleState.AI003_INVALID;
            EnterStopPath(PumpStopReason.AI003_INVALID);
            return;
        }

        if (SequencingPressure.PumpReady(pressure.Sample, _thresholds))
        {
            State = PumpLifecycleState.PUMP_READY;
            return;
        }

        if (now - _waitStartedAt > Configuration.PumpReadyTimeout)
        {
            _primaryFailure = PumpLifecycleState.AI003_TIMEOUT;
            EnterStopPath(PumpStopReason.AI003_TIMEOUT);
        }
    }

    /// <summary>
    /// Supervises the Pump while it is ready. Readiness is a live condition, not a latch: an active
    /// Fault, a lost running feedback or a no longer usable outlet pressure leaves readiness at once,
    /// which closes the valve-open permission before any Isolation Valve can act on it.
    /// </summary>
    private void StepReady()
    {
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_TRIP;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Current && !running.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _criticalEvent = CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpPressureRead pressure = _boundary.ReadPumpOutletPressure();
        if (pressure.Quality == ProcessIoObservationQuality.STALE)
        {
            _primaryFailure = PumpLifecycleState.AI003_STALE;
            EnterStopPath(PumpStopReason.AI003_STALE);
            return;
        }

        if (!pressure.Accepted || !SequencingPressure.PumpReady(pressure.Sample, _thresholds))
        {
            _primaryFailure = PumpLifecycleState.AI003_INVALID;
            EnterStopPath(PumpStopReason.AI003_INVALID);
        }
    }

    private void StepRampingDown(DateTimeOffset now)
    {
        if (now < _nextRampAt)
        {
            return;
        }

        double safeZero = Configuration.Ramp.SafeZeroEngineeringCommand;
        if (PumpSpeedRamp.AtTarget(_speedCommand, safeZero))
        {
            State = PumpLifecycleState.CLEARING_RUN;
            return;
        }

        double next = PumpSpeedRamp.Next(_speedCommand, safeZero, Configuration.Ramp.Step);
        if (!double.IsFinite(next))
        {
            // A ramp that cannot be computed is a recorded cleanup fault, and the two de-energisations
            // are still attempted.
            RecordCleanupFault(PumpLifecycleState.RAMP_DOWN_FAILED);
            State = PumpLifecycleState.CLEARING_RUN;
            return;
        }

        PumpAnalogCommandOutcome outcome = _boundary.SetSpeedCommand(next);
        if (!outcome.Succeeded)
        {
            RecordCleanupFault(PumpLifecycleState.RAMP_DOWN_FAILED);
            State = PumpLifecycleState.CLEARING_RUN;
            return;
        }

        _speedCommand = next;
        _nextRampAt = now + Configuration.Ramp.Interval;
    }

    private void StepClearingRun()
    {
        if (_runCleared)
        {
            State = PumpLifecycleState.DISABLING_INVERTER;
            return;
        }

        // CORRECTED ORDER, step 1: de-energise Run Pump first.
        _runCleared = true;
        _stopCommanded = true;
        PumpCommandOutcome outcome = _boundary.SetRunPump(false);
        if (!outcome.Succeeded)
        {
            RecordCleanupFault(PumpLifecycleState.CLEAR_RUN_FAILED);
        }

        State = PumpLifecycleState.DISABLING_INVERTER;
    }

    private void StepDisablingInverter()
    {
        if (_enableCleared)
        {
            State = PumpLifecycleState.WAITING_FOR_STOPPED;
            _waitStartedAt = _time.GetUtcNow();
            return;
        }

        // CORRECTED ORDER, step 2: de-energise Enable Inverter second, never before Run.
        _enableCleared = true;
        PumpCommandOutcome outcome = _boundary.SetEnableInverter(false);
        if (!outcome.Succeeded)
        {
            RecordCleanupFault(PumpLifecycleState.DISABLE_FAILED);
        }

        State = PumpLifecycleState.WAITING_FOR_STOPPED;
        _waitStartedAt = _time.GetUtcNow();
    }

    private void StepWaitingForStopped(DateTimeOffset now)
    {
        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Current && !running.Active)
        {
            Settle(StoppedConfirmed: true);
            return;
        }

        if (now - _waitStartedAt > Configuration.StoppedFeedbackTimeout)
        {
            // The stopped state is unknown. The Product must never report safe completion here.
            RecordCleanupFault(PumpLifecycleState.STOPPED_FEEDBACK_TIMEOUT);
            Settle(StoppedConfirmed: false);
        }
    }

    /// <summary>Records one cleanup fault once, in order, without overwriting the primary failure.</summary>
    private void RecordCleanupFault(PumpLifecycleState fault)
    {
        _cleanupFaultCodes.Add(fault.ToString());
    }

    /// <summary>
    /// Settles the terminal record. The primary failure keeps its identity; the cleanup outcome is
    /// reported beside it and never replaces it.
    /// </summary>
    private void Settle(bool StoppedConfirmed)
    {
        string cleanupState;
        PumpLifecycleState terminal;

        if (!StoppedConfirmed)
        {
            // The stopped state is unknown. The terminal says so unambiguously, while the primary
            // failure is retained beside it: a trip is never erased by the cleanup outcome.
            cleanupState = PumpCleanupStates.PhysicalAbortRequired;
            terminal = PumpLifecycleState.PHYSICAL_ABORT_REQUIRED;
        }
        else if (_cleanupFaultCodes.Count == 0)
        {
            cleanupState = PumpCleanupStates.Complete;
            terminal = _primaryFailure ?? PumpLifecycleState.STOPPED;
        }
        else
        {
            cleanupState = PumpCleanupStates.Incomplete;
            terminal = _primaryFailure ?? PumpLifecycleState.CLEANUP_INCOMPLETE;
        }

        _ownership.Release();
        State = terminal;
        Result = BuildResult(terminal, cleanupState, _stopCommanded, StoppedConfirmed);
    }

    private PumpLifecycleResult BuildResult(
        PumpLifecycleState terminal,
        string cleanupState,
        bool stopCommanded,
        bool stoppedConfirmed) => new()
        {
            TerminalState = terminal,
            PrimaryFailure = _primaryFailure,
            CleanupFaultCodes = _cleanupFaultCodes.ToArray(),
            CleanupState = cleanupState,
            StopCommanded = stopCommanded,
            StoppedConfirmed = stoppedConfirmed,
            StopReason = _stopReason,
            CriticalEvent = _criticalEvent,
            CompletedAt = _time.GetUtcNow(),
        };

    private static bool AllSucceeded(IReadOnlyList<PumpCommandOutcome> outcomes)
    {
        for (int index = 0; index < outcomes.Count; index += 1)
        {
            if (!outcomes[index].Succeeded) return false;
        }

        return true;
    }
}
