# Contract golden fixtures — PROVISIONAL

| File | Purpose |
| --- | --- |
| `snapshot.seed0.json` | Full `wjss.snapshot/1` projection (108-slot wall map, 106 Sensors, bounded queue, Active Job mid-Safe-Return, health, trend) |
| `delta.basic.json` | `wjss.delta/1` gapless delta (`previousRevision` 10 → 11), sparse keys only |
| `delta.gap.json` | `wjss.delta/1` gap fixture (7 → 12): structurally valid, must be detected as a GAP (consumer re-snapshots) |
| `command.refusal.json` | `CommandOutcome` refusal envelope sample (`RUNTIME_NOT_IMPLEMENTED`) |

## Status of these files

**PROVISIONAL STRUCTURAL FIXTURES — OWNER-LOCAL .NET GENERATION REQUIRED.**

- Authored in Arena by a deterministic Node script (the Arena sandbox has no
  .NET SDK); every file carries `"fixtureStatus"` with the marker above.
- C# records in `../` are authoritative. The .NET generator
  (`tests/integration/FixtureGenerator.cs`) regenerates these files Owner-local;
  on the first Owner-local run it MUST produce semantically equal JSON. If it
  differs, the regenerated files replace these in a review-correction commit
  and the TypeScript validator run re-confirms them.
- No claim is made here that .NET generation has run or that parity has
  passed — that evidence comes from the Owner-local validation document
  (`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`, steps 10–11).
- Round 6 (2026-10-07): the sensor-map example shape changed — `tcChannels`
  is a structured array of exactly two channel strings per sensor
  (ADR-0014 decision 8). `config/examples/sensor-map.example.json` was
  regenerated in the array form in Arena for TypeScript validation only; the
  preceding comma-delimited form (and the snapshot/delta files that drifted
  against it) is superseded evidence for the Owner regeneration sequence in
  the validation document §5 — regeneration stays gated behind a successful
  full Release build and passing non-parity tests.

## Rules the fixtures demonstrate (and the validator enforces)

- 106 Sensors / 212 unique thermocouple channel identities / 108 wall slots.
- Cannon slots at logical I7 / I16 are equipment, never Sensors.
- GlobalQueue: capacity 8, contiguous positions from 1, no hidden overflow,
  head-only dispatch (`positionBefore` always 1, revision +1).
- Mandatory Safe Return evidence ordering: valve command < valve confirmed <
  axis command < Standby confirmed (and the `lastJobOutcome` chain through
  release); strictly increasing event indices.
- Delta sparseness: absent key = unchanged; `activeJob: null` is invalid;
  `wallMap` never appears in a Delta.
- Only synthetic, labelled values; no Production addresses, registers,
  coordinates, thresholds, or timings (these are fixtures, not configuration).
