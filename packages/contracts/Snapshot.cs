namespace Wjss.Contracts;

/// <summary>
/// The authoritative full projection of one Runtime revision. Every SSE
/// connection (initial or after reconnect/gap) starts with a full Snapshot;
/// the UI applies nothing inferred from a stale cache. Field identities follow
/// the accepted 0.2.1A presentation baseline (wallMap is Snapshot-only; the
/// queue is the whole bounded queue; runtime health is present for diagnostics).
/// </summary>
public sealed record OperationalSnapshot
{
    /// <summary>Envelope discriminator; always "snapshot".</summary>
    public required string Kind { get; init; }

    /// <summary>Must equal <see cref="SchemaIds.Snapshot"/>; unknown values are refused by the consumer.</summary>
    public required string Schema { get; init; }

    public required int ApiVersion { get; init; }

    /// <summary>Monotonic Runtime revision this Snapshot projects.</summary>
    public required int Revision { get; init; }

    public required string GeneratedAt { get; init; }

    /// <summary>The Published Configuration revision the Runtime is applying.</summary>
    public required PublishedConfigurationRevision Config { get; init; }

    /// <summary>The active device profile, displayed in the chrome (ADR-0012 no-silent-substitution).</summary>
    public required DeviceProfile DeviceProfile { get; init; }

    /// <summary>108 logical slots: 106 SENSOR + 2 NON_SENSOR_GAP. Static; Snapshot-only; never in a Delta.</summary>
    public required IReadOnlyList<WallMapSlot> WallMap { get; init; }

    /// <summary>All 106 Sensor projections, ordered by ScanOrder.</summary>
    public required IReadOnlyList<SensorPresentationState> Sensors { get; init; }

    /// <summary>
    /// The approved Water Jet topology (exactly 8: WJ1–WJ8 with installed position,
    /// placement anchors and opposite-wall target coverage). Static configuration
    /// topology — NOT a controllable device instance; Snapshot-only; never in a Delta.
    /// </summary>
    public required IReadOnlyList<WaterJetConfiguration> WaterJets { get; init; }

    /// <summary>
    /// The approved Isolation Valve topology (exactly 8: IV1–IV8, one-to-one ordinal
    /// paired with the Water Jets). Static configuration topology — NOT a controllable
    /// device instance; Snapshot-only; never in a Delta.
    /// </summary>
    public required IReadOnlyList<IsolationValveConfiguration> IsolationValves { get; init; }

    public required IReadOnlyList<WallSummary> Walls { get; init; }

    /// <summary>At most one; null = no Active Job.</summary>
    public ActiveCleaningJobState? ActiveJob { get; init; }

    public required PumpState Pump { get; init; }
    public required QueueSummary Queue { get; init; }
    public required SequenceState Sequence { get; init; }
    public required AlarmSummary Alarms { get; init; }
    public required CommunicationHealth Communication { get; init; }
    public required RuntimeHealth Runtime { get; init; }
    public required TrendWindow Trend { get; init; }
}
