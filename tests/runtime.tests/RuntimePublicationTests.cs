using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

public sealed class RuntimePublicationTests
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ContractJson.Options);

    [Fact]
    public void One_Writer_Publishes_Only_Matching_State_And_History_And_Evicts_Oldest()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var store = RuntimePublicationStore.Create(run.Initial, 2);
        var writer = store.CreateWriter();
        Assert.Throws<InvalidOperationException>(() => store.CreateWriter());
        var held = store.Snapshot;
        Assert.Empty(held.Deltas);
        foreach (var candidate in run.Candidates.Take(4))
        {
            var before = store.Snapshot;
            var delta = RuntimeDeltaProjector.ProjectCandidate(before.Current, candidate);
            var result = writer.Publish(before.Current.Revision, candidate, delta);
            Assert.True(result.Accepted, result.Code);
            Assert.Equal(candidate.Revision, result.Publication.Current.Revision);
            Assert.Equal(candidate.Revision, result.Publication.NewestDeltaRevision);
            Assert.Equal(before.Generation + 1, result.Publication.Generation);
            Assert.InRange(result.Publication.Deltas.Count, 1, 2);
            Assert.Equal(result.Publication.Current.Revision, store.Snapshot.Deltas[0].Revision);
        }
        Assert.Empty(held.Deltas);
        Assert.Equal(run.Initial.Revision, held.Current.Revision);
        Assert.Equal(store.Snapshot.Deltas[0].PreviousRevision, store.Snapshot.Deltas[1].Revision);
        Assert.Throws<NotSupportedException>(() => ((IList<RuntimeDelta>)store.Snapshot.Deltas).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SensorPresentationState>)store.Snapshot.Current.Sensors).Clear());
    }

    [Fact]
    public void Invalid_State_Stale_Revision_And_Broken_Deltas_Never_Publish()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var store = RuntimePublicationStore.Create(run.Initial);
        var writer = store.CreateWriter();
        var candidate = run.Candidates[0];
        var delta = RuntimeDeltaProjector.ProjectCandidate(run.Initial, candidate);
        var initial = store.Snapshot;
        void Refused(int revision, RuntimeState state, RuntimeDelta change)
        {
            Assert.False(writer.Publish(revision, state, change).Accepted);
            Assert.Same(initial, store.Snapshot);
        }
        Refused(initial.Current.Revision + 1, candidate, delta);
        Refused(initial.Current.Revision, candidate with { Sensors = Array.Empty<SensorPresentationState>() }, delta);
        Refused(initial.Current.Revision, candidate, delta with { PreviousRevision = -1 });
        Refused(initial.Current.Revision, candidate, delta with { Revision = -1 });
        Refused(initial.Current.Revision, candidate, delta with { Queue = run.Initial.Queue with { Revision = 42 } });
        Assert.Equal(0, store.Snapshot.Generation);
        Assert.Empty(store.Snapshot.Deltas);
    }

    [Fact]
    public void Scenario_ActiveJob_Queue_And_Sequence_Transitions_Replay_In_Publication()
    {
        foreach (var id in new[] { SimulatorScenarioId.NORMAL_COMPLETION, SimulatorScenarioId.PUMP_TRIP })
        {
            var run = SimulatorRunHarness.RunScenario(id);
            var store = RuntimePublicationStore.Create(run.Initial);
            var writer = store.CreateWriter();
            foreach (var candidate in run.Candidates)
            {
                var previous = store.Snapshot.Current;
                var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
                Assert.True(writer.Publish(previous.Revision, candidate, delta).Accepted);
                Assert.Equal(Json(candidate), Json(store.Snapshot.Current));
            }
        }
    }

    [Fact]
    public void Full_Trend_Window_Projects_Only_New_Point_And_Replays_Eviction()
    {
        var initial = SimulatorRunHarness.InitialState();
        TrendPoint Point(long time) => new()
        {
            T = time,
            Series = new double?[] { time, time, time, time },
            Setpoint = 0,
            JobActive = false,
            AlarmActive = false,
        };
        var previous = initial with { Trend = initial.Trend with { Capacity = 2, Points = new[] { Point(1), Point(2) } } };
        var candidate = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
            Trend = previous.Trend with { Points = new[] { Point(2), Point(3) } },
        };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        Assert.Equal(3, delta.TrendPoint?.T);
        var store = RuntimePublicationStore.Create(previous);
        Assert.True(store.CreateWriter().Publish(previous.Revision, candidate, delta).Accepted);
        Assert.Equal(Json(candidate), Json(store.Snapshot.Current));
        Assert.Equal(3, store.Snapshot.Deltas[0].TrendPoint?.T);
        var wrong = candidate with { Trend = candidate.Trend with { Points = new[] { Point(1), Point(3) } } };
        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, wrong));
        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate with { Trend = candidate.Trend with { Capacity = 3 } }));
        Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, candidate with { Trend = candidate.Trend with { Points = new[] { Point(3) } } }));
    }
}
