# Test Strategy — WaterJet Sentinel Suite (WJSS)

**Document status:** [PROPOSED] as a strategy. The statements in section 2 about what has
and has not been tested are **facts**, not proposals.

> **No runtime testing has occurred.** There is no application code, no build, no database
> schema, and no device connection in this repository. Nothing in this project has been
> executed. Any statement implying otherwise is false.

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

## 3. Verification levels planned

Each level is `[PROPOSED]` and depends on a future Stage Gate.

### 3.1 Pure domain logic tests

Highest value, lowest cost, no hardware. Queue arbitration, DirtyScore computation,
deduplication, refill, sorting ties, timestamp arithmetic, and alarm blocking release are
all pure functions of data plus configuration and must be covered exhaustively.

Required cases, at minimum:

| Area | Cases |
| --- | --- |
| DirtyScore | Below lower bound, above upper bound, mid-range linearity, clamping, invalid bound rejection, `TC_F <= TC_R` diagnostic without eligibility removal |
| TempQueue | Entry dwell continuity, dwell reset on lost condition, removal dwell, re-dirty during removal, sorting by score then scanOrder |
| TimeQueue | Interval boundary exactly equal to the interval, ordering by elapsed time then scanOrder |
| GlobalQueue seeding | Fixed seed order, deduplication with earliest position preserved, reasons merged, duplicate source advancing to the next candidate, underfull queue |
| Refill | Shift forward preserving FIFO, refill from original source owner, empty tail when no candidate exists, refill after Reorder |
| Operator actions | Hold skip without reordering, Release Hold, Reject suppression scope, suppression end at new Auto Sequence, Reorder without ownership change |
| Alarm model | Blocking release only on cleared + acknowledged + no other block; acknowledgement of active alarm does not release; cleared-but-unacknowledged presentation string |
| Timestamps | No null, no `NEVER_CLEANED`, source transitions after a successful job only |
| Determinism | Identical inputs produce identical ordering across repeated runs and across randomized input ordering |

### 3.2 Simulator-backed integration tests

Adds a Modbus TCP simulator and a motion simulator. Verifies acquisition decoding, quality
handling, timeout behaviour, reconnection behaviour, and sequence execution without any
physical device.

Critical scenarios: disconnect mid-job, stale data after reconnection, partial feedback,
valve timeout during a job, pressure loss during a job.

### 3.3 Persistence tests

Against a test database instance only. Verifies capture at the configured intervals,
retention cutoff calculation per category, cleanup logging, and audit record completeness.

### 3.4 Interface tests

Verifies kiosk layout at the workstation resolution, responsive layouts on applicable
pages, alarm presentation strings, queue action dialogs, and permission gating. Interface
testing does not verify safe control behaviour.

### 3.5 Bench verification

Physical hardware, separately authorised, with a written method statement. See
[`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) section 8. This level is `[NOT AUTHORIZED]` at
present and its outcomes are `[NOT VERIFIED]`.

## 4. Testability requirements for future design

To keep the above achievable, the future implementation must:

1. Keep domain logic free of transport, storage, and UI dependencies.
2. Inject a clock rather than reading system time directly, so interval and dwell behaviour
   is testable deterministically.
3. Keep queue ordering a pure function of inputs plus configuration.
4. Model equipment state as explicit state machines with defined responses to lost
   communication, stale values, and partial feedback.
5. Make configuration validation a distinct, testable step that blocks publication.
6. Never substitute a default value silently for bad quality data.

## 5. Documentation validation (performed at documentation stages)

Documentation stages validate documentation. The expected checks are:

1. List every changed file.
2. Check internal Markdown links resolve where practical.
3. Search for prohibited content: production IP addresses, credentials, connection strings.
4. Confirm no null/`NEVER_CLEANED` handling for `LastSuccessfulCleaningCompletedAt` remains.
5. Confirm no claim that WAGO fail-safe has passed.
6. Confirm no claim that production device access is authorised.
7. Confirm no application code, package manifest, or dependency was created.
8. Review the full diff before committing.

The result of these checks for the current baseline is recorded in
[`CURRENT_STATE.md`](CURRENT_STATE.md).

## 6. What is explicitly not claimed

- No certification or compliance with IEC, ISA, ISO, or any other standard.
- No safety integrity level, category, or performance level.
- No claim that the system can protect equipment or personnel.
- No claim that any behaviour has been verified on hardware.

## 7. Open items

| Item | Status |
| --- | --- |
| Target coverage thresholds and what must be covered before a stage gate closes | `[OPEN]` |
| Test execution tooling and CI. Continuous integration is **not authorised** at the current stage | `[OPEN]` / `[NOT AUTHORIZED]` |
| Simulator fidelity requirements and who validates them | `[OPEN]` |
| Whether a test database is separate from the deployment database, and how it is provisioned | `[OPEN]` |
| Acceptance criteria for deployment acceptance | `[OPEN]` |
| Bench test method statement authorship and witnessing | `[OPEN]` |

---

## Related documents

- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — bench verification requirements
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — behaviour under test
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — sequence under test
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking rules under test
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — validation record
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and evidence rules
