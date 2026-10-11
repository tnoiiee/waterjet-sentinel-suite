# TC Drawing Schedule Issue Register — Baseline REV1 (Stage 0.4C-0B)

Status: **DEVELOPMENT CHECKPOINT record, SUBMITTED FOR OWNER REVIEW.** This register
records the drawing-evidence intake for the six-node thermocouple topology baseline,
the exact missing files, and why no Sensor-to-Channel schedule was derived in
Stage 0.4C-0B. It authorises nothing. It contains no mapping.

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
- [TC_EVIDENCE_SOURCE_REGISTER.md](TC_EVIDENCE_SOURCE_REGISTER.md) — evidence ledger (TCEV-12 … TCEV-16 record this intake)
- [STAGE_0.4C-0A_CHECKPOINT.md](STAGE_0.4C-0A_CHECKPOINT.md) — predecessor checkpoint (template policy, decision tree)
- [templates/sensor-to-tc-channel-schedule.template.csv](templates/sensor-to-tc-channel-schedule.template.csv) — the 212-row intake template (unchanged; still no mapping)
