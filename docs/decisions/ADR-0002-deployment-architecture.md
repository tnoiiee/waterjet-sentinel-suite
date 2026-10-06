# ADR-0002 — Deployment Architecture

- **Status:** ACCEPTED
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-confirmed review punchlist)
- **Scope:** Deployment topology, field interfaces, database placement, network stance, and
  configuration locality.
- **Authority:** Approved Stage 0.1 Scope Gate, as corrected by the Owner-confirmed Stage
  0.1 documentation review punchlist.

---

## Context

The system must supervise eight Water Jets, four motion controllers, a Main Pump, isolation
valves, and 212 thermocouple channels on a single Boiler Unit. Three interface families are
involved:

- **WAGO remote I/O** over Modbus TCP for digital inputs and outputs, analog inputs, and the
  VFD analog output. Coupler model 750-362.
- **Galil motion controllers**, model DMC-B140-M, four of them, each driving two Water Jets
  on axes A, B, C, and D.
- **The plant's DCS**, whose hardwired signals are monitored through WAGO I/O. The
  application does not connect directly to the DCS.

The plant also has constraints that shape the topology: the workstation is the only
computing asset dedicated to this installation, the site must work without Internet access,
and production network details are confidential and must not be published.

Isolation Valve assignment is one-to-one with Water Jets: Water Jet 1 to Isolation Valve 1
through Water Jet 8 to Isolation Valve 8. Each Water Jet has one dedicated valve, and no
valve is shared.

## Decision

1. **One Windows 11 Pro workstation per Boiler Unit**, running the application as a
   full-screen Control Room Kiosk with controlled navigation and an operational close guard.
   The exact process model and service identity are `[OPEN]`.
2. **One installation controls one Boiler Unit.** No application instance serves two Boiler
   Units, and no central aggregation service exists.
3. **Modbus TCP is the only field protocol** for the WAGO interface. Baseline function codes
   are FC01, FC02, FC03, FC04, FC05, FC06, FC15, and FC16. Address convention is zero-based
   internally.
4. **Galil motion controllers are reached over the local equipment network** by the
   workstation. This is a local, plant-side network dedicated to equipment communication.
   The **exact network topology is `[OPEN]` / `[NOT VERIFIED]`**, and production IP
   addresses and topology remain outside the public repository. The transport and library
   selection are `[OPEN]`.
5. **The Historian database is SQL Server 2025 Standard hosted on the workstation**, stored
   on three 2 TB HDDs in RAID 5, described as approximately 4 TB theoretical usable capacity
   before formatting and overhead. Effective capacity is `[NOT VERIFIED]`.
6. **No Internet dependency.** The system is standalone. Any feature that would require
   external network access is out of scope.
7. **DCS signals are read through WAGO Modbus TCP.** The application must not connect
   directly to the DCS. Communication and acquisition health for those signals is evaluated
   as described in [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 5.
8. **Production configuration is local and outside the Git working tree** where practical.
   Production IP addresses, register maps, tag lists, DCS permissive definitions, and
   setpoints are confidential deployment values and are never committed. See
   [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md).
9. **The control path to field outputs is not authorised.** Production valve and pump write
   control remains prohibited until the bench verification in
   [`ADR-0005-hardware-safety-boundary.md`](ADR-0005-hardware-safety-boundary.md) is complete
   and recorded.
10. **No device connection is authorised at the Stage 0.1 documentation stage.**

## Consequences

- The workstation is a single point of failure for supervision. This is accepted; it must
  therefore never be the only protection for equipment or personnel.
- Database sizing, backup, and recovery are local responsibilities. Whether the workstation
  disk set is the only copy of data is `[OPEN]` and must be resolved before deployment.
- Equipment network addressing, segmentation, and topology are deployment documents held
  outside this repository.
- Because there is one application instance per unit, per-unit configuration differences
  accumulate. Configuration must therefore be external and validated, not compiled in.
- Simulator-based development becomes essential, since the topology makes it impractical to
  develop against live plant equipment.
- Communication health cannot be inferred from whether a value changed. A static digital
  input or pressure reading may be perfectly normal. Health must be judged from transport
  evidence.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| Central server with thin clients | Adds a network dependency and a second point of failure; conflicts with one installation per Boiler Unit |
| Historian on a separate database server | No such server is planned; the standalone requirement makes local hosting the only supported option |
| Direct fieldbus (for example, a proprietary I/O protocol) instead of Modbus TCP | The installed interface is a WAGO Modbus TCP Coupler; Modbus TCP is the approved protocol |
| Ethernet/IP or OPC UA directly to the DCS | Not part of the approved interface baseline; DCS signals are monitored through WAGO I/O and the application must not connect directly to the DCS |
| Storing production configuration in the repository as a convenience | Prohibited by the public repository boundary |
| Judging communication health by value change | Incorrect: a constant value is not evidence of a lost link |

## Verification status

- `[NOT VERIFIED]`: effective usable database capacity after formatting, RAID overhead, and
  file system effects.
- `[NOT VERIFIED]`: exact local equipment network topology, segmentation, and address plan.
- `[NOT VERIFIED]`: Galil communication transport, library choice, and command set.
- `[NOT VERIFIED]`: WAGO module composition beyond the approximate I/O counts, and the
  production register map and DCS signal contract.
- `[OPEN]`: workstation process model, service identity, startup behaviour, and kiosk
  lockdown mechanism.
- `[OPEN]`: backup and disaster-recovery approach.
- `[NOT AUTHORIZED]`: any device connection at this stage.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — conceptual architecture, communication health, timing
- [`../HISTORIAN_RETENTION.md`](../HISTORIAN_RETENTION.md) — storage and retention
- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — configuration locality
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — control-write prohibition
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — PHY-008, COM-009, COMH requirements
- [`ADR-0001-product-identity.md`](ADR-0001-product-identity.md)
- [`ADR-0005-hardware-safety-boundary.md`](ADR-0005-hardware-safety-boundary.md)
