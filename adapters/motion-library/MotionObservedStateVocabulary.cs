namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical vocabulary of the B2 read-only observed-state foundation.
///
/// This surface commits SHAPE ONLY: a closed, typed list of observation kinds, the controller
/// axis letters, the closed output namespace of the Owner-confirmed brake allocation, the
/// interpretation states and the refusal words. It contains NO vendor command mnemonic, NO
/// command builder and NO response parser, because no observation is authorized in this
/// revision: every B2 candidate needs a read path that the B1 gate does not authorize (the B1
/// authority line is "no raw controller command, no axis read"), so the executable surface has
/// no way to issue one and no way to interpret a response.
///
/// Four observation kinds are declared because they are the four read candidates the Legacy
/// baseline contains, and a state model must exist before any read is authorized. Every one of
/// them is BLOCKED at this checkpoint: see
/// <c>docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md</c> section B and
/// <c>docs/MOTION_OBSERVED_STATE_FOUNDATION.md</c>. Blocked is a disposition about the read
/// surface, not about the model: the model still has to be able to say "unavailable" truthfully
/// and must never be able to say "verified" without evidence.
///
/// The interpretation vocabulary is complete for the future read contract, but only
/// <see cref="InterpretationUnavailable"/> and <see cref="InterpretationRefused"/> are reachable
/// in this revision (<see cref="ReachableInterpretationStates"/>). <see cref="InterpretationVerified"/>,
/// <see cref="InterpretationRawObserved"/>, <see cref="InterpretationUnconfirmed"/> and
/// <see cref="InterpretationStale"/> are RESERVED: they are declared so a future authorized read
/// cannot silently invent a different spelling, and they are never produced here. An unknown
/// state is never reported as safe, inactive, zero, home, limit or brake: the model has no such
/// member at all.
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
    /// Candidate B2-OBS-1: the per-axis status word. BLOCKED — the read needs a raw command path
    /// that the B1 gate does not authorize.
    /// </summary>
    public const string KindAxisStatus = "AXIS_STATUS";

    /// <summary>
    /// Candidate B2-OBS-2: the per-axis commanded (theoretical) position. BLOCKED — same reason.
    /// </summary>
    public const string KindCommandPosition = "COMMAND_POSITION";

    /// <summary>
    /// Candidate B2-OBS-3: the per-axis actual (encoder) position. BLOCKED — same reason.
    /// </summary>
    public const string KindActualPosition = "ACTUAL_POSITION";

    /// <summary>
    /// Candidate B2-OBS-4: one digital output of the closed output namespace. BLOCKED — same
    /// reason. The kind carries an output number and NO brake, valve or interlock meaning.
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
    /// First output of the closed output namespace. The namespace is the Owner-confirmed brake
    /// allocation A→1, B→2, C→3, D→4 of the commissioning foundation; it is a numbering fact, not
    /// a claim about any other controller function and not an electrical polarity statement.
    /// </summary>
    public const int MinimumOutputNumber = 1;

    /// <summary>Last output of the closed output namespace (see <see cref="MinimumOutputNumber"/>).</summary>
    public const int MaximumOutputNumber = 4;

    // ------------------------------------------------------- interpretation states (closed set)

    /// <summary>RESERVED: the observed value and its meaning are both proven. Never produced in this revision.</summary>
    public const string InterpretationVerified = "VERIFIED";

    /// <summary>RESERVED: a value was read and preserved verbatim, with no meaning assigned. Never produced in this revision.</summary>
    public const string InterpretationRawObserved = "RAW_OBSERVED";

    /// <summary>RESERVED: a value was read but its meaning (bit order, polarity, unit) is unconfirmed. Never produced in this revision.</summary>
    public const string InterpretationUnconfirmed = "UNCONFIRMED";

    /// <summary>A valid subject has no observation: the read surface is not authorized. The only non-refusal state produced here.</summary>
    public const string InterpretationUnavailable = "UNAVAILABLE";

    /// <summary>RESERVED: a result arrived for a previous session generation and was rejected. Never produced in this revision.</summary>
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
    /// The only two interpretation states any outcome of this revision can carry. Pinned so a
    /// later change that starts producing a stronger state must be a visible, reviewed decision.
    /// </summary>
    public static IReadOnlyList<string> ReachableInterpretationStates { get; } = Array.AsReadOnly(new[]
    {
        InterpretationUnavailable,
        InterpretationRefused,
    });

    // ------------------------------------------------------------------- native unit handling

    /// <summary>No native value exists, so no unit applies. Never paired with a raw value.</summary>
    public const string NativeUnitNone = "NONE";

    /// <summary>
    /// Reserved for a future authorized read: the value stays in controller units and is never
    /// converted to millimetres, degrees or a percentage by this surface.
    /// </summary>
    public const string NativeUnitControllerUnits = "CONTROLLER_UNITS";

    // ----------------------------------------------------------------------- refusal vocabulary

    /// <summary>
    /// No authorized read surface exists. The request was valid, but nothing was issued and no
    /// value was obtained: the subject is reported UNAVAILABLE, never zero and never "inactive".
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

    /// <summary>Outcome details must never carry an endpoint, a host, a port, a coordinate, a value, a path or exception internals.</summary>
    public const string DetailRedactionRequirement =
        "Outcome Detail members must never contain an endpoint, a host, a port, a coordinate, a " +
        "raw observed value, a directory, a path, a configured engineering constant or managed " +
        "exception internals.";
}
