# Physical Thermocouple Evidence Source Register — Stage 0.4C-0A

Status: **[PROPOSED]** evidence-intake structure. This register records **what evidence exists,
what it proves, at what scope, and what is still missing** for the Temperature Sensor
acquisition system. It authorises nothing. It is not a configuration, not a mapping and not a
commissioning record.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0A |
| Domain | **Domain B — `TEMPERATURE_SENSOR_COMMISSIONING`** (106 physical Sensor locations, 212 Thermocouple measurement Channels) |
| Created at | Approved remote-main base `bd1437586af73a33d43dfafce27b27538183a283` (PR #16 merge commit) |
| Governing decisions | Stage 0.4C-0 Owner decisions OQ-01 to OQ-04, treated as authoritative and protected |
| Current gate | `TC_ACQUISITION_TOPOLOGY: NOT_PROVIDED` · `SENSOR_TO_TC_CHANNEL_SCHEDULE: NOT_PROVIDED` · `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED` · `WIRE_BREAK_DECODING: BLOCKED` |
| Companion record | [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) |

**Binding rule.** No entry in this register has been populated with an invented value. Where an
item is unknown it reads `NOT_PROVIDED`. No SHA-256 has been recorded for any evidence that is
not physically present in this sandbox, and none has been invented for an item the Owner holds
outside the repository.

---

## 1. Scope, and what this register is not

This register answers one question: **what hardware acquires the 212 Thermocouple Channels, and
what evidence proves it?** It is an evidence ledger.

It is **not**:

- a Sensor-to-Channel mapping — the schedule template
  ([`templates/sensor-to-tc-channel-schedule.template.csv`](templates/sensor-to-tc-channel-schedule.template.csv))
  is deliberately unpopulated for every acquisition field;
- a device profile, a transport configuration, or a read implementation;
- an approval of acquisition, polling, output control, or `TEST_HARDWARE`;
- evidence that any physical Sensor has been tested. None has been.

Domain B is kept separate from Domain A (`PROCESS_INSTRUMENT_COMMISSIONING`, the identities the
authoritative workbook already represents). The two are never merged. See
[STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) §7 for the Domain A parallel
readiness register.

### 1.1 Why the workbook cannot answer these questions (re-verified at this head)

The authoritative workbook `T8_IO_Card_Mapping.xlsx` (SHA-256
`4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, unchanged) was re-parsed
read-only at this head. Re-derived facts:

| Fact | Re-derived value |
|---|---|
| Data rows | **114** |
| `Status` column | **108 `USED`, 6 `SPARE`** |
| Tagged channels | **104** (DI 37, DO 31, AI 35, AO 1) |
| Slots (RackSlot) | **23** |
| Distinct module models | 8: 750-362, 750-601, 750-430, 750-530, 750-613, 750-471, 750-554, 750-600 |
| Channel rows per model | 750-430 ×40, 750-530 ×32, 750-471 ×36, 750-554 ×2, plus 4 single-row non-process modules |
| `SPARE` rows | 6: 750-430 ch 6/7/8, 750-530 ch 8, 750-471 ch 4, 750-554 ch 2 |
| Thermocouple evidence | **NONE.** A case-insensitive scan of every parsed cell found no `thermocouple`, no `thermo couple`, no `TC-` tag and none of the thermocouple input module families 750-406, 750-407, 750-469 or 750-437 |

**Conclusion.** The workbook is authoritative for Domain A rack topology and channel tags. It
contains **no** thermocouple module, **no** Sensor identity, **no** TC Channel and **no** wiring.
It therefore cannot identify the Domain B acquisition system, and no part of it may be read as
doing so. This re-confirms conflict CF-01 from Stage 0.4C-0.

---

## 2. Evidence state and scope vocabulary

### 2.1 Evidence states

| State | Meaning |
|---|---|
| `NOT_PROVIDED` | No evidence of any kind has been supplied. |
| `PROVIDED_UNVERIFIED` | Supplied by the Owner, but its content cannot be checked in this sandbox. |
| `VERIFIED_OWNER_SOURCE` | Confirmed against an Owner-authored source available for inspection. |
| `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | Confirmed against the manufacturer's own document for that exact model. |
| `VERIFIED_ENGINEERING_EXPORT` | Confirmed against a configuration-tool export for the actual instance. |
| `VERIFIED_ACTUAL_RACK_SCREENSHOT` | Confirmed against a screenshot of the actual installed rack or configuration tool. |
| `VERIFIED_FIELD_LOOP_TEST` | Confirmed by an observed field loop test on the physical Channel. |
| `CONFLICTING_EVIDENCE` | Two sources disagree; neither is discarded, both are recorded. |
| `NOT_APPLICABLE` | The item does not apply to this acquisition system. |

### 2.2 Verification scopes

| Scope | Meaning |
|---|---|
| `MODEL_LEVEL` | True of the model. Says nothing about any installed instance. |
| `DEVICE_INSTANCE` | True of one named controller, head station or device. |
| `MODULE_INSTANCE` | True of one named module in a named position. |
| `CHANNEL` | True of exactly one Channel. |
| `COMPLETE_RACK` | True of the whole rack or node, verified as a whole. |
| `COMPLETE_SENSOR_SCHEDULE` | True of the full 106 Sensor / 212 Channel schedule. |
| `PARTIAL_SCREENSHOT` | A screenshot covering only part of the rack or configuration. |

### 2.3 Non-propagation rule (binding)

A fact verified at one scope never propagates automatically to a wider or different scope.
Specifically:

1. A `MODEL_LEVEL` fact does not establish any installed instance.
2. A fact verified for one Channel does not apply to another Channel on the same module.
3. A fact verified for one module does not apply to another module of the same model.
4. A `PARTIAL_SCREENSHOT` never establishes `COMPLETE_RACK`.
5. Sensor order in the logical matrix never establishes TC Channel order, module position or
   Channel A/B placement.
6. A single verified configuration screen never establishes that all instances of that model are
   configured identically.

Every register row carries one state and one scope. Widening either requires new evidence.

---

## 3. Category A — Physical hardware identity

Every field below is `NOT_PROVIDED`. The acquisition hardware for Domain B has not been
identified, so no field can be populated without inventing it.

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Manufacturer | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |
| Model | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |
| Variant / order number | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |
| Hardware revision | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |
| Firmware / software revision | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001, REQ-TC-010 |
| Serial number (Owner-authorised only) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | Not required for Stage 0.4C-1 |
| Panel / rack | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-003 |
| Physical location | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-003 |
| Power source | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-003 |
| Network / bus connection | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Photo or nameplate evidence reference | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |

**Candidate families are not recorded as candidates.** The Stage 0.4C-0 task question set asks
whether the acquisition system is WAGO, another PLC family, a temperature multiplexer, a
recorder, a remote I/O system or another device. No evidence distinguishes these options, so
none is listed as favoured. Listing a favourite would be an assumption, not evidence.

---

## 4. Category B — Module / device topology

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Controller or head station identity | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Rack or node name | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Module position | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Module model | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Channel capacity per module | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Channel numbering basis (0-based / 1-based) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002, REQ-TC-009 |
| Process-data role (contributing / non-contributing) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Actual configuration-tool position | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009 |
| Count of acquisition devices / modules | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-002 |
| Evidence source and scope per row | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009 |

**Not derived (binding).** No module count has been computed from 212 Channels divided by any
assumed Channel capacity. The device count and per-device capacity are independent Owner facts
(REQ-TC-002); deriving one from the other would invent both.

**Domain A positions are not reused.** The 23 RackSlots and the configuration-tool positions
`Pos. 01–20` recorded in Stage 0.4B-3 and Stage 0.4C-0 describe the **Domain A** rack. They are
not available as Domain B positions, and no Domain B module has been placed in them.

---

## 5. Category C — Sensor-to-Channel mapping

Populated only in the unpopulated template
[`templates/sensor-to-tc-channel-schedule.template.csv`](templates/sensor-to-tc-channel-schedule.template.csv).
The pre-population policy is in [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) §3.

| Field | State | Blocks |
|---|---|---|
| Sensor ID, wall, logical position, matrix row/column, measurement side | `VERIFIED_OWNER_SOURCE`, scope `COMPLETE_SENSOR_SCHEDULE` (logical structure only) | — |
| Measurement Channel ID | `NOT_PROVIDED` | REQ-TC-004 |
| Terminal block | `NOT_PROVIDED` | REQ-TC-005 |
| Terminal number | `NOT_PROVIDED` | REQ-TC-005 |
| Field cable or pair identity | `NOT_PROVIDED` | REQ-TC-005, REQ-TC-006 |
| Panel | `NOT_PROVIDED` | REQ-TC-003 |
| Acquisition device | `NOT_PROVIDED` | REQ-TC-001, REQ-TC-002 |
| Module | `NOT_PROVIDED` | REQ-TC-002 |
| Channel | `NOT_PROVIDED` | REQ-TC-004 |
| Signal / TC type | `NOT_PROVIDED` | REQ-TC-007 |
| Engineering unit | `NOT_PROVIDED` | REQ-TC-007, REQ-TC-016 |
| Expected normal range | `NOT_PROVIDED` | REQ-TC-016 |
| Evidence source | `NOT_PROVIDED` | REQ-TC-004 |
| Verification state | `NOT_PROVIDED` for every row | REQ-TC-004 |

**Not inferred (binding).** The 212 TC Channels are **not** mapped to `AI-001` through `AI-035`.
No Channel A/B placement, no module assignment and no TC Channel ordering has been inferred from
Sensor order, Sensor wall, logical row/column, or scan order.

---

## 6. Category D — Thermocouple configuration

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| TC type per Channel (or per approved Channel group) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-007 |
| Grounded / ungrounded configuration | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-007 |
| Cold-junction compensation method | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-008 |
| CJC source (internal / external / shared reference) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-008 |
| Configured measurement range | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009 |
| Configured data format | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009, REQ-TC-010 |
| Channel enable / disable | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009 |
| Filter | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-009 |
| Diagnostic settings | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-011 |
| Wire-break settings | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-011 |
| Substitute-value behaviour | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-011, REQ-TC-012 |
| Update cycle | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-017 |

**Domain A channel settings are not transferred.** Stage 0.4C-0 records exactly one
Channel-level settings observation (Pos. 10 / RackSlot 13 / Channel 3), a Domain A analog input.
Its signal type, filter, diagnosis and user-limit values say nothing about any Domain B
thermocouple Channel and have not been copied into this category.

---

## 7. Category E — Transport and process representation

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Transport type | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Read protocol | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Node / unit identity | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Process-data source | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Byte order | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Word order | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Channel width | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Status / diagnostic representation | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010, REQ-TC-011 |
| Value encoding | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-010 |
| Invalid-value encoding | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-011, REQ-TC-015 |
| Polling / acquisition cycle | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-017 |
| Reconnect behaviour | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-014 |
| Stale-data threshold basis | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-018 |

**No offset exists.** No byte, word or bit offset for Domain B has been computed, guessed or
carried over from the Domain A candidate process image. The Domain A candidate image is itself
`CANDIDATE_UNVERIFIED` with every offset `OFFSET_UNRESOLVED`, and it is not a Domain B source in
any case.

**No transport vocabulary is asserted.** Naming a protocol here would be an invented production
value under AGENTS.md §2.5.

---

## 8. Category F — Evidence provenance register

This is the ledger of items actually supplied. Columns are fixed. **No row has been invented**,
and no SHA-256 appears for evidence that is not present in this sandbox.

| Evidence ID | Owner-provided title | Type | File / path (if committed and authorised) | SHA-256 | Screenshot identity | Page / screen reference | Scope | Verification state | Reviewer | Review date | Conflicts | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| TCEV-01 | Authoritative Process-I/O workbook `T8_IO_Card_Mapping.xlsx` | Workbook | `T8_IO_Card_Mapping.xlsx` (in repository) | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` (re-verified at this head) | — | Sheet1, 114 data rows | Domain A topology and tags only | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-0A | **Contains no Domain B evidence** (CF-01) | Authoritative for Domain A. Establishes nothing for Domain B |
| TCEV-02 | Domain B thermocouple acquisition topology | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-001 to REQ-TC-003, REQ-TC-010 |
| TCEV-03 | Complete Sensor-to-Channel A/B schedule | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-004 |
| TCEV-04 | Terminal schedule | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-005 |
| TCEV-05 | Wiring drawings | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-006 |
| TCEV-06 | Thermocouple type per Channel or approved group | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-007 |
| TCEV-07 | Cold-junction compensation configuration | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-008 |
| TCEV-08 | Engineering-tool export or complete configuration screenshots | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-009 |
| TCEV-09 | Transport / network identity and read representation | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-010 |
| TCEV-10 | Wire-break diagnostic configuration and observed raw/diagnostic behaviour | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-011 to REQ-TC-015 |
| TCEV-11 | Ranges, cycles, thresholds, commissioning procedure, approval roles | — | Not supplied | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_PROVIDED` | — | — | — | Blocks REQ-TC-016 to REQ-TC-020 |

### 8.1 Domain A evidence already held (recorded, not reusable for Domain B)

Stage 0.4C-0 records `EV-02` (eight manufacturer manuals, model level, outside the repository,
unhashed) and `EV-03`/`EV-04` (a partial rack screenshot and one Channel-level configuration
screen). Those items are **Domain A** evidence. They are cited here only to state that they have
**not** been used to fill any Domain B field. No hash is recorded for them because none is
available in this sandbox.

---

## 9. Prioritised Owner evidence request register

Priority: **P0** blocks Stage 0.4C-1. **P1** blocks wire-break implementation. **P2** blocks
rollout. Every request is `NOT_PROVIDED` at this head.

### 9.1 P0 — blocks Stage 0.4C-1

| ID | Request | Status | Blocking stage | Evidence received | Evidence scope | Verification result | Conflict | Next action |
|---|---|---|---|---|---|---|---|---|
| REQ-TC-001 | Thermocouple acquisition hardware manufacturer and model | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-02) | — | Cannot verify | None | Owner supplies nameplate photo or datasheet reference |
| REQ-TC-002 | Count of acquisition devices/modules and Channel capacity | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-02) | — | Cannot verify | None | Owner supplies device/rack list with per-device Channel count |
| REQ-TC-003 | Physical panel, rack, cabinet or controller location | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-02) | — | Cannot verify | None | Owner supplies panel identifier and installation location |
| REQ-TC-004 | Complete Sensor ID to Channel A/B schedule (all 106 Sensors, 212 Channels) | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-03) | — | Cannot verify | None | Owner supplies schedule; populate the template row by row with evidence per row |
| REQ-TC-005 | Terminal schedule | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-04) | — | Cannot verify | None | Owner supplies terminal block and terminal number per Channel |
| REQ-TC-006 | Wiring drawings | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-05) | — | Cannot verify | None | Owner supplies drawing identifiers and revision |
| REQ-TC-007 | TC type per Channel or per approved Channel group | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-06) | — | Cannot verify | None | Owner supplies type per Channel, or an explicitly approved grouping rule |
| REQ-TC-008 | Cold-junction compensation configuration | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-07) | — | Cannot verify | None | Owner supplies CJC method and source |
| REQ-TC-009 | Engineering-tool export or complete configuration screenshots | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-08) | — | Cannot verify | None | Owner supplies export or screenshots covering every instance; partial screenshots accepted only at `PARTIAL_SCREENSHOT` scope |
| REQ-TC-010 | Transport/network identity and read representation | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-09) | — | Cannot verify | None | Owner supplies transport, protocol, node identity, byte/word order, Channel width, encoding |

### 9.2 P1 — blocks wire-break implementation

| ID | Request | Status | Blocking stage | Evidence received | Evidence scope | Verification result | Conflict | Next action |
|---|---|---|---|---|---|---|---|---|
| REQ-TC-011 | Explicit wire-break diagnostic configuration | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10) | — | Cannot verify | None | Owner supplies per-Channel diagnostic configuration |
| REQ-TC-012 | Raw value and diagnostic/status during a normal connected Sensor | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10) | — | Cannot verify | None | Owner supplies observed baseline capture |
| REQ-TC-013 | Raw value and diagnostic/status during an Owner-authorised open circuit | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10) | — | Cannot verify | None | **Requires the approved site procedure and LOTO controls first (REQ-TC-019).** No open-circuit instruction is authorised without them |
| REQ-TC-014 | Recovery behaviour after reconnect | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10) | — | Cannot verify | None | Owner supplies observed recovery capture and stabilisation basis |
| REQ-TC-015 | Under-range, over-range and module-fault representations | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10) | — | Cannot verify | None | Owner supplies the diagnostic/substitute encoding per condition |

### 9.3 P2 — required for rollout

| ID | Request | Status | Blocking stage | Evidence received | Evidence scope | Verification result | Conflict | Next action |
|---|---|---|---|---|---|---|---|---|
| REQ-TC-016 | Expected normal operating ranges by Sensor or approved group | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner supplies ranges; no range may be inferred from another Sensor |
| REQ-TC-017 | Channel update cycle | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner supplies the configured cycle |
| REQ-TC-018 | Permitted stale threshold basis | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner supplies threshold basis. Per OQ-06 this stays `CONFIGURABLE / OWNER_INPUT_PENDING`; no default is authorised |
| REQ-TC-019 | Commissioning procedure and LOTO reference | `NOT_PROVIDED` | Rollout; also gates REQ-TC-013 | None (TCEV-11) | — | Cannot verify | None | Owner supplies the approved procedure identifier. Per OQ-08 the author is the Commissioning / Instrument responsible role and safety approval is an Owner-authorised site safety role |
| REQ-TC-020 | Responsible approval roles | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner names the roles; OQ-08 records the role structure, not the named holders |

### 9.4 Register summary at this head

| Priority | Count | `NOT_PROVIDED` | Blocking |
|---|---|---|---|
| P0 | 10 | 10 | Stage 0.4C-1 TC acquisition — **BLOCKED** |
| P1 | 5 | 5 | Wire-break decoding — **BLOCKED** |
| P2 | 5 | 5 | Rollout — **BLOCKED** |
| **Total** | **20** | **20** | — |

---

## 10. Galil parallel evidence intake register

Kept **separate** from the TC acquisition register. No Galil control is implemented, proposed or
authorised. This is an evidence request list for future G1–G5 only.

Classification vocabulary: `PROVIDED` · `NOT_PROVIDED` · `VERIFIED` · `OWNER_INPUT_PENDING` ·
`BLOCKING_GATE`.

| ID | Item | Classification | Note |
|---|---|---|---|
| GAL-01 | Controller model | `NOT_PROVIDED` | No Galil hardware evidence exists in the repository or this sandbox |
| GAL-02 | Firmware | `NOT_PROVIDED` | — |
| GAL-03 | Network identity | `NOT_PROVIDED` | Naming one would invent a production value |
| GAL-04 | Axis count | `NOT_PROVIDED` | — |
| GAL-05 | Axis assignment | `NOT_PROVIDED` | — |
| GAL-06 | Motor / drive identity | `NOT_PROVIDED` | — |
| GAL-07 | Limit wiring and polarity | `NOT_PROVIDED` | Safety-relevant; never inferred |
| GAL-08 | Home / standby definition | `NOT_PROVIDED` | — |
| GAL-09 | E-stop / enable chain | `NOT_PROVIDED` | Safety-relevant; never inferred |
| GAL-10 | Safe speed and acceleration limits | `BLOCKING_GATE` | Production motion value; AGENTS.md §2.5 forbids invention |
| GAL-11 | Permitted travel envelope | `BLOCKING_GATE` | Production coordinate/limit value; AGENTS.md §2.5 forbids invention |
| GAL-12 | Fault and status representation | `NOT_PROVIDED` | — |
| GAL-13 | Approved commissioning procedure | `OWNER_INPUT_PENDING` | Owner must supply before any Galil commissioning is proposed |

**Galil status: NO EVIDENCE, NO IMPLEMENTATION, NO AUTHORISATION.** Galil work stays outside
this Stage and outside Domain B.

---

## 11. Related documents

- [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) — this Stage's checkpoint record
- [STAGE_0.4C-0_CHECKPOINT.md](STAGE_0.4C-0_CHECKPOINT.md) — predecessor evidence gate and Owner decisions OQ-01 to OQ-08
- [CURRENT_STATE.md](CURRENT_STATE.md) — verified state and stage status
- [MASTER_PLAN.md](MASTER_PLAN.md) — stage ledger
- [PUBLIC_REPOSITORY_BOUNDARY.md](PUBLIC_REPOSITORY_BOUNDARY.md) — what may be published
- [../AGENTS.md](../AGENTS.md) — working contract and evidence-first rules
