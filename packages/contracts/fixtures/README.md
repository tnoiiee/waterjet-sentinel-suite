# Contract golden fixtures — PROVISIONAL

| File | Purpose |
| --- | --- |
| `snapshot.seed0.json` | Full `wjss.snapshot/1` projection (108-slot wall map, 106 Sensors, bounded queue, Active Job mid-Safe-Return, health, trend) |
| `delta.basic.json` | `wjss.delta/1` gapless delta (`previousRevision` 10 → 11), sparse keys only |
| `delta.gap.json` | `wjss.delta/1` gap fixture (7 → 12): structurally valid, must be detected as a GAP (consumer re-snapshots) |
| `command.refusal.json` | `CommandOutcome` refusal envelope sample (`RUNTIME_NOT_IMPLEMENTED`) |

## Status of these files

**.NET-GENERATED GOLDEN FIXTURES — Owner-local validation PASSED (2026-10-07).**

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
