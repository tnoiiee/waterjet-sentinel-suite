# Stage 0.2.1A — Arena validation summary

> SYNTHETIC SPIKE VALIDATION - ARENA (Linux, Node) ONLY - NO BROWSER RESULTS

Recorded: 2026-10-06T05:00:00.000Z · Base: `e779f8ad2c856e367fd65985007a3da411bd0e73` · Parent checkpoint: `ea23bc58` · Scope: Stage 0.2.1A Operations readability refinement (Owner screenshot review) plus stale MAP U-shape E2E assertion correction; presentation and tests only. Sensor domain unchanged: 106 Sensor locations / 212 Thermocouple channels

| Check | Result | Detail |
| --- | --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund (PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1) | PASS | exit 0; package-lock.json unchanged; no browser download; dependencies unchanged |
| tsc --noEmit (TypeScript 6.0.3) | PASS | 0 errors (project); separate strict check of e2e/*.ts 0 errors (excluding absent Node type declarations for process.env) |
| vite build (production) | PASS | JS 316.77 kB (gzip 104.84 kB), CSS 25.68 kB (gzip 6.32 kB) |
| vitest (jsdom) 13 files | PASS | 93/93 (18 new: colorTokens 5, visual 3, layoutTokens 4, fullscreenLayout 6; jsdom does not validate pixel overlap) |
| runtime-harness node:test 2 files | PASS | 26/26 (sensorMap 12) |
| scenario runner (28 scenarios) | PASS | PASS 25, PASS+OWNER 2, OWNER-LOCAL 1, FAIL 0 |
| golden fixtures validated against canonical map | PASS | fixtures unchanged; fixtures.test PASS |
| playwright test --list (no browser) | PASS | 26 tests in 3 files; operations.spec + layout.spec selection 25 (was 21; 4 new READ-A..READ-D; stale MAP U-shape assertion corrected), project msedge |
| hard gates (sensor map) | PASS | 106 sensors; 212 channels; 24/29/24/29; 2 cannon slots; I7/I16 absent; 0 duplicate IDs/scanOrder/channels |
| ID / marker overlap, clipping, typography, viewport fit 1920x1080 / 1366x768 / 2560x1440 | NOT VERIFIED | no browser in Arena; asserted by e2e/layout.spec.ts (LAYOUT-A..E, READ-A..D), Owner-local Edge run PENDING |
| sensitive-data scan (added lines of changed and new files) | PASS | no credentials/keys/connection strings/e-mail; IPv4 only 127.0.0.1; URLs only http://127.0.0.1:5181 |
| markdown relative links (all tracked and new .md) | PASS | 44 files, 699 links, 0 broken; 13 anchors, 0 broken |
| loopback-only bind | PASS | harness socket 127.0.0.1; non-loopback refused by unit test |
| arena harness measurement 10 min | NOT RE-RUN | runtime unchanged since 935973e6 measurement (PASS); presentation-only change |
| offline npm ci rehearsal | NOT RE-RUN | dependencies and lock file unchanged since dd20a8bd rehearsal (PASS) |

**NOT VERIFIED:** ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge rendering, Sensor-cell overlap and clipping, typography, and viewport fit of the readability refinement (Owner-local Edge run and manual F11 1920x1080 review PENDING) · Bahnschrift glyph widths · heap / DOM / long tasks / end-to-end latency in Edge · Windows process CPU and memory · Windows offline restore · controlled 15-minute observation (PENDING) · Extended (4 h) stability · 60-minute run (PAUSED) · Production stability · Modbus performance · Production integration.
