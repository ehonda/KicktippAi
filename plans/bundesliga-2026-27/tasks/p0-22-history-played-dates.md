# P0-22 — Reconstruct exact played dates in history context

- Status: Not started
- Priority: P0
- Depends on: [P0-02](p0-02-competition-scoped-storage.md), [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md) plus a source and match-identity ADR to be accepted before implementation

## Outcome

Every row selected from Bundesliga `recent-history-*`, `home-history-*`, and `away-history-*` context has the exact played date and auditable provenance. Bundesliga fixtures use the competition's Kicktipp schedule/results; DFB-Pokal, UEFA, friendly, and other intervening fixtures use an accepted external source rather than an inferred Bundesliga date. Existing dated head-to-head documents remain unchanged.

## Work items

- [ ] Inventory the exact live history schemas and every place that creates, stores, reconstructs, or selects recent, home, away, and head-to-head history.
- [ ] Record an ADR that fixes the played-date source hierarchy, source/license requirements, canonical match identity, duplicate handling, ambiguity behavior, and last-known-good semantics. Evaluate official competition sources and the existing source-revision-pinned DuckDB data before adding a new dependency.
- [ ] Generalize the proven WM26 date-map/parser/application seams without changing WM26 output contracts or treating its competition-specific map as Bundesliga data.
- [ ] Check in a deterministic, source-attributed preseason map for all selected historical Bundesliga club rows that do not already carry an exact played date.
- [ ] For completed 2026/27 Bundesliga fixtures, resolve the date from the exact competition-scoped Kicktipp match schedule/result stored by collection, keyed by canonical manifest identities and match identity rather than fuzzy text matching.
- [ ] Resolve intervening DFB-Pokal, UEFA, friendly, and other non-Bundesliga fixtures from the accepted external source, retaining source name, URL or immutable dataset revision, verification time, and competition identity.
- [ ] Preserve a valid existing `Played_At`; never substitute `Data_Collected_At`, context collection time, matchday order, or a guessed date.
- [ ] Add dry-run inventory/export, apply, and strict audit modes. Ambiguous, conflicting, or unresolved selected rows must retain the last complete documents and fail the production collection gate with actionable diagnostics.
- [ ] Apply the step only to `recent-history-*`, `home-history-*`, and `away-history-*`; prove `head-to-head-*` bytes and dates are not rewritten.
- [ ] Expose a typed, competition-scoped collector step for P0-14 to run immediately after Kicktipp history collection; P0-18 then carries that profile step into reusable workflows.
- [ ] Emit trace/console evidence for exact dates by source class, preserved dates, unresolved rows, conflicts, affected documents, and the map/source revision.
- [ ] Add parser, identity, idempotency, duplicate, conflict, missing-source, last-known-good, competition-isolation, CRLF, and WM26 regression tests.

## Validation

- Reconstruct representative preseason recent/home/away documents and prove every row has an exact source-attributed `Played_At`.
- Reconstruct one completed 2026/27 Bundesliga fixture from the Kicktipp schedule and one intervening cup/UEFA fixture from the accepted external source.
- Prove ambiguous same-team/score cases fail rather than receiving a guessed date, repeated application is byte-stable, and head-to-head content is unchanged.
- Run the affected Core, context-provider, Firebase adapter, Orchestrator, and WM26 history-date test trees.
- During P0-20, run the strict audit against every live selected history document and record a zero-unresolved result before prediction validation.

## Complete when

- Every selected recent/home/away history row has an exact played date, source identity, and deterministic match join.
- No collection timestamp or inferred league order is presented as a played date.
- Bundesliga and intervening non-league fixtures follow separate accepted source paths, with ambiguity failing closed and the last complete context remaining visible.
- The typed collector is ready for automatic P0-14/P0-18 profile execution, while WM26 and head-to-head behavior remain unchanged.
