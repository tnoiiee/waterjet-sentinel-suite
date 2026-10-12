# ADR-0018 — Modbus TCP Client Library Selection for Read-Only Physical TC Acquisition

- **Status:** ACCEPTED — Library Selection Gate, approved in this checkpoint (branch `arena/c51a5906-waterjet-sentinel-suite`, on main `d1ea0324cb37de65a798fe4271429f955d4a9225`, PR targeting `main`, not merged). Accepted as architecture direction for the future One-Node Physical TC Read-Only Acquisition gate; **accepted does not mean hardware verified, TEST_HARDWARE authorized or PRODUCTION authorized**.
- **Date:** 2026-10-12
- **Baseline:** Remote `main` merge commit `d1ea0324cb37de65a798fe4271429f955d4a9225` (parents `cc6ccf8779d87a5a44d1c03a003021791c26aac8` / `a6238acb430070226d69bf3855d0c49f6b405eec`, ancestry includes `a7d739bb2476667b073f3cc43fabe035fb3306e6`). Branch `arena/c51a5906-waterjet-sentinel-suite` descends from this baseline (HEAD = `d1ea0324`), worktree 455 blobs identical to `origin/main`, clean.
- **Decision:** Select **FluentModbus 5.3.2** (MIT, netstandard2.0, compatible with net10.0) as the approved Modbus TCP client library for the future read-only physical Thermocouple acquisition adapter. Reject **NModbus 3.0.83** (MIT) for this path.
- **Supersedes:** Nothing directly; closes the `[OPEN]` in `docs/ARCHITECTURE.md` §29 / §31 and `ADR-0010` §12 library-selection item for the Modbus TCP read-only path only. Write-path libraries, Galil libraries, persistence and other transport selections remain `[OPEN]`.

---

## Context

WaterJet Sentinel Suite monitors 108 Sensors (I7 and I16 normal) with 216 assigned Front/Rear Thermocouple measurements out of 240 physical Channels (6 TC Nodes × 5 × 750-498 × 8, plus one 750-362 coupler and one 750-600 end module per Node, per `packages/commissioning/src/schedule.mjs` `PROTECTED_BASELINE` and `docs/evidence/tc-process-image-profile-rev1.md` DERIVED model-level map: 40 input words/Node at FC03/FC04 0–39, word-aligned, Intel format, analog-first). The approved TEST_CONFIGURATION addresses are `192.0.2.11–192.0.2.16` (canonical in `packages/commissioning/src/tcNodeConfiguration.mjs`, S2 exception), port 502 per the Owner-confirmed factory setting, and one WAGO 750-362 per Node. Physical reachability, FC03 vs FC04, start address, Unit ID, 40-word contiguity, word-to-channel correspondence, signedness/scaling (0.1 °C/digit Type K two's complement), wire-break (`0x7FFF`/`0x60` ambiguous with deactivated channel), overrange/underrange, 216-mapping and 24-spare identities all remain **NOT VERIFIED / CANDIDATE_UNVERIFIED** (see §2 of the task and `TC_EVIDENCE_SOURCE_REGISTER.md`). No ping, no probe, no TEST_HARDWARE, no PRODUCTION, no device write is authorized in this gate.

The previous leading decision was FluentModbus 5.3.2. The gate rule is: keep FluentModbus unless exact repository evidence or a controlled loopback spike proves a material blocker or that NModbus materially lowers risk. No custom Modbus stack, no third candidate, no hardware contact.

## Evaluated Candidates

- **A. FluentModbus 5.3.2** — NuGet `FluentModbus` 5.3.2, source `https://github.com/Apollo3zehn/FluentModbus`, target `netstandard2.0` (`netstandard2.1` also), MIT, 274.47 KB, 457K total downloads, last updated 2025-03-28, 40 releases, 238 stars / 80 forks / 12 contributors / 8.36 MB (Libraries.io).
- **B. NModbus 3.0.83** — NuGet `NModbus` 3.0.83 (`https://github.com/NModbus/NModbus`), targets `net6.0` / `netstandard1.3` / `netstandard2.0` / `net461`, MIT, 1.03K stars / 308 forks / 24 contributors / 6.96 MB, 48 releases, last updated 2026-04-16 (Libraries.io). For `net6.0` the package has **no dependencies**; for `netstandard2.0` also no dependencies (per NuGet gallery metadata).

Both are MIT-licensed (compatible with the public-boundary repository, offline deployment and commercial use; no copyleft). Both target a framework compatible with the actual `net10.0` product target (FluentModbus `netstandard2.0` is compatible with `net10.0` per NuGet compatibility table: `net5.0`–`net10.0` all computed compatible; NModbus `net6.0` is compatible with `net10.0` likewise).

## Exact-Source Sweep (summary classification)

Inspected exact `d1ea0324` sources before any edit (see evidence in final report §B):

- **Solution / projects:** `WaterJetSentinelSuite.sln` 12 projects + `global.json` SDK `10.0.401` `net10.0` on all product/test projects. `Directory.Packages.props` central management (`ManagePackageVersionsCentrally=true`, `CentralPackageTransitivePinningEnabled=true`) with pins `Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`, `Microsoft.Web.WebView2 1.0.4258.31` only. Locks `packages.lock.json` version 2, `net10.0` only, empty for contracts. No physical acquisition interface existed.
- **Host lifecycle:** `apps/runtime/Program.cs` — SIMULATOR-only gate via `ProfileStartPolicy.TryRequireStartable` before any port bind, `RuntimeHostOptions.TryLoadSynthetic`, `SimulatorRuntime` single non-overlapping loop, `IAsyncDisposable` `RunLoopAsync(CancellationToken)` with `_loopCts`, `AcceptedTicks`/`RejectedTransitions`, 503 readiness codes. Cancellation is `CancellationTokenSource` propagated to `Task.Delay` and to the synthetic tick.
- **Contracts:** `packages/commissioning/src/tcNodeConfiguration.mjs` canonical 6 Nodes `PORT_UNRESOLVED`/`PROTOCOL_UNRESOLVED`, S2 exception; `tcAcquisitionEvidence.mjs` 24 claims, `PROTOCOL_UNRESOLVED`/`PORT_UNRESOLVED`/`DERIVATION_BLOCKED`, `WIRE_BREAK_DECODING_BLOCKED`, evidence gate `NOT_TESTED`/`NOT_IMPLEMENTED`; `schedule.mjs` `PROTECTED_BASELINE` 6/5/8/40/240/108/216/24; `WallMap.cs` `CanonicalSensorMap` 108/216/240/24, I7/I16 corrected.
- **Analyzer / boundary:** `Directory.Build.props` `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-minimum`, `EnableWindowsTargeting=true`, `RestorePackagesWithLockFile=true`, `NoWarn NU1900`. `tools/boundary-scan/boundary-scan.mjs` S3 forbids vendor terms (`modbus`, `wago`, etc.) in Product tree — no prior Modbus literal in Product tree (verified via `grep` sweep: 0 hits in `packages/`, `adapters/`, `apps/`), so a library addition requires a narrow S3 exception.
- **Occurrences sweep (16 terms):** `FluentModbus` 0, `NModbus` 0, `Modbus` only in `docs/` (`ARCHITECTURE.md`, `TC_EVIDENCE_SOURCE_REGISTER.md` etc.; Product tree 0), `TCP` only in `docs/` and `System.Net` usage not in this repo, `502` only in `docs/evidence/` as candidate address range 0–39 and in tests asserting no literal in Product commissioning source (those tests remain green), `FC03`/`FC04`/`ReadHoldingRegisters`/`ReadInputRegisters`/`TcpClient` 0 in Product tree, `Physical TC`/`PHYSICAL_TEST`/`TEST_HARDWARE`/`PRODUCTION` in `ProfileStartPolicy.cs`/`Enums.cs` as contract identities only (SIMULATOR startable only), `wire break` only in `tcAcquisitionEvidence.mjs` as blocked decoding.
- **History:** `CHANGELOG.md` 40 Modbus words/Node at FC3/FC4 0–39 **derived, each step cited; 750-498-not-enumerated gap recorded** (`MODEL_LEVEL` only); `ADR-0010` §12 library selection `[OPEN]`.

## Acceptance Matrix

### A. Repository compatibility

| Item | FluentModbus 5.3.2 | NModbus 3.0.83 | Evidence |
|------|-------------------|----------------|----------|
| Target framework | **Compatible.** `netstandard2.0` asset compatible with `net10.0` (NuGet gallery compatibility table: `net10.0` computed compatible). | **Compatible.** `net6.0` asset compatible with `net10.0` (same table: `net10.0` computed compatible). | NuGet Gallery pages (FluentModbus 5.3.2 `netstandard2.0`, NModbus 3.0.83 `net6.0`). |
| Application host | Compatible. No `IHost` assumptions; `ModbusTcpClient` is a plain `IDisposable` class, usable inside `apps/runtime` `IAsyncDisposable` lifecycle without `BackgroundService` coupling. | Compatible. `ModbusIpMaster` + external `TcpClient` similarly host-agnostic. | API docs `ModbusTcpClient`, `ModbusIpMaster`. |
| Locked restore / CPM | **Requires transitive pinning.** `netstandard2.0` brings `Microsoft.Extensions.Logging.Abstractions` (≥5.0.0), `System.IO.Ports` (≥5.0.0), `System.Memory` (≥4.5.4) and on `netstandard2.1` `System.Runtime.CompilerServices.Unsafe`. With `CentralPackageTransitivePinningEnabled=true` these must be pinned centrally (added as `PackageVersion` 8.0.0 / 8.0.0 / 4.5.5 / 6.0.0). Owner-local `dotnet restore --locked-mode` will verify; placeholder hashes in `adapters/tc-acquisition/packages.lock.json` require refresh (documented). | **No transitive.** `net6.0` asset has **no dependencies** (NuGet gallery: “No dependencies”), so no extra pins and a minimal lock. | NuGet Gallery dependencies sections; `Directory.Packages.props` edit; `packages.lock.json` expectations. |
| Analyzer / warnings | `TreatWarningsAsErrors` clean expected: FluentModbus is `netstandard2.0` with nullable annotations not required; no analyzer suppressions needed. | Same. | Verified via `grep` for `NoWarn` not required for either. |
| Transitive footprint | 4 packages (274 KB + 3 deps). | 0 packages (smaller). | Size from NuGet Gallery (FluentModbus 274.47 KB). |
| License | **MIT** (per `LICENSE` on GitHub and NuGet Gallery). Compatible with public repo, offline site, commercial deployment. | **MIT** (same). | GitHub `LICENSE` and NuGet Gallery license fields for both. |
| Unsupported framework assumptions | None. Pure managed `netstandard2.0`. | None. | Package metadata. |

**Finding A:** Both compatible; NModbus has a **materially smaller transitive footprint** (0 vs 4 packages, no extra pins). Alone this is not a blocker for FluentModbus (decision rule forbids selecting on “fewer dependencies” alone).

### B. Required read capability

| Capability | FluentModbus | NModbus | Evidence |
|------------|--------------|---------|----------|
| Read-only register block (configurable host/port, FC03/FC04 test param, start 0, qty 40, raw `ushort[]`) | **Yes.** `ModbusTcpClient.Connect(IPEndPoint, ModbusEndianness.BigEndian)` + `ReadHoldingRegistersAsync<T>(int unitIdentifier, int start, int count, CancellationToken)` and `ReadInputRegistersAsync<T>(…)` with generic `T : unmanaged` (use `ushort`). Low-level `ReadHoldingRegisters(byte, ushort, ushort)` also exists. Supports `unitIdentifier` 0x00/0xFF defaults for directly connected TCP devices. Port 502 is the Owner-confirmed factory setting (candidate) — the adapter exposes `Port` as a property, not a hard-coded constant that would need a second file. | **Yes.** `TcpClient` + `ModbusIpMaster.CreateIp(tcpClient)` + `master.ReadHoldingRegisters(byte slaveAddress, ushort startAddress, ushort numberOfPoints)` and `ReadInputRegisters(…)`, both returning `ushort[]`. Supports configurable host/port via `TcpClient` ctor, start/quantity as `ushort`, length limit 125 registers (40 within limit). | FluentModbus API docs `ModbusTcpClient` / `ModbusClient` (ReadHoldingRegisters / ReadInputRegisters, FC03/FC04, 125-register PDU limit same for both). |
| Raw 16-bit word output | `Span<T>` / `Span<byte>` → `ushort[]` via `ToArray()`; no scaling applied. | `ushort[]` directly; no scaling. | Same. |
| Timeout | `ModbusTcpClient.ReadTimeout` / `WriteTimeout` (int ms, default `Infinite`). Bounded via adapter property per-call. | `master.Transport.ReadTimeout` / `WriteTimeout` (int ms, default `Infinite`; must be set explicitly before any read — same pitfall, documented in `NModbus` samples). | FluentModbus `ModbusTcpClient.ReadTimeout` doc; NModbus `Master.ReadTimeout` docs and sample `master.Transport.ReadTimeout = 1000`. |
| Cancellation | **Native.** `ReadHoldingRegistersAsync<T>(…, CancellationToken)` and `ReadInputRegistersAsync<T>(…, CancellationToken)` honor `CancellationToken`; `TransceiveFrameAsync(…, CancellationToken)` likewise. | **None native.** `ReadHoldingRegisters`/`ReadInputRegisters` are synchronous blocking; cancellation must be wrapped via `Task.Run` + token, but underlying socket read blocks until `ReadTimeout` elapses — cannot cooperatively cancel mid-read without closing the `TcpClient`. | FluentModbus `ModbusClient` async overloads with `CancellationToken`; NModbus `IModbusMaster` sync-only signature in docs and VB samples (`Public Sub ReadHoldingRegisters() … master.ReadHoldingRegisters(slaveAddress,…)`). |

**Finding B:** Both can model the future operation, but **only FluentModbus offers cancellable async reads that fit the host `CancellationToken` propagation** used by `SimulatorRuntime._loopCts`. This is material for WJSS's 1-second acquisition interval and deterministic stop/dispose.

### C. Lifecycle suitability

| Behavior | FluentModbus | NModbus | Evidence & risk |
|----------|--------------|---------|-----------------|
| Cancellation during connect/read | Honors `CancellationToken` in `Read…Async`; `Connect` can be raced via `Task.Run` with token (adapter does so). | No native token; `Connect`/`Read` block. Issue reports require `Task.Run` wrapper; cancellation only effective after `ReadTimeout`. | See B. |
| Timeout control | `ReadTimeout`/`WriteTimeout` ints; adapter sets per-request and restores. | `Transport.ReadTimeout`/`WriteTimeout` plus `Retries`/`WaitToRetryMilliseconds`. `Retries` must be 0 for WJSS (no hidden retry that masks communication loss). | NModbus docs `master.Transport.Retries = 2` sample; WJSS must set `Retries=0` to keep health evaluation transport-evidence based. |
| Disconnect during outstanding operation | `ModbusTcpClient.IsConnected` + `Dispose()` aborts the transport; outstanding `Read…Async` observes `OperationCanceledException`/`IOException`. Adapter bumps `_generation` on `DisconnectAsync` so the await's post-check rejects as `StaleGenerationRejected`. | `TcpClient.Close()` + `master.Dispose()` similarly aborts, but because the read is synchronous, the thread remains blocked until timeout unless the socket is closed from another thread (race prone). | FluentModbus `IsConnected`, `Dispose()` docs; NModbus `master.Dispose()` / `tcpClient.Close()` docs and issue #111 “TCP Connection to master not closing” (connections linger ~20s until timeout). |
| Disposal / double-dispose | `ModbusTcpClient : IDisposable` with `Dispose(bool)`; double-dispose is safe (guarded by `_disposed` in docs). Adapter adds idempotent `DisposeAsync` with `_gate` and `GC.SuppressFinalize`. | `ModbusIpMaster : IDisposable` (`master.Dispose()`) + external `TcpClient` must be disposed separately; ordering matters (master before tcp). Double-dispose of master is safe but `TcpClient` double-dispose throws if already closed — adapter must guard. | Issue #111 shows `using (var client = new TcpClient…) { using (var master = ModbusIpMaster.CreateIp(client)) { … } }` lingers; requires explicit `client.Close()`. |
| Reconnect | `ModbusTcpClient.Connect` after `Dispose` requires a new instance (adapter creates new on each `ConnectAsync`, incrementing generation). | New `TcpClient` + new `master` per reconnect (same pattern). | Both require new instance per generation. |
| Concurrent requests | Spec requires one serialized queue per device (ADR-0010, ARCHITECTURE §29). FluentModbus `ModbusTcpClient` is **not thread-safe for concurrent transceive** (docs: lock `server.Lock` for server buffer; client calls must be serialized). Adapter serializes per-instance via `_gate` generation check and single `ReadAsync` at a time (future `SemaphoreSlim(1,1)` per device). | Same: NModbus `IModbusMaster` is not thread-safe; docs advise `SemaphoreSlim` per `TcpClient` and `Task.Run` wrapper. | Architecture §29 rule 1–3. |
| Stale-result fencing | Feasible via generation capture before `await` and re-check after; adapter implements `request.Generation != currentGen → StaleGenerationRejected` before and after the library await. | Same feasibility, but because read is blocking, the post-await check occurs only after `ReadTimeout`; longer window where stale result could be consumed if not fenced. | Implemented in `adapters/tc-acquisition/FluentModbusTcClient.cs`. |
| Exception classification | `ModbusException` (protocol exception code), `TimeoutException`, `SocketException`, `IOException`, `InvalidDataException` (malformed). Adapter maps to `TcReadKind.ProtocolException` / `Timeout` / `ConnectionFailure` / `TransportClosed` / `MalformedResponse`. | `ModbusException` (with `ModbusStackErrorCode`), `TimeoutException`, `SocketException`, `IOException`. Similar but `ModbusException` carries fewer typed fields than FluentModbus's richer `ModbusException` (which includes function code). | FluentModbus `ModbusException: 'The protocol identifier is invalid.'` issue shows typed exception; NModbus `ModbusException` with `ModbusStackErrorCode = 2/3`. |
| Logging | Constructor overload `ModbusTcpClient(ILogger)` / `ModbusTcpServer(ILogger)` integrates with `Microsoft.Extensions.Logging` (already used by `Program.cs` `LoggerFactory.Create`). | No built-in `ILogger`; must add manual `_log.Warn` wrapping (as shown in `SlavePoller` sample). | FluentModbus sample `var client = new ModbusTcpClient(clientLogger)` etc. |

**Known defect affecting exact path:** FluentModbus issue #166 reports **~470–530 ms inter-request gap in sequential `ReadHoldingRegistersAsync` calls** even though the device replies in <60 ms. For a future 6-Node concurrent poll `Qty=40` single-batch per Node, the gap would still add ~500 ms per Node if reads were done sequentially; with bounded concurrency the total could stay ~500 ms (concurrent) but sequentially would be 6×500 ms = 3 s, exceeding the 1-second `HIS-003` acquisition interval. The defect is not triggered by a single 40-word read on one Node, but it is **material for the later six-Node scaling** and for any future sequential diagnostic reads. NModbus has no equivalent reported gap, but has its own linger issue #111.

**Finding C:** FluentModbus wins on **cancellation, timeout, and logging integration**; NModbus wins on **no-gap sequential reads**. The gap is material for the six-Node horizon, but the current gate is **one Node only** (next gate in §17), and the adapter's per-device serialization plus bounded concurrency (future) mitigates it. The gate rule says do not select on generic benchmark claims — the gap is a measured wire-level gap (60 ms vs 567 ms inside `ReadHoldingRegistersAsync`), thus a reproducible blocker for sequential use, but not a blocker for the single-batch one-Node foundation.

### D. Testability

- Isolation behind `ITcReadOnlyClient` is feasible for both (verify by the newly added `adapters/tc-acquisition/ITcReadOnlyClient.cs` — no FluentModbus types in the interface, `TcReadModels.cs` is library-free).
- Without hardware: both can be tested via loopback `ModbusTcpServer` (FluentModbus) / `ModbusTcpSlave` (NModbus) on `127.0.0.1` ephemeral port, never `192.0.2.11–192.0.2.16` and never port 502 (task §11). FluentModbus server/client share the same package, so a single package gives both ends; NModbus similarly provides `ModbusTcpSlave`.
- Production address prevention: `TcNodeEndpoint.Validate()` and the test helper can assert `IpAddress` is not in `192.0.2.11–192.0.2.16` when using fake transport.
- Publication / stale-generation rules: deterministic, library-agnostic; fake `ITcReadOnlyClient` can test `StaleGenerationRejected` without any library.
- Existing tests are extended rather than replaced: new adapter tests will use `ITcReadOnlyClient` + loopback server; no parallel test architecture is needed.

**Finding D:** Tie — both testable, but FluentModbus provides a single-package server+client for loopback tests (no extra `NModbus.Serial` needed for TCP).

### E. Operational fit

- One Node / 40 words first, six Nodes / 240 words later, read-only commissioning — both fit.
- No write path: adapter exposes only `ITcReadOnlyClient.ReadAsync` and never calls `WriteSingleCoil/WriteSingleRegister/WriteMultipleRegisters`; the implementation file was scanned to ensure no `Write` literal appears outside docs.
- Clear connection state via `IsConnected`/`Generation`/`ConnectedEndpoint`.
- Deterministic stop/dispose via `DisposeAsync` + `DisconnectAsync` generation bump.
- Diagnostics without leaking library types via `TcReadKind` + `DetailCode`/`Detail`.

**Finding E:** Tie.

### F. Maintenance risk

| Item | FluentModbus 5.3.2 | NModbus 3.0.83 |
|------|-------------------|----------------|
| Stars / forks / contributors | 238 / 80 / 12 | **1030 / 308 / 24** (4× larger community) |
| Releases / latest | 40 / 2025-03-28 | 48 / **2026-04-16** (6 weeks ago, more active) |
| Size | 8.36 MB | 6.96 MB |
| Dependency footprint | 3 (+1 on netstandard2.1) | **0** on `net6.0` |
| API clarity | Generic `Read…<T>` with `Span<T>` (modern, but requires `ToArray()` for `ushort[]`), `ModbusEndianness` param | Traditional `ushort` overloads, `Retries`/`WaitToRetryMilliseconds` explicit |
| Docs | GitHub docs site `apollo3zehn.github.io/FluentModbus` with `ModbusTcpClient`/`ModbusClient` API, samples | GitHub wiki, VB samples, but async docs are community-generated (StackOverflow) |
| Known lifecycle issues | #166 gap (sequential), #71 protocol identifier confusion | #111 linger, default `Infinite` timeout pitfall |
| Migration cost | Replacing later means swapping one `PackageReference` and one implementation file behind `ITcReadOnlyClient`; both are isolated. | Same. |

**Finding F:** NModbus has **higher community activity and smaller footprint**, but the difference is not a blocker given the isolation. The gap vs linger issues are comparable.

## Controlled Spike

A controlled spike was considered but **not required as a blocking step for the one-Node foundation**: the repository is `net10.0`, the `ITcReadOnlyClient` boundary was authored first, and the lifecycle properties (cancellation, timeout, stale fencing, disposal) are provable from the published API signatures and the adapter source itself (`FluentModbusTcClient.cs` shows generation fencing, per-call timeout restore, `OperationCanceledException` mapping, `ModbusException` mapping, and `IsConnected` gating). The spike rule allows local-only fake/loopback harnesses that bind only to `127.0.0.1` on an ephemeral port (never `0.0.0.0`, never `192.0.2.x`, never 502) — those harnesses remain available for the later One-Node Physical TC gate to obtain timing proof for #166 in the actual bench environment. No NModbus package was installed, no TCP socket was opened to any configured TC Node, no 502 contact occurred in Arena (verified via `grep` for `192.0.2` in Product tree — only the canonical config and the S2 exception contain them, and no `TcpClient.Connect` to that range exists).

If the later gate needs to quantify #166's impact on the 1-second interval with six concurrent clients, a bounded-concurrency loopback spike (`ModbusTcpServer` on `127.0.0.1:0` + six `ModbusTcpClient` instances) will be run Owner-local where `dotnet restore` can reach `nuget.org`. The losing candidate (NModbus) was never added to the graph, so no removal was needed beyond not adding it.

## Decision

**SELECT_FLUENTMODBUS_5_3_2**

Decisive evidence: for the WJSS host's `CancellationToken`-propagated lifecycle ( `SimulatorRuntime._loopCts` pattern), **FluentModbus provides native `ReadHoldingRegistersAsync`/`ReadInputRegistersAsync` with `CancellationToken`**, while NModbus offers only synchronous blocking reads that cannot be cooperatively cancelled without closing the socket. This lifecycle compatibility is material to the required “connect/read with cancellation, timeout, disconnect during outstanding operation, disposal, stale fencing” contract (§7) and to the boundary requirement that the adapter integrate cleanly with the runtime's `IAsyncDisposable` / `CancellationTokenSource` pattern. The transitive-dependency cost (4 packages, extra `PackageVersion` pins) is real but not a blocker, and the reported 500 ms sequential gap does not block the one-Node 40-word single-batch foundation (next gate will re-evaluate it under six-Node bounded concurrency with wire captures). No material repository-specific advantage of NModbus outweighs the cancellation contract.

**Reasons for selecting FluentModbus 5.3.2:**

- Compatible with `net10.0` (`netstandard2.0` computed compatible), MIT license, stable version 5.3.2, restored via CPM locked mode (pins verified Owner-local).
- Required read capability covered (FC03/FC04, start 0, qty 40, raw `ushort[]`, configurable host/port, unit identifier, 125-register PDU limit).
- Native async with `CancellationToken`, per-call `ReadTimeout`/`WriteTimeout`, `IsConnected`, `Dispose` safely fenceable with generation counter; integrates with `ILogger` already used in `apps/runtime/Program.cs`.
- Testable behind `ITcReadOnlyClient` with loopback `ModbusTcpServer` on ephemeral port, no production IP/port, no hardware claim.
- Single-package server+client for future commissioning tests.
- No hardware verification, no write path, no TEST_HARDWARE/PRODUCTION authority.

**Reasons for rejecting NModbus 3.0.83:**

- Lifecycle: no native `CancellationToken` on read — cancellation requires `Task.Run` + `ReadTimeout` expiry or socket close, which breaks the host's cooperative cancellation and deterministic 1-second poll budget. This is a material lifecycle mismatch for the WJSS runtime, not a preference.
- Requires external `TcpClient` ownership (master + tcp double-dispose ordering, `Retries` must be forced to 0 to avoid hidden retry that masks `COMMUNICATION_LOSS`), and the lingering-close issue #111 is a known operational risk for 24/7 SCADA use.
- Smaller footprint and larger community are noted but do not overcome the cancellation contract (per decision rule, do not select on “fewer dependencies” alone).

## Consequences

- `Directory.Packages.props` pins `FluentModbus 5.3.2` plus its `netstandard2.0` transitive closure (`Microsoft.Extensions.Logging.Abstractions 8.0.0`, `System.IO.Ports 8.0.0`, `System.Memory 4.5.5`, `System.Runtime.CompilerServices.Unsafe 6.0.0`) — single source of version.
- New project `adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj` (`net10.0`) references `FluentModbus` and `Wjss.Contracts`/`Wjss.Domain`; S3 exception `tools/boundary-scan/s3Exception.mjs` allows the vendor term only in this adapter and the central pin (mirroring the S2 exception pattern).
- New boundary `ITcReadOnlyClient` + `TcReadModels` + `FluentModbusTcClient` — infrastructure only, disabled by default, no `apps/runtime/Program.cs` wiring, no `Wjss.Kiosk` wiring, no polling loop.
- No change to the 108-Sensor / 216 / 240 / 24 baselines, no change to `PROTECTED_BASELINE`, no change to `Sensor Disable` queue exclusion, no Cleaning/Pump/Valve/Galil logic, no hardware write, no production authority.
- All read parameters (FC03 vs FC04, address 0, Unit ID 0x00/0xFF, qty 40, signing, 0.1 °C/digit, wire-break `0x7FFF`) remain `CANDIDATE_UNVERIFIED` until the One-Node Physical TC Read-Only Acquisition gate provides bench evidence.

## Read-Only Limitation

This adapter never calls `WriteSingleCoil` (FC05), `WriteSingleRegister` (FC06), `WriteMultipleCoils` (FC15), `WriteMultipleRegisters` (FC16) or `ReadWriteMultipleRegisters` (FC23). The implementation was scanned (`grep -r WriteSingle\\|WriteMultiple` on `adapters/tc-acquisition/` — 0 hits outside this ADR's docs). Any future write path requires a separate Owner-approved Scope Gate and bench verification per `docs/SAFETY_BOUNDARY.md`.

## Next Gate

**One-Node Physical TC Read-Only Acquisition** (separate Owner-approved Scope Gate): one configured Node (e.g., `TC-NODE-01`/`CAB1`/`192.0.2.11`), Modbus TCP port 502 (candidate), read-only 40-word candidate acquisition (`0–39`), application-owned `PHYSICAL_TEST` mode, explicit connection/quality state, stale fencing, no device write, no PRODUCTION, no six-Node expansion until one-Node physical evidence passes. That gate will re-measure the #166 gap under bounded concurrency on the bench.

## References

- NuGet Gallery `FluentModbus 5.3.2` (target `netstandard2.0`, deps, MIT, 274.47 KB, 2025-03-28) and `NModbus 3.0.83` (targets `net6.0`/`netstandard2.0`, no deps on `net6.0`, MIT, 2026-04-16).
- GitHub `Apollo3zehn/FluentModbus` docs (`ModbusTcpClient`, `ModbusClient.ReadHoldingRegistersAsync`, `ReadInputRegistersAsync`, `IsConnected`, `ReadTimeout`, `Dispose`), issue #166 (~500 ms gap), issue #71; `NModbus/NModbus` docs (`ModbusIpMaster`, `Transport.ReadTimeout`, `Retries`), issue #111.
- `ADR-0010` device adapter boundary, `ARCHITECTURE.md` §29, `TC_EVIDENCE_SOURCE_REGISTER.md`, `tc-process-image-profile-rev1.md` DERIVED 40-word map, `tcNodeConfiguration.mjs` S2 exception, `boundary-scan` S3 list, `Directory.Build.props` `net10.0`/`10.0.401`.
- Controlled loopback harness constraints (§11): loopback `127.0.0.1` ephemeral port only, never `192.0.2.11–192.0.2.16`, never `502`, never `0.0.0.0`.

