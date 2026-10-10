# Stage 0.4C-0A — Physical Temperature Acquisition Evidence Intake and Commissioning Source Register (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT — documentation and evidence-intake structure only, SUBMITTED FOR OWNER REVIEW.** Nothing is implemented, tested on hardware, commissioned or merged. **Physical TC acquisition: BLOCKED BY MISSING TC TOPOLOGY AND SENSOR-TO-CHANNEL SCHEDULE.** `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. `WIRE_BREAK_DECODING: BLOCKED`. **NEW PR OPEN — NOT MERGED.**

Stop point: this checkpoint. The Owner merges; the agent does not.

**Honesty lines:** NEW SESSION USED. APPROVED REMOTE-MAIN BASE VERIFIED. TC ACQUISITION TOPOLOGY NOT PROVIDED. SENSOR-TO-TC-CHANNEL SCHEDULE NOT PROVIDED. STAGE 0.4C-1 TC ACQUISITION BLOCKED. WIRE-BREAK DECODING BLOCKED. PHYSICAL SENSOR ACQUISITION NOT IMPLEMENTED. SENSOR COMMISSIONING MONITOR NOT IMPLEMENTED. ACTUAL PROCESS-IMAGE OFFSETS NOT VERIFIED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. No physical Sensor has been tested. No TC module, TC Channel order, Channel A/B placement or Sensor-to-Channel binding has been inferred. NEW PR OPEN - NOT MERGED.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0A |
| Session branch (platform-assigned, fixed) | `arena/1cfd7e20-waterjet-sentinel-suite` |
| Approved remote-main base | `main` = `bd1437586af73a33d43dfafce27b27538183a283` — the **PR #16 merge commit** |
| PR #16 merge parents | `29ee137c1df617a1aa3321c3c891161d5a5bb41a` and `8ba8d89492bffe6849b77ab135c6e434a48b31f5` |
| PR #16 state | `MERGED` at `2026-10-10T19:37:37Z` |
| Predecessor branch (closed, not pushed to) | `arena/fecda529-waterjet-sentinel-suite` |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — re-verified, unchanged |
| Development checkpoint commit | Its SHA is recorded in the PR and the final report, not inside its own commit (AGENTS §4.6) |

---

## 0. New-session base gate — PASSED

Every check below was executed at the start of this session; the results are the observed tool
output, not a restatement of the predecessor record.

| # | Check | Command / method | Result |
|---|---|---|---|
| 1 | Remote main equals the approved base | `git ls-remote origin refs/heads/main` | `bd1437586af73a33d43dfafce27b27538183a283` — **exact match** |
| 2 | Merge commit parents | `git cat-file -p bd14375…` | `parent 29ee137c1df617a1aa3321c3c891161d5a5bb41a`, `parent 8ba8d89492bffe6849b77ab135c6e434a48b31f5` — **exact match, in order** |
| 3 | Merge commit subject | same | `Merge pull request #16 from tnoiiee/arena/fecda529-waterjet-sentinel-suite` |
| 4 | PR #16 merged | `gh pr view 16` | `state: MERGED`, `mergedAt: 2026-10-10T19:37:37Z`, `mergeCommit.oid: bd1437586af73a33d43dfafce27b27538183a283`, `baseRefName: main` |
| 5 | Session branch starts at the base | `git rev-parse HEAD` on `arena/1cfd7e20-waterjet-sentinel-suite` | `bd1437586af73a33d43dfafce27b27538183a283` — **exact match** |
| 6 | Index clean | `git status --porcelain=v1` | empty output |
| 7 | Worktree clean | `git status` | `nothing to commit, working tree clean` |
| 8 | Authoritative workbook present | `find . -name T8_IO_Card_Mapping.xlsx` | `./T8_IO_Card_Mapping.xlsx` |
| 9 | Workbook SHA-256 exact | `sha256sum` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — **exact match** |
| 10 | No conflicting open PR | `gh pr list --state open` | `[]` — none open |

**The standing sandbox recreation protocol was not triggered.** Local `HEAD`, the branch ref,
the index and the worktree already agreed with the approved remote-main base, so no
`update-ref`, `read-tree` or targeted fetch was required, and no destructive Git operation was
used. No recovery approval was needed.

---

## 1. Protected decisions carried forward

The Stage 0.4C-0 Owner decisions are authoritative and were **not** reopened, softened or
reinterpreted in this Stage.

| Decision | Carried forward as |
|---|---|
| **OQ-01** TC topology gap | `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED`, `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED`, `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED`. The 212 TC Channels are **not** mapped to `AI-001`–`AI-035`; no TC module, Channel order or A/B placement is inferred; the workbook is not modified |
| **OQ-02** Two domains | **Domain A** `PROCESS_INSTRUMENT_COMMISSIONING` (workbook DI/DO/AI/AO) and **Domain B** `TEMPERATURE_SENSOR_COMMISSIONING` (106 Sensors / 212 TC Channels) stay separate and are never merged |
| **OQ-03** AI-003 | `PUMP_OUTLET`, Pump Outlet Pressure, 4–20 mA, 0–40 bar, the sole pre-P1 Pump-ready pressure source. The workbook text "Pressure Transmitter Main Valve Outlet" is source text only. `MAIN_VALVE_OUTLET_PRESSURE` remains absent |
| **OQ-04** Quality model | The `Quality` enum (`GOOD`, `UNCERTAIN`, `BAD`, `STALE`, `DISABLED`) is **unchanged**. `DiagnosticReason` and `CommissioningState` remain **separate proposed** vocabularies. `INVALID` is a presentation concept only. **No Product enum was modified in this Stage** |
| **OQ-05** Pilot population | No actual Sensor ID chosen; roles only; actual IDs `OWNER_INPUT_PENDING` |
| **OQ-06** Timing values | Staleness timeout, recovery stabilisation and batch size stay `CONFIGURABLE / OWNER_INPUT_PENDING`. No production default authorised |
| **OQ-07** Sensor verdict | PASS / PARTIAL / FAIL / NOT_TESTED / BLOCKED per the recorded rules; no averaging of A/B Channels to hide a failed Channel |
| **OQ-08** Procedure authority | Author: Commissioning / Instrument responsible role. Safety approval: Owner-authorised site safety role. Execution: authorised commissioning personnel. Final technical acceptance: Owner |

---

## 2. The eleven Stage questions, answered from evidence

Each answer states the evidence position, not a guess.

| # | Question | Answer at this head |
|---|---|---|
| 1 | What hardware acquires the 212 TC Channels? | **NOT DETERMINED.** No evidence identifies it. The authoritative workbook contains no thermocouple module (§2.1) |
| 2 | WAGO, another PLC family, a temperature multiplexer, a recorder, a remote I/O system, or another device? | **NOT DETERMINED.** No evidence distinguishes these options. No candidate is listed as favoured, because that would be an assumption |
| 3 | Where is that hardware physically installed? | **NOT DETERMINED.** `REQ-TC-003` |
| 4 | How many modules or devices exist? | **NOT DETERMINED.** `REQ-TC-002`. Not computed from 212 ÷ an assumed capacity |
| 5 | How many Channels exist per device/module? | **NOT DETERMINED.** `REQ-TC-002` |
| 6 | Which Sensor and A/B Channel maps to each physical acquisition Channel? | **NOT DETERMINED.** `REQ-TC-004`. The schedule template is issued unpopulated for every acquisition field (§3) |
| 7 | What transport or field network carries the readings? | **NOT DETERMINED.** `REQ-TC-010` |
| 8 | What signal representation is exposed? | **NOT DETERMINED.** `REQ-TC-010` |
| 9 | How are TC type, scaling, CJC and diagnostics configured? | **NOT DETERMINED.** `REQ-TC-007`, `REQ-TC-008`, `REQ-TC-009`, `REQ-TC-011` |
| 10 | How is explicit wire break represented? | **NOT DETERMINED.** `REQ-TC-011`. Wire-break decoding is **BLOCKED**. The decision tree in §6 is a design proposal, not an implementation |
| 11 | What evidence is still missing before Stage 0.4C-1? | **All 20 requests** `REQ-TC-001`–`REQ-TC-020` are `NOT_PROVIDED` — 10 P0, 5 P1, 5 P2. See [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) §9 |

### 2.1 Re-verified absence of thermocouple evidence in the authoritative workbook

The workbook was re-parsed read-only at this head (zip + XML, no installed packages, workbook
never modified). Re-derived: **114 data rows**; `Status` **108 `USED` / 6 `SPARE`**; **104 tagged**
channels (DI 37, DO 31, AI 35, AO 1); **23 slots**; **8 distinct module models**
(750-362, 750-601, 750-430, 750-530, 750-613, 750-471, 750-554, 750-600); 6 `SPARE` rows
(750-430 ch 6/7/8, 750-530 ch 8, 750-471 ch 4, 750-554 ch 2). A case-insensitive scan of every
parsed cell found **no** `thermocouple`, **no** `thermo couple`, **no** `TC-` tag and **none** of
the thermocouple input module families 750-406, 750-407, 750-469 or 750-437.

This **confirms** the Stage 0.4C-0 recorded values (114 / 108 / 6 / 104 / 23) rather than
contradicting them, and re-confirms conflict **CF-01**.

> **Parser note, recorded for honesty.** A first parse in this session reported 79 `USED`, 29
> empty `Status` cells and 6 `SPARE`. That result was **wrong** and is not used anywhere: the
> regular expression let a cell with no `<v>` element absorb the *following* cell's value, so
> `Status` values were attributed to the note column. `sharedStrings[14]` is `"USED"`, which
> identified the misattribution. The corrected parser bounds each `<c>` element before reading
> it, and reproduces the recorded baseline exactly. **The baseline was right; the first parse was
> the defect.**

---

## 3. Sensor-to-Channel schedule template

**File:** [`templates/sensor-to-tc-channel-schedule.template.csv`](templates/sensor-to-tc-channel-schedule.template.csv)

### 3.1 Structure

**28 columns**, in the required order: `SensorId`, `SensorWall`, `SensorLocation`, `MatrixRow`,
`LogicalColumn`, `MeasurementSide`, `MeasurementChannelId`, `AcquisitionSourceId`, `PanelId`,
`RackOrNodeId`, `ModuleInstanceId`, `ModuleModel`, `ConfigurationToolPosition`,
`PhysicalChannel`, `TerminalBlock`, `TerminalNumber`, `CableOrPairId`, `ThermocoupleType`,
`EngineeringUnit`, `ExpectedLow`, `ExpectedHigh`, `ColdJunctionMethod`,
`WireBreakDiagnosticBasis`, `EvidenceId`, `EvidenceScope`, `VerificationState`,
`CommissioningState`, `OwnerRemark`.

**212 data rows** — exactly one row per Thermocouple measurement Channel.

Deterministic row order, stated so the file is reproducible: ascending `MatrixRow` (1–6), then
ascending `LogicalColumn` (1–18), then `MeasurementSide` `FRONT` before `REAR`. The two
`NON_SENSOR_GAP` positions (logical `I7` at row 5 / column 7, anchoring `WJ3`, and logical `I16`
at row 5 / column 16, anchoring `WJ1`) are **excluded** — they are placement anchors, never
Sensors, and carry no Channels.

### 3.2 Pre-population policy — what is filled and what is deliberately empty

| Column group | Value written | Why it is permitted |
|---|---|---|
| `SensorId` | Canonical label, e.g. `G+201`, `I5`, `J18` | Derived from the Owner-confirmed labelling in `packages/contracts/WallMap.cs` (`CanonicalSensorMap.SensorIdFor`) |
| `SensorWall` | `LEFT` / `REAR` / `RIGHT` / `FRONT` | Derived from `CanonicalSensorMap.WallColumns` |
| `SensorLocation` | Slot identity, e.g. `SLOT-R5-C07` | `WallMapSlot.SlotId` is "derived from row/column, never invented per deployment" |
| `MatrixRow` | 1–6 | `LogicalRow`, Owner-confirmed matrix |
| `LogicalColumn` | 1–18 | `LogicalColumn`, Owner-confirmed matrix |
| `MeasurementSide` | `FRONT` / `REAR` | Each `SENSOR` slot carries a front and a rear Channel (`SensorPresentationState.TcFrontChannel` / `TcRearChannel`); two Channels per Sensor is a protected topology fact |
| `EvidenceId` | `REQ-TC-004` | The request that will supply this row's mapping evidence |
| `EvidenceScope`, `VerificationState` | `NOT_PROVIDED` | No mapping evidence exists |
| `CommissioningState` | `NOT_TESTED` | Nothing has been tested |
| `MeasurementChannelId`, `AcquisitionSourceId`, `PanelId`, `RackOrNodeId`, `ModuleInstanceId`, `ModuleModel`, `ConfigurationToolPosition`, `PhysicalChannel`, `TerminalBlock`, `TerminalNumber`, `CableOrPairId`, `ThermocoupleType`, `EngineeringUnit`, `ExpectedLow`, `ExpectedHigh`, `ColdJunctionMethod`, `WireBreakDiagnosticBasis`, `OwnerRemark` | `NOT_PROVIDED` (or empty for `OwnerRemark`) | **No evidence.** Filling any of these would invent a mapping |

### 3.3 Values deliberately **not** used

- **No synthetic Channel identity.** The example file
  [`config/examples/sensor-map.example.json`](../config/examples/sensor-map.example.json) carries
  `tcChannels` values such as `SYN-TC-01:CH00`. That file is labelled
  `PUBLIC-SAFE EXAMPLE - SYNTHETIC - INCOMPLETE BY DESIGN - NOT FOR DEPLOYMENT`. Copying those
  into a commissioning template would present synthetic fixture identities as a real mapping, so
  `MeasurementChannelId` stays `NOT_PROVIDED`.
- **No `AI-0nn` value.** The 35 workbook analog channels are Domain A and are not the 212 TC
  Channels (OQ-01, OQ-02).
- **No inferred Channel order.** Channel A/B placement and module position are not derived from
  Sensor order, wall, row, column or scan order.
- **No TC type, no engineering range, no terminal, no module instance.**

### 3.4 Verification of the template against the repository

The template's topology columns were derived from the rules in
[`packages/contracts/WallMap.cs`](../packages/contracts/WallMap.cs) and then cross-checked
against all 108 slots of
[`config/examples/sensor-map.example.json`](../config/examples/sensor-map.example.json) on
`sensorId`, `positionKind`, `wall`, `logicalColumn`, `logicalRow`, `wallRow`, `wallColumn` and
`gapAnchorForWaterJetId`:

| Check | Expected | Observed |
|---|---|---|
| Slots compared | 108 | **108** |
| Field mismatches | 0 | **0** |
| Sensor rows | 106 | **106** |
| `NON_SENSOR_GAP` rows emitted | 0 | **0** (`grep -c '^I7,\|^I16,'` returned 0) |
| Distinct `SensorId` values | 106 | **106** |
| `FRONT` rows / `REAR` rows | 106 / 106 | **106 / 106** |
| Total data rows | 212 | **212** (= `CanonicalSensorMap.ThermocoupleChannelCount`) |
| Sensors per wall | LEFT 24, REAR 29, RIGHT 24, FRONT 29 | **LEFT 24, REAR 29, RIGHT 24, FRONT 29** (= `CanonicalSensorMap.SensorsPerWall`) |
| TC Channels per wall | 48 + 58 + 48 + 58 = 212 | **212** |

### 3.5 No Product or generated-data change

The template lives under `docs/templates/` only. It is not in `config/`, `packages/`,
`apps/`, `adapters/`, `tests/` or `tools/`, and no Product source, fixture or generated data was
created or modified to produce it. A repository search of the Product tree found no reference to
`docs/templates`, so nothing reads this file. Because no pre-population produced a Product or
generated-data change, the STOP condition in the Stage brief was **not** reached and the
deliverable remains documentation-only.

---

## 4. Commissioning-record template

**File:** [`templates/tc-commissioning-record.template.csv`](templates/tc-commissioning-record.template.csv)

**26 columns**, in the required order: `CommissioningSessionId`, `SensorId`,
`MeasurementChannelId`, `MappingRevision`, `EvidenceRevision`, `Tester`, `StartedAt`,
`CompletedAt`, `BaselineRawValue`, `BaselineEngineeringValue`, `BaselineQuality`,
`StimulusDescriptionReference`, `StimulusRawValue`, `StimulusEngineeringValue`,
`OpenCircuitProcedureReference`, `OpenCircuitRawValue`, `OpenCircuitDiagnostic`,
`OpenCircuitQuality`, `RecoveryRawValue`, `RecoveryEngineeringValue`, `RecoveryQuality`,
`StabilizationObserved`, `Verdict`, `Remark`, `ApprovalRole`, `ApprovalTimestamp`.

**Zero data rows.** A header-only file is deliberate: every value in an example row would have
to be invented, and no physical Sensor has been tested. The file is a schema proposal.

### 4.1 Field semantics (proposal)

| Field | Proposed meaning | Constraints |
|---|---|---|
| `CommissioningSessionId` | Identity of one loop-test session | Unique per session; never reused across Channels |
| `SensorId`, `MeasurementChannelId` | The one Channel under test | One Channel per record. A/B Channels are never merged into one record |
| `MappingRevision`, `EvidenceRevision` | The schedule and evidence revisions in force | Records **when** the mapping was believed true, so a later schedule change cannot silently rewrite history |
| `Tester`, `StartedAt`, `CompletedAt` | Execution provenance | Per OQ-08, execution by authorised commissioning personnel |
| `Baseline*` | Connected-Sensor observation (`REQ-TC-012`) | Raw **and** engineering, plus `Quality` |
| `Stimulus*` | Applied-stimulus observation | A reference to the approved stimulus description, not an invented value |
| `OpenCircuit*` | Open-circuit observation (`REQ-TC-013`) | **Requires the approved procedure and LOTO reference** (`OpenCircuitProcedureReference`) before any such test. `OpenCircuitDiagnostic` is where the actual wire-break representation will be recorded once known |
| `Recovery*` | Post-reconnect observation (`REQ-TC-014`) | Includes the stabilisation basis |
| `StabilizationObserved` | Whether recovery stabilised, on the Owner-supplied basis | Per OQ-06 the stabilisation value stays `CONFIGURABLE / OWNER_INPUT_PENDING` |
| `Verdict` | `PASS` / `FAIL` / `PARTIAL` / `NOT_TESTED` / `BLOCKED` | Per OQ-07 |
| `Remark` | Free text | Must not carry production addresses or setpoints |
| `ApprovalRole`, `ApprovalTimestamp` | Acceptance | Per OQ-08, final technical acceptance is the Owner's |

### 4.2 Boundary

This is a schema proposal only. **No persistence, no export code, no importer, no validator and
no UI was implemented.** No Product type was added. The CSV is documentation.

---

## 5. Evidence-intake structure

**File:** [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md)

The register records the six required categories — **A** physical hardware identity, **B**
module/device topology, **C** Sensor-to-Channel mapping, **D** thermocouple configuration, **E**
transport and process representation, **F** evidence provenance — using the nine evidence states
(`NOT_PROVIDED`, `PROVIDED_UNVERIFIED`, `VERIFIED_OWNER_SOURCE`,
`VERIFIED_PRIMARY_MANUFACTURER_SOURCE`, `VERIFIED_ENGINEERING_EXPORT`,
`VERIFIED_ACTUAL_RACK_SCREENSHOT`, `VERIFIED_FIELD_LOOP_TEST`, `CONFLICTING_EVIDENCE`,
`NOT_APPLICABLE`) and the seven verification scopes (`MODEL_LEVEL`, `DEVICE_INSTANCE`,
`MODULE_INSTANCE`, `CHANNEL`, `COMPLETE_RACK`, `COMPLETE_SENSOR_SCHEDULE`, `PARTIAL_SCREENSHOT`).

**Every acquisition field in categories A–E is `NOT_PROVIDED`.** The provenance ledger
(TCEV-01 … TCEV-11) records one verified item — the authoritative workbook, whose hash was
re-verified — and ten `NOT_PROVIDED` requests.

**No hash was invented.** For every item the Owner holds outside the repository the register
reads "Not present in sandbox — no hash recorded, none invented", as required.

The non-propagation rule is recorded as binding: a fact verified for one Channel never
propagates to another Channel, module or model, and a `PARTIAL_SCREENSHOT` never establishes
`COMPLETE_RACK`.

---

## 6. Wire-break decision tree (design proposal — not implemented)

The future classification order. **Not implemented in Product source during Stage 0.4C-0A.** The
`Quality` enum is unchanged; `DiagnosticReason` remains a separate proposed vocabulary.

```
Input: one Thermocouple Channel reading, its transport state, its data age,
       and its explicit Channel diagnostic (if any).

[1] Is an EXPLICIT Channel diagnostic present and reporting wire break?
      YES -> Quality = BAD
             DiagnosticReason = WIRE_BREAK
             IsCurrentValueValid = false
             EngineeringValue = null
             LastValidValue = historical reference ONLY (never re-published as current)
             STOP. This is the only path that may assert a confirmed wire break.
      NO  -> continue

[2] Is there a DOCUMENTED substitute / special raw code for open circuit
    on THIS EXACT module and configuration?
      YES -> Interpret ONLY after exact module/configuration verification
             (REQ-TC-011 + REQ-TC-013 on that instance).
             Manufacturer-defined interpretation for that model only.
             DO NOT infer across models, instances or Channels.
             Unverified -> treat as [3], never as WIRE_BREAK.
      NO  -> continue

[3] Is the current value BELOW the expected range for this signal
    (for a 4-20 mA loop, below the live zero) with NO explicit diagnostic?
      YES -> Quality = UNCERTAIN
             DiagnosticReason = SUSPECTED_WIRE_BREAK
             IsCurrentValueValid = false
             EngineeringValue = null
             NEVER label WIRE_BREAK on this path.
      NO  -> continue

[4] Is the transport unavailable (no communication with the acquisition source)?
      YES -> Quality = BAD
             DiagnosticReason = COMMUNICATION_LOSS
             NOT WIRE_BREAK. A transport failure is not a field-circuit failure.
      NO  -> continue

[5] Has the data age exceeded the configured stale threshold?
      YES -> Quality = STALE
             NOT WIRE_BREAK, and NOT COMMUNICATION_LOSS unless the transport
             state independently indicates loss.
      NO  -> continue

[6] Is the measurement outside its configured range?
      YES -> Quality = BAD
             DiagnosticReason = OUT_OF_RANGE
             Keep this DISTINCT from wire break; do not collapse the two.
      NO  -> continue

[7] None of the above.
      Quality = GOOD
      DiagnosticReason = NONE
      IsCurrentValueValid = true
```

### 6.1 Rules that constrain the tree

1. **`WIRE_BREAK` is only ever explicit.** No inferred condition may assert it. An inferred open
   circuit is `SUSPECTED_WIRE_BREAK` at `UNCERTAIN`.
2. **Order matters.** An explicit diagnostic outranks every inferred path. Stale is evaluated
   after communication loss so a dead transport is not reported as merely old data.
3. **`OUT_OF_RANGE` is never collapsed into wire break**, even when the value is low: a genuine
   low reading and an open circuit must remain distinguishable.
4. **`INVALID` is presentation only.** It is `IsCurrentValueValid = false` plus `Quality` `BAD`
   or `STALE` plus a specific `DiagnosticReason` plus `EngineeringValue = null`. It is **not** an
   enum value, and no enum was changed.
5. **`LastValidValue` is historical.** It is never re-presented as a current value.
6. **Every threshold is `CONFIGURABLE / OWNER_INPUT_PENDING`** (OQ-06). No stale threshold, no
   recovery stabilisation time and no low-limit value is invented here or anywhere in this Stage.
7. **The tree is untestable today.** It cannot be validated because `REQ-TC-011` to `REQ-TC-015`
   are `NOT_PROVIDED`. It is a design position, not verified behaviour.

`WIRE_BREAK_DECODING: BLOCKED` until the P1 evidence exists.

---

## 7. Process Instrument parallel readiness register (Domain A)

The missing TC topology does **not** block the Process Instruments the workbook already
represents. This register records their separate readiness. **No acquisition, polling or output
control is authorised by anything in this section.**

Identity basis vocabulary: `WORKBOOK_TEXT` (the workbook description is the identity),
`OWNER_RULING` (an Owner ruling establishes the identity), `UNRESOLVED` (the workbook text does
not establish an identity).

| Group | Channels | Workbook identity (verified at this head) | Identity basis | Mapping status | Engineering range status | Diagnostic capability | Commissioning evidence needed | `TEST_HARDWARE` dependency |
|---|---|---|---|---|---|---|---|---|
| Water Level Sensor | `AI-001` | "Water Level Sensor", `AI (4-20 mA. HART5)`, slot 13 / 750-471 ch 1 | `WORKBOOK_TEXT` | Tag exists; address `ADDRESS_UNRESOLVED` | **NOT PROVIDED** | Model-level only; instance configuration not verified (only slot 13 ch 3 is Channel-level evidence) | Terminal schedule, wiring, range, instance configuration | Required |
| Pump Inlet Pressure | `AI-002` | Workbook text is the generic "Pressure Transmitter", slot 13 / ch 2. **"Pump Inlet" is not workbook text** | `OWNER_RULING` (2026-10-10): Pump Inlet Pressure, 4–20 mA, 0–40 bar | Tag exists; address `ADDRESS_UNRESOLVED` | `0–40 bar` per Owner ruling; **not bench-verified** | Model-level only | Terminal schedule, wiring, instance configuration, baseline capture | Required |
| Pump Outlet Pressure | `AI-003` | Workbook text "Pressure Transmitter Main Valve Outlet", slot 13 / ch 3, `Ref. 1.29` | `OWNER_RULING` (OQ-03): `PUMP_OUTLET`, Pump Outlet Pressure, 4–20 mA, 0–40 bar, sole pre-P1 Pump-ready source. Workbook text is source text only | Tag exists; address `ADDRESS_UNRESOLVED`. The one Channel-level settings record sits at this location — a **location coincidence, not tag verification** (RISK-01) | `0–40 bar` per Owner ruling; **not bench-verified** | `PROVIDED_UNVERIFIED` at `CHANNEL` scope only: signal type 4-20 mA, input filter Off, channel diagnosis On, wire-break diagnosis On, overload / range overflow / underflow / user-limit diagnoses On, user limits 32767 / −32768. Open-circuit representation, status byte layout and scaling result **NOT VERIFIED** | Terminal schedule and wiring drawing before this Channel is treated as wired to its tag | Required |
| IV1–IV8 pressure | `AI-004` … `AI-011` | "WSB Pressure transmitter #1" … "#8", `AI (4-20 mA. HART5)`, slots 13–15 | `OWNER_RULING` label rule: `WSB Pressure transmitter #n = IVn` (recorded as CF-05) | Tags exist; addresses `ADDRESS_UNRESOLVED` | **NOT PROVIDED** | Model-level only | Terminal schedule, wiring, per-Channel instance configuration, ranges | Required |
| Sealed Air Blower current | `AI-012` … `AI-019` | "WSB Sealed Air Blower Current #1" … "#8", **`AI (0-20 mA.)`** — note the different signal type, slots 15–17 | `WORKBOOK_TEXT` | Tags exist; addresses `ADDRESS_UNRESOLVED` | **NOT PROVIDED.** Signal type is `0-20 mA`, not `4-20 mA`, so a 4 mA live-zero assumption would be wrong | Model-level only | Terminal schedule, wiring, instance configuration, ranges. The 0–20 mA basis must be confirmed before any low-value diagnostic logic | Required |
| Lower limit switches | `DI-021` … `DI-028` | "WSB Lower Limit Switch #1" … "#8 (NO)", `DI (24 VDC.)`, slots 5–6 | `WORKBOOK_TEXT` | Tags exist; addresses `ADDRESS_UNRESOLVED`. Polarity `ACTIVE_WHEN_CLOSED` / `NO` per Stage 0.4B-1 defaults | Not applicable (digital) | Digital input; no analog diagnostic. Channel-level diagnosis not verified | Terminal schedule, wiring, actuated-state verification per switch | Required |
| Upper limit switches | `DI-029` … `DI-036` | "WSB Upper Limit Switch #1" … "#8 (NO)", `DI (24 VDC.)`, slots 6–7 | `WORKBOOK_TEXT` | As above | Not applicable (digital) | As above | As above | Required |
| Pump / Inverter status | `DI-001` … `DI-005` | "Pump #1 is selected", "Pump #2 is selected", "Inverter Is Ready", "Inverter Is Running", "Inverter Is Fault", slot 3 | `WORKBOOK_TEXT` | Tags exist; addresses `ADDRESS_UNRESOLVED` | Not applicable | Digital input | Terminal schedule, wiring, state verification | Required |
| Water level switch | `DI-006` | "Water Level Switch (NO)", slot 3 | `WORKBOOK_TEXT` | Tag exists; address `ADDRESS_UNRESOLVED` | Not applicable | Digital input | Terminal schedule, wiring | Required |
| Over-torque and valve position | `DI-007` … `DI-020` | "Over Torque Lamp" and "Valve is Opened / Closed" pairs, slots 3–5, mixed `DI (24 VDC.)` and `220VAC. to DI (24 VDC.)` | `WORKBOOK_TEXT` | Tags exist; addresses `ADDRESS_UNRESOLVED` | Not applicable | Digital input | Terminal schedule, wiring, state verification | Required |
| Pump speed output | `AO-001` | "Inverter Pump", `AO (4-20 mA.)`, slot 22 / 750-554 ch 1 | `WORKBOOK_TEXT` | Tag exists; address `ADDRESS_UNRESOLVED` | **NOT PROVIDED** | Output module settings **NOT PROVIDED** (Stage 0.4C-0 `EV-11`) | Terminal schedule, wiring, 750-554 configuration | Required — and **write control is `[NOT AUTHORIZED]`** |
| **Unresolved analog identities** | **`AI-020` … `AI-035`** | **All 16 descriptions read "XXX" plus a Thai note meaning "waiting for a reply"; `AI (4-20 mA.)`, slots 17–21** | **`UNRESOLVED`** | Tags exist but carry **no established identity**. Addresses `ADDRESS_UNRESOLVED` | **NOT PROVIDED** | Unknown | **The Owner must first establish what each channel is.** These are **NOT commissioned instruments** and must never be presented as such | Required, after identity |
| Spare channels | 6 `SPARE` rows | "SPARE CHANNEL": 750-430 ch 6/7/8, 750-530 ch 8, 750-471 ch 4, 750-554 ch 2 | `WORKBOOK_TEXT` | Not tagged | Not applicable | Not applicable | None | Not applicable |

### 7.1 Domain A conclusions

1. **18 tagged rows remain unresolved.** The 16 `AI-020`–`AI-035` rows plus `DI-037` and `DO-031`
   carry no established identity. They are recorded as reserved, not as commissioned instruments.
2. **No Domain A channel is commissioned.** Every address is `ADDRESS_UNRESOLVED`, the actual
   Process Data image and head-station field-network mapping remain `NOT PROVIDED`, and the
   candidate process image stays `CANDIDATE_UNVERIFIED` with every offset
   `OFFSET_UNRESOLVED`.
3. **Exactly one Channel-level configuration observation exists** (slot 13 / Pos. 10 / ch 3), and
   it does not verify the tag, the wiring or the scaling. RISK-01 stands.
4. **Domain A commissioning can proceed independently of Domain B** — but only after its own
   terminal schedule, wiring drawings and instance configuration evidence, and only under a
   separate Owner Scope Gate. **This Stage authorises none of it.**
5. **`AI-012`–`AI-019` are `0-20 mA`, not `4-20 mA`.** Any future low-value diagnostic must not
   assume a 4 mA live zero for these Channels.

---

## 8. Galil parallel evidence intake register

Recorded in [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) §10 as `GAL-01` …
`GAL-13`, classified `PROVIDED` / `NOT_PROVIDED` / `VERIFIED` / `OWNER_INPUT_PENDING` /
`BLOCKING_GATE`. All 13 items are `NOT_PROVIDED`, `OWNER_INPUT_PENDING` or `BLOCKING_GATE`; none
is `PROVIDED` or `VERIFIED`.

`GAL-10` (safe speed and acceleration limits) and `GAL-11` (permitted travel envelope) are marked
`BLOCKING_GATE` because they are production motion values whose invention AGENTS.md §2.5 forbids.

**No Galil control is implemented, proposed or authorised.** Galil stays separate from the TC
acquisition register and outside this Stage.

---

## 9. Stage 0.4C-1 entry criteria

Stage 0.4C-1 may be proposed as `READY` **only** when every item below is satisfied.

| # | Entry criterion | Evidence that satisfies it | Status at this head |
|---|---|---|---|
| 1 | Actual TC acquisition hardware identified | `REQ-TC-001` at `DEVICE_INSTANCE` or better | **NOT MET** |
| 2 | Complete rack / device topology known | `REQ-TC-002` at `COMPLETE_RACK` | **NOT MET** |
| 3 | Complete 212-Channel schedule received | `REQ-TC-004`, `REQ-TC-005`, `REQ-TC-006` at `COMPLETE_SENSOR_SCHEDULE` | **NOT MET** |
| 4 | TC type and CJC configuration known | `REQ-TC-007`, `REQ-TC-008` | **NOT MET** |
| 5 | Transport and process representation known | `REQ-TC-009`, `REQ-TC-010` | **NOT MET** |
| 6 | No unresolved contradictory mapping evidence | No `CONFLICTING_EVIDENCE` row open in the provenance ledger | **MET** (no mapping evidence exists at all, so none conflicts — this criterion is vacuous until evidence arrives and must be re-checked then) |
| 7 | Owner approves the exact read-only acquisition scope | Explicit Owner Scope Gate | **NOT MET** |
| 8 | Owner approves the `TEST_HARDWARE` Scope Gate | Explicit Owner Scope Gate | **NOT MET** |
| 9 | Disconnect / reconnect behaviour specified | `REQ-TC-014` | **NOT MET** |
| 10 | No-write proof designed | A design that proves no output, coil, register or device write path exists | **NOT MET** |
| 11 | Rollback and stop conditions defined | Documented rollback and stop conditions approved by the Owner | **NOT MET** |

**Verdict: 10 of 11 criteria NOT MET.** Criterion 6 is satisfied only vacuously and cannot be
relied on.

```
STAGE_0.4C-1_TC_ACQUISITION: BLOCKED      (all P0 evidence NOT_PROVIDED)
WIRE_BREAK_DECODING:         BLOCKED      (all P1 evidence NOT_PROVIDED)
ROLLOUT:                     BLOCKED      (all P2 evidence NOT_PROVIDED)
```

Wire-break implementation additionally requires **all** of `REQ-TC-011` to `REQ-TC-015`, and
`REQ-TC-013` (observed open-circuit behaviour) cannot even be gathered until `REQ-TC-019`
(approved commissioning procedure and LOTO reference) exists.

---

## 10. Scope observation reported, not acted on (AGENTS §2.3 / §11.2)

**`README.md` now carries a stale current-status block, and it was deliberately NOT modified.**

`README.md` is not in this Stage's expected file scope. Its status block still reads *Current
stage = Stage 0.4C-0*, *PR #16 OPEN — NOT MERGED*, and *base `main` `29ee137c…`*, all three of
which are now superseded: Stage 0.4C-0 is merged, PR #16 is `MERGED`, and remote `main` is
`bd143758…`.

Under AGENTS.md §2.2 an agent must not modify a file the current Stage Gate does not name, and
under §2.3 must stop, report and explain before creating or changing one. The predecessor Stage
obtained an explicit Owner authorisation for a narrow README correction; **no equivalent
authorisation exists for this Stage.** The discrepancy is therefore reported here rather than
repaired silently, and is recorded as conflict **CF-07**.

**Requested Owner decision:** authorise a narrow `README.md` current-status correction (three
rows: current stage, identifiers, scope gate) in a follow-up commit on this branch, or defer it.

A second, smaller observation: `docs/CURRENT_STATE.md` and `docs/MASTER_PLAN.md` were named in
this Stage's file scope and **were** updated to the Stage 0.4C-0A position, which is why the
README is now the only stale current-status surface.

---

## 11. Changed / Unchanged / Not Verified

### CHANGED

| File | Purpose |
|---|---|
| `docs/STAGE_0.4C-0A_CHECKPOINT.md` | **New.** This Stage's checkpoint record: base-gate proof, protected decisions, the eleven evidence answers, template policies, wire-break decision tree, Domain A parallel readiness register, Stage 0.4C-1 entry criteria, scope observation, validation |
| `docs/TC_EVIDENCE_SOURCE_REGISTER.md` | **New.** Physical TC evidence source register: categories A–F, evidence states and scopes, non-propagation rule, provenance ledger TCEV-01…TCEV-11, prioritised `REQ-TC-001`–`REQ-TC-020`, Galil register `GAL-01`–`GAL-13` |
| `docs/templates/sensor-to-tc-channel-schedule.template.csv` | **New.** 28-column, 212-row deterministic schedule template. Topology columns pre-populated from `WallMap.cs`; every acquisition column `NOT_PROVIDED` |
| `docs/templates/tc-commissioning-record.template.csv` | **New.** 26-column commissioning-record schema proposal, header only, zero data rows |
| `docs/CURRENT_STATE.md` | Stage 0.4C-0A current position added at the top of the status block and as validation record §12.35; the Stage 0.4C-0 block is labelled historical and superseded |
| `docs/MASTER_PLAN.md` | Stage 0.4C-0A ledger entry added at the top of §3 and as new §3.7; the Stage 0.4C-0 entry is marked merged |
| `CHANGELOG.md` | New Stage 0.4C-0A entry at the top |

### UNCHANGED

- **Product source** — no file under `apps/`, `packages/`, `adapters/` or `spikes/` was created,
  modified or deleted. No `Quality` enum, no `DiagnosticReason`, no `CommissioningState`, no
  contract, no domain type, no adapter.
- **Executable tests** — no file under `tests/` was created, modified or deleted. No test was
  run.
- **Authoritative workbook** — `T8_IO_Card_Mapping.xlsx` was read only. SHA-256 unchanged. No TC
  module was added and no cell was edited.
- **Fixture data and generated fixtures** — `config/examples/`, `packages/contracts/fixtures/` and
  every generated fixture are untouched.
- **Runtime, Mapping implementation, Galil implementation, MODBUS implementation** — none exists
  for TC acquisition, and none was added.
- **Dependencies and lock files** — `Directory.Packages.props`, `global.json`, every `.csproj`,
  every `package.json` and every lock file are untouched. No package was installed.
- **Production configuration** — untouched. No address, offset, threshold, timeout, setpoint or
  network identity was written anywhere.
- **CI workflows, release artifacts, build files** — untouched.
- **`README.md`** — untouched by decision; see §10.
- **The merged PR #16 branch** `arena/fecda529-waterjet-sentinel-suite` — not checked out, not
  committed to, not pushed to.

### NOT VERIFIED

| Item | Why unverified |
|---|---|
| Which hardware acquires the 212 TC Channels | `REQ-TC-001` `NOT_PROVIDED` |
| Device/module count and Channel capacity | `REQ-TC-002` `NOT_PROVIDED` |
| Physical installation location | `REQ-TC-003` `NOT_PROVIDED` |
| Every Sensor-to-Channel A/B binding | `REQ-TC-004` `NOT_PROVIDED`; template acquisition columns empty by design |
| Terminal schedule and wiring | `REQ-TC-005`, `REQ-TC-006` `NOT_PROVIDED` |
| TC type per Channel, CJC method and source | `REQ-TC-007`, `REQ-TC-008` `NOT_PROVIDED` |
| Channel configuration, scaling, filters, diagnostics | `REQ-TC-009`, `REQ-TC-011` `NOT_PROVIDED` |
| Transport, protocol, byte/word order, encoding | `REQ-TC-010` `NOT_PROVIDED` |
| **Explicit wire-break representation** | `REQ-TC-011` `NOT_PROVIDED`. `WIRE_BREAK_DECODING: BLOCKED` |
| Observed baseline, open-circuit and recovery values | `REQ-TC-012`–`REQ-TC-014` `NOT_PROVIDED`. No physical Sensor has been tested |
| Under-range, over-range, module-fault encodings | `REQ-TC-015` `NOT_PROVIDED` |
| Expected normal ranges, update cycle, stale threshold | `REQ-TC-016`–`REQ-TC-018` `NOT_PROVIDED`; per OQ-06 `CONFIGURABLE / OWNER_INPUT_PENDING` |
| Commissioning procedure, LOTO reference, approval holders | `REQ-TC-019`, `REQ-TC-020` `NOT_PROVIDED` |
| **Actual process-image offsets (Domain A)** | Still `OFFSET_UNRESOLVED`; the actual Process Data image and head-station field-network mapping remain `NOT PROVIDED` |
| Domain A addresses | Every address remains `ADDRESS_UNRESOLVED`; the candidate process image remains `CANDIDATE_UNVERIFIED` |
| `AI-020`–`AI-035` identities | Workbook descriptions carry no established identity; **not commissioned instruments** |
| Domain A instance-level channel configuration | Only slot 13 / Pos. 10 / ch 3 is Channel-level evidence, at `PROVIDED_UNVERIFIED` / `CHANNEL` scope; not generalised |
| The wire-break decision tree | Untestable while `REQ-TC-011`–`REQ-TC-015` are `NOT_PROVIDED`. Design position only |
| All Galil items `GAL-01`–`GAL-13` | No Galil evidence in the repository or this sandbox |
| **Predecessor test evidence** | Stage 0.4B-3 Mapping 268/268, Mapping UI 82/82 and Owner Browser Review PASSED are **predecessor evidence only, cited not re-run.** They were **not** executed at this Stage's head and are not claimed as results of this Stage |

---

## 12. Validation performed

Documentation-only validation, per AGENTS.md §10.1. No executable test was run, and none is
claimed.

| Check | Command / method | Result |
|---|---|---|
| Boundary scan S1–S9 | `node tools/boundary-scan/boundary-scan.mjs .` | **`boundary-scan: 0 findings (S1-S9 clean)`, exit 0** — identical to the pre-change baseline captured at the start of this session |
| Whitespace / conflict markers | `git diff --check` against the PR range | Clean — see the final report for the exact command and output |
| Changed-file allowlist | `git diff --name-status` against the base | Only the 7 files listed under CHANGED |
| Product-source no-change proof | `git diff --name-only` filtered to `apps/`, `packages/`, `adapters/`, `spikes/` | **Empty** |
| Executable-test no-change proof | `git diff --name-only` filtered to `tests/` | **Empty** |
| Workbook SHA-256 proof | `sha256sum T8_IO_Card_Mapping.xlsx` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — unchanged |
| Relative-link check | Every relative link in the new and changed documents resolved against the filesystem | See the final report for the per-link result |
| Template topology cross-check | 108 slots of `config/examples/sensor-map.example.json` vs `WallMap.cs` rules | **0 mismatches**; 106 Sensors, 212 Channels, wall counts 24/29/24/29 |
| Workbook re-parse | Read-only zip/XML parse | 114 rows, 108 `USED`, 6 `SPARE`, 104 tagged, 23 slots, no TC module — matches the recorded baseline |
| Clean worktree | `git status --porcelain` | Clean after commit |

**Explicitly not run:** no `dotnet build`, no `dotnet test`, no `npm test`, no Mapping package
test, no Mapping UI test. This Stage touched no code those runners execute, so a test run would
have executed no changed path. Recording predecessor evidence instead, per AGENTS.md §10.1 and
§10.5.

---

## 13. Related documents

- [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) — the evidence register and `REQ-TC` register
- [templates/sensor-to-tc-channel-schedule.template.csv](templates/sensor-to-tc-channel-schedule.template.csv) — schedule template
- [templates/tc-commissioning-record.template.csv](templates/tc-commissioning-record.template.csv) — commissioning-record template
- [STAGE_0.4C-0_CHECKPOINT.md](STAGE_0.4C-0_CHECKPOINT.md) — predecessor evidence gate, Owner decisions OQ-01 to OQ-08
- [CURRENT_STATE.md](CURRENT_STATE.md) — verified state and stage status
- [MASTER_PLAN.md](MASTER_PLAN.md) — stage ledger
- [DOMAIN_MODEL.md](DOMAIN_MODEL.md) — the 18 × 6 logical matrix and the two `NON_SENSOR_GAP` positions
- [PUBLIC_REPOSITORY_BOUNDARY.md](PUBLIC_REPOSITORY_BOUNDARY.md) — what may be published
- [SAFETY_BOUNDARY.md](SAFETY_BOUNDARY.md) — hardware safety boundary
- [../AGENTS.md](../AGENTS.md) — working contract, evidence-first rules, stop conditions
