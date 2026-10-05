# Changelog

All notable changes to this repository are recorded in this file.

The format follows the spirit of *Keep a Changelog*, adapted for a stage-gated project:
entries correspond to Owner-approved Delivery Stages and to review corrections, not to
releases of software. **The repository contains documentation and, from Stage 0.2.1A, one
removable synthetic feasibility spike — no Product code.**

---

## Versioning position

- **No application version exists.** The application version is **NOT ESTABLISHED**. No
  runtime release has been built, packaged, or delivered.
- A previous reference to a `0.1.0` documentation label was a documentation-only working
  label, not a product release. It is retained here only for historical traceability and is
  explicitly **not** a product version.
- The documentation versioning policy is **`[OPEN]`**. No scheme, such as semantic
  versioning, is adopted, and no documentation stage automatically advances a version
  number. A future Owner decision is required to establish one.

---

## [Unreleased]

### Stage 0.2.1A — React UI and Runtime Feasibility Spike (synthetic)

**Scope and Coding Start Gate:** `[APPROVED]`
**Implementation:** IN PROGRESS — PR #3 OPEN
**Owner-local testing (installed Edge, Windows 11):** PASS for checkpoint `dd20a8bd` (superseded
104-location map); re-run for the corrected Sensor map and the fullscreen layout PENDING
**Owner manual UI re-review (1920 × 1080, Edge F11):** PENDING · **60-minute run:** PAUSED
**Merge:** NOT MERGED
**React final selection:** NOT YET APPROVED — UI framework `[OPEN]`
**Blazor counter-spike:** DEFERRED / `[NOT AUTHORIZED]`
**Stage 0.3:** `[NOT AUTHORIZED]`

#### Changed — Fullscreen Operations UI refinement (Owner-approved Design Addendum)

- Presentation-only refinement of the React feasibility Operations page for Windows 11, Edge F11,
  1920 × 1080, zoom 100 %: strict `100dvh` layout with no page-level scrolling; larger Sensor
  cells (≈ 50 × 44 px at the target, capped at 54 × 50 px) with 12.5 px IDs and 15 px values;
  U-shaped map retained with overlapping vertical bands and a compact 204 × 136 px centre
  summary; two-column Sensor Detail inspector; compact Active Job and GlobalQueue (compact source
  reasons); bounded Trend and Camera bottom row; floating, closable Diagnostics drawer. All sizes
  come from one design-token location (`react-ui/src/global.css`).
- Sensor domain, mapping, counts, Cannon positions, functional behaviour, and dependencies are
  unchanged. New tests: 15 Vitest tests and 5 Owner-local Edge layout tests (`e2e/layout.spec.ts`).
  Viewport fit is **NOT VERIFIED** in Arena (no browser); the Owner-local re-run is PENDING.
- Documentation findings closed: **DP-01** (`docs/CURRENT_STATE.md` — the stale "24 versus 28"
  row-alignment open item is resolved by the 18 × 6 canonical matrix, 24 / 29 / 24 / 29, Cannon
  slots I7 / I16) and **DP-02** (`docs/ARCHITECTURE.md` §34 — final UI framework `[OPEN]`, route is
  React-first Stage 0.2.1A feasibility, Blazor counter-spike deferred behind a separate Scope Gate).

#### Changed — Owner domain correction: 106-location Sensor map

- The Owner corrected the protected count from **104 / 208** to **106 Sensor locations / 212
  Thermocouple channels** (Left 24, Rear 29, Right 24, Front 29). It is a domain correction, not
  a runtime failure. Documents changed: `README.md`, `docs/DOMAIN_MODEL.md` (new §2.2.1 logical
  matrix), `docs/REQUIREMENTS.md` (PHY-001, PHY-002, UIW-001, UIW-002), `docs/ARCHITECTURE.md`
  (§24, §25.1, §33.2), `docs/TEST_STRATEGY.md`, `docs/ROADMAP.md`, `docs/MASTER_PLAN.md`,
  `docs/CURRENT_STATE.md`, ADR-0002, ADR-0003, ADR-0006 to ADR-0009, the spike plan / results,
  and the contract docs.
- Spike: new single mapping source `contracts/sensorMap.mjs` (18 × 6 logical matrix, 106
  Sensors, Cannon slots at logical I7 / I16, synthetic scan order, device, channel, and jet
  assignment). It is consumed by the harness, Snapshot `wallMap`, validator, fixtures, scenario
  runner, and tests. The React U-shaped map renders 6-row wall grids from the Snapshot wall map,
  with neutral, non-selectable Cannon slots. Separate seeded random streams make the synthetic
  process workload deterministic per seed. The runtime refuses Sensor commands for Cannon IDs.
- Status headers corrected in the 12 documents previously reported stale in
  `docs/CURRENT_STATE.md` §12.3, plus `docs/ROADMAP.md` and `docs/decisions/README.md`.
- `docs/ARCHITECTURE.md` §33: the dual-candidate Stage 0.2.1 proposal is marked **SUPERSEDED**
  by the React-first Stage 0.2.1A spike. A Blazor counter-spike requires a future Owner Scope
  Gate.

#### Added

- **`spikes/ui-runtime-react/`** — removable synthetic feasibility spike: Node built-ins-only
  synthetic runtime harness (10 synthetic device sessions, serialized per device, bounded
  cross-device concurrency, Snapshot plus Delta over SSE on `127.0.0.1` only, bounded Historian
  slowdown simulator, invariant monitor); React 19 + TypeScript 6 + Vite 8 single Operations
  page with an external presentation store, per-Sensor subscriptions, and a uPlot trend
  (spike-only chart choice); contracts and golden fixtures; 28-scenario runner; measurement
  tools; Vitest (jsdom) tests; Playwright specs for Owner-local runs with installed Microsoft
  Edge; offline restore and Owner-local testing guides; committed environment record,
  summaries, SBOM, licence inventory, and SHA-256 manifest. Raw results are not committed.
- **`docs/spikes/stage-0.2.1a-plan.md`** and **`docs/spikes/stage-0.2.1a-results.md`**.

#### Changed

- Stage status in `README.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`,
  `docs/ROADMAP.md`, `docs/decisions/README.md`, and ADR-0006 to ADR-0013: Stage 0.2 recorded as
  OWNER ACCEPTED / MERGED; ADRs ACCEPTED (not implemented); Stage 0.2.1A approved and in
  progress.

#### Not changed / not verified

- No Product directory (`apps/`, `packages/`, `adapters/`, `config/`), no .NET code, no SQL, no
  device access, no Production value. SSE is a spike transport only; Production transport
  `[OPEN]`. ASP.NET Core integration, Windows Service behaviour, WebView2 kiosk behaviour,
  browser rendering performance, Windows offline restore, and extended stability are
  **NOT VERIFIED**.

### Stage 0.2 — Technology and Solution Architecture Decision

**Stage 0.2 Scope Gate:** `[APPROVED]`
**Stage 0.2 architecture checkpoint:** OWNER ACCEPTED / MERGED — PR #2 (source checkpoint
`5bcf1b33f924ab30590a55736676200115874fa1`, merge commit
`e779f8ad2c856e367fd65985007a3da411bd0e73`)
**ADR-0006 to ADR-0013:** ACCEPTED as architecture direction — not implemented
**Stage 0.2.1:** superseded by the approved, narrower Stage 0.2.1A React feasibility spike
**Stage 0.3:** `[NOT AUTHORIZED]`

*The status lines below this point in the Stage 0.2 entry are preserved as they were written at
submission time.*

Documentation-only stage. Established the technology and solution architecture foundation for
WJSS as a set of decision records and supporting architecture sections. **No application code,
runtime scaffold, solution or project file, package manifest, lock file, database schema, SQL
script, migration, hardware adapter, simulator, test code, CI workflow, installer, deployment
script, configuration file, package dependency, release archive, or runtime directory was
created, and no device or database was contacted.** No dependency was installed.

#### Added

- **`docs/decisions/ADR-0006-ui-delivery-model.md`** — UI delivery model: application-owned
  full-screen kiosk shell, close guard, controlled navigation, static local assets in an
  embedded WebView, local web UI. Two candidates compared on equal terms: Candidate A —
  React + TypeScript + Vite; Candidate B — Blazor Hybrid. The legacy application's observed UI
  workload is recorded as architecture input. **The final framework is `[OPEN]`**; the
  evidence-based preference for Candidate A is not acceptance and Candidate B is not rejected.
- **`docs/decisions/ADR-0007-runtime-process-model.md`** — process and ownership model:
  Equipment Runtime as the sole owner of physical device sessions; the UI cannot write
  hardware and owns no device session; adapters contain no UI logic; loopback, authenticated,
  contract-first Local Application API. Adds the live-state delivery and performance-isolation
  boundary: authoritative in-memory state, snapshot-plus-delta push, reconnect always
  re-snapshots, and a Main Pump stop that must never be blocked (PMP-007).
- **`docs/decisions/ADR-0008-technology-stack.md`** — .NET Long-Term Support track, ASP.NET
  Core minimal API, built-in dependency injection and configuration, structured local logging,
  validation, test strategy, and offline packaging from a local package source. The UI
  framework is explicitly `[OPEN]` and is not part of this decision.
- **`docs/decisions/ADR-0009-database-access-and-migrations.md`** — SQL Server 2025 Standard
  direction, connection ownership and pooling, transactional relational access, a
  batch-oriented Historian write path, migration ownership and execution policy, and database
  unavailability behaviour. ORM, mapper, micro-ORM, provider, and bulk-write mechanism are
  `[OPEN]`. SQL Server is not the per-cycle parameter source, and a Historian backlog must not
  decide which initiating actions are refused.
- **`docs/decisions/ADR-0010-device-adapter-boundary.md`** — adapter ports for Modbus TCP,
  WAGO I/O, Galil, SQL Server, clock, simulator, and a loopback test adapter; domain
  independence from vendor libraries; simulator parity with the application-facing contract;
  physical adapters explicit and disabled by default; command lifecycle states. Adds
  per-device serialized command queues, bounded concurrency, batched contiguous reads, a Poll
  Plan compiled at publication or startup, Fast/Medium/Slow poll groups, and the rule that the
  UI never polls.
- **`docs/decisions/ADR-0011-configuration-and-secrets.md`** — configuration layers (public
  example, local development, simulator, test hardware, production), Draft versus Published,
  secrets and user database, production configuration and secrets outside Git, and audit
  history. Adds the immutable in-memory **Published Configuration Snapshot** with atomic swap
  under the approved state gate, the rule that a Draft is never consumed, presentation
  thresholds read from Published configuration, and the Poll Plan compiled at publication.
- **`docs/decisions/ADR-0012-simulator-first-development.md`** — simulator as default,
  deterministic simulation, failure injection, contract parity, and the explicit statement
  that the proposed Stage 0.2.1 spike uses synthetic data only, touches no WAGO, Galil,
  Production SQL Server, or Production configuration, and that spike results are never
  hardware evidence.
- **`docs/decisions/ADR-0013-offline-deployment.md`** — offline Windows 11 Pro deployment,
  local services, SQL dependency, kiosk startup, service recovery, log and backup locations,
  upgrade and rollback, diagnostic bundle, local-only communication, and no Internet
  dependency. Offline packaging must be able to serve either UI candidate; the second package
  ecosystem is a cost, not a blocker.
- **`docs/ARCHITECTURE.md`** — new sections 23 to 33: legacy evidence and its required
  interpretation; UI framework candidates; the legacy UI workload with nine explicitly
  prohibited behaviours; the sensor presentation model (seven independent dimensions,
  Dirty/Cleaner classification rule, dirty-red colour rule); the quality-aware pipeline;
  operational state delivery; Modbus acquisition; the SQL and configuration hot path;
  Historian decoupling; trend direction; and the proposed Stage 0.2.1 spike. The open-item
  section moved to section 34 and its cross-references were corrected.
- **`docs/REQUIREMENTS.md`** — new section 26 with ten requirement groups: UI framework
  candidates (UIF), UI workload and prohibited behaviours (UIW), sensor presentation (SPC),
  data quality (DQS), live-state delivery (LSD), Modbus acquisition (MDA), configuration hot
  path (CPS), Historian decoupling (HDC), live trend (TRD), and the proposed spike (SPI).
  ARC-019 no longer names a language, ARC-021 no longer decides the UI framework, ARC-022 is
  stated in architecture language, and ARC-025 is bounded.
- **`docs/MASTER_PLAN.md` section 3.2** — the proposed Stage 0.2.1 UI and Runtime Technology
  Spike, `[NOT AUTHORIZED]`: identical synthetic data and contract for both candidates, the
  measurement set, the constraints, and the deliverable.
- **`docs/TEST_STRATEGY.md` section 3.8** — planned spike verification cases 46 to 60, all
  `PLANNED — NOT EXECUTED`, plus new testability requirements and open acceptance thresholds.
- Supporting alignment in `README.md`, `SECURITY.md`, `AGENTS.md`, `CHANGELOG.md`,
  `docs/CURRENT_STATE.md`, `docs/ROADMAP.md`, `docs/DOMAIN_MODEL.md` section 5.1,
  `docs/CONTROL_AUTHORITY.md` section 8.1, `docs/SAFETY_BOUNDARY.md` section 9.1,
  `docs/ALARM_MODEL.md` section 9.1, `docs/CLEANING_SEQUENCE.md` section 10.1,
  `docs/HISTORIAN_RETENTION.md` section 8.1, `docs/QUEUE_MODEL.md` section 11.1,
  `docs/USER_PERMISSION_MODEL.md` section 7.1, and `docs/decisions/README.md`.

#### Changed

- Stage-status wording across the repository now states one position: Stage 0.1 merged through
  PR #1; Stage 0.2 Scope Gate approved; Stage 0.2 architecture checkpoint submitted; review
  changes requested / in progress; Owner manual review pending; not merged; Stage 0.2.1 and
  Stage 0.3 not authorised. The previous `Stage 0.2 [NOT AUTHORIZED]` wording was removed from
  the forward-looking status of every document.
- The Stage 0.1 status block in this file is retained as history and annotated as superseded.

#### Unchanged

- Every protected decision is unchanged: one Boiler Unit per Workstation; Windows 11 Pro;
  English default with Thai contextual explanation; 104 Sensor locations; 208 Thermocouple
  channels *(historical Stage 0.2 record — superseded by the Stage 0.2.1A Owner domain correction
  to 106 Sensor locations / 212 Thermocouple channels)*; Eight Water Jets; Four Galil DMC-B140-M controllers; one Water Jet to one dedicated
  Isolation Valve; strictly sequential Cleaning Jobs; maximum one active Cleaning Job; parallel
  Water Jet Cleaning prohibited; GlobalQueue FIFO and source ownership; Hard Minimum Cleaning
  Interval on both source queues; `LastSuccessfulCleaningCompletedAt` never null; Operator as
  the baseline DCS Permissive Override role; override exclusions and the absent automatic
  expiry; cleared-state acknowledgement; the Operations UI close guard; Main Pump may remain
  running between sequential Jobs; WAGO fail-safe not verified; Production Write not
  authorised; no production configuration in the repository.
- No schema, migration, SQL script, threshold, register map, tag list, coordinate, IP address,
  connection string, credential, or secret was added.

#### Not verified

- Nothing in Stage 0.2 was built, compiled, run, measured, installed, or connected. No
  hardware, database, simulator, kiosk, installer, or deployment behaviour was tested.
- WAGO fail-safe behaviour remains `[NOT VERIFIED]`. The SQL Server deployment capacity
  conclusion remains `[NOT VERIFIED]`. Device-library behaviour is `[NOT VERIFIED]`.
- The UI framework, push transport, chart library, mapper technology, Historian overflow
  policy, poll-group intervals, spike acceptance thresholds, and the site-specific
  invalid-value mapping remain `[OPEN]`.

### Stage 0.1 review-correction checkpoint (documentation-only, superseded by Stage 0.2)

The Owner-confirmed Stage 0.1 review punchlist was applied across the affected documentation.
No application code,
  runtime scaffold, database script, hardware adapter, simulator, test, CI workflow,
  production configuration, ZIP, or release artifact was created. Summary of what the
  correction establishes:
  - **Water Jet to Isolation Valve cardinality** — exactly one-to-one, dedicated, never
    shared. A sensor's Isolation Valve is derived from the sensor's assigned Water Jet.
  - **Strictly sequential Cleaning Jobs** — INVARIANT-SEQ-001 through INVARIANT-SEQ-006,
    including that at most one Cleaning Job may be ACTIVE at any time and that parallel
    Water Jet cleaning is prohibited.
  - **GlobalQueue stop and restart policy** — an Operator stop records the Queue snapshot
    and Held/Rejected/Reordered state and closes the instance; a new AutoSequence rebuilds
    all source queues and seeds a fresh GlobalQueue rather than reloading the previous one.
  - **Source ownership after deduplication** — one source owner per entry, established by
    the preserved earliest position; merged reasons do not transfer ownership; refill uses
    the original owner.
  - **Alarm acknowledgement correction** — active acknowledgement is awareness only; a
    separate cleared-state acknowledgement is required to release a block.
  - **DCS and Modbus communication health** — transport-evidence based health evaluation
    with a configurable stale timeout, and the approved blocking behaviour for idle and
    active-job cases.
  - **DCS Permissive Override** — Operator-activated, manually released, scoped to the
    approved DCS permissive evaluation only, with an explicit exclusion list.
  - **Operations UI close guard** — blocked while a Cleaning Job is active or the Main Pump
    is running; an operational usability control, explicitly not safety protection.
  - **Requirement and test traceability** — new requirement groups (WJV, SEQ, COMH, OVR,
    UIG) and eighteen required planned test cases, all marked planned and not executed.
  - **Status, versioning, and capacity wording corrections** — gate approval separated
    from implementation acceptance; no application version; historian capacity conclusions
    removed as unsupported.

## [Historical — documentation foundation candidate]

### Stage 0.1 — Repository Documentation Foundation

**Stage 0.1 Scope Gate:** `[APPROVED]`
**Stage 0.1 implementation:** MERGED to `main` through PR #1 (merge commit
`d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`)
**Stage 0.1 documentation review:** CHANGES REQUESTED / IN PROGRESS at the time of the Stage
0.1 checkpoint; superseded by the Stage 0.1 review-correction checkpoints on the same branch
**Stage 0.1 Owner manual review:** recorded complete by the Owner in the approved Stage 0.2
Scope Gate
**Merge:** MERGED
**Stage 0.2 at the time of this Stage 0.1 record:** `[NOT AUTHORIZED]` — subsequently
authorised

The status block above is the historical Stage 0.1 record. It is retained as history and is
superseded by the current stage status recorded in [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md),
[`README.md`](README.md), and [`docs/MASTER_PLAN.md`](docs/MASTER_PLAN.md). At the current
Stage 0.2 position: Stage 0.2 Scope Gate `[APPROVED]`; Stage 0.2 architecture checkpoint
**SUBMITTED FOR OWNER REVIEW**; documentation review **CHANGES REQUESTED / IN PROGRESS**;
Owner manual review **PENDING**; **NOT MERGED**; Stage 0.2.1 `[NOT AUTHORIZED]`;
Stage 0.3 `[NOT AUTHORIZED]`.

Documentation-only stage. Established the repository governance model, the product identity
baseline, the queue and control models, and the delivery plan. No application code, runtime
scaffold, database schema, hardware adapter, CI workflow, production configuration, release
package, or archive was created.

### Added

- Repository governance
  - `AGENTS.md` — working contract for automated and human contributors: authority
    order, scope-lock rules, evidence-first rules, version and checkpoint rules,
    public-repository restrictions, device-access prohibition, destructive Git
    prohibition, Owner-only merge rule, Changed/Unchanged/Not Verified reporting,
    no-false-test-claims rule, and required stop conditions.
  - `SECURITY.md` — security policy, secret handling, vulnerability reporting,
    privileged-session expectations, and the public-repository secrecy boundary.
  - `CHANGELOG.md` — this file.
- Core documentation
  - `docs/CURRENT_STATE.md` — stage status, verified repository state, status legend, and
    open items.
  - `docs/MASTER_PLAN.md` — staged delivery plan, stage gates, and gate evidence rules.
  - `docs/ROADMAP.md` — forward view of planned stages and optional future capabilities.
  - `docs/REQUIREMENTS.md` — consolidated requirements with identifiers and status.
  - `docs/ARCHITECTURE.md` — conceptual architecture and subsystem responsibilities.
  - `docs/DOMAIN_MODEL.md` — entities, terminology, identifiers, and value ranges.
- Control, safety, and queue models
  - `docs/SAFETY_BOUNDARY.md` — hardware safety boundary, target safe states, and the
    bench verification required before any production write control is permitted.
  - `docs/CONTROL_AUTHORITY.md` — control authority matrix.
  - `docs/CLEANING_SEQUENCE.md` — Cleaning Job definition, normal sequence, and
    failure and recovery boundaries.
  - `docs/ALARM_MODEL.md` — condition, acknowledgement, and shelving dimensions,
    severities, and blocking-release rules.
  - `docs/QUEUE_MODEL.md` — TempQueue, TimeQueue, GlobalQueue, deduplication,
    FIFO refill, and operator queue actions.
  - `docs/HISTORIAN_RETENTION.md` — acquisition intervals, storage targets, retention
    proposals, and cleanup rules.
  - `docs/USER_PERMISSION_MODEL.md` — role templates, session rules, audit
    requirements, and the break-glass recovery account.
  - `docs/TEST_STRATEGY.md` — planned verification approach for future stages, and the
    explicit statement that no runtime testing has occurred.
  - `docs/PUBLIC_REPOSITORY_BOUNDARY.md` — what may and may not be published.
- Decision records
  - `docs/decisions/README.md` — ADR index and format.
  - `docs/decisions/ADR-0001-product-identity.md`
  - `docs/decisions/ADR-0002-deployment-architecture.md`
  - `docs/decisions/ADR-0003-queue-arbitration.md`
  - `docs/decisions/ADR-0004-historian-strategy.md`
  - `docs/decisions/ADR-0005-hardware-safety-boundary.md`

### Changed

- `README.md` — replaced the two-line placeholder with the repository entry point.
- `.gitignore` — extended the existing Visual Studio template with repository-specific
  exclusions for local production configuration, secrets, and exported data, so that
  confidential deployment material stays outside the public repository.

### Safety and scope notes for this entry

- No production device access was performed. Production device access remains
  `[NOT AUTHORIZED]`.
- WAGO watchdog fail-safe behaviour remains `[NOT VERIFIED]`.
- Motion limits, pulses per engineering unit, encoder behaviour, and pressure setpoints
  remain `[NOT VERIFIED]`.
- Production register maps and tag lists were not created and must never be committed;
  see `docs/PUBLIC_REPOSITORY_BOUNDARY.md`.
- No runtime, database, or hardware test was executed. Only documentation validation was
  performed. See `docs/CURRENT_STATE.md` for the validation record.
