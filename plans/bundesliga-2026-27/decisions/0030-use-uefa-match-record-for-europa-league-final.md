# ADR-0030: Use the UEFA match record for the Europa League final

- Status: Accepted
- Date: 2026-08-21

## Context

The pinned `transfermarkt-datasets` revision resolves every selected Europa League identity except the 2026 final between SC Freiburg and Aston Villa. That exact identity appears in two selected inventory rows. OpenLigaDB's `uel/2025` endpoint lists the fixture, but its current response is stale: it marks every knockout match unfinished and presents the final with a placeholder 0:0 result. It is not completed-match evidence and must not be treated as a valid-source conflict.

UEFA publishes the match-specific record as match `2047743` at `https://www.uefa.com/uefaeuropaleague/match/2047743/`, identifying the final played on 2026-05-20 with SC Freiburg 0:3 Aston Villa. UEFA also links a match-specific statistics PDF. Bounded direct-CDN and in-app-browser download attempts were unavailable because the CDN reset the connection and the browser runtime was unavailable, so no byte hash can truthfully be recorded. SC Freiburg's official report independently corroborates the exact date, teams, competition, and result.

## Decision

Use the stable official identity `UEFA match 2047743` and match-specific URL as the fixed source revision for this single factual record. Store only the two deterministic map rows that share this match identity; do not store or redistribute UEFA page or PDF content and do not scrape a broader UEFA database.

Both map rows must be exactly `EL`, SC Freiburg home, Aston Villa away, normalized score `0:3`, played date `2026-05-20`, source match ID `2047743`, source name `UEFA`, source URL `https://www.uefa.com/uefaeuropaleague/match/2047743/`, source revision `UEFA-match-2047743`, and the checked verification timestamp. The rows must be the exact `home-history-scf.csv` and `recent-history-scf.csv` inventory identities. Any missing row, additional use of this source, field disagreement, or duplicate within one document fails validation.

Record SC Freiburg's official report at `https://www.scfreiburg.com/aktuell/nachrichten/profis/spielberichte/2025-26/finalniederlage-gegen-villa` as independent manual corroboration. Runtime performs no network fetch.

## Alternatives considered

- **Use the unfinished OpenLigaDB record:** Rejected because its placeholder completion state and 0:0 result conflict with the completed 0:3 identity.
- **Require the UEFA PDF SHA-256:** Rejected as the immediate gate after bounded host and browser download paths were unavailable. The stable official match ID and URL, plus independent club corroboration, are sufficient for this one factual record.
- **Copy or scrape UEFA content:** Rejected because P0-22 needs only the factual identity/date row and has no reason to redistribute content or ingest a broader database.
- **Infer the final date from the competition calendar:** Rejected because an exact official match record is available.

## Consequences

- Under ADR-0032's superseding complete preseason inventory, all 398 selected completed rows have exact source coverage without guessing the final date; UEFA match 2047743 still covers exactly the same two rows.
- The UEFA exception is deliberately limited to one unique match identity repeated in two transparent rows.
- A future change to either official record requires a reviewed source update and a superseding decision.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0025 only where it limits the initial external map to the pinned DuckDB revision. All fixed-source, exact-identity, conflict, no-runtime-fetch, and last-known-good requirements remain valid.
