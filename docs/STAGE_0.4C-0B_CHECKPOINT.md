# Stage 0.4C-0B — Six-Node Thermocouple Topology Baseline Rev1, Drawing-Derived Channel Schedule and Documented Process-Image Profile (Development Checkpoint)

> **Closeout note (2026-10-12).** Sections 0–15 below are the 2026-10-11
> zero-evidence checkpoint record, preserved unchanged as audit history
> (initial PR #18 run: `EVIDENCE_NOT_AVAILABLE_IN_SESSION`). The evidence
> arrived afterwards and was fully reviewed; the current completion record is
> **§16**, which supersedes the §§4–5/7–8/10/12 STOP positions for current
> status only. Current Stage 0.4C-0B state: `EVIDENCE_REVIEW_COMPLETE`,
> `FIELD_VERIFICATION_PENDING`, `STAGE_0.4C-1_BLOCKED`. PR #18 OPEN — NOT MERGED.

Status: **DEVELOPMENT CHECKPOINT — documentation and evidence-intake record only, SUBMITTED FOR OWNER REVIEW.** Nothing is implemented, tested on hardware, commissioned or merged. **SCHEDULE DERIVATION STOPPED: NO DRAWING FILE IS PRESENT IN THIS SANDBOX (brief §4 STOP). MANUAL EVIDENCE EXTRACTION STOPPED: NO MANUAL FILE IS PRESENT (brief §§7–8).** `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. `WIRE_BREAK_DECODING: BLOCKED`. **NEW PR OPEN — NOT MERGED.**

Stop point: this checkpoint. The Owner merges; the agent does not.

**Honesty lines:** NEW SESSION USED. APPROVED REMOTE-MAIN BASE VERIFIED. SIX-NODE TC TOPOLOGY OWNER-CONFIRMED AS BRIEF-STATED DESIGN BASIS (NOT DRAWING-VERIFIED, NOT HARDWARE-VERIFIED). TEST IP CONFIGURATION OWNER-AUTHORIZED AS TEST ONLY (NOT PRODUCTION). 750-498 FACTORY DEFAULTS OWNER-CONFIRMED AS CONFIGURATION BASIS (MANUAL NOT READ — FILE ABSENT). DRAWING STATUS DRAWING_PENDING_REVISION. DRAWING BASELINE REV1 (ZERO ROWS DERIVED). DRAWING-DERIVED MAPPING IS FIELD-VERIFICATION PENDING — AND DRAWING-EXTRACTION PENDING: NO DRAWING FILE WAS AVAILABLE. PHYSICAL SENSOR ACQUISITION NOT IMPLEMENTED. HARDWARE LOOP TEST NOT EXECUTED. NO LIVE MODBUS COMMUNICATION IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor has been tested. No Sensor-to-Channel binding has been derived or inferred. No manual section, page or default value has been read or cited. NEW PR OPEN - NOT MERGED.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0B |
| Session branch (platform-assigned, fixed) | `arena/c4b63a26-waterjet-sentinel-suite` |
| Approved remote-main base | `main` = `3e1a7fd9d28db65221c123960a0c85b62d93949a` — the **PR #17 merge commit** |
| PR #17 merge parents | `bd1437586af73a33d43dfafce27b27538183a283` and `48d8605a4d687f509edf079f2d371319d6bffa21` |
| PR #17 state | `MERGED` at `2026-10-11T08:59:28Z` |
| Predecessor branch (closed, not pushed to) | `arena/1cfd7e20-waterjet-sentinel-suite` |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — re-verified, unchanged |
| Development checkpoint commit | Its SHA is recorded in the PR and the final report, not inside its own commit (AGENTS §4.6) |

---

## 0. New-session base gate — PASSED

Every check below was executed at the start of this session; the results are the observed tool
output, not a restatement of the predecessor record.

| # | Check | Command / method | Result |
|---|---|---|---|
| 1 | Remote main equals the approved base | `git ls-remote origin refs/heads/main` | `3e1a7fd9d28db65221c123960a0c85b62d93949a` — **exact match** |
| 2 | Merge commit parents | `git cat-file -p 3e1a7fd9…` (plus read-only `git fetch origin` of both parent commits to prove ancestry exists) | `parent bd1437586af73a33d43dfafce27b27538183a283`, `parent 48d8605a4d687f509edf079f2d371319d6bffa21` — **exact match, in order**; both parents resolve to commits |
| 3 | Merge commit subject | same | `Merge pull request #17 from tnoiiee/arena/1cfd7e20-waterjet-sentinel-suite` |
| 4 | PR #17 merged | `gh pr view 17` | `state: MERGED`, `mergedAt: 2026-10-11T08:59:28Z`, `mergeCommit.oid: 3e1a7fd9d28db65221c123960a0c85b62d93949a`, `baseRefName: main` |
| 5 | Session branch starts at the base | `git rev-parse HEAD` on `arena/c4b63a26-waterjet-sentinel-suite` | `3e1a7fd9d28db65221c123960a0c85b62d93949a` — **exact match** |
| 6 | Index clean | `git status --porcelain=v1` | empty output |
| 7 | Worktree clean | `git status` | `nothing to commit, working tree clean` |
| 8 | Authoritative workbook present | `find . -name T8_IO_Card_Mapping.xlsx` | `./T8_IO_Card_Mapping.xlsx` |
| 9 | Workbook SHA-256 exact | `sha256sum` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — **exact match** |
| 10 | No conflicting open PR | `gh pr list --state open` | `[]` — none open |

**The standing sandbox recreation protocol was not triggered.** Local `HEAD`, the branch ref,
the index and the worktree already agreed with the approved remote-main base, so no
`update-ref`, `read-tree` or recovery operation was required, and no destructive Git operation
was used. The parent-commit fetch in check 2 was a read-only ancestry proof: it moved no
branch ref and touched neither the index nor the worktree. (Note: this sandbox holds a
shallow clone whose boundary sits at the merge commit itself, which is why the parents were
fetched explicitly for the ancestry proof.) No recovery approval was needed.

---

## 1. Protected decisions and Owner positions carried forward

The Stage 0.4C-0 Owner decisions are authoritative and were **not** reopened, softened or
reinterpreted in this Stage. The Stage 0.4C-0B authorising brief adds new Owner-confirmed
positions; they are recorded below **exactly as brief-stated**, as the design basis for this
Stage only — brief-stated, not drawing-verified, not hardware-verified.

| Decision | Carried forward as |
|---|---|
| **OQ-01** TC topology gap | Still open as an *evidence* gap: `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`, `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. The 212 TC Channels are **not** mapped to `AI-001`–`AI-035`; no TC module position, Channel order or A/B placement is inferred; the workbook is not modified |
| **OQ-02** Two domains | **Domain A** `PROCESS_INSTRUMENT_COMMISSIONING` and **Domain B** `TEMPERATURE_SENSOR_COMMISSIONING` stay separate and are never merged |
| **OQ-03** AI-003 | `PUMP_OUTLET`, Pump Outlet Pressure, 4–20 mA, 0–40 bar, the sole pre-P1 Pump-ready pressure source. Unchanged |
| **OQ-04** Quality model | The `Quality` enum (`GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED`) is **unchanged**. `DiagnosticReason` and `CommissioningState` remain **separate proposed** vocabularies. `INVALID` is a presentation concept only. **No Product enum was modified in this Stage** |
| **OQ-05** Pilot population | No actual Sensor ID chosen; roles only; actual IDs `OWNER_INPUT_PENDING` |
| **OQ-06** Timing values | Staleness timeout, recovery stabilisation and batch size stay `CONFIGURABLE / OWNER_INPUT_PENDING`. No production default authorised. The §6 configuration-model timeout fields are shape-only; no value is proposed |
| **OQ-07** Sensor verdict | PASS / PARTIAL / FAIL / NOT_TESTED / BLOCKED per the recorded rules; no averaging of A/B Channels to hide a failed Channel |
| **OQ-08** Procedure authority | Author: Commissioning / Instrument responsible role. Safety approval: Owner-authorised site safety role. Execution: authorised commissioning personnel. Final technical acceptance: Owner |
| **0.4C-0B brief §2** Six-node physical baseline | **Owner-confirmed design basis**: 6 TC cabinets/nodes, each `750-362 × 1 + 750-498 × 5 + 750-600 × 1`; 240 physical Channels; 106 Sensors × 2 measurement Channels = 212 required; 28 remaining. Recorded in §2. **Not drawing-verified, not hardware-verified** |
| **0.4C-0B brief §3** Test IP configuration | **Owner-authorised test values only** (`192.0.2.11`–`192.0.2.16`, TEST-NET-1 documentation range): configuration-driven, never identity, never production. Recorded in §3 |
| **0.4C-0B brief §4** Drawing authority | Drawings are the mapping authority; status `DRAWING_PENDING_REVISION`; baseline `BASELINE_REV1`; future revisions supersede via an explicit revision record. No drawing file was available, so nothing was derived (§4) |
| **0.4C-0B brief §7** Factory-default basis | `OWNER_CONFIRMED_FACTORY_DEFAULT` for all 30 module instances as Baseline rev1 — recorded as a **configuration-basis decision**. No default value was read from any manual (§4.2) |

---

## 2. Six-node TC topology baseline rev1 (Owner-stated design basis)

Source: the authorising brief §2, treated as an authoritative Owner decision per
AGENTS.md authority order. Every fact below is **brief-stated**: usable as the design basis
for this Stage, and **not** verified against any drawing, manual, nameplate or hardware.

### 2.1 Per-cabinet topology (identical for all six nodes, Owner-confirmed)

| Node | Head station | TC input modules | End module | Network endpoint |
|---|---|---|---|---|
| TC-NODE-01 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |
| TC-NODE-02 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |
| TC-NODE-03 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |
| TC-NODE-04 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |
| TC-NODE-05 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |
| TC-NODE-06 | 750-362 × 1 | 750-498 × 5 | 750-600 × 1 | one configurable IP endpoint |

Totals: **750-362 × 6, 750-498 × 30, 750-600 × 6, 6 TC network endpoints.**
Per-module capacity (brief-stated): **8 TC input Channels per 750-498**.

### 2.2 Capacity arithmetic (computed from the brief-stated facts, not asserted)

| Quantity | Derivation | Value |
|---|---|---|
| Physical Channels | 6 nodes × 5 modules × 8 Channels | **240** |
| Required measurement Channels | 106 Sensors × 2 measurement Channels | **212** |
| Remaining Channels | 240 − 212 | **28** |

The 106-Sensor / 2-Channels-per-Sensor requirement is consistent with the
repository's authoritative Sensor topology (108 slots minus the two `NON_SENSOR_GAP`
positions `I7` and `I16`; `ThermocoupleChannelCount` 212 — re-confirmed by the
untouched template in §5). No conflict was found between the brief-stated requirement
and the canonical topology.

**The 28 remaining Channels are unscheduled, not commissioned spares.** Per the brief,
they must not be reinterpreted as spares until the drawing-derived schedule proves which
Channels are actually unused — and no schedule exists (§4).

### 2.3 Proposed stable identities (brief §6, design proposal)

Node, module and Channel identities are independent of IP addresses. The brief's
recommended scheme is adopted unchanged as the proposal:

- Nodes: `TC-NODE-01` … `TC-NODE-06`
- Modules: `TC-MODULE-<node>-<module>`, e.g. `TC-MODULE-01-01` … `TC-MODULE-06-05`
- Channels: `TC-CHANNEL-<node>-<module>-<channel>`, e.g. `TC-CHANNEL-01-01-01` … `TC-CHANNEL-06-05-08`

These identities are **proposed labels** recorded in documentation. They are not
configuration, not a mapping, and not evidence that any cabinet exists. Changing an IP
address must never change them or any future Sensor-to-Channel mapping.

---

## 3. Six-node configurable test-IP baseline (TEST ONLY, non-production)

Source: the authorising brief §3. These are **Owner-authorised test configuration
values**. They are not production values, they do not authorise `TEST_HARDWARE`, and no
live connection is authorised in this Stage.

| Node | Test IP (brief-stated) |
|---|---|
| TC-NODE-01 | `192.0.2.11` |
| TC-NODE-02 | `192.0.2.12` |
| TC-NODE-03 | `192.0.2.13` |
| TC-NODE-04 | `192.0.2.14` |
| TC-NODE-05 | `192.0.2.15` |
| TC-NODE-06 | `192.0.2.16` |

Brief-stated requirements, recorded as binding on any future use:

1. IP addresses are configuration-driven; they are never hard-coded into Product logic.
2. An IP address is never the identity of a Sensor, Channel or Cabinet (§2.3 identities apply).
3. Changing an IP must not change any Sensor-to-Channel mapping.
4. One node communication fault must not invalidate the other five nodes.
5. These values are **not** authorised production IPs. Their presence authorises nothing.

**Address-range note.** `192.0.2.0/24` is the TEST-NET-1 documentation range: reserved,
non-routable, and not a plant subnet. Recording these Owner-authorised test values in
narrative documentation is consistent with the public-repository boundary (documentation
may discuss address shapes; production addresses are prohibited).

**Why no separate configuration example file was created.** The brief's configuration
example is explicitly *optional*, and repository governance decides it: the boundary
scanner rule **S2** flags *any* non-loopback IPv4 literal inside the Product tree
(which includes `config/`), with no exception for documentation ranges — the scanner's
own rule comment states that narrative documentation may discuss address shapes while
the Product tree must never contain them. A `config/examples/` file holding these IPs
would therefore break the S1–S9 clean gate. The baseline above, together with the
configuration-model shape in §6, is the complete test-IP baseline record for this Stage;
no second, unsynchronised surface was created.

---

## 4. Evidence availability and STOP record (brief §§4–5, 7–8, 12)

### 4.1 Availability inventory — what was searched and what was found

| # | Expected evidence | Search method (observed tool output) | Result |
|---|---|---|---|
| D-01 | Six-cabinet TC drawings | Workspace-wide `find` for `*.pdf`, `*.dwg`, `*.dxf`, `*drawing*`, `*.png`, `*.jpg`, `*.jpeg`, `*.tif`, `*.tiff`, `*.bmp`; tracked-file review via `git ls-files` | **Zero hits. `NOT_AVAILABLE_IN_SESSION`.** No drawing file of any name or format exists in the sandbox. No drawing filename, title, sheet list or revision is stated in the brief or in any predecessor record |
| M-01 | 750-498 manual `m07500498_00000000_0en.pdf` | Same search, plus `*0750*`, `*wago*`, `*m0750*` filename search | **Absent. `NOT_AVAILABLE_IN_SESSION`** |
| M-02 | 750-362 manual (filename unspecified in the brief) | Same search | **Absent. `NOT_AVAILABLE_IN_SESSION`** |
| M-03 | 750-600 manual (filename unspecified in the brief) | Same search | **Absent. `NOT_AVAILABLE_IN_SESSION`** |
| W-01 | Authoritative workbook `T8_IO_Card_Mapping.xlsx` | `find` + `sha256sum` + `stat` | **Present.** 16648 bytes, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — exact match, unchanged |

`NOT_AVAILABLE_IN_SESSION` means: the brief asserts the Owner holds this evidence, but no
file was visible in this sandbox, so no filename, hash, size, title, revision, scope or
review status could be recorded. For gate purposes it is equivalent to `NOT_PROVIDED`.
No hash from memory or from a prior session was reused. Full record:
[TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md).

### 4.2 STOP record

| Gate | Rule | Outcome |
|---|---|---|
| Brief §4 (drawings) | *"If a Drawing is unavailable in the sandbox: STOP, report the exact missing file"* | **STOPPED.** Zero schedule rows derived; no `docs/evidence/` CSV created; the exact missing-file report is §2 of the issue register. No field was "left unresolved" — there was no drawing content at all, so there is nothing to attach a page reference to |
| Brief §§7–8 (manuals) | *"Read the actual Manual before recording claims"* | **STOPPED.** No manual section, page, default value, encoding, ordering rule, width, offset basis or status-byte determination was read, recorded or cited. §§7–8 below carry predecessor citations only |
| Brief §12 (capacity) | *"If Drawing-derived used Channels differ from 212: STOP, report the exact difference"* | **STOPPED and reported.** Drawing-derived used Channels = **0**; difference from 212 = **212 unscheduled measurement identities**. Nothing was forced or auto-filled |
| Brief §14 (scope) | *"If Product source becomes necessary: STOP"* | **Not reached.** No Product source became necessary; none was touched |

Per AGENTS.md §11.9 (*Evidence unavailable*), only the affected workstreams were stopped;
the unaffected documentation — this checkpoint, the issue register, the evidence-register
update, the configuration-model and verification-plan proposals — was completed and is
reported here.

### 4.3 Predecessor evidence position (cited, not re-verified)

The following predecessor facts bound what may be claimed without the missing files:

- **750-498: no profile exists anywhere in the repository.** `getProfile('750-498')`
  returns `null` (observed via `node -e` import of `moduleProfiles.mjs` in this session);
  the eight known models are the Domain A set (750-362, 750-601, 750-430, 750-530,
  750-613, 750-471, 750-554, 750-600). No Channel count, width, encoding or default in
  this repository is attributable to a 750-498 source.
- **750-362 / 750-600: model-level `PROVIDED_UNVERIFIED` facts only** (Stage 0.4B-3
  profiles, Stage 0.4C-0 EV-02 — themselves recorded from manuals outside the
  repository, unhashed): the 750-362 is a head station with zero process Channels that
  defines the node and process-image rules; the 750-600 terminates the internal bus and
  contributes no process data. The head-station process-image ordering rule, the
  alignment and word/byte packing rule, and the per-Channel offset rule are all listed
  as **missing** (`COMMON_MISSING`). Nothing in the predecessor record determines the
  §8 questions (ordering, grouping, alignment, status-byte inclusion, widths).
- **Domain A candidate image stays `CANDIDATE_UNVERIFIED`** with every offset
  `OFFSET_UNRESOLVED`; it is not a Domain B source in any case.

---

## 5. Drawing-derived schedule — NOT DERIVED (0 rows)

No Sensor-to-Channel schedule was derived in this Stage. The §5 validation rules from the
brief are recorded here as the **acceptance rules for the future deriving session**, with
this session's result against each:

| # | Brief §5 validation rule | Result this session |
|---|---|---|
| 1 | Exactly 106 physical Sensor identities expected | No rows derived — **not executable** |
| 2 | Exactly 212 A/B measurement identities expected | No rows derived — **not executable** |
| 3 | `I7` and `I16` must not appear as Sensors | Vacuous (zero rows); the intake template still excludes both (§5.1) |
| 4–5 | Each measurement / physical Channel maps at most once | Vacuous (zero mappings; no duplicate possible) |
| 6–8 | Cabinet 1–6, Module 1–5, Channel 1–8 | No rows to range-check — **not executable** |
| 9 | No A/B side may silently swap | No sides assigned — **not executable** |
| 10 | Every unresolved drawing field stays explicit | Satisfied structurally: with no drawing content, every drawing-derived field in the baseline reads `NOT_AVAILABLE_IN_SESSION` |
| 11–12 | Unused Channels reported; used + unused = 240 | **Blocked.** "Unassigned" is meaningful only against a derived schedule; with zero rows every physical Channel is unscheduled, not unassigned (§10) |

The brief's schedule-state vocabulary (`DRAWING_VERIFIED_REV1`, `DRAWING_AMBIGUOUS`,
`DRAWING_MISSING_IDENTITY`, `DRAWING_DUPLICATE_MAPPING`, `DRAWING_UNREADABLE`,
`RESERVED_UNASSIGNED`, `FIELD_VERIFICATION_PENDING`) is recorded for the future deriving
session. **Zero rows carry any of these states**, because zero rows exist. In particular,
no row was marked `RESERVED_UNASSIGNED`: the 28 remaining Channels stay unscheduled per §2.2.

### 5.1 Intake template re-verified untouched

The Stage 0.4C-0A template
[`templates/sensor-to-tc-channel-schedule.template.csv`](templates/sensor-to-tc-channel-schedule.template.csv)
was **not modified** in this Stage (see §12 allowlist proof). Re-checked read-only at this
head: **28 columns, 212 data rows**, zero rows starting with `I7,` or `I16,`. It remains an
evidence-intake template with every acquisition column `NOT_PROVIDED` — still not a mapping.
The commissioning-record template is likewise untouched (26 columns, header only).

---

## 6. Configuration and identity model (design proposal — brief §6)

This Stage defines the configuration **shape** only. No endpoint is connected to, no polling
is added, and no configuration file was created (§3 records why the optional example file
was declined).

Each of the six node configurations must support the following fields (brief-stated):

`SourceId`, `DisplayName`, `Enabled`, `IPAddress`, `Port`, `ConnectTimeout`,
`RequestTimeout`, `PollInterval`, `ReconnectDelay`, `StaleTimeout`,
`ExpectedTopologyRevision`, `ConfigurationRevision`.

Binding rules carried from the brief:

1. `SourceId` is the stable node identity (`TC-NODE-01` … `TC-NODE-06`, §2.3) and is
   independent of `IPAddress`.
2. `ExpectedTopologyRevision` pins the documented topology baseline the node is verified
   against; at this head the only baseline is the brief-stated rev1 in §2, so no node can
   yet pass a topology-revision check.
3. `Enabled` gates acquisition per node; one node's fault — communication or otherwise —
   must not invalidate the other five nodes.
4. All timeout/interval fields stay value-free in this Stage: per OQ-06 they are
   `CONFIGURABLE / OWNER_INPUT_PENDING`, and no default is proposed here.

---

## 7. Process-image profile — NOT ESTABLISHED (manuals absent)

The brief §8 question set (module ordering rule, analog/specialty ordering, input/output
grouping, alignment, register ordering, zero-based offset basis, status-byte inclusion for
the 750-498, per-module and per-Channel widths, node input/output sizes, read-only register
area, firmware requirements) **cannot be answered in this session**: the 750-362 and 750-498
manuals are `NOT_AVAILABLE_IN_SESSION` (§4.1), and the predecessor record explicitly lists
the ordering/alignment/offset rules as missing (§4.3).

Recorded for the future profile-deriving session:

- The required node order is brief-stated as `750-362 → 750-498 × 5 → 750-600`; whether the
  documented cyclic image follows that order, and with what grouping and alignment, is
  **undetermined**.
- The brief's vocabulary is adopted for that session: a manual-derived layout is
  `DOCUMENTED_FACTORY_DEFAULT_BASELINE` / `DOCUMENTED_BASELINE_REV1`, always paired with
  `FIELD_VERIFICATION_PENDING` / `NOT_HARDWARE_VERIFIED`, and is never confused with
  `FIELD_VERIFIED_ACTUAL_NODE`.
- If the manuals do not uniquely determine status-byte inclusion, both documented
  candidates must be recorded with the unresolved selector identified — no assumption.
- Read-only access is the only access ever in scope; output-image capacity, if any, never
  authorises output writes.

**Every Domain B address remains without a documented offset.** `ADDRESS_UNRESOLVED` is
retained **because the manuals are absent** — this is not the case the brief warns against
(preserving `ADDRESS_UNRESOLVED` merely for lack of hardware testing). No address/offset
manifest was created; there is no documented baseline to manifest.

---

## 8. Factory-default TC decoding contract — NOT ESTABLISHED (manual not read)

The documented decoding contract of brief §10 (signed value, °C conversion, invalid/raw
representations, explicit Channel status, wire-break/overrange/underrange/disabled
representations, diagnostic priority, last-valid-value and recovery behaviour) **cannot be
defined as documented fact in this session**: it must be based on manual evidence, and the
750-498 manual is `NOT_AVAILABLE_IN_SESSION`.

What stands instead, unchanged:

- The Stage 0.4C-0A §6 **wire-break decision-tree design proposal** (explicit diagnostic →
  `BAD`/`WIRE_BREAK`; documented substitute code → only after exact module/configuration
  verification; below-range without diagnostic → `UNCERTAIN`/`SUSPECTED_WIRE_BREAK`;
  transport loss → `BAD`/`COMMUNICATION_LOSS`; stale → `STALE`; out-of-range →
  `BAD`/`OUT_OF_RANGE`, distinct from wire break). It remains a **design position, not
  implemented, not verified against any manual**.
- The brief §10 protected rules are recorded as **requirements the future contract must
  satisfy** (invalid current data → `EngineeringValue` null, never silent 0 °C;
  `LastValidValue` historical only; explicit wire break → `BAD`/`WIRE_BREAK`/
  `IsCurrentValueValid` false; raw invalid code without explicit status → never automatic
  `WIRE_BREAK`; communication loss, stale and out-of-range stay separate; no current
  invalid value feeds Queue or Cleaning classification) — not as verified behaviour.

**No Product enum was modified in this Stage** (OQ-04). `WIRE_BREAK_DECODING: BLOCKED`.

---

## 9. Representative-node TEST_HARDWARE verification plan (future proposal — brief §11)

**Not executed. Not authorised.** The fifteen verification checks and nine STOP conditions
below are the brief §11 plan, recorded as a **proposal for a future Owner-authorised
TEST_HARDWARE session**. Every reference to "the documented baseline" is contingent on the
manuals first being ingested (§§7–8): at this head those baselines do not exist.

Future checks (one representative node before rollout):

1. 750-362 identity and firmware verified.
2. Module order equals the drawing baseline.
3. Five 750-498 modules present.
4. 750-600 terminates the node.
5. Cyclic input-image length equals the documented baseline.
6. Channel ordering matches derived offsets.
7. One normal connected Channel reads a plausible temperature.
8. The WAGO engineering-tool value agrees with the WJSS raw and engineering values.
9. An Owner-authorised open circuit produces the documented raw/status result.
10. WJSS classifies it as `BAD`/`WIRE_BREAK` only when explicit status supports it.
11. Reconnect clears the diagnostic.
12. Recovery passes the stabilisation rule.
13. Other nodes remain unaffected.
14. Disconnecting one node affects only Sensors assigned to that node.
15. No write request is transmitted.

Future STOP conditions: firmware incompatible; topology mismatch; input-image length
mismatch; status/value representation mismatch; duplicate mapping; incorrect Channel
identity; unexpected output data; any attempted write; unauthorised network reachability.

No connection, poll, loop test or hardware interaction was performed or authorised in this
Stage. `TEST_HARDWARE` remains **not authorised**.

---

## 10. Schedule and capacity report (brief §12)

| Item (brief §12) | Value at this head |
|---|---|
| Total physical Channels | **240** (brief-stated: 6 × 5 × 8; §2.2) |
| Drawing-used Channels | **0** — no schedule derived (§4.2 STOP) |
| Drawing-unassigned Channels | **Cannot be reported** — meaningful only against a derived schedule |
| Expected required Channels | **212** (brief-stated: 106 × 2; §2.2) |
| Missing Sensor measurements | **Cannot be computed** — all 212 expected identities are unscheduled (absence of evidence, not per-identity findings) |
| Duplicated Sensor measurements | None (vacuous — zero mappings) |
| Duplicated physical Channels | None (vacuous — zero mappings) |
| Unresolved drawing entries | **Not applicable as entries** — there is no drawing content; the baseline-level unresolved state is `NOT_AVAILABLE_IN_SESSION` for all drawing evidence |
| Per-node used / free summary | **Blocked** — no per-node assignment exists |
| Per-module used / free summary | **Blocked** — no per-module assignment exists |
| Used + unused = 240 check | **Not executable** — nothing is classified used or unused |

**§12 STOP recorded, not forced.** Drawing-derived used Channels (0) differ from 212 by the
full 212 unscheduled identities. The result was not forced to 212, no Channel was
auto-filled, and the 28 remaining Channels were not relabelled as spares.

---

## 11. Stage 0.4C-1 entry criteria — still 10 of 11 NOT MET

Re-checked against the Stage 0.4C-0A §9 criteria. The Owner statements in this Stage's brief
narrow what remains to be evidenced but satisfy no criterion: a brief statement is not a
verified device instance, schedule, configuration or transport.

| # | Entry criterion | Evidence that satisfies it | Status at this head |
|---|---|---|---|
| 1 | Actual TC acquisition hardware identified | `REQ-TC-001` at `DEVICE_INSTANCE` or better | **NOT MET** — models stated (`PROVIDED_UNVERIFIED`, model level), no device instance evidenced |
| 2 | Complete rack / device topology known | `REQ-TC-002` at `COMPLETE_RACK` | **NOT MET** — counts stated, no rack evidenced as a whole |
| 3 | Complete 212-Channel schedule received | `REQ-TC-004`, `REQ-TC-005`, `REQ-TC-006` at `COMPLETE_SENSOR_SCHEDULE` | **NOT MET** — 0 rows derived |
| 4 | TC type and CJC configuration known | `REQ-TC-007`, `REQ-TC-008` | **NOT MET** — default values unread (manual absent) |
| 5 | Transport and process representation known | `REQ-TC-009`, `REQ-TC-010` | **NOT MET** — test endpoint IPs stated; protocol, encoding, widths and image absent |
| 6 | No unresolved contradictory mapping evidence | No `CONFLICTING_EVIDENCE` row open | **MET** (still only vacuously — no mapping evidence exists at all; must be re-checked when evidence arrives) |
| 7 | Owner approves the exact read-only acquisition scope | Explicit Owner Scope Gate | **NOT MET** |
| 8 | Owner approves the `TEST_HARDWARE` Scope Gate | Explicit Owner Scope Gate | **NOT MET** |
| 9 | Disconnect / reconnect behaviour specified | `REQ-TC-014` | **NOT MET** |
| 10 | No-write proof designed | A design that proves no output, coil, register or device write path exists | **NOT MET** |
| 11 | Rollback and stop conditions defined | Documented rollback and stop conditions approved by the Owner | **NOT MET** |

```
STAGE_0.4C-1_TC_ACQUISITION: BLOCKED      (P0 evidence: 8 NOT_PROVIDED, 2 PROVIDED_UNVERIFIED-as-statement-only)
WIRE_BREAK_DECODING:         BLOCKED      (all P1 evidence NOT_PROVIDED)
ROLLOUT:                     BLOCKED      (all P2 evidence NOT_PROVIDED)
```

---

## 12. Changed / Unchanged / Not Verified

### CHANGED

| File | Purpose |
|---|---|
| `docs/STAGE_0.4C-0B_CHECKPOINT.md` | **New.** This Stage's checkpoint record: base-gate proof, protected decisions, Owner-stated six-node topology and test-IP baselines, evidence-availability inventory with the §§4/12 STOP record, schedule/process-image/decoding status (all not established, with reasons), configuration-model and verification-plan proposals, capacity report, entry-criteria re-check, validation |
| `docs/TC_DRAWING_ISSUE_REGISTER_REV1.md` | **New.** Drawing schedule issue register for `BASELINE_REV1`: expected-evidence intake, `NOT_AVAILABLE_IN_SESSION` record, exact missing-file STOP report, baseline consequences, revision rule, Owner next actions |
| `docs/TC_EVIDENCE_SOURCE_REGISTER.md` | Stage 0.4C-0B review: brief-stated Owner statements recorded as `PROVIDED_UNVERIFIED` (TCEV-12 … TCEV-16; `REQ-TC-001`/`REQ-TC-002` with statement-only scope); drawings and manuals recorded `NOT_AVAILABLE_IN_SESSION`; Category A/B statement supersessions; §9.4 counts; new §12 addendum |
| `docs/CURRENT_STATE.md` | Stage 0.4C-0B current position added at the top of the status block and as validation record §12.36; the Stage 0.4C-0A block is labelled historical (and PR #17 noted MERGED) |
| `docs/MASTER_PLAN.md` | Stage 0.4C-0B ledger entry added at the top of §3 and as new §3.8; the Stage 0.4C-0A entry is marked merged |
| `CHANGELOG.md` | New Stage 0.4C-0B entry at the top |

### UNCHANGED

- **Product source** — no file under `apps/`, `packages/`, `adapters/` or `spikes/` was created,
  modified or deleted. No `Quality` enum, no `DiagnosticReason`, no `CommissioningState`, no
  contract, no domain type, no adapter.
- **Executable tests** — no file under `tests/` was created, modified or deleted. No test was
  run.
- **Authoritative workbook** — `T8_IO_Card_Mapping.xlsx` was read only. SHA-256 unchanged.
- **Schedule and commissioning templates** — both files under `docs/templates/` are byte-identical;
  no acquisition column was filled.
- **Fixture data and generated fixtures** — `config/examples/` and every generated fixture are
  untouched. No configuration example was created (§3 records the governance reason).
- **Runtime, Mapping implementation, Galil implementation, MODBUS implementation** — none exists
  for TC acquisition, and none was added.
- **Dependencies and lock files** — `Directory.Packages.props`, `global.json`, every `.csproj`,
  every `package.json` and every lock file are untouched. No package was installed.
- **Production configuration** — untouched. No address, offset, threshold, timeout, setpoint or
  network identity was written anywhere outside this checkpoint's TEST ONLY narrative record.
- **CI workflows, release artifacts, build files** — untouched.
- **`README.md`** — untouched by decision; see §14.
- **The merged PR #17 branch** `arena/1cfd7e20-waterjet-sentinel-suite` — not checked out, not
  committed to, not pushed to.

### NOT VERIFIED

| Item | Why unverified |
|---|---|
| Six-node physical topology (cabinets, modules, endpoints) | Brief-stated Owner decision only; no drawing, nameplate, manual or hardware in the sandbox |
| Every Sensor-to-Channel A/B binding | `REQ-TC-004` `NOT_PROVIDED`; 0 rows derived; brief §4 STOP |
| Terminal schedule and wiring | `REQ-TC-005`, `REQ-TC-006` `NOT_PROVIDED`; drawings `NOT_AVAILABLE_IN_SESSION` |
| 750-498 Channel structure, widths, encoding, defaults | Manual `NOT_AVAILABLE_IN_SESSION`; no 750-498 profile exists in the repository |
| 750-362 cyclic process-image layout | Manual `NOT_AVAILABLE_IN_SESSION`; predecessor ordering/alignment/offset rules explicitly missing |
| Address / offset manifest | Not established — no documented baseline exists to manifest |
| TC type per Channel, CJC method and source | `REQ-TC-007`, `REQ-TC-008` `NOT_PROVIDED`; factory-default values unread |
| Channel configuration, scaling, filters, diagnostics | `REQ-TC-009`, `REQ-TC-011` `NOT_PROVIDED` |
| Transport, protocol, byte/word order, encoding | `REQ-TC-010` `NOT_PROVIDED`; only TEST ONLY endpoint IPs stated |
| **Explicit wire-break representation** | `REQ-TC-011` `NOT_PROVIDED`. `WIRE_BREAK_DECODING: BLOCKED` |
| Observed baseline, open-circuit and recovery values | `REQ-TC-012`–`REQ-TC-014` `NOT_PROVIDED`. No physical Sensor has been tested |
| Under-range, over-range, module-fault encodings | `REQ-TC-015` `NOT_PROVIDED` |
| Expected normal ranges, update cycle, stale threshold | `REQ-TC-016`–`REQ-TC-018` `NOT_PROVIDED`; per OQ-06 `CONFIGURABLE / OWNER_INPUT_PENDING` |
| Commissioning procedure, LOTO reference, approval holders | `REQ-TC-019`, `REQ-TC-020` `NOT_PROVIDED` |
| The §6 configuration model and §9 verification plan | Proposals only; untestable until manuals, drawings and an explicit `TEST_HARDWARE` gate exist |
| **Actual process-image offsets (Domain A)** | Still `OFFSET_UNRESOLVED`; unchanged from the predecessor position |
| Domain A addresses | Every address remains `ADDRESS_UNRESOLVED`; the candidate process image remains `CANDIDATE_UNVERIFIED` |
| `AI-020`–`AI-035` identities | Still carry no established identity; **not commissioned instruments** |
| All Galil items `GAL-01`–`GAL-13` | No Galil evidence in the repository or this sandbox |
| **Predecessor test evidence** | Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review PASSED are **predecessor evidence only, cited not re-run.** They were **not** executed at this Stage's head and are not claimed as results of this Stage |

---

## 13. Validation performed

Documentation-only validation, per AGENTS.md §10.1. No executable test was run, and none is
claimed.

| Check | Command / method | Result |
|---|---|---|
| Boundary scan S1–S9 | `node tools/boundary-scan/boundary-scan.mjs .` | See the final report for the post-change result (pre-change baseline at session start: **`0 findings (S1-S9 clean)`, exit 0**) |
| Whitespace / conflict markers | `git diff --check` against the PR range | See the final report for the exact command and output |
| Changed-file allowlist | `git diff --name-status` against the base | Only the 6 files listed under CHANGED (see the final report) |
| Product-source no-change proof | `git diff --name-only` filtered to `apps/`, `packages/`, `adapters/`, `spikes/` | **Empty** (see the final report) |
| Executable-test no-change proof | `git diff --name-only` filtered to `tests/` | **Empty** (see the final report) |
| Workbook SHA-256 proof | `sha256sum T8_IO_Card_Mapping.xlsx` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — unchanged |
| Template integrity (read-only) | Column/row count + `I7`/`I16` grep on the 0.4C-0A template | **28 columns, 212 data rows, 0 gap rows** — byte-identical, untouched |
| Capacity arithmetic | 6×5×8 = 240, 106×2 = 212, 240−212 = 28, recomputed (not copied) | **240 / 212 / 28** — matches the brief-stated values |
| Drawing-derived CSV validations (brief §16) | Structural / 212-row / duplicate / range / reference checks | **Not executable — correctly absent.** No drawing-derived CSV was created (§4.2 STOP); running row validations against zero rows would manufacture a vacuous pass, so none is claimed |
| Relative-link check | Every relative link in the new and changed documents resolved against the filesystem | See the final report for the per-link result |
| Clean worktree | `git status --porcelain` | Clean after commit (see the final report) |

**Explicitly not run:** no `dotnet build`, no `dotnet test`, no `npm test`, no Mapping package
test, no Mapping UI test. This Stage touched no code those runners execute, so a test run would
have executed no changed path. Recording predecessor evidence instead, per AGENTS.md §10.1 and
§10.5.

---

## 14. Scope observation reported, not acted on (AGENTS §2.3 / §11.2)

**`README.md` is stale again, and it was deliberately NOT modified.**

`README.md` is not in this Stage's expected file scope. Its status block still reads
*Current stage = Stage 0.4C-0A* and *PR #17 OPEN — NOT MERGED*, both now superseded:
Stage 0.4C-0A is merged (PR #17 `MERGED` at `3e1a7fd9`), and this Stage's checkpoint is
the current position.

Under AGENTS.md §2.2 an agent must not modify a file the current Stage Gate does not name.
No Owner authorisation for a narrow README correction exists for this Stage. The discrepancy
is therefore reported here rather than repaired silently, extending conflict **CF-07**
(Stage 0.4C-0A §10), which remains open awaiting the same Owner decision.

*Closeout addendum (2026-10-12): the above was the 2026-10-11 position. The
closeout instruction then explicitly authorised a narrow README current-status
update, which is the only README change made (see §16). Formal closure of CF-07
is left to the Owner at merge review.*

---

## 15. Related documents

- [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md) — drawing intake, missing-file STOP report, revision rule, review completion (§7)
- [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) — the evidence register and `REQ-TC` register (completion review §13, TCEV-17 … TCEV-20)
- [evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv) — derived 240-row REV1 schedule
- [evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md) — process-image profile + decoding contract
- [evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md) — narrow validation summary
- [templates/sensor-to-tc-channel-schedule.template.csv](templates/sensor-to-tc-channel-schedule.template.csv) — schedule template (unchanged)
- [templates/tc-commissioning-record.template.csv](templates/tc-commissioning-record.template.csv) — commissioning-record template (unchanged)
- [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) — predecessor checkpoint (template policy, decision tree, entry criteria)
- [STAGE_0.4C-0_CHECKPOINT.md](STAGE_0.4C-0_CHECKPOINT.md) — predecessor evidence gate, Owner decisions OQ-01 to OQ-08
- [CURRENT_STATE.md](CURRENT_STATE.md) — verified state and stage status
- [MASTER_PLAN.md](MASTER_PLAN.md) — stage ledger
- [PUBLIC_REPOSITORY_BOUNDARY.md](PUBLIC_REPOSITORY_BOUNDARY.md) — what may be published
- [SAFETY_BOUNDARY.md](SAFETY_BOUNDARY.md) — hardware safety boundary
- [../AGENTS.md](../AGENTS.md) — working contract, evidence-first rules, stop conditions

---

## 16. Evidence-review completion successor record (2026-10-12 closeout)

This section is the current completion record. Sections 0–15 remain the
preserved 2026-10-11 zero-evidence history. Same branch
`arena/c4b63a26-waterjet-sentinel-suite`, same base `main` `3e1a7fd9…`; PR #18
OPEN — NOT MERGED (do not merge).

### 16.1 Evidence-retrieval history (exact, preserved — not rewritten)

| Run | Result |
|---|---|
| Initial PR #18 run | `EVIDENCE_NOT_AVAILABLE_IN_SESSION` |
| SharePoint attempt | `SANDBOX_SHAREPOINT_TLS_EGRESS_BLOCKED`, `ZERO_BYTES_DOWNLOADED` |
| Google Drive public-link attempt | `GOOGLE_DRIVE_TLS_EGRESS_BLOCKED`, `ZERO_BYTES_DOWNLOADED` |
| Google Drive connector run | `OWNER_GOOGLE_DRIVE_CONNECTOR`, `FILE_BYTES_VERIFIED`, `DRAWING_REVIEWED`, `PRIMARY_MANUALS_REVIEWED`, `SCHEDULE_DERIVED`, `PROCESS_IMAGE_PROFILE_DOCUMENTED` |

Evidence package: `WJSS_TC_EVIDENCE_BASELINE_REV1.zip`, exactly 17345849 bytes,
SHA-256 `7B886950D7F5F665416241E4CB502CD1A5787E3581788C799077C062786AA23E`.
Drawing `T8_SDS_Cabinet_-Draft-7E.pdf`: `DRAWING_PENDING_REVISION`, baseline
`REV1`. Ephemeral bytes removed after review; archive and PDFs not committed.
No Connector IDs, Drive details or evidence-access metadata are recorded
anywhere in Git.

### 16.2 Accepted drawing-schedule totals (recalculated, not copied)

240 physical Channel rows: 200 verified, 12 `DRAWING_INTERNAL_CONFLICT`, 4
`DRAWING_PRODUCT_TOPOLOGY_CONFLICT`, 24 `RESERVED_UNASSIGNED`, 0 unresolved.
212 Product-compatible measurements across 106 Product-compatible Sensors.
216 mapped + 24 unmapped = 240.

Label-equivalence note (committed artefact vocabulary is preserved, not
rewritten): the schedule CSV's `DRAWING_TOPOLOGY_CONFLICT` ≡ the brief's
`DRAWING_PRODUCT_TOPOLOGY_CONFLICT`, and the CSV's `SPARE_UNMAPPED` ≡ the
brief's `RESERVED_UNASSIGNED`. Counts are identical; no calculation defect was
found, so the derived CSV, profile and validation summary are unchanged by this
closeout.

### 16.3 Owner ruling on I7 and I16 (binding)

I7-F, I7-R, I16-F, I16-R remain `DRAWING_PRODUCT_TOPOLOGY_CONFLICT`. They are
not valid Product Sensors: preserved as raw Drawing evidence, excluded from
Product-compatible mapping, `NOT_COMMISSIONED`,
`FIELD_RECONCILIATION_REQUIRED`. They are not restored as Sensors, not remapped,
not classified as Spare, not published into Runtime, and not used for Queue or
Cleaning logic. Product topology stays 106 Sensors / 212 measurement identities
with I7 and I16 as `NON_SENSOR_GAP`.

### 16.4 Drawing internal conflicts (binding)

The 12 internal-conflict rows remain `DRAWING_INTERNAL_CONFLICT`,
`OWNER_REVIEW_REQUIRED`, `FIELD_VERIFICATION_PENDING`. Both visible Drawing
claims and the page/sheet references are preserved per-row in the schedule CSV;
nothing was resolved from sequence, pattern or assumption.

### 16.5 Process-image ruling (Manual-supported)

Profile state: `DOCUMENTED_FACTORY_DEFAULT_BASELINE_REV1`. Per node: 5 ×
750-498, 40 TC input values, 40 input words, zero-based offsets 0–39, network
cyclic status bytes suppressed per the reviewed 750-362 primary manual, read
access documented through FC3/FC4.

Required state: documented = true, fieldVerified = false, hardwareReady =
false, writeAuthority = false, fieldVerificationPending = true. The offsets are
not Hardware-verified. No live communication is implemented.

### 16.6 Wire-break ruling (binding)

`WIRE_BREAK_MODEL_CAPABILITY`: `DOCUMENTED` (750-498 complete local-bus
representation). `WIRE_BREAK_EXPLICIT_STATUS_IN_CURRENT_MODBUS_CYCLIC_IMAGE`:
NOT AVAILABLE / NOT ESTABLISHED (750-362 cyclic profile suppresses status
bytes). `WIRE_BREAK_DECODING`: `BLOCKED`. A raw special value alone is never a
confirmed WIRE_BREAK. Explicit Wire Break, disabled Channel, underrange,
overrange, communication loss and stale data stay separate future
classifications. No Product enum or Product code was modified.

### 16.7 Current Stage 0.4C-0B state

`EVIDENCE_REVIEW_COMPLETE`. `FIELD_VERIFICATION_PENDING`.
`STAGE_0.4C-1_BLOCKED`.

Entry-criteria consequence: criteria satisfied by drawing/manufacturer evidence
advance only as documented in the source register (§13; `REQ-TC-001` … `REQ-TC-007`
drawing/manufacturer-verified, `REQ-TC-008` … `REQ-TC-020` stay `NOT_PROVIDED`);
no criterion is satisfied by configuration, observation or hardware. Stage
0.4C-1 remains BLOCKED. Physical Sensor acquisition NOT IMPLEMENTED. Hardware
loop test NOT EXECUTED. No live Modbus. No TEST_HARDWARE. No Production. No
device write/control.

### 16.8 Artefact pointers

- [evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv)
- [evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md)
- [evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md)
- [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md)
- [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md)
