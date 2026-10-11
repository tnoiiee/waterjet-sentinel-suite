# Stage 0.4C-K2 — TC Node/IP Configuration Foundation (Development Checkpoint)

**Status: DEVELOPMENT CHECKPOINT for Owner review. Branch `arena/699da2a5-waterjet-sentinel-suite`, based on `main` `a7d739bb2476667b073f3cc43fabe035fb3306e6`. A pull request to `main` is opened and NOT merged (do not merge). Merge authority is the Owner only.**

Owner Manual Review has **NOT** passed for this checkpoint. **Stage 0.4C-K2 is NOT claimed complete.** No Owner-local hardware, network or field evidence exists for it.

---

## 1. Purpose

K2 establishes the **configuration foundation** for the six Thermocouple Nodes that the Stage 0.4C-0B baseline defines. It makes the node model, the six Test addresses and the validation rules exist in one canonical source, and it shows them read-only in the Operations Preview. It does **not** contact any device, open any socket, implement any protocol, or read any acquisition data.

## 2. Canonical model

| Item | Value | Source |
|---|---|---|
| Nodes | `TC-NODE-01` … `TC-NODE-06`, one-to-one with `CAB1` … `CAB6` by ordinal | `packages/commissioning/src/tcNodeConfiguration.mjs` |
| Test IPs | `192.0.2.11` … `192.0.2.16`, one per Node | same file — the **only** Product file holding these six literals |
| Topology per Node | Head station `750-362 x 1`, TC input modules `5 x 750-498`, 40 physical TC Channels, end module `750-600 x 1` | same file (`EXPECTED_TOPOLOGY`) |
| Totals | 6 Nodes, 6 Cabinets, 30 TC modules, 240 physical TC Channels, 6 configured Test IPs | derived by `systemTotals`, never typed |
| Port | `PORT_UNRESOLVED` — **no port is assumed; port 502 is not used** | same file |
| Protocol | `PROTOCOL_UNRESOLVED` — **no protocol is assumed** | same file |
| Schema | `wjss.tc-node-configuration/1`, id `TC-NODE-CONFIGURATION-TEST-REV1` | same file |

Validation is atomic: a configuration is accepted only if identity, topology, IP and duplicate rules all pass. Duplicate Node, Cabinet or address identities, leading-zero or malformed IPv4 forms (`AMBIGUOUS_LEADING_ZERO_REFUSED` among them), a wrong Node-to-address pairing, invented ports or protocols, and any reachability or hardware claim are each refused with a named code. Nothing is repaired silently and there is no fallback address.

## 3. Status vocabulary (honest separation)

The Node configuration reports these states as **configuration evidence only**. None of them is a measurement, a connection or a verification.

| Status | Meaning in K2 |
|---|---|
| `CONFIGURED` | The Node entry validates against the canonical model. Configuration only. |
| `TEST_CONFIGURATION` / `TEST` | The configuration mode and address environment are Test. Not Production. |
| `OWNER_APPROVED_TEST_CONFIGURATION` | The basis of the six Test addresses (Owner-approved Test configuration). |
| `REACHABILITY_NOT_TESTED` | No ping, socket or probe has been attempted. Reachability is unknown. |
| `ACQUISITION_NOT_IMPLEMENTED` | No acquisition path exists in this Stage. |
| `HARDWARE_VERIFICATION_PENDING` | No Owner-local hardware verification has been performed. |
| `FIELD_VERIFICATION_PENDING` | No field verification has been performed. |
| `HARDWARE_ACCESS_NOT_AUTHORIZED` | Hardware access is not authorized in this Stage. |
| `PORT_UNRESOLVED` / `PROTOCOL_UNRESOLVED` | Port and protocol are not yet decided. |

Aggregate totals shown in the Preview: **reachability tested 0, acquisition verified 0, field verified 0.**

## 4. Owner decision: scoped S2 boundary exception

The boundary scanner rule **S2** forbids non-loopback IPv4 addresses in the Product tree. The six Test addresses cannot appear anywhere in Product source under that rule. The Owner approved a **narrow** exception, and only that:

- Exact addresses: `192.0.2.11`, `.12`, `.13`, `.14`, `.15`, `.16`.
- Exactly **one** canonical repository-relative source file: `packages/commissioning/src/tcNodeConfiguration.mjs`.
- No wildcard, no prefix, no `192.0.2.0/24`, no `.10` or `.17`, no other non-loopback IPv4, no second Product file holding the six, no Production address.
- The policy lives in `tools/boundary-scan/s2Exception.mjs`. It holds **no dotted address literal**: the six values are derived from the octets `[192, 0, 2]` plus `[11..16]`, and the policy must agree exactly with the canonical source (tested).
- Product tests derive the values from the canonical source and do not duplicate the six literals. Scanner negative fixtures are generated at runtime from fragments and never reach the repository.

Every other S-rule (S1, S3–S9) is unchanged and was re-verified inside the canonical path by the self-tests.

**Supersession:** `docs/STAGE_0.4C-0B_CHECKPOINT.md` §2–§6 earlier decided to keep the Test IPs out of `config/` because of S2. That decision is superseded for K2 by this Owner exception. The Test IPs remain outside `config/` in this Stage; that placement is unchanged.

## 5. Read-only Preview and route

- **Route:** `GET /api/tc-node-configuration` (and `HEAD`). Every other method returns **405** with `Allow: GET, HEAD`. Loopback only, same host as the rest of the Preview.
- **Payload:** stage, mode, configuration id, fingerprint (sha256 with a 12-character short revision), schema, configuration mode, address environment, configuration status, validation, the six Nodes, totals and the authority block.
- **Deliberately absent:** credentials, secrets, tokens, local file paths, environment values, ports, protocols, process-image addresses, offsets, byte or word addresses, sensor values, and any Hardware or Production claim.
- **Section:** `#node-configuration` renders the six TC-NODE columns as a transposed table inside a bounded `.table-wrap` frame (so it cannot widen the page), the aggregate totals, the validation line, a spelled-out status legend and the authority statement `TEST_CONFIGURATION · NO HARDWARE ACCESS / NO DEVICE WRITE`.
- **Controls:** the section contains **no button, input, select, form or textarea**. There is no Connect, Test Connection, Activate, Start Polling or Apply to Device control.
- **Boot isolation:** the projection is fetched in its own guard. If it fails, only this section shows an unavailable state; the Wall Map, the Overview and the Monitor still render.
- **Stage identifier:** `STAGE_ID` in the Preview host and the UI subtitle are now `0.4C-K2`. The earlier K1 header and assertion references were updated or re-labelled as re-verified at K2.

## 6. Tests added

| File | Group | Count | Scope |
|---|---|---|---|
| `packages/commissioning/test/tcNodeConfiguration.test.mjs` | A–F | 39 | model, validation, totals, status words, route-free projection, and a source-level check that the module has no socket, request, child-process, file, device-write or clock API |
| `apps/ops-preview/test/tcNodeConfiguration.server.test.mjs` | H | 9 | GET/HEAD only, 405 with `Allow: GET, HEAD`, deterministic bytes, exact six Nodes, no secrets or paths or port/protocol keys, no Hardware claim, no process-image keys, query strings cannot change the projection |
| `apps/ops-preview/test/tcNodeConfiguration.render.test.mjs` | G | 12 | real page boot, six columns with exact cabinet and IP, topology, UNRESOLVED port and protocol, honest statuses, totals text, no controls, no Connect/Test/Activate/Poll wording, Wall Map and Monitor regression, bounded frame, failed projection isolated |
| `tools/boundary-scan/test/s2Exception.test.mjs` | — | 14 | the six pass in the canonical path only; each fails in every other Product path; adjacent and other non-loopback addresses fail; similar and prefix paths get no exception; path normalisation cannot escape; loopback unchanged; S1, S3–S9 still fire inside the canonical path; the real repository is clean |

Focused tests are deterministic: no network, no clock and no random source. Each run writes only to temporary directories.

Existing suites were updated in one place only: the K1 stage-label assertions in `render.test.mjs` and the navigation-anchor count in `page.test.mjs` (5 → 6, for `#node-configuration`). No inherited assertion was weakened.

## 7. Files

**New:**
- `packages/commissioning/src/tcNodeConfiguration.mjs` — canonical six-node model, validation, totals, view and fingerprint. Zero dependency, browser-safe.
- `tools/boundary-scan/s2Exception.mjs` — the scoped S2 policy (no address literal).
- `packages/commissioning/test/tcNodeConfiguration.test.mjs`
- `apps/ops-preview/test/tcNodeConfiguration.server.test.mjs`
- `apps/ops-preview/test/tcNodeConfiguration.render.test.mjs`
- `tools/boundary-scan/test/s2Exception.test.mjs`
- `docs/STAGE_0.4C-K2_CHECKPOINT.md` (this file)

**Modified:** `tools/boundary-scan/boundary-scan.mjs` (one import and one S2 check, with comment), `packages/commissioning/src/index.mjs` (re-export block), `packages/commissioning/package.json` (`check` also syntax-checks the new module; no dependency change), `apps/ops-preview/server.mjs` (STAGE_ID, payload, route), `apps/ops-preview/public/index.html` (subtitle, nav anchor, section), `apps/ops-preview/public/app.mjs` (render and isolated fetch), `apps/ops-preview/public/styles.css` (header comment only), `apps/ops-preview/test/render.test.mjs`, `apps/ops-preview/test/page.test.mjs`, `apps/ops-preview/test/server.test.mjs`, `apps/ops-preview/test/thermalpair.render.test.mjs`, `apps/ops-preview/test/wallmap.render.test.mjs` (K1 header comments re-labelled, assertions unchanged), `CHANGELOG.md`, `docs/CURRENT_STATE.md`.

**Scope-lock report:** the new source and test files above were created under the approved K2 plan. The Owner-approved S2 exception is the basis for `s2Exception.mjs`. The focused test files fall under the plan's focused-tests item.

**Not changed:** no `package.json` dependency, no lock file (`packages.lock.json`, `package-lock.json`, `Directory.Packages.props`), no `.csproj`, no `.cs` source, no Kiosk source, no `config/` file, no Wall Map, Front/Rear or commissioning workflow logic, no Excel or workbook evidence.

## 8. Verification

Gates run in Arena on the final tree (see the commit body for the same record):

| Gate | Result |
|---|---|
| Kiosk source contract (`apps/kiosk/test/shell.contract.test.mjs`) | **21 / 21** |
| Commissioning `check` | OK |
| Commissioning tests | **144 / 144** (105 inherited + 39 new) |
| Operations Preview `check` | OK |
| Operations Preview tests | **109 / 109** (88 inherited + 9 route + 12 render) |
| Mapping Configuration UI `check` and tests | OK, **82 / 82** |
| Mapping package `check` and tests | OK, **261 pass / 0 fail / 7 skipped** (268 total; the 7 skips are the inherited Owner-local ones) |
| Boundary scan (real repository) | **0 findings (S1–S9 clean)** |
| Boundary scan self-tests (`s2Exception.test.mjs`) | **14 / 14** |
| `git diff --check` | clean |

**NOT RUN IN ARENA:** the .NET build and tests (no .NET SDK in the sandbox) and any real-browser or WebView2 execution (no browser engine). No pixel layout, no DOM-computed overflow, no DPI behaviour and no Kiosk behaviour is claimed. The Preview tests run the real `public/app.mjs` against the real host through a stub DOM; they are not a browser verification.

**Not performed, and not claimed:** no socket, ping, port probe or other contact with `192.0.2.11`–`192.0.2.16`; no Modbus or other protocol; no port 502 assumption; no WAGO or acquisition read; no credential; no device write; no Production mode; no fallback address.

## 9. Owner-local validation handoff (PowerShell, repository root)

Run on the Owner's Windows machine. Record each result. Stop and report to the agent on any failure.

1. Confirm the checkout branch and commit: `git branch --show-current` (expect `arena/699da2a5-waterjet-sentinel-suite`) and `git log -1 --oneline`.
2. Confirm a clean tree: `git status --short` (expect no output).
3. Confirm the baseline ancestry: `git merge-base --is-ancestor a7d739bb2476667b073f3cc43fabe035fb3306e6 HEAD` (expect exit 0).
4. Confirm Node is 22.22.2 or later: `node --version`.
5. Commissioning check: `npm --prefix .\packages\commissioning run check` (expect exit 0).
6. Commissioning tests: `npm --prefix .\packages\commissioning test` (expect 144 pass, 0 fail).
7. Operations Preview check: `npm --prefix .\apps\ops-preview run check` (expect exit 0).
8. Operations Preview tests: `npm --prefix .\apps\ops-preview test` (expect 109 pass, 0 fail).
9. Kiosk source contract: `node --test .\apps\kiosk\test\shell.contract.test.mjs` (expect 21 pass, 0 fail).
10. Mapping Configuration UI: `npm --prefix .\apps\mapping-config run check` and `npm --prefix .\apps\mapping-config test` (expect 82 pass, 0 fail).
11. Mapping package: `npm --prefix .\packages\mapping-config run check` and `npm --prefix .\packages\mapping-config test` (expect 261 pass, 0 fail, 7 skipped).
12. Boundary scan: `node .\tools\boundary-scan\boundary-scan.mjs .` (expect `0 findings (S1-S9 clean)`).
13. Scanner self-tests: `node --test .\tools\boundary-scan\test\s2Exception.test.mjs` (expect 14 pass, 0 fail).
14. Whitespace and dependency check: `git diff --check` (expect no output) and `git diff a7d739bb2476667b073f3cc43fabe035fb3306e6 -- "*lock*" "*.csproj" "*Directory.Packages.props"` (expect no output).
15. .NET build and tests (NOT RUN in Arena): `dotnet build .\WaterJetSentinelSuite.sln -c Release` then `dotnet test .\WaterJetSentinelSuite.sln -c Release` (expect zero errors and no new warnings; the Kiosk build must stay warning-free as in K1).
16. Start the Preview host: `npm --prefix .\apps\ops-preview start` (expect `listening on 127.0.0.1:5187`). Keep it running for steps 17–20.
17. Route read-back: `Invoke-RestMethod http://127.0.0.1:5187/api/tc-node-configuration` and confirm `nodes` = 6, totals 6 / 6 / 30 / 240 / 6, and reachability, acquisition and field verified = 0.
18. Method refusal: `Invoke-WebRequest -Method Post -Uri http://127.0.0.1:5187/api/tc-node-configuration -SkipHttpErrorCheck` and confirm status 405 with `Allow: GET, HEAD`.
19. No contact with the Test addresses: while the host and Kiosk run, `netstat -n | Select-String "192.0.2."` must return nothing. No ping or probe may be sent.
20. Browser and Kiosk review: open `http://127.0.0.1:5187/` and confirm the Node Configuration section shows six TC-NODE columns, the totals line, the status legend, no buttons in that section, and the Wall Map, Overview and Monitor unchanged. Then launch the Kiosk and confirm the header, the confirmed Exit, and the reconnecting fail-safe behave as in K1. Report the results to the Owner. Owner Manual Review remains an Owner decision.

## 10. Stop conditions and Owner items

- Any need for a second canonical path, a broader address range, another exception, a dependency change, or any network or hardware action must return to the Owner.
- K2 does not claim Owner Manual Review, does not claim Stage completion, and does not authorize merging. Merge is the Owner's decision.
- Next Stage work (reachability, acquisition, port and protocol decisions) is out of scope and requires a separate Owner decision.
