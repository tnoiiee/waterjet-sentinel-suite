# ADR-0016 — Kiosk Shell Direction: WinForms Host with WebView2 Detection (Draft)

- **Status:** DRAFT — authored in the Stage 0.3A-1 source checkpoint. Not `PROPOSED`, not
  `ACCEPTED`. Drafts the shell-host half of `[OPEN]` selection 6.7 in
  [`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md).
- **Date:** 2026-10-07
- **Supersedes:** Nothing.
- **Scope:** the application-owned full-screen window that hosts the local web UI: which
  framework wraps the WebView, what it owns, and what Stage 0.3A is allowed to contain.
- **Authority:** Owner Option-C amended Stage 0.3A Scope Gate (drafting authority only).

## Context

ADR-0006 locked the *delivery model*: a kiosk shell window hosting an embedded WebView that
loads static local assets; native desktop is the documented fallback. The windowing wrapper
itself stayed `[OPEN]`. The Stage 0.2.1A spike exercised Edge-as-browser, not an embedded
shell. Stage 0.3A reserves the product's place for the shell — as a compile-only skeleton —
because the shell owns non-trivial responsibilities (startup sequencing against the Runtime,
close guarding, DPI behaviour) that must not be discovered at 0.3A-5 design time.

## Decision (drafted, pending acceptance)

1. **Shell host: WinForms (.NET 10, `net10.0-windows`) as the outer window, hosting the
   WebView2 runtime control.** Chosen over WPF because the shell is one borderless top-level
   window with no composition needs — the minimum surface that satisfies "application-owned
   window with a close guard", and the pair (WinForms shell + WebView2) is what the Owner's
   legacy line-of-business workstations already ship, easing offline servicing.
2. **What the shell owns and nothing else:**
   - starts/stops/awaits the `Wjss.Runtime` process (health-checked before navigation);
   - navigates the WebView to the Runtime-served loopback URL — the shell never parses state;
   - full-screen kiosk presentation + an operator close guard (close requires confirmation
     or a supervisor gesture; the exact gesture is 0.3A-5 detail);
   - crash-restart supervision of the Runtime child process.
   The shell contains **zero domain logic, zero rendering logic**. All UI truth lives in the
   React assets; all state truth lives in the Runtime.
3. **Stage 0.3A-1 containment:** `apps/kiosk` is *compile-only*: window, message loop,
   manifest, DPI awareness — and **nothing else**. The stub performs **no environment
   probing of any kind**: no `Environment.GetEnvironmentVariable` read, no WebView2
   reference of any form, no loader, no fallback chain, no registry probing. (Closeout
   correction 2026-10-07: this item previously described an `Environment.GetEnvironmentVariable`
   detection step that was never present in the validated stub — the review corrections of
   rounds 1–2 removed the probe and the wording had not caught up.) ADR-0013's
   runtime-detection design (fixed name → fixed legacy directory → documented message;
   **no** search walk) is implemented no earlier than Stage 0.3A-4, behind its own Owner
   gate.
4. **Runtime-not-ready UX is in-scope for the shell** (a static local page, no network): the
   operator must be able to tell "Runtime starting / Runtime dead" from "device fault".

## Alternatives considered

- **WPF + WebView2.** Equivalent capability; more framework surface for a single-window
  host. Kept as the documented fallback pairing if a 0.3A-4 measurement (e.g. per-monitor
  DPI behaviour) favours it.
- **WinUI 3 packaging.** Rejected for this deployment class: MSIX/packaging complexity buys
  nothing on a fixed single-user workstation and complicates the ADR-0013 offline install
  story.
- **Edge `--app=` mode as the shell.** What the spike used; not application-owned (no close
  guard, external process lineage). Explicitly not the product shape, though it remains the
  developers' browser-based workflow via the Runtime URL.
- **Electron.** Rejected on ADR-0008 grounds: second runtime to service offline, larger
  attack surface, no .NET process supervision story.

## Consequences

- The repository grows a `net10.0-windows` TFM project (already declared in
  `Directory.Build.props`) that builds only on Windows — CI/Owner matrices must know it.
- When WebView2 is finally added (0.3A-4, gated), it enters `Directory.Packages.props` as a
  *new approved package*; the current minimum set does not include it and 0.3A-1 must not.
- Kiosk UX work in 0.3A-5 inherits close-guard and restart behaviour as testable shell
  requirements, not React requirements.

## Verification status

- `apps/kiosk` source: authored (compile-only trio + manifest). **Compilation: NOT RUN in
  Arena** (no .NET SDK; Owner-local gate per
  [`../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md)).
- WebView2 availability on target workstations: `[NOT VERIFIED]` — no runtime was launched
  anywhere in this checkpoint.
- Close-guard and supervision behaviour: `[NOT IMPLEMENTED]` until 0.3A-4.

## References

[`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md),
[`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md),
[`../ARCHITECTURE.md`](../ARCHITECTURE.md) §14.1,
[`../../apps/kiosk/README.md`](../../apps/kiosk/README.md).
