namespace Wjss.Contracts;

/// <summary>
/// One logical position of the Owner-confirmed 18-column x 6-row matrix
/// (108 positions = 108 Product Sensor locations; Owner critical topology correction,
/// 2026-10-12: I7 and I16 are normal Sensors, no NON_SENSOR_GAP position exists).
/// Delivered once in the Snapshot as <c>wallMap</c>; static; NEVER in a Delta.
/// The UI renders wall grids from these slots only and generates no IDs.
/// </summary>
public sealed record WallMapSlot
{
    /// <summary>Stable slot identity, e.g. "SLOT-R5-C07". Derived from row/column, never invented per deployment.</summary>
    public required string SlotId { get; init; }

    /// <summary>Always SENSOR: every logical position is a Product Sensor (I7 and I16 included).</summary>
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

    /// <summary>The canonical Sensor identity of this position (never null for a valid slot). An equipment identity is never a Sensor ID.</summary>
    public string? SensorId { get; init; }
}

/// <summary>
/// Canonical logical matrix structure (Owner-confirmed; Owner critical topology correction
/// 2026-10-12). The count identities are protected domain facts: 108 Product Sensors
/// (108 × 2 = 216 assigned Product Thermocouple measurements) out of 240 physical
/// Thermocouple channels (6 nodes × 5 modules × 8 channels), leaving 24 unassigned
/// (240 − 216 = 24). Scan order, device distribution, channel identifiers and equipment
/// assignment are NOT here: they belong to Published Configuration (later stage) and
/// are never invented. The superseded 106 / 212 / "I7 and I16 are NON_SENSOR_GAP" belief
/// is revoked and must not be reintroduced.
/// </summary>
public static class CanonicalSensorMap
{
    public const int LogicalColumnCount = 18;
    public const int LogicalRowCount = 6;

    /// <summary>108 Product Sensors — one per logical position; I7 and I16 included.</summary>
    public const int SensorLocations = 108;

    /// <summary>Two measurement sides (Front, Rear) per Sensor.</summary>
    public const int ChannelsPerSensor = 2;

    /// <summary>216 assigned Product Thermocouple measurements (108 × 2).</summary>
    public const int ThermocoupleChannelCount = SensorLocations * ChannelsPerSensor;

    /// <summary>108 logical slots; equals <see cref="SensorLocations"/> because no non-Sensor position exists.</summary>
    public const int MatrixSlots = LogicalColumnCount * LogicalRowCount;

    /// <summary>240 physical Thermocouple channels of the six-node baseline (6 × 5 × 8).</summary>
    public const int PhysicalThermocoupleChannelCount = 240;

    /// <summary>24 physical Thermocouple channels with no Product assignment (240 − 216).</summary>
    public const int UnassignedThermocoupleChannelCount = PhysicalThermocoupleChannelCount - ThermocoupleChannelCount;

    /// <summary>Wall totals: Sensors per wall (every position is a Sensor): 24 / 30 / 24 / 30.</summary>
    public static readonly IReadOnlyDictionary<Wall, int> SensorsPerWall =
        new Dictionary<Wall, int> { [Wall.LEFT] = 24, [Wall.REAR] = 30, [Wall.RIGHT] = 24, [Wall.FRONT] = 30 };

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
    /// The two Sensors the Owner critical topology correction restored to the catalogue.
    /// They are ordinary members of <see cref="SensorSequence"/>; this list exists only so
    /// that validators can assert their presence explicitly (exactly once each, in ordinal
    /// position). It confers no special behaviour.
    /// </summary>
    public static readonly IReadOnlyList<string> CorrectedSensorIds = ["I7", "I16"];

    /// <summary>
    /// The authoritative Product Sensor sequence: exactly 108 identities in canonical
    /// row-major order — G+201…G+218, G+101…G+118, G1…G18, H1…H18, I1…I18, J1…J18. I7 and I16
    /// appear once each in their normal ordinal position; there is no hole, no appended
    /// identity and no renumbering. Index + 1 is the canonical scan order.
    /// </summary>
    public static IReadOnlyList<string> SensorSequence { get; } = BuildSensorSequence();

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

    /// <summary>True when the identity is one of the 108 authoritative Product Sensors.</summary>
    public static bool IsProductSensorId(string? sensorId) =>
        sensorId is not null && SensorSequence.Contains(sensorId, StringComparer.Ordinal);

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

    private static IReadOnlyList<string> BuildSensorSequence()
    {
        var sequence = new string[MatrixSlots];
        var index = 0;
        for (var row = 1; row <= LogicalRowCount; row++)
        {
            for (var column = 1; column <= LogicalColumnCount; column++)
            {
                sequence[index++] = SensorIdFor(row, column);
            }
        }

        return Array.AsReadOnly(sequence);
    }
}
