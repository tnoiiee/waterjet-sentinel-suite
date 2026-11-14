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

/// <summary>
/// Response body of GET /api/health: the small factual answer the Kiosk shell's availability probe
/// reads while it decides whether to reload the approved route. The shell tests the status code only,
/// so every field here exists for the operator's and the reviewer's benefit, and none of it is a
/// readiness claim, a device claim or a control handle.
/// </summary>
public sealed record KioskHealthPayload
{
    /// <summary>Always "ALIVE" in a 200 response. It is not "READY".</summary>
    public required string Status { get; init; }

    /// <summary>Host identity label for diagnostics; not a route, address or handle.</summary>
    public required string Host { get; init; }

    /// <summary>The active device profile, echoed for display parity.</summary>
    public required DeviceProfile DeviceProfile { get; init; }

    /// <summary>
    /// The host's own redacted label for the acquisition mode (DETERMINISTIC or PHYSICAL_READ_ONLY).
    /// It names what the host may READ; it never states or implies that anything may be written.
    /// </summary>
    public required string AcquisitionMode { get; init; }

    /// <summary>
    /// Readiness as a fact alongside the liveness answer, so a screen can show both. It duplicates no
    /// decision: the caller reads the same readiness projection /health/ready judges, and the 200
    /// answer of this route never depends on it.
    /// </summary>
    public required bool Ready { get; init; }

    /// <summary>The revision the state store currently publishes, or null while none exists.</summary>
    public required int? CurrentRevision { get; init; }

    public required string StageMarker { get; init; }
}
