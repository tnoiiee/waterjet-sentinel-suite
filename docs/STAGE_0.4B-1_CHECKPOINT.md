# Stage 0.4B-1 — Simulation-Only Mapping Configuration (Development Checkpoint)

Status: **DEVELOPMENT CHECKPOINT. Committed and pushed to the session branch; a PR targeting `main` is opened, not merged.**
Stop point: this checkpoint. Nothing is merged.

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
| `packages/mapping-config/src/tagCatalogue.mjs` | Simulation/Runtime tag identities; pump range only; `pressureTagForWj(n)` returns IVn only. |
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
| `apps/mapping-config/server.mjs` | GET-only host. Binds to loopback. Excel path optional, local, env-gated, outside the repo. |
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
- **Pressure identities.** PUMP_OUTLET_PRESSURE and IV1–IV8_OUTLET_PRESSURE are separate. No aliasing,
  averaging or cross-fallback. WJn uses the IVn pressure (`pressureTagForWj`).
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
- **Provider.** SIMULATOR only (the constant in code; the spec's "SIMULATION" is the same provider; name not yet reconciled). The package performs no I/O and holds no credentials. It has no
  Modbus, TCP or device code. Draft state does not feed Runtime or RuntimePublication.
- **Revisions.** Deterministic. Independent of timestamps, object key order, array order of bindings,
  and UI state. Verified by tests.
- **Impact preview.** Classification precedence: BINDING_INVALID > BLOCKED > ADDRESS_CHANGED > MOVED >
  ADDRESS_UNRESOLVED > UNCHANGED.
- **Workbook import.** Reports sheet, rows, counts (USED/SPARE), slots, models, channels, duplicates,
  blank or malformed values, ambiguous inputs, placeholder rows and model-profile gaps. It never repairs
  silently. Placeholder rows stay USED, are never bound, and raise `SIGNAL_IDENTITY_UNRESOLVED` and
  `OWNER_INPUT_PENDING`. Output placeholders are classified as outputs first, so their output
  classification takes precedence over the reserved-row count.
- **UI.** Header, rack layout (Draft drag-and-drop, plus Alt+Arrow keyboard reorder), Undo, Redo, Reset,
  tag mapping (read-only), validation, impact preview and revisions. Controls are limited to Undo, Redo
  and Reset. There is no Connect, Poll, Read, Write, Force, command, Activate, TEST_HARDWARE or Production control.

## Validation

| Gate | Result | Evidence |
|---|---|---|
| Mapping package tests | **VERIFIED** | `npm test` in `packages/mapping-config`: 113 tests, 112 pass, 1 skipped (Owner-local check below). Includes the IVn label checks and the limit polarity tests. |
| UI shell tests | **VERIFIED** | `npm test` in `apps/mapping-config`: 17 pass. Includes a stub-DOM run of the real `app.mjs` against the real server. |
| Syntax checks | **VERIFIED** | `node --check` on every mapping module, `server.mjs` and `public/app.mjs`. |
| Boundary scan (S1–S9) | **VERIFIED** | `node tools/boundary-scan/boundary-scan.mjs`: 0 findings. |
| Whitespace in new files | **VERIFIED** | Grep for trailing whitespace and tabs: none. `git diff --check` on tracked files: empty. |
| Read-only and control scan | **VERIFIED** | UI test: button whitelist is exactly Undo, Redo, Reset. No non-GET request, no network API, no HTML injection sink. |
| Owner-local workbook topology | **VERIFIED LOCALLY, NOT IN REPO** | Run in the sandbox against the attached workbook: 23 modules in the expected sequence. Output is aggregate only. |
| Default import validation | **VERIFIED LOCALLY** | Owner-stated bindings and the Owner polarity rule: status VALID, 0 errors, 16 limits ACTIVE_WHEN_CLOSED (OWNER_RULE), 26 unresolved addresses, activation not ready. Blocking reason: NO_VERIFIED_PROCESS_IMAGE_RULE. Warnings only (profiles incomplete, ranges unconfigured). |
| Credentials audit of the workbook and repo diff | **VERIFIED** | See the Owner exception table above. |
| Locked restore, Release build, Runtime.Core and API tests, full .NET suite | **NOT VERIFIED** | The .NET SDK is absent in this sandbox. No .NET file is changed. |
| Fixture parity | **NOT VERIFIED** | No fixture changed. The parity run needs the .NET generator. |
| TypeScript checks | **NOT VERIFIED** | The TS package has no installed dependencies here. No TS file is changed. Installing them needs a STOP. |
| Browser rendering and layout | **NOT VERIFIED** | No browser in the sandbox. Verified only through the stub DOM and server tests. |
| `git diff --check` and clean tree | **VERIFIED AT COMMIT** | `git diff --check` clean and no uncommitted change before the commit that carries this document. The tree is committed; see the commit log. |

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

## Known limitations

- No verified process-image rule. Every address is unresolved by design until the 750-362 process-image
  rule and each module's process-data profile are verified from primary evidence or a WAGO-IO-CHECK export.
- Analog status-byte setting is not verified.
- Engineering ranges are UNCONFIGURED except the pump range (0–40 bar, Owner domain information).
- The browser UI has not been run in a real browser.
- The local preview is not published. The repository boundary rule S6 forbids any-interface bind
  instructions in the Product tree, so the server binds to loopback only. A preview that needs an
  all-interface bind conflicts with that rule and needs the Owner's decision.
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

The workbook is not added by any of these commits. It arrived in `6ff9e4a` under the exception above.
The PR targets `main`, is not merged, and does not reuse PR #11's branch.
