# Stage 0.2.1A — Offline restore procedure (react-ui)

**Status:** Linux offline rehearsal performed in Arena (see
[`../../../docs/spikes/stage-0.2.1a-results.md`](../../../docs/spikes/stage-0.2.1a-results.md)).
**Windows offline restore: NOT VERIFIED** until the Owner runs it locally.
The npm cache is never committed. No ZIP is created.

## Principles

- `package-lock.json` is the single source of exact versions and `integrity` hashes.
- The offline install points npm at an **unreachable registry** (`http://127.0.0.1:9/`) and
  uses `--offline`, so any cache miss fails loudly instead of silently reaching the network.
- `--ignore-scripts` is used: no package requires an install script for this spike (the only
  lockfile entry with an install script is `fsevents`, a macOS-only optional dependency).
- `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`: no browser binaries are downloaded; Owner-local runs
  use the installed Microsoft Edge.

## 1. Prepare the cache (online machine)

```bash
cd spikes/ui-runtime-react/react-ui
export PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
npm ci --cache ../.cache/npm-offline --ignore-scripts --no-audit --no-fund
# Add the Windows x64 native optional packages so the same cache works on Windows:
npm cache add --cache ../.cache/npm-offline \
  lightningcss-win32-x64-msvc@<version-from-lockfile> \
  @rolldown/binding-win32-x64-msvc@<version-from-lockfile>
```

Read the exact versions from `package-lock.json` (`packages["node_modules/…"].version`).

## 2. Offline rehearsal (Linux, as run in Arena)

```bash
rm -rf node_modules
PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 npm ci --offline \
  --cache ../.cache/npm-offline --registry http://127.0.0.1:9/ \
  --ignore-scripts --no-audit --no-fund
npm run typecheck && npm run build && npm test
```

## 3. Windows offline restore (Owner-local, NOT VERIFIED)

Transfer `spikes/ui-runtime-react/.cache/npm-offline/` through Owner storage (not Git), then in
PowerShell:

```powershell
cd spikes\ui-runtime-react\react-ui
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = '1'
Remove-Item -Recurse -Force node_modules -ErrorAction SilentlyContinue
npm ci --offline --cache ..\.cache\npm-offline --registry http://127.0.0.1:9/ --ignore-scripts --no-audit --no-fund
npm run typecheck; npm run build; npm test
```

Record the Node and npm versions, the command, the exit code, and any `ENOTCACHED` errors.

## Open questions (Owner)

- Where the offline cache is stored and how it is transferred and retained `[OPEN]`.
- Whether the Production offline-restore method will be an npm cache, a tarball mirror, or a
  vendored artefact store `[OPEN]` (ADR-0013 implementation detail).
