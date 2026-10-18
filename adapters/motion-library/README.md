# WJSS Motion Library Adapter — Native Library Boundary (Gate A + B1 + B2 + C1 Stop-only + Manual Motion Commissioning)

**Surfaces:** Native Load / Version Probe Software Gate (Gate A), B1 one-controller identity,
B2 read-only observed state, the separate C1 Stop-only foundation merged in PR #32, and the
WSB1 Manual Motion Commissioning foundation proposed in PR #34. The adapter carries strictly
separated surfaces: Gate A library inspection/loading only; B1 identity with one direct connection
attempt and three typed reads; B2's fixed read-only observation boundary; the internal C1 A/B Stop
plan and owner; and an internal, Owner-invoked manual-motion owner for WSB1 Axis A (Horizontal)
and Axis B (Vertical) only. The only production composition that opens for C1 Stop is the isolated
`apps/motion-stop-trial` console harness. The only production composition for the manual-motion
surface is the isolated `apps/motion-commissioning` application. Neither is Runtime/API/Kiosk/
Preview composition, and neither opens the application Motion gate. **Validation:** SOURCE-AUTHORED
IN ARENA; NOT COMPILED IN ARENA — the Owner-local .NET build is the validation of record; no native
DLL was loaded and no Controller was contacted here.

## What this adapter is

A minimal, vendor-neutral, fail-closed boundary that proves — on the Owner workstation, by
Owner invocation only — that the Owner-installed motion communication library:

1. exists in the **approved Owner-local directory** (environment configuration only; the value is
   confidential and never committed);
2. is bit-identical with the **approved expected SHA-256 identities** of both module files
   (`gclib.dll` and its companion `gclibo.dll`);
3. is **x64** (PE machine `0x8664`) for both modules, proven before any load;
4. **loads from the approved absolute path** with a restricted search set (module load directory +
   application directory + System32 only — never the current directory, user directories, or the
   machine-wide PATH, so the same-named copies known to exist in other vendor tool folders can
   never be selected);
5. exports the **single authorized function** `gclib_version` — a library-local version query that
   needs no controller handle, opens no controller connection and contacts no endpoint;
6. reports a version that **equals the Owner-configured expected version** exactly;
7. and releases every native handle deterministically.

On the probe surface, only `gclib_version` is resolvable: no open, close, discovery, address
enumeration, command, callback, subscription, revision, serial or status API can be reached
through it, and the probe kernel interface exposes only load / resolve-one-export / module-path /
invoke-version / release. On the B1 identity surface, exactly six exports are resolvable
(`gclib_force_gcaps`, `gclib_open`, `gclib_close`, `gclib_connection_type`,
`gclib_revision_information`, `gclib_serial_number`) and the sibling kernel interface exposes
only the invocation shapes those declarations require. Neither surface can resolve a raw-command,
discovery, subscription, program/array/firmware or gcaps-version API, and the source-boundary
tests pin both allow-lists exactly. Both surfaces reuse the one restricted loader
(`NativeMethods`): there is no second loader.

## What it is NOT (strictly out of scope for this Gate)

- No controller endpoint, NIC change, discovery, connection, read or command of any kind.
- No session, polling, reconnect or handle ownership; the runner owns nothing beyond the call.
- No Snapshot/Delta motion state, no route, no UI, no browser surface.
- No gcaps, no vendor GUI tool, no PATH dependence, no registry search.
- No native binary, header, import library or vendored wrapper in Git (enforced by `.gitignore`
  rules and the source-boundary tests).
- The Motion authorization gate stays CLOSED; the catalogue, mappings and candidate values are
  untouched.

## Files

| Path | Role |
| --- | --- |
| `MotionLibraryProbeVocabulary.cs` | Shape-only commits: environment variable NAMES, module file names, the one authorized export name, the refusal vocabulary. |
| `MotionLibraryProbeConfiguration.cs` | Redacted configuration record; fail-closed resolver (no I/O); file-shape inspection helpers (hash parse/compare, bounded SHA-256, PE machine scan, path containment). |
| `MotionLibraryProbeOutcome.cs` | One redacted outcome: `Accepted`, one vocabulary code, redacted detail, version only on acceptance, truthful native-handle-release state. |
| `NativeKernel.cs` | Internal OS loading surface (kernel32 `LoadLibraryExW`/`GetProcAddress`/`GetModuleFileNameW`/`FreeLibrary` with a restricted flag set) behind a narrow internal interface. |
| `MotionLibraryProbeRunner.cs` | The ordered, fail-closed proof: existence → identity → architecture → companion load → module-path proof → primary load → module-path proof → single export → one bounded version call → version equality → deterministic unload. |
| `MotionObservedStateVocabulary.cs` | B2 shape only: the four typed observation kinds and candidate identifiers, the closed axis and output sets, the six interpretation states (four reachable, `VERIFIED` reserved), the native-unit words, the acceptance words and the refusal vocabulary. No vendor command mnemonic and no command builder. |
| `MotionObservedState.cs` | B2 model: the state (private constructor, no public factory, no position/limit/home/brake/fault/safe member), the typed request and the redacted outcome. |
| `MotionObservationSession.cs` | B2 session: one owner, one outstanding observation, one generation, stale and replay rejection, deterministic idempotent close. Model mode has no I/O; reading mode reaches the wire only through the internal typed boundary. |
| `MotionObservationCommandPlan.cs` | B2 plan (internal): the sixteen typed intents, the immutable literal request table, the fixed request and response bounds, and the bounded response parser. No native name, no handle and no I/O. |
| `MotionControllerCommandKernel.cs` | Shared export-name/resolution seam: B2's fixed read kernel and the separate C1 typed Stop carrier reuse the one approved native command-and-response export; neither accepts caller text. |
| `MotionStopCommandPlan.cs` | C1 Stop plan (internal): the closed A/B axis domain (`MotionStopAxis`, Axis A = Horizontal, Axis B = Vertical, default value refused) and the only two Stop templates, chosen by a switch with no caller text. |
| `MotionStopAcknowledgement.cs` | C1 Stop acknowledgement (internal): the redacted outcome vocabulary (no confirmed-stop code) and the separate reply classifier. Not the B2 numeric parser. |
| `MotionStopDispatchKernel.cs` | C1 Stop carrier (internal): one guarded native dispatch against an already-open handle and an already-resolved export, with the same signature as the B2 carrier. Invalidation makes no native call. |
| `MotionStopSession.cs` | C1 Stop session (internal): one operation slot, a generation fence, duplicate, replay and stale rejection, one dispatch per accepted operation, no retry, and a redacted outcome that never claims confirmation. |
| `MotionStopOwner.cs` | C1 Stop owner (internal): invalidates its one session, then closes the handle, then releases the primary module. Idempotent; the trial releases the companion afterward. |
| `MotionStopTrialInvocation.cs` | Isolated harness entry: exact two-argument mode/A-or-B parser, one-use consent consumption, and fail-closed endpoint/native-configuration gates. |
| `MotionStopTrialSession.cs` | One-shot C1 trial lifecycle: Gate A, restricted module/path proof, approved export resolution, one direct-mode selection, one open, one typed Stop owner/session, invalidate-close-unload cleanup. |
| `MotionStopTrialOutcome.cs` | Fixed, redacted trial vocabulary and lifecycle flags. It cannot claim Stop confirmation. |
| `ManualMotionCommandPlan.cs` | Manual-motion plan (internal): the closed WSB1 A/B axis and brake-output domains and the bounded typed templates for brake output, Jog, Begin, Home velocity/search, position/dual-zero definition, and absolute Move setup/target. There is no caller text, relative target, drive enable/disable, output-bank write, or Stop template. |
| `ManualMotionCommandKernel.cs` | Manual-motion native carrier (internal): at most one guarded dispatch for one typed plan item against an already-open handle and already-resolved approved export. Invalidation makes no native call. |
| `ManualMotionControllerOutcome.cs` | Manual-motion redacted outcome and consent vocabulary, the closed A/B read targets, and generation-bearing dispatch/read results. Outcomes carry codes only and cannot claim Stop confirmation. |
| `ManualMotionControllerInvocation.cs` | Fail-closed Owner-local invocation gate: one-use exact consent consumption, then existing Gate A and endpoint configuration resolution. It does not load or open a Controller. |
| `ManualMotionControllerOwner.cs` | One long-lived WSB1 session owner: Gate A proof, restricted companion/primary load and path proof, approved export resolution, one direct-mode selection, one open, controller-wide native-call serialization, generation fencing, reuse of the existing typed A/B Stop session, and invalidate → close → primary/companion release cleanup. |
| `Wjss.Adapters.MotionLibrary.csproj` | Grants the isolated harnesses friend-assembly access to this adapter's internal boundaries; it adds no package or dependency. |
| `apps/motion-stop-trial/Program.cs` | Console-only invocation and explicit redacted JSON projection. |
| `apps/motion-stop-trial/Wjss.MotionStopTrial.csproj` | Isolated executable project; its only project reference is this adapter. |

## C1 Stop-only software foundation (source-authored; not compiled in the Arena sandbox)

- The only C1 command is `Stop` on Axis A (Horizontal) or Axis B (Vertical). Axis C, Axis D, empty,
  combined, multi-axis, unqualified, Stop-all and caller-supplied text are refused by construction.
- The command text is one of two fixed templates in `MotionStopCommandPlan.cs`. Their family-level
  evidence is the B140 command reference; they are not firmware-specific evidence for
  `DMC-B140-M` or `DMCB140 Rev 1.0c2`.
- A dispatch that completes is reported as `STOP_DISPATCHED`. It is never reported as a confirmed
  stop: no observed state is read and no confirmation field can be set.
- Nothing is retried, reconnected, replayed or polled. Invalidation precedes handle close, and
  handle close precedes module release.
- The Owner-invoked harness is the only production composition that opens a handle for this C1
  Stop trial. It was not executed in Arena; no native DLL was loaded and no endpoint or Controller
  was contacted. Focused tests use recording fakes only. No Runtime, API, Kiosk, Operations Preview or
  Production-composition path reaches this code.

## Manual Motion Commissioning foundation, WSB1 A/B only (PR #34; software capability, not hardware authorization)

- The internal plan is closed to Axis A (Horizontal), Axis B (Vertical), brake output 1 for A and
  brake output 2 for B. It can express only the approved bounded command shapes used by the
  Application session: individual brake-output set/clear, signed Jog speed, Begin, Home speed,
  Home search, position-zero definition, dual-zero definition, acceleration, speed, deceleration,
  and an absolute target. The existing typed Stop session remains the only Stop path.
- It cannot express Servo/drive enable or disable writes, a relative-position target, an output-bank
  write, a burn/save operation, soft-limit writes, a Controller program, another cabinet, Axis C/D,
  simultaneous axes, raw caller text, or an arbitrary template.
- The owner permits one open for one long-lived session. A second open is refused; native work is
  serialized behind one controller gate; results from an earlier generation are rejected; and a
  communications fault never causes an automatic retry or a new connection.
- Reads use the already approved typed observation boundary, restricted to the status, TP/RP and
  brake-output observations for A/B. Raw values stay separate from Application interpretation.
  Unexpected, rejected, stale or out-of-range replies become codes without a fabricated value.
- The owner observes drive readiness only through the Application profile's status-bit
  interpretation. It writes no drive-readiness command.
- Cleanup is one deterministic sequence: invalidate the read, Stop and manual-motion boundaries;
  close the handle; then release the primary module and companion module. A repeated close returns
  the first measured outcome and makes no second native lifetime attempt.
- This foundation was source-authored and statically scanned in the sandbox only. It was not
  compiled or executed there, no vendor module was loaded, and no Controller or endpoint was
  contacted. Owner-local exact-head validation and a separate post-merge hardware Scope Gate are
  still required before using `apps/motion-commissioning` in hardware-capable mode.

## Owner-local configuration (environment only; values never committed)

| Variable | Shape |
| --- | --- |
| `WJSS_MOTION_LIB_DIR` | Fully qualified local directory (no UNC, no wildcard, no relative value) |
| `WJSS_MOTION_LIB_GCLIB_SHA256` | 64 hexadecimal characters — expected identity of `gclib.dll` |
| `WJSS_MOTION_LIB_GCLIBO_SHA256` | 64 hexadecimal characters — expected identity of `gclibo.dll` |
| `WJSS_MOTION_LIB_EXPECTED_VERSION` | Expected library version string (exact ordinal match) |

The values for these variables live in the Owner-local evidence for this Gate. They are never
written to disk by the adapter, never logged and never returned in any output.

The isolated C1 Stop trial also requires the existing endpoint variable
`WJSS_MOTION_CONTROLLER_01_ENDPOINT` and an exact-match, Owner-local consent assertion in
`WJSS_MOTION_STOP_TRIAL_CONSENT`. The environment-supplied assertion is a one-invocation local
confirmation, not a credential or reusable authorization service. It is checked and removed from
the trial process before configuration resolution; the Owner must also unset it in the invoking
shell after the run. Neither the endpoint nor the supplied consent value is committed, echoed,
logged or included in evidence.

The isolated Manual Motion Commissioning application has a separate exact-match consent variable,
`WJSS_MANUAL_MOTION_CONSENT`, plus `WJSS_MANUAL_MOTION_PROFILE`, which names a fully qualified
Owner-local profile file outside the repository. The application consumes the process consent
before resolving the existing endpoint and Gate A configuration; unset it in the invoking shell
afterward. The profile path and values, endpoint and consent are never committed or displayed.
See `docs/MANUAL_MOTION_COMMISSIONING_RUNBOOK.md` for the software-validation sequence and the
separate hardware authorization boundary.

## B1 identity surface (one-controller first-contact software foundation)

A one-shot, fail-closed identity session for the separately authorized B1 software gate (see
`docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md`). It:

1. runs the Gate A probe first — no connection attempt may precede the native identity/version
   proof;
2. evaluates injected precondition posture (concurrent-authority refusal is a real code path);
3. loads the approved modules (companion first) with the same restricted search set and the same
   loaded-module-path proofs;
4. enforces the direct/no-gcaps posture (`gclib_force_gcaps` with the direct-only byte) BEFORE any
   open;
5. opens exactly ONE controller handle with the in-memory controller address — the validated
   canonical endpoint only, which is the documented `gclib_open()` address argument (gclib 2.6.0
   manual, `gclib_open` entry, printed page 48); no option switch is appended, because
   `--direct`/`--timeout` are documented for the deprecated legacy `GOpen()` parameter (printed
   page 76) and direct mode is already selected process-wide by `gclib_force_gcaps(false)`;
6. performs exactly three typed identity reads in fixed order — connection type (expected
   Ethernet), revision information (bounded, zeroed, printable-shape validated), serial number
   (nonzero required);
7. classifies identity evidence redactedly: the expected model token is compared ordinally; its
   absence is `MODEL_CONFIRMATION_REQUIRED`, never a false accept and never a false reject. The
   serial is observed, redacted and left to Owner manual confirmation;
8. closes the controller handle on EVERY post-open path and releases both module handles
   deterministically; the outcome always tells the truth about the final cleanup state.

The single open attempt's library result is classified into ONE canonical redacted refusal word in
the installed header order — `OPEN_INVALID_ARGUMENT`, `OPEN_INTERNAL_ERROR`, `CONNECTION_TIMEOUT`,
`OPEN_COMMAND_ERROR`, `OPEN_BUFFER_TOO_SMALL`, `OPEN_NOT_CONNECTED`, `OPEN_NOT_SUBSCRIBED` — and
any other non-zero value becomes `OPEN_RESULT_UNRECOGNISED`. The raw integer, the vendor error
string and every native detail stay inside the boundary; the word is the whole diagnostic.

Hard rules: one connection generation (late results rejected), one outstanding native operation
(fail-fast busy), no retry, no reconnect, no polling, no replay, no raw command, no axis read, no
motion, no controller configuration, no discovery. The session owns nothing reusable: it is the
isolated one-shot process boundary itself.

Redaction: outcomes never contain an endpoint, a serial number, a native path, a hash or raw
vendor error text. The endpoint variable (`WJSS_MOTION_CONTROLLER_01_ENDPOINT`) is process-local
environment configuration only and is never committed.

## B2 observed-state surface (verified subset non-empty; software only)

The four read candidates of the Legacy baseline — axis status, first position value, second
position value and one digital output — are real, documented vendor reads, and all four are in the
**verified subset** (`VERIFIED_FOR_B2`). The Owner-authorized B2 surface resolves ONE further
native command-and-response export behind a private, typed boundary:

- an **intent is a type**, one of sixteen closed members; the request text is a LITERAL from an
  immutable table, so no caller text, concatenation, format string or configuration value can
  reach a request;
- the axis domain is the four controller letters and the output domain is the Owner-confirmed
  numbering 1 to 4 — nothing else resolves;
- the request length and the response buffer are fixed, the reply has to be terminated and
  printable, and the accepted value is one bounded decimal token; an empty, truncated,
  non-printable, comma-separated, error-marked, hexadecimal or oversized reply becomes a refusal
  word, never a repaired value;
- no write, stop, jog, brake, home, define, move, auto, program-array or output-mutation mnemonic
  exists in the table, so none can be formed;
- the boundary is internal, has an internal constructor and is never exposed by a public member,
  route, contract, outcome or UI; the read path is reachable only from inside this assembly;
- the boundary holds no module handle and releases nothing: it is invalidated before the lifecycle
  owner closes the controller handle and unloads the module, so it cannot outlive them.

**Honesty rule.** A verified read is not a verified meaning. The two position candidates are
reported `RAW_OBSERVED` (the value is preserved and no engineering meaning is adopted), and the
status word and the output state are reported `UNCONFIRMED` (the value is preserved and no bit,
polarity, wiring or brake meaning is attached). `VERIFIED` stays unreachable: no candidate has a
commissioned meaning for this machine, and no millimetre conversion, limit polarity, Home, fault,
brake or motion-complete statement is produced. An observation never enables a command: the Motion
authorization gate stays CLOSED, endpoint configuration is `NOT_CONFIGURED` and the axes stay
`NOT_COMMISSIONED`.

`docs/MOTION_OBSERVED_STATE_FOUNDATION.md` records the per-candidate B2 evidence, the private
read boundary, the separate Owner-local B1-H and B2-H gates, and the distinct B2-to-Application
C1 contract, which remains **NOT IMPLEMENTED**. That application-facing read contract is separate
from the internal C1 Stop-only foundation of PR #32 and its isolated trial harness.

The vendor contract behind both surfaces — the three-layer separation (current C interface,
deprecated legacy interface, controller command language), the verified library entries with their
printed-page citations, the open-address misuse rule, the result order and the API/command registry
— is recorded in `docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`. That document is evidence only:
it authorizes nothing and it is not executable authority.

## B2-H one-shot observation trial composition (software only)

`MotionObservationTrialSession` is the smallest executable composition over that boundary. It is
one-shot by construction and performs exactly this sequence:

1. Gate A native identity/version proof (injected in tests, real probe runner in production);
2. load only the approved modules through the restricted loader, companion first, and prove each
   loaded path;
3. resolve only the four authorized exports — `gclib_force_gcaps`, `gclib_open`, `gclib_close`
   and the single scoped read export (named only in the internal boundary file);
4. `gclib_force_gcaps(false)` before the open;
5. exactly one `gclib_open` with the validated bare address;
6. one `MotionObservationSession` bound to that handle and that export;
7. the fixed sixteen typed intents, once, in plan order;
8. boundary invalidation, then controller close, then module release, in that order;
9. a redacted outcome whose cleanup flags are the measured truth.

There is no retry, no reconnect, no replay, no polling, no timer, no background reader, no second
open and no second session owner. A second `Run()` is refused with
`OBSERVATION_TRIAL_ALREADY_RUN`. The first refused read stops the remaining sequence and keeps its
own canonical code. Cleanup that cannot be confirmed turns an otherwise complete pass into
`CLEANUP_UNPROVEN` or `NATIVE_UNLOAD_FAILED` — a partial pass is never reported as complete.

The trial type has no string parameter, so no caller text can reach a request. `gclib_open`
refusals reuse the single canonical B1 classification (`MotionControllerApi.ClassifyOpenRefusal`),
so the two surfaces cannot drift.

`docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md` is the runbook of record for the read-only executable
and its Owner-local software gate; `apps/motion-observation` is that console harness. The separate
C1 Stop harness and its required future authorization boundary are documented in
`docs/MOTION_STOP_TRIAL_RUNBOOK.md`.
