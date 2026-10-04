# Stage 0.2.1A — Arena validation summary

> SYNTHETIC SPIKE VALIDATION - ARENA (Linux, Node) ONLY - NO BROWSER RESULTS

Recorded: 2026-10-04T22:59:20.494Z · Base: `e779f8ad2c856e367fd65985007a3da411bd0e73`

| Check | Result | Detail |
| --- | --- | --- |
| tsc --noEmit (TypeScript 6.0.3) | PASS | 0 errors |
| vite build (production) | PASS | JS 301.82 kB (gzip 100.79 kB), CSS 10.37 kB; ~0.23 s |
| vitest (jsdom) 8 files | PASS | 53/53 |
| runtime-harness node:test | PASS | 14/14 |
| scenario runner (28 scenarios) | PASS | PASS 25, PASS+OWNER 2, OWNER-LOCAL 1, FAIL 0 |
| arena harness measurement 10 min, 2 SSE clients | PASS | exit 0; invariant violations 0; accepted second jobs 0 |
| playwright test --list (no browser) | PASS | 16 tests in 2 files, project msedge |
| offline npm ci (unreachable registry 127.0.0.1:9, --offline) | PASS | exit 0; negative control ENOTCACHED exit 1; tsc/build/vitest/harness PASS on restored tree |
| sensitive-data scan (new and changed files) | PASS | no credentials/keys/connection strings; IPv4 only 127.0.0.1, 0.0.0.0, 192.0.2.10 (RFC 5737) |
| markdown relative links (all tracked and new .md) | PASS | 0 broken |
| loopback-only bind | PASS | harness socket 127.0.0.1; non-loopback refused by unit test |

**NOT VERIFIED:** ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge rendering / heap / DOM / long tasks / end-to-end latency · Windows process CPU and memory · Windows offline restore · Extended (4 h) stability.
