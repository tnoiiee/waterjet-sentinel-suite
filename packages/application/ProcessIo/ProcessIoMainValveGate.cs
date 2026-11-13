using Wjss.Contracts;

namespace Wjss.Runtime.Core.ProcessIo;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the Main Valve gate (motorized ball valve, panel block 1.27).
//
// WHAT THIS OWNS
//   The travel of the Main Valve to OPEN before the Pump is allowed to energise, and back to CLOSED after a
//   confirmed Pump stop, plus the two supervision duties the Owner ruled for the over-torque contact. It is
//   a phase machine with no thread, no timer and no sleep: the composition or trial that composed it calls
//   Step() from its own already-bounded drive loop, exactly like the Pump lifecycle does.
//
// WHAT IT DELIBERATELY DOES NOT OWN
//   The Pump. No method here calls Stop, and no refusal here is ever allowed to delay or replace a Pump stop.
//   The one direction that matters is asymmetric on purpose: a valve fault can prevent the Pump from being
//   energised, and can never prevent it from being de-energised.
//
// WHY THE POSITION NEVER COMES FROM THE COILS
//   The rack's own contract states that an output read-back is process-image state and is not proof of
//   field-device actuation. Writing coil 13 and reading it back proves the coupler latched a bit. So the only
//   accepted proof that the valve arrived is its limit switch: DI-013 for OPEN, DI-014 for CLOSED, both read
//   through the session and its freshness, generation and provenance rules by way of the Pump boundary.
//
// WHY THE COIL IS RELEASED AND NOT HELD
//   The actuator is a two-winding motorized ball valve with end-of-travel limit switches, not a solenoid that
//   must be held. Holding a coil at a limit burns the winding, so the confirmed position releases the coil
//   that reached it, and both coils are released on any abort. Both coils True is never written by any path:
//   every travel clears the opposite coil first, in the legacy order the Owner supplied.

/// <summary>
/// The phase of a <see cref="ProcessIoMainValveGate"/>. The ordinals are stable and never renumbered: a
/// transcript that says <c>3</c> must keep meaning the same thing to a reader next year.
/// </summary>
public enum ProcessIoMainValveGatePhase
{
    /// <summary>Nothing has been read or written.</summary>
    IDLE = 0,

    /// <summary>The open coil is energised and the gate is waiting for the open limit within the timeout.</summary>
    ENSURING_OPEN = 1,

    /// <summary>DI-013 confirmed OPEN and the open coil was released.</summary>
    OPEN_CONFIRMED = 2,

    /// <summary>The close coil is energised and the gate is waiting for the closed limit within the timeout.</summary>
    ENSURING_CLOSED = 3,

    /// <summary>DI-014 confirmed CLOSED and the close coil was released. This is the resting state of a run.</summary>
    CLOSED_CONFIRMED = 4,

    /// <summary>A fault ended the travel. Both coils were released. Only a Pump stop follows this state.</summary>
    ABORTED = 5,
}

/// <summary>
/// The one place the Main Valve travel is decided. It reads position through the Pump boundary, writes only
/// the two valve coils, and refuses rather than assumes on every unreadable or contradictory input.
/// </summary>
public sealed class ProcessIoMainValveGate
{
    private readonly IPumpCommandBoundary _boundary;
    private readonly ProcessIoMainValveConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly List<PumpCommandOutcome> _writes = new();

    private DateTimeOffset _deadline;
    private DateTimeOffset _travelStartedAt;
    private DateTimeOffset _nextSupervisionAt;

    /// <summary>
    /// The shortest spacing between two supervision reads. The composition drives its Pump loop every
    /// <c>PumpDriveIntervalMilliseconds</c> = 5 ms, and each supervision costs three discrete reads, so an
    /// unguarded hook would put six hundred requests a second on the same coupler that is carrying the ramp.
    /// One hundred ms is ten times finer than the only thing this supervision can act on (the trip is a
    /// latched contact, not an edge) and it is the same pace the Pump command trial already polls at.
    /// </summary>
    private static readonly TimeSpan SupervisionInterval = TimeSpan.FromMilliseconds(100);

    public ProcessIoMainValveGate(
        IPumpCommandBoundary boundary,
        ProcessIoMainValveConfiguration configuration,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);

        _boundary = boundary;
        _time = time;

        if (!configuration.Valid)
        {
            throw new ArgumentException(
                PumpCommandRefusals.RefusedMainValveConfiguration
                + ": an enforced Main Valve gate needs both travel timeouts and cannot run without them.",
                nameof(configuration));
        }

        _configuration = configuration;
    }

    /// <summary>The configuration this gate was composed with, exposed so the caller can derive its own bound.</summary>
    public ProcessIoMainValveConfiguration Configuration => _configuration;

    /// <summary>The current phase.</summary>
    public ProcessIoMainValveGatePhase Phase { get; private set; } = ProcessIoMainValveGatePhase.IDLE;

    /// <summary>The refusal that ended the travel, or <c>OK</c>. Never rewritten after an abort.</summary>
    public string Code { get; private set; } = PumpCommandRefusals.Granted;

    /// <summary>How the valve came to be open: commanded by this product, already at the limit, or not enforced.</summary>
    public string OpenSource { get; private set; } = ProcessIoMainValve.OpenSourceNotEnforced;

    /// <summary>The last complete input read, so a reader can audit the raw bits behind any claim.</summary>
    public ProcessIoMainValveRead? LastPosition { get; private set; }

    /// <summary>The position as the very first read of the run found it.</summary>
    public ProcessIoMainValvePosition PositionAtStart { get; private set; } = ProcessIoMainValvePosition.UNKNOWN;

    /// <summary>The position at the end of the run, i.e. after the post-stop close was attempted.</summary>
    public ProcessIoMainValvePosition PositionAtEnd { get; private set; } = ProcessIoMainValvePosition.UNKNOWN;

    /// <summary>Every valve coil write in order, with the session's own admission and dispatch answers.</summary>
    public IReadOnlyList<PumpCommandOutcome> Writes => _writes;

    /// <summary>True when DI-012 was seen TRUE at any point in this run.</summary>
    public bool OverTorqueSeen { get; private set; }

    /// <summary>True when the position inputs became unreadable while the valve was expected to stay OPEN.</summary>
    public bool PositionLostDuringRun { get; private set; }

    /// <summary>True when the valve left the open limit during the run without this product commanding it.</summary>
    public bool LeftOpenDuringRun { get; private set; }

    /// <summary>The number of travel and supervision reads performed, so the wait is auditable.</summary>
    public int PositionReads { get; private set; }

    /// <summary>True while the open coil is believed energised.</summary>
    public bool OpenCoilEnergised { get; private set; }

    /// <summary>True while the close coil is believed energised.</summary>
    public bool CloseCoilEnergised { get; private set; }

    /// <summary>True when neither valve coil is believed energised, which is the required resting state.</summary>
    public bool CoilsResting => !OpenCoilEnergised && !CloseCoilEnergised;

    /// <summary>True when the pump may be energised: OPEN is confirmed and has been held.</summary>
    public bool AdmitsPumpEnergise => Phase == ProcessIoMainValveGatePhase.OPEN_CONFIRMED;

    /// <summary>True when the gate has reached a state its caller must not keep driving.</summary>
    public bool Settled => Phase
        is ProcessIoMainValveGatePhase.OPEN_CONFIRMED
        or ProcessIoMainValveGatePhase.CLOSED_CONFIRMED
        or ProcessIoMainValveGatePhase.ABORTED;

    /// <summary>How long the travel to OPEN took, measured from the dispatch to the confirming read.</summary>
    public TimeSpan OpenElapsed { get; private set; }

    /// <summary>How long the travel to CLOSED took, measured the same way.</summary>
    public TimeSpan CloseElapsed { get; private set; }

    /// <summary>
    /// Reads the position and, unless the valve is already proven OPEN, commands the travel to OPEN. Returns
    /// true when OPEN is already confirmed - including when the limit switch proved it before anything was
    /// written, which the Owner ruled as an admissible start - and false while the caller must drive
    /// <see cref="Step"/> until <see cref="Settled"/>. A refusal here means no Pump channel was touched.
    /// </summary>
    public bool BeginOpen(out string code)
    {
        code = PumpCommandRefusals.Granted;

        if (Phase == ProcessIoMainValveGatePhase.OPEN_CONFIRMED)
        {
            return true;
        }

        if (!_configuration.Enforced)
        {
            code = PumpCommandRefusals.RefusedMainValveConfiguration;
            return false;
        }

        if (Phase != ProcessIoMainValveGatePhase.IDLE)
        {
            code = Code;
            return false;
        }

        ProcessIoMainValveRead read = Read();
        PositionAtStart = read.Position;

        if (!read.Usable)
        {
            // Owner ruling 4 and 8: an unreadable position is a refusal, never a default to closed or open.
            return Fail(PumpCommandRefusals.RefusedMainValvePositionUnconfirmed, out code);
        }

        if (read.Position == ProcessIoMainValvePosition.CONFLICT)
        {
            return Fail(PumpCommandRefusals.RefusedMainValveFeedbackConflict, out code);
        }

        if (read.OverTorqueActive)
        {
            // Owner ruling 3: tripped at the pre-flight means the actuator is already fighting something, so
            // the start is refused without energising either coil. Nothing is released because nothing is hot.
            OverTorqueSeen = true;
            return Fail(PumpCommandRefusals.RefusedMainValveOverTorque, out code);
        }

        if (read.Position == ProcessIoMainValvePosition.OPEN)
        {
            Phase = ProcessIoMainValveGatePhase.OPEN_CONFIRMED;
            OpenSource = ProcessIoMainValve.OpenSourcePreConfirmed;
            return true;
        }

        // CLOSED or IN_TRANSIT. The legacy order is kept exactly: the close coil is cleared before the open
        // coil is energised, so a valve that was mid-travel toward closed is never bridged across both coils.
        Record(_boundary.SetMainValveCloseCoil(false));
        PumpCommandOutcome open = _boundary.SetMainValveOpenCoil(true);
        Record(open);
        if (!open.Dispatched)
        {
            return Fail(PumpCommandRefusals.RefusedMainValveDispatch, out code);
        }

        OpenSource = ProcessIoMainValve.OpenSourceProductDispatch;
        _travelStartedAt = _time.GetUtcNow();
        _deadline = _travelStartedAt + _configuration.OpenTimeout;
        Phase = ProcessIoMainValveGatePhase.ENSURING_OPEN;
        code = PumpCommandRefusals.Granted;
        return false;
    }

    /// <summary>
    /// Advances one travel step. Called from the composition's own bounded drive loop, never from a timer.
    /// It is a no-op in every resting phase, so a caller that keeps stepping after a confirmation costs one
    /// read per step and changes nothing.
    /// </summary>
    public void Step()
    {
        switch (Phase)
        {
            case ProcessIoMainValveGatePhase.ENSURING_OPEN:
                StepTravel(ProcessIoMainValvePosition.OPEN, PumpCommandRefusals.RefusedMainValveOpenTimeout);
                break;
            case ProcessIoMainValveGatePhase.ENSURING_CLOSED:
                StepTravel(ProcessIoMainValvePosition.CLOSED, PumpCommandRefusals.RefusedMainValveCloseTimeout);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// One travel step toward <paramref name="target"/>. A not-usable read is retried until the deadline and
    /// then refused with the position code, so a single glitched read never ends a travel and a travel is
    /// never held open by a read that keeps failing.
    /// </summary>
    private void StepTravel(ProcessIoMainValvePosition target, string timeoutCode)
    {
        ProcessIoMainValveRead read = Read();
        bool opening = target == ProcessIoMainValvePosition.OPEN;

        if (read.OverTorqueActive)
        {
            // Owner ruling 3: the trip during travel releases both coils at once. The Pump stop is the
            // caller's duty and is requested from the same loop that is stepping this gate.
            OverTorqueSeen = true;

            // Fail releases both coils itself, so the release is written once rather than once per path that
            // noticed the same trip.
            Fail(PumpCommandRefusals.RefusedMainValveOverTorque);
            return;
        }

        if (read.Position == target)
        {
            // The coil that reached its limit is released here and never held (Owner ruling 1).
            ReleaseCoils();

            if (opening)
            {
                OpenElapsed = _time.GetUtcNow() - _travelStartedAt;
                Phase = ProcessIoMainValveGatePhase.OPEN_CONFIRMED;
            }
            else
            {
                CloseElapsed = _time.GetUtcNow() - _travelStartedAt;
                Phase = ProcessIoMainValveGatePhase.CLOSED_CONFIRMED;
            }

            Code = PumpCommandRefusals.Granted;
            return;
        }

        if (read.Position == ProcessIoMainValvePosition.CONFLICT)
        {
            Fail(PumpCommandRefusals.RefusedMainValveFeedbackConflict);
            return;
        }

        if (!read.Usable && _time.GetUtcNow() < _deadline)
        {
            return;
        }

        if (_time.GetUtcNow() >= _deadline)
        {
            Fail(read.Usable
                ? timeoutCode
                : PumpCommandRefusals.RefusedMainValvePositionUnconfirmed);
            return;
        }
    }

    /// <summary>
    /// Commands the travel to CLOSED, and only after the Pump stop is confirmed (Owner ruling 7). Returns
    /// true when the valve is already proven CLOSED. A false return with
    /// <see cref="PumpCommandRefusals.RefusedMainValveCloseNotStopped"/> is not an abort: nothing was
    /// attempted, and the caller must retry once the stop is confirmed. It never delays that stop.
    /// </summary>
    public bool BeginClose(bool pumpStoppedConfirmed, out string code)
    {
        if (!pumpStoppedConfirmed)
        {
            code = PumpCommandRefusals.RefusedMainValveCloseNotStopped;
            return false;
        }

        code = PumpCommandRefusals.Granted;

        if (!_configuration.Enforced)
        {
            code = PumpCommandRefusals.RefusedMainValveConfiguration;
            return false;
        }

        if (Phase == ProcessIoMainValveGatePhase.CLOSED_CONFIRMED)
        {
            return true;
        }

        ProcessIoMainValveRead read = Read();

        if (!read.Usable)
        {
            return Fail(PumpCommandRefusals.RefusedMainValvePositionUnconfirmed, out code);
        }

        if (read.Position == ProcessIoMainValvePosition.CONFLICT)
        {
            return Fail(PumpCommandRefusals.RefusedMainValveFeedbackConflict, out code);
        }

        if (read.Position == ProcessIoMainValvePosition.CLOSED)
        {
            ReleaseCoils();
            Phase = ProcessIoMainValveGatePhase.CLOSED_CONFIRMED;
            Code = PumpCommandRefusals.Granted;
            return true;
        }

        Record(_boundary.SetMainValveOpenCoil(false));
        PumpCommandOutcome close = _boundary.SetMainValveCloseCoil(true);
        Record(close);
        if (!close.Dispatched)
        {
            return Fail(PumpCommandRefusals.RefusedMainValveDispatch, out code);
        }

        _travelStartedAt = _time.GetUtcNow();
        _deadline = _travelStartedAt + _configuration.CloseTimeout;
        Phase = ProcessIoMainValveGatePhase.ENSURING_CLOSED;
        code = PumpCommandRefusals.Granted;
        return false;
    }

    /// <summary>
    /// The duty owed while the Pump runs: the over-torque trip and the loss of the position inputs (Owner
    /// rulings 3 and 8). Returns true when the caller must enter the Pump stop path now. It is a no-op until
    /// OPEN has been confirmed, because a gate that never admitted the Pump has nothing left to supervise.
    /// </summary>
    public bool Supervise()
    {
        if (Phase != ProcessIoMainValveGatePhase.OPEN_CONFIRMED)
        {
            return false;
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (now < _nextSupervisionAt)
        {
            // The travel itself is bounded inside Step(); this bound only paces the poll, and it never lets a
            // trip wait longer than one interval to be seen.
            return false;
        }

        _nextSupervisionAt = now + SupervisionInterval;

        ProcessIoMainValveRead read = Read();

        if (read.OverTorqueActive)
        {
            // Both coils are resting at this point, because a confirmed position released the coil that got
            // there. Fail still walks the release, so a trip seen between two writes cannot leave one hot.
            OverTorqueSeen = true;
            Fail(PumpCommandRefusals.RefusedMainValveOverTorque);
            return true;
        }

        if (!read.Usable)
        {
            // Owner ruling 8: position feedback lost while pumping is a stop request, and the valve is not
            // touched further until the Pump stop is confirmed. Both coils are already resting, so the
            // release below writes nothing.
            PositionLostDuringRun = true;
            Fail(PumpCommandRefusals.RefusedMainValvePositionUnconfirmed);
            return true;
        }

        if (read.Position != ProcessIoMainValvePosition.OPEN)
        {
            // Someone or something else moved the valve. WJSS is the single writer (Owner ruling 5), so this
            // is reported as a failed expectation of the run rather than silently forgiven - and it is not an
            // extra auto-abort beyond what was ruled: the pump is already stopped by then in the only
            // sequence where this can appear after the ramp down.
            LeftOpenDuringRun = true;
        }

        return false;
    }

    private ProcessIoMainValveRead Read()
    {
        ProcessIoMainValveRead read = _boundary.ReadMainValvePosition();
        PositionReads += 1;
        LastPosition = read;

        // Recorded on every read, so what a run reports as its ending position is the position the last read
        // actually saw and not a phase label that could outlive the evidence.
        PositionAtEnd = read.Position;
        return read;
    }

    private void Record(PumpCommandOutcome outcome)
    {
        _writes.Add(outcome);

        bool openCoil = string.Equals(
            outcome.Tag, ProcessIoMainValve.OpenCommandTag, StringComparison.Ordinal);

        // A dispatched write sets the belief to the value that was written. A write the session refused keeps
        // a coil that was asked for and never proven released marked hot, so the release is retried by the
        // next path that runs rather than quietly forgotten.
        if (openCoil)
        {
            OpenCoilEnergised = outcome.Dispatched ? outcome.Value : OpenCoilEnergised || outcome.Value;
        }
        else
        {
            CloseCoilEnergised = outcome.Dispatched ? outcome.Value : CloseCoilEnergised || outcome.Value;
        }
    }

    /// <summary>
    /// Releases every coil that is believed energised. Both are written FALSE in one abort so the actuator is
    /// never left holding one winding, and nothing is written when both are already believed resting, which
    /// keeps the confirmed-position release to exactly the one coil that reached its limit.
    /// </summary>
    private void ReleaseCoils()
    {
        if (OpenCoilEnergised)
        {
            Record(_boundary.SetMainValveOpenCoil(false));
        }

        if (CloseCoilEnergised)
        {
            Record(_boundary.SetMainValveCloseCoil(false));
        }
    }

    private bool Fail(string refusal, out string code)
    {
        code = refusal;
        Phase = ProcessIoMainValveGatePhase.ABORTED;
        Code = refusal;

        // Owner ruling 3: a fault during travel releases both coils. Nothing is energised is not the same as
        // releasing both, so this writes only when something is believed hot, and the belief is what a reader
        // can audit in the write list.
        if (!CoilsResting)
        {
            Record(_boundary.SetMainValveOpenCoil(false));
            Record(_boundary.SetMainValveCloseCoil(false));
        }

        return false;
    }

    private void Fail(string refusal) => Fail(refusal, out _);
}
