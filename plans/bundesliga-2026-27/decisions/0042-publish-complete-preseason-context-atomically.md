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

The first authorized `--full-season` attempt then proved all 34 pages and all
306 ordered fixtures, but failed closed while collecting the first matchday-2
fixture. `recent-history-vfb.csv` was produced from the matchday-1 away fixture
and again from the matchday-2 home fixture with different bytes. This is valid
provider behavior: Kicktipp's recent-history source is fixture-date and
home/away-role sensitive even though the stored identity is global. Across a
season, collision-driven enumeration would request each recent identity 34
times and each home/away identity 17 times. Selecting the first or last
collision would therefore make the frozen 54-document inventory incidental.
The attempt reached neither outcome refresh nor the atomic context save, and
no model or prediction operation followed.

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
4. Construct matchday-scoped providers only after every fixture page passes.
   Collect standings and the exact community rules document once. Select the
   canonical 54 global history sources explicitly: every recent document comes
   from its team's matchday-1 fixture, while each home/away document comes from
   that team's earliest scheduled fixture in the corresponding role. The
   accepted ADR-0032/ADR-0041 inventory requires each of those deterministic
   source fixtures to be in matchday 1 or 2; a later selector fails closed.
   Unselected per-fixture variants are never requested or silently collapsed.
5. Collect each of the 306 H2H documents separately through its fixture's exact
   matchday page, never through the current `tippabgabe` page. Require exact
   equality with the 362-document Kicktipp-owned strict subset: standings,
   rules, 54 selected histories, and 306 H2Hs. A duplicate inside either
   semantic phase reports only the document name, UTF-8 byte counts, and
   SHA-256 hashes; conflicting content and unexpected, WM26, unscoped,
   missing, or case-variant names fail.
6. Refresh current match outcomes only after that complete remote candidate set
   passes, then run the existing strict played-date collector with the exact 54
   selected names, all 430 frozen completed-map occurrences, and the accepted
   exact two excluded incomplete rows. Any unresolved, ambiguous, missing,
   unexpected, or count-mismatched frozen-map application fails.
7. Recheck the exact 362-name set after transformation and submit the
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
- Globally named selected histories now have explicit canonical fixture sources;
  their other valid fixture-scoped variants cannot affect publication order.
- A duplicate within the canonical-history or ordered-H2H phase still blocks
  publication with content-redacted byte/hash diagnostics.
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
