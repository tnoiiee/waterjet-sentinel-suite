namespace Wjss.Contracts;

/// <summary>
/// One logical position of the Owner-confirmed 18-column x 6-row matrix
/// (108 positions: 106 Sensor locations + 2 NON_SENSOR_GAP placement anchors).
/// Delivered once in the Snapshot as <c>wallMap</c>; static; NEVER in a Delta.
/// The UI renders wall grids from these slots only and generates no IDs.
/// </summary>
public sealed record WallMapSlot
{
    /// <summary>Stable slot identity, e.g. "SLOT-R5-C07". Derived from row/column, never invented per deployment.</summary>
    public required string SlotId { get; init; }

    /// <summary>SENSOR for the 106 actual Sensors; NON_SENSOR_GAP for I7 and I16 (no Sensor identity, no channels, no scan order).</summary>
    public required LogicalPositionKind PositionKind { get; init; }
    public required Wall Wall { get; init; }

    /// <summary>1-18 across the whole matrix.</summary>
    public required int LogicalColumn { get; init; }

    /// <summary>1-6, top to bottom (G+2xx, G+1xx, G, H, I, J).</summary>
    public required int LogicalRow { get; init; }

    /// <summary>1-based column inside the wall (LEFT/RIGHT 1-4, REAR/FRONT 1-5).</summary>
    public required int WallColumn { get; init; }

    /// <summary>1-6 inside the wall; equals LogicalRow (no rotation, no reversal).</summary>
    public required int WallRow { get; init; }

    /// <summary>Sensor identity; null for NON_SENSOR_GAP positions. An equipment identity is never a Sensor ID.</summary>
    public string? SensorId { get; init; }

    /// <summary>
    /// The Water Jet physically anchored at this position; non-null only for NON_SENSOR_GAP
    /// positions (I7 anchors WJ3, I16 anchors WJ1). The gap is a location anchor only — it
    /// is never a Water Jet identity and never a Sensor identity.
    /// </summary>
    public string? GapAnchorForWaterJetId { get; init; }
}

/// <summary>
/// Canonical logical matrix structure (Owner-confirmed). The count identities and
/// NON_SENSOR_GAP positions are protected domain facts. Scan order, device
/// distribution, channel identifiers and equipment assignment are NOT here:
/// they belong to Published Configuration (later stage) and are never invented.
/// </summary>
public static class CanonicalSensorMap
{
    public const int LogicalColumnCount = 18;
    public const int LogicalRowCount = 6;
    public const int SensorLocations = 106;
    public const int ThermocoupleChannelCount = 212;
    public const int MatrixSlots = 108;
    public const int NonSensorGapCount = 2;

    /// <summary>Wall totals: sensors per wall (NON_SENSOR_GAP positions are not Sensors).</summary>
    public static readonly IReadOnlyDictionary<Wall, int> SensorsPerWall =
        new Dictionary<Wall, int> { [Wall.LEFT] = 24, [Wall.REAR] = 29, [Wall.RIGHT] = 24, [Wall.FRONT] = 29 };

    /// <summary>Wall column ranges in logical-column order.</summary>
    public static readonly IReadOnlyDictionary<Wall, (int FirstColumn, int LastColumn)> WallColumns =
        new Dictionary<Wall, (int, int)>
        {
            [Wall.LEFT] = (1, 4),
            [Wall.REAR] = (5, 9),
            [Wall.RIGHT] = (10, 13),
            [Wall.FRONT] = (14, 18),
        };

    /// <summary>
    /// The two NON_SENSOR_GAP positions and the Water Jets they physically anchor
    /// (logical I7 = Rear anchoring WJ3, logical I16 = Front anchoring WJ1). They are
    /// location anchors only — never Sensors, never equipment identities.
    /// </summary>
    public static readonly (string LogicalId, string AnchorWaterJetId, int Row, int Column)[] NonSensorGapSlots =
    [
        ("I7", "WJ3", 5, 7),
        ("I16", "WJ1", 5, 16),
    ];

    /// <summary>Sensor ID for a logical row key and column, per the Owner-confirmed labelling.</summary>
    public static string SensorIdFor(int logicalRow, int logicalColumn) => logicalRow switch
    {
        1 => $"G+{200 + logicalColumn}",
        2 => $"G+{100 + logicalColumn}",
        3 => $"G{logicalColumn}",
        4 => $"H{logicalColumn}",
        5 => $"I{logicalColumn}",
        6 => $"J{logicalColumn}",
        _ => throw new ArgumentOutOfRangeException(nameof(logicalRow), "logical row must be 1-6"),
    };

    public static Wall WallForColumn(int logicalColumn)
    {
        foreach (var (wall, (first, last)) in WallColumns)
        {
            if (logicalColumn >= first && logicalColumn <= last)
            {
                return wall;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(logicalColumn), "logical column must be 1-18");
    }
}
