# Wjss.Adapters.Simulator — simulator-only synthetic source

Default and only startable profile in Stage 0.3A (`SIMULATOR`; ADR-0012).

## What exists (Stage 0.3A-2A)

`Synthetic/` provides the deterministic synthetic source the Runtime composes
its initial revision from:

| Type | Responsibility |
| --- | --- |
| `SyntheticSeed` | The explicit determinism seed. Passed by the composition root; no hidden global seed exists. |
| `DeterministicValueSource` | Platform-independent value stream (splitmix64 over the seed). No `System.Random`, no static mutable state. |
| `SyntheticSensorMap` | The canonical synthetic Sensor map: **108 logical positions row-major — 106 Sensor locations and 2 `NON_SENSOR_GAP` positions** (logical **I7**, Rear wall row 5, the placement anchor of **WJ3**; logical **I16**, Front wall row 5, the placement anchor of **WJ1**), the synthetic device distribution (14 + 14 + 13 × 6 = 106) and the 212 Thermocouple channel identities (`SYN-TC-nn:CHmm`, front = lower channel index). `BuildInitialSensors` projects the 106 initial Sensor states for one seed and one clock instant. There are **no Cannon slots in the Sensor matrix** |

The map composition is pinned to `config/examples/sensor-map.example.json` by a
parity test in `tests/runtime.tests`, so the simulator cannot drift from the
committed synthetic map.

## Sensor matrix and Water Jet topology the map encodes

The map implements the Owner-approved topology — `CanonicalSensorMap` in
`packages/contracts/WallMap.cs`, `packages/domain/WaterJetTopologyCatalog.cs`, ADR-0017:

- **108 logical positions** — 18 logical columns × 6 logical rows — of which **106 are
  Sensors** and **2 are `NON_SENSOR_GAP` positions**: logical **I7** (Rear wall, row 5) and
  logical **I16** (Front wall, row 5). Wall columns: Left 1–4, Rear 5–9, Right 10–13, Front
  14–18. Sensors per wall: 24 / 29 / 24 / 29.
- A gap position is a **placement anchor only**: it carries no `SensorId`, no Thermocouple
  channels and no equipment identity, and it is **never a Sensor and never a Water Jet**.
  There are **no Cannon slots in the Sensor matrix**; the earlier "Cannon equipment slot"
  wording is superseded and survives only inside dated historical records.
- **Water Jets are separate equipment entities, WJ1–WJ8**, each paired **1:1 with IV1–IV8 by
  ordinal** (`WJn` ↔ `IVn`). They are not Sensor-matrix slots and are not derived from them.
- **Installed Position and Target Coverage are separate concepts.** A Water Jet is installed
  on one wall and covers the **opposite** wall in the matching region: **WJ1 is installed
  Front-lower at the I16 gap anchor, covers the Rear-lower target area and pairs with IV1**;
  **WJ3 is installed Rear-lower at the I7 gap anchor, covers the Front-lower target area and
  pairs with IV3**.
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
