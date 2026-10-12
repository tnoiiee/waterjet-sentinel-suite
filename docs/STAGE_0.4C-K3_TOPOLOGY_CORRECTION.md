# Stage 0.4C-K3 — Owner Critical Topology Correction (Development Checkpoint Record)

**Status: DEVELOPMENT CHECKPOINT for Owner review on the existing Stage 0.4C-K3 branch
`arena/200cd6d0-waterjet-sentinel-suite` (PR #21 to `main`, OPEN, NOT MERGED, do not merge),
based on `main` `cc6ccf8779d87a5a44d1c03a003021791c26aac8`.** Owner Manual Review has **NOT**
passed. **Stage 0.4C-K3 is NOT claimed complete.** The .NET side of this correction was
**performed source-side in the K3 Completion Pass (§12) but is NOT compiled and NOT tested in
Arena** (no .NET SDK — `NOT_RUN_ENVIRONMENT_UNAVAILABLE`); the Owner-local .NET build and test
run (§8 step 6) is the mandatory arbiter before this PR can be called K3-complete or PR-ready.

Authority line (unchanged): `SIMULATION / PREVIEW / EVIDENCE AND CONTRACT FOUNDATION ONLY / NO
NETWORK PROBE / NO SOCKET / NO PHYSICAL ACQUISITION / NO DEVICE WRITE / NO TEST_HARDWARE / NO
PRODUCTION / FIELD VERIFICATION PENDING`.

---

## 1. Owner ruling (final)

| Item | Revoked belief | Authoritative baseline |
|---|---|---|
| Product Sensors | 106 | **108** |
| Logical I7 and I16 | `NON_SENSOR_GAP` placement anchors (I7 → WJ3, I16 → WJ1); their Drawing rows topology conflicts | **Normal Product Sensors** — same parameters, state model, Front/Rear pairing, runtime, commissioning, mapping and queue rules as every other Sensor |
| Assigned Product TC channels | 212 | **216** (108 × 2) |
| Physical TC channels | 240 | **240** (6 Nodes × 40) |
| Unassigned physical channels | 28 (24 spare + 4 "gap-conflict") | **24** (240 − 216) |
| Sensors per wall (Left / Rear / Right / Front) | 24 / 29 / 24 / 29 | **24 / 30 / 24 / 30** |
| Sensor sequence | 106 identities, skipping I7 and I16 | **Exactly 108 identities:** G+201–G+218, G+101–G+118, G1–G18, H1–H18, I1–I18, J1–J18; I7 and I16 once each in ordinal position; no holes, no replacement identities, no renumbering |
| U-shaped layout | two gap cells | **Unchanged except that I7 and I16 are populated Sensor cells** |

The question whether I7 and I16 are Sensors is closed and is not asked again. Supersession sentence
used throughout the documentation set: *Superseded by the Owner critical topology correction: I7 and
I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned
Product TC measurements.*

## 2. Baseline proof (before any change)

| Check | Result |
|---|---|
| Branch / head | `arena/200cd6d0-waterjet-sentinel-suite` at `145b9cc5c376e1cb575f02ffa90afd64ca82bb67` (K3 evidence-honesty correction; parent `7c09008a…`), PR #21 OPEN against `main` `cc6ccf87…` |
| Revoked baseline present in source | `PROTECTED_BASELINE` 106 / 212 / 4 topology conflicts; `NON_SENSOR_GAP` in `schedule.mjs`, `wallmap.mjs`, `thermalpair.mjs`, `presentation.mjs`, `scenarios.mjs`, the Ops Preview server/UI/tests, `config/examples/sensor-map.example.json`, the TS mirror, the Inspector page, the .NET sources and fixtures, and the documentation set |
| Gates at `145b9cc5…` | Commissioning 181 · Ops Preview 129 · Kiosk 22 · Mapping UI 82 · mapping package 261 pass / 7 skipped · S2 14 · boundary scan 0 · lock drift 0 |
| .NET SDK | absent (`which dotnet` → none) |

## 3. Front/Rear measurement identity of I7 and I16 (not invented)

Taken from the committed Drawing schedule `docs/evidence/tc-drawing-schedule-rev1.csv` (Baseline
REV1, table and X110 diagram agreeing, as recorded by ISS-REV1-02 before it was closed as
superseded):

| Measurement | Node | Module | Channel | Conductor labels | State |
|---|---|---|---|---|---|
| I7 Front | `TC-NODE-05` (CAB5) | `A102.4` (750-498) | 7 | `-X110.31` / `-X111.31` | `DRAWING_VERIFIED`, `MATCHED` |
| I7 Rear | `TC-NODE-05` (CAB5) | `A102.4` (750-498) | 8 | `-X110.32` / `-X111.32` | `DRAWING_VERIFIED`, `MATCHED` |
| I16 Front | `TC-NODE-06` (CAB6) | `A102.4` (750-498) | 7 | `-X110.31` / `-X111.31` | `DRAWING_VERIFIED`, `MATCHED` |
| I16 Rear | `TC-NODE-06` (CAB6) | `A102.4` (750-498) | 8 | `-X110.32` / `-X111.32` | `DRAWING_VERIFIED`, `MATCHED` |

The 12 unrelated Drawing internal conflicts (ISS-REV1-01: G+107:R, G+108:F, G+116:R, G+117:F,
H7:R, H8:F, H16:R, H17:F, J7:R, J8:F, J16:R, J17:F) remain unresolved and untouched.

## 4. Central invariant validation

`packages/commissioning/src/topologyInvariants.mjs` (exported from the package index) is the single
place where the layers are compared: Sensor catalogue (served logical matrix), Sensor sequence,
unique IDs, Front / Rear pairing, total assigned, physical schedule, unassigned, UI Wall Map and,
when supplied, runtime projection counts. `validateTopologyInvariants` returns every violation with a
`TOPOLOGY_*` refusal code; `requireTopologyInvariants` throws `topology invariant violation`. It is
called by the commissioning package tests and by the Ops Preview `loadEvidence()` so the Preview
**fails closed** rather than serving a summary of 216 beside a catalogue of 106.

Asserted: 108 / 108 / 108 / 108 (catalogue / sequence / unique IDs / Wall Map), Front 108, Rear 108,
216 assigned, 240 physical, 24 unassigned; I7 = 1, I16 = 1; I7F / I7R / I16F / I16R = 1 each;
`NON_SENSOR_GAP` positions for I7 / I16 = 0; Sensors per wall 24 / 30 / 24 / 30.

## 5. Changed (Arena-native, this pass)

| Area | Change |
|---|---|
| `packages/commissioning/src/{schedule,wallmap,thermalpair,presentation,scenarios,index}.mjs` | 108 / 216 / 24 baseline; `POSITION_KIND = {SENSOR}`; `isProductSensorId`; gap vocabulary removed; Wall Map refuses any non-Sensor position, duplicate, missing position or missing Front/Rear side; `PRODUCT_TOPOLOGY_CONFLICT` class retained (0 rows) |
| `packages/commissioning/src/topologyInvariants.mjs` (NEW) + `test/topologyInvariants.test.mjs` (NEW) | central check; Owner tests T1–T11, M12–M22, R35–R44 (32 numbered tests) |
| `packages/commissioning/test/*` | expectations corrected; synthetic `PRODUCT_TOPOLOGY_CONFLICT` row used to keep the blocking rule covered; helper `FIRST_TOPOLOGY_CONFLICT` removed |
| `docs/evidence/tc-drawing-schedule-rev1.csv` | four I7 / I16 rows re-stated as `DRAWING_VERIFIED` / `MATCHED` Product assignments (204 / 12 / 0 / 24) |
| `docs/templates/sensor-to-tc-channel-schedule.template.csv` | 216 rows (I7:F, I7:R, I16:F, I16:R in ordinal order) |
| `config/examples/sensor-map.example.json` | regenerated: 108 `SENSOR` slots, 216 channels, 0 gaps, `sensorsPerWall` 24/30/24/30, synthetic device distribution 14,14,14,14,13,13,13,13 |
| `tools/sensor-map-example/generate.mjs` (NEW) | byte-exact Node port of `tests/integration/ExampleFiles.cs::SensorMap`; `--write` regenerates; `--legacy-proof` reproduces the superseded 106 form for comparison only |
| `apps/ops-preview/server.mjs`, `public/{app.mjs,index.html,styles.css}` | `requireTopologyInvariants` in `loadEvidence()`; `/api/sensor-map` carries `nonSensorGapSlotCount` and `topologyInvariants {ok, authoritative}` (no `nonSensorGapSlots`, no `logicalLabel`, no gap anchors); Sensor Map summary "Topology invariants AGREE (108 / 216 / 240 / 24)"; every cell carries `data-sensor-id`; wall-order navigation over all 108 Sensors |
| `apps/ops-preview/test/*` | expectations corrected (108 / 216 / 204 / 36 / 12); I7 / I16 selectable as Sensors; wall order I6:R → I7:F → I7:R → I8:F |
| `packages/contracts/wjss-contracts-ts/{validate.mjs,types.ts,test/validate.test.mjs}` | `LogicalPositionKind = 'SENSOR'`; `TOPOLOGY` constants; validator refuses `NON_SENSOR_GAP` positions, missing I7 / I16, `placementKind: NON_SENSOR_GAP`; 108 / 216 enforced for the snapshot |
| `packages/contracts/fixtures/README.md`, `config/examples/README.md` | stale-fixture disclosure and supersession |
| `apps/runtime/Inspector/index.html` | "Topology baseline" tile (AGREES / STALE against 108 / 216 / 0); a slot still published as `NON_SENSOR_GAP` renders `STALE TOPOLOGY`, never a gap, never a Sensor value; inline script syntax-checked with `node --check` only |
| Documentation | current-facing: `README.md`, `SECURITY.md`, `docs/{CURRENT_STATE,DOMAIN_MODEL,REQUIREMENTS,ARCHITECTURE,TEST_STRATEGY,ROADMAP,MASTER_PLAN,TC_EVIDENCE_SOURCE_REGISTER,TC_DRAWING_ISSUE_REGISTER_REV1}.md`, `docs/decisions/README.md`, `apps/{kiosk,ui}/README.md`, `adapters/simulator/README.md`, `CHANGELOG.md` (new entry), this record. Historical (banner only, text retained): ADR-0002/0003/0006/0007/0008/0009/0014/0015/0017 (ADR-0017 status: ACCEPTED — SUPERSEDED IN PART), `STAGE_0.3A-3_CHECKPOINT_{A,B,C}`, `STAGE_0.3A_PLAN`, `STAGE_0.3A_OWNER_LOCAL_VALIDATION`, `STAGE_0.4C-0{,A,B}_CHECKPOINT`, `STAGE_0.4C-K1_CHECKPOINT`, `docs/evidence/tc-schedule-validation-rev1.md` (truncated file; banner only), `docs/spikes/*`, `spikes/ui-runtime-react/**/*.md` |

## 6. Unchanged (verified)

`packages/commissioning/src/tcNodeConfiguration.mjs` and the scoped S2 exception (six literals, one
path); `tcAcquisitionEvidence.mjs` and every K3 evidence-honesty position (`PROTOCOL_UNRESOLVED`,
`OWNER_EVIDENCE_PENDING`, `WITHDRAWN`, `SOURCE_COMPLETENESS_INSUFFICIENT`, `PORT_UNRESOLVED`,
`DERIVATION_BLOCKED`, `RECORD_TO_RECORD_CONSISTENCY_ONLY`, `WIRE_BREAK_DECODING_BLOCKED`, no decoder
for 0x7FFF, the connected-storage disclosure, no archive metadata); S1–S9; the Kiosk shell and
WebView2 `1.0.4258.31`; the K2 identity across Kiosk / Preview / Diagnostics; the static vertical
U-shaped Wall Map; FrontRearDelta diagnostic-only; the commissioning workflow; sequential GlobalQueue,
one Active Cleaning Job, WJ1–WJ8 ↔ IV1–IV8, Pump / Isolation Valve pressure separation, Mandatory
Safe Return; every dependency and lock file; `packages/mapping-config` and `apps/mapping-config`
(no topology content). No network action, no connected-storage access, no socket, no probe.

## 7. Defect-class sweep (final; every remaining hit classified)

| Class | Where | Classification |
|---|---|---|
| `NON_SENSOR_GAP`, 106, 212, I7/I16 exclusion | Node source, Node tests, served example, schedule CSV, template, TS mirror, Inspector page, current-facing docs | **Corrected** (the remaining tokens name the refusal of the superseded form, e.g. `NON_SENSOR_GAP_POSITIONS: 0`, `TOPOLOGY_NON_SENSOR_GAP_PRESENT`, "no `NON_SENSOR_GAP` position exists") |
| Same | ADRs, checkpoint records, plans, spike documents, `CURRENT_STATE` §11 / §12 dated records, `CHANGELOG` history | **Retained historical with supersession** (banner or note; text unedited) |
| Same | `.cs` sources and tests, `packages/contracts/fixtures/snapshot.seed0.json`, `config/examples/sensor-parameters.migrated.example.json` | **Corrected in the K3 Completion Pass (§12)** — every executable `.cs` occurrence is on 108 / 216; the two generated files were regenerated by proven Node ports (§12.4); the remaining `106` / `212` / `NON_SENSOR_GAP` tokens in `.cs` are supersession comments or negative-test inputs (§12.2). **Owner-local compile + test still required** (§8 step 6) |
| Same | `spikes/ui-runtime-react/**` source, fixtures and result summaries (`sensorMap.mjs` 106 Sensors / 2 Cannon slots / 212) | **Retained historical with supersession** (spike README, `CONTRACTS.md`, Owner-local testing and result summaries carry the banner; the spike is declared non-Product review code and already carries the earlier superseded "Cannon" vocabulary) |
| `G+212`, `G+106`, `SYN-REC-106`, `scanOrder 106` in the stale example | identifiers, not counts | **Unrelated numeric / part of the stale .NET example** |

## 8. .NET — source edits PERFORMED (§12); build and tests NOT RUN (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`)

Historical note: at commit `7dc7c691…` no C# file had been edited (no SDK; WJ1 / WJ3 vocabulary
open). The Owner then ruled on the vocabulary (UNRESOLVED, §9) and authorised the K3 Completion
Pass, which performed steps 1–5 below source-side (§12). Step 6 remains Owner-local and
mandatory; the list is kept as the Owner-local verification order:

1. Resolve §9 (WJ1 / WJ3 placement vocabulary).
2. `packages/contracts/{Enums,Topology,TcChannels,WallMap,Config,Snapshot,SchemaIds}.cs`: remove
   `NonSensorGap` from `LogicalPositionKind` (and the WJ placement kind), remove
   `GapAnchorForWaterJetId`, `NonSensorGapSlots` / `NonSensorGapCount`; 108 / 216 constants;
   `sensorsPerWall` 24 / 30 / 24 / 30; schema identities as the Owner decides.
3. `packages/domain/{SensorParameterCsvImporter,TopologyValidator,WaterJetTopologyCatalog}.cs`:
   I7 / I16 imported and validated like every Sensor (no exclusion, no refusal code for them);
   `cannon n → WJn` unchanged; catalogue 108.
4. `adapters/simulator/Synthetic/SyntheticSensorMap.cs`, `tests/integration/ExampleFiles.cs`:
   adopt the 108 form already emitted by `tools/sensor-map-example/generate.mjs` (device
   distribution 14,14,14,14,13,13,13,13); `packages/application/Runtime/**`,
   `apps/runtime/RuntimeApiResponses.cs`: 108 Sensors, 216 channels, no gap tile data.
5. Regenerate `packages/contracts/fixtures/snapshot.seed0.json` and
   `config/examples/sensor-parameters.migrated.example.json` with the corrected generators
   (fixtures README §5 gate); update `tests/**` expectations (`SensorParameterMigrationTests`,
   `TcChannelContractTests`, `CanonicalMapStructureTests`, `MigratedExampleShapeTests`,
   `RuntimeTopologyTests`, `RuntimeApiTopologyTests`, `FixtureParityTests`, …).
6. Commands: `dotnet build WaterJetSentinelSuite.sln -c Release`;
   `dotnet test WaterJetSentinelSuite.sln -c Release`;
   `dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release`
   (`WJSS_UPDATE_FIXTURES=1` on diff); then
   `cd packages/contracts/wjss-contracts-ts && npm ci && npm run check` (the one failing Arena
   test `snapshot.seed0.json passes validation` must turn green on the regenerated fixture).

Status after §12: steps 1–5 are performed in source (compile-by-eye, uncompiled); the TS mirror
test `snapshot.seed0.json passes validation` is **green (36 / 0)** on the regenerated fixture; the
Runtime Inspector shows "Topology baseline AGREES" only when the (rebuilt) API publishes
108 / 216 / 240 / 24 and otherwise STALE — nothing was silenced. Step 6 is the open gate.

## 9. Open Owner decisions

| # | Decision | Why it is open |
|---|---|---|
| OQ-T1 | Replacement placement vocabulary for WJ1 and WJ3 (formerly `NON_SENSOR_GAP` at I16 / I7; anchors `I15`,`I17` / `I6`,`I8`) | **RESOLVED by Owner ruling (K3 Completion Pass):** installed position is `UNRESOLVED` (`WaterJetPlacementKind.UNRESOLVED`, `placementAnchors: []`, operator wording "Installed position pending authoritative layout"). Nothing is inferred from I7 / I16 / the former gap / Sensor wall / target wall / target Sensor / legacy cannon / Assigned Cleaning Device ID; Target Coverage unchanged. Implemented in the C# catalogue, validators, API view (`installedPositionNote`), Inspector, TS mirror and both regenerated files (§12.5) |
| OQ-T2 | Executable location of the Disable / GlobalQueue semantics for I7 and I16 (Owner tests 23–34) | **RESOLVED by Owner ruling (K3 Completion Pass):** implemented in the canonical .NET Runtime / Queue layer — the pure sequencing kernel (`SequencingState.DisabledSensorIds`, event `SetSensorEnablement`) plus the importer default-enable rule — with the I7 / I16 coverage tests of §12.6. Source-authored; Owner-local compile + test pending. Mutation UI / route: **NOT_IMPLEMENTED** (read-only exposure only) |
| OQ-T3 | Schema identities | The contract keeps `wjss.snapshot/2` and `wjss.delta/2`; whether the topology correction warrants `/3` is an Owner decision not taken here (no field was renamed; the removed keys — `gapAnchorForWaterJetId`, `nonSensorGaps`, `nonSensorGapSlots` — were superseded-only). Open |

## 10. Validation (Arena, Node v22.22.3)

| Gate | Result |
|---|---|
| `npm --prefix packages/commissioning test` | 214 pass / 0 fail (32 topology-invariant tests included) |
| `npm --prefix apps/ops-preview test` | 129 pass / 0 fail |
| `node --test apps/kiosk/test/shell.contract.test.mjs` | 22 pass |
| `npm --prefix apps/mapping-config test` | 82 pass |
| `npm --prefix packages/mapping-config test` | 261 pass / 7 skipped / 0 fail |
| `node --test tools/boundary-scan/test/s2Exception.test.mjs` | 14 pass |
| `node tools/boundary-scan/boundary-scan.mjs .` | 0 findings (S1–S9 clean) |
| `npm run check` (commissioning, ops-preview) | clean |
| `node --test packages/contracts/wjss-contracts-ts/test/validate.test.mjs` | **36 pass / 0 fail** after the K3 Completion Pass (regenerated golden; `UNRESOLVED` placement test added). Historical at `7dc7c691…`: 34 / 1 by design |
| `tsc --noEmit` (TS mirror) | NOT RUN (no `node_modules` in Arena; Owner-local `npm ci && npm run check`) |
| `git diff --check`, lock / csproj / props drift against `cc6ccf87` | clean / none |
| .NET build and 414 tests, Release build, browser rendering of the Preview and Inspector | NOT RUN (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`) |

## 11. Boundary review

No network action, no connected-storage access, no new dependency, no lock-file change, no
protocol or port, no socket, no device write. The six Test IP literals remain only in
`tcNodeConfiguration.mjs`. No connector metadata, candidate / range / port / hardware-verified /
acquisition-ready / `OWNER_SOURCE_LOCATED_NOT_EXTRACTED` wording was reintroduced (asserted by the
commissioning Group H docs-scan and test R43).

---

## 12. K3 Completion Pass (2026-10-12, same branch, same PR #21)

Owner-authorised consolidated pass. Baseline proven before editing: HEAD = remote PR head
`7dc7c6918f66d3a75a073446cfdfeb08284c44ab` (parent `145b9cc5c376e1cb575f02ffa90afd64ca82bb67`),
PR #21 OPEN, working tree clean, `main` at `cc6ccf87…`. No sandbox recreation occurred.

### 12.1 Authoritative topology (unchanged ruling, now encoded in .NET)

108 Sensors / 108 unique identities / 108 Front / 108 Rear / 216 assigned / 240 physical /
24 unassigned; I7 and I16 normal Sensors once each in ordinal position (sequence index 78 and 87,
scanOrder 79 and 88; I7 REAR wall column 3, I16 FRONT wall column 3); groups G+201–218,
G+101–118, G1–18, H1–18, I1–18, J1–18; walls 24 / 30 / 24 / 30; I7 / I16 topology-conflict rows 0;
`NON_SENSOR_GAP` positions 0.

### 12.2 .NET correction (source-authored; NOT compiled in Arena)

| Layer | Files | Change |
|---|---|---|
| Contracts | `Enums.cs`, `WallMap.cs`, `Topology.cs`, `TcChannels.cs`, `Config.cs`, `Snapshot.cs`, `SchemaIds.cs`, **new `TopologyInvariants.cs`** | `LogicalPositionKind = { SENSOR }`; `WaterJetPlacementKind` gains `UNRESOLVED`, loses `NON_SENSOR_GAP`; `WallMapSlot` / `LogicalPositionRecord` / `SensorMapSlotExample` lose `GapAnchorForWaterJetId`; `CanonicalSensorMap`: `SensorLocations` 108, `ThermocoupleChannelCount` 216, `PhysicalThermocoupleChannelCount` 240, `UnassignedThermocoupleChannelCount` 24, `SensorsPerWall` 24/30/24/30, `SensorSequence` (108), `CorrectedSensorIds` [I7, I16], `IsProductSensorId`; `NonSensorGapSlots` / `NonSensorGapCount` removed; `TcChannelRules` 108 / 216 with the central invariant check; refusal codes `TOPO_SENSOR_IDENTITY`, `TOPO_INVARIANT_VIOLATION`, warning `MIGRATION_ENABLE_DEFAULTED`; schema ids stay `/2` (OQ-T3) |
| Central invariants | `packages/contracts/TopologyInvariants.cs` | .NET equivalent of `topologyInvariants.mjs`: `TopologyObservation` → `Check` / `FirstViolation` / `Require`; codes `TOPOLOGY_ARITHMETIC_IDENTITY`, `_SEQUENCE_LENGTH`, `_SEQUENCE_ORDER`, `_DUPLICATE_SENSOR_ID`, `_SENSOR_MISSING`, `_SENSOR_UNKNOWN`, `_NON_SENSOR_GAP_PRESENT`, `_CATALOGUE_WALL_COUNT`, `_SCHEDULE_PHYSICAL_COUNT`, `_SCHEDULE_ASSIGNED_COUNT`, `_SCHEDULE_UNASSIGNED_COUNT`, `_FRONT_REAR_PAIRING`, `_UI_MAP_COUNT`, `_RUNTIME_COUNT`, `_FIXTURE_COUNT`, `_LAYER_DISAGREEMENT`; wired into `TopologyValidator` (importer), `TcChannelRules.RequireValidMapping` (example / fixtures) and `RuntimeStateInvariants` (runtime catalogue + wall map), each failing closed |
| Domain | `WaterJetTopologyCatalog.cs`, `TopologyValidator.cs`, `SensorParameterCsvImporter.cs` | WJ1 / WJ3 `UNRESOLVED`, anchors `[]`, `UnresolvedPlacementWording`; targets unchanged; validator requires all 108 positions to be SENSOR positions in the authoritative sequence, scanOrder dense 1..108, UNRESOLVED ⇒ no anchors, then the central invariants; importer treats I7 / I16 as ordinary rows, empty `sensor_enable` → Enabled + recorded warning, unknown token still refuses |
| Runtime | `RuntimeRefusalCodes.cs`, `RuntimeState.cs`, `RuntimeWallSummaries.cs`, `RuntimeStateInvariants.cs`, `SyntheticSensorMap.cs`, `SimulatorScenarioCatalogue.cs` | 108-Sensor synthetic map (device distribution 14,14,14,14,13,13,13,13), `WALL_MAP_NON_SENSOR_POSITION` / `TOPOLOGY_INVARIANT` refusals, wall summaries over 30 / 30 |
| Sequencing kernel (Disable) | `SequencingTopology.cs`, `SequencingCodes.cs`, `SequencingEvent.cs`, `SequencingState.cs`, `SequencingStateValidator.cs`, `SequencingKernel.cs` | §12.6 |
| API / host | `RuntimeApiResponses.cs`, `Program.cs`, `SimulatorRuntime.cs`, `Inspector/index.html` | status counts `logicalSlots` 108 / `sensorLocations` 108 / `thermocoupleChannels` 216 / `physicalThermocoupleChannels` 240 / `unassignedThermocoupleChannels` 24 / `correctedSensorIds` [I7, I16] (**removed:** `nonSensorGaps`, `nonSensorGapSlots`); `RuntimeWaterJetView.installedPositionNote` (UNRESOLVED only); `RuntimeStatus.disabledSensorIds` (read-only); Inspector tile compares 108 / 216 / 240 / 24 / 0, shows "Installed position pending authoritative layout" for UNRESOLVED, shows the Disabled list read-only |
| Generators / examples | `tests/integration/FixtureGenerator.cs`, `tests/integration/ExampleFiles.cs` | corrected to the 108 form (identical to the Node ports, §12.4) |
| Tests | `tests/config.tests/{SyntheticSensorParameterCsv,SensorParameterMigrationTests,MigratedExampleShapeTests,ExampleConfigTests,TcChannelContractTests}.cs`, `tests/domain.tests/{CanonicalMapStructureTests,**TopologyInvariantTests (new)**}.cs`, `tests/api.tests/{ContractEncodingTests,RuntimeApiTopologyTests,SimulatorFastTrackTests}.cs`, `tests/runtime.tests/{RuntimeSnapshotProjectionTests,RuntimeStateInvariantTests,RuntimeTopologyTests,SyntheticEvolutionTests,SyntheticInitialStateTests}.cs`, `tests/runtime.tests/Sequencing/{SequencingKernelTests,SequencingStateIntegrityTests,SequencingJobSafeReturnTests}.cs` | expectations moved to 108 / 216 / 24-30-24-30 / scanOrder 1..108 / I7 & I16 present / no gap property; timezone warnings 54, unit warnings 36, disabled 6; gap tests replaced by identity / UNRESOLVED / invariant refusal tests; Disable tests D1–D9 and state-integrity tests added |

Exact-source sweep after the pass (`grep -rn "\b106\b\|\b212\b\|NON_SENSOR_GAP\|NonSensorGap\|GapAnchor" --include=*.cs`):
every remaining hit is (a) a supersession comment naming the revoked belief, (b) the defensive
`NonSensorGapCount` observation field / `TOPOLOGY_NON_SENSOR_GAP_PRESENT` code that *refuses* a gap,
or (c) a negative-test input in `TopologyInvariantTests` proving the superseded 106 / 212 reading
is refused. `G+106` / `G+212` are Sensor identities. No executable layer is on 106 / 212.

### 12.3 Not performed / not verified

- **.NET build and tests: NOT RUN (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`).** All C# edits are
  compile-by-eye. Owner-local, in order: `dotnet build WaterJetSentinelSuite.sln -c Release`;
  `dotnet test WaterJetSentinelSuite.sln -c Release`;
  `WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release`
  (expected: no fixture diff against the Node-regenerated `snapshot.seed0.json`; any diff is a
  finding, not something to overwrite silently); `cd packages/contracts/wjss-contracts-ts && npm ci
  && npm run check` (`tsc`).
- Browser rendering of the Inspector / Preview: NOT RUN.
- Sensor Disable **mutation** UI / API route: **NOT_IMPLEMENTED** (outside the approved read-only
  architecture); the model, kernel behaviour and read-only exposure are implemented.

### 12.4 Fixture parity (generated files, never hand-edited)

| File | Generator | Proof |
|---|---|---|
| `config/examples/sensor-map.example.json` | `tools/sensor-map-example/generate.mjs` (port of `ExampleFiles.cs::SensorMap`) | `--legacy-proof` byte-identical to the superseded committed form; `--write` deterministic; this pass additionally removed the superseded `gapAnchorForWaterJetId: null` key from every slot (the C# record no longer has the property) |
| `packages/contracts/fixtures/snapshot.seed0.json` | **new** `tools/fixture-generator/generate.mjs` (port of `FixtureGenerator.cs`, pure formula) | `--legacy-proof` byte-identical to the previously committed 106-form file; `--write` → 108 Sensors / 216 channels / I7 scan 79 / I16 scan 88 / no gap key / WJ1 & WJ3 `UNRESOLVED []` / walls LEFT 24 · REAR 30 · RIGHT 24 · FRONT 30; second run `cmp` identical; `delta.basic.json`, `delta.gap.json`, `command.refusal.json` unchanged (re-derived identically) |
| `config/examples/sensor-parameters.migrated.example.json` | **new** `tools/migrated-example/generate.mjs` (same per-Sensor formulas as `SyntheticSensorParameterCsv.cs` → importer) | `--legacy-proof` byte-identical to the previously committed file; `--write` → 108 positions / 108 Sensors scanOrder 1–108 / I7 → WJ1 / I16 → WJ3 / disabled scans 17, 34, 51, 68, 85, 102 / no gap key / WJ1 & WJ3 `UNRESOLVED []`; deterministic |

Consumers agree in Arena: TS validator 36 / 0 on the regenerated golden; Node commissioning /
Ops Preview / mapping gates green on the regenerated sensor map; C# consumers are corrected to the
same form but **unverified** until the Owner-local run.

### 12.5 WJ installed-position ruling

`WaterJetPlacementKind.UNRESOLVED` for WJ1 and WJ3 with `placementAnchors: []`; no coordinates,
anchors or walls invented; API `installedPositionNote` = "Installed position pending authoritative
layout"; Inspector renders that wording; validators refuse anchors on an UNRESOLVED placement and
refuse `NON_SENSOR_GAP`; cannon n → WJn and WJn ↔ IVn unchanged; Target Coverage unchanged
(WJ1 → REAR LOWER, WJ3 → FRONT LOWER).

### 12.6 Sensor Disable / Queue behaviour (kernel; source-authored)

Model: Sensor ID + Enabled / Disabled as kernel state `SequencingState.DisabledSensorIds`
(ordinal-sorted, read-only view, default empty = every Sensor Enabled, I7 and I16 included; no
parallel store; the importer's `Enabled` is the configuration side with the default-enable rule).
Event `SetSensorEnablement(At, SensorId, Enabled, Topology)` (kind `SET_SENSOR_ENABLEMENT`).
Codes: `SENSOR_DISABLED` (admission refusal, disable applied, removal reason), `SENSOR_ENABLED`,
`SENSOR_ENABLEMENT_UNCHANGED` (no-op), `SENSOR_UNKNOWN` (unknown identity, fail closed — never
enters the state or the GlobalQueue), `ENTRY_INVALID` (blank), `CRITICAL_SUSPENDED` (latched).
Validator: `QUEUE_ENTRY_SENSOR_DISABLED`, `DISABLED_SENSOR_ID_BLANK`, `DISABLED_SENSOR_ID_DUPLICATE`.
Behaviour 1–14: a Disabled Sensor is never a Cleaning Candidate, never admitted, never
auto-dispatched, never gets a new Job; disabling a queued non-active entry removes it in the same
transition with reason `SENSOR_DISABLED` (queue revision +1, FIFO of the rest preserved); no
disabled / blocked / held / waiting entry remains; an Active Job targeting the Sensor is never
auto-aborted (evidence carries the JobId; Pump / Valve / Abort / Safe Return rules govern); no
future candidate while Disabled; re-enable clears the state only — no resurrection, no reuse of a
stale classification, new valid evidence and normal eligibility required.
Tests (source-authored): `SequencingKernelTests` D1–D9 (I7 / I16 enabled pass eligibility;
disabled cannot enter; disabling queued I7 removes only I7; disabling queued I16 preserves FIFO;
re-enable I7 no resurrection; re-enable I16 requires new evidence; neither is a topology
exception / unknown fails closed; Active-Job target disable never aborts; deterministic evidence);
`SequencingStateIntegrityTests` (disabled entry in queue refused; blank / duplicate refused; valid
read-only projection); `SensorParameterMigrationTests` (empty token → Enabled + warning;
explicit DISABLED on I16 imports like any Sensor; unknown token on I7 refuses; missing I16 row is a
position-count refusal, not a tolerated gap); `SimulatorFastTrackTests` (read-only exposure, no
mutation route). Mutation UI: NOT_IMPLEMENTED.

### 12.7 Validation (Arena, Node v22)

Commissioning **214 / 0** (one assertion tightened: the gap-anchor key must be absent, not null);
Ops Preview 129 / 0; Kiosk 22 / 0; Mapping UI 82 / 0; mapping package 261 pass / 7 skipped / 0
fail; S2 14 / 0; boundary scan 0 findings (S1–S9 clean); commissioning and ops-preview `check`
clean; contracts-ts **36 / 0**; lock / csproj / props drift against `cc6ccf87`: none;
`git diff --check` clean. .NET (414 + new tests), Release build, `tsc`, browser: **NOT RUN**.

### 12.8 Evidence status (unchanged, re-verified by R43 / Group H)

`PROTOCOL_UNRESOLVED`, `OWNER_EVIDENCE_PENDING`, candidate `WITHDRAWN`,
`SOURCE_COMPLETENESS_INSUFFICIENT`, `PORT_UNRESOLVED`, `DERIVATION_BLOCKED`,
`RECORD_TO_RECORD_CONSISTENCY_ONLY` (240 / 240, 0 mismatches), `WIRE_BREAK_DECODING_BLOCKED`
(0x7FFF: no decoder), `authoritative: false`, `activationPossible: false`, the connected-storage
disclosure and the archive-metadata-absent statement are untouched. No protocol candidate, no
process-image range, no port inference, no hardware verification, no acquisition-ready claim.

### 12.9 Boundary review

No network action, no connected-storage access, no new dependency, no lock-file change, no
protocol / port / socket / device write / MODBUS / probe / TEST_HARDWARE / PRODUCTION. The six Test
IP literals remain only in `tcNodeConfiguration.mjs` (S2 14 / 14). Protected behaviours unchanged
(U-shaped map, 108 ordinal sequence, FIFO GlobalQueue, one Active Job, WJ ↔ IV, pump / valve
separation, AI-002 trend-only, AI-003 pump-ready, Safe Return, valve-close classification,
FrontRearDelta diagnostic-only, Kiosk / WebView2, scoped S2).

### 12.10 Completion statement

**Stage 0.4C-K3 is NOT complete and PR #21 is NOT PR-ready**: the .NET validation (build + tests +
fixture parity) is unperformed in Arena. Everything that can run in Arena is green with zero
failures. PR #21 stays OPEN and unmerged.
