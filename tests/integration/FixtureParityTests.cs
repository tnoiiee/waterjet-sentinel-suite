using System.Text.Json.Nodes;
using Xunit;

namespace Wjss.FixtureEmission.Tests;

/// <summary>
/// Golden-fixture parity. The committed files under packages/contracts/fixtures/
/// and config/examples/ are PROVISIONAL (Node-authored in Arena, because no .NET
/// SDK exists there). The first Owner-local run of this suite is the C# side of
/// the "regenerate and compare" gate. On any diff:
///   WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj
/// then commit the regenerated files in a review-correction checkpoint; the
/// TypeScript validator (packages/contracts/wjss-contracts-ts) must then pass.
/// Comparison is semantic (number-tolerant, key-order-tolerant, line-ending
/// tolerant) via the comparer below.
/// </summary>
public sealed class FixtureParityTests
{
    private static readonly string? Root = FindRoot();

    private static string? FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WaterJetSentinelSuite.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }

    private static void Check(string relativePath, string generated)
    {
        var path = Path.Combine(Root!, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"missing committed file {relativePath} (run with WJSS_UPDATE_FIXTURES=1 to generate)");

        var committed = File.ReadAllText(path);
        if (JsonCompare.Equal(JsonNode.Parse(committed), JsonNode.Parse(generated)))
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("WJSS_UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
            return;
        }

        Assert.Fail(
            $"{relativePath} differs from .NET-generated output. Replace the provisional fixture " +
            "via WJSS_UPDATE_FIXTURES=1 and re-run, then run the TypeScript validator.");
    }

    [Fact]
    public void Snapshot_Golden_Matches_Generator() =>
        Check("packages/contracts/fixtures/snapshot.seed0.json", FixtureGenerator.SerializeWithStatus(FixtureGenerator.BuildSnapshot()));

    [Fact]
    public void Delta_Basic_Golden_Matches_Generator() =>
        Check("packages/contracts/fixtures/delta.basic.json", FixtureGenerator.SerializeWithStatus(FixtureGenerator.BuildBasicDelta()));

    [Fact]
    public void Delta_Gap_Golden_Matches_Generator() =>
        Check("packages/contracts/fixtures/delta.gap.json", FixtureGenerator.SerializeWithStatus(FixtureGenerator.BuildGapDelta()));

    [Fact]
    public void Command_Refusal_Golden_Matches_Generator() =>
        Check("packages/contracts/fixtures/command.refusal.json", FixtureGenerator.SerializeWithStatus(new Wjss.Contracts.CommandOutcome
        {
            Accepted = false,
            Command = "autosequence-start",
            ReasonCode = Wjss.Contracts.Stage03A1.RuntimeNotImplemented,
            Detail = "Stage 0.3A-1 skeleton: the command surface arrives with the API host checkpoint (0.3A-3) after Owner-local validation.",
        }));

    [Fact]
    public void Sensor_Map_Example_Matches_Generator() =>
        Check("config/examples/sensor-map.example.json", ExampleFiles.SensorMap());

    [Fact]
    public void Published_Config_Example_Matches_Generator() =>
        Check("config/examples/published-config.example.json", ExampleFiles.PublishedConfig());

    [Fact]
    public void Generation_Is_Deterministic()
    {
        var first = FixtureGenerator.SerializeRaw(FixtureGenerator.BuildSnapshot());
        var second = FixtureGenerator.SerializeRaw(FixtureGenerator.BuildSnapshot());
        Assert.Equal(first, second);
    }
}
