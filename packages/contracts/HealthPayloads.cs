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

    /// <summary>True from Stage 0.3A-2C: the composed SIMULATOR Runtime answers this host. Liveness still is not a readiness claim.</summary>
    public required bool RuntimeImplemented { get; init; }

    public required string StageMarker { get; init; }
}

/// <summary>
/// Response body of GET /health/ready: 200 only when every readiness condition
/// holds, otherwise 503 with the <see cref="Code"/> that refused.
/// </summary>
public sealed record HealthReadyPayload
{
    /// <summary>"READY" on 200; "NOT_READY" on 503.</summary>
    public required string Status { get; init; }

    /// <summary>Machine readiness code (e.g. RUNTIME_READY, EVOLUTION_NOT_STARTED, STARTUP_FAULT); never prose, never a silent fallback.</summary>
    public required string Code { get; init; }

    public required string Detail { get; init; }

    public required string StageMarker { get; init; }
}
