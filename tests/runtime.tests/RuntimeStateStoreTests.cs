using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Runtime State Store contract: single authoritative writer, monotonic and
/// gapless revision progression, atomic refusal of invalid updates, bounded
/// revision history and no mutable collection leakage.
/// </summary>
public sealed class RuntimeStateStoreTests
{
    [Fact]
    public void Initial_Store_Publishes_The_Composed_Revision_One()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var store = RuntimeStateStore.Create(state);

        Assert.Equal(1, store.CurrentRevision);
        Assert.Equal(RuntimeLimits.DefaultRevisionHistoryCapacity, store.HistoryCapacity);
        Assert.Equal(1, store.Counters.CommittedRevisions);
        Assert.Equal(0, store.Counters.RefusedCommits);
        Assert.Equal(1, store.Counters.HistoryDepth);

        var entry = Assert.Single(store.History);
        Assert.Equal(1, entry.Revision);
        Assert.Equal(RuntimeRevisionKind.INITIAL, entry.Kind);
        Assert.Equal(RuntimeTestFixture.Instant, entry.GeneratedAtUtc);
    }

    [Fact]
    public void Commit_Advances_The_Revision_By_Exactly_One()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();

        var committed = writer.Commit(store.Current with
        {
            Revision = 2,
            GeneratedAtUtc = RuntimeTestFixture.Instant.AddSeconds(1),
        });

        Assert.Equal(2, committed.Revision);
        Assert.Same(committed, store.Current);
        Assert.Equal(2, store.Counters.CommittedRevisions);
        Assert.Equal(2, store.Counters.HistoryDepth);
        Assert.Equal(2, store.History[0].Revision);
        Assert.Equal(RuntimeRevisionKind.UPDATE, store.History[0].Kind);
        Assert.Equal(1, store.History[1].Revision);
    }

    [Fact]
    public void Revision_Progression_Stays_Monotonic_Across_Many_Commits()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();

        for (var step = 1; step <= 25; step++)
        {
            writer.Commit(store.Current with
            {
                Revision = store.CurrentRevision + 1,
                GeneratedAtUtc = RuntimeTestFixture.Instant.AddSeconds(step),
            });
        }

        Assert.Equal(26, store.CurrentRevision);
        Assert.Equal(26, store.Counters.CommittedRevisions);
        Assert.Equal(0, store.Counters.RefusedCommits);

        var revisions = store.History.Select(entry => entry.Revision).ToArray();
        var retainedRevisions = revisions.Length;
        Assert.Equal(26, retainedRevisions);
        for (var index = 1; index < revisions.Length; index++)
        {
            Assert.Equal(revisions[index - 1] - 1, revisions[index]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Duplicate_And_Backward_Revisions_Are_Refused_Without_Change(int refusedRevision)
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var before = store.Current;

        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = writer.Commit(before with { Revision = refusedRevision });
        });

        Assert.Contains(RuntimeRefusalCodes.RevisionNotMonotonic, refused.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Current);
        Assert.Equal(1, store.CurrentRevision);
        Assert.Equal(1, store.Counters.RefusedCommits);
        Assert.Equal(1, store.Counters.RevisionRefusals);
        Assert.Equal(0, store.Counters.InvalidStateRefusals);
        Assert.Equal(1, store.Counters.HistoryDepth);
    }

    [Fact]
    public void Skipped_Revisions_Are_Refused_Without_Change()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var before = store.Current;

        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = writer.Commit(before with { Revision = 3 });
        });

        Assert.Contains(RuntimeRefusalCodes.RevisionNotNext, refused.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Current);
        Assert.Equal(1, store.Counters.RevisionRefusals);
        Assert.Equal(0, store.Counters.InvalidStateRefusals);
    }

    [Fact]
    public void Invalid_State_Is_Refused_Atomically()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var before = store.Current;
        var truncated = before.Sensors.Take(before.Sensors.Count - 1).ToArray();

        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = writer.Commit(before with { Revision = 2, Sensors = truncated });
        });

        Assert.Contains(RuntimeRefusalCodes.SensorCount, refused.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Current);
        Assert.Equal(1, store.CurrentRevision);
        var publishedSensorCount = store.Current.Sensors.Count;
        Assert.Equal(SyntheticSensorMap.SensorCount, publishedSensorCount);
        Assert.Equal(1, store.Counters.RefusedCommits);
        Assert.Equal(0, store.Counters.RevisionRefusals);
        Assert.Equal(1, store.Counters.InvalidStateRefusals);
        Assert.Equal(1, store.Counters.HistoryDepth);
        Assert.Equal(RuntimeRevisionKind.INITIAL, store.History[0].Kind);
    }

    [Fact]
    public void Only_One_Authoritative_Writer_Is_Issued()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);

        _ = store.CreateWriter();
        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = store.CreateWriter();
        });

        Assert.Contains(RuntimeRefusalCodes.WriterAlreadyActive, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Revision_History_Is_Bounded_And_Keeps_The_Newest_Revisions()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed, historyCapacity: 8);
        var writer = store.CreateWriter();

        for (var step = 1; step <= 40; step++)
        {
            writer.Commit(store.Current with
            {
                Revision = store.CurrentRevision + 1,
                GeneratedAtUtc = RuntimeTestFixture.Instant.AddSeconds(step),
            });
        }

        var history = store.History;
        var retainedEntries = history.Count;
        Assert.Equal(8, retainedEntries);
        Assert.Equal(8, store.Counters.HistoryDepth);
        Assert.Equal(41, history[0].Revision);
        Assert.Equal(34, history[7].Revision);
        Assert.Equal(41, store.CurrentRevision);
        Assert.Equal(41, store.Counters.CommittedRevisions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RuntimeLimits.MaximumRevisionHistoryCapacity + 1)]
    public void Revision_History_Capacity_Is_Bounded(int requestedCapacity)
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = RuntimeStateStore.Create(state, requestedCapacity);
        });
    }

    [Fact]
    public void Store_Does_Not_Leak_Mutable_Collections()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();

        var sensors = new List<SensorPresentationState>(store.Current.Sensors);
        var series = new double?[] { 1.0, null, 2.0, 3.0 };
        var points = new List<TrendPoint>
        {
            new()
            {
                T = 1791331200,
                Series = series,
                Setpoint = 0.0,
                JobActive = false,
                AlarmActive = false,
            },
        };
        var trend = store.Current.Trend with { Capacity = 8, Points = points };

        writer.Commit(store.Current with { Revision = 2, Sensors = sensors, Trend = trend });

        // Mutating everything the caller still holds must not reach the store.
        sensors.RemoveAt(0);
        points.Clear();
        series[0] = 99.0;

        var current = store.Current;
        var currentSensorCount = current.Sensors.Count;
        Assert.Equal(SyntheticSensorMap.SensorCount, currentSensorCount);
        Assert.Single(current.Trend.Points);
        Assert.Equal(1.0, current.Trend.Points[0].Series[0] ?? double.NaN);

        var publishedSensors = Assert.IsAssignableFrom<IList<SensorPresentationState>>(current.Sensors);
        Assert.Throws<NotSupportedException>(() => publishedSensors.RemoveAt(0));
        var countAfterRefusedMutation = current.Sensors.Count;
        Assert.Equal(SyntheticSensorMap.SensorCount, countAfterRefusedMutation);
    }

    [Fact]
    public void History_Copies_Do_Not_Observe_Later_Commits()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var beforeCommit = store.History;

        writer.Commit(store.Current with { Revision = 2, GeneratedAtUtc = RuntimeTestFixture.Instant.AddSeconds(1) });

        var beforeCommitCount = beforeCommit.Count;
        var afterCommitCount = store.History.Count;
        Assert.Equal(1, beforeCommitCount);
        Assert.Equal(2, afterCommitCount);
    }
}
