# Stage 0.4C-K1 — Application-Owned Kiosk Shell and Sensor Commissioning Monitor Preview (Development Checkpoint)

**Status: DEVELOPMENT CHECKPOINT, submitted for Owner review in a NEW PR against `main` — OPEN, NOT
MERGED (do not merge). Merge authority is the Owner only.**

**Follow-up (Owner-authorized zero-warning correction pass):** the Owner-local validation reported in
§12 passed everything except exactly one Release-build warning, `MSB3277` (conflicting WindowsBase
assembly versions) caused by the package adding its WPF-only assembly to this WinForms-only project.
§13 records the exact root cause, the narrow project-local correction, the new source-contract coverage
and the honest verification status. No safety, authority, evidence or dependency-version boundary
changed. The checkpoint commit SHAs are recorded
in the pull request and the final report, not inside their own commits (AGENTS §4.6).

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
5. the Scenario selector carries the ten protected scenarios, the Node selector the six nodes, and the
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

Then confirm in the window: the Scenario selector offers ten scenarios; Compact and Comfortable change
the density; Sensor Map and Diagnostics navigate; the Overview shows 240 rows; selecting a row fills the
Sensor detail; the Node communication summary and Commissioning progress are populated; `Apply scenario`
changes the rendered rows; **no initialization error banner is visible**; there is no page-level
horizontal overflow (`document.scrollingElement.scrollWidth === clientWidth`); the Overview table remains
the only thing that scrolls sideways; and stopping/restarting the host still shows the shell's
reconnecting state and recovers by itself. If initialization ever fails, the banner states the category
on screen — report that text rather than a stack trace.
