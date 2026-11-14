# apps/ui — Operator presentation (Stage 0.4C-K4)

The fullscreen Operator screen for a WJSS cell: a Unified Command Bar over six tabs - the 108-Sensor
wall map with the active cockpit, the cleaning queue, the Water Jet and Pump surfaces, the Sensor and
Path detail, the alarms, and the host evidence with the trends. It is **browser-native**
(one document, one stylesheet, eleven ES modules) and it is **served by `Wjss.Runtime` itself**.
There is no build step, no package manager, no bundler and no transpiler anywhere in this
workspace: what is authored here is what the browser reads, byte for byte.

## The shipped set (13 files)

| Path | Role |
| --- | --- |
| `public/index.html` | The document: 4-band full-bleed `1920x1080` Hydrogen shell (`48px` topbar, `26px` status strip, `1fr` workspace with 6 tabs, `24px` event ticker, plus out-of-flow fixed `#diag-popover` overlay), one `<script type="module">`. No inline script, no inline style. |
| `public/global.css` | The only design-token location. Hydrogen Industrial HMI dark slate palette (`#0d1014` / `#15191f` / `#1c222a`), vivid cell status fills (`#1f6a49` / `#b0382e` / `#3e454f`), exact `1920x1080` full-bleed viewport lock, `2x2` Tab 1 grid (`6fr/4fr`, `1fr/188px`), 3-column U-shape wall map, and density switch (`39px`/`34px` cells). |
| `public/app.mjs` | Boot, the tab router, repaint cadence, selection and keyboard (keys 1-6 switch tabs), density toggle, offline banners, `document.body.dataset.appState` lifecycle (`booting` / `ready` / `failed`). |
| `public/topology.mjs` | **The one canonical topology table**: walls, tiers, the 24/30/24/30 counts, WJ1-WJ8 with their dedicated valves, the four WSB cabinets and their axis roles, the millimetre/native scaling (`1 mm = 10,000 native`), the published stage order and the pill arithmetic. Every other module imports it; none restates it. |
| `public/chartKit.mjs` | The shared canvas kit: a gap-safe series that ends the drawn segment at a missing sample, the frame and axis helpers, the dashed setpoint and ready band, the single scalar marker the isolation-valve pressure uses, and the label placer the square workspace frame needs. |
| `public/shellBar.mjs` | The command bar: the six tabs, the three summary pills with their tooltip and note, the mode badge built only from published `health` / `status`, the alarm count, the feed line and the simulator-gated Diagnostics dropdown. |
| `public/tabs.mjs` | Tabs 3, 4 and 6 plus the active cockpit and the camera viewport container: the cabinet grid, the pump card, the control availability, the master-detail Sensor list, the square 0-280 mm frame, the path stage table, the host summary, the wall breakdown and the normalised trend explorer. |
| `public/feed.mjs` | The whole network surface: fetch loop, revision-gap detection, reconnect, bounded delta row list. Nothing else in the UI may call `fetch`. |
| `public/wallMap.mjs` | The authoritative U-shape geometry and `buildModel` (it refuses a Snapshot that is not 108 cells). |
| `public/sensorDetail.mjs` | Selected-Sensor facts and the trend canvas (a `null` sample is drawn as a gap, never as a line to zero). |
| `public/thermal.mjs` | The thermal block and the Front/Rear availability statement. |
| `public/dispatchPanel.mjs` | Global queue, frozen countdown and frozen decision, 13-stage timeline, live `TimeDelay` preview. |
| `public/opsPanel.mjs` | Active Job, Pump, Alarms, the Critical Alarm modal and the status chips. |
| `test/ui.contract.test.mjs` | The gate: 30 tests over inventory, module graph, geometry, cadence, the read-only surface, the element ids the paint needs, the published contract shape behind every field it reads, the parity of `topology.mjs` with the two .NET catalogs, the Hydrogen 4-band/U-shape layout pins, and fake-DOM rendering with finite canvas coordinates. |

## Two roots, one copy

* authored root: `apps/ui/public/` — the files under test;
* served root: `Ui/` **beside the host binary**, produced by the single item in
  `apps/runtime/Wjss.Runtime.csproj`:

  ```xml
  <Content Include="..\ui\public\**\*.*" Link="Ui\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  ```

The include is relative to that project file. `apps/runtime/` is two directories below the repository
root, so the presentation is **one** level up, at `apps/ui/public`; a path that walks up twice points
at a directory that does not exist, an empty glob is not an MSBuild error, and the build stays green
while every route 404s. That happened once, which is why the copy is now checked twice rather than
assumed:

```xml
<Target Name="VerifyOperatorUiOutput" AfterTargets="CopyFilesToOutputDirectory">
  <Error Condition="!Exists('$(WjssUiOutputRoot)Ui\index.html') or !Exists('$(WjssUiOutputRoot)Ui\app.mjs')" ... />
</Target>
```

The target resolves its root from `$(OutDir)` (with `$(OutputPath)` as the fallback) and normalises
the trailing separator, so it looks at the directory the host itself reads from and cannot fail a
correct build. `dotnet test` additionally asserts the resolution of the include path and, when a build
output exists, that `Ui/` carries all thirteen files byte-identical to this directory.

That item is the only "build" the presentation has: MSBuild copies the directory into the output.
It introduces no `PackageReference`, so `packages.lock.json` is untouched and a locked restore stays
honest. A host build whose output lacks `Ui/index.html` no longer reaches the point of serving: it is
refused at build time. If a host that was built some other way still lacks the files, the document
route answers 500 with a plain sentence rather than serving a stale or partial tree.

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

## The 1920x1080 overhaul (Stage 0.4C-K4 fixup series)

The bar replaced the two-column frame: one row of plant state, six tabs, no outer scrollbar. The rules
the layout was built to, and what it deliberately does not show:

* **One topology source.** `topology.mjs` is the only file that writes the wall / tier / count / WJ / IV /
  WSB / scaling table. A module that uses the table imports it; the contract test fails a module that
  names `WJ1` itself. The table's content is checked against the C# catalogs on every run.
* **A tab that is not open is still current.** Every panel is painted from the same accepted frame on
  every tick, so switching tabs cannot land on a view that is quietly forty seconds old.
* **Pill three is `uncertain + notClassified`,** because `Classification` has no `UNCERTAIN` member; the
  tooltip and the note under the bar carry the breakdown, the full four-wall table lives in
  System and Trends, and a set that does not add to 108 is marked `TOPOLOGY_COUNT_MISMATCH` instead of
  being reconciled on screen.
* **The mode badge reads the host.** It is assembled from `KioskHealthPayload` and `RuntimeStatus` only —
  profile, acquisition mode, readiness, stage — and never from a label baked into the presentation.
  Presentation helpers (re-poll, move the selection, reveal the frame identity, preview the modal) are
  inside a Diagnostics dropdown that renders only when the host reports a simulator profile.
* **What is not published stays unpublished.** The IP Camera viewport is a container with a sentence, not
  a frame: `NO CAMERA SOURCE - THE READ-ONLY RUNTIME HAS NO IMAGE ROUTE`, zero requests, no address.
  The isolation-valve outlet pressure is one scalar on the active Job, so it is drawn as a marker with
  `NO IV HISTORY CHANNEL PUBLISHED` and never as a history line. Coal flow, a completed-cycle archive
  and a `tpToRpRatio` are the same kind of absence, and each says so on the panel it belongs to.
* **The workspace frame is honest geometry.** `0..280 mm` square, `1 mm = 10,000 native`, integer
  millimetres only, the Safe Return Centre at `125, 125 mm = 1,250,000 / 1,250,000 native`. No stage or
  Sensor coordinate is published, so none is plotted: a position invented for a dot is exactly what an
  operator would later trust.

## Gate

```
node --test apps/ui/test/*.test.mjs
```

30 tests, no dependencies to install. They are grouped into three kinds of promise.

* **Shape** — the file inventory, that every import resolves to a shipped file **by exact case** and
  stays inside `/ui/`, that no module looks up an element id the document does not carry (a `null`
  lookup during the first paint is a thrown boot, not a blank panel), the absence of inline script and
  of `url()` / `@import` / `@font-face`, the ASCII-only and `innerHTML`-free source, the GET-only
  surface, the geometry and refusal paths and the cadence constants.
* **Contract** — every property chain a module reads off a Snapshot, a status, a workspace document or
  the liveness body is resolved against the property graph of the `.NET` records that publish it
  (`packages/contracts/*.cs`, `apps/runtime/RuntimeApiResponses.cs`). A name the contract does not
  publish does not throw in a browser: it prints a fallback and looks answered, which is worse on an
  operator screen, so the graph, not prose, is the check. The Safe Return row is pinned field by field
  for the same reason.
* **Boundary** — the read-only surface, the stated `DFR_NOT_PUBLISHED_BY_RUNTIME` absence, the
  lifecycle on `document.body.dataset.appState`, the camera container that makes no request, and the
  parity of `topology.mjs` with `WaterJetTopologyCatalog`, `MotionTopologyCatalog` and
  `CleaningPathContracts` (the eight Water Jet rows, the 1-3 / 4-6 tier ruling, the sixteen axis lines,
  the eight cabinet bindings and `NativePerMillimetre`) — which is what makes "one canonical table" a
  checked fact rather than a claim a reviewer has to trust.

`node tools/boundary-scan/boundary-scan.mjs .` is the repo-wide check. There is no SVG element anywhere
in the presentation and therefore no `xmlns` namespace URL either: the trend, the delay bands and the
workspace frame are drawn on a `<canvas>` with `getContext('2d')`, which keeps every asset a plain text
file the host can serve from its own output directory.

The live answers — 200 on the document and on each allow-listed module with its own content type, 404
on an unknown or traversed name, 200 `ALIVE` on the shell liveness route — are asserted by
`tools/launch/Start-WjssOperatorKiosk.ps1 -Probe` against a running host, because reaching a listener
from a test here would need a test-only package.

**Status:** shipped in Stage 0.4C-K4 as a presentation layer over the existing read-only contracts.
The earlier React/Vitest/Playwright plan is not implemented and is not required by this checkpoint; if
the Owner later chooses a framework, that decision replaces this directory and its gate, and the
hosting slice above (`/`, `/ui/{file}`, `Ui/` content) is what carries over unchanged.
