namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// One observed state of one subject: what kind of observation it is, which subject it belongs
/// to, how far the interpretation got, in which native unit, and why it is in that state.
///
/// The type has NO public constructor and NO public factory. The only way to obtain an instance
/// in this revision is the internal <see cref="Unavailable"/> factory, so a caller cannot
/// construct a state that claims <see cref="MotionObservedStateVocabulary.InterpretationVerified"/>
/// or any other stronger interpretation. That is deliberate: a state model that can be built
/// from nowhere with an arbitrary interpretation would be a way to fabricate an observation
/// without a controller, and this revision has no authorization to observe anything at all.
///
/// There is intentionally NO member for "in position", "limit active", "home active", "brake
/// released", "fault" or "safe": those meanings are unconfirmed for this controller family
/// (see the evidence baseline section G), and an unknown state must never be read as inactive,
/// safe or zero. A future authorized read adds meaning only with the evidence that proves it.
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
    /// The observed value exactly as the controller reported it, or null when nothing was
    /// observed. This revision always produces null: no read is authorized, so no text exists.
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
/// One bounded outcome. <see cref="Accepted"/> is false for every outcome this revision can
/// produce, and <see cref="ObservationIssued"/> is false for every one of them as well: no
/// controller was contacted, no library was loaded and no value exists. The outcome therefore
/// reports the truth — refused, or valid-but-unavailable — instead of a placeholder value.
///
/// <see cref="InterpretationState"/> is one of exactly two words here,
/// <see cref="MotionObservedStateVocabulary.InterpretationRefused"/> for a request that was not
/// allowed to proceed, or <see cref="MotionObservedStateVocabulary.InterpretationUnavailable"/>
/// for a valid request whose read surface is not authorized. <see cref="States"/> carries one
/// unavailable state for the latter and is empty for the former.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionObservationOutcome
{
    /// <summary>False for every outcome of this revision: no observation can be accepted yet.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The single code that names the refusal or the unavailability, in the canonical vocabulary.</summary>
    public required string Code { get; init; }

    /// <summary>Redacted narrative. Never an endpoint, a host, a port, a coordinate, a value, a path or exception internals.</summary>
    public required string Detail { get; init; }

    /// <summary>REFUSED or UNAVAILABLE in this revision; see the type description.</summary>
    public required string InterpretationState { get; init; }

    /// <summary>The session generation the request was evaluated on.</summary>
    public required long Generation { get; init; }

    /// <summary>False for every outcome of this revision: nothing was sent to any controller.</summary>
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
        MotionObservedState state) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        InterpretationState = MotionObservedStateVocabulary.InterpretationUnavailable,
        Generation = generation,
        ObservationIssued = false,
        States = Array.AsReadOnly(new[] { state }),
    };

    /// <summary>A request that was not allowed to proceed: nothing was evaluated and no state exists.</summary>
    internal static MotionObservationOutcome Refused(
        string code,
        string detail,
        long generation) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        InterpretationState = MotionObservedStateVocabulary.InterpretationRefused,
        Generation = generation,
        ObservationIssued = false,
        States = Array.Empty<MotionObservedState>(),
    };
}
