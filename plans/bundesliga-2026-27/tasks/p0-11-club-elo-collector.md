# P0-11 — Implement Club Elo collection

- Status: Complete
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md), [P0-10](p0-10-club-elo-source.md)
- Decisions: [ADR-0002](../decisions/0002-supersede-transfer-documents.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0014](../decisions/0014-share-atomic-context-kpi-publication.md), [ADR-0015](../decisions/0015-club-elo-prompt-publication-contract.md), [ADR-0016](../decisions/0016-validate-club-elo-publication-metadata.md)

## Outcome

A collector publishes one source-dated `club-elo-{slug}.csv` per team and one complete `club-elo-rankings` KPI document.

## Progress evidence

- 2026-08-18: The shared ADR-0014 Firestore boundary passed its focused publication emulator suite and the default Firebase adapter suite before Club Elo collector integration.
- 2026-08-18: ADR-0015 fixed the exact five-column CSV, deterministic Elo tie ordering, strict byte rendering, and headed LKG reconstruction metadata. The seed-only `collect-context club-elo` command uses the canonical ADR-0014 definition and never enables a network source.
- 2026-08-18: ADR-0016 strengthened metadata reconstruction to exact enum/property/diagnostic semantics. Build uses the same semantic/provenance validation, so it cannot emit metadata that reconstruction rejects. Headed LKG reconstruction exact-compares every context CSV and the aggregate; valid NetworkDisabled metadata lexical leading-zero, LF, and aggregate-order tampering fail with their canonical CSV reasons.
- 2026-08-18: Focused review validation passed: Core Club Elo tree 16/16; command unit tree 8/8 (activity tags, explicit scope guards, custom missing/duplicate/non-numeric seed rejection, usable old-seed age); and a real Firestore-emulator command lifecycle 1/1 (initial published, unchanged, valid changed seed, dry-run no head movement, isolation, corrupt payload fail-closed). Firebase publication emulator tree remains 20/20.
- 2026-08-18: Broader affected suites passed serially: Core 110/110, Orchestrator 834/834, Firebase adapter emulator 240/240. Existing `SSH.NET` advisory warnings remain unrelated.

## Work items

- [x] Add an interface-backed rating source beside the FIFA ranking source so seed, cache, and any later network parser can be fixture-tested.
- [x] Map source names only through the P0-04 manifest.
- [x] Calculate deterministic global and Bundesliga rank order, with an explicit tie rule.
- [x] Render `Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At` per team and a documented aggregate schema.
- [x] Reject missing, duplicate, non-numeric, or fewer-than-18 mapped rows; reject stale network candidates under ADR-0013 while retaining a complete old seed/LKG with visible age.
- [x] Publish atomically to the explicit 2026/27 partition and preserve the complete seed/last-known-good snapshot on any partial or stale refresh.
- [x] Refuse unattended network use unless the late reuse/terms gate is accepted; surface seed/cache age in output and traces.
- [x] Expose dry-run diagnostics, source date, collection time, mapping coverage, and publication disposition.
- [x] Add provider, command, CSV, upload, and last-known-good tests.

## Validation

- Run the new Club Elo test tree and unchanged FIFA ranking tests.
- Dry-run a complete fixture and fixtures with one missing club, duplicate aliases, non-numeric data, and an old complete launch seed. Network-candidate staleness remains Core policy evidence because this CLI has no network path.

## Complete when

- All 18 per-team documents and the aggregate share one `Rated_At` snapshot.
- A partial response cannot overwrite the last complete version.
- Generated CSV satisfies repository rendering rules.
