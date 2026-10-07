using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Snapshot projection contract: envelope identity, explicit-null Active Job in
/// a Snapshot, agreement between the projected wall map and the Sensor
/// projections, and the unwired diagnostics blocks. Wire assertions read token
/// kinds through <see cref="JsonDocument"/>, never formatted text.
/// </summary>
public sealed class RuntimeSnapshotProjectionTests
{
    [Fact]
    public void Snapshot_Envelope_Matches_The_Contract_Identity()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var snapshot = RuntimeSnapshotProjector.Project(store.Current, store.Counters, 42.0);

        Assert.Equal(RuntimeSnapshotProjector.SnapshotKind, snapshot.Kind);
        Assert.Equal(SchemaIds.Snapshot, snapshot.Schema);
        Assert.Equal(SchemaIds.ApiVersion, snapshot.ApiVersion);
        Assert.Equal(store.CurrentRevision, snapshot.Revision);
        Assert.Equal(UtcTimestamps.Format(RuntimeTestFixture.Instant), snapshot.GeneratedAt);
        var slotCount = snapshot.WallMap.Count;
        var sensorCount = snapshot.Sensors.Count;
        var wallSummaryCount = snapshot.Walls.Count;

        Assert.Equal(DeviceProfile.SIMULATOR, snapshot.DeviceProfile);
        Assert.Equal(CanonicalSensorMap.MatrixSlots, slotCount);
        Assert.Equal(CanonicalSensorMap.SensorLocations, sensorCount);
        Assert.Equal(RuntimeWallSummaries.CanonicalWallOrder.Count, wallSummaryCount);

        Assert.Null(snapshot.ActiveJob);
        Assert.Equal(RuntimeStage.Marker, snapshot.Runtime.StageMarker);
        Assert.Equal(store.CurrentRevision, snapshot.Runtime.CurrentRevision);
        Assert.Equal(42.0, snapshot.Runtime.UptimeSeconds);
        Assert.Equal(0, snapshot.Runtime.SseClients);
        Assert.Equal(0, snapshot.Runtime.InvariantViolations);
        Assert.Equal(0, snapshot.Runtime.AcceptedSecondJobs);
        Assert.Equal(0, snapshot.Runtime.RefusedSecondJobs);
        Assert.False(snapshot.Runtime.Historian.Wired);
        Assert.Equal(store.Current.Trend.Capacity, snapshot.Trend.Capacity);
    }

    [Fact]
    public void Snapshot_Serializes_Under_The_Contract_Encoding()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var snapshot = RuntimeSnapshotProjector.Project(store.Current, store.Counters, 0.0);
        var json = JsonSerializer.Serialize(snapshot, ContractJson.Options);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("snapshot", root.GetProperty("kind").GetString());
        Assert.Equal(SchemaIds.Snapshot, root.GetProperty("schema").GetString());
        Assert.Equal(SchemaIds.ApiVersion, root.GetProperty("apiVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("revision").GetInt32());
        Assert.Equal("SIMULATOR", root.GetProperty("deviceProfile").GetString());
        Assert.Equal(UtcTimestamps.Format(RuntimeTestFixture.Instant), root.GetProperty("generatedAt").GetString());
        Assert.Equal(CanonicalSensorMap.MatrixSlots, root.GetProperty("wallMap").GetArrayLength());
        Assert.Equal(CanonicalSensorMap.SensorLocations, root.GetProperty("sensors").GetArrayLength());
        Assert.Equal(RuntimeWallSummaries.CanonicalWallOrder.Count, root.GetProperty("walls").GetArrayLength());

        // The three-state Active Job encoding belongs to the Delta contract; a
        // Snapshot always carries the key, and "no Active Job" is an explicit null.
        var activeJob = root.GetProperty("activeJob");
        Assert.Equal(JsonValueKind.Null, activeJob.ValueKind);

        var runtime = root.GetProperty("runtime");
        Assert.Equal(RuntimeStage.Marker, runtime.GetProperty("stageMarker").GetString());
        Assert.Equal(1, runtime.GetProperty("currentRevision").GetInt32());
        Assert.Equal(JsonValueKind.False, runtime.GetProperty("historian").GetProperty("wired").ValueKind);
        Assert.Equal(0, root.GetProperty("queue").GetProperty("totalQueued").GetInt32());
        Assert.Equal(QueueSummary.MaxEntries, root.GetProperty("queue").GetProperty("capacity").GetInt32());
        Assert.Equal(RuntimeLimits.TrendSeriesCount, root.GetProperty("trend").GetProperty("seriesNames").GetArrayLength());
    }

    [Fact]
    public void Snapshot_Wall_Map_And_Sensor_Projections_Agree()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var snapshot = RuntimeSnapshotProjector.Project(store.Current, store.Counters, 0.0);

        foreach (var sensor in snapshot.Sensors)
        {
            var slot = snapshot.WallMap.Single(candidate =>
                candidate.LogicalRow == sensor.LogicalRow && candidate.LogicalColumn == sensor.LogicalColumn);
            Assert.Equal(SlotType.SENSOR, slot.SlotType);
            Assert.Equal(sensor.SensorId, slot.SensorId);
            Assert.Equal(sensor.Wall, slot.Wall);
            Assert.Equal(sensor.WallColumn, slot.WallColumn);
        }

        var sensorSlots = snapshot.WallMap.Where(slot => slot.SlotType == SlotType.SENSOR).ToArray();
        Assert.Equal(snapshot.Sensors.Count, sensorSlots.Length);
        Assert.DoesNotContain(sensorSlots, slot => slot.EquipmentId is not null);
        Assert.DoesNotContain(snapshot.WallMap, slot => slot.SlotType == SlotType.CANNON && slot.SensorId is not null);
    }

    [Fact]
    public void Snapshot_Reports_Invariant_Refusals_And_Not_Revision_Refusals()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var before = store.Current;

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = writer.Commit(before with { Revision = 5 });
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = writer.Commit(before with
            {
                Revision = 2,
                Sensors = before.Sensors.Take(before.Sensors.Count - 1).ToArray(),
            });
        });

        var counters = store.Counters;
        Assert.Equal(2, counters.RefusedCommits);
        Assert.Equal(1, counters.RevisionRefusals);
        Assert.Equal(1, counters.InvalidStateRefusals);

        var snapshot = RuntimeSnapshotProjector.Project(store.Current, counters, 0.0);
        Assert.Equal(1, snapshot.Runtime.InvariantViolations);
        Assert.Equal(1, snapshot.Revision);
    }
}
