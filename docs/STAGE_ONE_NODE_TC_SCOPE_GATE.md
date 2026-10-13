# One-Node TC bench — software validation closeout

**2026-10-13: SOFTWARE IMPLEMENTATION VALIDATED — READY FOR OWNER PR REVIEW.**
Software-only MERGE READY recommendation requires the final documentation/available-check
closeout to pass. No merge, deployment, physical trial or capture is authorized by this record.
The PR targets `main` from `arena/bf844c54-waterjet-sentinel-suite`; its final documentation
successor SHA is resolved after commit/push, never pre-written or inferred.

- Approved main ancestor: `2ba0b6f4da5e52b277e17a57ed4c71c9668cad73`.
- Exact Owner-validated implementation head: `c0bc2c71636d3206098fd890e1de40c46fabae65`.
- Genuine-lock commit / closeout starting head: `e71e33b22d039b6cd5940fbe94da1879f71142fa`.
- The lock commit is the implementation head's direct child and changes ONLY the two locks.
- Closeout is documentation/validation only; Product, tests, project/solution files, locks,
  dependencies, configuration, fixtures, generators and boundary policy are not changed.

The closed library-selection decision is unchanged. [ADR-0019](decisions/ADR-0019-isolated-one-node-tc-bench.md)
records isolated bench ownership; earlier pending/blocked notices below are dated history,
not current software-validation status. Physical authorization remains false.

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
The embedded derived T01 catalog/digest are fixed for the reviewed build: no free-form endpoint
can redefine the exact Node/Cabinet/address/port tuple or FC/Unit/start/quantity/endianness candidates.

One client and one connect attempt, at most one read. No retry/reconnect/fallback, polling,
service/scheduler, detached tasks, engineering conversion, Product publication or write/control.
The internal validation seam permits only literal 127.0.0.1 and a non-502 port; actual listener
ports are OS-assigned ephemeral ports in test-only responder code. No configured Node is tested.
The adapter requires exact request/connected Node/address/port/Unit agreement, exact returned
payload/word count without padding/truncation, and application-owned copied bytes/words.
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
Typed required-field parsing rejects unknown/missing fields, duplicates and disallowed nulls;
nullable unobserved facts remain null, not fabricated zeros. Public export
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

## Owner-local validation evidence — reported by Owner, 2026-10-13

Environment: Windows; .NET SDK **10.0.401**, runtime **10.0.12**. `TargetPath`,
`WJSS_UPDATE_FIXTURES`, `WJSS_DEVICE_PROFILE`, `WJSS_SIMULATOR_SCENARIO`, `WJSS_API_PORT`,
documented WJSS pressure-threshold overrides and `KMOTION_PATH` cleared. .NET build servers
shut down before validation. This is Owner-local evidence, not Agent-side execution.

| Owner-local gate | Final result |
| --- | --- |
| Genuine normal solution restore | PASS |
| Locked restore | PASS, no drift; two rounds before CA1068 repair, rerun after repair |
| Targeted adapter Release build | PASS, 0 warnings / 0 errors |
| Targeted bench Release build | PASS, 0 warnings / 0 errors |
| Targeted focused-test-project Release build | PASS, 0 warnings / 0 errors |
| Focused fake/loopback tests | **81/81**, 0 failed / 0 skipped |
| Full solution Release build | PASS, 0 warnings / 0 errors |
| Full .NET tests | **536/536**, 0 failed / 0 skipped |
| JavaScript syntax / exporter | PASS / **14/14** |
| S2 / S3 / Boundary S1–S9 | **14/14** / **19/19** / **0 findings** |
| Commissioning final | **218/218**, 0 failed / 0 skipped |
| Fixture parity, VERIFY ONLY | **7/7**, 0 failed / 0 skipped |
| Tracked source/protected drift / diff-check | none / none / PASS |

.NET reconciliation: accepted baseline **455** + new focused TC **81** = **536**.
Focused tests use fake clients and loopback-only ephemeral non-502 responder ports; no configured
TC endpoint/502 contact, T01 execution, passive capture or physical deployment occurred.

Before the lock commit, tracked source/protected files were clean and only the two genuine locks
were untracked. Owner staged exactly those two files, committed/pushed `e71e33b`, then proved a
clean worktree and local HEAD = remote branch head. Existing adapter lock and central versions
are unchanged. No raw evidence, local paths or secret values are placed in this record.

### Owner-local dry-run proof

```powershell
dotnet run --project apps/tc-bench/Wjss.TcBench.csproj -c Release --no-build --no-restore -- --dry-run
```

Observed: mode/provenance **SIMULATION**; T01 represented with `executionAuthorized: false`;
`physicalVerification: NOT_VERIFIED`; connect/read **NOT_ISSUED**; wire evidence **NOT_PROVIDED**;
TrialAcceptance **QUARANTINED**; `copiedWordCount: null`; `words: []`; `noWrite: true`;
`noFallback: true`. No physical client construction/I/O, no T01 execution, no Product current
value and no temperature interpretation. Dry-run success is not physical acquisition evidence.

### R40 LF/CRLF disposition — separate from lock-byte hashes

Initial Owner Commissioning run was **217/218**, R40 only: generated canonical output was LF;
Windows worktree fixture representation was CRLF. JSON content and topology were identical.
Owner ran the approved `node tools/sensor-map-example/generate.mjs --write`; canonical LF
verification passed **218/218**, then restored the original Windows CRLF worktree representation.
No tracked fixture drift or fixture regeneration was committed; `WJSS_UPDATE_FIXTURES` stayed
unset; .NET fixture parity VERIFY ONLY passed **7/7**. This was not a Product, topology or
fixture-content defect. Agent closeout Commissioning is **218/218** on the repository
representation; Agent did not run `--write`, normalize or change a fixture.

## Genuine lock provenance and byte-representation evidence

Both new locks are genuine Owner-local NuGet restore output, committed at `e71e33b`; they were
not synthesized/hand-edited. Agent inspected exact committed bytes only, without restore or
formatting. Git blob identity and exact committed **LF** SHA-256 are authoritative repository
byte evidence. Owner-local **Windows CRLF** hashes are environment-specific validation evidence;
do not substitute a hash from one representation for the other.

### `apps/tc-bench/packages.lock.json`

- Git blob SHA: `4ae85080a53bd2fb22536302e030824d32ce366a`.
- Exact committed LF SHA-256:
  `1DBF33869C3C15833CD34B06F0B1CC4321149FAD9F13E091DD38EE2C9E3A717E`.
- Owner-local Windows CRLF SHA-256:
  `C091CB788E90E8893CD63DB6A2FA12D9A111145AF7EDE9D5ECB5D98AC2918DA9`.

### `tests/tc-acquisition.tests/packages.lock.json`

- Git blob SHA: `3671b2a00b1d7450d1bd256cb151e2c1040acad0`.
- Exact committed LF SHA-256:
  `6BA43FD651E1F61D337DB2E63FC572B3926057FB5B25D7A51A8ED7AA249B8167`.
- Owner-local Windows CRLF SHA-256:
  `C12E2CEDF1423C4CE2416C8EE9DCA7FABFD48C71941DF0E702C990BFE032C034`.

A read-only in-memory LF-to-CRLF hash comparison reproduced BOTH Owner-local hashes exactly.
No converted bytes were written. JSON content/dependency graph/versions/NuGet contentHash values
and lock semantics are identical; this is not a Product, lock or content-integrity defect.

Agent graph inspection: valid JSON, version **2**, target **net10.0**; harness records the adapter
Project dependency; focused test lock records harness and adapter Project dependencies. Harness
has 10 package entries + 1 Project; focused lock has 23 package entries + 2 Projects. Selected
library (FluentModbus) resolves exactly **5.3.2**. Central test pins remain
`Microsoft.NET.Test.Sdk` **17.12.0**, `xunit` **2.9.2**, `xunit.runner.visualstudio` **2.8.2**; approved logging/ports pins remain **8.0.0**. Exact package/version sets match
the existing adapter closure plus existing central test stack; shared contentHash values match
existing adapter metadata and all package hashes have valid SHA-512 encoding. No unexpected
package/version, placeholder, absolute local path, credential or secret was found. Genuine
package origin is Owner restore evidence; Agent did not download packages or perform restore.

## Agent/Arena validation actually run — 2026-10-13

No .NET executable is available here: **NO Agent-side restore/build/.NET tests/dry-run execution/
fixture-parity execution** is claimed. Those gates above are explicitly Owner-local.

| Agent check | Result |
| --- | --- |
| Both locks: JSON/graph/pins/contentHash/safe-text inspection | PASS |
| Exact Git blobs / committed LF SHA-256 / read-only CRLF comparison | PASS |
| JavaScript syntax (exporter + tests, S2/S3 + tests, scanner; Commissioning check) | PASS |
| Exporter (three C# checks are STATIC source assertions) | **14/14** |
| S2 / S3 | **14/14** / **19/19** |
| Full Boundary S1–S9 | **0 findings** |
| Commissioning | **218/218**, 0 failed / 0 skipped |
| Documentation stale-term/proven-drift/allowlist review | PASS |
| Complete path/blob/mode and protected-file proof / diff-check | PASS |

Only local Node/npm/static/git checks and authorized GitHub operations were executed in Agent.
No acquisition network test (including loopback), non-loopback/502 acquisition contact, capture,
physical deployment or write/control occurred. No fixture or lock representation was changed.
Final documentation patch/commit checks are rerun before publication; a failure blocks PR readiness.

### Sandbox recovery and identity evidence

Targeted fetch proved `origin/main = 2ba0b6f4` and the assigned remote head `e71e33b`, exact lock
parent `c0bc2c7`, exact two changed lock paths and the full direct-parent chain. Initial recreated
workspace had **482/484**, exactly the two locks missing, zero extras and zero present blob/mode
mismatches. Owner authorized ONLY exact committed-blob materialization: binary Git extraction
into missing paths; each immediate `git hash-object` matched the blob SHA listed above.
All other file bytes were unchanged. Post-write identity was **484/484**, zero missing/extra/
blob/mode mismatches. Only then CAS `update-ref` repaired stale branch metadata from `2ba0b6f4`
to `e71e33b` and `read-tree` WITHOUT `-u` repaired the index. HEAD/index/worktree/remote identity
and clean status passed. No reset/clean/restore/checkout/switch/stash/rebase/force-push was used.
The Owner clarified the LF/CRLF hash distinction after a correct STOP; no lock bytes were changed.
Closeout changes only the eight proven-drift documentation paths; all 484 paths/modes remain,
all 476 other tracked blobs and all 15 existing lock files remain unchanged from `e71e33b`.

## Changed / unchanged / not verified

**Changed by this implementation series:** isolated one-shot bench software foundation,
deterministic derived T01 catalog/projection and SIMULATION default; exact containment;
adapter endpoint/count hardening and application-owned copied memory; independent outcome layers,
evidence manifest/validation/public sanitizer; fake/loopback tests; exact two-lock S3 vocabulary
exception; genuine locks; documentation/ADR records. No custom transport or library fork.
This closeout itself changes documentation only.

**Unchanged:** Runtime and Kiosk; normal Runtime SIMULATOR default; 6 Nodes / 40 candidate words
per Node / 240 physical Channels / 216 assigned measurements / 24 spare Channels / 108 Sensors;
normal I7/I16 and Sensor/FrontRear topology; canonical unresolved/unverified evidence statuses;
package versions and existing adapter lock; Queue, Cleaning Job, AutoSequence, Pump, Valve,
Water Jet, Galil, alarm/trip/interlock/control authority; Production status; no device write/control.

**Not verified / not authorized:** physical Node reachability, field-hardware interoperability,
FC03 and FC04 physical correctness, Unit ID, start register, physical byte/word order, image
width/contiguity, scaling, wire-break decoding, response transaction/Unit identity, passive wire
framing, one-Node field timing, six-Node timing/concurrency, Sensor/Channel physical mapping;
T01, passive packet capture, PHYSICAL_TEST deployment, Runtime/Kiosk physical integration,
six-Node acquisition, Production and write/control. Software test evidence cannot close these.

## Dated implementation and repair history

| Commit | Dated role (2026-10-13); later validation supersedes its then-pending status |
| --- | --- |
| `6ad6fa4889d7702eb6e066e78e3a78928251cc14` | Initial 27-file source checkpoint, no new locks, .NET evidence pending then |
| `8aa5c25c416d39e8f36d8ac9c3d8c58ce06ad97a` | CA1068: token-last internal signature and all 12 calls, unchanged lifecycle |
| `d149635556baaf83580dd029bae130fda4c5231c` | CS8602: asserted typed owned-array locals, same-file nullable-pattern sweep |
| `c0bc2c71636d3206098fd890e1de40c46fabae65` | JSON-family assertion for NUL rejection only; Product unchanged; Owner-validated implementation head |
| `e71e33b22d039b6cd5940fbe94da1879f71142fa` | Genuine two-lock-only commit, direct parent `c0bc2c7`; clean Owner-local state |

The following original source-checkpoint record is retained as history, NOT current instructions.
Its pending-lock/build/draft-only statements were accurate at that checkpoint and are superseded
by the Owner validation, genuine-lock commit and current software-review authorization above.

## Historical source-checkpoint record — 2026-10-13 (superseded)

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

## Review and optional software-only revalidation

Owner-local gates are complete. Review the exact implementation `c0bc2c7`, genuine locks
`e71e33b`, and final documentation successor as one PR targeting `main`. Open/keep the PR OPEN;
Agent does not merge. Passing software validation grants no physical authority. Preserve the
committed locks; no new restore generation, package refresh, fixture `--write` or normalization
is needed for this closeout. If Owner reruns validation, use locked restore and verify-only:

```powershell
dotnet restore WaterJetSentinelSuite.sln --locked-mode
dotnet build adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj -c Release --no-restore
dotnet build apps/tc-bench/Wjss.TcBench.csproj -c Release --no-restore
dotnet build tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj -c Release --no-restore
dotnet test tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj -c Release --no-build --no-restore
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
dotnet test WaterJetSentinelSuite.sln -c Release --no-build --no-restore
dotnet run --project apps/tc-bench/Wjss.TcBench.csproj -c Release --no-build --no-restore -- --dry-run
# VERIFY ONLY: WJSS_UPDATE_FIXTURES remains unset.
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~FixtureParityTests
node --test tools/tc-bench/test/export-trial-configuration.test.mjs
node --test tools/boundary-scan/test/s2Exception.test.mjs
node --test tools/boundary-scan/test/s3Exception.test.mjs
node tools/boundary-scan/boundary-scan.mjs .
npm --prefix packages/commissioning run check
npm --prefix packages/commissioning test
git diff --check
git status --short
```

Stop on any failure/drift; do not relax warnings, alter protected paths or change representations
to obtain a pass. .NET commands above are Owner-local only, not Agent execution claims.

## Separate future physical authorization gates

Reconfirm exact approved source/build/lock identity, physical identity/isolation and sole ownership,
bounded lifetime/stop procedure, separately authorized T01 and deployment, separately authorized
narrowly scoped passive capture with external durable evidence, independent wire/artifact/Owner
review. No T01–T04 batch; every later trial requires separate authorization. No six-Node or
Production expansion. The current executable continues refusing physical deployment.

**HARDWARE CONTACT NOT AUTHORIZED**

**T01 NOT AUTHORIZED**

**PASSIVE CAPTURE NOT AUTHORIZED**

**PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED**
