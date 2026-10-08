using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b Delta tests. Every candidate Runtime revision projected from a SIMULATOR run is replayed through
/// <see cref="RuntimeDeltaProjector.ProjectCandidate"/> and <see cref="RuntimeDeltaApply.Apply"/>, and the replayed
/// revision must equal the candidate section by section. NOT EXECUTED IN ARENA: Owner-local validation required.
/// </summary>
public sealed class SequencingDeltaProjectionTests
{
    public static IEnumerable<object[]> AllScenarioIds() =>
        Enum.GetValues<SimulatorScenarioId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_Applied_Step_Replays_Through_ProjectCandidate_And_Apply(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var consumer = run.Initial;

        foreach (var candidate in run.Candidates)
        {
            var delta = RuntimeDeltaProjector.ProjectCandidate(consumer, candidate);
            var outcome = RuntimeDeltaApply.Apply(consumer, delta);

            Assert.True(outcome.Applied, outcome.Reason ?? string.Empty);
            var replayed = SimulatorRunHarness.Required(outcome.State);
            AssertSameSections(candidate, replayed);
            consumer = replayed;
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Active_Job_Encoding_Is_Absent_Present_Or_Cleared_By_Content(SimulatorScenarioId id)
    {
        var run = SimulatorRunHarness.RunScenario(id);
        var previous = run.Initial;

        foreach (var candidate in run.Candidates)
        {
            var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);

            Assert.Equal(ExpectedEncoding(previous.ActiveJob, candidate.ActiveJob), delta.ActiveJob.Encoding);
            if (delta.ActiveJob.Encoding == DeltaJobEncoding.Present)
            {
                Assert.Equal(SimulatorRunHarness.Json(candidate.ActiveJob), SimulatorRunHarness.Json(delta.ActiveJob.Present));
            }

            previous = candidate;
        }
    }

    [Fact]
    public void Active_Job_Is_Replaced_At_Dispatch_Unchanged_On_Plain_Valve_Feedback_And_Cleared_At_Release()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);

        Assert.Equal(
            DeltaJobEncoding.Present,
            RuntimeDeltaProjector.ProjectCandidate(run.PreviousTo(3), run.CandidateAt(3)).ActiveJob.Encoding);
        Assert.Equal(
            DeltaJobEncoding.Absent,
            RuntimeDeltaProjector.ProjectCandidate(run.PreviousTo(4), run.CandidateAt(4)).ActiveJob.Encoding);
        Assert.Equal(
            DeltaJobEncoding.Cleared,
            RuntimeDeltaProjector.ProjectCandidate(run.PreviousTo(16), run.CandidateAt(16)).ActiveJob.Encoding);
    }

    [Fact]
    public void Admission_During_A_Job_Changes_Only_The_Queue_Section_And_One_Sensor()
    {
        var set = SimulatorRunHarness.ScenarioSet();
        var steps = SimulatorRunHarness.LeadIn(set);
        steps.Add(SimulatorRunHarness.AdmitSensor(set, 7, 2));
        var run = SimulatorRunHarness.Run(steps);
        var delta = RuntimeDeltaProjector.ProjectCandidate(run.PreviousTo(7), run.CandidateAt(7));

        var changed = Assert.Single(delta.ChangedSensors);
        Assert.Equal(set.Sensors[2].SensorId, changed.SensorId);
        Assert.Equal(QueueState.QUEUED, changed.QueueState);
        Assert.False(changed.IsActiveJobTarget);
        Assert.Equal(QueueState.NONE, Assert.Single(delta.PreviousSensors).QueueState);
        Assert.NotNull(delta.Queue);
        Assert.Null(delta.Sequence);
        Assert.Null(delta.Pump);
        Assert.Null(delta.Alarms);
        Assert.Null(delta.Communication);
        Assert.Null(delta.Runtime);
        Assert.Empty(delta.ChangedWalls);
        Assert.Null(delta.TrendPoint);
        Assert.Equal(DeltaJobEncoding.Absent, delta.ActiveJob.Encoding);
    }

    [Fact]
    public void Rebuilt_But_Identical_Records_Produce_No_Spurious_Delta_Section()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var previous = run.PreviousTo(7);
        var job = SimulatorRunHarness.Required(previous.ActiveJob);
        var candidate = NextRevision(previous) with
        {
            Sensors = previous.Sensors.Select(sensor => sensor with { }).ToArray(),
            ActiveJob = job with { Dispatch = job.Dispatch with { } },
            Queue = previous.Queue with { Entries = previous.Queue.Entries.Select(entry => entry with { }).ToArray() },
            Sequence = previous.Sequence with { Controls = previous.Sequence.Controls with { } },
            Pump = previous.Pump with { },
        };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);

        Assert.True(delta.IsEmpty);
        Assert.Null(delta.TrendPoint);
        Assert.Equal(DeltaJobEncoding.Absent, delta.ActiveJob.Encoding);
    }

    [Fact]
    public void Single_Appended_Trend_Point_Is_Carried_And_Replayed()
    {
        var previous = SimulatorRunHarness.InitialState();
        var point = TrendPointAt(previous, 1, 0.0);
        var candidate = NextRevision(previous) with { Trend = previous.Trend with { Points = new[] { point } } };

        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var carried = SimulatorRunHarness.Required(delta.TrendPoint);
        RuntimeTestFixture.AssertTrendPointsEquivalent(new[] { point }, new[] { carried });

        var outcome = RuntimeDeltaApply.Apply(previous, delta);
        Assert.True(outcome.Applied, outcome.Reason ?? string.Empty);
        var replayed = SimulatorRunHarness.Required(outcome.State);
        RuntimeTestFixture.AssertTrendPointsEquivalent(candidate.Trend.Points, replayed.Trend.Points);
    }

    [Fact]
    public void Trend_Removal_Is_Refused()
    {
        var initial = SimulatorRunHarness.InitialState();
        var previous = initial with { Trend = initial.Trend with { Points = new[] { TrendPointAt(initial, 1, 0.0) } } };
        var candidate = NextRevision(previous) with { Trend = previous.Trend with { Points = Array.Empty<TrendPoint>() } };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Two_Appended_Trend_Points_In_One_Revision_Are_Refused()
    {
        var previous = SimulatorRunHarness.InitialState();
        var candidate = NextRevision(previous) with
        {
            Trend = previous.Trend with { Points = new[] { TrendPointAt(previous, 1, 0.0), TrendPointAt(previous, 2, 0.0) } },
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Changed_Trend_History_At_The_Same_Count_Is_Refused()
    {
        var initial = SimulatorRunHarness.InitialState();
        var previous = initial with { Trend = initial.Trend with { Points = new[] { TrendPointAt(initial, 1, 0.0) } } };
        var candidate = NextRevision(previous) with
        {
            Trend = previous.Trend with { Points = new[] { TrendPointAt(initial, 1, 5.0) } },
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Reordered_Trend_History_Is_Refused_Even_When_One_Point_Is_Appended()
    {
        var initial = SimulatorRunHarness.InitialState();
        var first = TrendPointAt(initial, 1, 0.0);
        var second = TrendPointAt(initial, 2, 0.0);
        var third = TrendPointAt(initial, 3, 0.0);
        var previous = initial with { Trend = initial.Trend with { Points = new[] { first, second } } };
        var candidate = NextRevision(previous) with
        {
            Trend = previous.Trend with { Points = new[] { second, first, third } },
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Changed_Trend_Capacity_Is_Refused_As_An_Identity_Change()
    {
        var previous = SimulatorRunHarness.InitialState();
        var candidate = NextRevision(previous) with
        {
            Trend = previous.Trend with { Capacity = previous.Trend.Capacity + 1 },
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Revision_Gap_Is_Refused_And_Nothing_Is_Projected()
    {
        var previous = SimulatorRunHarness.InitialState();
        var candidate = previous with
        {
            Revision = previous.Revision + 2,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Sensor_Order_Change_Is_Refused()
    {
        var previous = SimulatorRunHarness.InitialState();
        var reordered = previous.Sensors.OrderByDescending(sensor => sensor.ScanOrder).ToArray();
        var candidate = NextRevision(previous) with { Sensors = reordered };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Sensor_Identity_Set_Change_Is_Refused()
    {
        var previous = SimulatorRunHarness.InitialState();
        var fewer = previous.Sensors.Take(previous.Sensors.Count - 1).ToArray();
        var candidate = NextRevision(previous) with { Sensors = fewer };

        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate));
    }

    [Fact]
    public void Candidate_Delta_Projects_To_The_Wire_Envelope_With_The_Same_Revision_Step()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var previous = run.PreviousTo(3);
        var candidate = run.CandidateAt(3);
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var wire = RuntimeDeltaProjector.ProjectWire(delta);

        Assert.Equal(RuntimeDeltaProjector.DeltaKind, wire.Kind);
        Assert.Equal(previous.Revision, wire.PreviousRevision);
        Assert.Equal(candidate.Revision, wire.Revision);
        Assert.Equal(SimulatorRunHarness.Json(delta.Queue), SimulatorRunHarness.Json(wire.Queue));
    }

    private static RuntimeState NextRevision(RuntimeState previous) => previous with
    {
        Revision = previous.Revision + 1,
        GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
    };

    private static TrendPoint TrendPointAt(RuntimeState state, long t, double value) => new()
    {
        T = t,
        Series = Enumerable.Repeat<double?>(value, state.Trend.SeriesNames.Count).ToArray(),
        Setpoint = value,
        JobActive = false,
        AlarmActive = false,
    };

    private static DeltaJobEncoding ExpectedEncoding(ActiveCleaningJobState? before, ActiveCleaningJobState? after)
    {
        if (before is null)
        {
            return after is null ? DeltaJobEncoding.Absent : DeltaJobEncoding.Present;
        }

        if (after is null)
        {
            return DeltaJobEncoding.Cleared;
        }

        return SimulatorRunHarness.Json(before) == SimulatorRunHarness.Json(after)
            ? DeltaJobEncoding.Absent
            : DeltaJobEncoding.Present;
    }

    private static void AssertSameSections(RuntimeState expected, RuntimeState actual)
    {
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.GeneratedAtUtc, actual.GeneratedAtUtc);
        Assert.Equal(SimulatorRunHarness.Json(expected.Sensors), SimulatorRunHarness.Json(actual.Sensors));
        Assert.Equal(SimulatorRunHarness.Json(expected.Walls), SimulatorRunHarness.Json(actual.Walls));
        Assert.Equal(SimulatorRunHarness.Json(expected.ActiveJob), SimulatorRunHarness.Json(actual.ActiveJob));
        Assert.Equal(SimulatorRunHarness.Json(expected.Queue), SimulatorRunHarness.Json(actual.Queue));
        Assert.Equal(SimulatorRunHarness.Json(expected.Sequence), SimulatorRunHarness.Json(actual.Sequence));
        Assert.Equal(SimulatorRunHarness.Json(expected.Pump), SimulatorRunHarness.Json(actual.Pump));
        Assert.Equal(SimulatorRunHarness.Json(expected.Alarms), SimulatorRunHarness.Json(actual.Alarms));
        Assert.Equal(SimulatorRunHarness.Json(expected.Communication), SimulatorRunHarness.Json(actual.Communication));
        Assert.Equal(SimulatorRunHarness.Json(expected.Trend), SimulatorRunHarness.Json(actual.Trend));
    }
}
