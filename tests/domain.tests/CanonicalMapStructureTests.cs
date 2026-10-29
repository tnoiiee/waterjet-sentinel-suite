using Wjss.Contracts;
using Wjss.Domain;
using Xunit;

namespace Wjss.Domain.Tests;

/// <summary>
/// Canonical 108 Sensors / 216 assigned TC measurements / 240 physical / 24 unassigned
/// structural facts (Owner critical topology correction, Stage 0.4C-K3: I7 and I16 are
/// normal Product Sensors; the former 106 / 212 reading is superseded).
/// The C# map constants are the authoritative single mapping source; the
/// generated sensor-map example and every fixture must agree with them.
/// </summary>
public sealed class CanonicalMapStructureTests
{
    private static readonly string[] ExpectedCorrectedSensorIds = ["I7", "I16"];
    private static readonly string[] ExpectedLogicalPositionKinds = ["SENSOR"];

    [Fact]
    public void Counts_Match_Owner_Confirmed_Domain()
    {
        Assert.Equal(18, CanonicalSensorMap.LogicalColumnCount);
        Assert.Equal(6, CanonicalSensorMap.LogicalRowCount);
        Assert.Equal(108, CanonicalSensorMap.MatrixSlots);
        Assert.Equal(108, CanonicalSensorMap.SensorLocations);
        Assert.Equal(2, CanonicalSensorMap.ChannelsPerSensor);
        Assert.Equal(216, CanonicalSensorMap.ThermocoupleChannelCount);
        Assert.Equal(240, CanonicalSensorMap.PhysicalThermocoupleChannelCount);
        Assert.Equal(24, CanonicalSensorMap.UnassignedThermocoupleChannelCount);
        Assert.Equal(CanonicalSensorMap.MatrixSlots, CanonicalSensorMap.SensorLocations); // every slot is a Sensor

        Assert.Equal(24, CanonicalSensorMap.SensorsPerWall[Wall.LEFT]);
        Assert.Equal(30, CanonicalSensorMap.SensorsPerWall[Wall.REAR]);
        Assert.Equal(24, CanonicalSensorMap.SensorsPerWall[Wall.RIGHT]);
        Assert.Equal(30, CanonicalSensorMap.SensorsPerWall[Wall.FRONT]);
        Assert.Equal(108, CanonicalSensorMap.SensorsPerWall.Values.Sum());
    }

    [Fact]
    public void Sensor_Sequence_Is_The_108_Ordinal_Identities_With_I7_And_I16_In_Place()
    {
        var sequence = CanonicalSensorMap.SensorSequence;
        Assert.Equal(108, sequence.Count);
        Assert.Equal(108, sequence.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("G+201", sequence[0]);
        Assert.Equal("G+218", sequence[17]);
        Assert.Equal("G+101", sequence[18]);
        Assert.Equal("G1", sequence[36]);
        Assert.Equal("H1", sequence[54]);
        Assert.Equal("I1", sequence[72]);
        Assert.Equal("I6", sequence[77]);
        Assert.Equal("I7", sequence[78]);   // scanOrder 79, REAR
        Assert.Equal("I8", sequence[79]);
        Assert.Equal("I15", sequence[86]);
        Assert.Equal("I16", sequence[87]);  // scanOrder 88, FRONT
        Assert.Equal("I17", sequence[88]);
        Assert.Equal("J18", sequence[107]);

        Assert.Equal(ExpectedCorrectedSensorIds, CanonicalSensorMap.CorrectedSensorIds.ToArray());
        Assert.True(CanonicalSensorMap.IsProductSensorId("I7"));
        Assert.True(CanonicalSensorMap.IsProductSensorId("I16"));
        Assert.False(CanonicalSensorMap.IsProductSensorId("WJ1"));
        Assert.False(CanonicalSensorMap.IsProductSensorId("IV3"));
        Assert.False(CanonicalSensorMap.IsProductSensorId(null));
        Assert.False(CanonicalSensorMap.IsProductSensorId("I19"));
        Assert.Equal(Wall.REAR, CanonicalSensorMap.WallForColumn(7));
        Assert.Equal(Wall.FRONT, CanonicalSensorMap.WallForColumn(16));
    }

    [Fact]
    public void Sensor_Id_For_Row_And_Column_Follows_Owner_Labels()
    {
        Assert.Equal("G+201", CanonicalSensorMap.SensorIdFor(1, 1));
        Assert.Equal("G+218", CanonicalSensorMap.SensorIdFor(1, 18));
        Assert.Equal("G+101", CanonicalSensorMap.SensorIdFor(2, 1));
        Assert.Equal("G7", CanonicalSensorMap.SensorIdFor(3, 7));
        Assert.Equal("H18", CanonicalSensorMap.SensorIdFor(4, 18));
        Assert.Equal("I18", CanonicalSensorMap.SensorIdFor(5, 18));
        Assert.Equal("J1", CanonicalSensorMap.SensorIdFor(6, 1));
    }

    [Fact]
    public void No_Gap_Vocabulary_Survives_In_The_Canonical_Map()
    {
        // Owner critical topology correction: the former NON_SENSOR_GAP positions at I7 and
        // I16 are withdrawn; no member of the canonical map describes a gap or a gap anchor.
        Assert.Equal(ExpectedLogicalPositionKinds, Enum.GetNames<LogicalPositionKind>());
        Assert.DoesNotContain(typeof(CanonicalSensorMap).GetMembers(), m => m.Name.Contains("Gap", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(WallMapSlot).GetProperties(), p => p.Name.Contains("Gap", StringComparison.Ordinal));
        Assert.Equal("I7", CanonicalSensorMap.SensorIdFor(5, 7));
        Assert.Equal("I16", CanonicalSensorMap.SensorIdFor(5, 16));
    }

    // ---------------------------------------------------------------- logical-row tier boundary
    //
    // Owner-authoritative tier boundary (2026-10-29): rows 1-3 (G+201-G+218, G+101-G+118, G1-G18) are
    // UPPER; rows 4-6 (H1-H18, I1-I18, J1-J18) are LOWER. The earlier "rows 1-2 UPPER / rows 3-6
    // LOWER" mapping is revoked, and WaterJetTopologyCatalog.RegionForLogicalRow is the only tier
    // derivation in the Product.

    // The Owner's authoritative wall / tier examples, one per (row, wall) pair.
    private static readonly (string SensorId, int Row, Wall Wall, Region Region)[] AuthoritativeExamples =
    [
        ("G+201", 1, Wall.LEFT, Region.UPPER),
        ("G+205", 1, Wall.REAR, Region.UPPER),
        ("G+210", 1, Wall.RIGHT, Region.UPPER),
        ("G+214", 1, Wall.FRONT, Region.UPPER),
        ("G+101", 2, Wall.LEFT, Region.UPPER),
        ("G+105", 2, Wall.REAR, Region.UPPER),
        ("G+110", 2, Wall.RIGHT, Region.UPPER),
        ("G+114", 2, Wall.FRONT, Region.UPPER),
        ("G1", 3, Wall.LEFT, Region.UPPER),
        ("G5", 3, Wall.REAR, Region.UPPER),
        ("G10", 3, Wall.RIGHT, Region.UPPER),
        ("G14", 3, Wall.FRONT, Region.UPPER),
        ("H1", 4, Wall.LEFT, Region.LOWER),
        ("H5", 4, Wall.REAR, Region.LOWER),
        ("H10", 4, Wall.RIGHT, Region.LOWER),
        ("H14", 4, Wall.FRONT, Region.LOWER),
        ("I1", 5, Wall.LEFT, Region.LOWER),
        ("I7", 5, Wall.REAR, Region.LOWER),
        ("I10", 5, Wall.RIGHT, Region.LOWER),
        ("I16", 5, Wall.FRONT, Region.LOWER),
        ("J1", 6, Wall.LEFT, Region.LOWER),
        ("J5", 6, Wall.REAR, Region.LOWER),
        ("J10", 6, Wall.RIGHT, Region.LOWER),
        ("J14", 6, Wall.FRONT, Region.LOWER),
    ];

    [Fact]
    public void Region_For_Logical_Row_Is_Upper_For_Rows_1_To_3_And_Lower_For_Rows_4_To_6()
    {
        Assert.Equal(Region.UPPER, WaterJetTopologyCatalog.RegionForLogicalRow(1));
        Assert.Equal(Region.UPPER, WaterJetTopologyCatalog.RegionForLogicalRow(2));
        Assert.Equal(Region.UPPER, WaterJetTopologyCatalog.RegionForLogicalRow(3));
        Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.RegionForLogicalRow(4));
        Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.RegionForLogicalRow(5));
        Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.RegionForLogicalRow(6));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Region_For_Logical_Row_Outside_1_To_6_Is_Refused(int logicalRow)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => WaterJetTopologyCatalog.RegionForLogicalRow(logicalRow));
        Assert.Equal("logicalRow", refusal.ParamName);
    }

    /// <summary>
    /// Canonical ordinal (1-based) of one Sensor identity in the 18-column grid. Fails the test for an
    /// identity that is not in <see cref="CanonicalSensorMap.SensorSequence"/>, so a mistyped example
    /// can never be silently resolved to row 1 / column 0.
    /// </summary>
    private static int OrdinalOf(string sensorId)
    {
        var index = -1;
        for (var ordinal = 0; ordinal < CanonicalSensorMap.SensorSequence.Count; ordinal++)
        {
            if (string.Equals(CanonicalSensorMap.SensorSequence[ordinal], sensorId, StringComparison.Ordinal))
            {
                index = ordinal;
                break;
            }
        }

        Assert.True(index >= 0, $"{sensorId} is not one of the 108 canonical Product Sensors");
        return index + 1;
    }

    private static int RowOf(string sensorId) =>
        ((OrdinalOf(sensorId) - 1) / CanonicalSensorMap.LogicalColumnCount) + 1;

    private static int ColumnOf(string sensorId) =>
        ((OrdinalOf(sensorId) - 1) % CanonicalSensorMap.LogicalColumnCount) + 1;

    [Fact]
    public void Every_Canonical_Sensor_Resolves_To_The_Owner_Authoritative_Tier()
    {
        Assert.Equal(108, CanonicalSensorMap.SensorSequence.Count);

        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            var region = WaterJetTopologyCatalog.RegionForLogicalRow(RowOf(sensorId));
            var expected = PrefixOf(sensorId) is "G+2" or "G+1" or "G" ? Region.UPPER : Region.LOWER;
            Assert.Equal(expected, region);
        }

        // The identity prefixes and the logical rows carry the authoritative split: G+2xx, G+1xx and G
        // are rows 1-3 (UPPER); H, I and J are rows 4-6 (LOWER).
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "G+2"),
            id => Assert.Equal(1, RowOf(id)));
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "G+1"),
            id => Assert.Equal(2, RowOf(id)));
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "G"),
            id => Assert.Equal(3, RowOf(id)));
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "H"),
            id => Assert.Equal(4, RowOf(id)));
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "I"),
            id => Assert.Equal(5, RowOf(id)));
        Assert.All(
            CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == "J"),
            id => Assert.Equal(6, RowOf(id)));
    }

    [Fact]
    public void G1_To_G18_Are_Upper_And_H_To_J_Are_Lower()
    {
        foreach (var prefix in new[] { "G", "H", "I", "J" })
        {
            var expected = prefix == "G" ? Region.UPPER : Region.LOWER;
            var sensors = CanonicalSensorMap.SensorSequence.Where(id => PrefixOf(id) == prefix).ToArray();
            Assert.Equal(18, sensors.Length);
            Assert.All(
                sensors,
                id => Assert.Equal(expected, WaterJetTopologyCatalog.RegionForLogicalRow(RowOf(id))));
            Assert.All(
                sensors,
                id => Assert.Equal($"{prefix}{ColumnOf(id)}", id));
        }

        // G1-G18 are the row-3 identities: the revoked mapping classified exactly these as LOWER.
        Assert.All(
            Enumerable.Range(1, 18),
            column => Assert.Equal(
                Region.UPPER,
                WaterJetTopologyCatalog.RegionForLogicalRow(RowOf($"G{column}"))));
    }

    [Fact]
    public void I7_And_I16_Stay_Ordinary_Lower_Tier_Sensors_In_Their_Canonical_Positions()
    {
        Assert.Equal(79, OrdinalOf("I7"));
        Assert.Equal(88, OrdinalOf("I16"));
        Assert.Equal(5, RowOf("I7"));
        Assert.Equal(7, ColumnOf("I7"));
        Assert.Equal(Wall.REAR, CanonicalSensorMap.WallForColumn(ColumnOf("I7")));
        Assert.Equal(5, RowOf("I16"));
        Assert.Equal(16, ColumnOf("I16"));
        Assert.Equal(Wall.FRONT, CanonicalSensorMap.WallForColumn(ColumnOf("I16")));
        Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.RegionForLogicalRow(RowOf("I7")));
        Assert.Equal(Region.LOWER, WaterJetTopologyCatalog.RegionForLogicalRow(RowOf("I16")));
    }

    [Fact]
    public void Owner_Authoritative_Wall_And_Tier_Examples_Resolve_As_Pinned()
    {
        foreach (var (sensorId, row, wall, region) in AuthoritativeExamples)
        {
            Assert.Equal(row, RowOf(sensorId));
            Assert.Equal(wall, CanonicalSensorMap.WallForColumn(ColumnOf(sensorId)));
            Assert.Equal(region, WaterJetTopologyCatalog.RegionForLogicalRow(row));
        }

        // The wall column ranges are unchanged by the tier correction.
        Assert.Equal(Wall.LEFT, CanonicalSensorMap.WallForColumn(1));
        Assert.Equal(Wall.LEFT, CanonicalSensorMap.WallForColumn(4));
        Assert.Equal(Wall.REAR, CanonicalSensorMap.WallForColumn(5));
        Assert.Equal(Wall.REAR, CanonicalSensorMap.WallForColumn(9));
        Assert.Equal(Wall.RIGHT, CanonicalSensorMap.WallForColumn(10));
        Assert.Equal(Wall.RIGHT, CanonicalSensorMap.WallForColumn(13));
        Assert.Equal(Wall.FRONT, CanonicalSensorMap.WallForColumn(14));
        Assert.Equal(Wall.FRONT, CanonicalSensorMap.WallForColumn(18));
    }

    private static string PrefixOf(string sensorId) =>
        sensorId.StartsWith("G+", StringComparison.Ordinal) ? sensorId[..3] : sensorId[..1];

    [Fact]
    public void Wall_Columns_Partition_1_To_18()
    {
        var ranges = CanonicalSensorMap.WallColumns.Values.OrderBy(v => v.FirstColumn).ToArray();
        Assert.Equal(1, ranges[0].FirstColumn);
        Assert.Equal(18, ranges[^1].LastColumn);
        for (var i = 1; i < ranges.Length; i++)
        {
            Assert.Equal(ranges[i - 1].LastColumn + 1, ranges[i].FirstColumn);
        }

        for (var col = 1; col <= 18; col++)
        {
            _ = CanonicalSensorMap.WallForColumn(col); // must not throw inside the matrix
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSensorMap.WallForColumn(19));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSensorMap.WallForColumn(0));
    }
}
