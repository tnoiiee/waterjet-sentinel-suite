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
| Stage 0.3A-1 .NET validation | **PENDING — mandatory Owner-local pre-merge gate** ([runbook](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md)); no build/test/run success claimed in Arena |
| Stage 0.3A-2 | **NOT AUTHORIZED** |
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
| Current stage | Stage 0.3A-1 — Product Foundation Source Checkpoint (authored; Owner-local .NET validation PENDING) |
| Repository contents | Documentation, repository governance, the Stage 0.2.1A synthetic spike in `spikes/ui-runtime-react/`, and the Stage 0.3A-1 product foundation source skeleton (`WaterJetSentinelSuite.sln`, `packages/`, `apps/`, `adapters/`, `tests/`, `config/examples/`, `tools/`) |
| Application source code | **Authored, unvalidated.** The Stage 0.3A-1 skeleton exists as source only — no .NET compile, restore, test, or execution success is claimed (blocked in Arena; Owner-local gate pending). The spike remains synthetic feasibility code, not Product code |
| Project or solution files | `WaterJetSentinelSuite.sln` (12 projects) — source-committed, **never built in the authoring environment** |
| Package manifests or dependencies | Spike: `spikes/ui-runtime-react/react-ui/package.json` + `package-lock.json` (Owner-approved pins); `runtime-harness/package.json` (no deps). Product: `Directory.Packages.props` (pins **PROPOSED/UNVERIFIED**), `packages/contracts/wjss-contracts-ts/package.json` + lockfile (`typescript@6.0.3`, install-verified in Arena). No `global.json`; no NuGet lock file |
| Database schema or SQL scripts | **Do not exist** |
| Modbus or Galil adapter | **Does not exist** |
| Simulator | **Simulator adapter seam exists** (`adapters/simulator`: csproj + README only — behaviour, seeded acquisition and fault injection are 0.3A-2 scope). Never a production-path component. The deterministic synthetic topology currently lives in the test-side fixture generator (`tests/integration/FixtureGenerator.cs`). The spike's Node harness remains feasibility-only |
| Automated tests | Spike tests (Node `node:test`, Vitest, Playwright — Owner-local). Product tests **authored but never executed on .NET**: 5 xUnit projects (domain/runtime/api/config/fixture-parity) + TS mirror tests (14/14 PASS in Arena — the only green product-adjacent suite) |
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
| Wire-contract deviation | Spike `activeJob: null` replaced by delta-only `activeJobCleared: true` (STJ encoding limit) — recorded in DRAFT ADR-0014 §2; TS validator rejects the old form |
| Fixture provenance | Node-authored in Arena, labelled `PROVISIONAL STRUCTURAL FIXTURE`; **not** .NET-generated; superseded by the Owner-local generator run (runbook §5) |
| Arena verification | TS: `npm ci` + `tsc --noEmit` clean + `node --test` 14/14 PASS + fixtures validate; boundary scan exit 0; all JSON parses; links/whitespace/secret checks — see §12.12 |
| NOT verified | Every .NET claim: restore, build, tests, health-stub behaviour, kiosk, parity between C# generator and committed fixtures, NuGet pin availability |
| Delivery | Single commit on `arena/dd551752-waterjet-sentinel-suite`; PR "Stage 0.3A-1: product foundation source skeleton" → `main`; **NOT READY FOR MERGE** until runbook verdict PASS |
| Next | Owner-local runbook PASS → Owner merges → Owner explicitly authorizes **0.3A-2** (nothing advances otherwise; no ZIP before Stage 0.3 exit) |

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
