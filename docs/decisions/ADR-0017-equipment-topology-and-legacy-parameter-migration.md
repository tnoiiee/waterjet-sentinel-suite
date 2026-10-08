# ADR-0017 — Equipment Topology Decision and Legacy Parameter Migration Specification

- **Status:** PROPOSED — authored under the Stage 0.3A-3 Checkpoint A Owner authorization
  (planning / decision / specification only). The topology facts recorded here are
  **Owner-provided** and stated as canonical. Formal acceptance requires an explicit Owner
  decision. PR merge records repository integration and does not by itself constitute
  acceptance unless the Owner explicitly states acceptance as part of the merge decision.
  This ADR's status remains `PROPOSED` until explicit Owner acceptance. Planning output
  only: no Product source, contract, fixture, lock, test, or Inspector file is changed by
  this record. Checkpoint B implementation is **NOT STARTED**.
- **Date:** 2026-10-08 (corrected same day by two Stage 0.3A-3 Checkpoint A Owner reviews:
  first scanOrder derivation and acceptance semantics; then legacy field semantics,
  deferred mappings, TC-channel scope, public-repository boundary, and transitional
  runtime vocabulary scope)
- **Supersedes: the *interpretation* of logical positions I7 and I16 as **Cannon /
  Water Jet equipment slots** (previously carried by
  [`../../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) §2.2.1, [`../../REQUIREMENTS.md`](../REQUIREMENTS.md)
  PHY-001, and the simulator map comments). The grid geometry, all counts, and the gap
  *positions* remain `[OWNER CONFIRMED]` and are not changed; only the meaning of the two
  gaps changes: they are **NON_SENSOR_GAP placement anchors**, not equipment entities.
- **Scope:** canonical equipment topology (LogicalPosition, SensorConfiguration,
  WaterJetConfiguration, IsolationValveConfiguration), installed-position vs target-coverage
  separation, WJ↔IV pairing, legacy `sensorparam.csv` migration rules, I7/I16 exclusion
  rules, validation/refusal-code proposal, and the Checkpoint B expected change set.
- **Authority:** Owner Stage 0.3A-3 Checkpoint A instruction (2026-10-08) on approved
  remote-main base `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce`. Not authorized: Checkpoint B
  implementation, Checkpoint C runtime/Inspector work, queue dispatch, cleaning execution,
  pump commands, Isolation Valve actuation, axis motion, Mandatory Safe Return actuation,
  TEST_HARDWARE, PRODUCTION device access, ZIP, Release, Deployment.

---

## Context

The Stage 0.2.1A Owner domain correction defined the logical matrix — **18 columns × 6 rows
= 108 logical positions, of which 106 are Sensors** — and labelled the two sensorless
positions "Cannon equipment slots" (I7 Rear, I16 Front). The Owner has now superseded that
interpretation: **I7 and I16 are not equipment at all.** There are no Cannon entities in the
canonical model. I7 and I16 are **NON_SENSOR_GAP** logical positions that exist to record
*where a Water Jet is physically mounted*:

- **I7** is the physical placement anchor of **WJ3** (installed Rear lower); the sensor
  sequence skips it, I6 → I8.
- **I16** is the physical placement anchor of **WJ1** (installed Front lower); the sensor
  sequence skips it, I15 → I17.

Separately, the Owner has provided the legacy sensor-parameter database (`sensorparam.csv`)
whose `cannon` column means **Assigned Cleaning Device ID**, i.e. a direct ordinal reference
to WJ1–WJ8 — **not** a grid position, not an installed wall, and not "nearest/same-wall
Water Jet". Stage 0.3A-3 (the config loading pipeline) needs a settled topology and a
deterministic migration specification before any importer code is written. This record is
that decision. The full specification tables live in the companion checkpoint report
[`../STAGE_0.3A-3_CHECKPOINT_A.md`](../STAGE_0.3A-3_CHECKPOINT_A.md); this ADR fixes the
decisions themselves.

## Decision

1. **Canonical LogicalPosition model.** One record per logical matrix position — 108
   records. Attributes: `legacyRecordId` (the legacy `id` — a **database record
   identifier**, preserved as provenance only; never a logical-position identity and
   never a Sensor ID), `logicalId` (canonical logical-position label, e.g. `I7`, `G+102` —
   sourced from legacy `sensorname` and validated against the label derived from
   `orderTotal`, per rule 9), `wall`
   (`LEFT|REAR|RIGHT|FRONT`), `logicalColumn` (1–18), `logicalRow` (1–6), `wallColumn`,
   `wallRow`, `positionKind` (`SENSOR | NON_SENSOR_GAP`), `orderTotal` (legacy zero-based
   logical-position ordering, range 0–107 over all 108 rows, **including** the
   NON_SENSOR_GAP rows I7 and I16; provenance/layout ordering only — never the Sensor
   scanOrder), `orderWall`,
   and for NON_SENSOR_GAP positions only, `gapAnchorForWaterJetId` (`I7 → WJ3`,
   `I16 → WJ1`). The `sensorname` values of I7 and I16 remain their `logicalId` values as
   NON_SENSOR_GAP positions and never create a SensorConfiguration. Exactly 2 positions
   are NON_SENSOR_GAP; exactly 106 are SENSOR.

2. **Canonical SensorConfiguration model.** One record per actual Sensor — exactly 106.
   Identity/layout (`sensorId` — canonical, taken from legacy `sensorname` for SENSOR rows
   and validated per rule 9; `wall`, logical and wall coordinates, `orderWall`, and
   `scanOrder` — derived per the rule below, never taken directly from `order_total`),
   dirty-score settings imported as **direct recorded renames of the legacy numeric
   values** (`DiffLowerBound`/`DiffUpperBound` from
   `min_temp_dirtyscore`/`max_temp_dirtyscore`; `DirtyScoreThreshold` from
   `threshold_setpoint`; values `[NOT VERIFIED]`), eligibility/history (`Enabled` from
   `sensor_enable`; `cleaningCount` as the raw copy of `cleaning_count`), **exactly one
   `assignedWaterJetId`**, and `assignedIsolationValveId` **derived** through the pairing
   (never independently assigned). The already-approved DOMAIN_MODEL §2.3 attributes
   remain the long-term Sensor model, but the Checkpoint B importer must **not** derive:
   `UseDirtyScoreThreshold` (from `threshold_setpoint`), `HasVerifiedCleaningHistory`
   (from `cleaning_count`), `LastSuccessfulCleaningCompletedAt` (from
   `lastclean_timestamp`), or `HardMinimumCleaningInterval` (from
   `min_time_allowaddtoqueue`). Those four are deferred domain interpretations requiring
   a later explicit Owner decision; the corresponding legacy values are preserved as
   raw/imported/deferred fields with provenance and warnings.
   Acquisition binding (`ip_modbus`, `channel_pair`, `base_modbus_address`) is preserved
   raw and **DEFERRED**: it is not approved Production-device configuration, and no
   Production channel identity, Modbus address, or physical binding is created or claimed.
   No NON_SENSOR_GAP position ever yields a SensorConfiguration record.
   **`scanOrder` derivation rule:** legacy `order_total` is the logical-position order
   (zero-based 0–107, including I7 and I16); it is preserved as `orderTotal`
   provenance/layout ordering and is never itself the Sensor scanOrder. Canonical
   `scanOrder` is derived deterministically: sort all 108 logical positions by
   `orderTotal`, exclude `positionKind = NON_SENSOR_GAP` (I7, I16), then assign the dense
   one-based range 1–106 to the 106 actual Sensors, gapless, no duplicates. I7 and I16
   receive no SensorConfiguration and no scanOrder.

3. **Canonical WaterJetConfiguration model.** Exactly 8 records: `WJ1`–`WJ8`. Attributes:
   `waterJetId`, `installedWall`, `installedRegion` (`UPPER | LOWER`), `placementKind`
   (`NON_SENSOR_GAP | BETWEEN_HORIZONTAL | BETWEEN_VERTICAL | JUNCTION`),
   `placementAnchors` (logical position ids per the approved topology table),
   `targetWall`, `targetRegion`, and `pairedIsolationValveId`. A Water Jet is **not** a
   logical-matrix position and never appears as a Sensor id.

4. **Canonical IsolationValveConfiguration model.** Exactly 8 records: `IV1`–`IV8`.
   Attributes: `valveId` and `pairedWaterJetId`. No valve actuation parameters exist in the
   Owner data; none are invented. `OUT_OF_SERVICE` valve semantics remain as approved in
   the queue model.

5. **Installed-position model.** Installed position is defined **only** by the approved
   topology table (wall + region + placement kind + anchors), not inferred from sensor
   geometry and not derived from target coverage. Approved placements:
   WJ1 Front/LOWER at NON_SENSOR_GAP I16 (anchors I15, I17); WJ2 Left/LOWER
   BETWEEN_HORIZONTAL (J2, J3); WJ3 Rear/LOWER at NON_SENSOR_GAP I7 (I6, I8); WJ4
   Right/LOWER BETWEEN_HORIZONTAL (J11, J12); WJ5 Front/UPPER BETWEEN_VERTICAL
   (G+216, G+116); WJ6 Left/UPPER JUNCTION (vertical G+102–G+202, horizontal G+202–G+203);
   WJ7 Rear/UPPER BETWEEN_VERTICAL (G+207, G+107); WJ8 Right/UPPER JUNCTION (vertical
   G+111–G+211, horizontal G+211–G+212).

6. **Target-coverage model.** Target coverage is a separate concept from installed
   position. A Water Jet installed on one wall sprays the **opposite** wall
   (`LEFT↔RIGHT`, `FRONT↔REAR`) in the matching region. Approved targets: WJ1→REAR LOWER,
   WJ2→RIGHT LOWER, WJ3→FRONT LOWER, WJ4→LEFT LOWER, WJ5→REAR UPPER, WJ6→RIGHT UPPER,
   WJ7→FRONT UPPER, WJ8→LEFT UPPER. Target membership of a Sensor requires **both** that
   the Sensor's wall and region match the assigned Water Jet's target wall and region
   **and** that the legacy `cannon` value maps to that Water Jet; a disagreement is a
   fail-closed validation error, never a silent remap.

7. **WaterJet-to-Valve pairing invariant.** Exactly one-to-one, ordinal-matched:
   `WJn ↔ IVn` for n = 1…8. No Water Jet lacks a Valve; no Valve serves two Water Jets;
   no Valve exists without its Water Jet; the ordinals must match.

8. **Sensor assignedWaterJetId invariant.** Every actual Sensor carries **exactly one**
   `assignedWaterJetId` drawn from `WJ1`–`WJ8` (derived directly from legacy `cannon`
   n). Multiple Sensors may share one Water Jet; every reference resolves; the paired
   Isolation Valve follows from the pairing invariant.

9. **Legacy CSV migration rules.** Legacy `cannon` values map **directly and only** by
   ordinal: `cannon n → assignedWaterJetId WJn`. No wall-based remapping exists. The
   `cannon` column name survives only as provenance. Legacy `id` is preserved **only** as
   `legacyRecordId` provenance; legacy `sensorname` is the canonical
   `LogicalPosition.logicalId` and, for SENSOR rows, the canonical
   `SensorConfiguration.sensorId`. **Deterministic logical-label validation:** derive the
   logical row and column from `orderTotal` and the fixed 18-column matrix, derive the
   expected logical label from that row/column, and require `sensorname` to equal it; any
   mismatch refuses with the additive code `MIGRATION_LOGICAL_LABEL_MISMATCH`. All
   normalizations (wall tokens, boolean tokens, canonical field renames) are recorded
   normalization actions; **no timestamp is converted to UTC** (source timezone UNKNOWN;
   raw string preserved) and **no duration/TimeSpan is invented** (units UNKNOWN; raw
   values preserved); suspicious or placeholder values are preserved verbatim with
   warnings — never silently normalized. The full field-by-field matrix is in the
   checkpoint report.

10. **I7/I16 exclusion rules.** I7 and I16 are NON_SENSOR_GAP: never Sensors, never Water
    Jets, never queue, selection, Cleaning Job, alarm, or coverage targets, and never
    SensorConfiguration records. They exist in LogicalPosition only, as the physical
    placement anchors of WJ3 and WJ1 respectively. The sensor sequence skips them
    (I6→I8, I15→I17), and they receive no Sensor `scanOrder` — they carry only their
    legacy `orderTotal` positions in the 0–107 logical ordering. Their zero-valued
    dirty-score CSV fields are placeholder-like
    logical-position data, rejected from sensor import (disposition
    **REJECT FOR I7/I16**) and preserved only as gap provenance.

11. **Validation and refusal machine-code proposal.** A new config-validation refusal-code
    family is proposed — `TOPO_POSITION_COUNT`, `TOPO_SENSOR_COUNT`, `TOPO_CHANNEL_COUNT`,
    `TOPO_WALL_COUNTS`, `TOPO_GAP_IDENTITY`, `TOPO_DUPLICATE_ID`, `TOPO_DUPLICATE_ORDER`,
    `TOPO_WATERJET_COUNT`, `TOPO_VALVE_COUNT`, `TOPO_PAIRING_MISMATCH`,
    `TOPO_TARGET_WALL_CONTRADICTION`, `MIGRATION_CANNON_RANGE`,
    `MIGRATION_MISSING_ASSIGNMENT`, `MIGRATION_LOGICAL_LABEL_MISMATCH`,
    `MIGRATION_FIELD_TOKEN`, `MIGRATION_BOUNDS_ORDER`,
    `MIGRATION_PLACEHOLDER_VALUE`, `MIGRATION_UNIT_UNVERIFIED`,
    `MIGRATION_TIMEZONE_UNKNOWN` — additive, no existing code
    changed. Structural violations fail closed and abort the whole import atomically (no
    partial import, no partial revision publication); `TOPO_DUPLICATE_ID` guards the
    uniqueness of the legacy record identifier. Warning-level codes
    (`MIGRATION_PLACEHOLDER_VALUE`, `MIGRATION_UNIT_UNVERIFIED`,
    `MIGRATION_TIMEZONE_UNKNOWN`) import verbatim with recorded warnings and
    Owner-review entries instead of failing. Determinism: identical CSV bytes
    produce identical results.

12. **Migration warnings and Owner-review list.** Values that are representable but
    suspicious (degenerate bounds or thresholds on actual Sensors, offset-free sentinel
    timestamps of unconfirmed meaning, absent/unknown unit metadata for time fields,
    UNKNOWN source timezones, the superseded "Cannon" vocabulary in the transitional
    runtime representation and in existing documents/code identifiers, DEFER-field
    mapping) are preserved verbatim and
    surfaced on an explicit Owner-review list (checkpoint report §10). Nothing is silently
    normalized, defaulted, or dropped.

13. **Checkpoint B expected change set** is planned (not started): contracts
    (`Topology.cs` new; `Config.cs` and `Enums.cs` extensions — the latter only to add the
    canonical `LogicalPositionKind` vocabulary, never to rename the existing
    `SlotType.CANNON` wire surface), domain importer + validator
    (`packages/domain/`), a structure-only migrated config example
    (`config/examples/`), config tests for the invariants below, and documentation
    propagation of the superseded Cannon wording. Exact file list: checkpoint report §11.

14. **Protected baseline.** All Owner-confirmed counts and identities stand: 108 logical
    positions; 106 Sensors; 212 Thermocouple channels; Left 24 / Rear 29 / Right 24 /
    Front 29; I7 and I16 are NON_SENSOR_GAP; WJ1–WJ8 and IV1–IV8 identities;
    one-to-one ordinal pairing; installed position separate from target coverage; legacy
    `cannon n → WJn` direct mapping; Single Active Job; GlobalQueue head-only; Pump
    high-critical semantics; Mandatory Safe Return ordering; `activeJob` three-state wire
    semantics; SIMULATOR-only runtime boundary; no command or write path; the historical
    React spike remains untouched. No Production equipment parameter is invented: every
    non-Owner value stays `[NOT VERIFIED]` / `[OPEN]`.

15. **Transitional runtime vocabulary scope.** Checkpoint B introduces the canonical
    topology vocabulary `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` and uses it in
    all importer output, but must **NOT** rename or alter the existing Runtime/Snapshot
    wire surface that uses `SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`,
    `CannonSlotCount`, and `CANNON_REAR`/`CANNON_FRONT` — that representation is
    referenced by Runtime, Simulator, Snapshot, API, Inspector, fixtures, and tests, and
    changing it atomically belongs to **Checkpoint C**, which is NOT AUTHORIZED. The
    existing CANNON vocabulary is recorded as: **TRANSITIONAL LEGACY RUNTIME
    REPRESENTATION — SEMANTICALLY SUPERSEDED — RUNTIME MIGRATION DEFERRED TO CHECKPOINT
    C.** No canonical Cannon entity exists; the importer never emits `SlotType.CANNON`;
    and no alias permits Cannon and NON_SENSOR_GAP to be used interchangeably in
    canonical topology.

16. **TC channel scope.** Checkpoint B may enforce only the structural invariant —
    106 Sensors, exactly two expected Thermocouple channel sides per Sensor, structural
    total 212. The acquisition fields `ip_modbus`, `channel_pair`, and
    `base_modbus_address` remain deferred; no Production channel identity, Modbus
    address, or physical binding is created or claimed. The existing `SYN-TC-*`
    identities remain synthetic runtime data and are not Production migration evidence.

17. **Public repository boundary.** `sensorparam.csv` is Owner working data and contains
    non-public acquisition values. It must not be committed and must not be copied into
    tests, fixtures, documentation, or config examples. No real IP, register base, channel
    binding, timestamp, or plant value from the Owner CSV may enter Git (boundary S3).
    Checkpoint B tests must use clearly synthetic public-safe rows.

## Status classification

| Classification | Items |
| --- | --- |
| **OWNER CONFIRMED** | 108 logical positions; 106 Sensors; 212 Thermocouple channels; I7 and I16 are NON_SENSOR_GAP; WJ1–WJ8 installed positions; WJ1–WJ8 target coverage; WJn paired one-to-one with IVn; legacy `cannon n` maps directly to WJn |
| **NOT VERIFIED** | Production acquisition bindings (`ip_modbus`, `channel_pair`, `base_modbus_address` — DEFER, never approved Production-device configuration); legacy time-field units (`min_time_allowaddtoqueue` and the DEFER time fields); sentinel timestamp meaning (`lastclean_timestamp` zero/sentinel values) and its source timezone; runtime importer execution (no importer exists yet); physical device integration (no device has ever been addressed) |

## Consequences

- The canonical model has **no Cannon entity**. Existing code identifiers and comments that
  say "Cannon" (`SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`,
  `CANNON_REAR`/`CANNON_FRONT`, `EquipmentId`, simulator map comments,
  and the §2.2.1 wording noted above) are now **naming debt against this decision**,
  recorded as the TRANSITIONAL LEGACY RUNTIME REPRESENTATION — semantically superseded;
  the atomic runtime/wire migration belongs to **Checkpoint C** (NOT AUTHORIZED), not
  Checkpoint B (decision 15).
  The *behaviour* those names protect (two sensorless positions excluded from sensor
  runtime, queues, and jobs) is unchanged.
- Any future importer that mapped `cannon` by wall, or represented WJ1 as `I16` / WJ3 as
  `I7`, would now contradict an accepted decision and must fail validation instead.
- Stage 0.3A-3 (config pipeline) proceeds only on the basis of this topology;
  Checkpoint B remains unauthorized until the Owner authorizes it.
