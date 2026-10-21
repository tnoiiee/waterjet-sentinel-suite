# Manual Motion Legacy Continuity Evidence — PR #34

**Document class:** EVIDENCE AND CONTINUITY RECORD. This note connects the
already committed, redacted Legacy baseline to the WSB1 Manual Motion
Commissioning software slice proposed in PR #34. It is not a second Legacy
extraction, does not copy Legacy Source, does not promote a Legacy value,
and does not authorize hardware use.

## 1. Evidence of record

The source-side evidence is
[`LEGACY_MOTION_COMMISSIONING_BASELINE.md`](LEGACY_MOTION_COMMISSIONING_BASELINE.md).
Its provenance record identifies one read-only external pack named
`repomix-output.md`, 662,922 bytes and 10,834 lines, containing seven files:
`FormCannonController.cs`, `FormCannonController.Designer.cs`,
`FormCannonController.resx`, `FormDataCenter.cs`,
`FormDataCenter.Designer.cs`, `FormDataCenter.resx`, and
`IDataProvider.cs`. That document also records that the pack was not
committed and that all endpoint, coordinate, standby and numeric
commissioning values were redacted.

The vendor-side interface evidence remains
[`GCLIB_2_6_0_API_USAGE_BASELINE.md`](GCLIB_2_6_0_API_USAGE_BASELINE.md).
PR #34 uses the repository's current Gate A-approved native library
boundary rather than the Legacy application's COM-style shared command
path. Neither evidence document is rewritten by this PR.

## 2. Retained behavioural findings

Only the following already approved, redacted findings are carried into the
new typed design:

| Legacy behaviour recorded in the baseline | PR #34 disposition |
| --- | --- |
| Brake release precedes Jog, Home and Move. | Retained as an Application ordering rule: a typed brake-output change and configured settle precede Begin. No physical brake state is inferred from dispatch. |
| The brake allocation is A → output 1 and B → output 2. | Retained as Owner-confirmed topology data and enforced by the strict profile and closed adapter plan. Electrical polarity is not retained as fact. |
| Jog loads a signed speed, begins motion, and ends when the Operator releases the control. | Retained as hold-to-run only, with heartbeat, capture-loss, focus-loss and maximum-hold protections. |
| Stop is per axis. | Retained through the existing typed A/B Stop session. The new manual-motion plan deliberately has no Stop template or second Stop path. |
| Home loads home velocity, arms the home search and begins; on the home input it stops and defines position zero. | Retained as HV/HM/BG sequencing, followed by the typed Stop, DP zero, and TP/RP verification. A timeout or unverified zero is a failure, not a Home. |
| Position definition uses DP zero and dual definition uses DE zero. | Retained as separate typed operations. DP can be verified from later TP/RP reads; DE remains `DISPATCHED_EFFECT_UNVERIFIED`. Manual DP/DE clears the session Home claim. |
| Absolute Move configures acceleration, speed and deceleration, loads an absolute target and begins. | Retained as AC/SP/DC/PA/BG only. A verified session Home, fresh observations, Owner-local native bounds and TP/RP tolerance verification are required. |
| Feedback reads status, two position values and outputs. | Retained through the approved typed B2 observation intents, restricted to A/B status, TP, RP and outputs 1/2. Raw observations stay separate from interpretation. |

**Phase 1A addendum (2026-10-22, software only).** The dispositions above
were written for the PR #34 A/B slice and are unchanged as a record. Phase
1A widens the same closed typed design to Axis C (Horizontal, brake output
3) and Axis D (Vertical, brake output 4): the per-axis Stop, the brake →
jog → Begin ordering and the B2 status/TP/RP/output reads now cover A–D and
outputs 1–4 through the same single plans, with no second Stop path and no
new command shape. This addendum records no new Legacy finding and no
hardware activity: B/C/D wiring and route continuity continue to rest on
the Legacy Actual Run evidence summarized in this document, and the only
completed WJSS hardware trial remains Axis A (FINAL PASS, Owner record).

## 3. Corrections and deliberate non-ports

The WJSS slice does not reproduce the Legacy implementation structure. In
particular:

- no caller-built command string crosses the Application, UI or adapter
  boundary; the sole new template surface is one internal closed plan;
- one long-lived owner, one active operation, one controller-wide native
  gate and generation fencing replace concurrent unfenced dispatch;
- a communication fault ends the session as `FAULTED`; there is no retry,
  reconnect, replay, discovery or silent continuation;
- output and position values are never repaired, defaulted or converted to
  engineering units;
- no connect-time output clear or output-bank write exists;
- no automatic cleaning sequence, parallel-axis move, standby return,
  water-cannon switching or persisted Legacy path is ported;
- no Legacy speed, delay, coordinate, ratio, status decode or other numeric
  default becomes a product/profile default; and
- cleanup is deterministic and measured as invalidate → close → module
  release, rather than relying on replacement or process exit.

Excluded command surfaces remain excluded: no drive enable/disable write,
no relative target, no output-bank write, no burn/save operation, no
soft-limit write and no Controller program. Drive readiness is observation
only through a profile-supplied status interpretation.

## 4. Still unresolved

The gaps in section G.1 of the Legacy baseline remain controlling. In
particular, the following are `UNCONFIRMED` or `NOT VERIFIED` for real
hardware until separately evidenced under a hardware Scope Gate:

- installed-firmware response details beyond the already bounded accepted
  read/dispatch classifications;
- status-word bit order, active polarity and every physical meaning;
- brake-output electrical polarity and whether an output read proves the
  commanded or physical output state;
- physical unit and safe native travel bounds for either axis;
- home-switch behaviour outside the observed polling model; and
- timing, repeatability and failure behaviour on the physical Controller.

Accordingly, real Owner-local values stay `NOT_CONFIGURED`, both axes stay
`NOT_COMMISSIONED`, and no simulated or fake-kernel test result upgrades a
hardware statement.

## 5. Owner decisions reflected by PR #34

The approved software scope is one isolated PR: WSB1 only; Axis A Horizontal
and Axis B Vertical; manual brake/Jog/Stop/Home/DP/DE/absolute Move;
deterministic simulation; one hardware-capable adapter owner; an
Owner-local external profile; and no hardware execution by the Agent.
Runtime, API, Kiosk, Operations Preview and Production remain unchanged.
Merging the software, if the Owner later chooses to do so, still would not
authorize a physical commissioning activity; that requires the separate
post-merge Scope Gate described in
[`../MANUAL_MOTION_COMMISSIONING_RUNBOOK.md`](../MANUAL_MOTION_COMMISSIONING_RUNBOOK.md).

**Redaction statement.** This continuity note contains no Legacy Source, no
endpoint, local path, account or connector identifier, serial value,
coordinate, real profile value, consent value, hash or vendor error text.
