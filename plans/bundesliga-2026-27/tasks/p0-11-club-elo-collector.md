# P0-11 — Implement Club Elo collection

- Status: Not started
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md), [P0-10](p0-10-club-elo-source.md)
- Decisions: [ADR-0002](../decisions/0002-supersede-transfer-documents.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md)

## Outcome

A collector publishes one source-dated `club-elo-{slug}.csv` per team and one complete `club-elo-rankings` KPI document.

## Work items

- [ ] Add an interface-backed rating source beside the FIFA ranking source so seed, cache, and any later network parser can be fixture-tested.
- [ ] Map source names only through the P0-04 manifest.
- [ ] Calculate deterministic global and Bundesliga rank order, with an explicit tie rule.
- [ ] Render `Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At` per team and a documented aggregate schema.
- [ ] Reject missing, duplicate, non-numeric, stale, or fewer-than-18 mapped rows.
- [ ] Publish atomically to the explicit 2026/27 partition and preserve the complete seed/last-known-good snapshot on any partial or stale refresh.
- [ ] Refuse unattended network use unless the late reuse/terms gate is accepted; surface seed/cache age in output and traces.
- [ ] Expose dry-run diagnostics, source date, collection time, mapping coverage, and publication disposition.
- [ ] Add provider, command, CSV, upload, and last-known-good tests.

## Validation

- Run the new Club Elo test tree and unchanged FIFA ranking tests.
- Dry-run a complete fixture and fixtures with one missing club, duplicate aliases, and stale data.

## Complete when

- All 18 per-team documents and the aggregate share one `Rated_At` snapshot.
- A partial response cannot overwrite the last complete version.
- Generated CSV satisfies repository rendering rules.
