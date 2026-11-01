namespace Wjss.Contracts;

/// <summary>
/// One step of the Cleaning Job dispatch cycle, in the Owner-approved order. The order is a
/// contract, not a browser detail: PRECHECK, MOVE_TO_P1, OPEN_IV, P2 to P6, CLOSE_IV,
/// MANDATORY_SAFE_RETURN, COMPLETE, NEXT_DISPATCH_COUNTDOWN, DISPATCHED.
/// </summary>
public enum DispatchTimelineStage
{
    PRECHECK = 1,
    MOVE_TO_P1 = 2,
    OPEN_IV = 3,
    P2 = 4,
    P3 = 5,
    P4 = 6,
    P5 = 7,
    P6 = 8,
    CLOSE_IV = 9,
    MANDATORY_SAFE_RETURN = 10,
    COMPLETE = 11,
    NEXT_DISPATCH_COUNTDOWN = 12,
    DISPATCHED = 13,
}

/// <summary>Presentation state of one timeline step. No step is ever hidden or reordered.</summary>
public enum DispatchTimelineStepState
{
    PENDING = 1,
    CURRENT = 2,
    COMPLETE = 3,
    FAILED = 4,
}

/// <summary>One ordered timeline step with its Operator wording supplied by the server.</summary>
public sealed record DispatchTimelineStep
{
    public required DispatchTimelineStage Stage { get; init; }
    public required int Ordinal { get; init; }
    public required string Label { get; init; }
    public required DispatchTimelineStepState State { get; init; }
}

/// <summary>
/// The whole dispatch-cycle timeline: every step in the contract order, the one current stage,
/// and the observable evidence of the last dispatch (if any). It carries no execution authority.
/// </summary>
public sealed record DispatchTimelineView
{
    public required DispatchTimelineStage Stage { get; init; }
    public required IReadOnlyList<DispatchTimelineStep> Steps { get; init; }
    public string? LastDispatchJobId { get; init; }
    public string? LastDispatchedAt { get; init; }
}

/// <summary>
/// The read-only Cleaning Job Workspace document of H3-A: the GlobalQueue head and entries, the
/// single Active Job, the AutoSequence mode, the frozen next-dispatch countdown, the live TimeDelay
/// preview and the dispatch-cycle timeline, all at ONE Runtime revision.
///
/// <para>
/// This document is a projection of the authoritative Runtime state and reuses the accepted
/// Snapshot contract types (<see cref="QueueSummary"/>, <see cref="ActiveCleaningJobState"/>)
/// unchanged, so it can never disagree with the Snapshot it was taken from. It adds no second
/// queue, no second Active Job and no UI-owned execution state: the browser renders it and owns
/// nothing.
/// </para>
/// </summary>
public sealed record DispatchWorkspace
{
    /// <summary>The fixed truthful boundary sentence of every published document of this stage.</summary>
    public const string SimulationNote =
        "SIMULATION - no Hardware access, no device write, no field verification";

    /// <summary>Always <see cref="SchemaIds.DispatchWorkspaceKind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Always <see cref="SchemaIds.DispatchWorkspace"/>.</summary>
    public required string Schema { get; init; }
    public required int ApiVersion { get; init; }
    public required int Revision { get; init; }
    public required string GeneratedAt { get; init; }
    public required DeviceProfile DeviceProfile { get; init; }
    public required string StageMarker { get; init; }
    public required string Simulation { get; init; }

    public required QueueSummary Queue { get; init; }
    public ActiveCleaningJobState? ActiveJob { get; init; }

    public required AutoSequenceState AutoSequence { get; init; }
    public required AutoSequenceMode Mode { get; init; }

    public required DispatchCountdownView Countdown { get; init; }
    public required DispatchTimelineView Timeline { get; init; }

    /// <summary>The configured TimeDelay policy, or null when this Runtime has none configured.</summary>
    public TimeDelayConfiguration? Configuration { get; init; }

    public required TimeDelayPreview Preview { get; init; }
}
