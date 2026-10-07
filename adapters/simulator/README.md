# Wjss.Adapters.Simulator — simulator-only device adapters (skeleton)

Default and only startable profile in Stage 0.3A (`SIMULATOR`; ADR-0012).
Arriving in Stage 0.3A-2 (**not authorized**): deterministic seeded
acquisition, per-device serialized session queues, Fast/Medium/Slow poll
groups, bounded concurrency, and the accepted fault-injection set (device
timeout, Pump Stop/Trip, Isolation Valve feedback delay, Axis Standby
feedback delay, quality transitions, stale-after-reconnect).

## Rules baked into this seam

1. Simulator adapters implement the SAME contracts physical adapters will use
   (ADR-0010); no private simulator interface.
2. No physical device vocabulary, addresses, registers or timings anywhere in
   this project (public boundary; enforced by `tools/boundary-scan`).
3. Simulation results are labelled; they never assert physical behaviour
   ([NOT VERIFIED] applies to everything hardware-adjacent).
4. This project must never reference `spikes/**`.

**Status:** SOURCE-AUTHORED IN ARENA · NOT COMPILED IN ARENA · Owner-local
build required.
