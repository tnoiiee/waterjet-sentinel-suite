# WaterJet Sentinel Suite (WJSS)

**Automated Boiler Wall Water-Jet Cleaning Monitoring and Supervisory Control System**

Repository: `waterjet-sentinel-suite`

---

## Status

| Item | Value |
| --- | --- |
| Current stage | **Stage 0.3A-1 — Product Foundation Source Checkpoint** (Owner Option-C amended gate; Owner-local .NET validation **PENDING**) |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** |
| Stage 0.2 architecture checkpoint | **OWNER ACCEPTED / MERGED** — through PR #2 (source checkpoint `5bcf1b33f924ab30590a55736676200115874fa1`, merge commit `e779f8ad2c856e367fd65985007a3da411bd0e73`) |
| Stage 0.2 ADR-0006 to ADR-0013 | **ACCEPTED** as architecture direction — accepted does **not** mean implemented; items marked `[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` inside them keep those markers |
| Stage 0.2.1A Scope Gate / Coding Start | **APPROVED** / **APPROVED** |
| Stage 0.2.1A implementation | **COMPLETE FOR DEVELOPMENT CHECKPOINT** — synthetic feasibility spike in [`spikes/ui-runtime-react/`](spikes/ui-runtime-react/README.md); Owner-local final Edge gate **PASS** and Owner manual review **PASS** at `114c0761`; **MERGED** — PR #3, merge commit `d8d28201e641e436293136d04ba7ee553802d4e5`; controlled 15- and 60-minute observations **waived as merge blockers** |
| Primary UI Framework | **React selected** (Owner decision, 2026-10-07); Production transport and chart library remain `[OPEN]` |
| Blazor counter-spike | **NOT REQUIRED** unless a future material blocker is identified |
| Main Development Scope Gate | **PENDING** — Stage 0.3A proceeds under the Owner's Option-C amended gate only; Stage 0.3B+ **NOT AUTHORIZED** |
| Stage 0.3A-1 source checkpoint | **AUTHORED IN ARENA — NOT READY FOR MERGE.** .NET build **NOT RUN IN ARENA** (SDK/NuGet blocked in the sandbox); Owner-local validation per [`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) is the mandatory pre-merge gate. Stage 0.3A-2 **NOT AUTHORIZED** |
| Production devices | **NOT AUTHORIZED** |
| Repository contents | Documentation, repository governance, one removable synthetic feasibility spike, and — from Stage 0.3A-1 — the unvalidated product foundation skeleton |
| Application code | **Authored, not validated.** Stage 0.3A-1 supplies the product foundation as source only (`WaterJetSentinelSuite.sln`, `packages/`, `apps/`, `adapters/`, `tests/`, `config/examples/`, `tools/boundary-scan/`); the spike remains synthetic feasibility code, not Product code. **No .NET build, restore, test, or Runtime execution success is claimed anywhere**, and committed fixtures are `PROVISIONAL STRUCTURAL FIXTURE` until the Owner-local generator run replaces them |
| Application version | **NOT ESTABLISHED.** No runtime release exists. |
| Hardware connection | **Not authorised.** Production device access is prohibited. |
| Production Write | **Not authorised.** |
| Merge authority | **Owner only.** Agents never merge. |

This repository contains documentation, one removable **synthetic** React feasibility spike
(`spikes/ui-runtime-react/`, Stage 0.2.1A) that uses no device, no Production value, and no
Product directory, and — from Stage 0.3A-1 under the Owner's Option-C amended gate — the
**product foundation source skeleton** (`packages/contracts`, `packages/domain`,
`packages/application`, `packages/time`, `adapters/simulator`, `apps/runtime`, `apps/kiosk`,
`tests/`, `config/examples/`, `tools/boundary-scan/`). That skeleton is **authored, not
validated**: no .NET build has run in the authoring environment and nothing here is a running
system. See
[`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) for the stage status, the verified state,
and the status legend used throughout.

Stage 0.2 recorded the technology and solution architecture — UI delivery model, runtime
process model, technology stack, database access and migrations, device adapter boundary,
configuration and secrets, simulator-first development, and offline deployment — in
[`docs/decisions/`](docs/decisions/README.md). The Owner accepted Stage 0.2 and ADR-0006 to
ADR-0013 as architecture direction. **Accepted does not mean implemented**: items still marked
`[PROPOSED]`, `[OPEN]`, or `[NOT VERIFIED]` keep those markers, and no capability or later
stage is authorised by acceptance. Stage 0.3A-1 authors product *sources* that follow these
directions — that is drafting toward implementation, not implemented or verified code; its
three companion records ADR-0014 to ADR-0016 are `DRAFT`.

**React is selected as the Primary UI Framework** — this is a **settled Owner decision
(2026-10-07)**, taken after the Owner-local final Edge gate and manual review of the Stage
0.2.1A spike passed; it is no longer a preference awaiting acceptance. The Blazor
counter-spike is **not required** unless a future material blocker is identified.
*Historical context (superseded):* two candidates were recorded and compared on equal terms:
Candidate A — React + TypeScript + Vite — and Candidate B — Blazor Hybrid — each hosted
in the same application-owned kiosk shell behind the same loopback API. At that time, the
evidence-based preference for Candidate A was explicitly **not acceptance** and Candidate B
was explicitly **not rejected**; that position ended with the 2026-10-07 Owner selection.
React can be built and deployed offline; it introduces a second
package and build ecosystem, which increases offline dependency-management and supply-chain
effort without making offline development or deployment impossible. Choosing the framework
required measured evidence, and it was obtained: the Stage 0.2.1A React feasibility spike
was approved, implemented, and **closed out** — the feasibility checkpoint is **complete**
(Owner-local final Edge gate and manual review **PASS**;
[`docs/spikes/stage-0.2.1a-plan.md`](docs/spikes/stage-0.2.1a-plan.md) is retained as
history), and React is **selected** as the Primary UI Framework (see above). See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) sections 23 to 33 and
[`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) section 3.2.

## What the product is

A supervisory monitoring and control application for automated boiler-wall water-jet
cleaning. The planned deployment model is:

- One Windows 11 Pro workstation per Boiler Unit.
- One application installation controls one Boiler Unit.
- The main operating mode is a full-screen Control Room Kiosk with controlled navigation.
- Applicable pages support workstation-responsive layouts.
- The default application language is English; Thai may be used as supplementary
  contextual explanation where necessary.
- The application uses local application users.
- The initial system is standalone and does not depend on Internet access.

## What the product is not

WJSS is **not** a Safety Instrumented System. It does not replace emergency stop circuits,
hardwired protection, motor protection, mechanical limits, or controller-side safe-stop
behaviour. The application must never command, override, bypass, suppress, or replace an
external protection function. It is **not** certified to IEC, ISA, ISO, or any other
standard. Industrial standards are used only as guidance and design inspiration. See
[`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md).

## Planned technical scope (not yet built)

Modbus TCP monitoring and supervisory commands through WAGO remote I/O; temperature
monitoring; DCS hardwired signal monitoring through WAGO I/O; valve and motor supervisory
control; Galil motion control; cleaning criteria evaluation; TempQueue, TimeQueue, and
GlobalQueue arbitration; Historian; trend visualisation; alarm management; event logging;
audit logging; diagnostics; configurable roles and permissions; engineering configuration;
and reporting and export.

## Physical baseline (approved design intent)

| Item | Approved baseline |
| --- | --- |
| Boiler walls | Left, Rear, Right, Front |
| Sensor locations | Left 24, Rear 29, Right 24, Front 29 — **106 total** (Owner domain correction during Stage 0.2.1A; supersedes the earlier 104-location baseline) |
| Logical Sensor matrix | 18 logical columns × 6 logical rows = 108 positions: **106 Sensor locations + 2 Cannon equipment slots** (logical I7 Rear Cannon, logical I16 Front Cannon). Wall columns: Left 1–4, Rear 5–9, Right 10–13, Front 14–18. See [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) |
| Thermocouple channels per sensor | 2 — front channel (`TC_F`) and rear channel (`TC_R`) |
| Thermocouple channels total | **212** (supersedes the earlier 208) |
| Water Jet assemblies | 8, each with horizontal X and vertical Y axes |
| Isolation Valves | **8 — exactly one dedicated per Water Jet.** Never shared |
| Galil controllers | 4 (one controller per two Water Jets), model DMC-B140-M |
| Controller axes | A, B, C, D per controller |
| Modbus interface | WAGO 750-362 Modbus TCP Coupler, Modbus TCP only, zero-based internal addressing |
| Cleaning Jobs | **Strictly sequential.** At most one ACTIVE at any time |

Actual production tag lists, register maps, addresses, and motion values are confidential
local deployment information and are **not** in this repository.

## Two invariants worth stating up front

1. **At most one Cleaning Job may be ACTIVE within one installation at any time.**
   Different Water Jets, valves, walls, or controllers do not grant authority for
   concurrent jobs. Parallel Water Jet cleaning is prohibited.
2. **The Operations UI close guard is an operational usability control, not a safety
   protection.** It blocks the normal close action while a Cleaning Job is active or the
   Main Pump is running. It cannot protect against process termination, Windows shutdown,
   workstation restart, power loss, or hardware failure.

## Document index

### Governance

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Working contract: authority order, scope lock, evidence rules, git rules |
| [`SECURITY.md`](SECURITY.md) | Security policy, secret handling, vulnerability reporting |
| [`CHANGELOG.md`](CHANGELOG.md) | Documentation history |
| [`.gitignore`](.gitignore) | Files excluded from the public repository |

### Core documentation

| Document | Purpose |
| --- | --- |
| [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) | Stage status, verified state, legend, open items |
| [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) | Staged delivery plan and stage gates |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Forward view of planned stages and milestones |
| [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md) | Consolidated requirements with identifiers and status |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Conceptual architecture, communication health, UI guard |
| [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) | Entities, terminology, outcomes, and dispositions |

### Control and safety

| Document | Purpose |
| --- | --- |
| [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) | Hardware safety boundary and prohibited control writes |
| [`docs/CONTROL_AUTHORITY.md`](docs/CONTROL_AUTHORITY.md) | Authority matrix, sequencing authority, DCS override |
| [`docs/CLEANING_SEQUENCE.md`](docs/CLEANING_SEQUENCE.md) | Cleaning Job, sequencing invariants, failure handling |
| [`docs/ALARM_MODEL.md`](docs/ALARM_MODEL.md) | Condition, acknowledgement, shelving, cleared-state ack |

### Queues and data

| Document | Purpose |
| --- | --- |
| [`docs/QUEUE_MODEL.md`](docs/QUEUE_MODEL.md) | Eight source queues, GlobalQueue, ownership, refill, stop/rebuild |
| [`docs/HISTORIAN_RETENTION.md`](docs/HISTORIAN_RETENTION.md) | Storage targets, intervals, retention, cleanup |
| [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md) | Roles, permissions, sessions, break-glass, override permission |

### Delivery assurance

| Document | Purpose |
| --- | --- |
| [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) | Planned verification, including required review-confirmed cases |
| [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md) | What may and may not be committed |

### Decision records

| Document | Purpose |
| --- | --- |
| [`docs/decisions/README.md`](docs/decisions/README.md) | Architecture Decision Record index and format |
| [`docs/decisions/ADR-0001-product-identity.md`](docs/decisions/ADR-0001-product-identity.md) | Product identity and naming |
| [`docs/decisions/ADR-0002-deployment-architecture.md`](docs/decisions/ADR-0002-deployment-architecture.md) | Per-Boiler-Unit workstation deployment |
| [`docs/decisions/ADR-0003-queue-arbitration.md`](docs/decisions/ADR-0003-queue-arbitration.md) | Deterministic queue arbitration and sequencing |
| [`docs/decisions/ADR-0004-historian-strategy.md`](docs/decisions/ADR-0004-historian-strategy.md) | Historian storage and retention strategy |
| [`docs/decisions/ADR-0005-hardware-safety-boundary.md`](docs/decisions/ADR-0005-hardware-safety-boundary.md) | Prohibition of production control writes pending bench verification |

#### Stage 0.2 decision records — `ACCEPTED` (architecture direction; not implemented)

| Document | Purpose |
| --- | --- |
| [`docs/decisions/ADR-0006-ui-delivery-model.md`](docs/decisions/ADR-0006-ui-delivery-model.md) | Kiosk shell and local web UI delivery model; React and Blazor candidates compared — final framework `[OPEN]` |
| [`docs/decisions/ADR-0007-runtime-process-model.md`](docs/decisions/ADR-0007-runtime-process-model.md) | Process model, device-session ownership, Local Application API boundary |
| [`docs/decisions/ADR-0008-technology-stack.md`](docs/decisions/ADR-0008-technology-stack.md) | .NET support track, API framework, service hosting, logging, validation, tests, offline packaging; UI framework `[OPEN]` |
| [`docs/decisions/ADR-0009-database-access-and-migrations.md`](docs/decisions/ADR-0009-database-access-and-migrations.md) | Transactional relational access, batch Historian path, migration execution policy, database unavailability; ORM and mapper `[OPEN]` |
| [`docs/decisions/ADR-0010-device-adapter-boundary.md`](docs/decisions/ADR-0010-device-adapter-boundary.md) | Adapter ports, vendor isolation, command lifecycle, disabled-by-default physical adapters |
| [`docs/decisions/ADR-0011-configuration-and-secrets.md`](docs/decisions/ADR-0011-configuration-and-secrets.md) | Configuration layers, Draft versus Published, secrets, publication and rollback |
| [`docs/decisions/ADR-0012-simulator-first-development.md`](docs/decisions/ADR-0012-simulator-first-development.md) | Simulator-first development, device profiles, failure injection, contract parity |
| [`docs/decisions/ADR-0013-offline-deployment.md`](docs/decisions/ADR-0013-offline-deployment.md) | Offline installation, startup, backup and restore, upgrade and rollback, local-only communication |

#### Stage 0.2.1A feasibility spike — synthetic, removable, not Product code

| Document | Purpose |
| --- | --- |
| [`docs/spikes/stage-0.2.1a-plan.md`](docs/spikes/stage-0.2.1a-plan.md) | Approved spike scope, architecture, scenarios, measurement plan |
| [`docs/spikes/stage-0.2.1a-results.md`](docs/spikes/stage-0.2.1a-results.md) | Arena validation and measurement evidence; Owner-local final Edge gate and manual review PASS (Owner-reported, §0G) |
| [`spikes/ui-runtime-react/README.md`](spikes/ui-runtime-react/README.md) | Spike layout, run instructions, NOT VERIFIED list |

## Status legend

Every substantive statement in this documentation is marked with one of the following:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Behaviour or fact approved by the Owner in an approved Stage Gate. |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review. Binding. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. Must not be treated as approved behaviour. |
| `[NOT VERIFIED]` | Value or behaviour not yet confirmed by bench test, field test, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Activity that is currently prohibited. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed. |

## Contributing

Read [`AGENTS.md`](AGENTS.md) first. It defines the authority order, scope-lock rules,
evidence requirements, public-repository restrictions, device-access prohibitions,
prohibited Git operations, and the Owner-only merge rule.

## License and distribution

No licence has been granted in this repository. Publication of this documentation does not
grant rights to use, modify, or distribute the described system. Contact the Owner before
reusing any content.
