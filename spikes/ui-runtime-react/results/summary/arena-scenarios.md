# Stage 0.2.1A — Arena scenario results (synthetic)

> SYNTHETIC SPIKE EVIDENCE — NOT PRODUCTION VALIDATION. Runtime / SSE level only; no browser in Arena.
> `PASS+OWNER` = runtime part passed in Arena, browser part pending Owner-local Edge run. `OWNER-LOCAL` = browser-only.

Generated: 2026-10-04T22:48:14.045Z

Counts: PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · final invariant violations 0 · accepted second jobs 0

| ID | Scenario | Status | Duration (ms) | Key evidence |
| --- | --- | --- | --- | --- |
| S01 | Initial Snapshot | PASS | 15 | {"firstEvent":"snapshot","snapshotBytes":50521,"sensors":104,"revision":1} |
| S02 | One-second Deltas | PASS | 5201 | {"deltasIn5_2s":6,"medianIntervalMs":1000,"gaps":0,"lastValidated":true} |
| S03 | 30% Dirty | PASS | 2516 | {"dirty":31,"expectedApprox":31,"note":"one sensor may be under active cleaning"} |
| S04 | 70% Dirty | PASS | 2506 | {"dirty":73,"expectedApprox":73} |
| S05 | Threshold oscillation around 50 | PASS | 7013 | {"oscillatingSensors":8,"classificationFlipsIn7s":23} |
| S06 | All quality states | PASS | 1310 | {"qualities":{"SYN-LEFT-01":"UNCERTAIN","SYN-LEFT-02":"BAD","SYN-LEFT-03":"STALE","SYN-LEFT-04":"DISABLED","SYN-LEFT-05":"GOOD"},"uncertainBasis":"LAST_VALIDATED"} |
| S07 | Selection during updates | PASS+OWNER | 4 | {"runtimePart":"selection is not on the wire (UI-local)","jsdom":"renderIsolation.test.tsx: selection survives Deltas and Snapshots","browserPart":"OWNER-LOCAL e2e S07"} |
| S08 | Queue badge changes | PASS | 4004 | {"sensorsWithQueueStateChange":63,"statesSeen":["NONE","READY","ACTIVE"]} |
| S09 | GlobalQueue changes | PASS | 2416 | {"totalQueued":30,"previewLength":8,"heldStatuses":["HELD"],"fields":["position","sensorId","sourceReason","dirtyScore","secondsSinceLastClean","status"]} |
| S10 | Single Job progression | PASS | 30149 | {"phasesSeen":["P1","P2","P3","P4","P5","P6"],"jobsCompleted":2,"maxActiveJobs":1} |
| S11 | Active Alarm | PASS | 403 | {"alarmState":"ACTIVE_UNACK","classificationBefore":"CLEANER","classificationAfter":"CLEANER","note":"classification is independent of alarm state"} |
| S12 | Cleared Ack Required | PASS | 807 | {"afterClear":"CLEARED_UNACK","clearedUnackCount":1,"afterAck":"NONE"} |
| S13 | Device timeout | PASS | 5008 | {"affectedSensors":13,"qualitiesSeen":["GOOD","UNCERTAIN","BAD"],"deviceState":"TIMEOUT","consecutiveTimeouts":5} |
| S14 | Other Devices continue | PASS | 2 | {"pollsDuringTimeout":{"SYN-TC-01":6,"SYN-TC-02":6,"SYN-TC-04":6,"SYN-TC-05":6,"SYN-TC-06":6,"SYN-TC-07":6,"SYN-TC-08":6}} |
| S15 | Device recovery | PASS | 1914 | {"recoveredWithinMs":1911,"commAlarmAfterRecovery":"CLEARED_UNACK"} |
| S16 | UI disconnect | PASS | 304 | {"droppedClients":1,"lastRevisionSeen":81} |
| S17 | UI reconnect | PASS | 1700 | {"firstEventAfterReconnect":"snapshot","sentLastEventId":81} |
| S18 | Authoritative re-snapshot | PASS | 2 | {"snapshotRevision":83,"lastSeenBeforeDrop":81,"missedDeltasReplayed":0,"reconnectsWithLastEventId":1} |
| S19 | Historian slowdown | PASS | 5208 | {"deltasIn5_2s":6,"maxIntervalMs":1002,"revisionsAdvanced":5} |
| S20 | Near-overflow behavior | PASS | 1751 | {"nearOverflow":true,"depthAtFull":50000,"capacity":50000,"rejected":6302,"gapMarkers":1,"pumpStopRoundTripMs":2.17,"nearOverflowClearedAfterRestoreMs":512,"depthAtClear":37574,"pumpAfterRestart":"STARTING"} |
| S21 | Bounded Trend | PASS | 9010 | {"acceleratedTicks":89,"acceleratedCapacity":50,"pointsRetained":50,"mainTrendPoints":83,"mainCapacity":600,"note":"accelerated parameters are test-only, not the planning values"} |
| S22 | Camera placeholder | PASS+OWNER | 2 | {"sourceFilesScanned":20,"externalUrlsOrMediaElements":0,"browserPart":"OWNER-LOCAL e2e S22 (request-host check)"} |
| S23 | Synthetic close guard for Active Job | PASS | 1508 | {"evaluation":{"allowed":false,"reasons":["ACTIVE_JOB","PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S24 | Synthetic close guard for Pump running | PASS | 4 | {"evaluation":{"allowed":false,"reasons":["PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S25 | Viewport resize | OWNER-LOCAL | 0 | {"reason":"Browser layout cannot be exercised in Arena (no browser). Covered by react-ui/e2e/operations.spec.ts S25."} |
| S26 | Synthetic config revision | PASS | 805 | {"configRevision":2,"dirtyAtThreshold99":0,"restoredRevision":3} |
| S27 | Refused second Job | PASS | 7 | {"result":"ACTIVE_JOB_EXISTS","activeJobs":1,"acceptedSecondJobs":0,"refusedSecondJobs":2,"invariantViolations":0} |
| S28 | Revision gap -> re-snapshot | PASS | 3109 | {"gapsDetected":1,"resyncFirstEvent":"snapshot"} |
