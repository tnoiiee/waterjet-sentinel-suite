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

/// <summary>Vertical region of the boiler wall. Rows 1–2 (G+2xx, G+1xx) are UPPER; rows 3–6 (G, H, I, J) are LOWER.</summary>
public enum Region { UPPER, LOWER }

/// <summary>How a Water Jet is physically mounted, per the Owner-approved topology table (ADR-0017 decision 5).</summary>
public enum WaterJetPlacementKind
{
    /// <summary>Mounted at a NON_SENSOR_GAP logical position (the placement anchor).</summary>
    NON_SENSOR_GAP,

    /// <summary>Mounted between two horizontally adjacent Sensor positions.</summary>
    BETWEEN_HORIZONTAL,

    /// <summary>Mounted between two vertically adjacent Sensor positions.</summary>
    BETWEEN_VERTICAL,

    /// <summary>Mounted at the shared endpoint of one vertical and one horizontal anchor pair.</summary>
    JUNCTION,
}

/// <summary>
/// One logical matrix position of the Owner-confirmed 18-column × 6-row matrix —
/// exactly 108 records after a successful import. <see cref="LogicalPositionKind.SENSOR"/>
/// positions carry the canonical <see cref="LogicalId"/> (validated against the label
/// derived from <see cref="OrderTotal"/>); the two NON_SENSOR_GAP positions (I7, I16) are
/// location anchors only: no SensorConfiguration, no Thermocouple channels, no scanOrder,
/// never a queue, selection, Cleaning Job, alarm, or coverage target.
/// </summary>
public sealed record LogicalPositionRecord
{
    /// <summary>Canonical logical-position label, e.g. "I7", "G+102" (sourced from legacy sensorname and validated against the orderTotal-derived label).</summary>
    public required string LogicalId { get; init; }

    /// <summary>SENSOR for the 106 actual Sensors; NON_SENSOR_GAP for I7 and I16.</summary>
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
    /// Legacy zero-based logical-position ordering, range 0–107 over all 108 rows,
    /// INCLUDING the NON_SENSOR_GAP rows I7 and I16. Provenance/layout ordering only —
    /// never the Sensor scanOrder (which is derived densely, one-based 1–106, after
    /// excluding NON_SENSOR_GAP positions).
    /// </summary>
    public required int OrderTotal { get; init; }

    /// <summary>Wall-local scan order preserved from the legacy record.</summary>
    public required int OrderWall { get; init; }

    /// <summary>
    /// Legacy database record identifier (the legacy CSV 'id' value), preserved as
    /// provenance only. Never a logical-position identity and never a Sensor ID.
    /// </summary>
    public required string LegacyRecordId { get; init; }

    /// <summary>The Water Jet physically anchored at this position; non-null only for NON_SENSOR_GAP positions (I7 → WJ3, I16 → WJ1).</summary>
    public string? GapAnchorForWaterJetId { get; init; }
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
    /// NON_SENSOR_GAP placements anchor at the gap's neighbouring Sensor positions (the
    /// gap identity itself is carried by LogicalPositionRecord.GapAnchorForWaterJetId);
    /// JUNCTION placements list the distinct endpoints of the vertical and horizontal
    /// anchor pairs in first-appearance order.
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
