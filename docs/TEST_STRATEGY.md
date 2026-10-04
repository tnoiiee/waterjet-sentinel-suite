# Test Strategy — WaterJet Sentinel Suite (WJSS)

**Document status:** [PROPOSED] as a strategy. The statements in section 2 about what has
and has not been tested are **facts**, not proposals.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; implementation submitted for Owner
review; documentation review changes requested / in progress; Stage 0.2 `[NOT AUTHORIZED]`.

> **No runtime testing has occurred.** There is no application code, no build, no database
> schema, and no device connection in this repository. Nothing in this project has been
> executed. Any statement implying otherwise is false. Every case listed in this document
> is **planned, not executed**.

---

## 1. Position

This document describes *how* verification will be approached once code and hardware work
are authorised. It does not authorise any test that has not been approved by a Stage Gate,
and it does not describe testing that has occurred.

Documentation stages perform documentation-only validation. Runtime, build, database,
hardware, and device tests are not part of a documentation stage.

## 2. What has been verified so far

| Claim | State |
| --- | --- |
| Application builds | Not applicable — no code exists |
| Unit tests pass | Not applicable — no tests exist |
| Integration tests pass | Not applicable — no integration exists |
| Database schema validated | Not applicable — no schema exists |
| Hardware fail-safe verified | **`[NOT VERIFIED]`** — bench testing not authorised and not performed |
| WAGO watchdog behaviour verified | **`[NOT VERIFIED]`** |
| Motion limits established | **`[NOT VERIFIED]`** |
| Encoder mode behaviour established | **`[NOT VERIFIED]`** |
| Pressure setpoints established | **`[NOT VERIFIED]`** |
| Production register map established | Not created and must never be committed |
| Historian capacity model established | **`[NOT VERIFIED]`** — no capacity model exists |

## 3. Verification levels planned

Each level is `[PROPOSED]` and depends on a future Stage Gate.

### 3.1 Pure domain logic tests

Highest value, lowest cost, no hardware. Queue arbitration, DirtyScore computation,
deduplication, source ownership, refill, sorting ties, sequential-execution invariants,
timestamp arithmetic, alarm acknowledgement and release, and communication-health evaluation
are all pure functions of data plus configuration and must be covered exhaustively.

| Area | Cases |
| --- | --- |
| DirtyScore | Below lower bound, above upper bound, mid-range linearity, clamping, invalid bound rejection, `TC_F <= TC_R` diagnostic without eligibility removal |
| TempQueue | Entry dwell continuity, dwell reset on lost condition, removal dwell, re-dirty during removal, sorting by score then scanOrder **within a wall** |
| TimeQueue | Interval boundary exactly equal to the interval, ordering by elapsed time then scanOrder |
| GlobalQueue seeding | Fixed eight-source order, four TempQueues plus four TimeQueues present, deduplication with earliest position preserved, reasons merged, duplicate source advancing to its next candidate, underfull queue |
| Source ownership | Owner is the source that established the preserved earliest position; merged reasons do not transfer ownership; refill after Reorder uses the original owner |
| Refill | Shift forward preserving FIFO, refill from original source owner, empty tail when no candidate exists |
| Operator actions | Hold skip without reordering, Release Hold, Reject suppression scope, suppression end at new Auto Sequence, Reorder without ownership change |
| Alarm model | Active awareness acknowledgement does not release a block; cleared-state acknowledgement is required; the prohibited auto-clear sequence cannot be constructed; cleared-but-unacknowledged presentation string |
| Timestamps | No null, no `NEVER_CLEANED`, source transitions after a successful job only |
| Determinism | Identical inputs produce identical ordering across repeated runs and across randomized input ordering |

### 3.2 Required planned cases for the review-confirmed behaviours

These cases are planned, not executed. Each maps to an Owner-confirmed requirement.

| # | Planned case | Maps to |
| --- | --- | --- |
| 1 | A second Cleaning Job cannot start while one is active | SEQ-001, SEQ-002 |
| 2 | Different Water Jets cannot execute Cleaning Jobs concurrently | SEQ-003, SEQ-006 |
| 3 | Queue Reorder cannot create concurrency | SEQ-005, QUE-022 |
| 4 | Valve exclusion cannot create concurrency, and "continue" means sequential continuation on another Water Jet | SEQ-005, SEQ-009, VLV-009 |
| 5 | AutoSequence restart rebuilds GlobalQueue from current data, recalculating DirtyScore, TimeSinceLastClean, and HardMinimumCleaningInterval | QUE-025 |
| 6 | The previous GlobalQueue is retained only as Event history and is never reloaded as executable state | QUE-024, QUE-026 |
| 7 | An active awareness acknowledgement does not release an alarm block | ALM-003, ALM-005 |
| 8 | A cleared alarm requires cleared-state acknowledgement before the block releases, and cannot self-acknowledge | ALM-004, ALM-005, ALM-006 |
| 9 | A DCS communication failure during an active Job blocks the next Job while allowing the current Job to reach its approved terminal condition | COMH-007 |
| 10 | A static process value alone does not indicate communication loss | COMH-003 |
| 11 | The DCS Permissive Override cannot bypass safety and equipment gates, including communication health, valve verification, pressure validation, motion faults, the watchdog, and the one-active-job invariant | OVR-008, OVR-009, OVR-010 |
| 12 | Normal UI close is blocked while a Cleaning Job is active | UIG-001 |
| 13 | Normal UI close is blocked while the Main Pump is running | UIG-002 |
| 14 | The UI close guard is not treated as hardware fail-safe, and its limits are documented where it is implemented | UIG-005, UIG-006 |
| 15 | The Water Jet to Isolation Valve mapping remains one-to-one, and a sensor's valve is derived from its assigned Water Jet | WJV-001, WJV-004 |
| 16 | Operator stop records the Queue snapshot and the Held/Rejected/Reordered state before the instance closes | QUE-024 |
| 17 | A blocking DCS condition while idle stops the countdown, prevents dispatch, and requires cleared-state acknowledgement before the countdown resumes | COMH-006 |
| 18 | Main Pump stop is not blocked by application-level operational permissives | PMP-007 |

#### 3.2.1 Planned DCS Permissive Override permission cases

Status of every case in this group: **PLANNED — NOT EXECUTED.** They are documentation-level
and design-level verification cases only. No test code exists, and none may be created at
this stage.

| # | Planned case | Expected result | Maps to |
| --- | --- | --- | --- |
| 19 | Operator role template can activate the DCS Permissive Override | Activation permitted | OVR-011 |
| 20 | Operator role template can release the DCS Permissive Override | Release permitted | OVR-011 |
| 21 | A role without the Override permission cannot activate it | Activation refused and recorded | OVR-012 |
| 22 | A role without the Override permission cannot release it | Release refused and recorded | OVR-012 |
| 23 | Override activation requires explicit confirmation and a reason | Activation incomplete until both are supplied | OVR-003, OVR-004 |
| 24 | Override activation and release create Event and Audit records | Both records present, with user, timestamp, reason, and event type | OVR-006, OVR-014 |
| 25 | The persistent Override banner remains visible while the override is active | Banner visible for the whole active period, including after screen changes | OVR-005 |
| 26 | The DCS Permissive Override cannot bypass any item in the exclusion list | Every exclusion in OVR-009 still blocks, including communication health, valve verification, pressure validation, motion faults, the watchdog, external protection, alarm release requirements, and the one-active-Cleaning-Job invariant | OVR-008, OVR-009, OVR-010, OVR-013 |
| 27 | The DCS Permissive Override cannot create concurrent Cleaning Jobs | No second Cleaning Job enters an executing state while the override is active | SEQ-001, SEQ-005, SEQ-006, OVR-009 |
| 28 | Permission configurability does not change the approved baseline assignment without an explicit configuration change | Baseline remains Operator until a configuration change is made and recorded | OVR-011, OVR-015 |

### 3.3 Simulator-backed integration tests

Adds a Modbus TCP simulator and a motion simulator. Verifies acquisition decoding, quality
handling, timeout behaviour, reconnection behaviour, and sequence execution without any
physical device.

Critical scenarios: disconnect mid-job, stale data after reconnection, partial feedback,
valve timeout during a job, pressure loss during a job, static-value false reassurance,
exception responses, accumulating consecutive failures, and poll-cycle overrun.

### 3.4 Persistence tests

Against a test database instance only. Verifies capture at the configured intervals,
retention cutoff calculation per category, cleanup logging, and audit record completeness
including DCS override records.

### 3.5 Interface tests

Verifies kiosk layout at the workstation resolution, responsive layouts on applicable
pages, alarm presentation strings, the distinct presentation of a cleared alarm whose
cleared-state acknowledgement is pending, queue action dialogs, override banner and
confirmation behaviour, close-guard rejection messaging, and permission gating. Interface
testing does not verify safe control behaviour.

### 3.6 Bench verification

Physical hardware, separately authorised, with a written method statement. See
[`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) section 8. This level is `[NOT AUTHORIZED]` at
present and its outcomes are `[NOT VERIFIED]`.

## 4. Testability requirements for future design

To keep the above achievable, the future implementation must:

1. Keep domain logic free of transport, storage, and UI dependencies.
2. Inject a clock rather than reading system time directly, so interval and dwell behaviour
   is testable deterministically.
3. Keep queue ordering and source ownership pure functions of inputs plus configuration.
4. Model equipment state as explicit state machines with defined responses to lost
   communication, stale values, and partial feedback.
5. Make configuration validation a distinct, testable step that blocks publication.
6. Never substitute a default value silently for bad quality data.
7. Enforce the one-active-Cleaning-Job invariant in a single, testable gate that no queue
   action or operator action can bypass.
8. Make the DCS Permissive Override a scoped, enumerated bypass rather than a general flag,
   so that its exclusions are directly testable.

## 5. Documentation validation (performed at documentation stages)

Documentation stages validate documentation. The expected checks are:

1. List every changed file.
2. Check internal Markdown links resolve where practical.
3. Search for prohibited content: production IP addresses, credentials, connection strings.
4. Confirm no null/`NEVER_CLEANED` handling for `LastSuccessfulCleaningCompletedAt` remains.
5. Confirm no claim that WAGO fail-safe has passed.
6. Confirm no claim that production device access is authorised.
7. Confirm no remaining statement describes Cleaning Job concurrency or parallel Water Jet
   cleaning as `[OPEN]`.
8. Confirm no remaining statement describes Water Jet to Isolation Valve cardinality as
   `[OPEN]`.
9. Confirm no remaining statement allows a block to release without cleared-state
   acknowledgement.
10. Confirm no application code, package manifest, or dependency was created.
11. Review the full diff before committing.

The result of these checks for the current baseline is recorded in
[`CURRENT_STATE.md`](CURRENT_STATE.md).

## 6. What is explicitly not claimed

- No certification or compliance with IEC, ISA, ISO, or any other standard.
- No safety integrity level, category, or performance level.
- No claim that the system can protect equipment or personnel.
- No claim that any behaviour has been verified on hardware.
- No storage benchmark, capacity measurement, or performance measurement.

## 7. Open items

| Item | Status |
| --- | --- |
| Target coverage thresholds and what must be covered before a stage gate closes | `[OPEN]` |
| Test execution tooling and CI. Continuous integration is **not authorised** at the current stage | `[OPEN]` / `[NOT AUTHORIZED]` |
| Simulator fidelity requirements and who validates them | `[OPEN]` |
| Whether a test database is separate from the deployment database, and how it is provisioned | `[OPEN]` |
| Acceptance criteria for deployment acceptance | `[OPEN]` |
| Bench test method statement authorship and witnessing | `[OPEN]` |
| How the DCS Permissive Override is fault-injected in simulator testing | `[OPEN]` |

---

## Related documents

- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — bench verification requirements
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queues, source ownership, stop/rebuild
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — sequencing invariants and job execution
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — cleared-state acknowledgement rules
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — override scope and authority
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — validation record
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and evidence rules
