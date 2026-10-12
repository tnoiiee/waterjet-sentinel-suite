# Contract golden fixtures — PROVISIONAL

| File | Purpose |
| --- | --- |
| `snapshot.seed0.json` | Full `wjss.snapshot/1` projection (108-slot wall map, bounded queue, Active Job mid-Safe-Return, health, trend). **STALE TOPOLOGY — Owner-local .NET regeneration pending:** the committed file still carries the superseded 106-Sensor / 212-channel form (I7, I16 as `NON_SENSOR_GAP`; WJ1/WJ3 `placementKind: NON_SENSOR_GAP`). See "Owner critical topology correction" below. |
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
- `snapshot.seed0.json` is **not** regenerated in Arena: it is the .NET generator's output and
  the .NET SDK is unavailable in Arena. It is deliberately left as committed (not hand-edited)
  and is therefore **stale**. The TypeScript validator (`../wjss-contracts-ts/validate.mjs`) now
  enforces the corrected baseline and **rejects** this file; the test
  `snapshot.seed0.json passes validation (requires the Owner-local .NET regeneration …)` fails
  by design until the Owner regenerates it (§5 runbook gate). Do not weaken the validator or the
  test to accept the stale file.
- The replacement placement vocabulary for WJ1 and WJ3 (previously "at the I16 / I7 gap") is an
  open Owner decision; neither the validator nor the fixture invents it. The validator only
  refuses `placementKind: NON_SENSOR_GAP`.

## Status of these files

**.NET-GENERATED GOLDEN FIXTURES — Owner-local validation PASSED (2026-10-07) against the
superseded 106-Sensor baseline; regeneration to the 108-Sensor baseline is pending.**

- These committed files are the genuine .NET generator output, transferred from
  the Owner's validated Working Tree (handoff commit `488b98fb…`) — not
  Arena-authored substitutes. Parity (`FixtureParityTests`) passed 7/7 against
  them and the TypeScript validator accepts them (24/24).
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
