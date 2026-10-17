# Motion Observed-State Foundation (B2) — Software Only

**Checkpoint:** PR #31. **Surface:** B2 read-only observed-state foundation in
`adapters/motion-library`, plus the redacted Legacy baseline in
`docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md`.

**Gate position.** The Motion authorization gate is CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`). Endpoint configuration is `NOT_CONFIGURED`; the axes are
`NOT_COMMISSIONED`; the brake state is unknown; no Home exists. Planned is not authorized.

**Hardware position.** No controller was contacted while producing this checkpoint. No endpoint,
address, coordinate, command text, response value or engineering constant is committed. No NIC,
service, driver or registry setting was touched.

**Authority.** `docs/MASTER_PLAN.md` remains the roadmap of record. This document is not a stage,
not a gate and not a roadmap item; it does not reorder the critical path and does not replace the
TC work that remains the priority path.

---

## 1. What this checkpoint is

A typed, vendor-neutral model of observed motion state plus the session discipline that any future
read would have to obey, implemented so that:

- the four read candidates have a canonical, closed vocabulary;
- the model can truthfully say "unavailable" and cannot claim anything stronger;
- one session owns one observation slot;
- at most one observation is outstanding;
- one generation per session, with stale results rejected and replays refused;
- close is deterministic and idempotent;
- the observation surface cannot issue, enable or influence a command.

## 2. What this checkpoint is not

- It is **not a read**. No library is loaded, no connection is opened and no command is sent. The
  B2 sources contain no I/O call, no native import, no socket and no endpoint type.
- It is **not an authorization**. The gate stays closed and the refusal vocabulary is unchanged.
- It is **not a stage** in `docs/MASTER_PLAN.md`, and it does not authorize the roadmap's
  read-only or motion stages.
- It is **not a value model**: there is no position, limit, home, brake, fault or "safe" member,
  and no unit conversion.
- It is **not a UI, route, Snapshot member, Delta member or fixture change**.

## 3. Candidate dispositions

All four candidates are **BLOCKED**. The verified subset is empty.

| Candidate | Kind | Why it is blocked |
| --- | --- | --- |
| B2-OBS-1 | `AXIS_STATUS` | Needs the raw command-and-response path; the B1 gate authorizes six typed identity exports and states "no raw controller command, no axis read" |
| B2-OBS-2 | `COMMAND_POSITION` | Same reason |
| B2-OBS-3 | `ACTUAL_POSITION` | Same reason |
| B2-OBS-4 | `DIGITAL_OUTPUT` | Same reason |

The vendor commands behind these candidates are real and documented, and the vendor
command-and-response entry point is documented as the way to issue them. That is exactly the
problem: using it would add a seventh native export to an allow-list that the gate fixes at six,
and it would perform the axis read the gate forbids. That is an authorization decision for the
Owner, not an implementation choice for the Agent. It is reported in section 9 and in the
baseline's section G, and it is not worked around.

Because the verified subset is empty, the only reachable outcomes are:

- `UNAVAILABLE` — the subject is valid, but no authorized read surface exists. The model produces
  one state with no value, no unit and no meaning.
- `REFUSED` — the request itself was not allowed to proceed (unknown kind, unknown axis, invalid
  subject, busy slot, closed session, stale generation, replayed result). No state is produced.

`VERIFIED`, `RAW_OBSERVED`, `UNCONFIRMED` and `STALE` are declared and reserved. They are never
produced by this revision, and the focused suite pins that.

## 4. The model

| Element | Committed shape |
| --- | --- |
| Observation kinds | `AXIS_STATUS`, `COMMAND_POSITION`, `ACTUAL_POSITION`, `DIGITAL_OUTPUT` — closed, read-only list |
| Axes | `A`, `B`, `C`, `D` — closed set, diagnostic identity only |
| Output namespace | numbers 1 to 4 — the Owner-confirmed brake allocation, a numbering fact and not a polarity claim |
| Interpretation states | `VERIFIED`, `RAW_OBSERVED`, `UNCONFIRMED`, `UNAVAILABLE`, `STALE`, `REFUSED` — closed list |
| Reachable states | `UNAVAILABLE`, `REFUSED` only |
| Native units | `NONE` while no value exists; `CONTROLLER_UNITS` reserved for a future authorized read |
| State members | kind, axis, output number, interpretation state, native unit, native value text, detail code — nothing else |
| Construction | the state type has a private constructor and no public factory |

There is deliberately no member that could be read as "in position", "limit active", "home
active", "brake released", "fault" or "safe". An unknown state is not inactive, not zero and not
safe; it is unknown.

## 5. Session invariants

| Invariant | How it is enforced |
| --- | --- |
| One session owns its observation slot | All state is instance state; there is no static or shared state, so two sessions cannot observe through each other |
| At most one outstanding observation | A single slot with a fail-fast refusal; nothing is queued |
| One generation per session | The generation starts at one; a result from another generation is rejected |
| No replay | A result is accepted at most once per generation |
| No retry of a read result; no reconnect; no polling | The type has no loop, no timer, no connection, no restart path and no result cache; a second request is a new evaluation, never a replay |
| Deterministic close | `Dispose` closes the session and is idempotent; a closed session accepts nothing and never reopens |
| No command coupling | No reference to the authorization gate, the command gate, the commissioning session, the Runtime, the queue, the pump, the valve, alarms or Production |

## 6. C1 contract — documented, NOT IMPLEMENTED

C1 is the next contract. It is written down here so the Owner can decide whether to authorize it;
**no part of C1 exists in code, tests or configuration**.

### 6.1 Prerequisites

1. An Owner decision that authorizes a read surface, naming the exact native export and the exact
   command shapes (typed, fixed, no caller text).
2. Evidence for the interpretation that would be claimed: status-word bit order and polarity for
   the installed controller family, the output numbering for every cabinet, and the physical
   meaning of the brake outputs.
3. A single connection owner in the Application; no second authority, no browser-owned session.
4. A connection model that keeps B1's rules: one open, one outstanding operation, one generation,
   no retry, no reconnect, deterministic close and unload.

### 6.2 Contract shape (proposed, not agreed)

- The read surface exposes only the four typed kinds; the command text is never a parameter and
  never rendered.
- Responses are bounded, terminated and redacted; a malformed response is a refusal, never a
  zero.
- A read produces `RAW_OBSERVED` when the text is preserved verbatim, and `UNCONFIRMED` when the
  meaning depends on unproven bit or polarity evidence. `VERIFIED` requires the evidence in 6.1.
- Native units are preserved; no millimetre, percentage or "safe" value is derived.
- An observation never enables a command: the authorization gate is independent, and a closed
  gate stays closed whatever the observed state says.
- No persistence of observed state, no replay, and no history that could be read as authorization.
- No coupling to the queue, Auto, Cleaning, pump, valve, alarms or Production.

### 6.3 Status

**NOT IMPLEMENTED.** No export, no session, no parser, no test and no configuration. C1 requires
its own Owner gate, its own session branch and its own Owner-local validation.

## 7. Owner-local gate for this checkpoint

### 7.1 B1-H (unchanged, prerequisite)

`docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md` remains the runbook of record. B1-H is a separate
authorization and is executed by the Owner only, on the exact PR head, after the software gates
below pass. This checkpoint does not change B1-H, its ordering or its acceptance.

### 7.2 B2-H (Owner-local; software only; no device contact)

There is no B2 hardware operation to run, because no read is authorized. B2-H therefore contains
no device step and produces no hardware evidence; it is a software gate on the Owner workstation.

```powershell
# 0. exact head and clean worktree
git rev-parse HEAD
git status --porcelain

# 1. locked restore — must NOT create a new lock file (no project was added)
dotnet restore WaterJetSentinelSuite.sln --locked-mode
git status --porcelain

# 2. Release build, no restore
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

# 3. focused B2 suite
dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build

# 4. full suite
dotnet test WaterJetSentinelSuite.sln -c Release --no-build

# 5. repository boundary scan
node tools/boundary-scan/boundary-scan.mjs .
```

Expected results:

| Step | Acceptance |
| --- | --- |
| 1 | PASS; no new or modified `packages.lock.json`; no other change |
| 2 | 0 warnings, 0 errors (`TreatWarningsAsErrors`) |
| 3 | all focused tests pass, including the four BLOCKED dispositions and the reachable-state pin |
| 4 | all existing tests pass, unchanged |
| 5 | `0 findings (S1-S9 clean)` |

Additional proofs to attach:

```powershell
Get-Process | Where-Object { $_.Name -match 'gclib|gcaps|gdk|galil' }   # expected: no rows
Get-NetTCPConnection | Where-Object { $_.OwningProcess -eq $PID }        # expected: no new external connection
Select-String -Path adapters/motion-library/MotionObservation*.cs -Pattern 'DllImport|LoadLibrary|Socket|HttpClient'
# expected: no rows (the B2 surface has no I/O)
git status --porcelain
```

Acceptance for B2-H: steps 0 to 5 PASS, the process and network sighting is clean, the B2 surface
still contains no I/O call, and the worktree contains only the authorized PR #31 changes. A PASS
means the **software** foundation is accepted for this checkpoint; it says nothing about any
controller, and it does not open the Motion gate.

### 7.3 Order

1. B1-H on the exact PR head, per its own runbook, if and only if the Owner has authorized it.
2. B2-H as above.
3. A separate Owner decision on C1 (section 6) before any read is designed.

None of the three steps is performed by the Agent.

## 8. Validation performed here

**Performed in Arena:** source-level review of every commit in this PR; the Legacy baseline's
completeness checks (section A of the baseline); a repository-wide boundary scan attempt;
machine-checked source assertions written as tests (not executed); `git diff` review; proof that
no project, package pin or lock file changed.

**NOT RUN in Arena:** the .NET build, the focused suite, the full suite and the locked restore.
The Agent environment has no .NET SDK, and no official SDK or NuGet endpoint is reachable from it.
No compile or test result is claimed.

**Not verified anywhere:** every controller fact in the baseline's section G.

## 9. STOP items reported to the Owner

1. **A read surface does not exist and must not be improvised.** All four candidates are BLOCKED;
   the verified subset is empty; the model refuses rather than guesses.
2. **A seventh native export is an authorization change.** It needs its own gate and its own
   Owner-local validation; it cannot ride on PR #31.
3. **B2-H has no hardware step.** It is a software gate by construction, because no B2 operation
   is authorized.
4. **No dependency, project or lock change was made.** A new project would create a genuine lock
   and require an Owner-local restore; nothing about it would authorize a read.

## 10. Changed / Unchanged / Not Verified

**Changed:** three source files in `adapters/motion-library` (vocabulary, state model, session);
two focused test files; this document; the redacted Legacy baseline; the adapter README; the
CHANGELOG entry.

**Unchanged:** the Motion authorization gate and its refusal vocabulary; the commissioning
contracts and Application model; the topology catalogue and every Owner-confirmed mapping; the B1
identity surface and harness; the native probe surface; the Runtime composition; the Kiosk; the
Preview and its routes; every project file, package pin and lock file; every fixture; the
network posture.

**Not verified:** any compilation or test execution; any controller connection or read; any axis,
brake, limit, home or fault fact; any unit conversion; any timing.
