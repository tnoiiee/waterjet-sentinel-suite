# Stage 0.2.1A — Arena validation summary

> SYNTHETIC SPIKE VALIDATION - ARENA (Linux, Node) ONLY - NO BROWSER RESULTS

Recorded: 2026-10-05T05:49:44.000Z · Base: `e779f8ad2c856e367fd65985007a3da411bd0e73` · Parent checkpoint: `dd20a8bd` · Scope: Stage 0.2.1A sensor-map correction (Owner domain correction: 106 Sensor locations / 212 Thermocouple channels)

| Check | Result | Detail |
| --- | --- | --- |
| npm ci (PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1) | PASS | exit 0; package-lock.json unchanged |
| tsc --noEmit (TypeScript 6.0.3) | PASS | 0 errors |
| vite build (production) | PASS | JS 303.71 kB (gzip 101.41 kB), CSS 10.90 kB (gzip 3.45 kB) |
| vitest (jsdom) 10 files | PASS | 60/60 |
| runtime-harness node:test 2 files | PASS | 26/26 |
| scenario runner (28 scenarios) | PASS | PASS 25, PASS+OWNER 2, OWNER-LOCAL 1, FAIL 0 |
| golden fixtures regenerated and validated against canonical map | PASS | snapshot 106 sensors, 108 wallMap slots; delta chained |
| arena harness measurement 10 min, 2 SSE clients | PASS | exit 0; invariant violations 0; accepted second jobs 0; jobs completed 21 |
| playwright test --list (no browser) | PASS | 17 tests in 2 files, project msedge |
| hard gates (sensor map) | PASS | 106 sensors; 212 channels; 24/29/24/29; 2 cannon slots; I7/I16 absent; 0 duplicate IDs/slots/scanOrder/TC_F/TC_R; 0 shared channels |
| sensitive-data scan (changed and new files) | PASS | no credentials/keys/connection strings; IPv4 only 127.0.0.1, 0.0.0.0, 192.0.2.10 (RFC 5737) |
| markdown relative links (all tracked and new .md) | PASS | 44 files, 694 links, 0 broken; 7 anchors, 0 broken |
| loopback-only bind | PASS | harness socket 127.0.0.1; non-loopback refused by unit test |
| offline npm ci rehearsal | NOT RE-RUN | dependencies and lock file unchanged since dd20a8bd rehearsal (PASS) |

**NOT VERIFIED:** ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge rendering / heap / DOM / long tasks / end-to-end latency on the corrected map (Owner-local re-run PENDING) · Windows process CPU and memory · Windows offline restore · Extended (4 h) stability · 60-minute run (PAUSED).
