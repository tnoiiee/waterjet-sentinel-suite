using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// An incremental projection between two consecutive Runtime revisions.
///
/// Semantics (accepted 0.2.1A baseline):
///   * an ABSENT KEY means UNCHANGED;
///   * records are whole-record replacements;
///   * PreviousRevision must equal the consumer's current revision — a
///     mismatch is a GAP: the consumer applies nothing, closes the stream and
///     reopens for a fresh Snapshot. No replay, no inference from a stale cache.
///   * no wallMap key (the wall map is static, Snapshot-only).
///
/// Documented encoding deviation from the spike (recorded in ADR-0014 draft):
/// the spike signalled "Job cleared" with an explicit `activeJob: null`.
/// System.Text.Json cannot combine "omit when null" with "write explicit null"
/// per-property, so the Product Delta clears the Active Job with
/// <see cref="ActiveJobCleared"/> = true while omitting the activeJob key.
/// A literal `activeJob: null` in a Product Delta is INVALID and is rejected
/// by the contract validator (single unambiguous encoding).
/// </summary>
public sealed record OperationalDelta
{
    /// <summary>Envelope discriminator; always "delta".</summary>
    public required string Kind { get; init; }

    /// <summary>Must equal <see cref="SchemaIds.Delta"/>.</summary>
    public required string Schema { get; init; }

    public required int ApiVersion { get; init; }

    /// <summary>Revision the consumer must currently hold.</summary>
    public required int PreviousRevision { get; init; }

    /// <summary>New revision after application; always PreviousRevision + 1 in a gapless chain.</summary>
    public required int Revision { get; init; }

    public required string GeneratedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublishedConfigurationRevision? Config { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SensorPresentationState>? Sensors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WallSummary>? Walls { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActiveCleaningJobState? ActiveJob { get; init; }

    /// <summary>true = the Active Job has been cleared (release/SR7 done); activeJob key must be absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ActiveJobCleared { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PumpState? Pump { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueueSummary? Queue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SequenceState? Sequence { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AlarmSummary? Alarms { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommunicationHealth? Communication { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeHealth? Runtime { get; init; }

    /// <summary>One new bounded-trend point (the window itself is Snapshot-delivered; Deltas append).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrendPoint? TrendPoint { get; init; }
}
