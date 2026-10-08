using System.Text.Json;
using Wjss.Config.Examples.Tests; // ConfigTestPaths — shared repository-root discovery
using Wjss.Contracts;
using Wjss.Domain;
using Xunit;

namespace Wjss.Config.Tests;

/// <summary>
/// Stage 0.3A-3 Checkpoint B invariant tests for the legacy sensor-parameter migration
/// (ADR-0017; approved planned tests T1–T20). All fixtures are synthetic public-safe rows
/// built by <see cref="SyntheticSensorParameterCsv"/>; the Owner CSV never enters Git.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local test run is the arbiter).
/// </summary>
public sealed class SensorParameterMigrationTests
{
    // ---- deterministic default-fixture expectations ----
    private const int ExpectedTimezoneWarnings = 53; // even scanOrder values 2..106
    private const int ExpectedUnitWarnings = 35;     // scanOrder divisible by 3
    private const int ExpectedDisabledSensors = 6;   // scanOrder divisible by 17

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
        Assert.Equal(106, result.Sensors.Count);
        Assert.Equal(8, result.WaterJets.Count);
        Assert.Equal(8, result.IsolationValves.Count);
        Assert.Equal(212, result.Sensors.Count * 2); // structural TC sides: two per Sensor
        Assert.Equal(24, result.Sensors.Count(s => s.Wall == Wall.LEFT));
        Assert.Equal(29, result.Sensors.Count(s => s.Wall == Wall.REAR));
        Assert.Equal(24, result.Sensors.Count(s => s.Wall == Wall.RIGHT));
        Assert.Equal(29, result.Sensors.Count(s => s.Wall == Wall.FRONT));
        Assert.Null(TopologyValidator.Validate(result));
    }

    // ---- T2 ------------------------------------------------------------------

    [Fact]
    public void Gap_Positions_Are_NonSensorGap_Anchors_With_No_Sensor_Configuration()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        var gaps = result.LogicalPositions
            .Where(p => p.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
            .ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.Equal("I7", gaps[0].LogicalId);
        Assert.Equal("WJ3", gaps[0].GapAnchorForWaterJetId);
        Assert.Equal("I16", gaps[1].LogicalId);
        Assert.Equal("WJ1", gaps[1].GapAnchorForWaterJetId);

        // No SensorConfiguration and no scanOrder may exist for a gap position.
        Assert.DoesNotContain(result.Sensors, s => s.SensorId == "I7" || s.SensorId == "I16");
        Assert.All(gaps, g => Assert.Equal(LogicalPositionKind.NON_SENSOR_GAP, g.PositionKind));
    }

    // ---- T3 ------------------------------------------------------------------

    [Fact]
    public void ScanOrder_Is_Dense_1_To_106_Skips_Gaps_And_OrderTotal_Stays_0_To_107()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(
            Enumerable.Range(1, 106).ToArray(),
            result.Sensors.Select(s => s.ScanOrder).ToArray());

        Assert.Equal(
            Enumerable.Range(0, 108).ToArray(),
            result.LogicalPositions.Select(p => p.OrderTotal).ToArray());

        // The sensor sequence skips I7 and I16: their neighbours I6 and I8 are consecutive.
        var i6 = result.Sensors.Single(s => s.SensorId == "I6");
        var i8 = result.Sensors.Single(s => s.SensorId == "I8");
        Assert.Equal(i6.ScanOrder + 1, i8.ScanOrder);
    }

    // ---- T4 / T5 ---------------------------------------------------------------

    [Fact]
    public void Water_Jet_And_Valve_Counts_And_One_To_One_Pairing()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.Equal(
            new[] { "WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8" },
            result.WaterJets.Select(w => w.WaterJetId).ToArray());
        Assert.Equal(
            new[] { "IV1", "IV2", "IV3", "IV4", "IV5", "IV6", "IV7", "IV8" },
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
            ("WJ1", Wall.FRONT, Region.LOWER, WaterJetPlacementKind.NON_SENSOR_GAP, ["I15", "I17"], Wall.REAR, Region.LOWER, "IV1"),
            ("WJ2", Wall.LEFT, Region.LOWER, WaterJetPlacementKind.BETWEEN_HORIZONTAL, ["J2", "J3"], Wall.RIGHT, Region.LOWER, "IV2"),
            ("WJ3", Wall.REAR, Region.LOWER, WaterJetPlacementKind.NON_SENSOR_GAP, ["I6", "I8"], Wall.FRONT, Region.LOWER, "IV3"),
            ("WJ4", Wall.RIGHT, Region.LOWER, WaterJetPlacementKind.BETWEEN_HORIZONTAL, ["J11", "J12"], Wall.LEFT, Region.LOWER, "IV4"),
            ("WJ5", Wall.FRONT, Region.UPPER, WaterJetPlacementKind.BETWEEN_VERTICAL, ["G+216", "G+116"], Wall.REAR, Region.UPPER, "IV5"),
            ("WJ6", Wall.LEFT, Region.UPPER, WaterJetPlacementKind.JUNCTION, ["G+102", "G+202", "G+203"], Wall.RIGHT, Region.UPPER, "IV6"),
            ("WJ7", Wall.REAR, Region.UPPER, WaterJetPlacementKind.BETWEEN_VERTICAL, ["G+207", "G+107"], Wall.FRONT, Region.UPPER, "IV7"),
            ("WJ8", Wall.RIGHT, Region.UPPER, WaterJetPlacementKind.JUNCTION, ["G+111", "G+211", "G+212"], Wall.LEFT, Region.UPPER, "IV8"),
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
    public void Device_Ordinal_Maps_Directly_Including_Rear_Lower_To_WJ1()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        foreach (var sensor in result.Sensors)
        {
            var region = WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow);
            var expectedOrdinal = SyntheticSensorParameterCsv.DeviceOrdinalForTarget(sensor.Wall, region);
            Assert.Equal($"WJ{expectedOrdinal}", sensor.AssignedWaterJetId);
            Assert.Equal($"IV{expectedOrdinal}", sensor.AssignedIsolationValveId);
        }

        // Owner worked example: Rear-lower sensors carry device ordinal 1 → WJ1, which is
        // installed at FRONT lower and sprays across to REAR lower, paired valve IV1.
        var i6 = result.Sensors.Single(s => s.SensorId == "I6");
        Assert.Equal(Wall.REAR, i6.Wall);
        Assert.Equal("WJ1", i6.AssignedWaterJetId);
        Assert.Equal("IV1", i6.AssignedIsolationValveId);
        var wj1 = result.WaterJets.Single(w => w.WaterJetId == "WJ1");
        Assert.Equal(Wall.FRONT, wj1.InstalledWall);
        Assert.Equal(Region.LOWER, wj1.InstalledRegion);
        Assert.Equal(Wall.REAR, wj1.TargetWall);
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

        // The gap rows' sensorname values remain the logicalId values of NON_SENSOR_GAP
        // positions and never create a SensorConfiguration.
        Assert.Equal(
            new[] { "I7", "I16" },
            result.LogicalPositions
                .Where(p => p.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
                .Select(p => p.LogicalId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(result.Sensors, s => s.SensorId is "I7" or "I16");
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
        Assert.Equal(106 - ExpectedUnitWarnings, result.Sensors.Count(s => s.MinTimeAllowAddToQueueRaw is null));

        // DEFER fields are carried raw with no canonical attribute anywhere.
        Assert.All(result.Sensors, s =>
        {
            Assert.Null(s.MaxTimeAllowAddToQueueRaw);
            Assert.Null(s.MaxTimeEnableRaw);
            Assert.Null(s.LatestUpdateParamsTimestampRaw);
        });
    }

    // ---- T17 -----------------------------------------------------------------

    [Fact]
    public void No_Unapproved_Derivations_In_Importer_Source()
    {
        var importer = File.ReadAllText(Path.Combine(
            ConfigTestPaths.RepoRoot(), "packages", "domain", "SensorParameterCsvImporter.cs"));

        Assert.DoesNotContain("UseDirtyScoreThreshold", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("HasVerifiedCleaningHistory", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("LastSuccessfulCleaningCompletedAt", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("HardMinimumCleaningInterval", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeSpan", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("ToUniversalTime", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("ToLocalTime", importer, StringComparison.Ordinal);
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

    // ---- T19 -----------------------------------------------------------------

    [Fact]
    public void Canonical_Output_Uses_NonSensorGap_And_Never_Cannon()
    {
        var result = AcceptedResult(SyntheticSensorParameterCsv.Build());

        Assert.All(result.LogicalPositions, p =>
            Assert.True(
                p.PositionKind is LogicalPositionKind.SENSOR or LogicalPositionKind.NON_SENSOR_GAP,
                "canonical positions use only SENSOR and NON_SENSOR_GAP"));

        // The canonical vocabulary surface never references the transitional SlotType.
        var repoRoot = ConfigTestPaths.RepoRoot();
        var importer = File.ReadAllText(Path.Combine(repoRoot, "packages", "domain", "SensorParameterCsvImporter.cs"));
        var topology = File.ReadAllText(Path.Combine(repoRoot, "packages", "contracts", "Topology.cs"));
        var config = File.ReadAllText(Path.Combine(repoRoot, "packages", "contracts", "Config.cs"));
        Assert.DoesNotContain("SlotType", importer, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotType", topology, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotType", config, StringComparison.Ordinal);
        Assert.DoesNotContain("CANNON", topology, StringComparison.Ordinal);
    }

    // ---- T20 -----------------------------------------------------------------

    [Fact]
    public void Runtime_Cannon_Vocabulary_Remains_Transitional_And_Untouched()
    {
        var repoRoot = ConfigTestPaths.RepoRoot();
        var enums = File.ReadAllText(Path.Combine(repoRoot, "packages", "contracts", "Enums.cs"));
        var wallMap = File.ReadAllText(Path.Combine(repoRoot, "packages", "contracts", "WallMap.cs"));

        // The transitional legacy runtime representation is still present, byte-for-byte
        // in its declaration, and its atomic migration stays deferred to Checkpoint C.
        Assert.Contains("public enum SlotType { SENSOR, CANNON }", enums, StringComparison.Ordinal);
        Assert.Contains("public enum LogicalPositionKind { SENSOR, NON_SENSOR_GAP }", enums, StringComparison.Ordinal);
        Assert.Contains("CANNON_REAR", wallMap, StringComparison.Ordinal);
        Assert.Contains("CannonSlotCount = 2", wallMap, StringComparison.Ordinal);
        Assert.Equal(2, CanonicalSensorMap.CannonSlotCount);

        // No alias connects the vocabularies: the LogicalPositionKind declaration lists
        // no Cannon member.
        var kindLine = enums.Split('\n').Single(line => line.Contains("enum LogicalPositionKind", StringComparison.Ordinal));
        Assert.DoesNotContain("CANNON", kindLine, StringComparison.OrdinalIgnoreCase);
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
    private static SensorParameterMigrationResult AcceptedResult() =>
        SensorParameterCsvImporter.Import(SyntheticSensorParameterCsv.Build()).Result!;

    private static string RefusalCodeOf(SensorParameterMigrationResult result)
    {
        var refusal = TopologyValidator.Validate(result);
        Assert.NotNull(refusal);
        return refusal.Code;
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
    public void Broken_Gap_Anchor_Refuses()
    {
        var accepted = AcceptedResult();
        var mutated = accepted with
        {
            LogicalPositions = accepted.LogicalPositions
                .Select(p => p.LogicalId == "I16" ? p with { GapAnchorForWaterJetId = "WJ2" } : p)
                .ToArray(),
        };
        Assert.Equal(MigrationRefusalCodes.TopoGapIdentity, RefusalCodeOf(mutated));
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
