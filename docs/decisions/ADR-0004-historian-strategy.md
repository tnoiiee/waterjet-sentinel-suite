# ADR-0004 — Historian Strategy

- **Status:** ACCEPTED
- **Date:** 2026-10-04
- **Scope:** Where process history is stored, at what intervals, for how long, and how it is
  removed.
- **Authority:** Approved Stage 0.1 Scope Gate (Repository Documentation Foundation). The
  retention defaults inside this record are PROPOSED and require separate ratification.

---

## Context

The Historian must support several different uses at once, and they have conflicting
characteristics:

- **Incident investigation** needs high-resolution data for a short window: one second per
  sample during an active Cleaning Job, and one second around alarms.
- **Condition assessment** needs temperature behaviour at a normal resolution over months,
  to evaluate cleaning effectiveness and DirtyScore trends.
- **Audit and accountability** needs long retention of cleaning jobs, alarms, events, and
  audit records, because these are the evidence of what happened and who did it.
- **Diagnostics** needs a short window of low-value data with fast expiry.

The storage budget is fixed and modest: three 2 TB HDDs in RAID 5, described as approximately
4 TB of theoretical usable capacity before formatting and overhead. A naive "keep everything
at one second forever" approach cannot work on this budget. Equally, an approach that simply
overwrites the oldest data with the newest is wrong: it destroys audit evidence and makes
long-term condition assessment impossible.

There is also a correctness constraint that must be stated plainly: retention must not be
described or implemented as directly overwriting Day 1 with Day 301. Retention is
cutoff-based.

## Decision

1. **Engine:** SQL Server 2025 Standard, hosted on the Boiler Unit workstation.
2. **Storage:** three 2 TB HDDs in RAID 5, approximately 4 TB theoretical usable capacity
   before formatting and overhead. This is a statement of raw configuration, not a promise
   of usable space.
3. **Persistence intervals:**

   | Data | Interval |
   | --- | --- |
   | Modbus acquisition | 1 second |
   | Queue evaluation | 1 second |
   | Normal thermocouple historian | 5 seconds |
   | Active Cleaning Job detailed historian | 1 second |
   | Alarm-related detailed storage | 1 second |
   | Long-term aggregate | 1 minute |

4. **Tiered retention by category.** Raw high-resolution data has a short life; aggregated
   data has a long life; accountability data (jobs, alarms, events, audit) has the longest
   life.
5. **Proposed defaults** (require Owner ratification): raw temperature 300 days; one-minute
   aggregate 1,095 days; cleaning jobs 5 years; alarm history 3 years; event history 2 years;
   audit history 5 years; diagnostics 180 days; temporary exports 30 days.
6. **Independent configuration.** Retention is configurable separately per data category. A
   change to one category must not silently change another.
7. **Cleanup mechanism.** Cleanup uses scheduled cutoff-based deletion or partition
   maintenance. Retention is never implemented as in-place overwrite of the oldest records.
8. **Diagnostic preservation.** When `TC_F <= TC_R`, both temperature values are preserved in
   the Historian alongside the diagnostic condition.
9. **Audit accountability.** Audit history is retained at least as long as event history.

## Consequences

- Long-term growth is dominated by the one-minute aggregate (1,095 days), not by raw
  temperature (300 days). Sizing must reflect that.
- High-resolution data is available for incident investigation but is deliberately short-lived.
  An investigation that begins more than 300 days after an incident relies on aggregate data.
- Partition maintenance is the preferred cleanup mechanism, which implies a partition scheme
  must be designed. `[OPEN]`
- Because cleanup is cutoff-based and per-category, a mis-configured category can delete data
  earlier than intended. Cleanup must therefore be logged and diagnosable.
- Audit and event integrity become a design requirement rather than an afterthought. Tamper
  protection is `[OPEN]`.
- Backup and recovery are not solved by this decision. Whether the workstation is the only
  copy of the data must be resolved before deployment.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| In-place circular overwrite of oldest records | Destroys audit evidence, breaks long-term trends, and cannot be reasoned about safely |
| Single retention period for all data | Either wastes the budget on diagnostics or destroys accountability data, depending on the value chosen |
| Store everything at one second resolution forever | Exceeds the available storage by a wide margin |
| Store only one-minute aggregates | Loses the detail needed to investigate a job failure |
| External historian or cloud warehousing | Contradicts the standalone deployment and the one-installation-per-unit model |
| Keep audit records indefinitely | Not feasible on the fixed storage budget without an archiving process; the requirement is instead to keep them longest and to define archiving separately |

## Verification status

- `[APPROVED]`: engine, storage configuration statement, persistence intervals, per-category
  configurability, cutoff-based cleanup rule, and the prohibition on describing retention as
  overwriting Day 1 with Day 301.
- `[PROPOSED]`: every default retention value in item 5. These must be ratified by the Owner.
- `[NOT VERIFIED]`: effective usable capacity after formatting and overhead.
- `[OPEN]`: partition scheme, index strategy, aggregate function definition, backup and
  disaster recovery, audit tamper protection, clock discipline, and behaviour when the
  database is unavailable.
- No database, schema, or cleanup process exists. Nothing in this decision has been
  implemented or measured.

## References

- [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md) — full retention specification
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — timing model
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — HIS requirements
- [`../USER_PERMISSION_MODEL.md`](../USER_PERMISSION_MODEL.md) — audit record fields
- [`ADR-0002-deployment-architecture.md`](ADR-0002-deployment-architecture.md)
