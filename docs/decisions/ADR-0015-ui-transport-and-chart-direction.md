# ADR-0015 — UI Production Transport and Chart Direction (Draft)

- **Status:** DRAFT — authored in the Stage 0.3A-1 source checkpoint. Not `PROPOSED`, not
  `ACCEPTED`. The Owner's selection of **React as the Primary UI Framework** (2026-10-07) is
  already recorded and stands; this draft covers only the two selections that ADR-0006 and
  ADR-0008 deliberately left `[OPEN]`: the Production transport and the chart library.
- **Date:** 2026-10-07
- **Supersedes:** Nothing. Fills `[OPEN]` selections in
  [`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md) and
  [`ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md) once (and only once) the
  Owner accepts this record.
- **Scope:** how the React UI receives state from the product Runtime, and which library
  renders the trend panels.
- **Authority:** Owner Option-C amended Stage 0.3A Scope Gate (drafting authority only).

## Context

The Stage 0.2.1A spike proved, on Owner hardware, a loopback transport: the UI subscribes to
a single HTTP stream from the ASP.NET Core runtime bound to `127.0.0.1`, receives snapshot +
delta payloads, and re-snapshots on gap or disconnect. It met its cadence and recovery
criteria. The protected ADRs recorded the *direction* but kept the transport and chart
library `[OPEN]` because a spike is evidence, not acceptance. Stage 0.3A ports the same
transport semantics into the product contract (`Wjss.Contracts`), making the decision now
cheap and concrete.

## Decision (drafted, pending acceptance)

1. **Transport: loopback HTTP/1.1 with Server-Sent Events**, exactly the spike semantics:
   `GET /api/v1/stream` after a snapshot bootstrap; monotonic per-stream revision; a revision
   gap (`> prev + 1`) means the client MUST re-snapshot (flagged, not fatal); reconnect with
   backoff 250 ms → 4 s; `Last-Event-ID` is advisory, never authoritative. No WebSockets in
   0.3A: SSE survives one-way proxy-free loopback, requires no upgrade handshake, and its
   reconnect semantics are the browser's, not ours.
2. **Bound to loopback only.** Kestrel binds `127.0.0.1` explicitly; all-interface binding is
   prohibited by the architecture (`tools/boundary-scan` rule S6 machine-checks the source).
3. **Chart library candidate: uPlot** (MIT, ~50 kB gz, canvas, no transitive runtime deps),
   vendored as a local static asset — never fetched from a CDN (ADR-0006 offline rule). Its
   bounded-window model matches ADR-0004's ring-buffer historian. This is a *candidate
   selection for acceptance*, not a vendoring authorisation: the actual vendoring lands in
   Stage 0.3A-5 with its own diff; `packages/contracts/wjss-contracts-ts` currently has no
   chart dependency and none is added by 0.3A-1.
4. **UI stack stays React + TypeScript + Vite** as the Owner-selected primary framework;
   nothing in this draft re-opens it.

## Alternatives considered

- **WebSockets loopback.** Stronger model for later bidirectional needs (command streams are
  already plain POSTs; nothing today needs duplex). Rejected *for now*, revisited if command
  throughput or server-push acknowledgements demand it.
- **gRPC-Web.** Requires a proxy layer in the browser path; conflicts with the
  static-assets + loopback simplicity proven in the spike.
- **ECharts / Chart.js.** Heavier (ECharts ≫ 100 kB) or SVG/animation-oriented (Chart.js);
  both fine for dashboards, weaker for the 106-sensor, sub-second wall grid + trends envelope
  measured in the spike. Selection remains the Owner's.

## Consequences

- If accepted, the product UI consumes the ADR-0014 contract unchanged; the transport adds
  only framing (event ids, heartbeat ≤ 5 s) on top of the same JSON payloads.
- Vendoring uPlot later must go through the same offline-boundary rules; no package is added
  to the .NET side for this (Directory.Packages.props stays at the approved minimum).
- Until accepted, both items remain `[OPEN]` in the protected records, and Stage 0.3A-5 must
  build against *this draft* or ask the Owner to rule — silently choosing otherwise is not
  allowed.

## Verification status

- Spike evidence (Owner-local, 2026-10-07): PASS at the described cadence on the Owner
  workstation. Product implementation: **not started** — `apps/runtime` today is the
  health-endpoint skeleton only and streams nothing; the TS mirror validates payload
  *structure*, not transport. Chart library: **nothing rendered in product code yet**.

## References

[`../spikes/stage-0.2.1a-results.md`](../spikes/stage-0.2.1a-results.md),
[`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md),
[`ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md),
[`ADR-0014-product-contract-skeleton.md`](ADR-0014-product-contract-skeleton.md).
