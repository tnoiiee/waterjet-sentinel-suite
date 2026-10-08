# Stage 0.3A-3 — Checkpoint C: Atomic Runtime Topology Migration and Read-Only Inspector Integration

Record status: **IMPLEMENTED (Arena-authored) and OWNER-LOCALLY VALIDATED (2026-10-08,
§16)**. The implementation was NOT compiled and NOT executed in Arena (no .NET SDK in the
sandbox; every .NET claim in the implementation sections below is source-authored and
statically reviewed). The TypeScript mirror check (`npm ci` + `npm run check`) WAS executed
in Arena and passes (31/31). Owner-local validation of the corrected chain — see §16.

- Authorization: Owner instruction 2026-10-08 — Checkpoint C AUTHORIZED FOR IMPLEMENTATION
  on the existing PR #6 branch `arena/ca9c94c4-waterjet-sentinel-suite`
  (Checkpoint A OWNER APPROVED; ADR-0017 ACCEPTED; Checkpoint B OWNER-LOCALLY VALIDATED at
  `0c3dcea708ac9c87295aaa1a616a6c262ab75bbd`, the required PR head for this work).
- Base verified before authoring: remote PR head `0c3dcea…`, remote main `a74db62c…`
  (unmoved), feature head `2db853a…` and lock handoff `0728df61…` in ancestry.
- PR #6 remains **OPEN — NOT MERGED** (only the Owner merges). No replacement PR.

## 1. Objective

Migrate the current-facing Runtime from the legacy Cannon-slot interpretation to the
accepted topology (ADR-0017): I7/I16 are NON_SENSOR_GAP (not Sensors, not Water Jets);
WJ1–WJ8 and IV1–IV8 are separate read-only configuration topology records with WJn paired
one-to-one with IVn; the installed position is separate from target coverage; every
Sensor carries its assigned Water Jet (legacy `cannon n → WJn` direct, never remapped by
wall) and the Isolation Valve derived only through that pairing. Integrated into Runtime
read-only state, Snapshot/Delta, API response projections, the development Inspector, the
synthetic composition, the current-facing examples/fixtures, and the meaningful tests.
No canonical Cannon entity remains current-facing.

## 2. Slices and commits (this branch, on top of `0c3dcea`)

| Slice | Commit | Content |
| --- | --- | --- |
| C1 | `d378edf20323d3e66573d0d6143327f94c652b04` | Atomic schema/terminology migration: contracts, domain, Runtime state, simulator, API responses, TS mirror, fixtures/examples, migrated tests, new RuntimeTopologyTests |
| C2 | `27591ea1a6a512c5c84f5794906e3b78125c122c` | Read-only API topology projection tests (RuntimeApiTopologyTests) |
| C3 | this commit | Inspector integration (wall map with NON_SENSOR_GAP cells + installation overlay, equipment topology table, assigned-device columns) and documentation |

## 3. Vocabulary migration (old → new)

| Old (transitional legacy) | New (canonical, current-facing) |
| --- | --- |
| `SlotType { SENSOR, CANNON }` | **Deleted.** `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` is the only position vocabulary |
| `WallMapSlot.SlotType` | `WallMapSlot.PositionKind` |
| `WallMapSlot.EquipmentId` (`CANNON_REAR`/`CANNON_FRONT`) | `WallMapSlot.GapAnchorForWaterJetId` (`WJ3` at I7, `WJ1` at I16) |
| `CanonicalSensorMap.CannonSlotCount` | `CanonicalSensorMap.NonSensorGapCount` (2) |
| `CanonicalSensorMap.CannonSlots` | `CanonicalSensorMap.NonSensorGapSlots` (`("I7","WJ3",5,7)`, `("I16","WJ1",5,16)`) |
| `TcChannelRules.CannonSlotCount` / `RequireCannonCarriesNoChannels` | `NonSensorGapSlotCount` / `RequireNonSensorGapCarriesNoChannels` |
| `SensorMapSlotExample.IsCannon` | `IsNonSensorGap` |
| `RuntimeRefusalCodes.WallMapCannonSlots` = `WALL_MAP_CANNON_SLOTS` | `WallMapNonSensorGaps` = `WALL_MAP_NON_SENSOR_GAPS` (+ new `TOPOLOGY_WATER_JETS`, `TOPOLOGY_ISOLATION_VALVES`, `TOPOLOGY_PAIRING`, `SENSOR_ASSIGNMENT`) |
| API `RuntimeCannonReference`, counts `Cannons`/`CannonSlots` | `RuntimeGapReference` (label + `GapAnchorForWaterJetId`), counts `NonSensorGaps`/`NonSensorGapSlots` |
| Inspector "Cannons" tile reading `counts.cannonSlots` | "Non-sensor gaps" tile reading `counts.nonSensorGapSlots` |
| `StageMarker` `STAGE_03A2C_RUNTIME_API` | `STAGE_03A3C_RUNTIME_TOPOLOGY` |

Kept deliberately (legacy provenance, not current-facing Cannon entities): the importer's
`"cannon"` source-column header and `MIGRATION_CANNON_RANGE` (legacy migration refusal
vocabulary, Checkpoint B surface, unchanged), and historical wording in dated records.

## 4. Schema compatibility decision (explicit)

**Option A — atomic schema update in Stage 0.3A-3 Checkpoint C; no deprecated alias.**

- `wjss.snapshot/1` → **`wjss.snapshot/2`**; `wjss.delta/1` → **`wjss.delta/2`**;
  the sensor-map example schema identity → **`wjss.sensor-map/2`** (structural break:
  slot kind vocabulary + slot anchor field + per-Sensor assignment fields + new Snapshot
  topology collections).
- Rationale: this is a development-stage schema with exactly three in-repo consumers —
  the development Inspector, the TypeScript mirror, and the fixtures/examples/tests — all
  migrated in the same atomic commit. A deprecated read-only alias would preserve the
  inaccurate Cannon terminology the Owner explicitly prohibited; Option B was rejected on
  that ground. No deployed `/1` consumer exists; nothing outside the repository reads it.
- Impact: any consumer must reject `/1` and accept `/2`; the old slot fields
  (`slotType: "CANNON"`, `equipmentId`) no longer exist on the wire; Deltas carry no
  topology (unchanged rule, now also enforced by the TS validator for the two new keys);
  ApiVersion stays `1`.

## 5. Runtime topology shape (as implemented)

- `RuntimeState` (immutable, validated before commit, atomically revisioned as before)
  gains `WaterJets` (exactly 8) and `IsolationValves` (exactly 8), frozen by the State
  Freezer like every other collection; they are process-lifetime-immutable configuration
  references and are carried unchanged through every revision.
- Invariants (all enforced with machine refusal codes, total-or-nothing):
  wall map = 108 slots in row-major order; exactly 106 SENSOR slots with unique canonical
  ids and no anchor; exactly 2 NON_SENSOR_GAP slots at I7 (anchoring WJ3) and I16
  (anchoring WJ1) with null SensorId; scan order dense 1–106 over SENSOR slots only;
  212 unique thermocouple channel strings; exactly 8 WJ + 8 IV in ordinal order; WJn ↔ IVn
  pairing asserted in both directions; every Sensor's `AssignedWaterJetId` must exist,
  its TARGET wall/region must match the Sensor's wall/region (rear-lower Sensor ⇒ WJ1
  even though WJ1 installs front-lower), and `AssignedIsolationValveId` must equal that
  Water Jet's dedicated IVn.
- `WaterJetTopologyCatalog.TryRequireAssignment(...)` (domain, pure) is the single
  assignment rule; the simulator derives every Sensor's assignment from it; acquisition
  remains DEFERRED — no acquisition binding exists and none is simulated.
- No command state: WJ/IV are never controllable device instances; no writer, queue,
  pump, valve or axis path touches them.

## 6. Snapshot / Delta behavior

- `wjss.snapshot/2` gains `waterJets` and `isolationValves` (static topology,
  Snapshot-only, never in a Delta) next to the existing `wallMap` (106 SENSOR +
  2 NON_SENSOR_GAP, Snapshot-only). `SensorPresentationState` records carry
  `positionKind: "SENSOR"`, `assignedWaterJetId`, `assignedIsolationValveId`.
- Deltas remain sparse whole-record projections; the Delta type has no topology field,
  application reconstructs with the previous revision's topology (`current with {…}`),
  and the wire Delta is asserted (test + TS validator) to never carry `waterJets`,
  `isolationValves` or `wallMap`.
- The three-state `activeJob` encoding, revision/gap-detection semantics, gapless chain,
  bounded histories, and determinism are unchanged. No `activeJobCleared` field exists.
- Delta record identity now also covers the assignment fields (a Delta cannot rewrite
  them), and the new `SENSOR_ASSIGNMENT` refusal protects the invariant if it tried.

## 7. API surface (read-only, unchanged routes)

- Routes unchanged: `/health/live`, `/health/ready`, `/api/v1/snapshot` (now
  `wjss.snapshot/2`), `/api/v1/runtime`, `/api/v1/deltas`, `/inspector`. Every mapping is
  GET; no POST/PUT/PATCH/DELETE; no SSE; no command/write/publication/dispatch/
  equipment-control endpoint. Health/readiness untouched.
- `GET /api/v1/runtime` status payload gains: `sensorCounts.nonSensorGaps` (2),
  `sensorCounts.nonSensorGapSlots` (I7 → WJ3 row 5 col 7; I16 → WJ1 row 5 col 16, in
  orderTotal order), `sensorCounts.waterJets` (8), `sensorCounts.isolationValves` (8) and
  `sensorCounts.equipmentTopology` — `{ acquisition: "DEFERRED_NO_ACQUISITION_BINDING",
  waterJets[8] (id, installed wall/region, placement kind + anchors, target wall/region,
  dedicatedIsolationValveId), isolationValves[8] (valveId, servedWaterJetId) }`.
- `RuntimeSensorView` (the per-Sensor status projection) gains `assignedWaterJetId` and
  `assignedIsolationValveId`.

## 8. Development Inspector (read-only)

- Foundation tile "Cannons" → "Non-sensor gaps" (I7 · I16; "not Sensors, not devices";
  tooltip explains the placement-anchor role).
- NEW "Wall map — logical positions and installation overlay": all 108 logical positions
  per wall; the 106 Sensor cells show only their canonical id; I7 and I16 render visually
  distinct (dashed outline, warning tone) labelled `I7 · GAP` / `I16 · GAP` — no Sensor
  value, quality or TC channel, and never labelled WJ1/WJ3. Below each wall, a visually
  separate overlay line lists the Water Jets installed on that wall (placement kind +
  anchors + paired IV) and the opposite wall they spray.
- NEW "Equipment topology — Water Jets and Isolation Valves (read-only)" table: WJ id,
  paired valve, installed, placement, anchors, target ("opposite wall"), status
  ("topology reference · control not authorized · acquisition deferred").
- Sensor table gains "Assigned WJ" / "Assigned IV" columns; the note states the assigned
  device is the cleaning device responsible for this Sensor's wall, may be installed on
  the opposite wall, and acquisition is deferred.
- Snapshot facts gain the 8 / 8 topology row. The safety footer now also states that WJ/IV
  appear as read-only topology references only and acquisition is deferred. No actuation
  affordance exists anywhere (all requests GET; the only buttons are display filters,
  pause/density/copy). Placeholder state implies no physical feedback.

## 9. Synthetic composition (unchanged guarantees)

Explicit seed, deterministic ticks, 106 evolving Sensors, 212 TC sides, walls 24/29/24/29,
Snapshot/Delta determinism, bounded histories: unchanged. WJ/IV topology is static
process-lifetime state; the evolution NEVER simulates valve movement, water flow, nozzle
actuation, pump feedback, axis movement or cleaning execution — the new assigned-device
fields are static derived references.

## 10. Fixture / example regeneration note (Owner gate)

The committed `snapshot.seed0.json`, `delta.basic.json`, `delta.gap.json` and
`config/examples/sensor-map.example.json` were hand-mirrored in Arena to the updated
generators (semantic parity: key-order-, number- and line-ending-tolerant comparison).
The Owner-local gate is unchanged:

```
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release
# if any file differs:
WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release
# then the TypeScript mirror must still pass:
cd packages/contracts/wjss-contracts-ts && npm ci && npm run check
```

## 11. Test inventory (meaningful, migrated or new)

- Migrated to the canonical vocabulary (behavior preserved): `CanonicalMapStructureTests`
  (gap anchors I7/WJ3, I16/WJ1), `TcChannelContractTests` (gap slots carry no channels;
  canonical totals; structured position ordering), `ExampleConfigTests` (106/2 split by
  `positionKind`; gap anchors; no gap identity in sensor ids), `SensorParameterMigrationTests`
  (T19 name-based "no transitional property"; T20 rewritten as the migration-completeness
  proof: kind names exactly SENSOR/NON_SENSOR_GAP, gap anchor tuples, wall-map slot
  properties, and NO compiled `SlotType` type in the contracts assembly),
  `RuntimeStateInvariantTests` (NON_SENSOR_GAP positions + refusal code; new-arity
  composition; profile refusal intact), `SyntheticInitialStateTests`,
  `SyntheticEvolutionTests`, `RuntimeSnapshotProjectionTests`, `SnapshotDeltaTests`,
  `StageMarkerTests` (`STAGE_03A3C_RUNTIME_TOPOLOGY`), `ContractEncodingTests`
  (`NON_SENSOR_GAP` wire casing, explicit-null rule, `/2` schema id).
- NEW `tests/runtime.tests/RuntimeTopologyTests.cs`: exactly 8 WJ + 8 IV in ordinal order;
  both-direction pairing; installed separate from target per the approved table; every
  Sensor's assignment target coverage; rear-lower ⇒ WJ1/IV1 (responsibility, never
  location); gap positions anchor WJ3/WJ1 and are never Water Jet identities; Snapshot
  round-trip preserves the topology; Delta never emits the topology and application
  preserves it unchanged; profile refusal intact with the topology present.
- NEW `tests/api.tests/RuntimeApiTopologyTests.cs`: status counts 108/106/212/2/8/8;
  gap references in orderTotal order; gap labels never Water Jet identities; equipment
  topology acquisition DEFERRED + pairing + installed≠target + anchors; composed SIMULATOR
  state: per-Sensor assignment agreement, rear-lower ⇒ WJ1/IV1, snapshot carries 8+8;
  route registry read-only (exactly the six routes, no write-route constant).

## 12. Static validation performed in Arena (truthful)

- Boundary scan `tools/boundary-scan/boundary-scan.mjs`: **0 findings (S1–S9 clean)** on
  the changed worktree (run after each slice).
- TypeScript mirror **executed in Arena**: `npm ci` + `npm run check` — `tsc --noEmit`
  clean and `node --test` **31 total / 31 passed / 0 failed** against the migrated
  fixtures and example.
- Inline Inspector script extracted and syntax-checked with `node --check` (OK); HTML tag
  balance checked (all section/table/div/span/button counts balanced).
- Per-file brace/paren depth balance over every changed C# source (36 files): none
  unbalanced. `git diff --check` clean. No tab/CR introductions; final newlines preserved.
- Namespace/member cross-checks during authoring; no `#pragma`/`NoWarn`/severity/
  nullability/TreatWarningsAsErrors/AnalysisLevel changes; no analyzer suppression.
- **NOT done in Arena (no .NET SDK):** restore, Release build, xUnit execution, fixture
  parity run, lock refresh. Expected drift: `adapters/simulator/packages.lock.json` and
  `tests/integration/packages.lock.json` gain the new direct `wjss.domain` project-graph
  edge (both projects now reference `Wjss.Domain` directly; neither had domain access
  before). No `packages.lock.json` was hand-edited; the Owner-local restore refreshes
  them (Checkpoint B lock `0728df61` in `tests/config.tests` is untouched).

## 13. Changed / Unchanged / Not Verified

- **Changed:** every file of slices C1–C3 (46 + 1 + docs/Inspector files; exact lists in
  the commit messages and the delivery report).
- **Unchanged:** `spikes/**`; all `packages.lock.json`; `tools/**` scanner;
  `tests/config.tests` Checkpoint-B surface (`SyntheticSensorParameterCsv.cs`,
  `SensorParameterMigrationTests` migration-semantics tests T1–T18 semantics,
  `MigratedExampleShapeTests.cs`, `packages.lock.json`); `SensorParameterCsvImporter.cs`;
  `TopologyValidator.cs`; routes; health/readiness; queue/pump/sequence/alarms/Safe-Return
  shapes; Active-Job three-state encoding; determinism; `ApiVersion = 1`.
- **Not Verified in Arena:** every .NET execution claim (restore/build/test/parity);
  Inspector browser rendering; Owner-local UI review. Status: **SOURCE AUTHORED /
  STATICALLY REVIEWED / NOT COMPILED IN ARENA / NOT EXECUTED IN ARENA** (C#); TS check
  executed in Arena (31/31).

## 14. Owner-local verification commands

```
git fetch origin && git checkout arena/ca9c94c4-waterjet-sentinel-suite
dotnet restore WaterJetSentinelSuite.sln
dotnet build WaterJetSentinelSuite.sln -c Release
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release --no-build
node tools/boundary-scan/boundary-scan.mjs
cd packages/contracts/wjss-contracts-ts && npm ci && npm run check
```

## 15. Boundary confirmation

No hardware communication, no real Modbus/KMotion/Galil/PLC, no queue dispatch, no
cleaning execution, no pump/valve/axis command path, no Safe Return actuation, no
Production secrets or acquisition values, no `spikes/**` change, no installer, no ZIP,
no Release, no deployment, no SSE, no `activeJobCleared`, no write/command route, no
sensorparam CSV in Git. TEST_HARDWARE and PRODUCTION device access remain NOT AUTHORIZED.

## 16. Owner-local validation (2026-10-08) — CHECKPOINT C OWNER-LOCALLY VALIDATED

Recorded from the Owner's runs; Arena did not execute any .NET command. Historical failed
build/test observations before the correction chain are not acceptance evidence; the final
successful result below is the validation of record.

| Item | Record |
| --- | --- |
| Validated feature head | `162ad7ff1e5c7bab398de6c6d1a38131f17082de` (`fix(tests): align topology round-trip and sensor map vocabulary`) |
| Correction chain | `8567a78` (CS0103: `using Wjss.Domain;` added to `SyntheticSensorMap.cs`) → `440a7fa` (CS0246: `PlacementKind` type reference corrected to the contract enum `WaterJetPlacementKind` in `RuntimeApiResponses.cs`; property name unchanged) → `162ad7f` (test corrections: both raw `SensorMapSlotExample` JSON snippets moved from the superseded `slotType` to the canonical `positionKind` vocabulary — the invalid-scalar test now fails for the intended tcChannels reason — and the Snapshot topology round-trip assertions replaced record equality with explicit field-by-field semantic comparison including `PlacementAnchors` contents). Product source beyond those two one-line corrections: NONE |
| Owner artifact commit | `bac36add7011c72d6ab03ea5d3e3664ed82a197c` (parent `162ad7f`; exactly six files: five genuine `packages.lock.json` refreshes + the genuinely regenerated `config/examples/sensor-map.example.json`) |
| Artifact handoff verification | All five lock files valid JSON; zero package-version changes; the only changes are the expected `Wjss.Domain` project graph (direct `wjss.domain` edges in `adapters/simulator` and `tests/integration`; transitive `Wjss.Domain` dependencies added to the `wjss.adapters.simulator` project entries of `apps/runtime`, `tests/api.tests`, `tests/runtime.tests`); no absolute Owner-local path; no credential or secret. The regenerated example is valid JSON; its only semantic drift from the prior committed example is the corrected `$.notes[1]` NON_SENSOR_GAP text; 108 logical positions / 106 Sensors / 212 unique TC channels; I7 NON_SENSOR_GAP anchored to WJ3; I16 NON_SENSOR_GAP anchored to WJ1 |
| Environment | .NET SDK `10.0.401`; xUnit runtime .NET `10.0.12` |
| Normal restore | **PASS** |
| Locked restore | **PASS** |
| Release build | **PASS — 0 warnings, 0 errors** |
| Full .NET tests | **201 total / 201 passed / 0 failed / 0 skipped** |
| Fixture generation/update run | **7 / 7 passed** |
| Fixture parity (after update mode removed) | **7 / 7 passed** |
| TypeScript typecheck and tests | **PASS — 31 total / 31 passed** |
| Boundary scan | **0 findings (S1–S9 clean)** |
| JSON examples | all three parsed successfully |
| Final Owner-local working tree | **CLEAN** after the artifact push |

### 16.1 Owner reviews — PASSED

- **CHECKPOINT C FUNCTION REVIEW: PASSED.**
- **CHECKPOINT C LOGIC REVIEW: PASSED.**
- **CHECKPOINT C INSPECTOR UI/UX REVIEW: PASSED. CRITICAL UI BLOCKER: NONE.**

Owner visually verified: SIMULATOR profile; READY / `RUNTIME_READY`; EVOLUTION RUNNING;
stage marker `STAGE_03A3C_RUNTIME_TOPOLOGY`; revisions and accepted ticks advancing;
rejected transitions zero; 108 logical positions; 106 Sensors; 212 Thermocouple channels;
exactly two NON_SENSOR_GAP positions (I7 anchored to WJ3, I16 anchored to WJ1) with no
Sensor value, score, classification, quality or TC channel on gap cells; WJ1–WJ8 and
IV1–IV8 present with one-to-one WJn ↔ IVn pairing; installed position separate from target
coverage (WJ1 installs FRONT lower at the I16 gap and targets REAR lower; WJ3 installs
REAR lower at the I7 gap and targets FRONT lower); the Equipment Topology table showing all
eight installed positions, anchors, opposite-wall target coverage and paired valves; Sensor
values exposing Assigned WJ / Assigned IV by target coverage, not the installed wall
(FRONT upper → WJ7/IV7, LEFT upper → WJ8/IV8, REAR upper → WJ5/IV5, RIGHT upper → WJ6/IV6);
synthetic GOOD / STALE / BAD / UNCERTAIN paths with reasons; continuous clean Delta chain;
Snapshot schema `wjss.snapshot/2` with 8 Water Jets and 8 Isolation Valves; the Inspector
read-only with no Pump, Valve, Axis, Queue dispatch, Cleaning or Safe Return command.

### 16.2 Deferred non-blocking polish (Inspector presentation FROZEN for this checkpoint)

Deferred, non-blocking, and NOT authorized for another change round in Checkpoint C:

- a gap cell may later show `I7 · GAP` / `I16 · GAP` as the primary label with the slot id
  in a tooltip;
- machine placement vocabulary may later receive friendlier Product-UI captions;
- the development Inspector remains vertically scrollable because it exposes extensive
  diagnostic information.

The Inspector presentation is frozen for this checkpoint; no modification was made or is
authorized for these items.

### 16.3 Status

Stage 0.3A-3 Checkpoint A: OWNER APPROVED · ADR-0017: ACCEPTED · Stage 0.3A-3 Checkpoint B:
OWNER-LOCALLY VALIDATED · **Stage 0.3A-3 Checkpoint C: OWNER-LOCALLY VALIDATED (Function
Review PASSED, Logic Review PASSED, Inspector UI/UX Review PASSED; minor punchlist deferred
and non-blocking)** · PR #6: PREPARED FOR FINAL SOURCE REVIEW — OPEN, NOT MERGED ·
TEST_HARDWARE: NOT AUTHORIZED · PRODUCTION DEVICE ACCESS: NOT AUTHORIZED. Checkpoint C does
not authorize hardware, command, control, actuation, ZIP, Release or Deployment work.
