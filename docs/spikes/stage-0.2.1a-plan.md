# Stage 0.2.1A — React UI and Runtime Feasibility Spike: Plan

**Document status:** `[APPROVED]` as the Stage 0.2.1A Coding Start scope, as amended by the
Owner Coding Start Gate. This plan is a **feasibility spike plan**, not a Production design.
It does not select the final UI framework, the Production transport, or the Production chart
library.

| Item | Value |
| --- | --- |
| Stage | 0.2.1A — React UI and Runtime Feasibility Spike |
| Scope Gate | **APPROVED** |
| Coding Start Gate | **APPROVED** |
| Implementation | **IN PROGRESS** until a development checkpoint is submitted for review |
| Approved base | `e779f8ad2c856e367fd65985007a3da411bd0e73` (remote `main`) |
| Working branch | `arena/01a108d8-waterjet-sentinel-suite` |
| React | Primary feasibility candidate — **not** the final selection |
| Blazor Hybrid | **DEFERRED AND NOT AUTHORIZED** — no files, no counter-spike without a separate Owner gate |
| Final UI framework | `[OPEN]` |
| Production transport | `[OPEN]` |
| Production chart library | `[OPEN]` |

Results are recorded separately in [`stage-0.2.1a-results.md`](stage-0.2.1a-results.md).

---

## 1. Purpose

Show, with evidence, whether a React + TypeScript + Vite Operations page can present the
documented UI workload (104 Sensor locations folded from 208 Thermocouple channels, one active
Cleaning Job, GlobalQueue preview, pressure trend, alarms, communication health) at a one-second
cadence, fed by an authoritative runtime through a Snapshot-plus-Delta stream, without
full-page re-rendering, unbounded growth, or state inference after reconnect.

## 2. Owner amendments at the Coding Start Gate

The Pre-implementation Report proposed an ASP.NET Core loopback harness and a WebSocket
transport. The Owner Coding Start Gate replaced these for the spike:

| Topic | Pre-implementation proposal | Approved for the spike |
| --- | --- | --- |
| Synthetic runtime harness | ASP.NET Core (.NET 10) console host | **Node built-in HTTP server** (`node:http`), no runtime dependency |
| Runtime-to-UI delivery | Native WebSocket | **Server-Sent Events** (Snapshot then Delta) |
| Commands | WebSocket / HTTP | **HTTP POST** synthetic scenario commands |
| Trend | uPlot | **uPlot 1.6.32** (spike only) |
| UNCERTAIN rule | Proposed classification plus marker | **Approved** last-validated rule (section 7) |

Consequences that must remain explicit:

- ASP.NET Core integration: **NOT VERIFIED**.
- Windows Service behaviour: **NOT VERIFIED**.
- WebView2 kiosk behaviour: **NOT VERIFIED**.
- SSE is used for the spike only and is **not** the selected Production transport.
- The Node harness is synthetic spike infrastructure only. It does not replace the approved
  Production direction (.NET Equipment Runtime Windows Service, ASP.NET Core Local Application
  API, application-owned Windows kiosk shell).

## 3. Directory boundary

```text
spikes/ui-runtime-react/
  README.md               spike entry point, run instructions, NOT VERIFIED list
  .gitignore              local ignores (raw results, node_modules, dist, caches, run tokens)
  contracts/              CONTRACTS.md, shared TypeScript contract types, shared classify and
                          validate modules, golden fixtures
  runtime-harness/        Node synthetic runtime harness (no dependencies, node:test tests)
  react-ui/               React + TypeScript + Vite Operations page, Vitest, Playwright specs
  scenario-runner/        Node scenario runner and Arena measurement driver (built-ins only)
  measurements/           environment record, summariser, licence inventory, manifest,
                          Owner-local process sampler, offline-restore and Owner-local guides
  results/                raw/ (ignored), environment/, summary/, manifests/
docs/spikes/
  stage-0.2.1a-plan.md     this plan
  stage-0.2.1a-results.md  results, measurements, NOT VERIFIED list
```

Nothing is created under `apps/`, `packages/`, `adapters/`, `config/`, or any Production
solution directory. Deleting `spikes/ui-runtime-react/` and `docs/spikes/` removes the spike
completely without changing Product architecture.

## 4. Process model

```text
Synthetic Device Scheduler (10 sessions, 1 serialized queue each, cross-device semaphore)
  -> Acquisition simulation (Modbus-like poll plan, latency, timeout injection)
  -> Authoritative in-memory operational state (single monotonic revision)
  -> Snapshot / Delta publisher (1 s cadence, plus immediate publish after commands)
  -> Loopback SSE stream (127.0.0.1 only)
  -> React presentation store (Map keyed by Sensor ID, per-Sensor subscriptions)
  -> Partial Sensor and panel updates (React.memo, useSyncExternalStore)
```

Secondary paths:

| Path | Behaviour |
| --- | --- |
| Device timeout | Request exceeds the synthetic timeout; device session marks timeout; quality degrades UNCERTAIN then BAD; other device queues continue |
| Device recovery | First successful poll restores ONLINE and GOOD quality |
| Historian slowdown | Acquisition enqueues to a bounded channel; the consumer is asynchronous and never awaited by acquisition or publishing |
| UI disconnect | Presentation marked DISCONNECTED; last received data shown with a stale banner; no inference, no command queue |
| UI reconnect | Server always sends a fresh Snapshot first; the store is replaced entirely |
| Revision gap | UI discards incremental application, closes the stream and reopens it to obtain a fresh Snapshot |
| Slow client | If a client's socket buffer exceeds a bound, the server closes it; the client reconnects and receives a Snapshot |
| Measurement capture | Harness metrics endpoint; scenario runner; Owner-local Playwright soak sampler and process sampler |

## 5. Contracts

Contract types are defined once in `spikes/ui-runtime-react/contracts/operational.ts` and
documented in `contracts/CONTRACTS.md`. All are synthetic-only, Runtime-owned, and
serialised Runtime → UI except `SyntheticScenarioCommand` (UI/runner → Runtime).

| Contract | Ownership | Delivery |
| --- | --- | --- |
| OperationalSnapshot | Runtime | Full state; first message on every connection |
| OperationalDelta | Runtime | `previousRevision`, `revision`, changed records and changed singletons only |
| SensorPresentationState | Runtime (authoritative classification) | Whole-record replacement per changed Sensor |
| WallSummary | Runtime | Snapshot; Delta when changed |
| ActiveCleaningJobState | Runtime | Singleton; `null` when no job |
| PumpState | Runtime | Singleton |
| QueueSummary | Runtime | Singleton; at most 8 preview entries |
| AlarmSummary | Runtime | Singleton; bounded item list |
| CommunicationHealth | Runtime | Singleton; one entry per device |
| RuntimeHealth | Runtime | Singleton |
| PublishedConfigurationRevision | Runtime (immutable revision) | Snapshot; Delta when a new revision is published |
| TrendWindow / TrendPoint | Runtime | Bounded window in Snapshot; one point per Delta |
| SyntheticScenarioCommand | UI / runner | HTTP POST, token protected, never replayed |
| CloseRequestEvaluation | Runtime | HTTP POST response |
| MeasurementSample | Measurement tooling | Raw results only (not committed) |

Selection is UI-local and is never sent on the wire.

## 6. Synthetic model

All values below are **SYNTHETIC SPIKE PARAMETER — NOT A PRODUCTION VALUE**.

| Item | Value |
| --- | --- |
| Devices | `SYN-TC-01`..`SYN-TC-08` (26 Thermocouple channels each), `SYN-PIO-01` (pump, valves, jets), `SYN-STS-01` (motion-controller status) |
| Sensors | 104: `SYN-LEFT-01..24`, `SYN-REAR-01..28`, `SYN-RIGHT-01..24`, `SYN-FRONT-01..28` |
| Thermocouple channels | 208 (two per Sensor), folded to 104 presentation records in the Runtime |
| Water Jets / Isolation Valves | 8 / 8 (`SYN-JET-1..8`, `SYN-VLV-1..8`, one-to-one) |
| Motion controllers | 4 (`SYN-MC-1..4`) |
| Active Cleaning Jobs | at most 1 |
| Seed | 2101 |
| Poll groups | Fast 1 s, Medium 5 s, Slow 30 s |
| Cross-device concurrency | 4 |
| Request timeout | 750 ms |
| Device latency | 5–40 ms |
| Stale threshold | 5 s |
| Dirty threshold | 50 (from the Synthetic Published Configuration) |
| Historian channel | 50,000 samples, batch 500, near-overflow warning 80 % |
| Job phase duration | 4 s per phase, phases P1–P6 |

Poll-plan entries carry `deviceId`, `functionCategory`, `startAddress`, `quantity`,
`decodeInstructions`, `pollGroup`, `updateTargets`. Addresses are synthetic offsets only.

## 7. Classification and quality

- Dirty Score > threshold → DIRTY; Dirty Score ≤ threshold → CLEANER.
- GOOD: classify the current value; store it as last validated.
- UNCERTAIN: keep the last validated classification, show an amber quality marker; new
  uncertain values never change classification; with no last validated value use
  NOT_CLASSIFIED; the detail view states that the last validated classification is shown.
- BAD, STALE, DISABLED: NOT_CLASSIFIED, neutral gray presentation, never the Dirty or Cleaner
  background.
- Dirty red is a process-condition colour, never an alarm severity colour. Alarm uses a
  separate bright border and icon.
- Classification is implemented once (`contracts/classify.mjs`); visual mapping is
  implemented once (`react-ui/src/visual/toCellVisual.ts`).

## 8. Scenarios

Initial Snapshot; one-second Deltas; 30 % Dirty; 70 % Dirty; threshold oscillation around 50;
all quality states; selection during updates; queue badge changes; GlobalQueue changes; single
Job progression; Active Alarm; Cleared Ack Required; device timeout; other devices continue;
device recovery; UI disconnect; UI reconnect; authoritative re-snapshot; Historian slowdown;
near-overflow behaviour; bounded Trend; camera placeholder; close guard for Active Job; close
guard for Pump running; viewport resize; synthetic configuration revision; refused second Job;
revision-gap re-snapshot. Runtime-observable scenarios run in Arena through the scenario
runner; browser-observable scenarios are prepared as Playwright specs for Owner-local runs.

## 9. Measurements

| Where | What |
| --- | --- |
| Arena | Harness CPU and memory, event-loop delay, Vite build time, bundle size, dependency count, offline restore result, Snapshot and Delta size, message rate, poll latency, device scheduling, Historian queue metrics |
| Owner-local (Windows 11, installed Edge) | Browser rendering, heap, DOM node count, long tasks, end-to-end Delta lag, Windows process CPU and memory; 10 min Smoke, 60 min Feasibility, optional 4 h Extended |

Arena never claims Browser, Edge, WebView2, Windows-process, kiosk, end-to-end latency, or
long-running UI stability results.

## 10. Raw result handling

Raw results stay outside Git (`results/raw/` is ignored; Owner-local raw files are kept in
Owner local storage or SharePoint). Committed results are limited to: environment record,
summary JSON, summary Markdown, SHA-256 manifest, SBOM, licence inventory, validation summary.
No ZIP is created.

## 11. Outcome classification

React feasibility status is reported with exactly one of: ARENA FEASIBILITY EVIDENCE COMPLETE;
ARENA FEASIBILITY EVIDENCE COMPLETE WITH CONCERNS; ARENA FEASIBILITY EVIDENCE INCOMPLETE;
MATERIAL CONCERN FOUND. None of these selects React as the final framework. Any Blazor
counter-spike requires a separate Owner gate.
