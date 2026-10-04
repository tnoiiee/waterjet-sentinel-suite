# User and Permission Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the role templates, local-user model, session rules,
audit requirements, and the break-glass account. The concrete permission catalogue is
`[OPEN]`.

---

## 1. User model

- The application uses **local application users** `[APPROVED]`. There is no dependency on
  a central directory service, and the initial system is standalone.
- A user holds one or more roles.
- A role is a **configurable permission collection** `[APPROVED]`; role names are
  templates, not fixed behaviour.
- Credentials are never committed to this repository. See [`../SECURITY.md`](../SECURITY.md).

## 2. Default role templates

| Role | Intended purpose | Status |
| --- | --- | --- |
| Operator | Day-to-day supervision: viewing, queue actions, alarm acknowledgement, sequence start and stop | `[APPROVED]` as a template |
| Technician | Equipment-level tasks: valve and pump manual operation, diagnostics, return to service | `[APPROVED]` as a template |
| Engineer | Engineering configuration: thresholds, dwells, motion profiles, mappings | `[APPROVED]` as a template |
| Supervisor | Authorises operational exceptions, manual corrections, and shelving decisions | `[APPROVED]` as a template |
| Administrator | User and role management, retention configuration, system settings | `[APPROVED]` as a template |
| Break-glass Recovery Account | Recovery only, never for normal operation | `[APPROVED]` |

The specific permission assigned to each template is `[OPEN]` and must be ratified before
implementation. No permission may be assumed from a role name.

### Shared Operator account

A shared Operator account is permitted `[APPROVED]`. This must be stated plainly wherever
Operator actions are discussed, because of its consequence:

> **A shared Operator account prevents person-level Operator attribution.** Audit and Event
> records for Operator actions identify the account, not the individual who acted.

Consequences to carry into design and operations:

- Operator-held sessions are collectively authenticated only.
- Shift handover cannot be proven from audit records alone.
- Any action requiring individual attribution must use a named account with a distinct
  role, and must not use the shared Operator account.

## 3. Privileged sessions

Privileged sessions must support a **configurable inactivity timeout** `[APPROVED]`.

On timeout the system must `[APPROVED]`:

1. Log out the privileged user.
2. Return to the Operator session.
3. **Not** abort an active Auto Sequence.
4. Stop any manual hold-to-run operation.
5. Preserve or safely handle configuration drafts.

Points 3 and 4 are deliberately asymmetric: a session timeout is an authentication event,
not a process-safety event. It must never leave a manual operation running without a human
holding it, and it must never interrupt an automated sequence that is mid-job.

The timeout value is configurable. Its default is `[OPEN]`. Whether a warning is issued
before timeout is `[OPEN]`.

## 4. Break-glass recovery account

| Requirement | Detail |
| --- | --- |
| Purpose | Recovery only |
| Normal operation | Must **not** be used |
| Login record | Every login creates a **high-severity audit event** |
| Hardware safety | Cannot bypass hardware safety |
| Credentials | Must **never** be committed, documented in this repository, or transmitted through it |
| Recovery methods | The permitted recovery methods and their limits are `[OPEN]` |

## 5. Permission categories to be defined

The catalogue below is the planned scope. Values are `[OPEN]` until ratified.

| Category | Examples of permissions |
| --- | --- |
| Viewing | View process, trends, alarms, events, audit, diagnostics |
| Queue operation | Hold, Release Hold, Reject, Release Reject, Reorder |
| Alarm operation | Acknowledge, Shelve, Unshelve |
| Sequence control | Start Auto Sequence, Stop Auto Sequence, Stop All |
| Manual equipment | Manual pump start and stop, manual valve operation, manual motion jog and move |
| Return to service | Return a valve from `OUT_OF_SERVICE` |
| Data correction | Manual correction of `LastSuccessfulCleaningCompletedAt` |
| Configuration | Edit configuration, publish configuration, change motion profiles, change mappings |
| Retention and data | Configure retention, run cleanup, export, delete |
| Administration | Manage users, manage roles, manage break-glass recovery |

## 6. Audit and event requirements

Every one of the following must produce a durable record:

| Action | Required fields |
| --- | --- |
| Queue action | Timestamp, user, action, sensor, reason, original position, new position when applicable, Auto Sequence ID, queue snapshot reference |
| Manual timestamp correction | Sensor, previous timestamp, new timestamp, user, time, reason |
| Alarm acknowledgement | User, time, alarm instance |
| Alarm shelve and unshelve | User, time, alarm instance, reason, duration |
| Valve exclusion and return to service | User, time, valve, affected sensors, reason, Queue snapshot |
| Auto Sequence start and stop | User, time, reason |
| Configuration publication | User, time, what changed, previous and new values |
| Permission or role change | User, time, target user or role, previous and new state |
| Break-glass login | User, time, and a high-severity audit marker |

## 7. Open items

| Item | Status |
| --- | --- |
| Permission catalogue and role-to-permission mapping | `[OPEN]` |
| Authentication method and password policy | `[OPEN]` |
| Account lockout and failed-login handling | `[OPEN]` |
| Privileged timeout default value and pre-warning behaviour | `[OPEN]` |
| Configuration draft handling on timeout | `[OPEN]` |
| Whether user accounts may be disabled rather than deleted | `[OPEN]` |
| Audit record tamper protection | `[OPEN]` |
| Whether role changes take effect for an active session immediately | `[OPEN]` |

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — USR requirements
- [`../SECURITY.md`](../SECURITY.md) — security policy and secret handling
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — who may command what
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue action audit fields
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — audit retention
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — acknowledgement and shelving permissions
