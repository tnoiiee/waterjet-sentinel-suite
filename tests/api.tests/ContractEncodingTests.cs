using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Encodes the JSON contract expectations on the .NET side so an Owner-local
/// run proves the serializer behaves exactly as the golden fixtures and the
/// TypeScript mirror assume: camelCase keys, UPPER_SNAKE enum strings, sparse
/// Deltas (absent key = unchanged), explicit nulls in Snapshots.
/// </summary>
public sealed class ContractEncodingTests
{
    private static JsonObject SerializeToObject<T>(T value) =>
        JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value, ContractJson.Options))!;

    [Fact]
    public void Wall_Map_Slot_Serializes_CamelCase_And_Explicit_Nulls()
    {
        var slot = new WallMapSlot
        {
            SlotId = "SLOT-R5-C07",
            SlotType = SlotType.CANNON,
            Wall = Wall.REAR,
            LogicalColumn = 7,
            LogicalRow = 5,
            WallColumn = 3,
            WallRow = 5,
            SensorId = null,
            EquipmentId = "CANNON_REAR",
        };

        var json = SerializeToObject(slot);
        Assert.Equal("SLOT-R5-C07", (string?)json["slotId"]);
        Assert.Equal("CANNON", (string?)json["slotType"]);
        Assert.Equal("REAR", (string?)json["wall"]);
        Assert.True(json.ContainsKey("sensorId"));
        Assert.Equal(JsonValueKind.Null, json["sensorId"]!.GetValueKind());
    }

    [Fact]
    public void Delta_Is_Sparse_Absent_Key_Means_Unchanged()
    {
        var delta = new OperationalDelta
        {
            Kind = "delta",
            Schema = SchemaIds.Delta,
            ApiVersion = SchemaIds.ApiVersion,
            PreviousRevision = 10,
            Revision = 11,
            GeneratedAt = "2026-10-07T00:00:00.000Z",
        };

        var json = SerializeToObject(delta);

        // Envelope keys always present:
        Assert.Equal("delta", (string?)json["kind"]);
        Assert.Equal("wjss.delta/1", (string?)json["schema"]);
        Assert.Equal(1, (int?)json["apiVersion"]);
        Assert.Equal(10, (int?)json["previousRevision"]);
        Assert.Equal(11, (int?)json["revision"]);

        // Everything untouched must be ABSENT (never null, never empty):
        Assert.False(json.ContainsKey("sensors"));
        Assert.False(json.ContainsKey("pump"));
        Assert.False(json.ContainsKey("queue"));
        Assert.False(json.ContainsKey("sequence"));
        Assert.False(json.ContainsKey("activeJob"));
        Assert.False(json.ContainsKey("activeJobCleared"));

        // The wall map is static and Snapshot-only: it may never appear in a Delta key set.
        Assert.False(json.ContainsKey("wallMap"));
    }

    [Fact]
    public void Delta_ActiveJobCleared_Is_The_Only_Clear_Encoding()
    {
        var delta = new OperationalDelta
        {
            Kind = "delta",
            Schema = SchemaIds.Delta,
            ApiVersion = SchemaIds.ApiVersion,
            PreviousRevision = 10,
            Revision = 11,
            GeneratedAt = "2026-10-07T00:00:00.000Z",
            ActiveJobCleared = true,
        };

        var json = SerializeToObject(delta);
        Assert.Equal(true, (bool?)json["activeJobCleared"]);
        Assert.False(json.ContainsKey("activeJob")); // cleared, not replaced
    }

    [Fact]
    public void Snapshot_Keeps_Explicit_Nulls_For_Cleared_Legacy_Encoding()
    {
        // In a Snapshot (global Never-ignore), a null member is written as an
        // explicit null - "no Active Job" must be visible, not absent.
        var payload = new { activeJob = (object?)null };
        var json = JsonSerializer.Serialize(payload, ContractJson.Options);
        Assert.Contains("\"activeJob\": null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Routes_Are_Frozen_For_The_Health_Stub()
    {
        Assert.Equal("/health/live", ApiRoutes.HealthLive);
        Assert.Equal("/health/ready", ApiRoutes.HealthReady);
    }
}
