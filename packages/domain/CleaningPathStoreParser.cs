using System.Globalization;
using System.Text.Json;
using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Strict, fail-closed complete-file parser and validator for the Owner-local Cleaning Path Store
/// and for the Phase 3A Legacy migration/validation inspection path. The parser is pure: it never
/// reads a file, never writes anything and never discloses a source location. Every accepted file
/// must validate completely: 108 canonical Sensor paths, exactly six ordered points P1…P6 each,
/// and Owner-approved coordinates (parsed as decimal, mathematically integral millimetres only,
/// 0–280 inclusive, exact checked native = millimetres × 10000). Any error refuses the whole
/// file: no snapshot is created, no partial path set is accepted, no invalid point is skipped and
/// no value is rounded, clamped or corrected. Independently discoverable errors are collected in
/// one pass; structural traversal stops only where malformed JSON makes safe traversal impossible.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public static class CleaningPathStoreParser
{
    /// <summary>The only supported schema identifier of the canonical Owner-local store.</summary>
    public const string SchemaIdentifier = "wjss.cleaning-path-store/1";

    /// <summary>
    /// The largest accepted store text, in characters. The complete store carries 108 paths × 6
    /// points × two bounded coordinate tokens (≈ 55 KiB compact, ≈ 120 KiB indented); 256 KiB
    /// admits comfortably indented files while refusing pathological input. Refusal is fail-closed
    /// and never truncates: an over-bound file is refused whole.
    /// </summary>
    public const int MaximumTextLength = 262144;

    private const string SchemaProperty = "schema";
    private const string PathsProperty = "paths";
    private const string SensorIdProperty = "sensorId";
    private const string PointsProperty = "points";
    private const string PointIdProperty = "pointId";
    private const string HorizontalProperty = "horizontalMillimetres";
    private const string VerticalProperty = "verticalMillimetres";

    // Legacy Actual-Run shape property names (migration/validation inspection only).
    private const string LegacyPathNameProperty = "PathName";
    private const string LegacyCoordinatesProperty = "Coordinates";
    private const string LegacyXProperty = "X";
    private const string LegacyZProperty = "Z";

    /// <summary>The canonical point identities, in canonical order.</summary>
    public static readonly IReadOnlyList<string> ExpectedPointIds = Array.AsReadOnly(["P1", "P2", "P3", "P4", "P5", "P6"]);

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
    };

    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal) { SchemaProperty, PathsProperty };
    private static readonly HashSet<string> PathProperties = new(StringComparer.Ordinal) { SensorIdProperty, PointsProperty };
    private static readonly HashSet<string> PointProperties = new(StringComparer.Ordinal) { PointIdProperty, HorizontalProperty, VerticalProperty };
    private static readonly HashSet<string> LegacyPathProperties = new(StringComparer.Ordinal) { LegacyPathNameProperty, LegacyCoordinatesProperty };
    private static readonly HashSet<string> LegacyPointProperties = new(StringComparer.Ordinal) { LegacyXProperty, LegacyZProperty };

    private const string CoordinateContract = "integer millimetres 0-280 inclusive; native = millimetres x 10000";
    private const string PointIdContract = "pointId is one of P1, P2, P3, P4, P5, P6; exactly once each in canonical order";
    private const string PointsContract = "exactly 6 points per Sensor path";
    private const string PathCountContract = "exactly 108 Sensor paths (one per canonical Product Sensor)";

    /// <summary>
    /// Parses and completely validates one canonical Owner-local Cleaning Path store text.
    /// Never throws for malformed input. Acceptance produces exactly one immutable snapshot
    /// (source <see cref="CleaningPathSourceKind.OwnerLocalFile"/>).
    /// </summary>
    public static CleaningPathStoreResult Parse(string? text, DateTimeOffset? loadedAtUtc = null)
    {
        if (!TryOpenDocument(text, out var boundaryResult, out var document))
        {
            return boundaryResult;
        }

        using (document)
        {
            var errors = new List<CleaningPathValidationError>();
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(Error(CleaningPathCodes.RootWrongType, reason: "the store root is not a JSON object", allowed: "root is a JSON object with properties schema and paths"));
                return CleaningPathStoreResult.Refused(errors);
            }

            var root = ReadObject(document.RootElement, RootProperties, null, null, errors);
            ValidateSchema(root, errors);
            var drafts = new List<PathDraft>();
            if (root.TryGetValue(PathsProperty, out var pathsElement))
            {
                if (pathsElement.ValueKind == JsonValueKind.Array)
                {
                    drafts = ReadPathCollection(pathsElement, true, errors);
                    ValidateCompleteness(drafts, errors);
                }
                else
                {
                    errors.Add(Error(CleaningPathCodes.PathsWrongType,
                        offending: RawScalar(pathsElement),
                        reason: "the path collection must be a JSON array of Sensor path objects",
                        allowed: "a JSON array with exactly one path object per Sensor"));
                }
            }
            else
            {
                errors.Add(Error(CleaningPathCodes.PathsMissing,
                    reason: "the path collection is absent",
                    allowed: "a JSON array with exactly one path object per Sensor"));
            }

            return Finish(SchemaIdentifier, CleaningPathSourceKind.OwnerLocalFile, drafts, errors, loadedAtUtc);
        }
    }

    /// <summary>
    /// Migration/validation inspection entry: parses and completely validates one Legacy
    /// Actual-Run-shaped path collection (an array of { PathName, Coordinates:[{ X, Z }] }).
    /// Point identity is positional (P1…P6 in stored order); X is Horizontal and Z is Vertical.
    /// This entry exists so that every out-of-range Legacy coordinate is reported precisely; it
    /// never claims migration success while any Sensor path is invalid, and the produced snapshot
    /// source is never a runtime load source. Never throws for malformed input.
    /// </summary>
    public static CleaningPathStoreResult ParseLegacyActualRunShape(string? text, DateTimeOffset? loadedAtUtc = null)
    {
        if (!TryOpenDocument(text, out var boundaryResult, out var document))
        {
            return boundaryResult;
        }

        using (document)
        {
            var errors = new List<CleaningPathValidationError>();
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add(Error(CleaningPathCodes.RootWrongType, reason: "the Legacy store root is not a JSON array", allowed: "root is a JSON array of path objects"));
                return CleaningPathStoreResult.Refused(errors);
            }

            var drafts = ReadPathCollection(document.RootElement, canonicalShape: false, errors);
            ValidateCompleteness(drafts, errors);

            return Finish(SchemaIdentifier, CleaningPathSourceKind.LegacyMigrationInspection, drafts, errors, loadedAtUtc);
        }
    }

    private static bool TryOpenDocument(string? text, out CleaningPathStoreResult boundaryResult, out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            boundaryResult = CleaningPathStoreResult.Refused(Error(CleaningPathCodes.StoreNotProvided, reason: "no store text was provided", allowed: "a complete Cleaning Path store text"));
            return false;
        }

        if (text.Length > MaximumTextLength)
        {
            boundaryResult = CleaningPathStoreResult.Refused(Error(CleaningPathCodes.StoreTooLarge, reason: "the store text exceeds the accepted size bound", allowed: $"store text at most {MaximumTextLength.ToString(CultureInfo.InvariantCulture)} characters"));
            return false;
        }

        try
        {
            document = JsonDocument.Parse(text, DocumentOptions);
            boundaryResult = null!;
            return true;
        }
        catch (JsonException)
        {
            boundaryResult = CleaningPathStoreResult.Refused(Error(CleaningPathCodes.JsonMalformed, reason: "the store text is not well-formed JSON (or uses comments or trailing commas)", allowed: "strict JSON: no comments, no trailing commas"));
            return false;
        }
    }

    private static CleaningPathStoreResult Finish(
        string schema,
        CleaningPathSourceKind sourceKind,
        List<PathDraft> drafts,
        List<CleaningPathValidationError> errors,
        DateTimeOffset? loadedAtUtc)
    {
        if (errors.Count > 0)
        {
            return CleaningPathStoreResult.Refused(errors);
        }

        // Zero errors guarantees exactly one draft per canonical Sensor, each with six valid points.
        var bySensor = new Dictionary<string, PathDraft>(drafts.Count, StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            bySensor[draft.SensorId] = draft;
        }

        var ordered = new List<SensorCleaningPath>(drafts.Count);
        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            var draft = bySensor[sensorId];
            ordered.Add(new SensorCleaningPath
            {
                SensorId = draft.SensorId,
                Points = new System.Collections.ObjectModel.ReadOnlyCollection<CleaningPathPoint>(draft.Points),
            });
        }

        return CleaningPathStoreResult.FromSnapshot(CleaningPathSnapshot.Create(schema, sourceKind, ordered, loadedAtUtc));
    }

    private sealed class PathDraft
    {
        public PathDraft(string sensorId) => SensorId = sensorId;

        public string SensorId { get; }

        public List<CleaningPathPoint> Points { get; } = new();
    }

    /// <summary>
    /// Reads the path collection of one shape. Structural errors short-circuit only the unsafe
    /// subtree (never the whole pass) except for malformed JSON, which stops all traversal.
    /// </summary>
    private static List<PathDraft> ReadPathCollection(JsonElement pathsElement, bool canonicalShape, List<CleaningPathValidationError> errors)
    {
        var drafts = new List<PathDraft>();

        if (pathsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(Error(
                canonicalShape ? CleaningPathCodes.PathsWrongType : CleaningPathCodes.RootWrongType,
                offending: RawScalar(pathsElement),
                reason: canonicalShape ? "paths is not a JSON array" : "the Legacy store root is not a JSON array",
                allowed: canonicalShape ? "root property paths (array of Sensor paths)" : "root is a JSON array of path objects"));
            return drafts;
        }

        if (pathsElement.GetArrayLength() != CanonicalSensorMap.SensorLocations)
        {
            errors.Add(Error(CleaningPathCodes.PathCount,
                offending: pathsElement.GetArrayLength().ToString(CultureInfo.InvariantCulture),
                reason: "the store does not carry one path entry per canonical Product Sensor",
                allowed: PathCountContract));
        }

        var seenSensorIds = new HashSet<string>(StringComparer.Ordinal);
        var pathIndex = 0;
        foreach (var entry in pathsElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(Error(CleaningPathCodes.PathWrongType, pathIndex: pathIndex, offending: RawScalar(entry), reason: "a path entry is not a JSON object", allowed: canonicalShape ? "path object with properties sensorId and points" : "path object with properties PathName and Coordinates"));
                pathIndex++;
                continue;
            }

            var structuralErrors = new List<CleaningPathValidationError>();
            var properties = ReadObject(entry, canonicalShape ? PathProperties : LegacyPathProperties, pathIndex, null, structuralErrors);
            errors.AddRange(structuralErrors);

            string? sensorId = null;
            PathDraft? draft = null;
            if (!properties.TryGetValue(canonicalShape ? SensorIdProperty : LegacyPathNameProperty, out var sensorIdElement))
            {
                errors.Add(Error(CleaningPathCodes.SensorIdMissing, pathIndex: pathIndex, reason: "a path entry has no Sensor identity property", allowed: canonicalShape ? "path property sensorId" : "path property PathName"));
            }
            else if (sensorIdElement.ValueKind != JsonValueKind.String)
            {
                errors.Add(Error(CleaningPathCodes.SensorIdWrongType, pathIndex: pathIndex, offending: RawScalar(sensorIdElement), reason: "the Sensor identity is not a JSON string", allowed: "the Sensor identity is a string"));
            }
            else
            {
                sensorId = sensorIdElement.GetString();
                if (string.IsNullOrWhiteSpace(sensorId) || !CanonicalSensorMap.IsProductSensorId(sensorId))
                {
                    errors.Add(Error(CleaningPathCodes.SensorIdUnknown, pathIndex: pathIndex, sensorId: sensorId, offending: sensorId, reason: "the Sensor identity is not one of the 108 canonical Product Sensors", allowed: "a canonical Product Sensor identity"));
                }
                else if (!seenSensorIds.Add(sensorId))
                {
                    errors.Add(Error(CleaningPathCodes.SensorIdDuplicate, pathIndex: pathIndex, sensorId: sensorId, offending: sensorId, reason: "the Sensor identity appears in more than one path entry", allowed: "exactly one path per canonical Product Sensor"));
                }
                else
                {
                    draft = new PathDraft(sensorId);
                }
            }

            ReadPoints(properties, canonicalShape, pathIndex, sensorId, draft, errors);

            if (draft is not null)
            {
                drafts.Add(draft);
            }

            pathIndex++;
        }

        return drafts;
    }

    private static void ReadPoints(
        IReadOnlyDictionary<string, JsonElement> pathProperties,
        bool canonicalShape,
        int pathIndex,
        string? sensorId,
        PathDraft? draft,
        List<CleaningPathValidationError> errors)
    {
        if (!pathProperties.TryGetValue(canonicalShape ? PointsProperty : LegacyCoordinatesProperty, out var pointsElement))
        {
            errors.Add(Error(CleaningPathCodes.PointsMissing, pathIndex: pathIndex, sensorId: sensorId, reason: "a path entry has no points property", allowed: canonicalShape ? "path property points (array of 6 point objects)" : "path property Coordinates (array of 6 point objects)"));
            return;
        }

        if (pointsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(Error(CleaningPathCodes.PointsWrongType, pathIndex: pathIndex, sensorId: sensorId, offending: RawScalar(pointsElement), reason: "the points property is not a JSON array", allowed: canonicalShape ? "path property points (array of 6 point objects)" : "path property Coordinates (array of 6 point objects)"));
            return;
        }

        var pointIndex = 0;
        var seenPointIds = new HashSet<string>(StringComparer.Ordinal);
        var validIdsInOrder = new List<string>();
        var sawPointIdPropertyGap = false;

        foreach (var pointElement in pointsElement.EnumerateArray())
        {
            if (pointElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(Error(CleaningPathCodes.PointWrongType, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: canonicalShape ? null : PositionalIdOrNull(pointIndex), offending: RawScalar(pointElement), reason: "a point entry is not a JSON object", allowed: "point object with the documented coordinate properties"));
                pointIndex++;
                continue;
            }

            var structuralErrors = new List<CleaningPathValidationError>();
            var pointProperties = ReadObject(pointElement, canonicalShape ? PointProperties : LegacyPointProperties, pathIndex, pointIndex, structuralErrors);
            foreach (var error in structuralErrors)
            {
                errors.Add(error with { SensorId = sensorId ?? error.SensorId });
            }

            // The point identity used in later errors: the actual identity when the file supplies a
            // string, otherwise the positional identity for Legacy shape only.
            string? pointIdentity = canonicalShape ? null : PositionalIdOrNull(pointIndex);

            if (canonicalShape)
            {
                if (!pointProperties.TryGetValue(PointIdProperty, out var pointIdElement))
                {
                    sawPointIdPropertyGap = true;
                    errors.Add(Error(CleaningPathCodes.PointIdMissing, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, reason: "a point entry has no pointId property", allowed: PointIdContract));
                }
                else if (pointIdElement.ValueKind != JsonValueKind.String)
                {
                    errors.Add(Error(CleaningPathCodes.PointIdWrongType, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, offending: RawScalar(pointIdElement), reason: "the point identity is not a JSON string", allowed: PointIdContract));
                }
                else
                {
                    var pointId = pointIdElement.GetString() ?? string.Empty;
                    pointIdentity = pointId;
                    if (!ExpectedPointIds.Contains(pointId, StringComparer.Ordinal))
                    {
                        errors.Add(Error(CleaningPathCodes.PointIdUnknown, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, offending: pointId, reason: "the point identity is not one of P1-P6", allowed: PointIdContract));
                    }
                    else if (!seenPointIds.Add(pointId))
                    {
                        errors.Add(Error(CleaningPathCodes.PointIdDuplicate, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, offending: pointId, reason: "the point identity appears more than once in this path", allowed: PointIdContract));
                    }
                    else
                    {
                        validIdsInOrder.Add(pointId);
                    }
                }
            }

            var horizontal = ReadCoordinate(pointProperties, canonicalShape ? HorizontalProperty : LegacyXProperty, CleaningPathCoordinateIdentity.HORIZONTAL, pathIndex, pointIndex, sensorId, pointIdentity, errors);
            var vertical = ReadCoordinate(pointProperties, canonicalShape ? VerticalProperty : LegacyZProperty, CleaningPathCoordinateIdentity.VERTICAL, pathIndex, pointIndex, sensorId, pointIdentity, errors);

            if (draft is not null && horizontal is not null && vertical is not null && pointIdentity is not null && ExpectedPointIds.Contains(pointIdentity, StringComparer.Ordinal))
            {
                draft.Points.Add(new CleaningPathPoint
                {
                    PointId = pointIdentity,
                    HorizontalMillimetres = horizontal.Value.Millimetres,
                    VerticalMillimetres = vertical.Value.Millimetres,
                    HorizontalNative = horizontal.Value.Native,
                    VerticalNative = vertical.Value.Native,
                });
            }

            pointIndex++;
        }

        if (pointIndex != ExpectedPointIds.Count)
        {
            errors.Add(Error(CleaningPathCodes.PointsCount, pathIndex: pathIndex, sensorId: sensorId, offending: pointIndex.ToString(CultureInfo.InvariantCulture), reason: "the path does not carry exactly six points", allowed: PointsContract));
        }

        if (!canonicalShape)
        {
            return;
        }

        foreach (var expectedId in ExpectedPointIds)
        {
            if (!seenPointIds.Contains(expectedId))
            {
                errors.Add(Error(CleaningPathCodes.PointIdMissing, pathIndex: pathIndex, sensorId: sensorId, pointId: expectedId, reason: sawPointIdPropertyGap ? "a canonical point identity is absent (a point entry also carried no pointId property)" : "a canonical point identity is absent from the path", allowed: PointIdContract));
            }
        }

        // A pure ordering defect: all six identities appear exactly once, but not in P1..P6 order.
        if (validIdsInOrder.Count == ExpectedPointIds.Count && !validIdsInOrder.SequenceEqual(ExpectedPointIds, StringComparer.Ordinal))
        {
            errors.Add(Error(CleaningPathCodes.PointOrderInvalid, pathIndex: pathIndex, sensorId: sensorId, offending: string.Join(",", validIdsInOrder), reason: "the point identities are not in canonical order", allowed: PointIdContract));
        }
    }

    private static string? PositionalIdOrNull(int pointIndex) =>
        pointIndex >= 0 && pointIndex < ExpectedPointIds.Count ? ExpectedPointIds[pointIndex] : null;

    private readonly record struct CoordinateDraft(int Millimetres, int Native);

    private static CoordinateDraft? ReadCoordinate(
        IReadOnlyDictionary<string, JsonElement> pointProperties,
        string propertyName,
        CleaningPathCoordinateIdentity identity,
        int pathIndex,
        int pointIndex,
        string? sensorId,
        string? pointId,
        List<CleaningPathValidationError> errors)
    {
        var identityLabel = identity == CleaningPathCoordinateIdentity.HORIZONTAL ? "Horizontal" : "Vertical";

        if (!pointProperties.TryGetValue(propertyName, out var element))
        {
            errors.Add(Error(CleaningPathCodes.CoordinateMissing, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, reason: $"the {identityLabel} coordinate is missing", allowed: CoordinateContract));
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number)
        {
            errors.Add(Error(CleaningPathCodes.CoordinateWrongType, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: RawScalar(element), reason: $"the {identityLabel} coordinate is not a JSON number", allowed: CoordinateContract));
            return null;
        }

        if (!element.TryGetDecimal(out var value))
        {
            errors.Add(Error(CleaningPathCodes.CoordinateValueInvalid, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: RawScalar(element), reason: $"the {identityLabel} coordinate is not readable as a decimal value", allowed: CoordinateContract));
            return null;
        }

        var offending = value.ToString(CultureInfo.InvariantCulture);
        var anyError = false;

        // Independent checks: every discoverable defect is reported for this value.
        if (value != decimal.Truncate(value))
        {
            errors.Add(Error(CleaningPathCodes.CoordinateNotIntegerMillimetres, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: offending, reason: $"the {identityLabel} coordinate is not a whole number of millimetres (no rounding is applied)", allowed: CoordinateContract));
            anyError = true;
        }

        if (value < CleaningPathUnits.MinimumMillimetres)
        {
            errors.Add(Error(CleaningPathCodes.CoordinateBelowMinimum, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: offending, reason: $"the {identityLabel} coordinate is below the accepted minimum ({CleaningPathUnits.MinimumMillimetres.ToString(CultureInfo.InvariantCulture)} mm)", allowed: CoordinateContract));
            anyError = true;
        }

        if (value > CleaningPathUnits.MaximumMillimetres)
        {
            errors.Add(Error(CleaningPathCodes.CoordinateAboveMaximum, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: offending, reason: $"the {identityLabel} coordinate is above the accepted maximum ({CleaningPathUnits.MaximumMillimetres.ToString(CultureInfo.InvariantCulture)} mm)", allowed: CoordinateContract));
            anyError = true;
        }

        if (anyError)
        {
            return null;
        }

        if (!CleaningPathUnits.TryConvertMillimetresToNative((long)value, out var native))
        {
            errors.Add(Error(CleaningPathCodes.CoordinateConversionOverflow, pathIndex: pathIndex, pointIndex: pointIndex, sensorId: sensorId, pointId: pointId, coordinate: identity, offending: offending, reason: $"the {identityLabel} coordinate does not convert to a native coordinate (checked overflow refusal)", allowed: CoordinateContract));
            return null;
        }

        return new CoordinateDraft((int)value, native);
    }

    private static void ValidateSchema(IReadOnlyDictionary<string, JsonElement> root, List<CleaningPathValidationError> errors)
    {
        if (!root.TryGetValue(SchemaProperty, out var schema))
        {
            errors.Add(Error(CleaningPathCodes.SchemaMissing, reason: "the store has no schema property", allowed: $"root property schema with value {SchemaIdentifier}"));
            return;
        }

        if (schema.ValueKind != JsonValueKind.String)
        {
            errors.Add(Error(CleaningPathCodes.SchemaWrongType, offending: RawScalar(schema), reason: "the schema property is not a JSON string", allowed: $"root property schema with value {SchemaIdentifier}"));
            return;
        }

        if (!string.Equals(schema.GetString(), SchemaIdentifier, StringComparison.Ordinal))
        {
            errors.Add(Error(CleaningPathCodes.SchemaUnsupported, offending: RawScalar(schema), reason: "the schema identifier is not supported", allowed: $"root property schema with value {SchemaIdentifier}"));
        }
    }

    private static void ValidateCompleteness(List<PathDraft> drafts, List<CleaningPathValidationError> errors)
    {
        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            if (!drafts.Exists(d => string.Equals(d.SensorId, sensorId, StringComparison.Ordinal)))
            {
                errors.Add(Error(CleaningPathCodes.CanonicalSensorMissing, sensorId: sensorId, offending: sensorId, reason: "a canonical Product Sensor has no valid path in the store", allowed: PathCountContract));
            }
        }
    }

    /// <summary>
    /// Enumerates one JSON object, refusing duplicate and unknown properties and keeping the FIRST
    /// occurrence of each name for value traversal (deterministic under duplicate names).
    /// </summary>
    private static Dictionary<string, JsonElement> ReadObject(
        JsonElement element,
        HashSet<string> allowed,
        int? pathIndex,
        int? pointIndex,
        List<CleaningPathValidationError> errors)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                errors.Add(Error(CleaningPathCodes.JsonPropertyUnknown, pathIndex: pathIndex, pointIndex: pointIndex, offending: property.Name, reason: "the object carries an unknown property", allowed: "only the documented properties of this object"));
                continue;
            }

            if (map.ContainsKey(property.Name))
            {
                errors.Add(Error(CleaningPathCodes.JsonPropertyDuplicate, pathIndex: pathIndex, pointIndex: pointIndex, offending: property.Name, reason: "the object carries a duplicate property", allowed: "each property appears at most once"));
                continue;
            }

            map[property.Name] = property.Value;
        }

        return map;
    }

    private static string? RawScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => element.GetRawText(),
        JsonValueKind.String when (element.GetString()?.Length ?? 0) <= 64 => element.GetRawText(),
        _ => null,
    };

    private static CleaningPathValidationError Error(
        string code,
        int? pathIndex = null,
        int? pointIndex = null,
        string? sensorId = null,
        string? pointId = null,
        CleaningPathCoordinateIdentity? coordinate = null,
        string? offending = null,
        string reason = "",
        string allowed = "") => new()
    {
        Code = code,
        PathIndex = pathIndex,
        PointIndex = pointIndex,
        SensorId = sensorId,
        PointId = pointId,
        Coordinate = coordinate,
        OffendingValue = offending,
        Reason = reason,
        AllowedContract = allowed,
    };
}
