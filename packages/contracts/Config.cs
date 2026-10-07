namespace Wjss.Contracts;

/// <summary>
/// Identity of the immutable Published Configuration revision the Runtime is
/// applying (accepted model: Draft is never consumed; the runtime reads an
/// in-memory published snapshot; thresholds in fixtures are synthetic).
/// The full configuration schema (mapping documents, validation result sets)
/// belongs to 0.3A-F and is deliberately NOT frozen by this contract.
/// </summary>
public sealed record PublishedConfigurationRevision
{
    public required int Revision { get; init; }
    public required string PublishedAt { get; init; }
    public required string Label { get; init; }

    /// <summary>Synthetic Dirty-Score threshold source for presentation labelling. Value here is never authoritative for Production.</summary>
    public required double DirtyThreshold { get; init; }

    public required int StaleThresholdMs { get; init; }
}
