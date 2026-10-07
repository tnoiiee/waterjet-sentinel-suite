namespace Wjss.Runtime.Core;

/// <summary>
/// The single authoritative in-memory Runtime State Store.
///
/// Guarantees of this type (Stage 0.3A-2A foundation):
/// <list type="bullet">
///   <item><b>Single authoritative writer.</b> <see cref="CreateWriter"/> issues
///   exactly one writer per store instance; a second request is refused. Readers
///   hold no writer and cannot mutate anything.</item>
///   <item><b>Immutable state for readers.</b> Every published revision is an
///   immutable <see cref="RuntimeState"/> with read-only collection wrappers, so
///   a reader cannot observe or cause a change.</item>
///   <item><b>Monotonic revision.</b> The next committed revision must be
///   exactly <c>current + 1</c>. Duplicate, backward and non-contiguous
///   revisions are refused; the store never re-publishes a revision.</item>
///   <item><b>Atomic publication.</b> A commit is validated in full before the
///   published reference is replaced. A refused commit leaves the store's state,
///   revision and history exactly as they were: no partial commit exists.</item>
///   <item><b>Bounded buffers.</b> The revision-activity history is bounded by
///   <see cref="HistoryCapacity"/>; the trend window bound is validated on every
///   commit.</item>
///   <item><b>No static mutable state.</b> Everything a test needs is created
///   per instance, so two tests can never share state through this type.</item>
/// </list>
/// </summary>
public sealed class RuntimeStateStore
{
    private readonly object _gate = new();
    private readonly int _historyCapacity;
    private readonly List<RuntimeRevisionEntry> _history;
    private volatile RuntimeState _current;
    private bool _writerIssued;
    private int _highestRevision;
    private int _committedRevisions;
    private int _refusedCommits;
    private int _revisionRefusals;
    private int _invalidStateRefusals;

    private RuntimeStateStore(RuntimeState initialState, int historyCapacity)
    {
        _historyCapacity = historyCapacity;
        _history = new List<RuntimeRevisionEntry>(historyCapacity);
        _current = initialState;
        _highestRevision = initialState.Revision;
        _committedRevisions = 1;
        _history.Add(new RuntimeRevisionEntry
        {
            Revision = initialState.Revision,
            GeneratedAtUtc = initialState.GeneratedAtUtc,
            Kind = RuntimeRevisionKind.INITIAL,
        });
    }

    /// <summary>
    /// Creates a store around an already-composed initial state. The initial
    /// state is validated and frozen before it is published, so an invalid state
    /// can never become the store's revision 1.
    /// </summary>
    public static RuntimeStateStore Create(
        RuntimeState initialState,
        int historyCapacity = RuntimeLimits.DefaultRevisionHistoryCapacity)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        if (historyCapacity < RuntimeLimits.MinimumRevisionHistoryCapacity
            || historyCapacity > RuntimeLimits.MaximumRevisionHistoryCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(historyCapacity),
                historyCapacity,
                "Revision history capacity must be between the minimum and maximum declared by RuntimeLimits.");
        }

        RuntimeStateInvariants.RequireValid(initialState);
        return new RuntimeStateStore(RuntimeStateFreezer.Freeze(initialState), historyCapacity);
    }

    /// <summary>The current published revision. Reading it is always safe; the value never changes in place.</summary>
    public RuntimeState Current => _current;

    /// <summary>Revision of the current published state.</summary>
    public int CurrentRevision => _current.Revision;

    /// <summary>Configured bound of the revision-activity history.</summary>
    public int HistoryCapacity => _historyCapacity;

    /// <summary>
    /// Recent revision activity, newest first, bounded by
    /// <see cref="HistoryCapacity"/>. The returned list is a copy: a caller can
    /// neither observe later commits through it nor modify the store through it.
    /// </summary>
    public IReadOnlyList<RuntimeRevisionEntry> History
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_history.ToArray());
            }
        }
    }

    /// <summary>Snapshot of the store's diagnostics counters.</summary>
    public RuntimeStoreCounters Counters
    {
        get
        {
            lock (_gate)
            {
                return new RuntimeStoreCounters
                {
                    CommittedRevisions = _committedRevisions,
                    RefusedCommits = _refusedCommits,
                    RevisionRefusals = _revisionRefusals,
                    InvalidStateRefusals = _invalidStateRefusals,
                    HistoryDepth = _history.Count,
                    HistoryCapacity = _historyCapacity,
                };
            }
        }
    }

    /// <summary>
    /// Issues the store's single authoritative writer. The lease is per store
    /// instance and is not released in Stage 0.3A-2A: the Runtime has exactly one
    /// writer for the lifetime of its store, and a test creates its own store.
    /// </summary>
    public RuntimeStateWriter CreateWriter()
    {
        lock (_gate)
        {
            if (_writerIssued)
            {
                throw new InvalidOperationException(
                    $"[{RuntimeRefusalCodes.WriterAlreadyActive}] RuntimeStateStore issues exactly one authoritative "
                    + "writer; a second writer was refused.");
            }

            _writerIssued = true;
            return new RuntimeStateWriter(this);
        }
    }

    /// <summary>
    /// Commits the next revision. Refusals are counted and thrown; the store is
    /// left untouched in every refusal path.
    /// </summary>
    internal RuntimeState Commit(RuntimeState nextState)
    {
        ArgumentNullException.ThrowIfNull(nextState);

        lock (_gate)
        {
            var current = _current;

            if (nextState.Revision <= current.Revision || nextState.Revision <= _highestRevision)
            {
                _refusedCommits++;
                _revisionRefusals++;
                throw new InvalidOperationException(
                    $"[{RuntimeRefusalCodes.RevisionNotMonotonic}] Refused revision {nextState.Revision}: the store "
                    + $"already holds revision {_highestRevision}. Duplicate and backward revisions are refused, and "
                    + "nothing was committed.");
            }

            if (nextState.Revision != current.Revision + 1)
            {
                _refusedCommits++;
                _revisionRefusals++;
                throw new InvalidOperationException(
                    $"[{RuntimeRefusalCodes.RevisionNotNext}] Refused revision {nextState.Revision}: the next committed "
                    + $"revision must be {current.Revision + 1} (the chain is gapless), and nothing was committed.");
            }

            if (!RuntimeStateInvariants.TryValidate(nextState, out var reasonCode, out var detail))
            {
                _refusedCommits++;
                _invalidStateRefusals++;
                throw new InvalidOperationException(
                    $"[{reasonCode}] Refused revision {nextState.Revision}: {detail} The store is unchanged.");
            }

            var frozen = RuntimeStateFreezer.Freeze(nextState);
            _current = frozen;
            _highestRevision = frozen.Revision;
            _committedRevisions++;
            _history.Insert(0, new RuntimeRevisionEntry
            {
                Revision = frozen.Revision,
                GeneratedAtUtc = frozen.GeneratedAtUtc,
                Kind = RuntimeRevisionKind.UPDATE,
            });

            if (_history.Count > _historyCapacity)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            return frozen;
        }
    }
}

/// <summary>
/// The store's single authoritative writer. Obtained from
/// <see cref="RuntimeStateStore.CreateWriter"/>; every mutation of the
/// authoritative state goes through <see cref="Commit"/>.
/// </summary>
public sealed class RuntimeStateWriter
{
    private readonly RuntimeStateStore _store;

    internal RuntimeStateWriter(RuntimeStateStore store) => _store = store;

    /// <summary>Revision currently published by the owning store.</summary>
    public int CurrentRevision => _store.CurrentRevision;

    /// <summary>
    /// Publishes the next revision and returns the frozen state that became
    /// authoritative. Throws, without committing anything, when the revision is
    /// not the next one or when the state fails structural validation.
    /// </summary>
    public RuntimeState Commit(RuntimeState nextState) => _store.Commit(nextState);
}
