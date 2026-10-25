# H1 Dry Cleaning-Path Trial Procedure — WSB1 (REVIEW DRAFT)

> **DRY / NO WATER.** No Pump command. No physical Isolation Valve command. No water. No Queue.
> No AutoSequence. No next-job countdown. No Production.
>
> **HARDWARE NOT EXECUTED.** This procedure is a preparation artifact. Real Hardware execution
> remains fenced behind the explicit Owner authorization of section 13 for the exact frozen PR
> head and this exact reviewed procedure.

## 1. Identity of this procedure

| Field | Value |
| --- | --- |
| Working label | **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning** |
| Branch | `arena/7968e33e-waterjet-sentinel-suite` |
| Pull request | PR #41 to `main` (unmerged; do not merge before Owner review) |
| Approved main ancestor | `f9e6e9adfef210f483fd3bfc7471936bbd7f34b2` (PR #40 merge) |
| H1 implementation commit | `c803335fc4957dfa2064d839328188d609f8046e` (parent `edb572543fe064fe16acb51d4e777ac87bc4cfa7`) |
| Exact trial PR head | the PR head named verbatim in the Owner authorization wording of section 13 (frozen at authorization; no Source change afterward) |
| Build identity | `[RECORD AT T0 from the Owner-local build output; not committed]` |
| WSB1 controller identity | `[RECORD AT T0 through the existing approved identity session; the endpoint value itself is never recorded]` |

## 2. Selected run identity (Owner completes at review; values are not committed)

| Field | Value |
| --- | --- |
| Selected Sensor (one only) | `[OWNER SELECTS; under the adopted canonical Equipment topology (2026-10-25) the WSB1 routes are G+210 (Right / Upper) → canonical WJ1 / IV1, axes A/B (Horizontal A, Vertical B), and G+214 (Front / Upper) → canonical WJ2 / IV2, axes C/D (Horizontal C, Vertical D)]` |
| Canonical / default Water Jet | `[derived by the typed resolver; record the resolved value]` |
| Configured Effective-WJ override (if any) | `[none, or the exact WJ identity entered on the surface]` |
| Effective Water Jet | `[resolved value with provenance]` |
| Paired Isolation Valve | **informational evidence only — no Valve command exists in this scope** |
| WSB / controller | WSB1 / `MC-WSB1` (canonical; the run refuses any other cabinet) |
| Axis pair and roles | the resolved pair from the canonical Motion topology (A/B or C/D; A/C Horizontal, B/D Vertical) |
| P1–P6 (mm, Horizontal / Vertical) | `[read from the Owner-local store of record at T0 into the evidence log]` |
| Centre (fixed contract) | Horizontal = Vertical = **125 mm = 1250000 native** (committed Product authority `CleaningPathUnits.SafeReturnCentre`) |

The path is Sensor-owned data: the Effective-WJ resolution never changes P1–P6. The surface shows
the six points and the Centre contract before Start.

## 3. Coordinate establishment — the exact accepted DP/DE contract

- **Home (per axis, independent, no synchronized Begin):** the typed Home lifecycle dispatches the
  commissioned Home sequence and completes through the typed **position-zero definition (DP=0)**
  and its **zero verification by position reads** (TP and RP within the configured tolerance).
  Only `HOME_COMPLETED_ZERO_VERIFIED` establishes the Move-capable coordinate state.
- **Dual-zero definition (DE=0): OPTIONAL and separately authorized.** The exact Source contract:
  profile-permitted only (`dualZeroPermitted`), dispatch-only, its effect is **UNVERIFIED**
  (`DUAL_ZERO_DISPATCHED_EFFECT_UNVERIFIED`), and it **clears the homed coordinate claim**
  (any later Move is then refused until a fresh Home). It is therefore **recorded in Product
  state and NOT executed by this run**. If the Owner separately wants the Legacy reset-encoder
  behavior (position define then encoder define), it is a separate, separately-authorized step
  after the H1 run evidence is complete.
- No encoder-reset sequence is invented anywhere in this procedure.

## 4. Configuration recorded at T0 (values live outside Git)

Read from the Owner-local profile (`WJSS_MANUAL_MOTION_PROFILE`) and the run surface into the
evidence log before Start. Never commit these values.

| Item | Source | Value at T0 |
| --- | --- | --- |
| Observation interval / freshness window | profile | `[RECORD]` |
| Motion (Move) supervision timeout | profile | `[RECORD]` |
| Stop supervision timeout | profile | `[RECORD]` |
| Position tolerance (zero + Move verification) | profile | `[RECORD]` |
| Brake outputs and release action (A–D) | profile (Owner-confirmed allocation 1/2/3/4) | `[RECORD]` |
| Home speed / Home supervision timeout | profile | `[RECORD]` |
| Move acceleration / speed / deceleration | profile | `[RECORD]` |
| Dual-zero permission per axis | profile | `[RECORD — expected not permitted for this run]` |
| Move target bounds | profile | `[RECORD]` |

## 5. Expected command families (typed plan; Legacy-shape parity)

Exactly the closed typed Manual Motion plan may reach the controller, one axis per command:
brake output set/clear; Home speed + Home + Begin; AC / SP / DC / PA + Begin (Move); the typed
per-axis Stop; DP=0 (the Home completion branch). DE=0 exists in the plan but is not expected in
this run (section 3). **No raw command input exists on the surface.** Any unexpected command
family, any command on an unrelated axis, or any Pump/Valve/output command is a STOP condition.

## 6. Exact start preconditions (any missing, stale, invalid or contradictory = do not start)

1. WSB1 controller identity proven for this session (T0 evidence).
2. Owner-local endpoint configured (`WJSS_MOTION_CONTROLLER_01_ENDPOINT`, hardware mode; value
   never displayed or recorded) and Owner-local motion profile configured
   (`WJSS_MANUAL_MOTION_PROFILE`).
3. Canonical route matches the selected Effective WJ (resolver + canonical Motion topology).
4. Only the resolved axis pair will operate (the run requests no other axis).
5. Current and fresh Status, TP, RP and Brake-output observations on both resolved axes.
6. No axis operation active; no previous H1 run active.
7. Dry mode declared; no Pump command path; no physical Isolation Valve command path; no
   Queue / AutoSequence / next-job path connected.
8. Physical abort accessible; Owner present as Operator/Observer.
9. Owner-local Cleaning Path store (`WJSS_CLEANING_PATH_STORE`) and sensor-parameter CSV
   (`WJSS_SENSOR_PARAMETER_CSV`) loaded and accepted (fail-closed; read once; never written).
10. Exact frozen PR head of section 13 confirmed.

## 7. Exact abort method

- **Physical abort (the authoritative means):** `[OWNER-CONFIRMED HARDWIRED MEANS AT REVIEW — the
  trial must not begin without continuous access to it]`.
- **Software Abort (surface Abort control):** preserves the typed Owner-abort cause; prevents the
  next Point from starting; stops every remaining active axis exactly once through the typed
  lifecycle; supervises Stop to inactive; applies Brake after inactive; records cleanup separately;
  retains the last verified Point; blocks any further run until an explicit restart on a fresh
  session.
- **Close (surface Close control):** the typed close cause with the same exactly-once Stop,
  supervised cleanup and session resource close.

## 8. Trial phases

### H1-T0 — Read-only identity and initial state
- Connect through the existing approved boundary (`h1-wsb1-hardware` after the consent/Gate-A/
  endpoint chain; no motion).
- Verify controller identity (existing identity session evidence); record the redacted status only.
- Collect current Status / TP / RP / Brake state; verify no motion; verify physical abort access.
- Record every section 4 value into the evidence log.
- Perform **no movement** if any precondition is uncertain.

### H1-T1 — Home and coordinate establishment
- Home the resolved Horizontal and Vertical axes (independent lifecycles; Horizontal requested
  first; no synchronized Begin).
- Verify each axis independently: `HOME_COMPLETED_ZERO_VERIFIED`, TP and RP zero within tolerance,
  Brake applied after inactive.
- Record the DP/DE disposition of section 3 (expected: Home-carried DP verified; DE not
  dispatched).

### H1-T2 — Representative dual-axis Move
- One bounded dry Move per axis to the selected authorized coordinates (the same typed Move
  lifecycle; parallel-independent).
- Verify both axes independently (typed completion + fresh inactive Status + TP + RP in
  tolerance + Brake after inactive).
- Confirm the Stop/Abort capability read-only (no deliberate fault — see H1-T5).
- Do not proceed on any unexpected motion or observation.

### H1-T3 — Selected Sensor P1→P6
- Execute the strict ordered path P1 → P2 → P3 → P4 → P5 → P6 (Horizontal then Vertical request
  per point; fair parallel-independent dispatch; no skip, no repeat).
- Every Point must verify on BOTH axes before the next Point starts.
- Capture the typed lifecycle evidence per axis per point.
- No Pump/IV/water behavior exists or is expected.

### H1-T4 — Centre return
- After dual-verified P6 only: return Horizontal to 125 mm and Vertical to 125 mm through the same
  typed Move lifecycle; parallel-independent.
- Verify fresh inactive + TP + RP for both axes; Brake after inactive.
- The dry Safe Return completion is published only after both axes verify. It is a typed H1 dry
  result — never a Pump, Valve, physical Cleaning Job or Production completion.

### H1-T5 — Stop/Abort evidence
- Only the exact minimally invasive method approved in writing by the Owner for the exact final
  head may be used. **Do not improvise a failure during Hardware motion.** The Owner must approve
  the precise method before execution (separate to this draft).

## 9. Exact STOP conditions (abort the trial immediately; do not continue to the next phase)

- Controller identity differs; WSB/axis route differs; selected Sensor or coordinates differ.
- Hardware, wiring or mechanical arrangement changed from the accepted evidence.
- Endpoint/profile differs from the reviewed configuration.
- Observation is stale, invalid or missing; Status/TP/RP conflict.
- Unexpected axis moves; direction unexpected; motion exceeds the reviewed bound.
- Brake state uncertain; limit input unexpected.
- Point order differs; either axis fails verification.
- Stop/Abort does not behave exactly as reviewed; cleanup incomplete.
- Any Pump or Isolation Valve command appears; any unrelated axis/output command appears.
- Physical visibility or abort access is lost; Owner requests Stop.
- Source/head identity changes.

## 10. Exact evidence to capture (kept outside Git except the approved summary)

- T0: redacted identity status, initial Status/TP/RP/Brake observations + freshness, section 4
  values, abort-access confirmation.
- T1–T4: per-axis typed outcome codes, observation snapshots + freshness, point ledger, per-axis
  brake evidence, cleanup evidence (if any), dry Safe Return state.
- Terminal: the typed H1 dry-commissioning result (terminal phase, code, primary failure if any,
  coordinate evidence, path ledger, Centre record, cleanup evidence).
- Never capture or commit: endpoints, credentials, consent values, raw library paths or hashes.

## 11. Exact cleanup

- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- Close the session through the existing typed boundary at the end of the session.
- No fabricated Safe Return success under any failure shape.

## 12. Explicit scope statement

This trial performs **dry motion only**: Home, coordinate establishment, dual-axis Move, selected
Sensor P1→P6, Centre 125/125, Stop/Abort/Brake/cleanup. It performs **no Pump command, no physical
Isolation Valve command, no water flow, no Queue/AutoSequence, no Production behavior**, and it
publishes **no** Pump/Valve/Job/Production completion of any kind.

## 13. Hardware execution gate and required authorization

Do not execute H1-T0 through H1-T5 until: implementation is complete; final Owner-local software
validation passes on the exact head; this procedure is delivered and reviewed; the Owner reviews
the selected Sensor, path and Centre coordinates; the physical area is safe with continuous
visibility and physical abort access; and the Owner explicitly authorizes the exact head + this
procedure + the selected Sensor/WJ/axes. No Source or configuration change may occur afterward.

Required authorization wording (equivalent):

```text
AUTHORIZE H1 REAL GALIL DRY TRIAL
ON EXACT PR HEAD <SHA>
USING THE REVIEWED H1 TRIAL PROCEDURE
```

This document is not that authorization.
