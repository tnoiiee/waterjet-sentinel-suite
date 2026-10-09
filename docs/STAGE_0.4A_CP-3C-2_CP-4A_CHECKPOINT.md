# Stage 0.4A CP-3c-2 / CP-4A — development checkpoint (2026-10-09)

**Status: OWNER-AUTHORIZED DEVELOPMENT DELIVERY; NOT OWNER-VALIDATED. PR open, not merged.**

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
