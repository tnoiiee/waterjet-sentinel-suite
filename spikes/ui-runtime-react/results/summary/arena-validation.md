# Stage 0.2.1A — Arena validation summary

> SYNTHETIC SPIKE VALIDATION - ARENA (Linux, Node) ONLY - NO BROWSER RESULTS

Recorded: 2026-10-05T13:59:24.000Z · Base: `e779f8ad2c856e367fd65985007a3da411bd0e73` · Parent checkpoint: `935973e6` · Scope: Stage 0.2.1A fullscreen Operations UI refinement (Owner-approved Design Addendum; 1920 x 1080 Edge F11 target). Sensor domain unchanged: 106 Sensor locations / 212 Thermocouple channels

| Check | Result | Detail |
| --- | --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund (PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1) | PASS | exit 0; package-lock.json unchanged; no browser download |
| tsc --noEmit (TypeScript 6.0.3) | PASS | 0 errors |
| vite build (production) | PASS | JS 307.27 kB (gzip 102.61 kB), CSS 17.45 kB (gzip 4.83 kB) |
| vitest (jsdom) 12 files | PASS | 75/75 (15 new: layoutTokens 7, fullscreenLayout 8) |
| runtime-harness node:test 2 files | PASS | 26/26 (sensorMap 12) |
| scenario runner (28 scenarios) | PASS | PASS 25, PASS+OWNER 2, OWNER-LOCAL 1, FAIL 0 |
| golden fixtures validated against canonical map | PASS | fixtures unchanged; fixtures.test 6/6 |
| playwright test --list (no browser) | PASS | 22 tests in 3 files (5 new layout tests LAYOUT-A..E), project msedge |
| hard gates (sensor map) | PASS | 106 sensors; 212 channels; 24/29/24/29; 2 cannon slots; I7/I16 absent; 0 duplicate IDs/slots/scanOrder/TC_F/TC_R; 0 shared channels |
| viewport fit 1920x1080 / 1366x768 / 2560x1440 | NOT VERIFIED | no browser in Arena; asserted by e2e/layout.spec.ts, Owner-local Edge run PENDING |
| sensitive-data scan (added lines of changed and new files) | PASS | no credentials/keys/connection strings/e-mail; IPv4 only 127.0.0.1; URLs only http://127.0.0.1:5181 |
| markdown relative links (all tracked and new .md) | PASS | 44 files, 696 links, 0 broken; 10 anchors, 0 broken |
| loopback-only bind | PASS | harness socket 127.0.0.1; non-loopback refused by unit test |
| arena harness measurement 10 min | NOT RE-RUN | runtime unchanged since 935973e6 measurement (PASS); presentation-only change |
| offline npm ci rehearsal | NOT RE-RUN | dependencies and lock file unchanged since dd20a8bd rehearsal (PASS) |

**NOT VERIFIED:** ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge rendering and viewport fit of the fullscreen layout (Owner-local re-run and manual F11 1920x1080 review PENDING) · heap / DOM / long tasks / end-to-end latency in Edge · Windows process CPU and memory · Windows offline restore · Extended (4 h) stability · 60-minute run (PAUSED) · Production integration.
