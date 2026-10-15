using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// Honest Runtime-facing state for one read-only TC acquisition. This is a Product
/// contract for the deterministic vertical slice; it is not physical evidence and
/// carries no temperature, mapping, scaling or wire-break claim.
/// </summary>
public sealed record TcAcquisitionState
{
    public required string NodeId { get; init; }
    public required string CabinetId { get; init; }
    public required string Provenance { get; init; }
    public required bool Physical { get; init; }
    public required bool Quarantined { get; init; }
    public required string AcquisitionState { get; init; }
    public required DateTimeOffset AcquiredAtUtc { get; init; }
    public required int CopiedWordCount { get; init; }
    public required IReadOnlyList<ushort> RawWords { get; init; }
    public required string Quality { get; init; }
    public required string DiagnosticState { get; init; }
    public required string MappingStatus { get; init; }
    public required string DecodeStatus { get; init; }
    public required IReadOnlyList<string> NotVerified { get; init; }
}
