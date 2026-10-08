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
/// the full 108-slot example mapping, and the canonical totals (106 sensors / 212 TC).
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

    private static SensorMapSlotExample Gap(string slotId) => new()
    {
        SlotId = slotId,
        PositionKind = LogicalPositionKind.NON_SENSOR_GAP,
        Wall = Wall.REAR,
        LogicalColumn = 7,
        LogicalRow = 5,
        WallColumn = 7,
        WallRow = 5,
        GapAnchorForWaterJetId = "WJ3",
        LogicalLabel = "I7",
    };

    private static readonly JsonSerializerOptions Options = ContractJson.Options;

    // CA1861 correction (Owner round 6): the gap expectation constants are static
    // readonly fields, not inline array arguments. Assert.Equal only enumerates them,
    // so sharing one immutable instance is safe. The sequence IS the accepted logical
    // order (I7 then I16) and is compared only against structurally ordered data
    // (logicalRow/logicalColumn) — never against a lexicographic label sort (round 6d).
    private static readonly int[] ExpectedGapLogicalColumns = [7, 16];
    private static readonly string[] ExpectedGapLogicalLabels = ["I7", "I16"];

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

        var gapJson = JsonSerializer.Serialize(Gap("SLOT-R5-C07"), Options);
        using var gapDoc = JsonDocument.Parse(gapJson);
        Assert.False(gapDoc.RootElement.TryGetProperty("tcChannels", out _));
        // absent on NON_SENSOR_GAP slots: not an empty array, not an explicit null token
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
    public void Cross_Sensor_Duplicates_And_Gap_Channels_Are_Rejected()
    {
        // same channel claimed by two sensors
        var a = Sensor("A", "SYN-TC-01:CH00", "SYN-TC-01:CH01");
        var b = Sensor("B", "SYN-TC-01:CH01", "SYN-TC-01:CH02");
        var slots = new List<SensorMapSlotExample> { a, b };
        for (var i = 0; i < TcChannelRules.SensorSlotCount - 2; i++)
        {
            slots.Add(Sensor($"PAD-{i:D3}", $"SYN-TC-90:CH{i * 2:D2}", $"SYN-TC-90:CH{i * 2 + 1:D2}"));
        }
        slots.Add(Gap("SLOT-R5-C07"));
        slots.Add(Gap("SLOT-R5-C16") with { LogicalColumn = 16, WallColumn = 16, LogicalLabel = "I16", GapAnchorForWaterJetId = "WJ1" });
        Assert.Throws<ArgumentException>(() => TcChannelRules.RequireValidMapping(slots));

        // a NON_SENSOR_GAP slot carrying channels is rejected without counting sensors
        var badGap = Gap("SLOT-R5-C16") with { TcChannels = new[] { "SYN-TC-01:CH13", "SYN-TC-01:CH14" } };
        Assert.Throws<ArgumentException>(() =>
            TcChannelRules.RequireNonSensorGapCarriesNoChannels(badGap.TcChannels, badGap.SlotId));
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
        Assert.Equal(TcChannelRules.SensorSlotCount, records.Count(r => !r.IsNonSensorGap)); // 106
        Assert.Equal(TcChannelRules.NonSensorGapSlotCount, records.Count(r => r.IsNonSensorGap));  // 2
        Assert.Equal(
            TcChannelRules.TotalChannelCount,
            records.Where(r => !r.IsNonSensorGap).Sum(r => r.TcChannels!.Count)); // 212

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
        var nonSensorGapSlots = 0;
        var sensorArrayShapes = 0;
        foreach (var element in slotElements)
        {
            var isGap = string.Equals(element.GetProperty("positionKind").GetString(), "NON_SENSOR_GAP", StringComparison.Ordinal);
            var hasChannels = element.TryGetProperty("tcChannels", out var tc);
            if (isGap)
            {
                nonSensorGapSlots++;
                Assert.False(hasChannels,
                    $"NON_SENSOR_GAP {element.GetProperty("slotId").GetString()}: tcChannels must be absent");
                continue;
            }

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

        Assert.Equal(106, sensorSlots);
        Assert.Equal(2, nonSensorGapSlots);
        Assert.Equal(106, sensorArrayShapes); // every sensor array-shaped; 0 scalar (asserted above)
        Assert.Equal(212, allChannels.Count);            // total channel strings
        Assert.Equal(212, distinctChannels.Count);       // globally unique; 0 duplicates
        Assert.Equal(24, sensorsByWall["LEFT"]);
        Assert.Equal(29, sensorsByWall["REAR"]);
        Assert.Equal(24, sensorsByWall["RIGHT"]);
        Assert.Equal(29, sensorsByWall["FRONT"]);

        // exactly the canonical two NON_SENSOR_GAP slots (row 5, logical columns 7 and 16),
        // each without channels and without a sensorId; I7/I16 are their logical labels
        var gaps = records.Where(r => r.IsNonSensorGap).ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, c =>
        {
            Assert.Equal(5, c.LogicalRow);
            Assert.Null(c.TcChannels);
            Assert.Null(c.SensorId);
            Assert.NotNull(c.GapAnchorForWaterJetId);
            Assert.NotNull(c.LogicalLabel);
        });

        // Round 6d: logical order (I7, then I16) is ordered by STRUCTURED position —
        // logicalRow then logicalColumn. The removed defect was lexicographic label
        // sorting, which yields I16 before I7. No numbers are parsed out of labels and
        // no expectation was re-sorted to match text ordering.
        var orderedGaps = gaps
            .OrderBy(c => c.LogicalRow)
            .ThenBy(c => c.LogicalColumn)
            .ToArray();
        Assert.Equal(ExpectedGapLogicalColumns, orderedGaps.Select(c => c.LogicalColumn));
        Assert.Equal(ExpectedGapLogicalLabels, orderedGaps.Select(c => c.LogicalLabel));

        // explicit position-to-gap-anchor pairing (row I / columns 7 and 16), asserted per
        // slot so the mapping cannot pass through an ordering accident:
        Assert.Equal("I7", orderedGaps[0].LogicalLabel);
        Assert.Equal(5, orderedGaps[0].LogicalRow);
        Assert.Equal(7, orderedGaps[0].LogicalColumn);
        Assert.Equal("WJ3", orderedGaps[0].GapAnchorForWaterJetId);
        Assert.Equal("I16", orderedGaps[1].LogicalLabel);
        Assert.Equal(5, orderedGaps[1].LogicalRow);
        Assert.Equal(16, orderedGaps[1].LogicalColumn);
        Assert.Equal("WJ1", orderedGaps[1].GapAnchorForWaterJetId);

        // No sensor slot lacks the array or has the wrong length; and no sensor carries a
        // logicalLabel at all. Round 6d hotfix: the previous line was
        // Assert.False(r.LogicalLabel?.Contains("CANNON", ...)), a bool? that is null for
        // every sensor (xunit's Assert.False(bool?) rejects null rather than coercing it).
        // The generator assigns logicalLabel ONLY on the NON_SENSOR_GAP branch, so the actual
        // Sensor contract is label ABSENCE - asserted directly, not coerced to false.
        // Any label on a sensor (gap disguise included) now fails here.
        Assert.All(records.Where(r => !r.IsNonSensorGap), r =>
        {
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
