# ADR-0005 — Hardware Safety Boundary

- **Status:** ACCEPTED
- **Date:** 2026-10-04
- **Scope:** Which outputs the application may command, under what verification conditions,
  and what the system must never be relied upon to do.
- **Authority:** Approved Stage 0.1 Scope Gate (Repository Documentation Foundation).

---

## Context

The application controls real machinery: an isolation valve that is energize-to-open, a Main
Pump command, a VFD analog speed reference, and Galil-driven axes. The system has no direct
motor-running feedback, and its derived states are software derivations, not hardware
guarantees.

WAGO watchdog capability has been identified, but its exact fail-safe behaviour is not
verified. Until it is, a communication failure, a process crash, a workstation reboot, or a
WAGO reboot could in principle leave an output energized — and the application would be
unable to distinguish that from a healthy state.

Two failure patterns drive this decision:

1. **Stale command re-execution.** A command issued before a communication break is buffered
   and executes after reconnection, moving equipment at a moment no human expects.
2. **Auto re-energization.** Outputs resume their previous state after communication
   recovery, so a valve reopens without an operator command.

A related trap is believing that an interface-level guard is protection. It is not: a user
interface close guard cannot de-energize an output.

## Decision

1. **The system is not a Safety Instrumented System.** It does not replace emergency stop
   circuits, hardwired protection, motor protection, mechanical limits, or controller-side
   safe-stop behaviour. It is not certified to IEC, ISA, ISO, or any other standard.
2. **Hardware emergency stop, limit switches, motor protection, the Local/Remote selector,
   and independent protection remain outside the application's authority.** The application
   never commands, overrides, bypasses, or infers the state of those systems.
3. **Production valve and pump write control is PROHIBITED** until bench tests verify all of
   the following, with recorded evidence:
   1. Ethernet disconnection moves outputs to a safe state.
   2. Equipment Runtime termination moves outputs to a safe state.
   3. Workstation reboot does not leave outputs energized.
   4. WAGO reboot defaults outputs safely.
   5. Watchdog timeout matches configuration.
   6. VFD AO reaches the 0 Hz safe command.
   7. Stale commands do not execute after reconnection.
   8. Outputs do not automatically re-energize after communication recovery.
4. **Target safe states:** Isolation Valve digital output OFF; Main Pump command OFF; VFD
   analog output 0 Hz.
5. **The UI close guard is not an acceptable sole protection** against an energized output
   remaining active.
6. **Supervisory fault responses remain required** once writes are authorised: on valve
   verification failure during an active Cleaning Job, stop or abort motion per the approved
   fault class, command Valve OFF, set VFD AO to 0 Hz or stop the Main Pump, raise a blocking
   alarm, mark the job FAILED or RECOVERY_REQUIRED, do not update
   `LastSuccessfulCleaningCompletedAt`, block the next job, and wait for Operator action.
7. **Bench verification requires separate, explicit Owner authorisation** and a written
   method statement covering the device under test, isolation from production, measurement
   method, witnessing, abort criteria, and recovery steps.
8. **Failure of any single verification item leaves the whole prohibition in force.** Partial
   success grants no partial authority.

## Consequences

- The application can be developed and tested only against simulators until bench evidence
  exists. Simulator fidelity becomes a delivery requirement.
- Requirements for protection must be redirected to hardware rather than implemented in
  software.
- Derived states such as pump "running confirmed by pressure" and valve "OPEN" are
  informational, and may never be used as if they were protective interlocks.
- A communication loss must not be treated as a benign condition anywhere in the design.
- Reconnection logic must be explicit and must never replay buffered commands blindly.
- The absence of direct motor-running feedback is permanent, so pump state will always be an
  inference. Documentation must say so wherever pump state is displayed.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Enable supervisory writes now and verify fail-safe behaviour in the field | Unacceptable: outputs could remain energized with no verified safe state |
| Rely on the WAGO watchdog as the protective layer | Its behaviour is unverified; treating it as protection before evidence exists would be a false safety claim |
| Rely on the application's own close guard and shutdown path | Explicitly rejected as sole protection |
| Treat the derived pump and valve states as interlocks | Software inference is not hardware protection |
| Defer all development until hardware is available | Unnecessary; simulator-based development is possible and lower risk |

## Verification status

- `[NOT VERIFIED]`: every item in decision 3. None has been tested; no hardware has been
  accessed.
- `[NOT VERIFIED]`: WAGO watchdog capability behaviour, timeout matching, and safe-state
  behaviour on timeout.
- `[NOT AUTHORIZED]`: bench verification itself, until separately authorised.
- `[APPROVED]`: the prohibition, the target safe states, the non-substitute rules, the
  required supervisory fault responses, and the requirement for a written method statement.

## References

- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — full boundary statement
- [`../CONTROL_AUTHORITY.md`](../CONTROL_AUTHORITY.md) — authority matrix
- [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) — fault responses
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — HSB requirements
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — bench verification level
- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — no production values
