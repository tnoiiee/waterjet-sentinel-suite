# Accelerated Cleaning Integration — Two-Stage Hardware Evidence Package

**Owner authority:** `ACCELERATED-CLEANING-INTEGRATION-01-TWO-STAGE-HARDWARE-EVIDENCE`
**Profile:** `TEST_HARDWARE` for Trial A (read-only, no command path), `TEST_HARDWARE_DRY` for Trial B
(the single-DO write path), and nothing else for either
**Companion document:** `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` — the *integrated* dry trial
(motion + IV command + physical AI-003), which remains a **third, separate** evidence class and is
neither stage described here

> **Status: NOT EXECUTED, NOT AUTHORIZED.** Nothing in this document was run. No physical network
> contact, no valve actuation, no Pump command and no motion occurred while it was written. Items that
> only the Owner can decide are marked `OWNER_DECISION_REQUIRED` and are deliberately empty.
> **Both trials are now executable.** Trial A runs through `ProcessIoPumpReadyReadTrial` (read-only,
> physical `AI-003+`) and Trial B through `DryIvTrial` (one coil write, simulated `AI-003+`); see §2 and
> §3. Neither has been executed: the physical runs remain separately authorized Owner-local activities.

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
| Executable today | **Yes** — `ProcessIoPumpReadyReadTrial` (read-only, no command path; see §3) | **Yes** — `DryIvTrial` |

The two stages are **never combined**. Trial B deliberately runs with the Pump stopped, so its
Pump-ready input cannot be physical; Trial A deliberately runs with every valve closed, so it cannot
produce valve evidence. Combining them would produce one ambiguous record instead of two clean ones.

The two selects are mutually exclusive in software: Trial B refuses when the integrated dry selector
is present, Trial A refuses when either the integrated dry selector or the Trial B selector is present,
and neither of the two existing paths is modified by Trial A.

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
6. read the initial Upper/open and Lower/closed feedback **before** any write and admit the OPEN only
   from the one physically confirmed CLOSED state:

   | Upper / open | Lower / closed | Meaning | Result |
   |---|---|---|---|
   | `false` | `true` | closed, physically confirmed | **admitted** |
   | `true` | `true` | contradictory feedback | refused, `IV_TRIAL_REFUSED_INITIAL_STATE_CONTRADICTORY` |
   | `true` | `false` | already open | refused, `IV_TRIAL_REFUSED_INITIAL_STATE_UNSAFE` |
   | `false` | `false` | closed **not** confirmed | refused, `IV_TRIAL_REFUSED_INITIAL_CLOSED_NOT_CONFIRMED` |

   "Neither limit asserted" is not evidence that the valve is closed, so the last row refuses. An
   unusable or absent Lower observation reads `false` through the same path, so a missing feedback
   channel refuses here rather than being treated as safe. None of the three refused rows writes
   anything;
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

An operator abort (Ctrl+C) is honoured without ever acting from the console handler. The handler does
exactly two things: it suppresses the default runtime termination and raises a trial-local cancellation
flag. It holds no device, session or transport reference, so it cannot write a coil, close the session,
dispose the transport or terminate the process, and there is no second cleanup path running beside the
first. The bounded supervision loop observes the flag at its next observation point (one poll interval,
at most 25 ms), the main sequence returns `IV_TRIAL_ABORTED_BY_OPERATOR` with exit code 2, the single
existing cleanup path closes whatever the trial left energised, and the session closes last before the
transport is released. An abort during OPEN supervision therefore produces exactly one cleanup CLOSE; an
abort during CLOSE supervision produces none, because the coil is already de-energised.

If the process is killed outright while the coil is energised, the maintained coil stays energised — the
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

## 3. Trial A — physical Pump-ready evidence (executable, read-only)

> **Correction (2026-11-03).** An earlier revision of this section stated that "no new executable is
> added for Trial A, and none is required", and that the Owner could run the existing read-only
> acquisition path to obtain the Pump-ready acceptance decision. Source inspection under
> `TRIAL-A-PHYSICAL-PUMP-READY-ENABLEMENT` proved that statement false and it is withdrawn. The gap
> analysis is in `docs/TRIAL_A_PHYSICAL_PUMP_READY_ENABLEMENT_ANALYSIS.md`; the narrow host it proposed
> is now implemented as `ProcessIoPumpReadyReadTrial` and routed by the runtime host.

### 3.1 Entry point and selectors

| Variable | Required value | Meaning |
|---|---|---|
| `WJSS_PUMP_READY_READ_TRIAL` | `AI-003_ONLY` (exact, case-sensitive) | selects Trial A and nothing else |
| `WJSS_DEVICE_PROFILE` | `TEST_HARDWARE` | the only authorized profile for this stage |
| `WJSS_PROCESS_IO_HOST` / `_PORT` / `_UNIT_ID` | Owner-local endpoint | resolved by the existing `ProcessIoEndpointConfiguration` |
| `WJSS_PROCESS_IO_CONSENT` | the published **read-only** `PhysicalDiAiRead` phrase | write consent never authorizes a read trial |
| `WJSS_PROCESS_IO_CATALOGUE` | fully qualified path, ≤ 1 MiB | loaded by the existing `ProcessIoCatalogueLoader` |
| `WJSS_PUMP_READY_SETPOINT_BAR` | one finite bar value | **never defaulted** |
| `WJSS_PRESSURE_LOW_BAR` / `_HIGH_BAR` | optional, finite | carried unchanged, never invented as Owner values |
| `WJSS_PUMP_READY_FRESHNESS_MS` | integer 1–600000 | the freshness budget for the single observation |

The consent phrase is read from the environment only. It is never printed, logged, persisted or passed
on a command line; the report carries a non-reversible digest code instead of the phrase, and a
non-reversible fingerprint instead of the endpoint address.

### 3.2 Refusals, all before a transport is constructed

Trial A refuses, with exit code 4 and no network contact, when: the selector is absent; the selector is
not the exact literal; `PRODUCTION` is the profile; the profile is not `TEST_HARDWARE` (`SIMULATOR`,
`TEST_HARDWARE_DRY` and `TEST_HARDWARE_READ_ONLY` are all refused, the last because it is committed to
the one-Node TC read path); the integrated dry selector or the Trial B selector is also present; the
endpoint is missing or rejected; the consent is missing or does not match the read-only phrase; the
catalogue is missing, relative, oversized or refused by the loader; the `AI-003+` row is absent,
unbound, not an adopted READ Input Register on the Process-I/O rack, or resolves to another device; the
Pump-ready setpoint is missing or non-finite; the pressure band is invalid; or the freshness budget is
missing or out of range.

### 3.3 What the trial does, in order

1. validates the selector, the profile and the two exclusions;
2. resolves the Owner-local endpoint and validates the read-only consent;
3. loads the catalogue and proves the `AI-003+` binding is an adopted READ Input Register on the rack;
4. reads the setpoint, the optional band and the freshness budget, defaulting none of them;
5. constructs the existing `ProcessIoPhysicalTransport` — only now does anything exist that could
   reach the network;
6. opens the existing `ProcessIoSession` and captures its generation;
7. observes `AI-003+` once, capturing raw word, engineering bar, source tag, quality, `ObservedAt`,
   `ReceivedAt`, freshness and generation;
8. optionally observes `AI-002+` and reports it as `NOT_USED_FOR_DECISION`;
9. builds a `PressureSample` carrying the Pump-Outlet identity and applies the existing
   `SequencingPressure.Valid` and `SequencingPressure.PumpReady` rules with the configured setpoint;
10. reports the evidence, closes the session, releases the transport, and exits.

Steps 1–4 perform no I/O at all. Nothing is retried, no previous observation is reused, and no value is
substituted on refusal.

### 3.4 What the trial commands

**Nothing.** The path contains no write admission, no digital-output dispatch, no coil write, no
register write, no IV command, no motion, no Home, no Queue and no `TimeDelay`. It composes no write
authorization and holds no equipment boundary: a read needs no authority, and the total absence of a
write authority is what keeps the stage read-only. Every Isolation Valve stays CLOSED for the whole
stage.

### 3.5 Evidence printed

`TRIAL: PHYSICAL_PUMP_READY_READ_ONLY`, `source_tag: AI-003+`, `evidence_kind`, `physical_pump_verified`,
`physical_ai003_verified`, `pump_ready`, `raw_value`, `engineering_bar`, `quality`, `observed_at`,
`fresh`, `session_generation`, `configured_setpoint_bar`, `strict_comparison`,
`ai002_role: DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION` with `ai002_used_for_decision=FALSE`,
`iv_command_executed: FALSE`, `motion_executed: FALSE`, `waterjet_command_executed: FALSE`.

`physical_pump_verified` and `physical_ai003_verified` are `TRUE` only when the whole chain passed; a
refused or failed run can never claim physical Pump verification.

### 3.6 Exit codes

| Code | Meaning |
|---|---|
| `0` | the physical `AI-003+` observation is valid, current and **strictly** above the setpoint |
| `2` | the observation completed but Pump-ready was not accepted: invalid quality, stale, future-dated, superseded generation, wrong source identity, or at/below the setpoint |
| `3` | bounded read timeout or transport fault — no value was produced |
| `4` | refusal before acquisition: selector, profile, endpoint, consent, catalogue, binding, threshold or freshness |

### 3.7 Trial A procedure

1. all eight Isolation Valves are confirmed CLOSED and stay closed for the whole stage;
2. no IV command, no Home, no motion, no Queue, no `TimeDelay`, no next Job;
3. the Pump runs under the Owner's own control and authority;
4. `AI-003+` — the sole Pump-ready authority — is read and decided **by WJSS**, in one generation;
5. `AI-002+` is diagnostic only and is never treated as Pump-ready;
6. the record captures the Pump state, the running condition, the acceptance decision and the
   freshness/generation the decision was made in.

`OWNER_DECISION_REQUIRED` — the Pump running condition (speed, duration, water destination), the safe
observation point, the person authorized to start and stop the Pump, and the abort method. All of these
are Owner-local and none of them is defaulted by the software.

**Trial A proves:** a physical Pump can produce a physical `AI-003+` reading that the existing
Pump-ready rule accepts, in the recorded generation — `PHYSICAL_PUMP_READY`.

**Trial A does not prove:** any Isolation Valve movement, any valve feedback, any motion, any queue
behaviour, any wet cleaning outcome, or anything about Trial B. Trial A is not repeated per WSB: it is
one shared Pump-Outlet verification.

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

**Trial A establishes only:** that a physical `AI-003+` reading, obtained through the existing transport
and session, satisfied the existing Pump-ready rule under the Owner-supplied setpoint in the recorded
generation — reported as `PHYSICAL_PUMP_READY`. **Trial A does not establish** any valve, motion, queue
or wet-cleaning behaviour, and it establishes nothing about Production.

**Combining the two stages into one claim is not permitted.** A reader of either record must be able
to see which part was physical and which part was simulated without consulting a second document,
which is why the simulated evidence is labelled and the physical Pump evidence is explicitly declared
absent in Trial B.

## 6. Boundaries

**PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED** — no physical network contact, no valve actuation,
no Pump command and no motion occurred while this package was written.
**SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA** — the Owner-local build is the validation gate.
**PRODUCTION: NOT AUTHORIZED. MERGE: HOLD.**
