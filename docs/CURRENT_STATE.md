# Current State — WaterJet Sentinel Suite (WJSS)

**Document status:** The verified state below is `[APPROVED]` as a factual record.
Stage status wording and the open-item list were corrected by the Owner-confirmed Stage 0.1
documentation review punchlist, updated by the approved Stage 0.2 Scope Gate — *Technology and
Solution Architecture Decision*, refined by the Owner-requested Stage 0.2 documentation review
punchlist, and updated for Stage 0.2 Owner acceptance and the approved Stage 0.2.1A spike.
Open items are `[OPEN]` and must not be resolved by assumption.

This document answers one question: *what is actually true right now, with evidence?*
Nothing in this repository may contradict it. If something does, the discrepancy is reported
rather than silently resolved.

---

## 1. Status legend

Used throughout the documentation:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Approved by the Owner in an approved Stage Gate. Binding on future work. |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review. Binding. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. **Not** approved behaviour. |
| `[NOT VERIFIED]` | Not confirmed by bench test, field test, measurement, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Currently prohibited. The activity, write, or access must not be performed. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed in the meantime. |

Stage implementation status uses a separate vocabulary: `SUBMITTED FOR OWNER REVIEW`,
`CHANGES REQUESTED`, `IN PROGRESS`, `OWNER ACCEPTED`, `NOT MERGED`, `MERGED`. Only the Owner
may record `OWNER ACCEPTED` or `MERGED`.

## 2. Stage and approval status

| Item | Value |
| --- | --- |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — merged to `main` through PR #1 |
| Stage 0.1 Owner manual review | **Recorded as complete by the Owner** in the approved Stage 0.2 Scope Gate, which states that the previous Stage branch completed its role and was merged through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** — *Technology and Solution Architecture Decision* |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED / MERGED** — merged to `main` through PR #2; source checkpoint `5bcf1b33f924ab30590a55736676200115874fa1`; merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73` |
| ADR-0006 to ADR-0013 | **ACCEPTED** as architecture direction. Accepted does **not** mean implemented; items marked `[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` inside them keep those markers |
| Stage 0.2.1 (two-candidate spike as originally proposed) | Not started; superseded by the narrower Stage 0.2.1A |
| Stage 0.2.1A Scope Gate and Coding Start Gate | **APPROVED** — *React UI and Runtime Feasibility Spike* (synthetic) |
| Stage 0.2.1A implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT — MERGED** (PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; formerly OPEN — READY FOR OWNER MERGE). Checkpoint `114c0761` accepted by the Owner as the Development baseline (UI, synthetic AutoSequence controls, GlobalQueue presentation, critical Pump modal, Mandatory Safe Return behaviour) — see §12.11; [results §0G](spikes/stage-0.2.1a-results.md#0g-owner-local-final-review-and-stage-021a-closeout-documentation-only). *History:* branch `arena/01a108d8-waterjet-sentinel-suite`, PR #3 to `main` **OPEN**; Sensor map corrected to the Owner's 106-location domain (`935973e6`); fullscreen Operations layout refined under the Owner-approved Design Addendum (1920 × 1080, Edge F11); Operations readability refined after the Owner screenshot review (sensor-cell zones, typography scale, desaturated process colours separated from alarm, card hierarchy); final Owner UI punchlist on `4129687a` (self-hosted Google Sans, marker in the value row, expanded identity, legend clipping, Water Jet display terminology, mixed queue sources, opt-in synthetic test controls) on `23f48daa`; GlobalQueue semantics corrected under the Owner domain correction (bounded ready-only synthetic queue, head-only atomic dispatch; previous synthetic queue behaviour SUPERSEDED, not eligible for production promotion) with the `READ-A` value-clipping and `WJ-A` test hotfixes ([results §0D](spikes/stage-0.2.1a-results.md#0d-globalqueue-semantics-correction-and-head-only-dispatch-stage-021a)); synthetic critical Main Pump handling (High Critical; AutoSequence `CRITICAL_SUSPENDED`; blocking critical modal) and Mandatory Safe Return for every Job outcome under the Owner critical Pump decision — SYNTHETIC PROOF ONLY, production safety NOT VERIFIED ([results §0E](spikes/stage-0.2.1a-results.md#0e-critical-main-pump-handling-and-mandatory-safe-return-stage-021a)) |
| Stage 0.2.1A Owner-local testing (installed Edge, Windows 11) | Checkpoint `dd20a8bd` (superseded 104-location map): **PASS**. Windows 11, Node v24.20.0, npm 11.19.0, Git 2.55.0.windows.5, installed Edge; `npm ci`, typecheck, Vitest 53 / 53, build, harness 14 / 14, and Edge E2E 15 / 15 all PASS. That evidence **does not validate** the corrected Sensor map, the fullscreen layout, or the readability refinement; Owner-local Edge E2E at `4129687a`: **25 / 25 PASS** (≈ 1.3 min, Owner-reported). At `23f48daa`: 34 selected · 25 passed · 2 failed (`READ-A`, `WJ-A`) · 7 not run (Owner-reported). The GlobalQueue-correction checkpoint (35-test selection) was not re-run separately. At the critical Pump / Safe Return checkpoint `81c87a44`: 42 selected · 32 passed · 1 failed (`S11/S12`, Dirty Score drifted between samples) · 9 not run (Owner-reported). Final spike closeout checkpoint `114c0761` (48-test selection, `SEQ-B`..`SEQ-G`, deterministic `S11/S12`, 18-step manual sequence): Owner-local final Edge gate **PASS** and Owner manual review **PASS** (Owner-reported, 2026-10-07; detailed counts and screenshots were not supplied to the Agent). Owner-local interrupted overnight observation of `ea23bc58`: **COMPLETED** — not a controlled benchmark ([results §0B.1](spikes/stage-0.2.1a-results.md#0b1-owner-local-interrupted-overnight-observation-recorded-as-reported-by-the-owner)) |
| Stage 0.2.1A controlled 15-minute Owner-local observation | **Waived as a Stage 0.2.1A merge blocker** (Owner decision, 2026-10-07) — not run |
| Stage 0.2.1A Owner manual review (`114c0761`) | **PASS** (Owner-reported, 2026-10-07) |
| Stage 0.2.1A 60-minute Owner-local run | **Waived as a Stage 0.2.1A merge blocker** (Owner decision, 2026-10-07) — not run |
| Stage 0.2.1A merge | **MERGED** — PR #3 executed by the Owner (merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; the Agent never merges) |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07); Production transport and chart library remain `[OPEN]` |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Main Development Scope Gate | **PENDING** for 0.3B+ — 0.3A proceeds under the Owner's Option-C amended gate only |
| Stage 0.3 | **NOT AUTHORIZED** as a whole; **Stage 0.3A-1 source checkpoint** authored under the amended gate (see §11.4, §12.12) |
| Stage 0.3A-1 .NET validation | **PASSED (Owner-local, 2026-10-07)** — Release build 0 warnings / 0 errors, full suite 61/61, TypeScript 24/24, parity 7/7, boundary S1–S9 clean; PR #4 READY FOR OWNER MERGE (not merged). See §12.22 and the [runbook](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) verdict; Arena claims no .NET execution of its own |
| Stage 0.3A-2 | **AUTHORIZED** — Owner instruction 2026-10-07, three checkpoints A → B → C on one branch, SIMULATOR profile only. **Checkpoint A OWNER-LOCALLY VALIDATED (2026-10-07)**: Release build 0 warnings / 0 errors, full .NET suite **101/101**, locked restore PASS, boundary scan S1–S9 clean, genuine lock-refresh commit `55d3b8b` (see §11.5, §12.23, §12.24). **Checkpoint B OWNER-LOCALLY VALIDATED (2026-10-07)** at `cfa6d4a`: Release build 0 warnings / 0 errors, full .NET suite **129/129**, locked restore PASS, lock drift NONE, boundary S1–S9 clean (see §11.6, §12.25). **Checkpoint C (read-only Runtime API + development Runtime Inspector)** — Owner-authorized and **delivered as source only** (C1+C2 at `06aca79`; C3 Inspector page + runbook §14 in the commit carrying this record): **NOT COMPILED and NOT EXECUTED in Arena**, no Owner-local result recorded yet, **NOT MERGED** (see §11.7, §12.26) |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

The Owner accepted Stage 0.2 and it was merged through PR #2. Stage 0.2 was a documentation
and architecture-decision checkpoint only. Acceptance of ADR-0006 to ADR-0013 records
architecture direction; it is not implementation proof.

Approval of the Stage 0.2.1A Scope and Coding Start Gates authorises a synthetic, removable
React feasibility spike under `spikes/ui-runtime-react/`. It is **not** acceptance of the spike
result and **not** a UI framework selection. **Owner decision, 2026-10-07:** after the Owner-local
final Edge gate and the Owner manual review of `114c0761` passed, the Owner accepted the UI, the
synthetic AutoSequence controls, the GlobalQueue presentation, the critical Pump modal and the
Mandatory Safe Return behaviour as the **Development baseline**, recorded Stage 0.2.1A as
**COMPLETE FOR DEVELOPMENT CHECKPOINT**, and selected **React as the Primary UI Framework**. Stage
0.2.1A is **not merged** until the Owner merges PR #3, and this acceptance is not a Production
safety, stability, WebView2, kiosk, Modbus or hardware validation. The spike uses synthetic data
only: no device was contacted, no Production value is used, no Product directory exists, and
no Production Write was performed or authorised.

**Owner domain correction (Stage 0.2.1A).** The Owner corrected the physical baseline from 104
Sensor locations / 208 Thermocouple channels (Left 24, Rear 28, Right 24, Front 28) to **106
Sensor locations / 212 Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). The
Sensors sit in an 18-column × 6-row logical matrix with two Cannon equipment slots, logical I7
(Rear) and I16 (Front), which are not Sensors. See
[`DOMAIN_MODEL.md` §2.2.1](DOMAIN_MODEL.md#221-logical-sensor-matrix-and-cannon-slots-owner-confirmed).
This is a domain correction, not a runtime failure. Where 104 / 208 still appears in a
historical record below, it is the **superseded baseline**.

The synthetic process workload of the spike is deterministic for the same seed, scenario
timeline, synthetic configuration, and code revision. Cryptographic run and scenario tokens are
non-deterministic but do not affect process values, scenario ordering, the device latency
sequence, classification, or revision behaviour. Identical wall-clock timing is not claimed.

The Stage 0.1 process-deviation record in section 11.1 is retained unchanged.

## 3. Application version

| Item | Value |
| --- | --- |
| Application version | **NOT ESTABLISHED** |
| Runtime release | **None exists** |
| Documentation versioning policy | `[OPEN]` — no scheme is adopted |
| Former `0.1.0` label | A documentation-only working label. **Not** a product version, not a release. Retained in [`../CHANGELOG.md`](../CHANGELOG.md) only for historical traceability. |

No governance rule requires a documentation stage to advance a version number.

## 4. Repository state

| Item | State |
| --- | --- |
| Current stage | Stage 0.3A-2A Checkpoint C — read-only Runtime API + development Runtime Inspector, **source authored in Arena (`06aca79` + the C3 slice), NOT COMPILED and NOT EXECUTED in Arena**; the Owner-local Function/Logic/UI review per runbook §14 is the validation of record and has **not been run yet** (see §11.7, §12.26). Checkpoints A (`55d3b8b`) and B (`cfa6d4a`) are Owner-locally validated (A 101/101 — §12.24; B 129/129 — §12.25) |
| Repository contents | Documentation, repository governance, the Stage 0.2.1A synthetic spike in `spikes/ui-runtime-react/`, and the Stage 0.3A-1 product foundation source skeleton (`WaterJetSentinelSuite.sln`, `packages/`, `apps/`, `adapters/`, `tests/`, `config/examples/`, `tools/`) |
| Application source code | **Validated at checkpoint scope by the Owner-local run** (Release 0/0; 61/61; fixtures and lock files genuine, transferred and verified; SDK pinned `10.0.401`). Arena itself ran no .NET and claims no compile/test success of its own. The spike remains synthetic feasibility code, not Product code |
| Project or solution files | `WaterJetSentinelSuite.sln` (12 projects) — source-committed, **never built in the authoring environment** |
| Package manifests or dependencies | Spike: `spikes/ui-runtime-react/react-ui/package.json` + `package-lock.json` (Owner-approved pins); `runtime-harness/package.json` (no deps). Product: `Directory.Packages.props` (pins **PROPOSED/UNVERIFIED**), `packages/contracts/wjss-contracts-ts/package.json` + lockfile (`typescript@6.0.3`, install-verified in Arena). `global.json` pins the Owner-validated SDK `10.0.401` (`rollForward: latestPatch`, added at closeout per Owner decision); the twelve genuine Owner-local `packages.lock.json` are committed (transferred by handoff `488b98fb…`, never Arena-generated) |
| Database schema or SQL scripts | **Do not exist** |
| Modbus or Galil adapter | **Does not exist** |
| Simulator | **SIMULATOR-only source** — from Stage 0.3A-2A, `adapters/simulator/Synthetic/` holds the canonical synthetic map (`SyntheticSensorMap`: 108 slots / 106 Sensors / 2 Cannon slots I7+I16, scan order, device distribution 14+14+13×6, `SYN-TC-nn:CHmm` channel identities) and the seeded initial Sensor projection (`SyntheticSeed`, `DeterministicValueSource` — splitmix64, no `System.Random`, no static mutable state). Never a production-path component; no acquisition loop, no fault injection yet (Checkpoint B scope). The map composition is pinned to the committed `config/examples/sensor-map.example.json` by a parity test in `tests/runtime.tests`; the fixture-side generator (`tests/integration/FixtureGenerator.cs`) is untouched and its unification with the adapter remains `[OPEN]`. Checkpoint B adds the evolution source (`SyntheticEvolution`: explicit seed + tick sequence + clock, quality paths GOOD / UNCERTAIN (Last Validated basis) / STALE with deterministic recovery, bounded 0–100 unitless score walk). Checkpoint C composes that source into a runnable SIMULATOR runtime host (`apps/runtime`: explicit synthetic configuration, single-writer evolution lifecycle, read-only API) and adds the development Runtime Inspector page that observes it — source authored and statically reviewed, not compiled or executed in Arena. The adapter still has no acquisition loop, no fault injection and no device path, and the spike's Node harness remains feasibility-only |
| Automated tests | Spike tests (Node `node:test`, Vitest, Playwright — Owner-local). Product .NET tests (5 xUnit projects): **Stage 0.3A-2A Checkpoint A PASSED Owner-locally (2026-10-07)** — Release build 0 warnings / 0 errors and full suite **101 total / 101 passed / 0 failed / 0 skipped** on SDK `10.0.401` (xUnit observed runtime .NET 10.0.12), locked restore PASS, boundary scan S1–S9 clean, at validated feature head `a512aa7` + lock refresh `55d3b8b` (§12.24); the earlier 0.3A-1 baseline remains the 61/61 record (§12.22). Arena executed only the TS mirror (**24/24**) and never claims .NET execution itself. Checkpoint B is **PASSED Owner-locally (2026-10-07)** — Release build 0 warnings / 0 errors and full suite **129 total / 129 passed / 0 failed / 0 skipped** on the consolidated correction `cfa6d4a`, locked restore PASS, no lock drift, boundary S1–S9 clean (§12.25). Checkpoint C test sources — 9 new tests (7 composition/readiness/route facts in `tests/api.tests`, 6-fact start-gate source pin in `tests/config.tests` replacing the 4-fact stage-0.3A-1 stub pin, and the in-place marker pin) for a **source total of 138** (129 Owner-validated + 9) — are authored in Arena, statically reviewed, and **not executed anywhere yet** (§12.26) |
| CI workflow | **Does not exist** |
| Installer or release artifact | **Does not exist** |
| Production configuration | **Does not exist in this repository** |
| Device connections made | **None** |

## 5. Verified system state

- Stage 0.2 is a documentation and architecture-decision activity only. It created no runtime
  artefact and installed no dependency.
- Stage 0.2 selected a `[PROPOSED]` technology and solution architecture — UI delivery model,
  runtime process model, technology stack, database access and migrations, device adapter
  boundary, configuration and secrets model, simulator-first development model, and offline
  deployment model — recorded as ADR candidates in
  [`decisions/README.md`](decisions/README.md). **`[PROPOSED]` is not approval**; each record
  requires Owner acceptance, and nothing was implemented.
- The architecture was decided in a working environment that contains no .NET SDK, no SQL
  Server client tooling, and no Windows runtime. No build, restore, execution, or measurement
  was possible or performed.
- The Owner-requested Stage 0.2 documentation review punchlist was implemented as a
  documentation-only review-correction checkpoint. It recorded the legacy-application
  architecture evidence, returned the final UI framework selection to `[OPEN]`, added the fair
  React-versus-Blazor comparison, and added the UI workload, Sensor presentation, quality
  pipeline, live-state delivery, Modbus acquisition, configuration hot-path, Historian
  decoupling, trend, and proposed Stage 0.2.1 spike content. No code, dependency, device
  access, or Production configuration was created.
- Stage 0.1 is a documentation and repository-governance activity only.
- No application code has been written, and no code may be written until a later Stage Gate
  authorises it.
- No hardware has been accessed. No WAGO coupler, Galil controller, pump, valve, or DCS
  signal has been contacted, probed, or tested.
- No production IP address, register map, tag list, motion coordinate, travel limit, speed
  profile, pressure setpoint, production alarm threshold, DCS permissive definition,
  credential, or connection string has been created or committed.
- The documentation distinguishes approved behaviour (`[APPROVED]`), Owner review decisions
  (`[OWNER CONFIRMED]`), and proposals (`[PROPOSED]`).

## 6. Owner-confirmed decisions closed during documentation review

These were previously `[OPEN]` or ambiguously described. They are now settled and must not be
reopened without a new Owner decision.

| # | Decision | Where recorded |
| --- | --- | --- |
| 1 | Water Jet to Isolation Valve is exactly one-to-one, dedicated, never shared; a sensor's valve is derived from its assigned Water Jet | [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001..006, [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8 |
| 2 | AutoSequence executes Cleaning Jobs strictly sequentially; INVARIANT-SEQ-001 to 006 | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ group |
| 3 | Maximum active Cleaning Jobs equals one | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §3 |
| 4 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 5 | GlobalQueue behaviour on Operator stop, and queue rebuild on a new AutoSequence | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9, [`REQUIREMENTS.md`](REQUIREMENTS.md) QUE-024, QUE-025 |
| 6 | Source ownership after deduplication; refill uses the original source owner | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3, §5.4 |
| 7 | Active alarm acknowledgement is awareness only; a cleared-state acknowledgement is required to release a block | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 8 | "Allow unaffected Water Jets to continue" means sequential continuation only | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2 |
| 9 | DCS Permissive Override: operator activated, manually released, reason and audit required, banner, scoped exclusions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR group |
| 10 | Operations UI close guard required while a job is active or the pump runs; it is not safety protection | [`REQUIREMENTS.md`](REQUIREMENTS.md) UIG group, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 11 | Communication health must not depend on value change detection; configurable stale timeout | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 12 | Cross-wall DirtyScore tie-break is not required; GlobalQueue order comes from the fixed source order | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §2 |
| 13 | Baseline role for the DCS Permissive Override is **Operator**: the Operator role template holds permission to activate **and** release it; a role without the permission cannot do either. The permission model remains configurable, and the baseline assignment remains Operator until an explicit Owner decision changes it | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-011..OVR-015 |

## 7. Blocking constraints in force right now

1. **No production device access.** `[NOT AUTHORIZED]`
2. **No production valve or pump write control** until the bench verification in
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) is completed and recorded. `[NOT AUTHORIZED]`
3. **No application code, schema, adapter, simulator, test, CI workflow, installer, or
   release artifact** may be created until a later Stage Gate authorises it. `[NOT AUTHORIZED]`
   The only exception is the Stage 0.2.1A synthetic spike inside `spikes/ui-runtime-react/`,
   within its approved scope. `[APPROVED]`
4. **No production values** may be invented, inferred, or committed. `[NOT AUTHORIZED]`
5. **No concurrent Cleaning Jobs.** Parallel Water Jet cleaning is prohibited. `[OWNER CONFIRMED]`
6. **No merging.** Merge authority belongs to the Owner. `[APPROVED]`
7. **No history rewriting or force push** without specific Owner authorisation. `[APPROVED]`
8. **No reuse of PR #1 or PR #2.** Stage 0.2.1A is delivered through one new pull request to
   `main`, as instructed by the Owner. `[APPROVED]`

## 8. Open items requiring Owner decisions

These remain unresolved. None may be resolved by assumption. Items closed by the review
punchlist have been removed; see section 6 for what was closed. Items are grouped below by
theme rather than numbered, because the list changes as decisions are taken.

### 8.1 Product and technology

The Stage 0.1 open technology items were addressed by Stage 0.2. Each one is now either a
`[PROPOSED]` architecture decision awaiting Owner acceptance, an `[OPEN]` item with a named
follow-up gate, or a `[NOT VERIFIED]` item requiring hardware or workstation evidence. The
disposition table is in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 11, the remaining open
items are listed in section 34 of the same document, and the decision records are indexed in
[`decisions/README.md`](decisions/README.md).

| Item | Stage 0.2 disposition |
| --- | --- |
| Application language, runtime, UI framework, and UI delivery architecture | .NET with C# for the Equipment Runtime and Local Application API: `[PROPOSED]`; application-owned Windows kiosk shell: `[PROPOSED]`; embedded local web UI delivery model: `[PROPOSED]`; React + TypeScript + Vite: `[PROPOSED]` comparison candidate, not accepted; Blazor Hybrid: `[PROPOSED]` comparison candidate, not rejected; final UI framework: `[OPEN]`; exact .NET version: `[OPEN]`; final selection deferred; the Stage 0.2.1A React feasibility spike is approved and in progress; Blazor counter-spike deferred / not authorized. Browser-based, desktop, and hybrid local-web delivery were all evaluated on requirements |
| Process architecture, service identity, startup behaviour, and Equipment Runtime separation | Process model selected as `[PROPOSED]`; service identity `[OPEN]` |
| Modbus TCP client library selection and licence acceptability | Boundary decided; library selection `[OPEN]` pending licence and offline-availability review |
| Galil communication mechanism and library selection | `[OPEN]` — must be evaluated before motion code is written |
| Local configuration store format and its validation mechanism | Model selected as `[PROPOSED]`; format and secret store `[OPEN]` |
| Data access approach for SQL Server 2025 Standard | Approach selected as `[PROPOSED]`; provider version and compatibility `[OPEN]` |
| Logging, diagnostics, and crash-report storage | Strategy selected as `[PROPOSED]`; provider `[OPEN]` |
| Final documentation and application versioning scheme | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Deployment acceptance criteria | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Backup, restore, and off-box copy | Remains `[OPEN]` — carried forward into the offline deployment decision |
| Historian write-path measurement and overflow policy | Remains `[OPEN]` — requires measurement on the target workstation |
| Kiosk startup mechanism, package format, firewall rules, diagnostic bundle contents | Remains `[OPEN]` — deployment-gate items |
| **Final UI framework (Candidate A React + TypeScript + Vite versus Candidate B Blazor Hybrid)** | **CLOSED — React selected as the Primary UI Framework** (Owner decision, 2026-10-07) after the Stage 0.2.1A Owner-local final Edge gate and manual review passed. Blazor counter-spike not required unless a future material blocker is identified |
| Push transport and presentation-state payload encoding | `[OPEN]` — presentation contract is transport-agnostic |
| Chart / trend library; UI test tooling; visual regression tooling | `[OPEN]` — library-neutral requirements recorded; selection needs spike evidence |
| Site-specific invalid-value and sentinel mapping | `[OPEN]` — requires the Tag and data-quality contract; production values must never be committed |
| ORM, mapper, micro-ORM, provider, and bulk-write mechanism | `[OPEN]` — the earlier "mapper as the primary technology" wording was replaced with testable architecture language |
| Historian overflow, spool, retry, priority, and outage policy | `[OPEN]` — must be explicit before Historian implementation and separated from the audit-required refusal policy |
| Poll Plan batching limits, poll-group intervals, concurrency bound, runtime recompilation | `[OPEN]` — requires device evidence and local Production configuration |
| Sensor cell colour tokens, typography, dimensions, animation; accessibility requirement level | `[OPEN]` — deliberately deferred to a future UX/UI decision |
| Acceptance thresholds for the Stage 0.2.1 spike | `[OPEN]` — must be set before the spike runs |

### 8.2 Sequence, queue, and equipment behaviour

| Item | Affects |
| --- | --- |
| Fault class taxonomy: when to stop motion versus abort motion | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Recovery procedure for `RECOVERY_REQUIRED`; whether retry is permitted; whether partial-path jobs may resume | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Permission required for each operator queue action, and required reason text | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Reject / Release Reject / Reorder semantics in a ready-only, head-only GlobalQueue (**OWNER DECISION REQUIRED**) | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Queue snapshot storage format and retention inside Event history | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §11 |
| Exact motion limits, profiles, homing, pulses per engineering unit, and operational envelope | [`REQUIREMENTS.md`](REQUIREMENTS.md) GAL-006 `[NOT VERIFIED]` |
| Exact pressure setpoints, rise timeout, stable dwell, and valve open/close timeouts | [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-005, VLV-003 `[NOT VERIFIED]` |
| Sensor-to-Water-Jet mapping and exact production coordinates | Deployment data, `[NOT VERIFIED]` |

**Resolved (DP-01):** the former open item *row alignment rule for walls of unequal size (24
versus 28)* is closed. That wording used the superseded 28-Sensor Rear and Front walls. The wall
distribution is **24 / 29 / 24 / 29** (Left / Rear / Right / Front). The Owner-confirmed
canonical **18-column × 6-row** logical matrix aligns the logical rows of all four walls; the
Cannon slots occupy logical **I7** (Rear) and **I16** (Front); the Sensor identifiers are the
Owner logical labels (`G+2xx`, `G+1xx`, `G`, `H`, `I`, `J`). This is no longer an open mapping
question. Production scan order, device distribution, and Water Jet assignment stay
deployment data, `[NOT VERIFIED]`. See [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.2.1.

### 8.3 DCS and communication

| Item | Affects |
| --- | --- |
| Exact DCS permissive definition set that the override bypasses | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §9 |
| DCS heartbeat hardware contract, if a heartbeat feature is pursued | [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH-004 `[OPEN]` |
| Production register map, Tag List, and signal quality contract | `[NOT VERIFIED]` |
| Exact local equipment network topology and address plan | [`ARCHITECTURE.md`](ARCHITECTURE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) PHY-008 |
| Stale-data timeout production value (30 s is an example only) | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5 |

### 8.4 Alarms, users, and data

| Item | Affects |
| --- | --- |
| Alarm definition catalogue, thresholds, blocking scopes, and non-shelvable list | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10 |
| Shelving duration limits and per-alarm maximums | [`ALARM_MODEL.md`](ALARM_MODEL.md) §6 |
| Whether active awareness acknowledgement and cleared-state acknowledgement share a field or a permission | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10, [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Authentication method, password policy, lockout, and privileged timeout default | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Whether named individual accounts are introduced in a future approved scope | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Retention default ratification; aggregate function definition; partition scheme; index strategy | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Backup, restore, and disaster-recovery approach | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Audit and event tamper protection | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §6, [`../SECURITY.md`](../SECURITY.md) |
| Behaviour when the Historian is unavailable | [`ARCHITECTURE.md`](ARCHITECTURE.md) §9 |
| Workstation clock discipline and drift bounds | [`ARCHITECTURE.md`](ARCHITECTURE.md) §7 |
| Target test coverage thresholds and test execution tooling | [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §7 |
| Critical Main Pump / Mandatory Safe Return policies: Safe Return failure, valve / axis failures, outcomes, re-queue, retry, acknowledge role, clear evidence, Resume authority, modal minimise, second alarm channel | [`spikes/critical-pump-safe-return-decision-matrix.md`](spikes/critical-pump-safe-return-decision-matrix.md) (every row `OWNER DECISION REQUIRED`) |
| Production Pause / Resume semantics (pause the AutoSequence or the Cleaning Job lifecycle) replacing the superseded queue-level `HELD` (**OWNER DECISION REQUIRED**) | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2 |

## 9. Not verified — standing list

| Item | Reason |
| --- | --- |
| WAGO watchdog fail-safe behaviour and timeout value | No bench test performed; not authorised |
| Hardware fail-safe on Ethernet loss, process termination, workstation reboot, WAGO reboot, stale command replay, auto re-energization | No bench test performed; not authorised |
| Production hardware response to any output command | No device access; not authorised |
| Mechanical limits, soft limits, and operational envelope | Commissioning values; not measured |
| Pulses per engineering unit | Commissioning value; not measured |
| Encoder behaviour and whether feedback performs active correction | Not verified by engineering |
| Homing behaviour | Commissioning value; not measured |
| Speed, acceleration, deceleration profiles | Commissioning values; not measured |
| Pressure-ready setpoint, stable dwell, pressure rise timeout | Commissioning values; not measured |
| Valve open and close timeouts | Commissioning values; not measured |
| Stale-data timeout production value | Configurable; no production value captured |
| Production network topology and address plan | Confidential deployment information |
| Production register map, Tag List, and DCS signal contract | Confidential; never committed |
| Sensor-to-Water-Jet and sensor-to-valve mapping | Deployment data; not recorded here |
| Historian effective capacity and database sizing | No capacity model produced; no benchmark performed |
| Runtime build, unit tests, integration tests, database tests, hardware tests | No Product code exists; not applicable. Stage 0.2.1A synthetic spike tests are recorded in section 12.3 and are not Product verification |
| Stage 0.2.1A WebView2, kiosk, end-to-end latency, long-run UI, Windows offline restore | Not measurable in Arena and not validated. The installed-Edge Owner-local final gate and manual review **PASSED** at `114c0761` (Owner-reported, 2026-10-07); controlled 15- / 60-minute observations waived as merge blockers, not run |

## 10. Sensitive data review

| Check | Result |
| --- | --- |
| Production IP addresses in the repository | None found |
| Credentials, tokens, keys, or password hashes | None found |
| Connection strings | None found |
| Production register map, Tag List, or addresses | Not present and must never be added |
| Motion coordinates, travel limits, speed profiles | Not present |
| Pressure setpoints or production alarm thresholds | Not present |
| DCS permissive definitions | Not present |
| Production configuration content | Not created. Only documentation references and `.gitignore` exclusion patterns exist. |

The checks behind this table are listed with their observed results in section 12.

## 11. Checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.1 |
| Branch | `arena/01a1080d-waterjet-sentinel-suite` |
| Original implementation checkpoint | `0323f8a5a06bad25383dbe636b75c46ab38455af` |
| Original checkpoint status | **SUBMITTED FOR OWNER REVIEW** |
| Documentation review | **CHANGES REQUESTED** |
| Owner manual review | **PENDING** |
| Merge state | **NOT MERGED** |
| Pull request | https://github.com/tnoiiee/waterjet-sentinel-suite/pull/1 |
| Review-correction checkpoint | `899a96a5b01a8e3cfcf0aaf2468ff83ce735090e` — on the same branch and the same pull request |
| Final targeted-correction checkpoint | A final targeted-correction commit on the same branch and the same pull request. Its SHA is recorded in the delivery report and the pull request description, not inside the commit that creates it. |
| Stage 0.1 final merge state | **MERGED** to `main` through PR #1. The merge commit is `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`, which is the approved remote-main base of Stage 0.2 |
| Stage 0.2 | **AUTHORIZED** by the approved Stage 0.2 Scope Gate |

Each correction is a new commit on the existing branch. No reviewed checkpoint was amended,
rebased, or rewritten.

### 11.2 Stage 0.2 checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.2 — Technology and Solution Architecture Decision |
| Approved main base | `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a` (verified remote `main`) |
| Original Stage 0.2 architecture checkpoint | `b881a5fc7abac226e6c40f9cd01ec66d3ebf07c9` |
| First Stage 0.2 review-correction checkpoint | `058a4255fd87a943d73c1def7f27f839cee222ff` |
| Final documentation-consistency checkpoint | Created on the Stage 0.2 branch. Its SHA is recorded in the delivery report and in the pull request description, **not** inside the commit that creates it |
| Current Stage branch | `arena/01a1087c-waterjet-sentinel-suite` |
| Pull request | https://github.com/tnoiiee/waterjet-sentinel-suite/pull/2 |
| Stage 0.2 Scope Gate | **APPROVED** |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED** |
| Source checkpoint accepted | `5bcf1b33f924ab30590a55736676200115874fa1` |
| Merge | **MERGED** to `main` through PR #2 — merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`, which is the approved base of Stage 0.2.1A |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

*Rows above that described the Stage 0.2 review-in-progress state were replaced when the Owner
accepted and merged Stage 0.2.*

### 11.3 Stage 0.2.1A checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.2.1A — React UI and Runtime Feasibility Spike (synthetic) |
| Approved main base | `e779f8ad2c856e367fd65985007a3da411bd0e73` (verified remote `main` before coding) |
| Stage branch | `arena/01a108d8-waterjet-sentinel-suite` |
| Development checkpoint | `dd20a8bd` (104-location map, superseded by the Owner domain correction) |
| Sensor-map correction checkpoint | `935973e6ff13aca26efa67148977db44b78be2e1` — normal fast-forward on top of `dd20a8bd` |
| Fullscreen UI refinement checkpoint | A normal fast-forward commit on top of `935973e6`. Its SHA is recorded in the delivery report and in the PR #3 description, **not** inside the commit that creates it |
| Local recovery before the correction | An Owner-authorised, one-time local reference recovery set the local branch to the already-pushed `dd20a8bd` after an exact identity proof. No history was rewritten, nothing was force-pushed, and AGENTS.md is unchanged |
| Local recovery before the fullscreen refinement | The Arena sandbox was recreated (local branch at `e779f8ad`, checkpoint source present as working-tree changes). A second Owner-authorised, one-time, checkpoint-specific recovery: targeted fetch, complete-tree identity proof against `935973e6` (126 / 126 exact blob matches, 0 mismatches, 0 missing, 0 extra, 0 mode differences), compare-and-swap `update-ref` `e779f8ad` → `935973e6`, `read-tree` **without** `-u`; working tree unchanged. AGENTS.md is unchanged; this is not a general recovery rule |
| Fullscreen UI refinement checkpoint SHA | `ea23bc589b2f61a9c97aaa2b1a1faa9d2ac31d68` |
| Readability refinement checkpoint | A normal fast-forward commit on top of `ea23bc58` (presentation refinement plus the stale MAP U-shape assertion correction). Its SHA is recorded in the delivery report and in the PR #3 description, **not** inside the commit that creates it |
| Local recovery before the readability refinement | The Arena sandbox was recreated again (local branch at `e779f8ad`, checkpoint source present as working-tree changes). A third Owner-authorised, one-time, checkpoint-specific recovery: targeted fetch, complete-tree identity proof against `ea23bc58` (130 / 130 exact blob matches, 0 mismatches, 0 missing, 0 extra, 0 mode differences), compare-and-swap `update-ref` `e779f8ad` → `ea23bc58`, `read-tree` **without** `-u`; working tree unchanged. AGENTS.md is unchanged; this is not a general recovery rule |
| Pull request | PR #3 to `main` — **OPEN** (PR #1 and PR #2 are not reused) |
| Scope Gate / Coding Start Gate | **APPROVED** |
| Implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT** (`114c0761`) |
| Owner-local testing | Owner-local final Edge gate at `114c0761`: **PASS** (Owner-reported, 2026-10-07) |
| Owner manual UI re-review | **PASS** at `114c0761` (Owner-reported) — controlled 15- and 60-minute observations **waived as merge blockers** |
| Owner-local interrupted overnight observation (`ea23bc58`) | **COMPLETED** as reported by the Owner — not a controlled benchmark; Production stability **NOT VERIFIED** |
| Merge | **NOT MERGED** — ready for Owner merge |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07) |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |

### 11.4 Stage 0.3A-1 Product Foundation Source Checkpoint record

| Item | Statement |
| --- | --- |
| Gate | Owner **Option-C amended gate** — Arena may author the approved 0.3A-1 source set; it may not compile .NET (SDK/NuGet blocked by the sandbox network) and must not claim build/test success |
| Authored | Solution (12 projects); `Wjss.Contracts` (18 files) incl. queue capacity 8 + head-only consumption + profile-start fail-closed; Domain/Runtime.Core/Time/Simulator sources; Runtime health stub; compile-only Kiosk; 5 xUnit projects incl. the .NET golden-fixture generator + parity gate; TS mirror + validator; 4 provisional fixtures + 2 config examples; `tools/boundary-scan` (S1–S7, 0 findings); DRAFT ADR-0014/0015/0016; stage plan + Owner-local runbook; `.gitignore` product-`packages` conflict corrected |
| Wire-contract correction | The checkpoint draft's `activeJobCleared` flag was **rejected by Owner review**; the accepted three-state baseline (absent = unchanged / object = replace / `null` = clear) is restored, implemented via the scoped `Optional<T>` presence wrapper (structural technique, ADR-0014 §2) |
| Fixture provenance | Node-authored in Arena, labelled `PROVISIONAL STRUCTURAL FIXTURE`; **not** .NET-generated; superseded by the Owner-local generator run (runbook §5) |
| Arena verification | TS: `npm ci` + `tsc --noEmit` clean + `node --test` 14/14 PASS + fixtures validate; boundary scan exit 0; all JSON parses; links/whitespace/secret checks — see §12.12 |
| NOT verified | Every .NET claim: restore, build, tests, health-stub behaviour, kiosk, parity between C# generator and committed fixtures, NuGet pin availability |
| Delivery | Single commit on `arena/dd551752-waterjet-sentinel-suite`; PR "Stage 0.3A-1: product foundation source skeleton" → `main`; **NOT READY FOR MERGE** until runbook verdict PASS |
| Next | ~~Owner-local runbook PASS~~ **DONE** → Owner merges PR #4 (READY FOR OWNER MERGE — closeout 2026-10-07, see §12.22) → Owner explicitly authorizes **0.3A-2** (nothing advances otherwise; no ZIP before Stage 0.3 exit) |
| CLOSEOUT (2026-10-07) | Rows above describing "Node-authored fixtures", "NOT READY FOR MERGE" and blanket "NOT verified" are preserved as authored-time state. Authoritative current state: Owner-local Release build PASS (0/0), fresh suite **61/61**, TS 24/24, parity 7/7, boundary clean; fixtures and the 12 lock files are the genuine .NET/restore outputs transferred via handoff `488b98fb…`; SDK pinned `global.json` → `10.0.401`. **PR #4 READY FOR OWNER MERGE — NOT MERGED**; 0.3A-2 and Production devices NOT AUTHORIZED |

### 11.5 Stage 0.3A-2A Runtime State Foundation — Checkpoint A record

| Item | Statement |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Stage 0.3A-2 (Runtime State Foundation) coding AUTHORIZED for the **SIMULATOR profile only**, delivered as three checkpoints A → B → C on one branch and one pull request, from approved remote `bdf7f8f277b85087e3e53c067fe956fca7e04f90`. Arena still cannot compile .NET; sources are authored and statically reviewed only |
| Checkpoint A scope | In-memory Runtime State Store (single authoritative writer, immutable revisions, monotonic and gapless revisions, atomic refusal of invalid updates, bounded revision history, no mutable collection leakage, no static mutable state) + deterministic SIMULATOR initial state from the existing synthetic map (108 slots / 106 Sensors / 212 channels, Cannon slots I7 + I16) + `wjss.snapshot/1` projection. No Delta, no SSE, no command route, no queue dispatch, no pump/valve/axis control, no configuration write |
| Authored (Checkpoint A) | `packages/application/Runtime/` (11 sources: state, refusal codes, limits, collections, wall summaries, invariants, freezer, revision activity, store, composer, projector); `packages/time/UtcTimestamps.cs`; `adapters/simulator/Synthetic/` (3 sources); `tests/runtime.tests/` (5 new test sources, migrated stage-marker test, simulator project reference); adapter README, two csproj comment/reference updates, and this documentation record |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**) run on the changed worktree; final-newline / trailing-whitespace / tab checks; brace-balance script over every changed C# file; contract-member cross-check of every referenced type against `packages/contracts`; JSON re-parse of `config/examples/sensor-map.example.json` used by the parity test. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the new tests); the `tests/runtime.tests/packages.lock.json` refresh required by the new `Wjss.Adapters.Simulator` project reference (real Owner-local restore only, never hand-edited); the deliberate difference between the seed-derived synthetic presentation values and `tests/integration/FixtureGenerator.ScoreFor` (fixture formula preserved, not claimed equal); Checkpoints B and C (not started) |
| Delivery | Branch `arena/873f0015-waterjet-sentinel-suite`; **Checkpoint A commit `f51408e400935f7dd899d27544d8e7d9d18949b5`** (parent `bdf7f8f277b85087e3e53c067fe956fca7e04f90`, the approved remote `main` base); one pull request opened from this branch → `main` for all three checkpoints. The SHA above is recorded by this follow-up documentation commit, because AGENTS.md §4.6 forbids recording a commit's own SHA inside the commit that creates it. Checkpoint A status: **SUBMITTED FOR OWNER REVIEW — NOT MERGED** (the Agent never merges) |
| Correction chain (all pushed to the same branch/PR) | `b389805` CA1859 correction (concrete `ReadOnlyCollection<TrendPoint>` return); `93e8f24` CS0051 correction (`public enum Tamper` for the xUnit theory); `b75bccb` CS1061 + CA1859 synthetic-example test compile correction (`JsonSerializer.Deserialize<List<SensorMapSlotExample>>` on the slots node, concrete helper return type); `a512aa7` SensorChannels tamper isolation (fixture preserved ScanOrder/identity so the pinned code `SENSOR_TC_CHANNELS` is reached) |
| Genuine lock refresh | `55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c` — Owner-local real restore; changes exactly `tests/runtime.tests/packages.lock.json` (+7 lines) adding the `wjss.adapters.simulator` Project dependency with `Wjss.Contracts` and `Wjss.Time`. No absolute Owner-local path, no other file changed |
| Owner-local validation (2026-10-07) | **PASSED** at feature head `a512aa76c4b2d7633d2a83b10c523c318b3a420e` + lock refresh `55d3b8b`: .NET SDK **10.0.401**, xUnit runtime **.NET 10.0.12**; Release build **0 warnings / 0 errors**; full .NET suite **101 total / 101 passed / 0 failed / 0 skipped**; **locked restore PASS**; boundary scan **0 findings (S1–S9 clean)**; Working Tree clean after the lock commit and push. Recorded in §12.24 |
| Status | **Checkpoint A COMPLETE for development checkpoint** (Owner-locally validated; SUBMITTED FOR OWNER REVIEW — NOT MERGED, the Agent never merges) |
| Next | **Checkpoint B AUTHORIZED** (deterministic synthetic evolution + Snapshot/Delta foundation) — see §11.6; Checkpoint C (read-only API + lightweight Runtime Inspector) remains **NOT AUTHORIZED** |

### 11.6 Stage 0.3A-2A Checkpoint B — deterministic synthetic evolution + Snapshot/Delta foundation

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Checkpoint A of Stage 0.3A-2 is Owner-locally validated (build 0/0, suite 101/101, locked restore PASS, boundary S1–S9 clean at feature head `a512aa7` + lock refresh `55d3b8b`), and **Checkpoint B is authorized** on that validated store, SIMULATOR profile only. Checkpoint C remains NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION remain NOT AUTHORIZED |
| Delivered (source) | `packages/application/Runtime/`: `RuntimeSyntheticEvolution` (rules, tick outcome, deterministic walk and quality schedules), `RuntimeDelta` (apply-safe Delta plus the three-state Active Job slot), `RuntimeDeltaProjector` (Runtime-side and `wjss.delta/1` wire projection), `RuntimeDeltaApply` (strict total-or-nothing application), `RuntimeDeltaHistory` (bounded history and catch-up/gap results), `RuntimeTrendBuffer` (shared bounded append rule); the clock helper gains strict wire-timestamp parsing; `RuntimeRefusalCodes` gains `EVOLUTION_TICK_SEQUENCE`, `EVOLUTION_TICK_TIME`, `DELTA_OUT_OF_ORDER`, `RESNAPSHOT_REQUIRED`; `RuntimeLimits` gains explicit Delta-history capacities. Tests: `SyntheticEvolutionTests`, `SnapshotDeltaTests`, `DeltaApplyTests`, `DeltaHistoryTests` and the scenario helper `RuntimeDeltaTestFixture` |
| Semantics | Deterministic evolution from explicit (state, seed, tick number, tick instant) only; revision +1 exactly once per accepted tick; one committed state per accepted tick; refusals carry no candidate and change nothing; wall summaries recalculated from the complete evolved Sensor collection; bounded trend append with deterministic oldest-drop; `wjss.snapshot/1` retained; `wjss.delta/1` gapless with whole-record replacement and the exact three-state Active Job encoding; strict apply path (revision mismatch ⇒ machine-readable resnapshot-required, nothing applied, nothing normalized); bounded in-memory Delta history with deterministic oldest-eviction; gap detection only (no fabricated Delta, no continuation past a gap, no reconnect/retry/SSE) |
| Deliberately absent | Runtime API routes beyond the existing health stub, Runtime Inspector UI, SSE, write/command endpoints, queue dispatch, job execution, Pump/Valve/Axis/Cleaning commands, Safe Return actuation, configuration writes, persistence/Historian, auth, installer/release work, `spikes/**` changes, `apps/runtime/Program.cs` changes |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**); brace-balance, trailing-whitespace, final-newline and tab sweeps; `using`-directive resolution sweep (missing/unused namespaces); declaration sweep for every referenced type; determinism grep for ambient clocks and `System.Random`; `git diff --check`. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the new tests); the behaviour is **source authored / statically reviewed / NOT COMPILED and NOT EXECUTED in Arena**. Owner-local validation per runbook §13 is the validation of record |
| Correction chain (Owner-local build rounds) | `dd45bf6` CS0102 disambiguation (the `DeltaApplyOutcome.Applied` property kept; the success factory renamed to `Success`); `8692b77` test-source alignment (named `seed:` arguments, four CA1861 constant arrays hoisted to named `private static readonly` fields); `cfa6d4a` consolidated correction — **the only Product behaviour change of the three: `RuntimeDeltaHistory.CatchUpFrom` now selects the Delta that CONTINUES the consumer revision (`previousRevision` match) instead of the Delta whose own revision equals it**, which removed a spurious fresh-Snapshot result and a non-terminating walk; the remaining five findings were test-side (two structural `TrendPoint` comparisons via the shared `RuntimeTestFixture.AssertTrendPointsEquivalent` helper because `Series` is an array and record equality compares it by reference; three derived revision-expectation corrections: chain starts at revision 2, capacity-3 retention 4/5/6, catch-up input 3) |
| Delivery | Same branch and PR #5, after the Checkpoint A evidence commit; **NOT MERGED** (only the Owner merges) |
| Owner-local validation (2026-10-07) | **PASSED** at consolidated correction `cfa6d4a376bbf10a87db2349045cb6b9b57bb544`: Release build **0 warnings / 0 errors**; full .NET suite **129 total / 129 passed / 0 failed / 0 skipped**; **locked restore PASS**; **lock drift NONE**; boundary scan **0 findings (S1–S9 clean)**; Working Tree **CLEAN**. Recorded in §12.25 |
| Status | **Checkpoint B COMPLETE for a development checkpoint** (Owner-locally validated; SUBMITTED FOR OWNER REVIEW — NOT MERGED). Checkpoint B is **OWNER-LOCALLY VALIDATED** |
| Next | **Checkpoint C AUTHORIZED and delivered as source** — SIMULATOR runtime composition, deterministic evolution lifecycle, read-only Runtime API (`GET /api/v1/snapshot`, `GET /api/v1/runtime`, `GET /api/v1/deltas`), readiness semantics and a development-only Runtime Inspector; see §11.7. Owner-local Function/Logic/UI review per runbook §14 is the validation of record |

### 11.7 Stage 0.3A-2A Checkpoint C — SIMULATOR runtime composition, read-only Runtime API and development Runtime Inspector

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Checkpoint B is Owner-locally validated (`cfa6d4a`; build 0/0, suite 129/129, locked restore PASS, boundary S1–S9 clean) and **Checkpoint C is authorized** on that foundation, SIMULATOR profile only. `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED, as do PR merge, ZIP/release and any write surface |
| Delivered (source) | `apps/runtime/`: `RuntimeHostOptions.cs` (explicit synthetic configuration, safe development defaults, bounded validation, refusals as values), `SimulatorRuntime.cs` (composition of the initial revision from the adapter synthetic map through the Runtime composer, established initial Snapshot projection, single-writer evolution lifecycle, readiness answers with machine codes, fault observation), `RuntimeApiResponses.cs` (read-only status, Delta-feed and Sensor-view projections), rewritten `Program.cs` (profile → port → configuration → composition → lifecycle start → listener, then the read-only surface); `packages/contracts/ApiRoutes.cs` gains `Snapshot`, `Runtime`, `Deltas`, `Inspector`; `HealthPayloads.cs` documentation follows the substage that now exists; `RuntimeStage.Marker` moves to `STAGE_03A2C_RUNTIME_API`; `apps/runtime/Inspector/index.html` is the development Inspector page; `Wjss.Runtime.csproj` references `Wjss.Adapters.Simulator` and ships the page with the build output; `tests/api.tests` references the host project and adds `RuntimeHostCompositionTests`; `tests/config.tests/RuntimeStubPreservationSourceTests.cs` is renamed to `RuntimeStartGateSourceTests.cs` and rewritten for the new readiness contract |
| Routes and shapes | `GET /health/live` → 200 `{status: ALIVE, host, deviceProfile, runtimeImplemented: true, stageMarker}` (liveness only, never a readiness claim); `GET /health/ready` → 200 `{status: READY, code: RUNTIME_READY, detail, stageMarker}` when every readiness condition holds, else 503 with the same shape and the refusing machine code; `GET /api/v1/snapshot` → current `wjss.snapshot/1` projection (503 + code while no state exists); `GET /api/v1/runtime` → read-only status; `GET /api/v1/deltas` → bounded recent Delta activity with a reported gap flag; `GET /inspector` → the development page. Every payload serializes with `ContractJson.Options`; the host configures no second JSON policy. No write, command, dispatch or SSE surface exists anywhere |
| Readiness codes | `RUNTIME_READY` (200) plus the refusals `CONFIGURATION_NOT_VALIDATED`, `PROFILE_NOT_SIMULATOR`, `EVOLUTION_NOT_STARTED`, `STORE_NOT_INITIALIZED`, `REVISION_NOT_INITIALIZED`, `INITIAL_SNAPSHOT_UNAVAILABLE`, `STARTUP_FAULT`, `FATAL_RUNTIME_FAULT` (all 503, machine-readable, no silent fallback). The store, revision and projection preconditions are verified when readiness answers rather than assumed, so a failure fails closed instead of reporting a ready runtime |
| Lifecycle semantics | Exactly one non-overlapping evolution loop per runtime; one accepted tick = exactly one committed revision through the store's single writer; the timer paces execution while every presented value stays a function of (seed, tick number, committed state); a refused tick records a fault and a rejected-transition counter, emits no Delta and advances no revision, and tick instants advance monotonically so one refusal can never stall the loop; consecutive tick failures are bounded; shutdown cancels the loop through the `CancellationTokenSource` and observes it without rethrowing; a composition fault keeps readiness at 503 with `STARTUP_FAULT` instead of a silently degraded runtime |
| Inspector | Single static page served from the build output (no Node pipeline, no framework): ~1 s GET-only polling of the three read-only endpoints with non-overlapping requests, a visible stale-data banner when polling fails or stops, bounded recent Delta rows, wall-grouped Sensor table with score/classification/quality/reason, foundation metrics, wall summaries, runtime state with the placeholder labelling, a Snapshot facts block and a copy-Snapshot-JSON action. Local interactions (pause display, density, wall and quality filters, copy) never alter Runtime state; no control affordance exists |
| Deliberately absent | Write API, Runtime commands, queue dispatch, Start/Pause/Resume/Abort of a Cleaning Job, Pump/Valve/Axis commands, Safe Return execution, TEST_HARDWARE access, PRODUCTION access, physical device adapters, Modbus/Galil/KMotion/PLC, database, Historian persistence, authentication expansion, WebView2 shell migration, full Product UI migration, SSE, installer, ZIP, release, deployment, `spikes/**` changes |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**) on the changed worktree; single inline Inspector script extracted and syntax-checked with `node --check`, HTML tag balance checked; per-file brace-depth balance over every changed C# source; trailing-whitespace / final-newline / tab sweeps; contract-member cross-check of every referenced type and member against `packages/contracts` and `packages/application/Runtime`; duplicate-simple-name scan across the `Wjss.Contracts` / `Wjss.Runtime` / `Wjss.Runtime.Core` namespaces; `git diff --check`. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the authored tests); the two lock files the new project references are expected to refresh — `apps/runtime/packages.lock.json` and `tests/api.tests/packages.lock.json` (real Owner-local restore output only, never hand-edited); the actual browser behaviour, layout, scrolling and polling of the Inspector; the Owner-local Function/Logic/UI review. The runtime behaviour is **source authored / statically reviewed / NOT COMPILED and NOT EXECUTED in Arena** |
| Delivery | Same branch `arena/873f0015-waterjet-sentinel-suite` and PR #5, after the Checkpoint B evidence commit. Slice C1+C2 is `06aca79d03c57b703768afffecf95735de7a91d5` (`feat(runtime): compose the simulator runtime, lifecycle and read-only API`); slice C3 — the Inspector page, the Inspector source test and runbook §14 — is the commit immediately following it, and per [`AGENTS.md`](../AGENTS.md) §4.6 that commit does not record its own SHA (it is reported in the PR/Agent delivery report instead). **NOT MERGED** (only the Owner merges) |
| Status | **Checkpoint C source authored and statically reviewed — NOT VALIDATED anywhere yet**; submitted for Owner review |
| Next | Owner-local Function/Logic/UI review per runbook §14 of [`STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md); record the observed results in §14.9. `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED |

### 11.1 Process deviation record

| Item | Statement |
| --- | --- |
| Event | During the Stage 0.1 review-correction session, the local branch pointer did not match the expected remote Stage checkpoint |
| Reported evidence | The working-tree content was verified as byte-identical to the remote checkpoint, and the resulting push was reported as a fast-forward |
| Reported integrity | **NO MISMATCH FOUND** in source content, and **no history rewrite was reported** |
| Deviation | The approved Stop condition required the session to stop and report the baseline mismatch before continuing. A mixed pointer/index update was performed instead, so stop-condition compliance was **not** met |
| Governance response | [`../AGENTS.md`](../AGENTS.md) §7.1 now defines a strict Baseline Mismatch Stop Gate. Content comparison is diagnostic evidence only, content identity does not authorise recovery, `/tmp` is not durable recovery evidence, and recovery requires explicit Owner-authorised instructions |
| Status | **PROCESS DEVIATION RECORDED** — Owner acceptance **PENDING** |
| Integrity conclusion | The deviation does not indicate source corruption. It is recorded as a process-compliance deviation, not as an integrity failure |

This record is retained deliberately. Git history was not rewritten to remove the deviation.

## 12. Validation record

Documentation-only validation, performed for the Stage 0.1 review-correction checkpoint.
Detailed commands and observed output are reported in the delivery report.

| # | Check | Method |
| --- | --- | --- |
| 1 | List all changed files; confirm no out-of-scope file | `git status --porcelain` and `git diff --stat` before committing |
| 2 | Internal Markdown links resolve | Extracted every relative link target and verified each path exists |
| 3 | Production IP address search (IPv4 and CIDR) | Regular-expression scan across tracked files |
| 4 | Credential, token, password, private-key, and connection-string search | Keyword and pattern scan across tracked files |
| 5 | Conflicting null use for `LastSuccessfulCleaningCompletedAt` | Keyword scan |
| 6 | Obsolete `NEVER_CLEANED` behaviour | Keyword scan |
| 7 | Claims that WAGO fail-safe has passed | Keyword scan of surrounding context |
| 8 | Claims that production device access is authorised | Keyword scan |
| 9 | Parallel Cleaning Jobs described as `[OPEN]` | Cross-document search for `parallel`, `concurrent`, and related open-item entries |
| 10 | Water Jet to Isolation Valve cardinality described as `[OPEN]` | Cross-document search |
| 11 | "Six source queues" or any other wrong source-queue count | Cross-document search |
| 12 | Old GlobalQueue reload after a sequence restart | Cross-document search |
| 13 | Automatic final acknowledgement after an alarm clears | Cross-document search |
| 14 | `0.1.0` described as an application release | Cross-document search |
| 15 | Stage 0.1 implementation described as Owner accepted | Cross-document search |
| 16 | Web or local-web delivery described as rejected | Cross-document search |
| 17 | "must not infer the state" wording | Cross-document search |
| 18 | Main Pump stop described as executable in all circumstances | Cross-document search |
| 19 | Unsupported historian capacity certainty | Cross-document search |
| 20 | No application code or package dependency created | File-type and file-extension inventory |
| 21 | Diff reviewed before commit | Full diff read before creating the review-correction commit |

### 12.1 Stage 0.2 checkpoint documentation validation record

Documentation-only validation, performed for the **first** Stage 0.2 checkpoint. Every result
below is observed output from the method shown. The exact commands are reported in the Stage 0.2
delivery report.

**Superseded.** This record describes the first checkpoint. The Owner-requested Stage 0.2
documentation review then returned changes, and the corrected state is recorded in section
12.2. Two rows below are no longer an accurate description of the repository: row 1 names
12 modified and 8 created files, and row 8 lists the stage-status wording of the first
checkpoint. Neither is wrong as history; both were changed by the review-correction
checkpoint.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` | 12 modified existing documents and 8 new ADR files; nothing else created or modified |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. No solution, project, manifest, lock file, source, SQL, script, installer, archive, or CI file exists |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | **33 files scanned, 573 internal relative links checked, 0 broken** |
| 5 | Protected decisions remain stated | Targeted `grep` runs, listed in the delivery report | All required statements found, including Water Jet to Isolation Valve one-to-one, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, the Operator baseline override role, cleared-state acknowledgement, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, and Stage 0.3 `[NOT AUTHORIZED]` |
| 6 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | UI owns no device session and cannot write to hardware; the runtime service owns physical device sessions; domain logic is isolated from vendor libraries; simulator adapters implement the application-facing contracts; physical adapters are disabled by default; production configuration stays outside the repository; no statement permits parallel Cleaning Jobs |
| 7 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | **Zero matches** |
| 8 | Status check | Stage-status wording search | Stage 0.2 Scope Gate APPROVED; Stage 0.2 implementation SUBMITTED FOR OWNER REVIEW; Owner manual review PENDING; NOT MERGED; Stage 0.3 NOT AUTHORIZED; Production Device access and Production Write NOT AUTHORIZED |
| 9 | Full diff review | Complete staged diff read before the checkpoint commit | Every change traceable to Stage 0.2; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 10 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, or installer test was executed, and none is claimed |

### 12.2 Stage 0.2 review-correction (punchlist) documentation validation record

Documentation-only validation, performed for the Stage 0.2 review-correction checkpoint that
implemented the Owner-requested punchlist. Every result below is observed output from the
method shown. The exact commands are reported in the Stage 0.2 delivery report.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` and `git diff --stat` | Only repository documentation and decision records modified. No application source, project, manifest, lock file, SQL, script, installer, archive, CI file, configuration file, or runtime directory was created |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. Only `./docs` and `./docs/decisions` directories exist |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | **33 Markdown files scanned, 654 relative internal links checked, 0 broken** — previous checkpoint: 33 files, 656 links, 0 broken; delta -2 links from removed superseded scope-limitation narrative (no broken links) |
| 5 | Every ADR keeps its required section set | Heading extraction per ADR | Status, Context, Decision, Alternatives considered, Consequences, Risks, Verification status, Follow-up gates, Relationship to protected decisions |
| 6 | The UI framework selection is not presented as accepted | Cross-document search for framework-acceptance wording | Final framework is `[OPEN]`; both candidates are `[PROPOSED]`; the React preference is explicitly not acceptance; Blazor is explicitly not rejected |
| 7 | The React offline claim is correct and present | Targeted search | "React can be built and deployed offline" is stated, together with the second-ecosystem cost and the explicit statement that this does not make offline development or deployment impossible |
| 8 | Protected decisions remain stated and unmodified | Targeted `grep` runs, listed in the delivery report | All required statements found and unchanged: one Boiler Unit per Workstation, 104 Sensor locations, 208 Thermocouple channels *(superseded baseline — corrected by the Owner to 106 / 212 during Stage 0.2.1A, see section 2)*, Eight Water Jets, Four Galil DMC-B140-M controllers, Water Jet to Isolation Valve one-to-one, strictly sequential Cleaning Jobs, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, Operator baseline override role, cleared-state acknowledgement, Main Pump may remain running between sequential Jobs, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, Production configuration never in the repository |
| 9 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | The UI owns no device session and cannot write to hardware; the Equipment Runtime service is the sole owner of physical device sessions; adapters contain no UI logic and the domain is independent of vendor libraries; the simulator implements the same application-facing contract; physical adapters are disabled by default; the runtime consumes only Published configuration; the UI does not read Modbus and does not query SQL for live state; no statement permits parallel Cleaning Jobs |
| 10 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | Zero matches |
| 11 | Stage-status consistency | Stage-status wording search across every Markdown file | Zero remaining lines describing Stage 0.2 as `[NOT AUTHORIZED]`. Every file now states the current position: Stage 0.2 Scope Gate `[APPROVED]`; Stage 0.2 architecture checkpoint SUBMITTED FOR OWNER REVIEW; documentation review CHANGES REQUESTED / IN PROGRESS; Owner manual review PENDING; NOT MERGED; Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]` |
| 12 | Cross-reference integrity | Search for the old `section 22` reference and for the new section numbers | The open-item section is now section 34 and the earlier references were corrected. Sections 23–33 referenced by the decision records all exist |
| 13 | Full diff review | Complete diff read before the checkpoint commit | Every change is traceable to the punchlist; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 14 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, simulator, or installer test was executed, and none is claimed |

### 12.3 Stage 0.2.1A Arena validation record

*Checkpoint `dd20a8bd`, recorded on the superseded 104-location map. See section 12.4 for the
corrected map.*

Synthetic spike validation in Arena (Linux x64, Node v22.22.3). Detailed evidence:
[`spikes/stage-0.2.1a-results.md`](spikes/stage-0.2.1a-results.md) and
`spikes/ui-runtime-react/results/summary/arena-validation.md`.

| # | Check | Observed result |
| --- | --- | --- |
| 1 | TypeScript 6.0.3 `tsc --noEmit` | PASS, 0 errors |
| 2 | Vite 8.3.2 production build | PASS |
| 3 | Vitest (jsdom), 8 files | 53 / 53 PASS |
| 4 | Runtime harness `node:test` | 14 / 14 PASS |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | 10-minute harness measurement, 2 SSE clients | Exit 0; invariant violations 0; accepted second Jobs 0 |
| 7 | Playwright spec parse (`--list`, no browser) | PASS, 16 tests |
| 8 | Offline `npm ci` against an unreachable registry | PASS; negative control `ENOTCACHED` |
| 9 | Sensitive-data scan, new and changed files | PASS |
| 10 | Markdown relative links | PASS, 0 broken |
| 11 | Full diff review | Performed before the checkpoint commit |
| 12 | Browser, Edge, WebView2, Windows, kiosk, end-to-end latency, long-run UI | **NOT VERIFIED** in Arena — Owner-local testing PENDING |

**Reported discrepancy — resolved.** At checkpoint `dd20a8bd` the status headers of 12
documents still described the Stage 0.2 submission state: `SECURITY.md`, `ALARM_MODEL.md`,
`ARCHITECTURE.md`, `CLEANING_SEQUENCE.md`, `CONTROL_AUTHORITY.md`, `DOMAIN_MODEL.md`,
`HISTORIAN_RETENTION.md`, `QUEUE_MODEL.md`, `REQUIREMENTS.md`, `SAFETY_BOUNDARY.md`,
`TEST_STRATEGY.md`, `USER_PERMISSION_MODEL.md`. The Owner authorised the correction with the
Sensor-map domain correction, and all 12 (plus `ROADMAP.md` and `decisions/README.md`) now carry
the current status. See section 12.4.

### 12.4 Stage 0.2.1A Sensor-map correction — Arena validation record

Synthetic spike validation in Arena (Linux x64, Node v22.22.3) of the 106-location correction.
Detailed evidence: [`spikes/stage-0.2.1a-results.md` §0](spikes/stage-0.2.1a-results.md#0-sensor-map-correction-checkpoint).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, TypeScript 6.0.3 `tsc --noEmit` | PASS, 0 errors (lock file unchanged) |
| 2 | Vite 8.3.2 production build | PASS |
| 3 | Vitest (jsdom), 10 files | 60 / 60 PASS |
| 4 | Runtime harness `node:test`, 2 files | 26 / 26 PASS |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Golden fixtures regenerated and validated against the canonical map | PASS |
| 7 | 10-minute harness measurement, 2 SSE clients | Exit 0; invariant violations 0; accepted second Jobs 0 |
| 8 | Playwright spec parse (`--list`, no browser) | PASS, 17 tests |
| 9 | Hard gates | 106 Sensors; 212 Thermocouple channels; 24 / 29 / 24 / 29; 2 Cannon slots; I7 / I16 absent; 0 duplicate IDs, slots, scan orders, `TC_F`, `TC_R`, or shared channels |
| 10 | Markdown relative links | PASS — 44 files, 694 links, 0 broken |
| 11 | Sensitive-data scan, changed and new files | PASS |
| 12 | SHA-256 manifest regenerated and verified | PASS |
| 13 | Full diff review | Performed before the correction commit |
| 14 | Browser, Edge, WebView2, Windows, kiosk, end-to-end latency, long-run UI | **NOT VERIFIED** in Arena — Owner-local re-run and manual UI re-review **PENDING**; 60-minute run **PAUSED** |

### 12.5 Stage 0.2.1A fullscreen Operations refinement — Arena validation record

Owner-approved Design Addendum: retain the U-shaped map, 106 Sensors, 212 Thermocouple channels,
24 / 29 / 24 / 29, Cannon slots at I7 / I16; larger Sensor cells and text; smaller map centre;
two-column Sensor Detail; compact Active Job and GlobalQueue; Trend and Camera in the first
viewport; floating Diagnostics drawer; no page-level scrollbar at 1920 × 1080, Edge F11, 100 %
zoom. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0A](spikes/stage-0.2.1a-results.md#0a-fullscreen-operations-refinement-checkpoint-current).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci` (lock file restore), `tsc --noEmit` | PASS, 0 errors; dependencies unchanged |
| 2 | Vite production build | PASS |
| 3 | Vitest (jsdom), 12 files | 75 / 75 PASS (15 new: scale tokens, Diagnostics state, compact source reason, inspector, Active Job, mapping-independent cell classes, count and Cannon regressions) |
| 4 | Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 22 tests (5 new `LAYOUT-A`..`LAYOUT-E`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| 8 | Viewport fit, card bounds, computed cell sizes | **NOT VERIFIED** in Arena (no browser) — asserted by `e2e/layout.spec.ts`, Owner-local Edge run **PENDING** |
| 9 | Markdown relative links | PASS — 44 files, 696 links, 0 broken; 10 anchors, 0 broken |
| 10 | Sensitive-data scan (added lines), SHA-256 manifest | PASS / PASS (regenerated and verified) |
| 11 | Full diff review | Performed before the refinement commit |

### 12.6 Stage 0.2.1A Operations readability refinement — Arena validation record

Owner screenshot review of `ea23bc58`: status markers overlapped long Sensor IDs, small cells and
typography, oversaturated process colours, alarm and quality relying on colour, weak card
hierarchy. Presentation-only refinement plus the stale MAP U-shape assertion correction; the
U-map, mapping, counts, Cannon slots, right-side structure, bottom row, runtime, and contracts are
unchanged. Industrial HMI practice is guidance only; no standards certification is claimed.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0B](spikes/stage-0.2.1a-results.md#0b-operations-readability-refinement-checkpoint-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci` (lock file restore), `tsc --noEmit` (project + strict `e2e/*.ts`) | PASS, 0 errors; dependencies unchanged |
| 2 | Vite production build | PASS |
| 3 | Vitest (jsdom), 13 files | 93 / 93 PASS (18 new: colour tokens and contrast, typography and spacing tokens, cell zones, queue columns, detail groups, job phases and elapsed time, status groups, alarm strip, camera state) |
| 4 | Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 26 tests in 3 files; operations + layout selection 25 (was 21; 4 new `READ-A`..`READ-D`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| 8 | ID / marker overlap, clipping, typography, viewport fit | **NOT VERIFIED** in Arena (no browser) — asserted by `e2e/layout.spec.ts`, Owner-local Edge run **PENDING** |
| 9 | Markdown relative links and anchors | PASS (see the delivery report for counts) |
| 10 | Sensitive-data scan (added lines), SHA-256 manifest | PASS / PASS (regenerated and verified) |
| 11 | Full diff review | Performed before the readability commit |

### 12.7 Stage 0.2.1A final Owner UI punchlist — Arena validation record

Owner screenshot review of `4129687a` (Owner-local Edge E2E 25 / 25 PASS there). Presentation
changes, opt-in synthetic review tooling, and synthetic queue-source evidence; the U-map, mapping,
counts, I7 / I16 slots, right-side structure, bottom row, contracts, queue authority, FIFO model,
single-job rule, and dependencies are unchanged. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0C](spikes/stage-0.2.1a-results.md#0c-final-owner-ui-punchlist-checkpoint-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 325.93 kB (gzip 107.55 kB, +2.71 kB), CSS 28.16 kB (gzip 6.80 kB), one hashed WOFF2 47.67 kB |
| 3 | Vitest (jsdom), 15 files | 109 / 109 PASS (16 new: font asset and wiring, identity, legend bounds, Water Jet slots, queue label, synthetic control boundary) |
| 4 | Runtime harness `node:test` | 32 / 32 PASS (6 new: mixed queue, Water Jet refusal, default behaviour, presets, per-Sensor controls, token endpoint) |
| 5 | Scenario runner, 30 scenarios | PASS 25 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S29 mixed queue: 5 source types in the first 8 rows; S30 six presets) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 35 tests in 4 files (9 new: `FONT-A`, `CELL-A`, `IDENT-A`, `LEGEND-A`, `WJ-A`, `QUEUE-A`, `CTRL-A`..`CTRL-C`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 8 | Font provenance and licence | WOFF2 SHA-256 `40f917d9…1531b` and `OFL.txt` / `TRADEMARKS.md` Git blobs re-verified by test; OFL-1.1, no RFN; recorded in the licence inventory |
| 9 | Rendered font, cell zones, legend bounds, controls in a browser | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Public-repository boundary | Bundled font redistributable under OFL-1.1 with licence text; no Production Water Jet numbers, addresses, or credentials; per-run token files git-ignored; synthetic controls loopback-only and opt-in |
| 11 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.8 Stage 0.2.1A GlobalQueue semantics correction — Arena validation record

Owner domain correction after the Owner-local Edge run of `23f48daa` (34 selected · 25 passed ·
2 failed · 7 not run). The synthetic GlobalQueue now holds ready-to-dispatch entries only (≤ 8,
no entry states), dispatch is head-only and atomic with a synthetic dispatch record, and the
previous synthetic queue / job behaviour is **SUPERSEDED** (not eligible for production promotion).
Eligibility conditions are a proposal awaiting the Owner:
[queue eligibility decision matrix](spikes/queue-eligibility-decision-matrix.md) (every row
`OWNER DECISION REQUIRED`). A Production Queue Runtime is a Main Development slice (not authorized).
U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16 slots, fonts, and dependencies are unchanged.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0D](spikes/stage-0.2.1a-results.md#0d-globalqueue-semantics-correction-and-head-only-dispatch-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 327.90 kB (gzip 108.05 kB), CSS 27.67 kB (gzip 6.68 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 16 files | 120 / 120 PASS (new `globalQueue.test.tsx`; Sensor-cell vertical budget guard) |
| 4 | Runtime harness `node:test` | 40 / 40 PASS (new `queueDispatch.test.mjs`: gates A–F) |
| 5 | Scenario runner, 32 scenarios | PASS 27 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S31 head-only dispatch on the Owner example queue; S32 bounded queue under `dirty70`) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 36 tests in 4 files; Owner-local selection 35 (new `QUEUE-B`; `QUEUE-A`, `CTRL-A`, `CTRL-B`, `WJ-A` rewritten) |
| 7 | Queue capacity / dispatch gates A–F | PASS in Arena (harness + scenarios); UI gate E in jsdom; browser part Owner-local **PENDING** |
| 8 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 9 | Value clipping (`READ-A`), Water Jet clicks (`WJ-A`), rendered queue panel | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.9 Stage 0.2.1A critical Main Pump and Mandatory Safe Return — Arena validation record

Owner critical Pump decision on `60cd0398` (recovered by the Owner-authorized one-time recovery,
identity proof 147 / 147). In the synthetic spike:

- The Main Pump is a High Critical device.
- An unexpected stop or trip stops progression, water and dispatch. The AutoSequence becomes
  `CRITICAL_SUSPENDED`, an Active Job enters Mandatory Safe Return, and a blocking critical modal
  (alertdialog, Acknowledge only, no close, no Resume) opens.
- Every Job outcome ends through Mandatory Safe Return. The valve close is commanded and confirmed
  before the axis Standby command, and the Job stays Active until Standby is confirmed.
- The GlobalQueue remains ready-only and is frozen while suspended.

Queue-level `HELD` is superseded in [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2 and
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) (Owner-authorized). Unresolved policies are listed in the
[critical Pump / Safe Return decision matrix](spikes/critical-pump-safe-return-decision-matrix.md)
(every row `OWNER DECISION REQUIRED`).

**SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED.** No safety certification is claimed.

U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16 slots, fonts, and dependencies are unchanged.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0E](spikes/stage-0.2.1a-results.md#0e-critical-main-pump-handling-and-mandatory-safe-return-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 340.50 kB (gzip 111.65 kB), CSS 30.92 kB (gzip 7.34 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 17 files | 131 / 131 PASS (new `criticalModal.test.tsx`; critical palette distinctness test) |
| 4 | Runtime harness `node:test` (× 3) | 51 / 51 PASS each run (new `safeReturn.test.mjs`: gates 1–6; critical-alarm acknowledge route) |
| 5 | Scenario runner, 34 scenarios | PASS 29 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S33 Pump trip during a Job; S34 normal completion with delayed valve feedback) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 43 tests in 5 files; Owner-local selection 42 (new `critical.spec.ts`: `CRIT-A`..`CRIT-E`, `CRIT-F` × 2 viewports) |
| 7 | Safe Return ordering, completion / abort / trip, failure, queue-during-suspension gates | PASS in Arena (harness + scenarios + validator); modal structure in jsdom; browser part Owner-local **PENDING** |
| 8 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 9 | Modal rendering, geometry, focus trap and Escape in Edge | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.10 Stage 0.2.1A final spike closeout (synthetic AutoSequence controls) — Arena validation record

Owner final closeout on `81c87a44` (recovered by the Owner-authorized one-time recovery, identity
proof 153 / 153) after the Owner-local Edge run of `81c87a44` (42 selected · 32 passed · 1 failed
`S11/S12` · 9 not run). In the synthetic spike:

- `S11/S12` is deterministic: the Sensor's Dirty Score is fixed and held (Score 82) and Score,
  classification and the computed background must stay identical through Raise / Clear /
  Acknowledge (no RGB tolerance).
- Diagnostics-only synthetic AutoSequence controls (`SYN · ` labels, `--synthetic-test-controls`):
  START AUTOSEQUENCE, PAUSE AFTER CURRENT JOB, RESUME AUTOSEQUENCE, ABORT ACTIVE JOB, and RESET
  CRITICAL SCENARIO. Dispatch is always GlobalQueue Position 1; every Job ends through Mandatory
  Safe Return; clear + acknowledge never resume; RESET never dispatches and requires an explicit
  START. These are **not** the Production operator-control model.
- Current-facing queue-level `HELD` / `BLOCKED` / `EXCLUDED` wording is removed; ADR-0003 is
  **SUPERSEDED IN PART by Owner decision dated 2026-10-06**. Production Pause / Resume, abort
  re-queue and Reject / Reorder in the ready-only queue are **OWNER DECISION REQUIRED**.

Critical Pump / Safe Return behaviour, U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16
slots, fonts, and dependencies are unchanged. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0F](spikes/stage-0.2.1a-results.md#0f-final-spike-closeout-synthetic-autosequence-controls-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the 6 known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 345.17 kB (gzip 112.92 kB), CSS 31.70 kB (gzip 7.46 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 18 files | 138 / 138 PASS (new `autoSequenceControl.test.tsx`, 7 tests) |
| 4 | Runtime harness `node:test` (× 3) | 60 / 60 PASS each run (new `autoSequenceControls.test.mjs`: A–G) |
| 5 | Scenario runner, 35 scenarios | PASS 30 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (new S35; S09 expects `PAUSE_REQUESTED` with an Active Job) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 49 tests in 5 files; Owner-local selection 48 (`SEQ-B`..`SEQ-G` in `critical.spec.ts`; `S11/S12` rewritten) |
| 7 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 8 | Controls, alarm colour and modal in Edge | **NOT VERIFIED** in Arena (no browser) — Owner-local final Edge gate **PENDING** |
| 9 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.11 Stage 0.2.1A Owner-local final review and documentation closeout

Owner decision dated 2026-10-07 on checkpoint `114c07619f9fd249cd80b2e7a5f385ff9e081119`
(Owner-reported; the Agent received the outcome, not the raw Edge output or screenshots):

| Item | Recorded result |
| --- | --- |
| Owner-local final Edge gate | **PASS** |
| Owner manual review | **PASS** |
| Development baseline | UI, synthetic AutoSequence controls, GlobalQueue presentation (ready-only, ≤ 8, head-only dispatch), critical Pump modal and Mandatory Safe Return behaviour **accepted** |
| Primary UI Framework | **React selected** |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Controlled 15-minute and 60-minute observations | **Waived as Stage 0.2.1A merge blockers** (not run) |
| Stage 0.2.1A | **COMPLETE FOR DEVELOPMENT CHECKPOINT**; PR #3 **ready for Owner merge**, **NOT MERGED** |
| Main Development Scope Gate | **PENDING** — no Main Development coding has started |
| Production device access | **NOT AUTHORIZED** |

Not claimed: Production safety, Production stability, WebView2 validation, kiosk validation,
Modbus performance, hardware validation. Synthetic AutoSequence controls remain review tooling,
not the Production operator-control model; Production Pause / Resume, abort re-queue and reset
authority remain **OWNER DECISION REQUIRED**.

The closeout commit is documentation-only. It also removed the remaining current-facing
queue-level Hold wording in `CONTROL_AUTHORITY.md` (§4 invariant, §6 conflict table),
`QUEUE_MODEL.md` (snapshot contents), `TEST_STRATEGY.md` (operator-action tests) and
`USER_PERMISSION_MODEL.md` (queue permissions), and updated the status banners. No runtime, React,
CSS, contract, fixture, harness, scenario, E2E, dependency or Sensor-map file changed.

### 12.12 Stage 0.3A-1 Product Foundation Source Checkpoint — Arena validation record

Validation actually available inside Arena, executed 2026-10-07 on the final tree state (this
record's tables are the authority for what was and was not checked):

| # | Check | Tool / method | Result |
| --- | --- | --- | --- |
| 1 | TypeScript mirror typechecks | `npx tsc --noEmit` (strict) | **PASS** |
| 2 | TS structural tests | `node --test test/validate.test.mjs` | **14/14 PASS** |
| 3 | Committed fixtures validate structurally | validator CLI over `packages/contracts/fixtures/` + `config/examples/` | **PASS** (snapshot 0 issues; delta.basic gapless; delta.gap flagged) |
| 4 | All repository JSON parses | `node` parse sweep of tracked `.json` (excl. `node_modules`) | **PASS** |
| 5 | Boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` | **0 findings** (S1–S7) |
| 6 | Product tree references spike source | scanner rule S5 (code files only) | **PASS** — zero code references; prose boundary notes allowed |
| 7 | Solution completeness | scanner rule S7: every `*.csproj` (non-spike) listed in `WaterJetSentinelSuite.sln` | **PASS** (12/12, no ghosts, braces balanced) |
| 8 | Approved NuGet set only | read-back of `Directory.Packages.props` + all csproj `<PackageReference>` | **PASS** — exactly the three approved test packages; kiosk references no package; labels say PROPOSED/UNVERIFIED |
| 9 | `.gitignore` product/packages conflict | corrected rules + `git check-ignore` probes; fixture/example files confirmed trackable without force-add | **PASS** |
| 10 | No lock-file fabrication | `git ls-files` review; only npm-real lockfile (produced by genuine `npm install`) tracked; no `packages.lock.json` anywhere | **PASS** |
| 11 | Spike tree immutability | sha256 roll over `spikes/**` compared to pre-work baseline | **PASS — identical** (see commit report) |
| 12 | Whitespace hygiene | `git diff --check` | **PASS** (no errors) |
| 13 | Secret-shaped literals | scanner rule S4 across repository text | **PASS** (0) |
| 14 | Relative-link check | node sweep of markdown links in changed/new docs | **PASS** |

**NOT PERFORMED (impossible in Arena, not failures):** `dotnet restore/build/test`, fixture
regeneration via the .NET generator, Runtime/kiosk execution, NuGet pin resolution. See
[`STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) — that run,
reported verbatim into the PR, is the mandatory pre-merge gate.

### 12.13 Stage 0.3A-1 Owner source-review correction — Arena validation record

Owner review of `205456e` returned findings A–E; this checkpoint addresses all five as a
source correction on PR #4 (commit message `fix: correct Stage 0.3A-1 source checkpoint
blockers`). Validation actually executed in Arena, on the corrected tree:

| # | Check | Result |
| --- | --- | --- |
| A-1 | `Directory.Packages.props` XML comment double-hyphen (`--locked-mode`) | **REMOVED** — comment reworded; file parses |
| A-2 | New scanner rule **S8 XML well-formedness** over `*.csproj/*.props/*.targets/*.manifest/*.resx/*.config` (comments: no `--`, terminated, not `-`-ended; attribute quoting; element nesting; stray `<`) | **PASS — repo 0 findings**; negative controls (bad comment, unquoted attr, unclosed element) each flagged, exit 1 |
| B | `activeJobCleared` removed from C# contracts, TS types, validator, fixtures, tests, ADR and current-facing docs; three-state `activeJob` restored via `Optional<T>` + scoped converter; `delta.basic` fixture now exercises the explicit-null clear key (generator updated in lockstep) | **DONE** — C# compile/test NOT RUN (Arena); TS **15/15 PASS** incl. required-test-6 (three-state acceptance + flag rejection) |
| E | `xunit.runner.visualstudio` carries `PrivateAssets=all` + standard `IncludeAssets` list at the `PackageVersion` (CPM flow); versions unchanged; no lock files generated | **DONE** (static read-back) |
| C/D | Current-facing banners aligned in the 12 authorized files; README React-selection wording corrected (historical sentences labeled) | **DONE** — ad-hoc consistency sweep green |

Still NOT claimed: any .NET restore/build/test result (sandbox unchanged). Fixtures remain
`PROVISIONAL STRUCTURAL FIXTURE` pending the Owner-local regeneration.

### 12.14 Stage 0.3A-1 Owner-local compile correction — Arena validation record

Owner-local Release build of `355c064` (run twice, identical): **BUILD FAILED** —
`Wjss.Kiosk` WFO0003 (manifest DPI) and `Wjss.Contracts` CA2231 (`Optional<T>` operators);
`Wjss.Time` compiled. Corrected on PR #4 as source only (no suppressions, no severity or TFM
changes, no package edits, no lock files; contract wire semantics untouched): manifest
stripped of DPI elements (compatibility retained), `ApplicationHighDpiMode=PerMonitorV2` on
the kiosk project, generated-bootstrap entry point, `operator ==`/`!=` on `Optional<T>`
mirroring `Equals` exactly. New guards: scanner rule **S9** (DPI placement matrix, 0
findings; negative controls flag every violation) and xUnit static tests
(`KioskDpiConfigurationTests` ×3, `OptionalPresenceTests` ×7 — authored, NOT RUN in Arena).
Arena re-validation after correction: S1–S9 clean, TS 15/15, links green, spikes hash
unchanged. **Status remains: .NET build NOT VERIFIED — Owner-local re-run required.**

### 12.15 Stage 0.3A-1 Owner-local build round 3 — Arena validation record

Owner evidence (2026-10-07): round-2 cascade root-caused to a **process-scoped `TargetPath`
environment override** injected by an external motion-control toolset; after removal, ten
projects compiled clean (Time, Kiosk, Contracts, Domain, Adapters.Simulator, Runtime.Core,
Runtime.Api.Tests, Domain.Tests, FixtureEmission.Tests, Runtime.Core.Tests) — ProjectReference
paths confirmed correct. Two genuine defects remained and are corrected as source on PR #4:
missing `using Xunit;` in `KioskDpiConfigurationTests.cs` (per-file convention kept; no
GlobalUsings), and CA1873 in the Runtime startup log (now `IsEnabled(LogLevel.Information)`
guard; template, level, structured payloads, and every stub behaviour untouched — pinned by
four new static source tests, `RuntimeStubPreservationSourceTests`). Repository policy
recorded: no vendor paths, no `TargetPath`/`ReferencePath` overrides, no MSB3245 suppression;
runbook §0A makes the environment check mandatory; restore-generated `packages.lock.json`
files remain untracked pending full-pass Owner assessment. Arena static validation after
correction: S1–S9 zero findings, TS 15/15, links green, `git diff --check` clean, spikes roll
unchanged. **Status: first partial compile PASS is Owner-local evidence only; the full
Release build + test rerun remains REQUIRED. No .NET success is claimed in this record.**

### 12.16 Stage 0.3A-1 Owner-local build round 4 — Arena validation record

Owner evidence on `c16804c` (TargetPath removed from the build process): **11 projects
compile** — Time, Kiosk, Contracts, Adapters.Simulator, Domain, Runtime.Api.Tests,
FixtureEmission.Tests, Runtime.Core, Domain.Tests, Runtime.Core.Tests, **Runtime** — proving
the ProjectReference graph, the DPI correction, `Optional<T>`+converter, and the CA1873
guard. Sole remaining failure: `Wjss.Config.Examples.Tests` CS0103 ×2 — cross-namespace use
of `ExampleConfigTests.RepoRoot()`. Fixed per Owner preference with a stable assembly anchor:
`ConfigTestAssemblyMarker` + shared internal `ConfigTestPaths.RepoRoot()` (walk-up to the
solution file from `typeof(...).Assembly.Location`, behaviourally identical, repository
SOURCE inspected, not build output); both preservation tests switched; `ExampleConfigTests`
untouched; no fake classes, no new dependencies, no path or assertion changes. Arena static
validation after correction: boundary scan 0 findings (S1–S9), anchor type count = 1, zero
unresolved `ExampleConfigTests` code references, JSON 10/10, TS 15/15 (npm ci → typecheck →
node --test), links green, `git diff --check` clean, spikes roll unchanged, no KMotion/
TargetPath/vendor strings in Product source. **Status: .NET build of the config test project
and all test executions remain Owner-local; full Release build rerun REQUIRED.**

### 12.17 Stage 0.3A-1 Owner-local build & contract-test correction round 5 — Arena validation record

Owner evidence (2026-10-07): full-solution Release build **not yet passing**; the subsequent
`dotnet test --no-build` (49 total · 42 pass · 7 fail · 0 skip) is recorded **non-authoritative**
— stale assemblies, including a config-test "success" despite that project not compiling in
the preceding build. Source corrections on PR #4: (1) round-4 assembly-anchor mechanism
verified complete (marker once; both preservation tests share `ConfigTestPaths.RepoRoot()`; no
fake `ExampleConfigTests`; repository-source discovery unchanged); (2) the three failing
serializer tests rewritten to presence-aware `JsonDocument`/`JsonElement` assertions
(absent = `TryGetProperty` false / null = present + `JsonValueKind.Null` / object = present +
`JsonValueKind.Object`), covering post-serialization and post-deserialization stability of all
three `activeJob` states — wire contract untouched; (3) `Optional<T>` present-payload
equality and hash delegated to `EqualityComparer<T>.Default` (fixes equal-records split by
reference comparison; operators still exactly mirror `Equals`; `Present(null)` still rejected;
serialization unchanged), with delegation-focused tests added; (4) fixture drift on the three
named files retained as **expected provisional evidence** — Arena modified no fixture; the
runbook now gates regeneration behind full-build PASS + all non-parity tests PASS. Arena static
validation: S1–S9 zero findings; anchor/JSON checks green; TS 15/15 (npm ci → typecheck →
node --test); links and whitespace clean; spikes roll unchanged. **No .NET build/test success
claimed; Owner-local full Release build + fresh-assembly test run REQUIRED.**

### 12.18 Stage 0.3A-1 thermocouple channel contract shape correction (round 6) — Arena validation record

Owner-local progress (2026-10-07, rounds 3–5): full Release build **0 warnings / 0
errors**, corrected checkpoint suite **50/50**, fixture/delta parity **7/7**; the earlier
`--no-build` 42/49 stays recorded as non-authoritative. Residual drift on the three
provisional files led to a confirmed shape defect: `tcChannels` was a comma-delimited
scalar string in the Arena-authored sensor-map example (its test split it). Correction on
PR #4: structured two-entry array per SENSOR slot (deterministic front-then-rear order),
absent on CANNON slots; `SensorMapSlotExample` + `TcChannelRules` (`packages/contracts/
TcChannels.cs`) make the shape contractual and reject every invalid form; the generator
emits arrays and self-validates through the rules before serializing; the committed
example file was regenerated in array form for TypeScript validation only (still
PROVISIONAL — Owner regeneration after a full Release build remains mandatory and the
comma-delimited fixtures are kept as evidence); snapshot/delta presentation keeps
`tcFrontChannel`/`tcRearChannel` unchanged. ADR-0014 decision 8 records the rule and the
rejection history. Arena static validation: S1–S9 zero findings; JSON parse green; TS
24/24 (npm ci → typecheck → node --test); links and whitespace clean; spikes roll
unchanged. Round-6 C# is compile-reviewed only, **not built or tested in Arena**; Owner
must rerun the full sequence including the fixture regeneration gate.

### 12.19 Stage 0.3A-1 round 6 Owner-local test correction — Arena validation record

Owner-local round-6 validation (2026-10-07): the tcChannels structured-array contract is
VERIFIED by the .NET generator (108/106/2 slots; 212 total and 212 unique channels; 0
duplicates; 0 scalar sensors; cannons I7/I16; walls 24-29-24-29; CLR shape `System.Object[]`
with JSON two-string arrays), TypeScript 24/24, parity 7/7 after regeneration, boundary scan
S1–S9 clean. The `--no-build` 49/50 suite count is **non-authoritative** —
`Wjss.Config.Examples.Tests` did not rebuild (CA1861 in the new channel tests) and a stale
assembly ran the pre-correction `GetValue<string>()` assertion against the now-array
`tcChannels` node. Source-only corrections on PR #4: inline constant arrays extracted to
named `private static readonly` fields (no suppression, `TreatWarningsAsErrors` untouched),
and `ExampleConfigTests` now asserts the JsonArray shape directly (array cast required —
scalar strings fail as shape errors; two non-empty string items per sensor; 212 unique;
cannons key-free), keeping canonical counts and the typed `TcChannelRules` verification.
Arena static validation green; C# remains compile-reviewed only. Owner-local clean Release
build + fresh full-suite pass REQUIRED before merge.

### 12.20 Stage 0.3A-1 round 6c structural JSON assertion correction — Arena validation record

Owner-local at `0b088ad`: Release build **PASS 0 warnings / 0 errors**; fresh full suite
**61 total / 59 passed / 2 failed / 0 skipped**. Both failures were formatting-sensitive
test assertions in `tests/config.tests/TcChannelContractTests.cs` that "proved" the scalar
form was absent by matching serialized text across the indented writer's whitespace; the
tcChannels contract itself is Owner-verified and frozen (exact-two-element arrays,
108/106/2, 212 = 212 unique, I7/I16, 24-29-24-29, TS 24/24, parity 7/7, S1–S9 clean).
Round 6c replaced every shape claim with structural `JsonDocument`/`JsonValueKind`
inspection (presence, Array-kind, exactly two String-kind items, non-empty and
whitespace-pure, distinct, deterministic front-first order asserted on channel VALUES,
cannon key absence, full canonical mapping totals), and a defect-class sweep removed the
last two same-class serialized-text assertions elsewhere
(`Delta_RequiredTest3` redundancy; `ExampleConfigTests` deviceProfile text guards now
recursively scan parsed JSON). Contract, generator, serializer, fixtures, examples,
TypeScript mirror and lock files unchanged; no suppressions; `TreatWarningsAsErrors`
enabled. Arena mirrored the structural assertions in Python against the committed example:
all pass. Owner-local **final build + fresh full-suite rerun** REQUIRED before merge; no
.NET/xUnit success claimed in Arena.

### 12.21 Stage 0.3A-1 round 6d cannon logical ordering test correction — Arena validation record

Owner-local at `39577e2`: Release build **PASS 0/0**; fresh full suite 61 / **60 passed /
1 failed** / 0 skipped; parity 7/7; boundary scan S1–S9 clean. The lone failure —
`Example_Mapping_Validates_Through_The_Typed_Contract` — was a TEST-ordering defect, not a
mapping/contract defect: cannon labels were compared in lexicographic order
(`OrderBy(label)` → `I16, I7`) instead of the accepted logical order (`I7, I16`). Round 6d
fixes the assertion pipeline to sort cannon SLOTS by structured position
(`LogicalRow`, then `LogicalColumn`), keeps the expected `[7, 16]` / `["I7","I16"]`
sequences, adds explicit per-slot I7/I16 row/column/equipment pairing and a cannon
`sensorId`-absence check, and retains every canonical count. Ordering defect-class sweep
across the Stage 0.3A-1 test tree found no other lexicographic sorting of labels/IDs, no
label numeric parsing where structured fields exist, and no set/sequence confusion;
the only other `OrderBy` sites sort by structured numeric fields or parse inputs for the
product API under test. Product source, contracts, fixtures, TypeScript mirror, lock files
and spikes unchanged (zero diff); expectations were not flipped to match text sorting and
no sorting dependency was added. Arena mirrored the corrected pipeline in Python against
the committed example: structured ordering yields I7→I16 while lexicographic yields
I16→I7, confirming the defect and its correction. Owner-local **final build + fresh
full-suite rerun (61/61)** REQUIRED before merge.

### 12.22 Stage 0.3A-1 final Owner-local evidence closeout — PASSED

**STAGE 0.3A-1 OWNER-LOCAL VALIDATION PASSED** (Owner, 2026-10-07; authoritative because
the full Release build completed successfully immediately before the fresh full-suite
run). Final Release build: PASS, 0 warnings / 0 errors, all 12 projects, 3.7 s. Final
fresh .NET suite: 61 / 61 passed / 0 failed / 0 skipped (5.5 s). SDK 10.0.401, xUnit
observed runtime .NET 10.0.12. TypeScript 24/24; FixtureEmission parity 7/7; boundary
scan S1–S9 0 findings. Health: live 200 (ALIVE / SIMULATOR / runtimeImplemented=false /
STAGE_03A1_SKELETON), ready 503 (RUNTIME_NOT_IMPLEMENTED). Profile gates fail-closed:
SIMULATOR starts; TEST_HARDWARE exit 2; PRODUCTION exit 2; invalid profile exit 4; no
silent fallback with `--no-launch-profile`. Port collision on 5181: refusal, exit 3. The
tcChannels contract re-verified: 108/106/2, exact-two-element arrays (CLR
`System.Object[]`), 212 = 212 unique, 0 duplicates, 0 wrong-count sensors, cannons
I7/I16 channel-free, walls 24-29-24-29. Closeout commits: handoff `488b98fb…` (12
genuine `packages.lock.json` + 3 .NET-generated fixtures — verified in Arena against the
full static battery; Arena never generated or modified them) and the documentation
closeout (`global.json` pin `10.0.401` / `latestPatch` from Owner-validated evidence;
test policy recorded in the runbook §11; TargetPath process-scope prerequisite retained in
§0A; CHANGELOG placement sentence, ADR-0016 environment-probe residue and the
fixtures-README section pointer corrected; dated per-round records untouched). **PR #4:
READY FOR OWNER MERGE — NOT MERGED. Stage 0.3A-2 NOT AUTHORIZED. Production device
access NOT AUTHORIZED.**

### 12.23 Stage 0.3A-2A Checkpoint A — Arena validation record (source-only)

**Scope.** Owner-authorized Stage 0.3A-2 Checkpoint A (Runtime State Foundation,
SIMULATOR-only) authored in Arena. Arena executed **no .NET command**: the sandbox has no .NET
SDK and no NuGet endpoint is reachable, so no restore, build, or test result is claimed. The
C# position is **SOURCE AUTHORED / STATICALLY REVIEWED / NOT COMPILED IN ARENA / NOT EXECUTED
IN ARENA**, and any statement that Stage 0.3A-2 is "delivered" carries the qualifier
*development checkpoint submitted for review, pending Owner-local validation*.

| # | Check | Command / method | Observed result |
| --- | --- | --- | --- |
| 1 | Approved base | `git ls-remote origin refs/heads/main` | `bdf7f8f277b85087e3e53c067fe956fca7e04f90` — equals the approved base, so no Baseline-mismatch Stop Gate applied |
| 2 | Boundary scan (pre-change) | `node tools/boundary-scan/boundary-scan.mjs .` on the clean worktree | `boundary-scan: 0 findings (S1-S9 clean)`, exit 0 |
| 3 | Boundary scan (changed worktree) | same command with all Checkpoint A changes present | `boundary-scan: 0 findings (S1-S9 clean)`, exit 0 |
| 4 | C# sanity sweep | brace/paren balance script, final-newline check, trailing-whitespace and tab greps over every changed file | clean (no findings) |
| 5 | Diff hygiene | `git diff --cached --check` | clean |
| 6 | Contract cross-check | manual review of every referenced contract member against `packages/contracts` (`WallMapSlot`, `SensorPresentationState`, `WallSummary`, `QueueSummary`, `DispatchRecord`, `PumpState`, `SequenceState`, `AlarmSummary`, `CommunicationHealth`, `TrendWindow`, `RuntimeHealth`, `OperationalSnapshot`, `PublishedConfigurationRevision`, `Optional<T>`, `ContractJson`, `CanonicalSensorMap`, `TcChannelRules`) | all referenced members exist with the used shapes; no wire contract was changed |
| 7 | Synthetic-map parity artifact | re-parse of `config/examples/sensor-map.example.json` (108 slots, 106 Sensors, 2 Cannons at I7/I16, `scanOrderSynthetic` 1–106, `SYN-TC-nn:CHmm` pairs) | the adapter map reproduces slot identity, scan order, device distribution and channel identities; the parity assertion is authored in `tests/runtime.tests` and **not executed in Arena** |
| 8 | TS mirror | not re-run | No TypeScript or fixture file changed in Checkpoint A, so the mirror and its validator were not exercised; the last executed result remains the Owner-local 24/24 (§12.22) |

**Authored-time statements superseded.** The sentence in §12.22 ("Stage 0.3A-2 NOT AUTHORIZED")
records the 0.3A-1 closeout position and is preserved as authored-time state; the Owner's
2026-10-07 instruction recorded in §11.5 authorizes Checkpoint A of Stage 0.3A-2. Production
device access and Production Write remain **NOT AUTHORIZED**.

### 12.24 Stage 0.3A-2A Checkpoint A — Owner-local validation record (PASSED)

**Result (Owner-reported, 2026-10-07; authoritative because the Release build completed
successfully immediately before the test run).** Validated feature head
`a512aa76c4b2d7633d2a83b10c523c318b3a420e`; genuine lock-refresh commit
`55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c`. .NET SDK **10.0.401** (xUnit observed runtime
**.NET 10.0.12**).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | Release build (`dotnet build -c Release --no-restore`) | **PASS — 0 warnings, 0 errors** |
| 2 | Full .NET test suite | **101 total / 101 passed / 0 failed / 0 skipped** |
| 3 | Locked restore | **PASS** |
| 4 | Boundary scan (S1–S9) | **0 findings** — clean |
| 5 | Working Tree after the Owner-local lock commit and push | **CLEAN** |
| 6 | Lock-refresh scope | Only `tests/runtime.tests/packages.lock.json` changed; adds the `wjss.adapters.simulator` Project dependency with `Wjss.Contracts` + `Wjss.Time`; no absolute Owner-local path; no unexpected source or fixture change |
| 7 | Pre-validation correction chain (Owner-local rounds, Arena-authored) | `b389805` CA1859 → `93e8f24` CS0051 → `b75bccb` synthetic-example test compile (CS1061 + CA1859) → `a512aa7` SensorChannels tamper isolation; each pushed as a focused commit on the same PR #5 branch |

**Arena position.** Arena authored the Checkpoint A sources and corrections, ran the static
battery (boundary scan, brace/whitespace/final-newline checks, `git diff --check`, contract
cross-checks) and **ran no .NET command**: the validated build, locked restore and 101/101 suite
are Owner-local evidence. Checkpoint A is complete for a development checkpoint and **NOT
MERGED**; only the Owner merges.

**Deliberate scope boundaries recorded at closeout.** Checkpoint A wrote no documentation of
its own corrections (per the strict correction scope), so the four correction SHAs above are
recorded here by this evidence commit and in the PR #5 review comments; the fixture generator
and the committed fixtures remain untouched (regeneration stays an Owner-local gate); the
`FixtureGenerator` ↔ adapter unification remains `[OPEN]`.

### 12.25 Stage 0.3A-2A Checkpoint B — Owner-local validation record (PASSED)

**Result (Owner-reported, 2026-10-07; authoritative because the Release build completed
successfully immediately before the test run).** Validated head
`cfa6d4a376bbf10a87db2349045cb6b9b57bb544` (the consolidated correction of the
`feat(runtime): add deterministic synthetic evolution and snapshot/delta foundation`
checkpoint), on the Owner-locally validated Checkpoint A store.

| # | Check | Observed result |
| --- | --- | --- |
| 1 | Release build (`dotnet build -c Release --no-restore`) | **PASS — 0 warnings, 0 errors** |
| 2 | Full .NET test suite | **129 total / 129 passed / 0 failed / 0 skipped** |
| 3 | Locked restore | **PASS** |
| 4 | Lock drift | **NONE** |
| 5 | Boundary scan (S1–S9) | **0 findings** — clean |
| 6 | Working Tree after the run | **CLEAN** |
| 7 | Correction rounds validated by this run | `dd45bf6` (CS0102: `DeltaApplyOutcome` factory renamed `Success`, property `Applied` retained) → `8692b77` (test-source alignment: named `seed:` arguments; four CA1861 constant arrays hoisted) → `cfa6d4a` (consolidated: catch-up continuation lookup + five test-side corrections) |

**What the consolidated correction actually changed (recorded truthfully from the final diff).**

*Product behaviour — one method.* `RuntimeDeltaHistory.CatchUpFrom` walked the chain with a
lookup that matched a Delta by its **own** revision, so it selected the step the consumer had
already applied. That produced a spurious fresh-Snapshot result when that step was no longer
retained, and a non-terminating walk when it was. The walk now selects the Delta that
**continues** the consumer revision (`FindContinuingUnlocked`, matched on `previousRevision`);
gap detection, eviction, ordering, the public `Find` semantics and the composed revision
sequence are unchanged. This is the only Product behaviour change of the three rounds.

*Test-side corrections — no Product behaviour involved.* Two structural `TrendPoint`
comparisons now use the single shared helper `RuntimeTestFixture.AssertTrendPointsEquivalent`
(`TrendPoint.Series` is `double?[]`, so record equality compared the array by reference and two
value-identical points were never equal); and three revision expectations were re-derived
(the committed Delta chain starts at revision 2, so capacity-3 retention is 4/5/6 and a
catch-up input of revision 3 advances to revision 6 — the earlier values were off by one).

**Arena position.** Arena authored the Checkpoint B sources and all three correction rounds,
ran the static battery (boundary scan, structural/whitespace checks, `git diff --check`,
contract cross-checks, a mechanical re-derivation of the revision and history arithmetic) and
**ran no .NET command**: the build, locked restore and 129/129 suite are Owner-local evidence.
Checkpoint B is complete for a development checkpoint and **NOT MERGED**.

### 12.26 Stage 0.3A-2A Checkpoint C — Arena validation record (source-only)

**Arena did not restore, build, or execute any .NET code.** The Checkpoint C sources are
**authored and statically reviewed only**; the Owner-local Function/Logic/UI review per runbook §14
is the validation of record, and no result is claimed here.

| Item | Checked in Arena |
| --- | --- |
| Boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` — exit 0, **0 findings (S1–S9)** on the Checkpoint C worktree |
| Inspector script | the single inline script of `apps/runtime/Inspector/index.html` extracted and passed to `node --check` (exit 0); HTML tag balance verified; the page requests only `/api/v1/snapshot`, `/api/v1/runtime`, `/api/v1/deltas` with `GET`, and caps retained Delta rows |
| C# source shape | per-file brace-depth balance over every changed/new C# file; `git diff --check`; trailing-whitespace, final-newline and tab sweeps |
| Contract cross-check | every referenced type and member cross-checked against `packages/contracts` and `packages/application/Runtime` (status/feed/sensor projections, `QueueSummary`, `AlarmSummary`, `PumpState`/`PumpRunState`, `DeltaJobEncoding`, `RuntimeLimits` capacities, `RuntimeStateComposer.InitialRevision`, `RuntimeStoreCounters`, `RuntimeDeltaHistory` accessors, `IClock`/`SystemClock`, `ProfileStartPolicy`) |
| Namespace hygiene | duplicate-simple-name scan across `Wjss.Contracts`, `Wjss.Runtime` and `Wjss.Runtime.Core` (none); every `using` directive resolved to a real namespace used by the file |
| Concurrency review | the evolution loop is the only tick caller; `RuntimeDeltaHistory` accessors lock internally and return frozen copies; counters are read through `Interlocked`; `DisposeAsync` observes the loop without rethrowing |
| Test sources authored | `tests/api.tests/RuntimeHostCompositionTests.cs` (7 facts), the rewritten `tests/config.tests/RuntimeStartGateSourceTests.cs` (6 facts, replacing the 4-fact stage-0.3A-1 stub pin) and the in-place `tests/runtime.tests/StageMarkerTests.cs` marker update (2 facts): **9 new tests for a source total of 138** (129 Owner-locally validated + 9). **Not executed anywhere yet** |
| NOT verified | every .NET claim; the two expected lock-file refreshes (`apps/runtime/packages.lock.json`, `tests/api.tests/packages.lock.json`); the browser behaviour and layout of the Inspector; anything the Owner-local run has not yet observed |

Slice C1+C2 is commit `06aca79d03c57b703768afffecf95735de7a91d5`; the C3 slice (Inspector page, Inspector source test, runbook §14
and this record) is the commit immediately following it and — per [`AGENTS.md`](../AGENTS.md)
§4.6 — does not record its own SHA.

## 13. Required positive confirmations

The documentation explicitly contains each of the following:

| # | Statement | Where |
| --- | --- | --- |
| 1 | One Water Jet to one Isolation Valve, dedicated, never shared | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8, [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001 |
| 2 | At most one Cleaning Job ACTIVE at a time | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-001 |
| 3 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 4 | GlobalQueue stop and rebuild policy | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9 |
| 5 | Source ownership after deduplication | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3 |
| 6 | Cleared-state acknowledgement requirement | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 7 | DCS communication health | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 8 | DCS Permissive Override restrictions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009 |
| 9 | Operations UI close guard | [`ARCHITECTURE.md`](ARCHITECTURE.md) §4.12, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 10 | Production Write not authorized | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §4, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-002 |
| 11 | WAGO fail-safe not verified | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-001 |
| 12 | The UI cannot write to hardware and owns no device session | [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.3, [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-001 to ARC-003 |
| 13 | The Equipment Runtime is the only physical-device-session owner | [`decisions/ADR-0007`](decisions/ADR-0007-runtime-process-model.md), [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.2 |
| 14 | Domain logic does not depend on vendor device libraries | [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-007, [`decisions/ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) |
| 15 | Simulation is not hardware verification or certification | [`decisions/ADR-0012`](decisions/ADR-0012-simulator-first-development.md), [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §3.7 |

Apart from the Stage 0.2.1A synthetic spike validation recorded in section 12.3 (Arena, Node
and jsdom only), this document must not be read as claiming any test, build, database,
hardware, or device verification. See section 9.

---

## Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and implementation status
- [`ROADMAP.md`](ROADMAP.md) — forward view
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — current prohibition on control writes
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — verification levels and what has not been tested
