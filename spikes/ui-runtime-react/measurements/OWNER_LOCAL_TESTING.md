# Stage 0.2.1A — Owner-local test handoff (Windows 11, installed Microsoft Edge)

**Status:** PREPARED. The Agent has run none of it: Arena has no browser and no WebView.

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
