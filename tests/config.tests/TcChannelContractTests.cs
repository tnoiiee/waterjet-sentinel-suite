using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Config.Examples.Tests; // ConfigTestPaths — shared repository-root discovery (round 4 anchor)
using Wjss.Contracts;
using Xunit;

namespace Wjss.Config.Tests;

/// <summary>
/// Stage 0.3A-1 correction: <c>tcChannels</c> in sensor-map examples is a structured
/// JSON array of exactly two thermocouple channel strings — never a comma-delimited
/// scalar. Covers serialization, deserialization, rejection of earlier/invalid shapes,
/// the full 108-slot example mapping, and the canonical totals (106 sensors / 212 TC).
/// Snapshot/delta presentation keeps its separate tcFrontChannel/tcRearChannel fields
/// (covered by the wall-map/contract encoding tests); these tests cover the slot-array
/// contract only. Compile-only in Arena: Owner-local test run is the arbiter.
/// </summary>
public sealed class TcChannelContractTests
{
    private static SensorMapSlotExample Sensor(string slotId, params string[] channels) => new()
    {
        SlotId = slotId,
        SlotType = SlotType.SENSOR,
        Wall = Wall.LEFT,
        LogicalColumn = 0,
        LogicalRow = 0,
        WallColumn = 0,
        WallRow = 0,
        SensorId = "L-R0-C00",
        TcChannels = channels,
    };

    private static SensorMapSlotExample Cannon(string slotId) => new()
    {
        SlotId = slotId,
        SlotType = SlotType.CANNON,
        Wall = Wall.REAR,
        LogicalColumn = 7,
        LogicalRow = 5,
        WallColumn = 7,
        WallRow = 5,
        EquipmentId = "CANNON_REAR",
        LogicalLabel = "I7",
    };

    private static readonly JsonSerializerOptions Options = ContractJson.Options;

    // (A) Serialization of a valid mapping produces exactly two channel array tokens per
    // sensor and never a comma-delimited scalar.
    [Fact]
    public void Serializing_Valid_Mapping_Emits_Two_Item_Arrays_And_Never_A_Comma_Scalar()
    {
        var slot = Sensor("L-R0-C00", "SYN-TC-01:CH00", "SYN-TC-01:CH01");
        var json = JsonSerializer.Serialize(slot, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("tcChannels", out var tc));
        Assert.Equal(JsonValueKind.Array, tc.ValueKind);
        Assert.Equal(2, tc.GetArrayLength());
        Assert.Equal("SYN-TC-01:CH00", tc[0].GetString());
        Assert.Equal("SYN-TC-01:CH01", tc[1].GetString());
        Assert.Contains("\"tcChannels\": [", json); // array token under the indented contract writer...
        Assert.DoesNotContain("\"tcChannels\": \"", json); // ...and never a quoted scalar

        var cannonJson = JsonSerializer.Serialize(Cannon("CANNON_REAR"), Options);
        using var cannonDoc = JsonDocument.Parse(cannonJson);
        Assert.False(cannonDoc.RootElement.TryGetProperty("tcChannels", out _));
    }

    // (B) Deserialization: a two-string array binds; invalid shapes are rejected.
    [Fact]
    public void Deserializing_Two_String_Array_Binds_Into_The_Contract_Record()
    {
        const string slotJson = """
            {"slotId":"L-R0-C00","slotType":"SENSOR","wall":"LEFT","logicalColumn":0,"logicalRow":0,
             "wallColumn":0,"wallRow":0,"sensorId":"L-R0-C00",
             "tcChannels":["SYN-TC-01:CH00","SYN-TC-01:CH01"]}
            """;
        var slot = JsonSerializer.Deserialize<SensorMapSlotExample>(slotJson, Options)!;
        Assert.Equal(2, slot.TcChannels!.Count);
        TcChannelRules.RequireValidSensorChannels(slot.TcChannels, slot.SlotId); // accepted
    }

    [Fact]
    public void Deserializing_Comma_Delimited_String_Is_Rejected_Outright()
    {
        // The earlier encoding shape: a scalar string in place of the array. The contract
        // does not "support then normalize" it — System.Text.Json refuses to bind it.
        const string legacyJson = """
            {"slotId":"L-R0-C00","slotType":"SENSOR","wall":"LEFT","logicalColumn":0,"logicalRow":0,
             "wallColumn":0,"wallRow":0,"sensorId":"L-R0-C00","tcChannels":"SYN-TC-01:CH00,SYN-TC-01:CH01"}
            """;
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<SensorMapSlotExample>(legacyJson, Options));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""["SYN-TC-01:CH00"]""")]
    [InlineData("""["SYN-TC-01:CH00","SYN-TC-01:CH01","SYN-TC-01:CH02"]""")]
    [InlineData("""["SYN-TC-01:CH00","SYN-TC-01:CH00"]""")]   // duplicate pair
    [InlineData("""["","SYN-TC-01:CH01"]""")]                   // empty string
    [InlineData("""["   ","SYN-TC-01:CH01"]""")]                // blank string
    public void Invalid_Channel_Arrays_Are_Rejected_By_The_Rules(string channelsJson)
    {
        var channels = JsonSerializer.Deserialize<string[]>(channelsJson, Options)!;
        Assert.Throws<ArgumentException>(() =>
            TcChannelRules.RequireValidSensorChannels(channels, "L-R0-C00"));
    }

    [Fact]
    public void Cross_Sensor_Duplicates_And_Cannon_Channels_Are_Rejected()
    {
        // same channel claimed by two sensors
        var a = Sensor("A", "SYN-TC-01:CH00", "SYN-TC-01:CH01");
        var b = Sensor("B", "SYN-TC-01:CH01", "SYN-TC-01:CH02");
        var slots = new List<SensorMapSlotExample> { a, b };
        for (var i = 0; i < TcChannelRules.SensorSlotCount - 2; i++)
        {
            slots.Add(Sensor($"PAD-{i:D3}", $"SYN-TC-90:CH{i * 2:D2}", $"SYN-TC-90:CH{i * 2 + 1:D2}"));
        }
        slots.Add(Cannon("CANNON_REAR"));
        slots.Add(Cannon("CANNON_FRONT") with { LogicalColumn = 16, WallColumn = 16, LogicalLabel = "I16", EquipmentId = "CANNON_FRONT" });
        Assert.Throws<ArgumentException>(() => TcChannelRules.RequireValidMapping(slots));

        // a cannon carrying channels is rejected without counting sensors
        var badCannon = Cannon("CANNON_FRONT") with { TcChannels = new[] { "SYN-TC-01:CH13", "SYN-TC-01:CH14" } };
        Assert.Throws<ArgumentException>(() =>
            TcChannelRules.RequireCannonCarriesNoChannels(badCannon.TcChannels, badCannon.SlotId));
    }

    // (C) The generated example itself, through the typed contract.
    [Fact]
    public void Example_Mapping_Validates_Through_The_Typed_Contract()
    {
        var slots = JsonNode.Parse(ExampleSensorMapJson())!["logicalMatrix"]!["slots"]!.AsArray();
        var records = slots
            .Select(s => JsonSerializer.Deserialize<SensorMapSlotExample>(s!.ToJsonString(), Options)!)
            .ToArray();

        TcChannelRules.RequireValidMapping(records);

        Assert.Equal(TcChannelRules.SlotCount, records.Length);       // 108
        Assert.Equal(TcChannelRules.SensorSlotCount, records.Count(r => !r.IsCannon)); // 106
        Assert.Equal(TcChannelRules.CannonSlotCount, records.Count(r => r.IsCannon));  // 2
        Assert.Equal(
            TcChannelRules.TotalChannelCount,
            records.Where(r => !r.IsCannon).Sum(r => r.TcChannels!.Count)); // 212

        // raw file text: every tcChannels token is an array, never a scalar
        var raw = ExampleSensorMapJson();
        Assert.Contains("\"tcChannels\": [", raw);
        Assert.DoesNotContain("\"tcChannels\": \"", raw);

        // exactly the canonical two cannon slots (row 5, logical columns 7 and 16),
        // each without channels; I7/I16 are their logical labels
        var cannons = records.Where(r => r.IsCannon).ToArray();
        Assert.Equal(2, cannons.Length);
        Assert.All(cannons, c =>
        {
            Assert.Equal(5, c.LogicalRow);
            Assert.Null(c.TcChannels);
            Assert.NotNull(c.EquipmentId);
            Assert.NotNull(c.LogicalLabel);
        });
        Assert.Equal(new[] { 7, 16 }, cannons.Select(c => c.LogicalColumn).OrderBy(x => x));
        Assert.Equal(new[] { "I7", "I16" }, cannons.Select(c => c.LogicalLabel).OrderBy(x => x!));

        // no sensor slot lacks the array or has the wrong length; no cannon-as-sensor
        Assert.All(records.Where(r => !r.IsCannon), r =>
        {
            Assert.NotNull(r.TcChannels);
            Assert.Equal(2, r.TcChannels!.Count);
            Assert.False(r.LogicalLabel?.Contains("CANNON", StringComparison.Ordinal));
        });
    }

    private static string ExampleSensorMapJson()
    {
        var path = Path.Combine(ConfigTestPaths.RepoRoot(), "config", "examples", "sensor-map.example.json");
        Assert.True(File.Exists(path), $"missing example file: {path}");
        return File.ReadAllText(path);
    }
}
