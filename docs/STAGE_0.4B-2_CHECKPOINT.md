# Stage 0.4B-2 — Verified Process-Image Evidence Foundation and Mapping UI Readability Refinement (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT. SOURCE AUTHORED AND AUTOMATED CHECKS PASSED IN ARENA. NOT VALIDATED IN A BROWSER. NOT VALIDATED OWNER-LOCALLY. PR OPEN - NOT MERGED.**
Stop point: this checkpoint. Nothing is merged.

| Item | Value |
|---|---|
| Session | New session (the merged PR #12 branch `arena/1b92c50a-waterjet-sentinel-suite` is not used) |
| Branch | `arena/72d57c31-waterjet-sentinel-suite` |
| Base | `main` `dbbf34cfe49f4db6c524a675dadb8a24d5ef497f` (PR #12 merge; remote `main` was equal to this SHA at the start) |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`. Unchanged. |
| Commits | `62a4443` evidence profiles; `fe155b4` UI readability; `90401e4` tests (with two source fixes); this docs commit |

## Statements

NEW SESSION USED. APPROVED REMOTE-MAIN BASE VERIFIED. AUTHORITATIVE WORKBOOK UNCHANGED. WAGO PROCESS-IMAGE EVIDENCE INCOMPLETE UNLESS PRIMARY EVIDENCE IS PROVIDED.
ADDRESSES REMAIN ADDRESS_UNRESOLVED UNTIL VERIFIED. AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED.
NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. PR OPEN - NOT MERGED.

## Objective A — evidence foundation

**Evidence model** (`processImageEvidence.mjs`, `evidenceReport.mjs`). An evidence set keeps these kinds of fact apart: Owner workbook facts, primary-source evidence,
head-station rules (`processImageGrouping`, `moduleMappingOrder`, `wordWidthBits`, `maxProcessImageWords`), per-module process-data profiles
(`processWidthBits`, `channelDataBits`, `fillerBits`, `diagnosticBytes`), the status-byte setting, byte order and word order (module level), derived order,
derived read-only address and offset, and the unresolved state. Each observation is `{value, state, sourceId, note}`.

- **States:** `NOT_PROVIDED`, `PROVIDED_UNVERIFIED`, `VERIFIED_PRIMARY_SOURCE`, `VERIFIED_IO_CHECK_EXPORT`, `CONFLICTING_EVIDENCE`, `INCOMPLETE_PROFILE`.
- **New reasons:** `PROCESS_IMAGE_ORDER_NOT_VERIFIED`, `BYTE_ORDER_NOT_VERIFIED`, `WORD_ORDER_NOT_VERIFIED`, `CONFLICTING_PROCESS_IMAGE_EVIDENCE` (added to the existing reasons).
- **Verified requires a source:** a matching source type plus a SHA-256 or a revision. Otherwise the observation is demoted and reported (`EVIDENCE_SOURCE_MISSING`,
  `EVIDENCE_SOURCE_UNIDENTIFIED`, `EVIDENCE_SOURCE_TYPE_MISMATCH`, `EVIDENCE_SOURCE_SHA256_INVALID`, `EVIDENCE_VALUE_INVALID`). A manual address in an evidence set is refused (`MANUAL_ADDRESS_REFUSED`).
- **Only one layout is supported:** analog modules first, then digital bits, in ascending RackSlot order. Analog modules are word-aligned. Any other declared layout stays unresolved.
- **Missing evidence blocks only what it covers:** missing head-station layout blocks that direction area; missing byte or word order blocks the module; a missing status-byte setting adds
  `MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED` to analog entries only. Differing verified observations, or a declared `CONFLICTING_EVIDENCE`, give `CONFLICTING_PROCESS_IMAGE_EVIDENCE`.
  The total is checked against `maxProcessImageWords`.
- **Wired into derivation and revisions:** `deriveAddresses` takes the evidence set and returns `{ruleId, verified, synthetic, entries}`. Entries now carry `bitWidth`. The revision manifest
  and fingerprint include the evidence, so a change of verified evidence changes the fingerprint. `activationReady` also requires `addresses.verified`.
- **Authoritative state:** the authoritative evidence set contains the Owner workbook as an `OWNER_WORKBOOK_FACT` source and nothing else. Head station and all 23 modules are `NOT_PROVIDED` / `INCOMPLETE`;
  manufacturer is null. Every enabled binding stays `ADDRESS_UNRESOLVED` with six reasons (seven on 750-471 analog channels).
- **Target models:** 750-362, 750-430, 750-530, 750-601, 750-613, 750-471, 750-554, 750-600. All profiles are INCOMPLETE. No generic WAGO rule exists. No value was invented.
- **SYNTHETIC TEST RULE:** exists only in `packages/mapping-config/test/helpers/syntheticEvidence.mjs`. It is never a default, never authoritative, never serialized as the rack profile, and never served
  (test `U24`/`U25`). A synthetic set is never verified and never activation-ready.
- **ProcessModulePosition is not ProcessImageOrder, offset or address.** The report shows them in separate columns; in the synthetic test, DI-MODULE-01 has position 1 but derived order 4.

## Objective B — Mapping UI readability

Semantics are unchanged. Changes (all presentation): wider layout, a 1920x1080 target with no page-level horizontal scroll, larger type, sticky navigator and table headers; read-only filters
(All, Runtime-bound, Read-only, Owner pending, Analog, Digital, Pump, IV, Unresolved); grouped view (Pump, IV1..IV8, Reserved / Owner pending) or flat view; compact address cell
`ADDRESS UNRESOLVED · N reasons` with the reasons in a lazily-filled read-only `<details>`; grouped validation counts first (Errors, Module profiles incomplete, Engineering ranges pending, Owner identities pending,
Unresolved addresses, Other warnings), with the Errors group open by default; placeholder summary `17 Input + 1 Output = 18 Total`; Comfortable (default) / Compact density; a read-only Evidence review section.
Filters, grouping and density are presentation state: they never touch the Draft, history, revisions or Undo state (tests `U10`, `U11`, `U20`).
Allowed controls are reorder, Undo, Redo, Reset, filters, grouping, density and expand/collapse. There is no Activate, Connect, Poll, Read, Write, Start, Stop, Acknowledge, hardware reset, address override,
MODBUS setting or file upload.

## Defects found by the new tests (fixed in `90401e4`)

1. **`bitWidth`.** The overlap check multiplied `wordCount` by the word width. With a 32-bit word and 16-bit channels, adjacent analog channels were reported as overlapping and derivation raised an internal error
   (found by E10). Entries now carry an explicit `bitWidth`.
2. **Owner-identities count.** The validation group counted the 36 import records (two per placeholder row). It now lists one item per reserved row (18).

## Validation (Arena, no Owner-local environment unless stated)

| Check | Result |
|---|---|
| Package `npm test`, `packages/mapping-config` | 241 tests; **234 pass, 0 fail, 7 skipped** (the Owner-local tests need `MAPPING_EXCEL_DEFAULT_PATH`) |
| Package with an out-of-repo copy of the workbook as `MAPPING_EXCEL_DEFAULT_PATH` | **241 pass, 0 fail, 0 skipped** |
| UI `npm test`, `apps/mapping-config` | **73 pass, 0 fail** |
| `npm run check` (both packages) | OK |
| Boundary scan S1–S9 | 0 findings |
| Baseline before edits | package 217 (210 pass, 7 skipped); UI 56 pass; scan clean |

Tests authored: evidence E1–E13 plus source-handling tests (`processImageEvidence.test.mjs`); presentation U1–U8 (`presentation.test.mjs`); UI stub-DOM and server tests U9–U25
(`review.render`, `review.hostile.render`, `review.server`). No stress or soak tests.

## NOT VERIFIED

- **Browser rendering.** No browser exists in the Arena sandbox. Layout, the 1920x1080 fit, absence of page-level horizontal scroll, sticky headers, density appearance and colour contrast on a real screen are NOT VERIFIED. The stub-DOM tests prove structure and behaviour only.
- **Acceptance of a real primary document.** No primary document (coupler or module manual, or an I/O-check export) was provided. The path "verified evidence derives a real address" is tested only with the SYNTHETIC TEST RULE.
  Source-validation paths are tested as demotions. Real-evidence acceptance is NOT TESTED.
- **.NET.** No .NET code was touched, so no new .NET validation is claimed. The Owner-reported predecessor result (Release build PASS, 414/414 at `8847cfc`) is regression evidence only.
- **Owner-local validation** of this head has not been run.

## Known limitations

- **Boundary scanner S3 forbids vendor and transport words in Product source.** The evidence state is therefore named `VERIFIED_IO_CHECK_EXPORT`, source types are generic, and the manufacturer field is null in the authoritative set.
  UI and test text uses "fieldbus address". The scanner was not changed; the Owner may decide whether the rule needs an exception for evidence labels.
- `moduleProfiles.mjs` still carries a stage comment from Stage 0.4B-1 (comment only).
- Byte order and word order are module-level cells only.

## Owner-local commands (PowerShell, from the repository root)

```powershell
git fetch origin arena/72d57c31-waterjet-sentinel-suite
git rev-parse HEAD            # must equal the head recorded in the PR
Get-FileHash .\T8_IO_Card_Mapping.xlsx -Algorithm SHA256   # 4E0337E25C8377C01559F264653BAAB25BCFA23F4D3E071FDC8C80896F422E8E
$env:MAPPING_EXCEL_DEFAULT_PATH = "C:\wjss-local\T8_IO_Card_Mapping.xlsx"   # a byte-identical copy OUTSIDE the repository
Remove-Item Env:MAPPING_BINDING_SEED_PATH -ErrorAction SilentlyContinue
Push-Location packages\mapping-config; npm run check; npm test; Pop-Location     # expect 241 pass, 0 skipped
Push-Location apps\mapping-config; npm run check; npm test; Pop-Location          # expect 73 pass
node tools\boundary-scan\boundary-scan.mjs                                         # expect 0 findings
Push-Location apps\mapping-config; node server.mjs; Pop-Location                   # http://127.0.0.1:5186 (loopback only)
git status --short            # must be empty
```

Browser review checklist (Owner, at 1920x1080): no page-level horizontal scroll; headers and navigator stay visible; Comfortable and Compact both readable; the 9 filters and the grouped/flat toggle work;
group headers Pump, IV1..IV8, Reserved / Owner pending; address cells show `ADDRESS UNRESOLVED · N reasons` and expand to text; Errors group is open when non-zero; placeholder line reads 17 + 1 = 18; Evidence review lists every module
as NOT_PROVIDED / INCOMPLETE; no control other than reorder, Undo, Redo, Reset, filters, view, density, expand.
