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
- `sensor-parameters.migrated.example.json` is **STALE — not regenerated**: it is
  the output of the .NET importer over the synthetic CSV
  (`tests/config.tests/SyntheticSensorParameterCsv.cs`), not a formula that can be
  reproduced provably in Node, so it is left as committed (not hand-edited). It still
  carries I7/I16 as `NON_SENSOR_GAP`, 106 Sensor configurations, scanOrder 1–106 and
  `placementKind: NON_SENSOR_GAP` for WJ1/WJ3. Regenerating it requires the
  Owner-local .NET pass (importer correction + Owner placement decision for WJ1/WJ3).

Stage 0.3A-3 Checkpoint B added `sensor-parameters.migrated.example.json`: the
canonical shape of the migrated legacy sensor parameters (ADR-0017) — 108
logical positions, 108 Sensor configurations with dense scanOrder 1–108 (historical
file content: I7/I16 as NON_SENSOR_GAP anchors, 106 Sensor configurations, scanOrder
1–106 — Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.), the WJ1–WJ8 / IV1–IV8 paired
topology, and an empty warnings array. Every value is synthetic; the deferred
raw fields and the acquisition-binding provenance are null; the Owner CSV
itself and every real acquisition value never enter Git. The related
publication/validation pipeline remains Stage 0.3A-F content.
