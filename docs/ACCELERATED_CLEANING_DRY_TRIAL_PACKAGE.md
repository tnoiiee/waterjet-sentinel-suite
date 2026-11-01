# Accelerated Cleaning Integration — Dry Hardware Trial Package (Representative)

**Owner authority:** ACCELERATED-CLEANING-INTEGRATION-01 (D1, D2, D3)
**Trial identity:** `ACCELERATED-CLEANING-INTEGRATION-01 / IV-MAINTAINED-COIL / DRY-01`
**Profile:** `TEST_HARDWARE_DRY` (software-only command composition)

---

## 1. What this package is, and what it is not

This is the **representative dry trial**: it exercises the complete typed IV command and feedback
composition against the commissioning bench **with command authority enabled but no process
actuation claimed**. It exists so that the command path, the corrected feedback polarity, the
evidence separation and the safe-cleanup state can be witnessed before any wet or Production trial
is proposed.

It is **not** a wet trial, **not** a Production trial and **not** evidence of physical valve
actuation. A dry trial that succeeds proves the **software and wiring-of-record** path only.

| Boundary | Status in this trial |
|---|---|
| Physical network contact | **Authorized only under this package's own consent step (§4)** |
| Process-I/O reads | Authorized (FC02, FC04, FC01) |
| Process-I/O single-coil writes | Authorized (FC05, one coil at a time) |
| Pump command, Brake output, motion | **NOT AUTHORIZED** |
| Water flow | **NOT AUTHORIZED** |
| `PRODUCTION` | **NOT AUTHORIZED — never startable** |

## 2. Preconditions the Owner must satisfy locally

1. Owner-local endpoint configured outside the repository. No address, credential or endpoint value
   is committed to the Product tree; only a SHA-256 prefix fingerprint is ever reported.
2. The consent phrase supplied through the environment, never as a command-line argument:
   `I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL`.
3. All `WJSS_*` variables cleared in one PowerShell process before restore/build/test.
4. Dry profile selected explicitly: `TEST_HARDWARE_DRY` **with** the dry command mode. Selecting it
   together with the physical read-only mode is refused (`PROFILE_MODE_CONFLICT`), not resolved.
5. Pump physically isolated and the water supply closed, so an unintended output cannot move water.

## 3. Trial matrix (one row is one witnessed step)

| # | Step | Command / observation | Expected software result | Expected field observation |
|---|---|---|---|---|
| 1 | Profile gate | start with `TEST_HARDWARE_DRY` + dry mode | grant issued, `DRY_COMMAND_GRANTED` | n/a |
| 2 | Refusal — Production | request `PRODUCTION` | `DRY_COMMAND_REFUSED_PRODUCTION_NOT_AUTHORIZED` | no write, zero network |
| 3 | Refusal — endpoint | remove endpoint | `DRY_COMMAND_REFUSED_ENDPOINT_MISSING` | no write, zero network |
| 4 | Refusal — consent | wrong consent phrase | `DRY_COMMAND_REFUSED_CONSENT_MISMATCH` | no write, zero network |
| 5 | Pump readiness | read AI-003 | GOOD, finite, not stale, not future | gauge reading plausible |
| 6 | Pump readiness refusal | inlet instrument only (AI-002) | not ready — AI-002 is diagnostic/trend only | n/a |
| 7 | Open IV1 | FC05 coil 22 = **true** | `IV_OPEN_FIELD_CONFIRMED` after upper limit | IV1 visibly opens |
| 8 | Open IV8 | FC05 coil 29 = **true** | confirms only on `DI-028` (Upper/open) | IV8 visibly opens |
| 9 | Process-image check | FC01 coil 22 | reported, **not** treated as confirmation | — |
| 10 | Contradiction | force both limits for IV1 | `IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS`, not confirmed | fault flagged, no side taken |
| 11 | Close IV1 | FC05 coil 22 = **false** | `IV_CLOSED_FIELD_CONFIRMED` on `DI-029` (Lower/closed) | IV1 visibly closes |
| 12 | Wrong-identity refusal | issue `IV9` | `IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID` | no write |
| 13 | Safe cleanup | `SafeCleanupCloseAll` | false written to every opened coil, state retained CLOSED | all IVs de-energised/closed |
| 14 | Post-cleanup audit | read back all eight coils | all false | — |

## 4. Consent and stop conditions

**Stop immediately** and abandon the trial if any of the following occurs:

- any command is issued to a valve other than the one named in the step;
- a coil other than `21+n` for `IVn` is written;
- the software reports `FieldConfirmed` while the corresponding limit input is not asserted;
- FC01 read-back is presented anywhere as physical proof of actuation;
- the Pump, Brake, motion or any water path is activated, intentionally or not;
- the endpoint resolves to anything other than the commissioning bench;
- the profile reports anything other than `TEST_HARDWARE_DRY`, or reports `PRODUCTION`.

On stop: perform the safe cleanup step (§3 row 13), record the last typed code, and report. A stop is
a **finding**, not a failure of the trial.

## 5. Evidence to capture

For every row: the step number, the exact typed code, the coil address and value, the limit input
states, the pressure value with quality and timestamp, and the witness. The command acceptance, the
FC01 process image and the field feedback must be recorded as **three separate columns** — collapsing
them would destroy the distinction this trial exists to prove.

## 6. What a passing run establishes — and what it does not

**Establishes:** the maintained-coil polarity is correct end to end; the corrected Upper/open and
Lower/closed feedback rows are correct; the three evidence kinds stay separate; refusals cost zero
network I/O; safe cleanup leaves every commanded valve de-energised.

**Does not establish:** valve actuation time, seat tightness, leak rate, pressure-band thresholds under
real process conditions, or any Production readiness. Thresholds in this package are development
defaults (Low 1.0 bar, High 15.0 bar, Pump-ready setpoint 15.0 bar) and are **not** certified.

## 7. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED** in the authoring sandbox — this package is a
procedure for the Owner to run locally, not a record of execution.
**PRODUCTION: NOT AUTHORIZED. MERGE: NOT AUTHORIZED.**
