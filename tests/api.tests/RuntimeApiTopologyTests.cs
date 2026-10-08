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
/// Read-only API projections of the approved equipment topology (Stage 0.3A-3
/// Checkpoint C): the status counts (108 / 106 / 212 / 2 gaps / 8 WJ / 8 IV),
/// the two NON_SENSOR_GAP references I7 and I16 (never presented as Water Jet
/// identities), the paired WJ/IV topology views with installed position separate
/// from target coverage and acquisition explicitly DEFERRED, and per-Sensor
/// assigned device references. No control surface is asserted to exist: the
/// route registry carries the read-only routes only, every host mapping is a
/// GET, and no write/command/dispatch route constant exists to map.
/// Everything here is synthetic; nothing commands or actuates anything.
/// </summary>
public sealed class RuntimeApiTopologyTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] ExpectedGapLabels = ["I7", "I16"];

    private static readonly string[] ExpectedWaterJetIds =
        ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static RuntimeSensorView ViewOf(SensorPresentationState sensor) => RuntimeSensorView.From(sensor);

    [Fact]
    public void Status_Counts_Carry_The_Protected_Baseline_And_The_Topology_Totals()
    {
        var counts = RuntimeStatusSensorCounts.Canonical();

        Assert.Equal(108, counts.LogicalSlots.Count);
        Assert.Equal(106, counts.SensorLocations.Count);
        Assert.Equal(212, counts.ThermocoupleChannels.Count);

        var nonSensorGapCount = counts.NonSensorGaps.Count;
        var waterJetCount = counts.WaterJets.Count;
        var isolationValveCount = counts.IsolationValves.Count;
        Assert.Equal(2, nonSensorGapCount);
        Assert.Equal(8, waterJetCount);
        Assert.Equal(8, isolationValveCount);
    }

    [Fact]
    public void Gap_References_Are_I7_Anchoring_WJ3_And_I16_Anchoring_WJ1_In_Canonical_Order()
    {
        var counts = RuntimeStatusSensorCounts.Canonical();

        var gapSlotCount = counts.NonSensorGapSlots.Count;
        Assert.Equal(2, gapSlotCount);
        Assert.Equal(ExpectedGapLabels, counts.NonSensorGapSlots.Select(g => g.LogicalLabel).ToArray());
        Assert.Equal(("I7", "WJ3", 5, 7),
            (counts.NonSensorGapSlots[0].LogicalLabel,
             counts.NonSensorGapSlots[0].GapAnchorForWaterJetId,
             counts.NonSensorGapSlots[0].LogicalRow,
             counts.NonSensorGapSlots[0].LogicalColumn));
        Assert.Equal(("I16", "WJ1", 5, 16),
            (counts.NonSensorGapSlots[1].LogicalLabel,
             counts.NonSensorGapSlots[1].GapAnchorForWaterJetId,
             counts.NonSensorGapSlots[1].LogicalRow,
             counts.NonSensorGapSlots[1].LogicalColumn));
    }

    [Fact]
    public void Gap_Positions_Are_Never_Presented_As_Water_Jet_Identities()
    {
        var topology = RuntimeEquipmentTopology.Canonical();

        var waterJetIds = topology.WaterJets.Select(w => w.WaterJetId).ToArray();
        Assert.Equal(ExpectedWaterJetIds, waterJetIds);
        var gapLabels = RuntimeStatusSensorCounts.Canonical().NonSensorGapSlots
            .Select(g => g.LogicalLabel)
            .ToArray();
        Assert.All(gapLabels, label => Assert.DoesNotContain(label, waterJetIds));
        Assert.All(topology.WaterJets, w => Assert.DoesNotContain(w.WaterJetId, gapLabels));
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
            Assert.True(waterJet.PlacementAnchors.Count > 0);
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
