using System.Text.Json;
using System.Text.Json.Nodes;
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
///
/// Nullable flow (Owner-local compile correction, 2026-10-08): every JSON access is
/// captured through <see cref="JsonNodeExtensions.RequireString"/>/
/// <see cref="JsonNodeExtensions.RequireInt"/>/<see cref="JsonNodeExtensions.RequireArray"/>
/// into validated non-null locals — explicit throw guards instead of scattered
/// null-forgiving operators, with all test meanings and expected values preserved.
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

        Assert.Contains("SYNTHETIC", JsonNodeExtensions.RequireString(node, "_label"), StringComparison.Ordinal);
        Assert.Contains("NOT FOR DEPLOYMENT", JsonNodeExtensions.RequireString(node, "_label"), StringComparison.Ordinal);

        var logicalPositions = JsonNodeExtensions.RequireArray(node, "logicalPositions");
        var logicalPositionCount = logicalPositions.Count;
        Assert.Equal(108, logicalPositionCount);

        var sensorConfigurations = JsonNodeExtensions.RequireArray(node, "sensors");
        var sensorConfigurationCount = sensorConfigurations.Count;
        Assert.Equal(106, sensorConfigurationCount);

        var waterJets = JsonNodeExtensions.RequireArray(node, "waterJets");
        var waterJetCount = waterJets.Count;
        Assert.Equal(8, waterJetCount);

        var isolationValves = JsonNodeExtensions.RequireArray(node, "isolationValves");
        var isolationValveCount = isolationValves.Count;
        Assert.Equal(8, isolationValveCount);

        var warnings = JsonNodeExtensions.RequireArray(node, "warnings");
        Assert.Empty(warnings);
    }

    [Fact]
    public void Migrated_Example_Satisfies_Ordering_Gaps_And_Pairing()
    {
        var node = JsonNodeExtensions.ParseExample();

        var positions = JsonNodeExtensions.RequireArray(node, "logicalPositions");
        Assert.Equal(
            Enumerable.Range(0, 108).ToArray(),
            positions.Select(p => JsonNodeExtensions.RequireInt(p, "orderTotal")).ToArray());

        var gaps = positions
            .Where(p => JsonNodeExtensions.RequireString(p, "positionKind") == "NON_SENSOR_GAP")
            .ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.Equal("I7", JsonNodeExtensions.RequireString(gaps[0], "logicalId"));
        Assert.Equal("WJ3", JsonNodeExtensions.RequireString(gaps[0], "gapAnchorForWaterJetId"));
        Assert.Equal("I16", JsonNodeExtensions.RequireString(gaps[1], "logicalId"));
        Assert.Equal("WJ1", JsonNodeExtensions.RequireString(gaps[1], "gapAnchorForWaterJetId"));

        var sensors = JsonNodeExtensions.RequireArray(node, "sensors");
        Assert.Equal(
            Enumerable.Range(1, 106).ToArray(),
            sensors.Select(s => JsonNodeExtensions.RequireInt(s, "scanOrder")).ToArray());
        Assert.DoesNotContain(sensors, s =>
        {
            var id = JsonNodeExtensions.RequireString(s, "sensorId");
            return id == "I7" || id == "I16";
        });

        foreach (var n in Enumerable.Range(1, 8))
        {
            var waterJet = JsonNodeExtensions.RequireArray(node, "waterJets")
                .Single(w => JsonNodeExtensions.RequireString(w, "waterJetId") == $"WJ{n}");
            Assert.Equal($"IV{n}", JsonNodeExtensions.RequireString(waterJet, "dedicatedIsolationValveId"));
            var valve = JsonNodeExtensions.RequireArray(node, "isolationValves")
                .Single(v => JsonNodeExtensions.RequireString(v, "valveId") == $"IV{n}");
            Assert.Equal($"WJ{n}", JsonNodeExtensions.RequireString(valve, "servedWaterJetId"));
        }
    }

    [Fact]
    public void Migrated_Example_Assignments_Match_Approved_Target_Coverage()
    {
        var node = JsonNodeExtensions.ParseExample();

        foreach (var sensor in JsonNodeExtensions.RequireArray(node, "sensors"))
        {
            var logicalRow = JsonNodeExtensions.RequireInt(sensor, "logicalRow");
            var region = logicalRow <= 2 ? "UPPER" : "LOWER";
            var wall = JsonNodeExtensions.RequireString(sensor, "wall");
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
            Assert.Equal($"WJ{expectedOrdinal}", JsonNodeExtensions.RequireString(sensor, "assignedWaterJetId"));
            Assert.Equal($"IV{expectedOrdinal}", JsonNodeExtensions.RequireString(sensor, "assignedIsolationValveId"));
        }

        var opposite = new Dictionary<string, string>
        {
            ["LEFT"] = "RIGHT", ["RIGHT"] = "LEFT", ["FRONT"] = "REAR", ["REAR"] = "FRONT",
        };
        foreach (var waterJet in JsonNodeExtensions.RequireArray(node, "waterJets"))
        {
            var installed = JsonNodeExtensions.RequireString(waterJet, "installedWall");
            Assert.Equal(opposite[installed], JsonNodeExtensions.RequireString(waterJet, "targetWall"));
            Assert.Equal(
                JsonNodeExtensions.RequireString(waterJet, "installedRegion"),
                JsonNodeExtensions.RequireString(waterJet, "targetRegion"));
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
        foreach (var position in root.GetProperty("logicalPositions").EnumerateArray())
        {
            var recordId = position.GetProperty("legacyRecordId").GetString();
            if (recordId is null)
            {
                throw new InvalidOperationException(
                    "expected property 'legacyRecordId' to be a non-null JSON string");
            }

            Assert.Matches("^SYN-REC-\\d{3}$", recordId);
        }
    }

    [Fact]
    public void Migrated_Example_Labels_Agree_With_OrderTotal_Derivation()
    {
        var node = JsonNodeExtensions.ParseExample();

        foreach (var position in JsonNodeExtensions.RequireArray(node, "logicalPositions"))
        {
            var orderTotal = JsonNodeExtensions.RequireInt(position, "orderTotal");
            var row = (orderTotal / 18) + 1;
            var column = (orderTotal % 18) + 1;
            var logicalId = JsonNodeExtensions.RequireString(position, "logicalId");
            var wall = Enum.Parse<Wall>(JsonNodeExtensions.RequireString(position, "wall"), ignoreCase: false);
            var wallColumn = JsonNodeExtensions.RequireInt(position, "wallColumn");

            Assert.Equal(CanonicalSensorMap.SensorIdFor(row, column), logicalId);
            Assert.Equal(CanonicalSensorMap.WallForColumn(column), wall);
            Assert.Equal(column - CanonicalSensorMap.WallColumns[wall].FirstColumn + 1, wallColumn);
        }
    }
}

/// <summary>
/// Small local helpers keeping the JSON access in these tests explicit and
/// nullable-safe: each helper validates presence and kind with an explicit throw guard
/// and returns a non-null value, so no null-forgiving operator is needed at the call
/// sites and the compiler's nullable flow stays satisfied.
/// </summary>
internal static class JsonNodeExtensions
{
    internal static JsonNode ParseExample()
    {
        var path = Path.Combine(
            ConfigTestPaths.RepoRoot(), "config", "examples", "sensor-parameters.migrated.example.json");
        return JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException("the migrated example must parse to a JSON document");
    }

    /// <summary>Returns a non-null string property value, or throws an explicit guard failure.</summary>
    internal static string RequireString(JsonNode? parent, string field)
    {
        var value = parent?[field]?.GetValue<string>();
        return value
            ?? throw new InvalidOperationException($"expected property '{field}' to be a non-null JSON string");
    }

    /// <summary>Returns an int property value, or throws an explicit guard failure.</summary>
    internal static int RequireInt(JsonNode? parent, string field)
    {
        var node = parent?[field]
            ?? throw new InvalidOperationException($"expected property '{field}' to exist");
        return node.GetValue<int>();
    }

    /// <summary>Returns a JSON array property, or throws an explicit guard failure.</summary>
    internal static JsonArray RequireArray(JsonNode parent, string field)
    {
        return parent[field]?.AsArray()
            ?? throw new InvalidOperationException($"expected property '{field}' to be a JSON array");
    }
}
