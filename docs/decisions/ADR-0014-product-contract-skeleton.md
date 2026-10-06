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
   S1–S3).

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

- Contracts, validator, fixtures: authored and Node-validated in Arena (15/15 tests green
  after the Owner-review correction of 2026-10-07; the 14-test count pre-dates it).
- C# compilation of the contracts and the parity test: `NOT RUN IN ARENA` (no .NET SDK;
  sandbox network blocks it). **Owner-local run is the gate** — see
  [`../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](../STAGE_0.3A_OWNER_LOCAL_VALIDATION.md).

## References

[`../QUEUE_MODEL.md`](../QUEUE_MODEL.md), [`../ARCHITECTURE.md`](../ARCHITECTURE.md),
[`../TEST_STRATEGY.md`](../TEST_STRATEGY.md),
[`../../packages/contracts/fixtures/README.md`](../../packages/contracts/fixtures/README.md),
[`../../tools/fixtures/README.md`](../../tools/fixtures/README.md).
