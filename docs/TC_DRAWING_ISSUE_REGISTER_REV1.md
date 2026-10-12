# TC Drawing Schedule Issue Register — Baseline REV1 (Stage 0.4C-0B)

Status: **DEVELOPMENT CHECKPOINT record, SUBMITTED FOR OWNER REVIEW.** This register
records the drawing-evidence intake for the six-node thermocouple topology baseline.
Sections 1–5 record the 2026-10-11 position (evidence absent in-session, derivation
STOPPED with 0 rows) and are preserved unchanged as audit history. Section 7 records
the 2026-10-12 completion: the Baseline REV1 evidence arrived via the
Owner-connected source, was integrity-verified, and the 240-row schedule was derived
from it. It authorises nothing. It contains no mapping beyond the derived schedule
it points at.

> **Section 8 (2026-10-12, Owner critical topology correction) supersedes the I7/I16 findings
> of §7:** I7 and I16 are normal Product Sensors; ISS-REV1-02 is **closed as superseded** and the
> four rows are used Product assignments. The authoritative current baseline is 108 Sensors and
> 216 assigned Product TC measurements (240 physical, 24 unassigned). Sections 1–7 are preserved
> as audit history; every "106 / 212 / 28 / 4 topology-conflict / gap" figure in them is
> historical. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.

| Item | Value |
|---|---|
| Project / Stage | WaterJet Sentinel Suite (WJSS) — Stage 0.4C-0B |
| Drawing status | `DRAWING_PENDING_REVISION` |
| Evidence baseline | `BASELINE_REV1` |
| Session branch (platform-assigned, fixed) | `arena/c4b63a26-waterjet-sentinel-suite` |
| Approved remote-main base | `main` = `3e1a7fd9d28db65221c123960a0c85b62d93949a` (PR #17 merge commit) |
| Review date | 2026-10-11 |
| Companion record | [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) |

**Binding rule.** No schedule row has been derived, interpolated or invented. Every
drawing-derived field in this baseline reads `NOT_AVAILABLE_IN_SESSION` or
`NOT_PROVIDED`, and no SHA-256 has been recorded for any evidence file not physically
present in this sandbox.

---

## 1. Expected drawing evidence (Baseline rev1 intake)

The Stage 0.4C-0B authorising brief states that six-cabinet TC drawings are
Owner-provided evidence (`DRAWING_PENDING_REVISION`, baseline `REV1`) and instructs
the agent to read the actual drawing files and derive the Sensor-to-Channel schedule
from them — or, if a drawing is unavailable in the sandbox, to STOP and report the
exact missing file.

| # | Expected evidence | Identifier given in the authorising brief | Search performed | Result |
|---|---|---|---|---|
| D-01 | Six-cabinet TC drawings (one set covering TC-NODE-01 … TC-NODE-06) | **No filename, title, sheet list or revision stated** in the brief; no drawing title on record in any predecessor register (TCEV-05 reads "Wiring drawings — Not supplied") | Workspace-wide `find` for `*.pdf`, `*.dwg`, `*.dxf`, `*drawing*`, `*.png`, `*.jpg`, `*.jpeg`, `*.tif`, `*.tiff`, `*.bmp`; tracked-file review via `git ls-files` | **Zero hits. `NOT_AVAILABLE_IN_SESSION`.** No drawing file of any name or format is present in the sandbox |
| M-01 | WAGO 750-498 primary manufacturer manual | Exact filename `m07500498_00000000_0en.pdf` | Same workspace-wide search, plus filename search for `*0750*`, `*wago*`, `*m0750*` | **Absent. `NOT_AVAILABLE_IN_SESSION`.** No manual file is present |
| M-02 | WAGO 750-362 primary manufacturer manual | **No filename stated** ("Owner-provided primary manufacturer manual") | Same search | **Absent. `NOT_AVAILABLE_IN_SESSION`.** No manual file is present |
| M-03 | WAGO 750-600 primary manufacturer manual | **No filename stated** ("Owner-provided primary manufacturer manual") | Same search | **Absent. `NOT_AVAILABLE_IN_SESSION`.** No manual file is present |

**Vocabulary.** `NOT_AVAILABLE_IN_SESSION` means: the authorising brief asserts the
Owner holds this evidence, but no file was visible in this sandbox, so no filename,
hash, size, title, revision, scope or review status could be recorded for it. For
gate purposes it is equivalent to `NOT_PROVIDED`: no verifiable content exists in
the sandbox. No hash from memory or from a prior session was reused.

---

## 2. Exact missing-file report (STOP record, brief §4)

Per the brief §4 STOP rule — *"If a Drawing is unavailable in the sandbox: STOP,
report the exact missing file"* — schedule derivation was **stopped before any row
was written**:

1. **Six-cabinet TC drawings** — expected count 6 cabinets (TC-NODE-01 … TC-NODE-06);
   filenames, sheet references and revision **unspecified** in the authorising brief
   and in every predecessor record. **Zero drawing files present.**
2. **`m07500498_00000000_0en.pdf`** (750-498 manual, exact filename from the brief).
   **Not present.**
3. **750-362 manual** (filename unspecified in the brief). **Not present.**
4. **750-600 manual** (filename unspecified in the brief). **Not present.**

No value was left "unresolved pending clarification" because there was no drawing
content at all to extract from — not a legibility gap but a complete absence. No
Cabinet/Module/Channel entry exists to attach a page reference to.

---

## 3. Consequences for Baseline rev1

| Baseline item | Status at this head |
|---|---|
| Drawing-derived schedule rows | **0 rows.** None derived, none interpolated, none carried over from the template or from any synthetic mapping |
| Drawing-derived CSV under `docs/evidence/` | **Not created.** Creating it would have required inventing every acquisition field |
| `DRAWING_VERIFIED_REV1` rows | 0 |
| `DRAWING_AMBIGUOUS` / `DRAWING_MISSING_IDENTITY` / `DRAWING_DUPLICATE_MAPPING` / `DRAWING_UNREADABLE` rows | 0 — there is no drawing content to classify |
| `RESERVED_UNASSIGNED` rows | 0 — the brief forbids reinterpreting the 28 remaining Channels as commissioned spares until the drawing-derived schedule proves which Channels are actually unused, and no schedule exists |
| Drawing-used Channels | 0 of 240 physical Channels |
| Drawing-unassigned Channels | **Cannot be reported.** "Unassigned" is meaningful only against a derived schedule; with zero rows, every physical Channel is unscheduled, not unassigned |
| Expected required Channels | 212 (Owner-stated requirement: 106 Sensors × 2 measurement Channels; unchanged) |
| Missing Sensor measurements | **Cannot be computed.** All 212 expected measurement identities are unscheduled, which is an absence of evidence, not a per-identity finding |
| Duplicated Sensor measurements / duplicated physical Channels | None found — vacuously, because zero mappings exist |
| `REQ-TC-004` (schedule), `REQ-TC-005` (terminals), `REQ-TC-006` (wiring) | Remain `NOT_PROVIDED` |
| 750-498 factory-default evidence | **Not extracted.** The Owner-confirmed configuration basis `OWNER_CONFIRMED_FACTORY_DEFAULT` is recorded as a brief-stated decision; no manual section, page, default value or encoding was read or cited |
| 750-362 cyclic process-image profile | **Not established.** No module-ordering rule, grouping rule, alignment, width, offset basis or status-byte determination was derived |
| Address / offset manifest | **Not established.** Every Domain B address remains without a documented offset; `ADDRESS_UNRESOLVED` is retained **because the manuals are absent**, not merely because hardware is untested |
| Factory-default TC decoding contract | **Not established as documented fact.** The predecessor wire-break decision-tree design position (Stage 0.4C-0A §6) is carried forward unchanged as a design proposal |

Capacity arithmetic from Owner-stated (non-drawing) facts is recorded only in the
checkpoint: 240 physical Channels (6 nodes × 5 modules × 8 Channels), 212 required
(106 Sensors × 2), 28 remaining — all brief-stated, none drawing-verified.

---

## 4. Baseline revision rule (binding)

`BASELINE_REV1` is the approved evidence baseline for the drawing-derived schedule,
and it currently contains **zero derived rows**. The rule from the authorising brief
is recorded here so the next session cannot silently overwrite it:

1. A future revised drawing creates a **new** mapping/evidence revision (`REV2`, …)
   with its own issue-register revision record.
2. `REV1` is never edited in place to absorb new drawing content; it is superseded
   only through an explicit superseding revision record.
3. Any future schedule row must cite its drawing sheet/page reference; rows without
   drawing evidence keep an explicit unresolved state and are never auto-filled.
4. The 28 remaining Channels stay **unscheduled** (not commissioned spares) until a
   drawing-derived schedule proves which Channels are actually unused.

---

## 5. Next actions (Owner)

| # | Action | Needed for |
|---|---|---|
| 1 | Provide the six-cabinet TC drawings to a future session (with filenames, sheet references and revision), or confirm the exact expected filenames so absence can be reported against them | Schedule derivation (`REQ-TC-004`, `REQ-TC-005`, `REQ-TC-006`) |
| 2 | Provide `m07500498_00000000_0en.pdf` (or confirm its exact revision) to a future session | 750-498 factory-default evidence, decoding contract (`REQ-TC-007`, `REQ-TC-011`) |
| 3 | Provide the 750-362 and 750-600 primary manuals (with exact filenames/revisions) to a future session | 750-362 cyclic process-image profile, offset manifest (`REQ-TC-009`, `REQ-TC-010`) |
| 4 | No Owner transcription of the schedule is requested — the brief forbids asking the Owner to manually recreate a schedule already present in the drawings | — |

No live connection, loop test, `TEST_HARDWARE` or production activity is requested
or authorised by this register.

---

## 6. Related documents

- [STAGE_0.4C-0B_CHECKPOINT.md](STAGE_0.4C-0B_CHECKPOINT.md) — this Stage's checkpoint record
- [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) — evidence ledger (TCEV-12 … TCEV-16 record the absent-evidence intake; TCEV-17 … TCEV-20 record the completed review — see §7)
- [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) — predecessor checkpoint (template policy, decision tree)
- [templates/sensor-to-tc-channel-schedule.template.csv](templates/sensor-to-tc-channel-schedule.template.csv) — the 212-row intake template (unchanged; still no mapping)
- [evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv) — the derived 240-row REV1 schedule (see §7)
- [evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md) — process-image profile + decoding contract (see §7)
- [evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md) — narrow validation summary (see §7)

---

## 7. Evidence-review completion (2026-10-12 — supersedes the §2 STOP for REV1)

The Baseline REV1 evidence arrived after the §2 STOP was recorded, was
integrity-verified in-session, and was fully reviewed. This section records the
completion; §§1–5 above are preserved unchanged as the absence-position audit
trail. No new drawing revision arrived, so no REV2 was opened: this is the same
`BASELINE_REV1`, now present and reviewed. The §4 revision rule still binds any
*future* revised drawing.

### 7.1 Evidence received and integrity

Delivery: `OWNER_GOOGLE_DRIVE_CONNECTOR` (Owner-connected source; no link, ID or
token is recorded). Archive `WJSS_TC_EVIDENCE_BASELINE_REV1.zip`, exactly
17345849 bytes, SHA-256 `7B886950D7F5F665416241E4CB502CD1A5787E3581788C799077C062786AA23E`,
ZIP-signed, central directory valid, 4 listable members, extracted with
traversal/symlink/encryption guards to an ephemeral directory (removed after
review; the archive itself is **not** committed to the repository):

| Member | Size (bytes) | SHA-256 | Content |
|---|---|---|---|
| `T8_SDS_Cabinet_-Draft-7E.pdf` | 7539526 | `08db6f2a…1d6b98` (full hash in the source register TCEV-17) | Combined six-cabinet drawing, 32 pages, REV 1A, 16 MAR 2026, DXN Engineering and Supply Co., Ltd., MAE MOH THERMAL POWER PLANT UNIT 8 |
| `m07500498_00000000_0en.pdf` | 5041172 | `ed66534d…1fe83` (TCEV-18) | WAGO 750-498 manual v1.0.0, 96 pages, Nov 2019 |
| `m07500362-xxxxxxxx-0en.pdf` | 4224370 | `85e72f83…27bb` (TCEV-19) | WAGO 750-362 manual v1.1.1, 296 pages, Apr 2022 |
| `m07500600-xxxxxxxx-0en.pdf` | 1751049 | `c390260c…04b46` (TCEV-20) | WAGO 750-600 manual v1.3.0, 38 pages, 2015 |

### 7.2 Drawing identity and topology (visually verified on-render)

- `WIRING DIAGRAM OF SLAG DEPOSITION SENSOR CABINET`, REVISION 1A, document
  number blank — hence `DRAWING_PENDING_REVISION` is retained: the review
  approves nothing.
- Six cabinets `+SDSCAB1` … `+SDSCAB6` (elevations EL.40.000 / EL.40.000 /
  EL.36.500 / EL.36.500 / EL.26.670 / EL.26.670). Per cabinet: `-A101` = WAGO
  750-362, `-A102.1` … `-A102.5` = WAGO 750-498, `-A110` = WAGO 750-600,
  `-X110` = WDU 2.5/TC TYP K, `-X111` = UT 2.5-PE (p32 rows 7–11; p14; p19).
- A101 takes LAN from `-FBOCAB` (CAT6); field cable `T/C type K PVC Mylar`;
  all channels Type K. No topology conflict with the Stage baseline.

### 7.3 Schedule derivation (240 rows)

[evidence/tc-drawing-schedule-rev1.csv](evidence/tc-drawing-schedule-rev1.csv):
216 mapped + 24 spare (`SPARE_UNMAPPED`: A102.5 ch5–8 × 6) = 240. Per-row states:
200 `DRAWING_VERIFIED`, 12 `DRAWING_INTERNAL_CONFLICT`, 4
`DRAWING_TOPOLOGY_CONFLICT`, 0 unresolved. Template compatibility: 212
`MATCHED` (106/106 sensors), 4 `GAP_CONFLICT`, 24 `NO_COUNTERPART_SPARE`.
Visual verification: all 240 table cells read on-render (pp20/22/24/26/28/30);
all 6 X110 diagram SDS sequences follow the table conductor order
(pp21/23/25/27/29/31); all 6 A102.2 conflict zones, the I7/I16 cells and the
p6/p7 overview labels confirmed on 300-DPI crops. Full record:
[evidence/tc-schedule-validation-rev1.md](evidence/tc-schedule-validation-rev1.md).

### 7.4 Issue inventory (all recorded per-row in the CSV; nothing discarded)

| Issue | Rows / pages | Finding | Schedule treatment |
|---|---|---|---|
| ISS-REV1-01 — sheath/conductor mismatch | 12 rows: every cabinet A102.2 ch6 (sheath X-F, conductor X-R) + ch7 (sheath Y-R, conductor Y-F); tables pp20/22/24/26/28/30 | `DRAWING_INTERNAL_CONFLICT`: the sheath column contradicts the conductor column; the X110 diagram agrees with the conductor on all 6 cabinets (visually verified) | Follows conductor+X110 (2-of-3 sources); both claims preserved per-row |
| ISS-REV1-02 — live channels on gap positions (**CLOSED AS SUPERSEDED, see §8**) | 4 rows: CAB5 A102.4 ch7/8 = I7-F/R (X110.31/32), CAB6 A102.4 ch7/8 = I16-F/R (X110.31/32); table+diagram agree | Historical finding: `DRAWING_TOPOLOGY_CONFLICT`: drawing assigns live channels to the then-authoritative `NON_SENSOR_GAP` positions I7/I16. **Superseded:** I7 and I16 are Product Sensors; the drawing was right | Historical treatment: rows kept with conflict state, excluded from the compatible set. **Current (§8):** the four rows are `DRAWING_VERIFIED` / `MATCHED` Product assignments (I7 Front/Rear, I16 Front/Rear) |
| ISS-REV1-03 — swapped overview sensor groups | Overview p6 (+SDSCAB1 page shows CAB2's groups G+110–118/G+210–218) and p7 (+SDSCAB2 page shows CAB1's groups G+101–109/G+201–209); pp8–11 match their cabinets | `DRAWING_INTERNAL_CONFLICT` (overview vs wiring tables+X110, both cabinets) | Overviews are not a schedule source; wiring tables+X110 govern; Owner revision requested (§5 item 5) |
| SPARE-REV1 | 24 rows: A102.5 ch5–8 × 6 | `SPARE_UNMAPPED`: spare zone, no field wiring; words 36–39 still occupied | Carried as spares, not commissioned; the 28-remaining arithmetic is unchanged (24 spares + 4 gap-conflict rows) |

### 7.5 Manual extraction (drawn only from the reviewed manuals)

- 750-498: Type K = ID3, −200…+1370 °C, 0.1 °C/digit two's complement;
  wire break `0x7FFF`/status `0x60`; factory defaults Type K + internal CJC +
  diagnostics on (§2 of the profile). Full section/page table in
  [evidence/tc-process-image-profile-rev1.md](evidence/tc-process-image-profile-rev1.md) §2.
- 750-362: analog-first physical-order mapping; Modbus sees 16-bit words only
  (Intel format), **no status bytes**; 40 words per node at FC3/FC4 addresses
  0–39; 750-498-not-enumerated gap explicitly recorded (profile §3–§4).
- 750-600: passive bus termination, no process data (profile §1).
- Configured per-instance values remain unevidenced (§7.6).

### 7.6 What review did NOT establish (still blocked)

- Configured sensor type / CJC / diagnostics / scaling / channel-enable per
  instance (no engineering export) — `REQ-TC-008`, `REQ-TC-009`, `REQ-TC-011`
  stay `NOT_PROVIDED`.
- Node transport identities (no IP plan in evidence) — `REQ-TC-010` stays
  `NOT_PROVIDED` (read representation at `MODEL_LEVEL` only).
- Observed wire-break/normal/recovery behaviour — `REQ-TC-012` … `REQ-TC-015`
  stay `NOT_PROVIDED`.
- Wall/slot/matrix NAMESPACE cross-check — `NOT_VERIFIED` (no drawing counterpart).
- `+SDSCABn` ↔ `TC-NODE-0n` equated by ordinal assumption only
  (`ASSUMED_ORDINAL_CORRESPONDENCE`).
- No loop test, no connection, no hardware observation of any kind.

### 7.7 Owner next actions (extends §5)

| # | Action | Needed for |
|---|---|---|
| 5 | Revise the drawing (REV 1B or later): correct the A102.2 ch6/ch7 sheath labels on all six cabinets (or confirm the conductor+X110 reading) and un-swap the p6/p7 overview sensor groups. (The former request to "rule on the I7/I16 live channels" is withdrawn: the Owner critical topology correction confirms them as Product Sensors, §8.) | Clearing ISS-REV1-01/03; a revised drawing opens REV2 per §4 |
| 6 | Supply per-instance configuration evidence (engineering-tool export or complete screenshots) if any instance differs from factory default | Clearing the §7.6 configuration gaps |
| 7 | Supply the node transport plan (cabinet→node→address assignment) | Clearing `REQ-TC-010` and the ordinal-correspondence assumption |

---

## 8. Owner critical topology correction (2026-10-12) — I7 and I16 are Product Sensors

| Item | Value |
|---|---|
| Authority | Owner critical topology correction (final; the question whether I7/I16 are Sensors is not reopened) |
| Revoked baseline | 106 Sensors / 212 channels / I7, I16 `NON_SENSOR_GAP` / I7, I16 drawing rows as `DRAWING_TOPOLOGY_CONFLICT` |
| Authoritative baseline | **108 Product Sensors** (sequence G+201–G+218, G+101–G+118, G1–G18, H1–H18, I1–I18, J1–J18; I7 and I16 once each in ordinal position); **216 assigned Product TC channels** (108 × 2); **240 physical** (6 × 40); **24 unassigned** (240 − 216) |
| Schedule effect (`evidence/tc-drawing-schedule-rev1.csv`) | 4 rows re-stated from the committed drawing evidence, no channel invented: I7-F `TC-NODE-05 / A102.4 / ch7` (-X110.31/-X111.31), I7-R `TC-NODE-05 / A102.4 / ch8` (-X110.32/-X111.32), I16-F `TC-NODE-06 / A102.4 / ch7`, I16-R `TC-NODE-06 / A102.4 / ch8`; all 750-498, `DRAWING_VERIFIED` / `MATCHED` |
| Per-row states now | 204 `DRAWING_VERIFIED`, 12 `DRAWING_INTERNAL_CONFLICT` (ISS-REV1-01, unchanged and unresolved), 0 `DRAWING_TOPOLOGY_CONFLICT`, 24 `SPARE_UNMAPPED`; template compatibility 216 `MATCHED` (108/108 Sensors), 0 `GAP_CONFLICT`, 24 `NO_COUNTERPART_SPARE` |
| Template (`templates/sensor-to-tc-channel-schedule.template.csv`) | 216 rows (I7:F, I7:R, I16:F, I16:R added in deterministic order) |
| ISS-REV1-02 | **Closed as superseded.** ISS-REV1-01 and ISS-REV1-03 are unaffected and remain open |
| Not changed | Six Node identities and statuses, cabinet topology (1 × 750-362 + 5 × 750-498 + 1 × 750-600), factory-default basis, `PROTOCOL_UNRESOLVED` / `PORT_UNRESOLVED`, `DERIVATION_BLOCKED`, `WIRE_BREAK_DECODING_BLOCKED`, field verification pending |
| Validation | Central invariant check `packages/commissioning/src/topologyInvariants.mjs` (108 / 108 / 108 / 108 / 216 / 240 / 24; I7 = 1, I16 = 1; I7F/I7R/I16F/I16R = 1 each; `NON_SENSOR_GAP` = 0) enforced in the commissioning package and in the Ops Preview evidence load; see `STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md` |
