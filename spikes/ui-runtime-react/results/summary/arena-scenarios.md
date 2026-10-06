# Stage 0.2.1A — Arena scenario results (synthetic)

> SYNTHETIC SPIKE EVIDENCE — NOT PRODUCTION VALIDATION. Runtime / SSE level only; no browser in Arena.
> `PASS+OWNER` = runtime part passed in Arena, browser part pending Owner-local Edge run. `OWNER-LOCAL` = browser-only.

Generated: 2026-10-06T09:20:22.395Z

Counts: PASS 25 · PASS+OWNER 4 · OWNER-LOCAL 1 · final invariant violations 0 · accepted second jobs 0

| ID | Scenario | Status | Duration (ms) | Key evidence |
| --- | --- | --- | --- | --- |
| S01 | Initial Snapshot | PASS | 7 | {"firstEvent":"snapshot","snapshotBytes":78284,"sensors":106,"thermocoupleChannels":212,"perWall":{"LEFT":24,"REAR":29,"RIGHT":24,"FRONT":29},"wallMapSlots":108,"cannonSlots":["CANNON_REAR@R5C7","CANNON_FRONT@R5C16"],"revision":1} |
| S02 | One-second Deltas | PASS | 5200 | {"deltasIn5_2s":6,"medianIntervalMs":1000,"gaps":0,"lastValidated":true} |
| S03 | 30% Dirty | PASS | 2511 | {"dirty":32,"expectedApprox":32,"population":106,"note":"one sensor may be under active cleaning"} |
| S04 | 70% Dirty | PASS | 2507 | {"dirty":74,"expectedApprox":74,"population":106} |
| S05 | Threshold oscillation around 50 | PASS | 7009 | {"oscillatingSensors":8,"classificationFlipsIn7s":23} |
| S06 | All quality states | PASS | 1310 | {"qualities":{"G+201":"UNCERTAIN","G+202":"BAD","G+203":"STALE","G+204":"DISABLED","G+101":"GOOD"},"uncertainBasis":"LAST_VALIDATED"} |
| S07 | Selection during updates | PASS+OWNER | 5 | {"runtimePart":"selection is not on the wire (UI-local)","jsdom":"renderIsolation.test.tsx: selection survives Deltas and Snapshots","browserPart":"OWNER-LOCAL e2e S07"} |
| S08 | Queue badge changes | PASS | 4012 | {"sensorsWithQueueStateChange":64,"statesSeen":["READY","NONE","ACTIVE"]} |
| S09 | GlobalQueue changes | PASS | 2414 | {"totalQueued":32,"previewLength":8,"heldStatuses":["HELD"],"fields":["position","sensorId","sourceReason","dirtyScore","secondsSinceLastClean","status"]} |
| S10 | Single Job progression | PASS | 30151 | {"phasesSeen":["P1","P2","P3","P4","P5","P6"],"jobsCompleted":2,"maxActiveJobs":1} |
| S11 | Active Alarm | PASS | 402 | {"alarmState":"ACTIVE_UNACK","classificationBefore":"DIRTY","classificationAfter":"DIRTY","note":"classification is independent of alarm state"} |
| S12 | Cleared Ack Required | PASS | 808 | {"afterClear":"CLEARED_UNACK","clearedUnackCount":1,"afterAck":"NONE"} |
| S13 | Device timeout | PASS | 5008 | {"affectedSensors":13,"qualitiesSeen":["GOOD","UNCERTAIN","BAD"],"deviceState":"TIMEOUT","consecutiveTimeouts":5} |
| S14 | Other Devices continue | PASS | 3 | {"pollsDuringTimeout":{"SYN-TC-01":6,"SYN-TC-02":6,"SYN-TC-04":6,"SYN-TC-05":6,"SYN-TC-06":6,"SYN-TC-07":6,"SYN-TC-08":6}} |
| S15 | Device recovery | PASS | 1907 | {"recoveredWithinMs":1904,"commAlarmAfterRecovery":"CLEARED_UNACK"} |
| S16 | UI disconnect | PASS | 306 | {"droppedClients":1,"lastRevisionSeen":81} |
| S17 | UI reconnect | PASS | 1702 | {"firstEventAfterReconnect":"snapshot","sentLastEventId":81} |
| S18 | Authoritative re-snapshot | PASS | 3 | {"snapshotRevision":83,"lastSeenBeforeDrop":81,"missedDeltasReplayed":0,"reconnectsWithLastEventId":1} |
| S19 | Historian slowdown | PASS | 5207 | {"deltasIn5_2s":6,"maxIntervalMs":1002,"revisionsAdvanced":5} |
| S20 | Near-overflow behavior | PASS | 1737 | {"nearOverflow":true,"depthAtFull":50000,"capacity":50000,"rejected":6512,"gapMarkers":1,"pumpStopRoundTripMs":6.7,"nearOverflowClearedAfterRestoreMs":509,"depthAtClear":37340,"pumpAfterRestart":"STARTING"} |
| S21 | Bounded Trend | PASS | 9009 | {"acceleratedTicks":89,"acceleratedCapacity":50,"pointsRetained":50,"mainTrendPoints":83,"mainCapacity":600,"note":"accelerated parameters are test-only, not the planning values"} |
| S22 | Camera placeholder | PASS+OWNER | 2 | {"sourceFilesScanned":25,"nonCodeAssetsNotScanned":["react-ui/src/assets/fonts/.gitattributes","react-ui/src/assets/fonts/FONT_SOURCE.md","react-ui/src/assets/fonts/GoogleSans-Latin-Variable.woff2","react-ui/src/assets/fonts/OFL.txt","react-ui/src/assets/fonts |
| S23 | Synthetic close guard for Active Job | PASS | 1511 | {"evaluation":{"allowed":false,"reasons":["ACTIVE_JOB","PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S24 | Synthetic close guard for Pump running | PASS | 4 | {"evaluation":{"allowed":false,"reasons":["PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S25 | Viewport resize | OWNER-LOCAL | 0 | {"reason":"Browser layout cannot be exercised in Arena (no browser). Covered by react-ui/e2e/operations.spec.ts S25."} |
| S26 | Synthetic config revision | PASS | 808 | {"configRevision":2,"dirtyAtThreshold99":0,"restoredRevision":3} |
| S27 | Refused second Job | PASS | 7 | {"result":"ACTIVE_JOB_EXISTS","activeJobs":1,"acceptedSecondJobs":0,"refusedSecondJobs":2,"invariantViolations":0} |
| S28 | Revision gap -> re-snapshot | PASS | 3107 | {"gapsDetected":1,"resyncFirstEvent":"snapshot"} |
| S29 | Mixed GlobalQueue sources | PASS+OWNER | 1216 | {"sourceTypesFirst8":["SYN_TIME_DUE","SYN_TEMP_AND_TIME","SYN_OPERATOR_REQUEST","SYN_TEMP_RISE","SYN_DIRTY_SCORE_ABOVE_THRESHOLD"],"first8":["1:G+203:SYN_TIME_DUE","2:G+113:SYN_TEMP_AND_TIME","3:H2:SYN_OPERATOR_REQUEST","4:I10:SYN_TEMP_RISE","5:G+201:SYN_DIRTY |
| S30 | Synthetic review presets (Alarm + Queue) | PASS+OWNER | 5538 | {"sensorId":"G5","states":{"alarm-queue-dirty":"DIRTY/ACTIVE_UNACK/BLOCKED","alarm-queue-cleaner":"CLEANER/ACTIVE_UNACK/BLOCKED","selected-alarm-queue":"DIRTY/ACTIVE_UNACK/BLOCKED","job-alarm-queue":"DIRTY/ACTIVE_UNACK/ACTIVE/JOB","cleared-ack-queue":"DIRTY/CL |
