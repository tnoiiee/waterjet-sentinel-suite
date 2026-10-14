# Isolated TC bench — validated software-only foundation

## Current execution classes (2026-10-15)

The bench has three mutually exclusive execution classes selected on the command line. None of
them is hardware, coupler, TC-NODE-01 or T01 evidence; every record stays QUARANTINED in the bench.

| Selector | Provenance | I/O | Endpoint | Authorization |
|---|---|---|---|---|
| (default) or `--dry-run` | `SIMULATION` | none | default catalog address, port 502, reported only | none |
| `--local-simulation` | `LOCAL_TCP_SIMULATION` | one connect, one read on the local host | effective `127.0.0.1:502` | none, none consulted |
| `--physical-test` | `PHYSICAL_DIAGNOSTIC` | one connect, one read | trusted tuple only, no override | signed detached artifact, fail-closed gate, single use |

Accepted arguments: `--trial T01`, optional `--manifest FILE`, one selector, and (only with
`--physical-test`) `--authorization FILE --signature FILE`. There is no free-form endpoint,
port, unit, function, start or quantity argument, no listener and no environment activation.

### Local Simulation (`--local-simulation`)

Owner-local loopback execution class for a simulator listening on the local host, port 502. See
[`docs/TC_BENCH_LOCAL_SIMULATION.md`](../../docs/TC_BENCH_LOCAL_SIMULATION.md) for the Owner-local
procedure and output reading guide.

- Default (template) configuration stays the trusted catalog address / `502` / FC03 / Unit `0` /
  start `0` / quantity `40`; it is printed unchanged as `configuration` next to the effective
  `invocation`.
- The invocation configuration (`LocalSimulation.InvocationConfiguration`) is the default tuple with
  ONLY the address replaced by the literal `127.0.0.1`. It is built in process, never from an
  argument value, file or environment variable.
- `LocalSimulation.RequireValid` refuses, BEFORE any client exists: any address text other than the
  literal `127.0.0.1` (the default address, other IPv4, `localhost`, IPv6, alternative spellings),
  any port outside `1..65535`, and any request other than the trusted default.
- `TrialRunner.RunLocalSimulationAsync` reuses the one-shot lifecycle unchanged: one client, one
  connect, at most one read, finite timeouts, explicit disconnect, observed disposal, bounded
  cancellation, generation/epoch fencing; no retry, reconnect, fallback or correction.
- Evidence provenance `LOCAL_TCP_SIMULATION`; endpoint `127.0.0.1` and effective port recorded;
  authorization `NOT_ATTEMPTED` / `NOT_PROVIDED`; `physicalCapabilityEnabled false`;
  `trialAcceptance QUARANTINED`. `TrialEvidence.Validate` refuses a local record with any other
  endpoint and refuses offline ACCEPTANCE of a local record. Returned words are reported as
  received; the application encodes no expected register pattern.
- Physical protections are untouched: no `PhysicalTrialPlan` exists for the loopback address, and an
  authorization naming it is refused by exact tuple binding.
- Automated tests use a fake client or an ephemeral loopback listener port (never 502). The
  Owner-local simulator run is performed by the Owner; the Agent claims no interoperability.

## Dated history

**Software-only Owner-local validation CLOSED (2026-10-13); ready for Owner PR review after
final documentation/proof gates.** Exact implementation `c0bc2c71636d3206098fd890e1de40c46fabae65`;
genuine locks `e71e33b22d039b6cd5940fbe94da1879f71142fa`. Owner targeted/full Release **0w/0e**,
focused **81/81**, full .NET **536/536**, fixture parity VERIFY ONLY **7/7**.
[Scope closeout](../../docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md) records separate Owner/Agent evidence,
BOTH committed-LF and Owner-Windows-CRLF lock hashes, R40 disposition and dated repairs.
Agent ran lock/static/Node checks only; no Agent .NET execution or physical deployment.

Dated 2026-10-13 statements (superseded where noted): at that checkpoint the executable had
SIMULATION only and `--physical-test` was hard-refused. Since K6 (PR #24, merged to `main` at
`f356b162`) `--physical-test` is a fail-closed gated path, and since 2026-10-15 `--local-simulation`
is the loopback class described above. Still current:

- T01 is representation only, non-executable without a verified, consumed Owner authorization.
- No environment activation or free-form endpoint arguments.
- Manifest bytes must match the independent embedded expected digest and exact tuple.
- No automatic acceptance, retry, reconnect, polling, fallback, capture or Product value.

Owner-local `--dry-run` proof: mode/provenance SIMULATION, executionAuthorized false,
physicalVerification NOT_VERIFIED; connect/read NOT_ISSUED, wire NOT_PROVIDED,
TrialAcceptance QUARANTINED, copiedWordCount null, words empty, noWrite/noFallback true.
No physical client/I/O, T01 execution, Product current value or temperature interpretation.
Agent exporter 14/14, S2 14/14, S3 19/19, Boundary 0 findings, Commissioning 218/218 PASS;
these do not stand in for Owner-local .NET evidence or authorize physical work.

The build requires Node plus the pinned .NET SDK. The offline exporter imports the existing
canonical configuration, validates it, and writes only into ignored `obj` build output.
The resource and detached digest are embedded; generated addresses are never another tracked
source. Source SHA uses normalized UTF-8/LF; configuration SHA uses the existing canonical
serialization; manifest/catalog hashes use exact UTF-8 bytes including the final LF.
A self-declared hash in external input is not authority. Verify the reviewed build identity.

The internal software runner accepts only the literal loopback host and a non-502 port; tests
supply an ephemeral listener port or a fake client. It is not a physical execution API.
It observes bounded Connect work even after cancellation, awaits read/disconnect/disposal,
and coordinates epoch/generation checks with Stop and final handoff. No task is detached.

AdapterOutcome, WireEvidenceOutcome and TrialAcceptance are distinct. Diagnostic data remains
QUARANTINED. Offline review is a library API, not an acquisition/CLI acceptance command.
Physical deployment requires another explicit gate; this checkpoint cannot execute T01.

Public evidence accepts typed fields only. Neutral identifiers, detached SHA-256 hashes and
Owner-approved indexed-word excerpts are supported. No raw exception message, local/storage
path, secret or unreviewed prose. Export rejects rather than alters facts. Caller-controlled
external storage must preserve originals; Program writes only stdout, never evidence files.

Raw bytes captured from the network are not library payload bytes or copied words. The adapter
checks/copies the library byte payload then applies the explicit BigEndian candidate; numeric
words are not temperatures. Framing/count/identity observations must come from wire review,
not arithmetic on a word count. Known gaps include physical timing, image mapping, interpretation,
original-source completeness, and all physical evidence.

**HARDWARE CONTACT NOT AUTHORIZED**

**T01 NOT AUTHORIZED**

**PASSIVE CAPTURE NOT AUTHORIZED**

**PHYSICAL_TEST DEPLOYMENT NOT AUTHORIZED**
