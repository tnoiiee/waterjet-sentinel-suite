namespace Wjss.Contracts;

/// <summary>Response body of GET /health/live (200 while the host process is alive).</summary>
public sealed record HealthLivePayload
{
    /// <summary>Always "ALIVE" in a 200 response.</summary>
    public required string Status { get; init; }

    /// <summary>Host identity label for diagnostics; not a control handle.</summary>
    public required string Host { get; init; }

    /// <summary>Active device profile, echoed for display parity (SIMULATOR in Stage 0.3A).</summary>
    public required DeviceProfile DeviceProfile { get; init; }

    /// <summary>False at Stage 0.3A-1: liveness of the host is not the Product Runtime.</summary>
    public required bool RuntimeImplemented { get; init; }

    public required string StageMarker { get; init; }
}

/// <summary>Response body of GET /health/ready (503 with <see cref="Code"/> until the Runtime is implemented).</summary>
public sealed record HealthReadyPayload
{
    /// <summary>"NOT_READY" at Stage 0.3A-1.</summary>
    public required string Status { get; init; }

    /// <summary>Machine code; at Stage 0.3A-1 always <see cref="Stage03A1.RuntimeNotImplemented"/>.</summary>
    public required string Code { get; init; }

    public required string Detail { get; init; }

    public required string StageMarker { get; init; }
}
