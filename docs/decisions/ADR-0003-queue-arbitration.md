# ADR-0003 — Queue Arbitration

- **Status:** ACCEPTED
- **Date:** 2026-10-04
- **Scope:** How cleaning candidates are ranked, how the dispatch queue is seeded, deduplicated,
  refilled, and modified by the Operator.
- **Authority:** Approved Stage 0.1 Scope Gate (Repository Documentation Foundation).

---

## Context

Twenty-four to twenty-eight sensors exist per wall, 104 in total, and each cleaning job costs
significant time and water. Two competing concerns drive selection:

- **Cleanliness** — a sensor with a high DirtyScore should be cleaned sooner.
- **Time** — a sensor that has not been cleaned for a long time should not be starved.

The physical cleaning sequence is fixed by the machine layout: scanning begins at the upper
row and proceeds downward, with the wall order Left, Rear, Right, Front within each row.
The dispatch order, however, is a decision the system makes, and the Operator can and must
be able to intervene.

Three failure modes must be prevented by design:

1. **Thrashing** — a sensor hovering at a threshold entering and leaving the queue, causing
   unstable dispatch.
2. **Starvation** — a sensor with a moderate DirtyScore never being selected because a few
   sensors are always dirtier.
3. **Non-determinism** — the same plant state producing different dispatch orders on
   different runs, which makes incidents unreproducible and testing impossible.

## Decision

1. **Two source queue families, one per wall each.** Each wall has exactly one TempQueue and
   one TimeQueue.
   - TempQueue ordering: highest DirtyScore first, then stable `scanOrder`.
   - TimeQueue ordering: longest `TimeSinceLastClean` first, then stable `scanOrder`.
2. **Dwell-based TempQueue membership.** Entry requires the criteria to hold continuously for
   `TempQueueEntryDwellSeconds`; removal requires failure continuously for
   `TempQueueRemovalDwellSeconds`. Default for both is 10 seconds; they are independent and
   configurable. Loss of the condition during the entry dwell resets the dwell; re-dirtying
   during the removal dwell keeps the sensor queued.
3. **One GlobalQueue per Boiler Unit with a target capacity of eight unique sensors.**
4. **Fixed seed order.** Temp Left, Temp Rear, Temp Right, Temp Front, Time Left, Time Rear,
   Time Right, Time Front.
5. **Deduplication rule.** A sensor appears at most once. The earliest current position is
   preserved, all source reasons are merged onto that entry, and the duplicate source
   proposes its next eligible candidate. Seeding continues until eight unique sensors are
   present or no eligible candidates remain.
6. **FIFO after seeding.** Changes to DirtyScore or TimeSinceLastClean must not reorder
   existing entries automatically.
7. **FIFO refill with source ownership.** When an entry leaves, remaining entries shift
   forward preserving relative order, and the tail is refilled by the source owner of the
   departing entry. Operator Reorder changes dispatch order but never source ownership.
8. **Rebuild on new Auto Sequence.** GlobalQueue is rebuilt from current data when a new Auto
   Sequence starts, and the previous Queue snapshot is preserved in Event history.
9. **Operator actions:** Hold, Release Hold, Reject, Release Reject, and Reorder. Hold leaves
   the entry in place while the dispatcher skips it, without blocking the queue. Reject
   removes the entry and suppresses the sensor from refill for the current Auto Sequence.
   Every action produces a full Event record.
10. **Prohibitions.** Never duplicate a sensor, never bypass `HardMinimumCleaningInterval`,
    never use a disabled or inhibited sensor, never create a dummy job.
11. **HardMinimumCleaningInterval applies to both TempQueue and TimeQueue.**
12. **No null baseline.** `LastSuccessfulCleaningCompletedAt` is never null and there is no
    `NEVER_CLEANED` state; every sensor receives an explicit initial timestamp during
    configuration.

## Consequences

- Dispatch is reproducible: identical inputs and configuration yield identical ordering,
  because every ordering rule ends in a stable `scanOrder` tie-break.
- The queue is testable as pure logic, without hardware, because ordering depends on data
  values and persisted configuration only.
- An underfull GlobalQueue is a normal, expected state when few sensors are eligible. It is
  never corrected by relaxing a rule.
- Operator intent is preserved across position changes, because source ownership is
  immutable.
- The TempQueue dwell settings add a deliberate small delay before a dirty sensor becomes
  eligible. This is accepted as the price of avoiding thrashing.
- Because existing entries do not re-sort on value changes, a sensor that becomes much
  dirtier while queued does not jump forward until it is dispatched or the sequence restarts.
  This is accepted as the price of deterministic FIFO behaviour.
- `scanOrder` becomes load-bearing configuration: changing it changes queue tie-breaks, so
  it must be treated as stable persisted data.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Single combined priority formula over score and time | Opaque, hard to test, and hides starvation behaviour |
| Fully dynamic re-sorting of GlobalQueue on every cycle | Non-deterministic in practice; would reorder entries while an Operator watches, destroying trust |
| First-in-first-out without source queues | Removes the ability to balance cleanliness against time per wall |
| Deduplication by dropping the later source entirely | Wasteful; the later source has other eligible candidates, and the merged reason flags carry useful diagnosis |
| Refill from the global pool rather than the departing entry's source owner | Breaks the balancing property that the source seed order was designed to provide |
| A wait-for-operator model instead of automatic refill | Leaves the queue underfull and idles equipment while an Operator is present but busy |

## Verification status

- `[APPROVED]`: all rules stated in the Decision section.
- `[NOT VERIFIED]`: the per-sensor `HardMinimumCleaningInterval` values, `DiffLowerBound`,
  `DiffUpperBound`, and `DirtyScoreThreshold` values. These are commissioning values.
- `[OPEN]`: behaviour on Operator stop of the Auto Sequence (discard versus retain entries),
  the permission required per operator action, and the global tie-break question noted in
  [`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) section 11.
- No queue logic has been implemented or executed. Nothing in this decision has been tested.

## References

- [`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) — full queue specification
- [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) — DirtyScore and TimeSinceLastClean
- [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) — dispatch and job execution
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — QUE and SCN requirements
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned test coverage for queue logic
