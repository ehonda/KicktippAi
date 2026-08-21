# ADR-0027: Add a fixed CC0 source for second-division history

- Status: Accepted
- Date: 2026-08-21

## Context

The revision-pinned `transfermarkt-datasets` artifact accepted in ADR-0025 contains Bundesliga, DFB-Pokal, and UEFA games relevant to the preseason inventory, but it does not contain 2. Bundesliga. The exact Kicktipp inventory has 40 completed `2.BL` rows for promoted clubs, so that source cannot complete the map.

The official DFB Data Center publishes the exact 2025/26 2. Bundesliga schedule and individual match pages. Its general terms, however, limit protected portal and database content to private use, require permission for other reuse, prohibit automated scraping, and reserve text/data-mining rights. It is suitable for manual factual corroboration, not ingestion or redistribution in this repository without separate permission.

The `openfootball/deutschland` repository explicitly publishes German league data, including 2. Bundesliga, under CC0 1.0. At commit `eb6acee25966a32f3ee099c5b774107c250f71e9`, `2025-26/2-bundesliga2.txt` contains all 306 league matches with dates, teams, and full-time scores.

## Decision

Keep ADR-0025's source hierarchy and fixed `transfermarkt-datasets` revision for every covered preseason row. Use `openfootball/deutschland` only for completed `2.BL` rows that the accepted DuckDB artifact cannot cover, pinned to:

- repository commit `eb6acee25966a32f3ee099c5b774107c250f71e9`;
- data file `https://github.com/openfootball/deutschland/blob/eb6acee25966a32f3ee099c5b774107c250f71e9/2025-26/2-bundesliga2.txt`; and
- license `https://github.com/openfootball/deutschland/blob/eb6acee25966a32f3ee099c5b774107c250f71e9/LICENSE.md`.

Each `2.BL` map entry records source class `revision-pinned-dataset`, source name `openfootball/deutschland`, that commit, the immutable file URL, and a deterministic source match identity containing season, date, home team, and away team. Runtime does not fetch either repository.

The checked map must join the exact Kicktipp document row and independently match the fixed openfootball date, home team, away team, and full-time score. Official DFB schedule and match pages are manually checked as corroboration. DFB pages are never crawled, scraped, or copied into the seed. A conflict between openfootball, DFB corroboration, another map entry, or Kicktipp evidence fails closed and requires review; source priority never resolves conflicting facts silently.

## Alternatives considered

- **Use the DFB Data Center as the ingested source:** Rejected because the published terms do not grant the needed automated extraction or repository-reuse rights.
- **Use OpenLigaDB:** Not chosen because its ODbL attribution/share-alike obligations add avoidable complexity when a complete CC0 fixed source exists.
- **Treat 2. Bundesliga as `L1` in DuckDB:** Rejected because that would invent competition identity and attach unrelated matches.
- **Drop completed promoted-club history:** Rejected because those results are selected recent/home/away evidence and have a lawful exact source.

## Consequences

- The deterministic seed has two explicit CC0 sources with non-overlapping normal scope.
- Source validation enforces that `2.BL` uses the fixed openfootball revision and other rows use the fixed DuckDB revision.
- DFB remains useful corroboration without becoming a runtime dependency or unlawful ingestion path.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0025 only where it required the initial DuckDB revision as the sole fixed source for every non-Bundesliga row. Its source hierarchy, exact identity, ambiguity, last-known-good, and head-to-head decisions otherwise remain in force.
