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
| 0.4 | Offline Domain and Queue Engine Implementation | `[PROPOSED]` — **Stage 0.4A (Simulator Sequencing Foundation) Owner-authorised for CP-0 and CP-1 only, 2026-10-08** (see §3.3) | CP-0 status reconciliation (documentation); **CP-0 and CP-1 OWNER-LOCALLY VALIDATED** (validated head `03144f4`, 2026-10-08; F1 blocking state-integrity defect CLOSED; CP-1 Final Source Review PASSED); CP-2 scope gate PROPOSED (§3.3.1; prerequisite F1 corrected and validated); CP-2 coding, CP-3 and CP-4 `[NOT AUTHORIZED]` | First source code: pure domain logic with a simulator and automated tests; no device access |
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
- **Stage state (updated 2026-10-08, final evidence amendment):** CP-0 and CP-1 are **OWNER-LOCALLY VALIDATED** at `03144f4` (Owner-reported: 253 of 253 .NET tests; Release build 0 warnings and 0 errors). F1 blocking state-integrity defect **CLOSED** (`23b276f`, `03144f4`); CP-1 Final Source Review **PASSED**. The earlier "FOLLOW-UP RECOMMENDED BEFORE MERGE, not blocking" classification of F1 is withdrawn. The earlier validated head `2cfe648` (217 tests) is historical. CP-2 scope gate **PROPOSED** in §3.3.1; **CP-2 coding NOT AUTHORIZED**. CP-3 and CP-4 NOT AUTHORIZED. The earlier "Sources are authored in Arena / NOT COMPILED IN ARENA / OWNER-LOCAL VALIDATION REQUIRED" wording is superseded. PR #7 OPEN, NOT MERGED.

### 3.3.1 Stage 0.4A CP-2 scope gate — PROPOSAL (2026-10-08, not authorized)

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

**5. Exact Safe Return states.** `SafeReturnStep` values, in order: **SR1** stop cleaning activity and disable synthetic water output (same transition as the trigger); **SR2** request the paired Isolation Valve close (intent emitted; lifecycle `SAFE_RETURN_CLOSE_VALVE`, then `SAFE_RETURN_VERIFY_VALVE_CLOSED`); **SR3** confirm valve `CLOSED`, observed after the SR2 intent (an earlier observation is not reused); **SR4** request the axis return to Standby (intent emitted only after SR3, same transition as SR3 confirmation; lifecycle `SAFE_RETURN_TO_STANDBY`, then `SAFE_RETURN_VERIFY_STANDBY`); **SR5** confirm axis `AT_STANDBY`, observed after SR4; **SR6** record the outcome (§11); **SR7** release the Active Job; **SR8** later sequencing may be considered (no kernel action; the next `DispatchHead` is a separate transition). Failure: `SR_FAILED` with lifecycle `SAFE_RETURN_FAILED`.

**6. Exact valve feedback states** (derived from the accepted matrix, [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §5.2; columns as in that table):

| Upper limit | Lower limit | Derived `ValveFeedbackState` | Safe Return meaning |
| --- | --- | --- | --- |
| 0 | 1 | `CLOSED` | Satisfies SR3 when observed after SR2 |
| 1 | 0 | `OPEN` | Does not satisfy SR3; waits; a timeout input fails the step (§19 O-6) |
| 0 | 0 | `TRANSIT_OR_FAULT` | Legitimate during bounded travel; waits until a timeout input, then failure |
| 1 | 1 | `INVALID_LIMIT_STATE` | Always a fault: immediate `SAFE_RETURN_FAILED` |

Feedback carries the `ValveId`. A mismatched identity is refused (§19 O-6). Valve timeouts are input events only (§9).

**7. Exact axis feedback states** (abstract only). `AT_STANDBY` satisfies SR5. `NOT_AT_STANDBY` and `UNKNOWN` do not satisfy SR5 and wait until a `FeedbackTimeoutExpired(AXIS_STANDBY)` input, which fails the step. `FAULT` fails the step immediately. No coordinate, speed, homing or positioning value exists anywhere in CP-2. The confirmation source is open (decision D10, §19 O-7). No axis command is ever issued before SR3 is confirmed.

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
| `SAFE_RETURN_VERIFY_VALVE_CLOSED` | `ValveLimitObserved(CLOSED)`, observed after SR2 | none | `SAFE_RETURN_VERIFY_STANDBY` | SR3 confirmed; SR4 intent | `SR3`, `SR4` |
| `SAFE_RETURN_VERIFY_VALVE_CLOSED` | `ValveLimitObserved(OPEN` or `TRANSIT_OR_FAULT)` | none | unchanged | observation recorded | `VALVE_NOT_CONFIRMED` |
| `SAFE_RETURN_VERIFY_VALVE_CLOSED` | `ValveLimitObserved(INVALID_LIMIT_STATE)` or `FeedbackTimeoutExpired(VALVE_CLOSED)` | none | `SAFE_RETURN_FAILED` | Job retained; no release; no dispatch | `SR_FAILED` |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(AT_STANDBY)` | none | Job cleared after SR6 and SR7 | SR5 confirmed; outcome recorded; release | `SR5`, `OUTCOME_RECORDED`, `JOB_RELEASED` |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(NOT_AT_STANDBY` or `UNKNOWN)` | none | unchanged | observation recorded | `STANDBY_NOT_CONFIRMED` |
| `SAFE_RETURN_VERIFY_STANDBY` | `AxisFeedbackObserved(FAULT)` or `FeedbackTimeoutExpired(AXIS_STANDBY)` | none | `SAFE_RETURN_FAILED` | Job retained; no release; no dispatch | `SR_FAILED` |
| `SAFE_RETURN_FAILED` | any Job-advancing event | none | unchanged | refused; no retry, reset or resume | `SAFE_RETURN_FAILED_RETAINED` |
| any state with an Active Job | `DispatchHead` | none | unchanged | refused (CP-1 `DISPATCH_REFUSED_JOB_ACTIVE`) | CP-1 code |
| any state with an Active Job | `RequestPause` | none | `PAUSE_REQUESTED` (CP-1) | the Job continues through Safe Return | CP-1 code |
| Job released (SR8) | `DispatchHead` | CP-1 rules | next Job | separate transition only | CP-1 codes |

**11. Outcome timing.** The outcome is recorded at SR6 only, after SR5 (`AT_STANDBY` confirmed) on the success path. Release (SR7) is an ordered record in the same transition as SR6. The next dispatch is always a separate transition. Outcome mapping (proposal):

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
- T-09 an axis-return intent is never emitted before a valve `CLOSED` observation that follows SR2.
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

### 3.3.2 Stage 0.4A CP-2 coding authorization and applied Owner rulings (2026-10-08)

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
