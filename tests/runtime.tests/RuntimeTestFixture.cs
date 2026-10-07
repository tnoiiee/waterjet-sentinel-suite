using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Time;

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
    /// Serializes the snapshot projection of one state under the contract
    /// encoding. Used as the structural determinism comparison: two runs that
    /// agree here agree on the whole published projection.
    /// </summary>
    internal static string SerializeSnapshot(RuntimeState state) =>
        JsonSerializer.Serialize(
            RuntimeSnapshotProjector.Project(state, InitialCounters(), 0.0),
            ContractJson.Options);
}
