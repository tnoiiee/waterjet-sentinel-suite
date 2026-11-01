# WSB Upper/Lower Limit Switch Supersession — 2026-11-01

**Owner authority:** ACCELERATED-CLEANING-INTEGRATION-01
**Applies to:** `DI-021` … `DI-036` (FC02 addresses 20 … 35) and nothing else.

---

## 1. What changed and why

The committed 2026-10-22 inventory labelled the WSB limit switches reversed: FC02 `20..27` as
*Lower* and `28..35` as *Upper*. The Owner's corrected workbook supersedes those sixteen
descriptions. No address, tag, module, slot, channel, terminal, function code or output identity
changed — this is a **label-only** correction.

## 2. Superseding artifact

| Field | Value |
|---|---|
| File name | `WAGO_T8_Analog_Digital_Modbus_Mapping 1_Corrected_Upper_Lower.xlsx` |
| Bytes | 23,005 |
| Owner-declared SHA-256 | `d9d6d0d95cbe893f87557ecfe2b0a26b07cf0650b24b9301a32402bdd18ff030` |
| Observed SHA-256 of the delivered bytes | `b16670128f790d1f70ae3dcea2e623f16f74a32d61fee954f84595fe62784a40` |
| Drive file id | `1kjvsnVEUTIcUoJ2y8i8aZ19qYhXbRzkr` |

**Identity note (recorded, not resolved by assumption).** The delivered bytes do not reproduce the
Owner-declared hash. The same retrieval path reproduces the *previous* workbook byte-exactly
(`6b0fc1ca…f9428`, 23,006 B), so the mismatch is specific to the corrected file and is not a
transport artifact. The content was therefore validated directly instead of being accepted on hash:
a cell-level comparison against the prior workbook yields **exactly 16 differing cells, all in the
"Signal / Description" column, all Upper/Lower swaps**, and the resulting addresses (`20..27`
Upper, `28..35` Lower) match the Owner's written instruction exactly. The declared and observed
hashes are both recorded above; the file name is the code-level key.

## 3. Corrected mapping (current Product truth)

| Tags | FC02 | Description | IV role |
|---|---|---|---|
| `DI-021` … `DI-028` | 20 … 27 | WSB Upper Limit Switch #1 … #8 (NO) | IV1..IV8 **OPEN**-position feedback |
| `DI-029` … `DI-036` | 28 … 35 | WSB Lower Limit Switch #1 … #8 (NO) | IV1..IV8 **CLOSED**-position feedback |

## 4. The sixteen corrected descriptions

| Tag | Superseded text | Corrected text |
|---|---|---|
| DI-021 | WSB Lower Limit Switch #1 (NO) | WSB Upper Limit Switch #1 (NO) |
| DI-022 | WSB Lower Limit Switch #2 (NO) | WSB Upper Limit Switch #2 (NO) |
| DI-023 | WSB Lower Limit Switch #3 (NO) | WSB Upper Limit Switch #3 (NO) |
| DI-024 | WSB Lower Limit Switch #4 (NO) | WSB Upper Limit Switch #4 (NO) |
| DI-025 | WSB Lower Limit Switch #5 (NO) | WSB Upper Limit Switch #5 (NO) |
| DI-026 | WSB Lower Limit Switch #6 (NO) | WSB Upper Limit Switch #6 (NO) |
| DI-027 | WSB Lower Limit Switch #7 (NO) | WSB Upper Limit Switch #7 (NO) |
| DI-028 | WSB Lower Limit Switch #8 (NO) | WSB Upper Limit Switch #8 (NO) |
| DI-029 | WSB Upper Limit Switch #1 (NO) | WSB Lower Limit Switch #1 (NO) |
| DI-030 | WSB Upper Limit Switch #2 (NO) | WSB Lower Limit Switch #2 (NO) |
| DI-031 | WSB Upper Limit Switch #3 (NO) | WSB Lower Limit Switch #3 (NO) |
| DI-032 | WSB Upper Limit Switch #4 (NO) | WSB Lower Limit Switch #4 (NO) |
| DI-033 | WSB Upper Limit Switch #5 (NO) | WSB Lower Limit Switch #5 (NO) |
| DI-034 | WSB Upper Limit Switch #6 (NO) | WSB Lower Limit Switch #6 (NO) |
| DI-035 | WSB Upper Limit Switch #7 (NO) | WSB Lower Limit Switch #7 (NO) |
| DI-036 | WSB Upper Limit Switch #8 (NO) | WSB Lower Limit Switch #8 (NO) |

## 5. Proof that no unrelated workbook field changed

Cell-level comparison of the corrected workbook against the superseded workbook across all three
sheets (`Digital Mapping`, `Analog Mapping`, `Contract Summary`):

- differing cells: **16**
- differing sheets: **1** (`Digital Mapping`)
- differing column: **1** (`Signal / Description`, index 7)
- differing rows: 21 … 36 → tags `DI-021` … `DI-036`
- row counts unchanged (73 / 39 / 12)
- `Contract Summary` unchanged, including all read/write function codes and address ranges
- every address, slot, channel, terminal, `From`, `To`, `Ref.`, `Location` and note unchanged

The same result holds for the emitted inventory CSV: 16 cells in one column, nothing else.

## 6. Files corrected in this sweep

| File | Change |
|---|---|
| `tools/process-io-evidence/generate.mjs` | Supersession overlay `SUPERSEDED_DESCRIPTIONS` + per-row `SUPERSESSION_MISMATCH` and `assertSupersessionComplete` fail-closed guards |
| `tools/process-io-evidence/test/generate.test.mjs` | Six focused overlay tests |
| `docs/evidence/process-io-inventory-2026-10-22.csv` | Regenerated: 16 description cells corrected |
| `packages/process-io/src/legacyEvidence.mjs` | 16 descriptions; `ivUpperLimitRows` / `ivLowerLimitRows` swapped; new `ivOpenFeedbackRows` / `ivClosedFeedbackRows`; `ivLimitOrdinalRule`; `READ_BACK_SOURCES` for `DO-023`..`DO-030`; new `ivCommandRows` / `ivCommandOrdinalRule` / `ivCommandSemantics` |
| `packages/process-io/test/bindings.test.mjs` | Read-back expectation `DI-029` → `DI-021` |
| `docs/STAGE_0.4C-0A_CHECKPOINT.md` | Supersession note above the (otherwise preserved) dated table |

## 7. Preserved historical records

`docs/evidence/WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` is kept
**byte-exact** (`28245ad2…c5299`) as the dated 2026-10-22 record. The generator reads it, verifies
that hash, and applies the supersession overlay at parse time — so the historical artifact and the
current corrected inventory coexist without either being falsified, and a regeneration cannot
silently reintroduce the reversal.

`docs/STAGE_0.4C-0A_CHECKPOINT.md` keeps its original dated table; a supersession note above it
prevents any current reader from treating the reversed rows as truth.

## 8. Boundary statement

No Hardware was contacted to produce this record. No physical network access, no WAGO read or
write, no Galil connection, no motion, no Brake output, no Pump command, no IV actuation, no water
flow. **PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED. PRODUCTION: NOT AUTHORIZED.**
