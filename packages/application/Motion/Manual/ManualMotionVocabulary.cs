namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// The closed axis domain of the WSB1 Manual Motion Commissioning slice: exactly Axis A (the
/// Horizontal Axis), Axis B (the Vertical Axis), Axis C (the Horizontal Axis) and Axis D (the
/// Vertical Axis), Owner-confirmed for WSB1.
///
/// The enumeration starts at 1 on purpose: a default-initialized value (0) is not a member, so an
/// empty or defaulted axis is refused instead of silently becoming Axis A. Every other controller
/// is outside this slice. The stable snapshot order is A, B, C, D.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public enum ManualMotionAxis
{
    /// <summary>Axis A: the Horizontal Axis of WSB1.</summary>
    A = 1,

    /// <summary>Axis B: the Vertical Axis of WSB1.</summary>
    B = 2,

    /// <summary>Axis C: the Horizontal Axis of WSB1.</summary>
    C = 3,

    /// <summary>Axis D: the Vertical Axis of WSB1.</summary>
    D = 4,
}

/// <summary>The two hold-to-run Jog directions. The sign of the native jog speed carries the direction.</summary>
public enum ManualMotionJogDirection
{
    /// <summary>Positive native direction.</summary>
    Positive = 1,

    /// <summary>Negative native direction.</summary>
    Negative = 2,
}

/// <summary>Why the Operator side ended a hold-to-run Jog press.</summary>
public enum ManualMotionJogEndReason
{
    /// <summary>The hold-to-run control was released.</summary>
    Released = 1,

    /// <summary>The pointer capture of the hold-to-run control was lost.</summary>
    CaptureLost = 2,

    /// <summary>The commissioning application was deactivated.</summary>
    Deactivated = 3,
}

/// <summary>
/// The identity of the port behind a session. A simulated port can never be presented as, or
/// confused with, a hardware-capable port.
/// </summary>
public enum ManualMotionPortMode
{
    /// <summary>Deterministic in-process simulation: no native library, no controller, no network, no output.</summary>
    Simulated = 1,

    /// <summary>The motion-library adapter path for the WSB1 controller (Owner-local; separate Scope Gate for hardware use).</summary>
    HardwareCapable = 2,
}

/// <summary>
/// The closed typed command vocabulary a session may ask a port to execute. No member carries text:
/// the hardware-capable port renders each member through a closed command plan inside the adapter.
/// </summary>
public enum ManualMotionPortCommandKind
{
    /// <summary>Set the logical state of the configured brake output (one of the four WSB1 brake outputs).</summary>
    BrakeOutputSet = 1,

    /// <summary>Clear the logical state of the configured brake output.</summary>
    BrakeOutputClear = 2,

    /// <summary>Load the signed native jog speed for one axis.</summary>
    JogSpeed = 3,

    /// <summary>Begin the motion already loaded for one axis.</summary>
    Begin = 4,

    /// <summary>The existing typed per-axis Stop.</summary>
    Stop = 5,

    /// <summary>Load the native home speed for one axis.</summary>
    HomeSpeed = 6,

    /// <summary>Arm the controller home sequence for one axis.</summary>
    Home = 7,

    /// <summary>Define the axis position as zero (zero is the only operand in this scope).</summary>
    DefinePositionZero = 8,

    /// <summary>Define the axis dual register as zero (zero is the only operand in this scope).</summary>
    DefineDualZero = 9,

    /// <summary>Load the native acceleration for one axis.</summary>
    Acceleration = 10,

    /// <summary>Load the native speed for one axis.</summary>
    Speed = 11,

    /// <summary>Load the native deceleration for one axis.</summary>
    Deceleration = 12,

    /// <summary>Load the absolute native target for one axis.</summary>
    PositionAbsolute = 13,
}

/// <summary>
/// The closed typed read vocabulary. The two position reads keep NEUTRAL names: which of the two is
/// the commanded and which is the measured position is not resolved by committed evidence, so no
/// meaning is adopted here.
/// </summary>
public enum ManualMotionReadKind
{
    /// <summary>The raw axis status word (native status bits; interpretation only through the Owner-local profile).</summary>
    StatusWord = 1,

    /// <summary>The first raw position read (the existing TP read), meaning unresolved.</summary>
    PositionTp = 2,

    /// <summary>The second raw position read (the existing RP read), meaning unresolved.</summary>
    PositionRp = 3,

    /// <summary>The logical state of the configured brake output, UNCONFIRMED (no wiring or polarity meaning).</summary>
    BrakeOutputState = 4,
}

/// <summary>
/// Which brake-output action releases the brake of one axis. This is Owner-local configuration:
/// committed evidence does not prove the polarity, so a missing value refuses every brake release.
/// The apply action is always the opposite action.
/// </summary>
public enum ManualMotionBrakeReleaseAction
{
    /// <summary>Release is the set-output action; apply is the clear-output action.</summary>
    SetOutput = 1,

    /// <summary>Release is the clear-output action; apply is the set-output action.</summary>
    ClearOutput = 2,
}

/// <summary>The lifecycle of one Manual Motion Commissioning session.</summary>
public enum ManualMotionSessionState
{
    /// <summary>Constructed; the port has not been opened yet.</summary>
    NotStarted = 0,

    /// <summary>The profile was refused; the port is never opened.</summary>
    ProfileRefused = 1,

    /// <summary>The port open was refused or failed; no operation is possible.</summary>
    OpenRefused = 2,

    /// <summary>The port is open and operations may be requested.</summary>
    Ready = 3,

    /// <summary>A communication or native fault occurred; no further command is issued automatically and nothing reconnects.</summary>
    Faulted = 4,

    /// <summary>Close was requested; every active motion operation is being stopped before the port is closed.</summary>
    Closing = 5,

    /// <summary>The port was closed. A closed session never reopens.</summary>
    Closed = 6,
}

/// <summary>The kind of one axis operation of a session.</summary>
public enum ManualMotionOperationKind
{
    /// <summary>No operation.</summary>
    None = 0,

    /// <summary>Brake release on one axis.</summary>
    BrakeRelease = 1,

    /// <summary>Brake apply on one axis.</summary>
    BrakeApply = 2,

    /// <summary>Hold-to-run Jog on one axis.</summary>
    Jog = 3,

    /// <summary>A standalone typed Stop on one axis.</summary>
    Stop = 4,

    /// <summary>Home on one axis.</summary>
    Home = 5,

    /// <summary>Define position zero on one axis.</summary>
    DefinePositionZero = 6,

    /// <summary>Define dual zero on one axis.</summary>
    DefineDualZero = 7,

    /// <summary>Absolute bounded Move on one axis.</summary>
    Move = 8,
}

/// <summary>The phase of one axis operation. At most one port call is made per session step.</summary>
public enum ManualMotionOperationPhase
{
    /// <summary>No active operation.</summary>
    None = 0,

    /// <summary>The brake release command is due.</summary>
    ReleasingBrake = 1,

    /// <summary>Waiting for the configured brake settle time.</summary>
    BrakeSettling = 2,

    /// <summary>Loading the typed motion parameters, one command per step.</summary>
    Configuring = 3,

    /// <summary>The begin command is due.</summary>
    Beginning = 4,

    /// <summary>Motion is running and supervised by the session.</summary>
    Running = 5,

    /// <summary>The typed Stop is due.</summary>
    Stopping = 6,

    /// <summary>Waiting for an observation, taken after the Stop, that shows the axis is not moving.</summary>
    AwaitingStopped = 7,

    /// <summary>The define-position-zero command is due.</summary>
    DefiningZero = 8,

    /// <summary>Waiting for position reads taken after the zero definition.</summary>
    VerifyingZero = 9,

    /// <summary>The brake apply command is due.</summary>
    ApplyingBrake = 10,

    /// <summary>A single typed command is due (brake-only and dual-zero operations).</summary>
    Dispatching = 11,

    /// <summary>
    /// The define-dual-zero command of the Owner-approved Home-carried reset-encoder composition is
    /// due. Only a Home operation reaches this phase: it runs immediately after the accepted
    /// define-position-zero command, so no unrelated native command can occur between the two. The
    /// standalone define-position-zero operation never reaches this phase and is unchanged.
    /// </summary>
    DefiningDualZero = 12,
}

/// <summary>
/// The COMMAND state of one brake. It records what the session commanded, never the physical brake:
/// committed evidence proves neither the output polarity nor the wiring.
/// </summary>
public enum ManualMotionBrakeCommandState
{
    /// <summary>No brake command was issued in this session.</summary>
    NotCommanded = 0,

    /// <summary>The configured release action was dispatched without refusal (not a physical confirmation).</summary>
    ReleaseCommanded = 1,

    /// <summary>The configured apply action was dispatched without refusal (not a physical confirmation).</summary>
    ApplyCommanded = 2,

    /// <summary>A brake command was refused or failed; the command state is uncertain.</summary>
    Uncertain = 3,
}

/// <summary>The Home state of one axis as defined by this session.</summary>
public enum ManualMotionHomeState
{
    /// <summary>No Home was completed in this session, or the coordinate was redefined afterwards.</summary>
    NotHomed = 0,

    /// <summary>A Home operation is running.</summary>
    Homing = 1,

    /// <summary>The Home sequence completed but zero could not be verified by position reads.</summary>
    HomedUnverified = 2,

    /// <summary>The Home sequence completed and zero was verified by position reads within the configured tolerance (Owner-attested interpretation; not hardware evidence).</summary>
    Homed = 3,

    /// <summary>The Home operation failed (timeout, refusal, abort or zero verification failure).</summary>
    HomeFailed = 4,
}

/// <summary>The coordinate state produced by define-position-zero on one axis.</summary>
public enum ManualMotionCoordinateState
{
    /// <summary>No zero definition was dispatched in this session.</summary>
    Undefined = 0,

    /// <summary>A zero definition was dispatched; no verifying read has confirmed it.</summary>
    ZeroDispatched = 1,

    /// <summary>Position reads after the zero definition were within the configured tolerance.</summary>
    ZeroVerified = 2,

    /// <summary>Position reads after the zero definition were outside the configured tolerance.</summary>
    ZeroVerificationFailed = 3,
}

/// <summary>The dual-zero state of one axis. The effect of the dual definition is never verified in this scope.</summary>
public enum ManualMotionDualZeroState
{
    /// <summary>No dual-zero definition was dispatched in this session.</summary>
    NotDispatched = 0,

    /// <summary>The dual-zero definition was dispatched; its effect is UNVERIFIED.</summary>
    DispatchedUnverified = 1,

    /// <summary>The dual-zero definition was refused.</summary>
    Refused = 2,
}

/// <summary>
/// The Stop state of one axis. Dispatch is never confirmation: there is deliberately no confirmed
/// member, and <see cref="ManualMotionAxisSnapshot.StopConfirmed"/> is always false.
/// </summary>
public enum ManualAxisStopState
{
    /// <summary>No Stop was issued in this session.</summary>
    None = 0,

    /// <summary>The typed Stop was dispatched without refusal (dispatch only; not a confirmed stop).</summary>
    Issued = 1,

    /// <summary>The typed Stop was refused.</summary>
    Refused = 2,

    /// <summary>After the Stop, no observation showed the axis not moving within the configured timeout.</summary>
    NotObservedWithinTimeout = 3,
}

/// <summary>The interpretation of one status bit.</summary>
public enum ManualMotionInterpretation
{
    /// <summary>Not interpretable: no fresh observation, or the bit is not configured in the profile.</summary>
    Unknown = 0,

    /// <summary>The configured bit carries its configured active value.</summary>
    Active = 1,

    /// <summary>The configured bit does not carry its configured active value.</summary>
    Inactive = 2,
}

/// <summary>
/// The fixed, redacted code vocabulary of the Manual Motion Commissioning session. Codes only: no
/// command text, reply text, native result number, endpoint, path or hash ever appears in a code or
/// in its fixed detail text.
/// </summary>
public static class ManualMotionCodes
{
    // ----------------------------------------------------------------------------- profile

    /// <summary>The profile was accepted.</summary>
    public const string ProfileAccepted = "PROFILE_ACCEPTED";

    /// <summary>No profile text was provided.</summary>
    public const string ProfileNotProvided = "PROFILE_NOT_PROVIDED";

    /// <summary>The profile text exceeds the accepted size.</summary>
    public const string ProfileTooLarge = "PROFILE_TOO_LARGE";

    /// <summary>The profile file location is not a fully qualified local path, or the file cannot be read. The path is never displayed.</summary>
    public const string ProfileFileRefused = "PROFILE_FILE_REFUSED";

    /// <summary>The profile text is not a single valid JSON object.</summary>
    public const string ProfileJsonInvalid = "PROFILE_JSON_INVALID";

    /// <summary>The profile schema identifier is missing or not supported.</summary>
    public const string ProfileSchemaUnsupported = "PROFILE_SCHEMA_UNSUPPORTED";

    /// <summary>The profile carries a property this schema does not define.</summary>
    public const string ProfilePropertyUnknown = "PROFILE_PROPERTY_UNKNOWN";

    /// <summary>The profile carries the same property twice.</summary>
    public const string ProfilePropertyDuplicate = "PROFILE_PROPERTY_DUPLICATE";

    /// <summary>A required profile section is missing.</summary>
    public const string ProfileSectionMissing = "PROFILE_SECTION_MISSING";

    /// <summary>A profile value has the wrong type or is outside its structural range.</summary>
    public const string ProfileValueInvalid = "PROFILE_VALUE_INVALID";

    /// <summary>The profile names a controller other than WSB1.</summary>
    public const string ProfileControllerUnsupported = "PROFILE_CONTROLLER_UNSUPPORTED";

    /// <summary>The profile names an axis other than A, B, C or D.</summary>
    public const string ProfileAxisUnsupported = "PROFILE_AXIS_UNSUPPORTED";

    /// <summary>The profile brake output does not equal the Owner-confirmed allocation of that axis.</summary>
    public const string ProfileBrakeOutputAllocationMismatch = "PROFILE_BRAKE_OUTPUT_ALLOCATION_MISMATCH";

    /// <summary>The built-in simulation profile was offered to a hardware-capable port.</summary>
    public const string ProfileSimulationSourceRefusedForHardware = "PROFILE_SIMULATION_SOURCE_REFUSED_FOR_HARDWARE";

    // ----------------------------------------------------------------------------- session

    /// <summary>The port was opened.</summary>
    public const string SessionOpened = "SESSION_OPENED";

    /// <summary>The port open was refused.</summary>
    public const string SessionOpenRefused = "SESSION_OPEN_REFUSED";

    /// <summary>A communication or native fault moved the session to FAULTED.</summary>
    public const string SessionFaulted = "SESSION_FAULTED";

    /// <summary>The port was closed and every cleanup step was proven.</summary>
    public const string SessionClosed = "SESSION_CLOSED";

    /// <summary>The port was closed but at least one cleanup step was not proven.</summary>
    public const string SessionCloseIncomplete = "SESSION_CLOSE_INCOMPLETE";

    /// <summary>The session is not in a state that accepts the request.</summary>
    public const string SessionNotReady = "SESSION_NOT_READY";

    /// <summary>The session is closing and accepts no new operation.</summary>
    public const string SessionClosing = "SESSION_CLOSING";

    /// <summary>A port result belonged to an earlier port generation and was rejected.</summary>
    public const string StaleResultRejected = "STALE_RESULT_REJECTED";

    /// <summary>A port call reported a communication or native fault.</summary>
    public const string CommunicationFault = "COMMUNICATION_FAULT";

    /// <summary>A read returned no usable value; nothing was fabricated.</summary>
    public const string ObservationRefused = "OBSERVATION_REFUSED";

    // ---------------------------------------------------------------------------- refusals

    /// <summary>The axis is outside the closed A/B/C/D domain.</summary>
    public const string AxisOutOfDomain = "AXIS_OUT_OF_DOMAIN";

    /// <summary>The axis is not listed in the Owner-local profile.</summary>
    public const string AxisNotAllowedByProfile = "AXIS_NOT_ALLOWED_BY_PROFILE";

    /// <summary>Another operation is already active on this axis; at most one operation per axis.</summary>
    public const string OperationInProgress = "OPERATION_IN_PROGRESS";

    /// <summary>
    /// Retained code vocabulary (no longer returned by the session): a fresh observation of another
    /// axis moving is not a refusal basis, because different axes operate independently.
    /// </summary>
    public const string OtherAxisMovingObserved = "OTHER_AXIS_MOVING_OBSERVED";

    /// <summary>A fresh observation shows the axis moving.</summary>
    public const string AxisMovingObserved = "AXIS_MOVING_OBSERVED";

    /// <summary>No fresh status observation exists for the axis.</summary>
    public const string ObservationNotFresh = "OBSERVATION_NOT_FRESH";

    /// <summary>The motion status bit is not configured, so motion cannot be supervised.</summary>
    public const string MotionFeedbackNotConfigured = "MOTION_FEEDBACK_NOT_CONFIGURED";

    /// <summary>The home status bit is not configured, so Home cannot be supervised.</summary>
    public const string HomeFeedbackNotConfigured = "HOME_FEEDBACK_NOT_CONFIGURED";

    /// <summary>The brake output of the axis is not configured.</summary>
    public const string BrakeOutputNotConfigured = "BRAKE_OUTPUT_NOT_CONFIGURED";

    /// <summary>The brake release action (polarity) of the axis is not configured.</summary>
    public const string BrakePolarityNotConfigured = "BRAKE_POLARITY_NOT_CONFIGURED";

    /// <summary>The brake settle time of the axis is not configured.</summary>
    public const string BrakeSettleNotConfigured = "BRAKE_SETTLE_NOT_CONFIGURED";

    /// <summary>The jog speed of the axis is not configured.</summary>
    public const string JogSpeedNotConfigured = "JOG_SPEED_NOT_CONFIGURED";

    /// <summary>The jog heartbeat timeout or the maximum hold duration is not configured.</summary>
    public const string JogTimingNotConfigured = "JOG_TIMING_NOT_CONFIGURED";

    /// <summary>The Stop observation timeout of the axis is not configured.</summary>
    public const string StopTimeoutNotConfigured = "STOP_TIMEOUT_NOT_CONFIGURED";

    /// <summary>The observation interval or freshness window is not configured.</summary>
    public const string ObservationTimingNotConfigured = "OBSERVATION_TIMING_NOT_CONFIGURED";

    /// <summary>The home speed or home timeout of the axis is not configured.</summary>
    public const string HomeParametersNotConfigured = "HOME_PARAMETERS_NOT_CONFIGURED";

    /// <summary>A fresh observation shows the home input already active; Home is refused.</summary>
    public const string HomeInputAlreadyActive = "HOME_INPUT_ALREADY_ACTIVE";

    /// <summary>The position tolerance of the axis is not configured.</summary>
    public const string PositionToleranceNotConfigured = "POSITION_TOLERANCE_NOT_CONFIGURED";

    /// <summary>The move acceleration, speed, deceleration or timeout of the axis is not configured.</summary>
    public const string MoveParametersNotConfigured = "MOVE_PARAMETERS_NOT_CONFIGURED";

    /// <summary>The commissioned move bounds of the axis are not configured.</summary>
    public const string MoveBoundsNotConfigured = "MOVE_BOUNDS_NOT_CONFIGURED";

    /// <summary>The move target is outside the commissioned native-unit bounds.</summary>
    public const string MoveTargetOutOfBounds = "MOVE_TARGET_OUT_OF_BOUNDS";

    /// <summary>Absolute Move requires a verified Home of the axis in this session.</summary>
    public const string MoveRequiresHomed = "MOVE_REQUIRES_HOMED";

    /// <summary>The profile does not permit the dual-zero definition for the axis.</summary>
    public const string DualZeroNotPermittedByProfile = "DUAL_ZERO_NOT_PERMITTED_BY_PROFILE";

    /// <summary>The request was ignored because it does not match the active hold-to-run press.</summary>
    public const string JogPressNotActive = "JOG_PRESS_NOT_ACTIVE";

    // ---------------------------------------------------------------------------- outcomes

    /// <summary>The brake release action was dispatched without refusal (command state only).</summary>
    public const string BrakeReleaseCommanded = "BRAKE_RELEASE_COMMANDED";

    /// <summary>The brake apply action was dispatched without refusal (command state only).</summary>
    public const string BrakeApplyCommanded = "BRAKE_APPLY_COMMANDED";

    /// <summary>The brake release command was refused; the brake command state is uncertain.</summary>
    public const string BrakeReleaseRefused = "BRAKE_RELEASE_REFUSED";

    /// <summary>The brake apply command was refused; the brake command state is uncertain.</summary>
    public const string BrakeApplyRefused = "BRAKE_APPLY_REFUSED";

    /// <summary>Jog ended because the hold-to-run control was released.</summary>
    public const string JogStoppedOnRelease = "JOG_STOPPED_ON_RELEASE";

    /// <summary>Jog ended because the pointer capture was lost.</summary>
    public const string JogStoppedCaptureLost = "JOG_STOPPED_CAPTURE_LOST";

    /// <summary>Jog ended because the application was deactivated.</summary>
    public const string JogStoppedDeactivated = "JOG_STOPPED_DEACTIVATED";

    /// <summary>Jog ended because no heartbeat arrived within the configured timeout.</summary>
    public const string JogStoppedHeartbeatExpired = "JOG_STOPPED_HEARTBEAT_EXPIRED";

    /// <summary>Jog ended because the configured maximum hold duration was reached.</summary>
    public const string JogStoppedMaximumHold = "JOG_STOPPED_MAXIMUM_HOLD";

    /// <summary>Jog ended because the Operator pressed Stop.</summary>
    public const string JogStoppedByOperator = "JOG_STOPPED_BY_OPERATOR";

    /// <summary>Jog ended because the session is closing.</summary>
    public const string JogStoppedSessionClosing = "JOG_STOPPED_SESSION_CLOSING";

    /// <summary>The operation ended before any begin command was dispatched.</summary>
    public const string AbortedBeforeMotion = "ABORTED_BEFORE_MOTION";

    /// <summary>A typed motion command was refused; the operation ended through its cleanup path.</summary>
    public const string MotionCommandRefused = "MOTION_COMMAND_REFUSED";

    /// <summary>A standalone typed Stop was dispatched without refusal (dispatch only; not confirmation).</summary>
    public const string StopIssued = "STOP_ISSUED";

    /// <summary>The typed Stop was refused.</summary>
    public const string StopRefused = "STOP_REFUSED";

    /// <summary>After the Stop no observation showed the axis not moving within the configured timeout.</summary>
    public const string StopNotObservedWithinTimeout = "STOP_NOT_OBSERVED_WITHIN_TIMEOUT";

    /// <summary>Home completed and zero was verified by position reads (Owner-attested interpretation).</summary>
    public const string HomeCompletedZeroVerified = "HOME_COMPLETED_ZERO_VERIFIED";

    /// <summary>Home completed but zero could not be verified.</summary>
    public const string HomeCompletedZeroNotVerified = "HOME_COMPLETED_ZERO_NOT_VERIFIED";

    /// <summary>Home ended because the zero verification failed.</summary>
    public const string HomeZeroVerificationFailed = "HOME_ZERO_VERIFICATION_FAILED";

    /// <summary>Home ended because the configured home timeout elapsed.</summary>
    public const string HomeTimeout = "HOME_TIMEOUT";

    /// <summary>Home ended because the Operator pressed Stop.</summary>
    public const string HomeAbortedByOperator = "HOME_ABORTED_BY_OPERATOR";

    /// <summary>Home ended because the session is closing.</summary>
    public const string HomeAbortedSessionClosing = "HOME_ABORTED_SESSION_CLOSING";

    /// <summary>Define-position-zero was dispatched and verified by position reads.</summary>
    public const string PositionZeroVerified = "POSITION_ZERO_VERIFIED";

    /// <summary>Define-position-zero was dispatched but could not be verified.</summary>
    public const string PositionZeroNotVerified = "POSITION_ZERO_NOT_VERIFIED";

    /// <summary>Define-position-zero was dispatched but position reads were outside the tolerance.</summary>
    public const string PositionZeroVerificationFailed = "POSITION_ZERO_VERIFICATION_FAILED";

    /// <summary>Define-position-zero was refused.</summary>
    public const string PositionZeroRefused = "POSITION_ZERO_REFUSED";

    /// <summary>Define-dual-zero was dispatched; its effect is UNVERIFIED.</summary>
    public const string DualZeroDispatchedEffectUnverified = "DUAL_ZERO_DISPATCHED_EFFECT_UNVERIFIED";

    /// <summary>Define-dual-zero was refused.</summary>
    public const string DualZeroRefused = "DUAL_ZERO_REFUSED";

    /// <summary>
    /// The Home-carried reset-encoder sequence was refused at its dual-zero definition. The Home
    /// fails closed: no position read is accepted as a zero proof and the axis is not Homed.
    /// </summary>
    public const string HomeDualZeroRefused = "HOME_DUAL_ZERO_REFUSED";

    /// <summary>Move completed: not moving and both position reads within the tolerance of the target.</summary>
    public const string MoveCompletedPositionVerified = "MOVE_COMPLETED_POSITION_VERIFIED";

    /// <summary>Move ended because the configured motion timeout elapsed.</summary>
    public const string MoveTimeout = "MOVE_TIMEOUT";

    /// <summary>
    /// Move ended with the two position readings still disagreeing with EACH OTHER beyond the tolerance
    /// after both were normalized into the same reference (drive) domain. Reported separately from
    /// <see cref="MoveTimeout"/> and <see cref="MoveCompletedPositionVerified"/> so the Operator can tell a
    /// pulse-domain contradiction from a Move that simply did not arrive.
    /// </summary>
    public const string MoveRefusedTpRpDomainDisagreement = "MOVE_REFUSED_TP_RP_DOMAIN_DISAGREEMENT";

    /// <summary>Move ended because the Operator pressed Stop.</summary>
    public const string MoveAbortedByOperator = "MOVE_ABORTED_BY_OPERATOR";

    /// <summary>Move ended because the session is closing.</summary>
    public const string MoveAbortedSessionClosing = "MOVE_ABORTED_SESSION_CLOSING";

    /// <summary>A manual define clears the session Home state of the axis.</summary>
    public const string CoordinateRedefinedHomeCleared = "COORDINATE_REDEFINED_HOME_CLEARED";

    /// <summary>The operation was accepted and started.</summary>
    public const string OperationStarted = "OPERATION_STARTED";

    /// <summary>The fixed redacted detail text for one code. Unknown codes map to a generic text.</summary>
    public static string DetailFor(string code) => code switch
    {
        ProfileAccepted => "The Owner-local profile was accepted. Values remain local and are never displayed.",
        ProfileNotProvided => "No profile was provided. Every motion-producing operation stays refused.",
        ProfileSimulationSourceRefusedForHardware => "The built-in simulation profile can never configure the hardware-capable path.",
        SessionOpened => "The port is open. Planned is not authorized: hardware use needs its separate Scope Gate.",
        SessionOpenRefused => "The port open was refused. Nothing was commanded.",
        SessionFaulted => "A communication or native fault occurred. Nothing is retried and nothing reconnects; close the session.",
        SessionClosed => "The port was closed and cleanup was proven.",
        SessionCloseIncomplete => "The port was closed but cleanup was not fully proven.",
        StaleResultRejected => "A result from an earlier generation was rejected and not applied.",
        CommunicationFault => "A port call reported a communication or native fault.",
        ObservationRefused => "A read returned no usable value. Nothing was fabricated.",
        BrakeReleaseCommanded => "The configured release action was dispatched. Command state only: the physical brake is not confirmed.",
        BrakeApplyCommanded => "The configured apply action was dispatched. Command state only: the physical brake is not confirmed.",
        StopIssued => "The typed Stop was dispatched. Dispatch is not a confirmed stop.",
        StopNotObservedWithinTimeout => "No observation showed the axis not moving within the configured timeout. The brake was not applied automatically.",
        HomeCompletedZeroVerified => "Home completed and zero was verified by position reads under the Owner-attested profile interpretation. Not hardware evidence.",
        HomeCompletedZeroNotVerified => "Home completed but zero could not be verified.",
        DualZeroDispatchedEffectUnverified => "The dual-zero definition was dispatched. Its effect is UNVERIFIED.",
        HomeDualZeroRefused => "The Home-carried reset-encoder sequence was refused at its dual-zero definition. The Home failed closed and no zero was verified.",
        MoveCompletedPositionVerified => "Move completed: not moving and both position reads within the configured tolerance of the target.",
        MoveRefusedTpRpDomainDisagreement => "Move ended with the normalized theoretical and reference positions still disagreeing beyond the tolerance. No point completed.",
        CoordinateRedefinedHomeCleared => "A manual coordinate definition cleared the session Home state of the axis.",
        _ => "See the code. No command text, reply text or native detail is recorded.",
    };
}
