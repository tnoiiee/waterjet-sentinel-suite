# Stage 0.4A CP-3c-2 / CP-4A — development checkpoint (2026-10-09)

**Historical 2026-10-09 development status (superseded by the 2026-10-10 final closeout below): OWNER-AUTHORIZED DEVELOPMENT DELIVERY; NOT OWNER-VALIDATED. PR open, not merged.**

## Gate and scope

At start, `origin/main` and the clean assigned branch both resolved to `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b`; PR #10's merge commit is in ancestry, the CP-3c-1 `RuntimePublicationStore` foundation is present, and `gh pr list --state open` returned `[]`. No other branch was used. This checkpoint supersedes *authority wording only* in older CP-3c-1 documents; their historical validation claims remain dated facts.

Changed: startup-only exact `WJSS_SIMULATOR_SCENARIO` choice (absent = IDLE; empty, whitespace, case mismatch, unknown = `INVALID_SIMULATOR_SCENARIO`, exit 4 before listener). Profile gate precedes parsing; selection is read once. The SimulatorRuntime now owns scenario, kernel state, retention, cursor, accepted ordinal and the sole atomic State/Delta publication store/writer. First accepted ordinal **0** consumes catalogue step tick **0** into Runtime revision **2**. At most one input per accepted tick; inputs remain pending on publication failure. APPLIED, REFUSED and NO_OP are consumed only after publication. Event timestamps remain the catalogue's fixed epoch plus step seconds; synthetic Runtime timestamps remain paced from composition time. After the final input, synthetic revisions continue. No timer chooses scenario events.

Snapshot, Runtime status and bounded Delta **metadata** use a single acquired publication per response; `/api/v1/deltas` does **not** return a full Delta wire body. Runtime status names the selected scenario and labels the queue as SIMULATOR read-only sequencing. The Inspector uses existing periodic GET polling and displays scenario, revision, queue FIFO, Job/target, phase/progress, Safe Return trigger/step/valve/axis, Critical Pump/modal, last outcome and last successful data refresh. It has a visible GET-error stale state. Existing display-only controls affect the browser only. Sequence controls and Pump commands remain disabled. Stage marker unchanged. No additional route or verb.

Wire mapping: SR2 absent/present → `NOT_COMMANDED`/`CLOSE_COMMANDED`; valve unobserved/CLOSED/other actual observation → `ABSENT`/`CLOSED_CONFIRMED`/`NOT_CONFIRMED`; SR4 absent/present → `NOT_COMMANDED`/`RETURN_COMMANDED`; axis unobserved/AT_STANDBY/other actual observation → `ABSENT`/`STANDBY_CONFIRMED`/`NOT_CONFIRMED`. Evidence `Event` remains the original evidence code. Pre-P1 P1 phase/index 0/progress 0 and PhaseAtTrigger P1 are **Contract fallbacks, not verified facts**. PhaseStartedAt is Job StartedAt until latest PHASE_VERIFIED; progress is PhaseIndex / 6.0; CleaningPhase `IN_PROGRESS`. No contract, fixture or TypeScript change.

## Arena evidence and Owner gate

Arena: `.NET SDK 10.0.401` unavailable (`dotnet: command not found`). Therefore locked restore, Release build, focused/full .NET tests, fixture parity, and actual .NET HTTP/manual observation **NOT RUN / NOT VERIFIED** here; do not call the implementation validated until Owner-local gates pass. Static boundary scan S1–S9: **0 findings**. Inspector inline JavaScript `node --check`: **PASS**. TypeScript `npm ci --ignore-scripts && npm run check`: **31/31 tests and typecheck PASS** (unchanged TypeScript). `git diff --check`: PASS. Source-authored tests cover startup identity/profile/read count, ordinal/event bounds, atomic publication refusal and pending input, APPLIED/REFUSED/NO_OP consumption, final-step continuation, snapshot/metadata coherence and GET-only UI/host source. Existing projection assertions were updated for the approved wire labels. Reflection-based tick tests avoid a timer or listener; they are not a replacement for Owner-local HTTP checks.

### Owner-local PowerShell (repository root, SDK 10.0.401)

```powershell
git status --short --branch
dotnet restore WaterJetSentinelSuite.sln --locked-mode
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
dotnet test tests/runtime.tests/Wjss.Runtime.Core.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SequencingRuntimeProjectionTests|FullyQualifiedName~RuntimePublicationTests"
dotnet test tests/api.tests/Wjss.Runtime.Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SimulatorFastTrackTests|FullyQualifiedName~RuntimeHostCompositionTests|FullyQualifiedName~RuntimeApiTopologyTests"
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release --no-build
Push-Location packages/contracts/wjss-contracts-ts
npm ci --ignore-scripts
npm run check
Pop-Location
node tools/boundary-scan/boundary-scan.mjs .
git diff --check
git status --short --branch
```

### One scenario per process; GET-only observation

Run **one** line in a dedicated PowerShell terminal, stop with Ctrl+C before choosing the next. `WJSS_DEVICE_PROFILE` is explicitly SIMULATOR. The host binds loopback only; use a free port if 5181 is occupied. Never set TEST_HARDWARE or PRODUCTION.

```powershell
$env:WJSS_DEVICE_PROFILE = 'SIMULATOR'; $env:WJSS_SIMULATOR_SCENARIO = 'IDLE'; dotnet run --project apps/runtime/Wjss.Runtime.csproj -c Release --no-build
$env:WJSS_DEVICE_PROFILE = 'SIMULATOR'; $env:WJSS_SIMULATOR_SCENARIO = 'NORMAL_COMPLETION'; dotnet run --project apps/runtime/Wjss.Runtime.csproj -c Release --no-build
$env:WJSS_DEVICE_PROFILE = 'SIMULATOR'; $env:WJSS_SIMULATOR_SCENARIO = 'PUMP_TRIP'; dotnet run --project apps/runtime/Wjss.Runtime.csproj -c Release --no-build
$env:WJSS_DEVICE_PROFILE = 'SIMULATOR'; $env:WJSS_SIMULATOR_SCENARIO = 'VALVE_CLOSE_FAILURE'; dotnet run --project apps/runtime/Wjss.Runtime.csproj -c Release --no-build
```

With any one process running, open `http://127.0.0.1:5181/inspector` in a local browser, and in another PowerShell terminal use **GET only**:

```powershell
$base = 'http://127.0.0.1:5181'
Invoke-RestMethod -Method Get "$base/api/v1/snapshot" | ConvertTo-Json -Depth 100
Invoke-RestMethod -Method Get "$base/api/v1/runtime" | ConvertTo-Json -Depth 100
Invoke-RestMethod -Method Get "$base/api/v1/deltas" | ConvertTo-Json -Depth 100
```

Observe revisions 1→2 (step 0), at most one catalogue event per revision, FIFO queue/Job/Safe Return, after-final-step synthetic updates, revision/previousRevision metadata chain, and the Inspector's read-only/error states. The three GETs are separate HTTP responses, so revisions may advance between them; compare against each response's own revision or poll again. No write request or hardware access is needed.

## Boundaries and review

Unchanged: contracts, fixtures, TypeScript, adapters, project/lock files, config, tools, domain, device profiles, RuntimeStage marker, GET route list and loopback binding. Not verified: SDK compile/analyzers, .NET tests, parity, browser visual state, live HTTP and Owner acceptance. No MODBUS, device write, TEST_HARDWARE, PRODUCTION, persistence, SSE, new evidence route or operator control. CP-4 full UI refinement **NOT AUTHORIZED**. Stop here pending Owner-local results and review; do not merge this PR.

---

## Final closeout — Owner-local validation and exact-source review (2026-10-10)

**Superseding status:** The earlier development/pending language above is preserved as dated history. The exact **validated code head** is `fd053fde07c96163b073fc9cb4c0383f31eafe3c`, descended from approved main `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b`. Owner-local automated validation and Owner manual review are **PASSED** at that code head, as reported by the Owner; Arena did **not** compile or execute .NET or perform the browser review. PR #11 is **READY FOR OWNER MERGE REVIEW — OPEN, NOT MERGED**. This documentation-only successor does not change the validated code tree and is not itself a separately .NET-validated code head. No Owner-local artifact commit was needed.

### Owner-reported automated evidence (exact code head `fd053fd`)

| Gate | Reported result |
| --- | --- |
| Environment | Variables cleared before validation |
| Locked restore / locks | PASS; no lock drift |
| Release build | PASS; `TreatWarningsAsErrors` enabled; 0 warnings, 0 errors |
| Full .NET suite | **414 total / 414 passed / 0 failed / 0 skipped** |
| Fixture parity | **7 total / 7 passed / 0 failed / 0 skipped** |
| TypeScript | Typecheck PASS; **34 passed / 0 failed** |
| Boundary scan | **0 findings; S1–S9 clean** |
| Diff and tree | `git diff --check` PASS; no local source drift; final working tree CLEAN |

The Owner did not separately report exact-head focused Runtime.Core or API-project counts; the full-suite result above is the reported total. Older counts from predecessor commits are **not** evidence for `fd053fd`.

### Owner-observed browser evidence (not executed or screenshot-reviewed in Arena)

The Owner reported **PASS** for `NORMAL_COMPLETION`, `VALVE_CLOSE_LOW`, `VALVE_CLOSE_FAILURE` (middle band), `VALVE_CLOSE_HIGH` and `TWO_JOB_SEQUENTIAL_COMPLETION`; lower-limit pressure-inferred qualified completion; retention of leak and not-fully-closed Jobs; separately displayed Pump outlet and paired IVn outlet measurements with independent Pump-ready setpoint and Valve Low/High thresholds; Valve CLOSE followed immediately by Axis RETURN without waiting for close confirmation; the latched high-severity read-only modal and fault-based next-dispatch blocking; and truthful Pump control/readiness wording. In the two-Job scenario, `SYN-JOB-1` was the sole Active Job while `SYN-QE-2` stayed queued, released before `SYN-JOB-2` dispatched from that Queue head, with no simultaneous Active Jobs observed. The final Queue was empty, Active Job null, and Last Job Outcome `COMPLETED` for `SYN-JOB-2`. Screenshots are **Owner-observed evidence**, not Arena-executed evidence.

### Approved measured-pressure classification (SIMULATOR, read-only)

The Pump sample must be valid and identified as `PUMP_OUTLET`; readiness is strictly **Pump outlet bar > configured PumpReadySetpointBar**. The paired Valve sample must be valid and identified as `IVn_OUTLET` for the Job's own IVn; only the configured Valve Low/High thresholds classify it. The Pump and Valve thresholds are independent development configuration, not Production values or physical run-state commands. `P` below is a valid, fresh, source-matched Valve outlet sample; invalid quality, staleness, source or pressure is never placed in a valid band.

| Valve observation | Condition | Resolution / diagnosis |
| --- | --- | --- |
| OPEN | Invalid input | `BLOCKED` / `VALVE_PRESSURE_INPUT_INVALID` |
| OPEN, Upper detected | `P < Low` | `BLOCKED` / `VALVE_OPEN_FEEDBACK_CONTRADICTION` |
| OPEN, Upper detected | `Low <= P < High` | `BLOCKED` / `VALVE_LEAK_SUSPECTED` |
| OPEN, Upper detected | `P >= High` | `OPEN_CONFIRMED` / `NONE` |
| OPEN, Upper not detected, no timeout | Any valid `P` | Pending / `NONE` |
| OPEN, Upper not detected, timed out | `P < Low` | `BLOCKED` / `VALVE_FAILED_TO_OPEN` |
| OPEN, Upper not detected, timed out | `Low <= P < High` | `BLOCKED` / `VALVE_LEAK_SUSPECTED` |
| OPEN, Upper not detected, timed out | `P >= High` | `OPEN_BY_PRESSURE` / `UPPER_LIMIT_SENSOR_FAULT`; next dispatch blocked |
| CLOSE | Invalid input | `PRESSURE_INPUT_INVALID` / `VALVE_PRESSURE_INPUT_INVALID`; Job retained |
| CLOSE, Lower not detected, no timeout | Any valid `P` | Pending / `NONE` |
| CLOSE, qualifying observation | `P >= High` | `NOT_FULLY_CLOSED` / `VALVE_NOT_FULLY_CLOSED`; Job retained |
| CLOSE, qualifying observation | `Low <= P < High` | `LEAK_SUSPECTED` / `VALVE_LEAK_SUSPECTED`; Job retained |
| CLOSE, Lower detected | `P < Low` | `LOWER_LIMIT_CONFIRMED` / `NONE`; actual confirmation evidence |
| CLOSE, Lower not detected, timed out | `P < Low` | `CLOSED_BY_PRESSURE` / `LOWER_LIMIT_SENSOR_FAULT`; **no fabricated Lower confirmation**; qualified completed outcome and next dispatch blocked |

### Final Source Review — static, at `fd053fd`

The following classifications are based on exact PR #11 source, not a new Arena .NET or browser run. Source anchors: `apps/runtime/SimulatorRuntime.cs`, `Program.cs`, `RuntimeHostOptions.cs`, `Inspector/index.html`; `packages/application/Runtime/RuntimePublication.cs`, `Sequencing/SequencingKernel.cs`, `Sequencing/SequencingPressure.cs`, `Sequencing/SequencingTopology.cs`, `Simulator/SimulatorScenarioCatalogue.cs`, `Simulator/SequencingRetention.cs` and `Simulator/SequencingRuntimeProjection.cs`; `packages/domain/ProfileStartPolicy.cs`. The second dispatch is issued by the Runtime coordinator, while the first remains a scripted transition. Both use the same kernel and single writer; the coordinator does not run on a due-event tick.

| # | Classification | Exact-source finding |
| --- | --- | --- |
| 1 | PASS | One publication writer is issued by `RuntimePublicationStore.CreateWriter`. |
| 2 | PASS | Candidate State, matching Delta and bounded history are prepared and published through one atomic reference write. |
| 3 | PASS | Each Snapshot, Runtime-status or Delta-metadata response acquires one publication; separate HTTP requests may legitimately have different revisions. |
| 4 | PASS | Compatibility `State`, `Counters` and `Deltas` derive from the publication, not independent live stores. |
| 5 | PASS | `ExecuteTick` applies at most the current due Scenario step. |
| 6 | PASS | One guarded coordinator branch issues at most one automatic dispatch per accepted tick. |
| 7 | PASS | Due-event ticks cannot auto-dispatch; the release is published before another tick dispatches. |
| 8 | PASS | Sequencing, retention, cursor and accepted ordinal are adopted only after the writer accepts publication. |
| 9 | PASS | Queue entries are admitted as ready entries, without per-entry wait/hold states; structural eligibility is checked at admission and rechecked for dispatch. |
| 10 | PASS | Dispatch consumes only Queue position 1. |
| 11 | PASS | Neither dispatch path scans forward within a transition. |
| 12 | PASS | State holds one nullable Active Job; dispatch refuses another while one exists. |
| 13 | PASS | Cleaning activity belongs to that single Active Job, not parallel Jets. No physical Jet output exists. |
| 14 | PASS | The second entry remains queued through Job 1 Cleaning and Mandatory return. |
| 15 | PASS | Job 2's automatic dispatch follows Job 1 release in a later accepted publication. |
| 16 | PASS | Equipment-fault next-dispatch, critical-suspension and non-RUNNING Pause/Hold gates prevent auto-dispatch; kernel dispatch checks these gates too. |
| 17 | PASS | The coordinator leaves an invalid head in place; kernel scripted revalidation removes at most that head without dispatching a later entry in the same transition. Retained Jobs block dispatch. |
| 18 | PASS | Pump outlet samples use the dedicated `PUMP_OUTLET` identity. |
| 19 | PASS | Pump readiness compares the valid Pump sample only with `PumpReadySetpointBar` (strict greater-than). |
| 20 | PASS | `ValveOutletSource(valveId)` gives distinct `IV1_OUTLET`…`IV8_OUTLET` source identities. |
| 21 | PASS | A Job checks only its own paired `ValveId` and that Valve's outlet identity. |
| 22 | PASS | Valve OPEN/CLOSE bands use only the configured Valve Low/High thresholds. |
| 23 | PASS | No Pump/Valve sample alias, fallback, averaging or cross-source derivation is used for sequencing. |
| 24 | PASS | A wrong-source measurement is discarded as a usable sample and classified invalid; its event may still be APPLIED to retain invalid-input/fault evidence, rather than returning a kernel `REFUSED` outcome. |
| 25 | PASS | OPEN quality-first Low/High matrix distinguishes confirmed/open-by-pressure, contradiction, leak and failed-to-open. |
| 26 | PASS | CLOSE quality-first Low/High matrix distinguishes confirmed, pressure-inferred, leak, not-fully-closed and invalid input. |
| 27 | PASS | `StartSafeReturn` records SR2 Valve CLOSE before SR4 Axis RETURN. |
| 28 | PASS | SR4 is issued immediately after SR2 without an SR3/Valve-close confirmation gate. |
| 29 | PASS | Valve resolution and Axis Standby each may arrive first. |
| 30 | PASS | Earlier Valve/Axis feedback remains on the Job and retained evidence until the join. |
| 31 | PASS | `LOWER_LIMIT_CONFIRMED` and `CLOSED_BY_PRESSURE` join with Axis Standby to release. |
| 32 | PASS | Leak, not-fully-closed and invalid-pressure close results do not release the Job. |
| 33 | PASS | SR3 records resolution; ValveClosedConfirmedSeq is nullable and only set for actual Lower-limit confirmation. |
| 34 | PASS | Lower-limit sensor fault preserves `COMPLETED` Cleaning outcome with qualified completion/remark. |
| 35 | PASS | Faults have distinct identities and Upper/Lower faults can coexist. |
| 36 | PASS | Retention archives cumulative faults and both multiple-fault remarks. |
| 37 | PASS | Latched equipment faults have `NextDispatchBlocked` and prevent the next dispatch. |
| 38 | PASS | No automatic acknowledge, reset, bypass, resume or retry path was added. |
| 39 | PASS | Inspector polling is GET-only; page buttons change display state only. |
| 40 | PASS | Host registers GET routes only, no write route. |
| 41 | PASS | No operator command control is delivered. |
| 42 | PASS | No MODBUS access path is delivered. |
| 43 | PASS | Start policy refuses `TEST_HARDWARE` before listener binding. |
| 44 | PASS | Start policy refuses `PRODUCTION` before listener binding. |
| 45 | PASS | No device-output path exists in this PR. |

**Verdict: CP-3c-2 FINAL SOURCE REVIEW PASSED; BLOCKING DEFECTS NONE.** CP-4A minimal read-only observation and sequential AutoSequence critical gate passed Owner review. This review does not make the system safety-rated or authorize hardware access.

**NON-BLOCKING UI CONTEXT-LABEL PUNCHLIST:** During an Active Job, retained completion and Valve-close values from the last released Job may look like current-Job values. Future UI wording should distinguish *Current Active Job*, *Last released Job outcome*, and *Last released Valve-close resolution*. Record only; no Product or Inspector change in this closeout. CP-4 full UI refinement is **not delivered**.

**Scope boundary:** Runtime Host integration, startup Scenario selection, atomic State/Delta migration, existing GET-only observation, minimal read-only Inspector, independent Pump and paired-IVn pressure and their configured thresholds, parallel Valve/Axis Mandatory return, cumulative faults, qualified completion, fault-based next-dispatch block, sequential two-Job AutoSequence and truthful Pump wording are delivered for SIMULATOR observation. **MODBUS NOT IMPLEMENTED; TEST_HARDWARE NOT AUTHORIZED; PRODUCTION NOT AUTHORIZED; no device command/write path.** PR #11 remains OPEN, NOT MERGED; only the Owner may merge.
