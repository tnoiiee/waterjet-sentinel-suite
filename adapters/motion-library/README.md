# WJSS Motion Library Adapter — Native Load / Version Probe Boundary

**Gate:** Native Load / Version Probe Software Gate. **Validation:** SOURCE-AUTHORED IN ARENA; NOT
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

Only `gclib_version` is resolvable through this boundary. No open, close, discovery, address
enumeration, command, callback, subscription, revision, serial or status API can be reached:
the kernel interface exposes only load / resolve-one-export / module-path / invoke-version /
release.

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
