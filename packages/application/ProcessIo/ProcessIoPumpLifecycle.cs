using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.ProcessIo;

// PUMP PLATFORM, PHASE 0.5C: the ONE Main Pump lifecycle.
//
// The lifecycle owns the Pump command ORDER, the selection pair, the analog ramp and the corrected Stop
// sequence. It is deterministic: every transition is driven by Step() against the injected clock, so the
// corrected Stop order is observable and each de-energisation happens exactly once. It never spawns a
// thread, never retries, never sleeps and never touches a raw word or the transport: all I/O goes through
// the typed boundary, which goes through the Process-Io session.
//
// SAFETY POSTURE
//   * A physical Pump command fails closed while either required feedback polarity is uncommissioned.
//   * The corrected Stop order is Run Pump FALSE then Enable Inverter FALSE, and it is the only order.
//   * A ramp failure NEVER skips either de-energisation.
//   * Ownership is released only after the Stop is confirmed, never before.
//   * A completion is never claimed while the stopped state is unknown.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. Physical Hardware has NOT been executed and is NOT
// verified. Production write authority is NOT granted, and nothing here is a physical verification claim.

/// <summary>One Pump Start request. The owner is typed and a Production owner is always refused.</summary>
public sealed record PumpLifecycleStartRequest
{
    /// <summary>Who is asking. A Production owner is always refused in this scope.</summary>
    public required PumpCommandOwnerKind Owner { get; init; }

    /// <summary>A stable identity for the owner, used to refuse a conflicting second owner.</summary>
    public required string OwnerId { get; init; }

    /// <summary>The Pump ordinal. Only 1 and 2 exist, and neither is a default.</summary>
    public int Selection { get; init; }
}

/// <summary>The typed Start verdict. Nothing here is a physical confirmation.</summary>
public sealed record PumpLifecycleStartOutcome(bool Started, string Code, PumpLifecycleState State);

/// <summary>
/// The typed Stop verdict at the moment Stop was requested. A Stop is a REQUEST: the de-energisations
/// run on the following Step calls, and <see cref="StopConfirmed"/> is only ever true once the
/// commissioned not-running feedback was observed.
/// </summary>
public sealed record PumpLifecycleStopOutcome(bool StopConfirmed, string Code, PumpLifecycleState State);

/// <summary>
/// The terminal record. The primary failure is kept SEPARATE from the cleanup faults, so a cleanup that
/// failed after a trip can never overwrite the trip that caused it.
/// </summary>
public sealed record PumpLifecycleResult
{
    public required PumpLifecycleState TerminalState { get; init; }

    /// <summary>The first failure state that caused termination. Null on a normal Stop.</summary>
    public PumpLifecycleState? PrimaryFailure { get; init; }

    /// <summary>Cleanup faults in the order they occurred. Never merged into the primary failure.</summary>
    public required IReadOnlyList<string> CleanupFaultCodes { get; init; }

    /// <summary>One of the <see cref="PumpCleanupStates"/> values.</summary>
    public required string CleanupState { get; init; }

    /// <summary>True only when the commissioned not-running feedback was observed.</summary>
    public required bool StoppedConfirmed { get; init; }

    /// <summary>Why the Stop path was entered. Evidence only.</summary>
    public required PumpStopReason StopReason { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// The Pump command ownership channel. Exactly one owner may hold it, and a Production owner can never
/// acquire it in this scope. Ownership is released only after a confirmed Stop.
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
    /// Acquires the channel for exactly one owner. A second acquisition while the channel is held is
    /// refused, whether by the same owner or another one.
    /// </summary>
    public bool TryAcquire(PumpCommandOwnerKind kind, string ownerId, out string code)
    {
        code = PumpCommandRefusals.Granted;

        if (kind == PumpCommandOwnerKind.NONE)
        {
            code = PumpCommandRefusals.RefusedConflictingOwner;
            return false;
        }

        if (kind == PumpCommandOwnerKind.PRODUCTION)
        {
            // Production write authority is not granted in this scope. This is a refusal, not a gate.
            code = PumpCommandRefusals.RefusedProductionOwner;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            code = PumpCommandRefusals.RefusedConflictingOwner;
            return false;
        }

        if (IsHeld)
        {
            code = PumpCommandRefusals.RefusedConflictingOwner;
            return false;
        }

        _kind = kind;
        _ownerId = ownerId;
        _generation += 1;
        return true;
    }

    /// <summary>Releases the channel. Only ever called after a confirmed Stop or a refused Start.</summary>
    public void Release()
    {
        _kind = PumpCommandOwnerKind.NONE;
        _ownerId = null;
    }
}

/// <summary>
/// The one Pump lifecycle. It is driven explicitly by <see cref="Step"/> so every transition is
/// deterministic and every wait is bounded by the injected clock.
/// </summary>
public sealed class ProcessIoPumpLifecycle
{
    private readonly IPumpCommandBoundary _boundary;
    private readonly PumpCommandOwnership _ownership;
    private readonly TimeProvider _time;
    private readonly SequencingPressureThresholds _thresholds;
    private readonly List<string> _cleanupFaultCodes = new();

    private DateTimeOffset _waitStartedAt;
    private DateTimeOffset _stoppedWaitStartedAt;
    private DateTimeOffset _nextRampAt;
    private PumpAnalogCommand _speedCommand = PumpAnalogCommand.SafeStopSentinel;
    private bool _stopRequested;
    private bool _runCleared;
    private bool _enableCleared;
    private bool _stopEntered;
    private PumpLifecycleState? _primaryFailure;
    private PumpStopReason _stopReason = PumpStopReason.NORMAL_COMPLETION;

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

        // Only PumpReadySetpointBar is consumed, by the existing strict SequencingPressure.PumpReady
        // rule. The Low and High band members belong to the Isolation Valve classifier and are
        // deliberately not restated: this lifecycle must not invent a Valve threshold.
        _thresholds = new SequencingPressureThresholds(PumpReadySetpointBar: configuration.PumpReadySetpointBar);
    }

    public PumpLifecycleConfiguration Configuration { get; }

    public PumpLifecycleState State { get; private set; } = PumpLifecycleState.IDLE;

    /// <summary>True while this lifecycle holds the Pump command channel.</summary>
    public bool OwnsPumpCommand => _ownership.IsHeld;

    /// <summary>
    /// The current typed analog command. Its kind is the engineering identity: the 0 mA safe-stop
    /// sentinel and the 4 mA live minimum share raw 0, so a raw word is never read back as a state.
    /// </summary>
    public PumpAnalogCommand SpeedCommand => _speedCommand;

    /// <summary>The current analog command in engineering milliamps, for evidence only.</summary>
    public double SpeedCommandMilliamps => _speedCommand.Milliamps;

    /// <summary>The terminal record, populated once the lifecycle terminates.</summary>
    public PumpLifecycleResult? Result { get; private set; }

    /// <summary>The first failure state, retained separately from the cleanup faults.</summary>
    public PumpLifecycleState? PrimaryFailure => _primaryFailure;

    /// <summary>Cleanup faults in occurrence order. Never merged into the primary failure.</summary>
    public IReadOnlyList<string> CleanupFaultCodes => _cleanupFaultCodes;

    /// <summary>
    /// True only while the Pump is at confirmed readiness, the channel is owned and no Stop has been
    /// requested. This is the single precondition a new Isolation Valve OPEN must consult.
    /// </summary>
    public bool AllowsValveOpen =>
        State == PumpLifecycleState.PUMP_READY
        && !_stopRequested
        && Result is null
        && _ownership.IsHeld;

    /// <summary>
    /// True only for a confirmed, complete Stop. An unknown stopped state, an incomplete cleanup or any
    /// failure terminal is never a safe completion.
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
    /// Admits the Start and performs the ordered Start commands: the selection pair, Enable Inverter
    /// TRUE, then Run Pump TRUE. Every refusal happens before the first write.
    /// </summary>
    public PumpLifecycleStartOutcome Start(PumpLifecycleStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Result is not null || PumpLifecycleStates.IsTerminal(State))
        {
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedTerminal, State);
        }

        if (State != PumpLifecycleState.IDLE)
        {
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedConcurrentLifecycle, State);
        }

        if (!Configuration.Valid)
        {
            return RefuseStart(PumpCommandRefusals.RefusedAnalogOutOfDomain);
        }

        // The commissioning gate. A physical Pump command fails closed while either required polarity is
        // uncommissioned: without both, Running and Fault cannot be interpreted, and a Start that cannot
        // be observed is not a Start the Product may issue.
        if (!Configuration.Feedback.IsCommissioned)
        {
            return RefuseStart(PumpFeedbackConfiguration.NotCommissionedCode);
        }

        if (!PumpCommandContract.IsValidSelection(request.Selection))
        {
            return RefuseStart(PumpCommandRefusals.RefusedInvalidSelection);
        }

        if (!_ownership.TryAcquire(request.Owner, request.OwnerId, out string ownershipCode))
        {
            return RefuseStart(ownershipCode);
        }

        // An active Fault refuses the Start outright. The Fault polarity is commissioned at this point,
        // so this is a real reading and not an assumption. Nothing was energised, so nothing is cleared.
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            _ownership.Release();
            Settle(PumpLifecycleState.FAULT_ACTIVE, PumpCleanupStates.Complete, stoppedConfirmed: false);
            return new PumpLifecycleStartOutcome(false, PumpCommandRefusals.RefusedNotReady, State);
        }

        // 1. Selection: the selected contactor is energised, then the other is de-energised.
        State = PumpLifecycleState.SELECTING_PUMP;
        IReadOnlyList<PumpCommandOutcome> selection = _boundary.SelectPump(request.Selection);
        if (selection.Count != 2 || !AllSucceeded(selection))
        {
            _primaryFailure = PumpLifecycleState.SELECTION_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, FirstFailureCode(selection), State);
        }

        State = PumpLifecycleState.PUMP_SELECTED;

        // 2. Enable Inverter, first.
        PumpCommandOutcome enable = _boundary.SetEnableInverter(true);
        if (!enable.Succeeded)
        {
            _primaryFailure = PumpLifecycleState.ENABLE_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, enable.Code, State);
        }

        State = PumpLifecycleState.ENABLE_DISPATCHED;

        // 3. Run Pump, second. Both commands are maintained and neither is ever a pulse.
        PumpCommandOutcome run = _boundary.SetRunPump(true);
        if (!run.Succeeded)
        {
            _primaryFailure = PumpLifecycleState.RUN_FAILED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return new PumpLifecycleStartOutcome(false, run.Code, State);
        }

        State = PumpLifecycleState.RUN_DISPATCHED;
        State = PumpLifecycleState.WAITING_FOR_RUNNING;
        _waitStartedAt = _time.GetUtcNow();
        return new PumpLifecycleStartOutcome(true, PumpCommandRefusals.Granted, State);
    }

    // ---------------------------------------------------------------- stop

    /// <summary>
    /// Requests the Stop path for a normal completion, a cancellation, a downstream Isolation Valve
    /// failure or any other external reason. The mechanism is always the corrected order. The
    /// de-energisations run on the following <see cref="Step"/> calls.
    /// </summary>
    public PumpLifecycleStopOutcome Stop(PumpStopReason reason)
    {
        if (Result is not null)
        {
            return new PumpLifecycleStopOutcome(
                Result.StoppedConfirmed, PumpCommandRefusals.RefusedTerminal, State);
        }

        if (State == PumpLifecycleState.IDLE)
        {
            return new PumpLifecycleStopOutcome(false, PumpCommandRefusals.RefusedNotAdmitted, State);
        }

        // Stop is always permitted. No application permissive may block it.
        EnterStopPath(reason);
        return new PumpLifecycleStopOutcome(false, PumpCommandRefusals.Granted, State);
    }

    /// <summary>Enters the one Stop path. Idempotent: a second entry never restarts it.</summary>
    private void EnterStopPath(PumpStopReason reason)
    {
        _stopRequested = true;
        _stopReason = reason;

        if (_stopEntered)
        {
            return;
        }

        _stopEntered = true;

        // The mechanism always begins by withdrawing the analog reference, whatever the reason is. A
        // command path that never raised the reference passes through this stage in one step.
        State = PumpLifecycleState.RAMPING_DOWN;
        _nextRampAt = _time.GetUtcNow();
    }

    // ---------------------------------------------------------------- step

    /// <summary>
    /// Advances the lifecycle by exactly one deterministic unit. At most one command write happens per
    /// step, so the corrected Stop order is observable and each de-energisation happens exactly once.
    /// </summary>
    public void Step()
    {
        if (Result is not null) return;
        if (State == PumpLifecycleState.IDLE) return;

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
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Current && running.Active)
        {
            // Running is taken from the commissioned feedback input only. It is never inferred from the
            // outlet pressure and never from the command that was just dispatched.
            State = PumpLifecycleState.RAMPING_OUTPUT;
            _speedCommand = PumpAnalogCommand.SafeStopSentinel;
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
        if (now < _nextRampAt) return;

        double target = Configuration.Ramp.TargetEngineeringMilliamps;
        if (PumpAnalogRamp.AtTarget(_speedCommand, target))
        {
            State = PumpLifecycleState.WAITING_FOR_AI003_READY;
            _waitStartedAt = now;
            return;
        }

        if (!PumpAnalogRamp.TryNext(_speedCommand, target, Configuration.Ramp.Step, out PumpAnalogCommand next))
        {
            _primaryFailure = PumpLifecycleState.COMMAND_ADMISSION_REFUSED;
            EnterStopPath(PumpStopReason.START_FAILURE);
            return;
        }

        PumpAnalogCommandOutcome outcome = _boundary.SetSpeedCommand(next);
        if (!outcome.Succeeded)
        {
            // The upward reference could not be dispatched. The cleanup still runs: a ramp failure never
            // prevents the two de-energisations.
            _primaryFailure = PumpLifecycleState.COMMAND_ADMISSION_REFUSED;
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
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpPressureRead pressure = _boundary.ReadPumpOutletPressure();
        PressureSample? sample = pressure.Sample;

        if (pressure.Accepted && sample is not null && SequencingPressure.PumpReady(sample, _thresholds))
        {
            State = PumpLifecycleState.PUMP_READY;
            return;
        }

        if (!pressure.Accepted || sample is null)
        {
            // A refused reading is never waited on: a stale, superseded or unusable sample is a refusal
            // and never a slow one, and the Pump Inlet row is never substituted for the Outlet row.
            _primaryFailure = pressure.Stale ? PumpLifecycleState.AI003_STALE : PumpLifecycleState.AI003_INVALID;
            EnterStopPath(pressure.Stale ? PumpStopReason.AI003_STALE : PumpStopReason.AI003_INVALID);
            return;
        }

        if (now - _waitStartedAt > Configuration.PumpReadyTimeout)
        {
            _primaryFailure = PumpLifecycleState.AI003_TIMEOUT;
            EnterStopPath(PumpStopReason.AI003_TIMEOUT);
        }
    }

    private void StepReady()
    {
        PumpFeedbackRead fault = _boundary.ReadFault();
        if (fault.Current && fault.Active)
        {
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpFeedbackRead running = _boundary.ReadRunning();
        if (running.Commissioned && running.Usable && !running.Active)
        {
            // The Pump stopped without being told to. That is a trip, not a completion.
            _primaryFailure = PumpLifecycleState.FAULT_ACTIVE;
            EnterStopPath(PumpStopReason.FAULT);
            return;
        }

        PumpPressureRead pressure = _boundary.ReadPumpOutletPressure();
        PressureSample? sample = pressure.Sample;

        if (!pressure.Accepted || sample is null)
        {
            _primaryFailure = PumpLifecycleState.AI003_INVALID;
            EnterStopPath(PumpStopReason.AI003_INVALID);
            return;
        }

        if (!SequencingPressure.PumpReady(sample, _thresholds))
        {
            _primaryFailure = PumpLifecycleState.AI003_STALE;
            EnterStopPath(PumpStopReason.AI003_STALE);
        }
    }

    private void StepRampingDown(DateTimeOffset now)
    {
        if (now < _nextRampAt) return;

        double safeZero = Configuration.Ramp.SafeZeroEngineeringCommand;
        if (PumpAnalogRamp.AtTarget(_speedCommand, safeZero))
        {
            // The down-ramp converged on the EXPLICIT 0 mA sentinel. Raw 0 alone is not the proof.
            State = PumpLifecycleState.CLEARING_RUN;
            return;
        }

        if (!PumpAnalogRamp.TryNext(_speedCommand, safeZero, Configuration.Ramp.Step, out PumpAnalogCommand next))
        {
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

        PumpCommandOutcome clearRun = _boundary.SetRunPump(false);
        if (!clearRun.Succeeded)
        {
            RecordCleanupFault(PumpLifecycleState.CLEAR_RUN_FAILED);
        }

        // Marked regardless, so a failed write is never re-issued: exactly one attempt, never a retry.
        _runCleared = true;
        State = PumpLifecycleState.DISABLING_INVERTER;
    }

    private void StepDisablingInverter()
    {
        if (_enableCleared)
        {
            State = PumpLifecycleState.WAITING_FOR_STOPPED;
            _stoppedWaitStartedAt = _time.GetUtcNow();
            return;
        }

        PumpCommandOutcome clearEnable = _boundary.SetEnableInverter(false);
        if (!clearEnable.Succeeded)
        {
            RecordCleanupFault(PumpLifecycleState.DISABLE_FAILED);
        }

        _enableCleared = true;
        State = PumpLifecycleState.WAITING_FOR_STOPPED;
        _stoppedWaitStartedAt = _time.GetUtcNow();
    }

    private void StepWaitingForStopped(DateTimeOffset now)
    {
        PumpFeedbackRead running = _boundary.ReadRunning();
        bool stoppedConfirmed = running.Commissioned && running.Usable && !running.Active;

        if (stoppedConfirmed)
        {
            Settle(
                _primaryFailure ?? PumpLifecycleState.STOPPED,
                _cleanupFaultCodes.Count > 0 ? PumpCleanupStates.Incomplete : PumpCleanupStates.Complete,
                stoppedConfirmed: true);
            return;
        }

        if (now - _stoppedWaitStartedAt > Configuration.StoppedFeedbackTimeout)
        {
            // The stopped state is unknown. Completion is never reported and the channel is not released.
            RecordCleanupFault(PumpLifecycleState.STOPPED_FEEDBACK_TIMEOUT);
            Settle(
                PumpLifecycleState.PHYSICAL_ABORT_REQUIRED,
                PumpCleanupStates.PhysicalAbortRequired,
                stoppedConfirmed: false);
        }
    }

    // ---------------------------------------------------------------- settle

    private void Settle(PumpLifecycleState terminal, string cleanupState, bool stoppedConfirmed)
    {
        State = terminal;
        Result = new PumpLifecycleResult
        {
            TerminalState = terminal,
            PrimaryFailure = _primaryFailure,
            CleanupFaultCodes = _cleanupFaultCodes.ToArray(),
            CleanupState = cleanupState,
            StoppedConfirmed = stoppedConfirmed,
            StopReason = _stopReason,
            CompletedAt = _time.GetUtcNow(),
        };

        if (stoppedConfirmed)
        {
            // Ownership is released only after the Stop is confirmed, never before.
            _ownership.Release();
        }
    }

    // ---------------------------------------------------------------- helpers

    private PumpLifecycleStartOutcome RefuseStart(string code)
    {
        // Nothing was energised, so there is nothing to clean up. The channel is never held.
        _primaryFailure = PumpLifecycleState.COMMAND_ADMISSION_REFUSED;
        _ownership.Release();
        Settle(PumpLifecycleState.COMMAND_ADMISSION_REFUSED, PumpCleanupStates.Complete, stoppedConfirmed: false);
        return new PumpLifecycleStartOutcome(false, code, State);
    }

    private void RecordCleanupFault(PumpLifecycleState fault) => _cleanupFaultCodes.Add(fault.ToString());

    private static bool AllSucceeded(IReadOnlyList<PumpCommandOutcome> outcomes)
    {
        for (int index = 0; index < outcomes.Count; index += 1)
        {
            if (!outcomes[index].Succeeded) return false;
        }

        return true;
    }

    private static string FirstFailureCode(IReadOnlyList<PumpCommandOutcome> outcomes)
    {
        for (int index = 0; index < outcomes.Count; index += 1)
        {
            if (!outcomes[index].Succeeded) return outcomes[index].Code;
        }

        return PumpCommandRefusals.RefusedInvalidSelection;
    }
}
