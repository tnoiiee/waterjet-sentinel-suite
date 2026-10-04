# Current State — WaterJet Sentinel Suite (WJSS)

**Document status:** The verified state below is `[APPROVED]` as a factual record.
Stage status wording and the open-item list were corrected by the Owner-confirmed Stage 0.1
documentation review punchlist, updated by the approved Stage 0.2 Scope Gate — *Technology and
Solution Architecture Decision*, and refined by the Owner-requested Stage 0.2 documentation
review punchlist. Open items are `[OPEN]` and must not be resolved by assumption.

This document answers one question: *what is actually true right now, with evidence?*
Nothing in this repository may contradict it. If something does, the discrepancy is reported
rather than silently resolved.

---

## 1. Status legend

Used throughout the documentation:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Approved by the Owner in an approved Stage Gate. Binding on future work. |
| `[OWNER CONFIRMED]` | Explicitly confirmed by the Owner during documentation review. Binding. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. **Not** approved behaviour. |
| `[NOT VERIFIED]` | Not confirmed by bench test, field test, measurement, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Currently prohibited. The activity, write, or access must not be performed. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed in the meantime. |

Stage implementation status uses a separate vocabulary: `SUBMITTED FOR OWNER REVIEW`,
`CHANGES REQUESTED`, `IN PROGRESS`, `OWNER ACCEPTED`, `NOT MERGED`, `MERGED`. Only the Owner
may record `OWNER ACCEPTED` or `MERGED`.

## 2. Stage and approval status

| Item | Value |
| --- | --- |
| Stage 0.1 Scope Gate | **APPROVED** |
| Stage 0.1 implementation | **MERGED** — merged to `main` through PR #1 |
| Stage 0.1 Owner manual review | **Recorded as complete by the Owner** in the approved Stage 0.2 Scope Gate, which states that the previous Stage branch completed its role and was merged through PR #1 |
| Stage 0.2 Scope Gate | **APPROVED** — *Technology and Solution Architecture Decision* |
| Stage 0.2 architecture checkpoint | **SUBMITTED FOR OWNER REVIEW** |
| Documentation review (Stage 0.2) | **CHANGES REQUESTED / IN PROGRESS** — the Owner-requested punchlist has been implemented on the same branch and pull request; re-review pending |
| Owner manual review (Stage 0.2) | **PENDING** |
| Merge | **NOT MERGED** |
| Stage 0.2.1 | **NOT AUTHORIZED** |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

Approval of the Stage 0.2 Scope Gate authorised the work. It is **not** acceptance of the
Stage 0.2 implementation. The Stage 0.2 documentation review has requested changes; those
changes are implemented in a review-correction checkpoint on the same branch and pull request,
and re-review is pending. Nothing in this repository may describe the Stage 0.2 outcome as
Owner accepted, final, merged, reviewed, or as a completed development checkpoint accepted by
the Owner. Stage 0.2 is a documentation and architecture-decision checkpoint only: no
application code exists, no dependency was installed, no device was contacted, and no
Production Write was performed or authorised.

The Stage 0.1 process-deviation record in section 11.1 is retained unchanged.

## 3. Application version

| Item | Value |
| --- | --- |
| Application version | **NOT ESTABLISHED** |
| Runtime release | **None exists** |
| Documentation versioning policy | `[OPEN]` — no scheme is adopted |
| Former `0.1.0` label | A documentation-only working label. **Not** a product version, not a release. Retained in [`../CHANGELOG.md`](../CHANGELOG.md) only for historical traceability. |

No governance rule requires a documentation stage to advance a version number.

## 4. Repository state

| Item | State |
| --- | --- |
| Current stage | Stage 0.2 — Technology and Solution Architecture Decision (documentation and architecture decisions only) |
| Repository contents | Documentation and repository governance only |
| Application source code | **Does not exist** |
| Project or solution files | **Do not exist** |
| Package manifests or dependencies | **Do not exist** |
| Database schema or SQL scripts | **Do not exist** |
| Modbus or Galil adapter | **Does not exist** |
| Simulator | **Does not exist** |
| Automated tests | **Do not exist** |
| CI workflow | **Does not exist** |
| Installer or release artifact | **Does not exist** |
| Production configuration | **Does not exist in this repository** |
| Device connections made | **None** |

## 5. Verified system state

- Stage 0.2 is a documentation and architecture-decision activity only. It created no runtime
  artefact and installed no dependency.
- Stage 0.2 selected a `[PROPOSED]` technology and solution architecture — UI delivery model,
  runtime process model, technology stack, database access and migrations, device adapter
  boundary, configuration and secrets model, simulator-first development model, and offline
  deployment model — recorded as ADR candidates in
  [`decisions/README.md`](decisions/README.md). **`[PROPOSED]` is not approval**; each record
  requires Owner acceptance, and nothing was implemented.
- The architecture was decided in a working environment that contains no .NET SDK, no SQL
  Server client tooling, and no Windows runtime. No build, restore, execution, or measurement
  was possible or performed.
- The Owner-requested Stage 0.2 documentation review punchlist was implemented as a
  documentation-only review-correction checkpoint. It recorded the legacy-application
  architecture evidence, returned the final UI framework selection to `[OPEN]`, added the fair
  React-versus-Blazor comparison, and added the UI workload, Sensor presentation, quality
  pipeline, live-state delivery, Modbus acquisition, configuration hot-path, Historian
  decoupling, trend, and proposed Stage 0.2.1 spike content. No code, dependency, device
  access, or Production configuration was created.
- Stage 0.1 is a documentation and repository-governance activity only.
- No application code has been written, and no code may be written until a later Stage Gate
  authorises it.
- No hardware has been accessed. No WAGO coupler, Galil controller, pump, valve, or DCS
  signal has been contacted, probed, or tested.
- No production IP address, register map, tag list, motion coordinate, travel limit, speed
  profile, pressure setpoint, production alarm threshold, DCS permissive definition,
  credential, or connection string has been created or committed.
- The documentation distinguishes approved behaviour (`[APPROVED]`), Owner review decisions
  (`[OWNER CONFIRMED]`), and proposals (`[PROPOSED]`).

## 6. Owner-confirmed decisions closed during documentation review

These were previously `[OPEN]` or ambiguously described. They are now settled and must not be
reopened without a new Owner decision.

| # | Decision | Where recorded |
| --- | --- | --- |
| 1 | Water Jet to Isolation Valve is exactly one-to-one, dedicated, never shared; a sensor's valve is derived from its assigned Water Jet | [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001..006, [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8 |
| 2 | AutoSequence executes Cleaning Jobs strictly sequentially; INVARIANT-SEQ-001 to 006 | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ group |
| 3 | Maximum active Cleaning Jobs equals one | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §3 |
| 4 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 5 | GlobalQueue behaviour on Operator stop, and queue rebuild on a new AutoSequence | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9, [`REQUIREMENTS.md`](REQUIREMENTS.md) QUE-024, QUE-025 |
| 6 | Source ownership after deduplication; refill uses the original source owner | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3, §5.4 |
| 7 | Active alarm acknowledgement is awareness only; a cleared-state acknowledgement is required to release a block | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 8 | "Allow unaffected Water Jets to continue" means sequential continuation only | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2 |
| 9 | DCS Permissive Override: operator activated, manually released, reason and audit required, banner, scoped exclusions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR group |
| 10 | Operations UI close guard required while a job is active or the pump runs; it is not safety protection | [`REQUIREMENTS.md`](REQUIREMENTS.md) UIG group, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 11 | Communication health must not depend on value change detection; configurable stale timeout | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 12 | Cross-wall DirtyScore tie-break is not required; GlobalQueue order comes from the fixed source order | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §2 |
| 13 | Baseline role for the DCS Permissive Override is **Operator**: the Operator role template holds permission to activate **and** release it; a role without the permission cannot do either. The permission model remains configurable, and the baseline assignment remains Operator until an explicit Owner decision changes it | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-011..OVR-015 |

## 7. Blocking constraints in force right now

1. **No production device access.** `[NOT AUTHORIZED]`
2. **No production valve or pump write control** until the bench verification in
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) is completed and recorded. `[NOT AUTHORIZED]`
3. **No application code, schema, adapter, simulator, test, CI workflow, installer, or
   release artifact** may be created until a later Stage Gate authorises it. `[NOT AUTHORIZED]`
4. **No production values** may be invented, inferred, or committed. `[NOT AUTHORIZED]`
5. **No concurrent Cleaning Jobs.** Parallel Water Jet cleaning is prohibited. `[OWNER CONFIRMED]`
6. **No merging.** Merge authority belongs to the Owner. `[APPROVED]`
7. **No history rewriting or force push** without specific Owner authorisation. `[APPROVED]`
8. **No new pull request** to replace the existing reviewed pull request. `[APPROVED]`

## 8. Open items requiring Owner decisions

These remain unresolved. None may be resolved by assumption. Items closed by the review
punchlist have been removed; see section 6 for what was closed. Items are grouped below by
theme rather than numbered, because the list changes as decisions are taken.

### 8.1 Product and technology

The Stage 0.1 open technology items were addressed by Stage 0.2. Each one is now either a
`[PROPOSED]` architecture decision awaiting Owner acceptance, an `[OPEN]` item with a named
follow-up gate, or a `[NOT VERIFIED]` item requiring hardware or workstation evidence. The
disposition table is in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 11, the remaining open
items are listed in section 34 of the same document, and the decision records are indexed in
[`decisions/README.md`](decisions/README.md).

| Item | Stage 0.2 disposition |
| --- | --- |
| Application language, runtime, UI framework, and UI delivery architecture | Language, runtime, UI technology, and delivery model selected as `[PROPOSED]`; exact .NET version `[OPEN]`. Browser-based, desktop, and hybrid local-web delivery were all evaluated on requirements |
| Process architecture, service identity, startup behaviour, and Equipment Runtime separation | Process model selected as `[PROPOSED]`; service identity `[OPEN]` |
| Modbus TCP client library selection and licence acceptability | Boundary decided; library selection `[OPEN]` pending licence and offline-availability review |
| Galil communication mechanism and library selection | `[OPEN]` — must be evaluated before motion code is written |
| Local configuration store format and its validation mechanism | Model selected as `[PROPOSED]`; format and secret store `[OPEN]` |
| Data access approach for SQL Server 2025 Standard | Approach selected as `[PROPOSED]`; provider version and compatibility `[OPEN]` |
| Logging, diagnostics, and crash-report storage | Strategy selected as `[PROPOSED]`; provider `[OPEN]` |
| Final documentation and application versioning scheme | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Deployment acceptance criteria | Remains `[OPEN]` — unchanged by Stage 0.2 |
| Backup, restore, and off-box copy | Remains `[OPEN]` — carried forward into the offline deployment decision |
| Historian write-path measurement and overflow policy | Remains `[OPEN]` — requires measurement on the target workstation |
| Kiosk startup mechanism, package format, firewall rules, diagnostic bundle contents | Remains `[OPEN]` — deployment-gate items |
| **Final UI framework (Candidate A React + TypeScript + Vite versus Candidate B Blazor Hybrid)** | **Returned to `[OPEN]`** by the Owner-requested punchlist. A current evidence-based preference for Candidate A is recorded and is **not an acceptance**. Selection requires the proposed Stage 0.2.1 spike |
| Push transport and presentation-state payload encoding | `[OPEN]` — presentation contract is transport-agnostic |
| Chart / trend library; UI test tooling; visual regression tooling | `[OPEN]` — library-neutral requirements recorded; selection needs spike evidence |
| Site-specific invalid-value and sentinel mapping | `[OPEN]` — requires the Tag and data-quality contract; production values must never be committed |
| ORM, mapper, micro-ORM, provider, and bulk-write mechanism | `[OPEN]` — the earlier "mapper as the primary technology" wording was replaced with testable architecture language |
| Historian overflow, spool, retry, priority, and outage policy | `[OPEN]` — must be explicit before Historian implementation and separated from the audit-required refusal policy |
| Poll Plan batching limits, poll-group intervals, concurrency bound, runtime recompilation | `[OPEN]` — requires device evidence and local Production configuration |
| Sensor cell colour tokens, typography, dimensions, animation; accessibility requirement level | `[OPEN]` — deliberately deferred to a future UX/UI decision |
| Acceptance thresholds for the Stage 0.2.1 spike | `[OPEN]` — must be set before the spike runs |

### 8.2 Sequence, queue, and equipment behaviour

| Item | Affects |
| --- | --- |
| Fault class taxonomy: when to stop motion versus abort motion | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Recovery procedure for `RECOVERY_REQUIRED`; whether retry is permitted; whether partial-path jobs may resume | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §11 |
| Permission required for each operator queue action, and required reason text | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Whether a sensor may be both held and rejected simultaneously | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §12 |
| Queue snapshot storage format and retention inside Event history | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §11 |
| Row alignment rule for walls of unequal size (24 versus 28); sensor identifier format | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SCN-005 |
| Exact motion limits, profiles, homing, pulses per engineering unit, and operational envelope | [`REQUIREMENTS.md`](REQUIREMENTS.md) GAL-006 `[NOT VERIFIED]` |
| Exact pressure setpoints, rise timeout, stable dwell, and valve open/close timeouts | [`REQUIREMENTS.md`](REQUIREMENTS.md) PMP-005, VLV-003 `[NOT VERIFIED]` |
| Sensor-to-Water-Jet mapping and exact production coordinates | Deployment data, `[NOT VERIFIED]` |

### 8.3 DCS and communication

| Item | Affects |
| --- | --- |
| Exact DCS permissive definition set that the override bypasses | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §9 |
| DCS heartbeat hardware contract, if a heartbeat feature is pursued | [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH-004 `[OPEN]` |
| Production register map, Tag List, and signal quality contract | `[NOT VERIFIED]` |
| Exact local equipment network topology and address plan | [`ARCHITECTURE.md`](ARCHITECTURE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) PHY-008 |
| Stale-data timeout production value (30 s is an example only) | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5 |

### 8.4 Alarms, users, and data

| Item | Affects |
| --- | --- |
| Alarm definition catalogue, thresholds, blocking scopes, and non-shelvable list | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10 |
| Shelving duration limits and per-alarm maximums | [`ALARM_MODEL.md`](ALARM_MODEL.md) §6 |
| Whether active awareness acknowledgement and cleared-state acknowledgement share a field or a permission | [`ALARM_MODEL.md`](ALARM_MODEL.md) §10, [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Authentication method, password policy, lockout, and privileged timeout default | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Whether named individual accounts are introduced in a future approved scope | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) §8 |
| Retention default ratification; aggregate function definition; partition scheme; index strategy | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Backup, restore, and disaster-recovery approach | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §7 |
| Audit and event tamper protection | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) §6, [`../SECURITY.md`](../SECURITY.md) |
| Behaviour when the Historian is unavailable | [`ARCHITECTURE.md`](ARCHITECTURE.md) §9 |
| Workstation clock discipline and drift bounds | [`ARCHITECTURE.md`](ARCHITECTURE.md) §7 |
| Target test coverage thresholds and test execution tooling | [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §7 |

## 9. Not verified — standing list

| Item | Reason |
| --- | --- |
| WAGO watchdog fail-safe behaviour and timeout value | No bench test performed; not authorised |
| Hardware fail-safe on Ethernet loss, process termination, workstation reboot, WAGO reboot, stale command replay, auto re-energization | No bench test performed; not authorised |
| Production hardware response to any output command | No device access; not authorised |
| Mechanical limits, soft limits, and operational envelope | Commissioning values; not measured |
| Pulses per engineering unit | Commissioning value; not measured |
| Encoder behaviour and whether feedback performs active correction | Not verified by engineering |
| Homing behaviour | Commissioning value; not measured |
| Speed, acceleration, deceleration profiles | Commissioning values; not measured |
| Pressure-ready setpoint, stable dwell, pressure rise timeout | Commissioning values; not measured |
| Valve open and close timeouts | Commissioning values; not measured |
| Stale-data timeout production value | Configurable; no production value captured |
| Production network topology and address plan | Confidential deployment information |
| Production register map, Tag List, and DCS signal contract | Confidential; never committed |
| Sensor-to-Water-Jet and sensor-to-valve mapping | Deployment data; not recorded here |
| Historian effective capacity and database sizing | No capacity model produced; no benchmark performed |
| Runtime build, unit tests, integration tests, database tests, hardware tests | No code exists; not applicable at this stage |

## 10. Sensitive data review

| Check | Result |
| --- | --- |
| Production IP addresses in the repository | None found |
| Credentials, tokens, keys, or password hashes | None found |
| Connection strings | None found |
| Production register map, Tag List, or addresses | Not present and must never be added |
| Motion coordinates, travel limits, speed profiles | Not present |
| Pressure setpoints or production alarm thresholds | Not present |
| DCS permissive definitions | Not present |
| Production configuration content | Not created. Only documentation references and `.gitignore` exclusion patterns exist. |

The checks behind this table are listed with their observed results in section 12.

## 11. Checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.1 |
| Branch | `arena/01a1080d-waterjet-sentinel-suite` |
| Original implementation checkpoint | `0323f8a5a06bad25383dbe636b75c46ab38455af` |
| Original checkpoint status | **SUBMITTED FOR OWNER REVIEW** |
| Documentation review | **CHANGES REQUESTED** |
| Owner manual review | **PENDING** |
| Merge state | **NOT MERGED** |
| Pull request | https://github.com/tnoiiee/waterjet-sentinel-suite/pull/1 |
| Review-correction checkpoint | `899a96a5b01a8e3cfcf0aaf2468ff83ce735090e` — on the same branch and the same pull request |
| Final targeted-correction checkpoint | A final targeted-correction commit on the same branch and the same pull request. Its SHA is recorded in the delivery report and the pull request description, not inside the commit that creates it. |
| Stage 0.1 final merge state | **MERGED** to `main` through PR #1. The merge commit is `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a`, which is the approved remote-main base of Stage 0.2 |
| Stage 0.2 | **AUTHORIZED** by the approved Stage 0.2 Scope Gate |

Each correction is a new commit on the existing branch. No reviewed checkpoint was amended,
rebased, or rewritten.

### 11.2 Stage 0.2 checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.2 — Technology and Solution Architecture Decision |
| Branch | `arena/01a1087c-waterjet-sentinel-suite` |
| Base | `d49eeee0d937465d61abd6e754b9a2bea5ef1d6a` (approved remote `main`) |
| Development Checkpoint | Created on the Stage 0.2 branch. Its SHA is recorded in the delivery report and in the pull request description, **not** inside the commit that creates it |
| Checkpoint status | **SUBMITTED FOR OWNER REVIEW** |
| Documentation review | **PENDING** |
| Owner manual review | **PENDING** |
| Merge state | **NOT MERGED** |
| Pull request | A new pull request targeting `main`. PR #1 was not reused and is not reopened |
| Stage 0.3 | **NOT AUTHORIZED** |
| Production Device access | **NOT AUTHORIZED** |
| Production Write | **NOT AUTHORIZED** |

### 11.1 Process deviation record

| Item | Statement |
| --- | --- |
| Event | During the Stage 0.1 review-correction session, the local branch pointer did not match the expected remote Stage checkpoint |
| Reported evidence | The working-tree content was verified as byte-identical to the remote checkpoint, and the resulting push was reported as a fast-forward |
| Reported integrity | **NO MISMATCH FOUND** in source content, and **no history rewrite was reported** |
| Deviation | The approved Stop condition required the session to stop and report the baseline mismatch before continuing. A mixed pointer/index update was performed instead, so stop-condition compliance was **not** met |
| Governance response | [`../AGENTS.md`](../AGENTS.md) §7.1 now defines a strict Baseline Mismatch Stop Gate. Content comparison is diagnostic evidence only, content identity does not authorise recovery, `/tmp` is not durable recovery evidence, and recovery requires explicit Owner-authorised instructions |
| Status | **PROCESS DEVIATION RECORDED** — Owner acceptance **PENDING** |
| Integrity conclusion | The deviation does not indicate source corruption. It is recorded as a process-compliance deviation, not as an integrity failure |

This record is retained deliberately. Git history was not rewritten to remove the deviation.

## 12. Validation record

Documentation-only validation, performed for the Stage 0.1 review-correction checkpoint.
Detailed commands and observed output are reported in the delivery report.

| # | Check | Method |
| --- | --- | --- |
| 1 | List all changed files; confirm no out-of-scope file | `git status --porcelain` and `git diff --stat` before committing |
| 2 | Internal Markdown links resolve | Extracted every relative link target and verified each path exists |
| 3 | Production IP address search (IPv4 and CIDR) | Regular-expression scan across tracked files |
| 4 | Credential, token, password, private-key, and connection-string search | Keyword and pattern scan across tracked files |
| 5 | Conflicting null use for `LastSuccessfulCleaningCompletedAt` | Keyword scan |
| 6 | Obsolete `NEVER_CLEANED` behaviour | Keyword scan |
| 7 | Claims that WAGO fail-safe has passed | Keyword scan of surrounding context |
| 8 | Claims that production device access is authorised | Keyword scan |
| 9 | Parallel Cleaning Jobs described as `[OPEN]` | Cross-document search for `parallel`, `concurrent`, and related open-item entries |
| 10 | Water Jet to Isolation Valve cardinality described as `[OPEN]` | Cross-document search |
| 11 | "Six source queues" or any other wrong source-queue count | Cross-document search |
| 12 | Old GlobalQueue reload after a sequence restart | Cross-document search |
| 13 | Automatic final acknowledgement after an alarm clears | Cross-document search |
| 14 | `0.1.0` described as an application release | Cross-document search |
| 15 | Stage 0.1 implementation described as Owner accepted | Cross-document search |
| 16 | Web or local-web delivery described as rejected | Cross-document search |
| 17 | "must not infer the state" wording | Cross-document search |
| 18 | Main Pump stop described as executable in all circumstances | Cross-document search |
| 19 | Unsupported historian capacity certainty | Cross-document search |
| 20 | No application code or package dependency created | File-type and file-extension inventory |
| 21 | Diff reviewed before commit | Full diff read before creating the review-correction commit |

### 12.1 Stage 0.2 checkpoint documentation validation record

Documentation-only validation, performed for the **first** Stage 0.2 checkpoint. Every result
below is observed output from the method shown. The exact commands are reported in the Stage 0.2
delivery report.

**Superseded.** This record describes the first checkpoint. The Owner-requested Stage 0.2
documentation review then returned changes, and the corrected state is recorded in section
12.2. Two rows below are no longer an accurate description of the repository: row 1 names
12 modified and 8 created files, and row 8 lists the stage-status wording of the first
checkpoint. Neither is wrong as history; both were changed by the review-correction
checkpoint.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` | 12 modified existing documents and 8 new ADR files; nothing else created or modified |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. No solution, project, manifest, lock file, source, SQL, script, installer, archive, or CI file exists |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | **33 files scanned, 573 internal relative links checked, 0 broken** |
| 5 | Protected decisions remain stated | Targeted `grep` runs, listed in the delivery report | All required statements found, including Water Jet to Isolation Valve one-to-one, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, the Operator baseline override role, cleared-state acknowledgement, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, and Stage 0.3 `[NOT AUTHORIZED]` |
| 6 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | UI owns no device session and cannot write to hardware; the runtime service owns physical device sessions; domain logic is isolated from vendor libraries; simulator adapters implement the application-facing contracts; physical adapters are disabled by default; production configuration stays outside the repository; no statement permits parallel Cleaning Jobs |
| 7 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | **Zero matches** |
| 8 | Status check | Stage-status wording search | Stage 0.2 Scope Gate APPROVED; Stage 0.2 implementation SUBMITTED FOR OWNER REVIEW; Owner manual review PENDING; NOT MERGED; Stage 0.3 NOT AUTHORIZED; Production Device access and Production Write NOT AUTHORIZED |
| 9 | Full diff review | Complete staged diff read before the checkpoint commit | Every change traceable to Stage 0.2; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 10 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, or installer test was executed, and none is claimed |

### 12.2 Stage 0.2 review-correction (punchlist) documentation validation record

Documentation-only validation, performed for the Stage 0.2 review-correction checkpoint that
implemented the Owner-requested punchlist. Every result below is observed output from the
method shown. The exact commands are reported in the Stage 0.2 delivery report.

| # | Check | Method | Observed result |
| --- | --- | --- | --- |
| 1 | Changed and created file list; no out-of-scope file | `git status --porcelain` and `git diff --stat` | Only repository documentation and decision records modified. No application source, project, manifest, lock file, SQL, script, installer, archive, CI file, configuration file, or runtime directory was created |
| 2 | No runtime or package artefact exists | File-type and file-extension inventory over all non-`.git` files | 33 Markdown files and one `.gitignore`. Only `./docs` and `./docs/decisions` directories exist |
| 3 | No dependency installed | No package-manager or installer command was executed | No dependency installed, restored, or vendored |
| 4 | Internal relative Markdown links resolve | Inline Python checker over every Markdown file; relative targets only, absolute URLs and in-page anchors excluded | 33 files scanned; broken count reported in the delivery report. Any non-zero count is a defect that must be fixed before commit |
| 5 | Every ADR keeps its required section set | Heading extraction per ADR | Status, Context, Decision, Alternatives considered, Consequences, Risks, Verification status, Follow-up gates, Relationship to protected decisions |
| 6 | The UI framework selection is not presented as accepted | Cross-document search for framework-acceptance wording | Final framework is `[OPEN]`; both candidates are `[PROPOSED]`; the React preference is explicitly not acceptance; Blazor is explicitly not rejected |
| 7 | The React offline claim is correct and present | Targeted search | "React can be built and deployed offline" is stated, together with the second-ecosystem cost and the explicit statement that this does not make offline development or deployment impossible |
| 8 | Protected decisions remain stated and unmodified | Targeted `grep` runs, listed in the delivery report | All required statements found and unchanged: one Boiler Unit per Workstation, 104 Sensor locations, 208 Thermocouple channels, Eight Water Jets, Four Galil DMC-B140-M controllers, Water Jet to Isolation Valve one-to-one, strictly sequential Cleaning Jobs, maximum one active Cleaning Job, parallel cleaning prohibited, GlobalQueue FIFO and source ownership, `LastSuccessfulCleaningCompletedAt` never null, Operator baseline override role, cleared-state acknowledgement, Main Pump may remain running between sequential Jobs, WAGO fail-safe `[NOT VERIFIED]`, Production Write `[NOT AUTHORIZED]`, Production configuration never in the repository |
| 9 | Architecture consistency | Targeted `grep` runs, listed in the delivery report | The UI owns no device session and cannot write to hardware; the Equipment Runtime service is the sole owner of physical device sessions; adapters contain no UI logic and the domain is independent of vendor libraries; the simulator implements the same application-facing contract; physical adapters are disabled by default; the runtime consumes only Published configuration; the UI does not read Modbus and does not query SQL for live state; no statement permits parallel Cleaning Jobs |
| 10 | Sensitive-data scan | IPv4 and CIDR regex, credential-shaped regex, connection-string regex, numeric configuration patterns, and register-map-like rows, across all non-`.git` files | Zero matches |
| 11 | Stage-status consistency | Stage-status wording search across every Markdown file | Zero remaining lines describing Stage 0.2 as `[NOT AUTHORIZED]`. Every file now states the current position: Stage 0.2 Scope Gate `[APPROVED]`; Stage 0.2 architecture checkpoint SUBMITTED FOR OWNER REVIEW; documentation review CHANGES REQUESTED / IN PROGRESS; Owner manual review PENDING; NOT MERGED; Stage 0.2.1 `[NOT AUTHORIZED]`; Stage 0.3 `[NOT AUTHORIZED]` |
| 12 | Cross-reference integrity | Search for the old `section 22` reference and for the new section numbers | The open-item section is now section 34 and the earlier references were corrected. Sections 23–33 referenced by the decision records all exist |
| 13 | Full diff review | Complete diff read before the checkpoint commit | Every change is traceable to the punchlist; no domain or product behaviour changed; no proposed decision presented as a runtime fact |
| 14 | No test claim | Review of the complete diff | No build, runtime, database, hardware, kiosk, simulator, or installer test was executed, and none is claimed |

#### 12.2.1 Superseded scope limitation — stage-status lines in documents the first gate did not name

The first Stage 0.2 checkpoint reported, as a scope limitation, that seven documents still
carried the Stage 0.1-era line `Stage 0.2 `[NOT AUTHORIZED]``: `CHANGELOG.md`, `SECURITY.md`,
`docs/ALARM_MODEL.md`, `docs/CLEANING_SEQUENCE.md`, `docs/HISTORIAN_RETENTION.md`,
`docs/QUEUE_MODEL.md`, and `docs/USER_PERMISSION_MODEL.md`. They were not modified at that
checkpoint, because the first gate did not name them and [`AGENTS.md`](../AGENTS.md) section 2.2
prohibits modifying a file the current Stage Gate does not name.

**That limitation is now closed.** The Owner-requested Stage 0.2 documentation review punchlist
explicitly names those documents for a stage-status refresh, which authorises the change
(see the authority order in [`AGENTS.md`](../AGENTS.md) section 1). Their status lines now state
the current position. The `CHANGELOG.md` Stage 0.1 status block is retained as history and is
annotated as superseded, because it is a historical record of that stage; the forward-looking
stage status no longer says `[NOT AUTHORIZED]`.

## 13. Required positive confirmations

The documentation explicitly contains each of the following:

| # | Statement | Where |
| --- | --- | --- |
| 1 | One Water Jet to one Isolation Valve, dedicated, never shared | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §2.8, [`REQUIREMENTS.md`](REQUIREMENTS.md) WJV-001 |
| 2 | At most one Cleaning Job ACTIVE at a time | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) §2, [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-001 |
| 3 | Parallel Water Jet cleaning prohibited | [`REQUIREMENTS.md`](REQUIREMENTS.md) SEQ-006 |
| 4 | GlobalQueue stop and rebuild policy | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §9 |
| 5 | Source ownership after deduplication | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) §5.3 |
| 6 | Cleared-state acknowledgement requirement | [`ALARM_MODEL.md`](ALARM_MODEL.md) §3 |
| 7 | DCS communication health | [`ARCHITECTURE.md`](ARCHITECTURE.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) COMH group |
| 8 | DCS Permissive Override restrictions | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) OVR-009 |
| 9 | Operations UI close guard | [`ARCHITECTURE.md`](ARCHITECTURE.md) §4.12, [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §6 |
| 10 | Production Write not authorized | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §4, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-002 |
| 11 | WAGO fail-safe not verified | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) §5, [`REQUIREMENTS.md`](REQUIREMENTS.md) HSB-001 |
| 12 | The UI cannot write to hardware and owns no device session | [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.3, [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-001 to ARC-003 |
| 13 | The Equipment Runtime is the only physical-device-session owner | [`decisions/ADR-0007`](decisions/ADR-0007-runtime-process-model.md), [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.2 |
| 14 | Domain logic does not depend on vendor device libraries | [`REQUIREMENTS.md`](REQUIREMENTS.md) ARC-007, [`decisions/ADR-0010`](decisions/ADR-0010-device-adapter-boundary.md) |
| 15 | Simulation is not hardware verification or certification | [`decisions/ADR-0012`](decisions/ADR-0012-simulator-first-development.md), [`TEST_STRATEGY.md`](TEST_STRATEGY.md) §3.7 |

This document must not be read as claiming any test, build, database, hardware, or device
verification. See section 9.

---

## Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates and implementation status
- [`ROADMAP.md`](ROADMAP.md) — forward view
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — current prohibition on control writes
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — verification levels and what has not been tested
