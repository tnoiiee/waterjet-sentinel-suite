using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The SIMULATOR profile's deterministic initial state: repeatability for one
/// seed and clock, idleness and labelling honesty of the first revision, and
/// parity between the synthetic map the Runtime composes with and the committed
/// synthetic sensor map in <c>config/examples/</c>.
///
/// The synthetic values themselves are seed-derived presentation values, not the
/// fixture generator's provisional structural values: parity is claimed for the
/// map composition (slots, scan order, device distribution, Thermocouple channel
/// identities), which is the part the committed example documents.
/// </summary>
public sealed class SyntheticInitialStateTests
{
    [Fact]
    public void Same_Seed_And_Clock_Produce_The_Same_State()
    {
        var clock = new TestClock(RuntimeTestFixture.Instant);

        var first = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed, clock.UtcNow);
        var second = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed, clock.UtcNow);

        // RuntimeState record equality compares its collections by reference, so
        // the comparison is element-wise on the collections that carry values.
        Assert.Equal(first.Sensors, second.Sensors);
        Assert.Equal(first.Walls, second.Walls);
        Assert.Equal(first.Trend.SeriesNames, second.Trend.SeriesNames);
        Assert.Equal(first.Trend.Points, second.Trend.Points);
        Assert.Equal(first.Trend.Capacity, second.Trend.Capacity);
        Assert.Equal(
            RuntimeTestFixture.SerializeSnapshot(first),
            RuntimeTestFixture.SerializeSnapshot(second));
    }

    [Fact]
    public void Different_Seeds_Produce_Different_Synthetic_Values()
    {
        var first = RuntimeTestFixture.ComposeInitial(new SyntheticSeed(1));
        var second = RuntimeTestFixture.ComposeInitial(new SyntheticSeed(2));

        Assert.NotEqual(
            first.Sensors.Select(sensor => sensor.DirtyScore).ToArray(),
            second.Sensors.Select(sensor => sensor.DirtyScore).ToArray());
    }

    [Fact]
    public void First_Revision_Is_Idle_And_Labelled_Not_Implemented()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var seriesNameCount = state.Trend.SeriesNames.Count;

        Assert.Equal(1, state.Revision);
        Assert.Equal(DeviceProfile.SIMULATOR, state.DeviceProfile);
        Assert.Equal(RuntimeTestFixture.Instant, state.GeneratedAtUtc);

        Assert.Null(state.ActiveJob);
        Assert.Equal(0, state.Queue.TotalQueued);
        Assert.Equal(0, state.Queue.Revision);
        Assert.Empty(state.Queue.Entries);
        Assert.Equal(QueueSummary.MaxEntries, state.Queue.Capacity);
        Assert.Null(state.Queue.LastDispatch);
        Assert.Equal(AutoSequenceState.OFF, state.Sequence.AutoSequence);
        Assert.Equal(AutoSequenceMode.OFF, state.Sequence.Mode);
        Assert.Null(state.Sequence.Critical);
        Assert.Null(state.Sequence.LastJobOutcome);
        Assert.All(SequenceControlsOf(state.Sequence), control =>
        {
            Assert.False(control.Enabled);
            Assert.Equal(RuntimeStateComposer.ControlNotImplementedReason, control.Reason);
        });

        Assert.Equal(PumpRunState.STOPPED, state.Pump.State);
        Assert.False(state.Pump.Ready);
        Assert.Null(state.Pump.Pressure);

        Assert.Empty(state.Alarms.Items);
        Assert.Equal(0, state.Alarms.ActiveUnack);
        Assert.Equal(0, state.Alarms.ActiveAck);
        Assert.Equal(0, state.Alarms.ClearedUnack);

        // No acquisition exists in Stage 0.3A-2A, so no device health is claimed.
        Assert.Empty(state.Communication.Devices);

        Assert.Empty(state.Trend.Points);
        Assert.Equal(RuntimeLimits.DefaultTrendCapacity, state.Trend.Capacity);
        Assert.Equal(RuntimeLimits.TrendSeriesCount, seriesNameCount);

        Assert.All(state.Sensors, sensor =>
        {
            Assert.Equal(Quality.GOOD, sensor.Quality);
            Assert.Equal(ClassificationBasis.CURRENT, sensor.ClassificationBasis);
            Assert.Equal(QueueState.NONE, sensor.QueueState);
            Assert.False(sensor.IsActiveJobTarget);
            Assert.Equal(AlarmState.NONE, sensor.AlarmState);
            Assert.Null(sensor.AlarmSeverity);
            Assert.Null(sensor.QualityReason);
            Assert.Equal(UtcTimestamps.Format(RuntimeTestFixture.Instant), sensor.SourceTimestamp);
        });
    }

    [Fact]
    public void Trend_Window_Is_Bounded()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var pointCount = state.Trend.Points.Count;

        Assert.True(state.Trend.Capacity >= 1);
        Assert.True(state.Trend.Capacity <= RuntimeLimits.MaximumTrendCapacity);
        Assert.True(pointCount <= state.Trend.Capacity);
    }

    [Fact]
    public void Synthetic_Map_Matches_The_Committed_Sensor_Map_Example()
    {
        var exampleSlots = ReadExampleSlots();
        var exampleSlotCount = exampleSlots.Count;
        Assert.Equal(SyntheticSensorMap.SlotCount, exampleSlotCount);
        TcChannelRules.RequireValidMapping(exampleSlots);

        var generatedSlots = SyntheticSensorMap
            .BuildWallMap()
            .ToDictionary(slot => (slot.LogicalRow, slot.LogicalColumn));
        var generatedSensors = SyntheticSensorMap
            .BuildInitialSensors(RuntimeTestFixture.Config(), SyntheticSeed.DefaultSeed, RuntimeTestFixture.Instant)
            .ToDictionary(sensor => (sensor.LogicalRow, sensor.LogicalColumn));
        var generatedSensorCount = generatedSensors.Count;
        Assert.Equal(SyntheticSensorMap.SensorCount, generatedSensorCount);

        var exampleSensorSlots = 0;
        foreach (var slot in exampleSlots)
        {
            var position = (slot.LogicalRow, slot.LogicalColumn);
            var generated = generatedSlots[position];
            Assert.Equal(slot.SlotId, generated.SlotId);
            Assert.Equal(slot.PositionKind, generated.PositionKind);
            Assert.Equal(slot.Wall, generated.Wall);
            Assert.Equal(slot.WallColumn, generated.WallColumn);
            Assert.Equal(slot.WallRow, generated.WallRow);

            if (slot.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
            {
                Assert.Equal(slot.GapAnchorForWaterJetId, generated.GapAnchorForWaterJetId);
                Assert.Null(generated.SensorId);
                continue;
            }

            exampleSensorSlots++;
            var generatedSensor = generatedSensors[position];
            var exampleChannels = slot.TcChannels;
            Assert.NotNull(exampleChannels);
            var exampleChannelCount = exampleChannels.Count;

            Assert.Equal(SyntheticSensorMap.ChannelsPerSensor, exampleChannelCount);
            Assert.Equal(slot.SensorId, generated.SensorId);
            Assert.Equal(slot.SensorId, generatedSensor.SensorId);
            Assert.Equal(slot.ScanOrderSynthetic, generatedSensor.ScanOrder);
            Assert.Equal(exampleChannels[0], generatedSensor.TcFrontChannel);
            Assert.Equal(exampleChannels[1], generatedSensor.TcRearChannel);
            Assert.Equal(CanonicalSensorMap.SensorIdFor(slot.LogicalRow, slot.LogicalColumn), generatedSensor.SensorId);
        }

        Assert.Equal(SyntheticSensorMap.SensorCount, exampleSensorSlots);
    }

    [Fact]
    public void Synthetic_Score_Range_And_Rounding_Are_Deterministic()
    {
        var seed = new SyntheticSeed(1234);
        for (var scanOrder = 1; scanOrder <= SyntheticSensorMap.SensorCount; scanOrder++)
        {
            var score = SyntheticSensorMap.InitialScoreFor(seed, scanOrder);
            Assert.InRange(score, SyntheticSensorMap.MinimumInitialScore, SyntheticSensorMap.MaximumInitialScore);
            Assert.Equal(score, SyntheticSensorMap.InitialScoreFor(seed, scanOrder));
            Assert.Equal(score, Math.Round(score, SyntheticSensorMap.ScoreDecimals));
        }
    }

    private static SequenceControlAvailability[] SequenceControlsOf(SequenceState sequence) =>
    [
        sequence.Controls.Start,
        sequence.Controls.PauseAfterCurrentJob,
        sequence.Controls.Resume,
        sequence.Controls.AbortActiveJob,
        sequence.Controls.ResetCritical,
        sequence.Controls.PumpStart,
    ];

    /// <summary>
    /// Reads the committed synthetic sensor-map example through the shared contract
    /// serializer. The slots node is deserialized directly (never re-serialized to a
    /// string first), and a missing slots array or a null deserialization result fails
    /// loudly instead of silently yielding an empty map.
    /// </summary>
    private static List<SensorMapSlotExample> ReadExampleSlots()
    {
        var path = ExampleSensorMapPath();
        var root = JsonNode.Parse(File.ReadAllText(path));
        var slotsNode = root?["logicalMatrix"]?["slots"]
            ?? throw new InvalidOperationException($"'{path}' does not carry a logicalMatrix.slots array.");

        return JsonSerializer.Deserialize<List<SensorMapSlotExample>>(slotsNode, ContractJson.Options)
            ?? throw new InvalidOperationException(
                $"'{path}' logicalMatrix.slots could not be deserialized into sensor-map slots.");
    }

    private static string ExampleSensorMapPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "config", "examples", "sensor-map.example.json");
    }
}
