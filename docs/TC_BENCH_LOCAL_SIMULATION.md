# TC bench — Local Modbus TCP Simulation (Owner-local loopback execution class)

Status: implemented on branch `arena/18760814-waterjet-sentinel-suite` (parent `f356b162`), Agent-local
.NET build/test NOT RUN (no .NET SDK in the Agent sandbox); Owner-local build and execution are the
validation of record. Owner-local result (2026-10-15, PR head `59d0ffdd…`): affected gates PASS and
the Local Simulation run PASS — 40 words, exactly 100–139, `QUARANTINED` (see §5.6). PR #25 OPEN, NOT
MERGED. Nothing in this document is hardware, WAGO, TC-NODE-01 or T01 evidence.

**HARDWARE CONTACT NOT AUTHORIZED · T01 NOT AUTHORIZED · PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED ·
WRITE/CONTROL NOT AUTHORIZED.** This path contacts the local host only.

## 1. Purpose

The Owner needs an application-owned way to run the existing Thermocouple (TC) Modbus TCP acquisition
path against a Modbus TCP simulator on the Owner workstation without editing Source, without changing
the trusted physical candidate, without creating a physical authorization artifact and without using
`PHYSICAL_TEST`. The bench therefore has a third, explicitly separate execution class:

| Selector | Execution class / provenance | I/O | Endpoint | Authorization | Acceptance |
|---|---|---|---|---|---|
| (default) or `--dry-run` | `SIMULATION` | none | reported default `192.0.2.11:502` | none | QUARANTINED |
| `--local-simulation` | `LOCAL_TCP_SIMULATION` | one connect, one read on the local host | effective `127.0.0.1:502` | none, none consulted | QUARANTINED |
| `--physical-test` | `PHYSICAL_DIAGNOSTIC` | one connect, one read | trusted tuple only | signed, single-use, mandatory | QUARANTINED until offline Owner review |

The product token is `LOCAL_TCP_SIMULATION` (the Owner's "LOCAL_MODBUS_TCP_SIMULATION" class; the
protocol name is not permitted as a token in the bench tree by boundary rule S3). Port 502 alone is
not a physical indicator; the literal loopback address with port 502 is a Local Modbus TCP Simulation.

## 2. Three configuration layers, kept separate

| Layer | Where | Value | Authority |
|---|---|---|---|
| A. Default / template | trusted catalog (`TrialCatalog.LoadTrusted()`, exported offline from the canonical TC Node configuration) | `192.0.2.11` / `502` / FC03 / Unit `0` / start `0` / quantity `40` / BigEndian / `NOT_VERIFIED` | none — a candidate representation only |
| B. Invocation configuration | `LocalSimulation.InvocationConfiguration(catalog)` (in-process, typed, not a file or argument) | the default tuple with ONLY the address replaced by the literal `127.0.0.1`; port `502` | validated fail-closed by `LocalSimulation.RequireValid` before any client exists |
| C. Physical authorization | `PhysicalTrialAuthorization.Authorize` (unchanged) | signed detached artifact bound to the exact trusted tuple | mandatory for `PHYSICAL_TEST`; never involved in A or B |

The default configuration is printed unchanged next to the effective invocation configuration in
every local run, so the two are never confused. No externally editable field (argument value, file,
environment variable) selects the local endpoint: `--local-simulation` carries no value.

## 3. What one local run does — and does not do

Does (reusing the existing acquisition path unchanged — read-only client abstraction, factory,
FluentModbus adapter, request/result models, lifecycle orchestration, evidence model):

- validates the invocation configuration (address text exactly `127.0.0.1`, port `1..65535`, request
  exactly the trusted default FC03 / Unit 0 / start 0 / quantity 40 / BigEndian / T01 identity),
  refusing with a typed `ArgumentException` BEFORE the client factory is invoked;
- constructs one client, performs one connect (2000 ms timeout), at most one read (2000 ms timeout,
  one outstanding request, generation fencing), then explicit disconnect and observed disposal;
- records evidence of class `LOCAL_TCP_SIMULATION`: effective endpoint `127.0.0.1` and port, function
  code, unit, start, quantity, returned word count, raw words (only when exactly 40 words were
  returned), connect/read/cleanup outcome codes, authorization `NOT_ATTEMPTED` / `NOT_PROVIDED`,
  `physicalCapabilityEnabled false`, `trialAcceptance QUARANTINED`, limitation
  `PHYSICAL_EXECUTION_NOT_AUTHORIZED`;
- prints `{ mode, configuration, invocation, evidence }` to stdout and exits `0` when evidence was
  produced (including failed connects/reads, which are recorded, not retried).

Does not:

- retry, reconnect, poll, fall back to FC04, another Unit ID, start address or quantity, or correct
  any value;
- write or control anything, open a listener, ping, scan, resolve names, touch firewall or NIC;
- read or consume any authorization, consult the physical gate, or produce a `PhysicalTrialPlan`;
- compare the returned words with an expected pattern: the application does not encode `100..139`
  as a rule. The Owner compares the printed `words` to the simulator's register contents manually;
  transport success and content comparison stay separate;
- accept the result: a `LOCAL_TCP_SIMULATION` record can never become `ACCEPTED`, even by offline
  review with a matching wire review (`TrialEvidence.Validate` refuses it).

## 4. Safety boundary (fail-closed)

- Address: only the literal IPv4 text `127.0.0.1` (ordinal comparison). `192.0.2.11`, any other IPv4,
  `127.0.0.2`, `localhost`, `::1`, `127.1`, padded or suffixed text and empty text are refused with
  `LOCAL_SIMULATION_ADDRESS_REJECTED`. IPv6 loopback is out of scope. No resolution ever occurs.
- Port: `1..65535`; the command line always uses `502`. Invalid ports are refused with
  `LOCAL_SIMULATION_PORT_REJECTED`.
- Request: anything other than the trusted default request and T01 identity is refused with
  `LOCAL_SIMULATION_REQUEST_REJECTED`.
- Evidence: a `LOCAL_TCP_SIMULATION` record with any endpoint other than `127.0.0.1` / valid port, or a
  loopback record presented under `SIMULATION`, `SYNTHETIC_TEST_ONLY` or `PHYSICAL_DIAGNOSTIC`, fails
  `TrialEvidence.Validate`.
- Physical gate: `PHYSICAL_TEST` accepts no endpoint override of any kind; an authorization naming
  `127.0.0.1` is refused by exact tuple binding (`TUPLE_MISMATCH`).
- Mode selectors `--dry-run`, `--local-simulation`, `--physical-test` are mutually exclusive;
  `--authorization` / `--signature` with `--local-simulation` are refused.

## 5. Owner-local procedure (not performed by the Agent)

Prerequisites on the Owner workstation: pinned .NET SDK (see `global.json`), Node.js, and the
simulator. Replace `<REPO>` with the local repository path; no Owner path is recorded here.

### 5.1 Simulator

The Owner reported EasyModbusTCP Server Simulator v5.5 listening on `0.0.0.0:502`, verified at the time
of writing by TCP connect only (PowerShell `Test-NetConnection` to `127.0.0.1` port `502` →
`TcpTestSucceeded True`; the completed run is recorded in §5.6), with Holding
Registers UI addresses `1..40` set to `100..139` (protocol addresses `0..39`). Keep the simulator
running during the invocation. The application will issue FC03, Unit identifier `0`, start `0`,
quantity `40`. If the simulator rejects Unit identifier `0` or the register range, the application
reports the failure (for example `ProtocolException`) and does not retry with another value; that is
a finding about the simulator configuration, not something the application corrects.

### 5.2 Build and software validation (exact repository commands, run from `<REPO>`)

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

The automated tests use an in-process fake client or an ephemeral loopback listener port (never
`502`); they do not require, use or claim the Owner-local simulator.

### 5.3 Local simulation invocation

```powershell
dotnet run --project apps/tc-bench/Wjss.TcBench.csproj -c Release --no-build --no-restore -- --trial T01 --local-simulation
```

Exit `0`: evidence JSON on stdout. Exit `2`: typed refusal (argument, configuration, catalog or
build-identity rejection); no acquisition was performed. There is no exit code for "connection
failed": a failed connect or read is reported inside the evidence with exit `0`.

### 5.4 Reading the output

- `mode` is `LOCAL_TCP_SIMULATION`; `configuration.address` is still `192.0.2.11` (default, unchanged);
  `invocation.address` is `127.0.0.1`, `invocation.port` is `502`.
- `evidence.provenance` `LOCAL_TCP_SIMULATION`, `evidence.endpoint` `127.0.0.1`, `evidence.port` `502`,
  `functionCode 3`, `unitId 0`, `start 0`, `quantity 40`.
- Transport success: `connect.kind` and `read.kind` `Success`, `copiedWordCount 40`,
  `finalHandoffDecision RETAINED_QUARANTINED`, `words` with 40 entries `{ index, value }`,
  `disconnectResult` / `disposalResult` / `taskCompletion` `COMPLETE`, `cleanupState OBSERVED`.
- Content comparison (Owner, manual): with the simulator set as described, `words[i].value` is
  expected to equal `100 + i`. A different pattern is a finding to record, not a failure the
  application masks; transport success is reported independently of content.
- Failure shapes: `connect.kind ConnectionFailure` (nothing listening, port blocked),
  `Timeout`, `ProtocolException` (Modbus exception response), `MalformedResponse`, or a
  `copiedWordCount` other than 40 with `words` empty and `finalHandoffDecision
  REJECTED_OR_NOT_ISSUED`. Nothing is retried.
- `trialAcceptance` is always `QUARANTINED`; `authorizationVerification` /
  `authorizationConsumption` are `NOT_ATTEMPTED`; `physicalCapabilityEnabled` is `false`.

### 5.5 What a successful local run proves — and does not

Proves: the application-owned client, request shape (FC03 / Unit 0 / start 0 / quantity 40), framing,
word copy, lifecycle and evidence path work end to end against a TCP peer on the local host.

Does not prove: anything about TC-NODE-01, the WAGO coupler, the field network, the as-installed port,
Unit identifier behavior, FC03 vs FC04, register mapping, scaling, wire-break, timing or
interoperability beyond the simulator used. These remain `NOT_VERIFIED` / `CANDIDATE_UNVERIFIED` until
the One-Node Physical TC gate.

### 5.6 Owner-local result (2026-10-15, PR head `59d0ffdd1b3ca13168db4c26d0c0b70031908a3b`)

Owner-local, clean detached validation worktree; the Agent did not run any of it. Affected gates on
this head: Commissioning check PASS, Commissioning tests 220/220 (R40 PASS on the actual Windows
checkout), TC bench Node/static-source tests 19/19, S2 14/14, S3 19/19, boundary scan S1–S9 clean,
locked restore PASS, TC acquisition adapter and TC bench Release builds PASS, no lock drift. (One
initial build command carried an operator path typo and returned MSB1009; the correct command passed
immediately afterwards — not a Source defect.)

Simulator: EasyModbusTCP Server Simulator v5.5; listener verified first with
`Test-NetConnection 127.0.0.1 -Port 502` → `TcpTestSucceeded True` (the TCP check alone is not the
Modbus result). Invocation exactly as §5.3.

| Field | Owner-local result |
|---|---|
| Execution mode / provenance | `LOCAL_TCP_SIMULATION` |
| Default configuration (unchanged) | `192.0.2.11:502` |
| Effective invocation | `127.0.0.1:502` |
| Request | FC03 / Holding Registers; Unit ID 0; Start Address 0; Quantity 40 |
| Connect / Read | Success / Success |
| Copied word count | 40 |
| Returned values | index 0 = 100, index 1 = 101, … index 39 = 139 — exactly 100–139, no gaps or duplicates |
| Lifecycle | transport before DISCONNECTED; connect and read observed Success; disconnect COMPLETE; disposal COMPLETE; task completion COMPLETE; cleanup state OBSERVED; worker observation OBSERVED; timeout NOT_ELAPSED; cancellation NOT_REQUESTED; stale decision CURRENT_AT_DIAGNOSTIC_HANDOFF |
| Classification | trial acceptance `QUARANTINED`; physical capability enabled false; authorization ID `NOT_PROVIDED`; authorization verification and consumption `NOT_ATTEMPTED`; noWrite true; noFallback true; `wireEvidence` `NOT_PROVIDED` |
| Unchanged | Runtime, Kiosk, topology, control authority; validation worktree clean after execution |

Finding recorded on the way: the first two reads returned exactly 40 words with indexes 0–30 = 100–130
and indexes 31–39 = 0, stable on repetition, Connect / Read / cleanup successful. The Simulator had
been populated only through value 130 — incomplete Simulator register population, not an application
truncation defect (the application reports returned words as received and encodes no expected
pattern, §5.4). After the Owner populated the remaining Holding Registers the final run returned
100–139. No Source change was needed.

Boundary of this result: it proves the items in §5.5 "Proves" — including that protocol Start Address
0 maps to Simulator UI address 1 — against this Simulator only. It proves nothing in §5.5 "Does not
prove"; `wireEvidence` remains `NOT_PROVIDED`; it is not hardware verification and not T01 PASS. PR #25
remains OPEN, NOT MERGED; Merge and K7 are NOT AUTHORIZED.

## 6. Change summary (code)

- NEW `apps/tc-bench/LocalSimulation.cs` — execution class constants, invocation configuration,
  fail-closed `RequireValid`.
- `apps/tc-bench/TrialConfiguration.cs` — `BenchMode.LOCAL_TCP_SIMULATION`, `--local-simulation`
  selector, three-way `CONFLICTING_MODE`.
- `apps/tc-bench/TrialRunner.cs` — public `RunLocalSimulationAsync` (validate → one-shot execute).
- `apps/tc-bench/TrialEvidence.cs` — `LOCAL_TCP_SIMULATION` endpoint rule in `Validate`.
- `apps/tc-bench/Program.cs` — local branch (no authorization read, no gate), output
  `{ mode, configuration, invocation, evidence }`.
- Tests extended in `tests/tc-acquisition.tests` (parser, invocation, endpoint/request refusals,
  lifecycle with fake client and with the real adapter over an ephemeral loopback port, evidence
  rules, tuple-binding refusal of the loopback address) and in
  `tools/tc-bench/test/export-trial-configuration.test.mjs` (static-source guards).
- No dependency, package, lock, UI, role, authentication, persistence or network-configuration change.
