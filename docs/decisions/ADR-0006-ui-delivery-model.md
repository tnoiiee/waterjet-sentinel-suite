# ADR-0006 — UI Delivery Model

- **Status:** ACCEPTED — recorded by the Owner at Stage 0.2 acceptance (merged through PR #2,
  merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`). Accepted as architecture
  direction; **accepted does not mean implemented**. Selections marked `[PROPOSED]`,
  `[OPEN]`, or `[NOT VERIFIED]` below keep those markers. **The final UI framework
  remains `[OPEN]`**; React final selection is not yet approved (Stage 0.2.1A feasibility
  spike in progress); the Blazor counter-spike is deferred / not authorized.
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-requested Stage 0.2 documentation
  review punchlist)
- **Supersedes:** Nothing. This record closes the UI delivery question left `[OPEN]` by
  [ADR-0001](ADR-0001-product-identity.md) item 10 and by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11. It does **not** close the UI
  framework question, which remains `[OPEN]`.
- **Scope:** How the Operations UI, the Control Room Kiosk presentation, the responsive
  workstation pages, and the Engineering pages are delivered on the workstation; the
  framework candidates; and what the UI process may and may not own.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.1, together with the mandatory boundaries in section 8.3 of the same
  gate, as refined by the Owner-requested Stage 0.2 documentation review punchlist.

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

### Owner evidence from the legacy application — architecture input

The Owner provided direct operating experience from the previous implementation. That
application used C#, .NET Framework, WinForms, a WPF-hosted grid module, NModbus, SQL Server,
and a desktop graph library. Observed problems included slow Modbus polling across
approximately ten devices; delayed and resource-intensive UI operation; a graph library that
produced lag, freezes, and crashes; a WPF sensor grid consuming significant CPU when refreshed
continuously; repeated SQL parameter queries delaying the program; and limited UI interaction
while polling, database access, rendering, and graph updates all occurred in the same
application architecture. The Owner later observed better responsiveness with React, Vite, a
backend reading Modbus data in batches, WebSocket-style push delivery, and partial frontend
updates.

**This is recorded as an architecture input, not as a controlled benchmark.** The correct
interpretation is:

1. Push delivery does not make Modbus itself faster.
2. The likely advantage came from **batch acquisition, concurrent per-device polling,
   in-memory operational state, push delivery, reduced SQL reads, and partial UI rendering**.
3. The previous problems must not be attributed only to C#, WinForms, WPF, NModbus, or
   SQL Server. The legacy application concentrated acquisition, database access, rendering,
   and graph updates in one architecture in which they could block one another.
4. The consequence for this architecture is a requirement, not a framework choice: **I/O, SQL,
   Historian writes, graph updates, and UI rendering must not block one another**, and the UI
   must not be the performance bottleneck.

### Correction recorded by the documentation review

An earlier draft of this record selected a Blazor-based component model as the UI technology.
The Owner has ruled that selection premature. The framework decision is therefore returned to
`[OPEN]`, both candidates must be evaluated fairly inside the same architecture, and the final
selection requires a future constrained technology spike (proposed Stage 0.2.1). **Blazor is
not rejected, and React is not accepted.**

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
4. **Locked: the UI is a local web UI hosted inside the application-owned shell window.**
   The UI is a local web application rendered in the shell's embedded WebView, loading static
   local assets from the installed application directory. No UI assets are fetched from a
   network, and no remote web server is involved.
5. **The final UI framework is `[OPEN]`.** Two candidates must be evaluated inside the same
   architecture — the application-owned kiosk shell, an embedded WebView, a loopback
   ASP.NET Core Local Application API, and a separate .NET Equipment Runtime Windows Service:
   - **Candidate A: React + TypeScript + Vite**, built as static local assets served to the
     embedded WebView.
   - **Candidate B: Blazor Hybrid**, rendered in-process in the shell window.
   Neither candidate may be compared against a general-purpose external browser, and neither
   may be selected, rejected, or excluded on the basis of assumed Internet access.
6. **Current evidence-based preference: React + TypeScript + Vite.** This preference is
   recorded for transparency and is **not final acceptance** and not an approved decision. The
   evidence behind it:
   - direct positive Owner operating experience with React/Vite and push-based updates;
   - strong fit for dense visualisation;
   - a strong chart and browser UI ecosystem;
   - natural camera and web-content integration;
   - a strong UI and component testing ecosystem;
   - clear frontend/backend process separation, which suits the loopback-API boundary.
7. **Blazor Hybrid remains a fully open candidate.** Its genuine advantages — one language and
   toolchain, fewer build ecosystems, direct interop with .NET code in the shell process, and
   a lower number of artefacts to acquire offline — are real but **do not by themselves prove
   UI performance, graph quality, camera integration, or long-running kiosk stability**. Those
   claims require measurement, not language preference.
8. **Offline correction (binding clarification).** React can be built and deployed offline.
   React introduces a second package and build ecosystem — Node.js/npm in addition to NuGet —
   which increases offline dependency-management and supply-chain effort but does **not** make
   offline development or deployment impossible. Blazor reduces the number of build
   ecosystems, but that reduction is not by itself a performance, visualisation, or stability
   argument. Offline build, offline dependency restoration, package licensing, and package
   footprint must be measured for both candidates (see [ADR-0013](ADR-0013-offline-deployment.md)).
9. **The UI is presentation-only.** The UI process:
   - must not own any device session;
   - must not write to hardware directly;
   - must not read Modbus data directly;
   - must not connect to the SQL Server database directly;
   - must not query SQL for live operational state;
   - must not compute queue eligibility, sequencing decisions, or permission decisions;
   - must not own Queue or Cleaning Job state;
   - must not infer device state independently from the runtime;
   - submits **control requests** only, and reads all state as presentation state delivered by
     the runtime through the Local Application API defined by
     [ADR-0007](ADR-0007-runtime-process-model.md).
10. **Engineering pages are part of the same application.** Engineering configuration pages
    are pages of the same UI application under the same shell. No separate engineering web
    site is exposed, and no additional listening surface is introduced beyond the loopback API
    boundary recorded in [ADR-0007](ADR-0007-runtime-process-model.md).
11. **Development and test display is permitted; production kiosk display is not replaced.**
    The same UI assets may be opened in a local browser against a simulator-backed runtime for
    development, engineering diagnostics, and UI automation. Browser display is a development
    and test path only; the production path is the kiosk shell, and the close guard is enforced
    by the shell.
12. **Transparency in the chrome.** The shell must display the active device profile
    (`SIMULATOR`, `TEST_HARDWARE`, or `PRODUCTION`) and the active published configuration
    revision identifier, so that simulated data can never be mistaken for plant data. See
    [ADR-0012](ADR-0012-simulator-first-development.md).
13. **The shell host framework remains an implementation-level detail** (`[OPEN]`), constrained
    by Windows 11 Pro support, offline packaging, and the ability to host the selected web
    view in-process.

### Fair comparison inside the same architecture

Both candidates are compared inside the identical architecture. No candidate is compared
against an external browser. All assessments below are **design-level and unmeasured**; every
performance-related row must be settled by measurement in the proposed Stage 0.2.1 spike.

| Evaluation dimension | Candidate A — React + TypeScript + Vite | Candidate B — Blazor Hybrid | Note |
| --- | --- | --- | --- |
| 106 live Sensor cells | Component-per-cell model with keyed rendering supports partial updates | Component-per-cell model with diff-based rendering supports partial updates | Both must demonstrate the one-second target; neither is assumed |
| One-second update target | Must be measured under the spike workload | Must be measured under the spike workload | Acceptance measure, not a claim |
| Efficient partial updates | Explicit changed-state application; only affected cells re-render | Rendered diff over the component tree; only changed output is applied | Both are viable in principle; cost profile differs and is unmeasured |
| Multiple visual states per Sensor | Class/attribute-driven styling; states compose naturally | Same, expressed in C# components | Presentation model is framework-neutral (see [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 26) |
| Trend rendering | Mature canvas/SVG charting ecosystem with bounded-window and downsampling support | Fewer established options; rendering via JS interop or .NET drawing | Library selection `[OPEN]` for both |
| Camera integration | Direct browser media and video-element integration inside the WebView | Requires interop or embedded native hosting | Requirement is camera panel integration, not a specific transport |
| Data grids and Queue tables | Mature virtualised grid components | Mature commercial/free .NET grids; interop where needed | Must support column updates without full rebuild |
| Alarm visualisation | Straightforward; alarm semantics remain runtime-owned | Straightforward; alarm semantics remain runtime-owned | Alarm state must remain independently recognisable (section 27) |
| Responsive workstation layout | Standard CSS layout and container queries | Standard CSS layout; .NET component composition | Both must support workstation-responsive pages (IDN-006) |
| Kiosk-shell integration | Shell hosts WebView and owns window, close guard, and navigation allowlist | Shell hosts the .NET UI directly; interop for shell services | Both satisfy the shell-owned close guard; the difference is interop depth |
| UI crash and reconnect | Web UI reload re-requests a full snapshot | Same requirement; component tree re-initialises | Reconnect rules are contract-level (ADR-0007) |
| Memory stability over long operation | Depends on virtualisation discipline and bounded buffers | Depends on renderer and buffer discipline | Must be measured over extended operation |
| Component testing | Strong ecosystem of component and DOM testing tools | Testing available within the .NET toolchain | Test tooling choice is `[OPEN]` |
| Visual regression testing | Mature screenshot-diff tooling | Possible; tooling options narrower | Planned UI verification cases |
| Accessibility | Strong ecosystem support and tooling | Support available | Requirement level not yet ratified |
| Offline build | Fully possible offline; requires a prepared local npm cache/mirror or vendored packages | Offline by default within the .NET toolchain | **React does not require Internet access to build or deploy** |
| Offline dependency restoration | Second ecosystem to mirror, pin, and audit | One ecosystem | Both must restore offline from local sources |
| Package licensing | Large dependency tree; licence inventory required | Smaller dependency surface; licence inventory still required | No licence claim is made in this Stage |
| Package footprint | Runtime is the WebView plus local assets; node toolchain is build-time only | Runtime is the .NET UI stack; SDK is build-time only | Must be measured for both |
| Development effort | Requires a second language and build pipeline | Single language; no separate frontend toolchain | Effort is a real factor, recorded but not decisive |
| Long-term maintainability | Large, fast-moving ecosystem; version churn must be managed | Single toolchain; framework release cadence still applies | Both need a version pinning policy |
| Single-developer support | One developer can support it, but two toolchains must be kept current | One developer supports one toolchain | Neither candidate is disqualified |
| Contract generation | Typed client can be generated from the Local API contract | Typed client generation available in .NET | Contract generation approach is `[OPEN]` |
| Shell-to-UI interop | Requires an explicit bridge for shell services (close guard, focus, print, camera) | Direct in-process interop | A bridge must be defined and tested for Candidate A |

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Browser kiosk: the runtime serves the UI and the workstation opens it in a locked-down browser | Kiosk lockdown, exit blocking, and navigation control would be owned by the browser vendor's configuration, not by the application. The application cannot intercept the browser window's normal close action reliably. This is a requirement failure (UIG-001 through UIG-003, IDN-005), not a convenience objection. | Rejected |
| Native desktop UI (WPF or WinUI) | Viable: owns its window, needs no web view runtime, offline by construction. Not selected as the baseline because responsive, data-dense process pages, trend rendering, and UI automation tooling would require materially more bespoke work, and the heavy grid-based desktop approach is implicated in the legacy performance problems. Retained as the documented **fallback** if no web view can be guaranteed on the target image. | Not selected — retained as fallback |
| Local web UI without an application-owned shell (plain browser tab) | Same close-guard failure as the browser kiosk alternative, with less lockdown. | Rejected |
| Single-process desktop application that also hosts the Equipment Runtime | The UI lifecycle would determine equipment sessions and process state. Directly contradicts the approved mandatory boundary that only the approved Equipment Runtime boundary may own physical device sessions, and defeats failure isolation. | Rejected |
| Central or remote web UI served from a server | Contradicts one-installation-per-Boiler-Unit, standalone operation, and the offline requirement. | Rejected |
| Console or terminal-style UI | Fails kiosk usability, trends, alarm presentation, and queue interaction requirements. | Rejected |
| Web UI served by the runtime service to the shell over loopback, rendered server-side in the runtime process | Would place UI rendering workload and UI faults in the process that also owns equipment sessions, weakening failure isolation. Not needed, because the shell can host the rendering itself. | Rejected |
| **Selecting the final UI framework in this Stage** | The available evidence is the Owner's legacy operating experience plus ecosystem knowledge. Neither is a controlled comparison of both candidates on this workload. Selecting now would invent certainty. | Rejected — framework returned to `[OPEN]`, decision deferred to the proposed Stage 0.2.1 spike |
| **Excluding React because "a browser is easy to close" or because it is assumed to need Internet access** | Both statements are wrong in this architecture. The shell owns the window and the close guard; React can be built and deployed offline as local static assets. Rejecting a candidate on either ground would be a reasoning error. | Rejected as a basis for exclusion |
| **Selecting Blazor solely because it uses C# and reduces build ecosystems** | A single language and a single toolchain are genuine advantages, but they do not demonstrate the one-second UI target, trend quality, camera integration, or long-running kiosk stability. Those must be measured. | Rejected as a basis for selection |

## Consequences

- The close guard and controlled navigation are enforceable because the application owns the
  window; both remain operational usability controls with no protective authority.
- The kiosk shell becomes a supervised deployment unit: if it fails, the Operator loses
  visibility while the runtime service keeps the equipment session alive. Shell recovery
  supervision and an explicit "operator interface unavailable" state are required.
- A web view runtime becomes a deployment dependency for **both** candidates. Its presence,
  version, servicing channel, and offline installation path on the Windows 11 Pro image are
  `[NOT VERIFIED]` and must be resolved before the offline deployment model in
  [ADR-0013](ADR-0013-offline-deployment.md) is finalised.
- Because the UI holds no authority, closing or crashing the UI cannot by itself change
  equipment state, but it also cannot be relied on to stop anything. The close guard is not an
  acceptable sole protection against an energized output (HSB-004).
- One page implementation serves both the kiosk path and the development/test path, which
  reduces divergence; the two paths must not diverge in validation, because validation lives
  in the runtime, not in the page.
- The framework remains undecided, so a small amount of UI-tooling work is deliberately
  deferred. This is accepted: the boundary design (shell, API, presentation state) is
  framework-neutral, so deferring the framework does not block the architecture.
- Candidate A adds a second, fast-moving package ecosystem whose offline mirroring, version
  pinning, and licence inventory become operational obligations.
- Candidate B keeps one toolchain, at the cost of a narrower visualisation, camera, and UI-test
  tooling ecosystem and a rendering model whose per-update cost must be proven.
- Either way, the legacy performance lesson is enforced by architecture (presentation state
  pushed as deltas, no UI-side polling, no UI-side database access) rather than by framework
  choice.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Web view runtime absent, mismatched, or unserviceable on the target image | Kiosk cannot start offline | Verify the image; bundle a pinned offline runtime if it cannot be guaranteed; native desktop fallback remains available | `[NOT VERIFIED]` |
| Spike deferred or skipped, framework chosen informally later | A framework is adopted without evidence, and the legacy performance failure returns | The proposed Stage 0.2.1 spike is the only authorised selection route; no UI code may be created before a framework decision | `[PROPOSED]` |
| Candidate A's second ecosystem drifts or cannot be restored offline | Non-reproducible offline builds | Pinned versions, committed dependency manifest and lock file, prepared local mirror or vendored cache | `[OPEN]` |
| Candidate B's per-update rendering cost is too high at 106 cells and one-second cadence | UI lags, defeating the legacy correction | Measured acceptance threshold in the spike; partial-update and virtualisation requirements are binding regardless of framework | `[NOT VERIFIED]` |
| Shell crash while a Cleaning Job is active | Operator loses visibility | Supervise and restart the shell; raise a visibility alarm; runtime continues and holds state | `[PROPOSED]` |
| Close guard mistaken for protection | False safety belief | UIG-005/UIG-006 wording retained in every document that mentions it | `[OWNER CONFIRMED]` |
| Camera integration assumed to be simple in one candidate | Late rework in the UI | Camera panel is an explicit spike workload for both candidates | `[PROPOSED]` |

## Verification status

- `[NOT VERIFIED]`: every runtime property of this decision. No UI exists, no shell exists, no
  close guard has been exercised, and no kiosk behaviour has been observed.
- `[OPEN]`: **the final UI framework** (Candidate A versus Candidate B), the shell host
  framework, the chart/trend library, the push transport, and the UI test tooling.
- `[NOT VERIFIED]`: presence, version, and servicing behaviour of the web view runtime on the
  target Windows 11 Pro image; suitability of either candidate for the 106-cell one-second
  workload; memory stability over extended operation; camera integration.
- No candidate is rejected. No candidate is accepted. The recorded preference for
  React + TypeScript + Vite is an evidence-based current position, not an approval.
- Nothing in this record authorises Production Write, device access, or UI implementation.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| **Final UI framework selection — React + TypeScript + Vite versus Blazor Hybrid** | **Proposed Stage 0.2.1 — UI and Runtime Technology Spike** (PROPOSED, NOT AUTHORIZED). Update: the narrower Stage 0.2.1A synthetic React feasibility spike is approved and in progress; React final selection not yet approved; Blazor counter-spike deferred / not authorized |
| Shell host framework selection | Implementation Stage Gate before UI code is created |
| Web view runtime availability and offline installation path | Deployment and offline packaging Stage Gate, with workstation image evidence |
| Shell supervision and recovery policy | Implementation Stage Gate (runtime/service supervision) |
| Kiosk lockdown mechanism on Windows 11 Pro | Deployment Stage Gate |
| Chart/trend library and push transport | Stage 0.2.1 spike, then Implementation Stage Gate |
| Execution of the planned UIG test cases | UI test Stage Gate — cases remain planned, not executed |

## Relationship to protected decisions

- **Preserved, not modified:** kiosk operating mode (IDN-005), responsive workstation pages
  (IDN-006), the Operations UI close guard and its non-safety status (UIG-001 through
  UIG-006), one installation per Boiler Unit (IDN-004), offline standalone operation
  (IDN-009), and the prohibition on describing the system as safety-rated (IDN-010).
- **Implements approved mandatory boundaries:** the UI must not write to hardware directly,
  must not own device sessions, must not own Queue or Cleaning state, and must not infer device
  state independently from the runtime (approved Stage 0.2 Scope Gate, section 8.3).
- **Unchanged:** Production Write remains `[NOT AUTHORIZED]`; WAGO fail-safe remains
  `[NOT VERIFIED]`; no concurrency of Cleaning Jobs is enabled or implied. The legacy
  application evidence does not modify any protected decision.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit any framework candidate.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — process and ownership model (section 14), legacy
  evidence (section 23), UI framework candidates (section 24), UI workload (section 25), sensor
  presentation model (section 26), quality pipeline (section 27), live-state delivery
  (section 28), and the proposed spike (section 33)
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — IDN, UIG, UIW, SPC, DQS, LSD, and ARC groups
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned UI and spike verification
- [`../MASTER_PLAN.md`](../MASTER_PLAN.md) — proposed Stage 0.2.1
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md)
- [`ADR-0012-simulator-first-development.md`](ADR-0012-simulator-first-development.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
