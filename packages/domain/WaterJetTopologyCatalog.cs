using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// The Owner-approved Water Jet / Isolation Valve topology (ADR-0017 decisions 5 and 7,
/// approved at Stage 0.3A-3 Checkpoint A and confirmed by the final Owner review).
///
/// Installed position and target coverage are SEPARATE concepts: a Water Jet is installed
/// on one wall and sprays the OPPOSITE wall (LEFT↔RIGHT, FRONT↔REAR) in the matching
/// region. The pairing is mandatory one-to-one with matching ordinals: WJn ↔ IVn.
///
/// This catalog is the single source of the approved table for the migration importer and
/// the topology validator. It contains no Production equipment parameter: walls, regions,
/// placement kinds, logical anchor labels, and identities are Owner-approved layout
/// semantics, not device configuration. SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class WaterJetTopologyCatalog
{
    /// <summary>The two NON_SENSOR_GAP logical positions and the Water Jets they anchor (I7 → WJ3, I16 → WJ1). They are location anchors only — never equipment identities.</summary>
    public static readonly IReadOnlyDictionary<string, string> GapAnchors = new Dictionary<string, string>
    {
        ["I7"] = "WJ3",
        ["I16"] = "WJ1",
    };

    /// <summary>The approved Water Jet topology table, in ordinal order WJ1–WJ8.</summary>
    public static IReadOnlyList<WaterJetConfiguration> WaterJets { get; } =
    [
        new WaterJetConfiguration
        {
            WaterJetId = "WJ1",
            InstalledWall = Wall.FRONT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.NON_SENSOR_GAP,
            PlacementAnchors = ["I15", "I17"],
            TargetWall = Wall.REAR,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV1",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ2",
            InstalledWall = Wall.LEFT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.BETWEEN_HORIZONTAL,
            PlacementAnchors = ["J2", "J3"],
            TargetWall = Wall.RIGHT,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV2",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ3",
            InstalledWall = Wall.REAR,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.NON_SENSOR_GAP,
            PlacementAnchors = ["I6", "I8"],
            TargetWall = Wall.FRONT,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV3",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ4",
            InstalledWall = Wall.RIGHT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.BETWEEN_HORIZONTAL,
            PlacementAnchors = ["J11", "J12"],
            TargetWall = Wall.LEFT,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV4",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ5",
            InstalledWall = Wall.FRONT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.BETWEEN_VERTICAL,
            PlacementAnchors = ["G+216", "G+116"],
            TargetWall = Wall.REAR,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV5",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ6",
            InstalledWall = Wall.LEFT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.JUNCTION,
            PlacementAnchors = ["G+102", "G+202", "G+203"],
            TargetWall = Wall.RIGHT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV6",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ7",
            InstalledWall = Wall.REAR,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.BETWEEN_VERTICAL,
            PlacementAnchors = ["G+207", "G+107"],
            TargetWall = Wall.FRONT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV7",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ8",
            InstalledWall = Wall.RIGHT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.JUNCTION,
            PlacementAnchors = ["G+111", "G+211", "G+212"],
            TargetWall = Wall.LEFT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV8",
        },
    ];

    /// <summary>The approved pairing table, in ordinal order IV1–IV8.</summary>
    public static IReadOnlyList<IsolationValveConfiguration> IsolationValves { get; } =
    [
        new IsolationValveConfiguration { ValveId = "IV1", ServedWaterJetId = "WJ1" },
        new IsolationValveConfiguration { ValveId = "IV2", ServedWaterJetId = "WJ2" },
        new IsolationValveConfiguration { ValveId = "IV3", ServedWaterJetId = "WJ3" },
        new IsolationValveConfiguration { ValveId = "IV4", ServedWaterJetId = "WJ4" },
        new IsolationValveConfiguration { ValveId = "IV5", ServedWaterJetId = "WJ5" },
        new IsolationValveConfiguration { ValveId = "IV6", ServedWaterJetId = "WJ6" },
        new IsolationValveConfiguration { ValveId = "IV7", ServedWaterJetId = "WJ7" },
        new IsolationValveConfiguration { ValveId = "IV8", ServedWaterJetId = "WJ8" },
    ];

    private static readonly Dictionary<string, WaterJetConfiguration> WaterJetsById =
        WaterJets.ToDictionary(w => w.WaterJetId, StringComparer.Ordinal);

    /// <summary>Vertical region of a logical row: rows 1–2 (G+2xx, G+1xx) are UPPER; rows 3–6 (G, H, I, J) are LOWER.</summary>
    public static Region RegionForLogicalRow(int logicalRow) => logicalRow switch
    {
        1 or 2 => Region.UPPER,
        3 or 4 or 5 or 6 => Region.LOWER,
        _ => throw new ArgumentOutOfRangeException(nameof(logicalRow), "logical row must be 1-6"),
    };

    /// <summary>Resolves a Water Jet by exact id, or null when the id is not one of WJ1–WJ8.</summary>
    public static WaterJetConfiguration? FindWaterJet(string waterJetId) =>
        WaterJetsById.TryGetValue(waterJetId, out var waterJet) ? waterJet : null;

    /// <summary>
    /// Verifies one Sensor's cleaning-device assignment against the approved topology:
    /// the assigned Water Jet must exist, its TARGET coverage must match the Sensor's
    /// wall and region (assignment follows responsibility for the target wall, never
    /// the installed position — the legacy cleaning-device ordinal maps directly to
    /// WJn and a rear-lower Sensor is assigned WJ1 even though WJ1 installs on the
    /// front-lower wall), and the Isolation Valve must be exactly that Water Jet's
    /// dedicated IVn. Returns false on any mismatch; never throws for an assignment
    /// problem, so callers can refuse with their own reason code.
    /// </summary>
    public static bool TryRequireAssignment(
        string sensorId,
        Wall sensorWall,
        Region sensorRegion,
        string? assignedWaterJetId,
        string? assignedIsolationValveId)
    {
        if (string.IsNullOrWhiteSpace(sensorId)
            || string.IsNullOrWhiteSpace(assignedWaterJetId)
            || string.IsNullOrWhiteSpace(assignedIsolationValveId))
        {
            return false;
        }

        var waterJet = FindWaterJet(assignedWaterJetId);
        return waterJet is not null
            && waterJet.TargetWall == sensorWall
            && waterJet.TargetRegion == sensorRegion
            && string.Equals(waterJet.DedicatedIsolationValveId, assignedIsolationValveId, StringComparison.Ordinal);
    }
}
