using System.Text.Json;
using System.Text.RegularExpressions;
using Wjss.Config.Examples.Tests; // ConfigTestPaths — shared repository-root discovery
using Wjss.Contracts;
using Xunit;

namespace Wjss.Config.Tests;

/// <summary>
/// Schema-guard tests for <c>config/examples/sensor-parameters.migrated.example.json</c>
/// (Stage 0.3A-3 Checkpoint B): the committed example demonstrates the canonical migrated
/// shape with synthetic values only, satisfies every structural invariant the importer
/// enforces, and carries NO deferred raw values and NO acquisition-binding provenance
/// (public-repository boundary: the Owner CSV and every real acquisition value stay
/// outside Git). Directory-wide vocabulary and profile scans in
/// <see cref="ExampleConfigTests"/> cover this file automatically.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public sealed class MigratedExampleShapeTests
{
    private static readonly string ExamplePath = Path.Combine(
        ConfigTestPaths.RepoRoot(), "config", "examples", "sensor-parameters.migrated.example.json");

    [Fact]
    public void Migrated_Example_Has_Synthetic_Label_And_Canonical_Counts()
    {
        var node = JsonNodeExtensions.ParseExample();

        Assert.Contains("SYNTHETIC", (string?)node["_label"], StringComparison.Ordinal);
        Assert.Contains("NOT FOR DEPLOYMENT", (string?)node["_label"], StringComparison.Ordinal);
        Assert.Equal(108, node["logicalPositions"]!.AsArray().Count);
        Assert.Equal(106, node["sensors"]!.AsArray().Count);
        Assert.Equal(8, node["waterJets"]!.AsArray().Count);
        Assert.Equal(8, node["isolationValves"]!.AsArray().Count);
        Assert.Equal(0, node["warnings"]!.AsArray().Count);
    }

    [Fact]
    public void Migrated_Example_Satisfies_Ordering_Gaps_And_Pairing()
    {
        var node = JsonNodeExtensions.ParseExample();

        var positions = node["logicalPositions"]!.AsArray();
        Assert.Equal(
            Enumerable.Range(0, 108).ToArray(),
            positions.Select(p => (int)p!["orderTotal"]!).ToArray());

        var gaps = positions.Where(p => (string?)p!["positionKind"] == "NON_SENSOR_GAP").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.Equal("I7", (string?)gaps[0]["logicalId"]);
        Assert.Equal("WJ3", (string?)gaps[0]["gapAnchorForWaterJetId"]);
        Assert.Equal("I16", (string?)gaps[1]["logicalId"]);
        Assert.Equal("WJ1", (string?)gaps[1]["gapAnchorForWaterJetId"]);

        var sensors = node["sensors"]!.AsArray();
        Assert.Equal(
            Enumerable.Range(1, 106).ToArray(),
            sensors.Select(s => (int)s!["scanOrder"]!).ToArray());
        Assert.DoesNotContain(sensors, s =>
        {
            var id = (string?)s!["sensorId"];
            return id == "I7" || id == "I16";
        });

        foreach (var n in Enumerable.Range(1, 8))
        {
            var waterJet = node["waterJets"]!.AsArray().Single(w => (string?)w!["waterJetId"] == $"WJ{n}");
            Assert.Equal($"IV{n}", (string?)waterJet["dedicatedIsolationValveId"]);
            var valve = node["isolationValves"]!.AsArray().Single(v => (string?)v!["valveId"] == $"IV{n}");
            Assert.Equal($"WJ{n}", (string?)valve["servedWaterJetId"]);
        }
    }

    [Fact]
    public void Migrated_Example_Assignments_Match_Approved_Target_Coverage()
    {
        var node = JsonNodeExtensions.ParseExample();

        foreach (var sensor in node["sensors"]!.AsArray())
        {
            var logicalRow = (int)sensor!["logicalRow"]!;
            var region = logicalRow <= 2 ? "UPPER" : "LOWER";
            var wall = (string?)sensor["wall"];
            var expectedOrdinal = (wall, region) switch
            {
                ("REAR", "LOWER") => 1,
                ("RIGHT", "LOWER") => 2,
                ("FRONT", "LOWER") => 3,
                ("LEFT", "LOWER") => 4,
                ("REAR", "UPPER") => 5,
                ("RIGHT", "UPPER") => 6,
                ("FRONT", "UPPER") => 7,
                ("LEFT", "UPPER") => 8,
                _ => throw new InvalidOperationException("synthetic example left the canonical grid"),
            };
            Assert.Equal($"WJ{expectedOrdinal}", (string?)sensor["assignedWaterJetId"]);
            Assert.Equal($"IV{expectedOrdinal}", (string?)sensor["assignedIsolationValveId"]);
        }

        var opposite = new Dictionary<string, string>
        {
            ["LEFT"] = "RIGHT", ["RIGHT"] = "LEFT", ["FRONT"] = "REAR", ["REAR"] = "FRONT",
        };
        foreach (var waterJet in node["waterJets"]!.AsArray())
        {
            var installed = (string?)waterJet["installedWall"];
            Assert.Equal(opposite[installed!], (string?)waterJet["targetWall"]);
            Assert.Equal((string?)waterJet["installedRegion"], (string?)waterJet["targetRegion"]);
        }
    }

    [Fact]
    public void Migrated_Example_Carries_No_Acquisition_Or_Real_Values()
    {
        var text = File.ReadAllText(ExamplePath);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        // No deferred value of any kind is populated in the public example.
        foreach (var sensor in root.GetProperty("sensors").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("deferredAcquisition").ValueKind);
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("lastCleanTimestampRaw").ValueKind);
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("minTimeAllowAddToQueueRaw").ValueKind);
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("maxTimeAllowAddToQueueRaw").ValueKind);
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("maxTimeEnableRaw").ValueKind);
            Assert.Equal(JsonValueKind.Null, sensor.GetProperty("latestUpdateParamsTimestampRaw").ValueKind);
        }

        // No IPv4-shaped value anywhere in the committed example text.
        Assert.False(Regex.IsMatch(text, @"\b(?:\d{1,3}\.){3}\d{1,3}\b"), "no address-shaped literal may appear");

        // Record identifiers stay obviously synthetic.
        Assert.All(root.GetProperty("logicalPositions").EnumerateArray().ToArray(), position =>
            Assert.Matches("^SYN-REC-\\d{3}$", position.GetProperty("legacyRecordId").GetString()!));
    }

    [Fact]
    public void Migrated_Example_Labels_Agree_With_OrderTotal_Derivation()
    {
        var node = JsonNodeExtensions.ParseExample();

        foreach (var position in node["logicalPositions"]!.AsArray())
        {
            var orderTotal = (int)position!["orderTotal"]!;
            var row = (orderTotal / 18) + 1;
            var column = (orderTotal % 18) + 1;
            Assert.Equal(CanonicalSensorMap.SensorIdFor(row, column), (string?)position["logicalId"]);
            Assert.Equal(CanonicalSensorMap.WallForColumn(column), Enum.Parse<Wall>((string?)position["wall"]!, false));
            var wall = Enum.Parse<Wall>((string?)position["wall"]!, false);
            Assert.Equal(column - CanonicalSensorMap.WallColumns[wall].FirstColumn + 1, (int)position["wallColumn"]!);
        }
    }
}

/// <summary>Small local helper keeping the JSON access in these tests explicit.</summary>
internal static class JsonNodeExtensions
{
    internal static System.Text.Json.Nodes.JsonNode ParseExample()
    {
        var path = Path.Combine(
            ConfigTestPaths.RepoRoot(), "config", "examples", "sensor-parameters.migrated.example.json");
        return System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
    }
}
