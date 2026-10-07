# Wjss.Adapters.Simulator — simulator-only synthetic source

Default and only startable profile in Stage 0.3A (`SIMULATOR`; ADR-0012).

## What exists (Stage 0.3A-2A)

`Synthetic/` provides the deterministic synthetic source the Runtime composes
its initial revision from:

| Type | Responsibility |
| --- | --- |
| `SyntheticSeed` | The explicit determinism seed. Passed by the composition root; no hidden global seed exists. |
| `DeterministicValueSource` | Platform-independent value stream (splitmix64 over the seed). No `System.Random`, no static mutable state. |
| `SyntheticSensorMap` | The canonical synthetic Sensor map: 108 slots row-major, 106 Sensor locations, 2 Cannon equipment slots (logical I7 Rear, I16 Front), the synthetic device distribution and the 212 Thermocouple channel identities (`SYN-TC-nn:CHmm`, front = lower channel index). `BuildInitialSensors` projects the 106 initial Sensor states for one seed and one clock instant. |

The map composition is pinned to `config/examples/sensor-map.example.json` by a
parity test in `tests/runtime.tests`, so the simulator cannot drift from the
committed synthetic map.

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
