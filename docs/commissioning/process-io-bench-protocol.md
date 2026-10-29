# Process-I/O Hardware Bench Commissioning Protocol (Stage H2)

Status: **[COMMISSIONING RUNBOOK & BENCH PROTOCOL — HARDWARE NOT EXECUTED / NOT VERIFIED].**
This document defines the execution protocol, bench modes, consent governance, safety invariants,
and transcript redaction requirements for the Process-I/O hardware bench harness (`tools/process-io-bench/bench.mjs`
and `ProcessIoBenchRunner`).

---

## 1. Safety Boundary & Non-Execution Invariant

1. **Physical Hardware Status:** `PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED`.
2. **Sandbox Invariant:** Network socket communication to physical fieldbus endpoints (e.g. `192.168.1.70`)
   is strictly prohibited during development and automated sandbox execution.
3. **Execution Authority:** Physical trials are authorized exclusively on Owner-controlled hardware
   under explicit, interactive consent phrases.

---

## 2. Authoritative Physical Process-I/O Architecture

- **Canonical Device Key:** `PROCESS_IO_WAGO_T8` (single physical coupler rack; legacy 3-device model removed).
- **Physical Transport Default:** Industrial TCP, port `502`, unit ID `1` (configurable via `--unit-id` or `WJSS_PROCESS_IO_UNIT_ID`, Owner-local, verified in range 1..247).
- **Function Code & Memory Map Contract:**
  - **Discrete Inputs (DI):** 40 rows (`DI-001`..`DI-040`), Function Code **FC02**, register/bit addresses `0..39`.
  - **Digital Outputs (DO):** 32 rows (`DO-001`..`DO-031` + 1 spare):
    - Process image read-back: Function Code **FC01**, addresses `512..543` (offset 512).
    - Single channel command write: Function Code **FC05**, addresses `0..31` (offset 0).
  - **Analog Inputs (AI):** 36 rows (`AI-001+`..`AI-036`), Function Code **FC04**, input register addresses `0..35`.
  - **Analog Outputs (AO):** 2 rows (`AO-001+` + 1 spare):
    - Process image read-back: Function Code **FC03**, holding register addresses `512..513` (offset 512).
    - Single channel command write: Function Code **FC06**, register addresses `0..1` (offset 0).

---

## 3. Bench Modes & Explicit Consent Phrases

The bench runner supports five distinct operational modes. Physical modes require exact matching consent phrases:

| Mode | Allowed Operations | Required Consent Phrase |
|---|---|---|
| `SIMULATION` | Offline mock execution across all 111 catalogue rows | None required |
| `PHYSICAL_DI_AI_READ` | Read-only observation of all 40 DI channels and 36 AI channels (FC02/FC04) | `I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL` |
| `PHYSICAL_DO_SINGLE` | Single specified DO channel write with explicit allowlist (FC05 + FC01 read-back) | `I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL` |
| `PHYSICAL_AO_SINGLE` | Single specified AO channel write with explicit allowlist (FC06 + FC03 read-back) | `I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL` |
| `PHYSICAL_FULL_SELECTED_SEQUENCE` | Sequential execution across selected/whitelisted channels with safe return | `I AUTHORIZE FULL SELECTED PROCESS IO BENCH SEQUENCE` |

---

## 4. Write Authorization, Allowlists & Fail-Closed Cleanup

1. **No Catalogue-Wide Write:** Digital and analog output writes are never executed catalogue-wide.
2. **Explicit Allowlists:** Writes are restricted to explicitly specified channels via `--allow-do-write=<TAG>`
   and `--allow-ao-write=<TAG>`.
3. **Safe-Return Cleanup Invariant:**
   - Any channel successfully commanded during a trial is recorded in `touchedOutputs`.
   - On completion, abort, or failure, the runner deterministically commands safe-return states:
     - DO safe return: `false` (coil 0).
     - AO safe return: `0` (raw 0 / 0.00 mA).
   - Only channels touched during the current trial are returned; untouched channels are never disturbed.
4. **Read-Back Truth Separation:**
   - FC01 and FC03 read-back observations represent internal process-image state in the coupler,
     not physical field-device actuation proof.

---

## 5. Evidence Transcripts & Redaction Requirements

When running in physical test modes:
- Host IP addresses and network endpoints must be redacted (e.g. `192.168.1.xxx` -> `192.168.1.70` kept in Owner-local scope only; logs redact octets).
- Plaintext credentials, tokens, consent strings, and external paths must be excluded from public commit artifacts.
- Transaction ledgers record timestamp, device key, function code, address, payload summary, status, and duration without exposing network credentials.
