# Accelerated Cleaning Integration — Dry Hardware Trial Package (Representative)

**Owner authority:** ACCELERATED-CLEANING-INTEGRATION-01 (D1, D2, D3)
**Trial identity:** `ACCELERATED-CLEANING-INTEGRATION-01 / IV-MAINTAINED-COIL / DRY-01`
**Profile:** `TEST_HARDWARE_DRY` (real typed commands on the bench, no water claimed)
**Entry point:** `Wjss.Runtime --dry-cleaning-trial`

> **Status: NOT AUTHORIZED.** This package is not executable as written. §6 (physical dry
> arrangement) and §7 (representative route) are mandatory Owner Decision blocks and are empty by
> design: no isolation point, Pump state, pressure source, route or threshold is inferred here, and
> none may be filled in from simulation evidence. The trial is authorized only after the Owner fills
> and approves both blocks.

---

> **Sibling package.** The two-stage split of this evidence — **Trial A** (physical Pump-ready,
> procedure only, every IV closed) and **Trial B** (the isolated IV Hardware trial with an explicitly
> simulated `AI-003+` sample) — is documented in
> `docs/ACCELERATED_CLEANING_IV_TWO_STAGE_EVIDENCE.md`. This integrated trial remains a **third,
> separate** evidence class: it is not Trial A and not Trial B, and no claim may be carried from one
> of the three to another.

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

It is also **one selected Sensor path only**. It does not dispatch a next Job, it does not execute an
inter-job `TimeDelay`, and it composes no queue: `GlobalQueue`, the next-dispatch countdown and the
inter-job delay belong to the AutoSequence/Queue simulation surface, which this executable does not
compose. H3-A simulation evidence for Queue and `TimeDelay` remains **separate evidence** and is
never part of this trial.

| Boundary | Status in this trial |
|---|---|
| Physical network contact | **Only under the composition's own consent gates (§2)** |
| Process-I/O reads | Composed (FC02, FC04, FC01) |
| Process-I/O single-coil writes | Composed (FC05, one coil at a time, through the session) |
| Motion (Home, P1 through P6) | **Composed, and refused unless the separate existing motion consent gate passes** |
| Brake release / apply | **COMPOSED — the Home and Move lifecycles release the brake on entry and apply it on completion, on the Owner-configured brake output** |
| Pump command | **NOT COMPOSED — no Pump command path exists** |
| Water flow | **NOT AUTHORIZED** |
| `PRODUCTION` | **NOT AUTHORIZED — never startable** |

The brake row is not cosmetic. Every Home and every Move enters a brake-release phase, waits for the
brake to settle, moves, and then commands the brake back to the applied state; a fault or a Stop
during the operation still applies the brake. Whoever authorizes this trial authorizes that
lifecycle on the resolved axes.

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
   Stating this is not the same as having decided it: §6 records the actual arrangement and must be
   approved first.
6. The Owner-local motion consent, controller endpoint and Gate A values, the Owner-local motion
   profile file and the controller library identities are present (§4). Without them the composition
   refuses and **no axis is opened or moved**.

## 4. External configuration contract

No value below is committed. Every one is read from the process environment, held in memory only,
and never printed, serialized or logged. An absent or invalid value is a typed refusal; **nothing is
defaulted**, with the one stated exception marked below.

### Process-I/O and Cleaning Path

| Variable | Meaning | Refusal when absent or invalid |
|---|---|---|
| `WJSS_DRY_COMMAND_MODE` | explicit selector; only the literal `TEST_HARDWARE_DRY` selects the mode | `DRY_COMMAND_MODE_NOT_SELECTED` |
| `WJSS_DEVICE_PROFILE` | must be `TEST_HARDWARE_DRY` | `DRY_COMMAND_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY` |
| `WJSS_PROCESS_IO_HOST` | Owner-local bench host | `PROCESS_IO_ENDPOINT_NOT_CONFIGURED` |
| `WJSS_PROCESS_IO_PORT` | owner-local port; **defaults to the adopted transport shape** and is overridden only when set | `PROCESS_IO_ENDPOINT_REJECTED` |
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

### Motion (Gate A and the controller library)

Prepare performs real motion preparation, so these are as mandatory as the Process-I/O values. They
are part of the existing motion authority and are not re-implemented by this trial.

| Variable | Meaning | Effect when absent or invalid |
|---|---|---|
| `WJSS_MANUAL_MOTION_CONSENT` | the existing one-use motion consent phrase | motion composition not authorized; no axis opened |
| `WJSS_MANUAL_MOTION_PROFILE` | fully qualified Owner-local manual motion profile file | `MOTION_COMPOSITION_REFUSED` — no Home is attempted |
| `WJSS_MOTION_CONTROLLER_01_ENDPOINT` | Owner-local controller endpoint | motion composition refused |
| `WJSS_MOTION_LIB_DIR` | directory holding the approved controller library | probe refused; no native load |
| `WJSS_MOTION_LIB_EXPECTED_VERSION` | the approved library version identity | probe refused on mismatch |
| `WJSS_MOTION_LIB_GCLIB_SHA256` | approved primary library digest | probe refused on mismatch |
| `WJSS_MOTION_LIB_GCLIBO_SHA256` | approved companion library digest | probe refused on mismatch |

The separate motion **Stop** trial consent (`WJSS_MOTION_STOP_TRIAL_CONSENT`) belongs to a different
procedure and is not used by this run.

## 5. What the run does, in order

| # | Phase | Expected software result | Expected field observation |
|---|---|---|---|
| 1 | configuration and composition | the dry banner and the provenance block printed; the composition result is `DRY_COMPOSITION_COMPOSED` (returned, not printed) | nothing contacted yet |
| 2 | refusal — `PRODUCTION` | `PRODUCTION_NOT_AUTHORIZED` | no write, zero network, no listener |
| 3 | refusal — missing endpoint | `PROCESS_IO_ENDPOINT_NOT_CONFIGURED` | no write, zero network |
| 4 | refusal — wrong consent | `PROCESS_IO_CONSENT_MISMATCH` | no write, zero network |
| 5 | prepare | Process-Io session opened; motion port opened; **Home requested on the resolved Horizontal axis and on the resolved Vertical axis**; `prepare=DRY_PREPARE_READY` only when **both** axes report move-ready | both axes home, brake released and re-applied, both move-ready in a safe area |
| 6 | Pump readiness (AI-003) | accepted only if GOOD, finite, not stale, not future, live generation | gauge reading plausible |
| 7 | Pump readiness refusal (AI-002 only) | not ready — AI-002 is diagnostic/trend only | n/a |
| 8 | P1, then IV Open through admission | `ADMIT` then `DISPATCH_DO` in the session audit; open confirmed only on Upper/open | IV visibly opens |
| 9 | Upper/open **and** IV outlet pressure | both recorded as **separate** evidence | — |
| 10 | P2 through P6 | P2 never requested before the open is accepted | path progresses |
| 11 | IV Close through admission | closed only on Lower/closed | IV visibly closes |
| 12 | terminal | `terminal_status`, `terminal_code`, `primary_failure`, `steps` printed; the typed handoff names the fixed Centre contract (125 mm / 125 mm, 1250000 / 1250000 native) and `PathCompleteAwaitingSafeReturn` | **no Centre move is commanded here**, no Job is released and no `TimeDelay` runs: the Safe Return handoff is a typed record, and the return to Centre is a later lifecycle |
| 13 | FC01 process-image check | reported, **never** treated as field confirmation | — |
| 14 | contradiction (both limits) | `IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS`, not confirmed | fault flagged, no side taken |
| 15 | wrong identity | `IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID` | no write |
| 16 | safe cleanup | motion Stop and close per the motion authority, **then** every IV this trial successfully commanded OPEN is commanded CLOSED through the still-open session, **then** the session is closed | the valves this trial opened are seen to close; every other IV output is left untouched |
| 17 | post-cleanup audit | cleanup counts printed per category; `cleanup.complete` reported truthfully | — |

**Row 12 is narrower than an earlier revision of this package claimed.** That revision said
"Centre 125,125, release Job, TimeDelay". None of those three is executed: this composition owns no
queue, no next-dispatch countdown, no inter-job `TimeDelay` and no Job release, and the Centre
contract is carried as data on the handoff. The truthful chain is:

```
Centre 125,125 dry Safe Return handoff (typed, not commanded)
  → terminal composition result
  → structured shutdown and cleanup
  → process exit
```

**Row 16 is narrower than an earlier revision claimed.** That revision said "IV1..IV8 written false".
`SafeCleanupCloseAll` selects only the valves for which this boundary recorded a **successfully
dispatched OPEN command**, and attempts a CLOSED command for those alone. An IV that this trial never
opened is not written merely because it exists in the catalogue.

The Exit codes the executable actually returns are: `0` terminal result reached; `2` composition,
prepare or start refusal; `3` bounded timeout before a terminal result; `4` invocation or
configuration refusal at the options gate (profile, dry-mode selector, endpoint, consent, port). The
options gate runs before any composition exists, which is why a profile or endpoint refusal is `4`
and not `2`.

## 6. DRY PHYSICAL ARRANGEMENT — OWNER DECISION REQUIRED

This trial needs a pressure source that satisfies AI-003 **and** absolute confidence that no water
reaches the selected Water Jet. Those two conditions depend on the physical isolation arrangement and
cannot be inferred from source, from the catalogue, or from any simulation. They are recorded here by
the Owner before the run and are **not supplied by this package**.

**All twelve fields are `OWNER_DECISION_REQUIRED`.** The trial is **NOT AUTHORIZED** until every field
is filled and the section is approved.

| # | Field | Value |
|---|---|---|
| 1 | Exact physical isolation point | `OWNER_DECISION_REQUIRED` |
| 2 | Is that point upstream or downstream of AI-003 | `OWNER_DECISION_REQUIRED` |
| 3 | How AI-003 obtains valid pressure above the configured Pump-ready setpoint | `OWNER_DECISION_REQUIRED` |
| 4 | Does the Pump run during the trial | `OWNER_DECISION_REQUIRED` |
| 5 | Retained, external or other test pressure used | `OWNER_DECISION_REQUIRED` |
| 6 | Which Main Valve or isolation boundary prevents water reaching the selected WJ | `OWNER_DECISION_REQUIRED` |
| 7 | Proof that an IV command cannot produce water flow at the selected WJ | `OWNER_DECISION_REQUIRED` |
| 8 | Expected AI-003 pressure range during the trial | `OWNER_DECISION_REQUIRED` |
| 9 | Expected selected-IV outlet-pressure behavior | `OWNER_DECISION_REQUIRED` |
| 10 | Safe response if water is observed | `OWNER_DECISION_REQUIRED` |
| 11 | Physical abort method | `OWNER_DECISION_REQUIRED` |
| 12 | Safe initial and terminal valve states | `OWNER_DECISION_REQUIRED` |

Nothing in this section may be filled in by analogy, by copying another site's arrangement, or from
H3-A simulation evidence. An unfilled field is a stop, not a default.

## 7. REPRESENTATIVE ROUTE RECORD — OWNER DECISION REQUIRED

The route is populated from the Owner-selected Sensor, the canonical topology, the Sensor-owned path
and the approved external profiles. **No route is selected here.** In particular this package does
not select a route from H3-A simulation evidence, and it does not invent a representative Sensor.

Every row marked `OWNER_DECISION_REQUIRED` must be recorded verbatim from the authoritative sources
before the run. Rows marked **FIXED** are settled by the current contract and are recorded so that a
reviewer can check them, not so that they can be chosen.

| Field | Value |
|---|---|
| Sensor ID | `OWNER_DECISION_REQUIRED` |
| Sensor parameter source and hash | `OWNER_DECISION_REQUIRED` |
| Effective WJ | `OWNER_DECISION_REQUIRED` |
| Paired IV | `OWNER_DECISION_REQUIRED` |
| WSB | `OWNER_DECISION_REQUIRED` |
| Galil controller | `OWNER_DECISION_REQUIRED` |
| Horizontal axis | `OWNER_DECISION_REQUIRED` |
| Vertical axis | `OWNER_DECISION_REQUIRED` |
| P1 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| P2 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| P3 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| P4 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| P5 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| P6 Horizontal / Vertical | `OWNER_DECISION_REQUIRED` |
| Centre Horizontal | **FIXED — 125 mm (1250000 native)** |
| Centre Vertical | **FIXED — 125 mm (1250000 native)** |
| Pump-ready source | **FIXED — `AI-003+`, the sole Pump-ready pressure authority** |
| Selected-IV pressure tag | **FIXED by ordinal — `AI-` plus (3+n) formatted as exactly three decimal digits: IV1 → `AI-004+`, IV8 → `AI-011+`** |
| Selected-IV Upper/open-limit tag | **FIXED by ordinal — `DI-0` plus (20+n): IV1 → `DI-021`, IV8 → `DI-028`** |
| Selected-IV Lower/closed-limit tag | **FIXED by ordinal — `DI-0` plus (28+n): IV1 → `DI-029`, IV8 → `DI-036`** |
| Selected-IV DO tag | **FIXED by ordinal — `DO-0` plus (22+n): IV1 → `DO-023`, IV8 → `DO-030`** |
| Selected-IV FC05 coil | **FIXED by ordinal — coil 21+n: IV1 → 22, IV8 → 29** |
| OPEN state | **FIXED — true / energized** |
| CLOSE state | **FIXED — false / de-energized** |
| Motion endpoint identity, redacted | `OWNER_DECISION_REQUIRED` |
| Process-I/O endpoint identity, redacted | `OWNER_DECISION_REQUIRED` |
| Approved controller library (gclib) DLL identities | `OWNER_DECISION_REQUIRED` |
| Pump-ready threshold | `OWNER_DECISION_REQUIRED` |
| IV Low threshold | `OWNER_DECISION_REQUIRED` |
| IV High threshold | `OWNER_DECISION_REQUIRED` |
| Valve supervision timeout | `OWNER_DECISION_REQUIRED` |
| Observation freshness budget | **FIXED — 2 s development default of the equipment boundary; not certified and not configurable in this run** |
| Motion heartbeat / timeout values | `OWNER_DECISION_REQUIRED` |

The endpoint values are recorded **redacted**: the printed provenance carries only the
`endpoint-sha256-prefix` fingerprint for the Process-I/O endpoint, and no address, credential or
consent phrase is ever written to this repository.

The per-ordinal tags above are the contract, not a route choice: they follow from the paired IV once
the Sensor is chosen. They are recorded so that the evidence can be checked against the catalogue
row by row.

## 8. Consent and stop conditions

**Stop immediately** and abandon the trial if any of the following occurs:

- any command is issued to a valve other than the one named in the step;
- a coil other than the one bound to the named valve is written;
- the software reports a field confirmation while the corresponding limit input is not asserted;
- FC01 read-back is presented anywhere as physical proof of actuation;
- the Pump or any water path is activated, intentionally or not;
- an axis moves outside the resolved path, or a Home begins while the safe area is not confirmed;
- the endpoint resolves to anything other than the commissioning bench;
- the profile reports anything other than `TEST_HARDWARE_DRY`, or reports `PRODUCTION`;
- any line prints `cleanup.complete=false`, or a non-zero `cleanup.valves.refused` /
  `cleanup.valves.unknown` / `cleanup.valves.transport_failures`.

On stop: the run's `finally` block already performed the safe cleanup; record the last typed code and
report. A stop is a **finding**, not a failure of the trial.

**Abort authority.** The console run exposes no interactive abort key. During Prepare — including
both Home operations — the abort is the physical Emergency Stop plus the commissioned Stop/Abort
authority of the motion lifecycle, which pre-empts a multi-step Home between command steps and still
applies the brake. The Operator and every Observer must know that before the run starts.

## 9. Evidence to capture

For every row: the step number, the exact typed code, the coil address and value, the limit input
states, the pressure value with quality and timestamp, and the witness. The command acceptance, the
FC01 process image and the field feedback must be recorded as **three separate columns** — collapsing
them would destroy the distinction this trial exists to prove.

The provenance block printed at phase 1 is the composition's own statement of what it is
(`endpoint-sha256-prefix:…`, device identity, catalogue row count, Pump-ready source tag, Sensor,
Water Jet, Isolation Valve, axes, motion port mode, motion profile code). It must be archived with
the run.

The cleanup block reports its categories **separately and never as one boolean**:
`cleanup.valves.attempted`, `.closed`, `.refused`, `.transport_failures`, `.unknown`, together with
the motion records and the session state/generation. A cleanup is **complete** only when every IV
this trial successfully commanded OPEN is known to have received the de-energized CLOSED command,
together with the required motion and session evidence — and the cleanup never hides the primary
failure that triggered it.

## 10. Trial claim boundary

**A passing trial establishes only:**

- `TEST_HARDWARE_DRY` startup and the exact refusal behaviour of its gates;
- Owner-local configuration acceptance (endpoint, consent, catalogue, store, parameters, thresholds);
- the Process-I/O physical session lifecycle;
- the AI-003 Pump-ready observation and its acceptance rules;
- the selected IV typed command lifecycle, through admission and dispatch;
- selected IV field confirmation and separate pressure evidence;
- controller Home and the P1-through-P6 motion sequence;
- the dry Centre Safe Return **handoff**;
- Stop/Abort authority and structured cleanup;
- **one** representative integrated Hardware path.

**A passing trial does not establish:**

- water-on cleaning;
- automatic Queue execution;
- inter-job `TimeDelay`;
- second-Job dispatch;
- all-eight-WJ Hardware coverage;
- Production readiness;
- Production authorization;
- valve actuation time, seat tightness, leak rate or pressure-band behaviour under real process
  conditions;
- motion accuracy, or the correctness of any Owner-supplied threshold. The thresholds are whatever
  the Owner supplied locally, and this package does not certify them.

**Authorization checklist.** This trial is authorized as motion-plus-Process-I/O preparation, **not**
as "P1 through P6 only". The authorization must explicitly cover:

- Home of the selected Horizontal axis;
- Home of the selected Vertical axis;
- the existing commissioned Home command lifecycle;
- the brake release and apply lifecycle that Home performs on the Owner-configured brake output;
- Stop/Abort authority during Prepare;
- physical safe-area confirmation before the Home command is invoked;
- the ability of the Owner, the Operator and any Observer to abort before and during Home.

## 11. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED** in the authoring sandbox — this package is a
procedure for the Owner to run locally, not a record of execution.
**PRODUCTION: NOT AUTHORIZED. MERGE: HOLD.**
