namespace Wjss.Runtime.Core;

/// <summary>
/// Machine reason codes produced by the Runtime state foundation. Codes are part
/// of the diagnostics surface: the Runtime Inspector and the readiness payload
/// (Stage 0.3A-2C) read the same strings, so a refusal is never reported only as
/// prose. Codes are stable identities, not display text.
/// </summary>
public static class RuntimeRefusalCodes
{
    /// <summary>A committed state must be a SIMULATOR state (ADR-0012, protected baseline).</summary>
    public const string ProfileNotSimulator = "PROFILE_NOT_SIMULATOR";

    /// <summary>Revision is outside the contract range (revisions are 1-based).</summary>
    public const string RevisionOutOfRange = "REVISION_OUT_OF_RANGE";

    /// <summary>The next committed revision must be exactly current + 1 (no holes).</summary>
    public const string RevisionNotNext = "REVISION_NOT_NEXT";

    /// <summary>A duplicate or backward revision was refused.</summary>
    public const string RevisionNotMonotonic = "REVISION_NOT_MONOTONIC";

    /// <summary>The state failed structural validation; nothing was committed.</summary>
    public const string StateInvalid = "STATE_INVALID";

    /// <summary>The store issues exactly one authoritative writer.</summary>
    public const string WriterAlreadyActive = "WRITER_ALREADY_ACTIVE";

    /// <summary>The logical matrix must hold exactly 108 slots.</summary>
    public const string WallMapSlotCount = "WALL_MAP_SLOT_COUNT";

    /// <summary>The wall map must be in canonical row-major order with canonical slot identities.</summary>
    public const string WallMapSlotOrder = "WALL_MAP_SLOT_ORDER";

    /// <summary>A wall-map slot disagrees with the canonical logical matrix identity.</summary>
    public const string WallMapSlotIdentity = "WALL_MAP_SLOT_IDENTITY";

    /// <summary>Exactly two Cannon equipment slots are required, at logical I7 and I16.</summary>
    public const string WallMapCannonSlots = "WALL_MAP_CANNON_SLOTS";

    /// <summary>A Sensor slot must carry exactly the canonical Sensor identity for its position.</summary>
    public const string WallMapSensorBinding = "WALL_MAP_SENSOR_BINDING";

    /// <summary>Exactly 106 Sensor projections are required.</summary>
    public const string SensorCount = "SENSOR_COUNT";

    /// <summary>Sensor projections must be ordered by ScanOrder 1..106 without gaps.</summary>
    public const string SensorOrder = "SENSOR_ORDER";

    /// <summary>A Sensor projection disagrees with the canonical position identity.</summary>
    public const string SensorIdentity = "SENSOR_IDENTITY";

    /// <summary>Per-wall Sensor counts must be 24 / 29 / 24 / 29.</summary>
    public const string SensorWallCounts = "SENSOR_WALL_COUNTS";

    /// <summary>Each Sensor carries exactly two distinct, non-blank Thermocouple channels.</summary>
    public const string SensorChannels = "SENSOR_TC_CHANNELS";

    /// <summary>The mapping must supply exactly 212 distinct Thermocouple channels.</summary>
    public const string SensorChannelTotal = "SENSOR_TC_CHANNEL_TOTAL";

    /// <summary>Single Active Job: the Active-Job target must be the one ACTIVE Sensor, and vice versa.</summary>
    public const string SensorActiveTarget = "SENSOR_ACTIVE_TARGET";

    /// <summary>Wall summaries must match the Sensor composition they summarise.</summary>
    public const string WallSummaryMismatch = "WALL_SUMMARY_MISMATCH";

    /// <summary>Four wall summaries are required, in canonical wall order.</summary>
    public const string WallSummaryShape = "WALL_SUMMARY_SHAPE";

    /// <summary>Queue shape: bounded to 8 entries, contiguous positions, counter equals membership.</summary>
    public const string QueueShape = "QUEUE_SHAPE";

    /// <summary>Trend window bounds: positive capacity, bounded point count, fixed-width series.</summary>
    public const string TrendBounds = "TREND_BOUNDS";

    /// <summary>Alarm counters must equal the presented alarm items.</summary>
    public const string AlarmCounters = "ALARM_COUNTERS";

    /// <summary>Device health entries must be unique and must name configured devices.</summary>
    public const string DeviceHealthSet = "COMMUNICATION_DEVICE_SET";

    /// <summary>Pump pressure band shape only; no pump policy is encoded here.</summary>
    public const string PumpBand = "PUMP_BAND";

    /// <summary>A suspended AutoSequence requires a latched critical event.</summary>
    public const string SequenceCriticalLatch = "SEQUENCE_CRITICAL_LATCH";

    // ---------------------------------------------------------------------
    // Checkpoint B — synthetic evolution, Delta generation and Delta history.
    // ---------------------------------------------------------------------

    /// <summary>The requested evolution tick time is not later than the committed state time.</summary>
    public const string EvolutionTickTime = "EVOLUTION_TICK_TIME";

    /// <summary>The requested evolution tick number is not strictly after the committed tick number.</summary>
    public const string EvolutionTickSequence = "EVOLUTION_TICK_SEQUENCE";

    /// <summary>The Delta does not continue the committed revision chain (previousRevision is not the committed revision).</summary>
    public const string DeltaOutOfOrder = "DELTA_OUT_OF_ORDER";

    /// <summary>
    /// The consumer's revision cannot be advanced by the available Deltas: a fresh
    /// Snapshot must be requested. Nothing is applied or fabricated after this result.
    /// </summary>
    public const string ResnapshotRequired = "RESNAPSHOT_REQUIRED";
}
