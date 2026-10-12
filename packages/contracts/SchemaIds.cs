namespace Wjss.Contracts;

/// <summary>
/// Schema identity for the Product presentation contracts (ADR-0014, draft).
/// The spike identifiers (<c>wjss.spike.snapshot/1</c>) are NOT reused: this is
/// the product namespace. Fields marked as additive-protected may only grow in
/// a <c>/1</c> world; a structural break requires <c>/2</c> and a contract gate.
///
/// Stage 0.3A-3 Checkpoint C is such a structural break and moves both identifiers
/// to <c>/2</c>: the wall-map slot kind vocabulary became the canonical
/// <c>NON_SENSOR_GAP</c> (ADR-0017), <c>equipmentId</c> became
/// <c>gapAnchorForWaterJetId</c>, Sensor records gained their assigned Water Jet /
/// Isolation Valve references, and the Snapshot gained the static Water Jet and
/// Isolation Valve topology collections. The only consumers (development Inspector,
/// TypeScript mirror, fixtures and tests) were migrated in the same checkpoint; no
/// deployed consumer of <c>/1</c> exists.
///
/// Owner critical topology correction (Stage 0.4C-K3, 2026-10-12), still within <c>/2</c>:
/// the slot kind vocabulary shrank to <c>SENSOR</c> only (I7 and I16 are Product Sensors;
/// 108 Sensors / 216 channels), <c>gapAnchorForWaterJetId</c> is no longer emitted, and
/// <c>WaterJetPlacementKind</c> gained <c>UNRESOLVED</c> for the two placements that depended
/// on the revoked gap positions. No deployed consumer exists; the in-repository consumers
/// were migrated in the same pass. Whether the Owner contract gate assigns <c>/3</c> to this
/// change is an Owner decision recorded in <c>docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md</c>.
/// </summary>
public static class SchemaIds
{
    public const string Snapshot = "wjss.snapshot/2";
    public const string Delta = "wjss.delta/2";

    /// <summary>Numeric API version carried in every envelope (route family is /api/v1).</summary>
    public const int ApiVersion = 1;
}

/// <summary>
/// Stage 0.3A-1 markers. The runtime host answers /health/ready with
/// <see cref="RuntimeNotImplemented"/> until Stage 0.3A-2+ is delivered and
/// Owner-local validated. No behaviour is claimed by these constants.
/// </summary>
public static class Stage03A1
{
    public const string Marker = "STAGE_03A1_SKELETON";
    public const string RuntimeNotImplemented = "RUNTIME_NOT_IMPLEMENTED";
}
