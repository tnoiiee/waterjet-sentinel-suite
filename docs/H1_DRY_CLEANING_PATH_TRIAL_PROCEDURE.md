# H1 Dry Cleaning-Path Trial Procedure — WSB1, Two Sequential Runs (REVIEW DRAFT)

> **DRY / NO WATER.** No Pump command. No physical Isolation Valve command. No water. No Queue.
> No AutoSequence. No next-job countdown. No Production.
>
> **HARDWARE NOT EXECUTED.** This procedure is a preparation artifact. Real Hardware execution
> remains fenced behind the explicit Owner authorization of section 17 for the exact frozen PR
> head and this exact reviewed procedure.
>
> **TWO INDEPENDENT SEQUENTIAL RUNS — NEVER SIMULTANEOUS.** This trial contains exactly two
> runs on WSB1 (Run A: G+210 → WJ1, axes A/B; Run B: G+214 → WJ2, axes C/D). Run B may not
> begin until Run A has reached its terminal result, completed Stop/Abort/Brake/cleanup, closed
> its active run/session and been explicitly restarted by the Owner (section 9).

## 1. Identity of this procedure

| Field | Value |
| --- | --- |
| Working label | **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning (two independent sequential runs)** |
| Branch | `arena/7968e33e-waterjet-sentinel-suite` |
| Pull request | PR #41 to `main` (unmerged; do not merge before Owner review) |
| Approved main ancestor | `f9e6e9adfef210f483fd3bfc7471936bbd7f34b2` (PR #40 merge) |
| H1 implementation commit | `c803335fc4957dfa2064d839328188d609f8046e` (parent `edb572543fe064fe16acb51d4e777ac87bc4cfa7`) |
| Exact trial PR head | `0f93542535df3160354e7e774e8b30cd48a6d4b5` (the software-validated head of record; frozen at authorization — section 17) |
| Build identity | `[RECORD AT T0 from the Owner-local build output; not committed]` |
| WSB1 controller identity | `[RECORD AT T0 through the existing approved identity session; the endpoint value itself is never recorded]` |

## 2. Locked two-run scope (Owner decision; no selection remains)

The Owner has selected and locked **both** WSB1 routes. There is no remaining Sensor choice, no
alternative route and no Effective-WJ override anywhere in this trial. Both runs use
`MC-WSB1` only; WSB2–WSB4 are excluded.

### 2.1 Run A — locked identity

| Field | Value |
| --- | --- |
| Selected Sensor | **G+210** (Right / Upper) |
| Canonical / default Water Jet | **WJ1** |
| Configured Effective-WJ override | **none — not permitted in this trial** |
| Effective Water Jet | **WJ1** |
| Assignment provenance | **CANONICAL_ASSIGNMENT** |
| Paired Isolation Valve | **IV1 — informational identity only; no Valve command path exists or is exercised** |
| WSB / controller | WSB1 / `MC-WSB1` (canonical; the run refuses any other cabinet) |
| Axis pair | **A/B** |
| Horizontal axis | **A** |
| Vertical axis | **B** |
| Cleaning Path (P1–P6) | the Owner-local **G+210** Cleaning Path only (Sensor-owned; section 2.4) |
| Centre (fixed contract) | Horizontal = Vertical = **125 mm = 1250000 native per axis** (committed Product authority `CleaningPathUnits.SafeReturnCentre`) |

Run A route chain, exactly:

G+210 → canonical WJ1 → no override → Effective WJ1 → IV1 (informational only) → WSB1 →
MC-WSB1 → Horizontal A → Vertical B → G+210 P1→P6 → Centre 125/125 → terminal cleanup and
session/run close.

### 2.2 Run B — locked identity

| Field | Value |
| --- | --- |
| Selected Sensor | **G+214** (Front / Upper) |
| Canonical / default Water Jet | **WJ2** |
| Configured Effective-WJ override | **none — not permitted in this trial** |
| Effective Water Jet | **WJ2** |
| Assignment provenance | **CANONICAL_ASSIGNMENT** |
| Paired Isolation Valve | **IV2 — informational identity only; no Valve command path exists or is exercised** |
| WSB / controller | WSB1 / `MC-WSB1` (canonical; the run refuses any other cabinet) |
| Axis pair | **C/D** |
| Horizontal axis | **C** |
| Vertical axis | **D** |
| Cleaning Path (P1–P6) | the Owner-local **G+214** Cleaning Path only (Sensor-owned; section 2.4) |
| Centre (fixed contract) | Horizontal = Vertical = **125 mm = 1250000 native per axis** (committed Product authority `CleaningPathUnits.SafeReturnCentre`) |

Run B route chain, exactly:

G+214 → canonical WJ2 → no override → Effective WJ2 → IV2 (informational only) → WSB1 →
MC-WSB1 → Horizontal C → Vertical D → G+214 P1→P6 → Centre 125/125 → terminal cleanup and
session/run close.

### 2.3 Two independent sequential runs

- The two routes execute as **two independent sequential runs**. They must **never** execute
  simultaneously; no simultaneous Water Jet operation and no overlapping run execution exists.
- Run A must reach its terminal state, complete Stop/Abort/Brake/cleanup as applicable, close
  its active run/session according to the accepted lifecycle, and leave **no active axis
  operation** before Run B may begin.
- **No state, Point ledger, primary failure, cleanup evidence or coordinate evidence may leak
  from one run into the other.** Each run records its evidence independently.
- Run A failure, Abort, Close or incomplete cleanup **blocks Run B** (section 9).
- There is **no automatic transition** from Run A to Run B. The Owner must explicitly start
  Run B after reviewing Run A terminal evidence.
- No Sensor other than G+210 and G+214, no WSB2–WSB4 route and no Effective-WJ override is
  permitted in this trial.

### 2.4 Sensor-owned path data

- Run A P1–P6 comes **only** from the Owner-local **G+210** Cleaning Path.
- Run B P1–P6 comes **only** from the Owner-local **G+214** Cleaning Path.
- The Effective-WJ resolution never changes either Sensor path.
- Coordinates are **not copied** between the Sensors.
- Actual coordinate values remain Owner-local and are recorded at T0 into each run's own
  evidence log; they are never committed.

## 3. Coordinate establishment — the exact accepted DP/DE contract (both runs)

- **Home (per axis, independent, no synchronized Begin):** the typed Home lifecycle dispatches the
  commissioned Home sequence and completes through the typed **position-zero definition (DP=0)**
  and its **zero verification by position reads** (TP and RP within the configured tolerance).
  Only `HOME_COMPLETED_ZERO_VERIFIED` establishes the Move-capable coordinate state.
- **Dual-zero definition (DE=0): OPTIONAL and separately authorized — EXCLUDED from both
  Run A and Run B.** The exact Source contract: profile-permitted only (`dualZeroPermitted`),
  dispatch-only, its effect is **UNVERIFIED** (`DUAL_ZERO_DISPATCHED_EFFECT_UNVERIFIED`), and it
  **clears the homed coordinate claim** (any later Move is then refused until a fresh Home). It
  is therefore **recorded in Product state and NOT executed by either run**. If the Owner
  separately wants the Legacy reset-encoder behavior (position define then encoder define), it
  is a separate, separately-authorized step after the H1 run evidence is complete.
- No encoder-reset sequence is invented anywhere in this procedure.

## 4. Configuration recorded at T0 (per run; values live outside Git)

Read from the Owner-local profile (`WJSS_MANUAL_MOTION_PROFILE`) and the run surface into **each
run's own evidence log** before that run's Start. Recorded separately for Run A and for Run B.
Never commit these values.

| Item | Source | Value at T0 |
| --- | --- | --- |
| Observation interval / freshness window | profile | `[RECORD]` |
| Motion (Move) supervision timeout | profile | `[RECORD]` |
| Stop supervision timeout | profile | `[RECORD]` |
| Position tolerance (zero + Move verification) | profile | `[RECORD]` |
| Brake outputs and release action (A–D) | profile (Owner-confirmed allocation 1/2/3/4) | `[RECORD — Run A operates outputs 1 (A) and 2 (B) only; Run B operates outputs 3 (C) and 4 (D) only]` |
| Home speed / Home supervision timeout | profile | `[RECORD]` |
| Move acceleration / speed / deceleration | profile | `[RECORD]` |
| Dual-zero permission per axis | profile | `[RECORD — expected not permitted for either run]` |
| Move target bounds | profile | `[RECORD]` |

## 5. Expected command families (typed plan; per run)

Exactly the closed typed Manual Motion plan may reach the controller, one axis per command:
brake output set/clear; Home speed + Home + Begin; AC / SP / DC / PA + Begin (Move); the typed
per-axis Stop; DP=0 (the Home completion branch).

- **Run A issues these families on axes A and B only** (brake outputs 1 and 2).
- **Run B issues these families on axes C and D only** (brake outputs 3 and 4).
- **No A/B command may occur during Run B. No C/D command may occur during Run A.** Any
  unrelated axis or output command is an immediate STOP condition.
- DE=0 exists in the plan but is excluded from both runs (section 3).
- **No raw command input exists on the surface.** Any unexpected command family, any command on
  an unrelated axis, or any Pump/Valve/output command is a STOP condition.

## 6. Exact start preconditions (per run; any missing, stale, invalid or contradictory = do not start)

**Run A preconditions:**

1. WSB1 controller identity proven for this session (H1-A-T0 evidence).
2. Owner-local endpoint configured (`WJSS_MOTION_CONTROLLER_01_ENDPOINT`, hardware mode; value
   never displayed or recorded) and Owner-local motion profile configured
   (`WJSS_MANUAL_MOTION_PROFILE`).
3. Canonical route matches the locked identity: G+210 → canonical WJ1 → no override →
   Effective WJ1, provenance `CANONICAL_ASSIGNMENT`, paired IV1 informational only (resolver +
   canonical Motion topology).
4. Only axes A and B will operate (the run requests no other axis).
5. Current and fresh Status, TP, RP and Brake-output observations on axes A and B.
6. No axis operation active; no previous H1 run active.
7. Dry mode declared; no Pump command path; no physical Isolation Valve command path; no
   Queue / AutoSequence / next-job path connected.
8. Physical abort accessible; Owner present as Operator/Observer.
9. Owner-local Cleaning Path store (`WJSS_CLEANING_PATH_STORE`) and sensor-parameter CSV
   (`WJSS_SENSOR_PARAMETER_CSV`) loaded and accepted (fail-closed; read once; never written);
   the G+210 Cleaning Path is the loaded path of record for Run A.
10. Exact frozen PR head of section 17 confirmed.

**Run B preconditions (all of the above adapted to the Run B locked identity, plus):**

11. The inter-run gate of section 9 is fully satisfied (Run A terminal, cleaned up, closed,
    independently evidenced, no unresolved failure).
12. Current controller observations are **re-read and re-proven** for Run B at H1-B-T0 before
    any C/D motion. Run A observations are **never** treated as current evidence for Run B.

## 7. Exact abort method (per run)

- **Physical abort (the authoritative means):** `[OWNER-CONFIRMED HARDWIRED MEANS AT REVIEW — the
  trial must not begin without continuous access to it]`.
- **Software Abort (surface Abort control):** preserves the typed Owner-abort cause; prevents the
  next Point from starting; stops every remaining active axis exactly once through the typed
  lifecycle; supervises Stop to inactive; applies Brake after inactive; records cleanup separately;
  retains the last verified Point; blocks any further run until an explicit restart on a fresh
  session.
- **Close (surface Close control):** the typed close cause with the same exactly-once Stop,
  supervised cleanup and session resource close.
- The exact minimally invasive Stop/Abort **evidence** method for the T5 phase of either run is
  not yet fixed: see **BLOCKER B** in section 16.

## 8. Trial phases — Run A (G+210 / WJ1 / A-B)

### H1-A-T0 — Read-only identity and initial state (Run A)
- Connect through the existing approved boundary (`h1-wsb1-hardware` after the consent/Gate-A/
  endpoint chain; no motion).
- Verify controller identity (existing identity session evidence); record the redacted status only.
- Collect current Status / TP / RP / Brake state on axes A and B; verify no motion; verify
  physical abort access.
- Record every section 4 value and the G+210 P1–P6 point ledger into the Run A evidence log.
- Perform **no movement** if any precondition is uncertain.

### H1-A-T1 — Home and coordinate establishment (Run A)
- Home axes A (Horizontal) and B (Vertical) through independent lifecycles (Horizontal requested
  first; no synchronized Begin).
- Verify each axis independently: `HOME_COMPLETED_ZERO_VERIFIED`, TP and RP zero within
  tolerance, Brake applied after inactive.
- Record the DP/DE disposition of section 3 (expected: Home-carried DP verified; DE not
  dispatched).

### H1-A-T2 — Representative dual-axis Move (Run A)
- One bounded dry Move per axis (A and B) to the selected authorized coordinates (the same typed
  Move lifecycle; parallel-independent). The exact target and expected direction/displacement are
  **not fixed in this procedure** — see **BLOCKER A1** (section 16).
- Verify both axes independently (typed completion + fresh inactive Status + TP + RP in
  tolerance + Brake after inactive).
- Confirm the Stop/Abort capability read-only (no deliberate fault — see H1-A-T5).
- Do not proceed on any unexpected motion or observation.

### H1-A-T3 — Selected Sensor P1→P6 (Run A)
- Execute the strict ordered path P1 → P2 → P3 → P4 → P5 → P6 from the **G+210** Cleaning Path
  only (Horizontal then Vertical request per point; fair parallel-independent dispatch; no skip,
  no repeat).
- Every Point must verify on BOTH axes (A and B) before the next Point starts.
- Capture the typed lifecycle evidence per axis per point.
- No Pump/IV/water behavior exists or is expected.

### H1-A-T4 — Centre return (Run A)
- After dual-verified P6 only: return axis A to 125 mm and axis B to 125 mm through the same
  typed Move lifecycle; parallel-independent.
- Verify fresh inactive + TP + RP for both axes; Brake after inactive.
- The dry Safe Return completion is published only after both axes verify. It is a typed H1 dry
  result — never a Pump, Valve, physical Cleaning Job or Production completion.

### H1-A-T5 — Stop/Abort evidence (Run A)
- Only the exact minimally invasive method approved in writing by the Owner for the exact final
  head may be used. **Do not improvise a failure during Hardware motion.** The precise method is
  **BLOCKER B** (section 16) and must be approved before execution.

### Run A terminal cleanup and session/run close
- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- Close the active run/session through the existing typed boundary according to the accepted
  lifecycle; leave **no active operation on A or B**.
- Record the Run A terminal result and all Run A evidence independently (section 13).
- No fabricated Safe Return success under any failure shape.

## 9. Inter-run gate — Run A terminal review (blocks Run B)

There is no automatic transition. After Run A reaches its terminal result, the Owner reviews the
Run A terminal evidence and then explicitly starts Run B. **Run B must be refused if any of the
following is true after Run A:**

- axis A or axis B remains active;
- Stop remains unsupervised;
- Brake state is uncertain;
- cleanup is incomplete;
- session resources are not closed or reset according to the accepted lifecycle;
- Run A ended with unresolved failure;
- controller observations are stale;
- the Owner does not explicitly authorize starting Run B.

No state, Point ledger, primary failure, cleanup evidence or coordinate evidence from Run A may
be carried into Run B as Run B evidence.

## 10. Trial phases — Run B (G+214 / WJ2 / C-D)

### H1-B-T0 — Read-only identity and re-proven initial state (Run B)
- Connect through the existing approved boundary (`h1-wsb1-hardware` after the consent/Gate-A/
  endpoint chain; no motion).
- Verify controller identity (existing identity session evidence); record the redacted status only.
- **Re-read and re-prove current controller observations**: collect fresh Status / TP / RP /
  Brake state on axes C and D; verify no motion; verify physical abort access. Run A observations
  are not current evidence for Run B.
- Confirm the section 9 inter-run gate is satisfied.
- Record every section 4 value and the G+214 P1–P6 point ledger into the Run B evidence log.
- Perform **no movement** if any precondition is uncertain.

### H1-B-T1 — Home and coordinate establishment (Run B)
- Home axes C (Horizontal) and D (Vertical) through independent lifecycles (Horizontal requested
  first; no synchronized Begin).
- Verify each axis independently: `HOME_COMPLETED_ZERO_VERIFIED`, TP and RP zero within
  tolerance, Brake applied after inactive.
- Record the DP/DE disposition of section 3 (expected: Home-carried DP verified; DE not
  dispatched).

### H1-B-T2 — Representative dual-axis Move (Run B)
- One bounded dry Move per axis (C and D) to the selected authorized coordinates (the same typed
  Move lifecycle; parallel-independent). The exact target and expected direction/displacement are
  **not fixed in this procedure** — see **BLOCKER A2** (section 16).
- Verify both axes independently (typed completion + fresh inactive Status + TP + RP in
  tolerance + Brake after inactive).
- Confirm the Stop/Abort capability read-only (no deliberate fault — see H1-B-T5).
- Do not proceed on any unexpected motion or observation.

### H1-B-T3 — Selected Sensor P1→P6 (Run B)
- Execute the strict ordered path P1 → P2 → P3 → P4 → P5 → P6 from the **G+214** Cleaning Path
  only (Horizontal then Vertical request per point; fair parallel-independent dispatch; no skip,
  no repeat).
- Every Point must verify on BOTH axes (C and D) before the next Point starts.
- Capture the typed lifecycle evidence per axis per point.
- No Pump/IV/water behavior exists or is expected.

### H1-B-T4 — Centre return (Run B)
- After dual-verified P6 only: return axis C to 125 mm and axis D to 125 mm through the same
  typed Move lifecycle; parallel-independent.
- Verify fresh inactive + TP + RP for both axes; Brake after inactive.
- The dry Safe Return completion is published only after both axes verify. It is a typed H1 dry
  result — never a Pump, Valve, physical Cleaning Job or Production completion.

### H1-B-T5 — Stop/Abort evidence (Run B)
- Only the exact minimally invasive method approved in writing by the Owner for the exact final
  head may be used. **Do not improvise a failure during Hardware motion.** The precise method is
  **BLOCKER B** (section 16) and must be approved before execution.

### Run B terminal cleanup and session/run close
- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- Close the active run/session through the existing typed boundary according to the accepted
  lifecycle; leave **no active operation on C or D**.
- Record the Run B terminal result and all Run B evidence independently (section 13).
- No fabricated Safe Return success under any failure shape.

## 11. Final combined evidence closeout

- The trial closes with **two independent evidence sets** — Run A and Run B — each complete per
  section 13, recorded separately, with no shared or leaked state, Point ledger, primary failure,
  cleanup evidence or coordinate evidence.
- The combined closeout records: both terminal results, both Centre records, the inter-run gate
  outcome of section 9, and the confirmation that no simultaneous execution and no cross-run
  axis command occurred.
- Only the approved summary leaves the Owner-local environment; nothing is committed beyond it.

## 12. Exact STOP conditions (either run; abort immediately; do not continue to the next phase)

- Controller identity differs; WSB/axis route differs; selected Sensor or coordinates differ.
- Hardware, wiring or mechanical arrangement changed from the accepted evidence.
- Endpoint/profile differs from the reviewed configuration.
- Observation is stale, invalid or missing; Status/TP/RP conflict.
- Unexpected axis moves; direction unexpected; motion exceeds the reviewed bound.
- Brake state uncertain; limit input unexpected.
- Point order differs; either axis fails verification.
- Stop/Abort does not behave exactly as reviewed; cleanup incomplete.
- Any Pump or Isolation Valve command appears; any unrelated axis/output command appears.
- **Any A/B command occurs during Run B. Any C/D command occurs during Run A.**
- **The section 9 inter-run gate is not fully satisfied before Run B.**
- **Run A observations are relied on as current evidence for Run B.**
- **Any Sensor other than G+210 / G+214, any WSB2–WSB4 route, any Effective-WJ override or any
  simultaneous execution is attempted.**
- Physical visibility or abort access is lost; Owner requests Stop.
- Source/head identity changes.

## 13. Exact evidence to capture (per run; kept outside Git except the approved summary)

Each run records its own complete evidence set, independently:

- identity confirmation (selected Sensor; canonical/default WJ; Effective WJ; provenance;
  informational paired IV; controller; axis pair and roles);
- initial Status, initial TP, initial RP, Brake outputs and observed states;
- freshness proof (observation interval and freshness window) and no-active-operation proof;
- Home evidence (per axis, typed lifecycle);
- DP=0 and zero-verification evidence (per axis);
- representative dual-axis Move evidence (per axis);
- P1→P6 point ledger (per axis per point, from that run's own Sensor path);
- Centre-return evidence (Horizontal 125 mm, Vertical 125 mm, 1250000 native per axis);
- Stop/Abort/Brake/cleanup evidence;
- terminal result (terminal phase, code, primary failure if any);
- configuration of section 4 (observation interval; freshness window; Home timeout; Move
  timeout; Stop timeout; position tolerance; Home speed; Move acceleration/speed/deceleration;
  target bounds), physical-abort availability, and operator/observer confirmation.

Never capture or commit: endpoints, credentials, consent values, raw library paths or hashes,
actual G+210 P1–P6 values, actual G+214 P1–P6 values, profile parameter values or any other
Owner-local configuration value.

## 14. Exact cleanup (per run)

- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- Close the session through the existing typed boundary at the end of the session.
- No fabricated Safe Return success under any failure shape.

## 15. Explicit scope statement

Both Run A and Run B perform **dry motion only**: Home, coordinate establishment (DP=0 through
the Home lifecycle), position-zero verification, one bounded representative dual-axis Move,
the run's own selected-Sensor P1→P6, Centre return to 125 mm / 125 mm, Stop, Abort, Brake
lifecycle and cleanup.

Both runs exclude: **DE=0 unless separately authorized**; Pump; physical Isolation Valve
actuation; water; Queue; AutoSequence; next-job countdown; Production; WSB2–WSB4; any Sensor
other than G+210 and G+214; any Effective-WJ override; simultaneous Water Jet or simultaneous
run execution.

IV1 and IV2 are **informational identities only**. No physical IV command path may be available
or exercised. Neither run performs **any Pump command, any physical Isolation Valve command, any
water flow, any Queue/AutoSequence, any Production behavior**, and neither publishes **any**
Pump/Valve/Job/Production completion of any kind.

## 16. Unresolved execution details — Owner-decision blockers

These values are **not invented by this procedure**. They are explicit Owner-decision blockers;
each must be resolved in writing before the corresponding execution.

- **BLOCKER A1 — Run A representative Move.** Exact bounded representative dual-axis Move target
  and expected direction/displacement for Run A (G+210 / WJ1 / A-B). Unresolved.
- **BLOCKER A2 — Run B representative Move.** Exact bounded representative dual-axis Move target
  and expected direction/displacement for Run B (G+214 / WJ2 / C-D). Unresolved.
- **BLOCKER B — Stop/Abort evidence method.** Exact minimally invasive H1-T5 Stop/Abort evidence
  method. Unresolved. If the same Stop/Abort method is intended for both runs, it still requires
  **one explicit Owner approval covering both axis pairs (A/B and C/D)**. If different methods
  are required for the two runs, record and resolve separate **B1** and **B2** blockers instead.

**Hardware authorization cannot be issued until BLOCKERS A1, A2 and B are resolved.**

## 17. Hardware execution gate and required authorization

Do not execute any H1-A or H1-B phase until: implementation is complete; final Owner-local
software validation passes on the exact head (`0f93542535df3160354e7e774e8b30cd48a6d4b5` is the
software-validated head of record); this procedure is delivered and reviewed; **BLOCKERS A1, A2
and B are resolved**; the Owner reviews both routes' Sensor paths and the Centre coordinates; the
physical area is safe with continuous visibility and physical abort access; and the Owner
explicitly authorizes the exact head + this procedure + both locked runs. No Source or
configuration change may occur afterward.

Required authorization wording (equivalent):

```text
AUTHORIZE H1 REAL GALIL DRY TRIAL
ON EXACT PR HEAD <SHA>
USING THE REVIEWED H1 TRIAL PROCEDURE
```

That wording authorizes the reviewed two-run procedure as a whole; Run B still requires the
separate explicit Owner start of section 9 after the Run A terminal review. This document is not
that authorization.
