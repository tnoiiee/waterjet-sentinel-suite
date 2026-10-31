# Process-I/O Hardware Bench Commissioning Protocol (Stage H2)

Status: **[COMMISSIONING RUNBOOK & BENCH PROTOCOL — HARDWARE NOT EXECUTED / NOT VERIFIED].**
Status addition (2026-10-31): **H2 MODBUS SIMULATOR FUNCTION/ADDRESS BENCH: ACCEPTED PASS** — Owner decision; see §8. This addition does **not** upgrade any real-hardware status.
This document defines the execution protocol, bench modes, consent governance, safety invariants,
and transcript redaction requirements for the Process-I/O hardware bench harness (`tools/process-io-bench/bench.mjs`
and `ProcessIoBenchRunner`).

---

## 1. Safety Boundary & Non-Execution Invariant

1. **Physical Hardware Status:** `PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED`.
2. **Sandbox Invariant:** Network socket communication to physical fieldbus endpoints (e.g. `<OWNER_LOCAL_WAGO_T8_ENDPOINT>`)
   is strictly prohibited during development and automated sandbox execution.
3. **Execution Authority:** Physical trials are authorized exclusively on Owner-controlled hardware
   under explicit, interactive consent phrases.
4. **Endpoint Governance:** Physical IP addresses (such as `<OWNER_LOCAL_WAGO_T8_ENDPOINT>`) are strictly Owner-local and never committed
   to repository source code. Evidence transcripts redact physical IP octets.

---

## 2. Authoritative Physical Process-I/O Architecture & Legacy Rev2 Reconcile

- **Canonical Device Key:** `PROCESS_IO_WAGO_T8` (single physical coupler rack; legacy 3-device model removed).
- **Physical Transport Default:** Industrial TCP, port `502`, configured Unit ID `1` (from Legacy Production Rev2 evidence; physical hardware verification pending Owner-local execution).
- **Unit ID Validation:** Physical range `1..247` strictly enforced (rejects `0`, `248`, `255`). Automatic Unit-ID fallback is prohibited.
- **Function Code & Memory Map Contract:**
  - **Discrete Inputs (DI):** 40 rows (`DI-001`..`DI-040`), Function Code **FC02**, register/bit addresses `0..39`.
  - **Digital Outputs (DO):** 32 rows (`DO-001`..`DO-031` + 1 spare):
    - Process image read-back: Function Code **FC01**, addresses `512..543` (offset 512).
    - Single channel command write: Function Code **FC05**, addresses `0..31` (offset 0).
    - Multi-channel command write: Function Code **FC15**, addresses `0..31` (offset 0).
  - **Analog Inputs (AI):** 36 rows (`AI-001+`..`AI-036`), Function Code **FC04**, input register addresses `0..35`.
  - **Analog Outputs (AO):** 2 rows (`AO-001+` + 1 spare):
    - Process image read-back: Function Code **FC03**, holding register addresses `512..513` (offset 512).
    - Single channel command write: Function Code **FC06**, register addresses `0..1` (offset 0).
    - Multi-channel command write: Function Code **FC16**, register addresses `0..1` (offset 0).

---

## 3. Read Profiles (Explicit Selection Required)

The harness supports two explicit read profiles with zero automatic fallback:

| Read Profile | Operations Executed | Mapping Interpretation |
|---|---|---|
| `WORKBOOK_CANONICAL` (Default) | FC02 DI (0..39), FC04 AI (0..35), FC01 DO read-back (512..543), FC03 AO read-back (512..513) | Authoritative Workbook signal taxonomy |
| `LEGACY_PRODUCTION_COMPAT` | FC01 bulk read (start 0, qty 100), FC03 bulk read (start 0, qty 100) | Legacy bulk memory block read (preserved verbatim; never reinterpreted as canonical DI/AI) |

---

## 4. Bench Modes & Explicit Consent Governance

The bench runner supports five distinct operational modes. Generic or broad execution modes (such as deprecated `PHYSICAL_TEST`) are strictly refused.

| Mode | Allowed Operations | Required Consent Phrase |
|---|---|---|
| `SIMULATION` | Offline mock execution across all 111 catalogue rows | None required |
| `PHYSICAL_DI_AI_READ` | Read-only observation of all 40 DI channels and 36 AI channels (or Legacy 100-block) | `I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL` |
| `PHYSICAL_DO_SINGLE` | Single specified DO channel write with required `--hold-ms` (FC05 + FC01 read-back + hold + safe return) | `I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL` |
| `PHYSICAL_AO_SINGLE` | Single specified AO channel write with required `--ao-test-raw` & `--ao-write-function` (FC06/FC16 + FC03 read-back + safe return) | `I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL` |
| `PHYSICAL_FULL_SELECTED_SEQUENCE` | Sequential execution across selected/whitelisted channels with safe return | `I AUTHORIZE FULL SELECTED PROCESS IO BENCH SEQUENCE` |

### Consent Governance Rules:
1. **Environment Consent Only for Physical Execution:** Consent must be passed via the `WJSS_PROCESS_IO_CONSENT` environment variable. Command-line argument consent (`--consent=...`) is refused for physical modes to prevent leaking consent phrases into shell history or process tables.
2. **One-Time Consumption & Deletion:** The runner reads `WJSS_PROCESS_IO_CONSENT` once and immediately deletes it from `process.env` before establishing any network sockets.
3. **Redaction:** Consent phrases are never echoed in stdout transcripts, log files, or persisted JSON reports.

---

## 5. Parameter Validation & Hold Durations

1. **Digital Output Hold (`PHYSICAL_DO_SINGLE`):**
   - `--hold-ms=<100..2000>` is strictly required (no silent defaults).
   - Execution sequence: `FC05(true)` -> `FC01(512+addr)` -> hold for specified ms -> `FC05(false)` -> `FC01(512+addr)` safe return.
   - Summary records both `REQUESTED_HOLD_MS` and `OBSERVED_HOLD_MS`.
2. **Analog Output Write (`PHYSICAL_AO_SINGLE`):**
   - `--ao-test-raw=<0..32767>` is strictly required (no hardcoded magic numbers).
   - `--ao-write-function=FC06|FC16` is strictly required.
   - Execution sequence: `FC06/FC16(rawVal)` -> `FC03(512+addr)` -> safe return `FC06/FC16(0)` -> `FC03(512+addr)`.
   - Summary records `AO_WRITE_FUNCTION` and `AO_TEST_RAW`.

---

## 6. Write Authorization, Allowlists & Fail-Closed Cleanup

1. **No Catalogue-Wide Write:** Digital and analog output writes are never executed catalogue-wide.
2. **Explicit Allowlists:** Writes are restricted to explicitly specified channels via `--allow-do-write=<TAG>`
   and `--allow-ao-write=<TAG>`.
3. **Safe-Return Cleanup Invariant:**
   - Any channel successfully commanded during a trial is recorded in `touchedOutputs`.
   - On completion, abort, or failure, the runner deterministically commands safe-return states:
     - DO safe return: `false` (FC05 coil false).
     - AO safe return: `0` (FC06/FC16 register 0).
   - Only channels touched during the current trial are returned; untouched channels are never disturbed.
4. **Read-Back Truth Separation:**
   - FC01 and FC03 read-back observations represent internal process-image state in the coupler,
     not physical field-device actuation proof.

---

## 7. Owner-Local Command Templates

### 7.1. Simulation Mode (Safe Everywhere)
```bash
node tools/process-io-bench/bench.mjs --mode=SIMULATION
node tools/process-io-bench/bench.mjs --mode=SIMULATION --read-profile=LEGACY_PRODUCTION_COMPAT
```

### 7.2. Physical Read-Only Trial (Workbook Canonical Profile)
```bash
export WJSS_PROCESS_IO_CONSENT="I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL"
node tools/process-io-bench/bench.mjs \
  --mode=PHYSICAL_DI_AI_READ \
  --wago-t8-endpoint=<OWNER_LOCAL_WAGO_T8_ENDPOINT> \
  --unit-id=1 \
  --read-profile=WORKBOOK_CANONICAL
```

### 7.3. Physical Read-Only Trial (Legacy Production Compat Profile)
```bash
export WJSS_PROCESS_IO_CONSENT="I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL"
node tools/process-io-bench/bench.mjs \
  --mode=PHYSICAL_DI_AI_READ \
  --wago-t8-endpoint=<OWNER_LOCAL_WAGO_T8_ENDPOINT> \
  --unit-id=1 \
  --read-profile=LEGACY_PRODUCTION_COMPAT
```

### 7.4. Physical DO Single Trial (Explicit Hold Duration)
```bash
export WJSS_PROCESS_IO_CONSENT="I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL"
node tools/process-io-bench/bench.mjs \
  --mode=PHYSICAL_DO_SINGLE \
  --wago-t8-endpoint=<OWNER_LOCAL_WAGO_T8_ENDPOINT> \
  --unit-id=1 \
  --allow-do-write=DO-001 \
  --hold-ms=500
```

### 7.5. Physical AO Single Trial (FC06 Single Register)
```bash
export WJSS_PROCESS_IO_CONSENT="I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL"
node tools/process-io-bench/bench.mjs \
  --mode=PHYSICAL_AO_SINGLE \
  --wago-t8-endpoint=<OWNER_LOCAL_WAGO_T8_ENDPOINT> \
  --unit-id=1 \
  --allow-ao-write="AO-001+" \
  --ao-test-raw=6553 \
  --ao-write-function=FC06
```

### 7.6. Physical AO Single Trial (FC16 Multiple Registers)
```bash
export WJSS_PROCESS_IO_CONSENT="I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL"
node tools/process-io-bench/bench.mjs \
  --mode=PHYSICAL_AO_SINGLE \
  --wago-t8-endpoint=<OWNER_LOCAL_WAGO_T8_ENDPOINT> \
  --unit-id=1 \
  --allow-ao-write="AO-001+" \
  --ao-test-raw=6553 \
  --ao-write-function=FC16
```

---

## 8. H2 Bench Acceptance Record (2026-10-31) — Owner Decision

**Owner decision: the H2 Modbus Simulator Function/Address Bench is ACCEPTED AS PASS.** The Modbus
simulator at the loopback endpoint `127.0.0.1:502` (Unit ID `1`) is the accepted current H2
function/address bench reference. This record closes the H2 bench evidence question; it changes no
Product behaviour, no address, no function code and no authorization.

**Product head of record:** `5ae783cb7177eb3b629a097a48aa40c508868b2c` (direct parent
`edbffae5c2be3853915d9d6b3fc0d1ce34a1b744`; approved base `main`
`bfd2867af1dda29fcbc1987328be3d131a189d0a`). All three trials below were executed by the Owner on
that exact head against the simulator only. Consent was supplied only through
`WJSS_PROCESS_IO_CONSENT`; no consent phrase appears in any transcript, JSON report or this record.

### 8.1 Accepted read trial (`PHYSICAL_DI_AI_READ`, `WORKBOOK_CANONICAL`)

Endpoint `127.0.0.1:502`, Unit ID `1`, accepted `true`, outcome `SUCCESS`.

| Evidence | Result |
|---|---|
| FC02 discrete inputs, addresses 0 through 39 | completed — 40 DI reads |
| FC04 input registers, addresses 0 through 35 | completed — 36 AI reads |
| FC01 coils, addresses 512 through 543 | accessible — 32 DO read-backs |
| FC03 holding registers, addresses 512 through 513 | accessible — 2 AO read-backs |
| DO channels tested / AO channels tested | 0 / 0 |
| `cleanupAudit` | empty |
| Writes (FC05, FC06, FC15, FC16) | none occurred in the read trial |

### 8.2 Accepted single-DO trial (`PHYSICAL_DO_SINGLE`)

| Evidence | Result |
|---|---|
| Selected tag | `DO-001` |
| FC05 command address | 0 |
| Command TRUE protocol response | `SUCCESS` |
| Requested hold / observed hold | 500 ms / 502 ms |
| FC05 safe return FALSE protocol response | `SUCCESS` |
| DO channels tested / AO channels tested | 1 / 0 |
| Unrelated DO write / AO write | none / none |
| Touched-output cleanup scope | `DO-001` only |
| Cleanup safe state | `false` |

### 8.3 Accepted single-AO trial (`PHYSICAL_AO_SINGLE`)

| Evidence | Result |
|---|---|
| Selected tag | `AO-001+` |
| Function / write address | `FC06` / 0 |
| Test raw | 1000 |
| FC06 command protocol response | `SUCCESS` |
| FC06 safe return raw 0 protocol response | `SUCCESS` |
| AO channels tested / DO channels tested | 1 / 0 |
| Unrelated AO write / DO write | none / none |
| Touched-output cleanup scope | `AO-001+` only |
| Cleanup safe state | raw 0 |

### 8.4 Simulator limitation (recorded; not a WJSS Product failure)

The selected simulator does **not** implement WAGO output process-image mirroring. FC01 address 512
did not mirror FC05 address 0 during the simulator DO trial, and FC03 address 512 did not mirror FC06
address 0 during the simulator AO trial. Mirror equality is therefore **NOT APPLICABLE** to the
current simulator acceptance. This limitation is not a WJSS Product failure; real WAGO process-image
mirroring remains **DEFERRED BY OWNER**. This record does not state or imply that real WAGO mirroring
passed.

### 8.5 Deferred follow-up gate `H2-WAGO-MIRROR` (not a current merge blocker)

To be executed later against a later exact Product head, after validation appropriate to that
changed surface, and only under separate explicit Owner authorization:

1. FC05 write address n compared with FC01 read-back address 512+n.
2. FC06 or FC16 write address n compared with FC03 read-back address 512+n.
3. Command-state and safe-return mirror verification.
4. Real device timing.
5. Physical field observation recorded separately from process-image state.

### 8.6 Status at this record

- H2 SOFTWARE IMPLEMENTATION: **PASS**
- H2 OWNER-LOCAL SOFTWARE VALIDATION: **PASS**
- H2 MODBUS SIMULATOR FUNCTION/ADDRESS BENCH: **PASS**
- REAL WAGO ENDPOINT CONNECTIVITY (`<OWNER_LOCAL_WAGO_T8_ENDPOINT>`, never committed): **NOT EXECUTED / NOT VERIFIED**
- REAL WAGO PROCESS-IMAGE MIRRORING: **DEFERRED BY OWNER**
- REAL FIELD-DEVICE ACTUATION: **NOT EXECUTED / NOT VERIFIED**
- PR #42: **OPEN / NOT MERGED**
- MERGE: **HOLD PENDING OWNER AUTHORIZATION**
