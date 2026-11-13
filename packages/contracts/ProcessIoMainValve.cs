namespace Wjss.Contracts;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the typed Main Valve contract (motorized ball valve, panel block 1.27).
//
// AUTHORITY (exact, in this order)
//   1. The latest Owner decision (2026-11-19, ten rulings) is the authority for the command order, the
//      limit-switch semantics, the timeout defaults, the over-torque behaviour, the single-writer rule,
//      the pre-start admission, the post-stop close and the fail-closed behaviour on unreadable feedback.
//   2. The adopted binding table (ProcessIoBindings, sourced from the Owner's T8 I/O mapping) is the
//      authority for every tag, area and address. Nothing here restates an address: the open/close coils
//      and the three discrete inputs are resolved through PumpCommandContract.TryCoilAddress and
//      PumpCommandContract.TryDiscreteInputAddress at call time, so one row can never disagree with another.
//   3. The authoritative workbook note for this trio is recorded in ProcessIoMainValve below, including the
//      one known label discrepancy the Owner ruled to be a documentation matter only.
//
// WHAT THIS TYPE SET IS FOR
//   The Main Valve is a two-solenoid motorized actuator (open coil, close coil) with two end-of-travel
//   limit switches and an over-torque trip. It is NOT an Isolation Valve: it has no one-shot open request,
//   no timed hold and no seat-prove pulse, and it is therefore never routed through the motion equipment
//   boundary. It is commanded through the same one use-authorized Process-Io write admission as the Pump
//   channels, and its position is proven by the limit switches only.
//
// WHY THE LIMIT SWITCHES ARE THE ONLY POSITION EVIDENCE
//   The rack's own contract states that an output read-back is process-image state and never proof of
//   field-device actuation. Reading coil 13 back after writing it says the coupler latched the bit, not
//   that the valve turned. So Position is derived from DI-013 and DI-014 (FC02) and from nothing else.
//
// THE COIL ORDER IS NOT COSMETIC
//   Both coils True at the same time fights the actuator's own motor across its two windings, so the
//   sequence is always: clear the opposite coil first, then energise the requested one, and release the
//   energised coil as soon as the end position is confirmed. The actuator is not designed to be held
//   energised at a limit. Never both True, never a coil left hot.

/// <summary>
/// The observed position of the Main Valve, derived only from the two end-of-travel limit switches.
/// <see cref="UNKNOWN"/> is not a position: it means the inputs were not readable, and a caller must fail
/// closed on it exactly as it fails closed on <see cref="CONFLICT"/>.
/// </summary>
public enum ProcessIoMainValvePosition
{
    /// <summary>One or both limit inputs were refused, stale, unbound or off-generation, so nothing is known.</summary>
    UNKNOWN = 0,

    /// <summary>Opened TRUE, Closed FALSE (Owner ruling 4): the valve is at its open limit.</summary>
    OPEN = 1,

    /// <summary>Opened FALSE, Closed TRUE (Owner ruling 4): the valve is at its closed limit.</summary>
    CLOSED = 2,

    /// <summary>Both FALSE (Owner ruling 4): the valve is between limits, i.e. traveling or stalled mid-travel.</summary>
    IN_TRANSIT = 3,

    /// <summary>Both TRUE (Owner ruling 4): a wiring or auxiliary-contact fault, never a real position.</summary>
    CONFLICT = 4,
}

/// <summary>Whether the Main Valve admission is enforced on a given composition of the Pump boundary.</summary>
public enum ProcessIoMainValveEnforcement
{
    /// <summary>
    /// The Pump ladder is composed without any Main Valve path, exactly as the pump-only stage always was.
    /// This is the boundary's own default so an existing composition compiles and behaves unchanged; it is
    /// never selected by a physical run that claims valve evidence, and it must be printed as
    /// <c>NOT_ENFORCED</c> so no reader can mistake it for a valve that was proven open.
    /// </summary>
    NOT_ENFORCED = 0,

    /// <summary>
    /// The Main Valve must be confirmed OPEN before any Pump energising command, and confirmed CLOSED after
    /// every run whose Pump stop was confirmed.
    /// </summary>
    ENFORCED = 1,
}

/// <summary>
/// The Main Valve trio and its semantics, by tag. No address, no module, no slot and no panel number is
/// repeated here: those belong to the adopted binding table, and repeating them is how a second source of
/// truth gets born. The one label discrepancy in the mapping workbook is recorded as a comment only, per
/// the Owner's ruling, because the addresses are already correct in the table.
/// </summary>
public static class ProcessIoMainValve
{
    /// <summary>The open command coil (workbook row "DO-014 Open Valve"). Energise to travel toward OPEN.</summary>
    public const string OpenCommandTag = "DO-014";

    /// <summary>The close command coil (workbook row "DO-015 Close Valve"). Energise to travel toward CLOSED.</summary>
    public const string CloseCommandTag = "DO-015";

    /// <summary>The open limit switch (workbook row "DI-013 Valve is Opened").</summary>
    public const string OpenedFeedbackTag = "DI-013";

    /// <summary>The closed limit switch (workbook row "DI-014 Valve is Closed").</summary>
    public const string ClosedFeedbackTag = "DI-014";

    /// <summary>The actuator over-torque trip (workbook row "DI-012 Over Torque Lamp"). TRUE means tripped.</summary>
    public const string OverTorqueFeedbackTag = "DI-012";

    /// <summary>The panel block that carries this trio, stated for the transcript so the reader can walk to it.</summary>
    public const string PanelBlock = "1.27";

    /// <summary>
    /// Both limit inputs and the over-torque input are dry contacts of the actuator's auxiliary switch block,
    /// relayed to 24 VDC, closed only at that end of travel: ACTIVE HIGH, exactly like the Pump Running and
    /// Fault feedbacks. No inverted reading is offered here on purpose - a polarity an operator can flip is a
    /// way to make OPEN and CLOSED exchange meanings.
    /// </summary>
    public const PumpFeedbackPolarity FeedbackPolarity = PumpFeedbackPolarity.ACTIVE_HIGH;

    /// <summary>The one accepted travel timeout range. A timeout is never defaulted by the Product.</summary>
    public const int MinimumTravelTimeoutMilliseconds = 1;

    /// <summary>The ceiling on one travel timeout, so a mistyped value cannot park the run for minutes.</summary>
    public const int MaximumTravelTimeoutMilliseconds = 120000;

    /// <summary>The valve reached OPEN because this product wrote the open coil and watched it arrive.</summary>
    public const string OpenSourceProductDispatch = "PRODUCT_DISPATCH";

    /// <summary>
    /// The valve was already proven OPEN by its own limit switch when this run began, so no open command was
    /// needed. Honest label, and a rule the Owner set: an already-open valve is admitted, not faked.
    /// </summary>
    public const string OpenSourcePreConfirmed = "PRE_CONFIRMED_OPEN";

    /// <summary>No Main Valve path is composed, so nothing was read and nothing was commanded.</summary>
    public const string OpenSourceNotEnforced = "NOT_ENFORCED";

    /// <summary>
    /// Maps the two limit bits to a position (Owner ruling 4). <paramref name="positionReadable"/> is false
    /// when either input was refused, and the answer is then UNKNOWN rather than a guess.
    /// </summary>
    public static ProcessIoMainValvePosition Classify(
        bool opened,
        bool closed,
        bool positionReadable)
    {
        if (!positionReadable)
        {
            return ProcessIoMainValvePosition.UNKNOWN;
        }

        if (opened && closed)
        {
            return ProcessIoMainValvePosition.CONFLICT;
        }

        if (opened)
        {
            return ProcessIoMainValvePosition.OPEN;
        }

        if (closed)
        {
            return ProcessIoMainValvePosition.CLOSED;
        }

        return ProcessIoMainValvePosition.IN_TRANSIT;
    }
}

/// <summary>
/// How strictly the Main Valve gates a composition of the Pump boundary. The timeouts are required values
/// whenever the valve is enforced: a travel timeout that is invented by the Product is a wait nobody
/// authorized, and a missing one would make the gate wait forever or not at all.
/// </summary>
public sealed record ProcessIoMainValveConfiguration
{
    /// <summary>Whether the Pump energising commands are gated on a confirmed OPEN position.</summary>
    public required ProcessIoMainValveEnforcement Enforcement { get; init; }

    /// <summary>The bound on the travel to OPEN, read from Owner-local configuration.</summary>
    public required TimeSpan OpenTimeout { get; init; }

    /// <summary>The bound on the travel to CLOSED, read from Owner-local configuration.</summary>
    public required TimeSpan CloseTimeout { get; init; }

    /// <summary>True when the valve admission and the post-stop close are enforced.</summary>
    public bool Enforced => Enforcement == ProcessIoMainValveEnforcement.ENFORCED;

    /// <summary>
    /// True when the configuration is internally consistent. Enforcement without usable timeouts is refused
    /// rather than downgraded, because a gate whose timeout is invalid cannot bound its own wait.
    /// </summary>
    public bool Valid
    {
        get
        {
            if (!Enforced)
            {
                return true;
            }

            return InRange(OpenTimeout) && InRange(CloseTimeout);
        }
    }

    /// <summary>The configuration the pump-only stage and every pre-existing composition keeps.</summary>
    public static ProcessIoMainValveConfiguration NotEnforced => new()
    {
        Enforcement = ProcessIoMainValveEnforcement.NOT_ENFORCED,
        OpenTimeout = TimeSpan.Zero,
        CloseTimeout = TimeSpan.Zero,
    };

    private static bool InRange(TimeSpan value)
        => value >= TimeSpan.FromMilliseconds(ProcessIoMainValve.MinimumTravelTimeoutMilliseconds)
        && value <= TimeSpan.FromMilliseconds(ProcessIoMainValve.MaximumTravelTimeoutMilliseconds);
}

/// <summary>
/// One complete Main Valve input read. The three raw bits and the two usability flags are kept separately on
/// purpose: the position is an interpretation, and a reader must be able to audit the interpretation without
/// trusting it. Codes are the session's own refusal codes, never a restatement of them.
/// </summary>
public sealed record ProcessIoMainValveRead
{
    /// <summary>The interpreted position, or <see cref="ProcessIoMainValvePosition.UNKNOWN"/>.</summary>
    public required ProcessIoMainValvePosition Position { get; init; }

    /// <summary>DI-013 as the polarity rule reads it: true at the open limit.</summary>
    public required bool OpenedActive { get; init; }

    /// <summary>DI-014 as the polarity rule reads it: true at the closed limit.</summary>
    public required bool ClosedActive { get; init; }

    /// <summary>DI-012 as the polarity rule reads it: true means the actuator tripped on torque.</summary>
    public required bool OverTorqueActive { get; init; }

    /// <summary>True when both limit inputs were granted and current.</summary>
    public required bool PositionReadable { get; init; }

    /// <summary>True when the over-torque input was granted and current.</summary>
    public required bool OverTorqueReadable { get; init; }

    /// <summary>True when all three inputs were granted and current. Anything else is failed closed.</summary>
    public required bool Usable { get; init; }

    /// <summary>The refusal that explains a non-usable read, or <c>OK</c>.</summary>
    public required string Code { get; init; }

    /// <summary>The session generation all three inputs were read in.</summary>
    public required int SessionGeneration { get; init; }

    /// <summary>The instant the last of the three reads completed.</summary>
    public required DateTimeOffset At { get; init; }
}
