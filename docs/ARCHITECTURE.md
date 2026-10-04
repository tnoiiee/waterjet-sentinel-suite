# Architecture — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the conceptual structure and boundaries described
here. Concrete technology selection is `[PROPOSED]` or `[OPEN]` as marked.

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
   the application's authority.
2. **Determinism beats optimisation.** Fixed acquisition intervals, stable ordering rules,
   explicit state machines, and explicit failure states take precedence over throughput or
   convenience.

## 2. Deployment topology

| Layer | Baseline | Status |
| --- | --- | --- |
| Operator station | One Windows 11 Pro workstation per Boiler Unit, running the application in full-screen kiosk mode | `[APPROVED]` |
| Application instance | One installation controls exactly one Boiler Unit | `[APPROVED]` |
| Field I/O | WAGO 750-362 Modbus TCP Coupler and remote I/O modules | `[APPROVED]` |
| Motion | Four Galil DMC-B140-M controllers, one per two Water Jets, axes A/B/C/D | `[APPROVED]` |
| Historian database | SQL Server 2025 Standard on the workstation | `[APPROVED]` |
| Network | Local industrial network. Address plan, VLAN design, and topology are confidential deployment information and are **not** documented here. | `[OPEN]` |
| Internet dependency | None. The initial system is standalone. | `[APPROVED]` |

No production IP address, host name, or network diagram may be recorded in this
repository. See [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md).

## 3. Logical layers

| Layer | Responsibility | Must not do |
| --- | --- | --- |
| Presentation | Kiosk-mode operator pages, trends, alarms, queue views, engineering configuration, responsive layouts | Contain control logic, compute eligibility, or hold process state |
| Application services | Orchestration of the Cleaning Job sequence, Auto Sequence lifecycle, operator commands, permission checks, session handling | Bypass validation, bypass alarm blocking, or write directly to field I/O |
| Domain | Sensor model, DirtyScore, queue eligibility, queue arbitration, scan order, alarm state model, timestamp rules | Depend on transport, storage, or UI details |
| Acquisition and control adapters | Modbus TCP client, Galil motion client, time synchronisation, connection lifecycle, retry and stale-data handling | Decide eligibility, decide safety, or invent values |
| Persistence | Configuration store, Historian, alarms, events, audit, retention cleanup | Silently repair invalid configuration |

A layered, dependency-inward design is `[PROPOSED]` as an approach. The concrete pattern,
language, and frameworks remain `[OPEN]`.

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
substituted value.

### 4.3 Temperature and Dirty Score subsystem

Computes `DiffTemp = TC_F - TC_R` per sensor, evaluates the linear DirtyScore mapping, and
raises the `TC_F <= TC_R` diagnostic without blocking queue eligibility. See
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 5.

### 4.4 Queue subsystem

Maintains one TempQueue and one TimeQueue per wall, seeds and maintains GlobalQueue with
deduplication, performs FIFO refill, and applies operator Hold, Reject, and Reorder.
Ordering rules are fully specified in [`QUEUE_MODEL.md`](QUEUE_MODEL.md). Queue evaluation
runs at a 1 second interval.

### 4.5 Sequence and dispatch subsystem

Owns the Auto Sequence lifecycle, the next-job countdown, Cleaning Job execution, and the
per-job revalidation of permissions, eligibility, equipment state, and permissives. See
[`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md).

### 4.6 Equipment supervision subsystem

Derives valve state from limit feedback, derives Main Pump state from command state,
pressure value, pressure quality, setpoint, and rise timeout, and supervises motion state
and position knowledge. See sections 11 to 13 of [`REQUIREMENTS.md`](REQUIREMENTS.md).

### 4.7 Alarm subsystem

Maintains the independent condition, acknowledgement, and shelving dimensions, computes
blocking state, and drives the modal and banner workflows. See
[`ALARM_MODEL.md`](ALARM_MODEL.md).

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

## 5. Timing model

| Activity | Baseline interval | Status |
| --- | --- | --- |
| Modbus acquisition | 1 second | `[APPROVED]` |
| Queue evaluation | 1 second | `[APPROVED]` |
| Normal thermocouple historian | 5 seconds | `[APPROVED]` |
| Active Cleaning Job detailed historian | 1 second | `[APPROVED]` |
| Alarm-related detailed storage | 1 second | `[APPROVED]` |
| Long-term aggregate | 1 minute | `[APPROVED]` |
| TempQueue entry and removal dwell | 10 seconds each, independently configurable | `[APPROVED]` |

The architecture must tolerate a missed or delayed cycle without changing ordering
semantics. Because queue ordering is defined by data values and stable scanOrder rather
than by arrival time, a late cycle cannot reorder a queue silently.

## 6. Time handling

- Canonical storage is UTC. Display is Asia/Bangkok. `[APPROVED]`
- `LastSuccessfulCleaningCompletedAt` is never null. Every sensor receives an explicit
  initial timestamp during configuration. `[APPROVED]`
- Timing comparisons must use a single, monotonic-enough reference for interval
  arithmetic, and the durable timestamp for scheduling arithmetic. The precise mechanism
  is `[OPEN]`.
- Workstation clock discipline and drift bounds are not specified. `[OPEN]`

## 7. State machine inventory

The following explicit state machines are required:

| Machine | States | Reference |
| --- | --- | --- |
| Main Pump | STOPPED, START_REQUESTED, PRESSURIZING, PRESSURE_READY, RUNNING_CONFIRMED_BY_PRESSURE, STOP_REQUESTED, FAULT, UNKNOWN | [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-003 |
| Isolation Valve (derived) | CLOSED, OPEN, TRANSIT_OR_FAULT, INVALID_LIMIT_STATE | [`REQUIREMENTS.md`](REQUIREMENTS.md) VLV-002 |
| Cleaning Job | defined in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) | `[APPROVED]` |
| Axis position knowledge | known / unknown | [`REQUIREMENTS.md`](REQUIREMENTS.md) GAL-005 |
| Alarm condition and acknowledgement | ACTIVE/CLEARED × UNACKNOWLEDGED/ACKNOWLEDGED × UNSHELVED/SHELVED | [`ALARM_MODEL.md`](ALARM_MODEL.md) |

Every state machine must define its behaviour for lost communication, stale data, and
partial feedback. Undefined state handling is a defect.

## 8. Failure and recovery boundaries

| Failure | Required architectural response | Status |
| --- | --- | --- |
| Modbus communication loss | Publish explicit bad quality; do not substitute values; block control actions that depend on the lost data; raise alarm | `[PROPOSED]` |
| Valve feedback disagreement during a Cleaning Job | Follow VLV-004: stop or abort motion, command valve OFF, VFD AO to 0 Hz or stop pump, blocking alarm, job FAILED or RECOVERY_REQUIRED | `[APPROVED]` |
| Valve feedback abnormal while idle and pump running | Operator modal with Stop All or Continue With Valve Excluded | `[APPROVED]` |
| Pressure not confirmed within the rise timeout | Pump state must become FAULT; no job may start | `[PROPOSED]` |
| Axis position unknown | Motion mode and profile changes blocked; job must not start | `[APPROVED]` |
| Application terminated while equipment is commanded | Outputs must reach target safe states by hardware or controller behaviour, not by application action alone | `[NOT VERIFIED]` |
| Workstation reboot | Outputs must not remain energized; must not auto re-energize after recovery | `[NOT VERIFIED]` |
| Database unavailable | Diagnosis behaviour is unspecified; operator visibility of live process state must not depend on the Historian | `[OPEN]` |

## 9. Reliability and determinism principles

1. Every control path is explicit and enumerable. No implicit commands.
2. Every command has a verification step and a bounded wait.
3. Ordering is always defined by data plus stable configuration, never by iteration order
   of a dictionary, thread scheduling, or UI sorting.
4. Reconnection must never re-issue a stale command.
5. Recovery is operator-visible: blocking alarms and RECOVERY_REQUIRED states are explicit,
   never silently cleared.
6. Configurable values are validated at publication time; invalid configuration cannot be
   published.

## 10. Technology decisions still open

The following are `[OPEN]`. They must be decided by an approved Stage Gate before code is
written:

- Application language, runtime, and UI framework on Windows 11 Pro.
- Process architecture (single process versus supervised services) and restart behaviour.
- Modbus TCP client library selection and licence acceptability.
- Galil communication mechanism and library selection.
- Local configuration store format and its validation mechanism.
- Data access approach for SQL Server 2025 Standard.
- Logging, diagnostics, and crash-report storage.

Recording these as open is deliberate. Choosing them by assumption would violate
[`AGENTS.md`](../AGENTS.md) section 3.

## 11. Deployment configuration and secrets

Configuration is layered: public-safe defaults and examples may live in the repository;
local production values live outside the Git working tree where practical. Connection
strings and credentials are never committed. See
[`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) and
[`../SECURITY.md`](../SECURITY.md).

---

## Related documents

- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and terminology
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue specification
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — Cleaning Job specification
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — hardware safety boundary
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — control authority matrix
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — storage and retention
- [`decisions/README.md`](decisions/README.md) — architecture decision records
