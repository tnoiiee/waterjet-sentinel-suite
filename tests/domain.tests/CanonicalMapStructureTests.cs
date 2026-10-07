using Wjss.Contracts;
using Xunit;

namespace Wjss.Domain.Tests;

/// <summary>
/// Canonical 106 / 212 / 108 structural facts (Owner domain correction).
/// The C# map constants are the authoritative single mapping source; the
/// generated sensor-map example and every fixture must agree with them.
/// </summary>
public sealed class CanonicalMapStructureTests
{
    [Fact]
    public void Counts_Match_Owner_Confirmed_Domain()
    {
        Assert.Equal(18, CanonicalSensorMap.LogicalColumnCount);
        Assert.Equal(6, CanonicalSensorMap.LogicalRowCount);
        Assert.Equal(108, CanonicalSensorMap.MatrixSlots);
        Assert.Equal(106, CanonicalSensorMap.SensorLocations);
        Assert.Equal(212, CanonicalSensorMap.ThermocoupleChannelCount);
        Assert.Equal(2, CanonicalSensorMap.CannonSlotCount);

        Assert.Equal(24, CanonicalSensorMap.SensorsPerWall[Wall.LEFT]);
        Assert.Equal(29, CanonicalSensorMap.SensorsPerWall[Wall.REAR]);
        Assert.Equal(24, CanonicalSensorMap.SensorsPerWall[Wall.RIGHT]);
        Assert.Equal(29, CanonicalSensorMap.SensorsPerWall[Wall.FRONT]);
        Assert.Equal(106, CanonicalSensorMap.SensorsPerWall.Values.Sum());
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
    public void Cannon_Slots_Are_Equipment_At_I7_And_I16()
    {
        Assert.Equal(("CANNON_REAR", 5, 7), CanonicalSensorMap.CannonSlots[0]);
        Assert.Equal(("CANNON_FRONT", 5, 16), CanonicalSensorMap.CannonSlots[1]);
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
