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
| `Program.cs` | yes | WinForms message-loop entry only — visual styles, DPI mode, `Application.Run(new MainForm())`. **No WebView2 detection, loading or runtime probing of any kind exists** |
| `MainForm.cs` | yes | Maximized, resizable skeleton window with a placeholder label; **no close guard is implemented** |
| `app.manifest` | yes | DPI awareness + assembly identity |

**WebView2 detection and the close guard belong to later authorized checkpoints** (the
detection design is ADR-0013 territory, gated at Stage 0.3A-4/0.3A-5 per
[`../../docs/decisions/ADR-0016-kiosk-shell-direction.md`](../../docs/decisions/ADR-0016-kiosk-shell-direction.md)).
Nothing in this project performs or emulates them today.

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
