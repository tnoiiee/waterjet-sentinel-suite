# WaterJet Sentinel Suite (WJSS)

## 1. Project Identity

| Item | Value |
| --- | --- |
| Product | **WaterJet Sentinel Suite (WJSS)** |
| Description | Automated Boiler Wall Water-Jet Cleaning Monitoring and Supervisory Control System |
| Repository | `waterjet-sentinel-suite` |
| Deployment model | One Windows 11 Pro workstation per Boiler Unit; one installation controls one Boiler Unit |
| Operating mode | Full-screen Control Room Kiosk with controlled navigation; default language English |
| Current entry point | This README — navigation and current position only; it is **not** a roadmap authority |

WJSS is a supervisory monitoring and control application for automated boiler-wall water-jet cleaning.
It is **not** a Safety Instrumented System and is not certified to any standard; it never commands,
overrides, bypasses, suppresses or replaces an external protection function. The full original
product description is preserved verbatim in section 11.

## 2. Current Stage

| Field | Value |
| --- | --- |
| Stage | `0.4C-K6` |
| Title | T01 Physical Execution Enablement and Bench Readiness |
| Status | **IMPLEMENTED AND OWNER-LOCAL AUTOMATED VALIDATION PASSED; CANONICAL DOCUMENTATION PROMOTED; FINAL SOURCE AND PR REVIEW IN PROGRESS; NOT MERGED; K7 NOT AUTHORIZED** |
| Approved base | `193aba764f8934df3dc3b0fd1841e8b64e808cc2` |
| Initial K6 implementation | `00c2c0786de1966d6da279c8f38fc57c8b3b56de` |
| Namespace correction | `e40d43b3b9492a5078eac55f4b6776d3ededdfe3` |
| Validated implementation head | `00eb3c6c6ad581067aeef79b5c40b4dbb309194e` |
| Branch | `arena/5fcb82bf-waterjet-sentinel-suite` |

- The implementation was authored in Arena; **Owner-local automated validation is the authoritative
  evidence** for K6 and has passed.
- **K6 is NOT MERGED.** The K6 pull request is opened or updated in this pass and its review is in
  progress; the Owner merge decision is pending, and canonical documentation promotion is complete.
- **K7 (T01 One-Node Physical TC Read-Only Trial) is NOT AUTHORIZED.** K6 validation does not
  authorize K7; K7 requires its own Owner Scope Gate.
- The roadmap authority is [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md); this README is the canonical
  entry point.
- **Implementation identity versus documentation identity.** The Validated Implementation Head is
  `00eb3c6c6ad581067aeef79b5c40b4dbb309194e`. The Candidate documentation commits —
  `dccf200b4132cd281b3471409c23d08fe328c3ac` (Candidate foundation),
  `d5daaaabcb7547c4de40d990f74323501105ee33` (CR-01 correction) and
  `e4c3a37f6ad84194ad2e32287b3c53a4514bd806` (CR-02 refinement) and the CR-03 revision commit — are documentation-only and are **not** part of the validated implementation
  head; no implementation path changed in them.

## 3. Current Truth

Detailed current truth lives in [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md). Compact position:

| Question | Current answer |
| --- | --- |
| Current stage | `0.4C-K6` — implemented and Owner-local validated, **not merged** |
| Default build | **Physically disabled.** A normal repository build cannot construct a physical client |
| T01 | **Non-executable** without an Owner-signed authorization bound to the deployed build; no such artifact exists |
| Physical acquisition | **None.** No hardware contact, no passive capture, no port 502 access, no T01 execution |
| Topology | 6 Nodes / 40 words per Node / 240 physical TC channels / 216 assigned / 24 unassigned / 108 Sensors (I7 and I16 normal) |
| Runtime / Kiosk | Unchanged; no physical integration |
| Acceptance | Diagnostic success remains QUARANTINED; there is no automatic ACCEPTED path |
| Application version | **NOT ESTABLISHED** — no runtime release exists |

Placement rule: **README is an entry point.** Stage detail belongs to
[`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md); stage sequencing and authorization belong to
[`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md).

## 4. Approved Baseline

| Item | Value |
| --- | --- |
| Approved `main` | `193aba764f8934df3dc3b0fd1841e8b64e808cc2` (the PR #23 merge commit) |
| K6 approved base | the same commit |
| Validated implementation head | `00eb3c6c6ad581067aeef79b5c40b4dbb309194e` |
| Session branch | `arena/5fcb82bf-waterjet-sentinel-suite` |
| Parent chain | `00eb3c6…` → `e40d43b…` → `00c2c07…` → `193aba76…` |
| Merge authority | **Owner only.** Agents never merge |

A new session must verify the current remote refs before relying on any SHA written here.

## 5. Critical Path

The current critical path, stated as conditional steps — each step still requires its own
authorization, and none is granted by this promotion:

1. Final canonical documentation and complete source review. — **in progress**
2. Atomic Replacement completed and validated. — **DONE**
3. Owner PR review and merge decision for the K6 implementation checkpoint (**K6 NOT MERGED**).
4. A separate Owner Scope Gate for **K7** — T01 One-Node Physical TC Read-Only Trial
   (**NOT AUTHORIZED**).
5. Later stages per the roadmap in
   [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) section 4 — **PLANNED, NOT
   AUTHORIZED** unless and until the Owner authorizes each one.

Every later trial requires separate authorization. There is no batch approval for T01–T04, and a
failure does not authorize another candidate.

## 6. Safety and Authorization Boundary

```text
HARDWARE CONTACT NOT AUTHORIZED
T01 NOT AUTHORIZED
PASSIVE CAPTURE NOT AUTHORIZED
PORT 502 ACCESS NOT AUTHORIZED
PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED
RUNTIME/KIOSK PHYSICAL INTEGRATION NOT AUTHORIZED
SIX-NODE ACQUISITION NOT AUTHORIZED
PRODUCTION NOT AUTHORIZED
WRITE/CONTROL NOT AUTHORIZED
```

- The default build remains **physically disabled**.
- **K6 validation does not authorize K7.**
- Approved TEST_CONFIGURATION addresses are documentation-range addresses only; they are listed in
  `packages/commissioning/src/tcNodeConfiguration.mjs` and are not evidence of physical identity.
- No physical fact — reachability, port, function code, Unit ID, start address, quantity, endianness
  or framing size — is verified by this pass.

## 7. System Topology

Approved design intent (the authoritative statement remains the preserved original in section 11):

| Item | Approved baseline |
| --- | --- |
| Boiler walls | Left, Rear, Right, Front |
| Product Sensors | **108** (Left 24, Rear 30, Right 24, Front 30); I7 and I16 are normal Product Sensors |
| Thermocouple channels | **216 assigned** (108 × 2) of **240 physical** (six Nodes × 40); **24 unassigned** |
| Water Jet assemblies | **8 — WJ1 to WJ8**, each with horizontal X and vertical Y axes |
| Isolation Valves | **8**, paired WJn ↔ IVn by ordinal, never shared |
| Galil controllers | 4 (DMC-B140-M), axes A–D |
| Modbus interface | WAGO 750-362 Modbus TCP Coupler, zero-based internal addressing |
| Cleaning Jobs | **Strictly sequential** — at most one ACTIVE at any time |

Production tag lists, register maps, addresses and motion values are confidential local deployment
information and are not in this repository.

## 8. Validation Snapshot

| Area | Owner-local (authoritative) | Agent/Arena (Arena had no `dotnet`) |
| --- | --- | --- |
| K6 build | targeted adapter, targeted bench, focused tests and full solution Release builds PASS with no reported warnings or errors | no build performed |
| K6 tests | focused **95/95**; full .NET **550/550** (0 failed, 0 skipped) | no .NET execution |
| K6 dry run | SIMULATION only; connect/read NOT_ISSUED; QUARANTINED; no physical I/O | not applicable |
| K6 physical attempt | refused with `PHYSICAL_CAPABILITY_DISABLED`; deployment state `PHYSICAL_TEST_DEPLOYMENT_NOT_AUTHORIZED` | not applicable |
| Node/static | exporter 18/18, S2 14/14, S3 19/19, Boundary S1–S9 clean, Commissioning 218/218, fixture parity 7/7 | exporter 18/18, S2 14/14, S3 19/19, Boundary clean, Commissioning 220/220 with the corrected documentation guard (218 pre-existing + 2 authorized regression tests), syntax and path proofs |
| Physical | **none** — no hardware contact, no capture, no T01 execution | **none** |

### 8.1 Owner-local K6 record (authoritative)

**Restore**

- normal restore: PASS
- locked restore: PASS twice
- lock drift: none

**Build**

- targeted adapter Release build: PASS
- targeted TC bench Release build: PASS
- focused test-project Release build: PASS
- full solution Release build: PASS
- the reported builds completed with no reported warnings or errors

**Focused tests**

- 95 total
- 95 passed
- 0 failed
- 0 skipped

**Dry run**

- mode SIMULATION
- physical capability false
- connect NOT_ISSUED
- read NOT_ISSUED
- TrialAcceptance QUARANTINED
- no copied words
- no physical I/O

**Default PHYSICAL_TEST attempt**

- refused
- refusal code `PHYSICAL_CAPABILITY_DISABLED`
- `acquisitionPerformed` false
- `deploymentState` `PHYSICAL_TEST_DEPLOYMENT_NOT_AUTHORIZED`
- no physical-capable deployment build
- no real authorization artifact
- no real Owner signing key
- no physical contact

**Full .NET**

- 550 total
- 550 passed
- 0 failed
- 0 skipped

**Node/static validation**

- exporter 18/18
- S2 14/14
- S3 19/19
- Boundary S1–S9 zero findings
- Commissioning check PASS
- Commissioning 218/218 on the canonical LF representation
- fixture parity verify-only 7/7
- `WJSS_UPDATE_FIXTURES` false
- final working tree clean
- no lock drift
- no fixture drift
- no documentation drift at the validated implementation head

**R40 disposition**

- first normal Windows commissioning run: 217/218
- only R40 failed
- the mismatch was LF versus CRLF representation
- no JSON-content, topology or Product defect
- the approved generator produced the canonical LF representation
- canonical LF Commissioning rerun: 218/218
- the original Owner-local fixture was restored byte-for-byte
- original/restored Owner-local SHA-256: `CC3F45ADF43CED351F1E8AE0D17D06AEA181FD94D119126DF0659AAC3385DA80`
- the canonical LF SHA-256 was observed in an earlier generation run as `3BC8774CB74183080297BB89C3A855DB8E46782B421DF41AB955C1CA50A8256A`
- **Limitation:** the canonical hash was not recomputed in the same execution as the final 218/218 run
- final worktree clean
- no generated fixture was committed

### 8.2 Agent/Arena K6 record (separate scope)

Agent/Arena reported, separately from Owner-local validation:

- exporter 18/18
- S2 14/14
- S3 19/19
- Boundary S1–S9 clean
- Commissioning 220/220 with the corrected documentation guard (218 pre-existing tests plus the 2
  authorized regression tests added for the promoted heading layout)
- the guard correction changed only that single existing documentation-guard test; no evidence
  assertion was removed or relaxed
- JavaScript syntax checks passed
- changed-path and protected-surface proofs passed
- genuine locks unchanged

Agent did **not** have `dotnet` and must **not** be credited with:

- C# compilation
- analyzer execution
- xUnit or .NET tests
- .NET fixture parity
- physical execution

The full narrative record is in
[`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) section 5.

## 9. Documentation Index

The governing documents (unchanged by this pass):

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Working contract: authority order, scope lock, evidence rules, git rules |
| [`SECURITY.md`](SECURITY.md) | Security policy, secret handling, vulnerability reporting |
| [`CHANGELOG.md`](CHANGELOG.md) | Chronological documentation history |
| [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) | Stage status, verified state, legend, open items |
| [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) | Staged delivery plan and stage gates — **roadmap authority** |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Forward view of planned stages and milestones |
| [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md) | Consolidated requirements with identifiers and status |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Conceptual architecture, communication health, UI guard |
| [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) | Entities, terminology, outcomes, and dispositions |
| [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) | Hardware safety boundary and prohibited control writes |
| [`docs/CONTROL_AUTHORITY.md`](docs/CONTROL_AUTHORITY.md) | Authority matrix, sequencing authority, DCS override |
| [`docs/CLEANING_SEQUENCE.md`](docs/CLEANING_SEQUENCE.md) | Cleaning Job, sequencing invariants, failure handling |
| [`docs/ALARM_MODEL.md`](docs/ALARM_MODEL.md) | Condition, acknowledgement, shelving, cleared-state ack |
| [`docs/QUEUE_MODEL.md`](docs/QUEUE_MODEL.md) | Eight source queues, GlobalQueue, ownership, refill, stop/rebuild |
| [`docs/HISTORIAN_RETENTION.md`](docs/HISTORIAN_RETENTION.md) | Storage targets, intervals, retention, cleanup |
| [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md) | Roles, permissions, sessions, break-glass, override permission |
| [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) | Planned verification, including required review-confirmed cases |
| [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md) | What may and may not be committed |
| [`docs/decisions/README.md`](docs/decisions/README.md) | Architecture Decision Record index and format |
| [`.gitignore`](.gitignore) | Files excluded from the public repository |

The complete original index — including every ADR-0001…ADR-0020 row and the Stage 0.2.1A spike
entries — is preserved verbatim in section 11. No link target was changed or repaired in this pass.

## 10. Development Workflow

Read [`AGENTS.md`](AGENTS.md) first: it defines the authority order, scope-lock rules, evidence
requirements, public-repository restrictions, device-access prohibitions, prohibited Git operations
and the **Owner-only merge rule**. Contributors never merge.

Source authority order applied in this repository:

1. Latest Owner instruction
2. Owner-approved scope
3. Exact repository source and evidence
4. Current contracts, tests, analyzers and generators
5. Existing current-facing documentation
6. Historical documentation records
7. Older plans or unapproved proposals

Every stage report states **CHANGED**, **UNCHANGED** and **NOT VERIFIED**. Status legend used
throughout the documentation:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Behaviour or fact approved by the Owner in an approved Stage Gate |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review; binding |
| `[PROPOSED]` | A recommendation awaiting Owner approval; never treated as approved behaviour |
| `[NOT VERIFIED]` | Not yet confirmed by bench test, field test or Owner engineering review |
| `[NOT AUTHORIZED]` | Activity that is currently prohibited |
| `[OPEN]` | A question requiring an Owner decision; no behaviour may be assumed |

## 11. Historical Detail

The original README text is preserved verbatim below, including its earlier status table, product
description, planned scope, physical baseline, document index, status legend, contributing and
licence sections. Heading levels are demoted by two so this document keeps one heading hierarchy.

**Supersession notes (added for navigation only; the preserved text is not rewritten):**

- The preserved `## Current software checkpoint — 2026-10-13` block records the **K5-era** position
  (`c0bc2c7…` / `e71e33b…`, focused 81/81, full .NET 536/536, PR open). That work was merged as
  PR #23 into `main` at `193aba764f8934df3dc3b0fd1841e8b64e808cc2`, which is the approved base of
  K6. The current position is section 2 above.
- The preserved `## Historical status (superseded as current)` table keeps its dated
  "PR #22 OPEN" / "PR #21 OPEN" wording; [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) section 4
  records the Owner-approved status of those stages.
- CF-07 (the README status block lagging the then-current stage) remains an open Owner decision; no
  silent repair was made.

---

### Historical record — verbatim preservation of the authoritative original

The block below is the complete text of `README.md` as it exists at candidate source head
`00eb3c6c6ad581067aeef79b5c40b4dbb309194e`. Wording, tables, links, counts, SHAs, dates and statuses are unchanged. Only the
Markdown heading level is shifted by two (for example `##` → `####`) so that this candidate keeps a
single heading hierarchy; every heading's original text is retained verbatim.

### WaterJet Sentinel Suite (WJSS)

#### Current software checkpoint — 2026-10-13

**One-Node TC bench software validation CLOSED — READY FOR OWNER PR REVIEW.**
Exact Owner-validated implementation: `c0bc2c71636d3206098fd890e1de40c46fabae65`;
genuine two-lock commit: `e71e33b22d039b6cd5940fbe94da1879f71142fa` (direct child).
Owner-local targeted/full Release builds: **0 warnings / 0 errors**; focused **81/81**;
full .NET **536/536**; dry-run SIMULATION only; fixture parity **7/7**. Genuine/locked restore
PASS, no drift. Agent separately inspected locks/hashes and ran exporter **14/14**, S2 **14/14**,
S3 **19/19**, Boundary **0 findings**, Commissioning **218/218**; NO Agent-side .NET execution.

Dedicated isolated one-shot foundation; Runtime/Kiosk unchanged. T01 remains non-executable,
physical deployment refused, diagnostic success QUARANTINED without independent wire/Owner
review. No physical I/O or engineering interpretation. Software MERGE READY recommendation
is conditional on the final documentation/proof gates; PR remains OPEN for Owner review, not merged.

[Current state](docs/CURRENT_STATE.md) · [Validation, BOTH lock hash representations and history](docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md) ·
[ADR-0019](docs/decisions/ADR-0019-isolated-one-node-tc-bench.md) · [Bench README](apps/tc-bench/README.md).
The earlier status table below is historical; this notice supersedes its current/next-step wording.
**HARDWARE CONTACT NOT AUTHORIZED; T01 NOT AUTHORIZED; PASSIVE CAPTURE NOT AUTHORIZED;
PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED.**

**Automated Boiler Wall Water-Jet Cleaning Monitoring and Supervisory Control System**

Repository: `waterjet-sentinel-suite`

---

#### Historical status (superseded as current)

| Item | Value |
| --- | --- |
| Library Selection Gate (historical) | **Library Selection Gate — SELECT_FLUENTMODBUS_5_3_2 — MERGE READY — PR #22 OPEN targeting main, head `dfce519d1cffe090a8be431cbc30dd563f34df34` (consolidated documentation-continuity checkpoint, parent `fdc81d801aecabd3f00350948d69a9f9b49f9244` report-integrity checkpoint, genuine lock `49e404356edee64bb9dbaccd395d8bcc66435881` on `d1ea0324`)** — the Library Selection Gate implementation and validation position established at this checkpoint (`fdc81d8` report-integrity checkpoint, genuine lock `49e4043` version 2 net10.0 FluentModbus 5.3.2, `455/455` `S2 14/14 S3 11/11` `S1–S9` clean `Commissioning 218/218`) is preserved across subsequent documentation/governance/comment-only corrections in this PR chain (`fdc81d8` → `dfce519` → `0755483` → `7b9343c`); those successors preserve the same MERGE READY implementation and validation position with no Product, lock, package-version, fixture, test or boundary-policy change; a new session must verify the current remote PR head (`origin/arena/c51a5906-waterjet-sentinel-suite` / PR #22 head) and must not rely on a SHA pre-written here for the commit being created — FluentModbus 5.3.2 selected (MIT, netstandard2.0 via CPM), NModbus 3.0.83 rejected (no per-operation CancellationToken), Raw Socket not selected; foundation `adapters/tc-acquisition` (`ITcReadOnlyClient`, explicit FC03/FC04 no fallback, BigEndian candidate, one-outstanding READ_BUSY, cancellation/timeout/error classification, generation/stale fencing, transport-owned memory copied, no write, self-owned — no Wjss.Contracts/Domain dependency, genuine `packages.lock.json` `49e4043` version 2 net10.0 FluentModbus 5.3.2 + 8.0.0/8.0.0); Owner-local validation PASS (genuine restore PASS, locked restore PASS×2 no drift, targeted Release 0w0e, full Release 0w0e, full .NET 455/455 0 failed 0 skipped, S2 14/14 S3 11/11 S1–S9 clean, Commissioning 218/218, sensor-map parity PASS, no fixture change), Arena lock inspection PASS, S2/S3/Boundary/Commissioning PASS (no Arena .NET claim); topology unchanged 6 Nodes / 40 words per Node / 240 physical / 216 assigned / 24 spare / 108 Sensors (I7/I16 normal) / TCP 502 Owner-approved factory port; NOT VERIFIED physical connectivity/reachability/WAGO/FC03-vs-FC04/Unit ID/start register/byte-word order/scaling/wire-break/timing/concurrency/stale/PHYSICAL_TEST/PRODUCTION/device write (see `docs/CURRENT_STATE.md`); next critical-path after Owner merge → new session → new branch → Scope Gate: **One-Node Physical TC Read-Only Acquisition** (one configured Node, 40-word candidate, prove reachability/FC/Unit ID/start/raw words/interpretation, no write, no Production, no six-Node until one-Node passes); future: 6 Nodes→216/24 mapping→Kiosk PHYSICAL_TEST Monitor→commissioning baseline→pilot 2–4→full 108/216→Galil G1–G5→Dry Cycle→Production; prohibited: probe 192.0.2.11–16/502, write FC05/06/15/16/23, Production; links: [`CURRENT_STATE`](docs/CURRENT_STATE.md) · [`MASTER_PLAN`](docs/MASTER_PLAN.md) · [`Stage Report`](docs/STAGE_LIBRARY_SELECTION_GATE_REPORT.md) · [`ADR-0018`](docs/decisions/ADR-0018-modbus-tcp-client-library-selection.md). **Stage 0.4C-K3 — Physical TC Connectivity and Protocol Evidence Gate + Owner Critical Topology Correction — COMPLETE FOR MERGE REVIEW superseded but retained as history** (head `b5c7dff2`, PR #21, base `cc6ccf87`, `041b2863` validation, see [`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md)) |
| Stage 0.4C-0B (historical row — superseded; its 106 / 212 / I7-I16-conflict figures predate the Owner critical topology correction, and its process-image wording predates the K3 `DERIVATION_BLOCKED` ruling) | **Stage 0.4C-0B — Six-Node Thermocouple Topology Baseline Rev1 — EVIDENCE REVIEW COMPLETE, FIELD VERIFICATION PENDING** — development checkpoint, documentation only. PR #18 (since MERGED as `48f0315e…`). Drawing: **REV1 reviewed**, status `DRAWING_PENDING_REVISION`. Schedule: **240 physical rows** — 212 Product-compatible mappings across **106 Product Sensors**, 4 Product-topology conflict rows (I7/I16, `NOT_COMMISSIONED`), 12 Drawing internal-conflict rows, 24 unassigned physical Channels, **0 unresolved rows**. Process-image: **`DOCUMENTED_FACTORY_DEFAULT_BASELINE_REV1`** — per node 40 input words at offsets 0–39, status bytes suppressed in the documented 750-362 network cyclic image (documented, **not Hardware-verified**). **Wire-break decoding: BLOCKED** (model capability documented; no explicit status in the Modbus cyclic image). `REQ-TC-001`–`REQ-TC-007` drawing/manufacturer-verified; `REQ-TC-008`–`REQ-TC-020` stay `NOT_PROVIDED`. **`STAGE_0.4C-1_TC_ACQUISITION`: BLOCKED.** **Physical Sensor acquisition not implemented. Hardware loop test not executed. No live Modbus. No TEST_HARDWARE. No Production. No device write/control.** Predecessor Stage 0.4B-3 Mapping evidence (268/268; Mapping UI 82/82; Owner Browser Review passed) is recorded as predecessor evidence and was not re-run at this documentation head |
| Stage 0.4C-0B identifiers (historical) | Base `main` `3e1a7fd9d28db65221c123960a0c85b62d93949a`, the **PR #17 merge commit** (parents `bd143758…` and `48d8605a…`). **PR #17 MERGED** at `2026-10-11T08:59:28Z`. Stage 0.4C-0B development checkpoint on branch `arena/c4b63a26-waterjet-sentinel-suite` (checkpoint SHAs recorded in PR #18). **PR #18 MERGED as `48f0315e…` (historical-at-the-time: `PR #18 OPEN — NOT MERGED` at checkpoint, superseded by merge)**. Authoritative workbook `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, unchanged |
| Stage 0.3A-1 (historical) | **MERGED** — PR #4, merge commit `bdf7f8f2`. Owner-local validation had PASSED (Release 0 warnings / 0 errors; .NET 61/61; TypeScript 24/24; fixture parity 7/7; boundary clean) per [`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md). Later stages superseded it: Stage 0.3A-2 MERGED (PR #5), Stage 0.3A-3 MERGED (PR #6), Stage 0.4A checkpoints MERGED (PR #7–#11), Stage 0.4B-1 MERGED (PR #12), Stage 0.4B-2 MERGED (PR #14) |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED / MERGED** — through PR #2 (source checkpoint `5bcf1b33f924ab30590a55736676200115874fa1`, merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`) |
| Stage 0.2 ADR-0006 to ADR-0013 | **ACCEPTED** as architecture direction — accepted does **not** mean implemented; items marked `[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` inside them keep those markers |
| Stage 0.2.1A Scope Gate / Coding Start | **APPROVED** / **APPROVED** |
| Stage 0.2.1A implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT** — synthetic feasibility spike in [`spikes/ui-runtime-react/`](spikes/ui-runtime-react/README.md); Owner-local final Edge gate **PASS** and Owner manual review **PASS** at `114c0761`; **MERGED** — PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; controlled 15- and 60-minute observations **waived as merge blockers** |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07); Production transport and chart library remain `[OPEN]` |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Main Development Scope Gate | Stage 0.3A, Stage 0.4A, the Stage 0.4B substages, Stage 0.4C-0 and Stage 0.4C-0A proceeded under explicit Owner instructions and are **MERGED** through PR #14, PR #15, PR #16 and PR #17; **Stage 0.4C-K3 COMPLETE FOR MERGE REVIEW (PR #21 merged as `d1ea0324cb37de65a798fe4271429f955d4a9225` parents `cc6ccf87`/`a6238ac`, see [`CHANGELOG`](CHANGELOG.md); `PR #21 OPEN` wording below is dated historical-at-the-time)** and **Library Selection Gate SELECT_FLUENTMODBUS_5_3_2 MERGE READY (PR #22 OPEN targeting main, head `dfce519d1cffe090a8be431cbc30dd563f34df34` consolidated documentation-continuity checkpoint, parent `fdc81d801aecabd3f00350948d69a9f9b49f9244` report-integrity checkpoint, genuine lock `49e404356edee64bb9dbaccd395d8bcc66435881` on `d1ea0324`; chain `fdc81d8` report-integrity → `dfce519` → `0755483` → `7b9343c` documentation/governance/comment-only successors preserve same MERGE READY implementation and validation position — new session must verify current remote PR head, see [`STAGE_LIBRARY_SELECTION_GATE_REPORT`](docs/STAGE_LIBRARY_SELECTION_GATE_REPORT.md))**. The only work currently authorised beyond those is the **One-Node Physical TC Read-Only Acquisition** which **requires a separate Owner Scope Gate, Owner merge of PR #22, new session and new branch** — until then Stage 0.4C-1 TC acquisition (**BLOCKED**), wire-break decoding (**BLOCKED**), `TEST_HARDWARE`, six-Node expansion, Galil and Production remain **NOT AUTHORIZED** (see [`MASTER_PLAN`](docs/MASTER_PLAN.md) critical path) |
| Mapping Configuration (Stage 0.4B, current) | Simulation-only and read-only with respect to hardware. 750-601, 750-613, 750-600 and the 750-362 head station are **non-process modules with NO Process I/O address** (`NOT_APPLICABLE` / `NON_PROCESS_DATA_MODULE`) — they are not waiting for an address and are never `ADDRESS_UNRESOLVED`. All other module addresses remain **`ADDRESS_UNRESOLVED`**; the candidate process image is **`CANDIDATE_UNVERIFIED`** with **no invented byte, word or bit offset**. Activation **NOT READY**. No MODBUS, no TEST_HARDWARE, no PRODUCTION, no device write or control |
| Production devices | **NOT AUTHORIZED** |
| Repository contents | Documentation, repository governance, one removable synthetic feasibility spike, and — from Stage 0.3A-1 — the Owner-validated product foundation: solution + project sources, `global.json` (SDK `10.0.401`), twelve genuine Owner-local `packages.lock.json`, and the .NET-generated fixtures and examples |
| Repository contents note | `spikes/` remains synthetic feasibility code outside the Product build graph; the 15 transferred artifacts come from the Owner's validated Working Tree (handoff commit `488b98fb…`) — Arena generated none of them |
| Application code | **Validated for the checkpoint scope** Owner-locally (Release 0/0; 61/61; TS 24/24; parity 7/7; boundary clean; health + profile + port gates as recorded in the validation document). Arena claims no .NET execution of its own. The committed fixtures are .NET-GENERATED; the in-file `fixtureStatus` PROVISIONAL marker persists only because the generator constant still writes it — removal is a later Owner decision |
| Application version | **NOT ESTABLISHED.** No runtime release exists. |
| Hardware connection | **Not authorised.** Production device access is prohibited. |
| Production Write | **Not authorised.** |
| Merge authority | **Owner only.** Agents never merge. |

This repository contains documentation, one removable **synthetic** React feasibility spike
(`spikes/ui-runtime-react/`, Stage 0.2.1A) that uses no device, no Production value, and no
Product directory, and — from Stage 0.3A-1 under the Owner's Option-C amended gate — the
**product foundation source skeleton** (`packages/contracts`, `packages/domain`,
`packages/application`, `packages/time`, `adapters/simulator`, `apps/runtime`, `apps/kiosk`,
`tests/`, `config/examples/`, `tools/boundary-scan/`). That skeleton was **authored in Arena where .NET did not execute** (no SDK, no NuGet egress — Arena claims no .NET build); **Owner-local validation is authoritative** (locked restore PASS, Release build PASS, full .NET 455/455, Commissioning 218/218, S2 14/14 S3 11/11 S1–S9 clean, fixture parity 7/7 at `041b2863`/`b5c7dff2` and `49e4043`/`fdc81d8`/`dfce519` chain, merged `d1ea0324`); **Hardware/Production validation remains absent** (no WAGO/Galil bench, no TEST_HARDWARE/PRODUCTION, see [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) for stage status, verified state, and status legend).

Stage 0.2 recorded the technology and solution architecture — UI delivery model, runtime
process model, technology stack, database access and migrations, device adapter boundary,
configuration and secrets, simulator-first development, and offline deployment — in
[`docs/decisions/`](docs/decisions/README.md). The Owner accepted Stage 0.2 and ADR-0006 to
ADR-0013 as architecture direction. **Accepted does not mean implemented**: items still marked
`[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` keep those markers, and no capability or later
stage is authorised by acceptance. Stage 0.3A-1 authors product *sources* that follow these
directions — that is drafting toward implementation, not implemented or verified code; its
three companion records ADR-0014 to ADR-0016 are `DRAFT`.

**React is selected as the Primary UI Framework** — this is a **settled Owner decision
(2026-10-07)**, taken after the Owner-local final Edge gate and manual review of the Stage
0.2.1A spike passed; it is no longer a preference awaiting acceptance. The Blazor
counter-spike is **not required** unless a future material blocker is identified.
*Historical context (superseded):* two candidates were recorded and compared on equal terms:
Candidate A — React + TypeScript + Vite — and Candidate B — Blazor Hybrid — each hosted
in the same application-owned kiosk shell behind the same loopback API. At that time, the
evidence-based preference for Candidate A was explicitly **not acceptance** and Candidate B
was explicitly **not rejected**; that position ended with the 2026-10-07 Owner selection.
React can be built and deployed offline; it introduces a second
package and build ecosystem, which increases offline dependency-management and supply-chain
effort without making offline development or deployment impossible. Choosing the framework
required measured evidence, and it was obtained: the Stage 0.2.1A React feasibility spike
was approved, implemented, and **closed out** — the feasibility checkpoint is **complete**
(Owner-local final Edge gate and manual review **PASS**;
[`docs/spikes/stage-0.2.1a-plan.md`](docs/spikes/stage-0.2.1a-plan.md) is retained as
history), and React is **selected** as the Primary UI Framework (see above). See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) sections 23 to 33 and
[`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) section 3.2.

#### What the product is

A supervisory monitoring and control application for automated boiler-wall water-jet
cleaning. The planned deployment model is:

- One Windows 11 Pro workstation per Boiler Unit.
- One application installation controls one Boiler Unit.
- The main operating mode is a full-screen Control Room Kiosk with controlled navigation.
- Applicable pages support workstation-responsive layouts.
- The default application language is English; Thai may be used as supplementary
  contextual explanation where necessary.
- The application uses local application users.
- The initial system is standalone and does not depend on Internet access.

#### What the product is not

WJSS is **not** a Safety Instrumented System. It does not replace emergency stop circuits,
hardwired protection, motor protection, mechanical limits, or controller-side safe-stop
behaviour. The application must never command, override, bypass, suppress, or replace an
external protection function. It is **not** certified to IEC, ISA, ISO, or any other
standard. Industrial standards are used only as guidance and design inspiration. See
[`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md).

#### Planned full-system scope

Several software foundations already exist and are **checkpoint-validated within scope** — solution + project sources, `global.json` SDK `10.0.401`, `TopologyInvariants`/`WallMap`/`WaterJetTopologyCatalog` 108/216, `SENSOR_DISABLED` kernel, 750-362/750-498 topology, React feasibility spike, Kiosk shell (1.0.4258.31), `adapters/tc-acquisition` `ITcReadOnlyClient` Library Selection Gate `SELECT_FLUENTMODBUS_5_3_2` (`49e4043`/`dfce519` MERGE READY, 455/455, S2/S3 clean) — **physical integration, field commissioning, remaining operational capabilities and Production activation are incomplete/not authorized** (see `docs/CURRENT_STATE.md` and `STAGE_LIBRARY_SELECTION_GATE_REPORT.md`).

Modbus TCP monitoring and supervisory commands through WAGO remote I/O; temperature
monitoring; DCS hardwired signal monitoring through WAGO I/O; valve and motor supervisory
control; Galil motion control; cleaning criteria evaluation; TempQueue, TimeQueue, and
GlobalQueue arbitration; Historian; trend visualisation; alarm management; event logging;
audit logging; diagnostics; configurable roles and permissions; engineering configuration;
and reporting and export.

#### Physical baseline (approved design intent)

| Item | Approved baseline |
| --- | --- |
| Boiler walls | Left, Rear, Right, Front |
| Sensor locations | Left 24, Rear 30, Right 24, Front 30 — **108 Product Sensors** (**Owner critical topology correction, 2026-10-12**: I7 and I16 are normal Product Sensors; supersedes the 106-location belief of Stage 0.2.1A–0.4C-K3 and the earlier 104-location baseline) |
| Logical Sensor matrix | 18 logical columns × 6 logical rows = **108 logical positions, every one of them a Product Sensor**. The Sensor sequence is exactly G+201–G+218, G+101–G+118, G1–G18, H1–H18, I1–I18, J1–J18; **I7** (Rear wall, row 5) and **I16** (Front wall, row 5) appear once each in their normal ordinal position with the same parameters, state model, Front/Rear pairing, runtime, commissioning, mapping and queue rules as every other Sensor. No `NON_SENSOR_GAP` position exists. Wall columns: Left 1–4, Rear 5–9, Right 10–13, Front 14–18. Node source of truth: `packages/commissioning/src/topologyInvariants.mjs` (central invariant check, wired into commissioning and `packages/contracts/TopologyInvariants.cs` — both fail-closed) and `packages/commissioning/src/wallmap.mjs`, and the .NET sources `packages/contracts/WallMap.cs` (`CanonicalSensorMap` 108/216, `TopologyInvariants.cs` 108/108/108/108/216/240/24 I7=1 I16=1) and `packages/domain/WaterJetTopologyCatalog.cs` (WJ1/WJ3 `UNRESOLVED` with no gap anchors) which now correctly encode **108 Sensors / 216 assigned / 240 physical / 24 spare, I7 and I16 normal Sensors, no `NON_SENSOR_GAP` remains** — verified by Owner-local validation at `041b286322186977c5f87cde8184ae5a180d60a6` / `b5c7dff2` (locked restore PASS, Release build PASS, 455/455, fixture parity 7/7, `topologyInvariants.mjs` ↔ `TopologyInvariants.cs` parity) and retained in the merged `d1ea0324` main ancestor (`cc6ccf87`/`a6238ac`). Historical: "108 = 106 Sensors + 2 `NON_SENSOR_GAP` (I7 → WJ3, I16 → WJ1)" superseded by the Owner critical topology correction. |
| Water Jet identity vs. Sensor position | **Water Jets are separate equipment entities (WJ1–WJ8), never Sensor-matrix slots.** *Installed Position* and *Target Coverage* are separate concepts: a Water Jet is installed on one wall and covers the **opposite** wall in the matching region — **WJ1 is installed Front-lower, covers the Rear-lower target area and pairs with IV1**; **WJ3 is installed Rear-lower, covers the Front-lower target area and pairs with IV3**. The earlier description of WJ1/WJ3 as installed "at the I16 / I7 gap anchor" is superseded (I7 and I16 are Sensors); the replacement placement description for WJ1 and WJ3 is an **open Owner decision** and is not invented here. A Sensor's *Assigned Cleaning Device* is its assigned device identity — the legacy "cannon" ordinal maps directly to WJn — and is **never inferred from the Sensor's wall location**; assignment follows responsibility for the target wall (ADR-0017 supersedes the earlier "Cannon equipment slot" wording) |
| Thermocouple channels per sensor | 2 — front channel (`TC_F`) and rear channel (`TC_R`) |
| Thermocouple channels total | **216 assigned Product TC channels** (108 × 2) out of **240 physical** TC channels (six Nodes × 40), leaving **24 unassigned**. Historical: 212 (and the earlier 208). Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. |
| Water Jet assemblies | **8 — WJ1 to WJ8**, each with horizontal X and vertical Y axes |
| Isolation Valves | **8 — exactly one dedicated per Water Jet, paired WJn ↔ IVn by ordinal** (WJ1↔IV1 … WJ8↔IV8). Never shared |
| Galil controllers | 4 (one controller per two Water Jets), model DMC-B140-M |
| Controller axes | A, B, C, D per controller |
| Modbus interface | WAGO 750-362 Modbus TCP Coupler, Modbus TCP only, zero-based internal addressing |
| Cleaning Jobs | **Strictly sequential.** At most one ACTIVE at any time |

Actual production tag lists, register maps, addresses, and motion values are confidential
local deployment information and are **not** in this repository.

#### Two invariants worth stating up front

1. **At most one Cleaning Job may be ACTIVE within one installation at any time.**
   Different Water Jets, valves, walls, or controllers do not grant authority for
   concurrent jobs. Parallel Water Jet cleaning is prohibited.
2. **The Operations UI close guard is an operational usability control, not a safety
   protection.** It blocks the normal close action while a Cleaning Job is active or the
   Main Pump is running. It cannot protect against process termination, Windows shutdown,
   workstation restart, power loss, or hardware failure.

#### Document index

##### Governance

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Working contract: authority order, scope lock, evidence rules, git rules |
| [`SECURITY.md`](SECURITY.md) | Security policy, secret handling, vulnerability reporting |
| [`CHANGELOG.md`](CHANGELOG.md) | Documentation history |
| [`.gitignore`](.gitignore) | Files excluded from the public repository |

##### Core documentation

| Document | Purpose |
| --- | --- |
| [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) | Stage status, verified state, legend, open items |
| [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) | Staged delivery plan and stage gates |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Forward view of planned stages and milestones |
| [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md) | Consolidated requirements with identifiers and status |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Conceptual architecture, communication health, UI guard |
| [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) | Entities, terminology, outcomes, and dispositions |

##### Control and safety

| Document | Purpose |
| --- | --- |
| [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) | Hardware safety boundary and prohibited control writes |
| [`docs/CONTROL_AUTHORITY.md`](docs/CONTROL_AUTHORITY.md) | Authority matrix, sequencing authority, DCS override |
| [`docs/CLEANING_SEQUENCE.md`](docs/CLEANING_SEQUENCE.md) | Cleaning Job, sequencing invariants, failure handling |
| [`docs/ALARM_MODEL.md`](docs/ALARM_MODEL.md) | Condition, acknowledgement, shelving, cleared-state ack |

##### Queues and data

| Document | Purpose |
| --- | --- |
| [`docs/QUEUE_MODEL.md`](docs/QUEUE_MODEL.md) | Eight source queues, GlobalQueue, ownership, refill, stop/rebuild |
| [`docs/HISTORIAN_RETENTION.md`](docs/HISTORIAN_RETENTION.md) | Storage targets, intervals, retention, cleanup |
| [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md) | Roles, permissions, sessions, break-glass, override permission |

##### Delivery assurance

| Document | Purpose |
| --- | --- |
| [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) | Planned verification, including required review-confirmed cases |
| [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md) | What may and may not be committed |

##### Decision records

| Document | Purpose |
| --- | --- |
| [`docs/decisions/README.md`](docs/decisions/README.md) | Architecture Decision Record index and format |
| [`docs/decisions/ADR-0001-product-identity.md`](docs/decisions/ADR-0001-product-identity.md) | Product identity and naming |
| [`docs/decisions/ADR-0002-deployment-architecture.md`](docs/decisions/ADR-0002-deployment-architecture.md) | Per-Boiler-Unit workstation deployment |
| [`docs/decisions/ADR-0003-queue-arbitration.md`](docs/decisions/ADR-0003-queue-arbitration.md) | Deterministic queue arbitration and sequencing |
| [`docs/decisions/ADR-0004-historian-strategy.md`](docs/decisions/ADR-0004-historian-strategy.md) | Historian storage and retention strategy |
| [`docs/decisions/ADR-0005-hardware-safety-boundary.md`](docs/decisions/ADR-0005-hardware-safety-boundary.md) | Prohibition of production control writes pending bench verification |

###### Stage 0.2 decision records — `ACCEPTED` (architecture direction; not implemented)

| Document | Purpose |
| --- | --- |
| [`docs/decisions/ADR-0006-ui-delivery-model.md`](docs/decisions/ADR-0006-ui-delivery-model.md) | Kiosk shell and local web UI delivery model; React and Blazor candidates compared — final framework `[OPEN]` |
| [`docs/decisions/ADR-0007-runtime-process-model.md`](docs/decisions/ADR-0007-runtime-process-model.md) | Process model, device-session ownership, Local Application API boundary |
| [`docs/decisions/ADR-0008-technology-stack.md`](docs/decisions/ADR-0008-technology-stack.md) | .NET support track, API framework, service hosting, logging, validation, tests, offline packaging; UI framework `[OPEN]` |
| [`docs/decisions/ADR-0009-database-access-and-migrations.md`](docs/decisions/ADR-0009-database-access-and-migrations.md) | Transactional relational access, batch Historian path, migration execution policy, database unavailability; ORM and mapper `[OPEN]` |
| [`docs/decisions/ADR-0010-device-adapter-boundary.md`](docs/decisions/ADR-0010-device-adapter-boundary.md) | Adapter ports, vendor isolation, command lifecycle, disabled-by-default physical adapters |
| [`docs/decisions/ADR-0011-configuration-and-secrets.md`](docs/decisions/ADR-0011-configuration-and-secrets.md) | Configuration layers, Draft versus Published, secrets, publication and rollback |
| [`docs/decisions/ADR-0012-simulator-first-development.md`](docs/decisions/ADR-0012-simulator-first-development.md) | Simulator-first development, device profiles, failure injection, contract parity |
| [`docs/decisions/ADR-0013-offline-deployment.md`](docs/decisions/ADR-0013-offline-deployment.md) | Offline installation, startup, backup and restore, upgrade and rollback, local-only communication |

###### Stage 0.2.1A feasibility spike — synthetic, removable, not Product code

| Document | Purpose |
| --- | --- |
| [`docs/spikes/stage-0.2.1a-plan.md`](docs/spikes/stage-0.2.1a-plan.md) | Approved spike scope, architecture, scenarios, measurement plan |
| [`docs/spikes/stage-0.2.1a-results.md`](docs/spikes/stage-0.2.1a-results.md) | Arena validation and measurement evidence; Owner-local final Edge gate and manual review PASS (Owner-reported, §0G) |
| [`spikes/ui-runtime-react/README.md`](spikes/ui-runtime-react/README.md) | Spike layout, run instructions, NOT VERIFIED list |

#### Status legend

Every substantive statement in this documentation is marked with one of the following:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Behaviour or fact approved by the Owner in an approved Stage Gate. |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review. Binding. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. Must not be treated as approved behaviour. |
| `[NOT VERIFIED]` | Value or behaviour not yet confirmed by bench test, field test, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Activity that is currently prohibited. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed. |

#### Contributing

Read [`AGENTS.md`](AGENTS.md) first. It defines the authority order, scope-lock rules,
evidence requirements, public-repository restrictions, device-access prohibitions,
prohibited Git operations, and the Owner-only merge rule.

#### License and distribution

No licence has been granted in this repository. Publication of this documentation does not
grant rights to use, modify, or distribute the described system. Contact the Owner before
reusing any content.
