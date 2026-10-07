using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// Encoding of the three-state <c>activeJob</c> slot of <c>wjss.delta/1</c>. The
/// names mirror the contract wording: an ABSENT slot leaves the consumer's Active
/// Job untouched, a PRESENT object replaces it wholesale, and an explicit JSON
/// null clears it. There is deliberately no separate "cleared" boolean in the
/// wire contract, so none is modelled here either — the clear case is carried by
/// its own enum member.
/// </summary>
public enum DeltaJobEncoding
{
    /// <summary><c>activeJob</c> key omitted: the consumer keeps its current Active Job.</summary>
    Absent = 0,

    /// <summary><c>activeJob</c> is an object: replace the current Active Job with it.</summary>
    Present = 1,

    /// <summary><c>activeJob</c> is an explicit JSON null: clear the consumer's Active Job.</summary>
    Cleared = 2,
}

/// <summary>
/// The three-state <c>activeJob</c> slot exactly as carried on the wire: which of
/// the three encodings a Delta uses, plus the replacement job for
/// <see cref="DeltaJobEncoding.Present"/> only.
/// </summary>
public sealed record DeltaJobState
{
    /// <summary>Which encoding the Delta uses for <c>activeJob</c>.</summary>
    public required DeltaJobEncoding Encoding { get; init; }

    /// <summary>The replacement job; non-null exactly when <see cref="Encoding"/> is <see cref="DeltaJobEncoding.Present"/>.</summary>
    public ActiveCleaningJobState? Value { get; init; }

    /// <summary>The Active Job is unchanged (the key is omitted from the wire payload).</summary>
    public static DeltaJobState Unchanged() => new() { Encoding = DeltaJobEncoding.Absent };

    /// <summary>The Active Job is an object on the wire and replaces whatever the consumer held.</summary>
    public static DeltaJobState Replaced(ActiveCleaningJobState value) =>
        value is null
            ? throw new ArgumentNullException(nameof(value))
            : new DeltaJobState { Encoding = DeltaJobEncoding.Present, Value = value };

    /// <summary>The Active Job is an explicit JSON null on the wire and clears the consumer's job.</summary>
    public static DeltaJobState Cleared() => new() { Encoding = DeltaJobEncoding.Cleared };

    /// <summary>The replacement job; valid only when the encoding is <see cref="DeltaJobEncoding.Present"/>.</summary>
    public ActiveCleaningJobState Present => Encoding == DeltaJobEncoding.Present
        ? Value ?? throw new InvalidOperationException("A Present Delta Active Job must carry a value.")
        : throw new InvalidOperationException($"The Delta Active Job encoding is {Encoding}; no replacement value exists.");
}

/// <summary>
/// Immutable, fully merged <c>wjss.delta/1</c> payload for one committed Runtime
/// revision transition.
///
/// Semantics fixed by the accepted baseline and this checkpoint:
///   * <see cref="PreviousRevision"/> is the revision the consumer must already
///     hold; <see cref="Revision"/> is always <c>PreviousRevision + 1</c>;
///   * every section is a whole-record replacement (no field deltas, no field
///     merges, no positional encoding);
///   * <c>activeJob</c> is three-state (see <see cref="DeltaJobState"/>);
///   * a refused transition produces NO Delta at all, so a generated Delta is
///     always a step of the committed chain;
///   * the wall map is never part of a Delta (it is Snapshot-only and static).
///
/// The type is the frozen, apply-safe form of the transition: it retains the
/// previous revision's sensor records for the sensors this tick changed so that a
/// consumer can reverse one cleanly-applied Delta by itself, while the consumer
/// only ever needs the new records it carries. It is deliberately not the wire
/// DTO (<see cref="OperationalDelta"/>) and carries no timestamp of its own —
/// one Delta belongs to exactly one accepted tick, so the tick's time is the
/// state's time.
/// </summary>
public sealed record RuntimeDelta
{
    /// <summary>Revision the consumer must hold before applying this Delta.</summary>
    public required int PreviousRevision { get; init; }

    /// <summary>Revision after applying this Delta; always <see cref="PreviousRevision"/> + 1.</summary>
    public required int Revision { get; init; }

    /// <summary>
    /// The accepted tick's instant, mirrored on the wire as <c>generatedAt</c>.
    /// Applying the Delta stamps the reconstructed revision with this instant, so
    /// the applied state is identical to the directly evolved state and no caller
    /// has to supply (or could supply wrongly) a timestamp of its own.
    /// </summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>New sensor records (whole-record replacements), in canonical ScanOrder.</summary>
    public required IReadOnlyList<SensorPresentationState> ChangedSensors { get; init; }

    /// <summary>
    /// The previous revision's records for the same sensors (same count and order as
    /// <see cref="ChangedSensors"/>), retained so a cleanly-applied Delta can be
    /// reversed without consulting history.
    /// </summary>
    public required IReadOnlyList<SensorPresentationState> PreviousSensors { get; init; }

    /// <summary>New wall summaries for every wall whose summary changed; otherwise empty.</summary>
    public required IReadOnlyList<WallSummary> ChangedWalls { get; init; }

    /// <summary>New pump presentation state; null when unchanged.</summary>
    public PumpState? Pump { get; init; }

    /// <summary>New queue summary; null when unchanged.</summary>
    public QueueSummary? Queue { get; init; }

    /// <summary>New AutoSequence state; null when unchanged.</summary>
    public SequenceState? Sequence { get; init; }

    /// <summary>New alarm summary; null when unchanged.</summary>
    public AlarmSummary? Alarms { get; init; }

    /// <summary>New communication health; null when unchanged.</summary>
    public CommunicationHealth? Communication { get; init; }

    /// <summary>New Runtime health counters; null when unchanged.</summary>
    public RuntimeHealth? Runtime { get; init; }

    /// <summary>The trend point this tick appended; null when the tick appended none.</summary>
    public TrendPoint? TrendPoint { get; init; }

    /// <summary>Three-state Active Job slot (see <see cref="DeltaJobState"/>).</summary>
    public required DeltaJobState ActiveJob { get; init; }

    /// <summary>True when the Delta carries no change at all beyond the revision step.</summary>
    public bool IsEmpty =>
        ChangedSensors.Count == 0
        && ChangedWalls.Count == 0
        && Pump is null
        && Queue is null
        && Sequence is null
        && Alarms is null
        && Communication is null
        && Runtime is null
        && TrendPoint is null
        && ActiveJob.Encoding == DeltaJobEncoding.Absent;
}
