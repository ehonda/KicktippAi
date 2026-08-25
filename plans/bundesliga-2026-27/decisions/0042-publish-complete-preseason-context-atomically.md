# ADR-0042: Publish complete preseason Kicktipp context atomically

- Status: Accepted
- Date: 2026-08-25

## Context

P0-20's first non-dry Bundesliga development collection used the profile-owned
current-matchday path. The command succeeded and correctly published the
current matchday's candidate scope, but the subsequent strict P0-15 inventory
reported only 86 of 401 expected documents: 315 were missing, consisting of
297 ordered head-to-head documents and 18 selected home/away history
documents. The present CSV subset was valid and correctly scoped, so this was
a collection-scope mismatch rather than a weakened hygiene catalog or corrupt
publication.

ADR-0032 requires all 54 selected recent/home/away documents before activation,
while the strict season catalog requires one ordered head-to-head document for
each of the 306 fixtures. ADR-0034 supplied typed profile totals (`306` fixtures
and `9` per matchday), but the collector used only the per-matchday count and
did not offer a profile-owned complete-season collection contract. Passing an
ad hoc list to `--matchdays` could enumerate more pages, but did not prove the
exact 34-page schedule, the exact strict catalog, or one atomic publication.

## Decision

Add an explicit `--full-season` collection mode to `collect-context-dev` and
`collect-context-profile`. The raw Kicktipp subcommand exposes the flag only as
an implementation seam and rejects it without typed profile counts. The mode
is supported only for `bundesliga-2026-27`, cannot be combined with explicit
`--matchdays` or `--match-outcomes-only`, and is rejected for WM26 or any future
profile that has not accepted an equivalent contract.

The number of pages is derived from the typed profile's exact season and
per-matchday fixture counts: `306 / 9 = 34`. Full-season collection performs
the following operations serially and in this order:

1. Fetch matchdays 1 through 34 without constructing a context provider.
2. Require exactly nine distinct ordered fixtures on each page, the requested
   matchday identity on every fixture, and each of the 18 manifest clubs
   exactly once per matchday.
3. Require exactly 306 distinct ordered manifest pairs across the season and
   exact equality with the strict 306-name head-to-head catalog.
4. Construct providers only after every fixture page passes, enumerate them
   serially, reject conflicting bytes for a repeated document name, and require
   exact equality with the 362-document Kicktipp-owned strict subset: standings,
   the exact community rules document, 54 selected histories, and 306 H2H
   documents. Unexpected, WM26, unscoped, missing, or case-variant names fail.
5. Refresh current match outcomes only after that complete remote candidate set
   passes, then run the existing strict played-date collector with the exact 54
   selected names and all 430 frozen map occurrences. Any unresolved,
   ambiguous, missing, unexpected, or incomplete frozen-map application fails.
6. Recheck the exact 362-name set after transformation and submit the
   deterministic ordinal-name-ordered writes in one call to
   `SaveContextDocumentsAtomicallyAsync`.

There is no individual-save fallback in full-season mode. A page, provider,
catalog, history, repository-validation, cancellation, or transaction failure
publishes no context candidate from the run. The existing repository reads and
validates every ordinary-document identity before staging creates, so its
transaction rollback remains the last-complete-set boundary. A Docker Firestore
test exercises the exact 362-document create and no-op batch, in addition to
the existing later-document corruption rollback test. The implementation also
remains subject to Firestore's documented 10 MiB request and 270-second
transaction limits: <https://firebase.google.com/docs/firestore/quotas#writes_and_transactions>.

The ordinary current/explicit-matchday path retains its established behavior.
The competition-profile runner still stops immediately when Kicktipp fails;
Club Elo and roster commands are not constructed afterward. Collection does
not resolve or construct prediction/model services, and no prediction command
can begin through this runner.

## Alternatives considered

- **Weaken the strict catalog to the current matchday's 47 Kicktipp documents:**
  Rejected because activation would still lack 315 exact prompt identities.
- **Pass `1,2,...,34` through `--matchdays`:** Rejected because a caller string
  does not bind the typed 306/9 contract, prove every ordered fixture exactly
  once, or select the full atomic publication path.
- **Publish each matchday or document as it succeeds:** Rejected because a late
  provider or Firestore failure would expose a mixed incomplete season set.
- **Enable the mode generically for WM26:** Rejected because WM26 has a variable
  matchday shape and a different document catalog; its accepted path remains
  unchanged.

## Consequences

- P0-20 has one reproducible preseason command capable of producing the exact
  strict Kicktipp subset without changing the expected-set validator.
- Complete schedule acquisition is more expensive and intentionally serialized,
  but it is bounded to the 34 typed Bundesliga pages and runs only when the
  explicit flag is supplied.
- A repeated document with differing provider bytes now blocks full-season
  publication instead of making the result depend on first-observed order.
- Live collection must remain paused until this decision and implementation are
  independently reviewed, integrated, pushed, and exact-head CI is green.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0034 only where its current/explicit-matchday fixture gate was sufficient
for complete preseason publication. ADR-0032's exact history, source, ordering,
and atomic last-complete-set contracts remain in force.
