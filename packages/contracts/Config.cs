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

// ---------------------------------------------------------------------------
// Stage 0.3A-3 Checkpoint B — legacy sensor-parameter migration contracts
// (ADR-0017, Owner-approved Checkpoint A + Checkpoint B authorization).
//
// Approved dispositions only: the importer never normalizes suspicious values
// silently, never converts an offset-free timestamp to UTC, never invents a
// duration, and never derives a domain boolean that the Owner has not approved.
// Acquisition bindings are deferred raw provenance and are never Production
// configuration. SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
// ---------------------------------------------------------------------------

/// <summary>Approved per-field migration dispositions (ADR-0017 CSV field-classification matrix).</summary>
public enum MigrationFieldDisposition
{
    /// <summary>Imported verbatim (identity, layout, and approved direct renames).</summary>
    IMPORT,

    /// <summary>Imported with a recorded, deterministic normalization action (token or rename mapping).</summary>
    IMPORT_WITH_NORMALIZATION,

    /// <summary>Imported verbatim with a recorded warning and an Owner-review entry.</summary>
    PRESERVE_WITH_WARNING,

    /// <summary>Retained raw for a later explicit Owner decision; consumed by nothing.</summary>
    DEFER,

    /// <summary>Excluded from sensor import because the row is a NON_SENSOR_GAP logical-position record (I7, I16).</summary>
    REJECT_FOR_I7_I16,
}

/// <summary>The approved classification of one legacy CSV field (documents the matrix; data only, no behaviour).</summary>
public sealed record MigrationFieldClassification
{
    public required string Field { get; init; }
    public required string Classification { get; init; }
    public required string CanonicalTarget { get; init; }
    public required MigrationFieldDisposition Disposition { get; init; }
}

/// <summary>
/// Raw, deferred acquisition-binding provenance for one legacy record. These strings are
/// preserved verbatim for a later explicit Owner decision; they are NOT approved
/// Production-device configuration, no Production channel identity, network address, or
/// physical binding is created or claimed, and nothing may ever use them to contact or
/// address a device. The public repository never carries real values: the committed
/// example leaves this record null.
/// </summary>
public sealed record DeferredAcquisitionProvenance
{
    /// <summary>Raw legacy acquisition-endpoint column value (protocol-agnostic provenance; DEFER).</summary>
    public required string AcquisitionEndpointRaw { get; init; }

    /// <summary>Raw legacy channel-pair column value (provenance only; DEFER).</summary>
    public required string ChannelPairRaw { get; init; }

    /// <summary>Raw legacy acquisition base-address column value (provenance only; DEFER).</summary>
    public required string AcquisitionBaseAddressRaw { get; init; }
}

/// <summary>
/// Canonical configuration of one actual Sensor — exactly 106 records after a successful
/// import, never one for a NON_SENSOR_GAP position. Approved imports only: dirty-score
/// settings are direct recorded renames of the legacy numeric values (values remain
/// [NOT VERIFIED]); Enabled comes from the legacy enable token; CleaningCount is the raw
/// copy of the legacy count. Four domain derivations are PROHIBITED pending an explicit
/// Owner decision: no UseDirtyScoreThreshold, no HasVerifiedCleaningHistory, no
/// LastSuccessfulCleaningCompletedAt, and no HardMinimumCleaningInterval — the
/// corresponding legacy values are preserved raw below instead.
/// </summary>
public sealed record SensorConfigurationRecord
{
    /// <summary>Canonical Sensor ID — the legacy sensorname value for SENSOR rows, validated against the orderTotal-derived logical label.</summary>
    public required string SensorId { get; init; }

    public required Wall Wall { get; init; }
    public required int LogicalColumn { get; init; }
    public required int LogicalRow { get; init; }
    public required int WallColumn { get; init; }
    public required int WallRow { get; init; }

    /// <summary>Wall-local scan order preserved from the legacy record.</summary>
    public required int OrderWall { get; init; }

    /// <summary>Dense one-based scan ordinal 1–106, derived deterministically by sorting the SENSOR logical positions by OrderTotal and excluding the NON_SENSOR_GAP positions; the sequence skips I7 and I16.</summary>
    public required int ScanOrder { get; init; }

    /// <summary>Direct recorded rename of the legacy min temperature dirty-score bound. Value [NOT VERIFIED].</summary>
    public required double DiffLowerBound { get; init; }

    /// <summary>Direct recorded rename of the legacy max temperature dirty-score bound. Value [NOT VERIFIED].</summary>
    public required double DiffUpperBound { get; init; }

    /// <summary>Direct recorded rename of the legacy threshold setpoint. Value [NOT VERIFIED]. Whether it gates eligibility (UseDirtyScoreThreshold) is a deferred Owner decision.</summary>
    public required double DirtyScoreThreshold { get; init; }

    /// <summary>Canonicalized from the legacy enable token. No other domain boolean is derived.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Raw copy of the legacy cleaning count. Whether it implies verified history is a deferred Owner decision.</summary>
    public required int CleaningCount { get; init; }

    /// <summary>Exactly one assigned Water Jet, mapped DIRECTLY from the legacy cleaning-device ordinal (cannon n → WJn; never remapped by wall).</summary>
    public required string AssignedWaterJetId { get; init; }

    /// <summary>Derived ONLY through the WJn ↔ IVn pairing; never independently assigned.</summary>
    public required string AssignedIsolationValveId { get; init; }

    /// <summary>Legacy database record identifier (provenance only; never an identity).</summary>
    public required string LegacyRecordId { get; init; }

    /// <summary>Raw offset-free legacy last-clean timestamp. Source timezone UNKNOWN; never converted to UTC; no LastSuccessfulCleaningCompletedAt is created in Checkpoint B; never consumed by queue or cleaning behaviour.</summary>
    public string? LastCleanTimestampRaw { get; init; }

    /// <summary>Raw legacy minimum re-queue interval value. Unit UNKNOWN; no TimeSpan is invented; no HardMinimumCleaningInterval mapping; never consumed by Queue eligibility or Runtime behaviour.</summary>
    public string? MinTimeAllowAddToQueueRaw { get; init; }

    /// <summary>Raw legacy value with no approved canonical attribute (deferred Owner decision).</summary>
    public string? MaxTimeAllowAddToQueueRaw { get; init; }

    /// <summary>Raw legacy value with no approved canonical attribute (deferred Owner decision).</summary>
    public string? MaxTimeEnableRaw { get; init; }

    /// <summary>Raw legacy parameter-audit stamp; provenance only, never a cleaning-history timestamp.</summary>
    public string? LatestUpdateParamsTimestampRaw { get; init; }

    /// <summary>Deferred acquisition-binding provenance (raw strings only; never device configuration). Null when the legacy record carries no values.</summary>
    public DeferredAcquisitionProvenance? DeferredAcquisition { get; init; }
}

/// <summary>One recorded migration warning (warning-level outcome; the value is preserved verbatim and listed for Owner review).</summary>
public sealed record MigrationWarningRecord
{
    /// <summary>Machine code, e.g. MIGRATION_TIMEZONE_UNKNOWN (see MigrationRefusalCodes for the vocabulary).</summary>
    public required string Code { get; init; }

    /// <summary>Legacy record identifier the warning belongs to (provenance key, not an identity).</summary>
    public required string LegacyRecordId { get; init; }

    /// <summary>Legacy field name the warning belongs to.</summary>
    public required string Field { get; init; }

    /// <summary>Deterministic, operator-readable detail.</summary>
    public required string Detail { get; init; }
}

/// <summary>One fail-closed migration refusal. A refusal aborts the ENTIRE import: no partial result exists.</summary>
public sealed record MigrationRefusalRecord
{
    public required string Code { get; init; }
    public required string Detail { get; init; }
}

/// <summary>
/// The complete, atomic result of a successful legacy sensor-parameter import: the whole
/// canonical topology plus warnings. Deterministic: identical CSV bytes produce identical
/// results (same records, same warning order, same codes).
/// </summary>
public sealed record SensorParameterMigrationResult
{
    /// <summary>All 108 logical positions, ordered strictly ascending by OrderTotal.</summary>
    public required IReadOnlyList<LogicalPositionRecord> LogicalPositions { get; init; }

    /// <summary>The 106 actual Sensors, ordered strictly ascending by ScanOrder (1–106).</summary>
    public required IReadOnlyList<SensorConfigurationRecord> Sensors { get; init; }

    /// <summary>The 8 Water Jets, in ordinal order WJ1–WJ8.</summary>
    public required IReadOnlyList<WaterJetConfiguration> WaterJets { get; init; }

    /// <summary>The 8 Isolation Valves, in ordinal order IV1–IV8.</summary>
    public required IReadOnlyList<IsolationValveConfiguration> IsolationValves { get; init; }

    /// <summary>Recorded warnings, ordered deterministically by (OrderTotal, Code, Field).</summary>
    public required IReadOnlyList<MigrationWarningRecord> Warnings { get; init; }
}

/// <summary>
/// The outcome of one import attempt: EITHER a refusal (fail-closed, atomic —
/// <see cref="Result"/> is null and nothing was imported) OR a complete result
/// (<see cref="Refusal"/> is null). No third state exists.
/// </summary>
public sealed record SensorParameterImportOutcome
{
    public required bool Accepted { get; init; }
    public required MigrationRefusalRecord? Refusal { get; init; }
    public required SensorParameterMigrationResult? Result { get; init; }

    public static SensorParameterImportOutcome Refused(string code, string detail) => new()
    {
        Accepted = false,
        Refusal = new MigrationRefusalRecord { Code = code, Detail = detail },
        Result = null,
    };

    public static SensorParameterImportOutcome FromResult(SensorParameterMigrationResult result) => new()
    {
        Accepted = true,
        Refusal = null,
        Result = result,
    };
}

/// <summary>
/// Machine refusal/warning code vocabulary for the legacy sensor-parameter migration
/// (ADR-0017 decision 11). Additive only; no existing Runtime refusal code is changed.
/// Structural codes fail the whole import; the three WARNING-level codes
/// (<see cref="MigrationPlaceholderValue"/>, <see cref="MigrationUnitUnverified"/>,
/// <see cref="MigrationTimezoneUnknown"/>) preserve the value verbatim and record a
/// warning instead.
/// </summary>
public static class MigrationRefusalCodes
{
    public const string TopoPositionCount = "TOPO_POSITION_COUNT";
    public const string TopoSensorCount = "TOPO_SENSOR_COUNT";
    public const string TopoChannelCount = "TOPO_CHANNEL_COUNT";
    public const string TopoWallCounts = "TOPO_WALL_COUNTS";
    public const string TopoGapIdentity = "TOPO_GAP_IDENTITY";
    public const string TopoDuplicateId = "TOPO_DUPLICATE_ID";
    public const string TopoDuplicateOrder = "TOPO_DUPLICATE_ORDER";
    public const string TopoWaterJetCount = "TOPO_WATERJET_COUNT";
    public const string TopoValveCount = "TOPO_VALVE_COUNT";
    public const string TopoPairingMismatch = "TOPO_PAIRING_MISMATCH";
    public const string TopoTargetWallContradiction = "TOPO_TARGET_WALL_CONTRADICTION";
    public const string MigrationCannonRange = "MIGRATION_CANNON_RANGE";
    public const string MigrationMissingAssignment = "MIGRATION_MISSING_ASSIGNMENT";
    public const string MigrationLogicalLabelMismatch = "MIGRATION_LOGICAL_LABEL_MISMATCH";
    public const string MigrationFieldToken = "MIGRATION_FIELD_TOKEN";
    public const string MigrationBoundsOrder = "MIGRATION_BOUNDS_ORDER";

    /// <summary>WARNING-level: zero/degenerate bounds, threshold, or sentinel-like timestamp on an actual Sensor; the value is preserved and listed for Owner review.</summary>
    public const string MigrationPlaceholderValue = "MIGRATION_PLACEHOLDER_VALUE";

    /// <summary>WARNING-level: time-field unit UNKNOWN; the raw value is preserved, no conversion is applied, no duration is invented.</summary>
    public const string MigrationUnitUnverified = "MIGRATION_UNIT_UNVERIFIED";

    /// <summary>WARNING-level: offset-free timestamp imported; source timezone UNKNOWN; the raw string is preserved and NO UTC conversion is performed.</summary>
    public const string MigrationTimezoneUnknown = "MIGRATION_TIMEZONE_UNKNOWN";
}
