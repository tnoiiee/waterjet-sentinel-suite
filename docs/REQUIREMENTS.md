# Requirements — WaterJet Sentinel Suite (WJSS)

**Document status:** Requirements marked `[APPROVED]` were approved by the Owner in the
Stage 0.1 Scope Gate. Requirements marked otherwise are not approved behaviour.

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
| GOV-001 | The project must operate under an authority order: latest Owner instruction, then the approved Stage Gate, then [`AGENTS.md`](../AGENTS.md), then [`CURRENT_STATE.md`](CURRENT_STATE.md), then approved acceptance criteria, then [`ARCHITECTURE.md`](ARCHITECTURE.md), then [`ROADMAP.md`](ROADMAP.md), then older proposals. | `[APPROVED]` |
| GOV-002 | An inconsistency between governing sources must stop the affected part of the work, be documented, and must not be resolved by silently choosing a behaviour. | `[APPROVED]` |
| GOV-003 | Undocumented behaviour must not be treated as approved behaviour. | `[APPROVED]` |
| GOV-004 | Claims of success must be supported by observed tool output. | `[APPROVED]` |
| GOV-005 | Merge authority belongs exclusively to the Owner. Contributors must not merge. | `[APPROVED]` |
| GOV-006 | Force push, `reset --hard`, `clean -fd`, rebasing of approved checkpoints, and history rewriting are prohibited unless separately authorised. | `[APPROVED]` |
| GOV-007 | Every stage report must state CHANGED, UNCHANGED, and NOT VERIFIED items. | `[APPROVED]` |

## 2. Product identity and deployment

| ID | Requirement | Status |
| --- | --- | --- |
| IDN-001 | The product name is WaterJet Sentinel Suite, short name WJSS, repository name `waterjet-sentinel-suite`. | `[APPROVED]` |
| IDN-002 | The product is an automated boiler wall water-jet cleaning monitoring and supervisory control system. | `[APPROVED]` |
| IDN-003 | One Windows 11 Pro workstation per Boiler Unit. | `[APPROVED]` |
| IDN-004 | One application installation controls exactly one Boiler Unit. | `[APPROVED]` |
| IDN-005 | The main operating mode is a full-screen Control Room Kiosk. | `[APPROVED]` |
| IDN-006 | Applicable pages must support workstation-responsive layouts. | `[APPROVED]` |
| IDN-007 | The default application language is English. Thai may be used as supplementary contextual explanation where necessary. | `[APPROVED]` |
| IDN-008 | The application uses local application users. | `[APPROVED]` |
| IDN-009 | The initial system is standalone and must not depend on Internet access. | `[APPROVED]` |
| IDN-010 | The application must not be presented as a Safety Instrumented System, nor as a replacement for emergency stop circuits, hardwired protection, motor protection, mechanical limits, or controller-side safe-stop behaviour. | `[APPROVED]` |
| IDN-011 | No claim of certification or compliance with IEC, ISA, ISO, or any other standard may be made. Standards are guidance and design inspiration only. | `[APPROVED]` |

## 3. Priorities

| ID | Requirement | Status |
| --- | --- | --- |
| PRI-001 | Prioritise, in order: reliability, stability, deterministic behaviour, recoverability, operator usability, maintainability, appropriate baseline security. | `[APPROVED]` |

## 4. Physical system baseline

| ID | Requirement | Status |
| --- | --- | --- |
| PHY-001 | Boiler walls are Left, Rear, Right, and Front. Wall sensor counts are Left 24, Rear 28, Right 24, Front 28, giving 104 sensor locations. | `[APPROVED]` |
| PHY-002 | Each sensor has two thermocouple channels: a front channel (`TC_F`) and a rear channel (`TC_R`), giving 208 channels in total. | `[APPROVED]` |
| PHY-003 | Documentation and identifiers must distinguish the boiler wall name from the thermocouple side, because "Front" and "Rear" are both wall names and channel side names. | `[APPROVED]` |
| PHY-004 | There are eight Water Jet assemblies. Each Water Jet has a horizontal X axis and a vertical Y axis. | `[APPROVED]` |
| PHY-005 | One Galil controller controls two Water Jets; four controllers are expected in total, model DMC-B140-M, using axes A, B, C, and D. | `[APPROVED]` |
| PHY-006 | The axis mapping per controller follows the pattern: axis A is Water Jet 1 horizontal X, axis B is Water Jet 1 vertical Y, axis C is Water Jet 2 horizontal X, axis D is Water Jet 2 vertical Y. | `[APPROVED]` |
| PHY-007 | Production IP addresses, motion coordinates, travel limits, pulses per engineering unit, speeds, accelerations, and decelerations must not be invented. | `[APPROVED]` |

## 5. Modbus and WAGO

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

## 6. Temperature and Dirty Score

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

## 7. Cleaning timestamp baseline

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

## 8. Queue requirements

Full behaviour is specified in [`QUEUE_MODEL.md`](QUEUE_MODEL.md).

| ID | Requirement | Status |
| --- | --- | --- |
| QUE-001 | Each boiler wall has exactly one TempQueue and one TimeQueue. | `[APPROVED]` |
| QUE-002 | TempQueue eligibility requires: sensor enabled; sensor configuration valid; HardMinimumCleaningInterval satisfied; threshold condition satisfied when threshold is enabled; entry dwell satisfied. | `[APPROVED]` |
| QUE-003 | Global configuration includes TempQueueEntryDwellSeconds and TempQueueRemovalDwellSeconds, defaulting to 10 seconds each, independently configurable. | `[APPROVED]` |
| QUE-004 | Entry requires the sensor to meet TempQueue criteria continuously for the entry dwell; loss of the condition before dwell completion must reset the entry dwell. | `[APPROVED]` |
| QUE-005 | Removal requires the queued sensor to fail TempQueue criteria continuously for the removal dwell; becoming dirty again before the removal dwell completes must keep it queued. | `[APPROVED]` |
| QUE-006 | TempQueue sorting must be highest DirtyScore first, with stable scanOrder as the tie-break. | `[APPROVED]` |
| QUE-007 | TimeQueue eligibility requires `TimeSinceLastClean >= HardMinimumCleaningInterval`, where `TimeSinceLastClean = CurrentTime - LastSuccessfulCleaningCompletedAt`. | `[APPROVED]` |
| QUE-008 | TimeQueue sorting must be longest TimeSinceLastClean first, with stable scanOrder as the tie-break. | `[APPROVED]` |
| QUE-009 | GlobalQueue target capacity is eight unique sensor entries. | `[APPROVED]` |
| QUE-010 | Initial seeding order is Temp Left, Temp Rear, Temp Right, Temp Front, Time Left, Time Rear, Time Right, Time Front. | `[APPROVED]` |
| QUE-011 | After seeding and deduplication, GlobalQueue is FIFO. | `[APPROVED]` |
| QUE-012 | Changes to DirtyScore or TimeSinceLastClean must not reorder existing GlobalQueue entries automatically. | `[APPROVED]` |
| QUE-013 | GlobalQueue is rebuilt from current data when a new Auto Sequence starts, and the previous Queue snapshot must be preserved in Event history. | `[APPROVED]` |
| QUE-014 | A sensor must not appear more than once in GlobalQueue. | `[APPROVED]` |
| QUE-015 | On duplicate proposal, the earliest current GlobalQueue position is preserved, all source reasons are added to that entry, and the later duplicate source proposes its next eligible candidate. This continues until eight unique sensors are present or no eligible candidates remain. | `[APPROVED]` |
| QUE-016 | GlobalQueue may contain fewer than eight entries only when fewer than eight unique eligible sensors are available. | `[APPROVED]` |
| QUE-017 | The system must never duplicate a sensor, bypass HardMinimumCleaningInterval, use a disabled or inhibited sensor, or create a dummy job. | `[APPROVED]` |
| QUE-018 | When an entry leaves GlobalQueue, remaining entries shift forward preserving FIFO order, and the tail is refilled from the source owner of the entry that left. If no eligible candidate exists, the tail remains empty. | `[APPROVED]` |
| QUE-019 | Operator Reorder changes dispatch order but not source ownership; refill uses the entry's original source owner. | `[APPROVED]` |
| QUE-020 | Operator actions are Hold, Release Hold, Reject, Release Reject, and Reorder. Hold keeps the entry in the queue while the dispatcher skips it, without reordering others or blocking the queue. Reject removes the entry and suppresses the sensor from refill for the current Auto Sequence. | `[APPROVED]` |
| QUE-021 | Every operator queue action must produce an Event record containing timestamp, user, action, sensor, reason, original position, new position when applicable, Auto Sequence ID, and queue snapshot reference. | `[APPROVED]` |
| QUE-022 | Reorder must not move an active Cleaning Job, must not bypass eligibility revalidation, and must not alter source ownership. | `[APPROVED]` |
| QUE-023 | Suppression created by Reject ends when a new Auto Sequence is created unless released earlier. | `[APPROVED]` |

## 9. Deterministic scan order

| ID | Requirement | Status |
| --- | --- | --- |
| SCN-001 | The deterministic physical scanning sequence begins from the upper row and progresses downward. | `[APPROVED]` |
| SCN-002 | Within each row, the wall order is Left, then Rear, then Right, then Front. | `[APPROVED]` |
| SCN-003 | The system must account for different wall sensor counts: Left and Right have 24 each, Front and Rear have 28 each. | `[APPROVED]` |
| SCN-004 | scanOrder must be stable persisted configuration and must not depend on current UI sorting. | `[APPROVED]` |
| SCN-005 | The rule for aligning rows between walls of unequal height (24 versus 28), and whether scanOrder is authored explicitly or derived, is not specified. | `[OPEN]` |

## 10. Cleaning Job and sequence

Full behaviour is specified in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md).

| ID | Requirement | Status |
| --- | --- | --- |
| CLJ-001 | One Cleaning Job represents one sensor. | `[APPROVED]` |
| CLJ-002 | Each sensor is assigned one Water Jet, one Isolation Valve, six configurable path coordinates, one per-sensor HardMinimumCleaningInterval, and its temperature and queue parameters. | `[APPROVED]` |
| CLJ-003 | The normal sequence must follow the nineteen steps recorded in [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md), from selecting the GlobalQueue head to continuing the next-job countdown when no blocking condition exists. | `[APPROVED]` |
| CLJ-004 | Coordinates and timing values must not be invented. | `[APPROVED]` |
| CLJ-005 | A failed job must not update `LastSuccessfulCleaningCompletedAt`, must block the next job, and must wait for Operator action. | `[APPROVED]` |

## 11. Isolation Valve

| ID | Requirement | Status |
| --- | --- | --- |
| VLV-001 | The Isolation Valve is energize-to-open; DO OFF moves the valve toward the closed state. | `[APPROVED]` |
| VLV-002 | Derived feedback must be: upper 0 and lower 1 is CLOSED; upper 1 and lower 0 is OPEN; upper 0 and lower 0 is TRANSIT_OR_FAULT; upper 1 and lower 1 is INVALID_LIMIT_STATE. | `[APPROVED]` |
| VLV-003 | Opening and closing timeouts must be configurable. Their production values are `[NOT VERIFIED]`. | `[APPROVED]` / `[NOT VERIFIED]` |
| VLV-004 | During an active Cleaning Job, valve verification failure requires: stop or abort motion according to the approved fault class; command Valve OFF; set VFD AO to 0 Hz or stop the Main Pump; raise a blocking alarm; mark the Job FAILED or RECOVERY_REQUIRED; do not update the last successful cleaning timestamp; block the next job; wait for Operator action. | `[APPROVED]` |
| VLV-005 | While the Main Pump is running and no Cleaning Job is active, abnormal valve feedback must raise an alarm, pause the next-job countdown, and present a modal to the Operator identifying the affected valve, Water Jet, and sensors. | `[APPROVED]` |
| VLV-006 | The Operator must be offered: Stop All, which stops the Auto Sequence, cancels the countdown, stops the pump or commands VFD AO to 0 Hz, preserves the Queue snapshot, and records an Event and reason; or Continue With Valve Excluded, which marks the valve OUT_OF_SERVICE, excludes every associated sensor from TempQueue, TimeQueue, GlobalQueue, and candidate refill, allows unaffected Water Jets to continue, shows a persistent degraded-operation banner, and records the Event, user, valve, sensors, and Queue snapshot. | `[APPROVED]` |
| VLV-007 | Returning a valve to service requires valid closed feedback, a cleared alarm, acknowledgement of the cleared state, an explicit Operator return-to-service action, and runtime validation. | `[APPROVED]` |
| VLV-008 | Sensors re-entering normal source queue evaluation after a valve return to service must not be inserted into the middle of GlobalQueue. | `[APPROVED]` |

## 12. Main Pump

| ID | Requirement | Status |
| --- | --- | --- |
| PMP-001 | The Main Pump must support manual start and stop, and Auto Sequence operation. | `[APPROVED]` |
| PMP-002 | The system does not receive direct motor-running feedback; pump state must be derived from start/stop command state, pressure transmitter value, pressure quality, pressure-ready setpoint, and pressure rise timeout. | `[APPROVED]` |
| PMP-003 | Required conceptual states are STOPPED, START_REQUESTED, PRESSURIZING, PRESSURE_READY, RUNNING_CONFIRMED_BY_PRESSURE, STOP_REQUESTED, FAULT, and UNKNOWN. | `[APPROVED]` |
| PMP-004 | In an Auto Sequence the system must start the pump, wait for the pressure setpoint and a stable dwell, begin the countdown, keep the pump running between Cleaning Jobs, and stop only when the Operator stops the sequence or a blocking fault requires it. | `[APPROVED]` |
| PMP-005 | Pressure values, the pressure-ready setpoint, the stable dwell, and the pressure rise timeout must not be invented. Production values are `[NOT VERIFIED]`. | `[APPROVED]` / `[NOT VERIFIED]` |
| PMP-006 | The pump state model does not constitute motor protection and must not be presented as such. | `[APPROVED]` |

## 13. Galil motion

| ID | Requirement | Status |
| --- | --- | --- |
| GAL-001 | The Galil controller model is DMC-B140-M, driving stepper motors with encoder feedback. | `[APPROVED]` |
| GAL-002 | Required conceptual feedback modes are OPEN_LOOP and ENCODER_VERIFIED. | `[APPROVED]` |
| GAL-003 | ENCODER_VERIFIED must not be described as true closed-loop unless future engineering verification confirms that encoder feedback is used for active correction. | `[APPROVED]` |
| GAL-004 | Motion profiles are global by operation type: Manual Jog, Manual Command Move, and Cleaning Job. | `[APPROVED]` |
| GAL-005 | Motion mode or profile changes must not be allowed while the Main Pump is running, an Auto Sequence is active, a Cleaning Job is active, an axis is moving, the position is unknown, or a valve is open. | `[APPROVED]` |
| GAL-006 | Mechanical limits, soft limits, pulses per engineering unit, encoder behaviour, speed, acceleration, deceleration, homing, and operational envelope are commissioning values and remain `[NOT VERIFIED]`. | `[NOT VERIFIED]` |

## 14. Alarm model

| ID | Requirement | Status |
| --- | --- | --- |
| ALM-001 | Severities are Critical, High, Medium, Low, and Information. | `[APPROVED]` |
| ALM-002 | Condition, acknowledgement, and shelving are independent dimensions: ACTIVE or CLEARED; UNACKNOWLEDGED or ACKNOWLEDGED; UNSHELVED or SHELVED. | `[APPROVED]` |
| ALM-003 | A blocking condition is released only when the condition is CLEARED, the cleared state is ACKNOWLEDGED, and no other blocking alarm exists. | `[APPROVED]` |
| ALM-004 | Acknowledging an alarm while it is still active must not release the block. | `[APPROVED]` |
| ALM-005 | A cleared but unacknowledged alarm must display "RETURNED TO NORMAL - ACK REQUIRED". | `[APPROVED]` |
| ALM-006 | Shelving must support a configurable duration, a per-alarm maximum, allowed permissions, a required reason, auto-unshelve, a shelved alarm list, and audit and Event records. | `[APPROVED]` |
| ALM-007 | Critical hardware and safety-related alarms may be configured as non-shelvable. | `[APPROVED]` |

## 15. Historian and retention

Full details are in [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md).

| ID | Requirement | Status |
| --- | --- | --- |
| HIS-001 | The database target is SQL Server 2025 Standard. | `[APPROVED]` |
| HIS-002 | Storage baseline is three 2 TB HDDs in RAID 5, approximately 4 TB theoretical usable capacity before formatting and overhead. | `[APPROVED]` |
| HIS-003 | Baseline intervals: Modbus acquisition 1 second; queue evaluation 1 second; normal thermocouple historian 5 seconds; active Cleaning Job detailed historian 1 second; alarm-related detailed storage 1 second; long-term aggregate 1 minute. | `[APPROVED]` |
| HIS-004 | Default retention is proposed as: raw temperature 300 days; one-minute aggregate 1,095 days; cleaning jobs 5 years; alarm history 3 years; event history 2 years; audit history 5 years; diagnostics 180 days; temporary exports 30 days. | `[PROPOSED]` |
| HIS-005 | Retention values must be configurable separately by data category. | `[APPROVED]` |
| HIS-006 | Cleanup must use scheduled cutoff-based deletion or partition maintenance. Retention must not be described as directly overwriting Day 1 with Day 301. | `[APPROVED]` |
| HIS-007 | Audit history must be retained at least as long as event history. | `[PROPOSED]` |
| HIS-008 | Data volume, partition scheme, index strategy, backup method, and integrity scheme are not defined. | `[OPEN]` |

## 16. Users and permissions

Full details are in [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md).

| ID | Requirement | Status |
| --- | --- | --- |
| USR-001 | Default role templates are Operator, Technician, Engineer, Supervisor, Administrator, and Break-glass Recovery Account. | `[APPROVED]` |
| USR-002 | Roles are configurable permission collections. | `[APPROVED]` |
| USR-003 | A shared Operator account is allowed, and documentation must state that this prevents person-level Operator attribution. | `[APPROVED]` |
| USR-004 | Privileged sessions must support a configurable inactivity timeout. | `[APPROVED]` |
| USR-005 | On privileged-session timeout the system must log out the privileged user and return to the Operator session, must not abort an active Auto Sequence, must stop any manual hold-to-run operation, and must preserve or safely handle configuration drafts. | `[APPROVED]` |
| USR-006 | The break-glass account is for recovery only, must not be used for normal operation, must create a high-severity audit event on every login, must not bypass hardware safety, and its credentials must never be committed. | `[APPROVED]` |

## 17. Public repository boundary

| ID | Requirement | Status |
| --- | --- | --- |
| PUB-001 | The public repository may contain source documentation, future source code, simulator definitions, dummy configuration, example tags, generic example motion profiles, and automated tests. | `[APPROVED]` |
| PUB-002 | The public repository must not contain production IP addresses, production Tag Lists, production register maps, actual motion coordinates, actual travel limits, production speed profiles, pressure setpoints, production alarm thresholds, user databases, password hashes, credentials, production connection strings, production logs, or plant-sensitive information. | `[APPROVED]` |
| PUB-003 | Production configuration remains local and should be stored outside the Git working tree where practical. | `[APPROVED]` |
| PUB-004 | Stage 0.1 may create only example filenames or documentation references for configuration. Production configuration content must not be created. | `[APPROVED]` |

## 18. Hardware safety boundary

| ID | Requirement | Status |
| --- | --- | --- |
| HSB-001 | WAGO watchdog capability has been identified, but exact fail-safe behaviour is `[NOT VERIFIED]`. | `[NOT VERIFIED]` |
| HSB-002 | Production valve and pump write control is prohibited until bench tests verify: Ethernet disconnection moves outputs to a safe state; Equipment Runtime termination moves outputs to a safe state; workstation reboot does not leave outputs energized; WAGO reboot defaults outputs safely; watchdog timeout matches configuration; VFD AO reaches the 0 Hz safe command; stale commands do not execute after reconnection; outputs do not automatically re-energize after communication recovery. | `[NOT AUTHORIZED]` |
| HSB-003 | Target safe states are Isolation Valve DO OFF, Main Pump command OFF, and VFD AO 0 Hz. | `[APPROVED]` |
| HSB-004 | The UI close guard is not an acceptable sole protection against an energized output remaining active. | `[APPROVED]` |
| HSB-005 | Hardware emergency stop, limit switches, motor protection, the Local/Remote selector, and independent protection remain outside the application's authority. | `[APPROVED]` |

## 19. Test and verification

| ID | Requirement | Status |
| --- | --- | --- |
| TST-001 | Documentation stages perform documentation-only validation; runtime, build, database, hardware, and device tests are not part of a documentation stage. | `[APPROVED]` |
| TST-002 | Queue arbitration, dirty score computation, deduplication, refill, and alarm blocking-release behaviour are to be covered by deterministic automated tests once code exists. | `[PROPOSED]` |
| TST-003 | Hardware fail-safe behaviour must be verified on the bench before any production write control is enabled. | `[NOT AUTHORIZED]` |

---

## Related documents

- [`CURRENT_STATE.md`](CURRENT_STATE.md) — status legend and open items
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — subsystem responsibilities
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and terminology
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue specification
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — Cleaning Job specification
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — safety boundary and bench verification
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) — publication rules
