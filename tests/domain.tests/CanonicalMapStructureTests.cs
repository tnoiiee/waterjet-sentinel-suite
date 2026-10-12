using Wjss.Contracts;
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
