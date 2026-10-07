namespace Wjss.Contracts;

/// <summary>
/// Route identities of the Local Application API surface that EXISTS. Stage
/// 0.3A-1 registered the health routes only; Stage 0.3A-2C adds the READ-ONLY
/// Runtime observation routes. No write, command or dispatch route exists, and no
/// constant is declared for one, so no half-wired command surface can be mistaken
/// for implementation. SSE streaming remains deliberately absent (observers
/// poll).
/// </summary>
public static class ApiRoutes
{
    /// <summary>Liveness of the host process. 200 means only: host alive. It is NOT a readiness claim.</summary>
    public const string HealthLive = "/health/live";

    /// <summary>Readiness of the authoritative Runtime: 200 only when every readiness condition holds, else 503 with a structured machine code.</summary>
    public const string HealthReady = "/health/ready";

    /// <summary>Current <c>wjss.snapshot/1</c> projection of the committed revision. Read-only.</summary>
    public const string Snapshot = "/api/v1/snapshot";

    /// <summary>Small read-only Runtime status payload for operators and development observers.</summary>
    public const string Runtime = "/api/v1/runtime";

    /// <summary>Bounded recent Delta activity. Read-only and never unbounded.</summary>
    public const string Deltas = "/api/v1/deltas";
}
