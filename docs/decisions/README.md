# Architecture Decision Records (ADR)

**Status of this index:** [APPROVED] at the Stage 0.1 documentation foundation, corrected
by the Owner-confirmed documentation review punchlist.

**Stage status:** Stage 0.1 merged to `main` through PR #1. Stage 0.2 — *Technology and
Solution Architecture Decision* — **OWNER ACCEPTED / MERGED** (source
`5bcf1b33f924ab30590a55736676200115874fa1`, merge `e779f8ad`); ADR-0006 to ADR-0013
**ACCEPTED** (architecture direction, not implemented). Stage 0.2.1A — React UI and Runtime
Feasibility Spike — Scope Gate **APPROVED**, Coding Start **APPROVED**, implementation
**COMPLETE FOR DEVELOPMENT CHECKPOINT** (Owner-local final Edge gate **PASS**, Owner manual
review **PASS**), **MERGED** (PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`).
**React selected as the
Primary UI Framework** (Owner decision, 2026-10-07; Production transport and chart library remain
`[OPEN]`). Blazor counter-spike **NOT REQUIRED** unless a future material blocker is identified.
Main Development Scope Gate **PENDING**. Stage 0.3 — Option-C amended gate ACTIVE; **Stage 0.3A-1
source checkpoint authored, awaiting Owner-local .NET validation** (see section below). Production device access `[NOT AUTHORIZED]`.

This folder holds the decisions that shape the WaterJet Sentinel Suite. An ADR records a
decision, its context, and its consequences so that later work does not re-litigate or
silently contradict an approved position.

---

## Why these records exist

1. New contributors must be able to see *why* a constraint exists, not only *that* it
   exists.
2. Constraints that come from the physical plant or from an Owner decision must not be
   removed by a refactoring.
3. Where a decision depends on something unverified — a commissioning value, a watchdog
   behaviour, a bench test — the ADR must say so explicitly.

## Status values

| Status | Meaning |
| --- | --- |
| `ACCEPTED` | Approved by the Owner; binding on future work |
| `PROPOSED` | Drafted, not yet approved; not binding |
| `SUPERSEDED BY ADR-xxxx` | Replaced; the original text is retained, never deleted |
| `DEPRECATED` | Still recorded, no longer applicable; retained for history |

An ADR is never edited to reverse its meaning. To change a decision, write a new ADR that
supersedes the old one. Corrections of factual errors, formatting, link fixes, and
propagation of Owner-confirmed review decisions are permitted and must be noted in
[`../../CHANGELOG.md`](../../CHANGELOG.md).

**ADR status is not implementation status.** An `ACCEPTED` ADR records an approved design
decision. It does not mean the decision is implemented, tested, or Owner accepted as an
implementation. See [`../MASTER_PLAN.md`](../MASTER_PLAN.md).

**Product identity ADR-0001 does not decide the technology stack.** The UI delivery
architecture — browser-based, desktop, or hybrid local-web — remains `[OPEN]` and is a
question for a future approved technology decision.

## Index

### Accepted records

| ADR | Title | Status |
| --- | --- | --- |
| [ADR-0001](ADR-0001-product-identity.md) | Product identity | `ACCEPTED` (technology stack `OPEN` inside) |
| [ADR-0002](ADR-0002-deployment-architecture.md) | Deployment architecture | `ACCEPTED` (network topology `OPEN` inside) |
| [ADR-0003](ADR-0003-queue-arbitration.md) | Queue arbitration and sequencing | `ACCEPTED` |
| [ADR-0004](ADR-0004-historian-strategy.md) | Historian strategy | `ACCEPTED` (retention defaults `PROPOSED`, capacity `NOT VERIFIED` inside) |
| [ADR-0005](ADR-0005-hardware-safety-boundary.md) | Hardware safety boundary | `ACCEPTED` |

### Stage 0.2 records — accepted by the Owner

The Owner accepted Stage 0.2 (merged through PR #2, merge commit
`e779f8ad2c856e367fd65985007a3da411bd0e73`) and recorded ADR-0006 to ADR-0013 `ACCEPTED` as
architecture direction. **Accepted does not mean implemented.** Selections inside them that are
still marked `[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` keep those markers, and acceptance
authorises no capability and no later stage.

**React is selected as the Primary UI Framework** (Owner decision, 2026-10-07). [`ADR-0006`](ADR-0006-ui-delivery-model.md)
recorded Candidate A (React + TypeScript + Vite) and Candidate B (Blazor Hybrid); the Owner selected
React after the Stage 0.2.1A synthetic React feasibility spike
([`../spikes/stage-0.2.1a-plan.md`](../spikes/stage-0.2.1a-plan.md)) passed the Owner-local final
Edge gate and manual review. The Blazor counter-spike is **not required** unless a future material
blocker is identified. ADR-0006 and ADR-0008 carry dated update notes; their history is unchanged.

| ADR | Title | Status |
| --- | --- | --- |
| [ADR-0006](ADR-0006-ui-delivery-model.md) | UI delivery model | `ACCEPTED` |
| [ADR-0007](ADR-0007-runtime-process-model.md) | Runtime process model | `ACCEPTED` |
| [ADR-0008](ADR-0008-technology-stack.md) | Technology stack | `ACCEPTED` (several selections `OPEN` inside) |
| [ADR-0009](ADR-0009-database-access-and-migrations.md) | Database access and migration strategy | `ACCEPTED` (provider pin, sizing, overflow policy `OPEN` inside) |
| [ADR-0010](ADR-0010-device-adapter-boundary.md) | Device adapter boundary | `ACCEPTED` (library selections `OPEN` inside) |
| [ADR-0011](ADR-0011-configuration-and-secrets.md) | Configuration and secrets | `ACCEPTED` (secret store `OPEN` inside) |
| [ADR-0012](ADR-0012-simulator-first-development.md) | Simulator-first development | `ACCEPTED` |
| [ADR-0013](ADR-0013-offline-deployment.md) | Offline deployment | `ACCEPTED` (package format and startup mechanism `OPEN` inside) |

### Stage 0.3A records — DRAFT, authored in the source checkpoint

Stage 0.3A-1 (Option-C amended gate) drafts three records alongside the product foundation
skeleton. **All three are DRAFT: they are not `PROPOSED`, carry no authority, and bind no
work until the Owner accepts them.** The spike-derived contract facts they record are
already visible in the authored sources (`packages/contracts`, `tools/boundary-scan`).

| ADR | Title | Status |
| --- | --- | --- |
| [ADR-0014](ADR-0014-product-contract-skeleton.md) | Product contract skeleton and queue capacity | `DRAFT` (three-state `activeJob` encoding — the accepted spike baseline, restored by Owner review) |
| [ADR-0015](ADR-0015-ui-transport-and-chart-direction.md) | UI production transport and chart direction | `DRAFT` (fills the `[OPEN]` transport/chart selections; does not re-open React) |
| [ADR-0016](ADR-0016-kiosk-shell-direction.md) | Kiosk shell direction | `DRAFT` (WinForms + WebView2 detection-only; drafting only — ADR-0006's `[OPEN]` marker stands until acceptance) |
| [ADR-0017](ADR-0017-equipment-topology-and-legacy-parameter-migration.md) | Equipment topology and legacy parameter migration | `ACCEPTED` (Owner review PASS, 2026-10-08 — I7/I16 as NON_SENSOR_GAP anchors supersede the Cannon-slot interpretation; WJ1–WJ8 / IV1–IV8 topology, pairing, and the `sensorparam.csv` `cannon n → WJn` migration specification; Checkpoint B implemented on PR #6, Owner-local validation pending) |

`PROPOSED` means *drafted and submitted*, never *approved*. Acceptance of an ADR within
Stage 0.2 means acceptance of a **documentation decision**, not implementation proof. See
[`../CURRENT_STATE.md`](../CURRENT_STATE.md).

## Template

Copy this shape for a new record. Add it to the index above in the same change.

```markdown
# ADR-XXXX — Title

- **Status:** PROPOSED | ACCEPTED | SUPERSEDED BY ADR-YYYY | DEPRECATED
- **Date:** YYYY-MM-DD
- **Supersedes:** ADR-YYYY (if applicable)
- **Scope:** what this decision governs
- **Authority:** the Stage Gate or Owner instruction that approved it

## Context
What forces the decision. Include the physical, operational, and contractual constraints.

## Decision
What is decided, stated as binding rules.

## Alternatives considered
Each alternative and the reason it was rejected. Rejections must be on technical or
operational grounds — never because a delivery technology is merely more convenient to
close, replace, or navigate away from.

## Consequences
What becomes easier, what becomes harder, and what is now prohibited.

## Risks
The risks the decision creates or leaves open, with a mitigation direction and a status.
"Mitigation direction" is not a claim that a mitigation exists.

## Verification status
What about this decision is `[NOT VERIFIED]`, and what evidence would resolve it.

## Follow-up gates
Which later Scope Gate must close each remaining `[OPEN]` item. A follow-up gate is not an
authorisation: the gate must still be approved by the Owner.

## Relationship to protected decisions
Which protected decisions this record touches, and an explicit statement that none of them is
modified, weakened, or adapted to fit the technology choice.

## References
Relative links to related documents.
```

## Rules for new ADRs

1. One decision per record. If a change touches two independent decisions, write two ADRs.
2. State the authority. An ADR that quotes no Stage Gate and no Owner instruction is
   `PROPOSED` by definition.
3. Do not invent values. If a decision needs a value that has not been measured, mark it
   `[NOT VERIFIED]` and describe how it will be obtained.
4. Never delete a superseded ADR.
5. Keep the ADR consistent with [`../MASTER_PLAN.md`](../MASTER_PLAN.md),
   [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md), and [`../../AGENTS.md`](../../AGENTS.md).
6. Do not record an implementation as accepted, merged, or complete in an ADR. ADRs record
   decisions, not delivery status.
7. Record the design status of every decision using the four-way vocabulary used by
   [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 13: `[APPROVED]` or `[OWNER CONFIRMED]`
   for behaviour already binding from an approved source, `[PROPOSED]` for a selection made in
   this Stage that still requires Owner acceptance, `[OPEN]` for a question that cannot be
   closed from available evidence, and `[NOT VERIFIED]` for anything that depends on a
   measurement, bench test, field test, or hardware observation.
8. Never state a version number, support date, licence term, address, coordinate, limit,
   setpoint, or timeout that has not been observed from an authoritative source. If it is not
   observed, mark it `[OPEN]` or `[NOT VERIFIED]` and say which gate must close it.

---

## Related documents

- [`../../AGENTS.md`](../../AGENTS.md) — authority order and scope-lock rules
- [`../MASTER_PLAN.md`](../MASTER_PLAN.md) — stage gates and implementation status
- [`../CURRENT_STATE.md`](../CURRENT_STATE.md) — verified state and open items
