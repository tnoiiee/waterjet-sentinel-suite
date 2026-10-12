using Wjss.Contracts;
using Xunit;

namespace Wjss.Domain.Tests;

/// <summary>
/// Central .NET topology invariants (Owner critical topology correction, Stage 0.4C-K3) —
/// the .NET equivalent of the Node <c>topologyInvariants.mjs</c> gate. Required values:
/// 108 Sensors / 108 unique identities / 108 Front / 108 Rear / 216 assigned / 240 physical /
/// 24 unassigned; I7 and I16 exactly once each with exactly one Front and one Rear
/// measurement; no non-Sensor gap; every published layer count in agreement. Any
/// disagreement among the layers fails closed. SOURCE-AUTHORED IN ARENA; NOT COMPILED IN
/// ARENA (Owner-local test run is the arbiter).
/// </summary>
public sealed class TopologyInvariantTests
{
    private static TopologyObservation Canonical() => new()
    {
        SensorSequence = CanonicalSensorMap.SensorSequence.ToArray(),
        FrontMeasurementSensorIds = CanonicalSensorMap.SensorSequence.ToArray(),
        RearMeasurementSensorIds = CanonicalSensorMap.SensorSequence.ToArray(),
        AssignedMeasurementCount = 216,
        PhysicalChannelCount = 240,
        UnassignedChannelCount = 24,
        NonSensorGapCount = 0,
        SensorsPerWall = new Dictionary<Wall, int>
        {
            [Wall.LEFT] = 24,
            [Wall.REAR] = 30,
            [Wall.RIGHT] = 24,
            [Wall.FRONT] = 30,
        },
        UiMapSensorCount = 108,
        RuntimeSensorCount = 108,
        SerializedFixtureSensorCount = 108,
    };

    private static string[] Without(string sensorId) =>
        CanonicalSensorMap.SensorSequence.Where(id => id != sensorId).ToArray();

    [Fact]
    public void Required_Values_Are_Pinned_On_The_Canonical_Constants()
    {
        Assert.Equal(108, TopologyInvariants.ExpectedSensors);
        Assert.Equal(216, TopologyInvariants.ExpectedAssignedMeasurements);
        Assert.Equal(240, TopologyInvariants.ExpectedPhysicalChannels);
        Assert.Equal(24, TopologyInvariants.ExpectedUnassignedChannels);
    }

    [Fact]
    public void Canonical_Observation_Holds_Every_Invariant()
    {
        Assert.Empty(TopologyInvariants.Check(Canonical()));
        Assert.Null(TopologyInvariants.FirstViolation(Canonical()));
        TopologyInvariants.Require(Canonical()); // must not throw
    }

    [Fact]
    public void Optional_Layer_Counts_May_Be_Absent_But_Not_Wrong()
    {
        var minimal = Canonical() with { SensorsPerWall = null, UiMapSensorCount = null, RuntimeSensorCount = null, SerializedFixtureSensorCount = null };
        Assert.Empty(TopologyInvariants.Check(minimal));

        Assert.Contains(TopologyInvariants.Check(Canonical() with { UiMapSensorCount = 106 }), v => v.Code == TopologyInvariants.UiMapCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { RuntimeSensorCount = 106 }), v => v.Code == TopologyInvariants.RuntimeCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { SerializedFixtureSensorCount = 106 }), v => v.Code == TopologyInvariants.FixtureCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { RuntimeSensorCount = 106 }), v => v.Code == TopologyInvariants.LayerDisagreement);
    }

    [Theory]
    [InlineData("I7")]
    [InlineData("I16")]
    [InlineData("G+201")]
    [InlineData("J18")]
    public void A_Sequence_Missing_Any_Sensor_Fails_Closed(string sensorId)
    {
        var violations = TopologyInvariants.Check(Canonical() with { SensorSequence = Without(sensorId) });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SequenceLength);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SensorMissing && v.Detail.Contains($"'{sensorId}'", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Superseded_106_Sequence_Without_I7_And_I16_Is_Refused()
    {
        var superseded = CanonicalSensorMap.SensorSequence.Where(id => id is not ("I7" or "I16")).ToArray();
        Assert.Equal(106, superseded.Length);
        var violations = TopologyInvariants.Check(Canonical() with
        {
            SensorSequence = superseded,
            FrontMeasurementSensorIds = superseded,
            RearMeasurementSensorIds = superseded,
            AssignedMeasurementCount = 212,
            UnassignedChannelCount = 28,
            SensorsPerWall = new Dictionary<Wall, int> { [Wall.LEFT] = 24, [Wall.REAR] = 29, [Wall.RIGHT] = 24, [Wall.FRONT] = 29 },
            UiMapSensorCount = 106,
            RuntimeSensorCount = 106,
            SerializedFixtureSensorCount = 106,
        });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SequenceLength);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SensorMissing);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.ScheduleAssignedCount);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.ScheduleUnassignedCount);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.CatalogueWallCount);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.FrontRearPairing);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.UiMapCount);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.RuntimeCount);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.FixtureCount);
    }

    [Fact]
    public void Duplicate_Or_Unknown_Sensor_Identity_Fails_Closed()
    {
        var duplicated = CanonicalSensorMap.SensorSequence.Select(id => id == "I16" ? "I7" : id).ToArray();
        Assert.Contains(TopologyInvariants.Check(Canonical() with { SensorSequence = duplicated }), v => v.Code == TopologyInvariants.DuplicateSensorId);

        var unknown = CanonicalSensorMap.SensorSequence.Select(id => id == "I7" ? "WJ3" : id).ToArray();
        var violations = TopologyInvariants.Check(Canonical() with { SensorSequence = unknown });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SensorUnknown);
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SensorMissing && v.Detail.Contains("'I7'", StringComparison.Ordinal));
    }

    [Fact]
    public void Ordinal_Order_Is_Enforced_Not_Only_Membership()
    {
        var swapped = CanonicalSensorMap.SensorSequence.ToArray();
        (swapped[77], swapped[78]) = (swapped[78], swapped[77]); // I6 <-> I7
        var violations = TopologyInvariants.Check(Canonical() with { SensorSequence = swapped });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.SequenceOrder && v.Detail.Contains("'I6'", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_Sensor_Needs_Exactly_One_Front_And_One_Rear_Measurement()
    {
        Assert.Contains(
            TopologyInvariants.Check(Canonical() with { FrontMeasurementSensorIds = Without("I7") }),
            v => v.Code == TopologyInvariants.FrontRearPairing && v.Detail.Contains("Front", StringComparison.Ordinal));
        Assert.Contains(
            TopologyInvariants.Check(Canonical() with { RearMeasurementSensorIds = Without("I16") }),
            v => v.Code == TopologyInvariants.FrontRearPairing && v.Detail.Contains("Rear", StringComparison.Ordinal));

        var doubledFront = CanonicalSensorMap.SensorSequence.Select(id => id == "I6" ? "I7" : id).ToArray();
        Assert.Contains(
            TopologyInvariants.Check(Canonical() with { FrontMeasurementSensorIds = doubledFront }),
            v => v.Code == TopologyInvariants.FrontRearPairing);
    }

    [Fact]
    public void Physical_Assigned_And_Unassigned_Counts_Must_Agree()
    {
        Assert.Contains(TopologyInvariants.Check(Canonical() with { AssignedMeasurementCount = 212, UnassignedChannelCount = 28 }), v => v.Code == TopologyInvariants.ScheduleAssignedCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { PhysicalChannelCount = 232, UnassignedChannelCount = 16 }), v => v.Code == TopologyInvariants.SchedulePhysicalCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { UnassignedChannelCount = 28 }), v => v.Code == TopologyInvariants.ScheduleUnassignedCount);
        Assert.Contains(TopologyInvariants.Check(Canonical() with { AssignedMeasurementCount = 218, UnassignedChannelCount = 22 }), v => v.Code == TopologyInvariants.ScheduleAssignedCount);
    }

    [Fact]
    public void Any_Remaining_Non_Sensor_Gap_Classification_Is_Refused()
    {
        var violations = TopologyInvariants.Check(Canonical() with { NonSensorGapCount = 2 });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.NonSensorGapPresent);
    }

    [Fact]
    public void Wall_Totals_Must_Match_The_Catalogue()
    {
        var violations = TopologyInvariants.Check(Canonical() with
        {
            SensorsPerWall = new Dictionary<Wall, int> { [Wall.LEFT] = 24, [Wall.REAR] = 29, [Wall.RIGHT] = 24, [Wall.FRONT] = 31 },
        });
        Assert.Contains(violations, v => v.Code == TopologyInvariants.CatalogueWallCount && v.Detail.Contains("REAR", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Code == TopologyInvariants.CatalogueWallCount && v.Detail.Contains("FRONT", StringComparison.Ordinal));
    }

    [Fact]
    public void Require_Throws_With_The_Shared_Prefix()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariants.Require(Canonical() with { SensorSequence = Without("I16") }));
        Assert.StartsWith(TopologyInvariants.ViolationMessagePrefix, error.Message, StringComparison.Ordinal);
        Assert.Contains(TopologyInvariants.SensorMissing, error.Message, StringComparison.Ordinal);
    }
}
