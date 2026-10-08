# Master Plan — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the stage-gate discipline, for the Stage 0.1 and Stage 0.2
positions, and for the approved Stage 0.2 Scope Gate; **stage contents beyond Stage 0.2 remain
`[PROPOSED]`**.
**Governing authority:** [`AGENTS.md`](../AGENTS.md), the approved Stage 0.1 Scope Gate, and
the approved Stage 0.2 Scope Gate — *Technology and Solution Architecture Decision*.

This document defines how delivery is staged, what a stage gate is, and what evidence a
stage must produce. It deliberately does **not** schedule work, and it does not authorise
any future stage.

---

## 1. Delivery principle

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

## 2. What a stage gate is

A Stage Gate is the only authority that permits new artefacts. It must state, at minimum:

1. The stage identifier and title.
2. What may be created or modified, as an explicit file list or an explicit category list.
3. What is explicitly out of scope for that stage.
4. The validation the contributor must perform and report.
5. The delivery instruction (branch, checkpoint, and whether a pull request is expected).

Behaviour described in a Scope Gate becomes `[APPROVED]` behaviour. Behaviour confirmed by
the Owner during a documentation review becomes `[OWNER CONFIRMED]` behaviour. Behaviour
described anywhere else remains `[PROPOSED]` until a later gate approves it.

## 3. Stage ledger

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
| 0.4 | Offline Domain and Queue Engine Implementation | `[PROPOSED]` — **Stage 0.4A (Simulator Sequencing Foundation) Owner-authorised for CP-0 and CP-1 only, 2026-10-08** (see §3.3) | CP-0 status reconciliation (documentation); **CP-0 and CP-1 OWNER-LOCALLY VALIDATED** (feature head `2cfe648`, 2026-10-08); CP-1 Final Source Review complete. CP-2 scope gate PROPOSED (§3.3.1); CP-2 coding, CP-3 and CP-4 `[NOT AUTHORIZED]` | First source code: pure domain logic with a simulator and automated tests; no device access |
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
set out in [`DOMAIN_MODEL.md` §2.2.1](DOMAIN_MODEL.md#221-logical-sensor-matrix-and-cannon-slots-owner-confirmed).
The 104 / 208 figures in historical records are the superseded baseline.

Stages 0.3 through 0.8 are a **candidate** decomposition. The Owner may merge, split,
reorder, or replace them. They are recorded here so that work is not invented ad hoc, not
because they are approved. Stage 0.3 and later remain `[PROPOSED]` and are `[NOT AUTHORIZED]`
until a later Owner Scope Gate approves them. **Update (2026-10-07):** the Owner's Option-C
amended gate authorises **Stage 0.3A** work in gated substages (0.3A-1 source checkpoint now
authored; Owner-local .NET validation is its mandatory pre-merge gate — see
[`STAGE_0.3A_PLAN.md`](STAGE_0.3A_PLAN.md)); it authorises **no** other stage, and 0.3A-2+
stay `[NOT AUTHORIZED]` until each is explicitly approved.

### 3.3 Stage 0.4A — Simulator Sequencing Foundation (Owner authorisation 2026-10-08)

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
- **Stage state (updated 2026-10-08, CP-1 closeout):** CP-0 and CP-1 are **OWNER-LOCALLY VALIDATED** at `2cfe648`; CP-1 Final Source Review complete (FOLLOW-UP RECOMMENDED BEFORE MERGE, not blocking). CP-2 scope gate **PROPOSED** in §3.3.1; **CP-2 coding NOT AUTHORIZED**. CP-3 and CP-4 NOT AUTHORIZED. The earlier "Sources are authored in Arena / NOT COMPILED IN ARENA / OWNER-LOCAL VALIDATION REQUIRED" wording is superseded. PR #7 OPEN, NOT MERGED.

### 3.1 Recorded reconciliation of the Stage 0.2 title

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

### 3.2 Proposed Stage 0.2.1 — UI and Runtime Technology Spike

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

## 4. Gate evidence rules

For every stage, the report must contain:

- **CHANGED** — every file created or modified, with a short purpose.
- **UNCHANGED** — an explicit statement of what was not touched.
- **NOT VERIFIED** — every unproven value, behaviour, or integration.
- **Validation performed** — the exact checks executed and their observed results.

A stage is not complete until its validation evidence exists and its discrepancies are
documented. Claims of success without observed tool output are prohibited.

## 5. Standing constraints across every stage

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

## 6. Definition of done for a documentation stage

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

## Related documents

- [`AGENTS.md`](../AGENTS.md) — working contract
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state, stage status, and open items
- [`ROADMAP.md`](ROADMAP.md) — forward view of planned capability
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`decisions/README.md`](decisions/README.md) — decision records
