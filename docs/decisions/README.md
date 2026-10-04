# Architecture Decision Records (ADR)

**Status of this index:** [APPROVED] at the Stage 0.1 documentation foundation, corrected
by the Owner-confirmed documentation review punchlist.

**Stage status:** Stage 0.1 Scope Gate `[APPROVED]`; implementation submitted for Owner
review; documentation review changes requested / in progress; Stage 0.2 `[NOT AUTHORIZED]`.

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

| ADR | Title | Status |
| --- | --- | --- |
| [ADR-0001](ADR-0001-product-identity.md) | Product identity | `ACCEPTED` (technology stack `OPEN` inside) |
| [ADR-0002](ADR-0002-deployment-architecture.md) | Deployment architecture | `ACCEPTED` (network topology `OPEN` inside) |
| [ADR-0003](ADR-0003-queue-arbitration.md) | Queue arbitration and sequencing | `ACCEPTED` |
| [ADR-0004](ADR-0004-historian-strategy.md) | Historian strategy | `ACCEPTED` (retention defaults `PROPOSED`, capacity `NOT VERIFIED` inside) |
| [ADR-0005](ADR-0005-hardware-safety-boundary.md) | Hardware safety boundary | `ACCEPTED` |

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

## Consequences
What becomes easier, what becomes harder, and what is now prohibited.

## Alternatives considered
Each alternative and the reason it was rejected. Rejections must be on technical or
operational grounds — never because a delivery technology is merely more convenient to
close, replace, or navigate away from.

## Verification status
What about this decision is `[NOT VERIFIED]`, and what evidence would resolve it.

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

---

## Related documents

- [`../../AGENTS.md`](../../AGENTS.md) — authority order and scope-lock rules
- [`../MASTER_PLAN.md`](../MASTER_PLAN.md) — stage gates and implementation status
- [`../CURRENT_STATE.md`](../CURRENT_STATE.md) — verified state and open items
