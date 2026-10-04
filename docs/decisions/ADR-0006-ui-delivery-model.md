# ADR-0006 — UI Delivery Model

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record closes the UI delivery question left `[OPEN]` by
  [ADR-0001](ADR-0001-product-identity.md) item 10 and by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11.
- **Scope:** How the Operations UI, the Control Room Kiosk presentation, the responsive
  workstation pages, and the Engineering pages are delivered on the workstation, and what
  the UI process may and may not own.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.1, together with the mandatory boundaries in section 8.3 of the same
  gate.

---

## Context

The approved baseline fixes the operating mode, not the delivery technology. IDN-005 requires
a full-screen Control Room Kiosk with controlled navigation, IDN-006 requires
workstation-responsive layouts, and UIG-001 through UIG-006 require the **normal Operations UI
close action** to be blocked while a Cleaning Job is active or the Main Pump is running, with
a clear explanation and a redirect back to the active operation. IDN-012 states explicitly
that product identity does not decide the delivery technology, and that browser-based,
desktop, and hybrid local-web delivery all remained available.

The forces that must be weighed:

- The close guard is a requirement on the *normal close action of the application window*.
  A component that does not own that window cannot satisfy it.
- Controlled navigation means the interface must not become a general-purpose browser.
- The workstation is offline, standalone, and shared with SQL Server, the runtime service,
  and the kiosk shell; there is no Internet access for runtime downloads or asset fetch.
- The UI must not own device sessions, must not write to hardware directly, and must not
  hold device state. UI restart must not determine equipment state.
- A small development team must maintain the system for years, including data-dense process
  pages, trends, alarm lists, and queue views.
- Offline packaging must not depend on an Internet-reachable package ecosystem at
  installation time.

## Decision

1. **Kiosk shell owns the window.** The Operations UI is displayed inside a full-screen
   **application-owned kiosk shell window**. The shell owns the top-level window, applies the
   close guard, and is the only supported production presentation path.
2. **Close guard is enforced by the shell.** The shell must intercept the normal window close
   action, refuse it while a Cleaning Job is active or the Main Pump is running, display a
   clear explanation, and direct the Operator back to the active operation or Pump/Sequence
   state (UIG-001 through UIG-003). The guard remains an operational usability control and is
   never presented as protection (UIG-005, UIG-006).
3. **Controlled navigation.** The shell runs full-screen without browser chrome, without an
   address bar, and without general-purpose browsing. Navigation is restricted to the
   application's own page set. External navigation is not offered.
4. **The UI is a local web UI implemented in C#.** The page layer uses a Blazor-based
   component model so that the implementation stays in one toolchain, and so that offline
   builds do not depend on a second package ecosystem that requires Internet access to
   restore. The rendering runs in the shell process through an in-process .NET web view
   hosting model; no per-start runtime payload is downloaded.
5. **The UI is presentation-only.** The UI process:
   - must not own any device session;
   - must not write to hardware directly;
   - must not connect to the SQL Server database directly;
   - must not compute queue eligibility, sequencing decisions, or permission decisions;
   - submits **control requests** only, and reads all state through the Local Application API
     defined by [ADR-0007](ADR-0007-runtime-process-model.md).
6. **Engineering pages are part of the same application.** Engineering configuration pages
   are pages of the same UI application under the same shell. No separate engineering web
   site is exposed, and no additional listening surface is introduced beyond the loopback API
   boundary recorded in [ADR-0007](ADR-0007-runtime-process-model.md).
7. **Development and test display is permitted, production kiosk display is not replaced.**
   The same UI assets may be opened in a local browser against a simulator-backed runtime for
   development, engineering diagnostics, and UI automation. Browser display is a development
   and test path only; the production path is the kiosk shell, and the close guard is enforced
   by the shell.
8. **Transparency in the chrome.** The shell must display the active device profile
   (`SIMULATOR`, `TEST_HARDWARE`, or `PRODUCTION`) and the active published configuration
   revision identifier, so that simulated data can never be mistaken for plant data. See
   [ADR-0012](ADR-0012-simulator-first-development.md).
9. **The shell host framework is an implementation-level detail** (`[OPEN]`), constrained by
   Windows 11 Pro support, offline packaging, and the ability to host the chosen web view
   in-process.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Browser kiosk: the runtime serves the UI and the workstation opens it in a locked-down browser | Kiosk lockdown, exit blocking, and navigation control would be owned by the browser vendor's configuration, not by the application. The application cannot intercept the browser window's normal close action, cannot present the required explanation and redirect reliably, and is exposed to browser version drift on the workstation image. This is a requirement failure (UIG-001 through UIG-003, IDN-005), not a convenience objection. | Rejected |
| Native desktop UI (WPF or WinUI) | Viable: owns its window, needs no web view runtime, offline by construction. Not selected as the baseline because responsive, data-dense process pages, trend rendering, and UI automation tooling would require materially more bespoke work for the same usability, and the shell framework would carry the whole presentation burden. Retained as the documented **fallback** if the in-process web view cannot be guaranteed on the target image. | Not selected — retained as fallback |
| Local web UI without an application-owned shell (plain browser tab) | Same close-guard failure as the browser kiosk alternative, with less lockdown. | Rejected |
| Single-process desktop application that also hosts the Equipment Runtime | The UI lifecycle would determine equipment sessions and process state. Directly contradicts the approved mandatory boundary that only the approved Equipment Runtime boundary may own physical device sessions, and defeats failure isolation. | Rejected |
| Central or remote web UI served from a server | Contradicts one-installation-per-Boiler-Unit, standalone operation, and the offline requirement. | Rejected |
| Console or terminal-style UI | Fails kiosk usability, trends, alarm presentation, and queue interaction requirements. | Rejected |
| Web UI served by the runtime service to the shell over loopback, rendered server-side in the runtime process | Would place UI rendering workload and UI faults in the process that also owns equipment sessions, weakening failure isolation. Not needed, because the shell can host the rendering itself. | Rejected |

## Consequences

- The close guard and controlled navigation are enforceable because the application owns the
  window; both remain operational usability controls with no protective authority.
- The kiosk shell becomes a supervised deployment unit: if it fails, the Operator loses
  visibility while the runtime service keeps the equipment session alive. Shell recovery
  supervision and an explicit "operator interface unavailable" state are required.
- A web view runtime becomes a deployment dependency. Its presence, version, servicing
  channel, and offline installation path on the Windows 11 Pro image are `[NOT VERIFIED]` and
  must be resolved before the offline deployment model in
  [ADR-0013](ADR-0013-offline-deployment.md) is finalised.
- Because the UI holds no authority, closing or crashing the UI cannot by itself change
  equipment state, but it also cannot be relied on to stop anything. The close guard is not an
  acceptable sole protection against an energized output (HSB-004).
- One page implementation serves both the kiosk path and the development/test path, which
  reduces divergence; the two paths must not diverge in validation, because validation lives
  in the runtime, not in the page.
- Deferring the shell host framework keeps one small decision open without blocking the
  architecture.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Web view runtime absent, mismatched, or unserviceable on the target image | Kiosk cannot start offline | Verify the image; bundle a pinned offline runtime if it cannot be guaranteed; native desktop fallback remains available | `[NOT VERIFIED]` |
| Shell crash while a Cleaning Job is active | Operator loses visibility | Supervise and restart the shell; raise a visibility alarm; runtime continues and holds state | `[PROPOSED]` |
| Close guard mistaken for protection | False safety belief | UIG-005/UIG-006 wording retained in every document that mentions it | `[OWNER CONFIRMED]` |
| Contrived navigation escape from the kiosk page set | Uncontrolled operator screen | Shell navigation allowlist; verification case in the planned UI test set | `[PROPOSED]` |
| Web UI assets and runtime diverging across versions | Undiagnosable behaviour | Single offline package carries both; version identity displayed in the shell chrome | `[PROPOSED]` |

## Verification status

- `[NOT VERIFIED]`: every runtime property of this decision. No UI exists, no shell exists, no
  close guard has been exercised, and no kiosk behaviour has been observed.
- `[NOT VERIFIED]`: presence, version, and servicing behaviour of the web view runtime on the
  target Windows 11 Pro image.
- `[NOT VERIFIED]`: suitability of the chosen rendering model for the trend and alarm load of
  208 thermocouple channels; no performance measurement exists.
- `[OPEN]`: shell host framework selection.
- Nothing in this record authorises Production Write, device access, or UI implementation.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Shell host framework selection | Implementation Stage Gate before UI code is created |
| Web view runtime availability and offline installation path | Deployment and offline packaging Stage Gate, with workstation image evidence |
| Shell supervision and recovery policy | Implementation Stage Gate (runtime/service supervision) |
| Kiosk lockdown mechanism on Windows 11 Pro | Deployment Stage Gate |
| Execution of the planned UIG test cases | UI test Stage Gate — cases remain planned, not executed |

## Relationship to protected decisions

- **Preserved, not modified:** kiosk operating mode (IDN-005), responsive workstation pages
  (IDN-006), the Operations UI close guard and its non-safety status (UIG-001 through
  UIG-006), one installation per Boiler Unit (IDN-004), offline standalone operation
  (IDN-009), and the prohibition on describing the system as safety-rated (IDN-010).
- **Implements an approved mandatory boundary:** the UI must not write to hardware directly
  and must not own device sessions (approved Stage 0.2 Scope Gate, section 8.3).
- **Unchanged:** Production Write remains `[NOT AUTHORIZED]`; WAGO fail-safe remains
  `[NOT VERIFIED]`; no concurrency of Cleaning Jobs is enabled or implied.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this technology choice.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — process and ownership model (section 14)
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — IDN, UIG, and ARC requirement groups
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned UI verification cases
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md)
- [`ADR-0012-simulator-first-development.md`](ADR-0012-simulator-first-development.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
