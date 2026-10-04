# WaterJet Sentinel Suite (WJSS)

**Automated Boiler Wall Water-Jet Cleaning Monitoring and Supervisory Control System**

Repository: `waterjet-sentinel-suite`

---

## Status

| Item | Value |
| --- | --- |
| Current stage | **Stage 0.2 — Technology and Solution Architecture Decision** |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** |
| Stage 0.2 implementation | **SUBMITTED FOR OWNER REVIEW** |
| Stage 0.2 documentation review | **PENDING** |
| Owner manual review | **PENDING** |
| Merge | **NOT MERGED** |
| Stage 0.3 | **NOT AUTHORIZED** |
| Repository contents | Documentation and repository governance only |
| Application code | **None.** No source, no solution, no schema, no runtime. |
| Application version | **NOT ESTABLISHED.** No runtime release exists. |
| Hardware connection | **Not authorised.** Production device access is prohibited. |
| Production Write | **Not authorised.** |
| Merge authority | **Owner only.** Agents never merge. |

This repository currently contains **documentation only**. Everything described here is a
design baseline candidate, not a running system. See
[`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) for the stage status, the verified state,
and the status legend used throughout.

Stage 0.2 recorded a `[PROPOSED]` technology and solution architecture — UI delivery model,
runtime process model, technology stack, database access and migrations, device adapter
boundary, configuration and secrets, simulator-first development, and offline deployment — as
ADR candidates in [`docs/decisions/`](docs/decisions/README.md). `[PROPOSED]` means drafted
and submitted, **not approved**: the Owner has not yet accepted those records, nothing in them
is implemented, and no capability or later stage is authorised by them.

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
| Sensor locations | Left 24, Rear 28, Right 24, Front 28 — **104 total** |
| Thermocouple channels per sensor | 2 — front channel (`TC_F`) and rear channel (`TC_R`) |
| Thermocouple channels total | **208** |
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

#### Stage 0.2 candidate decision records — `PROPOSED`, Owner acceptance pending

| Document | Purpose |
| --- | --- |
| [`docs/decisions/ADR-0006-ui-delivery-model.md`](docs/decisions/ADR-0006-ui-delivery-model.md) | Kiosk shell and local web UI delivery model |
| [`docs/decisions/ADR-0007-runtime-process-model.md`](docs/decisions/ADR-0007-runtime-process-model.md) | Process model, device-session ownership, Local Application API boundary |
| [`docs/decisions/ADR-0008-technology-stack.md`](docs/decisions/ADR-0008-technology-stack.md) | .NET support track, API framework, UI technology, service hosting, logging, validation, tests, offline packaging |
| [`docs/decisions/ADR-0009-database-access-and-migrations.md`](docs/decisions/ADR-0009-database-access-and-migrations.md) | Data access, transactions, Historian write path, migration execution policy, database unavailability |
| [`docs/decisions/ADR-0010-device-adapter-boundary.md`](docs/decisions/ADR-0010-device-adapter-boundary.md) | Adapter ports, vendor isolation, command lifecycle, disabled-by-default physical adapters |
| [`docs/decisions/ADR-0011-configuration-and-secrets.md`](docs/decisions/ADR-0011-configuration-and-secrets.md) | Configuration layers, Draft versus Published, secrets, publication and rollback |
| [`docs/decisions/ADR-0012-simulator-first-development.md`](docs/decisions/ADR-0012-simulator-first-development.md) | Simulator-first development, device profiles, failure injection, contract parity |
| [`docs/decisions/ADR-0013-offline-deployment.md`](docs/decisions/ADR-0013-offline-deployment.md) | Offline installation, startup, backup and restore, upgrade and rollback, local-only communication |

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
