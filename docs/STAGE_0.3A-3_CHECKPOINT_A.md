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
Sensors). The file is Owner-provided working data: it is **not committed** to this
repository; only structure-only, synthetic-valued examples enter `config/examples/`.

**Row-level rule:** I7 and I16 rows take disposition **REJECT FOR I7/I16** — excluded from
any SensorConfiguration import, retained only as NON_SENSOR_GAP LogicalPosition records
with their placeholder values preserved as gap provenance (no silent normalization).

| # | Field | Class | Legacy meaning | Canonical target | Disposition | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `id` | A. Identity and layout | legacy logical-position label | `LogicalPosition.logicalId` | IMPORT (108 rows) | unique; duplicates fail closed (`TOPO_DUPLICATE_ID`). I7/I16 rows: REJECT FOR I7/I16 |
| 2 | `sensorname` | A | legacy display name | provenance `legacyDisplayName` | IMPORT | PRESERVE WITH WARNING if it carries superseded "Cannon" equipment wording (W5); never used to derive identity |
| 3 | `wall` | A | wall token | `Wall` enum (`LEFT/REAR/RIGHT/FRONT`) | IMPORT WITH NORMALIZATION | token→enum mapping recorded as a normalization action; unrecognized token fails closed (`MIGRATION_FIELD_TOKEN`) |
| 4 | `order_wall` | A | wall-local scan order | `orderWall` | IMPORT | preserved as authoritative wall-local ordinal |
| 5 | `order_total` | A | global scan order | `scanOrder` basis | IMPORT | unique 0–107; duplicates or out-of-range fail closed (`TOPO_DUPLICATE_ORDER`) |
| 6 | `cannon` | A | **Assigned Cleaning Device ID** | `assignedWaterJetId` (`cannon n → WJn`) | IMPORT WITH NORMALIZATION | semantic rename only; **no value remap**. Outside 1–8 fails closed (`MIGRATION_CANNON_RANGE`); wall/region contradiction with approved topology fails closed (`TOPO_TARGET_WALL_CONTRADICTION`) |
| 7 | `max_temp_dirtyscore` | B. Dirty-score settings | dirty-score mapping upper bound | `DiffUpperBound` | IMPORT WITH NORMALIZATION | canonical rename recorded. Zero/degenerate on an **actual Sensor** → PRESERVE WITH WARNING (`MIGRATION_PLACEHOLDER_VALUE`), Owner review; on I7/I16 → REJECT FOR I7/I16 |
| 8 | `min_temp_dirtyscore` | B | dirty-score mapping lower bound | `DiffLowerBound` | IMPORT WITH NORMALIZATION | as above; `min ≥ max` on an actual Sensor fails closed (`MIGRATION_BOUNDS_ORDER`) |
| 9 | `threshold_setpoint` | B | classification threshold setpoint | `DirtyScoreThreshold` (+ `UseDirtyScoreThreshold` derivation, rule recorded) | IMPORT | zero/placeholder on an actual Sensor → PRESERVE WITH WARNING (`MIGRATION_PLACEHOLDER_VALUE`) |
| 10 | `cleaning_count` | C. Cleaning eligibility and history | historical cleaning count | `cleaningCount`; `HasVerifiedCleaningHistory` derivation (count > 0, rule recorded) | IMPORT | preserved as-is |
| 11 | `sensor_enable` | C | participation flag | `Enabled` | IMPORT WITH NORMALIZATION | boolean tokens canonicalized; unrecognized token fails closed (`MIGRATION_FIELD_TOKEN`) |
| 12 | `lastclean_timestamp` | C | last cleaning time | `LastSuccessfulCleaningCompletedAt` + `LastCleaningTimestampSource` provenance | IMPORT WITH NORMALIZATION | legacy format → UTC ISO-8601; unparseable fails closed (`MIGRATION_FIELD_TOKEN`); sentinel/zero date → PRESERVE WITH WARNING, provenance recorded as unverified (W3) |
| 13 | `min_time_allowaddtoqueue` | C | minimum re-queue interval | `HardMinimumCleaningInterval` | IMPORT WITH NORMALIZATION | conversion recorded; **unit metadata absent in source** → PRESERVE WITH WARNING (`MIGRATION_UNIT_UNVERIFIED`); no conversion applied until Owner confirms unit (W4) |
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
3. Sensor sequence skips them: I6 → I8 and I15 → I17.
4. I7 is the physical placement anchor of WJ3; I16 is the physical placement anchor of WJ1.
   The Water Jets are separate `WaterJetConfiguration` records — never identified *as*
   I7/I16.
5. Their CSV rows take disposition REJECT FOR I7/I16; zero dirty-score values are preserved
   as gap provenance, never normalized into sensor defaults.
6. No Water Jet or Isolation Valve identity may appear as a Sensor identity, and vice
   versa; the id namespaces are disjoint.

## 9. Validation and refusal-machine-code proposal

Proposed additive config-validation refusal family (no existing code changed; final home
per Checkpoint B):

| Code | Fires when |
| --- | --- |
| `TOPO_POSITION_COUNT` | logical positions ≠ 108 |
| `TOPO_SENSOR_COUNT` | sensor records ≠ 106 |
| `TOPO_CHANNEL_COUNT` | Thermocouple channels ≠ 212 (2 per sensor) |
| `TOPO_WALL_COUNTS` | wall totals ≠ Left 24 / Rear 29 / Right 24 / Front 29 |
| `TOPO_GAP_IDENTITY` | I7 or I16 present as a Sensor, missing as a gap, or gap-anchor binding ≠ (I7→WJ3, I16→WJ1) |
| `TOPO_DUPLICATE_ID` | `id` not unique |
| `TOPO_DUPLICATE_ORDER` | `order_total` not unique or outside 0–107 |
| `TOPO_WATERJET_COUNT` | Water Jet records ≠ 8 |
| `TOPO_VALVE_COUNT` | Isolation Valve records ≠ 8 |
| `TOPO_PAIRING_MISMATCH` | WJ↔IV pairing not exactly one-to-one with matching ordinals |
| `TOPO_TARGET_WALL_CONTRADICTION` | a Sensor's `cannon`-assigned WJ has a target wall/region ≠ the Sensor's wall/region (incl. cross-region mismatch) |
| `MIGRATION_CANNON_RANGE` | `cannon` outside 1–8 |
| `MIGRATION_MISSING_ASSIGNMENT` | an actual Sensor has no assigned Water Jet |
| `MIGRATION_FIELD_TOKEN` | unparseable enum / boolean / timestamp token |
| `MIGRATION_BOUNDS_ORDER` | `min ≥ max` dirty-score bounds on an actual Sensor |
| `MIGRATION_PLACEHOLDER_VALUE` | zero/degenerate bounds or threshold on an actual Sensor (preserved with warning; listed for Owner review) |
| `MIGRATION_UNIT_UNVERIFIED` | time-field unit metadata absent (preserved with warning; no conversion applied) |

**Fail-closed policy:** any structural code (`TOPO_*`, `MIGRATION_CANNON_RANGE`,
`MIGRATION_MISSING_ASSIGNMENT`, `MIGRATION_FIELD_TOKEN`, `MIGRATION_BOUNDS_ORDER`) aborts
the entire import. `MIGRATION_PLACEHOLDER_VALUE` and `MIGRATION_UNIT_UNVERIFIED` import
verbatim with recorded warnings and Owner-review entries — never silent normalization,
never invented defaults. Identical input ⇒ identical output (determinism).

## 10. Migration warnings and Owner-review list

| # | Warning / review item | Disposition |
| --- | --- | --- |
| W1 | Superseded "Cannon slot" wording still present in: `REQUIREMENTS.md` PHY-001, `DOMAIN_MODEL.md` §2.2.1, `STAGE_0.3A_PLAN.md` §9 note, and Product identifiers/comments (`SlotType.CANNON`, `CannonCount`, `EquipmentId`, simulator-map comments, `sensor-map.example.json`) | Doc + code-identifier propagation deferred to Checkpoint B (would touch Product source/contracts); naming decision (rename vs alias) is an explicit Owner-review item. Behaviour (two sensorless positions excluded from sensor runtime/queues/jobs) is unchanged |
| W2 | Degenerate/zero dirty-score bounds or thresholds on actual Sensor rows in the Owner CSV | PRESERVE WITH WARNING at import; Owner review before values are trusted |
| W3 | Sentinel/zero `lastclean_timestamp` values | PRESERVE WITH WARNING; provenance recorded as unverified; never silently defaulted |
| W4 | Unit metadata absent for `min_time_allowaddtoqueue` (and DEFER time fields) | No unit conversion applied until Owner confirms the unit |
| W5 | Legacy `cannon` / "Cannon"-wording names embed superseded vocabulary | Canonical model uses WJ vocabulary; legacy labels preserved as provenance only |
| W6 | `max_time_allowaddtoqueue`, `max_time_enable` have no canonical attribute | DEFER; Owner mapping decision required before any queue semantics may use them |
| W7 | `ip_modbus` / `channel_pair` / `base_modbus_address` are not approved Production-device configuration | DEFER; `[NOT VERIFIED]`; no device is ever addressed with them in any authorized stage |

## 11. Checkpoint B expected change set (planned — NOT started, NOT authorized)

| File | Change | Content |
| --- | --- | --- |
| `packages/contracts/Topology.cs` | NEW | `LogicalPositionRecord`, `WaterJetConfiguration`, `IsolationValveConfiguration` (+ position-kind vocabulary) per ADR-0017 §1, §3, §4 |
| `packages/contracts/Config.cs` | EXTEND | configuration-schema records for the 0.3A-3 config pipeline: `SensorConfigurationRecord`, migration result/warning records, disposition enum |
| `packages/contracts/Enums.cs` | EXTEND | position-kind vocabulary including NON_SENSOR_GAP; `CANNON` rename-vs-alias per Owner decision (W1) |
| `packages/domain/` | NEW files | `SensorParameterCsvImporter` + `TopologyValidator` (+ refusal-code table from §9); deterministic, atomic, fail-closed |
| `config/examples/` | NEW | migrated sensor-parameter example — structure only, synthetic values, no Production values (boundary S3) |
| `tests/config.tests/` | NEW | invariant tests per §12 |
| `REQUIREMENTS.md`, `DOMAIN_MODEL.md`, `STAGE_0.3A_PLAN.md` | DOC | propagate the corrected NON_SENSOR_GAP wording (W1) |
| `README.md`, `CURRENT_STATE.md`, `CHANGELOG.md` | DOC | stage-status rows updated at Owner acceptance, per repo convention |

**Explicitly excluded from Checkpoint B:** fixtures, `packages.lock.json`, Inspector,
runtime write/command paths, device addressing, simulator synthetic *values*, the React
spike. No behaviour beyond configuration loading; nothing device-facing.

## 12. Checkpoint B test proposals (contract/topology risks only — no tests in Checkpoint A)

| # | Proposed test | Risk covered |
| --- | --- | --- |
| T1 | 108 logical positions / 106 Sensors / 212 TC channels / 24-29-24-29 wall totals on a migrated configuration | count regressions |
| T2 | I7 and I16 appear only as NON_SENSOR_GAP LogicalPositions; no SensorConfiguration exists for them | gap leakage into runtime |
| T3 | exactly 8 Water Jets; exactly 8 Isolation Valves | identity-set drift |
| T4 | WJn paired with IVn exactly, ordinals matching, one-to-one both directions | pairing corruption |
| T5 | every WJ's target wall is the opposite wall of its installed wall; target wall/region equal the approved table (§3) | coverage inferred from the wrong wall |
| T6 | legacy `cannon n` maps directly to `WJn` (including Rear-lower `cannon = 1` → WJ1 / FRONT-lower installation) | wall-based remapping |
| T7 | every actual Sensor carries exactly one assigned Water Jet resolving to a real WJ; paired IV derived | dangling/missing assignment |
| T8 | no Water Jet or Valve identity appears as a Sensor id; namespace disjointness | identity collision |
| T9 | importer runs twice on identical bytes ⇒ identical results (records, warnings, refusal codes) | nondeterministic import |
| T10 | each refusal code from §9 fires on its malformed fixture and aborts atomically with no partial import | ambiguous topology not failing closed |

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
| Commit 1 (specification) | `c994c26303340cbe1fcafe0d2baf60a253549ece` — `docs(topology): define Water Jet and legacy parameter migration` |
| Commit 2 (delivery record) | appends PR number and head references (this commit) |
| Changed | `docs/decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md` (NEW); `docs/STAGE_0.3A-3_CHECKPOINT_A.md` (NEW); `docs/decisions/README.md` (index row); `CHANGELOG.md` (Unreleased entry) |
| Unchanged | All Product source, contracts, fixtures, locks, tests, Inspector, simulator, config examples, README/CURRENT_STATE stage rows, the React spike |
| Not Verified | All values inherited from Owner data (`[OWNER CONFIRMED]` / Owner-provided CSV facts); the canonical models are specified, not implemented; no .NET, no importer, no device, no runtime was executed or exercised here |
| PR | **[#6](https://github.com/tnoiiee/waterjet-sentinel-suite/pull/6)** — created from this branch against `main` — **OPEN, NOT MERGED** |
| Spec head (commit 1) | `c994c26303340cbe1fcafe0d2baf60a253549ece` — full specification above |
| PR head (commit 2) | recorded authoritatively in the PR header (GitHub, PR #6) and in the Checkpoint A report chat status; this commit appends only this delivery record |

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
