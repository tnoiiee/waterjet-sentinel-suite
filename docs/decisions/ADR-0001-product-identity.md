# ADR-0001 — Product Identity

- **Status:** ACCEPTED
- **Date:** 2026-10-04 (corrected 2026-10-05 by the Owner-confirmed review punchlist)
- **Scope:** Product naming, product description, deployment model, operating mode, language,
  user model, connectivity stance, and the separation of product identity from technology
  choice.
- **Authority:** Approved Stage 0.1 Scope Gate, as corrected by the Owner-confirmed Stage
  0.1 documentation review punchlist.

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
- Operators work from a control room and need a screen that supports controlled navigation
  and cannot easily be shut down accidentally during active operation.
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
4. **Operating mode:** The main operating mode is a full-screen Control Room Kiosk with
   controlled navigation. Applicable pages must support workstation-responsive layouts.
   The normal Operations UI close action is blocked while a Cleaning Job is active or the
   Main Pump is running; this is an operational usability control, not a safety protection.
5. **Language:** The default application language is English. Thai may be used as
   supplementary contextual explanation where necessary.
6. **Users:** The application uses local application users. A shared Operator account is
   permitted (see [`../USER_PERMISSION_MODEL.md`](../USER_PERMISSION_MODEL.md)).
7. **Connectivity:** The initial system is standalone and does not depend on Internet
   access. Field devices are reached over a **local equipment network**; the exact topology
   is `[OPEN]`.
8. **Safety position:** The product is not a Safety Instrumented System and does not replace
   emergency stop circuits, hardwired protection, motor protection, mechanical limits, or
   controller-side safe-stop behaviour. It is not certified to IEC, ISA, ISO, or any other
   standard; industrial standards are used as guidance and design inspiration only. The
   application must never command, override, bypass, suppress, or replace an external
   protection function.
9. **Priorities, in order:** reliability, stability, deterministic behaviour,
   recoverability, operator usability, maintainability, appropriate baseline security.
10. **Technology neutrality:** Product identity does **not** decide the implementation
    technology. The application language, runtime, UI framework, and UI delivery
    architecture — browser-based, desktop, or hybrid local-web — remain `[OPEN]` and are
    questions for a future approved technology decision. The binding requirements are
    kiosk operation, controlled navigation, the operational close guard, responsive
    workstation pages, and Windows 11 Pro deployment.

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
- **No UI delivery technology is foreclosed by this ADR.** A future technology ADR must
  evaluate candidates on operational, maintainability, and risk grounds — not on the
  convenience of closing or navigating away from a window.

## Alternatives considered

| Alternative | Reason rejected |
| --- | --- |
| One server serving multiple Boiler Units | Conflicting requirement that one installation controls one Boiler Unit; adds network dependency contrary to the standalone stance |
| Central authentication (Active Directory) | Not available and not needed for the initial standalone deployment |
| Cloud or Internet-dependent monitoring | Contradicts the standalone requirement and adds an availability dependency the plant does not need |
| Presenting the system as a protective layer | Explicitly prohibited; the system is not a Safety Instrumented System |
| Deciding the UI framework here | Out of scope for product identity; the choice is a separate technology decision and remains `[OPEN]` |

The previously recorded rejection of "a web application with a browser kiosk" on the
grounds that "a browser is easier to close or navigate away from" has been **removed**. That
reasoning conflated a technology choice with a requirement. The requirements it was
implicitly addressing — controlled navigation, kiosk operation, and the operational close
guard — are now stated directly in item 4 and are binding on any delivery technology.

## Verification status

- `[NOT VERIFIED]`: the actual workstation hardware specification, display resolution, and
  kiosk lockdown mechanism. These are deployment decisions.
- `[NOT VERIFIED]`: whether Thai supplementary text is required on any specific page.
- `[NOT VERIFIED]`: exact local equipment network topology.
- `[APPROVED]`: naming, deployment model, operating mode, close-guard requirement, language
  default, user model, connectivity stance, safety position, and priority ordering.
- `[OWNER CONFIRMED]`: technology neutrality — the framework and UI delivery architecture
  are not decided by this ADR.

## References

- [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) — entities and terminology
- [`../USER_PERMISSION_MODEL.md`](../USER_PERMISSION_MODEL.md) — local users and roles
- [`../SAFETY_BOUNDARY.md`](../SAFETY_BOUNDARY.md) — safety position and UI guard limits
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — open technology decisions
- [`../REQUIREMENTS.md`](../REQUIREMENTS.md) — IDN and UIG requirements
- [`ADR-0002-deployment-architecture.md`](ADR-0002-deployment-architecture.md)
