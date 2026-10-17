# B2-H One-Shot Read-Only Observation Trial — Owner-Local Runbook

**Checkpoint:** PR #31. **Surface:** the B2-H executable composition — the one-shot, read-only
observation trial and its Owner-invoked console harness
`apps/motion-observation` (`Wjss.MotionObservation`).

**Gate position.** The Motion authorization gate is CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`). Endpoint configuration is `NOT_CONFIGURED`; the axes are
`NOT_COMMISSIONED`; the brake state is unknown; no Home exists.

**Hardware position.** Producing this checkpoint contacted no controller. No endpoint, serial,
native path, file hash, request text, response value or engineering constant is committed. The
Agent loaded no library, opened no handle and started no service. Physical execution of this
trial is an Owner-local act under a separate authorization.

**Authority.** `docs/MOTION_OBSERVED_STATE_FOUNDATION.md` remains the B2 checkpoint of record and
`docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md` remains the B1 runbook of record. This document
adds the executable composition and does not change either gate.

---

## 1. What the trial is

One process. One controller handle. One observation session. One deterministic pass over the
fixed sixteen typed read intents. Then: invalidate the read boundary, close the handle, release
the native modules, exit.

The trial does not add a controller capability. It composes the already committed B2 read boundary
with the already committed B1 lifecycle (proof, direct/no-gcaps posture, one open, canonical open
classification, deterministic close and unload) and executes that composition exactly once.

It has no retry, no reconnect, no replay, no polling, no timer, no background reader, no second
session owner, no second controller handle and no second open. A second `Run()` on the same
instance is refused with `OBSERVATION_TRIAL_ALREADY_RUN` and performs no native call.

## 2. What the trial is not

- Not an authorization. A completed trial does not open the Motion gate, does not commission an
  axis and does not confirm a brake state.
- Not a motion or control surface. There is no write, stop, jog, brake, home, position-define,
  move, auto-clean, program-array or output-mutation operation anywhere in the composition, and
  none can be formed: the read plan is a closed literal table of sixteen typed intents.
- Not a general command API. There is no string parameter on the trial type, no request
  reconstruction, no format call and no concatenation on the read path. No caller-supplied text —
  not from arguments, environment variables, configuration files, standard input, a UI, JSON,
  reflection, logging or the network — can reach a request.
- Not discovery. Nothing enumerates, pings, assigns or probes an address. The single address is
  the one validated Owner-local value.
- Not a service. No socket listener, no port, no route, no Snapshot, Delta, Kiosk, Preview or
  Production integration, no persistence of observed state.
- Not a value model. No millimetre, percentage, limit polarity, Home, fault, brake or
  motion-complete statement is produced, and `VERIFIED` is unreachable.

## 3. Prerequisites

1. **B1-H complete and Owner-confirmed** for the same controller, with the observed revision
   recorded locally (`DMCB140 Rev 1.0c2`, expected hardware `DMC-B140-M`).
2. **B2-H(S)** software gate PASS on the exact PR head (section 7).
3. **Explicit Owner authorization naming this trial.** Physical execution is not implied by
   B1-H, by B2-H(S) or by this document.
4. The controller is in the state the Owner intends for a read-only observation, and the Owner
   has decided that a single open with no motion command is safe for the machine.

## 4. Owner-local configuration (environment only; values never committed)

The trial resolves the same two Owner-local sources as the B1 identity harness, through the same
validated resolvers. No other input exists.

| Source | Purpose | Committed? |
| --- | --- | --- |
| The three approved native-library variables (directory, module paths, expected version) | Locate and prove the approved native modules (Gate A) | Never |
| The one approved endpoint variable, `WJSS_MOTION_CONTROLLER_01_ENDPOINT` | The single unicast controller address | Never |

The endpoint resolver accepts exactly one canonical dotted-quad host address: four decimal octets,
no leading zeros, no port, no scheme, no whitespace and no option text. Anything else is refused
before a module is loaded. Option text is never appended to the address — the vendor entry for
`gclib_open` (printed page 48) documents the second parameter as the controller address token, and
direct/no-gcaps is selected separately and process-wide by `gclib_force_gcaps(false)`.

The harness prints only the redacted statement `ENDPOINT_CONFIGURED`. It never prints the value.

## 5. The fixed sixteen intents (deterministic order)

The plan is a closed table. It is executed once, in this order, and cannot be extended, reordered
or parameterized at run time. Only the subjects are listed here; the request literals live only in
the immutable plan inside the adapter.

| Position | Kind | Subject | Reported interpretation |
| --- | --- | --- | --- |
| 1-4 | `AXIS_STATUS` | axes A, B, C, D | `UNCONFIRMED` |
| 5-8 | `COMMAND_POSITION` | axes A, B, C, D | `RAW_OBSERVED` |
| 9-12 | `ACTUAL_POSITION` | axes A, B, C, D | `RAW_OBSERVED` |
| 13-16 | `DIGITAL_OUTPUT` | outputs 1, 2, 3, 4 | `UNCONFIRMED` |

Reading 1 is issued first and reading 16 last. There is no batching, no interleaving and no
concurrent request: the session holds one observation slot and refuses a second outstanding
observation.

## 6. Lifecycle, in order

1. **Validate the invocation.** Any argument is refused with exit code 4 before anything is read.
2. **Resolve the Owner-local native configuration.** Absence or rejection refuses the trial.
3. **Resolve the Owner-local endpoint** through the validated resolver. Absence or rejection
   refuses the trial. No module is loaded before both resolutions succeed.
4. **Gate A proof.** The approved native identity/version proof must pass. A refusal stops the
   trial with `NATIVE_PROOF_REFUSED`; no module is loaded and no controller is contacted.
5. **Load only the approved modules** through the restricted loader, companion first, and prove
   each loaded path against its approved location. A mismatch stops the trial.
6. **Resolve only the four authorized exports**: `gclib_force_gcaps`, `gclib_open`,
   `gclib_close` and the single scoped read export. A missing lifecycle export or a missing read
   export refuses the trial before any open (`OBSERVATION_BOUNDARY_UNAVAILABLE` for the read
   export).
7. **`gclib_force_gcaps(false)`** — the process-wide, irreversible direct/no-gcaps selection —
   before the open.
8. **Exactly one `gclib_open`** with the validated bare address and nothing else. A non-success
   result is classified with the canonical B1 open-result word; there is no retry and no
   reconnect. A handle that arrives with a refused open is still owned and is closed on the
   cleanup path.
9. **One observation session bound to that handle and that export.** Exactly sixteen typed
   intents, once, in plan order.
10. **Bounded raw results only.** The first refused read stops the remaining sequence and keeps
    its own canonical code; nothing is repaired, rounded, clamped, retried or replayed.
11. **Invalidate the observation boundary** (no native call), then **close the controller
    handle**, then **release the native modules** — in that order, on every post-open path.
12. **Exit** with the outcome and its cleanup truth.

No secret is written into the process environment by the trial, and the trial removes no variable
it did not create. The Owner-local variables remain the Owner's, outside the repository.

## 7. Owner-local validation gate (B2-H(S)) — software tier, no device contact

Run on the exact PR head, in an isolated process, from the repository root.

```powershell
# 0. exact head and clean worktree
git rev-parse HEAD
git status --porcelain

# 1. restore - this checkpoint ADDS one project, so ONE new lock file is expected
dotnet restore WaterJetSentinelSuite.sln
git status --porcelain
# expected: exactly one new file, apps/motion-observation/packages.lock.json
#           (no other lock file may change); commit that genuine generated file

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
| 1 | PASS; exactly one NEW lock file at `apps/motion-observation/packages.lock.json`; no existing lock file modified |
| 2 | 0 warnings, 0 errors (`TreatWarningsAsErrors`) |
| 3 | every focused test passes, including the trial composition suite and the invocation-boundary suite |
| 4 | every existing test passes |
| 5 | `0 findings (S1-S9 clean)` |

After that file is committed, a `--locked-mode` restore is the check of record for every
subsequent change. The lock file is generated by the Owner-local restore and committed by the Owner. It is never
hand-authored and never fabricated in the Agent environment; `LOCK CHANGES` is reported as
`BLOCKED PENDING OWNER` until that genuine file exists on the branch.

**Status (2026-10-17).** The genuine lock file now exists on the branch
(`apps/motion-observation/packages.lock.json`, Owner commit `2d2ebe41`), so the `BLOCKED PENDING
OWNER` position is closed. It was not regenerated, rewritten or reformatted by the Agent, and no
other lock file changed. Every later change uses `--locked-mode`.

Additional proofs to attach:

```powershell
Get-Process | Where-Object { $_.Name -match 'gclib|gcaps|gdk' }             # expected: no rows
Get-NetTCPConnection | Where-Object { $_.OwningProcess -eq $PID }            # expected: none
Select-String -Path adapters/motion-library/MotionObservationTrial*.cs -Pattern 'DllImport|LoadLibrary|Socket|HttpClient'
# expected: no rows
git status --porcelain
```

## 8. Vendor-service posture and NIC sighting (before and after the trial)

Record both sighting blocks in the evidence note, on the exact machine used:

```powershell
Get-Service | Where-Object { $_.Name -match 'gcaps|gclib' } | Select-Object Name, Status, StartType
Get-NetAdapter | Select-Object Name, Status, LinkSpeed
Get-NetIPAddress -AddressFamily IPv4 | Select-Object InterfaceAlias, IPAddress, PrefixLength
Get-NetTCPConnection -State Established | Select-Object RemoteAddress, RemotePort, OwningProcess
```

Expected posture: no vendor helper service is required by this trial (direct/no-gcaps is selected
in-process), the controller link is up on the intended interface only, and no other process holds
an established connection to the controller. If a vendor helper service is running, record it and
decide — it is not required and it must not be started or stopped by the Agent.

Start no service, change no NIC setting, install nothing. The sighting is evidence, not
configuration.

## 9. Failure classification (canonical words; no numeric vendor value is ever printed)

| Condition | Code |
| --- | --- |
| Invocation carried arguments | exit 4, `INVALID_INVOCATION` |
| Native configuration missing or rejected | `MOTION_NATIVE_*` refusal from the resolver, exit 2 |
| Endpoint missing or rejected | `MOTION_ENDPOINT_NOT_CONFIGURED` / `MOTION_ENDPOINT_REJECTED`, exit 2 |
| Gate A proof refused | `NATIVE_PROOF_REFUSED` |
| Approved module absent or path unproven | `NATIVE_PROOF_REFUSED` |
| Authorized lifecycle export absent | `NATIVE_PROOF_REFUSED` |
| Read export absent | `OBSERVATION_BOUNDARY_UNAVAILABLE` |
| Direct/no-gcaps call failed | `DIRECT_MODE_NOT_ENFORCED` |
| Open refused | `OPEN_INVALID_ARGUMENT`, `OPEN_INTERNAL_ERROR`, `CONNECTION_TIMEOUT`, `OPEN_COMMAND_ERROR`, `OPEN_BUFFER_TOO_SMALL`, `OPEN_NOT_CONNECTED`, `OPEN_NOT_SUBSCRIBED`, `OPEN_RESULT_UNRECOGNISED` |
| Open threw before a result | `CONNECTION_FAILED` |
| Open succeeded but returned no handle | `CONNECTION_HANDLE_INVALID` |
| A typed read refused | that read's canonical code (for example `OBSERVATION_RESPONSE_UNTERMINATED`) |
| Close not confirmed after a full pass | `CLEANUP_UNPROVEN` |
| Module release not confirmed after a full pass | `NATIVE_UNLOAD_FAILED` |
| Second `Run()` on one instance | `OBSERVATION_TRIAL_ALREADY_RUN` |
| Unexpected managed failure before a result existed | `OBSERVATION_TRIAL_ABORTED` (cleanup still ran; no stack trace, no value) |
| Full pass and proven cleanup | `OBSERVATION_TRIAL_COMPLETE`, exit 0 |

A refusal never carries a partial pass as complete, never fabricates a value and never reports a
stale one. The cleanup truth (`observationBoundaryInvalidated`, `controllerHandleClosed`,
`nativeHandlesReleased`) is reported as measured, on every path.

## 10. Exit codes and output shape

| Exit | Meaning |
| --- | --- |
| `0` | Trial accepted: proof passed, one open succeeded, sixteen reads accepted in plan order, boundary invalidated, handle closed, modules released |
| `2` | Refused: configuration, proof, direct mode, open, boundary, a read, or cleanup (canonical code printed on standard error) |
| `4` | Invalid invocation: the harness takes no arguments |

Output is one redacted JSON object: `accepted`, `code`, `detail`, `expectedLogicalIdentity`
(`MC-WSB1`), `openClassification`, `generation`, the three cleanup flags, the redacted endpoint
statement, and one entry per executed intent (`ordinal`, `code`, `accepted`, `generation`, `kind`,
`axisId`, `outputNumber`, `nativeValueText`, `nativeUnit`, `interpretationState`, `detailCode`).

It contains no endpoint, serial, native path, hash, request text, vendor error string or managed
exception detail. `evidence/` notes must not add any.

## 11. Interpretation rules

- `RAW_OBSERVED` means the bounded native text is preserved and no engineering meaning is adopted.
- `UNCONFIRMED` means the value is preserved and the meaning depends on bit-order, polarity or
  wiring evidence that is not commissioned for this machine.
- `UNAVAILABLE` means the subject is valid but no usable value exists.
- `REFUSED` means the request itself was not allowed to proceed.
- `VERIFIED` is never produced by this trial, and `STALE` stays reserved.

No statement about limit switches, Home, faults, brake engagement, motion completion, position
tolerance or safety may be derived from these readings.

## 12. Evidence template (Owner-local; committed values must stay redacted)

```text
B2-H EXECUTION EVIDENCE
date (local)                 :
branch / head SHA            :
.git status --porcelain      : (expected: empty)
dotnet restore               : (expected: one new lock file only, then committed)
dotnet build -c Release      : (expected: 0 warnings, 0 errors)
dotnet test (focused)        : (expected: all pass, N tests)
dotnet test (full)           : (expected: all pass)
node tools/boundary-scan     : (expected: 0 findings (S1-S9 clean))
vendor service sighting      : (names and status only; no paths)
NIC sighting                 : (interface alias/link state only; no address values)
process sighting             : (expected: no vendor processes)
Gate A                       : PASS / REFUSED
endpoint                     : ENDPOINT_CONFIGURED  (never the value)
open classification          : GCLIB_SUCCESS / <canonical refusal word>
generation                   : (integer only)
sixteen readings             : (ordinal / kind / subject / state / code only; value text redacted)
boundary invalidated         : YES / NO
controller handle closed     : YES / NO
native modules released      : YES / NO
overall result               : ACCEPTED / REFUSED <CODE>
motion authorization gate    : CLOSED (unchanged)
candidate values             : NOT_CONFIGURED / NOT_COMMISSIONED
notes                        : (no endpoint, serial, path, hash or request text)
```

**Value-text exception (Owner-authorized).** The Owner may authorize publishing the non-identifying
native value text of the sixteen readings as part of a closeout; the committed baseline
`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md` section I does exactly that. Nothing else is
relaxed: the endpoint, the serial number, a host, a workstation path, a native path, a file hash, the
raw JSON envelope, request text and vendor error text remain prohibited without exception, and a
recorded value never becomes an interpretation.

## 13. STOP conditions

Stop and report, without editing code, if any of the following is observed:

- the committed read plan differs from the fixed sixteen intents in section 5;
- the trial requires any command text, axis string, output number or endpoint to arrive from a
  caller, an argument, an environment value outside the approved variable, a file or a network
  input;
- the trial requires a write, a motion, a configuration change, a service start or a second open;
- the gate classification, the cleanup ordering or the redaction rules would have to be weakened;
- any dependency, package pin or lock file other than the single generated
  `apps/motion-observation/packages.lock.json` would change.

## 14. Scope statement

The Agent authored this executable composition, its tests and this document. The Agent did not
compile it, did not test it, did not contact hardware, did not load the native library, did not
open a controller and did not open the Motion gate.

`SOURCE AUTHORED — YES`; `STATIC CHECKS — PASS`; `COMPILE — NOT VERIFIED BY AGENT`;
`TESTS — NOT VERIFIED BY AGENT`; `IMPLEMENTATION COMPLETE — NOT CLAIMED BY AGENT`.
