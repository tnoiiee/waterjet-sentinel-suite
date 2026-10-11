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
