# P0-22 — Reconstruct exact played dates in history context

- Status: Complete
- Priority: P0
- Depends on: [P0-02](p0-02-competition-scoped-storage.md), [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0025](../decisions/0025-reconstruct-bundesliga-history-played-dates.md), [ADR-0026](../decisions/0026-exclude-incomplete-history-rows.md), [ADR-0027](../decisions/0027-add-openfootball-for-second-bundesliga-history.md), [ADR-0028](../decisions/0028-capture-openligadb-second-bundesliga-history.md), [ADR-0029](../decisions/0029-capture-openligadb-dfb-pokal-final.md), [ADR-0030](../decisions/0030-use-uefa-match-record-for-europa-league-final.md), and [ADR-0031](../decisions/0031-correct-dfb-pokal-final-inventory-coverage.md)

## Outcome

Every row selected from Bundesliga `recent-history-*`, `home-history-*`, and `away-history-*` context has the exact played date and auditable provenance. Bundesliga fixtures use the competition's Kicktipp schedule/results; DFB-Pokal, UEFA, friendly, and other intervening fixtures use an accepted external source rather than an inferred Bundesliga date. Existing dated head-to-head documents remain unchanged.

## Work items

- [x] Inventory the exact live history schemas and every place that creates, stores, reconstructs, or selects recent, home, away, and head-to-head history.
- [x] Record an ADR that fixes the played-date source hierarchy, source/license requirements, canonical match identity, duplicate handling, ambiguity behavior, and last-known-good semantics. Evaluate official competition sources and the existing source-revision-pinned DuckDB data before adding a new dependency.
- [x] Generalize the proven WM26 date-map/parser/application seams without changing WM26 output contracts or treating its competition-specific map as Bundesliga data.
- [x] Check in a deterministic, source-attributed preseason map for all selected historical Bundesliga club rows that do not already carry an exact played date.
- [x] For completed 2026/27 Bundesliga fixtures, resolve the date from the exact competition-scoped Kicktipp match schedule/result stored by collection, keyed by canonical manifest identities and match identity rather than fuzzy text matching.
- [x] Resolve intervening DFB-Pokal, UEFA, friendly, and other non-Bundesliga fixtures from the accepted external source, retaining source name, URL or immutable dataset revision, verification time, and competition identity.
- [x] Preserve a valid existing `Played_At`; never substitute `Data_Collected_At`, context collection time, matchday order, or a guessed date.
- [x] Add dry-run inventory/export, apply, and strict audit modes. Ambiguous, conflicting, or unresolved selected rows must retain the last complete documents and fail the production collection gate with actionable diagnostics.
- [x] Apply the step only to `recent-history-*`, `home-history-*`, and `away-history-*`; prove `head-to-head-*` bytes and dates are not rewritten.
- [x] Expose a typed, competition-scoped collector step for P0-14 to run immediately after Kicktipp history collection; P0-18 then carries that profile step into reusable workflows.
- [x] Emit trace/console evidence for exact dates by source class, preserved dates, unresolved rows, conflicts, affected documents, and the map/source revision.
- [x] Add parser, identity, idempotency, duplicate, conflict, missing-source, last-known-good, competition-isolation, CRLF, and WM26 regression tests.

## Validation

- Reconstruct representative preseason recent/home/away documents and prove every row has an exact source-attributed `Played_At`.
- Reconstruct one completed 2026/27 Bundesliga fixture from the Kicktipp schedule and one intervening cup/UEFA fixture from the accepted external source.
- Prove ambiguous same-team/score cases fail rather than receiving a guessed date, repeated application is byte-stable, and head-to-head content is unchanged.
- Run the affected Core, context-provider, Firebase adapter, Orchestrator, and WM26 history-date test trees.
- During P0-20, run the strict audit against every live selected history document and record a zero-unresolved result before prediction validation.

## Evidence

- Read-only authenticated Kicktipp export for `ehonda-dev-buli-2627`, matchday 1: 36 documents and 288 raw selected rows; 25 incomplete future DFB-Pokal rows excluded before ordinal assignment; 263 completed rows retained.
- Frozen deterministic map: 263/263 inventory rows and 147 unique matches. Transfermarkt covers 214 rows / 116 matches, captured OpenLigaDB snapshots cover 47 rows / 30 matches, and the official UEFA match record covers 2 rows / 1 match.
- Artifact audit: exact header/order/counts, CRLF with final terminator, source revisions and hashes, source split, final identities, attribution/license boundaries, verification timestamps, constants, and secret scan all passed.
- Git checkout-filter audit: the history-map attribute resolves to `text eol=crlf`; index reconstruction produced 264 CRLF-only lines, no bare LF, the header as the first byte, and a final CRLF. The embedded-map/CRLF Core smoke class then passed 14/14 in 0.744s.
- Focused tests: Core 18/18 in 1.029s; Orchestrator history plus collect-context 42/42 in 4.667s; Firestore integration 1/1 in 18.073s; Dev dependency-resolution regression 3/3 in 2.442s.
- Full affected suites: Core 149/149 in 4.961s; ContextProviders.Kicktipp 46/46 in 2.748s; KicktippIntegration 193/193 in 10.727s; FirebaseAdapter 252/252 in 2m17.597s; Orchestrator 897/897 in 1m33.866s; Integration 4/4 in 1m33.098s.
- `dotnet build KicktippAi.slnx`: succeeded in 12.89s with 0 errors and 18 existing dependency-advisory warnings.
- No Firestore or Kicktipp write was performed. The P0-20 activation gate retains the required strict live zero-unresolved audit before prediction validation.

## Complete when

- Every selected recent/home/away history row has an exact played date, source identity, and deterministic match join.
- No collection timestamp or inferred league order is presented as a played date.
- Bundesliga and intervening non-league fixtures follow separate accepted source paths, with ambiguity failing closed and the last complete context remaining visible.
- The typed collector is ready for automatic P0-14/P0-18 profile execution, while WM26 and head-to-head behavior remain unchanged.
