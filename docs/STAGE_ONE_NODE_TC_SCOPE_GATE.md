# One-Node TC bench scope gate — software checkpoint

**2026-10-13: BLOCKED ON OWNER-LOCAL GENUINE LOCK / BUILD / TEST EVIDENCE.**
Software scope and the narrow dependency-lock policy revision are Owner-approved.
This is a pending-lock SOURCE checkpoint, not implementation acceptance or merge readiness.
The closed library-selection decision remains unchanged; earlier merge-ready wording applies
only to that historical gate. See [ADR-0019](decisions/ADR-0019-isolated-one-node-tc-bench.md).

## Architecture and authority

Approach A: a separate Owner-invoked, one-shot process is the temporary sole TC-session
owner. Runtime/Kiosk remain unchanged and disconnected. Default SIMULATION/dry-run constructs
no physical client and performs no socket/DNS/network activity. Program has no acquisition
command: even a supplied authorization cannot activate physical deployment in this checkpoint.

The canonical-derived representation is T01 / TC-NODE-01 / CAB1 / canonical first-Node address /
Owner bench setting 502 / FC03 / Unit 0 / start 0 / quantity 40 / BigEndian. It remains
`executionAuthorized: false`, with no trusted authorization digest. Owner's 502 bench setting
is NOT physical verification; canonical configuration/evidence remains unresolved/unverified.
The exporter imports and validates the existing canonical source, embeds independently hashed
catalog/manifest metadata through ignored `obj`, and creates no tracked second address source.
External input must match the embedded digest and tuple; an external self-hash is not authority.
Hashes are not signatures: the independently reviewed build/source identity is also required.

One client and one connect attempt, at most one read. No retry/reconnect/fallback, polling,
service/scheduler, detached tasks, engineering conversion, Product publication or write/control.
The internal validation seam permits only literal 127.0.0.1 and a non-502 port; actual listener
ports are OS-assigned ephemeral ports in test-only responder code. No configured Node is tested.
Connect is finite but non-cooperative mid-handshake: Stop/cancellation invalidates the epoch,
observes late completion, then awaits disconnect/disposal before evidence handoff. Generation,
endpoint and final-handoff fencing reject stale results. Faults record exception TYPE, not message.
Read/cleanup tasks are observed; observation or cleanup failures cannot retain diagnostic words.

## Three separate evidence outcomes

1. **AdapterOutcome**: connect/read result or explicitly observed thrown operation; timestamps,
   monotonic elapsed duration, generation, classification and exception type. NOT_ISSUED is not
   substituted for an operation that actually threw.
2. **WireEvidenceOutcome**: separate offline observations, never library/word-count inference.
3. **TrialAcceptance**: explicit independent offline Owner decision bound to original acquisition
   bytes, trial/session/time and independently hashed external artifacts. Acquisition never accepts.

Success alone is QUARANTINED. A synthetically complete example exercises offline validation only;
it is not a physical trial or hardware evidence. Wire MATCHED requires observed request/response
transaction and Unit identities, FC03, start/quantity, exact framing/counts/data, complete capture,
trial/time matching, artifact hashes and an independently decoded indexed-word digest. Expected
80 data bytes / byte-count 80 / PDU 82 / ADU 89 / MBAP length 83 / request ADU 12 are expectations,
NOT observations. No capture implementation is added. The selected library discards some identity
and framing information; exact copied payload size does not establish wire validity.

Evidence includes branch/build SHA, clean-state proof, canonical source/config/manifest hashes,
neutral operator/environment identifiers, authorization reference, acquisition epoch, transport
states, Stop/cancel/timeout, stale/handoff decisions, copied-word count and cleanup/task results.
Typed required-field parsing rejects unknown/missing/null fields and duplicates. Public export
requires independent hash review and approved indexed-word excerpts; rejects local/storage paths,
secrets, raw exception messages and unreviewed prose, rather than silently dropping/changing facts.
Original acquisition bytes and external artifacts must be preserved separately outside Git.
Unknown source completeness, physical timing/image mapping/interpretation remain limitations.

## Narrow S3 revision

Only `tools/boundary-scan/s3Exception.mjs` and its focused test were newly authorized.
The existing selected-term exception additionally matches ONLY these exact normalized paths:

- `apps/tc-bench/packages.lock.json`
- `tests/tc-acquisition.tests/packages.lock.json`

No parent/directory prefix, subdirectory, sibling, backup, renamed lock, other project lock,
source/docs/config or other vendor is excepted. Existing adapter/central metadata exceptions
remain. Engine, S1/S2/S4–S9 and write/control boundaries remain unchanged. Synthetic scanner
fixtures are temporary and outside the repository; they are not genuine dependency locks.
The scanner is not a generic ban on write identifiers: separate adapter/API source review proves
no write invocation. This revision grants dependency metadata vocabulary, not execution authority.

## Checkpoint and validation state

The approved path budget is 29; checkpoint contents are 27 source/test/policy/documentation files.
The two new `packages.lock.json` files are intentionally ABSENT. No placeholder or handwritten
lock is allowed. Existing genuine locks, central pins and dependency versions remain unchanged.
No .NET executable is available in Arena: restore/build/executable fake or loopback tests/full
.NET tests/fixture parity are NOT RUN, not inferred from Node/static tests or predecessor results.
Source review is not a compiler, analyzer, runtime or physical validation result.

Available final checks: exporter 14/14 (including three explicitly static C# assertions), S2 14/14,
S3 19/19, full Boundary S1–S9 zero findings, Commissioning 218/218, JavaScript syntax,
`git diff --check`, exact path/blob/mode/protected-baseline proof. Final Arena reruns on 2026-10-13 PASS (Node v22.22.3 / npm 10.9.8); zero failures,
cancellations or skips in the listed suites. Two new project XML files parse. Baseline proof:
correct assigned branch; HEAD = origin/main = approved `2ba0b6f4da5e52b277e17a57ed4c71c9668cad73`;
parents `d1ea0324cb37de65a798fe4271429f955d4a9225` and
`c386f7508f217f9435582a4e62d5a6791b427391`, both ancestors; all 465 baseline paths/modes
retained, all 455 non-allowlisted baseline blobs unchanged, clean pre-stage index, exactly 27
expected changed paths and no unknown paths. No recovery/reset/branch switch was needed.
Available checks do not close Owner-local gates.
Owner explicitly permits this atomic source checkpoint and normal assigned-branch push without
new locks. No PR is required; any later PR must be DRAFT / BLOCKED ON OWNER-LOCAL EVIDENCE.
Never merge or advertise this checkpoint as MERGE READY.

Protected: interface, package/build/SDK pins, all existing locks, canonical configuration,
schedule/process-image/evidence classifications, contracts/schemas/fixtures/generators, Runtime,
Kiosk/DI/UI/profile policy, topology, Sensor/FrontRear, other scanner rules, Queue/Cleaning/
AutoSequence/Pump/Valve/Water Jet/Galil/alarm/trip/interlock/control. Six Nodes/two Cabinets,
240 physical / 216 assigned / 24 spare / 108 Sensors, normal I7/I16 remain unchanged.

### Exact checkpoint inventory (27 files)

```text
apps/tc-bench/Wjss.TcBench.csproj
apps/tc-bench/Program.cs
apps/tc-bench/TrialConfiguration.cs
apps/tc-bench/TrialRunner.cs
apps/tc-bench/TrialEvidence.cs
apps/tc-bench/README.md
tools/tc-bench/export-trial-configuration.mjs
tools/tc-bench/test/export-trial-configuration.test.mjs
adapters/tc-acquisition/TcReadOnlyClientFactory.cs
adapters/tc-acquisition/FluentModbusTcClient.cs
adapters/tc-acquisition/TcReadModels.cs
adapters/tc-acquisition/README.md
tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj
tests/tc-acquisition.tests/TrialPolicyTests.cs
tests/tc-acquisition.tests/AdapterLifecycleTests.cs
tests/tc-acquisition.tests/TrialEvidenceTests.cs
tests/tc-acquisition.tests/TestClients.cs
tests/tc-acquisition.tests/LoopbackResponder.cs
WaterJetSentinelSuite.sln
docs/decisions/ADR-0019-isolated-one-node-tc-bench.md
docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md
docs/CURRENT_STATE.md
docs/MASTER_PLAN.md
README.md
CHANGELOG.md
tools/boundary-scan/s3Exception.mjs
tools/boundary-scan/test/s3Exception.test.mjs
```

Execution review: only local Node/npm/static/git checks and GitHub fetch/query were performed.
No .NET execution, acquisition network test (loopback or otherwise), configured endpoint/502
contact, passive capture, physical client deployment or write invocation occurred.

## Owner-local handoff — Windows PowerShell, repository root

Pull the source checkpoint on the assigned branch only. Record the fetched SHA and parent;
compare them with the checkpoint report. Start clean. Do not switch branches, rewrite history,
install another SDK, change versions, regenerate fixtures, or relax warnings/policy to pass.
Every command below must succeed; stop on failure or any unexpected/protected-path drift.
Use the pinned SDK 10.0.401 and Node required by the existing project.

```powershell
if ((git branch --show-current) -ne 'arena/bf844c54-waterjet-sentinel-suite') { throw 'Wrong branch' }
git pull --ff-only origin arena/bf844c54-waterjet-sentinel-suite
if ($LASTEXITCODE -ne 0) { throw 'Pull failed' }
git rev-parse HEAD
git show -s --format='%H %P' HEAD
if (git status --porcelain) { throw 'Start from clean source checkpoint' }

# Process scope only; record override NAMES, never secret values in evidence.
Remove-Item Env:TargetPath -ErrorAction SilentlyContinue
Get-ChildItem Env:WJSS_* -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
# Includes WJSS_UPDATE_FIXTURES and documented DEVICE_PROFILE, API_PORT,
# SYNTHETIC_SEED, TICK_INTERVAL_MS overrides. No Runtime host is launched.
function Assert-LastExit { if ($LASTEXITCODE -ne 0) { throw 'Validation command failed: stop and retain evidence' } }
if ((dotnet --version) -ne '10.0.401') { throw 'Verify pinned SDK before continuing' }
dotnet --info
Assert-LastExit
dotnet build-server shutdown
Assert-LastExit
dotnet restore WaterJetSentinelSuite.sln
Assert-LastExit
```

Inspect the TWO genuinely generated new locks: valid version-2 JSON, net10.0 graph, selected
5.3.2 dependency closure, genuine package contentHash values, existing central test pins.
Reject placeholders, absolute/local paths, credentials or unexplained graph/version changes.
Existing locks and all protected files must have zero diff. Do not hand-edit locks or disguise
transitive metadata. Save independent SHA-256 hashes of the TWO locks and exact validation logs
outside Git, using neutral environment/operator identifiers.

```powershell
$locks = @('apps/tc-bench/packages.lock.json', 'tests/tc-acquisition.tests/packages.lock.json')
$before = @{}
foreach ($p in $locks) {
  if (!(Test-Path $p)) { throw 'Missing genuine lock' }
  $json = Get-Content $p -Raw | ConvertFrom-Json
  if ($json.version -ne 2) { throw 'Unexpected lock format' }
  $before[$p] = (Get-FileHash $p -Algorithm SHA256).Hash
}
# Inspect git status / diff and lock graphs BEFORE continuing.
git status --short
git diff --name-only
dotnet restore WaterJetSentinelSuite.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore round 1 failed' }
foreach ($p in $locks) { if ((Get-FileHash $p -Algorithm SHA256).Hash -ne $before[$p]) { throw 'Lock drift round 1' } }
dotnet restore WaterJetSentinelSuite.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore round 2 failed' }
foreach ($p in $locks) { if ((Get-FileHash $p -Algorithm SHA256).Hash -ne $before[$p]) { throw 'Lock drift round 2' } }

dotnet build adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj -c Release --no-restore
Assert-LastExit
dotnet build apps/tc-bench/Wjss.TcBench.csproj -c Release --no-restore
Assert-LastExit
dotnet build tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj -c Release --no-restore
Assert-LastExit
dotnet test tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj -c Release --no-build --no-restore
Assert-LastExit
# Optional executable proof: dry-run ONLY, no physical switches.
dotnet run --project apps/tc-bench/Wjss.TcBench.csproj -c Release --no-build --no-restore -- --dry-run
Assert-LastExit

dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
Assert-LastExit
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore
Assert-LastExit
node --check tools/tc-bench/export-trial-configuration.mjs
Assert-LastExit
node --check tools/tc-bench/test/export-trial-configuration.test.mjs
Assert-LastExit
node --check tools/boundary-scan/s3Exception.mjs
Assert-LastExit
node --check tools/boundary-scan/test/s3Exception.test.mjs
Assert-LastExit
node --test tools/tc-bench/test/export-trial-configuration.test.mjs
Assert-LastExit
node --test tools/boundary-scan/test/s2Exception.test.mjs
Assert-LastExit
node --test tools/boundary-scan/test/s3Exception.test.mjs
Assert-LastExit
node tools/boundary-scan/boundary-scan.mjs .
Assert-LastExit
npm --prefix packages/commissioning run check
Assert-LastExit
npm --prefix packages/commissioning test
Assert-LastExit
# Verify ONLY; WJSS_UPDATE_FIXTURES must remain unset. No --write/regeneration.
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~FixtureParityTests
Assert-LastExit
git diff --check
Assert-LastExit
git diff --exit-code
Assert-LastExit
git status --short
```

Record each exit code/results, targeted builds with 0 warnings/0 errors, executable fake/loopback
cases (only 127.0.0.1 OS-ephemeral/non-502), full .NET counts, fixture parity, both locked rounds,
lock hashes/no drift, protected-path proof, implementation SHA/direct parent and final diff-check.
A mismatch or failure is a STOP, not permission to edit protected files. Commit and normally push
ONLY the two genuine locks plus exact validation records in the already-approved documentation
paths after ALL gates pass. Never use `git add -A`, merge, force-push, or stage raw artifacts/paths/
secrets. Follow-on lock/evidence commit remains on the assigned branch. Physical acceptance and
merge review still require explicit Owner review; passing software tests authorizes no contact.

## Separate future gates

Exact approved source/build/lock SHA; identity/isolation and sole ownership; bounded lifetime/stop
procedure; separately authorized T01 and physical deployment; separately authorized narrowly
scoped passive capture and durable external storage; independent wire/artifact/Owner review.
No T01–T04 batch. Every later trial requires its own authorization; no six-Node/Production scope.

**HARDWARE CONTACT NOT AUTHORIZED**

**T01 NOT AUTHORIZED**

**PASSIVE CAPTURE NOT AUTHORIZED**

**PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED**
