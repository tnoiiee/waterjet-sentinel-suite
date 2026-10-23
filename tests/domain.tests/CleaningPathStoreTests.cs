using System.Globalization;
using System.Text;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Domain.Tests;

/// <summary>
/// Phase 3A Cleaning Path store contract tests: the Owner-approved coordinate contract
/// (integer millimetres only, 0–280 inclusive, exact checked ×10000 conversion, no rounding, no
/// clamping, no defaults), complete-file validation with aggregated structured errors, the
/// 108-Sensor completeness rule, and the Legacy migration/validation inspection entry. Every
/// value below is synthetic and public-safe: no Owner-local path, no Actual-Run coordinate and no
/// Hardware value appears in this file.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathStoreTests
{
    private const string CanonicalSchema = "wjss.cleaning-path-store/1";

    // ---------------------------------------------------------------- builders (synthetic only)

    private static string DefaultPointJson(string pointId, int ordinal) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"pointId\":\"{pointId}\",\"horizontalMillimetres\":{10 + ordinal},\"verticalMillimetres\":{20 + ordinal}}}");

    private static string PointsArrayJson(string sensorId, Func<string, string, string?>? pointOverride)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        for (var ordinal = 1; ordinal <= 6; ordinal++)
        {
            if (ordinal > 1)
            {
                builder.Append(',');
            }

            var pointId = "P" + ordinal.ToString(CultureInfo.InvariantCulture);
            builder.Append(pointOverride?.Invoke(sensorId, pointId) ?? DefaultPointJson(pointId, ordinal));
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string DefaultPathJson(string sensorId, Func<string, string, string?>? pointOverride) =>
        "{\"sensorId\":\"" + sensorId + "\",\"points\":" + PointsArrayJson(sensorId, pointOverride) + "}";

    /// <summary>Builds the complete canonical store. A pathOverride returning an empty string SKIPS that Sensor's path.</summary>
    private static string BuildStore(
        Func<string, string, string?>? pointOverride = null,
        Func<string, string?>? pathOverride = null,
        string? schemaPropertyJson = null)
    {
        var builder = new StringBuilder();
        builder.Append('{').Append(schemaPropertyJson ?? $"\"schema\":\"{CanonicalSchema}\",").Append("\"paths\":[");
        var first = true;
        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            var overrideJson = pathOverride?.Invoke(sensorId);
            if (overrideJson is not null && overrideJson.Length == 0)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(overrideJson ?? DefaultPathJson(sensorId, pointOverride));
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string PointJson(string pointId, int ordinal, string? horizontalRaw, string? verticalRaw) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"pointId\":\"{pointId}\",\"horizontalMillimetres\":{horizontalRaw ?? (10 + ordinal).ToString(CultureInfo.InvariantCulture)},\"verticalMillimetres\":{verticalRaw ?? (20 + ordinal).ToString(CultureInfo.InvariantCulture)}}}");

    private static string DefaultLegacyCoordinateJson(int ordinal) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"X\":{10 + ordinal},\"Z\":{20 + ordinal}}}");

    private static string BuildLegacyStore(Func<string, int, string?>? coordinateOverride = null)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        var first = true;
        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append("{\"PathName\":\"").Append(sensorId).Append("\",\"Coordinates\":[");
            for (var ordinal = 1; ordinal <= 6; ordinal++)
            {
                if (ordinal > 1)
                {
                    builder.Append(',');
                }

                builder.Append(coordinateOverride?.Invoke(sensorId, ordinal) ?? DefaultLegacyCoordinateJson(ordinal));
            }

            builder.Append("]}");
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static CleaningPathValidationError Single(CleaningPathStoreResult result, string code)
    {
        var matches = result.Errors.Where(e => string.Equals(e.Code, code, StringComparison.Ordinal)).ToList();
        Assert.True(matches.Count >= 1, $"expected at least one error with code {code}; found {matches.Count} of {result.Errors.Count} errors");
        return matches[0];
    }

    // ---------------------------------------------------------------- coordinate contract

    [Fact]
    public void Whole_Number_Values_Are_Accepted_At_Zero_Centre_And_Maximum_With_Exact_Conversion()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
        {
            if (!string.Equals(sensor, "G1", StringComparison.Ordinal))
            {
                return null;
            }

            return point switch
            {
                "P1" => PointJson("P1", 1, "0", "0"),
                "P2" => PointJson("P2", 2, "125", "125"),
                "P3" => PointJson("P3", 3, "280", "280"),
                _ => null,
            };
        });

        var result = CleaningPathStoreParser.Parse(json);

        Assert.True(result.Accepted);
        Assert.NotNull(result.Snapshot);
        var path = result.Snapshot!.PathFor("G1");
        Assert.NotNull(path);

        Assert.Equal(0, path!.Points[0].HorizontalMillimetres);
        Assert.Equal(0, path.Points[0].VerticalMillimetres);
        Assert.Equal(0, path.Points[0].HorizontalNative);
        Assert.Equal(0, path.Points[0].VerticalNative);

        Assert.Equal(125, path.Points[1].HorizontalMillimetres);
        Assert.Equal(125, path.Points[1].VerticalMillimetres);
        Assert.Equal(1250000, path.Points[1].HorizontalNative);
        Assert.Equal(1250000, path.Points[1].VerticalNative);

        Assert.Equal(280, path.Points[2].HorizontalMillimetres);
        Assert.Equal(280, path.Points[2].VerticalMillimetres);
        Assert.Equal(2800000, path.Points[2].HorizontalNative);
        Assert.Equal(2800000, path.Points[2].VerticalNative);
    }

    [Fact]
    public void Mathematically_Integral_Decimal_Representations_Are_Accepted()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
        {
            if (!string.Equals(sensor, "G1", StringComparison.Ordinal))
            {
                return null;
            }

            return point switch
            {
                "P1" => PointJson("P1", 1, "125.0", "125"),
                "P2" => PointJson("P2", 2, "125.00", "125"),
                _ => null,
            };
        });

        var result = CleaningPathStoreParser.Parse(json);

        Assert.True(result.Accepted);
        var path = result.Snapshot!.PathFor("G1");
        Assert.Equal(125, path!.Points[0].HorizontalMillimetres);
        Assert.Equal(1250000, path.Points[0].HorizontalNative);
        Assert.Equal(125, path.Points[1].HorizontalMillimetres);
        Assert.Equal(1250000, path.Points[1].HorizontalNative);
    }

    [Fact]
    public void Fractional_Value_Is_Refused_Without_Rounding()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "H7", StringComparison.Ordinal) && string.Equals(point, "P5", StringComparison.Ordinal)
                ? PointJson("P5", 5, "125.5", "20")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        var error = Single(result, CleaningPathCodes.CoordinateNotIntegerMillimetres);
        Assert.Equal("H7", error.SensorId);
        Assert.Equal("P5", error.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.HORIZONTAL, error.Coordinate);
        Assert.Equal("125.5", error.OffendingValue);
    }

    [Fact]
    public void Negative_Value_Is_Refused_Without_Clamping()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G12", StringComparison.Ordinal) && string.Equals(point, "P3", StringComparison.Ordinal)
                ? PointJson("P3", 3, "10", "-1")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        var error = Single(result, CleaningPathCodes.CoordinateBelowMinimum);
        Assert.Equal("G12", error.SensorId);
        Assert.Equal("P3", error.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.VERTICAL, error.Coordinate);
        Assert.Equal("-1", error.OffendingValue);
    }

    [Fact]
    public void Value_Above_Maximum_Is_Refused_Without_Clamping()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G12", StringComparison.Ordinal) && string.Equals(point, "P3", StringComparison.Ordinal)
                ? PointJson("P3", 3, "10", "281")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        var error = Single(result, CleaningPathCodes.CoordinateAboveMaximum);
        Assert.Equal("G12", error.SensorId);
        Assert.Equal("P3", error.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.VERTICAL, error.Coordinate);
        Assert.Equal("281", error.OffendingValue);
    }

    [Fact]
    public void Value_320_Is_Refused_As_Above_Maximum_Never_Corrected()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G12", StringComparison.Ordinal) && string.Equals(point, "P3", StringComparison.Ordinal)
                ? PointJson("P3", 3, "10", "320")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        var error = Single(result, CleaningPathCodes.CoordinateAboveMaximum);
        Assert.Equal("G12", error.SensorId);
        Assert.Equal("P3", error.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.VERTICAL, error.Coordinate);
        Assert.Equal("320", error.OffendingValue);
        Assert.DoesNotContain(result.Errors, e => e.OffendingValue == "280"); // never silently corrected to 280 mm
    }

    [Fact]
    public void Value_280_0001_Is_Refused()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "H7", StringComparison.Ordinal) && string.Equals(point, "P5", StringComparison.Ordinal)
                ? PointJson("P5", 5, "280.0001", "20")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Errors, e => e.Code == CleaningPathCodes.CoordinateNotIntegerMillimetres);
        Assert.Contains(result.Errors, e => e.Code == CleaningPathCodes.CoordinateAboveMaximum);
    }

    [Fact]
    public void One_Invalid_Point_Refuses_The_Entire_File()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "I16", StringComparison.Ordinal) && string.Equals(point, "P2", StringComparison.Ordinal)
                ? PointJson("P2", 2, "10", "999")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Errors, e => e.Code == CleaningPathCodes.CoordinateAboveMaximum);
    }

    [Fact]
    public void Valid_Paths_Remain_Unavailable_When_Another_Path_Is_Invalid()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "J18", StringComparison.Ordinal) && string.Equals(point, "P6", StringComparison.Ordinal)
                ? PointJson("P6", 6, "10", "125.5")
                : null);

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Null(result.Snapshot?.PathFor("G1"));
    }

    [Fact]
    public void Multiple_Invalid_Points_Produce_All_Discoverable_Structured_Errors()
    {
        var json = BuildStore(pointOverride: (sensor, point) =>
        {
            if (string.Equals(sensor, "G12", StringComparison.Ordinal) && string.Equals(point, "P3", StringComparison.Ordinal))
            {
                return PointJson("P3", 3, "10", "320");
            }

            if (string.Equals(sensor, "H7", StringComparison.Ordinal) && string.Equals(point, "P5", StringComparison.Ordinal))
            {
                return PointJson("P5", 5, "125.5", "20");
            }

            return string.Equals(sensor, "J1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? PointJson("P1", 1, "-4", "20")
                : null;
        });

        var result = CleaningPathStoreParser.Parse(json);

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Equal(3, result.Errors.Count);

        var above = Single(result, CleaningPathCodes.CoordinateAboveMaximum);
        Assert.Equal("G12", above.SensorId);
        Assert.Equal("P3", above.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.VERTICAL, above.Coordinate);
        Assert.Equal("320", above.OffendingValue);
        Assert.False(string.IsNullOrEmpty(above.Reason));
        Assert.False(string.IsNullOrEmpty(above.AllowedContract));

        var fractional = Single(result, CleaningPathCodes.CoordinateNotIntegerMillimetres);
        Assert.Equal("H7", fractional.SensorId);
        Assert.Equal("P5", fractional.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.HORIZONTAL, fractional.Coordinate);
        Assert.Equal("125.5", fractional.OffendingValue);

        var below = Single(result, CleaningPathCodes.CoordinateBelowMinimum);
        Assert.Equal("J1", below.SensorId);
        Assert.Equal("P1", below.PointId);
        Assert.Equal(CleaningPathCoordinateIdentity.HORIZONTAL, below.Coordinate);
        Assert.Equal("-4", below.OffendingValue);
    }

    [Fact]
    public void Conversion_Is_Exact_And_Overflow_Is_Refused()
    {
        Assert.True(CleaningPathUnits.TryConvertMillimetresToNative(0, out var zero));
        Assert.Equal(0, zero);
        Assert.True(CleaningPathUnits.TryConvertMillimetresToNative(125, out var centre));
        Assert.Equal(1250000, centre);
        Assert.True(CleaningPathUnits.TryConvertMillimetresToNative(280, out var maximum));
        Assert.Equal(2800000, maximum);

        // Checked-overflow refusals: the product must be representable as a native coordinate.
        Assert.False(CleaningPathUnits.TryConvertMillimetresToNative(214749, out _));
        Assert.False(CleaningPathUnits.TryConvertMillimetresToNative(300000000, out _));
        Assert.False(CleaningPathUnits.TryConvertMillimetresToNative(long.MaxValue, out _));
    }

    // ---------------------------------------------------------------- complete-file validation

    [Fact]
    public void Malformed_JSON_Stops_Traversal_With_One_Structured_Error()
    {
        var result = CleaningPathStoreParser.Parse("{ \"schema\": ");

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Equal(CleaningPathCodes.JsonMalformed, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Comments_And_Trailing_Commas_Are_Refused()
    {
        var commented = CleaningPathStoreParser.Parse("{\"schema\": /* c */ \"x\"}");
        Assert.False(commented.Accepted);
        Assert.Equal(CleaningPathCodes.JsonMalformed, Assert.Single(commented.Errors).Code);

        var trailing = CleaningPathStoreParser.Parse("{\"schema\": \"x\",}");
        Assert.False(trailing.Accepted);
        Assert.Equal(CleaningPathCodes.JsonMalformed, Assert.Single(trailing.Errors).Code);
    }

    [Fact]
    public void Duplicate_And_Unknown_Properties_Are_Refused()
    {
        var duplicateRoot = CleaningPathStoreParser.Parse($"{{\"schema\":\"{CanonicalSchema}\",\"schema\":\"{CanonicalSchema}\",\"paths\":[]}}");
        Assert.False(duplicateRoot.Accepted);
        Assert.Contains(duplicateRoot.Errors, e => e.Code == CleaningPathCodes.JsonPropertyDuplicate);

        var duplicatePoint = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"pointId\":\"P1\",\"horizontalMillimetres\":10,\"horizontalMillimetres\":11,\"verticalMillimetres\":20}"
                : null));
        Assert.False(duplicatePoint.Accepted);
        Assert.Contains(duplicatePoint.Errors, e => e.Code == CleaningPathCodes.JsonPropertyDuplicate);

        var unknownRoot = CleaningPathStoreParser.Parse($"{{\"schema\":\"{CanonicalSchema}\",\"paths\":[],\"extra\":1}}");
        Assert.False(unknownRoot.Accepted);
        Assert.Contains(unknownRoot.Errors, e => e.Code == CleaningPathCodes.JsonPropertyUnknown);

        var unknownPoint = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"pointId\":\"P1\",\"horizontalMillimetres\":10,\"verticalMillimetres\":20,\"axis\":\"A\"}"
                : null));
        Assert.False(unknownPoint.Accepted);
        Assert.Contains(unknownPoint.Errors, e => e.Code == CleaningPathCodes.JsonPropertyUnknown && e.OffendingValue == "axis");
    }

    [Fact]
    public void Wrong_Types_Are_Refused()
    {
        var pathsAsString = CleaningPathStoreParser.Parse($"{{\"schema\":\"{CanonicalSchema}\",\"paths\":\"nope\"}}");
        Assert.False(pathsAsString.Accepted);
        Assert.Contains(pathsAsString.Errors, e => e.Code == CleaningPathCodes.PathsWrongType);

        var sensorIdAsNumber = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal)
                ? "{\"sensorId\":7,\"points\":" + PointsArrayJson(sensor, null) + "}"
                : null));
        Assert.False(sensorIdAsNumber.Accepted);
        Assert.Contains(sensorIdAsNumber.Errors, e => e.Code == CleaningPathCodes.SensorIdWrongType);

        var coordinateAsString = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"pointId\":\"P1\",\"horizontalMillimetres\":\"10\",\"verticalMillimetres\":20}"
                : null));
        Assert.False(coordinateAsString.Accepted);
        Assert.Contains(coordinateAsString.Errors, e => e.Code == CleaningPathCodes.CoordinateWrongType);

        var pointAsString = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) ? "{\"sensorId\":\"G1\",\"points\":[\"P1\"]}" : null));
        Assert.False(pointAsString.Accepted);
        Assert.Contains(pointAsString.Errors, e => e.Code == CleaningPathCodes.PointWrongType);
    }

    [Fact]
    public void Missing_Required_Properties_Are_Refused()
    {
        var noSchema = CleaningPathStoreParser.Parse("{\"paths\":[]}");
        Assert.Contains(noSchema.Errors, e => e.Code == CleaningPathCodes.SchemaMissing);

        var noPaths = CleaningPathStoreParser.Parse($"{{\"schema\":\"{CanonicalSchema}\"}}");
        Assert.Contains(noPaths.Errors, e => e.Code == CleaningPathCodes.PathsMissing);

        var noSensorId = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal)
                ? "{\"points\":" + PointsArrayJson(sensor, null) + "}"
                : null));
        Assert.Contains(noSensorId.Errors, e => e.Code == CleaningPathCodes.SensorIdMissing);

        var noPoints = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) ? "{\"sensorId\":\"G1\"}" : null));
        Assert.Contains(noPoints.Errors, e => e.Code == CleaningPathCodes.PointsMissing);

        var noPointId = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"horizontalMillimetres\":10,\"verticalMillimetres\":20}"
                : null));
        Assert.Contains(noPointId.Errors, e => e.Code == CleaningPathCodes.PointIdMissing);

        var noHorizontal = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"pointId\":\"P1\",\"verticalMillimetres\":20}"
                : null));
        Assert.Contains(noHorizontal.Errors, e => e.Code == CleaningPathCodes.CoordinateMissing && e.Coordinate == CleaningPathCoordinateIdentity.HORIZONTAL);

        var noVertical = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P1", StringComparison.Ordinal)
                ? "{\"pointId\":\"P1\",\"horizontalMillimetres\":10}"
                : null));
        Assert.Contains(noVertical.Errors, e => e.Code == CleaningPathCodes.CoordinateMissing && e.Coordinate == CleaningPathCoordinateIdentity.VERTICAL);
    }

    [Fact]
    public void Schema_Identity_Is_Validated()
    {
        var wrongType = CleaningPathStoreParser.Parse("{\"schema\":5,\"paths\":[]}");
        Assert.Contains(wrongType.Errors, e => e.Code == CleaningPathCodes.SchemaWrongType);

        var unsupported = CleaningPathStoreParser.Parse("{\"schema\":\"wjss.cleaning-path-store/2\",\"paths\":[]}");
        Assert.Contains(unsupported.Errors, e => e.Code == CleaningPathCodes.SchemaUnsupported);
    }

    [Fact]
    public void Total_Path_Count_Duplicate_Unknown_And_Missing_Canonical_Sensors_Are_Refused()
    {
        var missingOne = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "J18", StringComparison.Ordinal) ? string.Empty : null));
        Assert.False(missingOne.Accepted);
        Assert.Contains(missingOne.Errors, e => e.Code == CleaningPathCodes.PathCount);
        Assert.Contains(missingOne.Errors, e => e.Code == CleaningPathCodes.CanonicalSensorMissing && e.SensorId == "J18");

        var duplicated = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "J18", StringComparison.Ordinal)
                ? DefaultPathJson("J1", null) + "," + DefaultPathJson("J18", null)
                : null));
        Assert.False(duplicated.Accepted);
        Assert.Contains(duplicated.Errors, e => e.Code == CleaningPathCodes.SensorIdDuplicate && e.SensorId == "J1");

        var unknown = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "J18", StringComparison.Ordinal) ? DefaultPathJson("G999", null) : null));
        Assert.False(unknown.Accepted);
        Assert.Contains(unknown.Errors, e => e.Code == CleaningPathCodes.SensorIdUnknown);
    }

    [Fact]
    public void Point_Identity_Count_And_Ordering_Are_Validated()
    {
        var duplicateId = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal)
                ? "{\"sensorId\":\"G1\",\"points\":[" + string.Join(",", Enumerable.Range(1, 6).Select(k => DefaultPointJson(k == 6 ? "P1" : "P" + k.ToString(CultureInfo.InvariantCulture), k == 6 ? 1 : k))) + "]}"
                : null));
        Assert.False(duplicateId.Accepted);
        Assert.Contains(duplicateId.Errors, e => e.Code == CleaningPathCodes.PointIdDuplicate);
        Assert.Contains(duplicateId.Errors, e => e.Code == CleaningPathCodes.PointIdMissing && e.PointId == "P6");

        var missingId = CleaningPathStoreParser.Parse(BuildStore(pointOverride: (sensor, point) =>
            string.Equals(sensor, "G1", StringComparison.Ordinal) && string.Equals(point, "P4", StringComparison.Ordinal)
                ? "{\"horizontalMillimetres\":10,\"verticalMillimetres\":20}"
                : null));
        Assert.False(missingId.Accepted);
        Assert.Contains(missingId.Errors, e => e.Code == CleaningPathCodes.PointIdMissing);

        var wrongOrder = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal)
                ? "{\"sensorId\":\"G1\",\"points\":[" + string.Join(",", new[] { 2, 1, 3, 4, 5, 6 }.Select(k => DefaultPointJson("P" + k.ToString(CultureInfo.InvariantCulture), k))) + "]}"
                : null));
        Assert.False(wrongOrder.Accepted);
        Assert.Contains(wrongOrder.Errors, e => e.Code == CleaningPathCodes.PointOrderInvalid);

        var shortPath = CleaningPathStoreParser.Parse(BuildStore(pathOverride: sensor =>
            string.Equals(sensor, "G1", StringComparison.Ordinal)
                ? "{\"sensorId\":\"G1\",\"points\":[" + string.Join(",", Enumerable.Range(1, 5).Select(k => DefaultPointJson("P" + k.ToString(CultureInfo.InvariantCulture), k))) + "]}"
                : null));
        Assert.False(shortPath.Accepted);
        Assert.Contains(shortPath.Errors, e => e.Code == CleaningPathCodes.PointsCount);
        Assert.Contains(shortPath.Errors, e => e.Code == CleaningPathCodes.PointIdMissing && e.PointId == "P6");
    }

    // ---------------------------------------------------------------- snapshot / provenance / centre

    [Fact]
    public void Accepted_Store_Produces_One_Immutable_Snapshot_With_Centre_Provenance_And_Timestamp()
    {
        var loadedAt = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var result = CleaningPathStoreParser.Parse(BuildStore(), loadedAt);

        Assert.True(result.Accepted);
        Assert.Empty(result.Errors);
        var snapshot = result.Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(CanonicalSchema, snapshot!.Schema);
        Assert.Equal(CleaningPathSourceKind.OwnerLocalFile, snapshot.SourceKind);
        Assert.Equal(loadedAt, snapshot.LoadedAtUtc);
        Assert.Equal(CanonicalSensorMap.SensorLocations, snapshot.Paths.Count);
        Assert.Equal(CanonicalSensorMap.SensorLocations, snapshot.PathsBySensorId.Count);
        Assert.Equal(125, snapshot.Centre.HorizontalMillimetres);
        Assert.Equal(125, snapshot.Centre.VerticalMillimetres);
        Assert.Equal(1250000, snapshot.Centre.HorizontalNative);
        Assert.Equal(1250000, snapshot.Centre.VerticalNative);

        // The snapshot map is read-only and every path is Sensor-owned, six ordered points each.
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, SensorCleaningPath>)snapshot.PathsBySensorId).Clear());
        foreach (var path in snapshot.Paths)
        {
            Assert.Equal(6, path.Points.Count);
            for (var index = 0; index < 6; index++)
            {
                Assert.Equal("P" + (index + 1).ToString(CultureInfo.InvariantCulture), path.Points[index].PointId);
            }
        }
    }

    // ---------------------------------------------------------------- Legacy migration inspection

    [Fact]
    public void Legacy_Shape_Valid_Store_Accepts_With_Inspection_Provenance_And_Positional_Points()
    {
        var result = CleaningPathStoreParser.ParseLegacyActualRunShape(BuildLegacyStore());

        Assert.True(result.Accepted);
        var snapshot = result.Snapshot!;
        Assert.Equal(CleaningPathSourceKind.LegacyMigrationInspection, snapshot.SourceKind);
        var path = snapshot.PathFor("G1");
        Assert.Equal("P1", path!.Points[0].PointId);
        Assert.Equal("P6", path.Points[5].PointId);
        Assert.Equal(11, path.Points[0].HorizontalMillimetres); // Legacy X → Horizontal
        Assert.Equal(21, path.Points[0].VerticalMillimetres);   // Legacy Z → Vertical
    }

    [Fact]
    public void Legacy_Shape_Reports_Every_Out_Of_Range_Coordinate_Precisely_And_Never_Claims_Success()
    {
        var result = CleaningPathStoreParser.ParseLegacyActualRunShape(BuildLegacyStore((sensor, ordinal) =>
        {
            if (string.Equals(sensor, "G12", StringComparison.Ordinal) && ordinal == 3)
            {
                return "{\"X\":10,\"Z\":320}";
            }

            return string.Equals(sensor, "H7", StringComparison.Ordinal) && ordinal == 5
                ? "{\"X\":320,\"Z\":20}"
                : null;
        }));

        Assert.False(result.Accepted);
        Assert.Null(result.Snapshot);
        Assert.Equal(2, result.Errors.Count);

        Assert.Contains(result.Errors, e =>
            e.Code == CleaningPathCodes.CoordinateAboveMaximum
            && e.SensorId == "G12"
            && e.PointId == "P3"
            && e.Coordinate == CleaningPathCoordinateIdentity.VERTICAL
            && e.OffendingValue == "320");

        Assert.Contains(result.Errors, e =>
            e.Code == CleaningPathCodes.CoordinateAboveMaximum
            && e.SensorId == "H7"
            && e.PointId == "P5"
            && e.Coordinate == CleaningPathCoordinateIdentity.HORIZONTAL
            && e.OffendingValue == "320");
    }
}
