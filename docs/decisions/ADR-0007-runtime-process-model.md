# ADR-0007 — Runtime Process Model

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record closes the process-architecture question left `[OPEN]`
  by [ADR-0002](ADR-0002-deployment-architecture.md) item 1 and by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11.
- **Scope:** Which processes exist on the workstation, which process owns device sessions,
  what the Local Application API boundary is, how the Equipment Runtime is separated from the
  UI, and how the single-active-Cleaning-Job invariant is enforced by process ownership.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, sections 8.1, 8.3, and 8.4, together with the approved subsystem boundaries in
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 4.

---

## Context

The application supervises real equipment: a Main Pump, eight Isolation Valves, four motion
controllers, and 208 thermocouple channels. It must also run a Kiosk operator interface that
operators close, restart, and occasionally crash, and it must keep working when SQL Server is
unavailable.

Three structural forces drive the process model:

1. **Equipment Runtime separation from the UI.** Closing, restarting, or replacing the
   presentation layer must not by itself determine equipment state. This is an approved
   architectural direction and an approved mandatory boundary.
2. **A single enforcement point for the sequential-execution invariant.** At most one
   Cleaning Job may be ACTIVE at any time. If two processes could issue control actions, the
   invariant would have to be enforced by coordination between them — an invitation to a
   concurrency defect the plant is not authorised to tolerate.
3. **Fail-closed command boundaries.** Hardware commands must pass through authorization,
   lifecycle, interlock, ownership, and command-state validation before dispatch. Those
   checks must live in one place, next to the device sessions, not in the UI.

The workstation is a single machine that also hosts SQL Server. Adding processes has real
cost: more deployment units, more IPC surfaces, more failure modes to document and test. The
preference is therefore the least complex process model that still satisfies isolation.

## Decision

1. **Two application processes, plus SQL Server.** The workstation runs:
   - **Kiosk shell process** — the interactive-session presentation process described in
     [ADR-0006](ADR-0006-ui-delivery-model.md).
   - **Runtime service process** — one Windows Service that hosts the Equipment Runtime, the
     Local Application API, the queue engine, the cleaning orchestrator, the alarm engine, the
     historian writer, the configuration publisher, the device adapters, and the
     simulator-backed adapters when simulated operation is selected.
   - **SQL Server 2025 Standard** — approved baseline, separate service, unchanged.

2. **The runtime service is the only process that may own a physical device session.** No
   other process may open a Modbus TCP connection to the WAGO coupler or any Galil connection.
   The simulator adapters likewise live only in the runtime service (or in test harnesses),
   never in the UI.

3. **The runtime service is the only process that may write to hardware** and the only
   process that may issue supervisory commands. The UI cannot reach a device by any path.

4. **The runtime service is the only process that connects to the database.** The UI reads
   and writes exclusively through the Local Application API. This removes database credentials
   from the presentation process and keeps retention, migration, and transaction policy in one
   place.

5. **Local Application API.** A local, authenticated, loopback-only API is the UI's only
   channel to the application. Its boundary is:
   - hosted by the runtime service (in-process, baseline);
   - bound to the IPv4 loopback interface only — no interface that is reachable from the
     equipment network or any other host;
   - authenticated with the application's own local user session; no anonymous or
     unauthenticated control endpoint exists;
   - contract-defined in a shared contracts package so that the transport can be replaced
     without changing the contract;
   - transport-agnostic in contract, with loopback HTTP as the baseline transport. A
     named-pipe transport is recorded as the hardening alternative because it removes a
     listening TCP port and carries operating-system caller identity.
6. **Single instance enforcement.** Only one runtime service instance may exist on a
   workstation. A second instance must refuse to start, because two instances would break the
   one-active-Cleaning-Job invariant and duplicate device sessions.

7. **The one-active-job gate lives in the runtime service** and is enforced at the approved
   enforcement point (Cleaning Job step 2 revalidation) in addition to any earlier gate. No
   API request, queue action, operator action, or adapter retry may create a second active
   Cleaning Job.

8. **Service lifecycle.** The runtime service:
   - starts automatically, independent of any interactive sign-in;
   - on start enters an explicit startup state and must not issue any command until device
     state, position knowledge, and permissives have been re-established;
   - must not replay, resume, or re-issue a command that was in flight before a restart;
   - uses a bounded, configured service-recovery policy; recovery never authorises a command
     and never auto-resumes an AutoSequence or a Cleaning Job;
   - runs under a dedicated local service identity with least privilege. The exact identity
     and its database authorization are `[OPEN]`.

9. **UI restart behaviour.** If the kiosk shell closes, crashes, or is restarted:
   - the runtime service continues to supervise the process and, if a job is active, the job
     continues;
   - the shell reconnects and re-reads authoritative state from the API; it holds no
     authoritative state;
   - any manual hold-to-run operation must stop when the operator interface session ends;
   - commands in flight at the moment the session ends are not resumed by the UI.

10. **Composition root.** The runtime service is the single composition root for adapters,
    persistence, and application services. Dependency injection and configuration frameworks
    are recorded in [ADR-0008](ADR-0008-technology-stack.md).

11. **Explicitly not decided.** Whether the Local Application API, the historian writer, or
    the simulator host is later split into separate service processes. The trigger for
    revisiting is evidence, not preference: a measured condition where database or simulator
    behaviour degrades control determinism. Until such evidence exists, one runtime service is
    the least complex architecture that satisfies the mandatory boundaries.

### Live-state delivery and performance isolation (punchlist refinement)

The legacy application evidence in [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 23 shows
that when acquisition, database access, rendering, and graph updates share one execution
context, they block one another and the operator notices. The following boundaries are required
of the process model:

1. **The runtime maintains the authoritative in-memory operational state.** Acquisition writes
   into it; queue, alarm, sequencing, motion, and Cleaning Job logic read from it. It is the
   single answer to "what is the state of the plant right now".
2. **Operational parameters come from an in-memory Published Configuration Snapshot**, not from
   the database per cycle ([ADR-0009](ADR-0009-database-access-and-migrations.md) item 11,
   [ADR-0011](ADR-0011-configuration-and-secrets.md)).
3. **The UI holds presentation state only.** It is a consumer of the runtime's state, never a
   second authority, and never a source that the runtime reads back from.
4. **The runtime publishes an application-facing snapshot or delta contract**, delivered to the
   UI over a loopback push channel; the UI applies **partial component updates**. The UI does
   not poll each signal individually and does not query SQL for live operational state.
5. **Published content** must include, at minimum: a full bootstrap snapshot on initial
   connection; a monotonic sequence or revision; a timestamp; changed Sensor presentation
   states; the active Cleaning Job; Main Pump state; queue summary; alarm summary;
   communication health; and the published configuration revision.
6. **The exact push transport remains `[OPEN]`.** Candidates may include an ASP.NET Core push
   mechanism such as WebSocket-based delivery. No final push library is selected in Stage 0.2.
   The contract must not be coupled to the transport.
7. **Reconnect principles:** the UI reconnecting requests or receives a new authoritative
   snapshot; the UI does not instruct devices to recover; the UI does not replay commands; the
   UI does not infer Cleaning Job continuation from stale local state; the runtime remains
   authoritative.
8. **I/O, database access, Historian writes, graph updates, and UI rendering must not block one
   another.** Acquisition, queue evaluation, and alarm evaluation run independently of database
   write latency ([ADR-0009](ADR-0009-database-access-and-migrations.md) item 6); UI rendering
   runs in a different process from equipment supervision.
9. **Per-device acquisition isolation** is required: one runtime-owned connection and
   serialized command queue per Modbus device, bounded concurrent pollers, and no cross-device
   blocking ([ADR-0010](ADR-0010-device-adapter-boundary.md) item 12).
10. **A slow database write must not block a valid Main Pump stop request** (PMP-007).

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Three or more services: Equipment Runtime service, separate Local API service, separate Historian Writer service | Adds deployment units, IPC surfaces, and failure modes (partial startup, version skew between services, distributed command-state problems). The only benefit — isolating query or write load from the control path — is achievable in-process with a decoupled writer queue and has not been shown to be necessary at this scale. | Rejected for the baseline; trigger conditions recorded |
| Single process containing UI and Equipment Runtime | Fails the approved mandatory boundary and destroys failure isolation: a UI crash or UI workload would own device sessions. | Rejected |
| UI process hosts the API and the Equipment Runtime communicates with the UI process | Inverts the ownership rule and makes equipment state depend on the presentation process. | Rejected |
| One process per device adapter (per controller, per coupler) | Multiplies processes without a demonstrated isolation benefit; adapters are supervised I/O paths inside a service that already supervises them, and per-process command correlation would add complexity. | Rejected |
| Console application / scheduled task instead of a Windows Service | No session independence, no service recovery, no automatic start; a workstation reboot would leave the environment unsupervised. | Rejected |
| Containerisation or service isolation through a container runtime | Not appropriate for an offline Windows 11 Pro workstation that must reach plant hardware and provide native Windows services. | Rejected |
| Named pipes as the baseline API transport | Strong hardening property (no listening port, OS caller identity) but weaker browser-based development and test ergonomics. Recorded as the hardening alternative; the contract is transport-agnostic so the transport can change without a contract change. | Not selected — recorded alternative |
| UI polling the runtime once per signal every cycle | Recreates the legacy failure mode at the API boundary: request volume and UI work scale with signal count | Rejected — bootstrap snapshot plus changed-state delta push |
| UI querying the database for live operational state | Adds a second data path, defeats the runtime's authority, and reintroduces database latency into the operator's view | Rejected |
| Runtime reading operational parameters from SQL every cycle | Same latency coupling, now inside the control path | Rejected — in-memory Published Configuration Snapshot |
| Selecting a push transport (for example a specific WebSocket library) in this Stage | No spike evidence exists; the contract can be defined independently of the transport | Deferred — `[OPEN]` |
| Letting the UI infer Cleaning Job continuation from its own cached state after a reconnect | The UI holds no authority and cannot know job state; inference would display a false plant state | Rejected — the runtime remains authoritative; reconnect fetches a new snapshot |

## Consequences

- Device sessions, command validation, queue state, and the one-active-job gate have exactly
  one owner. There is no coordination protocol to get wrong.
- The UI can be closed, restarted, or replaced without touching equipment state. It also
  cannot stop equipment: it can only submit requests, and if the runtime service is down, no
  request can be issued at all.
- A runtime service restart is a genuine, operator-visible event: sessions are rebuilt,
  position knowledge may become unknown, and an interrupted Cleaning Job cannot resume
  silently.
- Database credentials and migration authority stay in one process.
- Because the API is hosted in-process, "Local API restart" and "Equipment Runtime restart"
  are the same failure event in the baseline. This is documented rather than hidden.
- The loopback API is a local attack surface: any process on the workstation can attempt to
  reach it. Session authentication is therefore mandatory, not optional.
- Service supervision, single-instance enforcement, and startup resynchronization must be
  implemented and tested before any control path is enabled. None exists today.
- Authoritative in-memory state means the runtime, not the database, is the reference for live
  behaviour. Restarting the service therefore loses no persisted truth but does lose derived
  live state, which must be re-established before any command is issued.
- Push delivery replaces per-signal querying, so the UI's cost scales with the **number of
  changed cells**, not with the number of signals. That is the property that keeps a 104-cell
  page inside the one-second target.
- The presentation-state contract becomes a first-class interface: it must define snapshot,
  delta, sequence, timestamp, and reconnect semantics, and it must be transport-agnostic.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Runtime service restarts while outputs are energized | Unverified output behaviour | Hardware safe-state behaviour is a bench verification item; the application must not be relied upon | `[NOT VERIFIED]` |
| Loopback API reachable by an unrelated local process | Unauthorised control attempt | Mandatory session authentication; loopback-only binding; named-pipe alternative recorded | `[PROPOSED]` |
| UI crash during an active job leaves the operator without visibility | Delayed operator response | Visibility alarm and shell supervision; runtime continues | `[PROPOSED]` |
| Second runtime instance started manually | Duplicate sessions and a broken sequencing invariant | Single-instance refusal to start | `[PROPOSED]` |
| Service recovery loops re-starting an environment that is in an unknown position | Uncontrolled motion | Startup state blocks all commands until resynchronization | `[PROPOSED]` |
| In-process historian writer competing with the control loop | Non-deterministic control timing | Decoupled bounded writer queue and write-path priority; measurement during implementation | `[PROPOSED]` |
| Push channel drops or falls behind, leaving the UI showing stale state | Operator acts on an outdated view | Monotonic sequence or revision plus timestamp on every message; the UI surfaces staleness rather than hiding it; reconnect requests a fresh snapshot | `[PROPOSED]` |
| Presentation-state payload grows with signal count | Update latency rises; one-second target missed | Publish changed states only; bounded payload; payload size is an explicit spike measurement | `[PROPOSED]` |
| Delta applied out of order or duplicated | UI shows an impossible state | Sequence or revision ordering enforced by the contract; out-of-order deltas trigger a resynchronisation | `[PROPOSED]` |

## Verification status

- `[NOT VERIFIED]`: every runtime property of this decision. No service, no API, no adapter,
  and no shell exists. Nothing has been started, stopped, restarted, or observed.
- `[NOT VERIFIED]`: hardware behaviour when the Equipment Runtime terminates — an explicit
  bench verification item.
- `[NOT VERIFIED]`: the effect of an in-process historian writer on control-loop timing; no
  measurement exists.
- `[OPEN]`: service identity and its database authorization; whether additional service
  processes are ever required, and the evidence that would justify them; the push transport;
  the presentation-state payload encoding; and how snapshot versus delta recovery is triggered.
- `[NOT VERIFIED]`: end-to-end update latency and payload size for the 104-cell one-second
  workload. No runtime, API, or UI exists, so nothing has been measured.
- `[NOT AUTHORIZED]`: production device access; production valve and pump write control.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Service identity, privileges, and database authorization | Implementation Stage Gate |
| Startup resynchronization sequence and its operator-visible state | Implementation Stage Gate |
| Service recovery policy details (counts, delays, alerting) | Implementation Stage Gate and Deployment Stage Gate |
| Loopback HTTP versus named-pipe transport decision | Security review before deployment, or an implementation Stage Gate if evidence changes |
| Splitting the API or historian writer into separate services | Only if measured evidence requires it; a new ADR would be required |
| Restart, recovery, and resynchronization verification | Test Stage Gate — cases planned, not executed |
| Push transport selection and presentation-state payload design | Stage 0.2.1 spike, then Implementation Stage Gate |
| Live update latency and payload measurement for the 104-cell workload | Stage 0.2.1 spike |

## Relationship to protected decisions

- **Preserved, not modified:** one installation per Boiler Unit (IDN-004), strictly sequential
  Cleaning Jobs and maximum one active Cleaning Job (SEQ-001 through SEQ-006), the Main Pump
  remaining allowed to run between sequential jobs (SEQ-008), the Operations UI close guard
  and its non-safety status (UIG-001 through UIG-006), and the prohibition on the override
  bypassing lifecycle gates (OVR-009).
- **Implements approved mandatory boundaries:** the UI does not write to hardware and owns no
  device session; only the approved Equipment Runtime boundary owns physical device sessions;
  device adapters contain no UI logic; hardware commands pass through authorization,
  lifecycle, interlock, ownership, and command-state validation; no architecture choice may
  permit concurrent Cleaning Jobs.
- **Unchanged:** Production Write is `[NOT AUTHORIZED]`; Production Device access is
  `[NOT AUTHORIZED]`; WAGO fail-safe remains `[NOT VERIFIED]`; Stage 0.3 remains
  unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this process model.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — process and ownership model (section 14),
  failure isolation matrix (section 15), live-state delivery (section 28), and Modbus
  acquisition isolation (section 29)
- [`../CONTROL_AUTHORITY.md`](../CONTROL_AUTHORITY.md) — command authority and UI boundary
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — bench verification and target safe states
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — SEQ, UIG, HSB, and ARC requirement groups
- [`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md)
- [`ADR-0008-technology-stack.md`](ADR-0008-technology-stack.md)
- [`ADR-0010-device-adapter-boundary.md`](ADR-0010-device-adapter-boundary.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
