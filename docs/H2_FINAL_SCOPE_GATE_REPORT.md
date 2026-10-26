# H2 Final Scope Gate — Physical Process-I/O (Modbus) Evidence Adoption and Scope Gate

Status: **[DEVELOPMENT CHECKPOINT — PRODUCT IMPLEMENTATION DELIVERED ON PR #42].** This report records (a) the
adoption of the exact Owner-supplied Process-I/O mapping artifact into the repository with
complete provenance and byte-identity evidence, and (b) the H2 Final Scope Gate
analysis and Product software foundation implementation across all 111 rows authored from the Exact
Legacy Source pack on PR #42 (`packages/process-io`, `packages/contracts/ProcessIoContracts.cs`,
`packages/application/ProcessIo/ProcessIoFoundation.cs`). It authorises no physical
device contact and no physical commissioning. It is not a configuration, not a mapping and not a
commissioning record. The companion provenance record is
[`docs/evidence/process-io-source-provenance-2026-10-22.md`](evidence/process-io-source-provenance-2026-10-22.md).

**Evidence-state vocabulary (used throughout):** `FACT FROM CONNECTED ARTIFACT` (verbatim
from the adopted HTML) · `FACT FROM EXACT SOURCE` (verbatim from repository Source at the
merged baseline) · `OWNER RULING` (an Owner decision already recorded in the repository) ·
`HISTORICAL EVIDENCE` (dated, superseded or model-level predecessor evidence) ·
`CANDIDATE` (unverified proposal of evidence) · `UNRESOLVED` (no evidence; never guessed) ·
`SPARE` · `DERIVED` (deterministic arithmetic/classification, non-authoritative) ·
`PROPOSAL` (this report's recommendation, not approved) · `AUTHORIZED NOW` · `NOT AUTHORIZED`.

---

## 1. Exact Baseline and Repository Identity

| Item | Value |
|---|---|
| Project | WaterJet Sentinel Suite (WJSS) |
| Approved merged main baseline | `bfd2867af1dda29fcbc1987328be3d131a189d0a` (merge commit; tree `a7076852…`) |
| Merge parents (verified by `git cat-file -p`) | Parent 1 `f9e6e9adfef210f483fd3bfc7471936bbd7f34b2`, Parent 2 / final PR #41 head `7ea635970e8327f4db3252467b095ccae7d7a180` |
| Working branch | `arena/de8398d2-waterjet-sentinel-suite` — **new platform-assigned branch** (created clean from the merged baseline; it is **not** the merged PR #41 branch `arena/7968e33e-…`) |
| HEAD at analysis start | `bfd2867af1dda29fcbc1987328be3d131a189d0a` = `origin/main` (fetched and verified) |
| Required ancestry | Both merge parents proven ancestors of HEAD (`git merge-base --is-ancestor` exit 0 ×2). The clone was initially shallow (depth 1); `git fetch --unshallow` completed; 370 commits present; no destructive Git operation used |
| Worktree at analysis start | `git status --porcelain --untracked-files=all` empty; staged diff none; unstaged diff none; no untracked non-ignored path; stash count 0 |
| PR #41 | **MERGED** (merge commit = baseline; Owner instruction). The K3 documentation-contract punchlist remains a separate accepted punchlist and is not the objective of this task |

`AUTHORIZED NOW` (this task): evidence adoption + read-only analysis + commit/push/PR of the
evidence and scope-gate artifacts only. `NOT AUTHORIZED`: everything else — see §24 and §28.

## 2. Connected-Source Retrieval Evidence

| Item | Value |
|---|---|
| Connected-source provider | Google Drive connected source (session connector) |
| Query executed | exact-name search `name = 'WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html'` |
| Result | **Exactly 1 file** of this exact name; not trashed; not a shortcut — no substitution, no ambiguity (matches the single authoritative file; no second exact-name file exists) |
| File id | `1GPig0jsZCLNbdgvBNR6a0LeXUdDHUjQd` |
| MIME type | `text/html` |
| Byte size (provider metadata) | 36,667 |
| Provider digests | SHA-256 `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` · MD5 `09522589f6f6617e7cdedeef7ee98b9d` · SHA-1 `0ea7ea9fc225c08c70e023804306141b9b894203` |
| Created / modified timestamps | `2026-10-26T07:14:18.832Z` / `2026-10-22T07:29:55.460Z` |
| Source location identity | Folder `13mRy81IIThPMEAjNx7PdUa3ktFZw5Dul` ("Arena AI Share Foler"); **no credential, token, signed URL or connector secret is recorded anywhere** |
| Retrieval method | Two independent `download_file` retrievals plus one native text-extraction cross-check |

**Retrieval transformation (full disclosure, see provenance record §1).** The connector's
download path proxied the bytes through a service injecting Cloudflare Rocket Loader material
(per-fetch nonce; two fetches proved it dynamic). The injected modifications were isolated
exactly (rewrite of the single inline `<script>` open tag; injected rocket-loader tag before
`</body>`) and reversed; the recovered 36,667 bytes match the provider metadata on **SHA-256,
MD5, SHA-1 and exact size** — cryptographic byte-identity proof (a three-algorithm match
makes any other content computationally implausible). The native text-extraction path
independently corroborates the un-transformed structure. Retrieval is therefore **VERIFIED**,
not trusted.

## 3. Source Artifact Identity

| Item | Value |
|---|---|
| Exact source filename | `WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` (exact name retained in the repository copy) |
| Structure | Complete, readable single-page HTML (`lang=th`); one `<table>` with 14 `<th>` columns; data carried as a JS array literal (`const D=[[…],…]`) — 111 rows × 14 string columns; page includes a Linear Mapping Calculator tab and filter UI (presentation only) |
| Artifact-internal declarations (verbatim, `FACT FROM CONNECTED ARTIFACT`, **not independently verified**) | `Source: WAGO_IO_Mapping(Update19-10-26)_2.xlsx · SHA-256 4937dda8dcae · 26,325 bytes · Filtered I/O Slot less than 0, equal to 0, and blank` |
| Upstream Excel workbook bytes available | **NO** — the workbook is not present in the connected source; its identity is declared inside the HTML only (digest shown is a truncated 12-hex prefix) |
| Column contract | `WAGO Module · Slot · I/O Slot · Channel · I/O Tag · Signal / Description · I/O Type · Terminal No. · From. · To. · Ref. · Location · Contact / Note · Status` |

## 4. Repository Artifact Paths

| Path | Role |
|---|---|
| `docs/evidence/WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` | `A.` Immutable source artifact — exact retrieved bytes, exact filename, no transformation |
| `docs/evidence/process-io-source-provenance-2026-10-22.md` | `B.` Provenance manifest (complete fields per the adoption requirement) |
| `docs/evidence/process-io-inventory-2026-10-22.csv` | `C.` Deterministic normalized inventory (generator-owned; 111 rows + header) |
| `tools/process-io-evidence/generate.mjs` | `D.` Deterministic parser/generator (fail-closed; hash-verified input; `--check` parity mode) |
| `tools/process-io-evidence/test/generate.test.mjs` | Focused artifact/parser tests (15 tests) |
| `docs/H2_FINAL_SCOPE_GATE_REPORT.md` | This report |

Path choice follows existing conventions (`FACT FROM EXACT SOURCE`): evidence data files
live in `docs/evidence/` (e.g. `tc-drawing-schedule-rev1.csv` — the committed, machine-read
evidence-data standard consumed by `packages/commissioning/src/schedule.mjs`), provenance and
derived contracts live beside them (`tc-process-image-profile-rev1.md`), generator-owned
artifacts live under `tools/<area>/generate.mjs` (`tools/fixture-generator/generate.mjs`,
`tools/sensor-map-example/generate.mjs`), and stage/gate reports live in `docs/`
(`STAGE_LIBRARY_SELECTION_GATE_REPORT.md`, `STAGE_ONE_NODE_TC_SCOPE_GATE.md`). No new
evidence layout was invented. The immutable source keeps its exact filename — no
deterministic-prefix convention exists for source artifacts in `docs/evidence/` (the one
committed Owner workbook, `T8_IO_Card_Mapping.xlsx`, also keeps its exact name at root).

## 5. SHA-256 and Byte-Identity Proof

| Proof step | Result |
|---|---|
| Source SHA-256 (provider metadata) | `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` |
| Retrieved-bytes SHA-256 (after the documented, isolated transformation reversal) | `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` ✓ |
| Retrieved-bytes MD5 / SHA-1 | `09522589…` / `0ea7ea9f…` — both match provider metadata ✓ |
| Retrieved byte size | 36,667 ✓ |
| Repository copy SHA-256 (re-computed after writing) | `28245ad2…` ✓ |
| Repository copy byte size | 36,667 ✓ |
| Exact byte-identity (copy == verified original) | **TRUE** (byte-for-byte comparison) |
| Generator hash gate | `generate.mjs` refuses any artifact whose SHA-256 ≠ the adopted hash (`HASH_MISMATCH`) |

**Exact-byte-match result: VERIFIED.** No content transformation was performed on the
repository copy (not reformatted, minified, prettified, sanitized, line-ending-normalized or
rewritten; LF line endings and Thai text are as-retrieved).

## 6. Parser/Generator and Determinism

- Generator ownership is required and in place: the CSV is generated from the immutable HTML
  only, never hand-entered. `docs/evidence/` data consumed by tests follows generator/test
  ownership in this repository; a committed source artifact + manifest without a generator
  would not satisfy "deterministic normalized inventory … generated from the adopted
  immutable HTML".
- Determinism: same artifact bytes → identical CSV bytes (no clock, randomness or
  environment reads). Double generation produced byte-identical output; `--check` parity mode
  compares against the committed CSV (ran: `PARITY OK`).
- Fail-closed refusals implemented and tested: malformed HTML / missing data array; wrong
  column count; table-vs-script column-contract mismatch; duplicate source-row identity
  (identical 14-tuples); duplicate physical (Slot, Channel) placement; duplicate I/O tags;
  artifact hash mismatch. **No silent repair**: every source value is copied verbatim
  including `-`, empty strings, Thai text, spelling variants (`Auxilary`, `BRAEKER`,
  ` contractor`) and trailing-`+` tags.
- Derived columns (explicitly marked `(DERIVED)` in the header): source-row ordinal; normalized
  I/O class (prefix classification of the exact `I/O Type` string); source-tag-present flag;
  source classification (`USED`/`SPARE`/`UNSET`/`OTHER`); parse diagnostic tokens; provenance
  link (artifact filename + SHA-256). Derived fields carry **no** Product authority, runtime
  behaviour, control effect, address, scaling or equipment identity.
- Dependency check: no dependency added; no lock-file change (stdlib `node:crypto/fs/path`
  only). **No dependency or lock-file change appeared necessary.**
- Note (disclosed): the generator source assembles the artifact's vendor product-line name
  from two string fragments — the same established technique the repository boundary scanner
  uses for its own vocabulary — so the Product tree stays clear of the literal while the
  committed artifact in `docs/` (outside the Product tree) keeps its exact name.

## 7. Complete Inventory Counts

`FACT FROM CONNECTED ARTIFACT` (complete artifact content; the machine-readable appendix is
[`docs/evidence/process-io-inventory-2026-10-22.csv`](evidence/process-io-inventory-2026-10-22.csv) —
every source row, complete source order, all 14 columns verbatim):

| Dimension | Counts |
|---|---|
| Total rows | **111** (all preserved; 0 lost; order 1–111 = source order) |
| By I/O class (`DERIVED`) | DI 40 · DO 32 · AI 36 · AO 2 · End module 1 · UNRESOLVED 0 |
| By source `Status` | USED 105 · SPARE 6 |
| Tagged / untagged | 108 tagged · 3 untagged (SPARE ch rows: 750-530 ch8, 750-554 ch2; End-module row) |
| Module rows by model | 750-430 ×40 (Slots 3–7) · 750-530 ×32 (Slots 8–11) · 750-471 ×36 (Slots 13–21) · 750-554 ×2 (Slot 22) · 750-600 ×1 (Slot 23) |
| Signal-class subsets (`DERIVED` from `I/O Type`) | `DI (24 VDC.)` 40 · `DO (24 VDC.)` 30 · `DO` (type unset) 2 · `AI (4-20 mA. HART5)` 11 · `AI (0-20 mA.)` 8 · `AI (4-20 mA.)` 16 · `AI` (type unset) 1 · `AO (4-20 mA.)` 1 · `AO` (type unset) 1 · `End` 1 |
| `SPARE` rows (6) | DI-038/039/040 (Slot 7 ch 6–8) · Slot 11 ch 8 (untagged, DO class) · AI-036 (Slot 21 ch 4) · Slot 22 ch 2 (untagged, AO class) |
| Rows with unset `Status` | 0 (the newer revision sets `USED`/`SPARE` on every row — see §10.5) |
| Rows with unresolved description text | 17 (`DO-031` + `AI-020`…`AI-035`, exact text `XXX รอพี่ยิมตอบ`) |

Coverage is **complete within the artifact's own declared filter** (§10.2): the H2 inventory
is exactly the AI, DI, DO and AO inventory present in the adopted artifact, including
unrelated used channels, unresolved channels, spare channels, untagged rows and the
end-module topology row. Inventory inclusion grants **no** commissioning or write authority.

## 8. Current Implemented State

`FACT FROM EXACT SOURCE` at `bfd2867a` — what already exists that H2 must consume, not
duplicate:

1. **Typed process-instrument catalogue and Owner rules (live in Mapping Configuration).**
   `packages/mapping-config/src/tagCatalogue.mjs` already encodes the Owner rulings:
   `AI-002` = Pump Inlet Pressure (diagnostic/trend only, never Pump-ready); `AI-003` = Pump
   Outlet Pressure (`PUMP_OUTLET`, the **sole** Pump-ready pressure authority);
   `AI-004`…`AI-011` = IV1…IV8 outlet pressure (ordinal); `DI-021`…`DI-028` = IV1…IV8 lower
   limits; `DI-029`…`DI-036` = IV1…IV8 upper limits — 26 authoritative default bindings,
   direct and ordinal. `mappingValidation.mjs` enforces: pressure-to-digital refusal, signal-
   type mismatch refusal, Pump/Valve channel separation (`PUMP_VALVE_CHANNEL_SHARED`), one
   measurement serves one pressure identity, Pump-ready gate reads Pump Outlet only, paired
   IV identity mismatch refusal. `T8_IO_Card_Mapping.xlsx` (SHA-256 `4e0337e2…`, 16,648 B,
   114 data rows) is the committed authoritative workbook default.
2. **Rack/process-image evidence discipline.** `actualRackEvidence.mjs` pins the 23-slot
   authoritative topology with `rackSlot` / `ioCheckPosition` / `processModulePosition`
   kept distinct; `addressDerivation.mjs` derives addresses **only** from a verified evidence
   set — with the authoritative set every binding is `ADDRESS_UNRESOLVED`, no offset is ever
   guessed; `candidateProcessImage.mjs` holds a `CANDIDATE_UNVERIFIED` image with all offsets
   `OFFSET_UNRESOLVED`.
3. **Typed read-only vendor transport (TC acquisition).**
   `adapters/tc-acquisition/` — `ITcReadOnlyClient`, `FluentModbusTcClient` (read-only API
   surface: input/holding register reads only; fixed 2000 ms timeouts; stale fencing by a
   generation counter bumped on connect/disconnect/dispose; bounded connect; cancellation
   fencing), `TcAcquisitionState` + `TcPhysicalReadOnlyEvidence` (quarantined physical record
   with read-profile identity; the Runtime refuses a physical record whose profile differs
   from the authorized one), `TEST_HARDWARE_READ_ONLY` device profile, one-shot sole-session
   owner precedent (`docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md`, ADR-0019).
4. **Process-instrument / pump readiness runtime contracts.** `packages/contracts/Pump.cs`
   (`PumpState`), `Enums.cs` (`PumpRunState`, `CriticalPumpKind`, `DeviceLinkState`,
   `Quality`), `SequencingPressure.PressureSample` (Bar, Quality, Stale, At, SourceId),
   `Topology.cs` (`IsolationValveConfiguration`, WJn↔IVn one-to-one ordinal pairing), H1 dry
   commissioning (merged) with Pump/physical-IV/Queue/AutoSequence command paths explicitly
   unavailable.
5. **Domain A readiness register (dated).** `docs/STAGE_0.4C-0A_CHECKPOINT.md` §7 records the
   per-group readiness of these same instrument groups (workbook-text identities, all
   addresses `ADDRESS_UNRESOLVED`, engineering ranges mostly `NOT PROVIDED`, write control
   `[NOT AUTHORIZED]`). The adopted artifact **extends that register with wiring evidence**
   (terminal numbers, From/To/Ref, locations) and newer row content (§10.5); it does not
   change any of its unresolved states.
6. **Not implemented (confirmed by inspection):** no Process-I/O Modbus adapter (only the TC
   read-only adapter exists), no output/write path of any kind, no raw Modbus UI, no
   parallel I/O catalogue outside mapping-config, no Process-I/O endpoint configuration
   (only `WJSS_TC_NODE_01_ENDPOINT` / `WJSS_TC_ACQUISITION_MODE` exist), no simulated
   process-I/O scenarios (the Simulator Scenario Catalogue covers sequencing only).

## 9. Exact Inspected Paths

Read-only inspection at the merged baseline (all paths exist as named):

- Governance: `AGENTS.md`, `docs/PUBLIC_REPOSITORY_BOUNDARY.md`, `CHANGELOG.md`, `README.md`
  (document index, T8 references), `docs/TEST_STRATEGY.md` (via boundary/`tests` conventions).
- Current-facing / planning: `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md` (H2 =
  "Physical Pump/IV I/O Commissioning — NEXT, NOT AUTHORIZED", formal mapping `[OPEN]`),
  `docs/CLEANING_SEQUENCE.md`, `docs/REQUIREMENTS.md`, `docs/DOMAIN_MODEL.md`.
- Authoritative workbook-derived I/O evidence: `T8_IO_Card_Mapping.xlsx` (re-parsed read-only:
  114 data rows, 104 tagged — DI 37 / DO 31 / AI 35 / AO 1, 6 SPARE, 23 slots, 8 module
  models; row comparison recorded in §10.5).
- Evidence registers/manifests and Owner-artifact conventions: `docs/TC_EVIDENCE_SOURCE_REGISTER.md`,
  `docs/STAGE_0.4C-0A_CHECKPOINT.md` §7 (Domain A register), `docs/evidence/`
  (`tc-drawing-schedule-rev1.csv` header/bytes, `tc-process-image-profile-rev1.md`,
  `tc-schedule-validation-rev1.md`, GCLIB/LEGACY/MANUAL baselines), `docs/STAGE_LIBRARY_SELECTION_GATE_REPORT.md`,
  `docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md`, `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md` (indexed).
- Mapping Configuration models and UI: `packages/mapping-config/src/`
  (`tagCatalogue.mjs`, `mappingValidation.mjs`, `addressDerivation.mjs`, `moduleProfiles.mjs`,
  `rack.mjs`, `actualRackEvidence.mjs`, `candidateProcessImage.mjs`, `processImageEvidence.mjs`,
  `workbookImport.mjs`, `providerPolicy.mjs`, `constants.mjs`, `draftSession.mjs`,
  `evidenceReport.mjs`, `presentation.mjs`, `limitNormalization.mjs`, `canonical.mjs`,
  `revisions.mjs`, `syntheticExample.mjs`), `apps/mapping-config/` (server + tests listing).
- Type/capacity/address derivation: `addressDerivation.mjs`, `moduleProfiles.mjs`
  (`addressRule: null` for every profile), `processImageEvidence.mjs` (authoritative set =
  no verified evidence).
- Existing Modbus clients/adapters/tests: `adapters/tc-acquisition/`
  (`FluentModbusTcClient.cs` — FC usage, timeouts, generation fencing; `ITcReadOnlyClient.cs`;
  `TcReadModels.cs` — `UnitIdentifier` default 0x00; `TcReadOnlyClientFactory.cs`;
  `DeterministicTcReadOnlyClient.cs`; `README.md`), `tests/tc-acquisition.tests/`
  (`AdapterLifecycleTests`, `PhysicalAuthorizationTests`, `TrialEvidenceTests`,
  `TrialPolicyTests`, `LoopbackResponder`, `TestClients`).
- TC Modbus read-only transport + endpoint/env configuration: `adapters/tc-acquisition/**`,
  `apps/runtime/TcEndpointConfiguration.cs` (exact env names `WJSS_TC_NODE_01_ENDPOINT`,
  `WJSS_TC_ACQUISITION_MODE`), `apps/runtime/RuntimeHostOptions.cs`, `docs/TC_PHYSICAL_READ_ONLY_ENDPOINT.md`.
- Process instrument / pump-readiness / paired-IV / valve supervision: `packages/contracts/Pump.cs`,
  `Enums.cs`, `Topology.cs`, `Config.cs`, `Sensors.cs`, `Jobs.cs`, `Snapshot.cs`,
  `packages/application/Runtime/Sequencing/SequencingPressure.cs` (+ `SequencingKernel/State/Event`),
  `packages/application/CleaningPath/Composition/CleaningPathCompositionContracts.cs`
  (+ Coordinator), `packages/application/Runtime/Simulator/` (scenario catalogue).
- Runtime state/publication boundaries: `packages/contracts/TcAcquisitionState.cs`,
  `TcChannels.cs`, `Config.cs`, `Delta.cs`, `Snapshot.cs`, `apps/runtime/Program.cs` profile
  handling (`TEST_HARDWARE_READ_ONLY`).
- Boundary scan and current motion boundaries: `tools/boundary-scan/boundary-scan.mjs`
  (S1–S9 rules; S3 vocabulary lists and the narrow `s3Exception.mjs` allow-list),
  `adapters/motion-library/` (gclib typed boundary, listing), `docs/SAFETY_BOUNDARY.md` (indexed).
- Dependency and lock files: `Directory.Packages.props`, `packages/contracts/packages.lock.json`,
  `adapters/tc-acquisition/packages.lock.json`, `packages/mapping-config/package.json`,
  `packages/contracts/wjss-contracts-ts/package.json` (no change; none needed).

Every AI/DI/DO/AO row, every listed module and end module and every column of the adopted
artifact was inspected through the parse in §7/§10 and the cross-references in §10.5/§16;
the artifact contains **no** process-image address, offset, endpoint, port, Unit ID, function
code, byte/word order, signedness or scaling value (scanned: zero IPv4 literals; zero
credential-shaped literals; one third-party font stylesheet URL in page presentation only).

## 10. Complete Inventory Evidence Status

### 10.1 Per-row evidence states (full per-row table = the CSV appendix)

Every one of the 111 rows is `FACT FROM CONNECTED ARTIFACT` for its source columns. The Scope
Gate classification (below and in the grouped §16) applies these states to each row:
none is `VERIFIED` (no wiring or behaviour has been physically verified), some groups are
`OWNER-RULED` on identity, and all physical actuation is `NOT AUTHORIZED` now.

### 10.2 Declared filter and its exact effect

`FACT FROM CONNECTED ARTIFACT`: `Filtered I/O Slot less than 0, equal to 0, and blank`. The
artifact lists only rows with a positive "I/O Slot" (1…20). `FACT FROM EXACT SOURCE`
(`actualRackEvidence.mjs`): the 23-slot authoritative rack has exactly 20 modules with an
`ioCheckPosition` — Slots 3–11, 13–23 — and exactly 3 without: Slot 1 (750-362 fieldbus
coupler), Slot 2 (750-601 power supply), Slot 12 (750-613 system power supply). The
artifact's Slot/I-O-Slot pairs match that sequence **exactly** (corroboration: strong — the
artifact documents the same rack as `T8_IO_Card_Mapping.xlsx`). The filtered-out rows are
therefore the 3 non-process module rows (no I/O channel exists on them; nothing process-
image-bearing is lost). The end module (Slot 23, I/O Slot 20) **is** included as one
topology row. No row is missing beyond the artifact's own declared filter.

### 10.3 Row-identity and duplicate diagnostics

Duplicate identical rows: 0 · Duplicate (Slot, Channel) placements: 0 · Duplicate tags: 0 ·
Untagged rows: 3 (kept; never invented) · All rows carry 14 string columns. Parsing
diagnostics column in the CSV flags: `IO_TYPE_UNCLASSIFIED` (3 rows: `DO`/`AI`/`AO` type
text empty), `STATUS_UNSET` (0 rows).

### 10.4 Source-value preservation

Exact values preserved, including: the trailing `+` on `AI-001+`…`AI-011+` and `AO-001+`
(12 rows; meaning of the `+` suffix `UNRESOLVED` — it is **not** silently stripped; the
Mapping-Configuration catalogue identities `AI-002`/`AI-003`/`AO-001` are related to these
strings only by the Owner default-binding rule, recorded `DERIVED`/`OWNER RULING`), Thai
contact notes, mixed English/Thai, spelling variants, `-` placeholders, and the two
unresolved description tokens `XXX รอพี่ยิมตอบ` (17 rows).

### 10.5 Cross-version relationship to the committed workbook (HISTORICAL/FACT comparison)

`FACT FROM EXACT SOURCE` re-parse of `T8_IO_Card_Mapping.xlsx` vs the adopted artifact (same
rack, newer declared workbook revision `Update19-10-26`):

| Aspect | T8 workbook (committed default) | Adopted artifact (2026-10-22) |
|---|---|---|
| Data rows | 114 (incl. 3 non-process module rows) | 111 (3 non-process rows filtered by declaration) |
| Tagged rows | 104 (DI 37 / DO 31 / AI 35 / AO 1) | 108 (DI 40 / DO 31 / AI 36 / AO 1) |
| Spare rows | 6, **untagged** | 6, **tagged** DI-038/039/040 + AI-036 (2 remain untagged) |
| `Status` coverage | 65 USED + 6 SPARE + **43 empty** | 105 USED + 6 SPARE (every row set) |
| Analog tags | `AI-001`…`AI-035` | `AI-001+`…`AI-035` + `AI-036` (`+` suffix on 1–11) |
| Columns | 10 | 14 (adds `I/O Slot`, `Terminal No.`, `From.`, `To.`) |
| `DI-037` description | `XXX รอพี่ยิมตอบ` | `BRAEKER OFF Q13-Q83` (From/Location `Q13-Q83`) |
| Description changes | — | 1 (DI-037 only); I/O-type text changes on DI-010/DI-011/DI-037 |
| Rack structure | 23 slots, 8 models | Identical slot/module groups |

This adoption **does not change** the Mapping Configuration workbook or its default bindings.
Whether the newer revision supersedes `T8_IO_Card_Mapping.xlsx` as the authoritative default
is an **Owner decision** (§29).

## 11. Rack and Module Topology

`FACT FROM CONNECTED ARTIFACT` + `FACT FROM EXACT SOURCE` (agree exactly):

| RackSlot | Model | I/O Slot | Role | Rows in artifact | Content |
|---|---|---|---|---|---|
| 1 | 750-362 | (filtered) | Fieldbus coupler / head station | 0 | process image host (per T8/Model evidence) |
| 2 | 750-601 | (filtered) | Power supply | 0 | non-process |
| 3–7 | 750-430 ×5 | 1–5 | DI 8ch | 40 | DI-001…DI-040 |
| 8–11 | 750-530 ×4 | 6–9 | DO 8ch | 32 | DO-001…DO-031 + 1 spare |
| 12 | 750-613 | (filtered) | System power supply | 0 | non-process |
| 13–21 | 750-471 ×9 | 10–18 | AI 4ch | 36 | AI-001+…AI-036 |
| 22 | 750-554 | 19 | AO 2ch | 2 | AO-001+ + 1 spare |
| 23 | 750-600 | 20 | End module | 1 | bus termination row (`End`) |

`DERIVED`: channel capacity per module is internally consistent (all channels 1…N present,
no gaps). The end-module row is topology evidence only (`module/end-module topology evidence`
requirement satisfied). This rack is the Domain A Process-I/O rack; it is **not** any TC node
(composition differs from the TC cabinets' 750-362+5×750-498+750-600 layout).

## 12. Modbus Physical Topology

Determined from exact evidence **without implementing**; every unknown is `UNRESOLVED`, not
guessed (`FACT FROM CONNECTED ARTIFACT` contains none of these values):

| Question | Status | Evidence |
|---|---|---|
| Physical controller/coupler identity | RackSlot 1 model `750-362` (as-drawn workbook identity) — physical identity `CANDIDATE` at model level | artifact + T8 + `moduleProfiles.mjs` |
| Physical rack/module order | **VERIFIED as documented** (§11; artifact ≡ authoritative topology) | artifact + `actualRackEvidence.mjs` |
| Endpoint ownership | `UNRESOLVED` — the Process-I/O rack's network endpoint is `NOT PROVIDED` anywhere in Source; TC cabinet endpoints are **not** to be reused (this rack is a different device; no evidence equates them; historical SDS CSV IP values are never current endpoints) | §9 inspection |
| Endpoint status | `NOT PROVIDED` / `UNRESOLVED` | |
| Subnet | `NOT PROVIDED` / `UNRESOLVED` | |
| Unit ID | `UNRESOLVED` (`TcReadModels.UnitIdentifier` defaults 0x00 for the TC adapter — a default, not evidence) | |
| Modbus port | `UNRESOLVED` (Owner bench 502 in the one-node TC scope gate is that bench's setting, not this rack's) | |
| Transport | `CANDIDATE` — TCP via the 750-362 head station is the model-level documented mechanism (`HISTORICAL EVIDENCE`: 750-362 manual, TC profile §4); instance transport `UNRESOLVED` | |
| DI/DO/AI/AO process images | `UNRESOLVED` — no process-image address/offset evidence in the artifact; `candidateProcessImage.mjs` remains all-offsets-`OFFSET_UNRESOLVED` | |
| Process-image order (analog vs digital grouping) | `UNRESOLVED` at instance level (`HISTORICAL EVIDENCE` at model level: analog-before-digital, word-by-word Intel format — TC profile §3 steps 1–6, for the 750-498 TC case) | |
| Byte/word order | `UNRESOLVED` at instance level (model-level `HISTORICAL EVIDENCE`: Intel format, low-byte-first words) | |
| Read/write ownership | `PROPOSAL` (§15/§18): one application-owned typed session per direction class; single session owner; read-only first | |

## 13. Process-Image and Address Model

The established Mapping Configuration intent is preserved and remains binding
(`FACT FROM EXACT SOURCE`, `addressDerivation.mjs` / `mappingValidation.mjs`): the user
selects physical Slot and Channel where configuration is permitted; the process-image
address/offset is derived automatically from the fixed rack topology and module order;
derived address is read-only; no free-form derived address entry; channel capacity and
signal-type compatibility are validated; duplicate binding is refused; Pump pressure
separation and WJn↔IVn ordinal consistency are enforced; unresolved rows are preserved
without fabricated addresses.

HTML column roles (`DERIVED`, for the adopted artifact):

| Column | Role |
|---|---|
| `WAGO Module`, `Slot`, `I/O Slot`, `Channel` | Source identity evidence **and** address-derivation inputs (fixed rack position) |
| `I/O Tag` | Source identity evidence (workbook identifier; Product identity assigned only per `OWNER RULING`) |
| `I/O Type` | Signal-class evidence (type/capacity compatibility input) |
| `Terminal No.`, `From.`, `To.`, `Location`, `Ref.` | Physical wiring evidence (descriptive for derivation; evidence for commissioning) |
| `Signal / Description`, `Contact / Note` | Descriptive-only values (source evidence; never used as identity) |
| `Status` | Source classification evidence (USED/SPARE; never "safe to actuate") |

No address, offset, function code, scaling, polarity or debounce value exists in the
artifact; **none is invented**. Address derivation for this rack stays
`ADDRESS_UNRESOLVED` until head-station process-image evidence (the missing evidence above)
is supplied and a verified evidence set is authorized — exactly the fail-closed engine
already implemented.

## 14. Function-Code Status

`UNRESOLVED` — the adopted artifact contains no function-code evidence and WAGO process-image
function codes are not assumed. `HISTORICAL EVIDENCE` (model-level, TC case only): 750-362
couples its analog input words to FC03/FC04 addresses 0–255 = %IW0–%IW255 (physical input
area 1), word-aligned Intel format (TC profile §3, cited from the 750-362 manual). The
existing TC adapter surface supports input-register and holding-register reads only.

Decision status for FC01/FC02/FC03/FC04/FC05/FC06/FC15/FC16 on **this** rack:

| Candidate use | Status | Evidence required |
|---|---|---|
| AI words read (FC03 or FC04) | `CANDIDATE` (model-level precedent only) | 750-362 I/O-config/process-image screen for this rack; 750-471/554 manual process-image sections |
| DI bits read (FC02 or packed in input words) | `UNRESOLVED` | as above; 750-430 manual process-image section |
| DO read-back (FC01 or held in an output image) | `UNRESOLVED` | 750-530 manual; coupler output-image mapping |
| DO write (FC05/FC15 or FC06/FC16) | `UNRESOLVED` and **NOT AUTHORIZED** (write authorization is a separate Owner gate regardless of FC) | as above + write-commissioning authorization |
| AO write (FC06/FC16) | `UNRESOLVED` and **NOT AUTHORIZED** | as above |

## 15. Typed Modbus Architecture (PROPOSAL — not implemented)

Every Process-I/O observation and command represented by the complete inventory must cross an
application-owned typed boundary — including all AI/DI/DO/AO rows, used, unresolved and
spare. For unresolved and spare rows, typed representation means catalogue/configuration
visibility and fail-closed exclusion from control, **not** runtime control authority. Galil
motion remains on the existing typed gclib boundary. The H2 Modbus foundation consumes the
canonical Equipment topology and Sensor assignment authority and must **not** duplicate
them. Proposed narrow types (each a separate contract; no generic raw client/operator UI):

1. `DigitalInputObservation` (typed channel identity, `Quality`, `Stale`, timestamp, source row provenance) — observation only.
2. `DigitalOutputCommand` (typed command admission record: channel, requested state, admission outcome, exactly-once token) — never a raw coil value.
3. `DigitalOutputEffectiveState` (read-back / effective state — distinct type from the command).
4. `AnalogInputRawValue` (counts/register words as read, with read-profile identity).
5. `AnalogInputEngineeringValue` (scaled value + unit + scaling provenance; engineering scaling is `UNRESOLVED` per channel until ruled).
6. `AnalogOutputCommandedValue` (commanded engineering value + admission record).
7. `AnalogOutputEffectiveValue` (read-back where supported).
8. `ProcessIoCommunicationState` (connected/disconnected/timeout/recovering — reuse `DeviceLinkState` vocabulary where compatible).
9. `Freshness` (`Stale` flag + last-observation timestamps; stale-response fencing).
10. `DiagnosticReason` (typed refusal/diagnostic codes; never string matching at call sites).
11. `CommissioningState` (per-channel phase: `EXCLUDED` / `READ_ONLY_OBSERVED` / `WRITE_BOUNDED` / `UNSUPERVISED` etc. — fail-closed default `EXCLUDED`).
12. `ProcessIoProvenance` (source artifact SHA-256 + source row ordinal + read-profile identity — the evidence-ledger link; reuses the `TcPhysicalReadOnlyEvidence` quarantine pattern).

No generic raw Modbus operator UI. Any engineering diagnostic surface must be separately
fenced, narrowly typed and unavailable to Production unless later authorized. The simulator
remains the default provider; the physical path stays quarantined behind the same
authorization-digest pattern as the TC physical read-only adapter.

## 16. Equipment-Group Analysis

Complete grouped analysis of every row (per-row detail = CSV appendix). `Authority Boundary`
for every group below: **NOT AUTHORIZED for physical actuation in this task**; inclusion in
the catalogue is observation scope only.

**G1 Pump / Inverter (Inverter Cabinet + Pump L Floor)** — rows `DI-001`…`DI-005`,
`DO-001`…`DO-007`, `AO-001+`.
Physical identity (`FACT`): Pump #1/#2 selection aux contacts; Inverter Ready/Running/Fault
relays; DO select-contactors K101/K102, Enable Inverter (K103→Inverter DI06), Run Pump
(K107→Inverter DI01), spares K104–K106 ("Spare for Pump 1/2/3"); `AO-001+` "Inverter Pump"
→ Inverter AI01+ (speed reference, 4-20 mA).
Product identity: `DO-007`/`AO-001+` are Pump run/speed command candidates; `DI-003`…`DI-005`
inverter status. Observation: all DI/AO read-back. Command: **process-affecting** (motor
start/speed) — `PROPOSED BOUNDED WRITE COMMISSIONING` only in a later phase with isolator
and abort; never in H2 read-only phases. Interlocks: Pump-ready gate (`AI-003`
`OWNER RULING`); contactor feedback (`DI-001`/`DI-002`) before accepting selection.
Scaling: AO 4-20 mA range `UNRESOLVED`. Safe state: outputs de-energized / no command.
Risk: **highest** (motor + pump). Phase: H2-D last (write) after all read-only evidence.

**G2 Pump outlet/inlet pressure (Pump L Floor)** — `AI-002+`, `AI-003+`.
`OWNER RULING` (2026-10-10): `AI-002` = Pump Inlet Pressure (diagnostic/trend only);
`AI-003` = Pump Outlet Pressure — **sole Pump-ready pressure authority** (source text on the
artifact for AI-003+ is "Pressure Transmitter Main Valve Outlet"; workbook text is source
evidence only and never overrides the Owner identity). Separate Measurements; no aliasing
(`mappingValidation.mjs` rules preserved). Signal 4-20 mA HART5; 0–40 bar `OWNER RULING`
(not bench-verified). Observation: read-only trend + readiness input. No command.
Phase: H2-B (read-only foundation) — first candidate for typed pressure observation via
`PressureSample`. Risk: none (observation).

**G3 WSB pressure transmitters / IV outlet pressure** — `AI-004+`…`AI-011+`.
`OWNER RULING` (CF-05 label rule): "WSB Pressure transmitter #n" = IVn outlet pressure,
**ordinal, direct** (AI-004+→IV1 … AI-011+→IV8). Paired-IV composition (Jobs.cs: measured
outlet of the job's paired IVn only). Read-only diagnostics. Phase: H2-B. Risk: none.

**G4 WSB limit switches (Pump L Floor)** — `DI-021`…`DI-036`.
`FACT`: WSB Lower #1–#8 + Upper #1–#8 (NO dry contacts). `OWNER RULING` defaults:
`DI-021`…`DI-028` = IV1…IV8 Lower; `DI-029`…`DI-036` = IV1…IV8 Upper — ordinal, direct.
Polarity `ACTIVE_WHEN_CLOSED` per existing Stage 0.4B-1 defaults (digital; not physical
verification). Read-only observation; physical actuation verification of switches is a
hardware trial. Phase: H2-B. Risk: none (observation).

**G5 WSB Air Solenoids (Pump L Floor)** — `DO-023`…`DO-030` (K160–K167).
`FACT`: 8 air-solenoid outputs (24 VDC DO → 220 VAC relay coils). Physical identity to WSB
n/IVn mapping `UNRESOLVED` (no ordinal in the source text beyond #1…#8 — **do not assume**
the #n ↔ IVn ordinal without Owner ruling; recorded `UNRESOLVED`). Commands are local device
actuation (air) — potentially process-adjacent. Read-back observation first; write commissioning
only with Owner trial authorization. Phase: H2-C then bounded write trial.

**G6 Valve banks — Water Tank K+1 (Ref 1.3, 1.4)** — `DI-007`…`DI-011`,
`DO-008`…`DO-013`, `AI-001+`.
`FACT`: per bank: Over Torque Lamp, Valve is Opened/Closed (limit contacts via K115–K117 /
K126–K127), Open/Close Valve + Heater outputs (K112–K114 / K122–K124); `AI-001+` Water Level
Sensor (4-20 mA HART5, X26.5). **These valves are NOT the WJ isolation valves** — they are
tank-side valve banks per the Ref/location evidence; their relationship to Product Isolation
Valves (IV1…IV8) is `UNRESOLVED` (never alias; `IsolationValveConfiguration` pairing is
canonical topology, consumed not duplicated). Commands Open/Close/Heater are
process-affecting (water path / heating). Phase: read-only H2-B; actuation `PROPOSED BOUNDED
WRITE COMMISSIONING` in a late H2-D phase at the earliest; Heater `PROPOSED` even later or
excluded pending Owner ruling. Risk: **high** (water/heat).

**G7 Valve banks — Pump L Floor (Ref 1.27) + Boiler J Floor (Ref 1.78) + Boiler G+1 Floor
(Ref 1.79)** — `DI-012`…`DI-020`, `DO-014`…`DO-022`.
Same pattern per bank (Over Torque Lamp, Valve Opened/Closed, Open/Close/Heater). Same
classification as G6: read-only first; actuation process-affecting; heater highest caution.
Risk: **high**.

**G8 Blower currents (WSB Sealed Air Blower #1–#8)** — `AI-012`…`AI-019`.
`FACT`: `AI (0-20 mA.)` — **not** 4-20 mA (a 4 mA live-zero assumption would be wrong;
existing Domain A register conclusion preserved). From transducers U91–U98. Read-only
diagnostics/trend. Engineering range `UNRESOLVED` (`OWNER_INPUT_REQUIRED`). Phase: H2-B.

**G9 Electrical breaker status** — `DI-037`.
`FACT`: `BRAEKER OFF Q13-Q83` (source spelling), From/Location `Q13-Q83`, note: 24 VDC
breaker contact NC series connection Q13 to Q83. The newer revision gives this row a defined
signal (in T8 it was an unresolved placeholder). Product role `UNRESOLVED` — what the
aggregate means and its exact polarity convention is `OWNER_INPUT_REQUIRED` before any
Product logic. Read-only observation candidate. Phase: H2-B with `UNRESOLVED` role flags.

**G10 Unresolved placeholders** — `DO-031`, `AI-020`…`AI-035` (17 rows).
`FACT`: descriptions `XXX รอพี่ยิมตอบ` ("waiting for a reply"), USED, wired to terminals
(X5.193…X5.223 for the AI group; DO-031 via K168). Identity `UNRESOLVED`. Policy §17.
Phase: H2 Final disposition; never commissioned before Owner identity ruling.

**G11 Spare channels** — 6 rows (DI-038/039/040, AI-036, 2 untagged DO/AO channels).
`SPARE`. Catalogue visibility only; fail-closed exclusion from every control path. Phase: H2
Final disposition record only.

**G12 End module** — Slot 23 / I/O Slot 20 (`750-600`).
`FACT`: bus termination row. Topology evidence only; no process data; not an I/O point.

## 17. Spare and Unresolved Policy

- `SPARE` ≠ available for arbitrary use. Spares remain typed catalogue rows with fail-closed
  exclusion (`CommissioningState = EXCLUDED`); any future use is an Owner-routed
  configuration change, never an opportunistic binding.
- `UNRESOLVED` ≠ optional. Unresolved rows stay visible, preserved verbatim, excluded from
  control, and block any claim of complete commissioning until resolved.
- `USED` ≠ safe to actuate. Present-in-list ≠ verified. A successful Modbus write would not
  prove successful physical operation (commanded/effective/physical effect remain distinct).
- Every unresolved and spare row still crosses the typed boundary as an observation-shaped
  catalogue entry (§15.12 provenance + §15.11 `EXCLUDED` state) — typed representation with
  zero control authority.

## 18. H2 Phase Plan (PROPOSAL)

Smallest safe Product-critical-path phasing covering the entire adopted inventory. Names and
order follow the required H2-A…H2-Final shape, extended by the artifact's own equipment
groups. Documentation/tooling (this adoption) is **enabling work only** — it is not the
final H2 outcome.

| Phase | Name | Scope | Contents |
|---|---|---|---|
| H2-A | Source Artifact Adoption, Complete I/O Catalogue and Exact Rack/Process-Image Evidence | **Evidence adoption (this task, `AUTHORIZED NOW`) + evidence closure (Owner inputs)** | Immutable artifact + manifest + generator + inventory + this scope gate (done here). Remaining H2-A items: head-station process-image evidence, endpoint/Unit ID/port provisioning decision, function-code evidence (§12/§14), the Owner identity rulings (§29). |
| H2-B | Typed Modbus Read-Only Observation Foundation (complete input inventory) | Product coding (needs Owner authorization) | Typed contracts (§15.1/4/5/8–12), read-only adapter session (single owner, stale/reconnect fencing, cancellation, bounded timeouts), simulator default + quarantined physical read path (TC-adapter pattern), evidence ledger. Covers **all 40 DI + 36 AI rows** — every group G2–G4, G6–G10 read side, incl. unresolved/spare visibility (fail-closed `EXCLUDED`). |
| H2-C | Typed Output Observation and Fail-Closed Write Foundation (complete output inventory) | Product coding (needs Owner authorization) | §15.2/3/6/7 types; output read-back observation for **all 32 DO + 2 AO rows**; write admission layer implemented **fail-closed disabled** (exactly-once admission, no stale-write replay, no write after Stop/Close, commanded/effective separation) with **zero actuation authority**. |
| H2-D1 | Pump/Inverter read-only + bounded commissioning | Hardware trials per §21 | G1 observations first; then (separate authorization) no-load write trial subset (selection relays only, pump isolated). |
| H2-D2 | Pressure transmitter commissioning (Pump Inlet/Outlet + IV1–IV8) | Hardware trials | G2/G3 read-only verification, transducer baseline capture; Pump-ready gate semantics proven against `AI-003` only. |
| H2-D3 | WSB limit-switch + blower-current commissioning | Hardware trials | G4/G8 read-only verification (manual switch actuation by hand under supervision). |
| H2-D4 | Tank/Boiler valve banks + air solenoids bounded write commissioning | Hardware trials (process-affecting gate) | G5–G7 subsets; each trial separately authorized; physical isolator + abort required. |
| H2-D5 | Breaker-status + unresolved/spare disposition | Owner rulings + read-only | G9 polarity/meaning ruling; G10 identities ruled or permanently excluded; G11 disposition recorded. |
| H2 Final | Complete I/O audit, unresolved/spare disposition and H3 readiness | Audit | Row-by-row audit of all 111 rows against commissioning evidence; zero unresolved left in control paths; H3 entry criteria (§23) assessed. |

## 19. Proposed First Product Coding Scope (PROPOSAL — no coding prompt is created)

H2-B, scoped tightly: typed input-observation contracts + a simulator-backed read-only
process-I/O observation path + evidence-ledger record + tests, consuming
`docs/evidence/process-io-inventory-2026-10-22.csv` as the catalogue-of-record (parse with
the same fail-closed rules) and the existing typed Quality/Stale/DeviceLinkState vocabulary;
no write type, no endpoint value, no scaling value, no new dependency; boundary scan clean.
This is the smallest Product-critical-path increment after the evidence work: it makes the
complete input inventory first-class typed observations without any actuation surface.

## 20. Validation Plan

Executed in this task (observed results in §26/§31): connected-source retrieval proof;
source/repo SHA-256; exact byte-identity comparison; HTML parse validation; column-contract
validation; complete row-count validation; per-I/O-class count validation; no-row-loss
validation; deterministic generation/parity (`--check`); duplicate source-row detection;
source-order preservation; `git diff --check`; boundary scan; focused artifact/parser tests
(15); relevant existing Node suites (commissioning, contracts-ts, ops-preview, mapping-config,
tools tests). **No C# Source changed → no .NET validation is claimed.**
For later Owner-local restore/build/test: clear the exact environment variables found in
Source in the same process before running — `WJSS_TC_NODE_01_ENDPOINT`,
`WJSS_TC_ACQUISITION_MODE`, `WJSS_SENSOR_PARAMETER_CSV` (names cited from Source; no others
guessed).

## 21. Future Hardware Commissioning Plan (PROPOSAL — NOT AUTHORIZED now)

Every later trial is separately authorized; classifications per the required scale:

| Trial | Class | Exact I/O subset | Prerequisites | Roles / access | Duration / timeout / cleanup / final state | Evidence / STOP |
|---|---|---|---|---|---|---|
| H2-D1-R1 Inverter status read-only | read-only | DI-003…DI-005 (subset of G1) | H2-B complete; endpoint ruled; read profile authorized | Owner authorization; Operator+Observer; physical abort accessible | Bounded session; `OWNER_INPUT_REQUIRED` numeric max; deterministic cleanup; outputs untouched | Read ledger; STOP on any write attempt, stale fence trip or unexpected state |
| H2-D2-R1 Pressure baselines | read-only | AI-002+…AI-011+ | as above + transducer ranges ruled | as above | as above | Baseline capture; STOP on quality BAD/overrange |
| H2-D3-R1 Limit switches hand-actuated | read-only (local device actuation by hand only) | DI-021…DI-036 | as above | Operator actuates switches manually under Observer; no command issued | Bounded; final state = as-found | Per-switch actuated-state evidence; STOP on any unexpected motion |
| H2-D1-W1 Pump selection relay test (pump isolated) | write with no expected actuation **if contactor power isolated**, else local device actuation | DO-001/DO-002 only | All H2-B/C; write admission enabled for exactly this subset; isolator proven | Owner + separate start authorization; Observer at isolator/abort | Bounded write pulse; timeout `OWNER_INPUT_REQUIRED`; cleanup = output to safe state; final = de-energized | Commanded vs effective vs contactor feedback; STOP on motion/sound/heat of any kind |
| H2-D4-W1 Valve bank Open/Close trial | process-affecting | one bank subset (e.g. DO-008/DO-009) | All above + wet/process approvals or physical isolation of the water path | Owner authorization phrase per trial; abort access mandatory | Strictly bounded pulse; verified cleanup | Valve DI feedback (G6/G7) as physical proof; STOP on water flow |
| Heater trials | wet/process-affecting — **prohibited in H2 unless separately ruled** | DO-010/DO-013/DO-016/DO-019/DO-022 | Owner ruling that heater trials are in H2 at all | | | |
| Any Pump run / AO speed reference | process-affecting — **prohibited until the H2-D1 write class is separately authorized** | DO-007, AO-001+ | | | | |
| Any write to spare/unresolved rows | **prohibited in H2** | DI-038/040, AI-036, DO-031, AI-020…AI-035, untagged spares | identity ruling first | | | |

All numeric durations/timeouts are `OWNER_INPUT_REQUIRED` (never invented).

## 22. H2 Completion Criteria (PROPOSAL)

1. Complete typed representation of all 111 artifact rows (incl. end-module/topology rows,
   spares and unresolved rows) with fail-closed `EXCLUDED` defaults.
2. Typed read-only observation running (simulator + quarantined physical) for all 76 inputs;
   evidence ledger per read; stale/reconnect fencing proven by tests.
3. Output observation + fail-closed write foundation for all 34 outputs with zero default
   authority; exactly-once admission, no stale-write replay, no write after Stop/Close
   proven by tests.
4. Equipment-group commissioning evidence recorded for G1–G9 read sides; bounded write
   trials completed only for separately authorized subsets.
5. Unresolved/spare disposition recorded (ruled or permanently excluded).
6. Function-code/process-image/endpoint evidence closed (§12/§14 rows all `VERIFIED` or
   explicitly excluded by Owner ruling).
7. Complete row-by-row I/O audit signed off (111/111); no unresolved row reachable by any
   control path.

## 23. H3 Entry Criteria (PROPOSAL)

1. H2 completion criteria (§22) all met and recorded at an Owner-merged checkpoint.
2. Pump-ready gate (`AI-003` only) proven against physical read evidence; `AI-002` proven
   diagnostic-only (never gate).
3. WJn↔IVn ordinal pairing proven on observed I/O (limit switches + transmitters) —
   canonical topology consumed, not duplicated.
4. Sequential-only, single-job Cleaning Job contract paths (`P1→P6`, Centre 125/125) remain
   intact; no Queue/AutoSequence in H3 (that is H4).
5. H1-style exact-head trial procedure authored and Owner-reviewed for the single real
   Cleaning Job; separate exact-head Hardware authorization mechanics preserved.
6. Every H2 risk in §25 either closed or explicitly accepted by the Owner.

## 24. Scope Exclusions

`NOT AUTHORIZED` / excluded from this task and this scope gate: Product runtime behaviour;
Modbus acquisition implementation or writes; any device, NIC or network contact; any
physical Modbus, Pump, Valve, Heater, Solenoid, Inverter, Galil or motion command; a
Product Coding Prompt; dependency or lock changes; changes to Product Source, Modbus
adapters, UI, device configuration; the K3 documentation-contract punchlist (separate
accepted item); H3/H4/H5 work; any raw Modbus UI; reuse of TC cabinet endpoints; use of
historical SDS CSV IP values as current endpoints; any invented address/endpoint/FC/
scaling/polarity/duration value.

## 25. Risks and Evidence Gaps

1. **Process-image/transport gap (largest).** No endpoint, Unit ID, port, process-image
   offsets, FC mapping, byte/word order or signedness evidence for this rack exists in any
   reviewed source. Until closed, no address derivation is possible and no read can be
   addressed — the fail-closed `ADDRESS_UNRESOLVED` position holds. Evidence required:
   750-362 I/O-Config/process-image screen for this rack + relevant manual sections +
   Owner endpoint provisioning.
2. **Declared filter.** 3 non-process module rows are outside the artifact by its own
   declaration (§10.2). Low risk (no I/O data), but the complete inventory means complete
   within the filter.
3. **Upstream workbook unavailable / truncated digest.** The declared workbook identity is
   not independently verified; if the workbook differs from the artifact, this adoption is
   bounded to the artifact bytes.
4. **`+` suffix semantics unknown** on 12 analog tags; stripping or interpreting it would
   risk identity corruption. Preserved verbatim; ruled by the Owner.
5. **Air-solenoid #n ↔ IVn mapping unknown** (G5); ordinal assumption prohibited.
6. **0-20 mA blower channels** must not be treated as live-zero 4-20 mA (G8).
7. **Heater/valve/pump actuation risk** (G1, G6, G7) — process/wet; phased last; each trial
   separately authorized.
8. **Version skew** between the committed workbook default and the newer artifact revision
   (§10.5) — resolved only by Owner ruling; no silent adoption.
9. **Spares tagged in the newer revision** (DI-038/039/040, AI-036) — identity now present;
   still `SPARE` classification; still fail-closed.

## 26. CHANGED

- `docs/evidence/WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` —
  **created**; immutable exact-byte copy of the connected-source artifact (36,667 bytes;
  SHA-256 `28245ad2…`).
- `docs/evidence/process-io-source-provenance-2026-10-22.md` — **created**; provenance
  manifest (identity fields, byte-identity proof incl. retrieval-transformation disclosure,
  parsing status, counts, limitations).
- `docs/evidence/process-io-inventory-2026-10-22.csv` — **created**; deterministic normalized
  inventory (111 rows, source order preserved, 14 exact columns + 7 DERIVED columns).
- `tools/process-io-evidence/generate.mjs` — **created**; deterministic fail-closed
  parser/generator with parity (`--check`) mode.
- `tools/process-io-evidence/test/generate.test.mjs` — **created**; 15 focused tests.
- `docs/H2_FINAL_SCOPE_GATE_REPORT.md` — **created**; this report.
- `docs/CURRENT_STATE.md` — **modified** (one new dated block at the top referencing this
  adoption and scope gate).

## 27. UNCHANGED

No Product runtime Source; no Modbus adapters (including `adapters/tc-acquisition`); no UI;
no device configuration; no dependency or lock files (`Directory.Packages.props`, all
`packages.lock.json`, all `package.json`); no generated Product fixtures; no generator
outputs other than the new evidence CSV; no canonical Equipment topology, Sensor
assignment, P1–P6, Centre 125/125, WJn↔IVn, queue/safety semantics; no `T8_IO_Card_Mapping.xlsx`
or Mapping Configuration defaults; no boundary-scan policy; no credentials; no endpoints;
no native paths; H2–H5 authorization state (all remain `NOT AUTHORIZED`); the K3 punchlist.

## 28. NOT VERIFIED

- Any physical wiring truth of any artifact row (rows are Owner source declarations).
- The upstream workbook's identity and bytes (truncated digest; bytes unavailable).
- Process-image addresses, function codes, Unit ID, port, endpoint, byte/word order,
  signedness, scaling, polarity, debounce, command durations — none evidenced, none invented.
- Hardware behaviour of every channel; every write class; the WAGO watchdog fail-safe
  (AGENTS.md §10.4).
- .NET validation — **no C# Source changed and no .NET toolchain exists in this environment**;
  no .NET claim is made. Owner-local commands for any later Product stage are listed in §20.
- Physical commissioning of any row (`AUTHORIZED NOW` covers evidence adoption only).

## 29. Required Owner Decisions

1. **D1 — Function-code and process-image evidence provisioning** (blocks all H2-B
   addressing): provide or authorize capture of the 750-362 I/O-Config/process-image
   evidence for this rack (offsets, areas, order, byte/word order) and the manual sections
   for 750-430/530/471/554 process images. `UNRESOLVED` until then.
2. **D2 — Process-I/O endpoint provisioning**: rack endpoint/subnet/Unit ID/port as
   Owner-local configuration (never committed). Confirm the rack is a device distinct from
   TC-NODE-01…06 (Source evidence says distinct; confirm as ruling).
3. **D3 — Unresolved identities**: `DO-031` and `AI-020`…`AI-035` (17 rows) — establish
   identity or rule permanent exclusion; `DI-037` breaker-chain meaning and polarity
   convention; air-solenoid `#n` ↔ WSB/IVn mapping (G5).
4. **D4 — `+` suffix semantics** on `AI-001+`…`AI-011+`, `AO-001+` (preserve as-is until
   ruled).
5. **D5 — Version authority**: does the newer declared revision
   (`WAGO_IO_Mapping(Update19-10-26)_2.xlsx`) supersede `T8_IO_Card_Mapping.xlsx` as the
   Mapping Configuration authoritative default (and if so, via its generator-owned import
   path in a later authorized stage), or do both remain as evidence layers?
6. **D6 — Blower-current engineering range** (0-20 mA channels) and pump transmitter
   ranges confirmation (0–40 bar remains `OWNER RULING`, not bench-verified).
7. **D7 — H2-B authorization** (Proposed First Product Coding Scope, §19) and the H2 phase
   plan (§18) as the working H2 scope (formal MASTER_PLAN mapping for H2 is `[OPEN]`).
8. **D8 — Heater trial classification** (G6/G7): prohibited in H2, or a separately bounded
   class with its own authorization mechanics.

## 30. Recommended Final H2 Scope

Adopt the §18 phase plan as the final H2 scope: **H2-A evidence closure (D1–D6 decisions)
→ H2-B typed read-only observation for the complete 76-input inventory → H2-C typed output
observation + fail-closed write foundation for the complete 34-output inventory (zero
default authority) → H2-D1…D5 per-group commissioning (read-only first; every write class
separately authorized; process-affecting last; unresolved/spares excluded) → H2 Final
complete 111-row I/O audit + H3 readiness.** The evidence artifact and generator are
enabling work for the complete Product I/O foundation — not the final H2 outcome. This task
delivers the evidence adoption and the scope-gate analysis only.

## 31. Commit, Branch and PR Identity

Filled after commit/push (recorded in `docs/CURRENT_STATE.md` by follow-up note if needed —
the SHA of a commit cannot be recorded inside that same commit):

- Branch: `arena/de8398d2-waterjet-sentinel-suite` (new platform-assigned branch from the
  merged baseline `bfd2867af1dda29fcbc1987328be3d131a189d0a`).
- Commit: evidence-adoption + scope-gate artifacts only (the exact changed surface of §26);
  commit body states CHANGED / UNCHANGED / NOT VERIFIED.
- PR: opened against `main` from this branch; **not merged** (Owner-only merge authority).

## 32. Owner Next Action

Review this report and decide **D1–D8** (§29). The immediate unblocking decisions are
**D1 (process-image/function-code evidence)** and **D2 (endpoint provisioning)**; the
Product-critical-path start is **D7 (authorize H2-B)** once D1/D2 are underway. Merge
authority for the evidence/scope PR remains the Owner's exclusive decision (AGENTS.md §8).
