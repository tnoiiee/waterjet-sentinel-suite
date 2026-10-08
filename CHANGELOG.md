# Changelog

All notable changes to this repository are recorded in this file.

The format follows the spirit of *Keep a Changelog*, adapted for a stage-gated project:
entries correspond to Owner-approved Delivery Stages and to review corrections, not to
releases of software. **The repository contains documentation, one removable synthetic
feasibility spike (Stage 0.2.1A), and — from Stage 0.3A-1 — the product foundation source
checkpoint VALIDATED Owner-locally (Release build 0 warnings / 0 errors; full .NET suite
61/61; TypeScript 24/24; fixture parity 7/7; boundary scan S1–S9 clean): the committed
fixtures are the genuine .NET-generated files and the twelve `packages.lock.json` are
genuine restore output, both transferred from the Owner's validated Working Tree
(handoff commit `488b98fb70c20ed6e028043e31b661f64d0c92f4`); the .NET SDK is pinned by
`global.json` at Owner-validated `10.0.401`. Arena never ran .NET; this Owner-local
evidence is authoritative for the checkpoint.**

---

## Versioning position

- **No application version exists.** The application version is **NOT ESTABLISHED**. No
  runtime release has been built, packaged, or delivered.
- A previous reference to a `0.1.0` documentation label was a documentation-only working
  label, not a product release. It is retained here only for historical traceability and is
  explicitly **not** a product version.
- The documentation versioning policy is **`[OPEN]`**. No scheme, such as semantic
  versioning, is adopted, and no documentation stage automatically advances a version
  number. A future Owner decision is required to establish one.

---

## [Unreleased]

### Stage 0.4A CP-3a / CP-3b — development checkpoint submitted for review (NOT compiled or executed in Arena; not merged)

- CP-3a (kernel hardening): FU-1 latch with a RUNNING Job rejected as `JOB_RUNNING_UNDER_LATCH`; FU-2 `Apply` wording; FU-3 documentation only; FU-4 valve movement away from OPEN during CLEANING enters Safe Return in the same transition; AxisStandbySeq written at SR5 and readable through `PreviewAxisStandbyLedger`.
- CP-3b (simulator library, no host wiring): eleven-scenario catalogue on a fixed epoch; bounded retention; GlobalQueue and Sensor queue-state projection; Delta content comparison and a pure candidate path.
- Not delivered (`[OPEN]`): the wire projection of the Active Job, Sequence, Pump and Safe Return sections, pending Owner rulings on their presentation fields.
- Owner-local validation required (Release build with 0 warnings, full .NET suite, fixture parity, TypeScript, boundary scan). CP-3c and CP-4 NOT AUTHORIZED. Not merged.

### Stage 0.4A CP-2 — Owner-local validation and Final Source Review (documentation only, 2026-10-08)

- **OWNER-LOCALLY VALIDATED** at validated head `1f94991b8fddd5b1e7e158c8f02c34a70ac0c14c` (Owner-reported; Arena did not run the toolchain). .NET SDK 10.0.401; xUnit runtime .NET 10.0.12; locked restore PASS with no drift; Release build 0 warnings / 0 errors; full .NET tests **300 total / 300 passed / 0 failed / 0 skipped** (CP-1 validated suite 253 + CP-2 new facts 47); fixture parity 7/7; TypeScript typecheck PASS and tests 31/31; boundary scan 0 findings (S1–S9 clean); `git diff --check` PASS; final Owner-local working tree CLEAN. No Owner-local artifact commit was required.
- **CP-2 Final Source Review (read-only, Arena): PASSED.** No blocking defect. Non-blocking follow-ups are recorded in `docs/STAGE_0.4A_CP-2_CHECKPOINT.md` §19. Source and tests were not changed by this review.
- **F2 (one evidence sequence): PASSED.** **F3 (release under the critical latch): PASSED**, matching the approved behaviour exactly. **EXPECTED_STOP during CLEANING: PASSED** as a refusal that changes no Job lifecycle, water state, Queue or QueueRevision, and does not set the latch. Owner ruling O-3 remains open for confirmation.
- PR #8 remains OPEN and NOT MERGED. **CP-3 NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** TEST_HARDWARE and PRODUCTION device access NOT AUTHORIZED.

### Stage 0.4A CP-2 — Cleaning Job, Mandatory Safe Return and Critical Pump Kernel (source authored; NOT compiled or executed in Arena; PR open, not merged)

- **Owner-authorized CP-2 scope** recorded in `docs/MASTER_PLAN.md` §3.3.2 (`ef51bd8`).
- **Kernel** (`packages/application/Runtime/Sequencing/**`, `6b94508`): one Active Job with a lifecycle (accepted `JobLifecycle` names), abstract Pump classification (READY, EXPECTED_STOP, UNEXPECTED_STOP, TRIP), Mandatory Safe Return in the Owner order (SR1 to SR7), abstract Isolation Valve and Axis feedback, and outcome recorded only after Safe Return is verified. Safe Return failure retains the Job with RECOVERY_REQUIRED evidence. The external `SafeReturnReleaseEvidence` and `ReleaseActiveJob` input are removed; release is the kernel's own SR7. One evidence sequence.
- **Tests** (`tests/runtime.tests/Sequencing/**`, `ffc2701`): 47 new facts for the CP-2 scenario list, plus CP-1 tests adjusted to the changed kernel API.
- **Contracts:** unchanged. No contract blocker.
- Checkpoint report: `docs/STAGE_0.4A_CP-2_CHECKPOINT.md`. CP-3 and CP-4 NOT AUTHORIZED. Owner-local .NET validation REQUIRED.

### Stage 0.4A CP-1 — Final evidence amendment, F1 closed, CP-1 Final Source Review passed (documentation only, 2026-10-08)

- **OWNER-LOCALLY VALIDATED** at head `03144f43122bed2bd011b26886f663c2b46b79c4` (Owner-reported; Arena did not run the toolchain). .NET SDK 10.0.401; xUnit runtime .NET 10.0.12; locked restore PASS with no drift; Release build 0 warnings / 0 errors; full .NET tests 253/253 passed; fixture parity 7/7; TypeScript typecheck PASS and tests 31/31; boundary scan S1–S9 clean; `git diff --check` PASS; working tree clean. No artifact commit was required.
- **Correction chain:** `2cfe648` (CP-1) → `27c939e` (validation record) → `7b84824` (CP-2 scope gate proposal) → `23b276f` (`fix(runtime): enforce sequencing state integrity`) → `03144f4` (`fix(tests): use predicate assertion for public setters`, xUnit2029 analyzer correction, test-only).
- **F1 reclassified and CLOSED:** the earlier "FOLLOW-UP RECOMMENDED BEFORE MERGE, not a blocking public-boundary defect" classification is withdrawn. F1 is a BLOCKING STATE-INTEGRITY DEFECT. Root cause: public construction and public init mutation of `SequencingState`; caller-owned collections could alias state; `Apply` had no state-integrity guard; an inconsistent Mode / critical-latch combination could silently project as running. Correction: internal construction boundary; read-only queue copy; deterministic validator; invalid `Apply` fails closed with `SEQUENCING_STATE_INVALID`; invalid projections throw with the stable code; default-as-running removed; checked counters.
- **CP-1 Final Source Review: PASSED.** Carried forward as CP-2 preconditions, not CP-1 blockers: F2 (single evidence sequence) and F3 (release accepted under the critical latch, consistent with the approved CP-2 interpretation).
- **Status:** CP-2 scope gate PROPOSED ONLY (prerequisite F1 corrected and validated). CP-2, CP-3 and CP-4 NOT AUTHORIZED. PR #7 OPEN, NOT MERGED.

### Stage 0.4A CP-0 / CP-1 — Owner-local validation recorded and CP-1 Final Source Review (documentation only, 2026-10-08)

- **OWNER-LOCALLY VALIDATED** at feature head `2cfe648d512241d9fef459cd91c663b9780753f9` (CP-1; parent `1f76da8` = CP-0; base `909d028`). Owner-reported: .NET SDK 10.0.401; locked restore PASS with no drift; Release build 0 warnings / 0 errors; full .NET tests 217/217 passed; fixture parity 7/7; TypeScript typecheck PASS and tests 31/31; boundary scan S1–S9 clean; `git diff --check` PASS; working tree clean. No artifact commit was required.
- **CP-1 Final Source Review (read-only):** no blocking public-boundary defect. FOLLOW-UP RECOMMENDED BEFORE MERGE for the public `SequencingState` constructor (a caller can bypass the critical latch or construct an over-capacity or inconsistent state; the kernel does not validate its input). CP-2 precondition: a single evidence sequence (the CP-1 release check currently compares a separate Safe Return sequence). No Runtime, API, Inspector, contract, schema or fixture change.
- The Stage 0.4A CP-2 scope gate is **proposed** (MASTER_PLAN §3.3.1). CP-2, CP-3 and CP-4 are NOT AUTHORIZED. PR #7 remains OPEN and NOT MERGED.

### Stage 0.4A CP-0 — Status and ADR reconciliation for Stage 0.4A sequencing (documentation only, 2026-10-08)

- Recorded that PR #5 (merge `a74db62`) and PR #6 (merge `909d028`, the Stage 0.4A approved base) are
  MERGED to `main`, and that Stage 0.3A-3 is COMPLETE. Superseded the stale "PR OPEN", "Checkpoint C NOT
  AUTHORIZED" and "config loading pipeline" status text with dated notes; historical text is retained.
- Recorded the Stage 0.4A scope gate and the Owner rulings for CP-1 (ready-only FIFO queue, capacity 8,
  head-only dispatch, single Active Job, pump waiting outside Queue entries, head-revalidation removal,
  pause as Job/AutoSequence state). Open items D6–D12 are listed, not decided.
- Appended a reconciliation note to ADR-0014 (status remains DRAFT). No contract, fixture, source, test,
  or schema change. CP-2, CP-3 and CP-4 are NOT AUTHORIZED.
- Source authored in Arena; NOT COMPILED IN ARENA; NOT EXECUTED IN ARENA.

### Stage 0.3A-3 Checkpoint A — Equipment topology decision and legacy parameter migration specification (planning only)

Owner-authorized Checkpoint A (planning / decision / specification only) on verified
remote-main base `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` (PR #5 foundation). New
[`ADR-0017`](docs/decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md)
(`PROPOSED`) and companion report
[`docs/STAGE_0.3A-3_CHECKPOINT_A.md`](docs/STAGE_0.3A-3_CHECKPOINT_A.md) record:

- The **corrected sensor topology**, superseding the I7/I16 "Cannon equipment slot"
  interpretation: I7 and I16 are **NON_SENSOR_GAP** positions — location anchors only
  (I7 → WJ3, I16 → WJ1); sensor sequence skips them (I6→I8, I15→I17). Protected counts
  stand: 108 logical positions / 106 Sensors / 212 TC channels / Left 24, Rear 29,
  Right 24, Front 29.
- **WJ1–WJ8** installed positions (wall, region, placement kind, anchors) and target
  coverage (opposite wall, matching region) as separate concepts; **IV1–IV8** with
  mandatory one-to-one ordinal pairing `WJn ↔ IVn`.
- The **legacy `sensorparam.csv` migration specification**: `cannon` = Assigned Cleaning
  Device ID mapping **directly** `cannon n → WJn` (never remapped by wall); the full
  CSV field-classification matrix (19 fields across five classes; dispositions IMPORT /
  IMPORT WITH NORMALIZATION / PRESERVE WITH WARNING / DEFER / REJECT FOR I7/I16);
  deterministic atomic fail-closed validation with a proposed refusal-code family;
  and the migration warnings / Owner-review list (including the Checkpoint-B deferral of
  the superseded "Cannon" wording in docs and code identifiers).

**No Product source, contract, fixture, lock, test, or Inspector file was changed; no
hardware or Production device was accessed; no runtime, queue, pump, valve, axis, or
Mandatory Safe Return action was taken or specified as authorized. Checkpoint B
implementation NOT STARTED. PR OPEN — NOT MERGED.**

**Owner review correction (PR #6 — CHANGES REQUESTED, 2026-10-08; documentation only):**
Sensor `scanOrder` is no longer described as taken from legacy `order_total` —
`order_total` is the legacy logical-position order (zero-based 0–107, including the
NON_SENSOR_GAP rows I7/I16), preserved as provenance, and canonical Sensor `scanOrder` is
the derived dense one-based 1–106 sequence (sort by `orderTotal`, exclude NON_SENSOR_GAP);
governance wording corrected so that formal acceptance requires an explicit Owner decision
and PR merge alone does not constitute acceptance (ADR-0017 remains `PROPOSED`); the
delivery record now shows the real commit chain and the reviewed PR head
`6648437f24e918d8be3a1c4ad27edb8c1bc4b0a3`, and the status classification separates
OWNER CONFIRMED from NOT VERIFIED. Checkpoint B remains NOT STARTED.

**Second Owner review correction (PR #6 — CHANGES REQUESTED, 2026-10-08; documentation
only):** legacy `id` corrected to `legacyRecordId` provenance (never a logical-position
identity or Sensor ID) and legacy `sensorname` corrected to the canonical
`LogicalPosition.logicalId` / `SensorConfiguration.sensorId`, validated against the label
derived from `orderTotal` (additive refusal `MIGRATION_LOGICAL_LABEL_MISMATCH`);
`lastclean_timestamp` disposition changed to raw preservation with UNKNOWN source timezone
and no UTC conversion (no `LastSuccessfulCleaningCompletedAt` in Checkpoint B);
`min_time_allowaddtoqueue` disposition changed to raw preservation with UNKNOWN unit and
no `HardMinimumCleaningInterval` mapping (no TimeSpan invented); the derivations of
`UseDirtyScoreThreshold`, `HasVerifiedCleaningHistory`,
`LastSuccessfulCleaningCompletedAt`, and `HardMinimumCleaningInterval` are removed from
Checkpoint B pending explicit Owner decisions; the existing runtime CANNON vocabulary
(`SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`,
`CANNON_REAR`/`CANNON_FRONT`) is recorded as TRANSITIONAL LEGACY RUNTIME REPRESENTATION —
semantically superseded — with its migration deferred to Checkpoint C, while Checkpoint B
introduces `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` only; TC enforcement is
structural-only (106 × 2 = 212); the public-repository boundary for `sensorparam.csv` is
strengthened (never committed or copied; synthetic public-safe test rows only). Also
restores the first-review §11 importer-behaviour text that had failed to persist in commit
`3dac1a0c`. Checkpoint B remains NOT STARTED.

### Stage 0.3A-3 Checkpoint B — implemented (Arena-authored; NOT compiled/executed in Arena)

**Checkpoint A final Owner review: PASS. ADR-0017: OWNER APPROVED (explicit Owner
acceptance decision, 2026-10-08). Checkpoint B: AUTHORIZED and implemented on PR #6.**
Delivered exactly per the approved change set (ADR-0017 decision 13 / report §11) plus
the wiring it requires:

- `packages/contracts/Topology.cs` (NEW): canonical `LogicalPositionRecord`
  (`legacyRecordId` provenance, `logicalId`, `positionKind`), `WaterJetConfiguration`,
  `IsolationValveConfiguration`, plus the `Region` and `WaterJetPlacementKind`
  vocabularies.
- `packages/contracts/Enums.cs` (EXTEND): canonical `LogicalPositionKind
  { SENSOR, NON_SENSOR_GAP }` only — the transitional `SlotType.CANNON` runtime wire
  vocabulary is untouched; its atomic migration remains deferred to Checkpoint C.
- `packages/contracts/Config.cs` (EXTEND): `SensorConfigurationRecord`,
  `DeferredAcquisitionProvenance` (raw provenance only, never device configuration),
  `MigrationWarningRecord`, `MigrationRefusalRecord`, `SensorParameterMigrationResult`,
  `SensorParameterImportOutcome` (atomic accept-or-refuse), the approved
  `MigrationFieldDisposition` enum and field-classification records, and the additive
  `MigrationRefusalCodes` vocabulary (including the three warning-level codes).
- `packages/domain/WaterJetTopologyCatalog.cs` (NEW): the Owner-approved WJ/IV topology
  table and the I7→WJ3 / I16→WJ1 gap anchors.
- `packages/domain/SensorParameterCsvImporter.cs` (NEW): deterministic, atomic,
  fail-closed importer — `id`→`legacyRecordId` provenance only; `sensorname`→canonical
  logicalId/sensorId validated against the `orderTotal`-derived label
  (`MIGRATION_LOGICAL_LABEL_MISMATCH`); dense derived `scanOrder` 1–106 skipping I7/I16;
  direct `cannon n → WJn` mapping with target-wall agreement; offset-free timestamps raw
  with UNKNOWN timezone (no UTC conversion); unknown-unit durations raw (no TimeSpan);
  no unapproved domain derivations; acquisition columns captured as deferred raw
  provenance only.
- `packages/domain/TopologyValidator.cs` (NEW): independent structural validator
  (108 / 106 / 212 / 24-29-24-29, ordering, gaps, pairing, assignment, targets,
  namespace disjointness); the importer runs it on its own output before accepting.
- `config/examples/sensor-parameters.migrated.example.json` (NEW): synthetic,
  structure-only example of the migrated shape (all deferred/raw and acquisition
  fields null; no Owner value).
- `tests/config.tests/`: `SyntheticSensorParameterCsv.cs` (public-safe synthetic CSV
  builder) + `SensorParameterMigrationTests.cs` (approved planned tests T1–T20 plus
  validator refusals, warning bookkeeping, and the approved field matrix) +
  `MigratedExampleShapeTests.cs` (example schema guard); the test csproj gains the
  required `Wjss.Domain` project reference (no package changes, so no lock file changes).
- `tools/boundary-scan` verified locally: **0 findings (S1–S9 clean)** including all new
  files (the legacy acquisition header names are fragment-assembled per the established
  S3 pattern).

**Identifier note for the Owner:** the boundary scanner's `redis` substring rule (S3)
matches the natural identifier `PairedIsolationValveId`/`PairedWaterJetId` (the letters
"…pai-RED-IS…"), so the contract properties implementing the approved
`pairedIsolationValveId` / `pairedWaterJetId` attributes are named
`DedicatedIsolationValveId` / `ServedWaterJetId` in code (JSON example:
`dedicatedIsolationValveId` / `servedWaterJetId`). Semantics are exactly the approved
one-to-one ordinal pairing; the rename is purely mechanical and imposed by the S3 rule.
All other approved attribute names are implemented verbatim.

**Arena executed no .NET command (no SDK in the sandbox): Release build, full test suite,
locked restore, and fixture parity are Owner-local validation gates, per the standing
Stage 0.3A convention. Runtime CANNON migration NOT started (Checkpoint C NOT
AUTHORIZED). No command/write path, no device access, no Production value. PR #6 remains
OPEN — NOT MERGED.**

**Owner-local validation (2026-10-08) — CHECKPOINT B OWNER-LOCALLY VALIDATED.** After the
test-compile correction (`18f853a5`), the CA1829 count correction (`901a070`), the
behavioural test correction (`79fa0ba5` — four test defects: sensor-level
`assignedIsolationValveId` expectation, two policy-violating source-substring tests
replaced with semantic output/contract-shape tests, and orderTotal-based I7-before-I16
ordering), and the final analyzer correction (`2db853a7` — CA1865, two xUnit2029, and the
last source-substring test replaced with compiled-public-type semantics), the Owner
supplied the authoritative evidence at feature head `2db853a7`: SDK `10.0.401`; normal
restore PASS; locked restore PASS; Release build **0 warnings / 0 errors**; fresh full
.NET suite **186 total / 186 passed / 0 failed / 0 skipped**; boundary scan **0 findings
(S1–S9 clean)**; JSON examples PASS; Owner acquisition data in the migrated example
**NONE**; Working Tree CLEAN. An earlier "139 total / 139 passed" observation was
non-authoritative (`--no-build` over stale binaries) and is not recorded as validation.
The Owner's genuine lock refresh `0728df61f917ba61f6dd3b8bf6d12e68dfa01d20` (parent
`2db853a7`; exactly `tests/config.tests/packages.lock.json`: the project-graph edge
`wjss.domain → Wjss.Contracts`; valid JSON, no path, no secret, no version change) was
adopted byte-for-byte. Full record: [`docs/STAGE_0.3A-3_CHECKPOINT_B.md`](docs/STAGE_0.3A-3_CHECKPOINT_B.md).
Checkpoint C remains NOT AUTHORIZED and NOT STARTED; PR #6 OPEN — NOT MERGED.

### Stage 0.3A-3 Checkpoint C — implemented (Arena-authored; NOT compiled/executed in Arena)

**Owner instruction 2026-10-08: Checkpoint C AUTHORIZED FOR IMPLEMENTATION on the existing
PR #6 branch (required head `0c3dcea`, verified). Checkpoint C: implemented in three slices
— C1 `d378edf` (atomic migration), C2 `27591ea` (API projection tests), C3 (this docs +
Inspector commit). PR #6 remains OPEN — NOT MERGED.**

- **Schema compatibility decision: Option A — atomic schema update, no deprecated alias.**
  `wjss.snapshot/1` → `wjss.snapshot/2`, `wjss.delta/1` → `wjss.delta/2` (structural
  break: the slot-kind vocabulary became the canonical NON_SENSOR_GAP, `equipmentId`
  became `gapAnchorForWaterJetId`, Sensor records gained `assignedWaterJetId` /
  `assignedIsolationValveId`, and the Snapshot gained the static `waterJets` /
  `isolationValves` topology collections). The only consumers (development Inspector,
  TypeScript mirror, fixtures/examples/tests) migrated in the same commits; no deployed
  `/1` consumer exists. ApiVersion stays `1`.
- `packages/contracts/`: `SlotType` enum DELETED (no canonical Cannon entity remains);
  `WallMapSlot` → `PositionKind` + `GapAnchorForWaterJetId` (I7 → WJ3, I16 → WJ1);
  `CanonicalSensorMap.NonSensorGapCount` / `NonSensorGapSlots`;
  `SensorPresentationState` → `PositionKind` + assigned device fields;
  `OperationalSnapshot` gains `WaterJets` / `IsolationValves` (Snapshot-only, never in a
  Delta); `TcChannelRules` gap vocabulary; `SchemaIds` `/2`.
- `packages/domain/WaterJetTopologyCatalog.cs` (+ `TryRequireAssignment`): the single
  assignment rule — target coverage must match the Sensor wall/region and the Isolation
  Valve must derive from the WJn ↔ IVn pairing (rear-lower Sensor ⇒ WJ1/IV1 even though
  WJ1 installs front-lower).
- Runtime: state carries the approved topology through every revision (frozen, immutable,
  process-lifetime); invariants validate gaps, 8+8 ordinal topology, both-direction
  pairing and per-Sensor assignment coverage (new refusal codes `WALL_MAP_NON_SENSOR_GAPS`,
  `TOPOLOGY_WATER_JETS`, `TOPOLOGY_ISOLATION_VALVES`, `TOPOLOGY_PAIRING`,
  `SENSOR_ASSIGNMENT`); Delta application preserves the topology and never emits it;
  simulator assigns every Sensor from the catalog; stage marker
  `STAGE_03A3C_RUNTIME_TOPOLOGY`.
- Read-only status gains the gap references (orderTotal order), 8+8 counts and the full
  equipment topology with `acquisition: DEFERRED_NO_ACQUISITION_BINDING`; sensor views
  gain the assigned device fields. Routes unchanged; every mapping GET; no write/command/
  dispatch/equipment-control route; no SSE; health/readiness untouched.
- Development Inspector: "Non-sensor gaps" tile; NEW wall map with visually distinct
  `I7 · GAP` / `I16 · GAP` cells (no Sensor value/quality/TC, never labelled WJ) and a
  visually separate per-wall installation overlay ("sprays the opposite wall"); NEW
  read-only equipment topology table; Sensor table gains Assigned WJ / Assigned IV
  columns; safety footer extended. No actuation affordance exists.
- Fixtures/examples hand-mirrored to the updated generators (`snapshot.seed0.json`,
  `delta.basic.json`, `delta.gap.json`, `config/examples/sensor-map.example.json`);
  Owner-local `dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release`
  is the parity gate.
- Tests migrated to the canonical vocabulary; NEW `RuntimeTopologyTests` (runtime) and
  `RuntimeApiTopologyTests` (API); T20 rewritten as the migration-completeness proof.
- Arena verification: boundary scan **0 findings (S1–S9 clean)**; TypeScript mirror
  **executed in Arena: `npm ci` + `npm run check` green (tsc clean, 31/31)**; inline
  Inspector JS `node --check` OK; HTML balance OK; brace-balance over 36 changed C#
  files OK; `git diff --check` clean. **No .NET command ran in Arena** (no SDK):
  restore/build/tests/parity are Owner-local gates. Expected lock drift:
  `adapters/simulator/packages.lock.json` + `tests/integration/packages.lock.json` gain
  the new direct `wjss.domain` edge on the next Owner restore (never hand-edited).
  `spikes/**` untouched; no sensorparam CSV in Git; no Production value.

Full record: [`docs/STAGE_0.3A-3_CHECKPOINT_C.md`](docs/STAGE_0.3A-3_CHECKPOINT_C.md).

### Stage 0.3A-3 Checkpoint C — Owner-local validation recorded

**Stage 0.3A-3 CHECKPOINT C OWNER-LOCALLY VALIDATED (Owner-reported, 2026-10-08) — Function
Review PASSED, Logic Review PASSED, Inspector UI/UX Review PASSED (no critical UI blocker);
minor punchlist deferred and non-blocking, Inspector presentation frozen. PR #6 PREPARED FOR
FINAL SOURCE REVIEW — OPEN, NOT MERGED.**

| Item | Observed |
| --- | --- |
| Validated feature head | `162ad7ff1e5c7bab398de6c6d1a38131f17082de` (correction chain `8567a78` CS0103 `Wjss.Domain` import → `440a7fa` CS0246 `WaterJetPlacementKind` type → `162ad7f` behavioural test corrections: canonical `positionKind` raw-JSON vocabulary ×2 + semantic topology round-trip assertions) |
| Owner artifact commit | `bac36add7011c72d6ab03ea5d3e3664ed82a197c` (parent `162ad7f`): five genuine `packages.lock.json` refreshes + the regenerated `sensor-map.example.json` — valid JSON, zero package-version changes, only the expected `Wjss.Domain` project-graph edges, no absolute path, no credential; the example's only semantic drift is the corrected NON_SENSOR_GAP note (108 / 106 / 212; I7 → WJ3, I16 → WJ1) |
| Environment | .NET SDK `10.0.401`; xUnit runtime .NET `10.0.12` |
| Restore / build | normal restore **PASS**; locked restore **PASS**; Release build **PASS — 0 warnings, 0 errors** |
| Full .NET tests | **201 total / 201 passed / 0 failed / 0 skipped** |
| Fixtures | generation/update run **7 / 7**; parity after update-mode removal **7 / 7** |
| TypeScript | typecheck **PASS**; tests **31 / 31** |
| Boundary scan | **0 findings (S1–S9 clean)** |
| JSON examples | all three parsed successfully |
| Working tree | **CLEAN** after the artifact push |
| Deferred non-blocking polish | gap-cell primary label with slot id in tooltip; friendlier placement captions; vertical scrollability of the diagnostic page. Inspector presentation FROZEN for this checkpoint |

Historical failed build/test observations before the correction chain are not acceptance
evidence. Full record:
[`docs/STAGE_0.3A-3_CHECKPOINT_C.md`](docs/STAGE_0.3A-3_CHECKPOINT_C.md) §16.

### Stage 0.3A-2A Checkpoint C — Owner-local validation recorded

**STAGE 0.3A-2C OWNER-LOCALLY VALIDATED (Owner-reported, 2026-10-07)** at the genuine lock-refresh
commit `faa79145a925832baa2ac8685f7fecf7a093552d` (`chore: refresh Checkpoint C project locks`; parent `e6a5a6c`, the Inspector
layout hotfix). Arena executed no .NET command; this evidence is authoritative.

| Item | Observed |
| --- | --- |
| .NET SDK | `10.0.401` |
| Locked restore | **PASS** |
| Release build | **PASS — 0 warnings, 0 errors** |
| Fresh full .NET tests | **139 total / 139 passed / 0 failed / 0 skipped** |
| Boundary scan (S1–S9) | **0 findings** |
| Working tree after lock handoff | **CLEAN** |
| Lock-refresh commit | `faa79145a925832baa2ac8685f7fecf7a093552d` — exactly `apps/runtime/packages.lock.json` and `tests/api.tests/packages.lock.json`; genuine restore output; no package version changed; no absolute path, no credential |
| Function / Logic review | **PASSED** — `ALIVE` / `READY` / `RUNTIME_READY`; revision progression and five-second advancement PASS; Delta history bounded 64 / 64; newest-first chain continuous with `hasRevisionGap` false; Cannon projection I7 = CANNON_REAR (row 5, column 7) and I16 = CANNON_FRONT (row 5, column 16); 108 / 106 / 212 and wall totals 24 / 29 / 24 / 29 retained; Sensor values evolving; rejected transitions 0; queue 0 / 8 placeholder only; no Active Job; Pump STOPPED placeholder only; no command or write path |
| Inspector UI review (1920 x 1080) | **PASSED — punchlist CLOSED** — no critical UI blocker; the Runtime state layout correction holds; the authorized minor presentation punchlist was applied and closed by the final Owner visual review at `b97d05a` |
| Owner final visual review (2026-10-07) | **FINAL OWNER VISUAL REVIEW (2026-10-07): Inspector UI/UX review PASSED; the minor punchlist is CLOSED.** Verified visually at 1920 x 1080: human-readable Foundation captions present; Queue and Pump placeholder explanations visually secondary; `SENSOR VALUES — SYNTHETIC EVOLUTION` title present; Foundation Metrics complete two-column layout; Runtime State uses the available width; no one-word-per-line wrapping remains; Snapshot compact; Delta and Sensor tables retain card-local scrolling; `HH:mm:ss.mmm` timestamps; Delta chain displays clean; I7 and I16 are the visible Cannon labels; synthetic UNCERTAIN and BAD quality states visible with reasons; no page-level horizontal scroll; safety footer and read-only boundary visible; no Critical UI blocker remains. One **optional future polish** item is recorded and explicitly non-blocking (top-row vertical balance, because Foundation Metrics is taller than Runtime State): it is deferred, does not require a change round, and is not authorised as part of Checkpoint C. |
| Test accounting | 129 (Checkpoint B) + 9 (Checkpoint C additions) + 1 (Delta-feed continuity projection test) = **139** |

PR #5 remains **OPEN — NOT MERGED**; `TEST_HARDWARE` and `PRODUCTION` remain **NOT AUTHORIZED**.

### Stage 0.3A-2A Checkpoint C — read-only Runtime API and development Runtime Inspector (source authored in Arena)

**SOURCE AUTHORED IN ARENA; NOT COMPILED and NOT EXECUTED there.** Owner-local validation has
since been recorded in the entry above: at the lock-refresh commit the delivered sources build
with 0 warnings / 0 errors and the fresh full .NET suite passes 139 / 139. That evidence is the
Owner's; nothing in the entry below is an Arena build, test or UI result.

Delivered on the same branch and pull request (#5) as Checkpoints A and B, in two slices:

- **C1+C2 — `06aca79d03c57b703768afffecf95735de7a91d5`** (`feat(runtime): compose the simulator
  runtime, lifecycle and read-only API`): `apps/runtime/RuntimeHostOptions.cs` (explicit
  synthetic configuration with safe development defaults and clear refusals),
  `apps/runtime/SimulatorRuntime.cs` (deterministic composition of the initial revision,
  single-writer evolution lifecycle: one non-overlapping loop, one accepted tick = one committed
  revision, refusal advances nothing and emits no Delta, explicit cancellation, observed tick
  exceptions, clean shutdown, structured startup/fatal fault codes),
  `apps/runtime/RuntimeApiResponses.cs`, the rewritten `apps/runtime/Program.cs` (validate →
  compose → start → listen, with `GET /api/v1/snapshot`, `GET /api/v1/runtime`,
  `GET /api/v1/deltas`, `GET /health/live`, `GET /health/ready` and one shared JSON policy),
  the read-only route identities in `packages/contracts/ApiRoutes.cs`, the health-payload
  documentation update, the `STAGE_03A2C_RUNTIME_API` marker, and the test sources.
- **C3 — the commit that carries this entry**: the development Runtime Inspector page
  (`apps/runtime/Inspector/index.html`, served at `GET /inspector`), the Inspector source test,
  and Owner-local runbook section 14 with the Function/Logic/UI review checklist.

**Read-only by construction.** No write, command, dispatch, queue, job, pump, valve, axis, Safe
Return or configuration route exists; no constant is declared for one. Every surface described
here observes the Runtime. `TEST_HARDWARE` and `PRODUCTION` remain **NOT AUTHORIZED** and are
refused before any port is bound. SSE is not implemented; the Inspector polls with GET requests
about once per second and keeps only a bounded number of Delta rows.

**Subsequently verified Owner-locally (see the entry above):** every .NET claim (restore, Release
build, xUnit execution of these tests), the Inspector browser behaviour and layout at
1920 x 1080, and the two expected lock refreshes (`apps/runtime/packages.lock.json`,
`tests/api.tests/packages.lock.json`) — genuine restore output, committed as `faa79145`, never
hand-edited. Arena still executed nothing itself.

### Stage 0.3A-2A Checkpoint B — Owner-local validation recorded

**STAGE 0.3A-2A CHECKPOINT B OWNER-LOCALLY VALIDATED (Owner-reported, 2026-10-07).** Validated
head `cfa6d4a376bbf10a87db2349045cb6b9b57bb544` (the consolidated correction of the Checkpoint B
feature commit `619f999`), on the Owner-locally validated Checkpoint A store.

| Item | Observed |
| --- | --- |
| Release build | **PASS — 0 warnings, 0 errors** |
| Full .NET tests | **129 total / 129 passed / 0 failed / 0 skipped** |
| Locked restore | **PASS** |
| Lock drift | **NONE** |
| Boundary scan | **0 findings (S1–S9 clean)** |
| Working Tree | **CLEAN** |

Three correction rounds were validated by that run: `dd45bf6` (CS0102 — the `DeltaApplyOutcome`
success factory renamed to `Success`; the `Applied` outcome property retained),
`8692b77` (test-source alignment — named `seed:` arguments and four CA1861 constant arrays
hoisted to named `private static readonly` fields) and `cfa6d4a` (consolidated).

**Recorded truthfully from the final diff:** the only Product behaviour change in those rounds is
inside `RuntimeDeltaHistory.CatchUpFrom`, which now selects the Delta that **continues** the
consumer revision (`previousRevision` match) instead of the Delta whose own revision equals it —
removing a spurious fresh-Snapshot result and a non-terminating walk while leaving gap detection,
eviction, ordering, `Find` semantics and the revision sequence unchanged. The other five findings
were test-side: two structural `TrendPoint` comparisons through the shared
`RuntimeTestFixture.AssertTrendPointsEquivalent` helper (`TrendPoint.Series` is an array, so
record equality compared it by reference) and three re-derived revision expectations (the chain
starts at revision 2, so capacity-3 retention is 4/5/6 and catch-up from revision 3 reaches 6).

This closeout records evidence only: no runtime, contract, test, fixture, lock-file or tooling
change. **Checkpoint B is complete for a development checkpoint — SUBMITTED FOR OWNER REVIEW,
NOT MERGED.** **Checkpoint C (read-only Runtime API + development Runtime Inspector) is
OWNER-AUTHORIZED**; TEST_HARDWARE and PRODUCTION remain **NOT AUTHORIZED**. Arena ran no .NET
command of its own and claims no compile or test result.

### Stage 0.3A-2A Checkpoint B — Deterministic synthetic evolution and Snapshot/Delta foundation (`feat(runtime): add deterministic synthetic evolution and snapshot/delta foundation`)

**Source-only checkpoint (Arena authored; NOT COMPILED and NOT EXECUTED in Arena).** Under the
Owner's 2026-10-07 instruction, Checkpoint B continues the Owner-locally validated Checkpoint A
store with deterministic synthetic evolution and the Snapshot/Delta foundation, SIMULATOR only.

CHANGED

- `packages/application/Runtime/RuntimeSyntheticEvolution.cs` (new): `SyntheticEvolutionRules`,
  `SyntheticEvolutionResult`, `SyntheticTickOutcome` and the deterministic tick. Presentation
  values are pure functions of (committed state, explicit seed, explicit tick number, explicit
  tick instant); the score is a unitless synthetic development value in 0.0-100.0 built from a
  bounded tenths-of-a-point walk with explicit culture-invariant rounding; the quality schedule
  is per-Sensor and deterministic; `GOOD` writes the validated value, `UNCERTAIN` presents a
  fresh value while keeping the last validated classification basis, `STALE` and `BAD` present no
  value, and an absent or expired basis is reported as no basis at all. Refusals
  (`EVOLUTION_TICK_SEQUENCE`, `EVOLUTION_TICK_TIME`, domain profile gate) return no candidate.
- `packages/application/Runtime/RuntimeDelta.cs` (new): the apply-safe Delta with whole-record
  replacements, retained previous records for clean reversal, and the three-state Active Job
  slot (`DeltaJobEncoding` / `DeltaJobState`) that mirrors the contract exactly — absent =
  unchanged, object = replacement, explicit null = clear. No second clear flag exists.
- `packages/application/Runtime/RuntimeDeltaProjector.cs` (new): the only Delta envelope
  producer, with the Runtime-side Delta and the `wjss.delta/1` wire projection (unchanged
  sections omitted, no fabricated runtime diagnostics block).
- `packages/application/Runtime/RuntimeDeltaApply.cs` (new): strict total-or-nothing
  application; a revision mismatch yields a machine-readable `RESNAPSHOT_REQUIRED` result and
  applies nothing; a self-inconsistent Delta is refused, never normalized; the reconstructed
  revision must pass the same structural invariants the store enforces.
- `packages/application/Runtime/RuntimeDeltaHistory.cs` (new): bounded in-memory history
  (newest-first, explicit minimum/maximum/default capacities independent of the revision
  history, deterministic oldest eviction, refused transitions never recorded) plus
  `CatchUpFrom`, which returns a gapless apply-order chain or the fresh-Snapshot-required result
  and never infers a missing Delta.
- `packages/application/Runtime/RuntimeTrendBuffer.cs` (new): the single bounded-append rule
  shared by evolution and Delta application.
- `packages/application/Runtime/RuntimeRefusalCodes.cs`, `RuntimeLimits.cs`: additive codes and
  Delta-history capacity bounds; no existing member changed.
- `packages/time/UtcTimestamps.cs`: strict, culture-invariant wire-timestamp parsing.
- `tests/runtime.tests/`: `SyntheticEvolutionTests`, `SnapshotDeltaTests`, `DeltaApplyTests`,
  `DeltaHistoryTests`, `RuntimeDeltaTestFixture`.
- `docs/STAGE_0.3A_PLAN.md` §5b and `docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md` §13 record the
  scope and the Owner-local validation plan; `docs/CURRENT_STATE.md` §11.6 records the position.

UNCHANGED

- Contracts (`packages/contracts/**`) including every fixture, `Directory.Build.props`,
  `global.json`, `TreatWarningsAsErrors`, `AnalysisLevel`, all `packages.lock.json`,
  `apps/runtime/Program.cs`, `spikes/**`, the protected baseline shape (108 slots / 106 Sensors /
  212 channels / I7+I16 / wall counts 24-29-24-29 / Single Active Job / head-only GlobalQueue),
  and the SIMULATOR-only startup refusal of TEST_HARDWARE and PRODUCTION.

NOT VERIFIED

- Every .NET claim: Arena ran no restore, build or test. Owner-local validation per runbook §13
  remains the validation of record. Checkpoint C is not started and stays NOT AUTHORIZED.

### Stage 0.3A-2A Checkpoint A — Owner-local validation recorded (documentation-only closeout)

**STAGE 0.3A-2A CHECKPOINT A OWNER-LOCALLY VALIDATED (Owner-reported, 2026-10-07).**
Validated feature head `a512aa76c4b2d7633d2a83b10c523c318b3a420e`; genuine lock-refresh commit
`55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c`. .NET SDK **10.0.401**; xUnit observed runtime
**.NET 10.0.12**; Release build **PASS — 0 warnings, 0 errors**; full .NET suite
**101 total / 101 passed / 0 failed / 0 skipped**; **locked restore PASS**; boundary scan
**0 findings (S1–S9 clean)**; Working Tree **clean** after the Owner-local lock commit and push.
The lock refresh changed exactly one file — `tests/runtime.tests/packages.lock.json` — adding the
`wjss.adapters.simulator` Project dependency with `Wjss.Contracts` and `Wjss.Time`; no absolute
Owner-local path and no unexpected source or fixture change.

Checkpoint A correction chain (each a focused commit on PR #5, authored in Arena, validated
Owner-locally as part of the run above):

| Commit | Correction |
| --- | --- |
| `f51408e` | Checkpoint A feature commit (Runtime State Store + deterministic SIMULATOR initial state + `wjss.snapshot/1` projection) |
| `b389805` | CA1859 — `FreezePoints` returns the concrete `ReadOnlyCollection<TrendPoint>` |
| `93e8f24` | CS0051 — the nested theory parameter enum `Tamper` is `public` |
| `b75bccb` | CS1061 + CA1859 — the synthetic-example test deserializes the slots node explicitly and returns the concrete `List<SensorMapSlotExample>` |
| `a512aa7` | SensorChannels tamper isolation — the fixture preserves ScanOrder/identity so the pinned `SENSOR_TC_CHANNELS` code is the deterministic refusal |
| `55d3b8b` | Genuine Owner-local lock refresh (one file, one Project dependency) |

This closeout records evidence only: no runtime, contract, test, fixture, lock-file or tooling
change, and no version claim. **Checkpoint A is complete for a development checkpoint —
SUBMITTED FOR OWNER REVIEW, NOT MERGED.** Checkpoint B (deterministic synthetic evolution +
Snapshot/Delta foundation) is now **AUTHORIZED**; Checkpoint C remains **NOT AUTHORIZED**;
TEST_HARDWARE and PRODUCTION device access remain **NOT AUTHORIZED**. Arena ran no .NET command
of its own and claims no compile or test result.

### Stage 0.3A-2A — Runtime State Foundation, Checkpoint A (`feat: add runtime state store and deterministic simulator initial state`)

**Source-only checkpoint (Arena authored; NOT COMPILED and NOT EXECUTED in Arena).** Under the
Owner's 2026-10-07 instruction, Stage 0.3A-2 is delivered in three checkpoints (A → B → C) on
one branch, SIMULATOR profile only. This is Checkpoint A: the in-memory Runtime State Store and
the deterministic SIMULATOR initial state, projected as `wjss.snapshot/1`.

Added:

- **Runtime State Store** (`packages/application/Runtime/`): single authoritative writer issued
  once per store (`WRITER_ALREADY_ACTIVE` on a second request); immutable `RuntimeState`
  revisions with read-only collection wrappers copied at the ingest point; monotonic and
  gapless revision progression (`REVISION_NOT_NEXT`, `REVISION_NOT_MONOTONIC`); full validation
  before the published reference is swapped, so a refused commit leaves state, revision and
  history untouched (atomic, no partial commit); bounded revision-activity history
  (1–1024, default 64) and bounded trend window; no static mutable state.
- **Protected-baseline invariants** (`RuntimeStateInvariants`): 108 logical slots, 106 Sensor
  projections in ScanOrder 1–106, 212 distinct Thermocouple channels, Cannon slots only at
  logical I7/I16, wall counts 24/29/24/29, queue capacity 8 with contiguous positions, bounded
  trend width, alarm counters equal to their items, device health only for configured devices,
  SIMULATOR-only states. Each refusal carries a stable machine code (`RuntimeRefusalCodes`).
- **Deterministic SIMULATOR initial state**: `packages/time/UtcTimestamps.cs` (one wire
  timestamp encoding), `RuntimeStateComposer` (initial revision 1; no Active Job; GlobalQueue
  empty at revision 0; every sequence control disabled with
  `CONTROL_NOT_IMPLEMENTED_STAGE_03A_2A`; Pump STOPPED with `ready = false`, pressure null and
  zeroed setpoint/band because no pump configuration is published in this substage; communication
  health publishes no fabricated device evidence; bounded trend window empty) and
  `RuntimeSnapshotProjector` (envelope identity, explicit-null `activeJob`, SSE/historian blocks
  reported unwired). The composition runs `Wjss.Domain.ProfileStartPolicy`, so TEST_HARDWARE and
  PRODUCTION are refused at composition exactly as they are refused at startup.
- **Simulator synthetic source** (`adapters/simulator/Synthetic/`): `SyntheticSeed`,
  `DeterministicValueSource` (splitmix64) and `SyntheticSensorMap` (canonical map + seeded
  initial Sensor projection). The map composition is parity-tested against the committed
  `config/examples/sensor-map.example.json`; the fixture generator is untouched and its
  unification with this source is recorded `[OPEN]`.
- **Tests** (`tests/runtime.tests/`, source-authored, not executed in Arena): state-store
  contract, protected-baseline invariants with machine-code assertions, snapshot projection,
  deterministic initial state and synthetic-map parity. `StageMarkerTests` migrates the
  Stage 0.3A-1 "empty staging area" assertion to the state-foundation identity.
- **Records**: `docs/CURRENT_STATE.md` §11.5 + §12.23, `docs/STAGE_0.3A_PLAN.md` §5/§9,
  runbook §12 (Owner-local commands for Checkpoint A), adapter README.

Arena verification: boundary scan **0 findings (S1–S9 clean)**, exit 0 on the changed worktree;
C# balance/whitespace/final-newline sweep clean; `git diff --cached --check` clean;
contract-member cross-check performed by review. **NOT VERIFIED:** every .NET claim (restore,
Release build, xUnit run), the `tests/runtime.tests/packages.lock.json` refresh required by the
new simulator project reference (real Owner-local restore only — never hand-edited), and
Checkpoints B and C (not started). Owner-local validation: runbook §12.

### Stage 0.3A-1 — Final Owner-Local Evidence Closeout (`chore: close Stage 0.3A-1 validation evidence`)

**STAGE 0.3A-1 OWNER-LOCAL VALIDATION PASSED.** Owner-local at predecessor `685e056` /
final test state `a9ef185` (2026-10-07): full Release build **PASS — 0 warnings, 0 errors**
(all 12 projects, 3.7 s); fresh full .NET suite **61 total / 61 passed / 0 failed / 0
skipped** (5.5 s) — authoritative because the build completed successfully immediately
before the run. Environment: .NET SDK **10.0.401**, xUnit observed runtime **.NET 10.0.12**.
TypeScript **24/24**; FixtureEmission parity **7/7**; boundary scan **0 findings (S1–S9
clean)**. Health: live HTTP 200 (`status=ALIVE`, `deviceProfile=SIMULATOR`,
`runtimeImplemented=false`, `stageMarker=STAGE_03A1_SKELETON`); ready HTTP 503
(`RUNTIME_NOT_IMPLEMENTED`). Profile gates fail-closed: SIMULATOR starts; `TEST_HARDWARE`
and `PRODUCTION` refused with exit 2; invalid profile refused with exit 4; no silent
fallback under `--no-launch-profile`. Port collision on 5181: explicit refusal, exit 3.
The tcChannels structural contract re-verified end to end (108 slots / 106 sensors /
2 cannons / exact-two-element arrays / 212 total = 212 unique / 0 duplicates / cannons
I7+I16 channel-free / walls 24-29-24-29; first-sensor shape CLR `System.Object[]`, JSON
`["SYN-TC-01:CH00","SYN-TC-01:CH01"]`).

Closeout content (this commit; documentation + SDK pin only — no behavior, contract,
generator, test, or dependency change):

- **`global.json` (new, repository root)**: pins the exact Owner-validated stable SDK
  `10.0.401` with `rollForward: latestPatch` — the narrowest policy consistent with the
  Stage 0.3A decision that an SDK pin is only added once Owner-validated (the runbook's
  former "intentionally no global.json" rule is superseded by this Owner decision); no
  Preview or RC SDK is referenced.
- **Genuine Owner-local artifacts recorded** (transferred by handoff commit `488b98fb…`,
  verified in Arena, never generated here): exactly **12** `packages.lock.json` files —
  valid JSON, correct per-project paths, framework sections consistent with each csproj
  TFM (`net10.0-windows7.0` for the kiosk), zero absolute local paths, zero credential
  findings, every Direct entry exactly matching the `Directory.Packages.props` pins
  (`Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`)
  with no unapproved package, and locked-restore policy still enabled; plus the **3**
  .NET-generated array fixtures (`config/examples/sensor-map.example.json`,
  `packages/contracts/fixtures/delta.basic.json`, `packages/contracts/fixtures/snapshot.seed0.json`)
  — all §7 shape/identity checks pass in Arena and the TypeScript validator accepts the
  transferred files (24/24). The previously rejected comma-delimited fixtures are not
  used; the `fixtureStatus` PROVISIONAL label remains inside the files because the
  generator constant still writes it — removing that marker is a later Owner call.
- **Environment finding recorded** (runbook §0A, already process-scope only): an external
  `TargetPath` process variable from another local toolset was imported by MSBuild as a
  global property and hijacked output resolution; removal from the current PowerShell
  process (`Remove-Item Env:TargetPath -ErrorAction SilentlyContinue`) resolved it. Not a
  WJSS dependency or repository defect; no User/Machine deletion, no repository-side
  override, no suppression — and none is ever added.
- **Test policy agreed for subsequent checkpoints** (runbook §11): automated tests only for
  product behavior, safety invariants, public/wire contracts, deterministic state
  transitions, cross-language parity, and meaningful product-impacting regressions;
  compiler, analyzers, parsers, the boundary scanner, and focused semantic review carry
  syntax/imports/analyzer-pattern/formatting/private-detail concerns instead. Existing
  contract, mapping, startup-safety and parity tests are retained; no new tests were added
  in this closeout.
- **Wording residues corrected**: the CHANGELOG header placement sentence (above) now
  states the validated repository contents, and ADR-0016 item 3 no longer describes an
  `Environment.GetEnvironmentVariable` WebView2 probe in the 0.3A-1 kiosk (the stub contains
  no environment probing of any kind; runtime detection begins at 0.3A-4 per ADR-0013);
  `packages/contracts/fixtures/README.md`'s validation pointer now cites the correct runbook
  section. README, CURRENT_STATE, STAGE_0.3A_PLAN, the validation runbook, ADR-0014 and the
  fixture/tooling READMEs move to the validated status; dated per-round records are kept
  untouched as state-at-the-time evidence.

**PR #4: READY FOR OWNER MERGE — NOT MERGED** (the Agent never merges; merge, and any
decision to drop the remaining `fixtureStatus` marker, is an Owner call). Stage 0.3A-2
**NOT AUTHORIZED**; Production device access **NOT AUTHORIZED**; `spikes/**` immutable
(roll `101be71b…3fbc5` re-verified).

### Stage 0.3A-1 — Round 6d cannon logical ordering test correction (`fix: order cannon assertions by logical position`)

Owner-local at `39577e2` (2026-10-07): Release build **PASS 0/0**; fresh full suite
**61 / 60 passed / 1 failed**; parity 7/7; boundary scan clean. The single failure was a
test-ordering defect in `Example_Mapping_Validates_Through_The_Typed_Contract`: the cannon
label sequence was produced by `OrderBy` on the `logicalLabel` STRINGS, which sorts
lexicographically to `I16, I7` while the accepted logical order is `I7, I16`. The
mapping and contract were correct and remain unchanged.

Correction (test source only): the cannon assertion pipeline now orders the cannon SLOTS
by structured position — `OrderBy(LogicalRow).ThenBy(LogicalColumn)` — and reads
`logicalLabel` from that order; the expected sequences stay `[7, 16]` / `["I7","I16"]`.
Per-slot pairing is asserted explicitly (I7 at row 5 / column 7 / `CANNON_REAR`; I16 at
row 5 / column 16 / `CANNON_FRONT`), and cannons gained a `sensorId`-absence assertion;
row-5, no-`tcChannels`, and all canonical counts (108/106/2, 212 total = 212 unique, walls
24-29-24-29) remain. Ordering defect-class sweep over the full Stage 0.3A-1 test tree:
the only other `OrderBy` sites sort by structured numeric fields (`WallColumns` ranges by
`FirstColumn`) or are input parsing for the product API under test — no lexicographic
ordering of labels/IDs, no numeric parsing out of labels where structured fields exist, no
sequence/set mismatches — remains. No expectation was flipped to match text sorting, no
padding normalization, no natural-sort dependency. Product contracts, `TcChannelRules`,
generator, TypeScript mirror, fixtures, examples, lock files and spikes: zero diff.
Final Owner-local Release build + fresh full-suite rerun REQUIRED; no .NET/xUnit success
claimed in Arena.

### Stage 0.3A-1 — Round 6c structural JSON assertion correction (`fix: assert tc channel JSON shape structurally`)

Owner-local state at `0b088ad` (2026-10-07): full Release build **PASS, 0 warnings / 0
errors**; fresh full test run 61 total / 59 passed / 2 failed / 0 skipped. Both failures —
`Serializing_Valid_Mapping_Emits_Two_Item_Arrays_And_Never_A_Comma_Scalar` and
`Example_Mapping_Validates_Through_The_Typed_Contract` — were test-assertion defects: they
tried to disprove the scalar shape by matching serialized text
(`Assert.DoesNotContain("\"tcChannels\": \"", …)`), which crosses the indented writer's
formatting whitespace and matched into the first array item. The accepted contract itself is
verified and frozen: exact-two-element arrays, CLR `System.Object[]`, 108/106/2 composition,
212 total = 212 unique channels, 0 duplicates, I7/I16, walls 24-29-24-29; TypeScript 24/24;
fixture parity 7/7; boundary scan clean.

Corrections (test sources only):

- `TcChannelContractTests.cs`: every shape claim is now proven structurally from parsed
  documents — presence, `JsonValueKind.Array`, never `JsonValueKind.String`, exactly two
  `JsonValueKind.String` items, non-empty and whitespace-pure values, distinct pairs,
  deterministic front-then-rear order (compared on the `:CH<digits>` values, not on text),
  cannon `tcChannels` key absence, and the full canonical 108/106/2/106-arrays/0-scalars/
  212/212/0-duplicates/walls mapping over the committed example.
- Defect-class sweep over the Stage 0.3A-1 test tree removed the remaining two assertions of
  the same class: the redundant serialized-text follow-up in `Delta_RequiredTest3`
  (the structural presence+`JsonValueKind.Null` pair already proves it) and the whitespace-
  baked `deviceProfile` text guards in `ExampleConfigTests`, which now recursively scan the
  parsed example JSON (compact-form evasion closed). Intentional source-preservation text
  guards (Kiosk DPI, Runtime stub, prohibited-vocabulary scan) and the JsonNode structural
  assertions of round 6b are unchanged; no comma-splitting, no normalization, no suppression,
  `TreatWarningsAsErrors` and `AnalysisLevel` untouched, no CA1861-style inline constant
  arrays added, all `[Fact]`/`[Theory]` files carry the xUnit import.
- Contract (`packages/contracts/TcChannels.cs`), generator, serializer, fixtures, examples,
  TypeScript mirror, lock files, and package versions are **unchanged**.

Owner-local final build + fresh full-suite rerun REQUIRED; no .NET build or xUnit success is
claimed in Arena (round-6c C# is compile-reviewed only, with its structural assertions
mirrored green in Python against the committed example).

### Stage 0.3A-1 — Round 6 Owner-local test correction (`fix: align config tests with tc channel arrays`)

Owner-local result for round 6 (2026-10-07): the tcChannels structured-array contract is
**accepted** — the .NET generator produced 108 slots / 106 sensors / 2 cannons, 106 sensors
with `tcChannels` arrays (CLR shape `System.Object[]`, JSON `["…:CH00","…:CH01"]`), 212 total
and 212 unique channels, 0 duplicates, 0 wrong-count sensors, cannons I7/I16, walls
24/29/24/29; TypeScript validation 24/24, fixture parity 7/7, boundary scan S1–S9 clean. The
`--no-build` suite run (49/50) is recorded **non-authoritative**: `Wjss.Config.Examples.Tests`
could not rebuild from current source, so a stale assembly ran. Two config-test corrections,
source-only — contract, generator, fixtures, serializer, and package versions untouched:

- **CA1861 compile errors** in `tests/config.tests/TcChannelContractTests.cs` (lines 166–167):
  the cannon-identity expectations were inline constant arrays passed to `Assert.Equal`.
  Extracted to named `private static readonly` fields (`ExpectedCannonLogicalColumns`,
  `ExpectedCannonLogicalLabels`); the comparison only enumerates them, so sharing the
  immutable instances is safe. No suppression, no severity or `TreatWarningsAsErrors` change.
- **Stale `tcChannels` assertion shape** in `tests/config.tests/ExampleConfigTests.cs`: the
  pre-correction assembly called `GetValue<string>()` on the `tcChannels` node and failed
  with "The node must be of type 'JsonValue'". The test now inspects the accepted structure
  directly: `tcChannels` must cast to a `JsonArray` (a scalar comma-delimited string fails
  as a shape error), exactly two non-empty string items per sensor, distinct within the pair,
  212 total items across 106 sensors, 212 globally unique, and cannon slots must not carry
  the key. Canonical counts and the typed `TcChannelRules.RequireValidMapping` verification
  remain. No comma-splitting was reintroduced.

Owner-local clean full Release build + fresh full-suite rerun REQUIRED after this correction;
no .NET build or xUnit success is claimed in Arena.

### Stage 0.3A-1 — Thermocouple channel contract shape correction (round 6)

Owner-local state (2026-10-07, rounds 3–5): after the two analyzer errors and the
`TargetPath` environment root cause were cleared, the full solution Release build
passed 0 warnings / 0 errors, the corrected checkpoint suite passed 50/50 and
fixture/delta parity 7/7 — superseding the explicitly non-authoritative `--no-build`
42/49 result. Residual drift on the three provisional files surfaced a real shape
defect: the sensor-map example encoded each sensor's thermocouple pair as one
comma-delimited string and its test asserted that by splitting the string.

- **Contract correction**: `tcChannels` in the sensor-map configuration example is a
  structured JSON array of exactly two channel strings per SENSOR slot (index 0 = lower
  channel index / front, index 1 = higher / rear); CANNON slots never carry it. New
  `SensorMapSlotExample` record + `TcChannelRules` in `packages/contracts/TcChannels.cs`
  reject scalar strings (deserialization cannot bind them), wrong lengths, empty
  entries, per-sensor and cross-sensor duplicates, and cannon-as-sensor composition;
  canonical totals 108 slots / 106 sensors / 2 cannons / 2×2 channels / 212 globally
  unique channels / walls 24-29-24-29. Snapshot/delta presentation keeps the existing
  separate `tcFrontChannel`/`tcRearChannel` fields — not renamed, not merged.
- Generator (`tests/integration/ExampleFiles.cs`) emits the array form and validates the
  mapping through `TcChannelRules` before serializing. `config/examples/sensor-map.example.json`
  was regenerated in Arena in the new form so TypeScript validation can run; it remains
  PROVISIONAL — final truth comes from the Owner regeneration sequence after the full
  Release build (rejected comma-delimited fixtures retained as validation evidence).
- Tests: focused serialization/deserialization/rejection coverage for every invalid
  channel shape plus the full typed mapping validation of the committed example
  (`tests/config.tests/TcChannelContractTests.cs`; example test upgraded from string
  splitting to typed contract validation). TypeScript validator + types mirror the
  contract (scalar-string and every array-shape negative; positive canonical totals),
  tests 15 → 24 green. ADR-0014 decision 8 records the structural rule and that the
  earlier comma-delimited form was detected by Owner validation and rejected pre-merge.
- No Product project, `.csproj`, `.sln`, TFW, warning-policy, package-version, or
  dependency changes; no lock file touched; no fixture under `packages/contracts/fixtures/`
  modified; spikes immutable (roll `101be71b…3fbc5`). C# sources compile-reviewed only:
  **not built in Arena**. Owner-local rerun of the full validation sequence REQUIRED.

### Stage 0.3A-1 — Owner-local build & contract-test correction round 5

**Test-run evidence recorded as NON-AUTHORITATIVE.** `dotnet test ... --no-build --no-restore`
returned 49 / 42 pass / 7 fail / 0 skip, but the preceding full-solution Release build had
**not** passed — assemblies may be stale (notably `Wjss.Config.Examples.Tests` "passed" though
its source did not compile in that build). This result is explicitly **not** the checkpoint
test record. Corrections in this round (source only; contract wire semantics untouched — the
absent/object/null triple stands and `activeJobCleared` stays absent):

- **Config test assembly anchor:** the round-4 `ConfigTestAssemblyMarker` + shared
  `ConfigTestPaths.RepoRoot()` mechanism is verified in place for both preservation tests
  (marker declared exactly once; zero code references to `ExampleConfigTests` remain; no fake
  substitute class; repository-source discovery unchanged; no absolute paths; no Product
  changes) — requirement completed in `12e3cfd`, confirmed here.
- **Presence-aware JSON null assertions (3 failing tests):** `JsonNode` indexers map JSON
  null to a CLR-null reference, so `json["activeJob"]!.GetValueKind()` NRE'd. Wire assertions
  in `ContractEncodingTests` now go through `JsonDocument`/`JsonElement` using the exact
  pattern — (A) absent: `TryGetProperty` false; (B) explicit null: true +
  `JsonValueKind.Null`; (C) object: true + `JsonValueKind.Object` — across serialization AND
  deserialization round-trip of all three `activeJob` states (new stability assertions in
  required-test-4).
- **`Optional<T>` payload equality:** `_value == other._value` compared present payloads by
  reference for the unconstrained `T`, wrongly splitting equal records (round-5 owner-local
  failure). `Equals` now delegates present-payload comparison to
  `EqualityComparer<T>.Default`; `GetHashCode` mirrors it (equal values hash equal); `==`/`!=`
  remain defined as `Equals`/`!Equals`; `Present(null)` still prohibited; wire serialization
  byte-identical. No recursive custom deep-equality invented — payloads keep their own .NET
  equality; new focused tests cover delegation for value-equal and reference-only payloads.
- **Fixture drift (3 files) retained as EXPECTED provisional evidence:**
  `snapshot.seed0.json`, `delta.basic.json`, `sensor-map.example.json`. Fixtures were NOT
  touched in Arena; regeneration remains gated on (1) full Release build PASS and (2) all
  non-parity test projects PASS first — now written into the runbook step 5 ordering gate.

**Still required:** a clean full-solution Release build, then a `dotnet test` WITHOUT
`--no-build` against fresh assemblies; only then may parity/fixture status be judged. No .NET
success is claimed anywhere in this entry.

### Stage 0.3A-1 — Owner-local build correction round 4

With the external process `TargetPath` override gone, the Owner-local build reached **eleven
projects compiling successfully**: `Wjss.Time`, `Wjss.Kiosk`, `Wjss.Contracts`,
`Wjss.Adapters.Simulator`, `Wjss.Domain`, `Wjss.Runtime.Api.Tests`, `Wjss.FixtureEmission.Tests`,
`Wjss.Runtime.Core`, `Wjss.Domain.Tests`, `Wjss.Runtime.Core.Tests`, `Wjss.Runtime` —
confirming the ProjectReference graph, the WinForms DPI correction, `Optional<T>` + converter,
and the CA1873 logging correction, and that no Product workaround for KMotion/`TargetPath` is
required (none was added; the absolute toolset path appears only in the dated Owner evidence
record). The single remaining failure was `Wjss.Config.Examples.Tests`: both
source-preservation tests referenced `ExampleConfigTests.RepoRoot()` across namespaces
(`Wjss.Config.Tests` vs `Wjss.Config.Examples.Tests`) — CS0103 ×2. Corrected minimally with
the Owner's preferred mechanism: new `ConfigTestAssembly.cs` defines `internal sealed class
ConfigTestAssemblyMarker` (a stable assembly anchor — no empty/fake substitute class, nothing
dependent on a renamable test-class name) plus `internal static ConfigTestPaths.RepoRoot()`,
anchored at `typeof(ConfigTestAssemblyMarker).Assembly.Location` with the identical
walk-up-to-`.sln` discovery; both tests now share it, `ExampleConfigTests` itself is
unchanged, all existing DPI/Runtime/configuration tests retained, no new dependency, no
absolute paths, no weakened assertions, no Product-project logic. **Full Owner-local Release
build + test re-run still required** — eleven green compiles are Owner evidence, and the
config test project plus every test execution await the rerun.

### Stage 0.3A-1 — Owner-local build correction round 3

Root cause of the round-2 cascading `Wjss.Contracts` reference failures was identified as
**local environment contamination**: a `TargetPath` variable defined in the Owner's active
PowerShell process (set by an unrelated external motion-control toolset) was imported by
MSBuild as a global property, hijacking output resolution. The Owner removed it from the
process scope; the cascade vanished and ten projects then compiled: `Wjss.Time`,
`Wjss.Kiosk`, `Wjss.Contracts`, `Wjss.Domain`, `Wjss.Adapters.Simulator`,
`Wjss.Runtime.Core`, `Wjss.Runtime.Api.Tests`, `Wjss.Domain.Tests`,
`Wjss.FixtureEmission.Tests`, `Wjss.Runtime.Core.Tests`. That first green compile proves the
`ProjectReference` paths are correct. The repository adopts **no** vendor path,
`ReferencePath`/`TargetPath` override, or KMotion-related reference of any kind — the only
change is documentation: the runbook gains a mandatory pre-build environment check (process-
scope removal only; never a permanent User/Machine deletion) and the lock-file policy now
states that restore-generated `packages.lock.json` files stay untracked until the full
validation passes. Two genuine source defects from the same round were fixed without any
suppression or severity change: (1) `KioskDpiConfigurationTests.cs` was missing its
per-file `using Xunit;` import (Fact resolution) — added, matching the existing explicit-using
convention, no GlobalUsings file introduced; (2) CA1873 in `apps/runtime/Program.cs` — the
startup `LogInformation` argument evaluation (including `{StartedUtc:O}` formatting) is now
wrapped in `if (app.Logger.IsEnabled(LogLevel.Information))` guard at the exact level,
template/structured payloads/behaviour unchanged; four new static source tests
(`RuntimeStubPreservationSourceTests`) assert health/live, health/ready, profile-refusal
ordering with exit codes 4/2/3, loopback binding, and the absence of suppression remain
intact. **A full Owner-local Release build + test rerun is still required**; the generated
lock-file set is untouched by this repository.

### Stage 0.3A-1 — Owner-local compile correction (`fix: resolve Owner-local .NET compile blockers`)

**First real .NET evidence.** The Owner ran
`dotnet build .\WaterJetSentinelSuite.sln --configuration Release --no-restore` locally
(twice, identical results): **BUILD FAILED** with exactly two errors —
(1) `Wjss.Kiosk` (net10.0-windows) **WFO0003**: high-DPI settings declared in
`app.manifest` must move to `Application.SetHighDpiMode`/`ApplicationHighDpiMode`;
(2) `Wjss.Contracts` **CA2231** (warnings-as-errors): `Optional<T>` overrides
`Equals` without equality operators. `Wjss.Time` compiled successfully. Both corrected as
source only:

- **WFO0003:** `apps/kiosk/app.manifest` — the `dpiAware`/`dpiAwareness` block (and its now-empty
  `application/windowsSettings` container) removed; `supportedOS` compatibility retained.
  `Wjss.Kiosk.csproj` — `ApplicationHighDpiMode=PerMonitorV2` added. `Program.cs` — manual
  `EnableVisualStyles`/`SetCompatibleTextRenderingDefault`/`SetHighDpiMode` trio replaced by the
  WinForms source-generated `ApplicationConfiguration.Initialize()` before `Application.Run`.
  Guarded by new scanner rule **S9** and three static xUnit source tests
  (`KioskDpiConfigurationTests`); kiosk README wording corrected (DPI via project property;
  manifest compatibility-only).
- **CA2231:** `Optional<T>` gains `operator ==`/`!=` defined exactly as `left.Equals(right)` —
  struct shape, Absent/Cleared/Present semantics, converter behaviour, hash scheme, and the
  three-state `activeJob` wire contract are all unchanged (contract wire semantics untouched).
  `OptionalPresenceTests`: seven xUnit cases covering every required state pair, the nine-pair
  operator-mirrors-Equals matrix, contract-record value equality, and hash conventions
  (authored; **NOT RUN in Arena**).
- No severities lowered, no suppressions, no TFM changes, no package/version edits, no lock
  files. **A full Owner-local Release build + test re-run is required** — this correction fixes
  the two reported errors as reviewed source; nothing here proves the next build passes.

### Stage 0.3A-1 — Source review correction (Owner findings A–E, same PR #4)

Independent Owner source review of `205456e` found five issues; corrected here as one
review-correction commit on PR #4. **Still no .NET build claim.**

- **A — build-blocking XML defect (FIXED):** the `Directory.Packages.props` header comment
  contained a double-hyphen sequence (`--locked-mode`), which is illegal in XML 1.0 and made
  the file unparseable by MSBuild — the checkpoint's own restore would have failed at line 9.
  Comment reworded; permanently guarded by new scanner rule **S8 XML well-formedness**
  covering `*.csproj`, `*.props`, `*.targets`, `*.manifest`, `*.resx`, `*.config`
  (verified against negative controls).
- **B — Delta Active-Job clear semantics (RESTORED TO ACCEPTED BASELINE):** the
  checkpoint-introduced `activeJobCleared: true` flag was **not approved**; the accepted
  three-state encoding is back — key absent = unchanged, object = replace, explicit
  `"activeJob": null` = clear. Implemented presence-aware with a scoped structural
  technique (`Optional<T>` wrapper + converter attributed only on
  `OperationalDelta.ActiveJob`; explicitly not a general serialization policy). The flag is
  removed from the C# records, TypeScript types, validator (which now **rejects** it), the
  fixtures (delta.basic exercises the null-clear key in lockstep with the .NET generator),
  the xUnit suite (five three-state tests authored — NOT RUN in Arena), ADR-0014 (rewritten
  with the rejected deviation kept as labeled history) and current-facing docs.
- **C — current-facing stage banners:** the 12 authorized status-banner documents (SECURITY,
  ALARM_MODEL, ARCHITECTURE, CLEANING_SEQUENCE, CONTROL_AUTHORITY, DOMAIN_MODEL,
  HISTORIAN_RETENTION, QUEUE_MODEL, REQUIREMENTS, SAFETY_BOUNDARY, TEST_STRATEGY,
  USER_PERMISSION_MODEL) now state PR #3 MERGED `d8d28201`, Stage 0.3A Scope Gate APPROVED,
  0.3A-1 SOURCE CHECKPOINT AUTHORED with Owner-local validation PENDING, PR #4 OPEN — NOT
  READY FOR MERGE, 0.3A-2 NOT AUTHORIZED, production-device access NOT AUTHORIZED. No
  domain/queue/alarm/safety/control content below the banners was touched.
- **D — README consistency:** the stale "preference is not acceptance" sentence is now
  explicitly labeled historical; React selected (Owner decision 2026-10-07) stated plainly;
  Blazor counter-spike not required absent a material blocker.
- **E — test-runner metadata:** `xunit.runner.visualstudio` `PackageVersion` carries the
  standard non-transitive pattern (`PrivateAssets=all`;
  `IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`).
  Proposed versions unchanged (Owner-local restore still arbitrates them); no lock files
  generated in Arena.
- Arena validation for this correction: XML well-formedness 15/15 files clean + negative
  controls flagged; JSON sweep green; `npm ci`/typecheck/`node --test` **15/15 PASS**
  (lockfile untouched); boundary scan **0 findings (S1–S8)**; `spikes/**` sha256 roll
  unchanged; `git diff --check` clean.

### Stage 0.3A-1 — Product foundation source checkpoint (authored in Arena; Owner-local validation PENDING)

**Gate:** Owner Option-C amended Stage 0.3A Scope Gate — source-checkpoint authority only.
**Status: NOT READY FOR MERGE.** .NET build **NOT RUN IN ARENA** (SDK/NuGet endpoints blocked
in the sandbox); the mandatory pre-merge gate is
[`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md).

- Added the product foundation skeleton as **source only**: `WaterJetSentinelSuite.sln`
  (12 projects); `packages/contracts/Wjss.Contracts` (18 files: snapshot/delta/command/status/
  queue/config records + `QueueRules`/`SensorMap` invariants); `packages/domain`,
  `packages/application`, `packages/time`; `adapters/simulator`; `apps/runtime` (health-stub:
  live 200 / ready 503 `RUNTIME_NOT_IMPLEMENTED`); `apps/kiosk` (compile-only shell, no
  WebView2 package); `apps/ui/README.md` (React scaffold is 0.3A-5 scope); five xUnit test
  projects incl. `tests/integration` (golden-fixture generator + parity gate).
- Added `packages/contracts/wjss-contracts-ts`: complete TypeScript structural mirror with
  validator and **14/14 passing tests** (Arena-verified: `npm ci`, `tsc --noEmit`,
  `node --test`); `typescript@6.0.3` install-verified, NuGet pins
  `Microsoft.NET.Test.Sdk 17.12.0` / `xunit 2.9.2` / `xunit.runner.visualstudio 2.8.2`
  **PROPOSED / UNVERIFIED** (first restore is Owner-local). No lock file committed.
- Added `packages/contracts/fixtures/` (4) and `config/examples/` (2) as
  **`PROVISIONAL STRUCTURAL FIXTURE`** artifacts (Node-authored to unblock the TS validator;
  superseded by the .NET generator on the Owner-local run — never claimed as .NET-generated).
- Added `tools/boundary-scan/boundary-scan.mjs`: repository-boundary gate S1–S7, currently
  **0 findings**. Added `docs/STAGE_0.3A_PLAN.md` and the Owner-local validation runbook.
- Added **DRAFT** ADR-0014 (contract skeleton; queue capacity 8; profile-start fail-closed;
  the delta clear-encoding it first drafted was superseded by the review-correction entry
  below), ADR-0015
  (loopback SSE transport + uPlot candidate; fills `[OPEN]` selections only on acceptance),
  ADR-0016 (WinForms + WebView2 detection-only shell; drafts ADR-0006 §6.7) and indexed them.
- Fixed `.gitignore`: the `**/[Pp]ackages/*` vendor rule conflicted with the new product
  `packages/` tree; negations and bin/obj re-exclusions corrected and verified with
  `git check-ignore` probes (product `packages/**` tracked; vendor dirs and build output
  ignored).
- Status records synchronized: `README.md` (status table, contents), this file,
  `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `docs/ROADMAP.md`, `docs/ARCHITECTURE.md`,
  `docs/decisions/README.md`. **Not claimed:** compilation, tests passing on .NET, runtime
  behaviour, fixture regeneration by .NET, kiosk behaviour, anything hardware-related.
  **Authorized by nothing here:** 0.3A-2, merges, ZIP.

### Stage 0.2.1A — React UI and Runtime Feasibility Spike (synthetic)

**Scope and Coding Start Gate:** `[APPROVED]`
**Implementation:** COMPLETE FOR DEVELOPMENT CHECKPOINT — **MERGED** (PR #3)
**Owner-local testing (installed Edge, Windows 11):** Edge E2E **25 / 25 PASS** at checkpoint
`4129687a` (Owner-reported, ≈ 1.3 min); at `23f48daa` 34 selected · 25 passed · 2 failed (`READ-A`,
`WJ-A`) · 7 not run (Owner-reported). The GlobalQueue-correction checkpoint (35-test selection) was not re-run separately; at the
critical Pump / Safe Return checkpoint `81c87a44` 42 selected · 32 passed · 1 failed (`S11/S12`,
Dirty Score drifted between samples) · 9 not run (Owner-reported). The final spike closeout
checkpoint `114c0761` (48-test selection): **Owner-local final Edge gate PASS** (Owner-reported,
2026-10-07)
**Owner manual review (1920 × 1080, Edge F11):** **PASS** at `114c0761` · **Controlled 15-minute
and 60-minute observations:** waived as Stage 0.2.1A merge blockers — not run
**Merge:** **MERGED** — PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5` (the
Agent never merges; the Owner executed the merge. The "OPEN — NOT MERGED" wording in the
closeout entry below reflects the state at the time it was written.)
**Primary UI Framework:** **React selected** (Owner decision, 2026-10-07)
**Blazor counter-spike:** NOT REQUIRED unless a future material blocker is identified
**Main Development Scope Gate:** PENDING
**Stage 0.3:** `[NOT AUTHORIZED]`

#### Docs — Stage 0.2.1A closeout: Owner-local final review PASS, React selected (documentation only)

- **Owner decision (2026-10-07)** on `114c0761`: Owner-local final Edge gate **PASS**; Owner manual
  review **PASS**; UI, synthetic AutoSequence controls, GlobalQueue presentation, critical Pump
  modal and Mandatory Safe Return behaviour accepted as the **Development baseline**; **React
  selected as the Primary UI Framework**; Blazor counter-spike **not required** unless a future
  material blocker is identified; controlled 15- / 60-minute observations **waived as merge
  blockers**. Stage 0.2.1A recorded as **COMPLETE FOR DEVELOPMENT CHECKPOINT**.
- `docs/CONTROL_AUTHORITY.md`: the §4 invariant now reads "Queue refill, score changes, Operator
  Reorder, Reject, AutoSequence lifecycle changes, valve exclusion and equipment availability must
  never produce concurrent Cleaning Jobs"; the struck-through queue-hold conflict row is replaced by
  the current rule (pause belongs to the AutoSequence or Job lifecycle). Remaining current-facing
  queue-level Hold wording removed from `QUEUE_MODEL.md` (snapshot contents), `TEST_STRATEGY.md`
  (operator-action tests) and `USER_PERMISSION_MODEL.md` (queue permissions).
- Status banners (14 documents), README, CURRENT_STATE (§12.11), MASTER_PLAN, ARCHITECTURE §24 /
  §34, REQUIREMENTS UIF-001 / UIF-003, decisions README, spike README, plan and results (§0G),
  OWNER_LOCAL_TESTING §1G result, ADR-0006 / ADR-0008 dated update notes (history kept),
  validation summary fields and manifest.
- Not claimed: Production safety or stability, WebView2, kiosk, Modbus performance, hardware. No
  application, test, contract, dependency or Sensor-map change. **PR #3 NOT MERGED** (the Agent
  never merges); Main Development coding **NOT STARTED**. *Later status:* PR #3 was merged by the
  Owner (`d8d28201`), and Main Development coding began with the Stage 0.3A-1 source checkpoint
  recorded below.

#### Fixed — Final spike closeout: explicit synthetic AutoSequence controls (synthetic review tooling)

**SYNTHETIC REVIEW TOOLING — NOT THE PRODUCTION OPERATOR-CONTROL MODEL.** Production Pause /
Resume, abort re-queue, Safe Return failure and reset authority remain OWNER DECISION REQUIRED.

- **Recovery:** the Owner-authorized one-time recovery restored the local branch to `81c87a44`
  (identity proof 153 / 153, compare-and-swap ref update, index-only refresh).
- **S11/S12 made deterministic:** the Owner Edge run of `81c87a44` failed because the alarm test
  compared backgrounds from two revisions while the synthetic Dirty Score drifted. The test now
  fixes and holds the Score (`set-sensor-score { hold: true }`, Score 82, released by
  `reset-sensor`), waits for that revision, and requires identical Score, classification and
  computed background through Raise / Clear / Acknowledge — no RGB tolerance; auto jobs are off
  during the test.
- **Synthetic AutoSequence controls** (Diagnostics only, `--synthetic-test-controls`, `SYN · `
  labels, token path, one click = one request, no target parameter):
  - START AUTOSEQUENCE → `RUNNING` + atomic dispatch of Position 1 (the selected Sensor is never
    used);
  - PAUSE AFTER CURRENT JOB → `PAUSE_REQUESTED` (the Job continues through Mandatory Safe Return;
    no next dispatch) → `PAUSED`; without a Job → `PAUSED` at once;
  - RESUME AUTOSEQUENCE → only from `PAUSED`; head-only dispatch; never clears a critical
    suspension;
  - ABORT ACTIVE JOB → `ABORTING` → Mandatory Safe Return → `ABORTED` → release; no re-queue;
    `PAUSE_REQUESTED` → `PAUSED`; `CRITICAL_SUSPENDED` stays;
  - RESET CRITICAL SCENARIO → only after clear + acknowledge + Safe Return complete → `OFF`;
    queue preserved; no dispatch, no Job, no Pump start; explicit START required.
  - Every button shows its disabled reason as text (`sequence.controls`).
- **Runtime:** `sequence.mode` (`OFF` / `RUNNING` / `PAUSE_REQUESTED` / `PAUSED` /
  `CRITICAL_SUSPENDED`) and `sequence.controls`; the release revision of a Job never dispatches
  (synthetic separation rule). Clear + acknowledge never resume.
- **Tests:** harness `autoSequenceControls.test.mjs` (A–G; 60 / 60 ×3), Vitest
  `autoSequenceControl.test.tsx` (138 / 138), scenario S35, Edge specs `SEQ-B`..`SEQ-G` in
  `critical.spec.ts` (48-test Owner selection, same four files).
- **Docs:** current-facing queue `HELD` / `BLOCKED` / `EXCLUDED` wording removed from
  ARCHITECTURE, REQUIREMENTS (QUE-020, QUE-024, SPC-001), CONTROL_AUTHORITY, CLEANING_SEQUENCE,
  CURRENT_STATE and QUEUE_MODEL. The "held and rejected simultaneously" question is removed; Reject
  / Reorder in the ready-only queue and Production Pause / Resume are OWNER DECISION REQUIRED.
  ADR-0003 is marked **SUPERSEDED IN PART by Owner decision dated 2026-10-06** (history kept).
  Critical matrix gains §G. OWNER_LOCAL_TESTING §1G holds the final Edge gate.
- **Unchanged:** critical Pump / Mandatory Safe Return behaviour, modal, Google Sans, Water Jet
  slots, GlobalQueue cap 8, head-only dispatch, no status column, 106 / 212.

#### Fixed — Critical Main Pump handling and Mandatory Safe Return (Owner critical Pump decision; synthetic)

**SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED.** No physical Pump, relay, VFD, valve,
axis, Galil program or interlock is involved; no safety certification is claimed; no Production
coordinates, addresses or protocols are used.

- **Recovery:** the Owner-authorized one-time recovery restored the local branch to `60cd0398`
  (identity proof 147 / 147, compare-and-swap ref update, index-only refresh).
- **Pump event classes:** the Main Pump is High Critical.
  - Expected commanded stop: not a fault (no modal, no suspension), but an Active Job still Safe
    Returns.
  - Unexpected stop / trip: stops progression, water and dispatch. The AutoSequence becomes
    `CRITICAL_SUSPENDED` (persists; no automatic Resume, no automatic next Job, no
    `WAITING_FOR_PUMP` Job) and an Active Job enters Mandatory Safe Return.
  - The GlobalQueue is frozen unchanged.
- **Mandatory Safe Return for every outcome** (SR1–SR8):
  - The valve close is commanded and confirmed before the axis Standby command.
  - The Job stays Active until Standby is confirmed and released.
  - `COMPLETED` applies only after Standby is confirmed; every other trigger gives `ABORTED`.
  - On Safe Return failure (`SAFE_RETURN_FAILED`), the Job is retained with no outcome, release or
    dispatch, and the modal stays.
  - Evidence carries increasing indices, a bounded outcome log and no coordinates.
  - The validator checks the ordering on every Snapshot.
- **Superseded:** the synthetic Job pre-check pump wait (`preCheck` on a Job). A Job is no longer
  created while the Pump is not ready; the AutoSequence shows `PUMP_NOT_READY`. Scenario
  `abort-job` is now timed through Safe Return (`{ immediate: true }` is a scenario-only shortcut).
- **Critical modal:**
  - Layout: "CRITICAL ALARM — MAIN PUMP STOPPED / TRIPPED", centered, viewport-fitted, over an
    inert dimmed background.
  - Content: live Safe Return step, valve / axis / Standby, and generic response text.
  - Controls: Acknowledge is the only action (same-origin loopback `POST /api/spike/critical-alarm-ack`);
    there is no close button and no Resume button. Acknowledge is not a clear.
  - Closing: the modal closes only when the alarm is cleared, Safe Return is complete and the alarm
    is acknowledged; the sequence stays suspended.
  - Accessibility: alertdialog, aria-modal, labelled; Escape does not dismiss; focus trap; polite
    announcements; no flashing.
  - Palette: distinct plum / orchid tokens.
- **UI:**
  - The Active Job panel shows the Safe Return status.
  - Diagnostics gains a Sequence section.
  - `--synthetic-test-controls` gains the "Critical Pump / Safe Return" group: 10 Owner controls
    plus valve-feedback-absent.
- **Docs:**
  - Queue-level `HELD` / Hold / Release Hold are **SUPERSEDED** in `docs/QUEUE_MODEL.md` §7.2 and
    `docs/DOMAIN_MODEL.md` (Owner-authorized).
  - New `docs/spikes/critical-pump-safe-return-decision-matrix.md` (matrices A–F, every row OWNER
    DECISION REQUIRED).
  - The queue eligibility matrix gained §6.
  - Notes added to CLEANING_SEQUENCE, ALARM_MODEL and CONTROL_AUTHORITY.
  - Results §0E; CURRENT_STATE §12.9; OWNER_LOCAL_TESTING §1F.
- **Arena validation:**

  | Check | Result |
  | --- | --- |
  | Vitest | 131 / 131 (17 files) |
  | Harness | 51 / 51 × 3 |
  | Scenarios | 29 / 4 / 1 / 0 (34) |
  | Playwright list | 43 tests in 5 files (Owner-local 42) |
  | Build | JS 340.50 kB, CSS 30.92 kB |

  Dependencies are unchanged. **NOT VERIFIED:** browser / Edge / WebView2 rendering, hardware,
  and Production safety.

#### Fixed — GlobalQueue semantics correction and head-only synthetic dispatch (Owner domain correction)

- **Recorded:** Owner-local Edge at `23f48daa`: 34 selected · 25 passed · 2 failed (`READ-A` G+205 value
  clipped; `WJ-A` assertion waited for `detail-id` after a non-selectable Water Jet slot) · 7 not run.
  The Owner-authorized one-time recovery restored the local branch to `23f48daa` (identity proof
  144 / 144).
- **Superseded:** the previous synthetic queue / job behaviour — per-entry `READY` / `HELD` /
  `BLOCKED` / `EXCLUDED` status, auto start that scanned forward past non-ready entries (Owner
  examples: Job `G+217` / `I12` while `G+110` was the head), an unbounded queue shown as an 8-row
  preview (76 / 77 queued), and arbitrary review Job targets — is **SUPERSEDED** and **NOT ELIGIBLE
  FOR PRODUCTION PROMOTION**.
- **Bounded synthetic GlobalQueue:** ready-to-dispatch entries only (presence = READY), at most 8
  unique entries physically (`QUEUE_FULL` beyond; `totalQueued === entries.length`; no hidden
  overflow), FIFO, deterministic entry IDs and refill, Water Jet slots excluded, monotonic queue
  revision. Per-Sensor `queueState` is `NONE` / `QUEUED` / `ACTIVE`. Labelled "GlobalQueue ·
  synthetic … not Production scheduling".
- **Head-only atomic dispatch:** `dispatchHead()` is the only Job creator: Position 1 only (no
  scan-forward), one step removes the head, creates exactly one Job for that Sensor, and writes a
  synthetic `DispatchRecord`; refused while a Job is active. Pump waits live on the Job
  (`preCheck: WAITING_FOR_PUMP`); `pause-auto-sequence` (alias `hold-queue`) pauses dispatch without
  touching entries. `start-job` / `review-job` make the Sensor the head first and never retarget.
- **Contract (spike):** `QueueEntry.status` removed, `entryId` added; `QueueSummary` gains
  `synthetic`, `label`, `capacity: 8`, `revision`, `autoSequence`, `lastDispatch`,
  `eligibilityDiagnostics`; `ActiveCleaningJobState` gains `preCheck` and `dispatch`; fixtures
  regenerated. Schema identifiers unchanged (transient spike stream, no persisted consumer).
- **UI:** GlobalQueue Status column and chips removed (source reason already a column, so no
  replacement); count "n / 8 queued · FIFO · not Production scheduling"; cell badge `Q` / `J`;
  Diagnostics "QUEUE → JOB DISPATCH (SYNTHETIC EVIDENCE)" and, only when a scenario sets it, "QUEUE
  ELIGIBILITY DIAGNOSTICS"; eight presets (Queued DIRTY, Queued CLEANER non-score, Selected queued,
  Dispatched head → Job, Alarm on Active Job, Alarm not admitted (demo), Head → Job transition,
  Reset).
- **Decision Matrix (proposal only):** `docs/spikes/queue-eligibility-decision-matrix.md` — quality,
  alarm, interval, equipment, and sequence rows, every one `OWNER DECISION REQUIRED`; conflicts with
  `QUEUE_MODEL.md` §7.1 / `DOMAIN_MODEL.md` (`HELD`) recorded, not resolved.
- **Edge value-clipping hotfix (`READ-A`):** value line box 20 px (full Google Sans content area at
  16 px) and ID row 12 px via tokens; rows `12px | minmax(20px, 1fr) | rail`. No font, size, weight,
  overflow, or transform change; cell 52–56 × 46–50 unchanged.
- **Water Jet test hotfix (`WJ-A`):** Case A (no selection → no detail, no selection, no command) and
  Case B (Sensor detail stays; no slot selected).
- **Tests:** Vitest 120 / 120 (16 files; new `globalQueue.test.tsx`, cell budget guard); harness 40 / 40
  (new `queueDispatch.test.mjs`, gates A–F); scenarios 32 (27 PASS / 4 PASS+OWNER / 1 OWNER-LOCAL /
  0 FAIL; new S31 head-only dispatch, S32 bounded load); Playwright list 36 tests in 4 files (new
  `QUEUE-B`; `QUEUE-A`, `CTRL-A`, `CTRL-B`, `WJ-A` rewritten). Browser results **NOT VERIFIED** in Arena.

#### Changed — Final Owner UI punchlist (Owner screenshot review of `4129687a`)

- **Recorded:** Owner-local Edge E2E 25 / 25 PASS at `4129687a` (≈ 1.3 min). The second
  Owner-authorized atomic recovery restored the local branch to `4129687a` (identity proof 131 / 131).
- **Google Sans, self-hosted (OFL-1.1):** one Latin variable WOFF2 subset (wght 400–700, 47,672 B)
  built deterministically from the official `google/fonts` repository (`ofl/googlesans`, ref
  `7085eb89`); verbatim `OFL.txt` and `TRADEMARKS.md` and a full provenance record
  (`react-ui/src/assets/fonts/FONT_SOURCE.md`) alongside; build script
  `measurements/font/build_google_sans_subset.py`. `@font-face` + preload, no CDN; fallback
  `"Segoe UI", system-ui, sans-serif`; Bahnschrift removed. First render waits (≤ 2.5 s) for the
  font so geometry does not reflow; uPlot canvas axes and form controls use the same family.
- **Sensor cell:** the ID row now spans the full cell width (13 px / 700, no marker beside it);
  the quality marker (8 px) moved to the right end of the value row with a symmetric
  `spacer | value | marker` grid so the value (16 px / 700) stays centred; bottom rail unchanged
  (alarm left, queue right); cell 52–56 × 46–50 px.
- **Application identity:** "WaterJet Sentinel Suite" (19 px / 700) over a muted
  "OPERATIONS CONSOLE" subtitle (11 px / 650, 0.06 em); synthetic badge kept separate; accessible
  name "WaterJet Sentinel Suite — Operations Console".
- **Legend:** dedicated `LegendSwatch` (16 × 16 SVG, symbols inset ≥ 2 px, no negative offsets) in a
  fixed 20 px icon column; eight entries (Dirty, Cleaner, Not classified, Uncertain, Alarm, Selected,
  Active Job, Water Jet); the clipped left edge is corrected.
- **Water Jet terminology (display only):** slots read `WJ` / `REAR` and `WJ` / `FRONT`; summary
  "2 Water Jet reference slots · synthetic" (never "2 Water Jets"; no Production equipment numbers).
  `CANNON_REAR` / `CANNON_FRONT` and `slotType: 'CANNON'` remain the legacy internal identifiers
  (contract and fixtures unchanged); I7 / I16 stay equipment-only.
- **Mixed GlobalQueue sources (synthetic):** deterministic `queue-mixed-sources` scenario queues
  TIME DUE, TEMP + TIME, OPERATOR, TEMP, then the DIRTY SCORE source (FIFO). The score source now
  releases only the entries it owns; de-duplication keeps the first source owner and position.
  Queue authority and the FIFO model are unchanged; default behaviour is unchanged.
- **SYNTHETIC TEST CONTROL (opt-in):** Diagnostics drawer section enabled only when the harness runs
  with `--synthetic-test-controls` (same-origin, loopback-host token endpoint; per-run token in
  memory, dropped on disconnect). Per selected Sensor: alarm raise / clear → ACK REQUIRED / ack,
  queue reasons, remove, quality, DIRTY / CLEANER, held Active Job target, reset; six presets.
  Spike review tooling only — not an authentication model, not a Production path.
- **Harness:** unknown `/api/*` routes now return a JSON 404 instead of the SPA fallback.
- **Tests:** Vitest 109 / 109 (15 files; 16 new); harness 32 / 32 (6 new); scenarios 30 (S29 mixed
  queue, S30 review presets; S22 scans browser-loadable sources only); Playwright list 35 tests in 4
  files (9 new Owner-local specs). Browser results are **NOT VERIFIED** in Arena.

#### Changed — Operations readability refinement (Owner screenshot review)

- **Stale test corrected:** the MAP E2E test asserted the pre-fullscreen Y-interval wall order;
  it now asserts positions relative to the compact centre summary (Rear above, Left left, Right
  right, Front below; centred Rear / Front; no wall or centre intersection; walls inside the map).
  The layout was not changed to satisfy the stale test.
- **Sensor cell zones:** ID top-left inside a width-limited zone, reserved 10 + 2 px top-right
  marker zone (quality glyph), centred value, bottom rail (alarm triangle, queue badge). Cells
  52–56 × 46–50 px; ID 13 px / 700 (Bahnschrift semi-condensed, Segoe UI fallback), value
  16 px / 700, tabular numerals.
- **Colours as central tokens:** desaturated Cleaner `#256B4A`–`#2F8059` and Dirty
  `#B33A2F`–`#8D2F27`; graphite Not classified; neutral + pattern + glyph for BAD / STALE /
  DISABLED; amber Uncertain dot; yellow alarm border + icon separated from Dirty red; cyan
  selection; white double outline for the Active Job; three surface levels. No glow.
- **Typography scale and spacing scale** centralised (no text below 11 px outside the 10 px
  cell badge; 2 / 4 / 6 / 8 / 12 / 16 / 24 px spacing).
- **Status bar** grouped Alarm → Process → System → Technical; actionable alarm strip.
- **Sensor Detail** ID header and Process / Location / State / Source groups; **Active Job**
  separated fields, completed / current / future phases, elapsed time; **GlobalQueue** fixed
  column widths, right-aligned tabular numbers, token status chips; **Pressure Trend** thicker
  lines, 12 px axes and legend, clearer band and setpoint, header summary; **Camera** placeholder
  badge and NO SIGNAL state.
- **Tests:** Vitest 93 / 93 (13 files); four new Edge specs `READ-A`..`READ-D`; `LAYOUT-A` /
  `LAYOUT-D` / `LAYOUT-E` aligned to the approved readability scale. Browser results are **NOT
  VERIFIED** in Arena; the Owner-local Edge run, the manual F11 review, and a controlled 15-minute
  observation are **PENDING**; the 60-minute run stays **PAUSED**.
- **Recorded:** the Owner-local interrupted overnight observation of `ea23bc58` (≈ 11 h 41 min wall
  clock with host sleep, ≈ 4 h likely active; invariant violations 0, accepted second Jobs 0,
  Historian rejections 0, 659 synthetic Jobs) — not a controlled benchmark.

#### Changed — Fullscreen Operations UI refinement (Owner-approved Design Addendum)

- Presentation-only refinement of the React feasibility Operations page for Windows 11, Edge F11,
  1920 × 1080, zoom 100 %: strict `100dvh` layout with no page-level scrolling; larger Sensor
  cells (≈ 50 × 44 px at the target, capped at 54 × 50 px) with 12.5 px IDs and 15 px values;
  U-shaped map retained with overlapping vertical bands and a compact 204 × 136 px centre
  summary; two-column Sensor Detail inspector; compact Active Job and GlobalQueue (compact source
  reasons); bounded Trend and Camera bottom row; floating, closable Diagnostics drawer. All sizes
  come from one design-token location (`react-ui/src/global.css`).
- Sensor domain, mapping, counts, Cannon positions, functional behaviour, and dependencies are
  unchanged. New tests: 15 Vitest tests and 5 Owner-local Edge layout tests (`e2e/layout.spec.ts`).
  Viewport fit is **NOT VERIFIED** in Arena (no browser); the Owner-local re-run is PENDING.
- Documentation findings closed: **DP-01** (`docs/CURRENT_STATE.md` — the stale "24 versus 28"
  row-alignment open item is resolved by the 18 × 6 canonical matrix, 24 / 29 / 24 / 29, Cannon
  slots I7 / I16) and **DP-02** (`docs/ARCHITECTURE.md` §34 — final UI framework `[OPEN]`, route is
  React-first Stage 0.2.1A feasibility, Blazor counter-spike deferred behind a separate Scope Gate).

#### Changed — Owner domain correction: 106-location Sensor map

- The Owner corrected the protected count from **104 / 208** to **106 Sensor locations / 212
  Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). It is a domain correction, not
  a runtime failure. Documents changed: `README.md`, `docs/DOMAIN_MODEL.md` (new §2.2.1 logical
  matrix), `docs/REQUIREMENTS.md` (PHY-001, PHY-002, UIW-001, UIW-002), `docs/ARCHITECTURE.md`
  (§24, §25.1, §33.2), `docs/TEST_STRATEGY.md`, `docs/ROADMAP.md`, `docs/MASTER_PLAN.md`,
  `docs/CURRENT_STATE.md`, ADR-0002, ADR-0003, ADR-0006 to ADR-0009, the spike plan / results,
  and the contract docs.
- Spike: new single mapping source `contracts/sensorMap.mjs` (18 × 6 logical matrix, 106
  Sensors, Cannon slots at logical I7 / I16, synthetic scan order, device, channel, and jet
  assignment). It is consumed by the harness, Snapshot `wallMap`, validator, fixtures, scenario
  runner, and tests. The React U-shaped map renders 6-row wall grids from the Snapshot wall map,
  with neutral, non-selectable Cannon slots. Separate seeded random streams make the synthetic
  process workload deterministic per seed. The runtime refuses Sensor commands for Cannon IDs.
- Status headers corrected in the 12 documents previously reported stale in
  `docs/CURRENT_STATE.md` §12.3, plus `docs/ROADMAP.md` and `docs/decisions/README.md`.
- `docs/ARCHITECTURE.md` §33: the dual-candidate Stage 0.2.1 proposal is marked **SUPERSEDED**
  by the React-first Stage 0.2.1A spike. A Blazor counter-spike requires a future Owner Scope
  Gate.

#### Added

- **`spikes/ui-runtime-react/`** — removable synthetic feasibility spike: Node built-ins-only
  synthetic runtime harness (10 synthetic device sessions, serialized per device, bounded
  cross-device concurrency, Snapshot plus Delta over SSE on `127.0.0.1` only, bounded Historian
  slowdown simulator, invariant monitor); React 19 + TypeScript 6 + Vite 8 single Operations
  page with an external presentation store, per-Sensor subscriptions, and a uPlot trend
  (spike-only chart choice); contracts and golden fixtures; 28-scenario runner; measurement
  tools; Vitest (jsdom) tests; Playwright specs for Owner-local runs with installed Microsoft
  Edge; offline restore and Owner-local testing guides; committed environment record,
  summaries, SBOM, licence inventory, and SHA-256 manifest. Raw results are not committed.
- **`docs/spikes/stage-0.2.1a-plan.md`** and **`docs/spikes/stage-0.2.1a-results.md`**.

#### Changed

- Stage status in `README.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`,
  `docs/ROADMAP.md`, `docs/decisions/README.md`, and ADR-0006 to ADR-0013: Stage 0.2 recorded as
  OWNER ACCEPTED / MERGED; ADRs ACCEPTED (not implemented); Stage 0.2.1A approved and in
  progress.

#### Not changed / not verified

- No Product directory (`apps/`, `packages/`, `adapters/`, `config/`), no .NET code, no SQL, no
  device access, no Production value. SSE is a spike transport only; Production transport
  `[OPEN]`. ASP.NET Core integration, Windows Service behaviour, WebView2 kiosk behaviour,
  browser rendering performance, Windows offline restore, and extended stability are
  **NOT VERIFIED**.

### Stage 0.2 — Technology and Solution Architecture Decision

**Stage 0.2 Scope Gate:** `[APPROVED]`
**Stage 0.2 architecture checkpoint:** OWNER ACCEPTED / MERGED — PR #2 (source checkpoint
`5bcf1b33f924ab30590a55736676200115874fa1`, merge commit
`e779f8ad2c856e367fd65985007a3da411bd0e73`)
**ADR-0006 to ADR-0013:** ACCEPTED as architecture direction — not implemented
**Stage 0.2.1:** superseded by the approved, narrower Stage 0.2.1A React feasibility spike
**Stage 0.3:** `[NOT AUTHORIZED]`

*The status lines below this point in the Stage 0.2 entry are preserved as they were written at
submission time.*

Documentation-only stage. Established the technology and solution architecture foundation for
WJSS as a set of decision records and supporting architecture sections. **No application code,
runtime scaffold, solution or project file, package manifest, lock file, database schema, SQL
script, migration, hardware adapter, simulator, test code, CI workflow, installer, deployment
script, configuration file, package dependency, release archive, or runtime directory was
created, and no device or database was contacted.** No dependency was installed.

#### Added

- **`docs/decisions/ADR-0006-ui-delivery-model.md`** — UI delivery model: application-owned
  full-screen kiosk shell, close guard, controlled navigation, static local assets in an
  embedded WebView, local web UI. Two candidates compared on equal terms: Candidate A —
  React + TypeScript + Vite; Candidate B — Blazor Hybrid. The legacy application's observed UI
  workload is recorded as architecture input. **The final framework is `[OPEN]`**; the
  evidence-based preference for Candidate A is not acceptance and Candidate B is not rejected.
- **`docs/decisions/ADR-0007-runtime-process-model.md`** — process and ownership model:
  Equipment Runtime as the sole owner of physical device sessions; the UI cannot write
  hardware and owns no device session; adapters contain no UI logic; loopback, authenticated,
  contract-first Local Application API. Adds the live-state delivery and performance-isolation
  boundary: authoritative in-memory state, snapshot-plus-delta push, reconnect always
  re-snapshots, and a Main Pump stop that must never be blocked (PMP-007).
- **`docs/decisions/ADR-0008-technology-stack.md`** — .NET Long-Term Support track, ASP.NET
  Core minimal API, built-in dependency injection and configuration, structured local logging,
  validation, test strategy, and offline packaging from a local package source. The UI
  framework is explicitly `[OPEN]` and is not part of this decision.
- **`docs/decisions/ADR-0009-database-access-and-migrations.md`** — SQL Server 2025 Standard
  direction, connection ownership and pooling, transactional relational access, a
  batch-oriented Historian write path, migration ownership and execution policy, and database
  unavailability behaviour. ORM, mapper, micro-ORM, provider, and bulk-write mechanism are
  `[OPEN]`. SQL Server is not the per-cycle parameter source, and a Historian backlog must not
  decide which initiating actions are refused.
- **`docs/decisions/ADR-0010-device-adapter-boundary.md`** — adapter ports for Modbus TCP,
  WAGO I/O, Galil, SQL Server, clock, simulator, and a loopback test adapter; domain
  independence from vendor libraries; simulator parity with the application-facing contract;
  physical adapters explicit and disabled by default; command lifecycle states. Adds
  per-device serialized command queues, bounded concurrency, batched contiguous reads, a Poll
  Plan compiled at publication or startup, Fast/Medium/Slow poll groups, and the rule that the
  UI never polls.
- **`docs/decisions/ADR-0011-configuration-and-secrets.md`** — configuration layers (public
  example, local development, simulator, test hardware, production), Draft versus Published,
  secrets and user database, production configuration and secrets outside Git, and audit
  history. Adds the immutable in-memory **Published Configuration Snapshot** with atomic swap
  under the approved state gate, the rule that a Draft is never consumed, presentation
  thresholds read from Published configuration, and the Poll Plan compiled at publication.
- **`docs/decisions/ADR-0012-simulator-first-development.md`** — simulator as default,
  deterministic simulation, failure injection, contract parity, and the explicit statement
  that the proposed Stage 0.2.1 spike uses synthetic data only, touches no WAGO, Galil,
  Production SQL Server, or Production configuration, and that spike results are never
  hardware evidence.
- **`docs/decisions/ADR-0013-offline-deployment.md`** — offline Windows 11 Pro deployment,
  local services, SQL dependency, kiosk startup, service recovery, log and backup locations,
  upgrade and rollback, diagnostic bundle, local-only communication, and no Internet
  dependency. Offline packaging must be able to serve either UI candidate; the second package
  ecosystem is a cost, not a blocker.
- **`docs/ARCHITECTURE.md`** — new sections 23 to 33: legacy evidence and its required
  interpretation; UI framework candidates; the legacy UI workload with nine explicitly
  prohibited behaviours; the sensor presentation model (seven independent dimensions,
  Dirty/Cleaner classification rule, dirty-red colour rule); the quality-aware pipeline;
  operational state delivery; Modbus acquisition; the SQL and configuration hot path;
  Historian decoupling; trend direction; and the proposed Stage 0.2.1 spike. The open-item
  section moved to section 34 and its cross-references were corrected.
- **`docs/REQUIREMENTS.md`** — new section 26 with ten requirement groups: UI framework
  candidates (UIF), UI workload and prohibited behaviours (UIW), sensor presentation (SPC),
  data quality (DQS), live-state delivery (LSD), Modbus acquisition (MDA), configuration hot
  path (CPS), Historian decoupling (HDC), live trend (TRD), and the proposed spike (SPI).
  ARC-019 no longer names a language, ARC-021 no longer decides the UI framework, ARC-022 is
  stated in architecture language, and ARC-025 is bounded.
- **`docs/MASTER_PLAN.md` section 3.2** — the proposed Stage 0.2.1 UI and Runtime Technology
  Spike, `[NOT AUTHORIZED]`: identical synthetic data and contract for both candidates, the
  measurement set, the constraints, and the deliverable.
- **`docs/TEST_STRATEGY.md` section 3.8** — planned spike verification cases 46 to 60, all
  `PLANNED — NOT EXECUTED`, plus new testability requirements and open acceptance thresholds.
- Supporting alignment in `README.md`, `SECURITY.md`, `AGENTS.md`, `CHANGELOG.md`,
  `docs/CURRENT_STATE.md`, `docs/ROADMAP.md`, `docs/DOMAIN_MODEL.md` section 5.1,
  `docs/CONTROL_AUTHORITY.md` section 8.1, `docs/SAFETY_BOUNDARY.md` section 9.1,
  `docs/ALARM_MODEL.md` section 9.1, `docs/CLEANING_SEQUENCE.md` section 10.1,
  `docs/HISTORIAN_RETENTION.md` section 8.1, `docs/QUEUE_MODEL.md` section 11.1,
  `docs/USER_PERMISSION_MODEL.md` section 7.1, and `docs/decisions/README.md`.

#### Changed

- Stage-status wording across the repository now states one position: Stage 0.1 merged through
  PR #1; Stage 0.2 Scope Gate approved; Stage 0.2 architecture checkpoint submitted; review
  changes requested / in progress; Owner manual review pending; not merged; Stage 0.2.1 and
  Stage 0.3 not authorised. The previous `Stage 0.2 [NOT AUTHORIZED]` wording was removed from
  the forward-looking status of every document.
- The Stage 0.1 status block in this file is retained as history and annotated as superseded.

#### Unchanged

- Every protected decision is unchanged: one Boiler Unit per Workstation; Windows 11 Pro;
  English default with Thai contextual explanation; 104 Sensor locations; 208 Thermocouple
  channels *(historical Stage 0.2 record — superseded by the Stage 0.2.1A Owner domain correction
  to 106 Sensor locations / 212 Thermocouple channels)*; Eight Water Jets; Four Galil DMC-B140-M controllers; one Water Jet to one dedicated
  Isolation Valve; strictly sequential Cleaning Jobs; maximum one active Cleaning Job; parallel
  Water Jet Cleaning prohibited; GlobalQueue FIFO and source ownership; Hard Minimum Cleaning
  Interval on both source queues; `LastSuccessfulCleaningCompletedAt` never null; Operator as
  the baseline DCS Permissive Override role; override exclusions and the absent automatic
  expiry; cleared-state acknowledgement; the Operations UI close guard; Main Pump may remain
  running between sequential Jobs; WAGO fail-safe not verified; Production Write not
  authorised; no production configuration in the repository.
- No schema, migration, SQL script, threshold, register map, tag list, coordinate, IP address,
  connection string, credential, or secret was added.

#### Not verified

- Nothing in Stage 0.2 was built, compiled, run, measured, installed, or connected. No
  hardware, database, simulator, kiosk, installer, or deployment behaviour was tested.
- WAGO fail-safe behaviour remains `[NOT VERIFIED]`. The SQL Server deployment capacity
  conclusion remains `[NOT VERIFIED]`. Device-library behaviour is `[NOT VERIFIED]`.
- The UI framework, push transport, chart library, mapper technology, Historian overflow
  policy, poll-group intervals, spike acceptance thresholds, and the site-specific
  invalid-value mapping remain `[OPEN]`.

### Stage 0.1 review-correction checkpoint (documentation-only, superseded by Stage 0.2)

The Owner-confirmed Stage 0.1 review punchlist was applied across the affected documentation.
No application code,
  runtime scaffold, database script, hardware adapter, simulator, test, CI workflow,
  production configuration, ZIP, or release artifact was created. Summary of what the
  correction establishes:
  - **Water Jet to Isolation Valve cardinality** — exactly one-to-one, dedicated, never
    shared. A sensor's Isolation Valve is derived from the sensor's assigned Water Jet.
  - **Strictly sequential Cleaning Jobs** — INVARIANT-SEQ-001 through INVARIANT-SEQ-006,
    including that at most one Cleaning Job may be ACTIVE at any time and that parallel
    Water Jet cleaning is prohibited.
  - **GlobalQueue stop and restart policy** — an Operator stop records the Queue snapshot
    and Held/Rejected/Reordered state and closes the instance; a new AutoSequence rebuilds
    all source queues and seeds a fresh GlobalQueue rather than reloading the previous one.
  - **Source ownership after deduplication** — one source owner per entry, established by
    the preserved earliest position; merged reasons do not transfer ownership; refill uses
    the original owner.
  - **Alarm acknowledgement correction** — active acknowledgement is awareness only; a
    separate cleared-state acknowledgement is required to release a block.
  - **DCS and Modbus communication health** — transport-evidence based health evaluation
    with a configurable stale timeout, and the approved blocking behaviour for idle and
    active-job cases.
  - **DCS Permissive Override** — Operator-activated, manually released, scoped to the
    approved DCS permissive evaluation only, with an explicit exclusion list.
  - **Operations UI close guard** — blocked while a Cleaning Job is active or the Main Pump
    is running; an operational usability control, explicitly not safety protection.
  - **Requirement and test traceability** — new requirement groups (WJV, SEQ, COMH, OVR,
    UIG) and eighteen required planned test cases, all marked planned and not executed.
  - **Status, versioning, and capacity wording corrections** — gate approval separated
    from implementation acceptance; no application version; historian capacity conclusions
    removed as unsupported.

## [Historical — documentation foundation candidate]

### Stage 0.1 — Repository Documentation Foundation

**Stage 0.1 Scope Gate:** `[APPROVED]`
**Stage 0.1 implementation:** MERGED to `main` through PR #1 (merge commit
`d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`)
**Stage 0.1 documentation review:** CHANGES REQUESTED / IN PROGRESS at the time of the Stage
0.1 checkpoint; superseded by the Stage 0.1 review-correction checkpoints on the same branch
**Stage 0.1 Owner manual review:** recorded complete by the Owner in the approved Stage 0.2
Scope Gate
**Merge:** MERGED
**Stage 0.2 at the time of this Stage 0.1 record:** `[NOT AUTHORIZED]` — subsequently
authorised

The status block above is the historical Stage 0.1 record. It is retained as history and is
superseded by the current stage status recorded in [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md),
[`README.md`](README.md), and [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md). At the current
Stage 0.2 position: Stage 0.2 Scope Gate `[APPROVED]`; Stage 0.2 architecture checkpoint
**SUBMITTED FOR OWNER REVIEW**; documentation review **CHANGES REQUESTED / IN PROGRESS**;
Owner manual review **PENDING**; **NOT MERGED**; Stage 0.2.1 `[NOT AUTHORIZED]`;
Stage 0.3 `[NOT AUTHORIZED]`.

Documentation-only stage. Established the repository governance model, the product identity
baseline, the queue and control models, and the delivery plan. No application code, runtime
scaffold, database schema, hardware adapter, CI workflow, production configuration, release
package, or archive was created.

### Added

- Repository governance
  - `AGENTS.md` — working contract for automated and human contributors: authority
    order, scope-lock rules, evidence-first rules, version and checkpoint rules,
    public-repository restrictions, device-access prohibition, destructive Git
    prohibition, Owner-only merge rule, Changed/Unchanged/Not Verified reporting,
    no-false-test-claims rule, and required stop conditions.
  - `SECURITY.md` — security policy, secret handling, vulnerability reporting,
    privileged-session expectations, and the public-repository secrecy boundary.
  - `CHANGELOG.md` — this file.
- Core documentation
  - `docs/CURRENT_STATE.md` — stage status, verified repository state, status legend, and
    open items.
  - `docs/MASTER_PLAN.md` — staged delivery plan, stage gates, and gate evidence rules.
  - `docs/ROADMAP.md` — forward view of planned stages and optional future capabilities.
  - `docs/REQUIREMENTS.md` — consolidated requirements with identifiers and status.
  - `docs/ARCHITECTURE.md` — conceptual architecture and subsystem responsibilities.
  - `docs/DOMAIN_MODEL.md` — entities, terminology, identifiers, and value ranges.
- Control, safety, and queue models
  - `docs/SAFETY_BOUNDARY.md` — hardware safety boundary, target safe states, and the
    bench verification required before any production write control is permitted.
  - `docs/CONTROL_AUTHORITY.md` — control authority matrix.
  - `docs/CLEANING_SEQUENCE.md` — Cleaning Job definition, normal sequence, and
    failure and recovery boundaries.
  - `docs/ALARM_MODEL.md` — condition, acknowledgement, and shelving dimensions,
    severities, and blocking-release rules.
  - `docs/QUEUE_MODEL.md` — TempQueue, TimeQueue, GlobalQueue, deduplication,
    FIFO refill, and operator queue actions.
  - `docs/HISTORIAN_RETENTION.md` — acquisition intervals, storage targets, retention
    proposals, and cleanup rules.
  - `docs/USER_PERMISSION_MODEL.md` — role templates, session rules, audit
    requirements, and the break-glass recovery account.
  - `docs/TEST_STRATEGY.md` — planned verification approach for future stages, and the
    explicit statement that no runtime testing has occurred.
  - `docs/PUBLIC_REPOSITORY_BOUNDARY.md` — what may and may not be published.
- Decision records
  - `docs/decisions/README.md` — ADR index and format.
  - `docs/decisions/ADR-0001-product-identity.md`
  - `docs/decisions/ADR-0002-deployment-architecture.md`
  - `docs/decisions/ADR-0003-queue-arbitration.md`
  - `docs/decisions/ADR-0004-historian-strategy.md`
  - `docs/decisions/ADR-0005-hardware-safety-boundary.md`

### Changed

- `README.md` — replaced the two-line placeholder with the repository entry point.
- `.gitignore` — extended the existing Visual Studio template with repository-specific
  exclusions for local production configuration, secrets, and exported data, so that
  confidential deployment material stays outside the public repository.

### Safety and scope notes for this entry

- No production device access was performed. Production device access remains
  `[NOT AUTHORIZED]`.
- WAGO watchdog fail-safe behaviour remains `[NOT VERIFIED]`.
- Motion limits, pulses per engineering unit, encoder behaviour, and pressure setpoints
  remain `[NOT VERIFIED]`.
- Production register maps and tag lists were not created and must never be committed;
  see `docs/PUBLIC_REPOSITORY_BOUNDARY.md`.
- No runtime, database, or hardware test was executed. Only documentation validation was
  performed. See `docs/CURRENT_STATE.md` for the validation record.
