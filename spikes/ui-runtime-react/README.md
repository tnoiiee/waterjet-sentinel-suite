# Stage 0.2.1A — React UI and Runtime Feasibility Spike (synthetic)

> **SYNTHETIC FEASIBILITY SPIKE — NOT PRODUCTION CODE.** React is the primary feasibility
> candidate, **not** the final UI framework (`[OPEN]`). Blazor Hybrid is **deferred and not
> authorized** (no files). The Production direction — .NET Equipment Runtime Windows Service,
> ASP.NET Core Local Application API, application-owned Windows kiosk shell — is unchanged.

Plan: [`../../docs/spikes/stage-0.2.1a-plan.md`](../../docs/spikes/stage-0.2.1a-plan.md) ·
Results: [`../../docs/spikes/stage-0.2.1a-results.md`](../../docs/spikes/stage-0.2.1a-results.md)

## What is here

| Path | Contents |
| --- | --- |
| [`contracts/`](contracts/CONTRACTS.md) | Snapshot / Delta contract types, the single classification module, structural validator, golden fixtures |
| `runtime-harness/` | Node synthetic runtime harness: built-ins only (`node:http`, `node:events`, `node:perf_hooks`, …), **no dependencies**, binds to `127.0.0.1` only |
| `react-ui/` | React 19 + TypeScript 6 + Vite 8 Operations page (one page), uPlot trend, Vitest (jsdom) tests, Playwright specs for Owner-local Edge runs |
| `scenario-runner/` | 28-scenario runner and Arena measurement driver (Node built-ins only) |
| `measurements/` | Environment record, licence inventory, SHA-256 manifest, Owner-local summariser and Windows process sampler, [offline restore](measurements/OFFLINE_RESTORE.md) and [Owner-local testing](measurements/OWNER_LOCAL_TESTING.md) guides |
| `results/` | `environment/`, `summary/`, `manifests/` (committed); `raw/` (git-ignored) |

## Architecture (spike)

```text
Synthetic device scheduler (10 sessions, serialized per device, 4 concurrent across devices)
  -> acquisition simulation (Modbus-like poll plan, latency, timeout injection)
  -> authoritative in-memory state (one monotonic revision)
  -> Snapshot / Delta publisher (1 s)
  -> SSE on 127.0.0.1 (spike transport only; Production transport [OPEN])
  -> React presentation store (Map by Sensor ID, per-Sensor subscriptions, useSyncExternalStore)
  -> partial Sensor and panel updates (React.memo)
```

## Run (Arena or any machine with Node >= 22.22.2)

```bash
cd spikes/ui-runtime-react/runtime-harness && npm test          # harness tests (node:test)
cd ../react-ui
PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 npm ci --ignore-scripts
npm run typecheck && npm test && npm run build
npm run harness                                                  # http://127.0.0.1:5181
cd .. && node scenario-runner/run-scenarios.mjs                  # 28 scenarios (runtime / SSE level)
node scenario-runner/arena-measure.mjs --minutes 10              # harness measurement
```

## Explicitly NOT VERIFIED by this spike

- ASP.NET Core integration — **NOT VERIFIED**
- Windows Service behaviour — **NOT VERIFIED**
- WebView2 kiosk behaviour — **NOT VERIFIED**
- Browser rendering performance — Owner-local (installed Edge); not measured in Arena
- Windows offline restore — Owner-local
- Extended (4 h) stability — **NOT VERIFIED** unless the Owner runs it

## Synthetic values

Every timing, threshold, capacity, pressure, and quality parameter in this directory is a
**SYNTHETIC SPIKE PARAMETER — NOT A PRODUCTION VALUE**. Sensor IDs are `SYN-*`. No Production
Sensor ID, address, register map, setpoint, threshold, or timeout appears here. uPlot is approved
for this spike only; the Production chart library remains `[OPEN]`. SSE is used for this spike
only. The close guard is an operational usability control, not a safety protection.

## Removal

Deleting `spikes/ui-runtime-react/` and `docs/spikes/` removes the spike completely; no
Product directory depends on it.
