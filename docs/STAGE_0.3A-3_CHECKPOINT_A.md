# Stage 0.3A-3 — Checkpoint A Report: Equipment Topology Decision and Legacy Parameter Migration Specification

- **Mode:** PLANNING / DECISION / SPECIFICATION ONLY — no Product coding.
- **Status:** AUTHORIZED (Owner Checkpoint A instruction, 2026-10-08) — **PR OPEN, NOT
  MERGED.** Checkpoint B implementation **NOT STARTED**.
- **Decision record:** [`docs/decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md`](decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md)
- **Not authorized:** Checkpoint B implementation; Checkpoint C Runtime/Inspector
  implementation; queue dispatch; cleaning execution; pump commands; Isolation Valve
  actuation; axis motion; Mandatory Safe Return actuation; TEST_HARDWARE; PRODUCTION device
  access; ZIP; Release; Deployment.

---

## 1. Recovery and base verification

Treated Arena sandbox recreation as a normal event and verified before working:

| Check | Required | Observed | Result |
| --- | --- | --- | --- |
| Remote main SHA | exactly `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` | `git ls-remote origin refs/heads/main` → `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` | MATCH |
| Local HEAD | equal to remote main | `git rev-parse HEAD` → `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` | MATCH |
| Branch | established from approved remote-main SHA | `arena/ca9c94c4-waterjet-sentinel-suite`, parent = approved SHA | OK |
| Working Tree | clean | `git status --porcelain` → empty (0 entries) | CLEAN |
| Ancestry | approved SHA is ancestor of HEAD | `git merge-base --is-ancestor` → true | OK |
| PR #5 foundation | present in history | HEAD **is** merge commit `a74db62` "Merge pull request #5 from tnoiiee/arena/873f0015-waterjet-sentinel-suite" | PRESENT |
| Tree identity | Working Tree = approved remote tree | `git diff <approved-SHA> HEAD` → empty; path/blob/mode identity complete | IDENTICAL |

No recovery protocol was needed: no stale refs, no working-tree divergence, no ambiguity.
No reset / clean / restore / rebase / force-push / working-tree checkout / stash was used.

## 2. Corrected sensor topology (supersedes the Cannon-slot interpretation)

The previous interpretation of I7 and I16 as Cannon or Water Jet slots is **superseded**.
Canonical baseline (protected):

| Item | Value |
| --- | --- |
| Logical grid positions | 108 (18 columns × 6 rows) |
| Actual Sensors | 106 |
| Thermocouple channels | 212 (2 per Sensor: TC_F / TC_R) |
| Left Sensors | 24 |
| Rear Sensors | 29 |
| Right Sensors | 24 |
| Front Sensors | 29 |

**I7 and I16 are NON_SENSOR_GAP positions:**

- not a Sensor, not a Water Jet, not any equipment entity;
- **I7**: sensor sequence skips it — I6 → I8; physical placement anchor for **WJ3**;
- **I16**: sensor sequence skips it — I15 → I17; physical placement anchor for **WJ1**;
- **Do not represent WJ1 as I16. Do not represent WJ3 as I7.** They are location anchors
  only.

Wall mapping (unchanged, `[OWNER CONFIRMED]`): columns 1–4 LEFT, 5–9 REAR, 10–13 RIGHT,
14–18 FRONT; rows top-to-bottom 1 `G+2xx`, 2 `G+1xx`, 3 `G`, 4 `H`, 5 `I`, 6 `J`.
Regions derived from the approved topology: **UPPER = rows 1–2, LOWER = rows 3–6.**

Per-wall logical/sensor arithmetic: LEFT 4×6=24 (24 sensors); REAR 5×6=30 minus I7 = 29;
RIGHT 4×6=24 (24); FRONT 5×6=30 minus I16 = 29. Total logical 108; total sensors 106.

## 3. Water Jet topology table (installed positions and target coverage)

Installed position and cleaning target coverage are **separate concepts**. A Water Jet is
installed on one wall and sprays the **opposite** wall (`FRONT↔REAR`, `LEFT↔RIGHT`).

| WJ | Installed wall | Installed region | Placement kind | Placement anchors (logical ids) | Target wall | Target region | Valve |
| --- | --- | --- | --- | --- | --- | --- | --- |
| WJ1 | FRONT | LOWER | NON_SENSOR_GAP | gap anchor **I16**; neighbours I15, I17 | REAR | LOWER | IV1 |
| WJ2 | LEFT | LOWER | BETWEEN_HORIZONTAL | J2, J3 | RIGHT | LOWER | IV2 |
| WJ3 | REAR | LOWER | NON_SENSOR_GAP | gap anchor **I7**; neighbours I6, I8 | FRONT | LOWER | IV3 |
| WJ4 | RIGHT | LOWER | BETWEEN_HORIZONTAL | J11, J12 | LEFT | LOWER | IV4 |
| WJ5 | FRONT | UPPER | BETWEEN_VERTICAL | G+216, G+116 | REAR | UPPER | IV5 |
| WJ6 | LEFT | UPPER | JUNCTION | vertical G+102–G+202; horizontal G+202–G+203 | RIGHT | UPPER | IV6 |
| WJ7 | REAR | UPPER | BETWEEN_VERTICAL | G+207, G+107 | FRONT | UPPER | IV7 |
| WJ8 | RIGHT | UPPER | JUNCTION | vertical G+111–G+211; horizontal G+211–G+212 | LEFT | UPPER | IV8 |

Anchor-consistency proof (each anchor resolves inside the installed wall and region of its
WJ, against the `[OWNER CONFIRMED]` grid):

| WJ | Anchor resolution | Installed wall/region check |
| --- | --- | --- |
| WJ1 | I16 = row 5, col 16 (FRONT wall col 3); I15 = r5 c15; I17 = r5 c17 | FRONT, row 5 ⇒ LOWER ✓ |
| WJ2 | J2 = row 6, col 2; J3 = row 6, col 3 (LEFT) | LEFT, row 6 ⇒ LOWER ✓ |
| WJ3 | I7 = row 5, col 7 (REAR wall col 3); I6 = r5 c6; I8 = r5 c8 | REAR, row 5 ⇒ LOWER ✓ |
| WJ4 | J11 = row 6, col 11; J12 = row 6, col 12 (RIGHT) | RIGHT, row 6 ⇒ LOWER ✓ |
| WJ5 | G+216 = row 1, col 16; G+116 = row 2, col 16 (FRONT) | FRONT, rows 1–2 ⇒ UPPER ✓ |
| WJ6 | G+102 = r2 c2; G+202 = r1 c2; G+203 = r1 c3 (LEFT) | LEFT, rows 1–2 ⇒ UPPER ✓ |
| WJ7 | G+207 = row 1, col 7; G+107 = row 2, col 7 (REAR) | REAR, rows 1–2 ⇒ UPPER ✓ |
| WJ8 | G+111 = r2 c11; G+211 = r1 c11; G+212 = r1 c12 (RIGHT) | RIGHT, rows 1–2 ⇒ UPPER ✓ |

Every placement kind is geometrically consistent: NON_SENSOR_GAP = mounted at the gap
position; BETWEEN_HORIZONTAL = between two horizontally adjacent sensors; BETWEEN_VERTICAL
= between two vertically adjacent sensors; JUNCTION = at the shared endpoint of one
vertical and one horizontal anchor pair (G+202 for WJ6; G+211 for WJ8).

## 4. WJ-to-IV pairing table

Mandatory **one-to-one, ordinal-matched** pairing. No Water Jet may lack a Valve; no Valve
may be paired with more than one Water Jet; no Valve may exist without its Water Jet.

| Pair | Pair | Pair | Pair |
| --- | --- | --- | --- |
| WJ1 ↔ IV1 | WJ2 ↔ IV2 | WJ3 ↔ IV3 | WJ4 ↔ IV4 |
| WJ5 ↔ IV5 | WJ6 ↔ IV6 | WJ7 ↔ IV7 | WJ8 ↔ IV8 |

Derived invariants: exactly 8 Water Jets; exactly 8 Isolation Valves; the `assignedIsolationValveId`
of any Sensor is **derived** from its `assignedWaterJetId` through this pairing and is
never independently assigned.

## 5. Target-coverage model (derived expectations)

Target coverage = the set of actual Sensors whose **wall and region** equal the assigned
Water Jet's target wall and region. The counts below are arithmetic consequences of the
approved topology and the `[OWNER CONFIRMED]` grid; per-sensor membership is validated
against `sensorparam.csv` at Checkpoint B import (both the wall/region test and the legacy
`cannon` test must agree — disagreement fails closed).

| WJ | Target wall/region | Expected sensor count (derived) |
| --- | --- | --- |
| WJ1 | REAR LOWER | 19 (5 cols × 4 rows − I7) |
| WJ2 | RIGHT LOWER | 16 (4 × 4) |
| WJ3 | FRONT LOWER | 19 (5 × 4 − I16) |
| WJ4 | LEFT LOWER | 16 (4 × 4) |
| WJ5 | REAR UPPER | 10 (5 × 2) |
| WJ6 | RIGHT UPPER | 8 (4 × 2) |
| WJ7 | FRONT UPPER | 10 (5 × 2) |
| WJ8 | LEFT UPPER | 8 (4 × 2) |
| **Total** | | **106** |

## 6. Legacy migration table (cannon → Water Jet)

Legacy column **`cannon` = Assigned Cleaning Device ID**. It is NOT a sensor-grid equipment
position, NOT an installed wall, NOT the nearest Water Jet, NOT "the Water Jet on the same
wall as the Sensor". Canonical direct mapping — **never remapped by wall**:

| Legacy `cannon` | `assignedWaterJetId` | Paired valve | Installed at | Sprays |
| --- | --- | --- | --- | --- |
| 1 | WJ1 | IV1 | FRONT LOWER | REAR LOWER |
| 2 | WJ2 | IV2 | LEFT LOWER | RIGHT LOWER |
| 3 | WJ3 | IV3 | REAR LOWER | FRONT LOWER |
| 4 | WJ4 | IV4 | RIGHT LOWER | LEFT LOWER |
| 5 | WJ5 | IV5 | FRONT UPPER | REAR UPPER |
| 6 | WJ6 | IV6 | LEFT UPPER | RIGHT UPPER |
| 7 | WJ7 | IV7 | REAR UPPER | FRONT UPPER |
| 8 | WJ8 | IV8 | RIGHT UPPER | LEFT UPPER |

Worked example (Owner-provided): Sensors in **Rear lower** may carry legacy `cannon = 1`.
That means assigned Water Jet = **WJ1**; WJ1 is installed at **Front lower**; WJ1 sprays
across to **Rear lower**; paired Isolation Valve = **IV1**. The wall the sensor sits on is
the *target* wall, never the installed wall.

## 7. CSV field-classification matrix

Source: Owner-provided `sensorparam.csv` — 108 logical-position rows; unique `id` values;
unique `order_total` values 0–107; 106 actual Sensor rows after excluding I7 and I16; wall
totals after exclusion 24 / 29 / 24 / 29; legacy `cannon` values 1–8; I7 and I16 carry zero
dirty-score min/max values (placeholder-like logical-position records, not runtime
Sensors). The file is Owner-provided working data containing **non-public acquisition values**: it
must **not be committed** and must not be copied into tests, fixtures, docs, or config
examples; no real IP, register base, channel binding, timestamp, or plant value from the
Owner CSV may enter Git. Only structure-only, synthetic-valued examples enter
`config/examples/`; Checkpoint B tests use clearly synthetic public-safe rows.

**Row-level rule:** I7 and I16 rows take disposition **REJECT FOR I7/I16** — excluded from
any SensorConfiguration import, retained only as NON_SENSOR_GAP LogicalPosition records
with their placeholder values preserved as gap provenance (no silent normalization).

| # | Field | Class | Legacy meaning | Canonical target | Disposition | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `id` | A. Identity and layout | legacy **database record identifier** | `legacyRecordId` provenance only | IMPORT (108 rows) | preserved verbatim as record provenance; **never** a logical-position identity and **never** a Sensor ID; duplicate record ids fail closed (`TOPO_DUPLICATE_ID`). I7/I16 rows: REJECT FOR I7/I16 as sensor data (the rows still supply the NON_SENSOR_GAP LogicalPosition records) |
| 2 | `sensorname` | A | legacy logical-position label | canonical `LogicalPosition.logicalId`; for SENSOR rows also canonical `SensorConfiguration.sensorId` | IMPORT (fail-closed validation) | must equal the logical label derived from the `orderTotal` row/column via the fixed 18-column matrix; mismatch refuses (`MIGRATION_LOGICAL_LABEL_MISMATCH`). I7/I16 `sensorname` values remain the `logicalId` values of NON_SENSOR_GAP positions and never create a SensorConfiguration |
| 3 | `wall` | A | wall token | `Wall` enum (`LEFT/REAR/RIGHT/FRONT`) | IMPORT WITH NORMALIZATION | token→enum mapping recorded as a normalization action; unrecognized token fails closed (`MIGRATION_FIELD_TOKEN`) |
| 4 | `order_wall` | A | wall-local scan order | `orderWall` | IMPORT | preserved as authoritative wall-local ordinal |
| 5 | `order_total` | A | legacy logical-position order — zero-based, range 0–107, **includes** the NON_SENSOR_GAP rows I7 and I16 | `LogicalPosition.orderTotal` (provenance/layout ordering) | IMPORT | unique 0–107; duplicates or out-of-range fail closed (`TOPO_DUPLICATE_ORDER`). **NOT the Sensor scanOrder**: canonical `scanOrder` is derived afterwards — sort all logical positions by `orderTotal`, exclude NON_SENSOR_GAP, assign the dense one-based range 1–106 to the 106 actual Sensors (see §8) |
| 6 | `cannon` | A | **Assigned Cleaning Device ID** | `assignedWaterJetId` (`cannon n → WJn`) | IMPORT WITH NORMALIZATION | semantic rename only; **no value remap**. Outside 1–8 fails closed (`MIGRATION_CANNON_RANGE`); wall/region contradiction with approved topology fails closed (`TOPO_TARGET_WALL_CONTRADICTION`) |
| 7 | `max_temp_dirtyscore` | B. Dirty-score settings | dirty-score mapping upper bound | `DiffUpperBound` | IMPORT WITH NORMALIZATION | canonical rename recorded. Zero/degenerate on an **actual Sensor** → PRESERVE WITH WARNING (`MIGRATION_PLACEHOLDER_VALUE`), Owner review; on I7/I16 → REJECT FOR I7/I16 |
| 8 | `min_temp_dirtyscore` | B | dirty-score mapping lower bound | `DiffLowerBound` | IMPORT WITH NORMALIZATION | as above; `min ≥ max` on an actual Sensor fails closed (`MIGRATION_BOUNDS_ORDER`) |
| 9 | `threshold_setpoint` | B | classification threshold setpoint | `DirtyScoreThreshold` (direct recorded rename of the legacy numeric value) | IMPORT | zero/placeholder on an actual Sensor → PRESERVE WITH WARNING (`MIGRATION_PLACEHOLDER_VALUE`). **`UseDirtyScoreThreshold` is NOT derived** — deferred to a later explicit Owner decision |
| 10 | `cleaning_count` | C. Cleaning eligibility and history | historical cleaning count | `cleaningCount` (raw copy) | IMPORT | preserved as-is; **`HasVerifiedCleaningHistory` is NOT derived** — deferred to a later explicit Owner decision |
| 11 | `sensor_enable` | C | participation flag | `Enabled` | IMPORT WITH NORMALIZATION | boolean tokens canonicalized; unrecognized token fails closed (`MIGRATION_FIELD_TOKEN`) |
| 12 | `lastclean_timestamp` | C | last cleaning time — **offset-free string; source timezone unconfirmed** | raw preservation as a deferred legacy field (keyed by `legacyRecordId`) | PRESERVE WITH WARNING / DEFER domain conversion | raw string preserved verbatim; syntax parse advisory-only (failure = warning; raw still preserved); **source timezone UNKNOWN — no UTC conversion** (`MIGRATION_TIMEZONE_UNKNOWN` warning); **no `LastSuccessfulCleaningCompletedAt` is created in Checkpoint B**; never consumed by Queue eligibility or cleaning behaviour (W3) |
| 13 | `min_time_allowaddtoqueue` | C | minimum re-queue interval — **unit unconfirmed** | raw preservation as a deferred legacy field | PRESERVE WITH WARNING / DEFER canonical interval conversion | raw value retained; **unit UNKNOWN** (`MIGRATION_UNIT_UNVERIFIED`); **no `HardMinimumCleaningInterval` mapping and no TimeSpan/duration invented**; never consumed by Queue eligibility or Runtime behaviour (W4) |
| 14 | `max_time_allowaddtoqueue` | C | (no canonical attribute yet) | retained raw in legacy record | DEFER | Owner mapping decision required (W6); never silently mapped to any queue rule |
| 15 | `max_time_enable` | C | (no canonical attribute yet) | retained raw in legacy record | DEFER | Owner mapping decision required (W6) |
| 16 | `ip_modbus` | D. Acquisition binding | acquisition endpoint | raw retention only | DEFER | **not approved Production-device configuration**; never used to contact or address any device; `[NOT VERIFIED]` |
| 17 | `channel_pair` | D | TC channel binding | raw retention only | DEFER | as above |
| 18 | `base_modbus_address` | D | register base | raw retention only | DEFER | as above |
| 19 | `latest_updateparams_timestamp` | E. Audit metadata | parameter-update audit stamp | parameter-audit provenance | IMPORT | provenance only; **never** consumed as a cleaning-history timestamp |

Import is atomic and deterministic: identical CSV bytes ⇒ identical result; any fail-closed
refusal aborts the entire import (no partial import, no partial revision publication).

## 8. I7/I16 handling (exclusion rules)

1. I7 and I16 exist **only** as NON_SENSOR_GAP `LogicalPosition` records (108-row layout
   preserved).
2. They produce **no** `SensorConfiguration`, no Thermocouple channels, no Dirty Score,
   no classification, no quality, no queue entry, no selection, no Cleaning Job target, no
   alarm, and no coverage membership.
3. Sensor sequence skips them: I6 → I8 and I15 → I17. They carry only their legacy
   `orderTotal` positions in the zero-based 0–107 logical ordering; they receive **no**
   Sensor `scanOrder`.
3a. `scanOrder` derivation (deterministic, performed after import of the 108 layout
   rows): sort all logical positions by `orderTotal`, exclude `positionKind =
   NON_SENSOR_GAP`, assign the dense one-based range **1–106** to the 106 actual Sensors —
   gapless, no duplicates, sequence skipping I7 and I16 (e.g. the Sensors at I6 and I8
   receive consecutive scanOrder values).
4. I7 is the physical placement anchor of WJ3; I16 is the physical placement anchor of WJ1.
   The Water Jets are separate `WaterJetConfiguration` records — never identified *as*
   I7/I16.
5. Their CSV rows take disposition REJECT FOR I7/I16; zero dirty-score values are preserved
   as gap provenance, never normalized into sensor defaults.
6. No Water Jet or Isolation Valve identity may appear as a Sensor identity, and vice
   versa; the id namespaces are disjoint.
7. Their `sensorname` values remain the `logicalId` values of the NON_SENSOR_GAP positions
   (validated by the same label rule as every row) and never create a
   `SensorConfiguration` or a `scanOrder`.

## 9. Validation and refusal-machine-code proposal

Proposed additive config-validation refusal family (no existing code changed; final home
per Checkpoint B):

| Code | Fires when |
| --- | --- |
| `TOPO_POSITION_COUNT` | logical positions ≠ 108 |
| `TOPO_SENSOR_COUNT` | sensor records ≠ 106 |
| `TOPO_CHANNEL_COUNT` | structural TC total ≠ 212 (106 Sensors × exactly two expected TC channel sides); structural invariant only — no Production channel identity or binding is created |
| `TOPO_WALL_COUNTS` | wall totals ≠ Left 24 / Rear 29 / Right 24 / Front 29 |
| `TOPO_GAP_IDENTITY` | I7 or I16 present as a Sensor, missing as a gap, or gap-anchor binding ≠ (I7→WJ3, I16→WJ1) |
| `TOPO_DUPLICATE_ID` | legacy record identifier `id` not unique |
| `TOPO_DUPLICATE_ORDER` | `order_total` not unique or outside 0–107 |
| `TOPO_WATERJET_COUNT` | Water Jet records ≠ 8 |
| `TOPO_VALVE_COUNT` | Isolation Valve records ≠ 8 |
| `TOPO_PAIRING_MISMATCH` | WJ↔IV pairing not exactly one-to-one with matching ordinals |
| `TOPO_TARGET_WALL_CONTRADICTION` | a Sensor's `cannon`-assigned WJ has a target wall/region ≠ the Sensor's wall/region (incl. cross-region mismatch) |
| `MIGRATION_CANNON_RANGE` | `cannon` outside 1–8 |
| `MIGRATION_MISSING_ASSIGNMENT` | an actual Sensor has no assigned Water Jet |
| `MIGRATION_LOGICAL_LABEL_MISMATCH` | legacy `sensorname` ≠ the logical label derived from the `orderTotal` row/column via the fixed 18-column matrix |
| `MIGRATION_FIELD_TOKEN` | unparseable enum / boolean token (`wall`, `sensor_enable`) |
| `MIGRATION_BOUNDS_ORDER` | `min ≥ max` dirty-score bounds on an actual Sensor |
| `MIGRATION_PLACEHOLDER_VALUE` | zero/degenerate bounds or threshold on an actual Sensor; sentinel/zero timestamp values (preserved with warning; listed for Owner review) |
| `MIGRATION_UNIT_UNVERIFIED` | time-field unit UNKNOWN — raw value preserved, no conversion, no TimeSpan invented (warning-level) |
| `MIGRATION_TIMEZONE_UNKNOWN` | offset-free timestamp imported: source timezone UNKNOWN, raw string preserved, **no UTC conversion** (warning-level) |

**Fail-closed policy:** any structural code (`TOPO_*`, `MIGRATION_CANNON_RANGE`,
`MIGRATION_MISSING_ASSIGNMENT`, `MIGRATION_LOGICAL_LABEL_MISMATCH`,
`MIGRATION_FIELD_TOKEN`, `MIGRATION_BOUNDS_ORDER`) aborts
the entire import. `MIGRATION_PLACEHOLDER_VALUE`, `MIGRATION_UNIT_UNVERIFIED`, and
`MIGRATION_TIMEZONE_UNKNOWN` are warning-level: the value imports
verbatim with recorded warnings and Owner-review entries — never silent normalization,
never invented defaults. Identical input ⇒ identical output (determinism).

## 10. Migration warnings and Owner-review list

| # | Warning / review item | Disposition |
| --- | --- | --- |
| W1 | Superseded "Cannon slot" wording still present in: `REQUIREMENTS.md` PHY-001, `DOMAIN_MODEL.md` §2.2.1, `STAGE_0.3A_PLAN.md` §9 note, and Product identifiers/comments (`SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`, `CANNON_REAR`/`CANNON_FRONT`, simulator-map comments, `sensor-map.example.json`) | Recorded as **TRANSITIONAL LEGACY RUNTIME REPRESENTATION — SEMANTICALLY SUPERSEDED — RUNTIME MIGRATION DEFERRED TO CHECKPOINT C** (NOT AUTHORIZED); doc propagation and the atomic Runtime/wire rename are Checkpoint C scope. Checkpoint B adds `LogicalPositionKind` only and never renames the wire surface. Behaviour (two sensorless positions excluded from sensor runtime/queues/jobs) is unchanged |
| W2 | Degenerate/zero dirty-score bounds or thresholds on actual Sensor rows in the Owner CSV | PRESERVE WITH WARNING at import; Owner review before values are trusted |
| W3 | `lastclean_timestamp`: offset-free values, UNKNOWN source timezone, sentinel/zero values of unconfirmed meaning, and unconfirmed validity/history semantics | PRESERVE WITH WARNING / DEFER domain conversion; raw string kept, **no UTC conversion**; **Owner review required for: (a) source timezone, (b) sentinel timestamp meaning, (c) validity/history semantics**; never consumed by queue or cleaning behaviour |
| W4 | Unit metadata UNKNOWN for `min_time_allowaddtoqueue` (and the DEFER time fields) | Raw value retained, unit UNKNOWN; **no conversion, no TimeSpan or duration invented, no `HardMinimumCleaningInterval` mapping**; never consumed by Queue eligibility or Runtime |
| W5 | Legacy `cannon` column name and the transitional runtime CANNON vocabulary embed superseded semantics | Canonical model uses WJ / `LogicalPositionKind` vocabulary; the legacy `cannon` column name survives as provenance only; importer output emits NON_SENSOR_GAP, **never CANNON**, with no Cannon↔NON_SENSOR_GAP alias; runtime CANNON migration deferred to Checkpoint C |
| W6 | `max_time_allowaddtoqueue`, `max_time_enable` have no canonical attribute | DEFER; Owner mapping decision required before any queue semantics may use them |
| W7 | `ip_modbus` / `channel_pair` / `base_modbus_address` are not approved Production-device configuration | DEFER; `[NOT VERIFIED]`; no device is ever addressed with them in any authorized stage |

## 11. Checkpoint B expected change set (planned — NOT started, NOT authorized)

| File | Change | Content |
| --- | --- | --- |
| `packages/contracts/Topology.cs` | NEW | `LogicalPositionRecord` (incl. `legacyRecordId` provenance and `logicalId`), `WaterJetConfiguration`, `IsolationValveConfiguration` (+ canonical `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` vocabulary) per ADR-0017 §1, §3, §4 |
| `packages/contracts/Config.cs` | EXTEND | configuration-schema records for the 0.3A-3 config pipeline: `SensorConfigurationRecord`, migration result/warning records, disposition enum |
| `packages/contracts/Enums.cs` | EXTEND | canonical `LogicalPositionKind` (`SENSOR`, `NON_SENSOR_GAP`) **only**; `SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`, `CANNON_REAR`/`CANNON_FRONT` and the rest of the Runtime/Snapshot wire surface are **NOT renamed or altered** (transitional legacy representation; atomic migration is Checkpoint C scope, NOT AUTHORIZED) |
| `packages/domain/` | NEW files | `SensorParameterCsvImporter` + `TopologyValidator` (+ refusal-code table from §9); deterministic, atomic, fail-closed. Planned importer behaviour: preserve `id` only as `legacyRecordId`; take `logicalId`/`sensorId` from `sensorname` validated against the label derived from `orderTotal` row/column (`MIGRATION_LOGICAL_LABEL_MISMATCH`); preserve `order_total` as `orderTotal` provenance over all 108 LogicalPositions (0–107, including I7/I16), then derive Sensor `scanOrder` deterministically — sort by `orderTotal`, exclude NON_SENSOR_GAP, assign dense one-based 1–106; I7/I16 receive no SensorConfiguration and no scanOrder; emit `NON_SENSOR_GAP`, never CANNON; preserve offset-free timestamps raw (timezone UNKNOWN, no UTC conversion) and unknown-unit durations raw (no TimeSpan); derive no `UseDirtyScoreThreshold`, `HasVerifiedCleaningHistory`, `LastSuccessfulCleaningCompletedAt`, or `HardMinimumCleaningInterval` |
| `config/examples/` | NEW | migrated sensor-parameter example — structure only, synthetic values, no Production values (boundary S3) |
| `tests/config.tests/` | NEW | invariant tests per §12 |
| `REQUIREMENTS.md`, `DOMAIN_MODEL.md`, `STAGE_0.3A_PLAN.md` | DOC | propagate the corrected NON_SENSOR_GAP wording (W1) |
| `README.md`, `CURRENT_STATE.md`, `CHANGELOG.md` | DOC | stage-status rows updated at Owner acceptance, per repo convention |

**Explicitly excluded from Checkpoint B:** fixtures, `packages.lock.json`, Inspector,
runtime write/command paths, device addressing, simulator synthetic *values*, the React
spike. No behaviour beyond configuration loading; nothing device-facing.

**Additional Checkpoint B boundaries (second Owner review):**

- **TC channel scope:** enforcement is structural only — 106 Sensors, exactly two expected
  TC channel sides per Sensor, structural total 212. No Production channel identity,
  Modbus address, or physical binding is created or claimed; `ip_modbus`,
  `channel_pair`, `base_modbus_address` stay deferred; existing `SYN-TC-*` identities
  remain synthetic runtime data and are not Production migration evidence.
- **Vocabulary scope:** canonical importer output uses `NON_SENSOR_GAP`, never
  `SlotType.CANNON`; no alias permits Cannon and NON_SENSOR_GAP interchangeably in
  canonical topology; the existing Runtime CANNON wire vocabulary is untouched by
  Checkpoint B (Checkpoint C defers).
- **Public repository boundary:** `sensorparam.csv` is never committed or copied into
  tests, fixtures, docs, or config examples; no real IP, register base, channel binding,
  timestamp, or plant value from the Owner CSV enters Git; tests use clearly synthetic
  public-safe rows (boundary S3).

## 12. Checkpoint B test proposals (contract/topology risks only — no tests in Checkpoint A)

| # | Proposed test | Risk covered |
| --- | --- | --- |
| T1 | 108 logical positions / 106 Sensors / 212 TC channels / 24-29-24-29 wall totals on a migrated configuration | count regressions |
| T2 | I7 and I16 appear only as NON_SENSOR_GAP LogicalPositions; no SensorConfiguration exists for them | gap leakage into runtime |
| T3 | imported Sensor `scanOrder` is exactly the dense one-based range 1–106 (no gaps, no duplicates); the sequence skips I7 and I16; `orderTotal` remains 0–107 over all 108 LogicalPositions; no NON_SENSOR_GAP row receives a scanOrder | ordering conflated with legacy layout order; scanOrder derived from the wrong field |
| T4 | exactly 8 Water Jets; exactly 8 Isolation Valves | identity-set drift |
| T5 | WJn paired with IVn exactly, ordinals matching, one-to-one both directions | pairing corruption |
| T6 | every WJ's target wall is the opposite wall of its installed wall; target wall/region equal the approved table (§3) | coverage inferred from the wrong wall |
| T7 | legacy `cannon n` maps directly to `WJn` (including Rear-lower `cannon = 1` → WJ1 / FRONT-lower installation) | wall-based remapping |
| T8 | every actual Sensor carries exactly one assigned Water Jet resolving to a real WJ; paired IV derived | dangling/missing assignment |
| T9 | no Water Jet or Valve identity appears as a Sensor id; namespace disjointness | identity collision |
| T10 | importer runs twice on identical bytes ⇒ identical results (records, warnings, refusal codes) | nondeterministic import |
| T11 | each refusal code from §9 fires on its malformed fixture and aborts atomically with no partial import | ambiguous topology not failing closed |
| T12 | legacy `id` is preserved only as `legacyRecordId` provenance — never used as a logical-position identity and never as a Sensor ID | identity provenance corruption |
| T13 | legacy `sensorname` becomes `LogicalPosition.logicalId` and, for SENSOR rows, `SensorConfiguration.sensorId`; I7/I16 `sensorname` values remain NON_SENSOR_GAP `logicalId` values with no SensorConfiguration | identity provenance corruption |
| T14 | `sensorname` must agree with the logical label derived from `orderTotal` (row/column via the fixed 18-column matrix); a mismatched fixture refuses with `MIGRATION_LOGICAL_LABEL_MISMATCH` | undetected label/position disagreement |
| T15 | offset-free `lastclean_timestamp` values remain raw strings with timezone UNKNOWN — never converted to UTC, no `LastSuccessfulCleaningCompletedAt` produced, never consumed by queue or cleaning behaviour | unapproved time interpretation |
| T16 | unknown-unit durations (`min_time_allowaddtoqueue`) remain raw values with unit UNKNOWN — no TimeSpan/duration invented, no `HardMinimumCleaningInterval` produced, never consumed by Queue eligibility or Runtime | unapproved duration invention |
| T17 | no unapproved domain booleans are derived: no `UseDirtyScoreThreshold` from `threshold_setpoint`, no `HasVerifiedCleaningHistory` from `cleaning_count` | silent domain derivation |
| T18 | no Production acquisition binding is created: `ip_modbus`, `channel_pair`, `base_modbus_address` never materialize as device identities, addresses, or bindings; no Owner-CSV value enters fixtures | acquisition-scope violation |
| T19 | canonical importer output uses `NON_SENSOR_GAP` and never emits `SlotType.CANNON`; no Cannon↔NON_SENSOR_GAP alias exists in canonical topology | transitional vocabulary leakage |
| T20 | the existing Runtime/Snapshot CANNON vocabulary (`SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`, `CANNON_REAR`/`CANNON_FRONT`) is byte-identical before and after Checkpoint B — migration remains deferred to Checkpoint C | unauthorized wire-surface rename |

No tests for syntax, wording, whitespace, private helpers, or tests of tests.

## 13. Protected baseline and explicit exclusions

Protected (do not regress): 108 logical positions; 106 Sensors; 212 Thermocouple channels;
Left 24 / Rear 29 / Right 24 / Front 29; I7 and I16 are NON_SENSOR_GAP; WJ1–WJ8
identities; IV1–IV8 identities; WJn↔IVn one-to-one pairing; installed position separate
from target coverage; legacy `cannon n → WJn` direct mapping; Single Active Job
architecture; GlobalQueue head-only architecture; Pump high-critical semantics; Mandatory
Safe Return ordering; `activeJob` three-state wire semantics; SIMULATOR-only runtime
boundary; no command or write path; the historical React spike remains untouched.

Explicit exclusions honored in this checkpoint: no Product source, contract, fixture,
lock, test, or Inspector file modified; no hardware access; no Production device access;
no queue dispatch, cleaning execution, pump commands, valve actuation, axis motion, or
Mandatory Safe Return actuation; no ZIP / Release / Deployment; no invented Production
equipment parameters (all non-Owner values remain `[NOT VERIFIED]` / `[OPEN]`).

## 14. Delivery record

| Item | Value |
| --- | --- |
| Verified base SHA | `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` (remote `main`, verified via `git ls-remote`) |
| New branch | `arena/ca9c94c4-waterjet-sentinel-suite` (from the approved base) |
| Commit 1 (specification) | `c994c26f621b49b01a1e437f8207651add126035` — `docs(topology): define Water Jet and legacy parameter migration` |
| Commit 2 (delivery record) | `03c2d15493afd713b9dde2adf3e4ff78ba2ffef6` — `docs(topology): record Checkpoint A PR number and delivery record` |
| Commit 3 (SHA correction) | `6648437f24e918d8be3a1c4ad27edb8c1bc4b0a3` — `docs(topology): correct specification commit SHA in Checkpoint A delivery record` |
| PR head at Owner review | `6648437f24e918d8be3a1c4ad27edb8c1bc4b0a3` — the head on which the Checkpoint A Owner review returned CHANGES REQUESTED |
| Commit 4 (review correction) | `3dac1a0c6c6ca9ab14bccee2b24fe15010292e5c` — `docs(topology): correct migration ordering and acceptance semantics` |
| PR head at second Owner review | `3dac1a0c6c6ca9ab14bccee2b24fe15010292e5c` — the head on which the second Owner review returned CHANGES REQUESTED |
| Commit 5 (second review correction) | `docs(topology): correct legacy field semantics and deferred mappings` — created by the second Owner-review correction task (2026-10-08); **this commit becomes the new PR head** (authoritative value in the PR #6 header) |
| Changed | `docs/decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md` (NEW); `docs/STAGE_0.3A-3_CHECKPOINT_A.md` (NEW); `docs/decisions/README.md` (index row); `CHANGELOG.md` (Unreleased entry + review-correction note) |
| Unchanged | All Product source, contracts, fixtures, locks, tests, Inspector, simulator, config examples, README/CURRENT_STATE stage rows, the React spike |
| Not Verified | Per the status classification in §15: Production acquisition bindings; legacy time-field units; sentinel timestamp meaning; runtime importer execution; physical device integration. The canonical models are specified, not implemented; no .NET, no importer, no device, no runtime was executed or exercised here |
| PR | **[#6](https://github.com/tnoiiee/waterjet-sentinel-suite/pull/6)** — created from this branch against `main` — **OPEN, NOT MERGED** |

## 15. Owner review correction record (2026-10-08 — CHANGES REQUESTED on PR #6)

### Correction 1 — order_total vs scanOrder

The original specification stated or implied that Sensor `scanOrder` is taken from legacy
`order_total`. That was **incorrect** and is corrected:

| | Rule |
| --- | --- |
| Old (incorrect) | `scanOrder` taken from / based on legacy `order_total` |
| New (corrected) | `order_total` is the **legacy logical-position order** — zero-based, range 0–107, **including** the NON_SENSOR_GAP rows I7 and I16 — preserved as `orderTotal` provenance/layout ordering only. Canonical Sensor `scanOrder` applies **only** to the 106 actual Sensors, is **one-based** and **gapless** (range 1–106), and is **derived deterministically**: (1) preserve `order_total` as provenance/layout ordering; (2) sort all logical positions by `orderTotal`; (3) exclude `positionKind = NON_SENSOR_GAP`; (4) assign dense `scanOrder` 1…106; (5) I7 and I16 receive no SensorConfiguration and no scanOrder. |

Updated in: ADR-0017 (§1, §2, §10); this report (§7 matrix row 5, §8); Checkpoint B planned
importer behaviour (§11); Checkpoint B proposed test list (new T3, §12). The importer is
**not implemented** in this correction.

### Correction 2 — acceptance vs merge

| | Wording |
| --- | --- |
| Old (incorrect) | "formal acceptance happens at PR merge" (ADR-0017 header) |
| New (required governance) | Formal acceptance requires an explicit Owner decision. PR merge records repository integration and does not by itself constitute acceptance unless the Owner explicitly states acceptance as part of the merge decision. ADR-0017 status remains `PROPOSED` until explicit Owner acceptance. |

### Status classification

| Classification | Items |
| --- | --- |
| **OWNER CONFIRMED** | 108 logical positions; 106 Sensors; 212 TC channels; I7 and I16 are NON_SENSOR_GAP; WJ1–WJ8 installed positions; WJ1–WJ8 target coverage; WJn paired one-to-one with IVn; legacy `cannon n` maps directly to WJn |
| **NOT VERIFIED** | Production acquisition bindings; legacy time-field units; sentinel timestamp meaning; runtime importer execution; physical device integration |

### Second Owner review correction (2026-10-08 — CHANGES REQUESTED on PR #6, head `3dac1a0c`)

Documentation-only correction. Old → new, by topic:

| Topic | Old (incorrect) | New (corrected) |
| --- | --- | --- |
| `id` mapping | `id` → `LogicalPosition.logicalId` | `id` is the legacy **database record identifier**, preserved only as `legacyRecordId` provenance — never a logical-position identity, never a Sensor ID |
| `sensorname` mapping | `sensorname` → display-only provenance (`legacyDisplayName`) | `sensorname` is the canonical `LogicalPosition.logicalId` and, for SENSOR rows, the canonical `SensorConfiguration.sensorId`; I7/I16 `sensorname` values remain the `logicalId` values of NON_SENSOR_GAP positions and never create SensorConfiguration |
| Logical-label validation | none | Derive logical row/column from `orderTotal` and the fixed 18-column matrix, derive the expected logical label, require `sensorname` to equal it; mismatch refuses with additive code `MIGRATION_LOGICAL_LABEL_MISMATCH` |
| `lastclean_timestamp` disposition | IMPORT WITH NORMALIZATION; legacy format → UTC ISO-8601; populates `LastSuccessfulCleaningCompletedAt` | PRESERVE WITH WARNING / DEFER domain conversion: raw string preserved, syntax parse advisory-only, **source timezone UNKNOWN, no UTC conversion**, **no `LastSuccessfulCleaningCompletedAt` created in Checkpoint B**, never consumed by queue or cleaning behaviour; Owner-review item added for source timezone, sentinel meaning, and validity/history semantics |
| `min_time_allowaddtoqueue` disposition | IMPORT WITH NORMALIZATION → `HardMinimumCleaningInterval` | PRESERVE WITH WARNING / DEFER canonical interval conversion: raw value retained, **unit UNKNOWN**, **no `HardMinimumCleaningInterval` mapping, no TimeSpan or duration invented**, never consumed by Queue eligibility or Runtime |
| Removed derivations | `UseDirtyScoreThreshold` from `threshold_setpoint`; `HasVerifiedCleaningHistory` from `cleaning_count`; `LastSuccessfulCleaningCompletedAt` from `lastclean_timestamp`; `HardMinimumCleaningInterval` from `min_time_allowaddtoqueue` | **All four derivations removed** from Checkpoint B; legacy values preserved as raw/imported/deferred fields with provenance and warnings; domain interpretation requires a later explicit Owner decision (`Enabled` from `sensor_enable` and the direct numeric renames remain approved) |
| Transitional runtime vocabulary | CANNON rename-vs-alias left as a Checkpoint B Owner-review item (W1) | Checkpoint B introduces `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` and must **NOT** rename/alter `SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`, `CANNON_REAR`/`CANNON_FRONT`; existing CANNON vocabulary recorded as TRANSITIONAL LEGACY RUNTIME REPRESENTATION — SEMANTICALLY SUPERSEDED — RUNTIME MIGRATION DEFERRED TO CHECKPOINT C; no canonical Cannon entity, importer never emits CANNON, no Cannon↔NON_SENSOR_GAP alias |
| TC channel scope | counts stated without scope qualification | Checkpoint B enforces only the structural invariant (106 Sensors × exactly two expected TC sides = 212); acquisition fields stay deferred; `SYN-TC-*` remains synthetic runtime data, not Production migration evidence |
| Public repository boundary | file noted as "not committed" | Strengthened: `sensorparam.csv` contains non-public acquisition values; must not be committed or copied into tests/fixtures/docs/config examples; no real IP, register base, channel binding, timestamp, or plant value may enter Git; Checkpoint B tests use clearly synthetic public-safe rows |

**Restoration note (found during this correction):** the first-review update to the
Checkpoint B planned importer behaviour (report §11 `packages/domain/` row, scanOrder
derivation text) was authored but did **not** persist into commit `3dac1a0c`; the committed
§11 row had reverted to the pre-review text while §15 of that commit claimed the update.
This commit restores that text (extended with the second-review semantics above), so §11
and this record now match the committed reality.

New proposed tests: T12–T20 (§12) cover id/sensorname semantics, label agreement, raw
timestamps, raw durations, no unapproved derivations, no acquisition bindings, NON_SENSOR_GAP
emission, and the deferred CANNON migration. No importer is implemented.

**FINAL STATUS**

- APPROVED MAIN BASE VERIFIED
- CORRECTED SENSOR TOPOLOGY RECORDED
- I7 AND I16 RECORDED AS NON_SENSOR_GAPS
- WJ1–WJ8 INSTALLED POSITIONS RECORDED
- WJ1–WJ8 TARGET COVERAGE RECORDED
- IV1–IV8 PAIRING RECORDED
- LEGACY CANNON DIRECT MAPPING RECORDED
- CSV MIGRATION SPECIFICATION RECORDED
- PRODUCT SOURCE UNCHANGED
- CHECKPOINT B IMPLEMENTATION NOT STARTED
- TEST_HARDWARE NOT AUTHORIZED
- PRODUCTION DEVICE ACCESS NOT AUTHORIZED
- PR OPEN — NOT MERGED
