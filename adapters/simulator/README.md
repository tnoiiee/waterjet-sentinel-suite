# Wjss.Adapters.Simulator — simulator-only synthetic source

Default and only startable profile in Stage 0.3A (`SIMULATOR`; ADR-0012).

## What exists (Stage 0.3A-2A)

`Synthetic/` provides the deterministic synthetic source the Runtime composes
its initial revision from:

| Type | Responsibility |
| --- | --- |
| `SyntheticSeed` | The explicit determinism seed. Passed by the composition root; no hidden global seed exists. |
| `DeterministicValueSource` | Platform-independent value stream (splitmix64 over the seed). No `System.Random`, no static mutable state. |
| `SyntheticSensorMap` | The canonical synthetic Sensor map. **Authoritative baseline (Owner critical topology correction, 2026-10-12): 108 logical positions row-major, all 108 of them Product Sensors (I7 and I16 included), synthetic device distribution 14 + 14 + 14 + 14 + 13 + 13 + 13 + 13 = 108 and 216 Thermocouple channel identities** (`SYN-TC-nn:CHmm`, front = lower channel index), as already emitted by the regenerated `config/examples/sensor-map.example.json`. **The C# class still encodes the superseded form** (106 Sensors + 2 `NON_SENSOR_GAP`, 14 + 14 + 13 × 6 = 106, 212 channels) and is pending the Owner-local .NET correction; until then its parity test against the example fails by design. `BuildInitialSensors` projects the initial Sensor states for one seed and one clock instant. There are **no Cannon slots in the Sensor matrix** |

The map composition is pinned to `config/examples/sensor-map.example.json` by a
parity test in `tests/runtime.tests`, so the simulator cannot drift from the
committed synthetic map.

## Sensor matrix and Water Jet topology the map encodes

The map implements the Owner-approved topology — `CanonicalSensorMap` in
`packages/contracts/WallMap.cs`, `packages/domain/WaterJetTopologyCatalog.cs`, ADR-0017:

- **108 logical positions** — 18 logical columns × 6 logical rows — **every one of them a
  Product Sensor**; logical **I7** (Rear wall, row 5) and logical **I16** (Front wall, row 5)
  are ordinary Sensors with two Thermocouple channels each. Wall columns: Left 1–4, Rear 5–9,
  Right 10–13, Front 14–18. Sensors per wall: 24 / 30 / 24 / 30. 108 × 2 = 216 channels.
- No `NON_SENSOR_GAP` position exists. There are **no Cannon slots in the Sensor matrix**; the
  earlier "Cannon equipment slot" wording and the later "two `NON_SENSOR_GAP` placement anchors
  (I7 → WJ3, I16 → WJ1), 106 Sensors, 212 channels" wording are superseded and survive only
  inside dated historical records. Superseded by the Owner critical topology correction: I7 and I16 are normal Product Sensors. The authoritative current baseline is 108 Sensors and 216 assigned Product TC measurements.
- **Water Jets are separate equipment entities, WJ1–WJ8**, each paired **1:1 with IV1–IV8 by
  ordinal** (`WJn` ↔ `IVn`). They are not Sensor-matrix slots and are not derived from them.
- **Installed Position and Target Coverage are separate concepts.** A Water Jet is installed
  on one wall and covers the **opposite** wall in the matching region: **WJ1 is installed
  Front-lower, covers the Rear-lower target area and pairs with IV1**; **WJ3 is installed
  Rear-lower, covers the Front-lower target area and pairs with IV3**. The former placement
  description "at the I16 / I7 gap anchor" is superseded (I7 and I16 are Sensors); the
  replacement placement description for WJ1 and WJ3 is an open Owner decision.
- **The Assigned Cleaning Device is never inferred from a Sensor's wall location.**
  `BuildInitialSensors` assigns `AssignedWaterJetId` / `AssignedIsolationValveId` by matching
  the Water Jet's **target** wall and region to the Sensor's own wall and region over
  `WaterJetTopologyCatalog.WaterJets`, exactly as `TryRequireAssignment` validates it. The
  legacy "cannon" ordinal maps directly to `WJn`, and the assigned device may be installed on
  the opposite wall.

## What does not exist yet

- No seeded acquisition loop, no tick/state evolution, no quality or
  classification evolution, no trend append. Those are the rest of Stage 0.3A-2
  and are **not** part of checkpoint A.
- No fault injection of any kind (device timeout, feedback delay, stale source,
  pressure loss). Injections arrive with the later 0.3A-2 checkpoints and stay
  scriptable and deterministic.
- No transport, no physical device simulation, no addresses, registers or timings.
- Nothing here is a Production path component, and simulated behaviour is never
  evidence of physical behaviour.

## Rules baked into this seam

1. Simulator sources implement the SAME contracts physical adapters will use
   (ADR-0010); no private simulator interface.
2. No physical device vocabulary, addresses, registers or timings anywhere in
   this project (public boundary; enforced by `tools/boundary-scan`).
3. Simulation results are labelled; they never assert physical behaviour
   (`[NOT VERIFIED]` applies to everything hardware-adjacent).
4. This project must never reference `spikes/**`.

**Status:** SOURCE-AUTHORED IN ARENA · NOT COMPILED IN ARENA · Owner-local
build required.
