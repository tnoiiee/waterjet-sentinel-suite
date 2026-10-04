# Architecture — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the conceptual structure and boundaries described
here. Concrete technology selection is `[PROPOSED]` or `[OPEN]` as marked.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; implementation submitted for Owner
review; documentation review changes requested / in progress; Stage 0.2 `[NOT AUTHORIZED]`.

This document describes the conceptual architecture of the application. It contains no
implementation and authorises none. Production addresses, register maps, tag lists,
coordinates, limits, and setpoints do not appear here and must never be added.

---

## 1. Architectural position

WJSS is a **monitoring and supervisory control** application. It observes the process,
evaluates cleaning criteria, arbitrates cleaning queues, and issues supervisory commands to
equipment through approved control paths. It is not a Safety Instrumented System and must
never be relied upon as one. See [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md).

Two consequences shape the whole architecture:

1. **Every control action must be non-safety-critical by construction.** Anything that
   protects equipment or people stays in hardwired or controller-side protection, outside
   the application's authority. The application must never command, override, bypass,
   suppress, or replace an external protection function.
2. **Determinism beats optimisation.** Fixed acquisition intervals, stable ordering rules,
   explicit state machines, and explicit failure states take precedence over throughput or
   convenience.

A third structural consequence is the **strictly sequential execution model**: at most one
Cleaning Job may be ACTIVE within one installation at any time, regardless of how many Water
Jets, valves, walls, or controllers exist.

## 2. Deployment topology

| Layer | Baseline | Status |
| --- | --- | --- |
| Operator station | One Windows 11 Pro workstation per Boiler Unit, running the application in full-screen kiosk mode with controlled navigation | `[APPROVED]` |
| Application instance | One installation controls exactly one Boiler Unit | `[APPROVED]` |
| Field I/O | WAGO 750-362 Modbus TCP Coupler and remote I/O modules | `[APPROVED]` |
| Motion | Four Galil DMC-B140-M controllers, one per two Water Jets, axes A/B/C/D | `[APPROVED]` |
| Equipment network | Controllers are reached over a **local equipment network**. Exact network topology is `[OPEN]` / `[NOT VERIFIED]` | `[OPEN]` |
| DCS interface | DCS-originated hardwired signals are read through WAGO Modbus TCP. The application must not connect directly to the DCS | `[APPROVED]` |
| Historian database | SQL Server 2025 Standard on the workstation | `[APPROVED]` |
| Internet dependency | None. The initial system is standalone. | `[APPROVED]` |

No production IP address, host name, or network diagram may be recorded in this
repository. See [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md).

## 3. Logical layers

| Layer | Responsibility | Must not do |
| --- | --- | --- |
| Presentation | Kiosk-mode operator pages, trends, alarms, queue views, engineering configuration, responsive layouts | Contain control logic, compute eligibility, or hold process state |
| Application services | Orchestration of the Cleaning Job sequence, Auto Sequence lifecycle, operator commands, permission checks, session handling, the one-active-job gate, the DCS Permissive Override | Bypass validation, bypass alarm blocking, or write directly to field I/O |
| Domain | Sensor model, DirtyScore, queue eligibility, queue arbitration, source ownership, scan order, alarm state model, timestamp rules | Depend on transport, storage, or UI details |
| Acquisition and control adapters | Modbus TCP client, Galil motion client, time synchronisation, connection lifecycle, communication-health evaluation, retry and stale-data handling, reconnect hygiene | Decide eligibility, decide safety, or invent values |
| Persistence | Configuration store, Historian, alarms, events, audit, retention cleanup | Silently repair invalid configuration |

A layered, dependency-inward design is `[PROPOSED]` as an approach. The concrete pattern,
language, and framework remain `[OPEN]`.

**Equipment Runtime separation** is an architectural direction: long-running equipment
interaction is separated from the operator-facing interface so that closing, restarting, or
replacing the presentation layer does not by itself determine equipment state. It is
subject to later implementation scope and is `[PROPOSED]`.

## 4. Subsystems

Each subsystem below is `[PROPOSED]` as a component boundary unless marked otherwise. The
behaviour it must implement is `[APPROVED]` where the linked document says so.

### 4.1 Configuration subsystem

Holds the approved configuration for sensors, Water Jets, valves, controllers, motion
profiles, alarm definitions, thresholds, dwells, and retention. Publication of invalid
configuration is blocked; the runtime never silently corrects invalid values. See
[`REQUIREMENTS.md`](REQUIREMENTS.md) TMP-004 through TMP-007.

### 4.2 Acquisition subsystem

Polls Modbus TCP at the 1 second acquisition interval, decodes values according to the
local register map, applies quality assessment, and publishes tagged values. When data is
unavailable or stale, the subsystem must publish an explicit quality state rather than a
substituted value. Communication health is evaluated as described in section 5.

### 4.3 Temperature and Dirty Score subsystem

Computes `DiffTemp = TC_F - TC_R` per sensor, evaluates the linear DirtyScore mapping, and
raises the `TC_F <= TC_R` diagnostic without blocking queue eligibility. See
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 5.

### 4.4 Queue subsystem

Maintains four TempQueues and four TimeQueues — **eight source queues** — plus one
GlobalQueue with a target capacity of eight unique Sensor entries. It seeds GlobalQueue in
the fixed source order, performs deduplication with earliest-position preservation, records
merged reason flags against a single source owner, performs FIFO refill from that owner, and
applies operator Hold, Reject, and Reorder. Ordering rules are fully specified in
[`QUEUE_MODEL.md`](QUEUE_MODEL.md). Queue evaluation runs at a 1 second interval.

### 4.5 Sequence and dispatch subsystem

Owns the Auto Sequence lifecycle, the next-job countdown, Cleaning Job execution, and the
per-job revalidation of permissions, eligibility, equipment state, and permissives. It
enforces the sequential-execution invariants: at most one Cleaning Job may be ACTIVE at a
time, a second job must not enter an executing state until the current job has reached an
approved safe and released terminal condition, and no queue action or equipment condition
may create concurrency. It also owns AutoSequence stop and new-sequence queue rebuild. See
[`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) section 2 and
[`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 9.

Step 2 of the job sequence (revalidation) is the enforcement point for the one-active-job
invariant, in addition to any earlier gate.

### 4.6 Equipment supervision subsystem

Derives valve state from limit feedback, derives Main Pump state from command state,
pressure value, pressure quality, setpoint, and rise timeout, and supervises motion state
and position knowledge. See sections 14 to 16 of [`REQUIREMENTS.md`](REQUIREMENTS.md).

Valve-to-Water-Jet association is one-to-one and is used to derive every sensor's Isolation
Valve from its assigned Water Jet.

### 4.7 Alarm subsystem

Maintains the independent condition, acknowledgement, and shelving dimensions, computes
blocking state, and drives the modal and banner workflows. It implements the required
cleared-state acknowledgement: an acknowledgement recorded while an alarm is ACTIVE is
awareness only, and the final-clearance acknowledgement becomes pending when the condition
clears. See [`ALARM_MODEL.md`](ALARM_MODEL.md) section 3.

### 4.8 Historian subsystem

Stores process data at the baseline intervals, performs scheduled cutoff-based cleanup or
partition maintenance, and serves trends. See
[`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md).

### 4.9 Event, audit, and diagnostics subsystem

Records operator actions and system events with the required fields, records auditable
changes with user, time, previous value, new value, and reason, and exposes diagnostic
state. Diagnostics retention is 180 days `[PROPOSED]`.

### 4.10 Security and session subsystem

Local application users, configurable permission collections, privileged-session inactivity
handling, and break-glass recovery. See [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md).

### 4.11 DCS Permissive Override subsystem

Owns the Operator-activated, manually released **DCS Permissive Override**. It is a
single-purpose, scoped bypass of the approved DCS permissive evaluation only. It must not
be implemented as a general permissive bypass, and it must not be configurable to bypass
WAGO or Modbus communication health, valve feedback or valve verification, Main Pump
pressure validation, Galil limits, motion faults, encoder or position validation, emergency
stop, Local/Remote selector, motor or drive protection, the WAGO output watchdog, external
hardware protection, critical application lifecycle gates, or the one-active-Cleaning-Job
invariant. See [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-001 through OVR-010 and
[`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) section 5.

### 4.12 Operations UI close guard

The normal Operations UI close action is blocked while a Cleaning Job is active or while the
Main Pump is running. A rejected close request displays a clear explanation and directs the
Operator back to the active operation or Pump/Sequence state.

This guard is an **operational usability control**. It is not a safety protection, and it
cannot guarantee protection against process termination, Windows shutdown, workstation
restart, power loss, or hardware failure. Equipment Runtime lifecycle, the WAGO watchdog,
safe output states, and external hardware protection remain independent requirements. See
[`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) section 6.

## 5. Communication health model

Communication health is evaluated from transport evidence, not from value change.

| Signal | Purpose |
| --- | --- |
| TCP connection state | Whether a transport session exists |
| Modbus request completion | Whether issued requests completed |
| Valid response receipt | Whether a well-formed response arrived |
| Modbus exception response | Whether the device returned an exception code |
| Request timeout | Whether a request exceeded its configured time |
| Consecutive failure count | Whether failures are accumulating |
| Last successful poll time | When the last good exchange completed |
| Poll-cycle lateness or overrun | Whether the scan cycle is being met |
| Signal quality state | The published quality of each value |

Rules:

1. A configurable **stale timeout** is required. An example operational value of 30 seconds
   may be used as an example only; it must remain configurable and must never be treated as
   a fixed production value.
2. **A process value remaining unchanged is not, by itself, proof that communication is
   lost.** A digital input, pressure value, or temperature may legitimately remain constant.
   Communication health must not depend solely on value change detection.
3. A future heartbeat or watchdog signal feature may be designed, but its exact hardware
   contract remains `[OPEN]` / `[NOT VERIFIED]` until approved and tested.
4. Unknown, unavailable, stale, or bad-quality indication must never be inferred as safe.

Approved fault behaviour:

| Situation | Required response |
| --- | --- |
| Condition becomes blocking while no Cleaning Job is active | Raise the alarm; stop the next-job countdown; do not dispatch a new Cleaning Job; require recovery; require **cleared-state acknowledgement** before the countdown resumes |
| Condition becomes blocking while a Cleaning Job is active | Allow the current Cleaning Job to reach its approved completion or fault-handling terminal condition per the approved process policy; do not dispatch the next Cleaning Job; raise or retain the alarm; stop the next-job countdown after the current job; require recovery and cleared-state acknowledgement before continuing |

## 6. Timing model

| Activity | Baseline interval | Status |
| --- | --- | --- |
| Modbus acquisition | 1 second | `[APPROVED]` |
| Queue evaluation | 1 second | `[APPROVED]` |
| Normal thermocouple historian | 5 seconds | `[APPROVED]` |
| Active Cleaning Job detailed historian | 1 second | `[APPROVED]` |
| Alarm-related detailed storage | 1 second | `[APPROVED]` |
| Long-term aggregate | 1 minute | `[APPROVED]` |
| TempQueue entry and removal dwell | 10 seconds each, independently configurable | `[APPROVED]` |
| Stale-data timeout | configurable; 30 seconds is an **example** only | `[APPROVED]` as configurable / `[NOT VERIFIED]` as a value |

The architecture must tolerate a missed or delayed cycle without changing ordering
semantics. Because queue ordering is defined by data values and stable scanOrder rather than
by arrival time, a late cycle cannot reorder a queue silently.

## 7. Time handling

- Canonical storage is UTC. Display is Asia/Bangkok. `[APPROVED]`
- `LastSuccessfulCleaningCompletedAt` is never null. Every sensor receives an explicit
  initial timestamp during configuration. `[APPROVED]`
- Timing comparisons must use a single, monotonic-enough reference for interval
  arithmetic, and the durable timestamp for scheduling arithmetic. The precise mechanism
  is `[OPEN]`.
- Workstation clock discipline and drift bounds are not specified. `[OPEN]`

## 8. State machine inventory

The following explicit state machines are required:

| Machine | States | Reference |
| --- | --- | --- |
| Main Pump | STOPPED, START_REQUESTED, PRESSURIZING, PRESSURE_READY, RUNNING_CONFIRMED_BY_PRESSURE, STOP_REQUESTED, FAULT, UNKNOWN | [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-003 |
| Isolation Valve (derived) | CLOSED, OPEN, TRANSIT_OR_FAULT, INVALID_LIMIT_STATE | [`REQUIREMENTS.md`](REQUIREMENTS.md) VLV-002 |
| Cleaning Job | defined in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) | `[APPROVED]` |
| Axis position knowledge | known / unknown | [`REQUIREMENTS.md`](REQUIREMENTS.md) GAL-005 |
| Alarm condition, acknowledgement, and clearance | ACTIVE/CLEARED × active-awareness × cleared-state acknowledgement × UNSHELVED/SHELVED | [`ALARM_MODEL.md`](ALARM_MODEL.md) |
| AutoSequence | idle / active / stopping / closed, with a new-sequence queue rebuild | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 9 |

Every state machine must define its behaviour for lost communication, stale data, and
partial feedback. Undefined state handling is a defect.

## 9. Failure and recovery boundaries

| Failure | Required architectural response | Status |
| --- | --- | --- |
| Modbus communication loss | Publish explicit bad quality; do not substitute values; block control actions that depend on the lost data; raise alarm | `[PROPOSED]` |
| DCS-related communication or stale-data condition blocks | While idle: stop countdown and do not dispatch. While a job is active: current job reaches its approved terminal condition, then the next job is blocked | `[APPROVED]` |
| Valve feedback disagreement during a Cleaning Job | Follow VLV-004: stop or abort motion, command valve OFF, VFD AO to 0 Hz or stop pump, blocking alarm, job FAILED or RECOVERY_REQUIRED | `[APPROVED]` |
| Valve feedback abnormal while idle and pump running | Operator modal with Stop All or Continue With Valve Excluded (**sequential continuation only**) | `[APPROVED]` |
| Pressure not confirmed within the rise timeout | Pump state must become FAULT; no job may start | `[PROPOSED]` |
| Axis position unknown | Motion mode and profile changes blocked; job must not start | `[APPROVED]` |
| Application terminated while equipment is commanded | Outputs must reach target safe states by hardware or controller behaviour, not by application action alone | `[NOT VERIFIED]` |
| Workstation reboot | Outputs must not remain energized; must not auto re-energize after recovery | `[NOT VERIFIED]` |
| Operator attempts to close the Operations UI while a job is active or the pump runs | Close request rejected with an explanation; Operator directed back to the active operation. This is an operational usability control, not protection | `[OWNER CONFIRMED]` |
| Database unavailable | Diagnosis behaviour is unspecified; operator visibility of live process state must not depend on the Historian | `[OPEN]` |

## 10. Reliability and determinism principles

1. Every control path is explicit and enumerable. No implicit commands.
2. Every command has a verification step and a bounded wait.
3. Ordering is always defined by data plus stable configuration, never by iteration order
   of a dictionary, thread scheduling, or UI sorting.
4. Reconnection must never re-issue a stale command.
5. Recovery is operator-visible: blocking alarms and RECOVERY_REQUIRED states are explicit,
   never silently cleared, and never released without cleared-state acknowledgement where a
   cleared-state acknowledgement is required.
6. Configurable values are validated at publication time; invalid configuration cannot be
   published.
7. At most one Cleaning Job may be ACTIVE at any time. This invariant must hold under every
   queue action, equipment state, and operator intervention.
8. The DCS Permissive Override is scoped to DCS permissive evaluation only and can never be
   configured to widen its scope.

## 11. Technology decisions still open

The following are `[OPEN]`. They must be decided by an approved Stage Gate before code is
written:

- Application language, runtime, and UI framework on Windows 11 Pro. **Browser-based,
  desktop, and hybrid local-web delivery all remain available**; product identity does not
  decide the framework, and no delivery technology is rejected on grounds of convenience.
- Process architecture (single process versus supervised services), restart behaviour, and
  the exact scope of Equipment Runtime separation.
- Modbus TCP client library selection and licence acceptability.
- Galil communication mechanism and library selection.
- Local configuration store format and its validation mechanism.
- Data access approach for SQL Server 2025 Standard.
- Logging, diagnostics, and crash-report storage.
- Exact local equipment network topology and address plan.

Recording these as open is deliberate. Choosing them by assumption would violate
[`AGENTS.md`](../AGENTS.md) section 3.

## 12. Deployment configuration and secrets

Configuration is layered: public-safe defaults and examples may live in the repository;
local production values live outside the Git working tree where practical. Connection
strings and credentials are never committed. See
[`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) and
[`../SECURITY.md`](../SECURITY.md).

---

## Related documents

- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and terminology
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue specification, source ownership, stop/rebuild
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — Cleaning Job specification and sequencing
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — hardware safety boundary and UI guard limits
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — control authority matrix and override
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — alarm model and cleared-state acknowledgement
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — storage and retention
- [`decisions/README.md`](decisions/README.md) — architecture decision records
