# ADR-0001 — Product Identity

- **Status:** ACCEPTED
- **Date:** 2026-10-04
- **Scope:** Product naming, product description, deployment model, operating mode, language,
  user model, and connectivity stance.
- **Authority:** Approved Stage 0.1 Scope Gate (Repository Documentation Foundation).

---

## Context

A new supervisory system is being built for automated boiler wall water-jet cleaning. Before
any code exists, several identity and operating decisions must be fixed, because they
determine packaging, user experience, security posture, and the meaning of every later
requirement. Without a fixed identity, later documents drift into different assumptions
about who operates the system, how many units it serves, and whether it depends on
infrastructure that may not exist at the plant.

The plant context that forces these decisions:

- A Boiler Unit is cleaned by water jets that must be supervised, not autonomously trusted.
- The system is a monitoring and supervisory control system, not a protective system.
- Operators work from a control room and need a screen that cannot easily be closed,
  minimised, or navigated away from during operation.
- Plant sites may not offer reliable external connectivity, and the initial deployment must
  not depend on it.
- Local accounts are sufficient for the intended user population; a directory service is
  not available.

## Decision

1. **Product name:** WaterJet Sentinel Suite. **Short name:** WJSS.
   **Repository name:** `waterjet-sentinel-suite`.
2. **Technical description:** Automated Boiler Wall Water-Jet Cleaning Monitoring and
   Supervisory Control System.
3. **Deployment model:**
   - One Windows 11 Pro workstation per Boiler Unit.
   - One application installation controls one Boiler Unit.
4. **Operating mode:** The main operating mode is a full-screen Control Room Kiosk.
   Applicable pages must support workstation-responsive layouts.
5. **Language:** The default application language is English. Thai may be used as
   supplementary contextual explanation where necessary.
6. **Users:** The application uses local application users. A shared Operator account is
   permitted (see [`../USER_PERMISSION_MODEL.md`](../USER_PERMISSION_MODEL.md)).
7. **Connectivity:** The initial system is standalone and does not depend on Internet
   access.
8. **Safety position:** The product is not a Safety Instrumented System and does not replace
   emergency stop circuits, hardwired protection, motor protection, mechanical limits, or
   controller-side safe-stop behaviour. It is not certified to IEC, ISA, ISO, or any other
   standard; industrial standards are used as guidance and design inspiration only.
9. **Priorities, in order:** reliability, stability, deterministic behaviour,
   recoverability, operator usability, maintainability, appropriate baseline security.

## Consequences

- Packaging is per Boiler Unit. Nothing may assume a central server, a shared database
  across units, or fleet-level orchestration.
- The interface must be designed for kiosk operation first; responsive layouts are a
  secondary requirement, not the primary one.
- Security design must work offline: local accounts, local password policy, local audit.
  Any cloud-dependent security feature is excluded by this decision.
- Documentation and code must never describe the system as safety-rated, and requirements
  that would require a protective function must be redirected to external hardware.
- Because the product name and short name are fixed, identifiers, folders, and future
  assembly names follow them.
- Multi-unit operation becomes an out-of-scope item requiring a new ADR, not an incremental
  enhancement.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| One server serving multiple Boiler Units | Conflicting requirement that one installation controls one Boiler Unit; adds network dependency contrary to the standalone stance |
| Web application with browser kiosk | A browser is easier to close or navigate away from; a full-screen desktop kiosk matches control room practice more directly |
| Central authentication (Active Directory) | Not available and not needed for the initial standalone deployment |
| Cloud or Internet-dependent monitoring | Contradicts the standalone requirement and adds an availability dependency the plant does not need |
| Presenting the system as a protective layer | Explicitly prohibited; the system is not a Safety Instrumented System |

## Verification status

- `[NOT VERIFIED]`: the actual workstation hardware specification, display resolution, and
  kiosk lockdown mechanism. These are deployment decisions.
- `[NOT VERIFIED]`: whether Thai supplementary text is required on any specific page.
- `[APPROVED]`: naming, deployment model, operating mode, language default, user model,
  connectivity stance, safety position, and priority ordering.

## References

- [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) — entities and terminology
- [`../USER_PERMISSION_MODEL.md`](../USER_PERMISSION_MODEL.md) — local users and roles
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — safety position
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — IDN requirements
- [`ADR-0002-deployment-architecture.md`](ADR-0002-deployment-architecture.md)
