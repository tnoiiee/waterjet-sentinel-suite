# Stage 0.2.1A — React UI and Runtime Feasibility Spike: Results

**Document status:** Evidence record for Stage 0.2.1A. **Arena evidence**, plus the Owner-local
evidence the Owner reported for checkpoint `dd20a8bd`. That Owner-local evidence covers the
superseded 104-location map and does **not** validate the corrected 106-location map, the
fullscreen Operations layout refinement (§0A), or the readability refinement (§0B), so the
Owner-local re-run and the manual 1920 × 1080 F11 UI re-review are **PENDING** and the 60-minute
run is **PAUSED**. The Owner-reported interrupted overnight observation of `ea23bc58` is recorded
in §0B.1; it is not a controlled benchmark. The Owner reported Edge E2E **25 / 25 PASS** at
`4129687a` and 25 passed / 2 failed / 7 not run (34 selected) at `23f48daa`; §0D corrects the
GlobalQueue semantics (Owner domain correction; previous synthetic queue behaviour SUPERSEDED) and
fixes `READ-A` / `WJ-A` — its Owner-local Edge re-run is **PENDING**. §0E adds synthetic critical Main Pump handling and Mandatory Safe Return
(SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED); its Owner-local Edge review is **PENDING**. The final UI punchlist (§0C) added nine Owner-local Edge specs, the
controlled 15-minute observation is **PAUSED**, and the 60-minute run was waived as a gate by the
Owner (not run). This
document does **not** select React as the final UI framework; the UI framework, Production
transport, and Production chart library remain `[OPEN]`. Blazor counter-spike: **DEFERRED /
NOT AUTHORIZED**.

Plan: [`stage-0.2.1a-plan.md`](stage-0.2.1a-plan.md) · Spike:
[`../../spikes/ui-runtime-react/README.md`](../../spikes/ui-runtime-react/README.md)

All values are produced by synthetic tooling with **SYNTHETIC SPIKE PARAMETERS — NOT
PRODUCTION VALUES**.

---

## 0F. Final spike closeout: synthetic AutoSequence controls (Stage 0.2.1A)

**SYNTHETIC REVIEW TOOLING — NOT THE PRODUCTION OPERATOR-CONTROL MODEL.** No physical device,
interlock or Production protocol is involved; no safety certification is claimed.

**Baseline and recovery.** The local branch had reverted to `main`. The Owner-authorized one-time
recovery restored `81c87a44` (identity proof 153 / 153, compare-and-swap ref update, index-only
refresh; no reset, no force push). This checkpoint is one normal fast-forward commit on
`81c87a44`; its SHA is recorded in the PR #3 description.

### 0F.1 Owner-local Edge failure at `81c87a44` and the deterministic alarm test

Owner-reported: 42 selected · 32 passed · 1 failed · 9 not run (serial mode stopped after the
failure). `S11/S12` compared the Sensor background before and after Raise Alarm:
`color(srgb 0.625961 0.205451 0.168314)` vs `color(srgb 0.624471 0.20502 0.168)`. The alarm did
not recolour the cell; the synthetic Dirty Score moved between the two revisions, and the Dirty
shade follows the Score. The test now:

1. disables auto jobs (the Sensor must not become a Job target);
2. fixes and holds the Score through `set-sensor-score { classification: 'DIRTY', hold: true }`
   (Score 82; the hold ends at `reset-sensor`);
3. waits until the snapshot carries Score 82 / `DIRTY`, then records revision, Score,
   classification, the inline `color-mix` shade and the computed background;
4. Raise Alarm → `ACTIVE_UNACK`, marker visible, a later revision with the same Score and
   classification, the **identical** shade expression and computed background;
5. Clear → `CLEARED_UNACK` + "acknowledgement required", still identical; Acknowledge; reset.

No RGB tolerance is used; the test is kept, not removed.

### 0F.2 Synthetic AutoSequence control model

`sequence.mode`: `OFF` / `RUNNING` / `PAUSE_REQUESTED` / `PAUSED` / `CRITICAL_SUSPENDED`;
`sequence.controls`: `{ enabled, reason }` per control. Commands take no target; dispatch is always
GlobalQueue Position 1, atomically (one Job, one `DispatchRecord`, `positionBefore` 1).

| Control | Accepted when | Effect |
| --- | --- | --- |
| START AUTOSEQUENCE | No critical latch, no Job, not RUNNING / PAUSED, Pump ready, queue ≥ 1 | `RUNNING`; head dispatch (`SYN_AUTOSEQUENCE_START`) |
| PAUSE AFTER CURRENT JOB | `RUNNING` | With a Job: `PAUSE_REQUESTED`, Job continues through Safe Return, no next dispatch, `PAUSED` after release. Without: `PAUSED`; queue unchanged |
| RESUME AUTOSEQUENCE | `PAUSED`, no Job, Pump ready, queue ≥ 1 | `RUNNING`; head dispatch (`SYN_AUTOSEQUENCE_RESUME`); never clears a critical latch |
| ABORT ACTIVE JOB | A Job not already in Safe Return | `ABORTING` → Safe Return (valve close + confirm, axis Standby + confirm) → `ABORTED` → release; no re-queue; `PAUSE_REQUESTED` → `PAUSED`; `CRITICAL_SUSPENDED` stays; with `RUNNING` a later revision may dispatch the new head (never the release revision) |
| RESET CRITICAL SCENARIO | Cleared + acknowledged + Safe Return complete (no Job), no Safe Return failure | Latch removed; `OFF`; queue preserved; no dispatch / Job / Pump start; explicit START required |

The UI (Diagnostics drawer only) shows mode, Job ID and target, queue head and count, Pump
readiness, critical condition / acknowledgement, Safe Return step, and each disabled reason as text.
Buttons are labelled `SYN · …`, send one request per click (no retry or replay), and are disabled
while disconnected. Abort re-queue, Production Pause / Resume and reset authority are recorded in
the [critical matrix §G](critical-pump-safe-return-decision-matrix.md) as OWNER DECISION REQUIRED.

### 0F.3 Documentation consistency

Current-facing queue `HELD` / `BLOCKED` / `EXCLUDED` wording was removed from ARCHITECTURE (§4.4,
§26), REQUIREMENTS (QUE-020, QUE-024, QUE-027 / SEQ-005, SPC-001), CONTROL_AUTHORITY (§6),
CLEANING_SEQUENCE (step 1, §4.2, INVARIANT-SEQ-005), CURRENT_STATE (open items) and QUEUE_MODEL
(§7, §9.1, §12). The "held and rejected simultaneously" question was removed. ADR-0003 carries the
note **SUPERSEDED IN PART by Owner decision dated 2026-10-06**; its history is unchanged.
Historical sections of this results document are left as recorded.

### 0F.4 Validation (Arena)

| Check | Result |
| --- | --- |
| Vitest | 138 / 138 (18 files; new `autoSequenceControl.test.tsx` 7) |
| Harness | 60 / 60 × 3 (new `autoSequenceControls.test.mjs` A–G) |
| Scenarios | 30 PASS / 4 PASS+OWNER / 1 OWNER-LOCAL / 0 FAIL (35; new S35) |
| Playwright list | 49 tests in 5 files (Owner-local selection 48) |
| Build | JS 345.17 kB (gzip 112.92 kB); CSS 31.70 kB (gzip 7.46 kB); unchanged 47.67 kB WOFF2 |

**PENDING (Owner-local final Edge gate):** the 48-test selection, the 18-step manual sequence and
10 screenshots ([`OWNER_LOCAL_TESTING.md`](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md) §1G).

**NOT VERIFIED:** browser rendering, Edge, WebView2, kiosk, hardware, Production queue / Pause /
Resume behaviour, and Production safety.

---

## 0E. Critical Main Pump handling and Mandatory Safe Return (Stage 0.2.1A)

**SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED.** No physical Main Pump, protection
relay, VFD, Isolation Valve, axis, Galil program or Production interlock is involved. No safety,
motion or standards certification is claimed. No Production coordinates, addresses or protocol
details are used.

**Baseline and recovery.** The local branch had reverted. The Owner-authorized one-time recovery
restored `60cd0398` (identity proof 147 / 147, compare-and-swap ref update, index-only refresh; no
reset, no force push). This checkpoint is one normal fast-forward commit on `60cd0398`; its SHA is
recorded in the PR #3 description.

### 0E.1 Owner critical Pump decision and pump event classes

The Main Pump is a **High Critical device**. Spike classification:

| Class | Synthetic event | Handling |
| --- | --- | --- |
| A | Expected commanded stop (`pump-stop`) | Not a fault: no critical modal and no suspension. An Active Job still Safe Returns (trigger `SYN_COMMANDED_PUMP_STOP`, outcome `ABORTED`) |
| B | Unexpected stop | High Critical: stop progression, water and dispatch → AutoSequence `CRITICAL_SUSPENDED` → Active Job enters Mandatory Safe Return → modal immediately |
| C | Trip | As B (trigger `SYN_PUMP_TRIP`) |

The GlobalQueue stays unchanged during B / C (FIFO, no entry state). The spike has no automatic
Resume, no automatic next Job and no `WAITING_FOR_PUMP` Job. If the Pump is not ready at dispatch,
no Job is created and the AutoSequence shows `PUMP_NOT_READY`.

### 0E.2 AutoSequence `CRITICAL_SUSPENDED`

`CRITICAL_SUSPENDED` is checked first in the AutoSequence state derivation. It persists after the
modal closes. Only the synthetic test reset (review tooling, **not a Resume**) leaves it, and the
AutoSequence is then `OFF`. While suspended, the runtime refuses these commands (`CRITICAL_SUSPENDED`):

- enqueue, dequeue and review-job;
- visual presets and mixed sources;
- dispatch-head, start-job and second-job attempts;
- pump-start and reset-sensor.

Two runtime invariants hold throughout: the queue revision and the dispatch count stay frozen.

### 0E.3 Mandatory Safe Return (every outcome)

Steps:

1. **SR1** stop water;
2. **SR2** command the Isolation Valve closed;
3. **SR3** confirm closed;
4. **SR4** command the axis to the Standby Position;
5. **SR5** confirm Standby;
6. **SR6** finalize the outcome;
7. **SR7** release the Active Job;
8. **SR8** let the AutoSequence consider dispatch.

Rules:

- The Job stays the Active Job (state `SAFE_RETURN_*` / `ABORTING`) until SR7.
- Normal completion (P6 end) → `COMPLETED`, which is applied only after SR5.
- Abort, cancel, failure, Pump stop / trip and test reset → `ABORTED` (never `COMPLETED`).
  `abort-job { immediate: true }` remains as a scenario-only shortcut for older layout specs.
- Synthetic timings: minimum step 1.5 s; feedback 3 s; timeout 20 s; review delay 6 s. These are
  SYNTHETIC SPIKE PARAMETERS.
- **Safe Return failure** (feedback absent beyond the timeout): `SAFE_RETURN_FAILED`. The Job is
  retained with no outcome, release or dispatch, and the modal stays open. The failure policy is
  **OWNER DECISION REQUIRED**.
- Evidence is a transient `safeReturn` object on the Active Job plus `lastOutcome` and a bounded
  outcome log (20). It contains per-step times and monotonically increasing evidence indices, and
  never any coordinates or addresses.
- The validator enforces the order: valve close command < valve confirmed < axis command <
  Standby confirmed < outcome < release.

### 0E.4 Critical modal

- **Title:** "CRITICAL ALARM — MAIN PUMP STOPPED" / "… TRIPPED".
- **Content:** time, condition, acknowledge state, AutoSequence, Job / target / phase, live Safe
  Return step, valve / axis / Standby, and the generic response text.
- **Layout:** centered and fitted to the viewport, over a dimmed, blocked (`inert`) background.
  Diagnostics stays outside the inert scope (z 60 over the backdrop at z 50).
- **Controls:** no close button; **Acknowledge** is the only action (`POST /api/spike/critical-alarm-ack`,
  same-origin loopback only; a repeat returns `ALREADY_ACKNOWLEDGED`).
- **Acknowledge** is not a clear and not a Resume. The modal closes only when the condition is
  cleared **and** Safe Return is complete **and** the alarm is acknowledged. The sequence then
  stays suspended, and there is no Resume button.
- **Accessibility:**
  - `role="alertdialog"`, `aria-modal`, labelled title and description;
  - state shown as text, not colour alone;
  - Escape does not dismiss, and focus is trapped with initial focus on Acknowledge;
  - polite live region; no flashing animation.
- **Palette:** dark plum surface `#2a1024` with orchid edge `#d0559c` and white ink (contrast ≥ 7).
  It is tested to be distinct from dirty red (≥ 25° hue), alarm amber and selection cyan (≥ 60°),
  and from the white Job style. No neon.
- **Active Job panel:** shows the Safe Return step and the valve / axis state.
- **Diagnostics:** a Sequence section shows the AutoSequence, the critical event and the last
  outcome.

### 0E.5 Synthetic review controls (`--synthetic-test-controls` only)

The "Critical Pump / Safe Return" group has the ten Owner controls:

1. stop with no Job;
2. trip with no Job;
3. trip in P1;
4. trip in P4;
5. normal completion → Safe Return;
6. valve feedback delay;
7. Standby feedback delay;
8. clear;
9. acknowledge;
10. reset (test only — not a Resume).

It also has one valve-feedback-absent control for failure review. The controls are disabled when
synthetic controls are off or the UI is disconnected, send a single request and never retry.

### 0E.6 GlobalQueue and HELD documentation

The GlobalQueue remains **ready-to-dispatch only** (≤ 8, FIFO, head-only, no entry state).
Queue-level `HELD` is **superseded**, by Owner authorization, in [`../QUEUE_MODEL.md`](../QUEUE_MODEL.md) §7.2 and
[`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md). A pause before dispatch is an AutoSequence state; a
pause during a Job is a Job state. Production Pause / Resume is pending.

### 0E.7 Owner decision matrices

[`critical-pump-safe-return-decision-matrix.md`](critical-pump-safe-return-decision-matrix.md) has
six matrices, every row **OWNER DECISION REQUIRED**:

- A — readiness and stop / trip by phase P1–P6;
- B — valve failures;
- C — axis failures;
- D — Safe Return exceptions;
- E — outcomes / re-queue / retry;
- F — acknowledge role, clear evidence, Resume authority, minimise, second channel.

The queue matrix gained §6, which covers ready-only queues and the superseded HELD.

### 0E.8 Validation (Arena) and pending Owner-local evidence

**Arena results:**

| Check | Result |
| --- | --- |
| Vitest | 131 / 131 (17 files; new `criticalModal.test.tsx` 10, critical palette test) |
| Harness | 51 / 51 × 3 (new `safeReturn.test.mjs` gates 1–6 + scenarios; ack-route test) |
| Scenarios | 29 PASS / 4 PASS+OWNER / 1 OWNER-LOCAL / 0 FAIL (34) |
| Playwright list | 43 tests in 5 files (Owner-local selection 42) |
| Build | JS 340.50 kB (gzip 111.65 kB); CSS 30.92 kB (gzip 7.34 kB); unchanged 47.67 kB WOFF2 |

Scenario detail:

- S33 trip during a Job: SR1–SR8 evidence indices increasing, Safe Return 7.65 s, queue frozen at
  7, `CRITICAL_SUSPENDED` after the modal closed, `OFF` after the test reset.
- S34 normal completion with a 5 s delayed valve feedback → `COMPLETED`.

See [`arena-validation.md`](../../spikes/ui-runtime-react/results/summary/arena-validation.md).

**PENDING (Owner-local Edge):**

- `CRIT-A`..`CRIT-E`;
- `CRIT-F` at 1920 × 1080 and 1366 × 768;
- the earlier selection;
- the manual F11 review with 8 screenshots
  ([`OWNER_LOCAL_TESTING.md`](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md) §1F).

**NOT VERIFIED:** browser rendering, Edge, WebView2, kiosk, hardware, Production Pump / valve /
axis behaviour, and Production safety.

---

## 0D. GlobalQueue semantics correction and head-only dispatch (Stage 0.2.1A)

**Baseline and recovery.** Owner-local Edge at `23f48daa`: 34 selected · 25 passed · 2 failed
(`READ-A` — G+205 value clipped; `WJ-A` — waited for `detail-id` after clicking a non-selectable
Water Jet slot) · 7 not run. The local branch had reverted to `e779f8ad`; the Owner-authorized
one-time recovery restored it to `23f48daa` (identity proof 144 / 144, compare-and-swap ref update,
index-only refresh; no reset, no force push). This checkpoint is one normal fast-forward commit on
`23f48daa`; its SHA is recorded in the PR #3 description.

### 0D.1 Owner domain correction and the superseded synthetic behaviour

The GlobalQueue holds **ready-to-dispatch entries only** (presence = READY), at most **8**. There
are no `BLOCKED` / `HELD` / `WAITING_*` / `EXCLUDED` / `INVALID` / `OUT_OF_SERVICE` / `DISABLED` /
`BAD` / `STALE` entry states. Pause and pump / Water Jet / valve / pressure / pre-check waits belong
to the Active Job or the AutoSequence. No eligibility condition is invented; see the
[Queue Eligibility Decision Matrix](queue-eligibility-decision-matrix.md) (every row
`OWNER DECISION REQUIRED`).

**Defect in the previous synthetic model (`23f48daa` and earlier) — SUPERSEDED, NOT ELIGIBLE FOR
PRODUCTION PROMOTION:**

| Previous behaviour | Effect (Owner examples) |
| --- | --- |
| Auto start scanned forward past non-`READY` entries (per-entry `HELD` / `BLOCKED` / `EXCLUDED`) | Example A: Job `G+217` while the queue was `G+110, G9, G8, I12`. Example B: Job `I12` while the queue was `G+110, G9, G8` |
| The score source admitted every Dirty Sensor; the UI showed an 8-row preview of an unbounded queue | Totals such as **76 / 77 queued** (historical, superseded); S29 recorded 33 |
| Review controls could set an arbitrary Active Job target | A Job could exist for a Sensor that was never the queue head |

### 0D.2 Bounded synthetic queue (spike only)

- Deterministic generator, 0–8 entries, scenario-prepared synthetic Sensors only, FIFO, no
  duplicates, Water Jet slots excluded, deterministic entry IDs (`SYN-QE-nnnnn`) and refill (score
  source in scan order, only into free capacity), labelled "GlobalQueue · synthetic".
- `enqueue` beyond capacity → `QUEUE_FULL`. `QueueSummary.entries` is the whole queue
  (`totalQueued === entries.length`, `capacity: 8`, monotonic `revision`); no `slice(0, 8)`.
- **NOT IMPLEMENTED (documented):** 4 TempQueues + 4 TimeQueues, entry / removal dwell, the full
  eligibility policy, production source ownership and refill, Reject / Reorder, audit records,
  recovery. Production Queue Runtime is a **Main Development** slice (not authorized).

### 0D.3 Head-only dispatch, Active Job transition, dispatch record

- `dispatchHead()` is the only Job creator. Only Position 1 is a candidate (no scan-forward). One
  synchronous step removes the head entry, creates exactly one Job (`target = that Sensor`), and
  writes one synthetic `DispatchRecord` (`SYN-DSP-nnnn`: queue revision before / after (+1),
  `queueEntryId`, `positionBefore: 1`, sensor, source reason, job ID, origin). Position 2 becomes
  Position 1.
- At most one Active Job: dispatch is refused (`ACTIVE_JOB_EXISTS`) while a Job is active; the
  AutoSequence state is `JOB_ACTIVE`; after the Job ends only the new head is a candidate.
- Pump not ready → the Job (not the queue) waits in pre-check (`preCheck: WAITING_FOR_PUMP`,
  shown as the Active Job phase). Synthetic `pump-stop` aborts the Active Job; the next head is
  then dispatched by the AutoSequence and waits in pre-check until the pump is ready.
  **Superseded by §0E:** no Job is created while the Pump is not ready (AutoSequence
  `PUMP_NOT_READY`); `pump-stop` sends the Active Job through Mandatory Safe Return; a Pump
  unexpected stop / trip suspends the AutoSequence (`CRITICAL_SUSPENDED`).
- `pause-auto-sequence` (alias `hold-queue`) pauses dispatch (`autoSequence: PAUSED`); it changes
  no queue entry.
- Review controls never retarget: `review-job` is refused while a Job for another Sensor is active;
  otherwise it first makes the Sensor the queue head (clears the synthetic queue and admits it),
  then dispatches the head. `start-job` uses the same head preparation.

Arena evidence (scenario runner): **S31** — queue `G+110, G9, G8, I12` with an alarm on `G+110`
and BAD / STALE quality on `G9` / `G8` (the conditions that previously caused scan-forward):
Job target `G+110`, new head `G9`, queue revision 123 → 124, second dispatch `ACTIVE_JOB_EXISTS`,
retarget `ACTIVE_JOB_EXISTS`. **S32** — `dirty70` with AutoSequence: maximum queue length 8, every
observed Job linked to a Position 1 dispatch of its own target. **S09** — paused AutoSequence;
`enqueue` on a full queue → `QUEUE_FULL`; entry fields carry no status.

### 0D.4 Hard gates (Arena)

| Gate | Requirement | Evidence |
| --- | --- | --- |
| A | Capacity ≤ 8, no overflow, no duplicates, Water Jet excluded | `queueDispatch.test.mjs`, S09, S29, S32 |
| B | Job target = former head; atomic removal; Position 2 → 1; no later position first | `queueDispatch.test.mjs`, S08, S31 |
| C | ≤ 1 Job; no second dispatch; Job A then Job B | `queueDispatch.test.mjs`, S27, S31 |
| D | Every automatic Job has a record; record sensor = target; Position 1; monotonic revisions | `queueDispatch.test.mjs`, S31, S32 |
| E | UI explainable; count 0–8; no status chips | `globalQueue.test.tsx` (jsdom); Owner-local `QUEUE-B` |
| F | No arbitrary retarget; Review Job makes the Sensor head first; Reset → valid bounded state | `reviewControls.test.mjs`, S30, S31; Owner-local `CTRL-B` |

### 0D.5 GlobalQueue UI and Diagnostics

- Heading **"GlobalQueue · synthetic"**; count **"n / 8 queued · FIFO · not Production
  scheduling"** (n = 0..8). Columns: Pos · Sensor · Source reason · Score · Since clean.
- **Status column removed** (no chips, no `data-status`). Replacement decision, as instructed:
  the source reason is already a column, so nothing replaces it; no entry-age column was added and
  no Source-owner semantics were invented.
- Sensor cell badge: `Q` (queued, ready to dispatch) / `J` (Active Job target); the old HELD /
  BLOCKED / EXCLUDED badge variants and tokens were removed.
- Diagnostics drawer: **"QUEUE → JOB DISPATCH (SYNTHETIC EVIDENCE)"** — AutoSequence state and
  "Position 1 · Sensor · Queue revision a→b · Dispatch ID" (or "No dispatch yet"). **"QUEUE
  ELIGIBILITY DIAGNOSTICS"** appears only when a scenario sets it (preset 6), e.g. "G+110 · NOT
  ADMITTED · Reason pending Owner-approved eligibility policy".
- Presets (SYNTHETIC TEST CONTROL, selected Sensor): 1 Queued DIRTY · 2 Queued CLEANER (synthetic
  non-score source) · 3 Selected queued · 4 Dispatched head becomes Active Job · 5 Alarm on the
  Active Job Sensor · 6 Alarm Sensor not admitted (synthetic demo, policy pending) · 7 Queue head →
  Job atomic transition · 8 Reset. Old preset names are refused (`UNKNOWN_PRESET`).

### 0D.6 Edge value-clipping hotfix (READ-A)

Cause: the value used `line-height: 1` (a 16 px line box) while Google Sans has a 20.03 px content
area at 16 px (typo ascent 966 / descent −286, `USE_TYPO_METRICS`); about 2 px of text content fell
below the line box and Chromium / Edge counts it in the value's `scrollHeight`, which `READ-A`
reports as clipped (first on G+205, the first Sensor cell in DOM order). The row grid
`14px | minmax(0, 1fr) | rail` left no room for a full line box.
Fix (no font, size, or weight change; no overflow hiding; no transform): tokens
`--sensor-id-row-h: 12px` (13 px ID, content 16.28 px, ink fits), `--sensor-value-line-h: 20px`
(value `line-height` from the token), rows `12px | minmax(20px, 1fr) | rail`; the alarm state
keeps its 2 px border with zero padding. Content height is 42.5 px at a 46.5 px cell (normal: 1 px
border + 1 px padding; alarm: 2 px border); 12 + 20 + 10 = 42 px fits across the 46–50 px clamp.
Cell bounds (52–56 × 46–50), ID 13 px, marker 8 px, value 16 px / 700 unchanged. Static guard:
`layoutTokens.test.mjs` "Sensor cell vertical budget". **Rendered result NOT VERIFIED** in Arena.

### 0D.7 Water Jet test hotfix (WJ-A)

The behaviour was correct; the assertion was wrong. Case A: without a selection, clicking a Water
Jet slot leaves `detail-id` absent, selects nothing, and sends no command. Case B: after selecting
a Sensor (detail visible), clicking `WJ / REAR` then `WJ / FRONT` keeps the Sensor detail and selects
no slot. Kept: no I7 / I16 Sensors, visible labels, no queue / job targeting.

### 0D.8 Validation (Arena) and pending Owner-local evidence

Arena: Vitest 120 / 120 (16 files), harness 40 / 40, scenarios 27 PASS / 4 PASS+OWNER /
1 OWNER-LOCAL / 0 FAIL (32), Playwright list 36 tests in 4 files (Owner-local selection 35), build
JS 327.90 kB (gzip 108.05 kB) and CSS 27.67 kB (gzip 6.68 kB) plus the unchanged 47.67 kB WOFF2. See
[`arena-validation.md`](../../spikes/ui-runtime-react/results/summary/arena-validation.md).
**PENDING (Owner-local Edge):** the 35-test selection including `READ-A`, `WJ-A`, `QUEUE-A`, `QUEUE-B`,
`CTRL-A`..`CTRL-C`, and the manual F11 re-review. **NOT VERIFIED:** any browser rendering,
production queue behaviour.

---

## 0C. Final Owner UI punchlist checkpoint (Stage 0.2.1A)

**Baseline.** Owner-local Edge E2E **25 / 25 PASS** at `4129687a` (Owner-reported, ≈ 1.3 min,
installed Edge, Windows 11). The local branch was restored to `4129687a` by the second
Owner-authorized atomic recovery (fetch, 131 / 131 identity proof, compare-and-swap ref update,
index-only refresh; no reset, no force push). This checkpoint is one normal fast-forward commit on
top of `4129687a`; its SHA is recorded in the PR #3 description.

**Unchanged:** U-shaped map, wall order, 106 Sensors / 212 Thermocouple channels, 24 / 29 / 24 / 29,
Water Jet reference slots at I7 / I16, canonical mapping source, right-side cards, bottom Trend +
Camera row, contracts and fixtures, queue authority and FIFO model, single-job rule, dependencies
(no package or lock-file change).

### 0C.1 Google Sans (licence and provenance)

| Item | Record |
| --- | --- |
| Source | Official `google/fonts` repository, `ofl/googlesans`, ref `7085eb89a950e85db5b166b7a58d414544b4140c` (directory last changed `a0e3dbcd`, 2026-09-24); downloaded 2026-10-06 |
| Original | `GoogleSans[GRAD,opsz,wght].ttf`, 4,974,940 B, SHA-256 `d0a87d835a944b8b40d0e82a5651bb59ab97b936a2aeed5946eb57e7b2a3a90a`, Git blob verified |
| Licence | SIL Open Font License 1.1, verbatim `OFL.txt`; **no Reserved Font Name** declared; `TRADEMARKS.md` kept verbatim ("Google" / "Google Sans" are trademarks of Google LLC; no affiliation implied) |
| Bundled asset | `GoogleSans-Latin-Variable.woff2`, **47,672 B**, SHA-256 `40f917d9d0a4de0577c69089456c9e68d8ad3bbf58ac1f8ac91730538cb1531b` — Latin subset, wght 400–700 variable, GRAD 0 / opsz 18 pinned, `kern` + `tnum` kept; deterministic (fontTools 4.60.1, Brotli 1.1.0) |
| Modified Version | Yes (subset / instance / WOFF2) — permitted by OFL-1.1; the name is kept because no RFN is declared |
| Not bundled | Original TTF, italic, other formats or weights; no CDN, external CSS, conversion website, or Windows-installed copy |

Full record: [`FONT_SOURCE.md`](../../spikes/ui-runtime-react/react-ui/src/assets/fonts/FONT_SOURCE.md) ·
build script: [`build_google_sans_subset.py`](../../spikes/ui-runtime-react/measurements/font/build_google_sans_subset.py) ·
licence inventory: [`licence-inventory.md`](../../spikes/ui-runtime-react/results/manifests/licence-inventory.md).

### 0C.2 Presentation changes

- **Font:** `@font-face` (local WOFF2, `font-display: block`) + preload; `--font-ui` =
  `'Google Sans', 'Segoe UI', system-ui, sans-serif` on every visible text, including 13 px Sensor
  IDs, form controls, and the uPlot canvas axes; Bahnschrift removed; first render waits up to
  2.5 s for the font (no fallback render followed by a geometry reflow).
- **Sensor cell:** grid `marker | 1fr | marker` (8 px marker, 2 px gaps); ID row spans the full
  width (13 px / 700); quality marker at the right end of the value row; value 16 px / 700 centred;
  rail unchanged (alarm left, queue right). Arena HarfBuzz measurement: longest ID `G+204` 42.24 px
  of ≈ 53 px usable; value column ≈ 32 px versus `100` = 28.8 px.
- **Identity:** "WaterJet Sentinel Suite" 19 px / 700 over "OPERATIONS CONSOLE" 11 px / 650,
  0.06 em, muted; separate SYNTHETIC badge; accessible name with the full title.
- **Legend:** `LegendSwatch` 16 × 16 SVG with inset symbols, 20 px icon column, 6 px column gap,
  5 px row gap; eight entries.
- **Water Jet terminology:** display labels `WJ REAR` / `WJ FRONT`, legend "Water Jet", summary
  "2 Water Jet reference slots · synthetic". **Display vs internal:** `CANNON_REAR` /
  `CANNON_FRONT` / `slotType: 'CANNON'` are legacy internal identifiers kept for contract and
  fixture compatibility (renaming them needs a separate compatibility decision).

### 0C.3 Mixed GlobalQueue sources (synthetic)

> **SUPERSEDED by §0D** (unbounded queue, per-entry status). Historical record only; not eligible
> for production promotion. The current S29 result is bounded to 8 entries.

`queue-mixed-sources` (scenario command, test evidence only) freezes automatic job starts, then
queues four canonical Sensors through explicit sources and appends every Dirty Sensor through the
score source. Arena result (S29): first eight rows contain **5 source types** — `SYN_TIME_DUE`,
`SYN_TEMP_AND_TIME`, `SYN_OPERATOR_REQUEST`, `SYN_TEMP_RISE`, `SYN_DIRTY_SCORE_ABOVE_THRESHOLD`
(UI labels TIME DUE, TEMP + TIME, OPERATOR, TEMP, DIRTY SCORE). Verified: FIFO positions 1..n,
no duplicate IDs, first-source ownership kept on duplicate enqueue, explicit-source order stable
across publishes, no Water Jet slot, invariant violations 0. Behaviour change: the score source
releases only the entries it owns (previously any non-OPERATOR entry); default runs only contain
score and operator entries, so default behaviour is unchanged.

### 0C.4 SYNTHETIC TEST CONTROL (opt-in review tooling)

> **Presets and queue / job semantics SUPERSEDED by §0D** (the six presets, "held Active Job
> target", and the `BLOCKED` queue state no longer exist). The token / opt-in boundary below is
> unchanged.

- Enabled only with `--synthetic-test-controls` (or `WJSS_SPIKE_TEST_CONTROLS=1`); otherwise
  `GET /api/spike/test-controls` is a JSON 404 and the drawer section reads "Off".
- Token endpoint: same-origin only (`Sec-Fetch-Site` cross-site → 403), loopback `Host` only
  (DNS-rebinding defence → 403), `no-store`, no CORS. The per-run token stays in memory, is dropped
  whenever the connection is not LIVE, and is re-fetched after reconnect; never written to storage.
  **Not an authentication model** and not a Production command path.
- Controls (selected Sensor; disabled without a selection or when disconnected; Water Jet slots
  are not selectable and are refused by the runtime): Raise Alarm, Clear Alarm → ACK REQUIRED,
  Acknowledge, TempQueue / TimeQueue / combined reason, Remove Queue state, GOOD / UNCERTAIN / BAD /
  STALE, DIRTY / CLEANER, Set / Clear held Active Job target, Reset Sensor.
- **Six presets:** 1 Alarm + Queue on DIRTY · 2 Alarm + Queue on CLEANER · 3 Selected + Alarm +
  Queue · 4 Active Job + Alarm + Queue · 5 Cleared Alarm + ACK REQUIRED + Queue · 6 Reset. One
  request per click, no queueing, no retry, no replay; the runtime stays authoritative and the
  never-two-jobs rule holds (S30: accepted second jobs 0).
- A queued Sensor with an alarm is published as `BLOCKED`; an Active Job target as `ACTIVE`
  (existing runtime semantics, unchanged).

### 0C.5 Validation (Arena) and pending Owner-local evidence

Arena: Vitest 109 / 109 (15 files), harness 32 / 32, scenarios 25 PASS / 4 PASS+OWNER /
1 OWNER-LOCAL / 0 FAIL (30), Playwright list 35 tests in 4 files, build JS 325.93 kB (gzip
107.55 kB, +2.71 kB) and CSS 28.16 kB (gzip 6.80 kB) plus the 47.67 kB WOFF2. See
[`arena-validation.md`](../../spikes/ui-runtime-react/results/summary/arena-validation.md).
**PENDING (Owner-local Edge):** `FONT-A`, `CELL-A`, `IDENT-A`, `LEGEND-A`, `WJ-A`, `QUEUE-A`,
`CTRL-A`..`CTRL-C`, plus the 25 existing specs, and the manual F11 re-review. **NOT VERIFIED:**
rendered Google Sans metrics in Edge, WebView2, kiosk shell, Production behaviour.

---

## 0B. Operations readability refinement checkpoint (Stage 0.2.1A)

> **Superseded in part by §0C:** the Sensor ID font is now the bundled Google Sans (Bahnschrift is
> no longer used), the ID row spans the full cell width, and the quality marker moved to the
> right end of the value row. The record below describes checkpoint `4129687a` as delivered.

The Owner's screenshot review of the fullscreen checkpoint (`ea23bc58`) found: status markers
overlapping long Sensor IDs (for example `G+205`), small cells and text, small typography
throughout, oversaturated Dirty / Cleaner colours, alarm and quality depending on colour, weak
Sensor Detail hierarchy, a dense Active Job card, queue spacing and numeric alignment, trend
legibility, an empty-looking camera card, and inconsistent surfaces. This checkpoint is a
presentation-only refinement plus one stale test correction, delivered as one normal
fast-forward commit on top of `ea23bc58`; its SHA is recorded in the PR #3 description.
Industrial HMI practice was used as design guidance only; **no standards certification is
claimed**.

**Unchanged:** the U-shaped map (Rear top, Left / Right sides, Front bottom, compact centre),
106 Sensors, 212 Thermocouple channels, 24 / 29 / 24 / 29, Cannon slots at I7 / I16, canonical
row and column order, the central mapping source, the right-side card structure, the bottom
Trend + Camera row, the runtime harness, the contracts, the queue logic, and the single-job
rule. No dependency was added or changed.

**Stale MAP assertion corrected.** `e2e/operations.spec.ts` (MAP test) still asserted the
pre-fullscreen Y-interval order (Rear ends before the side walls begin; the side walls end
before Front begins). The approved fullscreen layout deliberately lets the side walls share Y
range with the Rear / Front blocks, so those assertions were stale. They were replaced by
centre-relative assertions: Rear above the centre summary, Left left of it, Right right of it,
Front below it; Rear y-centre above Front y-centre; Left x-centre left of Right x-centre; Rear
and Front horizontally centred (± 2 px); no wall–wall and no wall–centre rectangle intersection;
every wall inside the map surface. The 4-wall / 6-row / 106 / 24-29-24-29 / Cannon / selection
assertions are unchanged. The layout was **not** changed to satisfy the stale test.

**Sensor cell composition (zones, CSS grid, no absolute overlap):**

| Zone | Content | Rule |
| --- | --- | --- |
| Top-left | Sensor ID, 13 px / 700, Bahnschrift semi-condensed (Windows system font; Segoe UI fallback) | Width limited to the cell minus the 12 px reserved marker zone; clipped to its own box, so it can never sit under a marker |
| Top-right | Reserved 10 × 10 px marker zone (+ 2 px gap) | Data quality: amber dot (UNCERTAIN), cross (BAD), clock (STALE), slashed circle (DISABLED) — inline SVG, shape + colour |
| Centre | Value, 16 px / 700, tabular numerals | Full width |
| Bottom rail (10 px) | Alarm warning triangle (left), queue badge (right) | Never over the ID, value, or quality marker |

Channels use disjoint CSS properties: process + score → background (`color-mix` of the base and
strong token); quality → marker zone (+ neutral pattern for BAD / STALE / DISABLED); alarm →
2 px yellow border + inset ring + rail icon (dashed when cleared-unacknowledged); selection →
3 px cyan outer ring; Active Job → white double outline. The alarm border keeps the content box
size (padding compensates), so the ID zone does not shrink when an alarm is raised.

**Cell scale:** height `clamp(46px, …, 50px)`, width `clamp(52px, min(height + 9px, (map width −
120px) / 13), 56px)`, gap 4 px; Cannon slot identical. Expected at 1920 × 1080 by layout
arithmetic (**not** a browser measurement): ≈ 55.3 × 46.5 px, ID zone ≈ 37 px wide. The 13 px
bold `G+205` in Segoe UI is ≈ 41 px wide and would not fit beside the marker zone in any 52–56 px
cell; the Bahnschrift semi-condensed face is estimated at ≈ 32–34 px. Actual glyph widths are
**not observable in Arena**; `READ-A` fails if any ID is clipped or touches the marker zone.

**Colours (central tokens in `react-ui/src/global.css`; values nowhere else):** Cleaner
`#256B4A` → `#2F8059`; Dirty `#B33A2F` → `#8D2F27`; Not classified graphite `#3F454D` with a
subtle hatch; BAD / STALE / DISABLED neutral `#4A5058` / `#353A40` + pattern + glyph; Uncertain
amber `#E8A33D` as a dot, never a background; Alarm yellow `#FFC531` border + triangle,
separated from Dirty red by hue and luminance (≥ 3 : 1 against every Dirty shade); Selection cyan
`#36D6F0`; Active Job white double outline; Cannon, three surface levels, two border levels, and
four text levels. Sensor text is pure white: computed WCAG contrast ≥ 4.5 : 1 on every
Dirty / Cleaner / neutral shade (`colorTokens.test.mjs`; with the off-white text token the
lightest Cleaner shade gave 4.41 : 1, so a dedicated `--cell-text` token is used). No glow, no
gradients, no glass, no heavy shadows.

**Typography (px / weight):** app title 15 / 700; status chips 12.5 / 600; alarm strip 12; card
titles 15 / 700; wall titles 14 / 700; wall metadata 12; Sensor ID 13 / 700; value 16 / 700;
detail labels 12, values 13 / 600, Sensor ID header 18 / 700; Job body 13; queue headers
12 / 650, rows 13, badges 11 / 700; trend title 14, axes and legend 12; camera title 14, state
12; diagnostics 12; group labels and captions 11. No 8–9 px text. Tabular numerals for values,
scores, times, revisions, pressure, and axes. Spacing scale 2 / 4 / 6 / 8 / 12 / 16 / 24 px.

**Status bar (40 px) and alarm strip (24 px):** groups in priority order Alarm → Process (Job,
Pump) → System (connection, devices) → Technical (revision, config, lower emphasis), separated
by dividers, `role="group"` with labels. The alarm strip shows a compact
`No active alarms · no acknowledgement required` state, and otherwise the counts plus, per alarm,
the condition and the required response (`acknowledge and inspect`, `monitor until cleared`,
`acknowledge`).

**Sensor Detail (212 px):** header with the Sensor ID (18 px) and a classification chip; two
columns — *Process* (Classification, Dirty Score, Last validated) and *Location* (Wall /
position) | *State* (Quality, Queue, Alarm) and *Source* (Device, TC_F / TC_R). Classification,
Dirty Score, Quality, Queue, and Alarm are emphasised; Device, channels, and timestamps are
de-emphasised; the synthetic notice is a small header label. No nested cards.

**Active Cleaning Job (160 px):** Job ID chip; Target Sensor / Water Jet / Isolation Valve as
separate label-over-value fields; P1–P6 as completed (✓) / current (`aria-current="step"`) /
future (dashed); phase label, progress bar, percentage, and elapsed time; single-job notice.

**GlobalQueue:** columns Pos 40 / Sensor 100 / Source reason (flex) / Score 90 / Since clean 110 /
Status 90 px; 28 px header and rows; 13 px tabular numerals, numeric columns right-aligned;
subtle separators; no red rows; outline status chips from tokens (READY, HELD, BLOCKED dashed,
EXCLUDED). Queue order and membership are unchanged.

**Pressure Trend:** lines 1.75 px; series colours resolved from tokens and separated by
luminance; axes and legend 12 px; lower-contrast grid; ready band with edge lines; dashed
setpoint; 18 × 3 px legend samples; header summary of the latest series values, setpoint, and
ready band. Still four series, 600-point bound, visible gaps, one uPlot instance, no animation.

**Camera:** title, `SYNTHETIC PLACEHOLDER` badge, 48 px no-camera icon, `NO SIGNAL`, and
`No video source configured`. No media element, no URL.

**Height budget at 1920 × 1080 (layout arithmetic):** status 40 + alarm strip 24; main padding
2 × 6, gap 6; bottom row `clamp(196px, 19.3vh, 240px)` ≈ 208; map workspace ≈ 790 (U surface
≈ 776 + 10 chrome); right column: Detail 212, Job 160, GlobalQueue ≈ 394 (28 + 8 × 28 rows
needed).

| Check (Arena, Linux x64) | Result |
| --- | --- |
| `npm ci --ignore-scripts` (`PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`), lock file unchanged | PASS — dependencies unchanged |
| TypeScript `tsc --noEmit` (project) and a separate strict check of `e2e/*.ts` | PASS, 0 errors |
| Vite production build | PASS — JS 316.77 kB (gzip 104.84 kB), CSS 25.68 kB (gzip 6.32 kB) |
| Vitest (jsdom), 13 files | **93 / 93** PASS (was 75; new `colorTokens.test.mjs` and new cases in `visual`, `layoutTokens`, `fullscreenLayout`) |
| Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| Playwright `--list` (no browser) | 26 tests in 3 files; `operations.spec.ts` + `layout.spec.ts` selection 25 (was 21; 4 new `READ-A`..`READ-D`) |
| Mapping hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| Overlap, clipping, typography, viewport fit in a browser | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |

jsdom performs no layout: the unit tests cover tokens, DOM zones, structure, groups,
`aria-current`, tabular-column classes, and the camera state. They do **not** validate pixel
overlap; that is asserted only by the Edge specs.

**Browser tests (Owner-local, Edge):** `READ-A` — at 1920 × 1080 with the quality showcase,
70 % Dirty, and three raised alarms: for all 106 cells the ID equals the Sensor ID, is not
clipped, does not intersect the marker zone (≥ 1 px gap) or the rail; the value is not clipped;
the quality marker sits inside its zone; the alarm icon and queue badge stay inside the cell and
off the ID, value, and quality marker; ID ≥ 13 px, value ≥ 16 px; Cannon size unchanged; no
page scroll. `READ-B` — alarm on a Dirty and on a Cleaner cell (alarm-token border ≥ 2 px + icon,
background unchanged), selection ring on a Dirty cell, Active Job double outline on a Cleaner
cell, exactly one current phase. `READ-C` — no visible text below 10 px; approved sizes and
weights for the title, chips, alarm strip, card / wall titles, detail ID, labels, values, queue
header, trend and camera titles, camera state; tabular numerals; no page scroll. `READ-D` —
right-aligned queue Score and Since-clean columns with a common right edge, no red row
backgrounds, status-bar group order and height ≤ 42 px, one uPlot instance with a 12 px legend,
trend summary, camera icon ≥ 40 px with no media element. `LAYOUT-A`/`LAYOUT-D`/`LAYOUT-E` were
aligned to the approved readability scale (cell 52–56 × 46–50 px, ID ≥ 13 px / 700, value
≥ 16 px / 700; the 2560 × 1440 width cap follows the approved 56 px maximum); `LAYOUT-B` and
`LAYOUT-C` are unchanged.

**Known limitations:** at 1366 × 768 the U-map (≈ 770 px at the 46 px minimum height) exceeds
the ≈ 490 px map area, so the map card scrolls internally (one-screen fit is not claimed there);
the map column is at least 740 px so the U fits horizontally. Bahnschrift availability and exact
glyph widths depend on the Windows installation and are not observable in Arena.

### 0B.1 Owner-local interrupted overnight observation (recorded as reported by the Owner)

| Item | Owner-reported value |
| --- | --- |
| Observation | Owner-local interrupted overnight observation: **COMPLETED**, from checkpoint `ea23bc58` |
| Harness wall-clock uptime | ≈ 11 h 41 min; host sleep / hibernate occurred |
| Likely active state-update exposure | ≈ 4 h |
| Connection at capture | LIVE; reconnects 1; trend points 600 (bounded) |
| Invariant violations | 0 |
| Second Jobs accepted | 0 |
| Historian samples rejected | 0 |
| Synthetic Jobs completed | 659 |
| Controlled 60-minute benchmark | **NOT PERFORMED** |
| Production stability | **NOT VERIFIED** |
| Modbus performance | **NOT TESTED** |

This was an interrupted, uncontrolled observation and is **not** a controlled benchmark. Next
Owner-local steps: full Edge E2E on this checkpoint, the manual F11 review, and a controlled
15-minute observation ([handoff §1C](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md#1c-re-run-after-the-operations-readability-refinement-required)).
The 60-minute run remains **PAUSED**.

## 0A. Fullscreen Operations refinement checkpoint (current)

*Superseded for visual scale, colours, and card composition by §0B; retained as the record of the
fullscreen checkpoint `ea23bc58`.*

The Owner approved a targeted Design Addendum for the existing Operations page. Primary target:
Windows 11, Microsoft Edge, F11 fullscreen, **1920 × 1080**, zoom 100 %. The corrected Sensor
domain is unchanged: 106 Sensors, 212 Thermocouple channels, 24 / 29 / 24 / 29, Cannon slots at
I7 / I16, canonical row and column order, U-shaped orientation, central mapping source. The
refinement is a fast-forward commit on top of `935973e6`; its SHA is recorded in the PR #3
description.

**What changed (presentation only):**

- **Single design-token location** (`react-ui/src/global.css`): typography scale, page height
  model, map / operations split, card heights, U geometry, Sensor cell scale. Component styles
  only reference the tokens.
- **Strict viewport-height model:** app root `100dvh`, `overflow: hidden`; top status bar
  (34 px) and alarm strip (24 px); main grid `minmax(0, 1fr)` workspace plus a bounded bottom row
  (`clamp(200px, 21vh, 240px)`, ≈ 227 px at 1080); all grid children `min-width: 0` /
  `min-height: 0`. Fit comes from space allocation, not from smaller text.
- **Sensor cells:** the Sensor Map area is the only CSS size container. Cell height is
  `clamp(42px, (map height − fixed U terms) / 12, 50px)`; cell width is `clamp(44px,
  min(height + 6px, (map width − 150px) / 13), 54px)`. Expected at 1920 × 1080 (layout
  arithmetic, not a browser measurement): ≈ 50.4 × 44.4 px. Sensor ID 12.5 px / 700, value
  15 px / 700, gap 4 px. Cannon slot: same outer size.
- **U-shaped geometry:** wall blocks are absolutely placed inside one bounded map surface —
  Rear centred at the top, Front centred at the bottom, Left / Right vertically centred at the
  card edges, so the side walls share Y range with the Rear / Front blocks while their X ranges
  stay disjoint. The map surface height is two wall blocks plus the compact centre; no empty grid
  track and no transform scaling.
- **Centre summary:** 204 × 136 px — `106 Sensors`, `2 Cannon slots · synthetic`, an
  eight-entry legend (Dirty, Cleaner, Not classified, Uncertain, Alarm, Selected, Active Job,
  Cannon), and the synthetic threshold caption.
- **Map / operations split:** `clamp(700px, 44%, 900px)` for the map, remainder for the
  operations column (≈ 44 % / 56 % at 1920); content width capped at 2240 px and centred.
- **Sensor Detail:** two-column inspector, fixed 184 px; values truncate with the full text in a
  tooltip; the UNCERTAIN note is one line.
- **Active Cleaning Job:** fixed 132 px; Job ID, Sensor, Water Jet, Isolation Valve, P1–P6
  (30 px cells), phase, progress, start time, single-job notice.
- **GlobalQueue:** fixed 25 px header and 26 px rows; compact source reason (`DIRTY SCORE`,
  `TEMP`, `TIME`, `TEMP + TIME`, `OPERATOR`, `SCENARIO`) with the full code as a tooltip; the
  queue behaviour is unchanged.
- **Bottom row:** same uPlot instance sized to its bounded host (legend height subtracted), 11 px
  axes, four series, 600-point bound, setpoint, ready band, visible gaps. Camera placeholder
  280 px wide, scaled with `preserveAspectRatio`.
- **Diagnostics:** floating drawer (`position: fixed`, 320 px, `max-height: min(60vh, …)`)
  below the status area, bounded above the bottom row, internal scrolling, Close button and the
  `D` shortcut; all metrics retained.

| Check (Arena, Linux x64) | Result |
| --- | --- |
| `npm ci --ignore-scripts --no-audit --no-fund` (`PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`), lock file unchanged | PASS |
| TypeScript `tsc --noEmit` | PASS, 0 errors |
| Vite production build | PASS — JS 307.27 kB (gzip 102.61 kB), CSS 17.45 kB (gzip 4.83 kB) |
| Vitest (jsdom), 12 files | **75 / 75** PASS (was 60; 15 new in `layoutTokens.test.mjs` and `fullscreenLayout.test.tsx`) |
| Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| Playwright `--list` (no browser) | 22 tests — 5 new layout tests `LAYOUT-A`..`LAYOUT-E` in `e2e/layout.spec.ts` |
| Mapping hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| 1920 × 1080 / 1366 × 768 / 2560 × 1440 viewport fit | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run of `e2e/layout.spec.ts` and the manual F11 review are **PENDING** |

**Layout tests (Owner-local, Edge):** `LAYOUT-A` 1920 × 1080 — document scroll ≤ client size,
status bar, alarm strip, and six primary cards inside the viewport and non-overlapping, eight
queue rows visible, computed cell 48–52 × 42–48 px, ID ≥ 12 px, value ≥ 14 px, Cannon size equal
to a Sensor cell, 106 / 2 rendered, Trend and Camera inside their cards. `LAYOUT-B` U geometry
— Rear above, Left left of, Right right of, Front below the centre; centre ≤ 210 × 170 px; no
wall overlap; side walls share Y range with Rear / Front; canonical order. `LAYOUT-C`
Diagnostics — unchanged document scroll size, inside the viewport, 300–340 px wide, never over
Trend or Camera, closable, `D` toggle. `LAYOUT-D` 1366 × 768 — no page overflow, no card overlap,
readable cells, overflow only inside panels with controlled scrolling. `LAYOUT-E` 2560 × 1440 —
cells ≤ 54 × 50 px, bounded centre and side-wall spacing, centred content.

**Known limitations:** at 1366 × 768 the full U-map (≈ 728 px high at the 42 px minimum cell
height) does not fit the ≈ 490 px map area, so the map card and the GlobalQueue scroll
internally; one-screen fit is **not** claimed there. Below 1100 px width the page falls back to
one column with internal scrolling of the main area. Exact text widths depend on the installed
Segoe UI metrics and are not observable in Arena.

## 0. Sensor-map correction checkpoint

The Owner corrected the protected count from 104 / 208 to **106 Sensor locations / 212
Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). The Sensors sit in an 18 × 6
logical matrix with Cannon equipment slots at logical I7 and I16; see
[plan §6.1](stage-0.2.1a-plan.md#61-sensor-map-owner-domain-correction). This is a domain
correction, not a runtime failure. The correction is a fast-forward commit on top of `dd20a8bd`.
Its SHA is recorded in the PR #3 description.

| Check (Arena, Linux x64, Node v22.22.3) | Result |
| --- | --- |
| `npm ci` (`PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`), lock file unchanged | PASS |
| `tsc --noEmit` (TypeScript 6.0.3) | PASS — 0 errors |
| `vite build` | PASS — JS 303.71 kB (gzip 101.41 kB), CSS 10.90 kB (gzip 3.45 kB) |
| Vitest (jsdom), 10 files | **60 / 60 PASS** (53 updated + 7 new: `wallMap.test.tsx` 5, `wallMapSource.test.mjs` 2) |
| Harness `node:test`, 2 files | **26 / 26 PASS** (14 updated + 12 new in `sensorMap.test.mjs`) |
| Scenario runner, 28 scenarios | **PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0**; S01 evidence: 106 Sensors, 212 channels, 24 / 29 / 24 / 29, 108 wall-map slots, Cannons at R5C07 / R5C16 |
| Golden fixtures regenerated and validated against the canonical map | PASS |
| 10-minute harness measurement, 2 SSE clients | PASS — exit 0, invariant violations 0, accepted second Jobs 0, Jobs completed 21 |
| `playwright test --list` (no browser) | PASS — 17 tests in 2 files (new `MAP` wall-map test) |
| Markdown relative links | PASS — 44 files, 694 links, 0 broken; 7 anchors, 0 broken |
| Sensitive-data scan, changed and new files | PASS |
| Hard gates | 106 Sensors · 212 channels · 24 / 29 / 24 / 29 · 2 Cannons · I7 / I16 absent · 0 duplicate IDs, slots, scan orders, `TC_F` / `TC_R`, or shared channels · 0 failed tests · 0 broken links · 0 accepted second Jobs · 0 invariant violations · 0 sensitive findings |

Mapping tests cover the following:

- counts;
- each ID range present exactly once, and I7 / I16 absent;
- Cannon positions;
- row and column order;
- the column-to-wall mapping, with 6 rows per wall and no rotation;
- duplicate and shared-channel checks;
- harness and poll-plan derivation from the single source;
- a Snapshot with 106 / 212 / 108 slots, and a `wallMap` that never appears in Deltas;
- the validator rejecting a Cannon used as a Sensor and a corrupted I7 slot;
- the runtime refusing `enqueue`, `start-job`, `raise-alarm`, `force-quality`, and `disable-sensor` for Cannon IDs (`CANNON_NOT_A_SENSOR`);
- dirty 30 % / 70 % computed over 106 Sensors (32 / 74);
- a deterministic synthetic workload: same seed gives the same process trace, per-device latency sequence, and per-device noise; a different seed gives a different trace.

UI tests (jsdom, not a browser) cover the following:

- 4 walls in U order (REAR, LEFT, RIGHT, FRONT), each 6 rows deep, with 5 / 4 / 4 / 5 columns;
- 106 Sensor cells and 2 Cannon slots;
- canonical row and column order, including `I5, I6, CANNON_REAR, I8, I9`;
- Cannons that are not buttons, not selectable, and carry no overlays;
- the selection detail showing the logical position;
- a layout that comes from the Snapshot only;
- the U-shaped grid areas retained in CSS;
- React source that never imports the mapping module or generates IDs.

**Determinism.** The synthetic process workload is deterministic for the same seed, scenario
timeline, synthetic configuration, and code revision. Cryptographic run and scenario tokens are
non-deterministic but do not affect process values, scenario ordering, the device latency
sequence, classification, or revision behaviour. Identical wall-clock timing is not claimed.

**Owner-local evidence for `dd20a8bd`** (Owner-reported): Windows 11, Node v24.20.0, npm 11.19.0,
Git 2.55.0.windows.5, installed Edge. `npm ci`, typecheck, Vitest 53 / 53, build, harness
14 / 14, and Edge E2E 15 / 15 were all PASS. **It does not validate the corrected checkpoint.**

Sections 1 to 6 below record the original `dd20a8bd` checkpoint (104-location map, superseded)
unless marked otherwise. The section 4 measurement table has been **re-measured on the corrected
map**.

## 1. Baseline

| Item | Observed |
| --- | --- |
| Branch | `arena/01a108d8-waterjet-sentinel-suite` |
| Base | `e779f8ad2c856e367fd65985007a3da411bd0e73` = remote `main` (verified with `git ls-remote` before coding) |
| Initial working tree | Clean |
| Arena environment | Linux x64, 2 logical cores, 3.8 GB, Node v22.22.3, npm 10.9.8 — [`arena-environment.json`](../../spikes/ui-runtime-react/results/environment/arena-environment.json) |
| Browser / WebView2 / .NET / Windows in Arena | None |

## 2. Dependencies (Owner-approved pins only)

| Package | Version | Licence | Use |
| --- | --- | --- | --- |
| react, react-dom | 19.3.0 | MIT | Runtime |
| uplot | 1.6.32 | MIT | Runtime — **spike only**; Production chart library `[OPEN]` |
| typescript | 6.0.3 | Apache-2.0 | Dev |
| vite | 8.3.2 | MIT | Dev |
| @vitejs/plugin-react | 6.1.1 | MIT | Dev |
| vitest | 5.0.3 | MIT | Dev |
| jsdom | 30.1.2 | MIT | Dev |
| @testing-library/react / dom | 16.3.3 / 10.4.2 | MIT | Dev |
| @playwright/test | 1.63.0 | Apache-2.0 | Dev (Owner-local Edge runs; no browser downloaded) |
| @types/react, @types/react-dom | 19.3.0 | MIT | Dev — type declarations named in the plan; **not on the Owner dependency list**, flagged for Owner confirmation |

- Lockfile: 122 entries (all platforms); npm reported 97 to 99 packages installed on Linux.
  Runtime bundle dependencies: react, react-dom, scheduler 0.28.0, uplot — all MIT.
- Licences across the lockfile: MIT 92, Apache-2.0 8, MPL-2.0 12 (lightningcss and its
  platform binaries — build-time only, weak file-level copyleft, recorded transparently),
  MIT-0 2, BSD-2-Clause 2, BSD-3-Clause 2, ISC 2, BlueOak-1.0.0 1, CC0-1.0 1.
  Inventory: [`licence-inventory.md`](../../spikes/ui-runtime-react/results/manifests/licence-inventory.md).
  SBOM (CycloneDX 1.5, 122 components): `results/manifests/react-ui-sbom.cdx.json`.
- Install scripts: the only lockfile entry with an install script is `fsevents` (macOS-only
  optional, not installed). All installs used `--ignore-scripts` and
  `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`.
- The runtime harness and scenario runner use Node built-ins only (no dependencies).
- No prohibited package (TypeScript 7, Blazor, SignalR, Zustand, Redux, UI component library,
  commercial chart, `@axe-core/playwright`, SQL, Modbus, Galil, code generation, authentication).

## 3. Arena validation

| Check | Result |
| --- | --- |
| `tsc --noEmit` (TypeScript 6.0.3) | PASS — 0 errors |
| `vite build` (production) | PASS — 226 ms reported by Vite; 0.5 s wall incl. npm start-up |
| Vitest (jsdom) — 8 files | **53 / 53 PASS** |
| Harness unit tests (`node:test`) | **14 / 14 PASS** |
| Scenario runner (28 scenarios, runtime / SSE level) | **PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0** — [`arena-scenarios.md`](../../spikes/ui-runtime-react/results/summary/arena-scenarios.md) |
| Playwright spec parse (`playwright test --list`, no browser) | PASS — 16 tests in 2 files listed for project `msedge` |
| Offline restore rehearsal (Linux) | PASS — see section 6 |
| Sensitive-data scan (new and changed files) | PASS — no credentials, keys, connection strings, or non-synthetic identifiers; only IPv4 literals are `127.0.0.1`, `0.0.0.0` (refusal test), and `192.0.2.10` (RFC 5737 documentation address used to prove non-loopback refusal) |
| Markdown relative-link check (all tracked and new `.md`) | PASS — 0 broken links |
| Loopback bind | Harness binds `127.0.0.1` only; binding `0.0.0.0` or a non-loopback address is refused (unit test) |

Vitest coverage by file: `classification.test.ts` (Dirty / Cleaner threshold, UNCERTAIN keeps
last validated, BAD / STALE / DISABLED → NOT_CLASSIFIED), `visual.test.ts` (seven-dimension
cell mapping, Dirty red ≠ alarm colour, selection and Active Job outlines), `store.test.ts`
(Snapshot replace, Delta apply, gap and duplicate detection, whole-record replacement),
`feed.test.ts` (gap → re-snapshot, reconnect state), `ringBuffer.test.ts` (bounded trend),
`fixtures.test.ts` (golden Snapshot / Delta contract fixtures), `renderIsolation.test.tsx`
(see below), `sseTransport.test.mjs` (real harness over HTTP: Snapshot first, chained Deltas,
`Last-Event-ID` → fresh Snapshot with no replay, client drop → re-snapshot, revision gap →
resync).

**Render isolation (jsdom, not a browser):** one changed Sensor → 1 SensorCell render;
panel-only Delta → 0 SensorCell renders; 10 changed Sensors → 10; selection change → 2;
selection survives Deltas and Snapshots. This is a React reconciliation result in jsdom and is
**not** a browser rendering or paint measurement.

### 3.1 Defects found and corrected during Arena validation

| Finding | Cause | Correction |
| --- | --- | --- |
| Measurement client counted 310 "gaps" in a 10 min run where one gap was injected | The Node measurement mirror counted the gap but did not re-snapshot, so every later Delta mismatched | Opt-in `resyncOnGap` in `scenario-runner/sse-client.mjs` mirrors the UI contract; rerun shows 1 gap and 1 re-snapshot per client |
| S20: Historian stayed near overflow > 12 s after the write delay was restored | The simulator applied a new write delay only to the *next* batch; the in-flight batch kept the 600 s stall timer from S19 | `HistorianChannel` now reschedules the in-flight batch at `min(original end, start + new delay)`; new unit test; S20 clears near-overflow in about 0.5 s. **Production lesson:** the Historian write path needs an explicit write-timeout / cancellation rule — part of the `[OPEN]` overflow policy |
| Measurement cycle had no pump stop | Omitted from the cycle | `pump-stop` / `pump-start` added |

## 4. Arena measurements (Node harness and SSE only)

**Re-measured on the corrected 106-location map.** 10-minute harness run, 2 SSE clients, 10 s
sampling, scenario command every 30 s —
[`arena-harness-smoke-10min.md`](../../spikes/ui-runtime-react/results/summary/arena-harness-smoke-10min.md).

| Harness metric | p50 / p95 / max (unless stated) |
| --- | --- |
| Harness process CPU % (one Node process, 2-core VM) | 1.03 / 1.45 / 1.57 |
| Harness RSS MB | 69.8 / 74.2 / 74.2 — first-third mean 67.7 → last-third mean 73.8 |
| JS heap used MB (p50 / max) | 10.4 / 16.5 |
| Event-loop delay p99 ms | 10.9 (at the default 10 ms histogram resolution of `monitorEventLoopDelay`) |
| Snapshot bytes (p50 / max) | 78,184 / 108,069 (was 50,422 / 80,318 on the 104 map; now includes the 108-slot `wallMap` and position fields) |
| Delta bytes | 61,018 / 61,091 / 61,985 (was ~50 kB; larger Sensor records) |
| Delta interval ms | 1000 / 1002 / 1004 |
| Delta lag, generated → received by a Node client on the same host, ms | 2 / 3 / 5 — **not** a browser or end-to-end UI latency |
| Delta rate | 1.03 / s |
| Revision gaps injected → re-snapshots (per client) | 1 → 1 |
| Publish duration ms | 1.115 / 1.785 / 4.203 |
| Max in-flight requests, global / per device | 4 / 1 (bound 4; no overlap within a device) |
| FAST poll latency ms | 21.9 / 38.29 / 750.99 (max = injected 750 ms timeout) |
| Historian max depth / rejected | 11,224 / 0 (capacity 50,000) |
| Jobs completed / accepted second Jobs | 21 / 0 |
| Pump-stop command handling inside the harness | 0.138 ms (n = 1) — not end-to-end |
| Invariant violations | 0 |

**RSS concern:** RSS rose by about 6 MB between the first and last thirds of a 10-minute run
while heap stayed bounded (max 16.5 MB). The `dd20a8bd` run showed about 7 MB. Ten minutes cannot separate warm-up from growth; the
trend is **not evaluated** and must be checked in the Owner-local 60-minute run.

| Build / bundle | Value |
| --- | --- |
| JS bundle | 301,821 B raw · 100.79 kB gzip (Vite) · 99,337 B `gzip -9` |
| CSS | 10,371 B raw · 3.29 kB gzip |
| `dist/` total | 312,815 B |
| Build time | 226 ms (Vite) |
| Offline `npm ci` | 2.0 s, exit 0 |

**Delta size note:** the synthetic acquisition changes every Sensor value every second, so the
Delta (~61 kB on the corrected map) is close to the Sensor portion of a full Snapshot. This is a deliberate worst case for
the spike; Production Delta size depends on real change rates and deadbands `[NOT VERIFIED]`.

**Not measured in Arena:** browser CPU, heap, DOM node count, long tasks, paint, end-to-end
Delta-to-pixel latency, Edge or WebView2 process memory, Windows behaviour, kiosk behaviour,
long-running UI stability.

## 5. Scenario summary

All 28 Owner scenarios plus the revision-gap scenario (S28) run in the scenario runner.
S07 (selection during updates) and S22 (camera placeholder) pass at the runtime level and
have Owner-local browser parts. S25 (viewport resize) is browser-only and Owner-local.
Highlights: S10 Job phases P1 to P6 observed with at most one active Job; S11 alarm does not
change classification; S12 cleared-unacknowledged alarm requires acknowledgement; S13 to S15
device timeout → UNCERTAIN → BAD, other devices keep polling, recovery; S17 and S18 reconnect
starts with a fresh Snapshot, 0 Deltas replayed; S19 Historian stall does not slow Deltas
(max interval about 1 s); S20 near-overflow, rejected count, gap marker, pump-stop round trip
about 2 ms during overflow; S21 bounded trend; S27 second Job refused (`ACTIVE_JOB_EXISTS`),
accepted second Jobs 0.

## 6. Offline restore rehearsal (Linux, Arena)

1. Online: `npm ci --cache ../.cache/npm-offline --ignore-scripts`, then
   `npm cache add` for `lightningcss-win32-x64-msvc@1.33.0` and
   `@rolldown/binding-win32-x64-msvc@1.2.12` (Windows natives). Cache size 54 MB (not committed).
2. `rm -rf node_modules dist`, then
   `npm ci --offline --cache ../.cache/npm-offline --registry http://127.0.0.1:9/ --ignore-scripts`
   → **exit 0**, 2.0 s.
3. Negative control with the same flags for an uncached package → `ENOTCACHED`, exit 1 (no
   network fallback).
4. On the offline-restored tree: `tsc` PASS, `vite build` PASS (identical bundle hash),
   Vitest 53 / 53 PASS, `playwright test --list` PASS, harness tests PASS.

**Windows offline restore: NOT VERIFIED** — procedure in
[`OFFLINE_RESTORE.md`](../../spikes/ui-runtime-react/measurements/OFFLINE_RESTORE.md).

## 7. Owner-local handoff

**Required re-run for the fullscreen refinement checkpoint:** see
[`OWNER_LOCAL_TESTING.md` §1B](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md#1b-re-run-after-the-fullscreen-layout-refinement-required)
— typecheck, Vitest, build, harness tests, Edge E2E including `e2e/layout.spec.ts`, and a manual
F11 review at 1920 × 1080, 100 % zoom, with four Owner screenshots.

**Earlier re-run for the corrected map** (still covered by §1B): typecheck, Vitest, build,
harness tests, Edge E2E (16 functional tests including `MAP`), and a manual UI review covering:

- the U-shape;
- 106 Sensors and 2 Cannons;
- 24 / 29 / 24 / 29;
- IDs, row order, and wall columns;
- I7 / I16;
- quality, queue, selection, Job, and alarm overlays.

See [`OWNER_LOCAL_TESTING.md` §1A](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md#1a-re-run-after-the-sensor-map-correction-required).
The **60-minute run stays PAUSED** until the Owner visually accepts the map.

Prepared: [`OWNER_LOCAL_TESTING.md`](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md)
— installed Edge via Playwright `channel: 'msedge'`, production build served by the harness,
PowerShell process sampler, 10 min Smoke, 60 min Feasibility, optional 4 h Extended, Owner sets
exploratory thresholds after Smoke. Results to be summarised with `measurements/summarize.mjs`.

## 8. Environment observation

Arena's sandbox platform automatically mirrors listening loopback ports with its own
forwarder (`socat`, bound to a link-local sandbox interface address and forwarding to `localhost:<port>`, started by the sandbox agent).
That listener is sandbox infrastructure, not the spike: the harness socket itself is bound to
`127.0.0.1` only (`ss -ltnp`). No preview server was started on `0.0.0.0`.

## 9. NOT VERIFIED

ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge
rendering, viewport fit at 1920 × 1080 / 1366 × 768 / 2560 × 1440, heap, DOM, long tasks,
end-to-end latency (Owner-local, pending) · Windows process
CPU / memory · Windows offline restore · Extended (4 h) stability · Production transport ·
Production Delta sizes · Production Historian write path and overflow policy · accessibility
level · any Production value.

## 10. Status

**React feasibility status: ARENA FEASIBILITY EVIDENCE COMPLETE WITH CONCERNS.** The concerns
are as follows:

1. The harness RSS trend over 10 minutes is not evaluated.
2. Owner-local browser evidence exists only for the superseded `dd20a8bd` map. The re-run,
   the Edge layout spec, and the manual 1920 × 1080 F11 UI re-review of the corrected map and
   the fullscreen layout are pending.
3. The synthetic Delta is a worst case close to full-Snapshot size.
4. `@types/react` / `@types/react-dom` require Owner confirmation as dependencies.
5. The Historian simulator defect found in S20 shows that the Production write path needs an
   explicit write-timeout rule (`[OPEN]`).

Stage 0.2.1A Sensor mapping corrected · fullscreen layout refined (Arena-supported validation
complete) · PR #3 OPEN, NOT MERGED · Owner-local 1920 × 1080 UI re-review
PENDING · 60-minute run PAUSED · React final selection NOT YET APPROVED · Blazor counter-spike
NOT AUTHORIZED · Stage 0.3 NOT AUTHORIZED · Production device access NOT AUTHORIZED.
