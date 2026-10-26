using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// The Owner-approved Water Jet / Isolation Valve topology: the canonical installed
/// topology adopted by the Owner-approved canonical Equipment topology adoption
/// (2026-10-25), superseding the installed wall / region and target rows adopted by the
/// Owner Canonical Topology Adoption (2026-10-21) and the earlier ADR-0017 decisions 5
/// and 7 (the ADR-0017 addendum records the supersession chain; the earlier tables are
/// history).
///
/// Installed position and target coverage are SEPARATE concepts: a Water Jet is installed
/// on one wall and sprays the OPPOSITE wall (LEFT↔RIGHT, FRONT↔REAR) in the matching
/// region. The pairing is mandatory one-to-one with matching ordinals: WJn ↔ IVn.
/// Canonical installed topology: WJ1–WJ4 are the UPPER tier and WJ5–WJ8 the LOWER tier
/// (WJ1 LEFT/UPPER, WJ2 REAR/UPPER, WJ3 RIGHT/UPPER, WJ4 FRONT/UPPER, WJ5 FRONT/LOWER,
/// WJ6 REAR/LOWER, WJ7 LEFT/LOWER, WJ8 RIGHT/LOWER), each targeting the opposite wall in
/// the same tier. The historical Legacy WJ identity of each Water Jet (WJ1 = legacy WJ2,
/// WJ2 = legacy WJ3, WJ3 = legacy WJ4, WJ4 = legacy WJ1, WJ5 = legacy WJ5, WJ6 = legacy
/// WJ7, WJ7 = legacy WJ6, WJ8 = legacy WJ8) is historical equipment identity ONLY: it is
/// distinct from the legacy cannon identity, and legacy <c>cannon n</c> continues to map
/// DIRECTLY to canonical WJn (no cannon permutation exists).
///
/// Owner placement disposition (Canonical Topology Adoption, incorporating the Stage
/// 0.4C-K3 installed-position ruling): logical I7 and I16 are Product Sensors, so the
/// former "mounted at the I16 / I7 gap" placements of WJ1 / WJ3 stay revoked; and the
/// stale fine-grained anchors formerly carried by WJ2, WJ4, WJ5, WJ6, WJ7 and WJ8
/// described the opposite installed tier, so they are removed. The FINE-GRAINED
/// placement of ALL EIGHT Water Jets is therefore
/// <see cref="WaterJetPlacementKind.UNRESOLVED"/> with NO placement anchors: no anchor
/// is inferred, mirrored or transferred from I7, I16, a former gap, a Sensor wall, the
/// target wall, a target Sensor, the legacy cannon value or the Assigned Cleaning
/// Device ID. Only the fine-grained placement is unresolved: the canonical installed
/// wall / region and the canonical target coverage of every Water Jet are authoritative.
/// Legacy <c>cannon n</c> still maps directly to WJn, and WJn remains paired one-to-one
/// with IVn.
///
/// This catalog is the single source of the approved table for the migration importer and
/// the topology validator. It contains no Production equipment parameter: walls, regions,
/// placement kinds, and identities are Owner-approved layout semantics, not device
/// configuration. SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class WaterJetTopologyCatalog
{
    /// <summary>
    /// Operator-facing wording for an <see cref="WaterJetPlacementKind.UNRESOLVED"/> placement.
    /// Only the fine-grained placement / anchors are unresolved; the canonical installed
    /// wall / region is confirmed and is never described as unknown.
    /// </summary>
    public const string UnresolvedPlacementWording = "Fine-grained installed placement pending authoritative layout (canonical wall/tier confirmed; no placement anchors)";

    /// <summary>
    /// The Water Jets whose fine-grained placement is UNRESOLVED under the current Owner
    /// disposition: all eight (WJ1 / WJ3 since the revoked gap placements; WJ2, WJ4–WJ8
    /// since their stale opposite-tier anchors were removed by the Canonical Topology
    /// Adoption). Their canonical installed wall / region remains authoritative.
    /// </summary>
    public static readonly IReadOnlyList<string> UnresolvedPlacementWaterJetIds = ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    /// <summary>The approved Water Jet topology table, in ordinal order WJ1–WJ8.</summary>
    public static IReadOnlyList<WaterJetConfiguration> WaterJets { get; } =
    [
        new WaterJetConfiguration
        {
            WaterJetId = "WJ1",
            InstalledWall = Wall.LEFT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.RIGHT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV1",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ2",
            InstalledWall = Wall.REAR,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.FRONT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV2",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ3",
            InstalledWall = Wall.RIGHT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.LEFT,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV3",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ4",
            InstalledWall = Wall.FRONT,
            InstalledRegion = Region.UPPER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.REAR,
            TargetRegion = Region.UPPER,
            DedicatedIsolationValveId = "IV4",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ5",
            InstalledWall = Wall.FRONT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.REAR,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV5",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ6",
            InstalledWall = Wall.REAR,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.FRONT,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV6",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ7",
            InstalledWall = Wall.LEFT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.RIGHT,
            TargetRegion = Region.LOWER,
            DedicatedIsolationValveId = "IV7",
        },
        new WaterJetConfiguration
        {
            WaterJetId = "WJ8",
            InstalledWall = Wall.RIGHT,
            InstalledRegion = Region.LOWER,
            PlacementKind = WaterJetPlacementKind.UNRESOLVED,
            PlacementAnchors = [],
            TargetWall = Wall.LEFT,
            TargetRegion = Region.LOWER,
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
    /// WJn and a rear-lower Sensor is assigned WJ5 even though WJ5 installs on the
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
