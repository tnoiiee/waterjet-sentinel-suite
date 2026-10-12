# WJSS TC Acquisition Adapter — Read-Only Foundation (Library Selection Gate)

**Stage:** Library Selection Gate (ADR-0018), on main `d1ea0324`.
**Library:** FluentModbus 5.3.2 (MIT, netstandard2.0, compatible with net10.0) — selected; NModbus 3.0.83 rejected.
**Boundary:** Application-owned `ITcReadOnlyClient` behind `adapters/tc-acquisition/`. No FluentModbus types escape to Domain, Runtime contracts, Kiosk or Mapping.

## What this adapter does

- One connection per instance (one Node, one session owner, serialized per-device queue future).
- Connect to a configured TC Node endpoint (IP from the six TEST_CONFIGURATION addresses defined in `tcNodeConfiguration.mjs` — TEST-NET-1 documentation range — port 502 CANDIDATE_UNVERIFIED), read a read-only register block (FC03 Read Holding Registers or FC04 Read Input Registers as a test parameter, both CANDIDATE_UNVERIFIED, quantity 40 at start 0 CANDIDATE_UNVERIFIED), return raw `ushort[]` plus communication metadata, accept `CancellationToken`, dispose cleanly, fence stale generations.
- Read-only only: never calls FC05/FC06/FC15/FC16 (write/coil). No hardware verification, no scaling, no wire-break decoding.

## What it does NOT do (NOT AUTHORIZED / NOT VERIFIED)

- No ping, no probe of the six TEST_CONFIGURATION TC Node addresses, no port 502 contact with non-loopback hosts in Arena.
- No TEST_HARDWARE, no PRODUCTION, no device write, no automatic polling, no Cleaning Queue effects.
- FC03 vs FC04, start address, Unit ID, 40-word contiguity, signing, 0.1 °C scaling, wire-break/overrange identities all remain CANDIDATE_UNVERIFIED until the later One-Node Physical TC gate provides physical evidence.

## Lifecycle

- `ConnectAsync(endpoint, ct)` — increments generation, disposes any prior client, connects via `ModbusTcpClient.Connect(IPEndPoint)`, honors cancellation, maps `ModbusException` → `ProtocolException`, `TimeoutException` → `Timeout`, `SocketException`/`IOException` → `ConnectionFailure`/`TransportClosed`.
- `ReadAsync(request, ct)` — checks `request.Generation == current Generation` else `StaleGenerationRejected`, checks `IsConnected`, applies per-call `Timeout` to `ReadTimeout`/`WriteTimeout`, awaits `ReadHoldingRegistersAsync`/`ReadInputRegistersAsync` with `CancellationToken`, re-checks generation after await, restores timeouts.
- `DisconnectAsync()` — bumps generation, disposes client; outstanding reads become stale.
- `DisposeAsync()` — idempotent, bumps generation, disposes client.

Stale fencing: every Connect/Disconnect increments `_generation` under `_gate`; ReadAsync captures generation at entry and again after await.

## Testability

Tests bind only to loopback (`127.0.0.1`) on ephemeral ports (never 502, never the TEST_CONFIGURATION range). A loopback server from the selected library can be used for local lifecycle tests; the adapter's contract is exercised without claiming field I/O module behavior.

## S3 exception

The product tree normally forbids the vendor transport term (assembled in the scanner as 'mo'+'db'+'us'). This adapter and the central pin in `Directory.Packages.props` are the ONLY exception (see `tools/boundary-scan/s3Exception.mjs`).

## Next gate

One-Node Physical TC Read-Only Acquisition (single configured Node, 40-word candidate read, PHYSICAL_TEST mode, no write) — requires a separate Owner-approved Scope Gate and physical evidence.
