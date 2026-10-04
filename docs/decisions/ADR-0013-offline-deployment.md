# ADR-0013 — Offline Deployment

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record addresses the deployment questions left `[OPEN]` by
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 11 and
  [`../ROADMAP.md`](../ROADMAP.md) section 3.
- **Scope:** Installation, startup, service recovery, configuration location, logs and
  diagnostics, backup and restore, upgrade and rollback, diagnostic bundle, local-only
  communication, firewall boundary, and the no-Internet-dependency rule on the Windows 11 Pro
  workstation.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.9, together with IDN-003, IDN-009 and
  [`ADR-0002`](ADR-0002-deployment-architecture.md).

---

## Context

The workstation is offline, standalone, and physically located with the Boiler Unit it
controls. One Windows 11 Pro machine per Boiler Unit runs the kiosk shell, the runtime
service, and SQL Server 2025 Standard. There is no Internet access, no central server, no
directory service, and no remote management channel.

That changes the economics of every deployment decision:

- Software cannot be downloaded at install time. Prerequisites must be present on media or on
  the image.
- A failed upgrade cannot be repaired by fetching a new build. Reversibility matters more
  than upgrade convenience.
- Diagnostics travel by removable media or network share chosen by the site, not by cloud
  upload.
- An unattended workstation must come back after a power event without an engineer present,
  and must not re-energize outputs or resume an interrupted job on its own.

## Decision

1. **Installation is offline from local media.** Installation and upgrade use installable
   media produced from the repository's build outputs. No installation step may require an
   Internet connection, a public package registry, a licence activation server, or a remote
   service.

2. **Prerequisites are detected, never downloaded.** SQL Server 2025 Standard is installed
   by the site from vendor media as a deployment prerequisite. The web view runtime required
   by the kiosk shell ([ADR-0006](ADR-0006-ui-delivery-model.md)) is either present on the
   Windows 11 Pro image or supplied offline with the application package. The installer must
   verify prerequisites and **fail closed with a clear, offline-actionable remedy** when one
   is missing.

3. **Startup and supervision:**
   - The runtime service starts automatically and independently of any interactive sign-in.
   - The kiosk shell starts automatically on the kiosk session. The exact Windows 11 Pro
     mechanism (assigned-access configuration, startup task, or shell replacement) is `[OPEN]`
     and must be selected with evidence during the deployment gate.
   - The runtime service's recovery policy is bounded and configured locally. Recovery never
     authorises a command: after any restart the runtime enters its startup state and requires
     device state, position knowledge, and permissives to be re-established
     ([ADR-0007](ADR-0007-runtime-process-model.md) item 8).
   - **No automatic resumption.** A restart never resumes an AutoSequence, never resumes a
     Cleaning Job, and never re-energizes an output.
4. **Configuration location.** Local site configuration lives outside the Git working tree in
   a local application data directory with operating-system file-system protection, separate
   from the application binaries so that an upgrade does not overwrite site values
   ([ADR-0011](ADR-0011-configuration-and-secrets.md)).
5. **Logs and diagnostics.** Structured local log files and diagnostic counters are written to
   a local application data path outside the repository, with rotation and retention.
   Diagnostics retention follows the approved category (`[PROPOSED]`: 180 days). No log
   shipper and no remote sink exists.
6. **Diagnostic bundle.** A diagnostic bundle is produced by an explicit local operator or
   maintainer action and saved to a chosen local path. Bundle contents, format, and whether
   Historian extracts may be included are `[OPEN]`. A bundle must never be automatically
   uploaded, and must be reviewed for prohibited plant content before it leaves the site.

7. **Backup and restore:**
   - Database backup uses the engine's native mechanism to local storage, with a mandatory
     pre-migration backup ([ADR-0009](ADR-0009-database-access-and-migrations.md) item 8).
   - Schedule, retention, and whether a copy leaves the workstation are `[OPEN]`; the current
     documented position is that off-box copy is unresolved and must be settled before
     deployment acceptance.
   - **Restore must be tested before deployment acceptance.** No restore test has been
     performed.
   - Local configuration and secret-store backup is `[OPEN]` and must be resolved together
     with the configuration model.
8. **Upgrade and rollback:**
   - Upgrades are versioned offline packages applied during a planned maintenance window.
   - The sequence is: stop the AutoSequence and the runtime service, verify the pre-upgrade
     backup, apply migrations, install the new package, verify startup and health, then return
     to service. The detailed procedure belongs to the deployment gate.
   - **Rollback is by restoring the pre-upgrade backup and reinstalling the previous package.**
     Schema downgrade is not relied upon ([ADR-0009](ADR-0009-database-access-and-migrations.md)
     item 4).
   - The application version identity displayed by the system must match the installed
     package. No application version exists yet, and none is established by this Stage
     ([`../../AGENTS.md`](../../AGENTS.md) section 4).

9. **Local-only communication:**
   - The Local Application API is bound to the loopback interface only
     ([ADR-0007](ADR-0007-runtime-process-model.md) item 5). No application component
     listens on an address reachable from the equipment network or any other host.
   - Device communication is Modbus TCP and the Galil path over the local equipment network
     only. No component may attempt to reach the DCS directly (COM-009).
   - No application component makes an outbound connection to the Internet, to a cloud
     service, or to any host outside the workstation and the local equipment network.
10. **Firewall boundary.** The Windows Firewall profile on the workstation must be configured
    so that the application exposes no inbound service beyond the loopback interface, and so
    that only the required local equipment communication is permitted outbound. Exact rule
    definitions belong to the deployment gate and to local site documentation; they are not
    recorded here.
11. **No deployment artefact is created in this Stage.** No installer, script, package
    definition, or CI workflow is authored.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Online installer with prerequisite download | Requires Internet access, forbidden by IDN-009 | Rejected |
| Central deployment server pushing updates to workstations | Contradicts one-installation-per-unit standalone operation and adds an infrastructure dependency the site does not have | Rejected |
| Scheduled silent automatic upgrades | The plant is a live control environment; an unattended schema or binary change without a verified backup is not acceptable | Rejected |
| Schema downgrade as the rollback mechanism | Unreliable in practice and untested; restoring a backup is the trustworthy path | Rejected as primary mechanism |
| Cloud-hosted diagnostics or telemetry | Prohibited by the offline requirement and by the public-boundary rules for plant-sensitive data | Rejected |
| Kiosk shell started only manually by an operator | A power event would leave the Control Room without an interface until an operator intervened | Rejected |
| Trusting the operating system image to always carry the web view runtime | It may be absent, disabled, or removed by servicing; the installer must detect and act | Rejected |
| Exposing the Local Application API on a site LAN interface for remote support | Expands the attack surface and contradicts the local-only boundary | Rejected |

## Consequences

- Deployment is a planned, verified, offline activity with a real maintenance window, not a
  background update. That is the cost of an offline control installation, and it is accepted.
- Rollback capability depends on a working backup and on the previous package being retained.
  Both must be part of the deployment procedure and both must be verified before acceptance.
- An interrupted job after a restart requires explicit operator resolution; nothing resumes
  automatically. This is deliberate and consistent with the recovery and acknowledgement
  rules.
- The kiosk startup mechanism remains an open deployment detail; it does not block the
  architecture but must be closed before installation is designed.
- Because nothing listens beyond loopback and nothing calls out, remote support must be
  provided by site-local means (on-site presence or site-chosen local connectivity), which is
  a plant process decision rather than an application feature.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Prerequisite missing on the workstation image | Installation fails on site | Prerequisite detection with explicit offline remedy; offline runtime package available | `[OPEN]` |
| Upgrade without a verified backup | Unrecoverable failure | Mandatory verified pre-migration backup in the procedure | `[PROPOSED]` |
| No off-box backup copy | Loss of all history if the workstation fails | Off-box copy decision must be resolved before deployment acceptance | `[OPEN]` |
| Automatic restart loops masking an unresolved fault | Repeated start attempt against unknown equipment state | Startup state blocks commands; bounded recovery; operator-visible fault | `[PROPOSED]` |
| Diagnostic bundle containing plant-sensitive content | Prohibited data leaves the site | Manual bundle creation with a review step; contents are `[OPEN]` | `[OPEN]` |
| Kiosk startup mechanism interacts badly with Windows servicing | Kiosk does not start after an OS update | Mechanism selection and re-verification belong to the deployment gate; shell supervision restarts it | `[OPEN]` |

## Verification status

- `[NOT VERIFIED]`: nothing here has been executed. No installer, package, service
  registration, backup, restore, upgrade, rollback, or kiosk startup has been performed or
  observed.
- `[NOT VERIFIED]`: presence and version of the required web view runtime on the target
  Windows 11 Pro image.
- `[NOT VERIFIED]`: hardware behaviour on reboot, on process termination, and on communication
  loss — all remain bench verification items.
- `[OPEN]`: package format; kiosk startup mechanism; firewall rule definitions; diagnostic
  bundle contents; backup schedule, retention, and off-box copy; configuration backup; whether
  a published configuration revision can be applied without a runtime restart.
- No release, ZIP, installer, or deployment script is created by this Stage.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Package format and offline prerequisite handling | Deployment Stage Gate |
| Kiosk startup mechanism with Windows 11 Pro evidence | Deployment Stage Gate |
| Firewall rule definitions and local-only verification | Deployment Stage Gate |
| Diagnostic bundle contents and handling rule | Deployment Stage Gate |
| Backup schedule, retention, and off-box copy | Deployment Stage Gate, before deployment acceptance |
| Restore test execution | Test Stage Gate (planned) and deployment acceptance |
| Upgrade and rollback procedure, including the verified-backup requirement | Deployment Stage Gate |
| Any packaging or installation artefact | A later Stage Gate that explicitly authorises creating it |

## Relationship to protected decisions

- **Preserved, not modified:** Windows 11 Pro target and one workstation per Boiler Unit
  (IDN-003, ADR-0002), standalone operation with no Internet dependency (IDN-009), one
  installation controlling one Boiler Unit (IDN-004), kiosk operating mode (IDN-005), local
  application users (IDN-008), production configuration outside the repository (PUB-002,
  PUB-003), and the prohibition on describing anything as safety-rated (IDN-010).
- **Implements approved mandatory decisions:** local services; no Internet dependency; the
  runtime service never auto-resumes an interrupted Cleaning Job or re-energizes an output;
  the application must not be relied on for safe states.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Production Device access
  `[NOT AUTHORIZED]`; WAGO fail-safe `[NOT VERIFIED]`; Stage 0.3 remains unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this deployment model.

## References

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — offline deployment model (section 19)
- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — prohibited content
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — bench verification and safe states
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — IDN, HSB, and ARC requirement groups
- [`ADR-0002-deployment-architecture.md`](ADR-0002-deployment-architecture.md)
- [`ADR-0007-runtime-process-model.md`](ADR-0007-runtime-process-model.md)
- [`ADR-0009-database-access-and-migrations.md`](ADR-0009-database-access-and-migrations.md)
- [`ADR-0011-configuration-and-secrets.md`](ADR-0011-configuration-and-secrets.md)
