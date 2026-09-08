# ADR-0080: Bound transitional context-publication recovery

- Status: Accepted
- Date: 2026-09-08

## Context

ADR-0078 introduced a deliberately transitional C1 shape for a crash after a
context-publication head committed but before its separate source receipt did.
That text allowed a missing receipt to infer `Published` or `Reactivated` from
immutable predecessor, creation, metadata, or target-existence facts. Those
facts cannot prove which transition was most recently committed, and therefore
cannot safely manufacture receipt provenance or a health/revision reduction.

C2 instead commits the guarded head, exact receipt, and final reduction in one
transaction. The only transitional work that remains is bounded recovery of a
provable pre-C2 head-before-receipt crash. Sources remain disabled; this ADR
does not add a journal, schema, migration, activation, or a new publication
path.

## Decision

Guarded publication is receipt-first. A present exact lane receipt is replayed
only after semantic comparison of the complete guard and receipt template and
after recomputing the selected snapshot from the current ordered request
documents. It returns the persisted original snapshot ID and disposition. Any
mismatch is fatal and mutation-free; replay never rewrites a head, source
health, revision state, issue projection, receipt, or immutable publication
document.

With an absent receipt, C2 first validates the exact typed guard, source enablement,
outer `HandoffReady` cycle and bundle digest, finalized source observation and
observation digest, exact watermark, strict consumer-prefix state, and the
authoritative prior-cycle health selection described below. Only then may it
read and compare the current head. The comparison is absolute:

- `expectedPreviousSnapshotId != currentHeadSnapshotId` is fatal and
  mutation-free, including when `currentHeadSnapshotId == targetSnapshotId`.
- `expectedPreviousSnapshotId == currentHeadSnapshotId == targetSnapshotId`
  is the sole transitional recovery branch. It verifies the exact immutable
  target snapshot, ordered entries, payload bytes, metadata, guard, bundle,
  and observation identities, then records only the matrix-valid `Unchanged`
  receipt and final reduction. It does not rewrite the head, target, entries,
  payloads, timestamps, or predecessor.
- `expectedPreviousSnapshotId == currentHeadSnapshotId` and the current head
  differs from the target is the ordinary C2 atomic path. It may produce
  `Published` or `Reactivated` only from the normal current transaction and
  atomically writes the head/result, exact receipt, strict next consumer prefix,
  and final health/revision reduction.

Missing-receipt recovery must never infer the latest transition from immutable
predecessor, creation timestamp, metadata, target existence, or any other
historical artifact. A malformed record, incomplete proof, or any other
ambiguity is fatal and requires manual recovery by the owner; it must not cause
head/document rewriting or synthetic provenance.

Prior health selection is not inferred from the current head. Before a guarded
metadata-unchanged write or reduction, the implementation selects the exact
authoritative prior completed cycle named by `health.lastCompletedCycleId` and
validates that cycle's immutable source observation, prior lane receipt, and
the prior cycle's `stalenessReferenceAtUtc` agree with the health selection.
Absent, malformed, cross-cycle, or unequal evidence is fatal and mutation-free.
Current-cycle freshness remains a separate calculation against the current
cycle's `stalenessReferenceAtUtc`; prior-cycle validation never substitutes a
current freshness date.

No transition journal, new persistence schema, data migration, or compatibility
fallback is introduced. Existing C2 types and paths remain the complete
implementation surface.

## Alternatives considered

- **Infer `Published` or `Reactivated` from immutable target history:** Rejected
  because immutable history does not identify the latest transition.
- **Treat a current target head as an unconditional `Unchanged`:** Rejected
  because a stale expected head is still a compare-and-swap conflict.
- **Add a transition journal or migrate existing records:** Rejected because
  the bounded, provable `Unchanged` case avoids schema expansion while C2 makes
  future guarded commits all-or-nothing.

## Consequences

- C2 corrects five local areas together: exact receipt replay, guard-before-head
  ordering, fatal expected/current mismatch handling, bounded absent-receipt
  recovery, and authoritative prior-cycle health selection.
- The direct guarded Firebase regression matrix must cover present-receipt
  replay and mismatch; absent receipt with expected/current mismatch (including
  current equals target); provable `Unchanged` with no head/document rewrite;
  ordinary atomic `Published` and `Reactivated`; malformed/ambiguous proof;
  strict prefix, supersession, legacy null-guard, crash/retry, and prior-cycle
  versus current-cycle staleness cases.
- Manual ambiguity recovery, flag-off rollback by reviewed revert, the project
  owner as recovery owner, and separate restoration/activation/completion gates
  remain unchanged. Source flags remain false.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 design](../designs/p1-04-05-context-refresh.md)
- [P1-04/P1-05 execution packet](../p1-04-05-execution-packet.md)

## Supersedes

Only ADR-0078's transitional C1 separate-receipt inference paragraph. All
other ADR-0078 decisions remain operative.
