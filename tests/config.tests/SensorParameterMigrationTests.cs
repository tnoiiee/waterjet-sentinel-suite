using System.Globalization;
using System.Text.Json;
using Wjss.Contracts;
using Wjss.Domain;
using Xunit;

namespace Wjss.Config.Tests;

/// <summary>
/// Stage 0.3A-3 Checkpoint B invariant tests for the legacy sensor-parameter migration
/// (ADR-0017; approved planned tests T1–T20), corrected to the Owner critical topology
/// correction (Stage 0.4C-K3): 108 Sensors, 216 structural TC sides, I7 and I16 normal Product
/// Sensors in ordinal position, no NON_SENSOR_GAP vocabulary; and to the Owner-approved
/// canonical Equipment topology adoption (2026-10-25): WJ1–WJ4 installed / targeting the
/// UPPER tier,
/// WJ5–WJ8 the LOWER tier, fine-grained placement UNRESOLVED with no anchors for all
/// eight Water Jets; and to the Owner-authoritative logical-row tier boundary (2026-10-29):
/// rows 1-3 (G+2xx, G+1xx and G) are UPPER and rows 4-6 (H, I and J) are LOWER, giving canonical
/// default assignments 12 / 15 / 12 / 15 / 15 / 15 / 12 / 12.
/// All fixtures are synthetic public-safe rows built by <see cref="SyntheticSensorParameterCsv"/>;
/// the Owner CSV never enters Git.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local test run is the arbiter).
/// </summary>
public sealed class SensorParameterMigrationTests
{
    // ---- deterministic default-fixture expectations ----
    private const int ExpectedTimezoneWarnings = 54; // even scanOrder values 2..108
    private const int ExpectedUnitWarnings = 36;     // scanOrder divisible by 3 (3..108)
    private const int ExpectedDisabledSensors = 6;   // scanOrder divisible by 17 (17..102)

    // Semantically distinct expected sequences (CA1861: never inline constant arrays as
    // arguments; never consolidate arrays with different meanings).
    private static readonly string[] ExpectedWaterJetIdOrder =
        ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static readonly string[] ExpectedIsolationValveIdOrder =
        ["IV1", "IV2", "IV3", "IV4", "IV5", "IV6", "IV7", "IV8"];

    private static readonly string[] ExpectedCorrectedSensorIds = ["I7", "I16"];

    private static readonly string[] ExpectedBooleanPropertyNames = ["Enabled"];

    private static readonly string[] ExpectedLogicalPositionKindNames = ["SENSOR"];

    private static SensorParameterImportOutcome Import(string csv) =>
        SensorParameterCsvImporter.Import(csv);

    private static SensorParameterMigrationResult AcceptedResult(string csv)
    {
        var outcome = Import(csv);
        Assert.True(outcome.Accepted, $"expected acceptance, got {outcome.Refusal?.Code}: {outcome.Refusal?.Detail}");
        Assert.NotNull(outcome.Result);
        return outcome.Result!;
    }

    private static string RefusalCodeOf(string csv)
    {
        var outcome = Import(csv);
        Assert.False(outcome.Accepted, "expected the import to refuse");
        Assert.NotNull(outcome.Refusal);
        Assert.Null(outcome.Result); // atomic: a refusal never carries a partial result
        return outcome.Refusal!.Code;
    }

    // ---- T1 ------------------------------------------------------------------

    [Fact]
    public void Accepted_Default_Fixture_Satisfies_All_Protected_Counts()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(108, result.LogicalPositions.Count);
        Assert.Equal(108, result.Sensors.Count);
        Assert.Equal(8, result.WaterJets.Count);
        Assert.Equal(8, result.IsolationValves.Count);
        Assert.Equal(216, result.Sensors.Count * 2); // structural TC sides: two per Sensor
        Assert.Equal(24, result.Sensors.Count(s => s.Wall == Wall.LEFT));
        Assert.Equal(30, result.Sensors.Count(s => s.Wall == Wall.REAR));
        Assert.Equal(24, result.Sensors.Count(s => s.Wall == Wall.RIGHT));
        Assert.Equal(30, result.Sensors.Count(s => s.Wall == Wall.FRONT));
        Assert.Null(TopologyValidator.Validate(result));
    }

    // ---- T2 (Owner critical topology correction) ---------------------------------

    [Fact]
    public void I7_And_I16_Are_Normal_Sensors_With_No_Gap_Position_And_No_Gap_Property()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.All(result.LogicalPositions, p => Assert.Equal(LogicalPositionKind.SENSOR, p.PositionKind));
        Assert.Equal(
            ExpectedCorrectedSensorIds,
            result.Sensors.Where(s => s.SensorId is "I7" or "I16").Select(s => s.SensorId).ToArray());

        var i7 = result.Sensors.Single(s => s.SensorId == "I7");
        Assert.Equal(79, i7.ScanOrder);
        Assert.Equal(Wall.REAR, i7.Wall);
        Assert.Equal(5, i7.LogicalRow);
        Assert.Equal(7, i7.LogicalColumn);
        Assert.Equal(3, i7.WallColumn);
        Assert.True(i7.Enabled);
        Assert.Equal(15 + (79 % 10), i7.DiffLowerBound);
        Assert.Equal("WJ5", i7.AssignedWaterJetId);
        Assert.Equal("IV5", i7.AssignedIsolationValveId);

        var i16 = result.Sensors.Single(s => s.SensorId == "I16");
        Assert.Equal(88, i16.ScanOrder);
        Assert.Equal(Wall.FRONT, i16.Wall);
        Assert.Equal(16, i16.LogicalColumn);
        Assert.Equal(3, i16.WallColumn);
        Assert.True(i16.Enabled);
        Assert.Equal("WJ6", i16.AssignedWaterJetId);
        Assert.Equal("IV6", i16.AssignedIsolationValveId);

        // The gap-anchor property no longer exists on any canonical record.
        Assert.DoesNotContain(typeof(LogicalPositionRecord).GetProperties(), p => p.Name == "GapAnchorForWaterJetId");
        Assert.DoesNotContain(typeof(WallMapSlot).GetProperties(), p => p.Name == "GapAnchorForWaterJetId");
    }

    // ---- T3 ------------------------------------------------------------------

    [Fact]
    public void ScanOrder_Is_Dense_1_To_108_In_Ordinal_Order_And_OrderTotal_Stays_0_To_107()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(
            Enumerable.Range(1, 108).ToArray(),
            result.Sensors.Select(s => s.ScanOrder).ToArray());

        Assert.Equal(
            Enumerable.Range(0, 108).ToArray(),
            result.LogicalPositions.Select(p => p.OrderTotal).ToArray());

        Assert.Equal(
            CanonicalSensorMap.SensorSequence.ToArray(),
            result.Sensors.Select(s => s.SensorId).ToArray());

        // I7 and I16 sit in their normal ordinal position: I6, I7, I8 and I15, I16, I17 are consecutive.
        var i6 = result.Sensors.Single(s => s.SensorId == "I6");
        var i7 = result.Sensors.Single(s => s.SensorId == "I7");
        var i8 = result.Sensors.Single(s => s.SensorId == "I8");
        Assert.Equal(i6.ScanOrder + 1, i7.ScanOrder);
        Assert.Equal(i7.ScanOrder + 1, i8.ScanOrder);
        var i15 = result.Sensors.Single(s => s.SensorId == "I15");
        var i16 = result.Sensors.Single(s => s.SensorId == "I16");
        var i17 = result.Sensors.Single(s => s.SensorId == "I17");
        Assert.Equal(i15.ScanOrder + 1, i16.ScanOrder);
        Assert.Equal(i16.ScanOrder + 1, i17.ScanOrder);
    }

    // ---- T4 / T5 ---------------------------------------------------------------

    [Fact]
    public void Water_Jet_And_Valve_Counts_And_One_To_One_Pairing()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(
            ExpectedWaterJetIdOrder,
            result.WaterJets.Select(w => w.WaterJetId).ToArray());
        Assert.Equal(
            ExpectedIsolationValveIdOrder,
            result.IsolationValves.Select(v => v.ValveId).ToArray());

        foreach (var n in Enumerable.Range(1, 8))
        {
            var waterJet = result.WaterJets.Single(w => w.WaterJetId == $"WJ{n}");
            Assert.Equal($"IV{n}", waterJet.DedicatedIsolationValveId);
            var valve = result.IsolationValves.Single(v => v.ValveId == $"IV{n}");
            Assert.Equal($"WJ{n}", valve.ServedWaterJetId);
        }
    }

    // ---- T6 ------------------------------------------------------------------

    private static readonly (string Id, Wall InstalledWall, Region InstalledRegion,
        WaterJetPlacementKind Kind, string[] Anchors, Wall TargetWall, Region TargetRegion, string Valve)[]
        ApprovedTopology =
        [
            ("WJ1", Wall.LEFT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, [], Wall.RIGHT, Region.UPPER, "IV1"),
            ("WJ2", Wall.REAR, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, [], Wall.FRONT, Region.UPPER, "IV2"),
            ("WJ3", Wall.RIGHT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, [], Wall.LEFT, Region.UPPER, "IV3"),
            ("WJ4", Wall.FRONT, Region.UPPER, WaterJetPlacementKind.UNRESOLVED, [], Wall.REAR, Region.UPPER, "IV4"),
            ("WJ5", Wall.FRONT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, [], Wall.REAR, Region.LOWER, "IV5"),
            ("WJ6", Wall.REAR, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, [], Wall.FRONT, Region.LOWER, "IV6"),
            ("WJ7", Wall.LEFT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, [], Wall.RIGHT, Region.LOWER, "IV7"),
            ("WJ8", Wall.RIGHT, Region.LOWER, WaterJetPlacementKind.UNRESOLVED, [], Wall.LEFT, Region.LOWER, "IV8"),
        ];

    [Fact]
    public void Approved_Topology_Table_Is_Reproduced_Exactly()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(ApprovedTopology.Length, result.WaterJets.Count);
        foreach (var expected in ApprovedTopology)
        {
            var actual = result.WaterJets.Single(w => w.WaterJetId == expected.Id);
            Assert.Equal(expected.InstalledWall, actual.InstalledWall);
            Assert.Equal(expected.InstalledRegion, actual.InstalledRegion);
            Assert.Equal(expected.Kind, actual.PlacementKind);
            Assert.Equal(expected.Anchors, actual.PlacementAnchors.ToArray());
            Assert.Equal(expected.TargetWall, actual.TargetWall);
            Assert.Equal(expected.TargetRegion, actual.TargetRegion);
            Assert.Equal(expected.Valve, actual.DedicatedIsolationValveId);
        }
    }

    // ---- T7 ------------------------------------------------------------------

    [Fact]
    public void Device_Ordinal_Maps_Directly_Including_Rear_Lower_To_WJ5()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        foreach (var sensor in result.Sensors)
        {
            var region = WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow);
            var expectedOrdinal = SyntheticSensorParameterCsv.DeviceOrdinalForTarget(sensor.Wall, region);
            Assert.Equal($"WJ{expectedOrdinal}", sensor.AssignedWaterJetId);
            Assert.Equal($"IV{expectedOrdinal}", sensor.AssignedIsolationValveId);
        }

        // Owner worked example (canonical topology): Rear-lower sensors carry device
        // ordinal 5 → WJ5, which is installed at FRONT lower and sprays across to REAR
        // lower, paired valve IV5.
        var i6 = result.Sensors.Single(s => s.SensorId == "I6");
        Assert.Equal(Wall.REAR, i6.Wall);
        Assert.Equal("WJ5", i6.AssignedWaterJetId);
        Assert.Equal("IV5", i6.AssignedIsolationValveId);
        var wj5 = result.WaterJets.Single(w => w.WaterJetId == "WJ5");
        Assert.Equal(Wall.FRONT, wj5.InstalledWall);
        Assert.Equal(Region.LOWER, wj5.InstalledRegion);
        Assert.Equal(Wall.REAR, wj5.TargetWall);
    }

    [Fact]
    public void Canonical_Default_Assignment_Counts_Per_WaterJet_Are_12_15_12_15_15_15_12_12()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        // Canonical zone populations by physical Sensor wall / tier under the Owner-authoritative
        // logical-row tier boundary (2026-10-29: rows 1-3, G+2xx / G+1xx / G, are UPPER and rows
        // 4-6, H / I / J, are LOWER): Right/Upper 12, Front/Upper 15, Left/Upper 12, Rear/Upper 15,
        // Rear/Lower 15, Front/Lower 15, Right/Lower 12, Left/Lower 12 — assigned to WJ1..WJ8
        // respectively (IVn paired). Each tier holds 54 of the 108 Sensors.
        var expectedCounts = new[] { 12, 15, 12, 15, 15, 15, 12, 12 };
        for (var n = 1; n <= 8; n++)
        {
            Assert.Equal(
                expectedCounts[n - 1],
                result.Sensors.Count(s => s.AssignedWaterJetId == $"WJ{n}" && s.AssignedIsolationValveId == $"IV{n}"));
        }

        Assert.Equal(108, result.Sensors.Count);
        Assert.Equal(
            result.Sensors.Count(s => s.LogicalRow is >= 1 and <= 3),
            result.Sensors.Count(s => WaterJetTopologyCatalog.RegionForLogicalRow(s.LogicalRow) == Region.UPPER));
        Assert.Equal(54, result.Sensors.Count(s => s.LogicalRow is >= 1 and <= 3));
        Assert.Equal(54, result.Sensors.Count(s => s.LogicalRow is >= 4 and <= 6));
    }

    [Fact]
    public void Canonical_G_Row_Cannon_Assignments_Are_Accepted_Under_Upper_Tier_Coverage()
    {
        // The importer derives each record's tier from its logical row through the single shared
        // WaterJetTopologyCatalog.RegionForLogicalRow derivation. G1-G18 are logical row 3, so their
        // cannon must name an UPPER-tier Water Jet (WJ1-WJ4); the synthetic fixture supplies exactly
        // that, and the whole 108-record file is accepted with no refusal.
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var expected = new (string SensorId, Wall Wall, string WaterJetId)[]
        {
            ("G1", Wall.LEFT, "WJ3"),
            ("G5", Wall.REAR, "WJ4"),
            ("G10", Wall.RIGHT, "WJ1"),
            ("G14", Wall.FRONT, "WJ2"),
        };

        foreach (var (sensorId, wall, waterJetId) in expected)
        {
            var sensor = result.Sensors.Single(s => s.SensorId == sensorId);
            Assert.Equal(3, sensor.LogicalRow);
            Assert.Equal(wall, sensor.Wall);
            Assert.Equal(Region.UPPER, WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow));
            Assert.Equal(waterJetId, sensor.AssignedWaterJetId);
            Assert.Equal($"IV{waterJetId[2..]}", sensor.AssignedIsolationValveId);
        }

        // Every G-row Sensor is covered by an UPPER-tier Water Jet, and every H/I/J Sensor by a
        // LOWER-tier one: the tier correction is exercised across the whole matrix, not one row.
        Assert.All(
            result.Sensors.Where(s => s.SensorId.StartsWith('G')),
            sensor => Assert.Equal(Region.UPPER, WaterJetTopologyCatalog.FindWaterJet(sensor.AssignedWaterJetId)!.TargetRegion));
        Assert.All(
            result.Sensors.Where(s => s.SensorId.StartsWith('H') || s.SensorId.StartsWith('I') || s.SensorId.StartsWith('J')),
            sensor => Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.FindWaterJet(sensor.AssignedWaterJetId)!.TargetRegion));
    }

    [Fact]
    public void A_Genuine_Target_Tier_Contradiction_Is_Still_Refused()
    {
        // Under the corrected boundary a G-row Sensor is UPPER, so a LOWER-tier cannon contradicts
        // its own logical row: the importer must refuse rather than silently re-map the cannon. This
        // is a genuine contradiction, not a stale expectation (the same cell with an UPPER cannon is
        // accepted by Canonical_G_Row_Cannon_Assignments_Are_Accepted_Under_Upper_Tier_Coverage).
        var row = SyntheticSensorParameterCsv.RowOfLogicalId("G1");
        Assert.Equal("3", SyntheticSensorParameterCsv.BuildRows()[row + 1][5]);
        Assert.Equal(
            MigrationRefusalCodes.TopoTargetWallContradiction,
            RefusalCodeOf(SyntheticSensorParameterCsv.WithCell(row, 5, "8")));
    }

    // ---- T8 ------------------------------------------------------------------

    [Fact]
    public void Every_Sensor_Has_One_Resolved_WaterJet_And_Derived_Valve()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.All(result.Sensors, sensor =>
        {
            var waterJet = WaterJetTopologyCatalog.FindWaterJet(sensor.AssignedWaterJetId);
            Assert.NotNull(waterJet);
            Assert.Equal(waterJet!.DedicatedIsolationValveId, sensor.AssignedIsolationValveId);
        });
    }

    // ---- T9 ------------------------------------------------------------------

    [Fact]
    public void Equipment_Identities_Never_Appear_As_Sensor_Identities()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var sensorIds = result.Sensors.Select(s => s.SensorId).ToArray();
        Assert.Equal(sensorIds.Length, sensorIds.Distinct().Count());
        foreach (var equipmentId in result.WaterJets.Select(w => w.WaterJetId)
                     .Concat(result.IsolationValves.Select(v => v.ValveId)))
        {
            Assert.DoesNotContain(equipmentId, sensorIds);
        }

        Assert.DoesNotContain(sensorIds, id => id.StartsWith("WJ", StringComparison.Ordinal));
        Assert.DoesNotContain(sensorIds, id => id.StartsWith("IV", StringComparison.Ordinal));
    }

    // ---- T10 -----------------------------------------------------------------

    [Fact]
    public void Import_Is_Deterministic()
    {
        var csv = SyntheticSensorParameterCsv.Build();
        var first = AcceptedResult(csv);
        var second = AcceptedResult(csv);

        // Records give value equality per element; lists are compared as serialized
        // documents so order and content must match exactly.
        var firstJson = JsonSerializer.Serialize(first);
        var secondJson = JsonSerializer.Serialize(second);
        Assert.Equal(firstJson, secondJson);

        Assert.Equal(first.Warnings.Count, second.Warnings.Count);
    }

    // ---- T11: refusal codes (fail-closed, atomic) -----------------------------

    [Fact]
    public void Missing_Row_Refuses_With_Position_Count()
    {
        var csv = SyntheticSensorParameterCsv.WithoutRow(SyntheticSensorParameterCsv.RowOfLogicalId("J18"));
        Assert.Equal(MigrationRefusalCodes.TopoPositionCount, RefusalCodeOf(csv));
    }

    [Fact]
    public void Duplicate_Record_Id_Refuses()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 0, "SYN-REC-001");
        Assert.Equal(MigrationRefusalCodes.TopoDuplicateId, RefusalCodeOf(csv));
    }

    [Fact]
    public void Out_Of_Range_Order_Total_Refuses()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 4, "108");
        Assert.Equal(MigrationRefusalCodes.TopoDuplicateOrder, RefusalCodeOf(csv));
    }

    [Fact]
    public void Duplicate_Order_Total_Refuses()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(1, 4, "0");
        Assert.Equal(MigrationRefusalCodes.TopoDuplicateOrder, RefusalCodeOf(csv));
    }

    [Fact]
    public void Mismatched_Logical_Label_Refuses()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 1, "G+202");
        Assert.Equal(MigrationRefusalCodes.MigrationLogicalLabelMismatch, RefusalCodeOf(csv));
    }

    [Fact]
    public void Wall_Disagreeing_With_Derived_Column_Refuses()
    {
        // G+201 is logical column 1 → LEFT; claiming REAR contradicts the derived position.
        var csv = SyntheticSensorParameterCsv.WithCell(0, 2, "REAR");
        Assert.Equal(MigrationRefusalCodes.MigrationLogicalLabelMismatch, RefusalCodeOf(csv));
    }

    [Fact]
    public void Unknown_Wall_Token_Refuses_With_Field_Token()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 2, "LEFTX");
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(csv));
    }

    [Fact]
    public void Unknown_Enable_Token_Refuses_With_Field_Token()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 10, "MAYBE");
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(csv));
    }

    [Fact]
    public void Out_Of_Range_Device_Ordinal_Refuses()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 5, "9");
        Assert.Equal(MigrationRefusalCodes.MigrationCannonRange, RefusalCodeOf(csv));
    }

    [Fact]
    public void Non_Integer_Device_Ordinal_Refuses_With_Field_Token()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 5, "ONE");
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(csv));
    }

    [Fact]
    public void Inverted_Dirty_Score_Bounds_Refuse()
    {
        var csv = SyntheticSensorParameterCsv.WithCell(0, 7, "999");
        Assert.Equal(MigrationRefusalCodes.MigrationBoundsOrder, RefusalCodeOf(csv));
    }

    [Fact]
    public void Wrong_Header_Refuses_With_Field_Token()
    {
        var rows = SyntheticSensorParameterCsv.BuildRows();
        rows[0][3] = "order_wal";
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(SyntheticSensorParameterCsv.ToCsv(rows)));
    }

    [Fact]
    public void Empty_Input_Refuses_With_Field_Token()
    {
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(string.Empty));
    }

    // ---- T12 -----------------------------------------------------------------

    [Fact]
    public void Legacy_Id_Is_Provenance_Only_Never_An_Identity()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var recordIds = result.LogicalPositions.Select(p => p.LegacyRecordId).ToArray();
        Assert.Equal(recordIds.Length, recordIds.Distinct().Count());
        Assert.All(recordIds, id => Assert.Matches("^SYN-REC-\\d{3}$", id));

        // Sensor identities are the logical labels, never the record identifiers.
        Assert.All(result.Sensors, s =>
        {
            Assert.DoesNotMatch("^SYN-REC-", s.SensorId);
            Assert.Matches("^SYN-REC-\\d{3}$", s.LegacyRecordId);
        });

        // Every position's record id resolves, and the sensor list carries the same set.
        var sensorRecordIds = result.Sensors.Select(s => s.LegacyRecordId).ToHashSet(StringComparer.Ordinal);
        var sensorPositionIds = result.LogicalPositions
            .Where(p => p.PositionKind == LogicalPositionKind.SENSOR)
            .Select(p => p.LegacyRecordId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(sensorPositionIds, sensorRecordIds);
    }

    // ---- T13 -----------------------------------------------------------------

    [Fact]
    public void Sensorname_Becomes_LogicalId_And_SensorId()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        foreach (var position in result.LogicalPositions)
        {
            var expectedLabel = CanonicalSensorMap.SensorIdFor(position.LogicalRow, position.LogicalColumn);
            Assert.Equal(expectedLabel, position.LogicalId);
        }

        Assert.All(result.Sensors, s =>
        {
            var position = result.LogicalPositions.Single(p => p.LegacyRecordId == s.LegacyRecordId);
            Assert.Equal(position.LogicalId, s.SensorId);
        });

        // The importer's canonical order is numeric structured order by order_total — I7
        // (order_total 78, scanOrder 79) before I16 (order_total 87, scanOrder 88) — never
        // lexicographic id order (which would put I16 first). Both are Sensor rows.
        var corrected = result.LogicalPositions
            .Where(p => p.LogicalId is "I7" or "I16")
            .ToArray();
        Assert.Equal(ExpectedCorrectedSensorIds, corrected.Select(p => p.LogicalId).ToArray());
        Assert.Equal(78, corrected[0].OrderTotal);
        Assert.Equal(87, corrected[1].OrderTotal);
        Assert.Equal(79, result.Sensors.Single(s => s.SensorId == "I7").ScanOrder);
        Assert.Equal(88, result.Sensors.Single(s => s.SensorId == "I16").ScanOrder);
    }

    // ---- T15 -----------------------------------------------------------------

    [Fact]
    public void Offset_Free_Timestamps_Remain_Raw_Never_Utc()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var rawTimestamps = result.Sensors
            .Where(s => s.LastCleanTimestampRaw is not null)
            .ToArray();
        Assert.Equal(ExpectedTimezoneWarnings, rawTimestamps.Length);
        Assert.All(rawTimestamps, s => Assert.Equal(SyntheticSensorParameterCsv.SynLastCleanTimestamp, s.LastCleanTimestampRaw));

        // Exactly one timezone warning per raw timestamp, and no placeholder warning
        // (the synthetic timestamps parse under the advisory formats).
        var timezoneWarnings = result.Warnings
            .Where(w => w.Code == MigrationRefusalCodes.MigrationTimezoneUnknown)
            .ToArray();
        Assert.Equal(ExpectedTimezoneWarnings, timezoneWarnings.Length);
        Assert.Equal(0, result.Warnings.Count(w => w.Code == MigrationRefusalCodes.MigrationPlaceholderValue));

        // No record ever grew a UTC interpretation: the raw string is the only form.
        Assert.DoesNotContain("Z", rawTimestamps.Select(s => s.LastCleanTimestampRaw!).ToList());
    }

    // ---- T16 -----------------------------------------------------------------

    [Fact]
    public void Unknown_Unit_Durations_Remain_Raw()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var rawDurations = result.Sensors
            .Where(s => s.MinTimeAllowAddToQueueRaw is not null)
            .ToArray();
        Assert.Equal(ExpectedUnitWarnings, rawDurations.Length);
        Assert.All(rawDurations, s => Assert.Equal(SyntheticSensorParameterCsv.SynMinTimeValue, s.MinTimeAllowAddToQueueRaw));

        var unitWarnings = result.Warnings
            .Where(w => w.Code == MigrationRefusalCodes.MigrationUnitUnverified)
            .ToArray();
        Assert.Equal(ExpectedUnitWarnings, unitWarnings.Length);
        Assert.Equal(108 - ExpectedUnitWarnings, result.Sensors.Count(s => s.MinTimeAllowAddToQueueRaw is null));

        // DEFER fields are carried raw with no canonical attribute anywhere.
        Assert.All(result.Sensors, s =>
        {
            Assert.Null(s.MaxTimeAllowAddToQueueRaw);
            Assert.Null(s.MaxTimeEnableRaw);
            Assert.Null(s.LatestUpdateParamsTimestampRaw);
        });
    }

    // ---- T17 (semantic replacement: output + contract shape, no source text) --

    [Fact]
    public void Migration_Output_Preserves_Raw_Values_And_Emits_No_Unapproved_Derivations()
    {
        var rows = SyntheticSensorParameterCsv.BuildRows();
        var result = AcceptedResult(SyntheticSensorParameterCsv.ToCsv(rows));

        // Every approved import is preserved verbatim from its legacy row, keyed by the
        // legacy record identifier provenance: the raw threshold value, the raw cleaning
        // count, the raw offset-free last-clean string, and the raw unknown-unit
        // minimum-time value.
        var rowsByRecordId = rows.Skip(1).ToDictionary(row => row[0], row => row);
        Assert.All(result.Sensors, sensor =>
        {
            var row = rowsByRecordId[sensor.LegacyRecordId];
            Assert.Equal(double.Parse(row[8], CultureInfo.InvariantCulture), sensor.DirtyScoreThreshold);
            Assert.Equal(int.Parse(row[9], CultureInfo.InvariantCulture), sensor.CleaningCount);
            Assert.Equal(row[11].Length == 0 ? null : row[11], sensor.LastCleanTimestampRaw);
            Assert.Equal(row[12].Length == 0 ? null : row[12], sensor.MinTimeAllowAddToQueueRaw);
        });

        // The source timezone remains UNKNOWN/deferred: every preserved last-clean value
        // is still the offset-free legacy string (no UTC variant, no offset suffix), and
        // each one raised exactly the MIGRATION_TIMEZONE_UNKNOWN warning.
        Assert.All(result.Sensors, sensor =>
        {
            if (sensor.LastCleanTimestampRaw is not null)
            {
                Assert.False(
                    sensor.LastCleanTimestampRaw.EndsWith('Z'),
                    "the raw last-clean value must remain the offset-free legacy string");
                Assert.False(
                    sensor.LastCleanTimestampRaw.EndsWith("+00:00", StringComparison.Ordinal),
                    "no UTC offset may be invented for the raw last-clean value");
            }
        });
        Assert.Equal(
            ExpectedTimezoneWarnings,
            result.Warnings.Count(w => w.Code == MigrationRefusalCodes.MigrationTimezoneUnknown));

        // Public Contract shape: the ONLY bool the record can carry is Enabled — no
        // unapproved derived boolean (no threshold-gate flag, no verified-history flag)
        // exists — and no canonical DateTimeOffset/DateTime or TimeSpan is invented.
        var booleanProperties = typeof(SensorConfigurationRecord).GetProperties()
            .Where(p => p.PropertyType == typeof(bool))
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedBooleanPropertyNames, booleanProperties);

        // No canonical temporal or duration type is invented anywhere on the record.
        var sensorRecordProperties = typeof(SensorConfigurationRecord).GetProperties();
        Assert.DoesNotContain(sensorRecordProperties, p =>
            p.PropertyType == typeof(DateTimeOffset)
            || p.PropertyType == typeof(DateTimeOffset?)
            || p.PropertyType == typeof(DateTime)
            || p.PropertyType == typeof(DateTime?)
            || p.PropertyType == typeof(TimeSpan)
            || p.PropertyType == typeof(TimeSpan?));
    }

    // ---- T18 -----------------------------------------------------------------

    [Fact]
    public void Deferred_Acquisition_Raws_Are_Captured_As_Provenance_Only()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.All(result.Sensors, s =>
        {
            Assert.NotNull(s.DeferredAcquisition);
            Assert.Equal(SyntheticSensorParameterCsv.SynAcquisitionEndpoint, s.DeferredAcquisition!.AcquisitionEndpointRaw);
            Assert.Equal(SyntheticSensorParameterCsv.SynChannelPair, s.DeferredAcquisition.ChannelPairRaw);
            Assert.Equal(SyntheticSensorParameterCsv.SynAcquisitionBase, s.DeferredAcquisition.AcquisitionBaseAddressRaw);
        });
    }

    // ---- T19 (semantic replacement: imported output + public kinds, no source text)

    [Fact]
    public void Canonical_Output_Contains_Only_Sensor_Positions()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        // Every logical position is a Sensor position in canonical orderTotal order, and
        // every one of them (I7 and I16 included) yields exactly one SensorConfiguration.
        Assert.Equal(108, result.LogicalPositions.Count(p => p.PositionKind == LogicalPositionKind.SENSOR));
        Assert.Single(result.Sensors, s => s.SensorId == "I7");
        Assert.Single(result.Sensors, s => s.SensorId == "I16");

        // The canonical kind vocabulary is exactly SENSOR: the superseded NON_SENSOR_GAP
        // member and the Runtime's former transitional SlotType representation are not part
        // of the canonical output vocabulary.
        Assert.Equal(
            ExpectedLogicalPositionKindNames,
            Enum.GetNames<LogicalPositionKind>());

        // No canonical record carries a property of the former transitional slot
        // vocabulary (the SlotType representation was deleted in Checkpoint C; the
        // check is name-based because the type itself no longer exists).
        var canonicalRecordProperties = new[]
        {
            typeof(LogicalPositionRecord),
            typeof(SensorConfigurationRecord),
            typeof(WaterJetConfiguration),
            typeof(IsolationValveConfiguration),
        }.SelectMany(type => type.GetProperties()).ToArray();
        var transitionalSlotTypeProperties = canonicalRecordProperties
            .Where(p => p.PropertyType.Name == "SlotType")
            .ToArray();
        Assert.Empty(transitionalSlotTypeProperties);

        // No Cannon entity or identity exists anywhere in the imported output.
        var allIdentities = result.LogicalPositions.Select(p => p.LogicalId)
            .Concat(result.Sensors.Select(s => s.SensorId))
            .Concat(result.WaterJets.Select(w => w.WaterJetId))
            .Concat(result.IsolationValves.Select(v => v.ValveId))
            .ToArray();
        Assert.All(allIdentities, id =>
            Assert.False(
                id.Contains("CANN", StringComparison.Ordinal),
                $"no Cannon identity may appear in canonical output, found '{id}'"));
    }


    // ---- T20 (semantic replacement: compiled public types, no source text) ----

    [Fact]
    public void Runtime_Vocabulary_Is_Sensor_Only_With_No_Gap_Or_Transitional_Type_Left()
    {
        // Owner critical topology correction: the canonical position vocabulary is exactly
        // SENSOR; the superseded NON_SENSOR_GAP member, the gap-anchor property and the former
        // transitional CANNON slot vocabulary no longer exist as compiled public surface — no
        // alias connects any of them to the canonical vocabulary.
        var kindNames = Enum.GetNames<LogicalPositionKind>();
        Assert.Single(kindNames);
        Assert.Equal("SENSOR", kindNames[0]);

        Assert.Equal(108, CanonicalSensorMap.SensorLocations);
        Assert.Equal(216, CanonicalSensorMap.ThermocoupleChannelCount);
        Assert.Equal(240, CanonicalSensorMap.PhysicalThermocoupleChannelCount);
        Assert.Equal(24, CanonicalSensorMap.UnassignedThermocoupleChannelCount);
        Assert.Equal(ExpectedCorrectedSensorIds, CanonicalSensorMap.CorrectedSensorIds.ToArray());
        Assert.DoesNotContain(typeof(CanonicalSensorMap).GetMembers(), m => m.Name.Contains("NonSensorGap", StringComparison.Ordinal));

        var wallMapSlotProperties = typeof(WallMapSlot).GetProperties();
        Assert.Contains(wallMapSlotProperties, p => p.Name == "PositionKind");
        var transitionalProperties = wallMapSlotProperties
            .Where(p => p.Name is "SlotType" or "EquipmentId" or "GapAnchorForWaterJetId")
            .ToArray();
        Assert.Empty(transitionalProperties);

        var contractTypes = typeof(CanonicalSensorMap).Assembly.GetTypes();
        var transitionalTypes = contractTypes
            .Where(t => t.Name == "SlotType")
            .ToArray();
        Assert.Empty(transitionalTypes);
    }

    // ---- Owner Sensor Disable ruling: enable token at import ------------------------

    [Fact]
    public void Empty_Enable_Token_Defaults_To_Enabled_With_A_Recorded_Warning()
    {
        var row = SyntheticSensorParameterCsv.RowOfLogicalId("I7");
        var result = AcceptedResult(SyntheticSensorParameterCsv.WithCell(row, 10, string.Empty));

        var i7 = result.Sensors.Single(s => s.SensorId == "I7");
        Assert.True(i7.Enabled);
        var defaulted = result.Warnings.Where(w => w.Code == MigrationRefusalCodes.MigrationEnableDefaulted).ToArray();
        Assert.Single(defaulted);
        Assert.Equal(i7.LegacyRecordId, defaulted[0].LegacyRecordId);
        Assert.Equal("sensor_enable", defaulted[0].Field);
    }

    [Fact]
    public void Explicit_Disabled_Token_On_I16_Imports_As_Disabled_Like_Any_Other_Sensor()
    {
        var row = SyntheticSensorParameterCsv.RowOfLogicalId("I16");
        var result = AcceptedResult(SyntheticSensorParameterCsv.WithCell(row, 10, "DISABLED"));

        Assert.False(result.Sensors.Single(s => s.SensorId == "I16").Enabled);
        Assert.Equal(ExpectedDisabledSensors + 1, result.Sensors.Count(s => !s.Enabled));
        Assert.Equal(108, result.Sensors.Count);
        Assert.Null(TopologyValidator.Validate(result));
    }

    [Fact]
    public void Unknown_Enable_Token_On_I7_Refuses_Like_Any_Other_Sensor()
    {
        var row = SyntheticSensorParameterCsv.RowOfLogicalId("I7");
        Assert.Equal(MigrationRefusalCodes.MigrationFieldToken, RefusalCodeOf(SyntheticSensorParameterCsv.WithCell(row, 10, "MAYBE")));
    }

    [Fact]
    public void Missing_I16_Row_Refuses_With_Position_Count_Not_As_A_Tolerated_Gap()
    {
        var csv = SyntheticSensorParameterCsv.WithoutRow(SyntheticSensorParameterCsv.RowOfLogicalId("I16"));
        Assert.Equal(MigrationRefusalCodes.TopoPositionCount, RefusalCodeOf(csv));
    }

    // ---- warning bookkeeping ---------------------------------------------------

    [Fact]
    public void Warnings_Are_Complete_Deterministic_And_Ordered()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(ExpectedTimezoneWarnings + ExpectedUnitWarnings, result.Warnings.Count);
        Assert.Equal(ExpectedDisabledSensors, result.Sensors.Count(s => !s.Enabled));

        // Deterministic order: (OrderTotal, Code, Field) with ordinal string comparison.
        var keys = result.Warnings
            .Join(result.Sensors, w => w.LegacyRecordId, s => s.LegacyRecordId,
                (w, s) => (Warning: w, OrderTotal: (int?)s.ScanOrder))
            .ToArray();
        Assert.Equal(result.Warnings.Count, keys.Length);
        for (var i = 1; i < keys.Length; i++)
        {
            var previous = keys[i - 1];
            var current = keys[i];
            Assert.True(
                previous.OrderTotal!.Value <= current.OrderTotal!.Value,
                "warnings must be ordered by the sensor's position in the order_total sequence");
        }

        // Every warning references a known legacy record id and field.
        Assert.All(result.Warnings, w =>
        {
            Assert.Matches("^SYN-REC-\\d{3}$", w.LegacyRecordId);
            Assert.False(string.IsNullOrWhiteSpace(w.Field));
            Assert.False(string.IsNullOrWhiteSpace(w.Detail));
        });
    }

    // ---- approved field matrix --------------------------------------------------

    [Fact]
    public void Approved_Field_Matrix_Matches_Importer_Headers_And_Dispositions()
    {
        var matrix = SensorParameterCsvImporter.ApprovedFieldMatrix;
        Assert.Equal(19, matrix.Count);

        // The matrix documents exactly the headers the importer requires (including the
        // fragment-assembled acquisition names).
        var headerRow = SyntheticSensorParameterCsv.BuildRows()[0];
        for (var i = 0; i < 19; i++)
        {
            Assert.Equal(headerRow[i], matrix[i].Field);
        }

        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "id").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "sensorname").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION, matrix.Single(f => f.Field == "wall").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION, matrix.Single(f => f.Field == "cannon").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "order_total").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "threshold_setpoint").Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "cleaning_count").Disposition);
        Assert.Equal(MigrationFieldDisposition.PRESERVE_WITH_WARNING, matrix.Single(f => f.Field == "lastclean_timestamp").Disposition);
        Assert.Equal(MigrationFieldDisposition.PRESERVE_WITH_WARNING, matrix.Single(f => f.Field == "min_time_allowaddtoqueue").Disposition);
        Assert.Equal(MigrationFieldDisposition.DEFER, matrix.Single(f => f.Field == "max_time_allowaddtoqueue").Disposition);
        Assert.Equal(MigrationFieldDisposition.DEFER, matrix.Single(f => f.Field == "max_time_enable").Disposition);
        Assert.Equal(MigrationFieldDisposition.DEFER, matrix.Single(f => f.Field == SyntheticSensorParameterCsv.IpHeader).Disposition);
        Assert.Equal(MigrationFieldDisposition.DEFER, matrix.Single(f => f.Field == "channel_pair").Disposition);
        Assert.Equal(MigrationFieldDisposition.DEFER, matrix.Single(f => f.Field == SyntheticSensorParameterCsv.BaseAddressHeader).Disposition);
        Assert.Equal(MigrationFieldDisposition.IMPORT, matrix.Single(f => f.Field == "latest_updateparams_timestamp").Disposition);
    }
}

/// <summary>
/// Validator-level refusal tests: each structural invariant is pinned by mutating an
/// otherwise-accepted synthetic result and expecting exactly one fail-closed refusal.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public sealed class TopologyValidatorTests
{
    private static readonly string[] ExpectedUnresolvedPlacementWaterJetIds =
        ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static SensorParameterMigrationResult AcceptedResult() =>
        SensorParameterCsvImporter.Import(SyntheticSensorParameterCsv.Build()).Result!;

    private static string RefusalCodeOf(SensorParameterMigrationResult result)
    {
        var refusal = TopologyValidator.Validate(result);
        Assert.NotNull(refusal);
        return refusal.Code;
    }

    [Fact]
    public void Validator_Uses_The_Corrected_Tier_Derivation_For_A_G_Row_Sensor()
    {
        // G1 is logical row 3 and therefore UPPER under the Owner-authoritative boundary. The
        // validator recomputes the tier from the logical row through the shared derivation, so a
        // LOWER-tier assignment contradicts it. Under the revoked rows-3-6-are-LOWER mapping this
        // exact mutation was ACCEPTED, which is why it is the discriminating case.
        var accepted = AcceptedResult();
        var lowerTierMutated = accepted with
        {
            Sensors = accepted.Sensors
                .Select(s => s.SensorId == "G1"
                    ? s with { AssignedWaterJetId = "WJ8", AssignedIsolationValveId = "IV8" }
                    : s)
                .ToArray(),
        };

        Assert.Equal(3, accepted.Sensors.Single(s => s.SensorId == "G1").LogicalRow);
        Assert.Equal("WJ3", accepted.Sensors.Single(s => s.SensorId == "G1").AssignedWaterJetId);
        Assert.Equal(MigrationRefusalCodes.TopoTargetWallContradiction, RefusalCodeOf(lowerTierMutated));
    }

    [Fact]
    public void Accepted_Result_Passes_Validation()
    {
        Assert.Null(TopologyValidator.Validate(AcceptedResult()));
    }

    [Fact]
    public void Dropping_A_Sensor_Refuses_With_Channel_Total()
    {
        var mutated = AcceptedResult() with { Sensors = AcceptedResult().Sensors.Skip(1).ToArray() };
        Assert.Equal(MigrationRefusalCodes.TopoChannelCount, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Breaking_ScanOrder_Density_Refuses_With_Sensor_Count()
    {
        var mutated = AcceptedResult() with
        {
            Sensors = AcceptedResult().Sensors
                .Select((s, i) => i == 0 ? s with { ScanOrder = 2 } : s)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoSensorCount, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Dropping_A_Logical_Position_Refuses_With_Position_Count()
    {
        var mutated = AcceptedResult() with { LogicalPositions = AcceptedResult().LogicalPositions.Skip(1).ToArray() };
        Assert.Equal(MigrationRefusalCodes.TopoPositionCount, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Dropping_A_Valve_Refuses_With_Valve_Count()
    {
        var mutated = AcceptedResult() with { IsolationValves = AcceptedResult().IsolationValves.Skip(1).ToArray() };
        Assert.Equal(MigrationRefusalCodes.TopoValveCount, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Broken_Pairing_Refuses()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            WaterJets = accepted.WaterJets
                .Select(w => w.WaterJetId == "WJ1" ? w with { DedicatedIsolationValveId = "IV2" } : w)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoPairingMismatch, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Missing_Water_Jet_Assignment_Refuses()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            Sensors = accepted.Sensors
                .Select((s, i) => i == 0 ? s with { AssignedWaterJetId = "WJ9" } : s)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.MigrationMissingAssignment, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Target_Wall_Contradiction_Refuses()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            WaterJets = accepted.WaterJets
                .Select(w => w.WaterJetId == "WJ5" ? w with { TargetWall = Wall.RIGHT } : w)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoTargetWallContradiction, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Corrupted_Position_Identity_Refuses_With_Sensor_Identity()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            LogicalPositions = accepted.LogicalPositions
                .Select(p => p.LogicalId == "I16" ? p with { LogicalId = "I16X" } : p)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoSensorIdentity, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Sensor_Without_A_Logical_Position_Refuses_With_Sensor_Identity()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            Sensors = accepted.Sensors
                .Select(s => s.SensorId == "I7" ? s with { SensorId = "WJ9" } : s)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoSensorIdentity, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Anchors_On_An_Unresolved_Placement_Refuse()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            WaterJets = accepted.WaterJets
                .Select(w => w.WaterJetId == "WJ3" ? w with { PlacementAnchors = ["I6", "I8"] } : w)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoTargetWallContradiction, RefusalCodeOf(mutated));
    }

    [Fact]
    public void Resolved_Placement_Kind_Refuses_Under_The_Current_Disposition()
    {
        // Canonical Topology Adoption: the fine-grained placement of every Water Jet is
        // UNRESOLVED with no anchors; reintroducing a resolved kind (even anchor-free)
        // is refused fail-closed rather than silently accepted.
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            WaterJets = accepted.WaterJets
                .Select(w => w.WaterJetId == "WJ2" ? w with { PlacementKind = WaterJetPlacementKind.JUNCTION } : w)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoTargetWallContradiction, RefusalCodeOf(mutated));

        Assert.All(
            accepted.WaterJets,
            w =>
            {
                Assert.Equal(WaterJetPlacementKind.UNRESOLVED, w.PlacementKind);
                Assert.Empty(w.PlacementAnchors);
            });
        Assert.Equal(ExpectedUnresolvedPlacementWaterJetIds, WaterJetTopologyCatalog.UnresolvedPlacementWaterJetIds.ToArray());
    }

    [Fact]
    public void Invariant_Violation_Code_Is_Distinct_And_Central_Check_Accepts_The_Result()
    {
        var accepted = AcceptedResult();
        Assert.Equal("TOPO_INVARIANT_VIOLATION", MigrationRefusalCodes.TopoInvariantViolation);
        Assert.Empty(TopologyInvariants.Check(new TopologyObservation
        {
            SensorSequence = accepted.Sensors.Select(s => s.SensorId).ToArray(),
            FrontMeasurementSensorIds = accepted.Sensors.Select(s => s.SensorId).ToArray(),
            RearMeasurementSensorIds = accepted.Sensors.Select(s => s.SensorId).ToArray(),
            AssignedMeasurementCount = accepted.Sensors.Count * 2,
            PhysicalChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount,
            UnassignedChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount - (accepted.Sensors.Count * 2),
            SensorsPerWall = accepted.Sensors.GroupBy(s => s.Wall).ToDictionary(g => g.Key, g => g.Count()),
            RuntimeSensorCount = accepted.LogicalPositions.Count,
        }));
    }

    [Fact]
    public void Corrupted_Wall_Total_Refuses()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            Sensors = accepted.Sensors
                .Select((s, i) => i == 0 ? s with { Wall = Wall.REAR } : s)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoWallCounts, RefusalCodeOf(mutated));
    }
}
