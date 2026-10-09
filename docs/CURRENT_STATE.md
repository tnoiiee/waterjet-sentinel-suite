# Current State — WaterJet Sentinel Suite (WJSS)

**Current Stage 0.4A CP-3c-2 / CP-4A closeout (2026-10-10):** The exact validated **code** head is `fd053fde07c96163b073fc9cb4c0383f31eafe3c` on `arena/3d5bdbfc-waterjet-sentinel-suite`, based on approved main `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b`. Owner-reported exact-head results (not executed in Arena): environment variables cleared; locked restore PASS, no lock drift; Release build PASS with TreatWarningsAsErrors, 0 warnings / 0 errors; full .NET **414/414 passed, 0 failed, 0 skipped**; fixture parity **7/7 passed, 0 failed, 0 skipped**; TypeScript typecheck PASS and **34/34 passed, 0 failed**; boundary scan 0 findings, S1–S9 clean; `git diff --check` PASS; no local source drift; final Owner-local tree CLEAN, no artifact commit. Owner manual browser review of the pressure/Valve matrix, parallel return, sequential two-Job AutoSequence, read-only fault presentation and truthful Pump labels **PASSED**; screenshots are Owner-observed, not Arena-executed. Exact-source static Final Source Review **PASSED, no blocking defect**. See [final checkpoint](STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md) for the full gate matrix and observations. **NON-BLOCKING UI CONTEXT-LABEL PUNCHLIST:** future wording should distinguish Current Active Job from last released Job outcome and Valve-close resolution; no change in this closeout. CP-3c-2 Runtime Host and CP-4A minimal GET-only Inspector are submitted for Owner merge review, **PR #11 OPEN, NOT MERGED**. CP-4 full UI refinement is not delivered; MODBUS not implemented, no device command/write path, TEST_HARDWARE and PRODUCTION not authorized. The older development/pending paragraph below is preserved as dated history and superseded for current status.

**Latest fast-track checkpoint (2026-10-09):** CP-3c-2 Runtime composition and CP-4A minimal read-only Inspector are Owner-authorized and source-delivered from approved main `dfc2c02c550c1cc2367fa5ca6271dd3b2510557b` (PR #10 merged in ancestry). .NET build/tests and Owner-local GET/UI observation are **NOT VERIFIED**; do not infer Owner acceptance. PR for this checkpoint is open, not merged. CP-4 full UI, write routes, MODBUS, TEST_HARDWARE and PRODUCTION remain unauthorized. See [development checkpoint](STAGE_0.4A_CP-3C-2_CP-4A_CHECKPOINT.md). Older PR #10 OPEN/CP-3c-2 NOT AUTHORIZED statements below are historical, superseded by this gate, not contemporary status.

**Document status:** The verified state below is `[APPROVED]` as a factual record.
Stage status wording and the open-item list were corrected by the Owner-confirmed Stage 0.1
documentation review punchlist, updated by the approved Stage 0.2 Scope Gate — *Technology and
Solution Architecture Decision*, refined by the Owner-requested Stage 0.2 documentation review
punchlist, and updated for Stage 0.2 Owner acceptance and the approved Stage 0.2.1A spike.
Open items are `[OPEN]` and must not be resolved by assumption.

This document answers one question: *what is actually true right now, with evidence?*
Nothing in this repository may contradict it. If something does, the discrepancy is reported
rather than silently resolved.

---

## 1. Status legend

Used throughout the documentation:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Approved by the Owner in an approved Stage Gate. Binding on future work. |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review. Binding. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. **Not** approved behaviour. |
| `[NOT VERIFIED]` | Not confirmed by bench test, field test, measurement, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Currently prohibited. The activity, write, or access must not be performed. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed in the meantime. |

Stage implementation status uses a separate vocabulary: `SUBMITTED FOR OWNER REVIEW`,
`CHANGES REQUESTED`, `IN PROGRESS`, `OWNER ACCEPTED`, `NOT MERGED`, `MERGED`. Only the Owner
may record `OWNER ACCEPTED` or `MERGED`.

## 2. Stage and approval status

**Latest CP-3c-1 position (2026-10-09): OWNER-LOCALLY VALIDATED at `29f133ce6598bc2eb489273a2f2d40f8827deb5f`; Final Source Review PASSED, no blocking defect. PR #10 OPEN, NOT MERGED. Library foundation only; Host/API/Inspector integration absent. CP-3c-2 and CP-4 NOT AUTHORIZED. This supersedes earlier CP-3c-1 validation-pending wording, without rewriting dated historical records.** See the CP-3c-1 checkpoint closeout.

| Item | Value |
| --- | --- |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — merged to `main` through PR #1 |
| Stage 0.1 Owner manual review | **Recorded as complete by the Owner** in the approved Stage 0.2 Scope Gate, which states that the previous Stage branch completed its role and was merged through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** — *Technology and Solution Architecture Decision* |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED / MERGED** — merged to `main` through PR #2; source checkpoint `5bcf1b33f924ab30590a55736676200115874fa1`; merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73` |
| ADR-0006 to ADR-0013 | **ACCEPTED** as architecture direction. Accepted does **not** mean implemented; items marked `[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` inside them keep those markers |
| Stage 0.2.1 (two-candidate spike as originally proposed) | Not started; superseded by the narrower Stage 0.2.1A |
| Stage 0.2.1A Scope Gate and Coding Start Gate | **APPROVED** — *React UI and Runtime Feasibility Spike* (synthetic) |
| Stage 0.2.1A implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT — MERGED** (PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; formerly OPEN — READY FOR OWNER MERGE). Checkpoint `114c0761` accepted by the Owner as the Development baseline (UI, synthetic AutoSequence controls, GlobalQueue presentation, critical Pump modal, Mandatory Safe Return behaviour) — see §12.11; [results §0G](spikes/stage-0.2.1a-results.md#0g-owner-local-final-review-and-stage-021a-closeout-documentation-only). *History:* branch `arena/01a108d8-waterjet-sentinel-suite`, PR #3 to `main` **OPEN**; Sensor map corrected to the Owner's 106-location domain (`935973e6`); fullscreen Operations layout refined under the Owner-approved Design Addendum (1920 × 1080, Edge F11); Operations readability refined after the Owner screenshot review (sensor-cell zones, typography scale, desaturated process colours separated from alarm, card hierarchy); final Owner UI punchlist on `4129687a` (self-hosted Google Sans, marker in the value row, expanded identity, legend clipping, Water Jet display terminology, mixed queue sources, opt-in synthetic test controls) on `23f48daa`; GlobalQueue semantics corrected under the Owner domain correction (bounded ready-only synthetic queue, head-only atomic dispatch; previous synthetic queue behaviour SUPERSEDED, not eligible for production promotion) with the `READ-A` value-clipping and `WJ-A` test hotfixes ([results §0D](spikes/stage-0.2.1a-results.md#0d-globalqueue-semantics-correction-and-head-only-dispatch-stage-021a)); synthetic critical Main Pump handling (High Critical; AutoSequence `CRITICAL_SUSPENDED`; blocking critical modal) and Mandatory Safe Return for every Job outcome under the Owner critical Pump decision — SYNTHETIC PROOF ONLY, production safety NOT VERIFIED ([results §0E](spikes/stage-0.2.1a-results.md#0e-critical-main-pump-handling-and-mandatory-safe-return-stage-021a)) |
| Stage 0.2.1A Owner-local testing (installed Edge, Windows 11) | Checkpoint `dd20a8bd` (superseded 104-location map): **PASS**. Windows 11, Node v24.20.0, npm 11.19.0, Git 2.55.0.windows.5, installed Edge; `npm ci`, typecheck, Vitest 53 / 53, build, harness 14 / 14, and Edge E2E 15 / 15 all PASS. That evidence **does not validate** the corrected Sensor map, the fullscreen layout, or the readability refinement; Owner-local Edge E2E at `4129687a`: **25 / 25 PASS** (≈ 1.3 min, Owner-reported). At `23f48daa`: 34 selected · 25 passed · 2 failed (`READ-A`, `WJ-A`) · 7 not run (Owner-reported). The GlobalQueue-correction checkpoint (35-test selection) was not re-run separately. At the critical Pump / Safe Return checkpoint `81c87a44`: 42 selected · 32 passed · 1 failed (`S11/S12`, Dirty Score drifted between samples) · 9 not run (Owner-reported). Final spike closeout checkpoint `114c0761` (48-test selection, `SEQ-B`..`SEQ-G`, deterministic `S11/S12`, 18-step manual sequence): Owner-local final Edge gate **PASS** and Owner manual review **PASS** (Owner-reported, 2026-10-07; detailed counts and screenshots were not supplied to the Agent). Owner-local interrupted overnight observation of `ea23bc58`: **COMPLETED** — not a controlled benchmark ([results §0B.1](spikes/stage-0.2.1a-results.md#0b1-owner-local-interrupted-overnight-observation-recorded-as-reported-by-the-owner)) |
| Stage 0.2.1A controlled 15-minute Owner-local observation | **Waived as a Stage 0.2.1A merge blocker** (Owner decision, 2026-10-07) — not run |
| Stage 0.2.1A Owner manual review (`114c0761`) | **PASS** (Owner-reported, 2026-10-07) |
| Stage 0.2.1A 60-minute Owner-local run | **Waived as a Stage 0.2.1A merge blocker** (Owner decision, 2026-10-07) — not run |
| Stage 0.2.1A merge | **MERGED** — PR #3 executed by the Owner (merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; the Agent never merges) |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07); Production transport and chart library remain `[OPEN]` |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Main Development Scope Gate | **PENDING** for 0.3B+ — 0.3A proceeds under the Owner's Option-C amended gate only; **Stage 0.4A CP-0 and CP-1 authorised 2026-10-08 (CP-2+ not authorised)** *Superseded 2026-10-09: CP-2 and CP-3a/CP-3b are authorised and validated as recorded in rows 60, 61 and the 2026-10-09 row; CP-3c and CP-4 NOT AUTHORIZED.* |
| Stage 0.3 | **NOT AUTHORIZED** as a whole; **Stage 0.3A-1 source checkpoint** authored under the amended gate (see §11.4, §12.12) |
| Stage 0.3A-1 .NET validation | **PASSED (Owner-local, 2026-10-07)** — Release build 0 warnings / 0 errors, full suite 61/61, TypeScript 24/24, parity 7/7, boundary S1–S9 clean; PR #4 READY FOR OWNER MERGE (not merged). See §12.22 and the [runbook](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) verdict; Arena claims no .NET execution of its own |
| Stage 0.3A-2 | **AUTHORIZED** — Owner instruction 2026-10-07, three checkpoints A → B → C on one branch, SIMULATOR profile only. **Checkpoint A OWNER-LOCALLY VALIDATED (2026-10-07)**: Release build 0 warnings / 0 errors, full .NET suite **101/101**, locked restore PASS, boundary scan S1–S9 clean, genuine lock-refresh commit `55d3b8b` (see §11.5, §12.23, §12.24). **Checkpoint B OWNER-LOCALLY VALIDATED (2026-10-07)** at `cfa6d4a`: Release build 0 warnings / 0 errors, full .NET suite **129/129**, locked restore PASS, lock drift NONE, boundary S1–S9 clean (see §11.6, §12.25). **Checkpoint C (read-only Runtime API + development Runtime Inspector) OWNER-LOCALLY VALIDATED (2026-10-07)** at the genuine lock-refresh commit `faa79145a925832baa2ac8685f7fecf7a093552d`: SDK `10.0.401`, locked restore PASS, Release build **0 warnings / 0 errors**, fresh full .NET suite **139 total / 139 passed / 0 failed / 0 skipped**, boundary scan S1–S9 clean, worktree CLEAN; Owner Function/Logic review **PASSED** and Inspector UI review **PASSED WITH MINOR PUNCHLIST** (see §11.7, §12.27). Arena compiled and executed nothing itself. **NOT MERGED** |
| Stage 0.3A-3 (equipment topology and legacy parameter migration; the earlier "config loading pipeline" label is superseded — see the CP-0 reconciliation block) | **COMPLETE — MERGED to `main` via PR #6 (`909d028`)**. Historical record: **AUTHORIZED** — Checkpoint A (equipment topology decision + legacy parameter migration specification) reviewed to **PASS**; **ADR-0017 explicitly Owner-ACCEPTED**. **Checkpoint B OWNER-LOCALLY VALIDATED (2026-10-08)**: Release build 0 warnings / 0 errors, fresh full .NET suite **186/186**, locked restore PASS, boundary S1–S9 clean, JSON examples PASS, no Owner acquisition data in Git; validated feature head `2db853a723a74399430ddd84900c40371fe56b29`, genuine Owner lock refresh `0728df61f917ba61f6dd3b8bf6d12e68dfa01d20` (project graph `wjss.domain → Wjss.Contracts` only). I7/I16 are NON_SENSOR_GAP; derived `scanOrder` 1–106; direct `cannon n → WJn`. See §11.8, §12.28 and [`STAGE_0.3A-3_CHECKPOINT_B.md`](STAGE_0.3A-3_CHECKPOINT_B.md). Arena compiled and executed nothing itself. **[Superseded 2026-10-08: PR #6 MERGED (`909d028`); Checkpoint C later authorised and validated, see §11.9] Historical text: PR #6 OPEN — NOT MERGED. Checkpoint C NOT AUTHORIZED** |
| Stage 0.4A (Simulator Sequencing Foundation) | **AUTHORIZED for CP-0 and CP-1 ONLY** — Owner instruction 2026-10-08. CP-0 = status/ADR reconciliation (documentation only); CP-1 = pure GlobalQueue and AutoSequence gate in `packages/application/Runtime/Sequencing/` with Runtime.Core tests. **CP-4 and CP-3c NOT AUTHORIZED.** CP-3a and CP-3b: Owner-authorized by the completion correction (§11.11); implementation NOT VALIDATED until the Owner-local gates pass. Source authored in Arena; NOT COMPILED IN ARENA; NOT EXECUTED IN ARENA; OWNER-LOCAL VALIDATION REQUIRED. *Superseded 2026-10-09 (see the 2026-10-09 CP-3a/CP-3b row): the 'CP-0 and CP-1 ONLY' headline and the 'source authored; validation required' wording are historical. CP-3a and CP-3b are OWNER-LOCALLY VALIDATED at `a315e90`; CP-3c and CP-4 NOT AUTHORIZED.* |
| (Superseded 2026-10-08 by the CP-1 closeout block below: CP-0 and CP-1 are **OWNER-LOCALLY VALIDATED** at feature head `2cfe648`; CP-1 Final Source Review complete. The "source authored; validation required" wording above is historical.) | |
| Stage 0.4A CP-3a / CP-3b completion correction (2026-10-08) | **OWNER-AUTHORIZED for CP-3a and CP-3b**; implementation **NOT VALIDATED** until the Owner-local gates pass. Branch `arena/9aa1746c-waterjet-sentinel-suite` from base `d884a54`. CP-3a: public preview removed; FU-1, FU-2, FU-4 and AxisStandbySeq (SR5) unchanged. CP-3b: canonical Sensor binding, five-scope retention, pure wire projection (queue, Sensor queue state, Active Job, Safe Return, Sequence, critical event, last Job outcome), candidate Delta with one appended trend point. **NOT COMPILED IN ARENA; NOT EXECUTED IN ARENA.** CP-3c and CP-4 NOT AUTHORIZED; not merged. See §11.11. *Superseded 2026-10-09 by the row below: validated head `a315e90` (Owner-reported); the 'NOT VALIDATED' and 'NOT COMPILED IN ARENA' wording in this row is historical, dated evidence.* |
| Stage 0.4A CP-3a / CP-3b Owner-local validation and Final Source Review (2026-10-09, documentation only) | **CP-3a and CP-3b OWNER-LOCALLY VALIDATED** at validated head `a315e90be28fb5e9662c6a7179587c992273cb88` (Owner-reported; not reproduced in Arena): .NET SDK 10.0.401; xUnit 10.0.12; Release build 0 warnings / 0 errors; full .NET **552/552**; focused Runtime.Core 430/430; Retention and Projection 131/131; direct Valve-retention regression 1/1; fixture parity 7/7; TypeScript typecheck PASS, tests 31/31; boundary S1–S9 clean; `git diff --check` PASS; working tree CLEAN; no artifact commit required. **VALVE FEEDBACK RETENTION DEFECT: CLOSED.** **CP-3a and CP-3b Final Source Review PASSED** (no blocking defect; non-blocking follow-ups NB-1 to NB-8 in MASTER_PLAN §3.3.5; NB-1, the Safe Return label vocabulary, is an Owner contract decision that gates any wire emission). PR #9 OPEN, NOT MERGED. **CP-3c NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** No Host, API, Inspector, environment-variable scenario selection, runtime scenario execution, TEST_HARDWARE or PRODUCTION access is authorized. See MASTER_PLAN §3.3.5 and §11.11. |
| Stage 0.4A CP-2 closeout (2026-10-08) | **CP-2 OWNER-LOCALLY VALIDATED** at validated head `1f94991`: full .NET 300/300, Release build 0 warnings / 0 errors, TypeScript 31/31, fixture parity 7/7, boundary S1–S9 clean (Owner-reported). **CP-2 Final Source Review PASSED** (read-only; no blocking defect). PR #8 OPEN, NOT MERGED. **CP-3 and CP-4 NOT AUTHORIZED.** See §11.10 and §12.31. *Historical: PR #8 is MERGED to `main` at `d884a54`. The 'PR #8 OPEN' and 'CP-3 and CP-4 NOT AUTHORIZED' wording here is superseded for CP-3a and CP-3b by the 2026-10-09 row; CP-3c and CP-4 remain NOT AUTHORIZED.* |
| (Superseded 2026-10-08 by the CP-1 FINAL EVIDENCE AMENDMENT below: CP-1 OWNER-LOCALLY VALIDATED at validated head `03144f4`; F1 blocking state-integrity defect CLOSED; CP-1 Final Source Review PASSED; CP-2 coding NOT AUTHORIZED.) | |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

**CP-0 reconciliation (2026-10-08, Stage 0.4A).** This block supersedes the stale status text elsewhere in this file, including the rows and sections that still read "PR #5 OPEN", "PR #5 NOT MERGED", "PR #6 OPEN — NOT MERGED", "Checkpoint C NOT AUTHORIZED", or "Stage 0.3A-3 (config loading pipeline) AUTHORIZED" as current state. Those historical entries are retained as dated evidence and are not rewritten.

- **PR #5 MERGED** to `main`: merge commit `a74db62`, second parent `5802a6a`.
- **PR #6 MERGED** to `main`: merge commit `909d02846febfe2383f4d712f04455447f62bab0` (the Stage 0.4A approved base), second parent `8a8d6f0` (PR #6 head), first parent `a74db62`.
- **Stage 0.3A-3 COMPLETE** (equipment topology and legacy parameter migration, ADR-0017 Owner-ACCEPTED). The row label "config loading pipeline" is a superseded planning label; the config publication pipeline is not implemented by 0.3A-3 and is not claimed here.
- **Stage 0.4A scope gate** recorded here. Owner rulings in force for CP-1 (they supersede the 0.2.1A spike defaults where they differ): (1) queue admission is explicit synthetic scenario-prepared entries only, with no DIRTY-score, threshold, time, cleaning-history, or operator admission; (2) GlobalQueue holds dispatch-ready entries only, with capacity 8, FIFO, head-only dispatch, no scan-forward, and exactly one Active Job; (3) pump readiness is NOT a Queue refusal and NOT a Queue-entry state: after an atomic head dispatch, a not-ready Pump places the Active Job or AutoSequence in WAITING_FOR_PUMP, no water begins, and no next Job dispatches; (4) head revalidation failure removes the head atomically with REMOVED_BY_ELIGIBILITY, creates no Active Job, does not scan forward, and does not dispatch in the same transition; (5) pause is AutoSequence/Job state only: PAUSE_REQUESTED while a Job is active, PAUSED only after that Job's outcome and Mandatory Safe Return, and direct PAUSED when no Job is active, with Queue entries unchanged.
- **Known difference, carried as an open risk:** the 0.2.1A spike `dispatchHead` refuses dispatch with PUMP_NOT_READY. The Stage 0.4A Owner ruling (3) replaces that refusal for the CP-1 kernel. Production pump-readiness semantics remain **OWNER DECISION REQUIRED** (QUEUE_MODEL and `docs/spikes/queue-eligibility-decision-matrix.md` row A). Under ruling (3), the AutoSequence wire state may show READY_TO_DISPATCH before dispatch while the pump is off; this is reported, not hidden.
- **Still OPEN, not implemented by CP-1:** Job phase execution, Pump critical actions, Valve and Axis actions, Safe Return execution, critical reset and resume (D9), valve timing on critical events (D6), outcome vocabulary (D7), Safe Return failure recovery (D8), axis confirmation source (D10), POST command surface (D11), and queue source model (D12). Resume from PAUSED is not implemented in CP-1 and is refused.
- **Governance-state pointers:** `docs/ROADMAP.md`, `docs/MASTER_PLAN.md` (§3.3 Stage 0.4A), `docs/STAGE_0.3A_PLAN.md` (status note), `docs/decisions/ADR-0014` (dated Stage 0.4A reconciliation note; status remains DRAFT, formal acceptance is an Owner action), and `CHANGELOG.md`.

**CP-1 closeout and Final Source Review (2026-10-08, Stage 0.4A).** Recorded by the Owner-local validation evidence and the Arena read-only source review. This block supersedes the validation-pending wording in §2 and in MASTER_PLAN §3.3.

- **Owner-local validation of CP-0 and CP-1 — OWNER-LOCALLY VALIDATED.** Validated feature head `2cfe648d512241d9fef459cd91c663b9780753f9` (parent `1f76da84cb6ef4431b9946e6d6683068c56ed159`, CP-0; base `909d028`). Environment: .NET SDK 10.0.401; xUnit runtime .NET 10.0.12. Results as reported by the Owner: locked restore PASS, lock drift NONE; Release build PASS with 0 warnings and 0 errors; full .NET tests 217 total, 217 passed, 0 failed, 0 skipped; fixture parity 7/7; TypeScript typecheck PASS; TypeScript tests 31/31; boundary scan 0 findings (S1–S9 clean); `git diff --check` PASS; Owner-local working tree CLEAN. No Owner-local artifact commit was required because validation produced no repository drift. These results are Owner-local evidence; Arena did not run the .NET or TypeScript toolchain.
- **Arena checks in this closeout:** `git ls-remote origin refs/heads/main` = `909d028`; PR #7 head = `2cfe648`; PR #7 OPEN and NOT MERGED; `git diff --check 909d028 2cfe648` clean; `node tools/boundary-scan/boundary-scan.mjs .` = 0 findings. No `packages/contracts`, fixture, TypeScript, `apps/**` or `adapters/**` file differs from the base.
- **CP-1 behaviour, as validated:** GlobalQueue capacity 8; scenario-prepared dispatch-ready entries only; FIFO; head-only dispatch; no scan-forward; ninth admission refused atomically; duplicate admission is a no-op; active-target admission refused; exactly one Active Job; CRITICAL_SUSPENDED freezes Queue and Queue revision. Structural head revalidation removes an invalid head atomically with `REMOVED_BY_ELIGIBILITY`, creates no Active Job, and dispatches no other entry in that transition. Pump-not-ready is not a Queue refusal and not a Queue-entry status: after the atomic head dispatch the Job is created and exposes AutoSequence `PUMP_NOT_READY`; no cleaning or water output exists in CP-1; no next Job dispatches. Pause: RUNNING → PAUSED with no Job; RUNNING → PAUSE_REQUESTED with an Active Job; dispatch while PAUSE_REQUESTED is refused; Queue unchanged. Identical initial state and events give identical final state and byte-identical evidence serialization.
- **CP-1 Final Source Review (read-only, Arena) — result: FOLLOW-UP RECOMMENDED BEFORE MERGE (no blocking public-boundary defect).** Findings: (F1) `SequencingState` has a public positional constructor, so a caller can construct a state that bypasses the critical latch (`CriticalSuspended = false`), exceeds capacity, or carries a `Mode` value the kernel never produces (`CRITICAL_SUSPENDED` would project as running, because the projection's default arm treats any non-listed mode as running). The kernel does not validate its input state. No product path constructs states, so this is not reachable from the current codebase, but it is a safety-relevant boundary weakness and a precondition for CP-2. (F2) Two sequence spaces: the release check compares the external `SafeReturnReleaseEvidence.Seq` with the kernel's dispatch evidence sequence. CP-2 must use one ledger sequence. (F3) In CP-1, a critical raise does not enter Safe Return; a release with synthetic completion evidence is accepted while critical. This is an intended CP-1 limit and is CP-2 scope. No change was made in this closeout.
- **Review answers (summary):** (1) implementation matches the Owner rulings; (2) no forbidden Queue-entry status exists — `SequencingEntry` and the wire `QueueEntry` projection carry none; (3) Pump waiting is kept outside Queue entries (Job-level flag only); (4) dispatch is head-only, atomic and does not scan forward; (5) invalid-head removal returns before any dispatch code path, so no other entry is dispatched in that transition; (6) a single Active Job is enforced by the type and by the dispatch guard, subject to F1; (7) refused and no-op transitions preserve Queue revision (only the evidence sequence advances); (8) evidence sequencing is deterministic (counters only; no clock, random source or configuration); (9) Safe Return evidence is consumed only as synthetic input, and no Safe Return execution exists (subject to F2, F3); (10) no Runtime, API or Inspector integration was added (`apps/runtime` references Runtime.Core only through its existing types; nothing references `Sequencing`); (11) no contract, schema or fixture changed; (12) public type surface: see the classification below.
- **Public type surface classification (accessibility not changed in this closeout):** `SequencingState` — FOLLOW-UP RECOMMENDED BEFORE MERGE (public constructor, see F1). `SequencingEntry`, `SequencingActiveJob` — ACCEPTABLE FOR CURRENT RUNTIME.CORE TESTABLE KERNEL (they are element types of the public state; they become internal candidates only if the state surface is narrowed). `SequencingTransition`, `SequencingEvidence` — ACCEPTABLE (returned evidence is the testable output). `SequencingEvent` and its subclasses — ACCEPTABLE (the kernel input). `SafeReturnReleaseEvidence` — ACCEPTABLE FOR CURRENT KERNEL; its name suggests a Safe Return product artefact, so CP-2 should replace external supply with an internal producer (see the CP-2 proposal). Overall: **FOLLOW-UP RECOMMENDED BEFORE MERGE; not a blocking public-boundary defect**, because no product path constructs or consumes the state. If the Owner judges that a bypassable critical latch on the public surface is blocking, the PR should not be merged until F1 is resolved.
- **Intentionally not delivered in CP-1:** no UI and no Inspector. This is by design. The read-only Inspector is planned for CP-4 and is not authorized.
- **Still not authorized:** CP-2 coding (Cleaning Job, Mandatory Safe Return and Critical Pump kernel), CP-3 (SIMULATOR composition), CP-4 (read-only Inspector), any command surface, and TEST_HARDWARE or PRODUCTION device access. The CP-2 scope gate is proposed in MASTER_PLAN §3.3.1 and awaits Owner ruling.
- **Status of PR #7:** OPEN, NOT MERGED. Owner-only merge.

**CP-1 FINAL EVIDENCE AMENDMENT and F1 closure (2026-10-08, Stage 0.4A; documentation only).** This block supersedes, by dated append, the CP-1 Final Source Review classification of F1 and the validation head recorded above. The earlier text is historical and is not rewritten.

- **Remote verification (Arena):** `origin/main` = `909d02846febfe2383f4d712f04455447f62bab0`. PR #7 branch and head = `03144f43122bed2bd011b26886f663c2b46b79c4`. Ancestry verified: `7b8482438fc2f9470b026590bfb25b00d1e952e4` → `23b276f4918a339256c9db9be05a726a50e4cc38` → `03144f43122bed2bd011b26886f663c2b46b79c4`. PR #7 OPEN, mergedAt null. Local working tree clean.
- **Correction chain:** `1f76da8` (CP-0, parent `909d028`) → `2cfe648` (CP-1, parent `1f76da8`) → `27c939e` (CP-1 validation record, parent `2cfe648`) → `7b84824` (CP-2 scope gate proposal, parent `27c939e`) → `23b276f` (`fix(runtime): enforce sequencing state integrity`, parent `7b84824`) → `03144f4` (`fix(tests): use predicate assertion for public setters`, parent `23b276f`).
- **Authoritative Owner-local validation at `03144f43122bed2bd011b26886f663c2b46b79c4`** (Owner-reported; Arena did not run the .NET or TypeScript toolchain): .NET SDK 10.0.401; xUnit runtime .NET 10.0.12. Locked restore PASS; lock drift NONE. Release build PASS; 0 warnings; 0 errors. Full .NET tests 253 total, 253 passed, 0 failed, 0 skipped. Fixture parity 7/7. TypeScript typecheck PASS. TypeScript tests 31/31. Boundary scan 0 findings (S1–S9 clean). `git diff --check` PASS. Owner-local working tree CLEAN. No Owner-local artifact commit was required because validation produced no repository drift. The Owner reports that the state-integrity correction and the original Q1–Q13 behaviour pass.
- **Superseded counts:** the 217-test result recorded above is historical and applies to CP-1 at `2cfe648`. It is not the validation result for the current head.
- **Failure history (superseded):** the Owner-local Release build at `23b276f` reported one analyzer error, xUnit2029, at `SequencingStateIntegrityTests.cs` (a filtered-collection `Assert.Empty`). It was corrected in `03144f4` by `Assert.DoesNotContain` over the same predicate. Test intent is unchanged. No Product source, suppression, analyzer setting or project file changed.
- **F1 — amended classification: BLOCKING STATE-INTEGRITY DEFECT — CLOSED.** The earlier classification "FOLLOW-UP RECOMMENDED BEFORE MERGE, not a blocking public-boundary defect" is withdrawn. Root cause: `SequencingState` had a public constructor and public init mutation; caller-owned collections could alias state; `Apply` had no state-integrity guard; an inconsistent Mode / critical-latch combination could silently project as running. Correction in `23b276f`: internal constructor and internal init setters; queue copied on construction and exposed read-only; a deterministic state validator; invalid `Apply` fails closed with `SEQUENCING_STATE_INVALID`; invalid projections throw `InvalidOperationException` carrying the stable code; the default-as-running projection is removed; counter overflow is prevented by a checked increment. CLOSED by Owner-local validation at `03144f4`.
- **CP-1 Final Source Review: PASSED** (after F1 closure, at `03144f4`). Two CP-2 preconditions are carried forward. They are **not** CP-1 blockers: (F2) two sequence spaces — the release check compares the external Safe Return sequence with the kernel's dispatch evidence sequence, so CP-2 must unify the ledger; (F3) a release with synthetic completion evidence is accepted while the critical latch is set. F3 is the intended CP-1 limit and is CP-2 scope, consistent with the Owner's approved interpretation that Safe Return may continue under the latch and that the Active Job releases only after verified Safe Return.
- **CP-1 validated behaviour (Owner-local):** ready-only FIFO; head-only dispatch; no scan-forward; single Active Job; pump waiting remains outside Queue entries (AutoSequence `PUMP_NOT_READY` on the Job, never a Queue state); invalid states fail closed. No Job execution is implemented. No Safe Return execution is implemented. No UI is delivered in CP-1 by design. CP-4 remains the planned read-only Inspector checkpoint.
- **Stage status:** CP-0 OWNER-LOCALLY VALIDATED (included in the validated head). CP-1 OWNER-LOCALLY VALIDATED at `03144f4`. CP-2 scope gate PROPOSED ONLY (MASTER_PLAN §3.3.1); its prerequisite F1 is corrected and Owner-locally validated. **CP-2 CODING NOT AUTHORIZED. CP-3 NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** No CP-2 implementation has begun.
- **PR #7:** OPEN, NOT MERGED. Final Owner review requested; Owner-only merge. TEST_HARDWARE NOT AUTHORIZED. PRODUCTION DEVICE ACCESS NOT AUTHORIZED.
- **Scope of this commit:** documentation only, committed after `03144f4`. No change to packages, adapters, apps, tests, config, tools, spikes, project or lock files, fixtures, TypeScript, contracts, Runtime/API/Inspector, or CP-1 accessibility or behaviour.

The Owner accepted Stage 0.2 and it was merged through PR #2. Stage 0.2 was a documentation
and architecture-decision checkpoint only. Acceptance of ADR-0006 to ADR-0013 records
architecture direction; it is not implementation proof.

Approval of the Stage 0.2.1A Scope and Coding Start Gates authorises a synthetic, removable
React feasibility spike under `spikes/ui-runtime-react/`. It is **not** acceptance of the spike
result and **not** a UI framework selection. **Owner decision, 2026-10-07:** after the Owner-local
final Edge gate and the Owner manual review of `114c0761` passed, the Owner accepted the UI, the
synthetic AutoSequence controls, the GlobalQueue presentation, the critical Pump modal and the
Mandatory Safe Return behaviour as the **Development baseline**, recorded Stage 0.2.1A as
**COMPLETE FOR DEVELOPMENT CHECKPOINT**, and selected **React as the Primary UI Framework**. Stage
0.2.1A is **not merged** until the Owner merges PR #3, and this acceptance is not a Production
safety, stability, WebView2, kiosk, Modbus or hardware validation. The spike uses synthetic data
only: no device was contacted, no Production value is used, no Product directory exists, and
no Production Write was performed or authorised.

**Owner domain correction (Stage 0.2.1A).** The Owner corrected the physical baseline from 104
Sensor locations / 208 Thermocouple channels (Left 24, Rear 28, Right 24, Front 28) to **106
Sensor locations / 212 Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). The
Sensors sit in an 18-column × 6-row logical matrix with two Cannon equipment slots, logical I7
(Rear) and I16 (Front), which are not Sensors. See
[`DOMAIN_MODEL.md` §2.2.1](DOMAIN_MODEL.md#221-logical-sensor-matrix-and-cannon-slots-owner-confirmed).
This is a domain correction, not a runtime failure. Where 104 / 208 still appears in a
historical record below, it is the **superseded baseline**.

The synthetic process workload of the spike is deterministic for the same seed, scenario
timeline, synthetic configuration, and code revision. Cryptographic run and scenario tokens are
non-deterministic but do not affect process values, scenario ordering, the device latency
sequence, classification, or revision behaviour. Identical wall-clock timing is not claimed.

The Stage 0.1 process-deviation record in section 11.1 is retained unchanged.

## 3. Application version

| Item | Value |
| --- | --- |
| Application version | **NOT ESTABLISHED** |
| Runtime release | **None exists** |
| Documentation versioning policy | `[OPEN]` — no scheme is adopted |
| Former `0.1.0` label | A documentation-only working label. **Not** a product version, not a release. Retained in [`../CHANGELOG.md`](../CHANGELOG.md) only for historical traceability. |

No governance rule requires a documentation stage to advance a version number.

## 4. Repository state

| Item | State |
| --- | --- |
| Current stage | **Stage 0.4A CP-0 and CP-1 OWNER-LOCALLY VALIDATED (2026-10-08, validated head `03144f4`; F1 blocking state-integrity defect CLOSED; CP-1 Final Source Review PASSED); CP-2 scope gate PROPOSED ONLY (MASTER_PLAN §3.3.1; prerequisite F1 corrected and validated); CP-2 CODING NOT AUTHORIZED; CP-3, CP-4 NOT AUTHORIZED; PR #7 OPEN, NOT MERGED.** *Superseded 2026-10-09 for the CP-3 entries (see §2 rows 60 and the 2026-10-09 row): CP-2 validated (§3.3.3); CP-3a and CP-3b Owner-authorised and OWNER-LOCALLY VALIDATED at `a315e90` (MASTER_PLAN §3.3.5); CP-3c and CP-4 NOT AUTHORIZED.* Historical (superseded): Owner-authorised CP-0 and CP-1 only; earlier validated head `2cfe648`. Previous stage: Stage 0.3A-3 COMPLETE (merged to `main` via PR #6, `909d028`). Historical detail for the previous stage — Stage 0.3A-3 Checkpoint C — atomic Runtime topology migration + read-only Inspector integration, **OWNER-LOCALLY VALIDATED (2026-10-08)** at feature head `162ad7f` / Owner artifact commit `bac36add` (SDK `10.0.401`; normal+locked restore PASS; Release build 0 warnings / 0 errors; full .NET suite **201/201**; fixture generation+parity **7/7 and 7/7**; TypeScript 31/31; boundary S1–S9 clean; JSON examples PASS; worktree CLEAN); Function / Logic / Inspector UI-UX reviews **PASSED, no critical UI blocker**; minor punchlist deferred and non-blocking, Inspector presentation frozen. Correction chain `8567a78` → `440a7fa` → `162ad7f` validated by this run (§11.9, §12.29, §12.30, [`STAGE_0.3A-3_CHECKPOINT_C.md`](STAGE_0.3A-3_CHECKPOINT_C.md) §16). Stage 0.3A-2A Checkpoint C — read-only Runtime API + development Runtime Inspector, **OWNER-LOCALLY VALIDATED (2026-10-07)** schema `wjss.snapshot/2` / `wjss.delta/2` (Option A atomic update, no alias), no canonical Cannon entity current-facing, 8 WJ + 8 IV read-only topology in Runtime state/Snapshot/status, per-Sensor assignments, Inspector wall map with NON_SENSOR_GAP cells + installation overlay + equipment topology table (see §11.9, [`STAGE_0.3A-3_CHECKPOINT_C.md`](STAGE_0.3A-3_CHECKPOINT_C.md)). Stage 0.3A-2A Checkpoint C — read-only Runtime API + development Runtime Inspector, **OWNER-LOCALLY VALIDATED (2026-10-07)** at the lock-refresh commit `faa79145a925832baa2ac8685f7fecf7a093552d` (SDK `10.0.401`; locked restore PASS; Release build 0 warnings / 0 errors; fresh full .NET suite **139/139**; boundary S1–S9 clean; worktree CLEAN); Owner Function/Logic review PASSED and the Inspector UI/UX review PASSED — **final Owner visual review recorded, minor punchlist CLOSED** (one optional future polish item deferred as non-blocking; see §11.7, §12.27). Checkpoints A (`55d3b8b`) and B (`cfa6d4a`) are Owner-locally validated (A 101/101 — §12.24; B 129/129 — §12.25) |
| Repository contents | Documentation, repository governance, the Stage 0.2.1A synthetic spike in `spikes/ui-runtime-react/`, and the Stage 0.3A-1 product foundation source skeleton (`WaterJetSentinelSuite.sln`, `packages/`, `apps/`, `adapters/`, `tests/`, `config/examples/`, `tools/`) |
| Application source code | **Validated at checkpoint scope by the Owner-local run** (Release 0/0; 61/61; fixtures and lock files genuine, transferred and verified; SDK pinned `10.0.401`). Arena itself ran no .NET and claims no compile/test success of its own. The spike remains synthetic feasibility code, not Product code |
| Project or solution files | `WaterJetSentinelSuite.sln` (12 projects) — source-committed, **never built in the authoring environment** |
| Package manifests or dependencies | Spike: `spikes/ui-runtime-react/react-ui/package.json` + `package-lock.json` (Owner-approved pins); `runtime-harness/package.json` (no deps). Product: `Directory.Packages.props` (pins **PROPOSED/UNVERIFIED**), `packages/contracts/wjss-contracts-ts/package.json` + lockfile (`typescript@6.0.3`, install-verified in Arena). `global.json` pins the Owner-validated SDK `10.0.401` (`rollForward: latestPatch`, added at closeout per Owner decision); the twelve genuine Owner-local `packages.lock.json` are committed (transferred by handoff `488b98fb…`, never Arena-generated) |
| Database schema or SQL scripts | **Do not exist** |
| Modbus or Galil adapter | **Does not exist** |
| Simulator | **SIMULATOR-only source** — from Stage 0.3A-2A, `adapters/simulator/Synthetic/` holds the canonical synthetic map (`SyntheticSensorMap`: 108 slots / 106 Sensors / 2 Cannon slots I7+I16, scan order, device distribution 14+14+13×6, `SYN-TC-nn:CHmm` channel identities) and the seeded initial Sensor projection (`SyntheticSeed`, `DeterministicValueSource` — splitmix64, no `System.Random`, no static mutable state). Never a production-path component; no acquisition loop, no fault injection yet (Checkpoint B scope). The map composition is pinned to the committed `config/examples/sensor-map.example.json` by a parity test in `tests/runtime.tests`; the fixture-side generator (`tests/integration/FixtureGenerator.cs`) is untouched and its unification with the adapter remains `[OPEN]`. Checkpoint B adds the evolution source (`SyntheticEvolution`: explicit seed + tick sequence + clock, quality paths GOOD / UNCERTAIN (Last Validated basis) / STALE with deterministic recovery, bounded 0–100 unitless score walk). Checkpoint C composes that source into a runnable SIMULATOR runtime host (`apps/runtime`: explicit synthetic configuration, single-writer evolution lifecycle, read-only API) and adds the development Runtime Inspector page that observes it — source authored and statically reviewed, not compiled or executed in Arena. The adapter still has no acquisition loop, no fault injection and no device path, and the spike's Node harness remains feasibility-only |
| Automated tests | Spike tests (Node `node:test`, Vitest, Playwright — Owner-local). Product .NET tests (5 xUnit projects): **Stage 0.3A-2A Checkpoint A PASSED Owner-locally (2026-10-07)** — Release build 0 warnings / 0 errors and full suite **101 total / 101 passed / 0 failed / 0 skipped** on SDK `10.0.401` (xUnit observed runtime .NET 10.0.12), locked restore PASS, boundary scan S1–S9 clean, at validated feature head `a512aa7` + lock refresh `55d3b8b` (§12.24); the earlier 0.3A-1 baseline remains the 61/61 record (§12.22). Arena executed only the TS mirror (**24/24**) and never claims .NET execution itself. Checkpoint B is **PASSED Owner-locally (2026-10-07)** — Release build 0 warnings / 0 errors and full suite **129 total / 129 passed / 0 failed / 0 skipped** on the consolidated correction `cfa6d4a`, locked restore PASS, no lock drift, boundary S1–S9 clean (§12.25). Checkpoint C is **PASSED Owner-locally (2026-10-07)** at the genuine lock-refresh commit `faa79145a925832baa2ac8685f7fecf7a093552d` — Release build **0 warnings / 0 errors** and fresh full suite **139 total / 139 passed / 0 failed / 0 skipped** (129 Checkpoint B + 9 Checkpoint C additions + 1 Delta-feed continuity projection test), locked restore PASS, boundary S1–S9 clean (§12.27). Arena executed nothing itself and claims no .NET result of its own |
| CI workflow | **Does not exist** |
| Installer or release artifact | **Does not exist** |
| Production configuration | **Does not exist in this repository** |
| Device connections made | **None** |

## 5. Verified system state

- Stage 0.2 is a documentation and architecture-decision activity only. It created no runtime
  artefact and installed no dependency.
- Stage 0.2 selected a `[PROPOSED]` technology and solution architecture — UI delivery model,
  runtime process model, technology stack, database access and migrations, device adapter
  boundary, configuration and secrets model, simulator-first development model, and offline
  deployment model — recorded as ADR candidates in
  [`decisions/README.md`](decisions/README.md). **`[PROPOSED]` is not approval**; each record
  requires Owner acceptance, and nothing was implemented.
- The architecture was decided in a working environment that contains no .NET SDK, no SQL
  Server client tooling, and no Windows runtime. No build, restore, execution, or measurement
  was possible or performed.
- The Owner-requested Stage 0.2 documentation review punchlist was implemented as a
  documentation-only review-correction checkpoint. It recorded the legacy-application
  architecture evidence, returned the final UI framework selection to `[OPEN]`, added the fair
  React-versus-Blazor comparison, and added the UI workload, Sensor presentation, quality
  pipeline, live-state delivery, Modbus acquisition, configuration hot-path, Historian
  decoupling, trend, and proposed Stage 0.2.1 spike content. No code, dependency, device
  access, or Production configuration was created.
- Stage 0.1 is a documentation and repository-governance activity only.
- No application code has been written, and no code may be written until a later Stage Gate
  authorises it.
- No hardware has been accessed. No WAGO coupler, Galil controller, pump, valve, or DCS
  signal has been contacted, probed, or tested.
- No production IP address, register map, tag list, motion coordinate, travel limit, speed
  profile, pressure setpoint, production alarm threshold, DCS permissive definition,
  credential, or connection string has been created or committed.
- The documentation distinguishes approved behaviour (`[APPROVED]`), Owner review decisions
  (`[OWNER CONFIRMED]`), and proposals (`[PROPOSED]`).

## 6. Owner-confirmed decisions closed during documentation review

These were previously `[OPEN]` or ambiguously described. They are now settled and must not be
reopened without a new Owner decision.

| # | Decision | Where recorded |
| --- | --- | --- |
| 1 | Water Jet to Isolation Valve is exactly one-to-one, dedicated, never shared; a sensor's valve is derived from its assigned Water Jet | [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001..006, [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8 |
| 2 | AutoSequence executes Cleaning Jobs strictly sequentially; INVARIANT-SEQ-001 to 006 | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ group |
| 3 | Maximum active Cleaning Jobs equals one | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §3 |
| 4 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 5 | GlobalQueue behaviour on Operator stop, and queue rebuild on a new AutoSequence | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9, [`REQUIREMENTS.md`](REQUIREMENTS.md) QUE-024, QUE-025 |
| 6 | Source ownership after deduplication; refill uses the original source owner | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3, §5.4 |
| 7 | Active alarm acknowledgement is awareness only; a cleared-state acknowledgement is required to release a block | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 8 | "Allow unaffected Water Jets to continue" means sequential continuation only | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2 |
| 9 | DCS Permissive Override: operator activated, manually released, reason and audit required, banner, scoped exclusions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR group |
| 10 | Operations UI close guard required while a job is active or the pump runs; it is not safety protection | [`REQUIREMENTS.md`](REQUIREMENTS.md) UIG group, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 11 | Communication health must not depend on value change detection; configurable stale timeout | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 12 | Cross-wall DirtyScore tie-break is not required; GlobalQueue order comes from the fixed source order | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §2 |
| 13 | Baseline role for the DCS Permissive Override is **Operator**: the Operator role template holds permission to activate **and** release it; a role without the permission cannot do either. The permission model remains configurable, and the baseline assignment remains Operator until an explicit Owner decision changes it | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-011..OVR-015 |

## 7. Blocking constraints in force right now

1. **No production device access.** `[NOT AUTHORIZED]`
2. **No production valve or pump write control** until the bench verification in
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) is completed and recorded. `[NOT AUTHORIZED]`
3. **No application code, schema, adapter, simulator, test, CI workflow, installer, or
   release artifact** may be created until a later Stage Gate authorises it. `[NOT AUTHORIZED]`
   The only exception is the Stage 0.2.1A synthetic spike inside `spikes/ui-runtime-react/`,
   within its approved scope. `[APPROVED]`
4. **No production values** may be invented, inferred, or committed. `[NOT AUTHORIZED]`
5. **No concurrent Cleaning Jobs.** Parallel Water Jet cleaning is prohibited. `[OWNER CONFIRMED]`
6. **No merging.** Merge authority belongs to the Owner. `[APPROVED]`
7. **No history rewriting or force push** without specific Owner authorisation. `[APPROVED]`
8. **No reuse of PR #1 or PR #2.** Stage 0.2.1A is delivered through one new pull request to
   `main`, as instructed by the Owner. `[APPROVED]`

## 8. Open items requiring Owner decisions

These remain unresolved. None may be resolved by assumption. Items closed by the review
punchlist have been removed; see section 6 for what was closed. Items are grouped below by
theme rather than numbered, because the list changes as decisions are taken.

### 8.1 Product and technology

The Stage 0.1 open technology items were addressed by Stage 0.2. Each one is now either a
`[PROPOSED]` architecture decision awaiting Owner acceptance, an `[OPEN]` item with a named
follow-up gate, or a `[NOT VERIFIED]` item requiring hardware or workstation evidence. The
disposition table is in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 11, the remaining open
items are listed in section 34 of the same document, and the decision records are indexed in
[`decisions/README.md`](decisions/README.md).

| Item | Stage 0.2 disposition |
| --- | --- |
| Application language, runtime, UI framework, and UI delivery architecture | .NET with C# for the Equipment Runtime and Local Application API: `[PROPOSED]`; application-owned Windows kiosk shell: `[PROPOSED]`; embedded local web UI delivery model: `[PROPOSED]`; React + TypeScript + Vite: `[PROPOSED]` comparison candidate, not accepted; Blazor Hybrid: `[PROPOSED]` comparison candidate, not rejected; final UI framework: `[OPEN]`; exact .NET version: `[OPEN]`; final selection deferred; the Stage 0.2.1A React feasibility spike is approved and in progress; Blazor counter-spike deferred / not authorized. Browser-based, desktop, and hybrid local-web delivery were all evaluated on requirements |
| Process architecture, service identity, startup behaviour, and Equipment Runtime separation | Process model selected as `[PROPOSED]`; service identity `[OPEN]` |
| Modbus TCP client library selection and licence acceptability | Boundary decided; library selection `[OPEN]` pending licence and offline-availability review |
| Galil communication mechanism and library selection | `[OPEN]` — must be evaluated before motion code is written |
| Local configuration store format and its validation mechanism | Model selected as `[PROPOSED]`; format and secret store `[OPEN]` |
| Data access approach for SQL Server 2025 Standard | Approach selected as `[PROPOSED]`; provider version and compatibility `[OPEN]` |
| Logging, diagnostics, and crash-report storage | Strategy selected as `[PROPOSED]`; provider `[OPEN]` |
| Final documentation and application versioning scheme | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Deployment acceptance criteria | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Backup, restore, and off-box copy | Remains `[OPEN]` — carried forward into the offline deployment decision |
| Historian write-path measurement and overflow policy | Remains `[OPEN]` — requires measurement on the target workstation |
| Kiosk startup mechanism, package format, firewall rules, diagnostic bundle contents | Remains `[OPEN]` — deployment-gate items |
| **Final UI framework (Candidate A React + TypeScript + Vite versus Candidate B Blazor Hybrid)** | **CLOSED — React selected as the Primary UI Framework** (Owner decision, 2026-10-07) after the Stage 0.2.1A Owner-local final Edge gate and manual review passed. Blazor counter-spike not required unless a future material blocker is identified |
| Push transport and presentation-state payload encoding | `[OPEN]` — presentation contract is transport-agnostic |
| Chart / trend library; UI test tooling; visual regression tooling | `[OPEN]` — library-neutral requirements recorded; selection needs spike evidence |
| Site-specific invalid-value and sentinel mapping | `[OPEN]` — requires the Tag and data-quality contract; production values must never be committed |
| ORM, mapper, micro-ORM, provider, and bulk-write mechanism | `[OPEN]` — the earlier "mapper as the primary technology" wording was replaced with testable architecture language |
| Historian overflow, spool, retry, priority, and outage policy | `[OPEN]` — must be explicit before Historian implementation and separated from the audit-required refusal policy |
| Poll Plan batching limits, poll-group intervals, concurrency bound, runtime recompilation | `[OPEN]` — requires device evidence and local Production configuration |
| Sensor cell colour tokens, typography, dimensions, animation; accessibility requirement level | `[OPEN]` — deliberately deferred to a future UX/UI decision |
| Acceptance thresholds for the Stage 0.2.1 spike | `[OPEN]` — must be set before the spike runs |

### 8.2 Sequence, queue, and equipment behaviour

| Item | Affects |
| --- | --- |
| Fault class taxonomy: when to stop motion versus abort motion | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Recovery procedure for `RECOVERY_REQUIRED`; whether retry is permitted; whether partial-path jobs may resume | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Permission required for each operator queue action, and required reason text | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Reject / Release Reject / Reorder semantics in a ready-only, head-only GlobalQueue (**OWNER DECISION REQUIRED**) | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Queue snapshot storage format and retention inside Event history | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §11 |
| Exact motion limits, profiles, homing, pulses per engineering unit, and operational envelope | [`REQUIREMENTS.md`](REQUIREMENTS.md) GAL-006 `[NOT VERIFIED]` |
| Exact pressure setpoints, rise timeout, stable dwell, and valve open/close timeouts | [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-005, VLV-003 `[NOT VERIFIED]` |
| Sensor-to-Water-Jet mapping and exact production coordinates | Deployment data, `[NOT VERIFIED]` |

**Resolved (DP-01):** the former open item *row alignment rule for walls of unequal size (24
versus 28)* is closed. That wording used the superseded 28-Sensor Rear and Front walls. The wall
distribution is **24 / 29 / 24 / 29** (Left / Rear / Right / Front). The Owner-confirmed
canonical **18-column × 6-row** logical matrix aligns the logical rows of all four walls; the
Cannon slots occupy logical **I7** (Rear) and **I16** (Front); the Sensor identifiers are the
Owner logical labels (`G+2xx`, `G+1xx`, `G`, `H`, `I`, `J`). This is no longer an open mapping
question. Production scan order, device distribution, and Water Jet assignment stay
deployment data, `[NOT VERIFIED]`. See [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.2.1.

### 8.3 DCS and communication

| Item | Affects |
| --- | --- |
| Exact DCS permissive definition set that the override bypasses | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §9 |
| DCS heartbeat hardware contract, if a heartbeat feature is pursued | [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH-004 `[OPEN]` |
| Production register map, Tag List, and signal quality contract | `[NOT VERIFIED]` |
| Exact local equipment network topology and address plan | [`ARCHITECTURE.md`](ARCHITECTURE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) PHY-008 |
| Stale-data timeout production value (30 s is an example only) | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5 |

### 8.4 Alarms, users, and data

| Item | Affects |
| --- | --- |
| Alarm definition catalogue, thresholds, blocking scopes, and non-shelvable list | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10 |
| Shelving duration limits and per-alarm maximums | [`ALARM_MODEL.md`](ALARM_MODEL.md) §6 |
| Whether active awareness acknowledgement and cleared-state acknowledgement share a field or a permission | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10, [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Authentication method, password policy, lockout, and privileged timeout default | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Whether named individual accounts are introduced in a future approved scope | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Retention default ratification; aggregate function definition; partition scheme; index strategy | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Backup, restore, and disaster-recovery approach | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Audit and event tamper protection | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §6, [`../SECURITY.md`](../SECURITY.md) |
| Behaviour when the Historian is unavailable | [`ARCHITECTURE.md`](ARCHITECTURE.md) §9 |
| Workstation clock discipline and drift bounds | [`ARCHITECTURE.md`](ARCHITECTURE.md) §7 |
| Target test coverage thresholds and test execution tooling | [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §7 |
| Critical Main Pump / Mandatory Safe Return policies: Safe Return failure, valve / axis failures, outcomes, re-queue, retry, acknowledge role, clear evidence, Resume authority, modal minimise, second alarm channel | [`spikes/critical-pump-safe-return-decision-matrix.md`](spikes/critical-pump-safe-return-decision-matrix.md) (every row `OWNER DECISION REQUIRED`) |
| Production Pause / Resume semantics (pause the AutoSequence or the Cleaning Job lifecycle) replacing the superseded queue-level `HELD` (**OWNER DECISION REQUIRED**) | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2 |

## 9. Not verified — standing list

| Item | Reason |
| --- | --- |
| WAGO watchdog fail-safe behaviour and timeout value | No bench test performed; not authorised |
| Hardware fail-safe on Ethernet loss, process termination, workstation reboot, WAGO reboot, stale command replay, auto re-energization | No bench test performed; not authorised |
| Production hardware response to any output command | No device access; not authorised |
| Mechanical limits, soft limits, and operational envelope | Commissioning values; not measured |
| Pulses per engineering unit | Commissioning value; not measured |
| Encoder behaviour and whether feedback performs active correction | Not verified by engineering |
| Homing behaviour | Commissioning value; not measured |
| Speed, acceleration, deceleration profiles | Commissioning values; not measured |
| Pressure-ready setpoint, stable dwell, pressure rise timeout | Commissioning values; not measured |
| Valve open and close timeouts | Commissioning values; not measured |
| Stale-data timeout production value | Configurable; no production value captured |
| Production network topology and address plan | Confidential deployment information |
| Production register map, Tag List, and DCS signal contract | Confidential; never committed |
| Sensor-to-Water-Jet and sensor-to-valve mapping | Deployment data; not recorded here |
| Historian effective capacity and database sizing | No capacity model produced; no benchmark performed |
| Runtime build, unit tests, integration tests, database tests, hardware tests | No Product code exists; not applicable. Stage 0.2.1A synthetic spike tests are recorded in section 12.3 and are not Product verification |
| Stage 0.2.1A WebView2, kiosk, end-to-end latency, long-run UI, Windows offline restore | Not measurable in Arena and not validated. The installed-Edge Owner-local final gate and manual review **PASSED** at `114c0761` (Owner-reported, 2026-10-07); controlled 15- / 60-minute observations waived as merge blockers, not run |

## 10. Sensitive data review

| Check | Result |
| --- | --- |
| Production IP addresses in the repository | None found |
| Credentials, tokens, keys, or password hashes | None found |
| Connection strings | None found |
| Production register map, Tag List, or addresses | Not present and must never be added |
| Motion coordinates, travel limits, speed profiles | Not present |
| Pressure setpoints or production alarm thresholds | Not present |
| DCS permissive definitions | Not present |
| Production configuration content | Not created. Only documentation references and `.gitignore` exclusion patterns exist. |

The checks behind this table are listed with their observed results in section 12.

## 11. Checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.1 |
| Branch | `arena/01a1080d-waterjet-sentinel-suite` |
| Original implementation checkpoint | `0323f8a5a06bad25383dbe636b75c46ab38455af` |
| Original checkpoint status | **SUBMITTED FOR OWNER REVIEW** |
| Documentation review | **CHANGES REQUESTED** |
| Owner manual review | **PENDING** |
| Merge state | **NOT MERGED** |
| Pull request | https://github.com/tnoiiee/waterjet-sentinel-suite/pull/1 |
| Review-correction checkpoint | `899a96a5b01a8e3cfcf0aaf2468ff83ce735090e` — on the same branch and the same pull request |
| Final targeted-correction checkpoint | A final targeted-correction commit on the same branch and the same pull request. Its SHA is recorded in the delivery report and the pull request description, not inside the commit that creates it. |
| Stage 0.1 final merge state | **MERGED** to `main` through PR #1. The merge commit is `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`, which is the approved remote-main base of Stage 0.2 |
| Stage 0.2 | **AUTHORIZED** by the approved Stage 0.2 Scope Gate |

Each correction is a new commit on the existing branch. No reviewed checkpoint was amended,
rebased, or rewritten.

### 11.2 Stage 0.2 checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.2 — Technology and Solution Architecture Decision |
| Approved main base | `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a` (verified remote `main`) |
| Original Stage 0.2 architecture checkpoint | `b881a5fc7abac226e6c40f9cd01ec66d3ebf07c9` |
| First Stage 0.2 review-correction checkpoint | `058a4255fd87a943d73c1def7f27f839cee222ff` |
| Final documentation-consistency checkpoint | Created on the Stage 0.2 branch. Its SHA is recorded in the delivery report and in the pull request description, **not** inside the commit that creates it |
| Current Stage branch | `arena/01a1087c-waterjet-sentinel-suite` |
| Pull request | https://github.com/tnoiiee/waterjet-sentinel-suite/pull/2 |
| Stage 0.2 Scope Gate | **APPROVED** |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED** |
| Source checkpoint accepted | `5bcf1b33f924ab30590a55736676200115874fa1` |
| Merge | **MERGED** to `main` through PR #2 — merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`, which is the approved base of Stage 0.2.1A |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

*Rows above that described the Stage 0.2 review-in-progress state were replaced when the Owner
accepted and merged Stage 0.2.*

### 11.3 Stage 0.2.1A checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.2.1A — React UI and Runtime Feasibility Spike (synthetic) |
| Approved main base | `e779f8ad2c856e367fd65985007a3da411bd0e73` (verified remote `main` before coding) |
| Stage branch | `arena/01a108d8-waterjet-sentinel-suite` |
| Development checkpoint | `dd20a8bd` (104-location map, superseded by the Owner domain correction) |
| Sensor-map correction checkpoint | `935973e6ff13aca26efa67148977db44b78be2e1` — normal fast-forward on top of `dd20a8bd` |
| Fullscreen UI refinement checkpoint | A normal fast-forward commit on top of `935973e6`. Its SHA is recorded in the delivery report and in the PR #3 description, **not** inside the commit that creates it |
| Local recovery before the correction | An Owner-authorised, one-time local reference recovery set the local branch to the already-pushed `dd20a8bd` after an exact identity proof. No history was rewritten, nothing was force-pushed, and AGENTS.md is unchanged |
| Local recovery before the fullscreen refinement | The Arena sandbox was recreated (local branch at `e779f8ad`, checkpoint source present as working-tree changes). A second Owner-authorised, one-time, checkpoint-specific recovery: targeted fetch, complete-tree identity proof against `935973e6` (126 / 126 exact blob matches, 0 mismatches, 0 missing, 0 extra, 0 mode differences), compare-and-swap `update-ref` `e779f8ad` → `935973e6`, `read-tree` **without** `-u`; working tree unchanged. AGENTS.md is unchanged; this is not a general recovery rule |
| Fullscreen UI refinement checkpoint SHA | `ea23bc589b2f61a9c97aaa2b1a1faa9d2ac31d68` |
| Readability refinement checkpoint | A normal fast-forward commit on top of `ea23bc58` (presentation refinement plus the stale MAP U-shape assertion correction). Its SHA is recorded in the delivery report and in the PR #3 description, **not** inside the commit that creates it |
| Local recovery before the readability refinement | The Arena sandbox was recreated again (local branch at `e779f8ad`, checkpoint source present as working-tree changes). A third Owner-authorised, one-time, checkpoint-specific recovery: targeted fetch, complete-tree identity proof against `ea23bc58` (130 / 130 exact blob matches, 0 mismatches, 0 missing, 0 extra, 0 mode differences), compare-and-swap `update-ref` `e779f8ad` → `ea23bc58`, `read-tree` **without** `-u`; working tree unchanged. AGENTS.md is unchanged; this is not a general recovery rule |
| Pull request | PR #3 to `main` — **OPEN** (PR #1 and PR #2 are not reused) |
| Scope Gate / Coding Start Gate | **APPROVED** |
| Implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT** (`114c0761`) |
| Owner-local testing | Owner-local final Edge gate at `114c0761`: **PASS** (Owner-reported, 2026-10-07) |
| Owner manual UI re-review | **PASS** at `114c0761` (Owner-reported) — controlled 15- and 60-minute observations **waived as merge blockers** |
| Owner-local interrupted overnight observation (`ea23bc58`) | **COMPLETED** as reported by the Owner — not a controlled benchmark; Production stability **NOT VERIFIED** |
| Merge | **NOT MERGED** — ready for Owner merge |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07) |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |

### 11.4 Stage 0.3A-1 Product Foundation Source Checkpoint record

| Item | Statement |
| --- | --- |
| Gate | Owner **Option-C amended gate** — Arena may author the approved 0.3A-1 source set; it may not compile .NET (SDK/NuGet blocked by the sandbox network) and must not claim build/test success |
| Authored | Solution (12 projects); `Wjss.Contracts` (18 files) incl. queue capacity 8 + head-only consumption + profile-start fail-closed; Domain/Runtime.Core/Time/Simulator sources; Runtime health stub; compile-only Kiosk; 5 xUnit projects incl. the .NET golden-fixture generator + parity gate; TS mirror + validator; 4 provisional fixtures + 2 config examples; `tools/boundary-scan` (S1–S7, 0 findings); DRAFT ADR-0014/0015/0016; stage plan + Owner-local runbook; `.gitignore` product-`packages` conflict corrected |
| Wire-contract correction | The checkpoint draft's `activeJobCleared` flag was **rejected by Owner review**; the accepted three-state baseline (absent = unchanged / object = replace / `null` = clear) is restored, implemented via the scoped `Optional<T>` presence wrapper (structural technique, ADR-0014 §2) |
| Fixture provenance | Node-authored in Arena, labelled `PROVISIONAL STRUCTURAL FIXTURE`; **not** .NET-generated; superseded by the Owner-local generator run (runbook §5) |
| Arena verification | TS: `npm ci` + `tsc --noEmit` clean + `node --test` 14/14 PASS + fixtures validate; boundary scan exit 0; all JSON parses; links/whitespace/secret checks — see §12.12 |
| NOT verified | Every .NET claim: restore, build, tests, health-stub behaviour, kiosk, parity between C# generator and committed fixtures, NuGet pin availability |
| Delivery | Single commit on `arena/dd551752-waterjet-sentinel-suite`; PR "Stage 0.3A-1: product foundation source skeleton" → `main`; **NOT READY FOR MERGE** until runbook verdict PASS |
| Next | ~~Owner-local runbook PASS~~ **DONE** → Owner merges PR #4 (READY FOR OWNER MERGE — closeout 2026-10-07, see §12.22) → Owner explicitly authorizes **0.3A-2** (nothing advances otherwise; no ZIP before Stage 0.3 exit) |
| CLOSEOUT (2026-10-07) | Rows above describing "Node-authored fixtures", "NOT READY FOR MERGE" and blanket "NOT verified" are preserved as authored-time state. Authoritative current state: Owner-local Release build PASS (0/0), fresh suite **61/61**, TS 24/24, parity 7/7, boundary clean; fixtures and the 12 lock files are the genuine .NET/restore outputs transferred via handoff `488b98fb…`; SDK pinned `global.json` → `10.0.401`. **PR #4 READY FOR OWNER MERGE — NOT MERGED**; 0.3A-2 and Production devices NOT AUTHORIZED |

### 11.5 Stage 0.3A-2A Runtime State Foundation — Checkpoint A record

| Item | Statement |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Stage 0.3A-2 (Runtime State Foundation) coding AUTHORIZED for the **SIMULATOR profile only**, delivered as three checkpoints A → B → C on one branch and one pull request, from approved remote `bdf7f8f277b85087e3e53c067fe956fca7e04f90`. Arena still cannot compile .NET; sources are authored and statically reviewed only |
| Checkpoint A scope | In-memory Runtime State Store (single authoritative writer, immutable revisions, monotonic and gapless revisions, atomic refusal of invalid updates, bounded revision history, no mutable collection leakage, no static mutable state) + deterministic SIMULATOR initial state from the existing synthetic map (108 slots / 106 Sensors / 212 channels, Cannon slots I7 + I16) + `wjss.snapshot/1` projection. No Delta, no SSE, no command route, no queue dispatch, no pump/valve/axis control, no configuration write |
| Authored (Checkpoint A) | `packages/application/Runtime/` (11 sources: state, refusal codes, limits, collections, wall summaries, invariants, freezer, revision activity, store, composer, projector); `packages/time/UtcTimestamps.cs`; `adapters/simulator/Synthetic/` (3 sources); `tests/runtime.tests/` (5 new test sources, migrated stage-marker test, simulator project reference); adapter README, two csproj comment/reference updates, and this documentation record |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**) run on the changed worktree; final-newline / trailing-whitespace / tab checks; brace-balance script over every changed C# file; contract-member cross-check of every referenced type against `packages/contracts`; JSON re-parse of `config/examples/sensor-map.example.json` used by the parity test. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the new tests); the `tests/runtime.tests/packages.lock.json` refresh required by the new `Wjss.Adapters.Simulator` project reference (real Owner-local restore only, never hand-edited); the deliberate difference between the seed-derived synthetic presentation values and `tests/integration/FixtureGenerator.ScoreFor` (fixture formula preserved, not claimed equal); Checkpoints B and C (not started) |
| Delivery | Branch `arena/873f0015-waterjet-sentinel-suite`; **Checkpoint A commit `f51408e400935f7dd899d27544d8e7d9d18949b5`** (parent `bdf7f8f277b85087e3e53c067fe956fca7e04f90`, the approved remote `main` base); one pull request opened from this branch → `main` for all three checkpoints. The SHA above is recorded by this follow-up documentation commit, because AGENTS.md §4.6 forbids recording a commit's own SHA inside the commit that creates it. Checkpoint A status: **SUBMITTED FOR OWNER REVIEW — NOT MERGED** (the Agent never merges) |
| Correction chain (all pushed to the same branch/PR) | `b389805` CA1859 correction (concrete `ReadOnlyCollection<TrendPoint>` return); `93e8f24` CS0051 correction (`public enum Tamper` for the xUnit theory); `b75bccb` CS1061 + CA1859 synthetic-example test compile correction (`JsonSerializer.Deserialize<List<SensorMapSlotExample>>` on the slots node, concrete helper return type); `a512aa7` SensorChannels tamper isolation (fixture preserved ScanOrder/identity so the pinned code `SENSOR_TC_CHANNELS` is reached) |
| Genuine lock refresh | `55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c` — Owner-local real restore; changes exactly `tests/runtime.tests/packages.lock.json` (+7 lines) adding the `wjss.adapters.simulator` Project dependency with `Wjss.Contracts` and `Wjss.Time`. No absolute Owner-local path, no other file changed |
| Owner-local validation (2026-10-07) | **PASSED** at feature head `a512aa76c4b2d7633d2a83b10c523c318b3a420e` + lock refresh `55d3b8b`: .NET SDK **10.0.401**, xUnit runtime **.NET 10.0.12**; Release build **0 warnings / 0 errors**; full .NET suite **101 total / 101 passed / 0 failed / 0 skipped**; **locked restore PASS**; boundary scan **0 findings (S1–S9 clean)**; Working Tree clean after the lock commit and push. Recorded in §12.24 |
| Status | **Checkpoint A COMPLETE for development checkpoint** (Owner-locally validated; SUBMITTED FOR OWNER REVIEW — NOT MERGED, the Agent never merges) |
| Next | **Checkpoint B AUTHORIZED** (deterministic synthetic evolution + Snapshot/Delta foundation) — see §11.6; Checkpoint C (read-only API + lightweight Runtime Inspector) remains **NOT AUTHORIZED** |

### 11.6 Stage 0.3A-2A Checkpoint B — deterministic synthetic evolution + Snapshot/Delta foundation

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Checkpoint A of Stage 0.3A-2 is Owner-locally validated (build 0/0, suite 101/101, locked restore PASS, boundary S1–S9 clean at feature head `a512aa7` + lock refresh `55d3b8b`), and **Checkpoint B is authorized** on that validated store, SIMULATOR profile only. Checkpoint C remains NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION remain NOT AUTHORIZED |
| Delivered (source) | `packages/application/Runtime/`: `RuntimeSyntheticEvolution` (rules, tick outcome, deterministic walk and quality schedules), `RuntimeDelta` (apply-safe Delta plus the three-state Active Job slot), `RuntimeDeltaProjector` (Runtime-side and `wjss.delta/1` wire projection), `RuntimeDeltaApply` (strict total-or-nothing application), `RuntimeDeltaHistory` (bounded history and catch-up/gap results), `RuntimeTrendBuffer` (shared bounded append rule); the clock helper gains strict wire-timestamp parsing; `RuntimeRefusalCodes` gains `EVOLUTION_TICK_SEQUENCE`, `EVOLUTION_TICK_TIME`, `DELTA_OUT_OF_ORDER`, `RESNAPSHOT_REQUIRED`; `RuntimeLimits` gains explicit Delta-history capacities. Tests: `SyntheticEvolutionTests`, `SnapshotDeltaTests`, `DeltaApplyTests`, `DeltaHistoryTests` and the scenario helper `RuntimeDeltaTestFixture` |
| Semantics | Deterministic evolution from explicit (state, seed, tick number, tick instant) only; revision +1 exactly once per accepted tick; one committed state per accepted tick; refusals carry no candidate and change nothing; wall summaries recalculated from the complete evolved Sensor collection; bounded trend append with deterministic oldest-drop; `wjss.snapshot/1` retained; `wjss.delta/1` gapless with whole-record replacement and the exact three-state Active Job encoding; strict apply path (revision mismatch ⇒ machine-readable resnapshot-required, nothing applied, nothing normalized); bounded in-memory Delta history with deterministic oldest-eviction; gap detection only (no fabricated Delta, no continuation past a gap, no reconnect/retry/SSE) |
| Deliberately absent | Runtime API routes beyond the existing health stub, Runtime Inspector UI, SSE, write/command endpoints, queue dispatch, job execution, Pump/Valve/Axis/Cleaning commands, Safe Return actuation, configuration writes, persistence/Historian, auth, installer/release work, `spikes/**` changes, `apps/runtime/Program.cs` changes |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**); brace-balance, trailing-whitespace, final-newline and tab sweeps; `using`-directive resolution sweep (missing/unused namespaces); declaration sweep for every referenced type; determinism grep for ambient clocks and `System.Random`; `git diff --check`. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the new tests); the behaviour is **source authored / statically reviewed / NOT COMPILED and NOT EXECUTED in Arena**. Owner-local validation per runbook §13 is the validation of record |
| Correction chain (Owner-local build rounds) | `dd45bf6` CS0102 disambiguation (the `DeltaApplyOutcome.Applied` property kept; the success factory renamed to `Success`); `8692b77` test-source alignment (named `seed:` arguments, four CA1861 constant arrays hoisted to named `private static readonly` fields); `cfa6d4a` consolidated correction — **the only Product behaviour change of the three: `RuntimeDeltaHistory.CatchUpFrom` now selects the Delta that CONTINUES the consumer revision (`previousRevision` match) instead of the Delta whose own revision equals it**, which removed a spurious fresh-Snapshot result and a non-terminating walk; the remaining five findings were test-side (two structural `TrendPoint` comparisons via the shared `RuntimeTestFixture.AssertTrendPointsEquivalent` helper because `Series` is an array and record equality compares it by reference; three derived revision-expectation corrections: chain starts at revision 2, capacity-3 retention 4/5/6, catch-up input 3) |
| Delivery | Same branch and PR #5, after the Checkpoint A evidence commit; **NOT MERGED** (only the Owner merges) |
| Owner-local validation (2026-10-07) | **PASSED** at consolidated correction `cfa6d4a376bbf10a87db2349045cb6b9b57bb544`: Release build **0 warnings / 0 errors**; full .NET suite **129 total / 129 passed / 0 failed / 0 skipped**; **locked restore PASS**; **lock drift NONE**; boundary scan **0 findings (S1–S9 clean)**; Working Tree **CLEAN**. Recorded in §12.25 |
| Status | **Checkpoint B COMPLETE for a development checkpoint** (Owner-locally validated; SUBMITTED FOR OWNER REVIEW — NOT MERGED). Checkpoint B is **OWNER-LOCALLY VALIDATED** |
| Next | **Checkpoint C AUTHORIZED and delivered as source** — SIMULATOR runtime composition, deterministic evolution lifecycle, read-only Runtime API (`GET /api/v1/snapshot`, `GET /api/v1/runtime`, `GET /api/v1/deltas`), readiness semantics and a development-only Runtime Inspector; see §11.7. Owner-local Function/Logic/UI review per runbook §14 is the validation of record |

### 11.7 Stage 0.3A-2A Checkpoint C — SIMULATOR runtime composition, read-only Runtime API and development Runtime Inspector

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-07**: Checkpoint B is Owner-locally validated (`cfa6d4a`; build 0/0, suite 129/129, locked restore PASS, boundary S1–S9 clean) and **Checkpoint C is authorized** on that foundation, SIMULATOR profile only. `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED, as do PR merge, ZIP/release and any write surface |
| Delivered (source) | `apps/runtime/`: `RuntimeHostOptions.cs` (explicit synthetic configuration, safe development defaults, bounded validation, refusals as values), `SimulatorRuntime.cs` (composition of the initial revision from the adapter synthetic map through the Runtime composer, established initial Snapshot projection, single-writer evolution lifecycle, readiness answers with machine codes, fault observation), `RuntimeApiResponses.cs` (read-only status, Delta-feed and Sensor-view projections), rewritten `Program.cs` (profile → port → configuration → composition → lifecycle start → listener, then the read-only surface); `packages/contracts/ApiRoutes.cs` gains `Snapshot`, `Runtime`, `Deltas`, `Inspector`; `HealthPayloads.cs` documentation follows the substage that now exists; `RuntimeStage.Marker` moves to `STAGE_03A2C_RUNTIME_API`; `apps/runtime/Inspector/index.html` is the development Inspector page; `Wjss.Runtime.csproj` references `Wjss.Adapters.Simulator` and ships the page with the build output; `tests/api.tests` references the host project and adds `RuntimeHostCompositionTests`; `tests/config.tests/RuntimeStubPreservationSourceTests.cs` is renamed to `RuntimeStartGateSourceTests.cs` and rewritten for the new readiness contract |
| Routes and shapes | `GET /health/live` → 200 `{status: ALIVE, host, deviceProfile, runtimeImplemented: true, stageMarker}` (liveness only, never a readiness claim); `GET /health/ready` → 200 `{status: READY, code: RUNTIME_READY, detail, stageMarker}` when every readiness condition holds, else 503 with the same shape and the refusing machine code; `GET /api/v1/snapshot` → current `wjss.snapshot/1` projection (503 + code while no state exists); `GET /api/v1/runtime` → read-only status; `GET /api/v1/deltas` → bounded recent Delta activity with a reported gap flag; `GET /inspector` → the development page. Every payload serializes with `ContractJson.Options`; the host configures no second JSON policy. No write, command, dispatch or SSE surface exists anywhere |
| Readiness codes | `RUNTIME_READY` (200) plus the refusals `CONFIGURATION_NOT_VALIDATED`, `PROFILE_NOT_SIMULATOR`, `EVOLUTION_NOT_STARTED`, `STORE_NOT_INITIALIZED`, `REVISION_NOT_INITIALIZED`, `INITIAL_SNAPSHOT_UNAVAILABLE`, `STARTUP_FAULT`, `FATAL_RUNTIME_FAULT` (all 503, machine-readable, no silent fallback). The store, revision and projection preconditions are verified when readiness answers rather than assumed, so a failure fails closed instead of reporting a ready runtime |
| Lifecycle semantics | Exactly one non-overlapping evolution loop per runtime; one accepted tick = exactly one committed revision through the store's single writer; the timer paces execution while every presented value stays a function of (seed, tick number, committed state); a refused tick records a fault and a rejected-transition counter, emits no Delta and advances no revision, and tick instants advance monotonically so one refusal can never stall the loop; consecutive tick failures are bounded; shutdown cancels the loop through the `CancellationTokenSource` and observes it without rethrowing; a composition fault keeps readiness at 503 with `STARTUP_FAULT` instead of a silently degraded runtime |
| Inspector | Single static page served from the build output (no Node pipeline, no framework): ~1 s GET-only polling of the three read-only endpoints with non-overlapping requests, a visible stale-data banner when polling fails or stops, bounded recent Delta rows, wall-grouped Sensor table with score/classification/quality/reason, foundation metrics, wall summaries, runtime state with the placeholder labelling, a Snapshot facts block and a copy-Snapshot-JSON action. Local interactions (pause display, density, wall and quality filters, copy) never alter Runtime state; no control affordance exists |
| Deliberately absent | Write API, Runtime commands, queue dispatch, Start/Pause/Resume/Abort of a Cleaning Job, Pump/Valve/Axis commands, Safe Return execution, TEST_HARDWARE access, PRODUCTION access, physical device adapters, Modbus/Galil/KMotion/PLC, database, Historian persistence, authentication expansion, WebView2 shell migration, full Product UI migration, SSE, installer, ZIP, release, deployment, `spikes/**` changes |
| Arena verification | Boundary scan exit 0 (**0 findings, S1–S9 clean**) on the changed worktree; single inline Inspector script extracted and syntax-checked with `node --check`, HTML tag balance checked; per-file brace-depth balance over every changed C# source; trailing-whitespace / final-newline / tab sweeps; contract-member cross-check of every referenced type and member against `packages/contracts` and `packages/application/Runtime`; duplicate-simple-name scan across the `Wjss.Contracts` / `Wjss.Runtime` / `Wjss.Runtime.Core` namespaces; `git diff --check`. **No .NET restore, build, or test execution happened in Arena** |
| NOT verified | Every .NET claim (restore, Release build, xUnit execution of the authored tests); the two lock files the new project references are expected to refresh — `apps/runtime/packages.lock.json` and `tests/api.tests/packages.lock.json` (real Owner-local restore output only, never hand-edited); the actual browser behaviour, layout, scrolling and polling of the Inspector; the Owner-local Function/Logic/UI review. The runtime behaviour is **source authored / statically reviewed / NOT COMPILED and NOT EXECUTED in Arena** |
| Delivery | Same branch `arena/873f0015-waterjet-sentinel-suite` and PR #5, after the Checkpoint B evidence commit. Slice C1+C2 is `06aca79d03c57b703768afffecf95735de7a91d5` (`feat(runtime): compose the simulator runtime, lifecycle and read-only API`); slice C3 — the Inspector page, the Inspector source test and runbook §14 — is the commit immediately following it, and per [`AGENTS.md`](../AGENTS.md) §4.6 that commit does not record its own SHA (it is reported in the PR/Agent delivery report instead). **NOT MERGED** (only the Owner merges) |
| Owner-local validation (2026-10-07) | **PASSED** at the genuine lock-refresh commit `faa79145a925832baa2ac8685f7fecf7a093552d`: SDK `10.0.401`; locked restore **PASS**; Release build **0 warnings / 0 errors**; fresh full .NET suite **139 total / 139 passed / 0 failed / 0 skipped**; boundary scan **0 findings (S1–S9 clean)**; worktree **CLEAN**. Owner **Function/Logic review PASSED** (runbook §14.9). Recorded in §12.27 |
| Owner final visual review (2026-10-07) | **PASSED** — **FINAL OWNER VISUAL REVIEW (2026-10-07): Inspector UI/UX review PASSED; the minor punchlist is CLOSED.** Verified visually at 1920 x 1080: human-readable Foundation captions present; Queue and Pump placeholder explanations visually secondary; `SENSOR VALUES — SYNTHETIC EVOLUTION` title present; Foundation Metrics complete two-column layout; Runtime State uses the available width; no one-word-per-line wrapping remains; Snapshot compact; Delta and Sensor tables retain card-local scrolling; `HH:mm:ss.mmm` timestamps; Delta chain displays clean; I7 and I16 are the visible Cannon labels; synthetic UNCERTAIN and BAD quality states visible with reasons; no page-level horizontal scroll; safety footer and read-only boundary visible; no Critical UI blocker remains. One **optional future polish** item is recorded and explicitly non-blocking (top-row vertical balance, because Foundation Metrics is taller than Runtime State): it is deferred, does not require a change round, and is not authorised as part of Checkpoint C. Recorded in §12.27 |
| Lock handoff (2026-10-07) | `faa79145a925832baa2ac8685f7fecf7a093552d` (`chore: refresh Checkpoint C project locks`) — exactly `apps/runtime/packages.lock.json` and `tests/api.tests/packages.lock.json`, genuine Owner-local restore output (no package version changed); Arena verified it statically and adopted it byte-for-byte. Never hand-edited |
| Status | **Checkpoint C COMPLETE for a development checkpoint** (Owner-locally validated; SUBMITTED FOR OWNER REVIEW — NOT MERGED) |
| Next | **Final Source Review** of PR #5 (Owner-only; the Agent never merges). The Inspector is frozen for this checkpoint: no further presentation change is authorised, and the deferred optional polish (top-row vertical balance) must not trigger another change round. `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED |

### 11.8 Stage 0.3A-3 — Equipment topology & legacy parameter migration — Checkpoints A and B

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-08**: Stage 0.3A-3 Checkpoint A authorized (planning / decision / specification only) on the approved main base `a74db62c4a7d4d8d5d2185cfe77a4c0229b01fce` (the PR #5 merge); Checkpoint C, queue dispatch, cleaning execution, pump/valve/axis commands, Safe Return actuation, TEST_HARDWARE, PRODUCTION, ZIP, Release and Deployment all NOT AUTHORIZED |
| Checkpoint A (specification) | Authored as [`ADR-0017`](decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md) + [`STAGE_0.3A-3_CHECKPOINT_A.md`](STAGE_0.3A-3_CHECKPOINT_A.md) on PR #6; two review rounds (CHANGES REQUESTED → scanOrder derivation + acceptance-vs-merge governance, then legacy field semantics, deferred mappings, TC scope, public boundary, transitional vocabulary scope); **final Owner review PASS; ADR-0017 explicitly Owner-ACCEPTED (2026-10-08)** |
| Checkpoint B (implementation, AUTHORIZED) | Delivered on PR #6 exactly per the approved change set: `packages/contracts/Topology.cs` (NEW; `LogicalPositionRecord` with `LegacyRecordId` provenance, `WaterJetConfiguration`, `IsolationValveConfiguration`, `Region`, `WaterJetPlacementKind`), `Enums.cs` (+ canonical `LogicalPositionKind {SENSOR, NON_SENSOR_GAP}` only — transitional `SlotType.CANNON` wire surface untouched, Checkpoint C defers), `Config.cs` (+ `SensorConfigurationRecord`, `DeferredAcquisitionProvenance`, migration warning/refusal records, atomic `SensorParameterImportOutcome`, approved field-classification records, additive `MigrationRefusalCodes`), `packages/domain/WaterJetTopologyCatalog.cs` + `SensorParameterCsvImporter.cs` + `TopologyValidator.cs` (deterministic, atomic, fail-closed), `config/examples/sensor-parameters.migrated.example.json` (synthetic; deferred-raw and acquisition fields null), and the T1–T20 semantic test suite with a public-safe CSV builder (`tests/config.tests/`; csproj gains the required `Wjss.Domain` reference). Identifier note: `DedicatedIsolationValveId`/`ServedWaterJetId` implement the approved pairing attributes because the boundary S3 `redis` substring rule matches `Pai-red-is-olation…` — mechanical rename, semantics unchanged, recorded in the CHANGELOG |
| Correction chain (new commits only) | `c994c26f` → `03c2d15` → `6648437f` → `3dac1a0c` → `8cb94d11` (Checkpoint A corrections; final review PASS) → `b0a5208` (Checkpoint B implementation) → `18f853a5` (Owner-local test-compile correction) → `901a070` (CA1829 count correction) → `79fa0ba5` (behavioural test correction: four test defects; source-substring tests replaced with semantic tests; orderTotal-based I7-before-I16 ordering) → `2db853a7` (CA1865 + two xUnit2029 + last source-substring test replaced with compiled-public-type semantics) → `0728df61` (**Owner-authored** genuine lock refresh). Nothing rewritten |
| Lock handoff (2026-10-08) | `0728df61f917ba61f6dd3b8bf6d12e68dfa01d20` (`chore: refresh Checkpoint B migration test lock`; parent `2db853a7`) — exactly `tests/config.tests/packages.lock.json` (+6 lines): the genuine project-graph edge `wjss.domain → { type: Project, dependencies: { Wjss.Contracts: [1.0.0, ) } }`; valid JSON; no absolute path; no credential; no package-version change; no Owner CSV or acquisition value; adopted byte-for-byte; never hand-edited |
| Owner-local validation (2026-10-08) | **PASSED** — SDK `10.0.401`; normal restore PASS; locked restore PASS; Release build **0 warnings / 0 errors**; fresh full .NET suite **186 total / 186 passed / 0 failed / 0 skipped**; boundary scan **0 findings (S1–S9 clean)**; JSON examples PASS; Owner acquisition data in the migrated example NONE; final Owner-local Working Tree CLEAN. Validated feature head `2db853a7`. Recorded in §12.28 and [`STAGE_0.3A-3_CHECKPOINT_B.md`](STAGE_0.3A-3_CHECKPOINT_B.md). An earlier "139 total / 139 passed" observation was non-authoritative (`--no-build` over stale binaries) and is not validation |
| Validated topology | 108 LogicalPositions; 106 SensorConfigurations; 212 structural TC channel sides; Left 24 / Rear 29 / Right 24 / Front 29; I7 and I16 NON_SENSOR_GAP; `orderTotal` 0–107; derived `scanOrder` dense 1–106 over actual Sensors only; exactly 8 Water Jets; exactly 8 Isolation Valves; `WJn ↔ IVn` one-to-one; installed position separate from target coverage; legacy `cannon n → WJn` direct; acquisition bindings deferred; no Production device binding created; no command or actuation path added |
| Status | **Checkpoint B COMPLETE — OWNER-LOCALLY VALIDATED** (SUBMITTED FOR OWNER REVIEW — PR #6 **OPEN, NOT MERGED**; only the Owner merges) |
| Next (SUPERSEDED 2026-10-08 — see the CP-0 reconciliation block in §2) | Historical: PR #6 was later merged (`909d028`) and Checkpoint C was later authorised and validated (§11.9). Original text: Owner-only final source review of PR #6. **Checkpoint C NOT AUTHORIZED and NOT STARTED** (proposed scope — atomic CANNON vocabulary migration, Runtime topology integration, Inspector overlays — is documented in [`STAGE_0.3A-3_CHECKPOINT_B.md`](STAGE_0.3A-3_CHECKPOINT_B.md) §4 only). `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED |

### 11.9 Stage 0.3A-3 Checkpoint C — atomic Runtime topology migration and read-only Inspector integration

| Item | Record |
| --- | --- |
| Gate | **Owner instruction 2026-10-08**: Checkpoint C AUTHORIZED FOR IMPLEMENTATION on the existing PR #6 branch (required head `0c3dcea`, verified; base `a74db62c` unmoved; `2db853a` and `0728df61` proven in ancestry). Merge, TEST_HARDWARE, PRODUCTION, ZIP, Release, Deployment, SSE, and every write/command path remain NOT AUTHORIZED |
| Schema decision | **Option A — atomic update in this checkpoint, no deprecated alias**: `wjss.snapshot/2` / `wjss.delta/2` / `wjss.sensor-map/2`; structural break documented (slot kind vocabulary, `gapAnchorForWaterJetId`, per-Sensor assignment fields, Snapshot topology collections); no deployed `/1` consumer; ApiVersion `1` unchanged; impact stated here and in the CHANGELOG |
| Terminology | `SlotType` enum deleted; `LogicalPositionKind {SENSOR, NON_SENSOR_GAP}` only; `NonSensorGapCount`/`NonSensorGapSlots` (I7→WJ3 at 5/7, I16→WJ1 at 5/16); refusal code `WALL_MAP_NON_SENSOR_GAPS`; API `RuntimeGapReference`; no canonical Cannon entity current-facing (the importer's legacy `"cannon"` source header and `MIGRATION_CANNON_RANGE` remain as legacy provenance; dated historical records keep their original wording) |
| Runtime state | `RuntimeState` gains frozen `WaterJets` (8) + `IsolationValves` (8), process-lifetime immutable, validated (8+8 ordinal, WJn ↔ IVn both directions, per-Sensor target-coverage assignment via `WaterJetTopologyCatalog.TryRequireAssignment`, rear-lower ⇒ WJ1/IV1); refusals `TOPOLOGY_*`, `SENSOR_ASSIGNMENT`; no command state |
| Snapshot/Delta | Snapshot carries `waterJets`/`isolationValves` (Snapshot-only); Deltas never emit topology (typed absence + tests + TS validator); application preserves it unchanged; three-state `activeJob`, revision/gap semantics, determinism unchanged; no `activeJobCleared` |
| API | Routes unchanged and GET-only; status gains gap references (orderTotal order), 8+8 counts, `equipmentTopology` with `DEFERRED_NO_ACQUISITION_BINDING`; sensor views gain `assignedWaterJetId`/`assignedIsolationValveId`; health/readiness untouched |
| Inspector | "Non-sensor gaps" tile; wall map: 106 sensor id cells + visually distinct `I7 · GAP`/`I16 · GAP` (no values, never WJ labels); per-wall installation overlay ("sprays the opposite wall"); equipment topology table (read-only, control not authorized, acquisition deferred); Sensor table Assigned WJ/IV columns; footer extended; no actuation affordance |
| Synthetic | Seed/ticks/counts/determinism unchanged; topology static; never simulates valve/flow/nozzle/pump/axis/cleaning |
| Slices | C1 `d378edf2` (atomic contracts/domain/runtime/TS/fixtures/tests) → C2 `27591ea1` (API projection tests) → C3 (Inspector + docs, this record) |
| Arena verification | Boundary scan **0 findings (S1–S9 clean)** per slice; TS mirror **executed in Arena: npm ci + npm run check green (31/31)**; Inspector inline JS `node --check` OK + HTML balance OK; C# brace-balance over 36 changed files OK; `git diff --check` clean. **NOT COMPILED / NOT EXECUTED in Arena** (no .NET SDK) |
| Expected lock drift | `adapters/simulator/packages.lock.json` + `tests/integration/packages.lock.json` gain the direct `wjss.domain` edge on the next Owner restore; no lock hand-edited; `tests/config.tests` lock (`0728df61`) untouched |
| Owner-local gates | Restore; Release build; full test suite; `dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release` (fixture parity; `WJSS_UPDATE_FIXTURES=1` on diff); `npm ci && npm run check` in `packages/contracts/wjss-contracts-ts`; boundary scan; UI review |
| Correction chain (Checkpoint C) | `d378edf2` → `27591ea1` → `f04d992d` (Arena implementation slices) → `8567a78` (Owner CS0103: `Wjss.Domain` import) → `440a7fa` (Owner CS0246: `WaterJetPlacementKind` type reference; property name unchanged) → `162ad7f` (Owner behavioural test corrections: canonical `positionKind` raw-JSON vocabulary ×2; semantic topology round-trip assertions) → `bac36add` (**Owner-authored** genuine artifact commit: five `packages.lock.json` refreshes + regenerated `sensor-map.example.json`). Nothing rewritten |
| Owner-local validation (2026-10-08) | **PASSED — Stage 0.3A-3 Checkpoint C OWNER-LOCALLY VALIDATED**: SDK `10.0.401` / xUnit runtime .NET `10.0.12`; normal restore PASS; locked restore PASS; Release build **0 warnings / 0 errors**; fresh full .NET suite **201 total / 201 passed / 0 failed / 0 skipped**; fixture generation/update **7/7** and parity **7/7**; TypeScript typecheck PASS, **31/31**; boundary scan **0 findings (S1–S9 clean)**; JSON examples PASS; final working tree **CLEAN**. Validated feature head `162ad7f`; artifact commit `bac36add`. Artifact handoff verified: no package-version change, only the expected `Wjss.Domain` project-graph edges, no absolute path or credential; the regenerated example's only semantic drift is the corrected NON_SENSOR_GAP note (108 / 106 / 212; I7 → WJ3, I16 → WJ1). Recorded in §12.30 and [`STAGE_0.3A-3_CHECKPOINT_C.md`](STAGE_0.3A-3_CHECKPOINT_C.md) §16 |
| Owner reviews (2026-10-08) | Function review **PASSED**; Logic review **PASSED**; Inspector UI/UX review **PASSED — CRITICAL UI BLOCKER: NONE**. Minor punchlist deferred and non-blocking (gap-cell primary label in a tooltip; friendlier placement captions; vertical scrollability) — Inspector presentation FROZEN for this checkpoint |
| Status | **Checkpoint C COMPLETE — OWNER-LOCALLY VALIDATED** (PR #6 PREPARED FOR FINAL SOURCE REVIEW — **OPEN, NOT MERGED**; only the Owner merges) |
| Next | Owner-only final source review of PR #6. `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED |

### 11.1 Process deviation record

| Item | Statement |
| --- | --- |
| Event | During the Stage 0.1 review-correction session, the local branch pointer did not match the expected remote Stage checkpoint |
| Reported evidence | The working-tree content was verified as byte-identical to the remote checkpoint, and the resulting push was reported as a fast-forward |
| Reported integrity | **NO MISMATCH FOUND** in source content, and **no history rewrite was reported** |
| Deviation | The approved Stop condition required the session to stop and report the baseline mismatch before continuing. A mixed pointer/index update was performed instead, so stop-condition compliance was **not** met |
| Governance response | [`../AGENTS.md`](../AGENTS.md) §7.1 now defines a strict Baseline Mismatch Stop Gate. Content comparison is diagnostic evidence only, content identity does not authorise recovery, `/tmp` is not durable recovery evidence, and recovery requires explicit Owner-authorised instructions |
| Status | **PROCESS DEVIATION RECORDED** — Owner acceptance **PENDING** |
| Integrity conclusion | The deviation does not indicate source corruption. It is recorded as a process-compliance deviation, not as an integrity failure |

This record is retained deliberately. Git history was not rewritten to remove the deviation.

### 11.10 Stage 0.4A CP-2 — Cleaning Job, Mandatory Safe Return and Critical Pump Kernel (source authored)

- **Status:** SOURCE AUTHORED; STATICALLY REVIEWED; NOT COMPILED IN ARENA; NOT EXECUTED IN ARENA; **OWNER-LOCAL VALIDATION REQUIRED**. Branch `arena/bba7709c-waterjet-sentinel-suite`, base `8323f78`. PR open, not merged.
- **Delivered (pure `Runtime.Core` kernel, no host, API, Inspector, device or route):** single Active Job with lifecycle and abstract Pump and feedback inputs; Mandatory Safe Return SR1 to SR7 in the Owner order; outcome recorded only after SR5, in the same transition as SR6 and SR7 (release); Safe Return failure retains the Job with RECOVERY_REQUIRED evidence; critical latch persists after release; one evidence sequence; `SafeReturnReleaseEvidence` removed.
- **Not implemented:** Runtime host integration, API projection, Inspector or UI, command or write routes, Pump, Valve or Axis hardware commands, Modbus, PLC, DCS, Galil or KMotion, coordinates, speeds, homing, timers, TempQueue, TimeQueue, persistence, SSE, TEST_HARDWARE, PRODUCTION.
- **Open:** O-11 (mid-cleaning pump not-ready refused, not held); Owner confirmation of O-3, O-4 and O-8 (see the checkpoint report).
- **Authority:** CP-3 NOT AUTHORIZED. CP-4 NOT AUTHORIZED. TEST_HARDWARE and PRODUCTION device access NOT AUTHORIZED.
- Full record: `docs/STAGE_0.4A_CP-2_CHECKPOINT.md`.
- **Owner-local closeout (2026-10-08, §12.31):** OWNER-LOCALLY VALIDATED at `1f94991`; CP-2 Final Source Review PASSED (read-only, no blocking defect); F2 PASSED; F3 PASSED; EXPECTED_STOP refusal during CLEANING PASSED. Non-blocking follow-ups are in checkpoint §19. PR #8 OPEN, NOT MERGED. CP-3 and CP-4 NOT AUTHORIZED.

### 11.11 Stage 0.4A CP-3a / CP-3b — completion correction record (Owner-authorized; NOT VALIDATED until the Owner-local gates pass)

- **Authority.** Owner-authorized for CP-3a and CP-3b (completion correction, 2026-10-08), scope `docs/MASTER_PLAN.md` §3.3.4. This record supersedes the earlier `[OPEN]` authority wording of the development checkpoint; the checkpoint commit is not rewritten (append-only). CP-3c, CP-4, TEST_HARDWARE and PRODUCTION device access: NOT AUTHORIZED. The agent does not merge. The existing pull request for this branch is PR #9; it is not merged, and no new pull request was created for this correction.
- **Base.** `origin/main` = `d884a541163c2d567ab4ba2882a981431a14bfe7` (PR #8 merged, CP-1 correction and CP-2 §19 present). Session branch `arena/9aa1746c-waterjet-sentinel-suite`.
- **Commits on the branch, oldest first.** `245d72b` docs(plan): scope record. `012cb41` fix(runtime): CP-3a kernel hardening. `ba7b3d9` feat(runtime): CP-3b library. `3ca3dc2` test(runtime): CP-3b tests. `3e93924` docs(checkpoint): development checkpoint record. `5d1c7dc` fix(runtime): CP-3a completion (public preview removed). `74d9e63` feat(runtime): CP-3b completion (canonical Sensor binding, five-scope retention, projection, candidate Delta trend). `522d733` test(runtime): CP-3b simulator test suite. The commit that carries this section follows and is not named here (AGENTS §4.6).
- **CP-3a (kernel), completion.** The public `PreviewAxisStandbyLedger` is removed. `AxisStandbySeq` is written at SR5 inside the AT_STANDBY confirmation path and is observable only through the release transition's SR5 evidence record. FU-1 (a critical latch with a RUNNING Job is rejected as `JOB_RUNNING_UNDER_LATCH`), FU-2 (wording) and FU-4 (movement away from OPEN during CLEANING enters Safe Return, reason `VALVE_LOST_DURING_CLEANING`) are unchanged. Thirteen facts and one two-row theory cover the required cases.
- **CP-3b (library, completion), no host wiring.** The scenario catalogue is bound to the canonical Runtime Sensor set (`SimulatorScenarioSet.Create`): the primary target is the first Sensor in ScanOrder, WJn and IVn come from the canonical records, and no `SYN-S` placeholder is projected. Five retention scopes: evidence log of 256 with a dropped count; current-Job Safe Return tail of 16 (reset at dispatch, archived at SR7); last dispatch; last Job outcome (captured at SR7 with trigger, outcome, the SR2 to SR7 sequences and a bounded event tail); first critical (captured once on the latch-raising transition; first wins; Job context at latch time). Pure projection (`SequencingRuntimeProjection`): QueueSummary; Sensor QueueState and IsActiveJobTarget; ActiveCleaningJobState; SafeReturnState; CriticalPumpEvent with the synthetic `SYN-CRITICAL-{EvidenceSeq}` and `SYN-PUMP-CRITICAL-{EvidenceSeq}` identities, HIGH, ConditionActive true, Acknowledged false, ModalOpen true; LastJobOutcome. PumpState and the Sequence controls are carried from the previous revision. `RuntimeDeltaProjector.ProjectCandidate` carries one appended trend point and refuses every other trend change explicitly.
- **Presentation rules (Owner review requested; these are rulings, not authority questions).** `PhaseProgress` is the contract's 0..1 unit, so verified phase Pn is presented as n/6 (the brief's integer table does not apply). Before the first verified phase: `P1 PENDING - PREPARING` and `P1 PENDING - READY_TO_CLEAN`; `P1 PENDING - CLEANING` for CLEANING before any verified phase is a flagged convention. `CleaningPhase` is `IN_PROGRESS` (the contract defines only running and frozen-at-trigger). A valve Feedback before any observation is `UNKNOWN`, because no valve feedback identity exists for it. Safe Return `Command` fields carry the SR2 and SR4 step codes only; no command-like vocabulary is added.
- **Contract change.** None. `packages/contracts` is not changed. No contract change is proposed by this correction.
- **CHANGED (completion correction).** `docs/MASTER_PLAN.md` (§3.3.4 and the stage-table clause for 0.4); `docs/CURRENT_STATE.md` (§2 rows 58 and 60, this §11.11); `CHANGELOG.md` (Unreleased entry); `packages/application/Runtime/Sequencing/SequencingKernel.cs`; `packages/application/Runtime/Simulator/SimulatorScenarioCatalogue.cs`, `SequencingRetention.cs`, `SequencingRuntimeProjection.cs`; `packages/application/Runtime/RuntimeDeltaProjector.cs`; `tests/runtime.tests/Sequencing/SequencingCp3aHardeningTests.cs`; `tests/runtime.tests/Simulator/` (new `SimulatorRunHarness.cs`, `SequencingRuntimeProjectionTests.cs`, `SequencingRetentionTests.cs`, `SequencingDeltaProjectionTests.cs`; `SimulatorScenarioCatalogueTests.cs` rewritten; `SimulatorCp3bProjectionTests.cs` removed).
- **UNCHANGED.** Snapshot schema `wjss.snapshot/2`; Delta schema `wjss.delta/2`; `RuntimeStage.Marker`; the Runtime host (`apps/runtime`, `Program.cs`, `SimulatorRuntime.cs`); API routes and response shapes; the Inspector (`apps/runtime/Inspector/index.html`: no UI, label, layout or control change); `packages/contracts`; fixtures; the TypeScript package; project and lock files; configuration; device adapters; `SequencingTransition.cs`, `SequencingCodes.cs`, `SequencingState.cs`, `SequencingEvent.cs`, `SequencingFeedback.cs`, `SequencingTopology.cs`, `SequencingSafeReturn.cs`, `SequencingStateValidator.cs`; the synthetic evolution, invariants and store; every CP-2 test.
- **Validation observed in Arena.** Boundary scan `node tools/boundary-scan/boundary-scan.mjs .`: 0 findings, S1 to S9 clean. `git diff --check d884a54..HEAD`: clean. A brace and parenthesis balance check of every changed C# file passed. This is a text check, not a compiler.
- **Validation NOT run in Arena.** No .NET SDK is available in the sandbox: NOT COMPILED IN ARENA and NOT EXECUTED IN ARENA. Planned for Owner-local validation: locked restore; Release build with 0 warnings and 0 errors (`TreatWarningsAsErrors`); the full .NET suite: the CP-2 figure of 300 plus 251 planned new cases (CP-3a: 15, being 13 facts and one two-row theory; CP-3b: 236, being 64 facts and 19 theory methods expanding to 172 cases); fixture parity; TypeScript typecheck and tests; boundary scan. Planned cases are not executed results.
- **Not verified / open.** Compilation and a warning-free build; every new case; the full suite on this head; the presentation rulings listed above; O-3, O-4, O-8 and O-11 (still Owner-open); the FU-4 trigger confirmation requested by CP-2 §19.8, including the valve-movement-before-OPEN residual; the Host composition that supplies the canonical Sensor set (CP-3c, NOT AUTHORIZED). CP-2 §1, §19.1 and §19.11 still describe PR #8 as open (historical, not edited, append-only).
- **Status.** Owner-authorized for CP-3a and CP-3b completion; implementation NOT VALIDATED until the Owner-local gates pass. Not production ready, not commissioned, not tested on hardware, not certified, not Owner accepted, not merged.
- **Closeout (2026-10-09, documentation only; supersedes the NOT VALIDATED wording above for CP-3a and CP-3b).** Owner-local validation reported PASS on `a315e90be28fb5e9662c6a7179587c992273cb88` (Owner-reported; not reproduced in Arena): .NET SDK 10.0.401; xUnit runtime 10.0.12; Release build 0 warnings, 0 errors; full .NET 552/552 (the planned figures of 300 plus 251 are superseded by this count: 300 + 251 + 1, the extra case being the round-3 regression); focused Runtime.Core 430/430; Retention and Projection 131/131; direct Valve-retention regression 1/1; fixture parity 7/7; TypeScript typecheck PASS and tests 31/31; boundary scan 0 findings, S1–S9 clean; `git diff --check` PASS; working tree CLEAN; drift NONE; no artifact commit required.
- **Correction (UNCHANGED list above).** `packages/application/Runtime/Sequencing/SequencingStateValidator.cs` is CHANGED, not unchanged: FU-1 (`JOB_RUNNING_UNDER_LATCH`) adds 8 lines and removes 4 (`d884a54..a315e90`). The UNCHANGED list is superseded for that file, and the CHANGED list should also name it.
- **Valve feedback retention defect: CLOSED** (round 3, `a315e90`). Only the valve observation record is retained as the valve identity; the preparation context record and the Safe Return step record are not. The `Step is null` conjunct is a deviation from the stated condition and is recorded in MASTER_PLAN §3.3.5.
- **Final Source Review: PASSED.** Blocking defects: none. The eighteen questions, the presentation classifications, the non-blocking follow-ups NB-1 to NB-8 and the governance supersession index are in MASTER_PLAN §3.3.5.
- **Contract vocabulary disclosure (NB-1).** The 'Contract change: None' statement above is accurate for this branch. However, the projection's Safe Return `command`, `feedback` and `standby` values (`SR2`, `SR4`, kernel names and `UNKNOWN`) do not match the accepted TypeScript label unions in `packages/contracts/wjss-contracts-ts/types.ts` or the golden fixture (`CLOSE_COMMANDED`, `RETURN_COMMANDED`, `CLOSED_CONFIRMED`, `NOT_CONFIRMED`). The projection tests pin the kernel names. This is recorded as an Owner contract decision, with two options proposed and none applied. No wire emission of Safe Return legs is authorized until it is resolved.
- **Authority.** CP-3a and CP-3b: Owner-authorized, delivered and Owner-locally validated. **CP-3c NOT AUTHORIZED. CP-4 NOT AUTHORIZED.** PR #9 is OPEN and NOT MERGED. This closeout does not authorise Host integration, API fields or routes, Inspector changes, environment-variable scenario selection, runtime execution of scenarios, TEST_HARDWARE or PRODUCTION access.

## 12. Validation record

Documentation-only validation, performed for the Stage 0.1 review-correction checkpoint.
Detailed commands and observed output are reported in the delivery report.

| # | Check | Method |
| --- | --- | --- |
| 1 | List all changed files; confirm no out-of-scope file | `git status --porcelain` and `git diff --stat` before committing |
| 2 | Internal Markdown links resolve | Extracted every relative link target and verified each path exists |
| 3 | Production IP address search (IPv4 and CIDR) | Regular-expression scan across tracked files |
| 4 | Credential, token, password, private-key, and connection-string search | Keyword and pattern scan across tracked files |
| 5 | Conflicting null use for `LastSuccessfulCleaningCompletedAt` | Keyword scan |
| 6 | Obsolete `NEVER_CLEANED` behaviour | Keyword scan |
| 7 | Claims that WAGO fail-safe has passed | Keyword scan of surrounding context |
| 8 | Claims that production device access is authorised | Keyword scan |
| 9 | Parallel Cleaning Jobs described as `[OPEN]` | Cross-document search for `parallel`, `concurrent`, and related open-item entries |
| 10 | Water Jet to Isolation Valve cardinality described as `[OPEN]` | Cross-document search |
| 11 | "Six source queues" or any other wrong source-queue count | Cross-document search |
| 12 | Old GlobalQueue reload after a sequence restart | Cross-document search |
| 13 | Automatic final acknowledgement after an alarm clears | Cross-document search |
| 14 | `0.1.0` described as an application release | Cross-document search |
| 15 | Stage 0.1 implementation described as Owner accepted | Cross-document search |
| 16 | Web or local-web delivery described as rejected | Cross-document search |
| 17 | "must not infer the state" wording | Cross-document search |
| 18 | Main Pump stop described as executable in all circumstances | Cross-document search |
| 19 | Unsupported historian capacity certainty | Cross-document search |
| 20 | No application code or package dependency created | File-type and file-extension inventory |
| 21 | Diff reviewed before commit | Full diff read before creating the review-correction commit |

### 12.1 Stage 0.2 checkpoint documentation validation record

Documentation-only validation, performed for the **first** Stage 0.2 checkpoint. Every result
below is observed output from the method shown. The exact commands are reported in the Stage 0.2
delivery report.

**Superseded.** This record describes the first checkpoint. The Owner-requested Stage 0.2
documentation review then returned changes, and the corrected state is recorded in section
12.2. Two rows below are no longer an accurate description of the repository: row 1 names
12 modified and 8 created files, and row 8 lists the stage-status wording of the first
checkpoint. Neither is wrong as history; both were changed by the review-correction
checkpoint.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` | 12 modified existing documents and 8 new ADR files; nothing else created or modified |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. No solution, project, manifest, lock file, source, SQL, script, installer, archive, or CI file exists |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | **33 files scanned, 573 internal relative links checked, 0 broken** |
| 5 | Protected decisions remain stated | Targeted `grep` runs, listed in the delivery report | All required statements found, including Water Jet to Isolation Valve one-to-one, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, the Operator baseline override role, cleared-state acknowledgement, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, and Stage 0.3 `[NOT AUTHORIZED]` |
| 6 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | UI owns no device session and cannot write to hardware; the runtime service owns physical device sessions; domain logic is isolated from vendor libraries; simulator adapters implement the application-facing contracts; physical adapters are disabled by default; production configuration stays outside the repository; no statement permits parallel Cleaning Jobs |
| 7 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | **Zero matches** |
| 8 | Status check | Stage-status wording search | Stage 0.2 Scope Gate APPROVED; Stage 0.2 implementation SUBMITTED FOR OWNER REVIEW; Owner manual review PENDING; NOT MERGED; Stage 0.3 NOT AUTHORIZED; Production Device access and Production Write NOT AUTHORIZED |
| 9 | Full diff review | Complete staged diff read before the checkpoint commit | Every change traceable to Stage 0.2; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 10 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, or installer test was executed, and none is claimed |

### 12.2 Stage 0.2 review-correction (punchlist) documentation validation record

Documentation-only validation, performed for the Stage 0.2 review-correction checkpoint that
implemented the Owner-requested punchlist. Every result below is observed output from the
method shown. The exact commands are reported in the Stage 0.2 delivery report.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` and `git diff --stat` | Only repository documentation and decision records modified. No application source, project, manifest, lock file, SQL, script, installer, archive, CI file, configuration file, or runtime directory was created |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. Only `./docs` and `./docs/decisions` directories exist |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | **33 Markdown files scanned, 654 relative internal links checked, 0 broken** — previous checkpoint: 33 files, 656 links, 0 broken; delta -2 links from removed superseded scope-limitation narrative (no broken links) |
| 5 | Every ADR keeps its required section set | Heading extraction per ADR | Status, Context, Decision, Alternatives considered, Consequences, Risks, Verification status, Follow-up gates, Relationship to protected decisions |
| 6 | The UI framework selection is not presented as accepted | Cross-document search for framework-acceptance wording | Final framework is `[OPEN]`; both candidates are `[PROPOSED]`; the React preference is explicitly not acceptance; Blazor is explicitly not rejected |
| 7 | The React offline claim is correct and present | Targeted search | "React can be built and deployed offline" is stated, together with the second-ecosystem cost and the explicit statement that this does not make offline development or deployment impossible |
| 8 | Protected decisions remain stated and unmodified | Targeted `grep` runs, listed in the delivery report | All required statements found and unchanged: one Boiler Unit per Workstation, 104 Sensor locations, 208 Thermocouple channels *(superseded baseline — corrected by the Owner to 106 / 212 during Stage 0.2.1A, see section 2)*, Eight Water Jets, Four Galil DMC-B140-M controllers, Water Jet to Isolation Valve one-to-one, strictly sequential Cleaning Jobs, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, Operator baseline override role, cleared-state acknowledgement, Main Pump may remain running between sequential Jobs, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, Production configuration never in the repository |
| 9 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | The UI owns no device session and cannot write to hardware; the Equipment Runtime service is the sole owner of physical device sessions; adapters contain no UI logic and the domain is independent of vendor libraries; the simulator implements the same application-facing contract; physical adapters are disabled by default; the runtime consumes only Published configuration; the UI does not read Modbus and does not query SQL for live state; no statement permits parallel Cleaning Jobs |
| 10 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | Zero matches |
| 11 | Stage-status consistency | Stage-status wording search across every Markdown file | Zero remaining lines describing Stage 0.2 as `[NOT AUTHORIZED]`. Every file now states the current position: Stage 0.2 Scope Gate `[APPROVED]`; Stage 0.2 architecture checkpoint SUBMITTED FOR OWNER REVIEW; documentation review CHANGES REQUESTED / IN PROGRESS; Owner manual review PENDING; NOT MERGED; Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]` |
| 12 | Cross-reference integrity | Search for the old `section 22` reference and for the new section numbers | The open-item section is now section 34 and the earlier references were corrected. Sections 23–33 referenced by the decision records all exist |
| 13 | Full diff review | Complete diff read before the checkpoint commit | Every change is traceable to the punchlist; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 14 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, simulator, or installer test was executed, and none is claimed |

### 12.3 Stage 0.2.1A Arena validation record

*Checkpoint `dd20a8bd`, recorded on the superseded 104-location map. See section 12.4 for the
corrected map.*

Synthetic spike validation in Arena (Linux x64, Node v22.22.3). Detailed evidence:
[`spikes/stage-0.2.1a-results.md`](spikes/stage-0.2.1a-results.md) and
`spikes/ui-runtime-react/results/summary/arena-validation.md`.

| # | Check | Observed result |
| --- | --- | --- |
| 1 | TypeScript 6.0.3 `tsc --noEmit` | PASS, 0 errors |
| 2 | Vite 8.3.2 production build | PASS |
| 3 | Vitest (jsdom), 8 files | 53 / 53 PASS |
| 4 | Runtime harness `node:test` | 14 / 14 PASS |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | 10-minute harness measurement, 2 SSE clients | Exit 0; invariant violations 0; accepted second Jobs 0 |
| 7 | Playwright spec parse (`--list`, no browser) | PASS, 16 tests |
| 8 | Offline `npm ci` against an unreachable registry | PASS; negative control `ENOTCACHED` |
| 9 | Sensitive-data scan, new and changed files | PASS |
| 10 | Markdown relative links | PASS, 0 broken |
| 11 | Full diff review | Performed before the checkpoint commit |
| 12 | Browser, Edge, WebView2, Windows, kiosk, end-to-end latency, long-run UI | **NOT VERIFIED** in Arena — Owner-local testing PENDING |

**Reported discrepancy — resolved.** At checkpoint `dd20a8bd` the status headers of 12
documents still described the Stage 0.2 submission state: `SECURITY.md`, `ALARM_MODEL.md`,
`ARCHITECTURE.md`, `CLEANING_SEQUENCE.md`, `CONTROL_AUTHORITY.md`, `DOMAIN_MODEL.md`,
`HISTORIAN_RETENTION.md`, `QUEUE_MODEL.md`, `REQUIREMENTS.md`, `SAFETY_BOUNDARY.md`,
`TEST_STRATEGY.md`, `USER_PERMISSION_MODEL.md`. The Owner authorised the correction with the
Sensor-map domain correction, and all 12 (plus `ROADMAP.md` and `decisions/README.md`) now carry
the current status. See section 12.4.

### 12.4 Stage 0.2.1A Sensor-map correction — Arena validation record

Synthetic spike validation in Arena (Linux x64, Node v22.22.3) of the 106-location correction.
Detailed evidence: [`spikes/stage-0.2.1a-results.md` §0](spikes/stage-0.2.1a-results.md#0-sensor-map-correction-checkpoint).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, TypeScript 6.0.3 `tsc --noEmit` | PASS, 0 errors (lock file unchanged) |
| 2 | Vite 8.3.2 production build | PASS |
| 3 | Vitest (jsdom), 10 files | 60 / 60 PASS |
| 4 | Runtime harness `node:test`, 2 files | 26 / 26 PASS |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Golden fixtures regenerated and validated against the canonical map | PASS |
| 7 | 10-minute harness measurement, 2 SSE clients | Exit 0; invariant violations 0; accepted second Jobs 0 |
| 8 | Playwright spec parse (`--list`, no browser) | PASS, 17 tests |
| 9 | Hard gates | 106 Sensors; 212 Thermocouple channels; 24 / 29 / 24 / 29; 2 Cannon slots; I7 / I16 absent; 0 duplicate IDs, slots, scan orders, `TC_F`, `TC_R`, or shared channels |
| 10 | Markdown relative links | PASS — 44 files, 694 links, 0 broken |
| 11 | Sensitive-data scan, changed and new files | PASS |
| 12 | SHA-256 manifest regenerated and verified | PASS |
| 13 | Full diff review | Performed before the correction commit |
| 14 | Browser, Edge, WebView2, Windows, kiosk, end-to-end latency, long-run UI | **NOT VERIFIED** in Arena — Owner-local re-run and manual UI re-review **PENDING**; 60-minute run **PAUSED** |

### 12.5 Stage 0.2.1A fullscreen Operations refinement — Arena validation record

Owner-approved Design Addendum: retain the U-shaped map, 106 Sensors, 212 Thermocouple channels,
24 / 29 / 24 / 29, Cannon slots at I7 / I16; larger Sensor cells and text; smaller map centre;
two-column Sensor Detail; compact Active Job and GlobalQueue; Trend and Camera in the first
viewport; floating Diagnostics drawer; no page-level scrollbar at 1920 × 1080, Edge F11, 100 %
zoom. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0A](spikes/stage-0.2.1a-results.md#0a-fullscreen-operations-refinement-checkpoint-current).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci` (lock file restore), `tsc --noEmit` | PASS, 0 errors; dependencies unchanged |
| 2 | Vite production build | PASS |
| 3 | Vitest (jsdom), 12 files | 75 / 75 PASS (15 new: scale tokens, Diagnostics state, compact source reason, inspector, Active Job, mapping-independent cell classes, count and Cannon regressions) |
| 4 | Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 22 tests (5 new `LAYOUT-A`..`LAYOUT-E`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| 8 | Viewport fit, card bounds, computed cell sizes | **NOT VERIFIED** in Arena (no browser) — asserted by `e2e/layout.spec.ts`, Owner-local Edge run **PENDING** |
| 9 | Markdown relative links | PASS — 44 files, 696 links, 0 broken; 10 anchors, 0 broken |
| 10 | Sensitive-data scan (added lines), SHA-256 manifest | PASS / PASS (regenerated and verified) |
| 11 | Full diff review | Performed before the refinement commit |

### 12.6 Stage 0.2.1A Operations readability refinement — Arena validation record

Owner screenshot review of `ea23bc58`: status markers overlapped long Sensor IDs, small cells and
typography, oversaturated process colours, alarm and quality relying on colour, weak card
hierarchy. Presentation-only refinement plus the stale MAP U-shape assertion correction; the
U-map, mapping, counts, Cannon slots, right-side structure, bottom row, runtime, and contracts are
unchanged. Industrial HMI practice is guidance only; no standards certification is claimed.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0B](spikes/stage-0.2.1a-results.md#0b-operations-readability-refinement-checkpoint-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci` (lock file restore), `tsc --noEmit` (project + strict `e2e/*.ts`) | PASS, 0 errors; dependencies unchanged |
| 2 | Vite production build | PASS |
| 3 | Vitest (jsdom), 13 files | 93 / 93 PASS (18 new: colour tokens and contrast, typography and spacing tokens, cell zones, queue columns, detail groups, job phases and elapsed time, status groups, alarm strip, camera state) |
| 4 | Runtime harness `node:test` | 26 / 26 PASS (12 mapping tests) |
| 5 | Scenario runner, 28 scenarios | PASS 25 · PASS+OWNER 2 · OWNER-LOCAL 1 · FAIL 0 |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 26 tests in 3 files; operations + layout selection 25 (was 21; 4 new `READ-A`..`READ-D`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Cannon slots · I7 / I16 absent · 0 duplicates |
| 8 | ID / marker overlap, clipping, typography, viewport fit | **NOT VERIFIED** in Arena (no browser) — asserted by `e2e/layout.spec.ts`, Owner-local Edge run **PENDING** |
| 9 | Markdown relative links and anchors | PASS (see the delivery report for counts) |
| 10 | Sensitive-data scan (added lines), SHA-256 manifest | PASS / PASS (regenerated and verified) |
| 11 | Full diff review | Performed before the readability commit |

### 12.7 Stage 0.2.1A final Owner UI punchlist — Arena validation record

Owner screenshot review of `4129687a` (Owner-local Edge E2E 25 / 25 PASS there). Presentation
changes, opt-in synthetic review tooling, and synthetic queue-source evidence; the U-map, mapping,
counts, I7 / I16 slots, right-side structure, bottom row, contracts, queue authority, FIFO model,
single-job rule, and dependencies are unchanged. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0C](spikes/stage-0.2.1a-results.md#0c-final-owner-ui-punchlist-checkpoint-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 325.93 kB (gzip 107.55 kB, +2.71 kB), CSS 28.16 kB (gzip 6.80 kB), one hashed WOFF2 47.67 kB |
| 3 | Vitest (jsdom), 15 files | 109 / 109 PASS (16 new: font asset and wiring, identity, legend bounds, Water Jet slots, queue label, synthetic control boundary) |
| 4 | Runtime harness `node:test` | 32 / 32 PASS (6 new: mixed queue, Water Jet refusal, default behaviour, presets, per-Sensor controls, token endpoint) |
| 5 | Scenario runner, 30 scenarios | PASS 25 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S29 mixed queue: 5 source types in the first 8 rows; S30 six presets) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 35 tests in 4 files (9 new: `FONT-A`, `CELL-A`, `IDENT-A`, `LEGEND-A`, `WJ-A`, `QUEUE-A`, `CTRL-A`..`CTRL-C`) |
| 7 | Hard gates | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 8 | Font provenance and licence | WOFF2 SHA-256 `40f917d9…1531b` and `OFL.txt` / `TRADEMARKS.md` Git blobs re-verified by test; OFL-1.1, no RFN; recorded in the licence inventory |
| 9 | Rendered font, cell zones, legend bounds, controls in a browser | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Public-repository boundary | Bundled font redistributable under OFL-1.1 with licence text; no Production Water Jet numbers, addresses, or credentials; per-run token files git-ignored; synthetic controls loopback-only and opt-in |
| 11 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.8 Stage 0.2.1A GlobalQueue semantics correction — Arena validation record

Owner domain correction after the Owner-local Edge run of `23f48daa` (34 selected · 25 passed ·
2 failed · 7 not run). The synthetic GlobalQueue now holds ready-to-dispatch entries only (≤ 8,
no entry states), dispatch is head-only and atomic with a synthetic dispatch record, and the
previous synthetic queue / job behaviour is **SUPERSEDED** (not eligible for production promotion).
Eligibility conditions are a proposal awaiting the Owner:
[queue eligibility decision matrix](spikes/queue-eligibility-decision-matrix.md) (every row
`OWNER DECISION REQUIRED`). A Production Queue Runtime is a Main Development slice (not authorized).
U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16 slots, fonts, and dependencies are unchanged.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0D](spikes/stage-0.2.1a-results.md#0d-globalqueue-semantics-correction-and-head-only-dispatch-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 327.90 kB (gzip 108.05 kB), CSS 27.67 kB (gzip 6.68 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 16 files | 120 / 120 PASS (new `globalQueue.test.tsx`; Sensor-cell vertical budget guard) |
| 4 | Runtime harness `node:test` | 40 / 40 PASS (new `queueDispatch.test.mjs`: gates A–F) |
| 5 | Scenario runner, 32 scenarios | PASS 27 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S31 head-only dispatch on the Owner example queue; S32 bounded queue under `dirty70`) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 36 tests in 4 files; Owner-local selection 35 (new `QUEUE-B`; `QUEUE-A`, `CTRL-A`, `CTRL-B`, `WJ-A` rewritten) |
| 7 | Queue capacity / dispatch gates A–F | PASS in Arena (harness + scenarios); UI gate E in jsdom; browser part Owner-local **PENDING** |
| 8 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 9 | Value clipping (`READ-A`), Water Jet clicks (`WJ-A`), rendered queue panel | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.9 Stage 0.2.1A critical Main Pump and Mandatory Safe Return — Arena validation record

Owner critical Pump decision on `60cd0398` (recovered by the Owner-authorized one-time recovery,
identity proof 147 / 147). In the synthetic spike:

- The Main Pump is a High Critical device.
- An unexpected stop or trip stops progression, water and dispatch. The AutoSequence becomes
  `CRITICAL_SUSPENDED`, an Active Job enters Mandatory Safe Return, and a blocking critical modal
  (alertdialog, Acknowledge only, no close, no Resume) opens.
- Every Job outcome ends through Mandatory Safe Return. The valve close is commanded and confirmed
  before the axis Standby command, and the Job stays Active until Standby is confirmed.
- The GlobalQueue remains ready-only and is frozen while suspended.

Queue-level `HELD` is superseded in [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §7.2 and
[`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) (Owner-authorized). Unresolved policies are listed in the
[critical Pump / Safe Return decision matrix](spikes/critical-pump-safe-return-decision-matrix.md)
(every row `OWNER DECISION REQUIRED`).

**SYNTHETIC PROOF ONLY — PRODUCTION SAFETY NOT VERIFIED.** No safety certification is claimed.

U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16 slots, fonts, and dependencies are unchanged.
Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0E](spikes/stage-0.2.1a-results.md#0e-critical-main-pump-handling-and-mandatory-safe-return-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 340.50 kB (gzip 111.65 kB), CSS 30.92 kB (gzip 7.34 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 17 files | 131 / 131 PASS (new `criticalModal.test.tsx`; critical palette distinctness test) |
| 4 | Runtime harness `node:test` (× 3) | 51 / 51 PASS each run (new `safeReturn.test.mjs`: gates 1–6; critical-alarm acknowledge route) |
| 5 | Scenario runner, 34 scenarios | PASS 29 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (S33 Pump trip during a Job; S34 normal completion with delayed valve feedback) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 43 tests in 5 files; Owner-local selection 42 (new `critical.spec.ts`: `CRIT-A`..`CRIT-E`, `CRIT-F` × 2 viewports) |
| 7 | Safe Return ordering, completion / abort / trip, failure, queue-during-suspension gates | PASS in Arena (harness + scenarios + validator); modal structure in jsdom; browser part Owner-local **PENDING** |
| 8 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 9 | Modal rendering, geometry, focus trap and Escape in Edge | **NOT VERIFIED** in Arena (no browser) — Owner-local Edge run **PENDING** |
| 10 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.10 Stage 0.2.1A final spike closeout (synthetic AutoSequence controls) — Arena validation record

Owner final closeout on `81c87a44` (recovered by the Owner-authorized one-time recovery, identity
proof 153 / 153) after the Owner-local Edge run of `81c87a44` (42 selected · 32 passed · 1 failed
`S11/S12` · 9 not run). In the synthetic spike:

- `S11/S12` is deterministic: the Sensor's Dirty Score is fixed and held (Score 82) and Score,
  classification and the computed background must stay identical through Raise / Clear /
  Acknowledge (no RGB tolerance).
- Diagnostics-only synthetic AutoSequence controls (`SYN · ` labels, `--synthetic-test-controls`):
  START AUTOSEQUENCE, PAUSE AFTER CURRENT JOB, RESUME AUTOSEQUENCE, ABORT ACTIVE JOB, and RESET
  CRITICAL SCENARIO. Dispatch is always GlobalQueue Position 1; every Job ends through Mandatory
  Safe Return; clear + acknowledge never resume; RESET never dispatches and requires an explicit
  START. These are **not** the Production operator-control model.
- Current-facing queue-level `HELD` / `BLOCKED` / `EXCLUDED` wording is removed; ADR-0003 is
  **SUPERSEDED IN PART by Owner decision dated 2026-10-06**. Production Pause / Resume, abort
  re-queue and Reject / Reorder in the ready-only queue are **OWNER DECISION REQUIRED**.

Critical Pump / Safe Return behaviour, U-map, mapping, 106 / 212, 24 / 29 / 24 / 29, I7 / I16
slots, fonts, and dependencies are unchanged. Detailed evidence:
[`spikes/stage-0.2.1a-results.md` §0F](spikes/stage-0.2.1a-results.md#0f-final-spike-closeout-synthetic-autosequence-controls-stage-021a).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | `npm ci`, `tsc --noEmit` (project; strict `e2e/*.ts`) | PASS, 0 errors (strict e2e: only the 6 known absent Node type declarations in `soak.spec.ts` / `support.ts`); dependencies unchanged |
| 2 | Vite production build | PASS — JS 345.17 kB (gzip 112.92 kB), CSS 31.70 kB (gzip 7.46 kB), WOFF2 47.67 kB unchanged |
| 3 | Vitest (jsdom), 18 files | 138 / 138 PASS (new `autoSequenceControl.test.tsx`, 7 tests) |
| 4 | Runtime harness `node:test` (× 3) | 60 / 60 PASS each run (new `autoSequenceControls.test.mjs`: A–G) |
| 5 | Scenario runner, 35 scenarios | PASS 30 · PASS+OWNER 4 · OWNER-LOCAL 1 · FAIL 0 (new S35; S09 expects `PAUSE_REQUESTED` with an Active Job) |
| 6 | Playwright spec parse (`--list`, no browser) | PASS, 49 tests in 5 files; Owner-local selection 48 (`SEQ-B`..`SEQ-G` in `critical.spec.ts`; `S11/S12` rewritten) |
| 7 | Hard gates (Sensor map) | 106 · 212 · 24 / 29 / 24 / 29 · 2 Water Jet reference slots (internal `CANNON_*`) · I7 / I16 absent · 0 duplicates |
| 8 | Controls, alarm colour and modal in Edge | **NOT VERIFIED** in Arena (no browser) — Owner-local final Edge gate **PENDING** |
| 9 | Markdown links / anchors, sensitive-data scan, SHA-256 manifest, diff review | PASS (counts in the delivery report) |

### 12.11 Stage 0.2.1A Owner-local final review and documentation closeout

Owner decision dated 2026-10-07 on checkpoint `114c07619f9fd249cd80b2e7a5f385ff9e081119`
(Owner-reported; the Agent received the outcome, not the raw Edge output or screenshots):

| Item | Recorded result |
| --- | --- |
| Owner-local final Edge gate | **PASS** |
| Owner manual review | **PASS** |
| Development baseline | UI, synthetic AutoSequence controls, GlobalQueue presentation (ready-only, ≤ 8, head-only dispatch), critical Pump modal and Mandatory Safe Return behaviour **accepted** |
| Primary UI Framework | **React selected** |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Controlled 15-minute and 60-minute observations | **Waived as Stage 0.2.1A merge blockers** (not run) |
| Stage 0.2.1A | **COMPLETE FOR DEVELOPMENT CHECKPOINT**; PR #3 **ready for Owner merge**, **NOT MERGED** |
| Main Development Scope Gate | **PENDING** — no Main Development coding has started |
| Production device access | **NOT AUTHORIZED** |

Not claimed: Production safety, Production stability, WebView2 validation, kiosk validation,
Modbus performance, hardware validation. Synthetic AutoSequence controls remain review tooling,
not the Production operator-control model; Production Pause / Resume, abort re-queue and reset
authority remain **OWNER DECISION REQUIRED**.

The closeout commit is documentation-only. It also removed the remaining current-facing
queue-level Hold wording in `CONTROL_AUTHORITY.md` (§4 invariant, §6 conflict table),
`QUEUE_MODEL.md` (snapshot contents), `TEST_STRATEGY.md` (operator-action tests) and
`USER_PERMISSION_MODEL.md` (queue permissions), and updated the status banners. No runtime, React,
CSS, contract, fixture, harness, scenario, E2E, dependency or Sensor-map file changed.

### 12.12 Stage 0.3A-1 Product Foundation Source Checkpoint — Arena validation record

Validation actually available inside Arena, executed 2026-10-07 on the final tree state (this
record's tables are the authority for what was and was not checked):

| # | Check | Tool / method | Result |
| --- | --- | --- | --- |
| 1 | TypeScript mirror typechecks | `npx tsc --noEmit` (strict) | **PASS** |
| 2 | TS structural tests | `node --test test/validate.test.mjs` | **14/14 PASS** |
| 3 | Committed fixtures validate structurally | validator CLI over `packages/contracts/fixtures/` + `config/examples/` | **PASS** (snapshot 0 issues; delta.basic gapless; delta.gap flagged) |
| 4 | All repository JSON parses | `node` parse sweep of tracked `.json` (excl. `node_modules`) | **PASS** |
| 5 | Boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` | **0 findings** (S1–S7) |
| 6 | Product tree references spike source | scanner rule S5 (code files only) | **PASS** — zero code references; prose boundary notes allowed |
| 7 | Solution completeness | scanner rule S7: every `*.csproj` (non-spike) listed in `WaterJetSentinelSuite.sln` | **PASS** (12/12, no ghosts, braces balanced) |
| 8 | Approved NuGet set only | read-back of `Directory.Packages.props` + all csproj `<PackageReference>` | **PASS** — exactly the three approved test packages; kiosk references no package; labels say PROPOSED/UNVERIFIED |
| 9 | `.gitignore` product/packages conflict | corrected rules + `git check-ignore` probes; fixture/example files confirmed trackable without force-add | **PASS** |
| 10 | No lock-file fabrication | `git ls-files` review; only npm-real lockfile (produced by genuine `npm install`) tracked; no `packages.lock.json` anywhere | **PASS** |
| 11 | Spike tree immutability | sha256 roll over `spikes/**` compared to pre-work baseline | **PASS — identical** (see commit report) |
| 12 | Whitespace hygiene | `git diff --check` | **PASS** (no errors) |
| 13 | Secret-shaped literals | scanner rule S4 across repository text | **PASS** (0) |
| 14 | Relative-link check | node sweep of markdown links in changed/new docs | **PASS** |

**NOT PERFORMED (impossible in Arena, not failures):** `dotnet restore/build/test`, fixture
regeneration via the .NET generator, Runtime/kiosk execution, NuGet pin resolution. See
[`STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) — that run,
reported verbatim into the PR, is the mandatory pre-merge gate.

### 12.13 Stage 0.3A-1 Owner source-review correction — Arena validation record

Owner review of `205456e` returned findings A–E; this checkpoint addresses all five as a
source correction on PR #4 (commit message `fix: correct Stage 0.3A-1 source checkpoint
blockers`). Validation actually executed in Arena, on the corrected tree:

| # | Check | Result |
| --- | --- | --- |
| A-1 | `Directory.Packages.props` XML comment double-hyphen (`--locked-mode`) | **REMOVED** — comment reworded; file parses |
| A-2 | New scanner rule **S8 XML well-formedness** over `*.csproj/*.props/*.targets/*.manifest/*.resx/*.config` (comments: no `--`, terminated, not `-`-ended; attribute quoting; element nesting; stray `<`) | **PASS — repo 0 findings**; negative controls (bad comment, unquoted attr, unclosed element) each flagged, exit 1 |
| B | `activeJobCleared` removed from C# contracts, TS types, validator, fixtures, tests, ADR and current-facing docs; three-state `activeJob` restored via `Optional<T>` + scoped converter; `delta.basic` fixture now exercises the explicit-null clear key (generator updated in lockstep) | **DONE** — C# compile/test NOT RUN (Arena); TS **15/15 PASS** incl. required-test-6 (three-state acceptance + flag rejection) |
| E | `xunit.runner.visualstudio` carries `PrivateAssets=all` + standard `IncludeAssets` list at the `PackageVersion` (CPM flow); versions unchanged; no lock files generated | **DONE** (static read-back) |
| C/D | Current-facing banners aligned in the 12 authorized files; README React-selection wording corrected (historical sentences labeled) | **DONE** — ad-hoc consistency sweep green |

Still NOT claimed: any .NET restore/build/test result (sandbox unchanged). Fixtures remain
`PROVISIONAL STRUCTURAL FIXTURE` pending the Owner-local regeneration.

### 12.14 Stage 0.3A-1 Owner-local compile correction — Arena validation record

Owner-local Release build of `355c064` (run twice, identical): **BUILD FAILED** —
`Wjss.Kiosk` WFO0003 (manifest DPI) and `Wjss.Contracts` CA2231 (`Optional<T>` operators);
`Wjss.Time` compiled. Corrected on PR #4 as source only (no suppressions, no severity or TFM
changes, no package edits, no lock files; contract wire semantics untouched): manifest
stripped of DPI elements (compatibility retained), `ApplicationHighDpiMode=PerMonitorV2` on
the kiosk project, generated-bootstrap entry point, `operator ==`/`!=` on `Optional<T>`
mirroring `Equals` exactly. New guards: scanner rule **S9** (DPI placement matrix, 0
findings; negative controls flag every violation) and xUnit static tests
(`KioskDpiConfigurationTests` ×3, `OptionalPresenceTests` ×7 — authored, NOT RUN in Arena).
Arena re-validation after correction: S1–S9 clean, TS 15/15, links green, spikes hash
unchanged. **Status remains: .NET build NOT VERIFIED — Owner-local re-run required.**

### 12.15 Stage 0.3A-1 Owner-local build round 3 — Arena validation record

Owner evidence (2026-10-07): round-2 cascade root-caused to a **process-scoped `TargetPath`
environment override** injected by an external motion-control toolset; after removal, ten
projects compiled clean (Time, Kiosk, Contracts, Domain, Adapters.Simulator, Runtime.Core,
Runtime.Api.Tests, Domain.Tests, FixtureEmission.Tests, Runtime.Core.Tests) — ProjectReference
paths confirmed correct. Two genuine defects remained and are corrected as source on PR #4:
missing `using Xunit;` in `KioskDpiConfigurationTests.cs` (per-file convention kept; no
GlobalUsings), and CA1873 in the Runtime startup log (now `IsEnabled(LogLevel.Information)`
guard; template, level, structured payloads, and every stub behaviour untouched — pinned by
four new static source tests, `RuntimeStubPreservationSourceTests`). Repository policy
recorded: no vendor paths, no `TargetPath`/`ReferencePath` overrides, no MSB3245 suppression;
runbook §0A makes the environment check mandatory; restore-generated `packages.lock.json`
files remain untracked pending full-pass Owner assessment. Arena static validation after
correction: S1–S9 zero findings, TS 15/15, links green, `git diff --check` clean, spikes roll
unchanged. **Status: first partial compile PASS is Owner-local evidence only; the full
Release build + test rerun remains REQUIRED. No .NET success is claimed in this record.**

### 12.16 Stage 0.3A-1 Owner-local build round 4 — Arena validation record

Owner evidence on `c16804c` (TargetPath removed from the build process): **11 projects
compile** — Time, Kiosk, Contracts, Adapters.Simulator, Domain, Runtime.Api.Tests,
FixtureEmission.Tests, Runtime.Core, Domain.Tests, Runtime.Core.Tests, **Runtime** — proving
the ProjectReference graph, the DPI correction, `Optional<T>`+converter, and the CA1873
guard. Sole remaining failure: `Wjss.Config.Examples.Tests` CS0103 ×2 — cross-namespace use
of `ExampleConfigTests.RepoRoot()`. Fixed per Owner preference with a stable assembly anchor:
`ConfigTestAssemblyMarker` + shared internal `ConfigTestPaths.RepoRoot()` (walk-up to the
solution file from `typeof(...).Assembly.Location`, behaviourally identical, repository
SOURCE inspected, not build output); both preservation tests switched; `ExampleConfigTests`
untouched; no fake classes, no new dependencies, no path or assertion changes. Arena static
validation after correction: boundary scan 0 findings (S1–S9), anchor type count = 1, zero
unresolved `ExampleConfigTests` code references, JSON 10/10, TS 15/15 (npm ci → typecheck →
node --test), links green, `git diff --check` clean, spikes roll unchanged, no KMotion/
TargetPath/vendor strings in Product source. **Status: .NET build of the config test project
and all test executions remain Owner-local; full Release build rerun REQUIRED.**

### 12.17 Stage 0.3A-1 Owner-local build & contract-test correction round 5 — Arena validation record

Owner evidence (2026-10-07): full-solution Release build **not yet passing**; the subsequent
`dotnet test --no-build` (49 total · 42 pass · 7 fail · 0 skip) is recorded **non-authoritative**
— stale assemblies, including a config-test "success" despite that project not compiling in
the preceding build. Source corrections on PR #4: (1) round-4 assembly-anchor mechanism
verified complete (marker once; both preservation tests share `ConfigTestPaths.RepoRoot()`; no
fake `ExampleConfigTests`; repository-source discovery unchanged); (2) the three failing
serializer tests rewritten to presence-aware `JsonDocument`/`JsonElement` assertions
(absent = `TryGetProperty` false / null = present + `JsonValueKind.Null` / object = present +
`JsonValueKind.Object`), covering post-serialization and post-deserialization stability of all
three `activeJob` states — wire contract untouched; (3) `Optional<T>` present-payload
equality and hash delegated to `EqualityComparer<T>.Default` (fixes equal-records split by
reference comparison; operators still exactly mirror `Equals`; `Present(null)` still rejected;
serialization unchanged), with delegation-focused tests added; (4) fixture drift on the three
named files retained as **expected provisional evidence** — Arena modified no fixture; the
runbook now gates regeneration behind full-build PASS + all non-parity tests PASS. Arena static
validation: S1–S9 zero findings; anchor/JSON checks green; TS 15/15 (npm ci → typecheck →
node --test); links and whitespace clean; spikes roll unchanged. **No .NET build/test success
claimed; Owner-local full Release build + fresh-assembly test run REQUIRED.**

### 12.18 Stage 0.3A-1 thermocouple channel contract shape correction (round 6) — Arena validation record

Owner-local progress (2026-10-07, rounds 3–5): full Release build **0 warnings / 0
errors**, corrected checkpoint suite **50/50**, fixture/delta parity **7/7**; the earlier
`--no-build` 42/49 stays recorded as non-authoritative. Residual drift on the three
provisional files led to a confirmed shape defect: `tcChannels` was a comma-delimited
scalar string in the Arena-authored sensor-map example (its test split it). Correction on
PR #4: structured two-entry array per SENSOR slot (deterministic front-then-rear order),
absent on CANNON slots; `SensorMapSlotExample` + `TcChannelRules` (`packages/contracts/
TcChannels.cs`) make the shape contractual and reject every invalid form; the generator
emits arrays and self-validates through the rules before serializing; the committed
example file was regenerated in array form for TypeScript validation only (still
PROVISIONAL — Owner regeneration after a full Release build remains mandatory and the
comma-delimited fixtures are kept as evidence); snapshot/delta presentation keeps
`tcFrontChannel`/`tcRearChannel` unchanged. ADR-0014 decision 8 records the rule and the
rejection history. Arena static validation: S1–S9 zero findings; JSON parse green; TS
24/24 (npm ci → typecheck → node --test); links and whitespace clean; spikes roll
unchanged. Round-6 C# is compile-reviewed only, **not built or tested in Arena**; Owner
must rerun the full sequence including the fixture regeneration gate.

### 12.19 Stage 0.3A-1 round 6 Owner-local test correction — Arena validation record

Owner-local round-6 validation (2026-10-07): the tcChannels structured-array contract is
VERIFIED by the .NET generator (108/106/2 slots; 212 total and 212 unique channels; 0
duplicates; 0 scalar sensors; cannons I7/I16; walls 24-29-24-29; CLR shape `System.Object[]`
with JSON two-string arrays), TypeScript 24/24, parity 7/7 after regeneration, boundary scan
S1–S9 clean. The `--no-build` 49/50 suite count is **non-authoritative** —
`Wjss.Config.Examples.Tests` did not rebuild (CA1861 in the new channel tests) and a stale
assembly ran the pre-correction `GetValue<string>()` assertion against the now-array
`tcChannels` node. Source-only corrections on PR #4: inline constant arrays extracted to
named `private static readonly` fields (no suppression, `TreatWarningsAsErrors` untouched),
and `ExampleConfigTests` now asserts the JsonArray shape directly (array cast required —
scalar strings fail as shape errors; two non-empty string items per sensor; 212 unique;
cannons key-free), keeping canonical counts and the typed `TcChannelRules` verification.
Arena static validation green; C# remains compile-reviewed only. Owner-local clean Release
build + fresh full-suite pass REQUIRED before merge.

### 12.20 Stage 0.3A-1 round 6c structural JSON assertion correction — Arena validation record

Owner-local at `0b088ad`: Release build **PASS 0 warnings / 0 errors**; fresh full suite
**61 total / 59 passed / 2 failed / 0 skipped**. Both failures were formatting-sensitive
test assertions in `tests/config.tests/TcChannelContractTests.cs` that "proved" the scalar
form was absent by matching serialized text across the indented writer's whitespace; the
tcChannels contract itself is Owner-verified and frozen (exact-two-element arrays,
108/106/2, 212 = 212 unique, I7/I16, 24-29-24-29, TS 24/24, parity 7/7, S1–S9 clean).
Round 6c replaced every shape claim with structural `JsonDocument`/`JsonValueKind`
inspection (presence, Array-kind, exactly two String-kind items, non-empty and
whitespace-pure, distinct, deterministic front-first order asserted on channel VALUES,
cannon key absence, full canonical mapping totals), and a defect-class sweep removed the
last two same-class serialized-text assertions elsewhere
(`Delta_RequiredTest3` redundancy; `ExampleConfigTests` deviceProfile text guards now
recursively scan parsed JSON). Contract, generator, serializer, fixtures, examples,
TypeScript mirror and lock files unchanged; no suppressions; `TreatWarningsAsErrors`
enabled. Arena mirrored the structural assertions in Python against the committed example:
all pass. Owner-local **final build + fresh full-suite rerun** REQUIRED before merge; no
.NET/xUnit success claimed in Arena.

### 12.21 Stage 0.3A-1 round 6d cannon logical ordering test correction — Arena validation record

Owner-local at `39577e2`: Release build **PASS 0/0**; fresh full suite 61 / **60 passed /
1 failed** / 0 skipped; parity 7/7; boundary scan S1–S9 clean. The lone failure —
`Example_Mapping_Validates_Through_The_Typed_Contract` — was a TEST-ordering defect, not a
mapping/contract defect: cannon labels were compared in lexicographic order
(`OrderBy(label)` → `I16, I7`) instead of the accepted logical order (`I7, I16`). Round 6d
fixes the assertion pipeline to sort cannon SLOTS by structured position
(`LogicalRow`, then `LogicalColumn`), keeps the expected `[7, 16]` / `["I7","I16"]`
sequences, adds explicit per-slot I7/I16 row/column/equipment pairing and a cannon
`sensorId`-absence check, and retains every canonical count. Ordering defect-class sweep
across the Stage 0.3A-1 test tree found no other lexicographic sorting of labels/IDs, no
label numeric parsing where structured fields exist, and no set/sequence confusion;
the only other `OrderBy` sites sort by structured numeric fields or parse inputs for the
product API under test. Product source, contracts, fixtures, TypeScript mirror, lock files
and spikes unchanged (zero diff); expectations were not flipped to match text sorting and
no sorting dependency was added. Arena mirrored the corrected pipeline in Python against
the committed example: structured ordering yields I7→I16 while lexicographic yields
I16→I7, confirming the defect and its correction. Owner-local **final build + fresh
full-suite rerun (61/61)** REQUIRED before merge.

### 12.22 Stage 0.3A-1 final Owner-local evidence closeout — PASSED

**STAGE 0.3A-1 OWNER-LOCAL VALIDATION PASSED** (Owner, 2026-10-07; authoritative because
the full Release build completed successfully immediately before the fresh full-suite
run). Final Release build: PASS, 0 warnings / 0 errors, all 12 projects, 3.7 s. Final
fresh .NET suite: 61 / 61 passed / 0 failed / 0 skipped (5.5 s). SDK 10.0.401, xUnit
observed runtime .NET 10.0.12. TypeScript 24/24; FixtureEmission parity 7/7; boundary
scan S1–S9 0 findings. Health: live 200 (ALIVE / SIMULATOR / runtimeImplemented=false /
STAGE_03A1_SKELETON), ready 503 (RUNTIME_NOT_IMPLEMENTED). Profile gates fail-closed:
SIMULATOR starts; TEST_HARDWARE exit 2; PRODUCTION exit 2; invalid profile exit 4; no
silent fallback with `--no-launch-profile`. Port collision on 5181: refusal, exit 3. The
tcChannels contract re-verified: 108/106/2, exact-two-element arrays (CLR
`System.Object[]`), 212 = 212 unique, 0 duplicates, 0 wrong-count sensors, cannons
I7/I16 channel-free, walls 24-29-24-29. Closeout commits: handoff `488b98fb…` (12
genuine `packages.lock.json` + 3 .NET-generated fixtures — verified in Arena against the
full static battery; Arena never generated or modified them) and the documentation
closeout (`global.json` pin `10.0.401` / `latestPatch` from Owner-validated evidence;
test policy recorded in the runbook §11; TargetPath process-scope prerequisite retained in
§0A; CHANGELOG placement sentence, ADR-0016 environment-probe residue and the
fixtures-README section pointer corrected; dated per-round records untouched). **PR #4:
READY FOR OWNER MERGE — NOT MERGED. Stage 0.3A-2 NOT AUTHORIZED. Production device
access NOT AUTHORIZED.**

### 12.23 Stage 0.3A-2A Checkpoint A — Arena validation record (source-only)

**Scope.** Owner-authorized Stage 0.3A-2 Checkpoint A (Runtime State Foundation,
SIMULATOR-only) authored in Arena. Arena executed **no .NET command**: the sandbox has no .NET
SDK and no NuGet endpoint is reachable, so no restore, build, or test result is claimed. The
C# position is **SOURCE AUTHORED / STATICALLY REVIEWED / NOT COMPILED IN ARENA / NOT EXECUTED
IN ARENA**, and any statement that Stage 0.3A-2 is "delivered" carries the qualifier
*development checkpoint submitted for review, pending Owner-local validation*.

| # | Check | Command / method | Observed result |
| --- | --- | --- | --- |
| 1 | Approved base | `git ls-remote origin refs/heads/main` | `bdf7f8f277b85087e3e53c067fe956fca7e04f90` — equals the approved base, so no Baseline-mismatch Stop Gate applied |
| 2 | Boundary scan (pre-change) | `node tools/boundary-scan/boundary-scan.mjs .` on the clean worktree | `boundary-scan: 0 findings (S1-S9 clean)`, exit 0 |
| 3 | Boundary scan (changed worktree) | same command with all Checkpoint A changes present | `boundary-scan: 0 findings (S1-S9 clean)`, exit 0 |
| 4 | C# sanity sweep | brace/paren balance script, final-newline check, trailing-whitespace and tab greps over every changed file | clean (no findings) |
| 5 | Diff hygiene | `git diff --cached --check` | clean |
| 6 | Contract cross-check | manual review of every referenced contract member against `packages/contracts` (`WallMapSlot`, `SensorPresentationState`, `WallSummary`, `QueueSummary`, `DispatchRecord`, `PumpState`, `SequenceState`, `AlarmSummary`, `CommunicationHealth`, `TrendWindow`, `RuntimeHealth`, `OperationalSnapshot`, `PublishedConfigurationRevision`, `Optional<T>`, `ContractJson`, `CanonicalSensorMap`, `TcChannelRules`) | all referenced members exist with the used shapes; no wire contract was changed |
| 7 | Synthetic-map parity artifact | re-parse of `config/examples/sensor-map.example.json` (108 slots, 106 Sensors, 2 Cannons at I7/I16, `scanOrderSynthetic` 1–106, `SYN-TC-nn:CHmm` pairs) | the adapter map reproduces slot identity, scan order, device distribution and channel identities; the parity assertion is authored in `tests/runtime.tests` and **not executed in Arena** |
| 8 | TS mirror | not re-run | No TypeScript or fixture file changed in Checkpoint A, so the mirror and its validator were not exercised; the last executed result remains the Owner-local 24/24 (§12.22) |

**Authored-time statements superseded.** The sentence in §12.22 ("Stage 0.3A-2 NOT AUTHORIZED")
records the 0.3A-1 closeout position and is preserved as authored-time state; the Owner's
2026-10-07 instruction recorded in §11.5 authorizes Checkpoint A of Stage 0.3A-2. Production
device access and Production Write remain **NOT AUTHORIZED**.

### 12.24 Stage 0.3A-2A Checkpoint A — Owner-local validation record (PASSED)

**Result (Owner-reported, 2026-10-07; authoritative because the Release build completed
successfully immediately before the test run).** Validated feature head
`a512aa76c4b2d7633d2a83b10c523c318b3a420e`; genuine lock-refresh commit
`55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c`. .NET SDK **10.0.401** (xUnit observed runtime
**.NET 10.0.12**).

| # | Check | Observed result |
| --- | --- | --- |
| 1 | Release build (`dotnet build -c Release --no-restore`) | **PASS — 0 warnings, 0 errors** |
| 2 | Full .NET test suite | **101 total / 101 passed / 0 failed / 0 skipped** |
| 3 | Locked restore | **PASS** |
| 4 | Boundary scan (S1–S9) | **0 findings** — clean |
| 5 | Working Tree after the Owner-local lock commit and push | **CLEAN** |
| 6 | Lock-refresh scope | Only `tests/runtime.tests/packages.lock.json` changed; adds the `wjss.adapters.simulator` Project dependency with `Wjss.Contracts` + `Wjss.Time`; no absolute Owner-local path; no unexpected source or fixture change |
| 7 | Pre-validation correction chain (Owner-local rounds, Arena-authored) | `b389805` CA1859 → `93e8f24` CS0051 → `b75bccb` synthetic-example test compile (CS1061 + CA1859) → `a512aa7` SensorChannels tamper isolation; each pushed as a focused commit on the same PR #5 branch |

**Arena position.** Arena authored the Checkpoint A sources and corrections, ran the static
battery (boundary scan, brace/whitespace/final-newline checks, `git diff --check`, contract
cross-checks) and **ran no .NET command**: the validated build, locked restore and 101/101 suite
are Owner-local evidence. Checkpoint A is complete for a development checkpoint and **NOT
MERGED**; only the Owner merges.

**Deliberate scope boundaries recorded at closeout.** Checkpoint A wrote no documentation of
its own corrections (per the strict correction scope), so the four correction SHAs above are
recorded here by this evidence commit and in the PR #5 review comments; the fixture generator
and the committed fixtures remain untouched (regeneration stays an Owner-local gate); the
`FixtureGenerator` ↔ adapter unification remains `[OPEN]`.

### 12.25 Stage 0.3A-2A Checkpoint B — Owner-local validation record (PASSED)

**Result (Owner-reported, 2026-10-07; authoritative because the Release build completed
successfully immediately before the test run).** Validated head
`cfa6d4a376bbf10a87db2349045cb6b9b57bb544` (the consolidated correction of the
`feat(runtime): add deterministic synthetic evolution and snapshot/delta foundation`
checkpoint), on the Owner-locally validated Checkpoint A store.

| # | Check | Observed result |
| --- | --- | --- |
| 1 | Release build (`dotnet build -c Release --no-restore`) | **PASS — 0 warnings, 0 errors** |
| 2 | Full .NET test suite | **129 total / 129 passed / 0 failed / 0 skipped** |
| 3 | Locked restore | **PASS** |
| 4 | Lock drift | **NONE** |
| 5 | Boundary scan (S1–S9) | **0 findings** — clean |
| 6 | Working Tree after the run | **CLEAN** |
| 7 | Correction rounds validated by this run | `dd45bf6` (CS0102: `DeltaApplyOutcome` factory renamed `Success`, property `Applied` retained) → `8692b77` (test-source alignment: named `seed:` arguments; four CA1861 constant arrays hoisted) → `cfa6d4a` (consolidated: catch-up continuation lookup + five test-side corrections) |

**What the consolidated correction actually changed (recorded truthfully from the final diff).**

*Product behaviour — one method.* `RuntimeDeltaHistory.CatchUpFrom` walked the chain with a
lookup that matched a Delta by its **own** revision, so it selected the step the consumer had
already applied. That produced a spurious fresh-Snapshot result when that step was no longer
retained, and a non-terminating walk when it was. The walk now selects the Delta that
**continues** the consumer revision (`FindContinuingUnlocked`, matched on `previousRevision`);
gap detection, eviction, ordering, the public `Find` semantics and the composed revision
sequence are unchanged. This is the only Product behaviour change of the three rounds.

*Test-side corrections — no Product behaviour involved.* Two structural `TrendPoint`
comparisons now use the single shared helper `RuntimeTestFixture.AssertTrendPointsEquivalent`
(`TrendPoint.Series` is `double?[]`, so record equality compared the array by reference and two
value-identical points were never equal); and three revision expectations were re-derived
(the committed Delta chain starts at revision 2, so capacity-3 retention is 4/5/6 and a
catch-up input of revision 3 advances to revision 6 — the earlier values were off by one).

**Arena position.** Arena authored the Checkpoint B sources and all three correction rounds,
ran the static battery (boundary scan, structural/whitespace checks, `git diff --check`,
contract cross-checks, a mechanical re-derivation of the revision and history arithmetic) and
**ran no .NET command**: the build, locked restore and 129/129 suite are Owner-local evidence.
Checkpoint B is complete for a development checkpoint and **NOT MERGED**.

### 12.26 Stage 0.3A-2A Checkpoint C — Arena validation record (source-only)

**Arena did not restore, build, or execute any .NET code.** The Checkpoint C sources are
**authored and statically reviewed only**; the Owner-local Function/Logic/UI review per runbook §14
is the validation of record, and no result is claimed here.

| Item | Checked in Arena |
| --- | --- |
| Boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` — exit 0, **0 findings (S1–S9)** on the Checkpoint C worktree |
| Inspector script | the single inline script of `apps/runtime/Inspector/index.html` extracted and passed to `node --check` (exit 0); HTML tag balance verified; the page requests only `/api/v1/snapshot`, `/api/v1/runtime`, `/api/v1/deltas` with `GET`, and caps retained Delta rows |
| C# source shape | per-file brace-depth balance over every changed/new C# file; `git diff --check`; trailing-whitespace, final-newline and tab sweeps |
| Contract cross-check | every referenced type and member cross-checked against `packages/contracts` and `packages/application/Runtime` (status/feed/sensor projections, `QueueSummary`, `AlarmSummary`, `PumpState`/`PumpRunState`, `DeltaJobEncoding`, `RuntimeLimits` capacities, `RuntimeStateComposer.InitialRevision`, `RuntimeStoreCounters`, `RuntimeDeltaHistory` accessors, `IClock`/`SystemClock`, `ProfileStartPolicy`) |
| Namespace hygiene | duplicate-simple-name scan across `Wjss.Contracts`, `Wjss.Runtime` and `Wjss.Runtime.Core` (none); every `using` directive resolved to a real namespace used by the file |
| Concurrency review | the evolution loop is the only tick caller; `RuntimeDeltaHistory` accessors lock internally and return frozen copies; counters are read through `Interlocked`; `DisposeAsync` observes the loop without rethrowing |
| Test sources authored | `tests/api.tests/RuntimeHostCompositionTests.cs` (7 facts), the rewritten `tests/config.tests/RuntimeStartGateSourceTests.cs` (6 facts, replacing the 4-fact stage-0.3A-1 stub pin) and the in-place `tests/runtime.tests/StageMarkerTests.cs` marker update (2 facts): **9 new tests for a source total of 138** (129 Owner-locally validated + 9). **Not executed anywhere yet** |
| NOT verified | every .NET claim; the two expected lock-file refreshes (`apps/runtime/packages.lock.json`, `tests/api.tests/packages.lock.json`); the browser behaviour and layout of the Inspector; anything the Owner-local run has not yet observed |

Slice C1+C2 is commit `06aca79d03c57b703768afffecf95735de7a91d5`; the C3 slice (Inspector page, Inspector source test, runbook §14
and this record) is the commit immediately following it and — per [`AGENTS.md`](../AGENTS.md)
§4.6 — does not record its own SHA.

### 12.27 Stage 0.3A-2A Checkpoint C — Owner-local validation record (PASSED)

**STAGE 0.3A-2C OWNER-LOCALLY VALIDATED** (Owner, 2026-10-07; authoritative because Arena can
neither restore, nor build, nor run this repository). Arena executed no .NET command in this record.

| Item | Observed |
| --- | --- |
| Validated head | `faa79145a925832baa2ac8685f7fecf7a093552d` (`chore: refresh Checkpoint C project locks`; parent `e6a5a6c`, the Inspector layout hotfix) |
| .NET SDK | `10.0.401` |
| Locked restore | **PASS** |
| Release build | **PASS — 0 warnings, 0 errors** |
| Fresh full .NET tests | **139 total / 139 passed / 0 failed / 0 skipped** |
| Boundary scan (S1–S9) | **0 findings** |
| Working tree after lock handoff | **CLEAN** |
| Lock handoff verification (Arena, static) | exactly the two expected files changed, +41 insertions, both parse as JSON, no absolute Owner-local path, no credential or secret, **no package version changed**; only Project-type graph entries added (`wjss.adapters.simulator`; `wjss.runtime`, `wjss.runtime.core`, `wjss.domain`, `wjss.adapters.simulator`, `wjss.time`) |
| Function and Logic review | **PASSED** — health live ALIVE; health ready READY with code `RUNTIME_READY`; revision progression and five-second advancement PASS; Delta history bounded 64 / 64; newest-first chain continuous (`newer.previousRevision == older.revision`); API `hasRevisionGap` false across the contiguous retained chain; Cannon projection I7 = CANNON_REAR (logical row 5, column 7) and I16 = CANNON_FRONT (logical row 5, column 16) with both logical and machine references; 108 logical slots / 106 Sensors / 212 Thermocouple channels and wall totals 24 / 29 / 24 / 29 retained; Synthetic Sensor values EVOLVING; rejected transitions 0 during review; queue 0 / 8 placeholder only; no Active Job; Pump STOPPED placeholder only; no command or write path |
| Inspector UI review (1920 x 1080) | **PASSED — punchlist CLOSED** — no critical UI blocker; the Runtime state layout correction holds (card uses the remaining top-row width, placeholder text wraps by phrase, compact Snapshot card, card-local table scrolling, 24-hour `HH:mm:ss.mmm` timestamps, I7 / I16 logical Cannon labels, Delta chain displays clean, no page-level horizontal scroll); the authorized minor presentation punchlist was applied in the commit immediately following this record and the final Owner visual review closed it at `b97d05a` |
| Owner final visual review (2026-10-07) | **FINAL OWNER VISUAL REVIEW (2026-10-07): Inspector UI/UX review PASSED; the minor punchlist is CLOSED.** Verified visually at 1920 x 1080: human-readable Foundation captions present; Queue and Pump placeholder explanations visually secondary; `SENSOR VALUES — SYNTHETIC EVOLUTION` title present; Foundation Metrics complete two-column layout; Runtime State uses the available width; no one-word-per-line wrapping remains; Snapshot compact; Delta and Sensor tables retain card-local scrolling; `HH:mm:ss.mmm` timestamps; Delta chain displays clean; I7 and I16 are the visible Cannon labels; synthetic UNCERTAIN and BAD quality states visible with reasons; no page-level horizontal scroll; safety footer and read-only boundary visible; no Critical UI blocker remains. One **optional future polish** item is recorded and explicitly non-blocking (top-row vertical balance, because Foundation Metrics is taller than Runtime State): it is deferred, does not require a change round, and is not authorised as part of Checkpoint C. |
| Test accounting | 129 (Checkpoint B, Owner-validated) + 9 (Checkpoint C additions) + 1 (Delta-feed continuity projection test) = **139**, all passing in this run |
| Arena position | Arena did not restore, build or execute any .NET code in this record; the Owner-local evidence above is authoritative |
| PR position | PR #5 **OPEN — NOT MERGED** (only the Owner merges). `TEST_HARDWARE` and `PRODUCTION` remain **NOT AUTHORIZED** |
| Checkpoint C correction chain (all pushed to the same branch/PR) | `06aca79` feature (composition + read-only API), `7857b3c` Inspector + runbook, `1d7b105` readiness verifies the Snapshot projection, `01bb2aa` CS0120 seed-type disambiguation, `349264a` ambiguous Inspector route removed, `40284b4` Delta chain status corrected in the feed projection + Cannon logical labels + presentation punchlist, `e6a5a6c` Runtime state layout width restored, `faa79145` genuine Owner-local lock refresh |

## 13. Required positive confirmations

The documentation explicitly contains each of the following:

| # | Statement | Where |
| --- | --- | --- |
| 1 | One Water Jet to one Isolation Valve, dedicated, never shared | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8, [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001 |
| 2 | At most one Cleaning Job ACTIVE at a time | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-001 |
| 3 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 4 | GlobalQueue stop and rebuild policy | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9 |
| 5 | Source ownership after deduplication | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3 |
| 6 | Cleared-state acknowledgement requirement | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 7 | DCS communication health | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 8 | DCS Permissive Override restrictions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009 |
| 9 | Operations UI close guard | [`ARCHITECTURE.md`](ARCHITECTURE.md) §4.12, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 10 | Production Write not authorized | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §4, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-002 |
| 11 | WAGO fail-safe not verified | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-001 |
| 12 | The UI cannot write to hardware and owns no device session | [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.3, [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-001 to ARC-003 |
| 13 | The Equipment Runtime is the only physical-device-session owner | [`decisions/ADR-0007`](decisions/ADR-0007-runtime-process-model.md), [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.2 |
| 14 | Domain logic does not depend on vendor device libraries | [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-007, [`decisions/ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) |
| 15 | Simulation is not hardware verification or certification | [`decisions/ADR-0012`](decisions/ADR-0012-simulator-first-development.md), [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §3.7 |

Apart from the Stage 0.2.1A synthetic spike validation recorded in section 12.3 (Arena, Node
and jsdom only), this document must not be read as claiming any test, build, database,
hardware, or device verification. See section 9.

---

### 12.28 Stage 0.3A-3 Checkpoint B — Owner-local validation record (PASSED)

Owner-supplied evidence (Arena never executed .NET); recorded at the closeout commit.

| Item | Evidence |
| --- | --- |
| SDK | `10.0.401` |
| Validated feature head | `2db853a723a74399430ddd84900c40371fe56b29` |
| Genuine lock refresh | `0728df61f917ba61f6dd3b8bf6d12e68dfa01d20` — parent `2db853a7`; exactly `tests/config.tests/packages.lock.json` (+6): project graph `wjss.domain → Wjss.Contracts`; valid JSON; no absolute path; no credential/secret; no package-version change; no Owner CSV or acquisition value; adopted byte-for-byte |
| Normal restore | PASS |
| Locked restore | PASS |
| Release build | PASS — 0 warnings / 0 errors |
| Fresh full .NET tests | 186 total / 186 passed / 0 failed / 0 skipped |
| Boundary scan | 0 findings — S1–S9 clean |
| JSON examples | PASS |
| Owner acquisition data in the migrated example | NONE |
| Final Owner-local Working Tree | CLEAN |
| Non-authoritative observation excluded | "139 total / 139 passed" at the implementation head (`dotnet test --no-build` over stale binaries after a failed test-project build) is explicitly NOT recorded as validation; its four behavioural failures and all compile/analyzer diagnostics were corrected on the chain `18f853a5` → `901a070` → `79fa0ba5` → `2db853a7` before the fresh authoritative run |
| Scope | Documentation-only closeout commit on top of the Owner handoff: CHANGELOG, this file (§2 row, §11.8, §12.28), `STAGE_0.3A_PLAN.md` §5d, `ADR-0017` implementation-evidence section, and the new [`STAGE_0.3A-3_CHECKPOINT_B.md`](STAGE_0.3A-3_CHECKPOINT_B.md). Product source, contracts, importer, validator, topology catalog, tests, fixtures, JSON examples, project files, and lock files untouched |

### 12.29 Stage 0.3A-3 Checkpoint C — Arena validation record (source-only)

Implementation record of the atomic Runtime topology migration (Owner authorization
2026-10-08; PR head `0c3dcea` verified before authoring). Slices C1 `d378edf2` and
C2 `27591ea1` plus the C3 Inspector/docs commit. **Arena verification:** boundary scan
0 findings (S1–S9 clean) after every slice; TypeScript mirror executed in Arena
(`npm ci` + `npm run check`: tsc clean, **31 total / 31 passed / 0 failed**) against the
migrated fixtures and example; Inspector inline script `node --check` OK and HTML tag
balance OK; per-file brace-depth balance over all 36 changed C# sources OK;
`git diff --check` clean. **No .NET restore, build or test execution happened in Arena**;
the fixture parity suite and the full xUnit suite are Owner-local gates. Full record:
[`STAGE_0.3A-3_CHECKPOINT_C.md`](STAGE_0.3A-3_CHECKPOINT_C.md).

### 12.30 Stage 0.3A-3 Checkpoint C — Owner-local validation record (PASSED)

Recorded from the Owner's runs (Arena executed no .NET command). Validated feature head
`162ad7ff1e5c7bab398de6c6d1a38131f17082de`; Owner artifact commit
`bac36add7011c72d6ab03ea5d3e3664ed82a197c` (parent `162ad7f`; exactly five genuine
`packages.lock.json` refreshes + the regenerated `sensor-map.example.json`; all lock files
valid JSON, zero package-version changes, only the expected `Wjss.Domain` project-graph
edges — direct in `adapters/simulator` and `tests/integration`, transitive through
`wjss.adapters.simulator` in `apps/runtime`, `tests/api.tests`, `tests/runtime.tests` —
no absolute Owner-local path, no credential or secret; the regenerated example's only
semantic drift from the prior committed example is the corrected NON_SENSOR_GAP note, with
108 logical positions / 106 Sensors / 212 unique TC channels, I7 anchored to WJ3 and I16
anchored to WJ1). Validation: SDK `10.0.401`, xUnit runtime .NET `10.0.12`; normal restore
PASS; locked restore PASS; Release build **0 warnings / 0 errors**; fresh full .NET suite
**201 total / 201 passed / 0 failed / 0 skipped**; fixture generation/update **7 / 7** and
parity after update-mode removal **7 / 7**; TypeScript typecheck PASS and **31 / 31**;
boundary scan **0 findings (S1–S9 clean)**; all three JSON examples parsed; final
Owner-local working tree **CLEAN**. Owner reviews: Function **PASSED**, Logic **PASSED**,
Inspector UI/UX **PASSED** (no critical UI blocker); minor punchlist deferred and
non-blocking, Inspector presentation frozen for this checkpoint. Historical failed
build/test observations before the correction chain are not acceptance evidence. Full
record: [`STAGE_0.3A-3_CHECKPOINT_C.md`](STAGE_0.3A-3_CHECKPOINT_C.md) §16 and
[`STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) §14.10.

### 12.31 Stage 0.4A CP-2 — Owner-local validation record and Final Source Review (2026-10-08)

Recorded from the Owner's runs (Arena executed no .NET command). Authoritative Owner-local
head: `1f94991b8fddd5b1e7e158c8f02c34a70ac0c14c`. Base `main` @ `8323f78c7ec2480bb30cbdd1432b6caa8e32c601`.
Environment: .NET SDK `10.0.401`; xUnit runtime .NET `10.0.12`. Results: locked restore PASS, no
lock drift; Release build **0 warnings / 0 errors**; full .NET tests **300 total / 300 passed / 0
failed / 0 skipped** (CP-1 validated suite 253 + CP-2 new facts 47); fixture parity **7 / 7**;
TypeScript typecheck PASS and tests **31 / 31**; boundary scan **0 findings (S1–S9 clean)**;
`git diff --check` PASS; final Owner-local working tree **CLEAN**; no artifact commit required.

CP-2 Final Source Review (read-only, Arena, source-level, no source or test edits): **PASSED**.
F2 single evidence sequence: **PASSED**. F3 release under the critical latch: **PASSED** (matches
the approved behaviour). EXPECTED_STOP during CLEANING: **PASSED** as a refusal with no effect on
lifecycle, water, Queue, QueueRevision or latch. Full answers, findings and follow-ups:
[`STAGE_0.4A_CP-2_CHECKPOINT.md`](STAGE_0.4A_CP-2_CHECKPOINT.md) §19.

Authority after this record: PR #8 OPEN, NOT MERGED. CP-3 NOT AUTHORIZED. CP-4 NOT AUTHORIZED.
TEST_HARDWARE and PRODUCTION device access NOT AUTHORIZED.

## Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and implementation status
- [`ROADMAP.md`](ROADMAP.md) — forward view
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — current prohibition on control writes
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — verification levels and what has not been tested

### CP-3c-1 development scope
Atomic Runtime publication is authorized as a library foundation only. The existing Host remains on its legacy stores; no scenario integration or GET behavior changes. See `STAGE_0.4A_CP-3C-1_CHECKPOINT.md`. Owner-local validation required.

### Stage 0.4A CP-3c-1 closeout (2026-10-09)

Owner-reported validation at code head `29f133ce6598bc2eb489273a2f2d40f8827deb5f` (.NET SDK 10.0.401; not run in Arena): locked restore PASS, lock drift NONE; Release build PASS, 0 warnings, 0 errors; RuntimePublicationTests **20/20**, Runtime.Core **450/450**, full .NET **572/572**, fixture parity **7/7**, TypeScript typecheck PASS and tests **31/31** (0 failed); boundary scan **0 findings, S1–S9 clean**; `git diff --check` PASS; final Owner-local working tree CLEAN. Failed 0, skipped 0 in each reported .NET suite; no Owner-local artifact commit required.

Final Source Review (static, exact validated code head): **PASS, no blocking defect**. CP-3c-1 delivers the library-only atomic State/Delta publication aggregate with one writer, bounded contiguous history and detached reader/result snapshots. The full-window trend failure was fixed by checking equal-count *unchanged content* before testing the exact oldest-eviction-plus-append shift. The internal-Delta JSON fingerprint failure was a test defect: fingerprints now serialize `ProjectWire(delta)` and previous Sensors, never raw `RuntimeDelta`. The reader-facing mutable `TrendPoint.Series` alias was corrected with a defensive-copy reader boundary and nested typed freeze, without changing the contract. Analyzer corrections CA1861 and xUnit1031 are closed at the validated code head. No Host integration, Runtime API integration, Inspector change, contract, fixture or TypeScript change is included. CP-3c-2 and CP-4 are NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION device access are NOT AUTHORIZED. PR #10 remains OPEN, NOT MERGED, for Owner merge review.
