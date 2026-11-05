namespace Wjss.Contracts;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the typed Main Pump command and feedback contract.
//
// AUTHORITY (exact, in this order)
//   1. The latest Owner decision (the consolidated Pump command contract) is the authority for the
//      command order, the maintained semantics, the selection exclusivity, the corrected Stop order,
//      the analog ramp shape and the feedback commissioning gate.
//   2. The authoritative T8 I/O mapping is the authority for every tag, function code and address.
//      Nothing here restates an address that the adopted binding table already owns: every address is
//      DERIVED from ProcessIoBindings at call time, so one row can never disagree with another.
//
// This file states identity and order only. It performs no I/O, holds no session and carries no
// authority to write: the write lifecycle remains ProcessIoSession.AdmitWrite/Dispatch*.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>
/// Commissioning state of one digital feedback identity. The physical polarity of the Running and
/// Fault inputs is NOT yet commissioned in WJSS, so the initial state of both is
/// <see cref="NOT_CONFIGURED"/> and a physical Pump command fails closed until an Owner commission
/// records a real value.
/// </summary>
public enum PumpFeedbackPolarity
{
    /// <summary>No value has been recorded for this input at all. The initial Product state.</summary>
    NOT_CONFIGURED = 0,

    /// <summary>A commission was attempted and did not establish a physical polarity.</summary>
    NOT_COMMISSIONED = 1,

    /// <summary>The input reads true (bit 1) when the physical condition is present.</summary>
    ACTIVE_HIGH = 2,

    /// <summary>The input reads false (bit 0) when the physical condition is present.</summary>
    ACTIVE_LOW = 3,
}

/// <summary>
/// The commissioned polarity of the two Pump feedback inputs. Both are uncommissioned by default, and
/// a command path must refuse rather than assume a polarity.
/// </summary>
public sealed record PumpFeedbackConfiguration
{
    public const string NotCommissionedCode = "WJSS_PUMP_FEEDBACK_POLARITY_NOT_COMMISSIONED";

    /// <summary>Polarity of DI-004, the inverter running input. Uncommissioned by default.</summary>
    public PumpFeedbackPolarity RunningPolarity { get; init; } = PumpFeedbackPolarity.NOT_CONFIGURED;

    /// <summary>Polarity of DI-005, the inverter fault input. Uncommissioned by default.</summary>
    public PumpFeedbackPolarity FaultPolarity { get; init; } = PumpFeedbackPolarity.NOT_CONFIGURED;

    /// <summary>The Product initial state: neither input is commissioned.</summary>
    public static PumpFeedbackConfiguration Uncommissioned { get; } = new();

    public bool RunningCommissioned =>
        RunningPolarity == PumpFeedbackPolarity.ACTIVE_HIGH
        || RunningPolarity == PumpFeedbackPolarity.ACTIVE_LOW;

    public bool FaultCommissioned =>
        FaultPolarity == PumpFeedbackPolarity.ACTIVE_HIGH
        || FaultPolarity == PumpFeedbackPolarity.ACTIVE_LOW;

    /// <summary>
    /// True only when BOTH required polarities are commissioned. A half-commissioned pair is not
    /// commissioned: the Running input alone cannot make the Fault input interpretable.
    /// </summary>
    public bool IsCommissioned => RunningCommissioned && FaultCommissioned;

    /// <summary>The typed reason a half-commissioned or uncommissioned pair is refused.</summary>
    public string CommissioningCode => IsCommissioned ? "OK" : NotCommissionedCode;
}

/// <summary>Who currently owns the Pump command channel. Exactly one owner may hold it at a time.</summary>
public enum PumpCommandOwnerKind
{
    /// <summary>No owner. The channel is free.</summary>
    NONE = 0,

    /// <summary>An operator-driven single command from the commissioning surface.</summary>
    MANUAL_COMMISSIONING = 1,

    /// <summary>The integrated dry command composition.</summary>
    INTEGRATED_DRY_CYCLE = 2,

    /// <summary>A Cleaning Job lifecycle.</summary>
    CLEANING_JOB = 3,

    /// <summary>A future automatic sequence.</summary>
    AUTOMATIC_SEQUENCE = 4,

    /// <summary>
    /// Production. Always refused in this scope: Production write authority is not granted, so no
    /// admission may ever produce this owner.
    /// </summary>
    PRODUCTION = 5,
}

/// <summary>
/// Why the Pump Stop path was entered. Every one of these runs the SAME corrected Stop order; the
/// reason is retained for evidence and never selects a different mechanism.
/// </summary>
public enum PumpStopReason
{
    /// <summary>The cleaning sequence reached its normal completion point.</summary>
    NORMAL_COMPLETION = 0,

    /// <summary>The operation was cancelled.</summary>
    CANCELLATION = 1,

    /// <summary>The inverter reported an active fault.</summary>
    FAULT = 2,

    /// <summary>The Pump Outlet pressure sample was not usable.</summary>
    AI003_INVALID = 3,

    /// <summary>The Pump Outlet pressure sample was older than the freshness budget.</summary>
    AI003_STALE = 4,

    /// <summary>No acceptable Pump Outlet pressure sample arrived inside the configured budget.</summary>
    AI003_TIMEOUT = 5,

    /// <summary>Fresh running feedback did not arrive inside the configured budget.</summary>
    RUNNING_FEEDBACK_TIMEOUT = 6,

    /// <summary>A downstream Isolation Valve command or supervision step failed.</summary>
    DOWNSTREAM_IV_FAILURE = 7,

    /// <summary>The Cleaning Job failed for a reason outside this lifecycle.</summary>
    CLEANING_JOB_FAILURE = 8,

    /// <summary>The sequence failed for a reason outside this lifecycle.</summary>
    SEQUENCE_FAILURE = 9,

    /// <summary>The application is cleaning up with Pump ownership still held.</summary>
    APPLICATION_CLEANUP = 10,

    /// <summary>A Start step failed, so the partially started Pump is returned to a known state.</summary>
    START_FAILURE = 11,
}

/// <summary>
/// Every Progress and Failure state of the one reusable Pump lifecycle. A lifecycle occupies exactly
/// one member. The Failure members are terminal: the cleanup obligations run before the terminal is
/// settled, and a cleanup failure is recorded separately so it can never overwrite the primary state.
/// </summary>
public enum PumpLifecycleState
{
    // Progression states.
    IDLE = 0,
    SELECTING_PUMP = 1,
    PUMP_SELECTED = 2,
    ENABLE_DISPATCHED = 3,
    RUN_DISPATCHED = 4,
    WAITING_FOR_RUNNING = 5,
    RAMPING_OUTPUT = 6,
    WAITING_FOR_AI003_READY = 7,
    PUMP_READY = 8,
    RAMPING_DOWN = 9,
    CLEARING_RUN = 10,
    DISABLING_INVERTER = 11,
    WAITING_FOR_STOPPED = 12,
    STOPPED = 13,

    // Failure states (terminal).
    COMMAND_ADMISSION_REFUSED = 20,
    SELECTION_FAILED = 21,
    ENABLE_FAILED = 22,
    RUN_FAILED = 23,
    RUNNING_FEEDBACK_TIMEOUT = 24,
    FAULT_ACTIVE = 25,
    AI003_INVALID = 26,
    AI003_STALE = 27,
    AI003_TIMEOUT = 28,
    RAMP_DOWN_FAILED = 29,
    CLEAR_RUN_FAILED = 30,
    DISABLE_FAILED = 31,
    STOPPED_FEEDBACK_TIMEOUT = 32,
    CLEANUP_INCOMPLETE = 33,
    PHYSICAL_ABORT_REQUIRED = 34,
}

/// <summary>Predicates over <see cref="PumpLifecycleState"/>. One classification, never duplicated.</summary>
public static class PumpLifecycleStates
{
    /// <summary>True for the fourteen progression states.</summary>
    public static bool IsProgression(PumpLifecycleState state) => (int)state is >= 0 and <= 13;

    /// <summary>True for the fifteen failure states.</summary>
    public static bool IsFailure(PumpLifecycleState state) => (int)state >= 20;

    /// <summary>True when no further transition may occur.</summary>
    public static bool IsTerminal(PumpLifecycleState state) => IsFailure(state) || state == PumpLifecycleState.STOPPED;

    /// <summary>True when the Pump is expected to be producing pressure.</summary>
    public static bool IsRunningOrReady(PumpLifecycleState state) =>
        state is PumpLifecycleState.WAITING_FOR_AI003_READY
            or PumpLifecycleState.PUMP_READY
            or PumpLifecycleState.RAMPING_OUTPUT;

    /// <summary>True when the Stop path has been entered and not yet completed.</summary>
    public static bool IsStopping(PumpLifecycleState state) =>
        state is PumpLifecycleState.RAMPING_DOWN
            or PumpLifecycleState.CLEARING_RUN
            or PumpLifecycleState.DISABLING_INVERTER
            or PumpLifecycleState.WAITING_FOR_STOPPED;

    /// <summary>
    /// True only when the Pump state is known well enough to report a safe completion. An unknown
    /// stopped state is never safe, and no caller may report completion while this is false.
    /// </summary>
    public static bool IsSafelyStopped(PumpLifecycleState state) => state == PumpLifecycleState.STOPPED;
}

/// <summary>Typed refusal vocabulary for the Pump command channel (fail closed, never string matching).</summary>
public static class PumpCommandRefusals
{
    public const string Granted = "OK";
    public const string RefusedCommandAdmission = "WJSS_PUMP_COMMAND_REFUSED_ADMISSION";
    public const string RefusedProduction = "WJSS_PUMP_COMMAND_REFUSED_PRODUCTION";
    public const string RefusedConflictingOwner = "WJSS_PUMP_COMMAND_REFUSED_CONFLICTING_OWNER";
    public const string RefusedConcurrentLifecycle = "WJSS_PUMP_COMMAND_REFUSED_CONCURRENT_LIFECYCLE";
    public const string RefusedOwnershipNotHeld = "WJSS_PUMP_COMMAND_REFUSED_OWNERSHIP_NOT_HELD";
    public const string RefusedWrongTag = "WJSS_PUMP_COMMAND_REFUSED_WRONG_TAG";
    public const string RefusedWrongFunctionOrAddress = "WJSS_PUMP_COMMAND_REFUSED_WRONG_FUNCTION_OR_ADDRESS";
    public const string RefusedInvalidSelection = "WJSS_PUMP_COMMAND_REFUSED_INVALID_SELECTION";
    public const string RefusedFaultActive = "WJSS_PUMP_COMMAND_REFUSED_FAULT_ACTIVE";
    public const string RefusedInvalidConfiguration = "WJSS_PUMP_COMMAND_REFUSED_INVALID_CONFIGURATION";
    public const string RefusedPolarityNotCommissioned = "WJSS_PUMP_COMMAND_REFUSED_POLARITY_NOT_COMMISSIONED";
    public const string RefusedNotAdmittedBySession = "WJSS_PUMP_COMMAND_REFUSED_BY_SESSION";
    public const string RefusedDispatchFailed = "WJSS_PUMP_COMMAND_DISPATCH_FAILED";
    public const string RefusedAnalogOutOfDomain = "WJSS_PUMP_ANALOG_REFUSED_OUT_OF_DOMAIN";
    public const string RefusedNotReady = "WJSS_PUMP_COMMAND_REFUSED_NOT_READY";
    public const string RefusedAlreadyTerminal = "WJSS_PUMP_COMMAND_REFUSED_ALREADY_TERMINAL";

    /// <summary>The alias the lifecycle uses for a terminal channel that may never restart.</summary>
    public const string RefusedTerminal = RefusedAlreadyTerminal;

    /// <summary>A Production owner attempted to acquire the channel.</summary>
    public const string RefusedProductionOwner = RefusedProduction;

    /// <summary>The transport reported a fault. Never retried automatically.</summary>
    public const string RefusedTransportFault = "WJSS_PUMP_COMMAND_REFUSED_TRANSPORT_FAULT";

    /// <summary>The tag has no adopted binding, so no address can be derived for it.</summary>
    public const string RefusedUnbound = "WJSS_PUMP_COMMAND_REFUSED_UNBOUND";

    /// <summary>The Pump Outlet Pressure evidence was not accepted as current physical evidence.</summary>
    public const string RefusedPressure = "WJSS_PUMP_READY_REFUSED_PRESSURE";

    /// <summary>The session did not admit the write, so nothing was dispatched.</summary>
    public const string RefusedNotAdmitted = RefusedNotAdmittedBySession;
}

/// <summary>One step of an ordered command sequence: the tag, the maintained value and its order.</summary>
public sealed record PumpCommandStep(int Order, string Tag, bool Value);

/// <summary>
/// The exact commissioned Pump command contract. Tags are stated once, addresses are derived from the
/// adopted binding table, and every order is expressed as an explicit sequence so a test can pin it
/// and a reader cannot mistake the superseded order for the current one.
/// </summary>
public static class PumpCommandContract
{
    // ---------------------------------------------------------------- identities

    /// <summary>Pump selection output for Pump 1 (the Current Critical Path selection).</summary>
    public const string SelectPumpOneTag = "DO-001";

    /// <summary>Pump selection output for Pump 2.</summary>
    public const string SelectPumpTwoTag = "DO-002";

    /// <summary>Inverter enable output. Energised FIRST on Start and cleared SECOND on Stop.</summary>
    public const string EnableInverterTag = "DO-003";

    /// <summary>Run Pump output. Energised SECOND on Start and cleared FIRST on Stop.</summary>
    public const string RunPumpTag = "DO-007";

    /// <summary>Analog speed reference output.</summary>
    public const string SpeedReferenceTag = "AO-001+";

    /// <summary>The single Running feedback input.</summary>
    public const string RunningFeedbackTag = "DI-004";

    /// <summary>The single Fault feedback input.</summary>
    public const string FaultFeedbackTag = "DI-005";

    /// <summary>The Pump Outlet Pressure row, the sole physical Pump-ready authority.</summary>
    public const string PumpOutletPressureTag = ProcessIoIsolationValves.PumpOutletPressureTag;

    /// <summary>The Pump Inlet Pressure row: diagnostic and trend only, never a Pump-ready source.</summary>
    public const string PumpInletPressureTag = ProcessIoIsolationValves.PumpInletPressureTag;

    /// <summary>The selection the Current Product Critical Path uses unless the Owner authorizes another.</summary>
    public const int CurrentCriticalPathSelection = 1;

    /// <summary>
    /// Both Start commands are maintained active-high outputs. They are not pulses, so no pulse width
    /// exists and no timer may clear them.
    /// </summary>
    public const string CommandSemantics = "MAINTAINED_ACTIVE_HIGH";

    /// <summary>Explicit statement that no pulse duration applies. Absence is intentional, not omitted.</summary>
    public const string PulseSemantics = "NO_PULSE_DURATION_APPLIES";

    // ---------------------------------------------------------------- addresses

    /// <summary>
    /// The adopted WRITE coil address for one Pump command output, derived from the single binding
    /// table. A tag that is not an adopted WRITE coil is refused rather than guessed.
    /// </summary>
    public static bool TryCoilAddress(string tag, out int address)
    {
        address = 0;
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding)) return false;
        if (binding.Area != ProcessIoArea.Coil || binding.Direction != "WRITE") return false;
        address = binding.Address;
        return true;
    }

    /// <summary>
    /// The adopted WRITE holding-register address of the analog speed reference, derived from the
    /// single binding table.
    /// </summary>
    public static bool TryRegisterAddress(string tag, out int address)
    {
        address = 0;
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding)) return false;
        if (binding.Area != ProcessIoArea.HoldingRegister || binding.Direction != "WRITE") return false;
        address = binding.Address;
        return true;
    }

    /// <summary>The adopted READ discrete-input address of one feedback input.</summary>
    public static bool TryDiscreteInputAddress(string tag, out int address)
    {
        address = 0;
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding)) return false;
        if (binding.Area != ProcessIoArea.DiscreteInput || binding.Direction != "READ") return false;
        address = binding.Address;
        return true;
    }

    /// <summary>True only for the four Pump command outputs this lifecycle may write.</summary>
    public static bool IsPumpCommandTag(string? tag) =>
        tag is SelectPumpOneTag or SelectPumpTwoTag or EnableInverterTag or RunPumpTag;

    // ---------------------------------------------------------------- selection

    /// <summary>
    /// The exact two-write selection sequence for one Pump: energise the selected contactor and
    /// de-energise the other, in that order. Selection is maintained and mutually exclusive, so the
    /// second write is what makes the exclusivity true rather than assumed.
    /// </summary>
    public static IReadOnlyList<PumpCommandStep> SelectionSequence(int selection)
    {
        if (selection == 1)
        {
            return new[]
            {
                new PumpCommandStep(1, SelectPumpOneTag, true),
                new PumpCommandStep(2, SelectPumpTwoTag, false),
            };
        }

        if (selection == 2)
        {
            return new[]
            {
                new PumpCommandStep(1, SelectPumpTwoTag, true),
                new PumpCommandStep(2, SelectPumpOneTag, false),
            };
        }

        return Array.Empty<PumpCommandStep>();
    }

    /// <summary>True only for the two commissioned selections.</summary>
    public static bool IsValidSelection(int selection) => selection is 1 or 2;

    // ---------------------------------------------------------------- start and stop

    /// <summary>
    /// The commissioned Start order: enable the inverter, then run the pump. Both maintained.
    /// </summary>
    public static IReadOnlyList<PumpCommandStep> StartSequence { get; } = new[]
    {
        new PumpCommandStep(1, EnableInverterTag, true),
        new PumpCommandStep(2, RunPumpTag, true),
    };

    /// <summary>
    /// The CORRECTED Stop order, as decided by the Owner: de-energise Run Pump FIRST, then de-energise
    /// Enable Inverter.
    ///
    /// The older Legacy source cleared Enable before Run. That order is SUPERSEDED and must never be
    /// implemented, documented or tested as current WJSS behaviour. This property is the only place
    /// the current order is expressed, so there is no second statement of it to drift.
    /// </summary>
    public static IReadOnlyList<PumpCommandStep> StopSequence { get; } = new[]
    {
        new PumpCommandStep(1, RunPumpTag, false),
        new PumpCommandStep(2, EnableInverterTag, false),
    };

    /// <summary>
    /// The Start order as ordered tags. A view of <see cref="StartSequence"/>: the order is stated
    /// once, and this member restates nothing.
    /// </summary>
    public static IReadOnlyList<string> StartOrder { get; } = new[]
    {
        EnableInverterTag,
        RunPumpTag,
    };

    /// <summary>
    /// The CORRECTED Stop order as ordered tags. A view of <see cref="StopSequence"/>, so the
    /// superseded order has no place to reappear.
    /// </summary>
    public static IReadOnlyList<string> StopOrder { get; } = new[]
    {
        RunPumpTag,
        EnableInverterTag,
    };

    /// <summary>
    /// The adopted WRITE holding-register address of the analog speed reference. The tag is the
    /// commissioned contract tag, so a caller cannot supply a different analog row here.
    /// </summary>
    public static bool TryAnalogAddress(out int registerAddress) =>
        TryRegisterAddress(SpeedReferenceTag, out registerAddress);

    /// <summary>
    /// True when the supplied order is the corrected one. Used to refuse a caller that supplies the
    /// superseded order rather than silently accepting it.
    /// </summary>
    public static bool IsCorrectedStopOrder(IReadOnlyList<PumpCommandStep>? sequence)
    {
        if (sequence is null || sequence.Count != StopSequence.Count) return false;
        for (int index = 0; index < StopSequence.Count; index += 1)
        {
            if (!string.Equals(sequence[index].Tag, StopSequence[index].Tag, StringComparison.Ordinal)) return false;
            if (sequence[index].Value != StopSequence[index].Value) return false;
        }
        return true;
    }
}

/// <summary>
/// The Pump analog speed reference command: the ONE Pump-specific conversion authority.
///
/// The Legacy write conversion is map(pumpCurrentOut, 4, 20, 0, 32767). The live running span is the
/// closed 4 to 20 mA range, an explicit 0 mA sentinel withdraws the reference, and a value in the open
/// 0 to 4 mA band, a negative value or a value above 20 mA is refused rather than clamped.
///
/// The general 0 to 20 mA helper belongs to unrelated Product callers, is NOT this authority, is not
/// modified, and is never used to produce a Pump command.
///
/// The engineering command unit is milliamps, as commissioned. It is deliberately NOT renamed to
/// pressure: no conversion authority proves that the analog output means bar.
/// </summary>
public static class PumpAnalogCommandContract
{
    /// <summary>The analog output row.</summary>
    public const string Tag = PumpCommandContract.SpeedReferenceTag;

    /// <summary>The engineering unit. Milliamps, not bar.</summary>
    public const string EngineeringUnit = "mA";

    /// <summary>The lowest live running reference. Inclusive.</summary>
    public const double LiveMinimumMilliamps = 4.0;

    /// <summary>The highest live running reference. Inclusive, and equal to raw full scale.</summary>
    public const double LiveMaximumMilliamps = 20.0;

    /// <summary>The explicit safe-stop sentinel. Not a live signal, and it shares raw 0 with 4 mA.</summary>
    public const double SafeStopSentinelMilliamps = 0.0;

    /// <summary>The commissioned automatic ramp interval.</summary>
    public const int RampIntervalMilliseconds = 1000;

    /// <summary>The commissioned automatic ramp step, in engineering command units.</summary>
    public const double RampStepEngineering = 2.0;

    /// <summary>The closed live running domain lower bound. Inclusive, equal to the live minimum.</summary>
    public const double EngineeringMinimum = 4.0;

    /// <summary>The closed live running domain upper bound. Inclusive, equal to the live maximum.</summary>
    public const double EngineeringMaximum = 20.0;

    /// <summary>The safe zero the down-ramp converges on: the explicit sentinel, never 2 mA.</summary>
    public const double SafeZeroEngineeringCommand = 0.0;

    /// <summary>Evidence code for the explicit safe-stop sentinel.</summary>
    public const string SafeStopSentinelCode = "PUMP_ANALOG_SAFE_STOP_SENTINEL_0MA";

    /// <summary>Evidence code for a live running reference.</summary>
    public const string RunningCode = "PUMP_ANALOG_RUNNING_REFERENCE";

    /// <summary>True when the value is a live running reference: 4 to 20 mA inclusive.</summary>
    public static bool IsLive(double milliamps) =>
        double.IsFinite(milliamps)
        && milliamps >= LiveMinimumMilliamps
        && milliamps <= LiveMaximumMilliamps;

    /// <summary>True when the value is the explicit 0 mA safe-stop sentinel.</summary>
    public static bool IsSafeStopSentinel(double milliamps) =>
        double.IsFinite(milliamps) && milliamps == SafeStopSentinelMilliamps;

    /// <summary>
    /// The Pump command domain: the sentinel plus the closed live span. The open 0 to 4 mA band is NOT
    /// in the domain, and neither is any negative or above-20 mA value.
    /// </summary>
    public static bool IsInDomain(double engineering) =>
        IsSafeStopSentinel(engineering) || IsLive(engineering);

    /// <summary>
    /// The exact Legacy conversion, applied to a typed command: map(mA, 4, 20, 0, 32767). A running
    /// command of exactly 4 mA yields raw 0, 12 mA yields 16384 and 20 mA yields 32767. The sentinel
    /// yields raw 0 as well, which is why the engineering identity is carried by the command kind and
    /// never inferred from the raw word.
    /// </summary>
    public static bool TryToRaw(PumpAnalogCommand command, out int raw)
    {
        raw = 0;
        if (!command.Valid) return false;
        if (command.IsSafeStopSentinel) return true;
        if (!IsLive(command.Milliamps)) return false;

        double span = LiveMaximumMilliamps - LiveMinimumMilliamps;
        double scaled = (command.Milliamps - LiveMinimumMilliamps)
            * ProcessIoRepresentation.RawFullScale / span;
        raw = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>
    /// Builds the one command the value denotes and converts it, or refuses it. This is the only Pump
    /// raw-word entry, and it refuses rather than clamps.
    /// </summary>
    public static bool TryToRaw(double milliamps, out int raw)
    {
        raw = 0;
        return PumpAnalogCommand.TryCreate(milliamps, out PumpAnalogCommand command) && TryToRaw(command, out raw);
    }
}

/// <summary>
/// The two admissible kinds of Pump analog command. The kind carries the engineering identity that
/// the raw word cannot: the 4 mA live minimum and the 0 mA safe-stop sentinel both convert to raw 0.
/// </summary>
public enum PumpAnalogCommandKind
{
    /// <summary>0 mA safe-stop sentinel: the analog reference is withdrawn.</summary>
    SAFE_STOP_SENTINEL = 0,

    /// <summary>A live running reference inside the commissioned 4 to 20 mA span.</summary>
    RUNNING = 1,
}

/// <summary>
/// One typed Pump analog command. A command is either the explicit 0 mA safe-stop sentinel or a live
/// running reference in the closed 4 to 20 mA span. Anything else is not a command: there is no third
/// kind and no unknown value, so an invalid engineering value can never be constructed.
/// </summary>
public readonly record struct PumpAnalogCommand
{
    private PumpAnalogCommand(PumpAnalogCommandKind kind, double milliamps)
    {
        Kind = kind;
        Milliamps = milliamps;
        Valid = true;
    }

    /// <summary>Which of the two admissible commands this is.</summary>
    public PumpAnalogCommandKind Kind { get; }

    /// <summary>The engineering command in milliamps.</summary>
    public double Milliamps { get; }

    /// <summary>False only for the default value, which is never a command.</summary>
    public bool Valid { get; }

    /// <summary>True only for the explicit 0 mA safe-stop sentinel.</summary>
    public bool IsSafeStopSentinel => Valid && Kind == PumpAnalogCommandKind.SAFE_STOP_SENTINEL;

    /// <summary>True only for a live running reference in the 4 to 20 mA span.</summary>
    public bool IsRunning => Valid && Kind == PumpAnalogCommandKind.RUNNING;

    /// <summary>The explicit safe-stop sentinel: 0 mA, raw 0.</summary>
    public static PumpAnalogCommand SafeStopSentinel =>
        new(PumpAnalogCommandKind.SAFE_STOP_SENTINEL, PumpAnalogCommandContract.SafeStopSentinelMilliamps);

    /// <summary>
    /// Creates the one command the value denotes, or refuses it. Exactly 0 mA is the sentinel; 4 to
    /// 20 mA inclusive is a live running reference; every other value (negative, above 20 mA and the
    /// open 0 to 4 mA band) is refused and is never clamped into a neighbouring command.
    /// </summary>
    public static bool TryCreate(double milliamps, out PumpAnalogCommand command)
    {
        command = default;

        if (!double.IsFinite(milliamps)) return false;

        if (milliamps == PumpAnalogCommandContract.SafeStopSentinelMilliamps)
        {
            command = SafeStopSentinel;
            return true;
        }

        if (!PumpAnalogCommandContract.IsLive(milliamps)) return false;

        command = new PumpAnalogCommand(PumpAnalogCommandKind.RUNNING, milliamps);
        return true;
    }

    /// <summary>
    /// Creates a live running reference. A value outside the closed 4 to 20 mA span is refused: a
    /// running target below 4 mA is an invalid configuration, not a slow speed.
    /// </summary>
    public static bool TryCreateRunning(double milliamps, out PumpAnalogCommand command)
    {
        command = default;
        if (!PumpAnalogCommandContract.IsLive(milliamps)) return false;
        command = new PumpAnalogCommand(PumpAnalogCommandKind.RUNNING, milliamps);
        return true;
    }

    /// <summary>The canonical evidence code for this command kind. Never the raw word.</summary>
    public string KindCode => Kind == PumpAnalogCommandKind.SAFE_STOP_SENTINEL
        ? PumpAnalogCommandContract.SafeStopSentinelCode
        : PumpAnalogCommandContract.RunningCode;

    public override string ToString() => KindCode;
}

/// <summary>Typed cleanup states. A completion is never claimed without a confirmed stopped reading.</summary>
public static class PumpCleanupStates
{
    /// <summary>Every stage was observed complete and the stopped state was confirmed.</summary>
    public const string Complete = "COMPLETE";

    /// <summary>At least one stage failed or could not be confirmed.</summary>
    public const string Incomplete = "CLEANUP_INCOMPLETE";

    /// <summary>The stopped state is unknown. Physical abort is required.</summary>
    public const string PhysicalAbortRequired = "PHYSICAL_ABORT_REQUIRED";
}

/// <summary>
/// The ramp shape: one command step per interval toward the target, clamped so the command never
/// passes the target, and expressed as a typed command so the 0 to 4 mA band is never emitted.
///
/// The Owner-initial shape is a 1000 ms interval with a 2 mA step, converging on the explicit
/// safe-stop sentinel. Both values are configuration, not physics.
/// </summary>
public sealed record PumpSpeedRampConfiguration
{
    /// <summary>The Owner-initial ramp interval.</summary>
    public const int OwnerInitialIntervalMilliseconds = 1000;

    /// <summary>The Owner-initial ramp step, in milliamps.</summary>
    public const double OwnerInitialStepMilliamps = 2.0;

    /// <summary>Elapsed time between two ramp steps.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(OwnerInitialIntervalMilliseconds);

    /// <summary>Engineering milliamps added or removed per interval.</summary>
    public double Step { get; init; } = OwnerInitialStepMilliamps;

    /// <summary>The configured running target. It must be a live reference.</summary>
    public required double TargetEngineeringMilliamps { get; init; }

    /// <summary>The safe zero the down-ramp converges on: the explicit sentinel.</summary>
    public double SafeZeroEngineeringCommand { get; init; } = PumpAnalogCommandContract.SafeZeroEngineeringCommand;

    /// <summary>
    /// True only when the interval and step are usable, the target is a live reference and the safe
    /// zero is the explicit sentinel. A target below 4 mA is an invalid configuration.
    /// </summary>
    public bool Valid =>
        Interval > TimeSpan.Zero
        && double.IsFinite(Step)
        && Step > 0
        && PumpAnalogCommandContract.IsLive(TargetEngineeringMilliamps)
        && PumpAnalogCommandContract.IsSafeStopSentinel(SafeZeroEngineeringCommand)
        && SafeZeroEngineeringCommand < TargetEngineeringMilliamps;
}

/// <summary>
/// The one ramp arithmetic. Every produced value is a typed command, so the caller cannot write an
/// analog value outside the commissioned domain even by accident.
/// </summary>
public static class PumpAnalogRamp
{
    private const double Epsilon = 1e-9;

    /// <summary>True when the command is already the target value.</summary>
    public static bool AtTarget(PumpAnalogCommand current, double target) =>
        current.Valid && double.IsFinite(target) && Math.Abs(current.Milliamps - target) <= Epsilon;

    /// <summary>
    /// The next command toward the target. The target is either the explicit 0 mA sentinel (a Stop
    /// ramp) or a live 4 to 20 mA reference (a Run ramp). Any other target, a non-positive step or an
    /// invalid current command is refused, and no command is produced.
    /// </summary>
    public static bool TryNext(
        PumpAnalogCommand current,
        double target,
        double step,
        out PumpAnalogCommand next)
    {
        next = default;

        if (!current.Valid) return false;
        if (!double.IsFinite(step) || step <= 0) return false;
        if (!PumpAnalogCommandContract.IsInDomain(target)) return false;

        if (AtTarget(current, target))
        {
            next = current;
            return true;
        }

        if (PumpAnalogCommandContract.IsSafeStopSentinel(target))
        {
            // A Stop ramp converges on the explicit sentinel. Anything below the 4 mA live minimum is
            // no longer a live reference, so the next command is the sentinel and never 2 mA.
            double lowered = current.Milliamps - step;
            next = lowered < PumpAnalogCommandContract.LiveMinimumMilliamps - Epsilon
                ? PumpAnalogCommand.SafeStopSentinel
                : Command(lowered);
            return true;
        }

        // The first live command of a Run ramp is the 4 mA live minimum, so a ramp that starts at the
        // sentinel never passes through the open band either.
        double candidate = current.Milliamps + step;
        if (candidate < PumpAnalogCommandContract.LiveMinimumMilliamps)
        {
            candidate = PumpAnalogCommandContract.LiveMinimumMilliamps;
        }

        if (candidate > target)
        {
            candidate = target;
        }

        next = Command(candidate);
        return true;
    }

    private static PumpAnalogCommand Command(double milliamps) =>
        PumpAnalogCommand.TryCreateRunning(milliamps, out PumpAnalogCommand command)
            ? command
            : PumpAnalogCommand.SafeStopSentinel;
}

/// <summary>
/// One lifecycle configuration. Every member is required: WJSS supplies no silent Product default for
/// a setpoint, a freshness budget or any supervision timeout.
/// </summary>
public sealed record PumpLifecycleConfiguration
{
    /// <summary>The Owner-initialized Pump-ready setpoint. The comparison against it is strict.</summary>
    public const double OwnerInitialPumpReadySetpointBar = 15.0;

    /// <summary>The Owner-initialized freshness budget for one Pump Outlet pressure sample.</summary>
    public const int OwnerInitialFreshnessBudgetMilliseconds = 2000;

    /// <summary>The strict Pump-ready rule: measured outlet pressure greater than this value.</summary>
    public required double PumpReadySetpointBar { get; init; }

    /// <summary>A Pump Outlet sample older than this is refused rather than reused.</summary>
    public required TimeSpan PressureFreshnessBudget { get; init; }

    /// <summary>How long fresh running feedback may take before the Start is abandoned.</summary>
    public required TimeSpan RunningFeedbackTimeout { get; init; }

    /// <summary>How long the outlet pressure may take to exceed the setpoint.</summary>
    public required TimeSpan PumpReadyTimeout { get; init; }

    /// <summary>How long confirmed not-running feedback may take during Stop.</summary>
    public required TimeSpan StoppedFeedbackTimeout { get; init; }

    /// <summary>The ramp shape of the analog command.</summary>
    public required PumpSpeedRampConfiguration Ramp { get; init; }

    /// <summary>
    /// The commissioned feedback polarity. The Product initial state leaves both inputs
    /// NOT_CONFIGURED, which fails the whole command path closed rather than assuming a polarity.
    /// </summary>
    public PumpFeedbackConfiguration Feedback { get; init; } = PumpFeedbackConfiguration.Uncommissioned;

    public bool Valid
    {
        get
        {
            if (Ramp is null) return false;
            return double.IsFinite(PumpReadySetpointBar)
                && PumpReadySetpointBar >= 0
                && PressureFreshnessBudget > TimeSpan.Zero
                && RunningFeedbackTimeout > TimeSpan.Zero
                && PumpReadyTimeout > TimeSpan.Zero
                && StoppedFeedbackTimeout > TimeSpan.Zero
                && Ramp.Valid;
        }
    }
}
