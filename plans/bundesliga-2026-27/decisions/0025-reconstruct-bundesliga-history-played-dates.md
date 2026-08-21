# ADR-0025: Reconstruct Bundesliga history played dates from fixed sources

- Status: Accepted
- Date: 2026-08-21

## Context

Kicktipp's recent, home, and away history tables identify the competition, teams, score, and annotation, but do not include the played date. The existing collector therefore writes `Data_Collected_At`, which describes when context was collected and must not be presented to a model as the match date. Head-to-head history has a separate parser and already contains its played timestamp.

Bundesliga 2026/27 league results can be joined to the exact, competition-scoped schedule and results already collected from Kicktipp. Older and intervening DFB-Pokal, UEFA, friendly, and other fixtures need a lawful, reproducible source. The locally audited `transfermarkt-datasets` DuckDB artifact identifies itself as upstream commit `154367dfa6d6eb0b86332e332f9df0a080c7ddce`; its `games` relation provides a calendar `date`, competition ID, home and away club IDs, score, source match ID, and source URL. The upstream project publishes that revision under CC0 1.0. Official DFB, Bundesliga, and UEFA pages corroborate source semantics, but they do not form an unattended runtime dependency.

History rows have no durable external match ID. The same teams and score can recur, so position, matchday order, a fuzzy club name, or a queue of duplicate keys would silently manufacture an identity.

## Decision

The Bundesliga history played-date collector is competition-scoped to `bundesliga-2026-27` and applies only to `recent-history-*`, `home-history-*`, and `away-history-*` documents owned by an exact slug in the season team manifest. It passes every other document, including `head-to-head-*`, through byte-for-byte.

The source hierarchy is:

1. Preserve an existing `Played_At` only when it is an exact ISO calendar date (`yyyy-MM-dd`) or offset timestamp. `Data_Collected_At` is never a played date.
2. Resolve completed 2026/27 Bundesliga fixtures from competition-scoped persisted Kicktipp match outcomes. Join exact, case-sensitive manifest team names, home/away direction, and normalized score. Convert `StartsAt` to the Europe/Berlin calendar date. An unavailable or incomplete outcome is not evidence.
3. Resolve every other row from the checked-in preseason map under `data/bundesliga-2026-27/history/`. Each entry records the exact document, row identity, ISO played date, source class/name/URL, immutable source revision, source match ID, and verification time. The initial map is derived only from the fixed DuckDB revision `154367dfa6d6eb0b86332e332f9df0a080c7ddce`. Runtime never downloads, scrapes, or floats to a newer dataset.

The canonical row identity is the ordinal, case-sensitive tuple `(document name, history competition, home team, away team, normalized score, annotation)`. Document names must use a supported history prefix and an exact manifest slug. A map entry and a current-season Kicktipp outcome must each resolve at most one row. Repeated indistinguishable row tuples, conflicting evidence, duplicate map identities, a mismatched ordinal, unknown teams, invalid dates, incomplete provenance, or any unresolved selected row fail closed with actionable diagnostics. Runtime does not fuzzy-match provider names, infer dates from order, or consume duplicate entries as a queue.

Collection is a two-phase operation: parse and audit the complete selected document set in memory, then publish only when every selected row resolves. Audit and dry-run never write. A failed batch leaves the last complete stored documents unchanged. Reapplying an already correct map is byte-stable, and generated prompt CSV uses deterministic CRLF with the exact header `Competition,Played_At,Home_Team,Away_Team,Score,Annotation` and a final line terminator.

The fixed map is both the launch seed and last-known-good external evidence. Updating non-Bundesliga history requires generating a new inventory, reviewing it against a new explicitly identified lawful source revision, and changing the checked-in map and provenance together. Official competition sites may be cited as human corroboration; they are not silently substituted when the fixed source lacks or ambiguously identifies a match.

## Alternatives considered

- **Use collection time or list order:** Rejected because neither establishes when a match was played.
- **Use fuzzy names or consume duplicate keys in encounter order:** Rejected because repeated fixtures and scorelines make the join non-deterministic.
- **Fetch Transfermarkt or official pages at runtime:** Rejected because launch needs reproducible source and license evidence, controlled network behavior, and a last-known-good path.
- **Add dates to head-to-head again:** Rejected because that schema already carries played timestamps and must remain byte-stable.
- **Treat the WM26 date map as Bundesliga data:** Rejected because its identity, preservation, and prediction-date rules are competition-specific. Shared CSV mechanics may be reused without changing WM26 contracts.

## Consequences

- Current Bundesliga results advance automatically from stored Kicktipp evidence, while external history advances only through reviewed map updates.
- Exact duplicates and provider drift stop publication rather than guessing; operators receive document and row diagnostics.
- Source attribution remains reconstructable outside the prompt payload through the checked-in map and collector report.
- P0-14 can compose the typed collector immediately after raw Kicktipp history collection without coupling Core to Firebase or the Kicktipp client.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-18](../tasks/p0-18-base-workflow-support.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

None.
