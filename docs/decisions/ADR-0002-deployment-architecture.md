# ADR-0002 — Deployment Architecture

- **Status:** ACCEPTED
- **Date:** 2026-10-04
- **Scope:** Deployment topology, field interfaces, database placement, network stance, and
  configuration locality.
- **Authority:** Approved Stage 0.1 Scope Gate (Repository Documentation Foundation).

---

## Context

The system must supervise eight Water Jets, four motion controllers, a Main Pump, isolation
valves, and 208 thermocouple channels on a single Boiler Unit. Three interface families are
involved:

- **WAGO remote I/O** over Modbus TCP for digital inputs and outputs, analog inputs, and the
  VFD analog output. Coupler model 750-362.
- **Galil motion controllers**, model DMC-B140-M, four of them, each driving two Water Jets
  on axes A, B, C, and D.
- **The plant's DCS**, whose hardwired signals are monitored through WAGO I/O.

The plant also has constraints that shape the topology: the workstation is the only
computing asset dedicated to this installation, the site must work without Internet access,
and production network details are confidential and must not be published.

## Decision

1. **One Windows 11 Pro workstation per Boiler Unit**, running the application as a
   full-screen Control Room Kiosk. The application runs as a single installation under a
   Windows service account or the kiosk user as decided later. `[OPEN]` for the exact
   process model.
2. **One installation controls one Boiler Unit.** No application instance serves two Boiler
   Units, and no central aggregation service exists.
3. **Modbus TCP is the only field protocol** for the WAGO interface. Baseline function codes
   are FC01, FC02, FC03, FC04, FC05, FC06, FC15, and FC16. Address convention is zero-based
   internally.
4. **Galil motion controllers are reached over the plant network** by the workstation. The
   transport and library selection are `[OPEN]`.
5. **The Historian database is SQL Server 2025 Standard hosted on the workstation**, stored
   on three 2 TB HDDs in RAID 5, described as approximately 4 TB theoretical usable capacity
   before formatting and overhead.
6. **No Internet dependency.** The system is standalone. Any feature that would require
   external network access is out of scope.
7. **Production configuration is local and outside the Git working tree** where practical.
   Production IP addresses, register maps, tag lists, and setpoints are confidential
   deployment values and are never committed. See
   [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md).
8. **The control path to field outputs is not authorised.** Production valve and pump write
   control remains prohibited until the bench verification in
   [`ADR-0005-hardware-safety-boundary.md`](ADR-0005-hardware-safety-boundary.md) is complete
   and recorded.

## Consequences

- The workstation is a single point of failure for supervision. This is accepted; it must
  therefore never be the only protection for equipment or personnel.
- Database sizing, backup, and recovery are local responsibilities. Whether the workstation
  disk set is the only copy of data is `[OPEN]` and must be resolved before deployment.
- Network addressing, VLAN design, and topology are deployment documents held outside this
  repository.
- Because there is one application instance per unit, per-unit configuration differences
  accumulate. Configuration must therefore be external and validated, not compiled in.
- Simulator-based development becomes essential, since the topology makes it impractical to
  develop against live plant equipment.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Central server with thin clients | Adds a network dependency and a second point of failure; conflicts with one installation per Boiler Unit |
| Historian on a separate database server | No such server is planned; the standalone requirement makes local hosting the only supported option |
| Direct fieldbus (for example, a proprietary I/O protocol) instead of Modbus TCP | The installed interface is a WAGO Modbus TCP Coupler; Modbus TCP is the approved protocol |
| Ethernet/IP or OPC UA to the DCS | Not part of the approved interface baseline; DCS signals are monitored through WAGO I/O |
| Storing production configuration in the repository as a convenience | Prohibited by the public repository boundary |

## Verification status

- `[NOT VERIFIED]`: effective usable database capacity after formatting, RAID overhead, and
  file system effects.
- `[NOT VERIFIED]`: Galil communication transport, library choice, and command set.
- `[NOT VERIFIED]`: WAGO module composition beyond the approximate I/O counts, and the
  production register map.
- `[OPEN]`: workstation process model, service identity, startup behaviour, and kiosk
  lockdown mechanism.
- `[OPEN]`: backup and disaster-recovery approach.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — conceptual architecture and timing
- [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md) — storage and retention
- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — configuration locality
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — control-write prohibition
- [`ADR-0001-product-identity.md`](ADR-0001-product-identity.md)
- [`ADR-0005-hardware-safety-boundary.md`](ADR-0005-hardware-safety-boundary.md)
