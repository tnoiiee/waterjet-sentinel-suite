# Wjss.Kiosk — application-owned shell window (Stage 0.3A-1: compile-only skeleton)

The full-screen kiosk shell that hosts the local web UI. Planned host: WinForms
(`net10.0-windows`) wrapping the fixed-name `Microsoft.Web.WebView2.Core` runtime control —
direction drafted in
[`../../docs/decisions/ADR-0016-kiosk-shell-direction.md`](../../docs/decisions/ADR-0016-kiosk-shell-direction.md)
(DRAFT, not accepted).

## What exists here today (and its limits)

| File | Present | State |
| --- | --- | --- |
| `Wjss.Kiosk.csproj` | yes | `WinForms` enabled; **no WebView2 package reference** — none is approved in `Directory.Packages.props` |
| `Program.cs` | yes | message-loop entry; runtime detection = fixed-name `Environment` probe, **detection only** — no loader, no fallback chain, no registry walk (that design is ADR-0013 territory, gated at 0.3A-4) |
| `MainForm.cs` | yes | borderless full-screen window + close-guard placeholder |
| `app.manifest` | yes | DPI awareness + assembly identity |

**Compile-only by contract.** This project must build; it must not navigate, load assets,
spawn the Runtime, or show anything meaningful. No behaviour in here is asserted as working —
`dotnet build` for this TFM has not been executed in Arena (see
[`../../docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](../../docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md)).

## Prohibited in this project until their gates arrive

- Any WebView2 NuGet reference or asset loading (0.3A-4, after Owner acceptance of ADR-0013's
  detection design).
- Any domain/state parsing — all truth flows through the Runtime loopback API only.
- Any production device access — the Kiosk never talks to hardware; it supervises
  `Wjss.Runtime` and points a WebView at its loopback URL.
