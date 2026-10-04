# ADR-0010 — Device Adapter Boundary

- **Status:** ACCEPTED — recorded by the Owner at Stage 0.2 acceptance (merged through PR #2,
  merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`). Accepted as architecture
  direction; **accepted does not mean implemented**. Selections marked `[PROPOSED]`,
  `[OPEN]`, or `[NOT VERIFIED]` below keep those markers.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record addresses the adapter questions left `[OPEN]` by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11 and
  [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) section 11.
- **Scope:** The boundary between domain logic and device integration for Modbus TCP, WAGO
  I/O, the Galil motion controllers, SQL Server, time sources, the simulator, and the
  loopback or in-process test adapter; the command-state vocabulary; and the rules that keep
  physical devices disabled by default.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, sections 8.5 and 8.8, together with the approved adapter responsibilities in
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 3.

---

## Context

The system must talk to a WAGO 750-362 Modbus TCP coupler (FC01, FC02, FC03, FC04, FC05,
FC06, FC15, FC16; zero-based internal addressing), four Galil DMC-B140-M controllers, a
pressure transmitter, valve limit switches, and a VFD analog output. Every one of those
integration paths is currently unverified, and production write control is prohibited until
bench verification is complete and recorded.

Vendor libraries and protocol stacks carry three distinct kinds of risk in this project:

1. **Licence risk** — the repository is public and the deployment is commercial; a library
   with unacceptable terms cannot ship.
2. **Availability risk** — the site is offline; a library must be acquirable and restorable
   without Internet access.
3. **Design risk** — a vendor library that leaks into domain logic makes the domain
   untestable without hardware and couples queue and sequencing behavior to a transport
   implementation.

The register map, tag naming, addressing granularity, and data-type conversion rules are
explicitly `[OPEN]` (`../REQUIREMENTS.md` COM-008), and no production register map, tag list,
coordinate, limit, or setpoint may be invented or committed.

## Decision

1. **The application owns the contracts.** Application-facing interfaces ("ports") are
   defined by the application in a contracts package, and adapters implement them. Domain
   logic depends on the ports, never on a vendor library, a socket, a driver, or a database
   client.

2. **Port shape for field I/O: configuration-driven tag access, not per-signal interfaces.**
   WAGO I/O is exposed through an application-owned I/O port addressed by **logical tag
   identity**, with the physical register map supplied by local configuration. The domain
   reasons about signals by identity and quality; it never reasons about register numbers or
   function codes. This keeps the open register-map question entirely in configuration and
   prevents a hardware address from appearing in domain code or tests.

3. **Port shape for motion: one motion port per controller responsibility**, exposing
   commands, state, and position knowledge per axis. Motion profiles are data selected by
   operation type (`MANUAL_JOG`, `MANUAL_MOVE`, `CLEANING_JOB`), not code.

4. **Required adapter set:**
   - Modbus TCP / WAGO I/O adapter (physical; disabled by default).
   - Galil motion adapter (physical; disabled by default).
   - SQL Server persistence adapter (per [ADR-0009](ADR-0009-database-access-and-migrations.md)).
   - Clock and time-source adapter — injectable, so interval and dwell behaviour is testable
     deterministically ([`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) section 4.2).
   - Simulator adapters — Modbus/WAGO simulation and motion simulation, implementing the
     **same application-facing contracts** as the physical adapters.
   - Loopback or in-process test adapter — deterministic, no network, used by unit and
     integration tests.
5. **Adapter selection is explicit, configuration-driven, and fail-closed.**
   - The default device profile is `SIMULATOR`
     ([ADR-0012](ADR-0012-simulator-first-development.md)).
   - Physical adapters are disabled by default and require an explicit local authorization
     plus an approved future Scope Gate.
   - **No silent substitution**: if a physical profile is selected and the physical adapter
     cannot be established, the runtime must publish an explicit fault/unknown state and
     block the actions that depend on the device. It must never fall back to simulated data
     while presenting itself as connected to plant equipment.
6. **Adapters contain no UI logic, no eligibility logic, and no safety logic.** An adapter
   transports, converts, validates at the protocol and data-type level, records transport
   evidence, and reports outcomes. It does not decide whether a sensor is eligible, whether a
   job may run, or whether an alarm should block. Adapters do not contain view models, page
   logic, or user-facing strings.
7. **Command-state vocabulary.** Every command that can reach a device must be traceable
   through a defined lifecycle, using these states where applicable:
   `REQUESTED`, `AUTHORIZED`, `QUEUED`, `ISSUED`, `ACCEPTED`, `EFFECTIVE`,
   `FEEDBACK_CONFIRMED`, and the terminal states `FAILED`, `TIMED_OUT`, `CANCELLED`,
   `ABORTED`.
   - The runtime service (not the UI, not the adapter) performs the
     requested → authorized transition, including permission, lifecycle, interlock,
     ownership, and command-state validation.
   - `ACCEPTED` means the device acknowledged the request; `EFFECTIVE` means the commanded
     change is observable in the process value or feedback; `FEEDBACK_CONFIRMED` means the
     derived state machine has confirmed the intended state. **How each transition is
     established for a given signal is `[NOT VERIFIED]` until bench evidence exists** — a
     Modbus write acknowledgement is not a valve position, and a valve limit switch is not a
     flow proof.
   - A command that is superseded, disconnected, or timed out must reach a terminal state
     explicitly. No command may sit in an indeterminate state indefinitely.
8. **Retry and reconnection discipline.**
   - Retries are bounded and configured locally; no unbounded retry loop exists.
   - Retries re-validate before re-issuing. A command that is not safely repeatable, or whose
     re-issue would constitute a new command, must not be replayed automatically.
   - **Reconnection must never re-issue a stale command** and must never re-energize an
     output. On reconnect, state is re-read and re-derived; commands are re-authorized.
   - Communication health is evaluated from transport evidence, never from value change
     ([`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 5).
9. **One owner per session.** Sessions exist only in the runtime service. No adapter is
   instantiated in the UI process, and no adapter may open a second session to the same
   device.
10. **Library selection remains `[OPEN]`.** Candidate libraries for the Modbus and Galil paths
    (open-source and commercial) must pass a licence, offline-availability, maintenance, and
    observability review before selection. Writing the required Modbus subset in-house is a
    documented fallback if no acceptable library exists. No device library is installed,
    referenced, or tested in this Stage.
11. **Production Device access remains `[NOT AUTHORIZED]`.** No adapter may be pointed at
    production equipment, and no test-hardware configuration may be created in this Stage.
12. **Modbus acquisition architecture (corrective requirement).** The legacy application polled
    approximately ten devices in a way that slowed the whole application. The following
    direction is required of the acquisition path, and it is an architecture requirement, not a
    library choice:
    - **One runtime-owned connection and one serialized command queue per Modbus device.**
      A session is never shared across devices.
    - **Different device pollers may operate concurrently under bounded scheduling.** The
      concurrency limit is configured locally; it is not unbounded.
    - **Requests to one device remain serialized** unless future device evidence permits
      otherwise.
    - **One slow or timed-out device must not block polling of unrelated devices.** Isolation
      between devices is the point of the per-device session and queue.
    - **Contiguous coil or register addresses are compiled into bounded batch reads** where the
      device and the function code support it, instead of one request per tag.
    - **Published Tag configuration compiles into a Poll Plan.** Compilation happens at
      configuration publication or runtime startup — **never from scratch every poll cycle**.
    - **Poll groups may use separate intervals: Fast, Medium, and Slow.** Critical one-second
      data belongs to the Fast group. Exact grouping and register spans remain Production
      configuration and are never committed.
    - Device limits, maximum quantities per request, address gaps, function codes, and
      byte/word order must be respected.
    - **The UI never performs Modbus polling** (see [ADR-0006](ADR-0006-ui-delivery-model.md)).
    - Whether a Poll Plan may be recompiled at runtime, and the batching limits per device, are
      `[OPEN]` and must be settled with device evidence.
13. **Library neutrality is preserved.** No Modbus library is selected or installed in this
    Stage. A previously used library (for example NModbus in the legacy application) may be
    evaluated later, but **prior slow behaviour must not be treated as proof that every
    architecture built on that library is slow**. The legacy latency is attributed to
    architecture — unbatched per-tag requests, unisolated devices, and shared work — not to a
    library's inherent speed.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Domain logic calls a vendor library directly | Fastest to start, and untestable without hardware, with vendor types diffusing through queue and sequencing code; a licence or version change would touch the domain | Rejected |
| One interface per physical signal (per valve, per limit switch, per axis) | Multiplies interfaces and makes configuration-driven mapping impossible while the register map is `[OPEN]`; every new point would require code | Rejected |
| Generic key/value tag bag with no typing | Flexible but untyped; validation and testability suffer, and typos become runtime faults | Rejected |
| Adapters own command validation | Would duplicate the single validation point and allow an adapter-level retry to bypass an interlock | Rejected |
| Symbolic "accepted" immediately on write, without feedback states | Overstates proof: a write acknowledgement is not a physical effect and would be indistinguishable from a confirmed state in the UI | Rejected |
| Automatic retry until success | Unbounded and dangerous; can re-energize or duplicate a motion command | Rejected |
| Replaying buffered commands after reconnection | Explicitly prohibited by the approved reliability principles | Rejected |
| Simulator implemented as a separate code path with its own interfaces | Simulator would stop being a proof of the application-facing contract and drift from the real path | Rejected |
| Selecting a Modbus or Galil library now | Licence and offline-availability evidence is unavailable in this Stage; selecting would invent certainty | Deferred — `[OPEN]` |
| One shared Modbus session or one global polling loop for all devices | A single slow or timed-out device would delay every other device — the legacy failure mode | Rejected — per-device session and serialized command queue required |
| One request per tag per cycle | Multiplies round trips and dominates poll time; cannot hold a one-second Fast group | Rejected — bounded batch reads of contiguous addresses |
| Compiling the poll list from scratch every cycle | Wastes the cycle budget on compilation and couples polling cost to configuration size | Rejected — Poll Plan compiled at publication or startup |
| Polling every device at the fastest interval | Saturates the equipment network and starves critical signals | Rejected — Fast, Medium, and Slow poll groups |
| Attributing the legacy slowness to NModbus itself | The evidence does not support it; the same library inside a batched, isolated, concurrent architecture may perform adequately | Rejected as a conclusion |

## Consequences

- Domain and queue logic stay pure and hardware-free, which is what makes the planned
  exhaustive unit coverage of queue, sequencing, and alarm behavior possible.
- The register map and motion values remain configuration, which matches the open state of
  COM-008 and the prohibition on inventing motion values.
- The simulator becomes a first-class implementation of the same contracts, so switching
  profiles cannot change application behavior — only the data source.
- The command-state model adds explicit states that must be implemented, tested, and mapped
  to real feedback evidence. Until bench evidence exists, several transitions cannot be
  claimed to work, only to be designed.
- Library selection remains a real, unresolved dependency with licence consequences; it is
  visible rather than assumed.
- Acquisition cost is bounded by configuration compilation plus batched reads rather than by
  tag count, so a larger tag list does not automatically lengthen the poll cycle.
- Device isolation means one misbehaving coupler degrades only its own poll group; its data
  quality goes bad while unrelated devices keep updating.
- Poll groups require a grouping decision in local Production configuration, which is
  deliberately not made in the public repository.
- Because the Poll Plan is compiled at publication or startup, a configuration publication has
  a real acquisition-side cost that must be measurable and bounded.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| No acceptable licensed Modbus library for the required function-code subset | Rework of the physical I/O path | In-house implementation of the required subset is a pre-identified fallback; the port isolates the impact to one adapter | `[PROPOSED]` |
| Galil integration mechanism unsuitable from .NET on Windows 11 | Motion path rework | Evaluate vendor-provided interfaces, a vendor library, and a direct command transport at the implementation gate before motion code is written | `[OPEN]` |
| Command-state transitions mapped to insufficient evidence | False confidence in "confirmed" states | Require bench evidence before any production write path is enabled; document the evidence for each transition | `[NOT VERIFIED]` |
| Adapter retry logic bypassing an interlock | Equipment commanded without authorization | Retries are re-validated by the runtime service, not the adapter; adapter retries are transport-level only | `[PROPOSED]` |
| Simulator and physical adapter drifting apart | Simulated success, physical failure | Shared contracts with contract tests; simulator cannot be built against different interfaces | `[PROPOSED]` |
| Clock adapter not used consistently | Non-deterministic dwell and interval behaviour | Injectable clock is a testability requirement; direct system-time reads are prohibited in domain code | `[PROPOSED]` |
| Bounded concurrency set too high for the equipment network | Saturation, timeouts, and cascading bad quality | Local configuration plus measurement during implementation; concurrency is bounded by design | `[OPEN]` |
| Batch reads cross a gap or exceed a device limit | Request rejected, or silently wrong data | Poll Plan compilation must respect maximum quantities, gaps, and function-code limits; malformed plans are refused at publication | `[PROPOSED]` |
| Poll Plan recompiled per cycle by accident | Acquisition cost scales with configuration, reintroducing the legacy symptom | Compilation only at publication or startup; a planned verification case covers it | `[PROPOSED]` |
| Slow device starves others despite isolation | One bad device degrades the whole cycle | Per-device session and queue, bounded scheduling, per-device health metrics | `[PROPOSED]` |

## Verification status

- `[NOT VERIFIED]`: every device interaction. No WAGO coupler, Galil controller, pump, valve,
  DCS signal, or any device library has been contacted, installed, or tested.
- `[NOT VERIFIED]`: register map, tag naming, addressing granularity, and conversion rules;
  motion values, limits, and profiles; how `ACCEPTED`, `EFFECTIVE`, and `FEEDBACK_CONFIRMED`
  are proven for each signal.
- `[NOT VERIFIED]`: WAGO watchdog behaviour and every bench-verification item in
  [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) section 4.
- `[OPEN]`: Modbus library, Galil integration mechanism, retry and timeout values (local
  configuration), poll-group interval values (local configuration), batching limits,
  concurrency bound, whether a Poll Plan may be recompiled at runtime, and the in-house
  fallback decision.
- `[NOT VERIFIED]`: the acquisition performance of any Poll Plan design. No poller exists, no
  device has been polled, and no cycle time has been measured. The legacy evidence justifies the
  architecture direction; it does not measure the new one.
- `[NOT AUTHORIZED]`: production device access and production valve and pump write control.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Modbus library selection with licence and offline-availability evidence | Implementation Stage Gate |
| Galil integration mechanism selection | Implementation Stage Gate, before motion code |
| In-house Modbus subset fallback decision | Implementation Stage Gate, if no library passes review |
| Command-state evidence model per signal | Implementation Stage Gate, then bench verification |
| Simulator contract tests proving parity with physical adapters | Test Stage Gate (planned) |
| Retry, timeout, and reconnection policy values | Implementation Stage Gate (local configuration only) |
| Poll Plan compilation, batching limits, poll-group intervals, and bounded concurrency | Implementation Stage Gate, with measurement against a simulator and then the bench |
| Confirmation that a large tag list does not lengthen the poll cycle beyond the Fast-group budget | Test Stage Gate (planned performance verification) |
| Enabling any physical adapter | A separate, future Owner-approved Scope Gate, after bench verification |

## Relationship to protected decisions

- **Preserved, not modified:** one Water Jet to one dedicated Isolation Valve
  (WJV-001 through WJV-005), sensor-to-valve derivation from the assigned Water Jet (WJV-004),
  strictly sequential Cleaning Jobs and maximum one active Cleaning Job (SEQ-001 through
  SEQ-006), the Main Pump remaining permitted to run between sequential jobs (SEQ-008), the
  approved fault responses (VLV-004, VLV-006), the Main Pump stop rule (PMP-007), motion-mode
  prohibitions (GAL-005), and the DCS Permissive Override exclusion list (OVR-009).
- **Implements approved mandatory principles:** domain logic must not depend directly on a
  vendor library; device libraries are isolated behind interfaces; simulator adapters
  implement the same application-facing contract; physical-device selection is explicit and
  disabled by default; commands distinguish requested, authorized, queued, issued, accepted,
  effective, feedback-confirmed, failed, timed-out, cancelled, and aborted states.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Production Device access
  `[NOT AUTHORIZED]`; WAGO fail-safe `[NOT VERIFIED]`; Stage 0.3 remains unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this adapter design.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — adapter strategy and command lifecycle
  (section 16), Modbus acquisition architecture (section 29)
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — bench verification and target safe states
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — COM, COMH, GAL, VLV, PMP, and ARC groups
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned integration levels
- [`ADR-0012-simulator-first-development.md`](ADR-0012-simulator-first-development.md)
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
