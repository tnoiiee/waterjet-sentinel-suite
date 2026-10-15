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

    /// <summary>
    /// A wall-map slot declares a non-Sensor position kind. Owner critical topology correction:
    /// every one of the 108 logical positions is a Product Sensor (I7 and I16 included); a slot
    /// still carrying the superseded gap classification is refused.
    /// </summary>
    public const string WallMapNonSensorPosition = "WALL_MAP_NON_SENSOR_POSITION";

    /// <summary>The state fails the central cross-layer <see cref="Wjss.Contracts.TopologyInvariants"/> agreement check (detail carries the TOPOLOGY_* code).</summary>
    public const string TopologyInvariant = "TOPOLOGY_INVARIANT";

    /// <summary>The Snapshot topology must carry exactly 8 Water Jets in ordinal order.</summary>
    public const string TopologyWaterJets = "TOPOLOGY_WATER_JETS";

    /// <summary>The Snapshot topology must carry exactly 8 Isolation Valves in ordinal order.</summary>
    public const string TopologyIsolationValves = "TOPOLOGY_ISOLATION_VALVES";

    /// <summary>Every WJn must pair with IVn and vice versa; no other pairing exists.</summary>
    public const string TopologyPairing = "TOPOLOGY_PAIRING";

    /// <summary>A Sensor's assigned Water Jet must exist, target the Sensor's wall/region, and derive its Isolation Valve.</summary>
    public const string SensorAssignment = "SENSOR_ASSIGNMENT";

    /// <summary>A Sensor slot must carry exactly the canonical Sensor identity for its position.</summary>
    public const string WallMapSensorBinding = "WALL_MAP_SENSOR_BINDING";

    /// <summary>Exactly 108 Sensor projections are required (I7 and I16 included).</summary>
    public const string SensorCount = "SENSOR_COUNT";

    /// <summary>Sensor projections must be ordered by ScanOrder 1..108 without gaps.</summary>
    public const string SensorOrder = "SENSOR_ORDER";

    /// <summary>A Sensor projection disagrees with the canonical position identity.</summary>
    public const string SensorIdentity = "SENSOR_IDENTITY";

    /// <summary>Per-wall Sensor counts must be 24 / 30 / 24 / 30.</summary>
    public const string SensorWallCounts = "SENSOR_WALL_COUNTS";

    /// <summary>Each Sensor carries exactly two distinct, non-blank Thermocouple channels.</summary>
    public const string SensorChannels = "SENSOR_TC_CHANNELS";

    /// <summary>The mapping must supply exactly 216 distinct Thermocouple channels (108 × 2).</summary>
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

    // ---------------------------------------------------------------------
    // One-Node physical TC read-only Product integration.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The device profile and the TC acquisition variant disagree: a SIMULATOR revision carried
    /// physical evidence, or a TEST_HARDWARE revision carried no physical quarantined record. The
    /// profile is never silently substituted and TEST_HARDWARE is never a general-purpose profile.
    /// </summary>
    public const string PhysicalTcProfileMismatch = "PHYSICAL_TC_PROFILE_MISMATCH";

    /// <summary>
    /// A physical TC acquisition record failed structural validation: wrong Node or Cabinet
    /// identity, non-quarantined, wrong word count, a missing or altered Not-Verified set, a
    /// missing evidence block, or an evidence block that does not carry the exact authorized
    /// read-only profile. Nothing was committed.
    /// </summary>
    public const string PhysicalTcRecordInvalid = "PHYSICAL_TC_RECORD_INVALID";
}
