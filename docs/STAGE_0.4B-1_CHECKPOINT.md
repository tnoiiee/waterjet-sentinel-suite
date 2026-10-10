# Stage 0.4B-1 — Simulation-Only Mapping Configuration (Development Checkpoint)

Status: **STAGE 0.4B-1 FINAL SOURCE REVIEW PASSED. OWNER-LOCAL AUTOMATED VALIDATION PASSED. OWNER BROWSER REVIEW PASSED WITH NON-BLOCKING UI PUNCHLIST. Validated code head `433189f9e489d7af6770e64c971585d99bf941ad`. Documentation closeout on the same branch and PR #12. Not merged.**
Stop point: this checkpoint. Nothing is merged.

**Successor (Owner ruling 2026-10-10, final mapping):** `fix(mapping): confirm pump inlet range and workbook defaults`,
one commit after `8847cfc` on the same branch and PR #12. Source statically reviewed in Arena. **OWNER-LOCAL VALIDATION PASSED at `433189f` (Owner-reported: Mapping 217/217, Mapping UI 56/56). OWNER BROWSER REVIEW
PASSED WITH NON-BLOCKING UI PUNCHLIST. Source review: no blocking defect. PR #12 OPEN - NOT MERGED.** See the section *Owner ruling 2026-10-10 - final mapping* below.

## Correction: authoritative defaults, reserved inventory and fixed Dark UI (2026-10-10)

Stage 0.4B-1 correction on the same branch and PR #12, after the successor `5f9f437`. No new scope gate, branch or PR.
Not merged. No force-push. Commits: `438cfd2` complete authoritative workbook defaults; `09608bb` expose reserved
unresolved inventory (with analog presentation); `64204e9` apply fixed dark presentation; and this docs commit.

- **Defaults.** 26 authoritative bindings load from the workbook alone. No seed. Pump: AI-002 is PUMP_INLET_PRESSURE and
  AI-003 is PUMP_OUTLET_PRESSURE. IV1–8 outlet pressure from AI-004–011, Lower from DI-021–028 and Upper from
  DI-029–036, with `#n = IVn`. A limit row with two `#n` labels or two Lower/Upper words is refused.
- **Seeded Main Valve.** A seed entry for `MAIN_VALVE_OUTLET_PRESSURE` is refused as `UNKNOWN_TAG` at import, and no
  binding is created.
- **Count of 17 (root cause).** The classification tested for OUTPUT before the placeholder marker. DO-031 is an OUTPUT,
  so it was counted as `outputNotAuthorised` and the placeholder count stopped at 17. The reserved test now runs first,
  so each row is counted once: `reservedUnresolved` is 18, and `outputNotAuthorised` is 31 (the other outputs). DO-031 is
  shown in the inventory as OUTPUT with NOT_AUTHORIZED_IN_READ_ONLY_STAGE, as a factual overlay, not as a second count.
- **Reserved inventory.** A read-only section, *Reserved channels awaiting Owner identity (18)*, shows all 18 rows with
  the 11 Owner columns. Each row is USED / RESERVED, SignalIdentity UNRESOLVED, BindingStatus UNBOUND,
  OwnerInputStatus OWNER_INPUT_PENDING, AutomaticBinding PROHIBITED, AvailableAsSpare false, ADDRESS_UNRESOLVED.
- **Analog presentation.** Analog pressure rows show Polarity NOT APPLICABLE and Contact NOT APPLICABLE. This is
  presentation only. Digital limits keep ACTIVE_WHEN_CLOSED and NO.
- **Fixed Dark theme.** One Dark presentation only: no selector, no SYSTEM or LIGHT option, no theme storage, no theme API
  and no theme state in the Draft or any revision. No external CSS and no network request beyond the configuration read.
- **Tests.** Default bindings D01–D15 (`defaultBindings.acceptance.test.mjs`), placeholder inventory P01–P20
  (`placeholderInventory.acceptance.test.mjs`), dark presentation T01–T19 (`darkTheme.test.mjs`) and reserved UI
  (`reserved.render.test.mjs`, 11 items). Pre-fix and mutant runs were made in `/tmp` copies only; no mutant file is committed.

Honesty lines for this correction: AI-002 RANGE OWNER-CONFIRMED. AI-002 DIAGNOSTIC/TREND ONLY. AI-003 PUMP-READY ONLY.
AUTHORITATIVE DEFAULT HAS 26 BINDINGS. AUTHORITATIVE DEFAULT REQUIRES NO SEED. RESERVED PLACEHOLDER INVENTORY HAS 18 ROWS.
FIXED DARK THEME ONLY. AI-002 RUNTIME PUBLICATION NOT IMPLEMENTED. ADDRESSES REMAIN ADDRESS_UNRESOLVED. OWNER BROWSER
REVIEW PASSED WITH NON-BLOCKING UI PUNCHLIST (Owner-reported; see the Owner validation section). NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED. PR #12 OPEN - NOT MERGED.
Source statically reviewed. Owner-local successor validation PASSED (Owner-reported at `433189f`).

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

## CHANGED (cumulative, committed on the branch, against `main` `a5540057`)

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
| `apps/runtime/Inspector/index.html` | **Owner-approved narrow presentation exception.** Two labels changed from the generic 'Pump pressure' wording to 'Pump Outlet pressure' (commit `a0642d4`). Presentation wording only: no Runtime state, Delta, contract, sequencing, alarm, interlock, control or device access. Does not publish AI-002. |
| `tools/boundary-scan/boundary-scan.mjs` | **Authorised B1 regression protection.** Rule S6 also flags an environment-controlled bind host (commit `156657d`). Scanner tooling only, not Product code. Owner-classified as in scope. |
| `docs/STAGE_0.4B-1_CHECKPOINT.md` | This document. |

## UNCHANGED

- Runtime (.NET) source, Runtime State and Delta, shared contracts and fixtures, FixtureGenerator, Simulator
  scenarios, MODBUS adapters, device profiles, dependencies and lock files: **not modified** by any Stage 0.4B-1
  commit. `git diff --name-only 8847cfc 433189f` lists no such path.
- Inspector (`apps/runtime/Inspector/index.html`): **changed by two presentation labels only** (accepted exception,
  see the CHANGED table). Its GET-only behaviour is unchanged.
- Boundary scanner (`tools/boundary-scan/boundary-scan.mjs`): **changed by the authorised S6 environment-bind-host
  rule only.** No other rule is changed.
- Workbook `T8_IO_Card_Mapping.xlsx` (Owner commit `6ff9e4a`): not modified, not re-committed, not removed.
- Existing TypeScript contract package: not modified.

An earlier Arena report stated that the Inspector and scanner changes were uncommitted and that the Runtime Inspector
was unchanged. That was incorrect. Both changes are committed (`a0642d4`, `156657d`), and they are classified in the
Owner validation section below.

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
- **Pump identity is locked; pump Slot and Channel are supported by the Draft model API.** The workbook gives the default
  module, Slot and Channel. `DraftSession.setBinding` may change them when the change is compatible. This is supported by
  the Draft model API and focused tests, but is not exposed as a browser editor control in Stage 0.4B-1. Validation enforces channel capacity,
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
  not FREE, are never auto-bound, and raise `SIGNAL_IDENTITY_UNRESOLVED` and `OWNER_INPUT_PENDING`. Reserved placeholders are classified before the output test, so each row is
  counted once (DO-031 is reserved, and its output fact is an overlay in the inventory).
- **UI.** Header, rack layout (Draft drag-and-drop, plus Alt+Arrow keyboard reorder), Undo, Redo, Reset,
  tag mapping (read-only), validation, impact preview and revisions. Controls are limited to Undo, Redo
  and Reset. There is no Connect, Poll, Read, Write, Force, command, Activate, TEST_HARDWARE or Production control.

## Validation

| Gate | Result | Evidence |
|---|---|---|
| Mapping package tests (correction head) | **VERIFIED IN ARENA** | `npm test` in `packages/mapping-config`, no workbook env: 217 tests, 210 pass, 0 fail, 7 skipped (Owner-local only). With `MAPPING_EXCEL_DEFAULT_PATH` set to a byte-identical copy outside the repository and no seed variable: 217 pass, 0 fail, 0 skipped. The predecessor 182-test figure belongs to the predecessor head. |
| UI shell tests (correction head) | **VERIFIED IN ARENA** | `npm test` in `apps/mapping-config`: 56 pass, 0 fail, with and without the workbook env (including reserved 11 and dark 19). The UI test servers use an explicit empty environment, so the synthetic-default tests do not depend on Owner-local variables. |
| Syntax checks | **VERIFIED IN ARENA** | `npm --prefix packages/mapping-config run check` and `npm --prefix apps/mapping-config run check`: exit 0. |
| Boundary scan (S1–S9) | **VERIFIED IN ARENA** | `node tools/boundary-scan/boundary-scan.mjs .`: 0 findings. |
| Whitespace in new files | **VERIFIED** | Grep for trailing whitespace and tabs: none. `git diff --check` on tracked files: empty. |
| Read-only and control scan | **VERIFIED** | UI test: button whitelist is exactly Undo, Redo, Reset. No non-GET request, no network API, no HTML injection sink. |
| Owner-local workbook topology | **VERIFIED IN ARENA AGAINST AN OUT-OF-REPO COPY** | Byte-identical copy (SHA-256 `4e0337e2…f422e8e`) outside the repository, removed afterwards. 23 modules in the expected sequence. Aggregate output only. |
| Authoritative default import (successor, no seed) | **VERIFIED IN ARENA AGAINST AN OUT-OF-REPO COPY** | Status VALID, 0 rack errors, 0 mapping errors. 26 enabled defaults with 0 `REQUIRED_TAG_MISSING`. 16 limits ACTIVE_WHEN_CLOSED (OWNER_RULE). AI-002 and AI-003 both 4-20 mA, 0–40 bar. 26 addresses ADDRESS_UNRESOLVED. Activation NOT AUTHORIZED, blocking reason `NO_VERIFIED_PROCESS_IMAGE_RULE`. 29 disabled read-only input listings. 18 placeholders unbound. Warnings only (profiles incomplete, IVn ranges unconfigured, placeholders pending). |
| Owner-local successor validation | **PASSED — Owner-reported at `433189f`** | Mapping 217/217 PASS; Mapping UI 56/56 PASS; boundary S1–S9 clean; PR-range `git diff --check` PASS. Arena re-ran the same commands at `433189f` and obtained the same totals, recorded separately and not claimed as the Owner's run. |
| Credentials audit of the workbook and repo diff | **VERIFIED** | See the Owner exception table above. |
| Locked restore, Release build, Runtime.Core and API tests, full .NET suite | **NOT VERIFIED** | The .NET SDK is absent in this sandbox. No .NET file is changed. |
| Fixture parity | **NOT VERIFIED** | No fixture changed. The parity run needs the .NET generator. |
| TypeScript checks | **NOT VERIFIED** | The TS package has no installed dependencies here. No TS file is changed. Installing them needs a STOP. |
| Browser rendering and layout | **OWNER BROWSER REVIEW PASSED WITH NON-BLOCKING UI PUNCHLIST (Owner-reported)** | There is no browser in the sandbox, so these are Owner-observed results, not Arena observations. Arena verified only through the stub DOM and server tests. |
| `git diff --check` and clean tree | **VERIFIED AT COMMIT** | `git diff --check` clean and no uncommitted change before the commit that carries this document. The tree is committed; see the commit log. |
| Full .NET regression | **NOT RUN AT `433189f`. Predecessor exact-head evidence only** | Owner-reported at predecessor `8847cfc`: Release build PASS; full .NET 414/414 PASS, failed 0, skipped 0. The Runtime graph and the Runtime Inspector have not changed since `8847cfc`: no .NET, Runtime, contract, fixture, Inspector or tools path differs between `8847cfc` and `433189f`. No .NET execution is claimed at `433189f`. |

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
9. `fix(mapping): complete authoritative workbook defaults` (`438cfd2`). 26 authoritative defaults from the workbook
   alone. Seeded `MAIN_VALVE_OUTLET_PRESSURE` refused as `UNKNOWN_TAG`. Reserved rows classified before the output test.
10. `fix(mapping): expose reserved unresolved inventory` (`09608bb`). Read-only reserved section, 18 rows. Analog rows
    show NOT APPLICABLE for polarity and contact.
11. `style(mapping): apply fixed dark presentation` (`64204e9`). Fixed Dark presentation and the dark-theme tests.
12. `docs(checkpoint): record mapping and UI correction` (`433189f`). Correction record. The loopback URL form in the
    reserved render test (boundary S1).

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
- OWNER BROWSER REVIEW PASSED WITH NON-BLOCKING UI PUNCHLIST (Owner-reported). NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED. NO PRODUCTION AUTHORIZED.
  PR #12 OPEN - NOT MERGED.

**Pump identity and source are locked; Slot and Channel are a Draft model API capability.** Pump Slot/Channel is supported by
the Draft model API and focused tests, but is not exposed as a browser editor control in Stage 0.4B-1. The browser
supports module reorder (drag/drop and keyboard), Undo, Redo and Reset only. Where the Draft API is used, a change is
accepted when the change is compatible, has a valid capacity, has no physical duplicate, is not shared with an IVn pressure, and has the
correct signal. The derived address is read-only and follows the change. Locked: `tagName`, canonical identity,
`sourceWorkbookTag`, `declaredSourceIdentity`, and the Pump-ready role. Swaps, AI-002 to Outlet, AI-003 to Inlet, a pump
moved onto an IVn source, and a shared channel are refused. The workbook description is source evidence and is not
identity. A description mismatch with a matching identifier raises no warning. An identifier conflict is still refused.

**Owner-local browser handoff (PowerShell, Windows, correction head).** The server binds the constant `127.0.0.1`
and reads the port from `MAPPING_UI_PORT` (default 5186). It takes the authoritative workbook from a byte-identical copy
outside the repository. No seed is created, and `MAPPING_BINDING_SEED_PATH` is unset. Run from the repository root.

```powershell
git status --porcelain                      # expect no output before you start
git fetch origin
git checkout arena/1b92c50a-waterjet-sentinel-suite
git pull --ff-only origin arena/1b92c50a-waterjet-sentinel-suite
git rev-parse HEAD                          # expect the correction head reported by Arena
node --version                              # expect v22.22.x or newer

$repo = (Get-Location).Path
$work = Join-Path $env:TEMP 'wjss-owner-review'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$copy = Join-Path $work 'T8_IO_Card_Mapping.xlsx'
Copy-Item -LiteralPath (Join-Path $repo 'T8_IO_Card_Mapping.xlsx') -Destination $copy -Force
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $copy).Hash
if ($hash -ne '4E0337E25C8377C01559F264653BAAB25BCFA23F4D3E071FDC8C80896F422E8E') { throw "SHA-256 mismatch: $hash" }
(Get-Item -LiteralPath $copy).Length        # expect 16648

if (Test-Path Env:MAPPING_BINDING_SEED_PATH) { Remove-Item Env:MAPPING_BINDING_SEED_PATH }
$env:MAPPING_EXCEL_DEFAULT_PATH = $copy
$env:MAPPING_UI_PORT = '5186'
node apps/mapping-config/server.mjs
# Open http://127.0.0.1:5186 in your browser. Loopback only.
# When the review is finished, press Ctrl+C in the server window, then run:
Remove-Item Env:MAPPING_EXCEL_DEFAULT_PATH -ErrorAction SilentlyContinue
Remove-Item Env:MAPPING_UI_PORT -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $copy -Force
git status --porcelain                      # expect no output
```

**Owner browser acceptance (38 items).** Classify the review as PASS, PASS WITH NON-BLOCKING PUNCHLIST, or FAIL/BLOCKING.
Report the result to Arena. Do not create an Owner UI closeout until the Owner reports.

*Dark theme*
1. The whole page is dark, with no light surface or text. 2. There is no theme selector, toggle, SYSTEM or LIGHT option.
3. A reload keeps the Dark appearance, and localStorage holds no key for this page (DevTools, Application).
4. DevTools Network shows one request at load (`/api/configuration`) and none on any click, Undo, Redo, Reset, drag or Alt+Arrow.
5. Body and table text are legible at 100% zoom. 6. Keyboard Tab shows a visible focus outline on buttons and rows.
7. At load, Undo, Redo and Reset are visibly disabled. 8. A refused move shows its message in the error colour.

*Topology*
9. The rack shows 23 modules in the expected sequence (RackSlot 1–23). 10. Coupler and End are fixed (not draggable).
11. Drag and drop reorders a movable module through the Draft. 12. Alt+Arrow reorders a movable module through the Draft.
13. Undo, Redo and Reset restore the expected order. 14. RackSlot and ProcessModulePosition are both shown, and differ where expected.
15. No rack row shows a numeric address. Each derived address reads ADDRESS UNRESOLVED.

*Enabled bindings*
16. The tag table shows 26 rows with Enabled = yes. The 29 listed read-only inputs show Enabled = no. 17. PUMP_INLET_PRESSURE (AI-002) is on AI-MODULE-01 channel 2, analog.
18. PUMP_OUTLET_PRESSURE (AI-003) is on AI-MODULE-01 channel 3, analog.
19. IV1–IV8 outlet pressure rows each show their module and channel, with NOT APPLICABLE polarity and contact.
20. The 8 Lower limits show ACTIVE_WHEN_CLOSED and NO. 21. The 8 Upper limits show ACTIVE_WHEN_CLOSED and NO.
22. No MAIN_VALVE_OUTLET_PRESSURE row exists. 23. Every enabled address reads ADDRESS UNRESOLVED.
24. Validation reads VALID, Activation ready: no, with NO_VERIFIED_PROCESS_IMAGE_RULE among the blocking reasons.

*Placeholders*
25. The heading reads "Reserved channels awaiting Owner identity (18)". 26. All 18 rows are rendered.
27. AI-020, AI-035, DI-037 and DO-031 are present. 28. DO-031 reads OUTPUT · NOT AUTHORIZED and NOT_AUTHORIZED_IN_READ_ONLY_STAGE.
29. Every row reads UNBOUND, OWNER_INPUT_PENDING, PROHIBITED and ADDRESS_UNRESOLVED.
30. Reserved rows, enabled rows, listed-not-enabled rows and the output row look different, as the legend describes.

*Presentation*
31. No analog row shows NOT SET or UNKNOWN. 32. ADDRESS UNRESOLVED is shown in the warning colour, never green.
33. The source summary reads 18 reserved placeholder rows, of them 1 an output (DO-031). 34. The legend matches what is shown.

*Boundary*
35. The URL is http://127.0.0.1:5186, and `Get-NetTCPConnection -LocalPort 5186` shows LocalAddress 127.0.0.1 only.
36. No connect, poll, read, write, force, command, activate, TEST_HARDWARE or production control is present.
37. The server was started with no `MAPPING_BINDING_SEED_PATH`, and no seed file was used.
38. After shutdown, the out-of-repository copy is deleted, the variables are unset, and `git status --porcelain` is empty.

**Seed-generation handoff (optional override, not required for defaults).** No seed generator is implemented in this
PR, and no new local script is added. If an authorised override is needed later, the handoff is: a deterministic
local-only command, output outside the repository, not committed, with no guessed identity and no numeric Modbus
address. The Owner must not hand-edit seed JSON. The command, output path, schema, binding count, identities by category,
cleanup command and proof that no repository file changed are reported before any Owner-run step. A new local script
requires a STOP and a report of its file and scope first.

**Known limits of the validated head.** The workbook is unchanged and remains the authoritative input. Arena did not run
.NET or TypeScript at `433189f`, and Arena made no browser run. The predecessor evidence (414/414, Owner-reported at
`8847cfc`) is not re-claimed for `433189f`. The Owner-local results recorded here are Owner-reported.

## Owner validation and Final Source Review (2026-10-10, documentation closeout)

**Validated code head:** `433189f9e489d7af6770e64c971585d99bf941ad`. **Parent of this record:** `433189f`. Branch
`arena/1b92c50a-waterjet-sentinel-suite`, base `a5540057ab668708a6129c3647685f82f1c5f08d` (`main`). PR #12 OPEN, NOT MERGED.

**Owner-local automated evidence (Owner-reported, not executed by Arena):**

| Gate | Result |
|---|---|
| Authoritative workbook | Copied byte-for-byte outside the repository; SHA-256 verified; no `MAPPING_BINDING_SEED_PATH`; the default loaded directly from the workbook. |
| Mapping syntax check | PASS |
| Mapping tests | **217 total, 217 passed, 0 failed, 0 skipped** |
| Mapping UI syntax check | PASS |
| Mapping UI tests | **56 total, 56 passed, 0 failed, 0 skipped** |
| Boundary scan | 0 findings; S1–S9 clean |
| PR-range whitespace, `git diff --check origin/main...HEAD` | PASS |
| Repository | No local source drift; final working tree CLEAN; no Owner-local artifact commit required |

Arena re-ran the same commands at `433189f` (not Owner evidence): mapping 217 total / 210 pass / 0 fail / 7 skipped without
the workbook path, and 217 / 217 / 0 / 0 with an out-of-repo byte-identical copy and no seed; UI 56 / 56 / 0 / 0 with and
without the workbook path; boundary 0 findings; `git diff --check origin/main...HEAD` exit 0; mapping and UI syntax checks
exit 0.

**Predecessor .NET evidence (Owner-reported at `8847cfc`, not re-executed):** Release build PASS; full .NET 414/414 PASS,
failed 0, skipped 0. This exact-head evidence covers the accepted Runtime Inspector wording, which was already present at
`8847cfc` (Arena verified the label is present there and is unchanged at `433189f`). No .NET execution is claimed at
`433189f`, and no new .NET run is required for this closeout.

**Owner browser review (Owner-reported, Owner-observed, not Arena-observed): PASS WITH NON-BLOCKING UI PUNCHLIST.**
Fixed Dark theme PASS. No theme selector or Light/System mode PASS. Authoritative workbook loaded as DEFAULT FROM EXCEL
PASS. Topology of 23 physical rack Slots PASS. RackSlot and ProcessModulePosition displayed separately PASS. Coupler and
End Module shown as fixed topology boundaries PASS. 26 enabled authoritative default bindings PASS. AI-002 Pump Inlet
Pressure shown separately PASS. AI-003 Pump Outlet Pressure shown separately PASS. No Main Valve pressure tag PASS. Pump
and IV pressure identities separate PASS. IV1–IV8 pressure, Lower and Upper ordinal mapping PASS. Analog polarity and
contact NOT APPLICABLE PASS. Digital limits ACTIVE_WHEN_CLOSED / NO PASS. 18 reserved channels visible PASS. AI-020, AI-035,
DI-037 and DO-031 visible PASS. DO-031 remains OUTPUT and NOT_AUTHORIZED_IN_READ_ONLY_STAGE PASS. Placeholder rows
UNRESOLVED / UNBOUND / OWNER_INPUT_PENDING PASS, and not auto-bindable PASS. No numeric address displayed PASS. Every address
ADDRESS_UNRESOLVED PASS. Module-profile and engineering-range warnings visible PASS. Activation NOT AUTHORIZED PASS. No
MODBUS control, no hardware activation, no write control PASS. Loopback-only browser path used PASS.

**Boundary scanner (`tools/boundary-scan/boundary-scan.mjs`): PASS. AUTHORIZED B1 REGRESSION PROTECTION. IN SCOPE.** The
narrow S6 rule detects Product code that reads an environment variable for a bind host. The Owner records that it detected
the earlier `MAPPING_UI_HOST` defect, avoids unrelated environment-variable false positives, preserves S6, and gives 0
findings on the corrected Product tree. The rule stays in place and is not restored to `main`.

**Runtime Inspector wording (`apps/runtime/Inspector/index.html`): PASS. OWNER-APPROVED NARROW PRESENTATION EXCEPTION.
NON-BLOCKING.** The two-line change labels the pump pressures "Pump Outlet pressure" in place of the generic "Pump pressure"
wording, aligned with AI-002 Pump Inlet Pressure, AI-003 Pump Outlet Pressure, and Pump-ready AI-003 / PUMP_OUTLET only. It
changes presentation only. It does not publish AI-002, change Runtime state or Delta or contracts or sequencing, add alarm,
interlock or control behaviour, or add device access or write authority. It is not restored to `main`.

**Final Source Review verdict: STAGE 0.4B-1 FINAL SOURCE REVIEW PASSED. BLOCKING DEFECTS NONE.** The review was static.
Item 66 (no Runtime, contract or fixture behaviour changed) is now PASS: no Runtime, contract or fixture path changed, and the
Inspector change is presentation only. Earlier, item 66 was BLOCKING on scope; the Owner decision above resolved it.

**Non-blocking follow-ups (recorded, not implemented):**

- **A. Pump Slot/Channel wording: NON-BLOCKING CAPABILITY/PRESENTATION CLARIFICATION.** `DraftSession.setBinding` supports
  compatible Pump Slot/Channel editing and focused tests cover it. The browser UI has no Slot/Channel binding editor. The
  browser supports module reorder (drag/drop and keyboard), Undo, Redo and Reset only. This wording is corrected in this
  record and in the PR body.
- **B. SPARE-versus-placeholder ordering: NON-BLOCKING FUTURE HARDENING.** The import checks `SPARE` before the placeholder
  test. A placeholder row marked SPARE would be counted as spare rather than reserved. The current authoritative workbook
  has no such row; all 18 reserved rows classify correctly. The importer is not changed in this closeout.
- **C. Draft API create-on-missing: NON-BLOCKING FUTURE API HARDENING.** `DraftSession.setBinding` creates a binding when the
  tag name is not found. The browser does not call this path, and catalogue and validation rules refuse unknown identities.
  Product code is not changed in this closeout.
- **D. UI readability: NON-BLOCKING UI READABILITY PUNCHLIST.** Dense tables, long unresolved-address reasons, and an import
  summary that could be easier to scan. Not implemented.

**Stated scope and status:**

- AI-002 Runtime publication is **NOT IMPLEMENTED**.
- Addresses remain **ADDRESS_UNRESOLVED**. Process-image profiles remain unverified.
- Activation is **NOT READY**. Activation is not authorized. `NO_VERIFIED_PROCESS_IMAGE_RULE` remains a blocking reason.
- **NO MODBUS** is implemented. No polling, device read, device write or device control.
- **TEST_HARDWARE is NOT AUTHORIZED.** **PRODUCTION is NOT AUTHORIZED.** No device write or control exists.
- The workbook `T8_IO_Card_Mapping.xlsx` is unchanged: SHA-256 `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`,
  16,648 bytes, blob `53bfd7abf1d6965d1f118f8d2003854e846097cf`.
- The working tree is clean at this record's commit.

**Honesty lines:** OWNER-LOCAL MAPPING 217/217 PASSED. OWNER-LOCAL MAPPING UI 56/56 PASSED. OWNER BROWSER REVIEW PASSED WITH
NON-BLOCKING PUNCHLIST. BOUNDARY SCANNER CHANGE AUTHORIZED IN B1 SCOPE. RUNTIME INSPECTOR WORDING ACCEPTED AS NARROW
PRESENTATION EXCEPTION. FINAL SOURCE REVIEW STATICALLY PERFORMED. BLOCKING DEFECTS NONE. AI-002 RUNTIME PUBLICATION NOT
IMPLEMENTED. ADDRESSES REMAIN ADDRESS_UNRESOLVED. ACTIVATION NOT READY. NO MODBUS IMPLEMENTED. NO TEST_HARDWARE AUTHORIZED.
NO PRODUCTION AUTHORIZED. PR #12 OPEN - NOT MERGED.
