# Test Strategy — WaterJet Sentinel Suite (WJSS)

**Document status:** [PROPOSED] as a strategy. The statements in section 2 about what has
and has not been tested are **facts**, not proposals.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**COMPLETE FOR DEVELOPMENT CHECKPOINT** (Owner-local final Edge gate **PASS**, Owner manual
review **PASS**), PR #3 **OPEN — READY FOR OWNER MERGE**, **NOT MERGED**. **React selected as the
Primary UI Framework** (Owner decision, 2026-10-07; Production transport and chart library remain
`[OPEN]`). Blazor counter-spike **NOT REQUIRED** unless a future material blocker is identified.
Main Development Scope Gate **PENDING**. Stage 0.3 `[NOT AUTHORIZED]`. Production device access `[NOT AUTHORIZED]`.

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
| Operator actions | Head-only dispatch with no entry skipped (queue-level Hold superseded, Owner decision 2026-10-06 — [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2); Reject suppression scope, suppression end at new Auto Sequence, Reorder without ownership change (Reject / Reorder semantics in the ready-only queue are OWNER DECISION REQUIRED) |
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

### 3.7 Planned architecture-boundary verification (Stage 0.2 decisions)

Status of every case in this group: **PLANNED — NOT EXECUTED.** They are design-level
verification cases derived from the `[PROPOSED]` Stage 0.2 architecture decisions. No test
code exists, and none may be created at this stage. If the Owner does not accept a decision,
the corresponding cases must be revised rather than executed.

| # | Planned case | Maps to |
| --- | --- | --- |
| 29 | The UI process cannot open a device session and cannot write to hardware by any path | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md), ARC-001 to ARC-003 |
| 30 | The UI process cannot connect to the database and holds no database credential | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md), ARC-020, ARC-023 |
| 31 | Domain logic has no direct dependency on any vendor device library | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md), ARC-007 |
| 32 | Simulator adapters satisfy the same contract tests as the physical adapters (parity) | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md), ARC-008 |
| 33 | A second runtime service instance refuses to start | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) |
| 34 | After a restart the runtime issues no command until state and position knowledge are re-established, and never replays a pre-restart command | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md), ARC-026 |
| 35 | A physical profile whose adapter cannot be established fails closed and does not substitute simulated values | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md), ARC-010 |
| 36 | Physical adapters are not constructed under the default profile | [`ADR-0012`](decisions/ADR-0012-simulator-first-development.md), ARC-009, ARC-016 |
| 37 | Publication of an invalid configuration is refused and leaves the previous published revision in force | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md), ARC-015 |
| 38 | An edited Draft is never consumed by the runtime | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md), ARC-015 |
| 39 | A published revision is not applied while a Cleaning Job is active | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md) |
| 40 | With the database unavailable, live supervision and UI visibility continue and a stop action is not blocked | [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md), ARC-024 |
| 41 | With the database unavailable, an audit-required initiating action is refused and is not recorded as performed | [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md), ARC-025 |
| 42 | A migration is never applied automatically by a service start | [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) |
| 43 | The Local Application API is unreachable from any interface other than loopback, and rejects an unauthenticated control request | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md), ARC-023 |
| 44 | Termination of the operator interface session stops any manual hold-to-run operation and does not abort an active AutoSequence | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) item 9 |
| 45 | No configuration file, example file, or secret is present in the public repository | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md), ARC-013, ARC-014 |

These cases are planned design-level checks. None of them has been executed, none of them
constitutes hardware evidence, and passing them would not constitute certification.


### 3.8 Planned spike verification workload (proposed Stage 0.2.1)

> **SUPERSEDED route.** The Owner replaced the dual-candidate proposal with the React-first Stage 0.2.1A feasibility spike; React was subsequently selected as the Primary UI Framework (Owner decision, 2026-10-07); a Blazor counter-spike is not required unless a future material blocker is identified. Stage 0.2.1A evidence is recorded in
> [`spikes/stage-0.2.1a-results.md`](spikes/stage-0.2.1a-results.md).

Status of every case in this group: **PLANNED — NOT EXECUTED.** They belong to the proposed
Stage 0.2.1 spike, which is `PROPOSED` and `[NOT AUTHORIZED]`. Nothing here authorises writing
spike code, and no spike has been run. If the Owner does not authorise Stage 0.2.1, these cases
are not executed.

| # | Planned case | Maps to |
| --- | --- | --- |
| 46 | The UI candidate renders 106 live Sensor cells (with the two Cannon slots as neutral equipment) with 212 channels updating at a one-second cadence without interactive stall. The dual-candidate wording of Stage 0.2.1 is superseded by the React-first Stage 0.2.1A spike; a Blazor counter-spike requires a future Owner Scope Gate | UIW-001, [`Architecture §33`](ARCHITECTURE.md) |
| 47 | Only changed Sensor cells are recomputed and repainted per update | UIW-009, UIW-012 |
| 48 | Each Sensor cell independently expresses classification, Dirty Score intensity, quality, queue state, selection, execution state, and alarm state | SPC-001 |
| 49 | Dirty/Cleaner classification follows the published effective threshold, not a hard-coded value | SPC-002, SPC-003 |
| 50 | Bad, stale, uncertain, and disabled quality are visually distinct from Dirty and Cleaner, and no raw sentinel value reaches the presentation layer as an ordinary state | DQS-001, DQS-002, DQS-005 |
| 51 | Wall summary counts show labelled Dirty, Cleaner, and Bad or unavailable values | UIW-003 |
| 52 | A live trend runs on a bounded window with no series recreation, no unbounded growth, and visible data gaps | TRD-001, TRD-002, TRD-004, TRD-006 |
| 53 | A GlobalQueue preview, active Cleaning Job marker, alarm marker, alarm banner, and camera placeholder all update without blocking one another | UIW-004, UIW-005, UIW-006 |
| 54 | Memory usage is stable over extended operation in both candidates | UIW-008 |
| 55 | UI disconnect and reconnect, with a simulated one-second gap, results in a fresh authoritative snapshot and no inferred job continuation | LSD-006 |
| 56 | One simulated device timeout degrades only that device's data while unrelated devices keep updating | MDA-003 |
| 57 | Push payload size and UI update latency are measured and reported for both candidates | LSD-004, [`Architecture §33.3`](ARCHITECTURE.md) |
| 58 | Database-writer slowdown simulation does not stall acquisition, queue evaluation, alarm evaluation, live UI, or a Main Pump stop request | HDC-001 |
| 59 | Offline build and dependency restoration succeed for both candidates with no Internet access | [`ADR-0013`](decisions/ADR-0013-offline-deployment.md) |
| 60 | CPU average and peak, kiosk integration, package footprint, and package licensing are measured and reported for both candidates | [`Architecture §33.3`](ARCHITECTURE.md) |

These cases produce **application-level performance evidence about disposable spike code**.
They are not hardware tests, not bench evidence, not fail-safe evidence, and not certification.

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
9. Keep the presentation-state contract (snapshot, delta, sequence, timestamp, reconnect)
   testable without a UI, so that a UI can be replaced without re-testing runtime logic.
10. Keep the Poll Plan and its batching deterministic and inspectable, so that acquisition
    behaviour can be asserted without a device.
11. Keep trend buffering, downsampling, and gap representation in testable application code
    rather than inside a chart component.

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
12. Confirm no runtime artefact, database object, adapter, simulator, CI workflow, installer,
    deployment script, release package, or archive was created, and that no device or
    production system was contacted.

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
| Acceptance thresholds for the proposed Stage 0.2.1 spike (what counts as acceptable CPU, latency, memory growth, and payload size) | `[OPEN]` — must be set before the spike runs |
| Which candidate's spike artefacts, if any, may inform later implementation, and how they are discarded | `[OPEN]` |
| Whether spike code is ever reused, and the review required if it is | `[OPEN]` |
| Chart library, UI test tooling, and visual regression tooling | `[OPEN]` |

---

## Related documents

- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — bench verification requirements
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queues, source ownership, stop/rebuild
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — sequencing invariants and job execution
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — cleared-state acknowledgement rules
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — override scope and authority
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — validation record
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — legacy evidence (§23), UI framework candidates (§24),
  workload (§25), presentation model (§26), quality pipeline (§27), live-state delivery (§28),
  acquisition (§29), configuration hot path (§30), Historian decoupling (§31), trend (§32),
  proposed spike (§33)
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and evidence rules
