# Queue Eligibility Decision Matrix — PROPOSAL (Stage 0.2.1A spike evidence)

**Document status:** PROPOSAL ONLY. **Every row is `OWNER DECISION REQUIRED`. Nothing in this
document is approved.** It does not authorize any implementation. The synthetic spike runtime
implements **none** of these conditions: it admits Sensors to the bounded synthetic GlobalQueue
only from scenario-prepared synthetic sources, and refuses only Water Jet slots, duplicates, the
Active Job target, and a full queue (capacity 8). A production GlobalQueue runtime, including its
eligibility policy, is a **Main Development** slice and is not authorized here.

Related: [`stage-0.2.1a-results.md` §0D](stage-0.2.1a-results.md#0d-globalqueue-semantics-correction-and-head-only-dispatch-stage-021a) ·
[`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) ·
[`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) ·
[`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) ·
[spike contract](../../spikes/ui-runtime-react/contracts/CONTRACTS.md)

---

## 1. Fixed semantics (Owner domain correction — not proposals)

- The GlobalQueue holds **ready-to-dispatch entries only**; presence in the queue means READY.
- There are no queue entry states or synonyms such as `BLOCKED`, `HELD`, `HELD_BY_OPERATOR`,
  `WAITING_FOR_PUMP`, `WAITING_FOR_EQUIPMENT`, `EXCLUDED`, `INVALID`, `OUT_OF_SERVICE`, `DISABLED`,
  `BAD`, or `STALE`. A Sensor that is not dispatchable is **not in the queue**.
- Operator pause, and pump / Water Jet / valve / pressure / pre-check waits, belong to the
  **Active Job or the AutoSequence**, never to queue entries.
- At most 8 unique entries; FIFO; only Position 1 is a dispatch candidate (no scan-forward);
  dispatch atomically removes the head, creates exactly one Job for that Sensor, and records the
  dispatch; at most one Active Job.

The rows below therefore only answer **whether a Sensor may enter the queue, and when an existing
entry is removed**. They never introduce an entry state.

## 2. Column meanings

| Column | Meaning |
| --- | --- |
| Proposed entry eligibility | Whether the condition prevents a Sensor from **entering** the GlobalQueue |
| Removal behaviour | What happens to an **existing** entry when the condition appears (remove before dispatch, or keep) |
| Owning layer | Where the decision would be evaluated (source queue evaluation, GlobalQueue admission, AutoSequence / Job pre-check, Operator action) |
| Audit evidence | What a production implementation would have to record |
| Existing reference | Related text in the domain documents (for the Owner's comparison; not an approval of this row) |
| Status | Always `OWNER DECISION REQUIRED` in this document |

## 3. Matrix

### A. Data quality

| Condition | Proposed entry eligibility | Removal behaviour | Owning layer | Audit evidence | Existing reference | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `GOOD` | Eligible | Keep | Source queue evaluation | none beyond the entry record | — | OWNER DECISION REQUIRED |
| `UNCERTAIN` | Proposal: eligible only with a validated last classification (`LAST_VALIDATED`) | Keep; re-evaluate on the next cycle | Source queue evaluation | quality and classification basis at admission | — | OWNER DECISION REQUIRED |
| `BAD` | Proposal: not eligible | Proposal: remove before dispatch (`REMOVED_BY_ELIGIBILITY`) | Source queue evaluation | quality value, time, removal disposition | QUEUE_MODEL §7.1 (`REMOVED_BY_ELIGIBILITY`) | OWNER DECISION REQUIRED |
| `STALE` | Proposal: not eligible | Proposal: remove after a removal dwell, not instantly | Source queue evaluation | last-good timestamp, dwell start / end | QUEUE_MODEL §3.4 (removal dwell) | OWNER DECISION REQUIRED |
| `DISABLED` | Not eligible | Remove (`REMOVED_BY_ELIGIBILITY`) | Configuration / source queue evaluation | configuration revision that disabled it | QUEUE_MODEL §1 invariant 2 | OWNER DECISION REQUIRED |
| `OUT_OF_SERVICE` | Not eligible | Remove (`REMOVED_BY_EQUIPMENT_EXCLUSION` or `REMOVED_BY_ELIGIBILITY`) | Configuration / source queue evaluation | who / when / why | QUEUE_MODEL §1 invariant 2, §8 | OWNER DECISION REQUIRED |

### B. Alarms

| Condition | Proposed entry eligibility | Removal behaviour | Owning layer | Audit evidence | Existing reference | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `ACTIVE_UNACK` on the Sensor | Proposal: depends on alarm category; cleaning-relevant categories make it not eligible | Proposal: remove before dispatch for cleaning-relevant categories only | GlobalQueue admission (alarm category lookup) | alarm ID, category, severity, decision | — | OWNER DECISION REQUIRED |
| `ACTIVE_ACK` | Proposal: same as `ACTIVE_UNACK` (acknowledgement does not clear the condition) | As above | GlobalQueue admission | alarm ID, ack user / time | — | OWNER DECISION REQUIRED |
| `CLEARED_UNACK` | Proposal: eligible (condition cleared; acknowledgement is an alarm-handling duty, not a cleaning gate) | Keep | Alarm subsystem only | alarm ID, clear time | — | OWNER DECISION REQUIRED |
| `NONE` | Eligible | Keep | — | — | — | OWNER DECISION REQUIRED |
| Severity (`LOW` / `MEDIUM` / `HIGH`) | Proposal: severity alone does not decide eligibility; category decides | — | Alarm configuration | category-to-eligibility table revision | — | OWNER DECISION REQUIRED |
| Unrelated alarm categories (e.g. communication of another device) | Proposal: no effect on this Sensor's eligibility | Keep | Alarm configuration | — | — | OWNER DECISION REQUIRED |

### C. Intervals and dwell

| Condition | Proposed entry eligibility | Removal behaviour | Owning layer | Audit evidence | Existing reference | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `HardMinimumCleaningInterval` unsatisfied | Not eligible | n/a (cannot be queued) | Source queue evaluation | last successful clean time, interval parameter revision | QUEUE_MODEL §1 invariant 3 | OWNER DECISION REQUIRED |
| TempQueue threshold (DirtyScore) | Eligible for TempQueue above threshold | Removal only via removal dwell | TempQueue | score, threshold revision | QUEUE_MODEL §3 | OWNER DECISION REQUIRED |
| TimeQueue threshold (TimeSinceLastClean) | Eligible for TimeQueue above threshold | Removal only via removal dwell | TimeQueue | elapsed time, threshold revision | QUEUE_MODEL §4 | OWNER DECISION REQUIRED |
| Entry dwell | Condition must persist for the entry dwell before admission | — | Source queue | dwell start / end | QUEUE_MODEL §3.3 | OWNER DECISION REQUIRED |
| Removal dwell | — | Entry removed only after the condition is absent for the removal dwell | Source queue | dwell start / end, disposition | QUEUE_MODEL §3.4 | OWNER DECISION REQUIRED |

### D. Equipment

| Condition | Proposed entry eligibility | Removal behaviour | Owning layer | Audit evidence | Existing reference | Status |
| --- | --- | --- | --- | --- | --- | --- |
| No Water Jet assignment | Not eligible (no dispatch target) | Remove | Configuration / GlobalQueue admission | configuration revision | — | OWNER DECISION REQUIRED |
| No valve assignment | Not eligible | Remove | Configuration / GlobalQueue admission | configuration revision | — | OWNER DECISION REQUIRED |
| Valve / Water Jet excluded by the Operator | Not eligible | Remove (`REMOVED_BY_EQUIPMENT_EXCLUSION`) | GlobalQueue admission | exclusion user / time / reason | QUEUE_MODEL §7.1, §8 | OWNER DECISION REQUIRED |
| Equipment temporarily unavailable (pump not ready, pressure low, jet busy) | Proposal: **no effect on the queue** — the Active Job / AutoSequence waits in pre-check | Keep | Job pre-check / AutoSequence | wait start / end on the Job | CLEANING_SEQUENCE (job lifecycle) | OWNER DECISION REQUIRED |
| Equipment communication failure | Proposal: no effect on queue entry; the Job pre-check fails or waits | Keep | Job pre-check | device health at pre-check | — | OWNER DECISION REQUIRED |

### E. Sequence and history

| Condition | Proposed entry eligibility | Removal behaviour | Owning layer | Audit evidence | Existing reference | Status |
| --- | --- | --- | --- | --- | --- | --- |
| Already cleaned in the current AutoSequence | Proposal: not eligible until the next AutoSequence | — | GlobalQueue admission | AutoSequence ID, job ID | QUEUE_MODEL §9 | OWNER DECISION REQUIRED |
| Already the Active Job target | Not eligible (spike refuses this today as a structural rule, not as a policy) | Removed by dispatch | GlobalQueue admission | dispatch record | QUEUE_MODEL §8 | OWNER DECISION REQUIRED |
| Operator rejected in this AutoSequence | Proposal: not eligible until release or the next AutoSequence | Remove (`REJECTED`) | Operator action | user, reason, AutoSequence ID | QUEUE_MODEL §7.1 | OWNER DECISION REQUIRED |
| Duplicate proposals from several sources | Single entry; first source owner kept | — | GlobalQueue de-duplication | merged source list | QUEUE_MODEL §5.2–5.3 | OWNER DECISION REQUIRED |
| Operator request as a source | Proposal: eligible subject to the same hard rules (interval, disabled, out of service) | — | Operator action → GlobalQueue admission | user, time | QUEUE_MODEL §7 | OWNER DECISION REQUIRED |
| Previous job for this Sensor failed | Proposal: not eligible until the failure is resolved by the Operator | — | AutoSequence recovery | job ID, failure outcome, resolution | QUEUE_MODEL §8 ("next Job is blocked") | OWNER DECISION REQUIRED |

## 4. Conflicts with existing domain text (for the Owner)

These are recorded, not resolved. The domain documents were **not** edited by this checkpoint.

| Document | Text | Conflict with the Owner correction |
| --- | --- | --- |
| `docs/QUEUE_MODEL.md` §7.1 | `HELD` — "Operator held the entry; dispatcher skips it" | A held entry that the dispatcher skips is a scan-forward over a non-ready entry. Under the correction, pause belongs to the AutoSequence and the queue contains ready entries only |
| `docs/DOMAIN_MODEL.md` (`QueueEntryDisposition`, `[OWNER CONFIRMED]`) | includes `HELD` | Same as above. Dispositions are audit events ("what happened"), which may remain valid for `REJECTED`, `REORDERED`, `REMOVED_BY_*` |
| `docs/QUEUE_MODEL.md` §8 | "Cleaning Job fails or requires recovery — The next Job is blocked" | Compatible if "blocked" is read as an AutoSequence state, not a queue entry state |

## 5. What the spike shows instead

- Queue Eligibility Diagnostics (Diagnostics drawer) shows a Sensor as `NOT ADMITTED` only when a
  synthetic test scenario explicitly sets it (preset 6), with the reason "Reason pending
  Owner-approved eligibility policy". This is a synthetic demonstration, not an eligibility rule.
- No row in this matrix is approved; no row is implemented.
