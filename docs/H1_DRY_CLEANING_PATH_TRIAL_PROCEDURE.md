# H1 Dry Cleaning-Path Trial Procedure — WSB1, Two Sequential Runs (REVIEW DRAFT)

> **DRY / NO WATER.** No Pump command. No physical Isolation Valve command. No water. No Queue.
> No AutoSequence. No next-job countdown. No Production.
>
> **HARDWARE NOT EXECUTED.** This procedure is a preparation artifact. Real Hardware execution
> remains fenced behind the explicit Owner authorization of section 19 for the exact frozen PR
> head and this exact reviewed procedure.
>
> **TWO INDEPENDENT SEQUENTIAL RUNS — NEVER SIMULTANEOUS.** This trial contains exactly two
> runs on WSB1 (Run A: G+210 → WJ1, axes A/B; Run B: G+214 → WJ2, axes C/D). Run B may not
> begin until Run A has reached its terminal result, completed Stop/Abort/Brake/cleanup, closed
> its active sessions and been explicitly restarted by the Owner (section 11). Each run consists
> of **four separately started fresh sessions** (section 5): representative Move, Stop evidence,
> Abort evidence, and the full P1→P6 path with Centre return.

## 1. Identity of this procedure

| Field | Value |
| --- | --- |
| Working label | **H1 — WSB1 Real Galil Dry Cleaning-Path Commissioning (two independent sequential runs)** |
| Branch | `arena/7968e33e-waterjet-sentinel-suite` |
| Pull request | PR #41 to `main` (unmerged; do not merge before Owner review) |
| Approved main ancestor | `f9e6e9adfef210f483fd3bfc7471936bbd7f34b2` (PR #40 merge) |
| H1 implementation commit | `c803335fc4957dfa2064d839328188d609f8046e` (parent `edb572543fe064fe16acb51d4e777ac87bc4cfa7`) |
| Software-validated Product head of record | `0f93542535df3160354e7e774e8b30cd48a6d4b5` (full Owner-local software validation of record; this procedure is a documentation-only descendant — the authorization of section 19 freezes the then-current exact PR head verbatim) |
| Build identity | `[RECORD AT T0 from the Owner-local build output; not committed]` |
| WSB1 controller identity | `[RECORD AT T0 through the existing approved identity session; the endpoint value itself is never recorded]` |

## 2. Locked two-run scope (Owner decision; no selection remains)

The Owner has selected and locked **both** WSB1 routes, their representative Move targets and
the Stop/Abort evidence method. There is no remaining Sensor choice, no alternative route, no
alternative representative target and no Effective-WJ override anywhere in this trial. Both
runs use `MC-WSB1` only; WSB2–WSB4 are excluded.

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
| Representative Move target | **G+210 P1** (Owner decision; section 4) |
| Cleaning Path (P1–P6) | the Owner-local **G+210** Cleaning Path only (Sensor-owned; section 2.4) |
| Centre (fixed contract) | Horizontal = Vertical = **125 mm = 1250000 native per axis** (committed Product authority `CleaningPathUnits.SafeReturnCentre`) |

Run A route chain, exactly:

G+210 → canonical WJ1 → no override → Effective WJ1 → IV1 (informational only) → WSB1 →
MC-WSB1 → Horizontal A → Vertical B → representative Move to G+210 P1 → G+210 P1→P6 →
Centre 125/125 → terminal cleanup and session/run close.

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
| Representative Move target | **G+214 P1** (Owner decision; section 4) |
| Cleaning Path (P1–P6) | the Owner-local **G+214** Cleaning Path only (Sensor-owned; section 2.4) |
| Centre (fixed contract) | Horizontal = Vertical = **125 mm = 1250000 native per axis** (committed Product authority `CleaningPathUnits.SafeReturnCentre`) |

Run B route chain, exactly:

G+214 → canonical WJ2 → no override → Effective WJ2 → IV2 (informational only) → WSB1 →
MC-WSB1 → Horizontal C → Vertical D → representative Move to G+214 P1 → G+214 P1→P6 →
Centre 125/125 → terminal cleanup and session/run close.

### 2.3 Two independent sequential runs

- The two routes execute as **two independent sequential runs**. They must **never** execute
  simultaneously; no simultaneous Water Jet operation and no overlapping run execution exists.
- Each run executes as **four separately started fresh sessions** (section 5): representative
  Move completion, Stop evidence, Abort evidence, and the full P1→P6 path with Centre return.
  No session is reused across those four purposes.
- Run A must reach its terminal state, complete Stop/Abort/Brake/cleanup as applicable, close
  its active sessions according to the accepted lifecycle, and leave **no active axis
  operation** before Run B may begin.
- **No state, Point ledger, primary failure, cleanup evidence or coordinate evidence may leak
  from one run into the other.** Each run records its evidence independently.
- Run A failure, unresolved Abort, Close or incomplete cleanup **blocks Run B** (section 11).
- There is **no automatic transition** — between runs and **between sessions**. The Owner
  explicitly starts each session after reviewing the prior terminal and cleanup evidence, and
  explicitly starts Run B after reviewing Run A terminal evidence.
- No Sensor other than G+210 and G+214, no WSB2–WSB4 route and no Effective-WJ override is
  permitted in this trial.

### 2.4 Sensor-owned path data

- Run A P1–P6 comes **only** from the Owner-local **G+210** Cleaning Path.
- Run B P1–P6 comes **only** from the Owner-local **G+214** Cleaning Path.
- The Effective-WJ resolution never changes either Sensor path.
- Coordinates are **not copied** between the Sensors.
- Actual coordinate values remain Owner-local and are recorded at T0 into each run's own
  evidence log; they are never committed.

## 3. Coordinate establishment — the exact accepted DP/DE contract (every motion-capable fresh session)

- **Home (per axis, independent, no synchronized Begin):** the typed Home lifecycle dispatches the
  commissioned Home sequence and completes through the Owner-approved **Home-carried reset-encoder
  composition**: after the supervised Stop and the inactive observation, the typed
  **position-zero definition (`DP<axis>=0`)**, then the typed **dual-zero definition
  (`DE<axis>=0`)**, as one contiguous native pair with no unrelated native command between them, and
  then its **zero verification by fresh position reads** whose observation evidence is strictly newer
  than the dual-zero definition (TP **and** RP within the configured tolerance).
  Only `HOME_COMPLETED_ZERO_VERIFIED` establishes the Move-capable coordinate state. Dispatch alone
  never establishes it; a refused position-zero definition prevents the dual-zero definition on that
  axis, and a refused dual-zero definition fails the Home closed with a typed code
  (`HOME_DUAL_ZERO_REFUSED`).
- **Home and coordinate establishment are repeated in every motion-capable fresh session.** The
  representative Move session, the Stop evidence session, the Abort evidence session and the
  full-path session of each run each begin with the full Home sequence of this section on that
  session's resolved axes, followed by verification that both axes are inactive with Brake
  applied, before any Move is requested.
- **Software mapping (Fast H1 Repair, Owner-approved 2026-10-26):** the Home sequence of each
  fresh session is started through the explicit **Commission / Home** action of the H1 surface,
  and every Move / path step of this procedure (T2, T3, T4, T5) is started through the separate
  **Run Path** action. **Run Path never dispatches Home**: it requires the session-scoped
  coordinate readiness (both resolved axes `Homed` with `ZeroVerified` **in that session**) and
  refuses with `H1_START_REFUSED_COORDINATE_READINESS_ABSENT`, directing the Operator to
  Commission/Home, when that readiness is absent. A new process or session always requires
  Commission/Home again. An **active Home input never proves coordinate readiness** and is never
  treated as a completed Home: a Home attempt against an already-active Home input is refused
  fail-closed (`HOME_INPUT_ALREADY_ACTIVE`, no native Home command dispatched) and the H1
  commissioning run reaches its typed terminal failure (`H1_FAILED_HOME` with the typed primary
  cause); it can no longer remain blocked in the Homing phase. Recovery is explicit: with the
  axes safely at rest and Brake applied, move the affected axis off the Home input through the
  existing safe manual means and start Commission/Home again — this repair deliberately adds no
  back-off motion and no automatic already-at-home acceptance.
- **Dual-zero definition (`DE=0`) inside a Home — REQUIRED by the Home lifecycle (Owner decision
  2026-10-26).** The H1 Home conditions and the both-reads zero-verification requirement are
  correct and unchanged; the defect they exposed was that the Home-carried zero establishment sent
  the position definition only. The exact Legacy Source proves the commissioned reset-encoder
  sequence is `DP<axis>=0` then `DE<axis>=0`, so the Home lifecycle now dispatches both, in that
  order, before verification. The sequence is profile-gated (`dualZeroPermitted`): a profile that
  does not permit the dual-zero definition keeps the position-only sequence, and the Owner-local
  commissioning profile must permit it for this procedure.
- **The standalone dual-zero definition remains a separate, optional, dispatch-only manual action.**
  Its effect stays **UNVERIFIED** (`DUAL_ZERO_DISPATCHED_EFFECT_UNVERIFIED`) and it still **clears
  the homed coordinate claim** (any later Move is then refused until a fresh Home). This procedure
  does not use it as a manual step; the encoder definition it needs is carried by Home.
- **The both-reads verification is an Owner-approved WJSS strengthening, not a Legacy behaviour.**
  The Legacy reset path takes no position reading at all; H1 keeps requiring fresh TP **and** RP
  within tolerance, and this is deliberately *not* weakened.
- **Hardware confirmation of the corrected sequence is still outstanding.** The sequence is proven
  at Source level and under deterministic simulation only. A separately authorized Real Galil H1
  re-trial on the exact corrected head is required before any physical confirmation is claimed.

## 4. Representative Move target — Owner decision (both runs)

- **Run A representative dual-axis Move target: G+210 P1.**
- **Run B representative dual-axis Move target: G+214 P1.**

Expected movement, per run:

- move from the verified post-Home coordinate state toward the run's Sensor P1;
- the Horizontal target is the run's Sensor P1 Horizontal coordinate from the Owner-local
  Cleaning Path store (Run A: G+210 P1 Horizontal; Run B: G+214 P1 Horizontal);
- the Vertical target is the run's Sensor P1 Vertical coordinate from the same store;
- the expected direction and displacement are calculated and displayed at T0 from
  **target minus current verified position** (in each motion-capable session the current
  verified position is that session's verified post-Home position);
- the actual coordinate values are never committed or invented, and coordinates are never
  copied from another Sensor.

Bounds and review:

- the representative trial is bounded by the commissioned target limits, the motion timeout
  and the Stop timeout;
- **P1 must be refused if it is outside the reviewed configured range**;
- the Owner must review the actual T0 P1 coordinates, the calculated directions and the
  calculated displacements **before enabling motion**;
- representative Move evidence does **not** allow the full path to start automatically.

## 5. Session structure — four separately started fresh sessions per run

Stop and Abort are tested as separate bounded trials before the full P1→P6 run, and every
trial uses a fresh session. The required session sequence for each route is:

1. **Representative Move completion session** (completed bounded Move to the run's Sensor P1).
2. **Stop evidence session** (software Stop during a fresh bounded Move toward the run's
   Sensor P1).
3. **Abort evidence session** (operator Abort during a different fresh bounded Move toward the
   run's Sensor P1).
4. **Full P1→P6 and Centre-return session.**

- **No session may be reused across those four purposes.**
- **No automatic transition is allowed.** The Owner explicitly starts each session after
  reviewing the prior terminal and cleanup evidence.
- Every session begins with the full start preconditions of section 9, a fresh identity and
  observation proof, and the Home sequence of section 3.

## 6. Configuration recorded at T0 (per run; values live outside Git)

Read from the Owner-local profile (`WJSS_MANUAL_MOTION_PROFILE`) and the run surface into **each
run's own evidence log** before that run's first Start. Recorded separately for Run A and for
Run B, with per-session re-reads where the section 9 preconditions require fresh observations.
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
| Dual-zero permission per axis | profile | `[RECORD — REQUIRED for both runs: the Home-carried reset-encoder composition needs it]` |
| Move target bounds | profile | `[RECORD]` |

## 7. Stop and Abort evidence method — Owner decision (same method for both routes)

The Owner has fixed **one method, applied independently to A/B and C/D**: Stop and Abort are
tested as separate bounded trials before the full P1→P6 run, each in a fresh session
(section 5), each during a bounded dual-axis Move toward the run's locked Sensor P1.

- **Software Stop (typed per-axis Stop):** issued only after current/fresh **motion-active
  evidence** is observed on at least one resolved axis and before representative-target
  completion; supervise every active resolved axis to inactive; each active axis receives
  Stop **no more than once**; Brake applied after inactive; the Stop outcome and cleanup are
  recorded separately.
- **Operator Abort (the H1 operator surface Abort control):** issued under the same
  motion-active-evidence condition; preserves the typed Owner-abort cause; blocks all later
  motion and all Point advancement; stops every remaining active resolved axis **exactly
  once**; supervises to inactive; applies Brake after inactive; the primary Abort cause is
  recorded separately from cleanup.
- **Physical abort:** remains continuously accessible and remains the **authoritative
  emergency means**, but it is **not intentionally triggered** as the software Stop/Abort
  trial method.

### 7.1 Stop / Abort timing safety (explicit)

- Stop or Abort is triggered by **current/fresh motion-active evidence, not by an assumed
  elapsed time**.
- **No deliberate command is issued if motion has already completed.**
- **No target is extended beyond the locked Sensor P1** merely to create a longer interruption
  window.
- **No speed, acceleration, deceleration or timeout is changed** merely to make Stop/Abort
  easier.
- If P1 is too close for a reliable active-motion interruption window, the trial must **STOP
  and return to the Owner**.
- **Do not choose P2–P6, Centre, another coordinate or another Sensor** as an alternative
  target without separate Owner authorization.
- Physical abort remains continuously accessible but is not intentionally triggered as the
  software Stop/Abort trial method; physical abort remains the authoritative emergency means.

## 8. Expected command families (typed plan; per run)

Exactly the closed typed Manual Motion plan may reach the controller, one axis per command:
brake output set/clear; Home speed + Home + Begin; AC / SP / DC / PA + Begin (Move); the typed
per-axis Stop; DP=0 (the Home completion branch).

- **Run A issues these families on axes A and B only** (brake outputs 1 and 2).
- **Run B issues these families on axes C and D only** (brake outputs 3 and 4).
- **No A/B command may occur during Run B. No C/D command may occur during Run A.** Any
  unrelated axis or output command is an immediate STOP condition.
- DE=0 is dispatched only as the second command of the Home-carried reset-encoder composition
  (section 3). No standalone DE=0 step exists in these routes.
- **No raw command input exists on the surface; no raw Galil command is authorized.** Any
  unexpected command family, any command on an unrelated axis, or any Pump/Valve/output command
  is a STOP condition.

## 9. Exact start preconditions (per session; any missing, stale, invalid or contradictory = do not start)

1. WSB1 controller identity proven for this session (this session's T0 evidence).
2. Owner-local endpoint configured (`WJSS_MOTION_CONTROLLER_01_ENDPOINT`, hardware mode; value
   never displayed or recorded) and Owner-local motion profile configured
   (`WJSS_MANUAL_MOTION_PROFILE`).
3. Canonical route matches the run's locked identity (resolver + canonical Motion topology):
   Run A — G+210 → canonical WJ1 → no override → Effective WJ1, provenance
   `CANONICAL_ASSIGNMENT`, paired IV1 informational only; Run B — G+214 → canonical WJ2 → no
   override → Effective WJ2, provenance `CANONICAL_ASSIGNMENT`, paired IV2 informational only.
4. Only the run's resolved axis pair will operate (the session requests no other axis).
5. Current and fresh Status, TP, RP and Brake-output observations on the run's resolved axes,
   **reacquired for this session** — prior-session observations are never current evidence.
6. No axis operation active; no previous session of this trial active; the prior session (if
   any) reached its terminal result, completed cleanup and closed.
7. Dry mode declared; no Pump command path; no physical Isolation Valve command path; no
   Queue / AutoSequence / next-job path connected.
8. Physical abort accessible; Owner present as Operator/Observer.
9. Owner-local Cleaning Path store (`WJSS_CLEANING_PATH_STORE`) and sensor-parameter CSV
   (`WJSS_SENSOR_PARAMETER_CSV`) loaded and accepted (fail-closed; read once; never written);
   the run's own Sensor Cleaning Path is the loaded path of record.
10. Exact frozen PR head of section 19 confirmed.
11. The Owner has explicitly started this session after reviewing the prior session's terminal
    and cleanup evidence (no automatic transition; section 5).
12. For every Run Path start of this procedure (T2, T3, T4, T5): the session-scoped coordinate
    readiness of section 3 is present — both resolved axes `Homed` with `ZeroVerified` in this
    session through the explicit Commission/Home action. Run Path never Homes automatically; a
    readiness refusal is resolved by Commission/Home, never by bypassing it.

**Run B sessions additionally:** the inter-run gate of section 11 is fully satisfied.

## 10. Trial phases — Run A (G+210 / WJ1 / A-B)

### H1-A-T0 — Identity, profile, P1 and initial-state review (Run A)
- Connect through the existing approved boundary (`h1-wsb1-hardware` after the consent/Gate-A/
  endpoint chain; no motion).
- Verify controller identity (existing identity session evidence); record the redacted status only.
- Collect current Status / TP / RP / Brake state on axes A and B; verify no motion; verify
  physical abort access.
- Record every section 6 value into the Run A evidence log.
- Record the **G+210 P1 target coordinates** and the calculated direction/displacement
  (**target minus current verified position**) for Owner review before enabling motion
  (section 4).
- Perform **no movement** if any precondition is uncertain.

### H1-A-T1 — Home and coordinate establishment (Run A; repeated in every motion-capable fresh session)
- Home axes A (Horizontal) and B (Vertical) through independent lifecycles (Horizontal requested
  first; no synchronized Begin).
- Complete the Home-carried **DP=0** and its **zero verification**: each axis independently
  `HOME_COMPLETED_ZERO_VERIFIED`, TP and RP zero within tolerance.
- Verify both axes inactive with Brake applied after inactive.
- Record the DP/DE disposition of section 3 (expected: Home-carried `DP=0` then `DE=0`
  dispatched, in that order, with the both-reads zero verification passing afterwards).
- This sequence is repeated at the start of the H1-A-T2, H1-A-T3, H1-A-T4 and H1-A-T5 sessions.

### H1-A-T2 — Completed bounded representative Move to G+210 P1 (fresh session)
- Fresh session, started explicitly by the Owner after H1-A-T0 review; full section 9
  preconditions; section 3 Home sequence on A and B (H1-A-T1).
- Request the bounded dual-axis Move toward **G+210 P1** (Horizontal and Vertical targets from
  the Owner-local G+210 Cleaning Path store; section 4 bounds and review).
- Verify both axes independently (typed completion + fresh inactive Status + TP + RP in
  tolerance + Brake after inactive).
- Record the representative Move evidence. **Representative Move evidence does not allow the
  full path to start automatically**; the Owner explicitly starts the next session.
- Close the session through the typed boundary.

### H1-A-T3 — Software Stop evidence during a fresh bounded Move toward G+210 P1
- Start from a **fresh session** (Owner-started after the H1-A-T2 terminal and cleanup
  evidence); re-prove G+210 / WJ1 / IV1 informational / WSB1 / MC-WSB1 / A/B.
- Reacquire current Status, TP, RP and Brake-output observations.
- Home A and B; complete Home-carried DP=0 and zero verification; verify both axes inactive
  and Brake applied.
- Request the bounded dual-axis Move toward **G+210 P1**.
- **After current/fresh motion-active evidence is observed on at least one resolved axis, and
  before representative-target completion, issue the typed software Stop.** Do not use a fixed
  blind delay as the authority to issue Stop.
- Supervise every active resolved axis to inactive; each active axis receives Stop **no more
  than once**; apply Brake after inactive.
- Record the Stop outcome and cleanup separately; close the session.
- **Do not continue if a usable active-motion observation window does not exist** (section 7.1).
- **No C/D command may occur during Run A.**

### H1-A-T4 — Operator Abort evidence during a different fresh bounded Move toward G+210 P1
- Start a **separate fresh session** after the H1-A-T3 Stop session has terminal cleanup and is
  closed; re-prove the complete Run A identity and current observations.
- Home A and B; complete Home-carried DP=0 and zero verification.
- Request the bounded dual-axis Move toward **G+210 P1**.
- **After current/fresh motion-active evidence is observed on at least one resolved axis, and
  before representative-target completion, use the H1 operator surface Abort control.** Do not
  use a fixed blind delay as the authority to issue Abort.
- Preserve the typed Owner-abort cause; block all later motion and all Point advancement.
- Stop every remaining active resolved axis **exactly once**; supervise to inactive; apply
  Brake after inactive.
- Record the primary Abort cause separately from cleanup; close the session.
- **Do not start P1→P6 in the same session.**
- **No C/D command may occur during Run A.**

### H1-A-T5 — Fresh-session full G+210 P1→P6 path and Centre return
- Only after the H1-A-T2 representative Move, the H1-A-T3 Stop evidence and the H1-A-T4 Abort
  evidence have **each completed successfully in separate fresh sessions** may the Owner start
  this session.
- Fresh session; full initial identity and observation proof; Home A/B; Home-carried DP=0;
  zero verification (section 3).
- Execute the strict ordered path **P1 → P2 → P3 → P4 → P5 → P6** from the **G+210** Cleaning
  Path only (Horizontal then Vertical request per point; fair parallel-independent dispatch;
  no skip, no repeat).
- **Both axes (A and B) must be verified at every Point** before the next Point starts.
- After dual-verified P6 only: return axis A to 125 mm and axis B to 125 mm through the same
  typed Move lifecycle (Centre 125/125; 1250000 native per axis).
- Verify fresh inactive + TP + RP for both axes; Brake after inactive. The dry Safe Return
  completion is published only after both axes verify — a typed H1 dry result, never a Pump,
  Valve, physical Cleaning Job or Production completion.
- Complete Brake and cleanup; close the session.

### Run A terminal review and complete inter-run cleanup gate
- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- All Run A sessions closed through the existing typed boundary; **no active operation
  remains on A or B**.
- Record the Run A terminal result and all Run A evidence independently (section 15).
- No fabricated Safe Return success under any failure shape.
- The Owner reviews the Run A terminal evidence and the section 11 inter-run gate before any
  Run B session may start.

## 11. Inter-run gate — Run A terminal review (blocks Run B)

There is no automatic transition. After Run A reaches its terminal result, the Owner reviews
the Run A terminal evidence and then explicitly starts Run B. **Run B must be refused if any of
the following is true after Run A:**

- axis A or axis B remains active;
- Stop remains unsupervised;
- Brake state is uncertain or not accepted;
- cleanup is incomplete;
- session resources are not closed or reset according to the accepted lifecycle;
- Run A ended with unresolved failure (a planned H1-A-T3 Stop or H1-A-T4 Abort evidence session
  that reached its terminal result with complete cleanup and a closed session is a **completed
  evidence session, not an unresolved failure**; an Abort whose cleanup did not complete or
  whose session did not close is an unresolved failure);
- current controller observations have not been reacquired;
- the Owner does not explicitly authorize starting Run B.

No state, Point ledger, primary failure, cleanup evidence or coordinate evidence from Run A may
be carried into Run B as Run B evidence.

## 12. Trial phases — Run B (G+214 / WJ2 / C-D)

### H1-B-T0 — Identity, profile, P1 and initial-state review (Run B)
- Connect through the existing approved boundary (`h1-wsb1-hardware` after the consent/Gate-A/
  endpoint chain; no motion).
- Verify controller identity (existing identity session evidence); record the redacted status only.
- **Re-read and re-prove current controller observations**: collect fresh Status / TP / RP /
  Brake state on axes C and D; verify no motion; verify physical abort access. Run A
  observations are not current evidence for Run B.
- Confirm the section 11 inter-run gate is satisfied.
- Record every section 6 value and the **G+214 P1 target coordinates** with calculated
  direction/displacement (target minus current verified position) into the Run B evidence log
  for Owner review before enabling motion (section 4).
- Perform **no movement** if any precondition is uncertain.

### H1-B-T1 — Home and coordinate establishment (Run B; repeated in every motion-capable fresh session)
- Home axes C (Horizontal) and D (Vertical) through independent lifecycles (Horizontal requested
  first; no synchronized Begin).
- Complete the Home-carried **DP=0** and its **zero verification**: each axis independently
  `HOME_COMPLETED_ZERO_VERIFIED`, TP and RP zero within tolerance.
- Verify both axes inactive with Brake applied after inactive.
- Record the DP/DE disposition of section 3 (expected: Home-carried `DP=0` then `DE=0`
  dispatched, in that order, with the both-reads zero verification passing afterwards).
- This sequence is repeated at the start of the H1-B-T2, H1-B-T3, H1-B-T4 and H1-B-T5 sessions.

### H1-B-T2 — Completed bounded representative Move to G+214 P1 (fresh session)
- Fresh session, started explicitly by the Owner after H1-B-T0 review; full section 9
  preconditions (including the section 11 inter-run gate); section 3 Home sequence on C and D
  (H1-B-T1).
- Request the bounded dual-axis Move toward **G+214 P1** (Horizontal and Vertical targets from
  the Owner-local G+214 Cleaning Path store; section 4 bounds and review).
- Verify both axes independently (typed completion + fresh inactive Status + TP + RP in
  tolerance + Brake after inactive).
- Record the representative Move evidence. **Representative Move evidence does not allow the
  full path to start automatically**; the Owner explicitly starts the next session.
- Close the session through the typed boundary.

### H1-B-T3 — Software Stop evidence during a fresh bounded Move toward G+214 P1
- Start from a **fresh session** (Owner-started after the H1-B-T2 terminal and cleanup
  evidence); re-prove G+214 / WJ2 / IV2 informational / WSB1 / MC-WSB1 / C/D.
- Reacquire current Status, TP, RP and Brake-output observations.
- Home C and D; complete Home-carried DP=0 and zero verification; verify both axes inactive
  and Brake applied.
- Request the bounded dual-axis Move toward **G+214 P1**.
- **After current/fresh motion-active evidence is observed on at least one resolved axis, and
  before representative-target completion, issue the typed software Stop.** Do not use a fixed
  blind delay as the authority to issue Stop.
- Supervise every active resolved axis to inactive; each active axis receives Stop **no more
  than once**; apply Brake after inactive.
- Record the Stop outcome and cleanup separately; close the session.
- **Do not continue if a usable active-motion observation window does not exist** (section 7.1).
- **No A/B command may occur during Run B.**

### H1-B-T4 — Operator Abort evidence during a different fresh bounded Move toward G+214 P1
- Start a **separate fresh session** after the H1-B-T3 Stop session has terminal cleanup and is
  closed; re-prove the complete Run B identity and current observations.
- Home C and D; complete Home-carried DP=0 and zero verification.
- Request the bounded dual-axis Move toward **G+214 P1**.
- **After current/fresh motion-active evidence is observed on at least one resolved axis, and
  before representative-target completion, use the H1 operator surface Abort control.** Do not
  use a fixed blind delay as the authority to issue Abort.
- Preserve the typed Owner-abort cause; block all later motion and all Point advancement.
- Stop every remaining active resolved axis **exactly once**; supervise to inactive; apply
  Brake after inactive.
- Record the primary Abort cause separately from cleanup; close the session.
- **Do not start P1→P6 in the same session.**
- **No A/B command may occur during Run B.**

### H1-B-T5 — Fresh-session full G+214 P1→P6 path and Centre return
- Only after the H1-B-T2 representative Move, the H1-B-T3 Stop evidence and the H1-B-T4 Abort
  evidence have **each completed successfully in separate fresh sessions** may the Owner start
  this session.
- Fresh session; full initial identity and observation proof; Home C/D; Home-carried DP=0;
  zero verification (section 3).
- Execute the strict ordered path **P1 → P2 → P3 → P4 → P5 → P6** from the **G+214** Cleaning
  Path only (Horizontal then Vertical request per point; fair parallel-independent dispatch;
  no skip, no repeat).
- **Both axes (C and D) must be verified at every Point** before the next Point starts.
- After dual-verified P6 only: return axis C to 125 mm and axis D to 125 mm through the same
  typed Move lifecycle (Centre 125/125; 1250000 native per axis).
- Verify fresh inactive + TP + RP for both axes; Brake after inactive. The dry Safe Return
  completion is published only after both axes verify — a typed H1 dry result, never a Pump,
  Valve, physical Cleaning Job or Production completion.
- Complete Brake and cleanup; close the session.

### Run B terminal review and final closeout
- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- All Run B sessions closed through the existing typed boundary; **no active operation
  remains on C or D**.
- Record the Run B terminal result and all Run B evidence independently (section 15).
- No fabricated Safe Return success under any failure shape.

## 13. Final combined evidence closeout

- The trial closes with **two independent evidence sets** — Run A and Run B — each complete per
  section 15, recorded separately, with no shared or leaked state, Point ledger, primary
  failure, cleanup evidence or coordinate evidence.
- The combined closeout records: both terminal results, both Centre records, the per-session
  evidence chain of section 5, the inter-run gate outcome of section 11, and the confirmation
  that no simultaneous execution and no cross-run axis command occurred.
- Only the approved summary leaves the Owner-local environment; nothing is committed beyond it.

## 14. Exact STOP conditions (any session of either run; abort immediately; do not continue)

- Controller identity differs; WSB/axis route differs; selected Sensor or coordinates differ.
- Hardware, wiring or mechanical arrangement changed from the accepted evidence.
- Endpoint/profile differs from the reviewed configuration.
- Observation is stale, invalid or missing; Status/TP/RP conflict.
- Unexpected axis moves; direction unexpected; motion exceeds the reviewed bound.
- Brake state uncertain; limit input unexpected.
- Point order differs; either axis fails verification.
- Stop/Abort does not behave exactly as reviewed; cleanup incomplete.
- **No usable active-motion observation window exists for the Stop or Abort evidence session,
  or P1 is too close for a reliable active-motion interruption window (section 7.1 — STOP and
  return to the Owner).**
- **Any attempt to extend the target beyond the locked Sensor P1, to select P2–P6, Centre,
  another coordinate or another Sensor as an alternative target, or to change speed,
  acceleration, deceleration or timeouts to make Stop/Abort easier.**
- **A Stop or Abort command is issued from an assumed elapsed time rather than current/fresh
  motion-active evidence, or a deliberate command is issued after motion has already
  completed.**
- **The full P1→P6 path is started in a Stop or Abort evidence session.**
- Any Pump or Isolation Valve command appears; any unrelated axis/output command appears.
- **Any A/B command occurs during Run B. Any C/D command occurs during Run A.**
- **The section 11 inter-run gate is not fully satisfied before a Run B session.**
- **Run A observations are relied on as current evidence for Run B.**
- **Any Sensor other than G+210 / G+214, any WSB2–WSB4 route, any Effective-WJ override or any
  simultaneous execution is attempted.**
- Physical visibility or abort access is lost; Owner requests Stop.
- Source/head identity changes.

## 15. Exact evidence to capture (per run, per session; kept outside Git except the approved summary)

Each run records its own complete evidence set, independently, with per-session evidence:

- identity confirmation (selected Sensor; canonical/default WJ; Effective WJ; provenance;
  informational paired IV; controller; axis pair and roles);
- initial Status, initial TP, initial RP, Brake outputs and observed states — per session,
  freshly reacquired;
- freshness proof (observation interval and freshness window) and no-active-operation proof;
- Home evidence (per axis, typed lifecycle, per session);
- DP=0 and zero-verification evidence (per axis, per session);
- representative Move evidence (per axis; target = the run's Sensor P1; calculated
  direction/displacement at T0);
- Stop evidence session record (motion-active trigger evidence, per-axis Stop supervision,
  exactly-once proof, Brake, outcome);
- Abort evidence session record (motion-active trigger evidence, typed Owner-abort cause,
  exactly-once Stop of remaining active axes, Brake, primary cause separate from cleanup);
- P1→P6 point ledger (per axis per point, from that run's own Sensor path);
- Centre-return evidence (Horizontal 125 mm, Vertical 125 mm, 1250000 native per axis);
- Stop/Abort/Brake/cleanup evidence;
- terminal result (terminal phase, code, primary failure if any);
- configuration of section 6 (observation interval; freshness window; Home timeout; Move
  timeout; Stop timeout; position tolerance; Home speed; Move acceleration/speed/deceleration;
  target bounds), physical-abort availability, and operator/observer confirmation.

Never capture or commit: endpoints, credentials, consent values, raw library paths or hashes,
actual G+210 P1–P6 values, actual G+214 P1–P6 values, profile parameter values or any other
Owner-local configuration value.

## 16. Exact cleanup (per session; per run)

- Every completed axis: Brake applied after inactive (typed lifecycle).
- On failure or abort: each remaining active axis stopped exactly once, supervised to inactive,
  Brake applied after inactive; cleanup recorded separately from the primary failure.
- Close the session through the existing typed boundary at the end of every session; a session
  is never reused across the four purposes of section 5.
- No fabricated Safe Return success under any failure shape.

## 17. Explicit scope statement

Both Run A and Run B perform **dry motion only**: Home, coordinate establishment (DP=0 through
the Home lifecycle), position-zero verification, bounded representative dual-axis Move to the
run's Sensor P1, software Stop evidence, operator Abort evidence, the run's own selected-Sensor
P1→P6, Centre return to 125 mm / 125 mm, Brake lifecycle and cleanup — each in the separate
fresh sessions of section 5.

Both routes exclude: a **standalone DE=0** manual step (the Home-carried composition of section 3
is required and is not a standalone step); Pump; physical Isolation Valve actuation; water; Queue;
AutoSequence; next-job countdown; Production; WSB2–WSB4; Effective-WJ overrides; any Sensor
other than G+210 and G+214; simultaneous execution; **coordinated trajectory**;
**synchronized Begin**; **raw Galil commands**.

IV1 and IV2 are **informational identities only**. No physical IV command path may be available
or exercised. Neither run performs **any Pump command, any physical Isolation Valve command,
any water flow, any Queue/AutoSequence, any Production behavior**, and neither publishes
**any** Pump/Valve/Job/Production completion of any kind.

## 18. Resolved Owner decisions (former execution blockers)

The three former execution blockers are **resolved by the Owner decisions recorded in this
procedure**; no unresolved execution blocker remains in this procedure:

- **Former blocker A1 — RESOLVED (section 4):** the Run A representative dual-axis Move target
  is **G+210 P1**, with direction/displacement calculated and displayed at T0 from target minus
  current verified position; actual values remain Owner-local.
- **Former blocker A2 — RESOLVED (section 4):** the Run B representative dual-axis Move target
  is **G+214 P1**, calculated identically; actual values remain Owner-local.
- **Former blocker B — RESOLVED (section 7):** the Stop/Abort evidence method is fixed as one
  method for both routes — separate bounded Stop and Abort trials before the full P1→P6 run,
  each in a fresh session, triggered by current/fresh motion-active evidence, never by blind
  delay; physical abort remains the authoritative emergency means and is not intentionally
  triggered as the trial method.

The T0 Owner-local values (P1 coordinates, directions, displacements and section 6
configuration) are recorded at T0 on the Owner-local machine and reviewed by the Owner before
enabling motion; they are not execution blockers of this procedure.

- **Fast H1 Repair — RESOLVED (Owner-approved 2026-10-26):** Commission/Home and Run Path are
  two explicitly separated H1 commissioning actions. Run Path never dispatches Home
  automatically; it consumes the existing session-scoped coordinate readiness (both resolved
  axes `Homed` + `ZeroVerified`) and refuses with the typed reason
  `H1_START_REFUSED_COORDINATE_READINESS_ABSENT` directing the Operator to Commission/Home when
  that readiness is absent. The `HOME_INPUT_ALREADY_ACTIVE` permanent Homing block is repaired:
  every applied Home request outcome is detected by per-request session-log correlation, so an
  H1 Home attempt always reaches a typed terminal failure and can no longer remain in Homing
  indefinitely. No back-off motion, no automatic already-at-home acceptance and no Production /
  Runtime / Queue / AutoSequence integration is added. The software change is a descendant of
  `7910c09792789aa7809e985f3f38c044a5d0892d` on PR #41; dated history is preserved.

## 19. Hardware execution gate and required authorization

Do not execute any H1-A or H1-B session until: implementation is complete; final Owner-local
software validation passes on the exact head (`0f93542535df3160354e7e774e8b30cd48a6d4b5` is
the software-validated Product head of record; this procedure is a documentation-only
descendant; the Fast H1 Repair of section 18 requires its own Owner-local validation and a
**new** authorization on the new exact PR head — an authorization frozen on any earlier head
does not cover it); this procedure is delivered and reviewed; the Owner has reviewed the actual
T0 P1 coordinates, calculated directions and displacements for **both** routes; the physical
area is safe with continuous visibility and physical abort access; and the Owner explicitly
authorizes the exact head + this procedure + both locked runs. No Source or configuration
change may occur afterward.

Required authorization wording (equivalent):

```text
AUTHORIZE H1 REAL GALIL DRY TRIAL
ON EXACT PR HEAD <SHA>
USING THE REVIEWED H1 TRIAL PROCEDURE
```

That wording authorizes the reviewed two-run procedure as a whole; every one of the eight
sessions (four per run) still requires its own explicit Owner start after the prior terminal
and cleanup review, and Run B still requires the separate explicit Owner start of section 11
after the Run A terminal review. This document is not that authorization.
