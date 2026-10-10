# Stage 0.4C-0 — Physical Sensor Readiness, Loop-Test Requirements and Actual I/O Evidence Gate (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT — documentation and contract proposal only, SUBMITTED FOR OWNER REVIEW, with Owner decisions OQ-01 to OQ-08 recorded in this successor revision (§0.1).** Nothing is implemented, tested on hardware, commissioned or merged. **Physical TC acquisition: BLOCKED BY MISSING TC TOPOLOGY AND SENSOR-TO-CHANNEL SCHEDULE.** Stage 0.4C-1 TC acquisition: **BLOCKED.** **PR #16 OPEN — NOT MERGED.**

Stop point: this checkpoint. The Owner merges; the agent does not.

**Honesty lines:** NEW SESSION USED. APPROVED REMOTE-MAIN BASE VERIFIED. TC ACQUISITION TOPOLOGY NOT PROVIDED. SENSOR-TO-TC-CHANNEL SCHEDULE NOT PROVIDED. STAGE 0.4C-1 TC ACQUISITION BLOCKED. PHYSICAL SENSOR ACQUISITION NOT IMPLEMENTED. WIRE-BREAK DECODING NOT IMPLEMENTED. SENSOR COMMISSIONING MONITOR NOT IMPLEMENTED. ACTUAL PROCESS-IMAGE OFFSETS NOT VERIFIED. AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor has been tested. Candidate addresses remain non-authoritative. PR #16 OPEN - NOT MERGED.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0 |
| Session branch (platform-assigned, fixed) | `arena/fecda529-waterjet-sentinel-suite` |
| Development checkpoint (first commit) | `4babdce4c53921f4c8c96073ebafa7d73e6ac07e` |
| Owner-decision successor commit | Introduced by the commit whose subject is `docs: resolve Stage 0.4C-0 owner decisions`. Its SHA is recorded in PR #16 and in the final report, not inside its own commit (AGENTS §4.6). |
| Approved remote-main base | `main` = `29ee137c1df617a1aa3321c3c891161d5a5bb41a` (PR #15 merge commit) |
| PR #15 merge parents | `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` and `8b479772d4fbef63b8f8eb34f59a1f2eba69618e` |
| PR #15 state | `MERGED` at `2026-10-10T18:21:35Z` |
| PR #16 | `arena/fecda529-waterjet-sentinel-suite` → `main`, OPEN, NOT MERGED |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — unchanged |
| Scope touched by the successor commit | `docs/STAGE_0.4C-0_CHECKPOINT.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md`, `README.md` (narrow current-status rows only), and the two stale `DOMAIN_MODEL.md` anchor links in `docs/CURRENT_STATE.md` and `docs/MASTER_PLAN.md` |

## 0. Gate outcome and decision status

- **Base gate: PASSED** (§1 of the first revision, re-verified at the start of the successor revision: remote `main` `29ee137`, session branch remote head `4babdce`, local HEAD `4babdce`, index and worktree clean, PR #16 OPEN, workbook SHA unchanged). The standing sandbox recreation protocol was not triggered.
- **TC topology: CONFIRMED EVIDENCE GAP (OQ-01).** The workbook does not contain the acquisition topology for the 106 Sensors and 212 thermocouple Channels. Owner decision: `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`, `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`, `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`.
- **Two commissioning domains (OQ-02).** Process Instrument Commissioning and Temperature Sensor Commissioning are kept separate and are not merged into one inventory.
- **AI-003 identity closed (OQ-03).** Pump Outlet Pressure, the only pre-P1 Pump-ready pressure source. No Main Valve pressure identity exists.
- **Quality model (OQ-04).** The existing `Quality` enum is unchanged. Diagnostic reason and commissioning state are separate proposed vocabularies.
- **Sensor verdict, pilot roles, timing values and procedure authority (OQ-05 to OQ-08)** are recorded in §4, §8, §12 and §13.
- **Nothing was implemented.** No Product source, executable test, workbook, dependency, fixture, lock file, Runtime, Mapping implementation, Galil or MODBUS code was changed.

### 0.1 Owner decisions recorded

| Decision | Owner ruling (recorded verbatim in substance) | Where applied |
|---|---|---|
| OQ-01 TC topology | Confirmed evidence gap. Do not map TC Channels onto the 35 analog Channels; do not infer TC module identity, TC Channel order or Sensor order; do not add TC modules to the workbook; do not modify the workbook; no physical TC acquisition. Keep TC acquisition on another rack, controller or network as a separate acquisition source. | §2, §3, §4 (CF-01), §5.5, §11, §15 |
| OQ-02 Existing analog channels | Workbook AI-001 to AI-035 are not the 212 TC Channels by default. Two domains: A. Process Instrument Commissioning (workbook DI/DO/AI/AO, identities only as explicit in the workbook or Owner rulings); B. Temperature Sensor Commissioning (106 Sensors, 212 TC Channels, blocked until OQ-01 evidence). Do not merge. | §4 (CF-02), §5.5 |
| OQ-03 AI-003 | AI-003 remains Pump Outlet Pressure, PUMP_OUTLET, 4–20 mA, 0–40 bar, the only Pump-ready pressure source before P1. The workbook text "Pressure Transmitter Main Valve Outlet" is source text and evidence only. It creates no Main Valve identity. `MAIN_VALVE_OUTLET_PRESSURE` remains absent. Not reopened without new physical evidence. | §2.5, §4 (CF-03) |
| OQ-04 Quality model | No change to the `Quality` enum. Add a separate proposed `DiagnosticReason` and a separate `CommissioningState`. INVALID is a presentation concept, not an enum value. Mappings per §6.3. | §6 |
| OQ-05 Pilot population | No actual Sensor ID is chosen until the Sensor-to-Channel schedule exists. Selection roles only. Actual IDs: `OWNER_INPUT_PENDING`. | §12 |
| OQ-06 Timing and batch | Staleness timeout, recovery stabilisation and commissioning batch size: `CONFIGURABLE / OWNER_INPUT_PENDING`. No production default is authorised. Any test example is labelled `TEST_EXAMPLE_ONLY` and `NOT_PRODUCTION_APPROVED`. | §6.7, §13 |
| OQ-07 Sensor verdict | Sensor with two required Channels: PASS (all PASS); PARTIAL (at least one PASS and another NOT_TESTED or BLOCKED); FAIL (any FAIL); NOT_TESTED (none tested); BLOCKED (testing cannot begin). No averaging of A/B Channels to hide a failed Channel. | §8.4, §10.5 |
| OQ-08 Procedure authority | Procedure author: Commissioning / Instrument responsible role. Safety approval: Owner-authorised site safety role. Execution: authorised commissioning personnel. Final technical acceptance: Owner. No open-circuit instruction is authorised without the approved site procedure and LOTO controls. | §8.1 |
| README | Narrow current-status correction authorised. | §16 |
| Pre-existing links | Two stale anchors corrected to the canonical anchor, documentation only. | §16 |

## 1. Authoritative baseline carried forward (protected)

Items marked **Verified** were re-derived from the authoritative workbook by a read-only zip/XML parse (no installed packages). Items marked **Carried** come from repository records.

| Protected item | Value | Status |
|---|---|---|
| Logical positions | 108 (18 logical columns × 6 logical rows) | Carried (README, DOMAIN_MODEL §2.2.1) |
| Physical Sensor locations | 106 (Left 24, Rear 29, Right 24, Front 29) | Carried (PHY-001) |
| Thermocouple measurement Channels | 212 (TC_F and TC_R per Sensor; 48 + 58 + 48 + 58 by wall) | Carried (PHY-002, PHY-003) |
| I7 and I16 | `NON_SENSOR_GAP` placement anchors; I7 anchors WJ3, I16 anchors WJ1; never Sensors, never Water Jets | Carried |
| WJ1–WJ8 | Equipment entities, separate from the Sensor matrix; pair 1:1 with IV1–IV8 by ordinal | Carried (ADR-0017) |
| Legacy cannon data | Means Assigned Cleaning Device ID; never inferred from Sensor wall location | Carried |
| AI-002 | Pump Inlet Pressure; 4–20 mA; 0–40 bar; diagnostic and trend only; not Pump-ready; not Valve diagnosis; no alarm, trip or interlock authority | Carried (Owner ruling 2026-10-10) |
| AI-003 | Pump Outlet Pressure; PUMP_OUTLET; 4–20 mA; 0–40 bar; the only pre-P1 Pump-ready pressure source | Carried (Owner ruling; OQ-03 re-confirmed) |
| Rack modules | 23 (RackSlot 1–23) | **Verified** |
| Workbook rows | 114 data rows: 108 `USED`, 6 `SPARE` | **Verified** |
| Tagged channels | 104 (DI 37, DO 31, AI 35, AO 1); the 4 non-process modules carry no tag | **Verified** |
| Reserved unresolved rows | 18 tagged rows whose note reads "pending reply" | **Verified** |
| Non-process modules | 750-362, 750-601, 750-613, 750-600: no Process I/O address, `NOT_APPLICABLE` | **Verified** (rows); address status carried |
| Candidate process layout | 19 contributing modules and 110 channels (750-430 ×5 = 40, 750-530 ×4 = 32, 750-471 ×9 = 36, 750-554 ×1 = 2) | **Verified** (counted); `CANDIDATE_UNVERIFIED`, offsets null |
| Mapping defaults | 26 enabled default bindings, no seed; 18 reserved unresolved rows | Carried (Stage 0.4B-1 / 0.4B-3) |

## 2. Evidence inventory

### 2.1 Classification legend

Categories: `OWNER_WORKBOOK_FACT` · `PRIMARY_MANUFACTURER_MODEL_FACT` · `ACTUAL_RACK_INSTANCE_EVIDENCE` · `ACTUAL_CHANNEL_EVIDENCE` · `OWNER_STATEMENT` · `PROVIDED_UNVERIFIED` · `NOT_PROVIDED` · `CONFLICTING_EVIDENCE`.

`PROVIDED_UNVERIFIED` is a **state**: the Owner provided the item but its content cannot be checked in this sandbox. Each item has one category and one state.

### 2.2 Evidence items

| ID | Evidence | Category | Scope | Presence / hash | State and note |
|---|---|---|---|---|---|
| EV-01 | Authoritative workbook `T8_IO_Card_Mapping.xlsx` | OWNER_WORKBOOK_FACT | Complete rack topology (23 modules, RackSlot 1–23) and the process channel-tag list. Contains **no** thermocouple module, **no** Sensor identity, **no** TC Channel, **no** wiring, **no** process-image offset. | In repository. SHA-256 verified; unchanged. | Authoritative for topology and tags as recorded. Does **not** establish physical wiring, Channel configuration or offset. The note column holds field-wiring descriptions for some channels: workbook notes, not verified wiring. |
| EV-02 | Primary manufacturer manuals, eight models (750-362, 750-430, 750-530, 750-601, 750-613, 750-471, 750-554, 750-600) | PRIMARY_MANUFACTURER_MODEL_FACT | Model level only | Owner-provided outside the repository. **Not in sandbox.** No hash; none invented. | `PROVIDED_UNVERIFIED`. Model facts as recorded by Stage 0.4B-3: 750-430 eight input bits; 750-530 eight output bits; 750-471 four 16-bit input values with status/diagnostic representation dependent on Coupler and configuration; 750-554 two 16-bit output values. |
| EV-03 | Owner rack sequence screen (visible Pos. 01–20) | ACTUAL_RACK_INSTANCE_EVIDENCE | **Partial screenshot only.** 20 of 23 modules. No entry for 750-362, 750-601, 750-613. | Owner-provided outside the repository. Not in sandbox. No hash. | `PROVIDED_UNVERIFIED`. Positions are configuration-tool positions, not RackSlots. |
| EV-04 | Owner configuration screen: 750-471 at Pos. 10 (RackSlot 13, `AI-MODULE-01`), **Channel 3 only** | ACTUAL_CHANNEL_EVIDENCE | **INSTANCE_AND_CHANNEL_ONLY.** Not generalised to other Channels, instances or 750-554. | Owner-provided outside the repository. Not in sandbox. No hash. Values as recorded in `packages/mapping-config/src/actualRackEvidence.mjs`. | `PROVIDED_UNVERIFIED`. Recorded: signal type 4-20 mA; input filter Off; channel diagnosis On; wire-break diagnosis On; overload, measuring-range overflow and underflow, user-limit diagnoses On; user limits 32767 / −32768; displayed type `4AI U/I Diff Galv`; version `01.01.46(04)`. Does **not** show the open-circuit output representation, status byte layout or scaling result. |
| EV-05 | Owner statements: manuals available; actual rack positions available; one 750-471 Channel configuration available | OWNER_STATEMENT | Statements only | Not in repository | Verify nothing beyond EV-02 to EV-04. |
| EV-06 | Actual Process Data view or export (Input and Output process image, offsets, word or byte grouping, module positions, status and control fields) | NOT_PROVIDED | — | — | Required for any live process-instrument read. |
| EV-07 | Actual field-network mapping; head-station (750-362) firmware and hardware revision; I/O Config | NOT_PROVIDED | — | — | Required for the process-instrument transport and the head-station profile. |
| EV-08 | 750-471 Common settings | NOT_PROVIDED | — | — | Required per model instance. |
| EV-09 | 750-471 Scaling settings | NOT_PROVIDED | Only the user-limit values of EV-04 are visible; their role is not verified | — | Required for engineering conversion. |
| EV-10 | 750-471 Channel-configuration consistency across nine instances and 36 Channels | NOT_PROVIDED | Only one Channel (EV-04) verified | — | Required before any other 750-471 Channel is treated as configured. |
| EV-11 | 750-554 settings | NOT_PROVIDED | — | — | Required only where the output affects Sensor readiness (for example AO-001). |
| EV-12 | **Thermocouple acquisition topology** — Owner-required list below | **NOT_PROVIDED** (`TC_ACQUISITION_TOPOLOGY`) | — | — | See §3.1. The workbook has no TC module (CF-01). |
| EV-13 | Sensor-to-Channel schedule (`SENSOR_TO_TC_CHANNEL_SCHEDULE`); terminal schedule; wiring drawings | NOT_PROVIDED | — | — | Required for Loop-Test steps 3 and 4. |
| EV-14 | Expected normal values; observed value during disconnection; any commissioning record | NOT_PROVIDED | — | — | No measured value or observation of a physical Sensor exists in the repository or the sandbox. |
| EV-15 | Workbook column `Ref.` (15 distinct values, 1.26 to 1.79) | OWNER_WORKBOOK_FACT | Values only | In repository | **Meaning NOT_PROVIDED.** Not interpreted as a drawing reference. |
| EV-16 | Owner-confirmed physical structure: walls, 106 Sensor locations, 108 logical positions, TC_F and TC_R sides, I7 and I16 gap anchors | OWNER_STATEMENT | Logical and domain structure. Not a physical verification. | Repository (PHY-001 to PHY-003; `WallMap.cs`; DOMAIN_MODEL §2.2.1) | Domain-confirmed. Per-Sensor physical verification NOT_PROVIDED. |
| EV-17 | Owner rulings on pressure identities (AI-002, AI-003, AI-004 to AI-011 as IVn outlet pressure with the label rule `WSB Pressure transmitter #n = IVn`) | OWNER_STATEMENT, with the AI-003 source label recorded as CONFLICTING_EVIDENCE | Ruling governs identity in the product. Workbook text unchanged. | Repository (STAGE_0.4B-1); Mapping tests | The AI-003 workbook label is source text only (OQ-03). The AI-004 to AI-011 labels read "WSB Pressure transmitter #1–#8" (CF-05). |
| EV-18 | Product-tree Sensor and Channel model (synthetic examples only) | Not Owner evidence of installed hardware | Synthetic identities only | `config/examples/sensor-map.example.json` (labelled example) | Shows the shape of a record. Not a production map. |
| EV-19 | Owner decisions OQ-01 to OQ-08 (this successor revision) | OWNER_STATEMENT | Policy and procedure decisions | This checkpoint (§0.1) | Governs the design. Not evidence of any hardware, wiring or configuration. |

### 2.3 Facts by scope

| Fact | Scope | Source | Permitted use |
|---|---|---|---|
| 750-471 four-Channel analog-input model with four 16-bit input values | MODEL | EV-02 | Model profile only |
| 750-430 eight input bits; 750-530 eight output bits; 750-554 two 16-bit output values | MODEL | EV-02 | Model profile only |
| RackSlot 1–23 with model and channel tags | COMPLETE RACK (topology) | EV-01 | Process-instrument topology and tag identity; not wiring or offset |
| Pos. 01–20 configuration-tool positions | PARTIAL SCREEN | EV-03 | Instance position for 20 of 23 modules |
| 4-20 mA; wire-break diagnosis enabled; underflow diagnosis enabled | CHANNEL (Pos. 10 / RackSlot 13 / Channel 3) | EV-04 | That one Channel only. Enablement is not an observed diagnostic. |
| Open-circuit output representation; status byte layout; scaling result | NONE | — | **Not verified** |
| Process-image offsets; byte and word grouping | NONE | — | **Not verified.** Candidate offsets are `OFFSET_UNRESOLVED`. |
| 750-362 firmware and hardware revision | NONE | — | **Not verified** |
| Temperature Sensor acquisition topology (TC modules, Channel capacity, transport, schedule) | NONE | — | **NOT PROVIDED. Blocked.** |

### 2.4 Not inferred (binding)

No evidence hash, firmware identity, revision interpretation, process-data offset, word or byte order, diagnostic encoding, Channel setting, TC module identity, TC Channel order or Channel-to-Sensor binding has been inferred or invented. Settings verified for one Channel do not apply to another Channel, instance or model. TC Channel order is **not** inferred from Sensor order.

### 2.5 Note RISK-01 — location coincidence on the only Channel-level settings record

EV-04 (Pos. 10 / RackSlot 13 / Channel 3) sits at the workbook location of AI-003. Per OQ-03, the AI-003 identity is closed. That closure does **not** convert EV-04 into tag verification: the terminal schedule and wiring drawing (EV-13) remain required before any process-instrument Channel is treated as wired to its tag. No Pump-readiness conclusion is drawn from EV-04.

## 3. Gap register and remaining evidence requests

Priority: **P0** blocks any live acquisition design or implementation; **P1** blocks a pilot Loop-Test; **P2** blocks the full rollout or a non-pilot Channel.

### 3.1 Temperature Sensor acquisition (Domain B) — Owner-required evidence

| Required evidence (Owner list) | Status |
|---|---|
| TC acquisition hardware or module model | NOT_PROVIDED |
| Module count and Channel capacity | NOT_PROVIDED |
| Physical panel, rack and location | NOT_PROVIDED |
| Communication transport | NOT_PROVIDED |
| Sensor ID to Channel A/B schedule (see naming note below) | NOT_PROVIDED |
| Terminal schedule | NOT_PROVIDED |
| Wiring drawings | NOT_PROVIDED |
| TC type per Channel | NOT_PROVIDED |
| Cold-junction compensation | NOT_PROVIDED |
| Wire-break behaviour and configuration | NOT_PROVIDED |
| Engineering-tool export or screenshots | NOT_PROVIDED |

**Summary status:** `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`. `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`.

**Naming note.** The Owner's schedule wording is "Channel A/B". The product records thermocouple sides as `TC_F` and `TC_R` (PHY-002, PHY-003). The correspondence between "A/B" and "TC_F/TC_R" is `OWNER_INPUT_PENDING`. Neither naming is assumed to be the other.

If TC acquisition sits on another rack, controller or network, it is recorded as a **separate acquisition source**. It is never merged into the process-instrument rack inventory.

### 3.2 Gap register

| Gap | Missing evidence | Needed for | Priority | Status |
|---|---|---|---|---|
| GAP-01 | TC acquisition topology (§3.1 list) | Domain B identity, every TC Loop-Test | **P0** | NOT_PROVIDED — BLOCKED |
| GAP-02 | Sensor-to-TC-Channel schedule (`SENSOR_TO_TC_CHANNEL_SCHEDULE`) | Domain B binding; rollout | **P0** | NOT_PROVIDED — BLOCKED |
| GAP-03 | Actual Process Data view or export (EV-06) | Any live process-instrument read | **P0** | NOT_PROVIDED |
| GAP-04 | Head-station firmware and revision; I/O Config; field-network mapping (EV-07) | Process-instrument transport and head-station profile | **P0** | NOT_PROVIDED |
| GAP-05 | Status and diagnostic representation for each model in use (explicit Channel diagnostic, module diagnostic, status bit or byte, special raw value) | Wire-break classification (§6.6) | **P0** | NOT_PROVIDED |
| GAP-06 | 750-471 Common settings (EV-08) | Every 750-471 Channel | **P1** | NOT_PROVIDED |
| GAP-07 | 750-471 Scaling settings (EV-09) | Engineering value | **P1** | NOT_PROVIDED |
| GAP-08 | 750-471 Channel-configuration consistency (EV-10) | Each 750-471 Channel | **P1** | Only Pos. 10 / Channel 3 verified |
| GAP-09 | TC type by Channel; cold-junction compensation; per-Channel wire-break configuration | Domain B measurement and diagnosis | **P0** | NOT_PROVIDED — BLOCKED |
| GAP-10 | Wiring drawings and terminal schedule; meaning of `Ref.` (EV-13, EV-15) | Loop-Test steps 3 and 4, both domains | **P1** | NOT_PROVIDED |
| GAP-11 | Expected normal values per pilot Channel | Baseline criteria | **P1** | NOT_PROVIDED |
| GAP-12 | Observed value during a disconnection | Diagnostic verification | **P1** | NOT_PROVIDED |
| GAP-13 | 750-554 settings (EV-11) | AO-001 only where relevant | **P2** | NOT_PROVIDED |
| GAP-14 | Revisions of the other eight 750-471 instances | Boundary pilot | **P2** | NOT_PROVIDED |
| GAP-15 | Owner-approved Commissioning procedure and LOTO procedure (§8.1) | Every field test | **P0** | NOT_PROVIDED — no field step authored here |
| GAP-16 | Owner decision on whether any workbook analog channel is a Physical Sensor | Domain A scope | Resolved in part by OQ-02 (two domains); pilot selection `OWNER_INPUT_PENDING` | Owner input pending for pilot |

## 4. Conflict and decision register

| ID | Subject | Status after Owner decision |
|---|---|---|
| **CF-01** | Temperature Sensor acquisition topology absent from the authoritative workbook. The workbook has 23 rack modules and no thermocouple input module. The product requires 212 TC Channels for 106 Sensors (PHY-002). | **CONFIRMED EVIDENCE GAP (OQ-01).** `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`. `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`. `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. Workbook not modified; no TC modules added; no mapping inferred. |
| **CF-02** | Whether the 35 workbook analog channels are inside the Physical Sensor population. | **RESOLVED BY DOMAIN SEPARATION (OQ-02).** The workbook AI-001 to AI-035 channels are Domain A Process Instrument channels unless the Owner states otherwise for a specific channel. They are not the 212 TC Channels by default. The two inventories are not merged. |
| **CF-03** | AI-003 workbook source text reads "Pressure Transmitter Main Valve Outlet"; the identity is Pump Outlet Pressure. | **CLOSED BY OWNER RULING (OQ-03).** Identity: Pump Outlet Pressure, PUMP_OUTLET. The workbook text is source text and evidence only. It creates no separate Main Valve pressure identity. `MAIN_VALVE_OUTLET_PRESSURE` remains absent. Not reopened without new physical evidence. |
| **CF-04** | Quality taxonomy mismatch between the first-revision 11-value list and the existing `Quality` enum. | **RESOLVED (OQ-04).** `Quality` stays `GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED`. Diagnostic reasons and commissioning states are separate proposed vocabularies (§6). `INVALID` is a presentation concept only. |
| **CF-05** | Workbook labels for AI-004 to AI-011 read "WSB Pressure transmitter #1–#8"; the Owner ruling maps them to IV1 to IV8 outlet pressure. | **OPEN — carried.** Owner-ruled label interpretation, enforced in Mapping. Physical confirmation sits with the Process Instrument commissioning of those Channels. Not reopened here. |
| **CF-06** | README.md current-status rows showed the Stage 0.4B-3 position with PR #15 open. | **RESOLVED BY NARROW CORRECTION (§16).** Current-status rows only. Unrelated README content untouched. |
| **CF-07** | Two links to `DOMAIN_MODEL.md` §2.2.1 in `docs/CURRENT_STATE.md` and `docs/MASTER_PLAN.md` used a legacy anchor (`…cannon-slots-owner-confirmed`). `DOMAIN_MODEL.md` line 70 deliberately retains that legacy HTML anchor, so the links were **not broken**. `ARCHITECTURE.md` and `REQUIREMENTS.md` already use the canonical heading anchor. | **RESOLVED (Owner-authorised documentation consistency change).** Exactly two links moved to the canonical anchor; no other link affected (§16). |

**Owner decisions OQ-01 to OQ-08:** all recorded in §0.1. Items the Owner did not specify are listed as `OWNER_INPUT_PENDING` in §6, §8 and §13 and are not assumed.

## 5. Commissioning identity model

### 5.1 Identity fields (Temperature Sensor domain B unless marked A)

| Field | Definition | Source today | Status |
|---|---|---|---|
| Sensor ID | Canonical identity of one Sensor location | Product `SensorId` field; production values not in repository | Field exists; production values NOT_PROVIDED |
| Physical wall | Left, Rear, Right or Front | PHY-001; `Wall` enum | Domain-confirmed; per-Sensor verification NOT_PROVIDED |
| Physical location | Wall column and wall row | Product `WallColumn`, `WallRow` | Structure confirmed; per-Sensor verification NOT_PROVIDED |
| Matrix row and logical column | Logical row 1–6, logical column 1–18, including the two gap positions | Product `LogicalRow`, `LogicalColumn` | Structure confirmed |
| Measurement identity | The Sensor's thermocouple pair (PHY-002) | PHY-002 | Domain fact |
| Channel side | `TC_F` or `TC_R`; the Owner's "A/B" wording is `OWNER_INPUT_PENDING` for correspondence | PHY-002, PHY-003 | Naming pending |
| Acquisition source | The TC acquisition source (rack, controller or network) | §3.1 | **NOT_PROVIDED** |
| Module instance, model, RackSlot, physical Channel | Module identity and position for a TC Channel | §3.1 | **NOT_PROVIDED** |
| Thermocouple type, cold-junction compensation, wire-break configuration | Per-Channel measurement settings | §3.1 | **NOT_PROVIDED** |
| Engineering unit and range | Scaled value unit and span | Domain B: NOT_PROVIDED. Domain A: AI-002 and AI-003 are 0–40 bar (Owner-confirmed); other AI channels UNCONFIGURED unless ruled. | Domain B blocked |
| Evidence basis | Evidence IDs for each field | §2 | Required per field |
| Commissioning state | §6.5 | — | No Sensor commissioned |

Domain A (Process Instrument) identities use the workbook tag, module, RackSlot and Channel as recorded in EV-01, plus the Owner rulings (AI-002, AI-003, IVn). They are not Sensor identities.

### 5.2 Required future live-observation fields

Raw process value · raw diagnostic or status representation · scaled engineering value · acquisition timestamp · value age · measurement quality · diagnostic reason · communication state · last valid value · last valid timestamp · recovery state. **None is implemented.** Definitions: §6 and §11.

### 5.3 Identity rules (binding)

1. Sensor, Channel and I/O tag are different identities. None is derived from another without recorded evidence.
2. Wall name and TC side are different concepts (PHY-003).
3. RackSlot, configuration-tool position and ProcessModulePosition are separate fields and are never aliased.
4. I7 and I16 carry no Sensor, no thermocouple Channel and no Water Jet identity. They are placement anchors only.
5. Assigned Cleaning Device is never inferred from a Sensor's wall location.
6. Actual-rack evidence belongs to the module instance and the Channel it was observed on.
7. Commissioning evidence is never written into Runtime control state.
8. TC Channel order is never inferred from Sensor order.

### 5.4 Domain B binding readiness

A Sensor enters Domain B binding only when the §3.1 evidence is supplied and verified. Until then every Sensor is `NOT_MAPPED` for binding purposes, and no Sensor-level value is treated as bound.

### 5.5 Two commissioning domains (OQ-02)

| | Domain A — Process Instrument Commissioning | Domain B — Temperature Sensor Commissioning |
|---|---|---|
| Population | Workbook DI, DO, AI and AO channels: 104 tagged rows, including 18 rows annotated "pending reply" | 106 Sensors; 212 thermocouple Channels |
| Identity source | Workbook tag and Owner rulings only | Owner-supplied TC topology and schedule (§3.1) |
| Current status | Evidence partial (EV-04 for one Channel; process image NOT_PROVIDED) | **BLOCKED** (TC_ACQUISITION_TOPOLOGY NOT_PROVIDED) |
| Acquisition source | Process-instrument rack and network (EV-07) | Separate source; rack, controller or network **NOT_PROVIDED** |
| AI-002 / AI-003 | Diagnostic only / Pump-ready source before P1 (OQ-03) | Not applicable |

The inventories are not merged. A Domain A Channel is never presented as a Sensor, and a Sensor is never presented as a workbook channel.

## 6. Quality and diagnostic model (OQ-04)

Everything in this section is **[PROPOSED]** except the `Quality` enum, which is **unchanged**. Nothing here is implemented.

### 6.1 Measurement Quality (existing enum — unchanged)

`GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED`. `packages/contracts/Enums.cs` and `wjss-contracts-ts/types.ts` are not modified by this Stage.

### 6.2 DiagnosticReason (separate proposed vocabulary)

`NONE` · `WIRE_BREAK` · `SUSPECTED_WIRE_BREAK` · `OUT_OF_RANGE` · `MODULE_FAULT` · `COMMUNICATION_LOSS` · `NOT_MAPPED` · `NOT_COMMISSIONED` · `CONFIGURATION_INVALID`

Each diagnostic carries a basis field: `EXPLICIT`, `SPECIAL_RAW_DOCUMENTED`, `CONFIGURED_SUBSTITUTE_DETECTED`, `INFERRED` or `NONE`.

### 6.3 Owner-specified mappings

| Condition | Quality | DiagnosticReason | EngineeringValue |
|---|---|---|---|
| Explicit wire break | `BAD` | `WIRE_BREAK` | **null** |
| Suspected wire break (inferred) | `UNCERTAIN` | `SUSPECTED_WIRE_BREAK` | Not specified by the Owner. Proposed: **null** (never zero). `OWNER_INPUT_PENDING` confirmation. |
| Out of range | `BAD` | `OUT_OF_RANGE` | Not specified by the Owner. Proposed: **null** for the current value; the out-of-range condition is shown, not clamped. `OWNER_INPUT_PENDING` confirmation. |
| Communication loss | `BAD` | `COMMUNICATION_LOSS` | Not specified by the Owner. Proposed: **null**. `OWNER_INPUT_PENDING` confirmation. |
| Stale | `STALE` | Not specified by the Owner. Proposed: `NONE` or the age reason per §6.7. | **null** for the current value; last value only as labelled history. `OWNER_INPUT_PENDING` confirmation. |

Not specified by the Owner, and therefore **not mapped here** (`OWNER_INPUT_PENDING`): the Quality value for `NOT_MAPPED`, `NOT_COMMISSIONED`, `MODULE_FAULT` and `CONFIGURATION_INVALID`. Until the Owner decides, every such condition is treated as not eligible for automatic use, and no Quality is inferred for it.

### 6.4 INVALID — presentation concept only

`INVALID` is **not** an enum value and is **not** added to `Quality`. A value is presented as invalid when:

- `IsCurrentValueValid` is false;
- `Quality` is `BAD` or `STALE`;
- a specific `DiagnosticReason` is set; and
- `EngineeringValue` is null.

Whether `IsCurrentValueValid` is false for `UNCERTAIN` (suspected wire break) is not specified by the Owner. Proposed: **false**, so that a suspected wire break is never treated as a usable measurement. `OWNER_INPUT_PENDING` confirmation.

### 6.5 CommissioningState (separate proposed vocabulary)

`NOT_TESTED` · `IN_PROGRESS` · `PASS` · `FAIL` · `PARTIAL` · `BLOCKED`

These describe the test verdict state of a Channel or a Sensor. They are evidence about testing. They are **not** a reading quality, and they never change `Quality`, Queue state or Runtime control state. A binding or evidence status (`NOT_MAPPED`, `NOT_PROVIDED`, etc.) is recorded separately from `CommissioningState`.

### 6.6 Actual wire-break representation — determination

| Candidate representation | Evidence | Determination |
|---|---|---|
| Explicit Channel diagnostic | EV-04: wire-break diagnosis **enabled** on one 750-471 Channel. No open-circuit observation. | **Enablement verified for one Channel. Observed representation NOT VERIFIED.** |
| Explicit module diagnostic | EV-02 says 750-471 status and diagnostic representation depends on Coupler and configuration. No module-level diagnostic is evidenced for any model. | **NOT VERIFIED** |
| Status bit or byte | EV-08 and EV-06 NOT_PROVIDED | **NOT VERIFIED** |
| Special raw value | No observation | **NOT VERIFIED** |
| Configured substitute value | No substitute behaviour evidenced; the user-limit values in EV-04 are shown but their role is not verified | **NOT VERIFIED. Never treated as a measurement.** |
| Inference only | The only route currently available | **Permitted only as `SUSPECTED_WIRE_BREAK`** |
| Temperature Sensor modules | No module evidence (§3.1) | **NOT VERIFIED — BLOCKED** |

**Conclusion.** WIRE-BREAK DECODING IS NOT IMPLEMENTED. The representation for any model and firmware in use must be evidenced by Owner commissioning evidence before any `WIRE_BREAK` classification is used. Until then, an open circuit can at most be `SUSPECTED_WIRE_BREAK`. Nothing is classified as a confirmed wire break.

### 6.7 Explicit and inferred basis; timing values

- A diagnosis is `EXPLICIT` only when the module or Channel reports it through a documented representation. A value alone is never sufficient for `WIRE_BREAK`.
- Discrimination matters on a 4–20 mA loop. Arithmetic from the stated 0–40 bar, 4–20 mA span (not a measurement): 1 bar is 4.4 mA and 0 bar is 4.0 mA. An open loop carries no loop current, so the input would read below 4 mA, the region a measuring-range underflow diagnosis covers. The two conditions must be separated by an explicit diagnostic. Underflow without an explicit wire-break diagnostic is `OUT_OF_RANGE` or `SUSPECTED_WIRE_BREAK`, never `WIRE_BREAK`.
- **Staleness timeout:** `CONFIGURABLE / OWNER_INPUT_PENDING`. No default is set. Any example is `TEST_EXAMPLE_ONLY` and `NOT_PRODUCTION_APPROVED`.
- **Recovery stabilisation:** `CONFIGURABLE / OWNER_INPUT_PENDING`. A Channel leaves a non-GOOD state only after a valid current reading and the stabilisation rule. Reconnection enters a recovering state, not an automatic return to `GOOD`.
- Recovery states (proposal): `NOT_RECOVERING` → `RECOVERY_PENDING_VALID_READING` → `STABILISING` → `RECOVERED`.

### 6.8 Diagnostic reason codes and `basis`

Diagnostic reasons in §6.2 are the only reason codes proposed. Module-specific diagnostic detail (for example underflow, overload) is recorded in the observation, not as a new DiagnosticReason, until the Owner decides otherwise.

## 7. Invalid-value safety rules

These rules bind every later stage. Test IDs are **planned, not executed**.

| Rule | Statement | Planned test (not executed) |
|---|---|---|
| INV-01 | An invalid engineering value is null and is never rendered or stored as `0`. | T-INV-01 |
| INV-02 | `WIRE_BREAK` means the current EngineeringValue is null, unless verified module behaviour explicitly requires another representation. | T-INV-02 |
| INV-03 | Last Valid Value is labelled historical and is never shown or used as the current measurement. | T-INV-03 |
| INV-04 | `COMMUNICATION_LOSS` remains separate from Channel `WIRE_BREAK`. | T-INV-04 |
| INV-05 | `STALE` remains separate from communication loss. | T-INV-05 |
| INV-06 | `OUT_OF_RANGE` remains separate from `WIRE_BREAK`. | T-INV-06 |
| INV-07 | `NOT_COMMISSIONED` (DiagnosticReason) and `CommissioningState` remain separate from `INVALID` presentation. | T-INV-07 |
| INV-08 | An inferred condition is never a confirmed wire break: it is `UNCERTAIN` with `SUSPECTED_WIRE_BREAK`. | T-INV-08 |
| INV-09 | Invalid, stale, communication-loss and uncommissioned observations never trigger automatic Queue entry. | T-INV-09 |
| INV-10 | Invalid observations never enter Cleaning classification. | T-INV-10 |
| INV-11 | Invalid observations never satisfy Pump, Valve or motion readiness. | T-INV-11 |
| INV-12 | Recovery requires a valid current reading **and** the explicit stabilisation rule (OWNER_INPUT_PENDING). | T-INV-12 |
| INV-13 | No automatic control authority is introduced by any observation, quality, diagnostic or commissioning record. | T-INV-13 |
| INV-14 | AI-002 remains diagnostic and trend only; it never satisfies Pump readiness, is never Valve diagnosis, and has no alarm, trip or interlock authority. AI-002 Runtime publication stays NOT IMPLEMENTED. | T-INV-14 |
| INV-15 | AI-003 (Pump Outlet Pressure, PUMP_OUTLET) is the only pre-P1 Pump-ready pressure source. Its readiness additionally requires every existing gate. This Stage changes nothing about it. `MAIN_VALVE_OUTLET_PRESSURE` remains absent. | T-INV-15 |
| INV-16 | Passing a commissioning verdict never changes measurement quality, Queue state or Runtime control state. | T-INV-16 |

## 8. Loop-Test workflow

### 8.1 Preconditions and procedure authority (OQ-08)

| Role | Holder |
|---|---|
| Procedure author | Commissioning / Instrument responsible role |
| Safety approval | Owner-authorised site safety role |
| Execution | Authorised commissioning personnel |
| Final technical acceptance | Owner |

- No open-circuit instruction is authorised without the approved site procedure and LOTO controls. This document authors **no** field steps, stimulus method or open-circuit method.
- Identity bindings for steps 1–4 are supplied. For Domain B (Temperature Sensor) Channels, the workflow is **BLOCKED** until §3.1 is supplied.
- Verdicts record evidence only. They write nothing to hardware (§9).

### 8.2 The 22 steps

| # | Step | Required record | Gate or stop rule |
|---|---|---|---|
| 1 | Select the Sensor (Domain B) or Channel (Domain A) and the measurement Channel | Identity, Channel side, binding status | Domain B: BLOCKED until §3.1. |
| 2 | Confirm identity and physical location | Wall, column and row; or Domain A tag location | Mismatch → STOP the test. |
| 3 | Confirm workbook tag (Domain A) or TC acquisition identity (Domain B), ModuleInstanceId, RackSlot and Channel | Each field with its evidence ID | Any field NOT_PROVIDED → BLOCKED. |
| 4 | Confirm wiring drawing and terminal schedule references | Drawing and schedule identifiers | Missing → BLOCKED. |
| 5 | Observe baseline raw and engineering values | Raw, scaled, timestamp, age, quality | Baseline accepted only when Quality is GOOD. |
| 6 | Confirm measurement quality is GOOD | Quality and DiagnosticReason | Not GOOD → NOT_TESTED or BLOCKED, with reason. |
| 7 | Apply the Owner-authorised stimulus for the sensor type | Procedure identifier; stimulus recorded by the Owner | Method per approved site procedure (`OWNER_INPUT_PENDING`). |
| 8 | Confirm the selected Channel changes | Before and after values, direction | No expected change → FAIL. |
| 9 | Confirm adjacent or unrelated Channels do not change incorrectly | Before and after values of defined neighbours | Unexpected change → FAIL. |
| 10 | Perform the Owner-authorised open-circuit test | Procedure identifier; LOTO record | Per approved site procedure only. |
| 11 | Observe the explicit diagnostic or documented special representation | Raw value, status bits or bytes, diagnostic fields | None observed → record "no explicit representation". |
| 12 | Confirm the WJSS state after the open circuit | Explicit: Quality BAD, DiagnosticReason WIRE_BREAK, EngineeringValue null. Inferred: Quality UNCERTAIN, DiagnosticReason SUSPECTED_WIRE_BREAK, EngineeringValue null. | Any other result → FAIL. |
| 13 | Confirm DiagnosticReason WIRE_BREAK **when explicitly evidenced** | Diagnostic reason and basis | Not explicit → **PARTIAL**, never PASS. |
| 14 | Confirm the current EngineeringValue is not shown as a valid zero | Displayed value | Any displayed `0` for an invalid value → **FAIL (critical)**. |
| 15 | Confirm Last Valid Value is labelled historical | Label text and timestamp | Missing label → FAIL. |
| 16 | Confirm the invalid Channel cannot influence Queue or Cleaning logic | Queue and Cleaning decision evidence | Any influence → **FAIL (critical)**. Future stage. |
| 17 | Restore the wiring | Restoration record; LOTO release; time; person | Per approved site procedure. |
| 18 | Confirm the diagnostic clears | Diagnostic fields after restoration | Remains → FAIL or STOP per procedure. |
| 19 | Confirm the measurement recovers | Current reading and quality | No valid reading → remain in recovering state. |
| 20 | Apply the stabilisation rule | Stabilisation record | `OWNER_INPUT_PENDING`. |
| 21 | Record PASS, FAIL, PARTIAL, NOT_TESTED or BLOCKED | Verdict per step and per Channel | Verdict is evidence only. |
| 22 | Record tester, timestamp and remark | Tester, UTC timestamp, remark | Missing tester or timestamp → verdict not accepted. |

### 8.3 Verdict vocabulary

`NOT_TESTED` · `IN_PROGRESS` · `PASS` · `FAIL` · `PARTIAL` · `BLOCKED` (§6.5). A PARTIAL is not a PASS. A BLOCKED is not a FAIL and is never counted as passed. Failed records are kept (append-only).

### 8.4 Sensor verdict (OQ-07)

For a Sensor with two required measurement Channels:

| Sensor verdict | Rule |
|---|---|
| PASS | All required Channels PASS |
| PARTIAL | At least one Channel PASS and another is NOT_TESTED or BLOCKED |
| FAIL | Any required Channel FAIL |
| NOT_TESTED | No required Channel has been tested |
| BLOCKED | Testing cannot begin because mapping, evidence or Hardware is unavailable |

A/B Channels are never averaged to hide a failed Channel. Progress is tracked per Channel (212) and per Sensor (106).

## 9. Commissioning Monitor requirements (design only; not implemented)

The smallest future Monitor is read-only with respect to Hardware. PASS, FAIL and other buttons record commissioning evidence only and never write to Hardware.

- **Overview fields:** Sensor ID (Domain B) or tag (Domain A); location; Channel identity; Rack, Slot and Channel; raw value; engineering value; unit; Quality; DiagnosticReason; communication state; last update; age; CommissioningState.
- **Detail fields:** complete identity; mapping basis; raw observation; diagnostic and status observation; scaling basis; current Quality; last valid history (labelled historical); evidence references; test history; tester remark.
- **Filters:** NOT_TESTED, GOOD, INVALID (presentation), WIRE_BREAK, OUT_OF_RANGE, COMMUNICATION_LOSS, STALE, NOT_MAPPED, FAILED (CommissioningState FAIL). Each filter reads one field: Quality, DiagnosticReason or CommissioningState.
- **Progress summaries:** total Channels; passed; failed; partial; blocked; not tested; active invalid; active wire break; communication loss; stale.
- **Behaviour:** a CommissioningState never changes a Quality, a Queue entry or a Runtime control state. An active WIRE_BREAK is shown as active only when it is explicit.

## 10. Commissioning record schema (proposal; not implemented)

### 10.1 Four separate categories

1. **Runtime live state** — current observation. Transient, not commissioning evidence, never edited by an operator.
2. **Historical commissioning evidence** — append-only record of baseline, stimulus, open-circuit, diagnostic and recovery observations, with evidence references.
3. **Operator-entered verdict** — CommissioningState, tester, timestamp and remark. Evidence about the test, not a measurement.
4. **Automatic measurement quality** — Quality, DiagnosticReason and IsCurrentValueValid, computed by the classifier. Never written by an operator; never used as a verdict.

### 10.2 Record fields

```text
CommissioningRecord {
  schemaVersion
  sessionId
  domain                         // PROCESS_INSTRUMENT | TEMPERATURE_SENSOR
  sensorId                       // Domain B only; null for Domain A
  measurementChannelId           // Domain B: TC_F or TC_R (naming pending); Domain A: workbook tag
  identity {                     // each field with its evidence ID
    wall, logicalRow, logicalColumn, channelSide,
    workbookTag, acquisitionSource, moduleInstanceId, modelNumber,
    rackSlot, ioCheckPosition, physicalChannel, signalType, evidenceRevision
  }
  baselineRawValue
  baselineEngineeringValue       // null when not GOOD
  stimulusObservation            // procedure ID; observed change
  openCircuitObservation         // procedure ID; observed raw and diagnostic
  diagnosticObservation          // raw diagnostic representation; basis
  recoveryObservation            // valid reading; stabilisation record
  automaticQualityAtCapture {    // automatic, read-only snapshot
    quality, diagnosticReason, isCurrentValueValid, capturedAt
  }
  commissioningState             // operator-entered: NOT_TESTED | IN_PROGRESS | PASS | FAIL | PARTIAL | BLOCKED
  tester
  timestamp                      // UTC, ISO 8601
  remark
  recordHash                     // deterministic canonical hash (algorithm OWNER_INPUT_PENDING; SHA-256 proposed)
}
```

Placeholders are not values. No commissioning record exists in the repository.

### 10.3 Deterministic export (proposal)

- **JSON:** UTF-8, LF, fixed key order as above, ISO 8601 UTC timestamps, explicit `null`.
- **CSV:** fixed column order matching §10.2 flattened with dotted names. Rows sorted by domain, then wall (LEFT, REAR, RIGHT, FRONT), logical row, logical column and channel side.
- Export includes `evidenceRevision` and `recordHash`. It does not include the Runtime live state.

### 10.4 Boundary

CommissioningState is **not** Runtime control state. Queue, Cleaning, Pump, Valve and motion logic never read it. Nothing in this schema writes to Hardware.

### 10.5 Sensor verdict in the record

The Sensor-level verdict is derived from its Channel records by the rule in §8.4. It is stored as a derived view, not as a separate operator entry, so it cannot disagree with the Channel records.

## 11. Read-only acquisition architecture (not implemented)

Two separate acquisition sources are kept apart: the **process-instrument source** (Domain A; rack and network to be evidenced under EV-06 and EV-07) and the **temperature source** (Domain B; rack, controller or network **NOT_PROVIDED**). Neither source is implied by the other.

| Layer | Responsibility | Must not |
|---|---|---|
| A. Transport state | Report `CONNECTED`, `DISCONNECTED`, `RECONNECTING`, `COMMUNICATION_FAULT` per source | Write, command or force any output |
| B. Channel acquisition | Raw word or value; raw diagnostic or status; monotonic acquisition revision; timestamp; staleness | Infer an explicit diagnostic |
| C. Scaling | Configured signal type, range and unit with a stated conversion basis; invalid input gives null, never zero | Clamp silently; use a substitute as a measurement |
| D. Quality classification | Quality, DiagnosticReason, explicit or inferred basis, recovery state, last valid history (§6) | Map an inferred condition to `WIRE_BREAK` |
| E. Runtime publication | One atomic Sensor or Channel observation and delta; no control transition; invalid values excluded from automatic classification and Queue eligibility | Publish AI-002 (not implemented); create a Queue entry from an invalid value |
| F. Commissioning evidence | Separate store and model; deterministic revision; operator CommissioningState | Feed Runtime control state; write Hardware |

No live acquisition is implemented. The transport choice is **not made** here (§15).

## 12. Pilot Loop-Test plan (OQ-05)

### 12.1 Status

**No actual Sensor ID or Channel is selected.** Actual IDs: `OWNER_INPUT_PENDING`. Selection roles are recorded below. Domain B pilots cannot begin until the §3.1 schedule exists. No actual IDs are chosen from this document.

### 12.2 Selection roles

| Pilot | Role | Actual ID | Status |
|---|---|---|---|
| Pilot 1 | First representative acquisition module | `OWNER_INPUT_PENDING` | Domain B: BLOCKED (§3.1). Domain A: Owner selection. |
| Pilot 2 | A different wall or acquisition group | `OWNER_INPUT_PENDING` | Domain B: BLOCKED. Domain A: Owner selection. |
| Pilot 3 | A Channel with an explicit wire-break diagnostic | `OWNER_INPUT_PENDING` | Requires evidence of an explicit representation (GAP-05). Not yet available for any Channel. |
| Pilot 4 | A module, revision or network boundary case | `OWNER_INPUT_PENDING` | Domain B: BLOCKED. Domain A: the revision evidence beyond Pos. 10 is NOT_PROVIDED (GAP-14). |

### 12.3 Per-pilot specification (applies to Pilots 1–4)

| Attribute | Specification |
|---|---|
| Required evidence | Identity and schedule (§3.1, GAP-02); process image (GAP-03); head station and network (GAP-04); status and diagnostic representation (GAP-05); module configuration (GAP-06 to GAP-09 as applicable); terminal schedule and drawings (GAP-10); expected normal values (GAP-11); approved procedures (GAP-15) |
| Baseline observation | Raw, scaled, unit, timestamp, age; Quality must be GOOD; neighbour Channels recorded |
| Stimulus method | Per the approved site procedure for the sensor type. Not specified here. |
| Open-circuit method | Per the approved site procedure under LOTO only. Not specified here. No field manipulation is instructed. |
| Expected diagnostic | `WIRE_BREAK` only if the representation is evidenced for that model and firmware. Otherwise: no explicit representation observed. |
| Expected WJSS state | Explicit: Quality BAD, DiagnosticReason WIRE_BREAK, EngineeringValue null. Inferred: Quality UNCERTAIN, DiagnosticReason SUSPECTED_WIRE_BREAK, EngineeringValue null; step 13 PARTIAL. |
| Recovery expectation | Diagnostic clears after restoration; valid current reading; stabilisation rule (`OWNER_INPUT_PENDING`) |
| PASS criteria | Steps 5–21 satisfied with observed records; step 13 PASS only with an explicit diagnostic; no step shows a valid zero; invalid Channel excluded from Queue and Cleaning (step 16 evidence recorded) |
| STOP criteria | Unexpected change on a neighbour Channel; displayed valid zero for an invalid value; unexpected output or Hardware change; LOTO not in force; identity mismatch; any requirement to write or force; any Pump or Valve state change; any Galil motion event |

## 13. Rollout plan — Temperature Sensor domain (106 Sensors / 212 Channels)

Blocked until §3.1 is supplied. Rules proposed for later:

| Topic | Rule |
|---|---|
| Population | 106 Sensors; 212 TC Channels (2 per Sensor). Wall split Left 24, Rear 29, Right 24, Front 29. I7 and I16 are never in the population. |
| Batch sequencing | By acquisition module or group once the topology exists, and by wall and area within it. Batch size: `CONFIGURABLE / OWNER_INPUT_PENDING`. No default is set. |
| Auto-advance | After a recorded verdict, the Monitor proposes the next untested Channel in the approved sequence. It never records a PASS automatically. |
| Exception-only retest | Retest only FAIL, PARTIAL and BLOCKED records, and Channels whose evidence is invalidated by a change in the rack or configuration revision. |
| Progress tracking | Per Channel and per Sensor, using the §8.4 verdict. |
| Review and sign-off | Owner final technical acceptance per batch and at the final freeze (§8.1). The agent never signs. |
| Failed-Channel handling | FAIL keeps the Channel out of commissioned state. The record is kept. Retest only after a recorded corrective action. |
| Blocked-test handling | BLOCKED requires a reason code and an owner. A BLOCKED item is never counted as passed. |
| Evidence export | Deterministic JSON and CSV (§10.3). |
| Final mapping freeze | After all required evidence and sign-off, freeze the Sensor-to-Channel map with a revision identifier. A later change invalidates only the affected records. |
| Commissioned definition | A Sensor is commissioned only after Owner final technical acceptance. A value existing is never sufficient. |

## 14. Galil parallel readiness plan (G1–G5; no Galil control implemented)

This plan is parallel and separate. It does **not** expand the Sensor-readiness scope. Four Galil DMC-B140-M controllers are expected (PHY-005). Network topology is `[OPEN]` and `[NOT VERIFIED]` (PHY-008). No controller is contacted. No numeric limit, speed, timeout, pulse count or coordinate is defined here.

**G1 — Communication and controller identity, no motion**
- Evidence prerequisites: controller identity and firmware (NOT_PROVIDED); network topology (OPEN).
- Safety prerequisites: no motion possible; drives disabled; emergency-stop function verified by the Owner against SAFETY_BOUNDARY.
- Allowed actions: identity and status read only.
- Prohibited actions: any motion command; any output or parameter write; any network scan.
- Acceptance criteria: identity matches the Owner-approved record; no write capability is exercised.
- STOP criteria: unexpected identity; any write path reached; any unexpected axis activity.

**G2 — Drive-disabled I/O, limits and fault feedback**
- Evidence prerequisites: Galil I/O map and limit wiring (NOT_PROVIDED); fault feedback definitions (NOT_PROVIDED).
- Safety prerequisites: G1 PASS; drives disabled; LOTO as applicable.
- Allowed actions: read I/O, limit and fault inputs with drives disabled.
- Prohibited actions: enabling drives; setting outputs.
- Acceptance criteria: each limit and fault input read correctly against an Owner-observed state.
- STOP criteria: any axis movement; any unexpected output change.

**G3 — Controlled low-energy motion**
- Evidence prerequisites: G1 and G2 PASS; Owner-approved low-energy parameter set (NOT_PROVIDED; not invented).
- Safety prerequisites: physical supervision; emergency stop; LOTO and approved procedure.
- Allowed actions: Owner-approved low-energy motion on one axis under supervision.
- Prohibited actions: production coordinates, travel limits or speeds; multi-axis motion.
- Acceptance criteria: motion stays within the approved set; stop and return observed.
- STOP criteria: limit or fault event; unexpected motion; loss of communication.

**G4 — Motion command lifecycle, timeout, abort and fault handling**
- Evidence prerequisites: G3 PASS; Owner-approved timeout values (NOT_PROVIDED; not invented).
- Safety prerequisites: as G3.
- Allowed actions: lifecycle tests covering accepted, complete, aborted, timed-out and faulted states.
- Prohibited actions: invented timeouts; motion beyond the approved set.
- Acceptance criteria: each lifecycle state observed and recorded; abort succeeds in every tested case.
- STOP criteria: abort fails; a state cannot be observed; a fault is not reported.

**G5 — Integrated dry cycle without Pump or water pressure**
- Evidence prerequisites: G1 to G4 PASS; Owner-approved cycle definition (NOT_PROVIDED).
- Safety prerequisites: Pump disabled; no water supply pressure; single-active-Cleaning-Job rule respected.
- Allowed actions: observe the dry sequence.
- Prohibited actions: any Pump start; any water supply; any Valve command outside the approved cycle.
- Acceptance criteria: the sequence follows the approved definition; no pressure, Pump or water event occurs.
- STOP criteria: any Pump or pressure event; unexpected motion; concurrent Cleaning Job.

## 15. Stage 0.4C-1 proposed scope (PROPOSED; TC acquisition BLOCKED)

**Status: PROPOSED. Stage 0.4C-1 TC acquisition: BLOCKED** (`STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`) until §3.1 and §3.1-linked schedule evidence are supplied and the Owner authorises a scope. Process-instrument acquisition is also not authorised, because its process image (GAP-03) and head station (GAP-04) are not provided. No file below is authorised by this checkpoint.

- **Scope name:** Read-only Physical Sensor Acquisition Foundation (two tracks, each gated separately: A. Process Instrument; B. Temperature Sensor).
- **Preconditions:** for Track B, §3.1 supplied; for Track A, EV-06 and EV-07 supplied; for both, DiagnosticReason wire-break representation evidenced for each model and firmware in use, or the scope limited to synthetic inputs.
- **Narrowest permitted alternative (Owner choice):** synthetic-only classification with no transport, testing the §6 mappings and §7 rules against synthetic fixtures. No device is read.
- **Product files expected to change (PROPOSED, not authorised):**
  - `packages/contracts/SensorAcquisition.cs` (new): observation, diagnostic and history records.
  - `packages/domain/SensorAcquisitionClassifier.cs` (new): pure mapping and rules; no I/O.
  - `packages/contracts/fixtures/sensor-acquisition.synthetic.json` (new): synthetic fixture under the generated-fixture policy.
  - TypeScript mirror and tests in `packages/contracts/wjss-contracts-ts/` (modified).
  - A new scoped test project (new): requires a separate dependency and lock-file approval.
  - `packages/contracts/Enums.cs`: **not modified** under OQ-04.
- **Not expected to change:** `apps/runtime` (Host integration remains under CP-3c-2 governance), Runtime publication, Inspector, every `adapters/*` project, `config/*`, the workbook, lock files, Galil and MODBUS code.
- **Transport choice:** none live in 0.4C-1. Input is replay of sanitised, Owner-supplied snapshots (synthetic only in the repository). The live transport for either track is **not chosen** here and needs its own evidence and a TEST_HARDWARE Scope Gate.
- **No-write proof:** (a) the acquisition contract exposes no write, command or force member, checked by a test; (b) no socket, serial or MODBUS dependency is added, checked by a dependency scan; (c) static search of changed files finds no write API; (d) boundary scan S1–S9 clean.
- **Required process-image evidence (Track A):** EV-06, EV-08 to EV-10. **Track B:** §3.1 in full.
- **Required diagnostic evidence:** the observed explicit diagnostic or special raw representation for each model and firmware, from Owner commissioning evidence. A manual alone is not sufficient.
- **Planned tests (not executed):** T-SAQ-01 linear scaling at the configured range (synthetic); T-SAQ-02 explicit wire-break fixture maps to BAD / WIRE_BREAK / null; T-SAQ-03 inferred fixture maps to UNCERTAIN / SUSPECTED_WIRE_BREAK / null; T-SAQ-04 underflow without explicit diagnostic is never WIRE_BREAK; T-SAQ-05 out-of-range maps to BAD / OUT_OF_RANGE / null; T-SAQ-06 communication loss maps to BAD / COMMUNICATION_LOSS / null; T-SAQ-07 stale maps to STALE and is separate from communication loss; T-SAQ-08 INVALID presentation requires IsCurrentValueValid false, BAD or STALE, a specific reason and null value; T-SAQ-09 Last Valid Value labelled historical; T-SAQ-10 Sensor verdict per OQ-07 (no averaging); T-SAQ-11 invalid input excluded from Queue eligibility; T-SAQ-12 recovery requires valid reading and stabilisation; T-SAQ-13 contract exposes no write member; T-SAQ-14 deterministic serialisation and ordering; T-SAQ-15 AI-002 never satisfies Pump readiness; T-SAQ-16 `Quality` enum unchanged.
- **Owner-local commands (planned, not run):** scoped .NET test run with the Owner's SDK; TypeScript contract tests. Not run for this checkpoint.
- **TEST_HARDWARE Scope Gate requirement:** any live connection, polling or hardware-facing run requires a separate Owner Scope Gate defining network, tags, LOTO, supervision and rollback.
- **Rollback and disconnect behaviour:** on transport loss, the affected Channels become COMMUNICATION_LOSS (Quality BAD, EngineeringValue null); last valid values are kept as historical; no write is issued; on reconnection the state is recovering until a valid reading and the stabilisation rule (`OWNER_INPUT_PENDING`).
- **Stale-data behaviour:** staleness timeout `CONFIGURABLE / OWNER_INPUT_PENDING`. No timeout is invented. Stale values are shown only as STALE with age and are never eligible.
- **Quality propagation:** Channel Quality to Sensor verdict (§8.4) to the Queue and Cleaning eligibility input. Only GOOD and a PASS commissioning verdict are eligible, and every other existing gate still applies. AI-003 remains the only pre-P1 Pump-ready pressure source.
- **Why this scope cannot activate control:** read-only contract; no transport write; no command; no Runtime state transition from an observation; invalid values create no Queue entry; no Pump, Valve or motion readiness is derived; AI-002 is not published; commissioning verdicts record evidence only.

## 16. Changed / Unchanged / Not Verified

**CHANGED — successor commit (Owner decisions)**
- `docs/STAGE_0.4C-0_CHECKPOINT.md`: rewritten to record Owner decisions OQ-01 to OQ-08; separate Domain A and Domain B; separate DiagnosticReason and CommissioningState; Owner-specified quality mappings; TC topology gap and blocked scope; updated pilot roles, verdict rules, rollout and Stage 0.4C-1 status.
- `docs/CURRENT_STATE.md`: current position updated to the Owner-decision state; §12.34 updated; **link consistency change**: the `DOMAIN_MODEL.md` §2.2.1 link moved from the legacy anchor `…cannon-slots-owner-confirmed` (retained in the target as an HTML anchor) to the canonical `…the-two-non_sensor_gap-positions-owner-confirmed` (one link).
- `docs/MASTER_PLAN.md`: §3.6 updated with the Owner decisions; ledger entry updated; **link consistency change**: same legacy anchor moved to the canonical anchor (one link).
- `CHANGELOG.md`: new dated successor entry.
- `README.md`: **narrow current-status correction** only: the "Current stage", "Current stage identifiers" and "Main Development Scope Gate" rows are updated to Stage 0.4C-0 (current evidence gate), PR #15 MERGED at `29ee137c…`, PR #16 OPEN — NOT MERGED, and the TC gap. No other README content changed.

**UNCHANGED**
- `T8_IO_Card_Mapping.xlsx` (SHA-256 verified).
- All Product source: `packages/**`, `apps/**`, `adapters/**`, `tests/**`, `tools/**`, `config/**`, `spikes/**`, including `packages/contracts/Enums.cs` (the `Quality` enum) and `packages/contracts/wjss-contracts-ts/types.ts`.
- All executable tests and fixtures; build, solution and props files; every lock file; dependencies.
- Runtime sequencing, Pump and Valve logic, AutoSequence, Galil and MODBUS code, production configuration.
- `docs/ARCHITECTURE.md` and `docs/REQUIREMENTS.md` (their existing anchor is already canonical; not touched).
- Boundary scanner and its rules.

**NOT VERIFIED**
- TC acquisition topology: module model, count, capacity, location, transport, schedule, terminal schedule, drawings, TC type, cold-junction compensation, wire-break configuration (§3.1).
- Process-image offsets; byte and word order; field-network mapping; head-station firmware and revision.
- Explicit wire-break representation for any model or firmware.
- 750-471 Common settings, Scaling and configuration consistency beyond Pos. 10 / Channel 3; 750-554 settings.
- Physical identity of AI-004 to AI-011 against their ruled labels (CF-05).
- Manuals and Owner screens: outside the repository, not in the sandbox, not hashed.
- Any physical Sensor's wiring, measured value, open-circuit or recovery behaviour. **No physical Sensor has been tested.**
- Staleness timeout, recovery stabilisation, batch size, record-hash algorithm (`OWNER_INPUT_PENDING`).
- Quality values for NOT_MAPPED, NOT_COMMISSIONED, MODULE_FAULT, CONFIGURATION_INVALID; IsCurrentValueValid for UNCERTAIN; EngineeringValue for suspected, out-of-range, communication-loss and stale (§6.3, §6.4).
- The A/B to TC_F/TC_R correspondence (§3.1 naming note).
- Pilot identities (`OWNER_INPUT_PENDING`).
- Any Owner-approved commissioning, LOTO or site procedure.
- Galil controller identity, network and every motion parameter.
- **Not implemented:** PHYSICAL SENSOR ACQUISITION NOT IMPLEMENTED; WIRE-BREAK DECODING NOT IMPLEMENTED; SENSOR COMMISSIONING MONITOR NOT IMPLEMENTED; ACTUAL PROCESS-IMAGE OFFSETS NOT VERIFIED; AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED; NO MODBUS IMPLEMENTED; NO TEST_HARDWARE AUTHORIZED; NO PRODUCTION AUTHORIZED; NO DEVICE WRITE OR CONTROL.

## 17. Validation performed

Documentation-only successor. No Product source, executable test, workbook, dependency or lock file changed. **No executable test was run for this commit and none is claimed at this documentation head.** Predecessor evidence is recorded only.

| Check | Result |
|---|---|
| Boundary scan `node tools/boundary-scan/boundary-scan.mjs .` (S1–S9) | 0 findings; S1–S9 clean |
| `git diff --check` (worktree) and `git diff --cached --check` | Clean, no output |
| Changed files vs checkpoint `4babdce` | `CHANGELOG.md`, `README.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `docs/STAGE_0.4C-0_CHECKPOINT.md`. All within the allowlist. |
| Changed files vs base `29ee137` | Same five files (checkpoint added, four modified). |
| No-change proof (Product source, tests, config, tools, spikes, lock files, fixtures, dependencies, Runtime, Mapping, Galil/MODBUS) | No file changed in any of these areas, against either `4babdce` or `29ee137`. |
| Workbook `T8_IO_Card_Mapping.xlsx` SHA-256 | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, unchanged |
| Relative links and anchors in the changed and linked docs | 371 relative links checked, 0 broken. The four `DOMAIN_MODEL.md` §2.2.1 links now use the canonical anchor. Anchor check used a local GitHub-style slug approximation, not the GitHub renderer: **NOT VERIFIED in the GitHub renderer**. |
| Executable tests (Node/.NET) | **Not run.** Documentation-only change; the Owner instructed no test re-run for documentation changes. |
| Clean-tree proof | Recorded after the successor commit (see final report). |

Predecessor evidence (Stage 0.4B-3, not executed at this head): Mapping package 268 tests (268/268 with the authoritative workbook supplied); Mapping UI 82/82; Owner Browser Review PASSED (Owner-reported, presentation only). Not re-run at the new documentation head.

## 18. Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates
- [`STAGE_0.4B-3_CHECKPOINT.md`](STAGE_0.4B-3_CHECKPOINT.md) — predecessor evidence
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — logical Sensor matrix and gap anchors
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — hardware safety boundary
- [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) — publication boundary
