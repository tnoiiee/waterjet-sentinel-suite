# Control Authority — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the authority model, including the Owner-confirmed
DCS Permissive Override. Every control action that writes to an output is
`[NOT AUTHORIZED]` until the bench verification in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md)
is complete and recorded.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**IN PROGRESS**, PR #3 **OPEN**, **NOT MERGED**. React final selection **NOT YET APPROVED** (UI
framework `[OPEN]`). Blazor counter-spike **DEFERRED / `[NOT AUTHORIZED]`**. Stage 0.3
`[NOT AUTHORIZED]`. Production device access `[NOT AUTHORIZED]`.

This document answers one question for every output: **who or what may command it, and
under what conditions.** Where an answer is not yet determined, it is marked `[OPEN]` — it
is never assumed.

---

## 1. Authority concepts

| Concept | Meaning |
| --- | --- |
| Manual authority | An operator acts directly on an equipment item from the interface |
| Supervisory authority | The Auto Sequence issues commands as part of a Cleaning Job |
| Application authority | Any command written by the application, whether manual or supervisory |
| External authority | Hardwired control, the Local/Remote selector, controller-side logic, or a protective device operating outside the application |

Rule: **External authority always wins.** The application must never command, override,
bypass, suppress, or replace an external protection function, and must never assume it
retains control. It must never re-assert a command without re-validating state.

Where a read-only indication of an external protection state is available, the application
may monitor and display it and use it as a supervisory command gate. That indication does
not transfer ownership of the protection function, is not a substitute for it, and unknown,
unavailable, stale, or bad-quality indication must never be inferred as safe.

## 2. Authority matrix

| Output | Commanded by | Conditions before commanding | Status |
| --- | --- | --- | --- |
| Isolation Valve DO | Application (manual or supervisory) | Valve not `OUT_OF_SERVICE`; permission held; permissives satisfied; feedback state consistent | `[NOT AUTHORIZED]` |
| Main Pump start | Application (manual or supervisory) | Permission held; no blocking alarm; pressure transmitter quality valid | `[NOT AUTHORIZED]` |
| Main Pump stop | Application (manual or supervisory) | No application-level operational permissive may block a valid stop request. Actual execution remains subject to command-path availability, communication availability, external authority, Local/Remote state, hardware state, current Production Write authorization, and independent protection behaviour | `[NOT AUTHORIZED]` |
| VFD AO (speed reference) | Application | Permission held; value within configured limits; source value quality valid | `[NOT AUTHORIZED]` |
| Galil motion (jog, move, job path) | Application | Axis position known; no motion-profile change while prohibited conditions apply; permissives satisfied | `[NOT AUTHORIZED]` |
| Motion profile or mode change | Application (engineering permission) | Not while the pump runs, an Auto Sequence is active, a Cleaning Job is active, an axis is moving, position is unknown, or a valve is open | `[NOT AUTHORIZED]` |
| Hardware emergency stop | **External only** | Outside application authority — never commanded, never overridden, never suppressed, never modelled as an application output | `[APPROVED]` restriction |
| Motor protection | **External only** | Outside application authority | `[APPROVED]` restriction |
| Limit switches, mechanical limits | **External only** | Outside application authority | `[APPROVED]` restriction |

## 3. Preconditions common to every application command

1. The acting user holds the required permission for that action.
2. No blocking alarm prevents the action. A block is released only when the condition is
   CLEARED, the **cleared-state** acknowledgement is ACKNOWLEDGED, and no other blocking
   condition remains. See [`ALARM_MODEL.md`](ALARM_MODEL.md) section 3.
3. The sensors and equipment involved are enabled, not inhibited, and not
   `OUT_OF_SERVICE`.
4. Signal quality for every value the decision depends on is valid. Bad quality must block
   the action, not be interpreted as a benign value.
5. The equipment's derived state is known. Unknown state blocks the action.
6. The action is recorded as an Event, with user, time, action, subject, and reason where
   a reason is required.

## 4. Sequential execution authority

**At most one Cleaning Job may be ACTIVE within one installation at any time.**
`[OWNER CONFIRMED]`

- Different Water Jets, different Isolation Valves, different boiler walls, and different
  Galil controllers do not grant authority for concurrent Cleaning Jobs.
- Parallel Water Jet cleaning is prohibited.
- A second Cleaning Job must not enter an executing state until the current Cleaning Job
  has reached an approved safe and released terminal condition.
- The selected eligible head of GlobalQueue is the only normal source for the next
  Cleaning Job.
- Queue refill, score changes, Operator Reorder, Hold, Reject, valve exclusion, and
  equipment availability must never produce concurrent Cleaning Jobs.

"Allow unaffected Water Jets to continue" means **sequential** continuation: after the valve
fault workflow is resolved and continuation is authorized, the AutoSequence may later select
a sequential Cleaning Job assigned to another available Water Jet, still executing only one
Cleaning Job at a time.

## 5. DCS Permissive Override

The Operator may activate the **DCS Permissive Override** `[OWNER CONFIRMED]`. It must
always be named exactly that, and never described as a general "Ignore DCS" function.

| Property | Rule |
| --- | --- |
| Activation | Explicit Operator action with a confirmation step |
| Expiry | None. Remains active until manually released |
| Reason | Recorded on activation and on release |
| Visibility | Persistent visible banner while active |
| Recording | Activation, release, user, timestamp, and reason in Event and Audit history |
| Attribution | Subject to shared Operator-account limitations |
| Permission | Requires an explicit permission |

**The override may bypass only the approved DCS permissive evaluation.** It must not bypass
any of the following, and must not be configurable to do so:

- WAGO communication health
- Modbus transport health
- Isolation Valve feedback
- Valve open or close verification
- Main Pump pressure validation
- Galil limits
- Motion faults
- Encoder or position validation
- Emergency stop
- Local/Remote selector
- Motor or drive protection
- WAGO output watchdog
- External hardware protection
- Critical application lifecycle gates
- The one-active-Cleaning-Job invariant

## 6. Command conflict resolution

| Situation | Required behaviour |
| --- | --- |
| ~~Operator holds an entry, then a job would dispatch it~~ | **SUPERSEDED (Owner decision, 2026-10-06)** — queue entries are never held or skipped; the GlobalQueue is ready-only and dispatched head-only. Pause belongs to the AutoSequence or the Cleaning Job lifecycle; Production Pause / Resume is **OWNER DECISION REQUIRED** ([`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2). |
| Operator tries to reorder an active Cleaning Job | Not permitted — Reorder cannot move an active Cleaning Job. `[APPROVED]` |
| Operator rejects an entry that is about to dispatch | Reject removes the entry and suppresses the sensor from refill during the current Auto Sequence. `[APPROVED]` |
| Operator reorders entries | Changes dispatch position only; never changes source ownership and never creates a second active Cleaning Job. `[OWNER CONFIRMED]` |
| Valve becomes `OUT_OF_SERVICE` while its sensor sits in GlobalQueue | Associated sensors are excluded from TempQueue, TimeQueue, GlobalQueue, and refill. `[APPROVED]` |
| Valve returns to service | Sensors re-enter normal source queue evaluation and must not be inserted into the middle of GlobalQueue. Requires cleared-state acknowledgement. `[OWNER CONFIRMED]` |
| Operator stops the AutoSequence | Dispatch stops; the active job is handled per the approved stop or recovery policy; the Queue snapshot and Rejected/Reordered state are recorded in Event history (queue-level Held is superseded); the instance closes; the old GlobalQueue is not kept as the executable queue. `[OWNER CONFIRMED]` |
| External stop removes the plant's ability to continue | Application must not re-assert commands; re-validation is required before any new command. `[PROPOSED]` |
| Privileged session times out during manual hold-to-run | The manual hold-to-run operation must stop. An active Auto Sequence must not be aborted. `[APPROVED]` |
| Operator attempts to close the Operations UI while a Cleaning Job is active or the Main Pump is running | The normal close request is rejected with a clear explanation and the Operator is directed back to the active operation or Pump/Sequence state. This is an **operational usability control**, not a safety protection, and it does not change equipment authority: the Equipment Runtime lifecycle, the WAGO watchdog, safe output states, and external hardware protection remain independent of it. `[OWNER CONFIRMED]` |

## 6.1 Operations UI authority boundary

The Operations UI is an operator interface, not a control authority. Blocking the normal
close action while a Cleaning Job is active or the Main Pump is running restricts the
interface, not the equipment:

- It does not create, transfer, or remove any authority over any output.
- It does not protect against process termination, Windows shutdown, workstation restart,
  power loss, or hardware failure.
- It must never be recorded, designed, or reasoned about as a protective function, and it
  never satisfies a bench verification item in
  [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) section 4.

The Operations UI holds no device session, cannot write to hardware directly, and cannot
connect to the database directly. It submits **control requests** only; the runtime service
performs the permission, lifecycle, interlock, ownership, and command-state validation that
turns a request into a dispatched command. This boundary is approved by the Stage 0.2 Scope
Gate and is carried by the process model in
[`decisions/ADR-0007`](decisions/ADR-0007-runtime-process-model.md); the UI delivery selection
in [`decisions/ADR-0006`](decisions/ADR-0006-ui-delivery-model.md) does not change it.

### 6.2 Critical suspension and Resume authority (Owner critical Pump decision, 2026-10-06)

After a Main Pump unexpected stop or trip, the AutoSequence stays suspended until a future,
Owner-approved Resume. No automatic Resume and no automatic next Job exist. Acknowledging the
critical alarm creates no authority and does not resume.

Who may resume, under which preconditions and with which confirmation is **OWNER DECISION
REQUIRED** ([`spikes/critical-pump-safe-return-decision-matrix.md`](spikes/critical-pump-safe-return-decision-matrix.md) matrix F). The same applies to who may acknowledge.

The Stage 0.2.1A spike's acknowledge endpoint is a synthetic, same-origin loopback request and
the spike's synthetic test reset is review tooling. Neither is a Resume or an authority model.

## 7. Priority of command sources

When more than one source could command the same output, the highest applicable priority
applies, and lower-priority sources must not fight it:

1. External hardwired or protective action (outside the application).
2. Application blocking response to a fault (for example, commanded Valve OFF and VFD AO
   0 Hz on valve fault).
3. Operator stop of the Auto Sequence.
4. Operator manual action within an active sequence.
5. Supervisory Cleaning Job command.

Sources 2 to 5 are `[NOT AUTHORIZED]` for production writes until bench verification
completes.

## 8. Auditing of authority

Every change of authority state must be recorded:

- Auto Sequence start, stop, and reason for stopping.
- Operator queue actions, with all fields required by
  [`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 7.
- Manual equipment commands, with user, time, target, and value.
- Valve `OUT_OF_SERVICE` marking and return to service, with user, reason, affected valve,
  affected sensors, and the Queue snapshot.
- Manual correction of `LastSuccessfulCleaningCompletedAt`, with sensor, previous
  timestamp, new timestamp, user, time, and reason.
- DCS Permissive Override activation and release, with user, timestamp, and reason.
- Break-glass login, always as a high-severity audit event.

## 8.1 Authority enforcement point (Stage 0.2)

1. **Every command is validated once, in the Runtime, before any actuation.** The approved
   order is: authorization, lifecycle, interlock, ownership, then command-state validation. A
   command that fails any check is refused and recorded; it is never partially applied.
2. **The Operations UI is a command requester, not an actuator.** The UI cannot write to
   hardware and cannot open a device session. Its controls issue requests that the Equipment
   Runtime-mediated command path accepts or refuses.
3. **The Equipment Runtime service is the sole owner of physical device sessions.** No other
   process — UI, Local Application API host, reporting, or tooling — may own, open, or share a
   device session for WAGO, Galil, or any future device.
4. **Adapters contain no UI logic and no authority logic.** They translate between the
   application-facing contract and a vendor protocol. Authority, sequencing, and interlock
   decisions are made above the adapter and are not delegated to it.
5. **A stale command is refused.** A command whose validity window has expired, or whose
   command state is unknown, or whose target position is unknown, is not executed and is not
   inferred as successful.
6. **UI close, crash, restart, or relaunch does not create authority.** It neither grants nor
   revokes a permission, and it does not interrupt or complete an active Cleaning Job.
7. **Nothing added here permits concurrent Cleaning Jobs**, a second active Cleaning Job, a
   parallel Water Jet Cleaning, or a shared Isolation Valve. Sections 4, 5, and 6 continue to
   govern, including the DCS Permissive Override exclusions, which cannot bypass safety or
   equipment gates.

## 9. Open authority items

| Item | Why it matters | Status |
| --- | --- | --- |
| Who may command manual valve and pump operation | Determines permission model detail | `[OPEN]` |
| Authority when the Local/Remote selector is in Local | Application visibility and interlock behaviour | `[OPEN]` |
| Behaviour when the Historian is unavailable | Whether control continues without recording | `[OPEN]` |
| Required reason text for each operator action | Audit completeness | `[OPEN]` |
| Exact definition of the DCS permissive set that the override bypasses | Deployment data; must be captured, not invented | `[OPEN]` |

Resolved by Owner confirmation and therefore **not** open: the Water Jet to Isolation Valve
cardinality, and whether parallel Cleaning Jobs are permitted (they are not).

---

## Related documents

- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — what the application may never do
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — the supervised command sequence and sequencing
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — operator queue actions and stop/rebuild
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking release and cleared-state acknowledgement
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — permissions and sessions
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — SEQ, OVR, PMP, and UIG requirements
