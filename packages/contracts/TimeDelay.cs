namespace Wjss.Contracts;

/// <summary>
/// The two TimeDelay modes of H3-A, and the only two. There is deliberately no third
/// member and no "compatibility" mode: a live-mutating Legacy countdown, a two-signal
/// mapping, a weighted multi-signal mapping and any future compatibility mode are NOT
/// part of this contract.
/// </summary>
public enum TimeDelayMode
{
    /// <summary>One fixed configured delay. Ignores RHTemp_SELECT, the Boiler ranges and the Upper-zone factor.</summary>
    MANUAL_FIXED = 1,

    /// <summary>The Legacy inverse linear mapping of the single RHTemp_SELECT input, frozen per countdown.</summary>
    AUTO_RH_LINEAR_FROZEN = 2,
}

/// <summary>
/// The Boiler Condition vocabulary of the Legacy TimeDelay table. Four conditions, each with
/// its own Auto delay range. This is a water-condition classification, NOT the Sensor
/// <see cref="Classification"/> (DIRTY / CLEANER / NOT_CLASSIFIED), which is a different
/// dimension and is never used as a Boiler Condition.
/// </summary>
public enum BoilerCondition
{
    VERY_CLEAN = 1,
    NORMAL_CLEAN = 2,
    MEDIUM_DIRTY = 3,
    VERY_DIRTY = 4,
}

/// <summary>
/// The zone classification of the NEXT Queue Sensor, derived from its wall and its wall-local
/// order only. It is recorded for every decision, including a MANUAL_FIXED decision, so the
/// evidence never hides the geometry; the factor itself is applied in AUTO mode only.
/// </summary>
public enum DelayZone
{
    LOWER = 1,
    UPPER = 2,
}

/// <summary>
/// The lifecycle of the next-dispatch countdown. This is AutoSequence lifecycle, NEVER a
/// <see cref="QueueEntry"/> status: the GlobalQueue stays ready-only and no entry ever carries
/// a waiting, held, counting or dispatched state.
/// </summary>
public enum CountdownLifecycleState
{
    /// <summary>No countdown exists for this AutoSequence session.</summary>
    NONE = 0,

    /// <summary>A decision is being prepared. The two public factories publish ACTIVE_FROZEN directly.</summary>
    PENDING_DECISION = 1,

    /// <summary>The decision is frozen and the applied delay is counting down. Nothing may be mutated.</summary>
    ACTIVE_FROZEN = 2,

    /// <summary>The frozen applied delay reached zero. Exactly one dispatch is now admissible.</summary>
    COMPLETED_AWAITING_DISPATCH = 3,

    /// <summary>The head was dispatched once. A second dispatch of the same decision is impossible.</summary>
    DISPATCHED = 4,

    /// <summary>The countdown was cancelled (no dispatch may follow this decision).</summary>
    CANCELLED = 5,

    /// <summary>The frozen premise became invalid. A new decision needs a new generation and a full delay.</summary>
    SUPERSEDED = 6,

    /// <summary>The countdown cannot continue. Fail closed: no dispatch follows a faulted countdown.</summary>
    FAULTED = 7,
}

/// <summary>
/// Stable, machine-readable refusal codes for the TimeDelay decision, the countdown lifecycle
/// and the mutation guards. <see cref="NONE"/> means "no refusal" and is the only successful value.
/// </summary>
public enum CountdownRefusalCode
{
    NONE = 0,

    // Configuration validation.
    MODE_UNSUPPORTED,
    MANUAL_DELAY_OUT_OF_RANGE,
    RH_RANGE_INVALID,
    RH_INPUT_WINDOW_INVALID,
    AUTO_DELAY_RANGE_INVALID,
    AUTO_DELAY_ORDER_INVALID,
    BOILER_CONDITION_RANGE_MISSING,
    BOILER_CONDITION_UNKNOWN,
    BOILER_CONDITION_DUPLICATE,
    CONFIGURATION_VERSION_MISSING,

    // RH input validation.
    RH_MISSING,
    RH_STALE,
    RH_FUTURE_DATED,
    RH_BAD_QUALITY,
    RH_GENERATION_INVALID,
    RH_VALUE_INVALID,
    RH_TIMESTAMP_INVALID,

    // Decision premise validation.
    QUEUE_EMPTY,
    ACTIVE_JOB_PRESENT,
    SENSOR_IDENTITY_INVALID,
    ASSIGNMENT_DERIVATION_INVALID,
    QUEUE_HEAD_CHANGED,

    // Countdown lifecycle and guards.
    COUNTDOWN_GENERATION_INVALID,
    COUNTDOWN_NOT_ACTIVE,
    COUNTDOWN_NOT_COMPLETE,
    COUNTDOWN_ALREADY_ACTIVE,
    COUNTDOWN_ALREADY_DISPATCHED,
    COUNTDOWN_ALREADY_TERMINAL,
    COUNTDOWN_TIME_REGRESSION,
    DECISION_TIMESTAMP_INVALID,
    APPLIED_DELAY_INVALID,
    SUPERSESSION_REASON_REQUIRED,
    MUTATION_REFUSED_DURING_COUNTDOWN,
    CONFIGURATION_MISSING,
    CONFIGURATION_CHANGE_REFUSED_DURING_COUNTDOWN,
}

/// <summary>
/// Why a frozen countdown was superseded. A superseded countdown never dispatches; the next
/// decision starts a new generation and the FULL applied delay again.
/// </summary>
public enum CountdownSupersessionReason
{
    NONE = 0,

    /// <summary>A different entry, or a different Sensor, is now Position 1.</summary>
    QUEUE_HEAD_IDENTITY_CHANGED,

    /// <summary>The frozen head is no longer present in the GlobalQueue.</summary>
    QUEUE_HEAD_WITHDRAWN,

    /// <summary>The frozen head Sensor was Disabled.</summary>
    HEAD_SENSOR_DISABLED,

    /// <summary>The frozen head is no longer structurally eligible under the equipment topology.</summary>
    HEAD_STRUCTURALLY_INVALID,

    /// <summary>A critical Pump event latched: the AutoSequence is suspended.</summary>
    CRITICAL_PUMP_EVENT,

    /// <summary>The AutoSequence left RUNNING (paused or paused after the current Job).</summary>
    AUTOSEQUENCE_NOT_RUNNING,
}

/// <summary>
/// One Boiler Condition and its Auto delay range in whole seconds. <see cref="AutoDelayMinSeconds"/>
/// is the delay at <see cref="TimeDelayConfiguration.RhTempMax"/> and
/// <see cref="AutoDelayMaxSeconds"/> is the delay at <see cref="TimeDelayConfiguration.RhTempMin"/>:
/// the mapping is inverse and linear. Both bounds are configuration values; the four legacy
/// seeded ranges are SIMULATION seeds, never DCS commissioning values.
/// </summary>
public sealed record BoilerConditionDelayRange
{
    public required BoilerCondition Condition { get; init; }
    public required int AutoDelayMinSeconds { get; init; }
    public required int AutoDelayMaxSeconds { get; init; }
}

/// <summary>
/// The configured TimeDelay policy. Every field is frozen into a decision at creation time, so a
/// later configuration edit can never change an active countdown. The type carries no clock, no
/// device address and no Legacy identifier.
///
/// <para>
/// The optional <see cref="LegacySeededSimulationDefaults"/> factory is the single place the
/// Owner-supplied Legacy Rev2 seeded values live: VeryClean 600 / 660, NormalClean 500 / 550,
/// MediumDirty 340 / 380, VeryDirty 240 / 280, RHTemp 515 / 535. They are SIMULATION values for
/// this stage and are explicitly NOT new DCS commissioning values.
/// </para>
/// </summary>
public sealed record TimeDelayConfiguration
{
    /// <summary>Label of the legacy-seeded simulation configuration; never a Production revision.</summary>
    public const string LegacySeededSimulationVersion = "SIM-H3A-LEGACY-SEED-1";

    /// <summary>The single RHTemp_SELECT lower bound of the legacy-seeded simulation configuration.</summary>
    public const double LegacySeededRhTempMin = 515.0;

    /// <summary>The single RHTemp_SELECT upper bound of the legacy-seeded simulation configuration.</summary>
    public const double LegacySeededRhTempMax = 535.0;

    /// <summary>The legacy-seeded RH input window in whole seconds (a simulation window, not a verified timing).</summary>
    public const int LegacySeededRhInputWindowSeconds = 30;

    public required TimeDelayMode Mode { get; init; }

    /// <summary>The one fixed delay of MANUAL_FIXED, in whole seconds. Ignored in AUTO mode.</summary>
    public required int ManualDelaySeconds { get; init; }

    /// <summary>The lower bound of the single RHTemp_SELECT range.</summary>
    public required double RhTempMin { get; init; }

    /// <summary>The upper bound of the single RHTemp_SELECT range. Must be strictly greater than <see cref="RhTempMin"/>.</summary>
    public required double RhTempMax { get; init; }

    /// <summary>How old an RH reading may be at decision time before Auto is refused as stale.</summary>
    public required int RhInputWindowSeconds { get; init; }

    /// <summary>The Boiler Condition in force. It must have a range in <see cref="BoilerRanges"/>.</summary>
    public required BoilerCondition ActiveBoilerCondition { get; init; }

    /// <summary>The configured Auto delay range of every Boiler Condition.</summary>
    public required IReadOnlyList<BoilerConditionDelayRange> BoilerRanges { get; init; }

    /// <summary>Configuration identity frozen into the decision.</summary>
    public required string ConfigurationVersion { get; init; }

    /// <summary>
    /// The Owner-supplied Legacy Rev2 seeded simulation values, in the one place they are written.
    /// The ranges are SIMULATION seeds for H3-A and are not a DCS commissioning statement.
    /// </summary>
    public static TimeDelayConfiguration LegacySeededSimulationDefaults(
        TimeDelayMode mode,
        int manualDelaySeconds = 0,
        BoilerCondition activeBoilerCondition = BoilerCondition.VERY_CLEAN)
    {
        return new TimeDelayConfiguration
        {
            Mode = mode,
            ManualDelaySeconds = manualDelaySeconds,
            RhTempMin = LegacySeededRhTempMin,
            RhTempMax = LegacySeededRhTempMax,
            RhInputWindowSeconds = LegacySeededRhInputWindowSeconds,
            ActiveBoilerCondition = activeBoilerCondition,
            BoilerRanges =
            [
                new BoilerConditionDelayRange { Condition = BoilerCondition.VERY_CLEAN, AutoDelayMinSeconds = 600, AutoDelayMaxSeconds = 660 },
                new BoilerConditionDelayRange { Condition = BoilerCondition.NORMAL_CLEAN, AutoDelayMinSeconds = 500, AutoDelayMaxSeconds = 550 },
                new BoilerConditionDelayRange { Condition = BoilerCondition.MEDIUM_DIRTY, AutoDelayMinSeconds = 340, AutoDelayMaxSeconds = 380 },
                new BoilerConditionDelayRange { Condition = BoilerCondition.VERY_DIRTY, AutoDelayMinSeconds = 240, AutoDelayMaxSeconds = 280 },
            ],
            ConfigurationVersion = LegacySeededSimulationVersion,
        };
    }
}

/// <summary>
/// One observed RHTemp_SELECT reading as the decision sees it. The reading is the ONLY RH input of
/// H3-A: there is no second signal, no weighting and no multi-signal fusion.
/// </summary>
public sealed record RhTimeDelayInput
{
    /// <summary>The selected reheat temperature, in the configured engineering unit.</summary>
    public required double RhTemperature { get; init; }

    /// <summary>The quality of the reading. Only <see cref="Quality.GOOD"/> admits a decision.</summary>
    public required Quality Quality { get; init; }

    /// <summary>The reading timestamp in the strict wire shape. A future or stale reading is refused, never reused.</summary>
    public required string ObservedAt { get; init; }

    /// <summary>The reading generation. A non-positive generation is refused as invalid.</summary>
    public required long Generation { get; init; }
}

/// <summary>
/// The live preview of what a NEW decision would produce right now. It is a read-only projection:
/// <see cref="AffectsExecution"/> is always false and no field of an active countdown is derived
/// from it. A refused preview carries the typed refusal instead of a silent zero.
/// </summary>
public sealed record TimeDelayPreview
{
    public required bool Available { get; init; }
    public required bool AffectsExecution { get; init; }
    public required CountdownRefusalCode RefusalCode { get; init; }
    public required TimeDelayMode Mode { get; init; }

    /// <summary>The live RH temperature, or null when no reading exists.</summary>
    public double? RhTemperature { get; init; }

    /// <summary>The live RH quality, or null when no reading exists.</summary>
    public Quality? RhQuality { get; init; }

    /// <summary>The live RH timestamp in the strict wire shape, or null.</summary>
    public string? RhObservedAt { get; init; }

    /// <summary>The live RH generation, or null when no reading exists.</summary>
    public long? RhGeneration { get; init; }

    /// <summary>The RH temperature after clamping into the configured range.</summary>
    public double? ClampedRhTemperature { get; init; }

    /// <summary>The inverse linear mapping result before truncation, in seconds.</summary>
    public double? MappedDelaySeconds { get; init; }

    /// <summary>The whole-second base delay the decision would freeze.</summary>
    public int? BaseDelaySeconds { get; init; }

    /// <summary>The zone of the Sensor the preview is computed for.</summary>
    public DelayZone Zone { get; init; }

    /// <summary>The zone factor that would be applied (1.00 in MANUAL_FIXED mode).</summary>
    public double ZoneFactor { get; init; }

    /// <summary>The applied delay the decision would freeze.</summary>
    public int? AppliedDelaySeconds { get; init; }

    /// <summary>The Boiler Condition the preview used, or null when the configuration has none.</summary>
    public BoilerCondition? BoilerCondition { get; init; }
}

/// <summary>
/// One frozen next-dispatch decision. Every value here is captured once, after Mandatory Safe
/// Return succeeded, and never recomputed: a live RH, Boiler or configuration value can change
/// the preview only. There is no override identity: the Effective Water Jet is the canonical
/// assignment, and its provenance is recorded as a stable name.
/// </summary>
public sealed record NextDispatchDelayDecision
{
    public required string DecisionId { get; init; }

    /// <summary>The countdown generation this decision belongs to. Every supersession increments it.</summary>
    public required int CountdownGeneration { get; init; }

    /// <summary>The GlobalQueue revision at freeze time.</summary>
    public required int QueueGeneration { get; init; }

    public required string QueueEntryId { get; init; }
    public required string NextSensorId { get; init; }
    public required Wall NextWall { get; init; }
    public required int NextSensorOrderWall { get; init; }

    public required string CanonicalWaterJetId { get; init; }
    public required string EffectiveWaterJetId { get; init; }

    /// <summary>The stable provenance name of the Effective Water Jet ("CANONICAL_ASSIGNMENT").</summary>
    public required string EffectiveWaterJetProvenance { get; init; }

    public required string IsolationValveId { get; init; }
    public required string CabinetId { get; init; }
    public required string ControllerId { get; init; }
    public required string HorizontalAxisId { get; init; }
    public required string VerticalAxisId { get; init; }

    /// <summary>The frozen RH reading of an AUTO decision, or null for MANUAL_FIXED.</summary>
    public RhTimeDelayInput? RhInput { get; init; }

    public required BoilerCondition BoilerCondition { get; init; }
    public required TimeDelayMode Mode { get; init; }
    public required string ConfigurationVersion { get; init; }

    public required int ManualDelaySeconds { get; init; }
    public required double RhTempMin { get; init; }
    public required double RhTempMax { get; init; }
    public required int RhInputWindowSeconds { get; init; }
    public required int AutoDelayMinSeconds { get; init; }
    public required int AutoDelayMaxSeconds { get; init; }

    public required int BaseDelaySeconds { get; init; }
    public required DelayZone Zone { get; init; }
    public required double ZoneFactor { get; init; }
    public required int AppliedDelaySeconds { get; init; }

    /// <summary>Creation timestamp in the strict wire shape.</summary>
    public required string CreatedAt { get; init; }
}

/// <summary>
/// The published countdown of the next dispatch. This is a read-only projection of the
/// Runtime-owned countdown state: the browser renders it and never derives, edits or owns it.
/// </summary>
public sealed record DispatchCountdownView
{
    public required CountdownLifecycleState Lifecycle { get; init; }
    public required int Generation { get; init; }
    public required int RemainingSeconds { get; init; }

    /// <summary>The frozen decision, or null when no decision exists.</summary>
    public NextDispatchDelayDecision? Decision { get; init; }

    /// <summary>True only while the frozen decision is immutable (ACTIVE_FROZEN or COMPLETED_AWAITING_DISPATCH).</summary>
    public required bool Frozen { get; init; }

    /// <summary>True only when exactly one dispatch is admissible for the frozen decision.</summary>
    public required bool DispatchAdmissible { get; init; }

    public CountdownSupersessionReason? SupersededReason { get; init; }

    /// <summary>
    /// The reason the PREVIOUS generation was superseded, carried across a restart so the evidence of
    /// a full-delay restart survives the new decision.
    /// </summary>
    public CountdownSupersessionReason? LastSupersededReason { get; init; }

    public CountdownRefusalCode? RefusalCode { get; init; }
    public string? CreatedAt { get; init; }
    public string? CompletedAt { get; init; }
    public string? DispatchedAt { get; init; }
}
