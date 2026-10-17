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

**Vendor contract record.** The interface contract this surface depends on is recorded in
`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md` — the three-layer separation, the verified
contracts, the open-address misuse rule, the result order and the command registry. That document is
evidence only; it is not executable authority and it authorizes no command.

---

## 1. What this checkpoint is

A typed, vendor-neutral model of observed motion state, the session discipline that governs it,
and ONE private internal read boundary over a non-empty verified subset, implemented so that:

- the four read candidates have a canonical, closed vocabulary;
- the model can truthfully say "unavailable" and cannot claim anything stronger;
- the read boundary is typed, immutable, length-bounded and unreachable from outside the adapter;
- one session owns one observation slot;
- at most one observation is outstanding;
- one generation per session, with stale results rejected and replays refused;
- close is deterministic and idempotent;
- the observation surface cannot issue, enable or influence a command.

## 2. What this checkpoint is not

- It is **not a general command API**. The read boundary accepts a typed intent only; it has no
  string, buffer or handle parameter, no request text in any public member, and no write, control
  or motion operation in its table.
- It is **not an authorization**. The gate stays closed, the endpoint stays `NOT_CONFIGURED`, the
  axes stay `NOT_COMMISSIONED`, and an observation never enables a command.
- It is **not a stage** in `docs/MASTER_PLAN.md`, and it does not authorize the roadmap's
  read-only or motion stages.
- It is **not a value model**: there is no position, limit, home, brake, fault or "safe" member,
  and no unit conversion.
- It is **not a UI, route, Snapshot member, Delta member or fixture change**.
- The three model sources still contain no I/O call, no native import, no socket and no endpoint
  type. The single native call lives in one separate internal boundary file.

## 3. Candidate dispositions

All four candidates are **VERIFIED_FOR_B2** and implemented. The verified subset is non-empty and
complete for this checkpoint.

| Candidate | Kind | Read shape | Interpretation |
| --- | --- | --- | --- |
| B2-OBS-1 | `AXIS_STATUS` | status-word read over the message command | `UNCONFIRMED` |
| B2-OBS-2 | `COMMAND_POSITION` | first per-axis position read | `RAW_OBSERVED` |
| B2-OBS-3 | `ACTUAL_POSITION` | second per-axis position read | `RAW_OBSERVED` |
| B2-OBS-4 | `DIGITAL_OUTPUT` | output-array read, outputs 1-4 | `UNCONFIRMED` |

**The authority split that makes this possible, stated once.** The B1 identity surface is
unchanged: exactly six typed identity exports, no raw controller read and no public command
surface. The B2 observed-state surface is separately authorized to resolve ONE further
command-and-response export behind a private, internal, typed boundary. That boundary is not a
widening of the B1 allow-list and it is not a public API:

- an intent is a **type** from a closed set of sixteen;
- the request is a **literal** from an immutable table, so there is no interpolation and no
  caller-supplied text anywhere on the path;
- the subject domain is closed: axis A-D, outputs 1-4;
- the request length and the response buffer are **fixed**; the reply has to be terminated,
  printable and one bounded decimal token; an output state has to be exactly zero or exactly one;
- an empty, truncated, non-printable, multi-value, error-marked, hexadecimal or oversized reply is
  a **refusal word**, never a repaired, rounded or truncated value;
- no write, stop, jog, brake, home, define, move, auto, program-array or output-mutation mnemonic
  exists in the table, so none can be formed;
- the boundary is internal, is never returned by a public member, and holds no module handle: it
  is invalidated before the lifecycle owner closes the controller handle and unloads the module.

The earlier reading of the B1 sentence "no raw controller command, no axis read" as a permanent
prohibition of every B2 read was wrong. It is a statement about the B1 surface. The correction is
recorded in the evidence baseline sections B.4, G.2 and H.

**Interpretation stays weaker than the read.** A bounded, validated read proves the VALUE; it does
not prove what the value means on this machine. The two position candidates keep their native
value with no meaning adopted (`RAW_OBSERVED`), and the status word and output state keep a value
whose machine meaning is not commissioned (`UNCONFIRMED`). `VERIFIED` stays unreachable: no
millimetre conversion, no limit polarity, no Home, no fault, no brake and no motion-complete
meaning is produced. The reachable outcome words are therefore:

- `RAW_OBSERVED` / `UNCONFIRMED` — one bounded native value was accepted.
- `UNAVAILABLE` — the subject is valid but no read boundary is attached, or the reply was not
  usable. The model produces one state with no value, no unit and no meaning.
- `REFUSED` — the request itself was not allowed to proceed (unknown kind, unknown axis, invalid
  subject, boundary closed or unbound, native failure, busy slot, closed session, stale
  generation). No state is produced.

`VERIFIED` and `STALE` are declared and reserved. They are never produced as state words, and the
focused suite pins that.

## 4. The model

| Element | Committed shape |
| --- | --- |
| Observation kinds | `AXIS_STATUS`, `COMMAND_POSITION`, `ACTUAL_POSITION`, `DIGITAL_OUTPUT` — closed, read-only list |
| Axes | `A`, `B`, `C`, `D` — closed set, diagnostic identity only |
| Output namespace | numbers 1 to 4 — the Owner-confirmed brake allocation, a numbering fact and not a polarity claim |
| Interpretation states | `VERIFIED`, `RAW_OBSERVED`, `UNCONFIRMED`, `UNAVAILABLE`, `STALE`, `REFUSED` — closed list |
| Reachable states | `RAW_OBSERVED`, `UNCONFIRMED`, `UNAVAILABLE`, `REFUSED` — `VERIFIED` and `STALE` stay reserved |
| Native units | `NONE` while no value exists; `CONTROLLER_UNITS` for both position reads; `STATUS_BITS` for the status word; `LOGICAL_STATE` for an output |
| State members | kind, axis, output number, interpretation state, native unit, native value text, detail code — nothing else |
| Construction | the state type has a private constructor and no public factory; only the internal unavailable and observed factories can build one |

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
| Typed, immutable read plan | Sixteen closed intents and sixteen literal requests; the table is a read-only wrapper and cannot be extended at run time |
| Fixed bounds | One request-length bound checked before the call, one fixed response buffer, one accepted value-text length |
| One native site | The single native call lives in one internal boundary file; the three model sources contain no I/O call |
| Close invalidates the boundary first | The boundary is invalidated before the lifecycle owner closes the controller handle and unloads the module, and it can never be used again |

## 6. C1 contract — documented, NOT IMPLEMENTED

C1 is the next contract. It is written down here so the Owner can decide whether to authorize it;
**no part of C1 exists in code, tests or configuration**.

### 6.1 Prerequisites

1. **RESOLVED — authorized for the B2 layer.** The Owner authorized a private read surface,
   naming the exact native export and the exact typed, fixed, caller-text-free request shapes;
   the evidence baseline section H records it. C1 is the next layer and needs its own decision.
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

### 7.2 B2-H (Owner-local; two tiers, kept separate)

B2-H is separate from B1-H and has two tiers. **B2-H(S)** is the software gate below and runs
first; **B2-H(R)** is a read-only device trial that may run only after a separate Owner
authorization, and only with B1-H complete.

**B2-H(S) — software gate, no device contact**

```powershell
# 0. exact head and clean worktree
git rev-parse HEAD
git status --porcelain

# 1. locked restore - must NOT create a new lock file (no project was added)
dotnet restore WaterJetSentinelSuite.sln --locked-mode
git status --porcelain

# 2. Release build, no restore
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

# 3. focused suite
dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build

# 4. full suite
dotnet test WaterJetSentinelSuite.sln -c Release --no-build

# 5. repository boundary scan
node tools/boundary-scan/boundary-scan.mjs .
```

| Step | Acceptance |
| --- | --- |
| 1 | PASS; no new or modified `packages.lock.json` for THIS checkpoint (it adds no project). The later B2-H executable checkpoint DOES add one project and therefore expects exactly one new generated `apps/motion-observation/packages.lock.json` — see `docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md` |
| 2 | 0 warnings, 0 errors (`TreatWarningsAsErrors`) |
| 3 | every focused test passes, including the sixteen-literal plan, the parser refusal table, the four verified dispositions and the negative-space boundary tests |
| 4 | every existing test passes |
| 5 | `0 findings (S1-S9 clean)` |

Additional proofs to attach:

```powershell
Get-Process | Where-Object { $_.Name -match 'gclib|gcaps|gdk|galil' }   # expected: no rows
Get-NetTCPConnection | Where-Object { $_.OwningProcess -eq $PID }        # expected: no new external connection
Select-String -Path adapters/motion-library/MotionObservation*.cs -Pattern 'DllImport|LoadLibrary|Socket|HttpClient'
# expected: no rows (the three model sources still have no I/O call)
git status --porcelain
```

The executable form of this tier — the one-shot observation harness and its lifecycle — is
specified in `docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md`.

**B2-H(R) — read-only device trial, separate authorization, Owner only**

Prerequisites: B1-H complete and the physical identity Owner-confirmed; B2-H(S) PASS on the exact
PR head; an explicit Owner authorization naming the trial. The trial may issue only the verified
subset. It may not configure, home, move, jog, brake, define a position, clear an output or change
any parameter. No retry, no reconnect, no polling loop: one bounded sequence, one open, one close.
It is not run by the Agent, and nothing in this checkpoint records a hardware result.

**Acceptance for B2-H(S):** steps 0 to 5 PASS, the process and network sighting is clean, the
three model sources still contain no I/O call, and the worktree contains only the authorized
PR #31 changes. A PASS means the **software** surface is accepted; it says nothing about any
controller, and it does not open the Motion gate.

### 7.3 Order

1. B1-H on the exact PR head, per its own runbook, if and only if the Owner has authorized it.
2. B2-H(S) software gate on the same head.
3. B2-H(R) read-only trial only under a separate Owner authorization.
4. A separate Owner decision on C1 (section 6) before any application contract is designed.

None of the four steps is performed by the Agent.

## 8. Validation performed here

**Performed in Arena:** source-level review of every commit in this PR; the Legacy baseline's
completeness checks (section A of the baseline); a repository-wide boundary scan attempt;
machine-checked source assertions written as tests (not executed); `git diff` review; proof that
no project, package pin or lock file changed.

**NOT RUN in Arena:** the .NET build, the focused suite, the full suite and the locked restore.
The Agent environment has no .NET SDK, and no official SDK or NuGet endpoint is reachable from it.
No compile or test result is claimed.

**Not verified anywhere:** every controller fact in the baseline's section G.

## 9. STOP items reported to the Owner — status

1. **RESOLVED BY OWNER DECISION.** A read surface now exists, but only as the private, typed,
   bounded boundary described in section 3 and the evidence baseline section H. It cannot be
   improvised into a general command API, and it exposes no write, control or motion operation.
2. **RESOLVED AS A SCOPED EXCEPTION.** The B1 allow-list is still exactly six exports. The B2
   surface resolves exactly one further export through its own internal file, with an exact-path,
   exact-token source-boundary exception that cannot be widened.
3. **DEFINED.** B2-H now has a software tier and a separately authorized read-only device tier;
   neither is executed by the Agent.
4. **CONFIRMED for this checkpoint.** No dependency, project or lock change was made, and none
   is needed here. The later B2-H executable composition adds one console project and therefore
   requires exactly one genuine Owner-local generated lock file; that is recorded separately in
   `docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md`.

## 10. Changed / Unchanged / Not Verified

**Changed:** five source files in `adapters/motion-library` (vocabulary, state model, session, the
new internal read plan and the new internal native boundary); four focused test files plus the
scoped-exception test helper; this document; the redacted Legacy baseline; the adapter README; the
CHANGELOG entry.

**Unchanged:** the Motion authorization gate and its refusal vocabulary; the B1 identity surface,
its six-export allow-list and its harness; the native probe surface; the commissioning contracts
and Application model; the topology catalogue and every Owner-confirmed mapping; the Runtime
composition; the Kiosk; the Preview and its routes; every project file, package pin and lock file;
every fixture; the network posture.

**Not verified:** any compilation or test execution; any controller connection or read; any axis,
brake, limit, home or fault fact; any unit conversion; any timing.

**Status line.** `COMPILE — NOT VERIFIED BY AGENT`; `TESTS — NOT VERIFIED BY AGENT`;
`IMPLEMENTATION COMPLETE — NOT CLAIMED BY AGENT`.

---

## 11. Closeout pointer (2026-10-17)

Additive record; sections 1-10 above describe the position at their own dated checkpoint and are
not rewritten.

- The vendor interface contract behind this surface is now committed as
  `docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`: three layers kept separate, eight verified
  library contracts plus three supporting entries, the open-address misuse rule with the Owner A/B
  observation, the result order 0-7, the API/command registry and the mandatory intake protocol.
- **B1-H PASS** and **B2-H PASS** (16 of 16 reads) are recorded there in redacted form, with the
  native value text the Owner authorized publishing. Neither result opens the Motion gate: it stays
  CLOSED, the endpoint stays `NOT_CONFIGURED` and the axes stay `NOT_COMMISSIONED`.
- C1 is unchanged: still documented in section 6, still **NOT IMPLEMENTED**, and still requiring its
  own Owner authorization. Every C1 candidate in the registry is
  `BLOCKED_PENDING_VENDOR_EVIDENCE` and has no executable form anywhere in the repository.
