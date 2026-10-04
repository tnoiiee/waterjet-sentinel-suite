# Public Repository Boundary — WaterJet Sentinel Suite (WJSS)

**Document status:** [APPROVED]. This document is authoritative for what may be committed
to this repository.

This repository is public. Anything committed here is permanently discoverable: removing a
file in a later commit does not remove it from history, and history rewriting is prohibited
without specific Owner authorisation. Treat every commit as permanent and public.

---

## 1. Who this applies to

Every contributor, every automated agent, every commit, every pull request, and every
artifact attached to this repository. It applies to this repository's default branch, to
feature branches, to commit messages, to issue text, and to pull request descriptions.

## 2. Permitted content

The following may be committed:

| Category | Notes |
| --- | --- |
| Source documentation | Design, requirements, models, decisions, plans |
| Future source code | Once a stage gate authorises it |
| Simulator definitions | Simulated devices for offline testing |
| Dummy configuration | Clearly labelled, obviously fake values |
| Example tags | Example identifiers that do not map to real plant points |
| Generic example motion profiles | Illustrative shapes with no production values |
| Automated tests | Once a stage gate authorises them |

## 3. Prohibited content

The following must **never** be committed:

| # | Prohibited | Examples |
| --- | --- | --- |
| 1 | Production IP addresses | Any address of a real WAGO coupler, Galil controller, workstation, or plant network |
| 2 | Production Tag List | The real list of tags for a Boiler Unit |
| 3 | Production register map | Real Modbus addresses and their meanings |
| 4 | Actual motion coordinates | Real P1 to P6 coordinates |
| 5 | Actual travel limits | Real mechanical and soft limits |
| 6 | Production speed profiles | Real speeds, accelerations, or decelerations |
| 7 | Pressure setpoints | Real pressure-ready values or rise timeouts |
| 8 | Production alarm thresholds | Real trip, warning, or diagnostic limits |
| 9 | User databases | Real users, roles, or account data |
| 10 | Password hashes | Any authentication material |
| 11 | Credentials | Passwords, tokens, keys, certificates, or shared secrets |
| 12 | Production connection strings | Real database or device connection strings |
| 13 | Production logs | Logs containing plant data or operator activity |
| 14 | Plant-sensitive information | Drawings, topology, partner names, or site identifiers |

The list above mirrors and does not weaken the approved boundary statement: production IP
addresses, production Tag Lists, production register maps, actual motion coordinates, actual
travel limits, production speed profiles, pressure setpoints, production alarm thresholds,
user databases, password hashes, credentials, production connection strings, production
logs, and plant-sensitive information are all prohibited.

## 4. Where production configuration lives

Production configuration remains **local** and should be stored **outside the Git working
tree** where practical. It is applied at deployment time and is not versioned in this
repository.

[`../.gitignore`](../.gitignore) carries repository-specific exclusion rules covering local
production configuration directories, secrets and certificates, local tag list and register
map files, local exports, and local logs. Ignore rules are a safety net, not authorisation:
a file being ignored does not make its content acceptable, and an ignore rule never
justifies creating prohibited content.

## 5. Examples

Examples are permitted only when they are:

1. **Obviously generic** — no resemblance to a real deployment's numbering scheme.
2. **Labelled** — the document or file states that the values are examples.
3. **Incomplete by design** — no example may be usable as a starting point for a real
   register map or motion profile.
4. **Free of secrets** — no placeholder that looks like a real credential, token, or key.

Prohibited in examples even as a placeholder: real host names, real subnets, real tag
naming conventions, real coordinates, real unit-specific identifiers, and strings formatted
like a connection string containing a credential.

## 6. Stage 0.1 restriction

At the Stage 0.1 documentation baseline, this repository may contain only documentation and
repository-governance files. Stage 0.1 may create only **example filenames or documentation
references** for configuration. It must not create production configuration content.

## 7. Procedure if prohibited content is found or added

1. Stop the affected work immediately.
2. Do not commit the content, and do not push it.
3. Report it to the Owner with the file, the nature of the exposure, and whether it was
   ever pushed.
4. Treat any exposed credential as compromised and rotate it outside the repository.
5. Only the Owner may authorise any history remediation. History rewriting is otherwise
   prohibited — see [`../AGENTS.md`](../AGENTS.md) section 7.

## 8. Pre-commit checks expected of contributors

Before committing, a contributor must have evidence that:

- No prohibited category in section 3 appears in the diff.
- No file matching a prohibited category is staged.
- Examples are labelled as examples.
- No credential-shaped string, connection string, or private key is present.

The record of these checks belongs in the stage report's validation section. See
[`TEST_STRATEGY.md`](TEST_STRATEGY.md) section 5.

---

## Related documents

- [`../SECURITY.md`](../SECURITY.md) — security policy and secret handling
- [`../AGENTS.md`](../AGENTS.md) — scope-lock and stop conditions
- [`REQUIREMENTS.md`](REQUIREMENTS.md) — PUB requirements
- [`CURRENT_STATE.md`](CURRENT_STATE.md) — sensitive-data review record
