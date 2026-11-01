# Accelerated Cleaning Integration — Two-Stage Hardware Evidence Package

**Owner authority:** `ACCELERATED-CLEANING-INTEGRATION-01-TWO-STAGE-HARDWARE-EVIDENCE`
**Profile:** `TEST_HARDWARE_DRY` for both stages, and nothing else
**Companion document:** `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` — the *integrated* dry trial
(motion + IV command + physical AI-003), which remains a **third, separate** evidence class and is
neither stage described here

> **Status: NOT EXECUTED, NOT AUTHORIZED.** Nothing in this document was run. No physical network
> contact, no valve actuation, no Pump command and no motion occurred while it was written. Items that
> only the Owner can decide are marked `OWNER_DECISION_REQUIRED` and are deliberately empty.

---

## 1. Why the evidence is split into two stages

The integrated dry trial asks one run to witness several unrelated things at once. When it fails, the
failure could belong to the Pump, the transmitter, the wiring, the valve, the controller or the
software, and the record cannot say which. Two small, separately authorized stages answer one question
each:

| | **Trial A — Pump-ready evidence** | **Trial B — IV Hardware evidence** |
|---|---|---|
| Question answered | Does a running Pump produce a Pump-ready pressure the software accepts from `AI-003+`? | Does one typed Isolation Valve command reach the field and come back as real position feedback? |
| Pump state | **Running** (Owner-controlled) | **Stopped**, Owner-attested |
| Pump-ready source | **Physical** `AI-003+` via the WAGO analogue input | **Software-simulated** `AI-003+` sample, explicitly labelled |
| IV commands | **None — every IV stays CLOSED** | **One** valve OPEN, then CLOSED |
| Motion / Home / P1–P6 | **None** | **None** |
| Queue / `TimeDelay` / next Job | **None** | **None** |
| Executable today | **No** — procedure only | **Yes** — `DryIvTrial` |

The two stages are **never combined**. Trial B deliberately runs with the Pump stopped, so its
Pump-ready input cannot be physical; Trial A deliberately runs with every valve closed, so it cannot
produce valve evidence. Combining them would produce one ambiguous record instead of two clean ones.

The two selects are mutually exclusive in software: Trial B refuses when the integrated dry selector
is present, and the integrated dry path is unmodified by Trial B.

## 2. Trial B — the isolated IV Hardware trial (executable)

### 2.1 Entry point and selectors

`Wjss.Runtime` routes to `DryIvTrial` **only** when every value below is present and exact. There is no
command-line parser in this host: `--dry-iv-trial` is the published surface token, and the actual
selector is an environment variable, exactly as the integrated dry trial works.

| Variable | Required value | Notes |
|---|---|---|
| `WJSS_DRY_IV_TRIAL_MODE` | `SOFTWARE_SIMULATED_AI003` | Exact, case-sensitive. Absent → the host continues down its existing path. Any other value → refusal. |
| `WJSS_PROCESS_IO_HOST` | Owner-local bench host | Never committed, never defaulted |
| `WJSS_PROCESS_IO_PORT` / `_UNIT_ID` | Optional | Defaults to the adopted transport shape |
| `WJSS_PROCESS_IO_CONSENT` | the published single-DO consent constant | Referenced in code as `ProcessIoConsentPhrases.PhysicalDoSingle` |
| `WJSS_DRY_IV_TRIAL_PUMP_STOPPED` | the published attestation literal | Referenced in code as `DryIvTrial.PumpStoppedAttestation` |
| `WJSS_DRY_IV_TRIAL_VALVE` | exactly one canonical identity, `IV1` … `IV8` | No alias, no zero-padding, no default |
| `WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR` | one finite number of bar | Must exceed the setpoint and lie inside the accepted engineering domain |
| `WJSS_PRESSURE_LOW_BAR` / `_HIGH_BAR` / `WJSS_PUMP_READY_SETPOINT_BAR` | Owner-local finite bar | Same contract as the integrated dry trial; never defaulted |
| `WJSS_VALVE_SUPERVISION_MS` | 1 … 600000 | The existing supervision window; never defaulted |
| `WJSS_PROCESS_IO_CATALOGUE` | fully qualified committed catalogue path | Loaded and verified by the existing loader |
| `WJSS_DRY_COMMAND_MODE` | **must not select** the integrated dry path | Present and selecting → refusal |

`PRODUCTION` is refused before anything else is read. `SIMULATOR` is refused because no simulated
runtime may stand in for a physical valve. The Kiosk, the HTTP surface, the Queue and every
Production startup path are untouched and cannot reach this trial.

### 2.2 Refusals, all before any coil write

Each is a typed code printed to standard error, with no retry and no substitution:

selector absent · integrated dry selector selected instead · profile not `TEST_HARDWARE_DRY` ·
`PRODUCTION` · endpoint absent or rejected · consent absent or mismatched · Pump-stopped attestation
absent or not the exact literal · simulated pressure absent, non-finite, at-or-below the setpoint or
outside the engineering domain · owner-local catalogue absent or refused · IV command and feedback
bindings missing · write authorization not granted · session not open · sample generation stale ·
initial feedback contradictory or already open · admission refused · OPEN or CLOSE not dispatched ·
feedback not confirmed within the supervision window.

The trial never silently falls back to the physical `AI-003+` reading, to the simulator, to a default
or previous pressure, to another valve, or to a Production path.

### 2.3 What the trial does, in order

1. validate the invocation, then the profile, endpoint, consent and attestation;
2. validate the simulated-input selector and the simulated pressure against the Product rule;
3. load and verify the catalogue, validate the adopted IV bindings, mint the write authorization;
4. construct the transport (no socket yet) and open the existing `ProcessIoSession`;
5. mint the simulated Pump-ready sample for **this** session generation and require the existing
   Pump-ready rule to accept it — the trial refuses otherwise;
6. read the initial Upper/open and Lower/closed feedback and refuse a contradictory or already-open
   state **before** any write;
7. admit and dispatch **OPEN** on the selected valve's existing command coil (`DO-023` for `IV1`);
8. supervise the physical Upper/open discrete input (`DI-021` for `IV1`) until the existing contract
   confirms, contradicts, or the supervision window expires;
9. report the FC01 process-image read-back **separately**, never as confirmation;
10. report the IV outlet pressure (`AI-004+` for `IV1`) **separately**, when usable;
11. admit and dispatch **CLOSE** on the same coil, then supervise the Lower/closed input (`DI-029`);
12. run structured cleanup, close the session **last**, release the transport, print the
    classification and exit.

The valve is held OPEN only for one supervision window. There is no dwell, no motion, no Home, no
P1–P6, no queue, no `TimeDelay`, no next Job and no Pump command anywhere in the sequence.

### 2.4 Evidence printed by the trial

| Field | Meaning |
|---|---|
| `source_tag` | `AI-003+` — the Pump-ready tag identity of record |
| `evidence_kind` | `SOFTWARE_SIMULATED` — never `PHYSICAL` |
| `trial_purpose` | `IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY` |
| `physical_pump_verified` | `FALSE`, always |
| `physical_ai003_verified` | `FALSE`, always |
| `physical_iv_command` | `TRUE` only when the command was actually dispatched |
| `physical_upper_feedback` | `TRUE` only from the physical discrete input |
| `physical_lower_feedback` | `TRUE` only from the physical discrete input |
| `PUMP_READY_EVIDENCE = SOFTWARE_SIMULATED` | printed unconditionally |
| `PHYSICAL_PUMP_EVIDENCE = NOT_ESTABLISHED_BY_THIS_TRIAL` | printed unconditionally |
| `PUMP_STOPPED: … / NOT SOFTWARE_VERIFIED` | the attestation is the Owner's statement; the software never claims to have measured it |

### 2.5 Cleanup

Cleanup runs on success, on every refusal after an OPEN was attempted, on a supervision timeout, on a
feedback contradiction, on an unexpected exception and on an operator abort. It acts **only** on the
valves this trial dispatched OPEN **and left energised** — so on a clean run it correctly attempts
nothing, because the trial closed its own valve already, and it never issues a second CLOSE dispatch.
Every close goes through the existing session admission and dispatch lifecycle, and the record reports
attempted / admitted / closed / refused / transport-failure / remaining-unknown separately. It never
hides the primary failure and never claims closure from the FC01 process image.

An operator abort (Ctrl+C) is honoured: it de-energises the same coil and closes the session. If the
process is killed outright while the coil is energised, the maintained coil stays energised — the
physical abort described in §2.6 is the authority for that case, and the procedure must say so.

### 2.6 Trial B procedure

1. `OWNER_DECISION_REQUIRED` — name the Operator and the person who can physically abort.
2. Confirm the Pump is stopped, no Pump command is authorized, Pump start is physically prevented or
   under continuous control, no water flow from the Pump is expected, and the selected Isolation Valve
   may be operated with the line in this state.
3. Record the attestation in the environment variable named in §2.1. The software records the
   attestation identity, not a claim of measurement.
4. Run the trial once. It is single-use: it does not retry, it does not loop and it does not sweep
   valves.
5. Capture standard output, standard error, the exit code and the session audit trail as the record.
6. `OWNER_DECISION_REQUIRED` — state whether a repeated run is permitted, and on what evidence.

## 3. Trial A — physical Pump-ready evidence (procedure only)

**No new executable is added for Trial A, and none is required by this package.** The Owner runs the
existing read-only acquisition path against the running Pump and records what the software accepts.

Trial A is a **procedure**, not a code change:

1. all eight Isolation Valves are confirmed CLOSED and stay closed for the whole stage;
2. no IV command, no Home, no motion, no Queue, no `TimeDelay`, no next Job;
3. the Pump runs under the Owner's own control and authority;
4. the existing acquisition records `AI-003+` — the sole Pump-ready authority — and the Pump-ready
   acceptance decision it produces;
5. `AI-002+` is recorded **as a diagnostic only** and is never treated as Pump-ready;
6. the record captures the Pump state, the running condition, the acceptance decision and the
   freshness/generation the decision was made in.

`OWNER_DECISION_REQUIRED` — the Pump running condition (speed, duration, water destination), the safe
observation point, the person authorized to start and stop the Pump, and the abort method.

**Trial A proves:** a physical Pump can produce an `AI-003+` reading that the existing Pump-ready rule
accepts, in the recorded generation.

**Trial A does not prove:** any Isolation Valve movement, any valve feedback, any motion, any queue
behaviour, or any wet cleaning outcome. Trial A produces **no** `PHYSICAL_PUMP_READY` label for the
software path; the label belongs to the Owner's record, not to a simulated or integrated run.

## 4. Route identity (stated once — no per-WSB matrix)

The canonical authority is the adopted Process-I/O catalogue and the existing Isolation Valve helpers.
For the representative route:

| Element | Identity |
|---|---|
| Water Jet / WSB | `WJ1` / `WSB1` / `MC-WSB1` (Legacy `WJ2`–`Cannon 1` mapping is the same row set) |
| Valve identity | `IV1` (ordinal 1 of 8) |
| Command coil | `DO-023`, zero-based coil 22, FC05, `true` = energised = OPEN, `false` = de-energised = CLOSED |
| Upper / OPEN feedback | `DI-021`, zero-based address 20 |
| Lower / CLOSED feedback | `DI-029`, zero-based address 28 |
| IV outlet pressure | `AI-004+`, input register 3 |
| Pump-ready pressure | `AI-003+` only |

`IV2` … `IV8` and the other WSBs follow the same canonical ordinal mapping (`DO-0(22+n)`,
`DI-0(20+n)`, `DI-0(28+n)`, `AI-0(3+n)+`) through the existing helpers and the commissioned Legacy
evidence. This package asserts **extendability only**: it does not claim that any other valve or WSB
was executed, and it deliberately does **not** repeat the route per WSB or require a per-WSB trial.

## 5. Claim boundary

**Trial B establishes only:** the isolated selector and its refusals; the trial-only write
authorization; the existing session admission and dispatch lifecycle for one coil; the physical
Upper/open and Lower/closed field feedback of one valve; the separate FC01 read-back; the separate IV
outlet pressure; and structured cleanup that closes exactly the valve it opened.

**Trial B does not establish:** anything about the Pump; anything about the physical `AI-003+`
transmitter; water-on behaviour; any second valve; any other WSB; motion, Home or P1–P6; Queue or
`TimeDelay` behaviour; Production readiness; valve actuation time, seat tightness, leak rate or
pressure-band behaviour; or the correctness of any Owner-supplied threshold.

**Combining the two stages into one claim is not permitted.** A reader of either record must be able
to see which part was physical and which part was simulated without consulting a second document,
which is why the simulated evidence is labelled and the physical Pump evidence is explicitly declared
absent in Trial B.

## 6. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED** — no physical network contact, no valve actuation,
no Pump command and no motion occurred while this package was written.
**SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA** — the Owner-local build is the validation gate.
**PRODUCTION: NOT AUTHORIZED. MERGE: HOLD.**
