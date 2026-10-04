# Control Authority — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the authority model. Every control action that writes
to an output is `[NOT AUTHORIZED]` until the bench verification in
[`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) is complete and recorded.

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

Rule: **External authority always wins.** The application must never assume it retains
control, and must never re-assert a command without re-validating state.

## 2. Authority matrix

| Output | Commanded by | Conditions before commanding | Status |
| --- | --- | --- | --- |
| Isolation Valve DO | Application (manual or supervisory) | Valve not `OUT_OF_SERVICE`; permission held; permissives satisfied; feedback state consistent | `[NOT AUTHORIZED]` |
| Main Pump start | Application (manual or supervisory) | Permission held; no blocking alarm; pressure transmitter quality valid | `[NOT AUTHORIZED]` |
| Main Pump stop | Application (manual or supervisory) | None — stop is always permitted | `[NOT AUTHORIZED]` |
| VFD AO (speed reference) | Application | Permission held; value within configured limits; source value quality valid | `[NOT AUTHORIZED]` |
| Galil motion (jog, move, job path) | Application | Axis position known; no motion-profile change while prohibited conditions apply; permissives satisfied | `[NOT AUTHORIZED]` |
| Motion profile or mode change | Application (engineering permission) | Not while the pump runs, an Auto Sequence is active, a Cleaning Job is active, an axis is moving, position is unknown, or a valve is open | `[NOT AUTHORIZED]` |
| Hardware emergency stop | **External only** | Outside application authority — never commanded, never overridden, never modelled as an application output | `[APPROVED]` restriction |
| Motor protection | **External only** | Outside application authority | `[APPROVED]` restriction |
| Limit switches, mechanical limits | **External only** | Outside application authority | `[APPROVED]` restriction |

## 3. Preconditions common to every application command

1. The acting user holds the required permission for that action.
2. No blocking alarm prevents the action.
3. The sensors and equipment involved are enabled, not inhibited, and not
   `OUT_OF_SERVICE`.
4. Signal quality for every value the decision depends on is valid. Bad quality must block
   the action, not be interpreted as a benign value.
5. The equipment's derived state is known. Unknown state blocks the action.
6. The action is recorded as an Event, with user, time, action, subject, and reason where
   a reason is required.

## 4. Command conflict resolution

| Situation | Required behaviour |
| --- | --- |
| Operator holds an entry, then a job would dispatch it | Dispatcher skips held entries; other entries preserve relative FIFO order; the queue is not blocked. `[APPROVED]` |
| Operator tries to reorder an active Cleaning Job | Not permitted — Reorder cannot move an active Cleaning Job. `[APPROVED]` |
| Operator rejects an entry that is about to dispatch | Reject removes the entry and suppresses the sensor from refill during the current Auto Sequence. `[APPROVED]` |
| Valve becomes `OUT_OF_SERVICE` while its sensor sits in GlobalQueue | Associated sensors are excluded from TempQueue, TimeQueue, GlobalQueue, and refill. `[APPROVED]` |
| Valve returns to service | Sensors re-enter normal source queue evaluation and must not be inserted into the middle of GlobalQueue. `[APPROVED]` |
| External stop removes the plant's ability to continue | Application must not re-assert commands; re-validation is required before any new command. `[PROPOSED]` |
| Privileged session times out during manual hold-to-run | The manual hold-to-run operation must stop. An active Auto Sequence must not be aborted. `[APPROVED]` |

## 5. Priority of command sources

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

## 6. Auditing of authority

Every change of authority state must be recorded:

- Auto Sequence start, stop, and reason for stopping.
- Operator queue actions, with all fields required by
  [`QUEUE_MODEL.md`](QUEUE_MODEL.md) section 7.
- Manual equipment commands, with user, time, target, and value.
- Valve `OUT_OF_SERVICE` marking and return to service, with user, reason, affected valve,
  affected sensors, and the Queue snapshot.
- Manual correction of `LastSuccessfulCleaningCompletedAt`, with sensor, previous
  timestamp, new timestamp, user, time, and reason.
- Break-glass login, always as a high-severity audit event.

## 7. Open authority items

| Item | Why it matters | Status |
| --- | --- | --- |
| Who may command manual valve and pump operation | Determines permission model detail | `[OPEN]` |
| Whether parallel Cleaning Jobs on independent Water Jets are permitted | Determines single-job assumption in [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 3 | `[OPEN]` |
| Authority when the Local/Remote selector is in Local | Application visibility and interlock behaviour | `[OPEN]` |
| Behaviour when the Historian is unavailable | Whether control continues without recording | `[OPEN]` |
| Required reason text for each operator action | Audit completeness | `[OPEN]` |

---

## Related documents

- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — what the application may never do
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — the supervised command sequence
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — operator queue actions
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking release
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — permissions and sessions
