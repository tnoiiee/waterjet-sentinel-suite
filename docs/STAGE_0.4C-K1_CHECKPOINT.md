# Stage 0.4C-K1 — Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview (Development Checkpoint)

> **Superseded topology note (Owner critical topology correction, 2026-10-12).** This document predates the correction and retains, as a dated historical record, the belief that logical positions I7 and I16 were `NON_SENSOR_GAP` positions (106 Sensors / 212 channels; Sensor sequence skipping I7 and I16; WJ3 / WJ1 "at the I7 / I16 gap anchor"). Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. 108 × 2 = 216; 240 physical TC channels − 216 = 24 unassigned. The text below is retained unedited and must not be used as a current baseline; the current record is [`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md).

**Status: DEVELOPMENT CHECKPOINT, submitted for Owner review in a NEW PR against `main` — OPEN, NOT
MERGED (do not merge). Merge authority is the Owner only.**

**Follow-up (Owner-authorized zero-warning correction pass):** the Owner-local validation reported in
§12 passed everything except exactly one Release-build warning, `MSB3277` (conflicting WindowsBase
assembly versions) caused by the package adding its WPF-only assembly to this WinForms-only project.
§13 records the exact root cause, the narrow project-local correction, the new source-contract coverage
and the honest verification status. No safety, authority, evidence or dependency-version boundary
changed. The checkpoint commit SHAs are recorded
in the pull request and the final report, not inside their own commits (AGENTS §4.6).

**Follow-up (Owner-authorized Commissioning Wall Mapping and Loop-Test Usability pass):** the Owner
authorized a read-only U-shaped Commissioning Wall Map so that field loop testing no longer needs the
manual translation wall position -> Sensor -> Front/Rear -> Node/Cabinet -> Module/Channel -> value ->
verdict. §16 records the exact-source sweep, the canonical topology source, the selection-identity
defect that the pass found and fixed, the changed/unchanged/not-verified split, the Arena totals at
this head, and the consolidated 21-step Owner-local handoff. No safety, authority, evidence or
dependency boundary changed; **no dependency was added at all** in this pass.

**Follow-up (Owner-authorized Front/Rear thermal-pair semantics and synthetic diagnostic pass):** the
Owner found that the Synthetic Preview systematically rendered **Rear above Front** (for example Front
29.4 / Rear 35.1), which contradicts the approved Sensor geometry and would train the Operator to read
the pair backwards. §18 records the corrected domain rule, the defect at its **source** (the synthetic
generator, not a rendered label), the read-only paired presentation model, the three new explicit
paired scenarios (catalogue total **13**), the changed/unchanged/not-verified split, the Arena totals at
this head and the consolidated **28-step** Owner-local handoff. `FrontRearDelta` is **READ-ONLY
DIAGNOSTIC / TREND ONLY** until field baselines and operating regimes are approved: no alarm, trip,
interlock, cleaning queue, GlobalQueue, WaterJet dispatch, cleaning job, AutoSequence, pump, isolation
valve, axis motion or automatic-control effect, and no field approval. No safety, authority, evidence or
dependency boundary changed; **no dependency was added at all** in this pass.

**Stage boundary (shown on screen by the preview and by the shell, at all times):**
`SIMULATION / PREVIEW - NO HARDWARE ACCESS - NO DEVICE WRITE - FIELD VERIFICATION PENDING`.

**No hardware, no live MODBUS communication, no TEST_HARDWARE, no PRODUCTION, no device write and no
control of any kind exists in this Stage.** `WIRE_BREAK_DECODING: BLOCKED` is unchanged: the Stage's
wire-break presentation is a labelled simulated diagnostic scenario, and `0x7FFF` alone is never
treated as a confirmed wire break.

---

## 1. Base gate and merge-parent proof

| Item | Value |
| --- | --- |
| Approved remote `main` | `48f0315ea1bcbd3f71f46a47bb4b55fc04327487` |
| Verification | `git ls-remote origin refs/heads/main` — exact match before any edit |
| Merge parents | `3e1a7fd9d28db65221c123960a0c85b62d93949a` (1st) and `b2048ec0aa5703767a2af85c73eaa1ae94d06e53` (2nd), `git cat-file -p` |
| Predecessor PR | **PR #18 MERGED** at that merge commit; `gh pr view 18` |
| Open conflicting PRs | none (`gh pr list --state open` empty) |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — re-verified at the start and at the end of this Stage, **unchanged** |
| Index / worktree | clean at session start; on completion the only entries are this Stage's own files |
| Session branch | `arena/3be5c713-waterjet-sentinel-suite`, starting exactly at the approved commit. The merged PR #18 branch `arena/c4b63a26-waterjet-sentinel-suite` was **not** used, continued or pushed |

**Sandbox recreation: NOT TRIGGERED.** No ref recovery, index reload or destructive Git operation was
needed or used. No `git reset`, `clean`, `restore`, `checkout`, `switch`, `stash`, `rebase`,
force-push or `read-tree -u` was executed at any point in this Stage.

## 2. Source inspection performed before any edit

`AGENTS.md`, `README.md`, `SECURITY.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`,
`docs/REQUIREMENTS.md`, `docs/DOMAIN_MODEL.md`, `docs/ARCHITECTURE.md`, `docs/TEST_STRATEGY.md`,
`docs/STAGE_0.4C-0B_CHECKPOINT.md`, `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md`,
`docs/TC_EVIDENCE_SOURCE_REGISTER.md`, `docs/evidence/tc-drawing-schedule-rev1.csv`,
`docs/evidence/tc-process-image-profile-rev1.md`, `docs/STAGE_0.4B-3_CHECKPOINT.md`,
`docs/decisions/ADR-0016-kiosk-shell-direction.md` and the accepted ADRs, the current Operations UI
source (`apps/ops-preview`, `apps/mapping-config`, `apps/runtime/Inspector`), the current Runtime
publication source (`apps/runtime`, `packages/application/Runtime`), the current application-shell
source (`apps/kiosk`), the current Transport/hardware paths, and
`tools/boundary-scan/boundary-scan.mjs` in full.

Inspected current state, stated plainly:

- **Current local UI entry point:** `apps/mapping-config/server.mjs` (the Stage 0.4B-3 Mapping UI,
  loopback only) is the only runnable local Operations surface that existed before this Stage. There
  is no Product Operations Console source: `apps/ui/` is a placeholder, and `apps/runtime/Inspector`
  is a self-contained runtime snapshot view. The Stage therefore **builds** the Product-shaped
  Operations preview rather than modifying the Mapping UI; `apps/mapping-config` is unchanged and
  still green (**82/82**).
- **Current Runtime transport:** loopback read-only HTTP (`127.0.0.1`, default port 5181), SIMULATOR
  profile by default. No transport was modified, added or contacted.
- **Current application shell:** a compile-only WinForms skeleton (Stage 0.3A-1 vintage) with a
  maximized placeholder label, no navigation, no close guard, no package reference.
- **Already-present host technologies:** .NET 10 WinForms (the shell project, DPI-correct since the
  WFO0003 correction), Node.js 22 for every local UI host, and the Owner-approved embedded web-view
  package. No new framework or runtime was introduced beyond the Owner-approved package.

## 3. Kiosk shell technology selection

**Selected: the existing WinForms shell project (`apps/kiosk`, .NET 10 `net10.0-windows`,
PerMonitorV2) hosting `Microsoft.Web.WebView2` pinned `1.0.4258.31`, Evergreen runtime.**

Why the existing stack could not meet the acceptance criteria without it: every Stage 5 behaviour —
application-owned chromeless window, no address bar or tabs, programmatic blocking of navigation,
popups, downloads and permissions, no developer tools, user-data folder control, process-failure
recovery and a reconnect state — is a property of the *embedded view host*. The repository had no
such host: the only alternative in the tree was the legacy WinForms web-browser control, which is a
different engine, has no ES-module support for this UI and offers no equivalent policy surface, and
the runtime's `Inspector` page is a page, not a shell. The Owner therefore approved exactly one
dependency (see §4) and **rejected** using Microsoft Edge in application or kiosk mode as the
delivered shell, the legacy web-browser control, Electron or another desktop framework, and a
source-only shell that remains intentionally non-building.

**Deliberately NOT implemented in this Stage** (out of Stage scope, or gated): Runtime process
start/stop supervision and crash-restart of a child process, Windows auto-login, OS shell
replacement, Group Policy lockdown, a production installer, auto-start, watchdog, privilege
escalation, credential storage, remote browser hosting and non-loopback binding. The Operator starts
the local host (see §8); the shell only points a window at it and reports whether it answers.

`docs/decisions/ADR-0016-kiosk-shell-direction.md` keeps its **DRAFT** status; §4 of the ADR carries
the dated status update. The ADR's draft Runtime-supervision items are **not** implemented here.

## 4. Dependency change record (Owner-authorised, minimum required)

Pre-change hashes (recorded before the first edit, taken from the approved base commit):

| File | SHA-256 at the approved base |
| --- | --- |
| `Directory.Packages.props` | `a5e9b352acd5286cd1aeb3f6733ba6531c81a9de5b1588d8b3b1991d840603b7` |
| `Directory.Build.props` | `68ebf5e8449b150d8f36961b85298af5f8b1b7ce29941558d634d7d89f321b7f` |
| `apps/kiosk/Wjss.Kiosk.csproj` | `601758ff286018f08f3682a234c893507bae6cec30c38759c5f1efbcec5ab3e3` |
| `apps/kiosk/packages.lock.json` | `f41632af8219dafb29467f732cadc0f71e810ca29374fbd3ffaa8bb02d02260e` |

| Change | Detail |
| --- | --- |
| Package | `Microsoft.Web.WebView2` |
| Version | `1.0.4258.31` (exact pin, Owner decision) |
| Where the version lives | `<PackageVersion Include="Microsoft.Web.WebView2" Version="1.0.4258.31" />` in `Directory.Packages.props` — the single source of truth |
| Where it is referenced | version-free `<PackageReference Include="Microsoft.Web.WebView2" />` in `apps/kiosk/Wjss.Kiosk.csproj` |
| Purpose | the application-owned shell's embedded view control (session 5 behaviours) |
| Framework compatibility | `net10.0-windows` WinForms; the package supports Windows desktop targets |
| Runtime distribution assumption | **Evergreen** WebView2 Runtime for this development preview (no fixed-version runtime is shipped with the repository) |
| Other dependencies | none added, removed or refreshed; no npm dependency was touched for the shell |
| Lock file | `apps/kiosk/packages.lock.json` (v2, group `net10.0-windows7.0`, **empty**) must be regenerated — **Owner-local only** |

**Restore/build limitation in Arena:** the Arena sandbox has **no .NET SDK and no reachable NuGet
feed**, so `dotnet restore`, `dotnet build`, `dotnet run` and all .NET tests were **not executed** in
Arena, and the lock file could not be regenerated there. Until the Owner commits the refreshed lock
file, a restore in locked mode will report the Kiosk lock file as stale — expected for this Stage
and not a defect claim in either direction.

**Owner-local validation commands** (also in `apps/kiosk/README.md`):

```powershell
dotnet restore apps/kiosk/Wjss.Kiosk.csproj      # regenerates the Kiosk lock file
dotnet build   apps/kiosk/Wjss.Kiosk.csproj -c Release
node apps/ops-preview/server.mjs                 # the local preview host (separate terminal)
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
```

## 5. What was built

### 5.1 Kiosk shell (`apps/kiosk`)

`KioskShellOptions.cs` (new) holds the approved origin (scheme and loopback host are hard constants;
the port defaults to 5187 and only the port may be matched by `WJSS_KIOSK_UI_PORT`), the Stage
boundary text, the health route and the probe timings. `MainForm.cs` is now the real shell:
borderless maximized window with a persistent status bar (Stage identity, boundary statement, host
state, policy counters) and a windowed/full-screen toggle; the embedded view is created against a
local runtime cache folder and loads the approved origin automatically; external navigation, other
ports, other schemes, local files, popups, new windows, downloads and permission requests are all
refused and counted; developer tools, context menus, browser accelerator keys, the status bar, zoom
control, web messages and the built-in error page are disabled; `ZoomFactor` is fixed at 1.0; a 2 s
health probe drives a visible **LOCAL PREVIEW HOST UNAVAILABLE — RECONNECTING** screen that hides the
view, so a frame that stopped updating is never presented as current values, and recovery reloads the
route; a view-process failure produces the same guarded state; exit is a visible button plus a
confirmation dialog, and the close guard cancels one user-initiated close and asks. The shell starts
no process, writes no record, parses no Product state and holds no credential.

### 5.2 Commissioning data and presentation (`packages/commissioning`)

A dependency-free read-only package that parses the committed 240-row drawing-derived schedule
(`docs/evidence/tc-drawing-schedule-rev1.csv`) and classifies it into the protected baseline:
108 logical positions, 106 Product Sensors, 212 measurements, 6 nodes × (750-362 ×1 + 750-498 ×5 +
750-600 ×1), 30 modules, 240 physical Channels, 200 `DRAWING_VERIFIED` / 12 `DRAWING_INTERNAL_CONFLICT`
/ 4 `DRAWING_TOPOLOGY_CONFLICT` / 24 `SPARE_UNMAPPED` / **0 unresolved**, with I7 and I16 kept as
authoritative `NON_SENSOR_GAP` positions — never Sensors, never Water Jets. Quality
(`GOOD/UNCERTAIN/BAD/STALE/DISABLED`), DiagnosticReason and CommissioningState stay **three separate
dimensions**; `INVALID` is presentation-only and was **not** added to the Quality enum. Invalid
current values are never rendered as a number, and the last valid sample is labelled as historical
and explicitly not the current value.

### 5.3 Operations preview host (`apps/ops-preview`)

A loopback-only, read-only Node host (no npm dependency; the same pattern as the Mapping UI) serving
the Operations route with the **Sensor Commissioning Monitor as the Product-shaped main area**, plus
the Sensor Map, Mapping and Diagnostics preview summaries, each stating its missing authority. Every
route is GET/HEAD; every other method is 405; there is **no** route for a device, transport, control,
queue, job, cleaning, pump, valve or motion action; CSP is `default-src 'self'` with no remote asset,
no remote font and no CORS wildcard; the bind host is a constant and the responses are `no-store`.
The monitor renders all 240 physical Channels with the required columns, the required filters and
summary counts, an operator-readable detail panel, and a Preview-only workflow that records a
verdict and a remark **in memory only** — clearly labelled as a preview record that approves nothing.

### 5.4 The ten deterministic scenarios (§10)

> The catalogue this section describes was the ten **protected** scenarios of this checkpoint. The
> Owner-authorized Front/Rear thermal-pair pass (§18) **appended** three explicit paired
> demonstrations (`REDUCED_FRONT_REAR_DIFFERENTIAL`, `NEAR_ZERO_FRONT_REAR_DIFFERENTIAL`,
> `REVERSED_FRONT_REAR_DIFFERENTIAL`), so the current catalogue is **thirteen** scenarios. **No**
> protected scenario was removed or renamed and every statement below still holds for those ten.

`NORMAL_GOOD`, `UNCERTAIN_TRANSIENT`, `OUT_OF_RANGE`, `STALE`, `COMMUNICATION_LOSS_NODE` (exactly one
node), `SIMULATED_WIRE_BREAK` (labelled `SIMULATED_DIAGNOSTIC_SCENARIO`, raw `0x7FFF` shown as
evidence only, engineering value unavailable), `RECOVERY`, `DRAWING_CONFLICT`,
`PRODUCT_TOPOLOGY_CONFLICT`, `UNASSIGNED_CHANNEL`. They are pure functions of the evidence rows and a
tick, with no clock, no random source and no device.

## 6. Automated validation in Arena (exact totals)

| Check | Command | Result |
| --- | --- | --- |
| Commissioning package | `cd packages/commissioning && node --test test/*.test.mjs` | **77 tests / 77 pass / 0 fail / 0 skipped** |
| Commissioning check sweep | `npm run check` | pass (all source modules parse) |
| Kiosk Preview host + UI | `cd apps/ops-preview && node --test test/*.test.mjs` | **40 tests / 40 pass / 0 fail** (server 15, render 25) |
| Kiosk shell source contract | `node --test apps/kiosk/test/*.test.mjs` | **12 tests / 12 pass / 0 fail** |
| Mapping UI (unchanged graph, regression) | `cd apps/mapping-config && npm test` | 82 tests / 82 pass / 0 fail / 0 skipped |
| Mapping package (unchanged graph, regression) | `cd packages/mapping-config && npm test` | 268 tests / 261 pass / 0 fail / **7 skipped** (skips are the Arena workbook-dependent Owner-local tests) |
| Repository boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` | **0 findings (S1–S9 clean)**, exit 0 |
| Workbook integrity | `sha256sum T8_IO_Card_Mapping.xlsx` | matches the protected value, unchanged |
| Whitespace / conflict check | `git diff --check` | clean |

The boundary scan is the machine proof of the protected boundary: no non-loopback address or URL in
the Product tree, no vendor or production vocabulary, no credential-shaped literal, no environment-
controlled bind host, no any-interface bind instruction, every project in the solution, every XML
file well-formed and the kiosk DPI shape intact. Two real findings were raised by the scan during
this Stage (a credential-shaped enum member and a vendor column name echoed in a test fixture); both
were fixed **in the source** and the scanner was **not** weakened.

## 7. Checks that were performed, and checks that were not

**Performed in Arena:** the local preview host was started in the sandbox and its routes were
exercised over loopback HTTP: `/api/health` 200, `/api/boundary` 200, `/api/commissioning/schedule`
serving the baseline, `/api/commissioning/observations` serving 240 observations and 240 derived views
with node-scoped communication loss (TC-NODE-04 lost, the other five online) and the wire-break
scenario carrying `SIMULATED_DIAGNOSTIC_SCENARIO` provenance with an unavailable engineering value,
`/api/sensor-map` serving the 108/106/212 structure with I7 and I16 as gaps, and a non-GET method
correctly refused with 405. The UI itself was rendered in the test environment's DOM against the real
host by the render suite, which asserts the rendered text of the boundary, the monitor, the filters,
the detail panel and the preview workflow.

**NOT performed in Arena, and not claimed:** no browser was opened against the preview; no Windows
session exists in Arena; the Kiosk shell was **not compiled and not run**; no screenshot, layout,
DPI-scaling or 1920×1080 visual acceptance was performed; the embedded-view runtime, its policy
callbacks and the real reconnect behaviour were not exercised. The shell's own suite is a
**source-contract** check — it proves the shell source contains the protected behaviours and no
forbidden ones, and it is explicitly not a substitute for the Owner-local build and visual check.

## 8. Owner-local run instructions

1. `dotnet restore apps/kiosk/Wjss.Kiosk.csproj` (refreshes the Kiosk lock file — see §4), then
   `dotnet build apps/kiosk/Wjss.Kiosk.csproj -c Release`.
2. In a separate terminal: `node apps/ops-preview/server.mjs` (binds `127.0.0.1` only; default port
   5187; prints its loopback URL).
3. Confirm the host answers (`/api/health`), then
   `dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release`.
4. Verify: boundary statement visible; no address bar or tabs; 1920×1080 at 100% scaling; `F12` and
   `Ctrl+Shift+I` do nothing; navigating away or opening a new window is refused and counted; stopping
   the host switches the window to the reconnecting screen (about 2 s) with no stale values left on
   screen; restarting the host restores the route automatically; **Exit Kiosk Preview** asks for
   confirmation.
5. Stop with the confirmed exit, then `Ctrl+C` in the host terminal.

Prerequisites: .NET SDK `10.0.401` (pinned by `global.json`), the Evergreen WebView2 Runtime, and
Node.js 22.22.2 or newer.

## 9. Changed / Unchanged

**Changed (new):** `packages/commissioning/**`, `apps/ops-preview/**`, `apps/kiosk/KioskShellOptions.cs`,
`apps/kiosk/test/shell.contract.test.mjs`, `docs/STAGE_0.4C-K1_CHECKPOINT.md`.

**Changed (modified):** `Directory.Packages.props` (the single new version pin), `apps/kiosk/Wjss.Kiosk.csproj`
(version-free package reference), `apps/kiosk/MainForm.cs`, `apps/kiosk/Program.cs`,
`apps/kiosk/README.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`,
`docs/decisions/ADR-0016-kiosk-shell-direction.md` (dated status update only).

**Unchanged, verified:** the authoritative workbook; the Drawing-derived schedule evidence, the
process-image evidence and the validation summary under `docs/evidence/`; the Pump/Valve logic, the
Cleaning sequence, the Queue kernel, any Galil and MODBUS implementation, production configuration,
the live Hardware provider, generated evidence files, the Drawing evidence, the manufacturer manuals
and the schedule templates; the Runtime, the Mapping implementation, `apps/mapping-config`,
`packages/mapping-config`, `packages/contracts`, `Directory.Build.props`, `global.json`, every other
project and lock file; `README.md` (see below). No file was deleted or renamed.

**Explicitly not modified — CF-07 stays open:** the root `README.md` still carries a status block
naming Stage 0.4C-0B with "PR #18 OPEN — NOT MERGED" (PR #18 is now merged). `README.md` remains
outside this Stage's declared file scope, as in the two preceding Stages; it is reported for an Owner
decision rather than repaired silently.

**Not verified:** compilation, Windows runtime behaviour, DPI rendering, the 1920×1080 visual
acceptance, the embedded-view runtime, the Owner Browser Review, and every .NET test of the changed
graph. These are the Owner-local steps in §8.

## 10. Honesty statement (§23)

- Physical Sensor acquisition is **NOT IMPLEMENTED**.
- **No hardware was connected. No live MODBUS communication exists. No device was written to and no
  device was controlled.**
- **No TEST_HARDWARE and no PRODUCTION work exists in this Stage.**
- The commissioning values in the preview are **synthetic simulated values**; none of them is a
  measured value, and the wire-break presentation is a **labelled simulated diagnostic scenario**.
- Field verification remains **PENDING** for every Channel; nothing in this Stage approves, records
  or advances field verification.
- The Kiosk shell was **not compiled and not run in Arena**; the preview UI was **not viewed in a
  browser in Arena**; the Owner-local build, run and visual acceptance have **not** been performed
  and are **not** claimed.
- Formula-derived or evidence-derived quantities are re-derived from the committed 240-row schedule;
  no mapping was invented, no conflict row was repaired and no `NON_SENSOR_GAP` position was
  commissioned.

## 11. Merge recommendation

**Do not merge.** This is a development checkpoint for Owner review: merging is the Owner's decision
alone, the Kiosk dependency change still requires the Owner-local lock-file refresh and Release
build, and the visual acceptance of the shell has not been performed. The recommended review order is
§4 (dependency record), §7 (what was and was not checked), §8 (Owner-local run) and §9 (changed
files).


## 12. Owner-local validation received (Owner-reported, 2026-10-12)

Recorded as received from the Owner-local Windows workstation. These are **Owner-local** results, not
Arena results.

| Check | Owner-local result |
| --- | --- |
| Kiosk shell contract | 12/12 passed |
| Commissioning `check` / tests | passed / 77/77 |
| Operations Preview `check` / tests | passed / 40/40 |
| Mapping Configuration `check` / tests | passed / 82/82 |
| Normal solution restore | passed |
| Locked solution restore | passed |
| Full .NET tests | **414/414 passed** |
| Boundary scan S1–S9 | clean, 0 findings |
| Working tree after the Owner-local restore | `M apps/kiosk/packages.lock.json` — the genuine Owner-local lock refresh, containing the direct package `Microsoft.Web.WebView2` requested/resolved `1.0.4258.31` |
| Release build | succeeded **with exactly one warning: `MSB3277`** — WindowsBase 4.0.0.0 versus WindowsBase 5.0.0.0 |

Owner-local evaluated properties (the project is WinForms-only, and WPF is not enabled by this
repository): `TargetFramework = net10.0-windows`, `UseWindowsForms = true`, `UseWPF = ` (empty),
`EnableWindowsTargeting = true`, `OutputType = WinExe`.

## 13. Zero-warning correction: WinForms-only WebView2 reference isolation

**Exact root cause (from the Owner-local package evidence, replacing every earlier hypothesis, including
any suggestion that this repository enabled both WinForms and WPF — it does not).** The installed package
file `microsoft.web.webview2\1.0.4258.31\buildTransitive\Microsoft.Web.WebView2.targets` contains a
"regular .NET 5+" branch, which is the branch selected for `net10.0-windows`, and that branch
unconditionally adds three assemblies to the reference graph:

- `Microsoft.Web.WebView2.Core.dll`
- `Microsoft.Web.WebView2.WinForms.dll`
- `Microsoft.Web.WebView2.Wpf.dll`

`Microsoft.Web.WebView2.Wpf.dll` is compiled against `WindowsBase, Version=5.0.0.0`, while the .NET 10
reference pack provides the `WindowsBase, Version=4.0.0.0` facade. That conflict produces `MSB3277`
even though this project uses no WPF type at all.

**Correction (project-local, narrow, in `apps/kiosk/Wjss.Kiosk.csproj`).** A target
`RemoveWebView2WpfReference` runs after the package references exist
(`AfterTargets="ResolvePackageAssets"`) and before assembly resolution
(`BeforeTargets="ResolveAssemblyReferences"`) and removes **only** the reference item whose file name is
`Microsoft.Web.WebView2.Wpf`. The match is by file name only, so it is independent of the Owner's user
profile, the global packages folder, any absolute cache path and the assembly version. It reads, writes
and modifies no package file, no cache entry and no lock file. `...Core`, `...WinForms` and the WebView2
loader/runtime assets are untouched.

Explicitly **not** done, per the Owner's prohibitions: no `UseWPF=true` and no mixed WinForms/WPF
application; no `MSB3277` suppression; no `NoWarn`; no warning-severity change; no binding redirect; no
manual WindowsBase reference; no edit of the global package cache; no vendored or copied WebView2
assembly; no package upgrade, downgrade or refresh; no target-framework change; no database, transport,
hardware, vendor-transport or device package; no change to the central pin `1.0.4258.31`; no
hand-editing of `apps/kiosk/packages.lock.json`.

A second target, `KioskWebView2ReferenceProof`, provides the deterministic Owner-local proof and **fails**
if `...Core` or `...WinForms` is missing or if `...Wpf` is present. The removal target itself also fails
the build if a WPF-only reference survives the removal, so a silent regression becomes a loud, actionable
error rather than a warning.

**New source-contract coverage (five added tests; `apps/kiosk/test/shell.contract.test.mjs` is now
17/17).** They assert that the project stays `UseWindowsForms=true` and never enables WPF; that the
correction exists with the required target wiring, removes only the WPF-only assembly, and contains no
user-profile, cache or absolute path; that Core and WinForms are kept and positively proved by the proof
target; and that no warning suppression, severity change or binding redirect is introduced (with the
shared build properties still failing the build on a warning).

**Arena re-run after the correction:** kiosk contract **17/17**; commissioning `check` OK and
**77/77**; Operations Preview `check` OK and **40/40**; Mapping Configuration `check` OK and **82/82**;
`packages/mapping-config` `check` OK and **268 (261 pass, 0 fail, 7 skipped)**; boundary scan
**`0 findings (S1–S9 clean)`**; `git diff --check` clean.

**Not verified.** The correction has **not** been executed in Arena: the sandbox has no .NET SDK and no
reachable package feed, so no restore, build, `msbuild -t:` invocation or .NET test was run there and no
build result is claimed. The Owner-local zero-warning Release build, the reference-graph proof output and
the manual kiosk visual review remain the verification of record.

### 13.1 Owner-local handoff commands

```powershell
# 1. Normal restore (refreshes the genuine kiosk lock file if the graph changed).
dotnet restore apps/kiosk/Wjss.Kiosk.csproj

# 2. Locked restore (must succeed; the lock file is the Owner-local artifact).
dotnet restore WaterJetSentinelSuite.sln --locked-mode

# 3. Evaluated property proof (expect UseWindowsForms=true and UseWPF empty).
dotnet msbuild apps/kiosk/Wjss.Kiosk.csproj -p:Configuration=Release `
  -getProperty:TargetFramework,UseWindowsForms,UseWPF,EnableWindowsTargeting,OutputType

# 4. Evaluated reference proof (expect CORE=1, WINFORMS=1, WPF=0; the target fails otherwise).
dotnet msbuild apps/kiosk/Wjss.Kiosk.csproj -t:KioskWebView2ReferenceProof -p:Configuration=Release

# 5. Release solution build on the restored graph (expect: Build succeeded. 0 Warning(s)).
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

# 6. Full .NET tests without rebuilding (expect the legitimate total, 414/414 or updated).
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore

# 7. Node gates.
node --test apps/kiosk/test/shell.contract.test.mjs
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test
npm --prefix apps/ops-preview run check      ; npm --prefix apps/ops-preview test
npm --prefix apps/mapping-config run check   ; npm --prefix apps/mapping-config test

# 8. Boundary scan (expect: 0 findings (S1-S9 clean)).
node tools/boundary-scan/boundary-scan.mjs

# 9. Runtime/output asset inspection: the loader must be present, the WPF assembly need not be.
Get-ChildItem -Recurse apps/kiosk/bin/Release/net10.0-windows |
  Where-Object { $_.Name -match 'WebView2' } | Select-Object Name, Length

# 10. Final proof: no unexpected working-tree changes (only the genuine lock-file refresh).
git status --porcelain
git diff --stat
```

Acceptance for this correction: **Build succeeded with 0 warning(s)**, full .NET tests at the legitimate
total, kiosk contract all pass, commissioning all pass, Operations Preview all pass, Mapping
Configuration all pass, boundary S1–S9 clean, WebView2 Core and WinForms references present, WebView2 WPF
reference absent, the required loader/runtime asset present, and no unexpected working-tree changes.


## 14. Owner Manual Review result and the fullscreen-fit correction (2026-10-12)

**Owner Manual Review: FAILED — and the failure is recorded here as received.** The Owner ran the real
local host (`node apps/ops-preview/server.mjs`; the host reported `listening on 127.0.0.1:5187`), the
Kiosk connected and displayed `LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)` — so this was **not** a
host-start, transport or reconnect failure. At fullscreen 1920×1080 the application was **wider than the
screen**:

1. **Native header.** `Windowed / full screen` remained visible while **`Exit Kiosk Preview` sat
   completely outside the right edge**; no wrap, compact mode or width adaptation occurred, so the
   required visible exit action could not be reached.
2. **Embedded Operations Preview.** The **Density control was clipped** at the right edge, `Comfortable`
   was only partly visible, and other right-side content extended beyond the 1920 px viewport.

**Root cause — one cause, two symptoms.** The header was a two-column grid whose left
`FlowLayoutPanel` was `AutoSize = true` with `WrapContents = false` and carried the long Stage boundary
string plus the state and policy text, so it insisted on its full preferred width, and the right action
panel did the same. The outer layout's single column carried **no explicit style**, and an unstyled
`TableLayoutPanel` column defaults to `AutoSize`, so the column grew to the header's preferred width.
That (a) pushed the action area past the right edge of the client area, and (b) stretched the content
cell — and therefore the embedded view control, and therefore the **web page viewport** — past the
screen edge, which is what clipped the page's own right-hand controls. The README's claim that 1920×1080
presentation was already DPI-safe and correct was **not supported by the Owner's screenshot** and has
been replaced by an explicit acceptance target.

**Correction A — native shell header (`apps/kiosk/MainForm.cs`).** The header is now a two-column,
four-row grid with explicit priority:

- column 1 is `AutoSize` and holds the action zone (both buttons) — the **only** header content that
  keeps its preferred width, so the actions can never be pushed off the right edge;
- column 0 takes the remainder and holds the text rows; **every text label is `AutoSize = false` with
  `Dock = Fill`**, so no text can demand width from the form;
- row 0 places the action zone beside the short Stage identity; the **host state**, the **Stage
  boundary** and the **approved-origin policy counters** each get their own full-width row and **wrap**;
- row heights are **measured** with `TextRenderer.MeasureText(..., WordBreak)` and applied as
  `SizeType.Absolute`, so layout is deterministic rather than dependent on WinForms auto-sizing;
- the measured layout is re-applied on client-size change (`OnResize`), on a monitor DPI change
  (`DpiChanged` and `OnDpiChangedAfterParent`) and whenever a header text changes (connection state,
  policy counters, window-mode label);
- the outer layout's single column is now explicitly `SizeType.Percent, 100F` with `AutoSize = false`,
  which bounds the content cell, the embedded view and the page viewport to the client width — the fix
  for defect 2 as well as defect 1;
- **unchanged:** the 1024×600 minimum window size, the text sizes, the confirmed-exit behaviour, the
  reconnecting fail-safe, the approved loopback origin, and both header actions (never hidden, never
  removed, keyboard reachable, `TabStop = true`). No scrollbar is introduced.

**Correction B — Operations Preview width fit (`apps/ops-preview/public/styles.css`).** Page-level
overflow sources were removed and containment was made explicit: `overflow-x: hidden` and
`max-width: 100%` as the page-level guard; `main` bounded to `grid-template-columns: minmax(0, 1fr)`;
`min-width: 0` on every flex/grid child that can hold wide content (`.banner .title`, `.banner-actions`,
`.chips`, `.sections`, `.view-controls`, `.toolbar`, `.detail-card`, `.detail-grid`, `.table-wrap`);
the 20-column Overview table contained in `.table-wrap.scroll` (`overflow: auto`, `max-width: 100%`),
which owns the only horizontal scrollbar on the page; `minmax(min(...), 100%)` for the auto-fit summary
and detail grids; `minmax(0, 180px) minmax(0, 1fr)` for the detail definition lists; the modal bounded
by `min(620px, calc(100% - 32px))`; and **no `100vw` or other viewport-relative width anywhere**.

**Correction C — launch commands.** `apps/ops-preview/package.json` now has a `start` script
(`node server.mjs`) alongside `check` and `test`, so the documented forms are both real:
`node apps/ops-preview/server.mjs` and `npm --prefix apps/ops-preview start`. The runbook and this
document now only show commands that exist, and
`apps/ops-preview/test/layout.test.mjs` parses the documented `node`/`npm` lines in both files and
asserts every referenced path and npm script exists (a `start` script must also name a file that
exists). There is no root `package.json`, so no root `npm start` is documented.

**Correction D — new contract coverage.** `apps/kiosk/test/shell.contract.test.mjs` gained four tests
(**21/21**): both header actions stay present, never hidden and never removed; the header is measured,
bounded and responsive (explicit percent column, four measured absolute rows, re-layout on resize/DPI/
text change, no `AutoScroll`, minimum size still exactly 1024×600); the left status content cannot force
the action area outside the client width (auto-size-free text rows, action width reserved first,
full-width spanning rows); and the boundary/host state/policy counters remain on screen and re-measured
when they change. `apps/ops-preview/test/layout.test.mjs` is a new 9-test source/structure contract for
the page-width rules, the bounded table frame, the wrapping containers and the documented launch
commands.

**Arena gates after the correction:** kiosk contract **21/21**; Operations Preview `check` OK and
**49/49** (was 40; +9 layout tests); commissioning `check` OK and **77/77**; Mapping Configuration
`check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass, 0 fail, 7 skipped)**;
boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean.

**NOT verified in Arena, and not claimed.** The WinForms header was **not compiled and not rendered**
(there is no .NET SDK in the Arena sandbox) and no browser was opened against the page, so **no layout,
scaling or 1920×1080 visual result is claimed from these source and structure contracts**. They prove the
intent and the absence of the defect's source shape; they do not prove pixels. The Owner-local fullscreen
rerun and the manual acceptance list below are the validation of record.

### 14.1 Owner-local handoff commands

```powershell
# 1. Preserve or regenerate the genuine kiosk lock refresh (it is the Owner-local artifact).
git status --porcelain apps/kiosk/packages.lock.json
dotnet restore apps/kiosk/Wjss.Kiosk.csproj

# 2. Locked restore must succeed.
dotnet restore WaterJetSentinelSuite.sln --locked-mode

# 3. Zero-warning Release build on the restored graph.
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

# 4. Full .NET tests without rebuilding.
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore

# 5. All Node/static gates.
node --test apps/kiosk/test/shell.contract.test.mjs
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test
npm --prefix apps/ops-preview run check      ; npm --prefix apps/ops-preview test
npm --prefix apps/mapping-config run check   ; npm --prefix apps/mapping-config test
npm --prefix packages/mapping-config run check ; npm --prefix packages/mapping-config test
node tools/boundary-scan/boundary-scan.mjs

# 6. Start the host (either form; the second is the package script).
node apps/ops-preview/server.mjs
npm --prefix apps/ops-preview start

# 7. Run the Kiosk.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release

# 8. Fullscreen check at 1920x1080: the complete header, BOTH actions, the Stage boundary, the host
#    state and the policy counters must all be visible; Exit Kiosk Preview must be reachable.

# 9. Windowed check: the Windowed / full screen toggle must produce a usable resizable window, and the
#    header must stay complete when the window is resized down to the 1024x600 minimum.

# 10. Exit check: Exit Kiosk Preview must ask for confirmation and then close only this window.

# 11. Overflow check in the embedded page (open DevTools outside the Kiosk, or measure in the window):
#     document.scrollingElement.scrollWidth must equal clientWidth; no page-level horizontal scrollbar.

# 12. Table check: scrolling the Overview table to its last column must move ONLY the table frame
#     (.table-wrap.scroll); the page, the Density control and the navigation must not move.

# 13. Reconnect check: stop the host, confirm the reconnecting screen appears with nothing stale left on
#     screen, restart the host, confirm the route returns by itself.

# 14. Final proof: no unexpected working-tree changes (only the genuine lock refresh).
git status --porcelain
git diff --stat
```

**Manual acceptance for this correction (Owner):** the complete native header fits inside fullscreen
1920×1080 · `Exit Kiosk Preview` fully visible · `Windowed / full screen` fully visible · Stage boundary
and host state still visible · Operations Preview does not extend outside the viewport · Density control
fully visible · Overview table scrolls only inside its own bounded frame · no page-level horizontal
scrollbar or overflow · windowed mode still usable · reconnection still fail-safe · all automated gates
still green. **The Stage is not complete until the Owner confirms this list.**

Two notes for step 11: use the built-in measurement above rather than `-getItem:Reference` for the
WebView2 graph — `-getItem` reports evaluation-time items, i.e. *before* `RemoveWebView2WpfReference`
runs — and the reference proof target `KioskWebView2ReferenceProof` (§13) is the supported way to check
that graph.


## 15. Owner Manual Review correction: Operations Preview runtime initialization (2026-10-12)

**Owner-local result, recorded as received.** The width correction **passed** visual review: the
complete native header fits, `Windowed / full screen` and `Exit Kiosk Preview` are both visible, the
Operations Preview stays inside the screen, the Density controls are visible, and the Overview table is
contained in its own horizontal-scroll frame.

**But the interaction review FAILED.** With the host started through the real package command
(`npm --prefix .\apps\ops-preview start`, listening on `127.0.0.1:5187`) and the Kiosk reporting
`LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)`, the HTML and CSS rendered while the application did
nothing: the Density controls and the Sensor Map / Diagnostics navigation did not respond, the Scenario
selector was empty, the Node selector was empty, the Node communication summary and the Commissioning
progress were empty, the Overview showed its header but **no rows**, and the Sensor detail fields were
empty.

**Exact root cause — the served page contained no `script` element at all.** `apps/ops-preview/public/index.html`
loaded `/styles.css` and nothing else, so the browser **never requested `app.mjs`**: no module executed,
no API was called, no data was rendered and no control was wired. Every symptom follows from that one
omission, including "the navigation does not respond" — the anchors scroll to sections that exist but
whose content was never filled in.

**Why the existing tests passed.** `test/render.test.mjs`, `test/layout.test.mjs` and the boot helper
import `public/app.mjs` **directly** and seed a stub DOM from `index.html`; they exercise the module, but
nothing in the suite ever checked that the **served page references** that module. A directly-imported
module is exactly the thing the browser never sees, so a test suite built on direct imports cannot catch
this defect class. That is now fixed at the level where the defect lived (see §15.2).

**15.1 Correction.** `index.html` now ends with `<script type="module" src="/app.mjs"></script>` — the
only script element in the page, same-origin, no inline script (so the existing
`script-src 'self'` CSP needs no relaxation, and `unsafe-inline`/`unsafe-eval` are still refused). Two
further changes make an inert page impossible to mistake for a healthy one:

- **A visible initialization-failure surface.** `<div class="boot-error" id="boot-error" role="alert" hidden>`
  sits directly under the banner, plus a `<noscript>` note for a window with JavaScript disabled. The
  application unhides it with a **safe category only** (data request / module load / runtime) — no stack
  trace, no local path, no internal detail; the actionable text stays in the local console.
- **A machine-readable boot sentinel.** `app.mjs` writes `data-app-state` on the page body: `booting`,
  then `ready` on a completed boot, or `failed` when initialization did not complete. Boot is split into
  a data stage and a render stage so the failure category distinguishes "the local host did not answer"
  from "the application failed to render", and uncaught errors/rejections after a successful boot also
  raise the visible failure state instead of leaving a half-empty screen.
- An empty control value now falls back to the current state (`||` rather than `??`), so a
  not-yet-populated selector can never send an empty scenario or node identity to the host.

Nothing else changed: the loopback-only bind, the approved origin, the no-external-asset rule, the
1920x1080 layout strategy, the read-only boundary, the reconnecting fail-safe, the confirmed exit and
the protected evidence totals are untouched.

**15.2 New runtime coverage (`apps/ops-preview/test/page.test.mjs`, 10 tests).** These resolve the
application the way a browser does — **through the server** — and then execute it over the served bytes:

1. the served page contains exactly one `<script type="module" src="/app.mjs">`, no inline script and no
   external script or stylesheet, and `/app.mjs` is served byte-identical to the file the tests execute;
2. the CSP permits that module (`script-src 'self'`, no `unsafe-inline`, no `unsafe-eval`) and the page
   contains nothing the policy would block;
3. **the whole served module graph resolves over the real routes** — every `import`/`export ... from`
   specifier is followed recursively from the served bytes (`/app.mjs` → `/pkg/index.mjs` →
   `/pkg/{schedule,quality,scenarios,observation,workflow,presentation}.mjs`), each answering 200 with a
   JavaScript MIME type and no `node:` or remote specifier anywhere;
4. boot completes and reports `ready`, with the failure surface hidden and **every** boot request a
   successful local GET (a failed asset, module or API request can no longer hide behind a passing test);
5. the Scenario selector carries the thirteen scenarios (the ten protected plus the three paired thermal
   demonstrations added in §18), the Node selector the six nodes, and the
   Node communication summary and Commissioning progress are populated;
6. the Overview renders exactly 240 physical Channels with 20 columns each, and selecting a row fills the
   identity, observation and Preview-workflow cards;
7. **Compact and Comfortable respond** (body density attribute plus `aria-pressed`), and all five
   navigation anchors point at sections the served page really contains;
8. applying a scenario reaches the rendered rows while the boot state stays `ready`;
9. a **failed boot becomes a visible, safe failure state** — `data-app-state="failed"`, the banner
   unhidden, the text carrying the safe category and containing no `.mjs`, no `apps/`, no path, no stack
   and no `undefined`;
10. the failure surface exists in the served markup and the stylesheet lets `hidden` win.

The boot helper now also gives **every** `bootApp` call a fresh module instance (an ES module is cached,
so a second import used to skip the application's boot entirely) and mirrors two browser behaviours the
stub previously got wrong: the first `<option>` appended to an empty `<select>` becomes its value, and
`document.body` **is** the `app-root` element. Both were stub-fidelity gaps that could have hidden or
fabricated behaviour.

**15.3 Arena gates after this correction.** kiosk contract **21/21**; Operations Preview `check` OK and
**59/59** (was 49; +10 page tests); commissioning `check` OK and **77/77**; Mapping Configuration
`check` OK and **82/82**; `packages/mapping-config` `check` OK and **268 (261 pass, 0 fail, 7 skipped)**;
boundary scan **`0 findings (S1–S9 clean)`**; `git diff --check` clean. The running local host was also
inspected directly over loopback HTTP: `/` now carries the module script element, and `/app.mjs` plus all
seven package modules answer `200 text/javascript`.

**15.4 NOT verified in Arena, and not claimed.** There is **no browser in the Arena sandbox**, so the
served page was never executed by a real browser engine and **real-browser execution is NOT RUN**: no
pixel layout, no DOM-computed overflow and no WebView2 behaviour is claimed from these tests. What they
prove is the wiring (the page loads the module, the graph resolves, the boot reaches `ready`, the
controls are wired, a failure is visible) over the **served** bytes and the **real** routes. The
Owner-local Kiosk rerun in §15.5 remains the verification of record, and the 1920x1080 interaction
acceptance stays open until it passes.

### 15.5 Owner-local rerun sequence

```powershell
# 1. Lock file stays as the genuine refresh; then locked restore and a zero-warning Release build.
git status --porcelain apps/kiosk/packages.lock.json
dotnet restore WaterJetSentinelSuite.sln --locked-mode
dotnet build   WaterJetSentinelSuite.sln -c Release --no-restore
dotnet test    WaterJetSentinelSuite.sln -c Release --no-build --no-restore

# 2. Node/static gates.
node --test apps/kiosk/test/shell.contract.test.mjs
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test
npm --prefix apps/ops-preview run check      ; npm --prefix apps/ops-preview test
npm --prefix apps/mapping-config run check   ; npm --prefix apps/mapping-config test
npm --prefix packages/mapping-config run check ; npm --prefix packages/mapping-config test
node tools/boundary-scan/boundary-scan.mjs

# 3. Start the host through the real package start command, then run the Kiosk.
npm --prefix .\apps\ops-preview start
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
```

Then confirm in the window: the Scenario selector offers thirteen scenarios (the ten protected scenarios
plus the three paired thermal demonstrations added in §18); Compact and Comfortable change
the density; Sensor Map and Diagnostics navigate; the Overview shows 240 rows; selecting a row fills the
Sensor detail; the Node communication summary and Commissioning progress are populated; `Apply scenario`
changes the rendered rows; **no initialization error banner is visible**; there is no page-level
horizontal overflow (`document.scrollingElement.scrollWidth === clientWidth`); the Overview table remains
the only thing that scrolls sideways; and stopping/restarting the host still shows the shell's
reconnecting state and recovers by itself. If initialization ever fails, the banner states the category
on screen — report that text rather than a stack trace.

## 16. Owner-authorized Commissioning Wall Mapping and Loop-Test usability pass (2026-10-12)

Same Stage, same branch `arena/3be5c713-waterjet-sentinel-suite`, same **PR #19 (OPEN, NOT MERGED — do
not merge)**, same approved base `main` `48f0315ea1bcbd3f71f46a47bb4b55fc04327487`. The pass starts at
the runtime-init checkpoint `040d322f734e528914527f1b2cfe7bf078d936a8` and adds one consolidated
checkpoint commit whose parent is exactly that SHA. Nothing in §13–§15 is reopened or reverted: the
zero-warning WebView2 reference isolation, the 1920x1080 fullscreen fit (Owner-reviewed **PASSED**) and
the runtime-initialization correction (the served page now loads and executes `/app.mjs`) are all
carried forward unchanged.

### 16.1 What the pass adds

- **Commissioning Wall Map (`packages/commissioning/src/wallmap.mjs`, NEW).** A read-only model of the
  approved physical wall: **108 logical positions**, **106 Product Sensor cells** and exactly **two
  `NON_SENSOR_GAP` cells — I7 and I16**, which are never Sensors and never Water Jets. The builder
  **throws** rather than render a wrong wall: it asserts 108/106/2, exactly the I7+I16 gaps, every
  Sensor exactly once, exactly one Front **and** one Rear measurement per Sensor, 212 measurements in
  the documented order, the approved per-wall counts (LEFT 24 / REAR 29+gap / RIGHT 24 / FRONT 29+gap),
  six rows in the approved row order (`G+201`, `G+101`, `G`, `H`, `I`, `J`) and distinct Channels per
  position. It is dependency-free and browser-safe; it invents no Sensor identity and reads no second
  topology source (§16.2).
- **Selection synchronised across the whole surface, on one identity.** Wall Map position -> its two
  measurements; Front/Rear -> exactly that measurement; Overview row -> its Sensor position; an
  internal-conflict row -> the right Sensor **and** the blocking reason; an unassigned Channel -> no
  false Sensor selection (`NOT_APPLICABLE`, nothing selected); an I7/I16 conflict row -> the **gap**,
  not a Sensor. Selection never writes to evidence, observations or verdicts.
- **Wall-order navigation.** Previous/Next in documented wall-mapping order (logical row, then column,
  gaps skipped, Front before Rear), edge-safe (no move at an edge, reported as an edge rather than a
  wrap). The existing physical-schedule Previous/Next **remains available and keeps its meaning**; it
  is now explicitly labelled `(schedule order)` so the two are never confused.
- **Loop-Test progress.** 106 Sensors and 212 measurements with `NOT_TESTED / IN_PROGRESS / PASS /
  FAIL / PARTIAL / BLOCKED`, derived **live** from the evidence plus workflow state for the selected
  scenario — no cached, stored or invented totals.
- **The read-only workflow is reachable from a map selection**: BASELINE / STIMULUS / OPEN_CIRCUIT /
  RECOVERY observations, PASS / FAIL / PARTIAL, remark, clear Preview result. All pre-existing blocking
  rules remain authoritative: a Product-topology conflict can never PASS, an unassigned Channel has no
  verdict, an internal conflict is FAIL/PARTIAL only, BLOCKED stays evidence-derived, and a Preview
  verdict approves nothing in the field.
- **Presentation.** The map is contained in its **own bounded frame** (`.wallmap-frame`, `max-width:100%`,
  `min-width:0`) so it can never widen the page; the Overview table remains the only other sideways
  scroller. *(This pass originally capped the frame height as well; Owner Manual Review found that cap
  produced a nested vertical scrollbar inside the map, and **§17 corrects it to a static vertical
  view** — the height cap is gone and the frame is bounded horizontally only.)* Cells are fixed-size tokens in both
  densities, in the approved compact range; the page uses no viewport-relative width and no inline
  style; quality/state are carried by text and symbols in addition to colour; the
  `SIMULATION / PREVIEW` vs field-verification distinction and the boundary text are preserved.

### 16.2 Canonical topology source (single source, no second source)

The wall topology is projected from **`config/examples/sensor-map.example.json` -> `logicalMatrix.slots`
— the protected Product topology plus committed evidence — and from nothing else.** The drawing's wall
label is not used to infer topology; the Runtime wall summaries and the spike's wall UI were read as
shape references only and are **not** authority. Verified at this head: 108 slots = 106 `SENSOR` + gaps
I7 (`SLOT-R5-C07`, REAR) and I16 (`SLOT-R5-C16`, FRONT); LEFT 1–4 (24), REAR 5–9 (29 + gap), RIGHT
10–13 (24), FRONT 14–18 (29 + gap); 18 columns x 6 rows; the matrix Sensors are identical to the 106
schedule Sensors (zero difference) and the 212 measurements are the 106 x (Front, Rear). The four
I7/I16 conflict rows, the twelve drawing-internal-conflict pairs and the 24 unassigned
`SPARE_UNMAPPED` Channels are unchanged protected facts. **Water Jet identity is never derived from
the Sensor wall**, and the served payload does not forward the gap Water-Jet anchor or any device /
Channel assignment to the browser.

### 16.3 Defect found and fixed by this pass (selection identity)

The first wiring selected a Wall Map position by **measurement** identity (`H7:F`) while the Overview,
the Sensor detail and the workflow are keyed by the **physical Channel** (`TC-NODE-03|A102.2|5`). The
symptom was a selection the rest of the page could not resolve (`NOT_APPLICABLE`, zero highlighted
Overview rows) — a real application defect, found by the new coverage, not a test artefact.
`wallmap.mjs` now publishes the two sequences in the same documented order (`order` in measurement
identity, `channelOrder` in physical-Channel identity) plus `positionByChannelKey`,
`measurementKeyByChannelKey` and `wallMeasurementKey()`, and **the one selection identity across the
Wall Map, the Overview, the Sensor detail and the workflow is the physical Channel key.** Navigation
runs on Channel keys. The Front and Rear measurements of a Sensor are never merged, copied, aliased or
made to hide one another.

### 16.4 Changed / Unchanged / Not verified

**Changed (this pass, 8 modified + 4 new paths):**

| Path | Change |
| --- | --- |
| `packages/commissioning/src/wallmap.mjs` | **NEW** — the read-only wall model, its order tables and its topology invariants (throws on violation) |
| `packages/commissioning/test/wallmap.test.mjs`, `test/helpers/wallMap.mjs` | **NEW** — 11 deterministic topology/order/selection tests over the committed public-safe sensor map |
| `packages/commissioning/src/presentation.mjs` | adds `loopTestSummary(schedule, sessionState)` (live 106/212 loop-test counts, no cached totals) |
| `packages/commissioning/src/index.mjs`, `package.json` | re-export the wall map and loop-test surfaces; `wallmap.mjs` added to the `check` sweep |
| `apps/ops-preview/public/index.html` | Loop-Test progress section and Wall Map section (frame, legend, view control, wall-order controls); existing physical-schedule nav relabelled `(schedule order)` |
| `apps/ops-preview/public/app.mjs` | wall renderers, live progress, view control, wall-order navigation, filter application, and the Channel-key selection wiring |
| `apps/ops-preview/public/styles.css` | the bounded `.wallmap-frame` and the fixed-geometry U map (bands, hollow centre, gap cells, legend, `--wj-*` tokens in both densities) |
| `apps/ops-preview/server.mjs` | forwards each slot's `logicalLabel` (the wall label the map prints); **does not** forward the gap Water-Jet anchor; no route, method or whitelist change |
| `apps/ops-preview/test/wallmap.render.test.mjs` | **NEW** — 17 tests: 108/106/2 rendering, value/unit honesty, STALE-as-history, gap inertness, map<->Overview<->detail<->workflow selection, view control, Front/Rear isolation, filter inertness, wall-order navigation, CSS bounds |
| `apps/ops-preview/test/layout.test.mjs` | the sideways-scroll pin now names the **two** bounded frames (`.table-wrap` group and `.wallmap-frame`) instead of one, and a new test pins the map's bounded/fixed-geometry contract — no assertion was deleted or weakened |

**Unchanged:** the WebView2 package and version (`1.0.4258.31`) and the WinForms-only WPF-reference
isolation; the lock files (**no dependency added, removed, refreshed or hand-edited in this pass**);
the 1920x1080 layout strategy, the corrected boot path and its visible failure surface; loopback-only
binding, the approved origin, the strict CSP and the absence of external assets; the read-only Preview
boundary and every route/method restriction (no device, transport, control, queue, job, cleaning,
pump, valve or motion route exists); the protected evidence totals and the authoritative workbook; the
reconnecting fail-safe and the confirmed exit. **No MODBUS, no physical acquisition, no contact with
`192.0.2.11`–`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no device write, no cleaning or motion
command, no queue authority, no Runtime supervision, no field-verification claim.**

**Not verified / not claimed:** there is **no browser engine and no .NET SDK in Arena**, so
**real-browser execution and the .NET build/tests are NOT RUN IN ARENA** — no pixel layout, no
DOM-computed overflow, no DPI behaviour, no WebView2 behaviour and no Windows rendering is claimed from
these tests. What the Arena tests prove is the model, the served bytes, the wiring and the contracts
over the **real** routes; the 1920x1080 interaction acceptance stays open until the Owner-local run in
§16.6 passes. A Preview verdict remains a Preview verdict and is never a field approval. Owner Manual
Review has **not** passed for this pass and Stage 0.4C-K1 is **not** claimed complete.

### 16.5 Automated validation in Arena at this head (exact totals)

| Check | Command | Result |
| --- | --- | --- |
| Commissioning package | `npm --prefix packages/commissioning test` | **88 tests / 88 pass / 0 fail / 0 skipped** (11 wall map) |
| Commissioning check sweep | `npm --prefix packages/commissioning run check` | pass (all source modules parse, incl. `wallmap.mjs`) |
| Kiosk Preview host + UI | `npm --prefix apps/ops-preview test` | **77 tests / 77 pass / 0 fail / 0 skipped** |
| Kiosk shell source contract | `node --test apps/kiosk/test/*.test.mjs` | **21 tests / 21 pass / 0 fail** |
| Mapping UI (unchanged graph, regression) | `npm --prefix apps/mapping-config run check && npm --prefix apps/mapping-config test` | **82 tests / 82 pass / 0 fail / 0 skipped** |
| Mapping package (unchanged graph, regression) | `npm --prefix packages/mapping-config run check && npm --prefix packages/mapping-config test` | **268 tests / 261 pass / 0 fail / 7 skipped** (skips are the Arena workbook-dependent Owner-local tests) |
| Repository boundary scan | `node tools/boundary-scan/boundary-scan.mjs` | **0 findings (S1–S9 clean)**, exit 0 |
| Workbook integrity | `sha256sum T8_IO_Card_Mapping.xlsx` | matches the protected value, unchanged |
| Whitespace / conflict check | `git diff --check` | clean |

The boundary scan raised one real finding in this pass (a credential-shaped literal created by a new
enumeration in `app.mjs`); it was fixed **in the source** and the scanner was **not** weakened. The
running host was also inspected over loopback HTTP at this head: `/` carries the Wall Map frame,
view control, wall-order controls and progress section; `/api/sensor-map` serves LEFT 24 / REAR 30 /
RIGHT 24 / FRONT 30 slots with `logicalLabel` forwarded and the two gaps marked `NON_SENSOR_GAP`; and
`/`, `/app.mjs`, `/styles.css`, `/pkg/*.mjs` and every `/api/...` route answer 200 over loopback.

### 16.6 Owner-local handoff — 21 steps (fast-forward from this checkpoint)

```powershell
# 1. Fast-forward to the checkpoint commit and prove where you are. Expect no local commits behind it.
git fetch origin ; git switch arena/3be5c713-waterjet-sentinel-suite ; git pull --ff-only
git rev-parse HEAD ; git log --oneline -3 ; git status --short

# 2. Preserve/regenerate the GENUINE Kiosk lock refresh: the committed lock file is the reviewed one.
git status --porcelain apps/kiosk/packages.lock.json
#    If dotnet asks for a refresh, let step 4 regenerate it and commit ONLY that file, reporting the
#    SHA-256 before and after. Never hand-edit it.

# 3. Confirm the tree is clean before building (nothing uncommitted, no stray artefact).
git diff --stat ; git diff --check

# 4. Locked restore.
dotnet restore WaterJetSentinelSuite.sln --locked-mode

# 5. Zero-warning Release build.  Expect 0 warnings / 0 errors.
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

# 6. Full .NET test run.  Expect 414 passed / 0 failed / 0 skipped.
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore

# 7. Prove the WinForms-only WebView2 graph is still isolated.
dotnet msbuild apps/kiosk/Wjss.Kiosk.csproj -t:KioskWebView2ReferenceProof -p:Configuration=Release
#    Expect exactly: CORE=1 / WINFORMS=1 / WPF=0.

# 8. Kiosk shell source contract.  Expect 21/21.
node --test apps/kiosk/test/*.test.mjs

# 9. Commissioning package (wall model + loop-test summary).  Expect 88/88.
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test

# 10. Operations Preview host + UI tests (the Wall Map coverage).  Expect 77/77.
npm --prefix apps/ops-preview run check ; npm --prefix apps/ops-preview test

# 11. Mapping Configuration UI regression.  Expect 82/82.
npm --prefix apps/mapping-config run check ; npm --prefix apps/mapping-config test

# 12. Mapping package regression.  Expect 268 (261 pass / 0 fail / 7 skipped); 268/268 with the
#     authoritative workbook supplied.
npm --prefix packages/mapping-config run check ; npm --prefix packages/mapping-config test

# 13. Static gates: boundary scan, whitespace, workbook integrity.
node tools/boundary-scan/boundary-scan.mjs
git diff --check
#    Expect 0 findings (S1-S9 clean), clean, and workbook SHA-256 4e0337e2...f422e8e unchanged.

# 14. Start the real Operations Preview host (loopback only, default port 5187).
npm --prefix apps\ops-preview start

# 15. Confirm the host answers before starting the shell (the shell probes it for about two seconds).
#     Browse to http://127.0.0.1:5187/api/health and http://127.0.0.1:5187/api/boundary

# 16. Start the Kiosk shell and confirm the boundary statement is visible and the host reads CONNECTED.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release

# 17. Wall Map: 108 positions, 106 Sensor cells, I7 and I16 shown as NON_SENSOR_GAP (never Sensors,
#     never Water Jets).  Each Sensor cell shows the Sensor ID, both Front and Rear states, the
#     current engineering value with its unit only when valid, Quality, Commissioning state and any
#     evidence blocking.

# 18. Front/Rear stay separate: switch Both / Front only / Rear only and confirm selecting the Front
#     measurement shows exactly that measurement and never hides, copies or alters the Rear one.

# 19. Selection and workflow: select a Sensor on the map and confirm its Overview rows highlight and
#     the Sensor detail fills; record BASELINE / STIMULUS / OPEN_CIRCUIT / RECOVERY observations with
#     PASS / FAIL / PARTIAL and a remark, then clear the Preview result.  Confirm a Product-topology
#     conflict cannot PASS, an unassigned Channel offers no verdict, an internal conflict is
#     FAIL/PARTIAL only, BLOCKED stays evidence-derived, and the workflow only ever affects the
#     selected measurement.

# 20. Wall-order navigation, filters and counts: Previous/Next walk wall order (Front before Rear,
#     gaps skipped) and stop at the edges; the physical-schedule Previous/Next still works and is
#     labelled (schedule order); the Commissioning and Measurement and Evidence filters change only
#     what is displayed - counts, evidence and the selected identity do not move; the Loop-Test
#     progress shows 106 Sensors and 212 measurements derived live.

# 21. Screen, recovery and final state: at 1920x1080 / 100% the page has no horizontal overflow
#     (document.scrollingElement.scrollWidth === clientWidth), the Overview table is the only other
#     sideways scroller, the map scrolls inside its own bounded frame only, both densities are
#     readable, and the header with Exit and Windowed/full-screen is intact.  Stop the host - the
#     window shows the reconnecting state with no stale values; restart it - the view recovers by
#     itself.  Finish with git status and git diff.
git status ; git diff
```

**Report back:** the 21-step results, the exact text of any failure, and the boundary statement shown
on screen. Do not report a Preview verdict as a field approval, and do not treat this handoff as Owner
Manual Review.

### 16.7 Honesty statement (§23) for this pass

This pass is **source + tests + documentation**. In Arena it was possible to: build the wall model and
prove its invariants, run every Node/static gate listed in §16.5, execute the served application module
in the test DOM over the real routes, and inspect the live host over loopback. It was **not** possible
to: compile or run the .NET solution, open a real browser (there is no browser engine in the sandbox),
measure real pixel layout or overflow, exercise WebView2, or observe Windows DPI/rendering. Those are
reported as **NOT RUN IN ARENA**, and the Owner-local sequence in §16.6 is the verification of record.
**PR #19 stays OPEN and is not merged. Owner Manual Review has not passed for this pass. Stage
0.4C-K1 is not claimed complete.**

## 17. Owner Manual Review correction: the Wall Map is a STATIC vertical view (no nested vertical scrolling)

Same Stage, same branch `arena/3be5c713-waterjet-sentinel-suite`, same **PR #19 (OPEN, NOT MERGED — do
not merge)**, same approved base `main` `48f0315ea1bcbd3f71f46a47bb4b55fc04327487`. This pass starts at
`34d0d8c0844aef300d85b0c87bc75dc49cd519ac` and adds one narrow checkpoint commit whose parent is
exactly that SHA. It changes **one scrolling strategy** and the layout contracts that pin it; nothing
else is reopened.

### 17.1 Owner result, recorded as received

Owner Manual Review of the Commissioning Wall Map: U-shape presentation **PASS**, 108 logical positions
**PASS**, 106 Product Sensors **PASS**, I7 and I16 shown as `NON_SENSOR_GAP` **PASS**, Front and Rear
presentation **PASS**, complete Wall Map rendering **PASS**, native Kiosk header inside 1920x1080
**PASS**, Operations Preview inside the viewport **PASS** — with **one usability defect: the Wall Map
frame carried its own vertical scrollbar**. That produced nested vertical scrolling (document scroll +
an inner scrollbar), which split one spatial reference into two scroll positions and is not suitable
for field loop testing.

### 17.2 Exact cause and the correction

**Cause.** `.wallmap-frame` was `max-height:80vh; overflow:auto`. The map's complete natural height
(three U rows of six-deep wall grids: about 1060 px at comfortable density) exceeds `80vh` at
1080, so the frame scrolled on **both** axes — a second vertical scrolling surface inside the page.

**Correction.** The frame is now `overflow-x:auto; overflow-y:hidden;` with **no `height` and no
`max-height`**, so it always expands to the map's complete natural height and the document is the
single vertical scrolling surface.

**Why not the illustrative `overflow-x:auto; overflow-y:visible`.** CSS Overflow 3 §3.1: when one axis
is neither `visible` nor `clip` and the other is `visible`, the used value of the `visible` axis becomes
`auto`. That pairing would therefore still compute to a scrolling vertical axis — the exact defect being
removed — and would silently reintroduce the nested scrollbar the moment any height constraint appeared.
`overflow-y:hidden` is the explicit, verifiable contract, and it is spelled out in the stylesheet
comment and pinned by a test that refuses `overflow-y:auto|scroll|visible` **and** refuses the
`overflow` shorthand on the frame.

**Clipping (requirement 4).** The frame was already a clipping scroll container (`overflow:auto` clips
to the padding box), so `overflow-y:hidden` clips nothing that was not already clipped, and the frame is
now exactly content-sized with its 10 px padding preserved. To keep keyboard focus fully visible inside
the cells — which clip their own content by design — the measurement buttons now draw their focus ring
**inside** their box (`outline-offset:-2px`). The legend, the presentation controls, the measurement
toggle, the wall-order controls and the selection/filter notes are all **outside** the frame, so they
can never be clipped or scrolled away; the frame contains the map element and nothing else (no second
internal viewport). Tooltips are native `title` attributes, unaffected by overflow.

**Horizontal containment retained.** The frame keeps `overflow-x:auto`, `max-width:100%` and
`min-width:0`: a narrow window scrolls the map sideways **inside the frame only**, the map keeps its
fixed cell geometry, and the map can never widen the page. At 1920x1080 the map is narrower than the
available width, so neither a page-level nor a frame-level horizontal scrollbar is produced. The
Overview table is untouched and keeps its own bounded horizontal scroll frame.

### 17.3 Changed / Unchanged / Not verified

**Changed**

| Path | Change |
| --- | --- |
| `apps/ops-preview/public/styles.css` | `.wallmap-frame` is now `overflow-x:auto; overflow-y:hidden` with no `height`/`max-height`; the Wall Map block comment documents the static vertical view and why `overflow-y:visible` is wrong; the measurement buttons gained an inset focus ring so the keyboard focus indicator can never be clipped |
| `apps/ops-preview/public/index.html` | The comment above the frame now describes horizontal containment only and the static vertical view (**no markup change**: the frame still contains the map element and nothing else) |
| `apps/ops-preview/test/layout.test.mjs` | The Wall Map contract test no longer expects a height cap; a new focused test separates the two axes (horizontal containment retained vs vertical scrolling prohibited, no height cap, no viewport-relative height), pins "no second internal viewport", and keeps the page-level guards and the Overview frame assertions |
| `apps/ops-preview/test/wallmap.render.test.mjs` | The bounded-frame test now asserts `overflow-x:auto` + `overflow-y:hidden`, refuses `max-height` and the `overflow` shorthand on the frame, and keeps every 108/106/2 and interaction assertion unchanged |
| `apps/kiosk/README.md` | The Wall Map verification paragraph now states the static vertical view and that horizontal containment is local to the frame |
| `CHANGELOG.md`, `docs/CURRENT_STATE.md`, `docs/STAGE_0.4C-K1_CHECKPOINT.md` | This correction recorded (§16.1 points here so no Stage text contradicts the source) |

**Unchanged:** the canonical Wall Map topology and its single source; 108 logical positions; 106 Product
Sensors; the I7/I16 `NON_SENSOR_GAP` identity; the approved U-shaped geometry, wall order and row
order; Front/Rear separation and the Both / Front only / Rear only control; the physical-Channel
selection identity and every synchronisation (Wall Map <-> Overview <-> Sensor detail <-> workflow);
the read-only Preview workflow and every blocking rule; wall-order and schedule-order navigation;
filters and loop-test progress; valid / BAD / STALE presentation rules; the scenario model; the
runtime-init module-script correction; the native Kiosk header layout; the WebView2 reference isolation,
version `1.0.4258.31` and the lock files (no dependency touched); loopback-only binding and the approved
origin; the strict CSP with no external assets; the reconnecting fail-safe; the confirmed exit; the
protected evidence totals and the authoritative workbook; the read-only Preview boundary (**no MODBUS,
no physical acquisition, no contact with `192.0.2.11`-`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no
device write or control**). No cell was resized, no font reduced, no wall hidden, paged, tabbed,
collapsed or accordioned, no spacing removed, no browser zoom or display scaling required, and no
page-level horizontal scrolling introduced.

**Not verified:** Owner-local Windows/WebView2 rendering; the fullscreen visual result after the
correction; the windowed visual result; and field loop-test usability — all awaiting the Owner rerun in
§17.4. **There is no browser engine and no .NET SDK in Arena**, so real-browser execution and the .NET
build/tests are **NOT RUN IN ARENA**; the CSS contracts above are source-level proofs and are explicitly
not a visual verification.

### 17.4 Owner-local rerun sequence (19 steps)

```powershell
# 1. Fast-forward to this checkpoint on the existing branch.
git fetch origin ; git switch arena/3be5c713-waterjet-sentinel-suite ; git pull --ff-only
# 2. Preserve the GENUINE Kiosk lock refresh: the committed lock file is the reviewed one.
git status --porcelain apps/kiosk/packages.lock.json
#    If dotnet asks for a refresh, let step 3 regenerate it and commit ONLY that file, reporting the
#    SHA-256 before and after. Never hand-edit it.
# 3. Locked restore.
dotnet restore WaterJetSentinelSuite.sln --locked-mode
# 4. Zero-warning Release build.  Expect 0 warnings / 0 errors.
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
# 5. Full .NET test run.  Expect 414 passed / 0 failed / 0 skipped.
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore
# 6. Complete Node/static gates.
node --test apps/kiosk/test/*.test.mjs
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test
npm --prefix apps/ops-preview run check ; npm --prefix apps/ops-preview test
npm --prefix apps/mapping-config run check ; npm --prefix apps/mapping-config test
npm --prefix packages/mapping-config run check ; npm --prefix packages/mapping-config test
node tools/boundary-scan/boundary-scan.mjs
git diff --check
# 7. Start the Operations Preview host (loopback only, default port 5187).
npm --prefix apps\ops-preview start
# 8. Start the Kiosk shell.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
# 9. Open the Commissioning Wall Map (scroll the page down to it).
# 10. Confirm the complete U-shape is vertically static: Rear, Left, Right and Front all visible in one
#     continuous view at 100% scaling.
# 11. Confirm there is NO vertical scrollbar inside the Wall Map frame.
# 12. Confirm page-level vertical scrolling is the ONLY vertical scrolling surface.
# 13. Confirm fullscreen 1920x1080 has no page-level horizontal overflow
#     (document.scrollingElement.scrollWidth === clientWidth).
# 14. Narrow the window: confirm only the Wall Map frame scrolls sideways, still with no vertical
#     scrollbar inside it and no page-level horizontal overflow.
# 15. Confirm the Overview table still scrolls only inside its own frame.
# 16. Confirm Sensor selection, Front/Rear synchronisation, Sensor detail and the Preview workflow
#     still behave as reviewed.
# 17. Confirm the native Kiosk controls (Exit Kiosk Preview, Windowed / full screen) remain visible.
# 18. Confirm stop/restart reconnect still works (host stopped -> reconnecting state, restarted ->
#     recovers by itself).
# 19. Confirm the final Git status and the complete diff.
git status ; git diff
```

**Report back** the 19-step results, the exact text of any failure, the map's on-screen appearance at
fullscreen and in a narrow window, and the boundary statement shown on screen. A Preview verdict is
never a field approval, and this handoff is not Owner Manual Review.

## 18. Owner-authorized Front/Rear thermal-pair semantics and synthetic diagnostic pass

Same Stage, same branch `arena/3be5c713-waterjet-sentinel-suite`, same **PR #19 (OPEN, NOT MERGED — do
not merge)**, same approved base `main` `48f0315ea1bcbd3f71f46a47bb4b55fc04327487`. This pass starts at
`4d177f614a82f226054e613631c60d37d71c8349` and adds one narrow checkpoint commit whose parent is exactly
that SHA. It changes the **synthetic paired generation**, adds a **read-only paired presentation model**
and its presentation, and adds focused tests; nothing else is reopened.

### 18.1 Owner finding, recorded as received

The Synthetic Preview systematically rendered **Rear above Front** for every Product Sensor pair (the
Owner's example: Front 29.4 / Rear 35.1). Because Front is the furnace-facing fin / heat-collection
measurement and Rear is the paired waterwall-tube-side reference, that inverted the physical reading and
would train the Operator to read the pair backwards. The defect was in the **source**, not in the
rendering: no screen may compensate for a wrong value, so the generator was corrected and the rendered
text was left alone.

### 18.2 Corrected domain rule (Owner-approved)

- `FrontTemperature` is the **furnace-facing fin / heat-collection** measurement of the pair.
- `RearTemperature` is the paired **waterwall-tube-side reference**: comparatively stable under
  comparable operation, and **not a constant** (it varies per Sensor and per physical zone).
- Under a **valid, stable, comparable** condition **Front > Rear**.
- **`FrontRearDelta` = `FrontTemperature` − `RearTemperature`**, in the same compatible unit.
- Interpretation: a healthy **positive** value is the expected gradient; a **reducing positive** value is
  a *candidate* reduced local heat transfer (possible slag insulation); a low positive value does **not**
  prove slag; **near zero** is an abnormal paired response; **reversed** (Front below Rear) is a
  paired-measurement contradiction to be investigated **first** for mapping, Sensor health, installation
  and process transient — never classified as slag by itself.
- Until field baselines and operating regimes are approved, `FrontRearDelta` is **READ-ONLY DIAGNOSTIC /
  TREND ONLY**. No threshold, alarm, trip, interlock, persistence, deadband, hysteresis or automatic slag
  classification is approved or implemented, and the value has **no** Cleaning Queue, GlobalQueue, WaterJet
  dispatch, cleaning job, AutoSequence, pump, isolation-valve, axis-motion, automatic-control or
  field-approval effect.

### 18.3 Exact cause at the source and the corrected generation rule

**Cause.** `syntheticEngineeringValue` (in `packages/commissioning/src/scenarios.mjs`) carried
`const sideBias = row.side === 'F' ? 0 : 4;`, which biased **every** Rear sample about +4 °C above its
Front partner — the Owner's Front 29.4 / Rear 35.1 pair. It was a generation bug, not a rendering bug.

**Correction (deterministic, no RNG, no clock, no rendering-order dependence).** For a Product Sensor
pair, Rear is now the deterministic paired reference
`round1(26 + (sensorOrdinal % 9) · 1.2 + (stableHash(sensorId|rear-wall-reference) % 400)/100)` and Front
is `Rear + differential`, where the differential is the scenario's declared value
(`NORMAL_GOOD`: `round1(3.2 + (hash % 37)/10)`), so **Front > Rear for every complete pair**. Non-pair
rows keep their previous identity-based value. Both sides are **pure functions of the physical identity**
(the FNV-1a hash of the Sensor identity), so the same Sensor renders the same sample at every tick while
the observation timestamp and the value age keep moving with the deterministic Preview clock — the
pre-existing "value is a function of identity" contract is preserved, not weakened. Verified at tick 0
with 106/106 pairs available: `NORMAL_GOOD` deltas **3.2 … 6.8 °C in 34 distinct values** (never one
universal delta for all 106), `REDUCED_FRONT_REAR_DIFFERENTIAL` **0.8 … 1.7**, `NEAR_ZERO…` **0.1 …
0.2**, `REVERSED…` **−3.4 … −1.0**; samples `G+101` Front 38.0 / Rear 31.9 = `+6.1`, `G+205` Front 41.8 /
Rear 37.9 = `+3.9`, `H7` Front 40.3 / Rear 34.9 = `+5.4`; repeat runs are deep-equal and a reversed
schedule order produces identical per-Sensor values.

**Existing scenarios are untouched in behaviour:** they keep their Quality and Diagnostic result and
simply make the delta **unavailable** where a side is invalid. No delta is ever fabricated to keep the UI
populated.

### 18.4 The read-only paired model (`packages/commissioning/src/thermalpair.mjs`, new)

`buildThermalPairs(schedule, observations)` derives one read-only descriptor per Product Sensor (plus a
descriptor per Front/Rear measurement, an unassigned Channel or an I7/I16 conflict row) **from the
authoritative observations on every render**. It never mutates an observation, never becomes a second
source of truth and is never persisted. It exposes: Sensor identity, Front/Rear measurement identity and
current value with validity, `FrontRearDelta`, delta validity, paired diagnostic state, paired diagnostic
reason, synthetic scenario provenance and the field-verification state.

- **States (five, kept strictly separate from Quality, DiagnosticReason, CommissioningState, evidence
  blocking and the Preview verdict — no `INVALID` was added to Quality):** `EXPECTED_POSITIVE` (with no
  clean-wall / no-slag / field-verification claim), `REDUCED_POSITIVE_CANDIDATE`, `NEAR_ZERO_CANDIDATE`,
  `REVERSED_CONTRADICTION`, `DIFFERENTIAL_UNAVAILABLE`.
- **Availability rule:** the delta exists only when both sides belong to one Product Sensor with exactly
  one Front and one Rear measurement, both current values valid, the same compatible unit, neither side
  BAD / STALE / DISABLED / communication-loss / wire-break, both values finite and the identity complete.
  It is never derived from one current plus one Last Valid value, from two historical values, from a
  BAD/STALE/disabled/unassigned value, from an I7/I16 conflict row or from a copied/substituted
  measurement, and there is no fallback, aliasing or derivation across Front and Rear.
- **Fourteen named blocking categories** (FRONT_/REAR_ × current-value-unavailable, quality-not-valid,
  stale, communication-loss, wire-break, plus `UNIT_MISMATCH`, `PAIR_IDENTITY_INCOMPLETE`,
  `NON_SENSOR_GAP`, `UNASSIGNED_CHANNEL`) so an unavailable value always states **which side** broke the
  pair and why.
- **Sign wins over the label:** `delta < 0` is always `REVERSED_CONTRADICTION` and `delta === 0` is always
  `NEAR_ZERO_CANDIDATE`, whatever a scenario declares. `REDUCED_POSITIVE` / `NEAR_ZERO` are assigned by the
  **explicit Synthetic scenario** they declare; no runtime threshold, deadband or inference exists.

**Presentation.** Each Wall Map cell carries one compact read-only paired row (`dFR +6.1 degC[+]`), with
the unit only while the value is available and `dFR -- [?]` with the named reason when it is not — never a
number, never a unit, never a substituted value. The Sensor detail gained a fourth read-only card showing
Sensor pair, Front measurement and current value, Rear measurement and current value,
`dFR = Front − Rear`, paired state with marker, guidance, synthetic scenario provenance,
`FIELD VERIFICATION PENDING` and the explicit no-automatic-effect statement. The Wall Map legend documents
the paired markers, the wall-order note states the read-only nature, and the scenario panel names the
declared paired mode. One render path builds the pairs, so the cell and the detail can never disagree.

### 18.5 Changed / Unchanged / Not verified

**Changed**

| Path | Change |
| --- | --- |
| `packages/commissioning/src/thermalpair.mjs` | **New.** Read-only paired model: five states, fourteen blocking categories, four declared paired modes, marker/guidance/effect vocabularies, `formatFrontRearDelta`, `buildThermalPair`, `buildThermalPairs`, `pairForRow`, `describeUnpairedMeasurement`. No I/O, no clock, no state, never mutates an observation |
| `packages/commissioning/src/scenarios.mjs` | Removed the Rear-up `sideBias`; added the deterministic paired helpers (`syntheticPairedReference`, `syntheticExpectedDifferential`, `syntheticPairedDifferential`, `syntheticPairedFrontTemperature`); added the three explicit paired scenarios; stamped every observation with `scenarioKey` and `pairedDifferentialMode`; `scenarioCatalogue()` now carries `pairedMode` / `pairedDiagnostic` / `pairedNote` |
| `packages/commissioning/src/index.mjs`, `package.json` | Export the new module and helpers; `check` also syntax-checks `src/thermalpair.mjs`; description states thirteen scenarios |
| `packages/commissioning/test/thermalpair.test.mjs` | **New.** 16 focused tests (A1–A4 physics/determinism/generator scope, B1–B2 identity/isolation, C1–C4 delta validity, D1–D4 the three paired scenarios and the no-effect vocabulary, E1–E2 totals/workflow separation) |
| `packages/commissioning/test/scenarios.test.mjs` | The catalogue contract is now "the ten protected scenarios plus the three paired demonstrations" (13), with the protected ten asserted unre­moved and unre­named, and the paired scenarios' mode/honesty assertions added |
| `apps/ops-preview/public/index.html` | The Sensor-detail grid carries the new read-only paired card `#detail-pair`; the Wall Map section states the paired rule and that the value has no Channel, no Sensor identity, no commissioning state and no Preview verdict of its own |
| `apps/ops-preview/public/styles.css` | The cell gains a fixed read-only paired row (cell height 52 → 66 px comfortable, 48 → 61 px compact) with state-coloured text; the paired detail card carries a state accent. Geometry, fonts, wall order and the static vertical scrolling contract are otherwise untouched |
| `apps/ops-preview/public/app.mjs` | Builds the pairs per render from the served observations, renders the cell delta row, the Sensor-detail paired card, the legend entries and the scenario paired line. The Overview gains **no** row and **no** column for the delta |
| `apps/ops-preview/test/thermalpair.render.test.mjs` | **New.** 10 focused presentation/real-page tests (E1–E4 presentation, F1–F2 workflow preservation, G1–G4 served module graph, all-scenario initialization, bounded wording, invalid-side unavailability) |
| `apps/ops-preview/test/page.test.mjs`, `test/server.test.mjs` | Scenario contracts updated to thirteen keys; the served module graph assertion now also requires `/pkg/wallmap.mjs` and `/pkg/thermalpair.mjs` |
| `apps/kiosk/README.md`, `CHANGELOG.md`, `docs/CURRENT_STATE.md`, this checkpoint | This pass recorded; the Wall Map verification paragraph now includes the paired checks and the bounded slag wording |

**Unchanged:** the 240 physical Channels / 216 mapped + 24 unassigned / 212 Product-compatible
measurements / 106 Product Sensors / I7 + I16 `NON_SENSOR_GAP` topology and its single canonical source;
the U-shape geometry, wall order, row order and the static vertical scrolling contract (§17); the
physical-Channel selection identity and every synchronisation; Front/Rear separation and the
Both / Front only / Rear only control; the read-only Preview workflow, its evidence blocking and the
"an internal conflict may record FAIL or PARTIAL but never PASS" rule; loop-test progress; filters;
navigation; the Kiosk shell, the native header layout, WebView2 `1.0.4258.31` and the WinForms-only
reference isolation; every lock file; loopback-only hosting, the approved origin and the strict CSP with
no external assets; the reconnecting fail-safe and the confirmed exit; the protected evidence and the
authoritative workbook. The delta is **not** a physical Channel, a Product-compatible measurement, a
Sensor, a Commissioning measurement or an independent loop-test target — the protected totals stay
240 / 106 / 212 — and it receives no BASELINE / STIMULUS / OPEN_CIRCUIT / RECOVERY observation, no
PASS / FAIL / PARTIAL, no remark and no independent Preview result. Recording or clearing a Front result
does not mutate the Rear result or either source value, and the reverse. **No MODBUS, no physical
acquisition, no contact with `192.0.2.11`-`192.0.2.16`, no TEST_HARDWARE, no PRODUCTION, no device
write, no cleaning or motion command, no queue authority.**

**Not verified:** the field baseline, the real operating delta, the load-normalized relationship, the
Burner-pattern normalization, any threshold, persistence, deadband or hysteresis, the pre/post-cleaning
response, the physical Sensor mapping, hardware acquisition and field verification — all outstanding, and
none of them is claimed. **There is no browser engine and no .NET SDK in Arena**, so **real-browser
execution and the .NET build/tests are NOT RUN IN ARENA**; the tests above are source-level and
stub-DOM proofs, and they are explicitly not a visual verification.

### 18.6 Arena validation at this head

| Gate | Result |
| --- | --- |
| `npm --prefix packages/commissioning run check` | OK (now also syntax-checks `src/thermalpair.mjs`) |
| `npm --prefix packages/commissioning test` | **105 tests — 105 pass / 0 fail** (was 88; +16 focused paired tests, +1 catalogue test) |
| `node --test packages/commissioning/test/thermalpair.test.mjs` | **16 / 16** |
| `npm --prefix apps/ops-preview run check` | OK |
| `npm --prefix apps/ops-preview test` | **88 tests — 88 pass / 0 fail** (was 78; +10 focused paired page/presentation tests) |
| `npm --prefix apps/mapping-config run check` / `test` | OK / 82 pass |
| `npm --prefix packages/mapping-config run check` / `test` | OK / 268 (261 pass / 0 fail / 7 pre-existing Owner-local skips) |
| `node --test apps/kiosk/test/*.test.mjs` | 21 / 21 |
| `node tools/boundary-scan/boundary-scan.mjs` | `0 findings (S1–S9 clean)` |
| `git diff --check` | clean |
| Authoritative workbook SHA-256 | `4e0337e2…f422e8e` unchanged |

**Focused paired-thermal tests: 26** (16 package + 10 page/presentation). **Scenario catalogue: 13**
(ten protected + three paired demonstrations).

### 18.7 Owner-local rerun sequence (28 steps)

```powershell
# 1. Fast-forward to this checkpoint on the existing branch (no branch switch, no reset).
git fetch origin ; git switch arena/3be5c713-waterjet-sentinel-suite ; git pull --ff-only
git log --oneline -3
# 2. Preserve the GENUINE Kiosk lock refresh: the committed lock file is the reviewed one.
git status --porcelain apps/kiosk/packages.lock.json
#    If dotnet asks for a refresh, let step 3 regenerate it and commit ONLY that file, reporting the
#    SHA-256 before and after. Never hand-edit it.
# 3. Locked restore.
dotnet restore WaterJetSentinelSuite.sln --locked-mode
# 4. Zero-warning Release build.  Expect 0 warnings / 0 errors.
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
# 5. Full .NET test run.  Expect 414 passed / 0 failed / 0 skipped.
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore
# 6. Complete Node/static gates.
node --test apps/kiosk/test/*.test.mjs
npm --prefix packages/commissioning run check ; npm --prefix packages/commissioning test
npm --prefix apps/ops-preview run check ; npm --prefix apps/ops-preview test
npm --prefix apps/mapping-config run check ; npm --prefix apps/mapping-config test
npm --prefix packages/mapping-config run check ; npm --prefix packages/mapping-config test
node tools/boundary-scan/boundary-scan.mjs
git diff --check
# 7. Start the Operations Preview host (loopback only, default port 5187).
npm --prefix apps\ops-preview start
# 8. Start the Kiosk shell.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
# 9. Apply NORMAL_GOOD.  For every one of the 106 pairs confirm Front is ABOVE Rear and the paired row
#    shows a POSITIVE, deterministic, per-Sensor-varying value (about +3 to +7 degC) - never one delta
#    for all Sensors.
# 10. Re-apply the same tick and confirm the same values (determinism), then a different tick and confirm
#     the same values with a moved timestamp and value age.
# 11. Apply REDUCED_FRONT_REAR_DIFFERENTIAL: every pair stays positive and the gradient drops; the state
#     reads REDUCED_POSITIVE_CANDIDATE with "possible reduced local heat transfer / possible slag
#     insulation (slag candidate)" and no confirmed-slag wording.
# 12. Apply NEAR_ZERO_FRONT_REAR_DIFFERENTIAL: the gradient is near zero and the state reads
#     NEAR_ZERO_CANDIDATE (abnormal paired response requiring investigation, no approved Product deadband).
# 13. Apply REVERSED_FRONT_REAR_DIFFERENTIAL: Front is below Rear and the state reads
#     REVERSED_CONTRADICTION with mapping / Sensor health / installation / process-transient guidance and
#     "never classified as slag by itself".
# 14. For each of steps 11-13 confirm NO automatic effect: no alarm, no queue entry, no cleaning job, no
#     WaterJet dispatch, no pump or valve action, no motion, no field approval.
# 15. Apply OUT_OF_RANGE, STALE, SIMULATED_WIRE_BREAK and COMMUNICATION_LOSS_NODE: the affected pairs show
#     `dFR --` with the named side and reason, the value has NO number and NO unit, and no Last Valid
#     Value is substituted. Confirm the pair returns to a value only when BOTH sides are valid again.
# 16. Confirm the Wall Map and the Sensor detail agree: selecting a Wall Map position (Front or Rear) and
#     selecting the matching Overview row show the same Front value, Rear value, delta, state and reason.
# 17. Confirm selection identity and synchronisation are unchanged: unassigned Channel selects no Sensor,
#     an I7 or I16 conflict row selects the gap, and the Sensor cell highlights with its exact side.
# 18. Confirm the protected totals are unchanged: 240 physical Channels in the Overview, 212
#     Product-compatible measurements, 106 Product Sensors, 108 Wall Map positions, I7 and I16 as
#     NON_SENSOR_GAP - the paired value adds none of them and appears in no Overview row.
# 19. Confirm the delta is not a loop-test target: it has no BASELINE / STIMULUS / OPEN_CIRCUIT / RECOVERY
#     observation, no PASS / FAIL / PARTIAL, no remark and no independent Preview result.
# 20. Record a Front PASS and confirm the Rear observation, the Rear verdict and the paired value are
#     unchanged; clear the Front result and confirm the same isolation in the other direction.
# 21. Confirm the Wall Map is still a STATIC vertical view (no inner vertical scrollbar) and the paired
#     row did not widen the U-shape or shrink the text illegibly.
# 22. Confirm there is no page-level horizontal overflow at 1920x1080
#     (document.scrollingElement.scrollWidth === clientWidth) and that the Overview table is still the
#     only other sideways scroller.
# 23. Confirm every paired cell is readable at 1920x1080 and in the compact density, and that the unit is
#     visible only while a value exists.
# 24. Confirm the Sensor detail shows Front value, Rear value, dFR = Front - Rear, paired state,
#     reason/guidance, synthetic provenance, FIELD VERIFICATION PENDING and the no-automatic-effect
#     statement.
# 25. Confirm the native Kiosk controls (Exit Kiosk Preview, Windowed / full screen) remain visible.
# 26. Confirm stop/restart reconnect still works (host stopped -> reconnecting state, restarted ->
#     recovers by itself).
# 27. Confirm no uncaught page error appears in any scenario (the failure banner stays hidden).
# 28. Confirm the final Git status and the complete diff.
git status ; git diff
```

**Report back** the 28-step results, the exact text of any failure, the observed Front/Rear values and
deltas for a few named Sensors, the on-screen paired wording for each scenario, and the boundary statement
shown on screen. **A Preview verdict is never a field approval, no paired state is a field verification,
and this handoff is not Owner Manual Review.**
