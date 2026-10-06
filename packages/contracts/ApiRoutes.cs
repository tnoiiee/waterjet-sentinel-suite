namespace Wjss.Contracts;

/// <summary>
/// Route identities of the Local Application API surface that EXISTS at
/// Stage 0.3A-1. Snapshot/Delta/commands routes are declared in ADR-0014 as
/// the /api/v1 family but are NOT REGISTERED by any host before 0.3A-3;
/// constants for them are deliberately absent so no half-wired surface can be
/// mistaken for implementation.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Liveness of the host process. 200 means only: host alive. It is NOT a claim that the Product Runtime is implemented.</summary>
    public const string HealthLive = "/health/live";

    /// <summary>Readiness of the authoritative Runtime. Stage 0.3A-1 answer: 503 + RUNTIME_NOT_IMPLEMENTED.</summary>
    public const string HealthReady = "/health/ready";
}
