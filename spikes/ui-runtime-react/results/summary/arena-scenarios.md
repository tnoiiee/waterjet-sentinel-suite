# Stage 0.2.1A — Arena scenario results (synthetic)

> SYNTHETIC SPIKE EVIDENCE — NOT PRODUCTION VALIDATION. Runtime / SSE level only; no browser in Arena.
> `PASS+OWNER` = runtime part passed in Arena, browser part pending Owner-local Edge run. `OWNER-LOCAL` = browser-only.

Generated: 2026-10-06T14:22:31.450Z

Counts: PASS 27 · PASS+OWNER 4 · OWNER-LOCAL 1 · final invariant violations 0 · accepted second jobs 0

| ID | Scenario | Status | Duration (ms) | Key evidence |
| --- | --- | --- | --- | --- |
| S01 | Initial Snapshot | PASS | 8 | {"firstEvent":"snapshot","snapshotBytes":79022,"sensors":106,"thermocoupleChannels":212,"perWall":{"LEFT":24,"REAR":29,"RIGHT":24,"FRONT":29},"wallMapSlots":108,"cannonSlots":["CANNON_REAR@R5C7","CANNON_FRONT@R5C16"],"revision":1} |
| S02 | One-second Deltas | PASS | 5202 | {"deltasIn5_2s":6,"medianIntervalMs":1000,"gaps":0,"lastValidated":true} |
| S03 | 30% Dirty | PASS | 2512 | {"dirty":32,"expectedApprox":32,"population":106,"note":"one sensor may be under active cleaning"} |
| S04 | 70% Dirty | PASS | 2506 | {"dirty":74,"expectedApprox":74,"population":106} |
| S05 | Threshold oscillation around 50 | PASS | 7013 | {"oscillatingSensors":8,"classificationFlipsIn7s":23} |
| S06 | All quality states | PASS | 1310 | {"qualities":{"G+201":"UNCERTAIN","G+202":"BAD","G+203":"STALE","G+204":"DISABLED","G+101":"GOOD"},"uncertainBasis":"LAST_VALIDATED"} |
| S07 | Selection during updates | PASS+OWNER | 5 | {"runtimePart":"selection is not on the wire (UI-local)","jsdom":"renderIsolation.test.tsx: selection survives Deltas and Snapshots","browserPart":"OWNER-LOCAL e2e S07"} |
| S08 | Queue badge changes | PASS | 6421 | {"sensorsWithQueueStateChange":3,"statesSeen":["ACTIVE","NONE","QUEUED"],"dispatchedHead":"G+209"} |
| S09 | GlobalQueue changes (bounded, status-free; AutoSequence pause is not an entry state) | PASS | 2408 | {"totalQueued":8,"capacity":8,"pausedAutoSequence":"PAUSED","enqueueH17":"QUEUE_FULL","fields":["position","entryId","sensorId","sourceReason","dirtyScore","secondsSinceLastClean"]} |
| S10 | Single Job progression | PASS | 30152 | {"phasesSeen":["P1","P2","P3","P4","P5","P6"],"jobsCompleted":1,"maxActiveJobs":1} |
| S11 | Active Alarm | PASS | 403 | {"alarmState":"ACTIVE_UNACK","classificationBefore":"DIRTY","classificationAfter":"DIRTY","note":"classification is independent of alarm state"} |
| S12 | Cleared Ack Required | PASS | 806 | {"afterClear":"CLEARED_UNACK","clearedUnackCount":1,"afterAck":"NONE"} |
| S13 | Device timeout | PASS | 5005 | {"affectedSensors":13,"qualitiesSeen":["GOOD","UNCERTAIN","BAD"],"deviceState":"TIMEOUT","consecutiveTimeouts":4} |
| S14 | Other Devices continue | PASS | 3 | {"pollsDuringTimeout":{"SYN-TC-01":5,"SYN-TC-02":5,"SYN-TC-04":5,"SYN-TC-05":5,"SYN-TC-06":5,"SYN-TC-07":5,"SYN-TC-08":5}} |
| S15 | Device recovery | PASS | 1512 | {"recoveredWithinMs":1509,"commAlarmAfterRecovery":"CLEARED_UNACK"} |
| S16 | UI disconnect | PASS | 304 | {"droppedClients":1,"lastRevisionSeen":87} |
| S17 | UI reconnect | PASS | 1701 | {"firstEventAfterReconnect":"snapshot","sentLastEventId":87} |
| S18 | Authoritative re-snapshot | PASS | 2 | {"snapshotRevision":89,"lastSeenBeforeDrop":87,"missedDeltasReplayed":0,"reconnectsWithLastEventId":1} |
| S19 | Historian slowdown | PASS | 5208 | {"deltasIn5_2s":6,"maxIntervalMs":1001,"revisionsAdvanced":5} |
| S20 | Near-overflow behavior | PASS | 1753 | {"nearOverflow":true,"depthAtFull":50000,"capacity":50000,"rejected":6326,"gapMarkers":1,"pumpStopRoundTripMs":5.66,"nearOverflowClearedAfterRestoreMs":519,"depthAtClear":37550,"pumpAfterRestart":"STARTING"} |
| S21 | Bounded Trend | PASS | 9008 | {"acceleratedTicks":89,"acceleratedCapacity":50,"pointsRetained":50,"mainTrendPoints":85,"mainCapacity":600,"note":"accelerated parameters are test-only, not the planning values"} |
| S22 | Camera placeholder | PASS+OWNER | 2 | {"sourceFilesScanned":25,"nonCodeAssetsNotScanned":["react-ui/src/assets/fonts/.gitattributes","react-ui/src/assets/fonts/FONT_SOURCE.md","react-ui/src/assets/fonts/GoogleSans-Latin-Variable.woff2","react-ui/src/assets/fonts/OFL.txt","react-ui/src/assets/fonts |
| S23 | Synthetic close guard for Active Job | PASS | 1512 | {"evaluation":{"allowed":false,"reasons":["ACTIVE_JOB","PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S24 | Synthetic close guard for Pump running | PASS | 7 | {"evaluation":{"allowed":false,"reasons":["PUMP_RUNNING"],"note":"Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe."}} |
| S25 | Viewport resize | OWNER-LOCAL | 0 | {"reason":"Browser layout cannot be exercised in Arena (no browser). Covered by react-ui/e2e/operations.spec.ts S25."} |
| S26 | Synthetic config revision | PASS | 805 | {"configRevision":2,"dirtyAtThreshold99":0,"restoredRevision":3} |
| S27 | Refused second Job | PASS | 6 | {"result":"ACTIVE_JOB_EXISTS","activeJobs":1,"acceptedSecondJobs":0,"refusedSecondJobs":2,"invariantViolations":0} |
| S28 | Revision gap -> re-snapshot | PASS | 3106 | {"gapsDetected":1,"resyncFirstEvent":"snapshot"} |
| S29 | Mixed GlobalQueue sources (bounded to 8) | PASS+OWNER | 1211 | {"sourceTypes":["SYN_TIME_DUE","SYN_TEMP_AND_TIME","SYN_OPERATOR_REQUEST","SYN_TEMP_RISE","SYN_DIRTY_SCORE_ABOVE_THRESHOLD"],"entries":["1:G+203:SYN_TIME_DUE","2:G+113:SYN_TEMP_AND_TIME","3:H2:SYN_OPERATOR_REQUEST","4:I10:SYN_TEMP_RISE","5:J13:SYN_DIRTY_SCORE_ |
| S30 | Synthetic review presets (valid queue / job states) | PASS+OWNER | 45 | {"sensorId":"G5","states":{"queued-dirty":"DIRTY/NONE/QUEUED · queue 8/8","queued-cleaner-non-score":"CLEANER/NONE/QUEUED · queue 8/8","selected-queued":"DIRTY/NONE/QUEUED · queue 8/8","dispatched-head-job":"DIRTY/NONE/ACTIVE/JOB · queue 8/8","alarm-on-active- |
| S31 | Head-only atomic dispatch (no scan-forward; Owner examples A/B) | PASS | 67 | {"queueBefore":["G+110","G9","G8","I12"],"jobTarget":"G+110","newHead":"G9","dispatch":"SYN-DSP-0011","revisions":"123->124","secondDispatch":"ACTIVE_JOB_EXISTS","retarget":"ACTIVE_JOB_EXISTS"} |
| S32 | Bounded queue under load and Job linkage (dirty70, AutoSequence) | PASS | 4031 | {"maxQueueLength":8,"dispatchesObserved":1,"queueMetrics":{"length":8,"capacity":8,"dispatches":12,"autoSequence":"JOB_ACTIVE"}} |
