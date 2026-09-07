# ADR-0076: Pause the orchestration hook-readiness gate

- Status: Accepted
- Date: 2026-09-07

## Context

ADR-0075 introduced a synchronous `UserPromptSubmit` marker intended to prove
that the exact repository hook definition was trusted and active for the
current session. In practice, the marker is not reliably injected into the
root turn, including after an explicit `$orchestrate` re-invocation. Treating
its absence as an owner gate can therefore block an otherwise ready run even
after the owner has already trusted the hooks.

Compaction recovery has a separate integrity boundary. Its hook manifest binds
`.codex/hooks.json`, the recovery implementation, packet construction,
admission controls, and policy inputs to raw-byte digests. Capsule checksum,
session identity, packet validation, ownership reconciliation, and cold
reconstruction remain independent of prompt-time trust evidence.

## Decision

Orchestration startup does not require prompt-time hook-trust evidence. Remove
the `UserPromptSubmit` hook, the capsule `hook_trust` field, and validation of
that field. Absence of a prompt-time marker is not an owner or readiness gate.

Keep the `PreCompact` and compact `SessionStart` recovery hooks and their
manifest-based drift validation. Capsules created with the earlier schema may
retain an extra `hook_trust` field; it is ignored so in-flight recovery state
remains compatible.

Revisit hook-readiness verification only after a reliable exact-session
mechanism can be forward-tested without turning missing context injection into
an unworkable startup blocker.

## Alternatives considered

- **Keep the fail-closed marker gate:** rejected because absence currently
  proves neither that hooks are disabled nor that the owner withheld trust.
- **Accept manual trust confirmation for each run:** rejected because the owner
  has already established durable trust and the repeated prompt would retain
  the same unnecessary gate.
- **Remove compaction recovery hooks too:** rejected because their validated
  recovery behavior is separate from the broken startup check.

## Consequences

- `$orchestrate` can proceed without a `UserPromptSubmit` marker.
- Recovery still validates the capsule, packet manifests and material, preview
  ceiling, session identity, owner gates, and ownership state.
- Hook trust is temporarily an owner-established premise rather than a runtime
  proof.
- A later readiness check requires a new reviewed decision and working
  forward-test evidence.

## Affected tasks

- All remaining Bundesliga P1 orchestration.
- Repository `$orchestrate` startup and recovery tooling.

## Refines

- [ADR-0075](0075-refine-orchestration-recovery-and-resource-admission.md),
  only for positive hook-trust evidence and its startup owner gate. All other
  context-tier, recovery, review, resource-admission, and archival decisions
  remain operative.
