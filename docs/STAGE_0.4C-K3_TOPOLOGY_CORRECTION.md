# Stage 0.4C-K3 — Owner Critical Topology Correction (Development Checkpoint Record)

**Status: DEVELOPMENT CHECKPOINT for Owner review on the existing Stage 0.4C-K3 branch
`arena/200cd6d0-waterjet-sentinel-suite` (PR #21 to `main`, OPEN, NOT MERGED, do not merge),
based on `main` `cc6ccf8779d87a5a44d1c03a003021791c26aac8`.** Owner Manual Review has **NOT**
passed. **Stage 0.4C-K3 is NOT claimed complete.** The .NET side of this correction was **NOT
performed** in Arena (no .NET SDK) and is listed exactly in §8; the Owner-local .NET suite is
expected to fail until it is performed.

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
| Same | `.cs` sources and tests, `packages/contracts/fixtures/snapshot.seed0.json`, `config/examples/sensor-parameters.migrated.example.json` | **Unresolved blocker — Owner-local .NET pass required** (§8); flagged in the fixtures and examples READMEs; refused, not accepted, by the Node validators |
| Same | `spikes/ui-runtime-react/**` source, fixtures and result summaries (`sensorMap.mjs` 106 Sensors / 2 Cannon slots / 212) | **Retained historical with supersession** (spike README, `CONTRACTS.md`, Owner-local testing and result summaries carry the banner; the spike is declared non-Product review code and already carries the earlier superseded "Cannon" vocabulary) |
| `G+212`, `G+106`, `SYN-REC-106`, `scanOrder 106` in the stale example | identifiers, not counts | **Unrelated numeric / part of the stale .NET example** |

## 8. .NET — NOT PERFORMED (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`)

No .NET SDK exists in Arena; no C# file was edited, because an uncompiled edit of the contract,
domain, runtime and fixture generators could not be tested and the WJ1 / WJ3 placement vocabulary
(§9) blocks a provable edit. Owner-local work required, in this order:

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

Until step 5 lands: the Owner-local .NET suite **fails** on example-parity and topology tests; the
TS mirror test `snapshot.seed0.json passes validation` **fails by design**; the Runtime Inspector
shows "Topology baseline STALE" and `STALE TOPOLOGY` cells. None of these is to be silenced.

## 9. Open Owner decisions

| # | Decision | Why it is open |
|---|---|---|
| OQ-T1 | Replacement placement vocabulary for WJ1 and WJ3 (formerly `NON_SENSOR_GAP` at I16 / I7; anchors `I15`,`I17` / `I6`,`I8`) | No existing `WaterJetPlacementKind` value is proven for them; inventing one is forbidden. The TS mirror and Node layers only refuse the superseded value |
| OQ-T2 | Executable location of the Disable / GlobalQueue semantics for I7 and I16 (Owner tests 23–34: parameter import of I7 / I16, unknown Sensor fails closed, default-enable migration rule, Disabled Sensor → no Cleaning Candidate / no GlobalQueue entry / no auto-dispatch / no new Job, immediate removal with `SENSOR_DISABLED`, FIFO preserved, no `BLOCKED` / `HELD` / `WAITING` / `SENSOR_DISABLED` entries retained, active Job not auto-aborted, re-enable does not resurrect) | The Node tree contains no importer, no queue engine and no `SENSOR_DISABLED` implementation; the .NET runtime is not editable in Arena. The rules are stated in `REQUIREMENTS.md` PHY-001 / `DOMAIN_MODEL.md` §2.2.1 and must be implemented and tested Owner-local |

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
| `node --test packages/contracts/wjss-contracts-ts/test/validate.test.mjs` | **34 pass / 1 fail by design** (`snapshot.seed0.json passes validation` — stale .NET golden fixture, §8) |
| `tsc --noEmit` (TS mirror) | NOT RUN (no `node_modules` in Arena; Owner-local `npm ci && npm run check`) |
| `git diff --check`, lock / csproj / props drift against `cc6ccf87` | clean / none |
| .NET build and 414 tests, Release build, browser rendering of the Preview and Inspector | NOT RUN (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`) |

## 11. Boundary review

No network action, no connected-storage access, no new dependency, no lock-file change, no
protocol or port, no socket, no device write. The six Test IP literals remain only in
`tcNodeConfiguration.mjs`. No connector metadata, candidate / range / port / hardware-verified /
acquisition-ready / `OWNER_SOURCE_LOCATED_NOT_EXTRACTED` wording was reintroduced (asserted by the
commissioning Group H docs-scan and test R43).
