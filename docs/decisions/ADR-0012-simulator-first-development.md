# ADR-0012 — Simulator-First Development

- **Status:** ACCEPTED — recorded by the Owner at Stage 0.2 acceptance (merged through PR #2,
  merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`). Accepted as architecture
  direction; **accepted does not mean implemented**. Selections marked `[PROPOSED]`,
  `[OPEN]`, or `[NOT VERIFIED]` below keep those markers.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record refines the simulator statements in
  [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) section 3.3 and
  [`../ROADMAP.md`](../ROADMAP.md) capability 4 and records the governance rules required by
  the approved Stage 0.2 Scope Gate.
- **Scope:** Default operating mode, device profiles, simulator boundaries for Modbus, Galil,
  valves, pump, alarms, communication faults, and the cleaning sequence, failure injection,
  and the separation of test-hardware access from production access.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.8, together with the standing device-access prohibition in
  [`../../AGENTS.md`](../../AGENTS.md) section 6 and
  [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md).

---

## Context

No application code exists, no device has been contacted, and production write control is
prohibited until the bench verification in [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md)
section 4 is completed and recorded. The system therefore has to be developed, demonstrated,
and tested against something other than plant equipment for a significant period — possibly
indefinitely if bench verification is delayed.

A simulator in this project is not a convenience. It is the only way to exercise queue
arbitration, sequencing, command lifecycle, alarm behaviour, and failure handling before
hardware evidence exists. It also carries a specific risk: simulated success can be mistaken
for verified behaviour, and a simulator that does not implement the real application-facing
contracts produces false confidence.

## Decision

1. **Simulator is the default development mode.** A fresh development or test environment
   runs against simulator adapters unless a developer explicitly and knowingly selects
   another profile. No physical adapter is instantiated by default
   ([ADR-0010](ADR-0010-device-adapter-boundary.md) item 5).

2. **Device profiles are explicit and limited to three:**
   | Profile | Meaning | Authorization |
   | --- | --- | --- |
   | `SIMULATOR` | Simulated devices in the runtime service or the test harness. Default. | Permitted for development and testing |
   | `TEST_HARDWARE` | A dedicated, isolated bench setup, never the Boiler Unit installation. | Requires a separate Owner-approved Scope Gate; `[NOT AUTHORIZED]` today |
   | `PRODUCTION` | The installed Boiler Unit workstation and its real devices. | `[NOT AUTHORIZED]` for device access and for production writes |

3. **Physical adapters are disabled by default.** Enabling a physical adapter requires:
   - an approved future Scope Gate that names the activity,
   - an explicit local authorization recorded on the workstation,
   - the matching device profile selected and visible.
   A configuration edit alone is never sufficient authorization.

4. **No silent substitution, in either direction.**
   - A simulated profile must never be presented as plant data; the active profile is
     displayed in the UI chrome ([ADR-0006](ADR-0006-ui-delivery-model.md) item 8) and
     recorded at startup.
   - A physical profile whose adapter cannot be established must fail closed with an explicit
     fault/unknown state — it must never fall back to simulated values.

5. **Test-hardware access is separate from production access.** A `TEST_HARDWARE` profile
   targets an isolated bench arrangement. Nothing in the test profile is derived from, or
   connected to, the Boiler Unit installation, and production addresses, register maps, and
   motion values are never copied into test configuration.

6. **Simulator boundaries to be provided:**
   - **Modbus / WAGO boundary** — a Modbus TCP simulator implementing the approved function
     codes and the transport behaviours that matter: request/response, exception responses,
     latency, timeouts, disconnection, and reconnection.
   - **Galil motion boundary** — commanded motion, position knowledge, position loss,
     profile application, and motion faults, per axis.
   - **Valve and pump simulation** — limit-switch feedback patterns including
     `TRANSIT_OR_FAULT` and `INVALID_LIMIT_STATE`, energize-to-open behaviour, pump
     pressure development against a simulated pressure value, pressure rise timeout, and
     pressure loss during a job.
   - **Alarm and communication-fault simulation** — quality transitions, stale data,
     consecutive failures, poll-cycle overrun, and the blocking/non-blocking cases.
   - **Cleaning sequence simulation** — the full job path including valve open, pressure
     confirmation, motion, valve close, and outcome determination, so that sequencing,
     countdown, and queue refill can be exercised end to end.
7. **Failure injection is a first-class capability.** The simulator must be able to inject, on
   demand and repeatably: mid-job disconnection, stale data after reconnection, partial
   feedback, valve timeout during a job, pressure loss during a job, static-value false
   reassurance, exception responses, accumulating consecutive failures, position loss, and
   database unavailability. Injections must be scriptable and deterministic. How the DCS
   Permissive Override path is fault-injected remains `[OPEN]`
   ([`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) section 7).
8. **Determinism where practical.** The simulator is driven by the injectable clock adapter
   ([ADR-0010](ADR-0010-device-adapter-boundary.md) item 4), so dwell, interval, countdown,
   and timeout behaviour can be advanced deterministically in tests rather than by waiting on
   wall-clock time. Random behaviour, where used, must be seeded and reproducible.
9. **Contract parity is mandatory.** Simulator adapters implement the same application-facing
   contracts as physical adapters, and will be covered by contract tests that fail if the
   implementations diverge. A simulator that requires application changes to run is a defect,
   not a test harness.
10. **Simulation is not certification and is not verification of hardware behaviour.** No
    result obtained against a simulator may be reported as hardware verification, bench
    evidence, or fail-safe proof. Simulator results are evidence about application logic only.
    The WAGO watchdog remains `[NOT VERIFIED]` regardless of any simulated watchdog behavior.
11. **Nothing is implemented in this Stage.** This record defines the model and the
    governance. No simulator code, device configuration, or test hardware configuration is
    created.
12. **Relationship to the proposed Stage 0.2.1 technology spike.** The spike is a constrained
    evaluation, not a simulator and not an implementation of this record:
    - The spike runs on **synthetic data only** and must not require the simulator adapters,
      a device driver, or any device profile beyond a synthetic feed.
    - The spike must not access WAGO, Galil, Production SQL Server, or Production
      configuration, and it does not authorise such access.
    - Spike results are **UI and runtime performance evidence about the application**, not
      simulator fidelity evidence and not hardware evidence.
    - A spike result that looks favourable must not be reported as verification of any hardware
      behaviour, and it must not be used to relax the bench verification requirements in
      [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md).
    - Whether the spike's synthetic workload later becomes part of the simulator is `[OPEN]`
      and belongs to the simulator Stage Gate, not to the spike.
    See [`../MASTER_PLAN.md`](../MASTER_PLAN.md) section 3.2 for the proposed spike scope.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Develop directly against bench hardware | `[NOT AUTHORIZED]`, and it would couple all development to hardware availability; most logic would stay untested | Rejected |
| Develop against the production installation | Prohibited outright | Rejected |
| Simulate inside the domain instead of behind adapter contracts | Would bypass the real contract, hide integration faults, and make switching profiles an application change | Rejected |
| Use a general-purpose third-party Modbus simulator as the only simulator | Useful for transport checks, but it cannot model the valve, pump, motion, and sequencing behaviour this project needs, and it is not a testable component with injected faults | Rejected as the only mechanism; acceptable later as an external transport check |
| Recorded or replayed plant data as the development source | No recorded plant data exists, and using real plant data would risk prohibited content in the public repository | Rejected |
| Simulator with its own separate interfaces | Guarantees drift and produces false confidence | Rejected |
| Wall-clock waiting in tests | Slow and non-deterministic; dwell and interval tests would be flaky | Rejected |
| Allowing a PRODUCTION profile to fall back to simulation on adapter failure | Would present simulated values as plant state and could mask a loss of communication | Rejected |

## Consequences

- Development and test progress does not depend on hardware availability or on bench
  authorization, which is the only workable position while production writes are prohibited.
- The simulator becomes a maintained product artefact with its own tests; it is not throwaway
  scaffolding.
- Switching from simulated to physical operation is a profile change with no application code
  change — the intended property, and the reason contract parity matters.
- Because simulation proves application logic and nothing else, the documentation must keep
  pointing at the bench verification list for any hardware claim, and reviewers must keep the
  two apart.
- Deterministic simulation makes the planned queue, sequencing, and alarm test cases
  feasible without hardware.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Simulator fidelity lower than assumed | Real-world faults missed | Document simulator assumptions; keep the bench verification list authoritative; external transport simulators may supplement later | `[OPEN]` |
| Simulator results mistaken for hardware verification | False safety belief | Explicit statement in this record and in [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md); terminology discipline in every report | `[APPROVED]` rule |
| Profiles confused at runtime | Operator sees simulated data believing it is plant data | Profile displayed in the UI chrome and recorded at startup; audited administrative change | `[PROPOSED]` |
| Simulator and physical adapters drift | Works in simulation, fails on hardware | Contract tests over the shared ports | `[PROPOSED]` |
| Test-hardware work creeping into production configuration | Prohibited values in the repository or in the plant | Test-hardware configuration stays local and is never committed; separate profile | `[APPROVED]` rule |

## Verification status

- `[NOT VERIFIED]`: nothing here has been implemented. No simulator, adapter, harness, or
  test exists, and no simulation result has been produced.
- `[NOT VERIFIED]`: hardware behaviour of every kind. Simulation cannot and does not verify
  it.
- `[OPEN]`: application-level simulator fidelity requirements and who validates them;
  DCS-override fault injection; external transport-simulator usage.
- `[NOT AUTHORIZED]`: test-hardware access; production device access; production writes.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Simulator scope and fidelity requirements per boundary | Implementation Stage Gate (before simulator code) |
| Contract-test coverage proving simulator parity | Test Stage Gate (planned) |
| Failure-injection catalogue completeness, including the override path | Test Stage Gate |
| Test-hardware profile definition and its separate authorization | A separate, future Owner-approved Scope Gate |
| Any connection to physical equipment | A separate, future Owner-approved Scope Gate after bench verification |

## Relationship to protected decisions

- **Preserved, not modified:** the prohibition on production device access and on production
  valve and pump write control (HSB-002, AGENTS.md section 6), WAGO fail-safe remaining
  `[NOT VERIFIED]` (HSB-001), strictly sequential Cleaning Jobs and maximum one active
  Cleaning Job (SEQ-001 through SEQ-006), the DCS Permissive Override exclusion list
  (OVR-009, OVR-010), and the principal that unknown or bad-quality indication is never
  inferred as safe.
- **Implements approved mandatory decisions:** simulator mode is the default development
  mode; production adapters are disabled by default; enabling physical adapters requires an
  approved future Scope Gate; test-hardware access is separate from production device access;
  simulator behaviour must be deterministic where practical; simulation must not be presented
  as hardware certification.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Stage 0.3 remains unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this development model.

## References

- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned simulator-backed verification levels
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — bench verification requirements
- [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) — sequence and failure handling to be
  simulated
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — ARC and TST requirement groups
- [`ADR-0010-device-adapter-boundary.md`](ADR-0010-device-adapter-boundary.md)
- [`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
- [`../MASTER_PLAN.md`](../MASTER_PLAN.md) — proposed Stage 0.2.1 technology spike
