# ADR-0008 — Technology Stack

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`. **The final UI framework is
  `[OPEN]`**, corrected by the Owner-requested Stage 0.2 documentation review punchlist.
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-requested Stage 0.2 documentation
  review punchlist)
- **Supersedes:** Nothing. This record addresses the stack questions left `[OPEN]` by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11 and listed in
  [`../CURRENT_STATE.md`](../CURRENT_STATE.md) section 8.1.
- **Scope:** Platform support track, backend and local API framework, UI technology and
  framework candidates, service hosting model, dependency injection, configuration framework,
  structured logging strategy, validation approach, unit- and integration-test approach, and
  offline packaging direction.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, sections 8.1, 8.2, and 8.6, as refined by the Owner-requested Stage 0.2
  documentation review punchlist.

---

## Context

The legacy application evidence recorded in [ADR-0006](ADR-0006-ui-delivery-model.md) and
[`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 23 shows what happens when acquisition,
database access, rendering, and graph updates share one architecture without isolation. The
stack chosen here must make that failure mode structurally difficult, not merely unlikely.

The target platform is Windows 11 Pro on a single standalone workstation per Boiler Unit. The
system must run offline, own long-lived device sessions, host a local API, run a Windows
Service independent of any interactive sign-in, and stay maintainable by a small team for
years.

Two environment facts bound this decision and are recorded as evidence:

1. The documentation working environment used for this Stage contains **no .NET SDK**, no
   SQL Server client tooling, and no Windows runtime. Nothing can be built, restored, or
   executed here. Every stack statement below is an architecture decision, not a build result.
2. The offline constraint applies to dependency acquisition as well as to runtime. A
   technology whose normal build or install path requires reaching a public package registry
   or a vendor download service must have an explicit offline substitute — a local package
   folder or feed, or a pinned offline installer.

The governing instruction for this Stage is explicit: do not invent release availability or
support dates, and keep a decision `OPEN` when current evidence cannot support it.

## Decision

1. **Platform and language: .NET with C# on Windows 11 Pro.**
   - **Support track: Long-Term Support (LTS).** The implementation must target an LTS release
     of the .NET platform that is supported at the time the implementation Stage Gate is
     approved.
   - **Exact major version: `[OPEN]`.** No version is pinned in this Stage, because the
     working environment contains no .NET SDK and support-window facts must be verified from
     an authoritative source rather than assumed. The implementation Stage Gate must pin the
     exact version and record the support reference that justifies it.
2. **Backend and Local API framework: ASP.NET Core** hosted in the runtime service, exposing
   the loopback-only Local Application API defined in
   [ADR-0007](ADR-0007-runtime-process-model.md). Minimal API style is the baseline: the API
   is a small, explicit, contract-first surface, not a general web application.
3. **UI delivery: a local web UI rendered inside the application-owned kiosk shell window**,
   as locked in [ADR-0006](ADR-0006-ui-delivery-model.md) items 1 to 4. The **final UI
   framework is `[OPEN]`**. The candidates, compared inside the same architecture, are
   **Candidate A — React + TypeScript + Vite** and **Candidate B — Blazor Hybrid**. The shell
   host framework is also `[OPEN]`. The framework decision is deferred to the proposed
   Stage 0.2.1 spike; no UI code may be created before that decision
   ([`../MASTER_PLAN.md`](../MASTER_PLAN.md) section 3.2).
4. **Windows Service hosting model:** the generic host model with the platform's Windows
   Service integration, one service instance, automatic start, service recovery configured at
   installation, and explicit startup-state gating before any command is issued
   ([ADR-0007](ADR-0007-runtime-process-model.md) item 8).
5. **Dependency injection:** the platform's built-in dependency-injection container. A
   third-party container is not selected: the composition needs (constructor injection,
   singleton/scoped/transient lifetimes, factory delegates) are covered by the built-in
   container, and one fewer dependency is one fewer offline-restore and version-risk surface.
6. **Configuration framework:** the platform's layered configuration providers, with the layer
   order and separation defined in [ADR-0011](ADR-0011-configuration-and-secrets.md).
   Configuration binding is typed; no configuration value is read as an untracked string
   from arbitrary code.
7. **Structured logging strategy:**
   - One logging abstraction used by every component; no direct console writing, and no
     component-specific log file formats.
   - Structured, machine-parsable records (event identity plus structured properties), not
     free text only, so that diagnostics can be searched and correlated.
   - Local file sinks only, with rotation and retention under a local application data path
     outside the Git working tree. No log shipping, no remote sink, no Internet dependency.
   - The logging abstraction and the runtime keep **process telemetry** separate from
     **Historian, Alarm, Event, and Audit records**. Logs are diagnostics; they are not audit
     evidence and must never be presented as such.
   - **Specific logging provider(s): `[OPEN]`.** Selection must include a licence and
     offline-availability review, and must be able to write local rolling files. No provider
     is installed in this Stage.
8. **Validation approach:**
   - Validation is a **domain-owned, explicit pipeline** that produces machine-readable
     results; it is not attached to UI controls and is not performed only in the presentation
     layer.
   - Configuration validation is a distinct, testable step that **blocks publication**
     (TMP-006, [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) section 7). Invalid configuration
     never reaches the runtime, and the runtime never silently corrects it (TMP-007).
   - Control requests are validated by the runtime service, never by the UI.
   - **Validation library selection: `[OPEN]`** and optional. A library may be adopted only
     after licence and offline-availability review; the baseline requirement is the pipeline
     and its results, not a specific library.
9. **Data access and migrations:** recorded in
   [ADR-0009](ADR-0009-database-access-and-migrations.md).
10. **Unit-test framework:** a .NET unit-test framework with deterministic execution order
    control and no external service dependency. **Baseline proposal: xUnit-style framework
    (`[PROPOSED]`); final selection and pinning require a licence and offline-availability
    check at the implementation Stage Gate.** NUnit and MSTest are accepted alternatives with
    the same constraints.
11. **Integration-test approach:**
    - Adapter contract tests that run against simulator adapters and in-process loopback test
      adapters through the same application-facing contracts.
    - Simulator-backed integration tests for transport behaviour, timeouts, reconnection, and
      sequencing, as already planned in [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) section
      3.3.
    - Persistence tests against a **test database instance only**, never a production
      database. Whether the test database is a separate instance or a separate database on a
      development machine is `[OPEN]`.
    - No integration test may require a physical device, and no test may be pointed at
      production.
12. **UI-test approach:** UI automation against the kiosk shell for the production path, plus
    component-level UI checks against a simulator-backed runtime through the Local Application
    API. Exact tooling is `[OPEN]`. Interface testing does not verify control behaviour and
    must not be reported as if it did.
13. **Offline packaging direction:**
    - Application builds are produced offline from a local package source (a local folder feed
      or equivalent) and pinned versions; no build step may require reaching a public package
      registry.
    - **Both UI framework candidates can be built and deployed offline.** If Candidate A is
      selected, a prepared local npm mirror or vendored package cache, a committed dependency
      manifest and lock file, and a pinned Node.js toolchain become part of the offline build
      definition. React does **not** require Internet access to build or to run.
    - Candidate A adds a **second package and build ecosystem** (Node.js/npm in addition to
      NuGet). This increases offline dependency-management, mirroring, and supply-chain audit
      effort. It does not make offline development or deployment impossible, and it is not a
      reason to reject the candidate.
    - Candidate B keeps a single build ecosystem. That reduction is a real advantage, but it
      does not by itself prove UI performance, graph quality, camera integration, or
      long-running kiosk stability.
    - Application packages are produced as offline installable media. The package format
      (MSI, MSIX, or custom) is `[OPEN]` and belongs to the deployment Stage Gate.
    - Prerequisites that are vendor media (SQL Server Standard, and the web view runtime if it
      is not guaranteed on the image) are **detected, not downloaded**. The installer must
      fail closed with a clear remedy if a prerequisite is missing; it must never attempt an
      Internet download.
    - No installer, package definition, or deployment script is created in this Stage.

### UI framework comparison position

The full dimension-by-dimension comparison is in
[ADR-0006](ADR-0006-ui-delivery-model.md). The stack-level position is:

| Item | Position |
| --- | --- |
| Locked by this Stage | Local web UI rendered in an application-owned kiosk shell window; loopback ASP.NET Core Local Application API; separate .NET Equipment Runtime Windows Service |
| Comparison candidates | Candidate A — React + TypeScript + Vite; Candidate B — Blazor Hybrid. Both hosted in the same application-owned shell with the same API contract, the same synthetic data, and the same acceptance measures |
| Current evidence-based preference | Candidate A, based on the Owner's legacy operating experience with React/Vite and push updates, dense-visualisation fit, charting and camera ecosystem, and UI test tooling. **A preference is not an acceptance** |
| Explicitly not a basis for selection | Internet access assumptions; ease of closing a window; C# language unity alone; ecosystem size alone |
| Selection route | Proposed Stage 0.2.1 — UI and Runtime Technology Spike, `PROPOSED` and `[NOT AUTHORIZED]` |
| Effect on the rest of the stack | None: .NET, ASP.NET Core, the service host, dependency injection, configuration, logging, validation, and the adapter boundaries are unchanged by the framework decision |

## Alternatives considered

| Decision | Alternative | Reason not selected |
| --- | --- | --- |
| **UI framework** | **Select Blazor Hybrid now because it uses C# and one toolchain** | Language unity and a smaller offline ecosystem are genuine advantages, but they do not demonstrate the one-second 104-cell workload, trend quality, camera integration, or long-running kiosk stability. Those require measurement |
| **UI framework** | **Exclude React because it is assumed to need Internet access** | Incorrect: React can be built and deployed offline from local assets and a local package mirror. Excluding a candidate on a false premise would be a reasoning error |
| Platform | Native Windows C++ or C# with a non-.NET runtime | Higher development and maintenance cost for the same offline Windows deployment, with weaker tooling for a small team; no requirement in the baseline forces a non-.NET platform |
| Platform | Cross-platform runtime with an abstraction layer | The deployment target is one Windows workstation per Boiler Unit; abstracting the platform adds cost with no deployment benefit |
| Support track | Rolling Current (standard-term) release track | Shorter support windows do not suit an offline, year-scale installation where the maintenance visit is a cost event. LTS reduces the number of version transitions the site must absorb |
| Support track | Pinning a specific version now | Cannot be justified from evidence available in this environment; would risk recording an unverifiable support claim |
| API framework | Full MVC-style web framework | The API is a small local contract surface; MVC structure adds indirection without benefit |
| API framework | gRPC or a custom binary protocol over a local transport | Strong typing but heavier tooling and harder ad-hoc diagnosis; HTTP keeps standard debugging and testing tools available offline |
| API framework | Windows Communication Foundation (WCF) | Legacy hosting and tooling path; no advantage over a modern HTTP contract on Windows 11 |
| UI | Native desktop UI only | Recorded as the fallback in [ADR-0006](ADR-0006-ui-delivery-model.md) |
| DI container | Third-party DI container | Not needed; adds a dependency to acquire and pin offline |
| Logging | Free-text flat log files | Not searchable or correlatable at the volume this system will produce during a fault |
| Logging | Cloud or remote log sink | Prohibited by the offline, standalone requirement |
| Logging | Treating application logs as audit records | Audit records have defined fields, retention, and integrity expectations (see [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md)); diagnostics logs do not meet them |
| Validation | Validation inside UI controls only | A UI-only check cannot protect the runtime, and the runtime must block publication itself |
| Tests | Mock-everything unit tests without simulator-backed integration coverage | Would not exercise the transport behaviour, reconnection rules, or command-state lifecycle that carry the project's real risk |
| Packaging | Installer that downloads prerequisites at install time | Requires Internet access, forbidden by IDN-009 and the offline deployment requirement |

## Consequences

- The stack is deliberately narrow on the server side: one platform, one language, one service
  host. This reduces the offline-restore and version-drift surface that a small team must
  support on a site with no Internet access.
- The UI framework remains undecided, so the stack now has **two possible shapes**: a .NET-only
  shape (Candidate B) or a .NET backend plus a TypeScript frontend shape (Candidate A). The
  architecture, the API contract, the presentation model, and the deployment boundary are
  identical in both, so the decision can be deferred without blocking design work.
- If Candidate A is selected, this repository inherits a second package ecosystem with its own
  pinning, mirroring, licence-inventory, and vulnerability-review obligations. That is a
  deliberate, accepted cost recorded in [ADR-0013](ADR-0013-offline-deployment.md), not an
  accident.
- Because the exact .NET version and the logging and test-framework selections stay `[OPEN]`,
  the implementation Stage Gate inherits a small, explicit list of pinning actions with
  licence and offline-availability checks. This is deliberate: inventing them now would
  create unverifiable documentation.
- Choosing an LTS track means the site absorbs fewer framework transitions, at the cost of
  occasionally lagging newest language and library features. Determinism and maintainability
  outrank feature currency (PRI-001).
- Keeping logging separate from audit records means audit completeness cannot be claimed from
  log files, and vice versa.
- Local package feeds become a build-infrastructure responsibility; without them the offline
  build cannot be reproduced.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Selected .NET version reaches end of support during the deployment's life | Security and servicing gap on an offline site | Pin an LTS release at the implementation gate; record a supported-life plan and an offline servicing path | `[OPEN]` |
| Third-party library licences unacceptable for a public repository or for the site | Rework, or an unlicensed deployment | Licence review is a mandatory follow-up item for every third-party selection (logging, validation, test framework, device libraries) | `[OPEN]` |
| Offline package feed diverges from the pinned versions | Non-reproducible builds | Pinned versions plus a maintained local feed; build definition owned by a later gate | `[PROPOSED]` |
| Validation implemented in the UI and skipped by the runtime | Invalid configuration or unauthorised command reaches equipment | Validation pipeline is domain-owned, and publication and command validation happen in the runtime service | `[PROPOSED]` |
| Logging volume during a fault saturates local disk | Loss of diagnostics during the incident that matters | Rotation, retention, and volume caps; diagnostics retention is already a separate category in the approved retention model | `[PROPOSED]` |
| A second package ecosystem drifts, or cannot be restored offline | Non-reproducible offline builds and unreviewed dependency risk | If Candidate A is selected: pinned versions, committed manifest and lock file, a prepared local mirror or vendored cache, and a licence and vulnerability inventory | `[OPEN]` |
| UI framework chosen informally after this Stage without the spike | The legacy UI performance failure returns, and the choice is not evidenced | Framework remains `[OPEN]`; the proposed Stage 0.2.1 spike is the only authorised selection route; no UI code before a decision | `[PROPOSED]` |

## Verification status

- `[NOT VERIFIED]`: every technology statement here. Nothing has been built, restored,
  executed, measured, or installed. The working environment contains no .NET SDK.
- `[NOT VERIFIED]`: exact .NET version, its support window, and Windows 11 Pro compatibility
  for the pinned version.
- `[NOT VERIFIED]`: offline restorability of every proposed dependency, and the licence
  position of every library — including the npm dependency tree if Candidate A is selected.
- `[NOT VERIFIED]`: the performance suitability of either UI framework candidate. No
  measurement exists for the 104-cell one-second workload, trend rendering, camera
  integration, or long-running memory stability.
- `[OPEN]`: **final UI framework (Candidate A or Candidate B)**; shell host framework; .NET
  version pin; logging provider; validation library (optional); unit-test framework;
  integration-test tooling; UI automation tooling; push transport; chart library; package
  format.
- No standard, certification, or compliance claim is made or implied by this stack.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| **Final UI framework selection (Candidate A versus Candidate B)** | **Proposed Stage 0.2.1 — UI and Runtime Technology Spike** (PROPOSED, NOT AUTHORIZED) |
| .NET LTS version pin with a cited support reference | Implementation Stage Gate (before the first project file is created) |
| Logging provider selection and licence review | Implementation Stage Gate |
| Unit-test framework selection and pinning | Implementation Stage Gate (with the first test project) |
| Integration and UI automation tooling | Test Stage Gate, and the Stage 0.2.1 spike for UI tooling ergonomics |
| Offline package format and local feed definition, including a second ecosystem if Candidate A is selected | Deployment Stage Gate |
| Dependency licence review for every third-party component | Implementation Stage Gate, repeated whenever a dependency is added |

## Relationship to protected decisions

- **Preserved, not modified:** Windows 11 Pro target (IDN-003), one installation per Boiler
  Unit (IDN-004), offline standalone operation (IDN-009), kiosk operating mode and responsive
  pages (IDN-005, IDN-006), local application users (IDN-008), the priority order in PRI-001,
  and the prohibition on describing anything as safety-rated or certified (IDN-010, IDN-011).
- **Implements approved mandatory boundaries:** the UI holds no device session, device
  adapters contain no UI logic, and commands are validated in one place.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Production Device access
  `[NOT AUTHORIZED]`; WAGO fail-safe `[NOT VERIFIED]`; Stage 0.3 remains unauthorised.
- The stack selection does not modify, weaken, or reinterpret any protected decision listed in
  the approved Stage 0.2 Scope Gate.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — decision index and required decision output;
  legacy evidence (section 23), UI framework candidates (section 24), legacy UI workload
  (section 25), sensor presentation model (section 26), quality pipeline (section 27),
  live-state delivery (section 28), Modbus acquisition (section 29), configuration snapshot
  (section 30), Historian decoupling (section 31), trend direction (section 32), proposed
  spike (section 33)
- [`../TEST_STRATEGY.md`](../TEST_STRATEGY.md) — planned verification levels
- [`../MASTER_PLAN.md`](../MASTER_PLAN.md) — proposed Stage 0.2.1
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — ARC, UIW, SPC, DQS, LSD, MDA, CPS, and TST
  requirement groups
- [`ADR-0006-ui-delivery-model.md`](ADR-0006-ui-delivery-model.md)
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0009-database-access-and-migrations.md`](ADR-0009-database-access-and-migrations.md)
- [`ADR-0011-configuration-and-secrets.md`](ADR-0011-configuration-and-secrets.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
