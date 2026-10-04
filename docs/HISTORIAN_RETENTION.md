# Historian and Retention — WaterJet Sentinel Suite (WJSS)

**Document status:** The database target, storage description, and acquisition intervals
are `[APPROVED]`. Default retention values are `[PROPOSED]`. Storage design details are
`[OPEN]`.

---

## 1. Database target

| Item | Value | Status |
| --- | --- | --- |
| Database engine | SQL Server 2025 Standard | `[APPROVED]` |
| Hosting | The Boiler Unit workstation | `[APPROVED]` |
| Storage configuration | Three 2 TB HDDs in RAID 5 | `[APPROVED]` |
| Capacity statement | Approximately 4 TB theoretical usable capacity **before** formatting and overhead | `[APPROVED]` |

The capacity statement is theoretical. Effective capacity after formatting, RAID overhead,
database files, indexes, and working space has not been calculated and is `[NOT VERIFIED]`.
The system must not be documented as having 4 TB of usable storage.

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
imply that every acquired sample is persisted at 1 second.

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
| Alarm history | Alarm instances with condition, acknowledgement, shelving, user, and timing |
| Event history | Operator and system events, including queue snapshots |
| Audit history | Permission changes, configuration publication, manual timestamp corrections, break-glass logins |
| Diagnostics | Diagnostic conditions, communication quality, cleanup results |
| Temporary exports | Generated export files pending expiry |

## 6. Data integrity and auditing

- Audit history is retained at least as long as event history `[PROPOSED]`. Audit records
  preserve user, timestamp, subject, previous value, new value, and reason where required.
- Tamper protection for audit and event records is `[OPEN]` and must be specified before
  deployment. It must not be assumed to exist.
- Manual correction of `LastSuccessfulCleaningCompletedAt` must record sensor, previous
  timestamp, new timestamp, user, time, and reason `[APPROVED]`.

## 7. Capacity and sizing

Not yet calculated. The following must be determined before deployment:

- Row sizes and index overhead per category.
- Daily and annual growth by category.
- The interaction between raw temperature retention and one-minute aggregate retention,
  including the fact that aggregate retention (1,095 days) far exceeds raw retention
  (300 days), so aggregate storage dominates long-term growth.
- Partition scheme and filegroup layout for partition maintenance.
- Backup, restore, and disaster-recovery approach, including whether the workstation disk
  set is the only copy.
- Clock discipline for timestamped data.

All of the above are `[OPEN]`.

## 8. Deletion and export lifecycle

- Temporary exports expire after the configured retention (default proposal 30 days).
- Deleting or exporting data must never create a gap that breaks trend continuity without
  the Operator being able to see why. `[PROPOSED]`
- Whether the Operator may delete data manually is `[OPEN]`. If permitted, it must be
  permission-controlled and audited.

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
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — alarm instances and history
- [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) — audit requirements
- [`decisions/ADR-0004-historian-strategy.md`](decisions/ADR-0004-historian-strategy.md)
