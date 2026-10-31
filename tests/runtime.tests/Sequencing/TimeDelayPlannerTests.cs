using Wjss.Contracts;
using Wjss.Runtime.Core.Motion;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests.Sequencing;

/// <summary>
/// The H3-A TimeDelay decision arithmetic, proved numerically and by refusal. Every test calls the
/// pure planner with explicit inputs: no clock, no configuration store and no Runtime is involved, so
/// a passing test is a statement about the decision rule itself.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local Release build and the Full .NET
/// matrix are the validation of record).
/// </summary>
public sealed class TimeDelayPlannerTests
{
    /// <summary>The decision instant of every test: a fixed, explicit instant, never wall time.</summary>
    private static readonly DateTimeOffset At = new(2026, 10, 8, 0, 0, 18, TimeSpan.Zero);

    private static readonly DateTimeOffset ReleaseAt = At.AddSeconds(-1);

    private static TimeDelayConfiguration Auto(BoilerCondition condition = BoilerCondition.VERY_CLEAN) =>
        TimeDelayConfiguration.LegacySeededSimulationDefaults(
            TimeDelayMode.AUTO_RH_LINEAR_FROZEN, activeBoilerCondition: condition);

    private static TimeDelayConfiguration Manual(int seconds) =>
        TimeDelayConfiguration.LegacySeededSimulationDefaults(TimeDelayMode.MANUAL_FIXED, seconds);

    /// <summary>One fully formed canonical identity; the LEFT wall order drives the zone.</summary>
    private static TimeDelaySensorIdentity Sensor(
        Wall wall = Wall.LEFT,
        int orderWall = 2,
        string sensorId = "G+202") =>
        new(sensorId, wall, orderWall, "WJ1", "WJ1", TimeDelayPlanner.CanonicalAssignmentProvenance,
            "IV1", "WSB1", "MC-WSB1", "A", "B");

    /// <summary>A LOWER-zone identity: no zone factor is applied, so the pure mapping is visible.</summary>
    private static TimeDelaySensorIdentity LowerSensor() => Sensor(Wall.LEFT, 12);

    private static RhTimeDelayInput Rh(
        double temperature,
        Quality quality = Quality.GOOD,
        long generation = 19,
        DateTimeOffset? observedAt = null) =>
        new()
        {
            RhTemperature = temperature,
            Quality = quality,
            ObservedAt = UtcTimestamps.Format(observedAt ?? ReleaseAt),
            Generation = generation,
        };

    private static NextDispatchDelayDecision PlanAuto(
        TimeDelayConfiguration configuration,
        RhTimeDelayInput? rh,
        TimeDelaySensorIdentity? sensor = null) =>
        Assert.IsType<NextDispatchDelayDecision>(TimeDelayPlanner
            .Plan(configuration, sensor ?? LowerSensor(), rh, At, "SYN-TD-1", 1, 7, "SYN-QE-2").Decision);

    private static NextDispatchDelayDecision PlanManual(int seconds, TimeDelaySensorIdentity? sensor = null) =>
        Assert.IsType<NextDispatchDelayDecision>(TimeDelayPlanner
            .Plan(Manual(seconds), sensor ?? Sensor(), null, At, "SYN-TD-1", 1, 7, "SYN-QE-2").Decision);

    private static CountdownRefusalCode Refusal(
        TimeDelayConfiguration? configuration,
        RhTimeDelayInput? rh,
        TimeDelaySensorIdentity? sensor = null) =>
        TimeDelayPlanner.Plan(configuration, sensor ?? LowerSensor(), rh, At, "SYN-TD-1", 1, 7, "SYN-QE-2")
            .RefusalCode;

    // ---------------------------------------------------------------- MANUAL_FIXED

    [Fact]
    public void Manual_fixed_uses_the_configured_delay_and_ignores_the_RH_input()
    {
        var decision = PlanManual(5);

        Assert.Equal(TimeDelayMode.MANUAL_FIXED, decision.Mode);
        Assert.Equal(5, decision.BaseDelaySeconds);
        Assert.Equal(5, decision.AppliedDelaySeconds);
        Assert.Equal(1.00, decision.ZoneFactor);
        Assert.Null(decision.RhInput);
        Assert.Equal(5, decision.ManualDelaySeconds);
        Assert.Equal("SYN-TD-1", decision.DecisionId);
        Assert.Equal(1, decision.CountdownGeneration);
        Assert.Equal(7, decision.QueueGeneration);
        Assert.Equal("SYN-QE-2", decision.QueueEntryId);
        Assert.Equal(UtcTimestamps.Format(At), decision.CreatedAt);
        Assert.Equal(TimeDelayConfiguration.LegacySeededSimulationVersion, decision.ConfigurationVersion);
    }

    [Fact]
    public void Manual_fixed_ignores_the_upper_zone_but_still_records_it()
    {
        var upper = PlanManual(600, Sensor(Wall.LEFT, 2));
        var lower = PlanManual(600, Sensor(Wall.LEFT, 12));

        Assert.Equal(DelayZone.UPPER, upper.Zone);
        Assert.Equal(DelayZone.LOWER, lower.Zone);
        Assert.Equal(1.00, upper.ZoneFactor);
        Assert.Equal(1.00, lower.ZoneFactor);
        Assert.Equal(600, upper.AppliedDelaySeconds);
        Assert.Equal(600, lower.AppliedDelaySeconds);
    }

    [Theory]
    [InlineData(Quality.UNCERTAIN)]
    [InlineData(Quality.BAD)]
    [InlineData(Quality.STALE)]
    [InlineData(Quality.DISABLED)]
    public void Manual_fixed_is_unaffected_by_the_RH_quality(Quality quality)
    {
        var plan = TimeDelayPlanner.Plan(Manual(4), Sensor(), Rh(520, quality), At, "SYN-TD-1", 1, 7, "SYN-QE-2");

        Assert.True(plan.Accepted);
        Assert.Equal(4, plan.Decision!.AppliedDelaySeconds);
        Assert.Null(plan.Decision.RhInput);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    [InlineData(int.MinValue)]
    public void Manual_fixed_refuses_a_delay_outside_the_configuration_bound(int seconds)
    {
        var plan = TimeDelayPlanner.Plan(Manual(seconds), Sensor(), null, At, "SYN-TD-1", 1, 7, "SYN-QE-2");

        Assert.False(plan.Accepted);
        Assert.Equal(CountdownRefusalCode.MANUAL_DELAY_OUT_OF_RANGE, plan.RefusalCode);
        Assert.Null(plan.Decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3600)]
    public void Manual_fixed_accepts_the_configuration_bound_inclusive(int seconds)
    {
        Assert.Equal(seconds, PlanManual(seconds).AppliedDelaySeconds);
    }

    // ---------------------------------------------------------------- AUTO_RH_LINEAR_FROZEN

    [Fact]
    public void Auto_at_the_RH_minimum_produces_the_maximum_delay()
    {
        var decision = PlanAuto(Auto(), Rh(515));

        Assert.Equal(660, decision.AutoDelayMaxSeconds);
        Assert.Equal(600, decision.AutoDelayMinSeconds);
        Assert.Equal(660, decision.BaseDelaySeconds);
        Assert.Equal(660, decision.AppliedDelaySeconds);
        Assert.Equal(515.0, decision.RhTempMin);
        Assert.Equal(535.0, decision.RhTempMax);
        Assert.Equal(DecisionRh(decision), 515.0);
    }

    [Fact]
    public void Auto_at_the_range_midpoint_is_the_range_midpoint()
    {
        Assert.Equal(630, PlanAuto(Auto(), Rh(525)).BaseDelaySeconds);
    }

    [Fact]
    public void Auto_at_the_RH_maximum_produces_the_minimum_delay()
    {
        Assert.Equal(600, PlanAuto(Auto(), Rh(535)).BaseDelaySeconds);
    }

    [Theory]
    [InlineData(500, 660)]
    [InlineData(0, 660)]
    [InlineData(-40, 660)]
    [InlineData(600, 600)]
    [InlineData(900, 600)]
    public void Auto_clamps_an_out_of_range_reading_to_the_range_bound(double reading, int expectedBaseDelay)
    {
        var decision = PlanAuto(Auto(), Rh(reading));

        // The frozen evidence keeps the reading as it was observed; the clamp is the mapping's, and
        // the clamped result is proved by the base delay being exactly the range-bound delay.
        Assert.Equal(reading, DecisionRh(decision));
        Assert.Equal(expectedBaseDelay, decision.BaseDelaySeconds);
    }

    [Theory]
    [InlineData(11, DelayZone.UPPER, 0.75)]
    [InlineData(12, DelayZone.LOWER, 1.00)]
    [InlineData(1, DelayZone.UPPER, 0.75)]
    [InlineData(24, DelayZone.LOWER, 1.00)]
    public void The_LEFT_RIGHT_upper_boundary_is_strictly_below_12(int orderWall, DelayZone zone, double factor)
    {
        foreach (var wall in new[] { Wall.LEFT, Wall.RIGHT })
        {
            var decision = PlanAuto(Auto(), Rh(515), Sensor(wall, orderWall));

            Assert.Equal(zone, decision.Zone);
            Assert.Equal(factor, decision.ZoneFactor);
            Assert.Equal(TimeDelayPlanner.ApplyZoneFactor(660, factor), decision.AppliedDelaySeconds);
        }
    }

    [Theory]
    [InlineData(14, DelayZone.UPPER, 0.75)]
    [InlineData(15, DelayZone.LOWER, 1.00)]
    [InlineData(30, DelayZone.LOWER, 1.00)]
    public void The_FRONT_REAR_upper_boundary_is_strictly_below_15(int orderWall, DelayZone zone, double factor)
    {
        foreach (var wall in new[] { Wall.REAR, Wall.FRONT })
        {
            var decision = PlanAuto(Auto(), Rh(515), Sensor(wall, orderWall));

            Assert.Equal(zone, decision.Zone);
            Assert.Equal(factor, decision.ZoneFactor);
        }
    }

    [Fact]
    public void The_upper_zone_factor_can_never_raise_the_delay()
    {
        for (var orderWall = 1; orderWall <= 30; orderWall++)
        {
            var decision = PlanAuto(Auto(), Rh(515), Sensor(Wall.LEFT, orderWall));
            Assert.InRange(decision.AppliedDelaySeconds, 0, decision.BaseDelaySeconds);
        }
    }

    [Fact]
    public void The_base_delay_truncates_toward_zero()
    {
        // 660 + (516.5 - 515) * (600 - 660) / 20 = 655.5, truncated to 655.
        var decision = PlanAuto(Auto(), Rh(516.5));

        Assert.Equal(655, decision.BaseDelaySeconds);
        Assert.Equal(Convert.ToInt32(655 * 0.75), decision.AppliedDelaySeconds);
    }

    [Theory]
    [InlineData(2, 0.75, 2)]   // 1.5 rounds half to EVEN: 2
    [InlineData(6, 0.75, 4)]   // 4.5 rounds half to EVEN: 4
    [InlineData(660, 0.75, 495)]
    [InlineData(655, 0.75, 491)]
    [InlineData(600, 1.00, 600)]
    [InlineData(0, 0.75, 0)]
    public void The_zone_factor_uses_the_owner_specified_Convert_ToInt32_conversion(
        int baseDelay, double factor, int expected)
    {
        Assert.Equal(expected, TimeDelayPlanner.ApplyZoneFactor(baseDelay, factor));
        Assert.Equal(Convert.ToInt32(baseDelay * factor), expected);
    }

    [Fact]
    public void Every_boiler_condition_maps_with_its_own_range()
    {
        var expected = new (BoilerCondition Condition, int Min, int Max)[]
        {
            (BoilerCondition.VERY_CLEAN, 600, 660),
            (BoilerCondition.NORMAL_CLEAN, 500, 550),
            (BoilerCondition.MEDIUM_DIRTY, 340, 380),
            (BoilerCondition.VERY_DIRTY, 240, 280),
        };

        foreach (var (condition, min, max) in expected)
        {
            var decision = PlanAuto(Auto(condition), Rh(525));
            Assert.Equal(condition, decision.BoilerCondition);
            Assert.Equal(min, decision.AutoDelayMinSeconds);
            Assert.Equal(max, decision.AutoDelayMaxSeconds);
            // The midpoint of 515..535 maps to the midpoint of the condition range.
            Assert.Equal((int)Math.Truncate((min + max) / 2.0), decision.BaseDelaySeconds);
        }
    }

    [Fact]
    public void A_configuration_change_never_alters_a_frozen_decision()
    {
        var frozen = PlanAuto(Auto(BoilerCondition.NORMAL_CLEAN), Rh(525));
        var later = PlanAuto(Auto(BoilerCondition.VERY_DIRTY), Rh(525));

        Assert.Equal(500, frozen.AutoDelayMinSeconds);
        Assert.Equal(550, frozen.AutoDelayMaxSeconds);
        Assert.Equal(525, frozen.BaseDelaySeconds);
        Assert.Equal(240, later.AutoDelayMinSeconds);
        Assert.Equal(260, later.BaseDelaySeconds);
    }

    [Fact]
    public void Planning_is_deterministic()
    {
        var first = PlanAuto(Auto(), Rh(516.5));
        var second = PlanAuto(Auto(), Rh(516.5));

        Assert.Equal(first, second);
        Assert.Equal(first.AppliedDelaySeconds, second.AppliedDelaySeconds);
        Assert.Equal(first.ZoneFactor, second.ZoneFactor);
    }

    // ---------------------------------------------------------------- refusals: configuration

    [Fact]
    public void A_missing_configuration_is_refused()
    {
        var plan = TimeDelayPlanner.Plan(null, Sensor(), Rh(520), At, "SYN-TD-1", 1, 7, "SYN-QE-2");

        Assert.False(plan.Accepted);
        Assert.Equal(CountdownRefusalCode.CONFIGURATION_MISSING, plan.RefusalCode);
        Assert.Null(plan.Decision);
    }

    [Theory]
    [InlineData(515.0, 515.0)]
    [InlineData(535.0, 515.0)]
    [InlineData(double.NaN, 535.0)]
    public void Auto_refuses_an_invalid_RH_range(double min, double max)
    {
        Assert.Equal(CountdownRefusalCode.RH_RANGE_INVALID,
            Refusal(Auto() with { RhTempMin = min, RhTempMax = max }, Rh(520)));
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 3601)]
    [InlineData(-5, -1)]
    public void Auto_refuses_an_invalid_auto_delay_bound(int min, int max)
    {
        Assert.Equal(CountdownRefusalCode.AUTO_DELAY_RANGE_INVALID,
            Refusal(Auto() with
            {
                BoilerRanges =
                [
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.VERY_CLEAN,
                        AutoDelayMinSeconds = min,
                        AutoDelayMaxSeconds = max,
                    },
                ],
            }, Rh(520)));
    }

    [Fact]
    public void Auto_refuses_an_inverted_auto_delay_range()
    {
        Assert.Equal(CountdownRefusalCode.AUTO_DELAY_ORDER_INVALID,
            Refusal(Auto() with
            {
                BoilerRanges =
                [
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.VERY_CLEAN,
                        AutoDelayMinSeconds = 660,
                        AutoDelayMaxSeconds = 600,
                    },
                ],
            }, Rh(520)));
    }

    [Fact]
    public void Auto_refuses_a_boiler_condition_with_no_range()
    {
        Assert.Equal(CountdownRefusalCode.BOILER_CONDITION_RANGE_MISSING,
            Refusal(Auto(BoilerCondition.MEDIUM_DIRTY) with
            {
                BoilerRanges =
                [
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.VERY_CLEAN,
                        AutoDelayMinSeconds = 600,
                        AutoDelayMaxSeconds = 660,
                    },
                ],
            }, Rh(520)));
    }

    [Fact]
    public void Auto_refuses_a_duplicated_boiler_range()
    {
        Assert.Equal(CountdownRefusalCode.BOILER_CONDITION_DUPLICATE,
            Refusal(Auto() with
            {
                BoilerRanges =
                [
                    new BoilerConditionDelayRange { Condition = BoilerCondition.VERY_CLEAN, AutoDelayMinSeconds = 600, AutoDelayMaxSeconds = 660 },
                    new BoilerConditionDelayRange { Condition = BoilerCondition.VERY_CLEAN, AutoDelayMinSeconds = 610, AutoDelayMaxSeconds = 670 },
                ],
            }, Rh(520)));
    }

    [Fact]
    public void Auto_refuses_an_empty_boiler_range_list()
    {
        Assert.Equal(CountdownRefusalCode.BOILER_CONDITION_RANGE_MISSING,
            Refusal(Auto() with { BoilerRanges = [] }, Rh(520)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Auto_refuses_an_invalid_RH_input_window(int window)
    {
        Assert.Equal(CountdownRefusalCode.RH_INPUT_WINDOW_INVALID,
            Refusal(Auto() with { RhInputWindowSeconds = window }, Rh(520)));
    }

    [Fact]
    public void Auto_refuses_a_missing_configuration_version()
    {
        Assert.Equal(CountdownRefusalCode.CONFIGURATION_VERSION_MISSING,
            Refusal(Auto() with { ConfigurationVersion = " " }, Rh(520)));
    }

    // ---------------------------------------------------------------- refusals: RH input

    [Fact]
    public void Auto_refuses_a_missing_RH_reading()
    {
        Assert.Equal(CountdownRefusalCode.RH_MISSING, Refusal(Auto(), null));
    }

    [Fact]
    public void Auto_refuses_a_stale_RH_reading_but_accepts_the_window_edge()
    {
        Assert.Equal(CountdownRefusalCode.RH_STALE,
            Refusal(Auto(), Rh(520, observedAt: At.AddSeconds(-31))));

        var edge = TimeDelayPlanner.Plan(
            Auto(), LowerSensor(), Rh(520, observedAt: At.AddSeconds(-30)), At, "SYN-TD-1", 1, 7, "SYN-QE-2");
        Assert.True(edge.Accepted);
        // 660 + (520 - 515) * (600 - 660) / 20 = 645.
        Assert.Equal(645, edge.Decision!.BaseDelaySeconds);
    }

    [Fact]
    public void Auto_refuses_a_future_dated_RH_reading()
    {
        Assert.Equal(CountdownRefusalCode.RH_FUTURE_DATED,
            Refusal(Auto(), Rh(520, observedAt: At.AddSeconds(1))));
    }

    [Theory]
    [InlineData(Quality.UNCERTAIN)]
    [InlineData(Quality.BAD)]
    [InlineData(Quality.STALE)]
    [InlineData(Quality.DISABLED)]
    public void Auto_refuses_a_bad_quality_RH_reading(Quality quality)
    {
        Assert.Equal(CountdownRefusalCode.RH_BAD_QUALITY, Refusal(Auto(), Rh(520, quality)));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Auto_refuses_an_invalid_RH_generation(long generation)
    {
        Assert.Equal(CountdownRefusalCode.RH_GENERATION_INVALID, Refusal(Auto(), Rh(520, generation: generation)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Auto_refuses_an_invalid_RH_value(double temperature)
    {
        Assert.Equal(CountdownRefusalCode.RH_VALUE_INVALID, Refusal(Auto(), Rh(temperature)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-10-08T00:00:00Z")]
    [InlineData("2026-10-08 00:00:00.000")]
    [InlineData("2026-10-08T00:00:00.000+07:00")]
    public void Auto_refuses_a_malformed_RH_timestamp(string observedAt)
    {
        var reading = new RhTimeDelayInput
        {
            RhTemperature = 520,
            Quality = Quality.GOOD,
            ObservedAt = observedAt,
            Generation = 1,
        };

        Assert.Equal(CountdownRefusalCode.RH_TIMESTAMP_INVALID, Refusal(Auto(), reading));
    }

    [Fact]
    public void An_unresolvable_sensor_identity_is_refused()
    {
        var plan = TimeDelayPlanner.Plan(Auto(), null, Rh(520), At, "SYN-TD-1", 1, 7, "SYN-QE-2");

        Assert.False(plan.Accepted);
        Assert.Equal(CountdownRefusalCode.SENSOR_IDENTITY_INVALID, plan.RefusalCode);
    }

    [Fact]
    public void An_override_identity_is_refused_because_H3A_has_no_override()
    {
        var overrideIdentity = Sensor() with
        {
            EffectiveWaterJetId = "WJ4",
            EffectiveWaterJetProvenance = "ENTRY_WJ_OVERRIDE",
        };

        Assert.Equal(CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID,
            Refusal(Auto(), Rh(520), overrideIdentity));

        // A mismatched canonical pairing is refused as well: WJ1 is paired with IV1 only.
        Assert.Equal(CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID,
            Refusal(Auto(), Rh(520), Sensor() with { IsolationValveId = "IV4" }));
    }

    [Fact]
    public void An_invalid_decision_identity_is_refused()
    {
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID,
            TimeDelayPlanner.Plan(Auto(), LowerSensor(), Rh(520), At, "SYN-TD-1", 0, 7, "SYN-QE-2").RefusalCode);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID,
            TimeDelayPlanner.Plan(Auto(), LowerSensor(), Rh(520), At, " ", 1, 7, "SYN-QE-2").RefusalCode);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID,
            TimeDelayPlanner.Plan(Auto(), LowerSensor(), Rh(520), At, "SYN-TD-1", 1, 7, "").RefusalCode);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID,
            TimeDelayPlanner.Plan(Auto(), LowerSensor(), Rh(520), At, "SYN-TD-1", 1, -1, "SYN-QE-2").RefusalCode);
    }

    // ---------------------------------------------------------------- arithmetic guards

    [Theory]
    [InlineData(515.0, 515.0)]
    [InlineData(535.0, 515.0)]
    [InlineData(double.NaN, 535.0)]
    public void The_inverse_mapping_refuses_a_non_positive_span(double min, double max)
    {
        Assert.False(TimeDelayPlanner.TryMapInverseLinear(520, min, max, 600, 660, out var mapped));
        Assert.Equal(0.0, mapped);
    }

    [Fact]
    public void The_inverse_mapping_is_linear_and_inverse_at_both_ends()
    {
        Assert.True(TimeDelayPlanner.TryMapInverseLinear(515, 515, 535, 600, 660, out var low));
        Assert.True(TimeDelayPlanner.TryMapInverseLinear(535, 515, 535, 600, 660, out var high));
        Assert.True(TimeDelayPlanner.TryMapInverseLinear(525, 515, 535, 600, 660, out var middle));

        Assert.Equal(660.0, low);
        Assert.Equal(600.0, high);
        Assert.Equal(630.0, middle);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.4, 0)]
    [InlineData(1.9, 1)]
    [InlineData(-3.4, 0)]
    [InlineData(655.5, 655)]
    public void Truncation_toward_zero_never_rounds_up(double seconds, int expected)
    {
        Assert.Equal(expected, TimeDelayPlanner.TruncateToWholeSeconds(seconds));
    }

    [Fact]
    public void A_negative_applied_delay_is_impossible()
    {
        for (var baseDelay = 0; baseDelay <= 3600; baseDelay += 137)
        {
            Assert.InRange(TimeDelayPlanner.ApplyZoneFactor(baseDelay, TimeDelayPlanner.UpperZoneFactor), 0, baseDelay);
            Assert.InRange(TimeDelayPlanner.ApplyZoneFactor(baseDelay, TimeDelayPlanner.LowerZoneFactor), 0, baseDelay);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TimeDelayPlanner.ApplyZoneFactor(-1, TimeDelayPlanner.LowerZoneFactor));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TimeDelayPlanner.ApplyZoneFactor(10, double.NaN));
    }

    // ---------------------------------------------------------------- preview

    [Fact]
    public void The_preview_never_affects_execution_and_never_touches_the_frozen_decision()
    {
        var decision = PlanAuto(Auto(), Rh(525));
        var preview = TimeDelayPlanner.Preview(Auto(), Sensor(Wall.LEFT, 12), Rh(516.5), At);

        Assert.False(preview.AffectsExecution);
        Assert.True(preview.Available);
        Assert.Equal(CountdownRefusalCode.NONE, preview.RefusalCode);
        Assert.Equal(516.5, preview.RhTemperature);
        Assert.Equal(516.5, preview.ClampedRhTemperature);
        Assert.Equal(655.5, preview.MappedDelaySeconds);
        Assert.Equal(655, preview.BaseDelaySeconds);
        Assert.Equal(1.00, preview.ZoneFactor);
        Assert.Equal(655, preview.AppliedDelaySeconds);

        // The frozen decision is unchanged: a preview is a projection, never a mutation.
        Assert.Equal(630, decision.BaseDelaySeconds);
        Assert.Equal(525.0, DecisionRh(decision));
    }

    [Fact]
    public void The_preview_reports_the_clamped_value_of_an_out_of_range_reading()
    {
        var preview = TimeDelayPlanner.Preview(Auto(), LowerSensor(), Rh(900), At);

        Assert.True(preview.Available);
        Assert.Equal(900.0, preview.RhTemperature);
        Assert.Equal(535.0, preview.ClampedRhTemperature);
        Assert.Equal(600, preview.BaseDelaySeconds);
        Assert.Equal(600, preview.AppliedDelaySeconds);
    }

    [Fact]
    public void A_refused_preview_reports_its_code_and_no_silent_zero()
    {
        var preview = TimeDelayPlanner.Preview(Auto(), LowerSensor(), null, At);

        Assert.False(preview.Available);
        Assert.False(preview.AffectsExecution);
        Assert.Equal(CountdownRefusalCode.RH_MISSING, preview.RefusalCode);
        Assert.Null(preview.AppliedDelaySeconds);
        Assert.Null(preview.BaseDelaySeconds);
    }

    [Fact]
    public void A_preview_without_a_next_sensor_is_refused_as_an_empty_queue()
    {
        var preview = TimeDelayPlanner.Preview(Auto(), null, Rh(520), At);

        Assert.False(preview.Available);
        Assert.Equal(CountdownRefusalCode.QUEUE_EMPTY, preview.RefusalCode);
        Assert.Null(preview.AppliedDelaySeconds);
    }

    [Fact]
    public void A_manual_preview_needs_no_RH_reading()
    {
        var preview = TimeDelayPlanner.Preview(Manual(5), Sensor(), null, At);

        Assert.True(preview.Available);
        Assert.False(preview.AffectsExecution);
        Assert.Equal(5, preview.BaseDelaySeconds);
        Assert.Equal(5, preview.AppliedDelaySeconds);
        Assert.Equal(1.00, preview.ZoneFactor);
        Assert.Null(preview.RhTemperature);
    }

    // ---------------------------------------------------------------- sensor identity

    [Fact]
    public void The_sensor_identity_is_derived_from_the_canonical_authorities()
    {
        var sensor = new SensorPresentationState
        {
            SensorId = "G+202",
            PositionKind = LogicalPositionKind.SENSOR,
            Wall = Wall.LEFT,
            LogicalColumn = 2,
            LogicalRow = 2,
            WallColumn = 2,
            WallRow = 2,
            ScanOrder = 14,
            DeviceId = "SYN-TC-01",
            AssignedWaterJetId = "WJ1",
            AssignedIsolationValveId = "IV1",
            TcFrontChannel = "SYN-TC-01:CH26",
            TcRearChannel = "SYN-TC-01:CH27",
            Classification = Classification.NOT_CLASSIFIED,
            ClassificationBasis = ClassificationBasis.NONE,
            Quality = Quality.GOOD,
            QueueState = QueueState.NONE,
            IsActiveJobTarget = false,
            AlarmState = AlarmState.NONE,
        };

        Assert.True(TimeDelayPlanner.TryResolveSensorIdentity(sensor, out var identity, out var refusal));
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.NotNull(identity);
        Assert.Equal("G+202", identity!.SensorId);
        Assert.Equal(Wall.LEFT, identity.Wall);
        // Wall-local ordinal of LEFT row 2, column 2 of four columns: (2 - 1) * 4 + 2 = 6.
        Assert.Equal(6, identity.OrderWall);
        Assert.Equal("WJ1", identity.CanonicalWaterJetId);
        Assert.Equal("WJ1", identity.EffectiveWaterJetId);
        Assert.Equal(TimeDelayPlanner.CanonicalAssignmentProvenance, identity.EffectiveWaterJetProvenance);
        Assert.Equal("IV1", identity.IsolationValveId);

        // The WSB cabinet, controller and axis pair are the canonical motion topology values.
        var binding = MotionTopologyCatalog.FindWaterJetBinding("WJ1");
        Assert.NotNull(binding);
        Assert.Equal(binding!.CabinetId, identity.CabinetId);
        Assert.Equal(binding.ControllerId, identity.ControllerId);
        Assert.Equal(binding.HorizontalAxisId, identity.HorizontalAxisId);
        Assert.Equal(binding.VerticalAxisId, identity.VerticalAxisId);
        Assert.Equal("A", identity.HorizontalAxisId);
        Assert.Equal("B", identity.VerticalAxisId);
        Assert.Equal("MC-WSB1", identity.ControllerId);

        // A mismatched pairing is refused: the assignment authority is WJn with IVn only.
        Assert.False(TimeDelayPlanner.TryResolveSensorIdentity(
            sensor with { AssignedIsolationValveId = "IV4" }, out var mismatched, out var mismatchRefusal));
        Assert.Null(mismatched);
        Assert.Equal(CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID, mismatchRefusal);

        // An unknown Water Jet is refused rather than defaulted.
        Assert.False(TimeDelayPlanner.TryResolveSensorIdentity(
            sensor with { AssignedWaterJetId = "WJ9" }, out _, out var unknownRefusal));
        Assert.Equal(CountdownRefusalCode.ASSIGNMENT_DERIVATION_INVALID, unknownRefusal);

        // An out-of-wall geometry is refused instead of producing an ordinal.
        Assert.False(TimeDelayPlanner.TryResolveSensorIdentity(
            sensor with { WallColumn = 5 }, out _, out var geometryRefusal));
        Assert.Equal(CountdownRefusalCode.SENSOR_IDENTITY_INVALID, geometryRefusal);
    }

    /// <summary>The frozen RH reading of a decision (raw, as observed), or a test-failing sentinel.</summary>
    private static double DecisionRh(NextDispatchDelayDecision decision) =>
        Assert.IsType<RhTimeDelayInput>(decision.RhInput).RhTemperature;
}
