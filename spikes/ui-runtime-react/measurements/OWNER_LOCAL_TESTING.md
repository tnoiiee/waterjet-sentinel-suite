# Stage 0.2.1A — Owner-local test handoff (Windows 11, installed Microsoft Edge)

**Status:** PREPARED, NOT EXECUTED. Arena has no browser and no WebView; nothing in this
document has been run by the Agent. Playwright with `channel: 'msedge'` automates the installed
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

## 2. Manual look (optional)

```powershell
npm run harness            # serves dist/ and the harness on http://127.0.0.1:5181 (loopback)
```

Open `http://127.0.0.1:5181` in Edge. Press `D` for the Spike Diagnostics overlay. The scenario
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
