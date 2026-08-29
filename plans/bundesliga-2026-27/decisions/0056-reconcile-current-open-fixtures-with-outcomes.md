# ADR-0056: Reconcile current open fixtures with outcomes

- Status: Accepted
- Date: 2026-08-29
- Accepted by: Project Owner on 2026-08-29

## Context

The first fixture of Bundesliga 2026/27 matchday 1 completed before scheduled
production-live run
[`33228910206`](https://github.com/ehonda/KicktippAi/actions/runs/33228910206).
Outcome collection in job
[`99037965621`](https://github.com/ehonda/KicktippAi/actions/runs/33228910206/job/99037965621)
fetched all nine fixtures and classified one completed plus eight pending, but
Kicktipp's current Spielinfo/prediction-input view then returned only the eight
open fixtures. ADR-0034's unconditional exact-nine current-view check rejected
that valid open set before context-provider creation and publication.

The Spielinfo view is intentionally an open prediction-input view. Adding
played matches to it would change match-prediction behavior and duplicate the
authoritative outcome view. ADR-0012 must also continue to require nine
distinct completed outcomes before a Bundesliga matchday is complete.

## Decision

Keep exact profile-count validation as the default. A reduced fixture count is
accepted only for an implicit current-matchday context collection when all of
these conditions hold:

- the already-completed outcome collection contains exactly one summary for
  its reported current matchday;
- that summary carries a read-only copy of exactly the profile's expected
  fetched outcomes, all identifying the reported current matchday;
- every outcome has a nonblank, ordinal-distinct `TippSpielId`, and every
  outcome has a distinct exact home-team/away-team fixture identity;
- its completed count exactly explains the fixtures absent from the open view;
- its pending count equals the returned open-fixture count; and
- every returned fixture identifies that reported current matchday, has a
  distinct exact home-team/away-team identity, and the returned identity set
  equals the pending outcome identity set exactly.

Fixture identity compares the home and away team strings with ordinal
semantics. It deliberately excludes `StartsAt`: cancelled-match Spielinfo
parsing can inherit a neighboring kickoff, so time is not stable enough for
cross-view reconciliation. Matchday identity is validated separately.

An exact expected count continues to succeed without reconciliation. Zero
fixtures, surplus fixtures, a missing or duplicate current summary, a partial
outcome fetch, blank or duplicate outcome IDs, duplicate or mismatched fixture
identities, inconsistent completed or pending counts, and an outcome/open-view
matchday mismatch fail closed before context-provider creation or publication.
Explicit target matchdays and full-season pages continue to require the exact
profile count and cannot use the reconciliation exception.

This decision changes only current context-input validation. It does not alter
the Kicktipp client, matchday prediction input semantics, outcome persistence,
or ADR-0012's nine-outcome completion policy.

## Alternatives considered

- **Return played fixtures from the Spielinfo client:** Rejected because that
  client intentionally represents open prediction inputs and is also consumed
  by matchday prediction commands.
- **Accept any current count from zero through nine:** Rejected because an
  incomplete scrape or changed page could silently publish partial context.
- **Use only the completed count without validating the outcome fetch:**
  Rejected because a partial outcome response cannot prove the complete
  matchday membership.
- **Relax explicit and full-season pages too:** Rejected because those views
  request stable fixture membership and have no open-view justification for a
  reduced count.

## Consequences

- Scheduled context collection can proceed after one or more fixtures have
  completed when the complete outcome view exactly accounts for the reduced
  open view.
- Two independently fetched views must agree arithmetically and on current
  matchday and exact pending-fixture identity, preserving fail-closed behavior
  when either view is incomplete, duplicated, or inconsistent.
- Played fixtures are not reintroduced into prediction inputs, and completed
  matchdays still require all nine persisted outcomes.

## Affected tasks

- [P1-09](../tasks/p1-09-current-open-matchday-context.md)
- [P0-14](../tasks/p0-14-profile-driven-collection.md)

## Supersedes

- [ADR-0034](0034-drive-context-collection-from-competition-profiles.md), only
  its requirement that an implicit current Bundesliga matchday view itself
  contain exactly nine fixtures. Its exact explicit-target/full-season count,
  profile ownership, collector order, and publication contracts remain in
  force.
