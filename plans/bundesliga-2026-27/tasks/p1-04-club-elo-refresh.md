# P1-04 — Refresh Club Elo during context collection

- Status: Interview complete — not implemented
- Priority: Highest remaining P1 priority (first)
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md)
- Design: [P1-04/P1-05 context refresh](../designs/p1-04-05-context-refresh.md)

## Outcome

Club strength observations run only inside existing Bundesliga context-collection
cycles and publish only a valid, strictly newer, fully mapped snapshot without
weakening dated-seed or last-known-good protection.

## Work items

- [ ] Freeze direct CSV bytes, coherent provider-date semantics, and explicit daily-name-to-manifest mapping; unknown/contradictory date evidence must be `UNKNOWN_SOURCE_DATE` and retain LKG.
- [ ] Add the bounded CSV candidate provider to existing `collect-context profile` acquisition, with one per-cycle immutable bundle reused across community jobs; do not add an Elo-only command or schedule.
- [ ] Enforce raw-byte hashing, frozen header, all-18/positive/deterministic data, strictly newer proven date, and the accepted two-attempt/25-second retry envelope.
- [ ] Preserve per-community selection and atomic heads; distinguish source observation from source date and do not reset staleness on unchanged data.
- [ ] Add source-health/issue deduplication and concise summary/warning diagnostics only after the implementation seam is independently reviewed.
- [ ] Add development-first valid, unchanged, partial, rejected, outage, retry-boundary, same-cycle reuse, dry-run, and serial-descendant tests.
- [ ] Add future source attribution linked from the repository-root README; do not place source attribution in prompt documents.

## Validation

- Prove exact source bytes/date/name mapping, valid/no-change/rejected/outage fixtures, and LKG retention in development.
- Obtain one real accepted CSV refresh and one later no-change context cycle before completion; first production activation remains separately reviewed.

## Complete when

- A real accepted CSV refresh and a no-change cycle are observed after approved activation.
- Partial, stale, or semantically rejected data cannot publish a new version or erase LKG.
