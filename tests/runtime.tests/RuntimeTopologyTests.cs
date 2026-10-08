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
/// rear-lower Sensor is assigned WJ1 even though WJ1 installs on the front-lower
/// wall), per-Sensor assignments derived only through the pairing, the
/// NON_SENSOR_GAP positions never presented as Water Jet identities, and the
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
        (Wall.FRONT, Region.LOWER, WaterJetPlacementKind.NON_SENSOR_GAP, Wall.REAR, Region.LOWER),
        (Wall.LEFT, Region.LOWER, WaterJetPlacementKind.BETWEEN_HORIZONTAL, Wall.RIGHT, Region.LOWER),
        (Wall.REAR, Region.LOWER, WaterJetPlacementKind.NON_SENSOR_GAP, Wall.FRONT, Region.LOWER),
        (Wall.RIGHT, Region.LOWER, WaterJetPlacementKind.BETWEEN_HORIZONTAL, Wall.LEFT, Region.LOWER),
        (Wall.FRONT, Region.UPPER, WaterJetPlacementKind.BETWEEN_VERTICAL, Wall.REAR, Region.UPPER),
        (Wall.LEFT, Region.UPPER, WaterJetPlacementKind.JUNCTION, Wall.RIGHT, Region.UPPER),
        (Wall.REAR, Region.UPPER, WaterJetPlacementKind.BETWEEN_VERTICAL, Wall.FRONT, Region.UPPER),
        (Wall.RIGHT, Region.UPPER, WaterJetPlacementKind.JUNCTION, Wall.LEFT, Region.UPPER),
    ];

    // The two NON_SENSOR_GAP anchors: I7 anchors WJ3, I16 anchors WJ1.
    private static readonly (string LogicalId, string AnchorWaterJetId, int Row, int Column)[] ExpectedGapAnchors =
    [
        ("I7", "WJ3", 5, 7),
        ("I16", "WJ1", 5, 16),
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
    public void Rear_Lower_Sensors_Are_Assigned_WJ1_Even_Though_WJ1_Installs_On_The_Front_Lower_Wall()
    {
        // The binding legacy-assignment example: a rear-lower Sensor with legacy
        // cleaning-device ordinal 1 is assigned WJ1/IV1 directly (never remapped by
        // wall), while WJ1 itself installs front-lower and targets rear-lower.
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var rearLowerSensors = state.Sensors
            .Where(sensor => sensor.Wall == Wall.REAR && sensor.LogicalRow == 5)
            .ToArray();
        Assert.True(rearLowerSensors.Length > 0);
        Assert.All(rearLowerSensors, sensor =>
        {
            Assert.Equal("WJ1", sensor.AssignedWaterJetId);
            Assert.Equal("IV1", sensor.AssignedIsolationValveId);
        });

        var wj1 = state.WaterJets.Single(w => w.WaterJetId == "WJ1");
        Assert.Equal(Wall.FRONT, wj1.InstalledWall);
        Assert.Equal(Wall.REAR, wj1.TargetWall);
    }

    [Fact]
    public void NonSensorGap_Positions_Anchor_WJ3_And_WJ1_And_Are_Never_WaterJet_Identities()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var gapSlots = state.WallMap
            .Where(slot => slot.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
            .ToArray();
        var gapCount = gapSlots.Length;
        Assert.Equal(2, gapCount);
        foreach (var gap in gapSlots)
        {
            var expected = ExpectedGapAnchors.Single(candidate =>
                candidate.Row == gap.LogicalRow && candidate.Column == gap.LogicalColumn);
            Assert.Equal(expected.AnchorWaterJetId, gap.GapAnchorForWaterJetId);
            Assert.Null(gap.SensorId);
        }

        // The anchors are location labels, not device identities: no Water Jet carries
        // a gap slot's anchor the other way round, and no gap slot carries a Water Jet
        // id. I7 anchors WJ3, I16 anchors WJ1 — never the reverse, never itself.
        var waterJetIds = state.WaterJets.Select(w => w.WaterJetId).ToArray();
        Assert.All(gapSlots, slot => Assert.Contains(slot.GapAnchorForWaterJetId, waterJetIds));
        Assert.Equal("WJ3", gapSlots.Single(slot => slot.LogicalColumn == 7).GapAnchorForWaterJetId);
        Assert.Equal("WJ1", gapSlots.Single(slot => slot.LogicalColumn == 16).GapAnchorForWaterJetId);
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
