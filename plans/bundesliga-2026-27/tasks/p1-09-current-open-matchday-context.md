# P1-09 — Reconcile current open matchday context

- Status: Complete
- Priority: P1
- Depends on: [P0-14](p0-14-profile-driven-collection.md), [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0034](../decisions/0034-drive-context-collection-from-competition-profiles.md), [ADR-0056](../decisions/0056-reconcile-current-open-fixtures-with-outcomes.md)

## Incident

Scheduled Bundesliga 2026/27 production-live run
[`33228910206`](https://github.com/ehonda/KicktippAi/actions/runs/33228910206), job
[`99037965621`](https://github.com/ehonda/KicktippAi/actions/runs/33228910206/job/99037965621),
failed on exact head `2c824c8c82bcc157d1db9ae94dce0b27ec87db87`
at `2026-08-29T02:26Z`. Outcome collection fetched all nine matchday-1
fixtures with one completed and eight pending. The current open Spielinfo view
then returned eight fixtures, and ADR-0034's exact-nine check rejected it.

## Outcome

Implicit current-matchday context collection accepts a reduced open fixture
set only when the complete outcome summary exactly accounts for every absent
fixture, every returned fixture belongs to that current matchday, and the exact
ordinal home/away identity set equals the pending outcome set. All other count
or identity mismatches fail before provider creation or publication; explicit
target matchdays and full-season pages remain exact.

## Work items

- [x] Preserve `GetMatchesWithHistoryAsync` as an open prediction-input view.
- [x] Pass the already-collected outcome result to current-page validation
      without changing outcome collection or persistence.
- [x] Require one current summary, an exact profile-sized outcome fetch,
      completed and pending arithmetic that exactly matches the reduced open
      view, and current-matchday identity for every returned fixture.
- [x] Carry the fetched outcome rows in the summary and require nonblank,
      ordinal-distinct `TippSpielId` values, distinct full/open fixture
      identities, and exact open-to-pending home/away identity-set equality.
- [x] Preserve exact validation for explicit targets, full-season pages, zero
      fixtures, surplus fixtures, and unexplained reductions.
- [x] Add regressions for explained 8/9 success, unexplained 8/9 failure,
      matchday-identity mismatch, and explicit-target exactness.
- [x] Record the narrowly superseded ADR-0034 clause in ADR-0056.

## Validation evidence — 2026-08-29

- The focused command tests use the nine official matchday-1 fixture pairs,
  with Bayern–VfB completed and the exact other eight pairs pending/open. They
  cover explained success; unexplained arithmetic; outcome and open matchday
  mismatch; blank/duplicate outcome IDs; duplicate/wrong-matchday outcome
  identity; duplicate/omitted open identity; and explicit-target exactness.
- Focused command:
  `dotnet run --project tests/Orchestrator.Tests --no-restore -- --treenode-filter "/*/*/(CollectContextKicktippCommand_NormalMode_Tests)|(MatchOutcomeCollectionServiceTests)/*"`
  passed 27/27 with zero failures or skips in 4.147 seconds. The output
  contained only existing package/compiler warnings.
- Full-season exact-count coverage remains in
  `CollectContextKicktippCommand_FullSeason_Tests`, including an eight-fixture
  page failing before provider, outcome, or context writes.
- Full command: `dotnet run --project tests/Orchestrator.Tests --no-restore`
  passed 1171/1171 with zero failures or skips in 2 minutes 41.370 seconds.
  The output contained only existing package/compiler warnings and the suite's
  normal slow-test notices.

## Complete when

- [x] The incident's explained eight-open-fixture current view reaches context
      collection without changing Kicktipp match prediction inputs.
- [x] An unexplained eight-fixture response still fails before provider
      creation or publication.
- [x] The reduced view cannot pass with blank/duplicate outcome IDs, duplicate
      fixture identities, or any open fixture outside the exact pending set.
- [x] Explicit target matchdays, full-season pages, and ADR-0012 outcome
      completion retain their exact-nine contracts.
