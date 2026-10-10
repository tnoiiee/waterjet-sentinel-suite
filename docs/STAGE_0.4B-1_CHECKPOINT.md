# Stage 0.4B-1 — Simulation-Only Mapping Configuration (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT. Committed and pushed to the session branch; a PR targeting `main` is opened, not merged.**
Stop point: this checkpoint. Nothing is merged.

**Successor (Owner ruling 2026-10-10, final mapping):** `fix(mapping): confirm pump inlet range and workbook defaults`,
one commit after `8847cfc` on the same branch and PR #12. Source statically reviewed in Arena. **OWNER-LOCAL SUCCESSOR
VALIDATION REQUIRED. OWNER BROWSER REVIEW PENDING. PR #12 OPEN - NOT MERGED.** See the section *Owner ruling 2026-10-10 - final mapping* below.

## Owner exception (Boundary Hold, AGENTS §11.5 / PUBLIC_REPOSITORY_BOUNDARY §7)

The Owner granted a **documented exception**, limited to this workbook and Stage 0.4B-1. It does not
authorise committing future Owner files. Record:

| Item | Value |
|---|---|
| Workbook path | `T8_IO_Card_Mapping.xlsx` (repository root) |
| Owner input commit | `6ff9e4a` (parent `a5540057…`, the PR #11 merge commit on `main`) |
| File SHA-256 | `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e` (16,648 bytes) |
| Sheet names | `Sheet1` (the only sheet; no hidden sheets) |
| Status | Owner-stated authoritative input. The workbook is unchanged in this stage. |
| Contents audit | No credential, password, token, private key, connection string with a secret, IP address or email. The URLs are standard OOXML namespaces. The `uid` matches are Excel revision GUID attributes (`xr:uid`), not user IDs. |
| Disclosure | The workbook's document properties include an author name (personnel text). It is covered by this exception and is disclosed here. |
| Structure | 0 formulas, 0 merged cells, 0 hidden rows, 0 hidden columns, no comments, no macros, no external links. |
| Scope | Not converted to runtime configuration. Not deployed. Not given numeric Modbus addresses (all remain ADDRESS_UNRESOLVED). |
| Temporary files | None committed. Scratch copies were removed. |

The workbook is already in `6ff9e4a`, so it appears in the PR diff against `main`. No new commit adds it again.

Scope boundary: no MODBUS, no TEST_HARDWARE, no PRODUCTION, no physical device read or write,
no Pump/Valve/Axis command authority, no device credentials, no external dependency.

## CHANGED (uncommitted, in the working tree)

| Path | Purpose |
|---|---|
| `packages/mapping-config/package.json` | Zero-dependency package, `type: module`, `node:test`. |
| `packages/mapping-config/src/constants.mjs` | Shared vocabulary; forbidden address keys for bindings. |
| `packages/mapping-config/src/moduleProfiles.mjs` | Profiles for the eight models. All INCOMPLETE; no address rule. |
| `packages/mapping-config/src/rack.mjs` | ModuleInstanceId, RackSlot, ProcessModulePosition (provisional slot-order ordinal), validation, reorder. |
| `packages/mapping-config/src/tagCatalogue.mjs` | Simulation/Runtime tag identities; both pump ranges 0–40 bar (4-20 mA, Owner-confirmed); the 26 authoritative default sources and the 18 placeholder rows; `pressureTagForWj(n)` returns IVn only. |
| `packages/mapping-config/src/mappingValidation.mjs` | Binding validation; polarity explicit; no aliasing; output refusal. |
| `packages/mapping-config/src/addressDerivation.mjs` | Derived address states; canonical zero-based arithmetic only with a verified rule object. |
| `packages/mapping-config/src/revisions.mjs` | RackTopologyRevision, TagMappingRevision, ModuleProfileRevision, DerivedAddressManifestFingerprint. |
| `packages/mapping-config/src/canonical.mjs` | Canonical JSON and dependency-free SHA-256 (browser-safe). |
| `packages/mapping-config/src/draftSession.mjs` | Draft rack, undo/redo, reset, validation, impact preview, draft revision document. No activation method. |
| `packages/mapping-config/src/providerPolicy.mjs` | SIMULATOR only; every other provider name refused. |
| `packages/mapping-config/src/workbookImport.mjs` | Deterministic import, Node-only, reports issues and never repairs them. |
| `packages/mapping-config/src/nodeImport.mjs` | Node-only entry for the importer. |
| `packages/mapping-config/src/syntheticExample.mjs` | Generic SYNTHETIC EXAMPLE configuration (not plant data). |
| `packages/mapping-config/src/index.mjs` | Browser-safe public entry. No Node built-ins. |
| `packages/mapping-config/test/*.test.mjs`, `test/helpers/*` | Focused tests (see Validation). Synthetic fixtures only. |
| `apps/mapping-config/package.json` | UI shell package, loopback only. |
| `apps/mapping-config/server.mjs` | GET-only host. Binds to loopback. The Excel path is optional and local, outside the repo. It alone loads the authoritative default. The seed is an optional override that requires the Excel path. |
| `apps/mapping-config/public/{index.html,app.mjs,styles.css}` | Read-only UI with a Draft rack. Only Undo, Redo, Reset controls. |
| `apps/mapping-config/test/*.test.mjs`, `test/helpers/*` | UI shell tests and a stub-DOM render test. |
| `packages/mapping-config/src/limitNormalization.mjs` | Pure normalization: ContactPolarity, then verified RawInputInversion (applied once), then LimitDetected. Not wired to Runtime. |
| `docs/STAGE_0.4B-1_CHECKPOINT.md` | This document. |

## UNCHANGED

- Runtime (.NET), Inspector (GET-only), AutoSequence (FIFO, one active job), Pump/Valve separation,
  WJn/IVn pairing, Runtime atomic publication, existing tests and fixtures. No tracked file is modified
  (`git diff --stat` against the base is empty; all work is in new untracked paths).
- Workbook `T8_IO_Card_Mapping.xlsx` (Owner commit `6ff9e4a`). Not modified, not re-committed, not removed.
- Existing TypeScript contract package and the boundary scanner rules.

## Behaviour delivered

- **Topology.** RackSlot 1–23 and ProcessModulePosition are both shown. ProcessModulePosition is display,
  ordering and validation only. It is not an address or offset.
- **Identity.** ModuleInstanceId (for example `AI-MODULE-01`) survives reorder. Bindings reference
  ModuleInstanceId and Channel, never RackSlot.
- **Addresses.** Every enabled binding is `ADDRESS_UNRESOLVED` with reasons
  `HEAD_STATION_PROFILE_NOT_VERIFIED`, `NO_VERIFIED_PROCESS_IMAGE_RULE`,
  `MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED` and, for analog channels,
  `MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED`. No numeric address is produced from the catalogue.
  The arithmetic is tested only with a clearly labelled SYNTHETIC TEST RULE.
- **Pressure identities (Owner rulings, 2026-10-10).** Pump Inlet, Pump Outlet and IVn outlet pressure are
  distinct physical measurements. AI-002 = Pump Inlet Pressure (`PumpInletPressureBar`, `PUMP_INLET_PRESSURE`,
  source `PUMP_INLET`), 4-20 mA, 0–40 bar: Header Tank / suction-side pressure before the Pump, read-only diagnostic
  and trend only. AI-003 = Pump Outlet Pressure (`PumpOutletPressureBar`, `PUMP_OUTLET_PRESSURE`, source
  `PUMP_OUTLET`), 4-20 mA, 0–40 bar: pump discharge pressure immediately after the Pump, the sole pre-P1 Pump-ready
  source. AI-004–AI-011 = IV1–IV8 outlet pressure (`IVn_OUTLET_PRESSURE`, ordinal `#n = IVn`), valve diagnostics only.
  No Main Valve I/O tag is defined. The workbook has no Main Valve row, so none is required and none is aliased to
  AI-003. No aliasing, averaging, derivation or cross-fallback exists across these groups. WJn uses only its paired
  IVn pressure (`pressureTagForWj`). Pump pressure is never used for IVn diagnosis.
- **Pump location is editable; pump identity is locked.** The workbook gives the default module, Slot and
  Channel. The Draft may change them when the change is compatible. Validation enforces channel capacity,
  signal type, physical duplicate binding, pump and valve separation, and ordinal consistency. The workbook
  identifier, source identity and canonical identity are never editable, so AI-002 cannot become AI-003 or an IV.
- **Source description is evidence, not identity.** A pump binding keeps the trimmed workbook text as
  `sourceDescription`. A description that differs from the Owner display name is accepted when the identifier
  matches the Owner table and the text names no other pump side and no IV ordinal. Otherwise the row is refused
  with `PUMP_PRESSURE_LABEL_CONFLICT`.
- **AI-002 Runtime publication: DEFERRED, NOT IMPLEMENTED (Owner ruling 2026-10-10).** The Runtime, Inspector and
  shared contracts are unchanged, and the Draft does not feed Runtime. A future candidate, not in this PR, is a
  read-only `PumpInletPressure` observation with no transition, no Pump-ready effect, no alarm, trip or interlock,
  and read-only Runtime/Inspector presentation. Until that is separately approved, AI-002 is a Mapping
  Configuration binding only.
- **PumpReady.** Reads the Pump Outlet (`PUMP_OUTLET`, AI-003) only. The Runtime names no inlet source.
  The mapping gate refuses AI-002 and any IV pressure.
- **Limits.** Digital limit tags bind to DI channels and require an explicit polarity. Owner rule: all
  16 IVn lower- and upper-limit inputs marked NO are `ACTIVE_WHEN_CLOSED`. The import records this with
  `polarityBasis: OWNER_RULE`. An explicit seed value is kept as written and recorded as `EXPLICIT_SEED`.
  Validation requires one ContactPolarity across all limits unless a channel records `polarityOverride`.
  - Interpretation before any verified hardware inversion: contact open = `LimitDetected` false; contact
    closed = `LimitDetected` true.
  - ContactPolarity is kept separate from: WAGO module inversion, coupler/process-image inversion,
    field-wiring inversion, and software acquisition-profile inversion. RawInputInversion is
    NOT_CONFIGURED by default and UNVERIFIED. A verified inversion, when configured, is applied exactly
    once (`normalizeLimit`).
  - A wire break is never claimed from an NO contact alone (`wireBreakDetectable` is always false).
  - Pump pressure and Valve pressure are never a substitute for a limit input. A digital limit bound to
    an analog channel is refused (`LIMIT_TO_ANALOG_REFUSED`).
  - IVn lower and upper inputs stay bound only to their own IVn. A limit binding must declare its own
    identity (`SOURCE_IDENTITY_MISMATCH` otherwise). On import, the workbook row must carry the same IV
    index (`#n`) and the same group word (Lower or Upper) as the tag, or the binding is refused with
    `LIMIT_IV_LABEL_MISMATCH` and is not made. Mismatches are reported, never repaired. The Owner-local
    workbook passes this check for all 16 limits.
  - Hardware inversion remains UNVERIFIED. TEST_HARDWARE activation remains blocked.
- **Outputs.** Output rows are classified `NOT AUTHORIZED FOR MAPPING IN READ-ONLY STAGE`. No output
  tag can be bound, and there is no write provider.
- **Provider.** Canonical provider identity is `SIMULATOR` (Owner decision, 2026-10-10). The provider
  constant, the provider policy and the UI provider chip all use it. The display label "Simulation"
  ("SIMULATION ONLY") is human-readable only. It is not a provider identity and is not serialized. The
  package performs no I/O and holds no credentials. It has no Modbus, TCP or device code. Draft state does
  not feed Runtime or RuntimePublication.
- **Revisions.** Deterministic. Independent of timestamps, object key order, array order of bindings,
  and UI state. Verified by tests.
- **Impact preview.** Classification precedence: BINDING_INVALID > BLOCKED > ADDRESS_CHANGED > MOVED >
  ADDRESS_UNRESOLVED > UNCHANGED.
- **Authoritative defaults (successor).** The import loads **26 default bindings with no seed**: AI-002 and AI-003
  (pump), AI-004–AI-011 (IV1–IV8 outlet pressure), DI-021–DI-028 (IV1–IV8 Lower) and DI-029–DI-036 (IV1–IV8 Upper),
  each derived from its explicit workbook identifier under the Owner ordinal rule. The import reports
  `DEFAULT_SOURCE_NOT_FOUND` for a missing default source (a pump keeps `PUMP_SOURCE_NOT_FOUND`) and never substitutes
  a neighbour. A seed (`MAPPING_BINDING_SEED_PATH`) is an optional authorised override. It replaces the default for
  the same runtime tag and goes through the same checks, and it requires `MAPPING_EXCEL_DEFAULT_PATH`. A pump
  signal that is not 4-20 mA is refused with `PUMP_SIGNAL_MISMATCH`.
- **Placeholder rows are never bindable (successor).** Any row whose signal carries `XXX` is refused as a binding
  source (`PLACEHOLDER_ROW_REFUSED`), whether it comes from a default or an override. The 18 rows are DI-037, DO-031
  and AI-020 to AI-035.
- **Workbook import.** Reports sheet, rows, counts (USED/SPARE), slots, models, channels, duplicates,
  blank or malformed values, ambiguous inputs, placeholder rows and model-profile gaps. It never repairs
  silently. Placeholder rows (DI-037, DO-031, AI-020 to AI-035) stay USED / RESERVED with
  SignalIdentity = UNRESOLVED, TagBinding = UNBOUND and OwnerInputStatus = OWNER_INPUT_PENDING. They are
  not FREE, are never auto-bound, and raise `SIGNAL_IDENTITY_UNRESOLVED` and `OWNER_INPUT_PENDING`. Output placeholders are classified as outputs first, so their output
  classification takes precedence over the reserved-row count.
- **UI.** Header, rack layout (Draft drag-and-drop, plus Alt+Arrow keyboard reorder), Undo, Redo, Reset,
  tag mapping (read-only), validation, impact preview and revisions. Controls are limited to Undo, Redo
  and Reset. There is no Connect, Poll, Read, Write, Force, command, Activate, TEST_HARDWARE or Production control.

## Validation

| Gate | Result | Evidence |
|---|---|---|
| Mapping package tests (successor) | **VERIFIED IN ARENA** | `npm test` in `packages/mapping-config`, no workbook env: 182 tests, 175 pass, 0 fail, 7 skipped (Owner-local only). With `MAPPING_EXCEL_DEFAULT_PATH` set to a byte-identical copy outside the repository and no seed variable: 182 pass, 0 fail, 0 skipped. |
| UI shell tests (successor) | **VERIFIED IN ARENA** | `npm test` in `apps/mapping-config`: 25 pass, 0 fail, with and without the workbook env. The UI test servers use an explicit empty environment, so the synthetic-default tests do not depend on Owner-local variables. |
| Syntax checks | **VERIFIED IN ARENA** | `npm --prefix packages/mapping-config run check` and `npm --prefix apps/mapping-config run check`: exit 0. |
| Boundary scan (S1–S9) | **VERIFIED IN ARENA** | `node tools/boundary-scan/boundary-scan.mjs .`: 0 findings. |
| Whitespace in new files | **VERIFIED** | Grep for trailing whitespace and tabs: none. `git diff --check` on tracked files: empty. |
| Read-only and control scan | **VERIFIED** | UI test: button whitelist is exactly Undo, Redo, Reset. No non-GET request, no network API, no HTML injection sink. |
| Owner-local workbook topology | **VERIFIED IN ARENA AGAINST AN OUT-OF-REPO COPY** | Byte-identical copy (SHA-256 `4e0337e2…f422e8e`) outside the repository, removed afterwards. 23 modules in the expected sequence. Aggregate output only. |
| Authoritative default import (successor, no seed) | **VERIFIED IN ARENA AGAINST AN OUT-OF-REPO COPY** | Status VALID, 0 rack errors, 0 mapping errors. 26 enabled defaults with 0 `REQUIRED_TAG_MISSING`. 16 limits ACTIVE_WHEN_CLOSED (OWNER_RULE). AI-002 and AI-003 both 4-20 mA, 0–40 bar. 26 addresses ADDRESS_UNRESOLVED. Activation NOT AUTHORIZED, blocking reason `NO_VERIFIED_PROCESS_IMAGE_RULE`. 29 disabled read-only input listings. 18 placeholders unbound. Warnings only (profiles incomplete, IVn ranges unconfigured, placeholders pending). |
| Owner-local successor validation | **REQUIRED — NOT RUN BY THE OWNER** | The Owner runs the commands in the section below on the successor head. |
| Credentials audit of the workbook and repo diff | **VERIFIED** | See the Owner exception table above. |
| Locked restore, Release build, Runtime.Core and API tests, full .NET suite | **NOT VERIFIED** | The .NET SDK is absent in this sandbox. No .NET file is changed. |
| Fixture parity | **NOT VERIFIED** | No fixture changed. The parity run needs the .NET generator. |
| TypeScript checks | **NOT VERIFIED** | The TS package has no installed dependencies here. No TS file is changed. Installing them needs a STOP. |
| Browser rendering and layout | **NOT VERIFIED** | No browser in the sandbox. Verified only through the stub DOM and server tests. |
| `git diff --check` and clean tree | **VERIFIED AT COMMIT** | `git diff --check` clean and no uncommitted change before the commit that carries this document. The tree is committed; see the commit log. |
| Full .NET regression (successor) | **NOT RUN** | No .NET file is changed by the successor, and the shared boundary is not changed. The predecessor evidence (414/414) is unchanged and is not re-claimed by this successor. |

## Owner decisions

1. **Workbook under Boundary Hold.** Applied: documented exception (see the table above). The exception is
   limited to this workbook and Stage 0.4B-1.
2. **Digital limit polarity.** Applied: ACTIVE_WHEN_CLOSED for all 16 IVn lower- and upper-limit inputs.
   Inversion stays UNVERIFIED. Required tests for open, closed, verified inversion (applied once), and
   per-IVn binding are implemented and pass.
3. **Remaining Owner decisions 1–5 of the Stage brief** (input, addresses, ProcessModulePosition, IV
   mapping, placeholder rows) are applied as written.
4. **Digital limit polarity (final bullet), resolved.** The Owner restated the full text on 2026-10-10. It is
   recorded verbatim below. Items it adds beyond the earlier answer are implemented as stated in the Limits
   bullet above.

   > All IV1-IV8 Lower-limit and Upper-limit inputs marked NO in the authoritative workbook use:
   > ContactPolarity = ACTIVE_WHEN_CLOSED
   > Interpretation before any verified hardware inversion: contact open = LimitDetected false; contact
   > closed = LimitDetected true. Keep contact polarity separate from WAGO module inversion,
   > coupler/process-image inversion, field-wiring inversion and software acquisition-profile inversion.
   > Apply any later verified inversion exactly once. Do not claim wire-break detection from an NO contact
   > alone. Do not use Pump pressure or Valve pressure as a substitute for a limit input. IVn Lower and
   > Upper inputs must remain bound only to IVn. Hardware inversion remains UNVERIFIED. TEST_HARDWARE
   > activation remains blocked.

5. **Provider identity (resolved 2026-10-10).** `SIMULATOR` stays canonical in serialization, validation,
   Draft bindings and provider identity. It is not renamed to SIMULATION in Stage 0.4B-1. "Simulation" is a
   display label only.
6. **Boundary S6 and preview (resolved 2026-10-10).** The server binds to 127.0.0.1 only. No 0.0.0.0, no
   `::`, and no preview bypass is added. Platform preview unavailability is accepted. Owner-local browser
   review is authoritative.
7. **Placeholder rows (resolved 2026-10-10).** DI-037, DO-031 and AI-020 to AI-035 stay USED / RESERVED,
   SignalIdentity UNRESOLVED, TagBinding UNBOUND, OwnerInputStatus OWNER_INPUT_PENDING. They are not
   re-requested unless new Owner identity data is supplied.

## Known limitations

- No verified process-image rule. Every address is unresolved by design until the 750-362 process-image
  rule and each module's process-data profile are verified from primary evidence or a WAGO-IO-CHECK export.
- Analog status-byte setting is not verified.
- Engineering ranges are UNCONFIGURED except the two pump ranges (AI-002 and AI-003, 0–40 bar, Owner-confirmed basis `OWNER_CONFIRMED_DOMAIN_INFORMATION`). IVn pressure ranges are UNCONFIGURED.
- **AI-002 hardware range limitation (accepted, recorded).** The expected installed-system pressure is below approximately
  1 bar, a small part of the 0–40 bar span. This is an accepted hardware-selection limitation. A future hardware review
  may consider a lower-range or compound transmitter. No NPSH, cavitation or suction-protection claim is made.
- **AI-002 is diagnostic and trend only.** It has no threshold, alarm, trip or interlock. It is not Pump-ready evidence.
- **AI-002 Runtime publication is NOT IMPLEMENTED** (deferred, see the pressure identities bullet above).
- **Addresses remain ADDRESS_UNRESOLVED.** No numeric Modbus address, offset or verified ProcessImageOrder exists.
  The status codes are `HEAD_STATION_PROFILE_NOT_VERIFIED`, `MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED` and
  `MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED`.
- The browser UI has not been run in a real browser by the agent. Owner-local browser review is
  authoritative (Owner decision 6).
- The platform live preview is unavailable for this checkpoint by decision (S6). The server binds to
  127.0.0.1 only, through the constant `UI_BIND_HOST`. Product code does not read `MAPPING_UI_HOST` or any
  other environment variable for the bind host. Regression tests are in `apps/mapping-config/test/loopback.test.mjs`,
  and boundary rule S6 flags any environment-controlled bind host.
- Hardware RawInputInversion is UNVERIFIED. Limit detection is therefore a logical mapping only.
  TEST_HARDWARE activation remains blocked.

## Commit sequence (on `arena/1b92c50a-waterjet-sentinel-suite`)

1. `feat(mapping): rack, profiles and tag catalogue`
2. `feat(mapping): derive addresses and validate`
3. `feat(mapping): read-only mapping configuration UI shell`
4. `test(mapping): reorder, addressing, validation, polarity and boundary`
5. `docs(checkpoint): stage 0.4B-1 development checkpoint`
6. Follow-up (this revision): the IVn label check, its tests, the synthetic label convention, and the resolved
   polarity bullet. The workbook is not touched.
7. Blocker correction (B1, B2), on the same branch and PR, with no new scope gate, branch or PR:
   - `fix(mapping): enforce loopback-only server binding`. The bind host is the constant `127.0.0.1`.
     `MAPPING_UI_HOST` is removed from Product code. Regression tests and boundary rule S6 cover it.
   - `fix(mapping): enforce IV pressure ordinal identity`. `WSB Pressure transmitter #n = IVn` is enforced for
     AI-004 to AI-011 to IV1 to IV8 outlet pressure. A mismatch is refused with `PRESSURE_IV_LABEL_MISMATCH`
     and creates no Binding. The Draft path refuses a module or Channel change for IVn pressure and limit tags.

8. Successor (Owner ruling 2026-10-10, same branch and PR, no new scope gate, branch or PR):
   `fix(mapping): confirm pump inlet range and workbook defaults`. AI-002 and AI-003 are 4-20 mA, 0–40 bar.
   The 26 authoritative defaults load without a seed. The seed is an optional override. Placeholders are refused
   as sources. AI-002 Runtime publication is deferred. See the section *Owner ruling 2026-10-10 - final mapping*.

The workbook is not added by any of these commits. It arrived in `6ff9e4a` under the exception above.
The PR targets `main`, is not merged, and does not reuse PR #11's branch.

## Owner ruling 2026-10-10 - final mapping

Applied as the successor `fix(mapping): confirm pump inlet range and workbook defaults`, one commit after `8847cfc`
on `arena/1b92c50a-waterjet-sentinel-suite`, PR #12, with no new Scope Gate, branch or PR. Not merged. No force-push.

- AI-002 range: OWNER-CONFIRMED. 4-20 mA, 0–40 bar. AI-002 DIAGNOSTIC/TREND ONLY. Read-only. Not Pump-ready, not valve
  diagnostic, not alarm, trip or interlock. No AI-002 threshold, NPSH, cavitation or suction-protection claim.
- AI-003 (`PumpOutletPressureBar`, source `PUMP_OUTLET`): 4-20 mA, 0–40 bar. AI-003 PUMP-READY ONLY. It is the sole
  pre-P1 Pump-ready source. Pump-ready reads `PUMP_OUTLET` only and refuses `PUMP_INLET` and every IVn pressure.
- AUTHORITATIVE DEFAULT DOES NOT REQUIRE A SEED. The 26 default bindings are 2 pump, 8 IVn pressure, 8 Lower and 8 Upper,
  derived from explicit workbook identifiers and Owner ordinal rules. The seed is an optional authorised override only.
- 18 placeholders are excluded from defaults and are refused as binding sources: DI-037, DO-031, AI-020 to AI-035.
  They stay USED / RESERVED, SignalIdentity UNRESOLVED, TagBinding UNBOUND, OWNER_INPUT_PENDING and ADDRESS_UNRESOLVED.
- No Main Valve pressure tag. `MAIN_VALVE_OUTLET_PRESSURE` is absent from the catalogue and the required set. AI-003 is never Main Valve.
- AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. Deferred. The future candidate is listed under Pressure identities above.
- ADDRESSES REMAIN ADDRESS_UNRESOLVED. Every address status code is non-numeric. No numeric address is entered or overridden.
- OWNER BROWSER REVIEW PENDING. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED.
  PR #12 OPEN - NOT MERGED.

**Pump identity and source are locked; Slot and Channel are editable.** Pump Slot/Channel is Draft-editable when the
change is compatible, has a valid capacity, has no physical duplicate, is not shared with an IVn pressure, and has the
correct signal. The derived address is read-only and follows the change. Locked: `tagName`, canonical identity,
`sourceWorkbookTag`, `declaredSourceIdentity`, and the Pump-ready role. Swaps, AI-002 to Outlet, AI-003 to Inlet, a pump
moved onto an IVn source, and a shared channel are refused. The workbook description is source evidence and is not
identity. A description mismatch with a matching identifier raises no warning. An identifier conflict is still refused.

**Owner-local browser command (successor head, after the Owner's own checkout is updated):**

```
MAPPING_EXCEL_DEFAULT_PATH=/absolute/path/outside/the/repository/T8_IO_Card_Mapping.xlsx node apps/mapping-config/server.mjs
```

Then open `http://127.0.0.1:5186`. Do not set `MAPPING_BINDING_SEED_PATH` for the authoritative default. The server
binds to 127.0.0.1 only.

**Owner-local browser checklist (23 items).** Report the result to Arena. Do not create an Owner UI closeout until the Owner reports.

1. The workbook default is shown. 2. Coupler and End are fixed. 3. Drag and drop reorders through the Draft.
4. Keyboard reorder (Alt+Arrow) reorders through the Draft. 5. Undo works. 6. Redo works. 7. Reset works.
8. RackSlot and ProcessModulePosition are shown and distinct. 9. Every address shows ADDRESS_UNRESOLVED.
10. No numeric-address entry exists. 11. Pump and IV1–8 pressure are distinct. 12. The 16 limits map `#n` to IVn, ACTIVE_WHEN_CLOSED.
13. The 18 placeholders show USED / RESERVED / UNBOUND. 14. A duplicate binding is refused. 15. A wrong-type binding is refused.
16. A wrong IV ordinal is refused. 17. A wrong IV group is refused. 18. Nothing activates to hardware.
19. No MODBUS control is present. 20. No write control is present. 21. Loopback only (127.0.0.1).
22. The Pump Slot/Channel can be changed in the Draft, and identity is locked. 23. AI-002 is shown as diagnostic and trend only, never Pump-ready.

**Seed-generation handoff (optional override, not required for defaults).** No seed generator is implemented in this
PR, and no new local script is added. If an authorised override is needed later, the handoff is: a deterministic
local-only command, output outside the repository, not committed, with no guessed identity and no numeric Modbus
address. The Owner must not hand-edit seed JSON. The command, output path, schema, binding count, identities by category,
cleanup command and proof that no repository file changed are reported before any Owner-run step. A new local script
requires a STOP and a report of its file and scope first.

**Known limits of this successor.** The workbook is unchanged and remains the authoritative input. .NET and TypeScript
were not run here. No browser run was made here. The Owner-local successor validation has not been run by the Owner.
The predecessor evidence (414/414) is not re-claimed by this successor.
