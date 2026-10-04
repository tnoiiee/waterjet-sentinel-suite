# Queue Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED]. Queue behaviour described here is deterministic by
construction and must be implemented exactly. Deviations require an approved change, not a
local decision.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 architecture checkpoint **SUBMITTED FOR OWNER REVIEW**; documentation
review **CHANGES REQUESTED / IN PROGRESS**; Owner manual review **PENDING**; **NOT MERGED**;
Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]`.

Identifiers used in worked examples are **illustrative placeholders only**. Real sensor
identifier formats and production mappings are not documented in this repository.

---

## 1. Overview and invariants

There are **eight source queues** and one dispatch queue per Boiler Unit:

| Queue | Cardinality | Orders by |
| --- | --- | --- |
| TempQueue | one per wall — **four in total** (Left, Rear, Right, Front) | DirtyScore descending, then scanOrder ascending |
| TimeQueue | one per wall — **four in total** (Left, Rear, Right, Front) | TimeSinceLastClean descending, then scanOrder ascending |
| GlobalQueue | one per Boiler Unit | FIFO after seeding and deduplication; target capacity eight unique Sensor entries |

Four TempQueues plus four TimeQueues give **eight source queues in total**.

Invariants, which must hold after every evaluation cycle `[APPROVED]`:

1. A sensor appears at most once in GlobalQueue.
2. GlobalQueue never contains a disabled, inhibited, invalid, or `OUT_OF_SERVICE` sensor.
3. No entry enters GlobalQueue if its HardMinimumCleaningInterval is unsatisfied.
4. No dummy or placeholder job is ever created to fill a position.
5. GlobalQueue holds at most eight entries and fewer than eight only when fewer than eight
   unique eligible sensors exist.
6. Ordering within each source queue is fully determined by data values plus stable
   scanOrder. No ordering depends on iteration order, arrival time, or UI state.
7. **At most one Cleaning Job may be ACTIVE within one installation at any time.** Queue
   operations must never produce concurrency. See section 8 and
   [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) section 2.

## 2. Source queue enumeration and seed order

The eight source queues are enumerated in this fixed order `[APPROVED]`:

| Seed index | Source |
| --- | --- |
| 1 | Temp Left |
| 2 | Temp Rear |
| 3 | Temp Right |
| 4 | Temp Front |
| 5 | Time Left |
| 6 | Time Rear |
| 7 | Time Right |
| 8 | Time Front |

This order is used for initial seeding. **GlobalQueue initial order is determined by this
fixed source order, not by a cross-wall DirtyScore comparison.** After seeding and
deduplication, GlobalQueue is FIFO and is not re-sorted by source or by value.

## 3. TempQueue

### 3.1 Eligibility

A sensor is **TempQueue eligible** only when all of the following hold [`REQUIREMENTS.md`](REQUIREMENTS.md) QUE-002:

1. The sensor is enabled.
2. The sensor configuration is valid.
3. `HardMinimumCleaningInterval` is satisfied for the sensor.
4. If `UseDirtyScoreThreshold` is true, `DirtyScore >= DirtyScoreThreshold`. If
   `UseDirtyScoreThreshold` is false, the threshold must not block eligibility.
5. The Dirty entry dwell is satisfied (section 3.3).

A `TC_F <= TC_R` diagnostic does not by itself remove eligibility. The system continues to
calculate DirtyScore, raises the diagnostic, and preserves both temperature values in the
Historian.

### 3.2 Sorting

Within each wall TempQueue, entries are ordered by:

1. Highest `DirtyScore` first.
2. `scanOrder` ascending as a stable tie-break.

**Sorting and tie-breaking are evaluated within each wall.** There is no cross-wall
DirtyScore ranking anywhere in the model.

### 3.3 Entry dwell

- The sensor must meet TempQueue criteria **continuously** for `TempQueueEntryDwellSeconds`
  before entering the queue.
- If the condition is lost before the dwell completes, the entry dwell is **reset** and
  restarts from zero when the condition returns.
- Default `TempQueueEntryDwellSeconds` is 10 seconds. It is configurable.

### 3.4 Removal dwell

- A queued sensor must **fail** TempQueue criteria continuously for
  `TempQueueRemovalDwellSeconds` before being removed.
- If it becomes dirty again before the removal dwell completes, it stays queued.
- Default `TempQueueRemovalDwellSeconds` is 10 seconds. It is configurable and is
  **independent** of the entry dwell.

### 3.5 Membership state machine

```text
NOT_QUEUED
  -> (criteria met continuously for entry dwell)      -> QUEUED
QUEUED
  -> (criteria fail continuously for removal dwell)   -> NOT_QUEUED
QUEUED
  -> (criteria fail, then met again before dwell)     -> QUEUED   (dwell timer discarded)
```

## 4. TimeQueue

Eligibility `[APPROVED]`:

```text
TimeSinceLastClean >= HardMinimumCleaningInterval
TimeSinceLastClean = CurrentTime - LastSuccessfulCleaningCompletedAt
```

- `LastSuccessfulCleaningCompletedAt` is never null and every sensor receives an explicit
  initial timestamp during configuration. The initial timestamp is a valid scheduling
  baseline.
- There is no null case and no `NEVER_CLEANED` special case. A sensor cannot be treated as
  "never cleaned", because that state does not exist in the model.

Sorting:

1. Longest `TimeSinceLastClean` first.
2. `scanOrder` ascending as a stable tie-break.

## 5. GlobalQueue seeding and deduplication

### 5.1 Seeding

A new Auto Sequence rebuilds GlobalQueue from current data. Seeding walks the eight sources
in the fixed order of section 2, and each source proposes its best eligible candidate:

| Source | Proposes |
| --- | --- |
| Temp Wall | That wall's TempQueue head, if eligible |
| Time Wall | That wall's TimeQueue head, if eligible |

A source with no eligible candidate proposes nothing and its seed position is left unfilled
for now (the FIFO refill rules of section 6 govern later fills).

### 5.2 Deduplication

A sensor must not appear more than once in GlobalQueue. If the same sensor is proposed by
more than one source:

1. Preserve the occurrence at the **earliest current GlobalQueue position**.
2. Record **all applicable source reasons** on the preserved entry, so the entry records
   both `TEMP_QUEUE` and `TIME_QUEUE` where applicable.
3. The later duplicate source advances to its **next eligible candidate** for the position
   it was filling.
4. Continue until eight unique entries are present or no eligible unique candidates remain.

### 5.3 Source ownership

Each GlobalQueue entry has exactly **one source owner** `[OWNER CONFIRMED]`:

1. The source owner is the source that **established the preserved earliest GlobalQueue
   position**.
2. Merged source reasons do not transfer or change source ownership. An entry may carry two
   reason flags while still having a single owner.
3. Operator Reorder changes dispatch position but not source ownership.
4. When the entry leaves GlobalQueue, tail refill uses its **original source owner**.

### 5.4 Worked example (illustrative identifiers)

```text
Situation:
  Position 2 is owned by TIME_LEFT and contains Sensor G1.
  Position 6 from TEMP_LEFT also proposes G1.

Resolution:
  step 1  Preserve G1 at Position 2. It is not moved and not duplicated.
  step 2  Record both TIME_QUEUE and TEMP_QUEUE as reasons on the preserved entry.
  step 3  TEMP_LEFT advances to its next eligible candidate for Position 6.
  pos 6   TEMP_LEFT proposes its next eligible candidate for Position 6 -> accepted.

Ownership:
  G1 at Position 2 remains owned by TIME_LEFT. Merging TEMP_QUEUE onto the entry
  did not transfer or change ownership.

Later refill:
  When G1 later leaves GlobalQueue, tail refill uses TIME_LEFT, because TIME_LEFT
  owns the preserved entry.
```

A source reason is recorded per preserved entry, so the G1 entry carries two reasons while
each other entry carries one. The reason flags are informational and diagnostic; they do not
affect ordering, which remains FIFO after seeding.

## 6. FIFO refill

When an entry leaves GlobalQueue (dispatched, rejected, invalidated, or excluded):

1. Remaining entries shift forward, preserving FIFO relative order.
2. The tail is refilled from the **source owner** of the entry that left.
3. The source must offer its next eligible unique sensor.
4. If no eligible candidate exists, the tail position remains empty.

Source ownership is fixed when an entry is seeded (section 5.3) and is not changed by
operator Reorder. When a reordered entry leaves, refill still uses that entry's original
source owner.

Refill must never produce a concurrent Cleaning Job. See SEQ-005 in
[`REQUIREMENTS.md`](REQUIREMENTS.md).

## 7. Operator queue actions

| Action | Effect | Limits |
| --- | --- | --- |
| Hold | Entry stays in GlobalQueue; the dispatcher temporarily skips it | Other entries preserve relative FIFO order; a hold does not block the whole queue |
| Release Hold | Entry becomes dispatchable again at its current position | — |
| Reject | Entry is removed from the current GlobalQueue, and the sensor is suppressed from refilling during the current Auto Sequence | Suppression ends when a new Auto Sequence is created, unless released earlier |
| Release Reject | Ends the suppression early | Requires the action's permission |
| Reorder | Changes GlobalQueue dispatch order | Cannot move an active Cleaning Job; does not bypass eligibility revalidation; does not alter source ownership |

All five actions are `[APPROVED]` behaviours. Their required permissions are `[OPEN]`.

Every action must produce an Event record containing:

- Timestamp
- User
- Action
- Sensor
- Reason
- Original position
- New position (when applicable)
- Auto Sequence ID
- Queue snapshot reference

### 7.1 Queue entry dispositions

Queue entry dispositions describe what happened to an entry in the queue. They are distinct
from Cleaning Job outcomes, which describe the result of an executed job. See
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) section 4.

| Disposition | Meaning |
| --- | --- |
| `HELD` | Operator held the entry; dispatcher skips it |
| `RELEASED` | A previous Hold or Reject suppression was released |
| `REJECTED` | Operator removed the entry and suppressed the sensor for the current Auto Sequence |
| `REORDERED` | Operator changed dispatch position; ownership unchanged |
| `REMOVED_BY_ELIGIBILITY` | The entry no longer satisfies eligibility (for example, removal dwell completed, configuration changed, or the sensor became disabled) |
| `REMOVED_BY_EQUIPMENT_EXCLUSION` | The entry was removed because its equipment, for example its Isolation Valve, was excluded |

`REJECTED` is **not** a Cleaning Job outcome. A job that is never executed because its entry
was rejected produces no job outcome at all.

## 8. Sequential execution interaction

Only one Cleaning Job may be ACTIVE at a time `[OWNER CONFIRMED]`. The queue subsystem must
enforce this through the following behaviours.

| Event | Required queue behaviour |
| --- | --- |
| New Auto Sequence starts | GlobalQueue is rebuilt from current data; the previous Queue snapshot is preserved in Event history |
| Cleaning Job completes successfully | Recalculate source queues; the sensor's `LastSuccessfulCleaningCompletedAt` is updated, so it becomes ineligible until its interval elapses again |
| Cleaning Job fails or requires recovery | The next Job is blocked; queue state is preserved for Operator inspection |
| Valve becomes `OUT_OF_SERVICE` | Every sensor associated with that valve is excluded from TempQueue, TimeQueue, GlobalQueue, and candidate refill |
| Valve returns to service | Associated sensors re-enter normal source queue evaluation; they must not be inserted into the middle of GlobalQueue |
| Valve fault resolved and continuation authorized | The AutoSequence may **later** select a sequential Cleaning Job assigned to another available Water Jet. Only one Cleaning Job executes at a time. "Allow unaffected Water Jets to continue" means sequential continuation, never concurrent operation |
| DirtyScore or TimeSinceLastClean changes | Existing GlobalQueue entries must **not** be reordered automatically |
| Operator Reorder | Changes dispatch position only; must not enable or create a second active Cleaning Job |
| Operator stop of the AutoSequence | See section 9 |
| Countdown paused by an idle valve fault | Queue is preserved while the modal is presented |

## 9. AutoSequence stop and restart

This behaviour is `[OWNER CONFIRMED]` and is no longer an open item.

### 9.1 When the Operator stops an AutoSequence

1. Stop dispatching new Cleaning Jobs.
2. Handle any active Cleaning Job according to the approved stop or recovery policy.
3. Record the current GlobalQueue snapshot in Event history.
4. Record Held, Rejected, and Reordered state in Event history.
5. Close the current AutoSequence instance.
6. Do **not** preserve the old GlobalQueue as the executable Queue for a future
   AutoSequence.

### 9.2 When a new AutoSequence starts

1. Read current process and configuration data.
2. Recalculate Dirty Scores.
3. Recalculate `TimeSinceLastClean`.
4. Re-evaluate `HardMinimumCleaningInterval`.
5. Rebuild all TempQueues.
6. Rebuild all TimeQueues.
7. Seed a new GlobalQueue.
8. Apply deduplication and refill.
9. Do **not** reload the previous executable GlobalQueue.
10. Preserve the previous Queue only as history and evidence.

The previous Queue is therefore evidence, not state. It must never be re-adopted as the
running queue.

## 10. Determinism requirements

1. Given identical inputs, configuration, and sequence state, queue contents and ordering
   must be identical regardless of processing order or platform.
2. Ties are always broken by `scanOrder` within the wall being evaluated. There is no "last
   in wins" behaviour.
3. `scanOrder` is stable persisted configuration and must not depend on current UI sorting.
4. Queue evaluation runs on the approved 1 second interval; a delayed or missed cycle must
   not change ordering semantics.

## 11. Queue snapshot

The Queue snapshot referenced by operator actions and sequence events must capture, at a
minimum: Auto Sequence ID, entry positions, sensor identifiers, source owners, source reason
flags (`TEMP_QUEUE` / `TIME_QUEUE`), hold and reject flags, and the capture timestamp. The
snapshot is stored in Event history.

Snapshot storage format, compression, and retention are `[OPEN]`.

## 11.1 Parameter source and evaluation boundaries (Stage 0.2)

Queue evaluation is a **per-cycle** operation at the approved one-second cadence. The following
boundaries apply.

1. **SQL Server is not the per-cycle queue parameter source.** Queue evaluation reads an
   **immutable in-memory Published Configuration Snapshot** loaded and validated by the
   Equipment Runtime at startup (see [`ARCHITECTURE.md`](ARCHITECTURE.md) section 30 and
   [`ADR-0011`](decisions/ADR-0011-configuration-and-secrets.md)).
2. **A Draft is never consumed by the runtime.** Only a Published revision is loaded, and a
   validated revision is swapped in atomically under the approved state gate — never while a
   Cleaning Job is active.
3. **Queue state is owned by the Runtime, not the UI.** The Operations UI observes queue state
   through the live-state contract and issues permitted operator queue actions through the
   approved command path. The UI must not own or recompute membership, order, dwell, or
   disposition (see [`ARCHITECTURE.md`](ARCHITECTURE.md) sections 25 and 28).
4. **Queue evaluation must not be blocked by database writes.** A slow Historian write or an
   alarm write must not delay a queue evaluation cycle.
5. **Sequential execution invariants are unchanged.** Sections 1, 8, and 10 continue to govern;
   nothing here permits more than one active Cleaning Job, parallel Water Jet Cleaning, or a
   shared Isolation Valve.
6. **The Poll Plan and the queue plan are compiled from the Published configuration**, not
   rebuilt per cycle (see [`ARCHITECTURE.md`](ARCHITECTURE.md) sections 29 and 30).

## 12. Open items

| Item | Status |
| --- | --- |
| Permission required for each operator queue action | `[OPEN]` |
| Reason text requirements per action | `[OPEN]` |
| Queue snapshot retention inside Event history | `[OPEN]` |
| Whether a sensor may be both held and rejected simultaneously | `[OPEN]` |
| Whether a rejected entry's disposition is recorded per-entry or per-sequence | `[OPEN]` |

Resolved by Owner confirmation and therefore **not** open: Water Jet to Isolation Valve
cardinality; Cleaning Job concurrency; whether more than one Cleaning Job may run;
GlobalQueue handling after Stop AutoSequence; whether the previous GlobalQueue is reloaded
on a new AutoSequence; and the cross-wall DirtyScore tie-break question.

---

## Related documents

- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities, dispositions, and enumerations
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — QUE, SEQ, and WJV requirements
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — dispatch, job execution, and sequencing
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking conditions and cleared-state acknowledgement
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — operator authority and the DCS override
- [`decisions/ADR-0003-queue-arbitration.md`](decisions/ADR-0003-queue-arbitration.md)
