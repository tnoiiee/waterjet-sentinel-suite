# ADR-0019 — Isolated one-Node TC bench ownership and quarantine

**Status: ACCEPTED scope decision by explicit Owner instruction (2026-10-13).**
**Software validation closeout (2026-10-13):** Owner validated exact implementation
`c0bc2c71636d3206098fd890e1de40c46fabae65`; genuine locks committed in direct child
`e71e33b22d039b6cd5940fbe94da1879f71142fa`. Targeted/full Release 0 warnings/0 errors,
focused 81/81, full .NET 536/536, SIMULATION dry-run and fixture parity 7/7 passed Owner-locally.
Agent lock/static/Node checks are separate; no Agent-side .NET execution. Final docs/proof gates
precede software MERGE READY recommendation for an OPEN Owner-review PR, not a merge.
[Scope closeout](../STAGE_ONE_NODE_TC_SCOPE_GATE.md) records BOTH LF/CRLF lock hash representations,
R40 explanation, exact history and evidence provenance. The accepted decision authorizes software,
fake-client and loopback validation only; no deployment, physical trial or capture.

## Decision

Approach A: a separate, Owner-invoked, one-shot bench process is the temporary sole TC
session owner for this gate. This is a bench-only exception to ADR-0010's Runtime session
ownership. Runtime and Kiosk remain unchanged and disconnected from physical acquisition.

Default is SIMULATION/dry-run: no client, socket, DNS, or network access. T01 is represented
in a canonical-derived, embedded catalog with `executionAuthorized: false` and no trusted
authorization digest. The executable refuses physical execution, including self-declared
external authorizations. Software implementation approval is not T01 approval.

One invocation: one client, one connect attempt, at most one read; no retry, reconnect,
fallback, scan, polling, detached task, Product publication, engineering conversion or control.
The executable exposes configuration planning only in this checkpoint. The internal runner
is a software-only loopback seam, accessible to the focused tests, not a physical bypass.

Three independent layers:

- AdapterOutcome: observed connect/read classification and diagnostic metadata.
- WireEvidenceOutcome: independent offline review of passive request/response evidence.
- TrialAcceptance: explicit offline Owner decision. Acquisition cannot accept itself.

Raw diagnostic success without matching wire evidence is QUARANTINED. Acceptance requires
successful adapter outcomes, exact wire identity/framing/data, trial/time correlation, complete
cleanup and Owner review. A complete failed evidence record is not a successful acquisition.
Synthetic test records can exercise the offline validator without making a physical claim.

## Dependency and observability

ADR-0018's SELECT_FLUENTMODBUS_5_3_2 stays closed. No custom transport, fork, dependency
replacement/version change, or generic execute-function operation is authorized.

Pinned upstream v5.3.2 (commit `5e44710bbea12d279fac78fb91eaf7081babb0de`) discards TCP response
transaction/Unit identifiers and checks minimum register-payload length. Its generic memory
cast can hide an odd trailing byte. The adapter uses the existing library byte-payload read
overloads, checks exact two-byte-per-word size before conversion, copies memory, and converts
the explicit BigEndian candidate. This is payload validation, not a custom transport or a
framing-verification claim. Actual MBAP identity/count fields require separate wire evidence.

## Evidence boundary

No capture implementation is added. Later passive capture must be separately authorized,
restricted to the approved bench endpoint/502, and retained outside Git. No discovery,
injection, probing or interface scanning. Public export is typed/allowlisted, hash-bound and
rejects paths, unreviewed prose, secrets and unapproved word excerpts. Original evidence is
never overwritten or silently redacted. Hash identity is not correctness or acceptance.

Owner review uses neutral identifiers and binds the canonical acquisition manifest digest.
Trusted catalog bytes/digest are embedded in the reviewed build. Hashes are not signatures:
the Owner must verify source/build identity and independently review authorization digests.

## Protected and future gates

Port 502 is an Owner-authorized bench connection setting, not verified physical state.
Canonical Node/evidence records stay unresolved/unverified. All topology, Sensor pairing,
Runtime/Kiosk, Queue, Cleaning, Pump, Valve, Water Jet, Galil, alarm/trip/interlock/control,
package pins, existing locks and fixtures stay unchanged. The only scanner policy addition is
the Owner-approved selected-term exception at the two exact new dependency-lock paths; engine
and S1/S2/S4–S9 stay unchanged. Two genuine new locks are committed at `e71e33b`; existing
locks and versions are unchanged. LF/CRLF hashes are separately labeled, with no lock edits.

Separate future gate: exact implementation/build SHA, genuine locks and software validation,
physical identity/isolation, T01 authorization, PHYSICAL_TEST deployment, capture method,
finite bench lifetime/stop procedure, durable evidence destination, and Owner review.
Every later trial requires separate authorization; no batch approval.

**HARDWARE CONTACT NOT AUTHORIZED; T01 NOT AUTHORIZED; PASSIVE CAPTURE NOT AUTHORIZED;
PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED.**
