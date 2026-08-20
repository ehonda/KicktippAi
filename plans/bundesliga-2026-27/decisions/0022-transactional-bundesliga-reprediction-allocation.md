# ADR-0022: Allocate Bundesliga repredictions transactionally

- Status: Accepted
- Date: 2026-08-20

## Context

ADR-0020 requires a provenance-capable save before a `bundesliga-2026-27` prediction can be submitted, but its original save wording did not make the reprediction index allocation atomic. Two Matchday executions can both observe the same current index before generation, then persist duplicate next indices or exceed a caller-selected maximum.

This is a durable storage identity rule, not a command-only retry detail. Cancelled matches retain their existing team-name-only lookup semantics, and legacy competition saves must remain available without acquiring the new Bundesliga provenance contract.

## Decision

New `bundesliga-2026-27` match prediction writes through the ordinary `SavePredictionAsync` and `SaveRepredictionAsync` APIs are rejected. New Bundesliga writes use only the provenance-capable APIs and a canonical validated manifest; historical reads remain supported.

For a Bundesliga provenance-capable reprediction, Matchday supplies the observed current index and configured nonnegative maximum to the storage API. The Firestore transaction re-reads the exact matching prediction set, using the normal exact-start identity for ordinary matches and the existing team-name-only identity for cancelled matches. It selects the exact model configuration's current index, requires it to equal the supplied expected index, allocates exactly one next index, and rejects the operation when the next index exceeds the configured maximum.

The transaction creates, never overwrites, a deterministic document ID derived from the full Bundesliga prediction identity and allocated index. The query read plus create makes concurrent matching writes retry or fail rather than persist duplicate or supra-maximum indices. A conflict/max failure occurs before Matchday may submit the generated prediction to Kicktipp.

No migration or deletion is performed. Existing prediction rows, including legacy and manifestless historical rows, remain readable; only new Bundesliga writes are constrained.

## Alternatives considered

- **Allocate the index in Matchday before save:** Rejected because concurrent command processes can observe the same value.
- **Use random IDs with a nontransactional save:** Rejected because storage cannot prevent duplicate semantic indices.
- **Apply the transaction to every legacy competition:** Rejected because it would change established historical save behavior without a Bundesliga need.

## Consequences

- Concurrent Bundesliga reprediction writers cannot silently create duplicate indices or go beyond the configured cap.
- A stale writer must regenerate after a conflict instead of submitting an unpersisted prediction.
- Tests and production adapters must use provenance-capable saves for new `bundesliga-2026-27` rows.

## Affected tasks

- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)

## Supersedes

Only the Bundesliga prediction-save and reprediction-allocation portion of [ADR-0020](0020-record-immutable-match-context-manifests.md). ADR-0020's manifest, reconstruction, and publication-snapshot decisions remain accepted.
