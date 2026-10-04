# ADR-0009 — Database Access and Migration Strategy

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record addresses the data-access and migration questions left
  `[OPEN]` by [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11 and
  [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md) section 7.
- **Scope:** Connection ownership, data-access technology, transaction boundaries, the
  Historian write path, the Alarm/Event/Audit write path, configuration transactions, schema
  migration ownership and execution policy, behavior when SQL Server is unavailable, and the
  backup and restore boundary.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.6. The SQL Server 2025 Standard target is the approved baseline of
  [ADR-0004](ADR-0004-historian-strategy.md) and is not reopened here.

---

## Context

SQL Server 2025 Standard runs on the same workstation as the runtime service. It holds three
very different kinds of data:

1. **Accountability data** — Cleaning Job outcomes, alarms, events, audit records. These must
   be durable, complete, and never silently lost.
2. **Process history** — thermocouple samples at the approved intervals, including
   one-second detail during an active Cleaning Job, across 208 channels.
3. **Configuration and user data** — the published engineering configuration, its revision
   history, local user accounts, roles, and permissions.

These workloads conflict. Process history is high-volume and tolerates short interruption;
accountability data is lower-volume and must not be lost; configuration publication must be
atomic and audited.

Exact physical sizing is `[NOT VERIFIED]` and no capacity model exists
(`../HISTORIAN_RETENTION.md` section 7). No schema, script, migration, or connection string is
created in this Stage.

## Decision

1. **Connection ownership.** Only the runtime service process opens database connections. The
   UI never connects to the database; it reaches data exclusively through the Local
   Application API ([ADR-0007](ADR-0007-runtime-process-model.md) item 4). One process means
   one connection pool and one transaction policy to reason about.

2. **Data-access technology.**
   - **Primary: an object-relational mapper (EF Core)** for configuration, user and permission
     data, Cleaning Job records, alarms, events, audit records, and the schema history.
   - **Measured escape hatch.** The Historian's high-rate sample write path may use a narrower,
     set-based path (a mapper bulk operation or narrowly scoped hand-written SQL) **only**
     when a measurement shows the mapper path cannot meet the approved one-second detail
     requirement on the target workstation. Such a path must stay behind the same persistence
     boundary, must be covered by the same tests, and must not leak into domain logic.
   - **`[OPEN]`:** the EF Core major version, its SQL Server provider version, and verified
     compatibility with SQL Server 2025 Standard. No package is installed and no
     compatibility claim is made in this Stage.

3. **Migration ownership and mechanism.**
   - Schema changes are produced as **reviewed, versioned migrations** that are committed to
     the repository; the schema history table is the authority on the applied version.
   - Migrations are generated only from the persistence package and reviewed like code. No
     hand-edited schema drift, and no ad-hoc SQL applied to a deployment database outside the
     migration path.
   - The migration tooling identity, its packaging, and the exact invocation are `[OPEN]`.

4. **Migration execution policy.**
   - Migrations are **never applied automatically at service start**. A service start must not
     silently change the schema.
   - Migrations are executed as an explicit, offline, separately-invoked maintenance step,
     under an approved procedure, on a workstation that is in a stopped/idle state, with a
     verified pre-change backup.
   - A migration run must produce a recorded result (applied version, outcome, operator or
     maintainer identity, timestamp).
   - **Preferred direction: forward-only.** Rollback is by restoring the pre-change backup
     plus reinstalling the previous package, not by relying on reverse migrations. Whether
     reverse migrations are ever authored is `[OPEN]`.

5. **Transaction boundaries.**
   - **Configuration publication:** a single transaction that writes the new published
     revision, its validation result, and its audit record. Either the whole publication is
     recorded or none of it is. A partially published configuration must not be observable.
   - **Alarm state transitions** (condition, acknowledgement, clearance, shelving) are
     transactional with their Event record, so that no state change exists without its record.
   - **Historian sample writes** are batched. A failed batch is a data gap to be reported, not
     a control event, and must never block control.
   - **Queue snapshots, job outcomes, and audit records** are written in the same transaction
     as the state change they describe where the state change is authoritative.
   - Exact isolation levels and batch sizing are `[OPEN]` and must be set by measurement.
6. **Historian write path.**
   - The Historian writer is **decoupled from the control path**: acquisition, queue
     evaluation, and sequencing decisions must not wait on database writes.
   - Writes are prioritised: accountability records (audit, event, alarm, job outcome) rank
     above active-job detail, which ranks above normal thermocouple samples, which ranks above
     aggregates.
   - The queue between the runtime and the database is **bounded**. Overflow policy is
     `[OPEN]` and must be explicit; silently blocking the control loop or growing without
     bound are both unacceptable. Whether overflow is spooled to local disk is `[OPEN]`.
7. **Behaviour when SQL Server is unavailable.**
   - The runtime continues to supervise the process using live in-memory state; **live
     process visibility in the UI must not depend on the Historian**
     ([`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 9).
   - Process-history writes fail with an explicit, visible state; the system must not present
     missing history as if it were recorded.
   - **Stop and de-energize actions must never be gated on database availability.** This
     follows PMP-007: no application-level operational permissive may block a valid Main Pump
     stop request.
   - **Audit-required initiating actions** must be refused if their required audit record
     cannot be persisted. The candidate set is: DCS Permissive Override activation and
     release, configuration publication, manual correction of
     `LastSuccessfulCleaningCompletedAt`, valve return to service, and user or permission
     changes. This set is `[PROPOSED]` and requires Owner ratification; it does not extend to
     any stop, de-energize, or release action.
   - Whether the application must raise an explicit "database unavailable" alarm and how the
     recovery of pending writes is handled is `[OPEN]`.
8. **Backup and restore boundary.**
   - Backup is taken with the database engine's native backup mechanism to local storage
     outside the Git working tree; no dump is ever committed.
   - A pre-migration backup is mandatory. A periodic backup schedule, its retention, its
     target location, and whether the backup leaves the workstation are `[OPEN]` — noting that
     [`ADR-0004`](ADR-0004-historian-strategy.md) already records restore and off-box copy as
     unresolved.
   - **Restore must be testable and must be tested before deployment acceptance.** No restore
     has been performed and none is claimed.
9. **Local authentication stance.** Where the runtime service and the database run on the same
   workstation, the preferred direction is operating-system integrated authentication for the
   service identity, so that no database password is stored in configuration at all. The
   fallback (a locally stored credential in an OS-protected store) and the exact identity
   model are `[OPEN]`. No credential may ever be committed
   ([`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md)).
10. **No capacity or performance claim.** Exact Historian physical sizing remains
    `[NOT VERIFIED]`. This record makes no benchmark, throughput, or storage-sufficiency
    claim.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Mapper-only data access with no escape hatch | Simplest, uniform; but may fail the approved one-second detail requirement under 208-channel load. The requirement outranks uniformity | Rejected as an absolute rule; kept as the default |
| Hand-written SQL and stored procedures for everything | Maximum control and explainability, but much slower to build and maintain, and duplicates mapping logic already covered by the mapper | Rejected |
| Raw low-level ADO-style access with no mapping layer | No productivity or maintainability benefit for a small team | Rejected |
| Micro-ORM for all persistence | Middle ground, but two access technologies and no migration story of its own | Rejected |
| Auto-apply migrations at service start | Convenient, and wrong here: a service restart would silently change the schema on a live workstation, with no maintenance window, no backup, and no recorded intent | Rejected |
| Reverse migrations as the rollback mechanism | Reverse migrations are frequently untested and can destroy data that forward migration added. Backup-and-restore is the trustworthy rollback path | Rejected as the primary mechanism; `[OPEN]` as a supplement |
| Historian writes synchronous with the control loop | Simple ordering guarantee, but couples control determinism to database latency — the exact coupling the failure-isolation requirement forbids | Rejected |
| Unbounded in-memory write queue | Removes blocking, but converts a database outage into an unbounded memory growth failure | Rejected |
| Treating logs as the accountability record | Logs are diagnostics without the required fields, retention, or integrity expectations | Rejected |
| Blocking all actions while the database is unavailable | Would extend a database outage into an equipment-control outage, and would contradict PMP-007 for stop requests | Rejected |

## Consequences

- One process owns connections, transactions, and migrations; there is no second opinion about
  schema state.
- Schema change becomes a planned maintenance activity with a backup and a recorded result,
  not a side effect of restarting a service.
- Process history can degrade without stopping the plant, but accountability data is
  deliberately treated as mandatory for the actions that require it. This creates a real
  behavior: during a database outage, the DCS Permissive Override cannot be activated
  (`[PROPOSED]`).
- The Historian write path becomes a measurement task, not a settled design, and the escape
  hatch must be justified by data.
- Restore capability becomes a prerequisite for deployment acceptance, and it does not exist
  yet.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Mapper path cannot sustain the approved sample rates | Histogram gaps or control-loop pressure | Measurement at the implementation gate; the escape hatch is pre-approved by this decision | `[OPEN]` |
| Overflow policy for the write queue left implicit | Either data loss or memory growth is discovered in production | Policy must be explicit before the historian write path is implemented | `[OPEN]` |
| Migration applied without a working backup | Unrecoverable schema change | Mandatory pre-migration backup and a verified restore path | `[PROPOSED]` |
| Provider/version incompatibility with SQL Server 2025 Standard | Late rework | Verify compatibility before the first data-access code is written | `[NOT VERIFIED]` |
| Audit-required refusals during an outage surprise operators | Operational confusion during an incident | Operator-visible explanation and documentation; the candidate set requires Owner ratification | `[PROPOSED]` |
| Local-only backups lost with the workstation | Total loss of history and accountability data | Off-box copy decision is `[OPEN]` and must be resolved before deployment acceptance | `[OPEN]` |

## Verification status

- `[NOT VERIFIED]`: SQL Server connectivity, schema creation, migration execution, restore,
  backup, and any throughput or capacity property. No database has been contacted.
- `[NOT VERIFIED]`: EF Core and provider compatibility with SQL Server 2025 Standard.
- `[NOT VERIFIED]`: Historian physical sizing, the dominant data category, and every input to
  the capacity model ([`ADR-0004`](ADR-0004-historian-strategy.md)).
- `[OPEN]`: provider and mapper versions; migration tooling identity; isolation levels and
  batch sizing; overflow and spool policy; backup schedule and off-box copy; restore testing.
- No database object, script, migration, or connection string is created by this Stage.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Provider and mapper version pin, with compatibility evidence | Implementation Stage Gate |
| Migration tooling identity and execution procedure | Implementation Stage Gate |
| Historian write-path measurement and escape-hatch decision | Implementation Stage Gate, with measurement on the target workstation |
| Overflow, spool, and priority policy for the write queue | Implementation Stage Gate |
| Audit-required refusal set ratification | Owner review of Stage 0.2, then Implementation Stage Gate |
| Backup schedule, retention, and off-box copy | Deployment Stage Gate |
| Restore test execution | Test Stage Gate (planned) and deployment acceptance |
| Capacity model and sizing | Before retention defaults are ratified ([`ADR-0004`](ADR-0004-historian-strategy.md)) |

## Relationship to protected decisions

- **Preserved, not modified:** SQL Server 2025 Standard as the database target (HIS-001),
  the approved intervals (HIS-003), per-category retention configurability (HIS-005),
  cutoff-based cleanup and the prohibition on describing retention as overwriting Day 1 with
  Day 301 (HIS-006, [`ADR-0004`](ADR-0004-historian-strategy.md)), audit retention at least as
  long as event retention (`[PROPOSED]`), `LastSuccessfulCleaningCompletedAt` never being null
  (TSB-001), the cleared-state acknowledgement requirement (ALM-004 through ALM-006), and
  PMP-007 (no application-level operational permissive may block a valid Main Pump stop
  request).
- **Implements approved mandatory boundaries:** commands pass through runtime validation; the
  UI does not hold device sessions or database connections.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Production Device access
  `[NOT AUTHORIZED]`; WAGO fail-safe `[NOT VERIFIED]`; Stage 0.3 remains unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this data-access choice.

## References

- [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md) — retention, cleanup, capacity,
  unavailability behaviour
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — data, configuration, and secrets model
  (section 17)
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — HIS, TMP, TSB, ALM, PMP, and ARC groups
- [`ADR-0004-historian-strategy.md`](ADR-0004-historian-strategy.md)
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0011-configuration-and-secrets.md`](ADR-0011-configuration-and-secrets.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
