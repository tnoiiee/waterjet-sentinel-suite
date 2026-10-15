namespace Wjss.Contracts;

/// <summary>
/// Canonical vocabulary for the deterministic one-Node, non-Hardware Product slice.
/// These are presentation identities, not physical addressing or decoded measurements.
/// </summary>
public static class DeterministicTcVocabulary
{
    public const string NodeId = "TC-NODE-01";
    public const string CabinetId = "CAB1";
    public const string Provenance = "DETERMINISTIC_SIMULATION";
    public const string Complete = "COMPLETE";
    public const string Quality = "SIMULATED";
    public const string NoDiagnostic = "NONE";
    public const string MappingNotVerified = "MAPPING_NOT_VERIFIED";
    public const string DecodeNotVerified = "DECODING_NOT_VERIFIED";
    public const int WordCount = 40;

    public static IReadOnlyList<string> NotVerified { get; } = Array.AsReadOnly(new[]
    {
        "PHYSICAL_REACHABILITY", "REGISTER_MAPPING", "SCALING", "WIRE_BREAK", "FIELD_TIMING",
    });

    public static ushort WordAt(int index) => checked((ushort)(100 + index));
}

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
