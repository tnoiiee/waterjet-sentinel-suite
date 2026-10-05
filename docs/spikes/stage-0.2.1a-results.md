# Stage 0.2.1A — React UI and Runtime Feasibility Spike: Results

**Document status:** Evidence record for Stage 0.2.1A. **Arena evidence**, plus the Owner-local
evidence the Owner reported for checkpoint `dd20a8bd`. That Owner-local evidence covers the
superseded 104-location map and does **not** validate the corrected 106-location map, so the
Owner-local re-run and manual UI re-review are **PENDING** and the 60-minute run is **PAUSED**. This
document does **not** select React as the final UI framework; the UI framework, Production
transport, and Production chart library remain `[OPEN]`. Blazor counter-spike: **DEFERRED /
NOT AUTHORIZED**.

Plan: [`stage-0.2.1a-plan.md`](stage-0.2.1a-plan.md) · Spike:
[`../../spikes/ui-runtime-react/README.md`](../../spikes/ui-runtime-react/README.md)

All values are produced by synthetic tooling with **SYNTHETIC SPIKE PARAMETERS — NOT
PRODUCTION VALUES**.

---

## 0. Sensor-map correction checkpoint (current)

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

**Required re-run for the corrected map:** typecheck, Vitest, build, harness tests, Edge E2E
(now 16 functional tests including `MAP`), and a manual UI review covering:

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
rendering, heap, DOM, long tasks, end-to-end latency (Owner-local, pending) · Windows process
CPU / memory · Windows offline restore · Extended (4 h) stability · Production transport ·
Production Delta sizes · Production Historian write path and overflow policy · accessibility
level · any Production value.

## 10. Status

**React feasibility status: ARENA FEASIBILITY EVIDENCE COMPLETE WITH CONCERNS.** The concerns
are as follows:

1. The harness RSS trend over 10 minutes is not evaluated.
2. Owner-local browser evidence exists only for the superseded `dd20a8bd` map. The re-run and
   manual UI re-review of the corrected map are pending.
3. The synthetic Delta is a worst case close to full-Snapshot size.
4. `@types/react` / `@types/react-dom` require Owner confirmation as dependencies.
5. The Historian simulator defect found in S20 shows that the Production write path needs an
   explicit write-timeout rule (`[OPEN]`).

Stage 0.2.1A Sensor mapping corrected · PR #3 OPEN, NOT MERGED · Owner-local UI re-review
PENDING · 60-minute run PAUSED · React final selection NOT YET APPROVED · Blazor counter-spike
NOT AUTHORIZED · Stage 0.3 NOT AUTHORIZED · Production device access NOT AUTHORIZED.
