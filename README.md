# WaterJet Sentinel Suite (WJSS)

**Automated Boiler Wall Water-Jet Cleaning Monitoring and Supervisory Control System**

Repository: `waterjet-sentinel-suite`

---

## Status

| Item | Value |
| --- | --- |
| Current stage | **Stage 0.1 — Repository Documentation Foundation** |
| Documentation baseline | `0.1.0` (see [`CHANGELOG.md`](CHANGELOG.md)) |
| Repository contents | Documentation and repository governance only |
| Application code | **None.** No source, no solution, no schema, no runtime. |
| Hardware connection | **Not authorised.** Production device access is prohibited. |
| Merge authority | **Owner only.** Agents never merge. |

This repository currently contains **documentation only**. Everything described here is a
design baseline, not a running system. See [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md)
for the verified state and the status legend used throughout.

## What the product is

A supervisory monitoring and control application for automated boiler-wall water-jet
cleaning. The planned deployment model is:

- One Windows 11 Pro workstation per Boiler Unit.
- One application installation controls one Boiler Unit.
- The main operating mode is a full-screen Control Room Kiosk.
- Applicable pages support workstation-responsive layouts.
- The default application language is English; Thai may be used as supplementary
  contextual explanation where necessary.
- The application uses local application users.
- The initial system is standalone and does not depend on Internet access.

## What the product is not

WJSS is **not** a Safety Instrumented System. It does not replace emergency stop circuits,
hardwired protection, motor protection, mechanical limits, or controller-side safe-stop
behaviour. It is **not** certified to IEC, ISA, ISO, or any other standard. Industrial
standards are used only as guidance and design inspiration. See
[`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md).

## Planned technical scope (not yet built)

Modbus TCP monitoring and supervisory commands through WAGO remote I/O; temperature
monitoring; DCS hardwired signal monitoring; valve and motor supervisory control; Galil
motion control; cleaning criteria evaluation; TempQueue, TimeQueue, and GlobalQueue
arbitration; Historian; trend visualisation; alarm management; event logging; audit
logging; diagnostics; configurable roles and permissions; engineering configuration; and
reporting and export.

## Physical baseline (approved design intent)

| Item | Approved baseline |
| --- | --- |
| Boiler walls | Left, Rear, Right, Front |
| Sensor locations | Left 24, Rear 28, Right 24, Front 28 — **104 total** |
| Thermocouple channels per sensor | 2 — front channel (`TC_F`) and rear channel (`TC_R`) |
| Thermocouple channels total | **208** |
| Water Jet assemblies | 8, each with horizontal X and vertical Y axes |
| Galil controllers | 4 (one controller per two Water Jets), model DMC-B140-M |
| Controller axes | A, B, C, D per controller |
| Modbus interface | WAGO 750-362 Modbus TCP Coupler, Modbus TCP only, zero-based internal addressing |

Actual production tag lists, register maps, addresses, and motion values are confidential
local deployment information and are **not** in this repository.

## Document index

### Governance

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Working contract: authority order, scope lock, evidence rules, git rules |
| [`SECURITY.md`](SECURITY.md) | Security policy, secret handling, vulnerability reporting |
| [`CHANGELOG.md`](CHANGELOG.md) | Documentation baseline history |
| [`.gitignore`](.gitignore) | Files excluded from the public repository |

### Core documentation

| Document | Purpose |
| --- | --- |
| [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) | Verified state, status legend, open items |
| [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md) | Staged delivery plan and stage gates |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Forward view of planned stages and milestones |
| [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md) | Consolidated functional and non-functional requirements |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Conceptual architecture and subsystem responsibilities |
| [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) | Entities, terminology, and identifiers |

### Control and safety

| Document | Purpose |
| --- | --- |
| [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) | Hardware safety boundary and prohibited control writes |
| [`docs/CONTROL_AUTHORITY.md`](docs/CONTROL_AUTHORITY.md) | Who and what may command which output, and under what conditions |
| [`docs/CLEANING_SEQUENCE.md`](docs/CLEANING_SEQUENCE.md) | Cleaning Job definition and normal sequence |
| [`docs/ALARM_MODEL.md`](docs/ALARM_MODEL.md) | Alarm condition, acknowledgement, shelving, and blocking rules |

### Queues and data

| Document | Purpose |
| --- | --- |
| [`docs/QUEUE_MODEL.md`](docs/QUEUE_MODEL.md) | TempQueue, TimeQueue, GlobalQueue, deduplication, refill |
| [`docs/HISTORIAN_RETENTION.md`](docs/HISTORIAN_RETENTION.md) | Storage targets, acquisition intervals, retention, cleanup |
| [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md) | Roles, permissions, sessions, break-glass account |

### Delivery assurance

| Document | Purpose |
| --- | --- |
| [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) | Planned verification approach, including the not-yet-run future |
| [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md) | What may and may not be committed |

### Decision records

| Document | Purpose |
| --- | --- |
| [`docs/decisions/README.md`](docs/decisions/README.md) | Architecture Decision Record index and format |
| [`docs/decisions/ADR-0001-product-identity.md`](docs/decisions/ADR-0001-product-identity.md) | Product identity and naming |
| [`docs/decisions/ADR-0002-deployment-architecture.md`](docs/decisions/ADR-0002-deployment-architecture.md) | Per-Boiler-Unit workstation deployment |
| [`docs/decisions/ADR-0003-queue-arbitration.md`](docs/decisions/ADR-0003-queue-arbitration.md) | Deterministic queue arbitration |
| [`docs/decisions/ADR-0004-historian-strategy.md`](docs/decisions/ADR-0004-historian-strategy.md) | Historian storage and retention strategy |
| [`docs/decisions/ADR-0005-hardware-safety-boundary.md`](docs/decisions/ADR-0005-hardware-safety-boundary.md) | Prohibition of production control writes pending bench verification |

## Status legend

Every substantive statement in this documentation is marked with one of the following:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Behaviour or fact approved by the Owner in an approved Stage Gate. |
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
