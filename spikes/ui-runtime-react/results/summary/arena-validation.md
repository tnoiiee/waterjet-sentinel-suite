# Stage 0.2.1A — Arena validation summary

> SYNTHETIC SPIKE VALIDATION - ARENA (Linux, Node) ONLY - NO BROWSER RESULTS

Recorded: 2026-10-06T14:00:00.000Z · Base: `e779f8ad2c856e367fd65985007a3da411bd0e73` · Parent checkpoint: `23f48daa` (Edge 34 selected / 25 passed / 2 failed (READ-A, WJ-A) / 7 not run (Owner-reported)) · Scope: Stage 0.2.1A GlobalQueue semantics correction (Owner domain correction): bounded ready-only synthetic GlobalQueue (<= 8, no entry states), head-only atomic Queue -> Job dispatch with a synthetic dispatch record, Status column removed, Queue Eligibility Decision Matrix (proposal, OWNER DECISION REQUIRED), Edge value-clipping hotfix (READ-A), Water Jet test hotfix (WJ-A). Previous synthetic queue / job behaviour SUPERSEDED, not eligible for production promotion. Sensor domain unchanged: 106 Sensor locations / 212 Thermocouple channels

| Check | Result | Detail |
| --- | --- | --- |
| npm ci --ignore-scripts --no-audit --no-fund (PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1) | PASS | exit 0; package.json and package-lock.json unchanged; no browser download; dependencies unchanged |
| tsc --noEmit (TypeScript 6.0.3) | PASS | 0 errors (project); strict check of e2e/*.ts: 0 errors in layout/operations/punchlist specs; only the known absent Node type declarations in soak.spec.ts / support.ts (unchanged files) |
| vite build (production) | PASS | JS 327.90 kB (gzip 108.05 kB; +1.97 kB / +0.50 kB gzip vs 23f48daa), CSS 27.67 kB (gzip 6.68 kB; -0.49 kB / -0.12 kB gzip), WOFF2 47.67 kB unchanged |
| vitest (jsdom) 16 files | PASS | 120/120 (new globalQueue.test.tsx 6: heading/count, 0/8 and 8/8, no prohibited words or status, Q/J badges, Diagnostics dispatch summary and eligibility; Sensor-cell vertical budget guard; existing queue/preset/status-column assertions updated to the Owner domain correction) |
| runtime-harness node:test | PASS | 40/40, stable over 3 consecutive runs (new queueDispatch.test.mjs 8: gates A-F; reviewControls updated to the new presets, QUEUE_FULL, no retarget) |
| scenario runner (32 scenarios) | PASS | PASS 27, PASS+OWNER 4, OWNER-LOCAL 1, FAIL 0; S08 head dispatch QUEUED -> ACTIVE; S09 paused AutoSequence, QUEUE_FULL, no entry status; S29 bounded to 8 with 5 source types; S30 eight presets; S31 Owner example queue G+110, G9, G8, I12 -> Job G+110, new head G9, revision +1, second dispatch and retarget refused; S32 dirty70 max queue 8, every Job linked to its Position 1 dispatch |
| queue capacity / dispatch gates A-F | PASS | A capacity <= 8, no overflow/duplicates, Water Jet excluded; B target = former head, atomic removal, Position 2 -> 1; C <= 1 Job, no second dispatch; D dispatch record per Job (sensor = target, Position 1, monotonic revisions); E UI count 0-8, no status chips (jsdom); F no arbitrary retarget, Review Job makes the Sensor head first, Reset bounded. Browser part Owner-local PENDING |
| golden fixtures validated against canonical map | PASS | fixtures regenerated for the corrected queue / job contract; fixtures.test PASS |
| playwright test --list (no browser) | PASS | 36 tests in 4 files; Owner-local selection (operations + layout + punchlist) 35; new QUEUE-B; QUEUE-A, CTRL-A, CTRL-B, WJ-A rewritten |
| hard gates (sensor map) | PASS | 106 sensors; 212 channels; 24/29/24/29; 2 Water Jet reference slots (internal CANNON_*); 108 slots; I7/I16 absent; 0 duplicate IDs; sensorMap.mjs / classify.mjs unchanged |
| font checks | PASS | WOFF2 47,672 B SHA-256 40f917d9d0a4de0577c69089456c9e68d8ad3bbf58ac1f8ac91730538cb1531b unchanged; fontAsset tests PASS; Google Sans retained; no size or weight change for the value |
| prohibited queue states / hidden overflow (source scan) | PASS | no HELD / BLOCKED / EXCLUDED / READY queue-state literals in runtime, contracts, or UI source; no slice(0, 8) queue preview |
| READ-A value clipping, WJ-A clicks, rendered GlobalQueue panel in a browser | NOT VERIFIED | no browser in Arena; asserted by e2e/layout.spec.ts and e2e/punchlist.spec.ts, Owner-local Edge run PENDING |
| sensitive-data and public-boundary scan (added lines) | PASS | no credentials/keys/connection strings/e-mail; no IPv4 addresses or URLs in added lines; no Production Water Jet numbers |
| markdown relative links (all tracked and new .md) | PASS | 47 files, 717 links, 0 broken; 17 anchors, 0 broken |
| loopback-only bind | PASS | harness socket 127.0.0.1; non-loopback refused by unit test |
| arena harness measurement 10 min | NOT RE-RUN | acquisition / publish path unchanged; queue now bounded (smaller payload) |
| offline npm ci rehearsal | NOT RE-RUN | dependencies and lock file unchanged since dd20a8bd rehearsal (PASS) |

**NOT VERIFIED:** ASP.NET Core integration · Windows Service behaviour · WebView2 kiosk behaviour · installed-Edge rendering of the value-clipping fix, Water Jet clicks, GlobalQueue panel, Diagnostics dispatch evidence, and the new presets (Owner-local Edge run and manual F11 1920x1080 review PENDING) · heap / DOM / long tasks / end-to-end latency in Edge · Windows process CPU and memory · Windows offline restore · controlled 15-minute observation (PAUSED) · 60-minute run (waived as a gate, not run) · Extended (4 h) stability · Production queue runtime, eligibility policy, and alarm behaviour · Production stability · Modbus performance · Production integration.
