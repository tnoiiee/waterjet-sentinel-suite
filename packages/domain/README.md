# Wjss.Domain — pure domain kernel

Dependency rule (ARCHITECTURE.md §20 rule 1): this project must never reference
transport, storage, UI, vendor, or spike code. BCL + `Wjss.Contracts` only.

## Contents at Stage 0.3A-1 (skeleton)

| File | Purpose |
| --- | --- |
| `ProfileStartPolicy.cs` | Device-profile startup gate: only `SIMULATOR` may start in Stage 0.3A (`TEST_HARDWARE` / `PRODUCTION` refuse with machine code `PROFILE_NOT_AUTHORIZED_FOR_STAGE_03A`). |

## Deliberately NOT here

- Queue eligibility policy (open Owner decision — queue eligibility matrix).
- Safe Return failure policy and outcome naming (open Owner decision — critical Pump matrix).
- Alarm blocking/acknowledgement policy (pending `ALARM_MODEL.md` §10).
- DirtyScore classification THRESHOLDS (Published Configuration; values are deployment data, never invented).
- Any physical device, address, register, coordinate, or timing constant.

**Status:** SOURCE-AUTHORED IN ARENA · NOT COMPILED IN ARENA · Owner-local build
required (`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`).
