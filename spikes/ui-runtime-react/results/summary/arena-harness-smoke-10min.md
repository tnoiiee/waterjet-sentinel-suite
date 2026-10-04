# Stage 0.2.1A — Arena harness measurement (smoke-10min)

> SYNTHETIC SPIKE MEASUREMENT - ARENA NODE HARNESS ONLY - NOT A BROWSER OR PRODUCTION RESULT

| Item | Value |
| --- | --- |
| Started | 2026-10-04T22:48:21.566Z |
| Duration | 601 s |
| SSE clients | 2 |
| Scenario cycle steps | 20 |
| Harness CPU % (p50 / p95 / max) | 0.82 / 1.18 / 1.43 |
| Harness RSS MB (p50 / p95 / max) | 69 / 76 / 76.3 |
| Harness RSS MB first-third mean → last-third mean | 66.1 → 73.5 |
| Heap used MB (p50 / max) | 11.2 / 16.1 |
| Event-loop delay p99 ms (p50 / max of samples) | 10.8 / 10.9 |
| Snapshot bytes (p50 / max) | 50422 / 80318 |
| Delta bytes (p50 / p95 / max) | 50188 / 50233 / 51173 |
| Delta interval ms (p50 / p95 / max) | 1000 / 1001 / 1045 |
| Delta lag generated→received ms (p50 / p95 / max) | 2 / 3 / 6 |
| Delta rate (/s, client 0) | 1.031 |
| Client gaps detected (scenario-injected) / re-snapshots | 1, 1 / 1, 1 |
| Client SSE connections (incl. resyncs) | 2, 2 |
| Publish duration ms (p50 / p95 / max, last 3600) | 0.84 / 1.383 / 4.948 |
| Max in-flight (global / per device) | 4 / 1 |
| FAST poll latency ms (p50 / p95 / max) | 22.28 / 38.41 / 751.03 |
| Historian max depth / rejected | 10950 / 0 |
| Jobs completed / accepted second jobs | 21 / 0 |
| Pump-stop command handling in harness ms (n / max) — not end-to-end | 1 / 0.129 |
| Invariant violations | 0 |

Browser, Edge, WebView2, Windows-process, kiosk, and end-to-end UI metrics: **NOT MEASURED IN ARENA** (Owner-local).
