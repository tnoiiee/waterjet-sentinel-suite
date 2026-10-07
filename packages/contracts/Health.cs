namespace Wjss.Contracts;

/// <summary>
/// One simulated device session's link health. Health is evaluated from
/// transport evidence, never from value change (accepted communication-health
/// rule). Values are simulator/test data in Stage 0.3A.
/// </summary>
public sealed record DeviceHealth
{
    public required string DeviceId { get; init; }
    public required DeviceLinkState State { get; init; }
    public required int ConsecutiveTimeouts { get; init; }
    public string? LastSuccessAt { get; init; }
    public int? LastLatencyMs { get; init; }
    public required int PollsOk { get; init; }
    public required int PollsFailed { get; init; }
}

/// <summary>Per-device communication health set (one entry per simulated device).</summary>
public sealed record CommunicationHealth
{
    public required IReadOnlyList<DeviceHealth> Devices { get; init; }
}

/// <summary>
/// Runtime self-observation for the Diagnostics presentation. These are
/// diagnostics counters, not audit records, not Historian, and not safety
/// evidence. The Historian block is present-but-zero in Stage 0.3A because no
/// Historian exists yet ([NOT WIRED]); the UI must present it as unavailable,
/// never as healthy.
/// </summary>
public sealed record RuntimeHealth
{
    public required double UptimeSeconds { get; init; }
    public required int SseClients { get; init; }
    public required int InvariantViolations { get; init; }
    public required int AcceptedSecondJobs { get; init; }
    public required int RefusedSecondJobs { get; init; }
    public required HistorianHealth Historian { get; init; }

    /// <summary>Monotonic revision the Runtime has published so far (diagnostics mirror).</summary>
    public required int CurrentRevision { get; init; }

    /// <summary>Authoritative-state owner stage marker, e.g. STAGE_03A1_SKELETON.</summary>
    public required string StageMarker { get; init; }
}

/// <summary>Placeholder shape for the future Historian write path; no Historian in Stage 0.3A.</summary>
public sealed record HistorianHealth
{
    public required bool Wired { get; init; }
    public required int Depth { get; init; }
    public required int Capacity { get; init; }
    public required bool NearOverflow { get; init; }
    public required int Rejected { get; init; }
    public int? LastBatchLatencyMs { get; init; }
    public required int GapMarkers { get; init; }
}
