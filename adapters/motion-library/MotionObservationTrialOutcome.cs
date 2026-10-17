namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical vocabulary of the B2-H one-shot read-only observation trial.
///
/// The trial adds NO new controller capability. It is the composition that binds the already
/// authorized B2 read boundary to one already-open controller handle for exactly one deterministic
/// pass over the fixed sixteen typed read intents, and then invalidates the boundary before the
/// lifecycle owner closes the handle and releases the native modules.
///
/// Public-repository boundary: this file carries no endpoint, no serial, no native path, no hash,
/// no request text and no vendor error string. The endpoint value and the serial number remain
/// Owner-local external values.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public static class MotionObservationTrialVocabulary
{
    /// <summary>Success: one open, sixteen accepted reads, boundary invalidated, close and unload proven.</summary>
    public const string TrialComplete = "OBSERVATION_TRIAL_COMPLETE";

    /// <summary>The single open returned the vendor success result.</summary>
    public const string OpenSucceeded = "GCLIB_SUCCESS";

    /// <summary>The one approved B2 read export was absent, so no read was issued and nothing was opened.</summary>
    public const string ObservationBoundaryUnavailable = "OBSERVATION_BOUNDARY_UNAVAILABLE";

    /// <summary>The closed read plan and the session model disagree; no read was issued.</summary>
    public const string ObservationPlanMismatch = "OBSERVATION_PLAN_MISMATCH";

    /// <summary>The trial session has already run. It is one-shot and never retried.</summary>
    public const string TrialAlreadyRun = "OBSERVATION_TRIAL_ALREADY_RUN";

    /// <summary>
    /// An unexpected managed failure ended the trial before a result existed. Cleanup still ran;
    /// the outcome carries the measured cleanup state and no stack trace.
    /// </summary>
    public const string TrialAborted = "OBSERVATION_TRIAL_ABORTED";

    /// <summary>Outcome details must never carry an endpoint, a serial, a path, a hash or vendor error text.</summary>
    public const string DetailRedactionRequirement =
        "Outcome members must never contain an endpoint, a host, a serial number, a directory, a " +
        "path, a hash, a request text, a vendor error string or managed exception internals.";
}

/// <summary>
/// One bounded observation of the one-shot trial: the deterministic position in the fixed plan,
/// the canonical result word, the generation it was evaluated on, and the observed state when one
/// was produced.
///
/// The state carries only the already approved model fields (kind, subject, native value text,
/// native unit, interpretation word, detail code). No request text, no vendor text and no
/// engineering conversion is added here.
/// </summary>
public sealed record MotionObservationTrialReading
{
    /// <summary>One-based position in the fixed sixteen-intent plan.</summary>
    public required int Ordinal { get; init; }

    /// <summary>The canonical observation code (accepted word or the refusal that replaced it).</summary>
    public required string Code { get; init; }

    /// <summary>True only when one bounded value was accepted for this intent.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The observation-session generation this reading was evaluated on.</summary>
    public required long Generation { get; init; }

    /// <summary>The observed state, or null when no state exists for this intent.</summary>
    public MotionObservedState? State { get; init; }
}

/// <summary>
/// The redacted result of one B2-H one-shot observation trial.
///
/// The outcome tells the truth about the whole lifecycle: the single open classification, one
/// reading per executed intent, the generation, the B2 boundary invalidation, the controller-handle
/// close and the native-module release. A refused trial never carries a partial pass as complete.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionObservationTrialOutcome
{
    /// <summary>True only when every step passed: proof, one open, sixteen accepted reads, cleanup.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The success code or the single canonical refusal code.</summary>
    public required string Code { get; init; }

    /// <summary>Redacted narrative. Never an endpoint, a serial, a path, a hash or vendor error text.</summary>
    public required string Detail { get; init; }

    /// <summary>The expected logical identity this trial is prepared for (MC-WSB1).</summary>
    public required string ExpectedLogicalIdentity { get; init; }

    /// <summary>The single open classification: the success word or the canonical refusal word.</summary>
    public required string OpenClassification { get; init; }

    /// <summary>The observation-session generation the pass ran on.</summary>
    public required long Generation { get; init; }

    /// <summary>True when no usable observation boundary remains (invalidated before the close).</summary>
    public required bool ObservationBoundaryInvalidated { get; init; }

    /// <summary>True when the controller handle was closed and confirmed zeroed.</summary>
    public required bool ControllerHandleClosed { get; init; }

    /// <summary>True when every native module handle the trial loaded was released deterministically.</summary>
    public required bool NativeHandlesReleased { get; init; }

    /// <summary>The readings of the executed intents, in plan order. Empty when nothing was read.</summary>
    public IReadOnlyList<MotionObservationTrialReading> Readings { get; init; } =
        Array.Empty<MotionObservationTrialReading>();

    /// <summary>Refusal: fail closed with the canonical code and a redacted detail.</summary>
    public static MotionObservationTrialOutcome Refused(
        string code,
        string detail,
        string openClassification,
        long generation,
        bool observationBoundaryInvalidated,
        bool controllerHandleClosed,
        bool nativeHandlesReleased,
        IReadOnlyList<MotionObservationTrialReading>? readings = null) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        ExpectedLogicalIdentity = MotionControllerIdentityVocabulary.ExpectedLogicalIdentity,
        OpenClassification = openClassification,
        Generation = generation,
        ObservationBoundaryInvalidated = observationBoundaryInvalidated,
        ControllerHandleClosed = controllerHandleClosed,
        NativeHandlesReleased = nativeHandlesReleased,
        Readings = readings ?? Array.Empty<MotionObservationTrialReading>(),
    };

    /// <summary>Success: every planned read was accepted and every cleanup step was proven.</summary>
    public static MotionObservationTrialOutcome Complete(
        long generation,
        IReadOnlyList<MotionObservationTrialReading> readings,
        string detail) => new()
    {
        Accepted = true,
        Code = MotionObservationTrialVocabulary.TrialComplete,
        Detail = detail,
        ExpectedLogicalIdentity = MotionControllerIdentityVocabulary.ExpectedLogicalIdentity,
        OpenClassification = MotionObservationTrialVocabulary.OpenSucceeded,
        Generation = generation,
        ObservationBoundaryInvalidated = true,
        ControllerHandleClosed = true,
        NativeHandlesReleased = true,
        Readings = readings,
    };
}
