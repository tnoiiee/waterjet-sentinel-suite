# Master Plan — WaterJet Sentinel Suite (WJSS)

## 1. Master Plan Authority

- **`docs/MASTER_PLAN.md` is the authoritative execution roadmap.** It became authoritative in this
  document through the completed, validated Atomic Replacement of the Owner-approved revision.
- **This document is the plan of record.** It cites no authorization: every stage still requires its
  own Owner Scope Gate before any work may begin.
- This document does **not** create a second roadmap and does **not** establish a competing authority.
  There is no `VERSION_ROADMAP.md`; roadmap authority stays with `docs/MASTER_PLAN.md`.
- A Progress Dashboard, when one exists, is only a **visual projection** of `MASTER_PLAN` and current
  truth; it is never a roadmap authority or an authorization record. No Progress Dashboard is created
  by this pass.
- **Planned does not mean Authorized.** Every stage — including every stage listed as `CURRENT`,
  `NEXT`, `PLANNED` or `FUTURE` in section 4 — requires its **own Owner Scope Gate** before any work
  may begin.
- **Implementation identity versus documentation identity.** The Validated Implementation Head is
  `00eb3c6c6ad581067aeef79b5c40b4dbb309194e`. The Candidate documentation commits —
  `dccf200b4132cd281b3471409c23d08fe328c3ac` (Candidate foundation),
  `d5daaaabcb7547c4de40d990f74323501105ee33` (CR-01 correction) and
  `e4c3a37f6ad84194ad2e32287b3c53a4514bd806` (CR-02 refinement) and the CR-03 revision commit — are documentation-only and are **not** part of the validated implementation
  head; no implementation path changed in them.
- Document status of the preserved original: `[APPROVED]` for the stage-gate discipline and for the
  Stage 0.1 and Stage 0.2 positions; stage contents beyond Stage 0.2 remain `[PROPOSED]`. Governing
  authority: [`AGENTS.md`](../AGENTS.md) and the approved Stage 0.1 and Stage 0.2 Scope Gates.

## 2. Versioning Convention

Stage identifiers follow the repository's existing convention `<program phase><track><ordinal>`:

| Prefix | Track | Ordinal range in the proposed roadmap |
| --- | --- | --- |
| `0.4C-K` | TC acquisition and commissioning (Kiosk/bench track) | `K1` … `K17` |
| `0.4D-G` | Galil and motion | `G1` … `G5` |
| `0.4E-I` | Integrated dry cycle | `I1` … `I4` |
| `0.5A-P` | Production readiness | `P1` … `P4` |

This is a naming convention for **stage identifiers** only. It does not establish an application
version: **no application version exists**, and the documentation versioning policy remains `[OPEN]`
(see the preserved `Versioning position` and `5. Standing constraints` text in section 11).

## 3. Current Program Position

| Field | Value |
| --- | --- |
| Stage | `0.4C-K6` |
| Title | T01 Physical Execution Enablement and Bench Readiness |
| Status | **K6 MERGED** (PR #24 → `main` `f356b162214946058b42fe56fa13e5afc4c80539`, 2026-10-14); post-K6 **Local TCP Simulation execution class** (not a roadmap stage; bench tooling only) — **MERGED as PR #25 (`b3fb60cd33c992c2af209cb7b291535ffacfed74`, 2026-10-14T20:59:29Z); the "PR #25 OPEN, NOT MERGED" wording retained in the dated records below is superseded by that merge, and `main` now stands at `4a28b428b82179acc3a83cf368665ae008f5d1cf`, the MERGED PR #26 merge commit**; OWNER-LOCAL VALIDATION COMPLETE — `25d34390` full automated validation; `59d0ffdd` final-head affected gates PASS and Local TCP Simulation run PASS (effective `127.0.0.1:502`, FC03 / Unit 0 / start 0 / quantity 40, 40 words exactly 100–139, `QUARANTINED`; Local Simulation evidence only); OWNER MERGE DECISION PENDING** (2026-10-15); **K7 NOT AUTHORIZED** |
| Approved base | `193aba764f8934df3dc3b0fd1841e8b64e808cc2` (K6); `f356b162214946058b42fe56fa13e5afc4c80539` (Local TCP Simulation branch `arena/18760814-waterjet-sentinel-suite`) |
| Initial K6 implementation | `00c2c0786de1966d6da279c8f38fc57c8b3b56de` |
| Namespace correction | `e40d43b3b9492a5078eac55f4b6776d3ededdfe3` |
| Validated implementation head | `00eb3c6c6ad581067aeef79b5c40b4dbb309194e` |
| Branch | `arena/5fcb82bf-waterjet-sentinel-suite` |

- **Current position (2026-10-25; supersedes the K-era position fields above as current-facing
  truth — those fields remain dated history).** The approved merged baseline is `main`
  `f9e6e9adfef210f483fd3bfc7471936bbd7f34b2` (the PR #40 merge commit; Parent 1
  `696e135cce01bdc1ec34328f973bbd5ef4aed71a`, Parent 2 / final PR #40 head
  `eff2ce5b549c5ff390e5ed220e75fd2208e43509`, merge tree
  `2715f8bd3b2ce97675bdfb00401dd00730ddb397`; PR #40 Phase 3C Pump-ready and paired-IV
  Simulation composition is MERGED). The Owner-authorized Product Critical Path is
  **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning** (Home → DP/DE → dual-axis Move →
  selected Sensor P1→P6 → Centre 125 mm / 125 mm → Stop / Abort / Brake / cleanup; dry — no
  Pump command, no physical Isolation Valve command, no water, no Queue / AutoSequence, no
  Production), the Owner working label over the `0.4D-G1`–`0.4D-G5` Galil and motion stage
  family (formal IDs unchanged). A standalone Simulation-only Phase 3D is **SUPERSEDED** as the
  immediate objective. H2 Physical Pump/IV I/O Commissioning (NEXT), H3 Single Real Cleaning Job,
  H4 Sequential Multi-Job/Queue and H5 Production Readiness remain **NOT AUTHORIZED**; every
  later stage requires explicit Owner authorization, and real Hardware execution remains fenced
  behind a separate explicit Owner authorization for the exact validated PR head and exact
  reviewed trial procedure.

- **H2 status synchronization (2026-10-31; supersedes only the H2 status field of the current
  position above and the numbered H2 label in §5 — both remain the dated record, and nothing else
  in this plan changes).** H2 Physical Process-I/O is **IMPLEMENTED and OWNER-LOCALLY VALIDATED on
  PR #42** at exact Product head `5ae783cb7177eb3b629a097a48aa40c508868b2c` (direct parent
  `edbffae5c2be3853915d9d6b3fc0d1ce34a1b744`; approved base `main`
  `bfd2867af1dda29fcbc1987328be3d131a189d0a`): SDK 10.0.403, normal and locked restore PASS with no
  drift, Full Release build PASS, focused Process-I/O .NET 32/32, Full .NET 1313/1313, focused
  Windows Node CLI 23/23, full Process-I/O Node 88/88, both Simulation read profiles PASS,
  evidence parity PASS, boundary S1–S9 clean, boundary self-tests 33/33. The Owner has accepted the
  **H2 Modbus Simulator Function/Address Bench as PASS** (loopback `127.0.0.1:502`, Unit ID 1); the
  simulator implements no WAGO output process-image mirroring, which is **not** an H2 failure. Real
  WAGO endpoint connectivity and real field-device actuation remain **NOT EXECUTED / NOT VERIFIED**;
  real WAGO process-image mirroring is **DEFERRED BY OWNER** under the deferred gate `H2-WAGO-MIRROR`
  (FC05 write n vs FC01 read-back 512+n; FC06 or FC16 write n vs FC03 read-back 512+n; command-state
  and safe-return mirror verification; real timing; physical field observation recorded separately).
  **PR #42 remains OPEN / NOT MERGED; merge is on HOLD pending Owner authorization**, and no merge is
  performed by the implementation session. H3 remains **PLANNED, NOT AUTHORIZED**: the Owner TimeDelay
  decision (FROZEN PER COUNTDOWN for Auto, one fixed configured delay in Manual, Legacy inverse
  linear mapping from `RHTemp_SELECT` in Auto, Upper-zone factor 0.75, Left/Right Upper condition
  `OrderWall < 12`, Front/Rear Upper condition `OrderWall < 15`, modifier derived from the next Queue
  Sensor, and freeze of next-Sensor identity, Queue generation, RH input and window, Boiler Condition
  and configuration after Mandatory Safe Return with `AppliedDelay` immutable during the active
  countdown) is recorded as **documentation context only** and authorises no H3 implementation.

- The K6 implementation was authored in Arena; **Owner-local automated validation is the
  authoritative evidence** and has passed.
- **K6 is MERGED** (PR #24, merge commit `f356b162…`, 2026-10-14); canonical documentation promotion is complete.
- **Local TCP Simulation (2026-10-15, post-K6, not a roadmap stage):** the bench gained a third
  execution class, `--local-simulation` / `LOCAL_TCP_SIMULATION`, running the existing read-only
  acquisition once against a simulator on the Owner workstation at the literal loopback address
  `127.0.0.1:502` with the unchanged default request. It changes no physical protection, grants no
  physical authority and is not K7 or hardware evidence. Agent-local .NET validation **NOT RUN**;
  Owner-local validation complete on PR head `59d0ffdd` (affected gates PASS; EasyModbus run PASS,
  40 words exactly 100–139, `QUARANTINED`; Local Simulation evidence only); Owner merge decision
  pending. See [`TC_BENCH_LOCAL_SIMULATION.md`](TC_BENCH_LOCAL_SIMULATION.md)
  and [`CURRENT_STATE.md`](CURRENT_STATE.md) section 3A.
- **K7 is NOT AUTHORIZED**; neither K6 validation nor the local simulation class authorizes it.
- **Deterministic Product vertical slice (post-K6, PR #26 Source checkpoint):** one-Node read-only TC acquisition is wired in Source from an Application-owned deterministic session through the Runtime host's sole publication store, Snapshot/Delta APIs and the Runtime-backed Kiosk projection. Agent-local .NET build, tests and genuine lock restore are **NOT RUN**; Owner-local SDK `10.0.401` validation remains required before Merge. This is not physical evidence and does not authorize K7, T01, six-Node acquisition, Galil or Production.
- Detailed current truth: [`CURRENT_STATE.md`](CURRENT_STATE.md) (authoritative:
  [`CURRENT_STATE.md`](CURRENT_STATE.md)).

K6 delivery, grouped: identity and capability; authorization and trust (detached Owner-signed
verification, NIST P-256 / SHA-256, SubjectPublicKeyInfo, canonical payload, single-use consumption,
replay rejection, exact candidate binding); one-shot execution discipline (client construction only
after all gates pass, one client, one connection attempt, at most one read, no retry, no reconnect,
no fallback); lifecycle and evidence (outer lifecycle budget, cleanup-overdue evidence,
worker-observation evidence, generation and epoch fencing, quarantined output, no automatic ACCEPTED
path); explicit non-delivery (no passive capture, no Runtime/Kiosk integration, no device
write/control path). Itemised list:
[`CURRENT_STATE.md`](CURRENT_STATE.md) section 4.

K6 validation evidence summary (Owner-local is authoritative; Agent/Arena had no `dotnet`):

| Check | Result |
| --- | --- |
| Restore | normal PASS; locked PASS twice; lock drift none |
| Builds | targeted adapter, targeted bench, focused tests, full solution — Release PASS, no reported warnings or errors |
| Focused tests | **95/95** passed (0 failed, 0 skipped) |
| Full .NET | **550/550** passed (0 failed, 0 skipped) |
| Dry run | SIMULATION; physical capability false; connect/read NOT_ISSUED; QUARANTINED; no copied words; no physical I/O |
| Default PHYSICAL_TEST attempt | refused with `PHYSICAL_CAPABILITY_DISABLED`; `acquisitionPerformed` false; `deploymentState` `PHYSICAL_TEST_DEPLOYMENT_NOT_AUTHORIZED` |
| Node/static (Owner-local) | exporter 18/18; S2 14/14; S3 19/19; Boundary S1–S9 zero findings; Commissioning check PASS; Commissioning 218/218 on canonical LF; fixture parity verify-only 7/7; `WJSS_UPDATE_FIXTURES` false |
| R40 disposition | initial Windows run 217/218 (LF vs CRLF only); canonical LF rerun 218/218; original fixture restored byte-for-byte; restored SHA-256 `CC3F45ADF43CED351F1E8AE0D17D06AEA181FD94D119126DF0659AAC3385DA80`; canonical LF SHA-256 observed earlier as `3BC8774CB74183080297BB89C3A855DB8E46782B421DF41AB955C1CA50A8256A` (**not recomputed in the same execution as the final 218/218 run — labeled limitation**) |
| Agent/Arena | exporter 18/18; S2 14/14; S3 19/19; Boundary clean; Commissioning 220/220 with the corrected documentation guard (218 pre-existing tests plus 2 authorized regression tests); JavaScript syntax and changed-path/protected-surface proofs; genuine locks unchanged; **no** C# build, analyzer, xUnit/.NET test, .NET fixture parity or physical execution |
| Physical | none — no hardware contact, no passive capture, no port 502 access, no T01 execution |

## 4. Proposed Version Roadmap

Roadmap authority: [`docs/MASTER_PLAN.md`](MASTER_PLAN.md). This section is the roadmap of record:
the roadmap and the stage identifiers it introduces became authoritative through the completed,
validated Atomic Replacement.

### 4.1 Completed/Merged history

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K1` | Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview | MERGED | Kiosk shell foundation and the sensor commissioning monitor preview | — |
| `0.4C-K2` | TC Node/IP Configuration Foundation | MERGED | Canonical six-node TC node/IP configuration foundation | 0.4C-K1 |
| `0.4C-K3` | 108-Sensor / 216-Measurement Topology Correction | MERGED | Owner critical topology correction: I7 and I16 are normal Product Sensors | 0.4C-K2 |
| `0.4C-K4` | Modbus TCP Client Library Selection and Read-Only Adapter Foundation | MERGED (PR #22) | Transport library selection and the read-only acquisition adapter foundation | 0.4C-K3 |
| `0.4C-K5` | One-Node Physical TC Read-Only Acquisition Software Foundation | MERGED (PR #23, `193aba76…`) | Isolated one-shot bench software foundation; diagnostic success quarantined | 0.4C-K4 |

### 4.2 Current Stage

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K6` | T01 Physical Execution Enablement and Bench Readiness | **CURRENT — IMPLEMENTED AND VALIDATED — MERGED (PR #24, `f356b162…`, 2026-10-14)**; post-K6 Local TCP Simulation bench class on branch, PR #25 open, not merged | Signed, single-use authorization and fail-closed one-shot physical execution path; no trial performed | 0.4C-K5 |

### 4.3 Next Stage

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K7` | T01 One-Node Physical TC Read-Only Trial | **NEXT — NOT AUTHORIZED** | The first authorized one-node physical read-only trial (one invocation, one client, one connect, at most one read) | 0.4C-K6 (merged `f356b162…`); its own Owner Scope Gate |

### 4.4 Planned TC acquisition and commissioning stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K8` | One-Node Evidence Closeout and Read-Profile Decision | PLANNED - NOT AUTHORIZED | Close out one-node evidence and decide the read profile | 0.4C-K7 |
| `0.4C-K9` | Six-Node TC Read-Only Acquisition Software Expansion | PLANNED - NOT AUTHORIZED | Extend read-only acquisition software to six nodes | 0.4C-K8 |
| `0.4C-K10` | Six-Node Physical TC Read-Only Acquisition | PLANNED - NOT AUTHORIZED | Physical read-only acquisition across six nodes | 0.4C-K9 |
| `0.4C-K11` | Physical TC Channel Mapping Verification | PLANNED - NOT AUTHORIZED | Verify physical TC channel mapping | 0.4C-K10 |
| `0.4C-K12` | Runtime Physical TC Publication Foundation | PLANNED - NOT AUTHORIZED | Runtime publication foundation for physical TC data | 0.4C-K11 |
| `0.4C-K13` | Kiosk Physical TC Commissioning Integration | PLANNED - NOT AUTHORIZED | Kiosk commissioning integration for physical TC data | 0.4C-K12 |
| `0.4C-K14` | Physical TC Baseline Commissioning | PLANNED - NOT AUTHORIZED | Commission the physical TC baseline | 0.4C-K13 |
| `0.4C-K15` | Pilot TC Loop Test | PLANNED - NOT AUTHORIZED | Pilot TC loop test | 0.4C-K14 |
| `0.4C-K16` | Full TC Loop Test - 108 Sensors / 216 Measurements | PLANNED - NOT AUTHORIZED | Full TC loop test at 108 Sensors / 216 Measurements | 0.4C-K15 |
| `0.4C-K17` | Physical TC Acquisition and Commissioning Closeout | PLANNED - NOT AUTHORIZED | Close out physical TC acquisition and commissioning | 0.4C-K16 |

### 4.5 Galil and motion stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4D-G1` | Galil Hardware and Communication Evidence Gate | PLANNED - NOT AUTHORIZED | Evidence gate for Galil hardware and communication | `0.4C-K17` (Owner-approved dependency) |
| `0.4D-G2` | Galil Read-Only Status Acquisition | PLANNED - NOT AUTHORIZED | Read-only Galil status acquisition | 0.4D-G1 |
| `0.4D-G3` | Galil Controlled Motion Test Foundation | PLANNED - NOT AUTHORIZED | Foundation for controlled Galil motion test | 0.4D-G2 |
| `0.4D-G4` | Galil Axis Functional Test | PLANNED - NOT AUTHORIZED | Galil axis functional test | 0.4D-G3 |
| `0.4D-G5` | Galil Motion and Mandatory Safe Return Closeout | PLANNED - NOT AUTHORIZED | Close out Galil motion and mandatory safe return | 0.4D-G4 |

### 4.6 Integrated dry-cycle stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4E-I1` | Integrated Dry-Cycle Scope and Safety Gate | PLANNED - NOT AUTHORIZED | Scope and safety gate for the integrated dry cycle | `0.4D-G5` (Owner-approved dependency) |
| `0.4E-I2` | Single-Job Integrated Dry Cycle | PLANNED - NOT AUTHORIZED | Single-job integrated dry cycle | 0.4E-I1 |
| `0.4E-I3` | Sequential Multi-Job Integrated Dry Cycle | PLANNED - NOT AUTHORIZED | Sequential multi-job integrated dry cycle | 0.4E-I2 |
| `0.4E-I4` | Integrated Dry-Cycle Closeout | PLANNED - NOT AUTHORIZED | Close out the integrated dry cycle | 0.4E-I3 |

### 4.7 Production-readiness stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.5A-P1` | Production Readiness Scope Gate | FUTURE - NOT AUTHORIZED | Scope gate for production readiness | `0.4E-I4` (Owner-approved dependency) |
| `0.5A-P2` | Production Configuration and Access Boundary | FUTURE - NOT AUTHORIZED | Production configuration and access boundary | 0.5A-P1 |
| `0.5A-P3` | Production Pilot Operation | FUTURE - NOT AUTHORIZED | Production pilot operation | 0.5A-P2 |
| `0.5A-P4` | Production Acceptance and Operational Handover | FUTURE - NOT AUTHORIZED | Production acceptance and operational handover | 0.5A-P3 |

**Dependency note (Owner-approved in CR-02; roadmap dependency only).** `0.4D-G1` follows
`0.4C-K17`, `0.4E-I1` follows `0.4D-G5` and `0.5A-P1` follows `0.4E-I4`. This approval establishes
roadmap dependency only: it authorizes none of these stages, adds no dates, and does not authorize an
automatic transition when a predecessor completes — each stage still requires its own explicit Owner
Scope Gate. Statuses remain `PLANNED - NOT AUTHORIZED` for `0.4D-G1` and `0.4E-I1`, and
`FUTURE - NOT AUTHORIZED` for `0.5A-P1`.

### 4.8 Historical Reconciliation

The items below were previously reported as conflicts or open points. Each is reconciled by later,
already-established evidence and requires **no new Owner decision**; the preserved historical text is
not rewritten. Every item previously reported in this section is mapped here or in section 4.9.

1. **Dated "OPEN — NOT MERGED" wording.** The preserved historical text and older entries describe
   `0.4C-K2` (PR #20) and `0.4C-K3` (PR #21) as `OPEN — NOT MERGED` at their checkpoints, while
   the proposed roadmap records them as **MERGED**. Both are correct for their own dates: the
   dated wording is the record of that checkpoint, and the merges are established by later
   evidence (K3 additionally recorded inside the preserved README text as
   `d1ea0324cb37de65a798fe4271429f955d4a9225`, parents `cc6ccf87` / `a6238ac`). Reconciliation:
   dated record retained unchanged; current status in section 4.1.

2. **Binding of stages to pull requests.** Established evidence binds `0.4C-K4` to PR #22 and
   `0.4C-K5` to PR #23 (`193aba76…`). Earlier text binds the Library Selection Gate to PR #22 and
   does not bind a PR number to `K4` or `K5` by those identifiers — an earlier naming granularity,
   not a conflict. Reconciliation: the proposed map adds the identifiers without altering any
   earlier text.

3. **New identifiers.** `0.4C-K6`–`0.4C-K17`, `0.4D-G1`–`G5`, `0.4E-I1`–`I4` and `0.5A-P1`–`P4`
   are **authoritative for this plan**, having become canonical through the completed, validated Atomic
   Replacement; they do not all appear in the preserved historical text. No historical record was
   altered to introduce them; section 4.8 records that governance transition.

4. **Dependencies previously unstated — now decided by the Owner.** `0.4D-G1`, `0.4E-I1` and
   `0.5A-P1` had no stated preceding dependency in the earlier source material. This was resolved
   by the Owner in CR-02 (roadmap dependency only): `0.4D-G1` ← `0.4C-K17`, `0.4E-I1` ← `0.4D-G5`,
   `0.5A-P1` ← `0.4E-I4`, recorded in sections 4.5–4.7, 6 and 7. Statuses, dates and authorization
   are unchanged: none of these stages is authorized, none has a date, and completion of a
   predecessor does not authorize a successor automatically.

5. **Superseded current-position text.** The preserved `Current critical path — 2026-10-13`
   section describes the K5-era closeout next steps (`e71e33b…`). That position was completed by
   the PR #23 merge into `main` at `193aba76…`, and K6 followed. Reconciliation: the preserved
   K5-era text is the record of its date; the current position is section 3 above. The preserved
   text is not rewritten.

6. **Earlier, more specific records.** The preserved sections contain more specific details for
   many stages (for example the Stage 0.4A CP-2/CP-3 records). Those details are retained verbatim
   in section 11 and are not replaced by the brief purposes above.

7. **Atomic Replacement completed.** The Owner approved the exact Candidate revision and separately
   authorized Atomic Replacement. The replacement was completed and validated at the replacement
   commit reported by Git; no future commit SHA was pre-written.

### 4.9 Owner Decisions Still Required

Genuine decisions that remain open and are **not** resolved by this document:

1. **No Scope Gate exists for any stage after K6.** `0.4C-K7` and every later stage — including
   `0.4D-G1`, `0.4E-I1` and `0.5A-P1` — require their own explicit Owner Scope Gate before any work
   may begin. None is authorized, scheduled or dated.

## 5. Critical Path

Current critical path (Owner-authorized 2026-10-25), as conditional steps on the Hardware
Critical Path working labels mapped to the formal stage families (formal IDs unchanged):

1. **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning — CURRENT, OWNER-AUTHORIZED.** Home →
   DP/DE → dual-axis Move → selected Sensor P1→P6 → Centre 125 mm / 125 mm → Stop / Abort /
   Brake / cleanup on WSB1, dry (no Pump command, no physical IV command, no water, no
   Queue / AutoSequence, no Production). Documentation realignment, exact-source inspection, H1
   Product implementation, deterministic Simulation validation, Owner-local software validation
   preparation and exact real-Hardware trial preparation are authorized in the current PR; real
   Hardware execution requires separate explicit Owner authorization for the exact validated PR
   head and the exact reviewed trial procedure. (Formal family: `0.4D-G1`–`0.4D-G5`.)
2. **H2 — Physical Pump/IV I/O Commissioning — NEXT, NOT AUTHORIZED.** (Formal mapping `[OPEN]`.)
3. **H3 — Single Real Cleaning Job — PLANNED, NOT AUTHORIZED.** (Formal mapping `[OPEN]`.)
4. **H4 — Sequential Multi-Job/Queue — PLANNED, NOT AUTHORIZED.** (Formal family `0.4E-I3`.)
5. **H5 — Production Readiness — FUTURE, NOT AUTHORIZED.** (Formal family `0.5A-P1`–`0.5A-P4`.)

Historical critical-path record (preserved verbatim as dated history; superseded as the current
position by the H1 Owner authorization above):

1. Final canonical documentation and complete source review. — **DONE** (K6)
2. Atomic Replacement completed and validated. — **DONE**
3. Owner PR review and merge decision for the K6 checkpoint. — **DONE: PR #24 MERGED at `f356b162` (2026-10-14)**
3A. Local TCP Simulation bench class: Owner-local build/test, Owner-local simulator run, Owner merge decision. — **PR #25 OPEN, NOT MERGED; Owner-local build/test and simulator run DONE on `59d0ffdd` (Local Simulation evidence only, `QUARANTINED`); Owner merge decision PENDING**
4. Separate Owner Scope Gate for `0.4C-K7`. — **NOT AUTHORIZED**
5. Subsequent stages in dependency order per section 6. — **PLANNED / FUTURE, NOT AUTHORIZED**

No batch approval exists: each later trial requires separate authorization, and a failure does not
authorize another candidate.

## 6. Stage Dependency Map

| Stage | Depends on |
| --- | --- |
| `0.4C-K1` … `0.4C-K5` | completed in sequence; each merged |
| `0.4C-K6` | `0.4C-K5` |
| `0.4C-K7` | `0.4C-K6` (merged — PR #24, `f356b162…`, 2026-10-14); K7 remains **NEXT — NOT AUTHORIZED**. The post-K6 Local TCP Simulation bench class (PR #25, open, not merged) is supporting bench capability, not a K7 prerequisite and not physical completion |
| `0.4C-K8` … `0.4C-K17` | the immediately preceding `0.4C-K` stage |
| `0.4D-G1` | `0.4C-K17` (Owner-approved dependency) |
| `0.4D-G2` … `0.4D-G5` | the immediately preceding `0.4D-G` stage |
| `0.4E-I1` | `0.4D-G5` (Owner-approved dependency) |
| `0.4E-I2` … `0.4E-I4` | the immediately preceding `0.4E-I` stage |
| `0.5A-P1` | `0.4E-I4` (Owner-approved dependency) |
| `0.5A-P2` … `0.5A-P4` | the immediately preceding `0.5A-P` stage |

## 7. Stage Definitions

### 7.1 Completed Stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K1` | Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview | MERGED | Kiosk shell foundation and the sensor commissioning monitor preview | — |
| `0.4C-K2` | TC Node/IP Configuration Foundation | MERGED | Canonical six-node TC node/IP configuration foundation | 0.4C-K1 |
| `0.4C-K3` | 108-Sensor / 216-Measurement Topology Correction | MERGED | Owner critical topology correction: I7 and I16 are normal Product Sensors | 0.4C-K2 |
| `0.4C-K4` | Modbus TCP Client Library Selection and Read-Only Adapter Foundation | MERGED (PR #22) | Transport library selection and the read-only acquisition adapter foundation | 0.4C-K3 |
| `0.4C-K5` | One-Node Physical TC Read-Only Acquisition Software Foundation | MERGED (PR #23, `193aba76…`) | Isolated one-shot bench software foundation; diagnostic success quarantined | 0.4C-K4 |

Detailed historical records for these stages remain verbatim in section 11 and in
[`../CHANGELOG.md`](../CHANGELOG.md).

### 7.2 Current Stage

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K6` | T01 Physical Execution Enablement and Bench Readiness | **CURRENT — IMPLEMENTED AND VALIDATED — MERGED (PR #24, `f356b162…`, 2026-10-14)** | Signed, single-use authorization and fail-closed one-shot physical execution path; no trial performed | 0.4C-K5 |

Post-K6 bench tooling (not a stage): Local TCP Simulation execution class, see
[`TC_BENCH_LOCAL_SIMULATION.md`](TC_BENCH_LOCAL_SIMULATION.md) and
[`CURRENT_STATE.md`](CURRENT_STATE.md) section 3A.

Implemented delivery and evidence for K6: see
[`CURRENT_STATE.md`](CURRENT_STATE.md) sections 4 and 5.

### 7.3 Planned Stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4C-K8` | One-Node Evidence Closeout and Read-Profile Decision | PLANNED - NOT AUTHORIZED | Close out one-node evidence and decide the read profile | 0.4C-K7 |
| `0.4C-K9` | Six-Node TC Read-Only Acquisition Software Expansion | PLANNED - NOT AUTHORIZED | Extend read-only acquisition software to six nodes | 0.4C-K8 |
| `0.4C-K10` | Six-Node Physical TC Read-Only Acquisition | PLANNED - NOT AUTHORIZED | Physical read-only acquisition across six nodes | 0.4C-K9 |
| `0.4C-K11` | Physical TC Channel Mapping Verification | PLANNED - NOT AUTHORIZED | Verify physical TC channel mapping | 0.4C-K10 |
| `0.4C-K12` | Runtime Physical TC Publication Foundation | PLANNED - NOT AUTHORIZED | Runtime publication foundation for physical TC data | 0.4C-K11 |
| `0.4C-K13` | Kiosk Physical TC Commissioning Integration | PLANNED - NOT AUTHORIZED | Kiosk commissioning integration for physical TC data | 0.4C-K12 |
| `0.4C-K14` | Physical TC Baseline Commissioning | PLANNED - NOT AUTHORIZED | Commission the physical TC baseline | 0.4C-K13 |
| `0.4C-K15` | Pilot TC Loop Test | PLANNED - NOT AUTHORIZED | Pilot TC loop test | 0.4C-K14 |
| `0.4C-K16` | Full TC Loop Test - 108 Sensors / 216 Measurements | PLANNED - NOT AUTHORIZED | Full TC loop test at 108 Sensors / 216 Measurements | 0.4C-K15 |
| `0.4C-K17` | Physical TC Acquisition and Commissioning Closeout | PLANNED - NOT AUTHORIZED | Close out physical TC acquisition and commissioning | 0.4C-K16 |

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4D-G1` | Galil Hardware and Communication Evidence Gate | PLANNED - NOT AUTHORIZED | Evidence gate for Galil hardware and communication | `0.4C-K17` (Owner-approved dependency) |
| `0.4D-G2` | Galil Read-Only Status Acquisition | PLANNED - NOT AUTHORIZED | Read-only Galil status acquisition | 0.4D-G1 |
| `0.4D-G3` | Galil Controlled Motion Test Foundation | PLANNED - NOT AUTHORIZED | Foundation for controlled Galil motion test | 0.4D-G2 |
| `0.4D-G4` | Galil Axis Functional Test | PLANNED - NOT AUTHORIZED | Galil axis functional test | 0.4D-G3 |
| `0.4D-G5` | Galil Motion and Mandatory Safe Return Closeout | PLANNED - NOT AUTHORIZED | Close out Galil motion and mandatory safe return | 0.4D-G4 |

The `0.4D-G` family above is the formal-stage home of the current Owner-authorized Product
Critical Path under the **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning** working label
(2026-10-25): dry commissioning of Home, DP/DE, dual-axis Move, selected-Sensor P1→P6, Centre
125 mm / 125 mm and Stop / Abort / Brake / cleanup on WSB1, through existing typed boundaries.
The formal stage IDs and the per-stage statuses above remain stable (dated planning record);
H1's authorization covers the dry commissioning scope only, and no stage closeout or later
stage is authorized by that appearance.

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.4E-I1` | Integrated Dry-Cycle Scope and Safety Gate | PLANNED - NOT AUTHORIZED | Scope and safety gate for the integrated dry cycle | `0.4D-G5` (Owner-approved dependency) |
| `0.4E-I2` | Single-Job Integrated Dry Cycle | PLANNED - NOT AUTHORIZED | Single-job integrated dry cycle | 0.4E-I1 |
| `0.4E-I3` | Sequential Multi-Job Integrated Dry Cycle | PLANNED - NOT AUTHORIZED | Sequential multi-job integrated dry cycle | 0.4E-I2 |
| `0.4E-I4` | Integrated Dry-Cycle Closeout | PLANNED - NOT AUTHORIZED | Close out the integrated dry cycle | 0.4E-I3 |

The purposes above are brief and derived from the stage titles; no implementation commitment,
authorization, start date or schedule is implied.

### 7.4 Future Stages

| Stage | Title | Status | Brief purpose | Preceding dependency |
| --- | --- | --- | --- | --- |
| `0.5A-P1` | Production Readiness Scope Gate | FUTURE - NOT AUTHORIZED | Scope gate for production readiness | `0.4E-I4` (Owner-approved dependency) |
| `0.5A-P2` | Production Configuration and Access Boundary | FUTURE - NOT AUTHORIZED | Production configuration and access boundary | 0.5A-P1 |
| `0.5A-P3` | Production Pilot Operation | FUTURE - NOT AUTHORIZED | Production pilot operation | 0.5A-P2 |
| `0.5A-P4` | Production Acceptance and Operational Handover | FUTURE - NOT AUTHORIZED | Production acceptance and operational handover | 0.5A-P3 |

## 8. Global Safety and Authorization Gates

- Every stage requires its own Owner Scope Gate; a Stage Gate being approved authorizes work, not
  acceptance of results.
- The default build remains **physically disabled**; a physical-capable deployment identity requires
  explicit Owner-supplied trust metadata.
- **K6 validation does not authorize K7.**

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

The full standing constraints that apply to all stages are preserved verbatim in section 11
(`## 5. Standing constraints across every stage`), including the prohibition on production device
access, production write control, invented production values, the single-active-Cleaning-Job
invariant, the absence of an application version, and the statement that the Operations UI close
guard is not a safety protection.

## 9. Validation and Completion Rules

Operating rules that remain in force (full original text preserved in section 11):

- Work proceeds in small, explicitly approved stages, each with a Scope Gate, one reviewable
  deliverable set, documentation validation evidence, a Development Checkpoint commit and Owner review
  before the next stage begins.
- Stage gates state the stage identifier and title, the exact files or categories that may be created
  or modified, what is out of scope, the validation required, and the delivery instruction.
- Every stage report states **CHANGED**, **UNCHANGED**, **NOT VERIFIED**, and the **validation
  performed** with observed results. Claims of success without observed tool output are prohibited.
- A documentation stage is done only when the named files exist and are complete, internal links
  resolve, the status legend is applied consistently, Owner-confirmed decisions are propagated,
  prohibited content is absent, the diff has been reviewed, the checkpoint commit exists with a
  CHANGED / UNCHANGED / NOT VERIFIED body, the branch is pushed, and a pull request exists if
  permissions allow — and the pull request is **not** merged by the contributor.
- A stage that received review changes is not done until a review-correction checkpoint is submitted
  and re-review is requested.

## 10. Deferred / Optional Work

- **Stage 0.2.1 proposed UI and Runtime technology spike** — retained as a recorded proposal in
  section 11 (`### 3.2 Proposed Stage 0.2.1 — UI and Runtime Technology Spike`); the related Stage
  0.2.1A feasibility spike, already completed, is retained as history.
- **Blazor counter-spike** — recorded as `NOT REQUIRED` unless a future material blocker is identified
  (preserved README/`[Unreleased]` text).
- **Wire-break decoding** — `BLOCKED`: the model capability is documented but the Modbus cyclic image
  carries no explicit status.
- **Stage 0.4C-1 TC acquisition** — recorded as `BLOCKED` in earlier Stage 0.4C-0B records; later
  stages were realigned by the K-series roadmap in section 4.
- **Production transport and chart library** — remain `[OPEN]` per the preserved records.
- **Documentation versioning policy** — remains `[OPEN]`.

## 11. Historical Master Plan Records

The authoritative `docs/MASTER_PLAN.md` is preserved verbatim below, including the dated critical-path
section, the delivery principle, the stage-gate definition, the complete stage ledger, the earlier
reconciliation and proposal sections, the gate evidence rules, the standing constraints, the
definition of done and the related-document list. Heading levels are demoted by two so this document
keeps one heading hierarchy.

**Supersession notes (navigation only; the preserved text is not rewritten):**

- The preserved `## Current critical path — 2026-10-13` section records the K5-era position
  (`c0bc2c7…` / `e71e33b…`). That work was merged as PR #23 into `main` at `193aba76…`. The current
  position is section 3 above.
- The preserved stage ledger in section 11 contains dated, more specific records for stages up to
  Stage 0.4C-K3 and the Library Selection Gate. The roadmap in section 4 covers
  `0.4C-K1` … `0.5A-P4`; earlier-vs-current reconciliation is recorded in section 4.8 and the
  remaining Owner decisions in section 4.9, rather than resolved by editing the preserved text.

---

### Historical record — verbatim preservation of the authoritative original

The block below is the complete text of `docs/MASTER_PLAN.md` as it exists at candidate source head
`00eb3c6c6ad581067aeef79b5c40b4dbb309194e`. Wording, tables, links, counts, SHAs, dates and statuses are unchanged. Only the
Markdown heading level is shifted by two (for example `##` → `####`) so that this candidate keeps a
single heading hierarchy; every heading's original text is retained verbatim.

### Master Plan — WaterJet Sentinel Suite (WJSS)

#### Current critical path — 2026-10-13

**One-Node TC bench software implementation validated; documentation closeout → Owner PR review.**
Exact validated implementation `c0bc2c71636d3206098fd890e1de40c46fabae65`; genuine lock commit
`e71e33b22d039b6cd5940fbe94da1879f71142fa`. Owner restore/build/test/verify-only gates are closed:
targeted/full Release **0w/0e**, focused **81/81**, full .NET **536/536**, dry-run SIMULATION only,
fixture parity **7/7**. Agent validation remains separately scoped to lock/static/Node/doc proofs.
[Full evidence and BOTH LF/CRLF lock hashes](STAGE_ONE_NODE_TC_SCOPE_GATE.md) ·
[ADR-0019](decisions/ADR-0019-isolated-one-node-tc-bench.md).

Next: atomic docs-only closeout → normal assigned-branch push → verify final SHA/direct parent
`e71e33b` and clean identity → OPEN PR targeting `main` → Owner review. MERGE READY applies
ONLY to software review after every required closeout check passes; Agent never merges.
Approach A is isolated one-shot sole bench-session ownership; Runtime/Kiosk unchanged. T01 is
non-executable, no Product publication/interpretation/write. Package versions/locks/topology and
canonical evidence statuses stay protected. R40 and lock hash differences are representation-only.

Future physical work requires its OWN identity/isolation, sole-ownership/lifetime/stop, T01,
deployment, capture/storage and wire/Owner-review gates. No batch trials, six-Node expansion,
Runtime/Kiosk physical integration or Production approval follows from software readiness.
Library selection stays closed. Historical plans below are retained, not current instructions.
**HARDWARE CONTACT NOT AUTHORIZED; T01 NOT AUTHORIZED; PASSIVE CAPTURE NOT AUTHORIZED;
PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED.**

**Document status:** [APPROVED] for the stage-gate discipline, for the Stage 0.1 and Stage 0.2
positions, and for the approved Stage 0.2 Scope Gate; **stage contents beyond Stage 0.2 remain
`[PROPOSED]`**.
**Governing authority:** [`AGENTS.md`](../AGENTS.md), the approved Stage 0.1 Scope Gate, and
the approved Stage 0.2 Scope Gate — *Technology and Solution Architecture Decision*.

This document defines how delivery is staged, what a stage gate is, and what evidence a
stage must produce. It deliberately does **not** schedule work, and it does not authorise
any future stage.

---

#### 1. Delivery principle

Work proceeds in small, explicitly approved stages. Each stage has:

- A Scope Gate issued by the Owner, naming exactly what may be created or changed.
- A single reviewable deliverable set.
- Documentation validation evidence.
- A Development Checkpoint commit.
- Owner review before the next stage begins. A review may request changes, which are
  applied as review-correction commits on the same branch and pull request.

No stage begins automatically when the previous one ends. A completed stage produces a
report and stops. See [`AGENTS.md`](../AGENTS.md) section 11 for the required stop
conditions.

**Gate approval and implementation acceptance are separate.** A Stage Gate being approved
means the work is authorised. It does not mean the resulting implementation is accepted.
Only the Owner may record acceptance.

#### 2. What a stage gate is

A Stage Gate is the only authority that permits new artefacts. It must state, at minimum:

1. The stage identifier and title.
2. What may be created or modified, as an explicit file list or an explicit category list.
3. What is explicitly out of scope for that stage.
4. The validation the contributor must perform and report.
5. The delivery instruction (branch, checkpoint, and whether a pull request is expected).

Behaviour described in a Scope Gate becomes `[APPROVED]` behaviour. Behaviour confirmed by
the Owner during a documentation review becomes `[OWNER CONFIRMED]` behaviour. Behaviour
described anywhere else remains `[PROPOSED]` until a later gate approves it.

#### 3. Stage ledger

**Library Selection Gate — SELECT_FLUENTMODBUS_5_3_2 — MERGE READY (2026-10-13; PR #22 OPEN targeting main, head `fdc81d801aecabd3f00350948d69a9f9b49f9244`, parent `91f5a68fdb6cfec74efc82f5237f47c600c61728`, genuine lock `49e404356edee64bb9dbaccd395d8bcc66435881` on `d1ea0324`, implementation repair `b31d1041125b7ea0d5fa758c02c6994edd32e0d4`): FluentModbus 5.3.2 selected, NModbus 3.0.83 rejected, Raw Socket not selected; foundation `adapters/tc-acquisition` (`ITcReadOnlyClient`, explicit FC03/FC04 no fallback, BigEndian candidate, one-outstanding READ_BUSY, cancellation/timeout/error classification, generation/stale fencing, memory copy, no write, self-owned, genuine `packages.lock.json` `49e4043`); Owner-local validation PASS (genuine restore PASS, locked restore PASS×2 no drift, targeted Release 0w0e, full Release 0w0e, full .NET 455/455, S2 14/14 S3 11/11 S1–S9 clean, Commissioning 218/218, sensor-map parity PASS), Arena lock/S2/S3/Boundary/Commissioning PASS (no Arena .NET); topology unchanged 6/40/240/216/24/108/I7/I16/502; NOT VERIFIED physical connectivity/reachability/WAGO/FC03-vs-FC04/Unit ID/start register/byte-word order/scaling/wire-break/timing/concurrency/stale/PHYSICAL_TEST/PRODUCTION/device write; next critical-path after Owner merge → new session → new branch → Scope Gate: One-Node Physical TC Read-Only Acquisition (one Node, 40-word candidate, prove reachability/FC/Unit ID/start/raw words/interpretation, no write, no Production, no six-Node until one-Node passes); future: 6 Nodes→216/24 mapping→Kiosk PHYSICAL_TEST Monitor→commissioning baseline→pilot 2–4→full 108/216→Galil G1–G5→Dry Cycle→Production; prohibited probe/write/Production; record: [`STAGE_LIBRARY_SELECTION_GATE_REPORT`](STAGE_LIBRARY_SELECTION_GATE_REPORT.md) and [`ADR-0018`](decisions/ADR-0018-modbus-tcp-client-library-selection.md). PR #22 OPEN — MERGE READY (do not pre-write main merge SHA).**

**Stage 0.4C-K3 final closeout (2026-10-12; PR #21 OPEN / UNMERGED pending final PR checks and the Owner merge action — not merged): Stage 0.4C-K3 = COMPLETE FOR MERGE REVIEW; Automated Gates = PASS; Owner Manual Review = PASS on exact head `b5c7dff2f944956c608d89b903cb151ea6eed551` (measurement-quality terminology correction verified visually — DISABLED = measurement unavailable / Channel not commissioned, separate from Sensor operational Enabled / Disabled; topology presentation 108 / 216 / 240 / 24 / 0 conflicts / 12 internal conflicts / I7 and I16 normal verified; evidence-honesty presentation verified; Kiosk Stage 0.4C-K2 identity confirmed intentional; Sensor Disable mutation UI NOT_IMPLEMENTED); Schema Decision = RESOLVED — RETAIN_SCHEMA_V2.** The paragraph that follows is the dated automated-gate closeout at `c4d77974…` (its "OWNER MANUAL REVIEW PENDING" wording is superseded).

**Stage 0.4C-K3 automated-gate closeout (2026-10-12; dated record at `c4d77974…`): K3 = AUTOMATED GATES PASSED, OWNER MANUAL REVIEW PENDING (since PASSED, see above).** After the Owner critical topology correction below, the K3 Completion Pass moved the .NET contracts, domain, runtime, API, generators and tests to the 108 / 216 / 240 / 24 baseline, added the central `packages/contracts/TopologyInvariants.cs`, implemented the Sensor Disable foundation in the canonical Runtime / Sequencing kernel (Disabled Sensor: no Cleaning Candidate, no GlobalQueue entry, no auto-dispatch; queued non-active entry removed immediately with reason `SENSOR_DISABLED`, FIFO preserved; Active Job never auto-aborted; re-enable never resurrects or reuses stale classification; unknown Sensor fails closed; I7 / I16 identical to every Sensor; mutation UI **NOT_IMPLEMENTED**, read-only exposure only), set WJ1 / WJ3 Installed Position to **UNRESOLVED** (no former-gap anchors; Target Coverage unchanged; cannon n → WJn; WJn ↔ IVn), and regenerated the golden fixture and migrated example with proven generators. Owner-local validation at exact head `041b286322186977c5f87cde8184ae5a180d60a6`: locked restore PASS; Release build PASS; full .NET **455 / 455**; fixture emission **7 / 7** and a second deterministic run **7 / 7 with no diff**; TypeScript typecheck PASS; contracts-ts **36 / 36**; working tree CLEAN. **Schema `/2` retained — OQ-T3 RESOLVED (RETAIN_SCHEMA_V2).** Evidence-honesty positions unchanged (`PROTOCOL_UNRESOLVED`, candidate `WITHDRAWN`, `PORT_UNRESOLVED`, `DERIVATION_BLOCKED`, `RECORD_TO_RECORD_CONSISTENCY_ONLY`, `WIRE_BREAK_DECODING_BLOCKED`; no hardware, no MODBUS, no TEST_HARDWARE, no PRODUCTION, no device write). Not claimed: Manual Review passed, PR-ready, K3 complete. Record: [`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md) §12–§13.

**Owner Critical Topology Correction (2026-10-12; on the Stage 0.4C-K3 branch, PR #21 — dated record; the "not performed in Arena" statements below were closed by the K3 closeout above):** the Owner revoked the 106-Sensor / 212-channel baseline: **I7 and I16 are normal Product Sensors**; 108 Sensors, 216 assigned Product TC measurements, 240 physical, 24 unassigned. Arena-native layers corrected with a central invariant check (`packages/commissioning/src/topologyInvariants.mjs`, wired into the commissioning package and the Ops Preview evidence load, fail-closed): commissioning schedule / wall map / thermal pair / presentation / scenarios, the Ops Preview server and UI, the regenerated `config/examples/sensor-map.example.json` (Node port of the C# generator), the schedule CSV and template, the TypeScript contract mirror, the Inspector page, and the documentation set (current-facing docs corrected; historical records banner-marked). **Not performed in Arena (no .NET SDK):** the C# contracts, domain catalogue, importer, runtime projection, synthetic map, fixture generators and 414-test suite still encode the superseded form; `packages/contracts/fixtures/snapshot.seed0.json` and `config/examples/sensor-parameters.migrated.example.json` are stale and flagged; the WJ1/WJ3 placement vocabulary is an open Owner decision; Disable/GlobalQueue semantics for I7/I16 are specified but have no executable Product implementation yet. Record: [`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md).

**Stage 0.4C-K1 Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview (2026-10-12; development checkpoint, Product source plus tests, new PR against `main`):** new session after **PR #18 MERGED** into `main` at `48f0315ea1bcbd3f71f46a47bb4b55fc04327487` (parents `3e1a7fd9` and `b2048ec0`, verified); platform-assigned branch `arena/3be5c713-waterjet-sentinel-suite`; base gate PASSED; **sandbox recreation not triggered**; workbook SHA-256 re-verified and unchanged. Owner dependency decision: **`Microsoft.Web.WebView2` pinned `1.0.4258.31`** (Evergreen runtime), applied centrally in `Directory.Packages.props` with a version-free reference in `apps/kiosk/Wjss.Kiosk.csproj` — the Stage's only dependency change; Edge application/kiosk mode, the legacy web-browser control, Electron and a non-building source-only shell were rejected by the Owner. Delivered: the real application-owned `apps/kiosk` shell (approved loopback origin only; navigation, popup, download and permission refusal; developer tools off; a visible Runtime-unavailable / reconnecting state that hides the view so no stale value is presented as current; confirmed Operator exit; no process start, no record, no credential), the read-only `packages/commissioning` model of the committed 240-row schedule (three separated dimensions Quality / DiagnosticReason / CommissioningState, `INVALID` presentation-only, invalid values never rendered as numbers, last valid sample labelled historical) with the ten deterministic scenarios, and the loopback-only read-only `apps/ops-preview` host with the Sensor Commissioning Monitor as the Product-shaped main area (GET/HEAD only, 405 otherwise, `default-src 'self'`, no remote asset, no route for any device/transport/control action). Validation in Arena: commissioning **77/77**; ops-preview **40/40**; kiosk shell source-contract **12/12**; Mapping regression **82/82** and **268 (261 pass, 7 skipped)**; boundary scan **S1–S9 clean, 0 findings**. **NOT verified in Arena:** the .NET restore/build/run of the changed graph (no .NET SDK, no NuGet — the Kiosk lock refresh is Owner-local), Windows runtime behaviour, DPI rendering, 1920×1080 visual acceptance and the embedded-view runtime; the shell suite is a static source contract, not a substitute. **NO HARDWARE, no live MODBUS, no TEST_HARDWARE, no PRODUCTION, no device write or control; `WIRE_BREAK_DECODING: BLOCKED`; field verification PENDING.** `README.md` was **not** modified (CF-07 stays open). NEW PR against `main` — OPEN, NOT MERGED (do not merge). See §3.9.

**Stage 0.4C-0B Six-Node Thermocouple Topology Baseline Rev1 (2026-10-11 checkpoint, evidence-blocked; 2026-10-12 evidence-review completion on the same branch):** new session after **PR #17 MERGED**, on the platform-assigned branch `arena/c4b63a26-waterjet-sentinel-suite`, approved remote-main base `main` `3e1a7fd9d28db65221c123960a0c85b62d93949a` (the PR #17 merge commit; parents `bd143758` and `48d8605a`, verified; PR #17 MERGED at `2026-10-11T08:59:28Z`). Recorded the **Owner-confirmed six-node topology baseline rev1 as brief-stated design basis** (6 nodes × `750-362 × 1 + 750-498 × 5 + 750-600 × 1`; 240/212/28 capacity; `OWNER_CONFIRMED_FACTORY_DEFAULT`; TEST ONLY IPs `192.0.2.11`–`192.0.2.16`) and the configuration-model and verification-plan proposals — then **STOPPED schedule derivation with 0 rows** (2026-10-11: zero drawing/manual files in the sandbox, `NOT_AVAILABLE_IN_SESSION`). On 2026-10-12 the Baseline REV1 evidence arrived via the Owner-connected source (integrity gate PASSED; ephemeral bytes removed after review) and was fully reviewed: new `docs/evidence/` 240-row schedule (200 verified, 12 internal-conflict, 4 topology-conflict, 24 spare, 0 unresolved; 212 MATCHED across 106/106 Sensors), process-image profile + `MODEL_LEVEL` decoding contract, and narrow validation summary. `REQ-TC-001`…`REQ-TC-007` drawing/manufacturer-verified; `REQ-TC-008`…`REQ-TC-020` stay `NOT_PROVIDED`. `SENSOR_TO_TC_CHANNEL_SCHEDULE: VERIFIED_OWNER_SOURCE` (with recorded conflicts); `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`; `WIRE_BREAK_DECODING: BLOCKED`. Physical Sensor acquisition NOT IMPLEMENTED; hardware loop test NOT EXECUTED; no live MODBUS; no TEST_HARDWARE, PRODUCTION or device write. Documentation-only — **no executable test was run**; Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review are predecessor evidence cited, not re-run. `README.md` was **not** modified (outside this Stage's scope); CF-07 stays open for an Owner decision. PR #18 OPEN against `main` — NOT MERGED (do not merge). See §3.8.

**Stage 0.4C-0A Physical Temperature Acquisition Evidence Intake and Commissioning Source Register (2026-10-10; development checkpoint, documentation and evidence-intake structure only; PR #17 since MERGED at `3e1a7fd9`):** new session after **PR #16 MERGED**, on the platform-assigned branch `arena/1cfd7e20-waterjet-sentinel-suite`, approved remote-main base `main` `bd1437586af73a33d43dfafce27b27538183a283` (the PR #16 merge commit; parents `29ee137c` and `8ba8d894`, verified; PR #16 MERGED at `2026-10-10T19:37:37Z`). Built the authoritative evidence-intake structure for the Temperature Sensor acquisition system: the physical TC evidence source register (categories A–F, nine evidence states, seven verification scopes, a binding non-propagation rule), the prioritised Owner register **`REQ-TC-001` to `REQ-TC-020` — all 20 `NOT_PROVIDED`** (10 P0 / 5 P1 / 5 P2), a deterministic **28-column / 212-row** Sensor-to-Channel schedule template with **every acquisition column `NOT_PROVIDED`**, a **26-column** commissioning-record schema proposal with **zero data rows**, the future wire-break decision tree (design only, **not implemented**; no Product enum modified), Stage 0.4C-1 entry criteria (**10 of 11 NOT MET**), a Domain A Process Instrument parallel readiness register, and a Galil evidence intake register (`GAL-01`–`GAL-13`, none `PROVIDED` or `VERIFIED`). A read-only workbook re-parse re-confirmed 114 rows, 108 `USED`, 6 `SPARE`, 104 tagged channels, 23 slots and **no thermocouple module** (CF-01). `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`; `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`; `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`; `WIRE_BREAK_DECODING: BLOCKED`. No TC module, Channel order, A/B placement or Sensor-to-Channel binding was inferred. Physical Sensor acquisition NOT IMPLEMENTED; wire-break decoding NOT IMPLEMENTED; sensor commissioning monitor NOT IMPLEMENTED; actual process-image offsets NOT VERIFIED; no MODBUS, TEST_HARDWARE, PRODUCTION or device write. Documentation-only — **no executable test was run**; Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review are predecessor evidence cited, not re-run. `README.md` was **not** modified (outside this Stage's scope); its stale status block is reported as **CF-07** for an Owner decision. NEW PR OPEN against `main` — NOT MERGED. See §3.7.

**Stage 0.4C-0 Physical Sensor Readiness, Loop-Test Requirements and Actual I/O Evidence Gate (2026-10-10; development checkpoint with Owner decisions OQ-01 to OQ-08, documentation and contract proposal only; PR #16 since MERGED at `bd143758`):** delivered on branch `arena/fecda529-waterjet-sentinel-suite`, approved remote-main base `main` `29ee137c1df617a1aa3321c3c891161d5a5bb41a` (PR #15 merge commit; parents `3a73dfce` and `8b479772`, verified; PR #15 MERGED). Checkpoint `4babdce`; Owner-decision successor commit recorded in PR #16. **TC gap (OQ-01):** the authoritative workbook has **no thermocouple input module**, while the product model requires 212 thermocouple Channels for 106 Sensors. `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`; `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`; `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. TC acquisition is BLOCKED BY MISSING TC TOPOLOGY AND SENSOR-TO-CHANNEL SCHEDULE. Process Instrument (Domain A) and Temperature Sensor (Domain B) commissioning are separate domains (OQ-02). The evidence inventory, gap register, identity model, quality and diagnostic contract (wire-break representation NOT VERIFIED), invalid-value rules, Loop-Test workflow, monitor and record-schema proposals, read-only architecture, pilot and rollout plans and Galil G1–G5 checklist are in [`docs/STAGE_0.4C-0_CHECKPOINT.md`](STAGE_0.4C-0_CHECKPOINT.md). Physical Sensor acquisition NOT IMPLEMENTED; wire-break decoding NOT IMPLEMENTED; no physical Sensor tested; no MODBUS, TEST_HARDWARE, PRODUCTION or device write. PR #16 OPEN against `main` — NOT MERGED. See §3.6.

**Stage 0.4B-3 Primary manufacturer evidence ingestion and read-only process-image preview (2026-10-10; development checkpoint; PR #15 since MERGED at `29ee137c`):** delivered on branch `arena/00483ae4-waterjet-sentinel-suite`, base `main` `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` (PR #14 merge). Power Supply, System Power Supply, End Module and the head station are corrected to **no Process I/O address** (`NOT_APPLICABLE` / `NON_PROCESS_DATA_MODULE`); RackSlot, the actual-rack configuration-tool position and ProcessModulePosition are separate fields; instance- and Channel-scoped actual-rack evidence is recorded; a read-only candidate process image is `CANDIDATE_UNVERIFIED` with **no invented offset**. Arena: Mapping 268 tests (261 pass, 7 pre-existing Owner-local skips; 268/268 with the authoritative workbook supplied), Mapping UI 82/82, boundary S1–S9 clean. Checkpoint `abe7e023eb7b5e3f976d8fbc5c9a5b17079058ed`. **Owner-local validation PASSED** (Owner-confirmed: Mapping 268/268, Mapping UI 82/82, boundary S1–S9 clean, workbook SHA-256 unchanged) and **Owner Browser Review PASSED** (Owner-confirmed from Owner-provided screenshots; the screenshots do not claim exhaustive browser interaction or responsive coverage, and are not actual process-image verification). Workbook unchanged. No MODBUS, no TEST_HARDWARE, no PRODUCTION, no write or control. PR #15 OPEN, NOT MERGED. See §3.5.

**Stage 0.4B-1 Mapping Configuration (2026-10-10; Owner validation and Final Source Review, documentation closeout):** Owner-local automated validation **PASSED** at exact code head `433189f9e489d7af6770e64c971585d99bf941ad` (Owner-reported: Mapping 217/217; Mapping UI 56/56; boundary S1–S9 clean). Owner browser review **PASSED WITH NON-BLOCKING UI PUNCHLIST**. Final Source Review **PASSED, blocking defects none**. PR #12 OPEN, NOT MERGED. Mapping Configuration only, SIMULATOR only, read-only with respect to hardware. No MODBUS, no TEST_HARDWARE, no PRODUCTION, no activation. See §3.4.

**Stage 0.4A CP-3c-2 / CP-4A final closeout (2026-10-10; supersedes the dated pending gate below):** Owner-reported automated validation **PASSED** at exact code head `fd053fde07c96163b073fc9cb4c0383f31eafe3c` (base `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b`): cleared environment; locked restore PASS, no lock drift; Release build with TreatWarningsAsErrors PASS, 0 warnings / 0 errors; full .NET **414/414** (0 failed, 0 skipped); fixture parity **7/7** (0 failed, 0 skipped); TypeScript typecheck PASS, tests **34/34** (0 failed); boundary scan 0 findings S1–S9; `git diff --check` PASS; no source drift, clean Owner-local tree, no artifact commit. Arena did **not** compile or execute .NET. Owner manual browser review **PASSED** for the Valve pressure matrices, independent Pump and paired-IVn pressure sources and thresholds, parallel Valve CLOSE / Axis RETURN, cumulative faults and qualified completion, blocked next dispatch, sequential FIFO two-Job AutoSequence (one Active Job; final Queue empty, Job 2 completed), read-only modal and truthful Pump wording. Screenshots are Owner-observed evidence, not Arena-executed evidence. Exact-source static Final Source Review **PASSED; blocking defects NONE**. [Checkpoint and numbered review](STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md). **NON-BLOCKING UI CONTEXT-LABEL PUNCHLIST:** distinguish Current Active Job from last released Job outcome and last released Valve-close resolution in future UI wording; not implemented here. CP-3c-2 Runtime Host integration and CP-4A minimal read-only observation are ready for Owner merge review; **PR #11 OPEN, NOT MERGED**. CP-4 full UI refinement is not delivered; no operator control, write route or device command; **MODBUS NOT IMPLEMENTED, TEST_HARDWARE NOT AUTHORIZED, PRODUCTION NOT AUTHORIZED**.

*The following 2026-10-09 fast-track gate statement is retained as historical development status, not current validation status.*

**Latest Stage 0.4A fast-track gate (2026-10-09):** On approved main `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b` (PR #10 merged), CP-3c-2 Runtime composition and CP-4A **minimal read-only** Inspector are OWNER-AUTHORIZED and source-delivered for Owner-local validation. CP-4 full UI, operator control, MODBUS, TEST_HARDWARE and PRODUCTION are **NOT AUTHORIZED**. This supersedes the earlier CP-3c-2/CP-4 blanket NOT AUTHORIZED wording below; past validation results remain historical. See [checkpoint](STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md). The new PR remains open, not merged; .NET validation and Owner acceptance pending.


**Latest Stage 0.4A CP-3c-1 status (2026-10-09):** Owner-local validation PASSED at `29f133ce6598bc2eb489273a2f2d40f8827deb5f`; static Final Source Review PASSED with no blocking defect. PR #10 OPEN, NOT MERGED; CP-3c-2 and CP-4 NOT AUTHORIZED. Earlier CP-3c-1 validation-pending and blanket CP-3c-not-authorized entries are dated history, superseded only for the authorized CP-3c-1 library foundation. See the CP-3c-1 checkpoint report.

Status vocabulary: `[APPROVED]`, `[OWNER CONFIRMED]`, `[PROPOSED]`, `[OPEN]`,
`[NOT AUTHORIZED]`, `[NOT VERIFIED]`, plus implementation status: `SUBMITTED FOR OWNER
REVIEW`, `CHANGES REQUESTED`, `IN PROGRESS`, `OWNER ACCEPTED`, `NOT MERGED`, `MERGED`.

| Stage | Title | Gate | Implementation | Deliverable |
| --- | --- | --- | --- | --- |
| 0.1 | Repository Documentation Foundation | `[APPROVED]` | **MERGED** — merged to `main` through PR #1 (merge commit `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`) | Repository governance and documentation foundation |
| 0.2 | Technology and Solution Architecture Decision | `[APPROVED]` | **OWNER ACCEPTED / MERGED** — through PR #2 (source checkpoint `5bcf1b33f924ab30590a55736676200115874fa1`, merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`); ADR-0006 to ADR-0013 **ACCEPTED** as direction, not implemented | Documentation-only technology and solution architecture decisions: UI delivery model, runtime process model, technology stack, database access and migrations, device adapter boundary, configuration and secrets, simulator-first development, offline deployment, and a documented repository structure direction — recorded as ADR candidates |
| **0.2.1** | **UI and Runtime Technology Spike** | **`[PROPOSED]`** — **`[NOT AUTHORIZED]`** | Not started | Measured comparison of Candidate A (React + TypeScript + Vite) and Candidate B (Blazor Hybrid) inside the application-owned kiosk shell, using synthetic data only, plus validation of the live-state delivery, live-trend, and offline-restore architecture. Deliverable is a comparison report, a recommendation, and a decision-record draft — **no production code** |
| **0.2.1A** | **React UI and Runtime Feasibility Spike** | **`[APPROVED]`** — Scope Gate and Coding Start approved | **COMPLETE FOR DEVELOPMENT CHECKPOINT** — Owner-local final Edge gate **PASS** and Owner manual review **PASS** at `114c0761` (2026-10-07); React selected as the Primary UI Framework; controlled 15- / 60-minute observations waived as merge blockers; PR #3 **OPEN — READY FOR OWNER MERGE**, **NOT MERGED** | Synthetic React feasibility spike in `spikes/ui-runtime-react/` (removable, no Product directory) plus [`docs/spikes/stage-0.2.1a-plan.md`](spikes/stage-0.2.1a-plan.md) and [`docs/spikes/stage-0.2.1a-results.md`](spikes/stage-0.2.1a-results.md). **React selected as the Primary UI Framework** (Owner decision, 2026-10-07); Blazor counter-spike **not required** unless a future material blocker is identified |
| 0.3 | Architecture and Interface Specification | `[PROPOSED]` — **0.3A executing under the Owner's Option-C amended gate only** | **Reconciled 2026-10-08:** 0.3A-1 Owner-local validation **PASSED**; 0.3A-2 validated; 0.3A-3 **COMPLETE — MERGED (PR #6, `909d028`)**. Historical text below (superseded): 0.3A-1 source checkpoint **authored, Owner-local .NET validation PENDING** ([plan](STAGE_0.3A_PLAN.md), [runbook](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md)); 0.3B+ not started | Component boundaries, interface contracts, error taxonomy, and a testable specification for queue and cleaning logic. The amended gate authorises product *foundation sources* (contracts/domain/simulator/runtime stub as source); documentation and specification discipline still governs everything else |
| 0.4 | Offline Domain and Queue Engine Implementation | `[PROPOSED]` — **Stage 0.4A (Simulator Sequencing Foundation) Owner-authorised for CP-0 and CP-1 only, 2026-10-08; CP-3a and CP-3b completion Owner-authorised 2026-10-08** (see §3.3 and §3.3.4) *Superseded 2026-10-09 (see §3.3.5): CP-2 Owner-authorised (§3.3.2) and OWNER-LOCALLY VALIDATED (§3.3.3); CP-3a and CP-3b Owner-authorised (§3.3.4) and OWNER-LOCALLY VALIDATED at `a315e90` (§3.3.5, Owner-reported); CP-3c and CP-4 `[NOT AUTHORIZED]`.* | CP-0 status reconciliation (documentation); **CP-0 and CP-1 OWNER-LOCALLY VALIDATED** (validated head `03144f4`, 2026-10-08; F1 blocking state-integrity defect CLOSED; CP-1 Final Source Review PASSED); CP-2 scope gate PROPOSED (§3.3.1; prerequisite F1 corrected and validated); CP-2 coding, CP-3 and CP-4 `[NOT AUTHORIZED]` | First source code: pure domain logic with a simulator and automated tests; no device access |
| 0.5 | Read-Only Monitoring Integration | `[PROPOSED]` | Not started | Modbus TCP read path against a simulator only; no production writes |
| 0.6 | Supervisory Control Path with Simulator | `[PROPOSED]` | Not started | Valve, pump, and motion command paths exercised against a simulator only |
| 0.7 | Historian, Alarm, Event, and Audit Subsystems | `[PROPOSED]` | Not started | Storage, retention, and cleanup mechanics verified against a test database |
| 0.8 | Operator Interface and Kiosk Operation | `[PROPOSED]` | Not started | Control Room Kiosk experience, responsiveness, close guard, and operator workflows |
| 0.9 | Bench Verification | `[NOT AUTHORIZED]` | Not started | Hardware benchmark evidence for the items listed in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md). Requires separate, explicit Owner authorisation and physical supervision. |
| 1.0 | Deployment Acceptance | `[NOT AUTHORIZED]` | Not started | Owner acceptance of a release candidate on a Boiler Unit |

Stage 0.2.1 is inserted by the Owner-requested Stage 0.2 documentation review punchlist as the
only authorised route to a UI framework decision. It remains `[PROPOSED]` and
`[NOT AUTHORIZED]`; it is **not** started by Stage 0.2, and no prototype or spike code may be
written until it has its own approved Scope Gate.

**Stage 0.2.1A update.** The Owner approved a narrower Stage 0.2.1A Scope and Coding Start
Gate: a synthetic React feasibility spike only. It produces feasibility evidence, not a
framework selection; the two-candidate comparison of Stage 0.2.1 is not performed, the Blazor
counter-spike is deferred and `[NOT AUTHORIZED]`, and the UI framework remains `[OPEN]` until
the Owner records a selection. **Owner selection recorded (2026-10-07):** React is the Primary UI
Framework; the Blazor counter-spike is not required unless a future material blocker is
identified; Stage 0.2.1A is complete for the Development checkpoint; the Main Development Scope
Gate is **PENDING**.

**Stage 0.2.1A domain correction.** During Stage 0.2.1A the Owner corrected the physical
baseline from 104 Sensor locations / 208 Thermocouple channels (Rear 28, Front 28) to **106
Sensor locations / 212 Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). The
Sensors sit in an 18 × 6 logical matrix with two Cannon equipment slots (logical I7 and I16), as
set out in [`DOMAIN_MODEL.md` §2.2.1](DOMAIN_MODEL.md#221-logical-sensor-matrix-and-the-two-non_sensor_gap-positions-owner-confirmed).
The 104 / 208 figures in historical records are the superseded baseline.

*Superseded-wording note (added at Stage 0.4B-3):* the *two Cannon equipment slots* label above is
the Stage 0.2.1A-era wording and is preserved here only as dated history. **Current wording:** the
two positions are **`NON_SENSOR_GAP` placement anchors** — logical **I7** (Rear wall) anchors
**WJ3** and logical **I16** (Front wall) anchors **WJ1** — they are never Sensors and never Water
Jets, and **Water Jets WJ1–WJ8 are separate equipment entities** paired 1:1 with **IV1–IV8** by
ordinal, with *Installed Position* distinct from *Target Coverage* (ADR-0017). The counts 106
Sensors / 212 Thermocouple channels / 108 logical positions are unchanged.

*Owner critical topology correction (2026-10-12, Stage 0.4C-K3 branch, PR #21):* both paragraphs
above are now dated history. **Current wording:** every one of the 108 logical positions is a
Product Sensor — **I7 and I16 are normal Product Sensors** in their ordinal position (Left 24,
Rear 30, Right 24, Front 30), giving **108 Sensors / 216 assigned Product TC measurements / 240
physical TC channels / 24 unassigned**. No `NON_SENSOR_GAP` position exists; the Sensor sequence
has no holes; WJ1–WJ8 remain separate equipment entities paired with IV1–IV8 (the replacement
placement description of WJ1/WJ3, formerly "at the I16 / I7 gap anchor", is an open Owner
decision). Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. Record:
[`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md).

*Owner-approved canonical Equipment topology adoption (2026-10-25):* supersedes the 2026-10-21
Canonical Topology Adoption paragraph below for current installed-position / Target-Coverage /
WSB-axis-allocation / default-assignment / identity authority (that paragraph is retained as
history). **Current wording:** the canonical table is WJ1 (legacy WJ2) IV1 installed LEFT/UPPER
→ RIGHT/UPPER on WSB1 A/B; WJ2 (legacy WJ3) IV2 REAR/UPPER → FRONT/UPPER on WSB1 C/D; WJ3
(legacy WJ4) IV3 RIGHT/UPPER → LEFT/UPPER on WSB2 C/D; WJ4 (legacy WJ1) IV4 FRONT/UPPER →
REAR/UPPER on WSB2 A/B; WJ5 (legacy WJ5) IV5 FRONT/LOWER → REAR/LOWER on WSB3 A/B; WJ6 (legacy
WJ7) IV6 REAR/LOWER → FRONT/LOWER on WSB3 C/D; WJ7 (legacy WJ6) IV7 LEFT/LOWER → RIGHT/LOWER on
WSB4 A/B; WJ8 (legacy WJ8) IV8 RIGHT/LOWER → LEFT/LOWER on WSB4 C/D; A/C Horizontal, B/D
Vertical; canonical default assignment by physical Sensor wall / tier is Right/Upper 8 →
WJ1/IV1, Front/Upper 10 → WJ2/IV2, Left/Upper 8 → WJ3/IV3, Rear/Upper 10 → WJ4/IV4,
Rear/Lower 20 → WJ5/IV5, Front/Lower 20 → WJ6/IV6, Right/Lower 16 → WJ7/IV7, Left/Lower 16 →
WJ8/IV8 (108 total; I7 → WJ5/IV5, I16 → WJ6/IV6). The Legacy WJ identity column is historical
equipment identity only; `cannon n → WJn` direct import is unchanged. Motion identity is
`OWNER_CANONICAL_TOPOLOGY_2026_10_25` / `MOTION_COMMISSIONING_DESIGN_2026_10_25`; REV2 and REV1
remain historical identities only. The H1 synthetic default route is G+210 → WJ1/IV1, WSB1 A/B.
The 108 / 216 / 240 / 24 baseline, the Sensor sequence, physical
Sensor-to-SDS/Cabinet/module/channel mapping and every safety / sequencing rule are unchanged;
no Hardware matrix is repeated and the .NET validation of the adoption is the pending
Owner-local gate. Record:
[`decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md`](decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md)
addendum and [`CURRENT_STATE.md`](CURRENT_STATE.md).

*Canonical Topology Adoption (2026-10-21; superseded for current authority by the 2026-10-25
adoption above; retained as history — HISTORICAL / SUPERSEDED: DO NOT USE FOR CURRENT RUNTIME,
PROFILE, COMMISSIONING, OR HARDWARE):* the "replacement placement description … open Owner
decision" wording above is now closed history. **Wording at that 2026-10-21 checkpoint
(historical):** the canonical installed
topology is WJ1 FRONT/UPPER, WJ2 LEFT/UPPER, WJ3 REAR/UPPER, WJ4 RIGHT/UPPER, WJ5 FRONT/LOWER,
WJ6 LEFT/LOWER, WJ7 REAR/LOWER, WJ8 RIGHT/LOWER, each with opposite-wall same-tier Target
Coverage (WJ1 → REAR/UPPER … WJ8 → LEFT/LOWER) and WJn ↔ IVn pairing; canonical default
assignment by physical Sensor wall / tier is Rear/Upper 10 → WJ1/IV1, Right/Upper 8 → WJ2/IV2,
Front/Upper 10 → WJ3/IV3, Left/Upper 8 → WJ4/IV4, Rear/Lower 20 → WJ5/IV5, Right/Lower 16 →
WJ6/IV6, Front/Lower 20 → WJ7/IV7, Left/Lower 16 → WJ8/IV8 (108 total; I7 → WJ5/IV5,
I16 → WJ7/IV7). The fine-grained placement of **all eight** Water Jets is `UNRESOLVED` with no
placement anchors (no anchor inferred, mirrored or transferred; only the fine-grained
placement is unresolved — the canonical wall / tier is confirmed). Motion identity is
`OWNER_CONFIRMED_REV2` / `MOTION_COMMISSIONING_DESIGN_REV2` (WJ1 = A,B on WSB2; WJ2 = A,B on
WSB1; WJ3 = C,D on WSB1; WJ4 = C,D on WSB2; WJ5 = A,B on WSB3; WJ6 = A,B on WSB4; WJ7 = C,D on
WSB3; WJ8 = C,D on WSB4); REV1 remains historical identity only. The 108 / 216 / 240 / 24
Sensor and TC baseline, the Sensor sequence, physical Sensor-to-SDS/Cabinet/module/channel
mapping and every safety / sequencing rule are unchanged. No Cleaning Path model, Sensor
override, C/D Manual Motion, output 3/4 write, Hardware use or Production integration is
authorized by the Adoption; the .NET validation of the Adoption is the pending Owner-local
gate. Record: [`decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md`](decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md)
addendum and [`CURRENT_STATE.md`](CURRENT_STATE.md).

Stages 0.3 through 0.8 are a **candidate** decomposition. The Owner may merge, split,
reorder, or replace them. They are recorded here so that work is not invented ad hoc, not
because they are approved. Stage 0.3 and later remain `[PROPOSED]` and are `[NOT AUTHORIZED]`
until a later Owner Scope Gate approves them. **Update (2026-10-07):** the Owner's Option-C
amended gate authorises **Stage 0.3A** work in gated substages (0.3A-1 source checkpoint now
authored; Owner-local .NET validation is its mandatory pre-merge gate — see
[`STAGE_0.3A_PLAN.md`](STAGE_0.3A_PLAN.md)); it authorises **no** other stage, and 0.3A-2+
stay `[NOT AUTHORIZED]` until each is explicitly approved.

##### 3.3 Stage 0.4A — Simulator Sequencing Foundation (Owner authorisation 2026-10-08)

- **Approved base:** `main` = `909d02846febfe2383f4d712f04455447f62bab0` (PR #6 merge).
- **Authorised:** CP-0 (status and ADR reconciliation, documentation only; commit subject
  `docs(state): reconcile Stage 0.4A sequencing scope`) and CP-1 (pure kernel under
  `packages/application/Runtime/Sequencing/` with Runtime.Core tests under
  `tests/runtime.tests/Sequencing/`; commit subject `feat(runtime): add pure global queue sequencing gate`).
- **Not authorised:** CP-2 (pure Job, Safe Return, valve and axis feedback, critical latch), CP-3
  (SIMULATOR composition), CP-4 (read-only Inspector), any command surface, Job phase execution,
  Pump critical action, Valve or Axis action, Safe Return execution, `TEST_HARDWARE`, and
  `PRODUCTION` device access. No `packages/contracts`, fixture, TypeScript mirror, RuntimeStage
  marker, or ProfileStartPolicy change is authorised.
- **Owner rulings in force for CP-1:** scenario-prepared explicit admission only; GlobalQueue
  capacity 8, FIFO, dispatch-ready entries only, head-only dispatch, no scan-forward, exactly one
  Active Job; no BLOCKED, HELD, WAITING_FOR_PUMP, WAITING_FOR_EQUIPMENT or EXCLUDED Queue-entry state;
  pump readiness is not a Queue refusal (WAITING_FOR_PUMP belongs to the Active Job/AutoSequence
  after an atomic dispatch); head revalidation failure removes the head with REMOVED_BY_ELIGIBILITY
  and dispatches nothing in that transition; pause is Job/AutoSequence state (PAUSE_REQUESTED →
  PAUSED after the Job's Mandatory Safe Return; direct PAUSED with no Job).
- **Open, not decided here:** D6 valve timing on critical events, D7 outcome vocabulary, D8 Safe
  Return failure recovery, D9 critical reset and resume, D10 axis confirmation source, D11 POST
  surface, D12 queue source model. Production pump-readiness semantics remain **OWNER DECISION
  REQUIRED** (matrix row A).
- **Stage state (updated 2026-10-08, final evidence amendment):** CP-0 and CP-1 are **OWNER-LOCALLY VALIDATED** at `03144f4` (Owner-reported: 253 of 253 .NET tests; Release build 0 warnings and 0 errors). F1 blocking state-integrity defect **CLOSED** (`23b276f`, `03144f4`); CP-1 Final Source Review **PASSED**. The earlier "FOLLOW-UP RECOMMENDED BEFORE MERGE, not blocking" classification of F1 is withdrawn. The earlier validated head `2cfe648` (217 tests) is historical. CP-2 scope gate **PROPOSED** in §3.3.1; **CP-2 coding NOT AUTHORIZED**. CP-3 and CP-4 NOT AUTHORIZED. The earlier "Sources are authored in Arena / NOT COMPILED IN ARENA / OWNER-LOCAL VALIDATION REQUIRED" wording is superseded. PR #7 OPEN, NOT MERGED.

##### 3.3.1 Stage 0.4A CP-2 scope gate — PROPOSAL (2026-10-08, not authorized)

**Status: PROPOSAL ONLY. CP-2 coding is NOT AUTHORIZED.** This section does not authorize any source, test, contract, schema, fixture or runtime change. It is ready for Owner ruling. Items marked **[BLOCKING]** in §19 must be ruled before CP-2 coding may start. Vocabulary is taken from the accepted contracts (`packages/contracts/Enums.cs`, `Jobs.cs`, `SafeReturn.cs`, `Sequence.cs`, `Pump.cs`) and from the accepted [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §§1–7. Spike behaviour is an oracle only; it is cited, not adopted. **Prerequisite update (2026-10-08, final evidence amendment):** the F1 correction is delivered in `23b276f` and Owner-locally validated at `03144f4`. The F1 prerequisite is satisfied. This update does not approve CP-2 and does not change the proposal's content.

**1. Name and objective.** *Stage 0.4A CP-2 — Cleaning Job, Mandatory Safe Return and Critical Pump Kernel.* Extend the pure `SequencingKernel` so that exactly one Active Job executes an abstract, SIMULATOR-oriented phase path. Every end of a Job (normal completion, abort, execution failure, Pump-critical stop) passes through Mandatory Safe Return in the Owner order. A Pump STOP or TRIP disables synthetic water output in the same transition. The kernel stays pure and deterministic, with no I/O, no host, no API and no Inspector.

**2. Existing CP-1 types reused.** `SequencingState`, `SequencingEntry`, `SequencingActiveJob` (extended), `SequencingEvent` (extended), `SequencingTransition`, `SequencingEvidence`, `SequencingTopology` (WJn↔IVn pairing), `SequencingCodes`, `SequencingKernel.Apply` and the projections. `SafeReturnReleaseEvidence` is proposed for retirement as an external input (§19, O-10). Reused accepted contract enums, with no contract change: `JobPhase` (P1–P6), `JobLifecycle`, `SafeReturnStep` (SR1–SR8, SR_FAILED), `CriticalPumpKind`, `AutoSequenceState`, `AutoSequenceMode`. `CriticalPumpEvent`, `SafeReturnState` and `JobOutcomeRecord` are projection targets only, in CP-3/CP-4.

**3. New types (proposed, internal unless stated; each new file needs Owner naming, §19 O-18).**
- `ValveFeedbackState` = `CLOSED` | `OPEN` | `TRANSIT_OR_FAULT` | `INVALID_LIMIT_STATE`, derived by a pure function from the upper and lower limit observations (§5 below).
- `AxisFeedbackState` = `AT_STANDBY` | `NOT_AT_STANDBY` | `UNKNOWN` | `FAULT` (abstract only).
- `CleaningJobOutcome` = `COMPLETED` | `FAILED` | `ABORTED` | `RECOVERY_REQUIRED` (internal enum; the wire `Outcome` field stays an opaque string per the contract comment, §19 O-13).
- `SequencingIntent` (data only): `VALVE_CLOSE(valveId)`, `AXIS_TO_STANDBY(jetId)`, `WATER_OUTPUT_OFF`. No adapter binding, no transport (§19 O-8).
- `FeedbackTarget` = `VALVE_CLOSED` | `AXIS_STANDBY` (names the timeout input that applies).
- A Safe Return ledger recording the SR1–SR8 steps and their evidence sequence numbers.

**4. Exact Job lifecycle and phases.**
- `JobLifecycle` (accepted contract names, reused): `RUNNING`, `ABORTING`, `SAFE_RETURN_CLOSE_VALVE`, `SAFE_RETURN_VERIFY_VALVE_CLOSED`, `SAFE_RETURN_TO_STANDBY`, `SAFE_RETURN_VERIFY_STANDBY`, `SAFE_RETURN_FAILED`.
- Phases within `RUNNING`: `P1` → `P2` → … → `P6`, strictly in order (`ExecutionPhaseVerified` events). Gates per [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §3: entry to P1 requires valve `CLOSED` observed (step 5); the transition from P1 to P2 requires valve `OPEN` observed (steps 9–10); P6 completion starts Safe Return. Motion values are never present; each phase is a verified synthetic event.
- The §4.1 pre-execution states (`PENDING`, `REVALIDATING`, `RESERVED`) are not separate CP-2 states. CP-1 atomic dispatch already performs revalidation and reservation (§19 O-9).
- The CP-1 pump-waiting gate (`PUMP_NOT_READY`) remains a Job-level flag after dispatch. A mid-run pump-not-ready is an open question (§19 O-11).

**5. Exact Safe Return states (CP-3c-2 correction).** SR1 disables synthetic water; SR2 commands the paired Isolation Valve CLOSE; SR4 commands Axis return immediately afterward, without waiting for Valve feedback. `ValveCloseCommandSeq < AxisReturnCommandSeq`. SR3 is the independently observed Valve-close **resolution**, not an Axis gate: a verified Lower limit gives `LOWER_LIMIT_CONFIRMED`, while pressure-only inference gives `CLOSED_BY_PRESSURE` and no Lower confirmation sequence. SR5 independently records Axis `AT_STANDBY` after SR4. SR3 and SR5 may arrive in either order. SR6 records the pending outcome and SR7 releases the Active Job immediately on the second valid result, only for `LOWER_LIMIT_CONFIRMED` or `CLOSED_BY_PRESSURE`. Leak, not-fully-closed and invalid pressure retain the Job. SR8 is not a kernel action; a later dispatch is a separate transition and is blocked by latched equipment faults. Failure: `SR_FAILED` retains the Job. Measured Pump outlet readiness is strictly above the configurable Pump setpoint (default 15 bar), independently of the paired IVn Low/High thresholds (defaults 1/15 bar).

**6. Exact valve feedback states** (derived from the accepted matrix, [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §5.2; columns as in that table):

| Upper limit | Lower limit | Derived `ValveFeedbackState` | Safe Return meaning |
| --- | --- | --- | --- |
| 0 | 1 | `CLOSED` | Lower limit contributes to SR3 only with valid IVn outlet pressure below Low; SR4 is already commanded |
| 1 | 0 | `OPEN` | Does not confirm close; pressure classification remains independent of Axis feedback |
| 0 | 0 | `TRANSIT_OR_FAULT` | Legitimate during bounded travel; waits until a timeout input, then failure |
| 1 | 1 | `INVALID_LIMIT_STATE` | Always a fault: immediate `SAFE_RETURN_FAILED` |

Feedback carries the `ValveId`. A mismatched identity is refused (§19 O-6). Valve timeouts are input events only (§9).

**7. Exact axis feedback states** (abstract only). `AT_STANDBY` satisfies SR5. `NOT_AT_STANDBY` and `UNKNOWN` do not satisfy SR5 and wait until a `FeedbackTimeoutExpired(AXIS_STANDBY)` input, which fails the step. `FAULT` fails the step immediately. No coordinate, speed, homing or positioning value exists anywhere in CP-2. The confirmation source is open (decision D10, §19 O-7). Axis return is commanded after Valve CLOSE and does not wait for SR3.

**8. Exact Pump-critical input states.**
- Accepted critical inputs: `CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP` (class B) and `CriticalPumpKind.MAIN_PUMP_TRIP` (class C). Each raises the critical condition.
- Class A (expected commanded stop) is not accepted in CP-2 and is refused with `PUMP_EXPECTED_STOP_NOT_MODELLED` (§19 O-3).
- `PumpRunState` (`STOPPED`, `STARTING`, `RUNNING`, `STOPPING`, `TRIPPED`) is **not** a kernel input. The accepted derivation has no direct motor-running feedback ([`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §7). CP-2 accepts only the critical event.
- Effect in one transition, with no delay and no timer: `CriticalSuspended = true`; AutoSequence projects `CRITICAL_SUSPENDED`; if a Job is active, `WATER_OUTPUT_OFF` and SR1 are recorded, the lifecycle moves to `SAFE_RETURN_CLOSE_VALVE`, and the SR2 intent is emitted in that same transition. The queue and queue revision are unchanged. No dispatch. The spike's `1.5 s` value is a minimum-step constant and is not used. The Owner rule that no delay precedes the valve-close request is honoured.

**9. Pure event model** (every event carries an explicit `At`; no clock, no timer, no transport):
- `PumpCriticalObserved(At, CriticalPumpKind)`.
- `ExecutionPhaseVerified(At, JobPhase)`, in strict order.
- `ExecutionFailureObserved(At, reasonCode)`.
- `AbortRequested(At)`: a synthetic internal trigger only, with no route (§19 O-12).
- `ValveLimitObserved(At, ValveId, upperLimit, lowerLimit)`.
- `AxisFeedbackObserved(At, AxisFeedbackState)`.
- `FeedbackTimeoutExpired(At, FeedbackTarget)`: the timeout is an input, not a kernel timer (§19 O-5).
- Retained from CP-1: `StartAutoSequence`, `AdmitQueueEntry`, `DispatchHead`, `ObservePumpReadiness`, `RequestPause`. `RaiseCriticalSuspension` is retained for tests only, and the Pump source uses `PumpCriticalObserved`. `ReleaseActiveJob` is removed as an external input, because release becomes SR7 (§19 O-10).

**10. Transition table** (CP-2 additions; CP-1 rules otherwise unchanged):

| From | Event | Guard | To | Same-transition effects | Evidence |
| --- | --- | --- | --- | --- | --- |
| `RUNNING`, P*k* | `ExecutionPhaseVerified(P*k+1*)` | In order; P1 entry needs valve `CLOSED`; P1→P2 needs valve `OPEN` | `RUNNING`, P*k+1* | none | `PHASE_VERIFIED` |
| `RUNNING`, P6 | `ExecutionPhaseVerified(P6)` | Final phase | `SAFE_RETURN_CLOSE_VALVE`, pending `COMPLETED` | SR1 (water off); SR2 intent | `SR1`, `SR2` |
| `RUNNING` (any phase) | `ExecutionFailureObserved` | none | `SAFE_RETURN_CLOSE_VALVE`, pending `FAILED` | SR1; SR2 intent | `SR1`, `SR2` |
| `RUNNING` (any phase) | `AbortRequested` | none | `ABORTING` → `SAFE_RETURN_CLOSE_VALVE`, pending `ABORTED` | SR1; SR2 intent (same transition) | `SR1`, `SR2` |
| `RUNNING` (any phase) | `PumpCriticalObserved` (B or C) | none | `SAFE_RETURN_CLOSE_VALVE`, pending per O-1; `CriticalSuspended = true` | SR1; SR2 intent; no dispatch | `CRITICAL_SUSPENSION_RAISED`, `SR1`, `SR2` |
| `SAFE_RETURN_VERIFY_STANDBY` | `ValveSupervisionObserved` for the paired IVn | valid quality/source and a completed close assessment | release if SR5 exists and close is safe; otherwise retain | SR3 resolution; SR4 was already commanded | `SR3` (possibly `SR6`, `SR7`) |
| `SAFE_RETURN_VERIFY_STANDBY` | Unsafe Valve resolution | leak, not-fully-closed or invalid pressure | retained with fault | SR3; no SR6/SR7 | `SR3` |
| `SAFE_RETURN_VERIFY_STANDBY` | `FeedbackTimeoutExpired(VALVE_CLOSED)` | no Valve result | `SAFE_RETURN_FAILED` | Job retained; no release; no dispatch | `SR_FAILED` |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(AT_STANDBY)` | after SR4 | release only if a safe SR3 resolution exists; otherwise retain | SR5 then optional SR6/SR7 | `SR5` (possibly `SR6`, `JOB_RELEASED`) |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(NOT_AT_STANDBY` or `UNKNOWN)` | none | unchanged | observation recorded | `STANDBY_NOT_CONFIRMED` |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(FAULT)` or `FeedbackTimeoutExpired(AXIS_STANDBY)` | none | `SAFE_RETURN_FAILED` | Job retained; no release; no dispatch | `SR_FAILED` |
| `SAFE_RETURN_FAILED` | any Job-advancing event | none | unchanged | refused; no retry, reset or resume | `SAFE_RETURN_FAILED_RETAINED` |
| any state with an Active Job | `DispatchHead` | none | unchanged | refused (CP-1 `DISPATCH_REFUSED_JOB_ACTIVE`) | CP-1 code |
| any state with an Active Job | `RequestPause` | none | `PAUSE_REQUESTED` (CP-1) | the Job continues through Safe Return | CP-1 code |
| Job released (SR8) | `DispatchHead` | CP-1 rules | next Job | separate transition only | CP-1 codes |

**11. Outcome timing.** The outcome is recorded at SR6 only after both SR3 safe Valve resolution and SR5 Standby, in the same transition as the second independent result. Release (SR7) is an ordered record in the same transition as SR6. The next dispatch is always a separate transition. Outcome mapping (proposal):

| Trigger | Safe Return verified | Outcome |
| --- | --- | --- |
| Normal completion (P6) | yes | `COMPLETED` |
| Explicit abort | yes | `ABORTED` |
| Execution failure | yes | `FAILED` |
| Pump-critical stop (B or C) | yes | **Open, §19 O-1.** The spike uses `ABORTED`. The Owner rulings define the four terminal outcomes but do not map a Pump-critical stop to one. |
| Any trigger | cannot be verified (`SAFE_RETURN_FAILED`) | `RECOVERY_REQUIRED`, **timing open (§19 O-2)** |

**12. Failure and retention.**
- On `SAFE_RETURN_FAILED`: the Active Job is retained (`ActiveJob` non-null) with lifecycle `SAFE_RETURN_FAILED`; the critical condition is retained at high severity; the queue and queue revision are unchanged; no release; no dispatch; water output remains off.
- No automatic retry: SR2 and SR4 are never re-issued by the kernel after a failure.
- No automatic reset: no event clears `CriticalSuspended` (D9 remains open).
- No automatic resume: no transition leaves `SAFE_RETURN_FAILED`; Start is refused.
- Feedback that arrives after `SAFE_RETURN_FAILED` is recorded as evidence only and advances nothing.
- A second critical event, or a critical event during Safe Return, is refused and recorded (`CRITICAL_EVENT_DURING_SAFE_RETURN` is the spike name; §19 O-4). An additional abort during Safe Return is noted and not restarted.

**13. Deterministic evidence ledger changes.**
- One evidence sequence (`EvidenceSeq`) across queue, Job and Safe Return. This retires the two-sequence design (§19 O-10).
- Evidence records gain optional fields: lifecycle, phase, SR step, valve feedback, axis feedback, internal outcome, and intent. Field order is fixed by the record declaration.
- Ordering invariant (checked by test; mirrors the spike ordering invariant): water-output-off seq ≤ valve-close intent seq < valve-CLOSED confirmed seq < axis-return intent seq < AT_STANDBY confirmed seq < outcome seq < release seq.
- New codes are listed in the test plan (§15).
- Identical input produces byte-identical state and evidence serialization (the CP-1 Q13 pattern is extended).
- Wire projection of Job state (`ActiveCleaningJobState`, `SafeReturnState`, `JobOutcomeRecord`) is deferred to CP-3 and CP-4.

**14. Expected files to change** (only if CP-2 is authorized):
- `packages/application/Runtime/Sequencing/SequencingState.cs`: extended Job fields; internal constructor (§19 O-17).
- `.../SequencingEvent.cs`: new events; `ReleaseActiveJob` removed as an external input (O-10).
- `.../SequencingKernel.cs`: Job execution and Safe Return transitions.
- `.../SequencingCodes.cs`: new codes.
- `.../SequencingTransition.cs`: evidence field additions.
- `.../SafeReturnReleaseEvidence.cs`: retired or made internal (O-10).
- `.../SequencingTopology.cs`: no change expected; WJn↔IVn reused.
- New source files, **not yet named**: `SequencingFeedback.cs` (valve and axis feedback derivation) and `SequencingSafeReturn.cs` (Safe Return ledger). Alternative: keep them in existing files, with no new file (§19 O-18).
- `tests/runtime.tests/Sequencing/SequencingKernelTests.cs`: only the CP-1 tests whose behaviour changes intentionally (the release path and the critical-raise path).
- New test file, **not yet named**: `tests/runtime.tests/Sequencing/SequencingJobSafeReturnTests.cs`.
- Documentation (CURRENT_STATE, CHANGELOG, MASTER_PLAN): after approval only.

**15. Tests proposed** (Runtime.Core only; no source-text, import, formatting, private-method or analyzer tests):
- T-01 a second Job cannot start while one is active.
- T-02 normal completion enters Safe Return.
- T-03 abort enters Safe Return.
- T-04 execution failure enters Safe Return.
- T-05 Pump STOP (class B) disables synthetic water output in the trigger transition.
- T-06 Pump TRIP (class C) disables it in the same way.
- T-07 Pump critical sets `CRITICAL_SUSPENDED`.
- T-08 the paired valve-close intent precedes any axis-return intent.
- T-09 Axis-return intent follows CLOSE command without waiting for a Valve confirmation; SR3 and SR5 may occur in either order.
- T-10 Safe Return evidence sequence numbers strictly increase.
- T-11 no outcome is recorded before SR5 on the success path.
- T-12 the Job is not released before Safe Return succeeds.
- T-13 the next dispatch is impossible before release.
- T-14 successful normal Safe Return produces `COMPLETED`.
- T-15 successful abort Safe Return produces `ABORTED`.
- T-16 successful execution-failure Safe Return produces `FAILED`.
- T-17 failed valve close produces `SAFE_RETURN_FAILED` (outcome per O-2).
- T-18 failed Standby confirmation produces `SAFE_RETURN_FAILED` (outcome per O-2).
- T-19 failure retains the Active Job.
- T-20 failure preserves the queue and queue revision.
- T-21 no automatic retry.
- T-22 no automatic reset.
- T-23 no automatic resume.
- T-24 paired device references are WJn↔IVn; mismatched pairs are refused.
- T-25 identical input produces byte-identical state and evidence.
- T-26 `INVALID_LIMIT_STATE` produces an immediate failure.
- T-27 `TRANSIT_OR_FAULT` waits, and a timeout input fails the step.
- T-28 a class A stop is refused with `PUMP_EXPECTED_STOP_NOT_MODELLED`.
- T-29 a second critical event during Safe Return is refused and recorded.

Boundary items (no POST, write, API, UI or control path added; boundaries clean) are verified by review and by `tools/boundary-scan`, not by source-text tests, consistent with the CP-1 rule.

**16. Protected files and behaviour.** Protected files: `packages/contracts/**`, fixtures, the TypeScript mirror, the RuntimeStage marker and `StageMarkerTests`, `ProfileStartPolicy`, `FixtureParityTests`, `RuntimeStartGateSourceTests`, `apps/**`, `adapters/**`, `config/**`, `tools/**`, `spikes/**`, and project and lock files (a lock change would mean a project-graph change and must stop the checkpoint). Protected behaviour: one Active Job; ready-only FIFO with head-only dispatch and no scan-forward; pump waiting outside Queue entries; critical freeze of the queue and revision; WJn↔IVn pairing; I7 and I16 are neither Sensors nor Water Jets; no Production device access.

**17. Explicit exclusions.** No Runtime host integration. No API, POST route, command route or SSE. No Inspector or UI. No physical device access, and no TEST_HARDWARE or PRODUCTION use. No commands or actuation: intents are data only and are never bound to an adapter (§19 O-8). No timers, clocks or numeric timeout, speed, homing or coordinate values. No persistence or restart recovery during Safe Return (§19 O-16). No operator reset, resume or abort route. No outcome enum in the contracts, and no Production approval of outcome names. Spike constants (20 s timeout, 3 s feedback, 1.5 s minimum step) are not adopted.

**18. Owner-local validation plan** (for CP-2, if authorized):
1. `git ls-remote origin refs/heads/main` equals the approved base; the PR head is the CP-2 head.
2. `dotnet restore WaterJetSentinelSuite.sln` with locked restore; no lock drift.
3. `dotnet build WaterJetSentinelSuite.sln -c Release --no-restore`: 0 warnings, 0 errors.
4. `dotnet test WaterJetSentinelSuite.sln -c Release --no-build`: 0 failed, 0 skipped; the total is the 217 baseline plus the new CP-2 tests, reported exactly.
5. Fixture parity 7/7; TypeScript typecheck and tests unchanged in result.
6. `node tools/boundary-scan/boundary-scan.mjs .`: 0 findings.
7. `git diff --check`; working tree clean; no contract, fixture or project file changed.
8. Review checklist: no route, POST or SSE added; no `apps/**` or `adapters/**` change; intents have no adapter reference.

**19. Open decisions requiring Owner ruling.** Items marked **[BLOCKING]** must be ruled before CP-2 coding.
- **O-1 [BLOCKING]** Outcome of a Pump-critical stop when Safe Return is verified: `ABORTED` (spike), `FAILED`, or another approved name. The Owner rulings define the four terminal outcomes but do not map a Pump-critical stop to one.
- **O-2 [BLOCKING]** Outcome on Safe Return failure. (a) Record `RECOVERY_REQUIRED` at the moment of failure, with the Job retained and not released (proposal). (b) Record nothing until an approved Owner route exists. Also define the "approved Safe Return point": the proposal is SR5 (`AT_STANDBY` confirmed).
- **O-3 [BLOCKING]** Expected commanded stop (class A) with an Active Job: refuse (proposal), model, or define an outcome.
- **O-4** A second critical event, or a critical event during Safe Return: refuse and record (proposal), or the spike's `CRITICAL_EVENT_DURING_SAFE_RETURN` with unchanged steps.
- **O-5** Feedback timeouts: proposal is input events only, with no numeric value in CP-2. The value and the timer owner are for an Owner-approved later checkpoint.
- **O-6** Contradictory or wrong-identity valve feedback. Proposal: OPEN after a close command waits until the timeout input. INVALID_LIMIT_STATE fails immediately. A feedback `ValveId` mismatch is refused.
- **O-7** Axis `AT_STANDBY` source (decision D10). Proposal: synthetic input only in CP-2.
- **O-8 [BLOCKING]** Intents as pure data (`VALVE_CLOSE`, `AXIS_TO_STANDBY`, `WATER_OUTPUT_OFF`) returned from the kernel, with no adapter binding. Proposal: permitted. This is a command-adjacent surface and needs explicit approval.
- **O-9 [BLOCKING]** Normal phase path P1→P6 with valve gates in CP-2, or Safe Return path only. Proposal: the phase ordering and valve gates are included, with no motion values.
- **O-10 [BLOCKING]** Retire the external `SafeReturnReleaseEvidence` and use one evidence sequence, with release as an internal SR7. Proposal: yes.
- **O-11** Pump not ready mid-run, after P1 has begun: hold (proposal), critical, or failure.
- **O-12** The abort trigger exists as a synthetic internal input only, with no route. Proposal: yes.
- **O-13** Outcome names. Proposal: the kernel uses `COMPLETED` / `FAILED` / `ABORTED` / `RECOVERY_REQUIRED` internally. The contract stays opaque strings. The CP-2 ruling is not Production approval (matrix E).
- **O-14** Lifecycle names. Proposal: the accepted contract `JobLifecycle` names are used (they match the spike string values). Confirm.
- **O-15** Critical latch permanence. Proposal: no reset in CP-2; `CRITICAL_SUSPENDED` persists until a future decision (D9 open). Confirm.
- **O-16** Persistence and restart during Safe Return: in-memory only (matrix D). Confirm.
- **O-17 [BLOCKING] — PREREQUISITE SATISFIED (2026-10-08).** The CP-1 F1 fix: an internal `SequencingState` constructor, and `Apply` validates its input state and refuses with `SEQUENCING_STATE_INVALID` (the refusal code as implemented). Prerequisite, old: F1 correction pending. Prerequisite, new: F1 corrected and Owner-locally validated. The F1 correction landed in `23b276f` before CP-2 and is Owner-locally validated at `03144f4`, so the placement choice is moot. The §20 acknowledgement remains an Owner ruling and does not authorize CP-2.
- **O-18** Naming and approval of the new source and test files in §14, or an instruction to keep them in existing files.

**20. Can CP-2 begin after explicit Owner approval?** Yes, but not yet. CP-2 may start only after an explicit Owner message that: (a) approves this scope gate, or a revised version of it; (b) rules on every **[BLOCKING]** item (O-1, O-2, O-3, O-8, O-9, O-10, O-17); (c) names or refuses the new files in O-18; and (d) reaffirms that CP-3 and CP-4 remain NOT AUTHORIZED. CP-2 would then be a separate checkpoint on `arena/bd0a7c64-waterjet-sentinel-suite`, under PR #7, which remains open until the Owner merges it. Until then, no CP-2 code, test, contract, UI or command path may be created.

##### 3.3.2 Stage 0.4A CP-2 coding authorization and applied Owner rulings (2026-10-08)

**Status: CP-2 OWNER-AUTHORIZED for source and tests in `packages/application/Runtime/Sequencing/**` and `tests/runtime.tests/Sequencing/**` only.** This section supersedes the "coding NOT AUTHORIZED" wording of §3.3.1 for CP-2 only. CP-3 and CP-4 remain NOT AUTHORIZED. No `packages/contracts`, fixture, API, Inspector, Runtime host, device or command surface is changed. PR #7 is not continued. The CP-2 PR is opened from the session branch `arena/bba7709c-waterjet-sentinel-suite` and is not merged.

**Owner rulings applied (the §19 items this section resolves):**

| Item | Applied ruling |
| --- | --- |
| O-1 | A Pump UNEXPECTED_STOP or TRIP during Cleaning ends in a verified Safe Return with outcome `ABORTED`. |
| O-2 | Safe Return failure records `RECOVERY_REQUIRED` evidence, lifecycle `SAFE_RETURN_FAILED`, Job retained, no release. The approved Safe Return point is SR5 (`AT_STANDBY` confirmed). |
| O-3 | An EXPECTED_STOP is non-critical and sets no latch. While the Job is in CLEANING it is refused as `PUMP_EXPECTED_STOP_NOT_MODELLED`. Implementation decision, flagged for Owner confirmation. |
| O-4 | A critical event during Safe Return sets the latch, is recorded as `CRITICAL_EVENT_DURING_SAFE_RETURN`, and does not restart or alter the Safe Return steps. Implementation decision, flagged for Owner confirmation. |
| O-5 | Timeouts are input events only (`FeedbackTimeoutExpired`). No numeric value and no timer. |
| O-6 | OPEN or TRANSIT_OR_FAULT after a close request waits for a timeout input. INVALID_LIMIT_STATE fails immediately. A ValveId mismatch is refused. |
| O-7 | Axis feedback is a synthetic input only. |
| O-8 | Intents (`WATER_OUTPUT_OFF`, `VALVE_CLOSE`, `AXIS_TO_STANDBY`) are recorded as data in evidence only. They have no adapter binding and no transport. Implementation decision, flagged for Owner confirmation. |
| O-9 | The P1 to P6 phase ordering and the valve gates are included. No motion values exist. |
| O-10 | `SafeReturnReleaseEvidence` and the external `ReleaseActiveJob` input are removed. Release is SR6 and SR7 inside the kernel's own axis-confirmation transition. |
| O-11 | Open. CP-2 refuses a mid-cleaning pump not-ready observation rather than holding it. Recorded as an open limitation. |
| O-12 | `RequestAbort` is a synthetic internal input only, with no route. |
| O-13 | Outcome names are the kernel's `COMPLETED`, `FAILED`, `ABORTED`, `RECOVERY_REQUIRED`. The contract stays opaque strings. |
| O-14 | Accepted `JobLifecycle` contract names are reused. Stages PREPARING, READY_TO_CLEAN and CLEANING are kernel-internal sub-stages of `RUNNING`. |
| O-15 | No reset in CP-2. `CRITICAL_SUSPENDED` persists. |
| O-16 | In-memory only. |
| O-17 | Satisfied (CP-1 F1). |

**Contract blocker check:** none. Every lifecycle, step, pump kind, outcome and AutoSequence name required by CP-2 is either an accepted contract name (reused) or a kernel-internal name with no wire meaning. `packages/contracts` is not modified.

##### 3.3.3 Stage 0.4A CP-2 Owner-local validation and Final Source Review (2026-10-08)

**Status: CP-2 OWNER-LOCALLY VALIDATED at `1f94991b8fddd5b1e7e158c8f02c34a70ac0c14c`; CP-2 Final Source Review PASSED (read-only, no blocking defect).** This section records the closeout only. It authorises no new coding. It does not supersede §3.3.2, which remains the coding authorisation for CP-2.

- Owner-local validation (Owner-reported, not reproduced in Arena): full .NET 300/300; Release build 0 warnings / 0 errors; fixture parity 7/7; TypeScript 31/31; boundary S1–S9 clean; locked restore with no drift; working tree CLEAN; no artifact commit required.
- Final Source Review: F2 (one evidence sequence) PASSED. F3 (release under the critical latch) PASSED. EXPECTED_STOP during CLEANING PASSED as a refusal. O-3, O-4, O-8 and O-11 remain open for Owner confirmation, as §3.3.2 records them.
- Non-blocking follow-ups (see `docs/STAGE_0.4A_CP-2_CHECKPOINT.md` §19): validator gap for a critical latch with a RUNNING Job; `Apply` wording for an exhausted or negative evidence counter; acceptance of the CP-1 test rewrites required by O-10; valve movement during CLEANING is recorded but not gated.
- Authority: PR #8 OPEN, NOT MERGED. **CP-3 NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** TEST_HARDWARE and PRODUCTION device access NOT AUTHORIZED.
- Superseding note (2026-10-09, documentation only): the CP-3 authority wording in this section is superseded for CP-3a and CP-3b by §3.3.4 and §3.3.5. CP-3c and CP-4 remain NOT AUTHORIZED. PR #8 is MERGED to `main` at `d884a541163c2d567ab4ba2882a981431a14bfe7`.

##### 3.3.4 Stage 0.4A CP-3a and CP-3b coding scope record (2026-10-08)

**Status: Owner-authorized for CP-3a and CP-3b (completion correction, 2026-10-08).** CP-3a and CP-3b source and tests are scope-locked to the files below, on the session branch `arena/9aa1746c-waterjet-sentinel-suite`, from approved base `d884a541163c2d567ab4ba2882a981431a14bfe7` (`origin/main`; PR #8 merged; CP-2 §19 present). The implementation is NOT VALIDATED until the Owner-local gates pass: a locked restore, a Release build with 0 warnings and 0 errors under `TreatWarningsAsErrors`, the full .NET suite, fixture parity, TypeScript, and the boundary scan. The agent never merges. *Superseded 2026-10-09 by §3.3.5: the Owner-local gates were reported PASS on `a315e90` (Owner-reported).* **CP-3c and CP-4 are NOT AUTHORIZED.** No Inspector UI, `apps/runtime` host, route, contract, fixture, TypeScript, device, PLC, Modbus, Galil, KMotion, DCS, command, POST/PUT/PATCH/DELETE, SSE, database or system-clock change is authorized.

**1. CP-3a files (kernel hardening).** `packages/application/Runtime/Sequencing/SequencingKernel.cs`, `SequencingStateValidator.cs`; tests in `tests/runtime.tests/Sequencing/SequencingCp3aHardeningTests.cs`. `SequencingSafeReturn.cs` is not changed unless a STOP is recorded. `SequencingTransition.cs`, `SequencingCodes.cs` and `SequencingState.cs` are not changed.

**2. CP-2 follow-up dispositions (CP-2 §19.8).**

| Item | Disposition in CP-3a |
| --- | --- |
| FU-1 latch with a RUNNING Job | Implemented: `JOB_RUNNING_UNDER_LATCH`, checked last in `RunningJobViolation`, so every earlier RUNNING identity is unchanged. |
| FU-2 `Apply` wording | Implemented as XML documentation only. No behaviour change. |
| FU-3 CP-1 test rewrites | Documentation only. No CP-1 test is restored or rewritten. Owner acceptance remains `[OPEN]`. |
| FU-4 valve movement during CLEANING | Implemented for the case the brief names: an OPEN reading during CLEANING followed by CLOSED or TRANSIT_OR_FAULT enters Safe Return in the same transition (SR1, SR2, pending FAILED, reason `VALVE_LOST_DURING_CLEANING`). Queue and latch unchanged. INVALID_LIMIT_STATE keeps its CP-2 path. CP-2 §19.8 asked for Owner confirmation of this trigger; that confirmation is `[OPEN]`. Source enumeration found no existing test that pins the recorded-only behaviour, so no existing test is changed. Valve movement before any OPEN reading in CLEANING stays recorded-only; this residual is `[OPEN]`. |
| FU-5 O-3, O-4, O-8, O-11 | Unchanged. Still `[OPEN]` for Owner confirmation. |

**3. AxisStandbySeq decision.** The SR5 evidence sequence is written into `AxisStandbySeq` inside the AT_STANDBY confirmation path. The released Job does not survive, so the value is observable only through the SR5 evidence record of the release transition. The public preview `SequencingKernel.PreviewAxisStandbyLedger` is removed by the completion correction. The release transition's own SR5 record carries the same sequence, so the Owner-facing outcome record can be sourced from evidence without a new transition receipt. `SequencingTransition` is not changed.

**4. CP-3b scope (simulator composition, library only; completion correction; historical baseline superseded by CP-3c-2 pressure projection).** Allowed: `packages/application/Runtime/Simulator/` (scenario catalogue bound to the canonical Runtime Sensor set, scenario-to-kernel schedules, five-scope bounded retention, and the pure wire projection of the GlobalQueue, Sensor queue state, Active Job, Safe Return, Sequence, critical event and last Job outcome) and `RuntimeDeltaProjector.cs` (content comparison, and a pure candidate path that carries one appended trend point and refuses other trend changes explicitly). Tests in `tests/runtime.tests/Simulator/`. Scenario selection is a pure name match with no clock, no randomness and no operator surface. The historical CP-3b projection carried the Pump section unchanged; CP-3c-2 projects a separate measured Pump outlet (never the Valve outlet). Sequence controls remain disabled. Presentation rules applied for Owner review (these are rulings requested, not authority questions): `PhaseProgress` is the contract's 0..1 unit, so verified phase Pn is presented as n/6 (the brief's integer table does not apply); before the first verified phase the Job is presented as `P1 PENDING - PREPARING` or `P1 PENDING - READY_TO_CLEAN`, and CLEANING before any verified phase as `P1 PENDING - CLEANING` (a flagged convention); `CleaningPhase` is `IN_PROGRESS` (the contract defines only running and frozen-at-trigger); a valve Feedback before any observation is `UNKNOWN` (no valve feedback identity exists for it); Safe Return `Command` fields carry the SR2 and SR4 step codes only. The Host composition that supplies the canonical Sensor set is CP-3c and is NOT AUTHORIZED.

**5. Equipment intents.** Intents remain pure evidence data. CP-3 adds no command-like vocabulary. The contract's existing `Command` fields are not renamed. O-8 stays `[OPEN]`.

**6. Protected behaviour.** Snapshot schema `wjss.snapshot/2`, Delta schema `wjss.delta/2`, the single-writer revision model, the gapless Delta chain, Queue capacity 8 with FIFO head-only dispatch, the three-state `activeJob` encoding, WJn paired with IVn, the `RuntimeStage.Marker` value, and the CP-2 release and latch rules are all unchanged.

##### 3.3.5 Stage 0.4A CP-3a and CP-3b Owner-local validation and Final Source Review (2026-10-09, documentation only)

**Status: CP-3a and CP-3b OWNER-LOCALLY VALIDATED at `a315e90be28fb5e9662c6a7179587c992273cb88` (Owner-reported; not reproduced in Arena). CP-3a and CP-3b Final Source Review PASSED (read-only at `a315e90`; no blocking defect). VALVE FEEDBACK RETENTION DEFECT: CLOSED.** This section records validation and review only, and it authorises no new coding. **CP-3c NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** This section does not authorise Host integration, an API field or route, an Inspector change, a command or write, SSE, a database, environment-variable scenario selection, runtime execution of scenarios, TEST_HARDWARE or PRODUCTION device access. PR #9 is OPEN and NOT MERGED. The agent does not merge.

- **Lineage.** Base `d884a541163c2d567ab4ba2882a981431a14bfe7` (`main`; PR #8 merged). Round 1 `7766fed2ba5f81865494d5cc7a7c9a52e59abde6` (`fix(runtime): use concrete private collection returns`, CA1859). Round 2 `f9ddac29f0e357188e4c1fdc2561f1d6f606d53f` (`fix(tests): resolve CP-3 analyzer findings`). Round 3 `a315e90be28fb5e9662c6a7179587c992273cb88` (`fix(runtime): retain only observed valve feedback`). The documentation successor, subject `docs(state): record CP-3a and CP-3b owner validation`, follows `a315e90`. Its SHA is in the branch history and in the PR #9 body.
- **Owner-local evidence (Owner-reported; authoritative for local runs; not reproduced in Arena).** Head `a315e90`; .NET SDK 10.0.401; xUnit runtime 10.0.12. Release build PASS, 0 warnings, 0 errors. Direct Valve-retention regression 1/1. Retention and Projection 131/131. Focused Runtime.Core 430/430. Full .NET 552/552. Fixture parity 7/7. TypeScript typecheck PASS; TypeScript tests 31/31. Boundary scan 0 findings; S1–S9 clean. `git diff --check` PASS. Working tree CLEAN; drift NONE; no artifact commit required.
- **Valve feedback retention defect: CLOSED.**
  - *Root cause (verified in source).* `SequencingRetention.Retain` set `CurrentValveFeedback` for every APPLIED record with a non-null valve value. The preparation advance (`ADVANCE_JOB_PREPARATION`, `PREPARATION_ADVANCED`) carries `ValveFeedback = CLOSED` as prerequisite context, and the Safe Return SR3 record of the same observation repeats the observed value. Either record replaced the observation identity, so the projected `Valve.FeedbackSeq` pointed at the context record (Owner-reported sequence 6) instead of the observation (sequence 5).
  - *Correction.* The valve condition is `Outcome == APPLIED && EventKind == VALVE_LIMIT_OBSERVED && ValveFeedback != null && Step == null`. The `Step is null` conjunct is a deviation from the Owner's stated condition, which was insufficient on its own: the SR3 record (kernel line 744) has the same EventKind, outcome and valve value. Axis retention is unchanged. Its observation identities are the SR5 (AT_STANDBY) and SR_FAILED (FAULT) records, which carry the axis value as the observation itself.
  - *Files.* `packages/application/Runtime/Simulator/SequencingRetention.cs` (+7 −1); `tests/runtime.tests/Simulator/SequencingRetentionTests.cs` (+25). `SequencingRuntimeProjectionTests.cs` is unchanged: `Assert.Equal(closedSeq, sr.Valve.FeedbackSeq)` at line 310 and `Assert.Equal(valveObserved.Seq, later.Valve.FeedbackSeq)` at line 326 are retained.
  - *Validation.* The Owner-local run on `a315e90`, with the `Step is null` conjunct in place, passed the direct regression, the retention and projection suites and the full suite. The deviation is therefore validated by that run. No separate written acknowledgement is recorded here.
- **Final Source Review (read-only, `a315e90`; the source was read, not the delivery reports).** Every finding is classified PASS, NON-BLOCKING FOLLOW-UP or BLOCKING DEFECT. **Blocking defects: none.**

| # | Question | Result | Basis (source and test) |
| --- | --- | --- | --- |
| 1 | FU-1 correct without rejecting legitimate Safe Return under the latch? | PASS | `RunningJobViolation` checks the latch last and only for RUNNING Jobs. Start, Admit, Dispatch and ApplyPause refuse under the latch; valve, axis and timeout events have no latch guard. Tests: `Latch_With_A_Safe_Return_Job_Is_Accepted_And_Projects_Critical`, `Latch_With_A_Failed_Safe_Return_Job_Is_Accepted_And_Projects_Critical`. |
| 2 | FU-4 triggers only after a confirmed OPEN during CLEANING? | PASS | Condition `CleaningActive && LastValveFeedback == OPEN && observed != OPEN`. BeginCleaning requires CLOSED, so an OPEN during CLEANING was observed after start. Residual: NB-2. Test: `Repeated_Open_Reading_During_Cleaning_Is_Not_A_Valve_Loss`. |
| 3 | FU-4 stops water, enters Safe Return with pending FAILED, keeps Queue and latch? | PASS | SR1 (water-off intent) and SR2 in the same transition; `PendingOutcome` FAILED; reason `VALVE_LOST_DURING_CLEANING`; Queue and `CriticalSuspended` carried; the triggering reading does not confirm the close. Tests: `Valve_Movement_Away_From_Open_During_Cleaning_Enters_Safe_Return_In_The_Same_Transition`, `Valve_Loss_Confirms_The_Close_Only_On_A_Later_Closed_Reading_And_Releases_As_Failed`. |
| 4 | INVALID_LIMIT_STATE behaviour retained? | PASS | Handled before FU-4 while RUNNING, and in the close wait, as in CP-2. Test: `Invalid_Limit_State_During_Cleaning_Enters_Failure_Safe_Return_In_The_Same_Transition`. |
| 5 | Canonical scenario identities come only from the supplied Sensor set? | PASS | `Create` copies and orders the supplied records; topology and every scenario Sensor derive from them. The only literal is the reason code `SYN-FAULT-1`. Kernel-level test fixtures (`SYN-S01` and similar) are test inputs, not scenario identities. |
| 6 | WJn paired only with IVn? | PASS | `IsPairedOrdinal` is used by admission, dispatch-head revalidation and the validator. Tests: `T30_Invalid_WJ_IV_Pairing_Is_Refused_At_Admission`, `Invalid_Water_Jet_And_Valve_Pairing_Is_Rejected`. |
| 7 | Catalogue deterministic; no clock or randomness? | PASS | Fixed `Epoch` plus whole-second ticks. No clock, random, environment, file, network or device token in the in-scope source or tests. Tests: `Identical_Scenario_Input_Produces_Byte_Identical_Evidence`, `Every_Scenario_Schedule_Has_Strictly_Increasing_Ticks`. |
| 8 | Retention: log 256; tail 16; reset at dispatch; archive at release; first critical wins? | PASS | Constants 256 and 16. Dispatch resets the tail and feedback. SR7 archives `LastJobOutcome` before resetting. `FirstCritical` is set once, on the latch transition. Note: NB-5. |
| 9 | `CurrentValveFeedback` retains only the observation identity? | PASS | Condition above. `FeedbackSeq` and `Feedback` agree because every `LastValveFeedback` change is paired with an APPLIED observation record or an equal NO_OP. Unchanged readings do not advance `FeedbackSeq`, and the value is unchanged. |
| 10 | `CurrentAxisFeedback` correct and unaffected? | PASS | Axis condition unchanged. The only APPLIED axis records are SR5 (AT_STANDBY) and SR_FAILED (FAULT). |
| 11 | Projection: QueueSummary, canonical Sensor queue states, one Active target, Active Job, Safe Return, Sequence critical, LastJobOutcome? | PASS | All present. One ACTIVE target is pinned by `Active_Job_Projects_Exactly_One_Runtime_Sensor_As_Its_Target`. `RuntimeStateInvariants` checks `IsActiveJobTarget` against ACTIVE for every Sensor. |
| 12 | Pump and Sequence controls carried unchanged? | PASS | `previous with { ... }` carries Pump and the other sections; `ProjectSequence` uses `previous.Sequence.Controls`. Test: `Pump_And_Sequence_Controls_Are_Carried_From_The_Previous_Revision`. Alarms and Communication are carried unchanged (NB-7). |
| 13 | Delta avoids spurious sections from reference-only differences? | PASS | Canonical JSON content equality for Pump, Queue, Sequence, Alarms, Communication, Active Job, each Sensor and trend points. `WallSummary` has scalar members only. The existing `Project` path also moved to content equality: intended (§3.3.4 item 4), and the Delta schema is unchanged. Test: `Rebuilt_But_Identical_Records_Produce_No_Spurious_Delta_Section`. |
| 14 | Candidate path: queue-only Sensor change, Active Job Present, Absent and Cleared, one trend append, invalid history, revision gap, Sensor reorder or set change? | PASS (NB-3, NB-4) | Each case is implemented and tested. Refusals are pinned by `Trend_Removal_Is_Refused`, `Two_Appended_Trend_Points_In_One_Revision_Are_Refused`, `Changed_Trend_History_At_The_Same_Count_Is_Refused`, `Revision_Gap_Is_Refused_And_Nothing_Is_Projected`, `Sensor_Order_Change_Is_Refused` and `Sensor_Identity_Set_Change_Is_Refused`. |
| 15 | Replay reproduces each candidate section by section? | PASS | `Every_Applied_Step_Replays_Through_ProjectCandidate_And_Apply` applies each candidate's Delta to the replayed consumer and compares every Delta-carried section, for all eleven scenarios. |
| 16 | Public read-only collection boundaries safe enough? | PASS (NB-6) | Interface types over fresh per-call collections; no alias into retained or kernel state. |
| 17 | Any mutable alias a blocking integrity defect? | NO (NB-6) | No kernel or retention path mutates an input collection. Retention copies before appending. Projection and outcome collections are fresh per call. |
| 18 | Any Host, API, Inspector, contract, fixture, TypeScript, route, adapter or device behaviour changed? | PASS | Name-only diff `d884a54..a315e90`: 15 files, 12 under `packages/application/Runtime/` and `tests/runtime.tests/`, and 3 docs. Nothing under `apps/`, `adapters/`, `packages/contracts/`, fixtures, TypeScript, `config/`, `tools/`, `spikes/`, project or lock files, or the Inspector. No caller outside the Simulator library references the new types. |

- **Presentation conventions (classification).**

| Convention | Classification | Note |
| --- | --- | --- |
| `PhaseProgress` = index / 6.0 | ACCEPTABLE FOR CP-3b | Contract 0..1 unit; presentation only. |
| `CleaningPhase` = `IN_PROGRESS` | ACCEPTABLE FOR CP-3b | Contract comment: IN_PROGRESS while running, frozen at the trigger. |
| Valve feedback before any observation = `UNKNOWN` | NON-BLOCKING FOLLOW-UP (NB-1) | Not a member of the accepted `ValveFeedbackLabel` union (`NOT_CONFIRMED`, `CLOSED_CONFIRMED`, `ABSENT`). |
| `P1 PENDING - CLEANING` | ACCEPTABLE FOR CP-3b | Free-text label; flagged in the record for Owner confirmation. |
| `PhaseAtTrigger` defaults to P1 | ACCEPTABLE FOR CP-3b | The contract requires a non-null `JobPhase`; the default applies only before any verified phase. |
| `PhaseStartedAt` = latest PHASE_VERIFIED, else Job start | ACCEPTABLE FOR CP-3b | Verified by retention and by `Current_Phase_Start_Follows_The_Dispatch_Then_Each_Verification`. |
| Candidates only for APPLIED transitions | ACCEPTABLE FOR CP-3b | NO_OP and REFUSED transitions leave the visible state unchanged; pinned by `Candidate_Count_Equals_The_Applied_Transition_Count_So_Refusals_Project_Nothing`. |
| `AxisStandbySeq` via SR5 evidence | ACCEPTABLE FOR CP-3b | Written in the release transition; reported as `StandbyConfirmedSeq` in `LastJobOutcome`. |
| Safe Return `command` = SR2 and SR4 step codes | NON-BLOCKING FOLLOW-UP (NB-1) | Accepted labels are `CLOSE_COMMANDED` and `RETURN_COMMANDED`. Pinned by `Assert.Equal("SR2", sr.Valve.Command)`. |
| Safe Return `feedback` and `standby` = kernel names | NON-BLOCKING FOLLOW-UP (NB-1) | Accepted labels are `CLOSED_CONFIRMED`, `NOT_CONFIRMED`, `STANDBY_CONFIRMED` and `ABSENT`. Pinned by `Assert.Equal("CLOSED", sr.Valve.Feedback)`. |

- **Findings.** Blocking defects: **none.** Non-blocking follow-ups (none changes the CP-3a or CP-3b validation):
  - **NB-1 Safe Return label vocabulary (Owner contract decision; gates any wire emission).** The projection writes `SR2` and `SR4` into `command`; kernel names (`OPEN`, `CLOSED`, `TRANSIT_OR_FAULT`, `INVALID_LIMIT_STATE`, and `UNKNOWN` before any observation) into `feedback`; and `AT_STANDBY`, `NOT_AT_STANDBY`, `UNKNOWN` and `FAULT` into `standby`. The accepted TypeScript unions (`ValveCommandLabel`, `ValveFeedbackLabel`, `AxisCommandLabel` and `StandbyLabel` in `packages/contracts/wjss-contracts-ts/types.ts`) and the golden fixture (`CLOSE_COMMANDED`, `RETURN_COMMANDED`, `CLOSED_CONFIRMED`, `NOT_CONFIRMED`, generated by `tests/integration/FixtureGenerator.cs`) use label names. The C# contract types these fields as `string`, so the mismatch compiles, and nothing emits the Safe Return legs in CP-3. The projection tests pin the kernel names. **Options proposed, none applied:** (A) amend the accepted label vocabulary to the kernel and step names, a contract change that requires separate Owner authorization and regeneration of the fixture and TypeScript; or (B) define a deterministic presentation mapping from kernel values to the accepted labels in a later source correction, with test updates. The Owner decides. Until this is resolved, there is no Host, API, serialization, Inspector or TypeScript consumption of the Safe Return legs.
  - **NB-2 FU-4 trigger and residual (Owner-open).** The Owner's confirmation of the FU-4 trigger (CP-2 §19.8) remains `[OPEN]`. Valve movement before any OPEN reading in CLEANING stays recorded-only, as `Valve_Movement_Before_Any_Open_Reading_In_Cleaning_Stays_Recorded_Only` pins.
  - **NB-3 Source comment (later source correction).** The `ProjectCandidate` summary says no trend point is appended. The method carries one appended point, as `Single_Appended_Trend_Point_Is_Carried_And_Replayed` pins. The summary should be corrected.
  - **NB-4 Non-Delta fields (later source change, before any external candidate producer).** `ProjectCandidate` does not compare or refuse changes to DeviceProfile, Config, WallMap, WaterJets or IsolationValves, which the Delta envelope cannot carry. This is not reachable today, because `ProjectRuntimeState` carries them from the previous revision. It should be refused explicitly before any caller other than that projection exists.
  - **NB-5 Dispatch origin (coupling).** `DispatchOf` writes `Origin = SCENARIO_PREPARED` as a constant. This is correct only because the kernel admits no other source (`AdmissionSourceNotScenario`). The retention summary says the origin is read from the queue head, which is inaccurate. Revisit if an admission source is added.
  - **NB-6 Collection hardening (later source change).** `SequencingTopology` stores caller-owned `IReadOnlyList` values without copying. `SafeReturnState.Events` and `JobOutcomeRecord.Events` are arrays behind `IReadOnlyList`. All are fresh per call, and none aliases retained or kernel state. Defensive copies or immutable collections are recommended. No integrity defect was found.
  - **NB-7 Alarm section scope (CP-3c or CP-4).** `Alarms` and `Communication` are carried unchanged in CP-3b. The critical condition is projected only in `Sequence.Critical`, with the `SYN-PUMP-CRITICAL-` identity. Alarm projection is outside the CP-3b scope and is required before any Alarm consumer.
  - **NB-8 Owner-open carry-forward (unchanged by this section).** O-3, O-4, O-8 and O-11 (§3.3.2); FU-3 (acceptance of the CP-1 test rewrites); FU-5; and the presentation conventions flagged above for Owner confirmation (`P1 PENDING - CLEANING`).
- **Governance review.** No reviewed wording authorises Host integration, API integration, an Inspector change, environment-variable scenario selection, runtime execution of scenarios, TEST_HARDWARE or PRODUCTION use. Blocking governance defects: none. Stale wording is classified below. Corrections are made by appended notes; dated entries are retained.

| Location | Stale or incorrect wording | Classification |
| --- | --- | --- |
| MASTER_PLAN §3.3 "Not authorised" bullet (2026-10-08) | CP-2 and CP-3 not authorised | Historical and superseded (§3.3.2, §3.3.4, this section). |
| MASTER_PLAN §3.3.3 Authority bullet | CP-3 NOT AUTHORIZED; PR #8 OPEN | Historical and superseded for CP-3a and CP-3b; note appended in §3.3.3. |
| MASTER_PLAN §3.3.4 status | "NOT VALIDATED until the Owner-local gates pass" | Historical and superseded; note appended. |
| MASTER_PLAN §3.3.4 status | Duplicated "CP-3c and CP-4" sentence | Correction required before merge; corrected (typographic only). |
| MASTER_PLAN stage table row 0.4 | "CP-0 and CP-1 only"; "CP-3 and CP-4 NOT AUTHORIZED" | Historical and superseded; note appended. |
| CURRENT_STATE §2 rows 53, 58, 60 and 61 | Stale headlines; "NOT VALIDATED"; "PR #8 OPEN" | Historical and superseded; notes appended. |
| CURRENT_STATE §4 Current stage row | "CP-2 CODING NOT AUTHORIZED; CP-3 NOT AUTHORIZED" | Historical and superseded; note appended. |
| CURRENT_STATE §11.11 heading, "NOT VALIDATED" and planned counts | Pre-validation wording | Historical and superseded; closeout bullets appended. |
| CURRENT_STATE §11.11 UNCHANGED list | `SequencingStateValidator.cs` listed as unchanged; the FU-1 change is +8 −4 | Correction required before merge; correction bullet appended. |
| CURRENT_STATE §11.11 "Contract change: None" | Omits the NB-1 vocabulary conflict | Correction required before merge; disclosure bullet appended. |
| CHANGELOG 2026-10-08 CP-3 entry | "NOT VALIDATED"; "Owner-local validation required" | Historical and superseded; new dated entry added above it. |
| PR #9 title and body | "development checkpoint"; "[OPEN]" authority; "NOT RUN" evidence; stale counts and commit list; "not defined in the repository" for the labels (they are defined in the TypeScript package); "deferred to CP-3c" for Sensor binding (the library binds the canonical set; only the Host composition is CP-3c) | Correction required before merge; title and body updated. |

- **Changed (documentation only).** `docs/MASTER_PLAN.md`, `docs/CURRENT_STATE.md`, `CHANGELOG.md`. No product source, test, contract, fixture, TypeScript, configuration, tool, spike, project or lock file changed.
- **Not verified in Arena.** Compilation and every test result: the figures above are Owner-reported. Arena has no .NET SDK.
- **Carried decisions (unchanged by this section).** O-3, O-4, O-8, O-11, FU-3 and FU-5 remain `[OPEN]`. The FU-4 trigger confirmation remains `[OPEN]`. The NB-1 vocabulary decision belongs to the Owner.
- **Status block (PASSED form).** CP-3a and CP-3b Final Source Review: **PASSED.** Blocking defects: none. Non-blocking follow-ups: NB-1 to NB-8. Valve feedback retention defect: CLOSED. CP-3c NOT AUTHORIZED. CP-4 NOT AUTHORIZED. PR #9 OPEN, NOT MERGED.

##### 3.4 Stage 0.4B-1 — Simulation-only Mapping Configuration: Owner validation and Final Source Review (2026-10-10, documentation only)

**Status: OWNER-LOCALLY VALIDATED at `433189f9e489d7af6770e64c971585d99bf941ad` (Owner-reported). Owner browser review PASSED WITH NON-BLOCKING UI PUNCHLIST (Owner-observed). Final Source Review PASSED, blocking defects none (static). PR #12 OPEN - NOT MERGED.**

- **Lineage.** Base `a5540057ab668708a6129c3647685f82f1c5f08d` (`main`). Branch `arena/1b92c50a-waterjet-sentinel-suite`. Predecessor `8847cfc`; successor `5f9f437`; correction commits `438cfd2`, `09608bb`, `64204e9`, `433189f`. One PR, no new branch and no new PR.
- **Scope.** Mapping Configuration, SIMULATOR provider only, Draft editing, authoritative workbook default. Read-only with respect to hardware.
- **Defaults.** 26 authoritative bindings from the workbook alone, with no seed. AI-002 is PUMP_INLET_PRESSURE (diagnostic and trend only). AI-003 is PUMP_OUTLET_PRESSURE (the sole Pump-ready source). IV1–8 outlet pressure, Lower and Upper follow `#n = IVn`. No Main Valve pressure tag.
- **Reserved inventory.** 18 read-only rows, all USED / RESERVED, UNBOUND, OWNER_INPUT_PENDING, ADDRESS_UNRESOLVED. DO-031 is an OUTPUT with NOT_AUTHORIZED_IN_READ_ONLY_STAGE.
- **Presentation.** Fixed Dark theme only. Analog pressure polarity and contact show NOT APPLICABLE. Digital limits show ACTIVE_WHEN_CLOSED / NO.
- **Boundary.** Server bound to `127.0.0.1` only. GET and HEAD only. No CORS. No device-action control. Scanner rule S6 covers environment-controlled bind hosts (authorised B1 regression protection, in scope).
- **Accepted presentation exception.** `apps/runtime/Inspector/index.html`: two labels changed to "Pump Outlet pressure", presentation only, non-blocking (Owner-approved). The label was already present at `8847cfc`.
- **Predecessor .NET evidence.** Owner-reported at `8847cfc`: Release build PASS; full .NET 414/414, failed 0, skipped 0. Not re-executed at `433189f`. The Runtime graph and Runtime Inspector have not changed since `8847cfc`.
- **Non-blocking follow-ups.** (A) Pump Slot/Channel is a Draft model API capability, not a browser editor control, in Stage 0.4B-1. (B) SPARE-versus-placeholder ordering in the importer (no such row in the current workbook). (C) Draft API create-on-missing for an unknown tag name, not reachable from the browser. (D) UI readability punchlist.
- **Not authorised or not implemented.** AI-002 Runtime publication NOT IMPLEMENTED. ADDRESSES REMAIN ADDRESS_UNRESOLVED. ACTIVATION NOT READY. NO MODBUS. NO TEST_HARDWARE. NO PRODUCTION. No device write or control.

Record: [`docs/STAGE_0.4B-1_CHECKPOINT.md`](STAGE_0.4B-1_CHECKPOINT.md), section *Owner validation and Final Source Review (2026-10-10, documentation closeout)*.

##### 3.5 Stage 0.4B-3 — Primary manufacturer evidence ingestion and read-only process-image preview (2026-10-10, development checkpoint)

**Status: DEVELOPMENT CHECKPOINT `abe7e023eb7b5e3f976d8fbc5c9a5b17079058ed`. Arena validation PASSED for the JavaScript scope. Owner-local validation PASSED (Owner-confirmed). Owner Browser Review PASSED (Owner-confirmed screenshots). PR #15 OPEN at the time of this checkpoint; since MERGED (merge commit `29ee137c1df617a1aa3321c3c891161d5a5bb41a`, verified in Stage 0.4C-0).**

- **Lineage.** New session after merged PR #14. Branch `arena/00483ae4-waterjet-sentinel-suite`; base `main` `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6`, verified against `git ls-remote origin refs/heads/main` before any edit. PR #14 parents `dbbf34cfe49f4db6c524a675dadb8a24d5ef497f` and `a811f6ffb10cb66d3a7b17721aaf1ac26c44ff71` confirmed; Stage 0.4B-2 head `fe38322d55acb7c565a9984b0eceb82c74b33bb5` confirmed as an ancestor.
- **Scope.** `packages/mapping-config/**` and `apps/mapping-config/**` only, plus focused tests and Stage documentation. No contract, fixture, Runtime, Runtime Inspector, adapter, dependency, lock file or workbook change. No other Product area was needed.
- **Module-role correction.** 750-601 `POWER_SUPPLY`, 750-613 `SYSTEM_POWER_SUPPLY`, 750-600 `END_MODULE`, 750-362 `FIELDBUS_COUPLER`: process-data contribution `NONE`, Input and Output width 0, no `ProcessModulePosition`, no `ProcessImageOrder`, `AddressStatus NOT_APPLICABLE`, `AddressReason NON_PROCESS_DATA_MODULE`. They are not waiting for an address and are never `ADDRESS_UNRESOLVED`.
- **Position discipline.** RackSlot is physical, one-based and covers all 23 modules; the actual-rack configuration-tool position is instance-level evidence and is never aliased to RackSlot or to ProcessModulePosition; ProcessModulePosition counts process-data modules only. The Owner's `WagoIoCheckPosition` is spelled `ioCheckPosition` in the Product tree because boundary rule S3 forbids vendor vocabulary there.
- **Evidence separation.** Primary-manual facts are model-level and `PROVIDED_UNVERIFIED` (`OWNER_PROVIDED_OUTSIDE_REPOSITORY`, no SHA-256 invented). Actual-rack settings are instance- and Channel-scoped; the single verified record covers Pos. 10 / Channel 3 of one 750-471 only. Actual-rack evidence never feeds address derivation.
- **Candidate.** The read-only candidate process image is `CANDIDATE_UNVERIFIED`, never authoritative, never hardware-ready, grants no write authority, and carries no invented byte, word or bit offset. Addresses remain `ADDRESS_UNRESOLVED` until the actual Process Data image and the head-station I/O Config and mapping are supplied (requests A–E recorded).
- **Arena validation.** Mapping package 268 tests (261 pass, 0 fail, 7 pre-existing Owner-local skips; **268 pass / 0 fail / 0 skipped** with the authoritative workbook supplied); Mapping UI 82 tests (82 pass); boundary scan 0 findings (S1–S9); PR-range `git diff --check` clean; workbook SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` unchanged. No .NET code touched, so no .NET result is claimed and none is requested. No browser was run in Arena; the browser evidence below is Owner-observed.
- **Owner-local validation and Browser Review — PASSED (Owner-confirmed).** Owner-local run **PASSED**: Mapping **268/268** with the authoritative workbook, Mapping UI **82/82**, boundary scan S1–S9 clean, workbook SHA-256 unchanged. **Owner Browser Review PASSED** from Owner-provided screenshots: the Mapping Configuration rendered, the candidate presentation was visible and usable, `CANDIDATE_UNVERIFIED` was clearly presented, unverified offsets were not presented as verified addresses, non-process modules were shown as not applicable, no Hardware or Modbus readiness was claimed, and no blocking visual defect was reported. The screenshots do **not** claim exhaustive browser interaction or responsive coverage and are **not** actual process-image verification.
- **Not authorised or not implemented.** AI-002 Runtime publication NOT IMPLEMENTED. CANDIDATE ADDRESSES ARE NOT AUTHORITATIVE. NO MODBUS. NO TEST_HARDWARE. NO PRODUCTION. No device write or control. PR #15 OPEN - NOT MERGED.

Record: [`docs/STAGE_0.4B-3_CHECKPOINT.md`](STAGE_0.4B-3_CHECKPOINT.md).

##### 3.6 Stage 0.4C-0 — Physical Sensor readiness, Loop-Test requirements and actual I/O evidence gate (2026-10-10, development checkpoint with Owner decisions)

**Status: DEVELOPMENT CHECKPOINT with Owner decisions OQ-01 to OQ-08 recorded (documentation and contract proposal only). Stage 0.4C-1 TC acquisition BLOCKED. PR #16 was OPEN against `main` at the time of this checkpoint; since MERGED at `bd1437586af73a33d43dfafce27b27538183a283` on `2026-10-10T19:37:37Z` — see §3.7.** The checkpoint commit `4babdce` and the Owner-decision successor commit SHAs are recorded in PR #16 and in the final report, not inside their own commits.

- **Lineage.** Session branch `arena/fecda529-waterjet-sentinel-suite`; base `main` `29ee137c1df617a1aa3321c3c891161d5a5bb41a`, verified by `git ls-remote` before edits. PR #15 merge parents `3a73dfce1d2c7c1dc4d3b0274ea10f97406088f6` and `8b479772d4fbef63b8f8eb34f59a1f2eba69618e`. PR #15 branch `arena/00483ae4-waterjet-sentinel-suite` was not used, continued or pushed. Successor commit is a single documentation commit after `4babdce`; no second branch or PR.
- **Scope.** `docs/STAGE_0.4C-0_CHECKPOINT.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`, `README.md` (three current-status rows only). No Product source, executable test, workbook, dependency, lock file, fixture, Runtime or Mapping implementation, Galil or MODBUS code was changed.
- **TC gap (OQ-01).** The authoritative workbook contains 23 rack modules and no thermocouple input module. The product requires 212 thermocouple Channels for 106 Sensors (PHY-002). `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`; `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`; `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. TC channels are not mapped onto the 35 analog channels, no TC module or channel order is inferred, the workbook is not modified, and no physical acquisition is authorised. TC on a separate source is kept separate.
- **Domain separation (OQ-02).** Domain A Process Instrument (workbook DI/DO/AI/AO; identities only where explicit). Domain B Temperature Sensor (106 Sensors / 212 TC Channels; blocked). Not merged.
- **AI-003 (OQ-03).** Remains Pump Outlet Pressure (PUMP_OUTLET, 4–20 mA, 0–40 bar), the only pre-P1 Pump-ready pressure source. The workbook label "Main Valve Outlet" is source text only. `MAIN_VALVE_OUTLET_PRESSURE` remains absent. Not reopened without new physical evidence. AI-002 is Pump Inlet Pressure, diagnostic and trend only, not Pump-ready.
- **Quality, diagnostic and commissioning model (OQ-04).** `Quality` enum unchanged. Separate `DiagnosticReason` and `CommissioningState`. Explicit wire break → BAD / WIRE_BREAK / EngineeringValue null; inferred open circuit → UNCERTAIN / SUSPECTED_WIRE_BREAK; out of range → BAD / OUT_OF_RANGE; communication loss → BAD / COMMUNICATION_LOSS; stale → STALE. INVALID is presentation only. Wire-break representation NOT VERIFIED.
- **Pilots, timing and roles (OQ-05, OQ-06, OQ-08).** Pilot roles only; actual IDs `OWNER_INPUT_PENDING`. Staleness, stabilisation and batch size `CONFIGURABLE / OWNER_INPUT_PENDING`, no production default; test examples labelled `TEST_EXAMPLE_ONLY` / `NOT_PRODUCTION_APPROVED`. Procedure author = Commissioning / Instrument responsible role; safety approval = Owner-authorised site safety role; execution = authorised commissioning personnel; final technical acceptance = Owner. No open-circuit instruction without an approved site procedure and LOTO.
- **Sensor verdict (OQ-07).** PASS / PARTIAL / FAIL / NOT_TESTED / BLOCKED as defined in the checkpoint; no averaging of A/B channels.
- **Evidence.** Manuals and the Owner's rack screens are outside the repository and not hashed. The only Channel-level configuration (Pos. 10 / RackSlot 13 / Channel 3, EV-04) sits at the workbook location of AI-003 — a location coincidence that is **not** a tag verification (RISK-01).
- **Galil.** G1–G5 checklist recorded as planning only. No controller contacted; no motion parameter defined.
- **Link consistency.** The `DOMAIN_MODEL.md` §2.2.1 links in this file moved from the legacy anchor (retained in the target) to the canonical anchor. This is not a broken-link fix.
- **Not authorised or not implemented.** Physical Sensor acquisition NOT IMPLEMENTED. Wire-break decoding NOT IMPLEMENTED. Sensor commissioning monitor NOT IMPLEMENTED. Actual process-image offsets NOT VERIFIED. AI-002 Runtime publication NOT IMPLEMENTED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor tested.

Record: [`docs/STAGE_0.4C-0_CHECKPOINT.md`](STAGE_0.4C-0_CHECKPOINT.md).

##### 3.7 Stage 0.4C-0A — Physical Temperature Acquisition Evidence Intake and Commissioning Source Register (2026-10-10, development checkpoint)

**Status: DEVELOPMENT CHECKPOINT (documentation and evidence-intake structure only), SUBMITTED FOR OWNER REVIEW in a new PR against `main`. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. `WIRE_BREAK_DECODING: BLOCKED`. NEW PR OPEN — NOT MERGED.** The checkpoint commit SHA is recorded in the PR and the final report, not inside its own commit (AGENTS §4.6).

- **Lineage.** New session after **PR #16 MERGED**. Platform-assigned branch `arena/1cfd7e20-waterjet-sentinel-suite`; base `main` `bd1437586af73a33d43dfafce27b27538183a283`, verified by `git ls-remote origin refs/heads/main` before any edit. Merge parents `29ee137c1df617a1aa3321c3c891161d5a5bb41a` and `8ba8d89492bffe6849b77ab135c6e434a48b31f5` confirmed by `git cat-file -p`. The merged PR #16 branch `arena/fecda529-waterjet-sentinel-suite` was **not** used, continued or pushed. Index and worktree clean at start; no conflicting open PR. **The standing sandbox recreation protocol was not triggered** — no ref recovery, index reload or destructive Git operation was required or used.
- **Scope.** `docs/STAGE_0.4C-0A_CHECKPOINT.md` (new), `docs/TC_EVIDENCE_SOURCE_REGISTER.md` (new), `docs/templates/sensor-to-tc-channel-schedule.template.csv` (new), `docs/templates/tc-commissioning-record.template.csv` (new), `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`. **`README.md` was not modified** — see the CF-07 note below. No Product source, executable test, workbook, dependency, lock file, fixture, Runtime or Mapping implementation, Galil or MODBUS code was changed.
- **Purpose.** Build the authoritative evidence-intake structure that identifies the actual Temperature Sensor acquisition system, and answer the eleven Stage questions **from evidence rather than assumption**. Every answer is `NOT DETERMINED` with the specific request that would close it, because no Domain B evidence exists.
- **Evidence source register.** Six intake categories (A physical hardware identity, B module/device topology, C Sensor-to-Channel mapping, D thermocouple configuration, E transport and process representation, F provenance) recorded in [`docs/TC_EVIDENCE_SOURCE_REGISTER.md`](TC_EVIDENCE_SOURCE_REGISTER.md) using nine evidence states and seven verification scopes, with a binding **non-propagation rule**: a fact verified for one Channel never propagates to another Channel, module or model, and a `PARTIAL_SCREENSHOT` never establishes `COMPLETE_RACK`. Every acquisition field in categories A–E is `NOT_PROVIDED`. Provenance ledger TCEV-01 to TCEV-11 records one verified item (the workbook, hash re-verified) and ten `NOT_PROVIDED` requests. **No SHA-256 was invented** for evidence absent from the sandbox.
- **Prioritised evidence register.** `REQ-TC-001` to `REQ-TC-020`, **all 20 `NOT_PROVIDED`**: 10 **P0** blocking Stage 0.4C-1, 5 **P1** blocking wire-break implementation, 5 **P2** blocking rollout. Each carries status, blocking stage, evidence received, evidence scope, verification result, conflict and next action. `REQ-TC-013` (observed open-circuit behaviour) additionally cannot be gathered until `REQ-TC-019` (approved procedure and LOTO reference) exists.
- **Sensor-to-Channel schedule template.** 28 columns in the required order, **212 rows** in a stated deterministic order (`MatrixRow` ascending, `LogicalColumn` ascending, `FRONT` before `REAR`), excluding the two `NON_SENSOR_GAP` positions `I7` and `I16`. Pre-populated **only** with facts already authoritative in the Sensor topology (`SensorId`, `SensorWall`, `SensorLocation` slot identity, `MatrixRow`, `LogicalColumn`, `MeasurementSide`), plus `EvidenceId = REQ-TC-004`, `EvidenceScope`/`VerificationState = NOT_PROVIDED` and `CommissioningState = NOT_TESTED`. **Every acquisition column is `NOT_PROVIDED`**: no `ModuleInstanceId`, `PhysicalChannel`, terminal, TC type or range was written. No synthetic `SYN-TC-*` identity and no `AI-0nn` value was used. Topology columns were cross-checked against all 108 slots of `config/examples/sensor-map.example.json` using the `packages/contracts/WallMap.cs` rules: **108 compared, 0 mismatches**; 106 Sensors, 106+106 sides, wall counts 24/29/24/29, total 212. Because no pre-population produced a Product or generated-data change, the deliverable stayed documentation-only and the Stage's STOP condition was not reached.
- **Commissioning-record template.** 26 columns in the required order, **header only, zero data rows** — every value in an example row would have to be invented and no physical Sensor has been tested. Schema proposal only: no persistence, no export code, no importer, no validator, no UI.
- **Wire-break decision tree.** Recorded as a **design proposal, not implemented**: explicit Channel diagnostic → `BAD`/`WIRE_BREAK`; documented substitute raw code → usable only after exact module/configuration verification, never inferred across models; below-range with no diagnostic → `UNCERTAIN`/`SUSPECTED_WIRE_BREAK`; communication unavailable → `BAD`/`COMMUNICATION_LOSS`; age beyond threshold → `STALE`; outside configured range → `BAD`/`OUT_OF_RANGE`, kept distinct from wire break. `WIRE_BREAK` is only ever explicit. `INVALID` remains presentation only. **No Product enum was modified.**
- **Stage 0.4C-1 entry criteria.** Eleven criteria; **10 NOT MET**. Criterion 6 (no contradictory mapping evidence) is satisfied only vacuously, because no mapping evidence exists at all, and must be re-checked when evidence arrives. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`; `WIRE_BREAK_DECODING: BLOCKED`; `ROLLOUT: BLOCKED`.
- **Domain A parallel readiness register.** Process Instruments the workbook already represents are registered separately so the TC gap does not obscure them: `AI-001`, `AI-002`, `AI-003`, `AI-004`–`AI-011`, `AI-012`–`AI-019`, `DI-001`–`DI-020`, `DI-021`–`DI-028`, `DI-029`–`DI-036`, `AO-001`, and the 6 `SPARE` rows, each with identity basis, mapping status, engineering range status, diagnostic capability, evidence needed and `TEST_HARDWARE` dependency. **No acquisition, polling or output control is authorised.** `AI-020`–`AI-035` (16 rows, description "XXX" plus a Thai note meaning "waiting for a reply") remain **`UNRESOLVED` and are not commissioned instruments**. `AI-012`–`AI-019` are `0-20 mA`, not `4-20 mA`, so a 4 mA live zero must not be assumed for them.
- **Galil parallel intake register.** `GAL-01` to `GAL-13` classified `PROVIDED` / `NOT_PROVIDED` / `VERIFIED` / `OWNER_INPUT_PENDING` / `BLOCKING_GATE`. **None is `PROVIDED` or `VERIFIED`**; `GAL-10` (safe speed and acceleration limits) and `GAL-11` (permitted travel envelope) are `BLOCKING_GATE` because inventing production motion values is forbidden. No Galil control implemented, proposed or authorised. Galil stays separate from the TC register.
- **Workbook re-parse (read-only).** 114 rows, 108 `USED`, 6 `SPARE`, 104 tagged channels, 23 slots, 8 module models, and **no thermocouple module or TC vocabulary** — matching the recorded Stage 0.4C-0 baseline and re-confirming CF-01. A first in-session parse reported 79/29/6 and was **wrong** (a cell with no `<v>` absorbed the next cell's value); the corrected parser reproduces the baseline exactly. The baseline was right; the first parse was the defect.
- **CF-07 reported, not repaired.** `README.md` is outside this Stage's file scope, so its now-stale current-status block (still naming Stage 0.4C-0, PR #16 OPEN and base `29ee137c…`) is reported for an Owner decision rather than edited, per AGENTS.md §2.2 and §2.3. The predecessor Stage had explicit Owner authorisation for a narrow README correction; this Stage does not.
- **Validation.** Documentation-only: boundary scan **S1–S9 clean, 0 findings, exit 0** (identical to the pre-change baseline); workbook SHA-256 unchanged; changed-file allowlist, Product-source no-change proof, executable-test no-change proof, relative-link check and `git diff --check` recorded in the final report. **No `dotnet` or `npm` test was run.** Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review PASSED are **predecessor evidence cited, not re-run at this head**.
- **Not authorised or not implemented.** Physical Sensor acquisition NOT IMPLEMENTED. Wire-break decoding NOT IMPLEMENTED. Sensor commissioning monitor NOT IMPLEMENTED. Actual process-image offsets NOT VERIFIED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor tested.

Record: [`docs/STAGE_0.4C-0A_CHECKPOINT.md`](STAGE_0.4C-0A_CHECKPOINT.md) and [`docs/TC_EVIDENCE_SOURCE_REGISTER.md`](TC_EVIDENCE_SOURCE_REGISTER.md).

##### 3.8 Stage 0.4C-0B — Six-Node Thermocouple Topology Baseline Rev1 (2026-10-11 checkpoint, evidence-blocked; 2026-10-12 evidence-review completion on the same branch)

**Status: DEVELOPMENT CHECKPOINT, SUBMITTED FOR OWNER REVIEW in PR #18 against `main` — OPEN, NOT MERGED (do not merge). `SENSOR_TO_TC_CHANNEL_SCHEDULE: VERIFIED_OWNER_SOURCE` (240-row REV1 schedule with recorded conflicts). `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. `WIRE_BREAK_DECODING: BLOCKED`.** Commit SHAs are recorded in the PR and the final report, not inside their own commits (AGENTS §4.6). The 2026-10-11 absence record below is preserved as audit history; the completion follows it.

- **Lineage.** New session after **PR #17 MERGED**. Platform-assigned branch `arena/c4b63a26-waterjet-sentinel-suite`; base `main` `3e1a7fd9d28db65221c123960a0c85b62d93949a`, verified by `git ls-remote origin refs/heads/main` before any edit. Merge parents `bd1437586af73a33d43dfafce27b27538183a283` and `48d8605a4d687f509edf079f2d371319d6bffa21` confirmed by `git cat-file -p`, with a read-only ancestry fetch proving both parents resolve (no ref, index or worktree change). The merged PR #17 branch `arena/1cfd7e20-waterjet-sentinel-suite` was **not** used, continued or pushed. Index and worktree clean at start; no conflicting open PR. **The standing sandbox recreation protocol was not triggered** — no ref recovery, index reload or destructive Git operation was required or used.
- **Scope.** `docs/STAGE_0.4C-0B_CHECKPOINT.md` (new), `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md` (new), `docs/TC_EVIDENCE_SOURCE_REGISTER.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`. **`README.md` was not modified** — see the CF-07 note below. No Product source, executable test, workbook, dependency, lock file, fixture, configuration or template file was created, modified or deleted.
- **Owner-statement intake (design basis only).** Six-node baseline recorded brief-stated: 6 nodes × `750-362 × 1 + 750-498 × 5 + 750-600 × 1` (750-362 × 6, 750-498 × 30, 750-600 × 6, 6 endpoints); capacity 240 physical / 212 required / 28 remaining (arithmetic recomputed); `OWNER_CONFIRMED_FACTORY_DEFAULT` configuration basis; TEST ONLY IPs `192.0.2.11`–`192.0.2.16` (TEST-NET-1 documentation range, narrative record only — no `config/` example file, because boundary-scanner rule S2 forbids any non-loopback IPv4 in the Product tree); proposed IP-independent identities `TC-NODE-*` / `TC-MODULE-*` / `TC-CHANNEL-*`. **Nothing here is drawing-verified or hardware-verified.**
- **STOP record.** Workspace-wide search proved **zero drawing files and zero manual files** in the sandbox. The six-cabinet drawings (filenames unspecified), `m07500498_00000000_0en.pdf`, and the 750-362 / 750-600 manuals (filenames unspecified) are `NOT_AVAILABLE_IN_SESSION`. Schedule derivation STOPPED with **0 rows** (brief §4); manual extraction STOPPED with nothing cited (brief §§7–8); capacity STOP recorded at 0 used vs 212 expected (brief §12). Only the affected workstreams were stopped (AGENTS §11.9); the checkpoint, issue register, evidence-register update and the configuration-model / verification-plan proposals were completed.
- **Not created — deliberately.** No drawing-derived CSV under `docs/evidence/` (would have required inventing every acquisition field); no separate TEST-IP configuration example (optional item declined on the S2 governance ground above); no process-image profile, no address/offset manifest, no documented decoding contract (no manual evidence exists to base them on). The predecessor wire-break decision-tree design position stands unchanged.
- **Evidence register.** `REQ-TC-001` and `REQ-TC-002` are statement-only `PROVIDED_UNVERIFIED` (TCEV-12, model facts at `MODEL_LEVEL`, instance counts stated-not-verified); test-IP statement TCEV-13; absent drawings/manuals TCEV-14 … TCEV-16; Category A/B statement supersessions; §9.4 counts 18 `NOT_PROVIDED` + 2 statement-only; new §12 addendum. No verified state was widened; no hash was invented or reused.
- **Stage 0.4C-1 entry criteria.** Re-checked: still **10 of 11 NOT MET** (criterion 6 satisfied only vacuously). A brief statement satisfies no criterion.
- **CF-07 extended, not repaired.** `README.md` is outside this Stage's file scope, so its status block (still naming Stage 0.4C-0A and PR #17 OPEN) is reported for an Owner decision rather than edited, per AGENTS.md §2.2 and §2.3.
- **Validation.** Documentation-only: boundary scan S1–S9 result, workbook SHA-256 unchanged, changed-file allowlist (6 files), Product-source and executable-test no-change proofs, template-integrity re-check (28 columns / 212 rows / 0 gap rows, untouched), capacity arithmetic recomputation, relative-link check and `git diff --check` recorded in the final report. Drawing-derived CSV validations are **correctly absent** (no CSV exists). **No `dotnet` or `npm` test was run.** Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review PASSED are **predecessor evidence cited, not re-run at this head**.
- **Not authorised or not implemented.** Physical Sensor acquisition NOT IMPLEMENTED. Hardware loop test NOT EXECUTED. NO live MODBUS communication implemented. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor tested.
- **Completion (2026-10-12, same branch/PR).** Baseline REV1 evidence arrived via the Owner-connected source (ZIP 17345849 bytes, SHA-256 `7B886950…AA23E`, integrity gate PASSED; ephemeral bytes removed after review; archive not committed) and was fully reviewed. New: `docs/evidence/tc-drawing-schedule-rev1.csv` (240 rows: 200 verified, 12 internal-conflict, 4 topology-conflict, 24 spare, 0 unresolved; 212 MATCHED across 106/106 Sensors; all cells read on-render), `docs/evidence/tc-process-image-profile-rev1.md` (40 Modbus words/node, Type K decoding contract at `MODEL_LEVEL`, status-byte invisibility + `0x7FFF` ambiguity recorded) and `docs/evidence/tc-schedule-validation-rev1.md` (narrow validation). Issue register §7 records ISS-REV1-01/02/03 + spares; source register §13 + TCEV-17…20 advance `REQ-TC-001`…`REQ-TC-007` (§9.4: 13 `NOT_PROVIDED`, 7 advanced). `REQ-TC-008`…`REQ-TC-020` stay `NOT_PROVIDED`; configuration, transport identity and observed behaviour remain unevidenced. Still BLOCKED: Stage 0.4C-1 acquisition and wire-break decoding. Still not authorised: TEST_HARDWARE, PRODUCTION, device write/control.

Record: [`docs/STAGE_0.4C-0B_CHECKPOINT.md`](STAGE_0.4C-0B_CHECKPOINT.md), [`docs/TC_DRAWING_ISSUE_REGISTER_REV1.md`](TC_DRAWING_ISSUE_REGISTER_REV1.md), [`docs/TC_EVIDENCE_SOURCE_REGISTER.md`](TC_EVIDENCE_SOURCE_REGISTER.md) and [`docs/evidence/`](evidence/tc-schedule-validation-rev1.md).

##### 3.9 Stage 0.4C-K1 — Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview (2026-10-12, development checkpoint)

**Status: DEVELOPMENT CHECKPOINT, SUBMITTED FOR OWNER REVIEW in a NEW PR against `main` — OPEN, NOT MERGED (do not merge).** Commit SHAs are recorded in the PR and the final report, not inside their own commits (AGENTS §4.6).

- **Base and session.** Approved remote-main base `main` `48f0315ea1bcbd3f71f46a47bb4b55fc04327487` (the **PR #18 merge commit**; parents `3e1a7fd9d28db65221c123960a0c85b62d93949a` and `b2048ec0aa5703767a2af85c73eaa1ae94d06e53`, verified in order), platform-assigned branch `arena/3be5c713-waterjet-sentinel-suite`, base gate PASSED (index and worktree clean, no conflicting open PR, workbook SHA-256 `4e0337e2…f422e8e` re-verified). **Sandbox recreation was not triggered.** The merged PR #18 branch was not used.
- **Owner dependency decision.** The single approved dependency is `Microsoft.Web.WebView2` pinned `1.0.4258.31` in `Directory.Packages.props`, referenced without a version by `apps/kiosk/Wjss.Kiosk.csproj`; the Evergreen runtime is the approved distribution for the development preview. Rejected: Edge application/kiosk mode as the delivered shell, the legacy web-browser control, Electron or another desktop framework, and a source-only shell that remains non-building. Pre-change file hashes and the full dependency record are in [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](STAGE_0.4C-K1_CHECKPOINT.md) §4. **No other dependency was added, removed or refreshed**, and no npm dependency was touched for the shell. `apps/kiosk/packages.lock.json` must be regenerated **Owner-locally** (the Arena sandbox has no .NET SDK or NuGet feed): the refresh is a recorded Owner-local step, not a silent omission.
- **Product features.** `apps/kiosk`: an application-owned chromeless window onto the approved loopback origin only (hard-constant scheme and host; only the port may be matched for the Owner-local preview), with navigation, other-port/other-scheme/local-file, popup, new-window, download and permission refusal — all counted on screen — developer tools and browser accelerator keys off, `ZoomFactor` 1.0 with the existing PerMonitorV2 DPI property, a 2 s health probe driving a visible **LOCAL PREVIEW HOST UNAVAILABLE — RECONNECTING** screen that hides the view so a frame that stopped updating is never shown as current, automatic reload on recovery and after a view-process failure, and a confirmed, visible Operator exit with a close guard on user-initiated closes. `packages/commissioning`: read-only parsing and classification of the committed 240-row drawing-derived schedule, three separated dimensions (Quality / DiagnosticReason / CommissioningState), `INVALID` presentation-only, invalid values never rendered as a number, last valid sample labelled historical and not current, the ten **protected** deterministic scenarios plus the three paired thermal
demonstrations added by the Front/Rear thermal-pair pass (thirteen in total), an observation contract with summaries, and an in-memory Preview-only workflow. `apps/ops-preview`: the loopback-only read-only host and the Product-shaped Operations route with the **Sensor Commissioning Monitor** as the main area, the Sensor Map showing the two gap positions with the 108/106/212 counts, and Mapping/Diagnostics preview summaries that state their missing authority; GET/HEAD only, 405 otherwise, `default-src 'self'`, no-store, no CORS wildcard, and no route for any device, transport, control, queue, job, cleaning, pump, valve or motion action.
- **Scenarios.** `NORMAL_GOOD`, `UNCERTAIN_TRANSIENT`, `OUT_OF_RANGE`, `STALE`, `COMMUNICATION_LOSS_NODE` (exactly one node, the other five reported online), `SIMULATED_WIRE_BREAK` (labelled `SIMULATED_DIAGNOSTIC_SCENARIO`; raw `0x7FFF` as evidence only with an unavailable engineering value), `RECOVERY`, `DRAWING_CONFLICT`, `PRODUCT_TOPOLOGY_CONFLICT`, `UNASSIGNED_CHANNEL` — pure functions of the evidence rows and a tick, with no clock, no random source and no device.
- **Validation in Arena (exact).** `packages/commissioning` `node --test test/*.test.mjs` → **77/77 pass, 0 fail**; `apps/ops-preview` → **40/40** (server 15, render 25); `apps/kiosk` source-contract → **12/12**; unchanged Mapping graph re-run → Mapping UI **82/82**, Mapping package **268 (261 pass, 0 fail, 7 skipped)**; `node tools/boundary-scan/boundary-scan.mjs .` → **`0 findings (S1–S9 clean)`**; workbook SHA-256 unchanged; `git diff --check` clean. Two genuine scan findings raised during the Stage were fixed **in the source** and the scanner was **not** weakened.
- **Checks performed and NOT performed.** The local host was started in the sandbox and exercised over loopback HTTP (health, boundary, schedule, observations with node-scoped communication loss and the labelled simulated wire-break, sensor map, 405 on a write method), and the render suite executes the real UI code against the real host in a test DOM. **No browser was opened against the preview; no Windows session exists; the Kiosk shell was not compiled or run; no screenshot, layout or 1920×1080 DPI acceptance was performed.** The shell suite is a static source contract and is not a substitute for the Owner-local build and visual check.
- **Documentation (single consolidated pass).** `docs/STAGE_0.4C-K1_CHECKPOINT.md` (new), `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`, `docs/decisions/ADR-0016-kiosk-shell-direction.md` (dated status update; the ADR keeps its **DRAFT** status and its draft Runtime-supervision items are **not** implemented in this Stage). **`README.md` was not modified** — CF-07 stays open for an Owner decision.
- **Unchanged.** The authoritative workbook; the `docs/evidence/` Drawing-derived schedule, process-image profile and validation summary; the schedule templates; the Runtime; the Mapping implementation and both Mapping suites; `packages/contracts`, `packages/domain`, `packages/application`, `packages/time`, `adapters/**`; Pump/Valve logic; the Cleaning sequence; the Queue kernel; the Galil and MODBUS implementation surfaces; production configuration; the live Hardware provider; `Directory.Build.props`; `global.json`; every other project and lock file. **No file was deleted or renamed.**
- **Not verified, and not claimed.** Compilation of the changed .NET graph, the Kiosk Release build, the Owner-local lock-file refresh, Windows runtime behaviour, DPI rendering, the 1920×1080 visual acceptance, the embedded-view runtime policy callbacks, the Owner Browser Review and every .NET test.
- **Status.** **NO HARDWARE. No live MODBUS. No TEST_HARDWARE. No PRODUCTION. No device write or control. No physical Sensor tested. Field verification PENDING. `WIRE_BREAK_DECODING: BLOCKED`.** Physical Sensor acquisition **NOT IMPLEMENTED**. **NEW PR against `main` — OPEN, NOT MERGED (do not merge).**

- **Owner-local validation and zero-warning correction (same branch and PR, 2026-10-12).** Owner-local results: kiosk contract 12/12, commissioning check + 77/77, Operations Preview check + 40/40, Mapping Configuration check + 82/82, normal and locked solution restores passing, full .NET tests **414/414**, boundary S1–S9 clean, a genuine Owner-local lock refresh for `Microsoft.Web.WebView2` `1.0.4258.31`, and a Release build with exactly one warning, `MSB3277` (WindowsBase `4.0.0.0` versus `5.0.0.0`). Root cause, from the Owner-local package evidence: this package's `buildTransitive` targets add `...Core`, `...WinForms` and `...Wpf` unconditionally on the branch selected for `net10.0-windows`, and the WPF-only assembly is compiled against a WindowsBase major version that the .NET 10 reference pack does not provide — the evaluated project properties are `UseWindowsForms=true` and `UseWPF` empty, so the conflict came from the package, not from the project. The correction is one narrow project-local MSBuild target in `apps/kiosk/Wjss.Kiosk.csproj` that removes only the `Microsoft.Web.WebView2.Wpf` reference item by file name between package evaluation and assembly resolution, with a companion proof target that fails if Core or WinForms is missing or WPF is present. No WPF enablement, no `NoWarn`, no severity change, no binding redirect, no manual WindowsBase reference, no cache or package-file edit, no vendored assembly, no dependency-version change and no lock-file hand-edit. Contract coverage extended (kiosk **17/17**); Arena re-run green (commissioning **77/77**, Operations Preview **40/40**, Mapping UI **82/82**, mapping package **268 = 261 pass + 7 skipped**, boundary **S1–S9 clean**). **Not verified in Arena:** the correction was not executed there (no .NET SDK, no reachable package feed) — the Owner-local zero-warning build, the reference-graph proof and the manual kiosk review remain the verification of record. The Stage is not complete until they pass.

Record: [`docs/STAGE_0.4C-K1_CHECKPOINT.md`](STAGE_0.4C-K1_CHECKPOINT.md), [`apps/kiosk/README.md`](../apps/kiosk/README.md) and [`apps/ops-preview/`](../apps/ops-preview/).

See also §12 Owner-local results, §13 correction and §13.1 handoff commands in the checkpoint document.

- **Owner Manual Review correction (same branch and PR, 2026-10-12): Owner Manual Review FAILED.** Owner-local automated validation had passed (zero-warning Release build, .NET **414/414**, kiosk contract 17/17, WebView2 isolation proof `CoreCount=1, WinFormsCount=1, WpfCount=0, LoaderCount=3`), and the Owner's real host run connected successfully (`listening on 127.0.0.1:5187`; `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)`), but at fullscreen 1920×1080 the application was **wider than the screen**: `Exit Kiosk Preview` sat completely outside the right edge and the embedded page's Density control was clipped. **One root cause, two symptoms:** the header's `AutoSize`/`WrapContents=false` left panel demanded its full preferred width and the outer layout's unstyled column defaulted to `AutoSize`, so the column grew past the client width and stretched the content cell, the embedded view and the web page viewport. **Corrections:** a measured two-column/four-row native header (action column `AutoSize` and reserved first; every text row `AutoSize=false` with `Dock=Fill` and wrapping; absolute row heights from `TextRenderer.MeasureText`; re-measured on resize, on monitor DPI change and on any header text change) with the outer column explicitly `Percent 100`; a hardened Operations Preview stylesheet (`overflow-x:hidden` page guard, `main` at `minmax(0, 1fr)`, `min-width:0` on wide flex/grid children, the 20-column table contained in its own scroll frame, no `100vw`); a real `start` script so both documented launch commands exist; and new contract coverage (kiosk **21/21**, new Operations Preview layout suite **9 tests**, Operations Preview total **49/49**). Unchanged: the WebView2 pin and WPF-reference isolation, Central Package Management, the protected evidence totals and topology, the loopback origin, the read-only boundary, the confirmed exit and the reconnecting fail-safe. **Not verified in Arena:** the header was not compiled or rendered and no browser was opened — the new tests are source/structure contracts, the earlier 1920×1080 DPI-safe claim is withdrawn to an **acceptance target**, and the Owner-local fullscreen rerun and manual acceptance list are the validation of record. The Stage is not complete until they pass. See checkpoint §14 and §14.1.

- **Runtime-init correction (same branch and PR, 2026-10-12): the width correction PASSED Owner visual review, but the interaction review FAILED.** With the host started through the real package command (`npm --prefix .\apps\ops-preview start`) and the Kiosk reporting `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)`, HTML and CSS rendered while the application did nothing: unresponsive Density and navigation, empty Scenario/Node selectors, no node summary, no commissioning progress, Overview header with no rows, empty Sensor detail. **Exact root cause: the served page contained no `script` element at all**, so the browser never requested `app.mjs`. The earlier suites imported `public/app.mjs` **directly** and therefore could not see it. **Correction:** the page now loads the module (`<script type="module" src="/app.mjs">`, the only script element, no inline script, CSP unchanged) and the application reports a machine-readable boot sentinel (`data-app-state` = `booting`/`ready`/`failed`) with a **visible, safe** initialization-failure surface (`#boot-error`, category only — no stack, path or internal detail) plus a `<noscript>` note; an empty control value falls back to the current state. **New coverage:** `apps/ops-preview/test/page.test.mjs` (10 tests) resolves the application through the server like a browser (one module script element, `/app.mjs` byte-identical to the tested file, CSP permitting it, the whole served module graph resolved recursively, boot reaching `ready` with every request a successful local GET, ten scenarios, six nodes, 240 rows, row selection, Compact/Comfortable and all five anchors, scenario application, and a failed boot becoming a visible safe state), and the boot helper now gives every call a fresh module instance. Arena gates: kiosk **21/21**, Operations Preview **59/59**, commissioning **77/77**, Mapping UI **82/82**, mapping package **268 (261 pass, 7 skipped)**, boundary **S1–S9 clean**. **NOT RUN in Arena:** real-browser execution (no browser in the sandbox) — the tests are wiring and boot contracts over served bytes, not rendering checks. Owner Manual Review has not passed; the Owner-local rerun in checkpoint §15.5 is the verification of record. Unchanged: WebView2 pin and WPF isolation, the 1920×1080 layout strategy, loopback binding and approved origin, no external assets, the protected evidence totals, the read-only boundary, the reconnecting fail-safe and the confirmed exit.


- **Front/Rear thermal-pair semantics and synthetic diagnostic pass (same branch and PR, 2026-10-12; starting at `4d177f6`).** The Owner found that the Synthetic Preview systematically rendered **Rear above Front** for every Product Sensor pair (Front 29.4 / Rear 35.1), which contradicts the approved Sensor geometry and would train the Operator to read the pair backwards. **Exact cause: the synthetic generator** (`syntheticEngineeringValue` applied `sideBias = side === 'F' ? 0 : 4`, biasing every Rear sample up by about 4 °C) — a generation bug, corrected at the source and never by patching rendered text. Rear is now the deterministic paired waterwall-tube-side **reference** and Front is `Rear + the scenario's declared differential`, so **Front > Rear for every complete pair**; both sides stay pure functions of the physical Sensor identity (no RNG, no clock, no rendering-order dependence). Verified at tick 0: 106/106 pairs available, `NORMAL_GOOD` deltas **3.2 … 6.8 °C in 34 distinct values** (never one universal delta), `REDUCED` **0.8 … 1.7**, `NEAR_ZERO` **0.1 … 0.2**, `REVERSED` **−3.4 … −1.0**. New read-only model `packages/commissioning/src/thermalpair.mjs` derives **`dFR = FrontTemperature − RearTemperature`** per render from the authoritative observations: five states (`EXPECTED_POSITIVE`, `REDUCED_POSITIVE_CANDIDATE`, `NEAR_ZERO_CANDIDATE`, `REVERSED_CONTRADICTION`, `DIFFERENTIAL_UNAVAILABLE`) kept strictly separate from Quality, DiagnosticReason, CommissioningState, evidence blocking and the Preview verdict; the delta exists only when both sides are one Product Sensor with exactly one Front and one Rear, both current values valid, the same compatible unit, neither BAD/STALE/DISABLED/communication-loss/wire-break, both finite and the identity complete — never from a Last Valid Value, a historical value, a BAD/STALE/disabled/unassigned value, an I7/I16 conflict row or a copied/substituted measurement, with fourteen named blocking categories and the sign always winning over a scenario label. Presentation: one compact read-only `dFR` row per Wall Map cell (unit only while available, `dFR --` with the named reason otherwise) and a fourth read-only Sensor-detail card (Front value, Rear value, delta, paired state, guidance, synthetic provenance, `FIELD VERIFICATION PENDING`, no automatic effect); the Overview gains no row and no column, the protected totals stay **240 / 106 / 212**, the delta is not a Channel, measurement, Sensor or loop-test target and receives no observation, verdict, remark or Preview result (recording or clearing one side never mutates the other). **`FrontRearDelta` is READ-ONLY DIAGNOSTIC / TREND ONLY** until field baselines and operating regimes are approved: no threshold, alarm, trip, interlock, persistence, deadband, hysteresis or automatic slag classification, and no Cleaning Queue / GlobalQueue / WaterJet dispatch / cleaning job / AutoSequence / pump / valve / motion / automatic-control / field-approval effect; slag wording stays candidate-bounded. **Scenario catalogue 10 → 13** (three appended protected-intact demonstrations; every count contract updated). Arena gates: commissioning check OK + **105/105**, focused package paired tests **16/16**, Operations Preview check OK + **88/88** (was 78), focused page/presentation paired tests **10/10**, kiosk **21/21**, Mapping UI **82/82**, mapping package **268 (261 pass / 7 skipped)**, boundary **S1–S9 clean**, `git diff --check` clean. **NOT RUN in Arena:** .NET build/tests and real-browser execution (no browser engine, no .NET SDK) — these are source-level and stub-DOM proofs, not a visual verification. Owner Manual Review has not passed; the 28-step Owner-local rerun is checkpoint §18.7. Unchanged: topology and evidence, the U-shape geometry and static vertical scrolling contract, the selection identity and synchronisations, the loop-test workflow authority, the Kiosk shell and WebView2 `1.0.4258.31` with its WinForms-only reference isolation, every lock file, loopback-only hosting, the strict CSP, the reconnecting fail-safe and the confirmed exit — **no dependency added or changed**.

##### 3.1 Recorded reconciliation of the Stage 0.2 title

The Stage 0.1 candidate decomposition listed Stage 0.2 as *Requirements and Configuration
Model Refinement* and Stage 0.3 as *Architecture and Interface Specification*. Both entries
were `[PROPOSED]` candidate decomposition, not approved stations.

The approved Stage 0.2 Scope Gate defines Stage 0.2 as **Technology and Solution Architecture
Decision**, a documentation and architecture-decision checkpoint. That approved gate takes
precedence for Stage 0.2, so the ledger above records the approved title. Consequences:

1. The previously proposed Stage 0.2 deliverable — Owner-ratified requirement set and
   configuration field specification — is **not authorised** by the approved Stage 0.2 gate
   and remains `[PROPOSED]` and `[NOT AUTHORIZED]`.
2. The previously proposed Stage 0.3 deliverable remains `[PROPOSED]` and
   `[NOT AUTHORIZED]`. It is **not** the same thing as Stage 0.2 and is not started. (Under the
   later Option-C amended gate, the narrower **0.3A-1 source checkpoint** — contracts and
   skeleton as *authored, unvalidated source* — exists; it is not the full Stage 0.3
   deliverable and proves nothing until the Owner-local validation PASS.)
3. The remaining open requirement and configuration items are listed in
   [`CURRENT_STATE.md`](CURRENT_STATE.md) section 8 and in
   [`ARCHITECTURE.md`](ARCHITECTURE.md) section 34, each with the gate that must close it.

##### 3.2 Proposed Stage 0.2.1 — UI and Runtime Technology Spike

> **SUPERSEDED.** The Owner replaced the dual-candidate proposal with the React-first Stage 0.2.1A feasibility spike; React was subsequently selected as the Primary UI Framework (Owner decision, 2026-10-07); a Blazor counter-spike is not required unless a future material blocker is identified. This section is retained as history.

| Item | Value |
| --- | --- |
| Stage | 0.2.1 — UI and Runtime Technology Spike |
| Gate | `[PROPOSED]` and **`[NOT AUTHORIZED]`** |
| Implementation | Not started |
| Purpose | Close the `[OPEN]` UI framework decision and validate the live-state delivery, live-trend, and offline-restore architecture with measured evidence |
| Candidates | Candidate A — React + TypeScript + Vite in the application-owned kiosk shell; Candidate B — Blazor Hybrid in the application-owned kiosk shell |
| Method | Identical synthetic data, identical runtime/API contract, identical Sensor presentation semantics, identical trend workload, identical camera placeholder or synthetic stream, identical reconnect scenario, identical acceptance measures |
| Measurements | CPU average and peak; memory usage; memory growth over extended operation; UI update latency; interaction latency; render stability; trend performance; push payload size; reconnect time; kiosk integration; offline build and restore complexity; package footprint; package licensing; testing ergonomics; development effort; maintainability |
| Constraints | Synthetic data only. No WAGO, Galil, Production SQL Server, or Production configuration access. No physical device. Spike results are application-level performance evidence only |
| Deliverable | A comparison report with measurements, a recommendation, and a decision-record draft. No production code, and no promotion of spike code into the product. **Any real UI implementation requires a separate later implementation Stage Gate** |

Full workload, measurement, and constraint detail is in
[`ARCHITECTURE.md`](ARCHITECTURE.md) section 33. The spike is **not** authorised by Stage 0.2,
and Stage 0.2 does not start it.

#### 4. Gate evidence rules

For every stage, the report must contain:

- **CHANGED** — every file created or modified, with a short purpose.
- **UNCHANGED** — an explicit statement of what was not touched.
- **NOT VERIFIED** — every unproven value, behaviour, or integration.
- **Validation performed** — the exact checks executed and their observed results.

A stage is not complete until its validation evidence exists and its discrepancies are
documented. Claims of success without observed tool output are prohibited.

#### 5. Standing constraints across every stage

These constraints apply to all stages unless a gate explicitly lifts one of them:

1. Production device access is `[NOT AUTHORIZED]`. See
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md).
2. Production valve and pump write control is `[NOT AUTHORIZED]` until bench verification
   is complete and recorded.
3. Production values — addresses, register maps, tag lists, coordinates, travel limits,
   pulses per engineering unit, speeds, accelerations, decelerations, pressure setpoints,
   DCS permissive definitions, and production thresholds — must never be invented, and
   production instances must never be committed.
4. Requirements, architecture, and documentation must remain consistent. An inconsistency
   stops the affected part of the work and is reported.
5. Reliability, stability, deterministic behaviour, recoverability, operator usability,
   maintainability, and appropriate baseline security rank above feature breadth.
6. Standards are used as guidance and design inspiration only. No claim of certification
   or compliance may be made at any stage.
7. **At most one Cleaning Job may be ACTIVE at any time. Parallel Water Jet cleaning is
   prohibited.** No stage may relax this.
8. **No application version exists.** A documentation stage does not establish one, and
   the documentation versioning policy remains `[OPEN]`.
9. **The Operations UI close guard is an operational usability control, not a safety
   protection.** No stage may describe it as hardware fail-safe.

#### 6. Definition of done for a documentation stage

A documentation stage is done when:

- Every named file exists and is complete.
- Internal links resolve (checked mechanically where practical).
- The status legend is applied consistently, and no unapproved behaviour is presented as
  approved.
- Owner-confirmed review decisions are propagated to every affected document.
- Prohibited content is absent (checked by search).
- The diff has been reviewed before committing.
- The checkpoint commit exists with a CHANGED / UNCHANGED / NOT VERIFIED body.
- The stage branch is pushed, and a pull request exists if permissions allow.
- The pull request is **not** merged by the contributor.

A stage that has received review changes is not done until a review-correction checkpoint
has been submitted and re-review has been requested.

---

#### Related documents

- [`AGENTS.md`](../AGENTS.md) — working contract
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state, stage status, and open items
- [`ROADMAP.md`](ROADMAP.md) — forward view of planned capability
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`decisions/README.md`](decisions/README.md) — decision records

##### Stage 0.4A CP-3c-1 development scope
Owner-authorized library-only atomic Runtime publication foundation on approved main `6b0ff7e`. State, matching Delta and bounded history must be one reader-visible generation. Existing Host composition is unchanged; CP-3c-2 and CP-4 are not authorized. See `STAGE_0.4A_CP-3C-1_CHECKPOINT.md` for development status; Owner-local validation remains required.

##### Stage 0.4A CP-3c-1 Owner-local validation and Final Source Review

Owner-reported validation at code head `29f133ce6598bc2eb489273a2f2d40f8827deb5f` (.NET SDK 10.0.401; not run in Arena): locked restore PASS, lock drift NONE; Release build PASS, 0 warnings, 0 errors; RuntimePublicationTests **20/20**, Runtime.Core **450/450**, full .NET **572/572**, fixture parity **7/7**, TypeScript typecheck PASS and tests **31/31** (0 failed); boundary scan **0 findings, S1–S9 clean**; `git diff --check` PASS; final Owner-local working tree CLEAN. Failed 0, skipped 0 in each reported .NET suite; no Owner-local artifact commit required.

Final Source Review (static, exact validated code head): **PASS, no blocking defect**. CP-3c-1 delivers the library-only atomic State/Delta publication aggregate with one writer, bounded contiguous history and detached reader/result snapshots. The full-window trend failure was fixed by checking equal-count *unchanged content* before testing the exact oldest-eviction-plus-append shift. The internal-Delta JSON fingerprint failure was a test defect: fingerprints now serialize `ProjectWire(delta)` and previous Sensors, never raw `RuntimeDelta`. The reader-facing mutable `TrendPoint.Series` alias was corrected with a defensive-copy reader boundary and nested typed freeze, without changing the contract. Analyzer corrections CA1861 and xUnit1031 are closed at the validated code head. No Host integration, Runtime API integration, Inspector change, contract, fixture or TypeScript change is included. CP-3c-2 and CP-4 are NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION device access are NOT AUTHORIZED. PR #10 remains OPEN, NOT MERGED, for Owner merge review.
