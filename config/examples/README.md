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
