namespace Wjss.Runtime.Core;

/// <summary>How a published revision came to exist. Diagnostics only; not a wire contract.</summary>
public enum RuntimeRevisionKind
{
    /// <summary>The first published revision of a Runtime State Store instance.</summary>
    INITIAL,

    /// <summary>A committed state update.</summary>
    UPDATE,
}

/// <summary>One bounded revision-activity entry (the Runtime Inspector's recent-revision list).</summary>
public sealed record RuntimeRevisionEntry
{
    public required int Revision { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required RuntimeRevisionKind Kind { get; init; }
}

/// <summary>
/// Runtime State Store counters for the diagnostics projection. These are
/// self-observation counters (not audit records, not safety evidence): a refused
/// transition is counted and reported, never hidden.
/// </summary>
public sealed record RuntimeStoreCounters
{
    /// <summary>Successful commits including the initial publish (so a fresh store reports 1).</summary>
    public required int CommittedRevisions { get; init; }

    /// <summary>All refused commits.</summary>
    public required int RefusedCommits { get; init; }

    /// <summary>Refusals caused by a duplicate, backward or non-contiguous revision.</summary>
    public required int RevisionRefusals { get; init; }

    /// <summary>Refusals caused by a state that failed structural validation.</summary>
    public required int InvalidStateRefusals { get; init; }

    /// <summary>Entries currently retained by the bounded revision history.</summary>
    public required int HistoryDepth { get; init; }

    /// <summary>Configured bound of the revision history.</summary>
    public required int HistoryCapacity { get; init; }
}
