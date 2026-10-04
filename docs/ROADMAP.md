# Roadmap — WaterJet Sentinel Suite (WJSS)

**Document status:** [PROPOSED] — a forward view. Nothing in this document is approved
work. Only an approved Stage Gate authorises delivery.

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
| 1 | Documentation and governance baseline | Approved Stage 0.1 gate | `[APPROVED]` — delivered |
| 2 | Owner-ratified requirement and configuration model | Resolution of open items in [`CURRENT_STATE.md`](CURRENT_STATE.md) | `[PROPOSED]` |
| 3 | Offline domain logic: sensor model, DirtyScore, queue arbitration | Testable specification of the queue and cleaning rules | `[PROPOSED]` |
| 4 | Simulator-backed Modbus TCP acquisition (read-only) | Simulator definitions and dummy configuration only | `[PROPOSED]` |
| 5 | Temperature monitoring and trends | Capability 4 | `[PROPOSED]` |
| 6 | Temperature and time-based queue arbitration in operation | Capability 3 | `[PROPOSED]` |
| 7 | Historian storage and retention mechanics | Test database; retention values ratified | `[PROPOSED]` |
| 8 | Alarm, event, and audit subsystems | Retention and permission models ratified | `[PROPOSED]` |
| 9 | Operator interface and Control Room Kiosk | Real operator workflow review | `[PROPOSED]` |
| 10 | Supervisory control path against simulator | Capabilities 4 and 6 | `[PROPOSED]` |
| 11 | Galil motion integration against simulator | Motion profile specification ratified | `[PROPOSED]` |
| 12 | Bench verification of fail-safe output behaviour | **Explicit separate Owner authorisation**, physical supervision, and written method statement | `[NOT AUTHORIZED]` |
| 13 | Production supervisory control of valve and pump outputs | Successful, recorded bench verification | `[NOT AUTHORIZED]` |
| 14 | DCS hardwired signal monitoring through WAGO I/O | Signal specification ratified; read-only verification first | `[PROPOSED]` |
| 15 | Engineering configuration publication workflow | Configuration validation rules ratified | `[PROPOSED]` |
| 16 | Reporting and export | Retention and export lifecycle ratified | `[PROPOSED]` |
| 17 | Diagnostics and recovery tooling | Failure taxonomy ratified | `[PROPOSED]` |
| 18 | Multi-unit or fleet-level capability | Not planned. One installation controls one Boiler Unit. | `[OPEN]` |

## 3. Milestones that require Owner evidence

Three milestones cannot be reached by implementation effort alone. They require physical
evidence that only the Owner can authorise and witness:

1. **Bench verification of the hardware safety boundary.** The list of required
   verifications is fixed in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md). Until it is
   completed and recorded, production write control stays prohibited.
2. **Commissioning value capture.** Travel limits, pulses per engineering unit, homing
   behaviour, speeds, accelerations, decelerations, pressures, and timeouts are field
   values. They are `[NOT VERIFIED]` and must be captured from engineering records, not
   inferred.
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
- Storing production configuration, tag lists, register maps, or credentials in this
  public repository.

## 5. Sequencing risks

| Risk | Effect | Mitigation approach `[PROPOSED]` |
| --- | --- | --- |
| Bench verification delayed | Supervisory control cannot be enabled in production | Keep the control path exercised against a simulator; keep write control prohibited |
| Unratified commissioning values | Motion and pressure behaviour cannot be finalised | Keep such values external and configuration-driven; never hard-code |
| Open specification items unresolved | Implementation would guess behaviour | Resolve items in [`CURRENT_STATE.md`](CURRENT_STATE.md) before implementing the affected behaviour |
| Scope growth | Determinism and recoverability suffer | Stage gates; owner-only merge; priorities list applied in order |

---

## Related documents

- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage ledger and gate rules
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state and open items
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — bench verification requirements
