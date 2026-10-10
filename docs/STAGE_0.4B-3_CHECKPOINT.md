# Stage 0.4B-3 — Primary Manufacturer Evidence Ingestion and Read-Only Process-Image Preview (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT. Arena validation PASSED for the JavaScript scope (Mapping package 268 tests / 261 pass / 0 fail / 7 skipped; Mapping UI 82 / 82 / 0 / 0; boundary scan S1–S9 clean). Owner-local validation PENDING. Owner browser review PENDING. PR OPEN - NOT MERGED.**
Stop point: this checkpoint. Nothing is merged.

**Honesty lines:** 750-601 HAS NO PROCESS I/O ADDRESS. 750-613 HAS NO PROCESS I/O ADDRESS. 750-600 HAS NO PROCESS I/O ADDRESS. POWER SUPPLY AND END MODULE ADDRESSES ARE NOT APPLICABLE. PRIMARY MANUALS ARE AVAILABLE FOR ALL EIGHT MODELS (OWNER-PROVIDED OUTSIDE THE REPOSITORY; NOT HASHED HERE). ACTUAL 750-471 SETTINGS VERIFIED ONLY FOR THE SHOWN INSTANCE/CHANNEL. ACTUAL PROCESS DATA MAPPING NOT YET VERIFIED. ACTUAL FIELD-NETWORK MAPPING NOT YET VERIFIED. CANDIDATE ADDRESSES ARE NOT AUTHORITATIVE. AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. NO DEVICE WRITE OR CONTROL. PR OPEN - NOT MERGED.

| Item | Value |
|---|---|
| Session | New session after merged PR #14. The merged branch `arena/72d57c31-waterjet-sentinel-suite` was not used, continued or pushed. |
| Branch | `arena/00483ae4-waterjet-sentinel-suite` (platform-assigned) |
| Approved remote-main base | `main` = `3a73dfce3d2c7c1dc4d3b0274ea10f97406088f6` (PR #14 merge commit). Verified by `git ls-remote origin refs/heads/main` before any edit; local `HEAD` and the session branch were already at that commit with a clean index and worktree. |
| PR #14 merge parents | `dbbf34cfe49f4db6c524a675dadb8a24d5ef497f` and `a811f6ffb10cb66d3a7b17721aaf1ac26c44ff71` — both confirmed as parents of `3a73dfce` (`git cat-file -p`) and both proven ancestors (`git merge-base --is-ancestor`, after a read-only `--deepen` fetch). |
| Stage 0.4B-2 validated code head | `fe38322d55acb7c565a9984b0eceb82c74b33bb5` — proven an ancestor of `3a73dfce`. |
| Authoritative workbook | `T8_IO_Card_Mapping.xlsx`, SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`. **Unchanged.** |
| Conflicting open PR | None (`gh pr list --state open` returned an empty list at the start). |
| Scope touched | `packages/mapping-config/**`, `apps/mapping-config/**`, this checkpoint, `CHANGELOG.md`, `docs/CURRENT_STATE.md`, `docs/MASTER_PLAN.md`. Nothing else. |

## 1. Owner-supplied evidence — exact resolution in this session

Every file the Owner describes was searched for in the sandbox (worktree, ignored paths, `/home/user`, `/tmp`, mount points). The result:

| Evidence | Filename | Path | Byte size | SHA-256 | Committed? | Kind |
|---|---|---|---|---|---|---|
| Authoritative I/O card mapping workbook | `T8_IO_Card_Mapping.xlsx` | repository root | 16,648 | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` | **Committed evidence** | Authoritative topology (Owner workbook fact) |
| Primary product manuals, 750-362 / 750-430 / 750-530 / 750-601 / 750-613 / 750-471 / 750-554 / 750-600 | not present | — | not available | **not recorded — no hash is invented** | **OWNER_PROVIDED_OUTSIDE_REPOSITORY** | Primary manual (model-level facts) |
| Actual-rack configuration-tool sequence screen | not present | — | not available | **not recorded** | **OWNER_PROVIDED_OUTSIDE_REPOSITORY** | Actual-rack configuration evidence (instance level) |
| Actual 750-471 configuration screen (Pos. 10, Channel 3) | not present | — | not available | **not recorded** | **OWNER_PROVIDED_OUTSIDE_REPOSITORY** | Actual-rack configuration evidence (instance and Channel level) |

The eight manuals and the two screens are **not physically present in this sandbox**. Nothing was invented for them: the recorded facts are exactly the facts the Owner documented in the Stage ruling, the source records carry `availability: OWNER_PROVIDED_OUTSIDE_REPOSITORY`, `documentSha256: null` and `documentRevision: null`, and every observation that cites them is `PROVIDED_UNVERIFIED`. Owner evidence was **not committed** — the Owner has not authorised a repository path or inclusion for any of it.

## 2. Module-role ruling as implemented (Owner ruling, §3 of the Stage task)

| Model | Role | Process data | Input width | Output width | ProcessModulePosition | ProcessImageOrder | AddressStatus | AddressReason |
|---|---|---|---|---|---|---|---|---|
| 750-362 | `FIELDBUS_COUPLER` (head station) | NONE | 0 bit | 0 bit | none | none | `NOT_APPLICABLE` | `NON_PROCESS_DATA_MODULE` |
| 750-601 | `POWER_SUPPLY` | NONE | 0 bit | 0 bit | none | none | `NOT_APPLICABLE` | `NON_PROCESS_DATA_MODULE` |
| 750-613 | `SYSTEM_POWER_SUPPLY` | NONE | 0 bit | 0 bit | none | none | `NOT_APPLICABLE` | `NON_PROCESS_DATA_MODULE` |
| 750-600 | `END_MODULE` (terminates the internal bus) | NONE | 0 bit | 0 bit | none | none | `NOT_APPLICABLE` | `NON_PROCESS_DATA_MODULE` |
| 750-430 | `DIGITAL_INPUT_MODULE` | INPUT | not verified | 0 | yes | not verified | `ADDRESS_UNRESOLVED` | evidence reasons |
| 750-530 | `DIGITAL_OUTPUT_MODULE` | OUTPUT | 0 | not verified | yes | not verified | `ADDRESS_UNRESOLVED` | evidence reasons |
| 750-471 | `ANALOG_INPUT_MODULE` | INPUT | not verified | 0 | yes | not verified | `ADDRESS_UNRESOLVED` | evidence reasons |
| 750-554 | `ANALOG_OUTPUT_MODULE` | OUTPUT | 0 | not verified | yes | not verified | `ADDRESS_UNRESOLVED` | evidence reasons |

None of these four non-process modules is marked `ADDRESS_UNRESOLVED` anywhere in the package, the review report or the UI, and rack validation no longer warns that a Power Supply "address stays ADDRESS_UNRESOLVED". No further proof was requested that a Power Supply or End module has no Process I/O address.

The head station stays separate from the physical channel modules: it is not counted as an input or output module, it receives no `ProcessModulePosition` and no channel address, and any management or diagnostic service register it exposes sits outside the Application I/O process image and outside this Stage — it is **not** modelled as a Mapping Configuration I/O address.

## 3. Authoritative physical topology and the three position concepts

RackSlot is one-based, physical, comes from the workbook, and includes the Power Supply and End modules. There is no RackSlot 0. The visible configuration-tool sequence and ProcessModulePosition are separate fields and are never aliased to RackSlot or to each other.

| RackSlot | Model | Instance | Configuration-tool Pos. | ProcessModulePosition |
|---:|---|---|---:|---:|
| 1 | 750-362 | COUPLER-01 | not in the supplied visible sequence | — |
| 2 | 750-601 | SUPPLY-01 | not in the supplied visible sequence | — |
| 3–7 | 750-430 ×5 | DI-MODULE-01…05 | 01–05 | 1–5 |
| 8–11 | 750-530 ×4 | DO-MODULE-01…04 | 06–09 | 6–9 |
| 12 | 750-613 | SUPPLY-02 | not in the supplied visible sequence | — |
| 13–21 | 750-471 ×9 | AI-MODULE-01…09 | 10–18 | 10–18 |
| 22 | 750-554 | AO-MODULE-01 | 19 | 19 |
| 23 | 750-600 | END-MODULE-01 | 20 | — |

The supplied visible sequence Pos. 01–20 is fully accounted for by the 5 digital input, 4 digital output, 9 analog input, 1 analog output and 1 End module entries, so no visible position entry remains for the head station or the two power supplies. They remain physical modules of the workbook topology; a missing position entry does not remove them. The End Module has a position entry **and** contributes no process data — a configuration-tool position is not process data and is not an address.

**Field naming (boundary rule S3).** The Owner's ruling names the configuration-tool position `WagoIoCheckPosition`. The product tree may not contain vendor vocabulary — boundary scanner rule S3 rejects it and the scan must stay clean — so the field is `ioCheckPosition` in `packages/mapping-config` and `apps/mapping-config`, and the UI labels the column `IO-CHECK Pos.`. The two names denote the same concept: the "Pos." number of the actual rack in the vendor configuration tool. The existing Stage 0.4B-2 taxonomy already used the same neutral spelling (`IO_CHECK_EXPORT`, `VERIFIED_IO_CHECK_EXPORT`).

Actual-rack evidence belongs to the **ModuleInstance**, not to the current RackSlot: a Draft reorder moves the RackSlot and the actual-rack position of that instance does not change (proved by test R8).

## 4. Model-level versus instance-level evidence

* **Model level (`moduleProfiles.mjs`)** — role, process-data contribution, and `manualFacts` from the primary manuals: 750-430 eight input bits bit-packed (DI1…DI8 → bits 0…7); 750-530 eight output bits bit-packed; 750-471 four 16-bit input values, status/diagnostic representation Coupler- and configuration-dependent; 750-554 two 16-bit output values. Every manual record is `scope: MODEL_LEVEL_ONLY`, `evidenceState: PROVIDED_UNVERIFIED`, `availability: OWNER_PROVIDED_OUTSIDE_REPOSITORY`, `documentSha256: null`. A manual capability is never promoted to an actual-rack setting. No profile hard-codes a Slot, a position, an order or an address (test S-R1).
* **Instance level (`actualRackEvidence.mjs`)** — one record per physical module instance carrying `moduleInstanceId`, `rackSlot`, `ioCheckPosition`, model, role, `softwareRevision`, `hardwareRevision`, `displayedType`, `displayedVersion`, `revisionEvidenceState`, `evidenceState` and `evidenceNote`. Revisions are recorded only where legible; the supplied screen shows one unqualified version field (`01.01.46(04)` for Pos. 10), which is recorded verbatim and **not** asserted to be a software or a hardware revision. No revision is inferred for any other instance, and no revision is generalised across the nine 750-471 modules.
* **Channel level (`INSTANCE_CHANNEL_EVIDENCE`)** — one record: Pos. 10 / RackSlot 13 / AI-MODULE-01 / Channel 3, `scope: INSTANCE_AND_CHANNEL_ONLY`, state `VERIFIED_ACTUAL_RACK_SCREENSHOT`, with the visible settings (Signal type 4-20 mA; Input filter Off; Channel Diagnosis On; Overload, Wire break, Measuring range overflow and underflow, Upper user limit exceeded, Lower user limit undershot all On; Upper user limit 32767; Lower user limit −32768). `appliesTo` is explicitly false for other Channels of the same module, for other instances of the same model and for other models.

**Evidence taxonomy decision (§8 of the Stage task).** The supplied evidence is a screen read, not an export, so it is classified `VERIFIED_ACTUAL_RACK_SCREENSHOT` under a dedicated actual-rack state vocabulary. No `VERIFIED_*` observation state maps to the new `ACTUAL_RACK_SCREENSHOT` evidence type, which means actual-rack evidence can never on its own support a derived address (test S-R3 proves derivation is identical with and without it).

## 5. Read-only candidate process image

`candidateProcessImage.mjs` builds one candidate for the rack and labels every part `CANDIDATE_UNVERIFIED`:

* `authoritative: false`, `verified: false`, `hardwareReady: false`, `writeAuthority: false`.
* 19 contributing modules and 110 candidate channels (5×8 + 9×4 + 4×8 + 2), each separating Channel, ModuleInstanceId, RackSlot, `ioCheckPosition`, ProcessModulePosition, ProcessImageOrder (candidate ordinal per image area, basis `ACTUAL_RACK_VISIBLE_SEQUENCE`), area, bit width, evidence basis and verification status.
* **No offset is invented.** `byteOffset`, `wordOffset` and `bitOffset` are present, `null` and `OFFSET_UNRESOLVED` with reasons `ACTUAL_PROCESS_DATA_NOT_PROVIDED`, `ACTUAL_MAPPING_NOT_CROSS_CHECKED`, `NO_VERIFIED_PROCESS_IMAGE_RULE`, `PROCESS_IMAGE_ORDER_NOT_VERIFIED`. The head-station grouping rule, the word width, the alignment rule, the analog status/diagnostic representation and the actual field-network mapping are all unverified, so any number here would be a guess.
* Output area members carry `writeAuthority: false`: an output process-image reservation creates no write authority (test R18).
* The candidate is not rendered as numbers in the UI. The UI shows only its status line, so no unverified numeric value can be read as an address.

## 6. Evidence model corrections (§13 of the Stage task)

1. Power Supply and End modules use `NOT_APPLICABLE`, not `ADDRESS_UNRESOLVED`. ✔
2. Power Supply and End modules have no `ProcessModulePosition`; `processModulePositionStatus` is `NON_PROCESS_DATA_MODULE`. ✔
3. Profile definitions stay model-level and hard-code no physical Slot. ✔ (test S-R1)
4. Actual-rack evidence is instance-level. ✔ (test S-R2)
5. RackSlot and the configuration-tool position are both retained. ✔
6. ProcessModulePosition stays independent. ✔
7. Primary-manual facts and actual-rack settings are separate records with separate states. ✔
8. Settings verified for one Channel do not apply to another Channel or instance. ✔ (tests R11, R12)
9. Output reservation creates no write authority. ✔ (test R18)
10. No address is verified until actual mapping evidence is cross-checked. ✔ (tests R19, R20)

Consequential model changes: non-process modules are no longer treated as process-area blockers in `resolveProcessImage`, and a non-process profile carries an empty `missing` list because nothing about it is pending (the head station keeps its missing head-station evidence). Existing tests E1 and the profile/rack-view tests were updated to the corrected model; their intent is unchanged.

## 7. Recorded evidence requests (§12 of the Stage task)

Recorded in `MISSING_ACTUAL_RACK_EVIDENCE` and shown in the UI; none of them blocks the corrections above.

| | Subject | Required to show |
|---|---|---|
| A | Configuration-tool Process Data screen | complete Input process image; complete Output process image; offsets or word positions; module positions; Channels; any status/control fields |
| B | 750-471 Common settings | Data format; status-information setting; module-level process-data options |
| C | 750-471 Scaling screen | raw representation; user scaling; channel-specific scaling where applicable |
| D | 750-554 Settings | data format; signal mode; Channel settings; output scaling |
| E | 750-362 head-station information | exact firmware revision; exact hardware revision; I/O Config; field-network mapping where available |

Until A and E are supplied: the actual final field-network mapping remains unverified, numeric addresses must not be marked verified, and hardware test activation remains blocked.

## 8. UI (read-only)

The Evidence review table now shows, per module: Model · Instance, RackSlot, IO-CHECK Pos., ProcessModulePosition, Role, Process data, verified Width in/out, Profile state, Evidence source, Diagnostic/status byte/byte-word order, Actual-rack evidence, ProcessImageOrder, Address state, Reason, Fingerprint. A new read-only **Actual rack** section lists the Channel-level evidence with its scope statement, states that the Power Supply modules are physical modules without position entries, states that the End Module has a position while contributing no process data, shows the candidate status line and lists evidence requests A–E.

Power supplies and the End module display `NOT APPLICABLE` with reason `NON_PROCESS_DATA_MODULE` and never `ADDRESS UNRESOLVED` (tests A3, A4). No Hardware, Polling or Write control was added; the only controls remain reorder, Undo, Redo, Reset, filters, grouping, density and expand/collapse (tests A7, U18). The server still binds to `127.0.0.1` only and serves GET and HEAD only.

## 9. Tests

* `packages/mapping-config` — **268 tests, 261 pass, 0 fail, 7 skipped** without the workbook environment variable, and **268 / 268 / 0 / 0** with `MAPPING_EXCEL_DEFAULT_PATH` pointing at a verified copy outside the repository (the 7 skips are the pre-existing Owner-local workbook tests). New file `test/moduleRolesAndRackEvidence.test.mjs`: R1–R23 plus the defect sweep S-R1…S-R4, covering every numbered requirement of §16 of the Stage task.
* `apps/mapping-config` — **82 tests, 82 pass, 0 fail, 0 skipped**. New file `test/actualRack.render.test.mjs`: A1–A8, rendering the real UI against the authoritative 23-module rack (workbook copied outside the repository, as the server requires). Updated: U17 (new columns) and the Stage subtitle test.
* No generic stress or soak coverage was added.

## 10. Arena validation record

| Check | Command | Result |
|---|---|---|
| Mapping package check | `npm run check` in `packages/mapping-config` | PASS |
| Mapping package tests | `npm test` in `packages/mapping-config` | 268 / 261 / 0 / 7 (the 7 skips are the pre-existing Owner-local workbook tests) |
| Mapping package tests with the workbook | `MAPPING_EXCEL_DEFAULT_PATH=<copy outside the repository> npm test` | 268 / 268 / 0 / 0 (copy SHA-256 verified first) |
| Mapping UI check | `npm run check` in `apps/mapping-config` | PASS |
| Mapping UI tests | `npm test` in `apps/mapping-config` | 82 / 82 / 0 / 0 |
| Boundary scan S1–S9 | `node tools/boundary-scan/boundary-scan.mjs .` | 0 findings, exit 0 |
| PR-range whitespace | `git diff --check <base>...HEAD` | clean |
| Workbook SHA-256 | `sha256sum T8_IO_Card_Mapping.xlsx` | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` — unchanged |
| Clean tree | `git status --porcelain` | empty after the final commit |

**.NET was not touched by this Stage, so no .NET result is claimed and none is requested.** No browser was run in Arena: layout and real-browser behaviour are **NOT VERIFIED** here and remain an Owner-local review item.

### Owner-local PowerShell commands (pending)

```powershell
# 1. Exact-head proof
cd <repo>; git rev-parse HEAD; git status --porcelain; git ls-remote origin refs/heads/main

# 2. Authoritative Workbook tests (workbook copy OUTSIDE the repository, no seed)
$env:MAPPING_EXCEL_DEFAULT_PATH = "$env:USERPROFILE\wjss-evidence\T8_IO_Card_Mapping.xlsx"
(Get-FileHash $env:MAPPING_EXCEL_DEFAULT_PATH -Algorithm SHA256).Hash
cd packages\mapping-config; npm test

# 3. Mapping UI tests
cd ..\..\apps\mapping-config; npm test

# 4. Loopback browser review (the server binds to 127.0.0.1 only)
node server.mjs   # then open http://127.0.0.1:5181 in the browser

# 5. Boundary scan and repository cleanliness
cd ..\..; node tools\boundary-scan\boundary-scan.mjs .
git diff --check origin/main...HEAD; git status --porcelain
```

## 11. Changed / Unchanged / Not verified

**Changed** — `packages/mapping-config/src/{constants,moduleProfiles,rack,processImageEvidence,evidenceReport,draftSession,providerPolicy,index}.mjs`; new `packages/mapping-config/src/{actualRackEvidence,candidateProcessImage}.mjs`; new `packages/mapping-config/test/moduleRolesAndRackEvidence.test.mjs`; updated `packages/mapping-config/test/{processImageEvidence,rackAndProfiles}.test.mjs`; `apps/mapping-config/public/{index.html,app.mjs}`; new `apps/mapping-config/test/actualRack.render.test.mjs`; updated `apps/mapping-config/test/{review.render,ui}.test.mjs`; both `package.json` version/description strings; `CHANGELOG.md`; `docs/CURRENT_STATE.md`; `docs/MASTER_PLAN.md`; this checkpoint.

**Unchanged** — `T8_IO_Card_Mapping.xlsx`; every .NET project, `packages/contracts`, fixtures, Runtime, Runtime Inspector, Runtime publication and sequencing; boundary scanner; no dependency, lock file or write/control route added.

**Not verified** — actual Process Data image; actual field-network mapping; exact 750-362 firmware and hardware revisions; 750-471 Common settings and Scaling; 750-554 settings; any numeric address; module revisions other than the one legible screen; browser layout; Owner-local runs.
