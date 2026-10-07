using Wjss.Adapters.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The bounded Delta history: explicit capacities, deterministic oldest-entry
/// eviction, newest-revision discovery, refused transitions never recorded, and
/// catch-up that either returns a gapless apply-order chain or requires a fresh
/// Snapshot — it never fabricates the missing Delta and never continues past a gap.
/// </summary>
public sealed class DeltaHistoryTests
{
    /// <summary>
    /// Revisions retained by a capacity-3 history after five appends, newest first.
    /// The composed initial revision is 1 and every accepted tick commits exactly one
    /// successor, so five ticks produce Delta revisions 2, 3, 4, 5 and 6; a capacity-3
    /// window evicts the oldest two and keeps 4, 5 and 6.
    /// </summary>
    private static readonly int[] RetainedRevisionsNewestFirst = [6, 5, 4];

    /// <summary>
    /// The gapless apply-order chain that advances a consumer holding revision 3 -
    /// the oldest revision the retained window still continues - to revision 6.
    /// </summary>
    private static readonly int[] CatchUpChainApplyOrder = [4, 5, 6];

    [Fact]
    public void Capacities_Are_Explicit_And_Independent_Of_The_Revision_History()
    {
        var defaultHistory = new RuntimeDeltaHistory();
        Assert.Equal(RuntimeLimits.DefaultDeltaHistoryCapacity, defaultHistory.Capacity);
        Assert.Equal(0, defaultHistory.Count);
        Assert.Null(defaultHistory.NewestRevision);
        Assert.Null(defaultHistory.OldestRevision);

        var smallest = new RuntimeDeltaHistory(RuntimeLimits.MinimumDeltaHistoryCapacity);
        Assert.Equal(RuntimeLimits.MinimumDeltaHistoryCapacity, smallest.Capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDeltaHistory(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeDeltaHistory(RuntimeLimits.MaximumDeltaHistoryCapacity + 1));

        // Creating a Delta history never resizes the store's own revision history.
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var deltas = new RuntimeDeltaHistory(RuntimeLimits.MinimumDeltaHistoryCapacity);
        Assert.Equal(RuntimeLimits.MinimumDeltaHistoryCapacity, deltas.Capacity);
        Assert.Equal(RuntimeLimits.DefaultRevisionHistoryCapacity, store.HistoryCapacity);
    }

    [Fact]
    public void Oldest_Entry_Is_Evicted_Deterministically_And_The_Newest_Is_Discoverable()
    {
        var history = new RuntimeDeltaHistory(3);
        var deltas = BuildChain(5);

        foreach (var delta in deltas)
        {
            history.Append(delta);
        }

        Assert.Equal(3, history.Count);
        Assert.Equal(6, history.NewestRevision);
        Assert.Equal(4, history.OldestRevision);
        Assert.Equal(RetainedRevisionsNewestFirst, history.NewestFirst.Select(delta => delta.Revision).ToArray());

        // The eviction boundary, derived from the same sequence: Delta revisions 2
        // and 3 were evicted, so revision 4 is both the oldest retained entry and the
        // step that continues revision 3.
        Assert.Null(history.Find(3));
        var found = history.Find(4);
        Assert.NotNull(found);
        Assert.Equal(3, found.PreviousRevision);

        // Re-appending the newest Delta is refused and changes nothing.
        Assert.Throws<InvalidOperationException>(() => history.Append(deltas[^1]));
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void A_Refused_Transition_Is_Never_Recorded()
    {
        var history = new RuntimeDeltaHistory(4);
        var deltas = BuildChain(3);

        history.Append(deltas[0]);
        Assert.Equal(1, history.Count);

        // Malformed: revision is not previousRevision + 1.
        var malformed = deltas[1] with { Revision = deltas[1].Revision + 2 };
        Assert.Throws<InvalidOperationException>(() => history.Append(malformed));
        Assert.Equal(1, history.Count);
        Assert.Equal(2, history.NewestRevision);

        // Not a continuation: previousRevision is ahead of the newest entry.
        var ahead = deltas[2] with { PreviousRevision = 9, Revision = 10 };
        Assert.Throws<InvalidOperationException>(() => history.Append(ahead));
        Assert.Equal(1, history.Count);
        Assert.Equal(2, history.NewestRevision);
    }

    [Fact]
    public void Catch_Up_Returns_A_Gapless_Apply_Order_Chain()
    {
        var history = new RuntimeDeltaHistory(3);
        foreach (var delta in BuildChain(5))
        {
            history.Append(delta);
        }

        // Revisions 4, 5 and 6 are retained, and the window still continues revision 3,
        // so a consumer holding 3 is advanced to 6 in apply order.
        var complete = history.CatchUpFrom(3);

        Assert.True(complete.Available);
        Assert.Equal(RuntimeDeltaCatchUp.CompleteCode, complete.Code);
        Assert.Equal(3, complete.FromRevision);
        Assert.Equal(6, complete.ToRevision);
        Assert.Equal(CatchUpChainApplyOrder, complete.Chain.Select(delta => delta.Revision).ToArray());
        Assert.Equal(3, complete.Chain[0].PreviousRevision);
        Assert.Equal(complete.Chain[0].Revision, complete.Chain[1].PreviousRevision);
        Assert.Equal(complete.Chain[1].Revision, complete.Chain[2].PreviousRevision);

        // The consumer that is already current gets an empty, available chain.
        var current = history.CatchUpFrom(6);
        Assert.True(current.Available);
        Assert.Empty(current.Chain);
        Assert.Equal(6, current.ToRevision);
    }

    [Fact]
    public void Catch_Up_Requires_A_Fresh_Snapshot_When_Any_Link_Is_Missing()
    {
        var history = new RuntimeDeltaHistory(3);
        foreach (var delta in BuildChain(5))
        {
            history.Append(delta);
        }

        // The Delta that continues revision 1 is revision 2, which was evicted, so the
        // chain from 1 has a gap: it is not inferred, filled in or continued past.
        var gap = history.CatchUpFrom(1);
        Assert.False(gap.Available);
        Assert.Equal(RuntimeRefusalCodes.ResnapshotRequired, gap.Code);
        Assert.Empty(gap.Chain);
        Assert.Equal(6, gap.ToRevision);

        // A consumer ahead of the newest retained revision cannot go backwards.
        var ahead = history.CatchUpFrom(9);
        Assert.False(ahead.Available);
        Assert.Equal(RuntimeRefusalCodes.ResnapshotRequired, ahead.Code);
        Assert.Empty(ahead.Chain);

        // An empty history always requires a Snapshot.
        var empty = new RuntimeDeltaHistory(3).CatchUpFrom(1);
        Assert.False(empty.Available);
        Assert.Equal(RuntimeRefusalCodes.ResnapshotRequired, empty.Code);
        Assert.Empty(empty.Chain);
    }

    [Fact]
    public void NewestFirst_Is_A_Read_Only_Copy()
    {
        var history = new RuntimeDeltaHistory(3);
        foreach (var delta in BuildChain(2))
        {
            history.Append(delta);
        }

        var snapshot = history.NewestFirst;
        Assert.Equal(2, snapshot.Count);

        history.Append(BuildChain(3)[^1]);

        // The copy taken earlier is unaffected by the later append.
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, history.Count);
    }

    private static List<RuntimeDelta> BuildChain(int count)
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;
        var deltas = new List<RuntimeDelta>();

        for (var tick = 1; tick <= count; tick++)
        {
            var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
                writer, state, tick, RuntimeTestFixture.Instant.AddSeconds(tick));

            deltas.Add(delta);
            state = committed;
        }

        return deltas;
    }
}
