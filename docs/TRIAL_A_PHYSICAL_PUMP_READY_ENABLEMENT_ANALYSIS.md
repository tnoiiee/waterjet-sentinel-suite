# Trial A — Physical Pump-ready Enablement: Capability Analysis

**Scope:** `TRIAL-A-PHYSICAL-PUMP-READY-ENABLEMENT`
**Question asked:** does the current Product already contain an Owner-invoked, bounded, read-only
execution path that can produce the WJSS Pump-ready decision from physical `AI-003+`?
**Answer at the analysed head (`5c270dd`): NO.** The Product contained every building block and no host.
This document records that finding of record, and §5 now records the narrow host that was authorized
afterwards and implemented in `apps/runtime/ProcessIoPumpReadyReadTrial.cs`.

> **Status: ANALYSIS, THEN IMPLEMENTATION. NO HARDWARE EXECUTED.**
> Nothing here was run against a device. No physical Pump evidence exists, and nothing in this document
> may be read as a record that it does. The gap is closed in source; the physical stage is not run.
> **PRODUCTION: NOT AUTHORIZED. MERGE: HOLD.**

---

## 1. Identity this analysis was performed against

| Item | Value |
|---|---|
| Branch | `arena/b179a92e-waterjet-sentinel-suite` (PR #45, OPEN / NOT MERGED) |
| Head | `5c270dd2da0861b2cddeea1bd5a72103eb121c0b` |
| Direct parent | `c72a97aa31294278fe0d1d949f8a2fb4f5afdd02` |
| Tree | `1c03d88119255a6a78ccffb6c47764b5e47ecf49` |
| Approved baseline | `619904efe0a99c7b5fae2262e68c901e30f29957` (ancestor) |
| Worktree / index / untracked / stash | clean / clean / 0 / 0 |

Owner-local validation at this head (Owner-reported, not run in the authoring sandbox): normal restore
PASS, locked restore PASS, no restore drift, Full Release build PASS, `DryIvTrialTests` 57/57, Runtime
API 178/178, Runtime Core 821/821, Full .NET matrix 1624/1624.

## 2. Capability matrix

Everything Trial A needs **exists**, except the one thing that turns the readings into a bounded,
labelled, Owner-invoked run.

| # | Required capability | Present | Exact source |
|---|---|---|---|
| 1 | Connect to the physical Process-I/O endpoint | **Yes** | `ProcessIoPhysicalTransport` (FC02/FC03/FC04 reads; TCP client created lazily, no socket before the first read) |
| 2 | Read `AI-003+` only | **Partly** | The tag, its binding (`InputRegister`, address 2) and `ProcessIoIsolationValves.PumpOutletPressureTag = "AI-003+"` (`ProcessIoContracts.cs:559`) exist; `ProcessIoSession.ObserveAnalogInput` reads one tag at a time (`ProcessIoFoundation.cs:344`). **No host calls it for `AI-003+` alone.** |
| 3 | Report the raw value | **Yes** | `ProcessIoPhysicalObservation.RawValue` (`ProcessIoContracts.cs:646`) |
| 4 | Report the engineering value in bar | **Yes** | `ProcessIoEngineering.PumpOutletPressureBar` (`ProcessIoContracts.cs:276`), applied for `AI-003+` in `TryEngineering` (`ProcessIoFoundation.cs:506`) |
| 5 | Report quality | **Yes** | `ProcessIoPhysicalObservation.Quality` / `IsUsable` |
| 6 | Report the observed timestamp | **Yes** | `ObservedAt` and `ReceivedAt` |
| 7 | Report freshness | **Yes** | `Fresh` plus `IsUsableAt(now, liveGeneration, freshnessBudget)` |
| 8 | Report session generation | **Yes** | `SourceGeneration` |
| 9 | Evaluate the configured Pump-ready setpoint | **Yes, as a rule** | `SequencingPressure.PumpReady` (`SequencingPressure.cs:31`): `Valid` **and** `SourceId == "PUMP_OUTLET"` **and** `Bar > PumpReadySetpointBar` **strictly** |
| 10 | Prove `AI-002+` was not substituted | **No** | The identity separation exists (`PumpInletPressureTag` vs `PumpOutletPressureTag`; `AI-002+` converts through `WcPressureBar`, `AI-003+` through `PumpOutletPressureBar`) but **no output states that `AI-002+` was excluded** |
| 11 | Perform zero writes | **Yes, structurally** | A read needs no `AdmitWrite`; `ProcessIoDryCommandGate` is not consulted on a read path |
| 12 | Open no motion session | **Yes, if a host exists** | Nothing in the Process-Io read path references motion |
| 13 | Command no IV | **Yes, if a host exists** | Reads use `ObserveAnalogInput`; `RequestValve` is never reached |
| 14 | Terminate cleanly with typed exit codes | **No** for this purpose | The only read host is the Node bench, which exits 0/2 and reports counts, not a Pump-ready decision |

**Rows 2, 10 and 14 were the gap** at the analysed head. Rows 1 and 3–9 were, and remain, finished
Product capability with no read-only caller. Rows 2, 10 and 14 are closed by the host recorded in §5:
the trial reads `AI-003+` alone, states the `AI-002+` exclusion in its own output, and terminates with
the typed exit-code contract.

## 3. Why each existing candidate path fails

### 3.1 `Wjss.Runtime` (`apps/runtime/Program.cs`)

The host has exactly three branches before any listener exists: the isolated IV trial
(`Program.cs:97`), the integrated dry composition (`Program.cs:123`), and the SIMULATOR runtime
(`Program.cs:173`). The only Process-Io reference outside those branches is the dry-mode selector read
at `Program.cs:46`. There is **no** Process-Io read branch.

`TEST_HARDWARE_READ_ONLY` + the physical acquisition mode is the **thermocouple** path
(`TcAcquisitionSession`), not Process-Io: it reads temperatures and never touches `AI-003+`.
`TEST_HARDWARE` itself is unstartable by policy (`ProfileStartPolicy`).

### 3.2 `CleaningPathDryComposition` (the integrated dry trial)

It does reach the decision: `CleaningPathCompositionCoordinator.cs:212` calls
`_equipment.ReadPumpOutletPressure()` and `SequencingPressure.PumpReady`. But it composes
`ManualMotionCleaningPathBoundary` first — asking it for a Pump-ready decision means asking it to Home
the axes and run P1–P6. That is motion, which Trial A forbids by construction.

### 3.3 `DryIvTrial` (Trial B)

It evaluates the same rule, but its Pump-ready input is the **software-simulated** sample. Using it for
Trial A would be the exact evidence conflation both packages forbid.

### 3.4 `ProcessIoBenchRunner` (C#, Application layer)

Modes are `SIMULATION`, `PHYSICAL_DI_AI_READ`, `PHYSICAL_DO_SINGLE`, `PHYSICAL_AO_SINGLE`,
`PHYSICAL_FULL_SELECTED_SEQUENCE`, `PHYSICAL_TEST` (refused). The read mode is genuinely read-only, but
it **iterates the whole catalogue** and then reports only counts —
`summary["DI_READS_PERFORMED"]`, `summary["AI_READS_PERFORMED"]` (`ProcessIoBenchRunner.cs:356-357`).
It discards every individual reading and never converts, never evaluates a setpoint, and never mentions
`AI-003+` or `AI-002+`.

Decisive: **it has no production caller.** The only references are the file itself and
`tests/runtime.tests/ProcessIo/ProcessIoBenchRunnerTests.cs`. There is no console project that invokes
it, so it is not an Owner-invoked path at all.

### 3.5 `tools/process-io-bench/bench.mjs` (Node CLI — the one the Owner can actually run)

This is a real bounded console host: `--mode=PHYSICAL_DI_AI_READ`, consent from
`WJSS_PROCESS_IO_CONSENT` only (`bench.mjs:74`, command-line consent actively refused), exit 0 on
`accepted` and 2 otherwise (`bench.mjs:142-149`).

Its read branch (`benchRunner.mjs:259`) does what §3.4 does — reads every DI and AI row,
`summary.DI_READS_PERFORMED` / `AI_READS_PERFORMED` (`benchRunner.mjs:293`) — and additionally emits a
redacted transport ledger whose `payloadSummary` carries each decoded word against its raw address
(`physicalTransport.mjs:283`). So the physical `AI-003+` word is *present* in that JSON, as an
unlabelled input-register-2 entry among 36 analogue reads.

That is not a WJSS Pump-ready decision. Specifically, this path cannot produce:

- the tag identity of the value (address 2 is never labelled `AI-003+`);
- the engineering value in bar (no `PumpOutletPressureBar` call anywhere in the Node tree);
- quality, freshness or session generation (the Node bench has no observation model);
- a setpoint comparison (`SequencingPressure.PumpReady` is C#-only; no JavaScript equivalent exists);
- any statement that `AI-002+` was read but not used;
- a `PHYSICAL_PUMP_READY_EVIDENCE` classification.

**A manual derivation from that ledger is not a Product decision.** An operator could find address 2,
read `0x…`, apply the 0–40 bar scaling by hand and compare it to a setpoint — but the resulting number
would be arithmetic performed *outside* WJSS on *unlabelled* data, with no quality, freshness or
generation attached. Labelling that `PHYSICAL_PUMP_READY_EVIDENCE` would attribute a Product decision to
the Product that the Product never made. It is refused here on exactly that ground.

## 4. Exact missing capabilities

| ID | Missing | Why it cannot be satisfied by configuration or by an existing host |
|---|---|---|
| **M1** | A bounded, Owner-invoked read-only **console host** for one Process-Io tag | No console project references `ProcessIoBenchRunner`; no `Program.cs` branch reads Process-Io |
| **M2** | A per-tag **observation report** (`tag`, raw, bar, quality, `ObservedAt`, `Fresh`, `SourceGeneration`) | Both existing read paths reduce to counts; `ProcessIoPhysicalObservation` is never serialised by any host |
| **M3** | A **Pump-ready evaluation** in a read-only path | `SequencingPressure.PumpReady` is C#-only and only reachable through the motion-composing coordinator or the simulated Trial B |
| **M4** | An explicit **`AI-002+` exclusion** statement | No host reads `AI-002+` deliberately-to-exclude it, and no output carries the fact |
| **M5** | An **evidence classification block** (`source_tag`, `evidence_kind: PHYSICAL`, `physical_pump_verified`, `physical_ai003_verified`, `iv_command_executed: FALSE`, `motion_executed: FALSE`, `waterjet_command_executed: FALSE`) | No existing surface emits these fields for a physical run |
| **M6** | A **typed exit-code contract** for the read trial (0 accepted, 3 setpoint not met / stale, 4 configuration refusal) | The Node bench has two codes (0/2) and no notion of "read succeeded but not Pump-ready" |

None of M1–M6 requires new domain logic. All of it is composition of existing, already-tested parts.

## 5. The narrow addition, as authorized and implemented

One console surface, one file, no new project, no dependency, no topology change.

**`apps/runtime/ProcessIoPumpReadyReadTrial.cs`** — `public static class ProcessIoPumpReadyReadTrial`,
plus one routing branch in `apps/runtime/Program.cs` beside the existing two.
Focused coverage: `tests/api.tests/ProcessIoPumpReadyReadTrialTests.cs`.

**Selector:** `WJSS_PUMP_READY_READ_TRIAL = AI-003_ONLY` (exact literal, compared exactly; absent means
not selected). Reuses `ProcessIoEndpointConfiguration` for the endpoint, but validates the consent
against the published **read-only** `ProcessIoConsentPhrases.PhysicalDiAiRead` phrase. The existing
`TryReadConsent` validates the single-output **write** phrase and is deliberately not reused: write
consent must never authorize a read trial. **No second consent channel and no second variable.**

**Sequence as built (bounded, single-use, read-only):**

1. refuse `PRODUCTION` first; then refuse an absent or inexact selector, `WJSS_DRY_COMMAND_MODE`,
   `WJSS_DRY_IV_TRIAL_MODE` and every profile other than `TEST_HARDWARE`;
2. resolve the endpoint and validate the read-only consent; load the catalogue with the existing
   `ProcessIoCatalogueLoader`; prove the `AI-003+` row is an adopted READ Input Register on the rack;
   read the setpoint, the optional band and the freshness budget, defaulting none of them;
3. construct `ProcessIoPhysicalTransport` and open `ProcessIoSession` (only here can anything reach the
   network);
4. `ProcessIoSession.ObserveAnalogInput("AI-003+", …)` exactly once, capturing raw, bar, quality,
   `ObservedAt`, `ReceivedAt`, freshness and generation;
5. read `AI-002+` once and report it as **`DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION`** with
   `ai002_used_for_decision=FALSE` — read so the exclusion is observed rather than asserted;
6. classify the acquisition first, then — and only if it passed — evaluate the existing
   `SequencingPressure.Valid` and `SequencingPressure.PumpReady` against the Owner-supplied setpoint;
7. print the evidence block of M5 with the two verdicts reported separately; close the session; dispose
   the transport; exit;
8. exit `0` (accepted), `2` (observed but not accepted), `3` (bounded read timeout or transport fault),
   `4` (refusal before acquisition).

**Two deliberate deviations from this document's own proposal, both narrowing:**

- **No `ProcessIoEquipmentBoundary`.** Its constructor requires a `ProcessIoWriteAuthorization`, and
  minting a write authorization on a read-only stage would be exactly the confusion this stage exists to
  avoid. The trial calls `ProcessIoSession.ObserveAnalogInput` and `SequencingPressure` directly, which
  are the same two authorities the boundary delegates to. `ReadPumpOutletPressure()` itself compounds
  the observation with the decision, which would have made a truthful per-field report impossible.
- **Exit codes are `0/2/3/4` as the Owner specified**, superseding the `0/3/4/2` ordering proposed here.
  The proposal folded "read good but not Pump-ready" and "stale" into one code; the Owner's contract
  separates a non-acceptance (`2`) from a bounded read failure (`3`).

**Invariants this function must keep:** zero `AdmitWrite`, zero `DispatchDigitalOutput`, zero
`WriteCoil`, no motion type referenced, no `SimulatorRuntime`, no Queue/`TimeDelay`, no Production path,
no persisted state, one process, one generation. `ProcessIoPhysicalTransport`, `ProcessIoSession`,
`ProcessIoEquipmentBoundary`, the canonical topology and the IV mapping are **unchanged**; Trial B is
untouched.

**Why it is narrow:** it adds a host, not a mechanism. Every capability it needs is already implemented
and already covered by existing tests; the new code reads one tag, calls one existing rule and prints
one record.

### 5.1 Three verdicts, two questions

M5's block carries three verification fields, and they must not be collapsed into one another:

| Field | Source | Question |
|---|---|---|
| `physical_ai003_verified` | `PumpReadyReadOutcome.PhysicalAi003Verified` | Did the physical `AI-003+` acquisition chain produce usable, current, in-generation evidence? |
| `pump_ready` | `PumpReadyReadOutcome.PumpReady` — the existing `SequencingPressure.PumpReady` verdict | Is that verified pressure strictly above the configured setpoint? |
| `physical_pump_verified` | the conjunction of the two | Is this a complete `PHYSICAL_PUMP_READY` result? |

`PhysicalAi003Verified` is set only after every acquisition requirement passes: the exact `AI-003+`
source tag, a `PHYSICAL_READ` provenance, a timestamp that is not future-dated, usable quality, the live
session generation, freshness at the moment of judgement, and a finite engineering value in bar. It
deliberately does **not** depend on the setpoint, because a correctly acquired 14 bar reading is genuine
physical evidence about the transmitter, the wiring, the rack input, the transport and the scaling — it
is simply not Pump-ready.

Consequently **physical AI-003 evidence may pass while Pump-ready fails**:

| Reading | `physical_ai003_verified` | `pump_ready` | `physical_pump_verified` |
|---|---|---|---|
| `29 bar`, GOOD, fresh, live generation, setpoint `15 bar` | `TRUE` | `TRUE` | `TRUE` |
| `15 bar`, GOOD, fresh, live generation, setpoint `15 bar` | `TRUE` | `FALSE` | `FALSE` |
| `11 bar`, GOOD, fresh, live generation, setpoint `15 bar` | `TRUE` | `FALSE` | `FALSE` |
| transport fault, non-physical read, wrong tag, future stamp, stale or unusable quality, superseded generation, or no value in bar | `FALSE` | `FALSE` | `FALSE` |

`PhysicalAi003Verified` is `FALSE` for every entry in the last row: absence of a verified acquisition is
never reported as evidence.

**Trial A requires `pump_ready=TRUE` for the complete `PHYSICAL_PUMP_READY` evidence verdict.** A
below-setpoint run is retained as useful physical `AI-003+` acquisition evidence — it is reported as
`physical_ai003_verified=TRUE` with `pump_ready=FALSE` — but it is **not** a passing Pump-ready run, and
it never becomes one by re-reading, reinterpretation or by combining it with `AI-002+`.

## 6. Trial A claim boundary (unchanged; the mechanism exists, the physical run does not)

**A passing Trial A (exit `0`, `pump_ready=TRUE`, `physical_pump_verified=TRUE`) would establish:**
physical Pump operation for that trial; physical `AI-003+` acquisition through the transmitter and the
WAGO input; the WJSS scaling and observation pipeline; quality/freshness/generation validation; and the
WJSS Pump-ready decision computed from `AI-003+` alone.

**A below-setpoint Trial A run establishes a strict subset:** `physical_ai003_verified=TRUE` — the
acquisition chain, the conversion and the freshness/generation rules — with `pump_ready=FALSE` and
`physical_pump_verified=FALSE`. It is recorded as acquisition evidence and is never presented as a
Pump-ready result.

**It would not establish:** IV command behaviour; Upper or Lower limit feedback; physical IV outlet
pressure; Galil motion; Cleaning Path execution; water-on cleaning; Queue; `TimeDelay`; Production
readiness.

**Trial A and Trial B are never combined into one physical claim.** Trial B remains the Pump-stopped,
simulated-`AI-003+`, physical-IV trial and carries no Pump evidence.

## 7. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED.** No endpoint was contacted, no Pump was started, no
valve was commanded and no motion occurred while this analysis was written. Every statement above is
derived from source inspection.

**PRODUCTION: NOT AUTHORIZED. MERGE: HOLD. FINAL READINESS REVIEW: NOT STARTED.**
