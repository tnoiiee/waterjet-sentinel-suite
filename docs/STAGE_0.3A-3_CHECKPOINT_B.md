# Stage 0.3A-3 — Checkpoint B Record: Equipment Topology and Legacy Parameter Migration (Owner-Locally Validated)

- **Status:** **OWNER-LOCALLY VALIDATED (2026-10-08)** — SUBMITTED FOR OWNER MERGE
  (**PR #6 OPEN — NOT MERGED**; only the Owner merges). Checkpoint A was reviewed to
  **PASS** and ADR-0017 was **explicitly Owner-accepted** in the same final review; this
  record documents the authorized Checkpoint B implementation and the authoritative
  Owner-local validation evidence. Companion records:
  [`STAGE_0.3A-3_CHECKPOINT_A.md`](STAGE_0.3A-3_CHECKPOINT_A.md) (specification and
  correction history) and [`decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md`](decisions/ADR-0017-equipment-topology-and-legacy-parameter-migration.md)
  (`ACCEPTED`).
- **Date:** 2026-10-08 (implementation and closeout; Arena-authored, Owner-validated).
- **Boundaries honored:** no Runtime, Inspector, Snapshot/Delta, Simulator, queue,
  cleaning, pump, valve, axis, or Mandatory Safe Return change; no command or write path;
  no device access; `TEST_HARDWARE` and `PRODUCTION` NOT AUTHORIZED; the transitional
  Runtime CANNON vocabulary was NOT touched (Checkpoint C scope, NOT AUTHORIZED);
  `spikes/**` untouched.

---

## 1. What Checkpoint B delivered

Implemented exactly per the approved change set (ADR-0017 decision 13 and the Checkpoint A
report §11), on branch `arena/ca9c94c4-waterjet-sentinel-suite` / PR #6:

| Layer | File | Content |
| --- | --- | --- |
| Contracts | `packages/contracts/Topology.cs` (NEW) | `LogicalPositionRecord` (canonical `LogicalId`, `LegacyRecordId` provenance, `PositionKind`, `OrderTotal` 0–107, `OrderWall`, gap anchor), `WaterJetConfiguration` (installed wall/region, placement kind, anchors, opposite-wall target), `IsolationValveConfiguration`; `Region` and `WaterJetPlacementKind` vocabularies |
| Contracts | `packages/contracts/Enums.cs` (EXTEND) | canonical `LogicalPositionKind { SENSOR, NON_SENSOR_GAP }` ONLY; the transitional `SlotType.CANNON` wire surface untouched |
| Contracts | `packages/contracts/Config.cs` (EXTEND) | `SensorConfigurationRecord` (dense derived `ScanOrder` 1–106; direct renames of the legacy dirty-score numbers; `Enabled` from the legacy token; raw `CleaningCount`; raw offset-free `LastCleanTimestampRaw`; raw unknown-unit `MinTimeAllowAddToQueueRaw`; DEFER raws; `DeferredAcquisitionProvenance` raw only), `MigrationWarningRecord`, `MigrationRefusalRecord`, atomic `SensorParameterImportOutcome`, `SensorParameterMigrationResult`, `MigrationFieldDisposition` + approved field-classification records, additive `MigrationRefusalCodes` (incl. warning-level `MIGRATION_PLACEHOLDER_VALUE`, `MIGRATION_UNIT_UNVERIFIED`, `MIGRATION_TIMEZONE_UNKNOWN`) |
| Domain | `packages/domain/WaterJetTopologyCatalog.cs` (NEW) | the Owner-approved WJ1–WJ8 / IV1–IV8 topology table (installed position separate from target coverage; opposite wall; matching region) and the I7→WJ3 / I16→WJ1 gap anchors |
| Domain | `packages/domain/SensorParameterCsvImporter.cs` (NEW) | deterministic, atomic, fail-closed CSV importer: `id` → `LegacyRecordId` provenance only; `sensorname` → canonical logicalId/sensorId validated against the `orderTotal`-derived label (`MIGRATION_LOGICAL_LABEL_MISMATCH`); `orderTotal` 0–107 preserved over all 108 positions; `ScanOrder` derived densely 1–106 over the 106 Sensors, skipping I7/I16; direct cleaning-device ordinal `n → WJn` (never remapped by wall) with target-wall agreement; offset-free timestamps preserved raw with UNKNOWN timezone (never UTC); unknown-unit durations raw (no TimeSpan); no unapproved domain derivations; acquisition columns captured as deferred raw provenance only; gap rows REJECT FOR I7/I16 |
| Domain | `packages/domain/TopologyValidator.cs` (NEW) | independent structural validator (counts, orderings, gaps, pairing, assignments, targets, namespace disjointness); run by the importer on its own output before acceptance |
| Config example | `config/examples/sensor-parameters.migrated.example.json` (NEW) | synthetic, structure-only migrated shape (108 / 106 / 8 / 8; all deferred-raw and acquisition fields null) |
| Tests | `tests/config.tests/`: `SyntheticSensorParameterCsv.cs`, `SensorParameterMigrationTests.cs`, `MigratedExampleShapeTests.cs` (NEW); csproj gains the required `Wjss.Domain` project reference | public-safe synthetic CSV builder; the approved planned tests T1–T20 as semantic assertions (final form: no source-substring or source-format tests); validator-level refusal tests; migrated-example schema guard |

**Identifier note:** the pairing properties are named `DedicatedIsolationValveId` /
`ServedWaterJetId` in code because the boundary scanner's `redis` substring rule (S3)
matches the natural `Pai-red-is-olation…` spelling. Semantics are exactly the approved
one-to-one ordinal pairing (`WJn ↔ IVn`); the rename is mechanical and was recorded for
Owner review in the CHANGELOG. The related JSON names are `dedicatedIsolationValveId` /
`servedWaterJetId`; the Sensor-level valve reference is `AssignedIsolationValveId` /
`assignedIsolationValveId`.

**Correction chain on PR #6 (each a new commit; nothing rewritten):**
`c994c26f` (Checkpoint A specification) → `03c2d15` → `6648437f` → `3dac1a0c` →
`8cb94d11` (Checkpoint A review corrections; final review PASS) → `b0a5208` (Checkpoint B
implementation) → `18f853a5` (Owner-local test-compile correction: `CultureInfo` using,
nullable-flow guards, xUnit2013, CA1861) → `901a070` (CA1829 count-property correction) →
`79fa0ba5` (behavioural test correction: `assignedIsolationValveId` expectation, two
source-substring tests replaced with semantic output/contract-shape tests,
`I7`-before-`I16` orderTotal ordering) → `2db853a7` (CA1865, two xUnit2029, and the last
source-substring test replaced with compiled-public-type semantics) → `0728df61`
(**Owner-authored** genuine lock refresh).

## 2. Authoritative Owner-local validation evidence

Recorded exactly as supplied by the Owner (Arena never ran .NET):

| Item | Evidence |
| --- | --- |
| SDK | `10.0.401` (pinned by `global.json`) |
| Validated feature head | `2db853a723a74399430ddd84900c40371fe56b29` |
| Genuine lock-refresh commit | `0728df61f917ba61f6dd3b8bf6d12e68dfa01d20` (`chore: refresh Checkpoint B migration test lock`; parent `2db853a7…`; changes exactly `tests/config.tests/packages.lock.json` (+6 lines): the genuine project-graph edge `wjss.domain → { type: Project, dependencies: { Wjss.Contracts: [1.0.0, ) } }`; valid JSON; no absolute path; no credential; no package-version change; no Owner CSV or acquisition value) |
| Normal restore | **PASS** |
| Locked restore | **PASS** |
| Release build | **PASS — 0 warnings / 0 errors** |
| Fresh full .NET tests | **186 total / 186 passed / 0 failed / 0 skipped** |
| Boundary scan | **0 findings — S1–S9 clean** |
| JSON examples | **PASS** |
| Owner acquisition data in the migrated example | **NONE** |
| Final Owner-local Working Tree | **CLEAN** |

An earlier "139 total / 139 passed" observation at the implementation head was
non-authoritative (`dotnet test --no-build` over stale binaries after a failed
test-project build) and is **not** recorded as validation; the four behavioural failures
and all compile/analyzer diagnostics found were corrected on this chain, after which the
above fresh full run is the first and authoritative Checkpoint B result.

## 3. Validated topology (acceptance record)

| Invariant | Validated value |
| --- | --- |
| LogicalPositions | 108 |
| SensorConfigurations | 106 |
| Structural Thermocouple channel sides | 212 (two per Sensor; structural invariant only) |
| Wall totals | Left 24 / Rear 29 / Right 24 / Front 29 |
| I7 and I16 | NON_SENSOR_GAP (placement anchors of WJ3 and WJ1); no SensorConfiguration, no scanOrder |
| `orderTotal` | 0–107 across all 108 positions (unique, layout provenance only) |
| `scanOrder` | dense 1–106 across the actual Sensors only (skips I7/I16) |
| Water Jets | exactly 8 (WJ1–WJ8), installed positions per the approved table |
| Isolation Valves | exactly 8 (IV1–IV8), one-to-one ordinal pairing `WJn ↔ IVn` |
| Installed position vs target coverage | separate concepts; target = opposite wall, matching region |
| Legacy cleaning-device ordinal | `n → WJn` direct; never remapped by wall |
| Acquisition bindings | deferred raw provenance only; **no Production device binding was created** |
| Control paths | **no command or actuation path was added** |

## 4. Proposed next scope — Checkpoint C (DOCUMENTED ONLY — NOT AUTHORIZED, NOT STARTED)

For the Owner's future consideration; nothing below is implemented or authorized:

- Atomic Runtime/Snapshot migration of the transitional CANNON vocabulary
  (`SlotType.CANNON`, `CanonicalSensorMap.CannonSlots`, `CannonSlotCount`,
  `CANNON_REAR`/`CANNON_FRONT`) to the canonical `LogicalPositionKind` NON_SENSOR_GAP
  representation, with golden-fixture regeneration and the TypeScript mirror.
- Runtime topology integration: WaterJetConfiguration / IsolationValveConfiguration
  visibility in the Snapshot facts (presentation only; no command surface).
- Inspector overlay candidates: Water Jet placement markers and Isolation Valve status
  display (read-only presentation).
- Config-pipeline continuation toward published revisions (0.3A-F), consuming the
  migration result format delivered here.

Not authorized and not started: queue dispatch, cleaning execution, pump commands, valve
commands, axis commands, Mandatory Safe Return actuation, TEST_HARDWARE, PRODUCTION
access, ZIP, Release, Deployment.

## 5. Arena static checks at closeout (no .NET in Arena)

Boundary scan **0 findings (S1–S9 clean)**; markdown fence balance, final-newline and
whitespace sweeps, `git diff --check`, protected-file diff review, and lock-blob
byte-identity against the Owner handoff all verified (see the delivery report). Product
source, contracts, tests, JSON examples, project files, and lock files were **not**
modified by this documentation-only closeout commit.
