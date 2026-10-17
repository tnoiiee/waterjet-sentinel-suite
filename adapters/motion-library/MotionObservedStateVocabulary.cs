namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical vocabulary of the B2 read-only observed-state surface.
///
/// Authority split, as decided by the Owner for this checkpoint:
///
///   - The B1 identity surface is unchanged. It resolves exactly six typed identity exports,
///     issues no raw controller read and exposes no public command surface.
///   - The B2 observed-state surface is separately authorized to resolve ONE further native
///     command-and-response export for a closed set of READ-ONLY, typed, immutable intents. That
///     capability is private to this assembly: an intent is a type, never caller text, and the
///     request literals live in one internal plan whose targets are exactly the four Legacy-
///     evidenced reads.
///
/// This file carries the shape only: the four observation kinds, the closed axis set, the closed
/// output namespace, the interpretation words, the native unit descriptors and the refusal
/// vocabulary. It carries NO command mnemonic, NO request literal, NO buffer and NO parser; those
/// live in the internal plan and in the internal kernel, and neither is reachable from a public
/// member.
///
/// Honesty rule. A verified read is not a verified meaning. Two candidates keep their native
/// value with no interpretation adopted (<see cref="InterpretationRawObserved"/>) and two keep a
/// value whose machine meaning is not commissioned (<see cref="InterpretationUnconfirmed"/>).
/// Nothing in this surface can say limit, home, brake, fault, in-position or safe, and
/// <see cref="InterpretationVerified"/> stays unreachable because no candidate has both a proven
/// value and a proven meaning for this machine.
///
/// Public-repository boundary: no endpoint, host, port, coordinate, engineering constant, limit
/// value, response text or command mnemonic appears in this file, and none may.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public static class MotionObservedStateVocabulary
{
    // ---------------------------------------------------- observation kinds (typed allow-list)

    /// <summary>
    /// Candidate B2-OBS-1: the per-axis status word. Read-only, verified for this surface, and
    /// reported with an unconfirmed interpretation: the value is kept, no bit meaning is adopted.
    /// </summary>
    public const string KindAxisStatus = "AXIS_STATUS";

    /// <summary>
    /// Candidate B2-OBS-2: the first per-axis position value. Read-only, verified for this
    /// surface, and reported raw with no engineering meaning adopted.
    /// </summary>
    public const string KindCommandPosition = "COMMAND_POSITION";

    /// <summary>
    /// Candidate B2-OBS-3: the second per-axis position value. Read-only, verified for this
    /// surface, and reported raw with no engineering meaning adopted.
    /// </summary>
    public const string KindActualPosition = "ACTUAL_POSITION";

    /// <summary>
    /// Candidate B2-OBS-4: one digital output of the closed output namespace. Read-only, verified
    /// for this surface, and reported with an unconfirmed interpretation: the logical state is
    /// preserved and NO brake, valve or interlock meaning is attached to it.
    /// </summary>
    public const string KindDigitalOutput = "DIGITAL_OUTPUT";

    /// <summary>The complete and closed set of observation kinds. Read-only: it cannot grow at run time.</summary>
    public static IReadOnlyList<string> ObservationKinds { get; } = Array.AsReadOnly(new[]
    {
        KindAxisStatus,
        KindCommandPosition,
        KindActualPosition,
        KindDigitalOutput,
    });

    /// <summary>The documentary candidate identifier of <see cref="KindAxisStatus"/>.</summary>
    public const string CandidateAxisStatus = "B2-OBS-1";

    /// <summary>The documentary candidate identifier of <see cref="KindCommandPosition"/>.</summary>
    public const string CandidateCommandPosition = "B2-OBS-2";

    /// <summary>The documentary candidate identifier of <see cref="KindActualPosition"/>.</summary>
    public const string CandidateActualPosition = "B2-OBS-3";

    /// <summary>The documentary candidate identifier of <see cref="KindDigitalOutput"/>.</summary>
    public const string CandidateDigitalOutput = "B2-OBS-4";

    /// <summary>The complete and closed set of candidate identifiers, in kind order.</summary>
    public static IReadOnlyList<string> CandidateIds { get; } = Array.AsReadOnly(new[]
    {
        CandidateAxisStatus,
        CandidateCommandPosition,
        CandidateActualPosition,
        CandidateDigitalOutput,
    });

    /// <summary>
    /// The candidates the Owner-authorized B2 read surface implements. All four are read-only,
    /// typed, length-bounded, vendor-documented and reachable only through the internal plan, so
    /// the verified subset is non-empty and complete for this checkpoint.
    /// </summary>
    public static IReadOnlyList<string> VerifiedForB2Candidates { get; } = Array.AsReadOnly(new[]
    {
        CandidateAxisStatus,
        CandidateCommandPosition,
        CandidateActualPosition,
        CandidateDigitalOutput,
    });

    /// <summary>
    /// The candidates that remain blocked. Empty for this checkpoint: every Legacy read candidate
    /// passed the read-only, bounded, vendor-documented and typed-intent review. A future
    /// candidate that fails any of those criteria is listed here, never quietly implemented.
    /// </summary>
    public static IReadOnlyList<string> BlockedCandidates { get; } = Array.AsReadOnly(Array.Empty<string>());

    // ------------------------------------------------- controller axis letters (closed set)

    /// <summary>Controller axis A. Diagnostic identity only; the role comes from the topology catalogue.</summary>
    public const string AxisA = "A";

    /// <summary>Controller axis B. Diagnostic identity only.</summary>
    public const string AxisB = "B";

    /// <summary>Controller axis C. Diagnostic identity only.</summary>
    public const string AxisC = "C";

    /// <summary>Controller axis D. Diagnostic identity only.</summary>
    public const string AxisD = "D";

    /// <summary>The complete and closed set of controller axis letters.</summary>
    public static IReadOnlyList<string> AxisIds { get; } = Array.AsReadOnly(new[]
    {
        AxisA,
        AxisB,
        AxisC,
        AxisD,
    });

    /// <summary>No axis: the subject of a non-axis observation. Never a valid axis identifier.</summary>
    public const string NoAxis = "";

    // --------------------------------------------------- digital output namespace (closed set)

    /// <summary>
    /// Initial output of the closed output namespace. The namespace is the Owner-confirmed brake
    /// allocation A→1, B→2, C→3, D→4 of the commissioning foundation; it is a numbering fact, not
    /// a claim about any other controller function and not an electrical polarity statement.
    /// </summary>
    public const int MinimumOutputNumber = 1;

    /// <summary>Final output of the closed output namespace (see <see cref="MinimumOutputNumber"/>).</summary>
    public const int MaximumOutputNumber = 4;

    // ------------------------------------------------------- interpretation states (closed set)

    /// <summary>RESERVED: the observed value and its meaning are both proven. Never produced: no candidate has a commissioned meaning.</summary>
    public const string InterpretationVerified = "VERIFIED";

    /// <summary>A value was read and preserved with no meaning assigned. Produced for the two position reads.</summary>
    public const string InterpretationRawObserved = "RAW_OBSERVED";

    /// <summary>A value was read but its machine meaning (bit order, polarity, wiring) is unconfirmed. Produced for the status word and the output state.</summary>
    public const string InterpretationUnconfirmed = "UNCONFIRMED";

    /// <summary>A valid subject has no accepted observation: no read boundary is attached, or the response was not usable. Carries no value.</summary>
    public const string InterpretationUnavailable = "UNAVAILABLE";

    /// <summary>RESERVED: a result arrived for a previous session generation and was rejected. Never produced as a state word.</summary>
    public const string InterpretationStale = "STALE";

    /// <summary>The request was not allowed to proceed at all: it produced no observation subject.</summary>
    public const string InterpretationRefused = "REFUSED";

    /// <summary>The complete and closed interpretation vocabulary, in model order.</summary>
    public static IReadOnlyList<string> InterpretationStates { get; } = Array.AsReadOnly(new[]
    {
        InterpretationVerified,
        InterpretationRawObserved,
        InterpretationUnconfirmed,
        InterpretationUnavailable,
        InterpretationStale,
        InterpretationRefused,
    });

    /// <summary>
    /// The only interpretation states an outcome can carry. Pinned so a later change that starts
    /// producing a stronger state is a visible, reviewed decision rather than a quiet drift.
    /// <see cref="InterpretationVerified"/> and <see cref="InterpretationStale"/> are absent: no
    /// candidate reaches a proven meaning, and a stale result is refused instead of reported.
    /// </summary>
    public static IReadOnlyList<string> ReachableInterpretationStates { get; } = Array.AsReadOnly(new[]
    {
        InterpretationRawObserved,
        InterpretationUnconfirmed,
        InterpretationUnavailable,
        InterpretationRefused,
    });

    // ------------------------------------------------------------------- native unit handling

    /// <summary>No native value exists, so no unit applies. Never paired with a raw value.</summary>
    public const string NativeUnitNone = "NONE";

    /// <summary>
    /// The value stays in controller units (counts) and is never converted to millimetres,
    /// degrees or a percentage by this surface.
    /// </summary>
    public const string NativeUnitControllerUnits = "CONTROLLER_UNITS";

    /// <summary>The value is a status bit field in the controller's own encoding; no bit meaning is assigned.</summary>
    public const string NativeUnitStatusBits = "STATUS_BITS";

    /// <summary>The value is a logical output state as the controller reports it; no wiring or polarity meaning is assigned.</summary>
    public const string NativeUnitLogicalState = "LOGICAL_STATE";

    // ----------------------------------------------------------------------- refusal vocabulary

    /// <summary>
    /// No read boundary is attached to this session. The request was valid, but nothing was issued
    /// and no value was obtained: the subject is reported UNAVAILABLE, never zero and never
    /// "inactive".
    /// </summary>
    public const string ObservationSurfaceNotAuthorized = "OBSERVATION_SURFACE_NOT_AUTHORIZED";

    /// <summary>The requested kind is not one of the four typed kinds. Nothing was issued.</summary>
    public const string ObservationKindUnknown = "OBSERVATION_KIND_UNKNOWN";

    /// <summary>The requested axis is not one of the four controller axis letters. Nothing was issued.</summary>
    public const string ObservationAxisUnknown = "OBSERVATION_AXIS_UNKNOWN";

    /// <summary>The subject combination is invalid (axis and output together, or an output outside the closed namespace).</summary>
    public const string ObservationSubjectInvalid = "OBSERVATION_SUBJECT_INVALID";

    /// <summary>The session is closed. A closed session accepts no further request and never reopens.</summary>
    public const string ObservationSessionClosed = "OBSERVATION_SESSION_CLOSED";

    /// <summary>A second observation was requested while one was outstanding. Fail-fast refusal; no queue.</summary>
    public const string OperationBusy = "OBSERVATION_OPERATION_BUSY";

    /// <summary>A result arrived for a previous session generation and was rejected as stale.</summary>
    public const string StaleGenerationResult = "OBSERVATION_GENERATION_STALE";

    /// <summary>A result was already accepted for the current generation; a replay is refused.</summary>
    public const string ResultReplayed = "OBSERVATION_RESULT_REPLAYED";

    /// <summary>The read boundary was invalidated before the request. Nothing was issued and nothing can be issued again.</summary>
    public const string ObservationKernelClosed = "OBSERVATION_KERNEL_CLOSED";

    /// <summary>The read boundary has no controller handle or no resolved export. Nothing was issued.</summary>
    public const string ObservationKernelUnbound = "OBSERVATION_KERNEL_UNBOUND";

    /// <summary>The typed intent is not a member of the closed request plan. Nothing was issued.</summary>
    public const string ObservationIntentUnmapped = "OBSERVATION_INTENT_UNMAPPED";

    /// <summary>The native call did not report success. No vendor result name, error text or exception detail is surfaced.</summary>
    public const string ObservationCommandFailed = "OBSERVATION_COMMAND_FAILED";

    /// <summary>The response carried no value before its terminator. No value was accepted.</summary>
    public const string ObservationResponseEmpty = "OBSERVATION_RESPONSE_EMPTY";

    /// <summary>The response exceeded the fixed value-text bound. No value was accepted.</summary>
    public const string ObservationResponseTooLong = "OBSERVATION_RESPONSE_TOO_LONG";

    /// <summary>The response buffer was not terminated inside its fixed bound, so it may be truncated. No value was accepted.</summary>
    public const string ObservationResponseUnterminated = "OBSERVATION_RESPONSE_UNTERMINATED";

    /// <summary>The response carried a byte outside printable ASCII. No value was accepted.</summary>
    public const string ObservationResponseNotPrintable = "OBSERVATION_RESPONSE_NOT_PRINTABLE";

    /// <summary>The response carried more than one value (a comma-separated reply). No value was accepted.</summary>
    public const string ObservationResponseMultiValue = "OBSERVATION_RESPONSE_MULTI_VALUE";

    /// <summary>The response carried the controller error marker. No value was accepted.</summary>
    public const string ObservationResponseControllerError = "OBSERVATION_RESPONSE_CONTROLLER_ERROR";

    /// <summary>The response was not one bounded decimal numeric token (including any hexadecimal-format reply). No value was accepted.</summary>
    public const string ObservationResponseInvalid = "OBSERVATION_RESPONSE_INVALID";

    /// <summary>The reported logical output value was not exactly zero or exactly one. No value was accepted and nothing was rounded.</summary>
    public const string ObservationOutputStateOutOfRange = "OBSERVATION_OUTPUT_STATE_OUT_OF_RANGE";

    // ------------------------------------------------------------------- acceptance vocabulary

    /// <summary>One bounded, validated observation was accepted and is carried by the outcome.</summary>
    public const string ObservationAccepted = "OBSERVATION_ACCEPTED";

    /// <summary>The accepted state is a status bit field whose meaning is not commissioned. The value is preserved; no bit is interpreted.</summary>
    public const string ObservationStatusWordRaw = "OBSERVATION_STATUS_WORD_RAW";

    /// <summary>The accepted state is a position value in controller units. The value is preserved; no engineering meaning is adopted.</summary>
    public const string ObservationPositionValueRaw = "OBSERVATION_POSITION_VALUE_RAW";

    /// <summary>The accepted state is a logical output state. The value is preserved; no wiring, polarity or brake meaning is assigned.</summary>
    public const string ObservationOutputStateUnconfirmed = "OBSERVATION_OUTPUT_STATE_UNCONFIRMED";

    /// <summary>Outcome details never carry an endpoint, a host, a port, a coordinate, a value, a path or exception internals.</summary>
    public const string DetailRedactionRequirement =
        "Outcome Detail members never contain an endpoint, a host, a port, a coordinate, a raw " +
        "observed value, a directory, a path, a configured engineering constant or managed " +
        "exception internals.";
}
