using System.Collections.ObjectModel;
using System.Globalization;

namespace Wjss.Domain;

// Phase 3A Cleaning Path domain contracts: the Owner-approved coordinate contract
// (integer millimetres 0-280 inclusive; native = integerMillimetres x 10000, checked overflow;
// Centre 125/125 mm = 1250000/1250000 native; no rounding, clamping, defaults or correction),
// the immutable validated snapshot, the Sensor-owned six-point path model and the structured
// complete-file validation error vocabulary. Pure data and pure conversion only: no motion,
// no Pump/IV behaviour, no execution and no topology authoring.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>
/// The two coordinates of one Cleaning Path point. X in the Legacy identity is the Horizontal
/// coordinate and Z is the Vertical coordinate; the Owner-approved path model names them by role.
/// </summary>
public enum CleaningPathCoordinateIdentity
{
    HORIZONTAL = 1,
    VERTICAL = 2,
}

/// <summary>
/// Where a validated Cleaning Path snapshot came from. The runtime state machine of later phases
/// may consume only snapshots whose source is <see cref="OwnerLocalFile"/>; the Legacy-shape source
/// exists solely for the Phase 3A migration/validation inspection path.
/// </summary>
public enum CleaningPathSourceKind
{
    /// <summary>An Owner-local Cleaning Path Store file, kept outside the repository.</summary>
    OwnerLocalFile = 1,

    /// <summary>A migration-inspection result produced from Legacy Actual-Run-shaped input. Never a runtime load source.</summary>
    LegacyMigrationInspection = 2,
}

/// <summary>
/// Stable machine codes for Cleaning Path store loading, complete-file validation and resolution.
/// Codes are part of the Product contract: they may be added but never renamed or reused.
/// </summary>
public static class CleaningPathCodes
{
    // File / parse boundary.
    public const string StoreNotProvided = "STORE_NOT_PROVIDED";
    public const string StoreTooLarge = "STORE_TOO_LARGE";
    public const string StoreFileRefused = "STORE_FILE_REFUSED";
    public const string JsonMalformed = "JSON_MALFORMED";
    public const string RootWrongType = "ROOT_WRONG_TYPE";
    public const string JsonPropertyUnknown = "JSON_PROPERTY_UNKNOWN";
    public const string JsonPropertyDuplicate = "JSON_PROPERTY_DUPLICATE";

    // Schema identity.
    public const string SchemaMissing = "SCHEMA_MISSING";
    public const string SchemaWrongType = "SCHEMA_WRONG_TYPE";
    public const string SchemaUnsupported = "SCHEMA_UNSUPPORTED";

    // Path collection / path entries.
    public const string PathsMissing = "PATHS_MISSING";
    public const string PathsWrongType = "PATHS_WRONG_TYPE";
    public const string PathWrongType = "PATH_WRONG_TYPE";
    public const string PathCount = "PATH_COUNT";

    // Sensor identity.
    public const string SensorIdMissing = "SENSOR_ID_MISSING";
    public const string SensorIdWrongType = "SENSOR_ID_WRONG_TYPE";
    public const string SensorIdUnknown = "SENSOR_ID_UNKNOWN";
    public const string SensorIdDuplicate = "SENSOR_ID_DUPLICATE";
    public const string CanonicalSensorMissing = "CANONICAL_SENSOR_MISSING";

    // Point collection / point entries.
    public const string PointsMissing = "POINTS_MISSING";
    public const string PointsWrongType = "POINTS_WRONG_TYPE";
    public const string PointWrongType = "POINT_WRONG_TYPE";
    public const string PointsCount = "POINTS_COUNT";
    public const string PointIdMissing = "POINT_ID_MISSING";
    public const string PointIdWrongType = "POINT_ID_WRONG_TYPE";
    public const string PointIdUnknown = "POINT_ID_UNKNOWN";
    public const string PointIdDuplicate = "POINT_ID_DUPLICATE";
    public const string PointOrderInvalid = "POINT_ORDER_INVALID";

    // Coordinate values.
    public const string CoordinateMissing = "COORDINATE_MISSING";
    public const string CoordinateWrongType = "COORDINATE_WRONG_TYPE";
    public const string CoordinateValueInvalid = "COORDINATE_VALUE_INVALID";
    public const string CoordinateNotIntegerMillimetres = "COORDINATE_NOT_INTEGER_MILLIMETRES";
    public const string CoordinateBelowMinimum = "COORDINATE_BELOW_MINIMUM";
    public const string CoordinateAboveMaximum = "COORDINATE_ABOVE_MAXIMUM";
    public const string CoordinateConversionOverflow = "COORDINATE_CONVERSION_OVERFLOW";

    // Resolution.
    public const string ResolutionAccepted = "RESOLUTION_ACCEPTED";
    public const string SensorNotProductSensor = "SENSOR_NOT_PRODUCT_SENSOR";
    public const string SensorConfigurationIdMismatch = "SENSOR_CONFIGURATION_ID_MISMATCH";
    public const string SensorNotInSnapshot = "SENSOR_NOT_IN_SNAPSHOT";
    public const string OverrideWaterJetUnknown = "OVERRIDE_WJ_UNKNOWN";
    public const string OverrideTopologyIncompatible = "OVERRIDE_TOPOLOGY_INCOMPATIBLE";
    public const string AssignmentTopologyIncompatible = "ASSIGNMENT_TOPOLOGY_INCOMPATIBLE";
    public const string ResolutionDerivationFailed = "RESOLUTION_DERIVATION_FAILED";
}

/// <summary>
/// The Owner-approved engineering↔native coordinate contract, implemented exactly:
/// integer millimetre values only (parsed as decimal; mathematically integral values such as
/// 125, 125.0 and 125.00 are accepted and 125.5 is refused), accepted range 0–280 mm inclusive
/// on every axis of every motion controller, and an exact checked conversion
/// native = integerMillimetres × 10000. There is no rounding, no clamping, no default
/// coordinate and no fallback: an out-of-contract value is always a validation error.
/// </summary>
public static class CleaningPathUnits
{
    /// <summary>The smallest accepted engineering coordinate, in whole millimetres.</summary>
    public const int MinimumMillimetres = 0;

    /// <summary>The largest accepted engineering coordinate, in whole millimetres.</summary>
    public const int MaximumMillimetres = 280;

    /// <summary>The largest accepted native coordinate (280 mm × 10000).</summary>
    public const int MaximumNative = 2800000;

    /// <summary>The native units produced by one engineering millimetre (280 mm → 2800000).</summary>
    public const int NativePerMillimetre = 10000;

    /// <summary>Mandatory Safe Return / Centre Horizontal coordinate in whole millimetres (Owner-approved).</summary>
    public const int SafeReturnCentreHorizontalMillimetres = 125;

    /// <summary>Mandatory Safe Return / Centre Vertical coordinate in whole millimetres (Owner-approved).</summary>
    public const int SafeReturnCentreVerticalMillimetres = 125;

    /// <summary>The fixed Centre contract in both unit systems (125 mm → 1250000 native on both axes).</summary>
    public static CleaningPathCentre SafeReturnCentre { get; } = new()
    {
        HorizontalMillimetres = SafeReturnCentreHorizontalMillimetres,
        VerticalMillimetres = SafeReturnCentreVerticalMillimetres,
        HorizontalNative = SafeReturnCentreHorizontalMillimetres * NativePerMillimetre,
        VerticalNative = SafeReturnCentreVerticalMillimetres * NativePerMillimetre,
    };

    /// <summary>
    /// Exact checked conversion native = integerMillimetres × 10000. Returns false when the
    /// product is not representable as a native (32-bit) coordinate. Never rounds, clamps or
    /// substitutes a default; callers refuse the value instead.
    /// </summary>
    public static bool TryConvertMillimetresToNative(long integerMillimetres, out int native)
    {
        try
        {
            var product = checked(integerMillimetres * NativePerMillimetre);
            if (product < int.MinValue || product > int.MaxValue)
            {
                native = 0;
                return false;
            }

            native = (int)product;
            return true;
        }
        catch (OverflowException)
        {
            native = 0;
            return false;
        }
    }
}

/// <summary>One validated Cleaning Path point, carrying the authored integer-mm value and its exact native conversion.</summary>
public sealed record CleaningPathPoint
{
    /// <summary>"P1" … "P6".</summary>
    public required string PointId { get; init; }

    /// <summary>Authored Horizontal (Legacy X) coordinate in whole millimetres.</summary>
    public required int HorizontalMillimetres { get; init; }

    /// <summary>Authored Vertical (Legacy Z) coordinate in whole millimetres.</summary>
    public required int VerticalMillimetres { get; init; }

    /// <summary>Horizontal native coordinate (millimetres × 10000).</summary>
    public required int HorizontalNative { get; init; }

    /// <summary>Vertical native coordinate (millimetres × 10000).</summary>
    public required int VerticalNative { get; init; }
}

/// <summary>
/// The Sensor-owned Cleaning Path of exactly one Sensor: exactly six ordered points P1…P6.
/// The path is Sensor-owned data: an Effective-WJ resolution never changes it.
/// Named <c>SensorCleaningPath</c> so the type name can never collide with the
/// <c>Wjss.Runtime.Core.CleaningPath</c> namespace under C# name lookup.
/// </summary>
public sealed record SensorCleaningPath
{
    /// <summary>The canonical Sensor identity (one of the 108 Product Sensors).</summary>
    public required string SensorId { get; init; }

    /// <summary>Exactly six points, P1 first and P6 last.</summary>
    public required IReadOnlyList<CleaningPathPoint> Points { get; init; }
}

/// <summary>The fixed Mandatory Safe Return / Centre contract in whole millimetres and exact native units.</summary>
public sealed record CleaningPathCentre
{
    public required int HorizontalMillimetres { get; init; }
    public required int VerticalMillimetres { get; init; }
    public required int HorizontalNative { get; init; }
    public required int VerticalNative { get; init; }
}

/// <summary>
/// One structured, machine-readable validation problem. Coordinate errors always carry the Sensor
/// identity (when available), the point identity (when available), the coordinate identity, the
/// offending value in invariant form (when safely available) and the allowed contract. The
/// record is Product data: presentation never relies on concatenated human-readable strings alone.
/// </summary>
public sealed record CleaningPathValidationError
{
    /// <summary>Stable code from <see cref="CleaningPathCodes"/>.</summary>
    public required string Code { get; init; }

    /// <summary>Zero-based file-order index of the path entry the error belongs to, when available.</summary>
    public int? PathIndex { get; init; }

    /// <summary>Zero-based index of the point entry the error belongs to, when available.</summary>
    public int? PointIndex { get; init; }

    /// <summary>The Sensor identity when the file supplies one (or the missing canonical identity).</summary>
    public string? SensorId { get; init; }

    /// <summary>The point identity ("P1" … "P6") when available.</summary>
    public string? PointId { get; init; }

    /// <summary>The coordinate identity for coordinate errors.</summary>
    public CleaningPathCoordinateIdentity? Coordinate { get; init; }

    /// <summary>The offending value in invariant form (raw JSON token or scalar text), when safely available.</summary>
    public string? OffendingValue { get; init; }

    /// <summary>Short invariant explanation of the refusal.</summary>
    public required string Reason { get; init; }

    /// <summary>The allowed contract the offending data violates.</summary>
    public required string AllowedContract { get; init; }
}

/// <summary>
/// One immutable validated Cleaning Path snapshot. It is produced only when the complete file
/// validated with zero errors; a refused file produces no snapshot at all. The later execution
/// state machine consumes only this snapshot and never rereads the raw file text.
/// </summary>
public sealed record CleaningPathSnapshot
{
    /// <summary>The schema identifier the snapshot was validated against.</summary>
    public required string Schema { get; init; }

    /// <summary>Where the validated data came from.</summary>
    public required CleaningPathSourceKind SourceKind { get; init; }

    /// <summary>The load instant, when the loader supplied one. Null for pure parser calls.</summary>
    public DateTimeOffset? LoadedAtUtc { get; init; }

    /// <summary>The fixed Centre contract (125 mm / 125 mm; 1250000 / 1250000 native).</summary>
    public required CleaningPathCentre Centre { get; init; }

    /// <summary>Every validated Sensor path, keyed by canonical Sensor identity.</summary>
    public required IReadOnlyDictionary<string, SensorCleaningPath> PathsBySensorId { get; init; }

    /// <summary>Every validated Sensor path in canonical Sensor sequence order.</summary>
    public required IReadOnlyList<SensorCleaningPath> Paths { get; init; }

    /// <summary>Returns one Sensor-owned path, or null when the snapshot carries none.</summary>
    public SensorCleaningPath? PathFor(string sensorId) =>
        PathsBySensorId.TryGetValue(sensorId, out var path) ? path : null;

    /// <summary>Builds a snapshot from validated paths (map is derived; callers never pass topology data).</summary>
    public static CleaningPathSnapshot Create(
        string schema,
        CleaningPathSourceKind sourceKind,
        IReadOnlyList<SensorCleaningPath> canonicalOrderPaths,
        DateTimeOffset? loadedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(canonicalOrderPaths);

        var map = new Dictionary<string, SensorCleaningPath>(canonicalOrderPaths.Count, StringComparer.Ordinal);
        foreach (var path in canonicalOrderPaths)
        {
            map[path.SensorId] = path;
        }

        return new CleaningPathSnapshot
        {
            Schema = schema,
            SourceKind = sourceKind,
            LoadedAtUtc = loadedAtUtc,
            Centre = CleaningPathUnits.SafeReturnCentre,
            PathsBySensorId = new ReadOnlyDictionary<string, SensorCleaningPath>(map),
            Paths = new ReadOnlyCollection<SensorCleaningPath>(canonicalOrderPaths.ToList()),
        };
    }
}

/// <summary>
/// The complete-file validation result. Acceptance is all-or-nothing: any error refuses the file,
/// the snapshot is never created, no partial path set exists and no invalid point is skipped.
/// </summary>
public sealed class CleaningPathStoreResult
{
    private CleaningPathStoreResult(bool accepted, CleaningPathSnapshot? snapshot, IReadOnlyList<CleaningPathValidationError> errors)
    {
        Accepted = accepted;
        Snapshot = snapshot;
        Errors = errors;
    }

    /// <summary>True only when the complete file validated with zero errors.</summary>
    public bool Accepted { get; }

    /// <summary>The accepted snapshot, or null when the file was refused.</summary>
    public CleaningPathSnapshot? Snapshot { get; }

    /// <summary>Every discoverable structured validation error (empty exactly when accepted).</summary>
    public IReadOnlyList<CleaningPathValidationError> Errors { get; }

    /// <summary>A refused result carrying every recorded error.</summary>
    public static CleaningPathStoreResult Refused(IReadOnlyList<CleaningPathValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CleaningPathStoreResult(false, null, new ReadOnlyCollection<CleaningPathValidationError>(errors.ToList()));
    }

    /// <summary>A refused result carrying a single structured error (file-boundary refusals).</summary>
    public static CleaningPathStoreResult Refused(CleaningPathValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Refused(new[] { error });
    }

    /// <summary>An accepted result for one validated snapshot.</summary>
    public static CleaningPathStoreResult FromSnapshot(CleaningPathSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CleaningPathStoreResult(true, snapshot, Array.Empty<CleaningPathValidationError>());
    }
}
