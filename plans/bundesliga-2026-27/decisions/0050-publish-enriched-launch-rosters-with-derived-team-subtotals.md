# ADR-0050: Publish enriched launch rosters with derived team subtotals

- Status: Accepted
- Date: 2026-08-26

## Context

The initial Bundesliga 2026/27 dev and arena roster publications were built by
the competition profile without an explicit local DuckDB artifact. That is the
safe generic profile behavior established by ADR-0017, but it means the headed
fallback seed supplied membership and stable IDs while every supplemental age,
position, and market value remained `N/A`. The local audited
`transfermarkt-datasets.duckdb` artifact can enrich the seed's 464 stable player
IDs without becoming an unattended runtime dependency. Its exact SHA-256 is
`808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c`,
its upstream revision is
`154367dfa6d6eb0b86332e332f9df0a080c7ddce`, and its snapshot date is
`2026-08-13`. A read-only audit produced 464 known ages, 464 known canonical
positions, and 450 positive market values.

The prompt-facing roster CSV also has no single row carrying the team total.
The squad-summary KPI already exposes a total of known positive player values,
but the per-team roster is the direct match context. Adding a derived row
changes canonical bytes, so new publication construction needs a versioned
contract while already headed v1 snapshots must remain reconstructable.

## Decision

New roster publications use metadata contract
`bundesliga-roster-publication/v2`. Each per-team roster appends exactly one
derived row after the coach and all players, and the aggregate roster appends
the same row after each team's players:

```text
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
<team>,<membership-date>,Team Accumulated,N/A,N/A,N/A,<subtotal-or-N/A>
```

`Team Accumulated` is a document role, not a roster-membership enum value. Its
market value is the sum of positive known player values only, using the existing
dot-thousands representation. It is a known-value subtotal when coverage is
partial and is `N/A` when no player has a known value. Unknown is never encoded
as zero. `team-squad-summary` remains the explicit coverage companion: its
`Valued_Player_Count`, squad size, total, and median show whether a subtotal is
partial. The derived row is excluded from player, coach, stable-ID, age,
position, and valuation coverage counts.

Strict reconstruction dispatches by the exact headed metadata contract. v1
keeps its historical canonical bytes and forbids the derived row. v2 requires
exactly one derived row in the final position for each team, requires `N/A` in
Name/Age/Position, parses only positive money or `N/A`, recomputes the subtotal,
and rejects a missing, duplicate, misplaced, malformed, or incorrect row. The
aggregate must exactly reproduce the corresponding per-team documents.

The initial enriched launch publication is an explicit operator action. It must
pass the exact local DuckDB SHA-256 pin and fail before publication unless the
complete 18-club snapshot has at least 464 known ages, 464 known positions, and
450 valued players. Those audited counts are regression floors, not claims that
the remaining 70 players without stable IDs have known supplemental data. A
successful enriched publication becomes the headed last-known-good snapshot.
Later ordinary profile collection without DuckDB continues selecting that
same-date last-known-good membership and preserves its enrichment while
rendering current v2 documents.

The machine-local path is deliberately absent from generic CI and reusable
context workflows. P1-05 retains ownership of acquiring/refreshing DuckDB,
detecting roster diffs, and automating current-season adoption. This decision
adds only the one-time P0 launch gate and v2 document contract.

## Alternatives considered

- **Put zero in an unknown team total:** Rejected because zero is data and would
  misrepresent unavailable valuations.
- **Sum only when every player is valued:** Rejected because useful known values
  would disappear; the companion coverage columns make a partial subtotal
  explicit.
- **Add `TeamAccumulated` to `BundesligaRosterRole`:** Rejected because the row
  is derived presentation data, not membership.
- **Change v1 reconstruction to expect the new row:** Rejected because a headed
  immutable historical snapshot must retain its original canonical bytes.
- **Add the audited local path to the generic workflow:** Rejected because the
  artifact is machine-local and recurring acquisition/refresh belongs to P1-05.

## Consequences

- Fresh v2 roster prompts contain one deterministic team subtotal per club.
- Historical v1 last-known-good snapshots remain valid and strict.
- The explicit launch command fails closed on wrong artifact bytes or audited
  coverage regression before a Firestore publication attempt.
- A later no-DuckDB profile run cannot erase the successful launch enrichment.
- P0-21 must publish and inspect enriched v2 rosters before initial production
  predictions; P1-05 remains the only automation task.

## Affected tasks

- [P0-25](../tasks/p0-25-roster-enrichment-and-team-total.md)
- [P0-21](../tasks/p0-21-production-activation.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

This decision supersedes the prompt-rendering and new-publication version
portions of ADR-0011, ADR-0017, ADR-0018, and ADR-0019. Their atomicity,
selection, provenance, semantic-truth, and last-known-good requirements remain
in force.
