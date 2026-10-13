> **Current software-only one-Node validation closeout (2026-10-13):** exact implementation
> `c0bc2c71636d3206098fd890e1de40c46fabae65`, genuine harness/test locks
> `e71e33b22d039b6cd5940fbe94da1879f71142fa`. Owner targeted/full Release builds 0 warnings /
> 0 errors; focused 81/81, full .NET 536/536, fixture parity 7/7, dry-run SIMULATION only.
> Agent lock/static/Node validation is separate: no Agent .NET execution. Runtime/Kiosk remain
> unchanged/disconnected. HARDWARE CONTACT NOT AUTHORIZED; T01 NOT AUTHORIZED;
> PASSIVE CAPTURE NOT AUTHORIZED; PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED.
> Final docs/proof gates precede software MERGE READY recommendation. See
> [scope closeout](../../docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md) for both LF/CRLF hash evidence.
> Library Selection/next-gate checkpoint passages below are dated history, not new authority.

# WJSS TC Acquisition Adapter — Read-Only Foundation (Library Selection Gate)

**Stage:** Library Selection Gate (ADR-0018), on main `d1ea0324`.
**Library:** FluentModbus 5.3.2 (MIT, netstandard2.0, compatible with net10.0) — selected; NModbus 3.0.83 rejected (see ADR-0018 for corrected comparison: NModbus exposes Task-returning async operations without per-operation CancellationToken).
**Boundary:** Application-owned `ITcReadOnlyClient` behind `adapters/tc-acquisition/`. No FluentModbus types escape to Domain, Runtime contracts, Kiosk or Mapping.

## What this adapter does

- One connection per instance (one Node, one session owner, serialized per-device queue future).
- Connect to a configured TC Node endpoint (IP from the six TEST_CONFIGURATION addresses defined in `tcNodeConfiguration.mjs` — TEST-NET-1 documentation range — port 502 CANDIDATE_UNVERIFIED), read a read-only register block via explicit `TcRegisterArea` candidate (`HoldingRegisters` FC03 or `InputRegisters` FC04, both CANDIDATE_UNVERIFIED, no fallback, quantity 40 at start 0 CANDIDATE_UNVERIFIED), return application-owned `ushort[]` (copied from transport-owned memory) plus communication metadata, accept `CancellationToken` for reads, enforce one outstanding read per client, dispose cleanly, fence stale generations.
- Read-only only: never calls FC05/FC06/FC15/FC16 or FC23. No hardware verification, no scaling, no wire-break decoding.

## What it does NOT do (NOT AUTHORIZED / NOT VERIFIED)

- No ping, no probe of the six TEST_CONFIGURATION TC Node addresses, no port 502 contact with non-loopback hosts in Arena.
- No TEST_HARDWARE, no PRODUCTION, no device write, no automatic polling, no Cleaning Queue effects.
- FC03 vs FC04, start address, Unit ID, 40-word contiguity, endianness (BigEndian network order), signing, 0.1 °C scaling, wire-break/overrange identities all remain CANDIDATE_UNVERIFIED until the later One-Node Physical TC gate provides physical evidence. Word ordering for multi-register values remains unverified.

## Lifecycle

- `ConnectAsync(endpoint, ct)` — increments generation, disposes any prior client, validates endpoint, checks cancellation before start, creates `ModbusTcpClient{ConnectTimeout=2000,ReadTimeout=2000,WriteTimeout=2000}`, connects synchronously via `Connect(IPEndPoint, BigEndian)` (explicit BigEndian network order, CANDIDATE_UNVERIFIED; Connect is synchronous, bounded by `ConnectTimeout`, not cooperatively cancellable mid-handshake — cancellation during the blocking wait does not interrupt the socket; a late success is fenced by generation and disposed), checks cancellation after, fences stale generation/dispose races, maps `ModbusException` → `ProtocolException`, `TimeoutException`/`TcpConnectTimeout` → `Timeout`, `SocketException`/`IOException` → `ConnectionFailure`/`TransportClosed`.
- `ReadAsync(request, ct)` — validates `TcRegisterArea` (explicit, no fallback) and `Endianness` (must equal `BigEndian`), quantity `1..125` (else `ConfigurationRejected`), generation, `IsConnected`, one-outstanding gate (`READ_BUSY` if busy), per-call timeout via linked `CancellationTokenSource` (no shared `ReadTimeout` mutation, no disposed-client access, no hidden-cleanup exception), requires request endpoint equality with connected endpoint, awaits existing byte-payload `ReadHoldingRegistersAsync`/`ReadInputRegistersAsync` overloads with linked token, validates exact returned byte payload (two bytes per requested word), copies transport-owned bytes, converts the explicit BigEndian candidate to application-owned words, re-checks generation after await (`StaleGenerationRejected`), maps `OperationCanceledException` (caller cancellation → `Cancelled`, per-call timeout → `Timeout`), `ModbusException` → `ProtocolException`, `TimeoutException` → `Timeout`, `SocketException`/`IOException` → `ConnectionFailure`/`TransportClosed`.
- `DisconnectAsync()` — bumps generation, disposes client; outstanding reads become stale.
- `DisposeAsync()` — idempotent, bumps generation, disposes client; read gate (`SemaphoreSlim`) is intentionally **not disposed** (managed admission state left for GC to avoid in-flight `Release()` race hiding primary outcome — see `FluentModbusTcClient.cs`).

Stale fencing: every Connect/Disconnect/Dispose increments `_generation` under `_gate`; ReadAsync captures generation at entry and again after await. Transport-owned `Memory` is copied via `ToArray()` before leaving the boundary.

## Testability

Tests bind only to loopback (`127.0.0.1`) on ephemeral ports (never 502, never the TEST_CONFIGURATION range). The focused tests use a test-only read-response responder and fake clients; the adapter's contract is exercised without claiming field I/O module behavior. Endianness mode is explicit and candidate; no write calls; production/physical endpoints absent from tests.

## S3 exception

The product tree normally forbids the vendor transport term (assembled in the scanner as 'mo'+'db'+'us'). The existing adapter/central-pin exception is retained; the one-Node policy additionally allows the selected term ONLY at exact normalized `apps/tc-bench/packages.lock.json` and `tests/tc-acquisition.tests/packages.lock.json` (see `tools/boundary-scan/s3Exception.mjs`). No prefix, sibling/nested/backup/renamed lock, source/docs/config or other-vendor exemption. Scanner engine and S1/S2/S4–S9 remain unchanged; no generic write-identifier scanner claim is made. Separate API/source proof establishes no write/control invocation.

## Historical next gate — Library Selection checkpoint

One-Node Physical TC Read-Only Acquisition (single configured Node, 40-word candidate read, PHYSICAL_TEST mode, no write, no Production, no six-Node until one-Node passes) — after Owner merges PR #22, requires NEW SESSION, platform-assigned new branch, Standing Sandbox Recreation Protocol, and a separate Owner-approved Scope Gate before any physical work — see `docs/MASTER_PLAN.md` and `docs/STAGE_LIBRARY_SELECTION_GATE_REPORT.md` (current heads `dfce519` consolidated documentation-continuity checkpoint parent `fdc81d8` report-integrity checkpoint, genuine lock `49e4043`, validated 455/455 S2 14/14 S3 11/11 S1–S9 clean Commissioning 218/218) — **Owner-local validation passed** (genuine restore PASS, locked restore PASS×2 no drift, Release builds PASS, 455/455), **physical evidence remains NOT VERIFIED** (no field-hardware bench, no TEST_HARDWARE/PRODUCTION, FC/Unit ID/start/byte-word order/scaling/wire-break/timing remain CANDIDATE_UNVERIFIED until One-Node proves them).

## Software-only hardening and limitations

`ENDPOINT_MISMATCH` rejects a different Node ID/address/port/Unit ID before read transmission.
`WORD_COUNT_MISMATCH` rejects excessive/odd payload lengths; the library rejects insufficient
payloads as protocol errors. No padding/truncation. The result still contains copied raw words
and application-owned exception type-name strings, never vendor objects. Caller cancellation
and deadline-caused stream-close exceptions retain cancellation/timeout classification.

The byte-payload overload prevents the library's generic cast from hiding an odd trailing byte.
Payload size does not prove the response byte-count field, Unit/transaction identity or MBAP
framing. These remain independently reviewed passive evidence; no library fork or custom
transport is added. New focused loopback tests are authored but not executed without the SDK.
The generic endpoint model itself is not a physical activation allowlist; the harness owns that
policy. Existing interface, pins, lock and S3 policy are unchanged.
