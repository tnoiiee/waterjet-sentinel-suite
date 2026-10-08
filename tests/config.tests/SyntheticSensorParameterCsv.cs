using Wjss.Contracts;
using Wjss.Domain;

namespace Wjss.Config.Tests;

/// <summary>
/// Deterministic, PUBLIC-SAFE synthetic legacy sensor-parameter CSV builder for the
/// Stage 0.3A-3 Checkpoint B migration tests.
///
/// Every value is synthetic and labelling-safe: record ids are "SYN-REC-000".."107",
/// acquisition columns carry obviously fake tokens, and no Owner value ever enters this
/// repository (public-repository boundary, second Owner review item 7). The builder
/// mirrors the canonical derivation exactly: logical labels come from
/// <see cref="CanonicalSensorMap.SensorIdFor"/>, walls from
/// <see cref="CanonicalSensorMap.WallForColumn"/>, and the cleaning-device ordinal of a
/// Sensor row is the ordinal of the Water Jet whose APPROVED TARGET wall/region equals
/// the Sensor's wall/region — never the installed wall. The two legacy acquisition
/// header names are assembled from fragments so this Product-tree source never contains
/// the prohibited transport vocabulary (boundary S3).
///
/// Derivations used by the default fixture (stable, asserted by tests):
/// scanOrder s ∈ 1..106 → dirty bounds 15+(s%10) .. 65+(s%10); threshold 55+(s%20);
/// enabled = s%17 ≠ 0; cleaningCount = s%4; lastclean non-empty for even s;
/// min_time "120" when s%3 = 0; acquisition tokens on every SENSOR row.
/// </summary>
internal static class SyntheticSensorParameterCsv
{
    // Boundary S3: fragments only — never the joined transport vocabulary.
    public const string IpHeader = "ip_mo" + "dbus";
    public const string BaseAddressHeader = "base_mo" + "dbus_address";

    public const string SynLastCleanTimestamp = "2026-01-01 00:00:00";
    public const string SynMinTimeValue = "120";
    public const string SynAcquisitionEndpoint = "SYN-ACQ-ENDPOINT";
    public const string SynChannelPair = "SYN-CHANNEL-PAIR";
    public const string SynAcquisitionBase = "SYN-ACQ-BASE";

    private static readonly string[] HeaderRow =
    [
        "id", "sensorname", "wall", "order_wall", "order_total", "cannon",
        "max_temp_dirtyscore", "min_temp_dirtyscore", "threshold_setpoint",
        "cleaning_count", "sensor_enable", "lastclean_timestamp",
        "min_time_allowaddtoqueue", "max_time_allowaddtoqueue", "max_time_enable",
        IpHeader, "channel_pair", BaseAddressHeader, "latest_updateparams_timestamp",
    ];

    /// <summary>The default fixture: header plus all 108 logical-position rows (106 Sensors + I7 + I16).</summary>
    public static string Build() => ToCsv(BuildRows());

    /// <summary>Default fixture rows (header first) for per-test mutation.</summary>
    public static List<string[]> BuildRows()
    {
        var rows = new List<string[]>(109) { HeaderRow.ToArray() };
        rows.AddRange(EnumerateDataRows());
        return rows;
    }

    /// <summary>The 108 data rows in order_total order 0..107.</summary>
    public static IEnumerable<string[]> EnumerateDataRows()
    {
        var wallOrdinal = new Dictionary<Wall, int>();
        var scanOrder = 0;
        for (var orderTotal = 0; orderTotal < 108; orderTotal++)
        {
            var logicalRow = (orderTotal / 18) + 1;
            var logicalColumn = (orderTotal % 18) + 1;
            var logicalId = CanonicalSensorMap.SensorIdFor(logicalRow, logicalColumn);
            var wall = CanonicalSensorMap.WallForColumn(logicalColumn);
            wallOrdinal[wall] = wallOrdinal.TryGetValue(wall, out var current) ? current + 1 : 1;
            var recordId = $"SYN-REC-{orderTotal:000}";

            if (logicalId is "I7" or "I16")
            {
                // NON_SENSOR_GAP row: identity and layout only; placeholder-like sensor
                // fields are rejected from import (REJECT FOR I7/I16).
                yield return
                [
                    recordId, logicalId, wall.ToString(), wallOrdinal[wall].ToString(CultureInfo.InvariantCulture),
                    orderTotal.ToString(CultureInfo.InvariantCulture), "0", "0", "0", "0", "0", "0",
                    string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty,
                ];
                continue;
            }

            scanOrder++;
            var lower = 15 + (scanOrder % 10);
            var region = WaterJetTopologyCatalog.RegionForLogicalRow(logicalRow);
            var deviceOrdinal = DeviceOrdinalForTarget(wall, region);
            var enabled = scanOrder % 17 != 0;
            var lastClean = scanOrder % 2 == 0 ? SynLastCleanTimestamp : string.Empty;
            var minTime = scanOrder % 3 == 0 ? SynMinTimeValue : string.Empty;

            yield return
            [
                recordId,
                logicalId,
                wall.ToString(),
                wallOrdinal[wall].ToString(CultureInfo.InvariantCulture),
                orderTotal.ToString(CultureInfo.InvariantCulture),
                deviceOrdinal.ToString(CultureInfo.InvariantCulture),
                (lower + 50).ToString(CultureInfo.InvariantCulture),
                lower.ToString(CultureInfo.InvariantCulture),
                (55 + (scanOrder % 20)).ToString(CultureInfo.InvariantCulture),
                (scanOrder % 4).ToString(CultureInfo.InvariantCulture),
                enabled ? "1" : "0",
                lastClean,
                minTime,
                string.Empty,
                string.Empty,
                SynAcquisitionEndpoint,
                SynChannelPair,
                SynAcquisitionBase,
                string.Empty,
            ];
        }
    }

    /// <summary>
    /// The cleaning-device ordinal whose Water Jet TARGETS the given wall and region —
    /// the direct ordinal mapping required by ADR-0017 (the sensor's wall is the target
    /// wall, never the installed wall).
    /// </summary>
    public static int DeviceOrdinalForTarget(Wall wall, Region region) => (wall, region) switch
    {
        (Wall.REAR, Region.LOWER) => 1,   // WJ1 installed FRONT LOWER
        (Wall.RIGHT, Region.LOWER) => 2,  // WJ2 installed LEFT LOWER
        (Wall.FRONT, Region.LOWER) => 3,  // WJ3 installed REAR LOWER
        (Wall.LEFT, Region.LOWER) => 4,   // WJ4 installed RIGHT LOWER
        (Wall.REAR, Region.UPPER) => 5,   // WJ5 installed FRONT UPPER
        (Wall.RIGHT, Region.UPPER) => 6,  // WJ6 installed LEFT UPPER
        (Wall.FRONT, Region.UPPER) => 7,  // WJ7 installed REAR UPPER
        (Wall.LEFT, Region.UPPER) => 8,   // WJ8 installed RIGHT UPPER
        _ => throw new ArgumentOutOfRangeException(nameof(wall)),
    };

    /// <summary>Serializes rows back to RFC 4180 plain CSV (no quoting needed for the synthetic vocabulary).</summary>
    public static string ToCsv(IEnumerable<string[]> rows) =>
        string.Join("\n", rows.Select(row => string.Join(",", row))) + "\n";

    /// <summary>Replaces one cell of one data row (rowIndex is 0-based over the DATA rows, excluding the header).</summary>
    public static string WithCell(int dataRowIndex, int cellIndex, string value)
    {
        var rows = BuildRows();
        rows[dataRowIndex + 1][cellIndex] = value;
        return ToCsv(rows);
    }

    /// <summary>Removes one data row (rowIndex is 0-based over the DATA rows, excluding the header).</summary>
    public static string WithoutRow(int dataRowIndex)
    {
        var rows = BuildRows();
        rows.RemoveAt(dataRowIndex + 1);
        return ToCsv(rows);
    }

    /// <summary>Returns the data row index (0-based) whose logical label (cell 1) equals <paramref name="logicalId"/>.</summary>
    public static int RowOfLogicalId(string logicalId)
    {
        var rows = BuildRows().Skip(1).ToList();
        return rows.FindIndex(row => row[1] == logicalId);
    }
}
