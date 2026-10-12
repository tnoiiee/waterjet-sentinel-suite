# Library Selection Gate Report — SELECT_FLUENTMODBUS_5_3_2 (2026-10-12)

**Branch:** `arena/c51a5906-waterjet-sentinel-suite` → PR to `main`
**Baseline:** remote `main` merge commit `d1ea0324cb37de65a798fe4271429f955d4a9225` (parents `cc6ccf8779d87a5a44d1c03a003021791c26aac8` / `a6238acb430070226d69bf3855d0c49f6b405eec`, ancestry `a7d739bb` → `cc6ccf87` → `d1ea0324`), verified `git ls-tree -r HEAD` vs `origin/main` 455 entries identical, clean trees.
**Decision:** **SELECT_FLUENTMODBUS_5_3_2** — FluentModbus 5.3.2 selected, NModbus 3.0.83 rejected.
**Reference:** `docs/decisions/ADR-0018-modbus-tcp-client-library-selection.md` (accepted, read-only foundation, no hardware verification, no TEST_HARDWARE/PRODUCTION/Device Write).

## 1. Authority and scope

Owner prompt 2026-10-12 Library Selection Evidence Gate. Decision-only: choose between A FluentModbus 5.3.2 and B NModbus 3.0.83 for the future read-only physical TC acquisition adapter. No hardware connect, no TCP socket to `192.0.2.11–192.0.2.16`, no Modbus transaction, no write `FC05/06/15/16`, no polling, no acquisition implementation. Previous leading decision FluentModbus remains default unless exact evidence proves a material blocker; do not select on preference/popularity/benchmarks. Baseline remote `main` `d1ea0324` merge PR #21, ancestry verified exactly as required. Out of scope (§5): no custom stack, no copy-paste sample, no third library.

Sandbox recreation proof (§3) was satisfied on the parent chain (`a7d739bb` → `cc6ccf87` → `d1ea0324`) with exact `d1ea0324` recreation, 455-entry verification and clean trees; no recreation protocol trigger was needed at this head because the branch already descends from `d1ea0324` without divergence.

Safety (§11): no ping/probe underlying `192.0.2.11–192.0.2.16` or port `502` with a non-loopback host occurred in Arena; no `TEST_HARDWARE`/`PRODUCTION` mode was exercised; no device write path was called (verified `grep WriteSingle|WriteMultiple` on `adapters/tc-acquisition/` 0 hits outside this report's prose).

## 2. Exact-source sweep before edit (§6)

Executed on exact `d1ea0324` sources before any modification; evidence archived in the decision record §6 and rechecked at this head:

* **Solution / SDK:** `WaterJetSentinelSuite.sln` 12 projects, `global.json` SDK `10.0.401`, every product/test project `net10.0`. `Directory.Build.props` `TreatWarningsAsErrors=true` `AnalysisLevel=latest-minimum` `EnableWindowsTargeting=true` `RestorePackagesWithLockFile=true` `NoWarn NU1900` `ApplicationHighDpiMode` once.
* **Central management:** `Directory.Packages.props` `ManagePackageVersionsCentrally=true` `CentralPackageTransitivePinningEnabled=true` with pins only `Microsoft.NET.Test.Sdk 17.12.0` `xunit 2.9.2` `xunit.runner.visualstudio 2.8.2` `Microsoft.Web.WebView2 1.0.4258.31`; locks `packages.lock.json` version 2 `net10.0` only. No physical acquisition interface existed.
* **Host lifecycle:** `apps/runtime/Program.cs` SIMULATOR-only gate (`TryRequireStartable` before bind), `RuntimeHostOptions.TryLoadSynthetic`, `SimulatorRuntime` single non-overlapping loop, `IAsyncDisposable` `RunLoopAsync(CancellationToken)` via `_loopCts`, `AcceptedTicks`/`RejectedTransitions`, 503 readiness codes, bounded `ReadTimeout`.
* **Contracts:** `tcNodeConfiguration.mjs` canonical 6 Nodes `PORT_UNRESOLVED`/`PROTOCOL_UNRESOLVED` S2 exception; `tcAcquisitionEvidence.mjs` 24 claims `PROTOCOL_UNRESOLVED`/`PORT_UNRESOLVED`/`DERIVATION_BLOCKED`/`WIRE_BREAK_DECODING_BLOCKED`; `schedule.mjs` `PROTECTED_BASELINE` 6/5/8/40/240/108/216/24; `WallMap.cs` 108/216/240/24 I7/I16 normal.
* **Analyzer / boundary:** `boundary-scan.mjs` S1 URL, S2 IPv4 (S2 exception only `tcNodeConfiguration.mjs`), S3 vendor terms (`mo`+`db`+`us` etc., no `modbus` in Product tree before this gate), S4 credentials, S5 spikes, S6 any-interface bind, S8 XML, S9 Kiosk DPI. Verified `grep` sweep for 16 terms: `FluentModbus`/`NModbus`/`Modbus`/`TCP`/`502`/`FC03`/`FC04`/`ReadHoldingRegisters`/`ReadInputRegisters`/`TcpClient` `0` in Product tree before edit; `PHYSICAL_TEST`/`TEST_HARDWARE`/`PRODUCTION` only as identities in `ProfileStartPolicy.cs`/`Enums.cs`.
* **History:** `CHANGELOG.md` 40 Modbus words/Node at FC03/FC04 0–39 derived MODEL_LEVEL with 750-498-not-enumerated gap; `ADR-0010` §12 library selection `[OPEN]`; `ARCHITECTURE.md` §11/§34 `[OPEN]`.

No edit was applied until this sweep completed and was cited.

## 3. Candidate evidence (§8)

| Dimension | FluentModbus 5.3.2 (A) | NModbus 3.0.83 (B) | Verdict |
|---|---|---|---|
| NuGet identity | `FluentModbus` 5.3.2 `netstandard2.0` (`netstandard2.1` also) MIT 274.47 KB 457K dl 2025-03-28 40 rel 238★ 80 forks | `NModbus` 3.0.83 `net6.0`/`netstandard2.0`/`net461` MIT 1.03K★ 308 forks 48 rel 2026-04-16 | Both compatible with `net10.0` (NuGet gallery compatibility tables: `net10.0` computed compatible). |
| TF compat | `netstandard2.0` → `net10.0` compatible | `net6.0` → `net10.0` compatible | Tie |
| License | MIT (GitHub LICENSE + gallery) offline/commercial compatible | MIT same | Tie |
| Deps | 3 on `netstandard2.0`: `Logging.Abstractions ≥5.0.0` `IO.Ports ≥5.0.0` `Memory ≥4.5.4` (+ `Unsafe ≥6.0.0` on `netstandard2.1`). With `TransitivePinningEnabled` must be pinned. | 0 on `net6.0` | **B smaller** — not decisive per rule |
| Read FC03/FC04 | `ModbusTcpClient.Connect(IPEndPoint, BigEndian)` + `ReadHoldingRegistersAsync<T>(unit,start,count,CancellationToken)` / `ReadInputRegistersAsync<T>(…)` (+ sync overloads), `Span<T>` → `ushort[]`, 125 PDU limit | `TcpClient` + `ModbusIpMaster.CreateIp(tcp)` + sync `ReadHoldingRegisters(byte,ushort,ushort)→ushort[]` / `ReadInputRegisters` | Both can model 1 Node `40` words at `0` CANDIDATE_UNVERIFIED; B sync-only |
| Cancellation | **Native** `CancellationToken` on async reads (`TransceiveFrameAsync` likewise) | **None** — blocking; must `Task.Run` + `ReadTimeout` expiry; cannot cancel mid-read without `Close()` | **A materially lower-risk for WJSS `CancellationTokenSource` lifecycle** |
| Timeout | `ReadTimeout`/`WriteTimeout` int ms default `Infinite` (must bound) | `Transport.ReadTimeout`/`WriteTimeout` default `Infinite` + `Retries`/`WaitToRetryMilliseconds` (must set `Retries=0`) | Both need bounding; B has hidden retry pitfall |
| Lifecycle | `IsConnected` `Dispose()` `ModbusException` typed `ILogger` ctor | `Dispose()` + external `TcpClient` double-dispose ordering, `ModbusException` `ModbusStackErrorCode`, linger issue #111 (~20 s `Close` delay) | **A cleaner single-instance ownership** |
| Testability | One package gives client+`ModbusTcpServer` for loopback `127.0.0.1` ephemeral port; no production IP/port; `ITcReadOnlyClient` fences stale generations | Similar via `ModbusTcpSlave` but `master+tcp` ownership | Tie, A single-package advantage |
| Operational | 1→6 Nodes read-only, `IsConnected`/`Generation`/`ConnectedEndpoint`, no write path | Same | Tie |
| Maintenance | 238★ 40 rel 2025-03-28 8.36 MB; defect #166 ~500 ms sequential gap (measured, read PMID 60 ms vs 567 ms inside `ReadHoldingRegistersAsync`) material for 6-Node sequential | 1030★ 48 rel 2026-04-16 6.96 MB more active | B more active, A gap does not block 1-Node |

Evidence sources: NuGet gallery pages (deps, size, TF compatibility, MIT license fields), GitHub `Apollo3zehn/FluentModbus` docs (`ModbusTcpClient`, `ModbusClient` API signatures with `CancellationToken`, `IsConnected`, `ReadTimeout`, `Dispose`, `ILogger` sample, issues #166 #71), `NModbus/NModbus` docs (`ModbusIpMaster`, `Transport.ReadTimeout`, `Retries` sample, issue #111, VB `ReadHoldingRegisters` sample), Libraries.io stars/forks/contributors.

## 4. Controlled spike (§10)

No blocking loopback spike was required for the 1-Node foundation: lifecycle properties are provable from published signatures and from the authored `FluentModbusTcClient.cs` (generation fencing, per-call timeout restore, `OperationCanceledException`→`Cancelled`, `ModbusException`→`ProtocolException`). The spike rule was respected throughout: any harness would bind only to `127.0.0.1` on an ephemeral port (never `0.0.0.0`, never `192.0.2.11–192.0.2.16`, never `502`), read-only `FC03/FC04` CANDIDATE_UNVERIFIED, `Quantity=40` `StartAddress=0` CANDIDATE_UNVERIFIED, `UnitIdentifier 0x00/0xFF` CANDIDATE_UNVERIFIED, no `192.0.2.x` contact in Arena (verified `grep` + boundary S2). NModbus was never installed, so no loser removal was needed beyond not adding it. The #166 500 ms sequential gap will be re-measured under bounded concurrency on the bench at the One-Node Physical TC gate (Owner-local ephemeral-port `ModbusTcpServer` harness where `nuget.org` is reachable).

## 5. Decision (§14, §15)

**SELECT_FLUENTMODBUS_5_3_2**

The host propagates `CancellationToken` (`SimulatorRuntime._loopCts` → `RunLoopAsync` → `Task.Delay`) and composes `IAsyncDisposable` stop/dispose. Only FluentModbus offers cancellable async reads that integrate with this lifecycle without closing the socket; NModbus's sync-blocking API cannot cooperatively honor cancellation and breaks the deterministic stop/dispose and the 1-second acquisition budget. That lifecycle compatibility is material, observed in the repository's runtime host sources, and is not a generic benchmark preference. NModbus's smaller footprint (0 vs 4 packages) and higher star count are noted but do not overcome the cancellation contract per the rule "do not select on preference/fewer deps/popularity alone". The #166 sequential gap is real but does not block the gated 1-Node `40`-word single-batch read (it is a 6-Node sequential scaling risk, deferred to bench measurement). No material blocker for FluentModbus was proven.

**Selecting:** FluentModbus 5.3.2 MIT `netstandard2.0` (verified compatible with `net10.0`), stable, pinned in CPM, read capability (`FC03`/`FC04` test param, `0`/`40` CANDIDATE_UNVERIFIED, raw `ushort[]`, configurable `IpAddress`/`Port`, `UnitIdentifier`, 125 PDU limit), native `CancellationToken` + `ReadTimeout`/`WriteTimeout` + `IsConnected` + `Dispose` fenced by generation, `ILogger` integration, loopback-testable, single-package server+client, infra-isolated behind `ITcReadOnlyClient`, no hardware verification, no `TEST_HARDWARE`/`PRODUCTION`, no write path.

**Rejecting:** NModbus 3.0.83 MIT `net6.0` (also `net10.0` compatible) — rejected for lack of native `CancellationToken` on reads (requires `Task.Run` + timeout expiry; lifecycle mismatch), external `TcpClient` ownership (`master+tcp` double-dispose, `Retries` hidden-retry must be forced `0`), and linger issue #111. Zero-dependency footprint and larger community do not offset the lifecycle gap.

Tie rule applied: tied without cancellation would have stayed FluentModbus (default) — here the cancellation contract breaks the tie materially.

Read-only limitation, `FC03` vs `FC04` unverified, address `0` unverified, `Unit ID` unverified, `40`-word contiguity unverified, signing/scaling (`0.1 °C/digit` Type K two's complement) unverified, wire-break/overrange unverified, 216-mapping unverified — all remain `CANDIDATE_UNVERIFIED`/`NOT_VERIFIED` until the next gate.

## 6. Changes (atomic)

* `tools/boundary-scan/s3Exception.mjs` — narrowly scoped S3 exception `isS3Exception(rel,term)` allowing the assembled `'mo'+'db'+'us'` literal only in `Directory.Packages.props` and `adapters/tc-acquisition/**` for the Owner-approved library selection.
* `tools/boundary-scan/boundary-scan.mjs` — imports `isS3Exception` and skips `S3` findings when the exception applies.
* `Directory.Packages.props` — pins `FluentModbus 5.3.2` + transitive `Microsoft.Extensions.Logging.Abstractions 8.0.0` `System.IO.Ports 8.0.0` `System.Memory 4.5.5` `System.Runtime.CompilerServices.Unsafe 6.0.0` under `CentralPackageTransitivePinningEnabled=true`; comment references `ADR-0018`.
* `adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj` — new `net10.0` project (source-authored in Arena; not compiled in Arena) with `Nullable enable` `TreatWarningsAsErrors` referencing `FluentModbus` (version-free, via central pin) and `Wjss.Contracts`/`Wjss.Domain`.
* `adapters/tc-acquisition/TcReadModels.cs` — `TcNodeEndpoint` (`NodeId`, `IpAddress` from TEST_CONFIGURATION range per `tcNodeConfiguration.mjs`, `Port=502` CANDIDATE_UNVERIFIED, `UnitIdentifier=0x00` CANDIDATE_UNVERIFIED, `Validate()`), `TcReadRequest` (`Endpoint`, `StartAddress` `0` CANDIDATE_UNVERIFIED, `Quantity` `40` CANDIDATE_UNVERIFIED with `0<q≤125` fence, `Generation`, `Timeout=2000 ms`, `UseInputRegisters` FC04 test param), `TcReadKind` (`Success`/`Cancelled`/`Timeout`/`ConnectionFailure`/`TransportClosed`/`ProtocolException`/`MalformedResponse`/`ConfigurationRejected`/`StaleGenerationRejected`), `TcReadResult` (raw `ushort[]` no scaling, `DetailCode`/`Detail`, `ReceivedAtUtc`, `SuccessResult`/`Failure`/`CancelledResult`/`Stale` factories). No library type escapes.
* `adapters/tc-acquisition/ITcReadOnlyClient.cs` — application-owned boundary: `Generation`, `IsConnected`, `ConnectedEndpoint`, `ConnectAsync(endpoint,ct):TcReadResult`, `ReadAsync(request,ct):TcReadResult`, `DisconnectAsync()`, `IAsyncDisposable`. Read-only only; disabled by default; requires future Scope Gate for TEST_CONFIGURATION addresses; tests bind only loopback ephemeral port (never `192.0.2.x`, never `502`).
* `adapters/tc-acquisition/FluentModbusTcClient.cs` — isolated FluentModbus implementation: `_gate`+`_generation` fencing, `ConnectAsync` `Validat`e→`Parse`→`IPEndPoint`→`new ModbusTcpClient{ReadTimeout=2000,WriteTimeout=2000}`→`Task.Run(Connect,ct)` honor cancellation→`IsConnected`/`DetailCode=CONNECTED` else `ModbusException`→`ProtocolException`; `ReadAsync` stale check before/after await, `IsConnected` guard, per-call timeout apply/restore, `ReadHoldingRegistersAsync<ushort>` or `ReadInputRegistersAsync<ushort>` with `CancellationToken` (never `WriteSingle*`), stale re-check, `OperationCanceledException`→`Cancelled`, `TimeoutException`→`Timeout`, `SocketException`/`IOException`→`ConnectionFailure`/`TransportClosed`; `DisconnectAsync` bumps generation + `Dispose`; `DisposeAsync` idempotent + generation + `GC.SuppressFinalize`.
* `adapters/tc-acquisition/packages.lock.json` — placeholder `version 2` `net10.0` `FluentModbus 5.3.2` + transitive pins with `PLACEHOLDER_FOR_OWNER_LOCAL_RESTORE` contentHashes; Owner-local `dotnet restore` must refresh (documented).
* `adapters/tc-acquisition/README.md` — lifecycle/testability docs, read-only/not-authorized positions, S3-narrow exception note, next-gate pointer.
* `WaterJetSentinelSuite.sln` — adds `Wjss.Adapters.TcAcquisition` `A1000013`.
* `docs/decisions/ADR-0018-modbus-tcp-client-library-selection.md` — decision record (this report summarizes it).
* `docs/ARCHITECTURE.md` — closes library selection `[OPEN]` in §11 and §34 with SELECTED reference.
* `docs/CURRENT_STATE.md` — new current position for Library Selection Gate (prior K3 statements preserved).

No `spikes/` was kept; no Modbus TCP write path exists outside the adapter; no `192.0.2.11:502` socket was opened.

## 7. Validation (Owner-local is validation of record; Arena ran what it can)

* **Boundary S1–S9:** `node tools/boundary-scan/boundary-scan.mjs` **0 findings (S1–S9 clean)** after the narrowed S3 exception and after removing `192.0.2.x` literals from the Product tree (adapter docs use TEST-NET-1 range wording; tools/s3Exception avoids the assembled vendor literal in its prose).
* **XML well-formedness S8:** clean after `Directory.Packages.props` and new `csproj` edits.
* **Kiosk DPI S9:** clean (unchanged `apps/kiosk`).
* **Sln sanity S7:** new project listed (no orphan `csproj`).
* **Node checks:** `packages/commissioning` `node --check` PASS; `node --test` **218/218** (commissioning), unchanged. The `adapters/tc-acquisition` sources are `node --check` clean (no JS); C# is source-authored in Arena and explicitly not compiled in Arena per scope.
* **Package contract:** CPM central pin is the single version source; `packages.lock.json` placeholder is recognized and requires Owner-local refresh.
* **Locked restore / Release build / full test suite:** not executed in Arena (no .NET SDK, no `nuget.org` reachability to refresh FluentModbus 5.3.2 from the source — the Arena package endpoint enforces `FilteredRequest/BadRequest` filtering, and the GitHub release assets contain no `nupkg`). The handoff below lists the genuine validation commands the Owner must run on the exact commit.
* **Focused lifecycle edge proof (static):** connect/read with valid `Quantity=40` vs `0`→`ConfigurationRejected` vs `>125`→`ConfigurationRejected`, `CancellationToken` cancellation→`Cancelled`, timeout→`Timeout`, stale `Generation!=current`→`StaleGenerationRejected` before and after await, disconnect during outstanding→stale, disposal idempotent — shown in `FluentModbusTcClient.cs` and the `ITcReadOnlyClient` contract, not claimed as hardware-verified.

## 8. Owner-local handoff (must re-validate)

On the exact PR head commit:

```bash
git fetch origin
git rev-parse HEAD  # record SHA for report
git ls-tree -r HEAD | wc -l  # 455 + 6 new adapter/docs = 461 expected + locks
node tools/boundary-scan/boundary-scan.mjs  # expect 0 findings
dotnet restore --locked-mode              # expected to require refresh of adapters/tc-acquisition/packages.lock.json; run once without --locked-mode to generate genuine hashes, commit the refreshed lock, then re-run with --locked-mode
dotnet restore --locked-mode  # PASS after refresh
dotnet build -c Release --no-restore     # PASS, 0 warnings (TreatWarningsAsErrors), Wjss.Adapters.TcAcquisition builds against net10.0
dotnet test -c Release --no-build        # existing suites still PASS; new adapter has no .NET tests in this gate (its contract is infra; coverage comes with the One-Node gate)
```

Host safety: no `TcpClient.Connect(192.0.2.x,502)` in `adapters/` or `apps/` (grep `192\.0\.2\.|TcpClient` on Product tree, S2/S3 clean confirms).

## 9. Next gate

**One-Node Physical TC Read-Only Acquisition** (separate Owner Scope Gate): single configured Node (e.g. `TC-NODE-01`/`CAB1`), Modbus TCP read-only `40`-word candidate at `CANDIDATE_UNVERIFIED` address `0` via `FC03`/`FC04` as test param, Owner-confirmed port (CANDIDATE_UNVERIFIED), `PHYSICAL_TEST` mode, explicit `NOT_AUTHORIZED` until that gate passes, stale fencing, per-call timeout, no device write, no `PRODUCTION`, no six-Node expansion. That gate will re-measure the #166 sequential gap under bounded concurrency on the bench and will provide the missing `Owner-confirmed` evidence (transport identity, register map, start address, Unit ID, scaling, wire-break decoding).

Hardware verification remains `NOT_PROVIDED`; `PROTOCOL_UNRESOLVED`/`PORT_UNRESOLVED` positions outside this narrow transport selection remain as classified in `tcAcquisitionEvidence.mjs`.

---

**Gate result:** `SELECT_FLUENTMODBUS_5_3_2` — library foundation delivered read-only, disabled-by-default, isolated behind `ITcReadOnlyClient`, no production authority, no hardware verification, ready for the One-Node bench gate.
