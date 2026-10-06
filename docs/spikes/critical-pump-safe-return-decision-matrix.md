# Critical Main Pump and Mandatory Safe Return Decision Matrix — PROPOSAL (Stage 0.2.1A spike evidence)

**Document status:** PROPOSAL ONLY. **Every matrix row is `OWNER DECISION REQUIRED`. Nothing in
the matrices is approved.** It does not authorize any implementation. The rows record the policy
questions the Owner critical Pump decision left open. The synthetic spike behaviour listed in each row
is a **conservative synthetic choice for review only**, not a proposal for Production and not a
safety function.

**SYNTHETIC PROOF ONLY.** Nothing here is a physical Main Pump, protection relay, VFD, Isolation
Valve, axis, motion profile, Galil program or Production interlock. No safety, motion or
standards certification is claimed. No Production coordinates, addresses or protocol details are
used or implied. WebView2, kiosk and real hardware behaviour are NOT VERIFIED.

Related: [`stage-0.2.1a-results.md` §0E](stage-0.2.1a-results.md#0e-critical-main-pump-handling-and-mandatory-safe-return-stage-021a) ·
[`queue-eligibility-decision-matrix.md`](queue-eligibility-decision-matrix.md) ·
[`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) §7.2 ·
[`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) ·
[`../ALARM_MODEL.md`](../ALARM_MODEL.md) ·
[`../CONTROL_AUTHORITY.md`](../CONTROL_AUTHORITY.md) ·
[spike contract](../../spikes/ui-runtime-react/contracts/CONTRACTS.md)

---

## 1. Fixed semantics (Owner critical Pump decision — not proposals)

- The **Main Pump is a High Critical device**.
- Pump events fall into three classes:
  - **A — expected commanded stop** (normal stop): not a fault, no critical modal.
  - **B — unexpected stop** and **C — trip**: High Critical events.
- On B or C:
  - stop normal Cleaning progression, cleaning water output and dispatch;
  - no new Job and no next Job;
  - AutoSequence → `CRITICAL_SUSPENDED`;
  - an Active Job enters **Mandatory Safe Return**;
  - a large blocking modal appears immediately.
- The GlobalQueue stays unchanged (FIFO, no entry state). There is no automatic Resume and no
  automatic next Job.
- **Every** Cleaning Job, whatever the outcome, ends through Mandatory Safe Return:
  1. **SR1** stop normal Cleaning / water command;
  2. **SR2** command the Isolation Valve closed;
  3. **SR3** confirm closed;
  4. **SR4** command the axis to the Standby Position;
  5. **SR5** confirm Standby;
  6. **SR6** finalize the outcome;
  7. **SR7** release Active Job ownership;
  8. **SR8** only then may later sequencing be considered.
- The Isolation Valve close is commanded **and confirmed** before the axis return command.
- The Job remains the **Active Job** until Standby is confirmed (SR5) and released (SR7).
- After a fault the outcome is `ABORTED` (spike name), the AutoSequence stays suspended, and the
  queue is unchanged. Resume is future work.
- The modal:
  - has no close (X) button; Acknowledge is the only action, and it is not a clear or a Resume;
  - closes only when the condition is cleared **and** Safe Return is complete **and** the alarm
    is acknowledged;
  - leaves the AutoSequence suspended when it closes; there is no Resume button.
- Production enum and outcome names are **not approved**. The spike names are `RUNNING`,
  `ABORTING`, `SAFE_RETURN_*`, `COMPLETED` and `ABORTED`.

## 2. Matrices

Column meanings: **Situation** — what happens; **Spike behaviour** — the conservative synthetic
choice in the spike (review only); **Open question** — what the Owner must decide; **Status** —
always `OWNER DECISION REQUIRED`.

### A. Pump readiness and stop / trip by Cleaning phase

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Pump not ready at dispatch | No Job created; AutoSequence `PUMP_NOT_READY`; queue unchanged | Production readiness rule (pressure band, dwell, confirmation source) | OWNER DECISION REQUIRED |
| Expected commanded stop with an Active Job | Timed Safe Return, trigger `SYN_COMMANDED_PUMP_STOP`, outcome `ABORTED`, no modal, no suspension | Whether a commanded stop with an Active Job is permitted at all, and its outcome | OWNER DECISION REQUIRED |
| Stop / trip in P1 (pre-check) | Safe Return from SR1 | Whether the valve was ever opened, so whether SR2 / SR3 apply | OWNER DECISION REQUIRED |
| Stop / trip in P2 (valve opening) | Safe Return from SR1 | Valve partially open: close confirmation timing | OWNER DECISION REQUIRED |
| Stop / trip in P3 | Safe Return from SR1 | Phase-specific handling | OWNER DECISION REQUIRED |
| Stop / trip in P4 (jetting) | Water output stops at SR1; Safe Return | Water hammer / pressure decay handling; valve close timing vs Pump run-down | OWNER DECISION REQUIRED |
| Stop / trip in P5 | Safe Return from SR1 | Phase-specific handling | OWNER DECISION REQUIRED |
| Stop / trip in P6 | Safe Return from SR1; outcome `ABORTED` | Whether a Job in P6 counts as cleaned | OWNER DECISION REQUIRED |
| Stop / trip during Safe Return | Recorded as `CRITICAL_EVENT_DURING_SAFE_RETURN`; Safe Return continues; trigger unchanged | Whether a critical event during Safe Return changes the steps or the outcome | OWNER DECISION REQUIRED |
| Second critical event while one is active | Refused (no stacking) | Multiple-event handling | OWNER DECISION REQUIRED |

### B. Isolation Valve failures

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Valve closed feedback absent | After the synthetic timeout (20 s): `SAFE_RETURN_FAILED`; Active Job retained; no outcome; no release; no dispatch; modal stays | Timeout value, retries, Operator procedure, outcome name (`RECOVERY_REQUIRED`?) | OWNER DECISION REQUIRED |
| Valve closed feedback delayed | Axis waits; the axis is never commanded before confirmation | Maximum acceptable delay | OWNER DECISION REQUIRED |
| Contradictory valve feedback (open and closed) | Not modelled | Treatment | OWNER DECISION REQUIRED |
| Valve communication lost during Safe Return | Not modelled | Treatment | OWNER DECISION REQUIRED |
| Wrong valve reports closed | Not modelled | Feedback identity check | OWNER DECISION REQUIRED |

### C. Axis / Standby failures

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Standby feedback absent | After the synthetic timeout: `SAFE_RETURN_FAILED`; Active Job retained; no outcome; modal stays | Timeout, retries, Operator procedure | OWNER DECISION REQUIRED |
| Standby feedback delayed | Job stays Active until confirmed | Maximum acceptable delay | OWNER DECISION REQUIRED |
| Axis fault / limit / following error during return | Not modelled | Treatment; manual recovery authority | OWNER DECISION REQUIRED |
| Open-loop vs encoder-verified Standby confirmation | Not modelled (synthetic feedback only) | Confirmation source per `MotionFeedbackMode` | OWNER DECISION REQUIRED |

### D. Safe Return exceptions

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Safe Return failure policy | Active Job retained, no outcome, no dispatch, modal stays; synthetic test reset only | Failure state name, recovery path, who may end the Job | OWNER DECISION REQUIRED |
| Additional abort during Safe Return | Noted (`ADDITIONAL_TRIGGER_NOTED`); not restarted | Behaviour | OWNER DECISION REQUIRED |
| Runtime restart during Safe Return | Not modelled (in-memory synthetic) | Persistence and resume of Safe Return state | OWNER DECISION REQUIRED |
| UI disconnected during Safe Return | Runtime continues; UI shows the last state; acknowledge disabled | Production behaviour | OWNER DECISION REQUIRED |
| Step timing (min step 1.5 s, feedback 3 s, timeout 20 s) | Synthetic constants | Production timings | OWNER DECISION REQUIRED |

### E. Outcomes, re-queue and retry

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Outcome names | `COMPLETED` (cleaning phases complete + Safe Return) / `ABORTED` (any other trigger) | Production names vs `CleaningJobOutcome` (`COMPLETED`, `FAILED`, `ABORTED`, `RECOVERY_REQUIRED`) | OWNER DECISION REQUIRED |
| Re-queue of the aborted Sensor | No automatic re-queue; a still-Dirty Sensor may be re-admitted later by its source (not while suspended) | Re-queue position / priority / suppression | OWNER DECISION REQUIRED |
| Retry of the aborted Job | None | Retry count / authority | OWNER DECISION REQUIRED |
| Clean credit for a partially executed Job | None (only `COMPLETED` applies the synthetic clean effect) | Partial credit | OWNER DECISION REQUIRED |
| Evidence retention | Transient synthetic evidence (last outcome + bounded log of 20), not an audit record | Production audit record content and retention | OWNER DECISION REQUIRED |

### F. Acknowledge, clear evidence, Resume and presentation

| Situation | Spike behaviour (synthetic) | Open question | Status |
| --- | --- | --- | --- |
| Acknowledge role / permission | Any local UI user (same-origin loopback endpoint) | Who may acknowledge a critical alarm | OWNER DECISION REQUIRED |
| Clear evidence | Synthetic `pump-fault-clear` command only | What evidence clears the condition (Pump status, pressure, Operator inspection record) | OWNER DECISION REQUIRED |
| Resume authority | No Resume exists; `CRITICAL_SUSPENDED` persists; a synthetic test reset (review tooling, not a Resume) leaves the AutoSequence OFF | Who may resume, preconditions, confirmation steps | OWNER DECISION REQUIRED |
| Minimise / move the modal | Not possible | Whether the modal may be minimised (e.g. to view the map) while the condition persists | OWNER DECISION REQUIRED |
| Second alarm channel (horn, beacon, remote notification) | None | Whether a second channel is required | OWNER DECISION REQUIRED |
| Modal after Safe Return failure | Stays open until a synthetic test reset | Production behaviour | OWNER DECISION REQUIRED |
| Operator response text | Generic only (cleaning stopped; sequence suspended; inspect Pump and process; do not resume until cleared and authorised) | Product-specific operating instructions | OWNER DECISION REQUIRED |

## 3. What the spike proves (synthetic only)

- Ordering: the evidence indices satisfy valve close command < valve closed confirmed < axis
  return command < Standby confirmed < outcome < release. The runtime checks this as an
  invariant, the validator checks it on every Snapshot, and harness gates 1–6 check it.
- The GlobalQueue revision and dispatch count do not change while `CRITICAL_SUSPENDED`
  (a runtime invariant).
- Modal presentation rules are covered by jsdom tests (structure) and Owner-local Edge specs
  CRIT-A..F (planned; **not executed in Arena**).
