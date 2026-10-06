# Requirements — WaterJet Sentinel Suite (WJSS)

**Document status:** Requirements marked `[APPROVED]` were approved by the Owner.
Requirements marked `[OWNER CONFIRMED]` were explicitly confirmed by the Owner during the
Stage 0.1 documentation review and are binding. Requirements marked otherwise are not
approved behaviour.

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

Requirement identifiers are stable. Requirements are never silently deleted; if one is
superseded, its status changes and the superseding requirement is recorded.

---

## Reading this register

- **Status** uses the legend in [`CURRENT_STATE.md`](CURRENT_STATE.md).
- **Source** names the governing document section that establishes the requirement.
- "Must" statements are obligations on the future implementation. They are not evidence
  that anything is built.

---

## 1. Governance requirements

| ID | Requirement | Status |
| --- | --- | --- |
| GOV-001 | The project must operate under the authority order in [`AGENTS.md`](../AGENTS.md) section 1: latest Owner instruction, approved Stage Gate, [`AGENTS.md`](../AGENTS.md), [`CURRENT_STATE.md`](CURRENT_STATE.md), approved acceptance criteria, approved ADRs, [`ARCHITECTURE.md`](ARCHITECTURE.md), [`REQUIREMENTS.md`](REQUIREMENTS.md), [`MASTER_PLAN.md`](MASTER_PLAN.md), [`ROADMAP.md`](ROADMAP.md), then older proposals. A `PROPOSED` ADR does not rank in that order, and the roadmap never authorises implementation. | `[APPROVED]` |
| GOV-002 | An inconsistency between governing sources must stop the affected part of the work, be documented, and must not be resolved by silently choosing a behaviour. | `[APPROVED]` |
| GOV-003 | Undocumented behaviour must not be treated as approved behaviour. | `[APPROVED]` |
| GOV-004 | Claims of success must be supported by observed tool output. | `[APPROVED]` |
| GOV-005 | Merge authority belongs exclusively to the Owner. Contributors must not merge. | `[APPROVED]` |
| GOV-006 | Force push, `reset --hard`, `clean -fd`, rebasing of approved checkpoints, and history rewriting are prohibited unless separately authorised. | `[APPROVED]` |
| GOV-007 | Every stage report must state CHANGED, UNCHANGED, and NOT VERIFIED items. | `[APPROVED]` |
| GOV-008 | Approval of a Stage Scope Gate must be recorded separately from acceptance of that stage's implementation. A gate being approved does not mean its implementation is accepted. | `[APPROVED]` |
| GOV-009 | Documentation must not describe a stage implementation as Owner accepted, approved, final, or merged unless the Owner has explicitly recorded that status. | `[APPROVED]` |
| GOV-010 | No application version or runtime release version exists until the Owner establishes one. Documentation versioning policy is `[OPEN]`. | `[APPROVED]` |

## 2. Product identity and deployment

| ID | Requirement | Status |
| --- | --- | --- |
| IDN-001 | The product name is WaterJet Sentinel Suite, short name WJSS, repository name `waterjet-sentinel-suite`. | `[APPROVED]` |
| IDN-002 | The product is an automated boiler wall water-jet cleaning monitoring and supervisory control system. | `[APPROVED]` |
| IDN-003 | One Windows 11 Pro workstation per Boiler Unit. | `[APPROVED]` |
| IDN-004 | One application installation controls exactly one Boiler Unit. | `[APPROVED]` |
| IDN-005 | The main operating mode is a full-screen Control Room Kiosk with controlled navigation. | `[APPROVED]` |
| IDN-006 | Applicable pages must support workstation-responsive layouts. | `[APPROVED]` |
| IDN-007 | The default application language is English. Thai may be used as supplementary contextual explanation where necessary. | `[APPROVED]` |
| IDN-008 | The application uses local application users. | `[APPROVED]` |
| IDN-009 | The initial system is standalone and must not depend on Internet access. | `[APPROVED]` |
| IDN-010 | The application must not be presented as a Safety Instrumented System, nor as a replacement for emergency stop circuits, hardwired protection, motor protection, mechanical limits, or controller-side safe-stop behaviour. | `[APPROVED]` |
| IDN-011 | No claim of certification or compliance with IEC, ISA, ISO, or any other standard may be made. Standards are guidance and design inspiration only. | `[APPROVED]` |
| IDN-012 | UI delivery technology (desktop, browser-based, or hybrid local web) is not decided by product identity. Kiosk operation, controlled navigation, the operational close guard, responsive workstation pages, and Windows 11 Pro deployment are the binding requirements. The framework choice remains `[OPEN]`. | `[APPROVED]` |

## 3. Priorities

| ID | Requirement | Status |
| --- | --- | --- |
| PRI-001 | Prioritise, in order: reliability, stability, deterministic behaviour, recoverability, operator usability, maintainability, appropriate baseline security. | `[APPROVED]` |

## 4. Physical system baseline

| ID | Requirement | Status |
| --- | --- | --- |
| PHY-001 | Boiler walls are Left, Rear, Right, and Front. Wall sensor counts are Left 24, Rear 29, Right 24, Front 29, giving 106 sensor locations, laid out in an 18-column × 6-row logical matrix with two Cannon equipment slots (logical I7 Rear, logical I16 Front) that are not sensors. See [`DOMAIN_MODEL.md` §2.2.1](DOMAIN_MODEL.md#221-logical-sensor-matrix-and-cannon-slots-owner-confirmed). Supersedes the earlier 104-location baseline (Rear 28, Front 28). | `[OWNER CONFIRMED]` |
| PHY-002 | Each sensor has two thermocouple channels: a front channel (`TC_F`) and a rear channel (`TC_R`), giving 212 channels in total (supersedes the earlier 208). | `[OWNER CONFIRMED]` |
| PHY-003 | Documentation and identifiers must distinguish the boiler wall name from the thermocouple side, because "Front" and "Rear" are both wall names and channel side names. | `[APPROVED]` |
| PHY-004 | There are eight Water Jet assemblies. Each Water Jet has a horizontal X axis and a vertical Y axis. | `[APPROVED]` |
| PHY-005 | One Galil controller controls two Water Jets; four controllers are expected in total, model DMC-B140-M, using axes A, B, C, and D. | `[APPROVED]` |
| PHY-006 | The axis mapping per controller follows the pattern: axis A is Water Jet 1 horizontal X, axis B is Water Jet 1 vertical Y, axis C is Water Jet 2 horizontal X, axis D is Water Jet 2 vertical Y. | `[APPROVED]` |
| PHY-007 | Production IP addresses, motion coordinates, travel limits, pulses per engineering unit, speeds, accelerations, and decelerations must not be invented. | `[APPROVED]` |
| PHY-008 | Galil controllers are reached over a **local equipment network**. The exact network topology is `[OPEN]` / `[NOT VERIFIED]`. Production IP addresses and topology remain outside the public repository. | `[APPROVED]` |

## 5. Water Jet and Isolation Valve assignment

| ID | Requirement | Status |
| --- | --- | --- |
| WJV-001 | The relationship between Water Jet and Isolation Valve is exactly one-to-one: Water Jet 1 to Isolation Valve 1, through Water Jet 8 to Isolation Valve 8. | `[OWNER CONFIRMED]` |
| WJV-002 | Each Water Jet has exactly one dedicated Isolation Valve. Each Isolation Valve serves exactly one Water Jet. An Isolation Valve is never shared between Water Jets. | `[OWNER CONFIRMED]` |
| WJV-003 | Multiple Sensors may be assigned to one Water Jet. | `[OWNER CONFIRMED]` |
| WJV-004 | Sensors assigned to a Water Jet use that Water Jet's dedicated Isolation Valve. Sensor-to-Isolation-Valve assignment is therefore derived from the Sensor's assigned Water Jet, unless a future approved scope explicitly changes the model. | `[OWNER CONFIRMED]` |
| WJV-005 | The Water Jet to Isolation Valve cardinality is settled and must not be recorded as `[OPEN]` or described as an assumption. | `[OWNER CONFIRMED]` |
| WJV-006 | The specific Sensor-to-Water-Jet mapping is deployment data. It is `[NOT VERIFIED]` here and must not be invented. | `[APPROVED]` |

## 6. Sequential execution invariants

| ID | Requirement | Status |
| --- | --- | --- |
| SEQ-001 | **INVARIANT-SEQ-001.** At most one Cleaning Job may be ACTIVE within one WaterJet Sentinel Suite installation at any time. | `[OWNER CONFIRMED]` |
| SEQ-002 | **INVARIANT-SEQ-002.** A second Cleaning Job must not enter an executing state until the current Cleaning Job has reached an approved safe and released terminal condition. | `[OWNER CONFIRMED]` |
| SEQ-003 | **INVARIANT-SEQ-003.** Different Water Jets, different Isolation Valves, different boiler walls, or different Galil controllers do not grant authority for concurrent Cleaning Jobs. | `[OWNER CONFIRMED]` |
| SEQ-004 | **INVARIANT-SEQ-004.** The selected eligible head of GlobalQueue is the only normal source for the next Cleaning Job. | `[OWNER CONFIRMED]` |
| SEQ-005 | **INVARIANT-SEQ-005.** Queue refill, score changes, Operator Reorder, Reject, valve exclusion, or equipment availability must never result in concurrent Cleaning Jobs. | `[OWNER CONFIRMED]` |
| SEQ-006 | **INVARIANT-SEQ-006.** Parallel Water Jet cleaning is prohibited. | `[OWNER CONFIRMED]` |
| SEQ-007 | The normal dispatch cycle must be: select one eligible entry from the front of GlobalQueue; execute one complete Cleaning Job; confirm the valve is closed; return the Water Jet to Standby; confirm the Job reaches its approved terminal condition; remove or resolve the completed entry; shift the remaining FIFO entries; refill the tail using the source-owner rule; apply the next-job countdown and required gates; only then may the next Cleaning Job begin. | `[OWNER CONFIRMED]` |
| SEQ-008 | The Main Pump may remain running between Cleaning Jobs during an active AutoSequence, but only one Water Jet may execute a Cleaning Job at a time. | `[OWNER CONFIRMED]` |
| SEQ-009 | The phrase "allow unaffected Water Jets to continue" describes **sequential** continuation only: after the valve fault workflow is resolved and continuation is authorized, the AutoSequence may later select a sequential Cleaning Job assigned to another available Water Jet. It must never imply concurrent operation. | `[OWNER CONFIRMED]` |
| SEQ-010 | No statement anywhere in the documentation may describe Cleaning Job concurrency or parallel Water Jet cleaning as `[OPEN]`. | `[OWNER CONFIRMED]` |

## 7. Modbus and WAGO

| ID | Requirement | Status |
| --- | --- | --- |
| COM-001 | Communication is Modbus TCP only. | `[APPROVED]` |
| COM-002 | The canonical internal address convention is zero-based. | `[APPROVED]` |
| COM-003 | The baseline function codes to support are FC01, FC02, FC03, FC04, FC05, FC06, FC15, and FC16. | `[APPROVED]` |
| COM-004 | Approximate I/O scale is 37 DI, 31 DO, 35 AI, and 1 AO. The implementation must not hard-limit configuration to those approximate counts. | `[APPROVED]` |
| COM-005 | The WAGO Modbus TCP Coupler is model 750-362. | `[APPROVED]` |
| COM-006 | Production Tag Lists, register maps, addresses, IP addresses, setpoints, and production configuration are confidential local deployment information, must not be committed, and must not be published. Only public-safe examples may be committed. | `[APPROVED]` |
| COM-007 | The exact fail-safe behaviour of the WAGO watchdog is unverified and must be treated as `[NOT VERIFIED]`. | `[APPROVED]` |
| COM-008 | The register map, tag naming, addressing granularity, and data-type conversion rules are not specified at this stage. | `[OPEN]` |
| COM-009 | The application reads DCS-originated hardwired signals through WAGO Modbus TCP. It must not connect directly to the DCS. | `[APPROVED]` |

## 8. Communication and acquisition health

| ID | Requirement | Status |
| --- | --- | --- |
| COMH-001 | Acquisition health must consider, at minimum: TCP connection state; Modbus request completion; valid response receipt; Modbus exception response; request timeout; consecutive failure count; last successful poll time; poll-cycle lateness or overrun; and signal quality state. | `[APPROVED]` |
| COMH-002 | A configurable stale timeout is required. An example operational value of 30 seconds may be used as an example only; it must remain configurable and must never be treated as a fixed production value. | `[APPROVED]` |
| COMH-003 | Communication health must not depend solely on value change detection. A process value remaining unchanged is not, by itself, proof that communication is lost, because a digital input, pressure value, or temperature may legitimately remain constant. | `[APPROVED]` |
| COMH-004 | A future heartbeat or watchdog signal feature may be designed, but its exact hardware contract remains `[OPEN]` / `[NOT VERIFIED]` until approved and tested. | `[OPEN]` |
| COMH-005 | Quality state must be published explicitly. Bad, stale, or unknown quality must never be substituted with a benign value. | `[APPROVED]` |
| COMH-006 | If a DCS-related communication or required stale-data condition becomes blocking while **no** Cleaning Job is active: raise the applicable alarm; stop the next-job countdown; do not dispatch a new Cleaning Job; require recovery of the condition; and require acknowledgement of the CLEARED state before the countdown resumes. | `[APPROVED]` |
| COMH-007 | If the condition becomes blocking while a Cleaning Job is **active**: allow the current Cleaning Job to reach its approved completion or fault-handling terminal condition according to the approved process policy; do not dispatch the next Cleaning Job; raise or retain the alarm; stop the next-job countdown after the current Job; and require condition recovery and cleared-state acknowledgement before continuing. | `[APPROVED]` |
| COMH-008 | Specific production registers and heartbeat addresses must never be invented. | `[APPROVED]` |

## 9. Temperature and Dirty Score

| ID | Requirement | Status |
| --- | --- | --- |
| TMP-001 | `DiffTemp = TC_F - TC_R` for each sensor. | `[APPROVED]` |
| TMP-002 | Each sensor must carry DiffLowerBound, DiffUpperBound, UseDirtyScoreThreshold, DirtyScoreThreshold, HardMinimumCleaningInterval, LastSuccessfulCleaningCompletedAt, LastCleaningTimestampSource, and HasVerifiedCleaningHistory. | `[APPROVED]` |
| TMP-003 | DirtyScore must be produced by linear mapping: 100 when `DiffTemp <= DiffLowerBound`; 0 when `DiffTemp >= DiffUpperBound`; otherwise `100 * (DiffUpperBound - DiffTemp) / (DiffUpperBound - DiffLowerBound)`. | `[APPROVED]` |
| TMP-004 | `DiffUpperBound` must be strictly greater than `DiffLowerBound`. | `[APPROVED]` |
| TMP-005 | DirtyScore must be clamped to the range 0 through 100. | `[APPROVED]` |
| TMP-006 | Invalid bound configuration must prevent configuration publication. | `[APPROVED]` |
| TMP-007 | The runtime must not silently correct invalid values. | `[APPROVED]` |
| TMP-008 | If `TC_F <= TC_R`, the system must continue calculating DirtyScore, raise a diagnostic condition, preserve both temperature values in the Historian, and must not automatically block queue eligibility solely because of this diagnostic. | `[APPROVED]` |
| TMP-009 | When UseDirtyScoreThreshold is true, `DirtyScore >= DirtyScoreThreshold` is eligible for TempQueue and `DirtyScore < DirtyScoreThreshold` is not. When UseDirtyScoreThreshold is false, the threshold must not block TempQueue eligibility. | `[APPROVED]` |
| TMP-010 | HardMinimumCleaningInterval must apply to both TempQueue and TimeQueue eligibility. The default operational expectation is approximately two hours, configurable per sensor. | `[APPROVED]` |
| TMP-011 | The production value of each HardMinimumCleaningInterval, DiffLowerBound, DiffUpperBound, and DirtyScoreThreshold is a commissioning value and is `[NOT VERIFIED]`. | `[APPROVED]` |

## 10. Cleaning timestamp baseline

| ID | Requirement | Status |
| --- | --- | --- |
| TSB-001 | `LastSuccessfulCleaningCompletedAt` must never be null. Every sensor must receive an explicit initial timestamp during configuration. | `[APPROVED]` |
| TSB-002 | Timestamps must be stored in UTC and displayed in the UI in Asia/Bangkok. | `[APPROVED]` |
| TSB-003 | Each sensor must carry LastSuccessfulCleaningCompletedAt, LastCleaningTimestampSource, and HasVerifiedCleaningHistory. | `[APPROVED]` |
| TSB-004 | Allowed `LastCleaningTimestampSource` values are INITIAL_BASELINE, IMPORTED_HISTORY, COMPLETED_JOB, and MANUAL_CORRECTION. | `[APPROVED]` |
| TSB-005 | Initial configuration uses `LastCleaningTimestampSource = INITIAL_BASELINE` and `HasVerifiedCleaningHistory = false`. | `[APPROVED]` |
| TSB-006 | After a successful Cleaning Job, `LastCleaningTimestampSource = COMPLETED_JOB` and `HasVerifiedCleaningHistory = true`. | `[APPROVED]` |
| TSB-007 | Manual correction must require permission and produce an audit record containing sensor, previous timestamp, new timestamp, user, time, and reason. | `[APPROVED]` |
| TSB-008 | Null timestamps, a synthetic NEVER_CLEANED state, and hidden fallback dates must not be used. | `[APPROVED]` |

## 11. Queue requirements

Full behaviour is specified in [`QUEUE_MODEL.md`](QUEUE_MODEL.md).

| ID | Requirement | Status |
| --- | --- | --- |
| QUE-001 | Each boiler wall has exactly one TempQueue and one TimeQueue. This yields four TempQueues and four TimeQueues: **eight source queues in total**, plus one GlobalQueue. | `[APPROVED]` |
| QUE-002 | TempQueue eligibility requires: sensor enabled; sensor configuration valid; HardMinimumCleaningInterval satisfied; threshold condition satisfied when threshold is enabled; entry dwell satisfied. | `[APPROVED]` |
| QUE-003 | Global configuration includes TempQueueEntryDwellSeconds and TempQueueRemovalDwellSeconds, defaulting to 10 seconds each, independently configurable. | `[APPROVED]` |
| QUE-004 | Entry requires the sensor to meet TempQueue criteria continuously for the entry dwell; loss of the condition before dwell completion must reset the entry dwell. | `[APPROVED]` |
| QUE-005 | Removal requires the queued sensor to fail TempQueue criteria continuously for the removal dwell; becoming dirty again before the removal dwell completes must keep it queued. | `[APPROVED]` |
| QUE-006 | TempQueue sorting must be highest DirtyScore first, with stable scanOrder as the tie-break, evaluated **within each wall**. | `[APPROVED]` |
| QUE-007 | TimeQueue eligibility requires `TimeSinceLastClean >= HardMinimumCleaningInterval`, where `TimeSinceLastClean = CurrentTime - LastSuccessfulCleaningCompletedAt`. | `[APPROVED]` |
| QUE-008 | TimeQueue sorting must be longest TimeSinceLastClean first, with stable scanOrder as the tie-break. | `[APPROVED]` |
| QUE-009 | GlobalQueue target capacity is eight unique sensor entries. | `[APPROVED]` |
| QUE-010 | Initial seeding order is Temp Left, Temp Rear, Temp Right, Temp Front, Time Left, Time Rear, Time Right, Time Front. GlobalQueue initial order is determined by this fixed source order, not by a cross-wall DirtyScore comparison. | `[APPROVED]` |
| QUE-011 | After seeding and deduplication, GlobalQueue is FIFO. | `[APPROVED]` |
| QUE-012 | Changes to DirtyScore or TimeSinceLastClean must not reorder existing GlobalQueue entries automatically. | `[APPROVED]` |
| QUE-013 | A Sensor must not appear more than once in GlobalQueue. | `[APPROVED]` |
| QUE-014 | On duplicate proposal, the occurrence at the earliest current GlobalQueue position is preserved, all applicable source reasons are recorded on that preserved entry, and the later duplicate source advances to its next eligible candidate. This continues until eight unique entries are present or no eligible unique candidates remain. | `[APPROVED]` |
| QUE-015 | Each GlobalQueue entry has exactly one **source owner** for tail-refill purposes: the source that established the preserved earliest GlobalQueue position. Merged source reasons do not transfer or change source ownership. | `[OWNER CONFIRMED]` |
| QUE-016 | Operator Reorder changes dispatch position but not source ownership. When an entry leaves GlobalQueue, tail refill uses that entry's original source owner. | `[OWNER CONFIRMED]` |
| QUE-017 | GlobalQueue may contain fewer than eight entries only when fewer than eight unique eligible sensors are available. | `[APPROVED]` |
| QUE-018 | The system must never duplicate a sensor, bypass HardMinimumCleaningInterval, use a disabled or inhibited sensor, or create a dummy job. | `[APPROVED]` |
| QUE-019 | When an entry leaves GlobalQueue, remaining entries shift forward preserving FIFO order, and the tail is refilled from the source owner of the entry that left. If no eligible candidate exists, the tail remains empty. | `[APPROVED]` |
| QUE-020 | ~~Hold / Release Hold~~ **SUPERSEDED (Owner decision, 2026-10-06)**: the GlobalQueue holds ready-to-dispatch entries only and is dispatched head-only (no entry is held or skipped); pause belongs to the AutoSequence or the Cleaning Job lifecycle ([`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2). Earlier approved wording for the remaining actions: Reject removes the entry and suppresses the sensor from refill for the current Auto Sequence; Release Reject; Reorder. How Reject / Reorder apply to the ready-only head-only queue is **OWNER DECISION REQUIRED**. | `[APPROVED]` (Hold part superseded) |
| QUE-021 | Every operator queue action must produce an Event record containing timestamp, user, action, sensor, reason, original position, new position when applicable, Auto Sequence ID, and queue snapshot reference. | `[APPROVED]` |
| QUE-022 | Reorder must not move an active Cleaning Job, must not bypass eligibility revalidation, and must not alter source ownership. | `[APPROVED]` |
| QUE-023 | Suppression created by Reject ends when a new Auto Sequence is created unless released earlier. | `[APPROVED]` |
| QUE-024 | On Operator stop of an AutoSequence: stop dispatching new Cleaning Jobs; handle any active Cleaning Job according to the approved stop or recovery policy; record the current GlobalQueue snapshot in Event history; record Rejected and Reordered state in Event history (queue-level Held superseded, Owner decision 2026-10-06); close the current AutoSequence instance; and do not preserve the old GlobalQueue as the executable Queue for a future AutoSequence. | `[OWNER CONFIRMED]` |
| QUE-025 | On starting a new AutoSequence: read current process and configuration data; recalculate Dirty Scores; recalculate TimeSinceLastClean; re-evaluate HardMinimumCleaningInterval; rebuild all TempQueues; rebuild all TimeQueues; seed a new GlobalQueue; apply deduplication and refill; do not reload the previous executable GlobalQueue; and preserve the previous Queue only as history and evidence. | `[OWNER CONFIRMED]` |
| QUE-026 | The previous Queue snapshot must be preserved in Event history as evidence of the previous AutoSequence. | `[APPROVED]` |
| QUE-027 | Queue refill, score changes, Operator Reorder, Reject, valve exclusion, and equipment availability must never produce concurrent Cleaning Jobs. See SEQ-005. | `[OWNER CONFIRMED]` |

## 12. Deterministic scan order

| ID | Requirement | Status |
| --- | --- | --- |
| SCN-001 | The deterministic physical scanning sequence begins from the upper row and progresses downward. | `[APPROVED]` |
| SCN-002 | Within each row, the wall order is Left, then Rear, then Right, then Front. | `[APPROVED]` |
| SCN-003 | The system must account for different wall sensor counts: Left and Right have 24 each, Front and Rear have 28 each. | `[APPROVED]` |
| SCN-004 | scanOrder must be stable persisted configuration and must not depend on current UI sorting. | `[APPROVED]` |
| SCN-005 | The rule for aligning rows between walls of unequal height (24 versus 28), and whether scanOrder is authored explicitly or derived, is not specified. | `[OPEN]` |

## 13. Cleaning Job and sequence

Full behaviour is specified in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md).

| ID | Requirement | Status |
| --- | --- | --- |
| CLJ-001 | One Cleaning Job represents one sensor. | `[APPROVED]` |
| CLJ-002 | Each sensor is assigned one Water Jet, one Isolation Valve, six configurable path coordinates, one per-sensor HardMinimumCleaningInterval, and its temperature and queue parameters. Sensor-to-valve assignment is derived from the assigned Water Jet (WJV-004). | `[OWNER CONFIRMED]` |
| CLJ-003 | The normal sequence must follow the nineteen steps recorded in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md), from selecting the GlobalQueue head to continuing the next-job countdown when no blocking condition exists. | `[APPROVED]` |
| CLJ-004 | Coordinates and timing values must not be invented. | `[APPROVED]` |
| CLJ-005 | A failed job must not update `LastSuccessfulCleaningCompletedAt`, must block the next job, and must wait for Operator action. | `[APPROVED]` |
| CLJ-006 | AutoSequence must execute Cleaning Jobs strictly sequentially, per the SEQ invariants. | `[OWNER CONFIRMED]` |
| CLJ-007 | A Cleaning Job must not begin unless the previous Cleaning Job has reached an approved safe and released terminal condition. | `[OWNER CONFIRMED]` |

## 14. Isolation Valve

| ID | Requirement | Status |
| --- | --- | --- |
| VLV-001 | The Isolation Valve is energize-to-open; DO OFF moves the valve toward the closed state. | `[APPROVED]` |
| VLV-002 | Derived feedback must be: upper 0 and lower 1 is CLOSED; upper 1 and lower 0 is OPEN; upper 0 and lower 0 is TRANSIT_OR_FAULT; upper 1 and lower 1 is INVALID_LIMIT_STATE. | `[APPROVED]` |
| VLV-003 | Opening and closing timeouts must be configurable. Their production values are `[NOT VERIFIED]`. | `[APPROVED]` / `[NOT VERIFIED]` |
| VLV-004 | During an active Cleaning Job, valve verification failure requires: stop or abort motion according to the approved fault class; command Valve OFF; set VFD AO to 0 Hz or stop the Main Pump; raise a blocking alarm; mark the Job FAILED or RECOVERY_REQUIRED; do not update the last successful cleaning timestamp; block the next job; wait for Operator action. | `[APPROVED]` |
| VLV-005 | While the Main Pump is running and no Cleaning Job is active, abnormal valve feedback must raise an alarm, pause the next-job countdown, and present a modal to the Operator identifying the affected valve, Water Jet, and sensors. | `[APPROVED]` |
| VLV-006 | The Operator must be offered: Stop All, which stops the Auto Sequence, cancels the countdown, stops the pump or commands VFD AO to 0 Hz, preserves the Queue snapshot, and records an Event and reason; or Continue With Valve Excluded, which marks the valve OUT_OF_SERVICE, excludes every associated sensor from TempQueue, TimeQueue, GlobalQueue, and candidate refill, allows **sequential** continuation on other available Water Jets, shows a persistent degraded-operation banner, and records the Event, user, valve, sensors, and Queue snapshot. | `[OWNER CONFIRMED]` |
| VLV-007 | Returning a valve to service requires valid closed feedback, a cleared alarm, acknowledgement of the cleared state, an explicit Operator return-to-service action, and runtime validation. | `[APPROVED]` |
| VLV-008 | Sensors re-entering normal source queue evaluation after a valve return to service must not be inserted into the middle of GlobalQueue. | `[APPROVED]` |
| VLV-009 | Valve exclusion must never permit a concurrent Cleaning Job on another Water Jet. See SEQ-005. | `[OWNER CONFIRMED]` |

## 15. Main Pump

| ID | Requirement | Status |
| --- | --- | --- |
| PMP-001 | The Main Pump must support manual start and stop, and Auto Sequence operation. | `[APPROVED]` |
| PMP-002 | The system does not receive direct motor-running feedback; pump state must be derived from start/stop command state, pressure transmitter value, pressure quality, pressure-ready setpoint, and pressure rise timeout. | `[APPROVED]` |
| PMP-003 | Required conceptual states are STOPPED, START_REQUESTED, PRESSURIZING, PRESSURE_READY, RUNNING_CONFIRMED_BY_PRESSURE, STOP_REQUESTED, FAULT, and UNKNOWN. | `[APPROVED]` |
| PMP-004 | In an Auto Sequence the system must start the pump, wait for the pressure setpoint and a stable dwell, begin the countdown, keep the pump running between Cleaning Jobs, and stop only when the Operator stops the sequence or a blocking fault requires it. | `[APPROVED]` |
| PMP-005 | Pressure values, the pressure-ready setpoint, the stable dwell, and the pressure rise timeout must not be invented. Production values are `[NOT VERIFIED]`. | `[APPROVED]` / `[NOT VERIFIED]` |
| PMP-006 | The pump state model does not constitute motor protection and must not be presented as such. | `[APPROVED]` |
| PMP-007 | No application-level operational permissive may block a valid Main Pump stop request. Actual command execution remains subject to command-path availability, communication availability, external authority, Local/Remote state, hardware state, current Production Write authorization, and independent protection behaviour. This is an intent about process permissives; it is not a claim that the application can physically execute a stop under every possible failure condition. | `[OWNER CONFIRMED]` |

## 16. Galil motion

| ID | Requirement | Status |
| --- | --- | --- |
| GAL-001 | The Galil controller model is DMC-B140-M, driving stepper motors with encoder feedback. | `[APPROVED]` |
| GAL-002 | Required conceptual feedback modes are OPEN_LOOP and ENCODER_VERIFIED. | `[APPROVED]` |
| GAL-003 | ENCODER_VERIFIED must not be described as true closed-loop unless future engineering verification confirms that encoder feedback is used for active correction. | `[APPROVED]` |
| GAL-004 | Motion profiles are global by operation type: Manual Jog, Manual Command Move, and Cleaning Job. | `[APPROVED]` |
| GAL-005 | Motion mode or profile changes must not be allowed while the Main Pump is running, an Auto Sequence is active, a Cleaning Job is active, an axis is moving, the position is unknown, or a valve is open. | `[APPROVED]` |
| GAL-006 | Mechanical limits, soft limits, pulses per engineering unit, encoder behaviour, speed, acceleration, deceleration, homing, and operational envelope are commissioning values and remain `[NOT VERIFIED]`. | `[NOT VERIFIED]` |

## 17. Alarm model

| ID | Requirement | Status |
| --- | --- | --- |
| ALM-001 | Severities are Critical, High, Medium, Low, and Information. | `[APPROVED]` |
| ALM-002 | Condition, acknowledgement, and shelving are independent dimensions: ACTIVE or CLEARED; UNACKNOWLEDGED or ACKNOWLEDGED; UNSHELVED or SHELVED. | `[APPROVED]` |
| ALM-003 | An ACTIVE alarm may record Operator awareness or acknowledgement if the model supports it, but acknowledging an ACTIVE alarm does not clear the alarm and does not release any block. | `[OWNER CONFIRMED]` |
| ALM-004 | When an ACTIVE condition transitions to CLEARED, final-clearance acknowledgement must be pending. The final-clearance acknowledgement becomes UNACKNOWLEDGED on that transition, and the Operator must acknowledge the CLEARED state. | `[OWNER CONFIRMED]` |
| ALM-005 | A blocking alarm releases its block only when the condition is CLEARED, the **cleared-state** acknowledgement is ACKNOWLEDGED, and no other blocking condition remains. | `[OWNER CONFIRMED]` |
| ALM-006 | The sequence ACTIVE + ACKNOWLEDGED, then condition clears, then CLEARED + ACKNOWLEDGED automatically, then block releases without a post-clear acknowledgement, must not be possible. | `[OWNER CONFIRMED]` |
| ALM-007 | A cleared but unacknowledged alarm must display "RETURNED TO NORMAL - ACK REQUIRED". | `[APPROVED]` |
| ALM-008 | Shelving must support a configurable duration, a per-alarm maximum, allowed permissions, a required reason, auto-unshelve, a shelved alarm list, and audit and Event records. | `[APPROVED]` |
| ALM-009 | Critical hardware and safety-related alarms may be configured as non-shelvable. | `[APPROVED]` |

## 18. DCS Permissive Override

| ID | Requirement | Status |
| --- | --- | --- |
| OVR-001 | The Operator is permitted to activate the **DCS Permissive Override**. It must never be described as a general "Ignore DCS" function. | `[OWNER CONFIRMED]` |
| OVR-002 | The override remains active until manually released. There is no automatic time expiry in the current approved baseline. | `[OWNER CONFIRMED]` |
| OVR-003 | Activation requires an explicit action and a confirmation step. | `[OWNER CONFIRMED]` |
| OVR-004 | A reason must be recorded on activation and on release. | `[OWNER CONFIRMED]` |
| OVR-005 | A persistent visible banner must be shown while the override is active. | `[OWNER CONFIRMED]` |
| OVR-006 | Activation, release, user, timestamp, and reason must be recorded in Event and Audit history. | `[OWNER CONFIRMED]` |
| OVR-007 | Shared Operator-account limitations apply: records identify the account, not the individual. See [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md). | `[APPROVED]` |
| OVR-008 | The override may bypass **only** the approved DCS permissive evaluation. | `[OWNER CONFIRMED]` |
| OVR-009 | The override must not bypass: WAGO communication health; Modbus transport health; Isolation Valve feedback; valve open or close verification; Main Pump pressure validation; Galil limits; motion faults; encoder or position validation; emergency stop; Local/Remote selector; motor or drive protection; WAGO output watchdog; external hardware protection; critical application lifecycle gates; or the one-active-Cleaning-Job invariant. | `[OWNER CONFIRMED]` |
| OVR-010 | The override must not be configurable to bypass any item in OVR-009. | `[OWNER CONFIRMED]` |
| OVR-011 | **The Operator role template has permission to activate and release the DCS Permissive Override.** The role-permission model remains configurable, but the approved baseline assignment is Operator. | `[OWNER CONFIRMED]` |
| OVR-012 | A role that does not hold the Override permission cannot activate or release the override. | `[OWNER CONFIRMED]` |
| OVR-013 | Holding the Override permission does not weaken, narrow, or modify the exclusion list in OVR-009. The exclusion list is not configurable through a permission. | `[OWNER CONFIRMED]` |
| OVR-014 | Activation and release of the override require Event and Audit records, per OVR-006 and [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md). | `[OWNER CONFIRMED]` |
| OVR-015 | Future authorised configuration may change role-permission assignments. The baseline assignment remains Operator until an explicit Owner decision changes it. | `[OWNER CONFIRMED]` |

## 19. Operations UI close guard

| ID | Requirement | Status |
| --- | --- | --- |
| UIG-001 | The normal Operations UI close action must be blocked while a Cleaning Job is active. | `[OWNER CONFIRMED]` |
| UIG-002 | The normal Operations UI close action must be blocked while the Main Pump is running. | `[OWNER CONFIRMED]` |
| UIG-003 | A rejected close request must be rejected with a clear explanation and must direct the Operator back to the active operation or Pump/Sequence state. | `[OWNER CONFIRMED]` |
| UIG-004 | The purpose is to prevent accidental normal UI shutdown during active operation. | `[OWNER CONFIRMED]` |
| UIG-005 | The UI guard is an operational usability control. It is **not** a safety protection, and it cannot guarantee protection against process termination, Windows shutdown, workstation restart, power loss, or hardware failure. The Equipment Runtime lifecycle, WAGO watchdog, safe output states, and external hardware protection remain independent requirements. | `[OWNER CONFIRMED]` |
| UIG-006 | The UI close guard must never be documented or reasoned about as hardware fail-safe protection. | `[OWNER CONFIRMED]` |

## 20. Users and permissions

Full details are in [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md).

| ID | Requirement | Status |
| --- | --- | --- |
| USR-001 | Default role templates are Operator, Technician, Engineer, Supervisor, Administrator, and Break-glass Recovery Account. | `[APPROVED]` |
| USR-002 | Roles are configurable permission collections. | `[APPROVED]` |
| USR-003 | A shared Operator account is allowed, and documentation must state that this prevents person-level Operator attribution. | `[APPROVED]` |
| USR-004 | Privileged sessions must support a configurable inactivity timeout. | `[APPROVED]` |
| USR-005 | On privileged-session timeout the system must log out the privileged user and return to the Operator session, must not abort an active Auto Sequence, must stop any manual hold-to-run operation, and must preserve or safely handle configuration drafts. | `[APPROVED]` |
| USR-006 | The break-glass account is for recovery only, must not be used for normal operation, must create a high-severity audit event on every login, must not bypass hardware safety, and its credentials must never be committed. | `[APPROVED]` |
| USR-007 | The DCS Permissive Override requires an explicit permission, and activating it must be audited with user, timestamp, and reason. | `[OWNER CONFIRMED]` |

## 21. Historian and retention

Full details are in [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md).

| ID | Requirement | Status |
| --- | --- | --- |
| HIS-001 | The database target is SQL Server 2025 Standard. | `[APPROVED]` |
| HIS-002 | Storage baseline is three 2 TB HDDs in RAID 5, approximately 4 TB theoretical usable capacity before formatting and overhead. This is a description of raw configuration, not a capacity guarantee. | `[APPROVED]` |
| HIS-003 | Baseline intervals: Modbus acquisition 1 second; queue evaluation 1 second; normal thermocouple historian 5 seconds; active Cleaning Job detailed historian 1 second; alarm-related detailed storage 1 second; long-term aggregate 1 minute. | `[APPROVED]` |
| HIS-004 | Default retention is proposed as: raw temperature 300 days; one-minute aggregate 1,095 days; cleaning jobs 5 years; alarm history 3 years; event history 2 years; audit history 5 years; diagnostics 180 days; temporary exports 30 days. | `[PROPOSED]` |
| HIS-005 | Retention values must be configurable separately by data category. | `[APPROVED]` |
| HIS-006 | Cleanup must use scheduled cutoff-based deletion or partition maintenance. Retention must not be described as directly overwriting Day 1 with Day 301. | `[APPROVED]` |
| HIS-007 | Audit history must be retained at least as long as event history. | `[PROPOSED]` |
| HIS-008 | Effective usable capacity is `[NOT VERIFIED]`. The dominant data category is `[NOT VERIFIED]` until a capacity model is completed. | `[NOT VERIFIED]` |
| HIS-009 | Row size, index overhead, compression, detailed-window duration, transaction-log allocation, backup allocation, and query requirements remain to be measured. | `[OPEN]` |
| HIS-010 | Data volume, partition scheme, index strategy, backup method, and integrity scheme are not defined. | `[OPEN]` |

## 22. Public repository boundary

| ID | Requirement | Status |
| --- | --- | --- |
| PUB-001 | The public repository may contain source documentation, future source code, simulator definitions, dummy configuration, example tags, generic example motion profiles, and automated tests. | `[APPROVED]` |
| PUB-002 | The public repository must not contain production IP addresses, production Tag Lists, production register maps, actual motion coordinates, actual travel limits, production speed profiles, pressure setpoints, production alarm thresholds, user databases, password hashes, credentials, production connection strings, production logs, or plant-sensitive information. | `[APPROVED]` |
| PUB-003 | Production configuration remains local and should be stored outside the Git working tree where practical. | `[APPROVED]` |
| PUB-004 | Stage 0.1 may create only example filenames or documentation references for configuration. Production configuration content must not be created. | `[APPROVED]` |

## 23. Hardware safety boundary

| ID | Requirement | Status |
| --- | --- | --- |
| HSB-001 | WAGO watchdog capability has been identified, but exact fail-safe behaviour is `[NOT VERIFIED]`. | `[NOT VERIFIED]` |
| HSB-002 | Production valve and pump write control is prohibited until bench tests verify: Ethernet disconnection moves outputs to a safe state; Equipment Runtime termination moves outputs to a safe state; workstation reboot does not leave outputs energized; WAGO reboot defaults outputs safely; watchdog timeout matches configuration; VFD AO reaches the 0 Hz safe command; stale commands do not execute after reconnection; outputs do not automatically re-energize after communication recovery. | `[NOT AUTHORIZED]` |
| HSB-003 | Target safe states are Isolation Valve DO OFF, Main Pump command OFF, and VFD AO 0 Hz. | `[APPROVED]` |
| HSB-004 | The UI close guard is not an acceptable sole protection against an energized output remaining active. See UIG-005. | `[OWNER CONFIRMED]` |
| HSB-005 | Hardware emergency stop, limit switches, motor protection, the Local/Remote selector, and independent protection remain outside the application's authority. The application must never command, override, bypass, suppress, or replace an external protection function. | `[OWNER CONFIRMED]` |
| HSB-006 | Where read-only indication of an external protection state is available, the application may monitor, display, and use that indication as a supervisory command gate. The indication does not transfer ownership of the external protection function, is not a substitute for the actual protection, and unknown, unavailable, stale, or bad-quality indication must never be inferred as safe. | `[OWNER CONFIRMED]` |

## 24. Test and verification

| ID | Requirement | Status |
| --- | --- | --- |
| TST-001 | Documentation stages perform documentation-only validation; runtime, build, database, hardware, and device tests are not part of a documentation stage. | `[APPROVED]` |
| TST-002 | Queue arbitration, dirty score computation, deduplication, refill, source ownership, sequential-execution invariants, communication-health evaluation, alarm cleared-state acknowledgement, and override restrictions are to be covered by deterministic automated tests once code exists. | `[PROPOSED]` |
| TST-003 | Hardware fail-safe behaviour must be verified on the bench before any production write control is enabled. | `[NOT AUTHORIZED]` |

## 25. Architecture and process boundaries (Stage 0.2)

This group records the architecture boundaries established by the approved Stage 0.2 Scope
Gate, *Technology and Solution Architecture Decision*. Boundaries stated as mandatory by that
approved gate are `[APPROVED]`. Selections made during Stage 0.2 are `[PROPOSED]` and require
Owner acceptance; they are recorded as ADR candidates in
[`decisions/README.md`](decisions/README.md).

| ID | Requirement | Status |
| --- | --- | --- |
| ARC-001 | The UI must not write to hardware directly. | `[APPROVED]` |
| ARC-002 | The UI must not own device sessions. | `[APPROVED]` |
| ARC-003 | Only the approved Equipment Runtime boundary may own physical device sessions. | `[APPROVED]` |
| ARC-004 | Device adapters must not contain UI logic, and must not decide eligibility, sequencing, or safety. | `[APPROVED]` |
| ARC-005 | Hardware commands must pass through authorization, lifecycle, interlock, ownership, and command-state validation before dispatch. That validation is performed in the runtime service. | `[APPROVED]` |
| ARC-006 | No architecture choice may permit concurrent Cleaning Jobs, and the one-active-Cleaning-Job gate must be enforced in a single place that no queue, operator, or adapter action can bypass. | `[APPROVED]` |
| ARC-007 | Domain logic must not depend directly on a vendor library; device libraries must be isolated behind application-owned interfaces. | `[APPROVED]` |
| ARC-008 | Simulator adapters must implement the same application-facing contracts as the physical adapters, and contract parity must be verifiable. | `[APPROVED]` |
| ARC-009 | Physical-device selection must be explicit and disabled by default. Enabling a physical adapter requires an approved future Scope Gate and a recorded local authorization; a configuration edit alone is never authorization. | `[APPROVED]` |
| ARC-010 | No silent substitution: a physical profile whose adapter cannot be established must fail closed with an explicit fault or unknown state, and must never fall back to simulated values presented as plant data. | `[APPROVED]` |
| ARC-011 | Commands must distinguish the requested, authorized, queued, issued, accepted, effective, feedback-confirmed, failed, timed-out, cancelled, and aborted states where applicable. How each transition is proven for a given signal remains `[NOT VERIFIED]` until bench evidence exists. | `[APPROVED]` |
| ARC-012 | Reconnection must never re-issue a stale command, and must never re-energize an output. | `[APPROVED]` |
| ARC-013 | Production configuration, secrets, production IP addresses, production Tag Lists, register maps, actual motion coordinates, production thresholds, and setpoints must remain outside the public repository. | `[APPROVED]` |
| ARC-014 | Public example configuration must contain public-safe synthetic values only, must be labelled as examples, and must be incomplete by design. | `[APPROVED]` |
| ARC-015 | Configuration publication must be conceptually separate from editing a Draft, and the runtime must consume an explicitly Published configuration, never a partially edited Draft. | `[APPROVED]` |
| ARC-016 | Simulator mode is the default development mode; physical adapters are disabled by default; test-hardware access is separate from Production Device access. | `[APPROVED]` |
| ARC-017 | Simulation must be deterministic where practical, and must never be presented as hardware certification. | `[APPROVED]` |
| ARC-018 | Stage 0.2 creates no runtime artefact, installs no dependency, contacts no device, creates no database object, and produces no ZIP or release. | `[APPROVED]` |
| ARC-019 | Preferred UI delivery: an application-owned full-screen kiosk shell window hosting a local web UI as static local assets in an embedded WebView, with the close guard enforced by the shell. This delivery requirement does not name the framework; the framework is recorded in UIF-003 (React selected as the Primary UI Framework, Owner decision 2026-10-07). | `[PROPOSED]` — [`ADR-0006`](decisions/ADR-0006-ui-delivery-model.md) |
| ARC-020 | Preferred process model: one kiosk shell process and one runtime Windows Service that owns all device sessions, command validation, job execution, queue evaluation, persistence, and database connectivity. | `[PROPOSED]` — [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) |
| ARC-021 | Preferred technology stack for the runtime, the Local Application API, and the persistence layer: .NET on the Long-Term Support track, C#, ASP.NET Core minimal API, built-in dependency injection and configuration, structured local logging, and offline packaging from a local package source. The exact .NET version is pinned at the implementation gate. **The UI framework is not decided by this requirement.** | `[PROPOSED]` — [`ADR-0008`](decisions/ADR-0008-technology-stack.md) |
| ARC-022 | Preferred data access, stated in architecture language: transactional relational access for configuration, alarm, event, audit, job, user, and queue-snapshot data; a batch-oriented write path for Historian samples; a decoupled and prioritised Historian write path; versioned migrations that are never applied automatically at service start; and a measured escape hatch permitted only after evidence identifies a bottleneck. The ORM, mapper, micro-ORM, provider, and bulk-write mechanism remain `[OPEN]`. | `[PROPOSED]` — [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) |
| ARC-023 | The Local Application API is hosted by the runtime service, bound to the loopback interface only, authenticated with the application's own local user session, and contract-defined so the transport can change without changing the contract. | `[PROPOSED]` — [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) |
| ARC-024 | Stop and de-energize actions must never be gated on database availability. | `[PROPOSED]` — [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) |
| ARC-025 | Audit-required initiating actions must be refused when their required audit record cannot be persisted. The candidate set requires Owner ratification and never extends to a stop, de-energize, or release action. Historian backlog or degradation must not be used to decide this set. | `[PROPOSED]` — [`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) |
| ARC-026 | After any start or restart, the runtime must enter an explicit startup state and must issue no command until device state, position knowledge, and permissives have been re-established. Nothing resumes automatically. | `[PROPOSED]` — [`ADR-0007`](decisions/ADR-0007-runtime-process-model.md) |
| ARC-027 | Offline installation must detect prerequisites and must never download them; installation, upgrade, and rollback are performed from local media. | `[PROPOSED]` — [`ADR-0013`](decisions/ADR-0013-offline-deployment.md) |
| ARC-028 | The repository structure direction in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 20 is documented only. No directory in it is created before an implementation Stage Gate authorises it. | `[PROPOSED]` |

---

## 26. UI workload, presentation, and runtime performance boundaries (Stage 0.2 punchlist)

This group records the requirements established by the Owner-requested Stage 0.2 documentation
review punchlist. Requirements marked `[APPROVED]` are architecture boundaries already binding
from the approved Stage 0.2 Scope Gate or from an existing Owner-confirmed decision.
Requirements marked `[PROPOSED]` are selections or refinements awaiting Owner acceptance.
**Libraries, frameworks, providers, and transports named anywhere in this group remain
`[OPEN]`.**

### 26.1 UI framework candidates

| ID | Requirement | Status |
| --- | --- | --- |
| UIF-001 | **Candidate A — React + TypeScript + Vite — is a comparison candidate** for the UI framework, hosted as static local assets inside the application-owned kiosk shell. *Superseded by UIF-003 (React selected, 2026-10-07); retained as history.* It must not be described as accepted or rejected. | `[PROPOSED]` (historical) |
| UIF-002 | **Candidate B — Blazor Hybrid — is a comparison candidate** for the UI framework, hosted in the application-owned kiosk shell. It must not be described as accepted or rejected. | `[PROPOSED]` |
| UIF-003 | **React is selected as the Primary UI Framework** (Owner decision, 2026-10-07, after the Stage 0.2.1A Owner-local final Edge gate and manual review). *History:* the final UI framework was `[OPEN]` at Stage 0.2, and no UI code was to be created before the framework decision. Production UI code still requires the Main Development Scope Gate. | `[OWNER CONFIRMED]` |
| UIF-004 | Both candidates must be evaluated inside the same architecture: the same application-owned kiosk shell, the same embedded WebView, the same loopback ASP.NET Core Local Application API, and the same separate .NET Equipment Runtime Windows Service. Neither candidate may be compared against a general-purpose external browser. | `[APPROVED]` boundary |
| UIF-005 | **React can be built and deployed offline.** It must never be excluded on the basis that it requires Internet access. React introduces a second package and build ecosystem (Node.js/npm in addition to NuGet), which increases offline dependency-management and supply-chain effort without making offline development or deployment impossible. | `[APPROVED]` |
| UIF-006 | Blazor Hybrid must not be selected solely because it uses C# or reduces the number of build ecosystems. Those advantages do not by themselves prove UI performance, graph quality, camera integration, or long-running kiosk stability. | `[APPROVED]` |
| UIF-007 | The current evidence-based preference is React + TypeScript + Vite, based on the Owner's legacy operating experience with React/Vite and push-based updates, dense-visualisation fit, charting and camera ecosystem, and UI test tooling. **This preference is not final acceptance and not an approved selection.** | `[PROPOSED]` |
| UIF-008 | Final framework selection requires measured spike evidence. The proposed dual-candidate Stage 0.2.1 spike is **SUPERSEDED**: the Owner replaced the dual-candidate proposal with the React-first Stage 0.2.1A feasibility spike; React was subsequently selected as the Primary UI Framework (Owner decision, 2026-10-07; see UIF-003), and a Blazor counter-spike is not required unless a future material blocker is identified. | `[PROPOSED]` (historical) |

### 26.2 UI workload

| ID | Requirement | Status |
| --- | --- | --- |
| UIW-001 | The UI must support **106 live Sensor cells** and **212 Thermocouple channels** with a **one-second operational update target** (Owner domain correction; supersedes 104 / 208). | `[APPROVED]` |
| UIW-002 | The UI must support the Owner-confirmed wall layouts: Left 24 (4 × 6), Rear 29 (5 × 6 with the Rear Cannon slot), Right 24 (4 × 6), Front 29 (5 × 6 with the Front Cannon slot); Cannon slots are shown as neutral equipment, never as Sensors. | `[OWNER CONFIRMED]` |
| UIW-003 | The UI must present wall-level **Dirty and Cleaner counts**, each with an explicit label, and a distinct Bad or unavailable count where applicable. Displaying only unlabelled numbers is prohibited. | `[APPROVED]` |
| UIW-004 | The UI must support Sensor detail drill-down, Active Cleaning Job visibility, GlobalQueue preview, and Pump and pressure status. | `[APPROVED]` |
| UIW-005 | The UI must support a live operational trend, an alarm banner, and an alarm workspace. | `[APPROVED]` |
| UIW-006 | The UI must support camera panel integration. | `[APPROVED]` |
| UIW-007 | The UI must support permission-aware controls, full-screen kiosk operation, and responsive workstation layouts (IDN-006). | `[APPROVED]` |
| UIW-008 | The UI must operate stably over long periods, including memory stability. | `[APPROVED]` |
| UIW-009 | The UI must support **efficient changed-state updates**: only the parts of the page whose presentation state changed may be recomputed and repainted. | `[APPROVED]` |

### 26.3 Prohibited UI architecture behaviours

| ID | Prohibition | Status |
| --- | --- | --- |
| UIW-010 | One UI request per signal is prohibited. | `[APPROVED]` |
| UIW-011 | One UI-thread dispatch per signal is prohibited. | `[APPROVED]` |
| UIW-012 | Rebuilding all Sensor cell objects every second is prohibited. | `[APPROVED]` |
| UIW-013 | Recreating chart series on every update is prohibited. | `[APPROVED]` |
| UIW-014 | Unbounded chart data growth is prohibited. | `[APPROVED]` |
| UIW-015 | The UI must not query SQL for operational state. | `[APPROVED]` |
| UIW-016 | The UI must not read Modbus directly. | `[APPROVED]` |
| UIW-017 | The UI must not own Queue or Cleaning state. | `[APPROVED]` |
| UIW-018 | The UI must not infer device state independently from the Runtime. | `[APPROVED]` |

### 26.4 Sensor presentation model

| ID | Requirement | Status |
| --- | --- | --- |
| SPC-001 | A Sensor cell must represent, independently: process classification (`DIRTY`, `CLEANER`); Dirty Score intensity 0–100; data quality (`GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED`); queue state (TempQueue, TimeQueue, GlobalQueue position — ready-to-dispatch entries only; no Held / Blocked / Excluded entry states, see [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2); interaction state (Selected, Not selected); execution state (Active Cleaning Job target, Not active); and alarm state (Active alarm, Cleared acknowledgement required, Shelved, No alarm). | `[APPROVED]` |
| SPC-002 | **Process classification display rule:** Dirty Score > 50 is `DIRTY`; Dirty Score <= 50 is `CLEANER`. | `[OWNER CONFIRMED]` |
| SPC-003 | Where the classification threshold becomes configurable, the UI must read the **effective threshold from the Published configuration** and must not hard-code visual text around an assumed value. | `[APPROVED]` |
| SPC-004 | Visual semantics direction: cell background represents Dirty/Cleaner classification; shade intensity may represent Dirty Score strength; a corner marker or pattern represents data quality; a badge represents queue state; a cyan outline may represent selection; a high-contrast double outline may represent the Active Cleaning Job target; an alarm icon and bright border represent alarm state. | `[PROPOSED]` |
| SPC-005 | **Dirty red is a process-condition colour and must not be used as an alarm severity colour.** Alarm state must remain independently recognisable through icon, border, text, the alarm banner, and the dedicated alarm surfaces. | `[APPROVED]` |
| SPC-006 | Exact colour tokens, typography, dimensions, and animation must not be locked in Stage 0.2. They remain a future UX/UI decision. | `[APPROVED]` |

### 26.5 Quality-aware data pipeline

| ID | Requirement | Status |
| --- | --- | --- |
| DQS-001 | The pipeline order is: raw signal → communication and signal quality validation → engineering conversion → derived DiffTemp → Dirty Score → queue eligibility → UI presentation. | `[APPROVED]` |
| DQS-002 | Invalid, Bad, Stale, or unavailable data must not be presented as an ordinary Dirty or Cleaner state; it must have a distinct presentation. | `[APPROVED]` |
| DQS-003 | Raw values and their quality must be preserved where appropriate for diagnostics; presentation uses validated operational values. | `[APPROVED]` |
| DQS-004 | Queue logic must consume quality-aware validated state, never raw or substituted values. | `[APPROVED]` |
| DQS-005 | Raw sentinel or out-of-range values must not become normal process classifications without validation. | `[APPROVED]` |
| DQS-006 | The site-specific invalid-value and sentinel mapping remains `[OPEN]` until the Tag and data-quality contract is approved. Production sentinel values must never be invented or committed. | `[OPEN]` |

### 26.6 Live-state delivery

| ID | Requirement | Status |
| --- | --- | --- |
| LSD-001 | The Equipment Runtime must maintain the **authoritative in-memory operational state**. | `[APPROVED]` |
| LSD-002 | The runtime must expose an application-facing **snapshot or delta contract** delivered over a **loopback push channel**; the UI applies partial component updates. | `[PROPOSED]` |
| LSD-003 | The UI must not poll each signal individually and must not query SQL for live operational state. | `[APPROVED]` |
| LSD-004 | Published messages must carry, at minimum: a bootstrap snapshot on initial connection; a monotonic sequence or revision; a timestamp; changed Sensor presentation states; the Active Cleaning Job; Main Pump state; queue summary; alarm summary; communication health; and the published configuration revision. | `[PROPOSED]` |
| LSD-005 | The push transport remains `[OPEN]`. Candidates may include an ASP.NET Core push mechanism such as WebSocket-based delivery; no final push library is selected in this Stage, and the contract must not be coupled to the transport. | `[OPEN]` |
| LSD-006 | Reconnect rules: the UI requests or receives a new authoritative snapshot; it does not instruct devices to recover; it does not replay commands; it does not infer Job continuation from stale local state; the runtime remains authoritative. | `[APPROVED]` |

### 26.7 Modbus acquisition

| ID | Requirement | Status |
| --- | --- | --- |
| MDA-001 | One runtime-owned connection and one **serialized command queue per Modbus device**. | `[APPROVED]` |
| MDA-002 | Different device pollers may operate **concurrently under bounded scheduling**; requests to one device remain serialized unless future device evidence permits otherwise. | `[APPROVED]` |
| MDA-003 | One slow or timed-out device must not block polling of unrelated devices. | `[APPROVED]` |
| MDA-004 | Contiguous coil or register addresses must be compiled into **bounded batch reads** where supported. | `[APPROVED]` |
| MDA-005 | Published Tag configuration must compile into a **Poll Plan** at publication or runtime startup — never from scratch every poll cycle. | `[APPROVED]` |
| MDA-006 | Poll groups may use separate intervals (**Fast, Medium, Slow**); critical one-second data belongs to the Fast group. Exact grouping and register spans remain Production configuration. | `[PROPOSED]` |
| MDA-007 | Device limits, maximum quantities, address gaps, function codes, and byte/word order must be respected. | `[APPROVED]` |
| MDA-008 | The UI must never perform Modbus polling. | `[APPROVED]` |
| MDA-009 | No Modbus library is selected or installed in Stage 0.2. Prior slow behaviour of a legacy library must not be treated as proof that every architecture built on that library is slow. | `[APPROVED]` |

### 26.8 Configuration hot path

| ID | Requirement | Status |
| --- | --- | --- |
| CPS-001 | **SQL Server is not the per-cycle operational parameter source.** | `[APPROVED]` |
| CPS-002 | The runtime loads the Published configuration at startup, validates it, and holds an **immutable in-memory Published Configuration Snapshot**. | `[PROPOSED]` |
| CPS-003 | Queue, alarm, acquisition, motion, and Cleaning logic read the in-memory snapshot, not the database. | `[PROPOSED]` |
| CPS-004 | Configuration is reloaded only after an explicitly Published revision or an approved startup or recovery action, and a validated revision is swapped in atomically under the approved state gate — never while a Cleaning Job is active. | `[PROPOSED]` |
| CPS-005 | A Draft is never consumed by the runtime. | `[APPROVED]` |
| CPS-006 | The UI must not query SQL for operational parameters every second, and device polling must not query SQL for static parameters every cycle. | `[APPROVED]` |
| CPS-007 | SQL remains appropriate for versioned configuration persistence, alarm history, event history, audit history, Cleaning Job history, the Historian, reports, and on-demand historical queries. | `[APPROVED]` |

### 26.9 Historian decoupling

| ID | Requirement | Status |
| --- | --- | --- |
| HDC-001 | A slow database write must not directly block Modbus acquisition, Galil monitoring, queue evaluation, alarm evaluation, live UI updates, or a valid Main Pump stop request (PMP-007). | `[APPROVED]` |
| HDC-002 | The Historian path must be bounded, batched, and instrumented: bounded queue, batch writes, health metrics, data-gap reporting, no unbounded memory growth, and no silent data loss. | `[APPROVED]` |
| HDC-003 | An explicit backpressure or overflow policy is required before Historian implementation. | `[OPEN]` |
| HDC-004 | Historian policy must be separated from Audit-required action policy: a Historian backlog must not silently change which initiating actions are permitted. | `[APPROVED]` |
| HDC-005 | Overflow, spool, retry, priority, and database-outage policy remain `[OPEN]` and must be closed before Historian implementation. | `[OPEN]` |

### 26.10 Live trend

| ID | Requirement | Status |
| --- | --- | --- |
| TRD-001 | The live trend must use a **bounded in-memory window**; old points leave the live buffer. | `[APPROVED]` |
| TRD-002 | Chart series must not be recreated on every update. | `[APPROVED]` |
| TRD-003 | Point markers and animation must be optional and disabled where they harm performance. | `[APPROVED]` |
| TRD-004 | Historical range queries must use appropriate aggregation or downsampling; the UI must not plot an unbounded raw data set, and resolution should suit the visible time range and pixel width. | `[APPROVED]` |
| TRD-005 | Cleaning Job and Alarm overlays must be supported conceptually. | `[APPROVED]` |
| TRD-006 | Data gaps must remain visible and must not be drawn as a continuous line. | `[APPROVED]` |
| TRD-007 | The chart library remains `[OPEN]`. Simulation or UI rendering evidence is not hardware certification. | `[OPEN]` |

### 26.11 Proposed Stage 0.2.1 spike

| ID | Requirement | Status |
| --- | --- | --- |
| SPI-001 | Proposed Stage 0.2.1 — UI and Runtime Technology Spike — is **SUPERSEDED** and was not performed: the Owner replaced the dual-candidate proposal with the React-first Stage 0.2.1A feasibility spike; React was subsequently selected as the Primary UI Framework (Owner decision, 2026-10-07; see UIF-003), and a Blazor counter-spike is not required unless a future material blocker is identified. | `[PROPOSED]` (historical) |
| SPI-002 | The spike must compare Candidate A and Candidate B on the same synthetic data, the same runtime/API contract, the same Sensor presentation semantics, the same trend workload, the same camera placeholder or synthetic stream, the same reconnect scenario, and the same acceptance measures. | `[PROPOSED]` |
| SPI-003 | The spike must use **synthetic data only** and must not access WAGO, Galil, Production SQL Server, or Production configuration. | `[PROPOSED]` |
| SPI-004 | Spike results are application-level performance evidence only — never hardware evidence, never certification, and never a relaxation of the bench verification requirements. | `[PROPOSED]` |

---

## Related documents


- [`CURRENT_STATE.md`](CURRENT_STATE.md) — status legend, stage status, open items
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — subsystem responsibilities
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and terminology
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue specification
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — Cleaning Job specification
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — alarm model
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — safety boundary and bench verification
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — control authority matrix
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) — publication rules
