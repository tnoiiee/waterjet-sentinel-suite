# WJSS Motion Library Adapter — Native Library Boundary (Probe Surface + B1 Identity Surface)

**Gates:** Native Load / Version Probe Software Gate (Gate A) and the B1 one-controller identity
software foundation. The adapter carries two separately authorized, strictly separated surfaces:
the probe surface (library inspection/loading ONLY, unchanged) and the B1 identity surface (one
direct connection attempt, three typed identity reads, deterministic close — no raw command of
any kind). **Validation:** SOURCE-AUTHORED IN ARENA; NOT
COMPILED IN ARENA — the Owner-local build and probe run are the validation of record
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
