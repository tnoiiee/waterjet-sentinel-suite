using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Deterministic synthetic evolution: repeatability from one seed and tick
/// sequence, order independence of the Sensor update, atomic refusal, the
/// protected-baseline shape after evolution, wall-summary recalculation from the
/// complete evolved Sensor set, bounded trend append, and the four quality paths
/// (normal, uncertain-with-last-validated, stale/degraded, recovery).
///
/// Every value is a synthetic development presentation value; nothing here
/// asserts a process meaning, a device address or a Production limit.
/// </summary>
public sealed class SyntheticEvolutionTests
{
    private static readonly ulong Seed = SyntheticSeed.DefaultSeed.Value;

    [Fact]
    public void Same_Seed_Tick_Sequence_And_Clock_Produce_The_Same_State()
    {
        var first = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var second = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var firstSnapshots = new List<string>();

        for (var tick = 1; tick <= 4; tick++)
        {
            var instant = RuntimeTestFixture.Instant.AddSeconds(tick);
            var firstOutcome = RuntimeSyntheticEvolution.Tick(first, Seed, tick, instant);
            var secondOutcome = RuntimeSyntheticEvolution.Tick(second, Seed, tick, instant);

            Assert.True(firstOutcome.Accepted, firstOutcome.RefusalReason);
            Assert.True(secondOutcome.Accepted, secondOutcome.RefusalReason);

            first = firstOutcome.Result!.State;
            second = secondOutcome.Result!.State;

            Assert.Equal(first.Sensors, second.Sensors);
            Assert.Equal(first.Walls, second.Walls);
            RuntimeTestFixture.AssertTrendPointsEquivalent(first.Trend.Points, second.Trend.Points);
            Assert.Equal(
                RuntimeTestFixture.SerializeSnapshot(first),
                RuntimeTestFixture.SerializeSnapshot(second));

            firstSnapshots.Add(RuntimeTestFixture.SerializeSnapshot(first));
        }

        Assert.Equal(4, firstSnapshots.Distinct().Count());
        Assert.Equal(5, first.Revision);
    }

    [Fact]
    public void Different_Seeds_Produce_Different_Evolved_Values()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var instant = RuntimeTestFixture.Instant.AddSeconds(1);

        // The helper's fourth parameter is the rules object and the fifth is the
        // seed, so the seed is passed by name: two distinct seeds, one tick, one
        // explicit instant, default rules.
        var first = Advance(state, 1, instant, seed: 1UL);
        var second = Advance(state, 1, instant, seed: 2UL);

        Assert.NotEqual(
            first.Sensors.Select(sensor => sensor.DirtyScore).ToArray(),
            second.Sensors.Select(sensor => sensor.DirtyScore).ToArray());
    }

    [Fact]
    public void A_Sensor_Is_Advanced_Independently_Of_Every_Other_Sensor()
    {
        var baseline = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var modified = baseline with
        {
            Sensors = baseline.Sensors.Select((sensor, index) =>
                index == 7 ? sensor with { DirtyScore = 5.0 } : sensor).ToArray(),
        };

        var instant = RuntimeTestFixture.Instant.AddSeconds(1);
        var baselineOutcome = RuntimeSyntheticEvolution.Tick(baseline, Seed, 1, instant);
        var modifiedOutcome = RuntimeSyntheticEvolution.Tick(modified, Seed, 1, instant);

        Assert.True(baselineOutcome.Accepted);
        Assert.True(modifiedOutcome.Accepted);

        var baselineSensors = baselineOutcome.Result!.State.Sensors;
        var modifiedSensors = modifiedOutcome.Result!.State.Sensors;

        for (var index = 0; index < baselineSensors.Count; index++)
        {
            if (index == 7)
            {
                Assert.NotEqual(baselineSensors[index], modifiedSensors[index]);
                continue;
            }

            Assert.Equal(baselineSensors[index], modifiedSensors[index]);
        }
    }

    [Fact]
    public void Tick_Refuses_A_Skipped_Tick_Number_And_A_Non_Later_Instant_And_A_Foreign_Profile()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var instant = RuntimeTestFixture.Instant.AddSeconds(1);

        var skipped = RuntimeSyntheticEvolution.Tick(state, Seed, 4, instant);
        Assert.False(skipped.Accepted);
        Assert.Equal(RuntimeRefusalCodes.EvolutionTickSequence, skipped.RefusalCode);
        Assert.Null(skipped.Result);

        var backward = RuntimeSyntheticEvolution.Tick(state, Seed, 1, RuntimeTestFixture.Instant);
        Assert.False(backward.Accepted);
        Assert.Equal(RuntimeRefusalCodes.EvolutionTickTime, backward.RefusalCode);
        Assert.Null(backward.Result);

        var foreign = RuntimeSyntheticEvolution.Tick(
            state with { DeviceProfile = DeviceProfile.TEST_HARDWARE }, Seed, 1, instant);
        Assert.False(foreign.Accepted);
        Assert.Equal(ProfileStartPolicy.RefusalCode, foreign.RefusalCode);
        Assert.Null(foreign.Result);

        var production = RuntimeSyntheticEvolution.Tick(
            state with { DeviceProfile = DeviceProfile.PRODUCTION }, Seed, 1, instant);
        Assert.False(production.Accepted);
        Assert.Equal(ProfileStartPolicy.RefusalCode, production.RefusalCode);
        Assert.Null(production.Result);
    }

    [Fact]
    public void One_Accepted_Tick_Commits_Exactly_One_Revision_And_A_Refusal_Commits_None()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        for (var tick = 1; tick <= 3; tick++)
        {
            var outcome = RuntimeSyntheticEvolution.Tick(
                state, Seed, tick, RuntimeTestFixture.Instant.AddSeconds(tick));

            Assert.True(outcome.Accepted, outcome.RefusalReason);
            Assert.Equal(state.Revision + 1, outcome.Result!.State.Revision);

            state = writer.Commit(outcome.Result.State);

            Assert.Equal(tick + 1, store.CurrentRevision);
            Assert.Same(state, store.Current);
        }

        Assert.Equal(4, store.Counters.CommittedRevisions);
        Assert.Equal(4, store.Counters.HistoryDepth);
        Assert.Equal(0, store.Counters.RefusedCommits);

        var refused = RuntimeSyntheticEvolution.Tick(
            state, Seed, 9, RuntimeTestFixture.Instant.AddSeconds(9));

        Assert.False(refused.Accepted);
        Assert.Equal(4, store.CurrentRevision);
        Assert.Equal(0, store.Counters.RefusedCommits);
    }

    [Fact]
    public void Evolution_Preserves_The_Protected_Baseline_Shape()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var evolved = Advance(state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        Assert.Equal(CanonicalSensorMap.MatrixSlots, evolved.WallMap.Count);
        Assert.Equal(CanonicalSensorMap.SensorLocations, evolved.Sensors.Count);
        Assert.Equal(
            CanonicalSensorMap.ThermocoupleChannelCount,
            evolved.Sensors
                .SelectMany(sensor => new[] { sensor.TcFrontChannel, sensor.TcRearChannel })
                .Distinct(StringComparer.Ordinal)
                .Count());

        var cannonSlots = evolved.WallMap.Where(slot => slot.SlotType == SlotType.CANNON).ToArray();
        Assert.Equal(2, cannonSlots.Length);
        Assert.Contains(cannonSlots, slot => slot.LogicalRow == 5 && slot.LogicalColumn == 7);
        Assert.Contains(cannonSlots, slot => slot.LogicalRow == 5 && slot.LogicalColumn == 16);
        Assert.All(cannonSlots, slot => Assert.Null(slot.SensorId));

        Assert.Equal(24, evolved.Sensors.Count(sensor => sensor.Wall == Wall.LEFT));
        Assert.Equal(29, evolved.Sensors.Count(sensor => sensor.Wall == Wall.REAR));
        Assert.Equal(24, evolved.Sensors.Count(sensor => sensor.Wall == Wall.RIGHT));
        Assert.Equal(29, evolved.Sensors.Count(sensor => sensor.Wall == Wall.FRONT));
    }

    [Fact]
    public void Wall_Summaries_Are_Recalculated_From_The_Complete_Evolved_Sensor_Set()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var evolved = Advance(state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        Assert.Equal(RuntimeWallSummaries.CanonicalWallOrder.Count, evolved.Walls.Count);

        foreach (var wall in RuntimeWallSummaries.CanonicalWallOrder)
        {
            var members = evolved.Sensors.Where(sensor => sensor.Wall == wall).ToArray();
            var summary = evolved.Walls.Single(candidate => candidate.Wall == wall);

            Assert.Equal(members.Length, summary.Total);
            Assert.Equal(members.Count(sensor => sensor.Classification == Classification.DIRTY), summary.Dirty);
            Assert.Equal(members.Count(sensor => sensor.Classification == Classification.CLEANER), summary.Cleaner);
            Assert.Equal(members.Count(sensor => sensor.Classification == Classification.NOT_CLASSIFIED), summary.NotClassified);
            Assert.Equal(members.Count(sensor => sensor.Quality == Quality.UNCERTAIN), summary.Uncertain);
            Assert.Equal(members.Max(sensor => sensor.DirtyScore), summary.MaxScore);
        }
    }

    [Fact]
    public void Trend_Append_Is_Bounded_And_Drops_The_Oldest_Point()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        state = state with
        {
            Trend = new TrendWindow
            {
                Capacity = 3,
                SeriesNames = state.Trend.SeriesNames,
                Points = Array.Empty<TrendPoint>(),
            },
        };

        var published = state.Trend;

        for (var tick = 1; tick <= 5; tick++)
        {
            state = Advance(state, tick, RuntimeTestFixture.Instant.AddSeconds(tick));
        }

        Assert.Equal(3, state.Trend.Capacity);
        Assert.Equal(3, state.Trend.Points.Count);
        Assert.Equal(RuntimeTestFixture.Instant.AddSeconds(3).ToUnixTimeSeconds(), state.Trend.Points[0].T);
        Assert.Equal(RuntimeTestFixture.Instant.AddSeconds(4).ToUnixTimeSeconds(), state.Trend.Points[1].T);
        Assert.Equal(RuntimeTestFixture.Instant.AddSeconds(5).ToUnixTimeSeconds(), state.Trend.Points[2].T);
        Assert.All(state.Trend.Points, point => Assert.Equal(RuntimeLimits.TrendSeriesCount, point.Series.Length));

        // The previously published window is immutable evidence: it was not
        // touched by any later append.
        Assert.Empty(published.Points);
        Assert.Equal(3, published.Capacity);
    }

    [Fact]
    public void Quality_Schedule_Reaches_Normal_Uncertain_Stale_Bad_And_Recovery()
    {
        var rules = new SyntheticEvolutionRules
        {
            UncertainPeriodBase = 3,
            UncertainPeriodSpan = 0,
            StalePeriodBase = 5,
            StalePeriodSpan = 0,
            BadPeriodBase = 7,
            BadPeriodSpan = 0,
        }.Validated();

        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var normal = Advance(state, 1, RuntimeTestFixture.Instant.AddSeconds(1), rules);
        var normalSensor = normal.Sensors[0];
        Assert.Equal(Quality.GOOD, normalSensor.Quality);
        Assert.Equal(ClassificationBasis.CURRENT, normalSensor.ClassificationBasis);
        Assert.Null(normalSensor.QualityReason);
        Assert.NotNull(normalSensor.DirtyScore);
        Assert.Equal(UtcTimestamps.Format(RuntimeTestFixture.Instant.AddSeconds(1)), normalSensor.SourceTimestamp);
        Assert.Equal(normalSensor.DirtyScore, normalSensor.LastValidatedScore);

        // Tick 2 is GOOD as well; tick numbers are consecutive, so every tick is evolved.
        var tick2 = Advance(normal, 2, RuntimeTestFixture.Instant.AddSeconds(2), rules);
        Assert.All(tick2.Sensors, sensor => Assert.Equal(Quality.GOOD, sensor.Quality));

        var uncertain = Advance(tick2, 3, RuntimeTestFixture.Instant.AddSeconds(3), rules);
        var uncertainSensor = uncertain.Sensors[0];
        Assert.Equal(Quality.UNCERTAIN, uncertainSensor.Quality);
        Assert.Equal(RuntimeSyntheticEvolution.UncertainReason, uncertainSensor.QualityReason);
        Assert.Equal(ClassificationBasis.LAST_VALIDATED, uncertainSensor.ClassificationBasis);
        Assert.NotEqual(ClassificationBasis.CURRENT, uncertainSensor.ClassificationBasis);
        Assert.NotNull(uncertainSensor.DirtyScore);
        Assert.Equal(ClassificationFor(uncertainSensor.LastValidatedScore), uncertainSensor.Classification);
        Assert.All(uncertain.Sensors, sensor =>
        {
            Assert.Equal(RuntimeSyntheticEvolution.UncertainReason, sensor.QualityReason);
            Assert.Equal(ClassificationFor(sensor.LastValidatedScore), sensor.Classification);
        });

        var recovered = Advance(uncertain, 4, RuntimeTestFixture.Instant.AddSeconds(4), rules);
        Assert.All(recovered.Sensors, sensor =>
        {
            Assert.Equal(Quality.GOOD, sensor.Quality);
            Assert.Equal(ClassificationBasis.CURRENT, sensor.ClassificationBasis);
            Assert.Null(sensor.QualityReason);
            Assert.Equal(sensor.DirtyScore, sensor.LastValidatedScore);
        });

        var stale = Advance(recovered, 5, RuntimeTestFixture.Instant.AddSeconds(5), rules);
        Assert.All(stale.Sensors, sensor =>
        {
            Assert.Equal(Quality.STALE, sensor.Quality);
            Assert.Equal(RuntimeSyntheticEvolution.StaleReason, sensor.QualityReason);
            Assert.Equal(ClassificationBasis.LAST_VALIDATED, sensor.ClassificationBasis);
            Assert.Null(sensor.DirtyScore);
            Assert.Equal(ClassificationFor(sensor.LastValidatedScore), sensor.Classification);
        });

        Assert.Equal(
            UtcTimestamps.Format(RuntimeTestFixture.Instant.AddSeconds(4)),
            stale.Sensors[0].SourceTimestamp);

        // Tick 6 is UNCERTAIN again, then tick 7 is BAD (its period is 7).
        var tick6 = Advance(stale, 6, RuntimeTestFixture.Instant.AddSeconds(6), rules);
        Assert.All(tick6.Sensors, sensor => Assert.Equal(Quality.UNCERTAIN, sensor.Quality));

        var bad = Advance(tick6, 7, RuntimeTestFixture.Instant.AddSeconds(7), rules);
        Assert.All(bad.Sensors, sensor =>
        {
            Assert.Equal(Quality.BAD, sensor.Quality);
            Assert.Equal(RuntimeSyntheticEvolution.BadReason, sensor.QualityReason);
            Assert.Equal(ClassificationBasis.LAST_VALIDATED, sensor.ClassificationBasis);
            Assert.Null(sensor.DirtyScore);
            Assert.Null(sensor.SourceTimestamp);
        });
    }

    [Fact]
    public void Degraded_Quality_Without_A_Usable_Basis_Reports_No_Basis()
    {
        var rules = new SyntheticEvolutionRules
        {
            UncertainPeriodBase = 2,
            UncertainPeriodSpan = 0,
            StalePeriodBase = 1000,
            StalePeriodSpan = 0,
            BadPeriodBase = 1000,
            BadPeriodSpan = 0,
            LastValidatedRetentionSeconds = 1,
        }.Validated();

        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        // Tick 1 is GOOD and re-establishes the validated basis at +1s.
        var validated = Advance(state, 1, RuntimeTestFixture.Instant.AddSeconds(1), rules);

        // Tick 2 is UNCERTAIN five seconds later: the basis is past its synthetic
        // retention, so the presentation must not claim a basis at all.
        var expired = Advance(validated, 2, RuntimeTestFixture.Instant.AddSeconds(6), rules);
        Assert.All(expired.Sensors, sensor =>
        {
            Assert.Equal(Quality.UNCERTAIN, sensor.Quality);
            Assert.Equal(RuntimeSyntheticEvolution.NoValidatedBasisReason, sensor.QualityReason);
            Assert.Equal(ClassificationBasis.NONE, sensor.ClassificationBasis);
            Assert.Equal(Classification.NOT_CLASSIFIED, sensor.Classification);
        });

        // A Sensor set that never carried a validated value reports the same way.
        var withoutBasis = state with
        {
            Sensors = state.Sensors
                .Select(sensor => sensor with { LastValidatedScore = null, LastValidatedAt = null })
                .ToArray(),
        };

        var never = Advance(
            withoutBasis, 1, RuntimeTestFixture.Instant.AddSeconds(1), rules with { UncertainPeriodBase = 1 });

        Assert.All(never.Sensors, sensor =>
        {
            Assert.Equal(RuntimeSyntheticEvolution.NoValidatedBasisReason, sensor.QualityReason);
            Assert.Equal(ClassificationBasis.NONE, sensor.ClassificationBasis);
            Assert.Equal(Classification.NOT_CLASSIFIED, sensor.Classification);
        });
    }

    private static Classification ClassificationFor(double? lastValidatedScore) =>
        lastValidatedScore is double score && score > RuntimeTestFixture.SyntheticDirtyThreshold
            ? Classification.DIRTY
            : Classification.CLEANER;

    private static RuntimeState Advance(
        RuntimeState state,
        long tick,
        DateTimeOffset instant,
        SyntheticEvolutionRules? rules = null,
        ulong? seed = null)
    {
        var outcome = RuntimeSyntheticEvolution.Tick(
            state, seed ?? Seed, tick, instant, rules ?? RuntimeSyntheticEvolution.DefaultRules);

        Assert.True(outcome.Accepted, outcome.RefusalReason);
        return outcome.Result!.State;
    }
}
