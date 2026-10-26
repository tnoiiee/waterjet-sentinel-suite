using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Approved-topology invariants of the Runtime (Stage 0.3A-3 Checkpoint C):
/// exactly 8 Water Jets and 8 Isolation Valves in ordinal order, WJn ↔ IVn
/// pairing asserted in both directions, installed position separate from target
/// coverage (the legacy cleaning-device ordinal maps directly to WJn — a
/// rear-lower Sensor is assigned WJ5 even though WJ5 installs on the front-lower
/// wall), per-Sensor assignments derived only through the pairing, the corrected
/// Sensors I7 / I16 never presented as Water Jet identities or placement anchors (Owner
/// critical topology correction; Canonical Topology Adoption 2026-10-21: canonical
/// installed wall / tier with fine-grained placement UNRESOLVED for all eight), and the
/// Snapshot/Delta split: the process-lifetime topology is established by the
/// Snapshot and preserved unchanged by Delta application, never emitted
/// per-tick. Every value is synthetic; no Production value appears.
/// </summary>
public sealed class RuntimeTopologyTests
{
    private static readonly string[] ExpectedWaterJetIds =
        ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static readonly string[] ExpectedIsolationValveIds =
        ["IV1", "IV2", "IV3", "IV4", "IV5", "IV6", "IV7", "IV8"];

    // (installed wall, installed region, placement kind, target wall, target region) per ordinal.
    private static readonly (Wall InstalledWall, Region InstalledRegion, WaterJetPlacementKind Placement, Wall TargetWall, Region TargetRegion)[] ExpectedTopology =
    [
        (Wall.LEFT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, Wall.RIGHT, Region.UPPER),
        (Wall.REAR, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, Wall.FRONT, Region.UPPER),
        (Wall.RIGHT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, Wall.LEFT, Region.UPPER),
        (Wall.FRONT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, Wall.REAR, Region.UPPER),
        (Wall.FRONT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, Wall.REAR, Region.LOWER),
        (Wall.REAR, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, Wall.FRONT, Region.LOWER),
        (Wall.LEFT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, Wall.RIGHT, Region.LOWER),
        (Wall.RIGHT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, Wall.LEFT, Region.LOWER),
    ];

    // The corrected Product Sensors (former gap positions): I7 at row 5 column 7 (REAR),
    // I16 at row 5 column 16 (FRONT). Neither anchors any Water Jet.
    private static readonly (string SensorId, Wall Wall, int Row, int Column)[] ExpectedCorrectedSensors =
    [
        ("I7", Wall.REAR, 5, 7),
        ("I16", Wall.FRONT, 5, 16),
    ];

    [Fact]
    public void Composed_State_Carries_Exactly_Eight_Water_Jets_And_Eight_Isolation_Valves_In_Ordinal_Order()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var waterJetCount = state.WaterJets.Count;
        var isolationValveCount = state.IsolationValves.Count;
        Assert.Equal(8, waterJetCount);
        Assert.Equal(8, isolationValveCount);

        var waterJetIds = state.WaterJets.Select(w => w.WaterJetId).ToArray();
        var isolationValveIds = state.IsolationValves.Select(v => v.ValveId).ToArray();
        Assert.Equal(ExpectedWaterJetIds, waterJetIds);
        Assert.Equal(ExpectedIsolationValveIds, isolationValveIds);
    }

    [Fact]
    public void WaterJets_And_IsolationValves_Pair_One_To_One_In_Both_Directions()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        for (var ordinal = 0; ordinal < 8; ordinal++)
        {
            var waterJet = state.WaterJets[ordinal];
            var valve = state.IsolationValves[ordinal];
            Assert.Equal($"IV{ordinal + 1}", waterJet.DedicatedIsolationValveId);
            Assert.Equal($"WJ{ordinal + 1}", valve.ServedWaterJetId);
        }
    }

    [Fact]
    public void Installed_Position_Is_Separate_From_Target_Coverage_Per_Approved_Table()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        for (var ordinal = 0; ordinal < 8; ordinal++)
        {
            var waterJet = state.WaterJets[ordinal];
            var expected = ExpectedTopology[ordinal];
            Assert.Equal(expected.InstalledWall, waterJet.InstalledWall);
            Assert.Equal(expected.InstalledRegion, waterJet.InstalledRegion);
            Assert.Equal(expected.Placement, waterJet.PlacementKind);
            Assert.Equal(expected.TargetWall, waterJet.TargetWall);
            Assert.Equal(expected.TargetRegion, waterJet.TargetRegion);
            Assert.NotEqual(waterJet.InstalledWall, waterJet.TargetWall);
            // Current Owner disposition: every fine-grained placement is UNRESOLVED and
            // anchor-free; only the canonical installed wall / region above is authority.
            Assert.Equal(WaterJetPlacementKind.UNRESOLVED, waterJet.PlacementKind);
            Assert.Empty(waterJet.PlacementAnchors);
        }
    }

    [Fact]
    public void Every_Sensor_Is_Assigned_A_WaterJet_Whose_Target_Coverage_Matches_The_Sensor_Wall_And_Region()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.Equal(CanonicalSensorMap.SensorLocations, state.Sensors.Count);
        foreach (var sensor in state.Sensors)
        {
            var waterJet = WaterJetTopologyCatalog.FindWaterJet(sensor.AssignedWaterJetId);
            Assert.NotNull(waterJet);
            Assert.Equal(sensor.Wall, waterJet.TargetWall);
            Assert.Equal(
                WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow),
                waterJet.TargetRegion);
            Assert.Equal(waterJet.DedicatedIsolationValveId, sensor.AssignedIsolationValveId);
        }
    }

    [Fact]
    public void Rear_Lower_Sensors_Are_Assigned_WJ5_Even_Though_WJ5_Installs_On_The_Front_Lower_Wall()
    {
        // The binding legacy-assignment example (canonical topology): a rear-lower
        // Sensor with legacy cleaning-device ordinal 5 is assigned WJ5/IV5 directly
        // (never remapped by wall), while WJ5 itself installs front-lower and targets
        // rear-lower.
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var rearLowerSensors = state.Sensors
            .Where(sensor => sensor.Wall == Wall.REAR && sensor.LogicalRow == 5)
            .ToArray();
        Assert.True(rearLowerSensors.Length > 0);
        Assert.All(rearLowerSensors, sensor =>
        {
            Assert.Equal("WJ5", sensor.AssignedWaterJetId);
            Assert.Equal("IV5", sensor.AssignedIsolationValveId);
        });

        var wj5 = state.WaterJets.Single(w => w.WaterJetId == "WJ5");
        Assert.Equal(Wall.FRONT, wj5.InstalledWall);
        Assert.Equal(Wall.REAR, wj5.TargetWall);
    }

    [Fact]
    public void Corrected_Sensors_I7_And_I16_Are_Ordinary_Sensor_Slots_And_Never_WaterJet_Identities_Or_Anchors()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.DoesNotContain(state.WallMap, slot => slot.PositionKind != LogicalPositionKind.SENSOR);
        foreach (var expected in ExpectedCorrectedSensors)
        {
            var slot = state.WallMap.Single(candidate =>
                candidate.LogicalRow == expected.Row && candidate.LogicalColumn == expected.Column);
            Assert.Equal(LogicalPositionKind.SENSOR, slot.PositionKind);
            Assert.Equal(expected.SensorId, slot.SensorId);
            Assert.Equal(expected.Wall, slot.Wall);
            Assert.Single(state.Sensors, sensor => sensor.SensorId == expected.SensorId);
        }

        // Sensor identities and equipment identities are disjoint, and no Water Jet anchors
        // on I7 or I16 (nothing is inferred from a former gap): ALL EIGHT Water Jets are
        // UNRESOLVED with no anchors under the current disposition; canonical Target
        // Coverage is the opposite wall in the same tier.
        var waterJetIds = state.WaterJets.Select(w => w.WaterJetId).ToArray();
        Assert.All(ExpectedCorrectedSensors, expected => Assert.DoesNotContain(expected.SensorId, waterJetIds));
        Assert.All(state.WaterJets, w =>
        {
            Assert.Equal(WaterJetPlacementKind.UNRESOLVED, w.PlacementKind);
            Assert.Empty(w.PlacementAnchors);
        });
        var wj1 = state.WaterJets.Single(w => w.WaterJetId == "WJ1");
        var wj3 = state.WaterJets.Single(w => w.WaterJetId == "WJ3");
        Assert.Equal((Wall.RIGHT, Region.UPPER), (wj1.TargetWall, wj1.TargetRegion));
        Assert.Equal((Wall.LEFT, Region.UPPER), (wj3.TargetWall, wj3.TargetRegion));
    }

    [Fact]
    public void Snapshot_Projects_The_Topology_And_Round_Trip_Preserves_It()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var snapshot = RuntimeSnapshotProjector.Project(store.Current, store.Counters, 0.0);

        var waterJetCount = snapshot.WaterJets.Count;
        var isolationValveCount = snapshot.IsolationValves.Count;
        Assert.Equal(8, waterJetCount);
        Assert.Equal(8, isolationValveCount);

        var wire = JsonSerializer.Serialize(snapshot, ContractJson.Options);
        var roundTripped = JsonSerializer.Deserialize<OperationalSnapshot>(wire, ContractJson.Options);

        Assert.NotNull(roundTripped);

        // Record equality would compare the nested PlacementAnchors collection by
        // reference (a frozen wrapper before serialization, a List<string> after
        // deserialization), so the round-trip is asserted field-by-field instead;
        // the anchor sequence itself is compared by contents.
        var expectedWaterJetCount = snapshot.WaterJets.Count;
        var roundTripWaterJetCount = roundTripped.WaterJets.Count;
        Assert.Equal(expectedWaterJetCount, roundTripWaterJetCount);
        for (var ordinal = 0; ordinal < snapshot.WaterJets.Count; ordinal++)
        {
            var expected = snapshot.WaterJets[ordinal];
            var actual = roundTripped.WaterJets[ordinal];
            Assert.Equal(expected.WaterJetId, actual.WaterJetId);
            Assert.Equal(expected.InstalledWall, actual.InstalledWall);
            Assert.Equal(expected.InstalledRegion, actual.InstalledRegion);
            Assert.Equal(expected.PlacementKind, actual.PlacementKind);
            Assert.Equal(expected.PlacementAnchors, actual.PlacementAnchors);
            Assert.Equal(expected.TargetWall, actual.TargetWall);
            Assert.Equal(expected.TargetRegion, actual.TargetRegion);
            Assert.Equal(expected.DedicatedIsolationValveId, actual.DedicatedIsolationValveId);
        }

        var expectedIsolationValveCount = snapshot.IsolationValves.Count;
        var roundTripIsolationValveCount = roundTripped.IsolationValves.Count;
        Assert.Equal(expectedIsolationValveCount, roundTripIsolationValveCount);
        for (var ordinal = 0; ordinal < snapshot.IsolationValves.Count; ordinal++)
        {
            var expected = snapshot.IsolationValves[ordinal];
            var actual = roundTripped.IsolationValves[ordinal];
            Assert.Equal(expected.ValveId, actual.ValveId);
            Assert.Equal(expected.ServedWaterJetId, actual.ServedWaterJetId);
        }
        Assert.All(roundTripped.Sensors.Zip(snapshot.Sensors), pair =>
        {
            Assert.Equal(pair.Second.AssignedWaterJetId, pair.First.AssignedWaterJetId);
            Assert.Equal(pair.Second.AssignedIsolationValveId, pair.First.AssignedIsolationValveId);
        });
    }

    [Fact]
    public void Delta_Never_Emits_The_Topology_And_Application_Preserves_It_Unchanged()
    {
        var initial = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();

        var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
            writer, initial, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        // The wire Delta carries no topology key at all: the process-lifetime
        // topology is established by the Snapshot and never emitted per-tick.
        var deltaWire = JsonSerializer.Serialize(RuntimeDeltaProjector.ProjectWire(delta), ContractJson.Options);
        using (var document = JsonDocument.Parse(deltaWire))
        {
            var hasTopologyKey =
                document.RootElement.TryGetProperty("waterJets", out _)
                || document.RootElement.TryGetProperty("isolationValves", out _)
                || document.RootElement.TryGetProperty("wallMap", out _);
            Assert.False(hasTopologyKey, "the process-lifetime topology is Snapshot-only and must never be emitted in a Delta");
        }

        // Reconstruction through Delta application keeps the topology equal to the
        // pre-evolution revision (immutable for the process lifetime).
        var outcome = RuntimeDeltaApply.Apply(initial, delta);
        Assert.True(outcome.Applied, outcome.Reason);
        Assert.Equal(initial.WaterJets, outcome.State!.WaterJets);
        Assert.Equal(initial.IsolationValves, outcome.State!.IsolationValves);
        Assert.Equal(committed.WaterJets, outcome.State!.WaterJets);
        Assert.Equal(committed.IsolationValves, outcome.State!.IsolationValves);
    }

    [Fact]
    public void Readiness_And_Profile_Refusals_Remain_Intact_With_The_Topology_Present()
    {
        // The topology addition does not weaken the profile gate: a non-SIMULATOR
        // profile still cannot compose, with the approved topology supplied.
        var config = RuntimeTestFixture.Config();
        var wallMap = SyntheticSensorMap.BuildWallMap();
        var sensors = SyntheticSensorMap.BuildInitialSensors(config, SyntheticSeed.DefaultSeed, RuntimeTestFixture.Instant);

        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = RuntimeStateComposer.ComposeInitial(
                DeviceProfile.TEST_HARDWARE,
                config,
                wallMap,
                sensors,
                WaterJetTopologyCatalog.WaterJets,
                WaterJetTopologyCatalog.IsolationValves,
                RuntimeTestFixture.Instant);
        });

        Assert.Contains(ProfileStartPolicy.RefusalCode, refused.Message, StringComparison.Ordinal);
    }
}
