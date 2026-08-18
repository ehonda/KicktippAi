# P0-15 — Clean the live context-document contract

- Status: Not started
- Priority: P0
- Depends on: [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-13](p0-13-bonus-context-baseline.md), [P0-14](p0-14-profile-driven-collection.md), [P0-22](p0-22-history-played-dates.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0015](../decisions/0015-club-elo-prompt-publication-contract.md), [ADR-0016](../decisions/0016-validate-club-elo-publication-metadata.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md)

## Outcome

No 2026/27 match or bonus prediction can receive stale, duplicated, deprecated, old-season, or cross-competition context. Manually stale `team-data` and `manager-data` artifacts are superseded by Club Elo, rosters, and squad summaries before any production prediction.

## Work items

- [ ] Inventory every live `team-data` and `manager-data` consumer, field, uploader, and prompt reference.
- [ ] Map fields already supplied by `team-squad-summary` and roster coach rows; remove duplicate context rather than refreshing it twice.
- [ ] Define the remaining team/manager fields, accepted sources, provenance, freshness, and ownership in an ADR.
- [ ] Implement a focused derived document or collector only for remaining required fields.
- [ ] Replace broad name-substring routing with explicit document names from the Bundesliga profile.
- [ ] Remove superseded upload utilities, files, and tests once no live consumer remains.
- [ ] Audit the complete match and bonus document catalogs and exclude transfer, WM26, 2025/26, and any other deprecated document from the 2026/27 live allowlists.
- [ ] Audit every selected recent/home/away row for exact `Played_At` provenance; reject collection timestamps or inferred match ordering masquerading as played dates and leave dated head-to-head rows untouched.
- [ ] Preserve historical competition partitions by default; produce an explicit dry-run inventory before deleting any remote current-scope document.
- [ ] Add trace-visible selected-document evidence and a negative test proving a stale document present in storage is not selected.
- [ ] Add freshness and missing-data tests.

## Validation

- Reconstruct representative match plus coach, relegation, and placement bonus prompts and compare facts/document sizes before and after.
- Run match-context catalog, KPI provider, bonus-command, prompt-reconstruction, and telemetry test trees.

## Complete when

- Each live team/manager fact has one authoritative document and freshness date.
- No prompt receives duplicate stale/manual and roster-derived versions of the same fact.
- A trace-backed allowlist audit accounts for every context document that can enter a live 2026/27 match or bonus prompt.
