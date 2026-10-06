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
/// Active Job uses the full three-state encoding of the accepted baseline,
/// restored by Owner review (no second boolean flag exists in the contract):
///   * <c>activeJob</c> ABSENT      -> unchanged;
///   * <c>activeJob</c> OBJECT      -> replace the Active Job;
///   * <c>activeJob: null</c>       -> clear the Active Job (release/SR7 done).
/// The distinction between "absent" and "explicit null" is implemented with
/// the structural presence wrapper <see cref="Optional{T}"/> — a contract
/// technique applied to this property ONLY, not a production serialization
/// policy for other fields.
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

    /// <summary>
    /// Three-state Active Job slot (see type docs): Absent = unchanged,
    /// Present = whole-record replacement, Cleared = explicit JSON null.
    /// </summary>
    [JsonConverter(typeof(OptionalJsonConverter<ActiveCleaningJobState>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<ActiveCleaningJobState> ActiveJob { get; init; }

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
