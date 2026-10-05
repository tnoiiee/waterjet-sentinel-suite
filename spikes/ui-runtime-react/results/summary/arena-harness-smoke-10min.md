# Stage 0.2.1A — Arena harness measurement (smoke-10min)

> SYNTHETIC SPIKE MEASUREMENT - ARENA NODE HARNESS ONLY - NOT A BROWSER OR PRODUCTION RESULT

| Item | Value |
| --- | --- |
| Started | 2026-10-05T05:37:44.956Z |
| Duration | 601 s |
| SSE clients | 2 |
| Scenario cycle steps | 20 |
| Harness CPU % (p50 / p95 / max) | 1.03 / 1.45 / 1.57 |
| Harness RSS MB (p50 / p95 / max) | 69.8 / 74.2 / 74.2 |
| Harness RSS MB first-third mean → last-third mean | 67.7 → 73.8 |
| Heap used MB (p50 / max) | 10.4 / 16.5 |
| Event-loop delay p99 ms (p50 / max of samples) | 10.9 / 10.9 |
| Snapshot bytes (p50 / max) | 78184 / 108069 |
| Delta bytes (p50 / p95 / max) | 61018 / 61091 / 61985 |
| Delta interval ms (p50 / p95 / max) | 1000 / 1002 / 1004 |
| Delta lag generated→received ms (p50 / p95 / max) | 2 / 3 / 5 |
| Delta rate (/s, client 0) | 1.03 |
| Client gaps detected (scenario-injected) / re-snapshots | 1, 1 / 1, 1 |
| Client SSE connections (incl. resyncs) | 2, 2 |
| Publish duration ms (p50 / p95 / max, last 3600) | 1.115 / 1.785 / 4.203 |
| Max in-flight (global / per device) | 4 / 1 |
| FAST poll latency ms (p50 / p95 / max) | 21.9 / 38.29 / 750.99 |
| Historian max depth / rejected | 11224 / 0 |
| Jobs completed / accepted second jobs | 21 / 0 |
| Pump-stop command handling in harness ms (n / max) — not end-to-end | 1 / 0.138 |
| Invariant violations | 0 |

Browser, Edge, WebView2, Windows-process, kiosk, and end-to-end UI metrics: **NOT MEASURED IN ARENA** (Owner-local).
