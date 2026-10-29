# config/examples — public-safe examples only

These files document the SHAPE of Published Configuration. They are:

- **synthetic** — every value is labelled and obviously fake;
- **incomplete by design** — they never enumerate device data, timing policy,
  or production parameters;
- **never a deployment starting point** — real configuration lives outside the
  Git working tree, per `docs/PUBLIC_REPOSITORY_BOUNDARY.md` and ADR-0011.

Prohibited in this directory (enforced by `tools/boundary-scan`, rules S2/S3):
production device addresses, device-point register-to-signal mappings, tag
inventories, motion coordinates, mechanical stroke limits, speed/pressure
values, production thresholds, credentials, site identifiers, and any
`deviceProfile` other than `SIMULATOR`.

The publication/validation pipeline (Draft vs Published revisions, validation
blocking, immutable in-memory snapshot, application state gates) is Stage
0.3A-F content and is NOT implemented by these examples.

Since the 2026-10-07 closeout these files are the .NET generator's own output
(`tests/integration/ExampleFiles.cs`, transferred via the Owner handoff) — the
sensor-map example's `tcChannels` is the exact two-entry array contract of
ADR-0014 decision 8, and TypeScript validation of the shape passes 24/24.

**Owner critical topology correction (2026-10-12).** The authoritative baseline is
108 Product Sensors (I7 and I16 are normal Sensors), 216 assigned Product TC
measurements, 240 physical TC channels, 24 unassigned.

- `sensor-map.example.json` has been **regenerated** to that baseline (108 `SENSOR`
  slots, no `NON_SENSOR_GAP`, 216 channels, `sensorsPerWall` 24/30/24/30) with
  `tools/sensor-map-example/generate.mjs` (`--write`), a byte-exact Node port of
  `tests/integration/ExampleFiles.cs::SensorMap`; `--legacy-proof` reproduces the
  superseded 106 form for comparison only. The C# generator must adopt the same
  form in the Owner-local .NET pass.
- `sensor-parameters.migrated.example.json` has been **regenerated** to that baseline
  with `tools/migrated-example/generate.mjs` (`--write`): 108 logical positions (all
  `SENSOR`, no `gapAnchorForWaterJetId`), 108 Sensor configurations with dense
  scanOrder 1–108 (canonical assignments per the Owner-approved canonical Equipment
  topology adoption, 2026-10-25: I7 scan 79 → WJ5/IV5, I16 scan 88 → WJ6/IV6; per-Water-Jet counts
  12/15/12/15/15/15/12/12 under the Owner-authoritative tier boundary of 2026-10-29 — logical rows
  1–3 (G+2xx, G+1xx and G) UPPER, rows 4–6 (H, I and J) LOWER), the same per-Sensor
  formulas the synthetic CSV (`tests/config.tests/SyntheticSensorParameterCsv.cs`)
  feeds the importer (dirty bounds 15+(s%10) … 65+(s%10), threshold 55+(s%20),
  enabled s%17≠0 → six Disabled Sensors at scan 17/34/51/68/85/102, cleaningCount s%4),
  and `placementKind: UNRESOLVED` with `placementAnchors: []` for **all eight** Water
  Jets (Owner disposition: only the fine-grained placement is pending authoritative
  layout — canonical wall/tier confirmed; nothing inferred from the former gap or any
  stale anchor). Equipment rows and assignments come from the canonical table in
  `tools/fixture-generator/generate.mjs`; the frozen historical table serves
  `--legacy-proof` only and is never Product authority. Proof: `--legacy-proof` reproduces the previously committed 106-form file
  **byte-identically**, and a second `--write` run produces no diff. The .NET importer
  and `SensorParameterMigrationTests` were corrected to the same form; the Owner-local
  `MigratedExampleShapeTests` / importer run **PASSED** at PR #21 head `041b2863…` (full .NET
  455 / 455; `docs/STAGE_0.4C-K3_TOPOLOGY_CORRECTION.md` §13). The file was never hand-edited.
  The Canonical Topology Adoption regeneration (2026-10-21) and the Owner-approved canonical
  Equipment topology adoption regeneration (2026-10-25) reuse the same generator and
  proofs; their Owner-local .NET validation is pending and is not claimed here.
  `sensor-map.example.json` is unchanged by both adoptions (re-verified byte-identical).

Stage 0.3A-3 Checkpoint B added `sensor-parameters.migrated.example.json`: the
canonical shape of the migrated legacy sensor parameters (ADR-0017) — 108
logical positions, 108 Sensor configurations with dense scanOrder 1–108 (historical
file content: I7/I16 as NON_SENSOR_GAP anchors, 106 Sensor configurations, scanOrder
1–106 — Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.), the WJ1–WJ8 / IV1–IV8 paired
topology, and an empty warnings array. Every value is synthetic; the deferred
raw fields and the acquisition-binding provenance are null; the Owner CSV
itself and every real acquisition value never enter Git. The related
publication/validation pipeline remains Stage 0.3A-F content.
