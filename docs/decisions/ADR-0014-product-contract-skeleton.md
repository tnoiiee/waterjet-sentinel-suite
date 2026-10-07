# ADR-0014 — Product Contract Skeleton and Queue Capacity

- **Status:** DRAFT — authored in the Stage 0.3A-1 source checkpoint. Not `PROPOSED`, not
  `ACCEPTED`. Nothing in this record is binding until the Owner accepts the Stage 0.3A gate
  that it accompanies.
- **Date:** 2026-10-07
- **Supersedes:** Nothing. This record *executes* the contract-boundary decisions already
  `ACCEPTED` in [`../decisions/ADR-0003-queue-arbitration.md`](ADR-0003-queue-arbitration.md),
  [`../decisions/ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md) and
  [`../decisions/ADR-0012-simulator-first-development.md`](ADR-0012-simulator-first-development.md),
  and adds one correction to the spike transport semantics (below).
- **Scope:** the shape of `packages/contracts/Wjss.Contracts` (snapshot, delta, command,
  status, queue, config types), the queue capacity decision, the profile-start rule, and the
  fixture generation policy that keeps the C# contract, the TypeScript mirror, and the golden
  fixtures from drifting apart.
- **Authority:** Owner Option-C amended Stage 0.3A Scope Gate (source checkpoint authority).

## Context

Stage 0.2.1A produced a working spike transport (`spikes/ui-runtime-react/runtime-node`)
whose wire format is the only observed shape the React UI can consume. Stage 0.3A moves that
shape into the product tree as .NET records. Two problems must be settled before code:

1. The spike encoded "no active job" as an explicit `activeJob: null` **and** omitted every
   other unchanged field — a mixed encoding that the spike only got away with because it
   hand-serialized JSON. System.Text.Json cannot emit an explicit null for one optional
   property while omitting the rest, without custom converters.
2. The queue capacity and the refusal behaviour when the queue is full were never fixed in a
   product-owned record; the spike used a list of undefined length.

## Decision

1. **Single authoritative contract.** `Wjss.Contracts` (C# records, net10.0, nullable on) is
   the only source of contract truth. The TypeScript mirror
   (`packages/contracts/wjss-contracts-ts`) is structural documentation: its tests validate
   fixture shape, but any type disagreement resolves in favour of C#.
2. **Delta keeps the accepted three-state `activeJob` encoding** (the 0.2.1A
   spike baseline, confirmed by the Stage 0.3A-1 Owner review of 2026-10-07):
   - `activeJob` ABSENT ⇒ unchanged;
   - `activeJob` OBJECT ⇒ whole-record replacement;
   - `"activeJob": null` ⇒ clear the Active Job (release / Safe-Return complete).
   Because System.Text.Json cannot distinguish "property never set" from
   "property set to null" on a bare nullable property, this is implemented
   with a small structural presence wrapper (`Optional<T>` plus a converter
   scoped to `OperationalDelta.ActiveJob` via `[JsonConverter]`). **This is a
   structural contract technique, not Production policy**: no other Delta
   field gains an explicit-null clear encoding in this checkpoint.
   *History:* the first checkpoint draft had replaced `activeJob: null` with a
   second boolean property (`activeJobCleared`) on serialization-convenience
   grounds; the Owner review rejected the deviation and the baseline above was
   restored in the same PR. The rejected flag is not part of the contract;
   the TypeScript validator rejects any payload that carries it.
3. **Queue capacity is 8, contiguous, head-only consumption.** Positions are 1..N, never
   sparse. A dequeue removes position 1; survivors shift down and the batch revision
   increments by exactly 1. Enqueue beyond capacity 8 is refused (HTTP 409, stable refusal
   reason `QUEUE_FULL`) — never silently dropped, never truncated. Rationale: ADR-0003 fixes
   arbitration at the Runtime; 8 is the maximum number of cleaning targets an operator can
   meaningfully supervise from one kiosk screen at 15.6-inch scale (Owner may re-pin; the
   constant lives in `QueueRules` only).
4. **Profile start is fail-closed.** `Production` refuses to start unless an explicit
   override file path is supplied *and* the (future) approval evidence gate is satisfied;
   until the override type exists, `Production` and `TEST_HARDWARE` always refuse. An
   unknown or missing profile is `INVALID`; there is **no** downgrade path to `SIMULATOR`.
   This executes ADR-0012's "simulator by default, never by accident".
5. **Sequence-record ordering is normative:** `valveCmd < valveConfirm < axisCmd <
   standbyConfirm < outcome < release`, and a delta for `HOLD_FOR_AXIS` may not contain an
   outcome before its standby-confirm (validator rule).
6. **Golden fixtures are emitted, not hand-written.** The only fixture generator is the
   xUnit-visible `FixtureGenerator` class in `tests/integration`. Committed fixtures under
   `packages/contracts/fixtures/` are PROVISIONAL (Node-authored in Arena to unblock the TS
   validator) and are superseded by the first Owner-local `dotnet test` run with
   `WJSS_UPDATE_FIXTURES=1`. Parity is then enforced by `FixtureParityTests` failing on drift.
7. **Config examples** (`config/examples/*.example.json`) carry structure and simulator-only
   values; device addresses, register-to-signal mappings, tag inventories, coordinates,
   limits and credentials never enter the repository (see `tools/boundary-scan`, rules
   S1–S3). The sensor-map example follows the canonical matrix structure: 18 columns,
   6 rows, 108 slots (106 sensor + 2 cannon), sensors-per-wall 24/29/24/29 — the
   `logicalMatrix` shape the publication pipeline (0.3A-F) will later consume.
8. **Sensor-map `tcChannels` is a structured channel array** (Owner round 6, 2026-10-07).
   In the sensor-map configuration example, each SENSOR slot carries `tcChannels` as a JSON
   array of exactly two thermocouple channel strings — never a comma-delimited scalar.
   Deterministic order: index 0 is the pair's lower channel index (front), index 1 the
   higher (rear). CANNON slots never carry `tcChannels`. The shape is contractual:
   `SensorMapSlotExample` + `TcChannelRules` (`packages/contracts/TcChannels.cs`) reject a
   scalar string (System.Text.Json cannot bind it to the array property), a wrong array
   length, empty entries, duplicate channels within a sensor, duplicate channels across
   sensors, and any cannon-as-sensor composition; the totals are fixed at 108 slots,
   106 sensors, 2 cannons, exactly 2 channels per sensor, and 212 globally unique
   channels. This record applies to the slot-array form only: snapshot/delta presentation
   keeps its existing separate `tcFrontChannel`/`tcRearChannel` fields.
   *History:* the Arena-authored example originally encoded the pair as a comma-delimited
   string and its test asserted that by splitting the string. The Owner detected the shape
   defect during local validation; it is documented here as a correction made before merge,
   and comma-delimited `tcChannels` must never be accepted again.

## Alternatives considered

- **A second boolean clear property instead of the explicit null.** Rejected by the Owner
  review: it changes the accepted wire contract to work around a serializer limitation that
  one scoped presence wrapper resolves cleanly.
- **Registering the Optional converter globally (affecting all fields).** Rejected: other
  fields keep their existing "absent means unchanged, null never written" semantics; a
  global converter would invent clear semantics they must not have.
- **Capacity 16 / unbounded queue.** Rejected: ADR-0003 requires deterministic, supervised
  arbitration; unbounded queues hide operator error.
- **Generate the TS mirror from C# in Stage 0.3A.** Rejected: build-time codegen requires the
  .NET SDK on every contributor's UI toolchain before the contract is stable; hand mirror +
  parity fixtures is cheaper until 0.3A-4.

## Consequences

- The product tree now owns the wire format; the spike freezes as historical evidence.
- Every future stage that touches transport must keep the three artifacts (records, TS
  mirror, fixtures) in lockstep — enforced by `FixtureParityTests`, which is a *test*, run
  Owner-local, not a docs promise.
- The UI-side reducer must distinguish all three `activeJob` states (ignore / replace /
  clear); a payload carrying the rejected boolean form is invalid and validator-rejected.

## Verification status

- Owner-local validation (2026-10-07, rounds 3–5): full Release build 0 warnings
  0 errors after the environment root cause (`TargetPath`) and the two analyzer errors
  were fixed; the corrected checkpoint suite passed 50/50, fixture/delta parity 7/7 —
  the earlier `--no-build` result (42/49 over stale assemblies) is explicitly
  non-authoritative. The residual drift of the three provisional files
  (`snapshot.seed0.json`, `delta.basic.json`, `sensor-map.example.json`) led the Owner
  to detect the sensor-map `tcChannels` shape defect corrected by decision 8: the
  committed example was regenerated in array form for TypeScript validation only and
  remains PROVISIONAL pending the Owner regeneration sequence.
- Contracts, validator, fixtures: authored and Node-validated in Arena (TypeScript
  tests 24/24 green after the tcChannels correction; the 15/15 count pre-dates it and
  14/14 pre-dates the earlier Owner-review correction).
- Round-6 code (the tcChannels contract, generator self-validation, and the C#
  channel tests) is authored in Arena, where no .NET SDK exists: the C# is
  compile-reviewed only there; the Owner's local build is the validator.
- Owner-local at `0b088ad` (rounds 6b/6c, 2026-10-07): full Release build **PASS 0
  warnings / 0 errors**; the tcChannels contract, generator mapping, regenerated fixtures
  and parity are VERIFIED (108/106/2 slots; 106 sensor arrays; 212 total = 212 unique
  channels; 0 duplicates; cannons I7/I16; walls 24-29-24-29; CLR `System.Object[]`;
  TypeScript 24/24; parity 7/7; boundary scan clean). The fresh 61-test suite then showed
  2 failures that were formatting-sensitive TEST assertions (serialized-text matching
  across indented JSON), corrected in round 6c to pure structural `JsonValueKind`
  inspection — contract, generator, serializer and fixtures untouched.
- **CLOSED (Owner-local, 2026-10-07):** the final Release build passed with **0 warnings /
  0 errors** and the fresh full suite passed **61/61**; genuine restore-generated lock files
  (12) and the .NET-generated fixtures (3, array-shaped) were committed via the Owner
  handoff and verified statically in Arena; `global.json` now pins SDK `10.0.401`.
  **Stage 0.3A-1 validation gate: PASSED.** This ADR moves from DRAFT toward ACCEPTED as
  the implementation record of the accepted checkpoint; formal acceptance status remains an
  Owner action — see
  [`../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) §10.

## References

[`../QUEUE_MODEL.md`](../QUEUE_MODEL.md), [`../ARCHITECTURE.md`](../ARCHITECTURE.md),
[`../TEST_STRATEGY.md`](../TEST_STRATEGY.md),
[`../../packages/contracts/fixtures/README.md`](../../packages/contracts/fixtures/README.md),
[`../../tools/fixtures/README.md`](../../tools/fixtures/README.md).
