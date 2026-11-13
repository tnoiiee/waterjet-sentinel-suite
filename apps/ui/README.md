# apps/ui — Operator presentation (Stage 0.4C-K4)

The fullscreen Operator screen for a WJSS cell: the 108-Sensor wall map, the selected Sensor,
the thermal block, the Dispatch Workspace and the operational blocks. It is **browser-native**
(one document, one stylesheet, seven ES modules) and it is **served by `Wjss.Runtime` itself**.
There is no build step, no package manager, no bundler and no transpiler anywhere in this
workspace: what is authored here is what the browser reads, byte for byte.

## The shipped set (10 files)

| Path | Role |
| --- | --- |
| `public/index.html` | The document: semantic structure, mount points, one `<script type="module">`. No inline script, no inline style. |
| `public/global.css` | The only design-token location. Dark, high-contrast, 1920×1080 first, `overflow-x: hidden`, one narrow-width reflow breakpoint, a density switch. |
| `public/app.mjs` | Boot, repaint cadence, selection and keyboard, density toggle, offline banners, `document.body.dataset.appState` lifecycle (`booting` / `ready` / `failed`). |
| `public/feed.mjs` | The whole network surface: fetch loop, revision-gap detection, reconnect, bounded delta row list. Nothing else in the UI may call `fetch`. |
| `public/wallMap.mjs` | The authoritative U-shape geometry and `buildModel` (it refuses a Snapshot that is not 108 cells). |
| `public/sensorDetail.mjs` | Selected-Sensor facts and the trend canvas (a `null` sample is drawn as a gap, never as a line to zero). |
| `public/thermal.mjs` | The thermal block and the Front/Rear availability statement. |
| `public/dispatchPanel.mjs` | Global queue, frozen countdown and frozen decision, 13-stage timeline, live `TimeDelay` preview. |
| `public/opsPanel.mjs` | Active Job, Pump, Alarms, the Critical Alarm modal and the status chips. |
| `test/ui.contract.test.mjs` | The gate: 18 tests over inventory, module graph, geometry, cadence, contract field names and the read-only surface. |

## Two roots, one copy

* authored root: `apps/ui/public/` — the files under test;
* served root: `Ui/` **beside the host binary**, produced by the single
  `<Content Include="..\..\ui\public\**\*.*" LinkBase="Ui" CopyToOutputDirectory="PreserveNewest" />`
  item in `apps/runtime/Wjss.Runtime.csproj`.

That item is the only "build" the presentation has: MSBuild copies the directory into the output.
It introduces no `PackageReference`, so `packages.lock.json` is untouched and a locked restore stays
honest. A host build whose output lacks `Ui/index.html` answers 500 with a plain sentence rather than
serving a stale or partial tree.

`packages/contracts/ApiRoutes.cs` carries the route identities: `Ui = "/"`, `UiAsset = "/ui/{file}"`
and `KioskHealth = "/api/health"`. The asset handler matches the route value against a frozen
whitelist of the eight names below and answers 404 **before** composing any path, so a request can
never contribute a path segment and a nested or traversed name is the same 404 as an unknown one.
The document lives at the origin root because the Kiosk shell navigates to the approved origin
itself: presentation and API stay same-origin, which is the point of hosting it here.

## Module graph

Every import inside `public/` is **relative** (`from './feed.mjs'`), never root-absolute. The browser
resolves a relative specifier against the URL of the importing module (`/ui/app.mjs` + `./feed.mjs`
= `/ui/feed.mjs`), and Node resolves it against the file on disk — so the same graph is reachable
both from the served route and from `node --test`, which is what makes this directory testable at
all. `app.mjs` is the single entry; every other module is reachable from it.

## Read-only boundary

* Five routes, all `GET`, all on the host origin: `/api/v1/snapshot`, `/api/v1/deltas`,
  `/api/v1/runtime`, `/api/v1/dispatch-workspace`, `/api/health`. There is no form, no button that
  posts, no `method:` other than `GET`, no `POST`/`PUT`/`PATCH`/`DELETE` constant to call.
* Cadence: `POLL_INTERVAL_MS = 1000` for the snapshot and delta feed, `HEALTH_EVERY_TICKS = 10` for
  the slower host-identity poll, `MAX_DELTA_ROWS = 60` rows of recent activity,
  `OFFLINE_AFTER_FAILURES = 3` failed ticks before the screen says it is stale, and `cache: 'no-store'`
  on every request.
* A delta whose `PreviousRevision` does not continue the applied revision is a **gap**: the buffer is
  discarded and a full Snapshot is refetched. The host publishes metadata-only delta rows, so this UI
  never merges a delta into a view — a tick either re-renders from a Snapshot or says it is stale.
* Reconnect always starts from a fresh Snapshot; nothing is replayed.
* Selection state, hover and density are the only state this UI owns. It holds no queue, job,
  eligibility, alarm or device state of its own, and it computes no decision: every number on screen is
  a projection the Runtime published.
* Front/Rear decoded temperatures do not exist in any contract the Runtime publishes, so the thermal
  block states `DFR_NOT_PUBLISHED_BY_RUNTIME` in the Front-minus-Rear row and shows the acquisition
  facts it does have (`Quality`, `DecodeStatus`, `CopiedWordCount`, `DiagnosticState`,
  `MappingStatus`, `NotVerified`). It is never rendered as a gap, and never as a zero.
* The wall map is the authoritative U-shape: `LEFT`, `REAR`, `RIGHT`, `FRONT` with 24 / 30 / 24 / 30
  cells over 6 rows (rows 1-3 `UPPER`, rows 4-6 `LOWER`), 108 cells total, `I7` and `I16` shown as
  normal Product Sensors, and `buildModel` refuses anything else.
* Fonts are the system stack. No font binary is copied into this workspace and no licence file with a
  URL in it is: an offline kiosk must not depend on a font fetch, and a copied URL would be an
  address in the product tree for no benefit.

## Gate

```
node --test apps/ui/test/*.test.mjs
```

18 tests, no dependencies to install. It pins the file inventory, that every import resolves to a
shipped file, the absence of inline script and of `url()` / `@import` / `@font-face`, the GET-only
surface, the ASCII-only and `innerHTML`-free source, the geometry and refusal paths, the cadence
constants, and the contract field names each panel reads (so a renamed contract field fails a test
instead of silently blanking a panel). `node tools/boundary-scan/boundary-scan.mjs .` is the repo-wide
check. `index.html` carries no `xmlns` on its inline SVG for the same reason: a namespace URL is an
address in the product tree.

**Status:** shipped in Stage 0.4C-K4 as a presentation layer over the existing read-only contracts.
The earlier React/Vitest/Playwright plan is not implemented and is not required by this checkpoint; if
the Owner later chooses a framework, that decision replaces this directory and its gate, and the
hosting slice above (`/`, `/ui/{file}`, `Ui/` content) is what carries over unchanged.
