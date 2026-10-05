# Alarm Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for severities, the three alarm dimensions, blocking
release, shelving, and display wording. The acknowledgement model in section 3 is
`[OWNER CONFIRMED]`. Alarm definitions and threshold values are commissioning values and
are `[NOT VERIFIED]`.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**IN PROGRESS**, PR #3 **OPEN**, **NOT MERGED**. React final selection **NOT YET APPROVED** (UI
framework `[OPEN]`). Blazor counter-spike **DEFERRED / `[NOT AUTHORIZED]`**. Stage 0.3
`[NOT AUTHORIZED]`. Production device access `[NOT AUTHORIZED]`.

This document defines how the system represents, displays, and releases alarms. It does
not define production alarm thresholds, which are confidential deployment values.

---

## 1. Severities

| Severity | Intended use |
| --- | --- |
| `CRITICAL` | Equipment or process condition requiring immediate Operator action; typically blocking |
| `HIGH` | Significant condition requiring prompt Operator attention; may block |
| `MEDIUM` | Condition needing attention before the next cleaning cycle |
| `LOW` | Advisory condition |
| `INFORMATION` | Informational; no action required |

Severity assignment per alarm definition, and which severities block, are `[PROPOSED]`
until ratified. Severity does not by itself determine blocking; the alarm definition does.

## 2. Three independent dimensions

Every alarm instance carries three orthogonal dimensions `[APPROVED]`:

| Dimension | Values |
| --- | --- |
| Condition | `ACTIVE`, `CLEARED` |
| Acknowledgement | `UNACKNOWLEDGED`, `ACKNOWLEDGED` |
| Shelving | `UNSHELVED`, `SHELVED` |

The dimensions are independent:

- Acknowledging an `ACTIVE` alarm does not clear it and does not release its block.
- Clearing a condition does not acknowledge it.
- Shelving does not clear, acknowledge, or delete an alarm.

## 3. Acknowledgement model

This model is `[OWNER CONFIRMED]` and must be applied consistently wherever alarm
acknowledgement is described.

### 3.1 Active acknowledgement is awareness only

- An `ACTIVE` alarm may record Operator awareness or acknowledgement if the model supports
  it.
- Acknowledging an `ACTIVE` alarm **does not clear the alarm**.
- Acknowledging an `ACTIVE` alarm **does not release any block**.

### 3.2 Cleared-state acknowledgement is required

- When an `ACTIVE` condition transitions to `CLEARED`, **final-clearance acknowledgement
  becomes pending**.
- On that transition, the final-clearance acknowledgement becomes `UNACKNOWLEDGED`.
- The Operator must acknowledge the `CLEARED` state.
- An acknowledgement recorded earlier, while the alarm was `ACTIVE`, does **not** satisfy
  this requirement.

### 3.3 The prohibited sequence

The following sequence must **not** be possible, and must not be implementable by
configuration:

```text
ACTIVE + ACKNOWLEDGED
  -> condition clears
  -> CLEARED + ACKNOWLEDGED automatically
  -> block releases without a post-clear acknowledgement     <-- PROHIBITED
```

### 3.4 Blocking release rule

A blocking alarm releases its block only when **all three** hold `[OWNER CONFIRMED]`:

1. Condition = `CLEARED`.
2. **Cleared-state** acknowledgement = `ACKNOWLEDGED`.
3. No other blocking condition remains.

Consequences that must be implemented exactly:

- An `ACTIVE` alarm that has been acknowledged still blocks.
- A `CLEARED` alarm whose cleared-state acknowledgement is pending still blocks and
  displays "RETURNED TO NORMAL - ACK REQUIRED".
- A `SHELVED` alarm that is still `ACTIVE` still blocks, because shelving suppresses
  presentation, not the condition.
- Releasing a block is never automatic on a timer and never silent.

## 4. Display and state combinations

| Condition | Acknowledgement | Shelving | Presentation |
| --- | --- | --- | --- |
| `ACTIVE` | `UNACKNOWLEDGED` | `UNSHELVED` | Standard active alarm, most prominent |
| `ACTIVE` | `ACKNOWLEDGED` | `UNSHELVED` | Active, acknowledged (**awareness only**) |
| `ACTIVE` | `UNACKNOWLEDGED` | `SHELVED` | Shelved but active; still visible in the shelved list |
| `ACTIVE` | `ACKNOWLEDGED` | `SHELVED` | Shelved, active, acknowledged (**awareness only**) |
| `CLEARED` | cleared-ack pending | any | **"RETURNED TO NORMAL - ACK REQUIRED"** |
| `CLEARED` | cleared-ack `ACKNOWLEDGED` | `UNSHELVED` | Cleared and acknowledged; leaves the active list |
| `CLEARED` | cleared-ack `ACKNOWLEDGED` | `SHELVED` | Cleared while shelved; auto-unshelve applies |

Acknowledging an alarm while it is still active does not release the block. "RETURNED TO
NORMAL - ACK REQUIRED" is the required wording for a cleared but unacknowledged alarm.

## 5. Blocking scope

Blocking is scoped, not global by default. The intended scopes are `[PROPOSED]`:

| Scope | Effect |
| --- | --- |
| `JOB` | Prevents starting or continuing a Cleaning Job |
| `SEQUENCE` | Prevents the Auto Sequence from dispatching the next job |
| `EQUIPMENT` | Prevents commanding the affected equipment item |
| `SENSOR` | Removes the sensor from queue eligibility and refill |

Which scope each alarm definition carries is `[OPEN]` and must be ratified before
implementation. A valve fault during a job is blocking for the job; an idle valve fault
pauses the sequence countdown; a blocking DCS communication condition stops the next-job
countdown; all are `[APPROVED]` behaviours that map onto these scopes.

## 6. Shelving

Shelving must support `[APPROVED]`:

- A configurable duration.
- A per-alarm maximum.
- Allowed permissions.
- A required reason.
- Auto-unshelve at expiry.
- A shelved alarm list that remains visible and searchable.
- Audit and Event records for every shelve and unshelve action.

Critical hardware and safety-related alarms **may be configured as non-shelvable**
`[APPROVED]`. The specific list of non-shelvable alarms is `[OPEN]` and must be ratified
before deployment.

A shelved alarm that reaches auto-unshelve while still `ACTIVE` must return to the
unshelved list in its current condition and acknowledgement state. It must not be
acknowledged by the unshelve, and its cleared-state acknowledgement requirement is
unaffected.

## 7. Alarm instances and lifecycle

| Element | Description |
| --- | --- |
| Alarm definition | The configured rule: condition source, severity, thresholds, blocking scope, shelvability |
| Alarm instance | One occurrence: raise time, condition, active acknowledgement, cleared-state acknowledgement, shelving, and history |

Lifecycle `[OWNER CONFIRMED]`:

```text
RAISED (ACTIVE, UNACKNOWLEDGED, UNSHELVED)
  -> ACTIVE + ACKNOWLEDGED            (awareness recorded; still ACTIVE; still blocking)
  -> condition clears
  -> CLEARED + cleared-ack PENDING    "RETURNED TO NORMAL - ACK REQUIRED"; still blocking
  -> Operator acknowledges the CLEARED state
  -> CLEARED + cleared-ack ACKNOWLEDGED
  -> block released if no other blocking condition remains
```

Return of the condition after clearing raises a new instance. Instance identity, chattering
suppression, and grouping rules are `[OPEN]`.

## 8. Specific approved alarm behaviours

| Situation | Required behaviour |
| --- | --- |
| `TC_F <= TC_R` diagnostic | Raise the diagnostic; keep calculating DirtyScore; preserve both temperatures in the Historian; do not block queue eligibility solely because of this diagnostic `[APPROVED]` |
| Valve fault during an active Cleaning Job | Blocking alarm, job `FAILED` or `RECOVERY_REQUIRED`, next job blocked `[APPROVED]` |
| Abnormal valve feedback while idle with the pump running | Alarm, pause countdown, Operator modal with Stop All / Continue With Valve Excluded `[APPROVED]` |
| DCS-related communication or stale-data condition becomes blocking while no Cleaning Job is active | Alarm, stop next-job countdown, no dispatch, require recovery and **cleared-state acknowledgement** before the countdown resumes `[APPROVED]` |
| Same condition becomes blocking while a Cleaning Job is active | Current job reaches its approved terminal condition; alarm raised or retained; next job not dispatched; countdown stopped after the current job; recovery and cleared-state acknowledgement required `[APPROVED]` |
| Valve return to service | Requires valid closed feedback, cleared alarm, **cleared-state acknowledgement**, explicit Operator action, and runtime validation `[OWNER CONFIRMED]` |
| Communication loss | Alarm; must not be auto-cleared on reconnection without operator awareness `[PROPOSED]` |
| Pressure not ready within the rise timeout | Pump `FAULT` state and blocking alarm `[PROPOSED]` |

## 9. Display and operator interface requirements

- Colour alone must not be the only carrier of meaning; severity and state must also appear
  as text or shape. `[PROPOSED]`
- "RETURNED TO NORMAL - ACK REQUIRED" must use exactly that wording. `[APPROVED]`
- Shelved alarms must remain discoverable from the alarm interface. `[APPROVED]`
- A blocking condition must have a persistent, unambiguous indicator of why dispatch is
  held. `[PROPOSED]`
- A cleared alarm whose cleared-state acknowledgement is pending must remain visually
  distinguishable from an alarm acknowledged after clearing. `[OWNER CONFIRMED]`

## 9.1 Alarm path, colour, and write-path boundaries (Stage 0.2)

1. **Colour separation.** Dirty red is a process-condition colour for Sensor classification
   (see [`ARCHITECTURE.md`](ARCHITECTURE.md) section 26.3). It must never be used as an alarm
   severity colour. Alarm severity remains recognisable through icon, border, text, the alarm
   banner, and the dedicated alarm workspace.
2. **Alarm evaluation is not blocked by the database.** Alarm evaluation and the live alarm
   surface must not wait on a Historian or SQL write. Alarm evaluation may read the in-memory
   Published Configuration Snapshot and must not query SQL every cycle
   (see [`ARCHITECTURE.md`](ARCHITECTURE.md) sections 30 and 31).
3. **The alarm write path is a specific, bounded path**, not a raw per-signal insert. Alarm
   state transitions are persisted with the event, audit, and job records, whereas continuous
   sample history belongs to the Historian path with its own bounded queue and batch writes.
4. **Alarm priority is independent of Historian health.** A degraded or unavailable Historian
   must not suppress, delay, mask, or silently clear an alarm, and must not change the
   acknowledgement model in section 3.
5. **[`ADR-0009`](decisions/ADR-0009-database-access-and-migrations.md) governs database
   access and migration execution.** The audit-required refusal policy (ARC-025) never extends
   to a stop, de-energize, or release action, and must not be driven by Historian backlog.
6. **Blocking release, acknowledgement, and shelving semantics are unchanged.** This
   subsection adds boundaries only; it does not alter section 3 through section 6.

## 10. Open items

| Item | Status |
| --- | --- |
| Alarm definition catalogue and thresholds | `[OPEN]` |
| Which alarms are blocking, and their scope | `[OPEN]` |
| Which alarms are non-shelvable | `[OPEN]` |
| Shelving duration limits and per-alarm maximum values | `[OPEN]` |
| Permissions for acknowledge, shelve, and unshelve per severity | `[OPEN]` |
| Whether the active-awareness acknowledgement and the cleared-state acknowledgement are stored as separate fields or as one field reset on clearing | `[OPEN]` — the observable behaviour of section 3 is binding either way |
| Chattering, flood, and first-up grouping behaviour | `[OPEN]` |
| Bulk acknowledgement | `[OPEN]` |
| Alarm-to-Event recording granularity | `[OPEN]` |

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — ALM, COMH, and OVR requirements
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — valve fault and communication-loss workflows
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — effect of blocking on dispatch
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — permissions and audit
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — alarm history retention
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — what the alarm system is not
