using System.Text.Json;
using System.Threading;
using Wjss.Contracts;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

public sealed class RuntimePublicationTests
{
    private static readonly string[] ChangedSeriesNames =
    [
        "A",
        "B",
        "C",
        "D",
    ];

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
            Assert.Equal(Fingerprint(result.Publication), Fingerprint(store.Snapshot));
            Assert.Equal(result.Publication.Current.Revision, result.Publication.Deltas[0].Revision);
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
            var refusal = writer.Publish(revision, state, change);
            Assert.False(refusal.Accepted);
            Assert.Equal(Fingerprint(initial), Fingerprint(refusal.Publication));
            Assert.Equal(Fingerprint(initial), Fingerprint(store.Snapshot));
            Assert.Equal(Json(initial.Current), Json(store.Snapshot.Current));
            Assert.Equal(initial.Deltas.Count, store.Snapshot.Deltas.Count);
            Assert.Equal(initial.Generation, store.Snapshot.Generation);
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

    private static TrendPoint Point(long time) => new()
    {
        T = time,
        Series = new double?[] { time, time, time, time },
        Setpoint = 0,
        JobActive = false,
        AlarmActive = false,
    };

    private static (RuntimeState Previous, RuntimeState Next) FullWindow(int capacity, params TrendPoint[] points)
    {
        var initial = SimulatorRunHarness.InitialState();
        var previous = initial with { Trend = initial.Trend with { Capacity = capacity, Points = points } };
        var next = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
        };
        return (previous, next);
    }

    [Fact]
    public void Unchanged_Full_Window_Has_No_Trend_Point()
    {
        var (previous, next) = FullWindow(2, Point(1), Point(2));
        // Independently constructed, semantically identical points: array reference equality is irrelevant.
        var candidate = next with { Trend = next.Trend with { Points = new[] { Point(1), Point(2) } } };
        Assert.Null(RuntimeDeltaProjector.ProjectCandidate(previous, candidate).TrendPoint);
    }

    [Fact]
    public void Full_Window_Rejects_Incorrect_Shift_Historical_Mutation_Reorder_Removal_And_Capacity_Change()
    {
        var (previous, next) = FullWindow(3, Point(1), Point(2), Point(3));
        void Refuse(TrendWindow trend) =>
            Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, next with { Trend = trend }));
        Refuse(previous.Trend with { Points = new[] { Point(1), Point(2), Point(4) } }); // wrong shift
        Refuse(previous.Trend with { Points = new[] { Point(2) with { Setpoint = 42 }, Point(3), Point(4) } }); // history
        Refuse(previous.Trend with { Points = new[] { Point(3), Point(2), Point(4) } }); // reorder
        Refuse(previous.Trend with { Points = new[] { Point(2), Point(3) } }); // removal
        Refuse(previous.Trend with { Capacity = 4, Points = new[] { Point(2), Point(3), Point(4) } });
        Refuse(previous.Trend with { SeriesNames = ChangedSeriesNames, Points = new[] { Point(2), Point(3), Point(4) } });
        Refuse(previous.Trend with { Points = new[] { Point(2), Point(3), Point(4), Point(5) } });
    }

    [Fact]
    public void Single_Slot_Full_Window_Evicts_And_Appends_One_Point()
    {
        var (previous, next) = FullWindow(1, Point(1));
        var candidate = next with { Trend = next.Trend with { Points = new[] { Point(2) } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        RuntimeTestFixture.AssertTrendPointsEquivalent(new[] { Point(2) }, new[] { Assert.IsType<TrendPoint>(delta.TrendPoint) });
        var applied = RuntimeDeltaApply.Apply(previous, delta);
        Assert.True(applied.Applied, applied.Reason);
        RuntimeTestFixture.AssertTrendPointsEquivalent(candidate.Trend.Points, Assert.IsType<RuntimeState>(applied.State).Trend.Points);
    }

    [Fact]
    public void Refused_Full_Window_Publication_Preserves_State_History_And_Generation()
    {
        var (previous, next) = FullWindow(2, Point(1), Point(2));
        var candidate = next with { Trend = next.Trend with { Points = new[] { Point(2), Point(3) } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var store = RuntimePublicationStore.Create(previous);
        var writer = store.CreateWriter();
        Assert.True(writer.Publish(previous.Revision, candidate, delta).Accepted);
        var committed = store.Snapshot;
        var invalid = candidate with
        {
            Revision = candidate.Revision + 1,
            GeneratedAtUtc = candidate.GeneratedAtUtc.AddSeconds(1),
            Trend = candidate.Trend with { Points = new[] { Point(2), Point(4) } },
        };
        // Reusing the earlier Delta cannot link to the new revision.
        Assert.False(writer.Publish(candidate.Revision, invalid, delta).Accepted);
        Assert.Equal(Fingerprint(committed), Fingerprint(store.Snapshot));
        Assert.Equal(committed.Current.Revision, store.Snapshot.Current.Revision);
        Assert.Equal(committed.Generation, store.Snapshot.Generation);
        Assert.Equal(Json(committed.Current), Json(store.Snapshot.Current));
        Assert.Equal(committed.Deltas.Count, store.Snapshot.Deltas.Count);
    }


    private static void AssertConsistent(RuntimePublication snapshot, int initialRevision)
    {
        if (snapshot.Deltas.Count == 0)
        {
            Assert.Equal(initialRevision, snapshot.Current.Revision);
            Assert.Equal(0, snapshot.Generation);
            return;
        }
        Assert.Equal(snapshot.Current.Revision, snapshot.Deltas[0].Revision);
        for (var index = 1; index < snapshot.Deltas.Count; index++)
            Assert.Equal(snapshot.Deltas[index - 1].PreviousRevision, snapshot.Deltas[index].Revision);
    }

    // Never serialize the internal Delta: DeltaJobState.Present is a guarded getter.
    private static string Fingerprint(RuntimePublication publication) => Json(new
    {
        State = Json(publication.Current),
        publication.HistoryCapacity,
        publication.Generation,
        History = publication.Deltas.Select(delta => new
        {
            delta.PreviousRevision,
            delta.Revision,
            delta.GeneratedAtUtc,
            Wire = Json(RuntimeDeltaProjector.ProjectWire(delta)),
            PreviousSensors = Json(delta.PreviousSensors),
        }).ToArray(),
    });

    [Fact]
    public async Task Concurrent_Calls_Through_One_Writer_Publish_Exactly_Once()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var store = RuntimePublicationStore.Create(run.Initial);
        var writer = store.CreateWriter();
        var candidate = run.Candidates[0];
        var delta = RuntimeDeltaProjector.ProjectCandidate(run.Initial, candidate);
        using var gate = new ManualResetEventSlim(false);
        var first = Task.Run(() => { if (!gate.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Start gate timed out."); return writer.Publish(run.Initial.Revision, candidate, delta); });
        var second = Task.Run(() => { if (!gate.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Start gate timed out."); return writer.Publish(run.Initial.Revision, candidate, delta); });
        gate.Set();
        var results = await Task
            .WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results, result => result.Accepted);
        Assert.Single(results, result => !result.Accepted && result.Code == "PUBLICATION_STALE_REVISION");
        Assert.Equal(1, store.Snapshot.Generation);
        Assert.Single(store.Snapshot.Deltas);
        AssertConsistent(store.Snapshot, run.Initial.Revision);
    }

    [Fact]
    public async Task Acquired_Snapshots_Are_Generation_Consistent_While_Writer_Publishes()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var store = RuntimePublicationStore.Create(run.Initial, 2);
        var writer = store.CreateWriter();
        using var gate = new ManualResetEventSlim(false);
        var publish = Task.Run(() =>
        {
            if (!gate.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Start gate timed out.");
            foreach (var candidate in run.Candidates)
            {
                var snapshot = store.Snapshot;
                var delta = RuntimeDeltaProjector.ProjectCandidate(snapshot.Current, candidate);
                var result = writer.Publish(snapshot.Current.Revision, candidate, delta);
                Assert.True(result.Accepted, result.Code);
                Assert.Equal(Fingerprint(result.Publication), Fingerprint(store.Snapshot));
            }
        });
        gate.Set();
        for (var index = 0; index < 100; index++)
            AssertConsistent(store.Snapshot, run.Initial.Revision);
        await publish.WaitAsync(TimeSpan.FromSeconds(30));
        AssertConsistent(store.Snapshot, run.Initial.Revision);
    }

    [Fact]
    public void Every_Refusal_Returns_The_Exact_Previous_Aggregate()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var store = RuntimePublicationStore.Create(run.Initial);
        var writer = store.CreateWriter();
        var candidate = run.Candidates[0];
        var delta = RuntimeDeltaProjector.ProjectCandidate(run.Initial, candidate);
        void Refuse(int revision, RuntimeState state, RuntimeDelta change)
        {
            var before = store.Snapshot;
            var result = writer.Publish(revision, state, change);
            Assert.False(result.Accepted);
            Assert.Equal(Fingerprint(before), Fingerprint(result.Publication));
            Assert.Equal(Fingerprint(before), Fingerprint(store.Snapshot));
            Assert.Equal(Json(before.Current), Json(store.Snapshot.Current));
            Assert.Equal(before.Deltas.Count, store.Snapshot.Deltas.Count);
            Assert.Equal(before.Generation, store.Snapshot.Generation);
        }
        Refuse(run.Initial.Revision + 1, candidate, delta);
        Refuse(run.Initial.Revision, candidate with { Sensors = Array.Empty<SensorPresentationState>() }, delta);
        Refuse(run.Initial.Revision, candidate, delta with { PreviousRevision = -1 });
        Refuse(run.Initial.Revision, candidate, delta with { Revision = -1 });
        Refuse(run.Initial.Revision, candidate, delta with { Queue = run.Initial.Queue with { Revision = 42 } });
        var runtime = RuntimeSnapshotProjector.Project(run.Initial, RuntimeTestFixture.InitialCounters(), 0).Runtime;
        Refuse(run.Initial.Revision, candidate, delta with { Runtime = runtime });
    }

    [Fact]
    public void Independent_Publications_Have_Byte_Identical_Safe_Fingerprints()
    {
        var firstRun = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var secondRun = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var first = RuntimePublicationStore.Create(firstRun.Initial, 2);
        var second = RuntimePublicationStore.Create(secondRun.Initial, 2);
        var firstWriter = first.CreateWriter();
        var secondWriter = second.CreateWriter();
        foreach (var pair in firstRun.Candidates.Zip(secondRun.Candidates))
        {
            var a = first.Snapshot.Current;
            var b = second.Snapshot.Current;
            Assert.True(firstWriter.Publish(a.Revision, pair.First, RuntimeDeltaProjector.ProjectCandidate(a, pair.First)).Accepted);
            Assert.True(secondWriter.Publish(b.Revision, pair.Second, RuntimeDeltaProjector.ProjectCandidate(b, pair.Second)).Accepted);
            Assert.Equal(Fingerprint(first.Snapshot), Fingerprint(second.Snapshot));
        }
    }

    [Fact]
    public void Isolated_Queue_And_Critical_Sequence_Changes_Replay()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.PUMP_TRIP);
        var initial = run.Initial;
        var critical = run.Candidates.First(candidate => candidate.Sequence.Critical is not null).Sequence;
        var cases = new[]
        {
            initial with { Revision = initial.Revision + 1, GeneratedAtUtc = initial.GeneratedAtUtc.AddSeconds(1),
                Queue = initial.Queue with { Revision = initial.Queue.Revision + 1 } },
            initial with { Revision = initial.Revision + 1, GeneratedAtUtc = initial.GeneratedAtUtc.AddSeconds(1),
                Sequence = critical },
        };
        for (var index = 0; index < cases.Length; index++)
        {
            var candidate = cases[index];
            var store = RuntimePublicationStore.Create(initial);
            var delta = RuntimeDeltaProjector.ProjectCandidate(initial, candidate);
            Assert.Empty(delta.ChangedSensors);
            Assert.Null(delta.TrendPoint);
            Assert.Equal(DeltaJobEncoding.Absent, delta.ActiveJob.Encoding);
            if (index == 0)
            {
                Assert.NotNull(delta.Queue);
                Assert.Null(delta.Sequence);
            }
            else
            {
                Assert.Null(delta.Queue);
                Assert.NotNull(delta.Sequence);
            }
            Assert.True(store.CreateWriter().Publish(initial.Revision, candidate, delta).Accepted);
            Assert.Equal(Json(candidate), Json(store.Snapshot.Current));
        }
    }

    [Fact]
    public void ActiveJob_Three_Encodings_Are_Published_With_Matching_Replay()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var seen = new HashSet<DeltaJobEncoding>();
        var store = RuntimePublicationStore.Create(run.Initial);
        var writer = store.CreateWriter();
        foreach (var candidate in run.Candidates)
        {
            var before = store.Snapshot.Current;
            var delta = RuntimeDeltaProjector.ProjectCandidate(before, candidate);
            seen.Add(delta.ActiveJob.Encoding);
            var outcome = writer.Publish(before.Revision, candidate, delta);
            Assert.True(outcome.Accepted, outcome.Code);
            Assert.Equal(Json(candidate.ActiveJob), Json(outcome.Publication.Current.ActiveJob));
            Assert.Equal(Json(candidate), Json(outcome.Publication.Current));
        }
        Assert.Contains(DeltaJobEncoding.Absent, seen);
        Assert.Contains(DeltaJobEncoding.Present, seen);
        Assert.Contains(DeltaJobEncoding.Cleared, seen);
    }


    [Fact]
    public void Caller_Owned_State_And_Delta_Collections_Cannot_Change_Published_Generation()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var source = run.Candidates.First(candidate => candidate.ActiveJob?.SafeReturn is not null);
        var before = run.Candidates[run.Candidates.ToList().IndexOf(source) - 1];
        var sensors = source.Sensors.ToList();
        var walls = source.Walls.ToList();
        var entries = source.Queue.Entries.ToList();
        var alarms = new List<AlarmItem>
        {
            new()
            {
                AlarmId = "SYN-ALIAS-TEST", Code = "SYN-ALIAS-TEST", Text = "Synthetic test alarm",
                Severity = AlarmSeverity.LOW, State = "ACTIVE_UNACK", RaisedAt = "2026-10-08T00:00:00Z",
            },
        };
        var devices = source.Communication.Devices.Select(device => device with { PollsOk = device.PollsOk + 1 }).ToList();
        var points = new List<TrendPoint> { Point(11) };
        var active = Assert.IsType<ActiveCleaningJobState>(source.ActiveJob);
        var safeReturn = Assert.IsType<SafeReturnState>(active.SafeReturn);
        var events = safeReturn.Events.ToList();
        var candidate = source with
        {
            Sensors = sensors,
            Walls = walls,
            Queue = source.Queue with { Revision = source.Queue.Revision + 1, Entries = entries },
            Alarms = source.Alarms with { ActiveUnack = 1, Items = alarms },
            Communication = source.Communication with { Devices = devices },
            Trend = source.Trend with { Points = points },
            ActiveJob = active with { SafeReturn = safeReturn with { Events = events } },
        };
        var store = RuntimePublicationStore.Create(before);
        var projected = RuntimeDeltaProjector.ProjectCandidate(before, candidate);
        var changed = projected.ChangedSensors.ToList();
        var previousSensors = projected.PreviousSensors.ToList();
        var changedWalls = projected.ChangedWalls.ToList();
        var deltaEntries = projected.Queue?.Entries.ToList();
        var deltaAlarms = projected.Alarms?.Items.ToList();
        var deltaDevices = projected.Communication?.Devices.ToList();
        var deltaEvents = projected.ActiveJob.Value?.SafeReturn?.Events.ToList();
        var delta = projected with
        {
            ChangedSensors = changed,
            PreviousSensors = previousSensors,
            ChangedWalls = changedWalls,
            Queue = projected.Queue is null ? null : projected.Queue with { Entries = deltaEntries ?? throw new InvalidOperationException("Missing queue entries.") },
            Alarms = projected.Alarms is null ? null : projected.Alarms with { Items = deltaAlarms ?? throw new InvalidOperationException("Missing alarms.") },
            Communication = projected.Communication is null ? null : projected.Communication with { Devices = deltaDevices ?? throw new InvalidOperationException("Missing devices.") },
            ActiveJob = projected.ActiveJob.Encoding == DeltaJobEncoding.Present && deltaEvents is not null
                ? DeltaJobState.Replaced(projected.ActiveJob.Present with
                    { SafeReturn = Assert.IsType<SafeReturnState>(projected.ActiveJob.Present.SafeReturn) with { Events = deltaEvents } })
                : projected.ActiveJob,
        };
        var result = store.CreateWriter().Publish(before.Revision, candidate, delta);
        Assert.True(result.Accepted, result.Code);
        var frozen = store.Snapshot;
        var fingerprint = Fingerprint(frozen);
        sensors.Clear(); walls.Clear(); entries.Clear(); alarms.Clear(); devices.Clear(); points.Clear(); events.Clear();
        changed.Clear(); previousSensors.Clear(); changedWalls.Clear();
        deltaEntries?.Clear(); deltaAlarms?.Clear(); deltaDevices?.Clear(); deltaEvents?.Clear();
        Assert.Equal(fingerprint, Fingerprint(store.Snapshot));
        Assert.Throws<NotSupportedException>(() => ((IList<SensorPresentationState>)frozen.Current.Sensors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<WallSummary>)frozen.Current.Walls).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<QueueEntry>)frozen.Current.Queue.Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TrendPoint>)frozen.Current.Trend.Points).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<QueueEntry>)Assert.IsType<QueueSummary>(frozen.Deltas[0].Queue).Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<AlarmItem>)frozen.Current.Alarms.Items).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DeviceHealth>)frozen.Current.Communication.Devices).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SafeReturnEvent>)Assert.IsType<SafeReturnState>(Assert.IsType<ActiveCleaningJobState>(frozen.Current.ActiveJob).SafeReturn).Events).Clear());
    }

    [Fact]
    public void Trend_Point_Series_Are_Copied_In_State_And_Delta()
    {
        var (previous, next) = FullWindow(1, Point(1));
        var point = Point(2);
        var candidate = next with { Trend = next.Trend with { Points = new[] { point } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var store = RuntimePublicationStore.Create(previous);
        Assert.True(store.CreateWriter().Publish(previous.Revision, candidate, delta).Accepted);
        point.Series[0] = 999;
        Assert.Equal(2d, store.Snapshot.Current.Trend.Points[0].Series[0]);
        Assert.Equal(2d, store.Snapshot.Deltas[0].TrendPoint?.Series[0]);
    }

    [Fact]
    public void Last_Outcome_Events_Are_Copied_In_State_And_Delta()
    {
        var run = SimulatorRunHarness.RunScenario(SimulatorScenarioId.NORMAL_COMPLETION);
        var index = run.Candidates.ToList().FindIndex(candidate => candidate.Sequence.LastJobOutcome is not null);
        Assert.True(index > 0);
        var previous = run.Candidates[index - 1];
        var source = run.Candidates[index];
        var outcome = Assert.IsType<JobOutcomeRecord>(source.Sequence.LastJobOutcome);
        var events = outcome.Events.ToList();
        var candidate = source with { Sequence = source.Sequence with
            { LastJobOutcome = outcome with { Events = events } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var store = RuntimePublicationStore.Create(previous);
        Assert.True(store.CreateWriter().Publish(previous.Revision, candidate, delta).Accepted);
        var count = Assert.IsType<JobOutcomeRecord>(store.Snapshot.Current.Sequence.LastJobOutcome).Events.Count;
        events.Clear();
        Assert.Equal(count, Assert.IsType<JobOutcomeRecord>(store.Snapshot.Current.Sequence.LastJobOutcome).Events.Count);
        Assert.Equal(count, Assert.IsType<JobOutcomeRecord>(Assert.IsType<SequenceState>(store.Snapshot.Deltas[0].Sequence).LastJobOutcome).Events.Count);
    }


    [Fact]
    public void Reader_Mutation_Of_Nested_Arrays_Cannot_Change_Store_Or_Other_Readers()
    {
        var (previous, next) = FullWindow(1, Point(1));
        var candidate = next with { Trend = next.Trend with { Points = new[] { Point(2) } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var store = RuntimePublicationStore.Create(previous);
        var writer = store.CreateWriter();
        var accepted = writer.Publish(previous.Revision, candidate, delta);
        Assert.True(accepted.Accepted, accepted.Code);
        var readerA = store.Snapshot;
        var baseline = Fingerprint(store.Snapshot);
        readerA.Current.Trend.Points[0].Series[0] = 999;
        Assert.IsType<TrendPoint>(readerA.Deltas[0].TrendPoint).Series[0] = 888;
        // PlacementAnchors are read-only wrappers in each detached copy.
        Assert.Throws<NotSupportedException>(() => ((IList<string>)readerA.Current.WaterJets[0].PlacementAnchors).Clear());
        accepted.Publication.Current.Trend.Points[0].Series[0] = 777;
        Assert.Equal(baseline, Fingerprint(store.Snapshot));
        Assert.NotEqual(Fingerprint(readerA), Fingerprint(store.Snapshot));
        Assert.NotEqual(Fingerprint(accepted.Publication), Fingerprint(store.Snapshot));
        AssertConsistent(store.Snapshot, previous.Revision);
    }

    [Fact]
    public void Refused_Result_Is_Detached_From_Internal_Publication()
    {
        var (previous, next) = FullWindow(1, Point(1));
        var store = RuntimePublicationStore.Create(previous);
        var baseline = Fingerprint(store.Snapshot);
        var candidate = next with { Trend = next.Trend with { Points = new[] { Point(2) } } };
        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
        var refused = store.CreateWriter().Publish(previous.Revision + 1, candidate, delta);
        Assert.False(refused.Accepted);
        refused.Publication.Current.Trend.Points[0].Series[0] = 999;
        Assert.Equal(baseline, Fingerprint(store.Snapshot));
    }


    [Fact]
    public void Topology_Anchor_Lists_Are_Frozen_At_Ingest_And_Read()
    {
        var initial = SimulatorRunHarness.InitialState();
        var anchors = initial.WaterJets[0].PlacementAnchors.ToList();
        var jets = initial.WaterJets.ToArray();
        jets[0] = jets[0] with { PlacementAnchors = anchors };
        var store = RuntimePublicationStore.Create(initial with { WaterJets = jets });
        var baseline = Fingerprint(store.Snapshot);
        anchors.Clear();
        jets[0] = jets[1];
        Assert.Equal(baseline, Fingerprint(store.Snapshot));
        var reader = store.Snapshot;
        Assert.Throws<NotSupportedException>(() => ((IList<string>)reader.Current.WaterJets[0].PlacementAnchors).Clear());
        Assert.Equal(baseline, Fingerprint(store.Snapshot));
    }

}
