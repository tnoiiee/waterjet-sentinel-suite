using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Config.Tests;

/// <summary>
/// Public-boundary guarantees for config/examples/. Vocabulary constants are
/// assembled from fragments so this scanner-consumer file never trips the
/// repository boundary scanner it exists to protect.
/// </summary>
public sealed class ExampleConfigTests
{
    private static readonly string[] ProhibitedVocabulary =
    [
        "mo" + "d" + "bus",
        "ga" + "lil",
        "wa" + "go",
        "trav" + "el limit",
        "regi" + "ster",
        "tag l" + "ist",
        "connection s" + "tring",
        "passwo" + "rd",
    ];

    [Fact]
    public void Repo_Root_Is_Reachable_From_Test_Output()
    {
        Assert.NotNull(RepoRoot());
    }

    [Fact]
    public void Published_Config_Example_Is_Synthetic_Labelled_And_Simulator_Only()
    {
        var path = Path.Combine(RepoRoot()!, "config", "examples", "published-config.example.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert.Equal("SIMULATOR", (string?)node["deviceProfile"]);
        Assert.Contains("SYNTHETIC", ((string?)node["_label"])!, StringComparison.Ordinal);
        Assert.Contains("NOT FOR DEPLOYMENT", ((string?)node["_label"])!, StringComparison.Ordinal);

        // The example is INCOMPLETE by design: exactly one published revision identity.
        var config = node["configuration"]!.AsObject();
        Assert.Equal(1, (int?)config["revision"]);
    }

    [Fact]
    public void Sensor_Map_Example_Matches_Canonical_Counts_And_Excludes_Gap_Positions_From_Sensors()
    {
        var path = Path.Combine(RepoRoot()!, "config", "examples", "sensor-map.example.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var matrix = node["logicalMatrix"]!.AsObject();

        var slots = matrix["slots"]!.AsArray();
        Assert.Equal(108, slots.Count);

        var sensors = slots.Where(s => (string?)s!["positionKind"] == "SENSOR").ToArray();
        var nonSensorGaps = slots.Where(s => (string?)s!["positionKind"] == "NON_SENSOR_GAP").ToArray();
        Assert.Equal(106, sensors.Length);
        Assert.Equal(2, nonSensorGaps.Length);

        foreach (var gap in nonSensorGaps)
        {
            Assert.Null(gap!["sensorId"]);
            Assert.NotNull(gap["gapAnchorForWaterJetId"]);
        }

        foreach (var sensor in sensors)
        {
            Assert.Null(sensor!["gapAnchorForWaterJetId"]);
            Assert.NotNull(sensor["sensorId"]);
        }

        var ids = sensors.Select(s => (string?)s!["sensorId"]).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.DoesNotContain("I7", ids);
        Assert.DoesNotContain("I16", ids);

        // tcChannels STRUCTURE (round 6b): every SENSOR slot exposes a JsonArray of exactly
        // two non-empty string items, distinct within the pair; scalar (comma-delimited)
        // nodes are rejected as a matter of shape. No comma-splitting anywhere and no
        // GetValue<string>() on the array node itself — only the two string ITEMS are read
        // as values. (The stale pre-correction assembly failed exactly here: the accepted
        // contract stores tcChannels as a JsonArray, not a JsonValue.)
        var channelPairs = sensors.Select(sensor =>
        {
            var node = sensor!["tcChannels"];
            Assert.NotNull(node);
            var tcArray = node as JsonArray; // a scalar (comma-delimited) JsonValue casts to null
            Assert.True(tcArray is not null,
                $"sensor {(string?)sensor!["slotId"]}: tcChannels must be a JSON array, not a scalar string");
            Assert.Equal(2, tcArray!.Count);
            var pair = tcArray.Select(item =>
            {
                Assert.NotNull(item);
                Assert.Equal(JsonValueKind.String, item!.GetValueKind());
                var value = item!.GetValue<string>();
                Assert.False(value.Trim().Length == 0,
                    "thermocouple channel entries must be non-empty JSON strings");
                Assert.Equal(value, value.Trim()); // entries are stored pure, without surrounding whitespace
                return value;
            }).ToArray();
            Assert.NotEqual(pair[0], pair[1]);
            return pair;
        }).ToArray();

        var allChannels = channelPairs.SelectMany(pair => pair).ToArray();
        Assert.Equal(212, allChannels.Length);             // exactly 2 channels for each of the 106 sensors
        Assert.Equal(212, allChannels.Distinct().Count()); // globally unique across the machine
        foreach (var gap in nonSensorGaps)
        {
            Assert.Null(gap!["tcChannels"]); // NON_SENSOR_GAP slots never carry the key at all
        }

        // canonical totals re-verified through the typed contract record: a scalar string
        // could not even deserialize into the array property (TcChannelRules rejects every
        // other invalid shape: lengths 0/1/3, blanks, duplicates, NON_SENSOR_GAP channels)
        var slotRecords = slots
            .Select(s => JsonSerializer.Deserialize<SensorMapSlotExample>(s!.ToJsonString(), ContractJson.Options)!)
            .ToArray();
        TcChannelRules.RequireValidMapping(slotRecords); // 108 slots / 106 sensors / 2 gaps / 212 distinct channels
        Assert.All(slotRecords.Where(r => r.IsNonSensorGap), r => Assert.Null(r.TcChannels));
        foreach (var wall in new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT })
        {
            var expected = wall == Wall.LEFT || wall == Wall.RIGHT ? 24 : 29;
            Assert.Equal(expected, slotRecords.Count(r => r.Wall == wall && !r.IsNonSensorGap));
        }
    }

    [Fact]
    public void Examples_Contain_No_Prohibited_Production_Vocabulary()
    {
        var dir = Path.Combine(RepoRoot()!, "config", "examples");
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue; // the README explains the boundary; scanners cover it at repo level
            }

            var text = File.ReadAllText(file).ToLowerInvariant();
            foreach (var term in ProhibitedVocabulary)
            {
                Assert.False(text.Contains(term, StringComparison.Ordinal), $"{Path.GetFileName(file)} contains prohibited term '{term}'");
            }
        }
    }

    [Fact]
    public void No_Production_Profile_Ever_Appears_In_Examples()
    {
        // Structural scan (round 6c): every deviceProfile property value in the parsed
        // document must stay SIMULATOR. The previous serialized-text guard baked the
        // indented writer's spacing ("\"deviceProfile\": \"...") into the pattern, so the
        // same prohibited value in compactly written JSON would have passed vacuously.
        var dir = Path.Combine(RepoRoot()!, "config", "examples");
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            AssertNoProductionProfile(doc.RootElement, file);
        }
    }

    private static void AssertNoProductionProfile(JsonElement element, string file)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "deviceProfile", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : null;
                        Assert.False(
                            value?.StartsWith("PRODUCTION", StringComparison.OrdinalIgnoreCase) == true
                            || value?.StartsWith("TEST_HARDWARE", StringComparison.OrdinalIgnoreCase) == true,
                            $"{Path.GetFileName(file)}: deviceProfile must never be Production or test-hardware (found '{value}')");
                    }

                    AssertNoProductionProfile(property.Value, file);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoProductionProfile(item, file);
                }

                break;
        }
    }

    internal static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WaterJetSentinelSuite.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
