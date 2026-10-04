# Architecture — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the conceptual structure and boundaries described
here. Concrete technology selection is `[PROPOSED]` or `[OPEN]` as marked.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 implementation **SUBMITTED FOR OWNER REVIEW**; Owner manual review
**PENDING**; **NOT MERGED**; Stage 0.3 `[NOT AUTHORIZED]`.

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

**Equipment Runtime separation** is required: long-running equipment interaction is separated
from the operator-facing interface so that closing, restarting, or replacing the presentation
layer does not by itself determine equipment state. The approved Stage 0.2 Scope Gate makes
this a mandatory boundary — only the approved Equipment Runtime boundary may own physical
device sessions, and the UI must not write to hardware directly. The concrete process mapping
selected in Stage 0.2 is `[PROPOSED]` in
[`decisions/ADR-0007`](decisions/ADR-0007-runtime-process-model.md) and is described in
section 14 of this document.

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

## 11. Technology decisions — Stage 0.2 disposition

The Stage 0.1 baseline deliberately left the items below `[OPEN]`. The approved Stage 0.2
Scope Gate authorised an architecture-decision Stage to close or refine them. The disposition
is recorded here and argued in the referenced ADR records.

| Stage 0.1 open item | Stage 0.2 disposition | Where |
| --- | --- | --- |
| Application language, runtime, and UI framework on Windows 11 Pro | Selected as `[PROPOSED]`: .NET with C#, LTS support track, local web UI in C# (Blazor component model) inside an application-owned kiosk shell window. Exact version `[OPEN]` until the implementation gate pins it with a cited support reference. Browser-based, desktop, and hybrid delivery were all evaluated against the project's requirements; none was rejected on convenience grounds | [`ADR-0006`](decisions/ADR-0006-ui-delivery-model.md), [`ADR-0008`](decisions/ADR-0008-technology-stack.md) |
| Process architecture, restart behaviour, and the scope of Equipment Runtime separation | Selected as `[PROPOSED]`: kiosk shell process plus one runtime service process that owns all device sessions, all control dispatch, and all database access | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md), section 14 |
| Modbus TCP client library selection and licence acceptability | Adapter **boundary** decided; **library selection `[OPEN]`** pending licence, offline-availability, maintenance, and observability review | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) |
| Galil communication mechanism and library selection | **`[OPEN]`** — vendor-provided interface, vendor library, and direct command transport must be evaluated before motion code is written | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) |
| Local configuration store format and its validation mechanism | Model decided as `[PROPOSED]` (Draft versus Published revisions, validation blocks publication, local site directory outside the working tree); format and secret-store mechanism `[OPEN]` | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md), section 17 |
| Data access approach for SQL Server 2025 Standard | Selected as `[PROPOSED]`: mapper as the primary technology, with a measured escape hatch for the high-rate Historian write path; provider version and compatibility `[OPEN]` | [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md), section 17 |
| Logging, diagnostics, and crash-report storage | Strategy decided as `[PROPOSED]` (structured local rolling files, separate from audit records, outside the working tree); provider selection `[OPEN]` | [`ADR-0008`](decisions/ADR-0008-technology-stack.md) |
| Exact local equipment network topology and address plan | Remains `[OPEN]` / `[NOT VERIFIED]` — confidential local deployment information | [`REQUIREMENTS.md`](REQUIREMENTS.md) PHY-008 |

Remaining `[OPEN]` architecture items after Stage 0.2 are itemised in section 22.

Recording a selection as `[PROPOSED]` is deliberate. Only the Owner may accept it, and
choosing by assumption would violate [`AGENTS.md`](../AGENTS.md) section 3.

## 12. Deployment configuration and secrets

Configuration is layered: public-safe defaults and examples may live in the repository;
local production values live outside the Git working tree where practical. Connection
strings and credentials are never committed. See
[`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) and
[`../SECURITY.md`](../SECURITY.md).

Stage 0.2 refines this into the model in section 17 and
[`decisions/ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md).

---

# Stage 0.2 — Technology and Solution Architecture

The sections below were added by the approved Stage 0.2 Scope Gate, *Technology and Solution
Architecture Decision*. Stage 0.2 is a documentation and architecture-decision Stage: it
authorises no code, no dependency, no device access, and no runtime artefact, and it
authorises no production write.

## 13. Stage 0.2 decision status and vocabulary

Architecture statements in this document and in the Stage 0.2 ADR records use four statuses:

| Status | Meaning in this Stage |
| --- | --- |
| `[APPROVED]` / `[OWNER CONFIRMED]` | Already binding from an approved source: an approved Scope Gate, an Owner-confirmed review decision, or an existing `ACCEPTED` ADR |
| `[PROPOSED]` | A selection made in Stage 0.2 that requires Owner acceptance. Not binding until the Owner records `ACCEPTED` |
| `[OPEN]` | A question that cannot be closed from evidence available in this Stage. It must be closed by a named later gate |
| `[NOT VERIFIED]` | Depends on measurement, bench test, field test, hardware observation, or a runtime that does not exist |

| Stage 0.2 decision | Status | Record | Binding boundaries inside it |
| --- | --- | --- | --- |
| UI delivery model | `[PROPOSED]` | [`ADR-0006`](decisions/ADR-0006-ui-delivery-model.md) | Close guard and kiosk requirements remain `[OWNER CONFIRMED]` |
| Runtime process model | `[PROPOSED]` | [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) | Device-session ownership and UI separation are approved boundaries |
| Technology stack | `[PROPOSED]` with `[OPEN]` items | [`ADR-0008`](decisions/ADR-0008-technology-stack.md) | Priority order and offline stance are `[APPROVED]` |
| Database access and migrations | `[PROPOSED]` with `[OPEN]` items | [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) | SQL Server 2025 Standard and the intervals are `[APPROVED]` |
| Device adapter boundary | `[PROPOSED]` | [`ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) | Vendor isolation, simulator parity, and disabled-by-default physical adapters are approved principles |
| Configuration and secrets | `[PROPOSED]` | [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md) | Production configuration and secrets outside Git are approved boundaries |
| Simulator-first development | `[PROPOSED]` | [`ADR-0012`](decisions/ADR-0012-simulator-first-development.md) | Simulator default and the physical-adapter gate are approved decisions |
| Offline deployment | `[PROPOSED]` with `[OPEN]` items | [`ADR-0013`](decisions/ADR-0013-offline-deployment.md) | No Internet dependency and no auto-resume are `[APPROVED]` |
| Repository structure direction | `[PROPOSED]` — documented, not created | Section 20 | No directory is created by Stage 0.2 |

**ADR acceptance in Stage 0.2 means acceptance as a documentation decision.** It is not
implementation proof, not hardware evidence, and not deployment acceptance.

## 14. Process and ownership model

### 14.1 Processes on the workstation

| Process | Role | May | Must not |
| --- | --- | --- | --- |
| Kiosk shell (interactive session, presentation only) | Operations UI, Engineering pages, close guard, profile display | Render pages, capture operator input, submit **control requests** to the Local Application API, display state read from the API | Own a device session, write to hardware, connect to the database, compute eligibility, enforce sequencing, or hold authoritative process state |
| Runtime service (Windows Service, single instance) | Equipment Runtime, Local Application API, queue engine, cleaning orchestrator, alarm engine, historian writer, configuration publisher, device adapters, simulator adapters when selected | Own all physical and simulated device sessions, validate and dispatch commands, execute Cleaning Jobs, evaluate queues, persist records, publish configuration, manage users and permissions, hold all authoritative state | Render UI, accept an unvalidated command, or allow a second active Cleaning Job |
| SQL Server 2025 Standard | Local database | Serve the runtime service | Be reached by the kiosk shell |

This mapping is `[PROPOSED]` in [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md).
The boundaries marked below are already approved and are not `[OPEN]`.

### 14.2 Ownership matrix

| Capability | Owning process | Notes |
| --- | --- | --- |
| Own Modbus TCP connections | Runtime service | Single session owner per device |
| Own Galil sessions | Runtime service | Single session owner per controller |
| Read device values | Runtime service | Values are exposed to the UI as read-only API data with quality |
| Request a control action | Kiosk shell (operator request) and runtime service (supervisory orchestration) | Requests are not commands |
| Validate a command | Runtime service | Permission, lifecycle, interlock, ownership, and command-state validation |
| Dispatch a device write | Runtime service | The only process that may write to hardware |
| Execute a Cleaning Job | Runtime service | One active Cleaning Job at any time |
| Evaluate queue eligibility | Runtime service | Domain logic, hardware-independent |
| Write Historian data | Runtime service | Decoupled write path; never blocks control |
| Persist Alarm, Event, and Audit records | Runtime service | Transactional with the state change they record |
| Publish Engineering configuration | Runtime service, on an authorised publication request | Atomic, validated, audited |
| Manage application users and permissions | Runtime service | Administration pages submit requests through the API |
| Connect to the database | Runtime service | Single connection owner; single migration authority |
| Render the UI | Kiosk shell | No authority over equipment |

### 14.3 Mandatory boundaries (approved — not `[OPEN]`)

1. The UI must not write to hardware directly.
2. The UI must not own device sessions.
3. Only the approved Equipment Runtime boundary may own physical device sessions.
4. Device adapters must not contain UI logic.
5. Hardware commands must pass through authorization, lifecycle, interlock, ownership, and
   command-state validation.
6. No architecture choice may permit concurrent Cleaning Jobs.

### 14.4 Lifecycle rules

- The runtime service starts automatically, independently of any interactive sign-in.
- After any start or restart it enters an explicit startup state: no command may be issued
  until device state, position knowledge, and permissives have been re-established, and no
  command in flight before the restart may be replayed, resumed, or re-issued.
- A restart never auto-resumes an AutoSequence or a Cleaning Job and never re-energizes an
  output.
- Only one runtime service instance may exist on a workstation; a second instance must refuse
  to start.
- The kiosk shell holds no authority: closing or crashing it cannot change equipment state,
  and losing it must be visible to the operator when a job is active.
- The Local Application API is loopback-only, authenticated, contract-defined, and hosted by
  the runtime service in the baseline. Its transport is replaceable without a contract change.
- Manual hold-to-run operation must stop when the operator interface session ends.
- A service restart is an operator-visible event, not a silent recovery.

## 15. Failure isolation matrix

Ownership and expected boundaries only. No recovery logic is implemented in this Stage, and no
status below may be read as verified behaviour.

| # | Situation | Owner / boundary | Expected architectural behaviour | Status |
| --- | --- | --- | --- | --- |
| 1 | Operations UI closed normally | Kiosk shell | Close is refused while a Cleaning Job is active or the Main Pump is running, with an explanation and redirect; runtime unaffected | `[OWNER CONFIRMED]` |
| 2 | Operations UI crash | Kiosk shell | Runtime service continues and holds state; the loss of the operator interface is visible; on restart the shell re-reads authoritative state and replays nothing | `[PROPOSED]` |
| 3 | Operations UI restart | Kiosk shell and Local Application API | Shell reconnects and re-reads state; sessions re-authenticate; hold-to-run operations do not resume | `[PROPOSED]` |
| 4 | Local Application API restart | Runtime service | In the baseline this is a runtime service restart (in-process hosting); the shell shows a disconnected state and must not submit requests | `[PROPOSED]` |
| 5 | Equipment Runtime restart | Runtime service | Explicit startup state; device sessions rebuilt; position knowledge may become unknown; no command replay; an interrupted Cleaning Job cannot resume silently | `[PROPOSED]` |
| 6 | SQL Server unavailable | Runtime service | Live supervision and UI visibility continue; process-history writes fail with an explicit state; stop and de-energize actions are never gated on the database; audit-required initiating actions are refused (`[PROPOSED]` set) | `[PROPOSED]` / partially `[OPEN]` |
| 7 | Historian writer unavailable | Runtime service | Control continues; write path is decoupled, bounded, and prioritised; overflow and backpressure policy is explicit | `[OPEN]` policy |
| 8 | Modbus communication loss | Runtime service, WAGO adapter | Explicit bad quality published; no substituted values; dependent control actions blocked; alarm raised; recovery requires revalidation; no stale command execution | `[APPROVED]` for the DCS-related blocking behaviour; `[PROPOSED]` elsewhere |
| 9 | Galil communication loss | Runtime service, motion adapter | Motion blocked; axis position knowledge becomes unknown; the active job reaches an approved fault terminal condition; no automatic resume | `[PROPOSED]` |
| 10 | Windows user logout | Interactive session and kiosk shell | Runtime service is unaffected; manual operations stop; privileged session state cleared; kiosk restart policy is a deployment decision | `[PROPOSED]` / `[OPEN]` |
| 11 | Privileged session timeout | Runtime service session manager | Privileged user logged out and the session returns to Operator; manual hold-to-run stops; an active AutoSequence is not aborted; drafts are preserved or safely handled | `[APPROVED]` |
| 12 | Workstation reboot | Operating system and hardware | Outputs must not remain energized and must not auto re-energize; the runtime service restarts into its startup state; nothing resumes automatically; position knowledge is unknown until re-established | `[NOT VERIFIED]` (hardware) / `[PROPOSED]` (application) |
| 13 | WAGO watchdog expiration | Hardware | No application claim is made; the application must not depend on watchdog behaviour for safety | `[NOT VERIFIED]` |
| 14 | Stale commands after reconnection | Runtime service, adapters | Commands have bounded validity; on reconnect commands are re-authorized and re-validated, never replayed | `[APPROVED]` principle |
| 15 | Incomplete Cleaning Job after a process restart | Runtime service, operator | Outcome requires explicit operator resolution; the last successful cleaning timestamp is not updated; the job is not resumed; the recovery procedure itself is `[OPEN]` | `[PROPOSED]` / `[OPEN]` |
| 16 | Unknown motion position | Runtime service, motion adapter | Motion mode and profile changes blocked and job start blocked; motion requires position re-establishment | `[APPROVED]` |
| 17 | Configuration publication failure | Runtime service | Publication is atomic; a failure leaves the previous published revision in force; the runtime never operates on a partially applied revision; the failure is reported and audited | `[PROPOSED]` |

## 16. Device adapter strategy and command lifecycle

### 16.1 Ports and adapters

| Port (application-owned contract) | Physical adapter | Simulator adapter | Test adapter |
| --- | --- | --- | --- |
| Field I/O by logical tag identity, quality-bearing | Modbus TCP / WAGO I/O | Modbus/WAGO simulator | In-process loopback adapter |
| Motion per axis, with position knowledge and fault state | Galil motion adapter | Motion simulator | In-process loopback adapter |
| Persistence | SQL Server (through the persistence boundary) | In-memory or test database | In-memory |
| Clock and time source | System clock | Deterministic test clock | Deterministic test clock |

Rules:

1. Domain logic depends only on the ports, never on a vendor library, a socket, a driver, or a
   database client.
2. Device libraries are isolated behind the ports; an adapter may use one, but the port
   definition must not expose vendor types.
3. Simulator adapters implement the same application-facing contracts as the physical
   adapters, and contract tests must fail if they diverge.
4. Physical-device selection is explicit and disabled by default; enabling it requires an
   approved future Scope Gate **and** a recorded local authorization.
5. No silent substitution: a physical profile whose adapter cannot be established fails closed
   with an explicit fault or unknown state, and must never fall back to simulated values.
6. Adapters transport, convert, validate at the protocol level, record transport evidence, and
   report the outcome of the command lifecycle. They decide nothing about eligibility,
   safety, or alarms, and they contain no UI logic.
7. Communication health is evaluated from transport evidence, never from value change
   (section 5).
8. Retries are bounded, transport-level, and re-validated; reconnection never re-issues a
   stale command and never re-energizes an output.
9. Modbus and Galil library selections remain `[OPEN]` pending licence, offline-availability,
   maintenance, and observability review. No library is installed in this Stage.

### 16.2 Command lifecycle

Every command that can reach a device is traceable through the following states where
applicable:

| State | Meaning | Performed by |
| --- | --- | --- |
| `REQUESTED` | A request exists (operator request or supervisory orchestration) | Kiosk shell / runtime orchestrator |
| `AUTHORIZED` | Permission, lifecycle, interlock, ownership, and command-state validation passed | Runtime service |
| `QUEUED` | Accepted for dispatch, in order | Runtime service |
| `ISSUED` | Transmitted to the device | Runtime service / adapter |
| `ACCEPTED` | The device acknowledged the request at protocol level | Adapter, reported to runtime |
| `EFFECTIVE` | The commanded change is observable in the process value or feedback | Runtime service, from process data |
| `FEEDBACK_CONFIRMED` | The derived state machine confirms the intended state | Runtime service |
| `FAILED` | The command failed and will not be retried without re-validation | Runtime service |
| `TIMED_OUT` | A bounded wait expired; terminal until re-validated | Runtime service |
| `CANCELLED` | Superseded or withdrawn before issue | Runtime service |
| `ABORTED` | Terminated as part of a fault response | Runtime service |

**How each transition is established for a given signal is `[NOT VERIFIED]` until bench
evidence exists.** A protocol acknowledgement is not a physical effect, and a limit switch is
not a flow proof. No command may remain in an indeterminate state indefinitely.

## 17. Data, configuration, and secrets model

### 17.1 Data access and write paths

- Only the runtime service connects to SQL Server. The UI has no database credentials and no
  database code path.
- Persistence is behind the persistence boundary: mapper as the primary technology, with a
  measured escape hatch for the high-rate Historian write path
  ([`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md)).
- The Historian write path is decoupled from control and prioritises accountability records
  above detail, detail above normal samples, and samples above aggregates.
- Configuration publication, alarm state transitions, job outcomes, and audit records are
  transactional with the state change they record.
- When SQL Server is unavailable: live supervision and live visibility continue; process
  history fails explicitly; stop and de-energize actions are never gated on the database;
  audit-required initiating actions are refused (`[PROPOSED]` set, see
  [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) item 7).
- Migrations are versioned, reviewed, executed only as an explicit offline maintenance step
  with a verified pre-change backup, and never applied automatically at service start.

### 17.2 Configuration layers

| Layer | Location | Committed |
| --- | --- | --- |
| Public-safe examples | Repository `config/examples/` (documented direction; not created) | Yes — synthetic, labelled, incomplete |
| Simulator and test fixtures | Repository test tree | Yes — public-safe and deterministic |
| Local development values | Local site directory outside the working tree | No |
| Test-hardware values | Local site directory outside the working tree | No — never derived from production |
| Production values | Local site directory outside the working tree | No |
| Secrets | Operating-system-protected local store outside the working tree | No |

### 17.3 Draft and Published configuration

- A **Draft** is editable, validated on demand, and never consumed by the runtime.
- A **Published** revision is immutable, numbered, validated, and audited, and is the only
  configuration the runtime consumes.
- Publication is one transaction: revision, validation result, and audit record.
- Validation failure blocks publication; the previous revision stays in force.
- Applying a published revision is a controlled runtime operation gated by runtime state; a
  revision must not be applied while a Cleaning Job is active, and a failed application keeps
  the previous revision in force.
- Rollback is by republishing an earlier revision. Revision history is append-only.

### 17.4 Profile identity

The active device profile (`SIMULATOR`, `TEST_HARDWARE`, `PRODUCTION`) is validated state,
displayed in the UI chrome, recorded at startup, and changeable only through a permissioned,
audited action. It is never changed implicitly.

## 18. Simulator-first development model

| Profile | Meaning | Authorization |
| --- | --- | --- |
| `SIMULATOR` | Simulated devices, the default | Permitted for development and testing |
| `TEST_HARDWARE` | Isolated bench arrangement, never the Boiler Unit installation | Separate Owner-approved Scope Gate; `[NOT AUTHORIZED]` |
| `PRODUCTION` | The installed workstation and its real devices | `[NOT AUTHORIZED]` |

- Simulator adapters implement the same ports as the physical adapters; contract parity is
  testable and required.
- The simulator must support deterministic advancement through the injectable clock, and
  repeatable failure injection: disconnection, stale data after reconnection, partial
  feedback, valve timeout during a job, pressure loss during a job, static-value false
  reassurance, exception responses, accumulating consecutive failures, position loss, and
  database unavailability.
- A physical adapter failure must never fall back to simulated values; the state is explicit
  and control-blocking.
- **Simulation verifies application logic only.** It is not hardware certification, not bench
  evidence, and not proof of fail-safe behaviour. WAGO watchdog behaviour remains
  `[NOT VERIFIED]` regardless of any simulated outcome.

## 19. Offline deployment model

- Installation and upgrade are offline from local media; no step requires an Internet
  connection or a remote service.
- Prerequisites (SQL Server 2025 Standard, and the web view runtime if it is not guaranteed on
  the image) are **detected, never downloaded**; a missing prerequisite fails closed with an
  offline-actionable remedy.
- The runtime service starts automatically and is supervised; the kiosk shell starts
  automatically on the kiosk session (mechanism `[OPEN]`).
- Restart, recovery, and upgrade never resume an AutoSequence or a Cleaning Job and never
  re-energize an output.
- Local configuration, secrets, logs, and diagnostics live outside the Git working tree in
  local application data locations.
- Backup uses the database engine's native mechanism with a mandatory pre-migration backup;
  schedule, retention, and any off-box copy remain `[OPEN]`; restore must be tested before
  deployment acceptance.
- Upgrade is a planned maintenance activity with a verified backup; rollback is by restoring
  the backup and reinstalling the previous package — schema downgrade is not relied upon.
- Communication is local-only: the Local Application API binds to loopback, no component
  listens on a reachable network interface, and no component makes outbound Internet
  connections.
- No installer, package definition, script, or CI workflow is created in Stage 0.2.

## 20. Repository structure direction (documented, not created)

The structure below is the recommended direction for a later implementation Stage Gate. It
reflects the approved architecture boundaries: presentation separated from the runtime,
domain separated from transport and storage, adapters isolated, simulators held to the same
contracts, and deployment held apart from application code.

```text
apps/
  kiosk/            Kiosk shell and the local web UI (presentation only)
  runtime/          Runtime Windows Service host, Local Application API host, composition root
packages/
  domain/           Pure domain logic: sensors, DirtyScore, queues, sequencing, alarm model
  application/      Use cases and orchestration: AutoSequence, cleaning orchestrator, dispatchers
  contracts/        Application-facing ports and contracts shared by runtime, UI, and adapters
  persistence/      SQL Server access, migrations, configuration store, historian write path
adapters/
  modbus.wago/      Modbus TCP / WAGO I/O adapter (physical; disabled by default)
  galil/            Galil motion adapter (physical; disabled by default)
  simulator/        Simulator adapters implementing the shared contracts
  time/             Clock and time-source adapters
tests/
  domain.tests/     Deterministic domain logic tests
  application.tests/Orchestration and lifecycle tests
  integration/      Simulator-backed integration tests
  ui/               UI automation and component checks
tools/              Configuration validation, diagnostics, offline build and validation helpers
deployment/         Installer definitions, service registration, kiosk startup, offline packaging
config/
  examples/         Public-safe example configuration (synthetic, labelled, incomplete)
docs/               Existing documentation, including decisions/
```

Rules that the structure expresses:

1. `packages/domain` must not reference transport, storage, UI, or vendor libraries.
2. Adapters depend on `packages/contracts` only, never on `packages/domain` internals or on
   the UI.
3. `apps/runtime` is the only composition root and the only place where adapters are wired.
4. `config/examples` never contains production values, and no production configuration tree
   exists in the repository.
5. **No directory in this structure is created by Stage 0.2.** The structure is a documented
   direction, not a reservation of names.

## 21. Stage 0.2 required decision output — answers

| # | Question | Answer | Status |
| --- | --- | --- | --- |
| 1 | What type of UI delivery is preferred? | An application-owned full-screen kiosk shell window hosting a local web UI. Browser kiosk and native desktop were evaluated; native desktop is the documented fallback | `[PROPOSED]` |
| 2 | What process owns the UI? | The kiosk shell process (interactive session); presentation only, no device session, no authoritative state | `[PROPOSED]` |
| 3 | What process owns device sessions? | The runtime Windows Service, exclusively | `[PROPOSED]` mapping; boundary `[APPROVED]` |
| 4 | Is a Local API used, and what is its boundary? | Yes — the Local Application API: loopback-only, authenticated, contract-first, hosted in-process by the runtime service, replaceable transport | `[PROPOSED]` |
| 5 | What .NET support-track target is preferred? | Long-Term Support (LTS) track; the exact version is pinned at the implementation gate with a cited support reference | `[PROPOSED]` policy; version `[OPEN]` |
| 6 | What UI technology is preferred? | Local web UI in C# with a Blazor-based component model, rendered in-process in the kiosk shell; shell host framework `[OPEN]` | `[PROPOSED]` |
| 7 | What backend framework is preferred? | ASP.NET Core minimal API hosted by the runtime service | `[PROPOSED]` |
| 8 | What Windows Service model is preferred? | One runtime Windows Service, generic host, automatic start, bounded recovery, explicit startup gating, single-instance enforcement | `[PROPOSED]` |
| 9 | What database-access strategy is preferred? | Mapper as the primary technology, narrow measured escape hatch for the high-rate Historian path, single connection owner, transactional accountability writes | `[PROPOSED]` |
| 10 | What migration strategy is preferred? | Versioned reviewed migrations, explicit offline execution only, never auto-applied at start, mandatory verified pre-change backup, forward-only preference | `[PROPOSED]` |
| 11 | What structured logging strategy is preferred? | One logging abstraction, structured local rolling files outside the working tree, no remote sink, logs kept separate from audit records; provider `[OPEN]` | `[PROPOSED]` |
| 12 | How are configuration Draft and Published states separated? | Draft is never consumed; publication is one validated, atomic, audited transaction; the runtime consumes only published revisions and applies them under a state gate | `[PROPOSED]` |
| 13 | How are secrets and Production configuration kept outside Git? | Local site directory outside the working tree plus an OS-protected local secret store; repository holds only synthetic, labelled, incomplete examples; `.gitignore` exclusions remain in force | Boundary `[APPROVED]`; mechanism `[OPEN]` |
| 14 | How is Simulator mode selected? | Through an explicit, validated, displayed, audited device profile whose default is `SIMULATOR` | `[PROPOSED]` |
| 15 | How are physical adapters disabled by default? | They are not constructed at all unless the profile explicitly selects physical operation **and** a recorded local authorization and an approved Scope Gate exist; failure is explicit, never simulated | `[PROPOSED]` |
| 16 | How does UI restart avoid owning hardware state? | The UI holds no device session and no authoritative state; the runtime continues; the shell re-reads state and replays nothing; hold-to-run stops with the session | `[PROPOSED]` |
| 17 | What happens architecturally when SQL Server is unavailable? | Live supervision and visibility continue; process history fails explicitly; stop actions are never gated on the database; audit-required initiating actions are refused (`[PROPOSED]` set); backlog and alarm policy `[OPEN]` | `[PROPOSED]` / `[OPEN]` |
| 18 | What is the offline installation direction? | Offline media, prerequisites detected and never downloaded, planned maintenance window, verified pre-change backup, versioned packages, rollback by restore and reinstall | `[PROPOSED]` |
| 19 | What repository structure should a later implementation Stage create? | The structure in section 20: `apps/`, `packages/`, `adapters/`, `tests/`, `tools/`, `deployment/`, `config/examples/` | `[PROPOSED]` — documented, not created |
| 20 | What decisions remain `[OPEN]` and why? | Section 22, with the reason and the gate that must close each item | `[OPEN]` |

## 22. Remaining open architecture items

| Item | Why it remains open | Gate that must close it |
| --- | --- | --- |
| Exact .NET LTS version and its support reference | No .NET SDK exists in the working environment; support-window facts must be observed from an authoritative source | Implementation Stage Gate |
| Shell host framework | Implementation-level selection; no evidence available now | Implementation Stage Gate |
| Web view runtime availability on the workstation image | Requires workstation image evidence; not observable here | Deployment Stage Gate |
| Modbus TCP client library | Licence, offline availability, maintenance, and observability review cannot be performed in a Stage that installs nothing | Implementation Stage Gate |
| Galil integration mechanism | Vendor interface options and their support status must be evaluated | Implementation Stage Gate |
| Logging provider and validation library | Licence and offline-availability review | Implementation Stage Gate |
| Unit-, integration-, and UI-test tooling | Licence and offline-availability review | Implementation Stage Gate / Test Stage Gate |
| EF Core and SQL Server provider versions; compatibility with SQL Server 2025 Standard | Compatibility must be verified by observation, not assumed | Implementation Stage Gate |
| Historian write-path measurement and escape-hatch decision | Requires measurement on the target workstation | Implementation Stage Gate |
| Overflow, spool, and backpressure policy for the historian queue | Requires the measured write-path design first | Implementation Stage Gate |
| Audit-required refusal set during a database outage | Needs Owner ratification of the candidate set | Owner review of Stage 0.2 |
| Secret-store mechanism and local configuration format | Mechanism depends on the local deployment model | Implementation Stage Gate |
| Service identity, privileges, and database authorization | Depends on the site's account model | Implementation Stage Gate |
| Startup resynchronization sequence details | Depends on device behaviour and commissioning values | Implementation Stage Gate, then bench verification |
| Package format, kiosk startup mechanism, firewall rules, diagnostic bundle contents | Deployment decisions that need workstation evidence | Deployment Stage Gate |
| Backup schedule, retention, and off-box copy; restore test | Requires deployment planning and evidence | Deployment Stage Gate; restore test in Test Stage Gate |
| Local equipment network topology and address plan | Confidential local deployment information | Site engineering, never committed |
| Historian physical sizing, row sizes, index and partition strategy | No capacity model exists | Before retention defaults are ratified |
| Recovery procedure for an interrupted Cleaning Job; fault-class taxonomy | Behaviour is undefined in the approved baseline | A later Stage Gate that specifies sequence recovery |
| Configuration application without a runtime restart | Alternative not yet evaluated | Implementation Stage Gate |

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
- [`decisions/ADR-0006-ui-delivery-model.md`](decisions/ADR-0006-ui-delivery-model.md) — UI
  delivery model
- [`decisions/ADR-0007-runtime-process-model.md`](decisions/ADR-0007-runtime-process-model.md)
  — runtime process model
- [`decisions/ADR-0008-technology-stack.md`](decisions/ADR-0008-technology-stack.md) —
  technology stack
- [`decisions/ADR-0009-database-access-and-migrations.md`](decisions/ADR-0009-database-access-and-migrations.md)
  — database access and migrations
- [`decisions/ADR-0010-device-adapter-boundary.md`](decisions/ADR-0010-device-adapter-boundary.md)
  — device adapter boundary
- [`decisions/ADR-0011-configuration-and-secrets.md`](decisions/ADR-0011-configuration-and-secrets.md)
  — configuration and secrets
- [`decisions/ADR-0012-simulator-first-development.md`](decisions/ADR-0012-simulator-first-development.md)
  — simulator-first development
- [`decisions/ADR-0013-offline-deployment.md`](decisions/ADR-0013-offline-deployment.md) —
  offline deployment
