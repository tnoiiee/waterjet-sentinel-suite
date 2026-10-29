namespace Wjss.Contracts;

/// <summary>
/// Canonical equipment topology records (ADR-0017, Owner-approved Stage 0.3A-3
/// Checkpoint A; implemented by Stage 0.3A-3 Checkpoint B).
///
/// These records are the canonical vocabulary of the legacy sensor-parameter migration:
/// <see cref="LogicalPositionRecord"/> (one per logical matrix position, 108 records),
/// <see cref="WaterJetConfiguration"/> (exactly 8) and
/// <see cref="IsolationValveConfiguration"/> (exactly 8, one-to-one ordinal paired with
/// the Water Jets). The Water Jet / Isolation Valve records are ALSO the Snapshot's
/// static topology collections since the Stage 0.3A-3 Checkpoint C migration (they are
/// configuration topology references, never controllable device instances).
///
/// No Production acquisition parameter exists in this file: acquisition bindings remain
/// deferred raw provenance (<see cref="DeferredAcquisitionProvenance"/> in Config.cs).
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the gate).
/// </summary>

/// <summary>Vertical region of the boiler wall. Rows 1–3 (G+2xx, G+1xx and G) are UPPER; rows 4–6 (H, I and J) are LOWER. The derivation itself lives in <c>WaterJetTopologyCatalog.RegionForLogicalRow</c>.</summary>
public enum Region { UPPER, LOWER }

/// <summary>
/// The fine-grained mounting placement of a Water Jet. Under the current Owner placement
/// disposition (Stage 0.4C-K3 installed-position ruling, completed by the Canonical
/// Topology Adoption, 2026-10-21) the fine-grained placement of ALL EIGHT Water Jets is
/// <see cref="UNRESOLVED"/>: the former "mounted at the I16 / I7 gap" placements of
/// WJ1 / WJ3 are revoked together with the gap positions themselves, and the stale
/// anchors formerly carried by WJ2, WJ4, WJ5, WJ6, WJ7 and WJ8 described the opposite
/// installed tier, so they are removed. No replacement anchor is inferred, mirrored or
/// transferred. The resolved kinds below remain defined vocabulary for a future
/// authoritative layout; they are not current Product truth, and the canonical
/// installed wall / region of every Water Jet is authoritative regardless.
/// </summary>
public enum WaterJetPlacementKind
{
    /// <summary>
    /// Fine-grained installed placement pending authoritative equipment layout (the
    /// canonical installed wall / region is confirmed; only the placement and its anchors
    /// are unresolved). No wall coordinate, Sensor anchor, target wall, target Sensor,
    /// legacy cannon value or Assigned Cleaning Device ID may be used to infer a
    /// replacement. Carries no placement anchors.
    /// </summary>
    UNRESOLVED,

    /// <summary>Mounted between two horizontally adjacent Sensor positions.</summary>
    BETWEEN_HORIZONTAL,

    /// <summary>Mounted between two vertically adjacent Sensor positions.</summary>
    BETWEEN_VERTICAL,

    /// <summary>Mounted at the shared endpoint of one vertical and one horizontal anchor pair.</summary>
    JUNCTION,
}

/// <summary>
/// One logical matrix position of the Owner-confirmed 18-column × 6-row matrix —
/// exactly 108 records after a successful import, every one of them a
/// <see cref="LogicalPositionKind.SENSOR"/> position carrying the canonical
/// <see cref="LogicalId"/> (validated against the label derived from <see cref="OrderTotal"/>).
/// Owner critical topology correction: I7 and I16 are normal Product Sensors with a
/// SensorConfiguration, two Thermocouple channels and a scanOrder like every other Sensor.
/// </summary>
public sealed record LogicalPositionRecord
{
    /// <summary>Canonical logical-position label, e.g. "I7", "G+102" (sourced from legacy sensorname and validated against the orderTotal-derived label).</summary>
    public required string LogicalId { get; init; }

    /// <summary>Always SENSOR: all 108 logical positions are Product Sensors (I7 and I16 included).</summary>
    public required LogicalPositionKind PositionKind { get; init; }

    public required Wall Wall { get; init; }

    /// <summary>1–18 across the whole matrix, derived from OrderTotal and the fixed 18-column matrix.</summary>
    public required int LogicalColumn { get; init; }

    /// <summary>1–6, top to bottom (G+2xx, G+1xx, G, H, I, J), derived from OrderTotal.</summary>
    public required int LogicalRow { get; init; }

    /// <summary>1-based column inside the wall (LEFT/RIGHT 1–4, REAR/FRONT 1–5).</summary>
    public required int WallColumn { get; init; }

    /// <summary>1–6 inside the wall; equals LogicalRow (no rotation, no reversal).</summary>
    public required int WallRow { get; init; }

    /// <summary>
    /// Legacy zero-based logical-position ordering, range 0–107 over all 108 rows.
    /// Provenance/layout ordering only — never the Sensor scanOrder (which is derived
    /// densely, one-based 1–108, over all 108 Sensor positions in OrderTotal order; the
    /// canonical scanOrder therefore equals OrderTotal + 1).
    /// </summary>
    public required int OrderTotal { get; init; }

    /// <summary>Wall-local scan order preserved from the legacy record.</summary>
    public required int OrderWall { get; init; }

    /// <summary>
    /// Legacy database record identifier (the legacy CSV 'id' value), preserved as
    /// provenance only. Never a logical-position identity and never a Sensor ID.
    /// </summary>
    public required string LegacyRecordId { get; init; }
}

/// <summary>
/// One of exactly eight Water Jets (WJ1–WJ8). Installed position (wall + region +
/// placement kind + anchors) is defined ONLY by the Owner-approved topology table and is
/// separate from target coverage: a Water Jet is installed on one wall and sprays the
/// OPPOSITE wall (LEFT↔RIGHT, FRONT↔REAR) in the matching region. A Water Jet is not a
/// logical-matrix position and its identity never appears as a Sensor ID.
/// </summary>
public sealed record WaterJetConfiguration
{
    /// <summary>"WJ1" … "WJ8".</summary>
    public required string WaterJetId { get; init; }

    public required Wall InstalledWall { get; init; }
    public required Region InstalledRegion { get; init; }
    public required WaterJetPlacementKind PlacementKind { get; init; }

    /// <summary>
    /// Logical position ids anchoring the placement, per the approved topology table.
    /// Under the current Owner placement disposition every Water Jet is
    /// <see cref="WaterJetPlacementKind.UNRESOLVED"/> and this list is EMPTY for all
    /// eight: no Sensor position is inferred, mirrored or transferred as a replacement
    /// anchor. (For a future authoritative layout, JUNCTION placements would list the
    /// distinct endpoints of the vertical and horizontal anchor pairs in
    /// first-appearance order.)
    /// </summary>
    public required IReadOnlyList<string> PlacementAnchors { get; init; }

    /// <summary>The opposite wall the Water Jet sprays. Never the installed wall.</summary>
    public required Wall TargetWall { get; init; }

    /// <summary>The vertical region of the target wall; equals the installed region.</summary>
    public required Region TargetRegion { get; init; }

    /// <summary>The dedicated Isolation Valve; one-to-one and ordinal-matched (WJn ↔ IVn).</summary>
    public required string DedicatedIsolationValveId { get; init; }
}

/// <summary>
/// One of exactly eight Isolation Valves (IV1–IV8), each paired one-to-one and
/// ordinal-matched with its Water Jet (WJn ↔ IVn). No valve actuation parameters exist in
/// the Owner data; none are invented here. OUT_OF_SERVICE semantics remain as approved in
/// the queue model.
/// </summary>
public sealed record IsolationValveConfiguration
{
    /// <summary>"IV1" … "IV8".</summary>
    public required string ValveId { get; init; }

    /// <summary>The exactly-one Water Jet this valve serves; the ordinal must match.</summary>
    public required string ServedWaterJetId { get; init; }
}
