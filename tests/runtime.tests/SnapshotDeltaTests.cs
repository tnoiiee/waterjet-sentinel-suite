using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// <c>wjss.delta/1</c> generation and the Snapshot foundation it continues: the
/// gapless revision chain, the sections a Delta does and does not carry, the
/// three-state Active Job encoding on the wire, Snapshot agreement with the
/// committed revision, and the determinism of the whole serialized sequence.
///
/// Wire assertions read token kinds through <see cref="JsonDocument"/>, never
/// formatted text.
/// </summary>
public sealed class SnapshotDeltaTests
{
    [Fact]
    public void A_Textbook_Tick_Delta_Carries_Only_What_Changed()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
            writer, state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        Assert.Equal(state.Revision, delta.PreviousRevision);
        Assert.Equal(committed.Revision, delta.Revision);
        Assert.Equal(committed.GeneratedAtUtc, delta.GeneratedAtUtc);
        Assert.Equal(DeltaJobEncoding.Absent, delta.ActiveJob.Encoding);
        Assert.NotNull(delta.TrendPoint);
        Assert.Equal(delta.ChangedSensors.Count, delta.PreviousSensors.Count);

        // Every Sensor carries a fresh source timestamp on a GOOD tick, so every
        // Sensor record changed in this textbook step.
        Assert.Equal(CanonicalSensorMap.SensorLocations, delta.ChangedSensors.Count);
        Assert.All(delta.ChangedSensors, sensor =>
            Assert.Equal(UtcTimestamps.Format(committed.GeneratedAtUtc), sensor.SourceTimestamp));

        // Sections the Runtime does not evolve yet stay absent, never fabricated.
        Assert.Null(delta.Pump);
        Assert.Null(delta.Queue);
        Assert.Null(delta.Sequence);
        Assert.Null(delta.Alarms);
        Assert.Null(delta.Communication);
        Assert.False(delta.IsEmpty);

        var wire = RuntimeDeltaProjector.ProjectWire(delta);
        var json = JsonSerializer.Serialize(wire, ContractJson.Options);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(RuntimeDeltaProjector.DeltaKind, root.GetProperty("kind").GetString());
        Assert.Equal(SchemaIds.Delta, root.GetProperty("schema").GetString());
        Assert.Equal(SchemaIds.ApiVersion, root.GetProperty("apiVersion").GetInt32());
        Assert.Equal(state.Revision, root.GetProperty("previousRevision").GetInt32());
        Assert.Equal(committed.Revision, root.GetProperty("revision").GetInt32());
        Assert.Equal(UtcTimestamps.Format(committed.GeneratedAtUtc), root.GetProperty("generatedAt").GetString());
        Assert.Equal(CanonicalSensorMap.SensorLocations, root.GetProperty("sensors").GetArrayLength());
        Assert.False(root.TryGetProperty("config", out _));
        Assert.False(root.TryGetProperty("activeJob", out _));
        Assert.False(root.TryGetProperty("pump", out _));
        Assert.False(root.TryGetProperty("queue", out _));
        Assert.False(root.TryGetProperty("sequence", out _));
        Assert.False(root.TryGetProperty("alarms", out _));
        Assert.False(root.TryGetProperty("communication", out _));
        Assert.False(root.TryGetProperty("runtime", out _));
        Assert.True(root.TryGetProperty("trendPoint", out var trendPoint));
        Assert.Equal(JsonValueKind.Object, trendPoint.ValueKind);
    }

    [Fact]
    public void Delta_Chain_Is_Gapless_And_Matches_The_Committed_Revisions()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;
        var revisions = new List<int>();

        for (var tick = 1; tick <= 3; tick++)
        {
            var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
                writer, state, tick, RuntimeTestFixture.Instant.AddSeconds(tick));

            Assert.Equal(state.Revision, delta.PreviousRevision);
            Assert.Equal(state.Revision + 1, delta.Revision);
            Assert.Equal(committed.Revision, delta.Revision);
            Assert.Equal(RuntimeDeltaProjector.ProjectWire(delta).GeneratedAt, UtcTimestamps.Format(committed.GeneratedAtUtc));

            revisions.Add(delta.Revision);
            state = committed;
        }

        // No duplicate revision, no skipped revision: one accepted tick, one step.
        Assert.Equal(new[] { 2, 3, 4 }, revisions);
        Assert.Equal(new[] { 4, 3, 2, 1 }, store.History.Select(entry => entry.Revision).ToArray());
        Assert.Equal(4, state.Revision);
    }

    [Fact]
    public void Snapshot_Agrees_With_The_Committed_Evolution()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        for (var tick = 1; tick <= 3; tick++)
        {
            (state, _) = RuntimeDeltaTestFixture.CommitTick(
                writer, state, tick, RuntimeTestFixture.Instant.AddSeconds(tick));
        }

        var snapshot = RuntimeSnapshotProjector.Project(state, store.Counters, 0.0);

        Assert.Equal(state.Revision, snapshot.Revision);
        Assert.Equal(state.Sensors, snapshot.Sensors);
        Assert.Equal(state.Walls, snapshot.Walls);
        Assert.Equal(state.Trend.Points, snapshot.Trend.Points);
        Assert.Null(snapshot.ActiveJob);

        var json = JsonSerializer.Serialize(snapshot, ContractJson.Options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(4, root.GetProperty("revision").GetInt32());
        Assert.Equal(UtcTimestamps.Format(RuntimeTestFixture.Instant.AddSeconds(3)), root.GetProperty("generatedAt").GetString());
        Assert.Equal(CanonicalSensorMap.MatrixSlots, root.GetProperty("wallMap").GetArrayLength());
        Assert.Equal(CanonicalSensorMap.SensorLocations, root.GetProperty("sensors").GetArrayLength());
        Assert.Equal(RuntimeWallSummaries.CanonicalWallOrder.Count, root.GetProperty("walls").GetArrayLength());
        Assert.Equal(3, root.GetProperty("trend").GetProperty("points").GetArrayLength());
        Assert.Equal(RuntimeLimits.TrendSeriesCount, root.GetProperty("trend").GetProperty("seriesNames").GetArrayLength());
    }

    [Fact]
    public void The_Serialized_Snapshot_And_Delta_Sequence_Is_Deterministic()
    {
        var first = RunSequence();
        var second = RunSequence();

        Assert.Equal(first, second);
        Assert.Equal(7, first.Count);
    }

    [Fact]
    public void ActiveJob_Uses_The_Three_State_Wire_Encoding()
    {
        var job = RuntimeDeltaTestFixture.SyntheticJob("G+201", UtcTimestamps.Format(RuntimeTestFixture.Instant));

        var absent = ProjectWireJson(DeltaFor(DeltaJobState.Unchanged()));
        using (var document = JsonDocument.Parse(absent))
        {
            Assert.False(document.RootElement.TryGetProperty("activeJob", out _));
        }

        var cleared = ProjectWireJson(DeltaFor(DeltaJobState.Cleared()));
        using (var document = JsonDocument.Parse(cleared))
        {
            Assert.True(document.RootElement.TryGetProperty("activeJob", out var property));
            Assert.Equal(JsonValueKind.Null, property.ValueKind);
        }

        var replaced = ProjectWireJson(DeltaFor(DeltaJobState.Replaced(job)));
        using (var document = JsonDocument.Parse(replaced))
        {
            Assert.True(document.RootElement.TryGetProperty("activeJob", out var property));
            Assert.Equal(JsonValueKind.Object, property.ValueKind);
            Assert.Equal(job.JobId, property.GetProperty("jobId").GetString());
            Assert.Equal(job.TargetSensorId, property.GetProperty("targetSensorId").GetString());
        }

        // Contract round-trip: all three states read back as themselves.
        Assert.True(Deserialize(absent).ActiveJob.IsAbsent);
        Assert.True(Deserialize(cleared).ActiveJob.IsCleared);
        Assert.True(Deserialize(replaced).ActiveJob.IsPresent);
    }

    [Fact]
    public void A_Cleared_Delta_Encodes_The_Clear_Without_Any_Extra_Flag()
    {
        var json = ProjectWireJson(DeltaFor(DeltaJobState.Cleared()));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Exactly one activeJob member exists and it is the explicit null: the
        // clear is carried by the three-state slot itself, never by a second flag.
        var activeJobKeys = root.EnumerateObject().Count(property => property.Name == "activeJob");
        Assert.Equal(1, activeJobKeys);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("activeJob").ValueKind);
    }

    private static RuntimeDelta DeltaFor(DeltaJobState job) => new()
    {
        PreviousRevision = 1,
        Revision = 2,
        GeneratedAtUtc = RuntimeTestFixture.Instant.AddSeconds(1),
        ChangedSensors = Array.Empty<SensorPresentationState>(),
        PreviousSensors = Array.Empty<SensorPresentationState>(),
        ChangedWalls = Array.Empty<WallSummary>(),
        ActiveJob = job,
    };

    private static string ProjectWireJson(RuntimeDelta delta) =>
        JsonSerializer.Serialize(RuntimeDeltaProjector.ProjectWire(delta), ContractJson.Options);

    private static OperationalDelta Deserialize(string json) =>
        JsonSerializer.Deserialize<OperationalDelta>(json, ContractJson.Options)
        ?? throw new InvalidOperationException("The Delta payload did not deserialize.");

    private static List<string> RunSequence()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;
        var sequence = new List<string> { RuntimeTestFixture.SerializeSnapshot(state) };

        for (var tick = 1; tick <= 3; tick++)
        {
            var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
                writer, state, tick, RuntimeTestFixture.Instant.AddSeconds(tick));

            sequence.Add(RuntimeTestFixture.SerializeSnapshot(committed));
            sequence.Add(ProjectWireJson(delta));
            state = committed;
        }

        return sequence;
    }
}
