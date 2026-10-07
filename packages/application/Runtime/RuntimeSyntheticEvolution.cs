using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Time;

namespace Wjss.Runtime.Core;

/// <summary>
/// Bounds and schedules of the synthetic evolution. Every value here is a
/// DEVELOPMENT PRESENTATION constant of the SIMULATOR profile: named, finite and
/// reviewable. Nothing in this type carries process meaning, a device address, a
/// calibration constant or a Production limit, and no value may be read as a
/// certified process parameter.
/// </summary>
public sealed record SyntheticEvolutionRules
{
    /// <summary>Largest per-tick walk step, in tenths of a Dirty Score point (default 20 = 2.0 points).</summary>
    public int StepRangeTenths { get; init; } = 20;

    /// <summary>Base period of the STALE quality schedule, in ticks (default 24).</summary>
    public int StalePeriodBase { get; init; } = 24;

    /// <summary>Additional deterministic per-Sensor spread of the STALE period (default 13, i.e. 24..36).</summary>
    public int StalePeriodSpan { get; init; } = 13;

    /// <summary>Base period of the UNCERTAIN quality schedule, in ticks (default 12).</summary>
    public int UncertainPeriodBase { get; init; } = 12;

    /// <summary>Additional deterministic per-Sensor spread of the UNCERTAIN period (default 9, i.e. 12..20).</summary>
    public int UncertainPeriodSpan { get; init; } = 9;

    /// <summary>Base period of the BAD quality schedule, in ticks (default 60).</summary>
    public int BadPeriodBase { get; init; } = 60;

    /// <summary>Additional deterministic per-Sensor spread of the BAD period (default 21, i.e. 60..80).</summary>
    public int BadPeriodSpan { get; init; } = 21;

    /// <summary>
    /// Synthetic retention of the last validated classification basis, in seconds
    /// (default 604800 = 7 presentation days). Older evidence is presented as no
    /// basis at all rather than as a stale classification.
    /// </summary>
    public int LastValidatedRetentionSeconds { get; init; } = 604800;

    /// <summary>Validates every bound; an out-of-range rule set is a programming error, not a runtime refusal.</summary>
    public SyntheticEvolutionRules Validated()
    {
        if (StepRangeTenths < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StepRangeTenths), StepRangeTenths, "The walk step range must not be negative.");
        }

        if (StalePeriodBase < 1 || StalePeriodSpan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StalePeriodBase), StalePeriodBase, "The STALE period base must be at least 1 and its span must not be negative.");
        }

        if (UncertainPeriodBase < 1 || UncertainPeriodSpan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(UncertainPeriodBase), UncertainPeriodBase, "The UNCERTAIN period base must be at least 1 and its span must not be negative.");
        }

        if (BadPeriodBase < 1 || BadPeriodSpan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BadPeriodBase), BadPeriodBase, "The BAD period base must be at least 1 and its span must not be negative.");
        }

        if (LastValidatedRetentionSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(LastValidatedRetentionSeconds), LastValidatedRetentionSeconds, "The last-validated retention must be at least one second.");
        }

        return this;
    }
}

/// <summary>
/// The candidate revision one accepted synthetic tick produced, plus the frozen
/// evidence needed to project and apply a Delta: the new Sensor records, the
/// previous revision's records for the same Sensors (so a cleanly-applied Delta
/// can be reversed without consulting history), and the trend point this tick
/// appended.
/// </summary>
public sealed record SyntheticEvolutionResult
{
    /// <summary>The candidate revision (never committed by this type).</summary>
    public required RuntimeState State { get; init; }

    /// <summary>New Sensor records for every Sensor this tick changed, in canonical ScanOrder.</summary>
    public required IReadOnlyList<SensorPresentationState> ChangedSensors { get; init; }

    /// <summary>The previous revision's records for the same Sensors, same order as <see cref="ChangedSensors"/>.</summary>
    public required IReadOnlyList<SensorPresentationState> PreviousSensors { get; init; }

    /// <summary>The bounded-trend point this tick appended.</summary>
    public required TrendPoint AppendedTrendPoint { get; init; }
}

/// <summary>
/// Outcome of one synthetic tick request: either the accepted candidate revision
/// or a machine-readable refusal. A refusal carries no state at all, so a caller
/// cannot commit anything by accident.
/// </summary>
public sealed record SyntheticTickOutcome
{
    /// <summary>True when the tick produced a candidate revision.</summary>
    public required bool Accepted { get; init; }

    /// <summary>Refusal code (see <see cref="RuntimeRefusalCodes"/>); null when accepted.</summary>
    public string? RefusalCode { get; init; }

    /// <summary>Human-readable refusal detail; null when accepted.</summary>
    public string? RefusalReason { get; init; }

    /// <summary>The accepted candidate; null when refused.</summary>
    public SyntheticEvolutionResult? Result { get; init; }

    /// <summary>Builds the accepted outcome.</summary>
    public static SyntheticTickOutcome Accept(SyntheticEvolutionResult result) =>
        new() { Accepted = true, Result = result ?? throw new ArgumentNullException(nameof(result)) };

    /// <summary>Builds the refused outcome.</summary>
    public static SyntheticTickOutcome Refused(string code, string reason) =>
        new() { Accepted = false, RefusalCode = code, RefusalReason = reason };
}

/// <summary>
/// Deterministic synthetic evolution for the SIMULATOR profile.
///
/// Determinism contract. Every presented value is a pure function of the
/// committed state, the explicit seed, the explicit tick number and the explicit
/// tick instant; the same four inputs always produce the same candidate revision,
/// on every host, in any Sensor iteration order. The generator is an integer
/// mixing function (splitmix64 constants), never <c>System.Random</c>, and there
/// is no ambient wall-clock read, no ambient clock, no static mutable state and
/// no timing input anywhere in this type or the types it touches.
///
/// Synthetic presentation values only. The Dirty Score here is a unitless
/// development presentation value in the documented synthetic range 0.0..100.0;
/// it is not a certified process measurement and carries no device address, no
/// calibration constant and no Production limit. The walk and quality schedules
/// are deliberately simple first-order rules that a reviewer can check by hand.
///
/// Relationship to the SIMULATOR adapter: the initial Sensor projection is
/// produced by <c>Wjss.Adapters.Simulator</c>, which Runtime.Core must not
/// reference. This engine therefore defines its own walk derivation; no equality
/// with the adapter's initial-score derivation, or with the fixture generator's
/// formula, is claimed or implied. Generator/adapter unification remains an open
/// Owner decision.
///
/// The engine never commits anything. It computes a candidate revision and the
/// caller submits it to the single authoritative writer of the Runtime State
/// Store, so the atomicity, validation and single-writer guarantees stay in one
/// place. A refused tick returns no candidate, and a refused commit leaves the
/// committed revision untouched.
/// </summary>
public static class RuntimeSyntheticEvolution
{
    /// <summary>Tick number of the composed initial revision (revision 1).</summary>
    public const long InitialTickNumber = 0;

    /// <summary>Lower bound of the synthetic Dirty Score, in tenths (0.0).</summary>
    public const int ScoreMinimumTenths = 0;

    /// <summary>Upper bound of the synthetic Dirty Score, in tenths (100.0).</summary>
    public const int ScoreMaximumTenths = 1000;

    /// <summary>Machine reason code carried by a degraded Sensor with no usable validated basis.</summary>
    public const string NoValidatedBasisReason = "NO_VALIDATED_BASIS";

    /// <summary>Machine reason code of the UNCERTAIN synthetic quality state.</summary>
    public const string UncertainReason = "SYNTHETIC_UNCERTAIN";

    /// <summary>Machine reason code of the STALE synthetic quality state.</summary>
    public const string StaleReason = "SYNTHETIC_STALE";

    /// <summary>Machine reason code of the BAD synthetic quality state.</summary>
    public const string BadReason = "SYNTHETIC_BAD";

    private static readonly SyntheticEvolutionRules DefaultRulesSet = new SyntheticEvolutionRules().Validated();

    /// <summary>The default rule set (documented synthetic bounds).</summary>
    public static SyntheticEvolutionRules DefaultRules => DefaultRulesSet;

    /// <summary>Advances one tick with the default rules.</summary>
    public static SyntheticTickOutcome Tick(
        RuntimeState state, ulong seed, long tickNumber, DateTimeOffset tickTimeUtc) =>
        Tick(state, seed, tickNumber, tickTimeUtc, DefaultRulesSet);

    /// <summary>
    /// Advances exactly one accepted tick. The committed state is never modified;
    /// the returned candidate is the input to the store's single writer. Refusals
    /// (non-SIMULATOR profile, tick number not the successor of the committed
    /// revision, tick instant not strictly later than the committed instant) carry
    /// no candidate and no mutated state.
    /// </summary>
    public static SyntheticTickOutcome Tick(
        RuntimeState state, ulong seed, long tickNumber, DateTimeOffset tickTimeUtc, SyntheticEvolutionRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        _ = rules.Validated();

        if (!ProfileStartPolicy.TryRequireStartable(state.DeviceProfile, out var profileCode, out var profileReason))
        {
            return SyntheticTickOutcome.Refused(profileCode, profileReason);
        }

        if (tickNumber != state.Revision)
        {
            return SyntheticTickOutcome.Refused(
                RuntimeRefusalCodes.EvolutionTickSequence,
                $"Refused tick {tickNumber}: the committed revision {state.Revision} expects tick {state.Revision} "
                + "(tick numbers are consecutive and one accepted tick commits exactly one revision). Nothing was evolved.");
        }

        var instant = tickTimeUtc.ToUniversalTime();
        if (instant <= state.GeneratedAtUtc)
        {
            return SyntheticTickOutcome.Refused(
                RuntimeRefusalCodes.EvolutionTickTime,
                $"Refused tick {tickNumber} at {UtcTimestamps.Format(instant)}: the committed revision is stamped "
                + $"{UtcTimestamps.Format(state.GeneratedAtUtc)} and a tick must be strictly later. Nothing was evolved.");
        }

        var previousSensors = state.Sensors;
        var nextSensors = new SensorPresentationState[previousSensors.Count];
        var changedIndices = new List<int>();

        for (var index = 0; index < previousSensors.Count; index++)
        {
            var previous = previousSensors[index];
            var next = AdvanceSensor(previous, state.Config.DirtyThreshold, seed, tickNumber, instant, rules);
            nextSensors[index] = next;

            if (!next.Equals(previous))
            {
                changedIndices.Add(index);
            }
        }

        var changedSensors = new List<SensorPresentationState>(changedIndices.Count);
        var changedPreviousSensors = new List<SensorPresentationState>(changedIndices.Count);
        foreach (var index in changedIndices)
        {
            changedSensors.Add(nextSensors[index]);
            changedPreviousSensors.Add(previousSensors[index]);
        }

        var trendPoint = BuildTrendPoint(state, instant);
        var candidate = state with
        {
            Revision = state.Revision + 1,
            GeneratedAtUtc = instant,
            Sensors = nextSensors,
            Walls = RuntimeWallSummaries.Recalculate(nextSensors),
            Trend = RuntimeTrendBuffer.Append(state.Trend, trendPoint),
        };

        return SyntheticTickOutcome.Accept(new SyntheticEvolutionResult
        {
            State = candidate,
            ChangedSensors = RuntimeCollections.Freeze(changedSensors),
            PreviousSensors = RuntimeCollections.Freeze(changedPreviousSensors),
            AppendedTrendPoint = trendPoint,
        });
    }

    /// <summary>
    /// Advances one Sensor: the score walk, the quality schedule and the
    /// classification/basis rules. The update depends only on this Sensor's own
    /// previous record and on (seed, tick, ScanOrder), so the Sensor iteration
    /// order can never influence a result.
    /// </summary>
    private static SensorPresentationState AdvanceSensor(
        SensorPresentationState sensor,
        double dirtyThreshold,
        ulong seed,
        long tickNumber,
        DateTimeOffset instant,
        SyntheticEvolutionRules rules)
    {
        var schedule = Mix(seed ^ Mix((ulong)sensor.ScanOrder));
        var stalePeriod = rules.StalePeriodBase + (int)((schedule >> 8) % (ulong)(rules.StalePeriodSpan + 1));
        var uncertainPeriod = rules.UncertainPeriodBase + (int)((schedule >> 16) % (ulong)(rules.UncertainPeriodSpan + 1));
        var badPeriod = rules.BadPeriodBase + (int)((schedule >> 24) % (ulong)(rules.BadPeriodSpan + 1));

        var tick = (ulong)tickNumber;
        var quality = tick % (ulong)badPeriod == 0
            ? Quality.BAD
            : tick % (ulong)stalePeriod == 0
                ? Quality.STALE
                : tick % (ulong)uncertainPeriod == 0
                    ? Quality.UNCERTAIN
                    : Quality.GOOD;

        var hasBasis = TryGetValidatedBasis(sensor, instant, rules, out var validatedScore);
        var timestamp = UtcTimestamps.Format(instant);

        switch (quality)
        {
            case Quality.GOOD:
            {
                var score = WalkScore(sensor, seed, tickNumber, rules);
                return sensor with
                {
                    DirtyScore = score,
                    SourceTimestamp = timestamp,
                    Classification = Classify(score, dirtyThreshold),
                    ClassificationBasis = ClassificationBasis.CURRENT,
                    Quality = Quality.GOOD,
                    QualityReason = null,
                    LastValidatedScore = score,
                    LastValidatedAt = timestamp,
                };
            }

            case Quality.UNCERTAIN:
            {
                var score = WalkScore(sensor, seed, tickNumber, rules);
                return sensor with
                {
                    DirtyScore = score,
                    SourceTimestamp = timestamp,
                    Classification = hasBasis ? sensor.Classification : Classification.NOT_CLASSIFIED,
                    ClassificationBasis = hasBasis ? ClassificationBasis.LAST_VALIDATED : ClassificationBasis.NONE,
                    Quality = Quality.UNCERTAIN,
                    QualityReason = hasBasis ? UncertainReason : NoValidatedBasisReason,
                    LastValidatedScore = hasBasis ? validatedScore : sensor.LastValidatedScore,
                };
            }

            case Quality.STALE:
            {
                // No fresh reading this tick: the previous reading's instant is
                // retained as evidence of when the value was last seen.
                return sensor with
                {
                    DirtyScore = null,
                    Classification = hasBasis ? sensor.Classification : Classification.NOT_CLASSIFIED,
                    ClassificationBasis = hasBasis ? ClassificationBasis.LAST_VALIDATED : ClassificationBasis.NONE,
                    Quality = Quality.STALE,
                    QualityReason = hasBasis ? StaleReason : NoValidatedBasisReason,
                };
            }

            default:
            {
                return sensor with
                {
                    DirtyScore = null,
                    SourceTimestamp = null,
                    Classification = hasBasis ? sensor.Classification : Classification.NOT_CLASSIFIED,
                    ClassificationBasis = hasBasis ? ClassificationBasis.LAST_VALIDATED : ClassificationBasis.NONE,
                    Quality = Quality.BAD,
                    QualityReason = hasBasis ? BadReason : NoValidatedBasisReason,
                };
            }
        }
    }

    /// <summary>
    /// One step of the bounded score walk, in tenths of a point. The step derives
    /// from (seed, tick, ScanOrder) only, and the position derives from the
    /// Sensor's own previous value (or, when no value is presented, from a
    /// deterministic seeded position), so the walk is order-independent and stays
    /// inside the documented synthetic bounds.
    /// </summary>
    private static double WalkScore(
        SensorPresentationState sensor, ulong seed, long tickNumber, SyntheticEvolutionRules rules)
    {
        var assertedTenths = sensor.DirtyScore is double previous
            ? (int)Math.Round(previous * 10.0, MidpointRounding.AwayFromZero)
            : (int)(Mix(seed ^ Mix((ulong)sensor.ScanOrder)) % (ulong)(ScoreMaximumTenths + 1));

        var span = (ulong)((2 * rules.StepRangeTenths) + 1);
        var offset = (int)(Mix(seed ^ Mix((ulong)tickNumber) ^ Mix((ulong)sensor.ScanOrder)) % span)
            - rules.StepRangeTenths;
        var bounded = Math.Clamp(assertedTenths + offset, ScoreMinimumTenths, ScoreMaximumTenths);

        // Explicitly rounded to one decimal place with a culture-invariant
        // midpoint rule: no culture, no ambient rounding mode, no formatting.
        return Math.Round(bounded / 10.0, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>The accepted classification rule: strictly above the published threshold is DIRTY.</summary>
    private static Classification Classify(double score, double dirtyThreshold) =>
        score > dirtyThreshold ? Classification.DIRTY : Classification.CLEANER;

    /// <summary>
    /// True when the Sensor carries a last validated value whose instant is
    /// parseable, not in the future and within the synthetic retention window.
    /// </summary>
    private static bool TryGetValidatedBasis(
        SensorPresentationState sensor, DateTimeOffset instant, SyntheticEvolutionRules rules, out double validatedScore)
    {
        validatedScore = default;

        if (sensor.LastValidatedScore is not double score
            || !UtcTimestamps.TryParse(sensor.LastValidatedAt, out var validatedAt))
        {
            return false;
        }

        var age = instant - validatedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(rules.LastValidatedRetentionSeconds))
        {
            return false;
        }

        validatedScore = score;
        return true;
    }

    /// <summary>
    /// The trend point of one accepted tick. The four series are mirrors of the
    /// published Pump presentation block (pressure, setpoint, ready-band low,
    /// ready-band high) and are therefore explicitly "not implemented" zeros in
    /// Stage 0.3A-2A, when no pump setpoint is published yet; a missing pressure
    /// value is carried as a data gap (null), never interpolated. No fabricated
    /// pump fidelity and no control meaning is added here.
    /// </summary>
    private static TrendPoint BuildTrendPoint(RuntimeState state, DateTimeOffset instant) => new()
    {
        T = instant.ToUnixTimeSeconds(),
        Series = new double?[]
        {
            state.Pump.Pressure,
            state.Pump.Setpoint,
            state.Pump.ReadyBandLow,
            state.Pump.ReadyBandHigh,
        },
        Setpoint = state.Pump.Setpoint,
        JobActive = state.ActiveJob is not null,
        AlarmActive = state.Alarms.ActiveUnack + state.Alarms.ActiveAck > 0,
    };

    /// <summary>
    /// The splitmix64 finalizer used to derive every synthetic value. Integer-only
    /// and platform independent; the constants are the canonical mixing constants
    /// of that generator family, and they imply no relationship to any adapter or
    /// fixture value derivation.
    /// </summary>
    private static ulong Mix(ulong value)
    {
        var mixed = value;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
        return mixed ^ (mixed >> 31);
    }
}
