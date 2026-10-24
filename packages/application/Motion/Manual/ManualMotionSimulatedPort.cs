using Wjss.Time;

namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// Deterministic scenario options of the simulated port. Every value is synthetic. The options are
/// fixed at construction (there is no runtime switch an Operator could reach), so a scenario is
/// reproducible and a refusal, timeout, stale-result or fault path can be exercised on purpose.
/// </summary>
public sealed class ManualMotionSimulationOptions
{
    /// <summary>The synthetic start position of Axis A, in simulated counts.</summary>
    public decimal StartPositionA { get; init; } = 12000m;

    /// <summary>The synthetic start position of Axis B, in simulated counts.</summary>
    public decimal StartPositionB { get; init; } = -8000m;

    /// <summary>The synthetic start position of Axis C, in simulated counts.</summary>
    public decimal StartPositionC { get; init; } = 5000m;

    /// <summary>The synthetic start position of Axis D, in simulated counts.</summary>
    public decimal StartPositionD { get; init; } = -3000m;

    /// <summary>The synthetic physical position of the simulated home switch of every axis.</summary>
    public decimal HomeSwitchPosition { get; init; }

    /// <summary>Half the width of the simulated home switch window.</summary>
    public decimal HomeSwitchHalfWidth { get; init; } = 40m;

    /// <summary>When false, the simulated home sequence never reaches the switch (a Home timeout scenario).</summary>
    public bool HomeReachable { get; init; } = true;

    /// <summary>When true, the simulated Stop does not stop motion (a Stop-not-observed scenario).</summary>
    public bool StopIgnored { get; init; }

    /// <summary>When true, the simulated zero definition is acknowledged but changes nothing (a zero-verification-failure scenario).</summary>
    public bool ZeroDefinitionIgnored { get; init; }

    /// <summary>A command kind that reports a simulated communication fault.</summary>
    public ManualMotionPortCommandKind? FaultOnCommand { get; init; }

    /// <summary>A command kind that the simulated controller refuses.</summary>
    public ManualMotionPortCommandKind? RefuseCommand { get; init; }

    /// <summary>A read kind whose reads are refused (no value).</summary>
    public ManualMotionReadKind? RefuseReadKind { get; init; }

    /// <summary>After this many successful reads, every further read reports a simulated communication fault.</summary>
    public int? FaultReadAfterCount { get; init; }

    /// <summary>After this many port calls, the port generation advances so later results are stale for the session.</summary>
    public int? InvalidateGenerationAfterCalls { get; init; }

    /// <summary>Optional per-axis overrides for Axis A; null keeps the global option behavior.</summary>
    public ManualMotionAxisSimulationOptions? AxisA { get; init; }

    /// <summary>Optional per-axis overrides for Axis B; null keeps the global option behavior.</summary>
    public ManualMotionAxisSimulationOptions? AxisB { get; init; }

    /// <summary>Optional per-axis overrides for Axis C; null keeps the global option behavior.</summary>
    public ManualMotionAxisSimulationOptions? AxisC { get; init; }

    /// <summary>Optional per-axis overrides for Axis D; null keeps the global option behavior.</summary>
    public ManualMotionAxisSimulationOptions? AxisD { get; init; }
}

/// <summary>
/// Narrow per-axis fault-injection options for the simulated port (Phase 3B simulation scenarios).
/// Every member is optional and simulation-only: an absent member keeps the global option behavior,
/// and no value here is ever a commissioned value — the record is synthetic test data only.
/// </summary>
public sealed class ManualMotionAxisSimulationOptions
{
    /// <summary>When true, a typed Stop for this axis is accepted but ignored (the axis keeps moving).</summary>
    public bool StopIgnored { get; init; }

    /// <summary>When set, a command of this kind dispatched to this axis reports a simulated fault.</summary>
    public ManualMotionPortCommandKind? FaultOnCommand { get; init; }

    /// <summary>When set, a command of this kind dispatched to this axis is refused (not a fault).</summary>
    public ManualMotionPortCommandKind? RefuseCommand { get; init; }

    /// <summary>When true, reads of this axis report a simulated fault while a Position Move is engaged on it (never during Home or other lifecycles).</summary>
    public bool FaultOnReadWhileMoving { get; init; }

    /// <summary>When set, reads of this kind on this axis are refused while a Position Move is engaged on it (previous values go stale).</summary>
    public ManualMotionReadKind? RefuseReadKindWhileMoving { get; init; }

    /// <summary>When true, this axis never physically advances during a Position Move (a mechanical stall); supervision times out. Home and Jog are unaffected.</summary>
    public bool MotionStalled { get; init; }

    /// <summary>Synthetic RP divergence added to this axis's RP reads only (TP remains the physical position).</summary>
    public decimal RpBias { get; init; }
}

/// <summary>
/// The SIMULATED Manual Motion Commissioning port. It loads no native library, opens no controller,
/// touches no network and drives no output: every behaviour is an in-process, deterministic model
/// advanced by the injected clock. A simulated result is never hardware evidence.
///
/// Model: each axis has a synthetic physical position and a coordinate offset (the reported position
/// is physical minus offset). Motion progresses only while the simulated brake output is in the
/// release state the profile names. Begin starts the motion armed by the previous jog-speed,
/// absolute-target or home command. The simulated home switch sits at a fixed physical position. The
/// status word is composed from the profile's Owner-attested bit layout (or the synthetic built-in
/// layout), so the session interprets it exactly as it would interpret the hardware-capable port.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class ManualMotionSimulatedPort : IManualMotionPort
{
    /// <summary>The simulated port opened.</summary>
    public const string CodeOpened = "SIMULATED_PORT_OPENED";

    /// <summary>A second open was refused: one open per port, nothing reconnects.</summary>
    public const string CodeOpenAlreadyPerformed = "SIMULATED_OPEN_ALREADY_PERFORMED";

    /// <summary>The simulated command was accepted.</summary>
    public const string CodeDispatched = "SIMULATED_DISPATCHED";

    /// <summary>The simulated controller refused the command.</summary>
    public const string CodeRefused = "SIMULATED_CONTROLLER_REFUSED";

    /// <summary>A simulated communication fault.</summary>
    public const string CodeFault = "SIMULATED_COMMUNICATION_FAULT";

    /// <summary>The simulated port is not open (before the open or after the close).</summary>
    public const string CodeNotOpen = "SIMULATED_PORT_NOT_OPEN";

    /// <summary>The simulated read produced a value.</summary>
    public const string CodeRead = "SIMULATED_READ";

    /// <summary>The simulated read was refused.</summary>
    public const string CodeReadRefused = "SIMULATED_READ_REFUSED";

    /// <summary>The simulated port closed.</summary>
    public const string CodeClosed = "SIMULATED_PORT_CLOSED";

    private const int SyntheticMotionBit = 6;
    private const int SyntheticHomeBit = 3;

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly ManualMotionSimulationOptions _options;
    private readonly SimulatedAxis _axisA;
    private readonly SimulatedAxis _axisB;
    private readonly SimulatedAxis _axisC;
    private readonly SimulatedAxis _axisD;
    private readonly int[] _outputs = new int[5];
    private readonly List<ManualMotionPortCommand> _executed = new();

    private bool _opened;
    private bool _closed;
    private int _openCount;
    private int _closeCount;
    private int _callCount;
    private int _successfulReads;
    private long _generation;

    /// <summary>Creates a simulated port whose bit layout and brake polarity follow the effective profile.</summary>
    public ManualMotionSimulatedPort(IClock clock, ManualMotionProfile profile, ManualMotionSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(profile);

        _clock = clock;
        _options = options ?? new ManualMotionSimulationOptions();
        _axisA = new SimulatedAxis(profile.AxisA, 1, _options.StartPositionA, _options.AxisA);
        _axisB = new SimulatedAxis(profile.AxisB, 2, _options.StartPositionB, _options.AxisB);
        _axisC = new SimulatedAxis(profile.AxisC, 3, _options.StartPositionC, _options.AxisC);
        _axisD = new SimulatedAxis(profile.AxisD, 4, _options.StartPositionD, _options.AxisD);

        // Every simulated brake starts in its APPLY state, for either release polarity: with a
        // clear-output release the apply state is the SET output, so a zeroed output array would
        // otherwise model an already-released brake and let motion progress before any release.
        _outputs[_axisA.BrakeOutput] = _axisA.ReleaseAction == ManualMotionBrakeReleaseAction.SetOutput ? 0 : 1;
        _outputs[_axisB.BrakeOutput] = _axisB.ReleaseAction == ManualMotionBrakeReleaseAction.SetOutput ? 0 : 1;
        _outputs[_axisC.BrakeOutput] = _axisC.ReleaseAction == ManualMotionBrakeReleaseAction.SetOutput ? 0 : 1;
        _outputs[_axisD.BrakeOutput] = _axisD.ReleaseAction == ManualMotionBrakeReleaseAction.SetOutput ? 0 : 1;
    }

    /// <inheritdoc/>
    public ManualMotionPortMode Mode => ManualMotionPortMode.Simulated;

    /// <summary>How many times <see cref="Open"/> was called.</summary>
    public int OpenCount
    {
        get
        {
            lock (_gate)
            {
                return _openCount;
            }
        }
    }

    /// <summary>How many times <see cref="Close"/> was called.</summary>
    public int CloseCount
    {
        get
        {
            lock (_gate)
            {
                return _closeCount;
            }
        }
    }

    /// <summary>A copy of every typed command executed while open, in order.</summary>
    public IReadOnlyList<ManualMotionPortCommand> ExecutedCommands
    {
        get
        {
            lock (_gate)
            {
                return _executed.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ManualMotionPortOpenResult Open()
    {
        lock (_gate)
        {
            _openCount++;
            if (_opened || _closed)
            {
                return new ManualMotionPortOpenResult(false, CodeOpenAlreadyPerformed, _generation);
            }

            _opened = true;
            _generation = 1;
            var now = _clock.UtcNow;
            _axisA.LastUpdate = now;
            _axisB.LastUpdate = now;
            _axisC.LastUpdate = now;
            _axisD.LastUpdate = now;
            return new ManualMotionPortOpenResult(true, CodeOpened, _generation);
        }
    }

    /// <inheritdoc/>
    public ManualMotionPortCommandResult Execute(ManualMotionPortCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_gate)
        {
            if (!_opened || _closed)
            {
                return new ManualMotionPortCommandResult(false, false, false, CodeNotOpen, _generation);
            }

            Advance(_clock.UtcNow);
            CountCall();
            _executed.Add(command);

            var axisOptions = AxisOptionsOf(command.Axis);
            if (axisOptions?.FaultOnCommand is ManualMotionPortCommandKind axisFault && axisFault == command.Kind)
            {
                return new ManualMotionPortCommandResult(false, true, true, CodeFault, _generation);
            }

            if (axisOptions?.RefuseCommand is ManualMotionPortCommandKind axisRefuse && axisRefuse == command.Kind)
            {
                return new ManualMotionPortCommandResult(false, false, true, CodeRefused, _generation);
            }

            if (_options.FaultOnCommand == command.Kind)
            {
                return new ManualMotionPortCommandResult(false, true, true, CodeFault, _generation);
            }

            if (_options.RefuseCommand == command.Kind)
            {
                return new ManualMotionPortCommandResult(false, false, true, CodeRefused, _generation);
            }

            var accepted = Apply(command);
            return new ManualMotionPortCommandResult(accepted, false, true, accepted ? CodeDispatched : CodeRefused, _generation);
        }
    }

    /// <inheritdoc/>
    public ManualMotionPortReadResult Read(ManualMotionPortRead read)
    {
        ArgumentNullException.ThrowIfNull(read);

        lock (_gate)
        {
            if (!_opened || _closed)
            {
                return new ManualMotionPortReadResult(false, false, 0m, CodeNotOpen, _generation);
            }

            Advance(_clock.UtcNow);
            CountCall();

            var axisOptions = AxisOptionsOf(read.Axis);
            if (axisOptions?.FaultOnReadWhileMoving == true && AxisOf(read.Axis)?.PositionMoveEngaged == true)
            {
                return new ManualMotionPortReadResult(false, true, 0m, CodeFault, _generation);
            }

            if (axisOptions?.RefuseReadKindWhileMoving is ManualMotionReadKind axisRefuse
                && axisRefuse == read.Kind
                && AxisOf(read.Axis)?.PositionMoveEngaged == true)
            {
                return new ManualMotionPortReadResult(false, false, 0m, CodeReadRefused, _generation);
            }

            if (_options.FaultReadAfterCount is int limit && _successfulReads >= limit)
            {
                return new ManualMotionPortReadResult(false, true, 0m, CodeFault, _generation);
            }

            if (_options.RefuseReadKind == read.Kind)
            {
                return new ManualMotionPortReadResult(false, false, 0m, CodeReadRefused, _generation);
            }

            if (!TryReadValue(read, out var value))
            {
                return new ManualMotionPortReadResult(false, false, 0m, CodeReadRefused, _generation);
            }

            _successfulReads++;
            return new ManualMotionPortReadResult(true, false, value, CodeRead, _generation);
        }
    }

    /// <inheritdoc/>
    public ManualMotionPortCloseResult Close()
    {
        lock (_gate)
        {
            _closeCount++;
            _closed = true;
            _generation++;
            return new ManualMotionPortCloseResult(true, true, true, CodeClosed);
        }
    }

    private void CountCall()
    {
        _callCount++;
        if (_options.InvalidateGenerationAfterCalls is int calls && _callCount == calls)
        {
            _generation++;
        }
    }

    private SimulatedAxis? AxisOf(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => _axisA,
        ManualMotionAxis.B => _axisB,
        ManualMotionAxis.C => _axisC,
        ManualMotionAxis.D => _axisD,
        _ => null,
    };

    private ManualMotionAxisSimulationOptions? AxisOptionsOf(ManualMotionAxis axis) => AxisOf(axis)?.Options;

    /// <summary>
    /// Replaces (or clears) the per-axis simulation options of one axis. Simulation-only delayed
    /// activation seam: a test double may run Open and Home with the default behavior and activate
    /// Phase 3B scenario options afterwards, before the intended Position Move requests. Absent
    /// per-axis options preserve every existing behavior; nothing here is ever a commissioned
    /// value and no Hardware or native path is touched.
    /// </summary>
    public void ConfigureAxisSimulationOptions(ManualMotionAxis axis, ManualMotionAxisSimulationOptions? options)
    {
        var simulatedAxis = AxisOf(axis);
        if (simulatedAxis is null)
        {
            return;
        }

        simulatedAxis.Options = options;
        simulatedAxis.PositionMoveEngaged = false;
    }

    private bool StopIgnoredFor(ManualMotionAxis axis) =>
        AxisOptionsOf(axis)?.StopIgnored == true || _options.StopIgnored;

    private bool Apply(ManualMotionPortCommand command)
    {
        var axis = AxisOf(command.Axis);
        if (axis is null)
        {
            return false;
        }

        switch (command.Kind)
        {
            case ManualMotionPortCommandKind.BrakeOutputSet:
            case ManualMotionPortCommandKind.BrakeOutputClear:
                if (command.BrakeOutput is not (1 or 2 or 3 or 4))
                {
                    return false;
                }

                _outputs[command.BrakeOutput] = command.Kind == ManualMotionPortCommandKind.BrakeOutputSet ? 1 : 0;
                return true;

            case ManualMotionPortCommandKind.JogSpeed:
                if (command.Operand == 0)
                {
                    return false;
                }

                axis.JogSpeed = command.Operand;
                axis.Armed = SimulatedMode.Jog;
                axis.PositionMoveEngaged = false;
                return true;

            case ManualMotionPortCommandKind.Begin:
                return Begin(axis);

            case ManualMotionPortCommandKind.Stop:
                if (!StopIgnoredFor(command.Axis))
                {
                    axis.Mode = SimulatedMode.Idle;
                    axis.Armed = SimulatedMode.Idle;
                    axis.PositionMoveEngaged = false;
                }

                return true;

            case ManualMotionPortCommandKind.HomeSpeed:
                axis.HomeSpeed = command.Operand;
                axis.PositionMoveEngaged = false;
                return command.Operand > 0;

            case ManualMotionPortCommandKind.Home:
                axis.Armed = SimulatedMode.Home;
                axis.PositionMoveEngaged = false;
                return true;

            case ManualMotionPortCommandKind.DefinePositionZero:
                if (axis.Mode != SimulatedMode.Idle)
                {
                    return false;
                }

                if (!_options.ZeroDefinitionIgnored)
                {
                    axis.Offset = axis.Physical;
                }

                axis.PositionMoveEngaged = false;
                return true;

            case ManualMotionPortCommandKind.DefineDualZero:
                axis.PositionMoveEngaged = false;
                return axis.Mode == SimulatedMode.Idle;

            case ManualMotionPortCommandKind.Acceleration:
            case ManualMotionPortCommandKind.Deceleration:
                return command.Operand > 0;

            case ManualMotionPortCommandKind.Speed:
                axis.Speed = command.Operand;
                return command.Operand > 0;

            case ManualMotionPortCommandKind.PositionAbsolute:
                axis.PendingTarget = command.Operand;
                axis.Armed = SimulatedMode.Position;
                return true;

            default:
                return false;
        }
    }

    private bool Begin(SimulatedAxis axis)
    {
        if (axis.Mode != SimulatedMode.Idle)
        {
            return false;
        }

        switch (axis.Armed)
        {
            case SimulatedMode.Jog:
                axis.Mode = SimulatedMode.Jog;
                axis.PositionMoveEngaged = false;
                break;
            case SimulatedMode.Position:
                axis.PhysicalTarget = axis.PendingTarget + axis.Offset;
                axis.Mode = SimulatedMode.Position;
                axis.PositionMoveEngaged = true;
                break;
            case SimulatedMode.Home:
                axis.PhysicalTarget = _options.HomeSwitchPosition;
                axis.Mode = SimulatedMode.Home;
                axis.PositionMoveEngaged = false;
                break;
            default:
                return false;
        }

        axis.Armed = SimulatedMode.Idle;
        return true;
    }

    private void Advance(DateTimeOffset now)
    {
        AdvanceAxis(_axisA, now);
        AdvanceAxis(_axisB, now);
        AdvanceAxis(_axisC, now);
        AdvanceAxis(_axisD, now);
    }

    private void AdvanceAxis(SimulatedAxis axis, DateTimeOffset now)
    {
        var elapsed = now - axis.LastUpdate;
        axis.LastUpdate = now;
        if (elapsed <= TimeSpan.Zero || axis.Mode == SimulatedMode.Idle || !BrakeReleased(axis))
        {
            return;
        }

        // A per-axis mechanical stall of a Position Move: the axis is commanded and reported busy,
        // but never physically advances, so every completion condition fails closed and supervision
        // times out. Home and Jog progression are never affected.
        if (axis.Options?.MotionStalled == true && axis.Mode == SimulatedMode.Position)
        {
            return;
        }

        var seconds = elapsed.Ticks / (decimal)TimeSpan.TicksPerSecond;
        switch (axis.Mode)
        {
            case SimulatedMode.Jog:
                axis.Physical += axis.JogSpeed * seconds;
                return;

            case SimulatedMode.Position:
                MoveToward(axis, axis.PhysicalTarget, Math.Abs(axis.Speed) * seconds);
                return;

            case SimulatedMode.Home:
                if (_options.HomeReachable)
                {
                    MoveToward(axis, axis.PhysicalTarget, Math.Abs(axis.HomeSpeed) * seconds);
                }
                else
                {
                    // The switch is never found: travel continues away from it until a Stop.
                    var away = axis.Physical >= _options.HomeSwitchPosition ? 1m : -1m;
                    axis.Physical += away * Math.Abs(axis.HomeSpeed) * seconds;
                }

                return;

            default:
                return;
        }
    }

    private static void MoveToward(SimulatedAxis axis, decimal target, decimal step)
    {
        var distance = target - axis.Physical;
        if (Math.Abs(distance) <= step)
        {
            axis.Physical = target;
            axis.Mode = SimulatedMode.Idle;
            return;
        }

        axis.Physical += Math.Sign(distance) * step;
    }

    private bool BrakeReleased(SimulatedAxis axis)
    {
        var state = _outputs[axis.BrakeOutput];
        return axis.ReleaseAction == ManualMotionBrakeReleaseAction.SetOutput ? state == 1 : state == 0;
    }

    private bool TryReadValue(ManualMotionPortRead read, out decimal value)
    {
        var axis = AxisOf(read.Axis);
        if (axis is null)
        {
            value = 0m;
            return false;
        }

        switch (read.Kind)
        {
            case ManualMotionReadKind.StatusWord:
                value = StatusWord(axis);
                return true;

            case ManualMotionReadKind.PositionTp:
                value = Math.Round(axis.Physical - axis.Offset, 0, MidpointRounding.ToZero);
                return true;

            case ManualMotionReadKind.PositionRp:
                // The optional per-axis bias makes RP disagree with TP deterministically (a TP/RP
                // contradiction scenario); with no bias RP equals TP exactly as before.
                value = Math.Round(axis.Physical - axis.Offset, 0, MidpointRounding.ToZero)
                    + (axis.Options?.RpBias ?? 0m);
                return true;

            case ManualMotionReadKind.BrakeOutputState:
                if (read.BrakeOutput is not (1 or 2 or 3 or 4))
                {
                    value = 0m;
                    return false;
                }

                value = _outputs[read.BrakeOutput];
                return true;

            default:
                value = 0m;
                return false;
        }
    }

    private decimal StatusWord(SimulatedAxis axis)
    {
        var moving = axis.Mode != SimulatedMode.Idle;
        var homeActive = Math.Abs(axis.Physical - _options.HomeSwitchPosition) <= _options.HomeSwitchHalfWidth;
        var motionValue = moving ? axis.MotionActiveValue : 1 - axis.MotionActiveValue;
        var homeValue = homeActive ? axis.HomeActiveValue : 1 - axis.HomeActiveValue;
        return (motionValue << axis.MotionBit) | (homeValue << axis.HomeBit);
    }

    private enum SimulatedMode
    {
        Idle = 0,
        Jog = 1,
        Position = 2,
        Home = 3,
    }

    private sealed class SimulatedAxis
    {
        public SimulatedAxis(
            ManualMotionAxisProfile? profile,
            int defaultBrakeOutput,
            decimal startPosition,
            ManualMotionAxisSimulationOptions? options = null)
        {
            Options = options;
            BrakeOutput = profile?.BrakeOutput ?? defaultBrakeOutput;
            ReleaseAction = profile?.BrakeReleaseAction ?? ManualMotionBrakeReleaseAction.SetOutput;
            MotionBit = profile?.MotionStatusBit ?? SyntheticMotionBit;
            MotionActiveValue = profile?.MotionActiveValue ?? 1;
            var homeBit = profile?.HomeStatusBit ?? SyntheticHomeBit;
            HomeBit = homeBit == MotionBit ? (MotionBit + 1) & 7 : homeBit;
            HomeActiveValue = profile?.HomeActiveValue ?? 1;
            Physical = startPosition;
        }

        public ManualMotionAxisSimulationOptions? Options { get; set; }

        /// <summary>
        /// True from the Begin of a Position Move until an effective Stop, Home, Jog or zero
        /// definition re-primitives the axis: the Phase 3B read options apply to the whole Move
        /// operation (including a parked-but-unverified state), never to Home or other lifecycles.
        /// </summary>
        public bool PositionMoveEngaged { get; set; }

        public int BrakeOutput { get; }

        public ManualMotionBrakeReleaseAction ReleaseAction { get; }

        public int MotionBit { get; }

        public int MotionActiveValue { get; }

        public int HomeBit { get; }

        public int HomeActiveValue { get; }

        public decimal Physical { get; set; }

        public decimal Offset { get; set; }

        public SimulatedMode Mode { get; set; }

        public SimulatedMode Armed { get; set; }

        public int JogSpeed { get; set; }

        public int HomeSpeed { get; set; }

        public int Speed { get; set; }

        public int PendingTarget { get; set; }

        public decimal PhysicalTarget { get; set; }

        public DateTimeOffset LastUpdate { get; set; }
    }
}
