# Contract golden fixtures — PROVISIONAL

| File | Purpose |
| --- | --- |
| `snapshot.seed0.json` | Full `wjss.snapshot/2` projection (108-slot wall map — 108 Sensor slots, I7 and I16 included; 216 channels; bounded queue; Active Job mid-Safe-Return; health; trend). **Regenerated 2026-10-12 to the corrected baseline** with `tools/fixture-generator/generate.mjs` (byte-exact Node port of `tests/integration/FixtureGenerator.cs`; see "Owner critical topology correction" below). Owner-local C# parity gate (`FixtureParityTests`) **PASSED** at PR #21 head `041b2863…` (7 / 7, no diff, repeated 7 / 7 no diff). |
| `delta.basic.json` | `wjss.delta/1` gapless delta (`previousRevision` 10 → 11), sparse keys only |
| `delta.gap.json` | `wjss.delta/1` gap fixture (7 → 12): structurally valid, must be detected as a GAP (consumer re-snapshots) |
| `command.refusal.json` | `CommandOutcome` refusal envelope sample (`RUNTIME_NOT_IMPLEMENTED`) |

## Owner critical topology correction (current baseline)

Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors.
The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements
(240 physical TC channels, 24 unassigned). Consequences for this folder:

- `config/examples/sensor-map.example.json` has been regenerated to the corrected baseline
  (108 `SENSOR` slots, 216 channels, no `NON_SENSOR_GAP`, `sensorsPerWall` 24/30/24/30) with
  `tools/sensor-map-example/generate.mjs`, a byte-exact Node port of
  `tests/integration/ExampleFiles.cs::SensorMap`; the C# generator must adopt the same form.
- `snapshot.seed0.json` **has been regenerated** (K3 Completion Pass, 2026-10-12) with
  `tools/fixture-generator/generate.mjs`, a Node port of the pure-formula C# generator
  `tests/integration/FixtureGenerator.cs` (no randomness; `SyntheticSeed` is a label only).
  Proof chain, all reproducible from the repository root with Node ≥ 22:
  - `node tools/fixture-generator/generate.mjs --legacy-proof` — the port, run in its superseded
    106-Sensor profile, reproduces the previously committed file **byte-identically**
    (read-only check; proves the port is faithful before any correction is applied);
  - `node tools/fixture-generator/generate.mjs --write` — emits the corrected 108-Sensor form
    (108 `SENSOR` slots, 216 channels, I7 scan 79 / I16 scan 88, no `gapAnchorForWaterJetId`,
    WJ1 / WJ3 `placementKind: UNRESOLVED` with `placementAnchors: []`, wall summaries recomputed
    over 24 / 30 / 24 / 30 Sensors); a second run produces **no diff** (deterministic);
  - `node --test ../wjss-contracts-ts/test/validate.test.mjs` — the TypeScript validator accepts
    the regenerated file (36 / 0; the golden test is no longer a by-design failure);
  - `delta.basic.json` and `delta.gap.json` are unchanged: their Sensor and runtime projections
    carry no topology-dependent field and were re-derived identically by the port.
  The C# generator (`FixtureGenerator.cs`) was corrected to the same form; the Owner-local
  `FixtureParityTests` run (`WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/…`) **PASSED**
  at PR #21 head `041b2863…` — 7 / 7 with no Git diff, and 7 / 7 with no additional diff on the
  deterministic second run (`docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md` §13). The Arena pass
  itself could not run .NET; the parity proof is the Owner-local record. The file was never
  hand-edited.
- WJ1 / WJ3 installed position (previously "at the I16 / I7 gap") is **UNRESOLVED** by Owner
  ruling: `placementKind: UNRESOLVED`, `placementAnchors: []`, operator wording "Installed
  position pending authoritative layout". Nothing is inferred from I7, I16, a former gap, a
  Sensor wall, a target wall, a target Sensor, a legacy cannon ordinal or an Assigned Cleaning
  Device ID. Target Coverage (WJ1 → REAR LOWER, WJ3 → FRONT LOWER) is unchanged. The validator
  accepts `UNRESOLVED` only with an empty anchor list and refuses `NON_SENSOR_GAP`.

## Status of these files

**GOLDEN FIXTURES — `snapshot.seed0.json` regenerated 2026-10-12 to the 108-Sensor baseline
via the proven Node port of the .NET generator; Owner-local .NET parity re-validation PASSED
at `041b2863…` (fixture emission 7 / 7, no diff, twice; full .NET 455 / 455; contracts-ts
36 / 36). Schema `wjss.snapshot/2` retained (OQ-T3 RESOLVED — RETAIN_SCHEMA_V2). The earlier
Owner-local validation (2026-10-07) PASSED against the superseded 106-Sensor baseline and is
historical.**

- `delta.basic.json`, `delta.gap.json` and `command.refusal.json` are the genuine .NET
  generator output, transferred from the Owner's validated Working Tree (handoff commit
  `488b98fb…`) and unchanged since. `snapshot.seed0.json` is the output of the byte-exact Node
  port of the same generator in its corrected form (see above). Historical: parity
  (`FixtureParityTests`) passed 7/7 and the TypeScript validator accepted 24/24 against the
  superseded form; the current TypeScript result is 36/0.
- The in-file `"fixtureStatus"` marker still reads `PROVISIONAL STRUCTURAL
  FIXTURE …` because the generator constant (`FixtureGenerator.FixtureStatus`)
  writes that string; removing the marker means amending the generator constant
  and is a later Owner decision — until then the label is a generator-emitted
  stage marker, not a statement that .NET generation is outstanding.
- C# records in `../` remain authoritative; regeneration for any contract
  change follows the runbook §5 gate (full Release build + non-parity tests
  green first). Evidence: `docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md` §10.
- History (2026-10-07, rounds 6–6d): the sensor-map example shape changed —
  `tcChannels` became a structured array of exactly two channel strings per
  sensor (ADR-0014 decision 8); the interim comma-delimited form and the
  Arena-side array conversion are superseded evidence. At closeout all three
  files above (plus the example) are the Owner-validated .NET-generated
  versions — the Arena conversion was replaced by the genuine transfer.

## Rules the fixtures demonstrate (and the validator enforces)

- 108 logical positions = 108 Product Sensors (I7 and I16 included, each exactly once in its
  ordinal position), with 216 unique thermocouple channel identities (two per Sensor: Front and
  Rear). No `NON_SENSOR_GAP` position exists.
  - Historical (superseded): "108 = 106 Sensors + 2 `NON_SENSOR_GAP` positions (I7 → WJ3,
    I16 → WJ1), 212 channels". Superseded by the Owner critical topology correction: I7 and I16
    are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216
    assigned Product TC measurements.
- GlobalQueue: capacity 8, contiguous positions from 1, no hidden overflow,
  head-only dispatch (`positionBefore` always 1, revision +1).
- Mandatory Safe Return evidence ordering: valve command < valve confirmed <
  axis command < Standby confirmed (and the `lastJobOutcome` chain through
  release); strictly increasing event indices.
- Delta sparseness: absent key = unchanged; `activeJob: null` is invalid;
  `wallMap` never appears in a Delta.
- Only synthetic, labelled values; no Production addresses, registers,
  coordinates, thresholds, or timings (these are fixtures, not configuration).
