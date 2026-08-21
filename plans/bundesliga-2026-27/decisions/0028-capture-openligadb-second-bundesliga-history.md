# ADR-0028: Capture OpenLigaDB for second-division history

- Status: Accepted
- Date: 2026-08-21

## Context

ADR-0027 selected a CC0 openfootball file for the 2. Bundesliga gap. Verification after acceptance found that the pinned file contains completed results only for an early portion of the season. Later rounds are fixture placeholders, and multiple matches share a round-level default date rather than an exact match date. It cannot establish most of the 40 required played dates and must not be used for them.

OpenLigaDB's public `bl2/2025` response contains all 306 2025/26 2. Bundesliga matches as completed, with stable match IDs, exact timestamps, teams, and full-time result type 2. Its `rel/2025` response contains both 2025/26 Bundesliga relegation legs between VfL Wolfsburg and SC Paderborn 07. OpenLigaDB explicitly offers its API for automated sports-result use and licenses the API data under the Open Database License (ODbL) 1.0.

## Decision

Replace ADR-0027's openfootball source with one-time captures of:

- `https://api.openligadb.de/getmatchdata/bl2/2025`; and
- `https://api.openligadb.de/getmatchdata/rel/2025`.

Store the raw responses under `data/bundesliga-2026-27/history/sources/`, record each byte-level SHA-256 as its immutable source revision, and never fetch OpenLigaDB at runtime. Before freezing the source, validation requires exactly 306 league matches, all 306 marked completed, exactly one full-time result (`resultTypeID == 2`) per match, and both completed relegation legs with exact teams and full-time results. The seed builder/report joins exact target document identity, selected completed-row ordinal, home/away direction, normalized score, and reviewed provider team identity. Missing, duplicate, or conflicting candidates fail.

The raw OpenLigaDB captures and the portions of `history-played-dates.csv` derived from them are made available under ODbL 1.0 with the attribution and license notice in `data/bundesliga-2026-27/history/SOURCES.md`. Application and tooling code remains under the repository's MIT license. The fixed `transfermarkt-datasets` CC0 revision remains the source for covered Bundesliga, DFB-Pokal, and UEFA rows.

Official DFB schedule and match pages remain manual factual corroboration only. Their terms do not permit this repository to automate extraction or redistribute the portal database, so they are never ingested into the captures or seed.

Source conflicts are not resolved by priority. Any disagreement among the checked map, OpenLigaDB capture, DuckDB source, DFB corroboration, or current-season competition-scoped Kicktipp outcomes fails closed and requires a reviewed source update.

## Alternatives considered

- **Keep the accepted openfootball file:** Rejected after verification proved its late-season dates/results incomplete.
- **Use the live OpenLigaDB API at collection time:** Rejected because deterministic launch history requires immutable, last-known-good evidence without unattended networking.
- **Copy DFB Data Center results:** Rejected because its published terms do not grant the required automated extraction or repository reuse.

## Consequences

- The remaining 2. Bundesliga and relegation rows have complete, lawful source evidence with stable match IDs.
- Captured OpenLigaDB data and its derived map rows carry an ODbL notice distinct from MIT-licensed code.
- A future refresh must capture, hash, validate, attribute, review, and commit a new response; changing an API response never changes runtime behavior by itself.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0027's accepted `openfootball/deutschland` source choice. ADR-0027's rejection of DFB ingestion and its fixed-source/conflict requirements remain valid.
