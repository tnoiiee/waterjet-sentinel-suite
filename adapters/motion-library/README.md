# WJSS Motion Library Adapter — Native Library Boundary (Probe Surface + B1 Identity Surface + B2 Observed-State Surface)

**Gates:** Native Load / Version Probe Software Gate (Gate A), the B1 one-controller identity
software foundation and the B2 read-only observed-state surface. The adapter carries three
separately authorized, strictly separated surfaces: the probe surface (library inspection/loading
ONLY, unchanged), the B1 identity surface (one direct connection attempt, three typed identity
reads, deterministic close — no raw command of any kind) and the B2 observed-state surface (typed
vocabulary, closed state model and session discipline plus ONE internal, typed, length-bounded
read-only boundary over the four Legacy-evidenced reads — the verified subset is non-empty and is
recorded in `docs/MOTION_OBSERVED_STATE_FOUNDATION.md`). **Validation:** SOURCE-AUTHORED IN
ARENA; NOT COMPILED IN ARENA — the Owner-local build and probe run are the validation of record
(see `docs/NATIVE_LOAD_VERSION_PROBE_RUNBOOK.md`).

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
| `MotionControllerCommandKernel.cs` | B2 native boundary (internal): the one approved command-and-response export constant, its resolution through the already-authorized kernel seam, and the bound kernel that issues one typed intent and validates the reply. Holds no module and releases nothing. |

## Owner-local configuration (environment only; values never committed)

| Variable | Shape |
| --- | --- |
| `WJSS_MOTION_LIB_DIR` | Fully qualified local directory (no UNC, no wildcard, no relative value) |
| `WJSS_MOTION_LIB_GCLIB_SHA256` | 64 hexadecimal characters — expected identity of `gclib.dll` |
| `WJSS_MOTION_LIB_GCLIBO_SHA256` | 64 hexadecimal characters — expected identity of `gclibo.dll` |
| `WJSS_MOTION_LIB_EXPECTED_VERSION` | Expected library version string (exact ordinal match) |

The values for these variables live in the Owner-local evidence for this Gate. They are never
written to disk by the adapter, never logged and never returned in any output.

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
5. opens exactly ONE controller handle with the in-memory open-address text
   `"<endpoint> --direct --timeout 2000"`;
6. performs exactly three typed identity reads in fixed order — connection type (expected
   Ethernet), revision information (bounded, zeroed, printable-shape validated), serial number
   (nonzero required);
7. classifies identity evidence redactedly: the expected model token is compared ordinally; its
   absence is `MODEL_CONFIRMATION_REQUIRED`, never a false accept and never a false reject. The
   serial is observed, redacted and left to Owner manual confirmation;
8. closes the controller handle on EVERY post-open path and releases both module handles
   deterministically; the outcome always tells the truth about the final cleanup state.

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

`docs/MOTION_OBSERVED_STATE_FOUNDATION.md` records the per-candidate evidence, the private
boundary, the scoped source-boundary exception (one exact file, one exact token), the separate
Owner-local B1-H and B2-H gates, and the C1 contract that is documented but **NOT IMPLEMENTED**.
