# TC Schedule Validation Summary — Baseline REV1 (Stage 0.4C-0B)

> **Superseded topology note (Owner critical topology correction, 2026-10-12).** This document predates the correction and retains, as a dated historical record, the belief that logical positions I7 and I16 were `NON_SENSOR_GAP` positions (106 Sensors / 212 channels; Sensor sequence skipping I7 and I16; WJ3 / WJ1 "at the I7 / I16 gap anchor"). Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements. 108 × 2 = 216; 240 physical TC channels − 216 = 24 unassigned. The schedule CSV this summary describes (`tc-drawing-schedule-rev1.csv`) has since been corrected: the four I7/I16 rows are `DRAWING_VERIFIED` / `MATCHED` Product assignments, giving 204 drawing-verified, 12 drawing-internal-conflict, 0 topology-conflict, 24 unassigned, 216 Product-compatible measurements across 108 Sensors (0 unresolved). The text below is retained unedited (including its committed truncation marker) and must not be used as a current baseline; the current record is [`STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md`](../STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md).

Status: **DEVELOPMENT CHECKPOINT record, SUBMITTED FOR OWNER REVIEW.** Narrow
validation of [tc-drawing-schedule-rev1.csv](tc-drawing-schedule-rev1.csv) (240
rows) against the Baseline REV1 evidence. Every count below was recomputed from
the artefacts in-session (not copied). It authorises nothing.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0B |
| Evidence baseline | `BASELINE_REV1` |
| Schedule under validation | [tc-drawing-schedule-rev1.csv](tc-drawing-schedule-rev1.csv) |
| Profile under validation | [tc-process-image-profile-rev1.md](tc-process-image-profile-rev1.md) |
| Intake template (read-only reference) | `docs/templates/sensor-to-tc-channel-schedule.template.csv` — 28 columns, 212 data rows, 0 `I7`/`I16` rows, byte-identical, untouched |
| Authoritative workbook (read-only reference) | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, re-verified before and after every read |

---

## 1. Separation counts (recomputed)

| Set | Count | Check |
|---|---|---|
| Total schedule rows | 240 | 6 cabinets × 5 modules × 8 channels ✓ |
| `DRAWING_VERIFIED` | 200 | sheath = conductor, sensor not a gap position |
| `DRAWING_INTERNAL_CONFLICT` | 12 | A102.2 ch6/ch7 on all 6 cabinets; sheath ≠ conductor; schedule follows conductor+X110 (2-of-3) |
| `DRAWING_TOPOLOGY_CONFLICT` | 4 | I7-F, I7-R (CAB5 A102.4 ch7/8), I16-F, I16-R (CAB6 A102.4 ch7/8) — live drawing channels on authoritative `NON_SENSOR_GAP` positions |
| `SPARE_UNMAPPED` | 24 | A102.5 ch5–8 × 6; no sheath/conductor/X110 content (spare zone) |
| Mapped + unmapped | 216 + 24 = **240** ✓ | Required identity holds |
| `TemplateCompatibility = MATCHED` | **212** | 106 sensors × 2 sides |
| `GAP_CONFLICT` | 4 | I7/I16 rows only |
| `NO_COUNTERPART_SPARE` | 24 | Spare rows only |
| Unresolved rows | **0** | Every row has a determined state |

## 2. 106-sensor validation (template cross-check)

- Template: 106 unique `SensorId`s, each with exactly one `FRONT` and one `REAR` row (212 rows).
- Drawing: 108 unique conductor sensors, each with exactly one `F` and one `R` channel (216 mapped rows); every conductor label occurs exactly once; +pole label = −pole label on all 216 rows.
- Set difference drawing−template = **exactly {I7, I16}**; template−drawing = **empty**.
- Therefore all 212 template rows have exactly one drawing counterpart, and the drawing's only extras are the 4 gap-conflict rows.
- **Side-mapping assumption (explicit).** No reviewed source defines `F`/`R`. This validation pairs template `FRONT`↔`F` and `REAR`↔`R` on the structural basis that both sides carry exactly one channel per sensor per side (1:1, zero contradicting evidence). The CSV preserves `F`/`R` verbatim and never writes `FRONT`/`REAR` from the drawing.
- **NAMESPACE result: NOT_VERIFIED.** Template wall/slot/matrix position
...[truncated 3452 chars]