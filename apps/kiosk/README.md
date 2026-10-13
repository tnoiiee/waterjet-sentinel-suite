# Wjss.Kiosk — application-owned shell (Stage 0.4C-K2)

> **Current position (2026-10-13, Library Selection Gate — selected library, PR #22 OPEN targeting main `fdc81d8`, genuine lock `49e4043` MERGE READY):** Kiosk shell and Operations Preview remain **preview/read-only** loopback-only (no TEST-NET-1 documentation range contact, no TEST_HARDWARE/PRODUCTION/device write). Software transport dependency is closed (selected library via CPM, self-owned adapter `ITcReadOnlyClient`, genuine lock `49e4043`) but **is not wired to Kiosk** — no Physical TC acquisition, no `PHYSICAL_TEST`/`PRODUCTION` until One-Node Physical TC gate after PR #22 merge (see `docs/STAGE_LIBRARY_SELECTION_GATE_REPORT.md` and `docs/TC_EVIDENCE_SOURCE_REGISTER.md`). Physical connectivity/reachability/field-hardware/FC/Unit ID/start/byte-word order/scaling/wire-break/timing remain `NOT VERIFIED`.

The full-screen, application-owned Windows window that hosts the local Operations preview route.
It is a real shell now: WinForms (`net10.0-windows`) wrapping the fixed-name embedded web-view
runtime control **`Microsoft.Web.WebView2`, pinned at `1.0.4258.31`** in
[`../../Directory.Packages.props`](../../Directory.Packages.props) under the Stage 0.4C-K1 Owner
dependency decision (2026-10-12). The Kiosk project references it **without a version**, so the
central pin stays the single source of the version.

The shell holds **no control authority**: it starts nothing, commands nothing, parses no Product
state, keeps no credential and writes nothing except its own embedded-view runtime cache. The local
preview host is a separate process that the Operator starts and stops.

## What this window does (Stage 0.4C-K2)

| Behaviour | How |
| --- | --- |
| Application-owned window, no browser chrome | WinForms borderless top-level window; the embedded view has no address bar, no tab strip and no browser UI |
| Opens the local route automatically | `KioskShellOptions.ApprovedOrigin` (scheme, loopback host and port 5187) is set on the view as soon as it is ready |
| Full-screen / kiosk presentation | Borderless, maximized by default (`FormBorderStyle.None`); the **Windowed / full screen** button switches to a normal resizable window, because this is a development preview and not an OS lockdown |
| 1920×1080 at 100% scaling (fullscreen fit) | **Acceptance TARGET for the Owner-local rerun — NOT yet verified.** The header is a measured four-row grid (Stage identity beside the action zone, then host state, Stage boundary and policy counters each full width and wrapped) and the embedded page is bounded to the client width. DPI configuration is `ApplicationHighDpiMode=PerMonitorV2` (project property, the WFO0003 correction) + `AutoScaleMode.Dpi` + `ZoomFactor = 1.0` |
| Loopback-only content | Scheme and host are hard constants; the approved origin cannot be pointed at another machine, another scheme or a local file |
| External navigation blocked | Every navigation whose target is not the approved loopback origin is cancelled; the blocked count is shown in the status bar |
| Popups / new windows blocked | `NewWindowRequested` is handled and refused; no second window can be opened |
| Arbitrary file navigation blocked | A `file:` target fails the approved-origin check and is cancelled |
| Downloads and permissions refused | `DownloadStarting` cancels; every permission request is denied |
| No developer tools in the Operator presentation | `AreDevToolsEnabled`, `AreDefaultContextMenusEnabled` and `AreBrowserAcceleratorKeysEnabled` are all off |
| Runtime-unavailable / reconnecting state | A local health probe runs every 2 s; while it fails, the view is hidden and the shell shows **LOCAL PREVIEW HOST UNAVAILABLE — RECONNECTING**, so a frame that stopped updating is never presented as current values |
| Safe reload / reconnect | When the host answers again, or after a view-process failure, the approved route is reloaded automatically |
| Intentional Operator exit | A visible **Exit Kiosk Preview** button (or the normal window close) plus a confirmation dialog; no credential is involved |
| Close guard | `OnFormClosing` cancels a user-initiated close once and asks for confirmation; an operating-system shutdown is never blocked. This is an operational usability control, never a safety protection |

The status bar always shows the Stage boundary statement:
`SIMULATION / PREVIEW - NO HARDWARE ACCESS - NO DEVICE WRITE - FIELD VERIFICATION PENDING`.

## Owner Manual Review findings and this correction (2026-10-12)

**Owner Local Manual Review result: FAILED** (automated Owner-local validation had passed: zero-warning
Release build, .NET 414/414, kiosk contract 12/12, and the WebView2 WPF-reference isolation proof
`CoreCount=1, WinFormsCount=1, WpfCount=0, LoaderCount=3`).

The Owner started the real local host (`node .\apps\ops-preview\server.mjs`), the Kiosk connected and
showed `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)` — so this was **not** a host-start or reconnect
failure. At fullscreen 1920×1080 the application was **wider than the screen**:

1. **Native header:** `Windowed / full screen` stayed visible while `Exit Kiosk Preview` sat completely
   outside the right edge, with no wrap or width adaptation — the required visible exit action was
   unreachable.
2. **Embedded Operations Preview:** the Density control was clipped at the right edge, `Comfortable` was
   only partly visible, and right-side content extended past the 1920 px viewport.

**Root cause (one cause, two symptoms).** The header was a two-column grid whose left `FlowLayoutPanel`
had `AutoSize = true` with `WrapContents = false` and a very long boundary/state line, so it insisted on
its full preferred width; the right action panel did the same. The outer layout's single column had no
explicit style, and an unstyled `TableLayoutPanel` column defaults to `AutoSize` — so the column grew to
the header's preferred width, which (a) pushed the actions past the right edge and (b) stretched the
content cell, the embedded view and therefore the **web page viewport** past the screen edge, which is
what clipped the page's own right-hand controls.

**Correction.**

- The header is now a two-column, four-row grid with explicit priority: the action column is `AutoSize`
  (the only content that keeps its preferred width, so the buttons can never be pushed off), and every
  text row is `AutoSize = false` with `Dock = Fill`, so **no text can demand width from the form**. The
  long texts (host state, Stage boundary, policy counters) each own a full-width row and wrap.
- Row heights are **measured** with `TextRenderer.MeasureText(..., WordBreak)` and applied as
  `SizeType.Absolute`, so nothing depends on WinForms auto-sizing guesswork; the measured layout is
  re-applied on resize, on a monitor DPI change and whenever a header text changes.
- The outer layout's single column is now explicitly `SizeType.Percent, 100F`, which bounds the content
  cell — and with it the embedded view and the web page viewport — to the client width. This is the fix
  for defect 2 as well as defect 1.
- The minimum window size is unchanged at 1024×600, no scrollbar is introduced, no text size is reduced,
  and the Exit action is never hidden or removed.
- The Operations Preview stylesheet was hardened on the web side: `overflow-x: hidden` plus `max-width:
  100%` as a page-level guard, `main` bounded to `minmax(0, 1fr)`, `min-width: 0` on every flex/grid
  child that can hold wide content, the 20-column Overview table contained in its own
  `.table-wrap.scroll` frame, `minmax(min(..., 100%), 1fr)` for the auto-fit grids, and no `100vw`
  anywhere. See `apps/ops-preview/test/layout.test.mjs`.

**Runtime-init correction (2026-10-12, after the Owner's interaction findings).** The width correction
passed the Owner's visual review, but the embedded Preview was inert: the served page
(`apps/ops-preview/public/index.html`) contained **no script element**, so the browser never requested
`app.mjs` and nothing was populated or wired. The page now loads the module
(`<script type="module" src="/app.mjs"></script>` — the only script element, same origin, no inline
script, so the content security policy is unchanged), the application reports its state on the page body
(`data-app-state` = `booting` / `ready` / `failed`), and an initialization failure raises a **visible**
banner carrying a safe error category with no stack trace, local path or internal detail, so an empty but
apparently usable page can no longer occur. Runtime coverage now resolves the application **through the
server** (module graph over the real routes, boot to `ready`, thirteen scenarios, 240 rows, wired controls,
visible failure state) in `apps/ops-preview/test/page.test.mjs`.

**Not verified in Arena:** the WinForms header was **not compiled and not rendered** — the Arena sandbox
has no .NET SDK — and **no browser exists there, so real-browser execution is NOT RUN**: the Preview was
never executed by a browser engine and no pixel layout, overflow measurement or WebView2 behaviour is
claimed from the Node tests. The header correction is asserted at source-contract level
only, and the page correction at source/structure level only. The Owner-local fullscreen rerun and the
manual acceptance below are the validation of record.

**Owner acceptance for this correction:** the complete native header fits inside fullscreen 1920×1080;
`Exit Kiosk Preview` and `Windowed / full screen` are both fully visible; the Stage boundary, host state
and policy counters remain visible; the Operations Preview does not extend outside the viewport; the
Density control is fully visible; the Overview table scrolls only inside its own frame; no page-level
horizontal scrollbar exists; windowed mode stays usable; reconnect stays fail-safe; and every automated
gate stays green. The 1920×1080 claim stays an **acceptance target** until the Owner confirms it.

## Out of scope in this Stage (deliberately, per the Stage brief)

No Runtime process supervision (the Operator starts the host), no Windows auto-login, no OS shell
replacement, no Group Policy lockdown, no production installer / auto-start / watchdog, no privilege
escalation, no credential storage, no remote browser hosting, no non-loopback binding, and no
control of any device.

## Owner-local prerequisites (Windows)

1. **.NET SDK 10.0.401** (pinned by [`../../global.json`](../../global.json)).
2. **Evergreen WebView2 Runtime** — pre-installed on current Windows 10/11; otherwise install the
   Microsoft Evergreen runtime, which is the approved runtime distribution for this preview
   (no fixed-version runtime is shipped with the repository).
3. **Node.js 22.22.2 or newer** for the local preview host.

## Owner-local runbook

```powershell
# 1. Refresh the Kiosk NuGet lock file for the approved package (see the note below), then build.
dotnet restore apps/kiosk/Wjss.Kiosk.csproj
dotnet build   apps/kiosk/Wjss.Kiosk.csproj -c Release

# 2. Start the local preview host (separate terminal). It prints its loopback URL and binds
#    127.0.0.1 only. Default port 5187. Both forms below start the SAME host; the first is the
#    command the Owner used successfully, the second is the package script that exists for it.
node apps/ops-preview/server.mjs
npm --prefix apps/ops-preview start

# 3. Confirm the host answers before starting the shell.
#    Browse to http://127.0.0.1:{port}/api/health  (the default port is 5187; the /api/boundary
#    route shows the same Stage boundary statement the window displays)

# 4. Start the Kiosk shell.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
#    or run the built Wjss.Kiosk.exe from apps/kiosk/bin/Release/net10.0-windows/
```

**Do not document `npm start` at the repository root:** there is no root `package.json`. The host has
exactly two launch forms — the direct `node` command above and the `start` script in
`apps/ops-preview/package.json`, which runs the same file. `apps/ops-preview/test/layout.test.mjs`
asserts that every launch command documented here exists and matches the package script.

**Port match:** the host defaults to `5187` and the shell defaults to the same port. If the host is
started with `OPS_PREVIEW_PORT=<port>`, start the shell with `$env:WJSS_KIOSK_UI_PORT=<port>` too.
The **host** is never configurable; only the port can match.

**What to verify in the window:** the boundary statement is visible; the Operations route renders at
1920×1080 with 100% scaling and no browser chrome; `F12` / `Ctrl+Shift+I` do nothing; navigating
away (or opening a new window) is refused and increments the blocked counters; stopping the host
switches the window to the reconnecting screen within about two seconds with **no stale values left
on screen**; restarting the host brings the route back automatically; and **Exit Kiosk Preview**
asks for confirmation before closing.

**What to verify in the Commissioning Wall Map (Stage 0.4C-K1 Wall Mapping pass, as corrected by the
Owner critical topology correction):** the map shows 108 positions and **108 Sensor cells — I7 and I16
are ordinary Sensor cells** in their normal ordinal position (no position is a `NON_SENSOR_GAP`); the
Sensor Map summary reads "Topology invariants AGREE (108 / 216 / 240 / 24)"; each Sensor cell carries the Sensor ID, the Front and the Rear state, the current
engineering value with its unit only when the value is valid, Quality, the Commissioning state and any
evidence blocking; **Both / Front only / Rear only** switch the displayed measurement and Front and
Rear are never merged into one value; the physical-schedule Previous/Next still works and is labelled
`(schedule order)`, while the Wall-order Previous/Next walk the map order over all 108 Sensors (Front
before Rear, I6:R → I7:F → I7:R → I8:F, stopped at the edges); selecting a position highlights its
Overview rows and fills the Sensor detail, an unassigned Channel selects no Sensor, and an I7 or I16
row selects that Sensor's cell exactly like any other Sensor; the Loop-Test progress shows 108 Sensors
and 216 measurements derived live (204 `NOT_TESTED`, 12 `BLOCKED` by Drawing internal conflicts); the
filters change only what is displayed and never a count, a verdict or the selected identity; the map
is vertically **static**: the complete U-shape (Rear, Left, Right, Front) is visible as one continuous
reference, there is **no vertical scrollbar inside the map frame**, and page-level scrolling is the only
vertical scrolling surface. A narrow window scrolls the map **sideways inside its own frame only** (still
with no inner vertical scrollbar); the Overview table stays the only other sideways scroller; and the
page has no horizontal overflow (`document.scrollingElement.scrollWidth === clientWidth`). A Preview
verdict approves nothing in the field.

**What to verify in the paired Front/Rear thermal diagnostic (Stage 0.4C-K1 thermal-pair pass):** with
`NORMAL_GOOD` applied, **Front is above Rear for every one of the 108 pairs** and each Wall Map cell shows
a compact read-only `dFR` row (`dFR +6.1 degC [+]`) whose value is positive, deterministic for the same
scenario tick and Sensor, and different between Sensors — never one delta for all 108. `REDUCED_FRONT_REAR_DIFFERENTIAL`
keeps Front above Rear with a reduced gradient and reads `REDUCED_POSITIVE_CANDIDATE` (a *candidate*: possible
reduced local heat transfer / possible slag insulation, never a confirmed result); `NEAR_ZERO_FRONT_REAR_DIFFERENTIAL`
reads `NEAR_ZERO_CANDIDATE` (an abnormal paired response that requires investigation, with no approved
Product deadband); `REVERSED_FRONT_REAR_DIFFERENTIAL` puts Front below Rear and reads `REVERSED_CONTRADICTION`,
which is a paired-measurement contradiction to check first for mapping, Sensor health, installation and
process transient — never a slag classification by itself. Every one of those states is **read-only
diagnostic / trend only**: no alarm, no queue entry, no cleaning job, no WaterJet dispatch, no pump or valve
action, no motion and no field approval. In `BAD`, `STALE`, wire-break and communication-loss scenarios the
paired value disappears to `dFR --` with the failing side named, **no number and no unit are shown, and no
Last Valid Value is substituted**. The Sensor detail additionally lists the Front value, the Rear value,
`dFR = Front - Rear`, the paired state, the guidance, the synthetic provenance and `FIELD VERIFICATION PENDING`.
The paired value has no Channel, no Sensor identity, no commissioning state and no Preview verdict of its
own: the Overview stays at 240 rows, the totals stay 216 measurements / 108 Sensors / 108 positions, and the
loop-test workflow (observations, verdicts, remarks) applies to the selected measurement only — recording or
clearing a Front result never changes the Rear result.

*Historical note:* the K1 text of these two paragraphs originally read "106 Sensor cells, I7 and I16 shown
as `NON_SENSOR_GAP`, gaps skipped, 106 pairs, 212 measurements / 106 Sensors". Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.

**Stopping:** close the shell through **Exit Kiosk Preview** (confirmed), then stop the host with
`Ctrl+C` in its terminal.

## WinForms-only WebView2 reference isolation (Stage 0.4C-K1 zero-warning correction)

The package's build targets add **three** assemblies to a `net10.0-windows` project: `...Core`,
`...WinForms` and `...Wpf`. Only the first two are used here — this shell is WinForms-only and does
not enable WPF — but the WPF assembly was still being added to the compile/reference graph, and it is
compiled against a WindowsBase major version that the .NET 10 reference pack does not provide. The
Owner-local Release build therefore emitted exactly one warning, **MSB3277** (conflicting WindowsBase
assembly versions), in an otherwise clean build.

`Wjss.Kiosk.csproj` now carries one narrow, project-local correction, the target
`RemoveWebView2WpfReference`. It runs after the package references exist (`AfterTargets="ResolvePackageAssets"`)
and before assembly resolution (`BeforeTargets="ResolveAssemblyReferences"`), and removes **only** the
reference item whose file name is `Microsoft.Web.WebView2.Wpf`.

What it deliberately does **not** do:

- it does not enable WPF and does not turn the shell into a mixed WinForms/WPF application;
- it does not suppress MSB3277: no `NoWarn`, no warning-severity change, no `TreatWarningsAsErrors`
  change, no binding redirect and no manual WindowsBase reference — the conflicting reference itself is
  removed instead;
- it does not read, write or modify any package file, the global package folder or the lock file, and
  it hard-codes no user profile, cache path or absolute path (the match is by file name only);
- it does not exclude `...Core`, `...WinForms` or the WebView2 loader/runtime assets;
- it does not change the package pin (`1.0.4258.31`), Central Package Management, locked restore, the
  target framework, the source-generated WinForms bootstrap or the DPI project property.

If a future package or project change reintroduces the WPF reference, the same target **fails the
build** with an actionable error instead of a warning, and the source-contract tests in
`test/shell.contract.test.mjs` fail as well. Three of those tests now assert exactly this: the project
stays WinForms-only, the correction removes only the WPF assembly and runs at the right point, Core and
WinForms are kept and positively proved, and no warning suppression exists.

Reference-graph proof (run it after a restore; it does not restore by itself):

```powershell
dotnet msbuild apps/kiosk/Wjss.Kiosk.csproj -t:KioskWebView2ReferenceProof -p:Configuration=Release
# expected, and the target fails unless it is exactly this:
#   WJSS-KIOSK-WEBVIEW2 CORE=1
#   WJSS-KIOSK-WEBVIEW2 WINFORMS=1
#   WJSS-KIOSK-WEBVIEW2 WPF=0
```

**Not verified in Arena:** this correction was authored from the Owner-local diagnostic evidence, but
the Arena sandbox has no .NET SDK and no reachable package feed, so the target itself has **not** been
executed there and no build result is claimed. The Owner-local zero-warning Release build is the
verification of record.

## Lock file note (Owner-local, required)
The repository sets `RestorePackagesWithLockFile=true`. Adding the approved package changes the Kiosk
dependency graph, so **`apps/kiosk/packages.lock.json` must be regenerated Owner-locally**
(`dotnet restore apps/kiosk/Wjss.Kiosk.csproj`) and the refreshed file committed after review. It
could not be regenerated in Arena: the Arena sandbox has no .NET SDK and no reachable NuGet feed, so
no restore, build or .NET test of this project has been executed there. Until the refresh is
committed, a restore running in locked mode will report the lock file as stale — that is expected
for this Stage and is recorded in the Stage checkpoint.

## Validation status

- **Verified in Arena:** the Stage boundary scanner (S1–S9, `node tools/boundary-scan/boundary-scan.mjs .`)
  is clean with this project's sources; and
  `node --test apps/kiosk/test/*.test.mjs` runs a source-contract suite that asserts the shell
  source satisfies the protected behaviours listed above (approved origin only, no developer tools,
  no second window, no download, no file navigation, visible reconnecting state, confirmed exit, no
  process start, no device write, central version pin, DPI bootstrap shape).
- **Not verified in Arena:** compilation, runtime behaviour, Windows presentation, DPI rendering and
  the embedded-view runtime itself. `dotnet restore`/`build`/`run` and the visual check are
  Owner-local steps, and the source-contract suite is a static check, not a substitute for them.
