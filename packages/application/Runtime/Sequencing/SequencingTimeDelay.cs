using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.Motion;
using Wjss.Time;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// The one canonical Sensor identity a TimeDelay decision needs, resolved BEFORE the decision is
/// created. Every member is derived from an existing authority: the Sensor record supplies the wall
/// and the assigned Water Jet, <see cref="WaterJetTopologyCatalog"/> supplies the paired Isolation
/// Valve and <see cref="MotionTopologyCatalog"/> supplies the WSB cabinet, the controller and the
/// motion axis pair. The wall-local order is the canonical
/// <see cref="CanonicalSensorMap.WallLocalOrdinal"/> of the Sensor geometry.
/// </summary>
public sealed record TimeDelaySensorIdentity(
    string SensorId,
    Wall Wall,
    int OrderWall,
    string CanonicalWaterJetId,
    string EffectiveWaterJetId,
    string EffectiveWaterJetProvenance,
    string IsolationValveId,
    string CabinetId,
    string ControllerId,
    string HorizontalAxisId,
    string VerticalAxisId);

/// <summary>
/// The outcome of one planning attempt: either one immutable decision, or one typed refusal.
/// A refusal is never a silent zero and never a last-known value.
/// </summary>
public sealed class TimeDelayPlanResult
{
    private TimeDelayPlanResult(bool accepted, NextDispatchDelayDecision? decision, CountdownRefusalCode refusal)
    {
        Accepted = accepted;
        Decision = decision;
        RefusalCode = refusal;
    }

    public bool Accepted { get; }

    public NextDispatchDelayDecision? Decision { get; }

    public CountdownRefusalCode RefusalCode { get; }

    public static TimeDelayPlanResult Refused(CountdownRefusalCode refusal) => new(false, null, refusal);

    public static TimeDelayPlanResult FromDecision(NextDispatchDelayDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new TimeDelayPlanResult(true, decision, CountdownRefusalCode.NONE);
    }
}

/// <summary>
/// The pure H3-A TimeDelay planner. It owns the whole arithmetic of both modes and nothing else:
/// no clock, no configuration store, no Random, no network and no state. The same inputs always
/// produce byte-identical values.
///
/// <para>
/// MANUAL_FIXED: BaseDelay = ManualDelaySeconds, the zone factor is 1.00 and AppliedDelay equals
/// ManualDelaySeconds. RHTemp_SELECT, the Boiler ranges and the Upper-zone factor are not consulted
/// at all, and a negative or out-of-range manual delay is refused.
/// </para>
/// <para>
/// AUTO_RH_LINEAR_FROZEN: one RHTemp_SELECT reading is clamped into RhTempMin..RhTempMax, then
/// MappedDelay = AutoDelayMax + (ClampedRh - RhTempMin) * (AutoDelayMin - AutoDelayMax) /
/// (RhTempMax - RhTempMin), BaseDelaySeconds = truncate-toward-zero(MappedDelay) and
/// AppliedDelaySeconds = Convert.ToInt32(BaseDelaySeconds * ZoneFactor) (the Owner-specified
/// conversion, which rounds half to even). The zone factor is 0.75 for the Upper zone and 1.00
/// otherwise, and it comes from the NEXT Queue Sensor only.
/// </para>
/// </summary>
public static class TimeDelayPlanner
{
    /// <summary>The lowest accepted configured delay, in whole seconds.</summary>
    public const int MinimumManualDelaySeconds = 0;

    /// <summary>
    /// The highest accepted configured delay, in whole seconds. This is an H3-A configuration bound
    /// of this stage, NOT a commissioning or process value.
    /// </summary>
    public const int MaximumConfiguredDelaySeconds = 3600;

    /// <summary>The Auto Upper-zone factor (Owner decision: 0.75).</summary>
    public const double UpperZoneFactor = 0.75;

    /// <summary>The Auto Lower-zone factor, and the MANUAL_FIXED factor in every zone.</summary>
    public const double LowerZoneFactor = 1.00;

    /// <summary>LEFT and RIGHT are Upper while the wall-local order is strictly below this bound.</summary>
    public const int LeftRightUpperOrderWallBound = 12;

    /// <summary>REAR and FRONT are Upper while the wall-local order is strictly below this bound.</summary>
    public const int FrontRearUpperOrderWallBound = 15;

    /// <summary>The stable provenance name of a canonical assignment decision.</summary>
    public static string CanonicalAssignmentProvenance =>
        nameof(EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT);

    /// <summary>
    /// Resolves the canonical identity of one Sensor for a delay decision. Any missing identity is
    /// refused fail closed: there is no fallback Water Jet, Isolation Valve, cabinet or axis.
    /// </summary>
    public static bool TryResolveSensorIdentity(
        SensorPresentationState? sensor,
        out TimeDelaySensorIdentity? identity,
        out CountdownRefusalCode refusal)
    {
        identity = null;
        refusal = CountdownRefusalCode.NONE;

        if (sensor is null || string.IsNullOrWhiteSpace(sensor.SensorId))
        {
            refusal = CountdownRefusalCode.SENSOR_IDENTITY_INVALID;
            return false;
        }

        if (!CanonicalSensorMap.TryWallLocalOrdinal(sensor.Wall, sensor.WallRow, sensor.WallColumn, out var orderWall))
        {
            refusal = CountdownRefusalCode.SENSOR_IDENTITY_INVALID;
            return false;
        }

        var canonicalWaterJetId = sensor.AssignedWaterJetId;
        if (string.IsNullOrWhiteSpace(canonicalWaterJetId))
        {
            refusal = CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID;
            return false;
        }

        var waterJet = WaterJetTopologyCatalog.FindWaterJet(canonicalWaterJetId);
        if (waterJet is null
            || string.IsNullOrWhiteSpace(waterJet.DedicatedIsolationValveId)
            || !string.Equals(waterJet.DedicatedIsolationValveId, sensor.AssignedIsolationValveId, StringComparison.Ordinal))
        {
            refusal = CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID;
            return false;
        }

        var binding = MotionTopologyCatalog.FindWaterJetBinding(canonicalWaterJetId);
        if (binding is null
            || string.IsNullOrWhiteSpace(binding.CabinetId)
            || string.IsNullOrWhiteSpace(binding.ControllerId)
            || string.IsNullOrWhiteSpace(binding.HorizontalAxisId)
            || string.IsNullOrWhiteSpace(binding.VerticalAxisId))
        {
            refusal = CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID;
            return false;
        }

        identity = new TimeDelaySensorIdentity(
            sensor.SensorId,
            sensor.Wall,
            orderWall,
            canonicalWaterJetId,
            canonicalWaterJetId,
            CanonicalAssignmentProvenance,
            waterJet.DedicatedIsolationValveId,
            binding.CabinetId,
            binding.ControllerId,
            binding.HorizontalAxisId,
            binding.VerticalAxisId);
        return true;
    }

    /// <summary>Validates one configuration. A missing configuration is a refusal, never a default.</summary>
    public static bool TryValidateConfiguration(
        TimeDelayConfiguration? configuration,
        out CountdownRefusalCode refusal)
    {
        refusal = CountdownRefusalCode.NONE;

        if (configuration is null)
        {
            refusal = CountdownRefusalCode.CONFIGURATION_MISSING;
            return false;
        }

        if (configuration.Mode is not (TimeDelayMode.MANUAL_FIXED or TimeDelayMode.AUTO_RH_LINEAR_FROZEN))
        {
            refusal = CountdownRefusalCode.MODE_UNSUPPORTED;
            return false;
        }

        if (string.IsNullOrWhiteSpace(configuration.ConfigurationVersion))
        {
            refusal = CountdownRefusalCode.CONFIGURATION_VERSION_MISSING;
            return false;
        }

        if (configuration.ManualDelaySeconds < MinimumManualDelaySeconds
            || configuration.ManualDelaySeconds > MaximumConfiguredDelaySeconds)
        {
            refusal = CountdownRefusalCode.MANUAL_DELAY_OUT_OF_RANGE;
            return false;
        }

        if (!double.IsFinite(configuration.RhTempMin)
            || !double.IsFinite(configuration.RhTempMax)
            || configuration.RhTempMin >= configuration.RhTempMax)
        {
            refusal = CountdownRefusalCode.RH_RANGE_INVALID;
            return false;
        }

        if (configuration.RhInputWindowSeconds <= 0)
        {
            refusal = CountdownRefusalCode.RH_INPUT_WINDOW_INVALID;
            return false;
        }

        if (configuration.BoilerRanges is null || configuration.BoilerRanges.Count == 0)
        {
            refusal = CountdownRefusalCode.BOILER_CONDITION_RANGE_MISSING;
            return false;
        }

        foreach (var range in configuration.BoilerRanges)
        {
            if (range is null || !Enum.IsDefined(range.Condition))
            {
                refusal = CountdownRefusalCode.BOILER_CONDITION_UNKNOWN;
                return false;
            }

            if (range.AutoDelayMinSeconds < 0
                || range.AutoDelayMaxSeconds < 0
                || range.AutoDelayMinSeconds > MaximumConfiguredDelaySeconds
                || range.AutoDelayMaxSeconds > MaximumConfiguredDelaySeconds)
            {
                refusal = CountdownRefusalCode.AUTO_DELAY_RANGE_INVALID;
                return false;
            }

            if (range.AutoDelayMinSeconds > range.AutoDelayMaxSeconds)
            {
                refusal = CountdownRefusalCode.AUTO_DELAY_ORDER_INVALID;
                return false;
            }
        }

        var configured = configuration.BoilerRanges.Count(range => range.Condition == configuration.ActiveBoilerCondition);
        if (configured == 0)
        {
            refusal = CountdownRefusalCode.BOILER_CONDITION_RANGE_MISSING;
            return false;
        }

        if (configured > 1)
        {
            refusal = CountdownRefusalCode.BOILER_CONDITION_DUPLICATE;
            return false;
        }

        return true;
    }

    /// <summary>The configured Auto delay range of the Active Boiler Condition, or a refusal.</summary>
    public static bool TryResolveActiveRange(
        TimeDelayConfiguration configuration,
        out BoilerConditionDelayRange? range,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        range = null;
        refusal = CountdownRefusalCode.NONE;

        foreach (var candidate in configuration.BoilerRanges ?? Array.Empty<BoilerConditionDelayRange>())
        {
            if (candidate.Condition != configuration.ActiveBoilerCondition) continue;
            range = candidate;
            return true;
        }

        refusal = CountdownRefusalCode.BOILER_CONDITION_RANGE_MISSING;
        return false;
    }

    /// <summary>
    /// The zone of one Sensor. LEFT and RIGHT are Upper strictly below wall order 12; REAR and FRONT
    /// strictly below wall order 15. Every other position, and every invalid wall order, is Lower.
    /// </summary>
    public static DelayZone ZoneFor(Wall wall, int orderWall)
    {
        if (orderWall < 1) return DelayZone.LOWER;
        var bound = wall is Wall.LEFT or Wall.RIGHT
            ? LeftRightUpperOrderWallBound
            : FrontRearUpperOrderWallBound;
        return orderWall < bound ? DelayZone.UPPER : DelayZone.LOWER;
    }

    /// <summary>The zone factor of one zone. It is applied in AUTO mode only.</summary>
    public static double ZoneFactorFor(DelayZone zone) =>
        zone == DelayZone.UPPER ? UpperZoneFactor : LowerZoneFactor;

    /// <summary>Clamps one RH temperature into the configured range.</summary>
    public static double ClampRhTemperature(double rhTemperature, double rhTempMin, double rhTempMax)
    {
        if (rhTemperature < rhTempMin) return rhTempMin;
        if (rhTemperature > rhTempMax) return rhTempMax;
        return rhTemperature;
    }

    /// <summary>
    /// The Legacy inverse linear mapping. It refuses a non-positive RH span instead of dividing by
    /// zero, so no arithmetic value is ever produced from an invalid range.
    /// </summary>
    public static bool TryMapInverseLinear(
        double clampedRhTemperature,
        double rhTempMin,
        double rhTempMax,
        int autoDelayMinSeconds,
        int autoDelayMaxSeconds,
        out double mappedDelaySeconds)
    {
        mappedDelaySeconds = 0.0;
        var span = rhTempMax - rhTempMin;
        if (!double.IsFinite(span) || span <= 0.0) return false;

        var mapped = autoDelayMaxSeconds
            + ((clampedRhTemperature - rhTempMin) * (autoDelayMinSeconds - autoDelayMaxSeconds) / span);
        if (!double.IsFinite(mapped)) return false;

        mappedDelaySeconds = mapped;
        return true;
    }

    /// <summary>
    /// Truncation toward zero, the Owner-specified whole-second base delay conversion. A
    /// non-positive value truncates to zero, and a value beyond the representable whole-second range
    /// is refused instead of being silently saturated.
    /// </summary>
    public static int TruncateToWholeSeconds(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds <= 0.0) return 0;
        if (seconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(seconds));
        return (int)Math.Truncate(seconds);
    }

    /// <summary>
    /// The Owner-specified zone-factor conversion, <c>Convert.ToInt32(BaseDelaySeconds * ZoneFactor)</c>,
    /// with a non-negative floor. <see cref="Convert.ToInt32(double)"/> rounds half to even; that
    /// rounding is part of the contract and is proved by test rather than replaced by a local rule.
    /// </summary>
    public static int ApplyZoneFactor(int baseDelaySeconds, double zoneFactor)
    {
        if (baseDelaySeconds < 0) throw new ArgumentOutOfRangeException(nameof(baseDelaySeconds));
        if (!double.IsFinite(zoneFactor) || zoneFactor < 0.0) throw new ArgumentOutOfRangeException(nameof(zoneFactor));

        var scaled = baseDelaySeconds * zoneFactor;
        if (scaled > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(zoneFactor));

        return Math.Max(0, Convert.ToInt32(scaled));
    }

    /// <summary>Validates one RH reading at decision time. Staleness is measured against the decision instant.</summary>
    public static bool TryValidateRhInput(
        TimeDelayConfiguration configuration,
        RhTimeDelayInput? rhInput,
        DateTimeOffset decidedAtUtc,
        out DateTimeOffset observedAtUtc,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        observedAtUtc = default;
        refusal = CountdownRefusalCode.NONE;

        if (rhInput is null)
        {
            refusal = CountdownRefusalCode.RH_MISSING;
            return false;
        }

        if (!double.IsFinite(rhInput.RhTemperature))
        {
            refusal = CountdownRefusalCode.RH_VALUE_INVALID;
            return false;
        }

        if (rhInput.Generation <= 0)
        {
            refusal = CountdownRefusalCode.RH_GENERATION_INVALID;
            return false;
        }

        if (rhInput.Quality != Quality.GOOD)
        {
            refusal = CountdownRefusalCode.RH_BAD_QUALITY;
            return false;
        }

        if (!UtcTimestamps.TryParse(rhInput.ObservedAt, out observedAtUtc))
        {
            refusal = CountdownRefusalCode.RH_TIMESTAMP_INVALID;
            return false;
        }

        if (observedAtUtc > decidedAtUtc)
        {
            refusal = CountdownRefusalCode.RH_FUTURE_DATED;
            return false;
        }

        if ((decidedAtUtc - observedAtUtc).TotalSeconds > configuration.RhInputWindowSeconds)
        {
            refusal = CountdownRefusalCode.RH_STALE;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Plans one frozen decision. The method is total: every failure is one typed refusal and no
    /// value is ever guessed, defaulted to zero or carried over from a previous decision.
    /// </summary>
    public static TimeDelayPlanResult Plan(
        TimeDelayConfiguration? configuration,
        TimeDelaySensorIdentity? nextSensor,
        RhTimeDelayInput? rhInput,
        DateTimeOffset atUtc,
        string decisionId,
        int countdownGeneration,
        int queueGeneration,
        string queueEntryId)
    {
        if (!TryValidateConfiguration(configuration, out var refusal))
        {
            return TimeDelayPlanResult.Refused(refusal);
        }

        if (string.IsNullOrWhiteSpace(decisionId) || countdownGeneration < 1 || queueGeneration < 0
            || string.IsNullOrWhiteSpace(queueEntryId))
        {
            return TimeDelayPlanResult.Refused(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID);
        }

        if (!TryValidateSensorIdentity(nextSensor, out refusal))
        {
            return TimeDelayPlanResult.Refused(refusal);
        }

        if (!TryResolveActiveRange(configuration!, out var range, out refusal))
        {
            return TimeDelayPlanResult.Refused(refusal);
        }

        var mode = configuration!.Mode;
        var zone = ZoneFor(nextSensor!.Wall, nextSensor.OrderWall);
        var zoneFactor = mode == TimeDelayMode.AUTO_RH_LINEAR_FROZEN ? ZoneFactorFor(zone) : LowerZoneFactor;

        if (mode == TimeDelayMode.MANUAL_FIXED)
        {
            var manualBase = configuration.ManualDelaySeconds;
            return TimeDelayPlanResult.FromDecision(new NextDispatchDelayDecision
            {
                DecisionId = decisionId,
                CountdownGeneration = countdownGeneration,
                QueueGeneration = queueGeneration,
                QueueEntryId = queueEntryId,
                NextSensorId = nextSensor.SensorId,
                NextWall = nextSensor.Wall,
                NextSensorOrderWall = nextSensor.OrderWall,
                CanonicalWaterJetId = nextSensor.CanonicalWaterJetId,
                EffectiveWaterJetId = nextSensor.EffectiveWaterJetId,
                EffectiveWaterJetProvenance = nextSensor.EffectiveWaterJetProvenance,
                IsolationValveId = nextSensor.IsolationValveId,
                CabinetId = nextSensor.CabinetId,
                ControllerId = nextSensor.ControllerId,
                HorizontalAxisId = nextSensor.HorizontalAxisId,
                VerticalAxisId = nextSensor.VerticalAxisId,
                RhInput = null,
                BoilerCondition = configuration.ActiveBoilerCondition,
                Mode = mode,
                ConfigurationVersion = configuration.ConfigurationVersion,
                ManualDelaySeconds = configuration.ManualDelaySeconds,
                RhTempMin = configuration.RhTempMin,
                RhTempMax = configuration.RhTempMax,
                RhInputWindowSeconds = configuration.RhInputWindowSeconds,
                AutoDelayMinSeconds = range!.AutoDelayMinSeconds,
                AutoDelayMaxSeconds = range.AutoDelayMaxSeconds,
                BaseDelaySeconds = manualBase,
                Zone = zone,
                ZoneFactor = LowerZoneFactor,
                AppliedDelaySeconds = ApplyZoneFactor(manualBase, LowerZoneFactor),
                CreatedAt = UtcTimestamps.Format(atUtc),
            });
        }

        if (!TryValidateRhInput(configuration, rhInput, atUtc, out _, out refusal))
        {
            return TimeDelayPlanResult.Refused(refusal);
        }

        var clamped = ClampRhTemperature(rhInput!.RhTemperature, configuration.RhTempMin, configuration.RhTempMax);
        if (!TryMapInverseLinear(
                clamped,
                configuration.RhTempMin,
                configuration.RhTempMax,
                range!.AutoDelayMinSeconds,
                range.AutoDelayMaxSeconds,
                out var mapped))
        {
            return TimeDelayPlanResult.Refused(CountdownRefusalCode.RH_RANGE_INVALID);
        }

        var baseDelay = TruncateToWholeSeconds(mapped);
        return TimeDelayPlanResult.FromDecision(new NextDispatchDelayDecision
        {
            DecisionId = decisionId,
            CountdownGeneration = countdownGeneration,
            QueueGeneration = queueGeneration,
            QueueEntryId = queueEntryId,
            NextSensorId = nextSensor.SensorId,
            NextWall = nextSensor.Wall,
            NextSensorOrderWall = nextSensor.OrderWall,
            CanonicalWaterJetId = nextSensor.CanonicalWaterJetId,
            EffectiveWaterJetId = nextSensor.EffectiveWaterJetId,
            EffectiveWaterJetProvenance = nextSensor.EffectiveWaterJetProvenance,
            IsolationValveId = nextSensor.IsolationValveId,
            CabinetId = nextSensor.CabinetId,
            ControllerId = nextSensor.ControllerId,
            HorizontalAxisId = nextSensor.HorizontalAxisId,
            VerticalAxisId = nextSensor.VerticalAxisId,
            RhInput = rhInput,
            BoilerCondition = configuration.ActiveBoilerCondition,
            Mode = mode,
            ConfigurationVersion = configuration.ConfigurationVersion,
            ManualDelaySeconds = configuration.ManualDelaySeconds,
            RhTempMin = configuration.RhTempMin,
            RhTempMax = configuration.RhTempMax,
            RhInputWindowSeconds = configuration.RhInputWindowSeconds,
            AutoDelayMinSeconds = range.AutoDelayMinSeconds,
            AutoDelayMaxSeconds = range.AutoDelayMaxSeconds,
            BaseDelaySeconds = baseDelay,
            Zone = zone,
            ZoneFactor = zoneFactor,
            AppliedDelaySeconds = ApplyZoneFactor(baseDelay, zoneFactor),
            CreatedAt = UtcTimestamps.Format(atUtc),
        });
    }

    /// <summary>
    /// The live preview of a NEW decision at the same instant. It never mutates, never freezes and
    /// never authorises anything: <see cref="TimeDelayPreview.AffectsExecution"/> is always false.
    /// A refused preview reports its typed refusal and never a silent zero.
    /// </summary>
    public static TimeDelayPreview Preview(
        TimeDelayConfiguration? configuration,
        TimeDelaySensorIdentity? nextSensor,
        RhTimeDelayInput? rhInput,
        DateTimeOffset atUtc)
    {
        var mode = configuration?.Mode ?? TimeDelayMode.MANUAL_FIXED;
        var zone = nextSensor is null ? DelayZone.LOWER : ZoneFor(nextSensor.Wall, nextSensor.OrderWall);
        var factor = mode == TimeDelayMode.AUTO_RH_LINEAR_FROZEN ? ZoneFactorFor(zone) : LowerZoneFactor;

        TimeDelayPreview Base(CountdownRefusalCode refusal) => new()
        {
            Available = false,
            AffectsExecution = false,
            RefusalCode = refusal,
            Mode = mode,
            RhTemperature = rhInput?.RhTemperature,
            RhQuality = rhInput?.Quality,
            RhObservedAt = rhInput?.ObservedAt,
            RhGeneration = rhInput?.Generation,
            Zone = zone,
            ZoneFactor = factor,
            BoilerCondition = configuration?.ActiveBoilerCondition,
        };

        if (!TryValidateConfiguration(configuration, out var refusal))
        {
            return Base(refusal);
        }

        if (nextSensor is null)
        {
            return Base(CountdownRefusalCode.QUEUE_EMPTY);
        }

        if (!TryValidateSensorIdentity(nextSensor, out refusal))
        {
            return Base(refusal);
        }

        if (!TryResolveActiveRange(configuration!, out var range, out refusal))
        {
            return Base(refusal);
        }

        if (mode == TimeDelayMode.MANUAL_FIXED)
        {
            var manualBase = configuration!.ManualDelaySeconds;
            return new TimeDelayPreview
            {
                Available = true,
                AffectsExecution = false,
                RefusalCode = CountdownRefusalCode.NONE,
                Mode = mode,
                RhTemperature = rhInput?.RhTemperature,
                RhQuality = rhInput?.Quality,
                RhObservedAt = rhInput?.ObservedAt,
                RhGeneration = rhInput?.Generation,
                ClampedRhTemperature = null,
                MappedDelaySeconds = null,
                BaseDelaySeconds = manualBase,
                Zone = zone,
                ZoneFactor = LowerZoneFactor,
                AppliedDelaySeconds = ApplyZoneFactor(manualBase, LowerZoneFactor),
                BoilerCondition = configuration.ActiveBoilerCondition,
            };
        }

        if (!TryValidateRhInput(configuration!, rhInput, atUtc, out _, out refusal))
        {
            return Base(refusal);
        }

        var clamped = ClampRhTemperature(rhInput!.RhTemperature, configuration!.RhTempMin, configuration.RhTempMax);
        if (!TryMapInverseLinear(
                clamped,
                configuration.RhTempMin,
                configuration.RhTempMax,
                range!.AutoDelayMinSeconds,
                range.AutoDelayMaxSeconds,
                out var mapped))
        {
            return Base(CountdownRefusalCode.RH_RANGE_INVALID);
        }

        var baseDelay = TruncateToWholeSeconds(mapped);
        return new TimeDelayPreview
        {
            Available = true,
            AffectsExecution = false,
            RefusalCode = CountdownRefusalCode.NONE,
            Mode = mode,
            RhTemperature = rhInput.RhTemperature,
            RhQuality = rhInput.Quality,
            RhObservedAt = rhInput.ObservedAt,
            RhGeneration = rhInput.Generation,
            ClampedRhTemperature = clamped,
            MappedDelaySeconds = mapped,
            BaseDelaySeconds = baseDelay,
            Zone = zone,
            ZoneFactor = factor,
            AppliedDelaySeconds = ApplyZoneFactor(baseDelay, factor),
            BoilerCondition = configuration.ActiveBoilerCondition,
        };
    }

    private static bool TryValidateSensorIdentity(
        TimeDelaySensorIdentity? identity,
        out CountdownRefusalCode refusal)
    {
        refusal = CountdownRefusalCode.SENSOR_IDENTITY_INVALID;
        if (identity is null) return false;
        if (string.IsNullOrWhiteSpace(identity.SensorId)) return false;
        if (identity.Wall is not (Wall.LEFT or Wall.REAR or Wall.RIGHT or Wall.FRONT)) return false;
        if (identity.OrderWall < 1) return false;
        if (string.IsNullOrWhiteSpace(identity.CanonicalWaterJetId)) return false;
        if (string.IsNullOrWhiteSpace(identity.EffectiveWaterJetId)) return false;
        if (string.IsNullOrWhiteSpace(identity.IsolationValveId)) return false;
        if (string.IsNullOrWhiteSpace(identity.CabinetId)) return false;
        if (string.IsNullOrWhiteSpace(identity.ControllerId)) return false;
        if (string.IsNullOrWhiteSpace(identity.HorizontalAxisId)) return false;
        if (string.IsNullOrWhiteSpace(identity.VerticalAxisId)) return false;
        if (string.IsNullOrWhiteSpace(identity.EffectiveWaterJetProvenance)) return false;

        // H3-A has no per-entry Water Jet override: a decision that claims one is refused rather
        // than silently frozen with an identity the Runtime cannot execute.
        if (!string.Equals(identity.EffectiveWaterJetId, identity.CanonicalWaterJetId, StringComparison.Ordinal)
            || !string.Equals(identity.EffectiveWaterJetProvenance, CanonicalAssignmentProvenance, StringComparison.Ordinal))
        {
            refusal = CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID;
            return false;
        }

        // Defence in depth: a decision may only freeze the identity the canonical catalogs derive
        // right now. A stale, foreign or hand-built identity is refused instead of being recorded
        // as if it had been derived, so the frozen evidence can never disagree with the topology.
        var waterJet = WaterJetTopologyCatalog.FindWaterJet(identity.EffectiveWaterJetId);
        var binding = MotionTopologyCatalog.FindWaterJetBinding(identity.EffectiveWaterJetId);
        if (waterJet is null
            || binding is null
            || !string.Equals(identity.IsolationValveId, waterJet.DedicatedIsolationValveId, StringComparison.Ordinal)
            || !string.Equals(identity.CabinetId, binding.CabinetId, StringComparison.Ordinal)
            || !string.Equals(identity.ControllerId, binding.ControllerId, StringComparison.Ordinal)
            || !string.Equals(identity.HorizontalAxisId, binding.HorizontalAxisId, StringComparison.Ordinal)
            || !string.Equals(identity.VerticalAxisId, binding.VerticalAxisId, StringComparison.Ordinal))
        {
            refusal = CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID;
            return false;
        }

        refusal = CountdownRefusalCode.NONE;
        return true;
    }
}
