# Safety Boundary — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] boundary statement. Bench verification items remain
`[NOT VERIFIED]` and production write control remains `[NOT AUTHORIZED]`.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; Stage 0.1 implementation merged to `main`
through PR #1. Stage 0.2 Scope Gate `[APPROVED]` — *Technology and Solution Architecture
Decision*; Stage 0.2 implementation **SUBMITTED FOR OWNER REVIEW**; Owner manual review
**PENDING**; **NOT MERGED**; Stage 0.3 `[NOT AUTHORIZED]`.

> **Read this first.** WJSS is **not** a Safety Instrumented System. It does not replace
> emergency stop circuits, hardwired protection, motor protection, mechanical limits, or
> controller-side safe-stop behaviour. It is **not** certified to IEC, ISA, ISO, or any
> other standard. Industrial standards are used only as guidance and design inspiration.

---

## 1. What the application may and may not do

| The application may | The application must never |
| --- | --- |
| Monitor process values and signal quality | Act as a protective function |
| Evaluate cleaning criteria and arbitrate queues | Inhibit, delay, suppress, or defeat a hardwired protective device |
| Issue supervisory commands over approved paths, once authorised | Command equipment in a way that depends on the application to be safe |
| Raise alarms and escalate to the Operator | Substitute for emergency stop, limit switches, or motor protection |
| Log, trend, and audit | Silence or suppress a protective function |

## 2. Systems outside the application's authority

The following remain outside the application's authority entirely:

- Hardware emergency stop circuits.
- Hardwired protection interlocking.
- Motor protection devices.
- Mechanical limits and hard stops.
- Controller-side safe-stop behaviour.
- The Local/Remote selector.
- Any independent protection layer.

**The application must never command, override, bypass, suppress, or replace an external
protection function.** `[OWNER CONFIRMED]`

### 2.1 Read-only indication of external protection state

Where a read-only indication of an external protection state is available, the application:

- **may** monitor and display that indication;
- **may** use that indication as a **supervisory command gate**;
- does **not** acquire ownership of the external protection function by doing so;
- must **not** treat the indication as a substitute for the actual protection.

**Unknown, unavailable, stale, or bad-quality indication must never be inferred as safe.**
`[OWNER CONFIRMED]`

An indication that is missing, stale, or of bad quality must block the supervisory action
that depends on it. It must never be silently substituted with a benign value, and it must
never be read as "protection present".

## 3. Target safe states

| Output | Target safe state | Status |
| --- | --- | --- |
| Isolation Valve digital output | OFF (valve moves toward closed; energize-to-open) | `[APPROVED]` |
| Main Pump command | OFF | `[APPROVED]` |
| VFD analog output | 0 Hz | `[APPROVED]` |

These are *targets*. That the plant actually reaches them under fault conditions is
`[NOT VERIFIED]` until bench evidence exists.

## 4. The control-write prohibition

Production valve and pump write control is **`[NOT AUTHORIZED]`** until bench tests verify
all of the following, with recorded evidence:

| # | Verification required |
| --- | --- |
| 1 | Ethernet disconnection moves outputs to a safe state. |
| 2 | Equipment Runtime termination moves outputs to a safe state. |
| 3 | Workstation reboot does not leave outputs energized. |
| 4 | WAGO reboot defaults outputs safely. |
| 5 | Watchdog timeout matches configuration. |
| 6 | VFD AO reaches the 0 Hz safe command. |
| 7 | Stale commands do not execute after reconnection. |
| 8 | Outputs do not automatically re-energize after communication recovery. |

Failure of any single item leaves the whole prohibition in force. Partial success does not
grant partial authority.

## 5. WAGO watchdog — current knowledge

WAGO watchdog capability has been **identified**. Its exact fail-safe behaviour remains
`[NOT VERIFIED]`. No document in this repository may state or imply that the watchdog
fail-safe has passed verification, that a timeout value is known, or that a verified safe
state is reached on timeout.

## 6. Explicit non-substitutes

The following must never be described, documented, or reasoned about as protection:

1. **The Operations UI close guard.** Blocking the normal close action while a Cleaning Job
   is active or the Main Pump is running is an **operational usability control** only. It
   prevents accidental normal UI shutdown during active operation. It is **not** a safety
   protection, and it cannot guarantee protection against process termination, Windows
   shutdown, workstation restart, power loss, or hardware failure. It is not an acceptable
   sole protection against an energized output remaining active. `[OWNER CONFIRMED]`
2. **Application-side interlocks.** Application permissives are supervisory. If an
   application interlock fails to act, no protective function may be lost.
3. **Software timeouts.** Timeouts bound the application's own waiting behaviour. They are
   not machinery protection.
4. **Alarms.** An alarm is information for a human. An unacknowledged alarm must not be the
   only barrier between a fault and damage.
5. **Historian records.** Recording a fault is not preventing it.
6. **The DCS Permissive Override.** The override bypasses **only** the approved DCS
   permissive evaluation. It must never be described as a general "Ignore DCS" function and
   must not bypass WAGO communication health, Modbus transport health, Isolation Valve
   feedback, valve open or close verification, Main Pump pressure validation, Galil limits,
   motion faults, encoder or position validation, emergency stop, the Local/Remote
   selector, motor or drive protection, the WAGO output watchdog, external hardware
   protection, critical application lifecycle gates, or the one-active-Cleaning-Job
   invariant. See [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009. `[OWNER CONFIRMED]`

## 7. Safety-related behaviour that *is* required of the application

Once control writes are authorised, the application must still implement these
supervisory responses. They reduce exposure; they do not make the system safety-rated:

- On valve verification failure during an active Cleaning Job: stop or abort motion
  according to the approved fault class; command Valve OFF; set VFD AO to 0 Hz or stop the
  Main Pump; raise a blocking alarm; mark the Job FAILED or RECOVERY_REQUIRED; do not
  update `LastSuccessfulCleaningCompletedAt`; block the next Job; wait for Operator action.
  `[APPROVED]`
- On abnormal valve feedback while the pump runs and no job is active: raise an alarm,
  pause the next-job countdown, and present the Stop All / Continue With Valve Excluded
  modal. "Continue" means **sequential** continuation on another available Water Jet; it
  never authorises concurrent Cleaning Jobs. `[OWNER CONFIRMED]`
- On a blocking DCS-related communication or stale-data condition: stop the next-job
  countdown and do not dispatch the next job; where a job is already active, allow it to
  reach its approved terminal condition first. `[APPROVED]`
- On unknown axis position: block motion mode changes and block job start. `[APPROVED]`
- On blocking alarm: hold the next job until the block is released by the conditions in
  [`ALARM_MODEL.md`](ALARM_MODEL.md), including **cleared-state acknowledgement**.
  `[OWNER CONFIRMED]`
- At most one Cleaning Job may be ACTIVE at any time. Parallel Water Jet cleaning is
  prohibited. `[OWNER CONFIRMED]`

## 8. Bench verification protocol requirements

Before any bench verification is attempted, the Owner must authorise it separately and a
written method statement must exist covering:

1. The exact device under test and its isolation from production equipment.
2. The safe-state measurement method for each of the eight verification items.
3. Who witnesses, and how results are recorded and retained.
4. The abort criteria and the stop condition for the test itself.
5. Recovery steps if an output fails to reach its safe state during the test.

Until then, bench verification is `[NOT AUTHORIZED]`, and the verification list in section
4 remains entirely `[NOT VERIFIED]`.

## 9. Commissioning values remain unverified

Mechanical limits, soft limits, pulses per engineering unit, encoder behaviour, speed,
acceleration, deceleration, homing, the operational envelope, pressure setpoints, pressure
rise timeouts, valve open and close timeouts, alarm thresholds, HardMinimumCleaningInterval
values, DCS permissive definitions, and the stale-data timeout value are commissioning
values. They are `[NOT VERIFIED]` and must be captured from engineering records or field
measurement. They must never be invented, inferred from convention, or copied from an
example. The 30 second stale-timeout figure is an **example only**.

## 10. Standards position

Industrial standards may be used as guidance and design inspiration. No claim of
conformance, compliance, or certification to IEC, ISA, ISO, or any other standard may be
made for this product, its documentation, or its tests.

---

## Related documents

- [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) — who may command what, and when
- [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) — sequence, sequencing invariants, failure handling
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — communication health and UI guard placement
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — HSB, OVR, UIG, and COMH requirements
- [`ALARM_MODEL.md`](ALARM_MODEL.md) — blocking and cleared-state acknowledgement
- [`decisions/ADR-0005-hardware-safety-boundary.md`](decisions/ADR-0005-hardware-safety-boundary.md)
- [`../AGENTS.md`](../AGENTS.md) — stop conditions and device-access prohibition
