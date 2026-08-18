# P0-09 — Implement roster enrichment and collection

- Status: Complete
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md), [P0-08](p0-08-roster-membership-seed.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0014](../decisions/0014-share-atomic-context-kpi-publication.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md)

## Outcome

A Bundesliga collector selects quality-gated current-season DuckDB membership per club, otherwise preserves fallback or last-known-good membership, enriches safely, and atomically publishes `roster-*`, `team-rosters`, and `team-squad-summary` documents.

## Progress evidence

- 2026-08-18: The shared ADR-0014 Firestore boundary passed its focused publication emulator suite and the default Firebase adapter suite. Roster collection, source selection, enrichment, and CLI work remain unimplemented.
- 2026-08-18: Added the local-only `collect-context rosters` path, strict ADR-0017/0018 DuckDB schema and canonical metadata boundary, ID-only selected-date enrichment, headed last-known-good reconstruction, deterministic 20-document construction, and atomic roster publication. The task remains In progress pending independent review, commit, and CI.

## Work items

- [x] Extract reusable seed/manifest, DuckDB enrichment, CSV rendering, quality-report, and last-known-good mechanics from the WM26 lineup implementation without renaming the historical WM26 contract.
- [x] Add a Bundesliga roster source and CLI command with explicit seed, manifest, DuckDB path, community context, and competition inputs.
- [x] Select DuckDB membership automatically only when the club explicitly represents 2026/27 and passes every P0-07 gate; never infer current membership from transfer events alone.
- [x] Fall back per club to the source-dated seed or last-known-good snapshot when DuckDB is missing, stale, partial, or suspicious.
- [x] Join enrichment only by stable identifiers or explicitly reviewed mappings.
- [x] Calculate age, position, latest market value, and summary aggregates according to P0-07.
- [x] Publish all per-team and aggregate documents to `bundesliga-2026-27` only after every required quality gate succeeds.
- [x] Preserve the previous complete version when source selection, enrichment, or upstream refresh fails.
- [x] Emit actionable unmatched-member and coverage diagnostics in dry-run and normal modes.
- [x] Add source, command, CSV, quality-gate, last-known-good, and Firestore upload tests.

## Validation

- Run the new roster test tree plus unchanged `CollectContextLineupsCommandTests` and `Wm26LineupSourceTests`.
- Dry-run valid DuckDB takeover, fallback, last-known-good, and deliberately incomplete fixtures.

## Validation evidence

- 2026-08-18: focused Core `BundesligaRoster*` tree passed 26/26; it covers roster schemas/rendering, exact quality gates, deterministic 20-document construction, and strict headed reconstruction/corruption rejection.
- 2026-08-18: focused `BundesligaRosterSourceTests` passed 2/2; it covers an eligible local DuckDB per-club takeover and initial complete-seed retention on missing schema.
- 2026-08-18: focused source plus `CollectContextRostersCommandTests` passed 5/5; it covers explicit scope, dry-run/no-write, canonical 20-document publication, and incomplete DuckDB provenance rejection.
- 2026-08-18: unchanged `CollectContextLineupsCommandTests` plus `Wm26LineupSourceTests` passed 15/15.
- 2026-08-18: focused `FirebaseDocumentPublicationRepositoryTests` emulator tree passed 20/20, covering canonical roster definition reservation, initial/unchanged/changed/reactivated snapshot lifecycle, exact LKG reads, CAS, and corruption rejection.
- 2026-08-18: review-correction focused checks passed: strict metadata reconstruction 3/3 and roster source plus command 6/6, including no-publish retained-LKG disposition.
- 2026-08-18: Superseded validation evidence: the earlier remediation counts predate ADR-0019's shared truth boundary and corrected membership/enrichment failure distinction. P0-09 remains In progress pending replacement evidence, review, and scoped commit.
- 2026-08-18: Final independent review accepted the collector checkpoint. Focused source matrix 34/34 covers per-club raw gates; source retention on membership/enrichment failure; enrichment for selected DuckDB, fallback, and LKG membership at each selected date; unmatched stable-ID diagnostics; and delimiter-safe DuckDB paths. Core roster contract 30/30 covers shared Build/Reconstruct truth validation and direct LKG provenance mutations. Full serial suites passed Core 116/116, Orchestrator 875/875, and Firebase adapter 240/240.

## Complete when

- All 18 roster documents and both aggregates are deterministic and complete.
- Missing enrichment yields `N/A` and a report; invalid DuckDB retains fallback/last-known-good membership; an incomplete 18-club result blocks publication.
- WM26 lineup behavior still passes its existing tests.
