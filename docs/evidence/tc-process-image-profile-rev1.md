# TC Process-Image Profile + Decoding Contract — Baseline REV1 (Stage 0.4C-0B)

Status: **DEVELOPMENT CHECKPOINT record, SUBMITTED FOR OWNER REVIEW.** This profile
is derived **only** from the Baseline REV1 evidence reviewed in-session (drawing
`T8_SDS_Cabinet_-Draft-7E.pdf`, manuals `m07500498_00000000_0en.pdf`,
`m07500362-xxxxxxxx-0en.pdf`, `m07500600-xxxxxxxx-0en.pdf`). It authorises no
polling, no connection and no production use. Every rule below carries its exact
source; derivation steps are labelled `DERIVED` where they combine sources, and
evidence gaps are stated explicitly. It is not a configuration and not a
commissioning record.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0B |
| Evidence baseline | `BASELINE_REV1` |
| Drawing status | `DRAWING_PENDING_REVISION` (REV 1A, 16 MAR 2026 — as-found, still pending Owner revision; review does not approve it) |
| Companion schedule | [tc-drawing-schedule-rev1.csv](tc-drawing-schedule-rev1.csv) (240 rows) |
| Companion validation | [tc-schedule-validation-rev1.md](tc-schedule-validation-rev1.md) |
| Companion records | [../TC_DRAWING_ISSUE_REGISTER_REV1.md](../TC_DRAWING_ISSUE_REGISTER_REV1.md), [../TC_EVIDENCE_SOURCE_REGISTER.md](../TC_EVIDENCE_SOURCE_REGISTER.md) |

**Node/cabinet correspondence assumption (explicit).** The drawing names cabinets
`+SDSCAB1` … `+SDSCAB6`; the Stage baseline names nodes `TC-NODE-01` …
`TC-NODE-06`. No reviewed source equates the two namespaces. This profile maps
them **by ordinal** (`+SDSCABn` ↔ `TC-NODE-0n`) as a working assumption, flagged
`ASSUMED_ORDINAL_CORRESPONDENCE` wherever it is used. If the Owner's cabinet→node
assignment differs, every per-node address in §3 moves with it.

---

## 1. Node composition (drawing evidence)

Per cabinet (drawing p32 DEVICE COMPONENTS rows 7–11, confirmed on-render; p14
disposition; p19 A101 wiring):

| Position | Tag | Model (drawing p32) | Process-data role |
|---|---|---|---|
| Head station | `-A101` | WAGO 750-362 Modbus Module | Coupler; exposes the node process image via Modbus TCP (§4) |
| TC modules ×5 | `-A102.1` … `-A102.5` | WAGO 750-498 T/C IO Module | 8 TC channels each → 40 process words per node (§3) |
| End module | `-A110` | WAGO 750-600 END Module | Bus termination only; **no process data** (750-600 manual: "used to terminate the internal bus of a fieldbus node … completes the internal data circuit") |
| TC terminals | `-X110.1` … `-X110.36` | WDU 2.5/TC TYP K (Weidmüller 1024100000) | 36 field channels per cabinet (A102.1–4 full + A102.5 ch1–4) |
| PE/shield | `-X111.1` … `-X111.36` | UT 2.5-PE (Phoenix Contact 3044092) | Paired 1:1 with X110 on the X110 diagrams (pp21/23/25/27/29/31) |

Cabinet elevations as drawn: CAB1/CAB2 EL.40.000 (pp20–23), CAB3/CAB4 EL.36.500
(pp24–27), CAB5/CAB6 EL.26.670 (pp28–31). A101 takes LAN from `-FBOCAB` (drawing
p19; cable `4x2xAWG23 US-9106 OUT UTP CAT 6` per installation overviews pp6–11).
Field cable: `T/C type K PVC Mylar Insulation` (pp6–11). Thermocouple type for
all 240 channels: **Type K** (drawing: TYP K terminals + Type K cable; 750-498
factory default Type K — manual p70 Table 39, marked factory setting).

---

## 2. Channel data format (750-498 evidence)

Source: 750-498 manual v1.0.0 (Nov 2019), §4 Process Image (pp32–46), §3.6
Technical Data (pp25–30), §7.1.1.4 commissioning defaults (pp67–72), §8
Diagnostics (pp84–85).

| Fact | Value | Source |
|---|---|---|
| Data per channel | 1 status byte + 1 data word (16 bits) | p32 |
| Word byte order on the local bus | Input byte 0 = low (D0), input byte 1 = high (D1) | p32 |
| Status-byte layout (all channels identical) | bit0 underrange, bit1 overrange, bit2 user-underrange, bit3 user-overrange, bit4 reserved, bit5 wire break, bit6 general error, bit7 RegCom | p34 Table 22 |
| Type K sensor ID | ID 3, DIN EN 60584-1 | p35 Table 23 |
| Type K range / resolution | −200 … +1370 °C at 0.1 °C/digit, two's complement, manufacturer scaling | p35 Table 23; p36 §4.3.2.1 |
| Reference points | 0.0 °C = `0x0000`; 100.0 °C = `0x03E8` (dec 1000) | p36 §4.3.2.1 |
| Cold-junction range violation | `0x8000` (−32768), status `0x00` | p39 Table 27 |
| Underrange clamp | `0xF830` (−2000 = −200.0 °C), status `0x41` | p39 Table 27 |
| Overrange clamp | `0x3584` (13700 = 1370.0 °C), status `0x42` | p39 Table 27 |
| Wire break | `0x7FFF` (32767), status `0x60` | p39 Table 27 |
| Deactivated channel | `0x7FFF` ("N/A" in the commissioning tool; on the wire identical to wire break) | p70 Table 39 |
| Diagnostic priority | Wire break (high) > under/overrange (medium) > user limits (low); min display 100 ms | p84 Table 46; p85 |
| Factory defaults | Sensor Type K; CJC activated, internal; diagnostics globally on (wire break, over/underrange shown); two's complement; notch 50 Hz; user scaling off; factory calibration | pp67/70/71/72 Table 38/39/40 |
| Type K accuracy, internal CJC, nominal mounting | −200…−50 °C ≤ ±2.5 K; −50…+1370 °C ≤ ±1.6 K (after ~20 min warm-up) | p27–28 §3.6.4.1, Table 12 |
| Conversion time | ≤100 ms/channel (notch on), ≤50 ms (notch off); SigmaDelta | p26 Table 11 |

**Configured values are NOT evidenced.** The drawing shows no configuration and no
engineering-tool export was reviewed. All "configured" fields (sensor type,
CJC, diagnostics, scaling, channel enable) are known **only** at
`MODEL_LEVEL` as factory defaults under the brief-stated
`OWNER_CONFIRMED_FACTORY_DEFAULT` design basis. Any instance whose configuration
differs from factory default invalidates the corresponding row of this contract.

---

## 3. Per-node Modbus word map (DERIVED — each step cited)

Derivation chain (750-362 manual v1.1.1, Apr 2022):

1. Analog modules are mapped **first**, **in physical-position order** after the head station (p51 §4; p79 §7.2.1).
2. "Input modules for thermocouples" are complex modules (data width > 1 byte/channel), addressed **from word 0** in physical order (p83 §7.3.2.1 Table 28).
3. For MODBUS the image is built **word-by-word, word-aligned, Intel format** (p80 §7.2.2).
4. An analog input module exposes **only its 16-bit data words** to Modbus/TCP — the coupler **has no access to the 8 control/status bits** (p220 §12.2.3). They are grouped as words, Intel format, analog in front of digital.
5. A 750-498 carries 8 × 16-bit data + optional 8 × 8-bit control/status on the local bus (750-498 manual p30 Table 16), i.e. 8 Modbus-visible words per module.
6. Read via FC3/FC4: Modbus addresses 0–255 = %IW0–%IW255, physical input area (1) (p188 Table 119).

**Result — identical for every cabinet** (5 × 8 words = 40 words at FC3/FC4
addresses 0–39 = %IW0–%IW39):

| Module (physical order) | Channels | Modbus input words (FC3/FC4) |
|---|---|---|
| A102.1 | ch1–8 | 0–7 |
| A102.2 | ch1–8 | 8–15 |
| A102.3 | ch1–8 | 16–23 |
| A102.4 | ch1–8 | 24–31 |
| A102.5 | ch1–8 (ch5–8 spare, still occupy words) | 32–39 |

Channel→Sensor for each word: [tc-drawing-schedule-rev1.csv](tc-drawing-schedule-rev1.csv)
(`ModbusWordOffset` column; basis `DERIVED_MODEL_LEVEL`).

**Explicit evidence gap (recorded, not hidden).** 750-362 manual §12.2.3.5
(p223 Table 201) enumerates 8-channel AI modules 750-451/458/496/497 and **does
not list 750-498** (full-text search: zero hits for "498"). The 8-sequential-words
layout above therefore rests on the class-level rules (steps 1–5: TC input
modules are complex >1-byte modules from word 0; 750-498 prov
...[truncated 3101 chars]