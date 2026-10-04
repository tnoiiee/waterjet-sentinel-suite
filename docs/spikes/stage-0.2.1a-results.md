# Stage 0.2.1A — React UI and Runtime Feasibility Spike: Results

**Document status:** Evidence record for the Stage 0.2.1A development checkpoint. **Arena
evidence only.** Owner-local testing (Windows 11, installed Microsoft Edge) is **PENDING**. This
document does **not** select React as the final UI framework; the UI framework, Production
transport, and Production chart library remain `[OPEN]`. Blazor counter-spike: **DEFERRED /
NOT AUTHORIZED**.

Plan: [`stage-0.2.1a-plan.md`](stage-0.2.1a-plan.md) · Spike:
[`../../spikes/ui-runtime-react/README.md`](../../spikes/ui-runtime-react/README.md)

All values are produced by synthetic tooling with **SYNTHETIC SPIKE PARAMETERS — NOT
PRODUCTION VALUES**.

---

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

10-minute harness run, 2 SSE clients, 10 s sampling, scenario command every 30 s —
[`arena-harness-smoke-10min.md`](../../spikes/ui-runtime-react/results/summary/arena-harness-smoke-10min.md).

| Harness metric | p50 / p95 / max (unless stated) |
| --- | --- |
| Harness process CPU % (one Node process, 2-core VM) | 0.82 / 1.18 / 1.43 |
| Harness RSS MB | 69 / 76 / 76.3 — first-third mean 66.1 → last-third mean 73.5 |
| JS heap used MB (p50 / max) | 11.2 / 16.1 |
| Event-loop delay p99 ms | 10.8 (at the default 10 ms histogram resolution of `monitorEventLoopDelay`) |
| Snapshot bytes (p50 / max) | 50,422 / 80,318 |
| Delta bytes | 50,188 / 50,233 / 51,173 |
| Delta interval ms | 1000 / 1001 / 1045 |
| Delta lag, generated → received by a Node client on the same host, ms | 2 / 3 / 6 — **not** a browser or end-to-end UI latency |
| Delta rate | 1.031 / s |
| Revision gaps injected → re-snapshots (per client) | 1 → 1 |
| Publish duration ms | 0.84 / 1.383 / 4.948 |
| Max in-flight requests, global / per device | 4 / 1 (bound 4; no overlap within a device) |
| FAST poll latency ms | 22.28 / 38.41 / 751.03 (max = injected 750 ms timeout) |
| Historian max depth / rejected | 10,950 / 0 (capacity 50,000) |
| Jobs completed / accepted second Jobs | 21 / 0 |
| Pump-stop command handling inside the harness | 0.129 ms (n = 1) — not end-to-end |
| Invariant violations | 0 |

**RSS concern:** RSS rose by about 7 MB between the first and last thirds of a 10-minute run
while heap stayed bounded (max 16.1 MB). Ten minutes cannot separate warm-up from growth; the
trend is **not evaluated** and must be checked in the Owner-local 60-minute run.

| Build / bundle | Value |
| --- | --- |
| JS bundle | 301,821 B raw · 100.79 kB gzip (Vite) · 99,337 B `gzip -9` |
| CSS | 10,371 B raw · 3.29 kB gzip |
| `dist/` total | 312,815 B |
| Build time | 226 ms (Vite) |
| Offline `npm ci` | 2.0 s, exit 0 |

**Delta size note:** the synthetic acquisition changes every Sensor value every second, so the
Delta (~44 to 51 kB) is close to a full Snapshot (~50 kB). This is a deliberate worst case for
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

Prepared, not executed: [`OWNER_LOCAL_TESTING.md`](../../spikes/ui-runtime-react/measurements/OWNER_LOCAL_TESTING.md)
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
rendering, heap, DOM, long tasks, end-to-end latency (Owner-local, pending) · Windows process
CPU / memory · Windows offline restore · Extended (4 h) stability · Production transport ·
Production Delta sizes · Production Historian write path and overflow policy · accessibility
level · any Production value.

## 10. Status

**React feasibility status: ARENA FEASIBILITY EVIDENCE COMPLETE WITH CONCERNS.** The concerns
are: (1) the harness RSS trend over 10 minutes is not evaluated; (2) all browser, Edge, WebView2,
Windows, and long-run UI evidence is pending Owner-local testing; (3) the synthetic Delta is a
worst case close to full-Snapshot size; (4) `@types/react` / `@types/react-dom` require Owner
confirmation as dependencies; (5) the Historian simulator defect found in S20 shows that the
Production write path needs an explicit write-timeout rule (`[OPEN]`).

Stage 0.2.1A checkpoint submitted · Owner-local testing PENDING · Owner manual review PENDING ·
React final selection NOT YET APPROVED · Blazor counter-spike NOT AUTHORIZED · NOT MERGED ·
Stage 0.3 NOT AUTHORIZED · Production device access NOT AUTHORIZED.
