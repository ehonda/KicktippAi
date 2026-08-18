# ADR-0017: Fix roster collector DuckDB and reconstruction contract

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0011 fixes the roster selection gates and output schemas, but it intentionally does not name the local DuckDB relations needed to evaluate those gates or define the metadata needed to reconstruct a last-known-good membership with stable identifiers. The prompt CSVs do not contain stable player IDs, so parsing them alone would make the overlap gate depend on a lossy reconstruction.

P0-09 must remain local-path-only. It must neither refresh the WM26 cache nor use a filesystem timestamp as membership provenance.

## Decision

The Bundesliga roster adapter accepts DuckDB only through an explicit local `--duckdb-path`, `--duckdb-revision`, and `--duckdb-snapshot-date`. It opens the file read-only and treats missing paths, missing required schema, query failures, or conversion failures as unavailable DuckDB input; it never downloads or refreshes a database.

The eligible membership schema is the following exact logical projection. Different physical types are allowed only when they can be converted without loss to the stated value.

```text
clubs(club_id, domestic_competition_id, last_season, squad_size, coach_name)
players(player_id, name, current_club_id, last_season, date_of_birth, position)
player_valuations(player_id, date, market_value_in_eur)
```

For each manifest club ID, `clubs` is queried directly by `club_id`; its exact-one result provides the club gate fields and `coach_name`. `players` is queried directly by `current_club_id`; its rows provide the membership gate fields and identity. `player_valuations` is restricted to the selected player IDs, positive values, and `date <= Membership_As_Of`; the latest date wins, with equal-date values required to agree. No games, transfers, names, current market-value columns, or inferred joins establish membership or enrichment. Positions are mapped only from the selected player row's exact `position` text to the four ADR-0011 categories; unknown values are `N/A` with coverage diagnostics.

Published roster metadata has object root and `contract` equal to `bundesliga-roster-publication/v1`. It stores the rendered quality-report CSV and one slug-ordered membership record per club containing the selected source, membership date, source references, source revision, DuckDB snapshot date, selection reason, diagnostics, and the exact coach/player identity list (`role`, normalized name, nullable Transfermarkt player ID). Last-known-good reconstruction must first validate the headed atomic snapshot through `DocumentPublicationContract.ValidateLoaded`, then validate this metadata, every canonical per-team CSV, the aggregate CSV, and the KPI summary against one another. Prompt CSV content by itself is never a last-known-good source.

On an enrichment query/schema failure, the collector retains a valid headed last-known-good snapshot. If no headed snapshot exists, a complete valid seed may publish with `N/A` enrichment values and an explicit `ENRICHMENT_UNAVAILABLE` diagnostic, as ADR-0011 permits. Successful queries with missing individual cells remain publishable with `N/A` coverage diagnostics.

## Alternatives considered

- **Reuse the WM26 cache downloader and its national-team schema:** Rejected because it would change the historical WM26 behavior and supplies neither the Bundesliga membership gates nor approved provenance.
- **Infer membership from transfers, player names, or current market values:** Rejected by ADR-0003 and ADR-0011.
- **Reconstruct LKG from the prompt CSVs only:** Rejected because the stable player IDs required for future DuckDB overlap are absent from those documents.

## Consequences

- DuckDB support is deterministic and testable from small local fixtures.
- A dataset with a changed schema safely falls back instead of silently weakening membership rules.
- Snapshot metadata carries the minimum identity/provenance needed for strict LKG reconstruction.

## Affected tasks

- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

None.
