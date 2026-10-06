# Stage 0.2.1A — Owner-local test handoff (Windows 11, installed Microsoft Edge)

**Status:** PREPARED. The Agent has run none of it: Arena has no browser and no WebView.
**Latest Owner-local result:** Edge E2E 25 / 25 PASS at `4129687a`; the final UI punchlist re-run
(section 1D) is **PENDING**; the current required re-run is **section 1F** (critical Main Pump /
Mandatory Safe Return, 42 tests + manual F11 review).

**Owner-local evidence recorded for checkpoint `dd20a8bd` (superseded 104-location map):**
Windows 11, Node v24.20.0, npm 11.19.0, Git 2.55.0.windows.5, installed Microsoft Edge. `npm ci`,
`typecheck`, Vitest 53 / 53, production build, harness tests 14 / 14, and Edge E2E 15 / 15 were all
**PASS**. That evidence does **not** validate the corrected 106-location Sensor map. The
**re-run in section 1A is required**. The **60-minute Feasibility run (section 5) is PAUSED**
until the Owner visually accepts the corrected map. Playwright with `channel: 'msedge'` automates the installed
Edge browser. **It does not verify embedded WebView2 or the application-owned kiosk shell —
both remain NOT VERIFIED.**

All runs use synthetic data only. No device, plant network, SQL Server, or Production value is
involved.

## 0. Prerequisites

- Windows 11, Microsoft Edge (installed, stable channel), Node.js `>=22.22.2` (record the exact
  version; Node 22 or 24 LTS — see the Owner decision list).
- A clean checkout of the spike branch.
- Close other Edge windows and Node tools before measured runs (the process sampler groups all
  `msedge` and all `node` processes).

## 1. Restore and build

```powershell
cd spikes\ui-runtime-react\react-ui
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = '1'
npm ci --ignore-scripts --no-audit --no-fund      # or the offline procedure in OFFLINE_RESTORE.md
npm run typecheck
npm test                                         # Vitest (jsdom) + SSE transport tests
npm run build                                    # production Vite build -> dist/
cd ..\runtime-harness; npm test; cd ..\react-ui   # Node harness tests
node ..\measurements\environment-record.mjs --label owner-local
```

## 1A. Re-run after the Sensor-map correction (required)

Repeat section 1 (`npm ci`, `typecheck`, `npm test`, `npm run build`, harness tests) and
section 3 (Edge E2E, which includes the new `MAP` wall-map test). Then do a manual UI review
with `npm run harness` (section 2) and check:

| # | Check |
| --- | --- |
| 1 | U-shaped plan view retained: Rear on top, Left on the left, Right on the right, Front at the bottom, legend in the centre |
| 2 | 106 Sensor cells; centre summary reads `106 Sensors` and `2 Cannon slots · synthetic` |
| 3 | 2 Cannon slots, neutral grey-dashed circles, not clickable or selectable: Rear wall row 5 / third column (logical I7), Front wall row 5 / third column (logical I16) |
| 4 | Wall counts 24 / 29 / 24 / 29; each wall is 6 rows deep (Left 4 columns, Rear 5, Right 4, Front 5) |
| 5 | IDs: rows top to bottom `G+2xx`, `G+1xx`, `G`, `H`, `I`, `J`; logical columns increase left to right (Left 1–4, Rear 5–9, Right 10–13, Front 14–18) |
| 6 | No cell labelled `I7` or `I16` |
| 7 | Overlays on corrected IDs: quality showcase (`G+201`..`G+204`), queue badges, selection ring and detail panel position, Active Job outline, alarm border (`H7` in the soak cycle) |

Record the result for the Owner. Screenshots are allowed only if the Owner approves them as
public-safe.

## 1B. Re-run after the fullscreen layout refinement (required)

Applies to the fullscreen refinement checkpoint (fast-forward on `935973e6`; SHA in the PR #3
description). It also covers the section 1A checks. From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 75 / 75
npm run build
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 26 / 26
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts   # 16 functional + 5 layout tests
```

Then the **manual F11 visual re-review**: `npm run harness`, open `http://127.0.0.1:5181` in
Edge on a **1920 × 1080** display, press **F11**, browser zoom **100 %**.

| # | Owner check |
| --- | --- |
| 1 | Sensor cells visibly larger than at `dd20a8bd` |
| 2 | Sensor IDs readable |
| 3 | Sensor values readable |
| 4 | Wall titles readable |
| 5 | U shape retained (Rear top, Left left, Right right, Front bottom) |
| 6 | Cannon slots retained (Rear row 5 / third column = I7; Front row 5 / third column = I16) |
| 7 | Centre gap materially smaller |
| 8 | No excessive empty Sensor Map area |
| 9 | No page-level scrollbar (vertical or horizontal) |
| 10 | Complete Pressure Trend visible |
| 11 | Complete Camera placeholder visible |
| 12 | Eight GlobalQueue rows visible (use a Dirty scenario so at least 8 are queued) |
| 13 | Sensor Detail uses the card width effectively (two columns) |
| 14 | Diagnostics (`D` or the button) does not obscure critical content and closes |
| 15 | Existing overlays remain clear (quality, queue badge, selection, Active Job, alarm) |

**Screenshots required** (only if the Owner approves them as public-safe; otherwise keep them
local):

1. the full Operations page at the target;
2. a selected Sensor with Sensor Detail;
3. Diagnostics open;
4. the Rear and Front Cannon slots visible.

The **60-minute run stays PAUSED** until the Owner accepts the corrected visual layout.

## 1C. Re-run after the Operations readability refinement (required)

Applies to the readability refinement checkpoint (fast-forward on `ea23bc58`; SHA in the PR #3
description). It supersedes the test counts of section 1B and also covers the 1A / 1B checks.
From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 93 / 93 (13 files)
npm run build
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 26 / 26
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts   # 25 tests: 16 functional + LAYOUT-A..E + READ-A..D
```

`READ-A` checks, for all 106 cells, that the Sensor ID is not clipped and never touches the
quality marker, alarm icon, or queue badge. If it fails only on ID clipping, record whether the
**Bahnschrift** font is installed (Settings → Personalization → Fonts); it ships with Windows
10 / 11 and the cell ID zone relies on its semi-condensed width.

**Manual F11 review** — `npm run harness`, open `http://127.0.0.1:5181` in Edge on a
**1920 × 1080** display, **F11**, zoom **100 %**. Use the quality showcase and a Dirty scenario
so markers, badges, and eight queue rows are visible.

| # | Owner check |
| --- | --- |
| 1 | Long IDs (`G+205`, `G+105`, `J18`) fully readable; no marker, badge, or alarm icon over any ID |
| 2 | Cells and values larger and easier to read than at `ea23bc58` |
| 3 | Dirty / Cleaner calmer (less saturated); Not classified graphite with a faint hatch |
| 4 | Alarm (yellow border + triangle) clearly different from Dirty red, on Dirty and Cleaner cells |
| 5 | UNCERTAIN amber dot, BAD cross, STALE clock, DISABLED slashed circle — readable without colour |
| 6 | Selection (cyan ring) and Active Job (white double outline) distinguishable, also together with an alarm |
| 7 | Status bar: Alarm, Process, System, Technical groups; revision / config visually quieter |
| 8 | Sensor Detail: ID header, Process / Location / State / Source groups, clear emphasis |
| 9 | Active Job: Target, Water Jet, Isolation Valve, P1–P6 with completed / current / future, progress, elapsed |
| 10 | GlobalQueue: aligned numeric columns, calm status chips, no red rows |
| 11 | Pressure Trend: readable axes and legend, distinct lines, ready band and setpoint clear, header summary |
| 12 | Camera: title, synthetic placeholder badge, NO SIGNAL state — no longer looks empty |
| 13 | U shape, Cannon slots (I7 / I16), right-side structure, and bottom row unchanged; no page scrollbar |

**Controlled 15-minute observation** (after the review; host sleep and hibernate disabled for the
duration, Edge in the foreground, no other heavy load). Two PowerShell windows, as in section 4:

```powershell
# window A
$env:WJSS_SOAK_MINUTES = '15'; $env:WJSS_SOAK_LABEL = 'observe15'
npm run e2e:soak
# window B (start right after A)
powershell -ExecutionPolicy Bypass -File ..\measurements\sample-processes.ps1 -Minutes 16 -IntervalSeconds 5 -Label observe15
```

Summarise with `measurements/summarize.mjs` as in section 4. Return the soak hard gates
(section 7), connection state, reconnects, trend points, invariant violations, accepted second
Jobs, Historian rejections, and the summary percentiles. The earlier Owner-local **interrupted
overnight observation** of `ea23bc58` (≈ 11 h 41 min wall clock with host sleep, ≈ 4 h likely
active) is recorded in the results document; it is **not** a controlled benchmark. The
**60-minute run stays PAUSED** until the Owner authorises it.

## 1D. Re-run after the final Owner UI punchlist (required)

**Recorded baseline:** Owner-local Edge E2E **25 / 25 PASS** at `4129687a` (≈ 1.3 min). The punchlist
checkpoint (fast-forward on `4129687a`; SHA in the PR #3 description) supersedes the counts of
section 1C. From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 109 / 109 (15 files)
npm run build                          # emits one hashed GoogleSans-Latin-Variable-*.woff2
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 32 / 32
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts e2e/punchlist.spec.ts   # 34 tests
```

The Playwright web server now starts the harness with `--synthetic-test-controls` (needed by
`QUEUE-A` and `CTRL-A`..`CTRL-C`). New specs: `FONT-A` (Google Sans loaded via
`document.fonts`, used by app text, IDs, values, legend, controls; no reflow after load),
`CELL-A` (all 106 IDs unclipped; ID / marker / value / rail zones separate; value centred;
G+205 / G+218 / G+118 cell size), `IDENT-A`, `LEGEND-A`, `WJ-A`, `QUEUE-A`, `CTRL-A`..`CTRL-C`.
The font is bundled, so no Windows font installation matters any more (Bahnschrift is no longer used).

**Manual F11 review with the synthetic test controls** (presets and queue checks below are
**SUPERSEDED by section 1E**):

```powershell
npm run harness -- --synthetic-test-controls   # http://127.0.0.1:5181, loopback only
```

Edge, **1920 × 1080**, **F11**, zoom **100 %**. Select a Sensor on the map, press **D** (or the
Diagnostics button), and use **SYNTHETIC TEST CONTROL**. Presets 1–5 freeze automatic job starts;
preset **6 Reset** restores them. Without the flag the section reads "Off" and nothing can be sent.

| # | Owner check |
| --- | --- |
| 1 | All text in Google Sans (IDs, values, status bar, panels, legend, trend axes, buttons) |
| 2 | ID row full width; `G+205`, `G+218`, `G+118` fully readable; quality marker at the right end of the value row; value centred |
| 3 | Preset 1 / 2: alarm triangle (left) and queue badge (right) on a DIRTY and on a CLEANER cell, no overlap with ID, value, or marker |
| 4 | Preset 3 / 4: selection ring or Active Job outline together with alarm and queue / ACTIVE state |
| 5 | Preset 5: Cleared alarm with ACK REQUIRED and queue badge |
| 6 | Identity: "WaterJet Sentinel Suite" with the "OPERATIONS CONSOLE" subtitle, separate SYNTHETIC badge |
| 7 | Legend: eight entries, nothing clipped at the left edge; "Water Jet" entry |
| 8 | Water Jet slots read `WJ REAR` / `WJ FRONT`; summary "2 Water Jet reference slots · synthetic" |
| 9 | Mixed queue: run `queue-mixed-sources` (below) — first rows show TIME DUE, TEMP + TIME, OPERATOR, TEMP, DIRTY SCORE |

```powershell
# mixed-source queue (synthetic); token from runtime-harness\.run-token
$t = Get-Content ..\runtime-harness\.run-token
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:5181/api/spike/scenario -Headers @{ 'x-spike-token' = $t } -ContentType 'application/json' -Body '{"command":"queue-mixed-sources","params":{}}'
```

The **controlled 15-minute observation is PAUSED** (Owner decision). The **60-minute run was waived
as a gate** by the Owner and is not run.

## 1E. Re-run after the GlobalQueue semantics correction (required)

**Recorded baseline:** at `23f48daa`, 34 selected · 25 passed · 2 failed (`READ-A`, `WJ-A`) · 7 not run
(Owner-reported). This checkpoint (fast-forward on `23f48daa`; SHA in the PR #3 description)
supersedes the counts and presets of section 1D. From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 120 / 120 (16 files)
npm run build
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 40 / 40
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts e2e/punchlist.spec.ts   # 35 tests
```

Changed / new specs: `READ-A` unchanged (value-clipping fix is in CSS), `WJ-A` (Case A: no
selection; Case B: Sensor detail stays), `QUEUE-A` (bounded to 8), new `QUEUE-B` (heading, "n / 8
queued", no Status column, Active Job linked to its Position 1 dispatch), `CTRL-A` / `CTRL-B` (new
presets, head-only dispatch, no retarget).

**Manual F11 review** (`npm run harness -- --synthetic-test-controls`, Edge 1920 × 1080, F11, 100 %;
select a Sensor, press **D**, use **SYNTHETIC TEST CONTROL**):

| # | Owner check |
| --- | --- |
| 1 | G+205 (and every Sensor) value fully visible, centred; marker right of the value; ID unclipped; rail visible |
| 2 | GlobalQueue heading "GlobalQueue · synthetic"; count "n / 8 queued · FIFO · not Production scheduling"; never more than 8 rows, never 76 / 77; no Status column |
| 3 | Preset 1 Queued DIRTY: Sensor at Position 1, `Q` badge |
| 4 | Preset 2 Queued CLEANER: source reason OPERATOR (synthetic non-score source) |
| 5 | Preset 3 Selected queued: Sensor at Position 2 with the selection ring |
| 6 | Preset 4 Dispatched head: Active Job = the Sensor, `J` badge, Diagnostics "Position 1 · Sensor · Queue revision a→b · SYN-DSP-…" |
| 7 | Preset 5 Alarm on the Active Job Sensor: alarm triangle left, `J` badge right |
| 8 | Preset 6 Alarm Sensor not admitted: not in the queue; Diagnostics "QUEUE ELIGIBILITY DIAGNOSTICS" shows "NOT ADMITTED · Reason pending Owner-approved eligibility policy" |
| 9 | Preset 7 Head → Job: the former head becomes the Active Job; Position 2 moves to Position 1 |
| 10 | Preset 8 Reset: bounded queue (0–8), AutoSequence resumes, no second Job |
| 11 | Water Jet slots `WJ REAR` / `WJ FRONT` are not selectable and do not clear a Sensor selection |

The **controlled 15-minute observation is PAUSED** and the **60-minute run was waived as a gate**
(not run).

## 1F. Re-run after the critical Main Pump / Mandatory Safe Return checkpoint (required)

**SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED.** This checkpoint is a fast-forward on
`60cd0398`; its SHA is in the PR #3 description. It supersedes the counts of section 1E and adds
`e2e/critical.spec.ts`. From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 131 / 131 (17 files)
npm run build
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 51 / 51
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts e2e/punchlist.spec.ts e2e/critical.spec.ts   # 42 tests
```

New specs:

| Spec | What it checks |
| --- | --- |
| `CRIT-A` | Pump trip during an Active Job: modal immediately, live Safe Return, queue frozen, `CRITICAL_SUSPENDED` |
| `CRIT-B` | Unexpected stop with no Job: modal; no Safe Return required; no Job created |
| `CRIT-C` | One Acknowledge; the modal stays while the condition is active |
| `CRIT-D` | Condition cleared before Safe Return completes: the modal stays until Standby is confirmed |
| `CRIT-E` | Cleared + Safe Return complete + acknowledged → modal closes; still `CRITICAL_SUSPENDED`; no Resume control; no new Job |
| `CRIT-F` (1920 × 1080 and 1366 × 768) | Geometry inside the viewport, background blocked, focus trap, Escape ignored, no close button |

`layout.spec.ts` / `operations.spec.ts` now end Jobs with the scenario-only `abort-job { immediate: true }`.

**Manual F11 review.** Run `npm run harness -- --synthetic-test-controls` in Edge at 1920 × 1080,
F11, 100 %. Press **D** and use **SYNTHETIC TEST CONTROL → Critical Pump / Safe Return**. The ten
controls are:

1. stop with no Job;
2. trip with no Job;
3. trip in P1;
4. trip in P4;
5. normal completion → Safe Return;
6. valve feedback delay;
7. Standby feedback delay;
8. clear;
9. acknowledge;
10. reset (test only — **not a Resume**).

There is also a valve-feedback-absent control for failure review.

| # | Owner check |
| --- | --- |
| 1 | Control 4 (trip in P4): the modal "CRITICAL ALARM — MAIN PUMP TRIPPED" appears at once, centered and fully inside the screen. The background is dimmed and not clickable. There is no X; Acknowledge is the only button |
| 2 | The modal shows time, condition, acknowledge state, AutoSequence `CRITICAL_SUSPENDED`, Job / target / phase, the live Safe Return step, and valve / axis / Standby. The response text is generic |
| 3 | Escape does nothing; Tab stays inside the modal; there is no flashing |
| 4 | Safe Return progresses: valve close commanded → closed confirmed → **only then** axis to Standby → Standby confirmed. The Active Job panel shows the same step |
| 5 | GlobalQueue entries and order are unchanged during the event; no new Job starts |
| 6 | Control 9 (Acknowledge): state ACKNOWLEDGED; the modal stays open while the fault is active |
| 7 | Control 6, then 3, then 8 (clear) before Safe Return completes: the modal stays open until Standby is confirmed |
| 8 | Clear + Safe Return complete + acknowledged: the modal closes, the AutoSequence stays `CRITICAL_SUSPENDED`, there is no Resume button, and no next Job starts |
| 9 | Control 5 (normal completion): no modal; Safe Return runs; the outcome in Diagnostics → Sequence is `COMPLETED` only after Standby is confirmed |
| 10 | Modal colours are plum / orchid, distinct from dirty red, alarm amber, selection cyan and the white Job ring; state is readable as text |
| 11 | Control 10 (reset) leaves the AutoSequence `OFF` (not resumed) |

**Screenshots to return (8):**

1. modal during an Active Job;
2. Safe Return at the valve step;
3. Safe Return at the Standby step;
4. acknowledged with the fault still active;
5. cleared with Safe Return incomplete;
6. normal-completion Safe Return;
7. GlobalQueue unchanged during the event;
8. final Edge E2E summary.

Policies shown as OWNER DECISION REQUIRED are listed in
[`critical-pump-safe-return-decision-matrix.md`](../../../docs/spikes/critical-pump-safe-return-decision-matrix.md).

## 1G. Re-run after the final spike closeout hotfix (required — final Owner-local Edge gate)

**SYNTHETIC REVIEW TOOLING — NOT THE PRODUCTION OPERATOR-CONTROL MODEL.** This checkpoint is a
fast-forward on `81c87a44`; its SHA is in the PR #3 description. It supersedes the counts of
section 1F. From `react-ui\`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = "1"
npm ci --ignore-scripts --no-audit --no-fund
npm run typecheck
npm test                               # Vitest: expect 138 / 138 (18 files)
npm run build
node --test "../runtime-harness/test/**/*.test.mjs"   # expect 60 / 60
npm run e2e -- e2e/operations.spec.ts e2e/layout.spec.ts e2e/punchlist.spec.ts e2e/critical.spec.ts   # 48 tests
```

Changed / new specs (same four files; the command is unchanged):

| Spec | What it checks |
| --- | --- |
| `S11/S12` (operations) | **Deterministic.** The Sensor's Dirty Score is fixed and held (`set-sensor-score { hold: true }`, Score 82); the test waits for that revision, then Raise Alarm / Clear / Acknowledge. Score, classification and the computed background must be **identical** (no RGB tolerance); marker and `ACTIVE_UNACK` / `CLEARED_UNACK` change only the alarm channel. Fixes the 81c87a44 Edge failure (score drifted between samples) |
| `SEQ-B` (critical) | START AUTOSEQUENCE dispatches GlobalQueue Position 1 even with another Sensor selected; the queue shifts FIFO |
| `SEQ-C` | PAUSE AFTER CURRENT JOB → `PAUSE_REQUESTED`; the Job continues through Safe Return; no next dispatch; `PAUSED` |
| `SEQ-D` | RESUME only from `PAUSED`; dispatches the current head; disabled in `CRITICAL_SUSPENDED` |
| `SEQ-E` | Clear + Acknowledge do not enable Resume; RESET disabled until Safe Return completes; RESET → `OFF`, no Job; explicit START needed |
| `SEQ-F` | ABORT ACTIVE JOB: only with a Job; Safe Return steps visible; the Job is not released early; no next Job when paused |
| `SEQ-G` | Controls only in Diagnostics; `SYN · ` labels; readable disabled reasons; no page overflow; keyboard activation; critical modal unchanged |

**Manual F11 sequence (18 steps).** Run `npm run harness -- --synthetic-test-controls` in Edge at
1920 × 1080, F11, 100 %. Press **D**; use **SYNTHETIC TEST CONTROL → Synthetic AutoSequence**.
Every button shows its disabled reason as text underneath.

| # | Action | Expected |
| --- | --- | --- |
| 1 | **SYN · RESET CRITICAL SCENARIO** if enabled, otherwise **Visual presets → Reset** | Mode `OFF · OFF`; no Active Job |
| 2 | Note the GlobalQueue head (Position 1) and select a *different* Sensor on the map | Selection does not change the head |
| 3 | **SYN · START AUTOSEQUENCE** | Mode `RUNNING`; Job target = the noted head (not the selected Sensor) |
| 4 | **SYN · PAUSE AFTER CURRENT JOB** | Mode `PAUSE_REQUESTED`; the Job keeps running; RESUME disabled with reason |
| 5 | Wait for the Job to finish | Safe Return runs (valve close → confirmed → axis Standby → confirmed) |
| 6 | Observe | Mode `PAUSED · PAUSED`; no next Job; the head is unchanged |
| 7 | **SYN · RESUME AUTOSEQUENCE** | Mode `RUNNING`; the new head starts |
| 8 | **SYN · ABORT ACTIVE JOB** | `ABORTING`, then the Safe Return steps; the Job stays Active until Standby is confirmed |
| 9 | After Safe Return | Outcome `ABORTED`; with mode `RUNNING` a later head may start (synthetic rule — never in the release revision) |
| 10 | **Critical Pump / Safe Return → trip in P4** (with a Job running) | Critical modal at once; `CRITICAL_SUSPENDED`; Safe Return runs |
| 11 | Acknowledge in the modal; **clear** the pump condition | Critical `CLEARED · ACKNOWLEDGED`; RESUME and START disabled |
| 12 | Before Standby is confirmed | RESET disabled: "Mandatory Safe Return not complete" |
| 13 | After Safe Return completes | The modal closes; mode still `CRITICAL_SUSPENDED` (no automatic Resume) |
| 14 | **SYN · RESET CRITICAL SCENARIO** | Mode `OFF · OFF`; queue order unchanged |
| 15 | Wait 5 s | No Job is created; the Pump is not started (START reason "Pump not ready") |
| 16 | **SYN · START PUMP (synthetic signal)** | Pump ready after ~3 s |
| 17 | **SYN · START AUTOSEQUENCE** | Mode `RUNNING` |
| 18 | Observe | The GlobalQueue head (Position 1) becomes the Active Job target |

**Screenshots to return (10):**

1. Diagnostics with the Synthetic AutoSequence group and disabled reasons (mode `OFF`);
2. after START (Job = head, another Sensor selected);
3. `PAUSE_REQUESTED` with the Job running;
4. `PAUSED` with no Job;
5. ABORT ACTIVE JOB during Safe Return;
6. critical modal with the drawer open;
7. cleared + acknowledged, still `CRITICAL_SUSPENDED`, RESET disabled reason;
8. after RESET (`OFF`, no Job);
9. after START (head started);
10. final Edge E2E summary (48 tests).

Abort re-queue, Production Pause / Resume, Safe Return failure and reset authority are
**OWNER DECISION REQUIRED** (see the
[critical Pump / Safe Return matrix](../../../docs/spikes/critical-pump-safe-return-decision-matrix.md)).

## 2. Manual look (optional)

```powershell
npm run harness            # serves dist/ and the harness on http://127.0.0.1:5181 (loopback)
```

Open `http://127.0.0.1:5181` in Edge. Press `D` (or the Diagnostics button) for the floating Spike Diagnostics drawer; close it with its Close button or `D`. The scenario
token is in `runtime-harness\.run-token` (git-ignored) for manual `POST /api/spike/scenario` calls.

## 3. Functional scenarios (Edge)

```powershell
npm run e2e -- e2e/operations.spec.ts
```

Covers S01–S28 browser-observable parts (selection, quality rendering, alarm independence,
reconnect, revision gap, Historian slowdown, bounded trend, loopback-only requests, close guard,
viewport resize, configuration revision, refused second Job).

## 4. Smoke — 10 minutes (run first)

Two PowerShell windows:

```powershell
# window A
$env:WJSS_SOAK_MINUTES = '10'; $env:WJSS_SOAK_LABEL = 'smoke'
npm run e2e:soak
# window B (start right after A)
powershell -ExecutionPolicy Bypass -File ..\measurements\sample-processes.ps1 -Minutes 11 -IntervalSeconds 5 -Label smoke
```

Then summarise (raw stays outside Git):

```powershell
node ..\measurements\summarize.mjs ..\results\raw\smoke-<timestamp> --processes ..\results\raw\processes-smoke-<timestamp>.csv
```

**After the Smoke run, the Owner sets the exploratory thresholds** (for example heap growth,
DOM-node growth, Delta lag p95, long-task budget, CPU) from the observed baseline. No threshold
is invented in advance.

## 5. Feasibility — 60 minutes

**PAUSED** until the Owner visually accepts the corrected Sensor map (section 1A).

Same as Smoke with `WJSS_SOAK_MINUTES=60`, `WJSS_SOAK_LABEL=feasibility`, sampler `-Minutes 61`.

## 6. Extended — 4 hours (optional)

Same with `WJSS_SOAK_MINUTES=240`, `WJSS_SOAK_LABEL=extended`, sampler `-Minutes 241`. If not
run: **Extended stability: NOT VERIFIED.**

## 7. Hard gates asserted by the soak spec

Page not crashed; connection `LIVE` at the end; harness invariant violations = 0; accepted
second Jobs = 0; trend points ≤ capacity. Everything else is reported as p50 / p95 / max for
Owner evaluation.

## 8. What to return

Commit only `results/summary/owner-local-<label>.{json,md}` and
`results/environment/owner-local-environment.json`, then regenerate the manifest
(`node measurements/manifest.mjs`). Keep raw JSONL / CSV / Playwright output in Owner storage
or SharePoint. Screenshots only if the Owner explicitly approves them as public-safe.
