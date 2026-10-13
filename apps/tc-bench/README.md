# Isolated TC bench software checkpoint

**Software-only. SOURCE AUTHORED; .NET compilation/execution and genuine new locks pending.**
See `docs/STAGE_ONE_NODE_TC_SCOPE_GATE.md` and ADR-0019 for authorization and handoff.

- Normal executable behavior is configuration/evidence-plan SIMULATION only.
- T01 is representation only, non-executable. No physical client is constructed by Program.
- `--physical-test` is hard-refused, with or without `--authorization`.
- No environment activation or free-form endpoint arguments.
- Accepted selectors: `--trial T01`, `--dry-run`, optional `--manifest FILE`.
- Manifest bytes must match the independent embedded expected digest and exact tuple.
- No automatic acceptance, retry, reconnect, polling, fallback, capture or Product value.

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
