# Domain Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the entities, terminology, and rules described here.
Entities marked `[OWNER CONFIRMED]` were explicitly confirmed by the Owner during the
Stage 0.1 documentation review and are binding. Field-level storage design is `[PROPOSED]`
or `[OPEN]` as marked.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 implementation **SUBMITTED FOR OWNER REVIEW**; Owner manual review
**PENDING**; **NOT MERGED**; Stage 0.3 `[NOT AUTHORIZED]`.

Stage 0.2 recorded no change to any entity, relationship, enumeration, or rule in this
document. Its architecture decisions must conform to the domain rules recorded here; where a
technology choice and a domain rule conflict, the domain rule governs.

This document defines what things are called and how they relate. It intentionally
contains no production values.

---

## 1. Terminology rules

1. **Wall versus side.** "Front" and "Rear" are boiler **wall** names *and* thermocouple
   **side** names. Documentation and identifiers must always make clear which is meant.
   The approved forms are `TC_F` (front channel) and `TC_R` (rear channel) for channel
   side, and `Left`, `Rear`, `Right`, `Front` for wall. When ambiguity is possible, write
   "wall Rear" or "channel side R". `[APPROVED]`
2. **Wall order.** Wherever walls are enumerated, the canonical order is Left, Rear,
   Right, Front. `[APPROVED]`
3. **Sensor identity.** A sensor location is identified by wall and position. A sensor
   carries two channels, which are *not* separate sensors. `[APPROVED]`
4. **Case.** Enumerated values are written in `UPPER_SNAKE_CASE`; entity and field names
   are written in `PascalCase` or `camelCase` as established by the governing document and
   must not be re-styled. `[PROPOSED]`
5. **One name per concept.** A concept has exactly one term across all documentation.
   Synonyms must be avoided or explicitly declared as aliases.
6. **Outcome versus disposition.** A Cleaning Job **outcome** describes the result of an
   executed job. A queue entry **disposition** describes what happened to an entry in a
   queue. They are different concepts and must not be conflated. `[OWNER CONFIRMED]`

## 2. Core entities

### 2.1 BoilerUnit

The physical boiler whose walls are cleaned. One application installation controls exactly
one Boiler Unit. `[APPROVED]`

### 2.2 BoilerWall

An enumerated wall: `LEFT`, `REAR`, `RIGHT`, `FRONT`. Each wall has:

| Attribute | Value | Status |
| --- | --- | --- |
| Sensor count | Left 24, Rear 28, Right 24, Front 28 | `[APPROVED]` |
| Total sensor locations | 104 | `[APPROVED]` |
| Owns one TempQueue | yes — four TempQueues in total across all walls | `[APPROVED]` |
| Owns one TimeQueue | yes — four TimeQueues in total across all walls | `[APPROVED]` |

The alignment of rows across walls of unequal height (24 versus 28 sensors) is `[OPEN]`.

### 2.3 Sensor

A single cleaning point on a wall. Each Sensor has:

| Attribute | Description | Status |
| --- | --- | --- |
| `SensorId` | Stable identifier | `[APPROVED]` — must be stable; format `[OPEN]` |
| `Wall` | Owning BoilerWall | `[APPROVED]` |
| `scanOrder` | Stable persisted position in the deterministic physical scan sequence | `[APPROVED]` |
| `Enabled` | Whether the sensor participates in evaluation | `[APPROVED]` |
| `DiffLowerBound`, `DiffUpperBound` | Bounds for the DirtyScore linear mapping | `[APPROVED]` — per-sensor values `[NOT VERIFIED]` |
| `UseDirtyScoreThreshold` | Whether the threshold gates eligibility | `[APPROVED]` |
| `DirtyScoreThreshold` | Threshold value used when enabled | `[APPROVED]` — value `[NOT VERIFIED]` |
| `HardMinimumCleaningInterval` | Minimum interval before requeue; applies to TempQueue and TimeQueue | `[APPROVED]` — value `[NOT VERIFIED]`; default expectation approximately two hours |
| `LastSuccessfulCleaningCompletedAt` | Explicit UTC timestamp; never null | `[APPROVED]` |
| `LastCleaningTimestampSource` | Provenance of the last timestamp | `[APPROVED]` |
| `HasVerifiedCleaningHistory` | Whether verified cleaning history exists | `[APPROVED]` |
| `AssignedWaterJet` | The Water Jet that cleans this sensor | `[APPROVED]` — mapping `[NOT VERIFIED]` |
| `AssignedIsolationValve` | The Isolation Valve serving this sensor | `[OWNER CONFIRMED]` — **derived from `AssignedWaterJet`**, not independently assigned |
| `PathCoordinatesP1..P6` | Six configurable path coordinates | `[APPROVED]` — values `[NOT VERIFIED]` |
| `Inhibited` | Whether the sensor is inhibited | `[APPROVED]` as a concept; semantics `[OPEN]` |

Rules:

- A Sensor never appears twice in GlobalQueue. `[APPROVED]`
- A Sensor that is disabled, inhibited, or invalid is never used for a Cleaning Job.
  `[APPROVED]`
- A Sensor whose valve is `OUT_OF_SERVICE` is excluded from TempQueue, TimeQueue,
  GlobalQueue, and candidate refill. `[APPROVED]`
- Multiple Sensors may be assigned to one Water Jet, and all of them use that Water Jet's
  dedicated Isolation Valve. `[OWNER CONFIRMED]`

### 2.4 ThermocoupleChannel

One of two channels belonging to a Sensor: `TC_F` or `TC_R`. There are 208 channels in
total across 104 sensors. `[APPROVED]`

### 2.5 DirtyScore

A derived, dimensionless value in the range 0 to 100, computed from the two channel
temperatures. Defined in section 5.

### 2.6 WaterJet

One of eight cleaning assemblies. Each has a horizontal X axis and a vertical Y axis, and
each has **exactly one dedicated Isolation Valve**. A Water Jet is reserved for the duration
of a Cleaning Job. `[OWNER CONFIRMED]`

### 2.7 GalilController

One of four motion controllers, model DMC-B140-M, driving stepper motors with encoder
feedback. Each controls two Water Jets using axes A, B, C, and D. The approved axis
pattern for a controller is: A is Water Jet 1 horizontal X, B is Water Jet 1 vertical Y,
C is Water Jet 2 horizontal X, D is Water Jet 2 vertical Y. Controllers are reached over a
**local equipment network**; the exact topology is `[OPEN]` / `[NOT VERIFIED]`. `[APPROVED]`

### 2.8 IsolationValve

An energize-to-open valve. The relationship with Water Jet is **exactly one-to-one**:
Water Jet 1 to Isolation Valve 1 through Water Jet 8 to Isolation Valve 8.
`[OWNER CONFIRMED]`

| Rule | Statement |
| --- | --- |
| Ownership | Each Isolation Valve serves exactly one Water Jet |
| Sharing | An Isolation Valve is never shared between Water Jets |
| Derivation | A sensor's Isolation Valve is derived from the sensor's assigned Water Jet |
| Change | A future approved scope would be required to change this model |

The valve's state is *derived* from two limit switches; the derived states are `CLOSED`,
`OPEN`, `TRANSIT_OR_FAULT`, and `INVALID_LIMIT_STATE`. It may additionally be marked
`OUT_OF_SERVICE` by the operator under the idle valve fault workflow. See
[`REQUIREMENTS.md`](REQUIREMENTS.md) VLV-002 and VLV-006.

### 2.9 MainPump

The cleaning water pump. No direct motor-running feedback is available; state is derived
from command state, pressure value, pressure quality, pressure-ready setpoint, and pressure
rise timeout. See [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-002 and PMP-003.

### 2.10 AutoSequence

An operator-started supervised cleaning session with a defined lifecycle: it starts the
pump, waits for pressure readiness and stable dwell, runs a next-job countdown, dispatches
Cleaning Jobs from GlobalQueue, and ends when the Operator stops it or a blocking fault
requires it.

Sequential rules `[OWNER CONFIRMED]`:

- An AutoSequence executes Cleaning Jobs **strictly sequentially**. At most one Cleaning Job
  may be ACTIVE at any time.
- The selected eligible head of GlobalQueue is the only normal source for the next Cleaning
  Job.
- A new AutoSequence seeds a **new** GlobalQueue from current data. It does not reload the
  previous executable queue; the previous Queue is preserved only as history and evidence.
- An AutoSequence instance is closed when the Operator stops it, and its GlobalQueue is not
  preserved as the executable Queue for a future AutoSequence. See
  [`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 9.

### 2.11 CleaningJob

One Cleaning Job cleans exactly one sensor, using its assigned Water Jet, that Water Jet's
dedicated Isolation Valve, and six path coordinates, under one reserved Water Jet. Full
specification in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md).

A Cleaning Job has exactly one **outcome**: `COMPLETED`, `FAILED`, `ABORTED`, or
`RECOVERY_REQUIRED`. Only one Cleaning Job may be ACTIVE at a time. `[OWNER CONFIRMED]`

### 2.12 DcsPermissiveOverride

An Operator-activated, manually released override that bypasses **only** the approved DCS
permissive evaluation. `[OWNER CONFIRMED]`

| Attribute | Behaviour |
| --- | --- |
| Activation | Explicit Operator action with a confirmation step |
| Expiry | None. Remains active until manually released |
| Reason | Required on activation and on release |
| Visibility | Persistent visible banner while active |
| Recording | Activation, release, user, timestamp, and reason in Event and Audit history |
| Attribution | Subject to shared Operator-account limitations |
| Scope | DCS permissive evaluation only |

The override must never be described as a general "Ignore DCS" function, and it must not
bypass any item listed in [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009.

### 2.13 Queues

| Entity | Cardinality | Ordering | Status |
| --- | --- | --- | --- |
| TempQueue | one per wall — **four in total** | DirtyScore descending, then scanOrder ascending, **within the wall** | `[APPROVED]` |
| TimeQueue | one per wall — **four in total** | TimeSinceLastClean descending, then scanOrder ascending | `[APPROVED]` |
| GlobalQueue | one per Boiler Unit | FIFO after seeding and deduplication; target capacity eight unique entries | `[APPROVED]` |

There are therefore **eight source queues in total** (four TempQueues plus four
TimeQueues) and one GlobalQueue. See [`QUEUE_MODEL.md`](QUEUE_MODEL.md).

Each GlobalQueue entry has exactly one **source owner**: the source that established the
preserved earliest GlobalQueue position. Merged reason flags do not change ownership.
`[OWNER CONFIRMED]`

### 2.14 Alarm

An alarm has three independent dimensions — condition, acknowledgement, and shelving — and
one of five severities. Acknowledgement of an ACTIVE alarm is awareness only; a separate
cleared-state acknowledgement is required to release a block. See
[`ALARM_MODEL.md`](ALARM_MODEL.md).

### 2.15 Event and AuditRecord

An Event records an operator or system action; an AuditRecord records an authoritative
change. Both carry timestamp, actor, subject, and reason. Required fields for queue actions
are listed in [`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 7; for timestamp corrections in
[`REQUIREMENTS.md`](REQUIREMENTS.md) TSB-007; for the DCS Permissive Override in
[`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-006.

### 2.16 AppUser and Role

A local application user with one or more roles. A role is a configurable permission
collection. See [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md).

## 3. Relationships

| From | To | Cardinality | Notes |
| --- | --- | --- | --- |
| BoilerUnit | BoilerWall | 1 to 4 | Fixed set: Left, Rear, Right, Front |
| BoilerWall | Sensor | 1 to 24 or 28 | Counts fixed by the approved baseline |
| Sensor | ThermocoupleChannel | 1 to 2 | `TC_F` and `TC_R` |
| Sensor | WaterJet | many to 1 | Mapping is `[NOT VERIFIED]` |
| **WaterJet** | **IsolationValve** | **1 to 1** | `[OWNER CONFIRMED]` — dedicated, never shared |
| Sensor | IsolationValve | many to 1 | Derived from the sensor's assigned Water Jet |
| WaterJet | GalilController | many to 1 | Two Water Jets per controller |
| GalilController | Axis | 1 to 4 | A, B, C, D |
| AutoSequence | CleaningJob | 1 to many | **Strictly sequential**: at most one Cleaning Job ACTIVE at a time |
| CleaningJob | Sensor | 1 to 1 | Exactly one sensor per job |
| GlobalQueue | Sensor | 0 to 8 | Unique sensors only |

**Maximum active Cleaning Jobs equals one.** Different Water Jets, different Isolation
Valves, different boiler walls, and different Galil controllers do not grant authority for
concurrent Cleaning Jobs. Parallel Water Jet cleaning is prohibited. `[OWNER CONFIRMED]`

## 4. Enumerations

| Enumeration | Values | Status |
| --- | --- | --- |
| `BoilerWall` | `LEFT`, `REAR`, `RIGHT`, `FRONT` | `[APPROVED]` |
| `ThermocoupleSide` | `F`, `R` | `[APPROVED]` |
| `LastCleaningTimestampSource` | `INITIAL_BASELINE`, `IMPORTED_HISTORY`, `COMPLETED_JOB`, `MANUAL_CORRECTION` | `[APPROVED]` |
| `ValveState` | `CLOSED`, `OPEN`, `TRANSIT_OR_FAULT`, `INVALID_LIMIT_STATE`, plus `OUT_OF_SERVICE` marker | `[APPROVED]` |
| `PumpState` | `STOPPED`, `START_REQUESTED`, `PRESSURIZING`, `PRESSURE_READY`, `RUNNING_CONFIRMED_BY_PRESSURE`, `STOP_REQUESTED`, `FAULT`, `UNKNOWN` | `[APPROVED]` |
| `MotionFeedbackMode` | `OPEN_LOOP`, `ENCODER_VERIFIED` | `[APPROVED]` |
| `MotionProfileType` | `MANUAL_JOG`, `MANUAL_MOVE`, `CLEANING_JOB` | `[APPROVED]` |
| `AlarmSeverity` | `CRITICAL`, `HIGH`, `MEDIUM`, `LOW`, `INFORMATION` | `[APPROVED]` |
| `AlarmCondition` | `ACTIVE`, `CLEARED` | `[APPROVED]` |
| `AlarmAcknowledgement` | `UNACKNOWLEDGED`, `ACKNOWLEDGED` — with a distinct cleared-state acknowledgement | `[OWNER CONFIRMED]` |
| `AlarmShelving` | `UNSHELVED`, `SHELVED` | `[APPROVED]` |
| `QueueSource` | `TEMP_LEFT`, `TEMP_REAR`, `TEMP_RIGHT`, `TEMP_FRONT`, `TIME_LEFT`, `TIME_REAR`, `TIME_RIGHT`, `TIME_FRONT` | `[APPROVED]` |
| `QueuePositionSourceReason` | `TEMP_QUEUE`, `TIME_QUEUE` | `[APPROVED]` |
| `CleaningJobOutcome` | `COMPLETED`, `FAILED`, `ABORTED`, `RECOVERY_REQUIRED` | `[OWNER CONFIRMED]` |
| `QueueEntryDisposition` | `HELD`, `RELEASED`, `REJECTED`, `REORDERED`, `REMOVED_BY_ELIGIBILITY`, `REMOVED_BY_EQUIPMENT_EXCLUSION` | `[OWNER CONFIRMED]` |

`REJECTED` is an Operator queue action and a queue entry disposition. It is **not** a
Cleaning Job outcome, because a rejected entry is never executed as a Cleaning Job.

## 5. DirtyScore rules

`DiffTemp = TC_F - TC_R`

```text
if DiffTemp <= DiffLowerBound:      DirtyScore = 100
if DiffTemp >= DiffUpperBound:      DirtyScore = 0
otherwise:                          DirtyScore = 100 * (DiffUpperBound - DiffTemp)
                                                 / (DiffUpperBound - DiffLowerBound)
```

Rules `[APPROVED]`:

1. `DiffUpperBound` must be strictly greater than `DiffLowerBound`.
2. DirtyScore is clamped to the interval 0 to 100.
3. Invalid bound configuration prevents configuration publication.
4. The runtime must not silently correct invalid values.
5. When `TC_F <= TC_R`: continue calculating DirtyScore, raise a diagnostic condition,
   preserve both temperature values in the Historian, and do not automatically block queue
   eligibility solely because of the diagnostic.

DirtyScore ordering applies **within a wall's TempQueue**. GlobalQueue initial order comes
from the fixed source order, not from a cross-wall DirtyScore comparison. `[OWNER CONFIRMED]`

### 5.1 Dirty and Cleaner display classification

`DirtyScore` is a continuous 0–100 value. The operator interface additionally presents a
two-state **process classification** derived from it, so that a cell can be read at a glance:

| Condition | Display classification |
| --- | --- |
| Dirty Score > 50 | `DIRTY` |
| Dirty Score <= 50 | `CLEANER` |

Rules:

1. The classification rule above is the current **Owner-confirmed display rule**.
2. The UI must read the **effective threshold from the Published configuration** if the
   threshold becomes configurable, and must not hard-code visual text around an assumed value.
   The site value, if it differs from the current rule, is local configuration and is never
   committed.
3. **Dirty red is a process-condition colour, not an alarm severity colour** (see
   [`ALARM_MODEL.md`](ALARM_MODEL.md) severities and
   [`ARCHITECTURE.md`](ARCHITECTURE.md) section 26.3). Alarm state remains
   independently recognisable through icon, border, text, the alarm banner, and the dedicated
   alarm surfaces.
4. Classification must never be derived from a raw or unvalidated value: the quality-aware
   pipeline in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 27 governs what reaches the UI.
5. Wall summary counts presented alongside this classification must carry explicit labels
   (Dirty, Cleaner, and Bad or unavailable where applicable), never unlabelled numbers.

## 6. TimeSinceLastClean

`TimeSinceLastClean = CurrentTime - LastSuccessfulCleaningCompletedAt`

- `LastSuccessfulCleaningCompletedAt` is never null and every sensor receives an explicit
  initial timestamp during configuration. `[APPROVED]`
- The initial timestamp is a valid scheduling baseline; there is no null case and no
  `NEVER_CLEANED` state. `[APPROVED]`
- Only a successful Cleaning Job updates the timestamp. A failed or aborted job does not.
  `[APPROVED]`

## 7. Identity and stability rules

1. `SensorId` and `scanOrder` must be stable across configuration publications, because
   queue tie-breaking and historical records depend on them. `[APPROVED]`
2. Renaming a sensor must not silently change its identity or its history. `[PROPOSED]`
3. Configuration publication must be atomic and auditable. `[PROPOSED]`
4. Deleting a sensor with history must require explicit authority and must preserve its
   historical records. `[PROPOSED]`

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register (WJV, SEQ, QUE, OVR groups)
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue specification
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — Cleaning Job specification and sequencing
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — alarm model and cleared-state acknowledgement
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — subsystem layout; sensor presentation model
  (section 26) and quality-aware pipeline (section 27)
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — roles and permissions
