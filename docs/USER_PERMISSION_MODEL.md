# User and Permission Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the role templates, local-user model, session rules,
audit requirements, and the break-glass account. The concrete permission catalogue is
`[OPEN]`. The DCS Permissive Override requirements are `[OWNER CONFIRMED]`.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**IN PROGRESS**, PR #3 **OPEN**, **NOT MERGED**. React final selection **NOT YET APPROVED** (UI
framework `[OPEN]`). Blazor counter-spike **DEFERRED / `[NOT AUTHORIZED]`**. Stage 0.3
`[NOT AUTHORIZED]`. Production device access `[NOT AUTHORIZED]`.

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
| Operator | Day-to-day supervision: viewing, queue actions, alarm acknowledgement, sequence start and stop, DCS Permissive Override **activation and release** | `[OWNER CONFIRMED]` as the baseline template |
| Technician | Equipment-level tasks: valve and pump manual operation, diagnostics, return to service | `[APPROVED]` as a template |
| Engineer | Engineering configuration: thresholds, dwells, motion profiles, mappings, DCS permissive definitions | `[APPROVED]` as a template |
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
- **The DCS Permissive Override is subject to this limitation.** An override record will
  identify the shared Operator account, not the individual who activated or released it.

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

## 5. DCS Permissive Override permissions

The DCS Permissive Override requires an explicit permission `[OWNER CONFIRMED]`.

### 5.1 Baseline role assignment

**The Operator role template has permission to activate and release the DCS Permissive
Override.** `[OWNER CONFIRMED]`

| Rule | Statement |
| --- | --- |
| Activation | The Operator role template includes permission to activate the override |
| Release | The Operator role template includes permission to release the override |
| Denial | A role that does not hold the required permission cannot activate or release the override |
| Configurability | The permission model remains configurable; future authorised configuration may change role-permission assignments |
| Baseline | The assignment remains Operator until an explicit Owner decision changes it |
| Exclusions | Holding the permission does not weaken the exclusion list in [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009 |

The permission to activate the override must not be conflated with the permission to change
DCS permissive definitions. Configuration of DCS permissives is an engineering activity;
activating the override is an operational activity.

### 5.2 Shared Operator account and override attribution

The override is subject to the shared Operator-account limitation `[OWNER CONFIRMED]`:

- Event and Audit records identify the **shared Operator account**.
- They do **not** provide individual person-level attribution.
- Named individual accounts, if introduced, would be a **future approved scope** item; they
  are not part of the current baseline.

### 5.3 Override property summary

| Requirement | Detail |
| --- | --- |
| Activation | Explicit action with a confirmation step |
| Release | Manual. There is no automatic time expiry in the current approved baseline |
| Reason | Required on activation and on release |
| Visibility | Persistent visible banner while active |
| Recording | Activation, release, user, timestamp, and reason in Event and Audit history |
| Scope | The approved DCS permissive evaluation only. Never a general "Ignore DCS" function |
| Exclusions | Must not bypass any item listed in [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009 |

## 6. Permission categories to be defined

The catalogue below is the planned scope. Values are `[OPEN]` until ratified.

| Category | Examples of permissions |
| --- | --- |
| Viewing | View process, trends, alarms, events, audit, diagnostics |
| Queue operation | Hold, Release Hold, Reject, Release Reject, Reorder |
| Alarm operation | Acknowledge (active awareness), Acknowledge cleared state, Shelve, Unshelve |
| Sequence control | Start Auto Sequence, Stop Auto Sequence, Stop All |
| Manual equipment | Manual pump start and stop, manual valve operation, manual motion jog and move |
| Return to service | Return a valve from `OUT_OF_SERVICE` |
| DCS override | Activate and release the DCS Permissive Override |
| Data correction | Manual correction of `LastSuccessfulCleaningCompletedAt` |
| Configuration | Edit configuration, publish configuration, change motion profiles, change mappings, define DCS permissives |
| Retention and data | Configure retention, run cleanup, export, delete |
| Administration | Manage users, manage roles, manage break-glass recovery |

Note that active awareness acknowledgement and cleared-state acknowledgement are listed
separately. Whether they require the same permission is `[OPEN]`; the behavioural
distinction between them is `[OWNER CONFIRMED]` and is described in
[`ALARM_MODEL.md`](ALARM_MODEL.md) section 3.

## 7. Audit and event requirements

Every one of the following must produce a durable record:

| Action | Required fields |
| --- | --- |
| Queue action | Timestamp, user, action, sensor, reason, original position, new position when applicable, Auto Sequence ID, queue snapshot reference |
| Manual timestamp correction | Sensor, previous timestamp, new timestamp, user, time, reason |
| Alarm acknowledgement (active awareness) | User, time, alarm instance |
| Alarm cleared-state acknowledgement | User, time, alarm instance |
| Alarm shelve and unshelve | User, time, alarm instance, reason, duration |
| Valve exclusion and return to service | User, time, valve, affected sensors, reason, Queue snapshot |
| Auto Sequence start and stop | User, time, reason. Stop records the Queue snapshot and the Held/Rejected/Reordered state |
| DCS Permissive Override | User, timestamp, reason, and event type (activation or release) |
| Configuration publication | User, time, what changed, previous and new values |
| Permission or role change | User, time, target user or role, previous and new state |
| Break-glass login | User, time, and a high-severity audit marker |

## 7.1 Session, command, and UI boundaries (Stage 0.2)

1. **A privileged session has an approved timeout and the timeout is enforced by the Runtime**,
   not only by the Operations UI. UI-side hiding or UI-side timers are convenience only and are
   never the control.
2. **UI close, crash, or restart must not grant, extend, or silently renew authority**, and must
   not terminate or release an active Cleaning Job.
3. **Logout or privileged-session timeout during an active Cleaning Job** must be an
   Owner-decided, documented behaviour before implementation, and must not create a state in
   which a job continues without a supervision path. That behaviour remains `[OPEN]` until
   ratified.
4. **The UI cannot write to hardware and cannot open a device session.** A UI control that
   appears to actuate a device issues a command request through the approved command path; the
   Equipment Runtime performs the actuation after authorization, lifecycle, interlock,
   ownership, and command-state validation (see [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md)
   and [`ARCHITECTURE.md`](ARCHITECTURE.md) section 14.3).
5. **No permission can bypass a lifecycle, interlock, or ownership rule**, and none can permit
   concurrent Cleaning Jobs.
6. **The DCS Permissive Override remains scoped, enumerated, and non-delegable** as recorded in
   section 5. Its exclusions, the absence of automatic expiry, and the cleared-state
   acknowledgement requirement are unchanged.
7. **Audit-required initiating actions** are refused when the required audit record cannot be
   persisted (ARC-025), and the candidate set is an Owner-ratified decision, not an
   implementation choice. Historian backlog must not be used to decide it.

## 8. Open items

| Item | Status |
| --- | --- |
| Permission catalogue and the full role-to-permission mapping, beyond the Operator Override assignment settled below | `[OPEN]` |
| Whether active awareness acknowledgement and cleared-state acknowledgement share a permission | `[OPEN]` |
| Authentication method and password policy | `[OPEN]` |
| Account lockout and failed-login handling | `[OPEN]` |
| Privileged timeout default value and pre-warning behaviour | `[OPEN]` |
| Configuration draft handling on timeout | `[OPEN]` |
| Whether user accounts may be disabled rather than deleted | `[OPEN]` |
| Audit record tamper protection | `[OPEN]` |
| Whether role changes take effect for an active session immediately | `[OPEN]` |
| Whether named individual accounts are introduced in a future approved scope | `[OPEN]` |

Resolved by Owner confirmation and therefore **not** open: the baseline role assignment for
the DCS Permissive Override. The Operator role template holds permission to activate and to
release it, and a role without the permission cannot do either. See section 5.

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — USR and OVR requirements
- [`../SECURITY.md`](../SECURITY.md) — security policy and secret handling
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — who may command what, and the override
- [`QUEUE_MODEL.md`](QUEUE_MODEL.md) — queue action audit fields
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — acknowledgement and shelving
- [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) — audit retention
