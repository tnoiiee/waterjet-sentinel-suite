# apps/ui — React production workspace (planned; NOT created in Stage 0.3A-1)

The React production workspace is created in **Stage 0.3A-4** (not authorized
in this checkpoint) as a port of the accepted Stage 0.2.1A Operations visual
baseline. This README reserves the intent and the rules; no npm workspace,
`package.json`, or source file is created by 0.3A-1.

## Port inventory (from the accepted spike, per the Owner Scope Gate)

| Accepted spike asset (`spikes/ui-runtime-react/react-ui/`) | Production home (0.3A-4) |
| --- | --- |
| `src/global.css` (single design-token location) | `apps/ui/src/global.css` — tokens carried verbatim |
| `src/App.tsx`, `src/main.tsx`, `src/fontReady.ts` | ported; Google Sans self-hosting byte-identical (WOFF2 + OFL + TRADEMARKS) |
| `src/components/*` (Operations map, SensorCell, Panels, StatusBar, LegendSwatch, CriticalAlarmModal, DiagnosticsOverlay, CameraPlaceholder) | ported — U-shaped 106-cell map, 1920×1080 F11 layout, critical modal semantics |
| `src/visual/*`, `src/lib/ringBuffer.ts` | ported presentation mapping + bounded trend buffer |
| `src/store/*` (feed/hooks/presentationStore/commands) | **replaced**, not copied: Node-harness assumptions removed; feed retargeted at the .NET Snapshot/Delta API (`127.0.0.1` origin, `wjss.snapshot/1`/`wjss.delta/1`) |
| `src/components/SyntheticTestControl.tsx` | separated as a **dev-flag-gated** panel hitting the dev-only simulator routes (0.3A-2+); never part of the Production UI |
| uPlot 1.6.32 | retained per Owner pinning decision for the future port; bounded to the accepted window (600 points, gaps, setpoint, ready band) |

## Binding rules

1. The UI consumes authoritative Snapshot/Delta contracts only; it owns
   selection and view state only (no queue, job, eligibility, or device state).
2. Reconnect always starts with a fresh full Snapshot; commands are never
   replayed; a revision gap forces re-snapshot.
3. No-color-only state communication is preserved (markers/text accompany
   colour on every state).
4. `spikes/**` is reference material for the port only; no product file may
   import or reference it (dependency-graph test).
5. Tooling retained for this workspace per the Owner gate: TypeScript, Vite,
   Vitest, Playwright (installed-Edge runs are Owner-local), pinned
   `package-lock.json` + offline-cache restore policy.

**Status:** placeholder — no source. Stage 0.3A-4 not authorized.
