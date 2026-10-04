# ADR-0005 — Hardware Safety Boundary

- **Status:** ACCEPTED
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-confirmed review punchlist)
- **Scope:** Which outputs the application may command, under what verification conditions,
  what the system must never be relied upon to do, and how external protection functions are
  treated.
- **Authority:** Approved Stage 0.1 Scope Gate, as corrected by the Owner-confirmed Stage
  0.1 documentation review punchlist.

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

Three failure patterns drive this decision:

1. **Stale command re-execution.** A command issued before a communication break is buffered
   and executes after reconnection, moving equipment at a moment no human expects.
2. **Auto re-energization.** Outputs resume their previous state after communication
   recovery, so a valve reopens without an operator command.
3. **Substituting a software guard for protection.** A related trap is believing that an
   interface-level guard protects anything. It does not: a user interface close guard cannot
   de-energize an output.

A fourth consideration is the operator's need for visibility. External protection states may
be *readable*. That readability is useful — and dangerous if it is mistaken for ownership of
the protection function.

## Decision

### Position

1. **The system is not a Safety Instrumented System.** It does not replace emergency stop
   circuits, hardwired protection, motor protection, mechanical limits, or controller-side
   safe-stop behaviour. It is not certified to IEC, ISA, ISO, or any other standard.
2. **The application must never command, override, bypass, suppress, or replace an external
   protection function.** This includes hardware emergency stop, hardwired protection
   interlocking, motor protection devices, mechanical limits and hard stops,
   controller-side safe-stop behaviour, the Local/Remote selector, and any independent
   protection layer. `[OWNER CONFIRMED]`
3. **Read-only indication of an external protection state.** Where such an indication is
   available, the application may monitor and display it and may use it as a **supervisory
   command gate**. The indication does not transfer ownership of the external protection
   function to the application, must not be treated as a substitute for the actual
   protection, and **unknown, unavailable, stale, or bad-quality indication must never be
   inferred as safe.** `[OWNER CONFIRMED]`
4. **Sequential execution.** At most one Cleaning Job may be ACTIVE at any time. Parallel
   Water Jet cleaning is prohibited. Different Water Jets, valves, walls, or controllers do
   not grant authority for concurrency. `[OWNER CONFIRMED]`

### Control-write prohibition

5. **Production valve and pump write control is PROHIBITED** until bench tests verify all of
   the following, with recorded evidence:
   1. Ethernet disconnection moves outputs to a safe state.
   2. Equipment Runtime termination moves outputs to a safe state.
   3. Workstation reboot does not leave outputs energized.
   4. WAGO reboot defaults outputs safely.
   5. Watchdog timeout matches configuration.
   6. VFD AO reaches the 0 Hz safe command.
   7. Stale commands do not execute after reconnection.
   8. Outputs do not automatically re-energize after communication recovery.
6. **Target safe states:** Isolation Valve digital output OFF; Main Pump command OFF; VFD
   analog output 0 Hz.
7. **Failure of any single verification item leaves the whole prohibition in force.** Partial
   success grants no partial authority.

### Non-substitutes

8. **The Operations UI close guard is an operational usability control, not a safety
   protection.** It blocks the normal close action while a Cleaning Job is active or the
   Main Pump is running, to prevent accidental normal UI shutdown during active operation.
   It cannot guarantee protection against process termination, Windows shutdown, workstation
   restart, power loss, or hardware failure, and it is not an acceptable sole protection
   against an energized output remaining active. `[OWNER CONFIRMED]`
9. **The DCS Permissive Override is scoped.** It may bypass only the approved DCS permissive
   evaluation. It must never be described as a general "Ignore DCS" function, and must not
   bypass WAGO communication health, Modbus transport health, Isolation Valve feedback, valve
   open or close verification, Main Pump pressure validation, Galil limits, motion faults,
   encoder or position validation, emergency stop, Local/Remote selector, motor or drive
   protection, the WAGO output watchdog, external hardware protection, critical application
   lifecycle gates, or the one-active-Cleaning-Job invariant. `[OWNER CONFIRMED]`

### Required supervisory responses

10. **Once writes are authorised, supervisory fault responses remain required:** on valve
    verification failure during an active Cleaning Job, stop or abort motion per the approved
    fault class, command Valve OFF, set VFD AO to 0 Hz or stop the Main Pump, raise a blocking
    alarm, mark the job FAILED or RECOVERY_REQUIRED, do not update
    `LastSuccessfulCleaningCompletedAt`, block the next job, and wait for Operator action.
11. **Blocking alarms release only on cleared-state acknowledgement.** An acknowledgement
    recorded while an alarm was ACTIVE is awareness only and does not release the block.

### Bench verification

12. **Bench verification requires separate, explicit Owner authorisation** and a written
    method statement covering the device under test, isolation from production, measurement
    method, witnessing, abort criteria, and recovery steps.

## Consequences

- The application can be developed and tested only against simulators until bench evidence
  exists. Simulator fidelity becomes a delivery requirement.
- Requirements for protection must be redirected to hardware rather than implemented in
  software.
- Derived states such as pump "running confirmed by pressure" and valve "OPEN" are
  informational, and may never be used as if they were protective interlocks.
- A communication loss must not be treated as a benign condition anywhere in the design. A
  constant value is not evidence that the link is healthy.
- Reconnection logic must be explicit and must never replay buffered commands blindly.
- The absence of direct motor-running feedback is permanent, so pump state will always be an
  inference. Documentation must say so wherever pump state is displayed.
- Read-only protection indications become a usability asset and a trap: they must always be
  labelled as indications, never as protection, and a missing or bad-quality indication must
  block the action that depends on it.
- Operators gain a documented, audited way to work through a DCS permissive problem, at the
  cost of maintaining an exclusion list that must be tested.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Enable supervisory writes now and verify fail-safe behaviour in the field | Unacceptable: outputs could remain energized with no verified safe state |
| Rely on the WAGO watchdog as the protective layer | Its behaviour is unverified; treating it as protection before evidence exists would be a false safety claim |
| Rely on the application's own close guard and shutdown path | Explicitly rejected as sole protection |
| Treat the derived pump and valve states as interlocks | Software inference is not hardware protection |
| Treat a readable protection indication as ownership of the protection function | Readability is not ownership; the indication is not a substitute for the protection |
| Infer "safe" from a missing or bad-quality indication | Prohibited: unknown, unavailable, stale, or bad-quality indication must never be inferred as safe |
| Make the DCS override a general permissive bypass | It would silently disable communication health, valve verification, motion faults, and the sequencing invariant — the exact protections the plant relies on |
| Defer all development until hardware is available | Unnecessary; simulator-based development is possible and lower risk |

## Verification status

- `[NOT VERIFIED]`: every item in decision 5. None has been tested; no hardware has been
  accessed.
- `[NOT VERIFIED]`: WAGO watchdog capability behaviour, timeout matching, and safe-state
  behaviour on timeout.
- `[NOT AUTHORIZED]`: bench verification itself, until separately authorised; and production
  device access, at this stage.
- `[OWNER CONFIRMED]`: the prohibition on commanding, overriding, bypassing, suppressing, or
  replacing an external protection function; the read-only indication rule; the UI close
  guard's non-safety status; the DCS Permissive Override scope and exclusions; and the
  sequencing invariant.

## References

- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — full boundary statement
- [`../CONTROL_AUTHORITY.md`](../CONTROL_AUTHORITY.md) — authority matrix and override scope
- [`../CLEANING_SEQUENCE.md`](../CLEANING_SEQUENCE.md) — fault responses and sequencing
- [`../ALARM_MODEL.md`](../ALARM_MODEL.md) — cleared-state acknowledgement
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — communication health and UI guard placement
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — HSB, OVR, and UIG requirements
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — bench verification level
- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — no production values
