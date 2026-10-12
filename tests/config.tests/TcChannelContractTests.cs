using System.Globalization;
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
/// the full 108-slot example mapping, and the canonical totals (108 Sensors / 216 TC; Owner
/// critical topology correction — I7 and I16 are normal Sensor slots, no NON_SENSOR_GAP slot).
/// All JSON-shape claims are proven STRUCTURALLY from parsed documents
/// (presence + <see cref="JsonValueKind"/>), never by matching serialized text:
/// whitespace-sensitive Contains checks against indented JSON crossed formatting
/// boundaries and were removed in the Owner round 6c correction.
/// Snapshot/delta presentation keeps its separate tcFrontChannel/tcRearChannel fields
/// (covered by the wall-map/contract encoding tests); these tests cover the slot-array
/// contract only. Compile-only in Arena: Owner-local test run is the arbiter.
/// </summary>
public sealed class TcChannelContractTests
{
    private static SensorMapSlotExample Sensor(string slotId, params string[] channels) => new()
    {
        SlotId = slotId,
        PositionKind = LogicalPositionKind.SENSOR,
        Wall = Wall.LEFT,
        LogicalColumn = 0,
        LogicalRow = 0,
        WallColumn = 0,
        WallRow = 0,
        SensorId = "L-R0-C00",
        TcChannels = channels,
    };

    /// <summary>
    /// A full canonical 108-slot mapping in authoritative sequence order with synthetic,
    /// globally unique channel pairs (two per Sensor; I7 and I16 included once each).
    /// </summary>
    private static List<SensorMapSlotExample> CanonicalSlots()
    {
        var slots = new List<SensorMapSlotExample>(TcChannelRules.SlotCount);
        var index = 0;
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var column = 1; column <= CanonicalSensorMap.LogicalColumnCount; column++)
            {
                var sensorId = CanonicalSensorMap.SensorIdFor(row, column);
                slots.Add(new SensorMapSlotExample
                {
                    SlotId = $"SLOT-R{row}-C{column:D2}",
                    PositionKind = LogicalPositionKind.SENSOR,
                    Wall = CanonicalSensorMap.WallForColumn(column),
                    LogicalColumn = column,
                    LogicalRow = row,
                    WallColumn = column,
                    WallRow = row,
                    SensorId = sensorId,
                    TcChannels = [$"SYN-TC-90:CH{index * 2:D3}", $"SYN-TC-90:CH{index * 2 + 1:D3}"],
                });
                index++;
            }
        }

        return slots;
    }

    private static readonly JsonSerializerOptions Options = ContractJson.Options;

    // CA1861: expectation constants are static readonly fields, not inline array arguments.
    // The corrected Sensor identities sit in row I (logicalRow 5) at logical columns 7 and 16
    // and are compared only against structurally ordered data (logicalRow/logicalColumn).
    private static readonly int[] ExpectedCorrectedLogicalColumns = [7, 16];
    private static readonly string[] ExpectedCorrectedSensorIds = ["I7", "I16"];

    // (A) Serialization of a valid mapping is proven from the PARSED document: the
    // tcChannels property exists, is an array (never the scalar kind), carries exactly
    // two non-empty, whitespace-pure, distinct string items in deterministic order.
    [Fact]
    public void Serializing_Valid_Mapping_Emits_Two_Item_Arrays_And_Never_A_Comma_Scalar()
    {
        var slot = Sensor("L-R0-C00", "SYN-TC-01:CH00", "SYN-TC-01:CH01");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(slot, Options));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("tcChannels", out var tc),
            "serialized sensor slot must carry tcChannels");
        Assert.NotEqual(JsonValueKind.String, tc.ValueKind); // never a comma-delimited scalar
        Assert.Equal(JsonValueKind.Array, tc.ValueKind);      // the accepted shape
        Assert.Equal(2, tc.GetArrayLength());
        Assert.Equal(JsonValueKind.String, tc[0].ValueKind);
        Assert.Equal(JsonValueKind.String, tc[1].ValueKind);
        var first = tc[0].GetString()!;
        var second = tc[1].GetString()!;
        Assert.False(first.Trim().Length == 0);
        Assert.False(second.Trim().Length == 0);
        Assert.Equal(first, first.Trim());   // stored pure: no surrounding whitespace
        Assert.Equal(second, second.Trim());
        Assert.NotEqual(first, second);      // distinct within the pair
        Assert.Equal("SYN-TC-01:CH00", first);  // deterministic order retained: front...
        Assert.Equal("SYN-TC-01:CH01", second); // ...then rear

        // The superseded gap vocabulary is not emitted by any slot: no gapAnchorForWaterJetId
        // key exists on the contract record at all.
        Assert.False(root.TryGetProperty("gapAnchorForWaterJetId", out _));
        Assert.DoesNotContain(typeof(SensorMapSlotExample).GetProperties(), p => p.Name == "GapAnchorForWaterJetId");
    }

    // (B) Deserialization: a two-string array binds; invalid shapes are rejected.
    [Fact]
    public void Deserializing_Two_String_Array_Binds_Into_The_Contract_Record()
    {
        const string slotJson = """
            {"slotId":"L-R0-C00","positionKind":"SENSOR","wall":"LEFT","logicalColumn":0,"logicalRow":0,
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
            {"slotId":"L-R0-C00","positionKind":"SENSOR","wall":"LEFT","logicalColumn":0,"logicalRow":0,
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
    public void Canonical_Mapping_Is_Accepted_And_Cross_Sensor_Duplicates_Are_Rejected()
    {
        // the canonical 108-slot mapping (I7 and I16 included as ordinary Sensors) passes
        TcChannelRules.RequireValidMapping(CanonicalSlots());

        // same channel claimed by two sensors
        var slots = CanonicalSlots();
        var i6 = slots.Single(s => s.SensorId == "I6");
        var i7 = slots.Single(s => s.SensorId == "I7");
        slots[slots.IndexOf(i7)] = i7 with { TcChannels = [i7.TcChannels![0], i6.TcChannels![1]] };
        var duplicate = Assert.Throws<ArgumentException>(() => TcChannelRules.RequireValidMapping(slots));
        Assert.Contains("assigned to more than one sensor slot", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mapping_Without_I7_Or_I16_As_A_Sensor_Is_Rejected()
    {
        // Owner critical topology correction: a slot without a Sensor identity (the former
        // "gap" shape) is refused — I7 and I16 are Product Sensors and must carry channels.
        foreach (var correctedId in ExpectedCorrectedSensorIds)
        {
            var slots = CanonicalSlots();
            var slot = slots.Single(s => s.SensorId == correctedId);
            slots[slots.IndexOf(slot)] = slot with { SensorId = null, TcChannels = null };
            var refusal = Assert.Throws<ArgumentException>(() => TcChannelRules.RequireValidMapping(slots));
            Assert.Contains("must be a SENSOR slot with a Sensor identity", refusal.Message, StringComparison.Ordinal);
        }

        // A mapping that renames I16 away from the authoritative sequence fails the central
        // topology invariants even when every slot is a well-formed Sensor slot.
        var renamed = CanonicalSlots();
        var i16 = renamed.Single(s => s.SensorId == "I16");
        renamed[renamed.IndexOf(i16)] = i16 with { SensorId = "I16X" };
        var invariant = Assert.Throws<ArgumentException>(() => TcChannelRules.RequireValidMapping(renamed));
        Assert.Contains(TopologyInvariants.ViolationMessagePrefix, invariant.Message, StringComparison.Ordinal);
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
        Assert.Equal(TcChannelRules.SensorSlotCount, records.Count(r => r.PositionKind == LogicalPositionKind.SENSOR)); // 108
        Assert.Equal(
            TcChannelRules.TotalChannelCount,
            records.Sum(r => r.TcChannels!.Count)); // 216

        // structural pass over the committed example document (round 6c): the shape
        // claims below read parsed JsonElement kinds only - no serialized-text matching
        using var example = JsonDocument.Parse(ExampleSensorMapJson());
        var slotElements = example.RootElement
            .GetProperty("logicalMatrix")
            .GetProperty("slots")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(108, slotElements.Length);

        // no sensor anywhere carries the scalar-string shape
        Assert.Equal(0, slotElements.Count(s =>
            s.TryGetProperty("tcChannels", out var t) && t.ValueKind == JsonValueKind.String));

        var allChannels = new List<string>();
        var distinctChannels = new HashSet<string>(StringComparer.Ordinal);
        var sensorsByWall = new Dictionary<string, int>(StringComparer.Ordinal);
        var sensorSlots = 0;
        var sensorArrayShapes = 0;
        foreach (var element in slotElements)
        {
            Assert.Equal("SENSOR", element.GetProperty("positionKind").GetString());
            Assert.False(element.TryGetProperty("gapAnchorForWaterJetId", out _));
            var hasChannels = element.TryGetProperty("tcChannels", out var tc);

            sensorSlots++;
            var slotId = element.GetProperty("slotId").GetString()!;
            Assert.True(hasChannels, $"sensor {slotId}: tcChannels must be present");
            Assert.Equal(JsonValueKind.Array, tc.ValueKind); // presence + kind: scalar/number/null all fail here
            sensorArrayShapes++;
            Assert.Equal(2, tc.GetArrayLength());
            Assert.Equal(JsonValueKind.String, tc[0].ValueKind);
            Assert.Equal(JsonValueKind.String, tc[1].ValueKind);
            var pairFirst = tc[0].GetString()!;
            var pairSecond = tc[1].GetString()!;
            Assert.False(pairFirst.Trim().Length == 0, $"sensor {slotId}: empty channel entry");
            Assert.False(pairSecond.Trim().Length == 0, $"sensor {slotId}: empty channel entry");
            Assert.Equal(pairFirst, pairFirst.Trim());
            Assert.Equal(pairSecond, pairSecond.Trim());
            Assert.NotEqual(pairFirst, pairSecond); // distinct within the pair
            AssertPairOrderedFrontFirst(tc, slotId); // deterministic order: lower index first
            allChannels.Add(pairFirst);
            allChannels.Add(pairSecond);
            Assert.True(distinctChannels.Add(pairFirst), $"sensor {slotId}: duplicate channel {pairFirst}");
            Assert.True(distinctChannels.Add(pairSecond), $"sensor {slotId}: duplicate channel {pairSecond}");
            var wall = element.GetProperty("wall").GetString()!;
            sensorsByWall[wall] = sensorsByWall.GetValueOrDefault(wall) + 1;
        }

        Assert.Equal(108, sensorSlots);
        Assert.Equal(108, sensorArrayShapes); // every sensor array-shaped; 0 scalar (asserted above)
        Assert.Equal(216, allChannels.Count);            // total channel strings
        Assert.Equal(216, distinctChannels.Count);       // globally unique; 0 duplicates
        Assert.Equal(24, sensorsByWall["LEFT"]);
        Assert.Equal(30, sensorsByWall["REAR"]);
        Assert.Equal(24, sensorsByWall["RIGHT"]);
        Assert.Equal(30, sensorsByWall["FRONT"]);

        // Owner critical topology correction: I7 and I16 are ordinary Sensor slots in row I
        // (logicalRow 5) at logical columns 7 and 16, each with a sensorId and two channels.
        // Ordering is by STRUCTURED position (logicalRow then logicalColumn), never by a
        // lexicographic label sort (which would yield I16 before I7).
        var corrected = records
            .Where(r => r.SensorId is "I7" or "I16")
            .OrderBy(c => c.LogicalRow)
            .ThenBy(c => c.LogicalColumn)
            .ToArray();
        Assert.Equal(2, corrected.Length);
        Assert.Equal(ExpectedCorrectedLogicalColumns, corrected.Select(c => c.LogicalColumn));
        Assert.Equal(ExpectedCorrectedSensorIds, corrected.Select(c => c.SensorId!));
        Assert.All(corrected, c =>
        {
            Assert.Equal(LogicalPositionKind.SENSOR, c.PositionKind);
            Assert.Equal(5, c.LogicalRow);
            Assert.NotNull(c.TcChannels);
            Assert.Equal(2, c.TcChannels!.Count);
            Assert.Null(c.LogicalLabel);
        });
        Assert.Equal(Wall.REAR, corrected[0].Wall);
        Assert.Equal(Wall.FRONT, corrected[1].Wall);

        // No sensor slot lacks the array or has the wrong length; and no sensor carries a
        // logicalLabel at all (the generator no longer has a branch that assigns one).
        Assert.All(records, r =>
        {
            Assert.Equal(LogicalPositionKind.SENSOR, r.PositionKind);
            Assert.NotNull(r.SensorId);
            Assert.NotNull(r.TcChannels);
            Assert.Equal(2, r.TcChannels!.Count);
            Assert.Null(r.LogicalLabel);
        });
    }

    /// <summary>
    /// Deterministic channel order is a contract property of the channel VALUES
    /// (front = lower channel index first, rear second) - asserted on parsed string
    /// values, not on serialized text. Applies whenever both entries carry the
    /// synthetic ":CH&lt;digits&gt;" suffix the generator emits.
    /// </summary>
    private static void AssertPairOrderedFrontFirst(JsonElement tcArray, string slotId)
    {
        var first = tcArray[0].GetString()!;
        var second = tcArray[1].GetString()!;
        if (TryChannelIndex(first, out var firstIndex) && TryChannelIndex(second, out var secondIndex))
        {
            Assert.True(firstIndex < secondIndex,
                $"sensor {slotId}: tcChannels must be ordered front ({first}) before rear ({second})");
        }
    }

    private static bool TryChannelIndex(string channel, out int index)
    {
        index = 0;
        var marker = channel.LastIndexOf(":CH", StringComparison.Ordinal);
        if (marker < 0)
        {
            return false;
        }
        var tail = channel.AsSpan(marker + ":CH".Length);
        return tail.Length > 0
            && int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static string ExampleSensorMapJson()
    {
        var path = Path.Combine(ConfigTestPaths.RepoRoot(), "config", "examples", "sensor-map.example.json");
        Assert.True(File.Exists(path), $"missing example file: {path}");
        return File.ReadAllText(path);
    }
}
