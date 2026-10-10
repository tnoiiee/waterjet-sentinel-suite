# Stage 0.4C-0 — Physical Sensor Readiness, Loop-Test Requirements and Actual I/O Evidence Gate (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT — documentation and contract proposal only, SUBMITTED FOR OWNER REVIEW.** Nothing is implemented, tested on hardware, commissioned or merged. Stage 0.4C-1 is **PROPOSED and BLOCKED** (see §0 and §15). **PR OPEN — NOT MERGED.**

Stop point: this checkpoint. The Owner merges; the agent does not.

**Honesty lines:** NEW SESSION USED. APPROVED REMOTE-MAIN BASE VERIFIED. PHYSICAL SENSOR ACQUISITION NOT IMPLEMENTED. WIRE-BREAK DECODING NOT IMPLEMENTED. SENSOR COMMISSIONING MONITOR NOT IMPLEMENTED. ACTUAL PROCESS-IMAGE OFFSETS NOT VERIFIED. AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. Thermocouple acquisition modules are NOT represented by the authoritative workbook (CF-01). Candidate addresses remain non-authoritative. No Sensor is commissioned by this Stage. PR OPEN - NOT MERGED.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0 |
| Session type | New session after merged PR #15 |
| Session branch (platform-assigned) | `arena/fecda529-waterjet-sentinel-suite` |
| Approved remote-main base | `main` = `29ee137c1df617a1aa3321c3c891161d5a5bb41a` (PR #15 merge commit). Verified by `git ls-remote origin refs/heads/main` before any edit. |
| PR #15 merge parents | `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` (parent 1) and `8b479772d4fbef63b8f8eb34f59a1f2eba69618e` (parent 2) — confirmed with `git cat-file -p 29ee137c…` |
| PR #15 state | `MERGED` at `2026-10-10T18:21:35Z`, head branch `arena/00483ae4-waterjet-sentinel-suite` (not used, continued or pushed in this session) |
| Session branch position | Points at `29ee137c…` at session start; index and worktree clean |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — **verified this Stage, unchanged** |
| Conflicting open PR | None (`gh pr list --state open` returned an empty list) |
| Scope touched | `docs/STAGE_0.4C-0_CHECKPOINT.md` (new), `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `CHANGELOG.md` |

## 0. Gate outcome and stop conditions

- **Base gate: PASSED.** Remote `main`, merge-parent identity, PR #15 `MERGED`, session branch position, clean index and worktree, workbook SHA-256 and absence of a conflicting open PR were all checked (§1). The standing sandbox recreation protocol was **not** triggered: HEAD, the session branch ref and the index were consistent.
- **STOP-1 triggered — Thermocouple acquisition modules are not in the authoritative workbook (CF-01).** The Product model requires 212 Thermocouple measurement Channels for 106 Sensors (PHY-002). The authoritative workbook lists 23 rack modules and 104 tagged I/O channels, and **none of them is a thermocouple input module**. No thermocouple module, TC type, Sensor-to-Channel schedule or Sensor-to-tag mapping exists in any evidence available to this Stage. Per the Stage rule, **no module or mapping has been invented**, and the affected parts are marked **BLOCKED**: thermocouple Channel binding, thermocouple pilot selection, thermocouple rollout binding and the Stage 0.4C-1 thermocouple acquisition scope.
- **Unaffected parts were completed:** the evidence inventory, the gap register, the commissioning identity model, the quality and diagnostic contract proposal, the invalid-value rules, the Loop-Test workflow, the Commissioning Monitor and record-schema proposals, the read-only architecture proposal, the Galil G1–G5 checklist and the Stage 0.4C-1 scope proposal. Each one carries its own status.
- **No Product source, executable test, workbook, dependency or lock file was changed.** No device, network, MODBUS, TEST_HARDWARE or Hardware path was touched.

## 1. Authoritative baseline carried forward (protected)

Items marked **Verified this Stage** were re-derived from the authoritative workbook with a read-only parse (zip/XML, no installed packages). Items marked **Carried** come from repository records and were not re-derived by this documentation-only Stage.

| Protected item | Value | Status |
|---|---|---|
| Logical positions | 108 (18 logical columns × 6 logical rows) | Carried (README, DOMAIN_MODEL §2.2.1) |
| Physical Sensor locations | 106 (Left 24, Rear 29, Right 24, Front 29) | Carried (PHY-001) |
| Thermocouple measurement Channels | 212 (TC_F and TC_R per Sensor; 48 + 58 + 48 + 58 by wall) | Carried (PHY-002, PHY-003) |
| I7 and I16 | `NON_SENSOR_GAP` placement anchors; I7 anchors WJ3, I16 anchors WJ1; never Sensors, never Water Jets | Carried (DOMAIN_MODEL, README) |
| WJ1–WJ8 | Equipment entities, separate from the Sensor matrix; pair 1:1 with IV1–IV8 by ordinal | Carried (ADR-0017) |
| Legacy cannon data | Means Assigned Cleaning Device ID; never inferred from Sensor wall location | Carried |
| AI-002 | Pump Inlet Pressure; 4–20 mA; 0–40 bar; diagnostic and trend only; not Pump-ready; not Valve diagnosis; no alarm, trip or interlock authority | Carried (Owner ruling 2026-10-10, STAGE_0.4B-1) |
| AI-003 | Pump Outlet Pressure; 4–20 mA; 0–40 bar; the only pre-P1 Pump-ready pressure source | Carried (Owner ruling); workbook label conflict recorded as CF-03 |
| Rack modules | 23 (RackSlot 1–23) | **Verified this Stage** |
| Workbook rows | 114 data rows: 108 `USED`, 6 `SPARE` | **Verified this Stage** |
| Tagged channels | 104 (DI 37, DO 31, AI 35, AO 1); the 4 non-process modules carry no tag | **Verified this Stage** |
| Reserved unresolved rows | 18 rows whose tag is `XXX` and whose note reads "pending reply" | **Verified this Stage** |
| Non-process modules | 750-362, 750-601, 750-613, 750-600 — no Process I/O address, `NOT_APPLICABLE` | **Verified this Stage** (workbook rows); address status carried |
| Candidate process layout | 19 contributing modules and 110 channels (750-430 ×5 = 40, 750-530 ×4 = 32, 750-471 ×9 = 36, 750-554 ×1 = 2) | **Verified this Stage** (counted from workbook); `CANDIDATE_UNVERIFIED`, offsets null |
| Mapping defaults | 26 enabled default bindings, no seed; 18 reserved unresolved rows | Carried (Stage 0.4B-1/0.4B-3) |
| Boundary scan | S1–S9 | **Verified this Stage** at the session baseline: 0 findings, exit 0 |

## 2. Evidence inventory (§9 of the Stage task)

### 2.1 Classification legend

`OWNER_WORKBOOK_FACT` · `PRIMARY_MANUFACTURER_MODEL_FACT` · `ACTUAL_RACK_INSTANCE_EVIDENCE` · `ACTUAL_CHANNEL_EVIDENCE` · `OWNER_STATEMENT` · `PROVIDED_UNVERIFIED` · `NOT_PROVIDED` · `CONFLICTING_EVIDENCE`

`PROVIDED_UNVERIFIED` is a **state**, not a category: it means the item was provided by the Owner but its content could not be checked in this sandbox. Each item below has one category and one state.

### 2.2 Evidence items

| ID | Evidence | Category | Scope | Presence in sandbox / hash | State and note |
|---|---|---|---|---|---|
| EV-01 | Authoritative workbook `T8_IO_Card_Mapping.xlsx` (repository root) | OWNER_WORKBOOK_FACT | Complete rack topology (23 modules, RackSlot 1–23) and channel-tag list. Contains tags, Signal/Description, I/O type, Location and a note column. It contains **no** thermocouple module, **no** Sensor identity, **no** wiring, **no** process-image offset. | In repository. SHA-256 `4e0337e2…f422e8e` verified this Stage. | Authoritative for topology and tags as recorded. Does **not** establish physical wiring, Channel configuration or process-image position. The note column contains field-wiring descriptions for some channels: these are workbook notes, not verified wiring. |
| EV-02 | Primary manufacturer manuals for 750-362, 750-430, 750-530, 750-601, 750-613, 750-471, 750-554, 750-600 | PRIMARY_MANUFACTURER_MODEL_FACT | Model level only | Owner-provided outside the repository. **Not in sandbox.** No SHA-256 recorded; none invented. | `PROVIDED_UNVERIFIED`. Model-level facts as recorded by Stage 0.4B-3 (`moduleProfiles.mjs`): 750-430 eight input bits; 750-530 eight output bits; 750-471 four 16-bit input values with status/diagnostic representation dependent on Coupler and configuration; 750-554 two 16-bit output values. |
| EV-03 | Owner-provided WAGO-I/O-CHECK rack sequence screen (visible Pos. 01–20) | ACTUAL_RACK_INSTANCE_EVIDENCE | **Partial screenshot only.** Covers 20 of 23 modules. No entry for 750-362, 750-601 or 750-613. | Owner-provided outside the repository. **Not in sandbox.** No hash. | `PROVIDED_UNVERIFIED`. As recorded by Stage 0.4B-3, Pos. 01–20 is fully accounted for by 5 DI, 4 DO, 9 AI, 1 AO and the End module. Position numbers are configuration-tool positions, not RackSlots. |
| EV-04 | Owner-provided configuration screen: 750-471 at Pos. 10 (RackSlot 13, `AI-MODULE-01`), **Channel 3 only** | ACTUAL_CHANNEL_EVIDENCE | **INSTANCE_AND_CHANNEL_ONLY.** Not generalised to Channels 1, 2 or 4, to any other 750-471 instance, or to any 750-554 Channel. | Owner-provided outside the repository. **Not in sandbox.** No hash. Values as recorded in `packages/mapping-config/src/actualRackEvidence.mjs`. | `PROVIDED_UNVERIFIED`. Recorded settings: signal type 4-20 mA; input filter Off; channel diagnosis On; **wire-break diagnosis On**; overload, measuring-range overflow and underflow, and user-limit diagnoses On; user limits 32767 / −32768; displayed type `4AI U/I Diff Galv`; displayed version `01.01.46(04)`. The screen does **not** show the open-circuit output representation, the status byte layout or the scaling result. |
| EV-05 | Owner statements: manuals available for all eight models; actual rack positions available; one 750-471 Channel configuration available | OWNER_STATEMENT | Statements only | Not in repository | Recorded as statements. They verify nothing beyond what is in EV-02 to EV-04. |
| EV-06 | Actual Process Data view or export: Input and Output process image, offsets, word or byte grouping, module positions, status and control fields | NOT_PROVIDED | — | — | Required for any live read. Offsets remain unresolved. |
| EV-07 | Actual field-network mapping; head-station (750-362) firmware revision, hardware revision and I/O Config | NOT_PROVIDED | — | — | Required for transport and for head-station profile. |
| EV-08 | 750-471 Common settings: data format, status-information setting, module-level process-data options | NOT_PROVIDED | — | — | Required per model instance. |
| EV-09 | 750-471 Scaling screen: raw representation, user scaling, channel-specific scaling | NOT_PROVIDED | Only the user-limit values of EV-04 are visible, and their role is not verified | — | Required for engineering conversion. |
| EV-10 | 750-471 channel-configuration consistency across the nine instances and 36 Channels | NOT_PROVIDED | Only one Channel (EV-04) is verified | — | Required before any 750-471 Channel is treated as configured. |
| EV-11 | 750-554 settings: data format, signal mode, Channel settings, output scaling | NOT_PROVIDED | — | — | Not a Sensor input. Required only where the output is relevant to Sensor readiness (for example AO-001 feeding the inverter). |
| EV-12 | Thermocouple input-module identity; TC type by Channel; cold-junction compensation settings; wire-break configuration by Channel | **CONFLICTING_EVIDENCE** (module identity, see CF-01); other items NOT_PROVIDED | — | — | The workbook has no thermocouple module. The product requires 212 thermocouple Channels. No model is named here, and none is inferred. |
| EV-13 | Sensor-to-Channel schedule; terminal schedule; wiring drawings | NOT_PROVIDED | — | — | Required for every Loop-Test step 3 and 4. |
| EV-14 | Expected normal values; observed value during disconnection; any commissioning record | NOT_PROVIDED | — | — | No measured value, observation or photograph of a physical Sensor is present in the repository or the sandbox. |
| EV-15 | Workbook column `Ref.` (values 1.26 to 1.79 on 15 distinct values) | OWNER_WORKBOOK_FACT | Values only | In repository | **Meaning NOT_PROVIDED.** It is not interpreted as a drawing or sheet reference. Required before the wiring-drawing link can be used. |
| EV-16 | Owner-confirmed physical structure: walls, 106 Sensor locations, 108 logical positions, TC_F and TC_R sides, I7 and I16 gap anchors | OWNER_STATEMENT | Logical and domain structure. **Not** a physical verification of any Sensor, wire or Channel. | In repository (PHY-001 to PHY-003; `WallMap.cs`; DOMAIN_MODEL §2.2.1) | Domain-confirmed. Per-Sensor physical verification is NOT_PROVIDED. |
| EV-17 | Owner rulings (2026-10-10) on pressure identities: AI-002 Pump Inlet; AI-003 Pump Outlet; AI-004 to AI-011 as IV1 to IV8 outlet pressure, with the label rule `WSB Pressure transmitter #n = IVn` | CONFLICTING_EVIDENCE (AI-003 label), with an OWNER_STATEMENT ruling | Ruling governs identity in the product. The workbook text is unchanged. | Repository record (STAGE_0.4B-1); enforced in Mapping tests | The workbook description of AI-003 reads "Pressure Transmitter Main Valve Outlet". The ruling says Pump Outlet. This is recorded as **CF-03**, not re-decided here. The workbook descriptions of AI-004 to AI-011 read "WSB Pressure transmitter #1–#8", which the ruling maps to IV1–IV8. |
| EV-18 | Product-tree Sensor and Channel model (synthetic examples only) | Not Owner evidence of installed hardware | Synthetic identities only (`SYN-TC-nn:CHmm`, example slot IDs) | `config/examples/sensor-map.example.json` (labelled example) | Shows the **shape** of a Sensor record. It is not a production map and is not evidence of any real Sensor-to-Channel binding. |

### 2.3 Facts by scope

| Fact | Scope | Source | Permitted use |
|---|---|---|---|
| 750-471 is a four-Channel analog-input model with four 16-bit input values | MODEL | EV-02 | Model profile only. Not an instance or Channel setting. |
| 750-430 has eight input bits; 750-530 has eight output bits; 750-554 has two 16-bit output values | MODEL | EV-02 | Model profile only |
| RackSlot 1–23 with model and channel tags | COMPLETE RACK (topology) | EV-01 | Topology and tag identity, not wiring or offset |
| Pos. 01–20 configuration-tool positions | PARTIAL SCREEN | EV-03 | Instance position for 20 of 23 modules only |
| 4-20 mA, wire-break diagnosis **enabled**, measuring-range underflow diagnosis **enabled** | CHANNEL (Pos. 10 / RackSlot 13 / Channel 3) | EV-04 | Configuration of that one Channel only. **Enablement is not an observed diagnostic.** |
| Open-circuit output representation, status byte layout, scaling result | NONE | — | **Not verified.** |
| Process-image offsets, byte and word grouping | NONE | — | **Not verified.** Candidate offsets are `OFFSET_UNRESOLVED`. |
| 750-362 firmware and hardware revision | NONE | — | **Not verified.** |
| Thermocouple module identity and Channel settings | NONE | — | **Not verified. Blocked (CF-01).** |

### 2.4 Not inferred (binding)

No evidence hash, firmware identity, revision interpretation, process-data offset, word order, byte order, diagnostic encoding, Channel setting or Channel-to-Sensor binding has been inferred or invented. Settings verified for one Channel do not apply to another Channel, another instance or another model.

### 2.5 Risk RISK-01 — location coincidence on the only Channel-level settings record

The one Channel-level configuration record (EV-04: Pos. 10 / RackSlot 13 / Channel 3) sits at the **workbook location of AI-003** (RackSlot 13, Channel 3, Pump (L Floor), "Pressure Transmitter Main Valve Outlet"). AI-003 is the sole pre-P1 Pump-ready pressure source.

This is a **location coincidence, not a tag verification**. The record verifies the settings of physical Channel 3 of the instance at Pos. 10. That Channel 3 carries AI-003 is a workbook fact, not a wiring or terminal verification. Before any use of that Channel for commissioning, the terminal schedule and wiring drawing (EV-13) must confirm the tag. The Owner must also confirm the AI-003 identity (CF-03). **No Pump-readiness conclusion is drawn from EV-04.**

## 3. Gap register and remaining evidence requests (§10 of the Stage task)

Priority: **P0** blocks any live acquisition design or any Stage 0.4C-1 implementation; **P1** blocks a pilot Loop-Test; **P2** blocks the full rollout or a non-pilot Channel.

| Gap | Missing evidence | Needed for | Priority | Status |
|---|---|---|---|---|
| GAP-01 | Thermocouple input-module identity, model and revision, and the module's place in the rack | TC Channel binding, every TC Loop-Test | **P0** | BLOCKED by CF-01 |
| GAP-02 | Sensor-to-Channel schedule (106 Sensors to 212 TC Channels, with module, RackSlot and Channel) | Identity binding, rollout | **P0** | NOT_PROVIDED |
| GAP-03 | Actual Process Data view or export: Input process image with offsets, byte and word grouping, module positions | Any live read (EV-06) | **P0** | NOT_PROVIDED |
| GAP-04 | Head-station (750-362) exact firmware and hardware revision; I/O Config; field-network mapping (EV-07) | Transport and head-station profile | **P0** | NOT_PROVIDED |
| GAP-05 | Status and diagnostic representation for each module model in use: explicit Channel diagnostic, module diagnostic, status bit or byte, special raw value | Wire-break classification (§6.3) | **P0** | NOT_PROVIDED |
| GAP-06 | 750-471 Common settings (data format, status information, module process-data options) | Every 750-471 Channel | **P1** | NOT_PROVIDED |
| GAP-07 | 750-471 Scaling (raw representation, user and channel scaling) | Engineering value (EV-09) | **P1** | NOT_PROVIDED |
| GAP-08 | 750-471 Channel-configuration consistency across nine instances and 36 Channels (EV-10) | Each 750-471 Channel | **P1** | Only Pos. 10 / Channel 3 verified |
| GAP-09 | Thermocouple type by Channel, cold-junction compensation settings, wire-break configuration by Channel (EV-12) | TC measurement and diagnosis | **P0** | BLOCKED by CF-01 |
| GAP-10 | Wiring drawings and terminal schedule, with the meaning of workbook `Ref.` (EV-13, EV-15) | Loop-Test steps 3 and 4 | **P1** | NOT_PROVIDED |
| GAP-11 | Expected normal values for each pilot Channel | Baseline PASS criteria | **P1** | NOT_PROVIDED |
| GAP-12 | Observed value during a disconnection (open-circuit observation) | Diagnostic verification | **P1** | NOT_PROVIDED |
| GAP-13 | 750-554 settings (EV-11) | AO-001 only where relevant | **P2** | NOT_PROVIDED |
| GAP-14 | Revisions of the eight other 750-471 instances; revision evidence beyond Pos. 10 | Revision-boundary pilot (§12) | **P2** | NOT_PROVIDED |
| GAP-15 | Owner-approved Commissioning procedure and LOTO procedure | Every field test | **P0** | NOT_PROVIDED — no field step is authored here |
| GAP-16 | Owner decision on whether the 35 workbook analog channels (AI-001 to AI-035) are in Physical Sensor commissioning scope (OQ-02) | Pilot and rollout population | **P1** | Owner decision |

## 4. Conflict register (STOP record)

| ID | Conflict | Governing sources | Consequence | Status |
|---|---|---|---|---|
| **CF-01** | **The Sensor population the product models (106 Sensors, 212 thermocouple Channels) is not represented in the authoritative workbook.** The workbook has 23 modules, none of them a thermocouple input module. Its analog population is 35 tagged 4–20 mA and 0–20 mA channels: level, pressure and blower-current devices as described (AI-001 to AI-019), and AI-020 to AI-035 annotated as pending reply. No document available to this Stage maps any Sensor to any workbook tag or module. | PHY-002 and the product model vs EV-01 | STOP for TC Channel binding, the TC pilot, the TC rollout and TC acquisition in Stage 0.4C-1. No module or mapping invented. | **OPEN — STOP** (Owner decision OQ-01) |
| **CF-02** | Whether the 35 workbook analog channels are inside the Physical Sensor commissioning population, or are separate process analog measurements, is not stated anywhere. | EV-01 vs PHY-001/PHY-002 | Determines the pilot population (§12). | OPEN (OQ-02) |
| **CF-03** | AI-003 is ruled "Pump Outlet Pressure" (STAGE_0.4B-1, Owner ruling 2026-10-10), but the workbook description reads "Pressure Transmitter Main Valve Outlet". The Mapping import test accepts the description text without an error when the identifier matches the Owner table (`pumpIdentity.test.mjs`). | EV-17 vs EV-01 | The Owner ruling governs product identity. The label conflict must be resolved physically (wiring, drawing) before any Pump-ready use or open-circuit test on that Channel. | OPEN — Owner confirmation requested (OQ-03). Not re-decided here. |
| **CF-04** | The required quality taxonomy (11 values, including `INVALID`, `WIRE_BREAK`, `SUSPECTED_WIRE_BREAK`, `MODULE_FAULT`, `COMMUNICATION_LOSS`, `NOT_MAPPED`, `NOT_COMMISSIONED`) does not match the existing `Quality` contract, which is `GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED` (`packages/contracts/Enums.cs`; `wjss-contracts-ts/types.ts`). `BAD` and `DISABLED` have no one-to-one counterpart. | Task §4 vs Product contract | No contract is renamed in this Stage. Any change to `Quality` requires an Owner decision and a Product gate. | OPEN (OQ-04) |
| **CF-05** | Workbook descriptions of AI-004 to AI-011 read "WSB Pressure transmitter #1–#8"; the Owner ruling and the Mapping label rule treat them as IV1–IV8 outlet pressure. | EV-17 vs EV-01 | Carried as an Owner-ruled interpretation. Commissioning must confirm each physical device. | OPEN — confirm at commissioning (GAP-10) |
| **CF-06** | README.md still describes the Stage 0.4B-3 position with PR #15 open. PR #15 is now merged. | Repository documentation | README is outside this Stage's expected file list, so it was **not** edited. The Owner is asked to authorise a narrow README status correction. | OPEN — documentation-only follow-up |

Open Owner decisions (none has been assumed):

- **OQ-01** Thermocouple acquisition: provide the thermocouple module identity and its rack position, and the Sensor-to-Channel schedule. If a corrected authoritative workbook is issued, it needs a new Owner-approved revision and hash. Until then CF-01 remains a STOP.
- **OQ-02** Is the workbook analog population (AI-001 to AI-035) inside the Physical Sensor commissioning scope?
- **OQ-03** Confirm AI-003 identity (Pump Outlet Pressure against the workbook label), and confirm the physical location and wiring before any use.
- **OQ-04** Quality vocabulary: one axis or two axes; mapping of `BAD` and `DISABLED`; whether `NOT_COMMISSIONED` is a quality value or a commissioning gate.
- **OQ-05** Pilot population: thermocouple Sensors after CF-01, or workbook analog channels; whether a pressure channel may be an open-circuit candidate.
- **OQ-06** Owner-set values: staleness threshold, stabilisation rule, batch size, value tolerances. None has been set or invented.
- **OQ-07** Sensor-level rule: is a Sensor commissioned only when both TC_F and TC_R pass?
- **OQ-08** Who authors and approves the Commissioning and LOTO procedures, and under what authority the field stimulus is applied.

## 5. Sensor commissioning identity model (§4 of the Stage task)

### 5.1 Required identity fields

| Field | Definition | Source today | Status |
|---|---|---|---|
| Sensor ID | Canonical identity of one Sensor location | Product `SensorId` contract field; production values not in repository | Field exists; production values NOT_PROVIDED |
| Physical wall | Left, Rear, Right or Front (boiler wall name) | PHY-001; `Wall` enum | Domain-confirmed; per-Sensor physical verification NOT_PROVIDED |
| Physical location | Wall column and wall row within the wall | Product `WallColumn`, `WallRow` | Structure Owner-confirmed; per-Sensor verification NOT_PROVIDED |
| Matrix row and logical column | Logical row 1–6 and logical column 1–18, including the two gap positions | Product `LogicalRow`, `LogicalColumn` | Structure confirmed; I7 and I16 are gaps |
| Measurement identity | The Sensor's measurement (thermocouple pair per PHY-002) | PHY-002 | Domain fact |
| Channel side | `TC_F` (front channel) or `TC_R` (rear channel). This is the thermocouple side, and it is **not** the wall name (PHY-003). | PHY-002, PHY-003 | Domain fact |
| Workbook I/O tag | Tag such as AI-002, for workbook channels only | EV-01 | **NOT_PROVIDED for any thermocouple Channel (CF-01)** |
| ModuleInstanceId | Physical module-instance identifier | Workbook has module per RackSlot; instance IDs such as `AI-MODULE-01` appear in the Product actual-rack record | Workbook modules only; thermocouple modules NOT_PROVIDED |
| Module model | Model number, for example 750-471 | EV-01 | Workbook modules only |
| RackSlot | One-based physical position, including Power Supply and End modules | EV-01 | Workbook modules only |
| IO-CHECK Position | Configuration-tool position number | EV-03, EV-04 | Pos. 01–20 partially visible; Pos. 10 verified for one Channel |
| Physical Channel | Channel number on the module | EV-01 Channel column | Workbook modules only |
| Signal type | For example 4-20 mA; for thermocouples, the TC type | EV-01, EV-04 | 4-20 mA recorded for Pos. 10 / Channel 3; thermocouple TC type NOT_PROVIDED |
| Configured measurement type | The type configured on the Channel | EV-04 (partial) | NOT_PROVIDED for most Channels |
| Engineering unit | Unit of the scaled value | EV-01 (bar for AI-002 and AI-003); other AI channels UNCONFIGURED | Pressure units Owner-confirmed; thermocouple unit NOT_PROVIDED |
| Engineering range | Span of the scaled value | AI-002 and AI-003: 0–40 bar (Owner-confirmed); IVn pressure UNCONFIGURED | Thermocouple range NOT_PROVIDED |
| Evidence basis | Enumerated evidence reference(s) for every field above | §2 evidence IDs | Required per field |
| Commissioning state | Enumerated, §5.3 | Future record | No Sensor is commissioned |

### 5.2 Required future live-observation fields

Raw process value · raw diagnostic or status representation · scaled engineering value · acquisition timestamp · value age · measurement quality · diagnostic reason · communication state · last valid value · last valid timestamp · recovery state. **None of these is implemented.** Their definitions are in §6 and §11.

### 5.3 Commissioning state (proposal, [PROPOSED])

`NOT_MAPPED` (no verified binding) → `MAPPED_UNVERIFIED` (binding recorded, evidence incomplete) → `COMMISSIONING_IN_PROGRESS` → `COMMISSIONED_PENDING_REVIEW` → `COMMISSIONED` (only after recorded Owner sign-off). A Sensor value existing never moves a Sensor forward. This state is **separate** from the reading quality in §6.

### 5.4 Identity rules (binding for every later stage)

1. Sensor, Channel and I/O tag are three different identities. None is derived from another without recorded evidence.
2. Wall name and TC side are different concepts (PHY-003).
3. RackSlot, configuration-tool position and ProcessModulePosition are three separate fields and are never aliased.
4. I7 and I16 carry no Sensor, no thermocouple Channel and no Water Jet identity. They are placement anchors only.
5. Assigned Cleaning Device is never inferred from a Sensor's wall location.
6. Actual-rack evidence belongs to the module instance and Channel it was observed on.
7. Commissioning evidence is never written into Runtime control state.

## 6. Quality and diagnostic contract proposal (§4 and §11.D)

Everything in this section is **[PROPOSED]**. No value here is implemented, and the vocabulary is subject to OQ-04 and CF-04.

### 6.1 Acquisition layers (separate concerns)

- **Transport state** — `CONNECTED`, `DISCONNECTED`, `RECONNECTING`, `COMMUNICATION_FAULT`. It describes the link and nothing else.
- **Channel acquisition** — raw word or value, diagnostic or status representation, monotonic acquisition revision, acquisition timestamp, staleness.
- **Scaling** — configured signal type, configured range, engineering unit, conversion basis, invalid-input policy.
- **Quality classification** — quality, diagnostic reason, explicit or inferred basis, recovery state, last valid history.
- **Runtime publication** — one atomic Sensor observation per update; no control transition.
- **Commissioning evidence** — a separate store (§10); deterministic revision; operator verdict; no Hardware write.

### 6.2 Quality taxonomy (required values)

| Quality | Meaning | Current engineering value | Automatic Queue or Cleaning use | Basis |
|---|---|---|---|---|
| `GOOD` | Current reading valid, Channel mapped, configured and commissioned | Shown | Eligible only when every other existing gate passes (not decided here) | Explicit |
| `UNCERTAIN` | A reading exists but its validity is not established (for example during stabilisation) | Shown, labelled | Not eligible | Explicit or inferred, stated |
| `INVALID` | No current usable value | **Null** (never zero) | Not eligible | Explicit or inferred, stated |
| `WIRE_BREAK` | An **explicit** wire-break diagnostic is evidenced | **Null** | Not eligible | Explicit only |
| `SUSPECTED_WIRE_BREAK` | An open circuit is **inferred**, not explicit | **Null** | Not eligible | Inferred only |
| `OUT_OF_RANGE` | Measured or diagnosed outside the configured range, and not a wire break | Not clamped; shown as out of range | Not eligible | Explicit diagnostic or stated value comparison |
| `MODULE_FAULT` | Explicit module-level fault or diagnostic | **Null** | Not eligible | Explicit module diagnostic |
| `COMMUNICATION_LOSS` | Transport or head-station link lost | **Null** for the current value | Not eligible | Transport state |
| `STALE` | Last value older than the staleness rule. The staleness threshold is `[OPEN]` (OQ-06). | Last value only, labelled with age | Not eligible | Age rule |
| `NOT_MAPPED` | No verified Sensor-to-Channel binding | **Null** | Not eligible | Identity |
| `NOT_COMMISSIONED` | Binding exists but commissioning is not recorded as complete | Raw only, labelled (proposal, OQ-04) | Not eligible | Commissioning record |

Required existing-contract mapping questions (not decided here, CF-04): `GOOD` maps to `GOOD`; `UNCERTAIN` and `STALE` exist in the current enum with its own semantics, which must be reconciled; `BAD` has no single equivalent (it could cover several INVALID-class values); `DISABLED` is not equivalent to `NOT_COMMISSIONED`.

### 6.3 Precedence (proposal)

When more than one condition holds, the **primary** quality is the first match in this order: `NOT_MAPPED` > `NOT_COMMISSIONED` > `COMMUNICATION_LOSS` > `MODULE_FAULT` > `WIRE_BREAK` > `SUSPECTED_WIRE_BREAK` > `INVALID` > `OUT_OF_RANGE` > `STALE` > `UNCERTAIN` > `GOOD`.

Secondary facts are **stored separately** and are never lost: `stale` (boolean), `communicationLoss` (boolean), `recoveryState`. `STALE` and `COMMUNICATION_LOSS` remain separate facts even when `COMMUNICATION_LOSS` is the primary quality.

### 6.4 Actual wire-break representation — determination

The task asks whether actual WIRE_BREAK is represented by one of six mechanisms. Based only on the evidence in §2:

| Candidate representation | Evidence available | Determination |
|---|---|---|
| Explicit Channel diagnostic | EV-04 shows wire-break diagnosis **enabled** on one 750-471 Channel. No open-circuit observation exists. | **Enablement verified for one Channel. Observed representation NOT VERIFIED.** |
| Explicit module diagnostic | Manual facts (EV-02) say the 750-471 status and diagnostic representation depends on Coupler and configuration. No module-level diagnostic is evidenced for any model. | **NOT VERIFIED** |
| Status bit or status byte | EV-08 (status-information setting) and EV-06 (byte and word order) are NOT_PROVIDED | **NOT VERIFIED** |
| Special raw value | No observation provided | **NOT VERIFIED** |
| Configured substitute value | No substitute behaviour evidenced. The user-limit values in EV-04 (32767 / −32768) are shown, but their role is not verified. | **NOT VERIFIED. Must never be treated as a measurement.** |
| Inference only | The only route currently available | **Permitted only as `SUSPECTED_WIRE_BREAK`** |
| Thermocouple modules | No module evidence (CF-01) | **NOT VERIFIED — BLOCKED** |

**Conclusion.** The evidence does not determine how an actual wire break is represented for any module in use. Therefore:

- WIRE-BREAK DECODING IS NOT IMPLEMENTED and is not proposed as verified.
- Any `WIRE_BREAK` classification is blocked until the Owner's commissioning evidence supplies the representation for the **model and firmware in use**.
- Until then, an open-circuit observation can at most be `SUSPECTED_WIRE_BREAK`. Nothing is classified as a confirmed wire break.

### 6.5 Explicit and inferred basis

A diagnosis is `EXPLICIT` only when the module or Channel reports it through a documented representation. A diagnosis is `INFERRED` when it is derived from a value alone. Value alone is **not** sufficient for `WIRE_BREAK`.

Illustration of why discrimination matters (arithmetic from the stated 0–40 bar, 4–20 mA span; not a measured value): 1 bar corresponds to 4.4 mA and 0 bar to 4.0 mA. An open loop carries no loop current, so the input would read below the 4 mA lower bound. That is the same region a measuring-range underflow diagnosis covers. The two conditions must therefore be separated by an **explicit** diagnostic, never by the value alone. An underflow without an explicit wire-break diagnosis is `OUT_OF_RANGE` or `SUSPECTED_WIRE_BREAK`, never `WIRE_BREAK`.

### 6.6 Diagnostic reason codes (proposal)

`DIAG_WIRE_BREAK_EXPLICIT_CHANNEL` · `DIAG_WIRE_BREAK_EXPLICIT_MODULE` · `DIAG_WIRE_BREAK_STATUS` · `DIAG_SPECIAL_RAW_VALUE` · `DIAG_OVERFLOW` · `DIAG_UNDERFLOW` · `DIAG_OVERLOAD` · `DIAG_USER_LIMIT` · `DIAG_MODULE_FAULT` · `DIAG_COMM_TIMEOUT` · `DIAG_STALE_AGE` · `DIAG_NOT_MAPPED` · `DIAG_NOT_COMMISSIONED` · `DIAG_INFERRED_OPEN_CIRCUIT`

Each code carries a `basis` field: `EXPLICIT`, `SPECIAL_RAW_DOCUMENTED`, `CONFIGURED_SUBSTITUTE_DETECTED`, `INFERRED` or `NONE`.

### 6.7 Recovery and stabilisation

- Recovery states (proposal): `NOT_RECOVERING` → `RECOVERY_PENDING_VALID_READING` → `STABILISING` → `RECOVERED`.
- A Channel leaves a non-GOOD state only after a **valid current reading** and the **explicit stabilisation rule**. Neither is an automatic clock. The stabilisation rule (count, window and duration) is `[OPEN]` (OQ-06). No value is invented.
- The reconnection of a transport enters `RECOVERING` and is not an automatic return to `GOOD`.

## 7. Invalid-value safety rules (§5 of the Stage task)

These rules bind every later stage. The test IDs are **planned, not executed**.

| Rule | Statement | Planned test (not executed) |
|---|---|---|
| INV-01 | `INVALID` must never silently become zero. A current invalid engineering value is null and is never rendered as `0`. | T-INV-01 |
| INV-02 | `WIRE_BREAK` means the current engineering value is unavailable or null, unless verified module behaviour explicitly requires another representation. | T-INV-02 |
| INV-03 | Last Valid Value is labelled historical and is never shown or used as the current measurement. | T-INV-03 |
| INV-04 | `COMMUNICATION_LOSS` remains separate from Channel `WIRE_BREAK`. | T-INV-04 |
| INV-05 | `STALE` remains separate from communication loss. | T-INV-05 |
| INV-06 | `OUT_OF_RANGE` remains separate from `WIRE_BREAK`. | T-INV-06 |
| INV-07 | `NOT_COMMISSIONED` remains separate from `INVALID`. | T-INV-07 |
| INV-08 | An inferred condition is never classified as a confirmed wire break. Inferred is `SUSPECTED_WIRE_BREAK`. | T-INV-08 |
| INV-09 | Invalid, stale, communication-loss and uncommissioned observations never trigger automatic Queue entry. | T-INV-09 |
| INV-10 | Invalid observations never enter Cleaning classification. | T-INV-10 |
| INV-11 | Invalid observations never satisfy Pump, Valve or motion readiness. | T-INV-11 |
| INV-12 | Recovery requires a valid current reading **and** the explicit stabilisation rule (OQ-06). | T-INV-12 |
| INV-13 | No automatic control authority is introduced by any observation, quality or commissioning record. | T-INV-13 |
| INV-14 | AI-002 remains diagnostic and trend only; it never satisfies Pump readiness, never acts as Valve diagnosis and has no alarm, trip or interlock authority. AI-002 Runtime publication stays NOT IMPLEMENTED. | T-INV-14 |
| INV-15 | Only AI-003 is the pre-P1 Pump-ready pressure source. Its readiness additionally requires every existing gate, and it is not changed by this Stage. | T-INV-15 |

## 8. Loop-Test workflow (§6 of the Stage task)

### 8.1 Preconditions (every test)

- The Owner has approved a **Commissioning procedure and a LOTO procedure** for the specific Sensor type and location (OQ-08, GAP-15). This document does not author field steps, does not prescribe a stimulus or an open-circuit method, and does not authorise any field manipulation.
- Identity bindings required by steps 1–4 are supplied (GAP-01, GAP-02, GAP-10). For thermocouple Channels, the workflow is **BLOCKED** until CF-01 is resolved.
- Recording only: PASS, FAIL and the other verdicts record evidence. They write nothing to hardware (§9).

### 8.2 The 22 steps

| # | Step | Required record | Gate or stop rule |
|---|---|---|---|
| 1 | Select the Sensor and measurement Channel | Sensor ID, Channel side, commissioning state | Sensor must not be `NOT_MAPPED`. TC: BLOCKED (CF-01). |
| 2 | Confirm Sensor identity and physical location | Wall, column and row, confirmed by the tester | Mismatch → STOP the test. |
| 3 | Confirm workbook tag, ModuleInstanceId, RackSlot and Channel | Each field with its evidence ID | Any field NOT_PROVIDED → BLOCKED. |
| 4 | Confirm wiring drawing and terminal schedule references | Drawing and schedule identifiers | Missing → BLOCKED. |
| 5 | Observe baseline raw and engineering values | Raw, scaled, timestamp, age, quality | A baseline is accepted only when quality is GOOD. |
| 6 | Confirm measurement quality is GOOD | Quality and reason | Not GOOD → NOT_TESTED or BLOCKED, with reason. |
| 7 | Apply the Owner-authorised field stimulus suitable for the Sensor type | Procedure identifier, stimulus recorded by the Owner | Method `[OPEN]` (OQ-08). |
| 8 | Confirm the selected Channel changes | Before and after values, direction | No expected change → FAIL. |
| 9 | Confirm adjacent or unrelated Channels do not change incorrectly | Before and after values of the neighbours defined from the mapping | Unexpected change → FAIL. |
| 10 | Perform the Owner-authorised open-circuit test | Procedure identifier, LOTO record | Procedure `[OPEN]`. No step is authored here. |
| 11 | Observe the explicit diagnostic or documented special representation | Raw value, status bits or bytes, diagnostic fields | None observed → record "no explicit representation". |
| 12 | Confirm WJSS quality changes to INVALID | Quality and reason | Not INVALID (or a stronger class) → FAIL. |
| 13 | Confirm diagnostic reason is WIRE_BREAK **when explicitly evidenced** | Diagnostic reason and basis | Not explicit → **PARTIAL**, never PASS. |
| 14 | Confirm the current engineering value is not shown as a valid zero | Displayed value | Any displayed `0` for an invalid value → **FAIL (critical)**. |
| 15 | Confirm Last Valid Value is labelled historical | Label text and timestamp | Missing label → FAIL. |
| 16 | Confirm the invalid Channel cannot influence Queue or Cleaning logic | Queue and Cleaning decision evidence | Any influence → **FAIL (critical)**. Future stage. |
| 17 | Restore the wiring | Restoration record, LOTO release, time and person | Procedure `[OPEN]`. |
| 18 | Confirm the diagnostic clears | Diagnostic fields after restoration | Remains → FAIL or STOP per procedure. |
| 19 | Confirm the measurement recovers | Current reading, quality | No valid reading → remain in recovery state. |
| 20 | Apply the stabilisation rule | Stabilisation record | Rule `[OPEN]` (OQ-06). |
| 21 | Record PASS, FAIL, PARTIAL, NOT_TESTED or BLOCKED | Verdict per step and per test | Verdict is evidence only. |
| 22 | Record tester, timestamp and remark | Tester, UTC timestamp, remark | Missing tester or timestamp → verdict not accepted. |

### 8.3 Verdict vocabulary

`PASS` · `FAIL` · `PARTIAL` · `NOT_TESTED` · `BLOCKED`. A `PARTIAL` is not a PASS. A `BLOCKED` is not a FAIL, but it is never counted as passed. Failed records are kept (append-only).

### 8.4 Progress across 106 Sensors and 212 Channels

Progress is tracked **per Channel** (212) and **per Sensor** (106). A Sensor-level verdict is derived from its two Channels. The derivation rule (for example, both TC_F and TC_R must pass) is `[OPEN]` (OQ-07). Nothing is commissioned because a value exists.

## 9. Commissioning Monitor requirements (design only; not implemented)

The smallest future Monitor is **read-only** with respect to Hardware. Its PASS, FAIL and other buttons record commissioning evidence only and never write to Hardware.

- **Overview fields:** Sensor ID; location; Channel identity; Rack, Slot and Channel; raw value; engineering value; unit; quality; diagnostic; communication state; last update; age; commissioning verdict.
- **Detail fields:** complete identity; mapping basis; raw observation; diagnostic and status observation; scaling basis; current quality; last valid history (labelled historical); evidence references; test history; tester remark.
- **Filters:** `NOT_TESTED`, `GOOD`, `INVALID`, `WIRE_BREAK`, `OUT_OF_RANGE`, `COMMUNICATION_LOSS`, `STALE`, `NOT_MAPPED`, `FAILED`.
- **Progress summaries:** total Channels; passed; failed; partial; blocked; not tested; active invalid; active wire break; communication loss; stale.
- **Behaviour rules:** a commissioning verdict never changes a quality, a Queue entry or a Runtime control state. An active `WIRE_BREAK` is shown as active only when it is explicit.

## 10. Commissioning record schema (§8 of the Stage task)

### 10.1 Four separate categories

1. **Runtime live state** — current observation. It is transient, is not commissioning evidence and is never edited by an operator.
2. **Historical commissioning evidence** — append-only record of baseline, stimulus, open-circuit and recovery observations, with evidence references.
3. **Operator-entered verdict** — verdict, tester, timestamp and remark. It is evidence about the test, not a measurement.
4. **Automatic measurement quality** — computed by the classifier (§6). It is never written by an operator and never used as a verdict.

### 10.2 Record fields (proposal; `[PROPOSED]`, not implemented)

```text
CommissioningRecord {
  schemaVersion                 // category: schema
  sessionId                     // historical evidence
  sensorId                      // identity
  measurementChannelId          // identity (TC_F or TC_R, or workbook tag where applicable)
  physicalMappingIdentity {     // identity, each field with evidence ID
    wall, logicalRow, logicalColumn, channelSide,
    workbookTag, moduleInstanceId, modelNumber, rackSlot, ioCheckPosition,
    physicalChannel, signalType, evidenceRevision
  }
  evidenceRevision              // historical evidence (deterministic revision of the mapping evidence)
  baselineRawValue              // historical evidence
  baselineEngineeringValue      // historical evidence (null when not GOOD)
  stimulusObservation           // historical evidence (procedure ID, observed change)
  openCircuitObservation        // historical evidence (procedure ID, observed raw and diagnostic)
  diagnosticObservation         // historical evidence (raw diagnostic representation, basis)
  recoveryObservation           // historical evidence (valid reading, stabilisation record)
  automaticQualityAtCapture {   // automatic measurement quality, read-only snapshot
    quality, diagnosticReason, basis, capturedAt
  }
  verdict                       // operator-entered verdict: PASS | FAIL | PARTIAL | NOT_TESTED | BLOCKED
  tester                        // operator-entered
  timestamp                     // operator-entered, UTC, ISO 8601
  remark                        // operator-entered, free text
  recordHash                    // deterministic hash of the canonical record (algorithm [OPEN], SHA-256 proposed)
}
```

Placeholders above are not values. No record in the repository is a commissioning record.

### 10.3 Deterministic export (proposal)

- **JSON:** UTF-8, LF line endings, fixed key order as in §10.2, ISO 8601 UTC timestamps, numeric values in invariant format, `null` written explicitly.
- **CSV:** fixed column order matching §10.2 flattened with dotted names. Rows sorted by wall (LEFT, REAR, RIGHT, FRONT), then logical row, then logical column, then channel side (`TC_F` before `TC_R`).
- Export includes `evidenceRevision` and `recordHash`. Export does not include the Runtime live state.

### 10.4 Boundary

Commissioning verdicts are **not** Runtime control state. They are not read by Queue, Cleaning, Pump, Valve or motion logic. Nothing in this schema writes to Hardware.

## 11. Read-only acquisition architecture (§11 of the Stage task; not implemented)

| Layer | Responsibility | Must not |
|---|---|---|
| A. Transport state | Report `CONNECTED`, `DISCONNECTED`, `RECONNECTING`, `COMMUNICATION_FAULT` | Write, command or force any output |
| B. Channel acquisition | Provide raw word or value, raw diagnostic or status, monotonic acquisition revision, timestamp, staleness | Infer a diagnosis that is not explicit |
| C. Scaling | Apply configured signal type, configured range and unit with a stated conversion basis. Invalid input gives null, never zero. | Clamp silently; use a substitute value as a measurement |
| D. Quality classification | Produce quality, diagnostic reason, explicit or inferred basis, recovery state, last valid history | Map an inferred condition to `WIRE_BREAK` |
| E. Runtime publication | Publish one atomic Sensor observation and Delta; no control transition; invalid values excluded from automatic classification and Queue eligibility | Publish AI-002 (not implemented) or create a Queue entry from an invalid value |
| F. Commissioning evidence | Separate store and model; deterministic revision; operator verdict | Feed Runtime control state; write Hardware |

No live acquisition is implemented in this Stage. Transport choice is **not made** here (§15).

## 12. Pilot Loop-Test plan (§12 of the Stage task)

### 12.1 Status

**BLOCKED for thermocouple Sensors by CF-01.** No thermocouple Sensor can be bound to a module, RackSlot, Channel or workbook tag. The pilot roles below are defined so that the Owner can choose candidates once the conflict is resolved (or once OQ-02 and OQ-05 are decided for workbook analog channels). **No pilot Sensor or Channel is selected by this Stage.**

### 12.2 Pilot roles

| Pilot | Role | Candidate population | Status |
|---|---|---|---|
| P-1 | Straightforward baseline behaviour | A thermocouple Sensor on a well-documented module, or a workbook channel whose expected value is documented (OQ-02, OQ-05) | BLOCKED (CF-01); no candidate selected |
| P-2 | A different physical wall or module from P-1 | Thermocouple Sensor on a different wall; or a workbook channel on a different RackSlot | BLOCKED (CF-01); no candidate selected |
| P-3 | An explicit wire-break diagnostic is expected | Only the Channel with wire-break diagnosis enabled in EV-04 is known (Pos. 10 / RackSlot 13 / Channel 3, see RISK-01). Its observed representation is NOT VERIFIED. | Candidate exists **only in the workbook-analog scope** and is subject to CF-03 and RISK-01. Owner decision required (OQ-05). |
| P-4 | A module or revision boundary | Thermocouple module boundary: BLOCKED. Workbook: the only legible 750-471 revision is for Pos. 10 (EV-04), so a boundary Channel on another instance is NOT VERIFIED. | BLOCKED for thermocouples; Owner selection for workbook analog |

### 12.3 Per-pilot specification (applies to P-1 to P-4)

| Attribute | Specification |
|---|---|
| Required evidence | Identity bindings (GAP-01, GAP-02, GAP-10); process image (GAP-03); head-station configuration and network (GAP-04); status and diagnostic representation (GAP-05); module configuration (GAP-06 to GAP-09 as applicable); expected normal values (GAP-11); Owner-approved Commissioning and LOTO procedures (GAP-15) |
| Baseline observation | Raw, scaled, unit, timestamp, age; quality must be GOOD; adjacent Channels recorded |
| Stimulus method | **Owner-approved procedure only, per sensor type.** Not specified in this document. |
| Open-circuit method | **Owner-approved procedure under LOTO only.** Not specified in this document. No field manipulation is instructed. |
| Expected diagnostic | Explicit wire-break diagnostic **only if** the representation is evidenced for that model and firmware (GAP-05). Otherwise: "no explicit representation observed". |
| Expected WJSS quality | `INVALID` with null engineering value. `WIRE_BREAK` only with an explicit diagnostic. Otherwise `INVALID` with `SUSPECTED_WIRE_BREAK`, and the step 13 verdict is `PARTIAL`. |
| Recovery expectation | Diagnostic clears after restoration; valid current reading; stabilisation rule applied (OQ-06) |
| PASS criteria | Steps 5–21 satisfied with observed records; step 13 PASS only with an explicit diagnostic; no step shows a valid zero; invalid Channel excluded from Queue and Cleaning (step 16 evidence recorded) |
| STOP criteria | Any unexpected change on a neighbouring Channel; any displayed valid zero for an invalid value; any unexpected output or Hardware change; LOTO not in force; identity mismatch; any requirement to write or force; any Pump or Valve state change; a Galil motion event |

## 13. 106-Sensor and 212-Channel rollout plan (§13 of the Stage task)

| Topic | Rule (proposal) |
|---|---|
| Population | 106 Sensors; 212 thermocouple Channels (2 per Sensor). Wall split: Left 24, Rear 29, Right 24, Front 29. I7 and I16 are never in the population. |
| Batch sequencing | By wall and area, following an Owner-approved sequence. Batch size `[OPEN]` (OQ-06). Gated by CF-01. |
| Wall and area grouping | Group by wall, then by logical row. Area grouping follows the Owner's physical access plan. |
| Auto-advance | After a recorded verdict on the current Channel, the Monitor proposes the next untested Channel in the approved sequence. It never records a PASS automatically. |
| Exception-only retest | Retest only FAIL, PARTIAL and BLOCKED records, and Channels whose evidence is invalidated by a change in the rack or configuration revision. |
| Progress tracking | Per Channel and per Sensor; summaries as §9. |
| Review and sign-off | Owner sign-off per batch and at final freeze. The agent never signs. |
| Failed-Channel handling | FAIL keeps the Channel out of commissioned state. The record is kept. Retest only after a recorded corrective action. |
| Blocked-test handling | BLOCKED needs a reason code and an owner. A BLOCKED item is never counted as passed. |
| Evidence export | Deterministic JSON and CSV per §10.3. |
| Final mapping freeze | After all required evidence and sign-off, freeze the Sensor-to-Channel-to-RackSlot map with a revision identifier. A later change invalidates only the affected records; it does not re-commission automatically. |
| Commissioned definition | A Sensor is commissioned only after recorded review. A value existing is never sufficient. |

## 14. Galil parallel readiness plan (G1–G5; no Galil control implemented)

This plan is parallel and separate. It does **not** expand the Sensor-readiness scope. Four Galil DMC-B140-M controllers are expected (PHY-005). Their network topology is `[OPEN]` and `[NOT VERIFIED]` (PHY-008). No controller is contacted by this Stage. No numeric limit, speed, timeout, pulse count or coordinate is defined here.

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
- Evidence prerequisites: G1 and G2 PASS; Owner-approved low-energy parameter set (NOT_PROVIDED, not invented).
- Safety prerequisites: physical supervision; emergency stop; LOTO and Owner-approved procedure.
- Allowed actions: Owner-approved low-energy motion on one axis under supervision.
- Prohibited actions: production coordinates, travel limits or speeds; multi-axis motion.
- Acceptance criteria: motion stays within the approved parameter set; stop and return are observed.
- STOP criteria: limit or fault event; unexpected motion; loss of communication.

**G4 — Motion command lifecycle, timeout, abort and fault handling**
- Evidence prerequisites: G3 PASS; Owner-approved timeout values (NOT_PROVIDED, not invented).
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
- STOP criteria: any Pump or pressure event; any unexpected motion; any concurrent Cleaning Job.

## 15. Stage 0.4C-1 proposed scope (PROPOSED and BLOCKED; not implemented)

**Status: PROPOSED. BLOCKED until CF-01 is resolved and the Owner decides OQ-04.** No file below is authorised by this checkpoint.

- **Scope name:** Read-only Physical Sensor Acquisition Foundation.
- **Preconditions:** CF-01 resolved (thermocouple modules and Sensor-to-Channel schedule supplied); EV-06 (process image) and EV-07 (head station) supplied; OQ-04 decided; the explicit wire-break representation for each model and firmware in use either evidenced or the scope limited to synthetic inputs.
- **Narrowest permitted alternative (for Owner choice):** synthetic-only classification with no transport. Scaling, quality, precedence and recovery rules are tested against synthetic fixtures, and nothing reads a device.
- **Exact Product files expected to change (PROPOSED, subject to approval):**
  - `packages/contracts/SensorAcquisition.cs` (new): observation, diagnostic and history contract records.
  - `packages/contracts/Enums.cs` (modified, only after OQ-04): quality and diagnostic vocabulary.
  - `packages/contracts/wjss-contracts-ts/types.ts`, `validate.mjs`, `test/validate.test.mjs` (modified): TypeScript mirror and validation.
  - `packages/contracts/fixtures/sensor-acquisition.synthetic.json` (new): synthetic fixture under the generated-fixture policy.
  - `packages/domain/SensorAcquisitionClassifier.cs` (new): pure scaling and quality classification; no I/O.
  - A new scoped test project (new): **requires a separate dependency and lock-file approval.** None is authorised here.
- **Not expected to change:** `apps/runtime` (Host integration stays under CP-3c-2 governance), Runtime publication, Inspector, every `adapters/*` project (no transport), `config/*`, the workbook, lock files, Galil and MODBUS code.
- **Transport choice:** none live in 0.4C-1. Input is replay of Owner-supplied, sanitised process-image snapshots (synthetic only in the repository). The live transport is **not chosen** here: it needs the field-network mapping (EV-07) and a separate TEST_HARDWARE Scope Gate.
- **No-write proof:** (a) the acquisition contract exposes no write, command or force member, checked by a test; (b) no socket, serial or MODBUS dependency is added, checked by a dependency scan; (c) a static search of changed files finds no write API; (d) boundary scan S1–S9 is clean.
- **Required process-image evidence:** EV-06 (Input process image with offsets and byte and word order), EV-08 to EV-10, EV-12.
- **Required diagnostic evidence:** the observed explicit diagnostic or special raw representation for each model and firmware, from Owner-approved commissioning evidence. A manual alone is not sufficient.
- **Required tests (planned, not executed):** T-SAQ-01 linear scaling at the configured range (synthetic); T-SAQ-02 below-range yields `OUT_OF_RANGE` or `SUSPECTED_WIRE_BREAK`, never `WIRE_BREAK`; T-SAQ-03 explicit fixture yields `WIRE_BREAK`; T-SAQ-04 inferred fixture yields `SUSPECTED_WIRE_BREAK`; T-SAQ-05 `INVALID` gives a null engineering value; T-SAQ-06 Last Valid Value labelled historical; T-SAQ-07 `STALE` separate from `COMMUNICATION_LOSS`; T-SAQ-08 `NOT_COMMISSIONED` separate from `INVALID`; T-SAQ-09 invalid input excluded from Queue eligibility; T-SAQ-10 recovery requires valid reading and stabilisation; T-SAQ-11 no write member in the contract; T-SAQ-12 deterministic serialisation and ordering; T-SAQ-13 AI-002 never satisfies Pump readiness.
- **Owner-local commands (planned, not run):** the scoped .NET test run with the Owner's SDK, and the TypeScript contract tests. No command has been run for this checkpoint.
- **TEST_HARDWARE Scope Gate requirement:** any live connection, polling or hardware-facing run needs a separate Owner Scope Gate defining network, tags, LOTO, supervision and rollback.
- **Rollback and disconnect behaviour:** on transport loss, all Channels on that transport become `COMMUNICATION_LOSS` with null current values; last valid values are kept as historical; no write is issued; on reconnection the state is `RECOVERING` until a valid reading and the stabilisation rule.
- **Stale-data behaviour:** the staleness threshold is `[OPEN]` (OQ-06). No timeout is invented. Stale values are shown only as `STALE` with age and are never eligible.
- **Quality propagation:** channel quality to Sensor quality (worst-of, subject to OQ-07) to the Queue and Cleaning eligibility input. Only `GOOD` and commissioned values are eligible, and every other gate still applies. AI-003 remains the only pre-P1 Pump-ready pressure source; this Stage does not change that.
- **Why this scope cannot activate control:** the contract is read-only; no transport write exists; no command is issued; no Runtime state transition results from an observation; invalid values create no Queue entry; no Pump, Valve or motion readiness is derived; AI-002 is not published; commissioning verdicts record evidence only.

## 16. Changed / Unchanged / Not Verified

**CHANGED**
- `docs/STAGE_0.4C-0_CHECKPOINT.md` — new; this checkpoint.
- `docs/CURRENT_STATE.md` — new current-position paragraph for Stage 0.4C-0; PR #15 recorded as merged at `29ee137c…`; the earlier 0.4B-3 position labelled historical.
- `docs/MASTER_PLAN.md` — stage-ledger entry for 0.4C-0; §3.6 status; PR #15 status corrected to MERGED where it was stated as open in current-status text.
- `CHANGELOG.md` — new dated entry for Stage 0.4C-0.

**UNCHANGED**
- `T8_IO_Card_Mapping.xlsx` (SHA-256 verified).
- All Product source: `packages/**`, `apps/**`, `adapters/**`, `tests/**`, `tools/**`, `config/**`, `spikes/**`.
- All executable tests and fixtures; `Directory.Build.props`, `Directory.Packages.props`, `WaterJetSentinelSuite.sln`, `global.json`; every lock file; all dependencies.
- Runtime sequencing, Pump and Valve logic, AutoSequence, Galil adapters, MODBUS adapters, production configuration.
- `README.md` (outside the expected file list; CF-06 follow-up).
- Boundary scanner and rules.

**NOT VERIFIED**
- Actual Process Data image, offsets, byte and word order (GAP-03).
- Actual field-network mapping and head-station firmware and revision (GAP-04).
- Explicit wire-break representation for any model or firmware (GAP-05). WIRE-BREAK DECODING NOT IMPLEMENTED.
- Thermocouple module identity, TC type, cold-junction compensation and per-Channel wire-break configuration (GAP-01, GAP-09; CF-01).
- 750-471 Common settings, Scaling and configuration consistency beyond Pos. 10 / Channel 3 (GAP-06 to GAP-08).
- 750-554 settings (GAP-13).
- Wiring drawings, terminal schedule and the meaning of workbook `Ref.` (GAP-10).
- Physical identity of AI-003 and of AI-004 to AI-011 against their rulings (CF-03, CF-05).
- Every manufacturer manual and the Owner's screens: not present in the sandbox; no hash recorded.
- Any Sensor's physical wiring, measured value, open-circuit behaviour or recovery behaviour. **No physical Sensor has been tested.** SENSOR COMMISSIONING MONITOR NOT IMPLEMENTED.
- Staleness threshold, stabilisation rule, batch size and tolerances (OQ-06).
- Any Owner-approved Commissioning or LOTO procedure (GAP-15).
- Galil controller identity, network and every motion parameter.
- Stage 0.4C-1 feasibility beyond this proposal.

## 17. Validation performed (this Stage)

Documentation-only Stage. No Product source, executable test or .NET or TypeScript project was changed, so **no executable validation is claimed and none was re-run.** The Stage 0.4B-3 results are recorded below as predecessor evidence only.

| Check | Command or method | Result |
|---|---|---|
| Remote main | `git ls-remote origin refs/heads/main` | `29ee137c1df617a1aa3321c3c891161d5a5bb41a` (approved) |
| Merge parents | `git cat-file -p 29ee137c…` | parents `3a73dfce…` and `8b479772…` (exact) |
| PR #15 state | `gh pr view 15` | `MERGED`, merge commit `29ee137c…` |
| Open PRs | `gh pr list --state open` | none |
| Session branch position | `git rev-parse` | `29ee137c…` at start |
| Index and worktree | `git status`, `git diff --cached --quiet`, `git diff --quiet` | clean at start |
| Workbook SHA-256 | `sha256sum T8_IO_Card_Mapping.xlsx` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` (exact; unchanged) |
| Workbook counts | read-only zip/XML parse | 23 modules; 114 rows; 108 USED; 6 SPARE; 104 tagged (DI 37, DO 31, AI 35, AO 1); 18 `XXX` rows; 110 candidate channels |
| Boundary scan S1–S9 | `node tools/boundary-scan/boundary-scan.mjs .` | 0 findings, exit 0 — run on the final content before commit |
| Whitespace | `git diff --check` and `git diff --check 29ee137c…` | exit 0 for both — run on the final content before commit |
| Relative links in changed Markdown | Read-only resolver over the four changed files | 180 links checked; one pre-existing anchor mismatch (`DOMAIN_MODEL.md#221-…`, present in the base commit, not introduced here, not edited) |
| Changed-file allowlist | `git diff --name-status 29ee137c…` | Four files only: `CHANGELOG.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`, `docs/STAGE_0.4C-0_CHECKPOINT.md` |
| Product, test, dependency and lock no-change | `git diff --name-only 29ee137c… -- packages apps adapters tests tools config spikes` and lock or manifest patterns | Empty |
| Predecessor, Stage 0.4B-3 (not re-run) | Mapping package 268 tests (261 pass, 7 Owner-local skips without the workbook; 268 / 268 with it); Mapping UI 82 / 82; boundary S1–S9 clean | Predecessor evidence only. Not a result of this Stage. |
| .NET | Not run | No .NET file changed; no .NET result claimed. |
| Browser | Not run | No browser observation claimed. |

## 18. Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — verified state
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates
- [`STAGE_0.4B-3_CHECKPOINT.md`](STAGE_0.4B-3_CHECKPOINT.md) — predecessor evidence
- [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) — logical Sensor matrix and gap anchors
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — hardware safety boundary
- [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) — publication boundary
