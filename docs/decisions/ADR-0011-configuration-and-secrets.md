# ADR-0011 — Configuration and Secrets

- **Status:** PROPOSED — submitted for Owner acceptance at the Stage 0.2 Owner Manual
  Review. Not binding until the Owner records `ACCEPTED`.
- **Date:** 2026-10-04
- **Supersedes:** Nothing. This record refines the configuration-layering statement in
  [`../ARCHITECTURE.md`](../ARCHITECTURE.md) section 12 and resolves the configuration-store
  question left `[OPEN]` in section 11.
- **Scope:** The separation of public example configuration, local development
  configuration, simulator configuration, test-hardware configuration, production
  configuration, secrets, the user database, versioned engineering configuration, the Draft
  and Published states, and the configuration audit history.
- **Authority:** Approved Stage 0.2 Scope Gate — *Technology and Solution Architecture
  Decision*, section 8.7, together with
  [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) and
  [`../../SECURITY.md`](../../SECURITY.md).

---

## Context

The repository is public. Production IP addresses, Tag Lists, register maps, motion
coordinates, travel limits, pressure setpoints, production thresholds, DCS permissive
definitions, credentials, and connection strings must never be committed. At the same time
the system cannot be built or tested without *some* configuration: the simulator needs
dummy values, the tests need deterministic fixtures, and the deployment needs real site
values.

Two different problems are easy to confuse:

1. **Where a value lives** — repository, local site directory, or secret store.
2. **Whether a value is allowed to affect the running plant** — a Draft being edited by an
   engineer must never influence a running AutoSequence.

The baseline already requires that configuration publication must be atomic and auditable
(`[PROPOSED]`, [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) section 7), that invalid
configuration prevents publication (TMP-006), and that the runtime never silently corrects
invalid values (TMP-007).

## Decision

1. **Four storage locations, never mixed:**
   | Location | Content | Committed? |
   | --- | --- | --- |
   | Repository, `config/examples/` | Public-safe **example** configuration with obviously synthetic values, labelled as examples and incomplete by design | Yes |
   | Repository, test and simulator fixtures | Deterministic, public-safe simulator and test data | Yes |
   | Local site directory, outside the Git working tree | Local development values, test-hardware values, and **production** configuration | No |
   | OS-protected local secret store, outside the Git working tree | Secrets only | No |

   The example files are documentation of shape, not a starting point for a real deployment.
2. **Production configuration never enters the repository.** No production IP address, Tag
   List, register map, motion coordinate, travel limit, pressure setpoint, production
   threshold, DCS permissive definition, or site identifier is committed — not in
   configuration, not in examples, not in commit messages, and not in test fixtures.
   Raw configuration is stored **outside the Git working tree**, per the existing
   [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) rule and the
   repository-specific exclusions already present in [`../../.gitignore`](../../.gitignore).
3. **Secrets never enter the repository, and are separate from configuration.** Passwords,
   credentials, tokens, keys, certificates, and connection strings live only in an
   OS-protected local store outside the working tree. The preferred direction is to avoid
   storing a database secret at all by using operating-system integrated authentication for
   the runtime service identity ([ADR-0009](ADR-0009-database-access-and-migrations.md) item
   9). The exact secret-store mechanism is `[OPEN]`.
4. **The user database is separate from engineering configuration.** Local application
   users, roles, and permission assignments live in the local SQL Server database and are
   provisioned by an explicit administration step. They are not stored in configuration
   files, and no account data is committed.
5. **Versioned engineering configuration with two states:**
   - **Draft** — editable, validated on demand, never consumed by the runtime.
   - **Published** — an immutable, numbered revision produced by an explicit publication
     action, which the runtime alone consumes.
   Publication is a single transaction that records the revision, its validation result, and
   an audit record with user, timestamp, and reason
   ([ADR-0009](ADR-0009-database-access-and-migrations.md) item 5). A partially edited Draft
   can never reach the runtime.
6. **Validation blocks publication.** Publication is refused when validation fails, when a
   required value is missing, or when a value violates a defined constraint (for example
   `DiffUpperBound` must be strictly greater than `DiffLowerBound`). Failed publication
   leaves the previously published revision in force.
7. **The runtime consumes only the published revision, and applies it under state control.**
   - Applying a newly published revision is a controlled runtime operation, not a file watch
     and not an automatic reload.
   - A revision must not be applied while a Cleaning Job is active; publication is recorded
     immediately, and the revision is applied when the runtime is in an approved state.
   - If applying a revision fails validation inside the runtime, the runtime keeps operating
     on the previous revision and raises an explicit fault — it never operates on a partially
     applied revision.
8. **Rollback is by republishing an earlier revision.** History is append-only; a revision is
   never edited in place, and an applied revision is never modified retrospectively. Every
   publication, refusal, and application is audited.
9. **Runtime configuration is local configuration.** Device addresses, register maps, tag
   lists, motion values, timeouts, thresholds, and retention values are local site data. The
   repository contains only structure, examples, and documentation references.
10. **No configuration file is created in this Stage.** This record defines the model and the
    separation rules only. Creating example or local configuration files is a later Stage
    Gate decision.
11. **Profile and identification.** The active device profile (`SIMULATOR`, `TEST_HARDWARE`,
    `PRODUCTION`) is configuration state that is validated at start and displayed in the UI
    chrome ([ADR-0006](ADR-0006-ui-delivery-model.md) item 8). Changing it is an audited
    administrative action requiring permission, and it is never changed implicitly.

## Alternatives considered

| Alternative | Evaluation | Outcome |
| --- | --- | --- |
| Commit a "real-shaped" example configuration that deployments edit in place | Risks leaking site identifiers, addresses, and motion values into the public repository, and makes example drift into production reality | Rejected |
| Store configuration in the repository with placeholder secrets | Placeholders are frequently replaced and committed by mistake; secret material must never have a defined location in version control | Rejected |
| Single configuration file for everything | Cannot separate public, local, simulator, test-hardware, and production concerns; a single mistake exposes prohibited content | Rejected |
| Runtime watches configuration files and reloads automatically | An edit could take effect mid-job with no validation gate, no audit, and no publication record | Rejected |
| Runtime reads a Draft directly | Breaks the requirement that a Draft is not runtime state, and makes an unvalidated intermediate edit executable | Rejected |
| Applying a new revision immediately even during an active job | Could change coordinates, timeouts, or thresholds mid-job, undermining deterministic sequencing and job reproducibility | Rejected |
| Editing a published revision in place | Destroys the audit trail and makes historical job analysis untrustworthy | Rejected |
| Storing secrets in the SQL Server database used for configuration | Moves the secret rather than protecting it, and adds a credential needed to reach the credential | Rejected |
| Applying an invalid configuration with a warning | Directly contradicts the approved rule that the runtime must not silently correct invalid values | Rejected |

## Consequences

- The public repository can be developed and tested without containing a single production
  value, because the simulator and test fixtures are public-safe by construction.
- Any site-specific or confidential value has exactly one home: the local site directory or
  the local secret store.
- Configuration change becomes an explicit, audited, and reviewable act with a version number,
  which makes it possible to answer "what configuration was in force during that job?".
- Applying a published revision is coupled to the runtime state, so an engineer cannot
  accidentally change behaviour during a job. This is deliberate friction.
- The Draft/Published split means two representations of the same engineering data must stay
  consistent in the UI; that is a UI design cost, not an architectural ambiguity.

## Risks

| Risk | Effect | Mitigation direction | Status |
| --- | --- | --- | --- |
| Site values copied into the repository during development | Confidential data becomes permanently public | Local site directory outside the working tree; `.gitignore` exclusions; pre-commit checks; example values are obviously synthetic and incomplete | `[APPROVED]` rule already in force |
| A published revision applied during an active job through a defect | Non-deterministic job behaviour | Runtime state gate on application; verification case planned | `[PROPOSED]` |
| Secret store mechanism left undecided | Ad-hoc credential storage appears during implementation | Must be selected before any secret is stored | `[OPEN]` |
| Local configuration lost or corrupted | System cannot start or starts with wrong values | Backup of local configuration is `[OPEN]`; failure must be explicit and must not substitute defaults | `[OPEN]` |
| Profile switched without authorization | Plant controlled while simulated data was expected, or the reverse | Audited, permissioned profile change; profile displayed in the UI chrome; startup logs record the profile | `[PROPOSED]` |
| Example files mistaken for deployable configuration | An example value reaches a real deployment | Examples must be labelled, incomplete, and structurally unusable as a real register map or motion profile | `[APPROVED]` |

## Verification status

- `[NOT VERIFIED]`: nothing in this record has been implemented. There is no configuration
  store, no publication workflow, no validation pipeline, no secret store, and no profile
  switch.
- `[OPEN]`: local configuration path and format; secret-store mechanism; configuration backup
  and restore; whether revision application can be performed without a runtime restart.
- No configuration file, no example file, and no credential is created by this Stage.

## Follow-up gates

| Item | Gate that must close it |
| --- | --- |
| Local configuration path and format | Implementation Stage Gate |
| Secret-store mechanism | Implementation Stage Gate, before any secret is stored |
| Configuration validation rule catalogue | Implementation Stage Gate (rules), with production values captured locally, never committed |
| Revision application procedure and state gate | Implementation Stage Gate |
| Local configuration backup and restore | Deployment Stage Gate |
| Example-file authoring (public-safe only) | A later Stage Gate that explicitly permits creating example configuration files |

## Relationship to protected decisions

- **Preserved, not modified:** production configuration remains outside the public repository
  (PUB-002, PUB-003), secrets remain outside Git ([`../../SECURITY.md`](../../SECURITY.md)
  section 2), production values must never be invented (PHY-007, AGENTS.md section 2.5),
  configuration publication is atomic and auditable (`[PROPOSED]` in
  [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md)), invalid configuration prevents publication
  (TMP-006), the runtime does not silently correct invalid values (TMP-007), and the DCS
  Permissive Override exclusion list is not configurable (OVR-010).
- **Implements approved mandatory boundaries:** configuration publication is conceptually
  separate from editing a Draft; the runtime consumes an explicitly Published configuration,
  not a partially edited Draft; public example files contain public-safe synthetic values
  only.
- **Unchanged:** Production Write `[NOT AUTHORIZED]`; Production Device access
  `[NOT AUTHORIZED]`; Stage 0.3 remains unauthorised.
- No protected decision listed in the approved Stage 0.2 Scope Gate is reopened, weakened, or
  adapted to fit this configuration model.

## References

- [`../PUBLIC_REPOSITORY_BOUNDARY.md`](../PUBLIC_REPOSITORY_BOUNDARY.md) — prohibited content
- [`../../SECURITY.md`](../../SECURITY.md) — secrets and credentials policy
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — configuration and secrets model (section 17)
- [`../DOMAIN_MODEL.md`](../DOMAIN_MODEL.md) — configuration identity and publication rules
- [`ADR-0009-database-access-and-migrations.md`](ADR-0009-database-access-and-migrations.md)
- [`ADR-0012-simulator-first-development.md`](ADR-0012-simulator-first-development.md)
- [`ADR-0013-offline-deployment.md`](ADR-0013-offline-deployment.md)
