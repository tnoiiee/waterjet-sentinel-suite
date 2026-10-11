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
| Current gate | `TC_ACQUISITION_TOPOLOGY: VERIFIED_OWNER_SOURCE` (drawing scope — as-drawn, not as-installed) · `SENSOR_TO_TC_CHANNEL_SCHEDULE: VERIFIED_OWNER_SOURCE` (complete: 212/212 identities scheduled; 12 internal + 4 topology conflicts recorded per-row, 0 unresolved) · `STAGE_0.4C-1_TC_ACQUISITION: BLOCKED` (configuration, transport identity, hardware observation still missing) · `WIRE_BREAK_DECODING: BLOCKED` (configured/observed behaviour still missing; decoding contract documented at `MODEL_LEVEL` only) |
| Stage 0.4C-0B review (2026-10-11) | Brief-stated Owner statements recorded as `PROVIDED_UNVERIFIED` (TCEV-12, TCEV-13; `REQ-TC-001`, `REQ-TC-002` at statement-only scope); drawings and manuals recorded `NOT_AVAILABLE_IN_SESSION` (TCEV-14 … TCEV-16); schedule derivation STOPPED with 0 rows — see §12 and [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md). No verified state was widened |
| Stage 0.4C-0B completion review (2026-10-12) | Baseline REV1 evidence arrived via the Owner-connected source, integrity-verified (TCEV-17 … TCEV-20) and fully reviewed: 240-row drawing schedule derived with per-row states (12 internal + 4 topology conflicts recorded, 0 unresolved), 750-498/362/600 manuals extracted with section/page refs, process-image profile + decoding contract derived at `MODEL_LEVEL`. `REQ-TC-001` … `REQ-TC-007` advance to drawing/manufacturer-verified states; `REQ-TC-008` … `REQ-TC-020` stay `NOT_PROVIDED`. See §13, [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md) §7 and `docs/evidence/` |
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

Stage 0.4C-0A position (preserved): every field was `NOT_PROVIDED`. The Stage
0.4C-0B completion review (§13) verified the as-drawn identity cells against
TCEV-17; as-installed identity (revision, firmware, serial, nameplate) stays
`NOT_PROVIDED`.

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Manufacturer | WAGO (A101/A102/A110); Weidmüller (X110); Phoenix Contact (X101/X111/G101/F100); ABB (Q101) — drawing p32 | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-001 |
| Model | A101 = 750-362, A102 = 750-498, A110 = 750-600, X110 = WDU 2.5/TC TYP K, X111 = UT 2.5-PE — drawing p32 rows 7–11 | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-001 |
| Variant / order number | Art. Nos. per p32 (750-362 / 750-498 / 750-600 / 1024100000 / 3044092 / …) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-001 |
| Hardware revision | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |
| Firmware / software revision | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001, REQ-TC-010 |
| Serial number (Owner-authorised only) | `NOT_PROVIDED` | `NOT_PROVIDED` | — | Not required for Stage 0.4C-1 |
| Panel / rack | `+SDSCAB1` … `+SDSCAB6` — drawing pp6–11/14/20–32 | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-003 |
| Physical location | As-designed elevations EL.40.000 (CAB1/2), EL.36.500 (CAB3/4), EL.26.670 (CAB5/6) — drawing pp20–31; as-installed location not evidenced | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-003 |
| Power source | `-G101` TRIO-PS-2G/1AC/24DC/10 + `-Q101` S202M-C16, feed `2x4.4 mm² CV-GA` from `+EROOMCAB QSDS1/2` — drawing p32 rows 5–6, pp6–7 | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-003 |
| Network / bus connection | A101 LAN from `-FBOCAB` via `4x2xAWG23 US-9106 OUT UTP CAT 6` to `FL SWITCH 1008NT` — drawing p19, pp6–7; node addresses not evidenced | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-010 |
| Photo or nameplate evidence reference | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-001 |

**Candidate families are not recorded as candidates.** The Stage 0.4C-0 task question set asks
whether the acquisition system is WAGO, another PLC family, a temperature multiplexer, a
recorder, a remote I/O system or another device. No evidence distinguishes these options, so
none is listed as favoured. Listing a favourite would be an assumption, not evidence.

---

## 4. Category B — Module / device topology

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Controller or head station identity | `-A101` = 750-362, one per cabinet — drawing p32 row 7, p19 (was brief-stated TCEV-12) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` (see §13) | REQ-TC-002 |
| Rack or node name | `+SDSCAB1` … `+SDSCAB6` (drawing); paired with `TC-NODE-01` … `TC-NODE-06` by `ASSUMED_ORDINAL_CORRESPONDENCE` only (§13.3) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` for cabinet tags; correspondence assumed, not verified | REQ-TC-002 |
| Module position | `-A101`, `-A102.1` … `-A102.5`, `-A110` per cabinet — drawing p14, pp20–31 | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-002 |
| Module model | 750-498 × 30 (5 per cabinet), 750-600 × 6 — drawing p32 rows 8–9, p14 (was brief-stated TCEV-12) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-002 |
| Channel capacity per module | 8 TC input Channels per 750-498 — drawing wiring tables + 750-498 manual §3.6.3 (p26) | `VERIFIED_OWNER_SOURCE` + `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `DRAWING_SCOPE` + `MODEL_LEVEL` | REQ-TC-002 |
| Channel numbering basis (0-based / 1-based) | 1-based `ch1` … `ch8` — drawing wiring-table `Channels` column | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-002, REQ-TC-009 |
| Process-data role (contributing / non-contributing) | 750-498 contributing (8×16-bit + status, TCEV-18 p30/p32); 750-600 non-contributing termination (TCEV-20) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-002 |
| Actual configuration-tool position | `NOT_PROVIDED` (no engineering export reviewed) | `NOT_PROVIDED` | — | REQ-TC-009 |
| Count of acquisition devices / modules | 6 cabinets; 750-362 × 6, 750-498 × 30, 750-600 × 6 — drawing-verified (was brief-stated TCEV-12) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-002 |
| Evidence source and scope per row | TCEV-17 (drawing) + TCEV-18/19/20 (manuals); per-Channel refs in `docs/evidence/tc-drawing-schedule-rev1.csv` | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-009 |

**Not derived (binding).** No module count has been computed from 212 Channels divided by any
assumed Channel capacity. The device count and per-device capacity are independent Owner facts
(REQ-TC-002); deriving one from the other would invent both. The Stage 0.4C-0B counts above
are recorded **as brief-stated Owner facts**, not computed — the 240/212/28 arithmetic in the
checkpoint recomputes the brief's own stated totals as a consistency check, not as an
independent derivation.

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
| Measurement Channel ID | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — per-Channel drawing identity (cabinet/module/ch + conductor label) in `docs/evidence/tc-drawing-schedule-rev1.csv`; 212 MATCHED + 4 gap-conflict + 24 spare | REQ-TC-004 |
| Terminal block | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `-X110` (TC) + `-X111` (PE) per Channel | REQ-TC-005 |
| Terminal number | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `-X110.1` … `-X110.36` + paired `-X111.n` per Channel (spare Channels: none) | REQ-TC-005 |
| Field cable or pair identity | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `T/C type K PVC Mylar Insulation`, pair = sheath label per Channel (12 sheath/conductor conflicts recorded) | REQ-TC-005, REQ-TC-006 |
| Panel | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `+SDSCAB1` … `+SDSCAB6` | REQ-TC-003 |
| Acquisition device | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `-A101` 750-362 per cabinet | REQ-TC-001, REQ-TC-002 |
| Module | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `-A102.1` … `-A102.5` per cabinet | REQ-TC-002 |
| Channel | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — `ch1` … `ch8` per module | REQ-TC-004 |
| Signal / TC type | `VERIFIED_OWNER_SOURCE`, `DRAWING_SCOPE` — Type K, all 240 Channels (TYP K terminals + Type K cable); configured per-instance type still unevidenced | REQ-TC-007 |
| Engineering unit | °C for temperature per 750-498 manual (TCEV-18: "for temperature measurements in °C", p72) — `VERIFIED_PRIMARY_MANUFACTURER_SOURCE`, `MODEL_LEVEL`; configured unit per instance unevidenced | REQ-TC-007, REQ-TC-016 |
| Expected normal range | `NOT_PROVIDED` (no operating ranges in any reviewed evidence) | REQ-TC-016 |
| Evidence source | `VERIFIED_OWNER_SOURCE` — TCEV-17 + per-row `EvidenceRef` in the schedule CSV | REQ-TC-004 |
| Verification state | Per-row states in the schedule CSV: 200 `DRAWING_VERIFIED`, 12 `DRAWING_INTERNAL_CONFLICT`, 4 `DRAWING_TOPOLOGY_CONFLICT`, 24 `SPARE_UNMAPPED`, 0 unresolved | REQ-TC-004 |

**Not inferred (binding).** The 212 TC Channels are **not** mapped to `AI-001` through `AI-035`.
No Channel A/B placement, no module assignment and no TC Channel ordering has been inferred from
Sensor order, Sensor wall, logical row/column, or scan order.

---

## 6. Category D — Thermocouple configuration

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| TC type per Channel (or per approved Channel group) | Type K, all 240 Channels — drawing (TYP K terminals + Type K cable) | `VERIFIED_OWNER_SOURCE` | `DRAWING_SCOPE` | REQ-TC-007 |
| Grounded / ungrounded configuration | `NOT_PROVIDED` (model-level input topology only: 2-conductor single-ended, TCEV-18 p26 — not a grounding configuration) | `NOT_PROVIDED` | — | REQ-TC-007 |
| Cold-junction compensation method | `NOT_PROVIDED` as configured (factory default: activated — TCEV-18 p70, `MODEL_LEVEL`, under `OWNER_CONFIRMED_FACTORY_DEFAULT` basis) | `NOT_PROVIDED` | — | REQ-TC-008 |
| CJC source (internal / external / shared reference) | `NOT_PROVIDED` as configured (factory default: internal — TCEV-18 p70, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-008 |
| Configured measurement range | `NOT_PROVIDED` as configured (Type K ID3 span −200…+1370 °C — TCEV-18 p35, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-009 |
| Configured data format | `NOT_PROVIDED` as configured (factory default: two's complement 0.1 °C/digit — TCEV-18 pp36/67, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-009, REQ-TC-010 |
| Channel enable / disable | `NOT_PROVIDED` as configured (factory default: activated; deactivated → `0x7FFF` — TCEV-18 p70, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-009 |
| Filter | `NOT_PROVIDED` as configured (factory default: notch 50 Hz — TCEV-18 p67, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-009 |
| Diagnostic settings | `NOT_PROVIDED` as configured (factory default: globally on, wire break + over/underrange shown — TCEV-18 p71, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-011 |
| Wire-break settings | `NOT_PROVIDED` as configured (factory default: shown; wire break → `0x7FFF`/status `0x60` — TCEV-18 pp39/71, `MODEL_LEVEL`) | `NOT_PROVIDED` | — | REQ-TC-011 |
| Substitute-value behaviour | `NOT_PROVIDED` (saturation + clamp behaviour per TCEV-18 pp39/84–85 is `MODEL_LEVEL` diagnostic behaviour, not a configured substitute value) | `NOT_PROVIDED` | — | REQ-TC-011, REQ-TC-012 |
| Update cycle | `NOT_PROVIDED` (conversion ≤100 ms/ch notch-on — TCEV-18 p26, `MODEL_LEVEL` module timing, not the acquisition cycle) | `NOT_PROVIDED` | — | REQ-TC-017 |

**Domain A channel settings are not transferred.** Stage 0.4C-0 records exactly one
Channel-level settings observation (Pos. 10 / RackSlot 13 / Channel 3), a Domain A analog input.
Its signal type, filter, diagnosis and user-limit values say nothing about any Domain B
thermocouple Channel and have not been copied into this category.

---

## 7. Category E — Transport and process representation

| Field | Value | State | Scope | Blocks |
|---|---|---|---|---|
| Transport type | Modbus TCP — the as-drawn head station is a 750-362 FC Modbus TCP coupler (TCEV-17 p32 + TCEV-19); instance network/addresses unevidenced | `VERIFIED_OWNER_SOURCE` + `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `DRAWING_SCOPE` + `MODEL_LEVEL` | REQ-TC-010 |
| Read protocol | Modbus TCP master/slave; inputs via FC3/FC4 (TCEV-19 pp81/188) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010 |
| Node / unit identity | `NOT_PROVIDED` (no IP plan in evidence; TEST ONLY brief-stated IPs remain statement-only TCEV-13) | `NOT_PROVIDED` | — | REQ-TC-010 |
| Process-data source | 5 × 750-498 × 8 words = 40 words at FC3/FC4 addresses 0–39 per node — DERIVED (chain cited in the [profile](evidence/tc-process-image-profile-rev1.md) §3) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` (derivation) | `MODEL_LEVEL` | REQ-TC-010 |
| Byte order | Intel format (D0 low, D1 high) — TCEV-19 p80/p220 + TCEV-18 p32 | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010 |
| Word order | Single-word Channels — no multi-word order applies (TCEV-19 p80 word-by-word) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010 |
| Channel width | 16-bit data word per Channel; control/status bytes NOT visible via Modbus/TCP (TCEV-19 p220) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010 |
| Status / diagnostic representation | No status byte on the wire via 750-362 Modbus — diagnostics are value-based only (TCEV-19 p220); status-byte layout documented for local-bus/WAGO-I/O-CHECK scope (TCEV-18 p34) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010, REQ-TC-011 |
| Value encoding | Type K 0.1 °C/digit, two's complement, manufacturer scaling (TCEV-18 pp35–36/39) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-010 |
| Invalid-value encoding | Wire break `0x7FFF` (ambiguous with deactivated Channel); CJ violation `0x8000`; clamps −2000/+13700 (TCEV-18 pp39/70) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | `MODEL_LEVEL` | REQ-TC-011, REQ-TC-015 |
| Polling / acquisition cycle | `NOT_PROVIDED` (no acquisition design or configured cycle in evidence) | `NOT_PROVIDED` | — | REQ-TC-017 |
| Reconnect behaviour | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-014 |
| Stale-data threshold basis | `NOT_PROVIDED` | `NOT_PROVIDED` | — | REQ-TC-018 |

**Offsets: derived at `MODEL_LEVEL` only (§13).** Per-node Modbus word offsets 0–39
are derived from the 750-362 mapping rules + 750-498 data width (see the
[profile](evidence/tc-process-image-profile-rev1.md) §3); they are marked
`DERIVED_MODEL_LEVEL` in the schedule CSV, never drawing-observed, and no
instance offset is verified. No offset has been carried over from the Domain A
candidate image (itself `CANDIDATE_UNVERIFIED`, not a Domain B source).

**Transport vocabulary is now evidenced at model/drawing scope (§13).** Modbus TCP
is the as-drawn head-station protocol (TCEV-17 p32 + TCEV-19), not an invented
value. Instance transport identities remain `NOT_PROVIDED` and nothing here
authorises a connection.

**Stage 0.4C-0B:** brief-stated TEST ONLY endpoint identities (`TC-NODE-01` … `TC-NODE-06` at
`192.0.2.11` … `192.0.2.16`) are recorded as Owner statements (TCEV-13) — they establish no
transport, protocol, byte/word order, width or encoding. Every row of the table above therefore
stays `NOT_PROVIDED`. See §12.

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
| TCEV-12 | Stage 0.4C-0B brief §2: six-node physical TC baseline statement (6 nodes; 750-362 × 6, 750-498 × 30, 750-600 × 6; 240/212/28 capacity; `OWNER_CONFIRMED_FACTORY_DEFAULT` basis) | Owner statement in the authorising brief | — (statement, no file) | — (no file; none invented) | — | Brief §2 as recorded in [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) §2 | Model facts at `MODEL_LEVEL`; instance counts stated, not verified | `PROVIDED_UNVERIFIED` | Stage 0.4C-0B | 2026-10-11 | None | Advances REQ-TC-001, REQ-TC-002 to statement-only `PROVIDED_UNVERIFIED`. Verifies no instance, no drawing, no default value |
| TCEV-13 | Stage 0.4C-0B brief §3: six-node configurable test-IP statement (`192.0.2.11` … `192.0.2.16`, TEST ONLY) | Owner statement in the authorising brief | — (statement, no file) | — (no file; none invented) | — | Brief §3 as recorded in [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) §3 | Stated endpoint identities only | `PROVIDED_UNVERIFIED` | Stage 0.4C-0B | 2026-10-11 | None | Establishes no transport, protocol, encoding or production value. Does not advance REQ-TC-010 |
| TCEV-14 | Six-cabinet TC drawings (Baseline rev1 mapping authority) | Expected: Owner-provided drawings | **No file visible in sandbox** | Not present in sandbox — **no hash recorded, none invented** | — | Filenames/sheets/revision unspecified in the brief | — | `NOT_AVAILABLE_IN_SESSION` | Stage 0.4C-0B | 2026-10-11 | None | Blocks REQ-TC-004, REQ-TC-005, REQ-TC-006. Schedule derivation STOPPED, 0 rows. See [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md) |
| TCEV-15 | WAGO 750-498 primary manufacturer manual `m07500498_00000000_0en.pdf` | Expected: primary manufacturer manual | **No file visible in sandbox** | Not present in sandbox — **no hash recorded, none invented** | — | Exact filename from the brief; revision unknown | — | `NOT_AVAILABLE_IN_SESSION` | Stage 0.4C-0B | 2026-10-11 | None | Blocks factory-default extraction and the decoding contract (REQ-TC-007, REQ-TC-011). No section or value cited |
| TCEV-16 | WAGO 750-362 and 750-600 primary manufacturer manuals (filenames unspecified in the brief) | Expected: primary manufacturer manuals | **No file visible in sandbox** | Not present in sandbox — **no hash recorded, none invented** | — | — | — | `NOT_AVAILABLE_IN_SESSION` | Stage 0.4C-0B | 2026-10-11 | None | Blocks the 750-362 process-image profile and offset manifest (REQ-TC-009, REQ-TC-010). No rule or width derived |
| TCEV-17 | Combined six-cabinet TC drawing `T8_SDS_Cabinet_-Draft-7E.pdf` (REV 1A, 16 MAR 2026; DXN, Mae Moh T8) | Owner-provided drawing (32-page PDF) | Owner-connected source, reviewed from ephemeral intake — **archive not committed** | `08db6f2afd72f0f8a185bfcd30844e7221a332a030bbdade46dafe8c141d6b98` (7539526 bytes; computed from bytes present at review) | — | Wiring tables pp20/22/24/26/28/30; X110 diagrams pp21/23/25/27/29/31; overviews pp6–11; pp14/19/32 | `DRAWING_SCOPE` (as-drawn, 6 cabinets) | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-0B | 2026-10-12 | ISS-REV1-01 (sheath/conductor, 12 rows), ISS-REV1-03 (p6/p7 swap) — both recorded, neither discarded | All 240 cells read on-render. Mapping authority for the REV1 schedule. Drawing stays `DRAWING_PENDING_REVISION` (REV 1A, no document number) |
| TCEV-18 | WAGO 750-498 primary manufacturer manual `m07500498_00000000_0en.pdf` (v1.0.0, Nov 2019, 96 pp) | Primary manufacturer manual | Owner-connected source, reviewed from ephemeral intake — **not committed** | `ed66534d10b0fe95e62c0241cefa4e6057c2cbca060d97a14a6961241611fe83` (5041172 bytes; computed from bytes present at review) | — | §3.6 (pp25–30), §4 (pp32–46), §7.1.1.4 (pp67–72), §8 (pp84–85) | `MODEL_LEVEL` (750-498) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | Stage 0.4C-0B | 2026-10-12 | None | Type K ID3 encoding, status bits, factory defaults, diagnostic behaviour extracted. Establishes no configured instance value |
| TCEV-19 | WAGO 750-362 primary manufacturer manual `m07500362-xxxxxxxx-0en.pdf` (v1.1.1, Apr 2022, 296 pp) | Primary manufacturer manual | Owner-connected source, reviewed from ephemeral intake — **not committed** | `85e72f83eedcf60a5b4abdd4c38b88ed546e3601da4b80bd7c19eb57d6c427bb` (4224370 bytes; computed from bytes present at review) | — | §4 (p51), §7.2–7.3 (pp79–84), §11.2.4 (p188), §12.2.3 (pp220–223) | `MODEL_LEVEL` (750-362) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | Stage 0.4C-0B | 2026-10-12 | Evidence gap (not a conflict): 750-498 not enumerated in Table 201 (p223) — derivation chain cited instead | Analog-first physical-order mapping, 16-bit-words-only / Intel format, FC3/FC4 map. Establishes no node identity or instance behaviour |
| TCEV-20 | WAGO 750-600 primary manufacturer manual `m07500600-xxxxxxxx-0en.pdf` (v1.3.0, 2015, 38 pp) | Primary manufacturer manual | Owner-connected source, reviewed from ephemeral intake — **not committed** | `c390260c5d6041ad271fe6bb23d1f814b6cc3ed9cc6d34f673e70e4f9c604b46` (1751049 bytes; computed from bytes present at review) | — | Device description (bus termination; no process-data section exists) | `MODEL_LEVEL` (750-600) | `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` | Stage 0.4C-0B | 2026-10-12 | None | Passive end module: no process data. Topology role only |

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
| REQ-TC-001 | Thermocouple acquisition hardware manufacturer and model | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17 p32: WAGO 750-362 / 750-498 / 750-600 + terminal/Power models per cabinet (supersedes statement-only TCEV-12) | `DRAWING_SCOPE` (as-drawn; nameplate/hardware still unevidenced) | Verified as-drawn | None | Owner supplies nameplate/instance evidence for as-installed identity |
| REQ-TC-002 | Count of acquisition devices/modules and Channel capacity | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17 (6 cabinets × 1+5+1; 8 ch/module) + 750-498 manual TCEV-18 p26 (supersedes TCEV-12) | `DRAWING_SCOPE` + capacity `MODEL_LEVEL` | Verified as-drawn | None | None for counts; instance verification rides on hardware observation |
| REQ-TC-003 | Physical panel, rack, cabinet or controller location | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17: `+SDSCAB1` … `+SDSCAB6` + design elevations EL.40.000/36.500/26.670 | `DRAWING_SCOPE` (as-designed; as-installed location unevidenced) | Verified as-drawn | None | Owner confirms as-installed locations at commissioning |
| REQ-TC-004 | Complete Sensor ID to Channel A/B schedule (all 106 Sensors, 212 Channels) | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17 → `docs/evidence/tc-drawing-schedule-rev1.csv` (240 rows; 212 MATCHED incl. 12 internal-conflict rows followed conductor+X110; 4 gap-conflict; 24 spare; 0 unresolved) | `DRAWING_SCOPE`, `COMPLETE_SENSOR_SCHEDULE` (all 212 identities scheduled) | Verified with recorded conflicts | ISS-REV1-01 (12 rows), ISS-REV1-02 (I7/I16, 4 rows) — see the issue register §7.4 | Owner rules on ISS-REV1-01/02 via drawing revision (REV2); no manual transcription requested |
| REQ-TC-005 | Terminal schedule | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17: `-X110.1` … `-X110.36` + paired `-X111.n` per Channel (spare Channels: none) | `DRAWING_SCOPE`, per-Channel | Verified as-drawn | ISS-REV1-01 affects 12 rows' sheath pairing only, not terminal numbers | None for terminals |
| REQ-TC-006 | Wiring drawings | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17 reviewed (REV 1A, 16 MAR 2026; hash-recorded, archive not committed) | `DRAWING_SCOPE` (6 cabinets) | Verified present and reviewed | ISS-REV1-01, ISS-REV1-03 (p6/p7 swap) — recorded, pending Owner revision; drawing stays `DRAWING_PENDING_REVISION` | Owner revises per the issue register §7.7 |
| REQ-TC-007 | TC type per Channel or per approved Channel group | `VERIFIED_OWNER_SOURCE` | Stage 0.4C-1 | Drawing TCEV-17: Type K all 240 Channels (TYP K terminals + Type K cable); 750-498 default Type K (TCEV-18 p70) | `DRAWING_SCOPE` + default `MODEL_LEVEL`; configured per-instance type unevidenced | Verified as-drawn | None | Owner confirms via configuration evidence if any instance deviates from default |
| REQ-TC-008 | Cold-junction compensation configuration | `NOT_PROVIDED` | Stage 0.4C-1 | Factory default only: CJC activated, internal (TCEV-18 p70, `MODEL_LEVEL`, under `OWNER_CONFIRMED_FACTORY_DEFAULT` basis) — configured value unevidenced | — | Cannot verify configured value | None | Owner supplies CJC method/source per instance (export or screenshots) |
| REQ-TC-009 | Engineering-tool export or complete configuration screenshots | `NOT_PROVIDED` | Stage 0.4C-1 | None (TCEV-08). Factory defaults now documented at `MODEL_LEVEL` (TCEV-18 pp67–72) but no instance export exists | — | Cannot verify | None | Owner supplies export or screenshots covering every instance; partial screenshots accepted only at `PARTIAL_SCREENSHOT` scope |
| REQ-TC-010 | Transport/network identity and read representation | `NOT_PROVIDED` | Stage 0.4C-1 | Read representation at `MODEL_LEVEL` only: Modbus TCP, FC3/FC4 words 0–39, Intel, 16-bit, Type K encoding (TCEV-18/19; [profile](evidence/tc-process-image-profile-rev1.md)). Node identities/addresses unevidenced | — | Cannot verify instance transport | None | Owner supplies the node transport plan (cabinet→node→address) |

### 9.2 P1 — blocks wire-break implementation

| ID | Request | Status | Blocking stage | Evidence received | Evidence scope | Verification result | Conflict | Next action |
|---|---|---|---|---|---|---|---|---|
| REQ-TC-011 | Explicit wire-break diagnostic configuration | `NOT_PROVIDED` | Wire-break decoding | Factory default only: shown, globally on (TCEV-18 p71, `MODEL_LEVEL`) — configured per-Channel value unevidenced | — | Cannot verify configured value | None | Owner supplies per-Channel diagnostic configuration |
| REQ-TC-012 | Raw value and diagnostic/status during a normal connected Sensor | `NOT_PROVIDED` | Wire-break decoding | Encoding rule only: Type K 0.1 °C/digit two's complement (TCEV-18 pp35–36/39, `MODEL_LEVEL`) — no observed baseline | — | Cannot verify | None | Owner supplies observed baseline capture |
| REQ-TC-013 | Raw value and diagnostic/status during an Owner-authorised open circuit | `NOT_PROVIDED` | Wire-break decoding | Manual rule only: open input → `0x7FFF` (TCEV-18 p39, `MODEL_LEVEL`) — no observed open-circuit capture | — | Cannot verify | None | **Requires the approved site procedure and LOTO controls first (REQ-TC-019).** No open-circuit instruction is authorised without them |
| REQ-TC-014 | Recovery behaviour after reconnect | `NOT_PROVIDED` | Wire-break decoding | None (TCEV-10). Diagnosis min-display 100 ms (TCEV-18 p85, `MODEL_LEVEL`) is display timing, not recovery behaviour | — | Cannot verify | None | Owner supplies observed recovery capture and stabilisation basis |
| REQ-TC-015 | Under-range, over-range and module-fault representations | `NOT_PROVIDED` | Wire-break decoding | Manual encodings only: clamps −2000/+13700, `0x8000` CJ violation (TCEV-18 p39, `MODEL_LEVEL`) — module-fault representation unevidenced; status bytes not on the Modbus wire (TCEV-19 p220) | — | Cannot verify as-observed | None | Owner supplies the diagnostic/substitute encoding per condition |

### 9.3 P2 — required for rollout

| ID | Request | Status | Blocking stage | Evidence received | Evidence scope | Verification result | Conflict | Next action |
|---|---|---|---|---|---|---|---|---|
| REQ-TC-016 | Expected normal operating ranges by Sensor or approved group | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner supplies ranges; no range may be inferred from another Sensor |
| REQ-TC-017 | Channel update cycle | `NOT_PROVIDED` | Rollout | Module conversion timing only: ≤100 ms/ch notch-on (TCEV-18 p26, `MODEL_LEVEL`) — not the acquisition cycle | — | Cannot verify | None | Owner supplies the configured cycle |
| REQ-TC-018 | Permitted stale threshold basis | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner supplies threshold basis. Per OQ-06 this stays `CONFIGURABLE / OWNER_INPUT_PENDING`; no default is authorised |
| REQ-TC-019 | Commissioning procedure and LOTO reference | `NOT_PROVIDED` | Rollout; also gates REQ-TC-013 | None (TCEV-11) | — | Cannot verify | None | Owner supplies the approved procedure identifier. Per OQ-08 the author is the Commissioning / Instrument responsible role and safety approval is an Owner-authorised site safety role |
| REQ-TC-020 | Responsible approval roles | `NOT_PROVIDED` | Rollout | None (TCEV-11) | — | Cannot verify | None | Owner names the roles; OQ-08 records the role structure, not the named holders |

### 9.4 Register summary at this head

| Priority | Count | `NOT_PROVIDED` | Advanced (§13) | Blocking |
|---|---|---|---|---|
| P0 | 10 | 3 (`REQ-TC-008/009/010`) | 7 (`REQ-TC-001` … `REQ-TC-007` → drawing/manufacturer-verified) | Stage 0.4C-1 TC acquisition — **BLOCKED** |
| P1 | 5 | 5 | 0 (model-level encodings documented; nothing configured/observed) | Wire-break decoding — **BLOCKED** |
| P2 | 5 | 5 | 0 | Rollout — **BLOCKED** |
| **Total** | **20** | **13** | **7** | — |

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

- [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) — Stage 0.4C-0B checkpoint record (six-node baseline, STOP record, proposals)
- [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md) — drawing intake, missing-file STOP report, revision rule, review completion (§7)
- [evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv) — derived 240-row REV1 schedule
- [evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md) — process-image profile + decoding contract
- [evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md) — narrow validation summary
- [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) — predecessor checkpoint record
- [STAGE_0.4C-0_CHECKPOINT.md](STAGE_0.4C-0_CHECKPOINT.md) — predecessor evidence gate and Owner decisions OQ-01 to OQ-08
- [CURRENT_STATE.md](CURRENT_STATE.md) — verified state and stage status
- [MASTER_PLAN.md](MASTER_PLAN.md) — stage ledger
- [PUBLIC_REPOSITORY_BOUNDARY.md](PUBLIC_REPOSITORY_BOUNDARY.md) — what may be published
- [../AGENTS.md](../AGENTS.md) — working contract and evidence-first rules

---

## 12. Stage 0.4C-0B evidence-availability review (2026-10-11)

Reviewer: Stage 0.4C-0B session, branch `arena/c4b63a26-waterjet-sentinel-suite`, base
`main` `3e1a7fd9d28db65221c123960a0c85b62d93949a` (PR #17 merge commit). Workbook TCEV-01
re-verified: `T8_IO_Card_Mapping.xlsx`, 16648 bytes, SHA-256
`4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, unchanged.

### 12.1 Vocabulary added by this review

| State | Meaning |
|---|---|
| `NOT_AVAILABLE_IN_SESSION` | The authorising brief asserts the Owner holds this evidence, but no file was visible in this sandbox: no filename, hash, size, title, revision, scope or review status could be recorded, and none was invented or reused from memory. For gate purposes it is equivalent to `NOT_PROVIDED` |

### 12.2 Statement intake (TCEV-12, TCEV-13)

The Stage 0.4C-0B authorising brief — the latest explicit Owner instruction — states the
six-node physical baseline (brief §2) and the test-IP configuration (brief §3). These are
recorded as **Owner statements** at `PROVIDED_UNVERIFIED`: supplied by the Owner through the
brief, with content that cannot be checked in this sandbox (no drawing, manual, nameplate,
export or hardware is present). Statement intake advances `REQ-TC-001` and `REQ-TC-002` to
statement-only `PROVIDED_UNVERIFIED` (model facts at `MODEL_LEVEL`; instance counts stated,
not verified) and supersedes the Category A `Manufacturer`/`Model` cells and the Category B
identity/count/capacity cells marked in §§3–4. It verifies no installed instance, no drawing
content, no default value, no transport and no production value, and it widens no verified
state: the non-propagation rule (§2.3) applies to statements exactly as to verified facts.

### 12.3 Absent evidence (TCEV-14 … TCEV-16) and STOP record

Workspace-wide search (all PDF, DWG/DXF, drawing-named and image formats, plus tracked-file
review) returned **zero evidence files**. The six-cabinet drawings, the 750-498 manual
`m07500498_00000000_0en.pdf`, and the 750-362 / 750-600 manuals (filenames unspecified in the
brief) are therefore `NOT_AVAILABLE_IN_SESSION`. Schedule derivation STOPPED with 0 rows
(brief §4), manual extraction STOPPED with no section or value cited (brief §§7–8), and the
capacity STOP was recorded with 0 drawing-derived used Channels against 212 expected
(brief §12). Full record: [TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md)
and [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) §4.

### 12.4 What this review did not change

- `REQ-TC-003` … `REQ-TC-020` keep their recorded states (all `NOT_PROVIDED`).
- Category C–E acquisition tables keep every recorded state; the added notes point at this
  section and change no cell.
- No SHA-256 was recorded or reused for any absent file. No schedule row, offset, decoding
  rule, configuration value or production value was created.

---

## 13. Stage 0.4C-0B evidence-review completion (2026-10-12)

Reviewer: Stage 0.4C-0B session (continued), branch
`arena/c4b63a26-waterjet-sentinel-suite`, base `main`
`3e1a7fd9d28db65221c123960a0c85b62d93949a` (PR #17 merge commit). The Baseline
REV1 evidence arrived after the §12 STOP via the Owner-connected source,
passed the full integrity gate, and was reviewed page by page (§13.2). The
§12 absence record above is preserved unchanged as audit history.

### 13.1 Vocabulary added by this review

| Scope | Meaning |
|---|---|
| `DRAWING_SCOPE` | True as-drawn for the named cabinet/module/Channel in the reviewed drawing revision. Says nothing about installation, configuration, or as-built state. Never propagates to `DEVICE_INSTANCE`, `MODULE_INSTANCE` (as-installed) or `CHANNEL` (as-observed) without new evidence |

The non-propagation rule (§2.3) applies to `DRAWING_SCOPE` exactly as to verified
facts: a drawing fact never establishes an installed instance, a configured value,
or an observed behaviour.

### 13.2 Evidence intake (TCEV-17 … TCEV-20)

Delivery: `OWNER_GOOGLE_DRIVE_CONNECTOR` (Owner-connected source; no link, ID or
token recorded). Archive `WJSS_TC_EVIDENCE_BASELINE_REV1.zip`: exactly 17345849
bytes, SHA-256 `7B886950D7F5F665416241E4CB502CD1A5787E3581788C799077C062786AA23E`,
ZIP-signed, central directory valid, 4 listable members, extracted with
traversal/symlink/encryption guards to an ephemeral directory and hashed
per-member; the ephemeral bytes were removed after review. The archive and the
evidence PDFs are **not** committed to the repository; the hashes below are the
re-verification basis (the Owner can re-verify the same archive by hash).

Full member hashes: drawing
`08db6f2afd72f0f8a185bfcd30844e7221a332a030bbdade46dafe8c141d6b98`
(7539526 bytes); 750-498 manual
`ed66534d10b0fe95e62c0241cefa4e6057c2cbca060d97a14a6961241611fe83`
(5041172 bytes); 750-362 manual
`85e72f83eedcf60a5b4abdd4c38b88ed546e3601da4b80bd7c19eb57d6c427bb`
(4224370 bytes); 750-600 manual
`c390260c5d6041ad271fe6bb23d1f814b6cc3ed9cc6d34f673e70e4f9c604b46`
(1751049 bytes). TCEV-17 … TCEV-20 in §8 carry the per-item record.

Review performed: drawing — all 240 wiring-table cells read on-render
(pp20/22/24/26/28/30), all 6 X110 diagram SDS sequences checked against the
table conductor order (pp21/23/25/27/29/31), conflict zones/I7/I16
cells/overview labels confirmed on 300-DPI crops, topology pages verified
(pp6/7/14/19/32); manuals — 750-498 §§3.6/4/7/8, 750-362 §§4/7/11/12,
750-600 device description, all with page/table references (see the
[process-image profile](evidence/tc-process-image-profile-rev1.md)).

### 13.3 Explicit assumptions (labelled, not verified)

1. `ASSUMED_ORDINAL_CORRESPONDENCE`: drawing cabinets `+SDSCAB1` … `+SDSCAB6`
   are paired with baseline nodes `TC-NODE-01` … `TC-NODE-06` by ordinal. No
   reviewed source equates the namespaces; zero contradicting evidence.
2. `ASSUMED_SIDE_CORRESPONDENCE`: template `FRONT`↔drawing `F`,
   `REAR`↔drawing `R`, on the structural 1:1 basis (exactly one channel per
   sensor per side on both sides). The CSV preserves `F`/`R` verbatim.
3. `OWNER_CONFIRMED_FACTORY_DEFAULT` (brief-stated, TCEV-12): instances run
   factory defaults. All configured-value content of the decoding contract
   rests on this basis plus manual `MODEL_LEVEL` defaults; any deviation
   invalidates the corresponding contract row.

### 13.4 What this review changed

- Categories A–E: drawing-observed cells → `VERIFIED_OWNER_SOURCE` /
  `DRAWING_SCOPE`; manual-extracted model facts →
  `VERIFIED_PRIMARY_MANUFACTURER_SOURCE` / `MODEL_LEVEL`. Cells needing
  installation, configuration or observation stay `NOT_PROVIDED` with notes.
- `REQ-TC-001` … `REQ-TC-007` advance to drawing/manufacturer-verified states
  (see §9.1); `REQ-TC-008` … `REQ-TC-020` stay `NOT_PROVIDED` (§9.4 recount:
  13 `NOT_PROVIDED`, 7 advanced).
- The 240-row schedule ([evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv)),
  the process-image profile + decoding contract
  ([evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md))
  and the narrow validation summary
  ([evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md))
  are created as the derived artefacts. The issue register records the three
  drawing issues and the spare set ([TC_DRAWING_ISSUE_REGISTER_REV1.md](TC_DRAWING_ISSUE_REGISTER_REV1.md) §7.4).

### 13.5 What this review did not change

- No installed instance, configured value, transport identity or observed
  behaviour is established. `STAGE_0.4C-1_TC_ACQUISITION` and
  `WIRE_BREAK_DECODING` stay `BLOCKED`.
- The template and workbook are byte-identical (read-only use only).
- No SHA-256 was invented: every hash above was computed from bytes physically
  present in the sandbox at review time.
- No connection, loop test, polling or hardware interaction was performed or
  authorised.
time.
- No connection, loop test, polling or hardware interaction was performed or
  authorised.
