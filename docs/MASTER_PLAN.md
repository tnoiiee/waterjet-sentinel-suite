# Master Plan — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] for the stage-gate discipline and for the Stage 0.1
position; stage contents beyond Stage 0.1 are [PROPOSED].
**Governing authority:** [`AGENTS.md`](../AGENTS.md) and the approved Stage 0.1 Scope Gate.

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
- Owner review before the next stage begins.

No stage begins automatically when the previous one ends. A completed stage produces a
report and stops. See [`AGENTS.md`](../AGENTS.md) section 11 for the required stop
conditions.

## 2. What a stage gate is

A Stage Gate is the only authority that permits new artefacts. It must state, at minimum:

1. The stage identifier and title.
2. What may be created or modified, as an explicit file list or an explicit category list.
3. What is explicitly out of scope for that stage.
4. The validation the contributor must perform and report.
5. The delivery instruction (branch, checkpoint, and whether a pull request is expected).

Behaviour described in a Scope Gate becomes `[APPROVED]` behaviour. Behaviour described
anywhere else remains `[PROPOSED]` until a later gate approves it.

## 3. Stage ledger

Status vocabulary: `[APPROVED]`, `[PROPOSED]`, `[OPEN]`, `[NOT AUTHORIZED]`,
`[NOT VERIFIED]`.

| Stage | Title | Status | Deliverable |
| --- | --- | --- | --- |
| 0.1 | Repository Documentation Foundation | `[APPROVED]` — delivered | Repository governance and documentation baseline `0.1.0` |
| 0.2 | Requirements and Configuration Model Refinement | `[PROPOSED]` | Owner-ratified requirement set, configuration field specification, and resolution of the open items in [`CURRENT_STATE.md`](CURRENT_STATE.md) |
| 0.3 | Architecture and Interface Specification | `[PROPOSED]` | Component boundaries, interface contracts, error taxonomy, and a testable specification for queue and cleaning logic — still documentation and specification only |
| 0.4 | Offline Domain and Queue Engine Implementation | `[PROPOSED]` | First source code: pure domain logic with a simulator and automated tests; no device access |
| 0.5 | Read-Only Monitoring Integration | `[PROPOSED]` | Modbus TCP read path against a simulator only; no production writes |
| 0.6 | Supervisory Control Path with Simulator | `[PROPOSED]` | Valve, pump, and motion command paths exercised against a simulator only |
| 0.7 | Historian, Alarm, Event, and Audit Subsystems | `[PROPOSED]` | Storage, retention, and cleanup mechanics verified against a test database |
| 0.8 | Operator Interface and Kiosk Operation | `[PROPOSED]` | Control Room Kiosk experience, responsiveness, and operator workflows |
| 0.9 | Bench Verification | `[NOT AUTHORIZED]` | Hardware benchmark evidence for the items listed in [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md). Requires separate, explicit Owner authorisation and physical supervision. |
| 1.0 | Deployment Acceptance | `[NOT AUTHORIZED]` | Owner acceptance of a release candidate on a Boiler Unit |

Stages 0.2 through 0.8 are a **candidate** decomposition. The Owner may merge, split,
reorder, or replace them. They are recorded here so that work is not invented ad hoc, not
because they are approved.

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
   and production thresholds — must never be invented, and production instances must never
   be committed.
4. Requirements, architecture, and documentation must remain consistent. An inconsistency
   stops the affected part of the work and is reported.
5. Reliability, stability, deterministic behaviour, recoverability, operator usability,
   maintainability, and appropriate baseline security rank above feature breadth.
6. Standards are used as guidance and design inspiration only. No claim of certification
   or compliance may be made at any stage.

## 6. Definition of done for a documentation stage

A documentation stage is done when:

- Every named file exists and is complete.
- Internal links resolve (checked mechanically where practical).
- The status legend is applied consistently, and no unapproved behaviour is presented as
  approved.
- Prohibited content is absent (checked by search).
- The diff has been reviewed before committing.
- The checkpoint commit exists with a CHANGED / UNCHANGED / NOT VERIFIED body.
- The stage branch is pushed, and a pull request exists if permissions allow.
- The pull request is **not** merged by the contributor.

---

## Related documents

- [`AGENTS.md`](../AGENTS.md) — working contract
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state and open items
- [`ROADMAP.md`](ROADMAP.md) — forward view of planned capability
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — requirement register
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — planned verification approach
- [`decisions/README.md`](decisions/README.md) — decision records
