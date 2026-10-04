# AGENTS.md — Working Contract for Contributors

**Project:** WaterJet Sentinel Suite (WJSS)
**Repository:** `waterjet-sentinel-suite`
**Document status:** [APPROVED] — established by the approved Stage 0.1 Scope Gate.
**Applies to:** every automated agent, tool-assisted change, and human contributor working in this repository.

This file is a working contract. It constrains *how* work is performed and *what* may be
claimed. It does not grant permission to build anything. Permission to build comes only
from an approved Stage Gate issued by the Owner.

---

## 1. Authority order

When two statements disagree, the higher-numbered source never overrides a lower-numbered
source. Resolve conflicts strictly in this order:

1. The latest explicit Owner instruction.
2. The approved Stage Gate for the current stage.
3. This file (`AGENTS.md`).
4. [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md).
5. Approved acceptance criteria.
6. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
7. [`docs/ROADMAP.md`](docs/ROADMAP.md).
8. Older proposals and unapproved recommendations.

A recommendation that appears in a design document is **not** approved behavior unless a
Stage Gate or the Owner has approved it. Approved behavior is always marked `[APPROVED]`
in the documentation; see the status legend in
[`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md).

## 2. Scope-lock rules

1. Work only inside the currently approved Stage Gate. Do not start the next stage.
2. Do not create, modify, or delete any file that the current Stage Gate does not name.
3. If an additional file appears necessary, **stop, report it, and explain why** before
   creating it. Do not create it first and justify it afterwards.
4. Do not infer that an undocumented behavior is approved. Unspecified behavior is
   unspecified — it is recorded as `[OPEN]`, not implemented and not guessed.
5. Do not invent production values of any kind. Invented values include IP addresses,
   register maps, tag names, coordinates, travel limits, pulses per engineering unit,
   speeds, accelerations, decelerations, pressure setpoints, alarm thresholds, and
   timeouts.
6. Scope-locked work is delivered as a checkpoint; the Owner merges. The agent never
   merges.

## 3. Evidence-first rules

1. Every factual claim in the final report must be traceable to actual tool output
   produced during the stage.
2. Never claim that a file, check, test, build, commit, push, or pull request succeeded
   unless the corresponding tool output was observed.
3. Read before writing. Inspect the current state of every file before changing it.
4. Verify after writing. Re-read or re-inspect what was produced, including link targets
   and search results.
5. Report negative results plainly. A search that found nothing is evidence; report the
   search that was run and what it returned.
6. Report uncertainty as uncertainty. Use `NOT VERIFIED` rather than an assumption.

## 4. Version and checkpoint rules

1. Documentation-only stages advance the documentation baseline version recorded in
   [`CHANGELOG.md`](CHANGELOG.md) using the `0.x.y` series while the product is pre-release.
2. A **Development Checkpoint** is a commit that records a reviewable, evidence-backed
   position. A checkpoint does **not** mean: production ready, commissioned, tested on
   hardware, or certified.
3. Every checkpoint commit body must state: the stage, what is `CHANGED`, what is
   `UNCHANGED`, and what is `NOT VERIFIED`.
4. Checkpoints are append-only. An approved checkpoint is never rewritten; corrections are
   new commits.
5. Record the checkpoint identifier (branch and commit SHA) in
   [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) when it is created.

## 5. Public repository restrictions

This repository is a public-boundary repository. It must not contain any item listed in
[`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md), including:

- Production IP addresses, host names, or network topology.
- Production Tag Lists, register maps, or addresses.
- Actual motion coordinates, travel limits, or speed profiles.
- Pressure setpoints, production alarm thresholds, or production timeouts.
- User databases, password hashes, credentials, tokens, or private keys.
- Production connection strings, production logs, or plant-sensitive information.

Only public-safe examples may be committed, and examples must be labelled as examples.
Production configuration is local, kept outside the Git working tree where practical, and
is never committed.

## 6. Device access prohibition

1. Do not connect to any device: no WAGO coupler, no Galil controller, no pump, no valve,
   no PLC, no DCS, no production database, no plant network.
2. Do not author code, scripts, adapters, or configuration that would write to production
   outputs. Production valve and pump write control is `NOT AUTHORIZED` until the bench
   verification listed in [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) has been
   completed and recorded.
3. Do not test, probe, scan, or enumerate plant networks.
4. Do not install dependencies, toolchains, or services that alter the working
   environment without explicit authorisation for that stage.

## 7. Destructive Git operations — prohibited unless separately authorised

The following are prohibited unless the Owner authorises them explicitly and in writing
for a specific, named purpose:

- `git push --force` or any forced update
- `git reset --hard`
- `git clean -fd`
- Rebasing approved checkpoints
- Any history rewrite (`filter-branch`, `filter-repo`, amend of published commits)
- Deleting or force-moving any shared branch or tag
- Deleting branches that contain unmerged checkpoints

Non-destructive, additive work is the default: new commits on the stage branch.

## 8. Owner-only merge rule

1. The agent creates checkpoints, pushes the stage branch, and may open a pull request.
2. The agent **never** merges a pull request, never merges `main`, and never fast-forwards
   `main`.
3. Review and merge authority belongs exclusively to the Owner.
4. If pull-request creation is unavailable, the agent reports the exact branch name and
   commit SHA so the Owner can act.

## 9. Reporting format — Changed / Unchanged / Not Verified

Every stage report and every checkpoint description uses these three headings:

- **CHANGED** — every file created, modified, or deleted, with a short purpose. Nothing
  else may appear under this heading.
- **UNCHANGED** — an explicit statement of what was *not* touched, naming the categories
  that the stage prohibited (for example: runtime code, hardware adapters, SQL scripts,
  production configuration, CI workflows, release artifacts).
- **NOT VERIFIED** — every value, behavior, or integration that remains unproven, with
  the reason it is unproven.

## 10. No false test claims

1. Documentation-only stages perform documentation-only validation. Do not describe
   documentation checks as tests passed.
2. Never claim runtime, unit, integration, database, hardware, device, performance, or
   safety tests unless such a test actually ran and its output was observed.
3. Never claim compliance with or certification to any standard (IEC, ISA, ISO, or
   other). Industrial standards are used as guidance and design inspiration only.
4. Never claim that the hardware fail-safe behaviour of the WAGO watchdog has passed
   verification. It remains `NOT VERIFIED` until bench evidence exists and is recorded.
5. If asked to report a result that was not observed, report the gap instead.

## 11. Required stop conditions

Stop the affected work, do not choose a behaviour silently, and report to the Owner when
any of the following occurs:

1. **Scope boundary reached.** The next required action is outside the approved Stage Gate.
2. **Inconsistency found.** Two governing sources disagree. Stop the affected part only;
   document the discrepancy and continue work that is unaffected.
3. **Undocumented behaviour required.** Correct completion depends on a behaviour that no
   governing source defines.
4. **Production value required.** Progress would require inventing addresses, coordinates,
   limits, setpoints, thresholds, or timeouts.
5. **Sensitive data encountered.** Anything in the public-boundary prohibited list appears
   in the working tree, in a diff, or in tool output. Do not commit it; report it.
6. **Device or network access requested.** Any instruction that would require connecting
   to plant equipment or production systems.
7. **Destructive Git requested.** See section 7.
8. **Evidence unavailable.** A required check cannot be executed or its output cannot be
   observed.
9. **Merge or merge-adjacent request.** Any instruction to merge, rebase, or rewrite
   approved history (see section 8).
10. **Stage completion ambiguous.** It is unclear whether the current stage is complete.

## 12. Safety position of the agent

The WaterJet Sentinel Suite is a monitoring and supervisory control system. It is **not** a
Safety Instrumented System and does not replace emergency stop circuits, hardwired
protection, motor protection, mechanical limits, or controller-side safe-stop behaviour.
No agent may present the system, its documentation, or its tests as safety-rated. See
[`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) and
[`docs/CONTROL_AUTHORITY.md`](docs/CONTROL_AUTHORITY.md).

---

## Related documents

- [`README.md`](README.md) — repository entry point and document index
- [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) — verified state and status legend
- [`docs/PUBLIC_REPOSITORY_BOUNDARY.md`](docs/PUBLIC_REPOSITORY_BOUNDARY.md) — what may be published
- [`docs/SAFETY_BOUNDARY.md`](docs/SAFETY_BOUNDARY.md) — hardware safety boundary
- [`SECURITY.md`](SECURITY.md) — security policy and secret handling
- [`docs/decisions/README.md`](docs/decisions/README.md) — decision record index
