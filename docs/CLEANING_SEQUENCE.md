# Cleaning Sequence — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the sequence, its verification steps, its sequencing
invariants, and its failure boundaries. All coordinate and timing values are
`[NOT VERIFIED]` and none are invented here.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 architecture checkpoint **SUBMITTED FOR OWNER REVIEW**; documentation
review **CHANGES REQUESTED / IN PROGRESS**; Owner manual review **PENDING**; **NOT MERGED**;
Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]`.

This document specifies what a Cleaning Job is and how it is supervised. It contains no
implementation, and it authorises no device access.

---

## 1. Cleaning Job definition

One Cleaning Job represents **one sensor** `[APPROVED]`. Each sensor is assigned:

- One Water Jet
- One Isolation Valve
- Six configurable path coordinates (P1 through P6)
- One per-sensor `HardMinimumCleaningInterval`
- Its temperature and queue parameters

**Assignment derivation** `[OWNER CONFIRMED]`: the Water Jet to Isolation Valve relationship
is exactly one-to-one — Water Jet 1 to Isolation Valve 1, through Water Jet 8 to Isolation
Valve 8. Each Water Jet has one dedicated Isolation Valve and each Isolation Valve serves
exactly one Water Jet; valves are never shared. Multiple sensors may be assigned to one
Water Jet, and **a sensor's Isolation Valve is derived from that sensor's assigned Water
Jet**. See [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001 to WJV-006.

Coordinate values are commissioning values: they are `[NOT VERIFIED]` and must be captured
from engineering records. They are never committed to this repository.

## 2. Sequential execution invariants

AutoSequence must execute Cleaning Jobs **strictly sequentially** `[OWNER CONFIRMED]`.

| Invariant | Statement |
| --- | --- |
| INVARIANT-SEQ-001 | At most one Cleaning Job may be ACTIVE within one WaterJet Sentinel Suite installation at any time. |
| INVARIANT-SEQ-002 | A second Cleaning Job must not enter an executing state until the current Cleaning Job has reached an approved safe and released terminal condition. |
| INVARIANT-SEQ-003 | Different Water Jets, different Isolation Valves, different boiler walls, or different Galil controllers do not grant authority for concurrent Cleaning Jobs. |
| INVARIANT-SEQ-004 | The selected eligible head of GlobalQueue is the only normal source for the next Cleaning Job. |
| INVARIANT-SEQ-005 | Queue refill, score changes, Operator Reorder, Hold, Reject, valve exclusion, or equipment availability must never result in concurrent Cleaning Jobs. |
| INVARIANT-SEQ-006 | Parallel Water Jet cleaning is prohibited. |

**The Main Pump may remain running between Cleaning Jobs during an active AutoSequence, but
only one Water Jet may execute a Cleaning Job at a time.** `[OWNER CONFIRMED]`

The phrase "allow unaffected Water Jets to continue" describes **sequential** continuation
only. Its approved meaning is: after the valve fault workflow is resolved and continuation
is authorized, the AutoSequence may later select a sequential Cleaning Job assigned to
another available Water Jet. It must still execute only one Cleaning Job at a time. It must
never imply concurrent operation. `[OWNER CONFIRMED]`

The normal dispatch cycle `[OWNER CONFIRMED]`:

1. Select one eligible entry from the front of GlobalQueue.
2. Execute one complete Cleaning Job.
3. Confirm the valve is closed.
4. Return the Water Jet to Standby.
5. Confirm the Job reaches its approved terminal condition.
6. Remove or resolve the completed entry.
7. Shift the remaining FIFO entries.
8. Refill the tail using the approved source-owner rule.
9. Apply the next-job countdown and required gates.
10. Only then may the next Cleaning Job begin.

## 3. Normal sequence

The normal sequence is an approved nineteen-step order `[APPROVED]`:

| Step | Action | Verification / condition |
| --- | --- | --- |
| 1 | Select the GlobalQueue head | Head is not held; no blocking condition |
| 2 | Revalidate permissions, eligibility, equipment state, and permissives | All must pass; otherwise the job must not start. Includes the one-active-job invariant check |
| 3 | Reserve the assigned Water Jet | No other job may use it |
| 4 | Verify Main Pump pressure readiness | Pressure value and quality must indicate readiness |
| 5 | Verify Isolation Valve closed state | Derived state must be `CLOSED` |
| 6 | Verify Galil readiness and a known position | Position is known and the controller is ready |
| 7 | Move from Standby to P1 with the valve closed | Motion command issued with valve closed |
| 8 | Confirm P1 reached | Verified arrival, not merely command issued |
| 9 | Command Isolation Valve open | Energize-to-open |
| 10 | Confirm valve open feedback | Derived state must be `OPEN` within the configured opening timeout |
| 11 | Move continuously through P2, P3, P4, P5, and P6 | Continuous path motion |
| 12 | Command Isolation Valve closed at P6 | De-energize |
| 13 | Confirm valve closed feedback | Derived state must be `CLOSED` within the configured closing timeout |
| 14 | Return the Water Jet to Standby | Motion command issued |
| 15 | Confirm Standby position | Verified arrival |
| 16 | Mark the Job completed | Only after all prior verifications succeeded |
| 17 | Update `LastSuccessfulCleaningCompletedAt` | Sets source to `COMPLETED_JOB` and `HasVerifiedCleaningHistory = true` |
| 18 | Recalculate source queues | TempQueue and TimeQueue re-evaluated |
| 19 | Continue the next-job countdown if no blocking condition exists | Otherwise hold until the block is released |

Steps 4 through 15 all carry explicit verification. A command that is issued but not
verified is not a completed step.

## 4. Job states and outcomes

### 4.1 Execution states

| State | Meaning |
| --- | --- |
| `PENDING` | Selected from GlobalQueue, not yet started |
| `REVALIDATING` | Step 2 in progress |
| `RESERVED` | Water Jet reserved |
| `RUNNING` | Sequence steps executing |
| `COMPLETED` | All steps verified; timestamp updated |
| `FAILED` | A required verification failed; operator action needed |
| `RECOVERY_REQUIRED` | Equipment state is ambiguous or unsafe for continuation; operator action needed |
| `ABORTED` | Stopped by an operator or by a blocking response |

`COMPLETED`, `FAILED`, `RECOVERY_REQUIRED`, and `ABORTED` are terminal. Only `COMPLETED`
updates `LastSuccessfulCleaningCompletedAt`.

### 4.2 Job outcomes

Cleaning Job **outcomes** describe the result of an executed job: `COMPLETED`, `FAILED`,
`ABORTED`, `RECOVERY_REQUIRED`. They are distinct from **queue entry dispositions**, which
describe what happened to an entry in a queue: `HELD`, `RELEASED`, `REJECTED`, `REORDERED`,
`REMOVED_BY_ELIGIBILITY`, `REMOVED_BY_EQUIPMENT_EXCLUSION`.

Operator Reject is a queue action, not a Cleaning Job outcome. A job that never executes
because its entry was rejected produces no job outcome. See
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 4 and
[`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 7.1.

Whether a partial restart resumes from an intermediate point is `[OPEN]`. Until decided, a
failed job must not be silently resumed.

## 5. Isolation Valve model

### 5.1 Actuation

- The valve is **energize-to-open**. `[APPROVED]`
- DO OFF moves the valve toward the closed state. `[APPROVED]`
- Opening and closing timeouts are configurable. `[APPROVED]` Values `[NOT VERIFIED]`.

### 5.2 Derived feedback

| Upper limit | Lower limit | Derived state |
| --- | --- | --- |
| 0 | 1 | `CLOSED` |
| 1 | 0 | `OPEN` |
| 0 | 0 | `TRANSIT_OR_FAULT` |
| 1 | 1 | `INVALID_LIMIT_STATE` |

`TRANSIT_OR_FAULT` is legitimate for a bounded travel period. Remaining in
`TRANSIT_OR_FAULT` beyond the configured timeout is a fault condition.
`INVALID_LIMIT_STATE` is always a fault condition because it is physically contradictory.

### 5.3 Fault response during an active Cleaning Job

On valve verification failure during an active Cleaning Job, the system must `[APPROVED]`:

1. Stop or abort motion according to the approved fault class.
2. Command Valve OFF.
3. Set VFD AO to 0 Hz or stop the Main Pump.
4. Raise a blocking alarm.
5. Mark the Job `FAILED` or `RECOVERY_REQUIRED`.
6. **Not** update `LastSuccessfulCleaningCompletedAt`.
7. Block the next Job.
8. Wait for Operator action.

The definition of the "approved fault class" taxonomy is `[OPEN]`. That taxonomy must be
approved before the control path is implemented.

## 6. Idle valve fault workflow

Trigger: valve feedback becomes abnormal while the Main Pump is running and **no** Cleaning
Job is active. `[APPROVED]`

Required response:

1. Raise an alarm.
2. Pause the next-job countdown.
3. Show a modal to the Operator identifying the affected valve, Water Jet, and sensors.

Operator choices:

### A. STOP ALL

- Stop the Auto Sequence.
- Cancel the countdown.
- Stop the pump, or command VFD AO to 0 Hz.
- Preserve the Queue snapshot.
- Record an Event and reason.

### B. CONTINUE WITH VALVE EXCLUDED

- Mark the valve `OUT_OF_SERVICE`.
- Exclude every associated sensor from TempQueue, TimeQueue, GlobalQueue, and candidate
  refill.
- Allow **sequential** continuation: the AutoSequence may later select a Cleaning Job on
  another available Water Jet. It still executes only one Cleaning Job at a time. This must
  never be read as permission for concurrent operation.
- Display a persistent degraded-operation banner.
- Record the Event, user, valve, sensors, and Queue snapshot.

### 6.1 Returning a valve to service

All of the following are required:

1. Valid closed feedback.
2. Alarm cleared.
3. **Cleared-state acknowledgement** — the Operator must acknowledge the CLEARED state. An
   acknowledgement made earlier, while the alarm was ACTIVE, does not satisfy this
   requirement. See [`ALARM_MODEL.md`](ALARM_MODEL.md) section 3.
4. Explicit Operator return-to-service action.
5. Runtime validation.

Associated sensors then re-enter normal source queue evaluation, and must **not** be
inserted into the middle of GlobalQueue.

## 7. Main Pump supervision during a sequence

- The system receives **no direct motor-running feedback**. Pump state is derived from
  command state, pressure transmitter value, pressure quality, the pressure-ready setpoint,
  and the pressure rise timeout. `[APPROVED]`
- Conceptual states: `STOPPED`, `START_REQUESTED`, `PRESSURIZING`, `PRESSURE_READY`,
  `RUNNING_CONFIRMED_BY_PRESSURE`, `STOP_REQUESTED`, `FAULT`, `UNKNOWN`. `[APPROVED]`
- In an Auto Sequence: start the pump, wait for the pressure setpoint and stable dwell,
  begin the countdown, keep the pump running between Cleaning Jobs, and stop only when the
  Operator stops the sequence or a blocking fault requires it. `[APPROVED]`
- Pressure values, setpoint, stable dwell, and rise timeout are `[NOT VERIFIED]` and must
  never be invented.

### 7.1 Main Pump stop

No application-level operational permissive may block a valid Main Pump stop request
`[OWNER CONFIRMED]`. Actual command execution remains subject to command-path availability,
communication availability, external authority, Local/Remote state, hardware state, current
Production Write authorization, and independent protection behaviour.

The intent is that normal process permissives must not prevent a valid stop request. It is
**not** a claim that the application can physically execute a stop under every possible
failure condition.

The pump state model is not motor protection and must never be presented as such. Motor
protection remains external to the application.

## 8. Motion supervision during a job

- Galil model: DMC-B140-M; stepper motors with encoder feedback. `[APPROVED]`
- Feedback modes: `OPEN_LOOP` and `ENCODER_VERIFIED`. `[APPROVED]`
- `ENCODER_VERIFIED` must not be described as true closed-loop unless future engineering
  verification confirms that encoder feedback is used for active correction. `[APPROVED]`
- Motion profiles are global by operation type: Manual Jog, Manual Command Move, and
  Cleaning Job. `[APPROVED]`
- Motion mode or profile changes must not be allowed while the Main Pump is running, an
  Auto Sequence is active, a Cleaning Job is active, an axis is moving, the position is
  unknown, or a valve is open. `[APPROVED]`

Mechanical limits, soft limits, pulses per engineering unit, encoder behaviour, speed,
acceleration, deceleration, homing, and the operational envelope remain `[NOT VERIFIED]`.

## 9. Communication loss during a job

If a DCS-related communication or required stale-data condition becomes blocking while a
Cleaning Job is active `[APPROVED]`:

1. Allow the current Cleaning Job to reach its approved completion or fault-handling
   terminal condition, according to the approved process policy.
2. Do not dispatch the next Cleaning Job.
3. Raise or retain the alarm.
4. Stop the next-job countdown after the current Job.
5. Require condition recovery and cleared-state acknowledgement before continuing.

If the same condition becomes blocking while **no** Cleaning Job is active: raise the alarm,
stop the next-job countdown, do not dispatch a new Cleaning Job, require recovery, and
require cleared-state acknowledgement before the countdown resumes.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) section 5 and
[`REQUIREMENTS.md`](REQUIREMENTS.md) COMH-006 and COMH-007.

## 10. Failure and recovery boundaries

| Failure | Job state | Last-cleaning timestamp | Next job | Operator action |
| --- | --- | --- | --- | --- |
| P1 not reached | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Valve does not open within timeout | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Valve does not close within timeout | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Invalid limit state at any point | `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Standby position not confirmed | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Pressure lost during the job | `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Communication loss during the job | Current job reaches its approved terminal condition | Not updated on failure | Blocked after the current job | Required |
| Blocking alarm raised | Job pauses or fails per the alarm's blocking scope | Not updated on failure | Blocked until the block is released | Per [`ALARM_MODEL.md`](ALARM_MODEL.md) |

A blocked next job must not be silently cleared. Release requires the alarm conditions in
[`ALARM_MODEL.md`](ALARM_MODEL.md) section 3 to be satisfied, including cleared-state
acknowledgement.

## 10.1 Ownership and single-writer boundary (Stage 0.2)

The following boundaries are additions from the approved Stage 0.2 architecture decisions and do
not change any rule in sections 1 to 10.

1. **The Equipment Runtime service is the sole owner of a Cleaning Job.** Job state, sequence
   state, step advancement, valve actuation, and motion commands are owned by the runtime.
   The Operations UI may request and observe; it must never own, advance, or complete a job.
2. **The Operations UI must not write to hardware and must not open a device session.** It must
   not actuate an Isolation Valve, the Main Pump, a VFD, or a Galil axis, and must not read
   Modbus or Galil directly. See [`ARCHITECTURE.md`](ARCHITECTURE.md) sections 4.12 and 25.
3. **UI close, crash, or restart must not affect or terminate an active Cleaning Job.** The
   runtime continues to own and supervise the job. The UI close guard is an operator-safety
   affordance, not the mechanism that protects the job. See [`ARCHITECTURE.md`](ARCHITECTURE.md)
   section 28.
4. **After a UI reconnect, the job state comes from the runtime**, delivered as an
   authoritative snapshot. The UI must not reconstruct, infer, or resume a job from stale local
   state, and must not replay a command (LSD-006).
5. **Commands remain subject to the approved validation order** — authorization, lifecycle,
   interlock, ownership, and command-state validation — before any actuation, and only one
   command path may reach a device session.
6. **Nothing in this subsection permits more than one active Cleaning Job**, a parallel Water
   Jet Cleaning, or a shared Isolation Valve.
7. If motion position is unknown, or a command is stale, or a WAGO watchdog has expired, the
   fail-closed behaviour recorded in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) and section 8 of
   this document applies unchanged.

## 11. Not specified

The following are `[OPEN]` and must be approved before implementation:

- The fault class taxonomy that determines "stop" versus "abort" motion.
- The exact recovery procedure and permitted operator remedies for `RECOVERY_REQUIRED`.
- Whether a failed job may be retried automatically, and if so under what limits.
- Whether partial-path jobs may resume.
- Which sensor-to-Water-Jet mappings are valid for each wall region.

Resolved by Owner confirmation and therefore **not** open: whether more than one Cleaning
Job may run in parallel (it may not — see section 2).

---

## Related documents

- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — how the head of GlobalQueue is produced
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — why writes are currently prohibited
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — authority matrix and the DCS override
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking and cleared-state acknowledgement
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities, outcomes, and dispositions
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — CLJ, SEQ, VLV, and PMP requirements
