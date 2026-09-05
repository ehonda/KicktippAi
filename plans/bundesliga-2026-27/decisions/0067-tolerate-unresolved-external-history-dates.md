# ADR-0067: Tolerate unresolved external history dates with a collection-date proxy

- Status: Accepted
- Date: 2026-09-05

## Context

The rolling selected-history windows can contain completed non-league fixtures before an exact external date has reached the retained map. Rejecting an otherwise valid row blocks the production context collection, while inventing an exact played date would corrupt history provenance.

## Decision

For only `DFB`, `CL`, `EL`, `ConfL`, `2.BL`, and `Releg` selected-history rows that have no exact incoming, prior, outcome, or fixed-map date, retain the row with `Played_At=collection-date-proxy:YYYY-MM-DD`. Capture one injected collection instant and derive that date in `Europe/Berlin`.

Before the history gate, read the latest version of every selected document. Parse those prior six-column documents strictly and join only on document name, competition, home team, away team, normalized score, and original annotation. Exact evidence must agree and replaces a proxy. In the absence of exact evidence, one matching prior proxy remains stable; conflicting incoming/prior proxies fail. Missing prior documents are valid first insertions.

Unresolved `1.BL`, unknown competitions, malformed rows or prior documents, duplicate identities, conflicts, and unexpected selected-document sets remain fatal. Selected histories retain one atomic publication; no repository schema or interface change is required. Diagnostics report occurrences and distinct tuple groups, never unidentifiable "unique matches".

## Alternatives considered

- **Reject every unresolved row:** Rejected because routine external-source lag would keep otherwise valid context from production recovery.
- **Write a guessed exact date:** Rejected because a collection time is not match evidence.
- **Add proxy metadata to Firestore:** Rejected because the existing six-column content contract expresses the state without a schema migration.

## Consequences

- Exact evidence remains authoritative and can upgrade a prior proxy without changing row identity.
- A proxy cannot be removed by reverting code alone: if one has been published, disable the cron under existing recovery authority and use a separately reviewed atomic data restoration before old code reads it.
- Full-season closed-input collection and CL-specific context work remain separate follow-ups.

## Affected tasks

- [P1-14](../tasks/p1-14-history-source-continuity.md)

## Supersedes

None.
