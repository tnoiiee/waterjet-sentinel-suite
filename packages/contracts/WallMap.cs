namespace Wjss.Contracts;

/// <summary>
/// One logical position of the Owner-confirmed 18-column x 6-row matrix
/// (108 positions: 106 Sensor locations + 2 Cannon equipment slots).
/// Delivered once in the Snapshot as <c>wallMap</c>; static; NEVER in a Delta.
/// The UI renders wall grids from these slots only and generates no IDs.
/// </summary>
public sealed record WallMapSlot
{
    /// <summary>Stable slot identity, e.g. "SLOT-R5-C07". Derived from row/column, never invented per deployment.</summary>
    public required string SlotId { get; init; }

    public required SlotType SlotType { get; init; }
    public required Wall Wall { get; init; }

    /// <summary>1-18 across the whole matrix.</summary>
    public required int LogicalColumn { get; init; }

    /// <summary>1-6, top to bottom (G+2xx, G+1xx, G, H, I, J).</summary>
    public required int LogicalRow { get; init; }

    /// <summary>1-based column inside the wall (LEFT/RIGHT 1-4, REAR/FRONT 1-5).</summary>
    public required int WallColumn { get; init; }

    /// <summary>1-6 inside the wall; equals LogicalRow (no rotation, no reversal).</summary>
    public required int WallRow { get; init; }

    /// <summary>Sensor identity; null for Cannon slots. A Cannon ID is never a Sensor ID.</summary>
    public string? SensorId { get; init; }

    /// <summary>Equipment identity (Cannon); null for Sensor slots.</summary>
    public string? EquipmentId { get; init; }
}

/// <summary>
/// Canonical logical matrix structure (Owner-confirmed). The count identities and
/// Cannon slot positions are protected domain facts. Scan order, device
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
    public const int CannonSlotCount = 2;

    /// <summary>Wall totals: sensors per wall (Cannon slots are equipment, not Sensors).</summary>
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

    /// <summary>Cannon equipment slots (logical I7 = Rear, logical I16 = Front). Not Sensors.</summary>
    public static readonly (string EquipmentId, int Row, int Column)[] CannonSlots =
    [
        ("CANNON_REAR", 5, 7),
        ("CANNON_FRONT", 5, 16),
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
