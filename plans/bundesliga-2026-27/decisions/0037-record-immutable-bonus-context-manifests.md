# ADR-0037: Record immutable bonus-context manifests

- Status: Accepted
- Date: 2026-08-21

## Context

ADR-0020 defines immutable provenance for Bundesliga match predictions, but bonus prompts have a different question-driven document set and storage shape. ADR-0024 requires every Bundesliga bonus question to read the two aggregate documents from current validated Club Elo and roster publication heads and may add exact roster documents. Persisting only document names or a creation time cannot reconstruct which bytes and coherent publication sets produced a bonus answer.

Bonus prediction values and metadata are retrieved through legacy repository methods. A latest-value read racing a latest-metadata read can otherwise validate the manifest for one persisted prediction while submitting another prediction value. In addition, treating a missing, corrupt, or stale manifest as an isolated per-question warning can omit that question and still let the command return success or place other answers.

## Decision

Every new `bundesliga-2026-27` normal bonus prediction and reprediction persists a canonical `resolvedBonusContextManifest` with its metadata. The manifest is an immutable validated value object with this exact ordered root schema:

1. `competition`;
2. `communityContext`;
3. `documents`;
4. `rosterPublicationSnapshotId`;
5. `clubEloPublicationSnapshotId`.

Each ordered document contains exactly `{ kind, name, version, contentSha256 }`. The competition is the canonical Bundesliga ID, the community is the exact prediction scope, versions are nonnegative, and hashes and both snapshot IDs are lowercase SHA-256 values. The ordered documents begin with KPI `club-elo-rankings` and KPI `team-squad-summary`, followed only by the exact question-selected `roster-{manifestSlug}` context documents in unique manifest-slug order. The manifest stores identities, versions, and hashes, not full prompt content.

The provider must implement `IResolvedBonusContextProvider` and resolve those documents from semantically validated current Club Elo and roster publication heads. The prediction repository must implement `IResolvedBonusContextPredictionRepository`; Bundesliga normal and reprediction writes cannot fall back to the legacy save methods. The Firestore metadata field is `resolvedBonusContextManifest`. Existing metadata remains readable with a missing optional manifest so historical data is not rewritten, but such a row is not reusable as a current Bundesliga prediction. WM26 continues to use its legacy provider, persistence, and timestamp freshness behavior.

The metadata row retains the exact bonus prediction payload. Every Bundesliga cache consumer that separately reads the latest value and metadata must compare their ordered selected-option IDs before trusting the manifest. Missing metadata, a value/metadata mismatch, corrupt canonical JSON, wrong scope, invalid document selection, or an unavailable or semantically invalid publication head fails closed. Freshness compares the manifest with the exact current headed roster and Club Elo snapshots and re-resolves the question selection; storage presence never expands that selection.

For the Bundesliga generation command, a cache/coherence read failure, provenance-resolution failure, unsafe cached row, or provenance-capable persistence failure aborts the command with a nonzero result before any selected bonus answers are placed. Those failures cannot be swallowed by per-question continuation. Reprediction may replace a coherently read but outdated row while below its explicit limit; an incoherent pair is not considered a safe reprediction source. `verify-bonus --check-outdated` treats the same missing, mismatched, or stale provenance as a discrepancy. P0-18 must include that explicit outdated check in the reusable Bundesliga workflow; this ADR does not change the workflow file in the P0-15 Lane B change.

## Alternatives considered

- **Reuse ADR-0020's match manifest unchanged:** Rejected because bonus selection has no match-bound seven-document catalog and needs question-selected roster documents plus content hashes.
- **Persist only document names and timestamps:** Rejected because names do not bind exact bytes or coherent publication heads.
- **Trust independent latest value and metadata reads:** Rejected because concurrency can pair provenance with a different selected-option payload.
- **Require migration of every legacy bonus row:** Rejected because optional backward reads preserve historical data while live Bundesliga reuse still fails closed.
- **Continue after a Bundesliga provenance failure:** Rejected because a successful partial generation can leave stale or manifestless questions in place.

## Consequences

- New Bundesliga bonus predictions have reproducible, content-addressed prompt provenance without duplicating prompt bodies.
- Providers and repositories advertise explicit optional capabilities, preserving legacy interface signatures for WM26 and backward reads.
- Bundesliga generation and outdated verification reject incoherent value/metadata pairs and incomplete provenance.
- P0-18 owns the workflow join that invokes `verify-bonus --check-outdated` after generation.

## Affected tasks

- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-16](../tasks/p0-16-question-aware-bonus-context.md)
- [P0-18](../tasks/p0-18-base-workflow-support.md)

## Supersedes

None. This extends the manifest principle in ADR-0020 to the distinct bonus storage contract.
