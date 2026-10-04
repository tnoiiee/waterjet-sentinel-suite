# Cleaning Sequence — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the sequence, its verification steps, and its failure
boundaries. All coordinate and timing values are `[NOT VERIFIED]` and none are invented
here.

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

Coordinate values are commissioning values: they are `[NOT VERIFIED]` and must be captured
from engineering records. They are never committed to this repository.

## 2. Normal sequence

The normal sequence is an approved nineteen-step order `[APPROVED]`:

| Step | Action | Verification / condition |
| --- | --- | --- |
| 1 | Select the GlobalQueue head | Head is not held; no blocking condition |
| 2 | Revalidate permissions, eligibility, equipment state, and permissives | All must pass; otherwise the job must not start |
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

## 3. Job states

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

Whether a partial restart resumes from an intermediate point is `[OPEN]`. Until decided, a
failed job must not be silently resumed.

## 4. Isolation Valve model

### 4.1 Actuation

- The valve is **energize-to-open**. `[APPROVED]`
- DO OFF moves the valve toward the closed state. `[APPROVED]`
- Opening and closing timeouts are configurable. `[APPROVED]` Values `[NOT VERIFIED]`.

### 4.2 Derived feedback

| Upper limit | Lower limit | Derived state |
| --- | --- | --- |
| 0 | 1 | `CLOSED` |
| 1 | 0 | `OPEN` |
| 0 | 0 | `TRANSIT_OR_FAULT` |
| 1 | 1 | `INVALID_LIMIT_STATE` |

`TRANSIT_OR_FAULT` is legitimate for a bounded travel period. Remaining in
`TRANSIT_OR_FAULT` beyond the configured timeout is a fault condition.
`INVALID_LIMIT_STATE` is always a fault condition because it is physically contradictory.

### 4.3 Fault response during an active Cleaning Job

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

## 5. Idle valve fault workflow

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
- Allow unaffected Water Jets to continue.
- Display a persistent degraded-operation banner.
- Record the Event, user, valve, sensors, and Queue snapshot.

### Returning a valve to service

All of the following are required:

1. Valid closed feedback.
2. Alarm cleared.
3. Cleared state acknowledged.
4. Explicit Operator return-to-service action.
5. Runtime validation.

Associated sensors then re-enter normal source queue evaluation, and must **not** be
inserted into the middle of GlobalQueue.

## 6. Main Pump supervision during a sequence

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

The pump state model is not motor protection and must never be presented as such. Motor
protection remains external to the application.

## 7. Motion supervision during a job

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

## 8. Failure and recovery boundaries

| Failure | Job state | Last-cleaning timestamp | Next job | Operator action |
| --- | --- | --- | --- | --- |
| P1 not reached | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Valve does not open within timeout | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Valve does not close within timeout | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Invalid limit state at any point | `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Standby position not confirmed | `FAILED` or `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Pressure lost during the job | `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Communication loss during the job | `RECOVERY_REQUIRED` | Not updated | Blocked | Required |
| Blocking alarm raised | Job pauses or fails per the alarm's blocking scope | Not updated on failure | Blocked until the block is released | Per [`ALARM_MODEL.md`](ALARM_MODEL.md) |

A blocked next job must not be silently cleared. Release requires the alarm conditions in
[`ALARM_MODEL.md`](ALARM_MODEL.md) to be satisfied.

## 9. Not specified

The following are `[OPEN]` and must be approved before implementation:

- The fault class taxonomy that determines "stop" versus "abort" motion.
- The exact recovery procedure and permitted operator remedies for `RECOVERY_REQUIRED`.
- Whether a failed job may be retried automatically, and if so under what limits.
- Whether partial-path jobs may resume.
- Which sensor-to-Water-Jet mappings are valid for each wall region.
- Whether more than one Cleaning Job may ever run in parallel (see
  [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 3).

---

## Related documents

- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — how the head of GlobalQueue is produced
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — why writes are currently prohibited
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — authority matrix
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking and acknowledgement
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and enumerations
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — CLJ and VLV requirements
