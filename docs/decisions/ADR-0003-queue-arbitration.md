# ADR-0003 — Queue Arbitration and Sequencing

- **Status:** ACCEPTED — **SUPERSEDED IN PART by Owner decision dated 2026-10-06** (see the
  supersession note below)
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-confirmed review punchlist)
- **Scope:** How cleaning candidates are ranked, how the dispatch queue is seeded,
  deduplicated, owned, refilled, stopped, rebuilt, and modified by the Operator, and how
  sequential execution is enforced.
- **Authority:** Approved Stage 0.1 Scope Gate, as corrected by the Owner-confirmed Stage
  0.1 documentation review punchlist.

> **SUPERSEDED IN PART by Owner decision dated 2026-10-06.** The following parts of this ADR
> no longer apply:
>
> - the queue-level `HELD` disposition and the Hold / Release Hold Operator actions;
> - "skip held entries" dispatch (the dispatcher now dispatches GlobalQueue Position 1 only;
>   no scan-forward);
> - any `BLOCKED` or `EXCLUDED` queue entry state (the GlobalQueue holds ready-to-dispatch
>   entries only; a Sensor that cannot be dispatched is not in the queue).
>
> Pause belongs to the **AutoSequence** or the **Cleaning Job lifecycle**, never to a queue
> entry. Production Pause / Resume semantics and authority are **OWNER DECISION REQUIRED**, as
> are Reject / Release Reject / Reorder semantics in the ready-only, head-only queue. The
> remaining decisions (ranking, seeding, deduplication, ownership, refill, stop and rebuild,
> sequential execution) stand. The text below is kept unchanged as the decision history; see
> [`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) §7.2 and
> [`../spikes/queue-eligibility-decision-matrix.md`](../spikes/queue-eligibility-decision-matrix.md).

---

## Context

Twenty-four to twenty-nine sensors exist per wall, 106 in total, and each cleaning job costs
significant time and water. Two competing concerns drive selection:

- **Cleanliness** — a sensor with a high DirtyScore should be cleaned sooner.
- **Time** — a sensor that has not been cleaned for a long time should not be starved.

The physical cleaning sequence is fixed by the machine layout: scanning begins at the upper
row and proceeds downward, with the wall order Left, Rear, Right, Front within each row.
The dispatch order, however, is a decision the system makes, and the Operator can and must
be able to intervene.

Four failure modes must be prevented by design:

1. **Thrashing** — a sensor hovering at a threshold entering and leaving the queue, causing
   unstable dispatch.
2. **Starvation** — a sensor with a moderate DirtyScore never being selected because a few
   sensors are always dirtier.
3. **Non-determinism** — the same plant state producing different dispatch orders on
   different runs, which makes incidents unreproducible and testing impossible.
4. **Concurrency** — two Cleaning Jobs executing at once, which the plant is not authorised
   to do and which no queue action may cause.

## Decision

### Queue structure

1. **Eight source queues, one GlobalQueue.** Each wall has exactly one TempQueue and one
   TimeQueue: four TempQueues and four TimeQueues, giving **eight source queues in total**.
   There is one GlobalQueue per Boiler Unit with a target capacity of eight unique Sensor
   entries.
   - TempQueue ordering: highest DirtyScore first, then stable `scanOrder`, evaluated
     **within each wall**.
   - TimeQueue ordering: longest `TimeSinceLastClean` first, then stable `scanOrder`.
2. **Fixed seed order:** Temp Left, Temp Rear, Temp Right, Temp Front, Time Left, Time Rear,
   Time Right, Time Front. GlobalQueue initial order is determined by this fixed source
   order, **not** by a cross-wall DirtyScore comparison. TempQueue sorting occurs within
   each wall only.
3. **Dwell-based TempQueue membership.** Entry requires the criteria to hold continuously
   for `TempQueueEntryDwellSeconds`; removal requires failure continuously for
   `TempQueueRemovalDwellSeconds`. Default for both is 10 seconds; they are independent and
   configurable.

### Deduplication and ownership

4. **Deduplication.** A Sensor appears at most once. The occurrence at the earliest current
   GlobalQueue position wins; all applicable source reasons are recorded on the preserved
   entry; the later duplicate source advances to its next eligible candidate. Seeding
   continues until eight unique entries are present or no eligible unique candidates remain.
5. **Source ownership.** Each entry has exactly one source owner: the source that
   established the preserved earliest GlobalQueue position. Merged reason flags do not
   transfer or change ownership.
6. **FIFO after seeding.** Changes to DirtyScore or TimeSinceLastClean must not reorder
   existing entries automatically.
7. **FIFO refill from the owner.** When an entry leaves, remaining entries shift forward
   preserving relative order, and the tail is refilled by the source owner of the departing
   entry. Operator Reorder changes dispatch position but never source ownership.

Worked example, which is binding:

- Position 2 is owned by `TIME_LEFT` and contains Sensor `G1`.
- Position 6 from `TEMP_LEFT` also proposes `G1`.
- Preserve `G1` at Position 2; record both `TIME_QUEUE` and `TEMP_QUEUE` as reasons on the
  preserved entry.
- `TEMP_LEFT` advances to its next eligible candidate for Position 6.
- When `G1` later leaves GlobalQueue, refill uses `TIME_LEFT`, because `TIME_LEFT` owns the
  preserved entry.

### Operator actions

8. **Operator actions** *(Hold / Release Hold superseded 2026-10-06 — see the note above)*: Hold, Release Hold, Reject, Release Reject, and Reorder. Hold leaves
   the entry in place while the dispatcher skips it, without blocking the queue. Reject
   removes the entry and suppresses the sensor from refill for the current Auto Sequence.
   Every action produces a full Event record.
9. **Queue entry dispositions** are distinct from Cleaning Job outcomes. `REJECTED` is a
   queue disposition and an Operator action, never a Cleaning Job outcome, because a
   rejected entry is never executed.

### Stop and restart

10. **Operator stop of an AutoSequence:** stop dispatching new Cleaning Jobs; handle any
    active Cleaning Job according to the approved stop or recovery policy; record the current
    GlobalQueue snapshot and the Held, Rejected, and Reordered state in Event history; close
    the AutoSequence instance; and do not preserve the old GlobalQueue as the executable
    Queue for a future AutoSequence.
11. **New AutoSequence:** read current process and configuration data; recalculate Dirty
    Scores; recalculate `TimeSinceLastClean`; re-evaluate `HardMinimumCleaningInterval`;
    rebuild all TempQueues; rebuild all TimeQueues; seed a new GlobalQueue; apply
    deduplication and refill; do not reload the previous executable GlobalQueue; and preserve
    the previous Queue only as history and evidence.

### Sequencing

12. **Strictly sequential execution.** AutoSequence executes Cleaning Jobs strictly
    sequentially:
    - INVARIANT-SEQ-001 — at most one Cleaning Job may be ACTIVE within one installation at
      any time.
    - INVARIANT-SEQ-002 — a second Cleaning Job must not enter an executing state until the
      current Cleaning Job has reached an approved safe and released terminal condition.
    - INVARIANT-SEQ-003 — different Water Jets, Isolation Valves, boiler walls, or Galil
      controllers do not grant authority for concurrent Cleaning Jobs.
    - INVARIANT-SEQ-004 — the selected eligible head of GlobalQueue is the only normal
      source for the next Cleaning Job.
    - INVARIANT-SEQ-005 — queue refill, score changes, Operator Reorder, Hold, Reject, valve
      exclusion, or equipment availability must never result in concurrent Cleaning Jobs.
    - INVARIANT-SEQ-006 — parallel Water Jet cleaning is prohibited.
13. **Main Pump between jobs.** The Main Pump may remain running between Cleaning Jobs
    during an active AutoSequence, but only one Water Jet may execute a Cleaning Job at a
    time.
14. **"Allow unaffected Water Jets to continue"** means sequential continuation only: after
    the valve fault workflow is resolved and continuation is authorized, the AutoSequence
    may later select a sequential Cleaning Job assigned to another available Water Jet. It
    must never imply concurrent operation.

### Prohibitions and baseline

15. **Prohibitions.** Never duplicate a sensor, never bypass `HardMinimumCleaningInterval`,
    never use a disabled or inhibited sensor, never create a dummy job, and never allow two
    Cleaning Jobs to execute concurrently.
16. **HardMinimumCleaningInterval applies to both TempQueue and TimeQueue.**
17. **No null baseline.** `LastSuccessfulCleaningCompletedAt` is never null and there is no
    `NEVER_CLEANED` state; every sensor receives an explicit initial timestamp during
    configuration.

## Consequences

- Dispatch is reproducible: identical inputs and configuration yield identical ordering,
  because every ordering rule ends in a stable `scanOrder` tie-break within its wall.
- The queue is testable as pure logic, without hardware, because ordering depends on data
  values and persisted configuration only.
- An underfull GlobalQueue is a normal, expected state when few sensors are eligible. It is
  never corrected by relaxing a rule.
- Operator intent is preserved across position changes, because source ownership is
  immutable.
- A stopped AutoSequence leaves no executable queue behind. Every restart is a fresh,
  evidence-backed rebuild, which prevents stale eligibility from leaking across sequences.
  The cost is that Held, Rejected, and Reordered state does not carry into a new
  AutoSequence; that state is preserved as history instead.
- Sequencing makes the safety argument simpler: only one motion path is ever commanded at a
  time, so the "which Water Jet is moving" question has exactly one answer.
- The TempQueue dwell settings add a deliberate small delay before a dirty sensor becomes
  eligible. This is accepted as the price of avoiding thrashing.
- Because existing entries do not re-sort on value changes, a sensor that becomes much
  dirtier while queued does not jump forward until it is dispatched or the sequence
  restarts. This is accepted as the price of deterministic FIFO behaviour.
- `scanOrder` becomes load-bearing configuration: changing it changes queue tie-breaks, so
  it must be treated as stable persisted data.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Single combined priority formula over score and time | Opaque, hard to test, and hides starvation behaviour |
| Fully dynamic re-sorting of GlobalQueue on every cycle | Non-deterministic in practice; would reorder entries while an Operator watches, destroying trust |
| First-in-first-out without source queues | Removes the ability to balance cleanliness against time per wall |
| Deduplication by dropping the later source entirely | Wasteful; the later source has other eligible candidates, and the merged reason flags carry useful diagnosis |
| Letting merged reasons transfer ownership to the last source that proposed the sensor | Would change refill behaviour based on a diagnostic annotation, making refill untraceable |
| Refill from the global pool rather than the departing entry's source owner | Breaks the balancing property that the source seed order was designed to provide |
| A wait-for-operator model instead of automatic refill | Leaves the queue underfull and idles equipment while an Operator is present but busy |
| Retaining the previous GlobalQueue as the executable queue across a stop and restart | Stale eligibility would dispatch jobs on evidence that no longer holds |
| Allowing parallel Cleaning Jobs on independent Water Jets | Prohibited. Different Water Jets, valves, walls, and controllers do not grant authority for concurrency |

## Verification status

- `[APPROVED]`: queue structure, seed order, dwell rules, deduplication, FIFO refill, and the
  prohibition set.
- `[OWNER CONFIRMED]`: source ownership rules, the stop and rebuild policy, and all six
  sequencing invariants.
- `[NOT VERIFIED]`: the per-sensor `HardMinimumCleaningInterval` values, `DiffLowerBound`,
  `DiffUpperBound`, and `DirtyScoreThreshold` values. These are commissioning values.
- `[OPEN]`: the permission required per operator action, reason text requirements, queue
  snapshot retention, and whether a sensor may be both held and rejected simultaneously
  (*this question no longer applies: queue-level Hold is superseded, 2026-10-06*).
- No queue or sequencing logic has been implemented or executed. Nothing in this decision
  has been tested.

## References

- [`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) — full queue specification
- [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) — sequencing invariants and job execution
- [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) — DirtyScore, TimeSinceLastClean, dispositions
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — QUE, SEQ, WJV, and SCN requirements
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned sequencing and deduplication coverage
