# Queue Model — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED]. Queue behaviour described here is deterministic by
construction and must be implemented exactly. Deviations require an approved change, not a
local decision.

Identifiers used in worked examples are **illustrative placeholders only**. Real sensor
identifier formats and production mappings are not documented in this repository.

---

## 1. Overview and invariants

Six source queues and one dispatch queue exist per Boiler Unit:

| Queue | Cardinality | Orders by |
| --- | --- | --- |
| TempQueue | one per wall (Left, Rear, Right, Front) | DirtyScore descending, then scanOrder ascending |
| TimeQueue | one per wall (Left, Rear, Right, Front) | TimeSinceLastClean descending, then scanOrder ascending |
| GlobalQueue | one per Boiler Unit | FIFO after seeding and deduplication; target capacity eight |

Invariants, which must hold after every evaluation cycle `[APPROVED]`:

1. A sensor appears at most once in GlobalQueue.
2. GlobalQueue never contains a disabled, inhibited, invalid, or `OUT_OF_SERVICE` sensor.
3. No entry enters GlobalQueue if its HardMinimumCleaningInterval is unsatisfied.
4. No dummy or placeholder job is ever created to fill a position.
5. GlobalQueue holds at most eight entries and fewer than eight only when fewer than eight
   unique eligible sensors exist.
6. Ordering within each source queue is fully determined by data values plus stable
   scanOrder. No ordering depends on iteration order, arrival time, or UI state.

## 2. Source queue sources

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

This order is used for initial seeding only. After seeding and deduplication, GlobalQueue is
FIFO and is not re-sorted by source or by value.

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
2. Add **all** source reasons to the preserved entry, so the entry records both
   `TEMP_QUEUE` and `TIME_QUEUE` as applicable.
3. The later duplicate source must propose its **next eligible candidate** for the position
   it was filling.
4. Continue until eight unique sensors are present or no eligible candidates remain.

### 5.3 Worked example (illustrative identifiers)

```text
Seed order and proposals:
  pos 1  Temp Left  proposes  S_LEFT_01
  pos 2  Temp Rear  proposes  S_REAR_03
  pos 3  Temp Right proposes  S_RIGHT_02
  pos 4  Temp Front proposes  S_FRONT_04
  pos 5  Time Left  proposes  S_LEFT_05
  pos 6  Time Rear  proposes  S_REAR_03   <- duplicate of pos 2

Resolution of the duplicate at pos 6:
  step 1  S_REAR_03 stays at pos 2. It is not moved and not duplicated.
  step 2  The entry at pos 2 gains the TIME_QUEUE reason, so it now records both
          TEMP_QUEUE (from Temp Rear) and TIME_QUEUE (from Time Rear).
  step 3  Time Rear, whose proposal was rejected as a duplicate, proposes its next
          eligible candidate for the position it was filling.
  pos 6   Time Rear proposes S_REAR_07 (next eligible candidate, unique) -> accepted.

Result:
  pos 1  S_LEFT_01   [TEMP_QUEUE]
  pos 2  S_REAR_03   [TEMP_QUEUE, TIME_QUEUE]
  pos 3  S_RIGHT_02  [TEMP_QUEUE]
  pos 4  S_FRONT_04  [TEMP_QUEUE]
  pos 5  S_LEFT_05   [TIME_QUEUE]
  pos 6  S_REAR_07   [TIME_QUEUE]
  pos 7  Time Right  (proposal depends on its own eligibility; nothing is invented)
  pos 8  Time Front  (proposal depends on its own eligibility; nothing is invented)
```

Note: a source reason is recorded per preserved entry, so the entry at pos 2 carries two
reasons while each other entry carries one. The reason flags are informational and
diagnostic; they do not affect ordering, which remains FIFO after seeding.

The approved example in the Stage 0.1 Scope Gate used a sensor named `G1` proposed by Time
Left at position 2 and by Temp Left at position 6; the outcome was that `G1` stayed at
position 2, both `TIME_QUEUE` and `TEMP_QUEUE` were recorded as reasons, and Temp Left
proposed its next eligible candidate for position 6. The rule stated in section 5.2 is the
general form of that example.

### 5.4 Underfull queue

GlobalQueue may contain fewer than eight entries **only** when fewer than eight unique
eligible sensors are available. Positions are never filled by weakening a rule. The system
must never duplicate a sensor, bypass HardMinimumCleaningInterval, use a disabled or
inhibited sensor, or create a dummy job.

## 6. FIFO refill

When an entry leaves GlobalQueue (dispatched, rejected, invalidated, or excluded):

1. Remaining entries shift forward, preserving FIFO relative order.
2. The tail is refilled from the **source owner** of the entry that left.
3. The source must offer its next eligible unique sensor.
4. If no eligible candidate exists, the tail position remains empty.

Source ownership is fixed when an entry is seeded and is not changed by operator Reorder.
When a reordered entry leaves, refill still uses that entry's original source owner.

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

## 8. Interaction with sequences and equipment state

| Event | Required queue behaviour |
| --- | --- |
| New Auto Sequence starts | GlobalQueue is rebuilt from current data; the previous Queue snapshot is preserved in Event history |
| Cleaning Job completes successfully | Recalculate source queues; the sensor's `LastSuccessfulCleaningCompletedAt` is updated, so it becomes ineligible until its interval elapses again |
| Cleaning Job fails or requires recovery | The next Job is blocked; queue state is preserved for Operator inspection |
| Valve becomes `OUT_OF_SERVICE` | Every sensor associated with that valve is excluded from TempQueue, TimeQueue, GlobalQueue, and candidate refill |
| Valve returns to service | Associated sensors re-enter normal source queue evaluation; they must not be inserted into the middle of GlobalQueue |
| DirtyScore or TimeSinceLastClean changes | Existing GlobalQueue entries must **not** be reordered automatically |
| Operator stops the Auto Sequence | GlobalQueue handling is `[OPEN]` — whether entries are discarded or retained for the next sequence must be decided by the Owner |
| Countdown paused by an idle valve fault | Queue is preserved while the modal is presented |

## 9. Determinism requirements

1. Given identical inputs, configuration, and sequence state, queue contents and ordering
   must be identical regardless of processing order or platform.
2. Ties are always broken by `scanOrder`. There is no "last in wins" behaviour.
3. `scanOrder` is stable persisted configuration and must not depend on current UI sorting.
4. Queue evaluation runs on the approved 1 second interval; a delayed or missed cycle must
   not change ordering semantics.

## 10. Queue snapshot

The Queue snapshot referenced by operator actions and sequence events must capture, at
minimum: Auto Sequence ID, entry positions, sensor identifiers, source owners, source
reason flags (`TEMP_QUEUE` / `TIME_QUEUE`), hold and reject flags, and the capture
timestamp. The snapshot is stored in Event history.

Snapshot storage format, compression, and retention are `[OPEN]`.

## 11. Open items

| Item | Status |
| --- | --- |
| GlobalQueue contents after an Operator stop of the Auto Sequence | `[OPEN]` |
| Permission required for each operator queue action | `[OPEN]` |
| Reason text requirements per action | `[OPEN]` |
| Queue snapshot retention inside Event history | `[OPEN]` |
| Behaviour when several walls are at equal DirtyScore for the same scanOrder tie-break | `[OPEN]` — a global tie-break beyond wall order may be required |
| Whether a sensor may be both held and rejected simultaneously | `[OPEN]` |

---

## Related documents

- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — entities and enumerations
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — QUE requirements
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — dispatch and job execution
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking conditions
- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — operator authority
- [`decisions/ADR-0003-queue-arbitration.md`](decisions/ADR-0003-queue-arbitration.md)
