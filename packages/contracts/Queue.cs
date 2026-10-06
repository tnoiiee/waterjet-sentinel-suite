namespace Wjss.Contracts;

/// <summary>
/// One ready-to-dispatch GlobalQueue entry. Presence in the queue IS the READY
/// state: there is deliberately no per-entry status field (Owner domain
/// correction; queue-level HELD is superseded). Admission/removal policy is a
/// pending Owner decision and is NOT part of this contract.
/// </summary>
public sealed record QueueEntry
{
    /// <summary>1-based FIFO position. Only Position 1 is a dispatch candidate.</summary>
    public required int Position { get; init; }

    public required string EntryId { get; init; }
    public required string SensorId { get; init; }

    /// <summary>Source reason label (presentation of the owning source queue).</summary>
    public required string SourceReason { get; init; }

    public double? DirtyScore { get; init; }
    public required int SecondsSinceLastClean { get; init; }
}

/// <summary>
/// The whole bounded GlobalQueue. At most 8 entries PHYSICALLY (no hidden
/// overflow, no preview cut); totalQueued === entries.Length; only the head is
/// ever a dispatch candidate; the revision bumps on every membership change.
/// </summary>
public sealed record QueueSummary
{
    public const int MaxEntries = 8;

    public required string Label { get; init; }
    public required int Capacity { get; init; }
    public required int TotalQueued { get; init; }
    public required int Revision { get; init; }
    public required IReadOnlyList<QueueEntry> Entries { get; init; }
    public required AutoSequenceState AutoSequence { get; init; }
    public DispatchRecord? LastDispatch { get; init; }
}

/// <summary>
/// Dispatch evidence created atomically when queue Position 1 is removed and
/// exactly one Cleaning Job is created for that Sensor. A presentation record,
/// NOT a Production audit record (audit persistence is a later stage).
/// </summary>
public sealed record DispatchRecord
{
    public required string DispatchId { get; init; }

    /// <summary>Queue revision before the atomic dispatch step.</summary>
    public required int QueueRevisionBefore { get; init; }

    /// <summary>Queue revision after the atomic dispatch step (always before+1).</summary>
    public required int QueueRevisionAfter { get; init; }

    public required string QueueEntryId { get; init; }

    /// <summary>Always 1: head-only dispatch, no scan-forward.</summary>
    public required int PositionBefore { get; init; }

    public required string SensorId { get; init; }
    public required string SourceReason { get; init; }
    public required string JobId { get; init; }

    /// <summary>Machine origin label (e.g. AUTOSEQUENCE_START); not a policy.</summary>
    public required string Origin { get; init; }

    public required string DispatchedAt { get; init; }
}
