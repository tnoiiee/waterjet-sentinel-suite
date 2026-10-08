# Stage 0.4A CP-2 — Cleaning Job, Mandatory Safe Return and Critical Pump Kernel — Checkpoint Report

**Status: SOURCE AUTHORED. STATICALLY REVIEWED. NOT COMPILED IN ARENA. NOT EXECUTED IN ARENA. OWNER-LOCAL VALIDATION REQUIRED.**

**Scope:** `packages/application/Runtime/Sequencing/**` and `tests/runtime.tests/Sequencing/**`, plus documentation. Pure `Runtime.Core` kernel logic only.

**Not authorized and not started:** CP-3, CP-4, TEST_HARDWARE, PRODUCTION, Runtime host integration, API, Inspector, command or write routes, device or Modbus/PLC/DCS/Galil/KMotion access, coordinates, speeds, homing, timers, persistence, SSE, TempQueue, TimeQueue, ZIP or release.

**PR state:** PR open, not merged. PR #7 not continued.

---

## 1. Base verification

| Check | Result |
| --- | --- |
| `git ls-remote origin refs/heads/main` | `8323f78c7ec2480bb30cbdd1432b6caa8e32c601` (exact) |
| Local HEAD before CP-2 work | `8323f78c7ec2480bb30cbdd1432b6caa8e32c601` (the approved base) |
| Branch | `arena/bba7709c-waterjet-sentinel-suite` (session-fixed; see §1.1) |
| Ancestry | Verified after a targeted `git fetch --unshallow origin main` (fetch only; no ref, working-tree or history rewrite). The clone had been shallow at the base. |
| Working tree before work | Clean (`git status --porcelain` empty) |
| PR #7 merge in ancestry | Yes: `8323f78` is `Merge pull request #7 …`; base is the PR #7 merge |
| CP-1 state-integrity correction in ancestry | Yes: `23b276f` (F1 correction) and `03144f4` (test correction) are ancestors of `8323f78` (verified) |
| CP-1 validation record present | Yes: `2cfe648` is an ancestor; `CHANGELOG.md` and `docs/CURRENT_STATE.md` record the Owner-local validation of CP-1 at `2cfe648` / `03144f4` |
| Sandbox recreation detected | No. The standing atomic recovery protocol was not needed, and no recovery approval was requested. |

Forbidden operations (`reset`, `clean`, `restore`, `rebase`, force-push, `checkout` of the working tree, `stash`) were not used.

### 1.1 Branch note

The brief asks for "one new branch". This session is fixed to `arena/bba7709c-waterjet-sentinel-suite`, so all CP-2 commits and the PR are on that branch. No other branch was created or pushed.

---

## 2. Commits (parents)

| Commit | Subject | Parent | Files |
| --- | --- | --- | --- |
| `ef51bd8` | `docs(plan): record Stage 0.4A CP-2 coding scope` | `8323f78` | `docs/MASTER_PLAN.md` (new §3.3.2: authorization and applied Owner rulings) |
| `6b94508` | `feat(runtime): add cleaning job safe return kernel` | `ef51bd8` | `packages/application/Runtime/Sequencing/`: `SequencingCodes.cs`, `SequencingEvent.cs`, `SequencingFeedback.cs` (new), `SequencingKernel.cs`, `SequencingSafeReturn.cs` (new), `SequencingState.cs`, `SequencingStateValidator.cs`, `SequencingTransition.cs`; deleted `SafeReturnReleaseEvidence.cs`. `tests/runtime.tests/Sequencing/`: `SequencingKernelTests.cs`, `SequencingStateIntegrityTests.cs` (CP-1 tests adjusted to compile against the changed kernel API). |
| `ffc2701` | `test(runtime): complete CP-2 sequencing scenarios` | `6b94508` | `tests/runtime.tests/Sequencing/SequencingJobSafeReturnTests.cs` (new, 47 tests) |
| (docs commit) | `docs: record Stage 0.4A CP-2 checkpoint` | `ffc2701` | `CHANGELOG.md`, `docs/CURRENT_STATE.md`, `docs/STAGE_0.4A_CP-2_CHECKPOINT.md` |

Product source changes are confined to `packages/application/Runtime/Sequencing/**`. Test changes are confined to `tests/runtime.tests/Sequencing/**`. No `packages/contracts`, fixture, project, lock, `apps/**`, `adapters/**`, `config/**` or `tools/**` file changed.

---

## 3. Existing contracts reused (no change)

| Contract | Use in CP-2 |
| --- | --- |
| `JobLifecycle` | `RUNNING`, `SAFE_RETURN_VERIFY_VALVE_CLOSED`, `SAFE_RETURN_VERIFY_STANDBY`, `SAFE_RETURN_FAILED` are stored. `SAFE_RETURN_CLOSE_VALVE`, `SAFE_RETURN_TO_STANDBY` and `ABORTING` are transient evidence values (see §7). |
| `SafeReturnStep` | `SR1`–`SR7` and `SR_FAILED`. `SR8` is not used (no kernel action). |
| `CriticalPumpKind` | Mapped to the two critical classes: `MAIN_PUMP_UNEXPECTED_STOP` and `MAIN_PUMP_TRIP`. |
| `JobPhase` | `P1`–`P6`, strictly ordered. |
| `AutoSequenceState`, `AutoSequenceMode` | Projections unchanged. `JOB_ACTIVE` projects any Job in a Safe Return lifecycle. |
| `ContractJson` | Deterministic serialization in tests. |

**Contract blocker check:** none. Every required state is either an accepted contract name (reused) or a kernel-internal name with no wire meaning. `packages/contracts` was not modified, so no Owner stop was needed.

---

## 4. New and changed type inventory

**New (kernel-internal, `public` only where the kernel API needs them):**

- Enums: `ValveFeedbackState` (`CLOSED`, `OPEN`, `TRANSIT_OR_FAULT`, `INVALID_LIMIT_STATE`); `AxisFeedbackState` (`AT_STANDBY`, `NOT_AT_STANDBY`, `UNKNOWN`, `FAULT`); `PumpObservation` (`READY`, `EXPECTED_STOP`, `UNEXPECTED_STOP`, `TRIP`); `FeedbackTarget` (`VALVE_CLOSED`, `AXIS_STANDBY`); `CleaningStage` (`PREPARING`, `READY_TO_CLEAN`, `CLEANING`, sub-stages of `RUNNING`); `CleaningJobOutcome` (`COMPLETED`, `FAILED`, `ABORTED`, `RECOVERY_REQUIRED`).
- Record: `SafeReturnLedger` (per-step evidence sequence numbers; written only by the kernel).
- Static: `ValveFeedbackDerivation.Derive(upper, lower)`.
- Events: `ObservePumpState`, `AdvanceJobPreparation`, `BeginCleaning`, `ExecutionPhaseVerified`, `RequestNormalCompletion`, `RequestAbort`, `ReportExecutionFailure`, `ValveLimitObserved`, `AxisFeedbackObserved`, `FeedbackTimeoutExpired`.
- Projection: `SequencingKernel.ProjectJobLifecycle`.

**Changed:**

- `SequencingActiveJob`: +12 fields (`Lifecycle`, `Stage`, `VerifiedPhase`, `CleaningActive`, `WaterOutputOn`, `LastValveFeedback`, `PendingOutcome`, `Trigger`, `TriggerReason`, `Step`, `FailureCode`, `Ledger`). All are explicit at dispatch (no default-as-running).
- `SequencingEvidence`: +11 optional fields (`JetId`, `ValveId`, `LifecycleBefore`, `LifecycleAfter`, `Phase`, `Step`, `ValveFeedback`, `AxisFeedback`, `Pump`, `JobOutcome`, `Intent`).
- `SequencingTransition`: +`Records` (every evidence record of the transition, in order). `Evidence` remains the last record.
- `SequencingState`: constructor and properties unchanged (eight-parameter internal seam, CP-1 boundary intact).
- `SequencingCodes`: CP-2 codes added; unused release codes removed.
- `SequencingStateValidator`: CP-1 checks unchanged; CP-2 combination checks added (§10).

**Removed:**

- `ReleaseActiveJob` and `SafeReturnReleaseEvidence` (file deleted). Release is now only the kernel's SR7 step.
- `RaiseCriticalSuspension`. A critical event now enters through `ObservePumpState` (see §12, deviation D-2).

---

## 5. Job transition table (CP-2 additions; CP-1 rules otherwise unchanged)

"Records" lists evidence codes in order. `SRn` is the Safe Return step.

| From (lifecycle / stage) | Event | Guard | To | Same-transition effects | Records |
| --- | --- | --- | --- | --- | --- |
| RUNNING / PREPARING | `ValveLimitObserved` | valve id matches the Job | RUNNING / PREPARING | stores valve feedback | `VALVE_FEEDBACK_OBSERVED` (or NO_OP if unchanged) |
| RUNNING / PREPARING | `AdvanceJobPreparation` | last valve `CLOSED` | RUNNING / READY_TO_CLEAN | none | `PREPARATION_ADVANCED` |
| RUNNING / READY_TO_CLEAN | `BeginCleaning` | pump ready and valve `CLOSED` | RUNNING / CLEANING, cleaning on, water on | intent `WATER_OUTPUT_ON` | `CLEANING_STARTED` |
| RUNNING / CLEANING | `ExecutionPhaseVerified(P1)` | valve `CLOSED` | P1 | none | `PHASE_VERIFIED` |
| RUNNING / CLEANING | `ExecutionPhaseVerified(P2)` | valve `OPEN` | P2 | none | `PHASE_VERIFIED` |
| RUNNING / CLEANING | `ExecutionPhaseVerified(P3…P6)` | previous phase verified | P3…P6 | none | `PHASE_VERIFIED` |
| RUNNING / CLEANING | `ExecutionPhaseVerified` out of order | — | unchanged | — | REFUSED `PHASE_OUT_OF_ORDER` |
| RUNNING / CLEANING | `RequestNormalCompletion` | P6 verified | SAFE_RETURN_VERIFY_VALVE_CLOSED, pending `COMPLETED` | SR1, SR2 | `SR1`, `SR2` |
| RUNNING (any stage) | `RequestAbort` | — | SAFE_RETURN_VERIFY_VALVE_CLOSED, pending `ABORTED` | ABORTING, SR1, SR2 | `ABORT_REQUESTED`, `SR1`, `SR2` |
| RUNNING (any stage) | `ReportExecutionFailure(reason)` | reason non-blank | SAFE_RETURN_VERIFY_VALVE_CLOSED, pending `FAILED` | SR1, SR2 | `SR1`, `SR2` |
| RUNNING (any stage) | `ValveLimitObserved` INVALID_LIMIT_STATE | — | SAFE_RETURN_VERIFY_VALVE_CLOSED, pending `FAILED`, reason `VALVE_INVALID_LIMIT_STATE` | observation, SR1, SR2 | `VALVE_FEEDBACK_OBSERVED`, `SR1`, `SR2` |
| RUNNING (any stage) | `ObservePumpState` UNEXPECTED_STOP or TRIP | — | SAFE_RETURN_VERIFY_VALVE_CLOSED, pending `ABORTED`, latch set | latch record, SR1, SR2, no dispatch | `CRITICAL_SUSPENSION_RAISED`, `SR1`, `SR2` |
| SAFE_RETURN_VERIFY_VALVE_CLOSED | `ValveLimitObserved` CLOSED (observed after SR2) | — | SAFE_RETURN_VERIFY_STANDBY | SR3 confirmed, SR4 axis-return requested in the same transition | `VALVE_FEEDBACK_OBSERVED`, `SR3`, `SR4` |
| SAFE_RETURN_VERIFY_VALVE_CLOSED | `ValveLimitObserved` OPEN or TRANSIT_OR_FAULT | — | unchanged (wait) | waits for timeout | `VALVE_NOT_CONFIRMED` |
| SAFE_RETURN_VERIFY_VALVE_CLOSED | `ValveLimitObserved` INVALID_LIMIT_STATE | — | SAFE_RETURN_FAILED | none | `VALVE_FEEDBACK_OBSERVED`, `SR_FAILED` (`VALVE_INVALID_LIMIT_STATE`, RECOVERY_REQUIRED) |
| SAFE_RETURN_VERIFY_VALVE_CLOSED | `FeedbackTimeoutExpired(VALVE_CLOSED)` | pending wait | SAFE_RETURN_FAILED | none | `SR_FAILED` (`VALVE_CLOSE_NOT_CONFIRMED`, RECOVERY_REQUIRED) |
| SAFE_RETURN_VERIFY_STANDBY | `AxisFeedbackObserved` AT_STANDBY | — | released (Job cleared) | SR5, SR6 (outcome recorded), SR7 (release) in one transition | `SR5`, `SR6`, `JOB_RELEASED` |
| SAFE_RETURN_VERIFY_STANDBY | `AxisFeedbackObserved` NOT_AT_STANDBY or UNKNOWN | — | unchanged (wait) | none | `STANDBY_NOT_CONFIRMED` (NO_OP) |
| SAFE_RETURN_VERIFY_STANDBY | `AxisFeedbackObserved` FAULT | — | SAFE_RETURN_FAILED | none | `SR_FAILED` (`AXIS_FAULT`, RECOVERY_REQUIRED) |
| SAFE_RETURN_VERIFY_STANDBY | `FeedbackTimeoutExpired(AXIS_STANDBY)` | pending wait | SAFE_RETURN_FAILED | none | `SR_FAILED` (`AXIS_STANDBY_NOT_CONFIRMED`, RECOVERY_REQUIRED) |
| RUNNING | `AxisFeedbackObserved` (any) | not in VERIFY_STANDBY | unchanged | evidence only | `AXIS_FEEDBACK_RECORDED_NO_EFFECT` (NO_OP) |
| Any Safe Return lifecycle | `RequestAbort` | — | unchanged | not restarted | `ABORT_NOTED_DURING_SAFE_RETURN` (NO_OP) |
| Any Safe Return lifecycle | `ReportExecutionFailure` | — | unchanged | not restarted | `EXECUTION_FAILURE_NOTED_DURING_SAFE_RETURN` (NO_OP) |
| Any Safe Return lifecycle | `ObservePumpState` critical | — | unchanged steps; latch set | latch only | `CRITICAL_EVENT_DURING_SAFE_RETURN` |
| SAFE_RETURN_FAILED | any valve or axis feedback | — | unchanged | evidence only | NO_OP |
| SAFE_RETURN_FAILED | `FeedbackTimeoutExpired` | — | unchanged | — | REFUSED `FEEDBACK_TIMEOUT_NOT_PENDING` |
| Any Job | `DispatchHead` | — | unchanged | — | REFUSED `DISPATCH_REFUSED_JOB_ACTIVE` |
| Any Job | `ObservePumpReadiness(false)` during CLEANING | — | unchanged | — | REFUSED `PUMP_STOP_REQUIRES_CLASSIFICATION` |

No transition has a timer, clock, delay or random value. The Safe Return from a critical event has no delay before SR2.

---

## 6. Safe Return transition table (strict order)

| Step | Owner order | Trigger | Lifecycle before → after | Intent (data only) | Evidence code | Effect |
| --- | --- | --- | --- | --- | --- | --- |
| SR1 | 1. cleaning activity false | Any terminal intent | RUNNING (or ABORTING) → SAFE_RETURN_CLOSE_VALVE | `WATER_OUTPUT_OFF` | `SR1` | cleaning and synthetic water off |
| SR2 | 2–3. synthetic water off, paired valve close requested | same transition, no delay | SAFE_RETURN_CLOSE_VALVE → SAFE_RETURN_VERIFY_VALVE_CLOSED | `VALVE_CLOSE` | `SR2` | valve close intent |
| SR3 | 4. paired valve closed confirmed | valve `CLOSED` observed after SR2 | SAFE_RETURN_VERIFY_VALVE_CLOSED → SAFE_RETURN_TO_STANDBY | — | `SR3` | valve confirmed; earlier readings are not reused |
| SR4 | 5. axis Standby return requested | same transition as SR3 | SAFE_RETURN_TO_STANDBY → SAFE_RETURN_VERIFY_STANDBY | `AXIS_TO_STANDBY` | `SR4` | axis return intent (never before SR3) |
| SR5 | 6. axis at Standby confirmed | `AT_STANDBY` observed after SR4 | SAFE_RETURN_VERIFY_STANDBY (same) | — | `SR5` | Standby confirmed |
| SR6 | 7. final outcome recorded | same transition as SR5 | (same) | — | `SR6` (JobOutcome = pending outcome) | outcome recorded only here |
| SR7 | 8. Active Job released | same transition as SR5 | → released | — | `JOB_RELEASED` | Job cleared; Queue unchanged; latch unchanged |
| SR_FAILED | failure | valve close or Standby not verified | → SAFE_RETURN_FAILED | — | failure reason, JobOutcome `RECOVERY_REQUIRED` | Job retained; no outcome; no release; no dispatch |

Ordering invariant (checked by test T11 and by the validator): water-off < valve-close request < valve-CLOSED confirmed < axis-return request < AT_STANDBY confirmed < outcome < release.

---

## 7. Pump classification table

| Observation | Critical? | Latch | Job RUNNING, not cleaning | Job CLEANING | Job in Safe Return | No Job |
| --- | --- | --- | --- | --- | --- | --- |
| `READY` | No | No | `PumpReady = true` | no change (NO_OP) | NO_OP (noted) | NO_OP (noted) |
| `EXPECTED_STOP` | No (class A) | No | `PumpReady = false` (APPLIED) | REFUSED `PUMP_EXPECTED_STOP_NOT_MODELLED` (O-3) | NO_OP (noted) | NO_OP (noted) |
| `UNEXPECTED_STOP` (class B) | Yes, High Critical | Set (same transition) | Safe Return: water off (SR1), valve close requested (SR2), pending `ABORTED` | same | latch set; steps not restarted (O-4) | latch set only |
| `TRIP` (class C) | Yes, High Critical | Set (same transition) | same as UNEXPECTED_STOP | same | same | latch set only |

Expected stop is not critical. It sets no latch and no High-severity condition. Pump run-state values (`PumpRunState`) are not kernel inputs (CLEANING_SEQUENCE §7).

---

## 8. Valve and Axis feedback tables

**Valve** (derived from the accepted matrix, CLEANING_SEQUENCE §5.2; the Job's own `ValveId` only):

| Upper | Lower | Derived state | RUNNING | After close requested (SR2) | In Safe Return after SR3 |
| --- | --- | --- | --- | --- | --- |
| 0 | 1 | `CLOSED` | Recorded; enables PREPARING → READY and P1 | Satisfies SR3 (fresh observation only) | No effect |
| 1 | 0 | `OPEN` | Recorded; enables P2 | Does not satisfy SR3; waits for timeout (O-6) | No effect |
| 0 | 0 | `TRANSIT_OR_FAULT` | Recorded | Waits for timeout | No effect |
| 1 | 1 | `INVALID_LIMIT_STATE` | Immediate execution failure → Safe Return | Immediate SAFE_RETURN_FAILED | No effect |

A `ValveId` that does not match the Job's valve is refused (`VALVE_ID_MISMATCH`), with evidence only.

**Axis** (abstract only; no coordinate, distance, speed, homing or motion value exists):

| Feedback | Effect in VERIFY_STANDBY | Effect elsewhere |
| --- | --- | --- |
| `AT_STANDBY` | SR5, SR6, SR7 (release) | NO_OP (never releases a Job that has not passed SR3) |
| `NOT_AT_STANDBY` | Waits (NO_OP) until the timeout input | NO_OP |
| `UNKNOWN` | Waits (NO_OP) until the timeout input | NO_OP |
| `FAULT` | Immediate SAFE_RETURN_FAILED | NO_OP |

---

## 9. Outcome timing

- No outcome is recorded before SR5 (`AT_STANDBY` confirmed) on the success path. The outcome is recorded at SR6, in the same transition as SR5 and SR7.
- The pending outcome is fixed at the trigger: normal completion `COMPLETED`; abort `ABORTED`; execution failure `FAILED`; Pump UNEXPECTED_STOP or TRIP `ABORTED`.
- Safe Return failure records `RECOVERY_REQUIRED` as evidence only (SR_FAILED). No final outcome is written, and no release occurs.
- A pending outcome is never `RECOVERY_REQUIRED`, and the validator rejects any mismatch between trigger and pending outcome.

---

## 10. Evidence-sequence design

- **One sequence.** `EvidenceSeq` is the single Kernel evidence counter for the Queue, the Job and Safe Return. The external `SafeReturnReleaseEvidence` sequence is removed (O-10). No second sequence space exists.
- **Multiple records per transition.** A Safe Return transition may emit several records (for example, a critical event emits `CRITICAL_SUSPENSION_RAISED`, `SR1` and `SR2`). Each record takes the next sequence number, so the stream is strictly increasing with no gaps.
- **Record fields.** Each record carries: `Seq`, `At`, `EventKind`, `Outcome`, `Code`, `EntryId`, `JobId`, `QueueRevision` (read from the committed state; the Queue is not mutated), `JetId`, `ValveId`, `LifecycleBefore`, `LifecycleAfter`, `Phase`, `Step`, `ValveFeedback`, `AxisFeedback`, `Pump`, `JobOutcome`, `Intent`.
- **Determinism.** Identical initial state and identical events produce byte-identical state and evidence (test T32, plus CP-1 Q13).
- **Refusals and no-ops** advance only the evidence sequence. Queue, Job, Mode and latch are unchanged.

---

## 11. Validator (CP-2 combinations)

In addition to every CP-1 check, the validator now rejects:

- undefined lifecycle, stage, phase, valve feedback, pending outcome, step or trigger values; blank trigger reason or failure code;
- a RUNNING Job that carries Safe Return data; a cleaning, water or stage mismatch; a READY_TO_CLEAN or CLEANING Job without a closed or observed valve; a CLEANING Job without a ready pump; an INVALID valve reading stored in RUNNING;
- a Safe Return Job with water on, incomplete trigger data, a trigger/outcome mismatch, a trigger-reason mismatch, a missing critical latch for a Pump trigger, an incomplete or out-of-order ledger, a step that does not match the lifecycle, a transient lifecycle stored as state, or an inconsistent failure code (valve-class versus axis-class).

---

## 12. CP-1 invariant preservation

| CP-1 protection | CP-2 status |
| --- | --- |
| Non-public `SequencingState` construction | Preserved (constructor unchanged) |
| Internal mutation boundary | Preserved |
| Copied and read-only Queue storage | Preserved |
| Deterministic state validator | Preserved and extended |
| Invalid Apply fails closed | Preserved (REFUSED `SEQUENCING_STATE_INVALID`) |
| Invalid projection rejected | Preserved (InvalidOperationException) |
| No default-as-running | Preserved (every Job field explicit at dispatch) |
| Checked counters | Preserved (`NextCounter` throws at the boundary) |
| Ready-only FIFO, head-only dispatch, no scan-forward | Preserved (unchanged dispatch code) |
| Single Active Job | Preserved (dispatch refused while a Job exists, including during Safe Return and after failure) |
| Pump waiting outside Queue entries | Preserved (the pump gate is a Job flag) |
| Critical freeze of queue and revision | Preserved (admission and dispatch refused while latched) |

**Deviations from CP-1 behaviour (intentional, Owner-scope):**

- D-1: `ReleaseActiveJob` (external completion evidence) is removed. CP-1 tests that released with synthetic evidence were rewritten to drive the full kernel Safe Return path.
- D-2: `RaiseCriticalSuspension` is removed. A critical event enters through `ObservePumpState(UNEXPECTED_STOP | TRIP)`. Without a Job it sets the same latch, as the CP-1 Q7 test requires.
- D-3: Pump readiness during CLEANING is classified rather than gated (`ObservePumpReadiness(false)` is refused while cleaning).

---

## 13. Owner rulings applied and implementation decisions

See `docs/MASTER_PLAN.md` §3.3.2 for the full table. Summary:

- **Applied as ruled:** O-1 (`ABORTED`), O-2 (RECOVERY_REQUIRED evidence, SAFE_RETURN_FAILED, Job retained, SR5 approved point), O-5 (timeouts are inputs), O-6 (OPEN waits, INVALID fails, ValveId mismatch refused), O-7 (axis synthetic), O-9 (phase path with valve gates, no motion values), O-10 (external release removed), O-12 (abort is a synthetic internal input), O-13, O-14, O-15 (no reset), O-16 (in-memory), O-17 (satisfied).
- **Implementation decisions flagged for Owner confirmation:** O-3 (EXPECTED_STOP refused while cleaning), O-4 (critical during Safe Return sets the latch and is recorded without restarting steps), O-8 (intents are data in evidence only, no adapter binding).
- **Open:** O-11 (mid-cleaning pump not-ready is refused, not held; no recovery path exists in CP-2 by design, so a Job that loses its pump mid-cleaning can only leave through a Pump critical event or Safe Return via abort or execution failure).

---

## 14. Tests implemented

**New file:** `tests/runtime.tests/Sequencing/SequencingJobSafeReturnTests.cs` — 47 facts (no theories).

Mapping of the required scenario list (§14 of the brief):

| # | Requirement | Test(s) |
| --- | --- | --- |
| 1 | Second Job cannot start while one is active | T01 |
| 2 | Normal completion enters Safe Return | T02 |
| 3 | Abort enters Safe Return | T03 |
| 4 | Execution failure enters Safe Return | T04 |
| 5 | UNEXPECTED_STOP disables water immediately | T05 |
| 6 | TRIP disables water immediately | T06 |
| 7 | Pump critical sets CRITICAL_SUSPENDED | T07 |
| 8 | Critical requests paired valve close in the same transition | T08 |
| 9 | Valve close request precedes Axis return | T09 |
| 10 | Axis return cannot be requested before valve closed confirmation | T10 |
| 11 | Safe Return evidence sequence strictly increases | T11 |
| 12 | Final outcome absent before valve and axis confirmations | T12 |
| 13 | Active Job retained until verified Safe Return | T13 |
| 14 | Next dispatch impossible before release | T14 |
| 15 | Normal Safe Return produces COMPLETED | T15 |
| 16 | Abort Safe Return produces ABORTED | T16 |
| 17 | Execution-failure Safe Return produces FAILED | T17 |
| 18 | Pump-critical Safe Return produces ABORTED | T18 |
| 19 | Valve-close failure produces SAFE_RETURN_FAILED | T19 (timeout), T19b (INVALID), T19c (INVALID during cleaning) |
| 20 | Axis-Standby failure produces SAFE_RETURN_FAILED | T20 (FAULT), T20b (timeout) |
| 21 | Safe Return failure carries RECOVERY_REQUIRED evidence | T19 |
| 22 | Safe Return failure retains Active Job | T22 |
| 23 | Safe Return failure preserves Queue and Queue revision | T19 (asserts queue JSON and revision) |
| 24 | No automatic retry | T24 |
| 25 | No automatic reset | T25 |
| 26 | No automatic resume | T26 |
| 27 | Critical Job release does not clear latch | T27 |
| 28 | Expected Pump stop is not critical | T28; `Expected_Pump_Stop_While_Cleaning_Is_Refused_And_Not_Critical` |
| 29 | WJn remains paired with IVn | T29 |
| 30 | Invalid WJ/IV pairing refused | T30 (admission); T29 (valve identity mismatch) |
| 31 | External completion evidence cannot skip Safe Return | T31 |
| 32 | Identical inputs produce byte-identical state and evidence | T32 |
| 33 | Existing CP-1 Q1–Q13 still pass | Existing `SequencingKernelTests.cs`. Q7 uses the classified-pump helper; Q13 now runs a full Job cycle. CP-1 behaviour is unchanged for each Q case. |
| 34 | CP-1 integrity tests still pass | Existing `SequencingStateIntegrityTests.cs`. The Job builder gained the CP-2 fields; the critical helper uses `ObservePumpState`; the release helper is removed. Assertions unchanged. |
| 35 | Boundary scan remains clean | Not a test (by brief rule). Verified by `tools/boundary-scan/boundary-scan.mjs .`: 0 findings. |
| 36 | No POST/write/API/UI/control path added | Not a test (by brief rule). Verified by review and boundary scan. No route, endpoint, adapter or UI file was added or changed. |

Additional behaviour tests: T07–T08 (latch and valve request), T12 (release cannot be reached without SR5), `Timeouts_Are_Only_Honoured_For_A_Pending_Wait`, T29b and `Second_Critical_Event…` (critical during Safe Return), `Phases_Are_Strictly_Ordered_And_Gated_By_The_Valve`, `Cleaning_Requires_A_Ready_Pump_And_A_Closed_Valve`, and eight validator tests (`Validator_Rejects_…`).

**Existing test changes (CP-1 adjusted to compile against the changed kernel API):** `SequencingKernelTests.cs` (release test rewritten through the full Safe Return path; Q13 script runs a full cycle; `Step` logs every record), `SequencingStateIntegrityTests.cs` (Job builder fields; critical helper; release helper removed; one projection scenario updated).

---

## 15. Changed / Unchanged / Not Verified

**Changed:** the Sequencing kernel, state, event, evidence, transition, codes and validator (`packages/application/Runtime/Sequencing/**`); CP-1 sequencing tests adjusted (`tests/runtime.tests/Sequencing/**`); one new test file; `docs/MASTER_PLAN.md` §3.3.2; `CHANGELOG.md`; `docs/CURRENT_STATE.md`; this report.

**Unchanged:** `packages/contracts/**` (no change, no blocker); fixtures; TypeScript mirror; `apps/**`; `adapters/**`; `config/**`; `tools/**`; project and lock files; Runtime host, API, Inspector, UI, command and route code; `SequencingState` constructor and properties; `SequencingTopology`; the Queue capacity (8) and admission rules.

**Not verified in Arena:**

- Compilation (no .NET SDK in the Arena sandbox; the .NET package endpoints are not reachable).
- Test execution, analyzer results under `AnalysisLevel latest-minimum` with `TreatWarningsAsErrors`.
- Count of the full .NET suite.

Verification performed in Arena: C# syntax parse of all 12 changed or new sequencing files with a tree-sitter C# grammar (a known grammar limitation on trailing commas inside multi-line `with` initializers was handled by a temporary normalized copy, not by changing any file; the unmodified CP-1 kernel shows the same limitation); cross-reference check of every `SequencingCodes` member and every contract/kernel enum member used; manual review of the transition paths against the tests; `tools/boundary-scan`: 0 findings; `git diff --check`: clean.

---

## 16. Known limitations and open items

- O-11 is open (see §13).
- Valve readings during Safe Return after SR5 (VERIFY_STANDBY) are recorded as no-effect evidence. No cross-check between the valve and axis is made after release.
- The stored state never carries `SAFE_RETURN_CLOSE_VALVE`, `SAFE_RETURN_TO_STANDBY` or `ABORTING`. They appear only as `LifecycleAfter`/`LifecycleBefore` evidence values, because the same transition continues to the next step. The validator rejects them as stored state.
- The Job's `PendingOutcome` is internal and is not projected to the wire (`JobOutcomeRecord` remains a CP-3/CP-4 projection target).
- No persistence; in-memory only (O-16).

---

## 17. Owner-local validation commands

Run from the repository root on the Owner workstation (the `.NET 10.0.401` SDK pinned by `global.json`):

```text
git ls-remote origin refs/heads/main            # expect 8323f78c7ec2480bb30cbdd1432b6caa8e32c601
git log --oneline 8323f78..HEAD                 # expect ef51bd8, 6b94508, ffc2701, then the docs commit
git diff --check 8323f78 HEAD                   # expect no output
dotnet restore WaterJetSentinelSuite.sln --locked-mode   # no lock drift
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore   # expect 0 warnings, 0 errors
dotnet test WaterJetSentinelSuite.sln -c Release --no-build      # report the exact total and any failures
node tools/boundary-scan/boundary-scan.mjs .    # expect 0 findings
```

**Expected test total:** the CP-1 baseline (253, Owner-reported at `03144f4`) plus 47 new CP-2 facts = **300**, if the baseline is unchanged. This is an expectation, not an observed result. Owner-local output is authoritative.

---

## 18. Honesty statement and final status

**SOURCE AUTHORED. STATICALLY REVIEWED. NOT COMPILED IN ARENA. NOT EXECUTED IN ARENA. OWNER-LOCAL VALIDATION REQUIRED.**

- CP-2 OWNER-AUTHORIZED
- CLEANING JOB KERNEL DELIVERED
- MANDATORY SAFE RETURN ORDERING DELIVERED
- PUMP CRITICAL LATCH DELIVERED
- VALVE CLOSE PRECEDES AXIS RETURN
- FINAL OUTCOME FOLLOWS SAFE RETURN
- SAFE RETURN FAILURE RETAINS ACTIVE JOB
- CRITICAL LATCH PREVENTS NEXT DISPATCH
- SINGLE EVIDENCE SEQUENCE RETAINED
- CP-1 INTEGRITY BOUNDARY RETAINED
- NO RUNTIME HOST INTEGRATION
- NO API OR INSPECTOR CHANGE
- CP-3 NOT AUTHORIZED
- CP-4 NOT AUTHORIZED
- TEST_HARDWARE NOT AUTHORIZED
- PRODUCTION DEVICE ACCESS NOT AUTHORIZED
- PR OPEN - NOT MERGED
