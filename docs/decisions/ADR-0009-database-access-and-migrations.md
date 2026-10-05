# ADR-0009 — Database Access and Migration Strategy

- **Status:** ACCEPTED — recorded by the Owner at Stage 0.2 acceptance (merged through PR #2,
  merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`). Accepted as architecture
  direction; **accepted does not mean implemented**. Selections marked `[PROPOSED]`,
  `[OPEN]`, or `[NOT VERIFIED]` below keep those markers.
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
   one-second detail during an active Cleaning Job, across 212 channels.
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

2. **Data-access technology — architecture language, not a library name.**
   - **Transactional relational access** for configuration, alarm, event, audit, cleaning-job,
     user and permission, and queue-snapshot data. These writes are small, correctness-critical,
     and must be transactional with the state change they record.
   - **Batch-oriented write path for Historian samples.** High-rate sample data is written in
     batches on a decoupled path, not one row at a time on the control path.
   - **Exact ORM, mapper, micro-ORM, provider, and bulk-write mechanism remain `[OPEN]`.** No
     technology is selected by this record, and no technology may be selected merely by using
     the generic word "mapper". A specific library may be chosen only at the implementation
     Stage Gate, with licence and offline-availability evidence.
   - **A measured escape hatch is allowed only after evidence identifies a bottleneck.** If
     measurement shows that the chosen transactional access cannot meet the approved one-second
     detail requirement on the target workstation, a narrower set-based write path may be
     introduced for that specific path. It must stay behind the same persistence boundary,
     must be covered by the same tests, and must not leak into domain logic.
   - **`[OPEN]`:** provider version and verified compatibility with SQL Server 2025 Standard.
     No package is installed and no compatibility claim is made in this Stage.

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
   - The flow is: acquisition → authoritative in-memory state → queue and alarm evaluation →
     UI push → bounded Historian channel → batch database writer.
   - **A slow database write must not directly block** Modbus acquisition, Galil monitoring,
     queue evaluation, alarm evaluation, live UI updates, or a valid Main Pump stop request
     (PMP-007).
   - Required properties: bounded queue; batch writes; health metrics; data-gap reporting; no
     unbounded memory growth; no silent data loss.
   - Writes are prioritised: accountability records (audit, event, alarm, job outcome) rank
     above active-job detail, which ranks above normal thermocouple samples, which ranks above
     aggregates.
   - **Historian policy is separate from Audit-required action policy.** A Historian backlog,
     degradation, or outage must not silently change which initiating actions are permitted;
     the audit-required refusal set is governed by item 7 below, not by the Historian channel's
     backlog state.
   - An **explicit backpressure or overflow policy** is required **before Historian
     implementation**. Overflow, spool, retry, priority, and database-outage policy remain
     `[OPEN]` and must be closed before that implementation.
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
11. **SQL Server is not the per-cycle operational parameter source.** This boundary addresses
    the legacy behaviour in which repeated parameter queries delayed the application.
    - The runtime loads the Published configuration at startup and validates it.
    - The runtime holds an **immutable in-memory Published Configuration Snapshot**.
    - Queue, alarm, acquisition, motion, and Cleaning Job logic read the in-memory snapshot,
      not the database.
    - Configuration is reloaded only after an explicitly Published revision or an approved
      startup or recovery action ([ADR-0011](ADR-0011-configuration-and-secrets.md)).
    - A validated new revision is swapped in **atomically**, under the approved runtime state
      gate.
    - A Draft is never consumed by the runtime.
    - The UI does not query SQL for operational parameters every second.
    - Device polling does not query SQL for static parameters every cycle.
12. **Where SQL remains appropriate.** Versioned configuration persistence; alarm history;
    event history; audit history; cleaning-job history; the Historian; reports; and on-demand
    historical queries. No schema, script, migration, or connection string is created by this
    record or by Stage 0.2.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| A single prescribed data-access library, named now | The available evidence does not justify one. Naming a library here would be tool-ambiguous and would pre-empt licence and compatibility review | Rejected — architecture language recorded; library `[OPEN]` |
| Transactional relational access with no escape hatch | Simplest and most uniform; but may fail the approved one-second detail requirement under 212-channel load. The requirement outranks uniformity | Rejected as an absolute rule; the measured escape hatch is permitted |
| Hand-written SQL and stored procedures for everything | Maximum control and explainability, but much slower to build and maintain, and duplicates mapping logic already covered by the transactional access layer | Rejected |
| Raw low-level ADO-style access with no mapping layer | No productivity or maintainability benefit for a small team | Rejected |
| Committing to a specific high-rate write mechanism before measurement | Would record an unverifiable performance claim and pre-empt the licence and compatibility review | Rejected — decision deferred to measurement at the implementation gate |
| Reading operational parameters from SQL each cycle | Causes the legacy failure mode: database latency becomes control-path latency | Rejected — in-memory Published Configuration Snapshot is required |
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
- Because operational parameters live in an in-memory snapshot, the database can be slow or
  unavailable without slowing acquisition, queue evaluation, alarm evaluation, or the live UI.
- That improvement has a price: configuration relevance is now a runtime responsibility. A
  revision that is published but not yet applied is **not** in force, and the runtime state
  gate — not the publication itself — decides when it takes effect.
- Omitting a library name keeps the decision testable but leaves a concrete selection to the
  implementation gate, together with its licence and offline-availability review.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| The chosen transactional access path cannot sustain the approved sample rates | Sample gaps or control-loop pressure | Measurement at the implementation gate; a measured, narrowly scoped escape hatch is permitted for the specific bottleneck | `[OPEN]` |
| In-memory configuration snapshot diverges from the persisted published revision | Runtime behaves on stale parameters | Snapshot is created only from a validated published revision; revision identifier is stamped and displayed; reload only on publication or approved startup/recovery | `[PROPOSED]` |
| Historian backlog policy left implicit | Either data loss or memory growth, discovered late | Explicit backpressure or overflow policy required before Historian implementation | `[OPEN]` |
| Overflow policy for the write queue left implicit | Either data loss or memory growth is discovered in production | Policy must be explicit before the historian write path is implemented | `[OPEN]` |
| Migration applied without a working backup | Unrecoverable schema change | Mandatory pre-migration backup and a verified restore path | `[PROPOSED]` |
| Provider/version incompatibility with SQL Server 2025 Standard | Late rework | Verify compatibility before the first data-access code is written | `[NOT VERIFIED]` |
| Audit-required refusals during an outage surprise operators | Operational confusion during an incident | Operator-visible explanation and documentation; the candidate set requires Owner ratification | `[PROPOSED]` |
| Local-only backups lost with the workstation | Total loss of history and accountability data | Off-box copy decision is `[OPEN]` and must be resolved before deployment acceptance | `[OPEN]` |

## Verification status

- `[NOT VERIFIED]`: SQL Server connectivity, schema creation, migration execution, restore,
  backup, and any throughput or capacity property. No database has been contacted.
- `[NOT VERIFIED]`: provider compatibility with SQL Server 2025 Standard, and the behaviour of
  any specific ORM, mapper, micro-ORM, or bulk-write mechanism — none is selected.
- `[NOT VERIFIED]`: Historian physical sizing, the dominant data category, and every input to
  the capacity model ([`ADR-0004`](ADR-0004-historian-strategy.md)).
- `[OPEN]`: ORM, mapper, micro-ORM, provider, and bulk-write mechanism; migration tooling
  identity; isolation levels and batch sizing; Historian overflow, spool, retry, priority, and
  outage policy; backup schedule and off-box copy; restore testing; whether a published
  configuration revision can be applied without a runtime restart.
- No database object, script, migration, or connection string is created by this Stage.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Data-access mechanism selection (ORM, mapper, micro-ORM, or hand-written path) with licence, offline-availability, and compatibility evidence | Implementation Stage Gate |
| Configuration snapshot application procedure and state gate | Implementation Stage Gate |
| Migration tooling identity and execution procedure | Implementation Stage Gate |
| Historian write-path measurement and escape-hatch decision | Implementation Stage Gate, with measurement on the target workstation |
| Overflow, spool, retry, priority, and database-outage policy for the write queue, including its separation from the audit-required refusal policy | Implementation Stage Gate, before Historian implementation |
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
  (section 17), configuration hot path (section 30), Historian decoupling (section 31)
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — HIS, TMP, TSB, ALM, PMP, and ARC groups
- [`ADR-0004-historian-strategy.md`](ADR-0004-historian-strategy.md)
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0011-configuration-and-secrets.md`](ADR-0011-configuration-and-secrets.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
