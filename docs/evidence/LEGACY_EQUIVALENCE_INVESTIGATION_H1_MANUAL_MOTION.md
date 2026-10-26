# Legacy-Equivalence Investigation — H1 Dry Cleaning Path and Manual Motion

**Document class:** EVIDENCE AND INVESTIGATION RECORD. It records one determination and the
evidence behind it. It authorizes no hardware operation, no Product surface, no command and no
implementation. `docs/MASTER_PLAN.md` remains the roadmap of record;
`docs/MOTION_COMMISSIONING_FOUNDATION.md` remains the commissioning authority;
`docs/SAFETY_BOUNDARY.md` remains the safety boundary. Nothing here introduces a stage or a gate.

**Gate position (unchanged).** The Motion authorization gate stays CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`); the axes stay `NOT_COMMISSIONED`; real commissioning
values stay `NOT_CONFIGURED`.

**Hardware position.** No controller was contacted, opened, read, configured or commanded while
producing this document. No native library was loaded. No endpoint, address, coordinate, speed,
acceleration, delay value or vendor error text is reproduced here. The Legacy source pack is not
committed and no Legacy source line is copied into this repository: every statement below is a
paraphrase of behaviour, and all Legacy numeric constants are referred to by name only.

**Question put to this investigation.** Whether the absence of a dual-zero definition
(`DE<axis>=0`) after a position definition (`DP<axis>=0`) in WJSS is the cause of any observed
H1 or Manual Motion divergence from the Legacy application, and whether H1 must therefore
reproduce the Legacy reset-encoder sequence.

**Owner decision (supersedes the §4 determination of the first pass).** The Owner has ruled that
the H1 Home conditions and the TP/RP zero-verification requirement are correct and must remain,
that the missing dual-zero definition inside the Home-carried zero establishment is the defect,
and that H1 must use the complete Legacy reset-encoder sequence after a successful Home. The
first-pass determination recorded in §4 is retained verbatim as dated history and is explicitly
superseded; §4.5 records the corrected classification and §8 the correction that was made. **A
Hardware re-trial on the corrected exact head is still required before any physical confirmation
is claimed.**

---

## 1. Evidence of record and pack integrity

### 1.1 The two packs

| Item | Previously recorded baseline | This investigation |
| --- | --- | --- |
| Exact filename | `repomix-output.md` | `repomix-output-exact-lagacy-source-pack.md` |
| Size | 662,922 bytes | 3,362,177 bytes |
| Lines | 10,834 | 57,439 |
| File blocks | 7 | 35 |
| Reference | `LEGACY_MOTION_COMMISSIONING_BASELINE.md` §A.1 | this document §1.2 |

This investigation was performed against the larger pack, which is the exact Legacy Source pack
for the current Owner question. It is **not** a recreation of the earlier evidence package and it
does not replace it: `LEGACY_MOTION_COMMISSIONING_BASELINE.md` and
`MANUAL_MOTION_LEGACY_CONTINUITY.md` remain the evidence of record for their own checkpoints and
are not amended here.

### 1.2 Integrity checks performed

- the pack was retrieved once, read-only, through the connected Google Drive connector, by exact
  filename, after a single-name search returned exactly one match;
- it was written by the connector into the workspace and was moved out of the repository working
  tree before it was read, so it was never tracked, staged or committable — `git status
  --porcelain` returned empty immediately after the move;
- 35 `## File:` headers resolve to 35 fenced blocks; 72 fence lines; the final block is the
  solution file and the document ends with its closing fence — no truncation marker was found;
- the seven files recorded in the previous baseline appear in this pack with identical line
  counts (1,988 / 3,064 / 1,324 / 3,512 / 282 / 430 / 160), so this pack is a strict superset of
  the same source, not a different revision;
- the 28 additional files include the application's main form, the water-cannon/pump form, sensor
  forms, persisted configuration files and project files. They were read **only** to establish
  whether any H1-equivalent position-definition, encoder-definition or reset-encoder call site
  exists outside the two reviewed implementation files. No production, pump, valve, water-cannon,
  queue or sensor behaviour is analysed, promoted or proposed here;
- local copies of the extracted pack were held outside the repository and are not committed.

### 1.3 Handling of the additional files

The additional files contain the prohibited categories of
[`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — persisted coordinates,
connection endpoints and motion configuration. Nothing from them is quoted, counted, sampled or
summarised into this repository. Their only use was a presence/absence search described in §2.

---

## 2. Complete Legacy command inventory for the investigated paths

Every non-commented controller command in the whole pack was enumerated. The complete set is:
acceleration, speed and deceleration loads; absolute target; jog speed; begin; home speed; home
search; position definition; encoder definition; stop; output set; output clear; an input read; a
status read; an output read; a controller time read; and an unconditional output-bank clear in the
connection path.

Within the two paths under investigation, the relevant call sites are exactly these:

| # | Sequence | Site (paraphrased) | Live? |
| --- | --- | --- | --- |
| 1 | `DP<axis>=0` then `DE<axis>=0`, both awaiting a reply | the reset-encoder helper | live |
| 2 | `ST <axis>` then a fixed wait then `DP<axis>=0` | the feedback loop's home-complete branch | live |
| 3 | `DP<axis>=0` inside the home sequence | the home command helper | **commented out** (dead) |
| 4 | `DP <axis>=0`, `ST <axis>` etc., with a space in the command text | private helpers beside the feedback loop | live, used by #2 |

Three consequences follow immediately and are the backbone of the whole determination:

1. A position definition and an encoder definition appear together in **exactly one** place in the
   entire Legacy application — the reset-encoder helper. Nothing else in the pack sends the
   encoder definition.
2. The Legacy **home completion** sends a position definition and **never** an encoder definition.
   The position definition that was once part of the home command sequence is commented out.
3. The Legacy **cleaning-path execution** sends neither a home sequence nor a position definition
   nor an encoder definition at any point. It only dispatches absolute moves.

The unconditional output-bank clear in the connection path is recorded here only to confirm that it
remains outside this question and outside every WJSS surface.

---

## 3. Function-by-function comparison

Classification vocabulary for a **difference**:

- **EQUIVALENT** — same command shape, same order, same trigger; no material difference.
- **DELIBERATE STRENGTHENING** — WJSS adds a bounded safety or evidence property the Legacy
  application did not have. Already recorded as such in
  [`MANUAL_MOTION_LEGACY_CONTINUITY.md`](MANUAL_MOTION_LEGACY_CONTINUITY.md) §3.
- **DELIBERATE NON-PORT** — an approved decision not to reproduce Legacy structure or behaviour.
- **LEGACY-EQUIVALENCE GAP** — a Legacy behaviour that has no WJSS counterpart; whether it matters
  is not established.
- **NOT VERIFIED** — the difference cannot be resolved from committed evidence.

Vocabulary for the **cause question** is the Owner's five-way set, used in §4.

### 3.1 Reset encoder (the subject of this investigation)

| Field | Legacy | WJSS |
| --- | --- | --- |
| Surface | one Operator button per mapped axis, in the manual panel | **no reset-encoder surface exists** |
| Preceding action | none — no brake release, no stop, no settle | n/a |
| Command sequence | position definition, then encoder definition, one axis | two **separate** Operator actions: define position zero (position definition only), and define dual zero (encoder definition only) |
| Reply handling | each command awaits a reply through the shared dispatch path; the shared path pre-probes liveness before each command | each is one typed dispatch through the closed plan; no liveness pre-probe exists |
| Inter-command delay | none | none within each action |
| Post-command verification | none — no position read is taken after either command | position definition verifies fresh TP **and** RP against zero within the configured tolerance; encoder definition is dispatch-only and reported as effect-unverified |
| Failure handling | one generic message; the exception propagates to the caller | typed refusal codes; no vendor text; session state preserved |
| Effect on the coordinate claim | not modelled | both standalone actions clear the session's Home claim |
| Where the sequence now runs | the Operator reset button only | the Home lifecycle of the Manual Motion session carries the same pair, in the Legacy order, when the Owner-local profile permits the dual-zero definition; the standalone actions remain available and unchanged |
| Classification | **RESOLVED by the authorized correction** — the Legacy command pair is now composed into the WJSS Home lifecycle as one contiguous native pair, `DP<axis>=0` then `DE<axis>=0`, with the Owner-approved fresh TP/RP verification as an additive strengthening. No standalone WJSS action claims reset-encoder equivalence, and none is added. | |

### 3.2 Home

| Field | Legacy | WJSS |
| --- | --- | --- |
| Trigger | Operator Home button, per axis | typed per-axis Home request; never dispatched by the Run Path action |
| Sequence | brake release, fixed wait, home speed, home search, begin | brake release, configured settle, home speed, home search, begin — one command per step |
| Brake | released by the helper before the sequence | released before the sequence; polarity from the Owner-local profile |
| Completion detection | **only** the poll loop seeing the home bit; no timeout, no resolution | the profile-decoded home bit observed after this operation's begin, bounded by the configured Home timeout |
| On completion | stop, fixed wait, position definition, clear the homing flag | supervised stop, wait for an observation that the axis is inactive, position definition, **then the encoder definition** (the Legacy Reset Encoder pair), fresh TP/RP zero verification, brake apply |
| Encoder definition | **never in the Home handler** — the Legacy Application sends it only from its separate reset-encoder action | **carried by the Home lifecycle** as the second half of the Owner-approved composition, which is why the two Legacy operations must be distinguished |
| Failure path | none — the axis stays in the homing state; no brake re-apply | bounded timeout, `.HomeFailed`, brake apply after an observed stop |
| Already-active home input | no such check; the sequence is dispatched regardless | `HOME_INPUT_ALREADY_ACTIVE` refusal before any native Home dispatch, fail-closed, bounded by observation freshness |
| Classification | Home **motion** order is **EQUIVALENT**; completion, timing, bounds and failure are **DELIBERATE STRENGTHENING**; the completion command pair is the Legacy Reset Encoder sequence composed in, which is a deliberate composition of two Legacy operations rather than a Legacy Home behaviour; the both-reads verification is an **Owner-approved strengthening** with no Legacy counterpart. | |

### 3.3 Jog

| Field | Legacy | WJSS |
| --- | --- | --- |
| Sequence | brake release, fixed wait, signed jog speed, begin | brake release, configured settle, signed jog speed, begin |
| Sign | the direction token selects the sign | the typed direction selects the sign |
| Hold-to-run | none — motion continues until an explicit Stop | press identity, heartbeat, capture/focus loss, maximum hold, supervised Stop |
| Brake apply at end | none | applied after the axis is observed inactive |
| Classification | command shape **EQUIVALENT**; lifecycle **DELIBERATE STRENGTHENING**. | |

### 3.4 Stop

| Field | Legacy | WJSS |
| --- | --- | --- |
| Shape | stop with the axis letter after a single space | stop with the axis letter after a single space |
| Scope | per mapped axis | per axis; touches no other axis |
| Confirmation | none | dispatch only; a stopped state is never fabricated; a timeout does not claim a brake application |
| Retry | caller may re-issue freely | one dispatch per lifecycle, protective Stop exactly once per may-have-begun operation |
| Classification | **EQUIVALENT** in shape; **DELIBERATE STRENGTHENING** in confirmation and retry discipline. | |

### 3.5 Absolute move

| Field | Legacy | WJSS |
| --- | --- | --- |
| Sequence | brake release, fixed wait, acceleration, speed, deceleration, absolute target, begin | brake release, configured settle, acceleration, speed, deceleration, absolute target, begin |
| Redundant brake release | **twice** — the action handler releases, then the helper releases again | once |
| Parameter units | converted through Legacy-specific expressions; the commanded and measured positions are scaled differently from each other | native integers only; no conversion, no scaling, no splitting |
| Target source | Operator numeric control, or a persisted path coordinate, or a standby coordinate, selected by a mode number embedded in the request | the typed request target, bounded by the Owner-local profile |
| Preconditions | none beyond a mapping lookup | verified Home in this session, Owner-local native bounds, tolerance configured, brake configured, observed stationary |
| Completion detection | none in the move helper; a separate flag machine in the status handler plus a counter | motion bit inactive, then fresh TP **and** RP within tolerance |
| Post-command verification | none | required |
| Classification | command shape and order **EQUIVALENT**; units, completion and verification are **DELIBERATE NON-PORT** and **DELIBERATE STRENGTHENING**. | |

### 3.6 Brake lifecycle

| Field | Legacy | WJSS |
| --- | --- | --- |
| Release before motion | yes, by both the action handler and the motion helper | yes, once, by the operation lifecycle |
| Apply after motion | **the apply branch is commented out** — the Legacy application never applies the brake automatically | applied after the axis is observed inactive |
| Commands from a read path | **yes** — the status handler releases the brake whenever it observes motion | never — no command is issued from observation |
| Observed brake state | never read; the UI shows the last commanded state | the output state is a separate raw observation reported as unconfirmed |
| Polarity | fixed in the source | Owner-local profile; a missing value refuses every brake release |
| Classification | order **EQUIVALENT**; the absence of a read-path command and the presence of an automatic apply are **DELIBERATE STRENGTHENING**. | |

### 3.7 TP and RP observations

| Field | Legacy | WJSS |
| --- | --- | --- |
| Read shapes | first position read and second position read, each with the axis letter after a single space | identical shapes, in a closed immutable intent table |
| Order | status, then the two position reads, per axis | status first, then the position reads, per axis |
| Scaling | the two values are scaled by different expressions (a known Legacy defect) | both stay raw native values; no conversion |
| Parse failure | becomes zero | refused or reported unavailable; never zero |
| Use in completion | not used by the move or home helpers | the sole basis of every zero, move and centre verification |
| Classification | shapes and order **EQUIVALENT**; scaling and parse behaviour are **DELIBERATE NON-PORT**. | |

### 3.8 H1 path execution (P1 to P6)

| Field | Legacy | WJSS |
| --- | --- | --- |
| Trigger | one Start control, driven by a timer | an explicit Run Path action |
| Home before the path | **none** — the path assumes the operator has already homed or reset | Run Path never homes; it requires a `Homed` and `ZeroVerified` readiness in this session and otherwise refuses, directing the Operator to Commission/Home |
| Position definition in the path | **none** | none — the readiness is carried from the Home lifecycle |
| Encoder definition in the path | **none** | none |
| Point dispatch | both mapped axes dispatched in parallel for the step, then a fixed wait | horizontal request then vertical request through one serialized boundary, each axis with its own independent lifecycle |
| Point count | six, hard-coded | six, from the accepted path store |
| Point verification | none — success is inferred from a moving flag that is read without synchronisation | per point, both axes: inactive, fresh TP and RP within tolerance, plus an accepted stop and brake |
| Path abort | stops the timer and the status flag only | typed abort stops every active axis exactly once and records cleanup |
| Classification | the path is **DELIBERATE NON-PORT** in structure; verification is **DELIBERATE STRENGTHENING**. The absence of any position or encoder definition in either implementation is **EQUIVALENT**. | |

### 3.9 Centre / standby return

| Field | Legacy | WJSS |
| --- | --- | --- |
| Trigger | after the last step, as two parallel absolute moves to a persisted standby coordinate | after a verified P6, as the dry Centre return on the fixed Centre contract |
| Verification | none | both axes must verify the Centre target before any completion is published |
| Classification | trigger and intent **EQUIVALENT**; verification **DELIBERATE STRENGTHENING**. | |

### 3.10 Abort

| Field | Legacy | WJSS |
| --- | --- | --- |
| Operator abort of the path | stops the timer; no stop is sent to either axis | typed Owner abort; every active axis stopped exactly once, cleanup recorded, no later point may start |
| Abort of a running operation | not modelled | typed stop reason preserved; supervised Stop; brake only after an observed inactive state |
| Classification | **DELIBERATE STRENGTHENING**. | |

### 3.11 Close and cleanup

| Field | Legacy | WJSS |
| --- | --- | --- |
| On form close | stops the feedback loop only | stops every active motion operation, awaits each axis independently, then invalidate, close the handle, release the modules |
| Stop on close | none | per-axis supervised Stop |
| Brake on close | none | applied only after the matching axis is observed inactive |
| Communication fault | counted; after a threshold the process may reconnect | session becomes `FAULTED`; no retry, no reconnect, no replay |
| Classification | **DELIBERATE STRENGTHENING** and **DELIBERATE NON-PORT**. | |

### 3.12 Axis identification

| Field | Legacy | WJSS |
| --- | --- | --- |
| Logical to controller axis | a per-index map persisted in a configuration file with no schema, no integrity check and two conflicting default sets | the canonical topology catalog, Owner-approved 2026-10-25 |
| Classification | **DELIBERATE NON-PORT** — the persisted Legacy map is not ported and does not become a product default. | |

### 3.13 Jog direction labels

| Field | Legacy | WJSS |
| --- | --- | --- |
| Vertical direction convention | the up control maps to the negative direction and the down control to the positive direction | the controls are labelled negative and positive with no up/down meaning and no inversion |
| Classification | **NOT A MATERIAL DIFFERENCE** — a presentation convention on the Legacy surface, not a command shape; recorded only so that no reader infers an unrecorded inversion. | |

---

## 4. Determination on the dual-zero question

> **Dated history — superseded.** Sections 4.1 to 4.3 are the first-pass determination of
> 2026-10-26 and are preserved verbatim. The Owner decision recorded in the header and in section
> 4.5 supersedes 4.2 and 4.3. Section 4.1 (source facts) and 4.4 (the nature of the post-command
> verification) remain accurate and are not superseded.

### 4.1 What is proven at source level

- **P-1.** The Legacy reset-encoder action is a position definition followed by an encoder
  definition, one axis, with a reply awaited for each and with no brake release, no stop, no
  inter-command delay and no verification. This is the only place in the entire pack where the two
  definitions appear together.
- **P-2.** The Legacy home completion is a stop, a fixed wait and a position definition **only**.
- **P-3.** The Legacy cleaning-path execution performs no home sequence and no position or encoder
  definition of any kind.
- **P-4.** WJSS already contains both definitions as separate typed operations, each with its own
  command shape, its own lifecycle and its own reported state; the encoder definition is present,
  permitted only by the Owner-local profile, and reported as effect-unverified.
- **P-5.** WJSS dispatches the position definition and never the encoder definition from the Home
  lifecycle, from the H1 path and from the Centre return — which is what the Legacy home completion
  and the Legacy path do as well.
- **P-6.** The current accepted H1 contract states that the dual-zero definition is optional,
  excluded from every session of the trial, and that reproducing the Legacy reset-encoder behaviour
  is a separate, separately-authorized step after H1 run evidence is complete
  ([`../H1_DRY_CLEANING_PATH_TRIAL_PROCEDURE.md`](../H1_DRY_CLEANING_PATH_TRIAL_PROCEDURE.md) §3).
- **P-7.** Committed tests pin the current behaviour on exactly this point: the Home sequence is
  pinned as brake, home speed, home search, begin, stop, position definition, brake; the H1 run is
  pinned as exactly two position definitions and zero encoder definitions; a Home failure is pinned
  as never sending an encoder definition.

### 4.2 The five-way classification applied

| Sub-question | Determination | Basis |
| --- | --- | --- |
| Is the absence of `DE<axis>=0` after `DP<axis>=0` a **PROVEN ROOT CAUSE** of any H1 or Manual Motion divergence? | **No** | No divergence is recorded anywhere in committed evidence — there is no hardware observation, no measured discrepancy and no reproduction, so no cause can be proven. A cause requires an effect. |
| Is it a **STRONGLY SUPPORTED CAUSE**? | **No** | The only mechanism that would make it causal is a difference between the position definition and the encoder definition on the installed firmware, and the vendor command semantics are **not** in the evidence of record. `GCLIB_2_6_0_API_USAGE_BASELINE.md` is a vendor library-API record, not the controller command reference. Without that, the mechanism is a hypothesis, not support. |
| Is it a **LEGACY-EQUIVALENCE GAP** whose root cause is not proven? | **Yes — for the manual reset-encoder action only** | The Legacy one-action reset has no WJSS counterpart; both of its constituent definitions exist separately. Whether the gap has any effect is not established. |
| Is it **NOT THE CAUSE** for H1? | **Yes — for the H1 path, the Home lifecycle and the Centre return** | P-2, P-3 and P-5 are equivalent on this dimension: Legacy Home and the Legacy path issue no encoder definition, so a WJSS H1 that issues none cannot diverge from them by omitting one. |
| Is the implementing question **NOT VERIFIED — OWNER DECISION REQUIRED**? | **Yes** | P-6 (an accepted Owner decision excludes the encoder definition from every session and defers the Legacy reset behaviour to a separate authorization) and P-7 (committed tests pin the opposite behaviour). Implementing it would reverse an Owner decision, not correct a defect. |

### 4.3 Determination

**PRIMARY DETERMINATION: `NOT VERIFIED — OWNER DECISION REQUIRED`.**

Restated in the Owner's own vocabulary, the composite answer is:

1. for the H1 path, the H1 Home lifecycle and the Centre return: **NOT THE CAUSE** — the omission
   is Legacy-equivalent, proven from the exact Legacy Source;
2. for the Manual Motion surface: **LEGACY-EQUIVALENCE GAP, ROOT CAUSE NOT PROVEN** — the Legacy
   combined reset has no single WJSS action;
3. for the H1 implementation question: **NOT VERIFIED — OWNER DECISION REQUIRED**, because the
   accepted contract and committed tests require the current behaviour.

It is therefore **not** proven that H1 must reproduce the Legacy reset-encoder sequence, and the
conditional implementation was correctly not made.

*End of the superseded first-pass determination.*

### 4.4 Why the requested post-command verification is not Legacy equivalence

The instruction attached a requirement to take fresh TP and RP observations after the final command
and verify both within tolerance. That requirement is **good practice, but it is not Legacy
equivalence**: the Legacy reset path takes no position reading at all after either definition. Any
combined reset would therefore be a WJSS strengthening built on a Legacy command shape — one more
reason it could not be adopted without an Owner decision.

The Owner has since taken that decision: the fresh post-definition TP/RP verification is retained as
an **Owner-approved WJSS safety strengthening**, and the Legacy command shape is composed beneath
it. The two are not the same thing, and the correction treats them separately: the *command
sequence* is Legacy-equivalent, the *verification* is a deliberate WJSS strengthening.

### 4.5 Corrected determination — PROVEN H1 COMMAND-SEQUENCE DEFECT

**Classification: `PROVEN H1 COMMAND-SEQUENCE DEFECT`.**

The first pass framed the question as "is the omission of `DE` the cause of a divergence?" and
answered "no divergence is recorded". That framing was incomplete in one decisive respect, and the
Owner's Hardware evidence supplies the missing effect: **H1 already requires a zero proof from both
position reads, so the run cannot succeed on a single definition whenever the two reads are not
independently zeroed by one command.** The defect is therefore not "a difference from Legacy" but
**an internal contradiction between the H1 contract and the H1 lifecycle**:

| Element | Evidence |
| --- | --- |
| The H1 contract requires `Homed` + `ZeroVerified` from **both** TP and RP within tolerance. | committed contract and tests |
| The Home-carried zero establishment dispatched **only** the position definition. | exact WJSS Source at `25389cd` |
| The Legacy commissioned reset-encoder sequence is the position definition **then** the encoder definition. | exact Legacy Source pack (§2, §3.1) |
| On the current implementation H1 reached zero verification, RP became zero, TP stayed at roughly 42,000 to 43,000 native units, and the run ended `HOME_ZERO_VERIFICATION_FAILED`. | Owner Hardware observation |

**Root-cause confidence: `STRONGLY SUPPORTED BY`** the exact current WJSS command sequence, the exact
Legacy Reset Encoder sequence, and the Owner Hardware observation. **Final causal confirmation still
requires the separately authorized Hardware re-trial after this correction; no Hardware proof is
claimed before that re-trial.**

Two related conclusions are corrected with it:

1. **Legacy Home and the Legacy Reset Encoder are separate operations.** The Legacy home-complete
   branch sends a stop, a fixed wait and a position definition only; it never sends the encoder
   definition. The Legacy reset-encoder action is the only place the two definitions appear together.
   Reproducing the reset-encoder *command pair* inside the WJSS Home lifecycle is therefore a
   deliberate **composition of two Legacy operations into one WJSS lifecycle**, authorized by the
   Owner, not a claim that Legacy Home contained it.
2. **H1 intentionally composes Home with the commissioned Legacy Reset Encoder sequence**: stop and
   inactive verification, then `DP<axis>=0`, then `DE<axis>=0`, then fresh TP/RP verification. The
   missing `DE` was a defect **relative to the H1 contract**, not a divergence from Legacy Home.

---

## 5. Material differences found outside the dual-zero class

Every difference is reported. None is proposed as an implementation.

| # | Difference | Classification | Defect? |
| --- | --- | --- | --- |
| M-01 | Legacy releases the brake whenever the status read observes motion, from inside the read path; WJSS never commands from observation | DELIBERATE STRENGTHENING | No — recorded as Legacy defect D-06 |
| M-02 | Legacy's automatic brake apply is commented out; WJSS applies the brake after an observed stop | DELIBERATE STRENGTHENING | No — a deliberate divergence, not a reproduction |
| M-03 | Legacy relies on fixed delays; WJSS uses a configured settle and an observed stopped state within a bounded timeout | DELIBERATE STRENGTHENING | No |
| M-04 | Legacy releases the brake twice on an absolute move; WJSS releases once | DELIBERATE NON-PORT | No — the Legacy duplication is not reproduced |
| M-05 | Legacy's completion counter is a single field shared by all four axes, incremented inside the status handler | DELIBERATE STRENGTHENING | No — recorded as Legacy defect D-18 |
| M-06 | Legacy verifies nothing after a move; WJSS requires fresh TP and RP within tolerance | DELIBERATE STRENGTHENING | No |
| M-07 | Legacy Home has no timeout and no failure path; WJSS is bounded and fails closed | DELIBERATE STRENGTHENING | No |
| M-08 | Legacy has no already-active-home refusal; WJSS refuses fail-closed | DELIBERATE STRENGTHENING | No |
| M-09 | Legacy Jog ends only on an explicit stop; WJSS ends on release, capture loss, heartbeat expiry, maximum hold or close | DELIBERATE STRENGTHENING | No |
| M-10 | Legacy close stops only the polling loop and leaves motion running; WJSS stops, awaits and then invalidates, closes and releases | DELIBERATE STRENGTHENING | No |
| M-11 | Legacy dispatches both path axes in parallel behind an unsynchronised flag; WJSS uses ordered per-axis requests through one serialized boundary | DELIBERATE NON-PORT | No |
| M-12 | Legacy converts Operator decimals to integers and then to scaled controller counts, with two different scalings | DELIBERATE NON-PORT | No — recorded as Legacy defects D-08, D-09, D-10 |
| M-13 | Legacy stores the logical-to-controller axis map in an unversioned file with conflicting defaults | DELIBERATE NON-PORT | No — superseded by the canonical topology |
| M-14 | Legacy sends a liveness probe before every command and doubles every round trip | DELIBERATE NON-PORT | No — recorded as Legacy defect D-01 |
| M-15 | Legacy has no single-outstanding rule, no generation fence and no correlation identifier | DELIBERATE STRENGTHENING | No — recorded as Legacy defects D-02, D-03 |
| M-16 | Legacy clears the whole output bank when connecting | **EXCLUDED** — already outside every WJSS surface | No action; confirmed absent from WJSS |
| M-17 | Legacy vertical jog labels invert the direction; WJSS uses signed labels with no inversion | NOT A MATERIAL DIFFERENCE | No |

**No difference in this table is a WJSS defect against the Legacy Source.** Each is either an
approved deliberate non-port or a deliberate strengthening already recorded in
[`MANUAL_MOTION_LEGACY_CONTINUITY.md`](MANUAL_MOTION_LEGACY_CONTINUITY.md) §3, or a Legacy defect
the Product deliberately does not reproduce.

**Consequence for this pass.** No implementation Scope was expanded, no code was changed and no
Owner decision in §6 was anticipated.

---

## 6. Owner decisions requested

> **Resolution status.** **D-A is RESOLVED** by the Owner decision recorded in the header and in
> section 4.5: H1 must use the complete Legacy Reset Encoder sequence after a successful Home, and
> the fresh TP/RP verification is retained. **D-B is ANSWERED** by the Owner Hardware observation
> recorded in section 4.5. **D-C and D-D remain OPEN** and do not block this correction.

1. **D-A — Reset-encoder surface (blocking this question only).** Should WJSS gain a single typed
   "reset encoder" action that reproduces the Legacy combined definition, as a strengthening with
   fresh post-command TP and RP verification, or should the two existing separate actions remain the
   only surface? Answering "yes" reverses the exclusion recorded in the accepted H1 contract §3, so
   it is an Owner decision, not an implementation detail. If the answer is yes, the affected
   contract text and the pinned tests must change in the same authorized pass.
2. **D-B — Is there a recorded symptom?** If the question arises from a physical observation, the
   observation itself is required as evidence (which axis, which session, which step, what was seen
   on the position reads, and whether the position definition or the encoder definition was
   discharged). No such record exists in the repository, and without it no cause classification can
   rise above "not proven".
3. **D-C — Firmware semantics of the two definition commands.** May the vendor command reference for
   the installed controller be supplied as evidence? It is the missing link between "the Legacy
   application sent two commands" and "the two commands have different effects on this machine".
4. **D-D — Confirmation that no other difference in §5 needs action.** The investigation found no
   WJSS defect against the Legacy Source outside §5; the Owner is asked to confirm that reading
   before any future consolidation pass.

---

## 7. What the first pass did not do, and what the correction changed

The first pass changed no code and used no Hardware; that statement remains true of the first pass
and is preserved as dated history. The correction that followed the Owner decision changed exactly
the Home-carried zero establishment and its evidence:

- the typed operation lifecycle gained one phase and one dispatch step so that a Home operation
  sends the position definition and then the encoder definition as one contiguous native pair;
- the zero-verification watermark moved from the position definition to the **last**
  zero-establishing command, so a reading taken before the encoder definition can never verify zero;
- the advance-order rule keeps the axis's encoder definition immediately after its accepted
  position definition, so no other axis's command can be interleaved between them;
- H1 records the composition as such (`H1_DUAL_ZERO_DISPATCHED_IN_HOME_SEQUENCE` /
  `H1_DUAL_ZERO_REFUSED_IN_HOME_SEQUENCE`) instead of reporting the encoder definition as an
  optional, unrelated manual define;
- the standalone define-position-zero and define-dual-zero operations are unchanged;
- no material difference in §5 was repaired, because none is inseparable from this correction.

**No Hardware was used in the correction either.** No endpoint was contacted, no library loaded and
no command dispatched.

---

## 8. Scope of the authorized correction

The first pass recorded an impact analysis for a future authorized change. The Owner decision made
that change authorized, and the correction followed it exactly; this section records what was
touched and what deliberately was not.

**Touched:**

- the operation lifecycle's zero establishment, as described in section 7;
- the closed typed vocabulary: one new operation phase and two new typed outcome codes, with no new
  command kind, no new mnemonic and no raw command text;
- the H1 coordinate evidence mapping and its disposition codes;
- the deterministic simulated port: one opt-in option that reproduces the Owner-observed split
  between the two position values, so the defect and its absence are both testable;
- the focused tests that pin the sequence, the order, the refusal paths and the watermark;
- the documents named in section 9 of the correction report.

**Deliberately untouched:**

- the closed command plan: both definition shapes were already rendered, so no template changed;
- the Home motion behavior (brake release, settle, home speed, home search, begin), the Stop
  supervision, the brake lifecycle, the Move lifecycle and the Centre return;
- the position tolerance, the zero-verification predicate and both position reads;
- the standalone Manual Motion operations and the profile permission semantics;
- the Run Path readiness rule, which still requires a session-scoped `Homed` + `ZeroVerified` and
  still dispatches no Home, no position definition and no encoder definition;
- every dependency, package and lock file.

---

## 9. Not verified

1. The effect of either definition command on the installed controller is **NOT VERIFIED**; the
   vendor command reference is not in the evidence of record.
2. Whether the position and encoder definitions differ in effect on this machine is **NOT VERIFIED**.
   The Owner Hardware observation of section 4.5 is the evidence that one command left a position
   value at zero and the other left it far from zero *in the current implementation*; which command
   produced which value is confirmed only by the Hardware re-trial.
3. **The corrected sequence is NOT Hardware-confirmed.** It is proven at Source level and covered by
   deterministic simulated tests only. A separately authorized Real Galil H1 re-trial on the exact
   corrected head is required before any physical confirmation is claimed.
4. Status-word bit order, polarity and meaning remain `UNCONFIRMED` as recorded in the previous
   baseline §G.1 and are not advanced by this document.
5. Brake-output electrical polarity remains `UNCONFIRMED`.
6. No build, no test execution and no .NET gate was run in the Author environment: it has no .NET
   SDK. The Owner-local locked restore, build and test suite remain the validation of record for the
   software as corrected.
7. This document makes no statement about any physical outcome of any command, before or after the
   correction.

---

## 9A. Required Hardware re-trial

The correction is a Software closeout only. The physical confirmation it enables must be obtained
separately, on the exact corrected head, under its own authorization:

- the trial must run the full H1 Commission/Home on the real controller and record, per axis, the
  native command sequence actually dispatched (position definition, then encoder definition);
- it must record the two position readings taken after the encoder definition and their relation to
  the configured tolerance;
- it must record whether the run reaches `HOME_COMPLETED_ZERO_VERIFIED` and `Homed` +
  `ZeroVerified`, and every terminal code if it does not;
- any result is Owner evidence. A simulated pass is never Hardware evidence and never commissioning.

---

## 10. Redaction statement

This record contains no Legacy source line, no endpoint, no address, no serial or account
identifier, no local path, no coordinate, no travel limit, no speed, acceleration or deceleration
value, no delay value, no vendor error text and no production profile value. Command shapes are
recorded as mnemonics with a placeholder axis or output, which is the same convention already
committed in the closed command plan and in the previous baseline. The pack itself is not committed
and was not committed at any point in this investigation.
