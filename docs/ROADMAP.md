# Roadmap — WaterJet Sentinel Suite (WJSS)

**Document status:** [PROPOSED] — a forward view. Nothing in this document is approved
work. Only an approved Stage Gate authorises delivery.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**COMPLETE FOR DEVELOPMENT CHECKPOINT** (Owner-local final Edge gate **PASS**, Owner manual
review **PASS**), PR #3 **OPEN — READY FOR OWNER MERGE**, **NOT MERGED**. **React selected as the
Primary UI Framework** (Owner decision, 2026-10-07; Production transport and chart library remain
`[OPEN]`). Blazor counter-spike **NOT REQUIRED** unless a future material blocker is identified.
Main Development Scope Gate **PENDING**. Stage 0.3 `[NOT AUTHORIZED]`. Production device access `[NOT AUTHORIZED]`. This roadmap authorises
nothing.

This roadmap describes *intended capability sequence*. It is not a schedule, not a
commitment, and not an authorisation. The authoritative description of how stages are
gated is in [`MASTER_PLAN.md`](MASTER_PLAN.md).

---

## 1. Guiding priorities

The following priorities are `[APPROVED]` and constrain every future stage, in this order:

1. Reliability
2. Stability
3. Deterministic behaviour
4. Recoverability
5. Operator usability
6. Maintainability
7. Appropriate baseline security

Feature breadth is not a priority. A capability that cannot be made deterministic and
recoverable is not delivered.

## 2. Capability sequence

Each capability below is `[PROPOSED]` unless marked otherwise. Capabilities list the
preconditions they depend on, because several depend on bench evidence that does not yet
exist.

| # | Capability | Preconditions | Status |
| --- | --- | --- | --- |
| 1 | Documentation and governance foundation | Approved Stage 0.1 gate | Gate `[APPROVED]`; implementation **MERGED** to `main` through PR #1 |
| 2 | Owner-ratified requirement and configuration model | Resolution of the remaining open items in [`CURRENT_STATE.md`](CURRENT_STATE.md) | `[PROPOSED]` |
| 3 | Offline domain logic: sensor model, DirtyScore, queue arbitration, sequencing gate | Testable specification of the queue, sequencing, and cleaning rules | `[PROPOSED]` |
| 4 | Simulator-backed Modbus TCP acquisition (read-only) | Simulator definitions and dummy configuration only | `[PROPOSED]` |
| 5 | Temperature monitoring and trends | Capability 4 | `[PROPOSED]` |
| 6 | Communication-health evaluation and stale-data handling | Capability 4; stale timeout configured locally, never invented | `[PROPOSED]` |
| 7 | Temperature and time-based queue arbitration in operation | Capability 3 | `[PROPOSED]` |
| 8 | Historian storage and retention mechanics | Test database; retention values ratified; capacity model complete | `[PROPOSED]` |
| 9 | Alarm, event, and audit subsystems | Retention and permission models ratified | `[PROPOSED]` |
| 10 | Operator interface and Control Room Kiosk, including the close guard | Real operator workflow review | `[PROPOSED]` |
| 11 | Supervisory control path against simulator | Capabilities 4 and 7 | `[PROPOSED]` |
| 12 | Galil motion integration against simulator | Motion profile specification ratified | `[PROPOSED]` |
| 13 | DCS Permissive Override against simulator | DCS permissive definition set captured locally | `[PROPOSED]` |
| 14 | Bench verification of fail-safe output behaviour | **Explicit separate Owner authorisation**, physical supervision, and written method statement | `[NOT AUTHORIZED]` |
| 15 | Production supervisory control of valve and pump outputs | Successful, recorded bench verification | `[NOT AUTHORIZED]` |
| 16 | DCS hardwired signal monitoring through WAGO I/O | Signal specification ratified; read-only verification first | `[PROPOSED]` |
| 17 | Engineering configuration publication workflow | Configuration validation rules ratified | `[PROPOSED]` |
| 18 | Reporting and export | Retention and export lifecycle ratified | `[PROPOSED]` |
| 19 | Diagnostics and recovery tooling | Failure taxonomy ratified | `[PROPOSED]` |
| 20 | Multi-unit or fleet-level capability | Not planned. One installation controls one Boiler Unit. | `[OPEN]` |

**Technology and solution architecture decisions are not a capability.** Stage 0.2 recorded
architecture decisions — UI delivery model, runtime process model, technology stack, database
access and migrations, device adapter boundary, configuration and secrets, simulator-first
development, and offline deployment. They are indexed in
[`decisions/README.md`](decisions/README.md); the Owner has recorded them `ACCEPTED` as
architecture direction, which does not mean implemented. No capability in the table above is authorised, started, or brought forward
by those decisions, and Stage 0.3 remains `[NOT AUTHORIZED]`.

Stage 0.2.1 — UI and Runtime Technology Spike — is **SUPERSEDED** (the dual-candidate proposal is
not performed; a Blazor counter-spike requires a future Owner Scope Gate). It was a
constrained evaluation, not an implementation Stage, and it is recorded in
[`MASTER_PLAN.md`](MASTER_PLAN.md) section 3.2. It is not a capability in the sequence above,
it is not started, and it does not bring any capability forward.

**Update:** the Owner approved the narrower Stage 0.2.1A synthetic React feasibility spike,
now in progress (see [`spikes/stage-0.2.1a-plan.md`](spikes/stage-0.2.1a-plan.md)). It is not a
capability in the sequence above, it selects no framework, and it brings no capability forward.

## 3. Milestones that require Owner evidence

Three milestones cannot be reached by implementation effort alone. They require physical
evidence that only the Owner can authorise and witness:

1. **Bench verification of the hardware safety boundary.** The list of required
   verifications is fixed in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md). Until it is
   completed and recorded, production write control stays prohibited.
2. **Commissioning value capture.** Travel limits, pulses per engineering unit, homing
   behaviour, speeds, accelerations, decelerations, pressures, timeouts, DCS permissive
   definitions, and the stale-data timeout are field values. They are `[NOT VERIFIED]` and
   must be captured from engineering records, not inferred.
3. **Deployment acceptance on a Boiler Unit.** Requires Owner acceptance criteria, which
   are not yet published. `[OPEN]`

## 4. Explicit non-goals

The following remain out of scope unless a future gate states otherwise:

- Becoming a Safety Instrumented System or any safety-rated function.
- Replacing emergency stop circuits, hardwired protection, motor protection, mechanical
  limits, or controller-side safe-stop behaviour.
- Certification to IEC, ISA, ISO, or any other standard.
- Internet-dependent operation. The initial system is standalone.
- Multi-unit orchestration from a single installation.
- **Concurrent or parallel Water Jet cleaning.** This is prohibited, not deferred.
- Storing production configuration, tag lists, register maps, or credentials in this
  public repository.

## 5. Sequencing risks

| Risk | Effect | Mitigation approach `[PROPOSED]` |
| --- | --- | --- |
| Bench verification delayed | Supervisory control cannot be enabled in production | Keep the control path exercised against a simulator; keep write control prohibited |
| Unratified commissioning values | Motion and pressure behaviour cannot be finalised | Keep such values external and configuration-driven; never hard-code |
| Open specification items unresolved | Implementation would guess behaviour | Resolve items in [`CURRENT_STATE.md`](CURRENT_STATE.md) before implementing the affected behaviour |
| Scope growth | Determinism and recoverability suffer | Stage gates; owner-only merge; priorities list applied in order |
| Unmodelled storage growth | Historian could exhaust local storage | Produce a capacity model before committing to retention defaults; keep retention configurable per category |
| Communication health judged by value change | A static value could mask a lost link | Evaluate health from transport evidence, never from value change alone |
| UI framework chosen without measurement | A dense 106-cell page with one-second updates could repeat the legacy lag, freeze, and CPU problems | Framework remains `[OPEN]`; selection only through the proposed Stage 0.2.1 spike with measured acceptance criteria |
| Acquisition path not batched and not isolated per device | One slow coupler delays unrelated devices, lengthening the whole cycle | Per-device serialized command queue, bounded concurrent pollers, compiled Poll Plan, Fast/Medium/Slow groups |

---

## Related documents

- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage ledger and gate rules
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — stage status, verified state, open items
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — communication health model
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — bench verification requirements
