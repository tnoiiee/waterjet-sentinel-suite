namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// One observed state of one subject: what kind of observation it is, which subject it belongs
/// to, how far the interpretation got, in which native unit, and why it is in that state.
///
/// The type has NO public constructor and NO public factory. Instances come from exactly two
/// internal factories: <see cref="Unavailable"/> for a subject with no accepted observation, and
/// <see cref="Observed"/> for one bounded, validated read. Both take the interpretation word from
/// the session, never from a caller, so a consumer of this assembly cannot construct a state that
/// claims a stronger interpretation than the read boundary actually produced.
///
/// There is intentionally NO member for "in position", "limit active", "home active", "brake
/// released", "fault" or "safe": those meanings are unconfirmed for this controller family
/// (see the evidence baseline section G), and an unknown state is never read as inactive, safe or
/// zero. The verified subset carries native values only, and the interpretation word says so.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionObservedState
{
    private MotionObservedState()
    {
    }

    /// <summary>One of <see cref="MotionObservedStateVocabulary.ObservationKinds"/>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The controller axis letter, or empty for a non-axis subject.</summary>
    public string AxisId { get; init; } = MotionObservedStateVocabulary.NoAxis;

    /// <summary>The output number inside the closed namespace, or null for an axis subject.</summary>
    public int? OutputNumber { get; init; }

    /// <summary>
    /// One of <see cref="MotionObservedStateVocabulary.InterpretationStates"/>. In this revision
    /// only <see cref="MotionObservedStateVocabulary.InterpretationUnavailable"/> is reachable.
    /// </summary>
    public string InterpretationState { get; init; } = MotionObservedStateVocabulary.InterpretationUnavailable;

    /// <summary>
    /// The native unit of <see cref="NativeValueText"/>; <see cref="MotionObservedStateVocabulary.NativeUnitNone"/>
    /// while no value exists. A future read keeps controller units and never converts here.
    /// </summary>
    public string NativeUnit { get; init; } = MotionObservedStateVocabulary.NativeUnitNone;

    /// <summary>
    /// The observed value as the controller reported it, normalized only by removing a single
    /// leading prompt character and the surrounding whitespace, or null when nothing was
    /// observed. It is a bounded numeric token: the boundary refuses anything else instead of
    /// repairing or truncating it.
    /// </summary>
    public string? NativeValueText { get; init; }

    /// <summary>The refusal or availability code that explains <see cref="InterpretationState"/>.</summary>
    public string DetailCode { get; init; } = string.Empty;

    /// <summary>
    /// The only state this revision can produce: the subject is valid, but no authorized read
    /// surface exists, so the observation is unavailable. No value, no unit and no meaning.
    /// </summary>
    internal static MotionObservedState Unavailable(
        string kind,
        string axisId,
        int? outputNumber,
        string detailCode) => new()
    {
        Kind = kind,
        AxisId = axisId,
        OutputNumber = outputNumber,
        InterpretationState = MotionObservedStateVocabulary.InterpretationUnavailable,
        NativeUnit = MotionObservedStateVocabulary.NativeUnitNone,
        NativeValueText = null,
        DetailCode = detailCode,
    };

    /// <summary>
    /// One accepted observation: a bounded native value text, the native unit descriptor and the
    /// interpretation word the read plan assigned. The factory never converts a unit, never
    /// derives a meaning and never accepts a stronger interpretation than the plan produced.
    /// </summary>
    internal static MotionObservedState Observed(
        string kind,
        string axisId,
        int? outputNumber,
        string nativeUnit,
        string nativeValueText,
        string interpretationState,
        string detailCode) => new()
    {
        Kind = kind,
        AxisId = axisId,
        OutputNumber = outputNumber,
        InterpretationState = interpretationState,
        NativeUnit = nativeUnit,
        NativeValueText = nativeValueText,
        DetailCode = detailCode,
    };
}

/// <summary>
/// One observation request: a typed kind plus its subject.
///
/// This is DATA, not a command. It carries no command text, no endpoint, no target position, no
/// speed and no value, and no member of it is ever formatted into controller text. The session
/// validates <see cref="Kind"/> and the subject against the closed vocabulary and refuses
/// anything outside it, so arbitrary caller text can never reach an observation path — there is
/// no observation path to reach.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionObservationRequest
{
    /// <summary>One of <see cref="MotionObservedStateVocabulary.ObservationKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>The controller axis letter for an axis kind; empty for a digital-output kind.</summary>
    public string AxisId { get; init; } = MotionObservedStateVocabulary.NoAxis;

    /// <summary>The output number for a digital-output kind; zero for an axis kind.</summary>
    public int OutputNumber { get; init; }
}

/// <summary>
/// The redacted result of one observation request.
///
/// One bounded outcome. <see cref="Accepted"/> is true only when one bounded, validated
/// observation was produced; <see cref="ObservationIssued"/> is true only when a read call was
/// actually attempted. The two are independent on purpose: an attempted read that returned an
/// unusable reply is reported as issued-but-not-accepted, never as a value.
///
/// <see cref="InterpretationState"/> is one of the four reachable words: REFUSED for a request
/// that was not allowed to proceed, UNAVAILABLE for a valid subject with no accepted observation,
/// RAW_OBSERVED or UNCONFIRMED for an accepted native value. <see cref="States"/> carries one
/// state for an accepted or unavailable result and is empty for a refusal.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionObservationOutcome
{
    /// <summary>True only when one bounded, validated observation was accepted.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The single code that names the refusal or the unavailability, in the canonical vocabulary.</summary>
    public required string Code { get; init; }

    /// <summary>Redacted narrative. Never an endpoint, a host, a port, a coordinate, a value, a path or exception internals.</summary>
    public required string Detail { get; init; }

    /// <summary>One of the four reachable words; see the type description.</summary>
    public required string InterpretationState { get; init; }

    /// <summary>The session generation the request was evaluated on.</summary>
    public required long Generation { get; init; }

    /// <summary>True only when a read call was actually attempted against the native boundary.</summary>
    public required bool ObservationIssued { get; init; }

    /// <summary>One unavailable state for a valid subject; empty when the request itself was refused.</summary>
    public IReadOnlyList<MotionObservedState> States { get; init; } = Array.Empty<MotionObservedState>();

    /// <summary>
    /// A valid request whose read surface is not authorized: the subject exists in the model, the
    /// observation does not. No value and no interpretation is attached.
    /// </summary>
    internal static MotionObservationOutcome Unavailable(
        string code,
        string detail,
        long generation,
        MotionObservedState state,
        bool observationIssued = false) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        InterpretationState = MotionObservedStateVocabulary.InterpretationUnavailable,
        Generation = generation,
        ObservationIssued = observationIssued,
        States = Array.AsReadOnly(new[] { state }),
    };

    /// <summary>A request that was not allowed to proceed: nothing was evaluated and no state exists.</summary>
    internal static MotionObservationOutcome Refused(
        string code,
        string detail,
        long generation,
        bool observationIssued = false) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        InterpretationState = MotionObservedStateVocabulary.InterpretationRefused,
        Generation = generation,
        ObservationIssued = observationIssued,
        States = Array.Empty<MotionObservedState>(),
    };

    /// <summary>
    /// One accepted observation: exactly one state, the interpretation word it carries, and the
    /// truthful issued flag. The interpretation word is taken from the state so the outcome can
    /// never claim a stronger reading than the state itself.
    /// </summary>
    internal static MotionObservationOutcome Observed(
        string code,
        string detail,
        long generation,
        MotionObservedState state) => new()
    {
        Accepted = true,
        Code = code,
        Detail = detail,
        InterpretationState = state.InterpretationState,
        Generation = generation,
        ObservationIssued = true,
        States = Array.AsReadOnly(new[] { state }),
    };
}
