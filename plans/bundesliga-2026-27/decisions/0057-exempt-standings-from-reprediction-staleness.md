# ADR-0057: Exempt standings from reprediction staleness

- Status: Accepted
- Date: 2026-08-30

## Context

Bundesliga 2026/27 predictions record `bundesliga-standings.csv` in their
immutable resolved-context manifest. The existing outdated checker compares the
exact version and content hash of every ordinary context document, so any
standings refresh makes every still-open prediction stale.

The first production-live weekend demonstrated the consequence. Bayern
München's Friday 5:1 result over VfB Stuttgart changed only the standings
document, yet the next scheduled run created index-1 predictions for all eight
remaining fixtures in every generating configuration and propagated index 1
through the copy lanes. Repeating that policy after Saturday fixtures would
spend another reprediction on Sunday fixtures.

A small standings change, especially early in the season, is not independently
material enough to justify replacing every remaining prediction. The current
behavior consumes limited reprediction indices and model budget in a routine,
predictable pattern.

## Decision

For `bundesliga-2026-27` match predictions, a later version or different content
hash of the exact canonical ordinary context document
`bundesliga-standings.csv` does not by itself make a stored prediction
outdated. The outdated checker excludes that document from its current-version
and current-content comparison.

The exemption affects only the decision to reuse an existing match prediction.
Standings remain a required manifest entry and prompt document, retain their
exact recorded provenance, and use the current content whenever a prediction
is generated for an initial prediction, a forced prediction, or another
eligible staleness trigger.

All other ordinary documents keep their exact version/content staleness checks.
Roster and Club Elo publication snapshot and selected-document checks remain
unchanged. Missing or malformed manifests, invalid scope, and other provenance
failures continue to fail closed. Bonus prediction behavior is not changed.

## Alternatives considered

- **Repredict after every standings update:** Rejected because Friday and
  Saturday results systematically consume repredictions for the rest of the
  same weekend without enough independent informational value.
- **Remove standings from match context and manifests:** Rejected because the
  current table remains useful when a prediction is generated and its exact
  provenance must remain reconstructable.
- **Apply a matchday or season-progress threshold:** Rejected for now because
  it adds policy complexity and still treats standings as an independent
  reprediction trigger; a later accepted ADR may introduce a materially
  justified policy.
- **Ignore all ordinary context changes:** Rejected because histories and
  community rules can materially affect a prediction and must retain their
  current exact staleness behavior.

## Consequences

- Routine Friday and Saturday standings refreshes no longer consume model
  calls, cost, or limited reprediction indices for the remaining weekend.
- A prediction may intentionally retain the standings snapshot recorded when
  it was created until another eligible trigger causes regeneration.
- Regression coverage must distinguish the exact standings exemption from all
  other provenance checks and verify copy-posting reuse.

## Affected tasks

- [P1-12](../tasks/p1-12-standings-reprediction-exemption.md)

## Supersedes

Only [ADR-0020](0020-record-immutable-match-context-manifests.md)'s requirement
to compare the current ordinary-document identity when that document is
`bundesliga-standings.csv`. ADR-0021's append-only identity, recorded content
hash, and exact reconstruction rules remain accepted and unchanged.
