using System.Reflection;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Read-only API projections of the approved equipment topology, corrected to the Owner
/// critical topology correction (Stage 0.4C-K3): the status counts (108 slots / 108
/// Sensors / 216 assigned / 240 physical / 24 unassigned / 8 WJ / 8 IV), the corrected
/// Sensor references I7 and I16 (normal Product Sensors, never presented as Water Jet
/// identities, never gaps), the paired WJ/IV topology views with installed position
/// separate from target coverage (WJ1 / WJ3 UNRESOLVED, no anchors) and acquisition
/// explicitly DEFERRED, and per-Sensor assigned device references. No control surface is asserted to exist: the
/// route registry carries the read-only routes only, every host mapping is a
/// GET, and no write/command/dispatch route constant exists to map.
/// Everything here is synthetic; nothing commands or actuates anything.
/// </summary>
public sealed class RuntimeApiTopologyTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] ExpectedCorrectedSensorIds = ["I7", "I16"];

    private static readonly string[] ExpectedWaterJetIds =
        ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static RuntimeSensorView ViewOf(SensorPresentationState sensor) => RuntimeSensorView.From(sensor);

    [Fact]
    public void Status_Counts_Carry_The_Protected_Baseline_And_The_Topology_Totals()
    {
        var counts = RuntimeStatusSensorCounts.Canonical();

        Assert.Equal(108, counts.LogicalSlots.Count);
        Assert.Equal(108, counts.SensorLocations.Count);
        Assert.Equal(216, counts.ThermocoupleChannels.Count);
        Assert.Equal(240, counts.PhysicalThermocoupleChannels.Count);
        Assert.Equal(24, counts.UnassignedThermocoupleChannels.Count);

        var waterJetCount = counts.WaterJets.Count;
        var isolationValveCount = counts.IsolationValves.Count;
        Assert.Equal(8, waterJetCount);
        Assert.Equal(8, isolationValveCount);

        // The former gap projection is gone from the API surface (no NonSensorGaps /
        // NonSensorGapSlots members exist to publish a 106 / 212 reading).
        Assert.DoesNotContain(typeof(RuntimeStatusSensorCounts).GetProperties(), p => p.Name.Contains("Gap", StringComparison.Ordinal));
    }

    [Fact]
    public void Corrected_Sensor_References_Are_I7_And_I16_In_Canonical_Order()
    {
        var counts = RuntimeStatusSensorCounts.Canonical();

        Assert.Equal(ExpectedCorrectedSensorIds, counts.CorrectedSensorIds.ToArray());
        Assert.All(counts.CorrectedSensorIds, id => Assert.True(CanonicalSensorMap.IsProductSensorId(id)));
        Assert.Equal(78, CanonicalSensorMap.SensorSequence.ToList().IndexOf("I7"));  // scanOrder 79
        Assert.Equal(87, CanonicalSensorMap.SensorSequence.ToList().IndexOf("I16")); // scanOrder 88
    }

    [Fact]
    public void Corrected_Sensors_Are_Never_Presented_As_Water_Jet_Identities_Or_Anchors()
    {
        var topology = RuntimeEquipmentTopology.Canonical();

        var waterJetIds = topology.WaterJets.Select(w => w.WaterJetId).ToArray();
        Assert.Equal(ExpectedWaterJetIds, waterJetIds);
        Assert.All(ExpectedCorrectedSensorIds, id => Assert.DoesNotContain(id, waterJetIds));
        Assert.All(topology.WaterJets, w => Assert.DoesNotContain(w.WaterJetId, ExpectedCorrectedSensorIds));
        Assert.All(topology.WaterJets, w => Assert.DoesNotContain(w.PlacementAnchors, a => a is "I7" or "I16"));
    }

    [Fact]
    public void WJ1_And_WJ3_Installed_Position_Is_Unresolved_With_No_Anchor_And_Unchanged_Target_Coverage()
    {
        var topology = RuntimeEquipmentTopology.Canonical();

        var wj1 = topology.WaterJets.Single(w => w.WaterJetId == "WJ1");
        Assert.Equal(WaterJetPlacementKind.UNRESOLVED, wj1.PlacementKind);
        Assert.Empty(wj1.PlacementAnchors);
        Assert.Equal(WaterJetTopologyCatalog.UnresolvedPlacementWording, wj1.InstalledPositionNote);
        Assert.Equal(Wall.REAR, wj1.TargetWall);
        Assert.Equal(Region.LOWER, wj1.TargetRegion);
        Assert.Equal("IV1", wj1.DedicatedIsolationValveId);

        var wj3 = topology.WaterJets.Single(w => w.WaterJetId == "WJ3");
        Assert.Equal(WaterJetPlacementKind.UNRESOLVED, wj3.PlacementKind);
        Assert.Empty(wj3.PlacementAnchors);
        Assert.Equal(WaterJetTopologyCatalog.UnresolvedPlacementWording, wj3.InstalledPositionNote);
        Assert.Equal(Wall.FRONT, wj3.TargetWall);
        Assert.Equal(Region.LOWER, wj3.TargetRegion);
        Assert.Equal("IV3", wj3.DedicatedIsolationValveId);

        Assert.All(topology.WaterJets.Where(w => w.PlacementKind != WaterJetPlacementKind.UNRESOLVED), w =>
        {
            Assert.Null(w.InstalledPositionNote);
            Assert.True(w.PlacementAnchors.Count > 0);
        });
    }

    [Fact]
    public void Equipment_Topology_Exposes_Paired_Devices_With_Installed_Separate_From_Target()
    {
        var topology = RuntimeEquipmentTopology.Canonical();

        Assert.Equal(RuntimeEquipmentTopology.AcquisitionStatus, topology.Acquisition);
        Assert.Contains("DEFERRED", topology.Acquisition, StringComparison.Ordinal);

        var waterJetCount = topology.WaterJets.Count;
        var isolationValveCount = topology.IsolationValves.Count;
        Assert.Equal(8, waterJetCount);
        Assert.Equal(8, isolationValveCount);

        for (var ordinal = 0; ordinal < 8; ordinal++)
        {
            var waterJet = topology.WaterJets[ordinal];
            var valve = topology.IsolationValves[ordinal];
            Assert.Equal($"WJ{ordinal + 1}", waterJet.WaterJetId);
            Assert.Equal($"IV{ordinal + 1}", waterJet.DedicatedIsolationValveId);
            Assert.Equal($"IV{ordinal + 1}", valve.ValveId);
            Assert.Equal($"WJ{ordinal + 1}", valve.ServedWaterJetId);
            Assert.NotEqual(waterJet.InstalledWall, waterJet.TargetWall);
            Assert.Equal(waterJet.InstalledRegion, waterJet.TargetRegion);
            Assert.Equal(
                waterJet.PlacementKind == WaterJetPlacementKind.UNRESOLVED,
                waterJet.PlacementAnchors.Count == 0);
        }
    }

    [Fact]
    public async Task Sensor_Views_Carry_The_Assigned_Devices_And_Target_Coverage_Agreement()
    {
        var runtime = SimulatorRuntime.Create(Options(), new TestClock(Instant));

        try
        {
            Assert.Equal(CanonicalSensorMap.SensorLocations, runtime.State.Sensors.Count);
            foreach (var sensor in runtime.State.Sensors)
            {
                var view = ViewOf(sensor);
                Assert.Equal(sensor.AssignedWaterJetId, view.AssignedWaterJetId);
                Assert.Equal(sensor.AssignedIsolationValveId, view.AssignedIsolationValveId);

                var waterJet = WaterJetTopologyCatalog.FindWaterJet(view.AssignedWaterJetId);
                Assert.NotNull(waterJet);
                Assert.Equal(sensor.Wall, waterJet.TargetWall);
                Assert.Equal(
                    WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow),
                    waterJet.TargetRegion);
            }

            // The binding legacy example: rear-lower Sensors are cleaned by WJ1/IV1,
            // which installs on the front-lower wall (assignment is responsibility,
            // never location).
            var rearLowerSensors = runtime.State.Sensors
                .Where(s => s.Wall == Wall.REAR && s.LogicalRow == 5)
                .ToArray();
            Assert.True(rearLowerSensors.Length > 0);
            Assert.All(rearLowerSensors, s =>
            {
                Assert.Equal("WJ1", s.AssignedWaterJetId);
                Assert.Equal("IV1", s.AssignedIsolationValveId);
            });

            // The composed snapshot itself carries the topology collections.
            var snapshot = runtime.ProjectSnapshot();
            var waterJetCount = snapshot.WaterJets.Count;
            var isolationValveCount = snapshot.IsolationValves.Count;
            Assert.Equal(8, waterJetCount);
            Assert.Equal(8, isolationValveCount);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public void Route_Registry_Is_Read_Only_With_No_Write_Route_Constant()
    {
        var constants = typeof(ApiRoutes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        var routeCount = constants.Length;
        Assert.Equal(6, routeCount);
        Assert.Equal(
            [ApiRoutes.HealthLive, ApiRoutes.HealthReady, ApiRoutes.Snapshot, ApiRoutes.Runtime, ApiRoutes.Deltas, ApiRoutes.Inspector],
            constants);

        // No route constant declares a write surface: none carries a method verb,
        // and the host maps every route with GET only (asserted by the composed
        // surface test + Owner-local HTTP checks).
        Assert.All(constants, route => Assert.True(route.StartsWith('/'), route));
    }

    private static RuntimeHostOptions Options() => new()
    {
        Profile = DeviceProfile.SIMULATOR,
        Port = 5181,
        SyntheticSeed = 20261007UL,
        TickIntervalMilliseconds = 150,
        StateHistoryCapacity = 32,
        DeltaHistoryCapacity = 32,
    };
}
