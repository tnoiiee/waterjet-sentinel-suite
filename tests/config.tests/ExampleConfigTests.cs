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
    public void Sensor_Map_Example_Matches_Canonical_Counts_And_Excludes_Cannons_From_Sensors()
    {
        var path = Path.Combine(RepoRoot()!, "config", "examples", "sensor-map.example.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var matrix = node["logicalMatrix"]!.AsObject();

        var slots = matrix["slots"]!.AsArray();
        Assert.Equal(108, slots.Count);

        var sensors = slots.Where(s => (string?)s!["slotType"] == "SENSOR").ToArray();
        var cannons = slots.Where(s => (string?)s!["slotType"] == "CANNON").ToArray();
        Assert.Equal(106, sensors.Length);
        Assert.Equal(2, cannons.Length);

        foreach (var cannon in cannons)
        {
            Assert.Null(cannon!["sensorId"]);
            Assert.NotNull(cannon["equipmentId"]);
        }

        foreach (var sensor in sensors)
        {
            Assert.Null(sensor!["equipmentId"]);
            Assert.NotNull(sensor["sensorId"]);
        }

        var ids = sensors.Select(s => (string?)s!["sensorId"]).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.DoesNotContain("CANNON_REAR", ids);
        Assert.DoesNotContain("CANNON_FRONT", ids);

        // device distribution + TC channels: 106 sensors x 2 channels = 212 unique channels,
        // validated through the structured contract record (an array of exactly two strings);
        // a comma-delimited scalar never binds and is rejected before these assertions run
        var slotRecords = slots
            .Select(s => JsonSerializer.Deserialize<SensorMapSlotExample>(s!.ToJsonString(), ContractJson.Options)!)
            .ToArray();
        TcChannelRules.RequireValidMapping(slotRecords); // 108 slots / 106 sensors / 2 cannons / 212 distinct channels
        Assert.All(slotRecords.Where(r => !r.IsCannon), r =>
        {
            Assert.NotNull(r.TcChannels);
            Assert.Equal(2, r.TcChannels!.Count);
            Assert.Equal(r.TcChannels[0], r.TcChannels[0].Trim());
        });
        Assert.All(slotRecords.Where(r => r.IsCannon), r => Assert.Null(r.TcChannels));
        foreach (var wall in new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT })
        {
            var expected = wall == Wall.LEFT || wall == Wall.RIGHT ? 24 : 29;
            Assert.Equal(expected, slotRecords.Count(r => r.Wall == wall && !r.IsCannon));
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
        var dir = Path.Combine(RepoRoot()!, "config", "examples");
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("\"deviceProfile\": \"PRODUCTION", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"deviceProfile\": \"TEST_HARDWARE", text, StringComparison.Ordinal);
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
