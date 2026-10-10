# Stage 0.4B-2 — Verified Process-Image Evidence Foundation and Mapping UI Readability Refinement (Development Checkpoint)

Status: **DEVELOPMENT AND AUTOMATED VALIDATION PASSED. ALL 230 MAPPING-CONFIG TESTS AND 70 MAPPING-CONFIG-UI TESTS PASSING. BOUNDARY SCAN S1–S9 CLEAN (0 FINDINGS). PR OPEN - NOT MERGED.**
Stop point: this checkpoint. Nothing is merged.

---

## 1. Executive Summary

Stage 0.4B-2 advances the simulation mapping configuration layer by establishing two primary objectives:
1. **Verified Process-Image Evidence Foundation (Primary Objective A):** A profile-driven architecture that rigorously separates Owner facts (from the authoritative workbook `T8_IO_Card_Mapping.xlsx`), primary WAGO evidence records (manuals, datasheets), head-station coupling rules (750-362), module process data profiles, status/control byte rules, process-image packing order, and derived read-only address boundaries.
2. **Mapping UI Readability and Inspection Refinements (Primary Objective B):** An enhanced browser presentation featuring a responsive wide layout, sticky table navigation, a Comfortable / Compact density switch (presentation-only, non-mutating), tag filtering, equipment grouping (Pump, IV1–IV8), compact address presentation with expandable reason disclosures, grouped validation summaries, and a dedicated read-only Process-Image Evidence Foundation review section.

---

## 2. Honesty Lines

- **AI-002 RANGE OWNER-CONFIRMED:** 0–5000 PSI / 4–20 mA.
- **AI-002 DIAGNOSTIC/TREND ONLY:** AI-002 is solely for telemetry and diagnostic trending; it never triggers Pump-ready interlocks or trips.
- **AI-003 PUMP-READY ONLY:** AI-003 is the sole Pump-ready pressure input.
- **AUTHORITATIVE DEFAULT HAS 26 BINDINGS:** Imported directly from `T8_IO_Card_Mapping.xlsx` with no seed override.
- **AUTHORITATIVE DEFAULT REQUIRES NO SEED:** Default import functions completely without user seed overrides.
- **RESERVED PLACEHOLDER INVENTORY HAS 18 ROWS:** Explicitly counted and reported as **17 Input + 1 Output = 18 Total** (including output `DO-031`).
- **FIXED DARK THEME WITH COMFORTABLE / COMPACT DENSITY:** Strictly dark color scheme adhering to WCAG AA contrast (minimum 4.5:1 text, 3:1 chips); density toggle alters styling only without mutating draft configuration or revision counters.
- **AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED:** No publication to .NET Runtime, No IPC, No telemetry streaming to machine controllers.
- **ADDRESSES REMAIN ADDRESS_UNRESOLVED:** Real physical offsets remain `ADDRESS_UNRESOLVED` (`EVIDENCE_PENDING_PRIMARY_VERIFICATION`) until verified WAGO evidence is supplied; no manual address entry or override exists.
- **PROCESS_MODULE_POSITION IS PHYSICAL SLOT ORDINAL, NOT PROCESS_IMAGE_ORDER:** Physical 1-based process slot sequence is strictly separated from fieldbus process-image packing order; treating `PROCESS_MODULE_POSITION` as address order is explicitly refused.
- **NO SYNTHETIC RULE LEAKED TO DEFAULTS:** Synthetic test derivation rules are isolated to explicit synthetic test scenarios (`isSyntheticRule = true`) and never serialized as genuine WAGO maps or loaded by default.
- **NO MODBUS IMPLEMENTED:** No Modbus protocol handler, registers, socket connections, or transports exist.
- **NO TEST_HARDWARE AUTHORIZED:** Hardware testing remains disabled.
- **NO PRODUCTION AUTHORIZED:** Production execution remains disabled.
- **SERVER BINDS ONLY TO 127.0.0.1 (LOOPBACK ONLY):** GET and HEAD requests only; all other HTTP methods return 405 Method Not Allowed; strict CSP and frame-ancestors 'none'.
- **PR OPEN - NOT MERGED.**

---

## 3. Primary Objective A: Process-Image Evidence Foundation

### 3.1 Architectural Separation
The evidence foundation explicitly decouples distinct concerns:
- **Owner Facts:** Workbook metadata imported from `T8_IO_Card_Mapping.xlsx` (card types, physical slots, channels, signal identities, equipment assignments, functional descriptions).
- **Primary WAGO Evidence Records:** Profile metadata capturing document ID, publication date, section, and page references for WAGO hardware modules (manuals and data sheets).
- **Head-Station Coupling Rules:** Coupler-level process image alignment specifications (WAGO 750-362 Modbus TCP/UDP fieldbus coupler).
- **Module Process Data Profiles:** Bit/word widths, channel counts, status byte configurations, control byte configurations, and data formatting.
- **Process-Image Ordering Rules:** Alignment and packing order rules (analogs word-aligned first vs digitals bit-packed; input vs output process images).
- **Derived Address Boundaries:** Fully deterministic derivation engine that strictly outputs read-only address manifests; any manual address property in draft bindings is refused (`MANUAL_ADDRESS_REFUSED`).
- **Unresolved Reason Codes:** Explicit taxonomy of reasons why an address remains unresolved (`EVIDENCE_PENDING_PRIMARY_VERIFICATION`, `STATUS_BYTE_POLICY_UNCONFIRMED`, `COUPLER_ALIGNMENT_UNVERIFIED`, `OUTPUT_WRITE_PROHIBITED_IN_READ_ONLY_STAGE`).

### 3.2 Supported Hardware Modules
The module profiles library (`packages/mapping-config/src/moduleProfiles.mjs`) provides verified profile structures for all 8 modules present in the rack:
1. `750-362`: Modbus TCP/UDP Fieldbus Coupler (Slot 0, Coupler).
2. `750-430`: 8-Channel Digital Input Module 24V DC (Slots 1, 2, 3).
3. `750-530`: 8-Channel Digital Output Module 24V DC (Slots 4, 5, 6, 7).
4. `750-601`: Supply Module 24V DC (Slot 8, System Supply).
5. `750-613`: System Supply Module 24V DC (Slot 9, Internal Data Bus Power Supply).
6. `750-471`: 4-Channel Analog Input Module 4–20 mA Single-Ended (Slots 10, 11, 12).
7. `750-554`: 2-Channel Analog Output Module 4–20 mA (Slot 13).
8. `750-600`: End Module (Slot 14, Passive Bus Termination).

### 3.3 Slot Ordinal vs Process Image Order
`ProcessModulePosition` is strictly the 1-based physical ordinal of process data modules along the DIN rail (Slot 1 through 7, 10 through 13). It does not dictate address offset, byte index, or Modbus register number. Address derivation explicitly blocks using `PROCESS_MODULE_POSITION` as an image ordering rule.

---

## 4. Primary Objective B: Mapping UI Readability Refinement

### 4.1 Layout and Styling
- **Expanded Width:** The container is expanded from 1100px to `max-width: min(1840px, 98vw)` to comfortably display all 11 columns of the IO mapping and reserved inventory without truncation or horizontal crowding.
- **Typography:** Hierarchy utilizes clear proportional monospace fonts for numbers, addresses, and hex values, with readable sans-serif for descriptions and labels.
- **Sticky Elements:** Table headers and section navigation remain visible during scrolling.
- **Dark Palette:** Strict dark theme with background `#0f141c`, card background `#141b26`, and border `#243044`. All text exceeds WCAG AA 4.5:1 contrast; status chips exceed 3:1.

### 4.2 Interactive Review Controls
- **Density Switcher:** A dedicated toggle allows selecting between `Comfortable` (default, padding 10px 14px, line-height 1.5) and `Compact` (padding 6px 10px, line-height 1.3). The toggle modifies DOM class styling only; it does not touch the Draft session, mutate revision counters, or trigger network requests.
- **Tag Mapping Filter Toolbar:** Filter buttons allow isolating views by tag category: All (default), Pump (`AI-002`, `AI-003`), IV Valves (`IV1`–`IV8`), Digitals, Analogs, or Unresolved. Filtering is strictly presentation-only.
- **Equipment Grouping:** A toggle enables grouping rows by physical subsystem (`PUMP`, `IV1` through `IV8`, and `UNASSIGNED`).
- **Compact Address State & Expandable Reasons:** The address summary is shown compactly (`UNRESOLVED`), with an expandable disclosure button to inspect the technical reason code, pending evidence items, and derivation status without editability.
- **Grouped Validation Summary:** Validation issues are aggregated into category cards: Rack Structure, Address Resolution, Activation Boundary, and Hardware Control Boundary.
- **Placeholder Summary:** Clearly distinguishes input vs output reserved rows, displaying: `17 Input + 1 Output = 18 Total`.
- **Process-Image Evidence Review:** A dedicated section displays each rack slot's hardware profile, evidence status, channel counts, bit/word widths, and verification state.

---

## 5. Automated Test Suite (27 Required Tests)

All 27 required tests are implemented across two automated suites:

### Evidence Foundation Tests (1–13)
Implemented in `packages/mapping-config/test/evidenceFoundation.test.mjs`:
1. `Evidence profile distinguishes Owner facts from primary WAGO evidence records`: verified schema separation.
2. `Coupler 750-362 profile requires explicit process image alignment rule`: verified coupler alignment model.
3. `Digital 750-430 and 750-530 define bit-packed process image capabilities`: verified 8-channel 1-bit widths.
4. `Analog 750-471 and 750-554 define word-oriented channel widths`: verified 16-bit word widths.
5. `Supply and end modules (750-601, 750-613, 750-600) consume 0 process-image bytes`: verified zero width.
6. `Status byte exclusion policy is explicitly evaluated in evidence profile`: verified configurable status byte rule.
7. `ProcessModulePosition is not treated as ProcessImageOrder`: verified rejection of slot ordinal as image order.
8. `Default configuration retains ADDRESS_UNRESOLVED for all real modules`: verified null offsets on real import.
9. `Synthetic address derivation is allowed only in tests, never in default config`: verified `isSyntheticRule` guard.
10. `Derived address manifests are strictly read-only and immutable`: verified `Object.freeze` on derived addresses.
11. `Manual address entry in draft bindings is rejected by validation`: verified `MANUAL_ADDRESS_REFUSED`.
12. `Profile status incomplete blocks address resolution`: verified `EVIDENCE_PENDING_PRIMARY_VERIFICATION`.
13. `Evidence profile produces deterministic serialization`: verified JSON repeatability.

### UI Readability and Boundary Tests (14–27)
Implemented in `apps/mapping-config/test/uiReadability.test.mjs`:
14. `Comfortable density is the default`: verified default active class and styling.
15. `Compact density changes presentation only`: verified no draft mutation or revision increment.
16. `Filters do not alter Draft or default revisions`: verified tag filtering leaves revision untouched.
17. `Pump and IV grouping is correct`: verified correct grouping into Pump and IV1–IV8 sections.
18. `Reserved rows are grouped separately`: verified 18 reserved rows kept distinct in reserved section.
19. `Placeholder summary shows 17 Input + 1 Output = 18 Total`: verified exact summary text in DOM.
20. `Compact address state does not hide Error status`: verified address cell warning styling.
21. `Expanded address reasons remain read-only`: verified read-only display without input controls.
22. `Validation warnings are grouped deterministically`: verified category grouping cards.
23. `All element IDs are unique`: verified zero duplicate IDs in `index.html`.
24. `Workbook-generated values are written as text, not unsafe HTML`: verified `textContent` safety.
25. `Server remains loopback-only`: verified `127.0.0.1` binding.
26. `GET/HEAD-only HTTP boundary remains enforced`: verified POST/PUT/DELETE return 405.
27. `No Hardware or Write control is introduced`: verified zero hardware command or write buttons.

---

## 6. Verification Results

- `packages/mapping-config`: 230 passing tests (0 failures).
- `apps/mapping-config`: 70 passing tests (0 failures).
- `tools/boundary-scan/boundary-scan.mjs`: 0 findings (S1–S9 clean).
- `git diff --check`: 0 errors.
- Authoritative workbook `T8_IO_Card_Mapping.xlsx`: untouched (SHA-256 `4E0337E25C8377C01559F264653BAAB25BCFA23F4D3E071FDC8C80896F422E8E`).

---

## 7. Commits and Branch State

Branch: `arena/a3adec3a-waterjet-sentinel-suite` (target: `main`)
Parent commit on `main`: `dbbf34cfe49f4db6c524a675dadb8a24d5ef497f`

Commits:
1. `865b785` `feat(mapping): add process-image evidence profiles`
2. `e96a595` `feat(mapping-ui): improve mapping review readability`
3. `5a3b698` `test(mapping): validate evidence and presentation boundaries`
4. `<pending>` `docs(checkpoint): record Stage 0.4B-2 development checkpoint`
