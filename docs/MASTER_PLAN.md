# Master Plan — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the stage-gate discipline, for the Stage 0.1 and Stage 0.2
positions, and for the approved Stage 0.2 Scope Gate; **stage contents beyond Stage 0.2 remain
`[PROPOSED]`**.
**Governing authority:** [`AGENTS.md`](../AGENTS.md), the approved Stage 0.1 Scope Gate, and
the approved Stage 0.2 Scope Gate — *Technology and Solution Architecture Decision*.

This document defines how delivery is staged, what a stage gate is, and what evidence a
stage must produce. It deliberately does **not** schedule work, and it does not authorise
any future stage.

---

## 1. Delivery principle

Work proceeds in small, explicitly approved stages. Each stage has:

- A Scope Gate issued by the Owner, naming exactly what may be created or changed.
- A single reviewable deliverable set.
- Documentation validation evidence.
- A Development Checkpoint commit.
- Owner review before the next stage begins. A review may request changes, which are
  applied as review-correction commits on the same branch and pull request.

No stage begins automatically when the previous one ends. A completed stage produces a
report and stops. See [`AGENTS.md`](../AGENTS.md) section 11 for the required stop
conditions.

**Gate approval and implementation acceptance are separate.** A Stage Gate being approved
means the work is authorised. It does not mean the resulting implementation is accepted.
Only the Owner may record acceptance.

## 2. What a stage gate is

A Stage Gate is the only authority that permits new artefacts. It must state, at minimum:

1. The stage identifier and title.
2. What may be created or modified, as an explicit file list or an explicit category list.
3. What is explicitly out of scope for that stage.
4. The validation the contributor must perform and report.
5. The delivery instruction (branch, checkpoint, and whether a pull request is expected).

Behaviour described in a Scope Gate becomes `[APPROVED]` behaviour. Behaviour confirmed by
the Owner during a documentation review becomes `[OWNER CONFIRMED]` behaviour. Behaviour
described anywhere else remains `[PROPOSED]` until a later gate approves it.

## 3. Stage ledger

Status vocabulary: `[APPROVED]`, `[OWNER CONFIRMED]`, `[PROPOSED]`, `[OPEN]`,
`[NOT AUTHORIZED]`, `[NOT VERIFIED]`, plus implementation status: `SUBMITTED FOR OWNER
REVIEW`, `CHANGES REQUESTED`, `IN PROGRESS`, `OWNER ACCEPTED`, `NOT MERGED`, `MERGED`.

| Stage | Title | Gate | Implementation | Deliverable |
| --- | --- | --- | --- | --- |
| 0.1 | Repository Documentation Foundation | `[APPROVED]` | **MERGED** — merged to `main` through PR #1 (merge commit `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`) | Repository governance and documentation foundation |
| 0.2 | Technology and Solution Architecture Decision | `[APPROVED]` | **SUBMITTED FOR OWNER REVIEW** — Owner manual review **PENDING**; **NOT MERGED** | Documentation-only technology and solution architecture decisions: UI delivery model, runtime process model, technology stack, database access and migrations, device adapter boundary, configuration and secrets, simulator-first development, offline deployment, and a documented repository structure direction — recorded as ADR candidates |
| 0.3 | Architecture and Interface Specification | `[PROPOSED]` | Not started | Component boundaries, interface contracts, error taxonomy, and a testable specification for queue and cleaning logic — still documentation and specification only |
| 0.4 | Offline Domain and Queue Engine Implementation | `[PROPOSED]` | Not started | First source code: pure domain logic with a simulator and automated tests; no device access |
| 0.5 | Read-Only Monitoring Integration | `[PROPOSED]` | Not started | Modbus TCP read path against a simulator only; no production writes |
| 0.6 | Supervisory Control Path with Simulator | `[PROPOSED]` | Not started | Valve, pump, and motion command paths exercised against a simulator only |
| 0.7 | Historian, Alarm, Event, and Audit Subsystems | `[PROPOSED]` | Not started | Storage, retention, and cleanup mechanics verified against a test database |
| 0.8 | Operator Interface and Kiosk Operation | `[PROPOSED]` | Not started | Control Room Kiosk experience, responsiveness, close guard, and operator workflows |
| 0.9 | Bench Verification | `[NOT AUTHORIZED]` | Not started | Hardware benchmark evidence for the items listed in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md). Requires separate, explicit Owner authorisation and physical supervision. |
| 1.0 | Deployment Acceptance | `[NOT AUTHORIZED]` | Not started | Owner acceptance of a release candidate on a Boiler Unit |

Stages 0.3 through 0.8 are a **candidate** decomposition. The Owner may merge, split,
reorder, or replace them. They are recorded here so that work is not invented ad hoc, not
because they are approved. Stage 0.3 and later remain `[PROPOSED]` and are `[NOT AUTHORIZED]`
until a later Owner Scope Gate approves them.

### 3.1 Recorded reconciliation of the Stage 0.2 title

The Stage 0.1 candidate decomposition listed Stage 0.2 as *Requirements and Configuration
Model Refinement* and Stage 0.3 as *Architecture and Interface Specification*. Both entries
were `[PROPOSED]` candidate decomposition, not approved stations.

The approved Stage 0.2 Scope Gate defines Stage 0.2 as **Technology and Solution Architecture
Decision**, a documentation and architecture-decision checkpoint. That approved gate takes
precedence for Stage 0.2, so the ledger above records the approved title. Consequences:

1. The previously proposed Stage 0.2 deliverable — Owner-ratified requirement set and
   configuration field specification — is **not authorised** by the approved Stage 0.2 gate
   and remains `[PROPOSED]` and `[NOT AUTHORIZED]`.
2. The previously proposed Stage 0.3 deliverable remains `[PROPOSED]` and
   `[NOT AUTHORIZED]`. It is **not** the same thing as Stage 0.2 and is not started.
3. The remaining open requirement and configuration items are listed in
   [`CURRENT_STATE.md`](CURRENT_STATE.md) section 8 and in
   [`ARCHITECTURE.md`](ARCHITECTURE.md) section 22, each with the gate that must close it.

## 4. Gate evidence rules

For every stage, the report must contain:

- **CHANGED** — every file created or modified, with a short purpose.
- **UNCHANGED** — an explicit statement of what was not touched.
- **NOT VERIFIED** — every unproven value, behaviour, or integration.
- **Validation performed** — the exact checks executed and their observed results.

A stage is not complete until its validation evidence exists and its discrepancies are
documented. Claims of success without observed tool output are prohibited.

## 5. Standing constraints across every stage

These constraints apply to all stages unless a gate explicitly lifts one of them:

1. Production device access is `[NOT AUTHORIZED]`. See
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md).
2. Production valve and pump write control is `[NOT AUTHORIZED]` until bench verification
   is complete and recorded.
3. Production values — addresses, register maps, tag lists, coordinates, travel limits,
   pulses per engineering unit, speeds, accelerations, decelerations, pressure setpoints,
   DCS permissive definitions, and production thresholds — must never be invented, and
   production instances must never be committed.
4. Requirements, architecture, and documentation must remain consistent. An inconsistency
   stops the affected part of the work and is reported.
5. Reliability, stability, deterministic behaviour, recoverability, operator usability,
   maintainability, and appropriate baseline security rank above feature breadth.
6. Standards are used as guidance and design inspiration only. No claim of certification
   or compliance may be made at any stage.
7. **At most one Cleaning Job may be ACTIVE at any time. Parallel Water Jet cleaning is
   prohibited.** No stage may relax this.
8. **No application version exists.** A documentation stage does not establish one, and
   the documentation versioning policy remains `[OPEN]`.
9. **The Operations UI close guard is an operational usability control, not a safety
   protection.** No stage may describe it as hardware fail-safe.

## 6. Definition of done for a documentation stage

A documentation stage is done when:

- Every named file exists and is complete.
- Internal links resolve (checked mechanically where practical).
- The status legend is applied consistently, and no unapproved behaviour is presented as
  approved.
- Owner-confirmed review decisions are propagated to every affected document.
- Prohibited content is absent (checked by search).
- The diff has been reviewed before committing.
- The checkpoint commit exists with a CHANGED / UNCHANGED / NOT VERIFIED body.
- The stage branch is pushed, and a pull request exists if permissions allow.
- The pull request is **not** merged by the contributor.

A stage that has received review changes is not done until a review-correction checkpoint
has been submitted and re-review has been requested.

---

## Related documents

- [`AGENTS.md`](../AGENTS.md) — working contract
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state, stage status, and open items
- [`ROADMAP.md`](ROADMAP.md) — forward view of planned capability
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`decisions/README.md`](decisions/README.md) — decision records
