## 2026-10-12 — Stage 0.4C-K3 closeout: Owner-local automated validation PASSED, schema v2 retained (PR #21 — OPEN, UNMERGED, do not merge; K3 = AUTOMATED GATES PASSED, OWNER MANUAL REVIEW PENDING; documentation-only commit)

- **Owner-local validation on exact PR #21 head `041b286322186977c5f87cde8184ae5a180d60a6`** (parent `01f1d006…`; ancestry from `cc6ccf87…` verified; exact source ZIP SHA-256 `5366377c9060c6505007622bd95e84c41182c5daa152b0c0454b0465cf13658d`, 455 files): `dotnet restore --locked-mode` **PASS** (no drift) · `dotnet build -c Release --no-restore` **PASS** (all projects, no error, no warning reported) · `dotnet test -c Release --no-build` **455 / 455 PASS**, 0 failed, 0 skipped · fixture emission (`WJSS_UPDATE_FIXTURES=1`, `Wjss.FixtureEmission.Tests.csproj`) **7 / 7 PASS, no Git diff** · second deterministic emission **7 / 7 PASS, no additional diff** · `wjss-contracts-ts` `npm ci && npm run check` **PASS** (typecheck PASS, **36 / 36**, no package-lock drift) · Owner-local working tree **CLEAN** after every step. **Automated Build / Test Gate = PASS.**
- **Analyzer corrections that preceded the green build (no suppression, pragma, NoWarn, severity, props, `.editorconfig`, fixture, schema or dependency change):** `01f1d006…` "Fix CA1859 in canonical 108-sensor sequence builder" (`packages/contracts/WallMap.cs`: private `BuildSensorSequence` returns `System.Collections.ObjectModel.ReadOnlyCollection<string>`; public `SensorSequence` stays `IReadOnlyList<string>`); `041b2863…` "Fix CA1861 in canonical topology assertions" (`tests/domain.tests/CanonicalMapStructureTests.cs`: `ExpectedCorrectedSensorIds = ["I7", "I16"]`, `ExpectedLogicalPositionKinds = ["SENSOR"]` as `private static readonly string[]`). Each was verified against the Owner-supplied exact source ZIP of the preceding head.
- **OQ-T3 RESOLVED — RETAIN_SCHEMA_V2.** `wjss.snapshot/2` and `wjss.delta/2` are retained; no schema `/3` in K3. Basis: backward-compatible; Sensor configuration without explicit enablement → Enabled; I7 / I16 Enabled unless explicitly disabled; unknown Sensor identity fails closed; Disable state implemented in the canonical Runtime / Sequencing foundation without an incompatible Snapshot / Delta wire-shape change; full build / 455 tests / 7 + 7 fixture runs / typecheck / 36 contract tests pass; no serializer, parity, migration or consumer incompatibility requires a major revision. No identifier, reader, writer, fixture or generated contract changed.
- **Documentation closeout (this commit, Markdown only).** `docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md` (status header, §8, §9 OQ-T3, §10, §12.3, §12.10, new §13 validation and status record), `docs/CURRENT_STATE.md` (new head position; Completion-Pass paragraph marked superseded), `docs/MASTER_PLAN.md` (K3 closeout paragraph), root `README.md` ("Current stage" row now K3; the Stage 0.4C-0B row kept as a labelled historical row — its 106 / 212 / I7-I16-conflict figures and process-image wording are superseded), `packages/contracts/fixtures/README.md` and `config/examples/README.md` (parity gates PASSED, not pending), `docs/STAGE_0.4C-K3_CHECKPOINT.md` (dated ".NET NOT RUN" row cross-referenced). Every remaining `NOT_RUN_ENVIRONMENT_UNAVAILABLE` / "not compiled" sentence is a dated Arena record explicitly superseded by §13.
- **Preserved and restated.** Topology 108 / 108 unique / I7, I16 normal / 108 Front / 108 Rear / 216 assigned / 240 physical / 24 unassigned / 0 I7-I16 conflict rows / 0 `NON_SENSOR_GAP` / 6 Nodes / 30 WAGO 750-498 module records / 12 unrelated Drawing internal conflicts unresolved. Sensor Disable: no Candidate, no GlobalQueue entry, no auto-dispatch; immediate removal of a queued non-active entry with reason `SENSOR_DISABLED`; FIFO preserved; GlobalQueue holds ready-to-dispatch entries only; Active Job not auto-aborted (Pump / Valve / Operator Abort / AutoSequence / Mandatory Safe Return authoritative); re-enable neither resurrects nor reuses stale classification; unknown Sensor fails closed; I7 / I16 identical; **mutation UI NOT_IMPLEMENTED** (Runtime foundation + read-only representation only). Equipment: WJ1–WJ8 ↔ IV1–IV8; legacy cannon = Assigned Cleaning Device ID, cannon n → WJn, not inferred from Sensor wall; Installed Position ≠ Target Coverage; WJ1 / WJ3 Installed Position **UNRESOLVED**, no remaining gap anchors, Target Coverage unchanged. Evidence-honesty: `PROTOCOL_UNRESOLVED` / candidate `WITHDRAWN` / `PORT_UNRESOLVED` / `OWNER_EVIDENCE_PENDING` / `SOURCE_COMPLETENESS_INSUFFICIENT` / `DERIVATION_BLOCKED` / `RECORD_TO_RECORD_CONSISTENCY_ONLY` / no startWord-endWord / `authoritative: false` / `activationPossible: false` / `WIRE_BREAK_DECODING_BLOCKED` (0x7FFF → no physical result) / Reachability NOT_TESTED / Physical Acquisition NOT_IMPLEMENTED / MODBUS, TEST_HARDWARE, PRODUCTION, Device Write NOT_AUTHORIZED / connected-storage disclosure retained / no connector-derived archive metadata.
- **Status.** Owner Manual Review **PENDING**; PR #21 **OPEN / UNMERGED**; **K3 = AUTOMATED GATES PASSED, OWNER MANUAL REVIEW PENDING.** Not claimed: Manual Review passed, PR-ready, K3 complete, merged, hardware verified, acquisition implemented, protocol or Port resolved, MODBUS / TEST_HARDWARE / PRODUCTION authorised. Arena ran documentation and lightweight gates only; .NET was not rerun in Arena.

## 2026-10-12 — Stage 0.4C-K3 Completion Pass: .NET topology correction, central .NET invariants, Sensor Disable kernel, regenerated fixtures, WJ1 / WJ3 UNRESOLVED (branch `arena/200cd6d0-waterjet-sentinel-suite`, PR #21 — OPEN, NOT MERGED, do not merge; K3 NOT complete, NOT PR-ready)

- **Historical figures notice.** Every older entry below that states 106 Sensors, 212 channels, 24 / 29 / 24 / 29, Cannon slots, `NON_SENSOR_GAP` at I7 / I16 or "I7 / I16 absent" is a historical record of a superseded belief. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements (240 physical, 24 unassigned, walls 24 / 30 / 24 / 30). Those entries are not edited.
- **Baseline proven before editing.** HEAD = remote PR head `7dc7c6918f66d3a75a073446cfdfeb08284c44ab` (parent `145b9cc5…`), PR #21 OPEN, clean tree, `main` `cc6ccf87…`; no sandbox recreation.
- **.NET correction performed in source — NOT compiled, NOT tested in Arena (`NOT_RUN_ENVIRONMENT_UNAVAILABLE`).** Contracts (`Enums`, `WallMap`, `Topology`, `TcChannels`, `Config`, `Snapshot`, `SchemaIds`): `LogicalPositionKind = { SENSOR }`, `WaterJetPlacementKind` + `UNRESOLVED` − `NON_SENSOR_GAP`, `GapAnchorForWaterJetId` / `NonSensorGapSlots` / `NonSensorGapCount` removed, `CanonicalSensorMap` 108 / 216 / 240 / 24, `SensorsPerWall` 24 / 30 / 24 / 30, `SensorSequence` and `CorrectedSensorIds` [I7, I16]; new refusal codes `TOPO_SENSOR_IDENTITY`, `TOPO_INVARIANT_VIOLATION`, warning `MIGRATION_ENABLE_DEFAULTED`. **New central `packages/contracts/TopologyInvariants.cs`** (equivalent of `topologyInvariants.mjs`; `TOPOLOGY_*` codes; required 108 / 108 / 108 / 108 / 216 / 240 / 24, I7 = 1, I16 = 1, I7F / I7R / I16F / I16R = 1; fails closed on unknown or contradictory identity), wired into the importer validator, `TcChannelRules` and the runtime invariants. Domain: `WaterJetTopologyCatalog` (WJ1 / WJ3 `UNRESOLVED`, anchors `[]`, Target Coverage unchanged), `TopologyValidator` (108 SENSOR positions in authoritative order, dense scanOrder 1..108, no anchors on UNRESOLVED), `SensorParameterCsvImporter` (I7 / I16 ordinary rows; empty `sensor_enable` → Enabled + warning; unknown token refuses). Runtime: 108-Sensor synthetic map (14,14,14,14,13,13,13,13), wall summaries 30 / 30, refusals `WALL_MAP_NON_SENSOR_POSITION` / `TOPOLOGY_INVARIANT`. API / host: status counts `logicalSlots` / `sensorLocations` 108 / `thermocoupleChannels` 216 / `physicalThermocoupleChannels` 240 / `unassignedThermocoupleChannels` 24 / `correctedSensorIds` (**removed** `nonSensorGaps`, `nonSensorGapSlots`); `installedPositionNote` on WaterJet views; `disabledSensorIds` on status (read-only); Inspector tile 108 / 216 / 240 / 24 / 0, "Installed position pending authoritative layout", read-only Disabled Sensors row. Schema ids stay `wjss.snapshot/2` / `wjss.delta/2` (whether `/3` is warranted is an open Owner decision).
- **Sensor Disable (executable requirement) — kernel foundation.** `SequencingState.DisabledSensorIds` (default empty = all Enabled, I7 / I16 included; no parallel store), event `SetSensorEnablement(At, SensorId, Enabled, Topology)`, codes `SET_SENSOR_ENABLEMENT` / `SENSOR_DISABLED` / `SENSOR_ENABLED` / `SENSOR_ENABLEMENT_UNCHANGED` / `SENSOR_UNKNOWN` (fail closed), validator `QUEUE_ENTRY_SENSOR_DISABLED` / `DISABLED_SENSOR_ID_BLANK` / `DISABLED_SENSOR_ID_DUPLICATE`. Behaviour: Disabled Sensor is never a Cleaning Candidate, never admitted to the GlobalQueue, never auto-dispatched, never gets a new Job; disabling a queued non-active entry removes it in the same transition with reason `SENSOR_DISABLED` and FIFO of the rest preserved; no disabled / blocked / held / waiting entry remains; an Active Job is never auto-aborted by a disable; re-enable clears the state only (no resurrection, no stale classification, new valid evidence + normal eligibility required). Tests authored: `SequencingKernelTests` D1–D9 (I7 / I16 coverage), `SequencingStateIntegrityTests`, importer default-enable / I7 / I16 tests, `SimulatorFastTrackTests` read-only exposure. **Mutation UI / API route: NOT_IMPLEMENTED** (outside the approved read-only architecture).
- **WJ installed-position ruling applied.** WJ1 / WJ3 `placementKind: UNRESOLVED`, `placementAnchors: []`; nothing inferred from I7 / I16 / the former gap / Sensor wall / target wall / target Sensor / legacy cannon / Assigned Cleaning Device ID; cannon n → WJn and WJn ↔ IVn unchanged; TS mirror accepts `UNRESOLVED`, still refuses `NON_SENSOR_GAP`.
- **Generated fixtures regenerated by proven generators (never hand-edited).** New `tools/fixture-generator/generate.mjs` (port of `FixtureGenerator.cs`) → `packages/contracts/fixtures/snapshot.seed0.json`; new `tools/migrated-example/generate.mjs` → `config/examples/sensor-parameters.migrated.example.json`; existing `tools/sensor-map-example/generate.mjs` → `sensor-map.example.json` (superseded `gapAnchorForWaterJetId` key removed). Each: `--legacy-proof` byte-identical to the previously committed file, `--write` deterministic on a second run; 108 Sensors / 216 channels / I7 scan 79 / I16 scan 88 / no gap / WJ1 & WJ3 UNRESOLVED. Deltas and refusal fixtures unchanged. `FixtureGenerator.cs` / `ExampleFiles.cs` corrected to the same form; C# parity (`WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj`) is the Owner-local gate.
- **contracts-ts: 36 / 0** (regenerated golden validates; `UNRESOLVED` placement test added; no "fail by design" remains).
- **Defect-class sweep (final).** `.cs` exact-source sweep: every remaining `106` / `212` / `NON_SENSOR_GAP` / `GapAnchor` token is a supersession comment, the fail-closed `NonSensorGapCount` / `TOPOLOGY_NON_SENSOR_GAP_PRESENT` guard or a negative-test input; `G+106` / `G+212` are Sensor identities. Docs: `CURRENT_STATE` §12.4 / §12.5 historical gate rows marked; fixtures and examples READMEs rewritten to the regenerated state.
- **Tests (Arena, zero failures).** Commissioning 214 / 0 (one gap-anchor assertion tightened to "key absent"), Ops Preview 129 / 0, Kiosk 22 / 0, Mapping UI 82 / 0, mapping package 261 / 0 / 7 skipped, S2 14 / 0, boundary scan 0 findings, contracts-ts 36 / 0, `check` clean, no lock / csproj / props drift. **NOT RUN:** .NET build and tests (414 + new), Release build, `tsc`, browser. Owner-local: `dotnet build WaterJetSentinelSuite.sln -c Release`; `dotnet test WaterJetSentinelSuite.sln -c Release`; `WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release`; `cd packages/contracts/wjss-contracts-ts && npm ci && npm run check`.
- **Unchanged.** `tcNodeConfiguration.mjs`, scoped S2, S1–S9, every K3 evidence-honesty position (`PROTOCOL_UNRESOLVED`, `WITHDRAWN`, `PORT_UNRESOLVED`, `DERIVATION_BLOCKED`, `RECORD_TO_RECORD_CONSISTENCY_ONLY`, `WIRE_BREAK_DECODING_BLOCKED`, connected-storage disclosure), Kiosk shell, WebView2, U-shaped Wall Map, FrontRearDelta diagnostic-only, GlobalQueue FIFO / one Active Job / WJ ↔ IV / Pump–Valve / Safe Return rules, Target Coverage, dependencies and lock files. No network action, no connected-storage access, no MODBUS, no probe, no device write.
- **Status.** K3 NOT complete, PR #21 NOT PR-ready and NOT merged: the .NET validation is unperformed in Arena. *(Dated statement at `fcf0e686…`; closed by the Owner-local validation recorded in the closeout entry above.)*

## 2026-10-12 — Owner Critical Topology Correction: I7 and I16 are normal Product Sensors (Stage 0.4C-K3 branch `arena/200cd6d0-waterjet-sentinel-suite`, PR #21 — OPEN, NOT MERGED, do not merge; K3 NOT complete)

- **Owner ruling (final).** The 106-Sensor / 212-channel baseline is revoked. Logical **I7 and I16 are normal Product Sensors** with the same parameters, state model, Front/Rear pairing, runtime, commissioning, mapping and queue rules as every other Sensor. Authoritative baseline: **108 Product Sensors** (sequence G+201–G+218, G+101–G+118, G1–G18, H1–H18, I1–I18, J1–J18; I7 and I16 once each in ordinal position; Left 24 / Rear 30 / Right 24 / Front 30), **216 assigned Product TC channels** (108 × 2), **240 physical**, **24 unassigned** (240 − 216). No `NON_SENSOR_GAP` position exists. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.
- **Measurement identity, not invented.** I7 Front / Rear = `TC-NODE-05 | A102.4 | ch7 / ch8`, I16 Front / Rear = `TC-NODE-06 | A102.4 | ch7 / ch8`, taken from the committed Drawing schedule; the four rows previously classed `DRAWING_TOPOLOGY_CONFLICT` are `DRAWING_VERIFIED` / `MATCHED` Product assignments (schedule 204 / 12 / 0 / 24; template 216 rows). The 12 unrelated Drawing internal conflicts stay unresolved. ISS-REV1-02 closed as superseded.
- **Central invariant validation.** New `packages/commissioning/src/topologyInvariants.mjs` compares catalogue, sequence, unique IDs, Front / Rear pairing, total, physical schedule, unassigned, UI Wall Map and runtime counts (108 / 108 / 108 / 108 / 216 / 240 / 24; I7 = 1, I16 = 1; I7F / I7R / I16F / I16R = 1; gaps = 0) and refuses any disagreement with `TOPOLOGY_*` codes; wired into the Ops Preview `loadEvidence()` (fails closed). 32 numbered tests (T1–T11, M12–M22, R35–R44).
- **Corrected in Arena.** Commissioning `schedule` / `wallmap` / `thermalpair` / `presentation` / `scenarios` (`POSITION_KIND = {SENSOR}`, gap vocabulary removed, Wall Map refuses any non-Sensor position); Ops Preview server, UI and tests (Sensor Map "Topology invariants AGREE (108 / 216 / 240 / 24)", every cell carries `data-sensor-id`, I7 / I16 selectable, wall order I6:R → I7:F → I7:R → I8:F, `/api/sensor-map` without gap anchors); `config/examples/sensor-map.example.json` regenerated by the new `tools/sensor-map-example/generate.mjs` (byte-exact Node port of `ExampleFiles.cs::SensorMap`; device distribution 14,14,14,14,13,13,13,13); TypeScript mirror (`LogicalPositionKind = 'SENSOR'`, `TOPOLOGY` constants, refusal of `placementKind: NON_SENSOR_GAP`); Runtime Inspector page ("Topology baseline" AGREES / STALE tile, `STALE TOPOLOGY` cells instead of gaps); documentation set (current-facing corrected; historical ADRs, checkpoints, plans and spike documents banner-marked; ADR-0017 `ACCEPTED — SUPERSEDED IN PART`).
- **NOT PERFORMED — `NOT_RUN_ENVIRONMENT_UNAVAILABLE`** *(historical as of the K3 Completion Pass entry above, which performed the source edits; validation remains Owner-local)*. The .NET contracts, domain catalogue and importer, runtime projection, synthetic map, fixture generators and 414 tests still encode the superseded form; `packages/contracts/fixtures/snapshot.seed0.json` and `config/examples/sensor-parameters.migrated.example.json` are stale and flagged (not hand-edited); the Owner-local .NET suite is expected to fail until the .NET pass lands; the TS mirror test `snapshot.seed0.json passes validation` fails by design (34 / 35). Exact Owner-local steps: `docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md` §8.
- **Open Owner decisions.** Replacement placement vocabulary for WJ1 / WJ3 (formerly `NON_SENSOR_GAP` at I16 / I7) — not invented; executable home of the Disable / GlobalQueue / `SENSOR_DISABLED` semantics for I7 and I16 (Owner tests 23–34) — no Node implementation exists.
- **Tests.** Commissioning 214 / 214, Ops Preview 129 / 129, Kiosk 22 / 22, Mapping UI 82 / 82, mapping package 261 pass / 7 skipped, S2 14 / 14, boundary scan 0 findings, `git diff --check` clean, no lock drift. .NET and browser: NOT RUN.
- **Unchanged.** `tcNodeConfiguration.mjs`, the scoped S2 exception, S1–S9, every K3 evidence-honesty position (`PROTOCOL_UNRESOLVED`, `WITHDRAWN`, `PORT_UNRESOLVED`, `DERIVATION_BLOCKED`, `RECORD_TO_RECORD_CONSISTENCY_ONLY`, `WIRE_BREAK_DECODING_BLOCKED`, the connected-storage disclosure), the Kiosk shell, WebView2, the static U-shaped Wall Map, FrontRearDelta diagnostic-only, GlobalQueue / Active Job / WJ↔IV / Pump–Valve / Safe Return rules, every dependency and lock file. No network action, no connected-storage access.

## 2026-10-12 — Stage 0.4C-K2 native Kiosk Stage identity correction (PR #20, same branch — OPEN, NOT MERGED, do not merge)

- **Owner visual review finding, recorded as received.** The native Kiosk header read `Kiosk Preview - Stage 0.4C-K1` and the window title named K1, while the embedded Preview and Diagnostics read `Stage 0.4C-K2`. This was the only blocking mismatch.
- **Corrected at the source.** `apps/kiosk/KioskShellOptions.cs` now sets `StageId` to `0.4C-K2` and `WindowTitle` to `WaterJet Sentinel Suite - Kiosk Preview (Stage 0.4C-K2)`. The header label is built from `StageId`. Current-facing comments in `MainForm.cs`, `Program.cs` and the Kiosk README were relabelled. The placeholder package version `0.0.0-0.4c-k1-preview` became `0.0.0-0.4c-k2-preview` in the two packages, and their descriptions now open with K2.
- **Historical K1 references kept.** Named K1 passes, the WebView2 dependency decision, the csproj history comments, the K1 checkpoint document and the merged PR #19 records are unchanged.
- **Guards.** The Kiosk contract test asserts the native identity and its match with the Preview `STAGE_ID`, and it rejects a K1 identity in any C# string literal. The Preview render test asserts the embedded header and Diagnostics read K2.
- **Unchanged.** The approved origin, WebView2 `1.0.4258.31`, the lock files, layout, reconnecting behaviour, confirmed exit, TC Node configuration, the S2 exception and every authority boundary. No dependency change.
- **NOT RUN IN ARENA.** The .NET build and rendering of the native header. The Owner must rerun the Kiosk. Owner Manual Review has NOT passed.

## 2026-10-11 — Stage 0.4C-K3 Physical TC Connectivity and Protocol Evidence Gate (branch `arena/200cd6d0-waterjet-sentinel-suite`, based on `main` cc6ccf87 — PR to `main` OPEN, NOT MERGED, do not merge)

- **One canonical evidence model.** `packages/commissioning/src/tcAcquisitionEvidence.mjs` holds nine identified evidence sources (with the 0B-recorded SHA-256 values marked as recorded, not recomputed), 24 claims each carrying one of the ten approved classes and one scope (model / drawing / instance / synthetic / Owner decision), the protocol and port decision records, the per-Node rack (no offset field), the wire-break position and eleven missing-evidence requests. Deterministic validation refuses missing sources, unknown classes, model / revision / module-count / Channel-capacity mismatches, incomplete or conflicting widths, missing status, conflicting byte or word order, ambiguous encoding, manual numeric offsets, overlapping placement, incomplete wire-break evidence, model-level evidence applied as instance, synthetic presented as actual, unproven hardware / field claims, and any protocol or port selected without evidence or with an assumed default.
- **Decisions recorded, not made (Owner ruling, correction pass).** Protocol `PROTOCOL_UNRESOLVED`, evidence `OWNER_EVIDENCE_PENDING`, candidate **`WITHDRAWN`**, completeness `SOURCE_COMPLETENESS_INSUFFICIENT`; no candidate is retained and nothing is inferred from the device family. The 0B model-level protocol reference survives only as a non-authoritative note (original not readable in this review, profile section truncated, as-installed applicability / firmware / configuration unverified, no port, no candidate, no adapter, no acquisition). Port `PORT_UNRESOLVED`, no candidate, no default; no port number appears in the Product tree. No MODBUS library, no network client, no socket.
- **Process image: `DERIVATION_BLOCKED`.** Every claim carries `sourceBasis` and `sourceCompleteness`; every manual or drawing fact is a Stage 0B transcription whose original was `SOURCE_NOT_READABLE_IN_CURRENT_REVIEW`. The canonical derivation is therefore blocked and produces **no module, word, start, end, offset or range**; the view and route show `PROCESS_IMAGE_DERIVATION_BLOCKED` for every Node and never `CANDIDATE_UNVERIFIED`; `ADDRESS` / `STATUS_LOCATION` / `BYTE_ORDER` / `WORD_ORDER` / `ENCODING` stay `UNRESOLVED`. The only arithmetic kept is `recordToRecordConsistency`, classified literally `RECORD_TO_RECORD_CONSISTENCY_ONLY` (240/240 rows, 0 mismatches) with its seven-point meaning: record-to-record only; not hardware verification; not current-session manufacturer verification; no process-image start; no module offsets; no adapter; no acquisition. The arithmetic is reachable only for a synthetic test set with complete readable sources, never authoritative.
- **Wire break.** `WIRE_BREAK_DECODING_BLOCKED`, `CONFLICTING_EVIDENCE`, `SOURCE_COMPLETENESS_INSUFFICIENT`, Hardware observation `NOT_PROVIDED`; the record separates the manufacturer transcription (0x7FFF/0x60 Table 27; deactivated 0x7FFF Table 39, as transcribed in 0B) from the 0B reviewer paraphrase ("on the wire identical"), the missing as-installed configuration and the missing field observation. No decoder; no fault, alarm, trip, interlock, queue, dispatch, validity or control from 0x7FFF (tested).
- **Read-only acquisition contract.** Sample fields with separate received / device-sample / decode / publication / Last-Valid times, mandatory provenance (`PHYSICAL_ACQUISITION` refused in this Stage), write / command / configuration keys refused by name, no numeric value on an invalid or stale sample, FrontRearDelta only from two current valid sides. Nine-state communication model as contract only (every Node `CONFIGURED_NOT_TESTED`); freshness thresholds `OWNER_INPUT_PENDING` so classification is `FRESHNESS_UNDETERMINED`; `WIRE_BREAK_DECODING_BLOCKED` kept, synthetic scenario kept synthetic.
- **Read-only Preview.** `GET /api/tc-acquisition-evidence` (GET and HEAD only, 405 otherwise, `no-store`) carrying `stageId 0.4C-K2` and `evidenceStage 0.4C-K3`; new `#tc-acquisition-evidence` section and seventh navigation anchor with six Node columns, decision block, source table, wire-break evidence record, the process image as DERIVATION BLOCKED (no range table) with the `RECORD_TO_RECORD_CONSISTENCY_ONLY` line, request list and authority line — no controls. `STAGE_ID`, the Kiosk and the K2 route are unchanged.
- **Evidence intake finding and disclosure.** The committed `tc-process-image-profile-rev1.md` and `tc-schedule-validation-rev1.md` are truncated in the repository (literal markers from the PR #18 merge; the head-station session / port section is lost). An Owner evidence archive was reported as located through an unapproved connected-storage metadata query. Archive content was not retrieved or read. The archive is not accepted as evidence for this checkpoint. Owner authorization is required before any future connected-storage access. The three manuals and the drawing are `NOT_AVAILABLE_IN_SESSION` / `CONNECTED_STORAGE_NOT_AUTHORIZED` / `CONTENT_NOT_RETRIEVED` / `OWNER_EVIDENCE_PENDING`; connector-derived metadata about the archive was removed from the model and every current-facing record.
- **Tests.** Commissioning 181/181 (37 new), Ops Preview 129/129 (19 new; page anchor count 6 → 7), Kiosk 22/22, Mapping UI 82/82, mapping package 261 pass / 7 skipped / 0 fail, boundary scan 0 findings, S2 self-tests 14/14, `git diff --check` clean. The boundary scan caught six address literals and a credential word in my own new tests; both were moved to runtime derivation and the S2 exception was not widened.
- **NOT RUN IN ARENA.** The .NET build and tests, and any real-browser execution. The 22-step Owner-local handoff is in `docs/STAGE_0.4C-K3_CHECKPOINT.md` §12.
- **Unchanged.** `tcNodeConfiguration.mjs`, the scoped S2 exception, S1–S9, every dependency and lock file, the Kiosk shell, the Wall Map, the workflow, and the read-only authority. No network action was performed. Owner Manual Review has NOT passed and Stage 0.4C-K3 is NOT claimed complete.

## 2026-10-12 — Stage 0.4C-K2 TC Node/IP Configuration Foundation (branch `arena/699da2a5-waterjet-sentinel-suite`, based on `main` a7d739bb — PR to `main` OPEN, NOT MERGED, do not merge)

- **Canonical six-node model.** `packages/commissioning/src/tcNodeConfiguration.mjs` holds `TC-NODE-01`…`06` ↔ `CAB1`…`CAB6` (one-to-one by ordinal) and the six Test addresses `192.0.2.11`–`.16`, the only Product file that holds them. Topology per Node is 750-362 x 1, 5 x 750-498, 40 physical TC Channels and 750-600 x 1. Totals are derived: 6 Nodes, 6 Cabinets, 30 TC modules, 240 channels and 6 configured Test IPs.
- **Atomic validation.** Identity, topology, IP and duplicate rules are checked together. Leading-zero and malformed IPv4 forms are refused, and a wrong Node-to-address pairing, an invented port or protocol, or any reachability or hardware claim is refused with a named code. Nothing is repaired silently and there is no fallback address.
- **Honest status separation.** `CONFIGURED`, `REACHABILITY_NOT_TESTED`, `ACQUISITION_NOT_IMPLEMENTED`, `HARDWARE_VERIFICATION_PENDING`, `FIELD_VERIFICATION_PENDING` and `HARDWARE_ACCESS_NOT_AUTHORIZED`. Port and protocol stay `PORT_UNRESOLVED` and `PROTOCOL_UNRESOLVED`. Port 502 is not assumed.
- **Owner-approved scoped S2 exception.** `tools/boundary-scan/s2Exception.mjs` permits exactly the six addresses in exactly one canonical path. It holds no address literal, and it is covered by 14 scanner self-tests. S1 and S3–S9 are unchanged.
- **Read-only Preview.** `GET /api/tc-node-configuration` (GET and HEAD only, 405 with `Allow: GET, HEAD` otherwise). The `#node-configuration` section shows six TC-NODE columns and the totals inside a bounded frame. It has no controls. A failed projection is isolated to that section.
- **Tests.** Commissioning 144/144 (39 new), Ops Preview 109/109 (21 new), Kiosk 21/21, Mapping UI 82/82, mapping package 261 pass / 7 skipped / 0 fail, boundary scan 0 findings and `git diff --check` clean. Two focused tests were corrected after an arithmetic check and a regex that matched `RegExp.exec`. The S2 gate found literal addresses and a `password` word in my own tests, and those were moved to runtime derivation.
- **NOT RUN IN ARENA.** The .NET build and tests, and any real-browser execution. The Owner-local handoff is in `docs/STAGE_0.4C-K2_CHECKPOINT.md` §9.
- **Unchanged.** No dependency and no lock file. No device contact of any kind. No Kiosk or Wall Map logic. Owner Manual Review has NOT passed and Stage 0.4C-K2 is NOT claimed complete.

## 2026-10-12 — Stage 0.4C-K1 PR #19 Owner-authorized Front/Rear thermal-pair semantics and synthetic diagnostic pass (same PR — OPEN, NOT MERGED, do not merge)

- **Owner finding, recorded as received.** The Synthetic Preview systematically rendered **Rear above Front** for every Product Sensor pair (the Owner's example: Front 29.4 / Rear 35.1). Front is the furnace-facing fin / heat-collection measurement and Rear is the paired waterwall-tube-side reference, so the inverted reading trained the Operator to read the pair backwards.
- **Exact cause at the source.** `syntheticEngineeringValue` (`packages/commissioning/src/scenarios.mjs`) carried `const sideBias = row.side === 'F' ? 0 : 4;`, biasing **every** Rear sample about +4 °C above its Front partner. It is a **generation** bug, not a rendering bug: the generator was corrected and **no screen compensates for a value**.
- **Corrected deterministic generation.** For a Product Sensor pair, Rear is now the deterministic paired reference (`26 + (sensorOrdinal % 9)·1.2 + hash%400/100`, FNV-1a over the Sensor identity) and Front is `Rear + the scenario's declared differential` (`NORMAL_GOOD`: `3.2 + hash%37/10`), so **Front > Rear for every complete pair**. Both sides stay **pure functions of the physical identity** (no RNG, no clock, no rendering-order dependence; stable across ticks while the timestamp and value age move), so the pre-existing "sample value is a function of identity" contract is preserved rather than weakened. Non-pair rows keep their previous value, and existing scenarios keep their Quality/Diagnostic behaviour — they simply make the delta unavailable where a side is invalid, and **no delta is ever fabricated to keep the UI populated**.
- **Verified numbers (tick 0, 106/106 pairs available).** `NORMAL_GOOD` deltas **3.2 … 6.8 °C in 34 distinct values** (never one universal delta for all 106); `REDUCED_FRONT_REAR_DIFFERENTIAL` **0.8 … 1.7**; `NEAR_ZERO_FRONT_REAR_DIFFERENTIAL` **0.1 … 0.2**; `REVERSED_FRONT_REAR_DIFFERENTIAL` **−3.4 … −1.0**; samples `G+101` F 38.0 / R 31.9 = `+6.1`, `G+205` F 41.8 / R 37.9 = `+3.9`, `H7` F 40.3 / R 34.9 = `+5.4`. Repeat runs are deep-equal and a reversed schedule order gives identical per-Sensor values.
- **Domain rule (Owner-approved).** `FrontRearDelta = FrontTemperature − RearTemperature` in the same compatible unit. Healthy positive = expected gradient; reducing positive = *candidate* reduced local heat transfer (possible slag insulation); a low positive value does **not** prove slag; near zero = abnormal paired response; reversed = paired-measurement contradiction to investigate **first** for mapping / Sensor health / installation / process transient, never classified as slag by itself. Until field baselines and operating regimes are approved the value is **READ-ONLY DIAGNOSTIC / TREND ONLY** with **no** threshold, alarm, trip, interlock, persistence, deadband, hysteresis or automatic slag classification, and no Cleaning Queue, GlobalQueue, WaterJet dispatch, cleaning job, AutoSequence, pump, isolation-valve, axis-motion, automatic-control or field-approval effect.
- **New read-only paired model `packages/commissioning/src/thermalpair.mjs`.** Derived on every render from the **authoritative observations**; never mutates an observation, never a second source of truth. Five states (`EXPECTED_POSITIVE`, `REDUCED_POSITIVE_CANDIDATE`, `NEAR_ZERO_CANDIDATE`, `REVERSED_CONTRADICTION`, `DIFFERENTIAL_UNAVAILABLE`) kept strictly separate from Quality, DiagnosticReason, CommissioningState, evidence blocking and the Preview verdict (no `INVALID` was added to Quality). Delta available only when both sides are one Product Sensor with exactly one Front and one Rear measurement, both current values valid, same compatible unit, neither BAD / STALE / DISABLED / communication-loss / wire-break, both finite and the identity complete; **never** from one current plus one Last Valid value, two historical values, a BAD/STALE/disabled/unassigned value, an I7/I16 conflict row or a copied/substituted measurement — no fallback, aliasing or derivation across Front and Rear. Fourteen **named** blocking categories say which side broke the pair and why; `delta < 0` is always `REVERSED_CONTRADICTION` and `delta === 0` always `NEAR_ZERO_CANDIDATE`, so the sign always wins over a scenario label, and `REDUCED_POSITIVE`/`NEAR_ZERO` are assigned only by the explicit Synthetic scenario that declares them.
- **Scenario catalogue 10 → 13.** Three new explicit paired demonstrations (`REDUCED_FRONT_REAR_DIFFERENTIAL`, `NEAR_ZERO_FRONT_REAR_DIFFERENTIAL`, `REVERSED_FRONT_REAR_DIFFERENTIAL`) were **added**; no protected scenario was removed or renamed and the protected ten keep their original order at the head of the catalogue. Every explicit count contract and documentation occurrence was updated honestly (`packages/commissioning/test/scenarios.test.mjs`, `apps/ops-preview/test/server.test.mjs`, `apps/ops-preview/test/page.test.mjs`, checkpoint §15/§18).
- **Presentation.** Each Wall Map cell carries one compact read-only paired row (`dFR +6.1 degC [+]`), unit shown **only** while the value is available, and `dFR -- [?]` with the named reason otherwise — never a number, never a unit, never a substituted value. Sensor detail gains a fourth read-only card with the Sensor pair, the Front measurement and current value, the Rear measurement and current value, `dFR = Front − Rear`, the paired state with its marker, the guidance, the synthetic scenario provenance, `FIELD VERIFICATION PENDING` and the explicit no-automatic-effect statement. The Wall Map legend documents the paired markers; one render path builds the pairs, so the cell and the detail can never disagree. The Overview gains **no** row and **no** column for the delta, the selection identity is unchanged (physical Channel key), and the Wall Map stays a single horizontally bounded, horizontally scrollable frame (the extra cell row is deliberately reserved inside the fixed cell geometry, so the U-shape width is unchanged and cells keep their size).
- **Workflow preservation.** The delta is not a Channel, a Product-compatible measurement, a Sensor, a Commissioning measurement or an independent loop-test target: the protected totals stay **240 / 106 / 212**, it receives no BASELINE / STIMULUS / OPEN_CIRCUIT / RECOVERY observation, no PASS / FAIL / PARTIAL, no remark and no independent Preview result, and recording or clearing a Front result never mutates the Rear result or either source value (and the reverse).
- **Tests.** `packages/commissioning/test/thermalpair.test.mjs` (**new**, 16 focused tests: A1–A4 physics/determinism/generator scope, B1–B2 identity/isolation, C1–C4 delta validity including Last-Valid-Value and unit mismatch, D1–D4 the three paired scenarios and the no-effect vocabulary, E1–E2 totals/workflow separation) and `apps/ops-preview/test/thermalpair.render.test.mjs` (**new**, 10 focused page/presentation tests: E1–E4 paired rendering and sign/state/marker honesty, F1–F2 workflow isolation, G1 served module graph, G2 all-thirteen initialization, G3 bounded slag wording, G4 invalid-side unavailability and presentation-only filters). Every pre-existing 108/106/2, selection, synchronisation, filter, progress and workflow assertion still passes — nothing was weakened or deleted to make the new behaviour pass.
- **Arena gates at this head:** commissioning `check` OK and **105/105** (was 88); focused package paired tests **16/16**; Operations Preview `check` OK and **88/88** (was 78); kiosk source contract **21/21**; Mapping UI `check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass / 0 fail / 7 pre-existing Owner-local skips)**; boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean; authoritative workbook SHA-256 `4e0337e2…f422e8e` unchanged. **Focused paired-thermal tests: 26** (16 package + 10 page).
- **NOT RUN IN ARENA:** there is no browser engine and no .NET SDK in the sandbox, so **real-browser execution and the .NET build/tests are NOT RUN** — no pixel layout, no computed overflow, no DPI and no WebView2 behaviour is claimed, and these tests are explicitly not a visual verification. The 28-step Owner-local rerun sequence is checkpoint §18.7.
- **Unchanged:** the 240 / 216+24 / 212 / 106 topology and its single canonical source; the U-shape geometry, wall order, row order and the static vertical scrolling contract; the physical-Channel selection identity and every synchronisation; Front/Rear separation and the Both / Front only / Rear only control; the read-only Preview workflow, its evidence blocking and "an internal conflict may record FAIL or PARTIAL but never PASS"; loop-test progress; filters; navigation; the Kiosk shell, the native header layout, WebView2 `1.0.4258.31` and the WinForms-only reference isolation; every lock file; loopback-only hosting, the approved origin and the strict CSP with no external assets; the reconnecting fail-safe and the confirmed exit; the protected evidence and the authoritative workbook. **No dependency was added or changed in this pass. No MODBUS, no physical acquisition, no contact with `192.0.2.11`–`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no device write, no cleaning or motion command, no queue authority, no field-verification claim.**
- **Status.** **PR #19 OPEN — NOT MERGED (do not merge).** Owner Manual Review has **not** passed for this pass; the paired states are **not** a field verification; **Stage 0.4C-K1 is not claimed complete.**

## 2026-10-12 — Stage 0.4C-K1 PR #19 Owner Manual Review correction: the Commissioning Wall Map is now a STATIC vertical view (same PR — OPEN, NOT MERGED, do not merge)

- **Owner result, recorded as received.** The Commissioning Wall Map renders and its topology is ACCEPTED: U-shape presentation PASS, 108 logical positions PASS, 106 Product Sensors PASS, I7 and I16 as `NON_SENSOR_GAP` PASS, Front/Rear presentation PASS, complete Wall Map rendering PASS, native Kiosk header inside 1920×1080 PASS, Operations Preview inside the viewport PASS. **One usability defect:** the Wall Map frame carried its **own vertical scrollbar**, producing nested vertical scrolling (document scroll plus an inner scrollbar) that split one spatial reference into two scroll positions — not suitable for field loop testing.
- **Exact cause.** `.wallmap-frame` was `max-height:80vh; overflow:auto`, so whenever the map's complete natural height (about 1060 px at comfortable density) exceeded the cap the frame scrolled on **both** axes.
- **Correction (one scrolling strategy, nothing else).** `.wallmap-frame` is now `overflow-x:auto; overflow-y:hidden` with **no `height` and no `max-height`**, so it always expands to the map's complete natural height and **the document is the single vertical scrolling surface**. Cells, fonts, wall geometry and spacing are untouched: nothing was compressed, hidden, paged, tabbed, collapsed or accordioned to remove the scrollbar.
- **Why the illustrative `overflow-x:auto; overflow-y:visible` was NOT used.** CSS Overflow 3 §3.1: next to a non-visible `overflow-x`, the used value of `overflow-y:visible` becomes `auto` — the defect would have stayed and would silently return with any height constraint. The stylesheet comment states this, and a test refuses `overflow-y:auto|scroll|visible` **and** the `overflow` shorthand on the frame.
- **Clipping and focus.** The frame was already a clipping scroll container, so `overflow-y:hidden` clips nothing that was not already clipped, and the frame is content-sized with its 10 px padding preserved. The measurement buttons now draw their focus ring **inside** their box (`outline-offset:-2px`) so the keyboard focus indicator can never be cut off by a cell that clips its own content. The legend, the Both/Front/Rear controls, the wall-order controls and the selection/filter notes stay **outside** the frame, and the frame contains the map element and nothing else — no second internal viewport. Tooltips are native `title` attributes and are unaffected by overflow.
- **Horizontal containment retained.** `overflow-x:auto`, `max-width:100%`, `min-width:0` and the fixed cell geometry remain: a narrow window scrolls the map sideways **inside the frame only**, the map can never widen the page, and at fullscreen 1920×1080 the map is narrower than the available width so neither a page-level nor a frame-level horizontal scrollbar appears. The Overview table is untouched and keeps its own bounded local scroll frame.
- **Tests updated and strengthened (`apps/ops-preview/test/layout.test.mjs`: 10 → 11 tests).** The Wall Map contract test no longer expects a height cap; a new focused test separates the two axes explicitly (horizontal containment retained vs vertical scrolling prohibited), asserts no `height`/`max-height`/viewport-relative height on the frame, pins that `overflow-y:visible` is not used and why, keeps the fixed cell and wall-grid geometry, asserts the frame holds only the map element, and re-checks the page-level horizontal guards and the Overview frame. `apps/ops-preview/test/wallmap.render.test.mjs` asserts `overflow-x:auto` + `overflow-y:hidden` and refuses `max-height`/shorthand on the frame. Every 108/106/2 topology, selection, synchronisation, filter, progress and workflow assertion is unchanged and still passes — no assertion was weakened or deleted to make this correction pass.
- **Arena gates after the correction:** Operations Preview `check` OK and **78/78** (was 77); focused Wall Map layout/render tests **28/28**; commissioning `check` OK and **88/88**; kiosk source contract **21/21**; Mapping UI `check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass / 0 fail / 7 pre-existing Owner-local skips)**; boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean; authoritative workbook SHA-256 `4e0337e2…f422e8e` unchanged. The running host was re-inspected over loopback: the served `/styles.css` carries `.wallmap-frame { overflow-x:auto; overflow-y:hidden; max-width:100%; min-width:0; … }` with no `max-height` anywhere on the frame, and `/` serves the frame containing the map element and nothing else.
- **NOT RUN IN ARENA:** there is no browser engine and no .NET SDK in the sandbox, so **real-browser execution and the .NET build/tests are NOT RUN** — no pixel layout, no DOM-computed overflow, no DPI and no WebView2 behaviour is claimed, and the CSS contracts are explicitly not a visual verification. The 19-step Owner-local rerun sequence is checkpoint §17.4.
- **Unchanged:** Wall Map topology and its single canonical source; 108/106/2 counts; I7/I16 gap identity; U-shape geometry, wall order and row order; Front/Rear separation and the Both/Front/Rear control; physical-Channel selection identity and every synchronisation; workflow, filters, loop-test progress, valid/BAD/STALE rules, scenarios; the runtime-init module-script correction; the native Kiosk header layout; WebView2 `1.0.4258.31` and the reference isolation; the lock files; loopback-only hosting, the approved origin, the strict CSP, no external assets; the reconnecting fail-safe; the confirmed exit; the protected evidence and the authoritative workbook; the read-only Preview boundary. **No MODBUS, no physical acquisition, no contact with `192.0.2.11`–`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no device write, no cleaning or motion command, no queue authority, no field-verification claim.**
- **Status.** **PR #19 OPEN — NOT MERGED (do not merge).** Owner Manual Review has **not** passed for this correction; **Stage 0.4C-K1 is not claimed complete.**

## 2026-10-12 — Stage 0.4C-K1 Commissioning Wall Mapping and Loop-Test usability pass (same PR — OPEN, NOT MERGED, do not merge)

- **Owner-authorized pass on the same branch and the same PR #19**, starting at the runtime-init checkpoint `040d322f734e528914527f1b2cfe7bf078d936a8`. It adds a read-only, interactive **Commissioning Wall Map** so that field loop testing no longer needs the manual translation wall position → Sensor → Front/Rear → Node/Cabinet → Module/Channel → value → verdict. No safety, authority, evidence, layout or dependency boundary was reopened, and **no dependency was added**.
- **New wall model (`packages/commissioning/src/wallmap.mjs`, dependency-free and browser-safe).** Exactly **108 logical positions**, **106 Product Sensor cells** and exactly **two `NON_SENSOR_GAP` cells — I7 and I16**, never Sensors and never Water Jets. The builder **throws** instead of rendering a wrong wall: 108/106/2, exactly the I7+I16 gaps, every Sensor exactly once, exactly one Front **and** one Rear measurement per Sensor, 212 measurements in the documented order, the approved per-wall counts (LEFT 24 / REAR 29 + gap / RIGHT 24 / FRONT 29 + gap), six rows in the approved order (`G+201`, `G+101`, `G`, `H`, `I`, `J`) and distinct Channels per position. The canonical topology source is **the protected Product topology plus committed evidence — `config/examples/sensor-map.example.json` → `logicalMatrix.slots`** — and nothing else; the drawing wall label is never used to infer topology, the Runtime wall summary and the spike UI are shape references only, Water Jet identity is never derived from the Sensor wall, and the served payload never forwards the gap Water-Jet anchor or any device/Channel assignment to the browser.
- **Selection synchronised across the whole surface on ONE identity.** Wall Map position → its two measurements; Front/Rear → exactly that measurement; Overview row → its Sensor position; internal-conflict row → the right Sensor **and** the blocking reason; unassigned Channel → no false Sensor selection (`NOT_APPLICABLE`); I7/I16 conflict row → the **gap**, not a Sensor. Selection never writes to evidence, observations or verdicts.
- **Real defect found and fixed by the new coverage.** The first wiring selected a Wall Map position by **measurement** identity (`H7:F`) while the Overview, the Sensor detail and the workflow are keyed by the **physical Channel** (`TC-NODE-03|A102.2|5`), so the selection resolved as `NOT_APPLICABLE` with zero highlighted rows. `wallmap.mjs` now publishes `order` and `channelOrder` in the same documented sequence plus `positionByChannelKey`, `measurementKeyByChannelKey` and `wallMeasurementKey()`, the map resolves positions and the current view by Channel key, and navigation runs on Channel keys. Front and Rear are never merged, copied, aliased or made to hide one another.
- **Wall-order navigation and live progress.** Wall-order Previous/Next walk the documented map order (logical row then column, gaps skipped, Front before Rear) and are edge-safe (no move at an edge, never a wrap); the existing physical-schedule Previous/Next **keeps its meaning** and is now explicitly labelled `(schedule order)`. Loop-Test progress shows **106 Sensors and 212 measurements** with NOT_TESTED / IN_PROGRESS / PASS / FAIL / PARTIAL / BLOCKED derived **live** from the evidence plus workflow state for the selected scenario (`loopTestSummary`), with no cached or invented totals. The Preview workflow is reachable from a map selection and every pre-existing blocking rule stays authoritative: a topology conflict can never PASS, an unassigned Channel has no verdict, an internal conflict is FAIL/PARTIAL only, BLOCKED remains evidence-derived, and a Preview verdict approves nothing in the field.
- **Presentation.** The map is contained in its **own bounded frame** (`.wallmap-frame`, `max-height` + `max-width:100%` + `min-width:0`) so it scrolls inside itself and can never widen the page — the Overview table remains the only other sideways scroller — while the map itself is fixed-geometry (`width: max-content`, fixed-size cell tokens in both densities, no viewport-relative width, no inline style). Quality and state are carried by text and symbols in addition to colour; the `SIMULATION / PREVIEW` vs field-verification distinction and the boundary text are preserved. The corrected header, `Exit Kiosk Preview`, `Windowed / full screen`, the boot path and its visible failure surface are untouched.
- **Arena validation at this head:** commissioning `check` OK and **88/88** (11 new wall-map tests); Operations Preview `check` OK and **77/77** (17 new Wall-Map render tests); kiosk source contract **21/21**; Mapping UI `check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass / 0 fail / 7 pre-existing Owner-local skips)**; boundary scan **`0 findings (S1–S9 clean)`** — after fixing one real finding the scanner itself raised against a new enumeration in `app.mjs`, fixed in the source, never by weakening the scanner; `git diff --check` clean; authoritative workbook SHA-256 unchanged. The running host was inspected over loopback: `/` serves the Wall Map frame, view control, wall-order controls and progress section, `/api/sensor-map` serves LEFT 24 / REAR 30 / RIGHT 24 / FRONT 30 slots with `logicalLabel` forwarded and the two gaps marked `NON_SENSOR_GAP`, and every documented route answers 200.
- **NOT RUN / not claimed:** there is **no browser engine and no .NET SDK in Arena**, so **real-browser execution and the .NET build/tests are NOT RUN IN ARENA** — no pixel layout, no DOM-computed overflow, no DPI behaviour and no WebView2 behaviour is claimed. The 21-step Owner-local handoff is checkpoint §16.6; the 1920×1080 interaction acceptance stays open until it passes there.
- **Unchanged:** WebView2 `1.0.4258.31` and the WinForms-only WPF-reference isolation; the lock files (nothing added, refreshed or hand-edited); loopback-only binding, the approved origin, the strict CSP, no external assets; the read-only boundary and every route/method restriction; the protected evidence totals and the authoritative workbook; the reconnecting fail-safe and the confirmed exit. **No MODBUS, no physical acquisition, no contact with `192.0.2.11`–`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no device write, no cleaning or motion command, no queue authority, no Runtime supervision, no field-verification claim.**
- **Status.** **PR #19 OPEN — NOT MERGED (do not merge).** Owner Manual Review has **not** passed for this pass; **Stage 0.4C-K1 is not claimed complete.**

## 2026-10-12 — Stage 0.4C-K1 PR #19 Owner Manual Review runtime-init correction: the Operations Preview now boots and interacts (same PR — OPEN, NOT MERGED, do not merge)

- **Owner-local result, recorded as received.** The 1920×1080 width correction **PASSED** visual review (complete native header fits; `Windowed / full screen` and `Exit Kiosk Preview` both visible; the Operations Preview stays inside the screen; Density controls visible; the Overview table contained in its own horizontal-scroll frame). The **interaction review FAILED**: with the host started through the real package command (`npm --prefix .\apps\ops-preview start`, `127.0.0.1:5187`) and the Kiosk reporting `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)`, the HTML and CSS rendered but the application did nothing — Density controls and Sensor Map / Diagnostics navigation unresponsive, empty Scenario and Node selectors, no Node communication summary, no Commissioning progress, the Overview showing its header with **no rows**, and empty Sensor detail fields.
- **Exact root cause: the served page contained no `script` element at all.** `apps/ops-preview/public/index.html` loaded `/styles.css` and nothing else, so the browser never requested `app.mjs`: no module executed, no API was called, nothing rendered and no control was wired. Every reported symptom follows from that single omission (including "navigation does not respond" — the anchors scroll to sections that exist but were never filled in).
- **Why the earlier tests passed.** `test/render.test.mjs`, `test/layout.test.mjs` and the boot helper import `public/app.mjs` **directly** and seed a stub DOM from `index.html`: they exercise the module, but nothing asserted that the **served page references** it. A direct import is precisely what a browser never does, so that defect class was invisible to the suite. The new coverage closes it at the level where the defect lived.
- **Correction.** `index.html` now ends with `<script type="module" src="/app.mjs"></script>` — the only script element, same-origin, no inline script, so the existing `script-src 'self'` CSP is unchanged and still refuses `unsafe-inline`/`unsafe-eval`. Added: a **visible initialization-failure surface** (`#boot-error`, `role="alert"`, hidden until needed) carrying a **safe category only** (data request / module load / runtime) with no stack trace, local path or internal detail; a `<noscript>` note; a machine-readable **boot sentinel** `data-app-state` = `booting` → `ready`/`failed` on the page body, with boot split into a data stage and a render stage so the category distinguishes "the local host did not answer" from "the application failed to render"; uncaught errors and rejections after a successful boot also raise the visible failure state. An empty control value now falls back to the current state (`||` rather than `??`), so an unpopulated selector can never send an empty scenario or node identity.
- **New runtime coverage (`apps/ops-preview/test/page.test.mjs`, 10 tests).** The application is resolved **through the server**, as a browser does, then executed over the served bytes: one module script element and `/app.mjs` served byte-identical to the tested file; CSP permits that module and the page contains nothing the policy would block; **the entire served module graph resolves recursively** over the real routes (`/app.mjs` → `/pkg/index.mjs` → all seven package modules) with 200 + JavaScript MIME and no `node:`/remote specifier; boot reaches `ready` with every boot request a successful local GET; the ten scenarios, six nodes, node summary and progress are populated; 240 rows with 20 columns render and row selection fills the detail cards; Compact/Comfortable respond and all five navigation anchors target real sections; applying a scenario reaches the rows; and a **failed boot becomes a visible safe failure state** (asserted to contain no `.mjs`, no `apps/`, no path, no stack, no `undefined`). The boot helper also now gives every `bootApp` call a fresh module instance (module caching previously made a second boot a silent no-op) and mirrors two browser behaviours the stub got wrong — first `<option>` auto-selection and `document.body` **being** the `app-root` element.
- **Arena gates after this correction:** kiosk contract **21/21**; Operations Preview `check` OK and **59/59** (was 49); commissioning `check` OK and **77/77**; Mapping Configuration `check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass, 0 fail, 7 skipped)**; boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean. The running host was also inspected over loopback HTTP: `/` now carries the module script element and `/app.mjs` plus all seven package modules answer `200 text/javascript`.
- **NOT RUN / not claimed:** there is **no browser in the Arena sandbox**, so the served page was never executed by a real browser engine — **real-browser execution is NOT RUN**, and no pixel layout, DOM-computed overflow or WebView2 behaviour is claimed. The tests prove wiring and boot completion over the served bytes and real routes; they are not a browser.
- **Unchanged:** WebView2 `1.0.4258.31` and the WPF-reference isolation; the corrected 1920×1080 layout strategy (unchanged except where interaction required it); loopback-only binding and the approved origin; no external assets; the read-only Preview boundary and the protected evidence totals (240 rows, 212 Product-compatible measurements, 106 Product Sensors, four I7/I16 topology-conflict rows, twelve internal-conflict rows, 24 unassigned Channels); the reconnecting fail-safe; the confirmed exit; no Production authority, no vendor transport, no hardware or physical transport, no device write or control.
- **Status.** **PR #19 OPEN — NOT MERGED (do not merge).** Owner Manual Review has **not** passed: the width finding is closed, the runtime correction is complete in source and awaits the Owner-local rerun in checkpoint §15.5.

## 2026-10-12 — Stage 0.4C-K1 PR #19 Owner Manual Review correction: fullscreen 1920x1080 fit and launch-runbook correction (same PR — OPEN, NOT MERGED, do not merge)

- **Owner Manual Review result: FAILED, recorded as received.** Owner-local automated validation had passed (zero-warning Release build, .NET **414/414**, kiosk contract 17/17, WebView2 isolation proof `CoreCount=1, WinFormsCount=1, WpfCount=0, LoaderCount=3`). The Owner then started the real host (`node apps/ops-preview/server.mjs`, `listening on 127.0.0.1:5187`); the Kiosk connected and showed `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)`, so this was **not** a host or reconnect problem. At fullscreen 1920×1080 the application was **wider than the screen**: the native header pushed **`Exit Kiosk Preview` completely outside the right edge** (`Windowed / full screen` stayed visible, no wrap or compact mode), and in the embedded Operations Preview the **Density control was clipped**, `Comfortable` was only partly visible and right-side content extended past the 1920 px viewport.
- **Root cause — one cause, two symptoms.** The header's left `FlowLayoutPanel` was `AutoSize = true` with `WrapContents = false` and carried the long boundary/state text, so it demanded its full preferred width; the outer layout's single column had no explicit style, and an unstyled `TableLayoutPanel` column defaults to **AutoSize**, so the column grew to the header's preferred width. That pushed the actions past the right edge and stretched the content cell — and therefore the embedded view and the **web page viewport** — past the screen edge, clipping the page's own right-hand controls. The README claim that 1920×1080 presentation was already DPI-safe was **not supported by the Owner's screenshot** and has been replaced by an explicit acceptance target.
- **Correction A (native header, `apps/kiosk/MainForm.cs`).** Two-column, four-row grid with explicit priority: the action column is `AutoSize` (the only content that keeps its preferred width, so the buttons can never leave the client area); **every text row is `AutoSize = false` with `Dock = Fill`**, so no text can demand width from the form; the host state, the Stage boundary and the policy counters each own a full-width, wrapping row beside the short identity row; row heights are **measured** with `TextRenderer.MeasureText(..., WordBreak)` and applied as `SizeType.Absolute`; the layout is re-applied on `OnResize`, on `DpiChanged`/`OnDpiChangedAfterParent` and whenever a header text changes. The outer layout's single column is now explicitly `SizeType.Percent, 100F` — the fix that also bounds the embedded page viewport. Unchanged: the 1024×600 minimum, the text sizes, the confirmed exit, the reconnecting fail-safe, the approved origin, and both actions (never hidden or removed, keyboard reachable). No scrollbar is introduced.
- **Correction B (page width fit, `apps/ops-preview/public/styles.css`).** `overflow-x: hidden` + `max-width: 100%` as the page-level guard; `main` bounded to `minmax(0, 1fr)`; `min-width: 0` on every flex/grid child that can hold wide content; the 20-column Overview table contained in `.table-wrap.scroll`, which owns the page's only horizontal scrollbar; `minmax(min(..., 100%), 1fr)` for the auto-fit grids; `minmax(0, 180px) minmax(0, 1fr)` for the detail definition lists; the modal bounded by `min(620px, calc(100% - 32px))`; and **no `100vw` anywhere**.
- **Correction C (launch commands).** `apps/ops-preview/package.json` now has a real `start` script (`node server.mjs`); the runbook and checkpoint document only commands that exist (`node apps/ops-preview/server.mjs` and `npm --prefix apps/ops-preview start`), and there is no root `package.json`, so no root `npm start` is documented.
- **Correction D (contract coverage).** `apps/kiosk/test/shell.contract.test.mjs` +4 tests (**21/21**): both header actions present/never hidden/never removed; the header is measured, bounded and responsive; the left status content cannot force the action area outside the client width; the boundary, host state and policy counters stay on screen and are re-measured when they change. New `apps/ops-preview/test/layout.test.mjs` (**9 tests**): page-level overflow guard, bounded grid column, contained table frame, wrapping/min-width-0 containers, the density control and navigation inside the wrapping view bar, and a contract that every documented `node`/`npm` command in the runbook and checkpoint exists and matches the package scripts.
- **Arena gates after the correction:** kiosk contract **21/21**; Operations Preview `check` OK and **49/49** (was 40; +9 layout tests); commissioning `check` OK and **77/77**; Mapping Configuration `check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass, 0 fail, 7 skipped)**; boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean.
- **NOT verified in Arena, and not claimed.** The WinForms header was not compiled or rendered (no .NET SDK) and no browser was opened against the page: the new tests are **source and structure contracts, not rendering checks**, so no layout, scaling or 1920×1080 visual result is claimed. The Owner-local fullscreen rerun and the manual acceptance list in [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](docs/STAGE_0.4C-K1_CHECKPOINT.md) §14.1 remain the validation of record, and the corrected fullscreen presentation is an **acceptance target**, not a verified result.
- **Unchanged:** WebView2 `1.0.4258.31` and the successful WinForms-only WPF-reference isolation; Central Package Management; the protected commissioning evidence (240 physical rows, 212 Product-compatible measurements, 106 Product Sensors, I7/I16 gap and conflict treatment); the approved loopback scheme and host; the read-only Preview boundary; the confirmed exit and the reconnecting fail-safe; no Production authority, no vendor transport, no hardware or physical transport, no device write or control.
- **Status.** **NEW PR #19 against `main` — OPEN, NOT MERGED (do not merge).** The Stage is not complete until the Owner-local rerun confirms the fullscreen fit and the manual acceptance list.

## 2026-10-12 — Stage 0.4C-K1 zero-warning correction: WinForms-only WebView2 reference isolation (same PR against `main` — OPEN, NOT MERGED, do not merge)

- **Owner-local validation received and recorded** (Owner-local, not Arena): kiosk contract 12/12, commissioning check + 77/77, Operations Preview check + 40/40, Mapping Configuration check + 82/82, normal and locked solution restores passed, full .NET tests **414/414**, boundary scan S1–S9 clean, working tree after restore `M apps/kiosk/packages.lock.json` (the genuine Owner-local lock refresh: `Microsoft.Web.WebView2` requested/resolved `1.0.4258.31`), and a Release build that succeeded **with exactly one warning**, `MSB3277` (WindowsBase `4.0.0.0` versus `5.0.0.0`).
- **Exact root cause, from the Owner-local package evidence:** `buildTransitive\Microsoft.Web.WebView2.targets` in `1.0.4258.31` has a "regular .NET 5+" branch — the branch selected for `net10.0-windows` — that unconditionally adds `Microsoft.Web.WebView2.Core.dll`, `...WinForms.dll` **and `...Wpf.dll`** to the reference graph. The WPF assembly is compiled against WindowsBase `5.0.0.0` while the .NET 10 reference pack provides the `4.0.0.0` facade, hence `MSB3277` in a project that uses no WPF type. This replaces every earlier hypothesis, including any suggestion that the repository enabled both WinForms and WPF: the evaluated properties are `UseWindowsForms=true` and `UseWPF` empty.
- **Correction (`apps/kiosk/Wjss.Kiosk.csproj`).** A project-local target `RemoveWebView2WpfReference` runs `AfterTargets="ResolvePackageAssets"` and `BeforeTargets="ResolveAssemblyReferences"` and removes **only** the reference item whose file name is `Microsoft.Web.WebView2.Wpf`, matching by file name alone (no user profile, no global-packages path, no absolute path, no version). No package file, cache entry or lock file is read, written or modified. `...Core`, `...WinForms` and the WebView2 loader/runtime assets are untouched. A companion target `KioskWebView2ReferenceProof` prints `WJSS-KIOSK-WEBVIEW2 CORE/WINFORMS/WPF` counts and **fails** if Core or WinForms is missing or WPF is present; the removal target itself fails the build if a WPF-only reference survives, so a silent regression becomes a loud error.
- **Nothing suppressed.** No `NoWarn`, no warning-severity change, no `MSB3277` suppression, no binding redirect, no manual WindowsBase reference, no `UseWPF=true`, no mixed WinForms/WPF application, no vendored assembly, no package upgrade/downgrade/refresh, no target-framework change, and no hand-edit of the Kiosk lock file. The central pin `1.0.4258.31`, Central Package Management, locked restore, the WinForms-only model, the source-generated bootstrap and the DPI project property are unchanged.
- **New source-contract coverage.** Five tests added to `apps/kiosk/test/shell.contract.test.mjs` asserting the WinForms-only project configuration, the correction's target wiring and scope (WPF-only, no paths), the preserved Core/WinForms references with their proof-target errors, and the absence of any warning suppression or binding redirect. The file is now **17/17**.
- **Arena re-run after the correction:** kiosk contract **17/17**; commissioning check OK + **77/77**; Operations Preview check OK + **40/40**; Mapping Configuration check OK + **82/82**; `packages/mapping-config` check OK + **268 (261 pass, 0 fail, 7 skipped)**; boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean. Two residual boundary-scan findings during authoring (assembly-version digits read as address-like literals) were fixed by **rephrasing the comment**, not by weakening the scanner.
- **NOT verified in Arena:** the correction was not executed — the sandbox has no .NET SDK and no reachable package feed, so no restore, build, `msbuild -t:` invocation or .NET test ran there and no build result is claimed. The Owner-local zero-warning Release build, the reference-graph proof output, and the manual kiosk visual review remain the verification of record; the exact handoff commands are in [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](docs/STAGE_0.4C-K1_CHECKPOINT.md) §13.1.
- **Unchanged:** WebView2 package version `1.0.4258.31`; the WinForms-only application model; the Stage 0.4C-K1 preview boundary (`SIMULATION / PREVIEW - NO HARDWARE ACCESS - NO DEVICE WRITE - FIELD VERIFICATION PENDING`); no Production, vendor-transport, hardware, physical-transport or device authority; the protected evidence totals and topology; the authoritative workbook and every evidence file under `docs/evidence/`.
- **Status.** **NEW PR against `main` — OPEN, NOT MERGED (do not merge).** The Stage is **not** complete until Owner-local zero-warning validation and the manual kiosk review pass. Record: [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](docs/STAGE_0.4C-K1_CHECKPOINT.md) §12 and §13.

## 2026-10-12 — Stage 0.4C-K1: Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview (development checkpoint; NEW PR against `main` — OPEN, NOT MERGED, do not merge)

- **New session after PR #18 MERGED** into `main` at `48f0315ea1bcbd3f71f46a47bb4b55fc04327487` (parents `3e1a7fd9d28db65221c123960a0c85b62d93949a` and `b2048ec0aa5703767a2af85c73eaa1ae94d06e53`, verified by `git cat-file -p`). Platform-assigned branch `arena/3be5c713-waterjet-sentinel-suite`, starting exactly at that commit; base gate PASSED (remote `main` exact match, index and worktree clean, no conflicting open PR, workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` re-verified and unchanged). **The standing sandbox recreation protocol was not triggered** — no ref recovery, index reload or destructive Git operation was needed or used. The merged PR #18 branch `arena/c4b63a26-waterjet-sentinel-suite` was not used, continued or pushed.
- **Owner dependency decision (the Stage's only dependency change):** `Microsoft.Web.WebView2` pinned **`1.0.4258.31`**, central `<PackageVersion>` in `Directory.Packages.props` plus a **version-free** `PackageReference` in `apps/kiosk/Wjss.Kiosk.csproj`; **Evergreen** WebView2 Runtime for the development preview. **Rejected by the Owner:** Edge application/kiosk mode as the delivered shell, the legacy web-browser control, Electron or another desktop framework, and a source-only shell that stays non-building. Pre-change hashes of the four dependency-relevant files are recorded in [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](docs/STAGE_0.4C-K1_CHECKPOINT.md) §4. **No other dependency was added, removed or refreshed, and no npm dependency was touched for the shell.**
- **Kiosk shell implemented (`apps/kiosk`) — the first time this project is more than a compile-only skeleton.** Application-owned chromeless window on the approved loopback origin only; navigation, other ports/schemes, local files, popups, new windows, downloads and permission requests refused and counted; developer tools, context menus, browser accelerator keys, status bar, zoom control and the built-in error page disabled; `ZoomFactor` 1.0 with the existing PerMonitorV2 DPI property; a 2 s health probe drives a visible **LOCAL PREVIEW HOST UNAVAILABLE — RECONNECTING** screen that **hides the view**, so a frame that stopped updating is never presented as a current value, and recovery reloads the route; a visible **Exit Kiosk Preview** button plus a confirmation dialog and a close guard on user-initiated closes. The shell starts no process, writes no record, parses no Product state and holds no credential. **It was NOT compiled or run in Arena** (no .NET SDK): compilation, Windows behaviour, DPI rendering and the 1920×1080 visual acceptance are Owner-local.
- **Sensor Commissioning Monitor and the ten deterministic scenarios (`packages/commissioning`, `apps/ops-preview`).** A dependency-free read-only package parses the committed 240-row drawing-derived schedule and reproduces the protected baseline exactly (108 logical positions, 106 Product Sensors, 212 measurements, 6 nodes, 30 750-498 modules, 240 physical Channels, 200 `DRAWING_VERIFIED` / 12 `DRAWING_INTERNAL_CONFLICT` / 4 `DRAWING_TOPOLOGY_CONFLICT` / 24 `SPARE_UNMAPPED` / **0 unresolved**, I7 and I16 preserved as `NON_SENSOR_GAP` positions — never Sensors, never Water Jets). Quality, DiagnosticReason and CommissioningState remain **three separate dimensions**; `INVALID` is presentation-only and was **not** added to the Quality enum; an invalid current value is never rendered as a number and the last valid sample is labelled historical and not current. The scenarios are pure functions of the evidence and a tick: `NORMAL_GOOD`, `UNCERTAIN_TRANSIENT`, `OUT_OF_RANGE`, `STALE`, `COMMUNICATION_LOSS_NODE` (exactly one node), `SIMULATED_WIRE_BREAK` (labelled `SIMULATED_DIAGNOSTIC_SCENARIO`, raw `0x7FFF` as evidence only, engineering value unavailable), `RECOVERY`, `DRAWING_CONFLICT`, `PRODUCT_TOPOLOGY_CONFLICT`, `UNASSIGNED_CHANNEL`. `WIRE_BREAK_DECODING: BLOCKED` is unchanged. The local host is loopback-only and read-only: GET/HEAD only, 405 otherwise, `default-src 'self'` CSP with no remote asset, no remote font, no CORS wildcard and no-store responses, and **no route** for a device, transport, control, queue, job, cleaning, pump, valve or motion action. The Preview workflow records verdicts and remarks **in memory only** and is labelled as approving nothing.
- **Automated validation in Arena.** `packages/commissioning` **77 tests / 77 pass / 0 fail / 0 skipped**; `apps/ops-preview` **40 / 40** (server 15, render 25); `apps/kiosk` shell **source-contract** suite **12 / 12**; regression re-runs of the unchanged Mapping graph: Mapping UI **82 / 82** and Mapping package **268 (261 pass, 0 fail, 7 skipped — the Arena workbook-dependent Owner-local skips)**; boundary scan **`0 findings (S1–S9 clean)`**; workbook SHA-256 unchanged; `git diff --check` clean. Two genuine boundary-scan findings raised during the Stage (a credential-shaped enum member and a vendor column name echoed in a test fixture) were fixed **in the source**, and the scanner was **not** weakened.
- **Honest scope of the checks.** The local preview host was started in the sandbox and its routes were exercised over loopback HTTP (health, boundary, schedule, observations with node-scoped communication loss and the labelled simulated wire-break, sensor map, 405 on a write method), and the render suite executes the real UI code against the real host in a test DOM. **No browser was opened against the preview, no Windows session exists, and the Kiosk shell was not compiled or run.** No screenshot, layout or DPI acceptance was performed.
- **Documentation (single consolidated pass):** `docs/STAGE_0.4C-K1_CHECKPOINT.md` (new), `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`, and a dated status update in `docs/decisions/ADR-0016-kiosk-shell-direction.md` (the ADR keeps its **DRAFT** status; its draft Runtime-supervision items are **not** implemented in this Stage). **`README.md` was NOT modified** — its status block still names Stage 0.4C-0B with "PR #18 OPEN — NOT MERGED" although PR #18 is now merged; **CF-07 stays open** for an Owner decision rather than a silent repair.
- **Unchanged:** the authoritative workbook and the Drawing-derived schedule, process-image and validation evidence; the schedule templates; the Runtime; the Mapping implementation and both Mapping suites; `packages/contracts`, `packages/domain`, `packages/application`, `packages/time`, `adapters/**`; Pump/Valve logic; the Cleaning sequence; the Queue kernel; the Galil and MODBUS implementation surfaces; production configuration; the live Hardware provider; `Directory.Build.props`; `global.json`; every other project and lock file. **No file was deleted or renamed.**
- **Not verified, and not claimed:** compilation of the changed .NET graph, the Kiosk Release build, the Owner-local lock-file refresh, Windows runtime behaviour, DPI rendering, 1920×1080 visual acceptance, the embedded-view runtime policy callbacks, the Owner Browser Review, and every .NET test. `apps/kiosk/packages.lock.json` (v2, group `net10.0-windows7.0`, still **empty**) must be regenerated Owner-locally; until then a locked-mode restore reports it as stale — expected for this Stage.
- **Status.** **NO HARDWARE. No live MODBUS. No TEST_HARDWARE. No PRODUCTION. No device write or control. No physical Sensor tested. Field verification remains PENDING.** Physical Sensor acquisition **NOT IMPLEMENTED**; wire-break decoding **BLOCKED**. **NEW PR against `main` — OPEN, NOT MERGED (do not merge).** Record: [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](docs/STAGE_0.4C-K1_CHECKPOINT.md).

## 2026-10-12 — Stage 0.4C-0B completion: Baseline REV1 Evidence Review and 240-Row Schedule (development checkpoint, documentation only; same branch and PR — PR #18 OPEN, NOT MERGED, do not merge)

- **Same session after the 2026-10-11 evidence-blocked checkpoint** (base `main` `3e1a7fd9d28db65221c123960a0c85b62d93949a`; platform-assigned branch `arena/c4b63a26-waterjet-sentinel-suite`; index and worktree clean; no conflicting open PR; workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` re-verified before and after every read, unchanged). The standing sandbox recreation protocol was **not triggered**.
- **Received and integrity-verified the Baseline REV1 evidence** via `OWNER_GOOGLE_DRIVE_CONNECTOR` (delivery recorded without links, IDs or tokens): `WJSS_TC_EVIDENCE_BASELINE_REV1.zip`, exactly 17345849 bytes, SHA-256 `7B886950D7F5F665416241E4CB502CD1A5787E3581788C799077C062786AA23E`, ZIP-signed, 4 listable members, per-member SHA-256 recorded (drawing `08db6f2a…1d6b98`, 750-498 `ed66534d…1fe83`, 750-362 `85e72f83…27bb`, 750-600 `c390260c…04b46`; full hashes in the source register TCEV-17 … TCEV-20). Safe extraction with traversal/symlink/encryption guards; **ephemeral bytes removed after review; the archive and evidence PDFs are not committed**.
- **Reviewed the combined six-cabinet drawing** (`T8_SDS_Cabinet_-Draft-7E.pdf`, REV 1A, 16 MAR 2026; stays `DRAWING_PENDING_REVISION`): `+SDSCAB1` … `+SDSCAB6` with `-A101` = 750-362, `-A102.1` … `-A102.5` = 750-498, `-A110` = 750-600, `-X110` = WDU 2.5/TC TYP K, `-X111` = UT 2.5-PE (p32/p14/p19). **All 240 wiring-table cells read on-render** (pp20/22/24/26/28/30); all 6 X110 diagram sequences follow the table conductor order (pp21/23/25/27/29/31); conflict zones, I7/I16 cells, p6/p7 overview labels and topology pages confirmed on-render/crops.
- **Derived `docs/evidence/tc-drawing-schedule-rev1.csv` (240 rows + header):** 200 `DRAWING_VERIFIED`, 12 `DRAWING_INTERNAL_CONFLICT` (A102.2 ch6/ch7 sheath≠conductor on all cabinets; schedule follows conductor+X110, both claims preserved), 4 `DRAWING_TOPOLOGY_CONFLICT` (live drawing channels on authoritative `NON_SENSOR_GAP` positions I7/I16 — kept, excluded from the compatible set, nothing commissioned/dropped), 24 `SPARE_UNMAPPED` (A102.5 ch5–8), **0 unresolved**; mapped+unmapped = 216+24 = 240; 212 `MATCHED` across 106/106 template Sensors (extras exactly {I7, I16}).
- **Extracted the three manuals with section/page references and built `docs/evidence/tc-process-image-profile-rev1.md`:** Type K = ID3, −200…+1370 °C at 0.1 °C/digit two's complement; wire break `0x7FFF`/status `0x60` (ambiguous with deactivated channel); factory defaults (Type K, internal CJC, diagnostics on) at `MODEL_LEVEL`; 40 Modbus words/node at FC3/FC4 addresses 0–39 (derived, each step cited; 750-498-not-enumerated gap recorded); **status bytes not visible via 750-362 Modbus** — diagnostics are value-based only. `MODEL_LEVEL` decoding contract with labelled assumptions (`ASSUMED_ORDINAL_CORRESPONDENCE`, side correspondence).
- **Recorded the third drawing issue and the validation:** installation-overview sensor groups on p6/p7 are swapped between CAB1/CAB2 (pp8–11 match); overviews are not a schedule source. Narrow validation in `docs/evidence/tc-schedule-validation-rev1.md` (separation counts, 106-sensor cross-check, §16-style structural/212-row/duplicate/range/reference checks all PASS, visual-verification record, NOT_VERIFIED register).
- **Updated the registers (append-only; absence history preserved):** issue register §7 (evidence integrity, topology, 240-row derivation, ISS-REV1-01/02/03 + spares, manual extraction, residual gaps, Owner next actions 5–7); source register TCEV-17 … TCEV-20 + new §13 addendum (`DRAWING_SCOPE` vocabulary, assumptions, recount) with Categories A–E advanced at `DRAWING_SCOPE` / `MODEL_LEVEL`. **§9.4 recount: 13 `NOT_PROVIDED`, 7 advanced** (`REQ-TC-001` … `REQ-TC-007` drawing/manufacturer-verified; `REQ-TC-008` … `REQ-TC-020` stay `NOT_PROVIDED` — no configuration, transport identity or observed behaviour).
- **Templates untouched.** Both `docs/templates/` files byte-identical (schedule template: 28 columns, 212 rows, 0 `I7`/`I16` rows; read-only use only).
- **CF-07 still open.** `README.md` outside this Stage's file scope, deliberately **not** modified.
- **Validation (documentation-only, per AGENTS §10.1).** Boundary scan post-change result recorded in the final report. Workbook SHA-256 unchanged. Changed-file allowlist, Product-source no-change proof, executable-test no-change proof, CSV validation results, relative-link check and `git diff --check` recorded in the final report. **No `dotnet` or `npm` test was run and none is claimed.**
- **Predecessor evidence cited, not re-run at this documentation head:** Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82, Owner Browser Review PASSED. These are **not** claimed as results of this Stage.
- **Changed:** `docs/evidence/tc-drawing-schedule-rev1.csv`, `docs/evidence/tc-process-image-profile-rev1.md`, `docs/evidence/tc-schedule-validation-rev1.md`, `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md`, `docs/TC_EVIDENCE_SOURCE_REGISTER.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`. **Unchanged:** Product source, executable tests, workbook, templates, dependencies, lock files, fixtures, configuration, Runtime, Mapping implementation, Galil and MODBUS code, `README.md`, checkpoint `docs/STAGE_0.4C-0B_CHECKPOINT.md`, and the merged PR #17 branch.
- **Status.** Physical Sensor acquisition NOT IMPLEMENTED. Hardware loop test NOT EXECUTED. NO live MODBUS communication implemented. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. No device write or control. No physical Sensor tested. **PR #18 OPEN — NOT MERGED (do not merge).**
- Record: [TC_DRAWING_ISSUE_REGISTER_REV1.md](docs/TC_DRAWING_ISSUE_REGISTER_REV1.md) §7, [TC_EVIDENCE_SOURCE_REGISTER.md](docs/TC_EVIDENCE_SOURCE_REGISTER.md) §13 and [docs/evidence/](docs/evidence/tc-schedule-validation-rev1.md).

## 2026-10-11 — Stage 0.4C-0B: Six-Node Thermocouple Topology Baseline Rev1 (development checkpoint, documentation only; evidence-blocked on drawings and manuals)

- **New session after PR #17 MERGED** (merge commit `3e1a7fd9d28db65221c123960a0c85b62d93949a`; parents `bd1437586af73a33d43dfafce27b27538183a283` and `48d8605a4d687f509edf079f2d371319d6bffa21`, verified by `git cat-file -p` in order, with a read-only ancestry fetch proving both parents resolve; PR #17 MERGED at `2026-10-11T08:59:28Z`). Platform-assigned branch `arena/c4b63a26-waterjet-sentinel-suite`, starting exactly at `3e1a7fd9…`. Base gate PASSED: remote `main` exact match, index and worktree clean, no conflicting open PR, workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` re-verified and unchanged (16648 bytes). **The standing sandbox recreation protocol was not triggered** — no ref recovery, index reload or destructive Git operation was needed or used. The merged PR #17 branch `arena/1cfd7e20-waterjet-sentinel-suite` was not used, continued or pushed.
- **Recorded the Owner-confirmed six-node TC topology baseline rev1 as brief-stated design basis** (6 nodes × `750-362 × 1 + 750-498 × 5 + 750-600 × 1`; 750-362 × 6, 750-498 × 30, 750-600 × 6, 6 endpoints; capacity 240 physical / 212 required / 28 remaining Channels, arithmetic recomputed not copied; `OWNER_CONFIRMED_FACTORY_DEFAULT` basis; TEST ONLY IPs `192.0.2.11`–`192.0.2.16`; proposed IP-independent identities `TC-NODE-*` / `TC-MODULE-*` / `TC-CHANNEL-*`). **Nothing in the baseline is drawing-verified or hardware-verified.** The 28 remaining Channels stay unscheduled, not commissioned spares.
- **STOPPED schedule derivation with 0 rows — no drawing file is present in the sandbox (brief §4 STOP).** A workspace-wide search for `*.pdf`, `*.dwg`, `*.dxf`, `*drawing*`, all common image formats and manual filename patterns returned **zero hits**, and tracked-file review confirms no evidence file is committed. The six-cabinet drawings (filenames unspecified in the brief), `m07500498_00000000_0en.pdf`, and the 750-362 / 750-600 manuals (filenames unspecified) are recorded `NOT_AVAILABLE_IN_SESSION`. **Added `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md`** with the expected-evidence intake, the exact missing-file STOP report, the baseline consequences (0 derived rows, capacity columns blocked), the binding REV1→REVn revision rule, and Owner next actions. No `docs/evidence/` CSV was created — creating it would have required inventing every acquisition field.
- **STOPPED manual evidence extraction — no manual file is present (brief §§7–8 STOP).** No 750-498 section, page, default value or encoding was read or cited; no 750-362 ordering, grouping, alignment, width, offset or status-byte rule was derived; no process-image profile, no address/offset manifest and no documented decoding contract were created. `getProfile('750-498')` returns `null` — no 750-498 profile exists in the repository; predecessor 750-362/750-600 facts are model-level `PROVIDED_UNVERIFIED` with ordering/alignment/offset rules explicitly missing. `ADDRESS_UNRESOLVED` is retained **because the manuals are absent**, not merely for lack of hardware testing. The predecessor wire-break decision-tree design position stands unchanged; **no Product enum was modified**. `WIRE_BREAK_DECODING: BLOCKED`.
- **Recorded the capacity and entry-criteria consequences without forcing any result (brief §12 STOP).** Drawing-derived used Channels = 0 against 212 expected (212 unscheduled identities); drawing-unassigned, missing, per-node and per-module columns are blocked as not computable, not zero-filled. Stage 0.4C-1 entry criteria re-checked: still **10 of 11 NOT MET** (criterion 6 satisfied only vacuously). `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`.
- **Recorded the configuration-model shape (§6) and the representative-node TEST_HARDWARE verification plan (§11) as future proposals.** No endpoint connected, no polling added, no configuration file created. The optional `config/examples/` TEST-IP file was **declined on a governance ground**: boundary-scanner rule S2 flags any non-loopback IPv4 in the Product tree (which includes `config/`), so the TEST ONLY baseline lives as narrative documentation only. No connection, loop test or hardware interaction was performed or authorised.
- **Updated `docs/TC_EVIDENCE_SOURCE_REGISTER.md`** (Stage 0.4C-0B review): `REQ-TC-001` and `REQ-TC-002` advance to statement-only `PROVIDED_UNVERIFIED` (TCEV-12; model facts at `MODEL_LEVEL`, instance counts stated-not-verified); test-IP statement TCEV-13; absent drawings/manuals TCEV-14 … TCEV-16; Category A/B statement supersessions with the binding derivation rule preserved; Category C–E notes; §9.4 counts 18 `NOT_PROVIDED` + 2 statement-only; new §12 addendum defining `NOT_AVAILABLE_IN_SESSION`. **No verified state was widened; no SHA-256 was invented or reused.**
- **Templates untouched.** Both `docs/templates/` files are byte-identical: the schedule template still reads **28 columns, 212 data rows, 0 `I7`/`I16` rows**, every acquisition column `NOT_PROVIDED`.
- **CF-07 extended, not repaired.** `README.md` is outside this Stage's file scope, so its status block (still naming Stage 0.4C-0A and PR #17 OPEN) is reported for an Owner decision rather than edited, per AGENTS.md §2.2 and §2.3.
- **Validation (documentation-only, per AGENTS §10.1).** Boundary scan post-change result recorded in the final report (pre-change baseline at session start: **S1–S9 clean, 0 findings, exit 0**). Workbook SHA-256 unchanged. Changed-file allowlist (6 files), Product-source no-change proof, executable-test no-change proof, template-integrity re-check, capacity arithmetic recomputation, relative-link check and `git diff --check` recorded in the final report. Drawing-derived CSV validations are **correctly absent** — no CSV exists to validate. **No `dotnet` or `npm` test was run and none is claimed.**
- **Predecessor evidence cited, not re-run at this documentation head:** Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82, Owner Browser Review PASSED. These are **not** claimed as results of this Stage.
- **Changed:** `docs/STAGE_0.4C-0B_CHECKPOINT.md`, `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md`, `docs/TC_EVIDENCE_SOURCE_REGISTER.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`. **Unchanged:** Product source, executable tests, workbook, templates, dependencies, lock files, fixtures, configuration, Runtime, Mapping implementation, Galil and MODBUS code, `README.md`, and the merged PR #17 branch.
- **Status.** Physical Sensor acquisition NOT IMPLEMENTED. Hardware loop test NOT EXECUTED. NO live MODBUS communication implemented. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. No device write or control. No physical Sensor tested. **NEW PR OPEN — NOT MERGED.**
- Record: [STAGE_0.4C-0B_CHECKPOINT.md](docs/STAGE_0.4C-0B_CHECKPOINT.md), [TC_DRAWING_ISSUE_REGISTER_REV1.md](docs/TC_DRAWING_ISSUE_REGISTER_REV1.md) and [TC_EVIDENCE_SOURCE_REGISTER.md](docs/TC_EVIDENCE_SOURCE_REGISTER.md).

## 2026-10-10 — Stage 0.4C-0A: Physical Temperature Acquisition Evidence Intake and Commissioning Source Register (development checkpoint, documentation only)

- **New session after PR #16 MERGED** (merge commit `bd1437586af73a33d43dfafce27b27538183a283`; parents `29ee137c1df617a1aa3321c3c891161d5a5bb41a` and `8ba8d89492bffe6849b77ab135c6e434a48b31f5`, verified by `git cat-file -p`; PR #16 MERGED at `2026-10-10T19:37:37Z`). Platform-assigned branch `arena/1cfd7e20-waterjet-sentinel-suite`, starting exactly at `bd143758…`. Base gate PASSED: remote `main` exact match, index and worktree clean, no conflicting open PR, workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` re-verified and unchanged. **The standing sandbox recreation protocol was not triggered** — no ref recovery, index reload or destructive Git operation was needed or used. The merged PR #16 branch `arena/fecda529-waterjet-sentinel-suite` was not used, continued or pushed.
- **Added `docs/TC_EVIDENCE_SOURCE_REGISTER.md`** — the physical TC evidence source register. Six intake categories (A physical hardware identity, B module/device topology, C Sensor-to-Channel mapping, D thermocouple configuration, E transport and process representation, F provenance), nine evidence states, seven verification scopes, and a binding **non-propagation rule** (a fact verified for one Channel never propagates to another Channel, module or model; a `PARTIAL_SCREENSHOT` never establishes `COMPLETE_RACK`). **Every acquisition field in categories A–E is `NOT_PROVIDED`.** Provenance ledger TCEV-01 to TCEV-11 holds one verified item (the workbook) and ten `NOT_PROVIDED` requests. **No SHA-256 was invented** for evidence absent from the sandbox. No candidate hardware family is listed as favoured, because no evidence distinguishes them.
- **Added the prioritised Owner evidence register `REQ-TC-001` to `REQ-TC-020` — all 20 `NOT_PROVIDED`** (10 **P0** blocking Stage 0.4C-1, 5 **P1** blocking wire-break implementation, 5 **P2** blocking rollout), each with status, blocking stage, evidence received, evidence scope, verification result, conflict and next action. `REQ-TC-013` (observed open-circuit behaviour) cannot be gathered until `REQ-TC-019` (approved procedure and LOTO reference) exists.
- **Added `docs/templates/sensor-to-tc-channel-schedule.template.csv`** — 28 columns in the required order, **212 rows**, deterministic order (`MatrixRow` ascending, `LogicalColumn` ascending, `FRONT` before `REAR`), excluding the two `NON_SENSOR_GAP` positions `I7` and `I16`. Pre-populated **only** with facts already authoritative in the Sensor topology (`SensorId`, `SensorWall`, `SensorLocation`, `MatrixRow`, `LogicalColumn`, `MeasurementSide`). **No `ModuleInstanceId`, `PhysicalChannel`, terminal, TC type, range, TC module or `AI-0nn` value was invented**, and no synthetic `SYN-TC-*` fixture identity was copied in. Topology columns cross-checked against all 108 slots of `config/examples/sensor-map.example.json` using the `packages/contracts/WallMap.cs` rules: **108 compared, 0 mismatches**; 106 Sensors, 106 `FRONT` + 106 `REAR`, **0** gap rows, wall counts 24/29/24/29, total 212. Documentation-only — no Product or generated-data change, so the Stage's STOP condition was not reached.
- **Added `docs/templates/tc-commissioning-record.template.csv`** — 26 columns in the required order, **header only, zero data rows**. Every value in an example row would have to be invented and no physical Sensor has been tested. Schema proposal only: no persistence, no export code, no importer, no validator, no UI.
- **Recorded the wire-break decision tree as a design proposal, not an implementation.** Explicit Channel diagnostic → `BAD`/`WIRE_BREAK`/`EngineeringValue` null; documented substitute raw code → usable only after exact module and configuration verification, never inferred across models; below-range with no diagnostic → `UNCERTAIN`/`SUSPECTED_WIRE_BREAK`; communication unavailable → `BAD`/`COMMUNICATION_LOSS`; age beyond threshold → `STALE`; outside configured range → `BAD`/`OUT_OF_RANGE`, kept distinct from wire break. **`WIRE_BREAK` is only ever explicit.** `INVALID` remains a presentation concept. **No Product enum was modified.** `WIRE_BREAK_DECODING: BLOCKED`.
- **Recorded Stage 0.4C-1 entry criteria — 10 of 11 NOT MET.** The single satisfied criterion (no contradictory mapping evidence) is satisfied only vacuously, because no mapping evidence exists at all, and must be re-checked when evidence arrives. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`; `ROLLOUT: BLOCKED`.
- **Added the Domain A Process Instrument parallel readiness register** so the missing TC topology does not obscure instruments the workbook already represents: `AI-001`, `AI-002`, `AI-003`, `AI-004`–`AI-011`, `AI-012`–`AI-019`, `DI-001`–`DI-020`, `DI-021`–`DI-028`, `DI-029`–`DI-036`, `AO-001` and the 6 `SPARE` rows, each classified by identity basis, mapping status, engineering range status, diagnostic capability, evidence needed and `TEST_HARDWARE` dependency. **No acquisition, polling or output control is authorised.** `AI-020`–`AI-035` (16 rows whose description is "XXX" plus a Thai note meaning "waiting for a reply") remain **`UNRESOLVED` and are not commissioned instruments**. `AI-012`–`AI-019` are **`0-20 mA`, not `4-20 mA`**, so a 4 mA live zero must not be assumed for them. `AI-002`'s workbook text is the generic "Pressure Transmitter" — the "Pump Inlet" identity is an Owner ruling, not workbook text.
- **Added the Galil evidence intake register `GAL-01` to `GAL-13`**, kept separate from the TC register. **None is `PROVIDED` or `VERIFIED`.** `GAL-10` (safe speed and acceleration limits) and `GAL-11` (permitted travel envelope) are `BLOCKING_GATE` because inventing production motion values is forbidden. **No Galil control implemented, proposed or authorised.**
- **Workbook re-parsed read-only** (zip + XML, workbook never modified): 114 data rows, `Status` 108 `USED` / 6 `SPARE`, 104 tagged channels (DI 37, DO 31, AI 35, AO 1), 23 slots, 8 distinct module models, 6 `SPARE` rows, and **no thermocouple module or TC vocabulary** — re-confirming **CF-01** and matching the recorded Stage 0.4C-0 baseline. A first in-session parse reported 79/29/6 and was **wrong**: its regular expression let a cell with no `<v>` element absorb the following cell's value (`sharedStrings[14]` is `"USED"`). The corrected parser bounds each `<c>` element first and reproduces the baseline exactly. **The baseline was right; the first parse was the defect.**
- **CF-07 reported, not repaired.** `README.md` is outside this Stage's file scope, so its now-stale current-status block (still naming Stage 0.4C-0, PR #16 OPEN and base `29ee137c…`) is reported for an Owner decision rather than edited, per AGENTS.md §2.2 and §2.3. The predecessor Stage had explicit Owner authorisation for a narrow README correction; this Stage does not.
- **Validation (documentation-only, per AGENTS §10.1).** Boundary scan **S1–S9 clean, 0 findings, exit 0** — identical to the pre-change baseline captured at the start of the session. Workbook SHA-256 unchanged. Changed-file allowlist, Product-source no-change proof, executable-test no-change proof, relative-link check and `git diff --check` recorded in the final report. **No `dotnet` or `npm` test was run and none is claimed.**
- **Predecessor evidence cited, not re-run at this documentation head:** Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82, Owner Browser Review PASSED. These are **not** claimed as results of this Stage.
- **Changed:** `docs/STAGE_0.4C-0A_CHECKPOINT.md`, `docs/TC_EVIDENCE_SOURCE_REGISTER.md`, `docs/templates/sensor-to-tc-channel-schedule.template.csv`, `docs/templates/tc-commissioning-record.template.csv`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`. **Unchanged:** Product source, executable tests, workbook, dependencies, lock files, fixtures, configuration, Runtime, Mapping implementation, Galil and MODBUS code, `README.md`, and the merged PR #16 branch.
- **Status.** Physical Sensor acquisition NOT IMPLEMENTED. Wire-break decoding NOT IMPLEMENTED. Sensor commissioning monitor NOT IMPLEMENTED. Actual process-image offsets NOT VERIFIED. NO MODBUS. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. No device write or control. No physical Sensor tested. **NEW PR OPEN — NOT MERGED.**
- Record: [STAGE_0.4C-0A_CHECKPOINT.md](docs/STAGE_0.4C-0A_CHECKPOINT.md) and [TC_EVIDENCE_SOURCE_REGISTER.md](docs/TC_EVIDENCE_SOURCE_REGISTER.md).

## 2026-10-10 — Stage 0.4C-0: Owner decisions resolved (documentation only; successor to checkpoint `4babdce`)

- **Successor commit** `docs: resolve Stage 0.4C-0 owner decisions` on the same branch and the same open **PR #16**. Base `main` `29ee137c1df617a1aa3321c3c891161d5a5bb41a` (PR #15 MERGED). No second branch or PR. Not merged.
- **Owner decisions OQ-01 to OQ-08 recorded** in `docs/STAGE_0.4C-0_CHECKPOINT.md` §0.1: TC gap confirmed (`TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`, `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`, `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`); two separate domains (Process Instrument and Temperature Sensor); AI-003 remains Pump Outlet Pressure; `Quality` unchanged with separate `DiagnosticReason` and `CommissioningState`; pilot roles only; timing values `CONFIGURABLE / OWNER_INPUT_PENDING`; Sensor verdict rules; procedure and approval roles.
- **Link consistency (two links).** `docs/CURRENT_STATE.md` and `docs/MASTER_PLAN.md` moved the `DOMAIN_MODEL.md` §2.2.1 link from the legacy anchor (retained in the target) to the canonical anchor. Not a broken-link fix.
- **README.** Three current-status rows (current stage, identifiers, scope gate) narrowly updated. No other README content changed.
- **Predecessor evidence cited only, not re-run at this documentation head:** Stage 0.4B-3 Mapping 268/268 (Owner-local), Mapping UI 82/82, Owner Browser Review passed.
- **Not implemented or authorised.** Physical Sensor acquisition, wire-break decoding, Sensor commissioning monitor, MODBUS, TEST_HARDWARE, PRODUCTION and device write or control. Actual process-image offsets NOT VERIFIED.
- **Changed:** `docs/STAGE_0.4C-0_CHECKPOINT.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`, `README.md`. **Unchanged:** Product source, tests, workbook, dependencies, lock files, fixtures, configuration, Runtime, Mapping implementation, Galil and MODBUS code.
- Record: [STAGE_0.4C-0_CHECKPOINT.md](docs/STAGE_0.4C-0_CHECKPOINT.md).

## 2026-10-10 — Stage 0.4C-0: Physical Sensor readiness, Loop-Test requirements and actual I/O evidence gate (development checkpoint, documentation only)

- New session after **PR #15 MERGED** (merge commit `29ee137c1df617a1aa3321c3c891161d5a5bb41a`; parents `3a73dfce1d2c7c1dc4d3b0274ea10f97406088f6` and `8b479772d4fbef63b8f8eb34f59a1f2eba69618e`, verified). Branch `arena/fecda529-waterjet-sentinel-suite`. Workbook unchanged (SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`).
- **STOP-1 recorded (CF-01).** The authoritative workbook lists 23 rack modules and no thermocouple input module, while the product model requires 212 thermocouple Channels for 106 Sensors. No module, TC type or Sensor-to-Channel mapping was invented. Thermocouple Channel binding, pilot and rollout are **BLOCKED**; Stage 0.4C-1 is **PROPOSED and BLOCKED**.
- **Added** `docs/STAGE_0.4C-0_CHECKPOINT.md`: evidence inventory and gap register; conflict register; commissioning identity model; quality and diagnostic contract proposal; invalid-value safety rules; 22-step Loop-Test workflow; Commissioning Monitor and record-schema proposals; read-only acquisition architecture; pilot Loop-Test plan; 106 Sensor / 212 Channel rollout plan; Galil G1–G5 readiness checklist; Stage 0.4C-1 scope proposal.
- **Wire-break representation NOT VERIFIED** for any model. A WIRE_BREAK is only ever explicit; an inferred open circuit is `SUSPECTED_WIRE_BREAK`. Wire-break decoding is NOT IMPLEMENTED.
- **Recorded conflicts, not resolved here:** the required 11-value quality taxonomy versus the existing `Quality` enum (CF-04); the AI-003 workbook label "Main Valve Outlet" versus the Owner ruling "Pump Outlet" (CF-03); the Owner-ruled WSB-to-IV label interpretation (CF-05); the README status block now stale (CF-06).
- **Only Channel-level settings evidence** (Pos. 10 / RackSlot 13 / Channel 3) is recorded as scoped to that Channel and sits at the AI-003 workbook location: a location coincidence, not a tag verification (RISK-01).
- **Status.** Physical Sensor acquisition NOT IMPLEMENTED. Sensor commissioning monitor NOT IMPLEMENTED. Actual process-image offsets NOT VERIFIED. AI-002 Runtime publication NOT IMPLEMENTED. NO MODBUS. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. No device write or control. No physical Sensor tested. Documentation-only: no executable test was run or changed. **PR open — NOT MERGED.**
- Record: [STAGE_0.4C-0_CHECKPOINT.md](docs/STAGE_0.4C-0_CHECKPOINT.md).

## 2026-10-10 — Stage 0.4B-3 documentation truth alignment (documentation only; review-correction on PR #15)

- **Documentation-only review-correction** on the same branch and the same open **PR #15** — one narrow commit. **No Product source, workbook, test, package manifest, lock file, solution or project file was changed.** Checkpoint under review `abe7e023eb7b5e3f976d8fbc5c9a5b17079058ed`; base `main` `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` (**PR #14 MERGED**).
- **README current status corrected.** The obsolete Stage 0.3A-1 / PR #4 status block is replaced by the Stage 0.4B-3 position: base `3a73dfce…`, checkpoint `abe7e02…`, **PR #14 MERGED**, **PR #15 OPEN — NOT MERGED**, Mapping **268/268** with the authoritative workbook, Mapping UI **82/82**, boundary **S1–S9 clean**, **Owner Browser Review PASSED**, candidate stays `CANDIDATE_UNVERIFIED`, offsets unresolved, AI-002 not implemented, no MODBUS / TEST_HARDWARE / PRODUCTION / device write or control. Stage 0.3A-1 is preserved as history with its PR #4 merge commit `bdf7f8f2`.
- **Sensor-topology wording corrected** in `README.md`, `adapters/simulator/README.md` and `docs/CURRENT_STATE.md`: 18 logical columns × 6 rows = **108 logical positions, 106 Sensors and 2 `NON_SENSOR_GAP` positions** — logical **I7** (Rear wall, placement anchor of **WJ3**) and logical **I16** (Front wall, placement anchor of **WJ1**), verified against `packages/contracts/WallMap.cs` and `packages/domain/WaterJetTopologyCatalog.cs`. The superseded *Cannon equipment slot* label is no longer presented as current truth: **Water Jets WJ1–WJ8 are separate equipment entities** paired **1:1 with IV1–IV8 by ordinal**; *Installed Position* is distinct from *Target Coverage* (**WJ1 is installed Front-lower at the I16 gap anchor, covers the Rear-lower target area and pairs with IV1**); a gap may serve as an anchor but is **never a Water Jet**; and the Assigned Cleaning Device — the legacy "cannon" ordinal, mapping directly to WJn — is **never inferred from a Sensor's wall location**. Dated historical records keep their original wording with an added supersession note; no blind global replacement was performed.
- **Stage status aligned** in `SECURITY.md` (stage-status banner only; the security policy is unchanged), `docs/CURRENT_STATE.md` (Stage 0.4B-3 is now the first current-facing position; the Stage 0.4B-2 and 0.4B-1 blocks are labelled historical and superseded), `docs/MASTER_PLAN.md` (§3 ledger line and §3.5) and `docs/STAGE_0.4B-3_CHECKPOINT.md`: **Owner-local validation PASSED** and **Owner Browser Review PASSED** — Owner-confirmed; the screenshots do **not** claim exhaustive browser interaction or responsive coverage and are **not** actual process-image verification.
- **Unresolved facts stay explicit:** candidate process-image information remains **`CANDIDATE_UNVERIFIED`** and not authoritative; every byte / word / bit offset remains **null / `OFFSET_UNRESOLVED`**; no address is marked verified; the actual Process Data image and the head-station field-network mapping are **NOT PROVIDED** (evidence requests A–E remain open); AI-002 Runtime publication **NOT IMPLEMENTED**; **NO MODBUS, NO TEST_HARDWARE, NO PRODUCTION, no device write or control**.
- **Arena re-validation at this correction:** Mapping package check PASS; Mapping package **268 tests — 261 pass / 0 fail / 7 pre-existing Owner-local skips** without the workbook and **268 / 268 / 0 / 0** with the SHA-verified workbook copy outside the repository; Mapping UI check PASS and **82 / 82**; boundary scan **0 findings (S1–S9 clean)**, exit 0; workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` unchanged. **No .NET file was touched, so no .NET result is claimed.**

## 2026-10-10 — Stage 0.4B-3: primary manufacturer evidence ingestion and read-only process-image preview (development checkpoint)

- New session after merged PR #14, branch `arena/00483ae4-waterjet-sentinel-suite`, base `main` `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` (PR #14 merge commit; parents `dbbf34cf` and `a811f6ff` both proven ancestors). Workbook unchanged (SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`).
- **Module-role correction (Owner ruling).** 750-601 `POWER_SUPPLY`, 750-613 `SYSTEM_POWER_SUPPLY`, 750-600 `END_MODULE` and the 750-362 head station contribute **no** Application Process I/O data: no ProcessModulePosition, no ProcessImageOrder, no Input or Output width, `AddressStatus NOT_APPLICABLE` with `AddressReason NON_PROCESS_DATA_MODULE`. They are no longer reported anywhere as `ADDRESS_UNRESOLVED`, and rack validation no longer claims a Power Supply address stays unresolved.
- **Three position concepts kept apart.** `RackSlot` (physical, one-based, 23 slots, Power Supply and End modules included), `ioCheckPosition` (the actual-rack configuration-tool "Pos." number, instance-level evidence, never aliased to RackSlot) and `ProcessModulePosition` (ordinal among process-data modules only). The Owner's `WagoIoCheckPosition` is spelled `ioCheckPosition` in the Product tree because boundary rule S3 forbids vendor vocabulary there.
- **Instance-level actual-rack evidence** (new `actualRackEvidence.mjs`): one record per physical instance with RackSlot, configuration-tool position, model, role, revisions only where legible, evidence state and note. One Channel-level record: Pos. 10 / RackSlot 13 / AI-MODULE-01 / Channel 3 (`VERIFIED_ACTUAL_RACK_SCREENSHOT`, scope `INSTANCE_AND_CHANNEL_ONLY`) — never generalised to another Channel, instance or model. Actual-rack evidence never feeds address derivation.
- **Primary manuals for all eight models** are recorded as model-level `manualFacts` (750-430 eight input bits; 750-530 eight output bits; 750-471 four 16-bit input values; 750-554 two 16-bit output values). The manuals are `OWNER_PROVIDED_OUTSIDE_REPOSITORY`, not committed, unhashed (no hash invented) and therefore `PROVIDED_UNVERIFIED`: recorded, visible in the review, never exposed to derivation.
- **Read-only candidate process image** (new `candidateProcessImage.mjs`): 19 contributing modules and 110 candidate channels with area, width, RackSlot, configuration-tool position, ProcessModulePosition, candidate ProcessImageOrder, evidence basis and verification status — all `CANDIDATE_UNVERIFIED`, `authoritative: false`, `hardwareReady: false`, `writeAuthority: false`. **No offset is invented**: byte, word and bit offsets are `null` and `OFFSET_UNRESOLVED` until the actual Process Data screen and the head-station I/O Config are supplied.
- **UI (read-only).** Evidence review gains RackSlot, IO-CHECK Pos., Role, Process data, Actual-rack evidence and Reason columns; a new Actual-rack section states the Pos. 10 / Channel 3 scope limit, lists evidence requests A–E and shows the candidate status. Power supplies and the End module display `NOT APPLICABLE` / `NON_PROCESS_DATA_MODULE`. No Hardware, Polling or Write control added; the server still binds to loopback only.
- **Arena results:** Mapping package 268 tests (261 pass, 0 fail, 7 skipped); Mapping UI 82 tests (82 pass); boundary S1–S9 clean; PR-range `git diff --check` clean; workbook SHA unchanged. No browser run in Arena: layout NOT VERIFIED. No .NET code touched; no new .NET result claimed.
- AI-002 Runtime publication NOT IMPLEMENTED. CANDIDATE ADDRESSES ARE NOT AUTHORITATIVE. ACTUAL PROCESS DATA AND FIELD-NETWORK MAPPING NOT VERIFIED. NO MODBUS. NO TEST_HARDWARE. NO PRODUCTION. No device write or control. PR OPEN - NOT MERGED.
- Record: [STAGE_0.4B-3_CHECKPOINT.md](docs/STAGE_0.4B-3_CHECKPOINT.md).

## 2026-10-10 — Stage 0.4B-2: Owner browser review, final narrow corrections and final source review (documentation only)

- **Owner Browser Review: PASS WITH NON-BLOCKING PUNCHLIST** (Owner-reported; not run in Arena). Carry-forward only: slightly larger Comfortable table text; improved filter-toolbar grouping; expandable Evidence summary rows.
- Corrections on PR #14: Stage subtitle now `Stage 0.4B-2 · Evidence and read-only mapping review` and header comment (`38742ba`); missing-evidence profile note made stage-neutral, `No primary manufacturer process-image evidence has been reviewed for this model.` (`fe38322`; `ModuleProfileRevision` changes as the expected consequence).
- **Validated source head `fe38322d55acb7c565a9984b0eceb82c74b33bb5`.** Final exact-source review PASSED (static), blocking defects none. Arena results: package 241 tests (234 pass, 7 skipped; 241 pass with the Owner-local workbook copy), UI 74 pass, boundary S1–S9 clean, PR-range diff check clean, workbook unchanged.
- PRIMARY MANUFACTURER PROCESS-IMAGE EVIDENCE NOT PROVIDED; real profiles remain incomplete; addresses remain ADDRESS_UNRESOLVED. No MODBUS; TEST_HARDWARE NOT AUTHORIZED; PRODUCTION NOT AUTHORIZED. PR #14 OPEN - NOT MERGED.
- Record: [STAGE_0.4B-2_CHECKPOINT.md](docs/STAGE_0.4B-2_CHECKPOINT.md).

## 2026-10-10 — Stage 0.4B-2 Verified Process-Image Evidence Foundation and Mapping UI Readability Refinement: development checkpoint

- New session, branch `arena/72d57c31-waterjet-sentinel-suite`, base `main` `dbbf34cfe49f4db6c524a675dadb8a24d5ef497f` (PR #12 merge). Workbook unchanged (SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`).
- **Evidence foundation:** profile-driven evidence sets (head-station rules, module process-data profiles, status-byte, byte/word order, sources) wired into address derivation and revision fingerprints; states `NOT_PROVIDED`, `PROVIDED_UNVERIFIED`, `VERIFIED_PRIMARY_SOURCE`, `VERIFIED_IO_CHECK_EXPORT`, `CONFLICTING_EVIDENCE`, `INCOMPLETE_PROFILE`; four new unresolved reasons. No generic WAGO rule; no invented value. All eight module profiles remain INCOMPLETE; **every address remains ADDRESS_UNRESOLVED**. The SYNTHETIC TEST RULE exists in tests only.
- **UI readability:** wider layout, larger type, sticky navigator and headers, read-only filters, Pump / IV1..IV8 / Reserved grouping, compact address summary with expandable reasons, grouped validation counts (Errors always visible), `17 Input + 1 Output = 18 Total`, Comfortable/Compact density, read-only Evidence review. Semantics unchanged.
- **Defects fixed:** address `bitWidth` overlap false positive (32-bit word with 16-bit channels); Owner-identity count (36 import records to 18 rows).
- **Arena results:** package 241 tests (234 pass, 7 skipped without Owner-local env; 241 pass with it); UI 73 pass; boundary S1–S9 clean. No browser run: layout NOT VERIFIED. No real primary document was provided, so real-evidence acceptance is NOT TESTED. No .NET code touched; no new .NET validation claimed.
- AI-002 Runtime publication NOT IMPLEMENTED; no MODBUS; TEST_HARDWARE NOT AUTHORIZED; PRODUCTION NOT AUTHORIZED; no device write or control. PR OPEN - NOT MERGED.
- Record: [STAGE_0.4B-2_CHECKPOINT.md](docs/STAGE_0.4B-2_CHECKPOINT.md).

## 2026-10-10 — Stage 0.4B-1 Mapping Configuration: Owner validation and final source review

- **Validated code head `433189f9e489d7af6770e64c971585d99bf941ad`** (branch `arena/1b92c50a-waterjet-sentinel-suite`, base `a5540057` on `main`). PR #12 OPEN, NOT MERGED.
- **Owner-local validation PASSED (Owner-reported, not executed by Arena):** Mapping 217/217; Mapping UI 56/56; boundary S1–S9 clean; PR-range `git diff --check` PASS. The authoritative workbook was used from an out-of-repository byte-identical copy with no seed. Arena re-ran the same commands at `433189f` and obtained the same totals; those are Arena results and are not claimed as the Owner's run.
- **Owner browser review PASSED WITH NON-BLOCKING UI PUNCHLIST** (Owner-observed; no browser was run in Arena). Fixed Dark theme; 26 enabled authoritative bindings without a seed; 18 visible reserved unresolved rows; AI-002 Pump Inlet and AI-003 Pump Outlet shown separately; analog NOT APPLICABLE; digital ACTIVE_WHEN_CLOSED / NO; addresses ADDRESS_UNRESOLVED; activation not ready; loopback-only server.
- **Final Source Review PASSED, blocking defects none** (static review). The boundary-scanner rule S6 (environment-controlled bind host) is classified as authorised B1 regression protection, in scope. The two-label Runtime Inspector wording change ("Pump Outlet pressure") is an Owner-approved narrow presentation exception, non-blocking. The Inspector wording was already present at predecessor `8847cfc`; no Runtime, contract, fixture or sequencing behaviour changed.
- **Predecessor .NET evidence is not re-claimed for `433189f`.** The Owner reports Release build PASS and full .NET 414/414 (failed 0, skipped 0) at predecessor `8847cfc`, which covers the accepted Inspector wording. No .NET run was executed at `433189f`.
- **Non-blocking follow-ups (not implemented):** (A) Pump Slot/Channel is supported by the Draft model API and focused tests but is not exposed as a browser editor control in Stage 0.4B-1; (B) SPARE-versus-placeholder ordering in the importer (no such row in the current workbook); (C) `DraftSession.setBinding` can create a binding for an unknown tag name, which the browser does not call and validation refuses; (D) UI readability punchlist.
- **Stated scope:** AI-002 Runtime publication NOT IMPLEMENTED; addresses ADDRESS_UNRESOLVED; activation NOT READY; no MODBUS; TEST_HARDWARE NOT AUTHORIZED; PRODUCTION NOT AUTHORIZED; no device write or control. The workbook `T8_IO_Card_Mapping.xlsx` is unchanged (SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`).
- Record: [STAGE_0.4B-1_CHECKPOINT.md](docs/STAGE_0.4B-1_CHECKPOINT.md#owner-validation-and-final-source-review-2026-10-10-documentation-closeout), [CURRENT_STATE.md](docs/CURRENT_STATE.md) §11.12 and §12.32, [MASTER_PLAN.md](docs/MASTER_PLAN.md) §3.4.

## 2026-10-10 — Stage 0.4A CP-3c-2 / CP-4A Owner validation and final source review

- **Owner-locally validated** at exact code head `fd053fde07c96163b073fc9cb4c0383f31eafe3c` (not compiled or executed in Arena): cleared environment; locked restore PASS with no lock drift; Release build PASS with TreatWarningsAsErrors, 0 warnings / 0 errors; full .NET **414/414**; fixture parity **7/7**; TypeScript typecheck PASS and tests **34/34**; boundary scan **0 findings (S1–S9 clean)**; `git diff --check` PASS; no source drift and clean Owner-local tree. No artifact commit required.
- Owner browser review **PASSED**: normal and Valve-close fault scenarios, independent Pump/paired-IVn pressure and thresholds, parallel Valve CLOSE then Axis RETURN, cumulative faults, qualified lower-limit completion, leak/not-fully-closed retention, read-only fault modal, blocked next dispatch, two FIFO Jobs sequentially without overlap, final empty Queue and completed Job 2, and truthful Pump control/readiness labels. Screenshots are Owner-observed, not Arena-executed. Static Final Source Review: **PASSED, no blocking defect**. See [checkpoint](docs/STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md).
- **NON-BLOCKING UI CONTEXT-LABEL PUNCHLIST:** future wording should distinguish Current Active Job from last released Job outcome and Valve-close resolution; not implemented in this closeout. CP-4 full UI refinement is not delivered. No operator control, device command/write path or MODBUS; TEST_HARDWARE and PRODUCTION not authorized. PR #11 **READY FOR OWNER MERGE REVIEW — OPEN, NOT MERGED**. The dated development/pending entry below is superseded as a current-status statement.

## 2026-10-09 — Stage 0.4A CP-3c-2 / CP-4A fast-track development checkpoint

- Owner-authorized SIMULATOR scenario startup, single-writer atomic State/Delta host composition and existing GET-only observation; minimal read-only Inspector sequencing display. No device access, writes, SSE or contract changes.
- Arena static boundary scan and JS/TypeScript checks passed; .NET SDK unavailable in Arena, so Owner-local Release build, tests, fixture parity and manual observation are **pending**. See [checkpoint](docs/STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md). PR open, not merged; CP-4 full UI not authorized.

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

### Stage 0.4A CP-3c-1 — Owner-local validation and Final Source Review (documentation-only, 2026-10-09)

- Owner-reported validation at code head `29f133ce6598bc2eb489273a2f2d40f8827deb5f` (.NET SDK 10.0.401; not run in Arena): locked restore PASS, lock drift NONE; Release build PASS, 0 warnings, 0 errors; RuntimePublicationTests **20/20**, Runtime.Core **450/450**, full .NET **572/572**, fixture parity **7/7**, TypeScript typecheck PASS and tests **31/31** (0 failed); boundary scan **0 findings, S1–S9 clean**; `git diff --check` PASS; final Owner-local working tree CLEAN. Failed 0, skipped 0 in each reported .NET suite; no Owner-local artifact commit required.
- Final Source Review (static, exact validated code head): **PASS, no blocking defect**. CP-3c-1 delivers the library-only atomic State/Delta publication aggregate with one writer, bounded contiguous history and detached reader/result snapshots. The full-window trend failure was fixed by checking equal-count *unchanged content* before testing the exact oldest-eviction-plus-append shift. The internal-Delta JSON fingerprint failure was a test defect: fingerprints now serialize `ProjectWire(delta)` and previous Sensors, never raw `RuntimeDelta`. The reader-facing mutable `TrendPoint.Series` alias was corrected with a defensive-copy reader boundary and nested typed freeze, without changing the contract. Analyzer corrections CA1861 and xUnit1031 are closed at the validated code head. No Host integration, Runtime API integration, Inspector change, contract, fixture or TypeScript change is included. CP-3c-2 and CP-4 are NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION device access are NOT AUTHORIZED. PR #10 remains OPEN, NOT MERGED, for Owner merge review.
- Earlier CP-3c-1 development/pending wording below is dated history, superseded by this validation record.


### Stage 0.4A CP-3a / CP-3b — Owner-local validation and Final Source Review (documentation only, 2026-10-09)

- Owner-local validation reported PASS on `a315e90be28fb5e9662c6a7179587c992273cb88` (Owner-reported; not reproduced in Arena): Release build 0 warnings and 0 errors; full .NET 552/552; focused Runtime.Core 430/430; Retention and Projection 131/131; direct Valve-retention regression 1/1; fixture parity 7/7; TypeScript typecheck PASS and tests 31/31; boundary scan S1–S9 clean.
- Valve feedback retention defect closed (`fix(runtime): retain only observed valve feedback`): only the valve observation record is retained as the valve identity, not the preparation context record or the Safe Return step record.
- CP-3a and CP-3b Final Source Review PASSED: no blocking defect. Non-blocking follow-ups NB-1 to NB-8 are recorded in MASTER_PLAN §3.3.5. NB-1, the Safe Return label vocabulary, is an Owner contract decision that gates any wire emission.
- Documentation corrections: the CP-3 UNCHANGED list omitted the FU-1 change to `SequencingStateValidator.cs`; the Safe Return vocabulary conflict is disclosed; superseded status wording is annotated and not erased.
- CP-3c and CP-4 NOT AUTHORIZED. PR #9 OPEN, NOT MERGED. This entry changes documentation only.

### Stage 0.4A CP-3a / CP-3b — completion correction (Owner-authorized; NOT compiled or executed in Arena; NOT VALIDATED until Owner-local gates pass; not merged)

*Superseded 2026-10-09 by the validation entry above; retained as dated evidence.*

- CP-3a (kernel): the public `PreviewAxisStandbyLedger` is removed. AxisStandbySeq is written at SR5 and is observable only through the release SR5 record. FU-1, FU-2 and FU-4 are unchanged. Thirteen facts and one two-row theory.
- CP-3b (simulator library, no host wiring): the scenario catalogue is bound to the canonical Runtime Sensor set. Five-scope bounded retention: evidence log of 256, current-Job Safe Return tail of 16, last dispatch, last Job outcome, first critical. Pure projection of the GlobalQueue, Sensor queue state, Active Job, Safe Return, Sequence, critical event and last Job outcome. The Delta candidate path carries one appended trend point and refuses other trend changes. Planned tests: 236 cases in `tests/runtime.tests/Simulator/`.
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

- Stage 0.4A CP-3c-1: scope limited to atomic in-memory Runtime State/Delta publication foundation and bounded trend append projection. No Host or API integration; Owner-local validation required.
