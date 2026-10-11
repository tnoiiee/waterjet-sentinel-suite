# Stage 0.4C-K3 — Physical TC Connectivity and Protocol Evidence Gate (Development Checkpoint)

**Status: DEVELOPMENT CHECKPOINT for Owner review. Branch `arena/200cd6d0-waterjet-sentinel-suite`, based on `main` `cc6ccf8779d87a5a44d1c03a003021791c26aac8` (the merge of PR #20; parents `a7d739bb2476667b073f3cc43fabe035fb3306e6` and `b5ca7a77d2a6927e0697ced75ebeae32f571aa74`). A pull request to `main` is opened and NOT merged (do not merge). Merge authority is the Owner only.**

Owner Manual Review has **NOT** passed for this checkpoint. **Stage 0.4C-K3 is NOT claimed complete.** No protocol is selected, no port is resolved, no socket was opened, no address was probed, no device was contacted, no process image was read and no Hardware or field evidence exists for it.

---

## 1. Purpose

K3 is the **evidence gate** between the configured six-Node model of K2 and any future read-only physical acquisition. It classifies every claim about the 750-362 head stations and the 750-498 modules by its evidence class, records the protocol and port decision position honestly, models the process-image evidence per Node, derives a deterministic **candidate** process image where the evidence permits it, and defines the read-only acquisition contract, the communication-state model and the freshness semantics as **contract and test models only**. It changes nothing in the K2 configuration and performs **no network action**.

## 2. Baseline verification (done before any change)

| Check | Result |
|---|---|
| `origin/main` | `cc6ccf8779d87a5a44d1c03a003021791c26aac8` = approved baseline (PR #20 merge; PR #20 state `MERGED`) |
| Branch | `arena/200cd6d0-waterjet-sentinel-suite`, HEAD = baseline, descends from it, tree and index clean |
| Pre-change gates (Node v22.22.3) | Commissioning 144/144 · Ops Preview 110/110 · Kiosk contract 22/22 · Mapping UI 82/82 · mapping package 268 total / 261 pass / 7 skipped · boundary scan `0 findings (S1-S9 clean)` · S2 self-tests 14/14 — all equal to the approved totals |
| .NET | NOT RUN in Arena (no SDK) |

## 3. Exact-source sweep and evidence intake

### 3.1 Repository evidence read

`AGENTS.md`; `docs/STAGE_0.4C-K2_CHECKPOINT.md`; `docs/TC_EVIDENCE_SOURCE_REGISTER.md`; `docs/evidence/tc-process-image-profile-rev1.md`; `docs/evidence/tc-schedule-validation-rev1.md`; `docs/evidence/tc-drawing-schedule-rev1.csv`; `packages/commissioning/src/{tcNodeConfiguration,schedule,quality,scenarios,observation,thermalpair,presentation,wallmap,workflow,index}.mjs`; `packages/mapping-config/src/processImageEvidence.mjs` (design reference only); `apps/ops-preview/{server.mjs,public/index.html,public/app.mjs}`; the K2 test groups and `tools/boundary-scan/boundary-scan.mjs`.

### 3.2 Findings that shape this Stage

1. **Committed evidence records are truncated.** `docs/evidence/tc-process-image-profile-rev1.md` ends at line 115 with a literal `...[truncated 3101 chars]` marker and `docs/evidence/tc-schedule-validation-rev1.md` ends at line 42 with a similar marker (both introduced by the PR #18 merge `48f0315…`). The lost profile sections after §3 held the head-station field-network session facts — including anything that would have named a port. **No committed document names a port.** Nothing lost was reconstructed from memory.
2. **Owner-connected source (Google Drive) holds exactly one evidence file:** `WJSS_TC_EVIDENCE_BASELINE_REV1.zip`, 17 345 849 bytes (equal to the size recorded in the 0B register), Drive-reported MD5 `2807f158ced9a984dea288e5ab30e2d8`. The connector cannot extract text from an archive, no individual manual or drawing PDF exists on the Drive, and transferring 17 MB inline was not feasible, so the archive was **located but not extracted** and its SHA-256 was **not re-verified** in K3. The manuals and the drawing are therefore `NOT_AVAILABLE_IN_SESSION` for content; every manual fact used here is a fact **as recorded in the committed REV1 profile**, and is labelled that way.
3. **Boundary rule S3** forbids vendor and transport vocabulary in the Product tree. The field-network protocol named by the drawing device list and the head-station manual is therefore referred to in Product source by the opaque reference `TC-K3-PROTOCOL-CANDIDATE-01` only. Its name appears in documentation (`docs/evidence/tc-process-image-profile-rev1.md` §1 and §3). This is the same convention `schedule.mjs` already uses for the word column.

### 3.3 Classification of claims (the canonical set is `CANONICAL_TC_ACQUISITION_EVIDENCE`, 24 claims)

| Claim | Class | Scope | Position |
|---|---|---|---|
| CL-01 topology (1 × 750-362, 5 × 750-498, 1 × 750-600, 8 ch/module, 6 Nodes) | OWNER_CONFIRMED | OWNER_DECISION | as-installed not verified |
| CL-02 drawing tags -A101 / -A102.1…5 / -A110, +SDSCAB1…6 | DRAWING_EVIDENCE | DRAWING_LEVEL | REV 1A pending revision; ordinal correspondence assumed |
| CL-03 all 30 modules factory default | OWNER_CONFIRMED | OWNER_DECISION | no instance export; nothing per Channel invented |
| CL-04 protocol named | CANDIDATE_UNVERIFIED | MODEL_LEVEL | named by DRAWING + MANUFACTURER evidence; Owner approval pending |
| CL-05 head-station session semantics | OWNER_INPUT_PENDING | MODEL_LEVEL | lost to truncation |
| CL-06 port | OWNER_INPUT_PENDING | MODEL_LEVEL | no evidence names it; no default assumed |
| CL-07 8 Channels per module | MANUFACTURER_EVIDENCE | MODEL_LEVEL | via profile |
| CL-08 16-bit data + 8-bit status per Channel (status on local bus) | MANUFACTURER_EVIDENCE | MODEL_LEVEL | via profile §2 |
| CL-09 status byte not on the field network | CANDIDATE_UNVERIFIED | MODEL_LEVEL | head-station class rule; 750-498 not enumerated in the head-station manual table |
| CL-10 byte order low-byte-first | MANUFACTURER_EVIDENCE | MODEL_LEVEL | instance unverified → status stays UNRESOLVED with candidate |
| CL-11 word order not applicable (single word) | CANDIDATE_UNVERIFIED | MODEL_LEVEL | until instance width verified |
| CL-12 encoding two's complement, 0.1 degC/digit, Type K | MANUFACTURER_EVIDENCE | MODEL_LEVEL | applies only while CL-03 holds |
| CL-13 under/over-range and CJC representations | MANUFACTURER_EVIDENCE | MODEL_LEVEL | status visibility unresolved |
| CL-14 wire break 0x7FFF/0x60 vs deactivated Channel 0x7FFF | **CONFLICTING_EVIDENCE** | MODEL_LEVEL | data word alone cannot prove wire break → decoding BLOCKED |
| CL-15 physical module order as drawn | DRAWING_EVIDENCE | DRAWING_LEVEL | as-installed unverified |
| CL-16 analog-first, physical order, word-aligned from word 0 | CANDIDATE_UNVERIFIED | MODEL_LEVEL | class rule; 750-498 not enumerated |
| CL-17 head station and end module carry no process data | MANUFACTURER_EVIDENCE | MODEL_LEVEL | — |
| CL-18 firmware / hardware revision | HARDWARE_VERIFICATION_PENDING | INSTANCE_LEVEL | unknown |
| CL-19 conversion ≤ 100 ms/Channel | MANUFACTURER_EVIDENCE | MODEL_LEVEL | informs, does not set, freshness |
| CL-20 freshness thresholds | OWNER_INPUT_PENDING | OWNER_DECISION | no number anywhere in Product source |
| CL-21 reachability | HARDWARE_VERIFICATION_PENDING | INSTANCE_LEVEL | 0 probes performed |
| CL-22 schedule word-ordinal column | REPOSITORY_CONTRACT | MODEL_LEVEL | record of the 0B derivation |
| CL-23 SIMULATED_WIRE_BREAK scenario | SYNTHETIC_TEST_ONLY | SYNTHETIC | never evidence |
| CL-24 truncated committed records | OWNER_INPUT_PENDING | OWNER_DECISION | restoration needs the Owner-held manuals |

Nothing in this Stage is `HARDWARE_VERIFIED` or `FIELD_VERIFIED`; the validator refuses either without a `HARDWARE_OBSERVATION` source, and none exists.

## 4. Decisions recorded (not made)

| Decision | Status | Candidate | Approval |
|---|---|---|---|
| Protocol | `PROTOCOL_UNRESOLVED` | `TC-K3-PROTOCOL-CANDIDATE-01` (`PROTOCOL_CANDIDATE`), named by drawing + manufacturer evidence, applicability `MODEL_LEVEL_ONLY`, session semantics `NOT_AVAILABLE_IN_SESSION`, write operation required `false` | `OWNER_INPUT_PENDING`; `approvedForTestAdapter: false`; `activated: false` |
| Port | `PORT_UNRESOLVED` | none (`NO_EVIDENCE_NAMES_THE_PORT`) | `OWNER_INPUT_PENDING` |

The validator refuses `APPROVED_FOR_TEST_ADAPTER`, `PROTOCOL_ACTIVATED`, `HARDWARE_CONNECTED`, `ACQUISITION_VERIFIED`, `PORT_APPROVED`, any port candidate that is zero, blank, out of range, unsourced or carrying a `DEFAULT` / `ASSUMED` / `WELL_KNOWN` / `GUESSED` basis (`PORT_DEFAULT_ASSUMED`), and any write operation. No well-known port number appears anywhere in the Product tree or in any route payload (tested).

## 5. Process-image evidence and candidate derivation

- Per Node the rack is fixed: slot 0 head station (no process width), slots 1–5 `750-498` (`processModulePosition` 1–5, drawing tags -A102.1…5), slot 6 end module (no process width). Rack entries carry **no offset field**; `MANUAL_NUMERIC_OFFSET_REFUSED` fires if one is added anywhere.
- `deriveCandidateProcessImage` reads only the claims named in `processImageBasis`. With the canonical set it yields `CANDIDATE_DERIVED`: 8 words per module, module *n* at words 8(n−1)…8n−1, **40 input words (640 bits) per Node**, no overlap, `candidateStatus: CANDIDATE_UNVERIFIED`, `addressStatus: ADDRESS_UNRESOLVED`, `authoritative: false`, `activationPossible: false`, with four named verification blockers (REQ-K3-02/03/04/05).
- Any incomplete, conflicting or unresolved basis claim gives `DERIVATION_BLOCKED` with named blockers and **no number** (tested for missing width, non-16-bit width, conflicting width claims, conflicting evidence class, unresolved ordering rule, missing or reversed module order, non-zero head-station width).
- `compareCandidateWithSchedule`: the derived candidate equals the committed schedule word ordinal for **240 of 240 rows, 0 mismatches**. This is record-to-record consistency between two repository artefacts and is labelled as such in the payload; it is **not** Hardware verification.
- `ProcessImageOrder` is derived from CL-15 (drawing order) **plus** CL-16 (ordering rule) and is labelled with both; `processModulePosition` alone is never treated as image order.

## 6. Read-only acquisition contract, communication state and freshness

- **Sample contract** (`ACQUISITION_SAMPLE_FIELDS`, `validateAcquisitionSample`): Node and Cabinet identity (exact approved pair, no fallback), read-cycle id, module instance 1–5, Channel 1–8, direction `INPUT`, **separate** `receivedTime` / `deviceSampleTime` / `decodeTime` / `publicationTime` / `lastValidValueTime`, raw payload as one hex word, decode status, validity, engineering value and unit, diagnostic category, communication state, freshness state, Last Valid Value, provenance. Keys matching write / command / output / setpoint / config / firmware / restart / activate / poll / connect are refused by name. An invalid or stale sample carries **no** numeric current value; a valid sample requires `DECODED`, no diagnostic, `FRESH`/`AGING`, and `ONLINE_READ_ONLY`. Provenance is mandatory and `PHYSICAL_ACQUISITION` is refused in this Stage (`PHYSICAL_PROVENANCE_NOT_AVAILABLE_IN_STAGE`).
- **Communication state model** (`ACQUISITION_COMMUNICATION_STATE`, nine states as briefed) is a contract/test model. The only state the K3 surface derives is `CONFIGURED_NOT_TESTED` (or `NOT_CONFIGURED` if the configuration failed validation). No Node is ever shown `ONLINE_READ_ONLY`, `COMMUNICATION_LOSS`, `CONNECTING` or `RECONNECTING` (tested in the package, the route and the page).
- **Freshness** (`classifyFreshness`): thresholds are inputs; the approved thresholds are all `OWNER_INPUT_PENDING`, so classification is `FRESHNESS_UNDETERMINED` and never `FRESH` until the Owner supplies numbers. The function takes an explicit evaluation time; it reads no clock.
- **FrontRearDelta** (`frontRearDeltaPermitted`): permitted only from two current valid, non-stale sides; the blocking side is named. The K1 `thermalpair.mjs` rule is unchanged.
- **Wire break**: `WIRE_BREAK_DECODING_BLOCKED` with 8 required evidence items and 0 provided. A set that claims a decoding rule without a `HARDWARE_OBSERVATION` source and complete items is refused (`WIRE_BREAK_EVIDENCE_INCOMPLETE`). There is deliberately **no** confirmed physical `WIRE_BREAK` diagnostic category; `SYNTHETIC_WIRE_BREAK` is accepted only with synthetic provenance.

## 7. Read-only Preview and route

- `GET /api/tc-acquisition-evidence` (GET and HEAD only; every other method 405 with `Allow: GET, HEAD`; `Cache-Control: no-store`; CSP unchanged). The payload carries `stageId: '0.4C-K2'` (the Preview identity is unchanged by Owner rule) and `evidenceStage: '0.4C-K3'`, the validation result, the decisions, the per-field statuses with their model-level candidates, the candidate process image, the schedule-consistency result, the sources (title, revision, availability, hash recorded yes/no with its basis), the classified claims, the eleven missing-evidence requests, the totals and an all-false authority block. It names no file path, URL, port number, protocol name or credential, and ignores any query string.
- `#tc-acquisition-evidence` section after `#node-configuration` (seventh navigation anchor): decision block, a six-column Node table (Cabinet, Test IP, Port, Protocol, Reachability, Acquisition, Communication state, Process image, Byte order, Word order, Wire-break decoding, Firmware, Hardware verification, Field verification, Hardware access — every status written as text with its code), the evidence-source table and claim-class summary, the candidate process-image table with its blockers, the missing-evidence list and the authority line. No button, input, select, form, link or control vocabulary (Connect, Test Connection, Ping, Activate, Start Polling, Apply to Device, Poll, Probe, Read Now, Start, Apply, Save, Hardware Mode, Production Mode). A failed route degrades only this section.
- The K2 route `/api/tc-node-configuration`, `/api/health` and the page header still identify `0.4C-K2`; `STAGE_ID` is unchanged.

## 8. Tests added (52) and gate results

| Suite | Before | After | New |
|---|---|---|---|
| Commissioning (`packages/commissioning/test/tcAcquisitionEvidence.test.mjs`, groups A/B/C/D/E/F/view/H) | 144 | **177** | 33 |
| Ops Preview (`tcAcquisitionEvidence.server.test.mjs` 8 + `tcAcquisitionEvidence.render.test.mjs` 11; `page.test.mjs` anchor count 6 → 7) | 110 | **129** | 19 |
| Kiosk contract | 22 | 22 | 0 |
| Mapping UI | 82 | 82 | 0 |
| Mapping package | 268 (261 pass / 7 skipped) | 268 (261 / 7) | 0 |
| Boundary scan | 0 findings | **0 findings (S1-S9 clean)** | — |
| S2 self-tests | 14 | 14 | 0 |
| `git diff --check` | clean | clean | — |
| Commissioning `check` (now includes the new module) | OK | OK | — |

Group H (static source scan) proves the new module imports only `./schedule.mjs` and `./tcNodeConfiguration.mjs`, uses no `node:` built-in, no socket/net/dgram/dns/fetch/WebSocket/`Date.now`/timers/`process.env`, spells no port number, no transport name and no address literal, and that no `index.mjs` export name matches the K2 transport-vocabulary policy. The boundary scan caught two of my own test defects before commit (six address literals and a credential word in test files); both were corrected by deriving the values at runtime — the S2 exception was **not** widened.

## 9. Files

**New:** `packages/commissioning/src/tcAcquisitionEvidence.mjs`, `packages/commissioning/test/tcAcquisitionEvidence.test.mjs`, `apps/ops-preview/test/tcAcquisitionEvidence.server.test.mjs`, `apps/ops-preview/test/tcAcquisitionEvidence.render.test.mjs`, `docs/STAGE_0.4C-K3_CHECKPOINT.md`.
**Modified:** `packages/commissioning/src/index.mjs` (re-export block), `packages/commissioning/package.json` (`check` script only — no version, dependency or lock change), `apps/ops-preview/server.mjs` (route + payload builder), `apps/ops-preview/public/index.html` (section + anchor), `apps/ops-preview/public/app.mjs` (renderer, isolated fetch), `apps/ops-preview/test/page.test.mjs` (anchor count), `docs/CURRENT_STATE.md`, `docs/TC_EVIDENCE_SOURCE_REGISTER.md` (§14), `CHANGELOG.md`.
**Deliberately untouched:** `packages/commissioning/src/tcNodeConfiguration.mjs`, `tools/boundary-scan/**`, the Kiosk shell, every lock file, every `.csproj`, the truncated evidence records (not reconstructed).

## 10. Changed / Unchanged / Not Verified

**Changed:** one new canonical evidence module and its re-exports; one new GET/HEAD route; one new read-only Preview section and navigation anchor; 52 tests; this documentation pass.

**Unchanged:** the six-Node identities and statuses (`TEST_CONFIGURATION`, `OWNER_APPROVED_TEST_CONFIGURATION`, `REACHABILITY_NOT_TESTED`, `ACQUISITION_NOT_IMPLEMENTED`, `HARDWARE_ACCESS_NOT_AUTHORIZED`, `FIELD_VERIFICATION_PENDING`, `PORT_UNRESOLVED`, `PROTOCOL_UNRESOLVED`); the topology and the 240 / 106 / 212 / 108 totals; the scoped S2 exception (one path, six literals); S1–S9; the Kiosk shell, WebView2 `1.0.4258.31`, the K2 identity across Kiosk, Preview and Diagnostics; the U-shaped Wall Map, Front/Rear and FrontRearDelta semantics, the static vertical Wall Map contract; the commissioning workflow; the reconnecting fail-safe and the confirmed exit; loopback-only hosting and the CSP; every dependency and lock file; the read-only authority (no Hardware access, no device write, no network action).

**Not verified:** anything about the physical head stations and modules (firmware, configuration, module order as installed, process-image mapping, byte order in practice, status visibility, wire-break behaviour); the port and the session semantics of the candidate protocol; the SHA-256 of the Drive archive (not re-computed); the .NET build and tests; real-browser rendering; Owner Manual Review.

## 11. Missing evidence requested from the Owner (`REQ-K3-01` … `REQ-K3-11`, all `OWNER_INPUT_PENDING`)

1. **REQ-K3-01** — head-station (750-362) manual section on the field-network session: port, connection limits, timeouts, read operation. *Blocks:* PORT, protocol applicability and session semantics, reconnect semantics.
2. **REQ-K3-02** — manufacturer statement whether the 750-498 status byte is visible on the field network through the 750-362 (the head-station module table does not enumerate the 750-498). *Blocks:* status location, wire-break decoding, invalid-value decoding.
3. **REQ-K3-03** — actual controller / engineering-tool configuration export for each of the six cabinets. *Blocks:* per-instance factory-default confirmation, instance process-image mapping, wire-break decoding.
4. **REQ-K3-04** — firmware and hardware revision of each head station and module. *Blocks:* manual applicability.
5. **REQ-K3-05** — actual process-image mapping from the engineering tool for one cabinet, with confirmation that all six are identical. *Blocks:* candidate offset verification, ProcessImageOrder.
6. **REQ-K3-06** — raw input words under controlled known-temperature conditions, with the reading method recorded. *Blocks:* byte order and encoding verification.
7. **REQ-K3-07** — controlled wire-break / open-circuit observation per Channel, against a deactivated Channel and against a communication loss. *Blocks:* physical wire-break rule.
8. **REQ-K3-08** — as-built cabinet and network drawing with the cabinet-to-Node assignment confirmed. *Blocks:* ASSUMED_ORDINAL_CORRESPONDENCE, as-installed module order.
9. **REQ-K3-09** — Owner decision on freshness thresholds (aging, stale, Last-Valid retention). *Blocks:* freshness semantics.
10. **REQ-K3-10** — restoration of the truncated committed records (profile after §3; validation summary after its marker) from the Owner-held manuals. *Blocks:* repository evidence for PORT and session semantics.
11. **REQ-K3-11** — Owner approval decision on `TC-K3-PROTOCOL-CANDIDATE-01`. *Blocks:* `APPROVED_FOR_TEST_ADAPTER`.

**Practical intake request:** please provide the three manuals and the drawing as **individual PDF files** (not inside the zip) on the connected Drive, so the agent can extract their text in-session; or paste the head-station field-network section text into the next brief.

## 12. Owner-local validation handoff (PowerShell, repository root)

Run on the Owner's Windows machine. Record each result. Stop and report to the agent on any failure. **No step below sends anything to a Test address.**

1. Confirm the checkout branch and commit: `git branch --show-current` (expect `arena/200cd6d0-waterjet-sentinel-suite`) and `git log -1 --oneline` (expect the K3 checkpoint SHA from the PR).
2. Confirm a clean tree: `git status --short` (expect no output).
3. Confirm the baseline ancestry: `git merge-base --is-ancestor cc6ccf8779d87a5a44d1c03a003021791c26aac8 HEAD` (expect exit 0).
4. Confirm Node is 22.22.2 or later: `node --version`.
5. Commissioning check: `npm --prefix .\packages\commissioning run check` (expect exit 0; it now checks `tcAcquisitionEvidence.mjs`).
6. Commissioning tests: `npm --prefix .\packages\commissioning test` (expect 177 pass, 0 fail).
7. Operations Preview check: `npm --prefix .\apps\ops-preview run check` (expect exit 0).
8. Operations Preview tests: `npm --prefix .\apps\ops-preview test` (expect 129 pass, 0 fail).
9. Kiosk source contract: `node --test .\apps\kiosk\test\shell.contract.test.mjs` (expect 22 pass, 0 fail).
10. Mapping Configuration UI: `npm --prefix .\apps\mapping-config run check` and `npm --prefix .\apps\mapping-config test` (expect 82 pass, 0 fail).
11. Mapping package: `npm --prefix .\packages\mapping-config run check` and `npm --prefix .\packages\mapping-config test` (expect 261 pass, 0 fail, 7 skipped).
12. Boundary scan: `node .\tools\boundary-scan\boundary-scan.mjs .` (expect `0 findings (S1-S9 clean)`).
13. Scanner self-tests: `node --test .\tools\boundary-scan\test\s2Exception.test.mjs` (expect 14 pass, 0 fail).
14. Whitespace and dependency check: `git diff --check` (expect no output) and `git diff cc6ccf8779d87a5a44d1c03a003021791c26aac8 -- "*lock*" "*.csproj" "*Directory.Packages.props"` (expect no output).
15. Vocabulary check of the new module: `Select-String -Path .\packages\commissioning\src\tcAcquisitionEvidence.mjs -Pattern '\b502\b','socket','fetch\(' -CaseSensitive:$false` (expect no matches).
16. .NET build and tests (NOT RUN in Arena): `dotnet build .\WaterJetSentinelSuite.sln -c Release` then `dotnet test .\WaterJetSentinelSuite.sln -c Release` (expect zero errors, no new warnings).
17. Start the Preview host: `npm --prefix .\apps\ops-preview start` (expect `listening on 127.0.0.1:5187`). Keep it running for steps 18–21.
18. Route read-back: `$e = Invoke-RestMethod http://127.0.0.1:5187/api/tc-acquisition-evidence; $e.stageId; $e.evidenceStage; $e.decisions.protocol.status; $e.decisions.port.status; $e.processImageCandidate.perNodeInputWords; $e.scheduleConsistency.mismatches; ($e.nodes | % communicationState | Sort-Object -Unique)` (expect `0.4C-K2`, `0.4C-K3`, `PROTOCOL_UNRESOLVED`, `PORT_UNRESOLVED`, `40`, `0`, `CONFIGURED_NOT_TESTED`).
19. Method refusal: `Invoke-WebRequest -Method Post -Uri http://127.0.0.1:5187/api/tc-acquisition-evidence -SkipHttpErrorCheck` (expect status 405 with `Allow: GET, HEAD`).
20. No contact with the Test addresses: while the host and the Kiosk run, `netstat -n | Select-String "192.0.2."` must return nothing, and no ping, probe or name resolution may be issued by hand.
21. Browser and Kiosk review: open `http://127.0.0.1:5187/`, go to **TC Acquisition Evidence**, and confirm: six TC-NODE columns with every status written as text, `PROTOCOL_UNRESOLVED` with the unapproved candidate reference, `PORT_UNRESOLVED` with no number, the nine evidence sources with their availability, the candidate process-image table (40 words per Node, `CANDIDATE_UNVERIFIED`, address unresolved), the eleven requests, the authority line, and **no button or control** in the section; the Node Configuration, Wall Map, Overview and Monitor unchanged; then launch the Kiosk and confirm the header, title, embedded Preview and Diagnostics still read `Stage 0.4C-K2` and the confirmed Exit and reconnecting fail-safe behave as in K2.
22. Evidence intake for the next Stage (Owner action, no agent network access involved): compute `Get-FileHash .\WJSS_TC_EVIDENCE_BASELINE_REV1.zip -Algorithm SHA256` locally and confirm `7B886950D7F5F665416241E4CB502CD1A5787E3581788C799077C062786AA23E`; then place the three manuals and the drawing as individual PDFs on the connected Drive (REQ-K3-01/02/10) and decide REQ-K3-09 and REQ-K3-11. Report the results to the agent. Owner Manual Review remains an Owner decision.

## 13. Stop conditions respected

The agent did **not**: select a protocol or a port; add any protocol, network-client or MODBUS dependency; widen S2 or add a second address source; open a socket, resolve a name or probe anything; implement hardware acquisition; assign a numeric offset outside the derived candidate; define a physical wire-break decoding rule; create a TEST_HARDWARE or Production authority; reset, clean, restore, checkout, switch, stash, rebase or force-push; claim Owner Manual Review or Stage completion.
