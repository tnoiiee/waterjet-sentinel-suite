using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Protected-baseline invariants: 108 logical slots, 106 Sensor locations, 212
/// Thermocouple channels, NON_SENSOR_GAP positions at logical I7 and I16 only,
/// wall counts 24 / 29 / 24 / 29, one Active Job at most, bounded queue and
/// bounded trend.
/// Each refusal is asserted through its machine reason code, not through prose.
/// </summary>
public sealed class RuntimeStateInvariantTests
{
    public enum Tamper
    {
        Profile,
        SensorCount,
        SensorOrder,
        SensorIdentity,
        SensorChannels,
        NonSensorGapPosition,
        WallSummary,
        ActiveTarget,
        QueueShape,
        TrendBounds,
        AlarmCounters,
        CommunicationDevices,
    }

    [Fact]
    public void Composed_State_Satisfies_The_Protected_Counts()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.True(
            RuntimeStateInvariants.TryValidate(state, out var reasonCode, out var detail),
            $"composed state must be valid: [{reasonCode}] {detail}");

        var slotCount = state.WallMap.Count;
        var uniqueSlotCount = state.WallMap.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count();
        var sensorSlotCount = state.WallMap.Count(slot => slot.PositionKind == LogicalPositionKind.SENSOR);
        var sensorProjectionCount = state.Sensors.Count;
        var channels = state.Sensors
            .SelectMany(sensor => new[] { sensor.TcFrontChannel, sensor.TcRearChannel })
            .ToArray();
        var channelCount = channels.Length;
        var uniqueChannelCount = channels.Distinct(StringComparer.Ordinal).Count();

        Assert.Equal(CanonicalSensorMap.MatrixSlots, slotCount);
        Assert.Equal(CanonicalSensorMap.MatrixSlots, uniqueSlotCount);
        Assert.Equal(CanonicalSensorMap.SensorLocations, sensorSlotCount);
        Assert.Equal(CanonicalSensorMap.SensorLocations, sensorProjectionCount);
        Assert.Equal(CanonicalSensorMap.ThermocoupleChannelCount, channelCount);
        Assert.Equal(CanonicalSensorMap.ThermocoupleChannelCount, uniqueChannelCount);
    }

    [Fact]
    public void NonSensorGap_Positions_Are_Logical_I7_And_I16_Anchor_WJ3_And_WJ1_And_Are_Not_Sensors()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        var gapSlots = state.WallMap.Where(slot => slot.PositionKind == LogicalPositionKind.NON_SENSOR_GAP).ToArray();
        var gapSlotCount = gapSlots.Length;
        Assert.Equal(CanonicalSensorMap.NonSensorGapCount, gapSlotCount);
        Assert.Contains(gapSlots, slot =>
            slot.GapAnchorForWaterJetId == "WJ3" && slot.LogicalRow == 5 && slot.LogicalColumn == 7 && slot.SensorId is null);
        Assert.Contains(gapSlots, slot =>
            slot.GapAnchorForWaterJetId == "WJ1" && slot.LogicalRow == 5 && slot.LogicalColumn == 16 && slot.SensorId is null);
        Assert.DoesNotContain(state.Sensors, sensor => sensor.SensorId == "I7" || sensor.SensorId == "I16");
    }

    [Fact]
    public void Wall_Summaries_Are_Recalculated_From_The_Sensors()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.Equal(RuntimeWallSummaries.Recalculate(state.Sensors), state.Walls);
        Assert.Equal(
            new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT },
            state.Walls.Select(summary => summary.Wall));

        var totals = state.Walls.ToDictionary(summary => summary.Wall, summary => summary.Total);
        Assert.Equal(24, totals[Wall.LEFT]);
        Assert.Equal(29, totals[Wall.REAR]);
        Assert.Equal(24, totals[Wall.RIGHT]);
        Assert.Equal(29, totals[Wall.FRONT]);

        foreach (var summary in state.Walls)
        {
            Assert.Equal(summary.Total, summary.Dirty + summary.Cleaner + summary.NotClassified);
        }
    }

    [Theory]
    [InlineData(Tamper.Profile, RuntimeRefusalCodes.ProfileNotSimulator)]
    [InlineData(Tamper.SensorCount, RuntimeRefusalCodes.SensorCount)]
    [InlineData(Tamper.SensorOrder, RuntimeRefusalCodes.SensorOrder)]
    [InlineData(Tamper.SensorIdentity, RuntimeRefusalCodes.SensorIdentity)]
    [InlineData(Tamper.SensorChannels, RuntimeRefusalCodes.SensorChannels)]
    [InlineData(Tamper.NonSensorGapPosition, RuntimeRefusalCodes.WallMapNonSensorGaps)]
    [InlineData(Tamper.WallSummary, RuntimeRefusalCodes.WallSummaryMismatch)]
    [InlineData(Tamper.ActiveTarget, RuntimeRefusalCodes.SensorActiveTarget)]
    [InlineData(Tamper.QueueShape, RuntimeRefusalCodes.QueueShape)]
    [InlineData(Tamper.TrendBounds, RuntimeRefusalCodes.TrendBounds)]
    [InlineData(Tamper.AlarmCounters, RuntimeRefusalCodes.AlarmCounters)]
    [InlineData(Tamper.CommunicationDevices, RuntimeRefusalCodes.DeviceHealthSet)]
    public void Tampered_State_Is_Refused_With_Its_Machine_Code(Tamper tamper, string expectedCode)
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var tampered = Apply(state, tamper);

        Assert.False(RuntimeStateInvariants.TryValidate(tampered, out var reasonCode, out _));
        Assert.Equal(expectedCode, reasonCode);
    }

    [Fact]
    public void RequireValid_Throws_With_The_Machine_Code()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var tampered = Apply(state, Tamper.Profile);

        var thrown = Assert.Throws<InvalidOperationException>(() => RuntimeStateInvariants.RequireValid(tampered));

        Assert.Contains(RuntimeRefusalCodes.ProfileNotSimulator, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_Simulator_Profiles_Are_Refused_At_Composition()
    {
        var config = RuntimeTestFixture.Config();
        var wallMap = SyntheticSensorMap.BuildWallMap();
        var sensors = SyntheticSensorMap.BuildInitialSensors(config, SyntheticSeed.DefaultSeed, RuntimeTestFixture.Instant);

        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var refused = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = RuntimeStateComposer.ComposeInitial(
                    profile,
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

    private static RuntimeState Apply(RuntimeState state, Tamper tamper) => tamper switch
    {
        Tamper.Profile => state with { DeviceProfile = DeviceProfile.TEST_HARDWARE },
        Tamper.SensorCount => state with { Sensors = state.Sensors.Take(state.Sensors.Count - 1).ToArray() },
        Tamper.SensorOrder => state with { Sensors = SwapFirstTwo(state.Sensors) },
        Tamper.SensorIdentity => state with
        {
            Sensors = ReplaceFirst(state.Sensors, state.Sensors[0] with { SensorId = "NOT-A-SENSOR" }),
        },
        Tamper.SensorChannels => state with
        {
            Sensors = ReplaceFirst(state.Sensors, state.Sensors[0] with { TcFrontChannel = state.Sensors[1].TcRearChannel }),
        },
        Tamper.NonSensorGapPosition => state with
        {
            WallMap = ReplaceFirst(
                state.WallMap,
                state.WallMap[0] with
                {
                    PositionKind = LogicalPositionKind.NON_SENSOR_GAP,
                    SensorId = null,
                    GapAnchorForWaterJetId = "WJ3",
                }),
        },
        Tamper.WallSummary => state with
        {
            Walls = ReplaceFirst(state.Walls, state.Walls[0] with { Dirty = state.Walls[0].Dirty + 1 }),
        },
        Tamper.ActiveTarget => state with
        {
            Sensors = ReplaceFirst(
                state.Sensors,
                state.Sensors[0] with { IsActiveJobTarget = true, QueueState = QueueState.ACTIVE }),
        },
        Tamper.QueueShape => state with { Queue = state.Queue with { TotalQueued = 1 } },
        Tamper.TrendBounds => state with { Trend = state.Trend with { Capacity = RuntimeLimits.MaximumTrendCapacity + 1 } },
        Tamper.AlarmCounters => state with { Alarms = state.Alarms with { ActiveUnack = 1 } },
        Tamper.CommunicationDevices => state with
        {
            Communication = new CommunicationHealth
            {
                Devices = Array.AsReadOnly(new[]
                {
                    new DeviceHealth
                    {
                        DeviceId = "SYN-TC-99",
                        State = DeviceLinkState.ONLINE,
                        ConsecutiveTimeouts = 0,
                        LastSuccessAt = null,
                        LastLatencyMs = null,
                        PollsOk = 0,
                        PollsFailed = 0,
                    },
                }),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(tamper), tamper, "Unhandled tamper case."),
    };

    private static SensorPresentationState[] SwapFirstTwo(IReadOnlyList<SensorPresentationState> sensors)
    {
        var copy = sensors.ToArray();
        (copy[0], copy[1]) = (copy[1], copy[0]);
        return copy;
    }

    private static SensorPresentationState[] ReplaceFirst(
        IReadOnlyList<SensorPresentationState> sensors,
        SensorPresentationState replacement)
    {
        var copy = sensors.ToArray();
        copy[0] = replacement;
        return copy;
    }

    private static WallMapSlot[] ReplaceFirst(IReadOnlyList<WallMapSlot> slots, WallMapSlot replacement)
    {
        var copy = slots.ToArray();
        copy[0] = replacement;
        return copy;
    }

    private static WallSummary[] ReplaceFirst(IReadOnlyList<WallSummary> summaries, WallSummary replacement)
    {
        var copy = summaries.ToArray();
        copy[0] = replacement;
        return copy;
    }
}
