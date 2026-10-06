# Wjss.Runtime.Core — authoritative-state Runtime core

Intended owner of: the single-writer state loop, deterministic transitions,
Runtime revision generation, Snapshot/Delta projection, shutdown/cancellation
boundaries, and health assembly (Stage 0.3A-2, **not authorized**).

## Contents at Stage 0.3A-1

Empty, wired project (approved skeleton). It participates in the solution and
the dependency graph so that the project boundary is real from day one; the
stage marker used by the health stub lives in `Wjss.Contracts.Stage03A1` and
is re-exposed by the host — this core deliberately has no behaviour in 0.3A-1.

**Status:** SOURCE-AUTHORED IN ARENA · NOT COMPILED IN ARENA · Owner-local
build required (`docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`).
