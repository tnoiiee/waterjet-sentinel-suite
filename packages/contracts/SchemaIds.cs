namespace Wjss.Contracts;

/// <summary>
/// Schema identity for the Product presentation contracts (ADR-0014, draft).
/// The spike identifiers (<c>wjss.spike.snapshot/1</c>) are NOT reused: this is
/// the product namespace. Fields marked as additive-protected may only grow in
/// a <c>/1</c> world; a structural break requires <c>/2</c> and a contract gate.
/// </summary>
public static class SchemaIds
{
    public const string Snapshot = "wjss.snapshot/1";
    public const string Delta = "wjss.delta/1";

    /// <summary>Numeric API version carried in every envelope (route family is /api/v1).</summary>
    public const int ApiVersion = 1;
}

/// <summary>
/// Stage 0.3A-1 markers. The runtime host answers /health/ready with
/// <see cref="RuntimeNotImplemented"/> until Stage 0.3A-2+ is delivered and
/// Owner-local validated. No behaviour is claimed by these constants.
/// </summary>
public static class Stage03A1
{
    public const string Marker = "STAGE_03A1_SKELETON";
    public const string RuntimeNotImplemented = "RUNTIME_NOT_IMPLEMENTED";
}
