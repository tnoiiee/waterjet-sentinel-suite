# Accelerated Cleaning Integration — Dry Hardware Trial Package (Representative)

**Owner authority:** ACCELERATED-CLEANING-INTEGRATION-01 (D1, D2, D3)
**Trial identity:** `ACCELERATED-CLEANING-INTEGRATION-01 / IV-MAINTAINED-COIL / DRY-01`
**Profile:** `TEST_HARDWARE_DRY` (software-only command composition)
**Entry point:** `Wjss.Runtime --dry-cleaning-trial`

---

## 1. What this package is, and what it is not

This is the **representative dry trial**: it exercises the complete typed IV command and feedback
composition, and the complete cleaning-path motion composition, against the commissioning bench
**with command authority enabled but no process actuation claimed**. It exists so that the command
path, the corrected feedback polarity, the evidence separation, the pressure/Pump-ready decision,
the axis orchestration and the safe-cleanup state can be witnessed before any wet or Production
trial is proposed.

It is **not** a wet trial, **not** a Production trial and **not** evidence of process outcome. A dry
trial that succeeds proves the **software and wiring-of-record** path only.

| Boundary | Status in this trial |
|---|---|
| Physical network contact | **Only under the composition's own consent gates (§2)** |
| Process-I/O reads | Composed (FC02, FC04, FC01) |
| Process-I/O single-coil writes | Composed (FC05, one coil at a time, through the session) |
| Motion (open, Home, P1 through P6) | **Composed, and refused unless the separate existing motion consent gate passes** |
| Pump command | **NOT COMPOSED — no Pump command path exists** |
| Brake output release | **NOT COMPOSED — no brake release is requested** |
| Water flow | **NOT AUTHORIZED** |
| `PRODUCTION` | **NOT AUTHORIZED — never startable** |

## 2. The composition root, and why it is one object

`apps/runtime/Program.cs` builds exactly one composition for this profile, in this order, and only
after every gate has passed:

1. the explicit dry selector (`WJSS_DRY_COMMAND_MODE=TEST_HARDWARE_DRY`) — never inferred;
2. the device profile gate (`TEST_HARDWARE_DRY` by name; `PRODUCTION` refused before any variable is
   read; the physical read-only mode is refused as a conflict, not resolved);
3. the Owner-local Process-Io endpoint and the exact consent phrase;
4. the committed Process-Io catalogue, verified by the loader (row count, column contract, source
   order and the per-row provenance hash);
5. the adopted bindings for IV1..IV8 and the AI-003 row, proven **before** anything is composed over
   them;
6. the Owner-local Cleaning Path store (canonical topology) and the Owner-local sensor-parameter CSV
   (Sensor-owned P1 through P6);
7. the Phase 3A resolver, which derives the path, the fixed Centre, the Effective Water Jet, the
   paired Isolation Valve and the axis letters;
8. the shared motion composition (`Wjss.MotionComposition`): the existing controller owner, the
   existing hardware-capable port and the existing manual motion session;
9. the Process-Io transport, session and equipment boundary;
10. the ONE `CleaningPathCompositionCoordinator`, which composes the ONE
    `CleaningPathExecutionCoordinator` internally as its point-advance gate.

The dry composition is **never routed through `SimulatorRuntime`**. It binds **no port**, registers
**no route** and adds **no Kiosk control**: the mode has exactly one surface, the bounded console run.

## 3. Preconditions the Owner must satisfy locally

1. Owner-local Process-Io endpoint configured outside the repository. No address, credential or
   endpoint value is committed to the Product tree; only a SHA-256 prefix fingerprint is reported.
2. The consent phrase supplied through the environment, never as a command-line argument:
   `I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL`.
3. All `WJSS_*` variables cleared in one PowerShell process before restore/build/test.
4. Dry profile selected explicitly: `TEST_HARDWARE_DRY` **with** the dry command mode. Selecting it
   together with the physical read-only mode is refused (`PROFILE_MODE_CONFLICT`), not resolved.
5. Pump physically isolated and the water supply closed, so an unintended output cannot move water.
6. The Owner-local motion consent, endpoint and Gate A values, and the Owner-local motion profile
   file, are present. Without them the composition refuses and **no axis is opened or moved**.

## 4. External configuration contract

No value below is committed. Every one is read from the process environment, held in memory only,
and never printed, serialized or logged. An absent or invalid value is a typed refusal; **nothing is
defaulted**.

| Variable | Meaning | Refusal when absent or invalid |
|---|---|---|
| `WJSS_DRY_COMMAND_MODE` | explicit selector; only the literal `TEST_HARDWARE_DRY` selects the mode | `DRY_COMMAND_MODE_NOT_SELECTED` |
| `WJSS_DEVICE_PROFILE` | must be `TEST_HARDWARE_DRY` | `DRY_COMMAND_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY` |
| `WJSS_PROCESS_IO_HOST` | Owner-local bench host | `PROCESS_IO_ENDPOINT_NOT_CONFIGURED` |
| `WJSS_PROCESS_IO_PORT` | owner-local port; defaults to the adopted transport shape | `PROCESS_IO_ENDPOINT_REJECTED` |
| `WJSS_PROCESS_IO_UNIT_ID` | owner-local unit identity | `PROCESS_IO_ENDPOINT_REJECTED` |
| `WJSS_PROCESS_IO_CONSENT` | the exact consent phrase | `PROCESS_IO_CONSENT_MISSING` / `_MISMATCH` |
| `WJSS_PROCESS_IO_CATALOGUE` | fully qualified committed catalogue CSV | `DRY_COMPOSITION_REFUSED_CATALOGUE` |
| `WJSS_CLEANING_PATH_STORE` | Owner-local Cleaning Path store | `DRY_COMPOSITION_REFUSED_STORE` |
| `WJSS_SENSOR_PARAMETER_CSV` | Owner-local sensor-parameter CSV | `DRY_COMPOSITION_REFUSED_SENSOR_PARAMETERS` |
| `WJSS_CLEANING_SENSOR_ID` | the canonical Sensor identity for this run | `DRY_COMPOSITION_REFUSED_SENSOR_ID` |
| `WJSS_PRESSURE_LOW_BAR` | Owner-local Low threshold | `DRY_COMPOSITION_REFUSED_THRESHOLDS` |
| `WJSS_PRESSURE_HIGH_BAR` | Owner-local High threshold | `DRY_COMPOSITION_REFUSED_THRESHOLDS` |
| `WJSS_PUMP_READY_SETPOINT_BAR` | Owner-local Pump-ready setpoint | `DRY_COMPOSITION_REFUSED_THRESHOLDS` |
| `WJSS_VALVE_SUPERVISION_MS` | supervision window, 1 to 600000 ms | `DRY_COMPOSITION_REFUSED_SUPERVISION_WINDOW` |

**The pressure thresholds are required Owner-local values.** Earlier revisions of this package quoted
development defaults; those defaults are no longer used, because an invented threshold would
fabricate a Pump-ready decision.

## 5. What the run does, in order

| # | Phase | Expected software result | Expected field observation |
|---|---|---|---|
| 1 | configuration and composition | `DRY_COMPOSITION_COMPOSED`, provenance block printed | nothing contacted yet |
| 2 | refusal — `PRODUCTION` | `PRODUCTION_NOT_AUTHORIZED` | no write, zero network, no listener |
| 3 | refusal — missing endpoint | `PROCESS_IO_ENDPOINT_NOT_CONFIGURED` | no write, zero network |
| 4 | refusal — wrong consent | `PROCESS_IO_CONSENT_MISMATCH` | no write, zero network |
| 5 | prepare | session opened; `DRY_PREPARE_READY` | both resolved axes move-ready |
| 6 | Pump readiness (AI-003) | accepted only if GOOD, finite, not stale, not future, live generation | gauge reading plausible |
| 7 | Pump readiness refusal (AI-002 only) | not ready — AI-002 is diagnostic/trend only | n/a |
| 8 | P1, then IV Open through admission | `ADMIT` then `DISPATCH_DO` in the session audit; open confirmed only on Upper/open | IV visibly opens |
| 9 | Upper/open **and** IV outlet pressure | both recorded as **separate** evidence | — |
| 10 | P2 through P6 | P2 never requested before the open is accepted | path progresses |
| 11 | IV Close through admission | closed only on Lower/closed | IV visibly closes |
| 12 | Centre 125,125, release Job, TimeDelay | terminal status and code printed | — |
| 13 | FC01 process-image check | reported, **never** treated as field confirmation | — |
| 14 | contradiction (both limits) | `IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS`, not confirmed | fault flagged, no side taken |
| 15 | wrong identity | `IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID` | no write |
| 16 | safe cleanup | motion Stop per axis, session close, then IV1..IV8 written false | all IVs de-energised/closed |
| 17 | post-cleanup audit | cleanup counts printed; `cleanup.complete` reported truthfully | — |

The Exit codes are: `0` terminal result reached; `2` configuration, composition or start refusal;
`3` bounded timeout before a terminal result; `4` invalid invocation.

## 6. Consent and stop conditions

**Stop immediately** and abandon the trial if any of the following occurs:

- any command is issued to a valve other than the one named in the step;
- a coil other than the one bound to the named valve is written;
- the software reports a field confirmation while the corresponding limit input is not asserted;
- FC01 read-back is presented anywhere as physical proof of actuation;
- the Pump or any water path is activated, intentionally or not;
- an axis moves outside the resolved path;
- the endpoint resolves to anything other than the commissioning bench;
- the profile reports anything other than `TEST_HARDWARE_DRY`, or reports `PRODUCTION`;
- any line prints `cleanup.complete=false`, or a non-zero `cleanup.valves.refused` /
  `cleanup.valves.unknown` / `cleanup.valves.transport_failures`.

On stop: the run's `finally` block already performed the safe cleanup; record the last typed code and
report. A stop is a **finding**, not a failure of the trial.

## 7. Evidence to capture

For every row: the step number, the exact typed code, the coil address and value, the limit input
states, the pressure value with quality and timestamp, and the witness. The command acceptance, the
FC01 process image and the field feedback must be recorded as **three separate columns** — collapsing
them would destroy the distinction this trial exists to prove.

The provenance block printed at phase 1 is the composition's own statement of what it is
(`endpoint-sha256-prefix:…`, device identity, catalogue row count, Pump-ready source tag, Sensor,
Water Jet, Isolation Valve, axes, motion port mode, motion profile code). It must be archived with
the run.

## 8. What a passing run establishes — and what it does not

**Establishes:** the maintained-coil polarity is correct end to end; the corrected Upper/open and
Lower/closed feedback rows are correct; the three evidence kinds stay separate; refusals cost zero
network I/O; every write goes through one session authority; safe cleanup leaves every commanded
valve de-energised and every axis stopped.

**Does not establish:** valve actuation time, seat tightness, leak rate, pressure-band behaviour under
real process conditions, motion accuracy, or any Production readiness. The thresholds are whatever
the Owner supplied locally; this package does not certify them.

## 9. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED** in the authoring sandbox — this package is a
procedure for the Owner to run locally, not a record of execution.
**PRODUCTION: NOT AUTHORIZED. MERGE: HOLD.**
