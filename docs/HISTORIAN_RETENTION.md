# Historian and Retention — WaterJet Sentinel Suite (WJSS)

**Document status:** The database target, storage description, and acquisition intervals
are `[APPROVED]`. Default retention values are `[PROPOSED]`. Storage design details and
capacity conclusions are `[OPEN]` / `[NOT VERIFIED]`.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 architecture checkpoint **SUBMITTED FOR OWNER REVIEW**; documentation
review **CHANGES REQUESTED / IN PROGRESS**; Owner manual review **PENDING**; **NOT MERGED**;
Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]`.

---

## 1. Database target

| Item | Value | Status |
| --- | --- | --- |
| Database engine | SQL Server 2025 Standard | `[APPROVED]` |
| Hosting | The Boiler Unit workstation | `[APPROVED]` |
| Storage configuration | Three 2 TB HDDs in RAID 5 | `[APPROVED]` |
| Capacity statement | **Approximately 4 TB theoretical usable capacity before formatting and overhead** | `[APPROVED]` |
| Effective usable capacity | Not calculated | `[NOT VERIFIED]` |

The capacity statement is a description of the raw configuration. It is **not** a capacity
guarantee and the system must not be documented as having 4 TB of usable storage.

Exact effective capacity is `[NOT VERIFIED]`. Exact row size, index overhead, compression,
detailed-window duration, transaction-log allocation, backup allocation, and query
requirements remain to be measured. **The dominant data category is `[NOT VERIFIED]` until
a capacity model is completed.** The repository does not currently contain — and must not
contain — a conclusion about which category dominates long-term growth.

The approved acquisition and retention direction in sections 2 and 3 is a **planning
baseline**, not a proven storage guarantee.

## 2. Acquisition and storage intervals

| Data | Interval | Status |
| --- | --- | --- |
| Modbus acquisition | 1 second | `[APPROVED]` |
| Queue evaluation | 1 second | `[APPROVED]` |
| Normal thermocouple historian | 5 seconds | `[APPROVED]` |
| Active Cleaning Job detailed historian | 1 second | `[APPROVED]` |
| Alarm-related detailed storage | 1 second | `[APPROVED]` |
| Long-term aggregate | 1 minute | `[APPROVED]` |

Acquisition and storage are separate concerns. A 1 second acquisition interval does not
imply that every acquired sample is persisted at 1 second. The duration of the
"active Cleaning Job detailed" window is `[OPEN]` and is one of the inputs a capacity model
needs.

## 3. Default retention

| Data category | Default retention | Status |
| --- | --- | --- |
| Raw temperature | 300 days | `[PROPOSED]` |
| One-minute aggregate | 1,095 days | `[PROPOSED]` |
| Cleaning jobs | 5 years | `[PROPOSED]` |
| Alarm history | 3 years | `[PROPOSED]` |
| Event history | 2 years | `[PROPOSED]` |
| Audit history | 5 years | `[PROPOSED]` |
| Diagnostics | 180 days | `[PROPOSED]` |
| Temporary exports | 30 days | `[PROPOSED]` |

Retention values are configurable **separately by data category** `[APPROVED]`. A change to
one category must not silently change another.

Ratification of these defaults is an Owner decision. They are proposals, and no cleanup
schedule may be built against them until they are ratified.

## 4. Cleanup rules

Cleanup must use **scheduled cutoff-based deletion** or **partition maintenance**
`[APPROVED]`.

Explicitly prohibited framing and behaviour: retention must **not** be described, designed,
or implemented as "Day 1 is overwritten by Day 301". That description is wrong for a
cutoff-based scheme and must not appear in code comments, configuration, or documentation.

Requirements for the cleanup mechanism `[PROPOSED]`:

1. Cleanup runs on a schedule, not on a per-write basis.
2. Cleanup deletes whole periods at or before a computed cutoff for each category.
3. Cleanup is logged, with category, cutoff, affected row or partition count, and outcome.
4. Cleanup must never delete data inside the retention window of another category.
5. Cleanup failure raises a diagnostic condition.
6. Cleanup must not block acquisition or queue evaluation.

## 5. Data categories and their content

| Category | Contains |
| --- | --- |
| Raw temperature | Per-channel temperatures at the normal historian interval, including `TC_F` and `TC_R` values preserved for `TC_F <= TC_R` diagnostics |
| One-minute aggregate | Aggregate of raw temperature over one minute. Aggregate function is `[OPEN]` (average, minimum, maximum, or a combination) |
| Cleaning jobs | Job records: sensor, Water Jet, valve, start, end, outcome, verification results |
| Alarm history | Alarm instances with condition, active acknowledgement, cleared-state acknowledgement, shelving, user, and timing |
| Event history | Operator and system events, including queue snapshots and DCS Permissive Override activation and release |
| Audit history | Permission changes, configuration publication, manual timestamp corrections, DCS override records, break-glass logins |
| Diagnostics | Diagnostic conditions, communication quality and health, cleanup results |
| Temporary exports | Generated export files pending expiry |

## 6. Data integrity and auditing

- Audit history is retained at least as long as event history `[PROPOSED]`. Audit records
  preserve user, timestamp, subject, previous value, new value, and reason where required.
- Tamper protection for audit and event records is `[OPEN]` and must be specified before
  deployment. It must not be assumed to exist.
- Manual correction of `LastSuccessfulCleaningCompletedAt` must record sensor, previous
  timestamp, new timestamp, user, time, and reason `[APPROVED]`.
- DCS Permissive Override records must capture activation, release, user, timestamp, and
  reason `[OWNER CONFIRMED]`.

## 7. Capacity and sizing

A capacity model has **not** been produced, and no capacity conclusion may be stated until
it is. The following remain to be determined:

- Row sizes and index overhead per category.
- Daily and annual growth by category.
- The interaction between raw temperature retention and one-minute aggregate retention.
  The relative contribution of each category to long-term growth is `[NOT VERIFIED]` and
  must be established by measurement or by an explicit, stated model — not assumed.
- The duration of the detailed-storage window that applies during active Cleaning Jobs and
  around alarms.
- Compression behaviour and whether it applies.
- Transaction-log allocation and growth behaviour.
- Backup allocation and retention.
- Query requirements and index strategy.
- Partition scheme and filegroup layout for partition maintenance.
- Backup, restore, and disaster-recovery approach, including whether the workstation disk
  set is the only copy.
- Clock discipline for timestamped data.

All of the above are `[OPEN]`. No database benchmark has been performed, and none is
claimed.

## 8. Deletion and export lifecycle

- Temporary exports expire after the configured retention (default proposal 30 days).
- Deleting or exporting data must never create a gap that breaks trend continuity without
  the Operator being able to see why. `[PROPOSED]`
- Whether the Operator may delete data manually is `[OPEN]`. If permitted, it must be
  permission-controlled and audited.

## 8.1 Write-path decoupling and bounded buffering (Stage 0.2)

1. **The Historian write path is decoupled from acquisition.** Modbus acquisition, Galil
   monitoring, queue evaluation, alarm evaluation, live UI state delivery, and a valid Main Pump
   stop request (PMP-007) must not be blocked by a slow, degraded, or unavailable database.
2. **The writer is bounded and batched.** The design requirement is a bounded in-memory queue,
   batch inserts, health metrics, and data-gap reporting. Unbounded growth is prohibited, and
   silent data loss is prohibited. See [`ARCHITECTURE.md`](ARCHITECTURE.md) section 31.
3. **An explicit backpressure or overflow policy is required before implementation** and
   remains `[OPEN]`. The Runtime must either slow, drop with an explicit recorded gap, or spool
   under a defined retention limit — the choice is an Owner decision recorded in a later gate,
   not an implementation-time guess.
4. **Data-loss reporting is a first-class requirement.** Any drop, overflow, or gap must be
   recorded and reported as a data gap in the affected signal's history rather than presented
   as continuous data.
5. **Historian policy is separate from audit-required action policy.** Historian backlog,
   degradation, or outage must not determine which initiating actions are permitted
   (ARC-025), and must not alter alarm semantics (see [`ALARM_MODEL.md`](ALARM_MODEL.md)
   section 9.1).
6. **Retention, cleanup, and capacity conclusions are unchanged** and remain as recorded in
   sections 3, 4, and 7, including the capacity statements that are `[OPEN]` or
   `[NOT VERIFIED]`.

## 9. Unavailability behaviour

Behaviour when the database is unavailable is `[OPEN]`. The following is `[PROPOSED]` as a
design constraint, not as approved behaviour:

- Loss of the Historian must not remove the Operator's visibility of live process state.
- Control decisions must not depend on the Historian being reachable.
- Data loss during an outage must be visible as a gap, not silently backfilled with
  fabricated values.

---

## Related documents

- [`REQUIREMENTS.md`](REQUIREMENTS.md) — HIS requirements
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — timing model and subsystem layout
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — alarm instances and cleared-state acknowledgement
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — audit requirements
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — DCS override auditing
- [`decisions/ADR-0004-historian-strategy.md`](decisions/ADR-0004-historian-strategy.md)
