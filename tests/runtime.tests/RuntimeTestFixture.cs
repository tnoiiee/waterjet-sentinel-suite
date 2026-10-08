using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Deterministic composition shared by the Runtime state tests. Every value here
/// is synthetic; no Production value, threshold, timeout or device identity
/// appears. The fixture builds the same way the composition root will: the
/// SIMULATOR source supplies the wall map and the seeded Sensor projection, and
/// the composer turns them into the initial revision.
/// </summary>
internal static class RuntimeTestFixture
{
    /// <summary>Synthetic Dirty-Score threshold; presentation-only, not a Production threshold.</summary>
    internal const double SyntheticDirtyThreshold = 50.5;

    /// <summary>Synthetic stale threshold; presentation-only, not a Production timeout.</summary>
    internal const int SyntheticStaleThresholdMs = 5000;

    /// <summary>The deterministic test instant (the accepted fixture timestamp).</summary>
    internal static readonly DateTimeOffset Instant = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    internal static PublishedConfigurationRevision Config() => new()
    {
        Revision = 1,
        PublishedAt = UtcTimestamps.Format(Instant),
        Label = "SYNTHETIC TEST CONFIGURATION - NOT A PRODUCTION VALUE",
        DirtyThreshold = SyntheticDirtyThreshold,
        StaleThresholdMs = SyntheticStaleThresholdMs,
    };

    internal static RuntimeState ComposeInitial(SyntheticSeed seed) => ComposeInitial(seed, Instant);

    internal static RuntimeState ComposeInitial(SyntheticSeed seed, DateTimeOffset instant)
    {
        var config = Config();
        return RuntimeStateComposer.ComposeInitial(
            DeviceProfile.SIMULATOR,
            config,
            SyntheticSensorMap.BuildWallMap(),
            SyntheticSensorMap.BuildInitialSensors(config, seed, instant),
            WaterJetTopologyCatalog.WaterJets,
            WaterJetTopologyCatalog.IsolationValves,
            instant);
    }

    internal static RuntimeStateStore CreateStore(
        SyntheticSeed seed,
        int historyCapacity = RuntimeLimits.DefaultRevisionHistoryCapacity) =>
        RuntimeStateStore.Create(ComposeInitial(seed), historyCapacity);

    /// <summary>Counters of a store that has published only its initial revision.</summary>
    internal static RuntimeStoreCounters InitialCounters(int historyCapacity = RuntimeLimits.DefaultRevisionHistoryCapacity) => new()
    {
        CommittedRevisions = 1,
        RefusedCommits = 0,
        RevisionRefusals = 0,
        InvalidStateRefusals = 0,
        HistoryDepth = 1,
        HistoryCapacity = historyCapacity,
    };

    /// <summary>
    /// Structural comparison of two bounded-trend sequences.
    ///
    /// <see cref="TrendPoint"/> carries its fixed-width series as a
    /// <c>double?[]</c>, and the compiler-generated record equality compares that
    /// array by REFERENCE: two independently produced points with identical values
    /// are not equal through <c>Assert.Equal</c>. This helper compares the fields
    /// the contract defines - <c>T</c>, the series length and every element value,
    /// <c>Setpoint</c>, <c>JobActive</c> and <c>AlarmActive</c> - so the assertion
    /// tests the presented values rather than the array identity. The contract type
    /// is deliberately left unchanged; this is a comparison helper only.
    /// </summary>
    internal static void AssertTrendPointsEquivalent(
        IReadOnlyList<TrendPoint> expected,
        IReadOnlyList<TrendPoint> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            var expectedPoint = expected[index];
            var actualPoint = actual[index];

            Assert.Equal(expectedPoint.T, actualPoint.T);
            Assert.Equal(expectedPoint.Series.Length, actualPoint.Series.Length);

            for (var series = 0; series < expectedPoint.Series.Length; series++)
            {
                Assert.Equal(expectedPoint.Series[series], actualPoint.Series[series]);
            }

            Assert.Equal(expectedPoint.Setpoint, actualPoint.Setpoint);
            Assert.Equal(expectedPoint.JobActive, actualPoint.JobActive);
            Assert.Equal(expectedPoint.AlarmActive, actualPoint.AlarmActive);
        }
    }

    /// <summary>
    /// Serializes the snapshot projection of one state under the contract
    /// encoding. Used as the structural determinism comparison: two runs that
    /// agree here agree on the whole published projection.
    /// </summary>
    internal static string SerializeSnapshot(RuntimeState state) =>
        JsonSerializer.Serialize(
            RuntimeSnapshotProjector.Project(state, InitialCounters(), 0.0),
            ContractJson.Options);
}
