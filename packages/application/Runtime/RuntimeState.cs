using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// One immutable Runtime revision: the authoritative state a reader observes.
///
/// Immutability contract
/// <list type="bullet">
///   <item>every property is init-only; no property may be reassigned after
///   publication;</item>
///   <item>every collection member is a read-only wrapper produced by the
///   Runtime State Store when the state is committed, so a reader cannot reach
///   a mutable collection through this record;</item>
///   <item>the state is assigned to the store's published reference in a single
///   write; a reader therefore observes either the previous revision or the new
///   one, never a partially-updated revision.</item>
/// </list>
///
/// Equality: record equality compares the collection members by reference, so
/// two structurally identical revisions are not <c>==</c> each other. Structural
/// comparison is done on the projected contract (Snapshot serialization) or on
/// the individual sequences, never by comparing <see cref="RuntimeState"/>
/// values.
/// </summary>
public sealed record RuntimeState
{
    /// <summary>Monotonic revision; the first published revision is 1.</summary>
    public required int Revision { get; init; }

    /// <summary>UTC instant of this revision, stamped from the injected clock.</summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>Active device profile. Only SIMULATOR may reach a committed state in Stage 0.3A.</summary>
    public required DeviceProfile DeviceProfile { get; init; }

    /// <summary>Published Configuration revision the Runtime is applying.</summary>
    public required PublishedConfigurationRevision Config { get; init; }

    /// <summary>108 logical slots (106 SENSOR + 2 CANNON), canonical row-major order.</summary>
    public required IReadOnlyList<WallMapSlot> WallMap { get; init; }

    /// <summary>106 Sensor projections, ordered by ScanOrder 1..106.</summary>
    public required IReadOnlyList<SensorPresentationState> Sensors { get; init; }

    /// <summary>Four wall summaries, canonical wall order (Left, Rear, Right, Front), recalculated from the Sensors.</summary>
    public required IReadOnlyList<WallSummary> Walls { get; init; }

    /// <summary>At most one Active Cleaning Job; null when no Job is active.</summary>
    public ActiveCleaningJobState? ActiveJob { get; init; }

    public required PumpState Pump { get; init; }

    public required QueueSummary Queue { get; init; }

    public required SequenceState Sequence { get; init; }

    public required AlarmSummary Alarms { get; init; }

    public required CommunicationHealth Communication { get; init; }

    public required TrendWindow Trend { get; init; }
}
