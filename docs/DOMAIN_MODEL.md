# Domain Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the entities, terminology, and rules described here.
Entities marked `[OWNER CONFIRMED]` were explicitly confirmed by the Owner during the
Stage 0.1 documentation review and are binding. Field-level storage design is `[PROPOSED]`
or `[OPEN]` as marked.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**COMPLETE FOR DEVELOPMENT CHECKPOINT** (Owner-local final Edge gate **PASS**, Owner manual
review **PASS**), **PR #3 MERGED** (merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`). **React selected as the
Primary UI Framework** (Owner decision, 2026-10-07; Production transport and chart library remain
`[OPEN]`). Blazor counter-spike **NOT REQUIRED** unless a future material blocker is identified.
Main Development Scope Gate **PENDING** (0.3B+). **Stage 0.3A Scope Gate APPROVED** (Owner Option-C amended gate).
**Stage 0.3A-1 SOURCE CHECKPOINT AUTHORED** — **Owner-local .NET validation PENDING** — **PR #4 OPEN — NOT READY FOR MERGE**.
**Stage 0.3A-2 NOT AUTHORIZED**. Production device access **NOT AUTHORIZED**.

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
| Sensor count | Left 24, Rear 30, Right 24, Front 30 | `[OWNER CONFIRMED]` — Owner critical topology correction (2026-10-12): I7 and I16 are normal Product Sensors. Historical: Rear 29 / Front 29 (Stage 0.2.1A) and the earlier Rear 28 / Front 28. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. |
| Total Product Sensors | **108** | `[OWNER CONFIRMED]` — Owner critical topology correction. Historical: 106 (Stage 0.2.1A–0.4C-K3) and the earlier 104. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. |
| Owns one TempQueue | yes — four TempQueues in total across all walls | `[APPROVED]` |
| Owns one TimeQueue | yes — four TimeQueues in total across all walls | `[APPROVED]` |

#### 2.2.1 Logical Sensor matrix (Owner critical topology correction) `[OWNER CONFIRMED]`

<a id="221-logical-sensor-matrix-and-cannon-slots-owner-confirmed"></a>
<a id="221-logical-sensor-matrix-and-the-two-non_sensor_gap-positions-owner-confirmed"></a>

*The legacy anchors above are retained so that links authored before the corrections keep
resolving to this section. The section title no longer uses the superseded "Cannon slots"
wording (ADR-0017) nor the superseded "two `NON_SENSOR_GAP` positions" wording (Owner critical
topology correction, 2026-10-12; see
[`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md)).*

One logical matrix of **18 logical columns × 6 logical rows = 108 logical positions**. **Every
logical position is a Product Sensor: the matrix holds 108 Product Sensors.** The Sensor
sequence is exactly, in this order and with no holes, replacement identities or renumbering:

| Logical row (top to bottom) | Logical labels |
| --- | --- |
| 1 | G+201 … G+218 |
| 2 | G+101 … G+118 |
| 3 | G1 … G18 |
| 4 | H1 … H18 |
| 5 | I1 … I18 — **I7 and I16 included, once each, in their normal ordinal position** |
| 6 | J1 … J18 |

| Wall | Logical columns | Wall grid | Product Sensors |
| --- | --- | --- | --- |
| Left | 1–4 | 4 columns × 6 rows | 24 |
| Rear | 5–9 | 5 columns × 6 rows | 30 (I7 is an ordinary Sensor at wall row 5, wall column 3) |
| Right | 10–13 | 4 columns × 6 rows | 24 |
| Front | 14–18 | 5 columns × 6 rows | 30 (I16 is an ordinary Sensor at wall row 5, wall column 3) |

- **I7 and I16 are normal Product Sensors.** They have a Sensor ID, two Thermocouple channels
  (Front and Rear), a Dirty Score and classification, quality, a scan order, the same parameter
  schema and validation as every other Sensor, the same Enabled/Disabled behaviour, and they
  are eligible for TempQueue, TimeQueue, GlobalQueue, selection and Cleaning Job targeting
  exactly like any other Sensor. The Sensor sequence runs I6 → I7 → I8 and I15 → I16 → I17.
- **No `NON_SENSOR_GAP` position exists.** The arithmetic identity is 108 Sensors × 2
  measurements = **216 assigned Product TC channels**; six TC Nodes × 40 channels = **240
  physical TC channels**; 240 − 216 = **24 unassigned physical channels**. The central
  invariant check (`packages/commissioning/src/topologyInvariants.mjs`) refuses any layer —
  catalogue, sequence, unique IDs, Front/Rear pairing, physical schedule, UI map, runtime
  counts — that disagrees with these numbers or still carries a `NON_SENSOR_GAP` position.
- **Front/Rear measurement identity for I7 and I16** comes from the committed Drawing schedule
  and the existing mapping records, not from invention: I7 Front `TC-NODE-05|A102.4|7`, I7 Rear
  `TC-NODE-05|A102.4|8`, I16 Front `TC-NODE-06|A102.4|7`, I16 Rear `TC-NODE-06|A102.4|8`
  (`docs/evidence/tc-drawing-schedule-rev1.csv`). The four Drawing rows previously treated as
  Drawing/Product-topology conflicts are used Product assignments; the 12 unrelated Drawing
  internal conflicts stay unresolved.
- **Water Jets are separate domain entities (WJ1–WJ8)** — see §2.6 — each paired 1:1 with a
  dedicated Isolation Valve **IV1–IV8 by ordinal** (§2.8). Nothing in the matrix owns, contains,
  or is a Water Jet; no matrix position is a Water Jet slot, a Cannon slot, or an equipment slot.
- **Installed Position and Target Coverage are separate concepts.** A Water Jet is installed on
  one wall and covers the *opposite* wall in the matching region. Canonical installed topology
  (Owner Canonical Topology Adoption, 2026-10-21; §2.6 table): **WJ1 is installed FRONT/UPPER,
  targets REAR/UPPER and pairs with IV1**; **WJ3 is installed REAR/UPPER, targets FRONT/UPPER and
  pairs with IV3** — WJ1–WJ4 form the UPPER tier and WJ5–WJ8 the LOWER tier. The *fine-grained*
  placement of **all eight** Water Jets is `UNRESOLVED` with no placement anchors: the earlier
  "at the I16 / I7 gap anchor" descriptions stay revoked and the stale opposite-tier anchors of
  the other six are removed; no replacement anchor is inferred, mirrored or transferred. Only
  the fine-grained placement is unresolved — the canonical installed wall / tier and Target
  Coverage are authoritative.
- **Assigned Cleaning Device identity is ordinal and is never inferred from the wall a Sensor is
  located on.** The legacy `cannon` column of `sensorparam.csv` means **Assigned Cleaning Device
  ID** — a direct ordinal reference `cannon n → WJn` (WJ1–WJ8), *not* a grid position, *not* an
  installed wall, and *not* a "nearest or same-wall Water Jet". Assignment follows responsibility
  for the **target** wall and region, so a Rear-lower Sensor (I7 included) is assigned WJ5 even
  though WJ5 is installed on the Front-lower wall. Canonical default assignment by physical
  Sensor wall / tier: Right/Upper → WJ1/IV1 (8 Sensors), Front/Upper → WJ2/IV2 (10),
  Left/Upper → WJ3/IV3 (8), Rear/Upper → WJ4/IV4 (10), Rear/Lower → WJ5/IV5 (20),
  Front/Lower → WJ6/IV6 (20), Right/Lower → WJ7/IV7 (16), Left/Lower → WJ8/IV8 (16) —
  108 in total, I7 → WJ5/IV5 and I16 → WJ6/IV6 included.
- Wall rows are the logical rows; there is no rotation or reversal. The question of row
  alignment across walls of unequal height is resolved by this matrix.
- The labels are a logical reference. They do **not** define Modbus addresses, register maps,
  physical coordinates, Water Jet assignments, or any Production mapping, all of which remain
  confidential deployment values and `[NOT VERIFIED]`.
- *Superseded wording (historical):* Stage 0.2.1A labelled I7 and I16 "Cannon equipment slots"
  (logical I7 "Rear Cannon", logical I16 "Front Cannon"); ADR-0017 (2026-10-08) then recorded
  them as `NON_SENSOR_GAP` placement anchors (I7 → WJ3, I16 → WJ1) with 106 Sensors and 212
  channels, and the Sensor sequence "skipping" them. Both are superseded and are preserved only
  inside dated historical records and in ADR-0017's own supersession history. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.
- *.NET status:* the .NET contracts, domain catalogue, importer, runtime, synthetic map and
  fixtures encode the corrected 108 / 216 baseline (Owner-validated at Stage 0.4C-K3) and,
  as of the Owner-approved canonical Equipment topology adoption (2026-10-25), the canonical
  installed topology,
  canonical Target Coverage, canonical default assignments and the all-`UNRESOLVED`
  fine-grained placement disposition. The Adoption's own .NET build / test / fixture-parity
  validation is the pending Owner-local gate (no .NET SDK in the Agent environment); no
  result is claimed here before that gate runs.

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
| `AssignedWaterJet` | The Water Jet that cleans this sensor — the **Assigned Cleaning Device**, an ordinal reference to one of WJ1–WJ8 (the legacy `cannon` column). Resolved by responsibility for the **target** wall and region, never inferred from the wall the Sensor sits on (§2.2.1) | `[APPROVED]` — mapping `[NOT VERIFIED]` |
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

One of two channels belonging to a Sensor: `TC_F` or `TC_R`. There are **216 assigned Product
TC channels** in total across **108 Sensors** (I7 and I16 included), out of 240 physical TC
channels (24 unassigned). `[OWNER CONFIRMED]` — Owner critical topology correction (2026-10-12).
Historical: 212 channels across 106 Sensors (Stage 0.2.1A) and the earlier 208 across 104. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.

### 2.5 DirtyScore

A derived, dimensionless value in the range 0 to 100, computed from the two channel
temperatures. Defined in section 5.

### 2.6 WaterJet

One of eight cleaning assemblies — **WJ1 to WJ8**, separate equipment entities that are never
Sensor-matrix positions. Each has a horizontal X axis and a vertical Y axis, and
each has **exactly one dedicated Isolation Valve** (IV*n* by ordinal, §2.8). A Water Jet is
reserved for the duration of a Cleaning Job. `[OWNER CONFIRMED]`

**Installed Position and Target Coverage are separate attributes.** A Water Jet is installed on
one wall and covers the *opposite* wall in the matching region:

Canonical installed topology (Owner-approved canonical Equipment topology adoption,
2026-10-25). For every row
the *fine-grained* placement is `UNRESOLVED` with no placement anchors (see below):

| Water Jet | Installed Position (canonical wall / tier) | Target Coverage | Dedicated Isolation Valve |
| --- | --- | --- | --- |
| WJ1 | LEFT / UPPER | RIGHT / UPPER | IV1 |
| WJ2 | REAR / UPPER | FRONT / UPPER | IV2 |
| WJ3 | RIGHT / UPPER | LEFT / UPPER | IV3 |
| WJ4 | FRONT / UPPER | REAR / UPPER | IV4 |
| WJ5 | FRONT / LOWER | REAR / LOWER | IV5 |
| WJ6 | REAR / LOWER | FRONT / LOWER | IV6 |
| WJ7 | LEFT / LOWER | RIGHT / LOWER | IV7 |
| WJ8 | RIGHT / LOWER | LEFT / LOWER | IV8 |

No matrix position is a Water Jet or any other equipment; I7 and I16 are Sensors (Owner
critical topology correction, §2.2.1), so the former "gap anchor" placement of WJ1 and WJ3 no
longer describes a matrix position. Owner placement disposition (Canonical Topology
Adoption): the fine-grained placement of **all eight** Water Jets is `UNRESOLVED` with **no**
placement anchors — WJ1 / WJ3 keep the revoked-gap disposition and the stale anchors formerly
carried by WJ2, WJ4, WJ5, WJ6, WJ7 and WJ8 (which described the opposite installed tier) are
removed. No anchor is inferred, mirrored, transferred or generated; Target-zone Sensor IDs
are never installed-position anchors. Only the fine-grained placement is unresolved: the
canonical installed wall / tier above and Target Coverage are authoritative, and current
wording must never present the wall / tier itself as unknown. The approved table lives in
`packages/domain/WaterJetTopologyCatalog.cs`; ADR-0017 decisions 5 and 7 are superseded in
this respect by the Adoption (see the ADR-0017 addendum). Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.

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
| `QueueEntryDisposition` | ~~`HELD`~~ (superseded), `RELEASED`, `REJECTED`, `REORDERED`, `REMOVED_BY_ELIGIBILITY`, `REMOVED_BY_EQUIPMENT_EXCLUSION` | `[OWNER CONFIRMED]`; `HELD` **SUPERSEDED** (Owner decision, 2026-10-06) |

`REJECTED` is an Operator queue action and a queue entry disposition. It is **not** a
Cleaning Job outcome, because a rejected entry is never executed as a Cleaning Job.

**Queue-level `HELD` is superseded** (Owner decision, 2026-10-06; see
[`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2). The GlobalQueue holds ready-to-dispatch entries only.
A pause before dispatch is an AutoSequence state; a pause during a Cleaning Job is a Cleaning
Job state; equipment waits are never queue entry states. Production Pause / Resume semantics
remain pending Owner approval.

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
