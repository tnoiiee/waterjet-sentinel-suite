namespace Wjss.Runtime.Core;

/// <summary>
/// Result of asking a Delta history to advance a consumer revision.
///
/// An available chain is a contiguous, gapless run of retained Deltas in APPLY
/// ORDER (oldest first): applying them in that order from
/// <see cref="FromRevision"/> reaches <see cref="ToRevision"/> exactly.
///
/// An unavailable chain is not repaired: no Delta is inferred, none is replayed
/// and nothing after the missing link is offered. The result carries
/// <see cref="RuntimeRefusalCodes.ResnapshotRequired"/> instead, which is the
/// machine-readable instruction to request a fresh Snapshot and rebuild from it.
/// </summary>
public sealed record RuntimeDeltaCatchUp
{
    /// <summary>Machine code of a complete chain.</summary>
    public const string CompleteCode = "DELTA_CHAIN_COMPLETE";

    /// <summary>True when a contiguous chain to the newest retained revision exists.</summary>
    public required bool Available { get; init; }

    /// <summary>Machine-readable outcome code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable detail.</summary>
    public required string Reason { get; init; }

    /// <summary>The revision the consumer held when it asked.</summary>
    public required int FromRevision { get; init; }

    /// <summary>The revision the chain reaches (the newest retained revision when available).</summary>
    public required int ToRevision { get; init; }

    /// <summary>The gapless chain in apply order; empty when the consumer was already current.</summary>
    public required IReadOnlyList<RuntimeDelta> Chain { get; init; }

    /// <summary>Builds a complete-chain result.</summary>
    public static RuntimeDeltaCatchUp Complete(int fromRevision, int toRevision, IReadOnlyList<RuntimeDelta> chain) => new()
    {
        Available = true,
        Code = CompleteCode,
        Reason = $"A contiguous Delta chain from revision {fromRevision} to {toRevision} is retained.",
        FromRevision = fromRevision,
        ToRevision = toRevision,
        Chain = chain ?? throw new ArgumentNullException(nameof(chain)),
    };

    /// <summary>Builds the fresh-Snapshot-required result.</summary>
    public static RuntimeDeltaCatchUp NeedsSnapshot(int fromRevision, int toRevision, string reason) => new()
    {
        Available = false,
        Code = RuntimeRefusalCodes.ResnapshotRequired,
        Reason = reason,
        FromRevision = fromRevision,
        ToRevision = toRevision,
        Chain = Array.Empty<RuntimeDelta>(),
    };
}

/// <summary>
/// Bounded in-memory history of accepted Deltas, owned by the Runtime's single
/// writer. The capacity is explicit and independent of the Runtime State Store's
/// revision-history capacity: the two buffers answer different questions (what a
/// consumer may apply, and what the store did) and a change to one must never
/// silently resize the other.
///
/// Ordering is explicit: entries are retained NEWEST FIRST (index 0 is the newest
/// revision), <see cref="NewestRevision"/> is therefore always the first entry,
/// and when the history is at capacity the OLDEST entry is evicted — the newest
/// revision is never evicted by an append, so the most recent Delta is always
/// discoverable.
///
/// Only accepted transitions can enter the history: a Delta that is malformed or
/// that does not continue the newest retained revision is refused, so a refused
/// transition is never recorded and the history always holds a gapless run. This
/// is an in-memory buffer only — nothing is persisted, and no Historian exists in
/// Stage 0.3A-2.
/// </summary>
public sealed class RuntimeDeltaHistory
{
    private readonly List<RuntimeDelta> _entries = [];
    private readonly object _gate = new();

    /// <summary>Creates a history with the default capacity.</summary>
    public RuntimeDeltaHistory()
        : this(RuntimeLimits.DefaultDeltaHistoryCapacity)
    {
    }

    /// <summary>Creates a history with an explicit capacity inside the accepted bounds.</summary>
    public RuntimeDeltaHistory(int capacity)
    {
        if (capacity < RuntimeLimits.MinimumDeltaHistoryCapacity || capacity > RuntimeLimits.MaximumDeltaHistoryCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                $"The Delta-history capacity must be between {RuntimeLimits.MinimumDeltaHistoryCapacity} and "
                + $"{RuntimeLimits.MaximumDeltaHistoryCapacity}; a bounded buffer cannot be unbounded by request.");
        }

        Capacity = capacity;
    }

    /// <summary>The configured capacity.</summary>
    public int Capacity { get; }

    /// <summary>Number of retained Deltas.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Newest retained revision, or null when the history is empty.</summary>
    public int? NewestRevision
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count == 0 ? null : _entries[0].Revision;
            }
        }
    }

    /// <summary>Oldest retained revision, or null when the history is empty.</summary>
    public int? OldestRevision
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count == 0 ? null : _entries[^1].Revision;
            }
        }
    }

    /// <summary>Retained Deltas, newest first, as a read-only copy.</summary>
    public IReadOnlyList<RuntimeDelta> NewestFirst
    {
        get
        {
            lock (_gate)
            {
                return RuntimeCollections.Freeze(_entries);
            }
        }
    }

    /// <summary>
    /// Appends one accepted Delta. A malformed Delta, or a Delta that does not
    /// continue the newest retained revision exactly, is refused and nothing is
    /// appended. The first Delta of an empty history establishes its base
    /// revision; every later Delta must continue the chain.
    /// </summary>
    public void Append(RuntimeDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.Revision != delta.PreviousRevision + 1)
        {
            throw new InvalidOperationException(
                $"[{RuntimeRefusalCodes.RevisionNotNext}] Refused malformed Delta revision {delta.Revision}: the revision "
                + "must be previousRevision + 1. Nothing was appended.");
        }

        lock (_gate)
        {
            if (_entries.Count > 0 && _entries[0].Revision != delta.PreviousRevision)
            {
                throw new InvalidOperationException(
                    $"[{RuntimeRefusalCodes.DeltaOutOfOrder}] Refused Delta {delta.Revision}: the newest retained Delta is "
                    + $"revision {_entries[0].Revision}, so the chain continues at {_entries[0].Revision + 1}. Nothing was appended.");
            }

            _entries.Insert(0, delta);

            if (_entries.Count > Capacity)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }
    }

    /// <summary>Finds the retained Delta of one revision, or null.</summary>
    public RuntimeDelta? Find(int revision)
    {
        lock (_gate)
        {
            return FindUnlocked(revision);
        }
    }

    /// <summary>
    /// Builds the gapless apply-order chain that advances a consumer from
    /// <paramref name="revision"/> to the newest retained revision. When any link
    /// is missing (an evicted or never-recorded revision), the result requires a
    /// fresh Snapshot: no Delta is inferred and nothing past the gap is offered.
    /// </summary>
    public RuntimeDeltaCatchUp CatchUpFrom(int revision)
    {
        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return RuntimeDeltaCatchUp.NeedsSnapshot(
                    revision, revision, "No Delta is retained, so the consumer revision cannot be advanced by Delta; a fresh Snapshot is required.");
            }

            var newest = _entries[0].Revision;

            if (revision > newest)
            {
                return RuntimeDeltaCatchUp.NeedsSnapshot(
                    revision,
                    newest,
                    $"The consumer holds revision {revision}, which is ahead of the newest retained revision {newest}; a fresh Snapshot is required.");
            }

            var chain = new List<RuntimeDelta>();
            var current = revision;

            while (current < newest)
            {
                var next = FindUnlocked(current);
                if (next is null)
                {
                    return RuntimeDeltaCatchUp.NeedsSnapshot(
                        revision,
                        newest,
                        $"No retained Delta continues revision {current}, so the chain from {revision} has a gap; "
                        + "the missing Delta is not inferred and the stream is not continued. A fresh Snapshot is required.");
                }

                chain.Add(next);
                current = next.Revision;
            }

            return RuntimeDeltaCatchUp.Complete(revision, newest, chain);
        }
    }

    private RuntimeDelta? FindUnlocked(int revision)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Revision == revision)
            {
                return _entries[index];
            }
        }

        return null;
    }
}
