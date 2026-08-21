# ADR-0029: Capture the OpenLigaDB DFB-Pokal final

- Status: Accepted
- Date: 2026-08-21

## Context

ADR-0028 limits OpenLigaDB capture to the 2025/26 2. Bundesliga and Bundesliga relegation endpoints. Exact inventory matching against the pinned `transfermarkt-datasets` revision resolves every selected DFB-Pokal identity except the 2026 final between FC Bayern München and VfB Stuttgart. The DuckDB artifact ends at the semifinals, while the same final occurs in three selected inventory rows. Guessing its date or copying the official DFB portal is not permitted.

OpenLigaDB's public `dfb/2025` response contains all 63 completed 2025/26 DFB-Pokal matches, including the missing final with a stable match ID, exact local timestamp, teams, and one full-time result. It is available under the same ODbL 1.0 contract accepted by ADR-0028.

## Decision

Add a one-time capture of `https://api.openligadb.de/getmatchdata/dfb/2025` under `data/bundesliga-2026-27/history/sources/`. Its byte-level SHA-256 is the immutable source revision, and runtime never fetches the endpoint.

Validate the capture before freezing it: exactly 63 unique matches, all 63 marked completed, an exact parseable local datetime and exactly one full-time result (`resultTypeID == 2`) for every match. Use this capture only when an exact DFB-Pokal inventory identity has no candidate in the pinned DuckDB source. For P0-22 that scope is one unique final identity shared by three inventory rows. Record both the unique consumed-match count and inventory-row coverage so repeated rows remain explicit.

Every consumed row must join the exact selected document and completed-row ordinal, home/away direction, normalized score, reviewed provider team identity, and source match ID. Missing or duplicate candidates and any disagreement with another checked source fail closed. The raw response and its derived map rows carry the ODbL attribution and license notice in `data/bundesliga-2026-27/history/SOURCES.md`; application code remains MIT-licensed.

Official DFB pages remain manual factual corroboration only and are never automated ingestion or redistributed source data.

## Alternatives considered

- **Leave the three rows unresolved:** Rejected because P0-22 requires complete, exact played dates for every selected completed history row.
- **Copy the final from the DFB Data Center:** Rejected because its terms do not grant the required automated extraction or repository reuse.
- **Fetch OpenLigaDB at runtime:** Rejected because launch history must remain deterministic, reviewable, and network-independent.

## Consequences

- The single DuckDB-missing DFB-Pokal final has lawful, immutable evidence without broadening runtime networking.
- The full captured response is retained and validated, but only the one missing exact identity contributes to the map.
- A future source refresh requires a new captured response, hash, validation, attribution, and accepted decision.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0028 only where it limits the OpenLigaDB endpoint scope to `bl2/2025` and `rel/2025`. ADR-0028's source validation, attribution, conflict, fixed-capture, and no-runtime-fetch requirements remain valid.
