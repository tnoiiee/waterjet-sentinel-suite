# Wjss.Kiosk — application-owned shell (Stage 0.4C-K1)

The full-screen, application-owned Windows window that hosts the local Operations preview route.
It is a real shell now: WinForms (`net10.0-windows`) wrapping the fixed-name embedded web-view
runtime control **`Microsoft.Web.WebView2`, pinned at `1.0.4258.31`** in
[`../../Directory.Packages.props`](../../Directory.Packages.props) under the Stage 0.4C-K1 Owner
dependency decision (2026-10-12). The Kiosk project references it **without a version**, so the
central pin stays the single source of the version.

The shell holds **no control authority**: it starts nothing, commands nothing, parses no Product
state, keeps no credential and writes nothing except its own embedded-view runtime cache. The local
preview host is a separate process that the Operator starts and stops.

## What this window does (Stage 0.4C-K1)

| Behaviour | How |
| --- | --- |
| Application-owned window, no browser chrome | WinForms borderless top-level window; the embedded view has no address bar, no tab strip and no browser UI |
| Opens the local route automatically | `KioskShellOptions.ApprovedOrigin` (scheme, loopback host and port 5187) is set on the view as soon as it is ready |
| Full-screen / kiosk presentation | Borderless, maximized by default (`FormBorderStyle.None`); the **Windowed / full screen** button switches to a normal resizable window, because this is a development preview and not an OS lockdown |
| 1920×1080 at 100% scaling, DPI-safe | `ApplicationHighDpiMode=PerMonitorV2` (project property, the WFO0003 correction) + `AutoScaleMode.Dpi` + `ZoomFactor = 1.0` |
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
#    127.0.0.1 only. Default port 5187.
node apps/ops-preview/server.mjs

# 3. Confirm the host answers before starting the shell.
#    Browse to http://127.0.0.1:{port}/api/health  (the default port is 5187; the /api/boundary
#    route shows the same Stage boundary statement the window displays)

# 4. Start the Kiosk shell.
dotnet run --project apps/kiosk/Wjss.Kiosk.csproj -c Release
#    or run the built Wjss.Kiosk.exe from apps/kiosk/bin/Release/net10.0-windows/
```

**Port match:** the host defaults to `5187` and the shell defaults to the same port. If the host is
started with `OPS_PREVIEW_PORT=<port>`, start the shell with `$env:WJSS_KIOSK_UI_PORT=<port>` too.
The **host** is never configurable; only the port can match.

**What to verify in the window:** the boundary statement is visible; the Operations route renders at
1920×1080 with 100% scaling and no browser chrome; `F12` / `Ctrl+Shift+I` do nothing; navigating
away (or opening a new window) is refused and increments the blocked counters; stopping the host
switches the window to the reconnecting screen within about two seconds with **no stale values left
on screen**; restarting the host brings the route back automatically; and **Exit Kiosk Preview**
asks for confirmation before closing.

**Stopping:** close the shell through **Exit Kiosk Preview** (confirmed), then stop the host with
`Ctrl+C` in its terminal.

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
