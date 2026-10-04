# Current State — WaterJet Sentinel Suite (WJSS)

**Document status:** The verified state below is `[APPROVED]` as a factual record.
Open items are `[OPEN]` and must not be resolved by assumption.

This document answers one question: *what is actually true right now, with evidence?*
Nothing in this repository may contradict it. If something does, the discrepancy is reported
rather than silently resolved.

---

## 1. Status legend

Used throughout the documentation:

| Marker | Meaning |
| --- | --- |
| `[APPROVED]` | Approved by the Owner in an approved Stage Gate. Binding on future work. |
| `[PROPOSED]` | A recommendation awaiting Owner approval. **Not** approved behaviour. |
| `[NOT VERIFIED]` | Not confirmed by bench test, field test, measurement, or Owner engineering review. |
| `[NOT AUTHORIZED]` | Currently prohibited. The activity, write, or access must not be performed. |
| `[OPEN]` | A question requiring an Owner decision. No behaviour may be assumed in the meantime. |

## 2. Repository state

| Item | State |
| --- | --- |
| Current stage | Stage 0.1 — Repository Documentation Foundation `[APPROVED]` |
| Documentation baseline | `0.1.0` |
| Repository contents | Documentation and repository governance only |
| Application source code | **Does not exist** |
| Project or solution files | **Do not exist** |
| Package manifests or dependencies | **Do not exist** |
| Database schema or SQL scripts | **Do not exist** |
| Modbus or Galil adapter | **Does not exist** |
| Simulator | **Does not exist** |
| Automated tests | **Do not exist** |
| CI workflow | **Does not exist** |
| Installer or release artifact | **Does not exist** |
| Production configuration | **Does not exist in this repository** |
| Device connections made | **None** |

## 3. Verified system state

At the Stage 0.1 baseline, the following are true:

- The approved scope of Stage 0.1 is documentation and repository governance only.
- No application code has been written, and no code may be written until a later Stage Gate
  authorises it.
- No hardware has been accessed. No WAGO coupler, Galil controller, pump, valve, or DCS
  signal has been contacted, probed, or tested.
- No production IP address, register map, tag list, motion coordinate, travel limit, speed
  profile, pressure setpoint, production alarm threshold, credential, or connection string
  has been created or committed.
- The documentation describes approved behaviour, marked `[APPROVED]`, and distinguishes it
  from proposals, marked `[PROPOSED]`.

## 4. What the documentation baseline establishes

| Topic | Document |
| --- | --- |
| Working contract, authority order, git rules | [`../AGENTS.md`](../AGENTS.md) |
| Security policy and secret handling | [`../SECURITY.md`](../SECURITY.md) |
| Stage-gate discipline and evidence rules | [`MASTER_PLAN.md`](MASTER_PLAN.md) |
| Forward capability view | [`ROADMAP.md`](ROADMAP.md) |
| Requirement register | [`REQUIREMENTS.md`](REQUIREMENTS.md) |
| Conceptual architecture | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Entities and terminology | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) |
| Hardware safety boundary | [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) |
| Control authority matrix | [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) |
| Queue arbitration | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) |
| Cleaning Job sequence | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) |
| Alarm model | [`ALARM_MODEL.md`](ALARM_MODEL.md) |
| Historian and retention | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) |
| Users and permissions | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) |
| Publication rules | [`PUBLIC_REPOSITORY_BOUNDARY.md`](PUBLIC_REPOSITORY_BOUNDARY.md) |
| Planned verification | [`TEST_STRATEGY.md`](TEST_STRATEGY.md) |
| Decision records | [`decisions/README.md`](decisions/README.md) |

## 5. Blocking constraints in force right now

1. **No production device access.** `[NOT AUTHORIZED]`
2. **No production valve or pump write control** until the bench verification in
   [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) is completed and recorded. `[NOT AUTHORIZED]`
3. **No application code, schema, adapter, simulator, test, CI workflow, installer, or
   release artifact** may be created until a later Stage Gate authorises it. `[NOT AUTHORIZED]`
4. **No production values** may be invented, inferred, or committed. `[NOT AUTHORIZED]`
5. **No merging.** Merge authority belongs to the Owner. `[APPROVED]`
6. **No history rewriting or force push** without specific Owner authorisation. `[APPROVED]`

## 6. Open items requiring Owner decisions

These are unresolved questions recorded honestly. None may be resolved by assumption.

| # | Item | Affects |
| --- | --- | --- |
| 1 | Sensor identifier format and stability rules | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) |
| 2 | Row alignment rule for walls of unequal size (24 versus 28) | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md), [`REQUIREMENTS.md`](REQUIREMENTS.md) SCN-005 |
| 3 | GlobalQueue contents after an Operator stops the Auto Sequence | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) |
| 4 | Permission required for each operator queue action, and required reason text | [`QUEUE_MODEL.md`](QUEUE_MODEL.md), [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) |
| 5 | Queue snapshot storage format and retention inside Event history | [`QUEUE_MODEL.md`](QUEUE_MODEL.md) |
| 6 | Fault class taxonomy: when to stop motion versus abort motion | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) |
| 7 | Recovery procedure for `RECOVERY_REQUIRED` and whether retry is permitted | [`CLEANING_SEQUENCE.md`](CLEANING_SEQUENCE.md) |
| 8 | Whether more than one Cleaning Job may ever run in parallel | [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md), [`CONTROL_AUTHORITY.md`](CONTROL_AUTHORITY.md) |
| 9 | Alarm definition catalogue, thresholds, blocking scopes, and non-shelvable list | [`ALARM_MODEL.md`](ALARM_MODEL.md) |
| 10 | Shelving duration limits and per-alarm maximums | [`ALARM_MODEL.md`](ALARM_MODEL.md) |
| 11 | Retention default ratification and aggregate function definition | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) |
| 12 | Backup, restore, and disaster-recovery approach | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) |
| 13 | Audit and event tamper protection | [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md), [`../SECURITY.md`](../SECURITY.md) |
| 14 | Authentication method, password policy, lockout, and privileged timeout default | [`USER_PERMISSION_MODEL.md`](USER_PERMISSION_MODEL.md) |
| 15 | Application language, runtime, UI framework, and process model | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| 16 | Modbus TCP client and Galil communication library selection | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| 17 | Configuration store format and validation mechanism | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| 18 | Behaviour when the Historian is unavailable | [`ARCHITECTURE.md`](ARCHITECTURE.md), [`HISTORIAN_RETENTION.md`](HISTORIAN_RETENTION.md) |
| 19 | Workstation clock discipline and drift bounds | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| 20 | Deployment acceptance criteria | [`ROADMAP.md`](ROADMAP.md), [`TEST_STRATEGY.md`](TEST_STRATEGY.md) |

## 7. Not verified — standing list

| Item | Reason |
| --- | --- |
| WAGO watchdog fail-safe behaviour | No bench test performed; not authorised |
| Hardware fail-safe on Ethernet loss, process termination, workstation reboot, WAGO reboot, stale command replay, auto re-energization | No bench test performed; not authorised |
| Watchdog timeout value matching configuration | No bench test performed; not authorised |
| Mechanical limits, soft limits, and operational envelope | Commissioning values; not measured |
| Pulses per engineering unit | Commissioning value; not measured |
| Encoder behaviour and whether feedback performs active correction | Not verified by engineering |
| Homing behaviour | Commissioning value; not measured |
| Speed, acceleration, deceleration profiles | Commissioning values; not measured |
| Pressure-ready setpoint, stable dwell, pressure rise timeout | Commissioning values; not measured |
| Valve open and close timeouts | Commissioning values; not measured |
| Sensor-to-Water-Jet and sensor-to-valve mapping | Deployment data; not recorded here |
| Production register map and Tag List | Confidential; never committed |
| Runtime build, unit tests, integration tests, database tests, hardware tests | No code exists; not applicable at this stage |

## 8. Sensitive data review

| Check | Result |
| --- | --- |
| Production IP addresses in the repository | None found |
| Credentials, tokens, keys, or password hashes | None found |
| Connection strings | None found |
| Production register map, Tag List, or addresses | Not present and must never be added |
| Motion coordinates, travel limits, speed profiles | Not present |
| Pressure setpoints or production alarm thresholds | Not present |
| Production configuration content | Not created. Only documentation references and `.gitignore` exclusion patterns exist. |

The checks behind this table are listed with their observed results in section 10.

## 9. Checkpoint record

| Item | Value |
| --- | --- |
| Stage | 0.1 |
| Branch | `arena/01a1080d-waterjet-sentinel-suite` |
| Base commit | `78171058f4d9a6f130bed2cd4b23c76841432a49` |
| Checkpoint commit | Recorded in the stage delivery report and visible in `git log` on the stage branch |
| Pull request | Opened from the stage branch, not merged. See the stage delivery report for the link or status. |
| Merge state | **Not merged.** Owner review pending. |

## 10. Validation record

Documentation-only validation, performed for the Stage 0.1 baseline. Detailed commands and
observed output are reported in the stage delivery report. Summary of what was checked:

| # | Check | Method |
| --- | --- | --- |
| 1 | List all changed files | `git status --porcelain` and `git diff --stat` before committing |
| 2 | Internal Markdown links resolve | Extracted every relative link target and verified each path exists |
| 3 | Production IP address search | Regular-expression scan across tracked files |
| 4 | Credential and connection-string search | Keyword scan across tracked files |
| 5 | Conflicting null use for `LastSuccessfulCleaningCompletedAt` | Keyword scan |
| 6 | Obsolete `NEVER_CLEANED` behaviour | Keyword scan |
| 7 | Claims that WAGO fail-safe has passed | Keyword scan of surrounding context |
| 8 | Claims that production device access is authorised | Keyword scan |
| 9 | No application code or package dependency created | File-type and file-extension inventory |
| 10 | Diff reviewed before commit | Full diff read before creating the checkpoint commit |

Recorded results of the checks above, observed during the Stage 0.1 baseline validation:

| # | Check | Result |
| --- | --- | --- |
| 1 | Changed file list | 25 Markdown files present plus `.gitignore`; no file present that the Stage 0.1 gate does not name |
| 2 | Internal Markdown links | 25 documents scanned, 293 relative link targets checked, **0 broken** |
| 3 | IPv4-pattern search across the whole tree | **0 matches**; CIDR-pattern and credential-bearing URL search: **0 matches** |
| 4 | Credential and connection-string search | **0 matches** for `Data Source=`, `Initial Catalog=`, `Server=`, `User Id=`, `UID=`, `Integrated Security=`, `Password=`, private-key headers, and common token prefixes. One regex hit on `sk-[A-Za-z0-9]` was a false positive from the word "kios**k-mo**de" |
| 5 | `LastSuccessfulCleaningCompletedAt` null usage | 7 documents mention it; every mention states that it is **never null**. No document defines or permits a null value |
| 6 | `NEVER_CLEANED` behaviour | 7 mentions, all of which prohibit or test against the obsolete state. No document implements it |
| 7 | WAGO fail-safe "passed" claims | None. Every mention states `[NOT VERIFIED]` or prohibits the claim |
| 8 | Production device access "authorised" claims | None. Every mention states `[NOT AUTHORIZED]`, or describes a conditional future state |
| 9 | Application code and dependencies | File-extension inventory found **no** `.cs`, `.sln`, `.csproj`, `.sql`, `.json`, `.yml`, `.ps1`, `.zip`, `.msi`, or package manifest. Tooling check confirmed no build toolchain is present in this environment |
| 10 | Diff review before commit | Full `git diff` reviewed before the checkpoint commit was created |

This document must not be read as claiming any test, build, database, or hardware
verification; see section 7.

---

## Related documents

- [`../AGENTS.md`](../AGENTS.md) — working contract and stop conditions
- [`MASTER_PLAN.md`](MASTER_PLAN.md) — stage gates
- [`ROADMAP.md`](ROADMAP.md) — forward view
- [`SAFETY_BOUNDARY.md`](SAFETY_BOUNDARY.md) — current prohibition on control writes
- [`TEST_STRATEGY.md`](TEST_STRATEGY.md) — verification levels and what has not been tested
