# Security Policy — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED] at the Stage 0.1 documentation baseline.
**Applies to:** the repository `waterjet-sentinel-suite` and, once they exist, every
deployed instance of the application.

WJSS is a monitoring and supervisory control system. It is **not** a Safety Instrumented
System, and no statement in this document should be read as a safety certification or as
compliance with IEC, ISA, ISO, or any other standard. See
[`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md).

---

## 1. Reporting a vulnerability

Security issues must be reported privately to the Owner. Do **not** open a public issue,
and do not include exploit details, credentials, plant topology, or production data in any
public channel.

When reporting, include:

- The affected document, artefact, or (in future) component and version.
- A description of the issue and its realistic impact on a deployed Boiler Unit.
- Reproduction detail that does not itself contain sensitive plant information.
- Whether the issue could lead to unintended equipment command, data exposure, or
  loss of operator visibility.

There is no bug-bounty programme and no response-time guarantee. Reports are triaged by
the Owner in line with the authority order in [`AGENTS.md`](AGENTS.md).

## 2. Secrets and credentials — absolute rules

1. Credentials are never committed. This includes passwords, password hashes, tokens,
   API keys, private keys, certificates, service-account secrets, and connection strings.
2. Production connection strings are never committed, and never used in examples.
3. The break-glass Recovery Account credentials are never committed, never documented in
   this repository, and never transmitted through it. See
   [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md).
4. Local production configuration lives outside the Git working tree where practical.
   [`.gitignore`](.gitignore) carries repository-specific exclusion rules for that purpose.
5. If a secret is committed by mistake, treat it as compromised: report it to the Owner
   immediately and rotate it locally. Removing the file in a later commit does not undo
   the exposure, and history rewriting is prohibited without specific Owner authorisation
   (see [`AGENTS.md`](AGENTS.md) section 7).

## 3. Public repository boundary

This is a public repository. The authoritative list of prohibited content is in
[`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md). In summary, the
repository must never contain:

- Production IP addresses, host names, or network topology.
- Production Tag Lists, register maps, or I/O addresses.
- Actual motion coordinates, travel limits, speeds, accelerations, or decelerations.
- Pressure setpoints, production alarm thresholds, or production timeouts.
- User databases, password hashes, credentials, or connection strings.
- Production logs, plant-sensitive information, or operator-identifying records.

Public-safe examples are permitted, must be clearly labelled as examples, and must not be
derived from a real Boiler Unit deployment.

## 4. Security posture and baseline expectations

The initial system is standalone and does not depend on Internet access. The security
baseline is deliberately simple and, until further stages are approved, consists of the
following approved intentions:

- Local application users rather than a central directory. `[APPROVED]`
- Configurable roles composed as permission collections. `[APPROVED]`
- Privileged sessions with a configurable inactivity timeout; on timeout the privileged
  user is logged out and the session returns to the Operator session. `[APPROVED]`
- Manual hold-to-run operations stop on privileged-session timeout; an active
  Auto Sequence is **not** aborted by a session timeout. `[APPROVED]`
- Every break-glass login raises a high-severity audit event. `[APPROVED]`
- Audit history retention longer than any other category (see
  [`docs/HISTORIAN_RETENTION.md`](docs/HISTORIAN_RETENTION.md)). `[PROPOSED]`
- Appropriate baseline security, prioritised below reliability, stability,
  determinism, recoverability, operator usability, and maintainability. `[APPROVED]`

Application hardening details — authentication method, password policy, account lockout,
audit integrity, and the protection of stored configuration — are **not** defined at this
stage. No hardening behaviour may be assumed until it is specified in a later approved
Stage Gate. `[OPEN]`

## 5. Known limitations that affect security posture

- A shared Operator account is permitted. This prevents person-level attribution for
  Operator actions; audit records for Operator actions identify the account, not an
  individual. See [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md).
- The system is not certified to any standard.
- Hardware fail-safe behaviour of the WAGO coupler watchdog is `[NOT VERIFIED]`. Until
  bench verification is recorded, production valve and pump write control is
  `[NOT AUTHORIZED]`.
- The application cannot and must not be relied upon as protection against an energized
  output. A UI close guard is not an acceptable sole protection.
- Hardware emergency stop, limit switches, motor protection, Local/Remote selector, and
  independent protection remain outside the application's authority.

## 6. Device and network access

Do not connect the repository working environment to plant equipment or plant networks.
Do not scan, probe, or enumerate plant networks. Production device access is prohibited
for contributors and automation alike; see [`AGENTS.md`](AGENTS.md) section 6.

## 7. Audit and event integrity

Operator queue actions, manual timestamp corrections, alarm acknowledgement and shelving,
permission changes, configuration publication, and break-glass logins are all required to
produce Event or Audit records with the fields listed in
[`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md) and
[`docs/QUEUE_MODEL.md`](docs/QUEUE_MODEL.md). Tamper protection for those records is
`[OPEN]` and must be specified before deployment.

---

## Related documents

- [`AGENTS.md`](AGENTS.md) — authority order, prohibitions, stop conditions
- [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md)
- [`docs/USER_PERMISSION_MODEL.md`](docs/USER_PERMISSION_MODEL.md)
- [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md)
- [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md)
