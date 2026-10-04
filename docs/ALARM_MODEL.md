# Alarm Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for severities, the three alarm dimensions, blocking
release, shelving, and display wording. Alarm definitions and threshold values are
commissioning values and are `[NOT VERIFIED]`.

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

Combinations and required presentation:

| Condition | Acknowledgement | Shelving | Presentation |
| --- | --- | --- | --- |
| `ACTIVE` | `UNACKNOWLEDGED` | `UNSHELVED` | Standard active alarm, most prominent |
| `ACTIVE` | `ACKNOWLEDGED` | `UNSHELVED` | Active, acknowledged |
| `ACTIVE` | `UNACKNOWLEDGED` | `SHELVED` | Shelved but active; still visible in the shelved list |
| `ACTIVE` | `ACKNOWLEDGED` | `SHELVED` | Shelved, active, acknowledged |
| `CLEARED` | `UNACKNOWLEDGED` | any | **"RETURNED TO NORMAL - ACK REQUIRED"** |
| `CLEARED` | `ACKNOWLEDGED` | `UNSHELVED` | Cleared and acknowledged; leaves the active list |
| `CLEARED` | `ACKNOWLEDGED` | `SHELVED` | Cleared while shelved; auto-unshelve applies |

"Acknowledging an alarm while it is still active does not release the block." `[APPROVED]`

## 3. Blocking release rule

A blocking condition is released only when **all three** hold `[APPROVED]`:

1. The condition is `CLEARED`.
2. The cleared state is `ACKNOWLEDGED`.
3. No other blocking alarm exists.

Consequences that must be implemented exactly:

- An `ACTIVE` alarm that has been acknowledged still blocks.
- A `CLEARED` but `UNACKNOWLEDGED` alarm still blocks and displays
  "RETURNED TO NORMAL - ACK REQUIRED".
- A `SHELVED` alarm that is still `ACTIVE` still blocks, because shelving suppresses
  presentation, not the condition.
- Releasing a block is never automatic on a timer and never silent.

## 4. Blocking scope

Blocking is scoped, not global by default. The intended scopes are `[PROPOSED]`:

| Scope | Effect |
| --- | --- |
| `JOB` | Prevents starting or continuing a Cleaning Job |
| `SEQUENCE` | Prevents the Auto Sequence from dispatching the next job |
| `EQUIPMENT` | Prevents commanding the affected equipment item |
| `SENSOR` | Removes the sensor from queue eligibility and refill |

Which scope each alarm definition carries is `[OPEN]` and must be ratified before
implementation. A valve fault during a job is blocking for the job; an idle valve fault
pauses the sequence countdown; both are `[APPROVED]` behaviours that map onto these scopes.

## 5. Shelving

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
acknowledged by the unshelve.

## 6. Acknowledgement

- Acknowledgement requires permission. Which permission per severity is `[OPEN]`.
- Acknowledgement records user, timestamp, and the alarm instance.
- Acknowledgement of an already-acknowledged alarm is a no-op; whether it is re-recorded is
  `[OPEN]`.
- Bulk acknowledgement of a selection is `[OPEN]` as a capability.

## 7. Alarm instances and lifecycle

| Element | Description |
| --- | --- |
| Alarm definition | The configured rule: condition source, severity, thresholds, blocking scope, shelvability |
| Alarm instance | One occurrence: raise time, condition, acknowledgement, shelving, and history |

Lifecycle:

```text
RAISED (ACTIVE, UNACKNOWLEDGED, UNSHELVED)
  -> ACKNOWLEDGED                 (still ACTIVE, still blocking)
  -> CLEARED                      -> "RETURNED TO NORMAL - ACK REQUIRED" if unacknowledged
  -> ACKNOWLEDGED after clearing  -> block released if no other blocking alarm exists
```

Return of the condition after clearing raises a new instance. Instance identity, chattering
suppression, and grouping rules are `[OPEN]`.

## 8. Specific approved alarm behaviours

| Situation | Required behaviour |
| --- | --- |
| `TC_F <= TC_R` diagnostic | Raise the diagnostic; keep calculating DirtyScore; preserve both temperatures in the Historian; do not block queue eligibility solely because of this diagnostic `[APPROVED]` |
| Valve fault during an active Cleaning Job | Blocking alarm, job `FAILED` or `RECOVERY_REQUIRED`, next job blocked `[APPROVED]` |
| Abnormal valve feedback while idle with the pump running | Alarm, pause countdown, Operator modal with Stop All / Continue With Valve Excluded `[APPROVED]` |
| Communication loss | Alarm; must not be auto-cleared on reconnection without operator awareness `[PROPOSED]` |
| Pressure not ready within the rise timeout | Pump `FAULT` state and blocking alarm `[PROPOSED]` |

## 9. Display and operator interface requirements

- Colour alone must not be the only carrier of meaning; severity and state must also appear
  as text or shape. `[PROPOSED]`
- "RETURNED TO NORMAL - ACK REQUIRED" must use exactly that wording. `[APPROVED]`
- Shelved alarms must remain discoverable from the alarm interface. `[APPROVED]`
- A blocking condition must have a persistent, unambiguous indicator of why dispatch is
  held. `[PROPOSED]`

## 10. Open items

| Item | Status |
| --- | --- |
| Alarm definition catalogue and thresholds | `[OPEN]` |
| Which alarms are blocking, and their scope | `[OPEN]` |
| Which alarms are non-shelvable | `[OPEN]` |
| Shelving duration limits and per-alarm maximum values | `[OPEN]` |
| Permissions for acknowledge, shelve, and unshelve per severity | `[OPEN]` |
| Chattering, flood, and first-up grouping behaviour | `[OPEN]` |
| Bulk acknowledgement | `[OPEN]` |
| Alarm-to-Event recording granularity | `[OPEN]` |

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — ALM requirements
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — valve fault workflows
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — effect of blocking on dispatch
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — permissions and audit
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — alarm history retention
